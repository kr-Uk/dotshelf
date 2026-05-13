using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace BookshelfPorting.Runtime
{
    public enum CameraMode
    {
        Overview,
        Frontal,
        Whiteboard,
        Notebook
    }

    // 노트북 포커스 진입 시 camera pose 산출 방식. 테마별 desk skin 의 Screen 변환 양상이 달라
    // 같은 식이 모든 case 에 맞지 않음 — 테마/desk entry 별로 골라 쓰기 위한 mode.
    //
    //   ScreenAnchorBaked       — 기존 (vintage / 기본 procedural laptop) 경로. Screen 자식
    //                             CameraFocusPoint (Screen-local (0, -0.58, 0.024)) 의 prefab-baked
    //                             offset 을 그대로 camera position 으로 사용. laptop 슬랜트 기준이라
    //                             native laptop 화면 정면 framing 에 fit. monitor 모드처럼 Screen 이
    //                             upright 으로 회전된 desk skin 에서는 camera 가 화면 아래로 떨어져
    //                             올려다보는 어색한 각도가 됨 — 그런 desk 는 ScreenForwardGeometry.
    //
    //   ScreenForwardGeometry   — DeepSea monitor 모드용 신규 경로. screen.forward × notebookFocusDistance
    //                             단위 vector × world distance 로 camera 배치 → Screen 의 lossyScale /
    //                             position / rotation 이 어떻게 바뀌어도 일정한 체감 거리/각도.
    //                             AlignNotebookScreenToMonitor 가 Screen.rotation 을 monitor 정면과
    //                             align 시켜 둔 경우에만 의도대로 작동.
    //
    // 향후 테마 추가 시 — 테이블 높이/laptop slant 가 native 와 비슷하면 ScreenAnchorBaked, monitor
    // 처럼 upright + scale 변화가 크면 ScreenForwardGeometry. 디자이너가 desk 카탈로그 entry 의
    // notebookFocusCameraMode 필드로 선택.
    public enum NotebookFocusCameraMode
    {
        ScreenAnchorBaked = 0,
        ScreenForwardGeometry = 1,
    }

    public class CameraController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera = null;
        [SerializeField] private Transform overviewAnchor = null;
        [SerializeField] private Transform frontalAnchor = null;
        [SerializeField] private Transform whiteboardAnchor = null;
        [SerializeField] private Transform notebookAnchor = null;
        [SerializeField] private Transform bookViewAnchor = null;
        [SerializeField] private float moveDuration = 0.4f;
        [SerializeField] private float overviewFieldOfView = 52f;
        [SerializeField] private float whiteboardFieldOfView = 38f;
        [SerializeField] private float whiteboardFocusDistance = 1.45f;
        [SerializeField] private float notebookFieldOfView = 24f;
        [Tooltip("노트북/모니터 화면 정면에서 camera 까지의 world distance (meter). 화면 기하학에서 직접 derive — " +
                 "Screen.lossyScale 변화 (DeepSea monitor 모드 등) 에도 같은 체감 거리 유지. 음수 입력 시 camera 가 " +
                 "screen.forward 반대편에 배치되므로 GLB convention 따라 부호 조정 가능.")]
        [SerializeField] private float notebookFocusDistance = 0.45f;
        [Tooltip("노트북 focus 진입 / 메인 복귀 transition duration (sec). 책장/대시보드의 moveDuration (0.4s) " +
                 "보다 길어 화면 안으로 부드럽게 끌려들어가는 cinematic 체감. 너무 크면 (>1.2s) 무거워지고 너무 " +
                 "작으면 (<0.3s) 급해 보임. 디자이너 튜닝 가능.")]
        [SerializeField] private float notebookTransitionDuration = 0.85f;
        [SerializeField] private float frontalDefaultFieldOfView = 60f;
        [SerializeField] private float frontalDefaultZoomDistance = 3.25f;
        [SerializeField] private float frontalMinZoomDistance = 0.8f;
        [SerializeField] private float frontalMaxZoomDistance = 4.5f;
        [SerializeField] private float frontalScrollZoomStep = 0.45f;
        [SerializeField] private float frontalZoomSmoothDuration = 0.15f;
        [SerializeField] private float wheelScrollReference = 120f;
        [SerializeField] private float frontalPanSensitivity = 0.002f;
        [SerializeField] private float frontalMinPanX = -0.9f;
        [SerializeField] private float frontalMaxPanX = 0.9f;
        [SerializeField] private float frontalMinCameraY = 0.3f;
        [SerializeField] private float frontalMaxCameraY = 1.8f;

        // MainView: the default main-scene pose. Derived at runtime from frontalAnchor
        // by pulling straight back along its forward axis (same look direction as the
        // bookshelf focus) and slightly widening the FOV so the surrounding room
        // becomes visible without orbiting the camera. The bookshelf stays centered.
        // QuarterView remains the existing overviewAnchor pose, untouched.
        [SerializeField] private float mainViewPullBack = 0.4f;
        [SerializeField] private float mainViewVerticalOffset = 0.04f;
        [SerializeField] private float mainViewSideAngleDegrees = 0f;
        [SerializeField] private float mainViewLookTargetDistance = 1.2f;
        [SerializeField] private float mainViewFieldOfView = 52f;

        private CameraTransitionSegment activeSegment;
        private bool isTransitioning;
        private bool isFrontalZooming;
        private float frontalZoomStartDistance;
        private float frontalZoomCurrentDistance;
        private float frontalZoomTargetDistance;
        private float frontalZoomElapsed;
        private Vector2 frontalPanOffset;
        private Func<bool> frontalZoomInputPredicate;
        public event Action<CameraMode> ModeChanged;
        public event Action<CameraMode> TransitionCompleted;

        public CameraMode CurrentMode { get; private set; } = CameraMode.Overview;
        public bool IsTransitioning => isTransitioning;
        public Transform BookViewAnchor => bookViewAnchor;
        public float FrontalZoomDistance => frontalZoomCurrentDistance;
        public float FrontalZoomInAmount => Mathf.InverseLerp(
            Mathf.Max(frontalMinZoomDistance, frontalMaxZoomDistance),
            Mathf.Min(frontalMinZoomDistance, frontalMaxZoomDistance),
            frontalZoomCurrentDistance);

        private struct CameraTransitionSegment
        {
            public Vector3 StartPosition;
            public Vector3 TargetPosition;
            public Quaternion StartRotation;
            public Quaternion TargetRotation;
            public float StartFieldOfView;
            public float TargetFieldOfView;
            public float Duration;
            public float Elapsed;

            public CameraTransitionSegment(
                Vector3 startPosition,
                Vector3 targetPosition,
                Quaternion startRotation,
                Quaternion targetRotation,
                float startFieldOfView,
                float targetFieldOfView,
                float duration)
            {
                StartPosition = startPosition;
                TargetPosition = targetPosition;
                StartRotation = startRotation;
                TargetRotation = targetRotation;
                StartFieldOfView = startFieldOfView;
                TargetFieldOfView = targetFieldOfView;
                Duration = Mathf.Max(0.01f, duration);
                Elapsed = 0f;
            }
        }

        public void Configure(Camera cameraTarget, Transform overview, Transform frontal, Transform whiteboard, Transform notebook, Transform bookView)
        {
            targetCamera = cameraTarget;
            overviewAnchor = overview;
            frontalAnchor = frontal;
            whiteboardAnchor = whiteboard;
            notebookAnchor = notebook;
            bookViewAnchor = bookView;
        }

        public void ConfigureViewSettings(
            float overviewFov,
            float frontalFov,
            float whiteboardFov,
            float notebookFov,
            float standardMoveDuration)
        {
            overviewFieldOfView = overviewFov;
            frontalDefaultFieldOfView = frontalFov;
            whiteboardFieldOfView = whiteboardFov;
            notebookFieldOfView = notebookFov;
            moveDuration = Mathf.Max(0.01f, standardMoveDuration);
        }

        public void ConfigureFrontalZoomSettings(
            float defaultDistance,
            float minDistance,
            float maxDistance,
            float scrollStep,
            float smoothDuration)
        {
            frontalDefaultZoomDistance = Mathf.Max(0.01f, defaultDistance);
            frontalMinZoomDistance = Mathf.Max(0.01f, minDistance);
            frontalMaxZoomDistance = Mathf.Max(frontalMinZoomDistance, maxDistance);
            frontalScrollZoomStep = Mathf.Max(0.01f, scrollStep);
            frontalZoomSmoothDuration = Mathf.Max(0.01f, smoothDuration);
            ResetFrontalZoomState();
        }

        public void ConfigureFrontalZoomInputPredicate(Func<bool> predicate)
        {
            frontalZoomInputPredicate = predicate;
        }

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            ResetFrontalZoomState();
        }

        private void Update()
        {
            if (!IsTransitioning &&
                CurrentMode == CameraMode.Frontal &&
                Mouse.current != null &&
                CanHandleFrontalZoomInput())
            {
                var scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.001f)
                {
                    var scrollSteps = NormalizeWheelScroll(scroll);
                    SetFrontalZoomTarget(frontalZoomTargetDistance - scrollSteps * frontalScrollZoomStep);
                }
            }
        }

        private void LateUpdate()
        {
            if (isTransitioning)
            {
                StepTransition(Time.deltaTime);
            }

            if (!isTransitioning && CurrentMode == CameraMode.Frontal)
            {
                StepFrontalZoom(Time.deltaTime);
            }
        }

        public void MoveToMode(CameraMode mode)
        {
            var previousMode = CurrentMode;
            var anchor = GetAnchor(mode);
            if (anchor == null)
            {
                return;
            }

            // Freeze the destination before UI/state events run. During the
            // movement, the camera never re-reads this anchor Transform.
            var targetPosition = anchor.position;
            var targetRotation = anchor.rotation;
            var targetFieldOfView = GetFieldOfView(mode);

            if (mode == CameraMode.Frontal)
            {
                ResetFrontalZoomState();
                ResetFrontalPanState();
            }

            CurrentMode = mode;
            ModeChanged?.Invoke(mode);

            if (previousMode == CameraMode.Notebook && mode == CameraMode.Overview)
            {
                StartNotebookReturnTransition(targetPosition, targetRotation, targetFieldOfView);
                return;
            }

            StartSingleSegmentTransition(targetPosition, targetRotation, targetFieldOfView, moveDuration);
        }

        public void MoveToWhiteboardSurface(Transform surfaceTransform)
        {
            if (surfaceTransform == null)
            {
                MoveToMode(CameraMode.Whiteboard);
                return;
            }

            if (!EnsureCameraResolved())
            {
                return;
            }

            var center = Vector3.zero;
            var collider = surfaceTransform.GetComponent<BoxCollider>();
            if (collider != null)
            {
                center = collider.center;
            }

            var targetPosition = surfaceTransform.TransformPoint(center + new Vector3(Mathf.Max(0.2f, whiteboardFocusDistance), 0f, 0f));
            var lookTarget = surfaceTransform.TransformPoint(center);
            var lookDirection = lookTarget - targetPosition;
            if (lookDirection.sqrMagnitude <= 0.0001f)
            {
                MoveToMode(CameraMode.Whiteboard);
                return;
            }

            CurrentMode = CameraMode.Whiteboard;
            ModeChanged?.Invoke(CameraMode.Whiteboard);
            StartSingleSegmentTransition(
                targetPosition,
                Quaternion.LookRotation(lookDirection.normalized, Vector3.up),
                whiteboardFieldOfView,
                moveDuration);
        }

        // MainView entry points -------------------------------------------------

        public void ApplyMainView()
        {
            if (!EnsureCameraResolved())
            {
                return;
            }

            if (!TryComputeMainViewPose(out var position, out var rotation, out var fieldOfView))
            {
                return;
            }

            targetCamera.transform.position = position;
            targetCamera.transform.rotation = rotation;
            targetCamera.fieldOfView = fieldOfView;

            CurrentMode = CameraMode.Overview;
            isTransitioning = false;
        }

        public bool TryGetMainViewPose(out Camera camera, out Vector3 position, out Quaternion rotation, out float fieldOfView)
        {
            camera = null;
            position = default;
            rotation = default;
            fieldOfView = mainViewFieldOfView;

            if (!EnsureCameraResolved())
            {
                return false;
            }

            if (!TryComputeMainViewPose(out position, out rotation, out fieldOfView))
            {
                return false;
            }

            camera = targetCamera;
            return camera != null;
        }

        public void MoveToMainView()
        {
            if (!EnsureCameraResolved())
            {
                return;
            }

            if (!TryComputeMainViewPose(out var position, out var rotation, out var fieldOfView))
            {
                return;
            }

            // The MainView shares CameraMode.Overview semantics (no scroll-zoom, treated
            // as the main-scene context by the manager) but the destination pose differs
            // from the overviewAnchor (which is the QuarterView). The ModeChanged event
            // is intentionally NOT raised so subscribers don't reset main-view-mode state.
            CurrentMode = CameraMode.Overview;
            StartSingleSegmentTransition(position, rotation, fieldOfView, moveDuration);
        }

        // QuarterView entry points (overviewAnchor pose, unchanged from existing scene
        // setup). Onboarding Step 3 needs an explicit snap to QuarterView before the
        // bookshelf-focus transition, so a public Apply method is exposed here.
        public void ApplyQuarterView()
        {
            if (!EnsureCameraResolved() || overviewAnchor == null)
            {
                return;
            }

            targetCamera.transform.position = overviewAnchor.position;
            targetCamera.transform.rotation = overviewAnchor.rotation;
            targetCamera.fieldOfView = overviewFieldOfView;

            CurrentMode = CameraMode.Overview;
            isTransitioning = false;
        }

        public void MoveToQuarterView()
        {
            // Existing behavior of MoveToMode(Overview) — preserved exactly so callers
            // like FocusQuarterView, CloseNotebookFocus, and the F1 shortcut keep landing
            // on overviewAnchor.
            MoveToMode(CameraMode.Overview);
        }

        // Backward-compatible alias retained so existing callers of the previous
        // experimental name still resolve to the renamed MainView method.
        public void MoveToWideMainView() => MoveToMainView();

        private bool EnsureCameraResolved()
        {
            if (targetCamera != null)
            {
                return true;
            }

            targetCamera = Camera.main;
            return targetCamera != null;
        }

        private bool TryComputeMainViewPose(out Vector3 position, out Quaternion rotation, out float fieldOfView)
        {
            position = default;
            rotation = default;
            fieldOfView = mainViewFieldOfView;

            var anchor = frontalAnchor != null ? frontalAnchor : overviewAnchor;
            if (anchor == null)
            {
                return false;
            }

            // Anchor a virtual look target in front of the bookshelf focus along its
            // forward axis. Orbiting the camera around this target keeps the bookshelf
            // roughly centered while the side angle reveals the laptop and surrounding
            // room.
            var lookTarget = anchor.position + anchor.forward * mainViewLookTargetDistance;
            var up = anchor.up;

            var totalDistance = mainViewLookTargetDistance + mainViewPullBack;
            var pulledOffset = -anchor.forward * totalDistance;
            var sideRotation = Quaternion.AngleAxis(mainViewSideAngleDegrees, up);
            var rotatedOffset = sideRotation * pulledOffset;

            position = lookTarget + rotatedOffset + up * mainViewVerticalOffset;
            rotation = Quaternion.LookRotation((lookTarget - position).normalized, up);
            return true;
        }

        // 무인자 오버로드 — 기존 호출자 호환. mode 미지정 시 ScreenAnchorBaked (vintage / 기본
        // procedural laptop) 로 진입. 새 호출자는 mode 를 명시.
        public void MoveToNotebookScreen(Transform screenTransform)
        {
            MoveToNotebookScreen(screenTransform, NotebookFocusCameraMode.ScreenAnchorBaked);
        }

        public void MoveToNotebookScreen(Transform screenTransform, NotebookFocusCameraMode mode)
        {
            if (screenTransform == null)
            {
                MoveToMode(CameraMode.Notebook);
                return;
            }

            if (!EnsureCameraResolved())
            {
                return;
            }

            // Snapshot the screen geometry before events/UI updates run — camera transition must never
            // re-read a Transform that may be reconfigured during fade.
            var screenCenterTransform = screenTransform.Find("ScreenCenter");
            var lookTarget = screenCenterTransform != null
                ? screenCenterTransform.position
                : screenTransform.TransformPoint(new Vector3(0f, 0.068f, 0.014f));

            Vector3 cameraPosition;
            if (mode == NotebookFocusCameraMode.ScreenForwardGeometry)
            {
                // DeepSea monitor 모드 — screen.forward × world distance. AlignNotebookScreenToMonitor
                // 가 Screen.rotation 을 monitor 정면과 align 해 두므로 screen.forward 가 display normal.
                // 단위 vector × distance 라 Screen.lossyScale 변화에 영향 안 받음.
                var distance = Mathf.Max(0.05f, notebookFocusDistance);
                cameraPosition = lookTarget + screenTransform.forward * distance;
            }
            else
            {
                // ScreenAnchorBaked (default / vintage / 기본 procedural laptop) — Screen 자식
                // CameraFocusPoint 의 prefab-baked offset 을 그대로 camera position 으로 사용.
                // laptop 슬랜트 기준이라 native laptop 화면 정면 framing 에 fit. CameraFocusPoint
                // 가 없으면 BookshelfGenerator 의 native offset (0, -0.58, 0.024) 로 fallback.
                var cameraFocusPoint = screenTransform.Find("CameraFocusPoint");
                cameraPosition = cameraFocusPoint != null
                    ? cameraFocusPoint.position
                    : screenTransform.TransformPoint(new Vector3(0f, -0.58f, 0.024f));
            }

            var lookDirection = lookTarget - cameraPosition;
            if (lookDirection.sqrMagnitude <= 0.0001f)
            {
                // degenerate (screen 기하학이 비정상) — 안전한 anchor fallback.
                MoveToMode(CameraMode.Notebook);
                return;
            }

            var cameraRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);

            CurrentMode = CameraMode.Notebook;
            ModeChanged?.Invoke(CameraMode.Notebook);
            // notebookTransitionDuration 으로 entry — 책장/대시보드 moveDuration (0.4s) 보다 살짝 길게 (기본 0.6s)
            // 해서 화면으로 끌려들어가는 부드러운 체감. SmoothStep easing 은 StepTransition 에서 공통 적용.
            StartSingleSegmentTransition(cameraPosition, cameraRotation, notebookFieldOfView, notebookTransitionDuration);
        }

        private Transform GetAnchor(CameraMode mode)
        {
            switch (mode)
            {
                case CameraMode.Overview:
                    return overviewAnchor;
                case CameraMode.Frontal:
                    return frontalAnchor;
                case CameraMode.Whiteboard:
                    return whiteboardAnchor;
                case CameraMode.Notebook:
                    return notebookAnchor;
                default:
                    return overviewAnchor;
            }
        }

        private float GetFieldOfView(CameraMode mode)
        {
            switch (mode)
            {
                case CameraMode.Overview:
                    return overviewFieldOfView;
                case CameraMode.Frontal:
                    return frontalDefaultFieldOfView;
                case CameraMode.Whiteboard:
                    return whiteboardFieldOfView;
                case CameraMode.Notebook:
                    return notebookFieldOfView;
                default:
                    return overviewFieldOfView;
            }
        }

        private void StartSingleSegmentTransition(Vector3 targetPosition, Quaternion targetRotation, float targetFov, float duration)
        {
            activeSegment = CreateSegmentFromCurrent(targetPosition, targetRotation, targetFov, duration);
            isTransitioning = true;
        }

        private float NormalizeWheelScroll(float scroll)
        {
            if (Mathf.Abs(scroll) > 10f && wheelScrollReference > 0f)
            {
                return scroll / wheelScrollReference;
            }

            return scroll;
        }

        private bool CanHandleFrontalZoomInput()
        {
            if (frontalZoomInputPredicate != null && !frontalZoomInputPredicate())
            {
                return false;
            }

            return EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
        }

        private void ResetFrontalZoomState()
        {
            frontalZoomCurrentDistance = ClampFrontalZoomDistance(frontalDefaultZoomDistance);
            frontalZoomTargetDistance = frontalZoomCurrentDistance;
            frontalZoomStartDistance = frontalZoomCurrentDistance;
            frontalZoomElapsed = 0f;
            isFrontalZooming = false;
        }

        private void ResetFrontalPanState()
        {
            frontalPanOffset = Vector2.zero;
        }

        public void PanFrontalCamera(Vector2 screenDelta)
        {
            if (CurrentMode != CameraMode.Frontal || frontalAnchor == null)
            {
                return;
            }

            frontalPanOffset.x = Mathf.Clamp(
                frontalPanOffset.x - screenDelta.x * frontalPanSensitivity,
                frontalMinPanX,
                frontalMaxPanX);

            var nextCameraY = Mathf.Clamp(
                frontalAnchor.position.y + frontalPanOffset.y - screenDelta.y * frontalPanSensitivity,
                frontalMinCameraY,
                frontalMaxCameraY);
            frontalPanOffset.y = nextCameraY - frontalAnchor.position.y;
        }

        private void SetFrontalZoomTarget(float distance)
        {
            var nextDistance = ClampFrontalZoomDistance(distance);
            if (Mathf.Abs(nextDistance - frontalZoomTargetDistance) <= 0.001f)
            {
                return;
            }

            frontalZoomStartDistance = frontalZoomCurrentDistance;
            frontalZoomTargetDistance = nextDistance;
            frontalZoomElapsed = 0f;
            isFrontalZooming = true;
        }

        private float ClampFrontalZoomDistance(float distance)
        {
            var minDistance = Mathf.Min(frontalMinZoomDistance, frontalMaxZoomDistance);
            var maxDistance = Mathf.Max(frontalMinZoomDistance, frontalMaxZoomDistance);
            return Mathf.Clamp(distance, minDistance, maxDistance);
        }

        private void StepFrontalZoom(float deltaTime)
        {
            if (targetCamera == null || frontalAnchor == null)
            {
                return;
            }

            if (isFrontalZooming)
            {
                frontalZoomElapsed += deltaTime;
                var normalizedTime = Mathf.Clamp01(frontalZoomElapsed / Mathf.Max(0.01f, frontalZoomSmoothDuration));
                var t = Mathf.SmoothStep(0f, 1f, normalizedTime);
                frontalZoomCurrentDistance = Mathf.Lerp(frontalZoomStartDistance, frontalZoomTargetDistance, t);

                if (normalizedTime >= 1f)
                {
                    frontalZoomCurrentDistance = frontalZoomTargetDistance;
                    isFrontalZooming = false;
                }
            }

            ApplyFrontalZoomPosition();
        }

        private void ApplyFrontalZoomPosition()
        {
            var anchorPosition = frontalAnchor.position;
            var focusZ = anchorPosition.z + ClampFrontalZoomDistance(frontalDefaultZoomDistance);
            targetCamera.transform.position = new Vector3(
                anchorPosition.x + frontalPanOffset.x,
                anchorPosition.y + frontalPanOffset.y,
                focusZ - frontalZoomCurrentDistance);
            targetCamera.transform.rotation = frontalAnchor.rotation;
            targetCamera.fieldOfView = frontalDefaultFieldOfView;
        }

        private void StartNotebookReturnTransition(Vector3 targetPosition, Quaternion targetRotation, float targetFov)
        {
            // Notebook → Overview 복귀도 entry 와 동일한 notebookTransitionDuration 사용 — 진입/복귀 체감
            // 속도 일관성.
            StartSingleSegmentTransition(targetPosition, targetRotation, targetFov, notebookTransitionDuration);
        }

        private CameraTransitionSegment CreateSegmentFromCurrent(Vector3 targetPosition, Quaternion targetRotation, float targetFov, float duration)
        {
            return new CameraTransitionSegment(
                targetCamera.transform.position,
                targetPosition,
                targetCamera.transform.rotation,
                targetRotation,
                targetCamera.fieldOfView,
                targetFov,
                duration);
        }

        private void StepTransition(float deltaTime)
        {
            activeSegment.Elapsed += deltaTime;
            var normalizedTime = Mathf.Clamp01(activeSegment.Elapsed / activeSegment.Duration);
            var t = Mathf.SmoothStep(0f, 1f, normalizedTime);

            targetCamera.transform.position = Vector3.Lerp(activeSegment.StartPosition, activeSegment.TargetPosition, t);
            targetCamera.transform.rotation = Quaternion.Slerp(activeSegment.StartRotation, activeSegment.TargetRotation, t);
            targetCamera.fieldOfView = Mathf.Lerp(activeSegment.StartFieldOfView, activeSegment.TargetFieldOfView, t);

            if (normalizedTime < 1f)
            {
                return;
            }

            targetCamera.transform.position = activeSegment.TargetPosition;
            targetCamera.transform.rotation = activeSegment.TargetRotation;
            targetCamera.fieldOfView = activeSegment.TargetFieldOfView;

            isTransitioning = false;
            TransitionCompleted?.Invoke(CurrentMode);
        }
    }
}
