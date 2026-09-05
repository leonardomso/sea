#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using Sea.Client;
using UnityEditor;
using UnityEngine.UIElements;

namespace Sea.Tests
{
    /// <summary>
    /// The instruments of Mechanics section 1.2: what the chart HUD reads from a snapshot, and
    /// what the authored document is required to carry for it to be read at all.
    /// </summary>
    public sealed partial class SeaProjectTests
    {
        [Test]
        public void Combat_hud_view_model_formats_player_target_and_reload_state()
        {
            var model = SeaHudViewModel.From(new SeaHudSnapshot
            {
                IsReady = true,
                Coordinate = "AX 59",
                HeadingDegrees = 275f,
                Speed = 12.5f,
                Hull = 750,
                MaxHull = 1000,
                MapRank = 4,
                Gold = 1234,
                HullName = "Sloop",
                CannonName = "Iron Six-Pounder",
                CannonTier = 2,
                VolleyDamage = 42,
                ReloadMilliseconds = 3500,
                MagazineSize = 6,
                CombatPowerUsed = 12f,
                CombatPowerBudget = 45f,
                SelectedAmmoName = "Chain Shot",
                TargetName = "RAIDER 7",
                TargetHull = 300,
                TargetMaxHull = 600,
                TargetArmorFace = "front",
                TargetArmorAbsorption = 0.25f,
                ReadyVolleys = 0,
                ReloadRemainingSeconds = 2f,
                ReloadDurationSeconds = 4f,
            });

            Assert.That(model.HullProgress, Is.EqualTo(0.75f));
            Assert.That(model.MapRankText, Is.EqualTo("4"));
            Assert.That(model.GoldText, Is.EqualTo("1,234 ¤"));
            Assert.That(model.ShipText, Is.EqualTo("SLOOP  •  IRON SIX-POUNDER T2"));
            Assert.That(model.VolleyText, Is.EqualTo("DMG 42  •  MAG 6  •  3.5s"));
            Assert.That(model.CombatPowerText, Does.Contain("12 / 45"));
            Assert.That(model.SelectedAmmoLabel, Is.EqualTo("CHAIN SHOT"));
            Assert.That(model.HullText, Is.EqualTo("750 / 1,000"));
            Assert.That(model.NavigationText, Is.EqualTo("AX 59  •  275°  •  12.5 KN"));
            Assert.That(model.HasTarget, Is.True);
            Assert.That(model.TargetHullProgress, Is.EqualTo(0.5f));
            Assert.That(model.TargetArmorText, Is.EqualTo("FRONT  •  25% ABSORBED"));
            Assert.That(model.ReloadProgress, Is.EqualTo(0.5f));
            Assert.That(model.IsLoaded, Is.False);
            Assert.That(model.ReloadText, Is.EqualTo("2.0s"));
            Assert.That(model.MagazineText, Is.EqualTo("0 / 6"));
        }

        /// <summary>
        /// The two instruments a captain glances at rather than reads: how many volleys are in
        /// the racks, and where the wind is pushing everything on the water.
        /// </summary>
        [Test]
        public void Hud_maps_the_magazine_and_the_wind_the_way_the_dial_draws_them()
        {
            var model = SeaHudViewModel.From(new SeaHudSnapshot
            {
                MagazineSize = 5,
                ReadyVolleys = 2,
                WindDirectionDegrees = 135f,
            });

            Assert.That(model.MagazineSize, Is.EqualTo(5));
            Assert.That(model.ReadyVolleys, Is.EqualTo(2));
            Assert.That(model.WindRotationDegrees, Is.EqualTo(135f));
            Assert.That(model.WindText, Is.EqualTo("SE"));

            // A bearing is read the way a compass is: clockwise from north, and wrapped.
            Assert.That(SeaHudViewModel.CompassPoint(0f), Is.EqualTo("N"));
            Assert.That(SeaHudViewModel.CompassPoint(90f), Is.EqualTo("E"));
            Assert.That(SeaHudViewModel.CompassPoint(-90f), Is.EqualTo("W"));
            Assert.That(SeaHudViewModel.CompassPoint(359f), Is.EqualTo("N"));
            Assert.That(
                SeaHudViewModel.From(new SeaHudSnapshot { WindDirectionDegrees = -45f })
                    .WindRotationDegrees,
                Is.EqualTo(315f));
        }

        /// <summary>
        /// A held fire key is a request to keep firing, not a stream of commands: the repeat
        /// waits for the racks and for the module's own minimum interval.
        /// </summary>
        [Test]
        public void A_held_fire_key_repeats_no_faster_than_the_module_accepts()
        {
            Assert.That(SeaFireRepeatRules.ShouldRepeat(true, true, 1f), Is.True);
            Assert.That(SeaFireRepeatRules.ShouldRepeat(true, true, 0.99f), Is.False);
            Assert.That(SeaFireRepeatRules.ShouldRepeat(true, false, 5f), Is.False);
            Assert.That(SeaFireRepeatRules.ShouldRepeat(false, true, 5f), Is.False);
        }

        [Test]
        public void A_wreck_is_offered_a_berth_once_and_then_counted_back_onto_the_water()
        {
            var unchosen = SeaHudViewModel.From(new SeaHudSnapshot { IsSunk = true });

            Assert.That(unchosen.IsSunk, Is.True);
            Assert.That(unchosen.CanChooseBerth, Is.True);
            Assert.That(unchosen.WreckText, Is.EqualTo("START AGAIN AT PORT LOWELL."));

            var chosen = SeaHudViewModel.From(new SeaHudSnapshot
            {
                IsSunk = true,
                RespawnChosen = true,
                RespawnRemainingSeconds = 5.4f,
            });

            Assert.That(chosen.CanChooseBerth, Is.False);
            Assert.That(chosen.WreckText, Is.EqualTo("BACK ON THE WATER IN 5s"));
            Assert.That(SeaHudViewModel.From(new SeaHudSnapshot()).IsSunk, Is.False);
        }

        [Test]
        public void Runtime_hud_contains_the_locked_chart_combat_instruments()
        {
            var document = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/SeaHud.uxml");
            Assert.That(document, Is.Not.Null);
            var root = document.CloneTree();

            var requiredElements = new[]
            {
                "connection-status", "navigation-readout", "gold-label", "diamond-label",
                "top-coordinate-ruler", "left-coordinate-ruler",
                "map-rank-label", "ship-loadout", "volley-text", "combat-power-label",
                "player-hull",
                "mini-map-frame",
                "target-frame", "target-hull", "target-armor-text",
                "fire-control", "reload-gauge", "reload-text", "magazine-text", "magazine-dots",
                "ammo-rail", "wind-dial", "wind-arrow", "wind-text",
                "ability-rail", "status-strip", "channel-progress", "coordinate-navigator",
                "chart-menu", "rebind-list",
            };

            Assert.That(requiredElements.All(name => root.Q(name) != null), Is.True);
            Assert.That(root.Q("player-experience"), Is.Null);

            // The ruler carries one slot per ruler cell -- ten squares to a cell -- on each edge.
            for (var cell = 0; cell < SeaChartCoordinates.ColumnCount; cell++)
            {
                Assert.That(root.Q($"top-coordinate-{cell}"), Is.Not.Null);
                Assert.That(root.Q($"left-coordinate-{cell}"), Is.Not.Null);
            }

            // The magazine never holds more than the hull's racks plus the rigging bonus.
            for (var dot = 0; dot < 6; dot++)
            {
                Assert.That(root.Q($"magazine-dot-{dot}"), Is.Not.Null);
            }

            // The ammunition keys are the labels: pressing 1 to 4 is loading the racks.
            Assert.That(root.Q<Button>("ammo-round").text, Is.EqualTo("1"));
            Assert.That(root.Q<Button>("ammo-incendiary").text, Is.EqualTo("4"));

            // One magazine bearing in every direction: the aim rail and the broadside pair are
            // gone, and the abilities they sat beside went with them.
            Assert.That(root.Q("weak-point-rail"), Is.Null);
            Assert.That(root.Q("port-broadside"), Is.Null);
            Assert.That(root.Q("starboard-broadside"), Is.Null);
            Assert.That(root.Q("ability-full-sail"), Is.Null);
            Assert.That(root.Q<Button>("repair").text, Is.EqualTo("R"));

            // The kit is a separate order on a cooldown of its own, so it gets a slot of its
            // own; the berth is the one order a wreck can still give.
            Assert.That(root.Q<Button>("repair-kit").text, Is.EqualTo("K"));
            Assert.That(root.Q("wreck-prompt"), Is.Not.Null);
            Assert.That(root.Q<Button>("respawn-button"), Is.Not.Null);
        }
    }
}
#endif
