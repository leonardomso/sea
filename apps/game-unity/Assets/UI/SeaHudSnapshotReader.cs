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
        private const uint EnvironmentRowId = 1;

        // Indexed by AmmunitionCode minus one.
        private static readonly string[] AmmoSlotNames =
            { "ammo-round", "ammo-chain", "ammo-grapeshot", "ammo-incendiary" };

        // Reused across rebuilds: a ship carries at most a handful of statuses and the
        // HUD flattens them into one string immediately.
        private readonly List<string> statusBuffer = new(8);
        private bool ammoLabelsApplied;

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
            snapshot.SelectedAmmoName =
                db.AmmoDef.AmmoId.Find(game.SelectedAmmoId)?.Name ?? game.SelectedAmmoId;

            var stats = ReadDock(db, snapshot);

            var progression = db.PlayerProgression.Owner.Find(connection.LocalIdentity);
            if (progression != null)
            {
                snapshot.MapRank = progression.MapRank;
                snapshot.Gold = progression.Gold;
            }

            snapshot.AmmoQuantity = AmmoQuantity(db, ship.EntityId, game.SelectedAmmoId);

            var tickRate = Mathf.Max(1u, connection.WorldTickRate);

            // Damage is still resolved against the ship row's hull budget, so the bar
            // pairs Hull with that row's MaxHull; mixing in ship_stats reads "50 / 1,600".
            snapshot.MaxHull = ship.MaxHull;
            // The ship_stats row is the dock-authored reload; the ship row's own reload budget
            // stands in until that row replicates.
            snapshot.ReloadDurationSeconds = stats != null
                ? stats.ReloadMilliseconds / 1000f
                : (float)ship.ReloadTicks / tickRate;
            snapshot.ReadyVolleys = ship.ReadyVolleys;
            snapshot.MagazineSize = ship.MagazineSize;

            // One wind blows over the whole map, so the dial reads the world's own row.
            var weather = db.EnvironmentState.Id.Find(EnvironmentRowId);
            if (weather != null)
            {
                snapshot.WindDirectionDegrees = weather.WindDirectionDegrees;
            }

            var world = db.WorldState.Id.Find(1);
            if (world != null)
            {
                // One magazine, one bar: what is left of the reload behind the next volley.
                snapshot.ReloadRemainingSeconds = ship.ReloadTicks <= ship.ReloadProgressTicks
                    ? 0f
                    : (float)(ship.ReloadTicks - ship.ReloadProgressTicks) / tickRate;
            }

            ReadTarget(db, ship, snapshot);
            ReadStatuses(db, ship, snapshot);
            if (world != null)
            {
                ReadChannelAndCooldowns(db, ship, snapshot);
                ReadWreck(db, ship, snapshot);
            }

            return snapshot;
        }

        // The dock tables (hull/ship_stats) plus the content definitions they point at
        // are what the ledger reports; the ship row only carries hot-path state.
        private static uint AmmoQuantity(RemoteTables db, ulong shipEntityId, string ammoId)
        {
            foreach (var item in db.Inventory.ByShip.Filter(shipEntityId))
            {
                if (item.ItemId == ammoId)
                {
                    return item.Quantity;
                }
            }

            return 0;
        }

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

            // The aim persists on the server; the frame only shows while the target is in vision.
            var range = Vector2.Distance(
                new Vector2(ship.PositionX, ship.PositionY),
                new Vector2(target.PositionX, target.PositionY));
            if (!SeaPresentationRules.IsInVision(range))
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
            snapshot.TargetRange = range;

            // The server reads the face from where this ship sits, so the HUD reads it the same
            // way rather than asking the captain to pick an aim point that no longer exists.
            var face = SeaVolleyPresentationRules.ArmorFaceAt(
                target.HeadingDegrees,
                new Vector2(target.PositionX, target.PositionY),
                new Vector2(ship.PositionX, ship.PositionY));
            snapshot.TargetArmorFace = face;
            snapshot.TargetArmorAbsorption = face switch
            {
                "front" => target.ArmorFront,
                "back" => target.ArmorBack,
                _ => target.ArmorSides,
            };
        }

        private void ReadStatuses(RemoteTables db, Ship ship, SeaHudSnapshot snapshot)
        {
            statusBuffer.Clear();
            foreach (var effect in db.Effect.ByShip.Filter(ship.EntityId))
            {
                if (effect.IsActive)
                {
                    statusBuffer.Add(effect.EffectType.ToUpperInvariant());
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
                snapshot.ProgressText = ChannelLabel(channel.ChannelType);
                snapshot.Progress = SeaTacticalPresentationRules.ChannelProgress(
                    channel.StartedAtTick,
                    channel.CompletesAtTick,
                    connection.CurrentWorldTick);
            }

            // The channel and the kit are the two cooldowns the module still writes, and they run
            // separately so that being caught mid-repair still leaves the crate on deck.
            foreach (var cooldown in db.Cooldown.ByShip.Filter(ship.EntityId))
            {
                var remaining = RemainingSeconds(
                    cooldown.ReadyAtTick,
                    connection.CurrentWorldTick,
                    connection.WorldTickRate);
                if (cooldown.CooldownType == "repair")
                {
                    snapshot.RepairCooldownSeconds = remaining;
                }
                else if (cooldown.CooldownType == "repair_kit")
                {
                    snapshot.RepairKitCooldownSeconds = remaining;
                }
            }
        }

        /// <summary>
        /// The seabed. A wreck with no berth chosen is waiting on its captain; one that has chosen
        /// is only waiting on the clock, and the prompt says which.
        /// </summary>
        private void ReadWreck(RemoteTables db, Ship ship, SeaHudSnapshot snapshot)
        {
            snapshot.IsSunk = !ship.IsAlive;
            if (ship.IsAlive)
            {
                return;
            }

            var work = db.RespawnWork.ShipEntityId.Find(ship.EntityId);
            snapshot.RespawnChosen = work != null && work.OptionCode != 0;
            if (work != null)
            {
                snapshot.RespawnRemainingSeconds = RemainingSeconds(
                    work.RespawnAtTick,
                    connection.CurrentWorldTick,
                    connection.WorldTickRate);
            }
        }

        private static string ChannelLabel(string channelType) => channelType switch
        {
            "repair" => "REPAIRING",
            "cast_off" => "CASTING OFF",
            _ => channelType.ToUpperInvariant(),
        };

        // Slot tooltips are content, not chrome: the ammo rail reads its names from ammo_def
        // once the table arrives.
        private void ApplyContentLabels(RemoteTables db)
        {
            if (root == null)
            {
                return;
            }

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
