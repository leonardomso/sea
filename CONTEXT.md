# Domain model

This file records the game concepts whose meaning must stay stable across the server, Unity client, admin tools, tests, and generated bindings.

## Combat encounter

A combat encounter is one life of one NPC ship. It opens when that NPC is seeded or respawns and closes exactly once when the NPC sinks. A respawn always receives a new encounter ID, so contributions and rewards never leak between lives.

The server owns encounter state. Clients can observe only their own resulting reward rows and reward events.

## Contribution

A contribution is the work one player ship performs in one encounter. Its identity is the pair `(encounter ID, contributor ship entity ID)`. Damage, boarding, and future support credit are saturating counters. Reducers are transactional, so one indexed lookup followed by insert or update is the sole write path. Contribution rows are deleted after atomic settlement because reward history, not mutable combat work, is the reconnect-safe record.

A contributor is eligible when its score is at least 5 percent of the encounter's total score. The comparison uses exact integer arithmetic.

## Shared reward

An NPC definition supplies one gold pool and one experience pool. At encounter closure, 30 percent of each pool is divided equally among eligible contributors and 70 percent is divided in proportion to contribution. Integer remainders go by contribution rank, then ship entity ID. Settlement always conserves the configured pools and can be replayed without paying twice.

Damage grants its immediate activity XP. The NPC's configured kill XP and gold are paid only by encounter settlement. Sail-over salvage is a separate small pickup bonus and does not duplicate the encounter gold pool. Experience pools are computed and written to reward rows; they are not stored on the player, which carries only gold and map rank. The boarding counter on a contribution stays in the model and is always zero in Milestone 1, because boarding left the model with the damage pools it scaled off.

Persisted reward rows are reconnect-safe history. Owner-filtered reward events provide immediate HUD feedback.

## Progression grant

Every progression source uses the same pure grant rule. Gold saturates at its storage limit. There are no character levels; a player's `MapRank` (1 to 10) gates which map they may sail and is raised by map progression, not by XP.

## Square

The square is the map's unit of distance and the only one the design speaks in. One square is ten world units. Havenmere is twenty squares across and twenty down, so the world runs from -100 to +100 on both axes. Content ranges, speeds and radii are authored in squares; the server stores world units. The chart ruler and the coordinate a captain reads, such as `14-6`, are one-based counts of those same squares from the north-west corner, not a separate grid.

## Magazine

A ship holds a magazine of ready volleys and one reload behind them. Firing spends one volley and restarts that reload; the reload runs whether or not the ship fired, so a full magazine still snaps a volley back the tick after one leaves. A magazine untouched by combat for fifteen seconds refills outright. The magazine is the whole of a ship's rate of fire: there are no separate broadsides and no aim point.

## Armour face

Damage is resolved against the face of the target the shot lands on, read from the target's heading and the two ships' positions: front within 45 degrees of the target's bow, back beyond 135 degrees, sides between. Each face carries its own absorption, and the shot's damage is `floor(volley damage x ammunition multiplier x (1 - absorption))`. The client reads the face the same way the server does, so the HUD never shows a face the server would not use.

## Port water

Port water is the harbour a ship is inside. A ship in port may not fire and may not be fired on, and leaving is a channel — casting off — rather than an order that takes effect at once. Spawn shielding is separate and covers a ship that has just put out.

## Wreck and berth

A sunk ship stays as a row on the seabed rather than being deleted: the world carries the same number of hulls whatever happens on it. Its captain chooses a berth, and the wreck puts out again from that berth when the countdown ends. Choosing a berth is the one order a wreck is allowed to give.
