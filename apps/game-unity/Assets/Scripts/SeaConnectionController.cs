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

        private readonly SeaAuthTokenStore authTokens = new();
        private Coroutine reconnectCoroutine;
        private bool manualDisconnect;
        private bool connectAttemptInFlight;
        private bool attemptedWithToken;
        private int transientFailureCount;

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
            if (Connection != null)
            {
                Connection.FrameTick();
            }
        }

        public void Connect()
        {
            manualDisconnect = false;
            IsSubscribed = false;

            if (connectAttemptInFlight)
            {
                return;
            }

            if (Connection != null && Connection.IsActive)
            {
                Status = "Connected";
                return;
            }

            connectAttemptInFlight = true;
            Status = "Connecting...";

            var builder = DbConnection.Builder()
                .OnConnect(HandleConnected)
                .OnConnectError(HandleConnectionError)
                .OnDisconnect(HandleDisconnected)
                .WithUri(serverUrl)
                .WithDatabaseName(databaseName);

            var token = authTokens.Token;
            attemptedWithToken = !string.IsNullOrWhiteSpace(token);
            if (attemptedWithToken)
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

            connectAttemptInFlight = false;
            IsSubscribed = false;
            Status = "Disconnected";
        }

        private void HandleConnected(DbConnection connection, Identity identity, string token)
        {
            connectAttemptInFlight = false;
            transientFailureCount = 0;
            authTokens.Save(token);
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
            Debug.Log("Sea client ready.", this);
        }

        private void HandleSubscriptionError(ErrorContext context, Exception exception)
        {
            IsSubscribed = false;
            Status = "Subscription error: " + exception.Message;
            Debug.LogException(exception, this);
        }

        private void HandleConnectionError(Exception exception)
        {
            connectAttemptInFlight = false;
            var failedConnection = Connection;
            Connection = null;
            if (failedConnection != null)
            {
                failedConnection.Disconnect();
            }

            IsSubscribed = false;
            HasIdentity = false;
            RecoverFromFailure(exception);
        }

        private void HandleDisconnected(DbConnection connection, Exception exception)
        {
            connectAttemptInFlight = false;
            if (Connection != connection)
            {
                return;
            }

            Connection = null;
            IsSubscribed = false;
            HasIdentity = false;
            Status = exception == null ? "Disconnected" : "Disconnected: " + exception.Message;
            if (!manualDisconnect)
            {
                RecoverFromFailure(exception ?? new Exception("The server closed the connection."));
            }
        }

        private void HandleUnhandledReducerError(ReducerEventContext context, Exception exception)
        {
            Status = "Reducer error: " + exception.Message;
            Debug.LogException(exception, this);
        }

        private void RecoverFromFailure(Exception exception)
        {
            var decision = SeaConnectionRecoveryPolicy.Decide(
                exception,
                attemptedWithToken,
                transientFailureCount);

            switch (decision.Action)
            {
                case SeaConnectionRecoveryAction.ClearIdentityAndRetry:
                    authTokens.Clear();
                    attemptedWithToken = false;
                    Status = "Cached identity expired; reconnecting...";
                    Debug.LogWarning("Cached identity rejected; retrying anonymously.", this);
                    ScheduleReconnect(0f);
                    break;
                case SeaConnectionRecoveryAction.RetryAfterDelay:
                    transientFailureCount++;
                    Status = $"Connection unavailable; retrying in {decision.DelaySeconds:0}s...";
                    Debug.LogWarning($"Connection failed; retrying in {decision.DelaySeconds:0}s: {exception.Message}", this);
                    ScheduleReconnect(decision.DelaySeconds);
                    break;
                case SeaConnectionRecoveryAction.Stop:
                    manualDisconnect = true;
                    Status = "Connection stopped: " + exception.Message;
                    Debug.LogError(Status, this);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ScheduleReconnect(float delaySeconds)
        {
            if (!manualDisconnect && reconnectCoroutine == null && isActiveAndEnabled)
            {
                reconnectCoroutine = StartCoroutine(ReconnectAfterDelay(delaySeconds));
            }
        }

        private IEnumerator ReconnectAfterDelay(float delaySeconds)
        {
            if (delaySeconds > 0f)
            {
                yield return new WaitForSeconds(delaySeconds);
            }
            else
            {
                yield return null;
            }

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
