using System;
using UnityEngine;

namespace Sea.Client
{
    public static class SeaShipMotion
    {
        public static void Step(
            Transform ship,
            Vector3 target,
            float targetHeadingDegrees,
            float deltaTime,
            float movementSpeed,
            float turnSpeedDegrees)
        {
            if (ship == null)
            {
                throw new ArgumentNullException(nameof(ship));
            }

            var targetRotation = Quaternion.Euler(0f, targetHeadingDegrees, 0f);
            ship.rotation = Quaternion.RotateTowards(
                ship.rotation,
                targetRotation,
                turnSpeedDegrees * deltaTime);

            ship.position = Vector3.MoveTowards(
                ship.position,
                target,
                movementSpeed * deltaTime);
        }
    }
}
