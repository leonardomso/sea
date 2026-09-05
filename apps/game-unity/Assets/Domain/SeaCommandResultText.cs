namespace Sea.Client
{
    public static class SeaCommandResultText
    {
        /// <summary>
        /// Mirrors <c>CommandRejectionCode</c> one for one. The server sends a byte and never a
        /// string, so this table is the only place the wire code becomes something a captain can
        /// read; a code with no entry is reported rather than hidden.
        /// </summary>
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
            11 => "no target selected",
            12 => "target already sunk",
            13 => "magazine reloading",
            14 => "firing too fast",
            15 => "target out of range",
            16 => "cannot fire in port",
            17 => "no repair kit",
            18 => "nothing to repair",
            19 => "no active channel",
            20 => "missing resource",
            21 => "not available yet",
            22 => "still on cooldown",
            23 => "target is spawn shielded",
            24 => "ship is not sunk",
            25 => "no way through",
            26 => "slow down",
            27 => "no crossing here",
            28 => "target is not damaged enough to board",
            29 => "target was boarded too recently",
            30 => "not enough hands to board",
            31 => "boarders have spiked the guns",
            _ => $"rejection code {code}",
        };

        /// <summary>
        /// The keys the mechanics sheet reserves for ramming, abilities and the PvP
        /// flag stay bound through Milestone 1 so the rebinder lists them; pressing one says so
        /// rather than doing nothing, which reads as a broken key.
        /// </summary>
        public static string NotAvailableYet(string feature) =>
            $"{feature} is not available yet.";
    }
}
