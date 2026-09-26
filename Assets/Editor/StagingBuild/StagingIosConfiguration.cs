using System;
using System.IO;
using Tagtag;
using UnityEngine;

namespace Tagtag.Editor
{
    public sealed class StagingFirebaseSettings
    {
        public string ProjectId;
        public string GcmSenderId;
        public string GoogleAppId;
        public string BundleId;
        public string ApiKey;
        public string ClientId;
        public string ReversedClientId;
    }

    public static class StagingIosConfiguration
    {
        public const string AllowedApiBaseUrl = "https://tagtag-api-542095619867.asia-northeast1.run.app";
        public const string MarkerName = ".tagtag-staging-build";
        public const string ProductionCopyName = ".tagtag-production-service-configuration.json";

        public static void RequireIsolatedProject(string projectRoot)
        {
            var root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!root.StartsWith("/private/tmp/", StringComparison.Ordinal) ||
                !File.Exists(Path.Combine(projectRoot, MarkerName)) ||
                !File.Exists(Path.Combine(projectRoot, ProductionCopyName)))
                throw new InvalidOperationException("Staging export requires a marked isolated project under /private/tmp.");
        }

        public static ServiceConfiguration LoadValidated(string stagingPath, string productionPath, StagingFirebaseSettings firebase)
        {
            var production = ReadConfiguration(productionPath);
            var staging = ReadConfiguration(stagingPath);
            if (production.nftEnabled || !IsSecureApi(production.apiBaseUrl) ||
                string.IsNullOrWhiteSpace(production.firebaseApiKey) ||
                string.IsNullOrWhiteSpace(production.googleClientId))
                throw new InvalidOperationException("Production service configuration is not a valid disabled baseline.");
            if (!staging.nftEnabled || staging.apiBaseUrl != AllowedApiBaseUrl ||
                string.IsNullOrWhiteSpace(staging.firebaseApiKey) ||
                string.IsNullOrWhiteSpace(staging.googleClientId) ||
                string.IsNullOrWhiteSpace(staging.googleReversedClientId) ||
                string.IsNullOrWhiteSpace(staging.thirdwebClientId))
                throw new InvalidOperationException("Staging service configuration is incomplete or NFTs are disabled.");
            var suffix = ".apps.googleusercontent.com";
            if (!staging.googleClientId.EndsWith(suffix, StringComparison.Ordinal) ||
                staging.googleReversedClientId != "com.googleusercontent.apps." +
                    staging.googleClientId.Substring(0, staging.googleClientId.Length - suffix.Length))
                throw new InvalidOperationException("Staging Google reversed client ID does not match its client ID.");
            if (new Uri(staging.apiBaseUrl).Host.Equals(new Uri(production.apiBaseUrl).Host, StringComparison.OrdinalIgnoreCase) ||
                staging.firebaseApiKey == production.firebaseApiKey ||
                staging.googleClientId == production.googleClientId ||
                staging.googleReversedClientId == production.googleReversedClientId)
                throw new InvalidOperationException("Staging must use separate API, Firebase, and Google public settings.");
            var appIdParts = firebase?.GoogleAppId?.Split(':');
            if (firebase == null || firebase.ProjectId != "tagtag-nft-staging-2026" ||
                firebase.GcmSenderId != "542095619867" ||
                appIdParts == null || appIdParts.Length != 4 || appIdParts[0] != "1" ||
                appIdParts[1] != "542095619867" || appIdParts[2] != "ios" ||
                string.IsNullOrWhiteSpace(appIdParts[3]) ||
                firebase.BundleId != "com.kenk.tagtag.staging" ||
                firebase.ApiKey != staging.firebaseApiKey ||
                firebase.ClientId != staging.googleClientId ||
                firebase.ReversedClientId != staging.googleReversedClientId)
                throw new InvalidOperationException("Firebase iOS plist does not match the approved staging project and service configuration.");
            return staging;
        }

        public static ServiceConfiguration Install(string stagingPath, string productionPath, string destinationPath,
            StagingFirebaseSettings firebase)
        {
            var configuration = LoadValidated(stagingPath, productionPath, firebase);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destinationPath)));
            // Serialize only the runtime's public fields; do not carry unknown input keys into the app.
            File.WriteAllText(destinationPath, JsonUtility.ToJson(configuration, true) + "\n");
            return configuration;
        }

        private static ServiceConfiguration ReadConfiguration(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new InvalidOperationException("Service configuration file is missing.");
            try
            {
                var configuration = JsonUtility.FromJson<ServiceConfiguration>(File.ReadAllText(path));
                if (configuration != null) return configuration;
            }
            catch (Exception error) when (error is ArgumentException || error is FormatException)
            {
                throw new InvalidOperationException("Service configuration JSON is invalid.", error);
            }
            throw new InvalidOperationException("Service configuration JSON is invalid.");
        }

        private static bool IsSecureApi(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo) &&
                string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
        }
    }
}
