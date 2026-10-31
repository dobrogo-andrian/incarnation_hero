using Core.Content;
using Core.Rng;
using Core.Sim;
using Core.State;

namespace Sim.Cli.Bots;

/// <summary>
/// The baseline (D43): every command it can form from the view, shuffled, so the runner executes
/// a uniformly random one among those Core accepts. Open-ended activities get 4–26 weeks; event
/// options are picked at random too, and a breakthrough may accept the risk of death.
/// </summary>
public sealed class RandomBot(ContentDb content, Xoshiro256StarStar rng) : IBot
{
    private const int MinWeeks = 4;
    private const int MaxWeeks = 26;

    private readonly WorldMemory _memory = new(content);

    public string Name => "random";

    public void Observe(IReadOnlyList<SimEvent> events) => _memory.Observe(events);

    public IEnumerable<Command> Propose(PlayerView view)
    {
        var candidates = view.Mode == GameMode.EventDialog ? EventCommands(view) : OverworldCommands(view);
        for (int n = candidates.Count; n > 1; n--)
        {
            int k = rng.NextInt(0, n);
            (candidates[n - 1], candidates[k]) = (candidates[k], candidates[n - 1]);
        }
        return candidates;
    }

    private static List<Command> EventCommands(PlayerView view) =>
        view.Event!.Options.Count == 0
            ? [new ChooseEventOption(0)]
            : view.Event.Options.Select(o => (Command)new ChooseEventOption(o.Index)).ToList();

    private List<Command> OverworldCommands(PlayerView view)
    {
        var commands = new List<Command>();
        commands.Add(Start(new WaitActivity()));
        commands.Add(Start(new AdventureActivity()));
        var subjects = view.Items.Select(i => i.ItemId)
            .Concat(content.All<KnowledgeDef>().Where(k => k.IsElement).Select(k => k.Id))
            .ToList();
        foreach (var place in view.Places)
        {
            foreach (var direction in content.All<MeditationDirectionDef>())
            {
                string? subject = direction.Studies && subjects.Count > 0 ? subjects[rng.NextInt(0, subjects.Count)] : null;
                commands.Add(Start(new MeditateActivity(direction.Id, place.PlaceId, subject)));
            }
            commands.Add(new AttemptBreakthrough(Simulation.QiVectorId, place.PlaceId, AcceptDeathRisk: rng.Chance(0.5)));
        }
        commands.AddRange(_memory.KnownZones().Select(z => new Travel(z.Id)));
        commands.AddRange(view.Items.Select(i => new UsePill(i.ItemId)));
        commands.AddRange(view.ShopOffers.Select(o => new Buy(o.ItemId)));
        commands.AddRange(view.Knowledge.Select(k => new SetActiveMethod(k.KnowledgeId)));
        return commands;
    }

    private StartActivity Start(Activity activity) => new(activity, rng.NextInt(MinWeeks, MaxWeeks + 1));
}
