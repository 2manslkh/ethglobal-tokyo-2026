using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly DeviceLocation location = new DeviceLocation();
        private RecoveryData recovery;
        private PlacementDraft pendingDraft;
        private int accountGeneration;
        private bool disposed;

        public TagtagController(ServiceConfiguration configuration, IArExperience ar, IMapExperience map, INativeIdentity identity)
        {
            this.configuration = configuration;
            this.identity = identity;
            Ar = ar; Map = map;
            api = new TagtagApi(configuration);
            session = new FirebaseSession(configuration, identity);
            cache = new LocalCollection(System.IO.Path.Combine(Application.persistentDataPath, "collections"));
            State.servicesConfigured = configuration.Configured;
            State.user = session.Current;
            State.collection = cache.Read(State.user?.uid);
            State.status = State.user == null ? "Your next little discovery is out there." : "Your sticker book is ready.";
            ar.Changed += Notify;
            ar.StickerTapped += Collect;
            map.StickerSelected += SelectSticker;
        }

        public void Start() { if (State.user != null && configuration.Configured) Run(SyncAccount); }
        public void Dispose()
        {
            disposed = true; accountGeneration++;
            Ar.Changed -= Notify; Ar.StickerTapped -= Collect; Map.StickerSelected -= SelectSticker;
            Ar.Exit(); Map.Hide();
        }
        private void Notify() { if (!disposed) Changed?.Invoke(); }
        public void Navigate(AppPage page)
        {
            if (State.busy) return;
            if (State.page == AppPage.Stick && page != AppPage.Stick) Ar.Exit();
            Map.Hide(); State.page = page; State.error = ""; State.detail = null; State.accountOpen = false;
            if (page == AppPage.Stick) Ar.Enter();
            Notify();
            if (page == AppPage.Explore && State.nearby.Count == 0) RefreshNearby();
        }
        public void SetAccountOpen(bool open) { State.accountOpen = open; Map.Hide(); Notify(); }
        public void SignIn(string provider)
        {
            if (State.busy) return;
            State.busy = true; State.error = ""; Notify();
            identity.SignIn(provider, configuration, credential =>
            {
                State.busy = false;
                Run(async () =>
                {
                    State.user = await session.SignIn(credential);
                    accountGeneration++;
                    State.collection = cache.Read(State.user.uid);
                    State.status = "You're signed in. Welcome to tagtag.";
                    await SyncAccount();
                });
            }, message => { State.busy = false; State.error = message; Notify(); });
        }
        public void SignOut()
        {
            if (State.busy) return;
            accountGeneration++; session.SignOut(); State.user = null; recovery = null;
            State.collection.Clear(); State.authored.Clear(); State.detail = null; State.selected = null;
            Ar.CancelPlacement(); pendingDraft = null;
            State.draftNote = State.draftTeaser = State.draftPlace = State.selectedPreset = "";
            State.status = "Signed out."; State.error = ""; Notify();
        }
        public void RefreshNearby() => Run(async () =>
        {
            State.location = await location.Current();
            var result = await api.Call<SummaryList>("POST", "/v1/nearby", new LocationRequest { location = State.location }, await session.Token(false));
            State.nearby = (result.items ?? Array.Empty<StickerSummary>()).ToList();
            State.status = State.nearby.Count == 0 ? "No stickers nearby yet. Leave the first one." : "Little discoveries around you.";
        });
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
            recovery = null; pendingDraft = null; State.selected = null; State.selectedPreset = presetId;
            Ar.SelectPreset(presetId); Notify();
        }
        public void SetDraft(string place, string teaser, string note)
        {
            // Preserve the operation ID only while the payload stays the same.
            if (State.draftPlace != place || State.draftTeaser != teaser || State.draftNote != note) pendingDraft = null;
            State.draftPlace = place ?? ""; State.draftTeaser = teaser ?? ""; State.draftNote = note ?? "";
        }
        public void Publish()
        {
            if (!RequireAccount() || State.busy) return;
            if (!Ar.CanPublish) { State.error = "Move slowly until the surface is mapped and Taggi is placed."; Notify(); return; }
            if (string.IsNullOrWhiteSpace(State.draftTeaser) || string.IsNullOrWhiteSpace(State.draftNote))
            { State.error = "Add a teaser and a note before publishing."; Notify(); return; }
            Run(async () =>
            {
                if (pendingDraft == null)
                {
                    var completion = new TaskCompletionSource<SpatialSnapshot>();
                    Ar.Capture(value => completion.TrySetResult(value), error => completion.TrySetException(new ApiFailure(error)));
                    if (await Task.WhenAny(completion.Task, Task.Delay(20000)) != completion.Task) throw new ApiFailure("Mapping took too long. Try again after looking around.");
                    pendingDraft = new PlacementDraft { operationId = Guid.NewGuid().ToString("N"), presetId = State.selectedPreset,
                        place = State.draftPlace.Trim(), teaser = State.draftTeaser.Trim(), note = State.draftNote.Trim(), snapshot = await completion.Task };
                }
                pendingDraft.location = State.location = await location.Current();
                byte[] bytes = Convert.FromBase64String(pendingDraft.snapshot.worldMapBase64);
                var draft = pendingDraft;
                var result = await api.Call<PrepareResult>("POST", "/v1/publications/prepare", new PrepareRequest {
                    operationId = draft.operationId, presetId = draft.presetId, place = draft.place, teaser = draft.teaser, note = draft.note,
                    location = draft.location, position = draft.snapshot.position, rotation = draft.snapshot.rotation,
                    widthMeters = draft.snapshot.widthMeters, mapBytes = bytes.Length }, await session.Token());
                if (!string.IsNullOrEmpty(result.uploadUrl)) await TagtagApi.Upload(result.uploadUrl, bytes);
                State.location = await location.Current();
                var published = await api.Call<StickerResult>("POST", "/v1/publications/" + Uri.EscapeDataString(result.id) + "/finalize",
                    new FinalizeRequest { operationId = draft.operationId, location = State.location }, await session.Token());
                State.authored.RemoveAll(item => item.id == published.sticker.id); State.authored.Add(published.sticker);
                State.nearby.RemoveAll(item => item.id == published.sticker.id); State.nearby.Add(published.sticker);
                pendingDraft = null; State.draftNote = State.draftTeaser = State.draftPlace = State.selectedPreset = "";
                Ar.CancelPlacement(); State.status = "Taggi is out there. Your sticker is published!";
            });
        }
        public void CancelPlacement()
        {
            if (State.busy) return;
            recovery = null; pendingDraft = null; State.selectedPreset = ""; State.selected = null;
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
            if (!RequireAccount()) return;
            Run(async () =>
            {
                await api.Call<OkResult>("POST", "/v1/blocks", new BlockRequest { authorId = authorId }, await session.Token());
                State.nearby.RemoveAll(item => item.authorId == authorId);
                if (State.selected?.authorId == authorId) State.selected = null;
                if (State.detail?.authorId == authorId) State.detail = null;
                recovery = null; Ar.CancelPlacement(); await SyncAccount(); State.status = "This person's stickers are now hidden.";
            });
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
                cache.Remove(uid); session.SignOut(); accountGeneration++;
                State.user = null; State.collection.Clear(); State.authored.Clear(); State.nearby.Clear(); State.detail = null; State.selected = null;
                pendingDraft = null; recovery = null; Ar.CancelPlacement(); State.draftNote = State.draftTeaser = State.draftPlace = State.selectedPreset = "";
                State.status = "Your account has been deleted.";
            });
        }
        private async Task SyncAccount()
        {
            var collection = await api.Call<CollectionList>("GET", "/v1/collection", null, await session.Token());
            State.collection = CollectionBook.Normalize(collection.items); SaveCollection();
            if (State.detail != null) State.detail = State.collection.FirstOrDefault(item => item.id == State.detail.id);
            var authored = await api.Call<SummaryList>("GET", "/v1/authored", null, await session.Token());
            State.authored = (authored.items ?? Array.Empty<StickerSummary>()).ToList();
        }
        public void Resume() { if (!State.busy && State.user != null && configuration.Configured) Run(SyncAccount); }
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
        private async void Run(Func<Task> work)
        {
            if (State.busy || disposed) return;
            State.busy = true; State.error = ""; int generation = accountGeneration; Notify();
            try { await work(); }
            catch (Exception error)
            {
                if (!disposed && generation == accountGeneration)
                    State.error = error is ApiFailure ? error.Message : "Something went wrong. Please try again.";
            }
            finally { State.busy = false; State.user = session.Current; Notify(); }
        }
        private static string Path(string id) => "/v1/stickers/" + Uri.EscapeDataString(id);
        private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
