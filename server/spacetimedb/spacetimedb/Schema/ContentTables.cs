using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    [SpacetimeDB.Table(Accessor = "MapDef", Public = true)]
    public partial struct MapDef
    {
        [PrimaryKey]
        public byte MapId;
        [Unique]
        public string Code;
        public string Name;
        public string Biome;
        public byte MapRank;
        public byte Width;
        public byte Height;
        public string PvpMode;
        public string MaterialId;
        public string PortName;
        public float PortX;
        public float PortY;
        public float PortRadius;

        public static MapDef From(MapContent map) => new()
        {
            MapId = map.MapId,
            Code = map.Code,
            Name = map.Name,
            Biome = map.Biome,
            MapRank = map.MapRank,
            Width = map.Width,
            Height = map.Height,
            PvpMode = map.PvpMode,
            MaterialId = map.MaterialId,
            PortName = map.PortName,
            PortX = map.PortX,
            PortY = map.PortY,
            PortRadius = map.PortRadius,
        };
    }

    [SpacetimeDB.Table(Accessor = "Sector", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByMap", Columns = new[] { nameof(MapId) })]
    public partial struct Sector
    {
        [PrimaryKey]
        public ulong SectorId;
        public byte MapId;
        public byte X;
        public byte Y;
        public byte TerrainCode;
    }

    [SpacetimeDB.Table(Accessor = "HullDef", Public = true)]
    public partial struct HullDef
    {
        [PrimaryKey]
        public string HullDefId;
        public string Name;
        public byte Tier;
        public uint HitPoints;
        public float ArmorFront;
        public float ArmorSides;
        public float ArmorBack;
        public byte CannonSlots;
        public float SpeedSquaresPerSecond;
        public float TurnDegreesPerSecond;
        public byte Magazine;
        public uint CostGold;
        public byte MapRankRequired;

        public static HullDef From(HullContent hull) => new()
        {
            HullDefId = hull.Id,
            Name = hull.Name,
            Tier = hull.Tier,
            HitPoints = hull.HitPoints,
            ArmorFront = hull.ArmorFront,
            ArmorSides = hull.ArmorSides,
            ArmorBack = hull.ArmorBack,
            CannonSlots = hull.CannonSlots,
            SpeedSquaresPerSecond = hull.SpeedSquaresPerSecond,
            TurnDegreesPerSecond = hull.TurnDegreesPerSecond,
            Magazine = hull.Magazine,
            CostGold = hull.CostGold,
            MapRankRequired = hull.MapRankRequired,
        };
    }

    [SpacetimeDB.Table(Accessor = "CannonDef", Public = true)]
    public partial struct CannonDef
    {
        [PrimaryKey]
        public string CannonDefId;
        public string Name;
        public byte Tier;
        public uint Damage;
        public float ReloadSeconds;
        public byte RangeSquares;
        public uint CostGold;

        public static CannonDef From(CannonContent cannon) => new()
        {
            CannonDefId = cannon.Id,
            Name = cannon.Name,
            Tier = cannon.Tier,
            Damage = cannon.Damage,
            ReloadSeconds = cannon.ReloadSeconds,
            RangeSquares = cannon.RangeSquares,
            CostGold = cannon.CostGold,
        };
    }

    [SpacetimeDB.Table(Accessor = "AmmoDef", Public = true)]
    public partial struct AmmoDef
    {
        [PrimaryKey]
        public string AmmoId;
        [Unique]
        public byte AmmoCode;
        public string Name;
        public float DamageMultiplier;
        public float ReloadMultiplier;
        public uint GoldPerVolley;
        public byte EffectCode;
        public float EffectMagnitude;
        public float EffectDurationSeconds;
        public byte RangeLimitSquares;
        public uint HullDamage;
        public uint SailDamage;
        public uint CannonDamage;
        public uint CrewDamage;
        public float RangeMultiplier;
        public string AppliedStatus;
        public byte AppliedStatusCode;

        public static AmmoDef From(AmmunitionContent ammunition) => new()
        {
            AmmoId = ammunition.Id,
            AmmoCode = (byte)ammunition.Code,
            Name = ammunition.Name,
            DamageMultiplier = ammunition.DamageMultiplier,
            ReloadMultiplier = ammunition.ReloadMultiplier,
            GoldPerVolley = ammunition.GoldPerVolley,
            EffectCode = (byte)ammunition.Effect,
            EffectMagnitude = ammunition.EffectMagnitude,
            EffectDurationSeconds = ammunition.EffectDurationSeconds,
            RangeLimitSquares = ammunition.RangeLimitSquares,
            HullDamage = ammunition.HullDamage,
            SailDamage = ammunition.SailDamage,
            CannonDamage = ammunition.CannonDamage,
            CrewDamage = ammunition.CrewDamage,
            RangeMultiplier = ammunition.RangeMultiplier,
            AppliedStatus = ammunition.AppliedStatus,
            AppliedStatusCode = (byte)ammunition.AppliedStatusCode,
        };
    }

    [SpacetimeDB.Table(Accessor = "AbilityDef", Public = true)]
    public partial struct AbilityDef
    {
        [PrimaryKey]
        public string AbilityId;
        [Unique]
        public byte AbilityCode;
        public uint CooldownTicks;
        public uint DurationTicks;

        public static AbilityDef From(AbilityContent ability) => new()
        {
            AbilityId = ability.Id,
            AbilityCode = (byte)ability.Code,
            CooldownTicks = ability.CooldownTicks,
            DurationTicks = ability.DurationTicks,
        };
    }

    [SpacetimeDB.Table(Accessor = "NpcDef", Public = true)]
    public partial struct NpcDef
    {
        [PrimaryKey]
        public string NpcId;
        [Unique]
        public byte ArchetypeCode;
        public string Name;
        public byte Tier;
        public byte MapId;
        public string Family;
        public string Behavior;
        public float AggroRange;
        public float DesiredRange;
        public float MaximumSpeed;
        public uint Hull;
        public uint CannonDamage;
        public byte PreferredAmmoCode;
        public byte PreferredWeakPointCode;
        public uint GoldReward;
        public ulong ExperienceReward;

        public static NpcDef From(NpcContent npc) => new()
        {
            NpcId = npc.Id,
            ArchetypeCode = (byte)npc.Code,
            Name = npc.Name,
            Tier = npc.Tier,
            MapId = npc.MapId,
            Family = npc.Family,
            Behavior = npc.Behavior,
            AggroRange = npc.AggroRange,
            DesiredRange = npc.DesiredRange,
            MaximumSpeed = npc.MaximumSpeed,
            Hull = npc.Hull,
            CannonDamage = npc.CannonDamage,
            PreferredAmmoCode = (byte)npc.PreferredAmmunition,
            PreferredWeakPointCode = (byte)npc.PreferredWeakPoint,
            GoldReward = npc.GoldReward,
            ExperienceReward = npc.ExperienceReward,
        };
    }

    [SpacetimeDB.Table(Accessor = "StatCaps", Public = true)]
    public partial struct StatCaps
    {
        [PrimaryKey]
        public byte Id;
        public float DamageBonusCap;
        public float ReloadBonusCap;
        public byte MagazineBonusCap;
        public float HitPointBonusCap;
        public float ArmorPointsCap;
        public float ArmorAbsoluteMax;
        public float SpeedBonusCap;
        public float TurnBonusCap;
        public byte RangeBonusCapSquares;
        public float RepairAmountBonusCap;
        public float RepairChannelBonusCap;
        public byte CannonSlotBonusCap;
        public float CombatPowerBudget;
        public float CombatPowerArmorWeight;
        public float ReloadFloorSeconds;
        public float FireMinIntervalSeconds;
        public float MagazineRefillIdleSeconds;
        public float BurnPerSecond;
        public float BurnDurationSeconds;
        public float BurnHealMultiplier;
        public float RepairBaseAmount;
        public float RepairChannelSeconds;
        public float RepairCooldownSeconds;
        public float RepairFatigue;
        public float RepairFatigueWindowSeconds;
        public float RepairCancelThreshold;
        public float KitHealAmount;
        public float KitCooldownSeconds;
        public float RespawnSeconds;
        public float SpawnShieldSeconds;
#pragma warning disable MA0016 // SpacetimeDB algebraic arrays require List<T> fields.
        public List<float> NpcHitPointMultipliers;
        public List<float> NpcDpsMultipliers;
        public List<float> NpcArmorByTier;
#pragma warning restore MA0016
        public uint GoldBase;
        public float GoldGrowth;

        public static StatCaps From(StatCapsContent caps) => new()
        {
            Id = 1, // Singleton row; the seed inserts exactly one.
            DamageBonusCap = caps.DamageBonusCap,
            ReloadBonusCap = caps.ReloadBonusCap,
            MagazineBonusCap = caps.MagazineBonusCap,
            HitPointBonusCap = caps.HitPointBonusCap,
            ArmorPointsCap = caps.ArmorPointsCap,
            ArmorAbsoluteMax = caps.ArmorAbsoluteMax,
            SpeedBonusCap = caps.SpeedBonusCap,
            TurnBonusCap = caps.TurnBonusCap,
            RangeBonusCapSquares = caps.RangeBonusCapSquares,
            RepairAmountBonusCap = caps.RepairAmountBonusCap,
            RepairChannelBonusCap = caps.RepairChannelBonusCap,
            CannonSlotBonusCap = caps.CannonSlotBonusCap,
            CombatPowerBudget = caps.CombatPowerBudget,
            CombatPowerArmorWeight = caps.CombatPowerArmorWeight,
            ReloadFloorSeconds = caps.ReloadFloorSeconds,
            FireMinIntervalSeconds = caps.FireMinIntervalSeconds,
            MagazineRefillIdleSeconds = caps.MagazineRefillIdleSeconds,
            BurnPerSecond = caps.BurnPerSecond,
            BurnDurationSeconds = caps.BurnDurationSeconds,
            BurnHealMultiplier = caps.BurnHealMultiplier,
            RepairBaseAmount = caps.RepairBaseAmount,
            RepairChannelSeconds = caps.RepairChannelSeconds,
            RepairCooldownSeconds = caps.RepairCooldownSeconds,
            RepairFatigue = caps.RepairFatigue,
            RepairFatigueWindowSeconds = caps.RepairFatigueWindowSeconds,
            RepairCancelThreshold = caps.RepairCancelThreshold,
            KitHealAmount = caps.KitHealAmount,
            KitCooldownSeconds = caps.KitCooldownSeconds,
            RespawnSeconds = caps.RespawnSeconds,
            SpawnShieldSeconds = caps.SpawnShieldSeconds,

            // The row is serialized on insert; these copies convert the catalog's
            // IReadOnlyList<float> to the column type without LINQ.
            NpcHitPointMultipliers = new List<float>(caps.NpcHitPointMultipliers),
            NpcDpsMultipliers = new List<float>(caps.NpcDpsMultipliers),
            NpcArmorByTier = new List<float>(caps.NpcArmorByTier),
            GoldBase = caps.GoldBase,
            GoldGrowth = caps.GoldGrowth,
        };
    }
}
