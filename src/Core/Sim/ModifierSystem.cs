using Core.State;

namespace Core.Sim;

/// <summary>Adds modifiers and expires timed ones. The values they change are computed by <c>CharacterRules</c>.</summary>
public static class ModifierSystem
{
    /// <summary>Adds <paramref name="modifier"/>; one from the same source on the same target is replaced.</summary>
    public static void Apply(PlayerState player, Modifier modifier)
    {
        player.Modifiers.RemoveAll(m => m.SourceId == modifier.SourceId && m.TargetId == modifier.TargetId);
        player.Modifiers.Add(modifier);
    }

    /// <summary>Removes every modifier <paramref name="sourceId"/> applied.</summary>
    public static void RemoveSource(PlayerState player, string sourceId) =>
        player.Modifiers.RemoveAll(m => m.SourceId == sourceId);

    public static void AdvanceWeek(GameState state)
    {
        var modifiers = state.Player.Modifiers;
        foreach (var m in modifiers)
        {
            if (m.WeeksLeft is int left)
                m.WeeksLeft = left - 1;
        }
        modifiers.RemoveAll(m => m.WeeksLeft == 0);
    }
}
