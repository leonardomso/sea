using System.Diagnostics;
using SpacetimeDB;
using SpacetimeDB.Types;

namespace Sea.Server.IntegrationTests;

internal sealed partial class IntegrationClient : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(45);

    private readonly DbConnection connection;
    private readonly Identity identity;
    private readonly List<CommandResultEvent> commandResults = [];
    private readonly List<SubscriptionHandle> subscriptions = [];
    private ulong nextCommandId = 1;
    private bool subscribed;
    private bool playerSubscribed;
    private bool spatialSubscribed;
    private bool dockSubscribed;
    private bool npcWorldSubscribed;
    private Exception? failure;
    private bool disposed;

    private IntegrationClient(DbConnection connection, Identity identity, string token)
    {
        this.connection = connection;
        this.identity = identity;
        Token = token;
        connection.OnUnhandledReducerError += (_, error) => UnhandledReducerError = error;
        connection.Db.CommandResultEvent.OnInsert += (_, result) =>
        {
            if (result.Owner == identity)
            {
                commandResults.Add(result);
            }
        };
    }

    public Exception? UnhandledReducerError { get; private set; }
    public string Token { get; }

    public static IntegrationClient Connect(string? token = null)
    {
        var database = RequiredEnvironment("SEA_TEST_DATABASE");
        var server = Environment.GetEnvironmentVariable("SEA_TEST_SERVER")
            ?? "http://host.docker.internal:43000";
        DbConnection? connectedClient = null;
        Identity connectedIdentityValue = default;
        var connectedTokenValue = string.Empty;
        Exception? connectionError = null;

        var builder = DbConnection.Builder()
            .OnConnect((value, connectedIdentity, connectedToken) =>
            {
                connectedClient = value;
                connectedIdentityValue = connectedIdentity;
                connectedTokenValue = connectedToken;
            })
            .OnConnectError(error => connectionError = error)
            .WithUri(server)
            .WithDatabaseName(database)
            .WithConfirmedReads(false);
        if (!string.IsNullOrWhiteSpace(token))
        {
            builder.WithToken(token);
        }

        var pendingConnection = builder.Build();

        PumpUntil(pendingConnection, () => connectedClient is not null || connectionError is not null);
        if (connectionError is not null)
        {
            pendingConnection.Disconnect();
            throw new InvalidOperationException("Could not connect to the test database.", connectionError);
        }

        return new IntegrationClient(connectedClient!, connectedIdentityValue, connectedTokenValue);
    }

    public void LoadPlayer()
    {
        var ownerLiteral = IdentitySqlLiteral(identity);
        subscriptions.Add(connection.SubscriptionBuilder()
            .OnApplied(_ => subscribed = true)
            .OnError((_, error) => failure = error)
            .Subscribe([
                $"SELECT * FROM player_ownership WHERE owner = {ownerLiteral}",
                $"SELECT * FROM player_progression WHERE owner = {ownerLiteral}",
                $"SELECT * FROM player_command_state WHERE owner = {ownerLiteral}",
                $"SELECT * FROM player_clock WHERE owner = {ownerLiteral}",
                $"SELECT * FROM command_result_event WHERE owner = {ownerLiteral}",
                $"SELECT * FROM encounter_reward WHERE owner = {ownerLiteral}",
                $"SELECT * FROM encounter_reward_event WHERE owner = {ownerLiteral}",
            ]));
        PumpUntil(connection, () => subscribed || failure is not null);
        ThrowIfFailed();

        connection.Reducers.LoadPlayer();
        PumpUntil(connection, () =>
            connection.Db.PlayerOwnership.Owner.Find(identity) is not null || failure is not null);
        ThrowIfFailed();

        var ownership = connection.Db.PlayerOwnership.Owner.Find(identity)!;
        subscriptions.Add(connection.SubscriptionBuilder()
            .OnApplied(_ => playerSubscribed = true)
            .OnError((_, error) => failure = error)
            .Subscribe([
                // Port Lowell is a world object like any other, and now that its water has
                // rules every client has to know where the mouth is.
                "SELECT * FROM world_object WHERE kind = 'harbor'",
                $"SELECT * FROM ship WHERE entity_id = {ownership.ShipEntityId}",
                $"SELECT * FROM inventory WHERE ship_entity_id = {ownership.ShipEntityId}",
                $"SELECT * FROM effect WHERE ship_entity_id = {ownership.ShipEntityId}",
                $"SELECT * FROM cooldown WHERE ship_entity_id = {ownership.ShipEntityId}",
                $"SELECT * FROM ship_channel WHERE ship_entity_id = {ownership.ShipEntityId}",
                $"SELECT * FROM ship_movement WHERE entity_id = {ownership.ShipEntityId}",
                $"SELECT * FROM respawn_work WHERE ship_entity_id = {ownership.ShipEntityId}",
            ]));
        PumpUntil(connection, () => playerSubscribed || failure is not null);
        ThrowIfFailed();
    }

    public void SubscribeSpatial(int chunkX, int chunkY, int radius)
    {
        var minimumX = chunkX - radius;
        var maximumX = chunkX + radius;
        var minimumY = chunkY - radius;
        var maximumY = chunkY + radius;
        var bounds = $"chunk_x >= {minimumX} AND chunk_x <= {maximumX} " +
            $"AND chunk_y >= {minimumY} AND chunk_y <= {maximumY}";
        spatialSubscribed = false;
        subscriptions.Add(connection.SubscriptionBuilder()
            .OnApplied(_ => spatialSubscribed = true)
            .OnError((_, error) => failure = error)
            .Subscribe([
                $"SELECT * FROM ship WHERE is_active = true AND {bounds}",
                $"SELECT * FROM world_object WHERE is_active = true AND {bounds}",
            ]));
    }

    /// <summary>
    /// Subscribes to the dock tables and the seeded content projections. The hull and stat
    /// queries are deliberately unfiltered so the rows that arrive are exactly what the
    /// server's owner visibility filters let through.
    /// </summary>
    public void SubscribeDock()
    {
        dockSubscribed = false;
        subscriptions.Add(connection.SubscriptionBuilder()
            .OnApplied(_ => dockSubscribed = true)
            .OnError((_, error) => failure = error)
            .Subscribe([
                "SELECT * FROM hull",
                "SELECT * FROM ship_stats",
                "SELECT * FROM hull_def",
                "SELECT * FROM cannon_def",
                "SELECT * FROM ammo_def",
                "SELECT * FROM stat_caps",
            ]));
        PumpUntil(connection, () => dockSubscribed || failure is not null);
        ThrowIfFailed();
    }

    /// <summary>
    /// Calls load_player again on an already loaded identity and waits for the reducer to come
    /// back. The reducer callback is the signal rather than the player clock: load_player anchors
    /// that clock to the live tick, so two calls inside the same tick leave it unchanged and no
    /// amount of pumping would ever move it.
    /// </summary>
    public void ReloadPlayer()
    {
        var reloaded = false;
        void Reloaded(ReducerEventContext context) => reloaded = true;

        connection.Reducers.OnLoadPlayer += Reloaded;
        try
        {
            connection.Reducers.LoadPlayer();
            PumpUntil(connection, () => reloaded || failure is not null);
        }
        finally
        {
            connection.Reducers.OnLoadPlayer -= Reloaded;
        }

        ThrowIfFailed();
    }

    public Hull OwnedHull() => connection.Db.Hull.Iter().Single(hull => hull.Owner == identity);

    public ShipStats OwnedShipStats() =>
        connection.Db.ShipStats.Iter().Single(stats => stats.Owner == identity);

    public Hull[] VisibleHulls() => connection.Db.Hull.Iter().ToArray();

    public ShipStats[] VisibleShipStats() => connection.Db.ShipStats.Iter().ToArray();

    public PlayerProgression OwnedProgression() =>
        connection.Db.PlayerProgression.Owner.Find(identity)
        ?? throw new InvalidOperationException("The integration identity has no progression row.");

    public HullDef[] HullDefs() => connection.Db.HullDef.Iter().ToArray();

    public CannonDef[] CannonDefs() => connection.Db.CannonDef.Iter().ToArray();

    public AmmoDef[] AmmoDefs() => connection.Db.AmmoDef.Iter().ToArray();

    public StatCaps SeededStatCaps() => connection.Db.StatCaps.Iter().Single();

    public void SubscribeNpcWorld()
    {
        subscriptions.Add(connection.SubscriptionBuilder()
            .OnApplied(_ => npcWorldSubscribed = true)
            .OnError((_, error) => failure = error)
            .Subscribe([
                "SELECT * FROM ship WHERE faction_code = 2",
                "SELECT * FROM ship_movement WHERE faction_code = 2",
                "SELECT * FROM npc_ai",
            ]));
        PumpUntil(connection, () => npcWorldSubscribed || failure is not null);
        ThrowIfFailed();
    }

    /// <summary>
    /// Live NPC kinematics. The fat <c>ship</c> row is only republished when a ship changes chunk
    /// or stops, so a moving ship's position has to be read from <c>ship_movement</c>, which the
    /// tick publishes every frame. That is the same table the game client renders from.
    /// </summary>
    public Dictionary<ulong, (float X, float Y)> NpcPositions() =>
        connection.Db.ShipMovement.Iter()
            .Where(movement => movement.FactionCode == 2)
            .ToDictionary(
                movement => movement.EntityId,
                movement => (movement.PositionX, movement.PositionY));

    /// <summary>Where a hostile actually is this tick, rather than where its fat row last said.</summary>
    public (float X, float Y) NpcPosition(ulong entityId)
    {
        var movement = connection.Db.ShipMovement.EntityId.Find(entityId)
            ?? throw new InvalidOperationException($"NPC {entityId} has no movement row.");
        return (movement.PositionX, movement.PositionY);
    }

    public int NpcCount(byte archetypeCode) => connection.Db.Ship.Iter()
        .Count(ship => ship.FactionCode == 2 && ship.ArchetypeCode == archetypeCode);

    public int NpcAiCount() => connection.Db.NpcAi.Iter().Count();

    /// <summary>The decision row behind a hostile: who it follows and whether it has signalled.</summary>
    public NpcAi NpcBrain(ulong entityId) => connection.Db.NpcAi.ShipEntityId.Find(entityId)
        ?? throw new InvalidOperationException($"NPC {entityId} has no decision row.");

    /// <summary>The hulls moored to one captain, waiting on her signal.</summary>
    public NpcAi[] EscortsOf(ulong leaderEntityId) => connection.Db.NpcAi.Iter()
        .Where(ai => ai.LeaderEntityId == leaderEntityId)
        .OrderBy(ai => ai.ShipEntityId)
        .ToArray();

    public Ship OwnedShip()
    {
        var ownership = connection.Db.PlayerOwnership.Owner.Find(identity)
            ?? throw new InvalidOperationException("The integration identity has no ownership row.");
        return connection.Db.Ship.EntityId.Find(ownership.ShipEntityId)
            ?? throw new InvalidOperationException("The integration identity has no ship row.");
    }

    public PlayerClock OwnedClock() => connection.Db.PlayerClock.Owner.Find(identity)
        ?? throw new InvalidOperationException("The integration identity has no clock row.");

    public Ship Npc(ulong entityId) => connection.Db.Ship.EntityId.Find(entityId)
        ?? throw new InvalidOperationException($"NPC {entityId} is not subscribed.");

    public EncounterReward[] EncounterRewards() => connection.Db.EncounterReward.Iter().ToArray();

    /// <summary>
    /// What the wrecks have left floating. Loot is the one reward a captain has to sail for, so
    /// a test about collecting it needs the rows as well as the purse.
    /// </summary>
    public void SubscribeLoot()
    {
        var applied = false;
        subscriptions.Add(connection.SubscriptionBuilder()
            .OnApplied(_ => applied = true)
            .OnError((_, error) => failure = error)
            .Subscribe(["SELECT * FROM loot"]));
        PumpUntil(connection, () => applied || failure is not null);
        ThrowIfFailed();
    }

    public Loot[] ActiveLoot() => connection.Db.Loot.Iter()
        .Where(loot => loot.IsActive)
        .ToArray();

    /// <summary>
    /// Subscribes to the shots this ship fires. The volley row is the only public record that a
    /// fire command actually reached the world, so a combat test has to read it rather than
    /// infer the shot from the damage it caused.
    /// </summary>
    public void SubscribeVolleys()
    {
        var ownership = connection.Db.PlayerOwnership.Owner.Find(identity)
            ?? throw new InvalidOperationException("The integration identity has no ownership row.");
        var volleysSubscribed = false;
        subscriptions.Add(connection.SubscriptionBuilder()
            .OnApplied(_ => volleysSubscribed = true)
            .OnError((_, error) => failure = error)
            .Subscribe([
                $"SELECT * FROM volley WHERE source_entity_id = {ownership.ShipEntityId}",
            ]));
        PumpUntil(connection, () => volleysSubscribed || failure is not null);
        ThrowIfFailed();
    }

    public Volley[] Volleys() => connection.Db.Volley.Iter().ToArray();

    public Effect[] Effects() => connection.Db.Effect.Iter().ToArray();

    public CommandResultEvent IssueFire(ulong commandId) => Issue(
        commandId,
        new ShipCommand.Fire(new FireCommand()));

    public CommandResultEvent IssueSetCourse(ulong commandId, float x, float y) => Issue(
        commandId,
        new ShipCommand.SetCourse(new SetCourseCommand(x, y)));

    public CommandResultEvent IssueSelectTarget(ulong commandId, ulong entityId) => Issue(
        commandId,
        new ShipCommand.SelectTarget(new SelectTargetCommand(entityId)));

    public CommandResultEvent IssueBoarding(ulong commandId) => Issue(
        commandId,
        new ShipCommand.StartBoarding(new StartBoardingCommand()));

    public CommandResultEvent IssueAbility(ulong commandId, string abilityId) => Issue(
        commandId,
        new ShipCommand.ActivateAbility(new ActivateAbilityCommand(abilityId)));

    public CommandResultEvent SetCourse(float x, float y) => Issue(
        nextCommandId++,
        new ShipCommand.SetCourse(new SetCourseCommand(x, y)));

    public CommandResultEvent StopCourse() => Issue(
        nextCommandId++,
        new ShipCommand.StopCourse(new StopCourseCommand()));

    public CommandResultEvent SelectTarget(ulong entityId) => Issue(
        nextCommandId++,
        new ShipCommand.SelectTarget(new SelectTargetCommand(entityId)));

    public CommandResultEvent SetAmmo(string ammunitionId) => Issue(
        nextCommandId++,
        new ShipCommand.SetAmmo(new SetAmmoCommand(ammunitionId)));

    public CommandResultEvent Fire() => Issue(
        nextCommandId++,
        new ShipCommand.Fire(new FireCommand()));

    public CommandResultEvent StartRepair() => Issue(
        nextCommandId++,
        new ShipCommand.StartRepair(new StartRepairCommand()));

    public CommandResultEvent UseRepairKit() => Issue(
        nextCommandId++,
        new ShipCommand.UseRepairKit(new UseRepairKitCommand()));

    public CommandResultEvent CancelChannel() => Issue(
        nextCommandId++,
        new ShipCommand.CancelChannel(new CancelChannelCommand()));

    public CommandResultEvent ChooseRespawn(byte optionCode) => Issue(
        nextCommandId++,
        new ShipCommand.ChooseRespawn(new ChooseRespawnCommand(optionCode)));

    /// <summary>
    /// Live kinematics for the player's own hull. The fat ship row is only republished on a chunk
    /// change or a stop, so a test that watches a ship hold station has to read the row the tick
    /// publishes every frame.
    /// </summary>
    public ShipMovement OwnedMovement() =>
        connection.Db.ShipMovement.EntityId.Find(OwnedShip().EntityId)
        ?? throw new InvalidOperationException("The integration identity has no movement row.");

    public RespawnWork? OwnedRespawnWork() =>
        connection.Db.RespawnWork.ShipEntityId.Find(OwnedShip().EntityId);

    /// <summary>The one channel a ship may hold, or null while it holds none.</summary>
    public ShipChannel? ActiveChannel() =>
        connection.Db.ShipChannel.ShipEntityId.Find(OwnedShip().EntityId);

    public Cooldown? OwnedCooldown(string cooldownType) => connection.Db.Cooldown.Iter()
        .FirstOrDefault(cooldown =>
            cooldown.ShipEntityId == OwnedShip().EntityId &&
            string.Equals(cooldown.CooldownType, cooldownType, StringComparison.Ordinal));

    public uint OwnedInventoryQuantity(string itemId) => connection.Db.Inventory.Iter()
        .Where(item =>
            item.ShipEntityId == OwnedShip().EntityId &&
            string.Equals(item.ItemId, itemId, StringComparison.Ordinal))
        .Select(item => item.Quantity)
        .FirstOrDefault();

    public ulong[] VisiblePlayerShipIds() => connection.Db.Ship.Iter()
        .Where(ship => ship.FactionCode == 1)
        .Select(ship => ship.EntityId)
        .ToArray();

    public bool HasOnlyBoundedSpatialRows(int minimumChunk, int maximumChunk)
    {
        if (!spatialSubscribed)
        {
            return false;
        }

        return connection.Db.Ship.Iter().All(ship =>
                   ship.ChunkX >= minimumChunk && ship.ChunkX <= maximumChunk &&
                   ship.ChunkY >= minimumChunk && ship.ChunkY <= maximumChunk) &&
               connection.Db.WorldObject.Iter().All(worldObject =>
                   worldObject.ChunkX >= minimumChunk && worldObject.ChunkX <= maximumChunk &&
                   worldObject.ChunkY >= minimumChunk && worldObject.ChunkY <= maximumChunk);
    }

    public void PumpOnce() => connection.FrameTick();

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        connection.Disconnect();
    }

    private CommandResultEvent Issue(ulong commandId, ShipCommand command)
    {
        var resultCount = commandResults.Count;
        connection.Reducers.IssueShipCommand(new CommandEnvelope(commandId, command));
        PumpUntil(connection, () => commandResults.Count > resultCount || failure is not null);
        ThrowIfFailed();
        return commandResults[^1];
    }

    private void ThrowIfFailed()
    {
        if (failure is not null)
        {
            throw new InvalidOperationException("The integration subscription failed.", failure);
        }
    }

    private static string RequiredEnvironment(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Environment variable {name} is required.");

    private static float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        return deltaX * deltaX + deltaY * deltaY;
    }

    private static string IdentitySqlLiteral(Identity value)
    {
        var text = value.ToString();
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text : "0x" + text;
    }

    private static void PumpUntil(DbConnection connection, Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            connection.FrameTick();
            if (stopwatch.Elapsed > Timeout)
            {
                throw new TimeoutException("SpacetimeDB integration operation timed out.");
            }

            Thread.Sleep(5);
        }

        connection.FrameTick();
    }
}
