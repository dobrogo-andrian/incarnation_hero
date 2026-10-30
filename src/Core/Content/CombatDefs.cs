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

    /// <summary>The technique level the formula resolver adds to damage (D42); the hex battle uses <see cref="Techniques"/>.</summary>
    public int TechniqueLevel { get; set; }

    /// <summary>Techniques it fights with on the hex field, by level (D56): cultivators have them, beasts do not.</summary>
    [ContentRef(typeof(KnowledgeDef))]
    public Dictionary<string, int> Techniques { get; set; } = [];

    /// <summary>How it fights on the hex field (D59): beasts are aggressive, people cautious.</summary>
    [ContentRef(typeof(AiArchetypeDef))]
    public string Ai { get; set; } = "";

    /// <summary>The injury scale its hits leave; <c>config.combat.body_injury</c> when not given.</summary>
    [ContentRef(typeof(InjuryDef))]
    public string? Injury { get; set; }

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (Stage < 0 || TechniqueLevel < 0 || WeaponDamage <= 0 || Stats.Values.Any(v => v < 0))
            yield return "needs a stage, technique level and stats of zero or more and a positive weapon_damage.";
        if (Ai.Length == 0)
            yield return "needs an ai archetype.";
        if (Techniques.Any(t => t.Value < 1 || db.Get<KnowledgeDef>(t.Key).Combat is null))
            yield return "techniques need a level of at least 1 and a combat block.";
    }
}

/// <summary>
/// A utility AI archetype for the hex battle (tech 4.1, D59): the weights that score each plan
/// of a turn (<c>BattleAi</c>). Below <see cref="FleeHealthShare"/> of health the fighter makes
/// for the rim and escapes; 0 means it never does (beasts fight to the end).
/// </summary>
public sealed class AiArchetypeDef : ContentDef, IValidatedDef
{
    public LocalizedText Name { get; set; } = new();

    /// <summary>Per point of expected damage dealt, up to what the target has left.</summary>
    public double Damage { get; set; }

    /// <summary>For a strike expected to bring its target down.</summary>
    public double Kill { get; set; }

    /// <summary>Per point of damage the enemies in reach of the hex it ends on could deal next turn.</summary>
    public double Risk { get; set; }

    /// <summary>Per point of its own weapon when an enemy is in its reach next turn: what makes a cautious fighter close in at all.</summary>
    public double Opportunity { get; set; }

    /// <summary>Per point of damage of the free attacks the walk provokes (D54).</summary>
    public double FreeAttack { get; set; }

    /// <summary>Per point of combat qi and body energy spent.</summary>
    public double Qi { get; set; }
    public double Energy { get; set; }

    /// <summary>Per hex between the hex it ends on and the nearest enemy.</summary>
    public double Approach { get; set; }

    public double FleeHealthShare { get; set; }

    /// <summary>When fleeing: per hex left to the rim, and for the escape itself.</summary>
    public double Flee { get; set; }

    /// <summary>Each plan's score gets a uniform 0..noise from the <c>ai</c> stream, so equal plans do not always go the same way.</summary>
    public double Noise { get; set; }

    /// <summary>
    /// Whether the fighter strikes at an enemy stepping away from it (D54). Beasts do not (D63):
    /// whoever backs off a beast gets away unhurt, though it still breaks an escape next to it.
    /// </summary>
    public bool FreeAttacks { get; set; } = true;

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (new[] { Damage, Kill, Risk, Opportunity, FreeAttack, Qi, Energy, Approach, Flee, Noise }.Any(w => w < 0) || FleeHealthShare is < 0 or > 1)
            yield return "weights and noise must be zero or more, flee_health_share a fraction.";
    }
}

/// <summary>Enemies an event can make the player fight (<c>grp.*</c>), and what beating them yields.</summary>
public sealed class EnemyGroupDef : ContentDef, IValidatedDef
{
    public LocalizedText Name { get; set; } = new();

    public List<GroupMemberDef> Members { get; set; } = [];

    public LootDef Loot { get; set; } = new();

    /// <summary>
    /// Set for those who spare a beaten player (D62): instead of dying they lose everything they
    /// carry, and are finished off after all with this chance. Not set: the fight is to the death.
    /// </summary>
    public double? KillChance { get; set; }

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (Members.Count == 0 || Members.Any(m => m.Count < 1))
            yield return "needs members, each at least once.";
        if (KillChance is < 0 or > 1)
            yield return "kill_chance must be a fraction.";
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
/// Combat rules (<c>config.combat</c>, tech 4.1): the hex battle (D53–D60) and what a fight leaves
/// on the character: the shield → health → injuries pipeline, insight. Draft numbers until Sim.Cli reports.
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

    /// <summary>The pool that refills the shield every turn; an empty pool refills nothing (§7.2).</summary>
    [ContentRef(typeof(ResourceDef))]
    public string QiPoolResource { get; set; } = "";

    /// <summary>The shield refills by this aspect × <see cref="RefillPerFlow"/> at the end of each own turn.</summary>
    [ContentRef(typeof(AspectDef))]
    public string FlowAspect { get; set; } = "";

    public double RefillPerFlow { get; set; }

    /// <summary>Fills the initiative gauge (D53).</summary>
    [ContentRef(typeof(AspectDef))]
    public string ReactionAspect { get; set; } = "";

    /// <summary>Movement points (D56).</summary>
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

    /// <summary>Hunting and the arena (§5) take no one below this share of maximum health: a spent body stops them.</summary>
    public double FightMinHealthShare { get; set; }

    /// <summary>The hex fight's limit: after this many turns side 0 is taken to have withdrawn (D59).</summary>
    public int MaxTurns { get; set; }

    /// <summary>The archetype that plays the player's side when the AI does (arena runs, auto-resolve).</summary>
    [ContentRef(typeof(AiArchetypeDef))]
    public string PlayerAi { get; set; } = "";

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

    /// <summary>Energy comes back by this aspect × <see cref="EnergyRefillPerOutput"/> × the share of health left, at the end of each own turn.</summary>
    [ContentRef(typeof(AspectDef))]
    public string EnergyOutputAspect { get; set; } = "";

    public double EnergyRefillPerOutput { get; set; }

    /// <summary>What comes back every turn whatever the body's state.</summary>
    public double EnergyRefillMin { get; set; }

    /// <summary>
    /// The fight's energy pool holds output × this (never less than the minimum refill) and starts
    /// full (D55): the body does not run dry, but a turn can spend no more than the pool holds.
    /// </summary>
    public double EnergyCapacityPerOutput { get; set; }

    /// <summary>Body energy the basic weapon attack costs (D56).</summary>
    public double AttackEnergy { get; set; }

    /// <summary>Steps a turn = agility × this, rounded down, at least 1 (D56).</summary>
    public double MovePointsPerAgility { get; set; }

    /// <summary>Body energy each step costs (D55); the movement points still cap the steps of a turn.</summary>
    public double StepEnergy { get; set; }

    /// <summary>Body energy of the other actions (D56): guard and escape.</summary>
    public double GuardEnergy { get; set; }
    public double FleeEnergy { get; set; }

    /// <summary>Share of a hit through the shield that reaches a guarding body.</summary>
    public double GuardBodyShare { get; set; }

    /// <summary>What gets through the qi shield hurts the body this many times over, injuries with it (§7.4).</summary>
    public double BodyDamageMultiplier { get; set; } = 1;

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (WeaponDamage <= 0 || MaxTurns < 1 || FieldRadius < 1)
            yield return "needs a positive weapon_damage, max_turns and field_radius.";
        if (InitiativeThreshold < 1 || InitiativePerReaction <= 0 || EnergyRefillPerOutput < 0 || EnergyRefillMin < 0 || EnergyCapacityPerOutput <= 0 || StepEnergy < 0 || AttackEnergy < 0 || GuardEnergy < 0 || FleeEnergy < 0 || MovePointsPerAgility <= 0)
            yield return "needs a positive initiative_threshold, initiative_per_reaction, energy_capacity_per_output and move_points_per_agility, and energy refills and costs of zero or more.";
        if (DamageSpread is < 0 or >= 1 || InsightFledShare is < 0 or > 1
            || ShieldBreakChance is < 0 or > 1 || GuardBodyShare is < 0 or > 1 || BodyDamageMultiplier < 1 || FightMinHealthShare is < 0 or > 1)
            yield return "damage_spread, guard_body_share, insight_fled_share, shield_break_chance and fight_min_health_share must be fractions, body_damage_multiplier at least 1.";
    }
}
