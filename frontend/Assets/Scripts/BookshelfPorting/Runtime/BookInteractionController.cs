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
        [SerializeField] private Camera sceneCamera = null;
        [SerializeField] private BookshelfState state = null;
        [SerializeField] private BookshelfGenerator generator = null;
        [SerializeField] private CameraController cameraController = null;
        [SerializeField] private MaterialFactory materialFactory = null;
        [SerializeField] private BookshelfExperienceManager experienceManager = null;
        [SerializeField] private float viewMoveDuration = 0.25f;
        [SerializeField] private float dragStartThresholdPixels = 5f;
        [SerializeField] private float validDropDuration = 0.3f;
        [SerializeField] private float invalidDropMaxDuration = 3f;
        [SerializeField] private float invalidDropBookMass = 0.18f;
        [SerializeField] private float invalidDropMinHorizontalSpeed = 0.45f;
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
        [SerializeField] private float viewedBookScaleMultiplier = 3.6f;
        [SerializeField] private Vector3 viewedBookInteractionPadding = new Vector3(0.28f, 0.22f, 0.18f);
        [SerializeField] private Vector3 editMarkerScalePadding = new Vector3(-0.06f, -0.12f, -0.02f);

        private readonly List<ShelfSectionMarker> editMarkers = new List<ShelfSectionMarker>();
        private BookEntity hoveredBook;
        private BookEntity pressedBook;
        private BookEntity draggingBook;
        private GameObject ghostPreview;
        private Transform dropPhysicsRoot;
        private PhysicsMaterial dropBookMaterial;
        private PhysicsMaterial dropBoundaryMaterial;
        private ShelfSection originalSectionBeforeDrag;
        private Vector2 lastPointer;
        private float shelfBookPointerTravel;
        private Vector3 lastDragWorldPosition;
        private Vector3 dragWorldVelocity;
        private Canvas runtimeCanvas;
        private Button backButton;
        private BookInteractionHitArea activeViewedHitArea;
        private bool isManipulatingViewedBook;
        private bool viewedBookPressStartedOnBook;
        private float viewedBookPointerTravel;
        private PointerTarget currentPointerTarget;
        private readonly List<BoxCollider> dropBoundaryColliders = new List<BoxCollider>();
        private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

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

            UpdateEditMarkers();

            var pointer = mouse.position.ReadValue();
            currentPointerTarget = RaycastPointerTarget(pointer);

            if (!CanViewShelfBooks())
            {
                hoveredBook = null;
                pressedBook = null;

                if (draggingBook != null)
                {
                    CancelDrag();
                }
            }
            else
            {
                hoveredBook = currentPointerTarget.Book;
            }

            if (state.IsViewingBook)
            {
                hoveredBook = IsPointerOverActiveViewedBook(currentPointerTarget) ? currentPointerTarget.Book : null;
            }

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

        public void SetActiveBookOrientation(BookOrientation orientation)
        {
            var target = state.ActiveViewedBook != null ? state.ActiveViewedBook : hoveredBook;
            if (target == null)
            {
                return;
            }

            state.ChangeBookOrientation(target, orientation, this);
        }

        public void CloseViewedBook(bool reapplyLayout)
        {
            if (!state.IsViewingBook || state.ActiveViewedBook == null)
            {
                return;
            }

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
                else if (IsEditModeActive() && experienceManager.SelectedEditBook != null)
                {
                    experienceManager.ClearEditSelection();
                }
            }

            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                SetActiveBookOrientation(BookOrientation.Spine);
            }

            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                SetActiveBookOrientation(BookOrientation.Front);
            }

            if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                SetActiveBookOrientation(BookOrientation.Angled45);
            }
        }

        private void OnPointerDown(Vector2 pointer)
        {
            if (state == null)
            {
                pressedBook = null;
                isManipulatingViewedBook = false;
                return;
            }

            if (!CanViewShelfBooks() && !state.IsViewingBook)
            {
                pressedBook = null;
                return;
            }

            currentPointerTarget = RaycastPointerTarget(pointer);

            if (IsPointerOverUi(pointer))
            {
                pressedBook = null;
                isManipulatingViewedBook = false;
                return;
            }

            if (IsEditModeActive())
            {
                if (currentPointerTarget.Type == PointerTargetType.Book && currentPointerTarget.Book != null)
                {
                    experienceManager.SelectEditBook(currentPointerTarget.Book);
                    lastPointer = pointer;
                    shelfBookPointerTravel = 0f;
                    pressedBook = currentPointerTarget.Book;
                    return;
                }

                if (currentPointerTarget.Type == PointerTargetType.EditSectionMarker)
                {
                    lastPointer = pointer;
                    shelfBookPointerTravel = 0f;
                    pressedBook = experienceManager.SelectedEditBook;
                    return;
                }

                pressedBook = null;
                return;
            }

            if (state.IsViewingBook)
            {
                lastPointer = pointer;
                pressedBook = state.ActiveViewedBook;
                isManipulatingViewedBook = true;
                viewedBookPressStartedOnBook = IsPointerOverActiveViewedBook(currentPointerTarget);
                viewedBookPointerTravel = 0f;
                return;
            }

            lastPointer = pointer;
            shelfBookPointerTravel = 0f;
            pressedBook = currentPointerTarget.Book;
        }

        private void OnPointerDrag(Vector2 pointer)
        {
            var delta = pointer - lastPointer;
            lastPointer = pointer;

            if (state.IsViewingBook && state.ActiveViewedBook != null)
            {
                if (isManipulatingViewedBook)
                {
                    viewedBookPointerTravel += delta.magnitude;
                    state.ActiveViewedBook.AddViewRotation(delta, viewRotationSpeed);
                }

                return;
            }

            if (pressedBook == null)
            {
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
                isManipulatingViewedBook = false;
                return;
            }

            currentPointerTarget = RaycastPointerTarget(pointer);

            if (draggingBook != null)
            {
                EndDrag(pointer);
            }
            else if (IsEditModeActive())
            {
                if (currentPointerTarget.Type == PointerTargetType.EditSectionMarker &&
                    experienceManager.SelectedEditBook != null)
                {
                    var section = state.GetSection(currentPointerTarget.Row, currentPointerTarget.Column);
                    TryPlaceSelectedEditBook(section);
                }
                else if (pressedBook != null)
                {
                    experienceManager.SelectEditBook(pressedBook);
                }
            }
            else if (state.IsViewingBook)
            {
                var pointerOverActiveBook = IsPointerOverActiveViewedBook(currentPointerTarget);
                if (isManipulatingViewedBook && viewedBookPointerTravel <= 4f)
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
                viewedBookPointerTravel = 0f;
            }
            else if (pressedBook != null &&
                     !state.IsViewingBook &&
                     (hoveredBook == pressedBook || currentPointerTarget.Book == pressedBook))
            {
                ViewBook(pressedBook);
            }

            pressedBook = null;
            shelfBookPointerTravel = 0f;
        }

        private bool CanInteractWithShelfBooks()
        {
            return state != null &&
                   sceneCamera != null &&
                   cameraController != null &&
                   cameraController.CurrentMode == CameraMode.Frontal &&
                   generator != null &&
                   !generator.IsBookshelfSwitching;
        }

        private bool CanViewShelfBooks()
        {
            return state != null &&
                   sceneCamera != null &&
                   cameraController != null &&
                   cameraController.CurrentMode == CameraMode.Frontal &&
                   generator != null &&
                   !generator.IsBookshelfSwitching;
        }

        private bool IsEditModeActive()
        {
            return experienceManager != null && experienceManager.IsEditMode;
        }

        private void CancelDrag()
        {
            if (draggingBook == null)
            {
                return;
            }

            if (originalSectionBeforeDrag != null)
            {
                state.TryPlaceBook(draggingBook, originalSectionBeforeDrag, this, true);
                state.LayoutSection(originalSectionBeforeDrag, this, true);
            }

            if (ghostPreview != null)
            {
                ghostPreview.SetActive(false);
            }

            draggingBook = null;
            state.ActiveDraggedBook = null;
            originalSectionBeforeDrag = null;
            shelfBookPointerTravel = 0f;
            dragWorldVelocity = Vector3.zero;
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

            PointerTarget fallbackMarker = PointerTarget.None;

            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                var hitArea = hit.collider.GetComponent<BookInteractionHitArea>();
                if (hitArea != null && hitArea.Owner != null)
                {
                    return new PointerTarget(PointerTargetType.ViewedBookInteractionArea, hitArea.Owner);
                }

                var book = hit.collider.GetComponentInParent<BookEntity>();
                if (book != null)
                {
                    return new PointerTarget(PointerTargetType.Book, book);
                }

                var marker = hit.collider.GetComponent<ShelfSectionMarker>();
                if (marker != null && fallbackMarker.Type == PointerTargetType.None)
                {
                    fallbackMarker = new PointerTarget(PointerTargetType.EditSectionMarker, null, marker.Row, marker.Column);
                }
            }

            return fallbackMarker;
        }

        private void ViewBook(BookEntity book)
        {
            if (book == null || state == null || cameraController == null || cameraController.BookViewAnchor == null)
            {
                return;
            }

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
                cameraController.BookViewAnchor.position,
                viewRotation,
                book.StoredScale * viewedBookScaleMultiplier));
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

        private void TryPlaceSelectedEditBook(ShelfSection destination)
        {
            var selectedBook = experienceManager.SelectedEditBook;
            if (selectedBook == null || destination == null)
            {
                return;
            }

            var sourceSection = selectedBook.CurrentSection;
            if (sourceSection == destination)
            {
                return;
            }

            if (sourceSection != null)
            {
                state.RemoveBook(selectedBook);
                state.LayoutSection(sourceSection, this, true);
            }

            if (state.TryPlaceBook(selectedBook, destination, this, true))
            {
                experienceManager.SelectEditBook(selectedBook);
                return;
            }

            if (sourceSection != null)
            {
                state.TryPlaceBook(selectedBook, sourceSection, this, true);
                state.LayoutSection(sourceSection, this, true);
            }
        }

        private void BeginDrag(BookEntity book)
        {
            DisableDropPhysics(book);
            if (generator != null && generator.BooksRoot != null && book.transform.parent != generator.BooksRoot)
            {
                book.transform.SetParent(generator.BooksRoot, true);
            }

            draggingBook = book;
            state.ActiveDraggedBook = book;
            originalSectionBeforeDrag = book.CurrentSection;
            lastDragWorldPosition = book.transform.position;
            dragWorldVelocity = Vector3.zero;

            if (originalSectionBeforeDrag != null)
            {
                state.RemoveBook(book);
                state.LayoutSection(originalSectionBeforeDrag, this, true);
            }

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
                    dragWorldVelocity = (world - lastDragWorldPosition) / Time.deltaTime;
                }

                lastDragWorldPosition = world;
                draggingBook.transform.position = world;
                draggingBook.transform.rotation = Quaternion.Euler(15f, 0f, 0f);

                var local = generator.BooksRoot.parent.InverseTransformPoint(world);
                var dropSection = state.FindAvailableDropSection(local, draggingBook);
                UpdateGhostPreview(dropSection, draggingBook);
            }
        }

        private void EndDrag(Vector2 pointer)
        {
            var movedBook = draggingBook;
            var localPosition = generator.BooksRoot.parent.InverseTransformPoint(draggingBook.transform.position);
            var destination = state.FindAvailableDropSection(localPosition, draggingBook);

            if (ghostPreview != null)
            {
                ghostPreview.SetActive(false);
            }

            draggingBook = null;
            originalSectionBeforeDrag = null;
            shelfBookPointerTravel = 0f;

            if (destination != null &&
                state.TryPlaceBookForDrop(movedBook, destination, out var targetLocalPosition, out var targetLocalRotation, out var targetLocalScale))
            {
                if (generator != null && generator.BooksRoot != null && movedBook.transform.parent != generator.BooksRoot)
                {
                    movedBook.transform.SetParent(generator.BooksRoot, true);
                }

                state.LayoutSectionExcept(destination, this, true, movedBook);
                StartCoroutine(AnimateValidDrop(movedBook, targetLocalPosition, targetLocalRotation, targetLocalScale));

                if (IsEditModeActive() && movedBook != null)
                {
                    experienceManager.SelectEditBook(movedBook);
                }

                return;
            }

            StartCoroutine(AnimateInvalidDrop(movedBook));
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
            body.linearVelocity = ResolveInvalidDropVelocity(book);
            body.angularVelocity = new Vector3(
                Random.Range(-1.5f, 1.5f),
                Random.Range(-0.8f, 0.8f),
                Random.Range(-1.5f, 1.5f));

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

            velocity.x = horizontal.x;
            velocity.z = horizontal.y;
            dragWorldVelocity = Vector3.zero;
            return velocity;
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
            return new Vector3(
                horizontalHalf,
                Mathf.Max(book.height, 0.02f) * 0.5f,
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
                   target.Type != PointerTargetType.None;
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

        private void UpdateGhostPreview(ShelfSection section, BookEntity book)
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

            ghostPreview.SetActive(true);
            ghostPreview.transform.SetParent(generator.BooksRoot, false);
            ghostPreview.transform.localScale = new Vector3(book.thickness, book.height, book.depth);

            var x = section.leftBound + state.BookPadding + book.GetFootprint() * 0.5f;
            ghostPreview.transform.localPosition = new Vector3(x, section.shelfTopY + book.height * 0.5f, section.frontZ - 0.01f);
            ghostPreview.transform.localRotation = BookFootprintUtility.GetVisualRotation(book.orientation);
        }

        private void UpdateEditMarkers()
        {
            RemoveMissingEditMarkers();
            EnsureEditMarkers();

            var shouldShowMarkers = IsEditModeActive() &&
                                    CanInteractWithShelfBooks() &&
                                    state != null &&
                                    generator != null &&
                                    experienceManager.SelectedEditBook != null;

            for (var i = 0; i < editMarkers.Count; i++)
            {
                var marker = editMarkers[i];
                if (marker == null)
                {
                    continue;
                }

                if (!shouldShowMarkers)
                {
                    marker.SetVisible(false);
                    continue;
                }

                var section = state.GetSection(marker.Row, marker.Column);
                var selectedBook = experienceManager.SelectedEditBook;
                var canFit = section != null &&
                             (section == selectedBook.CurrentSection || section.CanFit(selectedBook, state.BookPadding));

                marker.SetVisible(canFit);
            }
        }

        private void EnsureEditMarkers()
        {
            if (state == null || generator == null || materialFactory == null)
            {
                return;
            }

            RemoveMissingEditMarkers();

            if (editMarkers.Count == state.Sections.Count)
            {
                return;
            }

            for (var i = 0; i < editMarkers.Count; i++)
            {
                if (editMarkers[i] != null)
                {
                    DestroyMarkerObject(editMarkers[i].gameObject);
                }
            }

            editMarkers.Clear();

            for (var i = 0; i < state.Sections.Count; i++)
            {
                var section = state.Sections[i];
                var markerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                markerObject.name = $"SectionMarker_{section.row}_{section.column}";
                markerObject.transform.SetParent(generator.BooksRoot, false);
                markerObject.transform.localPosition = new Vector3(
                    (section.leftBound + section.rightBound) * 0.5f,
                    section.shelfTopY + section.sectionHeight * 0.48f,
                    section.frontZ - state.ShelfFrontInset - 0.04f);
                markerObject.transform.localScale = new Vector3(
                    Mathf.Max(0.08f, section.Width + editMarkerScalePadding.x),
                    Mathf.Max(0.08f, section.sectionHeight + editMarkerScalePadding.y),
                    Mathf.Max(0.04f, 0.08f + editMarkerScalePadding.z));

                var marker = markerObject.AddComponent<ShelfSectionMarker>();
                marker.Configure(section.row, section.column, materialFactory.GetGhostPreviewMaterial());
                marker.SetVisible(false);
                editMarkers.Add(marker);
            }
        }

        private void RemoveMissingEditMarkers()
        {
            for (var i = editMarkers.Count - 1; i >= 0; i--)
            {
                if (editMarkers[i] == null)
                {
                    editMarkers.RemoveAt(i);
                }
            }
        }

        private static void DestroyMarkerObject(GameObject markerObject)
        {
            if (markerObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(markerObject);
            }
            else
            {
                DestroyImmediate(markerObject);
            }
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
            text.text = "Back";
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
            public static PointerTarget None => new PointerTarget(PointerTargetType.None, null, -1, -1);

            public PointerTargetType Type { get; }
            public BookEntity Book { get; }
            public int Row { get; }
            public int Column { get; }

            public PointerTarget(PointerTargetType type, BookEntity book)
                : this(type, book, -1, -1)
            {
            }

            public PointerTarget(PointerTargetType type, BookEntity book, int row, int column)
            {
                Type = type;
                Book = book;
                Row = row;
                Column = column;
            }
        }

        private enum PointerTargetType
        {
            None,
            Book,
            ViewedBookInteractionArea,
            EditSectionMarker
        }
    }
}
