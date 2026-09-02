using System;
using System.Collections;
using System.Linq;
using SpacetimeDB;
using SpacetimeDB.Types;
using UnityEngine;
using Unity.Profiling;

namespace Sea.Client
{
    public sealed partial class SeaConnectionController : MonoBehaviour
    {
        private static readonly ProfilerMarker NetworkingMarker =
            new("Sea.Networking.FrameTick");

        [SerializeField] private string serverUrl = "http://127.0.0.1:3000";
        [SerializeField] private string databaseName = "sea-local";
        [SerializeField] private bool connectOnStart = true;

        private SeaAuthTokenStore authTokens;
        private Coroutine reconnectCoroutine;
        private bool manualDisconnect;
        private bool connectAttemptInFlight;
        private bool attemptedWithToken;
        private int transientFailureCount;
        private ulong subscribedPlayerEntityId;
        private SubscriptionHandle initialSubscription;
        private SubscriptionHandle playerSubscription;
        private ulong nextCommandId = 1;
        private ulong latestCommandId;
        private string latestCommandDescription = string.Empty;

        public DbConnection Connection { get; private set; }
        public Identity LocalIdentity { get; private set; }
        public bool HasIdentity { get; private set; }
        public bool IsSubscribed { get; private set; }
        public string Status { get; private set; } = "Not connected";
        public string ServerUrl => serverUrl;
        public string DatabaseName => databaseName;
        public string CommandStatus { get; private set; } = string.Empty;

        public ulong IssueCommand(ShipCommand command, string description)
        {
            if (Connection == null || !IsSubscribed)
            {
                return 0;
            }

            var commandId = nextCommandId++;
            latestCommandId = commandId;
            latestCommandDescription = description;
            CommandStatus = $"Pending • {description}";
            Connection.Reducers.IssueShipCommand(new CommandEnvelope(commandId, command));
            return commandId;
        }

        private void Awake()
        {
            var arguments = Environment.GetCommandLineArgs();
            databaseName = SeaClientOptions.DatabaseName(
                arguments,
                databaseName);
            var profile = SeaClientOptions.Profile(arguments, "captain-1");
            authTokens = new SeaAuthTokenStore(SeaClientOptions.IdentityTokenKey(profile));
        }

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
                using (NetworkingMarker.Auto())
                {
                    Connection.FrameTick();
                }

                ApplyPendingSpatialInterest(Time.realtimeSinceStartupAsDouble);
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
                .WithDatabaseName(databaseName)
                .WithConfirmedReads(false);

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
            ResetSubscriptions();
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
            connection.Db.PlayerOwnership.OnInsert += HandleOwnershipInserted;
            connection.Db.PlayerOwnership.OnUpdate += HandleOwnershipUpdated;
            connection.Db.Ship.OnInsert += HandleShipInserted;
            connection.Db.Ship.OnUpdate += HandleShipUpdated;
            connection.Db.Ship.OnDelete += HandleShipDeleted;
            connection.Db.WorldObject.OnDelete += HandleWorldObjectDeleted;
            connection.Db.Volley.OnInsert += HandleVolleyInserted;
            connection.Db.Volley.OnUpdate += HandleVolleyUpdated;
            connection.Db.Volley.OnDelete += HandleVolleyDeleted;
            connection.Db.PlayerCommandState.OnInsert += HandleCommandStateInserted;
            connection.Db.PlayerCommandState.OnUpdate += HandleCommandStateUpdated;
            connection.Db.CommandResultEvent.OnInsert += HandleCommandResult;
            connection.Db.EncounterRewardEvent.OnInsert += HandleEncounterReward;
            RegisterClientStateCallbacks(connection);

            initialSubscription = connection.SubscriptionBuilder()
                .OnApplied(HandleInitialSubscriptionApplied)
                .OnError(HandleSubscriptionError)
                .Subscribe(SeaSubscriptionPlan.Initial(ToIdentitySqlLiteral(identity)).ToArray());
        }

        private void HandleInitialSubscriptionApplied(SubscriptionEventContext context)
        {
            var ownership = context.Db.PlayerOwnership.Owner.Find(LocalIdentity);
            if (ownership != null)
            {
                SubscribePlayerScope(Connection, ownership.ShipEntityId);
            }
            else
            {
                context.Reducers.LoadPlayer();
            }
        }

        private void HandleOwnershipInserted(EventContext context, PlayerOwnership ownership)
        {
            if (ownership.Owner == LocalIdentity)
            {
                SubscribePlayerScope(Connection, ownership.ShipEntityId);
                NotifyHudStateChanged();
            }
        }

        private void HandleOwnershipUpdated(
            EventContext context,
            PlayerOwnership _oldOwnership,
            PlayerOwnership ownership)
        {
            if (ownership.Owner == LocalIdentity)
            {
                SubscribePlayerScope(Connection, ownership.ShipEntityId);
                NotifyHudStateChanged();
            }
        }

        private void SubscribePlayerScope(DbConnection connection, ulong shipEntityId)
        {
            if (shipEntityId == 0 || subscribedPlayerEntityId == shipEntityId)
            {
                return;
            }

            subscribedPlayerEntityId = shipEntityId;
            playerSubscription = connection.SubscriptionBuilder()
                .OnApplied(context => HandlePlayerSubscriptionApplied(context, shipEntityId))
                .OnError(HandleSubscriptionError)
                .Subscribe(SeaSubscriptionPlan.Player(shipEntityId).ToArray());
        }

        private void HandlePlayerSubscriptionApplied(
            SubscriptionEventContext context,
            ulong shipEntityId)
        {
            var ship = context.Db.Ship.EntityId.Find(shipEntityId);
            if (ship == null)
            {
                Status = "Player ship subscription returned no ship.";
                return;
            }

            SubscribeSpatialScope(Connection, ship.ChunkX, ship.ChunkY);
        }

        private void HandleShipInserted(EventContext context, Ship ship)
        {
            RefreshSpatialScope(Connection, ship);
            NotifyShipChanged(ship);
        }

        private void HandleShipUpdated(EventContext context, Ship _oldShip, Ship ship)
        {
            RefreshSpatialScope(Connection, ship);
            NotifyShipChanged(ship);
        }

        private void HandleCommandStateInserted(EventContext context, PlayerCommandState state) =>
            SynchronizeCommandSequence(state);

        private void HandleCommandStateUpdated(
            EventContext context,
            PlayerCommandState _oldState,
            PlayerCommandState state) => SynchronizeCommandSequence(state);

        private void SynchronizeCommandSequence(PlayerCommandState state)
        {
            if (state.Owner == LocalIdentity && state.LastProcessedCommandId >= nextCommandId)
            {
                nextCommandId = state.LastProcessedCommandId + 1;
            }
        }

        private void HandleCommandResult(EventContext context, CommandResultEvent result)
        {
            if (result.Owner != LocalIdentity || result.CommandId < latestCommandId)
            {
                return;
            }

            latestCommandId = result.CommandId;
            var description = string.IsNullOrEmpty(latestCommandDescription)
                ? $"Command {result.CommandId}"
                : latestCommandDescription;
            CommandStatus = result.Accepted
                ? $"Accepted • {description}"
                : $"Rejected • {description} • {SeaCommandResultText.Rejection(result.RejectionCode)}";
            NotifyHudStateChanged();
        }

        private void HandleEncounterReward(EventContext context, EncounterRewardEvent reward)
        {
            if (reward.Owner != LocalIdentity)
            {
                return;
            }

            CommandStatus = $"Shared reward • +{reward.Gold} gold • +{reward.Experience} XP";
            NotifyHudStateChanged();
        }

        private static string ToIdentitySqlLiteral(Identity identity)
        {
            var value = identity.ToString();
            return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? value
                : "0x" + value;
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
            ResetSubscriptions();
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
            ResetSubscriptions();
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

        private void ResetSubscriptions()
        {
            NotifyPresentationReset();
            initialSubscription = null;
            playerSubscription = null;
            subscribedPlayerEntityId = 0;
            ResetInterestSubscriptions();
            latestCommandId = 0;
            latestCommandDescription = string.Empty;
            CommandStatus = string.Empty;
        }
    }
}
