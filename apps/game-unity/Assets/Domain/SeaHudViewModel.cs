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
        public uint MagazineSize { get; set; }
        public float WindDirectionDegrees { get; set; }
        public float WindStrength { get; set; }
        public float CombatPowerUsed { get; set; }
        public float CombatPowerBudget { get; set; }
        public string SelectedAmmoName { get; set; } = string.Empty;
        public string TargetName { get; set; } = string.Empty;
        public uint TargetHull { get; set; }
        public uint TargetMaxHull { get; set; }
        public float TargetRange { get; set; }
        public string TargetArmorFace { get; set; } = string.Empty;
        public float TargetArmorAbsorption { get; set; }
        public string SelectedAmmo { get; set; } = "round";
        public uint AmmoQuantity { get; set; }
        public uint ReadyVolleys { get; set; }
        public float ReloadRemainingSeconds { get; set; }
        public float ReloadDurationSeconds { get; set; } = 1f;
        public string StatusText { get; set; } = "CLEAR";
        public string ProgressText { get; set; } = string.Empty;
        public float Progress { get; set; }
        public float RepairCooldownSeconds { get; set; }
        public float RepairKitCooldownSeconds { get; set; }
        public bool IsSunk { get; set; }
        public bool RespawnChosen { get; set; }
        public float RespawnRemainingSeconds { get; set; }
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
        public string TargetHullText { get; private set; }
        public string TargetRangeText { get; private set; }
        public string TargetArmorText { get; private set; }
        public string SelectedAmmo { get; private set; }
        public string SelectedAmmoLabel { get; private set; }
        public string AmmoQuantity { get; private set; }
        public float ReloadProgress { get; private set; }
        public bool IsLoaded { get; private set; }
        public string ReloadText { get; private set; }
        public string MagazineText { get; private set; }

        /// <summary>Volleys the racks hold, and how many of them can leave now.</summary>
        public int MagazineSize { get; private set; }

        public int ReadyVolleys { get; private set; }

        /// <summary>Degrees clockwise from north: the heading the wind blows towards.</summary>
        public float WindRotationDegrees { get; private set; }

        public string WindText { get; private set; }
        public string StatusText { get; private set; }
        public string ProgressText { get; private set; }
        public float Progress { get; private set; }
        public float RepairCooldownSeconds { get; private set; }
        public float RepairKitCooldownSeconds { get; private set; }
        public bool IsSunk { get; private set; }

        /// <summary>The berth is only worth offering while the wreck has not asked for one.</summary>
        public bool CanChooseBerth { get; private set; }
        public string WreckText { get; private set; }
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
                TargetHullText = Pair(source.TargetHull, source.TargetMaxHull),
                TargetRangeText = string.Format(DisplayCulture, "{0:0.0} NM", source.TargetRange),
                TargetArmorText = ArmorText(source),
                SelectedAmmo = source.SelectedAmmo.ToUpperInvariant(),
                SelectedAmmoLabel = (string.IsNullOrWhiteSpace(source.SelectedAmmoName)
                    ? source.SelectedAmmo
                    : source.SelectedAmmoName).ToUpperInvariant(),
                AmmoQuantity = source.AmmoQuantity.ToString("N0", DisplayCulture),
                ReloadProgress = 1f - Mathf.Clamp01(source.ReloadRemainingSeconds / reloadDuration),
                IsLoaded = source.ReadyVolleys > 0,
                ReloadText = ReadinessText(source),
                MagazineText = Pair(source.ReadyVolleys, source.MagazineSize),
                MagazineSize = (int)source.MagazineSize,
                ReadyVolleys = (int)source.ReadyVolleys,
                WindRotationDegrees = NormalizeHeading(source.WindDirectionDegrees),
                WindText = WindLabel(source),
                StatusText = source.StatusText,
                ProgressText = source.ProgressText,
                Progress = Mathf.Clamp01(source.Progress),
                RepairCooldownSeconds = Mathf.Max(0f, source.RepairCooldownSeconds),
                RepairKitCooldownSeconds = Mathf.Max(0f, source.RepairKitCooldownSeconds),
                IsSunk = source.IsSunk,
                CanChooseBerth = source.IsSunk && !source.RespawnChosen,
                WreckText = WreckLabel(source),
                LastAction = source.LastAction,
            };
        }

        /// <summary>
        /// A wreck reads either the offer or the wait, never both: once the berth is asked for
        /// there is nothing left to decide, only the count until the hull is back on the water.
        /// </summary>
        private static string WreckLabel(SeaHudSnapshot source)
        {
            if (!source.RespawnChosen)
            {
                return "PORT LOWELL HAS A BERTH WAITING.";
            }

            return string.Format(
                DisplayCulture,
                "PUTTING OUT FROM PORT LOWELL  •  {0:0}s",
                Mathf.Max(0f, source.RespawnRemainingSeconds));
        }

        /// <summary>
        /// The wind reads the way a captain calls it: the point of the compass it blows towards
        /// and the strength that decides how much of it the sails keep.
        /// </summary>
        private static string WindLabel(SeaHudSnapshot source) => string.Format(
            DisplayCulture,
            "{0}  •  {1:0.0}",
            CompassPoint(NormalizeHeading(source.WindDirectionDegrees)),
            source.WindStrength);

        public static string CompassPoint(float bearingDegrees)
        {
            var index = Mathf.RoundToInt(NormalizeHeading(bearingDegrees) / 45f) % CompassPoints.Length;
            return CompassPoints[index];
        }

        private static readonly string[] CompassPoints =
            { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

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

        /// <summary>
        /// A loaded magazine reads READY however long the reload behind it still has to run;
        /// the countdown only matters once the last volley has left the racks.
        /// </summary>
        private static string ReadinessText(SeaHudSnapshot source)
        {
            if (source.ReadyVolleys > 0)
            {
                return "READY";
            }

            return source.ReloadRemainingSeconds <= 0f
                ? "LOADING"
                : string.Format(DisplayCulture, "{0:0.0}s", source.ReloadRemainingSeconds);
        }

        private static string ArmorText(SeaHudSnapshot source) =>
            string.IsNullOrWhiteSpace(source.TargetArmorFace)
                ? "—"
                : string.Format(
                    DisplayCulture,
                    "{0}  •  {1:0}% ABSORBED",
                    source.TargetArmorFace.ToUpperInvariant(),
                    Mathf.Clamp01(source.TargetArmorAbsorption) * 100f);
    }
}
