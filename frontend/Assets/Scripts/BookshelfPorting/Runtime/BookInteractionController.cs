using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BookshelfPorting.Runtime
{
    public class BookInteractionController : MonoBehaviour
    {
        private const int DragVelocitySampleLimit = 5;
        private const float DragPreviewPitchDegrees = 15f;
        private const float DefaultBookHoverHintDelaySeconds = 0.7f;

        private enum InvalidDropBehavior
        {
            ReturnToOriginalSlot,
            PhysicsFloorDrop
        }

        [SerializeField] private Camera sceneCamera = null;
        [SerializeField] private BookshelfState state = null;
        [SerializeField] private BookshelfGenerator generator = null;
        [SerializeField] private CameraController cameraController = null;
        [SerializeField] private MaterialFactory materialFactory = null;
        [SerializeField] private BookshelfExperienceManager experienceManager = null;
        [SerializeField] private float viewMoveDuration = 0.25f;
        [SerializeField] private float dragStartThresholdPixels = 5f;
        [SerializeField] private float validDropDuration = 0.3f;
        [SerializeField] private float invalidReturnDuration = 0.35f;
        [SerializeField] private InvalidDropBehavior invalidDropBehavior = InvalidDropBehavior.ReturnToOriginalSlot;
        [SerializeField] private float invalidDropMaxDuration = 3f;
        [SerializeField] private float invalidDropBookMass = 0.18f;
        [SerializeField] private float invalidDropMinHorizontalSpeed = 0.45f;
        [SerializeField] private float invalidDropMaxHorizontalSpeed = 2.4f;
        [SerializeField] private float invalidDropThrowSpinMultiplier = 0.9f;
        [SerializeField] private float invalidDropBookBounciness = 0.05f;
        [SerializeField] private float invalidDropBoundaryBounciness = 0.18f;
        [SerializeField] private float invalidDropFriction = 0.4f;
        [SerializeField] private float roomFloorY = 0f;
        [SerializeField] private float roomLeftWallX = -2.01f;
        [SerializeField] private float roomRightWallX = 2.01f;
        [SerializeField] private float roomFrontWallZ = -2.05f;
        [SerializeField] private float roomBackWallZ = 1.41f;
        [SerializeField] private float roomPhysicsDepth = 4.2f;
        [SerializeField] private float roomWallHeight = 2.8f;
        [SerializeField] private float roomWallThickness = 0.08f;
        [SerializeField] private float dragWallSkin = 0.015f;
        [SerializeField] private float dragHeight = 1.0f;
        [SerializeField] private float viewRotationSpeed = 0.25f;
        [SerializeField] private float viewedBookScaleMultiplier = 4.8f;
        [SerializeField] private float viewedBookCameraDistance = 1.55f;
        [SerializeField] private float viewedBookZoomForwardOffset = 0.35f;
        [SerializeField] private Vector3 viewedBookInteractionPadding = new Vector3(0.28f, 0.22f, 0.18f);
        [SerializeField] private float edgeSwitchThresholdX = 0.95f;
        [SerializeField] private float edgeSwitchDwellSeconds = 1f;
        [SerializeField] private Color bookHoverColor = new Color(1f, 0.68f, 0.28f, 0.20f);
        [SerializeField] private Color sectionHoverColor = new Color(0.40f, 0.78f, 1f, 0.16f);
        [SerializeField] private float bookHoverScaleMultiplier = 1.12f;
        [SerializeField] private float bookHoverHintDelaySeconds = DefaultBookHoverHintDelaySeconds;
        [SerializeField] private float sectionHoverWidthInset = 0.025f;
        [SerializeField] private float sectionHoverHeightInset = 0.025f;

        private BookEntity hoveredBook;
        private BookEntity pressedBook;
        private BookEntity draggingBook;
        private GameObject ghostPreview;
        private GameObject bookHoverIndicator;
        private GameObject bookHoverHintObject;
        private Text bookHoverHintText;
        private RectTransform bookHoverHintPanelRect;
        private BookEntity pendingHoverHintBook;
        private float pendingHoverHintStartedAt;
        private GameObject sectionHoverIndicator;
        private Material bookHoverMaterial;
        private Material sectionHoverMaterial;
        private Transform dropPhysicsRoot;
        private PhysicsMaterial dropBookMaterial;
        private PhysicsMaterial dropBoundaryMaterial;
        private ShelfSection hoveredSection;
        private ShelfSection pressedSection;
        private ShelfSection originalSectionBeforeDrag;
        private ShelfSection activeDragPreviewSection;
        private int originalIndexBeforeDrag = -1;
        private int activeDragPreviewInsertIndex = -1;
        private Vector2 lastPointer;
        private float shelfBookPointerTravel;
        private Vector3 lastDragWorldPosition;
        private Vector3 dragWorldVelocity;
        private Canvas runtimeCanvas;
        private Button backButton;
        private BookInteractionHitArea activeViewedHitArea;
        private Transform dragLayerRoot;
        private bool isManipulatingViewedBook;
        private bool viewedBookPressStartedOnBook;
        private bool isTrackingViewedBookPointer;
        private bool isPanningFrontalCamera;
        private float viewedBookPointerTravel;
        private int edgeSwitchDirection;
        private float edgeSwitchElapsed;
        private bool pendingDropAfterBookshelfSwitch;
        private Vector2 pendingDropPointer;
        private PointerTarget currentPointerTarget;
        private readonly List<BoxCollider> dropBoundaryColliders = new List<BoxCollider>();
        private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
        private readonly Queue<Vector3> dragVelocitySamples = new Queue<Vector3>();

        public void Configure(
            BookshelfState bookshelfState,
            BookshelfGenerator bookshelfGenerator,
            CameraController controller,
            MaterialFactory factory,
            Camera targetCamera,
            BookshelfExperienceManager manager)
        {
            state = bookshelfState;
            generator = bookshelfGenerator;
            cameraController = controller;
            materialFactory = factory;
            sceneCamera = targetCamera;
            experienceManager = manager;
        }

        private void Awake()
        {
            if (sceneCamera == null)
            {
                sceneCamera = Camera.main;
            }

            EnsureRuntimeUI();
        }

        private void Update()
        {
            HandleKeyboardShortcuts();

            var mouse = Mouse.current;
            if (mouse == null || state == null)
            {
                return;
            }

            if (backButton != null)
            {
                var useOverlayBackButton = experienceManager != null && experienceManager.CurrentFocusArea == ExperienceFocusArea.Bookshelf;
                backButton.gameObject.SetActive(state != null && state.IsViewingBook && !useOverlayBackButton);
            }

            var pointer = mouse.position.ReadValue();
            currentPointerTarget = RaycastPointerTarget(pointer);

            if (draggingBook != null && generator != null && generator.IsBookshelfSwitching)
            {
                if (mouse.leftButton.wasReleasedThisFrame)
                {
                    pendingDropAfterBookshelfSwitch = true;
                    pendingDropPointer = pointer;
                }

                hoveredBook = null;
                hoveredSection = null;
                HideHoverIndicators();
                return;
            }

            if (pendingDropAfterBookshelfSwitch && draggingBook != null)
            {
                EndDrag(pendingDropPointer);
                pressedBook = null;
                pressedSection = null;
                return;
            }

            if (!CanViewShelfBooks())
            {
                hoveredBook = null;
                hoveredSection = null;
                pressedBook = null;
                pressedSection = null;

                if (draggingBook != null && (generator == null || !generator.IsBookshelfSwitching))
                {
                    CancelDrag();
                }
            }
            else
            {
                hoveredBook = currentPointerTarget.Book;
                hoveredSection = currentPointerTarget.Book == null ? currentPointerTarget.Section : null;
            }

            if (state.IsViewingBook)
            {
                hoveredBook = IsPointerOverActiveViewedBook(currentPointerTarget) ? currentPointerTarget.Book : null;
                hoveredSection = null;
            }

            UpdateHoverIndicators(pointer);

            if (mouse.leftButton.wasPressedThisFrame)
            {
                OnPointerDown(pointer);
            }

            if (mouse.leftButton.isPressed)
            {
                OnPointerDrag(pointer);
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                OnPointerUp(pointer);
            }
        }

        public void CloseViewedBook(bool reapplyLayout)
        {
            if (!state.IsViewingBook || state.ActiveViewedBook == null)
            {
                return;
            }

            ResetBookHoverHint();
            var book = state.ActiveViewedBook;
            state.IsViewingBook = false;
            state.ActiveViewedBook = null;
            isManipulatingViewedBook = false;
            viewedBookPressStartedOnBook = false;
            experienceManager?.HandleBookClosed();

            if (activeViewedHitArea != null)
            {
                activeViewedHitArea.SetActive(false);
                activeViewedHitArea = null;
            }

            if (reapplyLayout && book.CurrentSection != null)
            {
                state.LayoutSection(book.CurrentSection, this, true);
            }
            else
            {
                StartCoroutine(AnimateWorldMove(book.transform, book.StoredPosition, book.StoredRotation, book.StoredScale));
            }
        }

        public void ViewBookFromUi(BookEntity book)
        {
            if (book == null)
            {
                return;
            }

            if (!IsBookshelfFocusActive())
            {
                experienceManager?.FocusBookshelf();
                return;
            }

            if (state != null && state.IsViewingBook && state.ActiveViewedBook != book)
            {
                CloseViewedBook(true);
            }

            if (state != null && state.IsViewingBook && state.ActiveViewedBook == book)
            {
                experienceManager?.ToggleFocusedBookDetail(book);
                return;
            }

            ViewBook(book);
        }

        private void HandleKeyboardShortcuts()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            if (state.IsViewingBook &&
                (Keyboard.current.f1Key.wasPressedThisFrame || Keyboard.current.f3Key.wasPressedThisFrame))
            {
                CloseViewedBook(true);
            }

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (state.IsViewingBook)
                {
                    CloseViewedBook(true);
                }
            }

        }

        private void OnPointerDown(Vector2 pointer)
        {
            if (state == null)
            {
                pressedBook = null;
                pressedSection = null;
                isManipulatingViewedBook = false;
                isTrackingViewedBookPointer = false;
                isPanningFrontalCamera = false;
                return;
            }

            if (!CanViewShelfBooks() && !state.IsViewingBook)
            {
                pressedBook = null;
                pressedSection = null;
                isPanningFrontalCamera = false;
                return;
            }

            currentPointerTarget = RaycastPointerTarget(pointer);

            if (IsPointerOverUi(pointer))
            {
                pressedBook = null;
                pressedSection = null;
                isManipulatingViewedBook = false;
                isTrackingViewedBookPointer = false;
                isPanningFrontalCamera = false;
                return;
            }

            if (IsBookshelfWorldInputBlocked())
            {
                pressedBook = null;
                pressedSection = null;
                isManipulatingViewedBook = false;
                isTrackingViewedBookPointer = false;
                isPanningFrontalCamera = false;
                return;
            }

            if (state.IsViewingBook)
            {
                lastPointer = pointer;
                pressedBook = state.ActiveViewedBook;
                viewedBookPressStartedOnBook = IsPointerOverActiveViewedBook(currentPointerTarget);
                isTrackingViewedBookPointer = true;
                isManipulatingViewedBook = viewedBookPressStartedOnBook;
                viewedBookPointerTravel = 0f;
                return;
            }

            lastPointer = pointer;
            shelfBookPointerTravel = 0f;
            pressedBook = currentPointerTarget.Book;
            pressedSection = currentPointerTarget.Section;
            isPanningFrontalCamera = false;
        }

        private void OnPointerDrag(Vector2 pointer)
        {
            var delta = pointer - lastPointer;
            lastPointer = pointer;

            if (state.IsViewingBook && state.ActiveViewedBook != null)
            {
                if (!isTrackingViewedBookPointer)
                {
                    return;
                }

                viewedBookPointerTravel += delta.magnitude;
                if (isManipulatingViewedBook)
                {
                    state.ActiveViewedBook.AddViewRotation(delta, viewRotationSpeed);
                }

                return;
            }

            if (pressedBook == null)
            {
                if (pressedSection != null && !state.IsViewingBook)
                {
                    shelfBookPointerTravel += delta.magnitude;
                }

                if (CanPanFrontalCamera(pointer) &&
                    (pressedSection == null || !IsBookshelfEditMode() || shelfBookPointerTravel > dragStartThresholdPixels))
                {
                    isPanningFrontalCamera = true;
                    cameraController.PanFrontalCamera(delta);
                    HideHoverIndicators();
                }

                return;
            }

            if (!CanInteractWithShelfBooks())
            {
                return;
            }

            shelfBookPointerTravel += delta.magnitude;
            if (draggingBook == null && shelfBookPointerTravel > dragStartThresholdPixels)
            {
                BeginDrag(pressedBook);
            }

            if (draggingBook != null)
            {
                ContinueDrag(pointer);
            }
        }

        private void OnPointerUp(Vector2 pointer)
        {
            if (state == null)
            {
                pressedBook = null;
                pressedSection = null;
                isManipulatingViewedBook = false;
                isTrackingViewedBookPointer = false;
                isPanningFrontalCamera = false;
                return;
            }

            currentPointerTarget = RaycastPointerTarget(pointer);

            if (draggingBook != null)
            {
                if (generator != null && generator.IsBookshelfSwitching)
                {
                    pendingDropAfterBookshelfSwitch = true;
                    pendingDropPointer = pointer;
                    return;
                }

                EndDrag(pointer);
            }
            else if (state.IsViewingBook)
            {
                if (IsPointerOverUi(pointer))
                {
                    isManipulatingViewedBook = false;
                    viewedBookPressStartedOnBook = false;
                    isTrackingViewedBookPointer = false;
                    viewedBookPointerTravel = 0f;
                    return;
                }

                var pointerOverActiveBook = IsPointerOverActiveViewedBook(currentPointerTarget);
                if (viewedBookPointerTravel <= 4f)
                {
                    if (viewedBookPressStartedOnBook || pointerOverActiveBook)
                    {
                        experienceManager?.ToggleFocusedBookDetail(state.ActiveViewedBook);
                    }
                    else
                    {
                        CloseViewedBook(true);
                    }
                }

                isManipulatingViewedBook = false;
                viewedBookPressStartedOnBook = false;
                isTrackingViewedBookPointer = false;
                viewedBookPointerTravel = 0f;
            }
            else if (isPanningFrontalCamera)
            {
                isPanningFrontalCamera = false;
            }
            else if (pressedBook != null &&
                     !state.IsViewingBook &&
                     (hoveredBook == pressedBook || currentPointerTarget.Book == pressedBook))
            {
                if (IsBookshelfEditMode())
                {
                    experienceManager?.OpenBookEditPanel(pressedBook);
                }
                else
                {
                    if (experienceManager != null &&
                        experienceManager.CurrentFocusArea != ExperienceFocusArea.Bookshelf)
                    {
                        experienceManager.FocusBookshelf();
                    }
                    else
                    {
                        ViewBook(pressedBook);
                    }
                }
            }
            else if (IsBookshelfEditMode() &&
                     pressedBook == null &&
                     pressedSection != null &&
                     currentPointerTarget.Section == pressedSection &&
                     shelfBookPointerTravel <= dragStartThresholdPixels)
            {
                experienceManager?.OpenSectionAlignmentPanel(pressedSection);
            }

            pressedBook = null;
            pressedSection = null;
            shelfBookPointerTravel = 0f;
        }

        private bool CanPanFrontalCamera(Vector2 pointer)
        {
            return state != null &&
                   IsBookshelfFocusActive() &&
                   cameraController != null &&
                   cameraController.CurrentMode == CameraMode.Frontal &&
                   !cameraController.IsTransitioning &&
                   generator != null &&
                   !generator.IsBookshelfSwitching &&
                   !state.IsViewingBook &&
                   state.ActiveDraggedBook == null &&
                   !IsBookshelfWorldInputBlocked() &&
                   !IsBookAddPlacementSelectionActive() &&
                   !IsPointerOverUi(pointer);
        }

        private bool CanInteractWithShelfBooks()
        {
            return state != null &&
                   IsBookshelfFocusActive() &&
                   sceneCamera != null &&
                   cameraController != null &&
                   cameraController.CurrentMode == CameraMode.Frontal &&
                   generator != null &&
                   !generator.IsBookshelfSwitching &&
                   !IsBookshelfWorldInputBlocked() &&
                   !IsBookAddPlacementSelectionActive();
        }

        private bool IsBookshelfEditMode()
        {
            return experienceManager != null && experienceManager.IsBookshelfEditMode;
        }

        private bool IsBookAddPlacementSelectionActive()
        {
            return experienceManager != null && experienceManager.IsBookAddPlacementSelectionActive;
        }

        private bool IsBookshelfWorldInputBlocked()
        {
            return experienceManager != null && experienceManager.IsBookshelfWorldInputBlocked;
        }

        private bool IsBookshelfFocusActive()
        {
            if (experienceManager != null && experienceManager.CurrentFocusArea != ExperienceFocusArea.Bookshelf)
            {
                return false;
            }

            return cameraController != null &&
                   cameraController.CurrentMode == CameraMode.Frontal &&
                   !cameraController.IsTransitioning;
        }

        private bool CanViewShelfBooks()
        {
            return state != null &&
                   IsBookshelfFocusActive() &&
                   sceneCamera != null &&
                   cameraController != null &&
                   cameraController.CurrentMode == CameraMode.Frontal &&
                   generator != null &&
                   !generator.IsBookshelfSwitching &&
                   !IsBookshelfWorldInputBlocked() &&
                   !IsBookAddPlacementSelectionActive();
        }

        private void CancelDrag()
        {
            var book = draggingBook;
            if (book == null)
            {
                return;
            }

            ResetDragLayoutPreview(true);

            var restored = false;
            if (state != null && originalSectionBeforeDrag != null)
            {
                restored = state.TryPlaceBookAt(book, originalSectionBeforeDrag, originalIndexBeforeDrag, this, true);
            }

            if (!restored)
            {
                restored = TryPlaceBookInNearestAvailableSection(book, true);
            }

            if (!restored &&
                TryForcePlaceBookForInvalidReturn(
                    book,
                    originalSectionBeforeDrag,
                    originalIndexBeforeDrag,
                    out var targetLocalPosition,
                    out var targetLocalRotation,
                    out var targetLocalScale))
            {
                if (generator != null && generator.BooksRoot != null && book.transform.parent != generator.BooksRoot)
                {
                    book.transform.SetParent(generator.BooksRoot, true);
                }

                book.transform.localPosition = targetLocalPosition;
                book.transform.localRotation = targetLocalRotation;
                book.transform.localScale = targetLocalScale;

                if (state != null && book.CurrentSection != null)
                {
                    state.LayoutSectionExcept(book.CurrentSection, this, true, book);
                }
            }

            if (ghostPreview != null)
            {
                ghostPreview.SetActive(false);
            }

            draggingBook = null;
            state.ActiveDraggedBook = null;
            originalSectionBeforeDrag = null;
            originalIndexBeforeDrag = -1;
            pendingDropAfterBookshelfSwitch = false;
            shelfBookPointerTravel = 0f;
            ResetEdgeSwitchDwell();
            ResetDragVelocity();
        }

        private bool TryPlaceBookInNearestAvailableSection(BookEntity book, bool animate)
        {
            if (state == null ||
                book == null ||
                generator == null ||
                generator.BooksRoot == null ||
                generator.BooksRoot.parent == null)
            {
                return false;
            }

            var localPosition = generator.BooksRoot.parent.InverseTransformPoint(book.transform.position);
            var fallbackSection = state.FindNearestAvailableSection(localPosition, book);
            return fallbackSection != null &&
                   state.TryPlaceBookAt(book, fallbackSection, fallbackSection.books.Count, this, animate);
        }

        private PointerTarget RaycastPointerTarget(Vector2 pointer)
        {
            if (sceneCamera == null)
            {
                sceneCamera = Camera.main;
            }

            if (sceneCamera == null)
            {
                return PointerTarget.None;
            }

            var ray = sceneCamera.ScreenPointToRay(pointer);
            var hits = Physics.RaycastAll(ray, 100f);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                var hitArea = hit.collider.GetComponent<BookInteractionHitArea>();
                if (hitArea != null)
                {
                    continue;
                }

                var book = hit.collider.GetComponentInParent<BookEntity>();
                if (book != null)
                {
                    return new PointerTarget(PointerTargetType.Book, book, book.CurrentSection);
                }
            }

            for (var i = 0; i < hits.Length; i++)
            {
                var hitArea = hits[i].collider.GetComponent<BookInteractionHitArea>();
                if (hitArea != null && hitArea.Owner != null)
                {
                    return new PointerTarget(PointerTargetType.ViewedBookInteractionArea, hitArea.Owner, hitArea.Owner.CurrentSection);
                }
            }

            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (TryResolveSectionFromHit(hit, out var section))
                {
                    return new PointerTarget(PointerTargetType.Section, null, section);
                }
            }

            return PointerTarget.None;
        }

        private bool TryResolveSectionFromHit(RaycastHit hit, out ShelfSection section)
        {
            section = null;
            if (state == null || generator == null || generator.BooksRoot == null || generator.BooksRoot.parent == null)
            {
                return false;
            }

            var local = generator.BooksRoot.parent.InverseTransformPoint(hit.point);
            section = state.FindSectionAt(local, 0.01f);
            return section != null;
        }

        private void UpdateHoverIndicators(Vector2 pointer)
        {
            if (state == null ||
                draggingBook != null ||
                (state.ActiveDraggedBook != null && state.ActiveDraggedBook != draggingBook) ||
                IsPointerOverUi(pointer))
            {
                HideHoverIndicators();
                return;
            }

            var canShowShelfHover = CanViewShelfBooks() && !state.IsViewingBook;
            if (!canShowShelfHover)
            {
                HideHoverIndicators();
                return;
            }

            if (hoveredBook != null)
            {
                ShowBookHoverIndicator(hoveredBook);
                HideSectionHoverIndicator();
                return;
            }

            HideBookHoverIndicator();
            if (IsBookshelfEditMode() && hoveredSection != null)
            {
                ShowSectionHoverIndicator(hoveredSection);
            }
            else
            {
                HideSectionHoverIndicator();
            }
        }

        private void ShowBookHoverIndicator(BookEntity book)
        {
            if (book == null)
            {
                HideBookHoverIndicator();
                return;
            }

            EnsureBookHoverIndicator();
            if (bookHoverIndicator == null)
            {
                return;
            }

            bookHoverIndicator.transform.SetParent(book.transform, false);
            bookHoverIndicator.transform.localPosition = ResolveBookHoverCenter(book);
            bookHoverIndicator.transform.localRotation = Quaternion.identity;
            bookHoverIndicator.transform.localScale = ResolveBookHoverSize(book);
            bookHoverIndicator.SetActive(true);
            UpdateBookHoverHint(book);
        }

        private void UpdateBookHoverHint(BookEntity book)
        {
            if (book == null || draggingBook != null)
            {
                ResetBookHoverHint();
                return;
            }

            if (pendingHoverHintBook != book)
            {
                pendingHoverHintBook = book;
                pendingHoverHintStartedAt = Time.unscaledTime;
                HideBookHoverHint();
                return;
            }

            var hintDelay = Mathf.Max(0.1f, bookHoverHintDelaySeconds);
            if (Time.unscaledTime - pendingHoverHintStartedAt < hintDelay)
            {
                return;
            }

            ShowBookHoverHint(book);
        }

        private void ShowBookHoverHint(BookEntity book)
        {
            if (book == null || sceneCamera == null)
            {
                HideBookHoverHint();
                return;
            }

            EnsureBookHoverHint();
            if (bookHoverHintObject == null || bookHoverHintText == null || bookHoverHintPanelRect == null)
            {
                return;
            }

            var title = string.IsNullOrWhiteSpace(book.title) ? book.bookId : book.title;
            var author = string.IsNullOrWhiteSpace(book.author) ? "저자 미상" : book.author;
            bookHoverHintText.text = $"{title}\n{author} · {GetReadingStatusLabel(book.readingStatus)}";

            var worldPosition = book.transform.position + Vector3.up * Mathf.Max(0.16f, book.height * 1.08f);
            var screenPoint = sceneCamera.WorldToScreenPoint(worldPosition);
            if (screenPoint.z <= 0f)
            {
                HideBookHoverHint();
                return;
            }

            var size = bookHoverHintPanelRect.sizeDelta;
            var x = Mathf.Clamp(screenPoint.x, size.x * 0.5f + 16f, Screen.width - size.x * 0.5f - 16f);
            var y = Mathf.Clamp(screenPoint.y + 28f, size.y * 0.5f + 16f, Screen.height - size.y * 0.5f - 16f);
            bookHoverHintPanelRect.position = new Vector3(x, y, 0f);
            bookHoverHintObject.SetActive(true);
        }

        private Vector3 ResolveBookHoverCenter(BookEntity book)
        {
            var box = book != null ? book.GetComponent<BoxCollider>() : null;
            return box != null ? box.center : Vector3.zero;
        }

        private Vector3 ResolveBookHoverSize(BookEntity book)
        {
            var box = book != null ? book.GetComponent<BoxCollider>() : null;
            var size = box != null ? box.size : Vector3.one;
            return AbsVector(size) * Mathf.Max(1f, bookHoverScaleMultiplier);
        }

        private void ShowSectionHoverIndicator(ShelfSection section)
        {
            if (section == null || generator == null || generator.BooksRoot == null || generator.BooksRoot.parent == null)
            {
                HideSectionHoverIndicator();
                return;
            }

            EnsureSectionHoverIndicator();
            if (sectionHoverIndicator == null)
            {
                return;
            }

            var parent = generator.BooksRoot.parent;
            var width = Mathf.Max(0.02f, section.Width - sectionHoverWidthInset * 2f);
            var height = Mathf.Max(0.02f, section.sectionHeight - sectionHoverHeightInset * 2f);
            var centerX = (section.leftBound + section.rightBound) * 0.5f;
            var centerY = section.shelfTopY + section.sectionHeight * 0.5f;
            var frontZ = section.frontBoundZ - 0.012f;

            sectionHoverIndicator.transform.SetParent(parent, false);
            sectionHoverIndicator.transform.localPosition = new Vector3(centerX, centerY, frontZ);
            sectionHoverIndicator.transform.localRotation = Quaternion.identity;
            sectionHoverIndicator.transform.localScale = new Vector3(width, height, 0.006f);
            sectionHoverIndicator.SetActive(true);
        }

        private void HideHoverIndicators()
        {
            HideBookHoverIndicator();
            HideSectionHoverIndicator();
        }

        private void HideBookHoverIndicator()
        {
            if (bookHoverIndicator != null)
            {
                bookHoverIndicator.SetActive(false);
            }

            ResetBookHoverHint();
        }

        private void ResetBookHoverHint()
        {
            pendingHoverHintBook = null;
            pendingHoverHintStartedAt = 0f;
            HideBookHoverHint();
        }

        private void HideBookHoverHint()
        {
            if (bookHoverHintObject != null)
            {
                bookHoverHintObject.SetActive(false);
            }
        }

        private void HideSectionHoverIndicator()
        {
            if (sectionHoverIndicator != null)
            {
                sectionHoverIndicator.SetActive(false);
            }
        }

        private void EnsureBookHoverIndicator()
        {
            if (bookHoverIndicator != null)
            {
                return;
            }

            bookHoverIndicator = CreateHoverIndicator("BookHoverIndicator", GetBookHoverMaterial());
        }

        private void EnsureSectionHoverIndicator()
        {
            if (sectionHoverIndicator != null)
            {
                return;
            }

            sectionHoverIndicator = CreateHoverIndicator("SectionHoverIndicator", GetSectionHoverMaterial());
        }

        private void EnsureBookHoverHint()
        {
            if (bookHoverHintObject != null)
            {
                return;
            }

            bookHoverHintObject = new GameObject("BookHoverHintCanvas");
            var canvas = bookHoverHintObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 512;
            var scaler = bookHoverHintObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 1000f);
            scaler.matchWidthOrHeight = 0.5f;

            var canvasRect = bookHoverHintObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;

            var panelObject = new GameObject("HintPanel", typeof(RectTransform));
            panelObject.transform.SetParent(bookHoverHintObject.transform, false);
            bookHoverHintPanelRect = panelObject.GetComponent<RectTransform>();
            bookHoverHintPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
            bookHoverHintPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
            bookHoverHintPanelRect.pivot = new Vector2(0.5f, 0.5f);
            bookHoverHintPanelRect.sizeDelta = new Vector2(300f, 96f);
            bookHoverHintPanelRect.localScale = Vector3.one;

            var backgroundObject = new GameObject("Background");
            backgroundObject.transform.SetParent(panelObject.transform, false);
            var background = backgroundObject.AddComponent<Image>();
            background.color = new Color32(255, 248, 236, 238);
            background.raycastTarget = false;
            var backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            var textObject = new GameObject("Text");
            textObject.transform.SetParent(panelObject.transform, false);
            bookHoverHintText = textObject.AddComponent<Text>();
            bookHoverHintText.font = RuntimeUiFont.Resolve();
            bookHoverHintText.fontSize = 22;
            bookHoverHintText.fontStyle = RuntimeUiFont.ResolveStyle(bookHoverHintText.font, FontStyle.Bold);
            bookHoverHintText.color = new Color32(49, 42, 36, 255);
            bookHoverHintText.alignment = TextAnchor.MiddleLeft;
            bookHoverHintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookHoverHintText.verticalOverflow = VerticalWrapMode.Truncate;
            bookHoverHintText.raycastTarget = false;

            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18f, 10f);
            textRect.offsetMax = new Vector2(-18f, -10f);

            bookHoverHintObject.SetActive(false);
        }

        private static string GetReadingStatusLabel(App.Domain.ReadingStatus status)
        {
            switch (status)
            {
                case App.Domain.ReadingStatus.Read:
                    return "읽음";
                case App.Domain.ReadingStatus.Reading:
                    return "읽는 중";
                default:
                    return "안 읽음";
            }
        }

        private static GameObject CreateHoverIndicator(string name, Material material)
        {
            var indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.name = name;
            var collider = indicator.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = indicator.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            indicator.SetActive(false);
            return indicator;
        }

        private Material GetBookHoverMaterial()
        {
            if (bookHoverMaterial == null)
            {
                bookHoverMaterial = CreateHoverMaterial("BookHover", bookHoverColor);
            }

            return bookHoverMaterial;
        }

        private Material GetSectionHoverMaterial()
        {
            if (sectionHoverMaterial == null)
            {
                sectionHoverMaterial = CreateHoverMaterial("SectionHover", sectionHoverColor);
            }

            return sectionHoverMaterial;
        }

        private static Material CreateHoverMaterial(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.color = color;
            material.SetFloat("_Smoothness", 0.2f);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return material;
        }

        private static Vector3 AbsVector(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private void ViewBook(BookEntity book)
        {
            if (book == null || state == null || cameraController == null || cameraController.BookViewAnchor == null)
            {
                return;
            }

            if (!IsBookshelfFocusActive())
            {
                experienceManager?.FocusBookshelf();
                return;
            }

            ResetBookHoverHint();
            state.IsViewingBook = true;
            state.ActiveViewedBook = book;
            experienceManager?.HandleBookViewed(book);
            SetDynamicHierarchy(book.transform);
            book.CaptureCurrentTransform();
            var viewRotation = cameraController.BookViewAnchor.rotation * BookFootprintUtility.GetVisualRotation(BookOrientation.Front);
            book.BeginViewRotation(viewRotation);
            EnsureViewedBookHitArea(book);
            activeViewedHitArea.SetActive(true);
            StartCoroutine(AnimateWorldMove(
                book.transform,
                ResolveBookViewPosition(),
                viewRotation,
                book.StoredScale * viewedBookScaleMultiplier));
        }

        private Vector3 ResolveBookViewPosition()
        {
            var fallbackPosition = cameraController.BookViewAnchor.position;
            if (sceneCamera == null)
            {
                return fallbackPosition;
            }

            var zoomInAmount = cameraController.CurrentMode == CameraMode.Frontal
                ? cameraController.FrontalZoomInAmount
                : 0f;
            var cameraDistance = Mathf.Max(
                0.7f,
                viewedBookCameraDistance - Mathf.Max(0f, viewedBookZoomForwardOffset) * zoomInAmount);
            return sceneCamera.transform.position + sceneCamera.transform.forward * cameraDistance;
        }

        private static void SetDynamicHierarchy(Transform root)
        {
            if (root == null)
            {
                return;
            }

            root.gameObject.isStatic = false;
            for (var i = 0; i < root.childCount; i++)
            {
                SetDynamicHierarchy(root.GetChild(i));
            }
        }

        private void BeginDrag(BookEntity book)
        {
            ResetBookHoverHint();
            DisableDropPhysics(book);
            if (generator != null && generator.BooksRoot != null && book.transform.parent != generator.BooksRoot)
            {
                book.transform.SetParent(generator.BooksRoot, true);
            }

            draggingBook = book;
            state.ActiveDraggedBook = book;
            originalSectionBeforeDrag = book.CurrentSection;
            originalIndexBeforeDrag = originalSectionBeforeDrag != null ? originalSectionBeforeDrag.books.IndexOf(book) : -1;
            pendingDropAfterBookshelfSwitch = false;
            lastDragWorldPosition = book.transform.position;
            ClearDragLayoutPreview();
            ResetEdgeSwitchDwell();
            ResetDragVelocity();

            EnsureGhostPreview(book);
        }

        private void ContinueDrag(Vector2 pointer)
        {
            var plane = new Plane(Vector3.forward, new Vector3(0f, dragHeight, generator.BooksRoot.position.z + 0.12f));
            var ray = sceneCamera.ScreenPointToRay(pointer);
            if (plane.Raycast(ray, out var enter))
            {
                var world = ClampDraggedBookToRoom(ray.GetPoint(enter), draggingBook);
                if (Time.deltaTime > 0f)
                {
                    TrackDragVelocity((world - lastDragWorldPosition) / Time.deltaTime);
                }

                lastDragWorldPosition = world;
                draggingBook.transform.position = world;
                draggingBook.transform.rotation = Quaternion.Euler(DragPreviewPitchDegrees, 0f, 0f);
                UpdateEdgeSwitchDwell(draggingBook);

                var local = generator.BooksRoot.parent.InverseTransformPoint(world);
                var dropSection = state.FindAvailableDropSection(local, draggingBook);
                var insertIndex = dropSection != null ? state.ComputeDropInsertIndex(local, dropSection, draggingBook) : 0;
                UpdateDragLayoutPreview(dropSection, draggingBook, insertIndex);
                UpdateGhostPreview(dropSection, draggingBook, insertIndex);
            }
        }

        private void EndDrag(Vector2 pointer)
        {
            var movedBook = draggingBook;
            var returnSection = originalSectionBeforeDrag;
            var returnIndex = originalIndexBeforeDrag;
            var localPosition = generator.BooksRoot.parent.InverseTransformPoint(draggingBook.transform.position);
            var destination = state.FindAvailableDropSection(localPosition, draggingBook);
            var insertIndex = destination != null ? state.ComputeDropInsertIndex(localPosition, destination, draggingBook) : 0;
            var targetLocalPosition = default(Vector3);
            var targetLocalRotation = Quaternion.identity;
            var targetLocalScale = Vector3.one;
            var isValidDrop = destination != null &&
                              state.TryPlaceBookForDrop(
                                  movedBook,
                                  destination,
                                  insertIndex,
                                  out targetLocalPosition,
                                  out targetLocalRotation,
                                  out targetLocalScale);

            if (ghostPreview != null)
            {
                ghostPreview.SetActive(false);
            }

            if (isValidDrop)
            {
                draggingBook = null;
                originalSectionBeforeDrag = null;
                originalIndexBeforeDrag = -1;
                pendingDropAfterBookshelfSwitch = false;
                shelfBookPointerTravel = 0f;
                ResetEdgeSwitchDwell();

                if (generator != null && generator.BooksRoot != null && movedBook.transform.parent != generator.BooksRoot)
                {
                    movedBook.transform.SetParent(generator.BooksRoot, true);
                }

                ClearDragLayoutPreview();
                if (returnSection != null && returnSection != destination)
                {
                    state.LayoutSection(returnSection, this, true);
                }

                state.LayoutSectionExcept(destination, this, true, movedBook);
                StartCoroutine(AnimateValidDrop(movedBook, targetLocalPosition, targetLocalRotation, targetLocalScale));

                return;
            }

            ResetDragLayoutPreview(true);
            draggingBook = null;
            originalSectionBeforeDrag = null;
            originalIndexBeforeDrag = -1;
            pendingDropAfterBookshelfSwitch = false;
            shelfBookPointerTravel = 0f;
            ResetEdgeSwitchDwell();
            HandleInvalidDrop(movedBook, returnSection, returnIndex);
        }

        private IEnumerator AnimateValidDrop(
            BookEntity book,
            Vector3 targetLocalPosition,
            Quaternion targetLocalRotation,
            Vector3 targetLocalScale)
        {
            if (book == null)
            {
                ReleaseActiveDraggedBook(null);
                yield break;
            }

            var target = book.transform;
            var startPosition = target.localPosition;
            var startRotation = target.localRotation;
            var startScale = target.localScale;
            var elapsed = 0f;

            while (elapsed < validDropDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / validDropDuration);
                var horizontalT = Mathf.SmoothStep(0f, 1f, t);
                var fallT = t * t;

                var nextPosition = Vector3.Lerp(startPosition, targetLocalPosition, horizontalT);
                nextPosition.y = Mathf.Lerp(startPosition.y, targetLocalPosition.y, fallT);

                target.localPosition = nextPosition;
                target.localRotation = Quaternion.Slerp(startRotation, targetLocalRotation, horizontalT);
                target.localScale = Vector3.Lerp(startScale, targetLocalScale, horizontalT);
                yield return null;
            }

            target.localPosition = targetLocalPosition;
            target.localRotation = targetLocalRotation;
            target.localScale = targetLocalScale;
            ReleaseActiveDraggedBook(book);
        }

        private void HandleInvalidDrop(BookEntity book, ShelfSection returnSection, int returnIndex)
        {
            if (invalidDropBehavior == InvalidDropBehavior.PhysicsFloorDrop)
            {
                // MVP keeps invalid drops slot-based. The physics path stays in code for a later post-MVP option.
            }

            StartCoroutine(AnimateInvalidReturn(book, returnSection, returnIndex));
        }

        private void UpdateEdgeSwitchDwell(BookEntity book)
        {
            if (book == null ||
                generator == null ||
                generator.BookshelfCount <= 1 ||
                generator.IsBookshelfSwitching)
            {
                ResetEdgeSwitchDwell();
                return;
            }

            var localPosition = generator.transform.InverseTransformPoint(book.transform.position);
            var direction = 0;
            if (localPosition.x > edgeSwitchThresholdX)
            {
                direction = 1;
            }
            else if (localPosition.x < -edgeSwitchThresholdX)
            {
                direction = -1;
            }

            if (direction == 0)
            {
                ResetEdgeSwitchDwell();
                return;
            }

            if (direction != edgeSwitchDirection)
            {
                edgeSwitchDirection = direction;
                edgeSwitchElapsed = 0f;
                return;
            }

            edgeSwitchElapsed += Time.deltaTime;
            if (edgeSwitchElapsed < edgeSwitchDwellSeconds)
            {
                return;
            }

            TriggerEdgeSwitch(direction);
        }

        private void TriggerEdgeSwitch(int direction)
        {
            if (draggingBook == null || generator == null || generator.IsBookshelfSwitching)
            {
                ResetEdgeSwitchDwell();
                return;
            }

            var dragLayer = EnsureDragLayerRoot();
            if (dragLayer != null && draggingBook.transform.parent != dragLayer)
            {
                draggingBook.transform.SetParent(dragLayer, true);
            }

            var sourceSection = originalSectionBeforeDrag;
            var sourceIndex = originalIndexBeforeDrag;
            ResetDragLayoutPreview(true);
            DetachDraggingBookFromCurrentBookshelf();
            if (ghostPreview != null)
            {
                ghostPreview.SetActive(false);
            }

            var switched = direction > 0
                ? generator.ShowNextBookshelf()
                : generator.ShowPreviousBookshelf();
            if (!switched)
            {
                RestoreDetachedDraggingBook(sourceSection, sourceIndex);
                ResetEdgeSwitchDwell();
                return;
            }

            originalSectionBeforeDrag = null;
            originalIndexBeforeDrag = -1;
            ResetEdgeSwitchDwell();
        }

        private Transform EnsureDragLayerRoot()
        {
            if (dragLayerRoot != null)
            {
                return dragLayerRoot;
            }

            var rootObject = new GameObject("BookDragLayer");
            var parent = generator != null ? generator.transform : transform;
            rootObject.transform.SetParent(parent, false);
            dragLayerRoot = rootObject.transform;
            return dragLayerRoot;
        }

        private void ResetEdgeSwitchDwell()
        {
            edgeSwitchDirection = 0;
            edgeSwitchElapsed = 0f;
        }

        private void UpdateDragLayoutPreview(ShelfSection section, BookEntity book, int insertIndex)
        {
            if (state == null || book == null)
            {
                return;
            }

            if (section == null)
            {
                ResetDragLayoutPreview(true);
                return;
            }

            if (activeDragPreviewSection == section && activeDragPreviewInsertIndex == insertIndex)
            {
                return;
            }

            if (activeDragPreviewSection != null && activeDragPreviewSection != section)
            {
                state.LayoutSectionExcept(activeDragPreviewSection, this, true, book);
            }

            activeDragPreviewSection = section;
            activeDragPreviewInsertIndex = insertIndex;
            state.PreviewSectionForDrop(section, this, true, book, insertIndex);
        }

        private void ResetDragLayoutPreview(bool animate)
        {
            if (state != null && activeDragPreviewSection != null)
            {
                state.LayoutSectionExcept(activeDragPreviewSection, this, animate, draggingBook);
            }

            ClearDragLayoutPreview();
        }

        private void ClearDragLayoutPreview()
        {
            activeDragPreviewSection = null;
            activeDragPreviewInsertIndex = -1;
        }

        private void DetachDraggingBookFromCurrentBookshelf()
        {
            if (draggingBook == null || originalSectionBeforeDrag == null)
            {
                return;
            }

            originalSectionBeforeDrag.books.Remove(draggingBook);
            draggingBook.SetSection(null);
            if (state != null)
            {
                state.LayoutSection(originalSectionBeforeDrag, this, true);
            }
        }

        private void RestoreDetachedDraggingBook(ShelfSection section, int index)
        {
            if (draggingBook == null || section == null)
            {
                return;
            }

            if (!section.books.Contains(draggingBook))
            {
                section.books.Insert(Mathf.Clamp(index, 0, section.books.Count), draggingBook);
            }

            draggingBook.SetSection(section);
            originalSectionBeforeDrag = section;
            originalIndexBeforeDrag = index;
            if (state != null)
            {
                state.LayoutSectionExcept(section, this, true, draggingBook);
            }
        }

        private IEnumerator AnimateInvalidReturn(BookEntity book, ShelfSection returnSection, int returnIndex)
        {
            if (book == null)
            {
                ReleaseActiveDraggedBook(null);
                yield break;
            }

            DisableDropPhysics(book);
            if (returnSection == null && generator != null && generator.BooksRoot != null && generator.BooksRoot.parent != null)
            {
                var localPosition = generator.BooksRoot.parent.InverseTransformPoint(book.transform.position);
                returnSection = state.FindNearestAvailableSection(localPosition, book);
                returnIndex = returnSection != null ? returnSection.books.Count : 0;
            }

            if (returnSection == null ||
                !state.TryPlaceBookForDrop(book, returnSection, returnIndex, out var targetLocalPosition, out var targetLocalRotation, out var targetLocalScale))
            {
                if (!TryForcePlaceBookForInvalidReturn(book, returnSection, returnIndex, out targetLocalPosition, out targetLocalRotation, out targetLocalScale))
                {
                    ReleaseActiveDraggedBook(book);
                    yield break;
                }
            }

            if (generator != null && generator.BooksRoot != null && book.transform.parent != generator.BooksRoot)
            {
                book.transform.SetParent(generator.BooksRoot, true);
            }

            state.LayoutSectionExcept(returnSection, this, true, book);

            var target = book.transform;
            var startPosition = target.localPosition;
            var startRotation = target.localRotation;
            var startScale = target.localScale;
            var elapsed = 0f;
            var duration = Mathf.Max(0.01f, invalidReturnDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                target.localPosition = Vector3.Lerp(startPosition, targetLocalPosition, t);
                target.localRotation = Quaternion.Slerp(startRotation, targetLocalRotation, t);
                target.localScale = Vector3.Lerp(startScale, targetLocalScale, t);
                yield return null;
            }

            target.localPosition = targetLocalPosition;
            target.localRotation = targetLocalRotation;
            target.localScale = targetLocalScale;
            ReleaseActiveDraggedBook(book);
        }

        private bool TryForcePlaceBookForInvalidReturn(
            BookEntity book,
            ShelfSection preferredSection,
            int preferredIndex,
            out Vector3 targetLocalPosition,
            out Quaternion targetLocalRotation,
            out Vector3 targetLocalScale)
        {
            targetLocalPosition = default;
            targetLocalRotation = Quaternion.identity;
            targetLocalScale = Vector3.one;

            if (book == null || state == null)
            {
                return false;
            }

            var targetSection = preferredSection;
            if (targetSection == null && state.Sections.Count > 0)
            {
                targetSection = state.Sections[0];
            }

            if (targetSection == null)
            {
                return false;
            }

            if (book.CurrentSection != null)
            {
                book.CurrentSection.books.Remove(book);
            }

            targetSection.books.Remove(book);
            var insertIndex = preferredIndex >= 0 ? preferredIndex : targetSection.books.Count;
            targetSection.books.Insert(Mathf.Clamp(insertIndex, 0, targetSection.books.Count), book);
            book.SetSection(targetSection);
            return state.TryGetBookLayoutTransform(book, targetSection, out targetLocalPosition, out targetLocalRotation, out targetLocalScale);
        }

        private IEnumerator AnimateInvalidDrop(BookEntity book)
        {
            if (book == null)
            {
                ReleaseActiveDraggedBook(null);
                yield break;
            }

            var dropRoot = EnsureDropPhysicsRoot();
            if (dropRoot == null)
            {
                ReleaseActiveDraggedBook(book);
                yield break;
            }

            var target = book.transform;
            var collider = EnsureBookCollider(book);
            if (collider == null)
            {
                ReleaseActiveDraggedBook(book);
                yield break;
            }

            DisableDropPhysics(book);
            EnsureDropPhysicsBounds();
            target.SetParent(dropRoot, true);
            collider.isTrigger = false;
            collider.material = GetDropBookMaterial();
            book.SetSection(null);

            var body = target.gameObject.AddComponent<Rigidbody>();
            body.mass = Mathf.Max(0.01f, invalidDropBookMass);
            body.useGravity = true;
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var initialVelocity = ResolveInvalidDropVelocity(book);
            body.linearVelocity = initialVelocity;
            body.angularVelocity = ResolveInvalidDropAngularVelocity(initialVelocity);

            var elapsed = 0f;

            while (elapsed < invalidDropMaxDuration)
            {
                elapsed += Time.deltaTime;
                if (HasTouchedDropFloor(book))
                {
                    break;
                }

                yield return null;
            }

            ReleaseActiveDraggedBook(book);
        }

        private Vector3 ResolveInvalidDropVelocity(BookEntity book)
        {
            var velocity = dragWorldVelocity;
            velocity.y = Mathf.Min(velocity.y, 0f);

            var horizontal = new Vector2(velocity.x, velocity.z);
            if (horizontal.magnitude < invalidDropMinHorizontalSpeed)
            {
                var side = book != null && book.transform.position.x < 0f ? -1f : 1f;
                horizontal = new Vector2(side * invalidDropMinHorizontalSpeed, -invalidDropMinHorizontalSpeed * 0.5f);
            }
            else
            {
                var maxHorizontalSpeed = Mathf.Max(invalidDropMinHorizontalSpeed, invalidDropMaxHorizontalSpeed);
                horizontal = Vector2.ClampMagnitude(horizontal, maxHorizontalSpeed);
            }

            velocity.x = horizontal.x;
            velocity.z = horizontal.y;
            ResetDragVelocity();
            return velocity;
        }

        private Vector3 ResolveInvalidDropAngularVelocity(Vector3 initialVelocity)
        {
            var spin = Mathf.Max(0f, invalidDropThrowSpinMultiplier);
            var xSpin = Mathf.Clamp(-initialVelocity.z * spin + Random.Range(-0.4f, 0.4f), -3f, 3f);
            var zSpin = Mathf.Clamp(initialVelocity.x * spin + Random.Range(-0.4f, 0.4f), -3f, 3f);
            return new Vector3(xSpin, Random.Range(-0.8f, 0.8f), zSpin);
        }

        private void TrackDragVelocity(Vector3 velocity)
        {
            if (!IsFinite(velocity))
            {
                return;
            }

            dragVelocitySamples.Enqueue(velocity);
            while (dragVelocitySamples.Count > DragVelocitySampleLimit)
            {
                dragVelocitySamples.Dequeue();
            }

            var total = Vector3.zero;
            foreach (var sample in dragVelocitySamples)
            {
                total += sample;
            }

            dragWorldVelocity = total / dragVelocitySamples.Count;
        }

        private void ResetDragVelocity()
        {
            dragWorldVelocity = Vector3.zero;
            dragVelocitySamples.Clear();
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private Vector3 ClampDraggedBookToRoom(Vector3 worldPosition, BookEntity book)
        {
            var root = generator != null ? generator.transform : null;
            if (root == null || book == null)
            {
                return worldPosition;
            }

            var localPosition = root.InverseTransformPoint(worldPosition);
            var halfExtent = GetDragBoundaryHalfExtent(book);
            var skin = Mathf.Max(0f, dragWallSkin);
            var minX = roomLeftWallX + halfExtent.x + skin;
            var maxX = roomRightWallX - halfExtent.x - skin;
            if (minX <= maxX)
            {
                localPosition.x = Mathf.Clamp(localPosition.x, minX, maxX);
            }

            var minY = roomFloorY + halfExtent.y + skin;
            if (localPosition.y < minY)
            {
                localPosition.y = minY;
            }

            var minZ = roomFrontWallZ + halfExtent.z + skin;
            var maxZ = roomBackWallZ - halfExtent.z - skin;
            if (minZ <= maxZ)
            {
                localPosition.z = Mathf.Clamp(localPosition.z, minZ, maxZ);
            }

            return root.TransformPoint(localPosition);
        }

        private static Vector3 GetDragBoundaryHalfExtent(BookEntity book)
        {
            if (book == null)
            {
                return new Vector3(0.04f, 0.12f, 0.04f);
            }

            var horizontalHalf = Mathf.Max(book.thickness, book.depth, 0.02f) * 0.5f;
            var pitchRadians = DragPreviewPitchDegrees * Mathf.Deg2Rad;
            var verticalHalf =
                Mathf.Max(book.height, 0.02f) * 0.5f * Mathf.Abs(Mathf.Cos(pitchRadians)) +
                Mathf.Max(book.depth, 0.02f) * 0.5f * Mathf.Abs(Mathf.Sin(pitchRadians));
            return new Vector3(
                horizontalHalf,
                verticalHalf,
                horizontalHalf);
        }

        private Transform EnsureDropPhysicsRoot()
        {
            if (dropPhysicsRoot != null)
            {
                return dropPhysicsRoot;
            }

            var rootObject = new GameObject("BookDropPhysicsRoot");
            var parent = generator != null ? generator.transform : transform;
            rootObject.transform.SetParent(parent, false);
            dropPhysicsRoot = rootObject.transform;
            return dropPhysicsRoot;
        }

        private void EnsureDropPhysicsBounds()
        {
            if (dropBoundaryColliders.Count > 0 || EnsureDropPhysicsRoot() == null)
            {
                return;
            }

            var roomWidth = Mathf.Abs(roomRightWallX - roomLeftWallX);
            var wallThickness = Mathf.Max(0.02f, roomWallThickness);
            CreateDropBoundary(
                "DropFloor",
                new Vector3((roomLeftWallX + roomRightWallX) * 0.5f, roomFloorY - wallThickness * 0.5f, 0f),
                new Vector3(roomWidth + wallThickness * 2f, wallThickness, roomPhysicsDepth));
            CreateDropBoundary(
                "DropLeftWall",
                new Vector3(roomLeftWallX - wallThickness * 0.5f, roomWallHeight * 0.5f, 0f),
                new Vector3(wallThickness, roomWallHeight, roomPhysicsDepth));
            CreateDropBoundary(
                "DropRightWall",
                new Vector3(roomRightWallX + wallThickness * 0.5f, roomWallHeight * 0.5f, 0f),
                new Vector3(wallThickness, roomWallHeight, roomPhysicsDepth));
            CreateDropBoundary(
                "DropFrontWall",
                new Vector3((roomLeftWallX + roomRightWallX) * 0.5f, roomWallHeight * 0.5f, roomFrontWallZ - wallThickness * 0.5f),
                new Vector3(roomWidth + wallThickness * 2f, roomWallHeight, wallThickness));
            CreateDropBoundary(
                "DropBackWall",
                new Vector3((roomLeftWallX + roomRightWallX) * 0.5f, roomWallHeight * 0.5f, roomBackWallZ + wallThickness * 0.5f),
                new Vector3(roomWidth + wallThickness * 2f, roomWallHeight, wallThickness));
        }

        private void CreateDropBoundary(string boundaryName, Vector3 localPosition, Vector3 size)
        {
            var boundary = new GameObject(boundaryName);
            boundary.transform.SetParent(dropPhysicsRoot, false);
            boundary.transform.localPosition = localPosition;
            boundary.transform.localRotation = Quaternion.identity;

            var collider = boundary.AddComponent<BoxCollider>();
            collider.size = size;
            collider.material = GetDropBoundaryMaterial();
            dropBoundaryColliders.Add(collider);
        }

        private Collider EnsureBookCollider(BookEntity book)
        {
            if (book == null)
            {
                return null;
            }

            var collider = book.GetComponent<Collider>();
            if (collider != null)
            {
                return collider;
            }

            var box = book.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(
                Mathf.Max(book.thickness, 0.02f),
                Mathf.Max(book.height, 0.02f),
                Mathf.Max(book.depth, 0.02f));
            return box;
        }

        private void DisableDropPhysics(BookEntity book)
        {
            if (book == null)
            {
                return;
            }

            var body = book.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.useGravity = false;
                body.isKinematic = true;
                body.detectCollisions = false;
                DestroyRuntimeComponent(body);
            }

            var collider = book.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = false;
                collider.material = null;
            }
        }

        private bool HasTouchedDropFloor(BookEntity book)
        {
            var collider = book != null ? book.GetComponent<Collider>() : null;
            var floorWorldY = generator != null
                ? generator.transform.TransformPoint(new Vector3(0f, roomFloorY, 0f)).y
                : roomFloorY;
            return collider != null
                ? collider.bounds.min.y <= floorWorldY + 0.015f
                : book != null && book.transform.position.y <= floorWorldY;
        }

        private PhysicsMaterial GetDropBookMaterial()
        {
            if (dropBookMaterial != null)
            {
                return dropBookMaterial;
            }

            dropBookMaterial = new PhysicsMaterial("BookDrop_Book")
            {
                dynamicFriction = invalidDropFriction,
                staticFriction = invalidDropFriction,
                bounciness = invalidDropBookBounciness,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };
            return dropBookMaterial;
        }

        private PhysicsMaterial GetDropBoundaryMaterial()
        {
            if (dropBoundaryMaterial != null)
            {
                return dropBoundaryMaterial;
            }

            dropBoundaryMaterial = new PhysicsMaterial("BookDrop_Boundary")
            {
                dynamicFriction = invalidDropFriction,
                staticFriction = invalidDropFriction,
                bounciness = invalidDropBoundaryBounciness,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };
            return dropBoundaryMaterial;
        }

        private static void DestroyRuntimeComponent(Component component)
        {
            if (component == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(component);
            }
            else
            {
                DestroyImmediate(component);
            }
        }

        private void ReleaseActiveDraggedBook(BookEntity book)
        {
            if (state == null || (book != null && state.ActiveDraggedBook != book))
            {
                return;
            }

            state.ActiveDraggedBook = null;
        }

        private void EnsureViewedBookHitArea(BookEntity book)
        {
            var existingArea = book.GetComponentInChildren<BookInteractionHitArea>(true);
            if (existingArea == null)
            {
                var hitAreaObject = new GameObject("ViewedInteractionArea");
                hitAreaObject.transform.SetParent(book.transform, false);
                existingArea = hitAreaObject.AddComponent<BookInteractionHitArea>();
            }

            var hitAreaSize = new Vector3(
                1f + viewedBookInteractionPadding.x / Mathf.Max(book.thickness, 0.001f),
                1f + viewedBookInteractionPadding.y / Mathf.Max(book.height, 0.001f),
                1f + viewedBookInteractionPadding.z / Mathf.Max(book.depth, 0.001f));

            existingArea.Configure(book, hitAreaSize, Vector3.zero);
            activeViewedHitArea = existingArea;
        }

        private bool IsPointerOverActiveViewedBook(PointerTarget target)
        {
            return state.IsViewingBook &&
                   state.ActiveViewedBook != null &&
                   target.Book == state.ActiveViewedBook &&
                   target.Type == PointerTargetType.Book;
        }

        private void EnsureGhostPreview(BookEntity book)
        {
            if (ghostPreview != null)
            {
                ghostPreview.SetActive(true);
                return;
            }

            ghostPreview = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ghostPreview.name = "GhostPreview";
            Destroy(ghostPreview.GetComponent<Collider>());
            ghostPreview.GetComponent<MeshRenderer>().sharedMaterial = materialFactory.GetGhostPreviewMaterial();
            ghostPreview.transform.localScale = new Vector3(book.thickness, book.height, book.depth);
        }

        private void UpdateGhostPreview(ShelfSection section, BookEntity book, int insertIndex)
        {
            if (ghostPreview == null)
            {
                return;
            }

            if (section == null)
            {
                ghostPreview.SetActive(false);
                return;
            }

            if (!state.TryGetBookPreviewTransform(book, section, insertIndex, out var localPosition, out var localRotation, out _))
            {
                ghostPreview.SetActive(false);
                return;
            }

            ghostPreview.SetActive(true);
            ghostPreview.transform.SetParent(generator.BooksRoot, false);
            var layoutScale = state.GetDropLayoutScale(section, book);
            ghostPreview.transform.localScale = new Vector3(book.thickness, book.height, book.depth) * layoutScale;
            ghostPreview.transform.localPosition = localPosition;
            ghostPreview.transform.localRotation = localRotation;
        }

        private IEnumerator AnimateWorldMove(Transform target, Vector3 worldPosition, Quaternion worldRotation, Vector3 targetScale)
        {
            var elapsed = 0f;
            var startPos = target.position;
            var startRot = target.rotation;
            var startScale = target.localScale;

            while (elapsed < viewMoveDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, elapsed / viewMoveDuration);
                target.position = Vector3.Lerp(startPos, worldPosition, t);
                target.rotation = Quaternion.Slerp(startRot, worldRotation, t);
                target.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }

            target.position = worldPosition;
            target.rotation = worldRotation;
            target.localScale = targetScale;
        }

        private void EnsureRuntimeUI()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var eventSystemObject = new GameObject("EventSystem");
                eventSystemObject.AddComponent<EventSystem>();
                eventSystemObject.AddComponent<InputSystemUIInputModule>();
            }

            var canvasObject = new GameObject("BookViewCanvas");
            runtimeCanvas = canvasObject.AddComponent<Canvas>();
            runtimeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            var buttonObject = new GameObject("BackButton");
            buttonObject.transform.SetParent(canvasObject.transform, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.13f, 0.13f, 0.13f, 0.82f);

            backButton = buttonObject.AddComponent<Button>();
            backButton.targetGraphic = image;
            backButton.onClick.AddListener(() => CloseViewedBook(true));

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            rect.sizeDelta = new Vector2(120f, 42f);

            var textObject = new GameObject("Label");
            textObject.transform.SetParent(buttonObject.transform, false);
            var text = textObject.AddComponent<Text>();
            text.text = "X";
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = RuntimeUiFont.Resolve();
            text.resizeTextForBestFit = true;
            text.raycastTarget = false;

            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            buttonObject.SetActive(false);
        }

        private bool IsPointerOverUi(Vector2 pointer)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = pointer
            };
            uiRaycastResults.Clear();
            EventSystem.current.RaycastAll(eventData, uiRaycastResults);
            return uiRaycastResults.Count > 0;
        }

        private readonly struct PointerTarget
        {
            public static PointerTarget None => new PointerTarget(PointerTargetType.None, null, null);

            public PointerTargetType Type { get; }
            public BookEntity Book { get; }
            public ShelfSection Section { get; }

            public PointerTarget(PointerTargetType type, BookEntity book, ShelfSection section)
            {
                Type = type;
                Book = book;
                Section = section;
            }
        }

        private enum PointerTargetType
        {
            None,
            Book,
            ViewedBookInteractionArea,
            Section
        }
    }
}
