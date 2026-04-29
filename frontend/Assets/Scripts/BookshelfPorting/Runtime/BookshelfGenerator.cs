using System.Collections;
using System.Collections.Generic;
using App.Application.Books;
using App.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace BookshelfPorting.Runtime
{
    public class BookshelfGenerator : MonoBehaviour
    {
        private enum BookLabelKind
        {
            Cover,
            BackCover,
            Spine
        }

        private const int InitialBookshelfCount = 3;
        private const float BookshelfAnchorZ = 1.285f;
        private const float RoomWidth = 4.2f;
        private const float RoomDepth = 5f;
        private const float RoomHeight = 2.8f;
        private const float BackWallZ = 1.45f;
        private const float BackWallThickness = 0.08f;
        private const float BookshelfFacingPanelClearance = 0.045f;
        private const float BookshelfTurntableArmLength = BackWallZ - BackWallThickness * 0.5f - BookshelfAnchorZ + BookshelfFacingPanelClearance;
        private const float BookshelfTurntableZ = BookshelfAnchorZ + BookshelfTurntableArmLength;
        private const float BookshelfAlcoveStaticClearance = 0f;
        private const float BookshelfAlcoveDepth = 0.72f;
        private const float RotatingWallFrameSideReveal = 0.035f;
        private const float RotatingWallFrameTopReveal = 0.045f;
        private const float RotatingWallFrameReturnDepth = 0.06f;
        private const float BookshelfSwitchRotationDuration = 1f;
        private const float BookshelfSwitchFullAngle = 180f;
        private const float MinimumSectionFitScale = 0.68f;
        private const float SectionWallClearance = 0.012f;
        private const string BookCoverLabelsName = "BookCoverLabels";
        private const float BookCoverSurfaceOffset = 0.506f;
        private const float BookCoverCanvasWidthCoverage = 0.78f;
        private const float BookCoverCanvasHeightCoverage = 0.78f;
        private const float BookCoverCanvasPadding = 12f;
        private const float BookSpineCanvasHeightCoverage = 0.94f;
        private const float BookSpineCanvasPadding = 7f;
        private const int BookCoverFrontTitleLineLength = 7;
        private const int BookCoverFrontAuthorLineLength = 9;
        private const int BookCoverBackDescriptionLineLength = 11;
        private const int BookSpineTitleCharacterLimit = 10;
        private const int BookSpineTitleFontSize = 30;
        private const string HamhamAssetPath = "Assets/Meshy_AI_Hamham_0416082404_texture.glb";
        private const string BookStackAssetPath = "Assets/Arts/Models/Props/book_stack.glb";
        private const string RugAssetPath = "Assets/Arts/Models/Furniture/rug_natural.glb";
        private const string LampAssetPath = "Assets/Arts/Models/Furniture/lamp_floor.glb";
        private const string TableNaturalAssetPath = "Assets/Arts/Models/Furniture/table_natural.glb";
        private const string ClockHamhamAssetPath = "Assets/Arts/Models/Props/clock_hamham.glb";
        private const float DeskTargetWidthX = 0.54f;
        private const float DeskTargetHeightY = 0.41f;
        private const float DeskTargetDepthZ = 0.86f;
        private const string QABooksBaseTexturePath = "Assets/QA_Books/Textures/BooksA_bc.tga";
        private const string QABooksNormalTexturePath = "Assets/QA_Books/Textures/BooksA_n.tga";
        private const string QABooksMetallicTexturePath = "Assets/QA_Books/Textures/BooksA_m_ao_g.tga";
        private const string QABooksAltBaseTexturePath = "Assets/QA_Books/Textures/Books_bc.tga";
        private const string QABooksAltNormalTexturePath = "Assets/QA_Books/Textures/Books_n.tga";
        private const string QABooksAltMetallicTexturePath = "Assets/QA_Books/Textures/Books_m_ao_g.tga";
        private static readonly Vector2 BookCoverCanvasSize = new Vector2(144f, 216f);
        private static readonly Vector2 BookSpineCanvasSize = new Vector2(96f, 360f);
        private static readonly Vector3 MainSceneNotebookStationLocalPosition = new Vector3(0.72f, -1.18f, 0f);
        private static readonly Vector3 FallbackNotebookStationLocalPosition = new Vector3(-1.275f, 0f, 0.12f);

        [Header("References")]
        [SerializeField] private BookshelfState state = null;
        [SerializeField] private MaterialFactory materialFactory = null;
        [SerializeField] private Transform roomRoot = null;
        [SerializeField] private Transform bookshelfTurntableRoot = null;
        [SerializeField] private Transform bookshelfRoot = null;
        [SerializeField] private Transform booksRoot = null;
        [SerializeField] private GameObject hamhamModelAsset = null;
        [SerializeField] private GameObject bookStackAsset = null;
        [SerializeField] private GameObject rugAsset = null;
        [SerializeField] private GameObject lampAsset = null;
        [SerializeField] private GameObject tableNaturalAsset = null;
        [SerializeField] private GameObject clockHamhamAsset = null;
        [SerializeField] private Texture2D qaBooksBaseMap = null;
        [SerializeField] private Texture2D qaBooksNormalMap = null;
        [SerializeField] private Texture2D qaBooksMaskMap = null;
        [SerializeField] private Texture2D qaBooksAltBaseMap = null;
        [SerializeField] private Texture2D qaBooksAltNormalMap = null;
        [SerializeField] private Texture2D qaBooksAltMaskMap = null;

        [Header("Bookshelf Dimensions")]
        [SerializeField] private float width = 1.60f;
        [SerializeField] private float height = 1.60f;
        [SerializeField] private float depth = 0.25f;
        [SerializeField] private int rows = 4;
        [SerializeField] private int columns = 4;
        [SerializeField] private float boardThickness = 0.025f;
        [SerializeField] private float dividerThickness = 0.02f;
        [SerializeField] private float backPanelThickness = 0.012f;
        [Header("Defaults")]
        [SerializeField] private int booksPerSection = 3;
        [SerializeField] private Vector2 thicknessRange = new Vector2(0.025f, 0.045f);
        [SerializeField] private Vector2 heightRange = new Vector2(0.20f, 0.27f);
        [SerializeField] private float bookDepth = 0.16f;
        [SerializeField] private float addedBookInsertionDuration = 0.42f;
        [SerializeField] private Vector3 addedBookInsertionLocalOffset = new Vector3(0f, 0.12f, -0.46f);
        [SerializeField] private Color addBookPlacementGuideColor = new Color(0.30f, 0.58f, 0.47f, 0.78f);
        [SerializeField] private Color addBookPlacementGhostColor = new Color(0.30f, 0.58f, 0.47f, 0.24f);
        [SerializeField] private int randomSeed = 7;

        private readonly List<GameObject> generatedGeometry = new List<GameObject>();
        private readonly List<ShelfSection> bookshelfSections = new List<ShelfSection>();
        private readonly List<ShelfSection> transitionBookshelfSections = new List<ShelfSection>();
        private readonly List<GameObject> qaBookPrefabs = new List<GameObject>();
        private Material qaBooksMaterial;
        private Material qaBooksAltMaterial;
        private Coroutine bookshelfSwitchCoroutine;
        private int activeBookshelfIndex;
        private Vector3 turntableRootBaseScale = Vector3.one;
        private Vector3 bookshelfRootBaseScale = Vector3.one;
        private Transform transitionBookshelfRoot;
        private Transform transitionBooksRoot;
        private int pendingBookshelfIndex = -1;
        private int queuedBookshelfSwitchDirection;
        private int bookshelfCount = InitialBookshelfCount;
        private LoadMyBookshelfBooksUseCase loadBooksUseCase;
        private IReadOnlyList<BookCatalogItem> bookCatalogItems = System.Array.Empty<BookCatalogItem>();
        private readonly Dictionary<int, BookshelfLayoutSnapshot> bookshelfLayoutSnapshots = new Dictionary<int, BookshelfLayoutSnapshot>();
        private readonly Dictionary<int, string> bookshelfServerIds = new Dictionary<int, string>();
        private readonly Dictionary<int, Dictionary<string, string>> bookshelfSectionServerIds = new Dictionary<int, Dictionary<string, string>>();
        private readonly Dictionary<int, string> bookshelfNames = new Dictionary<int, string>();
        private readonly Dictionary<int, BookshelfThemeStyle> bookshelfDesignStyles = new Dictionary<int, BookshelfThemeStyle>();
        private readonly Dictionary<int, VisibilityScope> bookshelfVisibilityScopes = new Dictionary<int, VisibilityScope>();
        private BookEntity addBookPlacementGuideCandidate;
        private GameObject addBookPlacementGuideLine;
        private GameObject addBookPlacementGuideGhost;
        private Material addBookPlacementGuideLineMaterial;
        private Material addBookPlacementGuideGhostMaterial;
        private int runtimeBookSerial;

        public Transform BooksRoot => booksRoot;

        public bool TryGetBookshelfNavigationAnchors(out Vector3 previousAnchor, out Vector3 nextAnchor)
        {
            previousAnchor = default;
            nextAnchor = default;

            if (bookshelfRoot == null)
            {
                return false;
            }

            var sideOffset = width * 0.5f + Mathf.Max(0.10f, RotatingWallFrameSideReveal + 0.075f);
            var anchorY = height * 0.5f;
            var anchorZ = -depth * 0.5f - Mathf.Max(0.035f, BookshelfFacingPanelClearance);
            previousAnchor = bookshelfRoot.TransformPoint(new Vector3(-sideOffset, anchorY, anchorZ));
            nextAnchor = bookshelfRoot.TransformPoint(new Vector3(sideOffset, anchorY, anchorZ));
            return true;
        }
        public bool IsBookshelfSwitching => bookshelfSwitchCoroutine != null;
        public int BookshelfCount => bookshelfCount;
        public int ActiveBookshelfIndex => activeBookshelfIndex;
        public int DisplayBookshelfIndex => IsBookshelfSwitching && pendingBookshelfIndex >= 0 ? pendingBookshelfIndex : activeBookshelfIndex;
        public string DisplayBookshelfName => GetBookshelfName(DisplayBookshelfIndex);
        public int ActiveBookshelfBookCount => CountBooks(bookshelfSections);
        public BookshelfThemeStyle ActiveBookshelfDesignStyle => GetBookshelfDesignStyle(DisplayBookshelfIndex);
        public VisibilityScope ActiveBookshelfVisibility => GetBookshelfVisibility(DisplayBookshelfIndex);
        public IReadOnlyList<BookCatalogItem> BookCatalogItems
        {
            get
            {
                EnsureBookCatalog();
                return bookCatalogItems;
            }
        }

        public event System.Action BookshelfSwitchStateChanged;

        public void Configure(BookshelfState bookshelfState, MaterialFactory factory)
        {
            state = bookshelfState;
            materialFactory = factory;
        }

        public void Configure(BookshelfState bookshelfState, MaterialFactory factory, LoadMyBookshelfBooksUseCase booksUseCase)
        {
            state = bookshelfState;
            materialFactory = factory;
            loadBooksUseCase = booksUseCase;
        }

        public void ApplyServerBookshelves(IReadOnlyList<BookshelfRuntimeApiSnapshot> shelves)
        {
            if (shelves == null || shelves.Count == 0)
            {
                return;
            }

            var orderedShelves = new List<BookshelfRuntimeApiSnapshot>(shelves);
            orderedShelves.Sort((left, right) => left.DisplayOrder.CompareTo(right.DisplayOrder));

            bookshelfLayoutSnapshots.Clear();
            bookshelfServerIds.Clear();
            bookshelfSectionServerIds.Clear();
            bookshelfNames.Clear();
            bookshelfDesignStyles.Clear();
            bookshelfVisibilityScopes.Clear();

            bookshelfCount = Mathf.Max(1, orderedShelves.Count);
            activeBookshelfIndex = Mathf.Clamp(activeBookshelfIndex, 0, bookshelfCount - 1);
            pendingBookshelfIndex = -1;
            queuedBookshelfSwitchDirection = 0;

            for (var i = 0; i < orderedShelves.Count; i++)
            {
                var shelf = orderedShelves[i];
                bookshelfServerIds[i] = shelf.BookshelfId;
                bookshelfNames[i] = string.IsNullOrWhiteSpace(shelf.Name) ? GetDefaultBookshelfName(i) : shelf.Name;
                bookshelfVisibilityScopes[i] = shelf.VisibilityScope;
                bookshelfSectionServerIds[i] = CreateSectionServerIdMap(shelf);
                bookshelfLayoutSnapshots[i] = CreateServerBookshelfLayoutSnapshot(shelf, i);
            }

            if (bookshelfSections.Count > 0 && booksRoot != null)
            {
                RebuildActiveBookshelfVariant(activeBookshelfIndex);
                BookshelfSwitchStateChanged?.Invoke();
            }
        }

        public string GetBookshelfServerId(int index)
        {
            return bookshelfServerIds.TryGetValue(GetWrappedBookshelfIndex(index), out var id) ? id : null;
        }

        public string GetActiveBookshelfServerId()
        {
            return GetBookshelfServerId(activeBookshelfIndex);
        }

        public IReadOnlyList<string> GetBookshelfServerIdsInDisplayOrder()
        {
            var ids = new List<string>();
            for (var i = 0; i < bookshelfCount; i++)
            {
                if (bookshelfServerIds.TryGetValue(i, out var id) && !string.IsNullOrWhiteSpace(id))
                {
                    ids.Add(id);
                }
            }

            return ids;
        }

        public string GetSectionServerId(int bookshelfIndex, int row, int column)
        {
            if (!bookshelfSectionServerIds.TryGetValue(GetWrappedBookshelfIndex(bookshelfIndex), out var sections))
            {
                return null;
            }

            return sections.TryGetValue(GetSectionMapKey(row, column), out var id) ? id : null;
        }

        public int GetBookSlotNo(BookEntity book)
        {
            var section = book != null ? book.CurrentSection : null;
            if (section == null)
            {
                return 1;
            }

            var index = Mathf.Max(0, section.books.IndexOf(book));
            return index + 1;
        }

        private void Start()
        {
            RefreshExistingBookCoverVisuals();
            if (state != null && state.Sections.Count > 0 && booksRoot != null && booksRoot.childCount > 0)
            {
                return;
            }

            Generate();
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            EnsureRoots();
            LoadBookCatalog();
            ClearGenerated();
            BuildRoom();
            BuildBookshelf();
            NormalizeNotebookStationPlacement();
        }

        public void ApplyBookCatalogDataToExistingBooks()
        {
            if (booksRoot == null)
            {
                booksRoot = transform.Find("BookshelfTurntableRoot/BookshelfRoot/BooksRoot");
            }

            if (booksRoot == null)
            {
                booksRoot = transform.Find("BookshelfRoot/BooksRoot");
            }

            if (booksRoot == null)
            {
                return;
            }

            EnsureBookCatalog();
            if (bookCatalogItems.Count == 0)
            {
                Debug.LogWarning("Bookshelf catalog is empty; skipping book generation so stale mock/default book data is not shown.");
                return;
            }

            var books = booksRoot.GetComponentsInChildren<BookEntity>(true);
            for (var i = 0; i < books.Length; i++)
            {
                var catalogIndex = (activeBookshelfIndex * books.Length + i) % bookCatalogItems.Count;
                ApplyBookCatalogData(books[i], bookCatalogItems[catalogIndex], activeBookshelfIndex, i / booksPerSection, i % booksPerSection);
            }
        }

        public void NormalizeNotebookStationPlacement()
        {
            var resolvedRoomRoot = ResolveRoomRoot();
            if (resolvedRoomRoot == null)
            {
                return;
            }

            var guestbook = resolvedRoomRoot.Find("GuestbookBoard");
            if (guestbook == null)
            {
                return;
            }

            var notebookStation = FindPrimaryNotebookStation(resolvedRoomRoot);
            if (notebookStation == null)
            {
                return;
            }

            notebookStation.SetParent(guestbook, false);
            notebookStation.localPosition = MainSceneNotebookStationLocalPosition;
            notebookStation.localRotation = Quaternion.identity;
            notebookStation.localScale = Vector3.one;
        }

        private void EnsureRoots()
        {
            if (roomRoot == null)
            {
                roomRoot = new GameObject("RoomRoot").transform;
                roomRoot.SetParent(transform, false);
            }

            if (bookshelfTurntableRoot == null)
            {
                bookshelfTurntableRoot = transform.Find("BookshelfTurntableRoot");
            }

            if (bookshelfTurntableRoot == null)
            {
                bookshelfTurntableRoot = new GameObject("BookshelfTurntableRoot").transform;
                bookshelfTurntableRoot.SetParent(transform, false);
            }

            if (bookshelfRoot == null)
            {
                bookshelfRoot = bookshelfTurntableRoot.Find("BookshelfRoot");
            }

            if (bookshelfRoot == null)
            {
                bookshelfRoot = transform.Find("BookshelfRoot");
            }

            if (bookshelfRoot == null)
            {
                bookshelfRoot = new GameObject("BookshelfRoot").transform;
            }

            if (bookshelfRoot.parent != bookshelfTurntableRoot)
            {
                bookshelfRoot.SetParent(bookshelfTurntableRoot, false);
            }

            if (booksRoot == null)
            {
                booksRoot = new GameObject("BooksRoot").transform;
                booksRoot.SetParent(bookshelfRoot, false);
            }
            else if (booksRoot.parent != bookshelfRoot)
            {
                booksRoot.SetParent(bookshelfRoot, false);
            }
        }

        private void ClearGenerated()
        {
            if (bookshelfSwitchCoroutine != null)
            {
                StopCoroutine(bookshelfSwitchCoroutine);
                bookshelfSwitchCoroutine = null;
            }

            DestroyTransitionBookshelf();

            for (var i = generatedGeometry.Count - 1; i >= 0; i--)
            {
                if (generatedGeometry[i] != null)
                {
                    DestroyImmediate(generatedGeometry[i]);
                }
            }

            generatedGeometry.Clear();
            bookshelfSections.Clear();
            bookshelfLayoutSnapshots.Clear();
            bookshelfServerIds.Clear();
            bookshelfSectionServerIds.Clear();
            activeBookshelfIndex = 0;
            pendingBookshelfIndex = -1;
            queuedBookshelfSwitchDirection = 0;
            bookshelfCount = InitialBookshelfCount;

            ClearRoomChildren();
            ClearTurntableDecorationChildren();
            ClearBookshelfShellChildren();
            DestroyNamedChild(transform, "HamhamDisplay");

            DestroyBookEntitiesUnderBooksRoot();
        }

        private static readonly HashSet<string> PreservedRoomChildNames = new HashSet<string>
        {
            "LampFloor",
            "BookStack",
            "RugNatural",
            "Ceiling",
            "ClockHamham"
        };

        private void ClearRoomChildren()
        {
            if (roomRoot == null)
            {
                return;
            }

            var children = new List<GameObject>();
            for (var i = 0; i < roomRoot.childCount; i++)
            {
                var child = roomRoot.GetChild(i);
                if (PreservedRoomChildNames.Contains(child.name))
                {
                    continue;
                }

                children.Add(child.gameObject);
            }

            for (var i = 0; i < children.Count; i++)
            {
                DestroySpawnedObject(children[i]);
            }
        }

        private void ClearBookshelfShellChildren()
        {
            if (bookshelfRoot == null)
            {
                return;
            }

            ClearChildren(bookshelfRoot, booksRoot);
        }

        private void ClearTurntableDecorationChildren()
        {
            if (bookshelfTurntableRoot == null)
            {
                return;
            }

            ClearChildren(bookshelfTurntableRoot, bookshelfRoot);
        }

        private static void ClearChildren(Transform parent, Transform except)
        {
            var children = new List<GameObject>();
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (except != null && child == except)
                {
                    continue;
                }

                children.Add(child.gameObject);
            }

            for (var i = 0; i < children.Count; i++)
            {
                DestroySpawnedObject(children[i]);
            }
        }

        private Transform ResolveRoomRoot()
        {
            if (roomRoot != null)
            {
                return roomRoot;
            }

            var directRoot = transform.Find("RoomRoot");
            if (directRoot != null)
            {
                roomRoot = directRoot;
                return roomRoot;
            }

            var sceneRoot = GameObject.Find("RoomRoot");
            if (sceneRoot != null)
            {
                roomRoot = sceneRoot.transform;
                return roomRoot;
            }

            return null;
        }

        private static void DestroyNamedChild(Transform parent, string childName)
        {
            if (parent == null)
            {
                return;
            }

            var child = parent.Find(childName);
            if (child != null)
            {
                DestroySpawnedObject(child.gameObject);
            }
        }

        private void BuildRoom()
        {
            CreateBox("Floor", roomRoot, new Vector3(0f, -0.05f, 0f), new Vector3(RoomWidth, 0.1f, RoomDepth), materialFactory.GetFloorMaterial());
            BuildBackWallAlcove();
            CreateBox("LeftWall", roomRoot, new Vector3(-RoomWidth * 0.5f + 0.05f, RoomHeight * 0.5f, 0f), new Vector3(0.08f, RoomHeight, RoomDepth), materialFactory.GetWallMaterial());
            CreateBox("RightWall", roomRoot, new Vector3(RoomWidth * 0.5f - 0.05f, RoomHeight * 0.5f, 0f), new Vector3(0.08f, RoomHeight, RoomDepth), materialFactory.GetWallMaterial());

            BuildGuestbookBoard();
            BuildNotebookStation();
            EnsureRoomDecorations();
        }

        private static readonly Vector3 LampFloorLocalPosition = new Vector3(-1.05f, 0f, 1.05f);
        private static readonly Quaternion LampFloorLocalRotation = Quaternion.Euler(0f, 35f, 0f);
        private static readonly Vector3 LampFloorLocalScale = Vector3.one * 0.8f;

        private static readonly Vector3 BookStackLocalPosition = new Vector3(-1.72f, 0f, -0.42f);
        private static readonly Quaternion BookStackLocalRotation = Quaternion.Euler(0f, 18f, 0f);
        private static readonly Vector3 BookStackLocalScale = Vector3.one * 0.2f;

        private static readonly Vector3 RugNaturalLocalPosition = new Vector3(0.4f, 0.002f, -0.55f);
        private static readonly Quaternion RugNaturalLocalRotation = Quaternion.identity;
        private static readonly Vector3 RugNaturalLocalScale = Vector3.one * 0.6f;

        private static readonly Vector3 ClockHamhamLocalPosition = new Vector3(-2.0f, 1.72f, -1.15f);
        private static readonly Quaternion ClockHamhamLocalRotation = Quaternion.Euler(0f, 90f, 0f);
        private const float ClockHamhamTargetMaxDimension = 0.38f;
        private const float ClockHamhamAlignTopToY = 1.72f;

        public void EnsureRoomDecorations()
        {
            EnsureRoots();
            if (roomRoot == null)
            {
                return;
            }

            if (roomRoot.Find("LampFloor") == null)
            {
                SpawnDecoration(
                    ref lampAsset, LampAssetPath, "LampFloor",
                    LampFloorLocalPosition, LampFloorLocalRotation, LampFloorLocalScale,
                    alignToFloor: true);
            }

            if (roomRoot.Find("BookStack") == null)
            {
                SpawnDecoration(
                    ref bookStackAsset, BookStackAssetPath, "BookStack",
                    BookStackLocalPosition, BookStackLocalRotation, BookStackLocalScale,
                    alignToFloor: true);
            }

            if (roomRoot.Find("RugNatural") == null)
            {
                SpawnDecoration(
                    ref rugAsset, RugAssetPath, "RugNatural",
                    RugNaturalLocalPosition, RugNaturalLocalRotation, RugNaturalLocalScale,
                    alignToFloor: false);
            }

            if (roomRoot.Find("ClockHamham") == null)
            {
                SpawnWallMountedDecoration(
                    ref clockHamhamAsset, ClockHamhamAssetPath, "ClockHamham",
                    ClockHamhamLocalPosition, ClockHamhamLocalRotation, ClockHamhamTargetMaxDimension,
                    ClockHamhamAlignTopToY);
            }
        }

        private void SpawnWallMountedDecoration(ref GameObject cachedAsset, string assetPath, string rootName, Vector3 localPosition, Quaternion localRotation, float targetMaxDimension, float? alignTopToY = null)
        {
            var asset = ResolveAsset(cachedAsset, assetPath);
            if (asset == null)
            {
                return;
            }

            cachedAsset = asset;

            var decoRoot = new GameObject(rootName);
            decoRoot.transform.SetParent(roomRoot, false);
            decoRoot.transform.localPosition = localPosition;
            decoRoot.transform.localRotation = localRotation;
            decoRoot.transform.localScale = Vector3.one;

            var instance = Instantiate(asset, decoRoot.transform);
            instance.name = asset.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            var bounds = CalculateLocalBoundsFromMeshes(decoRoot.transform);
            if (bounds.size.sqrMagnitude > 0.0001f)
            {
                var maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (maxDim > 0.0001f)
                {
                    decoRoot.transform.localScale = Vector3.one * (targetMaxDimension / maxDim);
                }
            }

            if (alignTopToY.HasValue)
            {
                var parentBounds = CalculateBoundsOfSubtreeInSpace(decoRoot.transform, roomRoot);
                if (parentBounds.size.sqrMagnitude > 0.0001f)
                {
                    var yShift = alignTopToY.Value - parentBounds.max.y;
                    var lp = decoRoot.transform.localPosition;
                    decoRoot.transform.localPosition = new Vector3(lp.x, lp.y + yShift, lp.z);
                }
            }
        }

        private static Bounds CalculateBoundsOfSubtreeInSpace(Transform subtreeRoot, Transform coordinateSpace)
        {
            var hasBounds = false;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            var meshFilters = subtreeRoot.GetComponentsInChildren<MeshFilter>(true);
            for (var i = 0; i < meshFilters.Length; i++)
            {
                var mf = meshFilters[i];
                if (mf == null || mf.sharedMesh == null)
                {
                    continue;
                }

                var localBounds = mf.sharedMesh.bounds;
                var mft = mf.transform;
                var lmin = localBounds.min;
                var lmax = localBounds.max;
                for (var cx = 0; cx < 2; cx++)
                {
                    var x = cx == 0 ? lmin.x : lmax.x;
                    for (var cy = 0; cy < 2; cy++)
                    {
                        var y = cy == 0 ? lmin.y : lmax.y;
                        for (var cz = 0; cz < 2; cz++)
                        {
                            var z = cz == 0 ? lmin.z : lmax.z;
                            var worldPoint = mft.TransformPoint(new Vector3(x, y, z));
                            var spacePoint = coordinateSpace.InverseTransformPoint(worldPoint);
                            if (!hasBounds)
                            {
                                bounds = new Bounds(spacePoint, Vector3.zero);
                                hasBounds = true;
                            }
                            else
                            {
                                bounds.Encapsulate(spacePoint);
                            }
                        }
                    }
                }
            }

            return bounds;
        }

        private void SpawnDecoration(ref GameObject cachedAsset, string assetPath, string rootName, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, bool alignToFloor)
        {
            var asset = ResolveAsset(cachedAsset, assetPath);
            if (asset == null)
            {
                return;
            }

            cachedAsset = asset;

            var decoRoot = new GameObject(rootName);
            decoRoot.transform.SetParent(roomRoot, false);
            decoRoot.transform.localPosition = localPosition;
            decoRoot.transform.localRotation = localRotation;
            decoRoot.transform.localScale = localScale;

            var instance = Instantiate(asset, decoRoot.transform);
            instance.name = asset.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            if (alignToFloor)
            {
                AlignModelToFloor(decoRoot.transform, instance.transform);
            }
        }

        private static GameObject ResolveAsset(GameObject asset, string assetPath)
        {
            if (asset != null)
            {
                return asset;
            }

#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
#else
            return null;
#endif
        }

        private void BuildBackWallAlcove()
        {
            var wallMaterial = materialFactory.GetWallMaterial();
            var holeWidth = GetBookshelfAlcoveWidth();
            var holeHeight = GetBookshelfAlcoveHeight();
            var sideStripWidth = Mathf.Max(0.05f, (RoomWidth - holeWidth) * 0.5f);
            var topStripHeight = RoomHeight - holeHeight;

            CreateBox(
                "BackWall_LeftStrip",
                roomRoot,
                new Vector3(-(holeWidth + sideStripWidth) * 0.5f, RoomHeight * 0.5f, BackWallZ),
                new Vector3(sideStripWidth, RoomHeight, BackWallThickness),
                wallMaterial);
            CreateBox(
                "BackWall_RightStrip",
                roomRoot,
                new Vector3((holeWidth + sideStripWidth) * 0.5f, RoomHeight * 0.5f, BackWallZ),
                new Vector3(sideStripWidth, RoomHeight, BackWallThickness),
                wallMaterial);
            if (topStripHeight > 0.001f)
            {
                CreateBox(
                    "BackWall_TopStrip",
                    roomRoot,
                    new Vector3(0f, holeHeight + topStripHeight * 0.5f, BackWallZ),
                    new Vector3(holeWidth, topStripHeight, BackWallThickness),
                    wallMaterial);
            }

            BuildAlcoveInterior(holeWidth, holeHeight, wallMaterial);
        }

        private void BuildAlcoveInterior(float holeWidth, float holeHeight, Material wallMaterial)
        {
            var backZ = BookshelfTurntableZ + BookshelfAlcoveDepth;
            var sideThickness = BackWallThickness;
            var sideStartZ = GetBackWallBackSurfaceZ();
            var sideDepth = Mathf.Max(0.01f, backZ - sideStartZ);
            var sideMidZ = sideStartZ + sideDepth * 0.5f;

            CreateBox(
                "AlcoveBack",
                roomRoot,
                new Vector3(0f, holeHeight * 0.5f, backZ),
                new Vector3(holeWidth, holeHeight, BackWallThickness),
                wallMaterial);
            CreateBox(
                "AlcoveLeftSideFill",
                roomRoot,
                new Vector3(-holeWidth * 0.5f - sideThickness * 0.5f, holeHeight * 0.5f, sideMidZ),
                new Vector3(sideThickness, holeHeight, sideDepth),
                wallMaterial);
            CreateBox(
                "AlcoveRightSideFill",
                roomRoot,
                new Vector3(holeWidth * 0.5f + sideThickness * 0.5f, holeHeight * 0.5f, sideMidZ),
                new Vector3(sideThickness, holeHeight, sideDepth),
                wallMaterial);
            CreateBox(
                "AlcoveTopFill",
                roomRoot,
                new Vector3(0f, holeHeight + sideThickness * 0.5f, sideMidZ),
                new Vector3(holeWidth, sideThickness, sideDepth),
                wallMaterial);
        }

        private void BuildBookshelf()
        {
            ConfigureBookshelfTurntable();
            bookshelfRootBaseScale = bookshelfRoot.localScale;
            ResetBookshelfToFrontSlot();
            var wood = GetBookshelfMaterial(activeBookshelfIndex);

            BuildBookshelfShell(bookshelfRoot, bookshelfSections, true, wood);

            state.ResetSections(bookshelfSections);
            PopulateBookshelfVariant(activeBookshelfIndex);
            SetBookshelfAlpha(1f);
            BuildHamhamDisplay();
        }

        private void BuildBookshelfShell(Transform shellRoot, List<ShelfSection> sections, bool includeFocusHitArea, Material wood)
        {
            BuildBookshelfWallCassette(shellRoot);

            CreateBox("LeftPanel", shellRoot, new Vector3(-width * 0.5f + boardThickness * 0.5f, height * 0.5f, 0f), new Vector3(boardThickness, height, depth), wood);
            CreateBox("RightPanel", shellRoot, new Vector3(width * 0.5f - boardThickness * 0.5f, height * 0.5f, 0f), new Vector3(boardThickness, height, depth), wood);
            CreateBox("BottomBoard", shellRoot, new Vector3(0f, boardThickness * 0.5f, 0f), new Vector3(width, boardThickness, depth), wood);
            CreateBox("TopBoard", shellRoot, new Vector3(0f, height - boardThickness * 0.5f, 0f), new Vector3(width, boardThickness, depth), wood);
            CreateBox("BackPanel", shellRoot, new Vector3(0f, height * 0.5f, depth * 0.5f - backPanelThickness * 0.5f), new Vector3(width, height, backPanelThickness), wood);
            if (includeFocusHitArea)
            {
                CreateBookshelfFocusHitArea(shellRoot);
            }

            var usableWidth = width - boardThickness * 2f;
            var usableHeight = height - boardThickness * 2f;
            var sectionWidth = (usableWidth - dividerThickness * (columns - 1)) / columns;
            var sectionHeight = (usableHeight - boardThickness * (rows - 1)) / rows;
            var innerFrontZ = -depth * 0.5f;
            var innerBackZ = depth * 0.5f - backPanelThickness;
            var sectionCenterZ = (innerFrontZ + innerBackZ) * 0.5f;

            for (var row = 1; row < rows; row++)
            {
                var y = boardThickness + row * sectionHeight + (row - 0.5f) * boardThickness;
                CreateBox($"Shelf_{row}", shellRoot, new Vector3(0f, y, 0f), new Vector3(width - boardThickness * 2f, boardThickness, depth), wood);
            }

            for (var column = 1; column < columns; column++)
            {
                var x = -usableWidth * 0.5f + column * sectionWidth + (column - 0.5f) * dividerThickness;
                CreateBox($"Divider_{column}", shellRoot, new Vector3(x, height * 0.5f, 0f), new Vector3(dividerThickness, height - boardThickness * 2f, depth), wood);
            }

            sections.Clear();
            for (var row = 0; row < rows; row++)
            {
                var bottomY = boardThickness + row * (sectionHeight + boardThickness);
                var topY = bottomY + sectionHeight;
                for (var column = 0; column < columns; column++)
                {
                    var left = -usableWidth * 0.5f + column * (sectionWidth + dividerThickness);
                    var right = left + sectionWidth;
                    var innerLeft = left + SectionWallClearance;
                    var innerRight = right - SectionWallClearance;
                    if (innerRight <= innerLeft)
                    {
                        innerLeft = left;
                        innerRight = right;
                    }
                    sections.Add(new ShelfSection(row, column, innerLeft, innerRight, bottomY, topY - bottomY, sectionCenterZ, innerFrontZ, innerBackZ));
                }
            }
        }

        private void ConfigureBookshelfTurntable()
        {
            bookshelfTurntableRoot.localPosition = new Vector3(0f, 0f, BookshelfTurntableZ);
            bookshelfTurntableRoot.localRotation = Quaternion.identity;
            turntableRootBaseScale = bookshelfTurntableRoot.localScale;
        }

        private void BuildBookshelfWallCassette(Transform shellRoot)
        {
            if (shellRoot == null)
            {
                return;
            }

            var wallMaterial = materialFactory.GetWallMaterial();
            var frameWidth = GetBookshelfWallFrameWidth();
            var frameHeight = GetBookshelfWallFrameHeight();
            var sideReveal = Mathf.Max(0.01f, RotatingWallFrameSideReveal);
            var topReveal = Mathf.Max(0.01f, RotatingWallFrameTopReveal);
            var wallFrontSurfaceZ = GetBackWallFrontSurfaceLocalZ();
            var returnDepth = Mathf.Max(0.01f, RotatingWallFrameReturnDepth);
            var returnCenterZ = wallFrontSurfaceZ + returnDepth * 0.5f;

            CreateBox(
                "RotatingWallFrame_LeftReturn",
                shellRoot,
                new Vector3(-width * 0.5f - sideReveal * 0.5f, frameHeight * 0.5f, returnCenterZ),
                new Vector3(sideReveal, frameHeight, returnDepth),
                wallMaterial);
            CreateBox(
                "RotatingWallFrame_RightReturn",
                shellRoot,
                new Vector3(width * 0.5f + sideReveal * 0.5f, frameHeight * 0.5f, returnCenterZ),
                new Vector3(sideReveal, frameHeight, returnDepth),
                wallMaterial);
            CreateBox(
                "RotatingWallFrame_TopReturn",
                shellRoot,
                new Vector3(0f, height + topReveal * 0.5f, returnCenterZ),
                new Vector3(frameWidth, topReveal, returnDepth),
                wallMaterial);
        }

        private float GetBookshelfAlcoveWidth()
        {
            return GetBookshelfWallFrameWidth() + BookshelfAlcoveStaticClearance;
        }

        private float GetBookshelfAlcoveHeight()
        {
            return GetBookshelfWallFrameHeight() + BookshelfAlcoveStaticClearance;
        }

        private float GetBookshelfWallFrameWidth()
        {
            return width + RotatingWallFrameSideReveal * 2f;
        }

        private float GetBookshelfWallFrameHeight()
        {
            return height + RotatingWallFrameTopReveal;
        }

        private static float GetBackWallFrontSurfaceLocalZ()
        {
            return BackWallZ - BackWallThickness * 0.5f - BookshelfAnchorZ;
        }

        private static float GetBackWallBackSurfaceZ()
        {
            return BackWallZ + BackWallThickness * 0.5f;
        }

        private static Vector3 GetBookshelfFrontSlotPosition()
        {
            return new Vector3(0f, 0f, -BookshelfTurntableArmLength);
        }

        private static Vector3 GetBookshelfBackSlotPosition()
        {
            return new Vector3(0f, 0f, BookshelfTurntableArmLength);
        }

        private void ResetBookshelfToFrontSlot()
        {
            bookshelfTurntableRoot.localRotation = Quaternion.identity;
            bookshelfTurntableRoot.localScale = turntableRootBaseScale;
            bookshelfRoot.localPosition = GetBookshelfFrontSlotPosition();
            bookshelfRoot.localRotation = Quaternion.identity;
            bookshelfRoot.localScale = bookshelfRootBaseScale;
        }

        private void PopulateBookshelfVariant(int variantIndex)
        {
            ClearBookshelfBooks();
            SpawnBooks(bookshelfSections, variantIndex, booksRoot, true);
            RestoreBookshelfLayoutSnapshot(variantIndex, bookshelfSections, booksRoot);
            if (state != null)
            {
                state.LayoutAll(state, false);
            }
        }

        private void SpawnBooks(List<ShelfSection> sections, int variantIndex, Transform targetBooksRoot, bool registerInState)
        {
            EnsureBookCatalog();
            if (bookCatalogItems.Count == 0)
            {
                Debug.LogWarning("Bookshelf catalog is empty; skipping book generation so stale mock/default book data is not shown.");
                return;
            }

            Random.InitState(randomSeed + variantIndex * 137);
            var prefabs = GetQaBookPrefabs();

            for (var i = 0; i < sections.Count; i++)
            {
                var section = sections[i];
                for (var j = 0; j < booksPerSection; j++)
                {
                    BookEntity book;
                    if (prefabs.Count > 0)
                    {
                        var prefabIndex = (variantIndex * 11 + i * booksPerSection + j) % prefabs.Count;
                        book = CreatePrefabBook(prefabs[prefabIndex], variantIndex, i, j, targetBooksRoot);
                    }
                    else
                    {
                        book = CreateProceduralBook(variantIndex, i, j, targetBooksRoot);
                    }

                    ApplyBookCatalogData(book, GetBookCatalogItem(variantIndex, i, j, sections.Count), variantIndex, i, j);
                    if (registerInState)
                    {
                        state.AddBookToSection(book, section);
                    }
                    else
                    {
                        section.books.Add(book);
                        book.SetSection(section);
                    }
                }
            }
        }

        private void LoadBookCatalog()
        {
            bookCatalogItems = loadBooksUseCase != null
                ? loadBooksUseCase.Execute() ?? System.Array.Empty<BookCatalogItem>()
                : System.Array.Empty<BookCatalogItem>();

            if (Application.isPlaying)
            {
                Debug.Log($"Bookshelf catalog loaded: {bookCatalogItems.Count} item(s).");
            }
        }

        private void EnsureBookCatalog()
        {
            if (bookCatalogItems.Count > 0)
            {
                return;
            }

            LoadBookCatalog();
        }

        private BookCatalogItem GetBookCatalogItem(int variantIndex, int sectionIndex, int bookIndex, int sectionCount)
        {
            if (bookCatalogItems.Count == 0)
            {
                return null;
            }

            var catalogIndex = variantIndex * sectionCount * booksPerSection + sectionIndex * booksPerSection + bookIndex;
            return bookCatalogItems[catalogIndex % bookCatalogItems.Count];
        }

        private void ApplyBookCatalogData(BookEntity book, BookCatalogItem item, int variantIndex, int sectionIndex, int bookIndex)
        {
            if (book == null || item == null)
            {
                return;
            }

            book.ApplyMetadata(item.Book, item.UserBook, item.ReadingRecord);
            if (item.Book != null && !string.IsNullOrWhiteSpace(item.Book.BookId))
            {
                book.gameObject.name = $"Shelf{variantIndex}_{item.Book.BookId}_{sectionIndex}_{bookIndex}";
            }

            RefreshBookCoverVisuals(book);
        }

        private void RefreshBookCoverVisuals(BookEntity book)
        {
            if (book == null)
            {
                return;
            }

            RemoveBookCoverLabels(book);
            var renderer = book.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                return;
            }

            var coverColor = ResolveBookCoverColor(book);
            ApplySolidBookCoverMaterial(book, coverColor, renderer);
            RebuildBookCoverLabels(book, coverColor);
        }

        private void RefreshExistingBookCoverVisuals()
        {
            if (booksRoot == null)
            {
                booksRoot = transform.Find("BookshelfTurntableRoot/BookshelfRoot/BooksRoot");
            }

            if (booksRoot == null)
            {
                booksRoot = transform.Find("BookshelfRoot/BooksRoot");
            }

            if (booksRoot == null)
            {
                return;
            }

            var books = booksRoot.GetComponentsInChildren<BookEntity>(true);
            for (var i = 0; i < books.Length; i++)
            {
                RefreshBookCoverVisuals(books[i]);
            }
        }

        private void ApplySolidBookCoverMaterial(BookEntity book, Color coverColor, MeshRenderer renderer)
        {
            if (book == null || renderer == null)
            {
                return;
            }

            book.tintRenderers = new[] { renderer };
            renderer.sharedMaterial = CreateBookCoverMaterial(coverColor);
        }

        private void RemoveBookCoverLabels(BookEntity book)
        {
            var existingLabels = book.transform.Find(BookCoverLabelsName);
            if (existingLabels != null)
            {
                DestroySpawnedObject(existingLabels.gameObject);
            }
        }

        private void RebuildBookCoverLabels(BookEntity book, Color coverColor)
        {
            if (book.isDecoration)
            {
                return;
            }

            var labelsRoot = new GameObject(BookCoverLabelsName);
            labelsRoot.transform.SetParent(book.transform, false);
            labelsRoot.transform.localPosition = Vector3.zero;
            labelsRoot.transform.localRotation = Quaternion.identity;
            labelsRoot.transform.localScale = Vector3.one;

            var textColor = GetCoverTextColor(coverColor);
            CreateBookCoverCanvas(
                labelsRoot.transform,
                "FrontCoverCanvas",
                BookLabelKind.Cover,
                FormatFrontCoverText(book),
                new Vector3(BookCoverSurfaceOffset, 0.02f, 0f),
                Quaternion.Euler(0f, -90f, 0f),
                18,
                textColor);
            CreateBookCoverCanvas(
                labelsRoot.transform,
                "BackCoverCanvas",
                BookLabelKind.BackCover,
                FormatBackCoverText(book),
                new Vector3(-BookCoverSurfaceOffset, 0f, 0f),
                Quaternion.Euler(0f, 90f, 0f),
                13,
                textColor);
            CreateBookCoverCanvas(
                labelsRoot.transform,
                "SpineTitleCanvas",
                BookLabelKind.Spine,
                FormatSpineTitleText(book),
                new Vector3(0f, 0.02f, -BookCoverSurfaceOffset),
                Quaternion.Euler(0f, 180f, 0f),
                BookSpineTitleFontSize,
                textColor);
        }

        private static void CreateBookCoverCanvas(
            Transform parent,
            string name,
            BookLabelKind kind,
            string text,
            Vector3 localPosition,
            Quaternion localRotation,
            int fontSize,
            Color color)
        {
            var isSpineTitle = kind == BookLabelKind.Spine;
            var labelObject = new GameObject(name, typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = localPosition;
            labelObject.transform.localRotation = localRotation;
            var canvasScale = isSpineTitle ? GetBookSpineCanvasScale(parent) : GetBookCoverCanvasScale();
            if (isSpineTitle)
            {
                canvasScale.x *= -1f;
            }

            labelObject.transform.localScale = canvasScale;

            var rect = labelObject.GetComponent<RectTransform>();
            rect.sizeDelta = isSpineTitle ? BookSpineCanvasSize : BookCoverCanvasSize;
            rect.pivot = new Vector2(0.5f, 0.5f);

            var canvas = labelObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 20;

            var textObject = new GameObject("Label", typeof(RectTransform));
            textObject.transform.SetParent(labelObject.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            var padding = isSpineTitle ? BookSpineCanvasPadding : BookCoverCanvasPadding;
            textRect.offsetMin = new Vector2(padding, padding);
            textRect.offsetMax = new Vector2(-padding, -padding);
            textRect.anchoredPosition = Vector2.zero;
            textRect.localScale = Vector3.one;

            var label = textObject.AddComponent<Text>();
            var font = RuntimeUiFont.Resolve();
            if (font != null)
            {
                label.font = font;
            }

            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = isSpineTitle ? HorizontalWrapMode.Overflow : HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = !isSpineTitle;
            label.resizeTextMinSize = isSpineTitle ? fontSize : Mathf.Max(8, fontSize - 6);
            label.resizeTextMaxSize = fontSize;
            label.lineSpacing = isSpineTitle ? 0.76f : 0.9f;
            label.fontStyle = isSpineTitle ? FontStyle.Bold : FontStyle.Normal;
            label.color = color;
            label.supportRichText = false;
            label.raycastTarget = false;
        }

        private static Vector3 GetBookCoverCanvasScale()
        {
            return new Vector3(
                BookCoverCanvasWidthCoverage / BookCoverCanvasSize.x,
                BookCoverCanvasHeightCoverage / BookCoverCanvasSize.y,
                1f);
        }

        private static Vector3 GetBookSpineCanvasScale(Transform labelsRoot)
        {
            var parentScale = labelsRoot != null ? labelsRoot.lossyScale : Vector3.one;
            var sourceBook = labelsRoot != null ? labelsRoot.GetComponentInParent<BookEntity>() : null;
            var worldHeight = Mathf.Max(
                0.08f,
                (sourceBook != null ? sourceBook.height : 0.24f) * BookSpineCanvasHeightCoverage);
            var worldUnitsPerPixel = worldHeight / BookSpineCanvasSize.y;

            return new Vector3(
                worldUnitsPerPixel / Mathf.Max(Mathf.Abs(parentScale.x), 0.0001f),
                worldUnitsPerPixel / Mathf.Max(Mathf.Abs(parentScale.y), 0.0001f),
                1f);
        }

        private Material CreateBookCoverMaterial(Color coverColor)
        {
            if (materialFactory != null)
            {
                return materialFactory.CreateBookMaterial(coverColor);
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader) { name = "BookDynamicFallback" };
            material.color = coverColor;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", coverColor);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.3f);
            }

            return material;
        }

        private static Color ResolveBookCoverColor(BookEntity book)
        {
            var seed = !string.IsNullOrWhiteSpace(book.bookId)
                ? book.bookId
                : !string.IsNullOrWhiteSpace(book.title)
                    ? book.title
                    : book.gameObject.name;
            return ResolveBookCoverColorFromSeed(seed);
        }

        private static int GetStableHash(string value)
        {
            unchecked
            {
                var hash = 23;
                if (!string.IsNullOrEmpty(value))
                {
                    for (var i = 0; i < value.Length; i++)
                    {
                        hash = hash * 31 + value[i];
                    }
                }

                return Mathf.Abs(hash == int.MinValue ? int.MaxValue : hash);
            }
        }

        private static Color GetCoverTextColor(Color coverColor)
        {
            var luminance = coverColor.r * 0.299f + coverColor.g * 0.587f + coverColor.b * 0.114f;
            return luminance > 0.57f ? new Color(0.08f, 0.075f, 0.07f) : new Color(0.98f, 0.965f, 0.93f);
        }

        private static string FormatFrontCoverText(BookEntity book)
        {
            var title = string.IsNullOrWhiteSpace(book.title) ? "새 책" : book.title.Trim();
            var author = string.IsNullOrWhiteSpace(book.author) ? "저자 미상" : book.author.Trim();
            return $"{WrapCoverText(title, BookCoverFrontTitleLineLength, 3)}\n\n{WrapCoverText(author, BookCoverFrontAuthorLineLength, 1)}";
        }

        private static string FormatBackCoverText(BookEntity book)
        {
            var description = string.IsNullOrWhiteSpace(book.description)
                ? "책 설명이 없습니다."
                : book.description.Trim();
            return WrapCoverText(description, BookCoverBackDescriptionLineLength, 6);
        }

        private static string FormatSpineTitleText(BookEntity book)
        {
            var title = string.IsNullOrWhiteSpace(book.title) ? "새 책" : book.title.Trim();
            return StackCoverTextVertically(TruncateCoverText(title, BookSpineTitleCharacterLimit));
        }

        private static string StackCoverTextVertically(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var hasEllipsis = text.EndsWith("...");
            var body = hasEllipsis ? text.Substring(0, text.Length - 3) : text;
            var result = string.Empty;
            for (var i = 0; i < body.Length; i++)
            {
                if (i > 0)
                {
                    result += "\n";
                }

                result += body[i];
            }

            if (hasEllipsis)
            {
                if (!string.IsNullOrEmpty(result))
                {
                    result += "\n";
                }

                result += "...";
            }

            return result;
        }

        private static string TruncateCoverText(string text, int maxCharacters)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var normalized = text.Trim();
            return normalized.Length <= maxCharacters
                ? normalized
                : $"{normalized.Substring(0, maxCharacters)}...";
        }

        private static string WrapCoverText(string text, int maxCharactersPerLine, int maxLines)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var normalized = text.Trim();
            var lines = new List<string>();
            var cursor = 0;
            while (cursor < normalized.Length && lines.Count < maxLines)
            {
                var length = Mathf.Min(maxCharactersPerLine, normalized.Length - cursor);
                var line = normalized.Substring(cursor, length).Trim();
                cursor += length;

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (cursor < normalized.Length && lines.Count == maxLines - 1)
                {
                    line = $"{line.TrimEnd()}...";
                    lines.Add(line);
                    break;
                }

                lines.Add(line);
            }

            return string.Join("\n", lines);
        }

        private BookEntity CreateProceduralBook(int variantIndex, int sectionIndex, int bookIndex, Transform targetBooksRoot)
        {
            var bookObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bookObject.name = $"Shelf{variantIndex}_Book_{sectionIndex}_{bookIndex}";
            bookObject.transform.SetParent(targetBooksRoot, false);

            var thickness = Random.Range(thicknessRange.x, thicknessRange.y);
            var bookHeight = Random.Range(heightRange.x, heightRange.y);
            bookObject.transform.localScale = new Vector3(thickness, bookHeight, bookDepth);

            var renderer = bookObject.GetComponent<MeshRenderer>();
            var coverColor = ResolveBookCoverColorFromSeed(bookObject.name);
            renderer.sharedMaterial = CreateBookCoverMaterial(coverColor);

            var entity = bookObject.AddComponent<BookEntity>();
            entity.tintRenderers = new[] { renderer };
            entity.Initialize(bookObject.name, thickness, bookHeight, bookDepth, coverColor);
            return entity;
        }

        private static Color ResolveBookCoverColorFromSeed(string seed)
        {
            var hash = GetStableHash(seed);
            var hue = (hash % 360) / 360f;
            var saturation = 0.48f + ((hash / 360) % 27) / 100f;
            var value = 0.58f + ((hash / 9720) % 24) / 100f;
            return Color.HSVToRGB(hue, saturation, value);
        }

        private Material GetBookVisualMaterial()
        {
            if (qaBooksMaterial != null)
            {
                return qaBooksMaterial;
            }

#if UNITY_EDITOR
            if (qaBooksBaseMap == null)
            {
                qaBooksBaseMap = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(QABooksBaseTexturePath);
            }

            if (qaBooksNormalMap == null)
            {
                qaBooksNormalMap = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(QABooksNormalTexturePath);
            }

            if (qaBooksMaskMap == null)
            {
                qaBooksMaskMap = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(QABooksMetallicTexturePath);
            }
#endif

            if (qaBooksBaseMap != null)
            {
                qaBooksMaterial = materialFactory.CreateBookMaterial(qaBooksBaseMap, qaBooksNormalMap, Color.white, 0.28f);
                if (qaBooksMaskMap != null)
                {
                    qaBooksMaterial.EnableKeyword("_METALLICSPECGLOSSMAP");
                    qaBooksMaterial.SetTexture("_MetallicGlossMap", qaBooksMaskMap);
                    qaBooksMaterial.SetTexture("_OcclusionMap", qaBooksMaskMap);
                }
                return qaBooksMaterial;
            }

            return materialFactory.CreateBookMaterial(Random.ColorHSV(0f, 1f, 0.45f, 0.9f, 0.45f, 0.95f));
        }

        private Material GetAlternateBookVisualMaterial()
        {
            if (qaBooksAltMaterial != null)
            {
                return qaBooksAltMaterial;
            }

#if UNITY_EDITOR
            if (qaBooksAltBaseMap == null)
            {
                qaBooksAltBaseMap = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(QABooksAltBaseTexturePath);
            }

            if (qaBooksAltNormalMap == null)
            {
                qaBooksAltNormalMap = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(QABooksAltNormalTexturePath);
            }

            if (qaBooksAltMaskMap == null)
            {
                qaBooksAltMaskMap = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(QABooksAltMetallicTexturePath);
            }
#endif

            if (qaBooksAltBaseMap != null)
            {
                qaBooksAltMaterial = materialFactory.CreateBookMaterial(qaBooksAltBaseMap, qaBooksAltNormalMap, Color.white, 0.28f);
                if (qaBooksAltMaskMap != null)
                {
                    qaBooksAltMaterial.EnableKeyword("_METALLICSPECGLOSSMAP");
                    qaBooksAltMaterial.SetTexture("_MetallicGlossMap", qaBooksAltMaskMap);
                    qaBooksAltMaterial.SetTexture("_OcclusionMap", qaBooksAltMaskMap);
                }
                return qaBooksAltMaterial;
            }

            return GetBookVisualMaterial();
        }

        private List<GameObject> GetQaBookPrefabs()
        {
            if (qaBookPrefabs.Count > 0)
            {
                return qaBookPrefabs;
            }

#if UNITY_EDITOR
            var prefabGuids = UnityEditor.AssetDatabase.FindAssets("t:Prefab Book_", new[] { "Assets/QA_Books/Prefabs" });
            for (var i = 0; i < prefabGuids.Length; i++)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (!path.Contains("/Book_"))
                {
                    continue;
                }

                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    qaBookPrefabs.Add(prefab);
                }
            }
#endif

            return qaBookPrefabs;
        }

        private BookEntity CreatePrefabBook(GameObject prefab, int variantIndex, int sectionIndex, int bookIndex, Transform targetBooksRoot)
        {
            var root = new GameObject($"Shelf{variantIndex}_{prefab.name}_{sectionIndex}_{bookIndex}");
            root.transform.SetParent(targetBooksRoot, false);

            var visual = Instantiate(prefab, root.transform);
            visual.name = prefab.name;
            SetDynamicHierarchy(visual.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            visual.transform.localScale = Vector3.one;

            RemoveChildColliders(visual.transform);

            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            ApplyQaBookMaterials(renderers);
            CenterChildVisual(root.transform);

            var localBounds = CalculateLocalBounds(root.transform, root.GetComponentsInChildren<Renderer>());
            var collider = root.AddComponent<BoxCollider>();
            collider.center = localBounds.center;
            collider.size = AbsVector(localBounds.size);

            var entity = root.AddComponent<BookEntity>();
            entity.tintRenderers = renderers;
            entity.Initialize(root.name, Mathf.Abs(localBounds.size.x), Mathf.Abs(localBounds.size.y), Mathf.Abs(localBounds.size.z), Color.white);
            return entity;
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

        private void ApplyQaBookMaterials(MeshRenderer[] renderers)
        {
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || renderers[i].sharedMaterial == null)
                {
                    continue;
                }

                var sourceName = renderers[i].sharedMaterial.name;
                renderers[i].sharedMaterial = sourceName.Contains("BooksA") ? GetBookVisualMaterial() : GetAlternateBookVisualMaterial();
            }
        }

        private static void RemoveChildColliders(Transform root)
        {
            var colliders = root.GetComponentsInChildren<Collider>();
            for (var i = 0; i < colliders.Length; i++)
            {
                DestroyImmediate(colliders[i]);
            }
        }

        private static void CenterChildVisual(Transform root)
        {
            var bounds = CalculateLocalBounds(root, root.GetComponentsInChildren<Renderer>());
            for (var i = 0; i < root.childCount; i++)
            {
                root.GetChild(i).localPosition -= bounds.center;
            }
        }

        private static Bounds CalculateLocalBounds(Transform root, Renderer[] renderers)
        {
            var hasBounds = false;
            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                EncapsulateRendererWorldBounds(root, renderers[i].bounds, ref min, ref max);
                hasBounds = true;
            }

            if (!hasBounds)
            {
                return new Bounds(Vector3.zero, new Vector3(0.04f, 0.22f, 0.16f));
            }

            return new Bounds((min + max) * 0.5f, max - min);
        }

        private static void EncapsulateRendererWorldBounds(Transform root, Bounds worldBounds, ref Vector3 min, ref Vector3 max)
        {
            var worldMin = worldBounds.min;
            var worldMax = worldBounds.max;
            EncapsulateLocalPoint(root, new Vector3(worldMin.x, worldMin.y, worldMin.z), ref min, ref max);
            EncapsulateLocalPoint(root, new Vector3(worldMin.x, worldMin.y, worldMax.z), ref min, ref max);
            EncapsulateLocalPoint(root, new Vector3(worldMin.x, worldMax.y, worldMin.z), ref min, ref max);
            EncapsulateLocalPoint(root, new Vector3(worldMin.x, worldMax.y, worldMax.z), ref min, ref max);
            EncapsulateLocalPoint(root, new Vector3(worldMax.x, worldMin.y, worldMin.z), ref min, ref max);
            EncapsulateLocalPoint(root, new Vector3(worldMax.x, worldMin.y, worldMax.z), ref min, ref max);
            EncapsulateLocalPoint(root, new Vector3(worldMax.x, worldMax.y, worldMin.z), ref min, ref max);
            EncapsulateLocalPoint(root, new Vector3(worldMax.x, worldMax.y, worldMax.z), ref min, ref max);
        }

        private static void EncapsulateLocalPoint(Transform root, Vector3 worldPoint, ref Vector3 min, ref Vector3 max)
        {
            var localPoint = root.InverseTransformPoint(worldPoint);
            min = Vector3.Min(min, localPoint);
            max = Vector3.Max(max, localPoint);
        }

        private static Vector3 AbsVector(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private void CreateBox(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = localScale;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            DestroyImmediate(box.GetComponent<Collider>());
            generatedGeometry.Add(box);
        }

        private void BuildHamhamDisplay()
        {
            var hamhamAsset = ResolveHamhamAsset();
            if (hamhamAsset == null)
            {
                return;
            }

            var hamhamRoot = new GameObject("HamhamDisplay");
            hamhamRoot.transform.SetParent(transform, false);
            var bookshelfLocalPosition = transform.InverseTransformPoint(
                bookshelfTurntableRoot.TransformPoint(GetBookshelfFrontSlotPosition()));
            hamhamRoot.transform.localPosition = bookshelfLocalPosition + new Vector3(width * 0.5f + 0.42f, 0f, -0.05f);
            hamhamRoot.transform.localRotation = Quaternion.Euler(0f, 152f, 0f);
            hamhamRoot.transform.localScale = Vector3.one * 0.42f;
            generatedGeometry.Add(hamhamRoot);

            var instance = Instantiate(hamhamAsset, hamhamRoot.transform);
            instance.name = hamhamAsset.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            AlignModelToFloor(hamhamRoot.transform, instance.transform);
        }

        private GameObject ResolveHamhamAsset()
        {
            if (hamhamModelAsset != null)
            {
                return hamhamModelAsset;
            }

#if UNITY_EDITOR
            hamhamModelAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(HamhamAssetPath);
#endif

            return hamhamModelAsset;
        }

        private static void AlignModelToFloor(Transform root, Transform modelRoot)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            var minLocal = root.InverseTransformPoint(bounds.min);
            modelRoot.localPosition -= new Vector3(0f, minLocal.y, 0f);
        }

        public bool ShowPreviousBookshelf()
        {
            return BeginBookshelfSwitch(-1);
        }

        public bool ShowNextBookshelf()
        {
            return BeginBookshelfSwitch(1);
        }

        public bool ShowBookshelf(int index)
        {
            if (IsBookshelfSwitching ||
                booksRoot == null ||
                bookshelfSections.Count == 0 ||
                index < 0 ||
                index >= bookshelfCount)
            {
                return false;
            }

            if (index == activeBookshelfIndex)
            {
                return true;
            }

            SaveActiveBookshelfLayoutSnapshot();
            activeBookshelfIndex = index;
            pendingBookshelfIndex = -1;
            queuedBookshelfSwitchDirection = 0;
            RebuildActiveBookshelfVariant(activeBookshelfIndex);
            BookshelfSwitchStateChanged?.Invoke();
            return true;
        }

        private bool BeginBookshelfSwitch(int direction)
        {
            if (direction == 0 ||
                booksRoot == null ||
                bookshelfSections.Count == 0 ||
                bookshelfCount <= 1)
            {
                return false;
            }

            var normalizedDirection = direction > 0 ? 1 : -1;
            if (IsBookshelfSwitching)
            {
                queuedBookshelfSwitchDirection = normalizedDirection;
                BookshelfSwitchStateChanged?.Invoke();
                return true;
            }

            SaveActiveBookshelfLayoutSnapshot();
            return StartBookshelfSwitch(normalizedDirection);
        }

        public bool AddBookshelf()
        {
            if (IsBookshelfSwitching ||
                booksRoot == null ||
                bookshelfSections.Count == 0)
            {
                return false;
            }

            SaveActiveBookshelfLayoutSnapshot();
            var newIndex = bookshelfCount;
            EnsureBookshelfDataForCurrentOrder();
            bookshelfCount++;
            bookshelfLayoutSnapshots[newIndex] = CreateEmptyBookshelfLayoutSnapshot();
            bookshelfNames[newIndex] = GetDefaultBookshelfName(newIndex);
            activeBookshelfIndex = newIndex;
            pendingBookshelfIndex = -1;
            RebuildActiveBookshelfVariant(activeBookshelfIndex);
            BookshelfSwitchStateChanged?.Invoke();
            return true;
        }

        public bool DeleteActiveBookshelf()
        {
            if (IsBookshelfSwitching ||
                booksRoot == null ||
                bookshelfSections.Count == 0 ||
                bookshelfCount <= 1)
            {
                return false;
            }

            var deletedIndex = GetWrappedBookshelfIndex(activeBookshelfIndex);
            SaveActiveBookshelfLayoutSnapshot();
            EnsureBookshelfDataForCurrentOrder();
            var targetIndex = deletedIndex >= bookshelfCount - 1 ? deletedIndex - 1 : deletedIndex + 1;
            MergeBookshelfSnapshots(deletedIndex, targetIndex);
            RemoveBookshelfDataAt(deletedIndex);
            bookshelfCount--;
            activeBookshelfIndex = Mathf.Clamp(deletedIndex, 0, bookshelfCount - 1);
            pendingBookshelfIndex = -1;
            RebuildActiveBookshelfVariant(activeBookshelfIndex);
            BookshelfSwitchStateChanged?.Invoke();
            return true;
        }

        public BookshelfThemeStyle GetBookshelfDesignStyle(int index)
        {
            var wrapped = GetWrappedBookshelfIndex(index);
            return bookshelfDesignStyles.TryGetValue(wrapped, out var style)
                ? style
                : BookshelfThemeStyle.Default;
        }

        public bool SetActiveBookshelfDesignStyle(BookshelfThemeStyle style)
        {
            if (IsBookshelfSwitching ||
                booksRoot == null ||
                bookshelfSections.Count == 0)
            {
                return false;
            }

            var index = GetWrappedBookshelfIndex(activeBookshelfIndex);
            if (GetBookshelfDesignStyle(index) == style)
            {
                return true;
            }

            SaveActiveBookshelfLayoutSnapshot();
            if (style == BookshelfThemeStyle.Default)
            {
                bookshelfDesignStyles.Remove(index);
            }
            else
            {
                bookshelfDesignStyles[index] = style;
            }

            RebuildActiveBookshelfVariant(index);
            BookshelfSwitchStateChanged?.Invoke();
            return true;
        }

        public VisibilityScope GetBookshelfVisibility(int index)
        {
            var wrapped = GetWrappedBookshelfIndex(index);
            return bookshelfVisibilityScopes.TryGetValue(wrapped, out var visibility)
                ? visibility
                : VisibilityScope.Public;
        }

        public VisibilityScope CycleActiveBookshelfVisibility()
        {
            var index = GetWrappedBookshelfIndex(activeBookshelfIndex);
            var next = GetBookshelfVisibility(index) switch
            {
                VisibilityScope.Public => VisibilityScope.Followers,
                VisibilityScope.Followers => VisibilityScope.Private,
                _ => VisibilityScope.Public
            };
            SetBookshelfVisibility(index, next);
            return next;
        }

        public void SetBookshelfVisibility(int index, VisibilityScope visibility)
        {
            var wrapped = GetWrappedBookshelfIndex(index);
            if (visibility == VisibilityScope.Public)
            {
                bookshelfVisibilityScopes.Remove(wrapped);
            }
            else
            {
                bookshelfVisibilityScopes[wrapped] = visibility;
            }

            BookshelfSwitchStateChanged?.Invoke();
        }

        public bool ActiveBookshelfContainsBook(string title, string author)
        {
            var normalizedTitle = NormalizeBookIdentity(title);
            if (string.IsNullOrEmpty(normalizedTitle) || state == null)
            {
                return false;
            }

            var normalizedAuthor = NormalizeBookIdentity(author);
            for (var i = 0; i < state.Sections.Count; i++)
            {
                var section = state.Sections[i];
                if (section == null)
                {
                    continue;
                }

                for (var j = 0; j < section.books.Count; j++)
                {
                    var book = section.books[j];
                    if (book == null || book.isDecoration)
                    {
                        continue;
                    }

                    if (NormalizeBookIdentity(book.title) != normalizedTitle)
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(normalizedAuthor) ||
                        string.IsNullOrEmpty(NormalizeBookIdentity(book.author)) ||
                        NormalizeBookIdentity(book.author) == normalizedAuthor)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool MoveActiveBookshelfOrder(int direction)
        {
            if (direction == 0 ||
                IsBookshelfSwitching ||
                bookshelfCount <= 1 ||
                booksRoot == null ||
                bookshelfSections.Count == 0)
            {
                return false;
            }

            SaveActiveBookshelfLayoutSnapshot();
            EnsureBookshelfDataForCurrentOrder();
            var source = GetWrappedBookshelfIndex(activeBookshelfIndex);
            var target = source + (direction > 0 ? 1 : -1);
            if (target < 0 || target >= bookshelfCount)
            {
                return false;
            }

            SwapBookshelfData(source, target);
            activeBookshelfIndex = target;
            pendingBookshelfIndex = -1;
            RebuildActiveBookshelfVariant(activeBookshelfIndex);
            BookshelfSwitchStateChanged?.Invoke();
            return true;
        }

        public bool MoveBookshelfOrder(int sourceIndex, int targetIndex, bool selectMovedBookshelf = true)
        {
            if (IsBookshelfSwitching ||
                bookshelfCount <= 1 ||
                booksRoot == null ||
                bookshelfSections.Count == 0 ||
                sourceIndex < 0 ||
                targetIndex < 0 ||
                sourceIndex >= bookshelfCount ||
                targetIndex >= bookshelfCount ||
                sourceIndex == targetIndex)
            {
                return false;
            }

            SaveActiveBookshelfLayoutSnapshot();
            EnsureBookshelfDataForCurrentOrder();

            var previousActiveIndex = activeBookshelfIndex;
            MoveBookshelfData(sourceIndex, targetIndex);
            activeBookshelfIndex = selectMovedBookshelf
                ? targetIndex
                : ResolveActiveIndexAfterMove(previousActiveIndex, sourceIndex, targetIndex);
            pendingBookshelfIndex = -1;
            queuedBookshelfSwitchDirection = 0;
            RebuildActiveBookshelfVariant(activeBookshelfIndex);
            BookshelfSwitchStateChanged?.Invoke();
            return true;
        }

        public BookEntity AddBookToActiveBookshelf(
            string title,
            string author,
            string publisher,
            string description,
            BookOrientation orientation = BookOrientation.Spine,
            bool isDecoration = false)
        {
            if (IsBookshelfSwitching ||
                state == null ||
                booksRoot == null ||
                bookshelfSections.Count == 0)
            {
                return null;
            }

            var book = CreateConfiguredUserBook(title, author, publisher, description, orientation, isDecoration);
            if (book == null)
            {
                return null;
            }

            var targetSection = FindFirstSectionWithSpace(book);
            if (targetSection == null)
            {
                DestroySpawnedObject(book.gameObject);
                return null;
            }

            return TryPlaceConfiguredBookInSection(book, targetSection, targetSection.books.Count, true);
        }

        public BookEntity AddBookToActiveBookshelfAtPointer(
            Camera sceneCamera,
            Vector2 pointer,
            string title,
            string author,
            string publisher,
            string description,
            BookOrientation orientation = BookOrientation.Spine,
            bool isDecoration = false)
        {
            if (IsBookshelfSwitching ||
                state == null ||
                sceneCamera == null ||
                booksRoot == null ||
                bookshelfSections.Count == 0)
            {
                return null;
            }

            var book = CreateConfiguredUserBook(title, author, publisher, description, orientation, isDecoration);
            if (book == null)
            {
                return null;
            }

            var bookColliders = book.GetComponentsInChildren<Collider>(true);
            SetCollidersEnabled(bookColliders, false);
            var hasPlacement = TryResolveActiveShelfPlacement(sceneCamera, pointer, book, out var targetSection, out var insertIndex);
            SetCollidersEnabled(bookColliders, true);
            if (!hasPlacement)
            {
                DestroySpawnedObject(book.gameObject);
                return null;
            }

            return TryPlaceConfiguredBookInSection(book, targetSection, insertIndex, true);
        }

        public bool ShowBookAddPlacementGuide(Camera sceneCamera, Vector2 pointer, BookOrientation orientation)
        {
            if (IsBookshelfSwitching ||
                state == null ||
                sceneCamera == null ||
                booksRoot == null ||
                bookshelfSections.Count == 0)
            {
                HideBookAddPlacementGuide();
                return false;
            }

            var candidateBook = EnsureBookAddPlacementGuideCandidate(orientation);
            if (candidateBook == null ||
                !TryResolveActiveShelfPlacement(sceneCamera, pointer, candidateBook, out var targetSection, out var insertIndex) ||
                !state.TryGetBookPreviewTransform(candidateBook, targetSection, insertIndex, out var localPosition, out var localRotation, out var localScale))
            {
                HideBookAddPlacementGuide();
                return false;
            }

            EnsureBookAddPlacementGuideVisuals();
            if (addBookPlacementGuideLine == null || addBookPlacementGuideGhost == null)
            {
                HideBookAddPlacementGuide();
                return false;
            }

            addBookPlacementGuideGhost.SetActive(true);
            addBookPlacementGuideGhost.transform.SetParent(booksRoot, false);
            addBookPlacementGuideGhost.transform.localPosition = localPosition;
            addBookPlacementGuideGhost.transform.localRotation = localRotation;
            addBookPlacementGuideGhost.transform.localScale = localScale;

            addBookPlacementGuideLine.SetActive(true);
            addBookPlacementGuideLine.transform.SetParent(booksRoot, false);
            addBookPlacementGuideLine.transform.localPosition = new Vector3(
                localPosition.x,
                targetSection.shelfTopY + targetSection.sectionHeight * 0.5f,
                targetSection.frontBoundZ - 0.012f);
            addBookPlacementGuideLine.transform.localRotation = Quaternion.identity;
            addBookPlacementGuideLine.transform.localScale = new Vector3(
                Mathf.Max(0.008f, candidateBook.thickness * 0.28f),
                Mathf.Max(0.04f, targetSection.sectionHeight - 0.035f),
                0.01f);
            return true;
        }

        public void HideBookAddPlacementGuide()
        {
            if (addBookPlacementGuideLine != null)
            {
                addBookPlacementGuideLine.SetActive(false);
            }

            if (addBookPlacementGuideGhost != null)
            {
                addBookPlacementGuideGhost.SetActive(false);
            }
        }

        private BookEntity EnsureBookAddPlacementGuideCandidate(BookOrientation orientation)
        {
            if (booksRoot == null)
            {
                return null;
            }

            if (addBookPlacementGuideCandidate == null)
            {
                var candidateObject = new GameObject("BookAddPlacementGuideCandidate");
                candidateObject.transform.SetParent(booksRoot, false);
                candidateObject.transform.localScale = ResolveBookAddGuideCandidateScale();
                candidateObject.AddComponent<BoxCollider>();
                addBookPlacementGuideCandidate = candidateObject.AddComponent<BookEntity>();
                addBookPlacementGuideCandidate.Initialize(
                    "BookAddPlacementGuideCandidate",
                    candidateObject.transform.localScale.x,
                    candidateObject.transform.localScale.y,
                    candidateObject.transform.localScale.z,
                    Color.white);
                candidateObject.SetActive(false);
            }

            addBookPlacementGuideCandidate.SetOrientation(orientation);
            return addBookPlacementGuideCandidate;
        }

        private Vector3 ResolveBookAddGuideCandidateScale()
        {
            var thickness = Mathf.Clamp((thicknessRange.x + thicknessRange.y) * 0.5f, 0.02f, 0.08f);
            var bookHeight = Mathf.Clamp((heightRange.x + heightRange.y) * 0.5f, 0.12f, 0.34f);
            return new Vector3(thickness, bookHeight, Mathf.Max(0.04f, bookDepth));
        }

        private void EnsureBookAddPlacementGuideVisuals()
        {
            if (addBookPlacementGuideGhost == null)
            {
                addBookPlacementGuideGhost = CreateBookAddPlacementGuideCube(
                    "BookAddPlacementGhost",
                    GetBookAddPlacementGuideGhostMaterial());
            }

            if (addBookPlacementGuideLine == null)
            {
                addBookPlacementGuideLine = CreateBookAddPlacementGuideCube(
                    "BookAddPlacementGuideLine",
                    GetBookAddPlacementGuideLineMaterial());
            }
        }

        private GameObject CreateBookAddPlacementGuideCube(string objectName, Material material)
        {
            var guide = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guide.name = objectName;
            guide.transform.SetParent(booksRoot != null ? booksRoot : transform, false);
            var collider = guide.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = guide.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            guide.SetActive(false);
            return guide;
        }

        private Material GetBookAddPlacementGuideLineMaterial()
        {
            if (addBookPlacementGuideLineMaterial == null)
            {
                addBookPlacementGuideLineMaterial = CreateBookAddPlacementGuideMaterial("BookAddPlacementGuideLine", addBookPlacementGuideColor);
            }

            return addBookPlacementGuideLineMaterial;
        }

        private Material GetBookAddPlacementGuideGhostMaterial()
        {
            if (addBookPlacementGuideGhostMaterial == null)
            {
                addBookPlacementGuideGhostMaterial = CreateBookAddPlacementGuideMaterial("BookAddPlacementGhost", addBookPlacementGhostColor);
            }

            return addBookPlacementGuideGhostMaterial;
        }

        private static Material CreateBookAddPlacementGuideMaterial(string materialName, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader) { name = materialName };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            material.color = color;
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.18f);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return material;
        }

        private BookEntity CreateConfiguredUserBook(
            string title,
            string author,
            string publisher,
            string description,
            BookOrientation orientation,
            bool isDecoration)
        {
            if (booksRoot == null)
            {
                return null;
            }

            var book = CreateProceduralBook(activeBookshelfIndex, 0, runtimeBookSerial++, booksRoot);
            book.gameObject.name = $"Shelf{activeBookshelfIndex}_UserBook_{runtimeBookSerial:0000}";
            book.bookId = book.gameObject.name;
            book.title = string.IsNullOrWhiteSpace(title) ? (isDecoration ? "꾸미기 오브젝트" : "새 책") : title.Trim();
            book.author = string.IsNullOrWhiteSpace(author) ? (isDecoration ? "dotshelf" : "저자 미상") : author.Trim();
            book.publisher = string.IsNullOrWhiteSpace(publisher) ? "출판사 미상" : publisher.Trim();
            book.description = string.IsNullOrWhiteSpace(description)
                ? (isDecoration ? "책장 안에 배치한 꾸미기 오브젝트입니다." : "책 설명이 없습니다.")
                : description.Trim();
            book.memoPreview = book.description;
            book.isDecoration = isDecoration;
            book.SetOrientation(orientation);
            RefreshBookCoverVisuals(book);
            return book;
        }

        private BookEntity TryPlaceConfiguredBookInSection(BookEntity book, ShelfSection targetSection, int insertIndex, bool animateInsertion)
        {
            if (book == null || targetSection == null)
            {
                if (book != null)
                {
                    DestroySpawnedObject(book.gameObject);
                }

                return null;
            }

            if (!state.TryPlaceBookForDrop(book, targetSection, insertIndex, out var targetLocalPosition, out var targetLocalRotation, out var targetLocalScale))
            {
                DestroySpawnedObject(book.gameObject);
                return null;
            }

            if (animateInsertion && book.gameObject.activeInHierarchy)
            {
                state.LayoutSectionExcept(targetSection, state, true, book);
                StartCoroutine(AnimateAddedBookInsertion(book, targetLocalPosition, targetLocalRotation, targetLocalScale));
            }
            else
            {
                book.transform.localPosition = targetLocalPosition;
                book.transform.localRotation = targetLocalRotation;
                book.transform.localScale = targetLocalScale;
                state.LayoutSection(targetSection, state, false);
            }

            SaveActiveBookshelfLayoutSnapshot();
            BookshelfSwitchStateChanged?.Invoke();
            return book;
        }

        private bool TryResolveActiveShelfPlacement(
            Camera sceneCamera,
            Vector2 pointer,
            BookEntity candidateBook,
            out ShelfSection section,
            out int insertIndex)
        {
            section = null;
            insertIndex = 0;
            if (sceneCamera == null || candidateBook == null || state == null || booksRoot == null || booksRoot.parent == null)
            {
                return false;
            }

            var ray = sceneCamera.ScreenPointToRay(pointer);
            var hits = Physics.RaycastAll(ray, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (var i = 0; i < hits.Length; i++)
            {
                var local = booksRoot.parent.InverseTransformPoint(hits[i].point);
                if (TryResolveSectionPlacement(local, candidateBook, out section, out insertIndex))
                {
                    return true;
                }
            }

            var focusPlane = new Plane(
                booksRoot.parent.forward,
                booksRoot.parent.TransformPoint(new Vector3(0f, height * 0.5f, -depth * 0.5f - 0.05f)));
            if (focusPlane.Raycast(ray, out var enter))
            {
                var local = booksRoot.parent.InverseTransformPoint(ray.GetPoint(enter));
                if (TryResolveSectionPlacement(local, candidateBook, out section, out insertIndex))
                {
                    return true;
                }
            }

            section = null;
            insertIndex = 0;
            return false;
        }

        private bool TryResolveSectionPlacement(
            Vector3 localPosition,
            BookEntity candidateBook,
            out ShelfSection section,
            out int insertIndex)
        {
            section = state.FindAvailableDropSection(localPosition, candidateBook, 0.02f);
            insertIndex = section != null ? state.ComputeDropInsertIndex(localPosition, section, candidateBook) : 0;
            return section != null;
        }

        private IEnumerator AnimateAddedBookInsertion(
            BookEntity book,
            Vector3 targetLocalPosition,
            Quaternion targetLocalRotation,
            Vector3 targetLocalScale)
        {
            if (book == null)
            {
                yield break;
            }

            var target = book.transform;
            var startPosition = targetLocalPosition + addedBookInsertionLocalOffset;
            var startRotation = targetLocalRotation * Quaternion.Euler(0f, -8f, 0f);
            var startScale = targetLocalScale * 0.84f;
            var duration = Mathf.Max(0.01f, addedBookInsertionDuration);
            var elapsed = 0f;

            target.localPosition = startPosition;
            target.localRotation = startRotation;
            target.localScale = startScale;

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
        }

        private static void SetCollidersEnabled(Collider[] colliders, bool enabled)
        {
            if (colliders == null)
            {
                return;
            }

            for (var i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = enabled;
                }
            }
        }

        private ShelfSection FindFirstSectionWithSpace(BookEntity candidate)
        {
            for (var i = 0; i < bookshelfSections.Count; i++)
            {
                var section = bookshelfSections[i];
                if (section == null)
                {
                    continue;
                }

                if (candidate == null || section.CanFit(candidate, state != null ? state.BookPadding : 0.01f))
                {
                    return section;
                }
            }

            return null;
        }

        private bool StartBookshelfSwitch(int direction)
        {
            var targetIndex = GetWrappedBookshelfIndex(activeBookshelfIndex + direction);
            if (targetIndex == activeBookshelfIndex)
            {
                return false;
            }

            pendingBookshelfIndex = targetIndex;
            bookshelfSwitchCoroutine = StartCoroutine(AnimateBookshelfSwitch(direction, targetIndex));
            BookshelfSwitchStateChanged?.Invoke();
            return true;
        }

        private IEnumerator AnimateBookshelfSwitch(int direction, int targetIndex)
        {
            var signedFullAngle = BookshelfSwitchFullAngle * direction;
            PrepareTransitionBookshelf(targetIndex);

            yield return AnimateTurntableRotation(0f, signedFullAngle, BookshelfSwitchRotationDuration);

            SwapActiveBookshelfToTransitionTarget(targetIndex);
            FinishBookshelfSwitching();
        }

        private void PrepareTransitionBookshelf(int targetIndex)
        {
            DestroyTransitionBookshelf();

            transitionBookshelfRoot = new GameObject("BookshelfTransitionRoot").transform;
            transitionBookshelfRoot.SetParent(bookshelfTurntableRoot, false);
            transitionBookshelfRoot.localPosition = GetBookshelfBackSlotPosition();
            transitionBookshelfRoot.localRotation = Quaternion.Euler(0f, BookshelfSwitchFullAngle, 0f);
            transitionBookshelfRoot.localScale = bookshelfRootBaseScale;

            transitionBooksRoot = new GameObject("BooksRoot").transform;
            transitionBooksRoot.SetParent(transitionBookshelfRoot, false);

            BuildBookshelfShell(transitionBookshelfRoot, transitionBookshelfSections, false, GetBookshelfMaterial(targetIndex));
            SpawnBooks(transitionBookshelfSections, targetIndex, transitionBooksRoot, false);
            RestoreBookshelfLayoutSnapshot(targetIndex, transitionBookshelfSections, transitionBooksRoot);
            LayoutBookshelfSectionsImmediately(transitionBookshelfSections);
        }

        private void SwapActiveBookshelfToTransitionTarget(int targetIndex)
        {
            activeBookshelfIndex = targetIndex;
            RebuildActiveBookshelfVariant(activeBookshelfIndex);
            ResetBookshelfTransformForLayout();
            DestroyTransitionBookshelf();
        }

        private void RebuildActiveBookshelfVariant(int variantIndex)
        {
            ClearBookshelfShellChildren();
            ClearBookshelfBooks();
            BuildBookshelfShell(bookshelfRoot, bookshelfSections, true, GetBookshelfMaterial(variantIndex));
            if (state != null)
            {
                state.ResetSections(bookshelfSections);
            }

            SpawnBooks(bookshelfSections, variantIndex, booksRoot, true);
            RestoreBookshelfLayoutSnapshot(variantIndex, bookshelfSections, booksRoot);
            state.LayoutAll(state, false);
        }

        private void SaveActiveBookshelfLayoutSnapshot()
        {
            if (state == null || state.Sections.Count == 0)
            {
                return;
            }

            var snapshot = new BookshelfLayoutSnapshot();
            for (var i = 0; i < state.Sections.Count; i++)
            {
                var source = state.Sections[i];
                var sectionSnapshot = new SectionLayoutSnapshot(source.row, source.column, source.alignment);
                for (var j = 0; j < source.books.Count; j++)
                {
                    var book = source.books[j];
                    if (book == null)
                    {
                        continue;
                    }

                    sectionSnapshot.Books.Add(new BookPlacementSnapshot(
                        GetBookLayoutKey(book),
                        book.orientation,
                        book.bookId,
                        book.title,
                        book.author,
                        book.publisher,
                        book.description,
                        book.memoPreview,
                        book.ownerRating,
                        book.readingStatus,
                        book.isLiked,
                        book.isPublic,
                        book.isPaidCoverOwned,
                        book.isDecoration,
                        book.bookshelfItemId,
                        book.userBookId,
                        book.bookshelfId,
                        book.bookshelfSectionId,
                        book.readingRecordId,
                        book.reportVisibility,
                        book.reportTitle,
                        book.reportBody,
                        book.reportUpdatedLabel));
                }

                snapshot.Sections.Add(sectionSnapshot);
            }

            bookshelfLayoutSnapshots[GetWrappedBookshelfIndex(activeBookshelfIndex)] = snapshot;
        }

        private BookshelfLayoutSnapshot CreateEmptyBookshelfLayoutSnapshot()
        {
            var snapshot = new BookshelfLayoutSnapshot();
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    snapshot.Sections.Add(new SectionLayoutSnapshot(row, column, ShelfSectionAlignment.Left));
                }
            }

            return snapshot;
        }

        private BookshelfLayoutSnapshot CreateDefaultBookshelfLayoutSnapshot(int variantIndex)
        {
            var snapshot = CreateEmptyBookshelfLayoutSnapshot();
            for (var sectionIndex = 0; sectionIndex < snapshot.Sections.Count; sectionIndex++)
            {
                var section = snapshot.Sections[sectionIndex];
                for (var bookIndex = 0; bookIndex < booksPerSection; bookIndex++)
                {
                    section.Books.Add(CreateDefaultBookPlacementSnapshot(
                        variantIndex,
                        sectionIndex,
                        bookIndex,
                        GetBookCatalogItem(variantIndex, sectionIndex, bookIndex, snapshot.Sections.Count)));
                }
            }

            return snapshot;
        }

        private BookshelfLayoutSnapshot CreateServerBookshelfLayoutSnapshot(BookshelfRuntimeApiSnapshot shelf, int variantIndex)
        {
            var snapshot = CreateEmptyBookshelfLayoutSnapshot();
            if (shelf == null)
            {
                return snapshot;
            }

            for (var i = 0; i < shelf.Sections.Count; i++)
            {
                var source = shelf.Sections[i];
                var section = FindSectionSnapshotByGrid(snapshot, source.Row, source.Column);
                if (section != null)
                {
                    section.Alignment = source.Alignment;
                    section.SectionId = source.SectionId;
                }
            }

            var books = new List<BookshelfRuntimeBookSnapshot>(shelf.Books);
            books.Sort((left, right) =>
            {
                var rowCompare = left.Row.CompareTo(right.Row);
                if (rowCompare != 0)
                {
                    return rowCompare;
                }

                var columnCompare = left.Column.CompareTo(right.Column);
                return columnCompare != 0 ? columnCompare : left.SlotNo.CompareTo(right.SlotNo);
            });

            for (var i = 0; i < books.Count; i++)
            {
                var source = books[i];
                var row = Mathf.Clamp(source.Row, 0, rows - 1);
                var column = Mathf.Clamp(source.Column, 0, columns - 1);
                var section = FindSectionSnapshotByGrid(snapshot, row, column);
                if (section == null)
                {
                    continue;
                }

                section.Books.Add(new BookPlacementSnapshot(
                    CreateServerBookKey(variantIndex, source, section.Books.Count),
                    source.Orientation,
                    source.BookId,
                    source.Title,
                    source.Author,
                    source.Publisher,
                    source.Description,
                    source.MemoPreview,
                    source.OwnerRating,
                    source.ReadingStatus,
                    source.IsLiked,
                    source.ReportVisibility == VisibilityScope.Public,
                    source.IsPaidCoverOwned,
                    false,
                    source.BookshelfItemId,
                    source.UserBookId,
                    shelf.BookshelfId,
                    source.SectionId,
                    source.ReadingRecordId,
                    source.ReportVisibility,
                    source.ReportTitle,
                    source.ReportBody,
                    source.ReportUpdatedLabel));
            }

            return snapshot;
        }

        private static Dictionary<string, string> CreateSectionServerIdMap(BookshelfRuntimeApiSnapshot shelf)
        {
            var result = new Dictionary<string, string>();
            if (shelf == null)
            {
                return result;
            }

            for (var i = 0; i < shelf.Sections.Count; i++)
            {
                var section = shelf.Sections[i];
                if (!string.IsNullOrWhiteSpace(section.SectionId))
                {
                    result[GetSectionMapKey(section.Row, section.Column)] = section.SectionId;
                }
            }

            return result;
        }

        private static string GetSectionMapKey(int row, int column)
        {
            return $"{row}:{column}";
        }

        private static string CreateServerBookKey(int variantIndex, BookshelfRuntimeBookSnapshot source, int fallbackSlot)
        {
            var stableId = !string.IsNullOrWhiteSpace(source.BookshelfItemId)
                ? source.BookshelfItemId
                : !string.IsNullOrWhiteSpace(source.UserBookId)
                    ? source.UserBookId
                    : !string.IsNullOrWhiteSpace(source.BookId)
                        ? source.BookId
                        : fallbackSlot.ToString();
            return $"Shelf{variantIndex}_ApiBook_{stableId}";
        }

        private BookPlacementSnapshot CreateDefaultBookPlacementSnapshot(
            int variantIndex,
            int sectionIndex,
            int bookIndex,
            BookCatalogItem item)
        {
            var fallbackKey = $"Shelf{variantIndex}_Book_{sectionIndex}_{bookIndex}";
            var book = item?.Book;
            var userBook = item?.UserBook;
            var readingRecord = item?.ReadingRecord;
            var bookKey = book != null && !string.IsNullOrWhiteSpace(book.BookId)
                ? $"Shelf{variantIndex}_{book.BookId}_{sectionIndex}_{bookIndex}"
                : fallbackKey;
            var description = book?.Description;
            var memoPreview = readingRecord != null && !string.IsNullOrWhiteSpace(readingRecord.Content)
                ? readingRecord.Content
                : description;

            return new BookPlacementSnapshot(
                bookKey,
                BookOrientation.Spine,
                book?.BookId ?? fallbackKey,
                book?.Title,
                book?.Author,
                book?.Publisher,
                description,
                memoPreview,
                userBook?.OwnerRating.HasValue == true ? (float)userBook.OwnerRating.Value : 0f,
                userBook?.ReadingStatus ?? ReadingStatus.Unread,
                userBook?.IsLiked ?? false,
                readingRecord == null || readingRecord.VisibilityScope == VisibilityScope.Public);
        }

        private void MergeBookshelfSnapshots(int sourceIndex, int targetIndex)
        {
            if (sourceIndex == targetIndex ||
                sourceIndex < 0 ||
                targetIndex < 0 ||
                sourceIndex >= bookshelfCount ||
                targetIndex >= bookshelfCount)
            {
                return;
            }

            if (!bookshelfLayoutSnapshots.TryGetValue(sourceIndex, out var source))
            {
                return;
            }

            if (!bookshelfLayoutSnapshots.TryGetValue(targetIndex, out var target))
            {
                target = CreateEmptyBookshelfLayoutSnapshot();
                bookshelfLayoutSnapshots[targetIndex] = target;
            }

            for (var i = 0; i < source.Sections.Count; i++)
            {
                var sourceSection = source.Sections[i];
                var targetSection = FindSectionSnapshotByGrid(target, sourceSection.Row, sourceSection.Column);
                if (targetSection == null)
                {
                    continue;
                }

                for (var j = 0; j < sourceSection.Books.Count; j++)
                {
                    targetSection.Books.Add(sourceSection.Books[j]);
                }
            }
        }

        private void SwapBookshelfData(int leftIndex, int rightIndex)
        {
            SwapDictionaryValues(bookshelfLayoutSnapshots, leftIndex, rightIndex);
            SwapDictionaryValues(bookshelfNames, leftIndex, rightIndex);
            SwapDictionaryValues(bookshelfDesignStyles, leftIndex, rightIndex);
            SwapDictionaryValues(bookshelfVisibilityScopes, leftIndex, rightIndex);
        }

        private void MoveBookshelfData(int sourceIndex, int targetIndex)
        {
            MoveDictionaryValue(bookshelfLayoutSnapshots, sourceIndex, targetIndex);
            MoveDictionaryValue(bookshelfNames, sourceIndex, targetIndex);
            MoveDictionaryValue(bookshelfDesignStyles, sourceIndex, targetIndex);
            MoveDictionaryValue(bookshelfVisibilityScopes, sourceIndex, targetIndex);
        }

        private void EnsureBookshelfDataForCurrentOrder()
        {
            EnsureBookCatalog();
            EnsureBookshelfNamesForCurrentOrder();
            for (var i = 0; i < bookshelfCount; i++)
            {
                if (!bookshelfLayoutSnapshots.ContainsKey(i))
                {
                    bookshelfLayoutSnapshots[i] = CreateDefaultBookshelfLayoutSnapshot(i);
                }
            }
        }

        private void EnsureBookshelfNamesForCurrentOrder()
        {
            for (var i = 0; i < bookshelfCount; i++)
            {
                if (!bookshelfNames.TryGetValue(i, out var name) || string.IsNullOrWhiteSpace(name))
                {
                    bookshelfNames[i] = GetDefaultBookshelfName(i);
                }
            }
        }

        private static void SwapDictionaryValues<T>(Dictionary<int, T> dictionary, int leftIndex, int rightIndex)
        {
            var hasLeft = dictionary.TryGetValue(leftIndex, out var left);
            var hasRight = dictionary.TryGetValue(rightIndex, out var right);

            if (hasRight)
            {
                dictionary[leftIndex] = right;
            }
            else
            {
                dictionary.Remove(leftIndex);
            }

            if (hasLeft)
            {
                dictionary[rightIndex] = left;
            }
            else
            {
                dictionary.Remove(rightIndex);
            }
        }

        private static void MoveDictionaryValue<T>(Dictionary<int, T> dictionary, int sourceIndex, int targetIndex)
        {
            var hasSource = dictionary.TryGetValue(sourceIndex, out var sourceValue);
            if (sourceIndex < targetIndex)
            {
                for (var i = sourceIndex; i < targetIndex; i++)
                {
                    CopyDictionarySlot(dictionary, i, i + 1);
                }
            }
            else
            {
                for (var i = sourceIndex; i > targetIndex; i--)
                {
                    CopyDictionarySlot(dictionary, i, i - 1);
                }
            }

            if (hasSource)
            {
                dictionary[targetIndex] = sourceValue;
            }
            else
            {
                dictionary.Remove(targetIndex);
            }
        }

        private static void CopyDictionarySlot<T>(Dictionary<int, T> dictionary, int destinationIndex, int sourceIndex)
        {
            if (dictionary.TryGetValue(sourceIndex, out var value))
            {
                dictionary[destinationIndex] = value;
            }
            else
            {
                dictionary.Remove(destinationIndex);
            }
        }

        private static int ResolveActiveIndexAfterMove(int activeIndex, int sourceIndex, int targetIndex)
        {
            if (activeIndex == sourceIndex)
            {
                return targetIndex;
            }

            if (sourceIndex < targetIndex && activeIndex > sourceIndex && activeIndex <= targetIndex)
            {
                return activeIndex - 1;
            }

            if (sourceIndex > targetIndex && activeIndex >= targetIndex && activeIndex < sourceIndex)
            {
                return activeIndex + 1;
            }

            return activeIndex;
        }

        private static SectionLayoutSnapshot FindSectionSnapshotByGrid(BookshelfLayoutSnapshot snapshot, int row, int column)
        {
            if (snapshot == null)
            {
                return null;
            }

            for (var i = 0; i < snapshot.Sections.Count; i++)
            {
                var section = snapshot.Sections[i];
                if (section.Row == row && section.Column == column)
                {
                    return section;
                }
            }

            return null;
        }

        private void RemoveBookshelfDataAt(int deletedIndex)
        {
            var shiftedSnapshots = new Dictionary<int, BookshelfLayoutSnapshot>();
            foreach (var pair in bookshelfLayoutSnapshots)
            {
                if (pair.Key < deletedIndex)
                {
                    shiftedSnapshots[pair.Key] = pair.Value;
                }
                else if (pair.Key > deletedIndex)
                {
                    shiftedSnapshots[pair.Key - 1] = pair.Value;
                }
            }

            bookshelfLayoutSnapshots.Clear();
            foreach (var pair in shiftedSnapshots)
            {
                bookshelfLayoutSnapshots[pair.Key] = pair.Value;
            }

            var shiftedNames = new Dictionary<int, string>();
            foreach (var pair in bookshelfNames)
            {
                if (pair.Key < deletedIndex)
                {
                    shiftedNames[pair.Key] = pair.Value;
                }
                else if (pair.Key > deletedIndex)
                {
                    shiftedNames[pair.Key - 1] = pair.Value;
                }
            }

            bookshelfNames.Clear();
            foreach (var pair in shiftedNames)
            {
                bookshelfNames[pair.Key] = pair.Value;
            }

            var shiftedDesignStyles = new Dictionary<int, BookshelfThemeStyle>();
            foreach (var pair in bookshelfDesignStyles)
            {
                if (pair.Key < deletedIndex)
                {
                    shiftedDesignStyles[pair.Key] = pair.Value;
                }
                else if (pair.Key > deletedIndex)
                {
                    shiftedDesignStyles[pair.Key - 1] = pair.Value;
                }
            }

            bookshelfDesignStyles.Clear();
            foreach (var pair in shiftedDesignStyles)
            {
                bookshelfDesignStyles[pair.Key] = pair.Value;
            }

            var shiftedVisibilityScopes = new Dictionary<int, VisibilityScope>();
            foreach (var pair in bookshelfVisibilityScopes)
            {
                if (pair.Key < deletedIndex)
                {
                    shiftedVisibilityScopes[pair.Key] = pair.Value;
                }
                else if (pair.Key > deletedIndex)
                {
                    shiftedVisibilityScopes[pair.Key - 1] = pair.Value;
                }
            }

            bookshelfVisibilityScopes.Clear();
            foreach (var pair in shiftedVisibilityScopes)
            {
                bookshelfVisibilityScopes[pair.Key] = pair.Value;
            }
        }

        private void RestoreBookshelfLayoutSnapshot(int variantIndex, List<ShelfSection> targetSections, Transform targetBooksRoot)
        {
            if (targetSections == null ||
                targetBooksRoot == null ||
                !bookshelfLayoutSnapshots.TryGetValue(GetWrappedBookshelfIndex(variantIndex), out var snapshot))
            {
                return;
            }

            var spawnedBooks = targetBooksRoot.GetComponentsInChildren<BookEntity>(true);
            var booksByKey = new Dictionary<string, BookEntity>();
            for (var i = 0; i < spawnedBooks.Length; i++)
            {
                var key = GetBookLayoutKey(spawnedBooks[i]);
                if (!string.IsNullOrEmpty(key) && !booksByKey.ContainsKey(key))
                {
                    booksByKey.Add(key, spawnedBooks[i]);
                }
            }

            for (var i = 0; i < targetSections.Count; i++)
            {
                targetSections[i].books.Clear();
            }

            var restoredBooks = new HashSet<BookEntity>();
            for (var i = 0; i < snapshot.Sections.Count; i++)
            {
                var sectionSnapshot = snapshot.Sections[i];
                var targetSection = FindSectionByGrid(targetSections, sectionSnapshot.Row, sectionSnapshot.Column);
                if (targetSection == null)
                {
                    continue;
                }

                targetSection.alignment = sectionSnapshot.Alignment;
                for (var j = 0; j < sectionSnapshot.Books.Count; j++)
                {
                    var placement = sectionSnapshot.Books[j];
                    if (!booksByKey.TryGetValue(placement.BookKey, out var book) || book == null)
                    {
                        book = CreateRestoredBook(placement, targetBooksRoot, variantIndex, sectionSnapshot.Row, sectionSnapshot.Column, j);
                        if (book == null)
                        {
                            continue;
                        }
                    }

                    book.SetOrientation(placement.Orientation);
                    ApplySnapshotToBook(book, placement);
                    book.SetSection(targetSection);
                    targetSection.books.Add(book);
                    restoredBooks.Add(book);
                }
            }

            for (var i = 0; i < spawnedBooks.Length; i++)
            {
                var book = spawnedBooks[i];
                if (book != null && !restoredBooks.Contains(book))
                {
                    DestroySpawnedObject(book.gameObject);
                }
            }
        }

        private static ShelfSection FindSectionByGrid(List<ShelfSection> sections, int row, int column)
        {
            if (sections == null)
            {
                return null;
            }

            for (var i = 0; i < sections.Count; i++)
            {
                var section = sections[i];
                if (section.row == row && section.column == column)
                {
                    return section;
                }
            }

            return null;
        }

        private BookEntity CreateRestoredBook(
            BookPlacementSnapshot placement,
            Transform targetBooksRoot,
            int variantIndex,
            int row,
            int column,
            int bookIndex)
        {
            if (targetBooksRoot == null || string.IsNullOrWhiteSpace(placement.BookKey))
            {
                return null;
            }

            var book = CreateProceduralBook(variantIndex, row * columns + column, bookIndex + runtimeBookSerial++, targetBooksRoot);
            book.gameObject.name = placement.BookKey;
            return book;
        }

        private void ApplySnapshotToBook(BookEntity book, BookPlacementSnapshot placement)
        {
            if (book == null)
            {
                return;
            }

            book.bookId = string.IsNullOrWhiteSpace(placement.BookId) ? placement.BookKey : placement.BookId;
            book.bookshelfItemId = placement.BookshelfItemId;
            book.userBookId = placement.UserBookId;
            book.bookshelfId = placement.BookshelfId;
            book.bookshelfSectionId = placement.BookshelfSectionId;
            book.readingRecordId = placement.ReadingRecordId;
            book.title = string.IsNullOrWhiteSpace(placement.Title) ? book.title : placement.Title;
            book.author = string.IsNullOrWhiteSpace(placement.Author) ? book.author : placement.Author;
            book.publisher = string.IsNullOrWhiteSpace(placement.Publisher) ? book.publisher : placement.Publisher;
            book.description = string.IsNullOrWhiteSpace(placement.Description) ? book.description : placement.Description;
            book.memoPreview = string.IsNullOrWhiteSpace(placement.MemoPreview) ? book.memoPreview : placement.MemoPreview;
            book.ownerRating = Mathf.Clamp(placement.OwnerRating, 0f, 5f);
            book.rating = Mathf.RoundToInt(book.ownerRating);
            book.readingStatus = placement.ReadingStatus;
            book.isRead = placement.ReadingStatus == ReadingStatus.Read;
            book.isLiked = placement.IsLiked;
            book.isPublic = placement.IsPublic;
            book.reportVisibility = placement.ReportVisibility;
            book.reportTitle = placement.ReportTitle;
            book.reportBody = placement.ReportBody;
            book.reportUpdatedLabel = placement.ReportUpdatedLabel;
            book.isPaidCoverOwned = placement.IsPaidCoverOwned;
            book.isDecoration = placement.IsDecoration;
            RefreshBookCoverVisuals(book);
        }

        private static string GetBookLayoutKey(BookEntity book)
        {
            return book != null ? book.gameObject.name : string.Empty;
        }

        private void DestroyTransitionBookshelf()
        {
            if (transitionBookshelfRoot != null)
            {
                DestroySpawnedObject(transitionBookshelfRoot.gameObject);
            }

            transitionBookshelfRoot = null;
            transitionBooksRoot = null;
            transitionBookshelfSections.Clear();
        }

        private void LayoutBookshelfSectionsImmediately(List<ShelfSection> sections)
        {
            var padding = state != null ? state.BookPadding : 0.01f;
            var frontInset = state != null ? state.ShelfFrontInset : 0.01f;
            for (var i = 0; i < sections.Count; i++)
            {
                var section = sections[i];
                var sectionScale = GetSectionLayoutScale(section, padding);
                var x = GetSectionLayoutStartX(section, padding, sectionScale);
                for (var j = 0; j < section.books.Count; j++)
                {
                    var book = section.books[j];
                    if (book == null)
                    {
                        continue;
                    }

                    var footprint = book.GetFootprint() * sectionScale;
                    book.transform.localPosition = new Vector3(
                        x + footprint * 0.5f,
                        section.shelfTopY + book.height * sectionScale * 0.5f,
                        section.GetFrontAlignedBookCenterZ(book, sectionScale, frontInset));
                    book.transform.localRotation = BookFootprintUtility.GetVisualRotation(book.orientation);
                    book.transform.localScale = book.BaseLocalScale * sectionScale;
                    x += footprint + padding;
                }
            }
        }

        private static float GetSectionLayoutScale(ShelfSection section, float padding)
        {
            if (section == null)
            {
                return 1f;
            }

            var footprintSum = 0f;
            var bookCount = 0;
            for (var i = 0; i < section.books.Count; i++)
            {
                if (section.books[i] == null)
                {
                    continue;
                }

                footprintSum += section.books[i].GetFootprint();
                bookCount++;
            }

            if (bookCount == 0 || footprintSum <= 0f)
            {
                return 1f;
            }

            var availableForBooks = section.Width - padding * (bookCount + 1);
            if (availableForBooks <= 0f)
            {
                return MinimumSectionFitScale;
            }

            return Mathf.Min(1f, availableForBooks / footprintSum);
        }

        private static float GetSectionLayoutStartX(ShelfSection section, float padding, float sectionScale)
        {
            if (section == null)
            {
                return 0f;
            }

            var contentWidth = GetSectionContentWidth(section, padding, sectionScale);
            var leftStart = section.leftBound + padding;
            var rightStart = section.rightBound - padding - contentWidth;
            if (rightStart < leftStart)
            {
                return leftStart;
            }

            switch (section.alignment)
            {
                case ShelfSectionAlignment.Center:
                    return Mathf.Clamp(section.leftBound + (section.Width - contentWidth) * 0.5f, leftStart, rightStart);
                case ShelfSectionAlignment.Right:
                    return rightStart;
                default:
                    return leftStart;
            }
        }

        public string GetBookshelfName(int index)
        {
            var wrapped = GetWrappedBookshelfIndex(index);
            return bookshelfNames.TryGetValue(wrapped, out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : GetDefaultBookshelfName(wrapped);
        }

        public void SetBookshelfName(int index, string name)
        {
            var wrapped = GetWrappedBookshelfIndex(index);
            var trimmed = (name ?? string.Empty).Trim();
            if (trimmed.Length > 18)
            {
                trimmed = trimmed.Substring(0, 18);
            }

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                bookshelfNames.Remove(wrapped);
            }
            else
            {
                bookshelfNames[wrapped] = trimmed;
            }
        }

        private Material GetBookshelfMaterial(int index)
        {
            if (materialFactory == null)
            {
                return null;
            }

            return materialFactory.GetBookshelfWoodMaterial(GetBookshelfDesignStyle(index));
        }

        private static float GetSectionContentWidth(ShelfSection section, float padding, float sectionScale)
        {
            if (section == null || section.books.Count == 0)
            {
                return 0f;
            }

            var width = 0f;
            var bookCount = 0;
            for (var i = 0; i < section.books.Count; i++)
            {
                var book = section.books[i];
                if (book == null)
                {
                    continue;
                }

                width += book.GetFootprint() * sectionScale;
                bookCount++;
            }

            if (bookCount > 1)
            {
                width += padding * (bookCount - 1);
            }

            return width;
        }

        private IEnumerator AnimateTurntableRotation(float startAngle, float endAngle, float duration)
        {
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = SmootherStep(Mathf.Clamp01(elapsed / duration));
                SetTurntableRotation(Mathf.Lerp(startAngle, endAngle, t));
                yield return null;
            }

            SetTurntableRotation(endAngle);
        }

        private static float SmootherStep(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        private void SetTurntableRotation(float yAngle)
        {
            if (bookshelfTurntableRoot == null)
            {
                return;
            }

            bookshelfTurntableRoot.localScale = turntableRootBaseScale;
            bookshelfTurntableRoot.localRotation = Quaternion.Euler(0f, yAngle, 0f);
        }

        private void ResetBookshelfTransformForLayout()
        {
            if (bookshelfTurntableRoot == null || bookshelfRoot == null)
            {
                return;
            }

            ResetBookshelfToFrontSlot();
        }

        private void FinishBookshelfSwitching()
        {
            ResetBookshelfTransformForLayout();
            bookshelfSwitchCoroutine = null;
            pendingBookshelfIndex = -1;
            BookshelfSwitchStateChanged?.Invoke();

            var queuedDirection = queuedBookshelfSwitchDirection;
            queuedBookshelfSwitchDirection = 0;
            if (queuedDirection != 0)
            {
                BeginBookshelfSwitch(queuedDirection);
            }
        }

        private void ClearBookshelfBooks()
        {
            if (state != null)
            {
                for (var i = 0; i < state.Sections.Count; i++)
                {
                    state.Sections[i].books.Clear();
                }
            }

            DestroyBookEntitiesUnderBooksRoot();
        }

        private void DestroyBookEntitiesUnderBooksRoot()
        {
            if (booksRoot == null)
            {
                return;
            }

            var bookChildren = new List<GameObject>();
            for (var i = 0; i < booksRoot.childCount; i++)
            {
                var child = booksRoot.GetChild(i);
                if (child.GetComponent<BookEntity>() != null)
                {
                    bookChildren.Add(child.gameObject);
                }
            }

            for (var i = 0; i < bookChildren.Count; i++)
            {
                DestroySpawnedObject(bookChildren[i]);
            }
        }

        private static void DestroySpawnedObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            Object.DestroyImmediate(target);
        }

        private static int CountBooks(List<ShelfSection> sections)
        {
            if (sections == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < sections.Count; i++)
            {
                var section = sections[i];
                if (section == null)
                {
                    continue;
                }

                for (var j = 0; j < section.books.Count; j++)
                {
                    if (section.books[j] != null)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private int GetWrappedBookshelfIndex(int index)
        {
            var count = Mathf.Max(1, bookshelfCount);
            var wrapped = index % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }

        private static string GetDefaultBookshelfName(int index)
        {
            return $"{index + 1}번 책장";
        }

        private static string NormalizeBookIdentity(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        private void SetBookshelfAlpha(float alpha)
        {
            if (bookshelfRoot == null)
            {
                return;
            }

            var renderers = bookshelfRoot.GetComponentsInChildren<Renderer>(true);
            var processedMaterials = new HashSet<Material>();

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                var materials = renderer.sharedMaterials;
                for (var j = 0; j < materials.Length; j++)
                {
                    var material = materials[j];
                    if (material == null ||
                        material == materialFactory.GetGhostPreviewMaterial() ||
                        !processedMaterials.Add(material))
                    {
                        continue;
                    }

                    if (alpha < 0.999f)
                    {
                        ConfigureMaterialForFade(material);
                    }
                    else
                    {
                        RestoreMaterialFromFade(material);
                    }

                    var color = material.HasProperty("_BaseColor")
                        ? material.GetColor("_BaseColor")
                        : material.color;
                    color.a = alpha < 0.999f ? alpha : 1f;

                    if (material.HasProperty("_BaseColor"))
                    {
                        material.SetColor("_BaseColor", color);
                    }

                    material.color = color;
                }
            }
        }

        private static void ConfigureMaterialForFade(Material material)
        {
            if (material == null)
            {
                return;
            }

            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private static void RestoreMaterialFromFade(Material material)
        {
            if (material == null)
            {
                return;
            }

            material.SetFloat("_Surface", 0f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            material.renderQueue = -1;
        }

        private void BuildGuestbookBoard()
        {
            var guestbook = new GameObject("GuestbookBoard");
            guestbook.transform.SetParent(roomRoot, false);
            guestbook.transform.localPosition = new Vector3(-1.995f, 1.18f, 0.12f);
            guestbook.transform.localRotation = Quaternion.identity;
            generatedGeometry.Add(guestbook);

            CreateBox("Frame", guestbook.transform, Vector3.zero, new Vector3(0.04f, 1.08f, 1.52f), materialFactory.GetWhiteboardFrameMaterial());
            CreateBox("Surface", guestbook.transform, new Vector3(0.012f, 0f, 0f), new Vector3(0.012f, 0.96f, 1.38f), materialFactory.GetWhiteboardSurfaceMaterial());

            var hitArea = new GameObject("GuestbookHitArea");
            hitArea.transform.SetParent(guestbook.transform, false);
            hitArea.transform.localPosition = new Vector3(0.08f, 0f, 0f);
            var collider = hitArea.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.2f, 1.0f, 1.4f);
            hitArea.AddComponent<GuestbookBoardHitArea>();
            generatedGeometry.Add(hitArea);
        }

        private void CreateBookshelfFocusHitArea(Transform parent)
        {
            var hitArea = new GameObject("BookshelfFocusHitArea");
            hitArea.transform.SetParent(parent, false);
            hitArea.transform.localPosition = new Vector3(0f, height * 0.5f, -depth * 0.5f - 0.05f);
            var collider = hitArea.AddComponent<BoxCollider>();
            collider.size = new Vector3(width + 0.10f, height + 0.10f, 0.18f);
            hitArea.AddComponent<BookshelfFocusHitArea>();
            generatedGeometry.Add(hitArea);
        }

        private void BuildNotebookStation()
        {
            var guestbook = roomRoot != null ? roomRoot.Find("GuestbookBoard") : null;
            var stationParent = guestbook != null ? guestbook : roomRoot;
            var notebookStation = new GameObject("NotebookStation");
            notebookStation.transform.SetParent(stationParent, false);
            notebookStation.transform.localPosition = guestbook != null
                ? MainSceneNotebookStationLocalPosition
                : FallbackNotebookStationLocalPosition;
            notebookStation.transform.localRotation = Quaternion.identity;
            generatedGeometry.Add(notebookStation);

            var aluminum = materialFactory.CreateBookMaterial(new Color(0.72f, 0.73f, 0.71f));
            var darkGlass = materialFactory.CreateBookMaterial(new Color(0.015f, 0.018f, 0.022f));
            var screenGlow = materialFactory.CreateBookMaterial(new Color(0.10f, 0.16f, 0.24f));
            var keyboardTone = materialFactory.CreateBookMaterial(new Color(0.035f, 0.038f, 0.043f));
            var keyLetterTone = materialFactory.CreateBookMaterial(new Color(0.12f, 0.125f, 0.13f));
            var trackpadTone = materialFactory.CreateBookMaterial(new Color(0.60f, 0.61f, 0.59f));

            var desk = new GameObject("Desk");
            desk.transform.SetParent(notebookStation.transform, false);
            desk.transform.localPosition = Vector3.zero;
            desk.transform.localRotation = Quaternion.identity;
            generatedGeometry.Add(desk);

            var deskTopY = BuildDeskSurface(desk.transform);

            var notebook = new GameObject("Notebook");
            notebook.transform.SetParent(desk.transform, false);
            notebook.transform.localPosition = new Vector3(0.10f, deskTopY + 0.009f, 0f);
            notebook.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            generatedGeometry.Add(notebook);

            CreateBox("UnibodyBase", notebook.transform, new Vector3(0f, 0f, 0f), new Vector3(0.50f, 0.018f, 0.32f), aluminum);
            CreateBox("FrontChamfer", notebook.transform, new Vector3(0f, 0.006f, 0.165f), new Vector3(0.45f, 0.008f, 0.012f), materialFactory.CreateBookMaterial(new Color(0.82f, 0.83f, 0.80f)));
            CreateBox("KeyboardWell", notebook.transform, new Vector3(0f, 0.010f, -0.045f), new Vector3(0.36f, 0.006f, 0.145f), keyboardTone);
            BuildKeyboardKeys(notebook.transform, keyLetterTone);
            CreateBox("Trackpad", notebook.transform, new Vector3(0f, 0.012f, 0.095f), new Vector3(0.15f, 0.004f, 0.085f), trackpadTone);
            CreateBox("TrackpadInset", notebook.transform, new Vector3(0f, 0.0145f, 0.095f), new Vector3(0.13f, 0.002f, 0.067f), materialFactory.CreateBookMaterial(new Color(0.68f, 0.69f, 0.67f)));
            CreateBox("HingeBar", notebook.transform, new Vector3(0f, 0.021f, -0.154f), new Vector3(0.43f, 0.020f, 0.026f), aluminum);
            CreateBox("HingeShadowGap", notebook.transform, new Vector3(0f, 0.020f, -0.139f), new Vector3(0.39f, 0.006f, 0.008f), keyboardTone);

            var screen = new GameObject("Screen");
            screen.transform.SetParent(notebook.transform, false);
            screen.transform.localPosition = new Vector3(0f, 0.114f, -0.154f);
            screen.transform.localRotation = Quaternion.Euler(-99.5f, 0f, 0f);
            generatedGeometry.Add(screen);

            CreateBox("ScreenLid", screen.transform, Vector3.zero, new Vector3(0.50f, 0.018f, 0.31f), aluminum);
            CreateBox("LowerBezelLip", screen.transform, new Vector3(0f, -0.0095f, 0.143f), new Vector3(0.478f, 0.009f, 0.015f), aluminum);
            CreateBox("BlackBezel", screen.transform, new Vector3(0f, -0.0105f, 0f), new Vector3(0.474f, 0.008f, 0.270f), darkGlass);
            CreateBox("DisplayPanel", screen.transform, new Vector3(0f, -0.0135f, 0f), new Vector3(0.464f, 0.006f, 0.260f), screenGlow);
            CreateBox("CameraDot", screen.transform, new Vector3(0f, -0.0185f, -0.127f), new Vector3(0.012f, 0.004f, 0.012f), materialFactory.CreateBookMaterial(new Color(0.02f, 0.025f, 0.03f)));
            CreateReferenceTransform(screen.transform, "ScreenCenter", new Vector3(0f, 0.068f, 0.014f));
            CreateReferenceTransform(screen.transform, "CameraFocusPoint", new Vector3(0f, -0.58f, 0.024f));

            var hitArea = new GameObject("NotebookHitArea");
            hitArea.transform.SetParent(notebook.transform, false);
            hitArea.transform.localPosition = new Vector3(0f, 0.13f, -0.03f);
            hitArea.transform.localRotation = Quaternion.identity;
            var collider = hitArea.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.58f, 0.30f, 0.36f);
            hitArea.AddComponent<NotebookHitArea>();
            generatedGeometry.Add(hitArea);

            notebook.AddComponent<NotebookScreenController>();
        }

        private float BuildDeskSurface(Transform deskRoot)
        {
            var tableAsset = ResolveAsset(tableNaturalAsset, TableNaturalAssetPath);
            tableNaturalAsset = tableAsset;

            if (tableAsset != null)
            {
                var tableInstance = Instantiate(tableAsset, deskRoot);
                tableInstance.name = tableAsset.name;
                tableInstance.transform.localPosition = Vector3.zero;
                tableInstance.transform.localScale = Vector3.one;

                var initialBounds = CalculateLocalBoundsFromMeshes(deskRoot);
                if (initialBounds.size.sqrMagnitude > 0.0001f)
                {
                    float sx, sy, sz;
                    if (initialBounds.size.x > initialBounds.size.z)
                    {
                        tableInstance.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * tableInstance.transform.localRotation;
                        sx = DeskTargetDepthZ / Mathf.Max(initialBounds.size.x, 0.0001f);
                        sz = DeskTargetWidthX / Mathf.Max(initialBounds.size.z, 0.0001f);
                    }
                    else
                    {
                        sx = DeskTargetWidthX / Mathf.Max(initialBounds.size.x, 0.0001f);
                        sz = DeskTargetDepthZ / Mathf.Max(initialBounds.size.z, 0.0001f);
                    }

                    sy = DeskTargetHeightY / Mathf.Max(initialBounds.size.y, 0.0001f);
                    tableInstance.transform.localScale = new Vector3(sx, sy, sz);

                    var scaledBounds = CalculateLocalBoundsFromMeshes(deskRoot);
                    if (scaledBounds.size.sqrMagnitude > 0.0001f)
                    {
                        var localPos = tableInstance.transform.localPosition;
                        tableInstance.transform.localPosition = new Vector3(localPos.x, localPos.y - scaledBounds.min.y, localPos.z);
                        return scaledBounds.max.y - scaledBounds.min.y;
                    }

                    return DeskTargetHeightY;
                }

                DestroyImmediate(tableInstance);
            }

            return BuildBoxDesk(deskRoot);
        }

        private static Bounds CalculateLocalBoundsFromMeshes(Transform root)
        {
            var hasBounds = false;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            var meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            for (var i = 0; i < meshFilters.Length; i++)
            {
                var mf = meshFilters[i];
                if (mf == null || mf.sharedMesh == null)
                {
                    continue;
                }

                var localBounds = mf.sharedMesh.bounds;
                var mft = mf.transform;
                var lmin = localBounds.min;
                var lmax = localBounds.max;
                for (var cx = 0; cx < 2; cx++)
                {
                    var x = cx == 0 ? lmin.x : lmax.x;
                    for (var cy = 0; cy < 2; cy++)
                    {
                        var y = cy == 0 ? lmin.y : lmax.y;
                        for (var cz = 0; cz < 2; cz++)
                        {
                            var z = cz == 0 ? lmin.z : lmax.z;
                            var rootLocal = root.InverseTransformPoint(mft.TransformPoint(new Vector3(x, y, z)));
                            if (!hasBounds)
                            {
                                bounds = new Bounds(rootLocal, Vector3.zero);
                                hasBounds = true;
                            }
                            else
                            {
                                bounds.Encapsulate(rootLocal);
                            }
                        }
                    }
                }
            }

            return bounds;
        }

        private float BuildBoxDesk(Transform deskRoot)
        {
            var wood = materialFactory.GetFurnitureWoodMaterial();
            const float deskHeight = 0.38f;
            const float deskThickness = 0.06f;
            const float deskDepth = 0.54f;
            const float deskWidth = 0.86f;
            const float legThickness = 0.06f;

            CreateBox("Desktop", deskRoot, new Vector3(0f, deskHeight, 0f), new Vector3(deskDepth, deskThickness, deskWidth), wood);
            CreateBox("DeskLegA", deskRoot, new Vector3(-(deskDepth * 0.5f - 0.11f), deskHeight * 0.5f, -(deskWidth * 0.5f - 0.10f)), new Vector3(legThickness, deskHeight, legThickness), wood);
            CreateBox("DeskLegB", deskRoot, new Vector3(deskDepth * 0.5f - 0.11f, deskHeight * 0.5f, -(deskWidth * 0.5f - 0.10f)), new Vector3(legThickness, deskHeight, legThickness), wood);
            CreateBox("DeskLegC", deskRoot, new Vector3(-(deskDepth * 0.5f - 0.11f), deskHeight * 0.5f, deskWidth * 0.5f - 0.10f), new Vector3(legThickness, deskHeight, legThickness), wood);
            CreateBox("DeskLegD", deskRoot, new Vector3(deskDepth * 0.5f - 0.11f, deskHeight * 0.5f, deskWidth * 0.5f - 0.10f), new Vector3(legThickness, deskHeight, legThickness), wood);

            return deskHeight + deskThickness * 0.5f;
        }

        private static Transform FindPrimaryNotebookStation(Transform root)
        {
            var candidates = new List<Transform>();
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == "NotebookStation")
                {
                    candidates.Add(transforms[i]);
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            var primary = candidates[0];
            for (var i = 0; i < candidates.Count; i++)
            {
                var parent = candidates[i].parent;
                if (parent != null && parent.name == "GuestbookBoard")
                {
                    primary = candidates[i];
                    break;
                }
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] != primary)
                {
                    DestroySpawnedObject(candidates[i].gameObject);
                }
            }

            return primary;
        }

        private static void CreateReferenceTransform(Transform parent, string name, Vector3 localPosition)
        {
            var reference = new GameObject(name).transform;
            reference.SetParent(parent, false);
            reference.localPosition = localPosition;
            reference.localRotation = Quaternion.identity;
            reference.localScale = Vector3.one;
        }

        private void BuildKeyboardKeys(Transform notebook, Material keyMaterial)
        {
            const int columns = 10;
            const int rows = 4;
            const float keyWidth = 0.026f;
            const float keyDepth = 0.018f;
            const float spacingX = 0.032f;
            const float spacingZ = 0.026f;
            var startX = -spacingX * (columns - 1) * 0.5f;
            var startZ = -0.105f;

            for (var row = 0; row < rows; row++)
            {
                var rowOffset = row == 1 ? 0.008f : row == 2 ? 0.015f : 0f;
                var keysInRow = row == 3 ? 7 : columns;
                var rowStartX = startX + rowOffset + (columns - keysInRow) * spacingX * 0.5f;

                for (var column = 0; column < keysInRow; column++)
                {
                    var keyScale = new Vector3(keyWidth, 0.004f, keyDepth);
                    if (row == 3 && column == 3)
                    {
                        keyScale.x = 0.075f;
                    }

                    var keyPosition = new Vector3(rowStartX + column * spacingX, 0.017f, startZ + row * spacingZ);
                    CreateBox($"Key{row}_{column}", notebook, keyPosition, keyScale, keyMaterial);
                }
            }
        }

        private sealed class BookshelfLayoutSnapshot
        {
            public readonly List<SectionLayoutSnapshot> Sections = new List<SectionLayoutSnapshot>();
        }

        private sealed class SectionLayoutSnapshot
        {
            public SectionLayoutSnapshot(int row, int column, ShelfSectionAlignment alignment)
            {
                Row = row;
                Column = column;
                Alignment = alignment;
            }

            public int Row { get; }
            public int Column { get; }
            public string SectionId { get; set; }
            public ShelfSectionAlignment Alignment { get; set; }
            public List<BookPlacementSnapshot> Books { get; } = new List<BookPlacementSnapshot>();
        }

        private readonly struct BookPlacementSnapshot
        {
            public BookPlacementSnapshot(
                string bookKey,
                BookOrientation orientation,
                string bookId = null,
                string title = null,
                string author = null,
                string publisher = null,
                string description = null,
                string memoPreview = null,
                float ownerRating = 0f,
                ReadingStatus readingStatus = ReadingStatus.Unread,
                bool isLiked = false,
                bool isPublic = true,
                bool isPaidCoverOwned = false,
                bool isDecoration = false,
                string bookshelfItemId = null,
                string userBookId = null,
                string bookshelfId = null,
                string bookshelfSectionId = null,
                string readingRecordId = null,
                VisibilityScope reportVisibility = VisibilityScope.Public,
                string reportTitle = null,
                string reportBody = null,
                string reportUpdatedLabel = null)
            {
                BookKey = bookKey;
                Orientation = orientation;
                BookId = bookId;
                Title = title;
                Author = author;
                Publisher = publisher;
                Description = description;
                MemoPreview = memoPreview;
                OwnerRating = ownerRating;
                ReadingStatus = readingStatus;
                IsLiked = isLiked;
                IsPublic = isPublic;
                IsPaidCoverOwned = isPaidCoverOwned;
                IsDecoration = isDecoration;
                BookshelfItemId = bookshelfItemId;
                UserBookId = userBookId;
                BookshelfId = bookshelfId;
                BookshelfSectionId = bookshelfSectionId;
                ReadingRecordId = readingRecordId;
                ReportVisibility = reportVisibility;
                ReportTitle = reportTitle;
                ReportBody = reportBody;
                ReportUpdatedLabel = reportUpdatedLabel;
            }

            public string BookKey { get; }
            public BookOrientation Orientation { get; }
            public string BookId { get; }
            public string Title { get; }
            public string Author { get; }
            public string Publisher { get; }
            public string Description { get; }
            public string MemoPreview { get; }
            public float OwnerRating { get; }
            public ReadingStatus ReadingStatus { get; }
            public bool IsLiked { get; }
            public bool IsPublic { get; }
            public bool IsPaidCoverOwned { get; }
            public bool IsDecoration { get; }
            public string BookshelfItemId { get; }
            public string UserBookId { get; }
            public string BookshelfId { get; }
            public string BookshelfSectionId { get; }
            public string ReadingRecordId { get; }
            public VisibilityScope ReportVisibility { get; }
            public string ReportTitle { get; }
            public string ReportBody { get; }
            public string ReportUpdatedLabel { get; }
        }
    }
}
