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

Damage and successful boarding still grant their immediate activity XP. The NPC's configured kill XP and gold are paid only by encounter settlement. Sail-over salvage is a separate small pickup bonus and does not duplicate the encounter gold pool. Experience pools are still computed and written to reward rows; they are not stored on the player and are removed in sub-phase 1b.

Persisted reward rows are reconnect-safe history. Owner-filtered reward events provide immediate HUD feedback.

## Progression grant

Every progression source uses the same pure grant rule. Gold saturates at its storage limit. There are no character levels; a player's `MapRank` (1 to 10) gates which map they may sail and is raised by map progression, not by XP.
