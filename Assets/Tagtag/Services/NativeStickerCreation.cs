using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Tagtag.Services
{
    [Serializable] public sealed class CreatedStickerImage
    {
        public string status, path, name, kind, error;
        public int width, height;
    }
    public interface IStickerCreation
    {
        int Capabilities { get; }
        void Open(string source, Action<CreatedStickerImage> completed);
        void Cancel();
    }
    public sealed class NativeStickerCreation : MonoBehaviour, IStickerCreation
    {
        private Action<CreatedStickerImage> callback;
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int TagtagStickerCreationAvailable();
        [DllImport("__Internal")] private static extern void TagtagStickerCreationOpen(string source, string receiver, string callback);
        [DllImport("__Internal")] private static extern void TagtagStickerCreationCancel();
#endif
        public int Capabilities
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return TagtagStickerCreationAvailable();
#else
                return 0;
#endif
            }
        }
        public void Open(string source, Action<CreatedStickerImage> completed)
        {
            if (callback != null) { completed(new CreatedStickerImage { status = "error", error = "Finish the current sticker first." }); return; }
#if UNITY_IOS && !UNITY_EDITOR
            callback = completed;
            TagtagStickerCreationOpen(source, gameObject.name, nameof(OnStickerCreated));
#else
            completed(new CreatedStickerImage { status = "unavailable", error = "Sticker creation is available on iPhone." });
#endif
        }
        public void Cancel()
        {
#if UNITY_IOS && !UNITY_EDITOR
            TagtagStickerCreationCancel();
#endif
            var pending = callback; callback = null;
            pending?.Invoke(new CreatedStickerImage { status = "cancelled" });
        }
        [UnityEngine.Scripting.Preserve]
        public void OnStickerCreated(string json)
        {
            var pending = callback; callback = null;
            if (pending == null) return;
            CreatedStickerImage image;
            try { image = JsonUtility.FromJson<CreatedStickerImage>(json); }
            catch { image = null; }
            pending(image ?? new CreatedStickerImage { status = "error", error = "The image could not be opened." });
        }
    }
}
