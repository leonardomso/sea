namespace Sea.Client
{
    public static class SeaCommandResultText
    {
        public static string Rejection(byte code) => code switch
        {
            1 => "stale command",
            2 => "player not loaded",
            3 => "ship is busy",
            4 => "ship is sunk",
            5 => "invalid course",
            6 => "destination blocked",
            7 => "invalid target",
            8 => "players cannot be attacked",
            9 => "target concealed",
            10 => "unknown ammunition",
            11 => "ammunition not owned",
            12 => "no target selected",
            13 => "target already sunk",
            14 => "cannons disabled",
            15 => "out of ammunition",
            16 => "weapons reloading",
            17 => "target out of range",
            18 => "target outside firing arc",
            19 => "unknown ability",
            20 => "ability cooling down",
            21 => "no repair kit",
            22 => "nothing to repair",
            23 => "target too strong to board",
            24 => "no active channel",
            25 => "missing resource",
            26 => "invalid broadside side",
            27 => "invalid weak point",
            _ => $"rejection code {code}",
        };
    }
}
