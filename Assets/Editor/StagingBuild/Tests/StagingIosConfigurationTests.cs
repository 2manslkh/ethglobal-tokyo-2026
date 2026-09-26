using System;
using System.IO;
using NUnit.Framework;
using Tagtag;
using UnityEngine;

namespace Tagtag.Editor.Tests
{
    public sealed class StagingIosConfigurationTests
    {
        private string directory;
        private string productionPath;
        private string stagingPath;

        private const string Production = "{\"apiBaseUrl\":\"https://production.example.test\",\"firebaseApiKey\":\"production-key\",\"googleClientId\":\"production.apps.googleusercontent.com\",\"googleReversedClientId\":\"com.googleusercontent.apps.production\",\"nftEnabled\":false,\"thirdwebClientId\":\"\"}";
        private const string Staging = "{\"apiBaseUrl\":\"https://tagtag-api-542095619867.asia-northeast1.run.app\",\"firebaseApiKey\":\"staging-key\",\"googleClientId\":\"staging.apps.googleusercontent.com\",\"googleReversedClientId\":\"com.googleusercontent.apps.staging\",\"nftEnabled\":true,\"thirdwebClientId\":\"staging-thirdweb\"}";

        private static StagingFirebaseSettings ValidFirebase() => new StagingFirebaseSettings
        {
            ProjectId = "tagtag-nft-staging-2026",
            GcmSenderId = "542095619867",
            GoogleAppId = "1:542095619867:ios:abc123",
            BundleId = "com.kenk.tagtag.staging",
            ApiKey = "staging-key",
            ClientId = "staging.apps.googleusercontent.com",
            ReversedClientId = "com.googleusercontent.apps.staging"
        };

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "tagtag-staging-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            productionPath = Path.Combine(directory, "production.json");
            stagingPath = Path.Combine(directory, "staging.json");
            File.WriteAllText(productionPath, Production);
            File.WriteAllText(stagingPath, Staging);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(directory, true);

        [Test]
        public void ValidStagingConfigurationCanBeInstalledWithoutChangingProductionSource()
        {
            var destination = Path.Combine(directory, "copy", "ServiceConfiguration.json");
            var configuration = StagingIosConfiguration.Install(stagingPath, productionPath, destination, ValidFirebase());

            var installed = JsonUtility.FromJson<ServiceConfiguration>(File.ReadAllText(destination));
            Assert.AreEqual("https://tagtag-api-542095619867.asia-northeast1.run.app", installed.apiBaseUrl);
            Assert.IsTrue(installed.nftEnabled);
            Assert.AreEqual("staging-thirdweb", installed.thirdwebClientId);
            Assert.AreEqual(Production, File.ReadAllText(productionPath));
            Assert.AreEqual("com.googleusercontent.apps.staging", configuration.googleReversedClientId);
        }

        [Test]
        public void UnknownFieldsAreNotBundledIntoPlayerResource()
        {
            File.WriteAllText(stagingPath, Staging.Replace("\"nftEnabled\":true", "\"serverSecret\":\"must-not-bundle\",\"nftEnabled\":true"));
            var destination = Path.Combine(directory, "copy", "ServiceConfiguration.json");

            StagingIosConfiguration.Install(stagingPath, productionPath, destination, ValidFirebase());

            Assert.IsFalse(File.ReadAllText(destination).Contains("must-not-bundle"));
        }

        [Test]
        public void MissingOrMalformedStagingFileCannotReplaceConfiguration()
        {
            var destination = Path.Combine(directory, "resource.json");
            File.WriteAllText(destination, Production);
            Assert.Throws<InvalidOperationException>(() => StagingIosConfiguration.Install("missing.json", productionPath, destination, ValidFirebase()));
            File.WriteAllText(stagingPath, "not JSON");
            Assert.Throws<InvalidOperationException>(() => StagingIosConfiguration.Install(stagingPath, productionPath, destination, ValidFirebase()));
            Assert.AreEqual(Production, File.ReadAllText(destination));
        }

        [Test]
        public void ProductionServiceValuesAreRejectedBeforeResourceReplacement()
        {
            var destination = Path.Combine(directory, "resource.json");
            File.WriteAllText(destination, Production);
            foreach (var shared in new[] {
                Staging.Replace("https://tagtag-api-542095619867.asia-northeast1.run.app", "https://production.example.test"),
                Staging.Replace("staging-key", "production-key"),
                Staging.Replace("staging.apps.googleusercontent.com", "production.apps.googleusercontent.com")
                    .Replace("com.googleusercontent.apps.staging", "com.googleusercontent.apps.production")
            })
            {
                File.WriteAllText(stagingPath, shared);
                Assert.Throws<InvalidOperationException>(() => StagingIosConfiguration.Install(stagingPath, productionPath, destination, ValidFirebase()));
                Assert.AreEqual(Production, File.ReadAllText(destination));
            }
        }

        [Test]
        public void StagingRequiresNftsWalletAndMatchingGoogleScheme()
        {
            foreach (var invalid in new[] {
                Staging.Replace("\"nftEnabled\":true", "\"nftEnabled\":false"),
                Staging.Replace("staging-thirdweb", ""),
                Staging.Replace("com.googleusercontent.apps.staging", "com.googleusercontent.apps.other"),
                Staging.Replace("https://tagtag-api-542095619867.asia-northeast1.run.app", "https://other.example.test"),
                Staging.Replace("https://tagtag-api-542095619867.asia-northeast1.run.app", "https://user:secret@tagtag-api-542095619867.asia-northeast1.run.app"),
                Staging.Replace("https://tagtag-api-542095619867.asia-northeast1.run.app", "https://tagtag-api-542095619867.asia-northeast1.run.app?token=abc")
            })
            {
                File.WriteAllText(stagingPath, invalid);
                Assert.Throws<InvalidOperationException>(() => StagingIosConfiguration.LoadValidated(stagingPath, productionPath, ValidFirebase()));
            }
        }

        [Test]
        public void FirebasePlistMustIdentifyApprovedProjectAndApp()
        {
            foreach (Action<StagingFirebaseSettings> change in new Action<StagingFirebaseSettings>[] {
                settings => settings.ProjectId = "tagtag-production",
                settings => settings.GcmSenderId = "329004805254",
                settings => settings.GoogleAppId = "1:329004805254:ios:abc123",
                settings => settings.BundleId = "com.kenk.tagtag"
            })
            {
                var firebase = ValidFirebase();
                change(firebase);
                Assert.Throws<InvalidOperationException>(() => StagingIosConfiguration.LoadValidated(stagingPath, productionPath, firebase));
            }
        }

        [Test]
        public void FirebasePlistMustMatchSuppliedServiceConfiguration()
        {
            foreach (Action<StagingFirebaseSettings> change in new Action<StagingFirebaseSettings>[] {
                settings => settings.ApiKey = "another-staging-key",
                settings => settings.ClientId = "another.apps.googleusercontent.com",
                settings => settings.ReversedClientId = "com.googleusercontent.apps.another"
            })
            {
                var firebase = ValidFirebase();
                change(firebase);
                Assert.Throws<InvalidOperationException>(() => StagingIosConfiguration.LoadValidated(stagingPath, productionPath, firebase));
            }
        }

        [Test]
        public void BuildRequiresMarkedPrivateTemporaryProject()
        {
            Assert.Throws<InvalidOperationException>(() => StagingIosConfiguration.RequireIsolatedProject(directory));
        }

        [Test]
        public void MarkedPrivateTemporaryProjectIsAccepted()
        {
            var project = Path.Combine("/private/tmp", "tagtag-staging-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(project);
            try
            {
                File.WriteAllText(Path.Combine(project, StagingIosConfiguration.MarkerName), "");
                File.WriteAllText(Path.Combine(project, StagingIosConfiguration.ProductionCopyName), Production);
                Assert.DoesNotThrow(() => StagingIosConfiguration.RequireIsolatedProject(project));
            }
            finally
            {
                Directory.Delete(project, true);
            }
        }
    }
}
