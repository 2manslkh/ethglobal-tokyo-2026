using System;
using System.Collections.Generic;

namespace Tagtag.UI
{
    public static class MapPresentation
    {
        private static IMapExperience targetedMap;
        private static string targetedId;
        private static double targetedLatitude, targetedLongitude;

        public static bool ShouldShow(AppState state, bool sheetOpen)
        {
            return state?.user != null && !string.IsNullOrEmpty(state.user.uid) && state.page == AppPage.Explore && !state.accountOpen && !sheetOpen;
        }

        public static IReadOnlyList<StickerSummary> Pins(AppState state)
        {
            var pins = new List<StickerSummary>();
            if (state?.nearby != null) pins.AddRange(state.nearby);
            var target = state?.mapSelection;
            if (target != null && !string.IsNullOrEmpty(target.id) &&
                !pins.Exists(pin => pin?.id == target.id)) pins.Add(target);
            return pins;
        }

        public static void SyncTarget(AppState state, IMapExperience map)
        {
            var target = state?.mapSelection;
            if (target == null || map == null || !ShouldShow(state, false))
            {
                targetedMap = null;
                targetedId = null;
                return;
            }
            if (ReferenceEquals(targetedMap, map) && targetedId == target.id &&
                targetedLatitude == target.latitude && targetedLongitude == target.longitude) return;
            targetedMap = map;
            targetedId = target.id;
            targetedLatitude = target.latitude;
            targetedLongitude = target.longitude;
            map.Recenter(new LocationFix { latitude = target.latitude, longitude = target.longitude });
        }

        public static bool SyncVisibility(AppState state, bool sheetOpen, IMapExperience map)
        {
            bool visible = ShouldShow(state, sheetOpen);
            if (!visible) map?.Hide();
            if (state == null || state.page != AppPage.Explore)
            {
                targetedMap = null;
                targetedId = null;
            }
            return visible;
        }
    }
}
