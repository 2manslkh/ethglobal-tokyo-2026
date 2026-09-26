using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Tagtag.Services
{
    public sealed partial class TagtagController : ITagtagController, INftTransferController
    {
        public AppState State { get; } = new AppState();
        public IArExperience Ar { get; }
        public IMapExperience Map { get; }
        public event Action Changed;
        private readonly INativeIdentity identity;
        private readonly ServiceConfiguration configuration;
        private readonly FirebaseSession session;
        private readonly TagtagApi api;
        private readonly LocalCollection cache;
        private readonly PendingPublication publications;
        private readonly EditablePublication editablePublications;
        private bool synchronizing;
        private bool nearbyRefreshQueued;
        private bool draftEditRejected;
        private readonly LocalBlocks localBlocks = new LocalBlocks();
        private HashSet<string> blockedAuthors = new HashSet<string>();
        private readonly DeviceLocation location;
        private readonly ILocationConfirmation locationConfirmation;
        private readonly Func<string, Task<PublicationLocationResult>> loadPublicationLocation;
        private readonly Func<CancellationToken, Task<LocationFix>> locateNearby;
        private readonly Func<LocationFix, Task<StickerSummary[]>> loadNearby;
        private CancellationTokenSource nearbyCancellation;
        private DateTimeOffset nearbyLastSuccess;
        private string nearbyCacheUserId;
        private const string NearbyLocationMessage = "Finding your location. You can keep exploring the app.";
        private const string NearbyFetchingMessage = "Loading nearby stickers. You can keep exploring the app.";
        private RecoveryData recovery;
        private PlacementDraft pendingDraft;
        private int accountGeneration;
        private int publicationGeneration;
        private readonly CancellationTokenSource lifetimeCancellation = new CancellationTokenSource();
        private CancellationTokenSource captureCancellation;
        private bool disposed;
        private bool loginGateActive;
        private bool suspended;
        private string publicationStage;
        private long publicationStageStarted;
        private readonly WalletBinding wallet;
        private bool refreshingNfts;
        private readonly NftTransfers transfers;
        private readonly NftTransferStore transferStore;

        public TagtagController(ServiceConfiguration configuration, IArExperience ar, IMapExperience map, INativeIdentity identity,
            Func<CancellationToken, Task<LocationFix>> locateNearby = null,
            Func<LocationFix, Task<StickerSummary[]>> loadNearby = null,
            DeviceLocation deviceLocation = null, IStickerCreation stickerCreation = null,
            Func<string, Task<string>> connectWallet = null,
            Func<string, Task<string>> signWalletMessage = null,
            Func<Task> disconnectWallet = null,
            Func<string, string, Task<string>> nftOwner = null,
            Func<string, string, string, Task<string>> transferNft = null,
            Func<string, Task<string>> transferStatus = null,
            Func<string, Task<PlacementPage>> loadPlacements = null,
            ILocationConfirmation locationConfirmation = null,
            Func<string, Task<PublicationLocationResult>> loadPublicationLocation = null)
        {
            this.locationConfirmation = locationConfirmation;
            this.configuration = configuration;
            this.identity = identity;
            location = deviceLocation ?? new DeviceLocation();
            Ar = ar; Map = map;
            api = new TagtagApi(configuration);
            session = new FirebaseSession(configuration, identity);
            this.loadPublicationLocation = loadPublicationLocation ?? (async operationId =>
                await api.Call<PublicationLocationResult>("GET", "/v1/publications/operations/" + Uri.EscapeDataString(operationId),
                    null, await session.Token()));
            State.nftEnabled = configuration.nftEnabled;
            if (configuration.nftEnabled && connectWallet != null && signWalletMessage != null && disconnectWallet != null)
            {
                wallet = new WalletBinding(connectWallet, signWalletMessage, disconnectWallet,
                    token => api.Call<WalletStatus>("GET", "/v1/wallet", null, token),
                    (address, token) => api.Call<WalletChallenge>("POST", "/v1/wallet/challenge", new WalletChallengeRequest { address = address }, token),
                    (challengeId, signature, token) => api.Call<WalletStatus>("POST", "/v1/wallet/bind",
                        new WalletBindRequest { challengeId = challengeId, signature = signature }, token));
                wallet.Changed += WalletChanged;
            }
            else if (configuration.nftEnabled) State.walletStatus = "delayed";
            transferStore = new NftTransferStore(System.IO.Path.Combine(Application.persistentDataPath, "nft-transfers"));
            if (nftOwner != null && transferNft != null && transferStatus != null)
                transfers = new NftTransfers(transferStore.Save, nftOwner, transferNft, transferStatus);
            // Map browsing does not prove presence. Match the API's 5 km accuracy allowance;
            // discovery and collection still use the default 50 m location check.
            this.locateNearby = locateNearby ?? (token => location.Current(token, maxAccuracyMeters: 5000));
            this.loadNearby = loadNearby ?? (async fix =>
                (await api.Call<SummaryList>("POST", "/v1/nearby", new LocationRequest { location = fix }, await session.Token(false))).items);
            this.loadPlacements = loadPlacements;
            cache = new LocalCollection(System.IO.Path.Combine(Application.persistentDataPath, "collections"));
            publications = new PendingPublication(System.IO.Path.Combine(Application.persistentDataPath, "publications"));
            editablePublications = new EditablePublication(System.IO.Path.Combine(Application.persistentDataPath, "editable-publications"));
            State.servicesConfigured = configuration.Configured;
            State.user = session.Current;
            nearbyCacheUserId = State.user?.uid;
            SyncPlacementIdentity();
            State.accountOpen = State.user == null;
            State.nftTransfers = transferStore.Read(State.user?.uid);
            State.collection = cache.Read(State.user?.uid);
            InitializeCreation(stickerCreation);
            RestorePublication();
            blockedAuthors = localBlocks.Read(State.user?.uid); ApplyBlocks();
            State.status = State.user == null ? "Your next little discovery is out there." : "Your sticker book is ready.";
            ar.Changed += Notify;
            ar.StickerTapped += Collect;
            map.StickerSelected += SelectSticker;
            if (map is IMapLoadingExperience loadingMap) loadingMap.Changed += Notify;
        }

        public void Start() { Resume(); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; accountGeneration++; publicationGeneration++;
            lifetimeCancellation.Cancel();
            captureCancellation?.Cancel();
            if (wallet != null) { wallet.Changed -= WalletChanged; _ = wallet.Clear(); }
            CancelNearby();
            location.Dispose();
            locationConfirmation?.Cancel();
            creation?.Cancel();
            if (creationObject != null) StickerArtwork.Release(creationObject);
            Ar.Changed -= Notify; Ar.StickerTapped -= Collect; Map.StickerSelected -= SelectSticker;
            if (Map is IMapLoadingExperience loadingMap) loadingMap.Changed -= Notify;
            Ar.Exit(); Map.Hide();
        }
        private void Notify()
        {
            if (disposed) return;
            if (nearbyCacheUserId != State.user?.uid)
            {
                nearbyCacheUserId = State.user?.uid;
                nearbyLastSuccess = default;
            }
            SyncPlacementIdentity();
            if (State.user == null)
            {
                // Enforce the login gate after sign-out, deletion, and asynchronous session loss.
                bool stopExperiences = !loginGateActive;
                loginGateActive = true;
                State.page = AppPage.Home;
                State.accountOpen = true;
                State.creationOpen = false;
                State.detail = null;
                Map.Hide();
                if (stopExperiences)
                {
                    // Disk caches and drafts stay scoped to their owner; no private presentation
                    // may survive into a different account whose initial synchronization fails.
                    State.collection.Clear();
                    State.authored.Clear();
                    State.designs.Clear();
                    State.nearby.Clear();
                    State.selected = null;
                    State.selectedPreset = State.selectedDesign = "";
                    State.draftPlace = State.draftTeaser = State.draftNote = "";
                    State.hasPendingDesign = State.hasPendingPublication = false;
                    pendingDraft = null;
                    recovery = null;
                    CancelNearby();
                    location.Stop();
                    creation?.Cancel();
                    Ar.Exit();
                }
            }
            else loginGateActive = false;
            Changed?.Invoke();
        }
        public void SetSuspended(bool value)
        {
            if (disposed || suspended == value) return;
            suspended = value;
            if (suspended)
            {
                publicationGeneration++;
                CancelNearby(true);
                captureCancellation?.Cancel();
                location.Suspend();
                locationConfirmation?.Cancel();
            }
            else
            {
                location.Resume();
                if (State.page == AppPage.Stick && !State.accountOpen &&
                    (!string.IsNullOrEmpty(State.selectedPreset) || !string.IsNullOrEmpty(State.selectedDesign) || pendingDraft != null)) location.Prewarm();
                ResumeNearby();
            }
        }
        public void Navigate(AppPage page)
        {
            if (!RequireAccount()) return;
            if (nativeCreationOpen || State.busy && !synchronizing) return;
            if (page != AppPage.Explore) CancelNearby();
            if (State.page == AppPage.Stick && page != AppPage.Stick) Ar.Exit();
            State.creationOpen = false;
            if (page == AppPage.Home || State.page == AppPage.Stick && page != AppPage.Stick) location.Stop();
            Map.Hide(); State.page = page; State.error = ""; State.locationSettingsRequired = false;
            State.detail = null; State.accountOpen = false;
            if (page == AppPage.Stick)
            {
                Ar.Enter();
                if (pendingDraft == null && !string.IsNullOrEmpty(State.selectedPreset))
                {
                    Ar.SelectPreset(State.selectedPreset);
                    if (!suspended) location.Prewarm();
                }
            }
            if (page == AppPage.Stick && pendingDraft == null && !string.IsNullOrEmpty(State.selectedDesign)) RestoreSelectedArtwork();
            Notify();
            if (page == AppPage.Explore)
            {
                UnityEngine.Debug.Log($"Explore entered at {Time.realtimeSinceStartup:F3}s");
                if (nearbyLastSuccess + TimeSpan.FromSeconds(60) <= DateTimeOffset.UtcNow) RefreshNearby();
            }
        }
        public void SetAccountOpen(bool open)
        {
            if (!RequireAccount()) return;
            if (open) { CancelNearby(true); if (State.page == AppPage.Stick) location.Stop(); }
            State.accountOpen = open; Map.Hide(); Notify();
            if (!open)
            {
                if (State.page == AppPage.Stick && !suspended &&
                    (!string.IsNullOrEmpty(State.selectedPreset) || !string.IsNullOrEmpty(State.selectedDesign) || pendingDraft != null)) location.Prewarm();
                ResumeNearby();
            }
        }
        public void SignIn(string provider)
        {
            if (State.busy || !State.servicesConfigured || (provider != "apple" && provider != "google")) return;
            CancelNearby();
            State.status = "Opening " + (provider == "apple" ? "Apple" : "Google") + "…";
            State.busy = true; State.error = ""; State.designError = ""; Notify();
            identity.SignIn(provider, configuration, credential =>
            {
                accountGeneration++;
                State.busy = false;
                Run(async () =>
                {
                    State.user = await session.SignIn(credential);
                    State.page = AppPage.Home;
                    State.accountOpen = false;
                    State.nftTransfers = transferStore.Read(State.user.uid);
                    State.nftDeletionAcknowledged = false;
                    EnsureWallet();
                    State.collection = cache.Read(State.user.uid);
                    designs.ClaimGuest(State.user.uid); RestoreDesigns();
                    RestorePublication();
                    blockedAuthors = localBlocks.Read(State.user.uid); ApplyBlocks();
                    State.status = "You're signed in. Welcome to tagtag.";
                    await SyncAccount();
                    if (State.hasPendingDesign)
                    {
                        try { await SaveCreatedDesign(); }
                        catch (Exception error)
                        {
                            State.designError = error is ApiFailure ? error.Message : "Your sticker is saved on this phone. Please retry the upload.";
                        }
                    }
                });
            }, message => { State.busy = false; State.error = message; Notify(); });
        }
        public void SignOut()
        {
            if (State.busy) return;
            CancelNearby();
            location.Stop();
            accountGeneration++; session.SignOut(); State.user = null; recovery = null;
            nearbyLastSuccess = default;
            ClearWallet();
            RestoreDesigns(); State.creationOpen = false; State.selectedDesign = "";
            blockedAuthors.Clear(); State.collection.Clear(); State.authored.Clear(); State.detail = null; State.selected = null;
            Ar.CancelPlacement(); pendingDraft = null; State.hasPendingPublication = false;
            State.draftNote = State.draftTeaser = State.draftPlace = State.selectedPreset = State.selectedDesign = "";
            State.status = "Signed out."; State.error = ""; State.designError = ""; State.locationSettingsRequired = false; Notify();
        }
        public void RefreshNearby() => RefreshNearby(false);

        private async void RefreshNearby(bool preserveForegroundError)
        {
            if (disposed || suspended || State.user == null || State.page != AppPage.Explore || State.accountOpen) return;
            if (State.busy) { nearbyRefreshQueued = true; return; }
            if (State.nearbyLoading) return;
            nearbyRefreshQueued = false;
            var request = nearbyCancellation = new CancellationTokenSource();
            int generation = accountGeneration;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            State.nearbyLoading = true;
            State.nearbyFindingLocation = true;
            if (!preserveForegroundError) { State.error = ""; State.locationSettingsRequired = false; }
            State.status = NearbyLocationMessage;
            Notify();
            try
            {
                var fix = await locateNearby(request.Token);
                if (!CurrentNearby(request, generation)) return;
                State.location = fix;
                State.locationSettingsRequired = false;
                State.nearbyFindingLocation = false;
                State.status = NearbyFetchingMessage;
                UnityEngine.Debug.Log($"Explore location ready in {ElapsedMilliseconds(started)} ms");
                Notify();
                var items = await loadNearby(fix);
                if (!CurrentNearby(request, generation)) return;
                State.nearby = (items ?? Array.Empty<StickerSummary>()).ToList();
                nearbyLastSuccess = DateTimeOffset.UtcNow;
                ApplyBlocks();
                foreach (var item in State.nearby) if (!string.IsNullOrEmpty(item.designId)) StickerArtwork.Authorize(item.designId);
                State.status = State.nearby.Count == 0 ? "No stickers nearby yet. Leave the first one." : "Little discoveries around you.";
                UnityEngine.Debug.Log($"Explore nearby ready in {ElapsedMilliseconds(started)} ms ({State.nearby.Count} pins)");
            }
            catch (OperationCanceledException)
            {
                if (CurrentNearby(request, generation)) State.status = "Nearby lookup stopped. Refresh to try again.";
            }
            catch (Exception error)
            {
                if (CurrentNearby(request, generation))
                {
                    if (!preserveForegroundError || string.IsNullOrEmpty(State.error))
                    {
                        State.error = error is ApiFailure ? error.Message : "Nearby stickers could not load. Please try again.";
                        State.locationSettingsRequired = error is ApiFailure failure && failure.LocationSettingsRequired;
                    }
                    State.status = State.nearby.Count == 0 ? "Refresh nearby to try again." : "Showing your last nearby results. Refresh to try again.";
                }
            }
            finally
            {
                bool identityChanged = !disposed && generation == accountGeneration && State.user != session.Current;
                if (identityChanged)
                {
                    State.user = session.Current; RestoreDesigns(); State.selectedDesign = "";
                    if (State.user == null) ClearWallet();
                }
                if (nearbyCancellation == request)
                {
                    nearbyCancellation = null;
                    State.nearbyLoading = false;
                    State.nearbyFindingLocation = false;
                    Notify();
                }
                else if (identityChanged) Notify();
                request.Dispose();
            }
        }

        private bool CurrentNearby(CancellationTokenSource request, int generation) =>
            !disposed && nearbyCancellation == request && !request.IsCancellationRequested &&
            generation == accountGeneration && State.user?.uid == session.Current?.uid &&
            State.page == AppPage.Explore && !State.accountOpen;

        private void CancelNearby(bool resumeOnReturn = false)
        {
            var previous = nearbyCancellation;
            nearbyRefreshQueued = resumeOnReturn && (State.nearbyLoading || nearbyRefreshQueued);
            nearbyCancellation = null;
            State.nearbyLoading = false;
            State.nearbyFindingLocation = false;
            if (State.status == NearbyLocationMessage || State.status == NearbyFetchingMessage) State.status = "";
            previous?.Cancel();
        }

        private void ResumeNearby()
        {
            if (nearbyRefreshQueued && !disposed && State.page == AppPage.Explore && !State.accountOpen && !State.busy)
                RefreshNearby(true);
        }
        public void SelectSticker(string id)
        {
            State.selected = State.nearby.FirstOrDefault(item => item.id == id) ??
                (State.mapSelection?.id == id ? State.mapSelection : null);
            State.error = ""; Notify();
        }
        public void StartDiscovery()
        {
            if (!RequireAccount() || State.selected == null) return;
            string id = State.selected.id;
            Run(async () =>
            {
                // Camera permission and live imagery must not wait for GPS or map downloads.
                recovery = null;
                Map.Hide(); State.page = AppPage.Stick; State.selectedDesign = ""; State.selectedPreset = ""; State.accountOpen = false;
                Ar.CancelPlacement();
                State.status = "Checking your location to find this sticker…";
                Ar.Enter();
                Notify();
                State.location = await location.Current();
                State.status = "Loading this sticker’s saved spot…";
                Notify();
                var result = await api.Call<RecoverResult>("POST", Path(id) + "/recover", new LocationRequest { location = State.location }, await session.Token());
                byte[] worldMap = await TagtagApi.Download(result.mapUrl);
                recovery = new RecoveryData { sticker = result.sticker, discoveryId = result.discoveryId, expiresAt = result.expiresAt,
                    snapshot = new SpatialSnapshot { worldMapBase64 = Convert.ToBase64String(worldMap), position = result.position,
                        rotation = result.rotation, widthMeters = result.widthMeters } };
                if (!string.IsNullOrEmpty(result.sticker.designId))
                {
                    var artwork = await StickerArtwork.Load(result.sticker.designId, result.sticker.artworkUrl);
                    if (artwork == null) throw new ApiFailure("Sticker artwork could not load. Try discovery again.");
                }
                Ar.Recover(recovery);
                State.status = "Look around slowly, then tap the sticker when it appears.";
            });
        }
        public void SelectPreset(string presetId)
        {
            if (!RequireAccount()) return;
            if (State.busy || !new[] { "taggi-1", "taggi-2", "taggi-3", "taggi-4" }.Contains(presetId)) return;
            if (State.selectedPreset == presetId && pendingDraft != null) return;
            string previousDesign = State.selectedDesign; State.selectedDesign = "";
            if (!SaveEditableDraft(presetId, State.draftPlace, State.draftTeaser, State.draftNote)) { State.selectedDesign = previousDesign; Notify(); return; }
            if (pendingDraft != null && !ClearPublication()) { State.selectedDesign = previousDesign; Notify(); return; }
            recovery = null; State.selected = null; State.selectedPreset = presetId;
            if (State.creationOpen || State.page != AppPage.Stick)
            { State.creationOpen = false; State.accountOpen = false; Map.Hide(); State.page = AppPage.Stick; Ar.Enter(); }
            location.Prewarm();
            Ar.SelectPreset(presetId); Notify();
        }
        public void SetDraft(string place, string teaser, string note)
        {
            if (disposed || State.busy) return;
            // Preserve the operation ID only while the payload stays the same.
            if (State.draftPlace != place || State.draftTeaser != teaser || State.draftNote != note)
            {
                if (!SaveEditableDraft(State.selectedPreset, place, teaser, note)) { draftEditRejected = true; Notify(); return; }
                if (pendingDraft != null && !ClearPublication()) { draftEditRejected = true; Notify(); return; }
            }
            draftEditRejected = false;
            State.draftPlace = place ?? ""; State.draftTeaser = teaser ?? ""; State.draftNote = note ?? "";
        }
        public void Publish()
        {
            if (!RequireAccount() || State.busy) return;
            if (draftEditRejected) { State.error = "Your edit could not be saved. Retry the edit before publishing."; Notify(); return; }
            if (pendingDraft == null && !Ar.CanPublish) { State.error = "Move slowly until the surface is mapped and Taggi is placed."; Notify(); return; }
            if (string.IsNullOrWhiteSpace(State.draftTeaser) || string.IsNullOrWhiteSpace(State.draftNote))
            { State.error = "Add a teaser and a note before publishing."; Notify(); return; }
            int operationGeneration = publicationGeneration;
            int operationAccount = accountGeneration;
            Run(async () =>
            {
                EnsurePublicationActive(operationGeneration, operationAccount);
                SetPublicationStage("permission", "Checking location access…");
                if (!SaveEditableDraft(State.selectedPreset, State.draftPlace, State.draftTeaser, State.draftNote))
                    throw new ApiFailure(State.error);
                location.CheckPermission(requirePrecise: locationConfirmation == null);
                Task<LocationFix> prepareFixTask = location.Current(lifetimeCancellation.Token,
                    locationConfirmation == null ? 100 : 5000, preferredAccuracyMeters: 100);
                if (pendingDraft == null)
                {
                    SetPublicationStage("map", "Saving this spot…");
                    var completion = new TaskCompletionSource<SpatialSnapshot>();
                    SpatialSnapshot snapshot;
                    using (var capture = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCancellation.Token))
                    {
                        captureCancellation = capture;
                        try
                        {
                            Ar.Capture(value => completion.TrySetResult(value), error => completion.TrySetException(new ApiFailure(error)));
                            Task finished = await Task.WhenAny(completion.Task, Task.Delay(20000, capture.Token));
                            EnsurePublicationActive(operationGeneration, operationAccount);
                            if (finished != completion.Task)
                                throw new ApiFailure("Mapping took too long. Try again after looking around.");
                            snapshot = await completion.Task;
                            EnsurePublicationActive(operationGeneration, operationAccount);
                        }
                        finally
                        {
                            if (captureCancellation == capture) captureCancellation = null;
                        }
                    }
                    pendingDraft = new PlacementDraft { operationId = Guid.NewGuid().ToString("N"), presetId = State.selectedPreset, designId = State.selectedDesign,
                        place = State.draftPlace.Trim(), teaser = State.draftTeaser.Trim(), note = State.draftNote.Trim(), snapshot = snapshot };
                    State.hasPendingPublication = true;
                    publications.Save(State.user.uid, pendingDraft);
                }
                EnsurePublicationActive(operationGeneration, operationAccount);
                SetPublicationStage("location-prepare", "Finding a precise location…");
                var prepareFix = await prepareFixTask;
                EnsurePublicationActive(operationGeneration, operationAccount);
                // Migrate legacy drafts before either precision branch so retries retain their original target.
                if (!pendingDraft.hasPublicationLocation && pendingDraft.location?.measuredUnixSeconds > 0)
                {
                    var savedLocation = await loadPublicationLocation(pendingDraft.operationId);
                    EnsurePublicationActive(operationGeneration, operationAccount);
                    if (savedLocation.found)
                    {
                        pendingDraft.confirmedLocation = savedLocation.publicationLocation;
                        pendingDraft.locationConfirmed = savedLocation.locationConfirmed;
                    }
                    else if (!pendingDraft.locationConfirmed)
                        pendingDraft.confirmedLocation = new ConfirmedLocation
                            { latitude = pendingDraft.location.latitude, longitude = pendingDraft.location.longitude };
                    pendingDraft.hasPublicationLocation = true;
                    publications.Save(State.user.uid, pendingDraft);
                }
                float prepareAccuracy = locationConfirmation == null ? 100 : 5000;
                if (!location.IsFresh(prepareFix, prepareAccuracy))
                {
                    prepareFix = await location.Current(lifetimeCancellation.Token, prepareAccuracy, preferredAccuracyMeters: 100);
                    EnsurePublicationActive(operationGeneration, operationAccount);
                }
                if (!pendingDraft.locationConfirmed && prepareFix.accuracyMeters > 100)
                {
                    SetPublicationStage("confirm-location", "Confirm your sticker’s spot on the map.");
                    var confirmation = new TaskCompletionSource<ConfirmedLocation>();
                    ConfirmedLocation fixedSpot = pendingDraft.hasPublicationLocation ? pendingDraft.confirmedLocation : null;
                    // Older saved drafts did not record a separate immutable publication pin.
                    if (fixedSpot == null && pendingDraft.location?.measuredUnixSeconds > 0)
                        fixedSpot = new ConfirmedLocation { latitude = pendingDraft.location.latitude,
                            longitude = pendingDraft.location.longitude };
                    locationConfirmation.Open(prepareFix, value => confirmation.TrySetResult(value), fixedSpot);
                    var confirmed = await confirmation.Task;
                    EnsurePublicationActive(operationGeneration, operationAccount);
                    if (confirmed == null)
                        throw new ApiFailure("Your sticker and note are saved. Publish when you’re ready to confirm the spot.");
                    pendingDraft.confirmedLocation = confirmed;
                    pendingDraft.locationConfirmed = true;
                    publications.Save(State.user.uid, pendingDraft);
                    if (!location.IsFresh(prepareFix, 5000))
                        prepareFix = await location.Current(lifetimeCancellation.Token, 5000);
                    EnsurePublicationActive(operationGeneration, operationAccount);
                }
                if (!pendingDraft.hasPublicationLocation)
                {
                    if (!pendingDraft.locationConfirmed)
                        pendingDraft.confirmedLocation = new ConfirmedLocation
                            { latitude = prepareFix.latitude, longitude = prepareFix.longitude };
                    pendingDraft.hasPublicationLocation = true;
                }
                pendingDraft.location = State.location = prepareFix;
                publications.Save(State.user.uid, pendingDraft);
                State.hasPendingPublication = true;
                byte[] bytes = Convert.FromBase64String(pendingDraft.snapshot.worldMapBase64);
                var draft = pendingDraft;
                SetPublicationStage("prepare", "Preparing your sticker…");
                string prepareToken = await session.Token();
                EnsurePublicationActive(operationGeneration, operationAccount);
                var result = await api.Call<PrepareResult>("POST", "/v1/publications/prepare", new PrepareRequest {
                    operationId = draft.operationId, presetId = draft.presetId, designId = draft.designId, place = draft.place, teaser = draft.teaser, note = draft.note,
                    location = draft.location, confirmedLocation = draft.confirmedLocation, locationConfirmed = draft.locationConfirmed, hasPublicationLocation = draft.hasPublicationLocation, position = draft.snapshot.position, rotation = draft.snapshot.rotation,
                    widthMeters = draft.snapshot.widthMeters, mapBytes = bytes.Length }, prepareToken);
                EnsurePublicationActive(operationGeneration, operationAccount);
                if (result.publicationLocation != null)
                {
                    draft.confirmedLocation = result.publicationLocation;
                    draft.hasPublicationLocation = true;
                    publications.Save(State.user.uid, draft);
                }
                if (!string.IsNullOrEmpty(result.uploadUrl))
                {
                    SetPublicationStage("upload", "Sending your sticker…");
                    await TagtagApi.Upload(result.uploadUrl, bytes, result.uploadHeaders);
                    EnsurePublicationActive(operationGeneration, operationAccount);
                }
                SetPublicationStage("location-finalize", "Checking your location again…");
                float finalAccuracy = !draft.locationConfirmed ? 100 : 5000;
                var finalizeFix = location.IsFresh(prepareFix, finalAccuracy)
                    ? prepareFix : await location.Current(lifetimeCancellation.Token, finalAccuracy);
                EnsurePublicationActive(operationGeneration, operationAccount);
                State.location = finalizeFix;
                SetPublicationStage("finalize", "Publishing your sticker…");
                string finalizeToken = await session.Token();
                EnsurePublicationActive(operationGeneration, operationAccount);
                var published = await api.Call<StickerResult>("POST", "/v1/publications/" + Uri.EscapeDataString(result.id) + "/finalize",
                    new FinalizeRequest { operationId = draft.operationId, location = State.location, confirmedLocation = draft.confirmedLocation, locationConfirmed = draft.locationConfirmed, hasPublicationLocation = draft.hasPublicationLocation }, finalizeToken);
                EnsurePublicationActive(operationGeneration, operationAccount);
                State.authored.RemoveAll(item => item.id == published.sticker.id); State.authored.Add(published.sticker);
                State.nearby.RemoveAll(item => item.id == published.sticker.id); State.nearby.Add(published.sticker);
                RefreshPlacements();
                try { editablePublications.Remove(State.user.uid); }
                catch { throw new ApiFailure("Published online. Local draft cleanup failed; retrying will not duplicate it."); }
                if (!ClearPublication()) throw new ApiFailure("Published online. Local draft cleanup failed; retrying will not duplicate it.");
                State.draftNote = State.draftTeaser = State.draftPlace = State.selectedPreset = State.selectedDesign = "";
                location.Stop();
                FinishPublicationStage(false);
                Ar.CancelPlacement(); State.status = "Taggi is out there. Your sticker is published!";
            });
        }
        public void CancelPlacement()
        {
            if (State.busy) return;
            try { editablePublications.Remove(State.user?.uid); }
            catch { State.error = "This device could not remove the saved draft."; Notify(); return; }
            if (!ClearPublication()) { Notify(); return; }
            location.Stop();
            recovery = null; State.selectedPreset = State.selectedDesign = ""; State.selected = null;
            State.draftPlace = State.draftTeaser = State.draftNote = "";
            Ar.CancelPlacement(); Notify();
        }
        private void Collect(string id)
        {
            if (State.busy || !CollectionBook.CanUnlock(recovery, id, Ar.CanCollect, Now)) return;
            if (!RequireAccount()) return;
            var discovered = recovery;
            Run(async () =>
            {
                State.location = await location.Current();
                if (!CollectionBook.CanUnlock(discovered, id, Ar.CanCollect, Now)) throw new ApiFailure("Move closer and tap the tracked sticker again.");
                var result = await api.Call<CollectionResult>("POST", Path(id) + "/collect", new CollectRequest {
                    discoveryId = discovered.discoveryId, location = State.location }, await session.Token());
                State.collection.RemoveAll(item => item.id == result.sticker.id);
                State.collection.Add(result.sticker); State.collection = CollectionBook.Normalize(State.collection);
                SaveCollection(); State.detail = result.sticker; State.page = AppPage.Home;
                location.Stop();
                Ar.Exit(); Map.Hide(); recovery = null; State.status = "A little discovery, now in your book.";
            });
        }
        public void OpenCollected(string id) { State.detail = State.collection.FirstOrDefault(item => item.id == id); Map.Hide(); Notify(); }
        public void CloseDetail() { State.detail = null; Notify(); }
        public void Report(string id, string reason)
        {
            if (!RequireAccount()) return;
            Run(async () => { await api.Call<OkResult>("POST", Path(id) + "/report", new ReportRequest { reason = reason }, await session.Token()); State.status = "Report received. Thank you for letting us know."; });
        }
        public void Block(string authorId)
        {
            if (!RequireAccount() || string.IsNullOrEmpty(authorId) || authorId == State.user.uid) return;
            blockedAuthors.Add(authorId); localBlocks.Save(State.user.uid, blockedAuthors);
            ApplyBlocks(); recovery = null; Ar.CancelPlacement(); SaveCollection();
            State.status = "This person's stickers are now hidden."; Notify();
            Run(async () =>
            {
                await api.Call<OkResult>("POST", "/v1/blocks", new BlockRequest { authorId = authorId }, await session.Token());
                await SyncAccount();
            });
        }
        private void ApplyBlocks()
        {
            State.nearby.RemoveAll(item => blockedAuthors.Contains(item.authorId));
            foreach (var item in State.collection)
                if (blockedAuthors.Contains(item.authorId)) { item.unavailable = true; item.note = ""; if (!string.IsNullOrEmpty(item.designId)) StickerArtwork.Forget(item.designId); }
            if (State.selected != null && blockedAuthors.Contains(State.selected.authorId)) State.selected = null;
            if (State.mapSelection != null && blockedAuthors.Contains(State.mapSelection.authorId)) State.mapSelection = null;
            if (State.detail != null && blockedAuthors.Contains(State.detail.authorId)) State.detail = null;
        }
        public void Withdraw(string id)
        {
            if (!RequireAccount()) return;
            Run(async () => { await api.Call<OkResult>("POST", Path(id) + "/withdraw", new object(), await session.Token()); State.authored.RemoveAll(item => item.id == id); State.nearby.RemoveAll(item => item.id == id); PlacementWithdrawn(id); State.status = "Sticker withdrawn from discovery."; });
        }
        public void DeleteAccount()
        {
            if (!RequireAccount()) return;
            if ((State.nftEnabled || State.collection.Any(item => !string.IsNullOrEmpty(item.nft?.status))) && !State.nftDeletionAcknowledged)
            {
                State.error = "Review your NFT transfers and acknowledge possible wallet access loss before deleting your account.";
                Notify(); return;
            }
            Run(async () =>
            {
                string uid = State.user.uid;
                await api.Call<OkResult>("DELETE", "/v1/account", null, await session.Token());
                foreach (var design in State.designs) StickerArtwork.Forget(design.id);
                foreach (var sticker in State.collection.Cast<StickerSummary>().Concat(State.authored).Concat(State.nearby))
                    if (!string.IsNullOrEmpty(sticker.designId)) StickerArtwork.Forget(sticker.designId);
                session.SignOut(); accountGeneration++;
                ClearWallet();
                try { cache.Remove(uid); publications.Remove(uid); editablePublications.Remove(uid); localBlocks.Remove(uid); transferStore.Remove(uid); designs.Remove(uid); }
                catch { /* Account access is revoked even when local file removal must wait. */ }
                State.user = null; RestoreDesigns(); blockedAuthors.Clear(); State.collection.Clear(); State.authored.Clear(); State.nearby.Clear(); State.detail = null; State.selected = null;
                pendingDraft = null; State.hasPendingPublication = false; recovery = null; Ar.CancelPlacement(); State.draftNote = State.draftTeaser = State.draftPlace = State.selectedPreset = State.selectedDesign = "";
                State.status = "Your account has been deleted.";
            });
        }
        private async Task SyncAccount()
        {
            foreach (string authorId in blockedAuthors.ToArray())
                await api.Call<OkResult>("POST", "/v1/blocks", new BlockRequest { authorId = authorId }, await session.Token());
            var collection = await api.Call<CollectionList>("GET", "/v1/collection", null, await session.Token());
            State.collection = CollectionBook.Normalize(collection.items);
            foreach (var item in State.collection) if (item.unavailable && !string.IsNullOrEmpty(item.designId)) StickerArtwork.Forget(item.designId);
            ApplyBlocks(); SaveCollection();
            if (State.detail != null) State.detail = State.collection.FirstOrDefault(item => item.id == State.detail.id);
            var authored = await api.Call<SummaryList>("GET", "/v1/authored", null, await session.Token());
            State.authored = (authored.items ?? Array.Empty<StickerSummary>()).ToList();
            await SyncDesigns();
            foreach (var item in State.collection.Where(item => !item.unavailable).Cast<StickerSummary>().Concat(State.authored))
                if (!string.IsNullOrEmpty(item.designId)) StickerArtwork.Authorize(item.designId);
            RefreshPlacements();
        }
        public void Resume()
        {
            if (!RequireAccount()) return;
            EnsureWallet();
            if (!State.busy && State.user != null && configuration.Configured)
                Run(async () => { synchronizing = true; try { await SyncAccount(); } finally { synchronizing = false; } }, true);
        }
        private void WalletChanged()
        {
            if (disposed) return;
            State.walletAddress = State.user == null ? "" : wallet.Address;
            State.walletStatus = State.user == null ? "" : wallet.Status;
            Notify();
        }
        private void ClearWallet()
        {
            State.walletAddress = State.walletStatus = "";
            State.nftTransfers = new List<NftTransfer>();
            State.nftDeletionAcknowledged = false;
            if (wallet != null) _ = wallet.Clear();
        }
        private void EnsureWallet()
        {
            if (disposed || wallet == null || session.Current == null) return;
            string uid = session.Current.uid;
            int generation = accountGeneration;
            _ = wallet.Ensure(uid, async () =>
            {
                if (disposed || generation != accountGeneration || session.Current?.uid != uid)
                    throw new OperationCanceledException();
                string token = await session.Token();
                if (disposed || generation != accountGeneration || session.Current?.uid != uid)
                    throw new OperationCanceledException();
                return token;
            });
        }
        public async void RefreshNfts()
        {
            if (disposed || !configuration.nftEnabled || State.busy || refreshingNfts || session.Current == null) return;
            EnsureWallet();
            if (!State.collection.Any(item => item.nft != null && (item.nft.status == "pending" || item.nft.status == "delayed"))) return;
            string uid = session.Current.uid;
            int generation = accountGeneration;
            refreshingNfts = true;
            try
            {
                var collection = await api.Call<CollectionList>("GET", "/v1/collection", null, await session.Token());
                if (disposed || generation != accountGeneration || session.Current?.uid != uid || State.busy) return;
                State.collection = CollectionBook.Normalize(collection.items); ApplyBlocks(); SaveCollection();
                if (State.detail != null) State.detail = State.collection.FirstOrDefault(item => item.id == State.detail.id);
                Notify();
            }
            catch { /* Keep the collected sticker; a later foreground refresh retries. */ }
            finally
            {
                refreshingNfts = false;
                if (!disposed && generation == accountGeneration && session.Current == null)
                {
                    State.user = null; ClearWallet(); Notify();
                }
            }
        }
        public void AcknowledgeNftLoss(bool acknowledged) { State.nftDeletionAcknowledged = acknowledged; Notify(); }
        public void TransferNft(string stickerId, string recipient)
        {
            if (!RequireAccount() || transfers == null) return;
            Run(async () =>
            {
                string uid = State.user.uid;
                await SyncAccount();
                EnsureWallet();
                if (wallet?.Status != "ready") throw new ApiFailure("Wait for your souvenir wallet to finish connecting, then retry.");
                var sticker = State.collection.FirstOrDefault(item => item.id == stickerId);
                await transfers.Send(uid, wallet.Address, sticker, (recipient ?? "").Trim(), State.nftTransfers);
                State.status = "Check the transfer status below before deleting your account.";
            });
        }
        public void RefreshNftTransfers()
        {
            if (!RequireAccount() || transfers == null) return;
            Run(async () =>
            {
                EnsureWallet();
                if (wallet?.Status != "ready") throw new ApiFailure("Wait for your souvenir wallet to finish connecting, then retry.");
                await transfers.Refresh(State.user.uid, wallet.Address, State.nftTransfers);
                State.status = "Transfer status refreshed.";
            });
        }
        private void RestorePublication()
        {
            pendingDraft = publications.Read(State.user?.uid);
            State.hasPendingPublication = pendingDraft != null;
            if (pendingDraft != null)
            {
                State.selectedPreset = pendingDraft.presetId;
                State.selectedDesign = pendingDraft.designId ?? "";
                State.draftPlace = pendingDraft.place; State.draftTeaser = pendingDraft.teaser; State.draftNote = pendingDraft.note;
                return;
            }
            var editable = editablePublications.Read(State.user?.uid);
            if (editable == null) return;
            State.selectedPreset = editable.presetId ?? "";
            State.selectedDesign = editable.designId ?? "";
            State.draftPlace = editable.place ?? "";
            State.draftTeaser = editable.teaser ?? "";
            State.draftNote = editable.note ?? "";
        }
        private bool SaveEditableDraft(string presetId, string place, string teaser, string note, bool designOperation = false)
        {
            if (State.user == null) return true;
            try
            {
                editablePublications.Save(State.user.uid, new EditablePublicationDraft
                { presetId = presetId ?? "", designId = State.selectedDesign ?? "", place = place ?? "", teaser = teaser ?? "", note = note ?? "" });
                return true;
            }
            catch
            {
                if (designOperation) State.designError = "This device could not save your draft. Try again before publishing.";
                else State.error = "This device could not save your draft. Try again before publishing.";
                return false;
            }
        }
        private bool ClearPublication(bool designOperation = false)
        {
            try { publications.Remove(State.user?.uid); }
            catch
            {
                if (designOperation) State.designError = "This device could not remove the saved draft.";
                else State.error = "This device could not remove the saved draft.";
                return false;
            }
            pendingDraft = null; State.hasPendingPublication = false;
            return true;
        }
        private void SaveCollection()
        {
            try { cache.Save(State.user.uid, State.collection); }
            catch { State.status = "Saved online. Offline storage is unavailable on this device."; }
        }
        private bool RequireAccount()
        {
            if (State.user != null) return true;
            State.accountOpen = true; State.status = "Sign in to leave a sticker or collect a discovery."; Map.Hide(); Notify(); return false;
        }
        private void SetPublicationStage(string stage, string status)
        {
            FinishPublicationStage(false);
            publicationStage = stage;
            publicationStageStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            State.status = status;
            Notify();
        }
        private void FinishPublicationStage(bool failed)
        {
            if (publicationStage == null) return;
            long ticks = System.Diagnostics.Stopwatch.GetTimestamp() - publicationStageStarted;
            long elapsedMs = ticks * 1000 / System.Diagnostics.Stopwatch.Frequency;
            string line = "[Tagtag publish] stage=" + publicationStage + " result=" + (failed ? "failed" : "done") +
                " elapsedMs=" + elapsedMs;
            if (failed) Debug.LogWarning(line); else Debug.Log(line);
            publicationStage = null;
        }
        private void EnsurePublicationActive(int generation, int account)
        {
            if (disposed || lifetimeCancellation.IsCancellationRequested || account != accountGeneration)
                throw new OperationCanceledException("Publication ended.");
            if (suspended || generation != publicationGeneration)
                throw new ApiFailure("Publishing was interrupted. Return to STICK and try again.");
        }
        private async void Run(Func<Task> work, bool preserveForegroundError = false, bool designOperation = false)
        {
            if (State.busy || disposed) return;
            CancelNearby(true);
            bool preserveError = preserveForegroundError &&
                (!string.IsNullOrEmpty(State.error) || State.locationSettingsRequired);
            State.busy = true;
            if (designOperation) State.designError = "";
            else if (!preserveError) { State.error = ""; State.locationSettingsRequired = false; }
            int generation = accountGeneration; Notify();
            try { await work(); }
            catch (Exception error)
            {
                if (!disposed && generation == accountGeneration)
                {
                    if (!designOperation && publicationStage != null)
                    {
                        FinishPublicationStage(true);
                        State.status = pendingDraft == null ? "Publishing stopped. Try again when you're ready." :
                            "Your draft is saved. Try publishing again.";
                    }
                    if (designOperation)
                        State.designError = error is ApiFailure ? error.Message : "Something went wrong. Please try again.";
                    else if (!preserveError)
                    {
                        State.error = error is ApiFailure ? error.Message : "Something went wrong. Please try again.";
                        State.locationSettingsRequired = error is ApiFailure failure && failure.LocationSettingsRequired;
                    }
                }
            }
            finally
            {
                if (!designOperation && publicationStage != null) FinishPublicationStage(true);
                if (!disposed)
                {
                    State.busy = false;
                    if (State.user?.uid != session.Current?.uid)
                    { State.user = session.Current; RestoreDesigns(); State.selectedDesign = ""; }
                    else State.user = session.Current;
                    if (State.user == null) ClearWallet();
                    Notify();
                    ResumeNearby();
                }
            }
        }
        private static string Path(string id) => "/v1/stickers/" + Uri.EscapeDataString(id);
        private static long ElapsedMilliseconds(long started) =>
            (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000 / System.Diagnostics.Stopwatch.Frequency;
        private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
