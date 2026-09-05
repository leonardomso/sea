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

Click-to-sail is the only ship control. WASD and middle-mouse drag move the
chart, and the chart stays where you put it until `Home` or the HUD recenter
button brings it back to the ship.

| Input | Action |
|---|---|
| Left-click water | Set or replace course |
| Left-click a ship | Select it as the target |
| Right-click | Stop sailing |
| Q or Space | Fire at the selected target |
| Tab / Shift+Tab | Next / previous enemy |
| Escape | Clear the target, then open the local menu |
| 1 / 2 / 3 / 4 | Round / Chain / Grapeshot / Incendiary |
| R | Start or cancel the repair channel |
| K | Use a repair kit |
| N | Open the coordinate navigator |
| WASD / middle-mouse drag | Pan the chart |
| Mouse wheel | Zoom |
| Home | Recenter the chart on the player ship |
| E / F / P | Board / ram / PvP flag: bound, answer "not available yet" |
| Ability keys | Bound, answer "not available yet" |

Every binding uses the Unity Input System and can be rebound at runtime. The
keys that answer "not available yet" stay bound on purpose so they appear in
the rebinder; they start working in Milestones 2 and 3. See `docs/STATUS.md`.

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

The generated main scene connects to `http://127.0.0.1:43000`, database
`sea-local`. Start Docker and publish or reset the module before runtime tests.
The macOS player is written to `Build/Sea.app`; WebGL output is written to
`Build/WebGL`.

Apricum and future owned models belong behind Addressables. Import validation
must catch missing textures, materials, wrong orientation, scale, bounds, and
broken references before a build is accepted.
