using System;
using NUnit.Framework;

namespace Tagtag.Services.Tests
{
    public sealed class ServiceBoundaryTests
    {
        [TestCase("http://example.com/api", false)]
        [TestCase("https://example.com/api", true)]
        [TestCase("https://example.com/upload?signature=abc", true)]
        [TestCase("https://user:secret@example.com/api", false)]
        [TestCase("https://example.com/api#fragment", false)]
        public void SecureUrlsRequireHttpsWithoutEmbeddedCredentials(string url, bool accepted)
        {
            Assert.AreEqual(accepted, TagtagApi.IsSecureUrl(url));
        }

        [Test]
        public void PrepareReadsAllSignedUploadHeaders()
        {
            PrepareResult result = TagtagApi.ParsePrepareResponse(
                "{\"id\":\"sticker-1\",\"uploadUrl\":\"https://example.com/upload?signature=abc\",\"uploadHeaders\":{\"content-type\":\"application/octet-stream\",\"x-goog-content-length-range\":\"1,16777216\"}}");

            Assert.AreEqual("sticker-1", result.id);
            Assert.AreEqual(2, result.uploadHeaders.Length);
            Assert.AreEqual("content-type", result.uploadHeaders[0].name);
            Assert.AreEqual("application/octet-stream", result.uploadHeaders[0].value);
            Assert.AreEqual("x-goog-content-length-range", result.uploadHeaders[1].name);
            Assert.AreEqual("1,16777216", result.uploadHeaders[1].value);
        }

        [TestCase("{\"id\":\"one\",\"uploadUrl\":\"https://example.com\"}")]
        [TestCase("{\"id\":\"one\",\"uploadUrl\":\"https://example.com\",\"uploadHeaders\":{}}")]
        [TestCase("{\"id\":\"one\",\"uploadUrl\":\"https://example.com\",\"uploadHeaders\":{\"authorization\":\"Bearer token\"}}")]
        [TestCase("{\"id\":\"one\",\"uploadUrl\":\"https://example.com\",\"uploadHeaders\":{\"content-type\":\"safe\\r\\nInjected: true\"}}")]
        [TestCase("{\"id\":\"one\",\"uploadUrl\":\"https://example.com\",\"uploadHeaders\":{\"content-type\":\"first\",\"Content-Type\":\"second\"}}")]
        public void PrepareRejectsMissingOrUnsafeSignedHeaders(string json)
        {
            Assert.Throws<ApiFailure>(() => TagtagApi.ParsePrepareResponse(json));
        }

        [Test]
        public void PrepareAllowsIdempotentRetryWithoutUpload()
        {
            PrepareResult result = TagtagApi.ParsePrepareResponse("{\"id\":\"already-published\",\"uploadUrl\":\"\",\"uploadHeaders\":{}}");
            Assert.AreEqual(0, result.uploadHeaders.Length);
        }

        [Test]
        public void NeedConfirmationGivesExistingMethodGuidanceWithoutCreatingSession()
        {
            var reply = new FirebaseSignInReply { needConfirmation = true, localId = "uid", idToken = "token", refreshToken = "refresh", expiresIn = "3600" };
            ApiFailure error = Assert.Throws<ApiFailure>(() => FirebaseSession.AcceptSignIn(reply, 100));
            StringAssert.Contains("another sign-in method", error.Message);
        }

        [Test]
        public void SignInRejectsIncompleteResponse()
        {
            Assert.Throws<ApiFailure>(() => FirebaseSession.AcceptSignIn(null, 100));
            Assert.Throws<ApiFailure>(() => FirebaseSession.AcceptSignIn(
                new FirebaseSignInReply { localId = "uid", idToken = "token", expiresIn = "3600" }, 100));
            Assert.Throws<ApiFailure>(() => FirebaseSession.AcceptSignIn(
                new FirebaseSignInReply { localId = "uid", idToken = "token", refreshToken = "refresh", expiresIn = "0" }, 100));
        }

        [Test]
        public void RefreshRejectsChangedAccountAndDoesNotMutateOriginal()
        {
            var original = new UserSession { uid = "original", idToken = "old", refreshToken = "old-refresh", expiresAt = 10 };
            Assert.Throws<ApiFailure>(() => FirebaseSession.AcceptRefresh(original,
                new FirebaseRefreshReply { user_id = "different", id_token = "new", refresh_token = "new-refresh", expires_in = "3600" }, 100));
            Assert.AreEqual("old", original.idToken);
            Assert.AreEqual("old-refresh", original.refreshToken);
        }

        [Test]
        public void RefreshReplacesValidatedSession()
        {
            var original = new UserSession { uid = "uid", displayName = "Taggi", idToken = "old", refreshToken = "old-refresh" };
            UserSession renewed = FirebaseSession.AcceptRefresh(original,
                new FirebaseRefreshReply { user_id = "uid", id_token = "new", refresh_token = "new-refresh", expires_in = "3600" }, 100);
            Assert.AreEqual("uid", renewed.uid);
            Assert.AreEqual("Taggi", renewed.displayName);
            Assert.AreEqual("new", renewed.idToken);
            Assert.AreEqual(3700, renewed.expiresAt);
            Assert.AreEqual("old", original.idToken);
        }
    }
}
