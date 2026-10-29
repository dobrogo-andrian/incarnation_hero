namespace Core.Content;

/// <summary>A point of a zone the player steps into and reads about (D46): a place, shop, hunting ground or arena.</summary>
public interface IDescribedDef
{
    LocalizedText? Description { get; }
}

/// <summary>
/// What the weeks of a training activity (§5) are spent on, shared by jobs, hunting grounds and
/// arenas: each week adds <see cref="Gains"/> points to the stats' scales (D48).
/// </summary>
public abstract class TrainingDef : ContentDef, IValidatedDef
{
    public LocalizedText Name { get; set; } = new();

    [ContentRef(typeof(ZoneDef))]
    public string Zone { get; set; } = "";

    /// <summary>Stat → points a week.</summary>
    [ContentRef(typeof(StatDef))]
    public Dictionary<string, double> Gains { get; set; } = [];

    public virtual IEnumerable<string> Validate(ContentDb db)
    {
        if (Gains.Values.Any(g => g < 0))
            yield return "gains cannot be negative.";
    }
}

/// <summary>
/// Hired work for a mortal (<c>job.*</c>): a small wage every week and the body it trains, with no
/// rare finds, since guaranteed pay rules them out (§13.2). A settlement's jobs share its job board.
/// </summary>
public sealed class JobDef : TrainingDef
{
    /// <summary>Spirit stones paid each week.</summary>
    public int Pay { get; set; }

    public override IEnumerable<string> Validate(ContentDb db)
    {
        foreach (string error in base.Validate(db))
            yield return error;
        if (Pay < 0)
            yield return "pay cannot be negative.";
    }
}

/// <summary>
/// A hunting ground (<c>hunt.*</c>, §5): each week of hunting may meet one of its beast groups,
/// a real fight that can kill; a won fight yields the group's loot (pelts sold in the settlement).
/// </summary>
public sealed class HuntDef : TrainingDef, IDescribedDef
{
    /// <summary>Chance a week of hunting finds prey.</summary>
    public double EncounterChance { get; set; }

    /// <summary>Group → weight of meeting it.</summary>
    [ContentRef(typeof(EnemyGroupDef))]
    public Dictionary<string, double> Groups { get; set; } = [];

    /// <summary>Its point on a settlement's local map (D44); wild zones have no local map.</summary>
    public MapPoint? Map { get; set; }

    public LocalizedText? Description { get; set; }

    public override IEnumerable<string> Validate(ContentDb db)
    {
        foreach (string error in base.Validate(db))
            yield return error;
        if (EncounterChance is < 0 or > 1)
            yield return "encounter_chance must be from 0 to 1.";
        if (Groups.Count == 0 || Groups.Values.Any(w => w <= 0))
            yield return "needs groups with positive weights.";
    }
}

/// <summary>
/// An arena (<c>arena.*</c>, §5): each week one bout against the picked rank of opponents. Bouts
/// end when the player yields, never in death; the fee is paid before the bout, the prize only for a win.
/// </summary>
public sealed class ArenaDef : TrainingDef, IDescribedDef
{
    public List<ArenaBoutDef> Bouts { get; set; } = [];

    public MapPoint? Map { get; set; }

    public LocalizedText? Description { get; set; }

    public override IEnumerable<string> Validate(ContentDb db)
    {
        foreach (string error in base.Validate(db))
            yield return error;
        if (Bouts.Count == 0 || Bouts.Any(b => b.Fee < 0 || b.Prize < 0))
            yield return "needs bouts with a fee and a prize of zero or more.";
    }
}

/// <summary>One rank of arena opponents.</summary>
public sealed class ArenaBoutDef
{
    [ContentRef(typeof(EnemyGroupDef))]
    public string Group { get; set; } = "";

    public int Fee { get; set; }

    public int Prize { get; set; }
}

/// <summary>A part of a settlement's local map that groups its points by what they are for (<c>district.*</c>).</summary>
public sealed class DistrictDef : ContentDef
{
    public LocalizedText Name { get; set; } = new();

    [ContentRef(typeof(ZoneDef))]
    public string Zone { get; set; } = "";
}
