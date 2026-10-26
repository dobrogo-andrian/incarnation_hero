using System.Text.Json.Nodes;
using Core.State;

namespace Core.Save;

/// <summary>
/// Upgrades raw save JSON step by step to <see cref="GameState.CurrentSchemaVersion"/>.
/// Run saves may be invalidated by updates: a missing step makes the save incompatible
/// instead of loading it half-migrated.
/// </summary>
public static class SaveMigrations
{
    /// <summary>Key = source version; the step rewrites the node to version key + 1.</summary>
    private static readonly IReadOnlyDictionary<int, Action<JsonNode>> Steps = new Dictionary<int, Action<JsonNode>>();

    public static bool TryMigrate(JsonNode state, int fromVersion, out string? error)
    {
        for (int v = fromVersion; v < GameState.CurrentSchemaVersion; v++)
        {
            if (!Steps.TryGetValue(v, out var step))
            {
                error = $"No migration from save schema {v}.";
                return false;
            }
            step(state);
        }
        state["schema_version"] = GameState.CurrentSchemaVersion;
        error = null;
        return true;
    }
}
