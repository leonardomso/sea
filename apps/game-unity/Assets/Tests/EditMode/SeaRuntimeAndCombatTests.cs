#if UNITY_EDITOR
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sea.Client;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Sea.Tests
{
    public sealed partial class SeaProjectTests
    {
        [Test]
        public void Client_chart_coordinates_match_the_server_contract()
        {
            // The client's grid is the server's grid: forty ruler cells of ten squares on a
            // side, columns lettered from A and rows spoken from 1, counted from the
            // north-west corner, with no flip against the world's own top-left-origin,
            // south-growing y axis. Indices are zero-based on both sides; only the spoken
            // row number is offset.
            Assert.That(SeaChartCoordinates.TryCellCenter("N6", out var center), Is.True);
            Assert.That(center.Column, Is.EqualTo(13));
            Assert.That(center.Row, Is.EqualTo(5));
            Assert.That(center.X, Is.EqualTo(135f).Within(0.001f));
            Assert.That(center.Y, Is.EqualTo(55f).Within(0.001f));
            Assert.That(SeaChartCoordinates.LabelAt(center.X, center.Y), Is.EqualTo("N6"));
            Assert.That(SeaChartCoordinates.LabelAt(0.1f, 0.1f), Is.EqualTo("A1"));
            Assert.That(SeaChartCoordinates.LabelAt(399.9f, 399.9f), Is.EqualTo("AN40"));
            Assert.That(SeaChartCoordinates.TryCellCenter("AO1", out _), Is.False);
            Assert.That(SeaChartCoordinates.TryCellCenter("A0", out _), Is.False);
        }

        [Test]
        public void Chart_camera_rules_clamp_zoom_and_do_not_issue_ship_commands()
        {
            Assert.That(SeaChartCameraRules.ClampZoom(5f), Is.EqualTo(12f));
            Assert.That(SeaChartCameraRules.ClampZoom(100f), Is.EqualTo(45f));
            Assert.That(SeaChartCameraRules.PanDelta(1f, -1f, 20f, 0.5f),
                Is.EqualTo(new Vector3(10f, 0f, -10f)));
        }

        [Test]
        public void Gameplay_input_map_exposes_every_locked_navigation_and_combat_command()
        {
            var controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/Input/SeaControls.inputactions");

            Assert.That(controls, Is.Not.Null);
            var gameplay = controls.FindActionMap("Gameplay", throwIfNotFound: true);
            var requiredActions = new[]
            {
                "Point", "SetCourse", "StopCourse", "PanChart", "ZoomChart", "DragChart", "RecenterChart",
                "OpenNavigator", "CycleTargetNext", "CycleTargetPrevious", "ClearTarget",
                "Fire", "AmmoRound", "AmmoChain", "AmmoGrapeshot", "AmmoIncendiary", "Repair",
                "RepairKit", "Board", "Ram", "Ability1", "Ability2", "Ability3", "Ability4",
                "PvpFlag",
            };

            Assert.That(gameplay.actions.Select(action => action.name), Is.EquivalentTo(requiredActions));
            Assert.That(controls.FindActionMap("Menu", throwIfNotFound: true), Is.Not.Null);
        }

        /// <summary>
        /// Mechanics section 1.1 is the contract a captain learns once: left click sails, Q or
        /// Space fires, Tab takes a target, Escape lets it go, R repairs and 1 to 4 load the
        /// racks. There is no steering key and no full-speed key, because there is no steering.
        /// </summary>
        [Test]
        public void Default_bindings_are_the_ones_the_mechanics_sheet_promises()
        {
            var gameplay = AssetDatabase
                .LoadAssetAtPath<InputActionAsset>("Assets/Input/SeaControls.inputactions")
                .FindActionMap("Gameplay", throwIfNotFound: true);

            var expected = new (string Action, string[] Paths)[]
            {
                ("SetCourse", new[] { "<Mouse>/leftButton" }),
                ("StopCourse", new[] { "<Mouse>/rightButton" }),
                ("DragChart", new[] { "<Mouse>/middleButton" }),
                ("RecenterChart", new[] { "<Keyboard>/home" }),
                ("CycleTargetNext", new[] { "<Keyboard>/tab" }),
                ("ClearTarget", new[] { "<Keyboard>/escape" }),
                ("Fire", new[] { "<Keyboard>/q", "<Keyboard>/space" }),
                ("AmmoRound", new[] { "<Keyboard>/1" }),
                ("AmmoChain", new[] { "<Keyboard>/2" }),
                ("AmmoGrapeshot", new[] { "<Keyboard>/3" }),
                ("AmmoIncendiary", new[] { "<Keyboard>/4" }),
                ("Repair", new[] { "<Keyboard>/r" }),
                ("Board", new[] { "<Keyboard>/e" }),
                ("Ram", new[] { "<Keyboard>/f" }),
                ("PvpFlag", new[] { "<Keyboard>/p" }),
            };

            foreach (var (name, paths) in expected)
            {
                var action = gameplay.FindAction(name, throwIfNotFound: true);
                Assert.That(
                    action.bindings.Where(binding => !binding.isComposite)
                        .Select(binding => binding.path),
                    Is.EquivalentTo(paths),
                    name);
            }

            // Steering is the server's business; the keyboard only pans the chart.
            var wasd = gameplay.FindAction("PanChart", throwIfNotFound: true).bindings
                .Select(binding => binding.path);
            Assert.That(wasd, Contains.Item("<Keyboard>/w"));
            Assert.That(wasd, Contains.Item("<Keyboard>/s"));
        }

        [Test]
        public void One_authoritative_command_binding_replaces_gameplay_reducers()
        {
            Assert.That(File.Exists(
                "Assets/Generated/SpacetimeDB/Reducers/IssueShipCommand.g.cs"), Is.True);
            Assert.That(File.Exists(
                "Assets/Generated/SpacetimeDB/Reducers/Engage.g.cs"), Is.False);
            Assert.That(File.Exists(
                "Assets/Generated/SpacetimeDB/Reducers/MoveTo.g.cs"), Is.False);
            Assert.That(File.Exists(
                "Assets/Generated/SpacetimeDB/Reducers/FireBroadside.g.cs"), Is.False);
            Assert.That(File.Exists(
                "Assets/Generated/SpacetimeDB/Types/FireBroadsideCommand.g.cs"), Is.False);
        }

        [Test]
        public void Gameplay_commands_are_generated_as_one_typed_union()
        {
            var commands = new[]
            {
                "SetCourseCommand.g.cs", "StopCourseCommand.g.cs",
                "SelectTargetCommand.g.cs", "ClearTargetCommand.g.cs",
                "SetAmmoCommand.g.cs", "FireCommand.g.cs",
                "ActivateAbilityCommand.g.cs", "StartRepairCommand.g.cs",
                "StartBoardingCommand.g.cs", "CancelChannelCommand.g.cs",
                "UseRepairKitCommand.g.cs", "ChooseRespawnCommand.g.cs",
            };

            Assert.That(commands.All(file => File.Exists(
                $"Assets/Generated/SpacetimeDB/Types/{file}")), Is.True);
        }

        [Test]
        public void Command_rejections_have_stable_player_facing_text()
        {
            Assert.That(SeaCommandResultText.Rejection(1), Is.EqualTo("stale command"));
            Assert.That(SeaCommandResultText.Rejection(6), Is.EqualTo("destination blocked"));
            Assert.That(SeaCommandResultText.Rejection(13),
                Is.EqualTo("magazine reloading"));
            Assert.That(SeaCommandResultText.Rejection(21), Is.EqualTo("not available yet"));
            Assert.That(SeaCommandResultText.Rejection(255), Is.EqualTo("rejection code 255"));
        }

        [Test]
        public void Primitives_share_unity_meshes_without_colliders()
        {
            foreach (var type in new[]
            {
                PrimitiveType.Sphere,
                PrimitiveType.Cube,
                PrimitiveType.Cylinder,
                PrimitiveType.Plane,
                PrimitiveType.Quad,
                PrimitiveType.Capsule,
            })
            {
                var primitive = SeaPrimitive.Create(type, type.ToString(), null);
                var reference = GameObject.CreatePrimitive(type);

                Assert.That(
                    primitive.GetComponent<MeshFilter>().sharedMesh,
                    Is.SameAs(reference.GetComponent<MeshFilter>().sharedMesh),
                    type.ToString());
                Assert.That(primitive.GetComponent<Collider>(), Is.Null);
                Object.DestroyImmediate(primitive);
                Object.DestroyImmediate(reference);
            }
        }

        [Test]
        public void Shoals_and_storms_have_distinct_chart_geometry()
        {
            var shallows = SeaMaterialFactory.CreateTransparent(new Color(0.2f, 0.8f, 0.7f, 0.35f));
            var storm = SeaMaterialFactory.CreateTransparent(new Color(0.12f, 0.16f, 0.2f, 0.7f));
            var shoal = SeaWorldGeometryFactory.CreateShoal(
                "Test Shoal", Vector3.zero, 10f, shallows);
            var cloud = SeaWorldGeometryFactory.CreateStorm(
                "Test Storm", Vector3.zero, 10f, storm);

            Assert.That(shoal.transform.Find("Shoal Water"), Is.Not.Null);
            Assert.That(cloud.GetComponentsInChildren<Renderer>(), Has.Length.GreaterThanOrEqualTo(5));
            Assert.That(shoal.GetComponentsInChildren<Collider>(), Is.Empty);
            Assert.That(cloud.GetComponentsInChildren<Collider>(), Is.Empty);
            Object.DestroyImmediate(shoal);
            Object.DestroyImmediate(cloud);
            Object.DestroyImmediate(shallows);
            Object.DestroyImmediate(storm);
        }

        [Theory]
        [TestCase(10ul, 60ul, 10ul, 0f)]
        [TestCase(10ul, 60ul, 35ul, 0.5f)]
        [TestCase(10ul, 60ul, 60ul, 1f)]
        public void Tactical_channel_progress_uses_authoritative_ticks(
            ulong startedAtTick,
            ulong completesAtTick,
            ulong currentTick,
            float expected)
        {
            Assert.That(SeaTacticalPresentationRules.ChannelProgress(
                startedAtTick,
                completesAtTick,
                currentTick), Is.EqualTo(expected).Within(0.001f));
        }

        [Theory]
        [TestCase(5ul, 10ul, 5ul, 0f)]
        [TestCase(5ul, 10ul, 7ul, 0.4f)]
        [TestCase(5ul, 10ul, 10ul, 1f)]
        [TestCase(5ul, 10ul, 20ul, 1f)]
        public void Volley_presentation_uses_authoritative_launch_and_impact_ticks(
            ulong firedAtTick,
            ulong impactAtTick,
            ulong currentTick,
            float expected)
        {
            Assert.That(SeaVolleyPresentationRules.Progress(
                firedAtTick,
                impactAtTick,
                currentTick), Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void Muzzle_smoke_follows_the_bearing_to_the_target()
        {
            // The offset is local to the firing ship, so a target dead ahead puts the smoke on
            // the bow whatever the ship is heading; a beam target puts it out on that side.
            var ahead = SeaVolleyPresentationRules.LocalMuzzleOffset(
                90f, Vector2.zero, Vector2.right * 10f, 3f);
            Assert.That(ahead.z, Is.EqualTo(3f).Within(0.001f));
            Assert.That(ahead.x, Is.EqualTo(0f).Within(0.001f));

            var starboard = SeaVolleyPresentationRules.LocalMuzzleOffset(
                0f, Vector2.zero, Vector2.right * 10f, 3f);
            Assert.That(starboard.x, Is.EqualTo(3f).Within(0.001f));

            var port = SeaVolleyPresentationRules.LocalMuzzleOffset(
                0f, Vector2.zero, Vector2.left * 10f, 3f);
            Assert.That(port.x, Is.EqualTo(-3f).Within(0.001f));
        }

        [Test]
        public void The_hud_names_the_same_armour_face_the_server_charges_for()
        {
            // CombatRules.FaceHit: 45 degrees of bow, 45 of stern, the rest is beam.
            //
            // These are chart positions, where y grows south, so a shooter due north of the
            // target sits at NEGATIVE y and Vector2.up is astern. The two cases below read
            // upside down for that reason and used to be written the other way round, which
            // passed only while heading 0 sailed south.
            var north = new Vector2(0f, -10f);
            var south = new Vector2(0f, 10f);

            Assert.That(SeaVolleyPresentationRules.ArmorFaceAt(
                0f, Vector2.zero, north), Is.EqualTo("front"));
            Assert.That(SeaVolleyPresentationRules.ArmorFaceAt(
                0f, Vector2.zero, south), Is.EqualTo("back"));
            Assert.That(SeaVolleyPresentationRules.ArmorFaceAt(
                0f, Vector2.zero, Vector2.right * 10f), Is.EqualTo("sides"));
            Assert.That(SeaVolleyPresentationRules.ArmorFaceAt(
                0f, Vector2.zero, Vector2.zero), Is.EqualTo("sides"));
        }

        [Test]
        public void Combat_visual_pool_reuses_released_instances()
        {
            var pool = new SeaCombatVisualPool(() => new GameObject("Pooled combat visual"));
            var first = pool.Acquire();

            pool.Release(first);
            var second = pool.Acquire();

            Assert.That(second, Is.SameAs(first));
            Assert.That(pool.CreatedCount, Is.EqualTo(1));
            Object.DestroyImmediate(second);
        }

        [Test]
        public void Aggregated_volley_visual_is_lightweight_and_trail_enabled()
        {
            var material = SeaMaterialFactory.Create(Color.black);
            var volley = SeaCombatVisualFactory.CreateVolley(material);

            Assert.That(volley.GetComponentsInChildren<MeshRenderer>(true), Has.Length.EqualTo(5));
            Assert.That(volley.GetComponentsInChildren<TrailRenderer>(true), Has.Length.EqualTo(5));
            Assert.That(volley.GetComponentsInChildren<Collider>(true), Is.Empty);
            Object.DestroyImmediate(volley);
            Object.DestroyImmediate(material);
        }

        [Test]
        public void Combat_effect_visual_supports_particles_and_spatial_audio()
        {
            var material = SeaMaterialFactory.Create(Color.white);
            var effect = SeaCombatVisualFactory.CreateEffect("Impact", material);
            var audio = effect.GetComponent<AudioSource>();

            Assert.That(effect.GetComponent<ParticleSystem>(), Is.Not.Null);
            Assert.That(audio, Is.Not.Null);
            Assert.That(audio.spatialBlend, Is.GreaterThan(0f));
            Object.DestroyImmediate(effect);
            Object.DestroyImmediate(material);
        }

        [Test]
        public void Opening_the_chart_menu_blocks_gameplay_without_pausing_the_world()
        {
            var controls = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/Input/SeaControls.inputactions"));
            var host = new GameObject("Input mode test");
            var input = host.AddComponent<SeaInputController>();
            var originalTimeScale = Time.timeScale;

            input.Configure(controls);
            input.SetMenuOpen(true);

            Assert.That(input.IsMenuOpen, Is.True);
            Assert.That(controls.FindActionMap("Gameplay").enabled, Is.False);
            Assert.That(controls.FindActionMap("Menu").enabled, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(originalTimeScale));
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(controls);
        }

        [Test]
        public void Every_player_command_exposes_a_rebindable_keyboard_or_mouse_binding()
        {
            var controls = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/Input/SeaControls.inputactions"));
            var host = new GameObject("Rebind test");
            var input = host.AddComponent<SeaInputController>();

            input.Configure(controls);
            var bindings = input.GetRebindableBindings();

            var required = controls.FindActionMap("Gameplay").actions
                .Where(action => action.name != "Point")
                .Select(action => action.name)
                .ToArray();
            Assert.That(bindings.Select(binding => binding.ActionName).Distinct(), Is.SupersetOf(required));
            Assert.That(bindings.All(binding => !string.IsNullOrWhiteSpace(binding.DisplayPath)), Is.True);
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(controls);
        }


        [Test]
        public void Main_scene_hosts_the_input_system_and_runtime_hud_document()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);

            var input = Object.FindFirstObjectByType<SeaInputController>();
            var hud = Object.FindFirstObjectByType<SeaHudController>();
            var document = Object.FindFirstObjectByType<UIDocument>();

            Assert.That(input, Is.Not.Null);
            Assert.That(input.Actions, Is.Not.Null);
            Assert.That(hud, Is.Not.Null);
            Assert.That(document, Is.Not.Null);
            Assert.That(document.visualTreeAsset, Is.SameAs(
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/SeaHud.uxml")));
            Assert.That(document.panelSettings, Is.Not.Null);
            Assert.That(document.panelSettings.referenceResolution,
                Is.EqualTo(new Vector2Int(1280, 720)));
        }

        [Test]
        public void Runtime_client_has_no_legacy_input_or_immediate_mode_hud_path()
        {
            var runtimeSources = new[]
                {
                    "Assets/Domain",
                    "Assets/Networking",
                    "Assets/Input",
                    "Assets/Presentation",
                    "Assets/UI",
                    "Assets/Bootstrap",
                }
                .SelectMany(directory => Directory.GetFiles(
                    directory,
                    "*.cs",
                    SearchOption.AllDirectories))
                .Select(File.ReadAllText)
                .ToArray();

            Assert.That(runtimeSources.Any(source => source.Contains("void OnGUI(")), Is.False);
            Assert.That(runtimeSources.Any(source => source.Contains("Input.Get")), Is.False);
        }
    }
}
#endif
