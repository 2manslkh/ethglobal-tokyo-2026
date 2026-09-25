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

    public static class PaperFlow
    {
        public const string EmptyBookInvitation = "Your next little discovery is out there.";

        public static string StatusMessage(AppState state, bool invitationAlreadyShown)
        {
            if (state == null) return "";
            if (!string.IsNullOrWhiteSpace(state.error)) return state.error;
            string message = state.status ?? "";
            return invitationAlreadyShown && message == EmptyBookInvitation ? "" : message;
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

        public static bool ShouldClearPublishedDraft(bool submitted, AppState state)
        {
            return submitted && state != null && !state.busy && !state.hasPendingPublication &&
                string.IsNullOrEmpty(state.error) && string.IsNullOrEmpty(state.selectedPreset) &&
                string.IsNullOrEmpty(state.draftPlace) && string.IsNullOrEmpty(state.draftTeaser) &&
                string.IsNullOrEmpty(state.draftNote);
        }

        public static bool ShouldAnimateCollection(AppPage previousPage, AppState state, string lastPresentedId)
        {
            return previousPage == AppPage.Stick && state != null && state.page == AppPage.Home &&
                !string.IsNullOrEmpty(state.detail?.id) && state.detail.id != lastPresentedId;
        }
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
