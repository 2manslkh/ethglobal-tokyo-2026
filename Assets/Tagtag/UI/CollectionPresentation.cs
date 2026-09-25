using System.Collections.Generic;

namespace Tagtag.UI
{
    public static class CollectionPresentation
    {
        public static string DetailKey(CollectedSticker sticker, bool busy)
        {
            if (sticker == null) return "missing:" + busy;
            return sticker.id + ":" + sticker.revision + ":" + sticker.unavailable + ":" +
                sticker.collectedAt + ":" + sticker.place + ":" + sticker.teaser + ":" +
                sticker.authorName + ":" + sticker.note;
        }

        public static List<CollectedSticker> OrderedDistinct(IReadOnlyList<CollectedSticker> source)
        {
            List<CollectedSticker> items = new List<CollectedSticker>();
            if (source == null) return items;
            Dictionary<string, CollectedSticker> newest = new Dictionary<string, CollectedSticker>();
            for (int index = 0; index < source.Count; index++)
            {
                CollectedSticker sticker = source[index];
                if (sticker == null || string.IsNullOrEmpty(sticker.id)) continue;
                if (!newest.TryGetValue(sticker.id, out CollectedSticker current) || sticker.revision > current.revision)
                {
                    newest[sticker.id] = sticker;
                }
            }

            items.AddRange(newest.Values);
            items.Sort((left, right) =>
            {
                int time = left.collectedAt.CompareTo(right.collectedAt);
                return time != 0 ? time : string.CompareOrdinal(left.id, right.id);
            });
            return items;
        }
    }
}
