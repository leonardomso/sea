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

public enum ChannelCode : byte
{
    None = 0,
    Repair = 1,
    CastOff = 2,
}

public enum CooldownCode : byte
{
    None = 0,
    Repair = 1,
    RepairKit = 2,
}

/// <summary>
/// Where a hit came from. It names the event a damaged ship publishes and decides whether the
/// hit is the kind a repair crew cannot work through.
/// </summary>
public enum DamageSourceCode : byte
{
    Volley = 0,
    Burning = 1,
    Storm = 2,
}

/// <summary>
/// Where a wreck comes back. Zero means its owner has not chosen yet, which is why a player who
/// never answers stays on the seabed while an NPC, handed its home the moment it sinks, does not.
/// </summary>
public enum RespawnOptionCode : byte
{
    Unchosen = 0,
    HomePort = 1,
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
    /// <summary>
    /// The one movement-relevant effect left. It rides on <c>Ship.MovementStatusMask</c> so the
    /// sailing shard can read it without touching the effect table.
    /// </summary>
    public const byte SlowedMovementMask = 1 << 0;

    public static byte MovementMask(EffectCode effect) =>
        effect == EffectCode.Slowed ? SlowedMovementMask : (byte)0;

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

    public static bool BlocksMovement(WorldObjectCode kind) =>
        kind is WorldObjectCode.Island or WorldObjectCode.Reef;

    public static string EffectId(EffectCode code) => code switch
    {
        EffectCode.Slowed => "slowed",
        EffectCode.Burning => "burning",
        EffectCode.ReloadSlowed => "reload_slowed",
        _ => "none",
    };

    public static string ArmorFaceId(ArmorFace face) => face switch
    {
        ArmorFace.Front => "front",
        ArmorFace.Back => "back",
        _ => "sides",
    };

    public static string CooldownId(CooldownCode code) => code switch
    {
        CooldownCode.Repair => "repair",
        CooldownCode.RepairKit => "repair_kit",
        _ => "none",
    };

    public static string DamageSourceId(DamageSourceCode code) => code switch
    {
        DamageSourceCode.Burning => "burning",
        DamageSourceCode.Storm => "storm",
        _ => "volley",
    };

    public static string ChannelId(ChannelCode code) => code switch
    {
        ChannelCode.Repair => "repair",
        ChannelCode.CastOff => "cast_off",
        _ => "none",
    };
}
