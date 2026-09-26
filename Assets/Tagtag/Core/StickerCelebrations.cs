using System.Collections.Generic;

namespace Tagtag
{
    public enum StickerCelebrationKind { Placed, Found }

    // Transient presentation state, deliberately excluded from collection/draft persistence.
    public sealed class StickerCelebration
    {
        public string Token { get; }
        public StickerCelebrationKind Kind { get; }
        public StickerSummary Sticker { get; }
        public bool Presented { get; private set; }

        internal StickerCelebration(string token, StickerCelebrationKind kind, StickerSummary sticker)
        { Token = token; Kind = kind; Sticker = sticker; }

        public bool TryPresent()
        {
            if (Presented) return false;
            Presented = true;
            return true;
        }
    }

    public sealed class StickerCelebrations
    {
        private string account;
        private readonly HashSet<string> completed = new HashSet<string>();
        public StickerCelebration Pending { get; private set; }

        public void SetAccount(string uid)
        {
            if (account == uid) return;
            account = uid;
            Pending = null;
            completed.Clear();
        }

        public void Published(string uid, string operationId, StickerSummary sticker)
        { Complete(uid, "placed:" + operationId, operationId, StickerCelebrationKind.Placed, sticker); }

        public void Collected(string uid, string discoveryId, StickerSummary sticker, bool alreadyCollected)
        {
            SetAccount(uid);
            if (!alreadyCollected)
                Complete(uid, "found:" + discoveryId, discoveryId, StickerCelebrationKind.Found, sticker);
        }

        private void Complete(string uid, string token, string operationId,
            StickerCelebrationKind kind, StickerSummary sticker)
        {
            SetAccount(uid);
            if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(operationId) ||
                string.IsNullOrEmpty(sticker?.id) || !completed.Add(token)) return;
            Pending = new StickerCelebration(token, kind, sticker);
        }

        // /v1/collection currently returns at most 1,000 entries. A capped or failed
        // read cannot prove absence; collection still succeeds without a reward.
        public static bool IsNewInServerSnapshot(string stickerId, IReadOnlyList<CollectedSticker> items)
        {
            if (string.IsNullOrEmpty(stickerId) || items == null || items.Count >= 1000) return false;
            foreach (var item in items)
                if (item == null || item.id == stickerId) return false;
            return true;
        }

        public void Acknowledge(string token)
        {
            if (Pending?.Token == token) Pending = null;
        }
    }
}
