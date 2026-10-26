using Core.Content;
using Core.State;

namespace Core.Sim;

/// <summary>The region's own weekly life: meditation places refill their qi reserve (§9, tech 4.4).</summary>
public static class WorldSystem
{
    /// <summary>Every meditation place starts full.</summary>
    public static void InitWorld(WorldState world, ContentDb db) =>
        world.Places = db.All<PlaceDef>().Select(p => new PlaceState { PlaceId = p.Id, Reserve = p.Capacity }).ToList();

    public static void AdvanceWeek(GameState state, ContentDb db)
    {
        foreach (var place in state.World.Places)
        {
            var def = db.Get<PlaceDef>(place.PlaceId);
            place.Reserve = StateNumbers.Round(Math.Min(def.Capacity, place.Reserve + def.RegenPerWeek));
        }
    }
}
