using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Tagtag.Services
{
    public sealed class NativeLocationConfirmation : MonoBehaviour, ILocationConfirmation
    {
        [Serializable] private sealed class Result
        {
            public string requestId, status;
            public double latitude, longitude;
        }
        private Action<ConfirmedLocation> callback;
        private string requestId;
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void TagtagConfirmLocationOpen(double latitude,
            double longitude, float accuracy, bool locked, double pinLatitude, double pinLongitude, string receiver, string requestId);
        [DllImport("__Internal")] private static extern void TagtagConfirmLocationCancel();
#endif
        public void Open(LocationFix measured, Action<ConfirmedLocation> completed, ConfirmedLocation fixedSpot = null)
        {
            if (callback != null) { completed(null); return; }
#if UNITY_IOS && !UNITY_EDITOR
            callback = completed;
            requestId = Guid.NewGuid().ToString("N");
            TagtagConfirmLocationOpen(measured.latitude, measured.longitude, measured.accuracyMeters,
                fixedSpot != null, fixedSpot?.latitude ?? 0, fixedSpot?.longitude ?? 0, gameObject.name, requestId);
#else
            completed(null);
#endif
        }
        public void Cancel()
        {
            var pending = callback;
            callback = null;
            requestId = null;
#if UNITY_IOS && !UNITY_EDITOR
            TagtagConfirmLocationCancel();
#endif
            pending?.Invoke(null);
        }
        [UnityEngine.Scripting.Preserve]
        public void OnLocationConfirmed(string json)
        {
            Result result;
            try { result = JsonUtility.FromJson<Result>(json); }
            catch { return; }
            if (callback == null || result == null || result.requestId != requestId) return;
            var pending = callback;
            callback = null;
            requestId = null;
            pending(result.status == "confirmed" ? new ConfirmedLocation
                { latitude = result.latitude, longitude = result.longitude } : null);
        }
        private void OnDestroy() { Cancel(); }
    }
}
