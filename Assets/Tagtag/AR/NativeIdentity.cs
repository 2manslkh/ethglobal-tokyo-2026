using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Tagtag.AR
{
    public sealed class NativeIdentity : MonoBehaviour, INativeIdentity
    {
        [Serializable] private sealed class IdentityResult
        {
            public string providerId;
            public string idToken;
            public string accessToken;
            public string rawNonce;
            public string error;
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern bool TagtagIdentityBegin(string provider, string googleClientId, string googleReversedClientId);
        [DllImport("__Internal")] private static extern IntPtr TagtagIdentityPoll();
        [DllImport("__Internal")] private static extern void TagtagIdentityFree(IntPtr pointer);
        [DllImport("__Internal")] private static extern void TagtagIdentityCancel();
        [DllImport("__Internal")] private static extern bool TagtagSessionStore(string value);
        [DllImport("__Internal")] private static extern IntPtr TagtagSessionLoad();
        [DllImport("__Internal")] private static extern void TagtagSessionClear();
#endif

        private Coroutine request;

        public void SignIn(string provider, ServiceConfiguration configuration,
            Action<IdentityCredential> success, Action<string> failure)
        {
            if (success == null || failure == null) throw new ArgumentNullException("identity callback");
            if (request != null) { failure("Sign-in is already in progress."); return; }
            if (provider != "apple" && provider != "google") { failure("Choose Apple or Google."); return; }
            if (provider == "google" && (configuration == null ||
                string.IsNullOrEmpty(configuration.googleClientId) || string.IsNullOrEmpty(configuration.googleReversedClientId)))
            { failure("Google sign-in is not configured."); return; }
#if UNITY_IOS && !UNITY_EDITOR
            if (!TagtagIdentityBegin(provider, configuration?.googleClientId, configuration?.googleReversedClientId))
            { failure("Sign-in could not start."); return; }
            request = StartCoroutine(Poll(success, failure));
#else
            failure("Apple and Google sign-in require an iPhone build.");
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        private IEnumerator Poll(Action<IdentityCredential> success, Action<string> failure)
        {
            var deadline = Time.realtimeSinceStartup + 180f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var pointer = TagtagIdentityPoll();
                if (pointer != IntPtr.Zero)
                {
                    IdentityResult result;
                    try { result = JsonUtility.FromJson<IdentityResult>(Marshal.PtrToStringAnsi(pointer)); }
                    finally { TagtagIdentityFree(pointer); }
                    request = null;
                    if (result == null || !string.IsNullOrEmpty(result.error) || string.IsNullOrEmpty(result.idToken))
                        failure(result?.error ?? "Sign-in did not return a credential.");
                    else success(new IdentityCredential
                    {
                        providerId = result.providerId,
                        idToken = result.idToken,
                        accessToken = result.accessToken,
                        rawNonce = result.rawNonce
                    });
                    yield break;
                }
                yield return null;
            }
            TagtagIdentityCancel();
            request = null;
            failure("Sign-in timed out. Try again.");
        }
#endif

        public void StoreSession(string value)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (string.IsNullOrEmpty(value)) TagtagSessionClear();
            else if (!TagtagSessionStore(value)) Debug.LogWarning("Secure session storage is unavailable.");
#endif
        }

        public string LoadSession()
        {
#if UNITY_IOS && !UNITY_EDITOR
            var pointer = TagtagSessionLoad();
            if (pointer == IntPtr.Zero) return null;
            try { return Marshal.PtrToStringAnsi(pointer); }
            finally { TagtagIdentityFree(pointer); }
#else
            return null;
#endif
        }

        public void ClearSession()
        {
#if UNITY_IOS && !UNITY_EDITOR
            TagtagSessionClear();
#endif
        }

        private void OnDestroy()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (request != null) TagtagIdentityCancel();
#endif
        }
    }
}
