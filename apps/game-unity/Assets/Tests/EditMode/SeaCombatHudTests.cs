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

        /// <summary>
        /// SEA_5 8.3: the server settled the volley when the trigger was pulled and says how long
        /// the ball is in the air. The number waits for it, or it appears over a hull the shot has
        /// not reached yet.
        /// </summary>
        [Test]
        public void A_hit_number_waits_for_the_ball_and_never_waits_long()
        {
            Assert.That(SeaHitPresentationRules.ImpactAt(10f, 0.5f), Is.EqualTo(10.5f).Within(0.0001f));
            Assert.That(SeaHitPresentationRules.ImpactAt(10f, 0f), Is.EqualTo(10f).Within(0.0001f));

            // A corrupt row or a clock that jumped must not park a number off screen forever.
            Assert.That(
                SeaHitPresentationRules.ImpactAt(10f, 900f),
                Is.EqualTo(10f + SeaHitPresentationRules.MaximumHoldSeconds).Within(0.0001f));

            Assert.That(SeaHitPresentationRules.IsDue(10.5f, 10.5f), Is.True);
            Assert.That(SeaHitPresentationRules.IsDue(10.5f, 10.49f), Is.False);
        }

        [Test]
        public void A_critical_is_marked_on_the_number_a_captain_reads()
        {
            Assert.That(SeaHitPresentationRules.DamageLabel(42u, isCritical: false), Is.EqualTo("-42"));
            Assert.That(SeaHitPresentationRules.DamageLabel(42u, isCritical: true), Is.EqualTo("-42!"));
        }

        /// <summary>
        /// The jolt saturates: a hull cannot be shaken twice as hard by twice the damage without
        /// leaving the water, and what has to be readable off it is that she was hit.
        /// </summary>
        [Test]
        public void A_hull_is_thrown_about_by_a_hit_and_never_out_of_the_water()
        {
            Assert.That(SeaHitPresentationRules.Shock(0u, isCritical: false), Is.EqualTo(0f));
            Assert.That(SeaHitPresentationRules.Shock(120u, isCritical: false), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                SeaHitPresentationRules.Shock(60u, false),
                Is.LessThan(SeaHitPresentationRules.Shock(240u, false)));
            Assert.That(
                SeaHitPresentationRules.Shock(120u, isCritical: true),
                Is.GreaterThan(SeaHitPresentationRules.Shock(120u, isCritical: false)));
            Assert.That(SeaHitPresentationRules.Shock(uint.MaxValue, isCritical: true), Is.EqualTo(1f));
        }

        /// <summary>
        /// Several volleys can be in the air at once and they do not necessarily land in the order
        /// they were fired: a shot from close by arrives before one fired earlier from further off.
        /// </summary>
        [Test]
        public void Shots_in_the_air_land_in_the_order_they_arrive_not_the_order_they_were_fired()
        {
            var queue = new SeaHitQueue();
            queue.Enqueue(new SeaPendingHit(1UL, 9UL, 30u, false, 0, impactAtSeconds: 10.75f));
            queue.Enqueue(new SeaPendingHit(2UL, 9UL, 10u, true, 1, impactAtSeconds: 10.25f));

            Assert.That(queue.Count, Is.EqualTo(2));
            Assert.That(queue.TryTakeDue(10f, out _), Is.False);

            Assert.That(queue.TryTakeDue(10.5f, out var first), Is.True);
            Assert.That(first.Damage, Is.EqualTo(10u));
            Assert.That(first.IsCritical, Is.True);
            Assert.That(queue.TryTakeDue(10.5f, out _), Is.False);

            // A hull the server has already sunk waits for the ball that sank her.
            Assert.That(queue.HasShotInTheAir(9UL), Is.True);
            Assert.That(queue.HasShotInTheAir(8UL), Is.False);

            Assert.That(queue.TryTakeDue(11f, out var second), Is.True);
            Assert.That(second.Damage, Is.EqualTo(30u));
            Assert.That(queue.HasShotInTheAir(9UL), Is.False);
            Assert.That(queue.Count, Is.Zero);
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
        public void A_border_asks_before_the_chart_is_changed_and_names_what_is_beyond_it()
        {
            var offered = SeaHudViewModel.From(new SeaHudSnapshot
            {
                CrossingOffered = true,
                CrossingMapName = "The Broken Shoals",
            });

            Assert.That(offered.HasCrossingOffer, Is.True);
            Assert.That(offered.CrossingText, Is.EqualTo("SAIL ON TO THE BROKEN SHOALS?"));

            // The chart beyond a border is authored, so the name can be late without the
            // prompt being wrong: she is still being asked to leave this one.
            var unnamed = SeaHudViewModel.From(new SeaHudSnapshot { CrossingOffered = true });
            Assert.That(unnamed.HasCrossingOffer, Is.True);
            Assert.That(unnamed.CrossingText, Is.EqualTo("SAIL ON TO THE NEXT CHART?"));

            Assert.That(SeaHudViewModel.From(new SeaHudSnapshot()).HasCrossingOffer, Is.False);
        }

        [Test]
        public void A_sunk_captain_is_not_asked_about_a_border_as_well()
        {
            // Two prompts over one chart is one prompt too many, and the wreck's is the one
            // she has to answer: the offer is withdrawn on the server the moment she goes
            // down, but the rows arrive separately and the HUD must not flicker both.
            var model = SeaHudViewModel.From(new SeaHudSnapshot
            {
                IsSunk = true,
                CrossingOffered = true,
            });

            Assert.That(model.IsSunk, Is.True);
            Assert.That(model.HasCrossingOffer, Is.False);
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

            // A border is the other question the chart asks, and it is asked the same way.
            Assert.That(root.Q("crossing-prompt"), Is.Not.Null);
            Assert.That(root.Q<Button>("crossing-button"), Is.Not.Null);
        }
    }
}
#endif
