using Tagtag.AR;
using Tagtag.Services;
using Tagtag.UI;
using UnityEngine;

namespace Tagtag
{
    public sealed class TagtagApplication : MonoBehaviour
    {
        private TagtagController controller;
        private void Awake()
        {
            // Always clear to paper, including frames before the UI or AR rig is ready.
            var fallback = new GameObject("Paper camera fallback").AddComponent<Camera>();
            fallback.transform.SetParent(transform, false);
            fallback.clearFlags = CameraClearFlags.SolidColor;
            fallback.backgroundColor = new Color32(255, 254, 250, 255);
            fallback.cullingMask = 0;
            fallback.depth = -100;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            var resource = Resources.Load<TextAsset>("Tagtag/ServiceConfiguration");
            var configuration = resource == null ? new ServiceConfiguration() : JsonUtility.FromJson<ServiceConfiguration>(resource.text);
            var ar = gameObject.AddComponent<ArExperience>();
            var map = gameObject.AddComponent<NativeMapView>();
            var identity = gameObject.AddComponent<NativeIdentity>();
            controller = new TagtagController(configuration, ar, map, identity);
            gameObject.AddComponent<TagtagAppView>().Initialize(controller);
            controller.Start();
        }
        private void OnApplicationPause(bool paused)
        {
            controller?.SetSuspended(paused);
            if (!paused) controller?.Resume();
        }
        private void OnDestroy() { controller?.Dispose(); }
    }
}
