using System.Collections.Generic;
using Game;
using Game.Common;
using Game.Prefabs;
using Game.Routes;
using Game.Tools;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;

namespace VehiclePreSelection
{
    public partial class RouteVehicleSelectionApplySystem : GameSystemBase
    {
        private PrefabSystem m_PrefabSystem;
        private EntityQuery m_CreatedRouteQuery;
        private EntityQuery m_TransportVehiclePrefabQuery;
        private EntityQuery m_WorkVehiclePrefabQuery;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_CreatedRouteQuery = GetEntityQuery(
                ComponentType.ReadOnly<Route>(),
                ComponentType.ReadOnly<RouteNumber>(),
                ComponentType.ReadOnly<Created>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<Deleted>());
            m_TransportVehiclePrefabQuery = GetEntityQuery(TransportVehicleSelectData.GetEntityQueryDesc());
            m_WorkVehiclePrefabQuery = GetEntityQuery(WorkVehicleSelectData.GetEntityQueryDesc());
        }

        protected override void OnUpdate()
        {
            if (m_CreatedRouteQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var persistedSelections = PersistedRouteSelectionStore.Load();
            using var routes = m_CreatedRouteQuery.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < routes.Length; i++)
            {
                var routeEntity = routes[i];
                var prefabRef = EntityManager.GetComponentData<PrefabRef>(routeEntity);
                var routePrefabName = m_PrefabSystem.GetPrefabName(prefabRef.m_Prefab);
                var savedSelection = PersistedRouteSelectionStore.Find(persistedSelections, routePrefabName);
                if (savedSelection == null)
                {
                    continue;
                }

                ApplySavedSelection(routeEntity, prefabRef.m_Prefab, savedSelection);
            }
        }

        private void ApplySavedSelection(Entity routeEntity, Entity routePrefab, PersistedRouteSelectionStore.PersistedRouteSelection savedSelection)
        {
            var primaryPrefabs = ResolvePrefabs(savedSelection.primary);
            var secondaryPrefabs = ResolvePrefabs(savedSelection.secondary);
            var supportsSecondary = SupportsSecondarySelection(routePrefab);

            if (primaryPrefabs.Count == 0 && (!supportsSecondary || secondaryPrefabs.Count == 0))
            {
                return;
            }

            var buffer = EntityManager.HasBuffer<VehicleModel>(routeEntity)
                ? EntityManager.GetBuffer<VehicleModel>(routeEntity)
                : EntityManager.AddBuffer<VehicleModel>(routeEntity);

            buffer.Clear();

            for (var i = 0; i < primaryPrefabs.Count; i++)
            {
                buffer.Add(new VehicleModel
                {
                    m_PrimaryPrefab = primaryPrefabs[i],
                    m_SecondaryPrefab = Entity.Null
                });
            }

            if (supportsSecondary)
            {
                for (var i = 0; i < secondaryPrefabs.Count; i++)
                {
                    buffer.Add(new VehicleModel
                    {
                        m_PrimaryPrefab = Entity.Null,
                        m_SecondaryPrefab = secondaryPrefabs[i]
                    });
                }
            }
        }

        private List<Entity> ResolvePrefabs(List<string> prefabNames)
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

        private bool SupportsSecondarySelection(Entity routePrefab)
        {
            if (!EntityManager.HasComponent<TransportLineData>(routePrefab))
            {
                return false;
            }

            var lineData = EntityManager.GetComponentData<TransportLineData>(routePrefab);
            return lineData.m_TransportType == TransportType.Train
                && lineData.m_CargoTransport
                && !lineData.m_PassengerTransport;
        }
    }
}
