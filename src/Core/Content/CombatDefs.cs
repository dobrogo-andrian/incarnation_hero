namespace Core.Content;

/// <summary>
/// One enemy (<c>enemy.*</c>): a character snapshot (D42). Its values come from the same rules as
/// the player's, so a stat missing here is 0 and a beast without meridians has no qi shield.
/// </summary>
public sealed class EnemyDef : ContentDef, IValidatedDef
{
    public LocalizedText Name { get; set; } = new();

    /// <summary>Qi stage index, as on the player's track: 0 is mortal.</summary>
    public int Stage { get; set; }

    /// <summary>Base stat values.</summary>
    [ContentRef(typeof(StatDef))]
    public Dictionary<string, double> Stats { get; set; } = [];

    /// <summary>How its stats divide between aspects; the run's starting profile when not given.</summary>
    [ContentRef(typeof(AspectProfileDef))]
    public string? AspectProfile { get; set; }

    /// <summary>Damage of its weapon, claws or fangs; <c>config.combat.weapon_damage</c> when not given.</summary>
    public double? WeaponDamage { get; set; }

    public int TechniqueLevel { get; set; }

    /// <summary>The injury scale its hits leave; <c>config.combat.body_injury</c> when not given.</summary>
    [ContentRef(typeof(InjuryDef))]
    public string? Injury { get; set; }

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (Stage < 0 || TechniqueLevel < 0 || WeaponDamage <= 0 || Stats.Values.Any(v => v < 0))
            yield return "needs a stage, technique level and stats of zero or more and a positive weapon_damage.";
    }
}

/// <summary>Enemies an event can make the player fight (<c>grp.*</c>), and what beating them yields.</summary>
public sealed class EnemyGroupDef : ContentDef, IValidatedDef
{
    public LocalizedText Name { get; set; } = new();

    public List<GroupMemberDef> Members { get; set; } = [];

    public LootDef Loot { get; set; } = new();

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (Members.Count == 0 || Members.Any(m => m.Count < 1))
            yield return "needs members, each at least once.";
    }
}

public sealed class GroupMemberDef
{
    [ContentRef(typeof(EnemyDef))]
    public string Enemy { get; set; } = "";

    public int Count { get; set; } = 1;
}

/// <summary>What a won fight yields (rolled on the loot stream): stones in a range and items, each with its own chance.</summary>
public sealed class LootDef : IValidatedDef
{
    public int StonesMin { get; set; }
    public int StonesMax { get; set; }

    public List<LootItemDef> Items { get; set; } = [];

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (StonesMin < 0 || StonesMax < StonesMin)
            yield return "loot needs 0 <= stones_min <= stones_max.";
        if (Items.Any(i => i.Chance is <= 0 or > 1 || i.Count < 1))
            yield return "loot items need a chance above 0 up to 1 and a count of at least 1.";
    }
}

public sealed class LootItemDef
{
    [ContentRef(typeof(ItemDef))]
    public string Item { get; set; } = "";

    public double Chance { get; set; }

    public int Count { get; set; } = 1;
}

/// <summary>
/// The stage 1 formula resolver (<c>config.combat</c>, tech 4.1, D42): abstract rounds of the
/// shield → health → injuries pipeline. Draft numbers until Sim.Cli reports.
/// </summary>
public sealed class CombatConfigDef : ContentDef, IValidatedDef
{
    public const string CombatId = "config.combat";

    /// <summary>Damage of the base weapon everyone fights with until weapons are items.</summary>
    public double WeaponDamage { get; set; }

    /// <summary>Damage × (1 + best technique level × this).</summary>
    public double TechniqueBonusPerLevel { get; set; }

    /// <summary>Damage × (1 + qi stage × this): denser qi hits harder.</summary>
    public double QiDensityPerStage { get; set; }

    /// <summary>Each hit is its damage × a uniform roll of 1 ± this.</summary>
    public double DamageSpread { get; set; }

    [ContentRef(typeof(ResourceDef))]
    public string HealthResource { get; set; } = "";

    /// <summary>The qi shield: hits break it before they reach health (§7.4).</summary>
    [ContentRef(typeof(ResourceDef))]
    public string ShieldResource { get; set; } = "";

    /// <summary>The pool that refills the shield every round; an empty pool refills nothing (§7.2).</summary>
    [ContentRef(typeof(ResourceDef))]
    public string QiPoolResource { get; set; } = "";

    /// <summary>The shield refills by this aspect × <see cref="RefillPerFlow"/> a round.</summary>
    [ContentRef(typeof(AspectDef))]
    public string FlowAspect { get; set; } = "";

    public double RefillPerFlow { get; set; }

    /// <summary>Turn order (ties by ID).</summary>
    [ContentRef(typeof(AspectDef))]
    public string ReactionAspect { get; set; } = "";

    /// <summary>The player's side of the escape chance (§7.6).</summary>
    [ContentRef(typeof(AspectDef))]
    public string AgilityAspect { get; set; } = "";

    /// <summary>Damage to health over this aspect gives injuries (§8).</summary>
    [ContentRef(typeof(AspectDef))]
    public string ResilienceAspect { get; set; } = "";

    /// <summary>The scale hits wound by default.</summary>
    [ContentRef(typeof(InjuryDef))]
    public string BodyInjury { get; set; } = "";

    /// <summary>Injury points = damage to health / resilience × this.</summary>
    public double InjuryPerResilience { get; set; }

    /// <summary>A hit that breaks the shield hurts the meridians with this chance, by this many points (§7.4).</summary>
    [ContentRef(typeof(InjuryDef))]
    public string ShieldBreakInjury { get; set; } = "";

    public double ShieldBreakChance { get; set; }
    public double ShieldBreakAmount { get; set; }

    /// <summary>Below this share of maximum health the player tries to escape instead of attacking.</summary>
    public double FleeHealthShare { get; set; }

    /// <summary>Escape chance = base + (agility − the fastest enemy's reaction) × per point, clamped.</summary>
    public double FleeChanceBase { get; set; }
    public double FleeChancePerPoint { get; set; }
    public double FleeChanceMin { get; set; }
    public double FleeChanceMax { get; set; }

    /// <summary>Hunting and the arena (§5) take no one below this share of maximum health: a spent body stops them.</summary>
    public double FightMinHealthShare { get; set; }

    /// <summary>A fight still undecided after this many rounds ends with the player withdrawing.</summary>
    public int MaxRounds { get; set; }

    /// <summary>Combat insight = difficulty × this, at most <see cref="InsightMax"/>; difficulty is the enemies' threat over the player's.</summary>
    public double InsightPerDifficulty { get; set; }
    public double InsightMax { get; set; }

    /// <summary>Share of the insight an escape still gives.</summary>
    public double InsightFledShare { get; set; }

    /// <summary>The hex battlefield reaches this many steps from its centre (D56).</summary>
    public int FieldRadius { get; set; }

    /// <summary>CTB (D53): a fighter acts when its gauge reaches this; the gauge fills by reaction × <see cref="InitiativePerReaction"/> a tick.</summary>
    public int InitiativeThreshold { get; set; }
    public double InitiativePerReaction { get; set; }

    /// <summary>The body's energy reserve (D55); it does not come back during a fight.</summary>
    [ContentRef(typeof(ResourceDef))]
    public string EnergyResource { get; set; } = "";

    /// <summary>Energy flows from the reserve by this aspect × <see cref="EnergyRefillPerOutput"/> × the share of health left, at the end of each own turn.</summary>
    [ContentRef(typeof(AspectDef))]
    public string EnergyOutputAspect { get; set; } = "";

    public double EnergyRefillPerOutput { get; set; }

    /// <summary>What flows every turn whatever the body's state, even from an empty reserve.</summary>
    public double EnergyRefillMin { get; set; }

    /// <summary>Energy ready to spend holds at most output × this (and never less than the minimum refill).</summary>
    public double EnergyCapacityPerOutput { get; set; }

    /// <summary>Body energy the basic weapon attack costs (D56).</summary>
    public double AttackEnergy { get; set; }

    /// <summary>Steps a turn = agility × this, rounded down, at least 1 (D56).</summary>
    public double MovePointsPerAgility { get; set; }

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (WeaponDamage <= 0 || MaxRounds < 1 || FieldRadius < 1)
            yield return "needs a positive weapon_damage, max_rounds and field_radius.";
        if (InitiativeThreshold < 1 || InitiativePerReaction <= 0 || EnergyRefillPerOutput < 0 || EnergyRefillMin < 0 || EnergyCapacityPerOutput <= 0 || AttackEnergy < 0 || MovePointsPerAgility <= 0)
            yield return "needs a positive initiative_threshold, initiative_per_reaction, energy_capacity_per_output and move_points_per_agility, and energy refills and costs of zero or more.";
        if (DamageSpread is < 0 or >= 1 || FleeHealthShare is < 0 or > 1 || InsightFledShare is < 0 or > 1
            || ShieldBreakChance is < 0 or > 1 || FightMinHealthShare is < 0 or > 1 || FleeChanceMin < 0 || FleeChanceMax > 1 || FleeChanceMin > FleeChanceMax)
            yield return "damage_spread, flee_health_share, insight_fled_share, shield_break_chance, fight_min_health_share and the flee chance bounds must be fractions.";
    }
}
