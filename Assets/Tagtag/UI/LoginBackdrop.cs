using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace Tagtag.UI
{
    /// <summary>Owns decorative login playback. A poster remains until the first decoded frame.</summary>
    public sealed class LoginBackdrop : MonoBehaviour
    {
        private VisualElement surface;
        private Texture2D poster;
        private VideoPlayer player;
        private RenderTexture frame;
        private IVisualElementScheduledItem repaint;
        private bool hasFrame;
        private bool failed;
        private bool reducedMotion;
        private bool paused;
        private bool focused = true;

        public void Mount(VisualElement target, bool reduceMotion)
        {
            surface = target;
            reducedMotion = reduceMotion;
            poster = Resources.Load<Texture2D>("Tagtag/Login/Poster");
            ShowPoster();
            if (reducedMotion) return;
            var clip = Resources.Load<VideoClip>("Tagtag/Login/Background");
            if (clip == null) return;
            frame = new RenderTexture((int)clip.width, (int)clip.height, 0, RenderTextureFormat.ARGB32)
                { name = "Tagtag login video", useMipMap = false, autoGenerateMips = false };
            frame.Create();
            player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = true;
            player.skipOnDrop = true;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = frame;
            player.clip = clip;
            player.waitForFirstFrame = true;
            player.sendFrameReadyEvents = true;
            player.prepareCompleted += Prepared;
            player.frameReady += FirstFrame;
            player.errorReceived += PlaybackError;
            player.Prepare();
        }

        private bool CanPlay => isActiveAndEnabled && !paused && focused && !failed && !reducedMotion && surface?.panel != null;
        private void Prepared(VideoPlayer source) => SyncPlayback();
        private void FirstFrame(VideoPlayer source, long index)
        {
            hasFrame = true;
            source.sendFrameReadyEvents = false;
            SyncPlayback();
        }
        private void SyncPlayback()
        {
            repaint?.Pause();
            if (player == null) return;
            if (!CanPlay) { player.Pause(); return; }
            if (!player.isPrepared) return;
            player.Play();
            if (!hasFrame) return;
            surface.style.backgroundImage = Background.FromRenderTexture(frame);
            if (repaint == null) repaint = surface.schedule.Execute(surface.MarkDirtyRepaint).Every(42);
            else repaint.Resume();
        }
        private void ShowPoster()
        {
            if (surface != null && poster != null) surface.style.backgroundImage = Background.FromTexture2D(poster);
        }
        private void PlaybackError(VideoPlayer source, string message)
        {
            failed = true;
            repaint?.Pause();
            source.Stop();
            ShowPoster();
            Debug.LogWarning("Login background unavailable; using poster. " + message);
        }
        private void OnApplicationPause(bool value) { paused = value; SyncPlayback(); }
        private void OnApplicationFocus(bool value) { focused = value; SyncPlayback(); }
        private void OnEnable() => SyncPlayback();
        private void OnDisable() { repaint?.Pause(); player?.Pause(); }
        private void OnDestroy()
        {
            repaint?.Pause();
            if (player != null)
            {
                player.prepareCompleted -= Prepared;
                player.frameReady -= FirstFrame;
                player.errorReceived -= PlaybackError;
                player.Stop();
                player.targetTexture = null;
                Destroy(player);
            }
            if (frame != null) { frame.Release(); Destroy(frame); }
            surface = null;
        }
    }
}
