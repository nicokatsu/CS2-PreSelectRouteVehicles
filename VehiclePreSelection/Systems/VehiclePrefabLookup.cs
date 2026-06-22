using System.Collections.Generic;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;

namespace VehiclePreSelection
{
    internal sealed class VehiclePrefabLookup
    {
        private readonly PrefabSystem m_PrefabSystem;
        private readonly EntityQuery m_TransportVehiclePrefabQuery;
        private readonly EntityQuery m_WorkVehiclePrefabQuery;

        internal VehiclePrefabLookup(
            PrefabSystem prefabSystem,
            EntityQuery transportVehiclePrefabQuery,
            EntityQuery workVehiclePrefabQuery)
        {
            m_PrefabSystem = prefabSystem;
            m_TransportVehiclePrefabQuery = transportVehiclePrefabQuery;
            m_WorkVehiclePrefabQuery = workVehiclePrefabQuery;
        }

        internal List<Entity> ResolvePrefabs(List<string> prefabNames)
        {
            var result = new List<Entity>();
            if (prefabNames == null || prefabNames.Count == 0)
            {
                return result;
            }

            using var transportVehicles = m_TransportVehiclePrefabQuery.ToEntityArray(Allocator.Temp);
            using var workVehicles = m_WorkVehiclePrefabQuery.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < prefabNames.Count; i++)
            {
                var prefabName = prefabNames[i];
                if (string.IsNullOrEmpty(prefabName))
                {
                    continue;
                }

                if (TryFindPrefab(transportVehicles, prefabName, out var prefab)
                    || TryFindPrefab(workVehicles, prefabName, out prefab))
                {
                    result.Add(prefab);
                }
            }

            return result;
        }

        internal bool TryFindPrefab(string prefabName, out Entity prefab)
        {
            prefab = Entity.Null;
            if (string.IsNullOrEmpty(prefabName))
            {
                return false;
            }

            using (var transportVehicles = m_TransportVehiclePrefabQuery.ToEntityArray(Allocator.Temp))
            {
                if (TryFindPrefab(transportVehicles, prefabName, out prefab))
                {
                    return true;
                }
            }

            using (var workVehicles = m_WorkVehiclePrefabQuery.ToEntityArray(Allocator.Temp))
            {
                return TryFindPrefab(workVehicles, prefabName, out prefab);
            }
        }

        private bool TryFindPrefab(NativeArray<Entity> prefabs, string prefabName, out Entity prefab)
        {
            for (var i = 0; i < prefabs.Length; i++)
            {
                if (m_PrefabSystem.GetPrefabName(prefabs[i]) == prefabName)
                {
                    prefab = prefabs[i];
                    return true;
                }
            }

            prefab = Entity.Null;
            return false;
        }
    }
}
