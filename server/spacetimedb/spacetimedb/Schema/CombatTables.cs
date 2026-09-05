using SpacetimeDB;

// Everything an encounter leaves behind: who hurt what, what the pool came to and
// who was paid out of it. Split off Tables.cs to keep both files inside the
// five-hundred-line limit, not because the schema is two schemas.
public static partial class Module
{
    /// <summary>
    /// The heals a ship has completed recently, newest last. Fatigue is a rolling window, so it
    /// cannot be collapsed into a counter; the list is pruned every time it is read.
    /// </summary>
    [SpacetimeDB.Table(Accessor = "ShipHealLog")]
    public partial struct ShipHealLog
    {
        [PrimaryKey]
        public ulong ShipEntityId;
#pragma warning disable MA0016 // SpacetimeDB algebraic arrays require List<T> fields.
        public List<ulong> CompletedTicks;
#pragma warning restore MA0016
    }

    [SpacetimeDB.Table(Accessor = "CombatContribution")]
    [SpacetimeDB.Index.BTree(Accessor = "ByEncounter", Columns = new[] { nameof(EncounterId) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByContributor", Columns = new[] { nameof(ContributorEntityId) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByEncounterContributor", Columns = new[] { nameof(EncounterId), nameof(ContributorEntityId) })]
    public partial struct CombatContribution
    {
        [PrimaryKey]
        [AutoInc]
        public ulong ContributionId;
        public ulong EncounterId;
        public ulong ContributorEntityId;
        public ulong Damage;
        public ulong Boarding;
        public ulong Support;
    }

    [SpacetimeDB.Table(Accessor = "CombatEncounter")]
    [SpacetimeDB.Index.BTree(Accessor = "ByNpc", Columns = new[] { nameof(NpcEntityId) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByState", Columns = new[] { nameof(StateCode) })]
    public partial struct CombatEncounter
    {
        [PrimaryKey]
        public ulong EncounterId;
        public ulong NpcEntityId;
        public byte StateCode;
        public uint GoldPool;
        public ulong ExperiencePool;
        public ulong OpenedAtTick;
        public ulong SettledAtTick;
    }

    [SpacetimeDB.Table(Accessor = "EncounterReward", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByOwner", Columns = new[] { nameof(Owner) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByEncounter", Columns = new[] { nameof(EncounterId) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByEncounterContributor", Columns = new[] { nameof(EncounterId), nameof(ContributorEntityId) })]
    public partial struct EncounterReward
    {
        [PrimaryKey]
        [AutoInc]
        public ulong RewardId;
        public ulong EncounterId;
        public Identity Owner;
        public ulong ContributorEntityId;
        public uint Gold;
        public ulong Experience;
        public ulong AwardedAtTick;
    }

    [SpacetimeDB.Table(Accessor = "EncounterRewardEvent", Public = true, Event = true)]
    public partial struct EncounterRewardEvent
    {
        public Identity Owner;
        public ulong EncounterId;
        public ulong ContributorEntityId;
        public uint Gold;
        public ulong Experience;
    }

    [SpacetimeDB.Table(Accessor = "CombatEvent", Public = true, Event = true)]
    public partial struct CombatEvent
    {
        public ulong OwnerEntityId;
        public string EventType;
        public string Details;
        public ulong Tick;
    }

#pragma warning disable STDB_UNSTABLE
    [SpacetimeDB.ClientVisibilityFilter]
    public static readonly Filter EncounterRewardOwnerFilter = new Filter.Sql(
        "SELECT * FROM encounter_reward WHERE encounter_reward.owner = :sender");

    [SpacetimeDB.ClientVisibilityFilter]
    public static readonly Filter EncounterRewardEventOwnerFilter = new Filter.Sql(
        "SELECT * FROM encounter_reward_event WHERE encounter_reward_event.owner = :sender");
#pragma warning restore STDB_UNSTABLE
}
