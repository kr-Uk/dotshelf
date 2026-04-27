using System;
using UnityEngine;
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
        [SerializeField] private float whiteboardFieldOfView = 56f;
        [SerializeField] private float notebookFieldOfView = 24f;
        [SerializeField] private float notebookTransitionDuration = 0.75f;
        [SerializeField] private float frontalDefaultFieldOfView = 60f;
        [SerializeField] private float frontalZoomSpeed = 4f;
        [SerializeField] private float frontalMinFov = 25f;
        [SerializeField] private float frontalMaxFov = 55f;

        // MainView: the default main-scene pose. Derived at runtime from frontalAnchor
        // by pulling straight back along its forward axis (same look direction as the
        // bookshelf focus) and slightly widening the FOV so the surrounding room
        // becomes visible without orbiting the camera. The bookshelf stays centered.
        // QuarterView remains the existing overviewAnchor pose, untouched.
        [SerializeField] private float mainViewPullBack = 0.5f;
        [SerializeField] private float mainViewVerticalOffset = 0.04f;
        [SerializeField] private float mainViewSideAngleDegrees = 0f;
        [SerializeField] private float mainViewLookTargetDistance = 1.2f;
        [SerializeField] private float mainViewFieldOfView = 62f;

        private CameraTransitionSegment activeSegment;
        private bool isTransitioning;
        public event Action<CameraMode> ModeChanged;
        public event Action<CameraMode> TransitionCompleted;

        public CameraMode CurrentMode { get; private set; } = CameraMode.Overview;
        public bool IsTransitioning => isTransitioning;
        public Transform BookViewAnchor => bookViewAnchor;

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

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (!IsTransitioning && CurrentMode == CameraMode.Frontal && Mouse.current != null)
            {
                var scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.001f)
                {
                    targetCamera.fieldOfView = Mathf.Clamp(
                        targetCamera.fieldOfView - scroll * Time.deltaTime * frontalZoomSpeed,
                        frontalMinFov,
                        frontalMaxFov);
                }
            }
        }

        private void LateUpdate()
        {
            if (isTransitioning)
            {
                StepTransition(Time.deltaTime);
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

            CurrentMode = mode;
            ModeChanged?.Invoke(mode);

            if (previousMode == CameraMode.Notebook && mode == CameraMode.Overview)
            {
                StartNotebookReturnTransition(targetPosition, targetRotation, targetFieldOfView);
                return;
            }

            StartSingleSegmentTransition(targetPosition, targetRotation, targetFieldOfView, moveDuration);
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

        public void MoveToNotebookScreen(Transform screenTransform)
        {
            if (screenTransform == null)
            {
                MoveToMode(CameraMode.Notebook);
                return;
            }

            // Snapshot the screen target before events/UI updates run, so the camera
            // transition never chases a Transform that may be reconfigured this frame.
            var screenCenterTransform = screenTransform.Find("ScreenCenter");
            var cameraFocusPoint = screenTransform.Find("CameraFocusPoint");
            var screenCenter = screenCenterTransform != null
                ? screenCenterTransform.position
                : screenTransform.TransformPoint(new Vector3(0f, 0.068f, 0.014f));
            var cameraPosition = cameraFocusPoint != null
                ? cameraFocusPoint.position
                : screenTransform.TransformPoint(new Vector3(0f, -0.58f, 0.024f));
            var cameraRotation = Quaternion.LookRotation((screenCenter - cameraPosition).normalized, Vector3.up);

            CurrentMode = CameraMode.Notebook;
            ModeChanged?.Invoke(CameraMode.Notebook);
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
                    return Mathf.Clamp(frontalDefaultFieldOfView, frontalMinFov, frontalMaxFov);
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

        private void StartNotebookReturnTransition(Vector3 targetPosition, Quaternion targetRotation, float targetFov)
        {
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
