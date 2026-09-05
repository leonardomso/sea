# Milestone 1 validation record

Havenmere (1/1) as it was built, measured on 2026-09-04 on an Apple Silicon host
with the SpacetimeDB module in Docker (`linux/arm64`) and the Unity 6000.3.23f1
client built for macOS and WebGL. Every number below comes from a command in
this repository; where a gate is missed the miss is stated rather than the gate
moved.

## What the gates asked for

PLAN.md section 1f keeps the numeric gates of the previous Phase 18:

| Gate | Target |
| --- | --- |
| World tick p95 at 100 clients | at most 10 ms |
| World tick p99 at 100 clients | at most 20 ms |
| Command acknowledgement p95 | at most 150 ms |
| Clients connected | at least 99.9 % |
| Ships at 60 FPS, macOS | 250 |
| Ships at 60 FPS, WebGL | 100 |

## Automated gates

`pnpm verify` returns 0. It carries:

| Suite | Command | Result |
| --- | --- | --- |
| Domain and simulation | `pnpm server:test` | 683 passed, 0 failed |
| Module against a live database | `pnpm server:test:integration` | 21 passed, 0 failed |
| Client rules | `unity test --mode EditMode` | 175 passed, 0 failed |
| Identity leak | `pnpm runtime:test:identity` | passed |
| World schema | `pnpm runtime:test:world` | passed |
| Idle cost | `pnpm runtime:test:idle` | 379.84 µs per tick at 5.74 % CPU |
| Unity playmode, performance, builds, runtime scenario | `pnpm unity:verify` | passed |

`pnpm verify:full` adds the two proofs that need four clients and a hundred:
`pnpm runtime:test:shared-world` and `pnpm runtime:test:scale-isolated`.

## Presentation: 250 hulls on macOS, 100 in the browser

Both probes seed their own fleet, sail it for 180 warm-up frames and measure the
next 300. They sail alone: the ship budget on WebGL is exactly the hundred hulls
the gate asks for, so a live world would cost the probe hulls it has already
counted.

| Platform | Ships | Frame p95 | Frame p99 | Idle bytes per frame | Pools stable |
| --- | ---: | ---: | ---: | ---: | :---: |
| macOS (`Sea.app`, 1920x1080) | 250 | 3.866 ms | 4.866 ms | 0 | yes |
| WebGL (headless Chrome, 1920x1080) | 100 | 5 ms | 9 ms | 0 | yes |

Both are inside the 16.7 ms that 60 FPS allows, with no allocation on a settled
frame and no pool growth after warm-up. The browser figure reads the managed
heap rather than a per-thread allocation counter, because the browser runtime
has none; it catches a frame that leaves bytes behind, not one that allocates
and collects between two samples.

## Four captains in one world

`pnpm runtime:test:shared-world` runs two scenarios against a freshly published
database, four real SDK connections each: 2 passed, 0 failed, 3 m 25 s.

- **One conserved reward.** Four captains break the same hostile and the payout
  is split by the damage each did, summing to exactly what the hostile was
  worth.
- **Red Mary.** Four captains break Havenmere's named captain, with the two
  hulls moored beside her answering.

## A hundred clients: the tick gate is missed

`pnpm runtime:test:scale-isolated` publishes a private database, connects 100
real SDK clients, sails them, and reads `run_simulation_dispatch` out of the
module's own metrics. Two runs, the second with a ten second warm-up before
sampling:

| Measure | Run A | Run B | Gate |
| --- | ---: | ---: | ---: |
| Tick p95 | 27.68 ms | 25.64 ms | 10 ms |
| Tick p99 | 58.10 ms | 40.48 ms | 20 ms |
| Command ack p95 | 35.21 ms | 27.50 ms | 150 ms |
| Command ack p99 | 43.06 ms | 40.12 ms | -- |
| Server CPU | 1.07 % | 0.58 % | 85 % |
| Memory growth | 0 % | 0 % | 5 % |
| Failed clients | 0 | 0 | 0 |

**The tick gate is missed and the budget in `PerformanceBudget.cs` has not been
weakened, so this command exits non-zero.** Every other measure in that budget
passes with room.

`docs/performance/benchmarks.md` records the pass that took the tick from 31.40
to 20.13 ms average and why the rest of the distance is not rule cost: a host
call into the datastore costs 50 to 150 µs whatever the rule behind it does, so
a tick's price is close to the number of rows it touches. The remaining lever is
to touch fewer rows still -- one blob per chunk instead of one row per ship --
which is a replication rewrite that reaches the client's interpolation and every
test that reads a `ShipMovement` row. It is not Milestone 1 work and it is not
started here.

What this costs a player today: nothing that can be felt. The tick fires at
10 Hz, so 25 ms of work sits inside a 100 ms budget, the host is under 1 % of a
core, and a command is answered in 27.50 ms against a 150 ms gate.

## Five thousand connections: the sockets hold, the simulation does not

`scripts/test-server-scale-smoke.sh` was pointed at 5,000 clients against an
isolated database. The run needed two fixes before it produced a number at all:
the ramp had to fit inside the sail window, and the control-plane reads had to
stop timing out. A `SELECT` that returns a row per client is slower the more
clients there are, and those reads ran under `curl --max-time 3` and
`--max-time 5` inside a `set -e` script, so the harness killed its own run at
around four thousand connections and reported nothing. They now honour
`SEA_CONTROL_PLANE_TIMEOUT` (30 s by default) and treat a timeout as a missing
sample rather than as a verdict.

With that fixed, the run reached:

| Measure | Result | Gate |
| --- | ---: | ---: |
| Clients connected | 5000 / 5000 (100 %) | at least 99.9 % |
| Connection failures | 0 | 0 |
| Server CPU | 23.90 % | at most 85 % |
| Ships kept sailing | 1357 / 5000 | -- |
| Command failures | 6929 | -- |

**The connection half of the gate passes; the simulation half does not.** Five
thousand sockets subscribe and stay subscribed on a quarter of one core, which
is what the gate actually asks for. Asking all five thousand of them to sail at
once is a different load, and it is the load the server cannot serve: three in
four `set_course` commands were rejected or dropped, and only 1,357 hulls were
moving when the sample was taken.

This is the same ceiling the hundred-client run measures, seen from the other
end. A tick costs roughly what the rows it touches cost, and a sailing ship is
rows every tick where an idle one is none. The fix is the same per-chunk blob
rewrite named above, and it is equally not started.

Command acknowledgement latency at 5,000 clients was **not** captured: the run
aborts on its readiness check before the latency sample, and that check is a
count of moving ships. That number is owed and it is not in this document.

## Still owed

- **The 5,000 client acknowledgement latency.** The connection proof is above;
  the latency half of it is not, because the run stops at a readiness check the
  server cannot satisfy.
- **The played session.** The 10 to 15 minute playtest in PLAN.md section 1f is
  the user's, not the machine's: sailing, steering, selecting, firing at every
  bearing with each ammunition, repair and its cancel, port rules, sinking,
  respawn, and every hostile on the map, each finding recorded pass, fail, or
  deferred.
- **The two review passes.** `/thermo-nuclear-code-quality-review` and
  `/improve-codebase-architecture` over `532d0d7..HEAD`; both refuse to run
  except when the user invokes them.
