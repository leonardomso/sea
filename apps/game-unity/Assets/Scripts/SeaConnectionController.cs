using System;
using System.Collections;
using SpacetimeDB;
using SpacetimeDB.Types;
using UnityEngine;

namespace Sea.Client
{
    public sealed class SeaConnectionController : MonoBehaviour
    {
        [SerializeField] private string serverUrl = "http://127.0.0.1:3000";
        [SerializeField] private string databaseName = "sea-local";
        [SerializeField] private bool connectOnStart = true;
        [SerializeField] private float reconnectDelaySeconds = 2f;

        private Coroutine reconnectCoroutine;
        private bool manualDisconnect;

        public DbConnection Connection { get; private set; }
        public Identity LocalIdentity { get; private set; }
        public bool HasIdentity { get; private set; }
        public bool IsSubscribed { get; private set; }
        public string Status { get; private set; } = "Not connected";
        public string ServerUrl => serverUrl;
        public string DatabaseName => databaseName;

        private void Start()
        {
            if (connectOnStart)
            {
                Connect();
            }
        }

        private void Update()
        {
            if (Connection != null && Connection.IsActive)
            {
                Connection.FrameTick();
            }
        }

        public void Connect()
        {
            manualDisconnect = false;
            IsSubscribed = false;

            if (Connection != null && Connection.IsActive)
            {
                Status = "Connected";
                return;
            }

            Status = "Connecting...";

            var builder = DbConnection.Builder()
                .OnConnect(HandleConnected)
                .OnConnectError(HandleConnectionError)
                .OnDisconnect(HandleDisconnected)
                .WithUri(serverUrl)
                .WithDatabaseName(databaseName);

            var token = AuthToken.Token;
            if (!string.IsNullOrWhiteSpace(token))
            {
                builder.WithToken(token);
            }

            try
            {
                Connection = builder.Build();
            }
            catch (Exception exception)
            {
                HandleConnectionError(exception);
            }
        }

        public void Disconnect()
        {
            manualDisconnect = true;
            if (reconnectCoroutine != null)
            {
                StopCoroutine(reconnectCoroutine);
                reconnectCoroutine = null;
            }

            if (Connection != null)
            {
                Connection.Disconnect();
                Connection = null;
            }

            IsSubscribed = false;
            Status = "Disconnected";
        }

        private void HandleConnected(DbConnection connection, Identity identity, string token)
        {
            AuthToken.SaveToken(token);
            PlayerPrefs.Save();
            LocalIdentity = identity;
            HasIdentity = true;
            Status = "Connected; subscribing...";

            connection.OnUnhandledReducerError += HandleUnhandledReducerError;
            connection.SubscriptionBuilder()
                .OnApplied(HandleSubscriptionApplied)
                .OnError(HandleSubscriptionError)
                .SubscribeToAllTables();

            connection.Reducers.LoadPlayer();
        }

        private void HandleSubscriptionApplied(SubscriptionEventContext context)
        {
            IsSubscribed = true;
            Status = "Ready";
        }

        private void HandleSubscriptionError(ErrorContext context, Exception exception)
        {
            IsSubscribed = false;
            Status = "Subscription error: " + exception.Message;
            Debug.LogException(exception, this);
        }

        private void HandleConnectionError(Exception exception)
        {
            Status = "Connection error: " + exception.Message;
            Debug.LogException(exception, this);
            ScheduleReconnect();
        }

        private void HandleDisconnected(DbConnection connection, Exception exception)
        {
            if (Connection == connection)
            {
                Connection = null;
            }

            IsSubscribed = false;
            Status = exception == null ? "Disconnected" : "Disconnected: " + exception.Message;
            if (exception != null)
            {
                Debug.LogException(exception, this);
            }

            ScheduleReconnect();
        }

        private void HandleUnhandledReducerError(ReducerEventContext context, Exception exception)
        {
            Status = "Reducer error: " + exception.Message;
            Debug.LogException(exception, this);
        }

        private void ScheduleReconnect()
        {
            if (!manualDisconnect && reconnectCoroutine == null && isActiveAndEnabled)
            {
                reconnectCoroutine = StartCoroutine(ReconnectAfterDelay());
            }
        }

        private IEnumerator ReconnectAfterDelay()
        {
            yield return new WaitForSeconds(reconnectDelaySeconds);
            reconnectCoroutine = null;
            if (!manualDisconnect)
            {
                Connect();
            }
        }

        private void OnDestroy()
        {
            Disconnect();
        }
    }
}
