using System;
using System.Linq;
using System.Threading.Tasks;

namespace Tagtag.Services
{
    public sealed partial class TagtagController : IPlacedLocationsController
    {
        private readonly Func<string, Task<PlacementPage>> loadPlacements;
        private string placementsUserId;
        private int placementsRequest;

        private void SyncPlacementIdentity()
        {
            string uid = State.user?.uid;
            if (placementsUserId == uid) return;
            placementsUserId = uid;
            placementsRequest++;
            State.placements.Clear();
            State.placementsLoading = State.placementsLoaded = false;
            State.placementsError = State.placementsNextCursor = "";
            if (State.mapSelection != null)
            {
                if (State.selected?.id == State.mapSelection.id) State.selected = null;
                State.mapSelection = null;
            }
        }

        public void RefreshPlacements()
        {
            if (disposed) return;
            SyncPlacementIdentity();
            if (State.user == null) { Notify(); return; }
            FetchPlacements("");
        }

        public void LoadMorePlacements()
        {
            if (disposed) return;
            SyncPlacementIdentity();
            if (State.user == null || State.placementsLoading) return;
            if (!State.placementsLoaded) { RefreshPlacements(); return; }
            if (!string.IsNullOrEmpty(State.placementsNextCursor)) FetchPlacements(State.placementsNextCursor);
        }

        private async void FetchPlacements(string cursor)
        {
            int request = ++placementsRequest;
            int generation = accountGeneration;
            string uid = State.user.uid;
            State.placementsLoading = true;
            State.placementsError = "";
            Notify();
            try
            {
                PlacementPage page;
                if (loadPlacements != null) page = await loadPlacements(cursor);
                else
                {
                    string token = await session.Token();
                    if (!CurrentPlacements(request, generation, uid)) return;
                    page = await api.Call<PlacementPage>("GET", "/v1/authored?status=published&limit=50" +
                        (string.IsNullOrEmpty(cursor) ? "" : "&cursor=" + Uri.EscapeDataString(cursor)), null, token);
                }
                if (!CurrentPlacements(request, generation, uid)) return;
                if (page == null) throw new ApiFailure("Your placed stickers could not load. Please retry.");
                if (!string.IsNullOrEmpty(cursor) && page.nextCursor == cursor)
                    throw new ApiFailure("More placed stickers could not load. Please retry.");
                var incoming = (page.items ?? Array.Empty<StickerSummary>())
                    .Where(item => item != null && item.status == "published" && item.authorId == uid && !string.IsNullOrEmpty(item.id));
                State.placements = (string.IsNullOrEmpty(cursor) ? incoming : State.placements.Concat(incoming))
                    .GroupBy(item => item.id).Select(group => group.Last())
                    .OrderByDescending(item => item.createdAt).ThenByDescending(item => item.id, StringComparer.Ordinal).ToList();
                foreach (var item in State.placements)
                    if (!string.IsNullOrEmpty(item.designId)) StickerArtwork.Authorize(item.designId);
                State.placementsNextCursor = page.nextCursor ?? "";
                State.placementsLoaded = true;
            }
            catch (Exception error)
            {
                if (CurrentPlacements(request, generation, uid))
                    State.placementsError = error is ApiFailure ? error.Message : "Your placed stickers could not load. Please retry.";
            }
            finally
            {
                if (CurrentPlacements(request, generation, uid)) State.placementsLoading = false;
                if (!disposed && generation == accountGeneration && session.Current?.uid != State.user?.uid)
                { State.user = session.Current; RestoreDesigns(); }
                if (!disposed) Notify();
            }
        }

        private bool CurrentPlacements(int request, int generation, string uid) =>
            !disposed && request == placementsRequest && generation == accountGeneration && State.user?.uid == uid;

        private void PlacementWithdrawn(string id)
        {
            placementsRequest++;
            State.placementsLoading = false;
            State.placements.RemoveAll(item => item.id == id);
            if (State.selected?.id == id) State.selected = null;
            if (State.mapSelection?.id == id) State.mapSelection = null;
            RefreshPlacements();
        }

        public void OpenPlacedLocation(StickerSummary sticker)
        {
            if (disposed || State.busy || nativeCreationOpen || sticker == null ||
                sticker.status != "published" || State.user == null || sticker.authorId != State.user.uid) return;
            Navigate(AppPage.Explore);
            if (State.page != AppPage.Explore) return;
            State.mapSelection = sticker;
            State.selected = sticker;
            Notify();
        }
    }
}
