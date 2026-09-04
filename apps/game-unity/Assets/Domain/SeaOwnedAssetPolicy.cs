using System.Collections.Generic;
using UnityEngine;

namespace Sea.Client
{
    public static class SeaOwnedAssetSlots
    {
        public const string PlayerShip = "ship.player";
        public const string SkiffShip = "ship.skiff";
        public const string ReefCrabShip = "ship.reef_crab";
        public const string FancyShip = "ship.fancy";
        public const string RedMaryShip = "ship.red_mary";
        public const string ShipMaterial = "ship.material";
        public const string Island = "world.island";
        public const string Reef = "world.reef";
        public const string Harbor = "world.harbor";
        public const string Loot = "world.loot";
        public const string Projectile = "combat.projectile";
        public const string Impact = "combat.impact";
        public const string UiIcon = "ui.icon";
        public const string Audio = "audio.effect";
    }

    public enum SeaOwnedAssetFallback : byte
    {
        None = 0,
        ProceduralGeometry = 1,
        ProceduralEffect = 2,
        GeneratedIcon = 3,
        Silent = 4,
    }

    public readonly struct SeaOwnedAssetDefinition
    {
        public SeaOwnedAssetDefinition(
            string id,
            bool required,
            SeaOwnedAssetFallback fallback)
        {
            Id = id;
            Required = required;
            Fallback = fallback;
        }

        public string Id { get; }
        public bool Required { get; }
        public SeaOwnedAssetFallback Fallback { get; }
    }

    public static class SeaOwnedAssetPolicy
    {
        private static readonly SeaOwnedAssetDefinition[] Slots =
        {
            Required(SeaOwnedAssetSlots.PlayerShip),
            Required(SeaOwnedAssetSlots.SkiffShip),
            Required(SeaOwnedAssetSlots.ReefCrabShip),
            Required(SeaOwnedAssetSlots.FancyShip),
            Required(SeaOwnedAssetSlots.RedMaryShip),
            Required(SeaOwnedAssetSlots.ShipMaterial),
            Optional(SeaOwnedAssetSlots.Island, SeaOwnedAssetFallback.ProceduralGeometry),
            Optional(SeaOwnedAssetSlots.Reef, SeaOwnedAssetFallback.ProceduralGeometry),
            Optional(SeaOwnedAssetSlots.Harbor, SeaOwnedAssetFallback.ProceduralGeometry),
            Optional(SeaOwnedAssetSlots.Loot, SeaOwnedAssetFallback.ProceduralGeometry),
            Optional(SeaOwnedAssetSlots.Projectile, SeaOwnedAssetFallback.ProceduralEffect),
            Optional(SeaOwnedAssetSlots.Impact, SeaOwnedAssetFallback.ProceduralEffect),
            Optional(SeaOwnedAssetSlots.UiIcon, SeaOwnedAssetFallback.GeneratedIcon),
            Optional(SeaOwnedAssetSlots.Audio, SeaOwnedAssetFallback.Silent),
        };

        /// <summary>The player plus every hostile hull the map can show.</summary>
        public const int ShipRoleCount = 5;

        public static IReadOnlyList<SeaOwnedAssetDefinition> Definitions => Slots;

        public static SeaOwnedShipRole ShipRole(byte factionCode, byte archetypeCode)
        {
            if (factionCode == 1)
            {
                return SeaOwnedShipRole.Player;
            }

            return archetypeCode switch
            {
                2 => SeaOwnedShipRole.ReefCrab,
                3 => SeaOwnedShipRole.Fancy,
                4 => SeaOwnedShipRole.RedMary,
                _ => SeaOwnedShipRole.Skiff,
            };
        }

        public static string ShipSlot(SeaOwnedShipRole role) => role switch
        {
            SeaOwnedShipRole.Player => SeaOwnedAssetSlots.PlayerShip,
            SeaOwnedShipRole.Skiff => SeaOwnedAssetSlots.SkiffShip,
            SeaOwnedShipRole.ReefCrab => SeaOwnedAssetSlots.ReefCrabShip,
            SeaOwnedShipRole.Fancy => SeaOwnedAssetSlots.FancyShip,
            SeaOwnedShipRole.RedMary => SeaOwnedAssetSlots.RedMaryShip,
            _ => throw new System.ArgumentOutOfRangeException(nameof(role)),
        };

        private static SeaOwnedAssetDefinition Required(string id) =>
            new(id, true, SeaOwnedAssetFallback.None);

        private static SeaOwnedAssetDefinition Optional(
            string id,
            SeaOwnedAssetFallback fallback) => new(id, false, fallback);
    }

    public enum SeaOwnedShipRole : byte
    {
        Player = 1,
        Skiff = 2,
        ReefCrab = 3,
        Fancy = 4,
        RedMary = 5,
    }

    public static class SeaShipVariantPolicy
    {
        public static Color Tint(byte factionCode, byte archetypeCode)
        {
            if (factionCode == 1)
            {
                return Color.white;
            }

            return archetypeCode switch
            {
                1 => new Color(0.86f, 1f, 0.88f, 1f),
                2 => new Color(1f, 0.78f, 0.66f, 1f),
                3 => new Color(0.72f, 0.84f, 1f, 1f),
                4 => new Color(0.78f, 0.22f, 0.24f, 1f),
                _ => new Color(0.86f, 0.88f, 0.90f, 1f),
            };
        }
    }
}
