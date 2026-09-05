using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    /// <summary>
    /// Everything admission needs to answer an order to throw hooks. The target is read live
    /// rather than off the fat row, exactly as firing does: four squares is close enough that a
    /// position one chunk stale would let a captain grapple a ship already out of reach.
    /// </summary>
    private static CommandSnapshot BoardingSnapshot(
        ReducerContext ctx,
        TickWorld world,
        Ship source,
        CommandSnapshot snapshot)
    {
        var target = source.TargetEntityId == 0
            ? default(Ship?)
            : ctx.Db.Ship.EntityId.Find(source.TargetEntityId);
        if (target is Ship tracked)
        {
            HydrateTrackedKinematics(ctx, world, ref tracked);
            target = tracked;
        }

        return snapshot with
        {
            TargetIsFriendly = target is Ship friend && friend.FactionCode == source.FactionCode,
            BoardingRejection = BoardingRules.Validate(new BoardingRequest
            {
                SourceAlive = source.IsActive && source.IsAlive,
                TargetSelected = target.HasValue,
                TargetAlive = target is Ship alive && alive.IsActive && alive.IsAlive,
                InPort = IsHarborTruce(ctx, world, source, target),
                DistanceSquares = target is Ship reached
                    ? CombatRules.Distance(
                        source.PositionX,
                        source.PositionY,
                        reached.PositionX,
                        reached.PositionY)
                    : float.MaxValue,
                DefenderHull = target?.Hull ?? 0u,
                DefenderMaxHull = target?.MaxHull ?? 0u,
                AttackerHands = HandsAt(source, world.Tick),
                AttackerMaxHands = source.MaxHands,
                CurrentTick = world.Tick,
                AttackerCooldownUntilTick = source.BoardCooldownUntilTick,
                DefenderImmuneUntilTick = target?.BoardImmuneUntilTick ?? 0UL,
            }),
        };
    }

    /// <summary>
    /// The harbour truce, which is a circle on the water and not the berth flag: a hull that has
    /// cast off is out of port the moment she moves, and hooks must not reach her until she is
    /// clear of the safe water. Either deck inside it is enough, because a boarding is two ships.
    /// </summary>
    private static bool IsHarborTruce(
        ReducerContext ctx,
        TickWorld world,
        Ship source,
        Ship? target)
    {
        if (source.IsInPort || target?.IsInPort == true)
        {
            return true;
        }

        if (world.Harbor(ctx) is not WorldObject harbor)
        {
            return false;
        }

        return PortRules.IsSafeWater(
                source.PositionX,
                source.PositionY,
                harbor.PositionX,
                harbor.PositionY) ||
            (target is Ship other && PortRules.IsSafeWater(
                other.PositionX,
                other.PositionY,
                harbor.PositionX,
                harbor.PositionY));
    }

    /// <summary>
    /// Her hands as of this tick, brought forward from whenever they were last written. Nothing
    /// adds a sailor per hull per tick: the recovery is a subtraction of two tick numbers, done
    /// when somebody asks, and the answer is the same either way.
    /// </summary>
    private static uint HandsAt(Ship ship, ulong tick) => BoardingRules.Recover(
        ship.Hands,
        ship.MaxHands,
        tick > ship.HandsRecoveredAtTick ? tick - ship.HandsRecoveredAtTick : 0UL);

    /// <summary>
    /// Grapples and takes a ship. One instant check, decided by the same hash a critical is
    /// decided by, so a replay of the same command log boards on exactly the same ticks. Neither
    /// ship is stopped or slowed (SEA_5 section 9.2) -- the fight goes on either way.
    /// </summary>
    private static void ApplyStartBoarding(ReducerContext ctx, TickWorld world, ref Ship source)
    {
        var target = ctx.Db.Ship.EntityId.Find(source.TargetEntityId) ??
            throw new InvalidOperationException("Accepted boarding command has no target.");
        HydrateTrackedKinematics(ctx, world, ref target);

        var attacker = new BoardingParty(
            HandsAt(source, world.Tick),
            BoardingRules.AttackerMorale(source.Hull, source.MaxHull),
            source.HullTier);
        var defenders = new BoardingParty(
            HandsAt(target, world.Tick),
            BoardingRules.DefenderMorale(target.Hull, target.MaxHull),
            target.HullTier);
        var outcome = BoardingRules.Resolve(
            attacker,
            defenders,
            target.MaxHull,
            CriticalHitRules.Roll(
                world.Environment(ctx)?.Seed ?? 0UL,
                world.Tick,
                source.EntityId,
                target.EntityId));

        SpendHands(ref source, attacker.Hands, outcome.AttackerHandsLost, world.Tick);
        ChargeFailedBoarding(ref source, outcome);

        // SEA_5 section 9.3: a minute before she may grapple a captain again, fifteen seconds
        // before she may grapple another hostile.
        source.BoardCooldownUntilTick = world.Tick +
            (target.FactionCode == (byte)FactionCode.Player
                ? BoardingRules.PlayerCooldownTicks
                : BoardingRules.NpcCooldownTicks);
        source.LastCombatTick = world.Tick;
        source.IsEngaged = true;

        SettleBoarding(ctx, world, source, target, defenders, outcome);
    }

    /// <summary>
    /// What the boarding did to the ship that was boarded, and what it paid. The victim's five
    /// minutes are hers whether the party took the deck or was thrown off it: she was grappled
    /// either way, and the immunity is there so one hull cannot be worked over by a fleet.
    /// </summary>
    private static void SettleBoarding(
        ReducerContext ctx,
        TickWorld world,
        Ship source,
        Ship target,
        BoardingParty defenders,
        BoardingOutcome outcome)
    {
        var ships = new ShipTickBuffer();
        var defender = target;
        SpendHands(ref defender, defenders.Hands, outcome.DefenderHandsLost, world.Tick);
        defender.BoardImmuneUntilTick = world.Tick + BoardingRules.VictimImmunityTicks;
        if (outcome.SilenceTicks > 0)
        {
            defender.WeaponSilencedUntilTick = world.Tick + outcome.SilenceTicks;
        }

        var applied = ApplyDamageToShip(
            ctx,
            ships,
            source.EntityId,
            ref defender,
            outcome.HullDamage,
            world.Tick,
            DamageSourceCode.Boarding);
        ships.Stage(defender);
        ships.Flush(ctx, world.Tick);

        PayForBoarding(ctx, source, outcome);
        AppendEvent(
            ctx,
            world.Tick,
            source.EntityId,
            outcome.AttackerWon ? "boarding_won" : "boarding_repulsed",
            $"target={defender.EntityId},damage={applied}");
    }

    /// <summary>
    /// Writes hands back with the recovery clock reset to now: what she has left is what she has
    /// as of this tick, so the next reader must not credit her for the minutes before it.
    /// </summary>
    private static void SpendHands(ref Ship ship, uint standing, uint lost, ulong tick)
    {
        ship.Hands = lost >= standing ? 0u : standing - lost;
        ship.HandsRecoveredAtTick = tick;
    }

    /// <summary>
    /// The attacker's own price for being thrown off. It comes off the hull directly rather than
    /// through the damage path, because a repulse is not a broadside: nobody shot at her, there is
    /// no armour face to find and no encounter to credit. One point of hull is the floor, so a
    /// captain cannot sink herself on a gamble she was allowed to take.
    /// </summary>
    private static void ChargeFailedBoarding(ref Ship source, BoardingOutcome outcome)
    {
        if (outcome.AttackerHullFractionLost <= 0f || source.Hull <= 1)
        {
            return;
        }

        var price = (uint)MathF.Round(source.MaxHull * outcome.AttackerHullFractionLost);
        source.Hull = price >= source.Hull ? 1u : source.Hull - price;
    }

    /// <summary>
    /// The haul, or the bill. SEA_2 section 5.7 pays a won boarding fifteen map drops scaled by
    /// how one-sided it was, and charges a lost one twenty-five, capped at a twentieth of the
    /// purse so a poor captain is not ruined by one bad throw. A hostile boarding party is paid in
    /// ship rather than in coin and has no purse to charge.
    /// </summary>
    private static void PayForBoarding(ReducerContext ctx, Ship source, BoardingOutcome outcome)
    {
        if (ctx.Db.PlayerOwnership.ShipEntityId.Find(source.EntityId) is not PlayerOwnership owner ||
            ctx.Db.PlayerProgression.Owner.Find(owner.Owner) is not PlayerProgression progression)
        {
            return;
        }

        var baseGold = NpcDerivation.BaseGold(source.MapId, Catalog.Content.StatCaps);
        progression.Gold = outcome.AttackerWon
            ? ProgressionRules.AddGoldSaturating(
                progression.Gold,
                BoardingRules.Haul(baseGold, outcome.LootMultiplier))
            : ProgressionRules.TakeGoldSaturating(
                progression.Gold,
                BoardingRules.FailGold(baseGold, progression.Gold));
        ctx.Db.PlayerProgression.Owner.Update(progression);
    }
}
