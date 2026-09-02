using System.Collections.Generic;
using SpacetimeDB.Types;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Sea.Client
{
    public sealed partial class SeaWorldView
    {
        private readonly HashSet<ulong> relevantEndpointIds = new();
        private readonly SeaRowRegistry<ulong, ShipMovement> movementRows = new();

        private void HandleShipChanged(Ship ship)
        {
            shipRows.Upsert(ship.EntityId, ship);
            PushMovementSample(
                ship.EntityId,
                ship.PositionX,
                ship.PositionY,
                ship.HeadingDegrees);
            if (ship.EntityId == connection.LocalShipEntityId)
            {
                localShip = ship;
                playerEntityId = ship.EntityId;
            }

            visibilityDirty = true;
        }

        private void HandleShipMovementChanged(ShipMovement movement)
        {
            movementRows.Upsert(movement.EntityId, movement);
            PushMovementSample(
                movement.EntityId,
                movement.PositionX,
                movement.PositionY,
                movement.HeadingDegrees);
            visibilityDirty = true;
        }

        private void PushMovementSample(
            ulong entityId,
            float positionX,
            float positionY,
            float headingDegrees)
        {
            if (!targets.TryGetValue(entityId, out var interpolation))
            {
                interpolation = new SeaInterpolationBuffer();
                targets.Add(entityId, interpolation);
            }

            interpolation.Push(
                ToWorld(positionX, positionY, ShipRootHeight),
                headingDegrees,
                Time.realtimeSinceStartupAsDouble);
        }

        private void HandleWorldObjectChanged(WorldObject entity)
        {
            if (!mapGeometry.TryGetValue(entity.EntityId, out var geometry))
            {
                geometry = CreateWorldGeometry(entity);
                if (geometry == null)
                {
                    return;
                }

                mapGeometry.Add(entity.EntityId, geometry);
            }

            geometry.SetActive(entity.IsActive);
            geometry.transform.position = ToWorld(entity.PositionX, entity.PositionY, 0f);
        }

        private GameObject CreateWorldGeometry(WorldObject entity)
        {
            var position = ToWorld(entity.PositionX, entity.PositionY, 0f);
            return entity.Kind switch
            {
                "island" => SeaWorldGeometryFactory.CreateIsland(
                    $"Map island {entity.EntityId}",
                    position,
                    entity.Radius,
                    sandMaterial,
                    rockMaterial,
                    landMaterial),
                "reef" => SeaWorldGeometryFactory.CreateReef(
                    $"Map reef {entity.EntityId}",
                    position,
                    entity.Radius,
                    shallowsMaterial,
                    rockMaterial),
                "harbor" => SeaWorldGeometryFactory.CreateHarbor(
                    $"Map harbor {entity.EntityId}",
                    position,
                    entity.Radius,
                    shallowsMaterial,
                    dockMaterial),
                "shoal" => SeaWorldGeometryFactory.CreateShoal(
                    $"Map shoal {entity.EntityId}",
                    position,
                    entity.Radius,
                    shoalMaterial),
                "storm" => SeaWorldGeometryFactory.CreateStorm(
                    $"Map storm {entity.EntityId}",
                    position,
                    entity.Radius,
                    stormMaterial),
                _ => null,
            };
        }

        private void HandleVolleyChanged(Volley volley)
        {
            if (volley.IsActive)
            {
                volleyRows.Upsert(volley.VolleyId, volley);
            }
            else
            {
                volleyRows.Remove(volley.VolleyId);
            }

            RebuildRelevantEndpoints();
        }

        private void HandleVolleyRemoved(ulong volleyId)
        {
            volleyRows.Remove(volleyId);
            RebuildRelevantEndpoints();
        }

        private void HandleWorldTickChanged(ulong tick) => worldTick = tick;

        private void RebuildRelevantEndpoints()
        {
            relevantEndpointIds.Clear();
            foreach (var volley in volleyRows.Values)
            {
                relevantEndpointIds.Add(volley.SourceEntityId);
                relevantEndpointIds.Add(volley.TargetEntityId);
            }

            visibilityDirty = true;
        }

        private void ReconcileVisibility()
        {
            if (!assetsReady)
            {
                return;
            }

            var cameraTransform = Camera.main != null ? Camera.main.transform : null;
            var origin = cameraTransform != null
                ? new Vector3(cameraTransform.position.x, 0f, cameraTransform.position.z)
                : localShip == null
                    ? Vector3.zero
                    : MovementPosition(localShip);
            if (!visibilityDirty && (origin - previousVisibilityOrigin).sqrMagnitude < 0.25f)
            {
                return;
            }

            visibilityDirty = false;
            previousVisibilityOrigin = origin;
            visibilityCandidates.Clear();
            var trackedCount = 0;
            foreach (var ship in shipRows.Values)
            {
                if (!ship.IsActive || trackedCount >= MaximumTrackedShipRows)
                {
                    continue;
                }

                visibilityEntityIds[trackedCount] = ship.EntityId;
                var position = MovementPosition2(ship);
                visibilityPositions[trackedCount] = new float2(position.x, position.y);
                trackedCount++;
            }

            var distanceJob = new SeaVisibilityDistanceJob
            {
                Positions = visibilityPositions,
                SquaredDistances = visibilitySquaredDistances,
                Origin = new float2(origin.x, origin.z),
            };
            distanceJob.Schedule(trackedCount, innerloopBatchCount: 64).Complete();
            for (var index = 0; index < trackedCount; index++)
            {
                var entityId = visibilityEntityIds[index];
                if (!shipRows.TryGetValue(entityId, out var ship))
                {
                    continue;
                }

                var distance = Mathf.Sqrt(visibilitySquaredDistances[index]);
                var relevant = ship.EntityId == playerEntityId ||
                    ship.EntityId == localShip?.TargetEntityId ||
                    relevantEndpointIds.Contains(ship.EntityId);
                var level = SeaPresentationRules.LevelFor(distance, relevant);
                if (level == SeaPresentationLevel.Hidden)
                {
                    continue;
                }

                visibilityCandidates.Add(new SeaVisibilityCandidate(
                    ship.EntityId,
                    distance,
                    relevant ? 0 : 1,
                    level));
            }

            visibilityCandidates.Sort(SeaVisibilityCandidateComparer.Instance);
            desiredPresentations.Clear();
            var limit = SeaPresentationRules.VisibleShipLimit(
                SeaPresentationRules.CurrentPlatform());
            var visibleCount = Mathf.Min(limit, visibilityCandidates.Count);
            for (var index = 0; index < visibleCount; index++)
            {
                desiredPresentations.Add(visibilityCandidates[index].EntityId);
            }

            releaseEntityIds.Clear();
            foreach (var entity in entities)
            {
                if (!desiredPresentations.Contains(entity.Key))
                {
                    releaseEntityIds.Add(entity.Key);
                }
            }

            foreach (var entityId in releaseEntityIds)
            {
                ReleaseShipPresentation(entityId);
            }

            for (var index = 0; index < visibleCount; index++)
            {
                var candidate = visibilityCandidates[index];
                if (!shipRows.TryGetValue(candidate.EntityId, out var ship))
                {
                    continue;
                }

                if (!entities.TryGetValue(candidate.EntityId, out var shipObject))
                {
                    shipObject = CreateShip(ShipName(ship), ship);
                    if (shipObject == null)
                    {
                        break;
                    }

                    shipObject.transform.position = ToWorld(
                        movementRows.TryGetValue(ship.EntityId, out var movement)
                            ? movement.PositionX
                            : ship.PositionX,
                        movement != null ? movement.PositionY : ship.PositionY,
                        ShipRootHeight);
                    entities.Add(ship.EntityId, shipObject);
                    if (ship.EntityId == playerEntityId)
                    {
                        playerObject = shipObject;
                        playerFeedback = shipFeedback[ship.EntityId];
                    }
                }

                shipObject.GetComponent<SeaShipPresentation>().Apply(
                    ship.Hull,
                    ship.MaxHull,
                    movementRows.TryGetValue(ship.EntityId, out var latestMovement)
                        ? latestMovement.Speed
                        : ship.Speed,
                    ship.MaximumSpeed,
                    candidate.Level,
                    ship.FactionCode,
                    ship.ArchetypeCode);
            }
        }

        private void UpdateLocalPresentation()
        {
            if (localShip == null)
            {
                return;
            }

            fogMaterial.SetVector(
                "_PlayerPosition",
                movementRows.TryGetValue(localShip.EntityId, out var movement)
                    ? new Vector4(movement.PositionX, movement.PositionY, 0f, 0f)
                    : new Vector4(localShip.PositionX, localShip.PositionY, 0f, 0f));
            UpdateTargetRing(localShip);
            UpdateCourseIndicator(localShip);
        }

        private static string ShipName(Ship ship) => ship.FactionCode == 1
            ? $"Player Ship {ship.EntityId}"
            : $"Enemy Ship {ship.EntityId}";

        private Vector3 MovementPosition(Ship ship)
        {
            var position = MovementPosition2(ship);
            return new Vector3(position.x, 0f, position.y);
        }

        private Vector2 MovementPosition2(Ship ship) =>
            movementRows.TryGetValue(ship.EntityId, out var movement)
                ? new Vector2(movement.PositionX, movement.PositionY)
                : new Vector2(ship.PositionX, ship.PositionY);

        private void ReleaseShipPresentation(ulong entityId)
        {
            if (!entities.Remove(entityId, out var shipObject))
            {
                return;
            }

            shipFeedback.Remove(entityId);
            if (entityId == playerEntityId)
            {
                playerObject = null;
                playerFeedback = null;
            }

            if (shipObject != null)
            {
                shipPool.Release(shipObject);
            }
        }

        private readonly struct SeaVisibilityCandidate
        {
            public SeaVisibilityCandidate(
                ulong entityId,
                float distance,
                int priority,
                SeaPresentationLevel level)
            {
                EntityId = entityId;
                Distance = distance;
                Priority = priority;
                Level = level;
            }

            public ulong EntityId { get; }
            public float Distance { get; }
            public int Priority { get; }
            public SeaPresentationLevel Level { get; }
        }

        private sealed class SeaVisibilityCandidateComparer : IComparer<SeaVisibilityCandidate>
        {
            public static readonly SeaVisibilityCandidateComparer Instance = new();

            public int Compare(SeaVisibilityCandidate left, SeaVisibilityCandidate right)
            {
                var priority = left.Priority.CompareTo(right.Priority);
                if (priority != 0)
                {
                    return priority;
                }

                var distance = left.Distance.CompareTo(right.Distance);
                return distance != 0
                    ? distance
                    : left.EntityId.CompareTo(right.EntityId);
            }
        }
    }
}
