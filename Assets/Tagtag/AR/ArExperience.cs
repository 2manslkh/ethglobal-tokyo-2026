using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
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
    public sealed class ArExperience : MonoBehaviour, IArExperience
    {
        public event Action Changed;
        public event Action<string> StickerTapped;
        public string Status { get; private set; } = "Open STICK to scan a surface.";
        public bool IsTracking => active && !paused && ARSession.state == ARSessionState.SessionTracking &&
            cameraFrameAt > 0 && Time.realtimeSinceStartupAsDouble - cameraFrameAt < 1.5;
        public bool CanPublish => ArGates.CanPublish(IsTracking, anchor != null && anchor.trackingState == TrackingState.Tracking,
            MapReady, visual != null && anchor != null, busy);
        public bool CanCollect => ArGates.CanCollect(IsTracking, recovered, anchor != null && anchor.trackingState == TrackingState.Tracking,
            camera == null || visual == null ? float.PositiveInfinity : Vector3.Distance(camera.transform.position, visual.transform.position), true);

        private GameObject rig;
        private XROrigin origin;
        private ARSession session;
        private ARCameraManager cameraManager;
        private ARRaycastManager raycasts;
        private ARPlaneManager planes;
        private ARAnchorManager anchors;
        private Camera camera;
        private GameObject visual;
        private Material material;
        private ARAnchor anchor;
        private readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
        private InputAction positionInput;
        private InputAction rotationInput;
        private string presetId;
        private string recoveredStickerId;
        private bool active;
        private bool paused;
        private bool busy;
        private bool recovered;
        private bool wasTracking;
        private bool wasMapped;
        private bool wasAnchorTracking;
        private bool appliedMap;
        private Pose previewPose;
        private float widthMeters = 0.2f;
        private float twistDegrees;
        private double cameraFrameAt;
        private int generation;
        private Coroutine recoveryRoutine;
        private Coroutine captureRoutine;
        private bool gestureActive;
        private float gestureDistance;
        private float gestureAngle;
#if UNITY_IOS
        private ARWorldMapRequest? mapRequest;
        private ARKitSessionSubsystem ArKit => session != null ? session.subsystem as ARKitSessionSubsystem : null;
        private bool MapReady => ArKit != null && ARKitSessionSubsystem.worldMapSupported &&
            ArKit.worldMappingStatus == ARWorldMappingStatus.Mapped;
#else
        private bool MapReady => false;
#endif

        public void Enter()
        {
            if (rig == null) CreateRig();
            active = true;
            paused = false;
            rig.SetActive(true);
            SetStatus(string.IsNullOrEmpty(presetId) ? "Look around to discover a sticker." :
                "Move slowly until a surface appears, then tap to place.");
        }

        public void Exit()
        {
            active = false;
            CancelOperations();
            if (recovered)
            {
                ClearPlacement();
                recovered = false;
                recoveredStickerId = null;
            }
            if (rig != null) rig.SetActive(false);
            SetStatus("Open STICK to scan a surface.");
        }

        public void SelectPreset(string id)
        {
            CancelOperations();
            ClearPlacement();
            recoveredStickerId = null;
            recovered = false;
            presetId = IsPreset(id) ? id : null;
            if (presetId == null) SetStatus("Choose a sticker to place.");
            else SetStatus("Move slowly until a surface appears, then tap to place.");
        }

        public void CancelPlacement()
        {
            CancelOperations();
            ClearPlacement();
            presetId = null;
            recoveredStickerId = null;
            recovered = false;
            SetStatus("Placement cancelled.");
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
            camera.backgroundColor = Color.clear;
            camera.depth = 10;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 30f;
            cameraManager = cameraObject.AddComponent<ARCameraManager>();
            cameraObject.AddComponent<ARCameraBackground>();
            cameraManager.frameReceived += OnCameraFrame;
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
            var tracking = IsTracking;
            var mapped = MapReady;
            var anchorTracking = anchor != null && anchor.trackingState == TrackingState.Tracking;
            if (tracking != wasTracking || mapped != wasMapped || anchorTracking != wasAnchorTracking)
            {
                wasTracking = tracking;
                wasMapped = mapped;
                wasAnchorTracking = anchorTracking;
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
                return;
            }
            if (busy || string.IsNullOrEmpty(presetId) || !tracking) return;
            if (anchor == null) UpdateSurfacePreview();
            HandlePlacementTouches();
        }

        private void UpdateSurfacePreview()
        {
            if (!TrySurface(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), out previewPose))
            {
                if (visual != null) visual.SetActive(false);
                return;
            }
            if (visual == null) CreateVisual(presetId);
            visual.SetActive(true);
            visual.transform.SetPositionAndRotation(previewPose.position, previewPose.rotation * Quaternion.Euler(90f, twistDegrees, 0f));
            visual.transform.localScale = Vector3.one * widthMeters;
        }

        private void HandlePlacementTouches()
        {
            if (Input.touchCount == 2 && anchor != null)
            {
                var first = Input.GetTouch(0);
                var second = Input.GetTouch(1);
                if (TouchOnUi(first.fingerId, first.position) || TouchOnUi(second.fingerId, second.position))
                { gestureActive = false; return; }
                var delta = second.position - first.position;
                if (gestureActive)
                {
                    widthMeters = Mathf.Clamp(widthMeters * delta.magnitude / Mathf.Max(1f, gestureDistance), 0.1f, 0.5f);
                    twistDegrees += Mathf.DeltaAngle(gestureAngle, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                    visual.transform.localScale = Vector3.one * widthMeters;
                    visual.transform.localRotation = Quaternion.Euler(90f, twistDegrees, 0f);
                    Changed?.Invoke();
                }
                gestureDistance = delta.magnitude;
                gestureAngle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                gestureActive = true;
                return;
            }
            gestureActive = false;
            if (Input.touchCount != 1) return;
            var touch = Input.GetTouch(0);
            if (touch.phase != UnityEngine.TouchPhase.Ended || TouchOnUi(touch.fingerId, touch.position)) return;
            if (anchor == null && TrySurface(touch.position, out var pose)) PlaceAnchor(pose);
        }

        private async void PlaceAnchor(Pose pose)
        {
            busy = true;
            var attempt = ++generation;
            SetStatus("Attaching sticker to this surface…");
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
                if (visual == null) CreateVisual(presetId);
                visual.transform.SetParent(anchor.transform, false);
                visual.transform.localPosition = new Vector3(0f, 0.002f, 0f);
                visual.transform.localRotation = Quaternion.Euler(90f, twistDegrees, 0f);
                visual.transform.localScale = Vector3.one * widthMeters;
                SetStatus("Pinch and twist to adjust. Scan around the sticker before publishing.");
            }
            catch (Exception)
            {
                SetStatus("Could not attach there. Choose another surface.");
            }
            finally { if (attempt == generation) { busy = false; Changed?.Invoke(); } }
        }

        private bool TrySurface(Vector2 point, out Pose pose)
        {
            pose = default;
            if (raycasts == null || !raycasts.Raycast(point, hits, TrackableType.PlaneWithinPolygon)) return false;
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
            if (touch.phase != UnityEngine.TouchPhase.Ended || TouchOnUi(touch.fingerId, touch.position)) return;
            var ray = camera.ScreenPointToRay(touch.position);
            if (!Physics.Raycast(ray, out var hit, 3.25f)) return;
            if (hit.collider == null || hit.collider.gameObject != visual) return;
            if (!ArGates.CanCollect(IsTracking, recovered, anchor.trackingState == TrackingState.Tracking,
                Vector3.Distance(camera.transform.position, hit.point), true)) return;
            StickerTapped?.Invoke(recoveredStickerId);
        }

        private bool TouchOnUi(int fingerId, Vector2 screenPoint)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fingerId)) return true;
            var document = GetComponent<UIDocument>();
            var root = document == null ? null : document.rootVisualElement;
            var panel = root?.panel;
            if (panel == null) return false;
            var topLeft = new Vector2(screenPoint.x, Screen.height - screenPoint.y);
            var point = RuntimePanelUtils.ScreenToPanel(panel, topLeft);
            for (var element = panel.Pick(point); element != null && element != root; element = element.parent)
            {
                if (element is Button || element is TextField || element is ScrollView ||
                    element.resolvedStyle.backgroundColor.a > 0.1f) return true;
            }
            return false;
        }

        private void CreateVisual(string id)
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
            visual.name = "Tracked tagtag sticker";
            material = new Material(Shader.Find("Unlit/Transparent"));
            material.mainTexture = Resources.Load<Texture2D>("Tagtag/Presets/" + id);
            visual.GetComponent<Renderer>().sharedMaterial = material;
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
            byte[] encoded;
            try { encoded = Convert.FromBase64String(snapshot.worldMapBase64); }
            catch (Exception) { SetStatus("This spatial map cannot be opened."); busy = false; yield break; }
            if (!WorldMapEnvelope.TryDecode(encoded, out var anchorId, out var mapBytes) ||
                !IsPreset(data.sticker.presetId) || snapshot.widthMeters < 0.1f || snapshot.widthMeters > 0.5f)
            { SetStatus("This spatial map is incomplete."); busy = false; yield break; }
            var started = Time.realtimeSinceStartup;
            while (ArKit == null && Time.realtimeSinceStartup - started < 10f) yield return null;
            if (attempt != generation) yield break;
            if (ArKit == null || !ARKitSessionSubsystem.worldMapSupported)
            { SetStatus("Spatial recovery is unavailable on this iPhone."); busy = false; yield break; }
            session.Reset();
            cameraFrameAt = 0;
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
            SetStatus("Scan the original spot. The sticker appears only after its anchor matches.");
            var gate = new RecoveryGate();
            started = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - started < 45f)
            {
                if (attempt != generation) yield break;
                if (data.expiresAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                { busy = false; SetStatus("This discovery expired. Choose the sticker again."); yield break; }
                var candidate = anchors.GetAnchor(anchorId);
                if (gate.Observe(candidate != null && candidate.trackingState == TrackingState.Tracking,
                    IsTracking, cameraFrameAt > 0 && Time.realtimeSinceStartupAsDouble - cameraFrameAt < 0.5,
                    Time.unscaledDeltaTime))
                {
                    anchor = candidate;
                    CreateVisual(data.sticker.presetId);
                    visual.transform.SetParent(anchor.transform, false);
                    visual.transform.localPosition = snapshot.position;
                    visual.transform.localRotation = snapshot.rotation;
                    visual.transform.localScale = Vector3.one * snapshot.widthMeters;
                    widthMeters = snapshot.widthMeters;
                    recoveredStickerId = data.sticker.id;
                    recovered = true;
                    busy = false;
                    SetStatus("Sticker found. Tap it within three metres to unlock its note.");
                    yield break;
                }
                yield return null;
            }
            busy = false;
            SetStatus("Original spot was not recognized. Move around it and try again.");
        }
#endif

        private void OnCameraFrame(ARCameraFrameEventArgs frame)
        {
            if (!paused) cameraFrameAt = Time.realtimeSinceStartupAsDouble;
        }

        private void OnApplicationPause(bool value)
        {
            paused = value;
            cameraFrameAt = 0;
            if (value)
            {
                CancelOperations();
                recovered = false;
                if (visual != null) visual.SetActive(false);
                SetStatus("Camera interrupted. Scan the area again.");
            }
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
            CancelOperations();
            if (cameraManager != null) cameraManager.frameReceived -= OnCameraFrame;
            ClearPlacement();
            if (rig != null) Destroy(rig);
            positionInput?.Dispose();
            rotationInput?.Dispose();
        }
    }
}
