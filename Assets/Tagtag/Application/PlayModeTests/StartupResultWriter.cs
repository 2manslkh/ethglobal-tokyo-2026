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
        public void RunFinished(ITestResult testResults) { }
        public void TestStarted(ITest test) { }
        public void TestFinished(ITestResult result)
        {
            var startup = result.Test.FullName == "Tagtag.Tests.StartupTests.EntrySceneRendersHomeOnFirstLaunch";
            var review = result.Test.FullName == "Tagtag.Tests.PaperVisualTests.CapturesPaperScreensAndRecoveryStates";
            if (!startup && !review) return;
            File.WriteAllText(Path.Combine(Application.temporaryCachePath, startup ? "tagtag-startup-result.xml" : "tagtag-paper-visual-result.xml"),
                result.ToXml(true).OuterXml);
        }
    }
}
