using System.Collections.Generic;
using UnityEngine;

namespace Sea.Client
{
    public static class SeaOwnedAssetSlots
    {
        public const string PlayerShip = "ship.player";
        public const string PatrolShip = "ship.patrol";
        public const string RaiderShip = "ship.raider";
        public const string GunshipShip = "ship.gunship";
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
            Required(SeaOwnedAssetSlots.PatrolShip),
            Required(SeaOwnedAssetSlots.RaiderShip),
            Required(SeaOwnedAssetSlots.GunshipShip),
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

        public static IReadOnlyList<SeaOwnedAssetDefinition> Definitions => Slots;

        public static SeaOwnedShipRole ShipRole(byte factionCode, byte archetypeCode)
        {
            if (factionCode == 1)
            {
                return SeaOwnedShipRole.Player;
            }

            return archetypeCode switch
            {
                2 => SeaOwnedShipRole.Raider,
                3 => SeaOwnedShipRole.Gunship,
                _ => SeaOwnedShipRole.Patrol,
            };
        }

        public static string ShipSlot(SeaOwnedShipRole role) => role switch
        {
            SeaOwnedShipRole.Player => SeaOwnedAssetSlots.PlayerShip,
            SeaOwnedShipRole.Patrol => SeaOwnedAssetSlots.PatrolShip,
            SeaOwnedShipRole.Raider => SeaOwnedAssetSlots.RaiderShip,
            SeaOwnedShipRole.Gunship => SeaOwnedAssetSlots.GunshipShip,
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
        Patrol = 2,
        Raider = 3,
        Gunship = 4,
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
                _ => new Color(0.86f, 0.88f, 0.90f, 1f),
            };
        }
    }
}
