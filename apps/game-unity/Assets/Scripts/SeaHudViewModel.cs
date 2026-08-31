using System;
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
        public ulong Experience { get; set; }
        public ulong CurrentLevelExperience { get; set; }
        public ulong NextLevelExperience { get; set; }
        public uint Level { get; set; } = 1;
        public uint Gold { get; set; }
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
        public string LastAction { get; set; } = string.Empty;
    }

    public sealed class SeaHudViewModel
    {
        private static readonly CultureInfo DisplayCulture = CultureInfo.InvariantCulture;

        public bool IsReady { get; private set; }
        public string ConnectionStatus { get; private set; }
        public string NavigationText { get; private set; }
        public string HullText { get; private set; }
        public string ExperienceText { get; private set; }
        public string LevelText { get; private set; }
        public string GoldText { get; private set; }
        public float HullProgress { get; private set; }
        public float ExperienceProgress { get; private set; }
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
                ExperienceText = Pair(source.Experience, source.NextLevelExperience),
                LevelText = $"LEVEL {source.Level}",
                GoldText = source.Gold.ToString("N0", DisplayCulture),
                HullProgress = Ratio(source.Hull, source.MaxHull),
                ExperienceProgress = LevelRatio(
                    source.Experience,
                    source.CurrentLevelExperience,
                    source.NextLevelExperience),
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
                LastAction = source.LastAction,
            };
        }

        private static float Ratio(ulong value, ulong maximum) =>
            maximum == 0 ? 0f : Mathf.Clamp01((float)value / maximum);

        private static float LevelRatio(ulong value, ulong current, ulong next) =>
            next <= current ? 1f : Mathf.Clamp01((float)(value - Math.Min(value, current)) / (next - current));

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
