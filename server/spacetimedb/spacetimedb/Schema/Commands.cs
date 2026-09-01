using SpacetimeDB;

public static partial class Module
{
    [SpacetimeDB.Type]
    public partial struct SetCourseCommand
    {
        public float X;
        public float Y;
    }

    [SpacetimeDB.Type]
    public partial struct StopCourseCommand;

    [SpacetimeDB.Type]
    public partial struct SelectTargetCommand
    {
        public ulong EntityId;
    }

    [SpacetimeDB.Type]
    public partial struct ClearTargetCommand;

    [SpacetimeDB.Type]
    public partial struct SetAmmoCommand
    {
        public string AmmoId;
    }

    [SpacetimeDB.Type]
    public partial struct FireBroadsideCommand
    {
        public string Side;
        public string WeakPoint;
    }

    [SpacetimeDB.Type]
    public partial struct ActivateAbilityCommand
    {
        public string AbilityId;
    }

    [SpacetimeDB.Type]
    public partial struct StartRepairCommand;

    [SpacetimeDB.Type]
    public partial struct StartBoardingCommand;

    [SpacetimeDB.Type]
    public partial struct CancelChannelCommand;

    [SpacetimeDB.Type]
    public partial record ShipCommand : TaggedEnum<(
        SetCourseCommand SetCourse,
        StopCourseCommand StopCourse,
        SelectTargetCommand SelectTarget,
        ClearTargetCommand ClearTarget,
        SetAmmoCommand SetAmmo,
        FireBroadsideCommand FireBroadside,
        ActivateAbilityCommand ActivateAbility,
        StartRepairCommand StartRepair,
        StartBoardingCommand StartBoarding,
        CancelChannelCommand CancelChannel)>;

    [SpacetimeDB.Type]
    public partial struct CommandEnvelope
    {
        public ulong CommandId;
        public ShipCommand Command;
    }

    [SpacetimeDB.Table(Accessor = "PlayerCommandState", Public = true)]
    public partial struct PlayerCommandState
    {
        [PrimaryKey]
        public Identity Owner;
        public ulong LastProcessedCommandId;
        public bool LastAccepted;
        public byte LastRejectionCode;
        public byte LastModeCode;
    }

    [SpacetimeDB.Table(Accessor = "CommandResultEvent", Public = true, Event = true)]
    public partial struct CommandResultEvent
    {
        public Identity Owner;
        public ulong CommandId;
        public bool Accepted;
        public byte RejectionCode;
        public byte ModeCode;
        public bool IsDuplicate;
    }

#pragma warning disable STDB_UNSTABLE
    [SpacetimeDB.ClientVisibilityFilter]
    public static readonly Filter PlayerCommandStateOwnerFilter = new Filter.Sql(
        "SELECT * FROM player_command_state WHERE player_command_state.owner = :sender");

    [SpacetimeDB.ClientVisibilityFilter]
    public static readonly Filter CommandResultOwnerFilter = new Filter.Sql(
        "SELECT * FROM command_result_event WHERE command_result_event.owner = :sender");
#pragma warning restore STDB_UNSTABLE
}
