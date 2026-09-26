using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Tagtag.AR
{
    public sealed class NativeMapView : MonoBehaviour, IMapExperience
    {
        [Serializable] private sealed class MapPin
        {
            public string id;
            public string presetId, thumbnailUrl;
            public string place;
            public double latitude;
            public double longitude;
        }

        [Serializable] private sealed class MapPins { public MapPin[] items; }

        public event Action<string> StickerSelected;
        private bool visible;
        private string lastPins;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void TagtagMapShow(float x, float y, float width, float height,
            float screenWidth, float screenHeight, double latitude, double longitude, string pins);
        [DllImport("__Internal")] private static extern void TagtagMapHide();
        [DllImport("__Internal")] private static extern void TagtagMapDispose();
        [DllImport("__Internal")] private static extern void TagtagMapSetReducedMotion(bool reduced);
        [DllImport("__Internal")] private static extern void TagtagMapRecenter(double latitude, double longitude);
        [DllImport("__Internal")] private static extern IntPtr TagtagMapPoll();
        [DllImport("__Internal")] private static extern void TagtagMapFree(IntPtr pointer);
#endif

        // screenRect is in Unity Screen pixels: bottom-left origin, width and height in pixels.
        // The native bridge converts it to UIKit points with a top-left origin.
        public void Show(Rect screenRect, LocationFix location, IReadOnlyList<StickerSummary> stickers)
        {
            if (screenRect.width <= 0 || screenRect.height <= 0 || location == null) { Hide(); return; }
            var pins = new MapPins { items = new MapPin[stickers == null ? 0 : stickers.Count] };
            for (var i = 0; i < pins.items.Length; i++)
            {
                var item = stickers[i];
                pins.items[i] = new MapPin
                {
                    id = item.id, presetId = item.presetId, thumbnailUrl = item.thumbnailUrl, place = item.place,
                    latitude = item.latitude, longitude = item.longitude
                };
            }
            var encoded = JsonUtility.ToJson(pins);
#if UNITY_IOS && !UNITY_EDITOR
            TagtagMapSetReducedMotion(PlayerPrefs.GetInt("tagtag.reducedMotion", 0) != 0);
            TagtagMapShow(screenRect.x, screenRect.y, screenRect.width, screenRect.height,
                Screen.width, Screen.height, location.latitude, location.longitude,
                encoded == lastPins ? null : encoded);
#endif
            lastPins = encoded;
            visible = true;
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
