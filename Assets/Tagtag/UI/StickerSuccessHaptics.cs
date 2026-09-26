using System.Runtime.InteropServices;

namespace Tagtag.UI
{
    internal static class StickerSuccessHaptics
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void TagtagStickerSuccessHaptic();
#endif
        public static void Play()
        {
#if UNITY_IOS && !UNITY_EDITOR
            TagtagStickerSuccessHaptic();
#endif
        }
    }
}
