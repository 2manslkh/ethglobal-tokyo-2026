using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tagtag
{
    public enum AppPage { Home, Stick, Explore }
    public enum CameraPresentationState { Inactive, Preparing, Live, PermissionDenied, Unavailable, Interrupted, Failed }
    public enum PlacementScanState { FindingSurface, SurfaceReady, Placed, Ready }
    public enum ReferencePhotoState { None, Loading, Ready, Unavailable }
    [Serializable] public sealed class LocationFix
    {
        public double latitude, longitude;
        public float accuracyMeters;
        public long measuredUnixSeconds;
    }
    [Serializable] public sealed class ConfirmedLocation
    {
        public double latitude, longitude;
    }
    public interface ILocationConfirmation
    {
        void Open(LocationFix measured, Action<ConfirmedLocation> completed, ConfirmedLocation fixedSpot = null);
        void Cancel();
    }
    [Serializable] public class StickerSummary
    {
        public string id, presetId, authorId, authorName, place, teaser;
        public string status;
        public string designId, artworkUrl, thumbnailUrl;
        public int artworkWidth, artworkHeight;
        public double latitude, longitude;
        public int revision;
        public long createdAt;
    }
    [Serializable] public sealed class CollectedSticker : StickerSummary
    {
        public string note;
        public long collectedAt;
        public bool unavailable;
        public NftStatus nft;
    }
    [Serializable] public sealed class NftStatus
    {
        public string status, contractAddress, tokenId, transactionHash;
        public int chainId;
    }
    [Serializable] public sealed class NftTransfer
    {
        public string stickerId, contractAddress, tokenId, recipient, transactionHash, status, message;
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
        public string operationId, presetId, designId, place, teaser, note;
        public LocationFix location;
        public SpatialSnapshot snapshot;
        public ConfirmedLocation confirmedLocation;
        public bool locationConfirmed, hasPublicationLocation;
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
        public bool nftEnabled;
        public string thirdwebClientId;
        public bool Configured => !string.IsNullOrEmpty(apiBaseUrl) && !string.IsNullOrEmpty(firebaseApiKey);
    }
    [Serializable] public sealed class StickerDesign
    {
        public string id, ownerId, name, kind, artworkUrl, thumbnailUrl;
        public int width, height, revision;
        public long createdAt;
    }
    public interface ICustomArtworkAr
    {
        void SelectArtwork(StickerDesign design, Texture2D texture);
        void SuspendForCreation(bool suspended);
    }
    public interface IReferencePhotoAr
    {
        Texture2D ReferencePhoto { get; }
        ReferencePhotoState PhotoState { get; }
    }
    public sealed class AppState
    {
        public AppPage page;
        public List<StickerDesign> designs = new List<StickerDesign>();
        public bool creationOpen, designsLoading, hasPendingDesign;
        public int creationCapabilities;
        public string selectedDesign = "";
        public UserSession user;
        public LocationFix location;
        public List<CollectedSticker> collection = new List<CollectedSticker>();
        public List<StickerSummary> nearby = new List<StickerSummary>();
        public List<StickerSummary> authored = new List<StickerSummary>();
        public StickerSummary selected;
        public StickerSummary mapSelection;
        public List<StickerSummary> placements = new List<StickerSummary>();
        public bool placementsLoading, placementsLoaded, nearbyFindingLocation;
        public string placementsError = "", placementsNextCursor = "";
        public CollectedSticker detail;
        public string status = "", error = "", designError = "", selectedPreset = "", draftPlace = "", draftTeaser = "", draftNote = "";
        public bool busy, discoveryLoading, nearbyLoading, accountOpen, servicesConfigured, hasPendingPublication;
        public bool locationSettingsRequired;
        public bool nftEnabled;
        public string walletAddress = "", walletStatus = "";
        public List<NftTransfer> nftTransfers = new List<NftTransfer>();
        public bool nftDeletionAcknowledged;
    }
    public interface IArExperience
    {
        event Action Changed;
        event Action<string> StickerTapped;
        CameraPresentationState CameraPresentation { get; }
        PlacementScanState ScanState { get; }
        bool IsTracking { get; }
        bool CanPublish { get; }
        bool CanCollect { get; }
        bool HasPlacementSurface { get; }
        bool HasPlacementPreview { get; }
        bool HasTrackedPlacement { get; }
        bool PlacementBusy { get; }
        float PlacementWidthMeters { get; }
        float PlacementRotationDegrees { get; }
        string Status { get; }
        void Enter();
        void Exit();
        void SelectPreset(string presetId);
        void CancelPlacement();
        void SetCameraInteraction(Rect cameraScreenRect, bool blocked);
        void Place(Vector2 screenPoint);
        void AdjustPlacement(float widthMeters, float rotationDegrees, Vector2? screenPoint = null);
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
    public interface IMapLoadingExperience
    {
        event Action Changed;
        bool IsLoading { get; }
        string Error { get; }
        void Retry();
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
        void OpenCreation();
        void CloseCreation();
        void CreateSticker(string source);
        void RefreshDesigns();
        void SelectDesign(string id);
        void DeleteDesign(string id);
        void RetryDesignSave();
        void RefreshArtwork();
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
    [Serializable] public sealed class PlacementPage
    {
        public StickerSummary[] items;
        public string nextCursor;
    }
    public interface IPlacedLocationsController
    {
        void RefreshPlacements();
        void LoadMorePlacements();
        void OpenPlacedLocation(StickerSummary sticker);
    }
    public interface INftTransferController
    {
        void TransferNft(string stickerId, string recipient);
        void RefreshNftTransfers();
        void AcknowledgeNftLoss(bool acknowledged);
    }
}
