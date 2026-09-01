using SpacetimeDB.Types;
using UnityEngine;

namespace Sea.Client
{
    public sealed partial class SeaWorldView
    {
        private const ulong SyntheticPerformanceEntityBase = 9_000_000_000_000_000_000;

        internal int VisibleShipPresentationCount => entities.Count;

        internal void SeedSyntheticPerformanceFleet(int count)
        {
            for (var index = 0; index < count; index++)
            {
                var column = index % 10;
                var row = index / 10;
                var positionX = -27f + column * 6f;
                var positionY = -27f + row * 6f;
                HandleShipChanged(new Ship
                {
                    EntityId = SyntheticPerformanceEntityBase + (ulong)index,
                    ArchetypeCode = 1,
                    FactionCode = 2,
                    PositionX = positionX,
                    PositionY = positionY,
                    DestinationX = positionX + 4f,
                    DestinationY = positionY,
                    HeadingDegrees = index * 17f % 360f,
                    Speed = 5f,
                    MaximumSpeed = 8f,
                    IsMoving = true,
                    IsActive = true,
                    IsAlive = true,
                    Hull = 750,
                    MaxHull = 1_000,
                    Sails = 500,
                    MaxSails = 500,
                    Cannons = 400,
                    MaxCannons = 400,
                    Crew = 300,
                    MaxCrew = 300,
                });
            }

            ReconcileVisibility();
        }

        internal void RunSyntheticPerformanceFrame()
        {
            ReconcileVisibility();
            UpdateEntityTransforms();
            SyncCombatPresentation();
        }
    }
}
