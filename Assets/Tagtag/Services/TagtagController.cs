using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Tagtag.Services
{
    public sealed class TagtagController : ITagtagController
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
        private readonly Func<CancellationToken, Task<LocationFix>> locateNearby;
        private readonly Func<LocationFix, Task<StickerSummary[]>> loadNearby;
        private CancellationTokenSource nearbyCancellation;
        private const string NearbyLoadingMessage = "Finding nearby stickers. You can keep exploring the app.";
        private RecoveryData recovery;
        private PlacementDraft pendingDraft;
        private int accountGeneration;
        private int publicationGeneration;
        private readonly CancellationTokenSource lifetimeCancellation = new CancellationTokenSource();
        private CancellationTokenSource captureCancellation;
        private bool disposed;
        private bool suspended;
        private string publicationStage;
        private long publicationStageStarted;

        public TagtagController(ServiceConfiguration configuration, IArExperience ar, IMapExperience map, INativeIdentity identity,
            Func<CancellationToken, Task<LocationFix>> locateNearby = null,
            Func<LocationFix, Task<StickerSummary[]>> loadNearby = null,
            DeviceLocation deviceLocation = null)
        {
            this.configuration = configuration;
            this.identity = identity;
            location = deviceLocation ?? new DeviceLocation();
            Ar = ar; Map = map;
            api = new TagtagApi(configuration);
            session = new FirebaseSession(configuration, identity);
            this.locateNearby = locateNearby ?? (token => location.Current(token));
            this.loadNearby = loadNearby ?? (async fix =>
                (await api.Call<SummaryList>("POST", "/v1/nearby", new LocationRequest { location = fix }, await session.Token(false))).items);
            cache = new LocalCollection(System.IO.Path.Combine(Application.persistentDataPath, "collections"));
            publications = new PendingPublication(System.IO.Path.Combine(Application.persistentDataPath, "publications"));
            editablePublications = new EditablePublication(System.IO.Path.Combine(Application.persistentDataPath, "editable-publications"));
            State.servicesConfigured = configuration.Configured;
            State.user = session.Current;
            State.collection = cache.Read(State.user?.uid);
            RestorePublication();
            blockedAuthors = localBlocks.Read(State.user?.uid); ApplyBlocks();
            State.status = State.user == null ? "Your next little discovery is out there." : "Your sticker book is ready.";
            ar.Changed += Notify;
            ar.StickerTapped += Collect;
            map.StickerSelected += SelectSticker;
        }

        public void Start() { Resume(); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; accountGeneration++; publicationGeneration++;
            lifetimeCancellation.Cancel();
            captureCancellation?.Cancel();
            CancelNearby();
            location.Dispose();
            Ar.Changed -= Notify; Ar.StickerTapped -= Collect; Map.StickerSelected -= SelectSticker;
            Ar.Exit(); Map.Hide();
        }
        private void Notify() { if (!disposed) Changed?.Invoke(); }
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
            }
            else
            {
                location.Resume();
                if (State.page == AppPage.Stick && !State.accountOpen &&
                    (!string.IsNullOrEmpty(State.selectedPreset) || pendingDraft != null)) location.Prewarm();
                ResumeNearby();
            }
        }
        public void Navigate(AppPage page)
        {
            if (State.busy && !synchronizing) return;
            if (page != AppPage.Explore) CancelNearby();
            if (State.page == AppPage.Stick && page != AppPage.Stick) Ar.Exit();
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
            Notify();
            if (page == AppPage.Explore && State.nearby.Count == 0) RefreshNearby();
        }
        public void SetAccountOpen(bool open)
        {
            if (open) { CancelNearby(true); if (State.page == AppPage.Stick) location.Stop(); }
            State.accountOpen = open; Map.Hide(); Notify();
            if (!open)
            {
                if (State.page == AppPage.Stick && !suspended &&
                    (!string.IsNullOrEmpty(State.selectedPreset) || pendingDraft != null)) location.Prewarm();
                ResumeNearby();
            }
        }
        public void SignIn(string provider)
        {
            if (State.busy) return;
            CancelNearby();
            State.busy = true; State.error = ""; Notify();
            identity.SignIn(provider, configuration, credential =>
            {
                State.busy = false;
                Run(async () =>
                {
                    State.user = await session.SignIn(credential);
                    State.collection = cache.Read(State.user.uid);
                    RestorePublication();
                    blockedAuthors = localBlocks.Read(State.user.uid); ApplyBlocks();
                    State.status = "You're signed in. Welcome to tagtag.";
                    await SyncAccount();
                });
            }, message => { State.busy = false; State.error = message; Notify(); });
        }
        public void SignOut()
        {
            if (State.busy) return;
            CancelNearby();
            location.Stop();
            accountGeneration++; session.SignOut(); State.user = null; recovery = null;
            blockedAuthors.Clear(); State.collection.Clear(); State.authored.Clear(); State.detail = null; State.selected = null;
            Ar.CancelPlacement(); pendingDraft = null; State.hasPendingPublication = false;
            State.draftNote = State.draftTeaser = State.draftPlace = State.selectedPreset = "";
            State.status = "Signed out."; State.error = ""; State.locationSettingsRequired = false; Notify();
        }
        public void RefreshNearby() => RefreshNearby(false);

        private async void RefreshNearby(bool preserveForegroundError)
        {
            if (disposed || suspended || State.page != AppPage.Explore || State.accountOpen) return;
            if (State.busy) { nearbyRefreshQueued = true; return; }
            if (State.nearbyLoading) return;
            nearbyRefreshQueued = false;
            var request = nearbyCancellation = new CancellationTokenSource();
            int generation = accountGeneration;
            State.nearbyLoading = true;
            if (!preserveForegroundError) { State.error = ""; State.locationSettingsRequired = false; }
            State.status = NearbyLoadingMessage;
            Notify();
            try
            {
                var fix = await locateNearby(request.Token);
                if (!CurrentNearby(request, generation)) return;
                State.location = fix;
                State.locationSettingsRequired = false;
                Notify();
                var items = await loadNearby(fix);
                if (!CurrentNearby(request, generation)) return;
                State.nearby = (items ?? Array.Empty<StickerSummary>()).ToList();
                ApplyBlocks();
                State.status = State.nearby.Count == 0 ? "No stickers nearby yet. Leave the first one." : "Little discoveries around you.";
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (CurrentNearby(request, generation) && (!preserveForegroundError || string.IsNullOrEmpty(State.error)))
                {
                    State.error = error is ApiFailure ? error.Message : "Nearby stickers could not load. Please try again.";
                    State.locationSettingsRequired = error is ApiFailure failure && failure.LocationSettingsRequired;
                }
            }
            finally
            {
                bool identityChanged = !disposed && generation == accountGeneration && State.user != session.Current;
                if (identityChanged) State.user = session.Current;
                if (nearbyCancellation == request)
                {
                    nearbyCancellation = null;
                    State.nearbyLoading = false;
                    Notify();
                }
                else if (identityChanged) Notify();
                request.Dispose();
            }
        }

        private bool CurrentNearby(CancellationTokenSource request, int generation) =>
            !disposed && nearbyCancellation == request && !request.IsCancellationRequested &&
            generation == accountGeneration && State.page == AppPage.Explore && !State.accountOpen;

        private void CancelNearby(bool resumeOnReturn = false)
        {
            var previous = nearbyCancellation;
            nearbyRefreshQueued = resumeOnReturn && (State.nearbyLoading || nearbyRefreshQueued);
            nearbyCancellation = null;
            State.nearbyLoading = false;
            if (State.status == NearbyLoadingMessage) State.status = "";
            previous?.Cancel();
        }

        private void ResumeNearby()
        {
            if (nearbyRefreshQueued && !disposed && State.page == AppPage.Explore && !State.accountOpen && !State.busy)
                RefreshNearby(true);
        }
        public void SelectSticker(string id)
        {
            State.selected = State.nearby.FirstOrDefault(item => item.id == id);
            State.error = ""; Notify();
        }
        public void StartDiscovery()
        {
            if (!RequireAccount() || State.selected == null) return;
            string id = State.selected.id;
            Run(async () =>
            {
                State.location = await location.Current();
                var result = await api.Call<RecoverResult>("POST", Path(id) + "/recover", new LocationRequest { location = State.location }, await session.Token());
                byte[] worldMap = await TagtagApi.Download(result.mapUrl);
                recovery = new RecoveryData { sticker = result.sticker, discoveryId = result.discoveryId, expiresAt = result.expiresAt,
                    snapshot = new SpatialSnapshot { worldMapBase64 = Convert.ToBase64String(worldMap), position = result.position,
                        rotation = result.rotation, widthMeters = result.widthMeters } };
                Map.Hide(); State.page = AppPage.Stick; State.selectedPreset = ""; State.accountOpen = false;
                Ar.Enter(); Ar.Recover(recovery);
                State.status = "Look around slowly, then tap the sticker when it appears.";
            });
        }
        public void SelectPreset(string presetId)
        {
            if (State.busy || !new[] { "taggi-1", "taggi-2", "taggi-3", "taggi-4" }.Contains(presetId)) return;
            if (State.selectedPreset == presetId && pendingDraft != null) return;
            if (!SaveEditableDraft(presetId, State.draftPlace, State.draftTeaser, State.draftNote)) { Notify(); return; }
            if (pendingDraft != null && !ClearPublication()) { Notify(); return; }
            recovery = null; State.selected = null; State.selectedPreset = presetId;
            if (State.page == AppPage.Stick) location.Prewarm();
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
                location.CheckPermission();
                Task<LocationFix> prepareFixTask = location.Current(lifetimeCancellation.Token, 100);
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
                    pendingDraft = new PlacementDraft { operationId = Guid.NewGuid().ToString("N"), presetId = State.selectedPreset,
                        place = State.draftPlace.Trim(), teaser = State.draftTeaser.Trim(), note = State.draftNote.Trim(), snapshot = snapshot };
                    State.hasPendingPublication = true;
                    publications.Save(State.user.uid, pendingDraft);
                }
                EnsurePublicationActive(operationGeneration, operationAccount);
                SetPublicationStage("location-prepare", "Finding a precise location…");
                var prepareFix = await prepareFixTask;
                EnsurePublicationActive(operationGeneration, operationAccount);
                pendingDraft.location = State.location = prepareFix;
                publications.Save(State.user.uid, pendingDraft);
                State.hasPendingPublication = true;
                byte[] bytes = Convert.FromBase64String(pendingDraft.snapshot.worldMapBase64);
                var draft = pendingDraft;
                SetPublicationStage("prepare", "Preparing your sticker…");
                string prepareToken = await session.Token();
                EnsurePublicationActive(operationGeneration, operationAccount);
                var result = await api.Call<PrepareResult>("POST", "/v1/publications/prepare", new PrepareRequest {
                    operationId = draft.operationId, presetId = draft.presetId, place = draft.place, teaser = draft.teaser, note = draft.note,
                    location = draft.location, position = draft.snapshot.position, rotation = draft.snapshot.rotation,
                    widthMeters = draft.snapshot.widthMeters, mapBytes = bytes.Length }, prepareToken);
                EnsurePublicationActive(operationGeneration, operationAccount);
                if (!string.IsNullOrEmpty(result.uploadUrl))
                {
                    SetPublicationStage("upload", "Sending your sticker…");
                    await TagtagApi.Upload(result.uploadUrl, bytes, result.uploadHeaders);
                    EnsurePublicationActive(operationGeneration, operationAccount);
                }
                SetPublicationStage("location-finalize", "Checking your location again…");
                var finalizeFix = location.IsFresh(prepareFix, 100)
                    ? prepareFix : await location.Current(lifetimeCancellation.Token, 100);
                EnsurePublicationActive(operationGeneration, operationAccount);
                State.location = finalizeFix;
                SetPublicationStage("finalize", "Publishing your sticker…");
                string finalizeToken = await session.Token();
                EnsurePublicationActive(operationGeneration, operationAccount);
                var published = await api.Call<StickerResult>("POST", "/v1/publications/" + Uri.EscapeDataString(result.id) + "/finalize",
                    new FinalizeRequest { operationId = draft.operationId, location = State.location }, finalizeToken);
                EnsurePublicationActive(operationGeneration, operationAccount);
                State.authored.RemoveAll(item => item.id == published.sticker.id); State.authored.Add(published.sticker);
                State.nearby.RemoveAll(item => item.id == published.sticker.id); State.nearby.Add(published.sticker);
                try { editablePublications.Remove(State.user.uid); }
                catch { throw new ApiFailure("Published online. Local draft cleanup failed; retrying will not duplicate it."); }
                if (!ClearPublication()) throw new ApiFailure("Published online. Local draft cleanup failed; retrying will not duplicate it.");
                State.draftNote = State.draftTeaser = State.draftPlace = State.selectedPreset = "";
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
            recovery = null; State.selectedPreset = ""; State.selected = null;
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
                if (blockedAuthors.Contains(item.authorId)) { item.unavailable = true; item.note = ""; }
            if (State.selected != null && blockedAuthors.Contains(State.selected.authorId)) State.selected = null;
            if (State.detail != null && blockedAuthors.Contains(State.detail.authorId)) State.detail = null;
        }
        public void Withdraw(string id)
        {
            if (!RequireAccount()) return;
            Run(async () => { await api.Call<OkResult>("POST", Path(id) + "/withdraw", new object(), await session.Token()); State.authored.RemoveAll(item => item.id == id); State.nearby.RemoveAll(item => item.id == id); State.status = "Sticker withdrawn from discovery."; });
        }
        public void DeleteAccount()
        {
            if (!RequireAccount()) return;
            Run(async () =>
            {
                string uid = State.user.uid;
                await api.Call<OkResult>("DELETE", "/v1/account", null, await session.Token());
                session.SignOut(); accountGeneration++;
                try { cache.Remove(uid); publications.Remove(uid); editablePublications.Remove(uid); localBlocks.Remove(uid); }
                catch { /* Account access is revoked even when local file removal must wait. */ }
                State.user = null; blockedAuthors.Clear(); State.collection.Clear(); State.authored.Clear(); State.nearby.Clear(); State.detail = null; State.selected = null;
                pendingDraft = null; State.hasPendingPublication = false; recovery = null; Ar.CancelPlacement(); State.draftNote = State.draftTeaser = State.draftPlace = State.selectedPreset = "";
                State.status = "Your account has been deleted.";
            });
        }
        private async Task SyncAccount()
        {
            foreach (string authorId in blockedAuthors.ToArray())
                await api.Call<OkResult>("POST", "/v1/blocks", new BlockRequest { authorId = authorId }, await session.Token());
            var collection = await api.Call<CollectionList>("GET", "/v1/collection", null, await session.Token());
            State.collection = CollectionBook.Normalize(collection.items); ApplyBlocks(); SaveCollection();
            if (State.detail != null) State.detail = State.collection.FirstOrDefault(item => item.id == State.detail.id);
            var authored = await api.Call<SummaryList>("GET", "/v1/authored", null, await session.Token());
            State.authored = (authored.items ?? Array.Empty<StickerSummary>()).ToList();
        }
        public void Resume()
        {
            if (!State.busy && State.user != null && configuration.Configured)
                Run(async () => { synchronizing = true; try { await SyncAccount(); } finally { synchronizing = false; } }, true);
        }
        private void RestorePublication()
        {
            pendingDraft = publications.Read(State.user?.uid);
            State.hasPendingPublication = pendingDraft != null;
            if (pendingDraft != null)
            {
                State.selectedPreset = pendingDraft.presetId;
                State.draftPlace = pendingDraft.place; State.draftTeaser = pendingDraft.teaser; State.draftNote = pendingDraft.note;
                return;
            }
            var editable = editablePublications.Read(State.user?.uid);
            if (editable == null) return;
            State.selectedPreset = editable.presetId ?? "";
            State.draftPlace = editable.place ?? "";
            State.draftTeaser = editable.teaser ?? "";
            State.draftNote = editable.note ?? "";
        }
        private bool SaveEditableDraft(string presetId, string place, string teaser, string note)
        {
            if (State.user == null) return true;
            try
            {
                editablePublications.Save(State.user.uid, new EditablePublicationDraft
                { presetId = presetId ?? "", place = place ?? "", teaser = teaser ?? "", note = note ?? "" });
                return true;
            }
            catch
            {
                State.error = "This device could not save your draft. Try again before publishing.";
                return false;
            }
        }
        private bool ClearPublication()
        {
            try { publications.Remove(State.user?.uid); }
            catch { State.error = "This device could not remove the saved draft."; return false; }
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
        private async void Run(Func<Task> work, bool preserveForegroundError = false)
        {
            if (State.busy || disposed) return;
            CancelNearby(true);
            bool preserveError = preserveForegroundError &&
                (!string.IsNullOrEmpty(State.error) || State.locationSettingsRequired);
            State.busy = true;
            if (!preserveError) { State.error = ""; State.locationSettingsRequired = false; }
            int generation = accountGeneration; Notify();
            try { await work(); }
            catch (Exception error)
            {
                if (!disposed && generation == accountGeneration)
                {
                    if (publicationStage != null)
                    {
                        FinishPublicationStage(true);
                        State.status = pendingDraft == null ? "Publishing stopped. Try again when you're ready." :
                            "Your draft is saved. Try publishing again.";
                    }
                    if (!preserveError)
                    {
                        State.error = error is ApiFailure ? error.Message : "Something went wrong. Please try again.";
                        State.locationSettingsRequired = error is ApiFailure failure && failure.LocationSettingsRequired;
                    }
                }
            }
            finally
            {
                if (publicationStage != null) FinishPublicationStage(true);
                if (!disposed)
                {
                    State.busy = false; State.user = session.Current; Notify();
                    ResumeNearby();
                }
            }
        }
        private static string Path(string id) => "/v1/stickers/" + Uri.EscapeDataString(id);
        private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
