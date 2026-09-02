using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    [SpacetimeDB.Table(Accessor = "Hull", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByOwner", Columns = new[] { nameof(Owner) })]
    public partial struct Hull
    {
        [PrimaryKey]
        [AutoInc]
        public ulong HullId;
        public Identity Owner;
        public string HullDefId;
        public string Name;
        public string CannonDefId;
        public byte CannonCount;
    }

    [SpacetimeDB.Table(Accessor = "ShipStats", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByOwner", Columns = new[] { nameof(Owner) })]
    public partial struct ShipStats
    {
        [PrimaryKey]
        public ulong HullId;
        public Identity Owner;
        public uint VolleyDamage;
        public uint ReloadMilliseconds;
        public byte Magazine;
        public uint MaxHitPoints;
        public float ArmorFront;
        public float ArmorSides;
        public float ArmorBack;
        public float SpeedSquaresPerSecond;
        public float TurnDegreesPerSecond;
        public byte RangeSquares;
        public float RepairAmount;
        public uint RepairChannelMilliseconds;
        public float CombatPowerUsed;
        public float CombatPowerInactive;
        public float FightScore;

        public static ShipStats From(Hull hull, ShipStatSheet sheet) => new()
        {
            HullId = hull.HullId,
            Owner = hull.Owner,
            VolleyDamage = sheet.VolleyDamage,
            ReloadMilliseconds = sheet.ReloadMilliseconds,
            Magazine = sheet.Magazine,
            MaxHitPoints = sheet.MaxHitPoints,
            ArmorFront = sheet.ArmorFront,
            ArmorSides = sheet.ArmorSides,
            ArmorBack = sheet.ArmorBack,
            SpeedSquaresPerSecond = sheet.SpeedSquaresPerSecond,
            TurnDegreesPerSecond = sheet.TurnDegreesPerSecond,
            RangeSquares = sheet.RangeSquares,
            RepairAmount = sheet.RepairAmount,
            RepairChannelMilliseconds = sheet.RepairChannelMilliseconds,
            CombatPowerUsed = sheet.CombatPowerUsed,
            CombatPowerInactive = sheet.CombatPowerInactive,
            FightScore = sheet.FightScore,
        };
    }

    [SpacetimeDB.Table(Accessor = "PlayerAccount")]
    public partial struct PlayerAccount
    {
        [PrimaryKey]
        public Identity Owner;
        public string AccountId;
    }

#pragma warning disable STDB_UNSTABLE
    [SpacetimeDB.ClientVisibilityFilter]
    public static readonly Filter HullOwnerFilter =
        new Filter.Sql("SELECT * FROM hull WHERE hull.owner = :sender");

    [SpacetimeDB.ClientVisibilityFilter]
    public static readonly Filter ShipStatsOwnerFilter =
        new Filter.Sql("SELECT * FROM ship_stats WHERE ship_stats.owner = :sender");
#pragma warning restore STDB_UNSTABLE
}
