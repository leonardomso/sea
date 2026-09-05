# Sea — current state

Updated: 2026-09-05. This file answers one question: what works today, what is
half-built, and what has not been started. `PLAN.md` says what we intend to
build and in what order. `docs/validation/milestone-1.md` holds the measured
numbers behind the claims here.

Short version: **Milestone 1 is code-complete, and the SEA_5 physics rewrite has
landed on top of it.** Three maps are seeded and sailable; Havenmere (1/1) is the
one with enemies on it. Boarding, the map border crossings and the trust score
arrived with the physics pass, ahead of the milestones that had planned them.
Milestones 2 to 5 have not otherwise been started. Two performance gates are
missed and written down as missed.

## 1. Done and working

### The world

- Three maps, seeded and sailable: Havenmere (1/1), Gull Rocks (1/2), and Brine
  Fields (1/3). Each is four hundred squares by four hundred, with (0, 0) at the
  top-left corner.
- A square is the unit. There is no world unit and no conversion: every position,
  range and speed on the server is squares, and the client converts once, at the
  boundary, to draw. Chunks are fifty squares.
- Islands, reefs, shoals, current zones, one wind that changes over time, and
  storms that move.
- A hull that sails into the band along a border is held there and offered the
  crossing; taking it is a command of its own. She comes out of the new chart at
  the point along its edge she left the old one at, still pointing the way she
  came in, and without her course, her target, or anything that was stuck to her.
  Havenmere opens east into Gull Rocks; Gull Rocks west back to Havenmere and
  east into Brine Fields.
- The server simulates the world at 10 Hz. NPC decisions run at 2 Hz. Movement
  is split across eight shards so a tick only pays for ships that are sailing,
  and ships are replicated one packed blob per chunk rather than one row each.
- Clients only subscribe to the part of the world near them.

### Sailing

- Click the water to sail there. This is the only way to steer a ship.
- The server plots a course around islands and reefs with A* on the one-square
  land grid, up to thirty-two waypoints, and refuses a course it cannot reach at
  all rather than sailing part of the way to it.
- There is no acceleration, no braking curve and no turning circle. A ship walks
  a fixed distance along her course each tick, corner to corner, in straight
  lines, so a course takes exactly its own length over her speed however it
  bends. Heading is an output, never an input: a mark astern of her is answered
  on the same tick, making way astern.
- She counts as arrived within 0.15 squares of her last mark.
- A captain may lay at most eight courses a second; the ninth is refused rather
  than queued.
- A hull too deep in the draught to cross a shoal is routed around it rather than
  stopped at the edge of one she was sent into.
- Ships pass through each other. Only land blocks them.
- The camera stays where you push it (WASD, middle-mouse drag, or the mini-map)
  until `Home` or the recenter button brings it back to the ship.

### Combat

- Pick a target, then fire. The guns bear in every direction; there are no
  broadside arcs and no aim point.
- Range is the cannon's, from 18 squares for a first-rate gun to 30 for a
  fifth, with half a square of grace at the edge and a range bonus that is
  added before the ten percent cap rather than after it.
- One shot in ten is a critical, worth half again, decided by a seeded hash of
  the shot rather than by a random number, so a replay resolves the same shot
  the same way.
- A ship holds a magazine of ready volleys with one reload always running
  behind them. A magazine left alone for fifteen seconds refills.
- Damage is resolved against the face of the target the shot hits: front,
  side, or back. Each face has its own armour. There is one HP pool.
- Four kinds of ammunition are seeded: Round, Chain, Grapeshot, Incendiary.
  Their effects (slow, slower reload, burn) are applied by the server.
- Repair is a channel that can be cancelled. The repair kit is a separate
  instant item on its own cooldown.
- Boarding: a hostile inside four squares and under half her hull can be
  hooked, the roll weighted by tier and by the hands each side still has. A
  win takes hull and hands off the loser and pays a loot multiplier; a loss
  costs the attacker hands and silences her for three seconds. Both sides carry
  cooldowns, and a hull just boarded is immune for five minutes.
- Port water refuses fire in both directions, clears effects, and leaving is a
  cast-off channel rather than an instant order.
- A sunk ship stays on the seabed as a wreck, its captain picks a berth, and
  it puts out again from its home port with a spawn shield.
- Every ship carries a trust score of 0 to 100. Dropped moves, metronomic
  command timing, and volleys fired at the exact edge of range all take it
  down; it recovers on its own over time.

### Enemies

- Fifteen hostiles hold Havenmere: twelve patrol slots, with a veteran on every
  fifth sail, plus Red Mary and the two hulls moored beside her.
- Four enemy definitions are seeded: Skiff, Reef Crab, Fancy, Red Mary.

### Rewards and progression

- Every NPC life is one combat encounter. Contributions are counted per
  player, and the reward is settled once when the NPC sinks.
- A player carries gold and a Map Rank of 1 to 10. There are no character
  levels and no XP on the player.
- Loot drops can be sailed over and picked up.

### Content

- All game content lives in JSON under
  `server/spacetimedb/spacetimedb/Content/Data/`: maps, hulls, cannons, ammo,
  NPCs, and stat caps. Five hulls (Skiff through Galleon) and five cannons
  (Iron Cannon through Basilisk) are seeded, one per tier, which is enough to
  make the stat pipeline real.
- `ContentCatalog.g.cs` is generated from that JSON. Never edit it by hand.

### Client

- Unity 6 client for macOS and WebGL, drawing the owned Apricum ship model.
- HUD shows the magazine, the reload, the repair cooldown, the target, the
  wind, and a coordinate ruler: a forty by forty lettered and numbered grid
  laid over the four hundred square chart, ten squares to a cell.
- Every key can be rebound at runtime. The defaults:

  | Key | Does |
  | --- | --- |
  | Left click | Sail there, or select what you clicked |
  | Right click | Stop |
  | `Q` or `Space` | Fire at the selected target |
  | `Tab` / `Shift+Tab` | Next / previous enemy |
  | `Esc` | Clear the target, then open the menu |
  | `1` to `4` | Round, Chain, Grapeshot, Incendiary |
  | `R` | Repair channel |
  | `K` | Repair kit |
  | `N` | Coordinate navigator |
  | `Home` | Recenter the chart on your ship |
  | WASD, middle-mouse drag, mini-map | Move the chart |
  | `E` | Throw hooks at the selected target |
  | `F`, `P`, ability keys | Bound, answer "not available yet" |
- Ship presentation is pooled and capped: 250 ships on macOS, 100 on WebGL.
- While the window is focused the player draws in step with the display rather
  than at a fixed 60 frames a second, so motion is even on a 120Hz screen.
  Unfocused it drops to 15 frames a second. Measured with 250 ships on screen:
  p95 5.9 ms a frame, p99 6.1 ms, nothing allocated per frame.

### Commands and safety

- Clients send one command type, `IssueShipCommand`, with a command id. The
  server answers with a typed result. A rejected command never throws.
- Players and NPCs go through the same command policy and the same effect
  executor.

## 2. Partly done

| Thing | Where it stands | Finished in |
| --- | --- | --- |
| Maps 1/2 and 1/3 | Gull Rocks and Brine Fields are seeded, sailable, and reachable through the border bands. Neither has any enemies on it yet, so there is nothing to fight once you arrive. | Milestone 2 |
| Ramming and ship-to-ship collision | Key bound, answers `NotAvailable`. Ships pass through each other until ramming exists. | Milestone 3 |
| Abilities | Four keys bound, all answer `NotAvailable`. The old four abilities were removed with the damage pools they scaled off. | Milestone 2 |
| PvP flag | Key bound, answers `NotAvailable`. | Milestone 2 |
| Reward split | The code still splits 30 percent equally and 70 percent by contribution, with a 5 percent eligibility floor. The design wants a pure damage share. | Milestone 2 |
| Ammunition | Four of the design's seven types are seeded. Frost, Blessed, and Heavy arrive with the maps that use them. | Milestone 4 |
| Admin panel | Reads the SpacetimeDB SQL endpoint. It cannot write anything. | Milestone 5 |
| Identity | Anonymous local identity. A private `PlayerAccount` table already carries an account column so a real login can attach later without a schema reset. | Milestone 5 |
| Server tick under load | See section 4. Correct, but slower than the gate allows. | Not scheduled |

## 3. Not started

Nothing below exists in any form.

- **Milestone 2, progress and safety.** Spawn points, damage-share rewards,
  daily earning caps, missions and the mission board, charts, the enemies that
  belong on maps 1/2 and 1/3, the Cannons and Armor skill trees, crew hiring,
  harbour protection, the PvP flag, duels, kill records, combat rating, and the
  honor ledger.
- **Milestone 3, competition and trust.** Arena and matchmaking, ban waves,
  phantom NPCs, the Sails, Repair, and Plunder trees, ship configurations,
  ramming, collision, parties, and the ping wheel.
- **Milestone 4, guilds and world.** Guilds, renown, island and tower
  ownership, war windows, alliances, and maps 2/1 through 5/1.
- **Milestone 5, money and operations.** Real accounts, payments, diamonds and
  cosmetics, the Sea Pass, market and auction, admin writes, a replay viewer,
  and any cloud deployment. Everything today is local only.

Three things planned for later milestones arrived with the SEA_5 physics pass
instead, and are recorded as done in section 1: boarding and the map border
crossings, which Milestone 3 and Milestone 2 respectively had been holding, and
the trust score, which Milestone 3 had. Maps 1/2 and 1/3 came with the crossings
and are half here: the water exists, the enemies do not.

## 4. Known problems

Both are measured, both are written down, and neither has been hidden by
lowering a gate. **Every number in this section was measured on 2026-09-04,
before the chunk-blob replication rewrite landed. They are the last numbers we
actually have, not a reading of the code as it stands today.**

**The world tick was too slow at a hundred clients.** The gate asks for p95 at
or under 10 ms. The last run measured 37.7 ms, and p99 56.8 ms against a 20 ms
gate. `PerformanceBudget.cs` still holds the original numbers, so `pnpm
runtime:test:scale-isolated` exits non-zero on purpose. It was 25.6 ms before
the sailing pass: every hull is now integrated on the tick it moves rather than
on every second tick, which doubled the shard rows a tick writes. The trade was
taken on purpose, because the old way published a hull's position up to 200 ms
after it happened and a captain feels that on every click.

Nothing about this was felt by a player: the tick fires every 100 ms, so 38 ms
of work still fits, the host stayed under 2 percent of a core, and a command was
answered in about 40 ms against a 150 ms gate. It is measured in
`docs/performance/benchmarks.md` down to which phase spends what, and the answer
was not a constant anyone can tune -- it was that a movement shard carried every
hull it sails in one blob, so moving anything rewrote the whole blob.

**Five thousand ships could not sail at once.** Five thousand clients connect
and stay connected with no failures on about a quarter of one core, which is
what the gate asks. Asking all five thousand to sail was a different load: only
about 1,357 hulls kept moving and roughly seven thousand commands were rejected.

Both had the same cause: a tick costs about what the rows it touches cost, and a
sailing ship wrote rows every tick where an idle one wrote none. The fix named
here -- replicate one blob per chunk instead of one row per ship -- has since
been built and landed (`368f121`, `81b42e5`). **It has not been measured.** The
re-baseline run was started and stopped before it reached
`runtime:test:scale-isolated`, so nobody yet knows whether the rewrite moved
these two numbers, by how much, or in which direction. Until that run is taken,
treat both gates as still missed by the figures above.

## 5. Still owed by hand

- **A played session.** Ten to fifteen minutes covering sailing, steering,
  selecting, firing at every bearing with each ammunition, repair and its
  cancel, port rules, sinking, respawn, and every enemy on the map. Each
  finding recorded as pass, fail, or deferred.
- **Two review passes** over `532d0d7..HEAD`:
  `/thermo-nuclear-code-quality-review` and `/improve-codebase-architecture`.
  Both refuse to run unless a person invokes them.
- **Acknowledgement latency at 5,000 clients.** The run stops at a readiness
  check the server cannot satisfy, so this number does not exist yet.
- **The performance re-baseline after the chunk-blob rewrite.** `pnpm
  verify:full` has to be run to the end and the tick and scale numbers written
  into `docs/performance/benchmarks.md` beside the old ones. Section 4 is stale
  until it is.

## 6. Checking this yourself

```sh
pnpm ci:fast        # static checks and repository invariants, no Docker
pnpm server:test    # the server unit, property, and replay tests
pnpm verify         # the normal gate: real module, live integration suite, Unity tests, both builds
pnpm verify:full    # verify, plus the four-client world and the 100-client scale run
```

`pnpm ci:fast` is the cheap gate and it is cheap because it leaves things out:
it does not compile the Unity assembly, run the C# suites, check `dotnet
format`, check the generated bindings, or run the integration suite. Passing it
is not evidence that the module builds. Use `pnpm verify` for that.

`pnpm verify` passes. `pnpm verify:full` fails at the last step, for the reason
in section 4.
