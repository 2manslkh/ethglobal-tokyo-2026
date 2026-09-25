using System.IO;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(Tagtag.Tests.StartupResultWriter))]

namespace Tagtag.Tests
{
    // Keep a local result when a standalone player cannot connect back to the editor.
    public sealed class StartupResultWriter : ITestRunCallback
    {
        public void RunStarted(ITest testsToRun) { }
        public void RunFinished(ITestResult testResults)
        {
            File.WriteAllText(Path.Combine(Application.temporaryCachePath, "tagtag-player-result.xml"),
                testResults.ToXml(true).OuterXml);
        }
        public void TestStarted(ITest test) { }
        public void TestFinished(ITestResult result)
        {
            var startup = result.Test.FullName == "Tagtag.Tests.StartupTests.EntrySceneRendersHomeOnFirstLaunch";
            var review = result.Test.FullName == "Tagtag.Tests.PaperVisualTests.CapturesPaperScreensAndRecoveryStates";
            var camera = result.Test.FullName == "Tagtag.Tests.PaperVisualTests.CameraInventoryFlowUsesFullScreenAndExplicitPlacement";
            var artwork = result.Test.FullName == "Tagtag.AR.PlayMode.Tests.StickerRenderTests.RuntimeStickerMaterialIsIncludedAndRendersArtworkInsteadOfErrorMagenta";
            var missingArtwork = result.Test.FullName == "Tagtag.AR.PlayMode.Tests.StickerRenderTests.MissingArtworkReportsFailureWithoutLeavingAMagentaQuad";
            if (!startup && !review && !camera && !artwork && !missingArtwork) return;
            string filename = startup ? "tagtag-startup-result.xml" : review ? "tagtag-paper-visual-result.xml" :
                camera ? "tagtag-camera-flow-result.xml" : artwork ? "tagtag-sticker-render-result.xml" :
                "tagtag-missing-artwork-result.xml";
            File.WriteAllText(Path.Combine(Application.temporaryCachePath, filename),
                result.ToXml(true).OuterXml);
        }
    }
}
