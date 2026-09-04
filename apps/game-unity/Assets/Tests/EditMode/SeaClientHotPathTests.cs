using NUnit.Framework;
using Sea.Client;
using UnityEngine;

namespace Sea.Tests.EditMode
{
    /// <summary>Per-frame client paths must not allocate or repeat work they can skip.</summary>
    public sealed class SeaClientHotPathTests
    {
        [Test]
        public void Chart_ruler_labels_are_shared_and_agree_with_full_labels()
        {
            // The ruler counts the map's own squares: 1 at each of the north and west edges,
            // 40 at each of the south and east ones.
            Assert.That(SeaChartCoordinates.ColumnLabelAt(0), Is.EqualTo("1"));
            Assert.That(
                SeaChartCoordinates.ColumnLabelAt(SeaChartCoordinates.ColumnCount - 1),
                Is.EqualTo("40"));
            Assert.That(
                SeaChartCoordinates.RowLabelAt(SeaChartCoordinates.RowCount - 1),
                Is.EqualTo("40"));
            Assert.That(
                SeaChartCoordinates.ColumnLabelAt(5),
                Is.SameAs(SeaChartCoordinates.ColumnLabelAt(5)),
                "Labels are cached, not formatted per call.");
            Assert.That(SeaChartCoordinates.RowLabelAt(7), Is.SameAs(SeaChartCoordinates.RowLabelAt(7)));

            foreach (var (x, y) in new[] { (-99.9f, 99.9f), (0f, 0f), (99.9f, -99.9f), (12.3f, -45.6f) })
            {
                var expected =
                    SeaChartCoordinates.ColumnLabelAt(SeaChartCoordinates.ColumnIndexAt(x)) + "-" +
                    SeaChartCoordinates.RowLabelAt(SeaChartCoordinates.RowIndexAt(y));
                Assert.That(SeaChartCoordinates.LabelAt(x, y), Is.EqualTo(expected));
            }

            Assert.That(SeaChartCoordinates.ColumnIndexAt(-1000f), Is.EqualTo(0));
            Assert.That(
                SeaChartCoordinates.ColumnIndexAt(1000f),
                Is.EqualTo(SeaChartCoordinates.ColumnCount - 1));
            // Y grows south from a top-left origin now, so a large y clamps to the last row and
            // a negative one clamps to the first -- there is no flip left to invert that.
            Assert.That(
                SeaChartCoordinates.RowIndexAt(1000f),
                Is.EqualTo(SeaChartCoordinates.RowCount - 1));
            Assert.That(SeaChartCoordinates.RowIndexAt(-1000f), Is.EqualTo(0));
        }

        [Test]
        public void Focus_target_set_only_reports_real_changes()
        {
            var targets = new SeaFocusTargetSet();
            Assert.That(targets.Targets, Is.Empty);

            targets.Begin();
            targets.Add(9);
            targets.Add(0);
            targets.Add(8);
            targets.Add(9);
            Assert.That(targets.Commit(), Is.True);
            Assert.That(targets.Targets, Is.EqualTo(new ulong[] { 8, 9 }), "Sorted, deduplicated, no zero.");

            targets.Begin();
            targets.Add(8);
            targets.Add(9);
            Assert.That(targets.Commit(), Is.False, "Same ships in another order is not a change.");
            Assert.That(targets.Targets, Is.EqualTo(new ulong[] { 8, 9 }));

            targets.Begin();
            targets.Add(9);
            Assert.That(targets.Commit(), Is.True);
            Assert.That(targets.Targets, Is.EqualTo(new ulong[] { 9 }));

            targets.Begin();
            Assert.That(targets.Commit(), Is.True, "Dropping the last target is a change.");
            Assert.That(targets.Targets, Is.Empty);
            targets.Begin();
            Assert.That(targets.Commit(), Is.False);

            targets.Begin();
            targets.Add(3);
            targets.Commit();
            targets.Clear();
            Assert.That(targets.Targets, Is.Empty);
        }

        [Test]
        public void Hud_depends_only_on_the_local_ship_and_its_target()
        {
            Assert.That(SeaHudViewModel.DependsOnShip(7, 7, 0), Is.True);
            Assert.That(SeaHudViewModel.DependsOnShip(9, 7, 9), Is.True);
            Assert.That(SeaHudViewModel.DependsOnShip(12, 7, 9), Is.False);
            Assert.That(SeaHudViewModel.DependsOnShip(0, 0, 0), Is.False, "No ship never marks the HUD.");
        }

        [Test]
        public void Viewport_marker_redraws_only_when_the_footprint_moves()
        {
            var marker = new SeaMiniMapViewportMarker();
            try
            {
                Assert.That(marker.Show(new Vector3(1f, 0f, 2f), new Vector2(10f, 5f)), Is.True);
                Assert.That(marker.Show(new Vector3(1f, 0f, 2f), new Vector2(10f, 5f)), Is.False);
                Assert.That(marker.Show(new Vector3(1.5f, 0f, 2f), new Vector2(10f, 5f)), Is.True);
                Assert.That(marker.Show(new Vector3(1.5f, 0f, 2f), new Vector2(12f, 6f)), Is.True, "Zoom changes the footprint.");
                Assert.That(marker.Show(new Vector3(1.5f, 0f, 2f), new Vector2(12f, 6f)), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(GameObject.Find("Chart Viewport"));
            }
        }
    }
}
