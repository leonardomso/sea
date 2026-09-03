using NUnit.Framework;
using Sea.Client;
using UnityEngine;

namespace Sea.Tests.EditMode
{
    public sealed class SeaClickPingTests
    {
        [Test]
        public void A_ping_lives_for_its_duration_and_no_longer()
        {
            Assert.That(SeaClickPingRules.IsAlive(0f), Is.True);
            Assert.That(SeaClickPingRules.IsAlive(SeaClickPingRules.DurationSeconds - 0.01f), Is.True);
            Assert.That(SeaClickPingRules.IsAlive(SeaClickPingRules.DurationSeconds), Is.False);
            Assert.That(
                SeaClickPingRules.IsAlive(-0.01f),
                Is.False,
                "A ping that has not been fired is not on the water.");
        }

        [Test]
        public void A_ping_opens_from_the_click_and_fades_as_it_goes()
        {
            var early = SeaClickPingRules.DurationSeconds * 0.25f;
            var late = SeaClickPingRules.DurationSeconds * 0.75f;

            Assert.That(SeaClickPingRules.RadiusAt(0f), Is.EqualTo(SeaClickPingRules.StartRadius));
            Assert.That(SeaClickPingRules.RadiusAt(early), Is.LessThan(SeaClickPingRules.RadiusAt(late)));
            Assert.That(
                SeaClickPingRules.RadiusAt(SeaClickPingRules.DurationSeconds),
                Is.EqualTo(SeaClickPingRules.EndRadius).Within(0.001f));

            Assert.That(SeaClickPingRules.AlphaAt(0f), Is.EqualTo(SeaClickPingRules.PeakAlpha));
            Assert.That(SeaClickPingRules.AlphaAt(early), Is.GreaterThan(SeaClickPingRules.AlphaAt(late)));
            Assert.That(
                SeaClickPingRules.AlphaAt(SeaClickPingRules.DurationSeconds),
                Is.EqualTo(0f).Within(0.001f),
                "The ring is invisible by the time it stops being drawn.");
        }

        [Test]
        public void A_ping_opens_faster_than_it_finishes()
        {
            var half = SeaClickPingRules.DurationSeconds * 0.5f;
            var span = SeaClickPingRules.EndRadius - SeaClickPingRules.StartRadius;

            Assert.That(
                SeaClickPingRules.RadiusAt(half) - SeaClickPingRules.StartRadius,
                Is.GreaterThan(span * 0.5f),
                "A linear ring reads as an animation; a splash is mostly over by halfway.");
        }

        [Test]
        public void Ping_segments_trace_a_circle_on_the_water_around_the_click()
        {
            var center = new Vector3(10f, 0.08f, -20f);

            var first = SeaClickPingRules.SegmentPosition(center, 0, 3f);
            var quarter = SeaClickPingRules.SegmentPosition(center, SeaClickPingRules.Segments / 4, 3f);

            Assert.That(first.x, Is.EqualTo(13f).Within(0.001f));
            Assert.That(first.z, Is.EqualTo(-20f).Within(0.001f));
            Assert.That(quarter.x, Is.EqualTo(10f).Within(0.001f));
            Assert.That(quarter.z, Is.EqualTo(-17f).Within(0.001f));
            Assert.That(
                SeaClickPingRules.SegmentPosition(center, 7, 3f).y,
                Is.EqualTo(center.y),
                "The ring lies flat on the water rather than tilting with the camera.");
        }
    }
}
