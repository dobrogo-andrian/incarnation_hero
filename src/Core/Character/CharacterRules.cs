using Core.Content;
using Core.State;

namespace Core.Character;

/// <summary>One modifier's part in a computed value, for "where does this number come from".</summary>
public sealed record ModifierPart(string SourceId, ModifierOp Op, double Value);

/// <summary>A computed value with its base and every modifier that shaped it.</summary>
public sealed record StatBreakdown(string Id, double Base, IReadOnlyList<ModifierPart> Modifiers, double Value);

/// <summary>
/// Computes the character's effective values (§2, tech 2.5). Values form a chain, stat → aspect →
/// resource maximum: each link takes its base from the link before and then applies its own
/// modifiers with the same rule, so one mechanism explains every number.
/// </summary>
public sealed class CharacterRules
{
    private readonly ContentDb _db;

    /// <summary>Lookup only, never iterated.</summary>
    private readonly Dictionary<string, StatDef> _statOfAspect = new(StringComparer.Ordinal);

    public CharacterRules(ContentDb db)
    {
        _db = db;
        var config = db.Get<RunConfigDef>(RunConfigDef.RunId);
        Stats = config.Stats.Select(db.Get<StatDef>).ToList();
        Resources = config.Resources.Select(db.Get<ResourceDef>).ToList();
        StartAspectProfile = config.StartAspectProfile;
        foreach (var stat in Stats)
        {
            foreach (string aspect in stat.Aspects)
                _statOfAspect.Add(aspect, stat);
        }
    }

    /// <summary>The character's stats in display order.</summary>
    public IReadOnlyList<StatDef> Stats { get; }

    /// <summary>The character's resources in display order.</summary>
    public IReadOnlyList<ResourceDef> Resources { get; }

    public string StartAspectProfile { get; }

    /// <summary>Gives a new character its starting stats and profile, with every resource full.</summary>
    public void InitPlayer(PlayerState player)
    {
        player.Stats = Stats.Select(s => new StatValue { StatId = s.Id, Base = s.StartValue }).ToList();
        player.AspectProfileId = StartAspectProfile;
        player.Resources = Resources.Select(r => new ResourceValue { ResourceId = r.Id, Current = r.StartEmpty ? 0 : Value(player, r.Id) }).ToList();
    }

    public double Value(PlayerState player, string id) => Explain(player, id).Value;

    public StatBreakdown Explain(PlayerState player, string id) => ApplyModifiers(id, BaseOf(player, id), player.Modifiers);

    /// <summary>
    /// The modifier rule: (base + ΣAdd) × (1 + ΣMul), then the lowest Cap; an Override replaces the
    /// result entirely, the latest one winning. Percentages add up: +10% and +10% make +20%.
    /// </summary>
    public static StatBreakdown ApplyModifiers(string id, double baseValue, IEnumerable<Modifier> modifiers)
    {
        var own = modifiers.Where(m => m.TargetId == id).ToList();
        double add = 0, mul = 0;
        double? cap = null, overridden = null;
        foreach (var m in own)
        {
            switch (m.Op)
            {
                case ModifierOp.Add: add += m.Value; break;
                case ModifierOp.Mul: mul += m.Value; break;
                case ModifierOp.Cap: cap = cap is { } c ? Math.Min(c, m.Value) : m.Value; break;
                case ModifierOp.Override: overridden = m.Value; break;
            }
        }

        double value = overridden ?? Math.Min((baseValue + add) * (1 + mul), cap ?? double.MaxValue);
        return new StatBreakdown(id, baseValue, own.Select(m => new ModifierPart(m.SourceId, m.Op, m.Value)).ToList(), value);
    }

    private double BaseOf(PlayerState player, string id)
    {
        if (_db.TryGet<StatDef>(id, out _))
            return player.Stats.First(s => s.StatId == id).Base;

        if (_statOfAspect.TryGetValue(id, out var stat))
        {
            double share = _db.Get<AspectProfileDef>(player.AspectProfileId).Shares[id];
            return Value(player, stat.Id) * stat.Aspects.Count * share;
        }

        if (_db.TryGet<ResourceDef>(id, out var resource))
            return resource.Base + (resource.Source is { } source ? resource.Coefficient * Value(player, source) : 0);

        throw new KeyNotFoundException($"'{id}' is not a stat, aspect or resource of the character.");
    }
}
