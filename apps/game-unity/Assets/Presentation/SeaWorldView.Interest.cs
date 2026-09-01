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
        }

        private void UnbindInterestCallbacks()
        {
            if (interestConnection == null)
            {
                return;
            }

            interestConnection.ShipLeftInterest -= RemoveShipPresentation;
            interestConnection.WorldObjectLeftInterest -= RemoveWorldObjectPresentation;
            interestConnection = null;
        }

        private void RemoveShipPresentation(ulong entityId)
        {
            targets.Remove(entityId);
            shipFeedback.Remove(entityId);
            if (entityId == playerEntityId)
            {
                if (playerObject != null)
                {
                    Destroy(playerObject);
                }

                playerObject = null;
                playerFeedback = null;
                playerEntityId = 0;
                return;
            }

            if (entities.Remove(entityId, out var entityObject) && entityObject != null)
            {
                Destroy(entityObject);
            }
        }

        private void RemoveWorldObjectPresentation(ulong entityId)
        {
            if (mapGeometry.Remove(entityId, out var geometry) && geometry != null)
            {
                Destroy(geometry);
            }
        }

        private void OnDestroy() => UnbindInterestCallbacks();

        private static Vector3 ToWorld(float x, float y, float height) => new(x, height, y);

        private readonly struct PresentationTarget
        {
            public PresentationTarget(Vector3 position, float headingDegrees, float speed)
            {
                Position = position;
                HeadingDegrees = headingDegrees;
                Speed = speed;
            }

            public Vector3 Position { get; }
            public float HeadingDegrees { get; }
            public float Speed { get; }
        }
    }
}
