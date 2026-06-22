using System.Collections.Generic;
using Game.Routes;
using Unity.Entities;

namespace VehiclePreSelection
{
    internal static class VehicleModelSelectionWriter
    {
        internal static DynamicBuffer<VehicleModel> EnsureBuffer(EntityManager entityManager, Entity routeEntity)
        {
            if (!entityManager.HasBuffer<VehicleModel>(routeEntity))
            {
                return entityManager.AddBuffer<VehicleModel>(routeEntity);
            }

            return entityManager.GetBuffer<VehicleModel>(routeEntity);
        }

        internal static void WriteSelections(
            DynamicBuffer<VehicleModel> buffer,
            IReadOnlyList<Entity> primaryPrefabs,
            IReadOnlyList<Entity> secondaryPrefabs,
            bool includeSecondary)
        {
            buffer.Clear();

            for (var i = 0; i < primaryPrefabs.Count; i++)
            {
                buffer.Add(new VehicleModel
                {
                    m_PrimaryPrefab = primaryPrefabs[i],
                    m_SecondaryPrefab = Entity.Null
                });
            }

            if (!includeSecondary)
            {
                return;
            }

            for (var i = 0; i < secondaryPrefabs.Count; i++)
            {
                buffer.Add(new VehicleModel
                {
                    m_PrimaryPrefab = Entity.Null,
                    m_SecondaryPrefab = secondaryPrefabs[i]
                });
            }
        }

        internal static bool BufferMatchesSelections(
            DynamicBuffer<VehicleModel> buffer,
            IReadOnlyList<Entity> primaryPrefabs,
            IReadOnlyList<Entity> secondaryPrefabs,
            bool includeSecondary)
        {
            var primaryCount = 0;
            var secondaryCount = 0;

            for (var i = 0; i < buffer.Length; i++)
            {
                var vehicleModel = buffer[i];
                if (vehicleModel.m_PrimaryPrefab != Entity.Null)
                {
                    if (!Contains(primaryPrefabs, vehicleModel.m_PrimaryPrefab))
                    {
                        return false;
                    }

                    primaryCount++;
                }

                if (vehicleModel.m_SecondaryPrefab != Entity.Null)
                {
                    if (!includeSecondary || !Contains(secondaryPrefabs, vehicleModel.m_SecondaryPrefab))
                    {
                        return false;
                    }

                    secondaryCount++;
                }
            }

            return primaryCount == primaryPrefabs.Count
                && (!includeSecondary || secondaryCount == secondaryPrefabs.Count);
        }

        private static bool Contains(IReadOnlyList<Entity> list, Entity entity)
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
    }
}
