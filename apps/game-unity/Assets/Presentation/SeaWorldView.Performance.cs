using SpacetimeDB.Types;
using UnityEngine;

namespace Sea.Client
{
    public sealed partial class SeaWorldView
    {
        private const ulong SyntheticPerformanceEntityBase = 9_000_000_000_000_000_000;

        internal int VisibleShipPresentationCount => entities.Count;

        internal int SyntheticShipPoolCreatedCount => shipPool?.CreatedCount ?? 0;

        internal void SeedSyntheticPerformanceFleet(int count)
        {
            var cameraTransform = ChartCameraTransform();
            var center = cameraTransform == null
                ? Vector2.zero
                : new Vector2(cameraTransform.position.x, cameraTransform.position.z);
            for (var index = 0; index < count; index++)
            {
                var position = SeaRuntimeValidationRules.SyntheticFleetPosition(
                    index,
                    count,
                    center);
                HandleShipChanged(new Ship
                {
                    EntityId = SyntheticPerformanceEntityBase + (ulong)index,
                    ArchetypeCode = 1,
                    FactionCode = 2,
                    PositionX = position.x,
                    PositionY = position.y,
                    DestinationX = position.x + 4f,
                    DestinationY = position.y,
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
