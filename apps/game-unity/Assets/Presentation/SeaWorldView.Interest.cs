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
            interestConnection.WorldObjectChanged += HandleWorldObjectChanged;
            interestConnection.VolleyChanged += HandleVolleyChanged;
            interestConnection.VolleyLeftInterest += HandleVolleyRemoved;
            interestConnection.WorldTickChanged += HandleWorldTickChanged;
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
            interestConnection.WorldObjectChanged -= HandleWorldObjectChanged;
            interestConnection.VolleyChanged -= HandleVolleyChanged;
            interestConnection.VolleyLeftInterest -= HandleVolleyRemoved;
            interestConnection.WorldTickChanged -= HandleWorldTickChanged;
            interestConnection.PresentationReset -= ResetPresentations;
            interestConnection = null;
        }

        private void RemoveShipPresentation(ulong entityId)
        {
            targets.Remove(entityId);
            shipRows.Remove(entityId);
            if (localShip?.EntityId == entityId)
            {
                localShip = null;
                playerEntityId = 0;
            }

            ReleaseShipPresentation(entityId);
            visibilityDirty = true;
        }

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
            targets.Clear();
            volleyRows.Clear();
            relevantEndpointIds.Clear();
            localShip = null;
            playerEntityId = 0;
            worldTick = 0;
            if (targetRing != null)
            {
                targetRing.SetActive(false);
            }

            if (courseLine != null)
            {
                courseLine.gameObject.SetActive(false);
            }

            if (destinationRing != null)
            {
                destinationRing.gameObject.SetActive(false);
            }

            combatPresenter?.Reset();
            visibilityDirty = true;
        }

        private void OnDestroy()
        {
            UnbindInterestCallbacks();
            if (visibilityPositions.IsCreated)
            {
                visibilityPositions.Dispose();
            }

            if (visibilitySquaredDistances.IsCreated)
            {
                visibilitySquaredDistances.Dispose();
            }
        }

        private static Vector3 ToWorld(float x, float y, float height) => new(x, height, y);
    }
}
