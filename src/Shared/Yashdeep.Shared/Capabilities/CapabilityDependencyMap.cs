namespace Yashdeep.Shared.Capabilities;

public static class CapabilityDependencyMap
{
    private static readonly Dictionary<CapabilityId, HashSet<CapabilityId>> Dependencies = new()
    {
        [CapabilityId.PosBilling] = new HashSet<CapabilityId>(),
        [CapabilityId.KotRouting] = new HashSet<CapabilityId> { CapabilityId.PosBilling },
        [CapabilityId.BilingualMarathiKot] = new HashSet<CapabilityId> { CapabilityId.KotRouting },
        [CapabilityId.DynamicUpiQr] = new HashSet<CapabilityId> { CapabilityId.PosBilling },
        [CapabilityId.OutboxSqliteSync] = new HashSet<CapabilityId>(),

        [CapabilityId.MultiTierInventory] = new HashSet<CapabilityId> { CapabilityId.PosBilling },
        [CapabilityId.ExciseFl3Compliance] = new HashSet<CapabilityId> { CapabilityId.MultiTierInventory, CapabilityId.PosBilling },
        [CapabilityId.LoosePegDispensing] = new HashSet<CapabilityId> { CapabilityId.MultiTierInventory },

        [CapabilityId.HotelRooms] = new HashSet<CapabilityId>(),
        [CapabilityId.Housekeeping] = new HashSet<CapabilityId> { CapabilityId.HotelRooms },
        [CapabilityId.RoomServicePosting] = new HashSet<CapabilityId> { CapabilityId.HotelRooms, CapabilityId.PosBilling },

        [CapabilityId.MultiBranchStockTransfer] = new HashSet<CapabilityId> { CapabilityId.MultiTierInventory },
        [CapabilityId.CentralizedMasterMenu] = new HashSet<CapabilityId> { CapabilityId.PosBilling },
        [CapabilityId.CustomApiWebhooks] = new HashSet<CapabilityId>(),
        [CapabilityId.AdvancedAnalytics] = new HashSet<CapabilityId>(),
        [CapabilityId.QuestPdfReporting] = new HashSet<CapabilityId>()
    };

    public static IReadOnlySet<CapabilityId> GetDirectDependencies(CapabilityId capability)
    {
        return Dependencies.TryGetValue(capability, out var deps) ? deps : new HashSet<CapabilityId>();
    }

    public static IReadOnlySet<CapabilityId> GetAllDependencies(CapabilityId capability)
    {
        var visited = new HashSet<CapabilityId>();
        GetDependenciesRecursive(capability, visited);
        return visited;
    }

    private static void GetDependenciesRecursive(CapabilityId current, HashSet<CapabilityId> visited)
    {
        if (!Dependencies.TryGetValue(current, out var directDeps))
        {
            return;
        }

        foreach (var dep in directDeps)
        {
            if (visited.Add(dep))
            {
                GetDependenciesRecursive(dep, visited);
            }
        }
    }

    public static bool ArePrerequisitesSatisfied(CapabilityId capability, IEnumerable<CapabilityId> enabledCapabilities)
    {
        var enabledSet = enabledCapabilities is IReadOnlySet<CapabilityId> set
            ? set
            : new HashSet<CapabilityId>(enabledCapabilities);

        var required = GetAllDependencies(capability);
        foreach (var req in required)
        {
            if (!enabledSet.Contains(req))
            {
                return false;
            }
        }

        return true;
    }

    public static IReadOnlySet<CapabilityId> ResolveValidCapabilities(IEnumerable<CapabilityId> requestedCapabilities)
    {
        var requestedSet = new HashSet<CapabilityId>(requestedCapabilities);
        var resolved = new HashSet<CapabilityId>();

        bool changed;
        do
        {
            changed = false;
            foreach (var cap in requestedSet.ToList())
            {
                if (!resolved.Contains(cap) && ArePrerequisitesSatisfied(cap, requestedSet))
                {
                    resolved.Add(cap);
                    changed = true;
                }
            }
            requestedSet.IntersectWith(resolved);
        } while (changed);

        return resolved;
    }
}
