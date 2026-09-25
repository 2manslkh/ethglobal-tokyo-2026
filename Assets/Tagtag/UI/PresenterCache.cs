namespace Tagtag.UI
{
    // Each mounted destination owns its own cache. Reset on a new visual host,
    // even when the data has the same revision as an earlier visit.
    public sealed class PresenterCache
    {
        private string key;

        public bool NeedsRefresh(string value)
        {
            if (key == value) return false;
            key = value;
            return true;
        }

        public void Reset() { key = null; }
    }
}
