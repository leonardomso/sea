using System.Diagnostics;
using SpacetimeDB.Types;
using Xunit;

namespace Sea.Server.IntegrationTests;

/// <summary>
/// End-to-end coverage of the 1c port and repair reducers against a live module: the berth a new
/// hull wakes up in, the cast-off it has to pay to leave, and the two repairs -- the channel and
/// the kit -- that mend it on cooldowns of their own.
/// </summary>
public sealed class PortIntegrationTests
{
    private const byte NothingToRepairRejection = 18;
    private const byte OnCooldownRejection = 22;
    private const byte NotSunkRejection = 24;
    private const byte HomePortRespawn = 1;
    private const byte OperationalMode = 0;
    private const byte CastingOffMode = 3;
    private const byte SkiffArchetype = 1;

    /// <summary>Inside the four squares a skiff watches, inside the range it shoots from.</summary>
    private const float AggroApproachUnits = 20f;

    /// <summary>Open water north of Port Lowell: no island, reef or shoal is within thirty units.</summary>
    private const float OpenWaterX = 0f;
    private const float OpenWaterY = 40f;

    /// <summary>
    /// Long enough for a skiff to work through a third of a sloop's hull, which is several
    /// magazines and the reloads between them rather than a handful of shots.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    [Fact]
    public void ANewHullIsBerthedInPortLowellWithBothRepairsStillUnspent()
    {
        using var client = Berthed();
        var harbor = client.Harbor();
        var ship = client.OwnedShip();

        Assert.True(ship.IsInPort);
        Assert.True(client.IsNear(harbor.PositionX, harbor.PositionY, harbor.Radius));
        Assert.Equal(OperationalMode, ship.ModeCode);
        Assert.Null(client.ActiveChannel());
        Assert.Equal(10u, client.OwnedInventoryQuantity("repair_kit"));

        // A whole hull is the one thing neither repair will spend itself on, and a ship still
        // afloat has no berth to ask for.
        Assert.Equal(NothingToRepairRejection, client.StartRepair().RejectionCode);
        Assert.Equal(NothingToRepairRejection, client.UseRepairKit().RejectionCode);
        Assert.Equal(NotSunkRejection, client.ChooseRespawn(HomePortRespawn).RejectionCode);

        Assert.Equal(10u, client.OwnedInventoryQuantity("repair_kit"));
        Assert.Null(client.OwnedRespawnWork());
        Assert.Null(client.UnhandledReducerError);
    }

    [Fact]
    public void TheFirstCourseOutOfPortIsPaidForWithACastOff()
    {
        using var client = Berthed();
        var berth = client.OwnedMovement();

        Assert.True(client.SetCourse(OpenWaterX, OpenWaterY).Accepted);
        var channel = client.ActiveChannel();
        Assert.NotNull(channel);
        Assert.Equal("cast_off", channel.ChannelType);
        Assert.Equal(CastingOffMode, client.OwnedShip().ModeCode);

        // The course is taken up but not sailed: the hull holds its berth for the whole channel.
        HoldsStationWhileChannelling(client, berth);

        Assert.Equal(OperationalMode, client.OwnedShip().ModeCode);
        PumpUntil(client, () => HasLeft(berth, client.OwnedMovement()));
        Assert.Null(client.UnhandledReducerError);
    }

    [Fact]
    public void AbandoningTheCastOffLeavesTheHullAtItsBerth()
    {
        using var client = Berthed();
        var berth = client.OwnedMovement();

        Assert.True(client.SetCourse(OpenWaterX, OpenWaterY).Accepted);
        Assert.NotNull(client.ActiveChannel());
        Assert.True(client.CancelChannel().Accepted);

        // The cast-off was the whole of the course out, so giving it up leaves the course with it.
        var cancelled = client.OwnedShip();
        Assert.Null(client.ActiveChannel());
        Assert.Equal(OperationalMode, cancelled.ModeCode);
        Assert.False(cancelled.HasCourse);
        Assert.True(cancelled.IsInPort);

        // Stopping is the other way to change one's mind, and it clears the channel just the same.
        Assert.True(client.SetCourse(OpenWaterX, OpenWaterY).Accepted);
        Assert.NotNull(client.ActiveChannel());
        Assert.True(client.StopCourse().Accepted);
        Assert.Null(client.ActiveChannel());

        PumpFor(client, TimeSpan.FromSeconds(2));
        Assert.False(HasLeft(berth, client.OwnedMovement()));
        Assert.True(client.OwnedShip().IsInPort);
        Assert.Null(client.UnhandledReducerError);
    }

    /// <summary>
    /// The two repairs on one damaged hull. The kit is a crate opened on deck and the channel is
    /// the crew off the guns, so spending one has to leave the other still there to spend.
    /// </summary>
    [Fact]
    public void TheKitAndTheChannelMendTheSameHullOnCooldownsOfTheirOwn()
    {
        using var client = DamagedAtItsBerth();
        var damaged = client.OwnedShip().Hull;

        Assert.True(client.UseRepairKit().Accepted);
        Assert.Equal(9u, client.OwnedInventoryQuantity("repair_kit"));
        Assert.NotNull(client.OwnedCooldown("repair_kit"));
        Assert.Null(client.OwnedCooldown("repair"));
        var mended = client.OwnedShip().Hull;
        Assert.True(mended > damaged, $"The kit left the hull at {mended}.");

        CompleteAChannelledRepair(client, mended);

        // Each repair now owes its own cooldown, and neither answers for the other.
        Assert.Equal(OnCooldownRejection, client.StartRepair().RejectionCode);
        Assert.Equal(OnCooldownRejection, client.UseRepairKit().RejectionCode);
        Assert.Equal(9u, client.OwnedInventoryQuantity("repair_kit"));
        Assert.Null(client.UnhandledReducerError);
    }

    private static void CompleteAChannelledRepair(IntegrationClient client, uint before)
    {
        Assert.True(client.StartRepair().Accepted);
        var channel = client.ActiveChannel();
        Assert.NotNull(channel);
        Assert.Equal("repair", channel.ChannelType);

        // Nothing is mended on the way: the hull only rises on the tick the channel is due.
        PumpUntil(client, () => client.ActiveChannel() is null);
        var healed = client.OwnedShip().Hull;
        Assert.True(healed > before, $"The channel left the hull at {healed}.");
        Assert.NotNull(client.OwnedCooldown("repair"));
    }

    /// <summary>
    /// Watches the hull sit still for the whole cast-off. The movement row is the one the tick
    /// publishes every frame, so it is what a ship holding station has to be read from.
    /// </summary>
    private static void HoldsStationWhileChannelling(IntegrationClient client, ShipMovement berth)
    {
        var stopwatch = Stopwatch.StartNew();
        while (client.ActiveChannel() is not null)
        {
            Assert.False(HasLeft(berth, client.OwnedMovement()), "The hull sailed unpaid for.");
            client.PumpOnce();
            ThrowIfTimedOut(client, stopwatch);
        }
    }

    /// <summary>
    /// A client berthed in Port Lowell with the harbour itself in view. The circle is a world
    /// object like any other, so the water it sits in has to be subscribed to before it can be
    /// read.
    /// </summary>
    private static IntegrationClient Berthed()
    {
        var client = IntegrationClient.Connect();
        try
        {
            client.LoadPlayer();
            var berth = client.OwnedShip();
            client.SubscribeSpatial(berth.ChunkX, berth.ChunkY, 1);
            PumpUntil(client, client.HasHarbor);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    /// <summary>
    /// A hull that has been shot at and has come home. The skiff is left to open the fight --
    /// it engages on sight and holds its distance -- so the damage arrives without the test
    /// having to fire back and sink the ship it is relying on. The port is where the repairs are
    /// then measured, because inside the circle nothing can hit the hull mid-assertion.
    /// </summary>
    private static IntegrationClient DamagedAtItsBerth()
    {
        var client = IntegrationClient.Connect();
        try
        {
            client.LoadPlayer();
            client.SubscribeNpcWorld();
            SailIntoGunfire(client);
            PumpUntil(client, () => IsHurtEnoughForBothRepairs(client.OwnedShip()));
            Assert.True(client.SetCourse(0f, 0f).Accepted);
            PumpUntil(client, () => client.OwnedShip().IsInPort && !client.OwnedShip().HasCourse);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Sails to within sight of the nearest skiff and then lets it come. A skiff shoots from
    /// a range it picks itself, so a course laid onto it would push it out of its own gunsight
    /// for as long as the chase lasted; the course stops short instead, and is only re-plotted
    /// once the ship has arrived and the guns have still not opened.
    /// </summary>
    private static void SailIntoGunfire(IntegrationClient client)
    {
        // Port Lowell stops every shot that would otherwise land, so the fight is picked with a
        // skiff in open water and the hull is out of the harbour before it waits to be hit.
        //
        // A skiff is the one hostile that is always under way: it is a common, so the map keeps
        // a dozen of them on patrol, and none of them is a named captain's escort lying at anchor.
        var hostile = client.ClosestNpcClearOfPort(SkiffArchetype);
        var targetId = hostile.EntityId;
        client.PutToSea(hostile.PositionX, hostile.PositionY);
        var stopwatch = Stopwatch.StartNew();
        var nextCourseAt = TimeSpan.Zero;
        while (client.OwnedShip().Hull == client.OwnedShip().MaxHull)
        {
            if (!client.OwnedShip().HasCourse && stopwatch.Elapsed >= nextCourseAt)
            {
                var approach = ApproachTo(client.OwnedShip(), client.NpcPosition(targetId));
                Assert.True(client.SetCourse(approach.X, approach.Y).Accepted);
                nextCourseAt = stopwatch.Elapsed + TimeSpan.FromSeconds(5);
            }

            client.PumpOnce();
            ThrowIfTimedOut(client, stopwatch);
        }
    }

    /// <summary>
    /// A berth inside the skiff's aggro range but short of the ship itself, so the approach
    /// ends with the hostile picking the fight up rather than backing away from it.
    /// </summary>
    private static (float X, float Y) ApproachTo(Ship own, (float X, float Y) hostile)
    {
        var deltaX = hostile.X - own.PositionX;
        var deltaY = hostile.Y - own.PositionY;
        var distance = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
        if (distance <= AggroApproachUnits)
        {
            return (own.PositionX, own.PositionY);
        }

        var travel = (distance - AggroApproachUnits) / distance;
        return (own.PositionX + deltaX * travel, own.PositionY + deltaY * travel);
    }

    /// <summary>
    /// Thirty percent of the hull. The kit mends a quarter and the channel a fifth of what is
    /// left, so a hull hurt by less than this would be whole again halfway through the test and
    /// the second repair would answer "nothing to repair" instead of doing its job.
    /// </summary>
    private static bool IsHurtEnoughForBothRepairs(Ship ship) =>
        ship.Hull * 10 <= ship.MaxHull * 7;

    private static bool HasLeft(ShipMovement berth, ShipMovement current) =>
        Math.Abs(current.PositionX - berth.PositionX) > 0.01f ||
        Math.Abs(current.PositionY - berth.PositionY) > 0.01f;

    private static void PumpFor(IntegrationClient client, TimeSpan duration)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < duration)
        {
            client.PumpOnce();
            Thread.Sleep(5);
        }
    }

    private static void PumpUntil(IntegrationClient client, Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            client.PumpOnce();
            ThrowIfTimedOut(client, stopwatch);
        }
    }

    private static void ThrowIfTimedOut(IntegrationClient client, Stopwatch stopwatch)
    {
        if (stopwatch.Elapsed > Timeout)
        {
            throw new TimeoutException(
                $"Port integration operation timed out: {client.Describe()}");
        }
    }
}
