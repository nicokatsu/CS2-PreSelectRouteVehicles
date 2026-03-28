using System;
using System.Collections.Generic;
using Game.Areas;
using Game.Net;
using Game.Prefabs;
using Game.Routes;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace VehiclePreSelection
{
    internal static class RouteColorRandomizationUtils
    {
        internal struct RouteColorFamily
        {
            public bool IsWorkRoute;
            public TransportType TransportType;
            public bool CargoTransport;
            public bool PassengerTransport;
            public MapFeature MapFeature;
            public RoadTypes RoadTypes;
        }

        // Bright real-world transit palette inspired by official route-map colors.
        // Prefer high-contrast, high-saturation line colors over darker signage tones.
        private static readonly Color32[] s_Palette =
        {
            new Color32(0x00, 0x7A, 0xCC, 0xFF), // bright route blue
            new Color32(0x00, 0xA8, 0xE8, 0xFF), // sky blue
            new Color32(0x00, 0xB1, 0x8A, 0xFF), // vivid teal
            new Color32(0x00, 0xB1, 0x4F, 0xFF), // bright green
            new Color32(0x7A, 0xC9, 0x43, 0xFF), // lime green
            new Color32(0xC4, 0xD6, 0x00, 0xFF), // yellow-green
            new Color32(0xFF, 0xC8, 0x24, 0xFF), // bright yellow
            new Color32(0xFF, 0xA3, 0x00, 0xFF), // amber
            new Color32(0xFF, 0x7A, 0x00, 0xFF), // bright orange
            new Color32(0xF9, 0x4F, 0x43, 0xFF), // vivid red-orange
            new Color32(0xE4, 0x00, 0x2B, 0xFF), // route red
            new Color32(0xFF, 0x4D, 0x6D, 0xFF), // bright pink-red
            new Color32(0xFF, 0x5C, 0xB8, 0xFF), // pink
            new Color32(0xD2, 0x4D, 0xFF, 0xFF), // violet
            new Color32(0xA1, 0x4B, 0xFF, 0xFF), // bright purple
            new Color32(0x7B, 0x61, 0xFF, 0xFF), // line purple
            new Color32(0x42, 0x7B, 0xFF, 0xFF), // electric blue
            new Color32(0x00, 0xC2, 0xFF, 0xFF), // cyan
            new Color32(0x00, 0xD4, 0xC7, 0xFF), // aqua
            new Color32(0xFF, 0x8A, 0x65, 0xFF), // light coral
            new Color32(0xFF, 0xB7, 0x4D, 0xFF), // warm orange
            new Color32(0x9C, 0xD6, 0x3B, 0xFF), // fresh green
            new Color32(0x5E, 0xC8, 0xFF, 0xFF), // bright light blue
            new Color32(0xD8, 0x6D, 0xFF, 0xFF)  // bright magenta
        };

        internal static bool TryBuildFamily(EntityManager entityManager, Entity routePrefab, out RouteColorFamily family)
        {
            family = default;

            if (entityManager.HasComponent<TransportLineData>(routePrefab))
            {
                var lineData = entityManager.GetComponentData<TransportLineData>(routePrefab);
                family = new RouteColorFamily
                {
                    TransportType = lineData.m_TransportType,
                    CargoTransport = lineData.m_CargoTransport,
                    PassengerTransport = lineData.m_PassengerTransport
                };
                return true;
            }

            if (entityManager.HasComponent<WorkRouteData>(routePrefab))
            {
                var workRouteData = entityManager.GetComponentData<WorkRouteData>(routePrefab);
                family = new RouteColorFamily
                {
                    IsWorkRoute = true,
                    TransportType = TransportType.Work,
                    CargoTransport = true,
                    MapFeature = workRouteData.m_MapFeature,
                    RoadTypes = workRouteData.m_RoadType
                };
                return true;
            }

            return false;
        }

        internal static string BuildKey(Entity routePrefab, EntityManager entityManager, PrefabSystem prefabSystem)
        {
            if (!TryBuildFamily(entityManager, routePrefab, out var family))
            {
                return prefabSystem.GetPrefabName(routePrefab);
            }

            return BuildKey(family);
        }

        internal static string BuildKey(RouteColorFamily family)
        {
            if (family.IsWorkRoute)
            {
                return $"work:{family.MapFeature}:{family.RoadTypes}";
            }

            return $"transport:{family.TransportType}:cargo={family.CargoTransport}:passenger={family.PassengerTransport}";
        }

        internal static bool Matches(RouteColorFamily left, RouteColorFamily right)
        {
            return left.IsWorkRoute == right.IsWorkRoute
                && left.TransportType == right.TransportType
                && left.CargoTransport == right.CargoTransport
                && left.PassengerTransport == right.PassengerTransport
                && left.MapFeature == right.MapFeature
                && left.RoadTypes == right.RoadTypes;
        }

        internal static Color32 ChooseRandomColor(
            EntityManager entityManager,
            EntityQuery coloredRoutesQuery,
            Entity excludeRoute,
            RouteColorFamily family)
        {
            var usageCounts = new int[s_Palette.Length];

            using var routes = coloredRoutesQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < routes.Length; i++)
            {
                var routeEntity = routes[i];
                if (routeEntity == excludeRoute
                    || !entityManager.HasComponent<PrefabRef>(routeEntity)
                    || !entityManager.HasComponent<Game.Routes.Color>(routeEntity))
                {
                    continue;
                }

                var routePrefab = entityManager.GetComponentData<PrefabRef>(routeEntity).m_Prefab;
                if (!TryBuildFamily(entityManager, routePrefab, out var routeFamily) || !Matches(family, routeFamily))
                {
                    continue;
                }

                var color = entityManager.GetComponentData<Game.Routes.Color>(routeEntity).m_Color;
                var paletteIndex = IndexOfPaletteColor(color);
                if (paletteIndex >= 0)
                {
                    usageCounts[paletteIndex]++;
                }
            }

            var minimumUsage = int.MaxValue;
            for (var i = 0; i < usageCounts.Length; i++)
            {
                minimumUsage = Math.Min(minimumUsage, usageCounts[i]);
            }

            var candidateIndices = new List<int>(s_Palette.Length);
            for (var i = 0; i < usageCounts.Length; i++)
            {
                if (usageCounts[i] == minimumUsage)
                {
                    candidateIndices.Add(i);
                }
            }

            if (candidateIndices.Count == 0)
            {
                return s_Palette[0];
            }

            var seed = unchecked((int)DateTime.UtcNow.Ticks) ^ excludeRoute.Index ^ family.TransportType.GetHashCode() ^ BuildKey(family).GetHashCode();
            var random = new System.Random(seed);
            return s_Palette[candidateIndices[random.Next(candidateIndices.Count)]];
        }

        private static int IndexOfPaletteColor(Color32 color)
        {
            for (var i = 0; i < s_Palette.Length; i++)
            {
                if (s_Palette[i].r == color.r
                    && s_Palette[i].g == color.g
                    && s_Palette[i].b == color.b
                    && s_Palette[i].a == color.a)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
