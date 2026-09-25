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
        private void OnApplicationPause(bool paused) { if (!paused) controller?.Resume(); }
        private void OnDestroy() { controller?.Dispose(); }
    }
}
