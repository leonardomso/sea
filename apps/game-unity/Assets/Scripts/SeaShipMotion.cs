using System;
using UnityEngine;

namespace Sea.Client
{
    public static class SeaShipMotion
    {
        public static void Step(
            Transform ship,
            Vector3 target,
            float deltaTime,
            float movementSpeed,
            float turnSpeedDegrees,
            float modelYawOffset)
        {
            if (ship == null)
            {
                throw new ArgumentNullException(nameof(ship));
            }

            var direction = target - ship.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up)
                    * Quaternion.Euler(0f, modelYawOffset, 0f);
                ship.rotation = Quaternion.RotateTowards(
                    ship.rotation,
                    targetRotation,
                    turnSpeedDegrees * deltaTime);
            }

            ship.position = Vector3.MoveTowards(
                ship.position,
                target,
                movementSpeed * deltaTime);
        }
    }
}
