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
        [SerializeField] private float frontalPanZoomInSensitivityScale = 0.3f;
        [SerializeField] private float frontalMinPanX = -0.9f;
        [SerializeField] private float frontalMaxPanX = 0.9f;
        [SerializeField] private float frontalMinCameraY = 0.3f;
        [SerializeField] private float frontalMaxCameraY = 1.8f;

        // MainView/QuarterView (Overview) 마우스 드래그 orbit 회전. 카메라가 lookTarget 주위를
        // 도는 형태로 yaw/pitch 누적. 회전 한계로 방 뒷면이 보이지 않게 clamp. 모드 진입 시
        // (MoveToMainView/MoveToQuarterView/Apply*) offset 0 으로 reset 되며, 전환 종료 후
        // LateUpdate 에서 base pose 가 캡처돼 base pose 주변으로 orbit 한다.
        [SerializeField] private float mainViewRotateSensitivity = 0.03f;
        [SerializeField] private float mainViewMaxYawDegrees = 70f;
        [SerializeField] private float mainViewMaxPitchDegrees = 25f;
        [SerializeField] private float mainViewOrbitPivotDistance = 1.8f;

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
        private float mainViewYawOffset;
        private float mainViewPitchOffset;
        private Vector3 mainViewBasePosition;
        private Vector3 mainViewBasePivot;
        private float mainViewBaseFieldOfView;
        private bool mainViewBaseCaptured;
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

        // SerializeField default 값을 씬에 저장된 인스턴스가 override 하지 못하도록 installer
        // 에서 명시적으로 호출. ConfigureFrontalZoomSettings 와 동일 패턴.
        public void ConfigureMainViewRotateSettings(
            float sensitivity,
            float maxYawDegrees,
            float maxPitchDegrees,
            float pivotDistance)
        {
            mainViewRotateSensitivity = Mathf.Max(0.0001f, sensitivity);
            mainViewMaxYawDegrees = Mathf.Max(0f, maxYawDegrees);
            mainViewMaxPitchDegrees = Mathf.Max(0f, maxPitchDegrees);
            mainViewOrbitPivotDistance = Mathf.Max(0.1f, pivotDistance);
            ResetMainViewRotationOffset();
        }

        public void ConfigureFrontalPanSensitivity(float sensitivity)
        {
            frontalPanSensitivity = Mathf.Max(0.0001f, sensitivity);
        }

        public void ConfigureFrontalPanZoomInSensitivityScale(float scale)
        {
            frontalPanZoomInSensitivityScale = Mathf.Clamp(scale, 0.01f, 1f);
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

            if (!isTransitioning && CurrentMode == CameraMode.Overview)
            {
                ApplyMainViewRotation();
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

            if (mode == CameraMode.Overview)
            {
                ResetMainViewRotationOffset();
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
            ResetMainViewRotationOffset();
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
            ResetMainViewRotationOffset();
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
            ResetMainViewRotationOffset();
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
            // 사용자가 캡처한 정면뷰 기본 pose 하드코딩 (책장 우측 약간에서 좌측을 바라보는 각도).
            // 이전의 anchor 상대 계산 (lookTarget + pullback + sideAngle + verticalOffset) 은
            // 두 단계 toggle (MainView ↔ QuarterView) 구조에서 좌우 대칭 정면을 가정했으나,
            // QuarterView 폐지 후엔 단일 기본 pose 만 있으면 충분.
            position = new Vector3(0.5770924f, 1.116157f, -2.342854f);
            rotation = Quaternion.Euler(6.834f, -18.838f, 0f);
            fieldOfView = mainViewFieldOfView;

            // Camera 가 아직 attach 되지 않은 ultra-early 호출 (씬 셋업 전) 만 거른다.
            return targetCamera != null;
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
            if (frontalZoomInputPredicate != null)
            {
                return frontalZoomInputPredicate();
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

            // 줌아웃일수록 pan 범위 축소, 줌인일수록 확장 (최대 줌아웃에서 범위 0 → 카메라 고정).
            // FrontalZoomInAmount: 0 = max 줌아웃, 1 = max 줌인.
            var zoomScale = FrontalZoomInAmount;
            var anchorY = frontalAnchor.position.y;

            var clampMinX = frontalMinPanX * zoomScale;
            var clampMaxX = frontalMaxPanX * zoomScale;
            var clampMinY = (frontalMinCameraY - anchorY) * zoomScale;
            var clampMaxY = (frontalMaxCameraY - anchorY) * zoomScale;

            // 줌인할수록 sensitivity 도 함께 낮춰 fine control 강화. 줌아웃에서 1.0x, 최대
            // 줌인에서 frontalPanZoomInSensitivityScale(기본 0.3x). 줌아웃에선 range 자체가
            // 0 에 가까워서 sensitivity 가 큰 의미는 없지만, 중간 줌 영역에서 자연스러운 감속.
            var sensitivityScale = Mathf.Lerp(1f, frontalPanZoomInSensitivityScale, zoomScale);
            var effectiveSensitivity = frontalPanSensitivity * sensitivityScale;

            frontalPanOffset.x = Mathf.Clamp(
                frontalPanOffset.x - screenDelta.x * effectiveSensitivity,
                clampMinX,
                clampMaxX);

            frontalPanOffset.y = Mathf.Clamp(
                frontalPanOffset.y - screenDelta.y * effectiveSensitivity,
                clampMinY,
                clampMaxY);
        }

        // Overview (MainView/QuarterView) 마우스 드래그 orbit 회전. screenDelta 의 X 는 yaw, Y 는
        // pitch 에 누적되며 ±mainViewMaxYaw/Pitch 범위로 clamp 된다. 부호 convention:
        //   - drag right (delta.x > 0) → camera 가 오른쪽으로 orbit (yaw +)
        //   - drag up    (delta.y > 0) → camera 가 위쪽으로 orbit (pitch -, 카메라가 위로 들어올림)
        // base pose 는 ApplyMainViewRotation 진입 시점에 1회 캡처되며, 모드 전환 시
        // ResetMainViewRotationOffset 으로 무효화돼 다음 frame 에 fresh capture 된다.
        public void RotateMainViewCamera(Vector2 screenDelta)
        {
            if (CurrentMode != CameraMode.Overview || isTransitioning)
            {
                return;
            }

            mainViewYawOffset = Mathf.Clamp(
                mainViewYawOffset + screenDelta.x * mainViewRotateSensitivity,
                -mainViewMaxYawDegrees,
                mainViewMaxYawDegrees);

            mainViewPitchOffset = Mathf.Clamp(
                mainViewPitchOffset - screenDelta.y * mainViewRotateSensitivity,
                -mainViewMaxPitchDegrees,
                mainViewMaxPitchDegrees);
        }

        private void ResetMainViewRotationOffset()
        {
            mainViewYawOffset = 0f;
            mainViewPitchOffset = 0f;
            mainViewBaseCaptured = false;
        }

        private void ApplyMainViewRotation()
        {
            if (targetCamera == null)
            {
                return;
            }

            if (!mainViewBaseCaptured)
            {
                mainViewBasePosition = targetCamera.transform.position;
                var forward = targetCamera.transform.forward;
                mainViewBasePivot = mainViewBasePosition + forward * mainViewOrbitPivotDistance;
                mainViewBaseFieldOfView = targetCamera.fieldOfView;
                mainViewBaseCaptured = true;
            }

            var offsetFromPivot = mainViewBasePosition - mainViewBasePivot;
            var yawRot = Quaternion.AngleAxis(mainViewYawOffset, Vector3.up);
            var yawedRight = yawRot * Vector3.right;
            var pitchRot = Quaternion.AngleAxis(mainViewPitchOffset, yawedRight);

            var rotatedOffset = pitchRot * yawRot * offsetFromPivot;
            var finalPosition = mainViewBasePivot + rotatedOffset;
            var lookDir = mainViewBasePivot - finalPosition;
            if (lookDir.sqrMagnitude < 0.0001f)
            {
                return;
            }

            targetCamera.transform.position = finalPosition;
            targetCamera.transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            targetCamera.fieldOfView = mainViewBaseFieldOfView;
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

        // 책 viewing 진입 시 카메라가 너무 zoomed in 이면 책이 책장 뒷면을 뚫고 들어가는 회귀
        // 방지. 부드러운 zoom-out animation 으로 책 꺼내는 motion 과 자연스럽게 어울리게.
        // 책 viewPosition 은 TryGetFrontalCameraTargetPose 로 미래 카메라 pose 기준으로 계산해
        // animation 종료 시점에 책-카메라 상대 위치가 일관되도록 한다.
        public void EnsureFrontalZoomAtLeast(float minDistance)
        {
            if (CurrentMode != CameraMode.Frontal || frontalAnchor == null)
            {
                return;
            }

            var clampedMin = ClampFrontalZoomDistance(minDistance);
            if (frontalZoomTargetDistance < clampedMin)
            {
                SetFrontalZoomTarget(clampedMin);
            }
        }

        // 현재 zoom target 에 도달했을 때의 카메라 pose 예측. zoom animation 진행 중에도
        // 책 viewPosition 을 final pose 기준으로 계산하기 위해 사용. pan offset 도 target zoom
        // 의 zoom-scaled clamp 가 적용된 상태로 평가.
        public bool TryGetFrontalCameraTargetPose(out Vector3 cameraPosition, out float zoomInAmount)
        {
            cameraPosition = default;
            zoomInAmount = 0f;
            if (CurrentMode != CameraMode.Frontal || frontalAnchor == null)
            {
                return false;
            }

            var anchorPosition = frontalAnchor.position;
            var focusZ = anchorPosition.z + ClampFrontalZoomDistance(frontalDefaultZoomDistance);
            var clampedZoom = ClampFrontalZoomDistance(frontalZoomTargetDistance);

            zoomInAmount = Mathf.InverseLerp(
                Mathf.Max(frontalMinZoomDistance, frontalMaxZoomDistance),
                Mathf.Min(frontalMinZoomDistance, frontalMaxZoomDistance),
                clampedZoom);

            var anchorY = anchorPosition.y;
            var clampedPanX = Mathf.Clamp(frontalPanOffset.x,
                frontalMinPanX * zoomInAmount,
                frontalMaxPanX * zoomInAmount);
            var clampedPanY = Mathf.Clamp(frontalPanOffset.y,
                (frontalMinCameraY - anchorY) * zoomInAmount,
                (frontalMaxCameraY - anchorY) * zoomInAmount);

            cameraPosition = new Vector3(
                anchorPosition.x + clampedPanX,
                anchorPosition.y + clampedPanY,
                focusZ - clampedZoom);
            return true;
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

            // zoom 단계 변경(특히 줌아웃) 시 기존 pan offset 이 새 허용 범위를 벗어날 수 있어
            // 매 프레임 재-clamp. 줌아웃하면 자동으로 카메라가 중앙으로 끌려옴.
            var zoomScale = FrontalZoomInAmount;
            frontalPanOffset.x = Mathf.Clamp(
                frontalPanOffset.x,
                frontalMinPanX * zoomScale,
                frontalMaxPanX * zoomScale);
            frontalPanOffset.y = Mathf.Clamp(
                frontalPanOffset.y,
                (frontalMinCameraY - anchorPosition.y) * zoomScale,
                (frontalMaxCameraY - anchorPosition.y) * zoomScale);

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
