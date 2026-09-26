using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine.Rendering;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.UIElements;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#if UNITY_IOS
using UnityEngine.XR.ARKit;
#endif

namespace Tagtag.AR
{
    public sealed class ArExperience : MonoBehaviour, IArExperience, ICustomArtworkAr
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int TagtagCameraAuthorizationStatus();
        [DllImport("__Internal")] private static extern void TagtagCameraRequestAccess();
#endif

        public event Action Changed;
        public event Action<string> StickerTapped;
        public string Status { get; private set; } = "Open STICK to scan a surface.";
        public CameraPresentationState CameraPresentation => presentation.State;
        public PlacementScanState ScanState => PlacementFlow.ScanState(IsTracking, HasPlacementSurface,
            anchor != null && visual != null, HasTrackedPlacement, MapReady);
        public bool HasPlacementSurface { get; private set; }
        public bool HasPlacementPreview => !recovered && anchor != null && visual != null;
        public bool PlacementBusy => busy;
        public float PlacementWidthMeters => widthMeters;
        public float PlacementRotationDegrees => twistDegrees;
        public bool IsTracking => active && !paused && ARSession.state == ARSessionState.SessionTracking &&
            cameraFrameAt > 0 && Time.realtimeSinceStartupAsDouble - cameraFrameAt < 1.5;
        public bool CanPublish => ArGates.CanPublish(IsTracking, HasTrackedPlacement,
            MapReady, visual != null && anchor != null, busy);
        public bool HasTrackedPlacement => anchor != null && anchor.trackingState == TrackingState.Tracking;
        public bool CanCollect => ArGates.CanCollect(IsTracking, recovered, anchor != null && anchor.trackingState == TrackingState.Tracking,
            camera == null || visual == null ? float.PositiveInfinity : Vector3.Distance(camera.transform.position, visual.transform.position), true);

        private GameObject rig;
        private XROrigin origin;
        private ARSession session;
        private ARCameraManager cameraManager;
        private ARCameraBackground cameraBackground;
        private ARRaycastManager raycasts;
        private ARPlaneManager planes;
        private ARAnchorManager anchors;
        private Camera camera;
        private GameObject visual;
        private Material material;
        private ARAnchor anchor;
        private readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
        private readonly PlacementFlow placementFlow = new PlacementFlow();
        private readonly Dictionary<TrackableId, SurfaceHatch> surfaceHatches = new Dictionary<TrackableId, SurfaceHatch>();
        private readonly List<TrackableId> removedHatches = new List<TrackableId>();
        private readonly PlaneHatchMesh hatchMeshBuilder = new PlaneHatchMesh();
        private readonly Plane[] frustumPlanes = new Plane[6];
        private Material hatchMaterial;
        private bool hatchMaterialChecked;
        private PlacementScanState observedScanState;
        private float nextSurfaceUpdate;
        private InputAction positionInput;
        private InputAction rotationInput;
        private string presetId;
        private Texture2D customArtwork;
        private bool creationSuspended;
        private bool resumeCreationPlacement;
        private string recoveredStickerId;
        private bool active;
        private bool paused;
        private bool busy;
        private bool recovered;
        private bool wasTracking;
        private bool wasMapped;
        private bool wasAnchorTracking;
        private bool appliedMap;
        private float widthMeters = 0.2f;
        private float twistDegrees;
        private double cameraFrameAt;
        private int generation;
        private Coroutine recoveryRoutine;
        private Coroutine captureRoutine;
        private int cameraFrameNumber;
        private readonly CameraPresentation presentation = new CameraPresentation();
        private readonly CameraSuspension suspension = new CameraSuspension();
        private Coroutine permissionRoutine;
        private int cameraGeneration;
#if UNITY_IOS
        private ARWorldMapRequest? mapRequest;
        private ARKitSessionSubsystem ArKit => session != null ? session.subsystem as ARKitSessionSubsystem : null;
        private bool MapReady => ArKit != null && ARKitSessionSubsystem.worldMapSupported &&
            ArGates.CanSerializeWorldMap(ArKit.worldMappingStatus);
#else
        private bool MapReady => false;
#endif

        public void Enter()
        {
            if (rig == null) CreateRig();
            active = true;
            if (paused)
            {
                CancelOperations();
                ResetCameraContent();
                presentation.Begin(Time.realtimeSinceStartupAsDouble);
                presentation.Interrupt();
                SetStatus("Camera interrupted. Return to tagtag to scan again.");
            }
            else
            {
                StartCamera();
                SetStatus(string.IsNullOrEmpty(presetId) ? "Look around to discover a sticker." :
                    "Move slowly until a surface appears, then tap to place.");
            }
        }

        public void Exit()
        {
            active = false;
            StopCameraPermissionRequest();
            CancelOperations();
            ResetCameraContent();
            if (rig != null) rig.SetActive(false);
            cameraFrameAt = 0d;
            presentation.Exit();
            UpdatePlacementGuidance();
            SetStatus("Open STICK to scan a surface.");
        }

        public void SelectPreset(string id)
        {
            CancelOperations();
            ClearPlacement();
            recoveredStickerId = null;
            recovered = false;
            customArtwork = null;
            presetId = IsPreset(id) ? id : null;
            widthMeters = 0.2f;
            twistDegrees = 0f;
            nextSurfaceUpdate = 0f;
            if (presetId == null) SetStatus("Choose a sticker to place.");
            else SetStatus("Move slowly until a surface appears, then tap to place.");
            UpdatePlacementGuidance();
        }

        public void SelectArtwork(StickerDesign design, Texture2D texture)
        {
            SelectPreset(null);
            if (design == null || string.IsNullOrEmpty(design.id) || texture == null) return;
            customArtwork = texture;
            presetId = "design:" + design.id;
            SetStatus("Move slowly until a surface appears, then tap to place.");
            UpdatePlacementGuidance();
        }
        public void SuspendForCreation(bool value)
        {
            creationSuspended = value;
            if (value) resumeCreationPlacement = active;
            UpdateCameraSuspension();
        }

        public void CancelPlacement()
        {
            CancelOperations();
            ClearPlacement();
            presetId = null; customArtwork = null;
            recoveredStickerId = null;
            recovered = false;
            UpdatePlacementGuidance();
            SetStatus("Placement cancelled.");
        }

        public void SetCameraInteraction(Rect cameraScreenRect, bool blocked)
        {
            if (!placementFlow.SetInteraction(cameraScreenRect, blocked)) return;
            nextSurfaceUpdate = 0f;
            UpdatePlacementGuidance();
        }

        public void Place(Vector2 screenPoint)
        {
            if (!active || paused || busy || recovered || presetId == null || anchor != null ||
                !placementFlow.Allows(screenPoint) || TouchOnUi(screenPoint)) return;
            if (!IsTracking)
            {
                SetStatus("Tracking is not ready. Scan the surroundings and try again.");
                return;
            }
            if (!TrySurface(screenPoint, out var pose))
            {
                SetStatus("No surface here yet. Aim at a textured wall or table.");
                return;
            }
            PlaceAnchor(pose);
        }

        public void AdjustPlacement(float widthMeters, float rotationDegrees, Vector2? screenPoint = null)
        {
            if (!active || paused || busy || recovered || presetId == null || anchor == null || visual == null ||
                !IsTracking || anchor.trackingState != TrackingState.Tracking || placementFlow.IsBlocked ||
                float.IsNaN(widthMeters) || float.IsInfinity(widthMeters) ||
                float.IsNaN(rotationDegrees) || float.IsInfinity(rotationDegrees)) return;
            if (screenPoint.HasValue && placementFlow.Allows(screenPoint.Value) &&
                !TouchOnUi(screenPoint.Value) && TrySurface(screenPoint.Value, out var pose) &&
                PlacementFlow.TryMoveOnOriginalPlane(new Pose(anchor.transform.position, anchor.transform.rotation),
                    pose, out var position)) visual.transform.position = position;
            this.widthMeters = Mathf.Clamp(widthMeters, 0.1f, 0.5f);
            twistDegrees = Mathf.Repeat(rotationDegrees + 180f, 360f) - 180f;
            visual.transform.localScale = ArtworkScale(this.widthMeters);
            visual.transform.localRotation = Quaternion.Euler(0f, twistDegrees, 0f) * Quaternion.Euler(90f, 0f, 0f);
            Changed?.Invoke();
        }

        public void Capture(Action<SpatialSnapshot> success, Action<string> failure)
        {
            if (success == null || failure == null) throw new ArgumentNullException("capture callback");
            if (!CanPublish)
            {
                failure("Keep scanning until the sticker is tracked and the surroundings are mapped.");
                return;
            }
#if UNITY_IOS && !UNITY_EDITOR
            captureRoutine = StartCoroutine(CaptureWorldMap(success, failure));
#else
            failure("Spatial map capture requires an ARKit iPhone.");
#endif
        }

        public void Recover(RecoveryData recovery)
        {
            CancelOperations();
            ClearPlacement();
            recovered = false;
            recoveredStickerId = null;
            presetId = null;
            UpdatePlacementGuidance();
            if (recovery == null || recovery.sticker == null || recovery.snapshot == null ||
                string.IsNullOrEmpty(recovery.sticker.id) || recovery.expiresAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            {
                SetStatus("This discovery expired. Choose the sticker again.");
                return;
            }
#if UNITY_IOS && !UNITY_EDITOR
            recoveryRoutine = StartCoroutine(RecoverWorldMap(recovery));
#else
            SetStatus("Spatial recovery requires an ARKit iPhone.");
#endif
        }

        private void CreateRig()
        {
            rig = new GameObject("Tagtag AR rig");
            rig.transform.SetParent(transform, false);
            rig.SetActive(false);
            var sessionObject = Child("AR session", rig.transform);
            session = sessionObject.AddComponent<ARSession>();
            sessionObject.AddComponent<ARInputManager>();
            session.matchFrameRateRequested = false;
            var originObject = Child("XR origin", rig.transform);
            origin = originObject.AddComponent<XROrigin>();
            var offset = Child("Camera offset", origin.transform);
            var cameraObject = Child("AR camera", offset.transform);
            camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(255, 254, 250, 255);
            camera.depth = 10;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 30f;
            cameraManager = cameraObject.AddComponent<ARCameraManager>();
            cameraBackground = cameraObject.AddComponent<ARCameraBackground>();
            cameraManager.frameReceived += OnCameraFrame;
            presentation.Changed += OnCameraPresentationChanged;
            var driver = cameraObject.AddComponent<TrackedPoseDriver>();
            positionInput = new InputAction("AR position", binding: "<HandheldARInputDevice>/devicePosition");
            rotationInput = new InputAction("AR rotation", binding: "<HandheldARInputDevice>/deviceRotation");
            driver.positionInput = new InputActionProperty(positionInput);
            driver.rotationInput = new InputActionProperty(rotationInput);
            driver.ignoreTrackingState = true;
            origin.Camera = camera;
            origin.CameraFloorOffsetObject = offset;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            origin.CameraYOffset = 0;
            planes = originObject.AddComponent<ARPlaneManager>();
            planes.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
            raycasts = originObject.AddComponent<ARRaycastManager>();
            anchors = originObject.AddComponent<ARAnchorManager>();
        }

        private void Update()
        {
            if (!active || rig == null) return;
            presentation.ObserveSession(ARSession.state);
            presentation.Tick(Time.realtimeSinceStartupAsDouble);
            var tracking = IsTracking;
            var mapped = MapReady;
            var anchorTracking = anchor != null && anchor.trackingState == TrackingState.Tracking;
            UpdatePlacementGuidance();
            var scanState = ScanState;
            if (tracking != wasTracking || mapped != wasMapped || anchorTracking != wasAnchorTracking ||
                scanState != observedScanState)
            {
                wasTracking = tracking;
                wasMapped = mapped;
                wasAnchorTracking = anchorTracking;
                observedScanState = scanState;
                Changed?.Invoke();
            }
            if (anchor != null && visual != null)
            {
                var visible = tracking && anchor.trackingState == TrackingState.Tracking && (recovered || presetId != null);
                visual.SetActive(visible);
            }
            if (recovered)
            {
                if (!IsTracking || anchor == null || anchor.trackingState != TrackingState.Tracking)
                {
                    recovered = false;
                    SetStatus("Tracking was lost. Reopen this sticker and scan again.");
                }
                else HandleRecoveredTap();
            }
        }

        private void UpdatePlacementGuidance()
        {
            var show = PlacementFlow.ShouldOutline(active && !paused && presetId != null && !recovered,
                IsTracking, busy, HasPlacementPreview, placementFlow.IsBlocked);
            var scan = active && !paused && presetId != null && !recovered && IsTracking && anchor == null;
            if (!scan || planes == null || camera == null)
            {
                foreach (var hatch in surfaceHatches.Values) if (hatch.Renderer != null) hatch.Renderer.enabled = false;
                SetSurfaceAvailable(false);
                return;
            }
            if (!show)
                foreach (var hatch in surfaceHatches.Values) if (hatch.Renderer != null) hatch.Renderer.enabled = false;
            if (Time.unscaledTime < nextSurfaceUpdate) return;
            nextSurfaceUpdate = Time.unscaledTime + 0.1f;
            if (!hatchMaterialChecked)
            {
                hatchMaterialChecked = true;
                hatchMaterial = Resources.Load<Material>("Tagtag/AR/PlaneHatch");
                if (hatchMaterial == null || hatchMaterial.shader == null || !hatchMaterial.shader.isSupported)
                {
                    Debug.LogError("[TagtagAR] Plane hatch material is unavailable.");
                    hatchMaterial = null;
                }
            }
            removedHatches.Clear();
            removedHatches.AddRange(surfaceHatches.Keys);
            var visible = false;
            GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
            foreach (var plane in planes.trackables)
            {
                if (plane.trackingState != TrackingState.Tracking || plane.subsumedBy != null ||
                    plane.boundary.Length < 3) continue;
                if (!surfaceHatches.TryGetValue(plane.trackableId, out var hatch) || hatch.Renderer == null)
                {
                    var hatchObject = Child("Placement surface hatch", plane.transform);
                    var filter = hatchObject.AddComponent<MeshFilter>();
                    filter.sharedMesh = new Mesh { name = "Placement surface polygon" };
                    filter.sharedMesh.MarkDynamic();
                    var renderer = hatchObject.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = hatchMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    hatch = new SurfaceHatch(hatchObject, filter.sharedMesh, renderer);
                    surfaceHatches[plane.trackableId] = hatch;
                }
                removedHatches.Remove(plane.trackableId);
                var valid = hatchMeshBuilder.Rebuild(plane.boundary, hatch.Mesh);
                var inView = valid && GeometryUtility.TestPlanesAABB(frustumPlanes, hatch.Renderer.bounds);
                hatch.Renderer.enabled = show && hatchMaterial != null && inView;
                visible |= inView;
            }
            foreach (var id in removedHatches)
            {
                var hatch = surfaceHatches[id];
                if (hatch.Object != null) Destroy(hatch.Object);
                if (hatch.Mesh != null) Destroy(hatch.Mesh);
                surfaceHatches.Remove(id);
            }
            SetSurfaceAvailable(visible);
        }

        private sealed class SurfaceHatch
        {
            public readonly GameObject Object;
            public readonly Mesh Mesh;
            public readonly MeshRenderer Renderer;

            public SurfaceHatch(GameObject hatchObject, Mesh mesh, MeshRenderer renderer)
            {
                Object = hatchObject;
                Mesh = mesh;
                Renderer = renderer;
            }
        }

        private void SetSurfaceAvailable(bool available)
        {
            if (HasPlacementSurface == available) return;
            HasPlacementSurface = available;
            if (active && !paused && !busy && anchor == null && presetId != null && !placementFlow.IsBlocked)
                SetStatus(available ? "Surface found. Tap it to place your sticker." :
                    "Move slowly until a surface appears, then tap to place.");
            else Changed?.Invoke();
        }

        private void DisposePlacementGuidance()
        {
            ClearSurfaceHatches();
        }

        private void ClearSurfaceHatches()
        {
            foreach (var hatch in surfaceHatches.Values)
            {
                if (hatch.Renderer != null) hatch.Renderer.enabled = false;
                if (hatch.Object != null) Destroy(hatch.Object);
                if (hatch.Mesh != null) Destroy(hatch.Mesh);
            }
            surfaceHatches.Clear();
        }

        private async void PlaceAnchor(Pose pose)
        {
            busy = true;
            var attempt = ++generation;
            SetStatus("Attaching sticker to this surface…");
            UpdatePlacementGuidance();
            try
            {
                var result = await anchors.TryAddAnchorAsync(pose);
                if (attempt != generation || !this)
                {
                    if (result.value != null) Destroy(result.value.gameObject);
                    return;
                }
                if (!result.status.IsSuccess() || result.value == null)
                {
                    SetStatus("Could not attach there. Choose another surface.");
                    return;
                }
                anchor = result.value;
                if (!CreateVisual(presetId))
                {
                    Destroy(anchor.gameObject);
                    anchor = null;
                    return;
                }
                visual.transform.SetParent(anchor.transform, false);
                visual.transform.localPosition = new Vector3(0f, 0.002f, 0f);
                visual.transform.localRotation = Quaternion.Euler(0f, twistDegrees, 0f) *
                    Quaternion.Euler(90f, 0f, 0f);
                visual.transform.localScale = ArtworkScale(widthMeters);
                UpdatePlacementGuidance();
                SetStatus("Pinch and twist to adjust. Scan around the sticker before publishing.");
            }
            catch (Exception)
            {
                if (attempt == generation) ClearPlacement();
                SetStatus("Could not attach there. Choose another surface.");
            }
            finally { if (attempt == generation) { busy = false; Changed?.Invoke(); } }
        }

        private bool TrySurface(Vector2 point, out Pose pose)
        {
            pose = default;
            if (raycasts == null || planes == null ||
                !raycasts.Raycast(point, hits, TrackableType.PlaneWithinPolygon)) return false;
            foreach (var hit in hits)
            {
                var plane = planes.GetPlane(hit.trackableId);
                if (plane == null || plane.trackingState != TrackingState.Tracking || plane.subsumedBy != null) continue;
                pose = hit.pose;
                return true;
            }
            return false;
        }

        private void HandleRecoveredTap()
        {
            if (Input.touchCount != 1 || !CanCollect) return;
            var touch = Input.GetTouch(0);
            if (touch.phase != UnityEngine.TouchPhase.Ended || !placementFlow.Allows(touch.position) ||
                TouchOnUi(touch.position)) return;
            var ray = camera.ScreenPointToRay(touch.position);
            if (!Physics.Raycast(ray, out var hit, 3.25f)) return;
            if (hit.collider == null || hit.collider.gameObject != visual) return;
            if (!ArGates.CanCollect(IsTracking, recovered, anchor.trackingState == TrackingState.Tracking,
                Vector3.Distance(camera.transform.position, hit.point), true)) return;
            StickerTapped?.Invoke(recoveredStickerId);
        }

        private bool TouchOnUi(Vector2 screenPoint)
        {
            var document = GetComponent<UIDocument>();
            var root = document == null ? null : document.rootVisualElement;
            var panel = root?.panel;
            if (panel == null) return false;
            var topLeft = new Vector2(screenPoint.x, Screen.height - screenPoint.y);
            var point = RuntimePanelUtils.ScreenToPanel(panel, topLeft);
            for (var element = panel.Pick(point); element != null && element != root; element = element.parent)
            {
                if (element is Button || element is TextField || element is ScrollView) return true;
                if (element.name == "STICK Camera Surface") return false;
                if (element.resolvedStyle.backgroundColor.a > 0.1f) return true;
            }
            return false;
        }

        private Vector3 ArtworkScale(float width) => customArtwork == null ? Vector3.one * width :
            StickerArtwork.Scale(width, customArtwork.width, customArtwork.height);

        private bool CreateVisual(string id)
        {
            var template = Resources.Load<Material>("Tagtag/AR/DeviceSticker");
            var artwork = customArtwork != null ? customArtwork : Resources.Load<Texture2D>("Tagtag/Presets/" + id);
            if (template == null || template.shader == null || !template.shader.isSupported || artwork == null)
            {
                Debug.LogError("[TagtagSticker] Sticker material or artwork is unavailable: preset=" + id +
                    " material=" + (template != null) + " artwork=" + (artwork != null));
                SetStatus("Sticker artwork could not load. Try again.");
                return false;
            }
            Material nextMaterial = null;
            GameObject nextVisual = null;
            try
            {
                nextMaterial = new Material(template);
                nextMaterial.mainTexture = artwork;
                nextVisual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                nextVisual.name = "Tracked tagtag sticker";
                nextVisual.GetComponent<Renderer>().sharedMaterial = nextMaterial;
                visual = nextVisual;
                material = nextMaterial;
                return true;
            }
            catch (Exception exception)
            {
                if (nextVisual != null) Destroy(nextVisual);
                if (nextMaterial != null) Destroy(nextMaterial);
                Debug.LogError("[TagtagSticker] Could not create sticker visual: " + exception.Message);
                SetStatus("Sticker artwork could not load. Try again.");
                return false;
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        private IEnumerator CaptureWorldMap(Action<SpatialSnapshot> success, Action<string> failure)
        {
            busy = true;
            var attempt = ++generation;
            SetStatus("Saving this sticker and its surroundings…");
            try
            {
                mapRequest = ArKit.GetARWorldMapAsync();
                var started = Time.realtimeSinceStartup;
                while (mapRequest.HasValue && mapRequest.Value.status == ARWorldMapRequestStatus.Pending &&
                    Time.realtimeSinceStartup - started < 20f) yield return null;
                if (attempt != generation) yield break;
                if (!mapRequest.HasValue || mapRequest.Value.status != ARWorldMapRequestStatus.Success ||
                    !IsTracking || !MapReady || anchor == null || anchor.trackingState != TrackingState.Tracking)
                {
                    failure("The spatial map is not ready. Keep the preview and scan from more angles.");
                    yield break;
                }
                byte[] bytes;
                try
                {
                    using (var map = mapRequest.Value.GetWorldMap())
                    using (var serialized = map.Serialize(Allocator.Temp))
                        bytes = WorldMapEnvelope.Encode(anchor.trackableId.subId1, anchor.trackableId.subId2, serialized.ToArray());
                }
                catch (Exception)
                {
                    failure("The spatial map could not be captured. Keep the preview and try again.");
                    yield break;
                }
                success(new SpatialSnapshot
                {
                    worldMapBase64 = Convert.ToBase64String(bytes),
                    position = visual.transform.localPosition,
                    rotation = visual.transform.localRotation,
                    widthMeters = widthMeters
                });
                SetStatus("Spatial map captured. Publishing…");
            }
            finally
            {
                DisposeMapRequest();
                if (attempt == generation) { busy = false; Changed?.Invoke(); }
                captureRoutine = null;
            }
        }

        private IEnumerator RecoverWorldMap(RecoveryData data)
        {
            busy = true;
            var attempt = ++generation;
            SetStatus("Opening the saved surroundings…");
            var snapshot = data.snapshot;
            customArtwork = null;
            if (!string.IsNullOrEmpty(data.sticker.designId))
            {
                customArtwork = StickerArtwork.Get(null, data.sticker.designId, data.sticker.artworkUrl, data.sticker.thumbnailUrl, false);
                if (customArtwork == null) { SetStatus("Sticker artwork could not load. Try discovery again."); busy = false; yield break; }
            }
            byte[] encoded;
            try { encoded = Convert.FromBase64String(snapshot.worldMapBase64); }
            catch (Exception) { SetStatus("This spatial map cannot be opened."); busy = false; yield break; }
            if (!WorldMapEnvelope.TryDecode(encoded, out var anchorId, out var mapBytes) ||
                (!IsPreset(data.sticker.presetId) && string.IsNullOrEmpty(data.sticker.designId)) || snapshot.widthMeters < 0.1f || snapshot.widthMeters > 0.5f)
            { SetStatus("This spatial map is incomplete."); busy = false; yield break; }
            var started = Time.realtimeSinceStartup;
            while (ArKit == null && Time.realtimeSinceStartup - started < 10f) yield return null;
            if (attempt != generation) yield break;
            if (ArKit == null || !ARKitSessionSubsystem.worldMapSupported)
            { SetStatus("Spatial recovery is unavailable on this iPhone."); busy = false; yield break; }
            session.Reset();
            cameraFrameAt = 0;
            presentation.ResetFrame(Time.realtimeSinceStartupAsDouble);
            yield return null;
            yield return null;
            started = Time.realtimeSinceStartup;
            while (anchors.trackables.count > 0 && Time.realtimeSinceStartup - started < 3f) yield return null;
            if (attempt != generation) yield break;
            if (anchors.trackables.count > 0)
            { SetStatus("The previous session is still closing. Try again."); busy = false; yield break; }
            using (var native = new NativeArray<byte>(mapBytes, Allocator.Temp))
            {
                if (!ARWorldMap.TryDeserialize(native, out var worldMap))
                { SetStatus("This spatial map cannot be read."); busy = false; yield break; }
                using (worldMap) ArKit.ApplyWorldMap(worldMap);
            }
            appliedMap = true;
            cameraFrameAt = 0;
            presentation.ResetFrame(Time.realtimeSinceStartupAsDouble);
            SetStatus("Scan the original spot. The sticker appears only after its anchor matches.");
            var gate = new RecoveryGate();
            started = Time.realtimeSinceStartup;
            var observedFrameNumber = cameraFrameNumber;
            var observedFrameAt = 0d;
            while (Time.realtimeSinceStartup - started < 45f)
            {
                if (attempt != generation) yield break;
                if (data.expiresAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                { busy = false; SetStatus("This discovery expired. Choose the sticker again."); yield break; }
                var candidate = anchors.GetAnchor(anchorId);
                var nextFrame = cameraFrameNumber != observedFrameNumber &&
                    cameraFrameAt > 0 && Time.realtimeSinceStartupAsDouble - cameraFrameAt < 0.5;
                if (nextFrame)
                {
                    var frameInterval = observedFrameAt == 0d ? 0f : (float)(cameraFrameAt - observedFrameAt);
                    observedFrameAt = cameraFrameAt;
                    observedFrameNumber = cameraFrameNumber;
                    if (gate.Observe(candidate != null && candidate.trackingState == TrackingState.Tracking,
                        IsTracking, frameInterval <= 0.2f, frameInterval))
                    {
                        anchor = candidate;
                        if (!CreateVisual(data.sticker.presetId))
                        {
                            anchor = null;
                            busy = false;
                            yield break;
                        }
                        visual.transform.SetParent(anchor.transform, false);
                        visual.transform.localPosition = snapshot.position;
                        visual.transform.localRotation = snapshot.rotation;
                        visual.transform.localScale = ArtworkScale(snapshot.widthMeters);
                        widthMeters = snapshot.widthMeters;
                        recoveredStickerId = data.sticker.id;
                        recovered = true;
                        busy = false;
                        SetStatus("Sticker found. Tap it within three metres to unlock its note.");
                        yield break;
                    }
                }
                else if (cameraFrameAt == 0 || Time.realtimeSinceStartupAsDouble - cameraFrameAt > 0.5)
                    gate.Observe(false, false, false, 0f);
                yield return null;
            }
            busy = false;
            SetStatus("Original spot was not recognized. Move around it and try again.");
        }
#endif

        private void OnCameraFrame(ARCameraFrameEventArgs frame)
        {
            if (!active || paused) return;
            var textures = frame.textures;
            var validTextures = textures != null && frame.propertyNameIds != null &&
                frame.propertyNameIds.Count == textures.Count;
            if (validTextures)
                foreach (var texture in textures)
                    if (texture == null || texture.width <= 0 || texture.height <= 0)
                    { validTextures = false; break; }
            var displayable = Tagtag.AR.CameraPresentation.CanDisplayFrame(textures == null ? 0 : textures.Count,
                validTextures, cameraBackground != null && cameraBackground.backgroundRenderingEnabled,
                cameraBackground != null && cameraBackground.material != null,
                cameraBackground != null && cameraBackground.currentRenderingMode != XRCameraBackgroundRenderingMode.None);
            if (!displayable) cameraFrameAt = 0d;
            presentation.ObserveFrame(Time.realtimeSinceStartupAsDouble, displayable);
            if (!displayable || presentation.State != CameraPresentationState.Live) return;
            cameraFrameAt = Time.realtimeSinceStartupAsDouble;
            cameraFrameNumber++;
        }

        private void OnApplicationPause(bool value)
        {
            suspension.SetPaused(value);
            UpdateCameraSuspension();
        }

        private void OnApplicationFocus(bool focused)
        {
            suspension.SetFocused(focused);
            UpdateCameraSuspension();
        }

        private void UpdateCameraSuspension()
        {
            var suspended = suspension.IsSuspended || creationSuspended;
            if (suspended == paused) return;
            paused = suspended;
            if (suspended)
            {
                cameraFrameAt = 0d;
                StopCameraPermissionRequest();
                CancelOperations();
                if (!resumeCreationPlacement) ResetCameraContent();
                if (rig != null) rig.SetActive(false);
                presentation.Interrupt();
                if (active) SetStatus("Camera interrupted. Scan the area again.");
            }
            else if (active)
            {
                if (resumeCreationPlacement && rig != null)
                {
                    resumeCreationPlacement = false;
                    cameraFrameAt = 0;
                    presentation.Begin(Time.realtimeSinceStartupAsDouble);
                    rig.SetActive(true);
                    SetStatus("Scan the surroundings to continue placing your sticker.");
                }
                else StartCamera();
            }
        }

        private void StartCamera()
        {
            StopCameraPermissionRequest();
            CancelOperations();
            ResetCameraContent();
            if (rig != null) rig.SetActive(false);
            cameraFrameAt = 0d;
            presentation.Begin(Time.realtimeSinceStartupAsDouble);
            permissionRoutine = StartCoroutine(PrepareCamera());
        }

        private IEnumerator PrepareCamera()
        {
            var attempt = cameraGeneration;
            yield return ARSession.CheckAvailability();
            if (attempt != cameraGeneration || !active || paused) yield break;
            presentation.ObserveSession(ARSession.state);
            if (presentation.State == CameraPresentationState.Unavailable)
            {
                permissionRoutine = null;
                yield break;
            }
#if UNITY_IOS && !UNITY_EDITOR
            var nativeStatus = ReadNativeCameraAuthorization();
            var decision = ObserveCameraAuthorization(nativeStatus, "before-request");
            if (decision.Action == CameraAuthorizationAction.Request)
            {
                try { TagtagCameraRequestAccess(); }
                catch (EntryPointNotFoundException exception)
                {
                    Debug.LogError("[TagtagCameraPermission] Native request bridge is missing: " + exception.Message);
                    nativeStatus = -1;
                }
                while (nativeStatus == 0)
                {
                    yield return null;
                    if (attempt != cameraGeneration || !active || paused) yield break;
                    nativeStatus = ReadNativeCameraAuthorization();
                }
                decision = ObserveCameraAuthorization(nativeStatus, "after-request");
            }
            permissionRoutine = null;
            switch (decision.Action)
            {
                case CameraAuthorizationAction.Start:
                    StartAuthorizedCamera();
                    break;
                case CameraAuthorizationAction.Deny:
                    presentation.PermissionDenied();
                    SetStatus(nativeStatus == 1 ? "Camera access is restricted on this device." :
                        "Allow camera access to find stickers in AR. Open Settings to allow access.");
                    break;
                default:
                    presentation.StartupFailed();
                    SetStatus("Camera permission could not be checked. Try again.");
                    break;
            }
#else
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if (attempt != cameraGeneration || !active || paused) yield break;
            permissionRoutine = null;
            if (Application.HasUserAuthorization(UserAuthorization.WebCam)) StartAuthorizedCamera();
            else
            {
                presentation.PermissionDenied();
                SetStatus("Allow camera access to find stickers in AR. Open Settings to allow access.");
            }
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        private static int ReadNativeCameraAuthorization()
        {
            try { return TagtagCameraAuthorizationStatus(); }
            catch (EntryPointNotFoundException exception)
            {
                Debug.LogError("[TagtagCameraPermission] Native status bridge is missing: " + exception.Message);
                return -1;
            }
        }

        private static CameraAuthorizationDecision ObserveCameraAuthorization(int nativeStatus, string phase)
        {
            var unityAuthorized = Application.HasUserAuthorization(UserAuthorization.WebCam);
            var decision = CameraAuthorizationPolicy.Decide(nativeStatus, unityAuthorized);
            Debug.Log("[TagtagCameraPermission] phase=" + phase + " native=" + nativeStatus +
                " unity=" + unityAuthorized + " disagree=" + decision.UnityDisagrees +
                " app=" + Application.identifier + " ar=" + ARSession.state);
            return decision;
        }
#endif

        private void StartAuthorizedCamera()
        {
            if (!active || paused) return;
            presentation.PermissionGranted(Time.realtimeSinceStartupAsDouble);
            rig.SetActive(true);
        }

        private void StopCameraPermissionRequest()
        {
            cameraGeneration++;
            if (permissionRoutine == null) return;
            StopCoroutine(permissionRoutine);
            permissionRoutine = null;
        }

        private void OnCameraPresentationChanged()
        {
            switch (presentation.State)
            {
                case CameraPresentationState.Unavailable:
                    SetStatus("AR camera is unavailable on this device.");
                    return;
                case CameraPresentationState.Failed:
                    SetStatus("Camera could not start. Try again.");
                    return;
                case CameraPresentationState.Interrupted:
                    SetStatus("Camera feed stopped. Try again.");
                    return;
                case CameraPresentationState.Live:
                    if (Status.StartsWith("Camera feed stopped", StringComparison.Ordinal) ||
                        Status.StartsWith("Camera interrupted", StringComparison.Ordinal) ||
                        Status.StartsWith("Camera could not start", StringComparison.Ordinal))
                    {
                        SetStatus(string.IsNullOrEmpty(presetId) ? "Look around to discover a sticker." :
                            "Move slowly until a surface appears, then tap to place.");
                        return;
                    }
                    break;
            }
            Changed?.Invoke();
        }

        private void CancelOperations()
        {
            ++generation;
            if (captureRoutine != null) StopCoroutine(captureRoutine);
            if (recoveryRoutine != null) StopCoroutine(recoveryRoutine);
            captureRoutine = null;
            recoveryRoutine = null;
#if UNITY_IOS
            DisposeMapRequest();
            if (appliedMap && ArKit != null)
            {
                ArKit.ApplyWorldMap(default);
                session.Reset();
                cameraFrameAt = 0;
                presentation.ResetFrame(Time.realtimeSinceStartupAsDouble);
            }
            appliedMap = false;
#endif
            busy = false;
        }

#if UNITY_IOS
        private void DisposeMapRequest()
        {
            if (!mapRequest.HasValue) return;
            mapRequest.Value.Dispose();
            mapRequest = null;
        }
#endif

        private void ClearPlacement()
        {
            if (visual != null) Destroy(visual);
            if (material != null) Destroy(material);
            if (anchor != null) Destroy(anchor.gameObject);
            visual = null;
            material = null;
            anchor = null;
        }

        private void ResetCameraContent()
        {
            ClearPlacement();
            recovered = false;
            recoveredStickerId = null;
            ClearSurfaceHatches();
            SetSurfaceAvailable(false);
            nextSurfaceUpdate = 0f;
        }

        private void SetStatus(string value)
        {
            Status = value;
            Changed?.Invoke();
        }

        private static bool IsPreset(string id)
        {
            return id == "taggi-1" || id == "taggi-2" || id == "taggi-3" || id == "taggi-4";
        }

        private static GameObject Child(string name, Transform parent)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private void OnDestroy()
        {
            StopCameraPermissionRequest();
            CancelOperations();
            if (cameraManager != null) cameraManager.frameReceived -= OnCameraFrame;
            presentation.Changed -= OnCameraPresentationChanged;
            ClearPlacement();
            DisposePlacementGuidance();
            if (rig != null) Destroy(rig);
            positionInput?.Dispose();
            rotationInput?.Dispose();
        }
    }
}
