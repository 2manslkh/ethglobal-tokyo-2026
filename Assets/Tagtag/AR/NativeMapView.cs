using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Tagtag.AR
{
    public sealed class NativeMapView : MonoBehaviour, IMapExperience, IMapLoadingExperience
    {
        [Serializable] private sealed class MapPin
        {
            public string id;
            public string presetId, thumbnailUrl, thumbnailPath;
            public string place, teaser;
            public double latitude;
            public double longitude;
        }

        [Serializable] private sealed class MapPins { public MapPin[] items; }

        public event Action<string> StickerSelected;
        public event Action Changed;
        public bool IsLoading { get; private set; }
        public string Error { get; private set; } = "";
        private bool visible;
        private string lastPins;
        private LocationFix lastLocation;
        private bool firstMountLogged;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int TagtagMapShow(float x, float y, float width, float height,
            float screenWidth, float screenHeight, int hasLocation, double latitude, double longitude, string pins);
        [DllImport("__Internal")] private static extern void TagtagMapHide();
        [DllImport("__Internal")] private static extern void TagtagMapDispose();
        [DllImport("__Internal")] private static extern void TagtagMapSetReducedMotion(bool reduced);
        [DllImport("__Internal")] private static extern void TagtagMapRecenter(double latitude, double longitude);
        [DllImport("__Internal")] private static extern int TagtagMapLoadingStatus();
        [DllImport("__Internal")] private static extern void TagtagMapRetry();
        [DllImport("__Internal")] private static extern IntPtr TagtagMapPoll();
        [DllImport("__Internal")] private static extern void TagtagMapFree(IntPtr pointer);
#endif

        // screenRect is in Unity Screen pixels: bottom-left origin, width and height in pixels.
        // The native bridge converts it to UIKit points with a top-left origin.
        public void Show(Rect screenRect, LocationFix location, IReadOnlyList<StickerSummary> stickers)
        {
            if (screenRect.width <= 0 || screenRect.height <= 0) { Hide(); return; }
            bool firstMount = !firstMountLogged;
            if (location != null) lastLocation = location;
            var initialLocation = location ?? lastLocation;
            var pins = new MapPins { items = new MapPin[stickers == null ? 0 : stickers.Count] };
            for (var i = 0; i < pins.items.Length; i++)
            {
                var item = stickers[i];
                pins.items[i] = new MapPin
                {
                    id = item.id, presetId = item.presetId, thumbnailUrl = item.thumbnailUrl,
                    thumbnailPath = StickerArtwork.CachedPath(item.designId), place = item.place, teaser = item.teaser,
                    latitude = item.latitude, longitude = item.longitude
                };
            }
            var encoded = JsonUtility.ToJson(pins);
#if UNITY_IOS && !UNITY_EDITOR
            TagtagMapSetReducedMotion(false);
            if (TagtagMapShow(screenRect.x, screenRect.y, screenRect.width, screenRect.height,
                Screen.width, Screen.height, initialLocation != null ? 1 : 0,
                initialLocation?.latitude ?? 0, initialLocation?.longitude ?? 0,
                encoded == lastPins ? null : encoded) == 0) return;
#endif
            if (firstMount)
            {
                firstMountLogged = true;
                Debug.Log($"Explore native map mounted at {Time.realtimeSinceStartup:F3}s");
            }
            lastPins = encoded;
            visible = true;
#if UNITY_IOS && !UNITY_EDITOR
            if (firstMount) { IsLoading = true; Changed?.Invoke(); }
#endif
        }

        public void Retry()
        {
            if (!visible) return;
            IsLoading = true;
            Error = "";
#if UNITY_IOS && !UNITY_EDITOR
            TagtagMapRetry();
#else
            IsLoading = false;
#endif
            Changed?.Invoke();
        }

        public void Hide()
        {
            if (!visible) return;
            visible = false;
#if UNITY_IOS && !UNITY_EDITOR
            TagtagMapHide();
#endif
        }

        public void Recenter(LocationFix location)
        {
            if (location == null) return;
#if UNITY_IOS && !UNITY_EDITOR
            if (visible) TagtagMapRecenter(location.latitude, location.longitude);
#endif
        }

        private void Update()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (!visible) return;
            int loadingStatus = TagtagMapLoadingStatus();
            bool isLoading = loadingStatus == 1;
            string error = loadingStatus == 2 ? "Map tiles could not load. Check your connection and retry." : "";
            if (IsLoading != isLoading || Error != error)
            {
                IsLoading = isLoading;
                Error = error;
                Changed?.Invoke();
            }
            var pointer = TagtagMapPoll();
            if (pointer == IntPtr.Zero) return;
            string id;
            try { id = Marshal.PtrToStringAnsi(pointer); }
            finally { TagtagMapFree(pointer); }
            if (!string.IsNullOrEmpty(id)) StickerSelected?.Invoke(id);
#endif
        }

        private void OnDestroy()
        {
            Hide();
#if UNITY_IOS && !UNITY_EDITOR
            TagtagMapDispose();
#endif
        }
    }
}
