using UnityEngine;

namespace Sea.Client
{
    public sealed partial class SeaWorldView
    {
        private SeaConnectionController interestConnection;

        private void BindInterestCallbacks(SeaConnectionController next)
        {
            if (interestConnection == next)
            {
                return;
            }

            UnbindInterestCallbacks();
            interestConnection = next;
            if (interestConnection == null)
            {
                return;
            }

            interestConnection.ShipLeftInterest += RemoveShipPresentation;
            interestConnection.WorldObjectLeftInterest += RemoveWorldObjectPresentation;
            interestConnection.ShipChanged += HandleShipChanged;
            interestConnection.ShipMovementChanged += HandleShipMovementChanged;
            interestConnection.ShipMovementLeftInterest += HandleShipMovementRemoved;
            interestConnection.WorldObjectChanged += HandleWorldObjectChanged;
            interestConnection.VolleyChanged += HandleVolleyChanged;
            interestConnection.VolleyLeftInterest += HandleVolleyRemoved;
            interestConnection.HitLanded += HandleHitLanded;
            interestConnection.WorldTickChanged += HandleWorldTickChanged;
            interestConnection.LootChanged += HandleLootChanged;
            interestConnection.LootLeftInterest += HandleLootRemoved;
            interestConnection.PresentationReset += ResetPresentations;
        }

        private void UnbindInterestCallbacks()
        {
            if (interestConnection == null)
            {
                return;
            }

            interestConnection.ShipLeftInterest -= RemoveShipPresentation;
            interestConnection.WorldObjectLeftInterest -= RemoveWorldObjectPresentation;
            interestConnection.ShipChanged -= HandleShipChanged;
            interestConnection.ShipMovementChanged -= HandleShipMovementChanged;
            interestConnection.ShipMovementLeftInterest -= HandleShipMovementRemoved;
            interestConnection.WorldObjectChanged -= HandleWorldObjectChanged;
            interestConnection.VolleyChanged -= HandleVolleyChanged;
            interestConnection.VolleyLeftInterest -= HandleVolleyRemoved;
            interestConnection.HitLanded -= HandleHitLanded;
            interestConnection.WorldTickChanged -= HandleWorldTickChanged;
            interestConnection.LootChanged -= HandleLootChanged;
            interestConnection.LootLeftInterest -= HandleLootRemoved;
            interestConnection.PresentationReset -= ResetPresentations;
            interestConnection = null;
        }

        private void RemoveShipPresentation(ulong entityId)
        {
            targets.Remove(entityId);
            shipRows.Remove(entityId);
            movementRows.Remove(entityId);
            if (localShip?.EntityId == entityId)
            {
                localShip = null;
                playerEntityId = 0;
            }

            ReleaseShipPresentation(entityId);
            visibilityDirty = true;
        }

        private void HandleShipMovementRemoved(ulong entityId) =>
            movementRows.Remove(entityId);

        private void RemoveWorldObjectPresentation(ulong entityId)
        {
            if (mapGeometry.Remove(entityId, out var geometry) && geometry != null)
            {
                Destroy(geometry);
            }
        }

        private void ResetPresentations()
        {
            releaseEntityIds.Clear();
            foreach (var entityId in entities.Keys)
            {
                releaseEntityIds.Add(entityId);
            }

            foreach (var entityId in releaseEntityIds)
            {
                ReleaseShipPresentation(entityId);
            }

            foreach (var geometry in mapGeometry.Values)
            {
                if (geometry != null)
                {
                    Destroy(geometry);
                }
            }

            mapGeometry.Clear();
            shipRows.Clear();
            movementRows.Clear();
            targets.Clear();
            snapshotClock = null;
            volleyRows.Clear();
            relevantEndpointIds.Clear();
            localShip = null;
            playerEntityId = 0;
            worldTick = 0;
            if (targetRing != null)
            {
                targetRing.SetActive(false);
            }

            if (ownShipRing != null)
            {
                ownShipRing.SetActive(false);
            }

            if (coursePing != null)
            {
                coursePing.gameObject.SetActive(false);
            }

            combatPresenter?.Reset();
            ResetLootPresentations();
            visibilityDirty = true;
        }

        private void OnDestroy()
        {
            UnbindInterestCallbacks();
            ownedAssetLease?.Release();
            if (visibilityPositions.IsCreated)
            {
                visibilityPositions.Dispose();
            }

            if (visibilitySquaredDistances.IsCreated)
            {
                visibilitySquaredDistances.Dispose();
            }
        }

        // The chart's own flip lives in SeaChartCoordinates and nowhere else. This drew the
        // world upside down while it read `new(x, height, y)`: chart y grows south, world z
        // draws up the screen, and nothing turned one into the other.
        private static Vector3 ToWorld(float x, float y, float height) =>
            SeaChartCoordinates.ToWorld(x, y, height);
    }
}
