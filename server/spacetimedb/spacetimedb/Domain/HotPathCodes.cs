namespace Sea.Server;

public enum FactionCode : byte
{
    Neutral = 0,
    Player = 1,
    Npc = 2,
}

public enum ShipArchetypeCode : byte
{
    PlayerSloop = 0,
    Patrol = 1,
    Raider = 2,
    Gunship = 3,
}

public enum AmmunitionCode : byte
{
    None = 0,
    Round = 1,
    Chain = 2,
    Grapeshot = 3,
    Incendiary = 4,
}

public enum AbilityCode : byte
{
    None = 0,
    FullSail = 1,
    Brace = 2,
    EmergencyPump = 3,
    SmokeScreen = 4,
}

public enum StatusCode : byte
{
    None = 0,
    Burning = 1,
    Flooding = 2,
    Slowed = 3,
    DisabledSails = 4,
    FullSail = 5,
    Brace = 6,
    EmergencyPump = 7,
    SmokeScreen = 8,
    BoardingFatigue = 9,
}

public enum WeakPointCode : byte
{
    Hull = 0,
    Sails = 1,
    Cannons = 2,
}

public enum BroadsideCode : byte
{
    Port = 0,
    Starboard = 1,
}

public enum ChannelCode : byte
{
    None = 0,
    Repair = 1,
    Boarding = 2,
}

public enum CooldownCode : byte
{
    None = 0,
    FullSail = 1,
    Brace = 2,
    EmergencyPump = 3,
    SmokeScreen = 4,
    Boarding = 5,
}

public enum WorldObjectCode : byte
{
    Harbor = 0,
    Island = 1,
    Reef = 2,
    Shoal = 3,
    Storm = 4,
}

public enum TerrainCode : byte
{
    Water = 0,
    Shallow = 1,
    Land = 2,
}

public enum AmmoEffectCode : byte
{
    None = 0,
    Slow = 1,
    Burn = 2,
    SlowReload = 3,
}

public static class HotPathCodes
{
    public const byte FullSailMovementMask = 1 << 0;
    public const byte SlowedMovementMask = 1 << 1;

    public static byte MovementMask(StatusCode status) => status switch
    {
        StatusCode.FullSail => FullSailMovementMask,
        StatusCode.Slowed => SlowedMovementMask,
        _ => 0,
    };

    public static ShipArchetypeCode ShipArchetype(string id) => id switch
    {
        "patrol" => ShipArchetypeCode.Patrol,
        "raider" => ShipArchetypeCode.Raider,
        "gunship" => ShipArchetypeCode.Gunship,
        _ => ShipArchetypeCode.PlayerSloop,
    };

    public static bool TryParseAmmunition(string? id, out AmmunitionCode code)
    {
        code = id switch
        {
            "round" => AmmunitionCode.Round,
            "chain" => AmmunitionCode.Chain,
            "grapeshot" => AmmunitionCode.Grapeshot,
            "incendiary" => AmmunitionCode.Incendiary,
            _ => AmmunitionCode.None,
        };
        return code != AmmunitionCode.None;
    }

    public static string AmmunitionId(AmmunitionCode code) => code switch
    {
        AmmunitionCode.Round => "round",
        AmmunitionCode.Chain => "chain",
        AmmunitionCode.Grapeshot => "grapeshot",
        AmmunitionCode.Incendiary => "incendiary",
        _ => "none",
    };

    public static string WeakPointId(WeakPointCode code) => code switch
    {
        WeakPointCode.Sails => "sails",
        WeakPointCode.Cannons => "cannons",
        _ => "hull",
    };

    public static bool TryParseAbility(string? id, out AbilityCode code)
    {
        code = id switch
        {
            "full_sail" => AbilityCode.FullSail,
            "brace" => AbilityCode.Brace,
            "emergency_pump" => AbilityCode.EmergencyPump,
            "smoke_screen" => AbilityCode.SmokeScreen,
            _ => AbilityCode.None,
        };
        return code != AbilityCode.None;
    }

    public static bool TryParseWeakPoint(string? id, out WeakPointCode code)
    {
        code = id?.ToLowerInvariant() switch
        {
            "hull" => WeakPointCode.Hull,
            "sails" => WeakPointCode.Sails,
            "cannons" => WeakPointCode.Cannons,
            _ => (WeakPointCode)byte.MaxValue,
        };
        return (byte)code != byte.MaxValue;
    }

    public static bool TryParseBroadside(string? id, out BroadsideCode code)
    {
        code = id?.ToLowerInvariant() switch
        {
            "port" => BroadsideCode.Port,
            "starboard" => BroadsideCode.Starboard,
            _ => (BroadsideCode)byte.MaxValue,
        };
        return (byte)code != byte.MaxValue;
    }

    public static bool TryParseWorldObject(string? id, out WorldObjectCode code)
    {
        code = id switch
        {
            "harbor" => WorldObjectCode.Harbor,
            "island" => WorldObjectCode.Island,
            "reef" => WorldObjectCode.Reef,
            "shoal" => WorldObjectCode.Shoal,
            "storm" => WorldObjectCode.Storm,
            _ => (WorldObjectCode)byte.MaxValue,
        };
        return (byte)code != byte.MaxValue;
    }

    public static bool TryParseTerrain(char symbol, out TerrainCode terrain)
    {
        switch (symbol)
        {
            case '.':
                terrain = TerrainCode.Water;
                return true;
            case '~':
                terrain = TerrainCode.Shallow;
                return true;
            case '#':
                terrain = TerrainCode.Land;
                return true;
            default:
                terrain = TerrainCode.Water;
                return false;
        }
    }

    public static StatusCode TryStatus(string? id) => id switch
    {
        "burning" => StatusCode.Burning,
        "flooding" => StatusCode.Flooding,
        "slowed" => StatusCode.Slowed,
        "disabled_sails" => StatusCode.DisabledSails,
        "full_sail" => StatusCode.FullSail,
        "brace" => StatusCode.Brace,
        "emergency_pump" => StatusCode.EmergencyPump,
        "smoke_screen" => StatusCode.SmokeScreen,
        "boarding_fatigue" => StatusCode.BoardingFatigue,
        _ => StatusCode.None,
    };

    public static StatusCode StatusFor(AbilityCode ability) => ability switch
    {
        AbilityCode.FullSail => StatusCode.FullSail,
        AbilityCode.Brace => StatusCode.Brace,
        AbilityCode.EmergencyPump => StatusCode.EmergencyPump,
        AbilityCode.SmokeScreen => StatusCode.SmokeScreen,
        _ => StatusCode.None,
    };

    public static CooldownCode CooldownFor(AbilityCode ability) => ability switch
    {
        AbilityCode.FullSail => CooldownCode.FullSail,
        AbilityCode.Brace => CooldownCode.Brace,
        AbilityCode.EmergencyPump => CooldownCode.EmergencyPump,
        AbilityCode.SmokeScreen => CooldownCode.SmokeScreen,
        _ => CooldownCode.None,
    };

    public static bool BlocksMovement(WorldObjectCode kind) =>
        kind is WorldObjectCode.Island or WorldObjectCode.Reef;

    public static string StatusId(StatusCode code) => code switch
    {
        StatusCode.Burning => "burning",
        StatusCode.Flooding => "flooding",
        StatusCode.Slowed => "slowed",
        StatusCode.DisabledSails => "disabled_sails",
        StatusCode.FullSail => "full_sail",
        StatusCode.Brace => "brace",
        StatusCode.EmergencyPump => "emergency_pump",
        StatusCode.SmokeScreen => "smoke_screen",
        StatusCode.BoardingFatigue => "boarding_fatigue",
        _ => "none",
    };

    public static string CooldownId(CooldownCode code) => code switch
    {
        CooldownCode.FullSail => "full_sail",
        CooldownCode.Brace => "brace",
        CooldownCode.EmergencyPump => "emergency_pump",
        CooldownCode.SmokeScreen => "smoke_screen",
        CooldownCode.Boarding => "boarding",
        _ => "none",
    };
}
