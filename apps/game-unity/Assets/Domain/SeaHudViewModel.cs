using System.Globalization;
using UnityEngine;

namespace Sea.Client
{
    public sealed class SeaHudSnapshot
    {
        public bool IsReady { get; set; }
        public string ConnectionStatus { get; set; } = "Connecting…";
        public string Coordinate { get; set; } = "—";
        public float HeadingDegrees { get; set; }
        public float Speed { get; set; }
        public uint Hull { get; set; }
        public uint MaxHull { get; set; }
        public byte MapRank { get; set; } = 1;
        public uint Gold { get; set; }
        public string HullName { get; set; } = string.Empty;
        public string CannonName { get; set; } = string.Empty;
        public byte CannonTier { get; set; }
        public uint VolleyDamage { get; set; }
        public uint ReloadMilliseconds { get; set; }
        public byte MagazineSize { get; set; }
        public float CombatPowerUsed { get; set; }
        public float CombatPowerBudget { get; set; }
        public string SelectedAmmoName { get; set; } = string.Empty;
        public string TargetName { get; set; } = string.Empty;
        public uint TargetHull { get; set; }
        public uint TargetMaxHull { get; set; }
        public uint TargetSails { get; set; }
        public uint TargetMaxSails { get; set; }
        public uint TargetCannons { get; set; }
        public uint TargetMaxCannons { get; set; }
        public float TargetRange { get; set; }
        public string SelectedWeakPoint { get; set; } = "hull";
        public string SelectedAmmo { get; set; } = "round";
        public uint AmmoQuantity { get; set; }
        public float PortReloadRemainingSeconds { get; set; }
        public float StarboardReloadRemainingSeconds { get; set; }
        public float ReloadDurationSeconds { get; set; } = 1f;
        public string StatusText { get; set; } = "CLEAR";
        public string ProgressText { get; set; } = string.Empty;
        public float Progress { get; set; }
        public float FullSailCooldownSeconds { get; set; }
        public float BraceCooldownSeconds { get; set; }
        public float PumpCooldownSeconds { get; set; }
        public float SmokeCooldownSeconds { get; set; }
        public string LastAction { get; set; } = string.Empty;
    }

    public sealed class SeaHudViewModel
    {
        /// <summary>
        /// The HUD reads only the player's ship and the ship it targets, so other ship rows
        /// can change without forcing a rebuild.
        /// </summary>
        public static bool DependsOnShip(
            ulong shipEntityId,
            ulong localShipEntityId,
            ulong targetEntityId) =>
            shipEntityId != 0 &&
            (shipEntityId == localShipEntityId || shipEntityId == targetEntityId);

        private static readonly CultureInfo DisplayCulture = CultureInfo.InvariantCulture;

        public bool IsReady { get; private set; }
        public string ConnectionStatus { get; private set; }
        public string NavigationText { get; private set; }
        public string HullText { get; private set; }
        public string MapRankText { get; private set; }
        public string GoldText { get; private set; }
        public string ShipText { get; private set; }
        public string VolleyText { get; private set; }
        public string CombatPowerText { get; private set; }
        public float HullProgress { get; private set; }
        public bool HasTarget { get; private set; }
        public string TargetName { get; private set; }
        public float TargetHullProgress { get; private set; }
        public float TargetSailsProgress { get; private set; }
        public float TargetCannonsProgress { get; private set; }
        public string TargetHullText { get; private set; }
        public string TargetSailsText { get; private set; }
        public string TargetCannonsText { get; private set; }
        public string TargetRangeText { get; private set; }
        public string SelectedWeakPoint { get; private set; }
        public string SelectedAmmo { get; private set; }
        public string SelectedAmmoLabel { get; private set; }
        public string AmmoQuantity { get; private set; }
        public float PortReloadProgress { get; private set; }
        public float StarboardReloadProgress { get; private set; }
        public bool PortReady { get; private set; }
        public bool StarboardReady { get; private set; }
        public string PortReloadText { get; private set; }
        public string StarboardReloadText { get; private set; }
        public string StatusText { get; private set; }
        public string ProgressText { get; private set; }
        public float Progress { get; private set; }
        public float FullSailCooldownSeconds { get; private set; }
        public float BraceCooldownSeconds { get; private set; }
        public float PumpCooldownSeconds { get; private set; }
        public float SmokeCooldownSeconds { get; private set; }
        public string LastAction { get; private set; }

        public static SeaHudViewModel From(SeaHudSnapshot source)
        {
            var reloadDuration = Mathf.Max(0.01f, source.ReloadDurationSeconds);
            return new SeaHudViewModel
            {
                IsReady = source.IsReady,
                ConnectionStatus = source.ConnectionStatus,
                NavigationText = string.Format(
                    DisplayCulture,
                    "{0}  •  {1:000}°  •  {2:0.0} KN",
                    source.Coordinate,
                    NormalizeHeading(source.HeadingDegrees),
                    source.Speed),
                HullText = Pair(source.Hull, source.MaxHull),
                MapRankText = source.MapRank.ToString(DisplayCulture),
                GoldText = source.Gold.ToString("N0", DisplayCulture) + " ¤",
                ShipText = ShipLabel(source),
                VolleyText = string.Format(
                    DisplayCulture,
                    "DMG {0:N0}  •  MAG {1:N0}  •  {2:0.0}s",
                    source.VolleyDamage,
                    source.MagazineSize,
                    source.ReloadMilliseconds / 1000f),
                CombatPowerText = string.Format(
                    DisplayCulture,
                    "{0:0.#} / {1:0.#} CP",
                    source.CombatPowerUsed,
                    source.CombatPowerBudget),
                HullProgress = Ratio(source.Hull, source.MaxHull),
                HasTarget = !string.IsNullOrWhiteSpace(source.TargetName),
                TargetName = source.TargetName,
                TargetHullProgress = Ratio(source.TargetHull, source.TargetMaxHull),
                TargetSailsProgress = Ratio(source.TargetSails, source.TargetMaxSails),
                TargetCannonsProgress = Ratio(source.TargetCannons, source.TargetMaxCannons),
                TargetHullText = Pair(source.TargetHull, source.TargetMaxHull),
                TargetSailsText = Pair(source.TargetSails, source.TargetMaxSails),
                TargetCannonsText = Pair(source.TargetCannons, source.TargetMaxCannons),
                TargetRangeText = string.Format(DisplayCulture, "{0:0.0} NM", source.TargetRange),
                SelectedWeakPoint = source.SelectedWeakPoint.ToUpperInvariant(),
                SelectedAmmo = source.SelectedAmmo.ToUpperInvariant(),
                SelectedAmmoLabel = (string.IsNullOrWhiteSpace(source.SelectedAmmoName)
                    ? source.SelectedAmmo
                    : source.SelectedAmmoName).ToUpperInvariant(),
                AmmoQuantity = source.AmmoQuantity.ToString("N0", DisplayCulture),
                PortReloadProgress = 1f - Mathf.Clamp01(source.PortReloadRemainingSeconds / reloadDuration),
                StarboardReloadProgress = 1f - Mathf.Clamp01(source.StarboardReloadRemainingSeconds / reloadDuration),
                PortReady = source.PortReloadRemainingSeconds <= 0f,
                StarboardReady = source.StarboardReloadRemainingSeconds <= 0f,
                PortReloadText = ReloadText(source.PortReloadRemainingSeconds),
                StarboardReloadText = ReloadText(source.StarboardReloadRemainingSeconds),
                StatusText = source.StatusText,
                ProgressText = source.ProgressText,
                Progress = Mathf.Clamp01(source.Progress),
                FullSailCooldownSeconds = Mathf.Max(0f, source.FullSailCooldownSeconds),
                BraceCooldownSeconds = Mathf.Max(0f, source.BraceCooldownSeconds),
                PumpCooldownSeconds = Mathf.Max(0f, source.PumpCooldownSeconds),
                SmokeCooldownSeconds = Mathf.Max(0f, source.SmokeCooldownSeconds),
                LastAction = source.LastAction,
            };
        }

        private static string ShipLabel(SeaHudSnapshot source)
        {
            if (string.IsNullOrWhiteSpace(source.HullName))
            {
                return "—";
            }

            return string.IsNullOrWhiteSpace(source.CannonName)
                ? source.HullName.ToUpperInvariant()
                : string.Format(
                    DisplayCulture,
                    "{0}  •  {1} T{2}",
                    source.HullName.ToUpperInvariant(),
                    source.CannonName.ToUpperInvariant(),
                    source.CannonTier);
        }

        private static float Ratio(ulong value, ulong maximum) =>
            maximum == 0 ? 0f : Mathf.Clamp01((float)value / maximum);

        private static string Pair(ulong value, ulong maximum) =>
            string.Format(DisplayCulture, "{0:N0} / {1:N0}", value, maximum);

        private static float NormalizeHeading(float heading)
        {
            heading %= 360f;
            return heading < 0f ? heading + 360f : heading;
        }

        private static string ReloadText(float seconds) =>
            seconds <= 0f ? "READY" : string.Format(DisplayCulture, "{0:0.0}s", seconds);
    }
}
