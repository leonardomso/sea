using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sea.Client
{
    public sealed partial class SeaHudController
    {
        private const byte StatCapsRowId = 1;

        // Indexed by AmmunitionCode / AbilityCode minus one.
        private static readonly string[] AmmoSlotNames =
            { "ammo-round", "ammo-chain", "ammo-grapeshot", "ammo-incendiary" };

        private static readonly string[] AbilitySlotNames =
            { "ability-full-sail", "ability-brace", "ability-pump", "ability-smoke" };

        // Reused across rebuilds: a ship carries at most a handful of statuses and the
        // HUD flattens them into one string immediately.
        private readonly List<string> statusBuffer = new(8);
        private bool ammoLabelsApplied;
        private bool abilityLabelsApplied;

        private SeaHudSnapshot CaptureSnapshot()
        {
            var snapshot = new SeaHudSnapshot
            {
                IsReady = connection?.IsSubscribed == true,
                ConnectionStatus = connection?.Status ?? "CONTROLLER MISSING",
                LastAction = game?.LastAction ?? "Waiting for chart link.",
            };

            if (game == null || !game.TryGetLocalShip(out var ship))
            {
                return snapshot;
            }

            var db = connection.Connection.Db;
            ApplyContentLabels(db);

            snapshot.Coordinate = SeaChartCoordinates.LabelAt(ship.PositionX, ship.PositionY);
            snapshot.HeadingDegrees = ship.HeadingDegrees;
            snapshot.Speed = ship.Speed;
            snapshot.Hull = ship.Hull;
            snapshot.SelectedAmmo = game.SelectedAmmoId;
            snapshot.SelectedWeakPoint = game.SelectedWeakPoint;
            snapshot.SelectedAmmoName =
                db.AmmoDef.AmmoId.Find(game.SelectedAmmoId)?.Name ?? game.SelectedAmmoId;

            var stats = ReadDock(db, snapshot);

            var progression = db.PlayerProgression.Owner.Find(connection.LocalIdentity);
            if (progression != null)
            {
                snapshot.MapRank = progression.MapRank;
                snapshot.Gold = progression.Gold;
            }

            snapshot.AmmoQuantity = db.Inventory.ByShip
                .Filter(ship.EntityId)
                .FirstOrDefault(item => item.ItemId == game.SelectedAmmoId)?.Quantity ?? 0;

            var tickRate = connection.WorldTickRate;

            // The ship_stats row is the dock-authored truth; the ship row and its tick
            // budget only stand in until that row replicates.
            snapshot.MaxHull = stats?.MaxHitPoints ?? ship.MaxHull;
            snapshot.ReloadDurationSeconds = stats != null
                ? stats.ReloadMilliseconds / 1000f
                : (float)ship.CannonCooldownTicks / tickRate;

            var world = db.WorldState.Id.Find(1);
            if (world != null)
            {
                var worldTick = connection.CurrentWorldTick;
                snapshot.PortReloadRemainingSeconds = RemainingSeconds(ship.NextPortFireTick, worldTick, tickRate);
                snapshot.StarboardReloadRemainingSeconds =
                    RemainingSeconds(ship.NextStarboardFireTick, worldTick, tickRate);
            }

            ReadTarget(db, ship, snapshot);
            ReadStatuses(db, ship, snapshot);
            if (world != null)
            {
                ReadChannelAndCooldowns(db, ship, snapshot);
            }

            return snapshot;
        }

        // The dock tables (hull/ship_stats) plus the content definitions they point at
        // are what the ledger reports; the ship row only carries hot-path state.
        private ShipStats ReadDock(RemoteTables db, SeaHudSnapshot snapshot)
        {
            var caps = db.StatCaps.Id.Find(StatCapsRowId);
            if (caps != null)
            {
                snapshot.CombatPowerBudget = caps.CombatPowerBudget;
            }

            var hull = db.Hull.ByOwner.Filter(connection.LocalIdentity).FirstOrDefault();
            if (hull != null)
            {
                snapshot.HullName = db.HullDef.HullDefId.Find(hull.HullDefId)?.Name ?? hull.Name;
                var cannon = db.CannonDef.CannonDefId.Find(hull.CannonDefId);
                if (cannon != null)
                {
                    snapshot.CannonName = cannon.Name;
                    snapshot.CannonTier = cannon.Tier;
                }
            }

            var stats = db.ShipStats.ByOwner.Filter(connection.LocalIdentity).FirstOrDefault();
            if (stats == null)
            {
                return null;
            }

            snapshot.VolleyDamage = stats.VolleyDamage;
            snapshot.ReloadMilliseconds = stats.ReloadMilliseconds;
            snapshot.MagazineSize = stats.Magazine;
            snapshot.CombatPowerUsed = stats.CombatPowerUsed;
            return stats;
        }

        private void ReadTarget(RemoteTables db, Ship ship, SeaHudSnapshot snapshot)
        {
            var targetId = ship.TargetEntityId != 0 ? ship.TargetEntityId : game.SelectedTargetId;
            var target = targetId == 0 ? null : db.Ship.EntityId.Find(targetId);
            if (target == null || !target.IsAlive)
            {
                return;
            }

            snapshot.TargetName = string.Format(
                CultureInfo.InvariantCulture,
                "{0}  {1}",
                ArchetypeName(db, target.ArchetypeCode),
                target.EntityId);
            snapshot.TargetHull = target.Hull;
            snapshot.TargetMaxHull = target.MaxHull;
            snapshot.TargetSails = target.Sails;
            snapshot.TargetMaxSails = target.MaxSails;
            snapshot.TargetCannons = target.Cannons;
            snapshot.TargetMaxCannons = target.MaxCannons;
            snapshot.TargetRange = Vector2.Distance(
                new Vector2(ship.PositionX, ship.PositionY),
                new Vector2(target.PositionX, target.PositionY));
        }

        private void ReadStatuses(RemoteTables db, Ship ship, SeaHudSnapshot snapshot)
        {
            statusBuffer.Clear();
            foreach (var status in db.ShipStatus.ByShip.Filter(ship.EntityId))
            {
                if (status.IsActive)
                {
                    statusBuffer.Add(status.Stacks > 1
                        ? string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} ×{1}",
                            status.StatusType.ToUpperInvariant(),
                            status.Stacks)
                        : status.StatusType.ToUpperInvariant());
                }
            }

            snapshot.StatusText = statusBuffer.Count == 0
                ? "CLEAR"
                : string.Join("  •  ", statusBuffer);
        }

        private void ReadChannelAndCooldowns(RemoteTables db, Ship ship, SeaHudSnapshot snapshot)
        {
            var channel = db.ShipChannel.ShipEntityId.Find(ship.EntityId);
            if (channel != null && channel.IsActive)
            {
                snapshot.ProgressText = channel.ChannelType == "repair"
                    ? "REPAIRING"
                    : string.Format(
                        CultureInfo.InvariantCulture,
                        "BOARDING  •  TARGET {0}",
                        channel.TargetEntityId);
                snapshot.Progress = SeaTacticalPresentationRules.ChannelProgress(
                    channel.StartedAtTick,
                    channel.CompletesAtTick,
                    connection.CurrentWorldTick);
            }

            foreach (var cooldown in db.Cooldown.ByShip.Filter(ship.EntityId))
            {
                var seconds = RemainingSeconds(
                    cooldown.ReadyAtTick,
                    connection.CurrentWorldTick,
                    connection.WorldTickRate);
                switch (cooldown.CooldownType)
                {
                    case "full_sail":
                        snapshot.FullSailCooldownSeconds = seconds;
                        break;
                    case "brace":
                        snapshot.BraceCooldownSeconds = seconds;
                        break;
                    case "emergency_pump":
                        snapshot.PumpCooldownSeconds = seconds;
                        break;
                    case "smoke_screen":
                        snapshot.SmokeCooldownSeconds = seconds;
                        break;
                }
            }
        }

        // Slot tooltips are content, not chrome: the ammo rail and the ability rail read
        // their names and cooldowns from ammo_def/ability_def once the tables arrive.
        private void ApplyContentLabels(RemoteTables db)
        {
            if (root == null)
            {
                return;
            }

            var tickRate = Mathf.Max(1u, connection.WorldTickRate);
            if (!ammoLabelsApplied)
            {
                var applied = false;
                for (var index = 0; index < AmmoSlotNames.Length; index++)
                {
                    var ammo = db.AmmoDef.AmmoCode.Find((byte)(index + 1));
                    var button = ButtonFor(AmmoSlotNames[index]);
                    if (ammo == null || button == null)
                    {
                        continue;
                    }

                    button.tooltip = ammo.Name;
                    applied = true;
                }

                ammoLabelsApplied = applied;
            }

            if (abilityLabelsApplied)
            {
                return;
            }

            var abilitiesApplied = false;
            for (var index = 0; index < AbilitySlotNames.Length; index++)
            {
                var ability = db.AbilityDef.AbilityCode.Find((byte)(index + 1));
                var button = ButtonFor(AbilitySlotNames[index]);
                if (ability == null || button == null)
                {
                    continue;
                }

                button.tooltip = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}  •  {1:0.0}s cooldown  •  not available yet",
                    ability.AbilityId.Replace('_', ' ').ToUpperInvariant(),
                    (float)ability.CooldownTicks / tickRate);
                abilitiesApplied = true;
            }

            abilityLabelsApplied = abilitiesApplied;
        }

        // npc_def owns the display name for every hostile archetype; the code is only a
        // fallback for a target whose definition has not replicated yet.
        private static string ArchetypeName(RemoteTables db, byte code)
        {
            var definition = db.NpcDef.ArchetypeCode.Find(code);
            return definition == null
                ? code.ToString(CultureInfo.InvariantCulture)
                : definition.Name.ToUpperInvariant();
        }

        private static float RemainingSeconds(ulong readyTick, ulong currentTick, uint tickRate) =>
            readyTick <= currentTick ? 0f : (float)(readyTick - currentTick) / tickRate;
    }
}
