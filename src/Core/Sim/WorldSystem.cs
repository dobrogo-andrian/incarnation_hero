using Core.Content;
using Core.State;

namespace Core.Sim;

/// <summary>
/// The region's own weekly life: meditation places refill their qi reserve (§9, tech 4.4). Also
/// what the player knows of the map: hidden zones and places count only once revealed (D40).
/// </summary>
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

    /// <summary>A zone or place the player knows: not hidden, or revealed.</summary>
    public static bool IsKnown(WorldState world, ContentDb db, string id)
    {
        bool hidden = db.TryGet<ZoneDef>(id, out var zone) ? zone.Hidden : db.Get<PlaceDef>(id).Hidden;
        return !hidden || world.Revealed.BinarySearch(id, StringComparer.Ordinal) >= 0;
    }

    /// <summary>A known meditation place of the player's zone.</summary>
    public static bool IsPlaceHere(GameState state, ContentDb db, string placeId) =>
        db.TryGet<PlaceDef>(placeId, out var place) && place.Zone == state.Player.ZoneId && IsKnown(state.World, db, placeId);

    /// <summary>Makes a hidden zone or place known; false if it already was.</summary>
    public static bool Reveal(WorldState world, string id)
    {
        int index = world.Revealed.BinarySearch(id, StringComparer.Ordinal);
        if (index >= 0)
            return false;
        world.Revealed.Insert(~index, id);
        return true;
    }
}
