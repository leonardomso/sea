using System.Diagnostics;
using SpacetimeDB.Types;
using Xunit;

namespace Sea.Server.IntegrationTests;

/// <summary>
/// The moves every live fight is made of: sail a group of captains onto one hostile, keep them
/// firing through the reload, and pump the connections while the world ticks. The scenarios
/// differ in what they assert afterwards, not in how they pick the fight, so the sailing and the
/// shooting live here once rather than being copied into each of them.
/// </summary>
internal static class FightScenario
{
    public const byte NoTargetRejection = 11;
    public const byte TargetSunkRejection = 12;
    public const byte ReloadingRejection = 13;
    public const byte FiringTooFastRejection = 14;
    public const byte OutOfRangeRejection = 15;

    /// <summary>A hull that has just put to sea keeps its shield until the tenth second.</summary>
    public const byte SpawnShieldedRejection = 23;

    /// <summary>Inside the tier-one cannon's reach with room for the target to manoeuvre.</summary>
    private const float FiringRange = 24f;

    /// <summary>
    /// Sails every captain who is still out of reach onto the target, re-laying the course once a
    /// second because the hostile is under way and the point plotted a second ago is behind it.
    /// </summary>
    public static void MoveIntoRange(
        IReadOnlyCollection<IntegrationClient> clients,
        ulong targetId,
        TimeSpan timeout)
    {
        var nextCourseAt = TimeSpan.Zero;
        var stopwatch = Stopwatch.StartNew();
        while (!clients.All(client => Distance(client.OwnedShip(), client.Npc(targetId)) <= FiringRange))
        {
            if (stopwatch.Elapsed >= nextCourseAt)
            {
                var target = clients.First().Npc(targetId);
                foreach (var client in clients.Where(client =>
                             Distance(client.OwnedShip(), target) > FiringRange))
                {
                    Assert.True(TrySetApproachCourse(client, target));
                }

                nextCourseAt = stopwatch.Elapsed + TimeSpan.FromSeconds(1);
            }

            PumpOnce(clients);
            ThrowIfTimedOut(stopwatch, timeout);
        }
    }

    /// <summary>
    /// Lays a course to a station beside the target, trying each bearing in turn: the ring around
    /// a hostile can be part island, and the module refuses a course that ends on one.
    /// </summary>
    public static bool TrySetApproachCourse(IntegrationClient client, Ship target)
    {
        var source = client.OwnedShip();
        var sourceAngle = MathF.Atan2(
            source.PositionX - target.PositionX,
            source.PositionY - target.PositionY);
        for (var index = 0; index < 8; index++)
        {
            var angle = sourceAngle + index * MathF.PI / 4f;
            var result = client.SetCourse(
                target.PositionX + MathF.Sin(angle) * 22f,
                target.PositionY + MathF.Cos(angle) * 22f);
            if (result.Accepted)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Fires once the module will let this captain fire, closing the range if that is what is in
    /// the way. Returns when the shot lands or when the target is already gone.
    /// </summary>
    public static void FireWhenLegal(
        IntegrationClient client,
        IReadOnlyCollection<IntegrationClient> clients,
        ulong targetId,
        TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (client.Npc(targetId).IsAlive)
        {
            var fire = client.Fire();
            if (fire.Accepted)
            {
                return;
            }

            if (fire.RejectionCode is ReloadingRejection or FiringTooFastRejection
                or SpawnShieldedRejection)
            {
                PumpFor(clients, TimeSpan.FromMilliseconds(100));
                ThrowIfTimedOut(stopwatch, timeout);
                continue;
            }

            // Range is the only geometry left: the magazine bears in every direction, so a
            // rejection that is not the reload is the target sitting too far away.
            Assert.True(
                fire.RejectionCode == OutOfRangeRejection,
                $"Unexpected fire rejection {fire.RejectionCode}; shooter hull " +
                $"{client.OwnedShip().Hull}/{client.OwnedShip().MaxHull} mode " +
                $"{client.OwnedShip().ModeCode} target {client.OwnedShip().TargetEntityId}; " +
                $"target hull {client.Npc(targetId).Hull}/{client.Npc(targetId).MaxHull} " +
                $"alive {client.Npc(targetId).IsAlive}.");
            Approach(client, client.Npc(targetId));
            PumpFor(clients, TimeSpan.FromMilliseconds(150));
            ThrowIfTimedOut(stopwatch, timeout);
        }

        throw new InvalidOperationException("Target sank before every participant fired.");
    }

    /// <summary>
    /// Keeps a whole group shooting one hostile until <paramref name="until"/> answers. The fat
    /// <c>ship</c> row only republishes on a chunk change or a stop, so a caller watching a hull
    /// fall has to keep pumping between volleys rather than trusting the row it already holds.
    /// </summary>
    public static void KeepFiring(
        IReadOnlyCollection<IntegrationClient> clients,
        ulong targetId,
        Func<bool> until,
        TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!until())
        {
            foreach (var client in clients)
            {
                var fire = client.Fire();
                if (fire.RejectionCode is NoTargetRejection or TargetSunkRejection)
                {
                    return;
                }

                if (!fire.Accepted && fire.RejectionCode == OutOfRangeRejection)
                {
                    Approach(client, client.Npc(targetId));
                }
            }

            PumpFor(clients, TimeSpan.FromMilliseconds(150));
            ThrowIfTimedOut(stopwatch, timeout);
        }
    }

    /// <summary>Lays a course that closes to just inside the guns' reach.</summary>
    public static void Approach(IntegrationClient client, Ship target)
    {
        var source = client.OwnedShip();
        var radians = Bearing(source, target) * MathF.PI / 180f;
        var distance = MathF.Max(8f, Distance(source, target) - 20f);
        Assert.True(client.SetCourse(
            source.PositionX + MathF.Sin(radians) * distance,
            source.PositionY + MathF.Cos(radians) * distance).Accepted);
    }

    public static float Bearing(Ship source, Ship target) =>
        MathF.Atan2(target.PositionX - source.PositionX, target.PositionY - source.PositionY) *
        (180f / MathF.PI);

    public static float Distance(Ship source, Ship target)
    {
        var deltaX = target.PositionX - source.PositionX;
        var deltaY = target.PositionY - source.PositionY;
        return MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }

    public static void PumpUntil(
        IReadOnlyCollection<IntegrationClient> clients,
        Func<bool> condition,
        TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            PumpOnce(clients);
            ThrowIfTimedOut(stopwatch, timeout);
        }
    }

    public static void PumpFor(IReadOnlyCollection<IntegrationClient> clients, TimeSpan duration)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < duration)
        {
            PumpOnce(clients);
        }
    }

    public static void PumpOnce(IEnumerable<IntegrationClient> clients)
    {
        foreach (var client in clients)
        {
            client.PumpOnce();
        }

        Thread.Sleep(5);
    }

    public static void ThrowIfTimedOut(Stopwatch stopwatch, TimeSpan timeout)
    {
        if (stopwatch.Elapsed > timeout)
        {
            throw new TimeoutException($"Fight scenario exceeded {timeout.TotalSeconds:0} seconds.");
        }
    }
}
