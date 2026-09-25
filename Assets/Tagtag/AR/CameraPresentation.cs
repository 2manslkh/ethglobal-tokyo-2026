using System;
using UnityEngine.XR.ARFoundation;

namespace Tagtag.AR
{
    public enum CameraAuthorizationAction { Request, Start, Deny, Fail }

    public readonly struct CameraAuthorizationDecision
    {
        public CameraAuthorizationAction Action { get; }
        public bool UnityDisagrees { get; }

        public CameraAuthorizationDecision(CameraAuthorizationAction action, bool unityDisagrees)
        {
            Action = action;
            UnityDisagrees = unityDisagrees;
        }
    }

    public static class CameraAuthorizationPolicy
    {
        // Native values are normalized in TagtagCameraPermission.mm to Apple's four video authorization states.
        public static CameraAuthorizationDecision Decide(int nativeStatus, bool unityAuthorized)
        {
            switch (nativeStatus)
            {
                case 0: return new CameraAuthorizationDecision(CameraAuthorizationAction.Request, unityAuthorized);
                case 1:
                case 2: return new CameraAuthorizationDecision(CameraAuthorizationAction.Deny, unityAuthorized);
                case 3: return new CameraAuthorizationDecision(CameraAuthorizationAction.Start, !unityAuthorized);
                default: return new CameraAuthorizationDecision(CameraAuthorizationAction.Fail, false);
            }
        }
    }

    // iOS permission sheets can change focus and pause in either order.
    public sealed class CameraSuspension
    {
        private bool applicationPaused;
        private bool focused = true;

        public bool IsSuspended => applicationPaused || !focused;

        public void SetPaused(bool value) { applicationPaused = value; }
        public void SetFocused(bool value) { focused = value; }
    }

    // Camera pixels and world tracking are independent: the feed can be visible while AR relocalizes.
    public sealed class CameraPresentation
    {
        private const double FrameMaxAgeSeconds = 1.5d;
        private const double StartupTimeoutSeconds = 15d;
        private bool permissionGranted;
        private double startedAt;
        private double lastFrameAt;

        public event Action Changed;
        public CameraPresentationState State { get; private set; } = CameraPresentationState.Inactive;

        public void Begin(double now)
        {
            permissionGranted = false;
            startedAt = now;
            lastFrameAt = 0d;
            SetState(CameraPresentationState.Preparing);
        }

        public void ResetFrame(double now)
        {
            if (State == CameraPresentationState.Inactive || State == CameraPresentationState.PermissionDenied ||
                State == CameraPresentationState.Unavailable) return;
            startedAt = now;
            lastFrameAt = 0d;
            SetState(CameraPresentationState.Preparing);
        }

        public void Exit()
        {
            permissionGranted = false;
            lastFrameAt = 0d;
            SetState(CameraPresentationState.Inactive);
        }

        public void Interrupt()
        {
            if (State == CameraPresentationState.Inactive) return;
            lastFrameAt = 0d;
            SetState(CameraPresentationState.Interrupted);
        }

        public void PermissionGranted(double now)
        {
            if (State != CameraPresentationState.Preparing) return;
            permissionGranted = true;
            startedAt = now;
        }

        public void PermissionDenied()
        {
            if (State == CameraPresentationState.Inactive) return;
            permissionGranted = false;
            lastFrameAt = 0d;
            SetState(CameraPresentationState.PermissionDenied);
        }

        public void StartupFailed()
        {
            if (State == CameraPresentationState.Inactive) return;
            permissionGranted = false;
            lastFrameAt = 0d;
            SetState(CameraPresentationState.Failed);
        }

        public void ObserveSession(ARSessionState sessionState)
        {
            if (State == CameraPresentationState.Inactive || State == CameraPresentationState.PermissionDenied) return;
            if (sessionState == ARSessionState.Unsupported || sessionState == ARSessionState.NeedsInstall)
            {
                lastFrameAt = 0d;
                SetState(CameraPresentationState.Unavailable);
            }
        }

        public void ObserveFrame(double now, bool displayable)
        {
            if (!permissionGranted || State == CameraPresentationState.Inactive ||
                State == CameraPresentationState.Unavailable || State == CameraPresentationState.Failed) return;
            if (!displayable)
            {
                if (State == CameraPresentationState.Live)
                {
                    lastFrameAt = 0d;
                    SetState(CameraPresentationState.Interrupted);
                }
                return;
            }
            lastFrameAt = now;
            SetState(CameraPresentationState.Live);
        }

        public void Tick(double now)
        {
            if (!permissionGranted) return;
            if (State == CameraPresentationState.Live && now - lastFrameAt > FrameMaxAgeSeconds)
            {
                lastFrameAt = 0d;
                SetState(CameraPresentationState.Interrupted);
            }
            else if (State == CameraPresentationState.Preparing && now - startedAt >= StartupTimeoutSeconds)
                SetState(CameraPresentationState.Failed);
        }

        public static bool CanDisplayFrame(int textureCount, bool validTextures, bool backgroundEnabled,
            bool hasMaterial, bool renderingModeAvailable)
        {
            return textureCount > 0 && validTextures && backgroundEnabled && hasMaterial && renderingModeAvailable;
        }

        private void SetState(CameraPresentationState value)
        {
            if (State == value) return;
            State = value;
            Changed?.Invoke();
        }
    }
}
