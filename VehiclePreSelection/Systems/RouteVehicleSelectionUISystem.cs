using Colossal.UI.Binding;
using Game;
using Game.Buildings;
using Game.Areas;
using Game.City;
using Game.Common;
using Game.Economy;
using Game.Net;
using Game.Prefabs;
using Game.Routes;
using Game.SceneFlow;
using Game.Tools;
using Game.UI;
using Game.UI.InGame;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;
using System.Text;
using System.Collections.Generic;
using System.IO;

namespace VehiclePreSelection
{
    public partial class RouteVehicleSelectionUISystem : UISystemBase
    {
        private struct PlannedRouteContext
        {
            public TransportType TransportType;
            public SizeClass SizeClass;
            public bool CargoTransport;
            public bool PassengerTransport;
            public bool IsWorkRoute;
            public MapFeature MapFeature;
            public RoadTypes RoadTypes;
        }

        private const string Group = "vehiclePreSelection";

        private RouteToolSystem m_RouteToolSystem;
        private ToolSystem m_ToolSystem;
        private PrefabSystem m_PrefabSystem;
        private PrefabUISystem m_PrefabUISystem;
        private ImageSystem m_ImageSystem;
        private CityConfigurationSystem m_CityConfigurationSystem;
        private ValueBinding<bool> m_IsPlanningRouteBinding;
        private ValueBinding<string> m_RoutePrefabBinding;
        private ValueBinding<bool> m_SupportsSecondarySelectionBinding;
        private ValueBinding<string> m_AvailablePrimaryVehiclesBinding;
        private ValueBinding<string> m_AvailableSecondaryVehiclesBinding;
        private ValueBinding<string> m_SelectedPrimaryIndicesBinding;
        private ValueBinding<string> m_SelectedSecondaryIndicesBinding;
        private ValueBinding<string> m_CurrentPrimaryVehicleBinding;
        private ValueBinding<string> m_CurrentSecondaryVehicleBinding;

        private EntityQuery m_TempRouteQuery;
        private EntityQuery m_DepotQuery;
        private EntityQuery m_TransportVehiclePrefabQuery;
        private EntityQuery m_WorkVehiclePrefabQuery;

        private TransportVehicleSelectData m_TransportVehicleSelectData;
        private WorkVehicleSelectData m_WorkVehicleSelectData;
        private NativeList<Entity> m_AvailablePrimaryVehicles;
        private NativeList<Entity> m_AvailableSecondaryVehicles;
        private NativeArray<int> m_DepotResults;
        private string m_LastRoutePrefabName = string.Empty;
        private readonly List<Entity> m_PendingPrimaryPrefabs = new List<Entity>(8);
        private readonly List<Entity> m_PendingSecondaryPrefabs = new List<Entity>(8);
        private PersistedRouteSelectionStore.PersistedSelectionFile m_PersistedSelections = new PersistedRouteSelectionStore.PersistedSelectionFile();

        protected override void OnCreate()
        {
            base.OnCreate();

            m_RouteToolSystem = World.GetOrCreateSystemManaged<RouteToolSystem>();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_PrefabUISystem = World.GetOrCreateSystemManaged<PrefabUISystem>();
            m_ImageSystem = World.GetOrCreateSystemManaged<ImageSystem>();
            m_CityConfigurationSystem = World.GetOrCreateSystemManaged<CityConfigurationSystem>();

            m_TempRouteQuery = GetEntityQuery(
                ComponentType.ReadOnly<Route>(),
                ComponentType.ReadOnly<Temp>(),
                ComponentType.ReadOnly<PrefabRef>());
            m_DepotQuery = GetEntityQuery(
                ComponentType.ReadOnly<Game.Buildings.TransportDepot>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<Deleted>());

            m_TransportVehicleSelectData = new TransportVehicleSelectData(this);
            m_WorkVehicleSelectData = new WorkVehicleSelectData(this);
            m_TransportVehiclePrefabQuery = GetEntityQuery(TransportVehicleSelectData.GetEntityQueryDesc());
            m_WorkVehiclePrefabQuery = GetEntityQuery(WorkVehicleSelectData.GetEntityQueryDesc());
            m_AvailablePrimaryVehicles = new NativeList<Entity>(16, Allocator.Persistent);
            m_AvailableSecondaryVehicles = new NativeList<Entity>(16, Allocator.Persistent);
            m_DepotResults = new NativeArray<int>(2, Allocator.Persistent);
            LoadPersistedSelections();

            var stateBinding = new RawValueBinding(Group, "state", WriteState);
            AddBinding(stateBinding);
            AddUpdateBinding(stateBinding);
            AddBinding(m_IsPlanningRouteBinding = new ValueBinding<bool>(Group, "isPlanningRoute", false));
            AddBinding(m_RoutePrefabBinding = new ValueBinding<string>(Group, "routePrefab", string.Empty));
            AddBinding(m_SupportsSecondarySelectionBinding = new ValueBinding<bool>(Group, "supportsSecondarySelection", false));
            AddBinding(m_AvailablePrimaryVehiclesBinding = new ValueBinding<string>(Group, "availablePrimaryVehiclesJson", "[]"));
            AddBinding(m_AvailableSecondaryVehiclesBinding = new ValueBinding<string>(Group, "availableSecondaryVehiclesJson", "[]"));
            AddBinding(m_SelectedPrimaryIndicesBinding = new ValueBinding<string>(Group, "selectedPrimaryIndicesJson", "[]"));
            AddBinding(m_SelectedSecondaryIndicesBinding = new ValueBinding<string>(Group, "selectedSecondaryIndicesJson", "[]"));
            AddBinding(m_CurrentPrimaryVehicleBinding = new ValueBinding<string>(Group, "currentPrimaryVehicle", string.Empty));
            AddBinding(m_CurrentSecondaryVehicleBinding = new ValueBinding<string>(Group, "currentSecondaryVehicle", string.Empty));

            AddBinding(new TriggerBinding<int>(Group, "togglePrimaryIndex", TogglePrimaryIndex));
            AddBinding(new TriggerBinding<int>(Group, "toggleSecondaryIndex", ToggleSecondaryIndex));
            AddBinding(new TriggerBinding(Group, "clearSelections", ClearSelections));
        }

        protected override void OnDestroy()
        {
            if (m_AvailablePrimaryVehicles.IsCreated)
            {
                m_AvailablePrimaryVehicles.Dispose();
            }

            if (m_AvailableSecondaryVehicles.IsCreated)
            {
                m_AvailableSecondaryVehicles.Dispose();
            }

            if (m_DepotResults.IsCreated)
            {
                m_DepotResults.Dispose();
            }

            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            var routeEntity = Entity.Null;
            var hasActiveRoutePrefab = TryGetActiveRoutePrefab(out var routePrefab, out var routeContext);
            var hasTempRoute = hasActiveRoutePrefab && TryGetCurrentTempRoute(routePrefab, out routeEntity);
            var routePrefabName = hasActiveRoutePrefab ? m_PrefabSystem.GetPrefabName(routePrefab) : string.Empty;

            if (hasActiveRoutePrefab && !string.IsNullOrEmpty(m_LastRoutePrefabName) && m_LastRoutePrefabName != routePrefabName)
            {
                ResetPendingSelections();
                RestorePersistedSelections(routePrefabName);
            }
            else if (hasActiveRoutePrefab && string.IsNullOrEmpty(m_LastRoutePrefabName))
            {
                RestorePersistedSelections(routePrefabName);
            }

            if (hasActiveRoutePrefab)
            {
                m_LastRoutePrefabName = routePrefabName;
            }
            else
            {
                m_LastRoutePrefabName = string.Empty;
            }

            m_IsPlanningRouteBinding.Update(hasActiveRoutePrefab);
            m_RoutePrefabBinding.Update(routePrefabName);
            UpdateUiBindings(hasActiveRoutePrefab, hasTempRoute, routeEntity, routeContext, routePrefabName);
        }

        private void WriteState(IJsonWriter writer)
        {
            if (!TryGetActiveRoutePrefab(out var routePrefab, out var routeContext))
            {
                writer.TypeBegin("object");
                writer.PropertyName("isPlanningRoute");
                writer.Write(false);
                writer.TypeEnd();
                return;
            }

            RefreshAvailableVehicles(routeContext);
            var hasTempRoute = TryGetCurrentTempRoute(routePrefab, out var routeEntity);

            writer.TypeBegin("object");

            writer.PropertyName("isPlanningRoute");
            writer.Write(true);

            writer.PropertyName("routeEntityIndex");
            writer.Write(hasTempRoute ? routeEntity.Index : -1);

            writer.PropertyName("routePrefab");
            writer.Write(m_PrefabSystem.GetPrefabName(routePrefab));

            writer.PropertyName("supportsSecondarySelection");
            writer.Write(NeedsSecondarySelection(routeContext));

            writer.PropertyName("selectedPrimaryVehicles");
            if (hasTempRoute)
            {
                WriteSelectedVehicles(writer, routeEntity, primary: true);
            }
            else
            {
                WritePendingVehicles(writer, m_PendingPrimaryPrefabs);
            }

            writer.PropertyName("selectedSecondaryVehicles");
            if (hasTempRoute)
            {
                WriteSelectedVehicles(writer, routeEntity, primary: false);
            }
            else
            {
                WritePendingVehicles(writer, m_PendingSecondaryPrefabs);
            }

            writer.PropertyName("availablePrimaryVehicles");
            WriteAvailableVehicles(writer, m_AvailablePrimaryVehicles);

            writer.PropertyName("availableSecondaryVehicles");
            if (NeedsSecondarySelection(routeContext))
            {
                WriteAvailableVehicles(writer, m_AvailableSecondaryVehicles);
            }
            else
            {
                writer.WriteNull();
            }

            writer.TypeEnd();
        }

        private void UpdateUiBindings(bool hasActiveRoutePrefab, bool hasTempRoute, Entity routeEntity, PlannedRouteContext routeContext, string routePrefabName)
        {
            if (!hasActiveRoutePrefab)
            {
                m_SupportsSecondarySelectionBinding.Update(false);
                m_AvailablePrimaryVehiclesBinding.Update("[]");
                m_AvailableSecondaryVehiclesBinding.Update("[]");
                m_SelectedPrimaryIndicesBinding.Update("[]");
                m_SelectedSecondaryIndicesBinding.Update("[]");
                m_CurrentPrimaryVehicleBinding.Update(string.Empty);
                m_CurrentSecondaryVehicleBinding.Update(string.Empty);
                return;
            }

            RefreshAvailableVehicles(routeContext);
            EnsurePendingSelections(routeContext, routePrefabName);
            if (hasTempRoute)
            {
                ApplyPendingSelections(routeEntity, routeContext);
            }

            var supportsSecondarySelection = NeedsSecondarySelection(routeContext);
            m_SupportsSecondarySelectionBinding.Update(supportsSecondarySelection);
            m_AvailablePrimaryVehiclesBinding.Update(SerializeVehicles(m_AvailablePrimaryVehicles));
            m_SelectedPrimaryIndicesBinding.Update(hasTempRoute
                ? SerializeSelectedVehicleIndices(routeEntity, m_AvailablePrimaryVehicles, primary: true)
                : SerializePendingSelectionIndices(m_PendingPrimaryPrefabs, m_AvailablePrimaryVehicles));
            m_CurrentPrimaryVehicleBinding.Update(hasTempRoute
                ? GetCurrentSelectedVehicleSummary(routeEntity, primary: true)
                : GetPendingSelectedVehicleSummary(m_PendingPrimaryPrefabs));

            if (supportsSecondarySelection)
            {
                m_AvailableSecondaryVehiclesBinding.Update(SerializeVehicles(m_AvailableSecondaryVehicles));
                m_SelectedSecondaryIndicesBinding.Update(hasTempRoute
                    ? SerializeSelectedVehicleIndices(routeEntity, m_AvailableSecondaryVehicles, primary: false)
                    : SerializePendingSelectionIndices(m_PendingSecondaryPrefabs, m_AvailableSecondaryVehicles));
                m_CurrentSecondaryVehicleBinding.Update(hasTempRoute
                    ? GetCurrentSelectedVehicleSummary(routeEntity, primary: false)
                    : GetPendingSelectedVehicleSummary(m_PendingSecondaryPrefabs));
            }
            else
            {
                m_AvailableSecondaryVehiclesBinding.Update("[]");
                m_SelectedSecondaryIndicesBinding.Update("[]");
                m_CurrentSecondaryVehicleBinding.Update(string.Empty);
            }
        }

        private void WriteSelectedVehicles(IJsonWriter writer, Entity routeEntity, bool primary)
        {
            if (!EntityManager.HasBuffer<VehicleModel>(routeEntity))
            {
                JsonWriterExtensions.ArrayBegin(writer, 0);
                writer.ArrayEnd();
                return;
            }

            var buffer = EntityManager.GetBuffer<VehicleModel>(routeEntity, true);
            var count = 0;

            foreach (var vehicleModel in buffer)
            {
                var prefab = primary ? vehicleModel.m_PrimaryPrefab : vehicleModel.m_SecondaryPrefab;
                if (prefab != Entity.Null)
                {
                    count++;
                }
            }

            JsonWriterExtensions.ArrayBegin(writer, count);

            foreach (var vehicleModel in buffer)
            {
                var prefab = primary ? vehicleModel.m_PrimaryPrefab : vehicleModel.m_SecondaryPrefab;
                if (prefab != Entity.Null)
                {
                    WriteVehicle(writer, prefab);
                }
            }

            writer.ArrayEnd();
        }

        private void WritePendingVehicles(IJsonWriter writer, List<Entity> prefabs)
        {
            JsonWriterExtensions.ArrayBegin(writer, prefabs.Count);
            for (var i = 0; i < prefabs.Count; i++)
            {
                WriteVehicle(writer, prefabs[i]);
            }

            writer.ArrayEnd();
        }

        private void WriteAvailableVehicles(IJsonWriter writer, NativeList<Entity> availableVehicles)
        {
            JsonWriterExtensions.ArrayBegin(writer, availableVehicles.Length);

            for (var i = 0; i < availableVehicles.Length; i++)
            {
                WriteVehicle(writer, availableVehicles[i]);
            }

            writer.ArrayEnd();
        }

        private void WriteVehicle(IJsonWriter writer, Entity prefabEntity)
        {
            writer.TypeBegin("object");

            writer.PropertyName("entityIndex");
            writer.Write(prefabEntity.Index);

            writer.PropertyName("name");
            writer.Write(GetPrefabDisplayName(prefabEntity));

            writer.PropertyName("id");
            writer.Write(m_PrefabSystem.GetPrefabName(prefabEntity));

            writer.PropertyName("thumbnail");
            writer.Write(m_ImageSystem.GetThumbnail(prefabEntity) ?? m_ImageSystem.placeholderIcon);

            writer.PropertyName("objectRequirementIcons");
            WriteObjectRequirementIcons(writer, prefabEntity);

            writer.TypeEnd();
        }

        private string SerializeVehicles(NativeList<Entity> vehicles)
        {
            var builder = new StringBuilder();
            builder.Append('[');

            for (var i = 0; i < vehicles.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                var vehicleEntity = vehicles[i];
                builder.Append("{\"entityIndex\":");
                builder.Append(vehicleEntity.Index);
                builder.Append(",\"name\":\"");
                builder.Append(EscapeJson(GetPrefabDisplayName(vehicleEntity)));
                builder.Append("\",\"id\":\"");
                builder.Append(EscapeJson(m_PrefabSystem.GetPrefabName(vehicleEntity)));
                builder.Append("\",\"thumbnail\":\"");
                builder.Append(EscapeJson(m_ImageSystem.GetThumbnail(vehicleEntity) ?? m_ImageSystem.placeholderIcon));
                builder.Append("\",\"objectRequirementIcons\":");
                builder.Append(SerializeObjectRequirementIcons(vehicleEntity));
                builder.Append('}');
            }

            builder.Append(']');
            return builder.ToString();
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        private string GetPrefabDisplayName(Entity prefabEntity)
        {
            if (prefabEntity == Entity.Null)
            {
                return string.Empty;
            }

            m_PrefabUISystem.GetTitleAndDescription(prefabEntity, out var titleId, out _);
            var dictionary = GameManager.instance?.localizationManager?.activeDictionary;
            if (!string.IsNullOrEmpty(titleId) &&
                dictionary != null &&
                dictionary.TryGetValue(titleId, out var localizedTitle) &&
                !string.IsNullOrWhiteSpace(localizedTitle))
            {
                return localizedTitle;
            }

            return m_PrefabSystem.GetPrefabName(prefabEntity);
        }

        private void WriteObjectRequirementIcons(IJsonWriter writer, Entity prefabEntity)
        {
            if (!EntityManager.HasBuffer<ObjectRequirementElement>(prefabEntity))
            {
                writer.WriteNull();
                return;
            }

            var buffer = EntityManager.GetBuffer<ObjectRequirementElement>(prefabEntity, true);
            writer.ArrayBegin(buffer.Length);
            for (var i = 0; i < buffer.Length; i++)
            {
                writer.Write(m_ImageSystem.GetThumbnail(buffer[i].m_Requirement));
            }
            writer.ArrayEnd();
        }

        private string SerializeObjectRequirementIcons(Entity prefabEntity)
        {
            if (!EntityManager.HasBuffer<ObjectRequirementElement>(prefabEntity))
            {
                return "null";
            }

            var buffer = EntityManager.GetBuffer<ObjectRequirementElement>(prefabEntity, true);
            var builder = new StringBuilder();
            builder.Append('[');

            for (var i = 0; i < buffer.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append('"');
                builder.Append(EscapeJson(m_ImageSystem.GetThumbnail(buffer[i].m_Requirement) ?? string.Empty));
                builder.Append('"');
            }

            builder.Append(']');
            return builder.ToString();
        }

        private void TogglePrimaryIndex(int index)
        {
            if (!TryGetActiveRoutePrefab(out var routePrefab, out var routeContext))
            {
                return;
            }

            RefreshAvailableVehicles(routeContext);
            if (index < 0 || index >= m_AvailablePrimaryVehicles.Length)
            {
                return;
            }

            var prefab = m_AvailablePrimaryVehicles[index];
            TogglePendingSelection(m_PendingPrimaryPrefabs, prefab);
            SavePersistedSelections(routePrefabName: m_PrefabSystem.GetPrefabName(routePrefab));
            var hasTempRoute = TryGetCurrentTempRoute(routePrefab, out var routeEntity);
            if (hasTempRoute)
            {
                ApplyPendingSelections(routeEntity, routeContext);
            }
        }

        private void ToggleSecondaryIndex(int index)
        {
            if (!TryGetActiveRoutePrefab(out var routePrefab, out var routeContext))
            {
                return;
            }

            if (!NeedsSecondarySelection(routeContext))
            {
                return;
            }

            RefreshAvailableVehicles(routeContext);
            if (index < 0 || index >= m_AvailableSecondaryVehicles.Length)
            {
                return;
            }

            var prefab = m_AvailableSecondaryVehicles[index];
            TogglePendingSelection(m_PendingSecondaryPrefabs, prefab);
            SavePersistedSelections(routePrefabName: m_PrefabSystem.GetPrefabName(routePrefab));
            var hasTempRoute = TryGetCurrentTempRoute(routePrefab, out var routeEntity);
            if (hasTempRoute)
            {
                ApplyPendingSelections(routeEntity, routeContext);
            }
        }

        private void ClearSelections()
        {
            if (!TryGetActiveRoutePrefab(out var routePrefab, out _))
            {
                return;
            }

            ResetPendingSelections();
            SavePersistedSelections(routePrefabName: m_PrefabSystem.GetPrefabName(routePrefab));

            if (TryGetCurrentTempRoute(routePrefab, out var routeEntity))
            {
                var buffer = EnsureVehicleModelBuffer(routeEntity);
                buffer.Clear();
            }
        }

        private void ApplyPendingSelections(Entity routeEntity, PlannedRouteContext routeContext)
        {
            FilterPendingSelections(m_PendingPrimaryPrefabs, m_AvailablePrimaryVehicles);
            FilterPendingSelections(m_PendingSecondaryPrefabs, m_AvailableSecondaryVehicles);

            if (m_PendingPrimaryPrefabs.Count == 0 && (!NeedsSecondarySelection(routeContext) || m_PendingSecondaryPrefabs.Count == 0))
            {
                return;
            }

            var buffer = EnsureVehicleModelBuffer(routeEntity);
            if (BufferMatchesPendingSelections(buffer, routeContext))
            {
                return;
            }

            buffer.Clear();
            for (var i = 0; i < m_PendingPrimaryPrefabs.Count; i++)
            {
                buffer.Add(new VehicleModel
                {
                    m_PrimaryPrefab = m_PendingPrimaryPrefabs[i],
                    m_SecondaryPrefab = Entity.Null
                });
            }

            if (NeedsSecondarySelection(routeContext))
            {
                for (var i = 0; i < m_PendingSecondaryPrefabs.Count; i++)
                {
                    buffer.Add(new VehicleModel
                    {
                        m_PrimaryPrefab = Entity.Null,
                        m_SecondaryPrefab = m_PendingSecondaryPrefabs[i]
                    });
                }
            }
        }

        private void ResetPendingSelections()
        {
            m_PendingPrimaryPrefabs.Clear();
            m_PendingSecondaryPrefabs.Clear();
        }

        private void EnsurePendingSelections(PlannedRouteContext routeContext, string routePrefabName)
        {
            var needsPrimary = m_PendingPrimaryPrefabs.Count == 0;
            var needsSecondary = NeedsSecondarySelection(routeContext) && m_PendingSecondaryPrefabs.Count == 0;

            if (!needsPrimary && !needsSecondary)
            {
                return;
            }

            SelectRandomPendingSelections(routeContext, needsPrimary, needsSecondary);
            SavePersistedSelections(routePrefabName);
        }

        private void SelectRandomPendingSelections(PlannedRouteContext routeContext, bool fillPrimary, bool fillSecondary)
        {
            var seed = (uint)(System.DateTime.UtcNow.Ticks & 0x7fffffff);
            if (seed == 0)
            {
                seed = 1;
            }

            var random = new Unity.Mathematics.Random(seed);

            if (routeContext.IsWorkRoute)
            {
                if (!fillPrimary)
                {
                    return;
                }

                var primaryPrefab = Entity.Null;
                m_WorkVehicleSelectData.PreUpdate(this, m_CityConfigurationSystem, m_WorkVehiclePrefabQuery, Allocator.TempJob, out var jobHandle);
                jobHandle.Complete();

                try
                {
                    m_WorkVehicleSelectData.SelectVehicle(
                        ref random,
                        routeContext.RoadTypes,
                        routeContext.SizeClass,
                        VehicleWorkType.Harvest,
                        routeContext.MapFeature,
                        Resource.NoResource,
                        out primaryPrefab);
                }
                finally
                {
                    m_WorkVehicleSelectData.PostUpdate(default);
                }

                if (primaryPrefab != Entity.Null && !ContainsPendingSelection(m_PendingPrimaryPrefabs, primaryPrefab))
                {
                    m_PendingPrimaryPrefabs.Add(primaryPrefab);
                }

                return;
            }

            var publicTransportPurpose = routeContext.PassengerTransport ? PublicTransportPurpose.TransportLine : (PublicTransportPurpose)0;
            var cargoResources = routeContext.CargoTransport ? (Resource)8 : Resource.NoResource;
            var passengerCapacity = routeContext.PassengerTransport ? new Unity.Mathematics.int2(1, int.MaxValue) : default;
            var cargoCapacity = routeContext.CargoTransport ? new Unity.Mathematics.int2(1, int.MaxValue) : default;
            var selectedPrimary = Entity.Null;
            var selectedSecondary = Entity.Null;

            m_TransportVehicleSelectData.PreUpdate(this, m_CityConfigurationSystem, m_TransportVehiclePrefabQuery, Allocator.TempJob, out var transportJobHandle);
            transportJobHandle.Complete();

            try
            {
                m_TransportVehicleSelectData.SelectVehicle(
                    ref random,
                    routeContext.TransportType,
                    EnergyTypes.FuelAndElectricity,
                    routeContext.SizeClass,
                    publicTransportPurpose,
                    cargoResources,
                    out selectedPrimary,
                    out selectedSecondary,
                    ref passengerCapacity,
                    ref cargoCapacity);
            }
            finally
            {
                m_TransportVehicleSelectData.PostUpdate(default);
            }

            if (fillPrimary && selectedPrimary != Entity.Null && !ContainsPendingSelection(m_PendingPrimaryPrefabs, selectedPrimary))
            {
                m_PendingPrimaryPrefabs.Add(selectedPrimary);
            }

            if (fillSecondary && selectedSecondary != Entity.Null && !ContainsPendingSelection(m_PendingSecondaryPrefabs, selectedSecondary))
            {
                m_PendingSecondaryPrefabs.Add(selectedSecondary);
            }
        }

        private void LoadPersistedSelections()
        {
            m_PersistedSelections = PersistedRouteSelectionStore.Load();
        }

        private void SavePersistedSelections(string routePrefabName)
        {
            if (string.IsNullOrEmpty(routePrefabName))
            {
                return;
            }

            var routeSelection = PersistedRouteSelectionStore.FindOrCreate(m_PersistedSelections, routePrefabName);
            routeSelection.primary = SerializePendingPrefabs(m_PendingPrimaryPrefabs);
            routeSelection.secondary = SerializePendingPrefabs(m_PendingSecondaryPrefabs);

            if (routeSelection.primary.Count == 0 && routeSelection.secondary.Count == 0)
            {
                m_PersistedSelections.routes.Remove(routeSelection);
            }

            try
            {
                PersistedRouteSelectionStore.Save(m_PersistedSelections);
            }
            catch (System.Exception ex)
            {
                Mod.log.Error(ex, "Failed to save persisted selections");
            }
        }

        private void RestorePersistedSelections(string routePrefabName)
        {
            ResetPendingSelections();

            if (string.IsNullOrEmpty(routePrefabName) || m_PersistedSelections.routes == null)
            {
                return;
            }

            var routeSelection = PersistedRouteSelectionStore.Find(m_PersistedSelections, routePrefabName);
            if (routeSelection == null)
            {
                return;
            }

            RestorePendingPrefabs(routeSelection.primary, m_PendingPrimaryPrefabs);
            RestorePendingPrefabs(routeSelection.secondary, m_PendingSecondaryPrefabs);
        }

        private List<string> SerializePendingPrefabs(List<Entity> prefabs)
        {
            var result = new List<string>(prefabs.Count);
            for (var i = 0; i < prefabs.Count; i++)
            {
                var prefabName = m_PrefabSystem.GetPrefabName(prefabs[i]);
                if (!string.IsNullOrEmpty(prefabName))
                {
                    result.Add(prefabName);
                }
            }

            return result;
        }

        private void RestorePendingPrefabs(List<string> prefabNames, List<Entity> target)
        {
            if (prefabNames == null)
            {
                return;
            }

            for (var i = 0; i < prefabNames.Count; i++)
            {
                if (TryFindVehiclePrefab(prefabNames[i], out var prefab))
                {
                    target.Add(prefab);
                }
            }
        }

        private bool TryFindVehiclePrefab(string prefabName, out Entity prefab)
        {
            prefab = Entity.Null;
            if (string.IsNullOrEmpty(prefabName))
            {
                return false;
            }

            using (var transportVehicles = m_TransportVehiclePrefabQuery.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < transportVehicles.Length; i++)
                {
                    if (m_PrefabSystem.GetPrefabName(transportVehicles[i]) == prefabName)
                    {
                        prefab = transportVehicles[i];
                        return true;
                    }
                }
            }

            using (var workVehicles = m_WorkVehiclePrefabQuery.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < workVehicles.Length; i++)
                {
                    if (m_PrefabSystem.GetPrefabName(workVehicles[i]) == prefabName)
                    {
                        prefab = workVehicles[i];
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ContainsEntity(NativeList<Entity> list, Entity entity)
        {
            for (var i = 0; i < list.Length; i++)
            {
                if (list[i] == entity)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsPendingSelection(List<Entity> list, Entity entity)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] == entity)
                {
                    return true;
                }
            }

            return false;
        }

        private static void TogglePendingSelection(List<Entity> list, Entity entity)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] == entity)
                {
                    list.RemoveAt(i);
                    return;
                }
            }

            list.Add(entity);
        }

        private static void FilterPendingSelections(List<Entity> pending, NativeList<Entity> available)
        {
            for (var i = pending.Count - 1; i >= 0; i--)
            {
                if (!ContainsEntity(available, pending[i]))
                {
                    pending.RemoveAt(i);
                }
            }
        }

        private bool BufferMatchesPendingSelections(DynamicBuffer<VehicleModel> buffer, PlannedRouteContext routeContext)
        {
            var primaryCount = 0;
            var secondaryCount = 0;

            for (var i = 0; i < buffer.Length; i++)
            {
                var vehicleModel = buffer[i];
                if (vehicleModel.m_PrimaryPrefab != Entity.Null)
                {
                    if (!ContainsPendingSelection(m_PendingPrimaryPrefabs, vehicleModel.m_PrimaryPrefab))
                    {
                        return false;
                    }

                    primaryCount++;
                }

                if (vehicleModel.m_SecondaryPrefab != Entity.Null)
                {
                    if (!NeedsSecondarySelection(routeContext) || !ContainsPendingSelection(m_PendingSecondaryPrefabs, vehicleModel.m_SecondaryPrefab))
                    {
                        return false;
                    }

                    secondaryCount++;
                }
            }

            return primaryCount == m_PendingPrimaryPrefabs.Count
                && (!NeedsSecondarySelection(routeContext) || secondaryCount == m_PendingSecondaryPrefabs.Count);
        }

        private bool TryGetActiveRoutePrefab(out Entity routePrefab, out PlannedRouteContext routeContext)
        {
            routePrefab = Entity.Null;
            routeContext = default;

            if (m_ToolSystem.activeTool != m_RouteToolSystem)
            {
                return false;
            }

            var activePrefab = m_ToolSystem.activePrefab;
            if (activePrefab == null || !m_PrefabSystem.TryGetEntity(activePrefab, out routePrefab))
            {
                return false;
            }

            return TryBuildRouteContext(routePrefab, out routeContext);
        }

        private bool TryGetCurrentTempRoute(Entity routePrefab, out Entity routeEntity)
        {
            routeEntity = Entity.Null;

            if (m_TempRouteQuery.IsEmptyIgnoreFilter)
            {
                return false;
            }

            using var routes = m_TempRouteQuery.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < routes.Length; i++)
            {
                var candidate = routes[i];
                if (!EntityManager.HasComponent<PrefabRef>(candidate))
                {
                    continue;
                }

                var prefabRef = EntityManager.GetComponentData<PrefabRef>(candidate);
                if (prefabRef.m_Prefab == routePrefab)
                {
                    routeEntity = candidate;
                    return true;
                }
            }

            return false;
        }

        private string SerializeSelectedVehicleIndices(Entity routeEntity, NativeList<Entity> availableVehicles, bool primary)
        {
            if (!EntityManager.HasBuffer<VehicleModel>(routeEntity) || availableVehicles.Length == 0)
            {
                return "[]";
            }

            var builder = new StringBuilder();
            builder.Append('[');
            var buffer = EntityManager.GetBuffer<VehicleModel>(routeEntity, true);
            var first = true;
            var writtenEntityIndices = new HashSet<int>();

            foreach (var vehicleModel in buffer)
            {
                var prefab = primary ? vehicleModel.m_PrimaryPrefab : vehicleModel.m_SecondaryPrefab;
                if (prefab == Entity.Null)
                {
                    continue;
                }

                for (var i = 0; i < availableVehicles.Length; i++)
                {
                    if (availableVehicles[i] == prefab)
                    {
                        var entityIndex = prefab.Index;
                        if (!writtenEntityIndices.Add(entityIndex))
                        {
                            break;
                        }

                        if (!first)
                        {
                            builder.Append(',');
                        }

                        builder.Append(entityIndex);
                        first = false;
                        break;
                    }
                }
            }

            builder.Append(']');
            return builder.ToString();
        }

        private static string SerializePendingSelectionIndices(List<Entity> pendingPrefabs, NativeList<Entity> availableVehicles)
        {
            if (pendingPrefabs.Count == 0 || availableVehicles.Length == 0)
            {
                return "[]";
            }

            var builder = new StringBuilder();
            builder.Append('[');
            var first = true;

            for (var i = 0; i < pendingPrefabs.Count; i++)
            {
                var prefab = pendingPrefabs[i];
                for (var j = 0; j < availableVehicles.Length; j++)
                {
                    if (availableVehicles[j] != prefab)
                    {
                        continue;
                    }

                    if (!first)
                    {
                        builder.Append(',');
                    }

                    builder.Append(prefab.Index);
                    first = false;
                    break;
                }
            }

            builder.Append(']');
            return builder.ToString();
        }

        private string GetCurrentSelectedVehicleSummary(Entity routeEntity, bool primary)
        {
            if (!EntityManager.HasBuffer<VehicleModel>(routeEntity))
            {
                return string.Empty;
            }

            var buffer = EntityManager.GetBuffer<VehicleModel>(routeEntity, true);
            var names = new List<string>(4);
            var seenEntityIndices = new HashSet<int>();

            foreach (var vehicleModel in buffer)
            {
                var prefab = primary ? vehicleModel.m_PrimaryPrefab : vehicleModel.m_SecondaryPrefab;
                if (prefab != Entity.Null && seenEntityIndices.Add(prefab.Index))
                {
                    names.Add(GetPrefabDisplayName(prefab));
                }
            }

            if (names.Count == 0)
            {
                return string.Empty;
            }

            if (names.Count == 1)
            {
                return names[0];
            }

            return $"{names.Count} selected";
        }

        private string GetPendingSelectedVehicleSummary(List<Entity> prefabs)
        {
            if (prefabs.Count == 0)
            {
                return string.Empty;
            }

            if (prefabs.Count == 1)
            {
                return GetPrefabDisplayName(prefabs[0]);
            }

            return $"{prefabs.Count} selected";
        }

        private bool TryBuildRouteContext(Entity routePrefab, out PlannedRouteContext routeContext)
        {
            routeContext = default;

            if (EntityManager.HasComponent<TransportLineData>(routePrefab))
            {
                var lineData = EntityManager.GetComponentData<TransportLineData>(routePrefab);
                routeContext = new PlannedRouteContext
                {
                    TransportType = lineData.m_TransportType,
                    SizeClass = lineData.m_SizeClass,
                    CargoTransport = lineData.m_CargoTransport,
                    PassengerTransport = lineData.m_PassengerTransport
                };
                return true;
            }

            if (EntityManager.HasComponent<WorkRouteData>(routePrefab))
            {
                var workRouteData = EntityManager.GetComponentData<WorkRouteData>(routePrefab);
                routeContext = new PlannedRouteContext
                {
                    TransportType = TransportType.Work,
                    SizeClass = workRouteData.m_SizeClass,
                    CargoTransport = true,
                    IsWorkRoute = true,
                    MapFeature = workRouteData.m_MapFeature,
                    RoadTypes = workRouteData.m_RoadType
                };
                return true;
            }

            return false;
        }

        private void RefreshAvailableVehicles(PlannedRouteContext routeContext)
        {
            m_AvailablePrimaryVehicles.Clear();
            m_AvailableSecondaryVehicles.Clear();
            if (routeContext.IsWorkRoute)
            {
                RefreshWorkVehiclesFromOfficialSelector(routeContext);
                return;
            }

            RefreshTransportVehiclesFromOfficialSelector(routeContext);
        }

        private void RefreshTransportVehiclesFromOfficialSelector(PlannedRouteContext routeContext)
        {
            var energyTypes = CollectDepotEnergyTypes(routeContext.TransportType);
            var publicTransportPurpose = routeContext.CargoTransport ? (PublicTransportPurpose)0 : PublicTransportPurpose.TransportLine;
            var cargoResources = routeContext.CargoTransport ? unchecked((Resource)(-1)) : Resource.NoResource;

            m_TransportVehicleSelectData.PreUpdate(this, m_CityConfigurationSystem, m_TransportVehiclePrefabQuery, Allocator.TempJob, out var jobHandle);
            jobHandle.Complete();

            try
            {
                m_TransportVehicleSelectData.ListVehicles(
                    routeContext.TransportType,
                    energyTypes,
                    routeContext.SizeClass,
                    publicTransportPurpose,
                    cargoResources,
                    m_AvailablePrimaryVehicles,
                    m_AvailableSecondaryVehicles,
                    ignoreTheme: true);
            }
            finally
            {
                m_TransportVehicleSelectData.PostUpdate(default);
            }
        }

        private void RefreshWorkVehiclesFromOfficialSelector(PlannedRouteContext routeContext)
        {
            m_WorkVehicleSelectData.PreUpdate(this, m_CityConfigurationSystem, m_WorkVehiclePrefabQuery, Allocator.TempJob, out var jobHandle);
            jobHandle.Complete();

            try
            {
                m_WorkVehicleSelectData.ListVehicles(
                    routeContext.RoadTypes,
                    routeContext.SizeClass,
                    VehicleWorkType.Harvest,
                    routeContext.MapFeature,
                    Resource.NoResource,
                    m_AvailablePrimaryVehicles);
            }
            finally
            {
                m_WorkVehicleSelectData.PostUpdate(default);
            }
        }

        private EnergyTypes CollectDepotEnergyTypes(TransportType transportType)
        {
            if (m_DepotResults.IsCreated)
            {
                for (var i = 0; i < m_DepotResults.Length; i++)
                {
                    m_DepotResults[i] = 0;
                }
            }

            using var depots = m_DepotQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < depots.Length; i++)
            {
                var depot = depots[i];
                if (!EntityManager.HasComponent<PrefabRef>(depot))
                {
                    continue;
                }

                var prefabRef = EntityManager.GetComponentData<PrefabRef>(depot);
                if (!EntityManager.HasComponent<TransportDepotData>(prefabRef.m_Prefab))
                {
                    continue;
                }

                var depotData = EntityManager.GetComponentData<TransportDepotData>(prefabRef.m_Prefab);

                if (EntityManager.HasBuffer<InstalledUpgrade>(depot))
                {
                    var installedUpgrades = EntityManager.GetBuffer<InstalledUpgrade>(depot, true);
                    UpgradeUtils.CombineStats(EntityManager, ref depotData, installedUpgrades);
                }

                if (depotData.m_TransportType != transportType)
                {
                    continue;
                }

                m_DepotResults[0] = 1;
                m_DepotResults[1] |= (int)depotData.m_EnergyTypes;
            }

            return (EnergyTypes)m_DepotResults[1];
        }

        private DynamicBuffer<VehicleModel> EnsureVehicleModelBuffer(Entity routeEntity)
        {
            if (!EntityManager.HasBuffer<VehicleModel>(routeEntity))
            {
                return EntityManager.AddBuffer<VehicleModel>(routeEntity);
            }

            return EntityManager.GetBuffer<VehicleModel>(routeEntity);
        }
        private static bool NeedsSecondarySelection(PlannedRouteContext routeContext)
        {
            return !routeContext.IsWorkRoute
                && routeContext.TransportType == TransportType.Train
                && routeContext.CargoTransport
                && !routeContext.PassengerTransport;
        }
    }
}
