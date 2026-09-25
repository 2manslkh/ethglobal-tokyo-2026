using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Tagtag.Services
{
    public sealed class FirebaseSession
    {
        private readonly ServiceConfiguration configuration;
        private readonly INativeIdentity identity;
        private Task<UserSession> refreshing;
        public UserSession Current { get; private set; }
        public FirebaseSession(ServiceConfiguration configuration, INativeIdentity identity)
        {
            this.configuration = configuration;
            this.identity = identity;
            try { Current = JsonUtility.FromJson<UserSession>(identity.LoadSession() ?? "{}"); }
            catch { Current = null; identity.ClearSession(); }
            if (Current != null && (string.IsNullOrEmpty(Current.uid) || string.IsNullOrEmpty(Current.idToken) ||
                string.IsNullOrEmpty(Current.refreshToken) || Current.expiresAt <= 0))
            {
                Current = null;
                identity.ClearSession();
            }
        }

        public async Task<UserSession> SignIn(IdentityCredential credential)
        {
            if (string.IsNullOrEmpty(configuration.firebaseApiKey)) throw new ApiFailure("Sign-in is not configured yet.");
            if (credential == null || (credential.providerId != "apple.com" && credential.providerId != "google.com") ||
                string.IsNullOrEmpty(credential.idToken)) throw new ApiFailure("Sign-in did not return a usable credential. Try again.");
            string post = "providerId=" + Escape(credential.providerId);
            if (!string.IsNullOrEmpty(credential.idToken)) post += "&id_token=" + Escape(credential.idToken);
            if (!string.IsNullOrEmpty(credential.accessToken)) post += "&access_token=" + Escape(credential.accessToken);
            if (!string.IsNullOrEmpty(credential.rawNonce)) post += "&nonce=" + Escape(credential.rawNonce);
            var reply = await TagtagApi.Json<FirebaseSignInReply>("POST", "https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key=" + Escape(configuration.firebaseApiKey),
                new SignInRequest { requestUri = "http://localhost", postBody = post, returnSecureToken = true });
            Current = AcceptSignIn(reply, Now);
            Save();
            return Current;
        }

        public static UserSession AcceptSignIn(FirebaseSignInReply reply, long now)
        {
            if (reply == null)
                throw new ApiFailure("Sign-in did not complete. Try again.");
            if (reply.needConfirmation || !string.IsNullOrEmpty(reply.errorMessage))
                throw new ApiFailure("This email may already use another sign-in method. Sign in with that method first. Account linking is not available yet.");
            if (string.IsNullOrEmpty(reply.localId) || string.IsNullOrEmpty(reply.idToken) ||
                string.IsNullOrEmpty(reply.refreshToken))
                throw new ApiFailure("Sign-in did not complete. Try again with your existing sign-in method.");
            return new UserSession { uid = reply.localId, displayName = reply.displayName, idToken = reply.idToken,
                refreshToken = reply.refreshToken, expiresAt = now + ParseLifetime(reply.expiresIn) };
        }

        public async Task<string> Token(bool required = true)
        {
            if (Current == null)
            {
                if (required) throw new ApiFailure("Sign in to continue.");
                return null;
            }
            if (Current.expiresAt <= Now + 60)
            {
                if (refreshing == null) refreshing = Refresh();
                try { await refreshing; } finally { refreshing = null; }
            }
            return Current.idToken;
        }

        private async Task<UserSession> Refresh()
        {
            var original = Current;
            var form = new WWWForm();
            form.AddField("grant_type", "refresh_token");
            form.AddField("refresh_token", original.refreshToken);
            try
            {
                var reply = await TagtagApi.Form<FirebaseRefreshReply>("https://securetoken.googleapis.com/v1/token?key=" + Escape(configuration.firebaseApiKey), form);
                if (Current != original) throw new ApiFailure("Your account changed. Please retry.");
                try { Current = AcceptRefresh(original, reply, Now); }
                catch (ApiFailure)
                {
                    SignOut();
                    throw;
                }
                Save();
                return Current;
            }
            catch (ApiFailure error)
            {
                if (Current == original && (error.Status == 400 || error.Status == 401))
                {
                    SignOut();
                    throw new ApiFailure("Your session expired. Sign in again.", error.Status);
                }
                throw;
            }
        }

        public static UserSession AcceptRefresh(UserSession original, FirebaseRefreshReply reply, long now)
        {
            if (original == null || reply == null || string.IsNullOrEmpty(reply.id_token) ||
                string.IsNullOrEmpty(reply.refresh_token) || reply.user_id != original.uid)
                throw new ApiFailure("The sign-in service returned an invalid session. Sign in again.");
            return new UserSession { uid = original.uid, displayName = original.displayName, idToken = reply.id_token,
                refreshToken = reply.refresh_token, expiresAt = now + ParseLifetime(reply.expires_in) };
        }

        public void SignOut() { Current = null; identity.ClearSession(); }
        private void Save() { identity.StoreSession(JsonUtility.ToJson(Current)); }
        private static string Escape(string value) => Uri.EscapeDataString(value ?? "");
        private static long ParseLifetime(string value)
        {
            if (!long.TryParse(value, out long seconds) || seconds < 1 || seconds > 604800)
                throw new ApiFailure("The sign-in service returned an invalid session. Sign in again.");
            return seconds;
        }
        private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        [Serializable] private sealed class SignInRequest { public string requestUri, postBody; public bool returnSecureToken; }
    }

    [Serializable] public sealed class FirebaseSignInReply
    {
        public string localId, displayName, idToken, refreshToken, expiresIn, errorMessage;
        public bool needConfirmation;
    }

    [Serializable] public sealed class FirebaseRefreshReply
    {
        public string user_id, id_token, refresh_token, expires_in;
    }
}
