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
                : SeaChartCoordinates.ToChart(cameraTransform.position);
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
                    BaseSpeedSquaresPerSecond = 8f,
                    EffectiveSpeedSquaresPerSecond = 8f,
                    IsMoving = true,
                    IsActive = true,
                    IsAlive = true,
                    Hull = 750,
                    MaxHull = 1_000,
                    MagazineSize = 3,
                    ReadyVolleys = 3,
                    ReloadTicks = 30,
                    ArmorFront = 0.25f,
                    ArmorSides = 0.1f,
                    ArmorBack = 0.05f,
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
