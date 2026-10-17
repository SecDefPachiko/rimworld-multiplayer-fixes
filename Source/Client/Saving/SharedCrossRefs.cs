using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Multiplayer.Client;

public class SharedCrossRefs : LoadedObjectDirectory
{
    // Used in CrossRefs patches
    public HashSet<string> tempKeys = new();

    public void Unregister(ILoadReferenceable reffable)
    {
        allObjectsByLoadID.Remove(reffable.GetUniqueLoadID());
    }

    public void UnregisterAllTemp()
    {
        foreach (var key in tempKeys)
            allObjectsByLoadID.Remove(key);

        tempKeys.Clear();
    }

    private static bool IsOnMap(ILoadReferenceable val, Map map) =>
        val is Thing thing && thing.Map == map ||
        val is PassingShip ship && ship.Map == map ||
        val is Bill bill && bill.Map == map;

    public static bool IsOnAnyMap(ILoadReferenceable val) =>
        val is Thing { Map: not null } || val is PassingShip { Map: not null } || val is Bill { Map: not null };

    public List<ILoadReferenceable> UnregisterAllFrom(Map map)
    {
        var unregistered = new List<ILoadReferenceable>();

        foreach (var val in allObjectsByLoadID.Values.ToArray())
        {
            if (IsOnMap(val, map))
            {
                Unregister(val);
                unregistered.Add(val);
            }
        }

        return unregistered;
    }

    public void Reregister(List<ILoadReferenceable> items)
    {
        foreach (var item in items)
            allObjectsByLoadID[item.GetUniqueLoadID()] = item;
    }
}
