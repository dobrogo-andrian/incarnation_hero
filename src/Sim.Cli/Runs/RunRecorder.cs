using Core.Combat;
using Core.Content;
using Core.Sim;
using Core.State;

namespace Sim.Cli.Runs;

/// <summary>
/// Collects a run's metrics (D43) from each executed command, the view before it and the events it
/// produced. Unlike a bot, the recorder may read the true state: it measures, it does not play.
/// </summary>
public sealed class RunRecorder
{
    /// <summary>An injury scale at this share of its maximum counts as heavy for the death-spiral metric.</summary>
    public const double HeavyInjuryShare = 0.5;

    private readonly Simulation _sim;
    private readonly string _bot;
    private readonly RunConfigDef _run;
    private readonly int _startStones;

    private readonly SortedDictionary<string, int> _activityWeeks = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, int> _events = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, int> _rumors = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, int> _earned = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, int> _spent = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, int> _bought = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, int> _taken = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, CombatGroupSummary> _combat = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, double> _peakInjuries = new(StringComparer.Ordinal);
    private readonly List<BreakthroughRecord> _breakthroughs = [];

    private BreakthroughRecord? _pending;
    private string? _eventId;
    private int _healingWeeks;
    private double _insight;
    private double _peakToxins;
    private int? _firstHeavyWeek;
    private int? _progressWeek;
    private double _progressMark;

    public RunRecorder(Simulation sim, string bot)
    {
        _sim = sim;
        _bot = bot;
        _run = sim.Content.Get<RunConfigDef>(RunConfigDef.RunId);
        _startStones = sim.State.Player.Stones;
        _progressMark = ProgressMark();
    }

    public void Record(PlayerView before, Command command, IReadOnlyList<SimEvent> events)
    {
        string? activity = ActivityKey(command, before);
        bool healing = before.Injuries.Count > 0 && activity is { } key && IsHealing(key);
        if (command is AttemptBreakthrough attempt)
            _pending = Pending(before, attempt.VectorId);

        string? lootGroup = null;
        foreach (var e in events)
        {
            switch (e)
            {
                case WeekPassed:
                    Add(_activityWeeks, activity ?? "other", 1);
                    if (healing)
                        _healingWeeks++;
                    break;
                case EventStarted started:
                    _eventId = started.EventId;
                    Add(_events, started.EventId, 1);
                    break;
                case EventEnded:
                    _eventId = null;
                    break;
                case RumorHeard heard:
                    Add(_rumors, heard.RumorId, 1);
                    break;
                case CombatResolved fight:
                    lootGroup = fight.GroupId;
                    var counts = _combat.GetValueOrDefault(fight.GroupId, new CombatGroupSummary(0, 0, 0));
                    _combat[fight.GroupId] = fight.End switch
                    {
                        CombatEnd.Won => counts with { Won = counts.Won + 1 },
                        CombatEnd.Fled or CombatEnd.Yielded => counts with { Fled = counts.Fled + 1 },
                        _ => counts with { Died = counts.Died + 1 },
                    };
                    break;
                case BuffGained { Kind: BuffKind.CombatInsight } insight:
                    _insight += insight.Points;
                    break;
                case StonesChanged { Amount: not 0 } stones:
                    string source = lootGroup is not null ? "loot:" + lootGroup : _eventId ?? StoneSink(command);
                    Add(stones.Amount > 0 ? _earned : _spent, source, Math.Abs(stones.Amount));
                    break;
                case ItemsChanged { Count: > 0 } items when command is Buy:
                    Add(_bought, items.ItemId, items.Count);
                    break;
                case PillTaken pill:
                    Add(_taken, pill.ItemId, 1);
                    break;
                case BreakthroughSucceeded success when _pending is not null:
                    Resolve(_pending with { Result = "success", Rank = success.RankId });
                    break;
                case BreakthroughFailed failure when _pending is not null:
                    Resolve(_pending with { Result = "failed", FailureZone = failure.FailureZoneId });
                    break;
                case RunEnded { Cause: RunEndCause.BreakthroughDeath } when _pending is not null:
                    Resolve(_pending with { Result = "death" });
                    break;
            }
        }
        TrackState(before.Week);
    }

    public RunSummary Finish()
    {
        var state = _sim.State;
        var player = state.Player;
        return new RunSummary(
            state.Seed,
            _bot,
            state.EndCause?.ToString() ?? "none",
            player.AgeWeeks,
            Round(player.AgeWeeks / (double)_run.WeeksPerYear),
            QiTrack().StageIndex,
            state.Week,
            _breakthroughs,
            _activityWeeks,
            _healingWeeks,
            _events,
            _rumors,
            new CombatSummary(
                _combat.Values.Sum(c => c.Won + c.Fled + c.Died),
                _combat.Values.Sum(c => c.Won),
                _combat.Values.Sum(c => c.Fled),
                _combat.Values.Sum(c => c.Died),
                Round(_insight),
                _combat),
            new StonesSummary(_startStones, player.Stones, _earned, _spent),
            new PillsSummary(_bought, _taken, Round(_peakToxins)),
            new InjurySummary(
                _peakInjuries,
                _firstHeavyWeek,
                _firstHeavyWeek is { } heavy ? (_progressWeek ?? state.Week) - heavy : null,
                _firstHeavyWeek is null ? null : _progressWeek is not null));
    }

    /// <summary>What the weeks of <paramref name="command"/> are spent on; meditation is keyed by its direction.</summary>
    public static string? ActivityKey(Command command, PlayerView before) => command switch
    {
        StartActivity start => RunStatsSystem.ActivityKey(start.Activity),
        Travel => "travel",
        AttemptBreakthrough => "breakthrough",
        ChooseEventOption => "event",
        _ => null,
    };

    /// <summary>Rest, or meditation aimed at an injury scale.</summary>
    private bool IsHealing(string activityKey) =>
        activityKey == "wait"
        || (_sim.Content.TryGet<MeditationDirectionDef>(activityKey, out var direction)
            && direction.Target is { } target && _sim.Content.TryGet<InjuryDef>(target, out _));

    private static string StoneSink(Command command) => command switch
    {
        Buy => "shop",
        RestoreQiWithStones => "qi_restore",
        RefillPlaceWithStones => "place_refill",
        _ => "other",
    };

    private BreakthroughRecord? Pending(PlayerView before, string vectorId)
    {
        var track = before.Tracks.First(t => t.VectorId == vectorId);
        if (track.Forecast is not { } forecast)
            return null;
        var toxins = before.Resources.First(r => r.ResourceId == _run.ToxinsResource);
        return new BreakthroughRecord(
            before.Week,
            Round(before.AgeWeeks / (double)_run.WeeksPerYear),
            track.StageIndex,
            Result: "pending",
            Rank: null,
            forecast.FailureZoneId,
            Round(forecast.Threshold),
            Round(forecast.Foundation),
            Round(before.Foundation.Purity),
            Round(before.Foundation.Knowledge),
            Round(Math.Max(0, (forecast.Threshold - forecast.Foundation) / forecast.Threshold)),
            Round(forecast.FailureChance),
            Round(toxins.Current));
    }

    private void Resolve(BreakthroughRecord record)
    {
        _breakthroughs.Add(record);
        _pending = null;
    }

    /// <summary>Peaks of toxins and injuries, and the death-spiral marks, after a command that started in <paramref name="startWeek"/>.</summary>
    private void TrackState(int startWeek)
    {
        var player = _sim.State.Player;
        var toxins = player.Resources.First(r => r.ResourceId == _run.ToxinsResource);
        _peakToxins = Math.Max(_peakToxins, toxins.Current);

        double mark = ProgressMark();
        if (mark > _progressMark && _firstHeavyWeek is not null && _progressWeek is null)
            _progressWeek = Math.Max(_firstHeavyWeek.Value, startWeek + 1);
        _progressMark = mark;

        foreach (var injury in player.Injuries)
        {
            _peakInjuries[injury.InjuryId] = Round(Math.Max(_peakInjuries.GetValueOrDefault(injury.InjuryId), injury.Value));
            if (_firstHeavyWeek is null && injury.Value >= HeavyInjuryShare * _sim.Content.Get<InjuryDef>(injury.InjuryId).Max)
                _firstHeavyWeek = _sim.State.Week;
        }
    }

    /// <summary>Grows with every point of stage progress and every stage, even though a breakthrough empties the scale.</summary>
    private double ProgressMark()
    {
        var track = QiTrack();
        return track.StageIndex * 1_000_000 + track.StageProgress;
    }

    private CultivationTrack QiTrack() => _sim.State.Player.Tracks.First(t => t.VectorId == Simulation.QiVectorId);

    private static void Add(SortedDictionary<string, int> counts, string key, int amount) =>
        counts[key] = counts.GetValueOrDefault(key) + amount;

    private static double Round(double value) => Math.Round(value, 2);
}
