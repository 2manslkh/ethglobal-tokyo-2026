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
            if (string.IsNullOrEmpty(Current?.uid)) Current = null;
        }

        public async Task<UserSession> SignIn(IdentityCredential credential)
        {
            if (string.IsNullOrEmpty(configuration.firebaseApiKey)) throw new ApiFailure("Sign-in is not configured yet.");
            string post = "providerId=" + Escape(credential.providerId);
            if (!string.IsNullOrEmpty(credential.idToken)) post += "&id_token=" + Escape(credential.idToken);
            if (!string.IsNullOrEmpty(credential.accessToken)) post += "&access_token=" + Escape(credential.accessToken);
            if (!string.IsNullOrEmpty(credential.rawNonce)) post += "&nonce=" + Escape(credential.rawNonce);
            var reply = await TagtagApi.Json<SignInReply>("POST", "https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key=" + Escape(configuration.firebaseApiKey),
                new SignInRequest { requestUri = "https://tagtag-tokyo-2026.firebaseapp.com", postBody = post, returnSecureToken = true });
            Current = new UserSession { uid = reply.localId, displayName = reply.displayName, idToken = reply.idToken,
                refreshToken = reply.refreshToken, expiresAt = Now + ParseLifetime(reply.expiresIn) };
            Save();
            return Current;
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
            var reply = await TagtagApi.Form<RefreshReply>("https://securetoken.googleapis.com/v1/token?key=" + Escape(configuration.firebaseApiKey), form);
            if (Current != original) throw new ApiFailure("Your account changed. Please retry.");
            Current.idToken = reply.id_token;
            Current.refreshToken = reply.refresh_token;
            Current.expiresAt = Now + ParseLifetime(reply.expires_in);
            Save();
            return Current;
        }

        public void SignOut() { Current = null; identity.ClearSession(); }
        private void Save() { identity.StoreSession(JsonUtility.ToJson(Current)); }
        private static string Escape(string value) => Uri.EscapeDataString(value ?? "");
        private static long ParseLifetime(string value) => long.TryParse(value, out var seconds) ? seconds : 3600;
        private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        [Serializable] private sealed class SignInRequest { public string requestUri, postBody; public bool returnSecureToken; }
        [Serializable] private sealed class SignInReply { public string localId, displayName, idToken, refreshToken, expiresIn; }
        [Serializable] private sealed class RefreshReply { public string user_id, id_token, refresh_token, expires_in; }
    }
}
