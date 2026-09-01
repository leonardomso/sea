# Unity client

The client targets Unity `6000.3.23f1` on Apple Silicon with URP and an
orthographic 2.5D presentation. It supports macOS and WebGL. SpacetimeDB owns
gameplay state; Unity sends commands, receives scoped row/event updates, and
interpolates the 10 Hz simulation at 60 FPS.

## Runtime structure

Assembly definitions separate domain-facing models, networking, input,
presentation, UI, editor tools, and tests. VContainer creates one application
lifetime scope. MonoBehaviours are thin scene or rendering adapters.

Client state is maintained from SpacetimeDB insert, update, delete, and event
callbacks. Per-frame code is limited to input, camera movement, interpolation,
visibility, and active effects. Repeated ships, bars, target rings, projectiles,
smoke, impacts, status icons, and loot use pools.

Generated bindings live in `Assets/Generated/SpacetimeDB` and must not be
edited by hand.

## Controls

| Input | Action |
|---|---|
| Left-click water | Set or replace course |
| Right-click water | Stop sailing |
| Left-click NPC | Select target |
| WASD | Pan the chart |
| Mouse wheel | Zoom |
| Space | Recenter on the player ship |
| Tab / Shift+Tab | Cycle NPC targets |
| T | Clear target |
| Q / E | Fire port / starboard broadside |
| 1 / 2 / 3 | Aim at hull / sails / cannons |
| 4 / 5 / 6 / 7 | Select ammunition |
| Z / X / C / V | Full Sail / Brace / Emergency Pump / Smoke Screen |
| R | Start or cancel repair |
| B | Start or cancel boarding |
| N | Open the coordinate navigator |
| Escape | Open the local menu |

Gameplay bindings use the Unity Input System and remain rebindable.

## Build and test

```sh
pnpm unity:scene
pnpm unity:test
pnpm unity:test:playmode
pnpm unity:test:performance
pnpm unity:test:runtime
pnpm unity:build:macos
pnpm unity:build:webgl
```

The generated main scene connects to `http://127.0.0.1:3000`, database
`sea-local`. Start Docker and publish or reset the module before runtime tests.
The macOS player is written to `Build/Sea.app`; WebGL output is written to
`Build/WebGL`.

Apricum and future owned models belong behind Addressables. Import validation
must catch missing textures, materials, wrong orientation, scale, bounds, and
broken references before a build is accepted.
