using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tagtag
{
    public enum AppPage { Home, Stick, Explore }
    public enum CameraPresentationState { Inactive, Preparing, Live, PermissionDenied, Unavailable, Interrupted, Failed }
    [Serializable] public sealed class LocationFix
    {
        public double latitude, longitude;
        public float accuracyMeters;
        public long measuredUnixSeconds;
    }
    [Serializable] public class StickerSummary
    {
        public string id, presetId, authorId, authorName, place, teaser;
        public double latitude, longitude;
        public int revision;
        public long createdAt;
    }
    [Serializable] public sealed class CollectedSticker : StickerSummary
    {
        public string note;
        public long collectedAt;
        public bool unavailable;
    }
    [Serializable] public sealed class SpatialSnapshot
    {
        public string worldMapBase64;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public float widthMeters = 0.2f;
    }
    [Serializable] public sealed class RecoveryData
    {
        public StickerSummary sticker;
        public SpatialSnapshot snapshot;
        public string discoveryId;
        public long expiresAt;
    }
    [Serializable] public sealed class PlacementDraft
    {
        public string operationId, presetId, place, teaser, note;
        public LocationFix location;
        public SpatialSnapshot snapshot;
    }
    [Serializable] public sealed class IdentityCredential
    {
        public string providerId, idToken, accessToken, rawNonce;
    }
    [Serializable] public sealed class UserSession
    {
        public string uid, displayName, idToken, refreshToken;
        public long expiresAt;
    }
    [Serializable] public sealed class ServiceConfiguration
    {
        public string apiBaseUrl, firebaseApiKey, googleClientId, googleReversedClientId;
        public bool Configured => !string.IsNullOrEmpty(apiBaseUrl) && !string.IsNullOrEmpty(firebaseApiKey);
    }
    public sealed class AppState
    {
        public AppPage page;
        public UserSession user;
        public LocationFix location;
        public List<CollectedSticker> collection = new List<CollectedSticker>();
        public List<StickerSummary> nearby = new List<StickerSummary>();
        public List<StickerSummary> authored = new List<StickerSummary>();
        public StickerSummary selected;
        public CollectedSticker detail;
        public string status = "", error = "", selectedPreset = "", draftPlace = "", draftTeaser = "", draftNote = "";
        public bool busy, nearbyLoading, accountOpen, servicesConfigured, hasPendingPublication;
    }
    public interface IArExperience
    {
        event Action Changed;
        event Action<string> StickerTapped;
        CameraPresentationState CameraPresentation { get; }
        bool IsTracking { get; }
        bool CanPublish { get; }
        bool CanCollect { get; }
        string Status { get; }
        void Enter();
        void Exit();
        void SelectPreset(string presetId);
        void CancelPlacement();
        void Capture(Action<SpatialSnapshot> success, Action<string> failure);
        void Recover(RecoveryData recovery);
    }
    public interface IMapExperience
    {
        event Action<string> StickerSelected;
        void Show(Rect screenRect, LocationFix location, IReadOnlyList<StickerSummary> stickers);
        void Hide();
        void Recenter(LocationFix location);
    }
    public interface INativeIdentity
    {
        void SignIn(string provider, ServiceConfiguration configuration, Action<IdentityCredential> success, Action<string> failure);
        void StoreSession(string value);
        string LoadSession();
        void ClearSession();
    }
    public interface ITagtagController
    {
        AppState State { get; }
        IArExperience Ar { get; }
        IMapExperience Map { get; }
        event Action Changed;
        void Navigate(AppPage page);
        void SetAccountOpen(bool open);
        void SignIn(string provider);
        void SignOut();
        void RefreshNearby();
        void SelectSticker(string id);
        void StartDiscovery();
        void SelectPreset(string presetId);
        void SetDraft(string place, string teaser, string note);
        void Publish();
        void CancelPlacement();
        void OpenCollected(string id);
        void CloseDetail();
        void Report(string id, string reason);
        void Block(string authorId);
        void Withdraw(string id);
        void DeleteAccount();
    }
}
