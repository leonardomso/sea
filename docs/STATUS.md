# Sea — current state

Updated: 2026-09-04. This file answers one question: what works today, what is
half-built, and what has not been started. `PLAN.md` says what we intend to
build and in what order. `docs/validation/milestone-1.md` holds the measured
numbers behind the claims here.

Short version: **Milestone 1 is code-complete and measured.** One map,
Havenmere (1/1), can be sailed and fought in. Milestones 2 to 5 have not been
started. Two performance gates are missed and written down as missed.

## 1. Done and working

### The world

- One map, Havenmere (1/1). Twenty squares by twenty. One square is ten world
  units, so the world runs from -100 to +100 on both axes.
- Islands, reefs, shoals, current zones, one wind that changes over time, and
  storms that move.
- The server simulates the world at 10 Hz. NPC decisions run at 2 Hz. Movement
  is split across eight shards so a tick only pays for ships that are sailing.
- Clients only subscribe to the part of the world near them.

### Sailing

- Click the water to sail there. This is the only way to steer a ship.
- A ship stops where you clicked and stays stopped. She counts as arrived once
  she is within 1.5 units of the mark, however she is pointing, so a click just
  off the bow no longer puts her into a circle she orbits forever.
- A ship stops from full speed in 10 units and turns in a circle 9 units wide,
  both inside one chart square (10 units), so a click is answered inside the
  square you clicked. The worst course on the whole chart takes 7.9 seconds.
- The server plots a course around islands and reefs, and refuses a course that
  ends on one.
- Ships pass through each other. Only land blocks them.
- The camera stays where you push it (WASD, middle-mouse drag, or the mini-map)
  until `Home` or the recenter button brings it back to the ship.

### Combat

- Pick a target, then fire. The guns bear in every direction; there are no
  broadside arcs and no aim point.
- A ship holds a magazine of ready volleys with one reload always running
  behind them. A magazine left alone for fifteen seconds refills.
- Damage is resolved against the face of the target the shot hits: front,
  side, or back. Each face has its own armour. There is one HP pool.
- Four kinds of ammunition are seeded: Round, Chain, Grapeshot, Incendiary.
  Their effects (slow, slower reload, burn) are applied by the server.
- Repair is a channel that can be cancelled. The repair kit is a separate
  instant item on its own cooldown.
- Port water refuses fire in both directions, clears effects, and leaving is a
  cast-off channel rather than an instant order.
- A sunk ship stays on the seabed as a wreck, its captain picks a berth, and
  it puts out again from Port Lowell with a spawn shield.

### Enemies

- Fifteen hostiles hold the map: twelve patrol slots, with a veteran on every
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
  NPCs, and stat caps. One hull (Sloop) and one cannon (Iron Cannon) are
  seeded, which is enough to make the stat pipeline real.
- `ContentCatalog.g.cs` is generated from that JSON. Never edit it by hand.

### Client

- Unity 6 client for macOS and WebGL, drawing the owned Apricum ship model.
- HUD shows the magazine, the reload, the repair cooldown, the target, the
  wind, and a coordinate ruler in squares 1 to 20.
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
  | `E`, `F`, `P`, ability keys | Bound, answer "not available yet" |
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
| Boarding | The key is bound and appears in the rebinder. Pressing it answers `NotAvailable`. The contribution row keeps a boarding counter that is always zero. | Milestone 3 |
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
  daily earning caps, missions and the mission board, charts, map edge exits,
  maps 1/2 (Gull Rocks) and 1/3 (Brine Fields), the Cannons and Armor skill
  trees, crew hiring, harbour protection, the PvP flag, duels, kill records,
  combat rating, and the honor ledger.
- **Milestone 3, competition and trust.** Arena and matchmaking, trust score
  and ban waves, phantom NPCs, the Sails, Repair, and Plunder trees, ship
  configurations, boarding, ramming, collision, parties, and the ping wheel.
- **Milestone 4, guilds and world.** Guilds, renown, island and tower
  ownership, war windows, alliances, and maps 2/1 through 5/1.
- **Milestone 5, money and operations.** Real accounts, payments, diamonds and
  cosmetics, the Sea Pass, market and auction, admin writes, a replay viewer,
  and any cloud deployment. Everything today is local only.

## 4. Known problems

Both are measured, both are written down, and neither has been hidden by
lowering a gate.

**The world tick is too slow at a hundred clients.** The gate asks for p95 at
or under 10 ms. We measure 25.6 ms. `PerformanceBudget.cs` still holds the
original numbers, so `pnpm runtime:test:scale-isolated` exits non-zero on
purpose. Nothing about this is felt by a player today: the tick fires every
100 ms, so 25 ms of work fits easily, the host stays under 1 percent of a core,
and a command is answered in about 27 ms against a 150 ms gate.

**Five thousand ships cannot sail at once.** Five thousand clients connect and
stay connected with no failures on about a quarter of one core, which is what
the gate asks. Asking all five thousand to sail is a different load: only about
1,357 hulls kept moving and roughly seven thousand commands were rejected.

Both have the same cause. A tick costs about what the rows it touches cost, and
a sailing ship writes rows every tick where an idle one writes none. The known
fix is to replicate one blob per chunk instead of one row per ship. That is a
rewrite that reaches the client's interpolation and every test that reads a
`ShipMovement` row, so it is not Milestone 1 work and it has not been started.

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

## 6. Checking this yourself

```sh
pnpm ci:fast        # static checks and repository invariants, no Docker
pnpm server:test    # 683 server unit, property, and replay tests
pnpm verify         # the normal gate: real module, Unity tests, both builds
pnpm verify:full    # verify, plus the four-client world and the 100-client scale run
```

`pnpm verify` passes. `pnpm verify:full` fails at the last step, for the reason
in section 4.
