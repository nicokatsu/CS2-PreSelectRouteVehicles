using Game.Areas;
using Game.Net;
using Game.Prefabs;
using Game.Routes;
using Game.Vehicles;
using Unity.Entities;

namespace VehiclePreSelection
{
    internal struct RouteSelectionContext
    {
        internal TransportType TransportType;
        internal SizeClass SizeClass;
        internal bool CargoTransport;
        internal bool PassengerTransport;
        internal bool IsWorkRoute;
        internal MapFeature MapFeature;
        internal RoadTypes RoadTypes;

        internal bool NeedsSecondarySelection =>
            !IsWorkRoute
            && TransportType == TransportType.Train
            && CargoTransport
            && !PassengerTransport;

        internal static bool TryCreate(EntityManager entityManager, Entity routePrefab, out RouteSelectionContext context)
        {
            context = default;

            if (entityManager.HasComponent<TransportLineData>(routePrefab))
            {
                var lineData = entityManager.GetComponentData<TransportLineData>(routePrefab);
                context = new RouteSelectionContext
                {
                    TransportType = lineData.m_TransportType,
                    SizeClass = lineData.m_SizeClass,
                    CargoTransport = lineData.m_CargoTransport,
                    PassengerTransport = lineData.m_PassengerTransport
                };
                return true;
            }

            if (entityManager.HasComponent<WorkRouteData>(routePrefab))
            {
                var workRouteData = entityManager.GetComponentData<WorkRouteData>(routePrefab);
                context = new RouteSelectionContext
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

        internal string ToDiagnosticString()
        {
            if (IsWorkRoute)
            {
                return $"WorkRoute mapFeature={MapFeature}, roadTypes={RoadTypes}, sizeClass={SizeClass}";
            }

            return $"TransportRoute transportType={TransportType}, sizeClass={SizeClass}, cargo={CargoTransport}, passenger={PassengerTransport}, secondary={NeedsSecondarySelection}";
        }
    }
}
