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
        private EntityQuery m_ColoredRouteQuery;
        private EntityQuery m_TransportVehiclePrefabQuery;
        private EntityQuery m_WorkVehiclePrefabQuery;
        private VehiclePrefabLookup m_VehiclePrefabLookup;
        private EntityArchetype m_ColorUpdateArchetype;

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
            m_ColoredRouteQuery = GetEntityQuery(
                ComponentType.ReadOnly<Route>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.ReadOnly<Game.Routes.Color>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<Deleted>());
            m_TransportVehiclePrefabQuery = GetEntityQuery(TransportVehicleSelectData.GetEntityQueryDesc());
            m_WorkVehiclePrefabQuery = GetEntityQuery(WorkVehicleSelectData.GetEntityQueryDesc());
            m_VehiclePrefabLookup = new VehiclePrefabLookup(
                m_PrefabSystem,
                m_TransportVehiclePrefabQuery,
                m_WorkVehiclePrefabQuery);
            m_ColorUpdateArchetype = EntityManager.CreateArchetype(ComponentType.ReadWrite<Game.Common.Event>(), ComponentType.ReadWrite<ColorUpdated>());
            Mod.LogEssential("[VehiclePreSelection] Apply system created. Created route query and official vehicle prefab queries registered.");
        }

        protected override void OnUpdate()
        {
            if (m_CreatedRouteQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var persistedSelections = PersistedRouteSelectionStore.Load();
            using var routes = m_CreatedRouteQuery.ToEntityArray(Allocator.Temp);
            Mod.LogDiagnostic($"[VehiclePreSelection] Applying persisted route preferences to created routes. Count={routes.Length}; Path={PersistedRouteSelectionStore.PathValue}");

            for (var i = 0; i < routes.Length; i++)
            {
                var routeEntity = routes[i];
                var prefabRef = EntityManager.GetComponentData<PrefabRef>(routeEntity);
                var routePrefabName = m_PrefabSystem.GetPrefabName(prefabRef.m_Prefab);
                var savedSelection = PersistedRouteSelectionStore.Find(persistedSelections, routePrefabName);
                if (savedSelection == null)
                {
                    ApplyRandomColorIfEnabled(routeEntity, prefabRef.m_Prefab, persistedSelections);
                    continue;
                }

                ApplySavedSelection(routeEntity, prefabRef.m_Prefab, savedSelection);
                ApplyRandomColorIfEnabled(routeEntity, prefabRef.m_Prefab, persistedSelections);
            }
        }

        private void ApplySavedSelection(Entity routeEntity, Entity routePrefab, PersistedRouteSelectionStore.PersistedRouteSelection savedSelection)
        {
            if (!RouteSelectionContext.TryCreate(EntityManager, routePrefab, out var routeContext))
            {
                Mod.LogDiagnostic(
                    $"[VehiclePreSelection] Skipped saved selection for unsupported route prefab. RouteEntity={routeEntity.Index}; RoutePrefab={m_PrefabSystem.GetPrefabName(routePrefab)}");
                return;
            }

            var primaryPrefabs = m_VehiclePrefabLookup.ResolvePrefabs(savedSelection.primary);
            var secondaryPrefabs = m_VehiclePrefabLookup.ResolvePrefabs(savedSelection.secondary);
            var includeSecondary = routeContext.NeedsSecondarySelection;

            if (primaryPrefabs.Count == 0 && (!includeSecondary || secondaryPrefabs.Count == 0))
            {
                return;
            }

            var buffer = VehicleModelSelectionWriter.EnsureBuffer(EntityManager, routeEntity);
            VehicleModelSelectionWriter.WriteSelections(
                buffer,
                primaryPrefabs,
                secondaryPrefabs,
                includeSecondary);
            Mod.LogDiagnostic(
                $"[VehiclePreSelection] Applied saved route vehicle selection. RouteEntity={routeEntity.Index}; RoutePrefab={m_PrefabSystem.GetPrefabName(routePrefab)}; PrimaryCount={primaryPrefabs.Count}; SecondaryCount={(includeSecondary ? secondaryPrefabs.Count : 0)}; Context={routeContext.ToDiagnosticString()}");
        }

        private void ApplyRandomColorIfEnabled(
            Entity routeEntity,
            Entity routePrefab,
            PersistedRouteSelectionStore.PersistedSelectionFile persistedSelections)
        {
            if (!EntityManager.HasComponent<Game.Routes.Color>(routeEntity)
                || !RouteColorRandomizationUtils.TryBuildFamily(EntityManager, routePrefab, out var family))
            {
                return;
            }

            var colorKey = RouteColorRandomizationUtils.BuildKey(family);
            var colorPreference = PersistedRouteSelectionStore.FindColorPreference(persistedSelections, colorKey);
            if (colorPreference?.enabled != true)
            {
                return;
            }

            var selectedColor = RouteColorRandomizationUtils.ChooseRandomColor(
                EntityManager,
                m_ColoredRouteQuery,
                routeEntity,
                family);

            EntityManager.SetComponentData(routeEntity, new Game.Routes.Color(selectedColor));

            var vehicleColorUpdates = 0;
            if (EntityManager.HasBuffer<RouteVehicle>(routeEntity))
            {
                var routeVehicles = EntityManager.GetBuffer<RouteVehicle>(routeEntity, true);
                for (var i = 0; i < routeVehicles.Length; i++)
                {
                    var vehicleEntity = routeVehicles[i].m_Vehicle;
                    if (vehicleEntity == Entity.Null)
                    {
                        continue;
                    }

                    if (EntityManager.HasComponent<Game.Routes.Color>(vehicleEntity))
                    {
                        EntityManager.SetComponentData(vehicleEntity, new Game.Routes.Color(selectedColor));
                    }
                    else
                    {
                        EntityManager.AddComponentData(vehicleEntity, new Game.Routes.Color(selectedColor));
                    }

                    vehicleColorUpdates++;
                }
            }

            var colorUpdated = EntityManager.CreateEntity(m_ColorUpdateArchetype);
            EntityManager.SetComponentData(colorUpdated, new ColorUpdated(routeEntity));
            Mod.LogDiagnostic(
                $"[VehiclePreSelection] Applied random route color. RouteEntity={routeEntity.Index}; Key={colorKey}; Color=rgba({selectedColor.r},{selectedColor.g},{selectedColor.b},{selectedColor.a}); VehicleUpdates={vehicleColorUpdates}");
        }
    }
}
