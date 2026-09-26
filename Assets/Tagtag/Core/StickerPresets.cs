using System;
using System.Collections.Generic;

namespace Tagtag
{
    public static class StickerPresets
    {
        public static IReadOnlyList<string> Ids { get; } = Array.AsReadOnly(new[]
        {
            "taggi-1", "taggi-2", "taggi-3", "taggi-4", "taggi-5", "taggi-6",
            "taggi-7", "taggi-8", "taggi-9", "taggi-10", "taggi-11", "taggi-12",
            "taggi-13"
        });

        private static readonly string[] names =
        {
            "Taggi pose 1", "Taggi pose 2", "Taggi pose 3", "Taggi pose 4",
            "Waving Taggi", "Heart-hugging Taggi", "Laughing Taggi", "Sleepy Taggi",
            "Surprised Taggi", "Cheering Taggi", "Shy Taggi", "Thinking Taggi",
            "Taggi holding ETHGlobal Tokyo sticker"
        };

        public static bool Contains(string id) => IndexOf(id) >= 0;

        public static string DisplayName(string id)
        {
            int index = IndexOf(id);
            return index >= 0 ? names[index] : "Taggi";
        }

        private static int IndexOf(string id)
        {
            for (int index = 0; index < Ids.Count; index++)
                if (string.Equals(Ids[index], id, StringComparison.Ordinal)) return index;
            return -1;
        }
    }
}
