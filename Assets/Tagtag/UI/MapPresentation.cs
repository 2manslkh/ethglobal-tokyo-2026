namespace Tagtag.UI
{
    public static class MapPresentation
    {
        public static bool ShouldShow(AppState state, bool sheetOpen)
        {
            return state != null && state.page == AppPage.Explore && state.location != null && !state.accountOpen && !sheetOpen;
        }

        public static bool SyncVisibility(AppState state, bool sheetOpen, IMapExperience map)
        {
            bool visible = ShouldShow(state, sheetOpen);
            if (!visible) map?.Hide();
            return visible;
        }
    }
}
