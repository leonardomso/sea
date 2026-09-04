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
            // The ruler counts cells of ten squares, not squares. Columns are lettered and
            // rows numbered, which is what the server says too -- see the block below.
            Assert.That(SeaChartCoordinates.ColumnLabelAt(0), Is.EqualTo("A"));
            Assert.That(SeaChartCoordinates.ColumnLabelAt(25), Is.EqualTo("Z"));
            Assert.That(SeaChartCoordinates.ColumnLabelAt(26), Is.EqualTo("AA"));
            Assert.That(
                SeaChartCoordinates.ColumnLabelAt(SeaChartCoordinates.ColumnCount - 1),
                Is.EqualTo("AN"));
            Assert.That(SeaChartCoordinates.RowLabelAt(0), Is.EqualTo("1"));
            Assert.That(
                SeaChartCoordinates.RowLabelAt(SeaChartCoordinates.RowCount - 1),
                Is.EqualTo("40"));
            Assert.That(
                SeaChartCoordinates.ColumnLabelAt(5),
                Is.SameAs(SeaChartCoordinates.ColumnLabelAt(5)),
                "Labels are cached, not formatted per call.");
            Assert.That(SeaChartCoordinates.RowLabelAt(7), Is.SameAs(SeaChartCoordinates.RowLabelAt(7)));

            // These are the strings the server's ChartCoordinates.LabelAt answers for the same
            // points, copied by hand because the Unity assembly cannot reference Sea.Server.
            // They are the only thing holding the two mirrors together: this client answered
            // "13-12" where the server answered "M12" until the schemes were made one.
            // If the server's ruler changes, these fail, and that is the point.
            foreach (var (x, y, expected) in new[]
                     {
                         (0f, 0f, "A1"),
                         (5f, 5f, "A1"),
                         (15f, 5f, "B1"),          // ten squares east is one column, not one row
                         (5f, 15f, "A2"),          // ten squares south is one row, not one column
                         (125f, 115f, "M12"),
                         (265f, 5f, "AA1"),        // the carry from Z into AA
                         (399.9f, 399.9f, "AN40"),
                         (400f, 400f, "AN40"),     // the far corner is inside the map, and clamps
                     })
            {
                Assert.That(SeaChartCoordinates.LabelAt(x, y), Is.EqualTo(expected));
            }

            // And back the other way, through the parser the navigator uses.
            Assert.That(SeaChartCoordinates.TryCellCenter("M12", out var cell), Is.True);
            Assert.That(cell.Column, Is.EqualTo(12));
            Assert.That(cell.Row, Is.EqualTo(11));
            Assert.That(cell.X, Is.EqualTo(125f).Within(0.001f));
            Assert.That(cell.Y, Is.EqualTo(115f).Within(0.001f));

            foreach (var rejected in new[]
                     { "", "A0", "AN41", "AO1", "12M", "M 12", "AA", "M12extra", "13-12" })
            {
                Assert.That(
                    SeaChartCoordinates.TryCellCenter(rejected, out _),
                    Is.False,
                    $"expected '{rejected}' to be rejected");
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
