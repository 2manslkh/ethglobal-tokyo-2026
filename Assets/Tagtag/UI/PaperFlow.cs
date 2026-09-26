using System.Collections.Generic;

namespace Tagtag.UI
{
    public enum CameraRecovery { None, Retry, Settings }

    public readonly struct CameraMessage
    {
        public readonly bool Cover;
        public readonly string Title;
        public readonly string Detail;
        public readonly CameraRecovery Recovery;

        public CameraMessage(bool cover, string title, string detail, CameraRecovery recovery)
        {
            Cover = cover;
            Title = title;
            Detail = detail;
            Recovery = recovery;
        }
    }

    public readonly struct NearbyStatus
    {
        public readonly string EmptyTitle;
        public readonly string MapMessage;
        public readonly string LocationNotice;
        public readonly bool CanRefresh;

        public NearbyStatus(string emptyTitle, string mapMessage, string locationNotice, bool canRefresh)
        {
            EmptyTitle = emptyTitle;
            MapMessage = mapMessage;
            LocationNotice = locationNotice;
            CanRefresh = canRefresh;
        }
    }

    public readonly struct PaperStickState
    {
        public readonly string Title;
        public readonly string Guidance;
        public readonly bool CanWriteNote;

        public PaperStickState(string title, string guidance, bool canWriteNote)
        {
            Title = title;
            Guidance = guidance;
            CanWriteNote = canWriteNote;
        }
    }

    public static class PaperFlow
    {
        public const string EmptyBookInvitation = "Find your places, Collect your moments";
        private const string ServiceEmptyBookStatus = "Your next little discovery is out there.";

        public static bool ShowBottomNavigation(AppState state) => state == null ||
            state.accountOpen || state.page != AppPage.Stick;
        public static PaperStickState StickPlacement(AppState state, bool tracking, bool surface,
            bool preview, bool placementBusy)
        {
            bool selected = HasPlacementSelection(state);
            bool retry = state != null && state.hasPendingPublication;
            string artwork = !string.IsNullOrEmpty(state?.selectedDesign) ? "your sticker" : "Taggi";
            string guidance = !selected ? "Choose a sticker to place." :
                placementBusy ? "Placing " + artwork + "…" :
                !tracking ? "Move slowly to start tracking." :
                !surface && !preview ? "Scan a wall or table for a surface." :
                !preview ? "Surface found. Tap it to place " + artwork + "." :
                "Drag to move. Pinch to resize. Twist to rotate.";
            return new PaperStickState("Place Sticker", guidance,
                selected && (preview || retry) && state != null && !state.busy && !placementBusy);
        }
        public static bool HasPlacementSelection(AppState state) => state != null &&
            (!string.IsNullOrEmpty(state.selectedPreset) || !string.IsNullOrEmpty(state.selectedDesign));
        public static string PresetName(string presetId)
        {
            switch (presetId)
            {
                case "taggi-1": return "Taggi pose 1";
                case "taggi-2": return "Taggi pose 2";
                case "taggi-3": return "Taggi pose 3";
                case "taggi-4": return "Taggi pose 4";
                default: return "Taggi";
            }
        }
        public static string DiscoveryGuidance(StickerSummary sticker)
        {
            if (sticker == null) return "Look around slowly for Taggi.";
            string place = string.IsNullOrWhiteSpace(sticker.place) ? "the place" : sticker.place.Trim();
            string clue = string.IsNullOrWhiteSpace(sticker.teaser) ? "Look for the sticker." : sticker.teaser.Trim();
            return "Find " + place + ": " + clue +
                (string.IsNullOrEmpty(sticker.designId) ? " Tap Taggi in AR to unlock the full note." :
                    " Tap the sticker in AR to unlock the full note.");
        }
        public static bool ShowDiscoveryRetry(AppState state) => state?.selected != null &&
            !HasPlacementSelection(state);
        public static bool BlockCameraInteraction(AppState state, bool sheetOpen,
            CameraPresentationState camera, bool tracking = true) => state == null ||
                state.page != AppPage.Stick || state.accountOpen || state.busy || sheetOpen ||
                camera != CameraPresentationState.Live || !tracking;

        public static string StatusMessage(AppState state, bool invitationAlreadyShown)
        {
            if (state == null) return "";
            if (!string.IsNullOrWhiteSpace(state.error)) return state.error;
            string message = state.status ?? "";
            return invitationAlreadyShown && (message == EmptyBookInvitation || message == ServiceEmptyBookStatus) ? "" : message;
        }

        public static NearbyStatus Nearby(AppState state, bool hasMap)
        {
            bool loading = state != null && state.nearbyLoading;
            bool findingLocation = loading && state.nearbyFindingLocation;
            bool hasLocation = state?.location != null && state.location.accuracyMeters > 0f;
            int nearbyCount = state?.nearby?.Count ?? 0;
            string title = findingLocation ? "Finding your location" :
                loading ? "Loading nearby stickers" :
                nearbyCount == 0 ? "No stickers in view yet" : "Tap a sticker on the map";
            string mapMessage = !hasMap ? "Map is unavailable on this device." :
                !hasLocation ? findingLocation ? "Finding your location…" : "Location is unavailable." : "";
            string locationNotice = state != null && !state.servicesConfigured ?
                "Nearby stickers need a configured service." :
                hasLocation && state.location.accuracyMeters > 50f ?
                "Approximate location. Refresh nearby to update your position." :
                state != null && !hasLocation && !loading &&
                string.IsNullOrWhiteSpace(state?.error) ?
                "Location is not ready yet. Try refreshing nearby." : "";
            return new NearbyStatus(title, mapMessage, locationNotice, !loading);
        }

        public static CameraMessage Camera(CameraPresentationState state)
        {
            switch (state)
            {
                case CameraPresentationState.Live:
                    return new CameraMessage(false, "", "", CameraRecovery.None);
                case CameraPresentationState.PermissionDenied:
                    return new CameraMessage(true, "Allow camera access to find stickers in AR",
                        "Open Settings to allow access, then return to STICK.", CameraRecovery.Settings);
                case CameraPresentationState.Unavailable:
                    return new CameraMessage(true, "AR is unavailable here",
                        "Try STICK on a device that supports AR camera tracking.", CameraRecovery.None);
                case CameraPresentationState.Interrupted:
                    return new CameraMessage(true, "Camera paused",
                        "Return to the app and try the camera again.", CameraRecovery.Retry);
                case CameraPresentationState.Failed:
                    return new CameraMessage(true, "Camera could not start",
                        "Try starting the camera again.", CameraRecovery.Retry);
                default:
                    return new CameraMessage(true, "Getting the camera ready",
                        "Hold your phone up while the camera starts.", CameraRecovery.None);
            }
        }

        public static bool CanPresentPublish(string place, string teaser, string note,
            bool cameraCanPublish, bool busy, bool pending = false)
        {
            return !busy && (cameraCanPublish || pending) &&
                !string.IsNullOrWhiteSpace(place) && !string.IsNullOrWhiteSpace(teaser) &&
                !string.IsNullOrWhiteSpace(note);
        }

        public static string PublishNotice(string place, string teaser, string note,
            bool tracking, bool hasPlacement, bool placementTracked, bool cameraCanPublish,
            bool busy, bool pending, bool customDesign = false)
        {
            if (busy) return "Publishing is in progress.";
            if (pending) return "Your saved placement is ready to retry. Location is checked again after you tap.";

            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(place)) missing.Add("place");
            if (string.IsNullOrWhiteSpace(teaser)) missing.Add("clue");
            if (string.IsNullOrWhiteSpace(note)) missing.Add("note");
            if (missing.Count > 0)
            {
                string needed = missing.Count == 1 ? missing[0] : missing.Count == 2 ?
                    missing[0] + " and " + missing[1] :
                    string.Join(", ", missing.GetRange(0, missing.Count - 1).ToArray()) +
                    ", and " + missing[missing.Count - 1];
                return "Still needed: " + needed + ".";
            }
            if (!tracking) return "Move slowly until AR tracking is stable.";
            string artwork = customDesign ? "your sticker" : "Taggi";
            if (!hasPlacement) return "Place " + artwork + " on a tracked surface before publishing.";
            if (!placementTracked) return "Keep " + artwork + " visible until its surface anchor is tracked.";
            if (!cameraCanPublish) return "Scan around " + artwork + " from more angles until the spatial map is ready.";
            return "Ready to publish. Location is checked after you tap.";
        }

        public static bool ShouldClearPublishedDraft(bool submitted, AppState state)
        {
            return submitted && state != null && !state.busy && !state.hasPendingPublication &&
                string.IsNullOrEmpty(state.error) && !HasPlacementSelection(state) &&
                string.IsNullOrEmpty(state.draftPlace) && string.IsNullOrEmpty(state.draftTeaser) &&
                string.IsNullOrEmpty(state.draftNote);
        }

        public static bool ShouldOpenCollectedDetail(AppPage previousPage, AppState state, string lastPresentedId)
        {
            return previousPage == AppPage.Stick && state != null && state.page == AppPage.Home &&
                !string.IsNullOrEmpty(state.detail?.id) && state.detail.id != lastPresentedId;
        }
    }

    public static class PaperCreation
    {
        public static bool CanStart(string source, AppState state)
        {
            if (state == null || state.busy || state.hasPendingDesign) return false;
            int capabilities = state.creationCapabilities;
            switch (source)
            {
                case "import": return (capabilities & 1) != 0;
                case "polaroid": return (capabilities & (1 | 2)) != 0;
                case "ai": return (capabilities & 4) != 0;
                default: return false;
            }
        }

        public static string PolaroidNotice(int capabilities)
        {
            bool library = (capabilities & 1) != 0;
            bool camera = (capabilities & 2) != 0;
            if (library && camera) return "Choose a photo library image or use the front or rear camera, then crop and caption it.";
            if (library) return "Choose a photo library image, then crop and caption it.";
            if (camera) return "Use the front or rear camera, then crop and caption it.";
            return "Polaroid creation is unavailable on this device.";
        }
    }

    public static class PaperScan
    {
        private static readonly string[] Labels =
            { "Find surface", "Place sticker", "Scan surroundings", "Scan ready" };

        public static int StageIndex(PlacementScanState state)
        {
            switch (state)
            {
                case PlacementScanState.SurfaceReady: return 1;
                case PlacementScanState.Placed: return 2;
                case PlacementScanState.Ready: return 3;
                default: return 0;
            }
        }

        public static string Label(PlacementScanState state) => Labels[StageIndex(state)];
        public static string Label(int index) => Labels[index];
        public static int StageCount => Labels.Length;
    }

    public sealed class PaperActivation
    {
        private bool running;
        public bool TryBegin()
        {
            if (running) return false;
            running = true;
            return true;
        }
        public void End() { running = false; }
    }
}
