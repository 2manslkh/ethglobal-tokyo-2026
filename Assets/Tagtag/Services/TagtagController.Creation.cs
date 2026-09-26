using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Tagtag.Services
{
    public sealed partial class TagtagController
    {
        private IStickerCreation creation;
        private LocalDesigns designs;
        private GameObject creationObject;
        private bool nativeCreationOpen;
        private void InitializeCreation(IStickerCreation source)
        {
            designs = new LocalDesigns(System.IO.Path.Combine(Application.persistentDataPath, "designs"));
            if (source != null) creation = source;
            else
            {
                creationObject = new GameObject("Tagtag Sticker Creation " + Guid.NewGuid().ToString("N"));
                if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(creationObject);
                creation = creationObject.AddComponent<NativeStickerCreation>();
            }
            State.creationCapabilities = creation.Capabilities;
            RestoreDesigns();
        }
        private void RestoreDesigns()
        {
            StickerArtwork.SetAccount(State.user?.uid);
            State.designError = "";
            State.designs = designs.Read(State.user?.uid).ToList();
            State.hasPendingDesign = designs.ReadDraft(State.user?.uid) != null;
        }
        public void OpenCreation()
        {
            if (!RequireAccount()) return;
            if (State.busy || nativeCreationOpen) return;
            State.creationOpen = true; State.accountOpen = false; State.detail = null;
            State.designError = "";
            State.creationCapabilities = creation.Capabilities;
            Map.Hide(); CancelNearby(true); Notify();
            if (State.user != null) RefreshDesigns();
        }
        public void CloseCreation()
        {
            if (State.busy || nativeCreationOpen) return;
            State.creationOpen = false; Notify(); ResumeNearby();
        }
        public void CreateSticker(string source)
        {
            if (!RequireAccount()) return;
            if (State.busy || nativeCreationOpen || (source != "import" && source != "ai" && source != "polaroid")) return;
            if (State.hasPendingDesign)
            { State.designError = "Save your pending sticker before creating another."; Notify(); return; }
            State.creationOpen = true; State.designError = "";
            nativeCreationOpen = true;
            (Ar as ICustomArtworkAr)?.SuspendForCreation(true);
            location.Stop();
            string owner = State.user?.uid;
            int version = accountGeneration;
            Notify();
            creation.Open(source, image =>
            {
                nativeCreationOpen = false;
                (Ar as ICustomArtworkAr)?.SuspendForCreation(false);
                if (disposed || version != accountGeneration) return;
                if (image.status == "success")
                {
                    try
                    {
                        designs.SaveDraft(owner, image); State.hasPendingDesign = true;
                        if (System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(image.path)) == "StickerCreations")
                            try { File.Delete(image.path); } catch { }
                    }
                    catch { State.designError = "This device could not save the sticker. Please try again."; Notify(); return; }
                    RetryDesignSave();
                }
                else if (image.status != "cancelled") State.designError = image.error ?? "Sticker creation is unavailable.";
                Notify();
            });
        }
        public void RetryDesignSave()
        {
            if (State.busy || !State.hasPendingDesign) return;
            if (!RequireAccount()) { State.status = "Your sticker is saved on this phone. Sign in to sync it."; Notify(); return; }
            Run(SaveCreatedDesign, designOperation: true);
        }
        private async Task SaveCreatedDesign()
        {
            string owner = State.user.uid;
            int version = accountGeneration;
            designs.ClaimGuest(owner);
            var draft = designs.ReadDraft(owner);
            if (draft == null) { State.hasPendingDesign = false; return; }
            State.hasPendingDesign = true;
            byte[] bytes = File.ReadAllBytes(draft.path);
            State.status = "Saving your sticker…"; Notify();
            PrepareResult prepared;
            try { prepared = await PrepareDesign(draft, bytes.Length); }
            catch (ApiFailure error) when (error.Code == "design_expired")
            {
                EnsureDesignAccount(owner, version);
                draft = designs.RestartDraft(owner);
                prepared = await PrepareDesign(draft, bytes.Length);
            }
            EnsureDesignAccount(owner, version);
            if (!string.IsNullOrEmpty(prepared.uploadUrl)) await TagtagApi.Upload(prepared.uploadUrl, bytes, prepared.uploadHeaders);
            EnsureDesignAccount(owner, version);
            var result = await api.Call<DesignResult>("POST", "/v1/designs/" + Uri.EscapeDataString(prepared.id) + "/finalize", new object(), await session.Token());
            EnsureDesignAccount(owner, version);
            if (result?.design == null || result.design.ownerId != owner) throw new ApiFailure("The sticker could not be saved. Please retry.");
            State.designs.RemoveAll(item => item.id == result.design.id); State.designs.Insert(0, result.design);
            designs.Save(owner, State.designs.ToArray());
            StickerArtwork.Store(result.design.id, bytes);
            designs.ClearDraft(owner); State.hasPendingDesign = designs.ReadDraft(owner) != null;
            State.accountOpen = false; State.creationOpen = true;
            State.status = State.hasPendingDesign ? "Saved to My Stickers. One more saved draft is ready to upload." :
                "Saved to My Stickers. Choose it whenever you're ready to place.";
        }
        private async Task<PrepareResult> PrepareDesign(DesignUploadDraft draft, int length) =>
            await api.Call<PrepareResult>("POST", "/v1/designs/prepare", new DesignPrepareRequest
            { operationId = draft.operationId, name = draft.name, kind = draft.kind, width = draft.width, height = draft.height, imageBytes = length }, await session.Token());
        private void EnsureDesignAccount(string owner, int version)
        {
            if (disposed || version != accountGeneration || session.Current?.uid != owner) throw new OperationCanceledException();
        }
        public void RefreshArtwork()
        {
            if (State.busy) return;
            if (State.user == null) { StickerArtwork.Retry(); RefreshNearby(); return; }
            Run(async () => { await SyncAccount(); StickerArtwork.Retry(); });
        }
        public void RefreshDesigns()
        {
            if (State.busy || State.designsLoading || State.user == null) return;
            StickerArtwork.Retry();
            Run(SyncDesigns, designOperation: true);
        }
        private async Task SyncDesigns()
        {
            if (State.user == null) return;
            string owner = State.user.uid;
            int version = accountGeneration;
            State.designError = "";
            State.designsLoading = true; Notify();
            try
            {
                var result = await api.Call<DesignList>("GET", "/v1/designs", null, await session.Token());
                EnsureDesignAccount(owner, version);
                State.designs = (result?.items ?? Array.Empty<StickerDesign>()).Where(item => item != null && item.ownerId == owner).ToList();
                foreach (var design in State.designs) StickerArtwork.Authorize(design.id);
                designs.Save(owner, State.designs.ToArray());
            }
            catch (Exception error)
            {
                if (!disposed && version == accountGeneration && State.user?.uid == owner)
                    State.designError = error is ApiFailure ? error.Message : "My Stickers could not refresh. Please try again.";
            }
            finally { State.designsLoading = false; }
        }
        public void SelectDesign(string id)
        {
            if (!RequireAccount()) return;
            if (State.busy || nativeCreationOpen) return;
            var design = State.designs.FirstOrDefault(item => item.id == id && item.ownerId == State.user?.uid);
            if (design == null || !(Ar is ICustomArtworkAr custom)) return;
            string owner = State.user.uid;
            int version = accountGeneration;
            Run(async () =>
            {
                State.status = "Opening sticker artwork…"; Notify();
                var texture = await StickerArtwork.Load(design.id, design.artworkUrl);
                EnsureDesignAccount(owner, version);
                if (texture == null) throw new ApiFailure("Artwork could not load. Refresh My Stickers and try again.");
                string previous = State.selectedDesign;
                InvalidateCapture();
                State.selectedDesign = design.id;
                if (!SaveEditableDraft("", State.draftPlace, State.draftTeaser, State.draftNote, true))
                { State.selectedDesign = previous; throw new ApiFailure(State.designError); }
                if (pendingDraft != null && !ClearPublication(true))
                { State.selectedDesign = previous; throw new ApiFailure(State.designError); }
                State.selectedPreset = ""; recovery = null; State.selected = null;
                State.creationOpen = false; State.accountOpen = false; State.page = AppPage.Stick;
                Map.Hide(); Ar.Enter(); custom.SelectArtwork(design, texture); location.Prewarm();
                State.status = "Find a surface for your sticker.";
            }, designOperation: true);
        }
        public void DeleteDesign(string id)
        {
            if (State.busy || State.user == null || !State.designs.Any(item => item.id == id && item.ownerId == State.user.uid)) return;
            string owner = State.user.uid;
            int version = accountGeneration;
            Run(async () =>
            {
                await api.Call<OkResult>("DELETE", "/v1/designs/" + Uri.EscapeDataString(id), null, await session.Token());
                EnsureDesignAccount(owner, version);
                if (State.selectedDesign == id)
                {
                    Ar.CancelPlacement();
                    if (!ClearPublication(true)) throw new ApiFailure(State.designError);
                    try { editablePublications.Remove(owner); }
                    catch { throw new ApiFailure("This device could not remove the saved draft. Please retry."); }
                    State.selectedDesign = "";
                }
                var remaining = State.designs.Where(item => item.id != id).ToList();
                designs.Save(owner, remaining.ToArray());
                State.designs = remaining;
                State.status = "Removed from My Stickers. Published copies remain available.";
            }, designOperation: true);
        }
        private async void RestoreSelectedArtwork()
        {
            string id = State.selectedDesign;
            int version = accountGeneration;
            var design = State.designs.FirstOrDefault(item => item.id == id);
            if (design == null || !(Ar is ICustomArtworkAr custom)) return;
            var texture = await StickerArtwork.Load(id, design.artworkUrl);
            if (disposed || accountGeneration != version || State.selectedDesign != id || State.page != AppPage.Stick) return;
            if (texture != null) custom.SelectArtwork(design, texture);
            else { State.designError = "Artwork could not load. Open My Stickers and refresh."; Notify(); }
        }
    }
}
