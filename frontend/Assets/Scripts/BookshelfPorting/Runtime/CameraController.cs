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
