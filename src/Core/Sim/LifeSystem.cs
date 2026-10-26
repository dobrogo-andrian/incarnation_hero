using Core.Content;
using Core.Rng;
using Core.State;

namespace Core.Sim;

/// <summary>
/// Natural death and life-end warnings (§3). Death is possible from a window before the expected
/// lifespan; the weekly chance grows linearly, scaled so the mean age of death equals the expected
/// lifespan (a discrete Rayleigh distribution). No transcendental functions, to stay deterministic.
/// </summary>
public static class LifeSystem
{
    public static double NaturalDeathChance(int ageWeeks, int lifespanWeeks, int windowWeeks)
    {
        int weeksIntoWindow = ageWeeks - (lifespanWeeks - windowWeeks);
        if (weeksIntoWindow <= 0)
            return 0;

        // Hazard a·t has mean survival time sqrt(pi / 2a); a = pi / 2W² puts the mean at the window width W.
        double slope = Math.PI / (2.0 * windowWeeks * windowWeeks);
        return Math.Min(1.0, slope * weeksIntoWindow);
    }

    public static void AdvanceWeek(GameState state, RunConfigDef config, RngStreams rng, List<SimEvent> events)
    {
        var player = state.Player;
        double deathChance = NaturalDeathChance(player.AgeWeeks, player.LifespanWeeks, config.NaturalDeathWindowYears * config.WeeksPerYear);
        if (deathChance > 0 && rng[RngStream.Life].Chance(deathChance))
        {
            RunEndSystem.End(state, RunEndCause.OldAge, events);
            return;
        }

        int weeksLeft = player.LifespanWeeks - player.AgeWeeks;
        foreach (int years in config.LifeEndWarningYears)
        {
            if (weeksLeft == years * config.WeeksPerYear)
                events.Add(new LifeEndApproaching(years));
        }
    }
}
