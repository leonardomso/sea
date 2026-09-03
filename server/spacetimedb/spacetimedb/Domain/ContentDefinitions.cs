namespace Sea.Server;

public sealed record WorldObjectContent
{
    public required ulong EntityId { get; init; }
    public required string Kind { get; init; }
    public required float X { get; init; }
    public required float Y { get; init; }
    public required float Radius { get; init; }
    public required bool BlocksMovement { get; init; }
    public required float DirectionDegrees { get; init; }
    public required float MovementSpeed { get; init; }
    public required float Intensity { get; init; }
}

public sealed record CurrentContent
{
    public required ulong ZoneId { get; init; }
    public required float X { get; init; }
    public required float Y { get; init; }
    public required float Radius { get; init; }
    public required float DirectionDegrees { get; init; }
    public required float Strength { get; init; }
}

public sealed record MapContent
{
    public required byte MapId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Biome { get; init; }
    public required byte MapRank { get; init; }
    public required byte Width { get; init; }
    public required byte Height { get; init; }
    public required string PvpMode { get; init; }
    public required string MaterialId { get; init; }
    public required string PortName { get; init; }
    public required float PortX { get; init; }
    public required float PortY { get; init; }
    public required float PortRadius { get; init; }
    /// <summary>
    /// Row 0 is the southern row at world y = <see cref="WorldRules.MapMin"/> and rows run north;
    /// <see cref="ChartCoordinates"/> instead counts its vertical axis by column, down from the top.
    /// </summary>
    public required IReadOnlyList<string> TerrainRows { get; init; }
    public required IReadOnlyList<WorldObjectContent> Objects { get; init; }
    public required IReadOnlyList<CurrentContent> Currents { get; init; }
}

public sealed record HullContent
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required byte Tier { get; init; }
    public required uint HitPoints { get; init; }
    public required float ArmorFront { get; init; }
    public required float ArmorSides { get; init; }
    public required float ArmorBack { get; init; }
    public required byte CannonSlots { get; init; }
    public required float SpeedSquaresPerSecond { get; init; }
    public required float TurnDegreesPerSecond { get; init; }
    public required byte Magazine { get; init; }
    public required uint CostGold { get; init; }
    public required byte MapRankRequired { get; init; }
}

public sealed record CannonContent
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required byte Tier { get; init; }
    public required uint Damage { get; init; }
    public required float ReloadSeconds { get; init; }
    public required byte RangeSquares { get; init; }
    public required uint CostGold { get; init; }
}

public sealed record AmmunitionContent
{
    public required string Id { get; init; }
    public required AmmunitionCode Code { get; init; }
    public required string Name { get; init; }
    public required float DamageMultiplier { get; init; }
    public required float ReloadMultiplier { get; init; }
    public required uint GoldPerVolley { get; init; }
    public required AmmoEffectCode Effect { get; init; }
    public required float EffectMagnitude { get; init; }
    public required float EffectDurationSeconds { get; init; }
    public required byte RangeLimitSquares { get; init; }
    public required float RangeMultiplier { get; init; }
}

public sealed record NpcContent
{
    public required string Id { get; init; }
    public required ShipArchetypeCode Code { get; init; }
    public required string Name { get; init; }
    public required byte Tier { get; init; }
    public required byte MapId { get; init; }
    public required string Family { get; init; }
    public required string Behavior { get; init; }
    public required float AggroRangeSquares { get; init; }
    public required float DesiredRangeSquares { get; init; }
    public required float MaximumSpeedSquares { get; init; }
    public required uint Hull { get; init; }
    public required uint CannonDamage { get; init; }
    public required AmmunitionCode PreferredAmmunition { get; init; }
    public required uint GoldReward { get; init; }
    public required ulong ExperienceReward { get; init; }
}

public sealed record StatCapsContent
{
    public required float DamageBonusCap { get; init; }
    public required float ReloadBonusCap { get; init; }
    public required byte MagazineBonusCap { get; init; }
    public required float HitPointBonusCap { get; init; }
    public required float ArmorPointsCap { get; init; }
    public required float ArmorAbsoluteMax { get; init; }
    public required float SpeedBonusCap { get; init; }
    public required float TurnBonusCap { get; init; }
    public required byte RangeBonusCapSquares { get; init; }
    public required float RepairAmountBonusCap { get; init; }
    public required float RepairChannelBonusCap { get; init; }
    public required byte CannonSlotBonusCap { get; init; }
    public required float CombatPowerBudget { get; init; }
    public required float CombatPowerArmorWeight { get; init; }
    public required float ReloadFloorSeconds { get; init; }
    public required float FireMinIntervalSeconds { get; init; }
    public required float MagazineRefillIdleSeconds { get; init; }
    public required float BurnPerSecond { get; init; }
    public required float BurnDurationSeconds { get; init; }
    public required float BurnHealMultiplier { get; init; }
    public required float RepairBaseAmount { get; init; }
    public required float RepairChannelSeconds { get; init; }
    public required float RepairCooldownSeconds { get; init; }
    public required float RepairFatigue { get; init; }
    public required float RepairFatigueWindowSeconds { get; init; }
    public required float RepairCancelThreshold { get; init; }
    public required float KitHealAmount { get; init; }
    public required float KitCooldownSeconds { get; init; }
    public required float RespawnSeconds { get; init; }
    public required float SpawnShieldSeconds { get; init; }
    public required IReadOnlyList<float> NpcHitPointMultipliers { get; init; }
    public required IReadOnlyList<float> NpcDpsMultipliers { get; init; }
    public required IReadOnlyList<float> NpcArmorByTier { get; init; }
    public required uint GoldBase { get; init; }
    public required float GoldGrowth { get; init; }
}

public sealed record GameContent
{
    public required IReadOnlyList<MapContent> Maps { get; init; }
    public required IReadOnlyList<HullContent> Hulls { get; init; }
    public required IReadOnlyList<CannonContent> Cannons { get; init; }
    public required IReadOnlyList<AmmunitionContent> Ammunition { get; init; }
    public required IReadOnlyList<NpcContent> Npcs { get; init; }
    public required StatCapsContent StatCaps { get; init; }
}
