using NUnit.Framework;
using UnityEngine;

namespace Tagtag.AR.Tests
{
    public sealed class PlacementFlowTests
    {
        [Test]
        public void CameraActionsRequireAnUnblockedPointInsideTheCameraHitArea()
        {
            var flow = new PlacementFlow();
            flow.SetInteraction(new Rect(20f, 120f, 300f, 480f), false);
            Assert.IsTrue(flow.Allows(new Vector2(170f, 360f)));
            Assert.IsFalse(flow.Allows(new Vector2(170f, 100f)));
            Assert.IsFalse(flow.Allows(new Vector2(330f, 360f)));
            flow.SetInteraction(new Rect(20f, 120f, 300f, 480f), true);
            Assert.IsFalse(flow.Allows(new Vector2(170f, 360f)));
        }

        [Test]
        public void UnchangedCameraBoundsDoNotRestartPlaneScanCadence()
        {
            var flow = new PlacementFlow();
            var area = new Rect(20f, 120f, 300f, 480f);
            Assert.IsTrue(flow.SetInteraction(area, false));
            Assert.IsFalse(flow.SetInteraction(area, false));
            Assert.IsTrue(flow.SetInteraction(area, true));
            Assert.IsTrue(flow.SetInteraction(new Rect(20f, 121f, 300f, 480f), true));
        }

        [Test]
        public void SurfaceOutlinesAppearOnlyWhileFindingAPlacement()
        {
            Assert.IsTrue(PlacementFlow.ShouldOutline(true, true, false, false, false));
            Assert.IsFalse(PlacementFlow.ShouldOutline(false, true, false, false, false));
            Assert.IsFalse(PlacementFlow.ShouldOutline(true, false, false, false, false));
            Assert.IsFalse(PlacementFlow.ShouldOutline(true, true, true, false, false));
            Assert.IsFalse(PlacementFlow.ShouldOutline(true, true, false, true, false));
            Assert.IsFalse(PlacementFlow.ShouldOutline(true, true, false, false, true));
        }

        [Test]
        public void DragMovesAlongTheOriginalPlaneOnly()
        {
            var original = new Pose(Vector3.zero, Quaternion.identity);
            var samePlane = new Pose(new Vector3(.12f, .005f, .2f), Quaternion.identity);
            Assert.IsTrue(PlacementFlow.TryMoveOnOriginalPlane(original, samePlane, out var position));
            Assert.That(position.x, Is.EqualTo(.12f).Within(.0001f));
            Assert.That(position.y, Is.EqualTo(.007f).Within(.0001f));
            Assert.IsFalse(PlacementFlow.TryMoveOnOriginalPlane(original,
                new Pose(new Vector3(.12f, .04f, .2f), Quaternion.identity), out _));
            Assert.IsFalse(PlacementFlow.TryMoveOnOriginalPlane(original,
                new Pose(samePlane.position, Quaternion.Euler(11f, 0f, 0f)), out _));
        }
    }
}
