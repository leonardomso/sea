using System;
using SpacetimeDB.Types;
using UnityEngine;

namespace Sea.Client
{
    public sealed class SeaRuntimeValidationProbe : MonoBehaviour
    {
        private SeaConnectionController connection;
        private bool enabledForThisRun;
        private bool moveRequested;
        private Vector2 start;
        private Vector2 destination;

        private void Awake()
        {
            enabledForThisRun = Array.Exists(
                Environment.GetCommandLineArgs(),
                argument => argument == "-seaRuntimeMoveTest");
            connection = FindFirstObjectByType<SeaConnectionController>();
        }

        private void Update()
        {
            if (!enabledForThisRun || connection?.Connection == null || !connection.IsSubscribed)
            {
                return;
            }

            foreach (var ship in connection.Connection.Db.PlayerShip.Iter())
            {
                if (ship.Owner != connection.LocalIdentity)
                {
                    continue;
                }

                ObserveShip(ship);
                return;
            }
        }

        private void ObserveShip(PlayerShip ship)
        {
            var position = new Vector2(ship.PositionX, ship.PositionY);
            if (!moveRequested)
            {
                start = position;
                destination = new Vector2(Mathf.Min(position.x + 20f, 90f), position.y);
                connection.Connection.Reducers.MoveTo(destination.x, destination.y);
                moveRequested = true;
                return;
            }

            var travelled = Vector2.Distance(start, position);
            var remaining = Vector2.Distance(position, destination);
            if (ship.IsMoving && travelled > 0.1f && remaining > 0.1f)
            {
                enabledForThisRun = false;
                Debug.Log("Sea runtime observed progressive sailing.", this);
            }
        }
    }
}
