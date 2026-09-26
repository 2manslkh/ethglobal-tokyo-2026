using Tagtag.AR;
using Tagtag.Blockchain;
using Tagtag.Services;
using Tagtag.UI;
using UnityEngine;

namespace Tagtag
{
    public sealed class TagtagApplication : MonoBehaviour
    {
        private TagtagController controller;
        private float nextNftRefresh;
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
            if (configuration.nftEnabled && !string.IsNullOrWhiteSpace(configuration.thirdwebClientId))
            {
                var wallet = new ThirdwebEmbeddedWallet(configuration.thirdwebClientId);
                controller = new TagtagController(configuration, ar, map, identity,
                    locationConfirmation: gameObject.AddComponent<NativeLocationConfirmation>(),
                    connectWallet: wallet.Connect, signWalletMessage: wallet.SignMessage, disconnectWallet: wallet.Disconnect,
                    nftOwner: wallet.GetNftOwner,
                    transferNft: async (contract, token, recipient) =>
                    {
                        try { return await wallet.TransferNft(contract, token, recipient); }
                        catch (WalletTransferException error) { throw new NftTransferFailure(error.Code); }
                    }, transferStatus: wallet.GetTransferStatus);
            }
            else controller = new TagtagController(configuration, ar, map, identity,
                locationConfirmation: gameObject.AddComponent<NativeLocationConfirmation>());
            gameObject.AddComponent<TagtagAppView>().Initialize(controller);
            controller.Start();
        }
        private void OnApplicationPause(bool paused)
        {
            controller?.SetSuspended(paused);
            if (!paused) controller?.Resume();
        }
        private void Update()
        {
            if (Time.unscaledTime < nextNftRefresh) return;
            nextNftRefresh = Time.unscaledTime + 15f;
            controller?.RefreshNfts();
        }
        private void OnDestroy() { controller?.Dispose(); }
    }
}
