using System.Collections;
using System.Collections.Generic;
using App.Application.Books;
using App.Infrastructure.Catalogs;
using Dotshelf.Scripts.RoomTheme;
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
        // RoomWidth(X) 와 RoomDepth(Z) 를 5×5 정사각형으로 유지. 이전엔 4.2×5 였으나
        // X 축 확장으로 Floor/LeftWall/RightWall/Ceiling/BackWall strip 모두 자동 재배치.
        // 좌/우 벽 위치는 `-RoomWidth*0.5 + 0.05 = -2.45` / `+2.45` 로 갱신됨.
        private const float RoomWidth = 5f;
        private const float RoomDepth = 5f;
        private const float RoomHeight = 2.8f;
        private const float BackWallZ = 1.45f;
        private const float BackWallThickness = 0.08f;
        private const float BookshelfFacingPanelClearance = 0.045f;
        private const float RotatingWallFrameSideReveal = 0.035f;
        private const float BookshelfSwitchRotationDuration = 1f;
        private const float BookshelfSwitchFullAngle = 180f;
        private const float MinimumSectionFitScale = 0.68f;
        private const float SectionWallClearance = 0.012f;
        private const string AdvertisementBookInstanceName = "AdvertisementBook_Section";
        private const string BookCoverLabelsName = "BookCoverLabels";
        private const string BookPageEdgesName = "BookPageEdges";
        private const string BookPageEdgeTextureResourcePath = "Books/book_page_edge_texture";
        private const float BookCoverSurfaceOffset = 0.506f;
        private const float BookPageEdgeInset = 0.012f;
        private const float BookPageEdgeSurfaceThickness = 0.006f;
        private const float BookPageEdgeSurfaceEpsilon = 0.0008f;
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
        private const string TableNaturalAssetPath = "Assets/Arts/Models/table_natural.glb";
        private const string BookshelfThemeLayoutCatalogResourcePath = "BookshelfThemeLayoutCatalog";
        private const float DeskTargetWidthX = 0.54f;
        private const float DeskTargetHeightY = 0.41f;
        private const float DeskTargetDepthZ = 0.86f;
        private const string QABooksBaseTexturePath = "Assets/QA_Books/Textures/BooksA_bc.tga";
        private const string QABooksNormalTexturePath = "Assets/QA_Books/Textures/BooksA_n.tga";
        private const string QABooksMetallicTexturePath = "Assets/QA_Books/Textures/BooksA_m_ao_g.tga";
        private const string QABooksAltBaseTexturePath = "Assets/QA_Books/Textures/Books_bc.tga";
        private const string QABooksAltNormalTexturePath = "Assets/QA_Books/Textures/Books_n.tga";
        private const string QABooksAltMetallicTexturePath = "Assets/QA_Books/Textures/Books_m_ao_g.tga";
        private const int BookCoverSkinSpriteCacheLimit = 16;
        private static readonly Vector2 BookCoverCanvasSize = new Vector2(144f, 216f);
        private static readonly Vector2 BookSpineCanvasSize = new Vector2(96f, 360f);
        private static readonly Dictionary<string, Sprite> BookCoverSkinSpriteCache = new Dictionary<string, Sprite>();
        private static readonly Queue<string> BookCoverSkinSpriteCacheKeys = new Queue<string>();
        private static readonly Vector3 MainSceneNotebookStationLocalPosition = new Vector3(0.72f, -1.18f, 0f);
        private static readonly Vector3 FallbackNotebookStationLocalPosition = new Vector3(-1.275f, 0f, 0.12f);

        [Header("References")]
        [SerializeField] private BookshelfState state = null;
        [SerializeField] private MaterialFactory materialFactory = null;
        [SerializeField] private Transform roomRoot = null;
        [SerializeField] private Transform bookshelfTurntableRoot = null;
        [SerializeField] private Transform bookshelfRoot = null;
        [SerializeField] private Transform booksRoot = null;
        [SerializeField] private GameObject tableNaturalAsset = null;
        [SerializeField] private GameObject notebookStationAsset = null;
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

        // Canonical bookshelf shell dimensions — RoomEditModeController.ApplyVintageScaleNormalization
        // 이 vintage_* 자산 크기 보정의 reference 로 사용. shell 이 아직 만들어지지 않았거나 (bookshelfCount=0)
        // 동적 Renderer.bounds 측정 실패 시 deterministic fallback. 인스펙터에서 width/height/depth 가
        // 변경되면 그대로 따라감.
        public Vector3 CanonicalShellSize => new Vector3(width, height, depth);

        // active bookshelfRoot 의 shell mesh 가 (재)생성된 직후 발행. 외부 (App.Bootstrap.
        // BookshelfVisualSkin) 가 listen 해서 자기 형제 (functional shell) renderer 들을 다시
        // hide 하는 등 skin overlay 일관성 유지에 사용.
        public event System.Action BookshelfShellRebuilt;

        // 회전 swap 중간 상태의 transient transitionBookshelfRoot 가 준비된 직후 발행 — root
        // 자체와 그 자식 shell mesh 가 모두 완성된 시점. 외부 (App.Bootstrap.BookshelfVisualSkin)
        // 가 listen 해서 transition root 에도 skin clone 을 attach + functional mesh 를 hide
        // 한다. transition root 는 SwapActiveBookshelfToTransitionTarget 이 swap 후 destroy
        // 하므로 거기 attach 된 skin clone 도 자동 정리.
        public event System.Action<Transform> TransitionShellPrepared;
        [SerializeField] private int rows = 4;
        [SerializeField] private int columns = 4;
        [SerializeField] private float boardThickness = 0.025f;
        [SerializeField] private float dividerThickness = 0.02f;
        [SerializeField] private float backPanelThickness = 0.012f;

        [Header("Bookshelf Switching")]
        [SerializeField] private float bookshelfWallMountClearance = 0.085f;
        [SerializeField] private float bookshelfRotationWallClearance = 0.055f;
        [SerializeField] private float minimumBookshelfRotationClearWidth = 1.75f;
        [SerializeField] private float bookshelfSwitchPairGap = 0.12f;
        [SerializeField] private float bookshelfSwitchSlideDurationRatio = 0.22f;

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

        [Header("Advertisement Book")]
        [SerializeField] private GameObject advertisementBookPrefab = null;
        [SerializeField] private Vector3 advertisementBookLocalEulerAngles = new Vector3(0f, 180f, 0f);
        [SerializeField] [Range(0.1f, 1f)] private float advertisementBookSectionHeightRatio = 0.78f;
        [SerializeField] [Range(0.1f, 1f)] private float advertisementBookSectionWidthRatio = 0.52f;
        [SerializeField] [Range(0.1f, 1f)] private float advertisementBookSectionDepthRatio = 0.82f;
        [SerializeField] private float advertisementBookBottomInset = 0.014f;
        [SerializeField] private float advertisementBookFrontInset = 0.014f;

        private readonly List<GameObject> generatedGeometry = new List<GameObject>();
        private readonly List<ShelfSection> bookshelfSections = new List<ShelfSection>();
        private readonly List<ShelfSection> transitionBookshelfSections = new List<ShelfSection>();
        private readonly List<GameObject> qaBookPrefabs = new List<GameObject>();
        private Material qaBooksMaterial;
        private Material qaBooksAltMaterial;
        private Material bookPageEdgeMaterial;
        private Texture2D generatedBookPageEdgeTexture;
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
        private bool bookCatalogLoaded;
        private bool useServerBookshelfSnapshots;
        private bool hasReceivedServerSnapshot;
        private bool isConfigured;
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
        private BookshelfThemeLayoutCatalog bookshelfThemeLayoutCatalog;
        private readonly List<int> navigableBookshelfIndices = new List<int>();
        private RoomThemeApplier cachedRoomThemeApplier;
        private string lastNavigationCandidateLogSignature;
        private bool navigableBookshelfIndicesDirty = true;
        private long lastNavigableThemeAssetId = long.MinValue;
        private BookshelfThemeStyle lastNavigableThemeStyle = (BookshelfThemeStyle)(-1);
        private int lastNavigableBookshelfCount = -1;

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
        public int NavigableBookshelfCount => GetNavigableBookshelfCount();
        // 활성 테마와 호환되는 책장(=실제 3D 씬에 표시되는 책장) 인덱스 목록. 책장관리 modal 이 유령 책장을
        // 표시하지 않도록 외부 노출. internal navigableBookshelfIndices 는 List<int> 지만 IReadOnlyList 로 캡슐화.
        public IReadOnlyList<int> NavigableBookshelfIndices
        {
            get
            {
                BuildNavigableBookshelfIndices();
                return navigableBookshelfIndices;
            }
        }
        public bool UseServerBookshelfSnapshots => useServerBookshelfSnapshots;
        public bool HasReceivedServerSnapshot => hasReceivedServerSnapshot;
        public int ActiveBookshelfIndex => activeBookshelfIndex;
        public int DisplayBookshelfIndex => IsBookshelfSwitching && pendingBookshelfIndex >= 0 ? pendingBookshelfIndex : activeBookshelfIndex;
        public int NavigableDisplayBookshelfIndex => GetNavigableDisplayBookshelfIndex();
        public string DisplayBookshelfName => GetBookshelfName(DisplayBookshelfIndex);
        public int ActiveBookshelfBookCount => CountBooks(bookshelfSections);
        public int ActiveBookshelfSectionCount => CountUserBookSections(bookshelfSections);
        public int ActiveBookshelfOccupiedSectionCount => CountOccupiedSections(bookshelfSections);
        public int ActiveBookshelfEmptySectionCount => Mathf.Max(0, ActiveBookshelfSectionCount - ActiveBookshelfOccupiedSectionCount);
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

        // Generate() 가 한 사이클 끝났음을 알리는 신호. 첫 generate / 재generate / ClearGenerated 후 재호출
        // 모두에서 발사된다. 첫 generate 끝나야 GuestbookBoardHitArea 가 attach 되므로, 외부에서
        // DashboardScreenPanel 같이 hitArea 에 의존하는 mount 작업을 이 시점에 trigger 해야 race 없이
        // 동작한다 (#114 후속, 신규 가입 직후 빈 액자 회귀 관련).
        public event System.Action Generated;

        public void Configure(BookshelfState bookshelfState, MaterialFactory factory)
        {
            state = bookshelfState;
            materialFactory = factory;
            isConfigured = state != null && materialFactory != null;
        }

        public void Configure(BookshelfState bookshelfState, MaterialFactory factory, LoadMyBookshelfBooksUseCase booksUseCase)
        {
            state = bookshelfState;
            materialFactory = factory;
            loadBooksUseCase = booksUseCase;
            isConfigured = state != null && materialFactory != null;
        }

        public void ConfigureServerBookshelfSnapshots(bool enabled)
        {
            useServerBookshelfSnapshots = enabled;
        }

        public void ApplyServerBookshelves(IReadOnlyList<BookshelfRuntimeApiSnapshot> shelves)
        {
            hasReceivedServerSnapshot = true;
            var orderedShelves = shelves != null
                ? new List<BookshelfRuntimeApiSnapshot>(shelves)
                : new List<BookshelfRuntimeApiSnapshot>();
            orderedShelves.Sort((left, right) => left.DisplayOrder.CompareTo(right.DisplayOrder));

            var hasItemsLoadFallback = orderedShelves.Exists(shelf => shelf != null && shelf.ItemsLoadFailed);
            if (hasItemsLoadFallback)
            {
                SaveActiveBookshelfLayoutSnapshot();
            }

            var previousLayoutsByServerId = hasItemsLoadFallback
                ? CreateBookshelfLayoutSnapshotsByServerId()
                : null;
            var previousSectionsByServerId = hasItemsLoadFallback
                ? CreateSectionServerIdsByServerId()
                : null;

            bookshelfLayoutSnapshots.Clear();
            bookshelfServerIds.Clear();
            bookshelfSectionServerIds.Clear();
            bookshelfNames.Clear();
            bookshelfDesignStyles.Clear();
            bookshelfVisibilityScopes.Clear();
            MarkNavigableBookshelfIndicesDirty();

            if (orderedShelves.Count == 0)
            {
                bookshelfCount = 0;
                activeBookshelfIndex = 0;
                pendingBookshelfIndex = -1;
                queuedBookshelfSwitchDirection = 0;
                ClearBookshelfShellChildren();
                ClearBookshelfBooks();
                bookshelfSections.Clear();
                if (state != null)
                {
                    state.ResetSections(bookshelfSections);
                }

                BookshelfSwitchStateChanged?.Invoke();
                return;
            }

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
                if (shelf.HasThemeStyle)
                {
                    if (shelf.ThemeStyle == BookshelfThemeStyle.Default)
                    {
                        bookshelfDesignStyles.Remove(i);
                    }
                    else
                    {
                        bookshelfDesignStyles[i] = shelf.ThemeStyle;
                    }
                }

                if (shelf.ItemsLoadFailed &&
                    !string.IsNullOrWhiteSpace(shelf.BookshelfId) &&
                    previousSectionsByServerId != null &&
                    previousSectionsByServerId.TryGetValue(shelf.BookshelfId, out var previousSections))
                {
                    bookshelfSectionServerIds[i] = new Dictionary<string, string>(previousSections);
                }
                else
                {
                    bookshelfSectionServerIds[i] = CreateSectionServerIdMap(shelf);
                }

                if (shelf.ItemsLoadFailed &&
                    !string.IsNullOrWhiteSpace(shelf.BookshelfId) &&
                    previousLayoutsByServerId != null &&
                    previousLayoutsByServerId.TryGetValue(shelf.BookshelfId, out var previousLayout))
                {
                    bookshelfLayoutSnapshots[i] = previousLayout;
                }
                else
                {
                    bookshelfLayoutSnapshots[i] = CreateServerBookshelfLayoutSnapshot(shelf, i);
                }
            }

            if (booksRoot != null)
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

        public bool HasServerPlacementIdsForBookshelf(int index)
        {
            var wrappedIndex = GetWrappedBookshelfIndex(index);
            if (!bookshelfServerIds.TryGetValue(wrappedIndex, out var bookshelfId) ||
                string.IsNullOrWhiteSpace(bookshelfId))
            {
                return false;
            }

            if (!bookshelfSectionServerIds.TryGetValue(wrappedIndex, out var sections) ||
                sections == null)
            {
                return false;
            }

            var requiredSectionCount = Mathf.Max(1, rows * columns);
            return sections.Count >= requiredSectionCount;
        }

        private Dictionary<string, BookshelfLayoutSnapshot> CreateBookshelfLayoutSnapshotsByServerId()
        {
            var result = new Dictionary<string, BookshelfLayoutSnapshot>();
            foreach (var pair in bookshelfServerIds)
            {
                if (string.IsNullOrWhiteSpace(pair.Value) ||
                    !bookshelfLayoutSnapshots.TryGetValue(pair.Key, out var snapshot))
                {
                    continue;
                }

                result[pair.Value] = snapshot;
            }

            return result;
        }

        private Dictionary<string, Dictionary<string, string>> CreateSectionServerIdsByServerId()
        {
            var result = new Dictionary<string, Dictionary<string, string>>();
            foreach (var pair in bookshelfServerIds)
            {
                if (string.IsNullOrWhiteSpace(pair.Value) ||
                    !bookshelfSectionServerIds.TryGetValue(pair.Key, out var sections) ||
                    sections == null)
                {
                    continue;
                }

                result[pair.Value] = new Dictionary<string, string>(sections);
            }

            return result;
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
            if (!isConfigured)
            {
                return;
            }

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
            if (!useServerBookshelfSnapshots)
            {
                LoadBookCatalog();
            }

            ClearGenerated();
            BuildRoom();
            BuildBookshelf();
            NormalizeNotebookStationPlacement();

            // 첫 generate 후 GuestbookBoardHitArea 가 막 attach 된 시점.
            // BookshelfExperienceManager 가 신규 가입 race 회복용으로 이 신호를 듣는다.
            Generated?.Invoke();
        }

        public void ApplyBookCatalogDataToExistingBooks()
        {
            if (useServerBookshelfSnapshots)
            {
                if (Application.isPlaying)
                {
                    Debug.Log("Bookshelf catalog metadata is not applied to scene books in API mode; waiting for server bookshelf snapshots.");
                }

                return;
            }

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

            EnsureActiveBookshelfVisible();
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
            bookshelfCount = useServerBookshelfSnapshots ? 0 : InitialBookshelfCount;

            ClearRoomChildren();
            ClearTurntableDecorationChildren();
            ClearBookshelfShellChildren();
            DestroyNamedChild(transform, "HamhamDisplay");

            DestroyBookEntitiesUnderBooksRoot();
        }

        // roomRoot 자식 중 ClearRoomChildren() 가 destroy 하지 않을 이름들. 현재는 천장
        // (테마 무관 공통 구조물) 만. 향후 spawn 단계가 추가되면 이 list 확장.
        private static readonly HashSet<string> PreservedRoomChildNames = new HashSet<string>
        {
            "Ceiling"
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

                // App.Bootstrap.BookshelfVisualSkin 으로 부착된 visual skin 자식은 destroy 제외.
                // BookshelfPorting → App.Bootstrap 역방향 의존 회피를 위해 컴포넌트 reference 가
                // 아닌 GameObject 이름 ("ThemeBookshelfVisual") 으로 매칭한다. skin 의 lifecycle
                // (생성/제거) 은 App.Bootstrap.BookshelfVisualSkinApplier 가 담당한다.
                if (child.gameObject.name == "ThemeBookshelfVisual")
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
            BuildBackWallPanel();
            CreateBox("LeftWall", roomRoot, new Vector3(-RoomWidth * 0.5f + 0.05f, RoomHeight * 0.5f, 0f), new Vector3(0.08f, RoomHeight, RoomDepth), materialFactory.GetWallMaterial());
            CreateBox("RightWall", roomRoot, new Vector3(RoomWidth * 0.5f - 0.05f, RoomHeight * 0.5f, 0f), new Vector3(0.08f, RoomHeight, RoomDepth), materialFactory.GetWallMaterial());
            EnsureCeiling();

            BuildGuestbookBoard();
            BuildNotebookStation();
        }

        private const float CeilingThickness = 0.08f;

        // 천장은 테마와 무관한 공통 구조물. 다음 두 메커니즘으로 Generate() 재실행에서도 보존된다:
        //   1) generatedGeometry 에 의도적으로 등록하지 않음 → ClearGenerated() 의 강제 destroy 대상에서 제외.
        //   2) PreservedRoomChildNames 에 "Ceiling" 등록됨 → ClearRoomChildren() 가 자식 destroy 시 skip.
        //
        // roomRoot.Find("Ceiling") 으로 idempotent: 이미 있으면 새로 만들지 않음 (중복 차단).
        // 추후 천장 조명/장식은 이 Ceiling 의 자식으로 추가 (예: Ceiling/Lights, Ceiling/Decor) — 조명 parent
        // 가 분리되도록 구조 확장 가능.
        private void EnsureCeiling()
        {
            if (roomRoot == null)
            {
                return;
            }

            if (roomRoot.Find("Ceiling") != null)
            {
                return;
            }

            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Ceiling";
            ceiling.transform.SetParent(roomRoot, false);
            // 벽 top y == RoomHeight, 천장 bottom y == RoomHeight 로 정렬해 틈/겹침 차단.
            ceiling.transform.localPosition = new Vector3(0f, RoomHeight + CeilingThickness * 0.5f, 0f);
            // floor 와 동일한 footprint — 시각적으로 닫힌 방.
            ceiling.transform.localScale = new Vector3(RoomWidth, CeilingThickness, RoomDepth);
            ceiling.GetComponent<MeshRenderer>().sharedMaterial = materialFactory.GetCeilingMaterial();
            DestroyImmediate(ceiling.GetComponent<Collider>());
            // generatedGeometry 등록 안 함 — 의도적. ClearGenerated() 가 강제 destroy 하지 않도록.
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

        private BookshelfThemeLayout ResolveBookshelfThemeLayout(BookshelfThemeStyle style)
        {
            if (style == BookshelfThemeStyle.Default)
            {
                return null;
            }

            if (bookshelfThemeLayoutCatalog == null)
            {
                bookshelfThemeLayoutCatalog = Resources.Load<BookshelfThemeLayoutCatalog>(BookshelfThemeLayoutCatalogResourcePath);
            }

            if (bookshelfThemeLayoutCatalog != null &&
                bookshelfThemeLayoutCatalog.TryGetLayout(style, out var layout) &&
                layout != null &&
                layout.IsUsable)
            {
                return layout;
            }

            return null;
        }

        private RoomThemeAsset ResolveActiveNavigationTheme()
        {
            if (cachedRoomThemeApplier == null)
            {
                cachedRoomThemeApplier = FindFirstObjectByType<RoomThemeApplier>(FindObjectsInactive.Include);
            }

            return cachedRoomThemeApplier != null ? cachedRoomThemeApplier.CurrentTheme : null;
        }

        private BookshelfThemeStyle ResolveActiveNavigationThemeStyle()
        {
            var activeStyle = GetBookshelfDesignStyle(DisplayBookshelfIndex);
            if (activeStyle != BookshelfThemeStyle.Default && ResolveBookshelfThemeLayout(activeStyle) != null)
            {
                return activeStyle;
            }

            var theme = ResolveActiveNavigationTheme();
            if (theme != null)
            {
                var themeStyle = (BookshelfThemeStyle)theme.BookshelfStyleIndex;
                if (System.Enum.IsDefined(typeof(BookshelfThemeStyle), themeStyle) &&
                    themeStyle != BookshelfThemeStyle.Default &&
                    ResolveBookshelfThemeLayout(themeStyle) != null)
                {
                    return themeStyle;
                }
            }

            return BookshelfThemeStyle.Default;
        }

        private BookshelfThemeStyle ResolveRenderedBookshelfDesignStyle(int index)
        {
            var shelfStyle = GetBookshelfDesignStyle(index);
            if (shelfStyle != BookshelfThemeStyle.Default && ResolveBookshelfThemeLayout(shelfStyle) != null)
            {
                return shelfStyle;
            }

            var activeThemeStyle = ResolveActiveNavigationThemeStyle();
            if (activeThemeStyle != BookshelfThemeStyle.Default)
            {
                return activeThemeStyle;
            }

            return shelfStyle;
        }

        private void MarkNavigableBookshelfIndicesDirty()
        {
            navigableBookshelfIndicesDirty = true;
        }

        private void BuildNavigableBookshelfIndices()
        {
            var activeTheme = ResolveActiveNavigationTheme();
            var activeThemeStyle = ResolveActiveNavigationThemeStyle();
            var activeThemeAssetId = activeTheme != null ? activeTheme.AssetId : 0L;
            var totalCandidateCount = Mathf.Max(0, bookshelfCount);

            if (!navigableBookshelfIndicesDirty &&
                lastNavigableThemeAssetId == activeThemeAssetId &&
                lastNavigableThemeStyle == activeThemeStyle &&
                lastNavigableBookshelfCount == totalCandidateCount)
            {
                return;
            }

            navigableBookshelfIndicesDirty = false;
            lastNavigableThemeAssetId = activeThemeAssetId;
            lastNavigableThemeStyle = activeThemeStyle;
            lastNavigableBookshelfCount = totalCandidateCount;
            navigableBookshelfIndices.Clear();
            var excludedEntries = new List<string>();

            if (totalCandidateCount <= 0)
            {
                LogBookshelfCandidateFilter(activeTheme, activeThemeStyle, totalCandidateCount, 0, excludedEntries, false);
                return;
            }

            for (var i = 0; i < totalCandidateCount; i++)
            {
                var style = GetBookshelfDesignStyle(i);
                var includeCandidate = true;
                var exclusionReason = string.Empty;

                if (activeThemeStyle != BookshelfThemeStyle.Default)
                {
                    if (style != BookshelfThemeStyle.Default && style != activeThemeStyle)
                    {
                        includeCandidate = false;
                        exclusionReason = $"style_mismatch:{style}";
                    }
                }
                else if (style != BookshelfThemeStyle.Default)
                {
                    var shelfLayout = ResolveBookshelfThemeLayout(style);
                    if (shelfLayout == null || !shelfLayout.IsUsable)
                    {
                        includeCandidate = false;
                        exclusionReason = $"missing_layout:{style}";
                    }
                }

                if (includeCandidate)
                {
                    navigableBookshelfIndices.Add(i);
                    continue;
                }

                excludedEntries.Add($"{GetBookshelfName(i)}[{i}]={exclusionReason}");
            }

            var fallbackUsed = false;
            if (navigableBookshelfIndices.Count == 0 && totalCandidateCount > 0)
            {
                fallbackUsed = true;
                navigableBookshelfIndices.Add(GetWrappedBookshelfIndex(DisplayBookshelfIndex));
            }

            LogBookshelfCandidateFilter(activeTheme, activeThemeStyle, totalCandidateCount, navigableBookshelfIndices.Count, excludedEntries, fallbackUsed);
        }

        private void LogBookshelfCandidateFilter(
            RoomThemeAsset activeTheme,
            BookshelfThemeStyle activeThemeStyle,
            int totalCandidateCount,
            int filteredCandidateCount,
            IReadOnlyList<string> excludedEntries,
            bool fallbackUsed)
        {
            var excludedSummary = excludedEntries != null && excludedEntries.Count > 0
                ? string.Join(", ", excludedEntries)
                : "none";
            var signature = $"{activeTheme?.AssetId ?? 0L}|{activeTheme?.DisplayName ?? "none"}|{activeThemeStyle}|{totalCandidateCount}|{filteredCandidateCount}|{excludedSummary}|{fallbackUsed}";
            if (signature == lastNavigationCandidateLogSignature)
            {
                return;
            }

            lastNavigationCandidateLogSignature = signature;
            Debug.Log(
                $"[BookshelfNav] candidate_build activeThemeId={activeTheme?.AssetId ?? 0L} activeThemeName={activeTheme?.DisplayName ?? "none"} " +
                $"activeThemeStyle={activeThemeStyle} totalCandidates={totalCandidateCount} filteredCandidates={filteredCandidateCount} " +
                $"excluded={excludedSummary} candidateFallback={fallbackUsed}");
        }

        private void LogBookshelfNavigationState(string reason, int selectedIndex, bool fallbackUsed)
        {
            var activeTheme = ResolveActiveNavigationTheme();
            var activeThemeStyle = ResolveActiveNavigationThemeStyle();
            var selectedStyle = ResolveRenderedBookshelfDesignStyle(selectedIndex);
            var selectedLayout = ResolveBookshelfThemeLayout(selectedStyle);
            var selectedPrefabName = selectedLayout != null && selectedLayout.BookshelfPrefab != null
                ? selectedLayout.BookshelfPrefab.name
                : "none";
            var selectedSpecName = selectedLayout != null ? selectedLayout.name : "none";
            Debug.Log(
                $"[BookshelfNav] reason={reason} activeThemeId={activeTheme?.AssetId ?? 0L} activeThemeName={activeTheme?.DisplayName ?? "none"} " +
                $"activeThemeStyle={activeThemeStyle} totalCandidates={bookshelfCount} candidateCount={navigableBookshelfIndices.Count} " +
                $"selectedIndex={selectedIndex} selectedStyle={selectedStyle} selectedLayout={selectedSpecName} selectedPrefab={selectedPrefabName} fallback={fallbackUsed}");
        }

        private void BuildBackWallPanel()
        {
            var wallMaterial = materialFactory.GetWallMaterial();
            CreateBox(
                "BackWall",
                roomRoot,
                new Vector3(0f, RoomHeight * 0.5f, BackWallZ),
                new Vector3(RoomWidth, RoomHeight, BackWallThickness),
                wallMaterial);
        }

        private void BuildBookshelf()
        {
            ConfigureBookshelfTurntable();
            bookshelfRootBaseScale = bookshelfRoot.localScale;
            ResetBookshelfToFrontSlot();
            if (bookshelfCount <= 0)
            {
                ClearBookshelfShellChildren();
                ClearBookshelfBooks();
                bookshelfSections.Clear();
                if (state != null)
                {
                    state.ResetSections(bookshelfSections);
                }

                return;
            }

            var wood = GetBookshelfMaterial(activeBookshelfIndex);

            BuildBookshelfShell(bookshelfRoot, bookshelfSections, true, wood, ResolveRenderedBookshelfDesignStyle(activeBookshelfIndex));

            state.ResetSections(bookshelfSections);
            PopulateBookshelfVariant(activeBookshelfIndex);
            SetBookshelfAlpha(1f);

            BookshelfShellRebuilt?.Invoke();
        }

        private void BuildBookshelfShell(Transform shellRoot, List<ShelfSection> sections, bool includeFocusHitArea, Material wood, BookshelfThemeStyle style)
        {
            var themeLayout = ResolveBookshelfThemeLayout(style);
            if (themeLayout != null && TryBuildThemedBookshelfShell(shellRoot, sections, includeFocusHitArea, themeLayout))
            {
                return;
            }

            var activeTheme = ResolveActiveNavigationTheme();
            var fallbackMessage =
                $"[BookshelfNav] procedural_fallback activeThemeId={activeTheme?.AssetId ?? 0L} activeThemeName={activeTheme?.DisplayName ?? "none"} " +
                $"shellRoot={(shellRoot != null ? shellRoot.name : "null")} style={style} includeFocusHitArea={includeFocusHitArea} " +
                $"reason={(themeLayout == null ? "layout_missing" : "layout_invalid")}";
            if (style == BookshelfThemeStyle.Default)
            {
                Debug.Log(fallbackMessage);
            }
            else
            {
                Debug.LogWarning(fallbackMessage);
            }

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
                    var section = new ShelfSection(row, column, innerLeft, innerRight, bottomY, topY - bottomY, sectionCenterZ, innerFrontZ, innerBackZ)
                    {
                        isAdvertisementSection = IsAdvertisementSection(row, column)
                    };
                    sections.Add(section);
                }
            }
        }

        private bool TryBuildThemedBookshelfShell(
            Transform shellRoot,
            List<ShelfSection> sections,
            bool includeFocusHitArea,
            BookshelfThemeLayout themeLayout)
        {
            if (shellRoot == null || sections == null || themeLayout == null || !themeLayout.IsUsable)
            {
                return false;
            }

            var visual = themeLayout.InstantiateVisual(shellRoot);
            if (visual != null)
            {
                generatedGeometry.Add(visual);
            }

            if (includeFocusHitArea)
            {
                CreateBookshelfFocusHitArea(shellRoot, themeLayout.FocusLocalPosition, themeLayout.FocusSize);
            }

            themeLayout.BuildSections(sections);
            return sections.Count > 0;
        }

        private void ConfigureBookshelfTurntable()
        {
            bookshelfTurntableRoot.localPosition = new Vector3(0f, 0f, GetBookshelfTurntableZ());
            bookshelfTurntableRoot.localRotation = Quaternion.identity;
            turntableRootBaseScale = bookshelfTurntableRoot.localScale;
        }

        private float GetBookshelfWallFrameWidth()
        {
            return width + RotatingWallFrameSideReveal * 2f;
        }

        private static float GetBackWallFrontSurfaceWorldZ()
        {
            return BackWallZ - BackWallThickness * 0.5f;
        }

        private float GetBookshelfMountedCenterZ()
        {
            return GetBackWallFrontSurfaceWorldZ() - depth * 0.5f - bookshelfWallMountClearance;
        }

        private float GetBookshelfTurntableZ()
        {
            return GetBackWallFrontSurfaceWorldZ() - GetBookshelfRotationHalfWidth() - bookshelfRotationWallClearance;
        }

        private float GetBookshelfRotationHalfWidth()
        {
            var clearWidth = Mathf.Max(GetBookshelfWallFrameWidth(), minimumBookshelfRotationClearWidth);
            if (bookshelfThemeLayoutCatalog == null)
            {
                bookshelfThemeLayoutCatalog = Resources.Load<BookshelfThemeLayoutCatalog>(BookshelfThemeLayoutCatalogResourcePath);
            }

            if (bookshelfThemeLayoutCatalog != null)
            {
                clearWidth = Mathf.Max(clearWidth, bookshelfThemeLayoutCatalog.MaxFocusWidth);
            }

            return clearWidth * 0.5f;
        }

        private float GetBookshelfTurntableRadius()
        {
            return Mathf.Max(0.1f, GetBookshelfMountedCenterZ() - GetBookshelfTurntableZ());
        }

        private Vector3 GetBookshelfFrontSlotPosition()
        {
            return new Vector3(0f, 0f, GetBookshelfTurntableRadius());
        }

        private float GetAttachedBookshelfPairHalfSpacing()
        {
            return Mathf.Max(0.1f, depth * 0.5f + bookshelfSwitchPairGap * 0.5f);
        }

        private float GetAttachedSwitchRestTurntableZ()
        {
            return GetBookshelfMountedCenterZ() + GetAttachedBookshelfPairHalfSpacing();
        }

        private float GetAttachedSwitchSafeTurntableZ()
        {
            return Mathf.Min(
                GetAttachedSwitchRestTurntableZ(),
                GetBackWallFrontSurfaceWorldZ() - GetBookshelfRotationHalfWidth() - bookshelfRotationWallClearance);
        }

        private Vector3 GetAttachedSwitchFrontSlotPosition()
        {
            return new Vector3(0f, 0f, -GetAttachedBookshelfPairHalfSpacing());
        }

        private Vector3 GetAttachedSwitchBackSlotPosition()
        {
            return new Vector3(0f, 0f, GetAttachedBookshelfPairHalfSpacing());
        }

        private bool IsAdvertisementSection(int row, int column)
        {
            return row == rows - 1 && column == columns - 1;
        }

        private void ResetBookshelfToFrontSlot()
        {
            bookshelfTurntableRoot.localPosition = new Vector3(0f, 0f, GetBookshelfTurntableZ());
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

            SpawnAdvertisementBook(bookshelfSections, booksRoot);
        }

        private void SpawnBooks(List<ShelfSection> sections, int variantIndex, Transform targetBooksRoot, bool registerInState)
        {
            if (useServerBookshelfSnapshots)
            {
                return;
            }

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
                if (section == null || section.isAdvertisementSection)
                {
                    continue;
                }

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

        private void SpawnAdvertisementBook(List<ShelfSection> sections, Transform targetBooksRoot)
        {
            DestroyAdvertisementBooksUnderRoot(targetBooksRoot);
            if (sections == null || targetBooksRoot == null || advertisementBookPrefab == null)
            {
                return;
            }

            var targetSection = FindAdvertisementSection(sections);
            if (targetSection == null)
            {
                return;
            }

            var instance = new GameObject(AdvertisementBookInstanceName);
            instance.transform.SetParent(targetBooksRoot, false);
            var visual = Instantiate(advertisementBookPrefab, instance.transform, false);
            visual.name = advertisementBookPrefab.name;
            StripRuntimeInteractionComponents(visual);

            var instanceTransform = instance.transform;
            instanceTransform.localPosition = Vector3.zero;
            instanceTransform.localRotation = Quaternion.Euler(advertisementBookLocalEulerAngles);
            instanceTransform.localScale = Vector3.one;

            SetDynamicHierarchy(instanceTransform);
            CenterChildVisual(instanceTransform);
            FitAdvertisementBookToSection(instanceTransform, targetSection, targetBooksRoot);
            ConfigureAdvertisementBookInteraction(instance, targetSection);
        }

        private ShelfSection FindAdvertisementSection(List<ShelfSection> sections)
        {
            if (sections == null)
            {
                return null;
            }

            for (var i = 0; i < sections.Count; i++)
            {
                var section = sections[i];
                if (section != null && section.isAdvertisementSection)
                {
                    return section;
                }
            }

            return null;
        }

        private void FitAdvertisementBookToSection(Transform adBook, ShelfSection section, Transform boundsSpace)
        {
            if (adBook == null || section == null || boundsSpace == null)
            {
                return;
            }

            if (!TryGetRendererBoundsInLocalSpace(adBook, boundsSpace, out var bounds))
            {
                adBook.localPosition = section.Center;
                return;
            }

            var targetHeight = Mathf.Max(0.01f, section.sectionHeight * Mathf.Clamp(advertisementBookSectionHeightRatio, 0.1f, 1f));
            var targetWidth = Mathf.Max(0.01f, section.Width * Mathf.Clamp(advertisementBookSectionWidthRatio, 0.1f, 1f));
            var targetDepth = Mathf.Max(0.01f, Mathf.Abs(section.Depth) * Mathf.Clamp(advertisementBookSectionDepthRatio, 0.1f, 1f));
            var heightScale = bounds.size.y > 0.0001f ? targetHeight / bounds.size.y : float.PositiveInfinity;
            var widthScale = bounds.size.x > 0.0001f ? targetWidth / bounds.size.x : float.PositiveInfinity;
            var depthScale = bounds.size.z > 0.0001f ? targetDepth / bounds.size.z : float.PositiveInfinity;
            var scale = Mathf.Min(heightScale, Mathf.Min(widthScale, depthScale));
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
            {
                scale = 1f;
            }

            adBook.localScale *= scale;
            if (!TryGetRendererBoundsInLocalSpace(adBook, boundsSpace, out bounds))
            {
                return;
            }

            var bottomInset = Mathf.Min(Mathf.Max(0f, advertisementBookBottomInset), section.sectionHeight * 0.25f);
            var frontInset = Mathf.Min(Mathf.Max(0f, advertisementBookFrontInset), Mathf.Abs(section.Depth) * 0.25f);
            var frontIsMin = section.frontBoundZ <= section.backBoundZ;
            var targetFrontZ = section.frontBoundZ + (frontIsMin ? frontInset : -frontInset);
            var currentFrontZ = frontIsMin ? bounds.min.z : bounds.max.z;
            var offset = new Vector3(
                (section.leftBound + section.rightBound) * 0.5f - bounds.center.x,
                section.shelfTopY + bottomInset - bounds.min.y,
                targetFrontZ - currentFrontZ);

            adBook.localPosition += offset;
        }

        private static void StripRuntimeInteractionComponents(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    Object.DestroyImmediate(colliders[i]);
                }
            }

            var rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (var i = 0; i < rigidbodies.Length; i++)
            {
                if (rigidbodies[i] != null)
                {
                    Object.DestroyImmediate(rigidbodies[i]);
                }
            }

            var bookEntities = root.GetComponentsInChildren<BookEntity>(true);
            for (var i = 0; i < bookEntities.Length; i++)
            {
                if (bookEntities[i] != null)
                {
                    Object.DestroyImmediate(bookEntities[i]);
                }
            }
        }

        private static void ConfigureAdvertisementBookInteraction(GameObject root, ShelfSection section)
        {
            if (root == null || section == null)
            {
                return;
            }

            var rendererBounds = TryGetRendererBoundsInLocalSpace(root.transform, root.transform, out var localBounds);
            var collider = root.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = root.AddComponent<BoxCollider>();
            }

            if (rendererBounds)
            {
                collider.center = localBounds.center;
                collider.size = AbsVector(localBounds.size);
            }

            var entity = root.GetComponent<BookEntity>();
            if (entity == null)
            {
                entity = root.AddComponent<BookEntity>();
            }

            var boundsSize = rendererBounds ? AbsVector(localBounds.size) : collider.size;
            entity.thickness = Mathf.Max(boundsSize.x, 0.02f);
            entity.height = Mathf.Max(boundsSize.y, 0.02f);
            entity.depth = Mathf.Max(boundsSize.z, 0.02f);
            entity.orientation = BookOrientation.Front;
            entity.tintRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
            entity.bookId = BookshelfSponsoredBook.RuntimeBookId;
            BookshelfSponsoredBook.ApplyDisplayMetadata(entity);
            entity.isDecoration = true;
            entity.readingStatus = ReadingStatus.Unread;
            entity.SetSection(section, true);
        }

        private static bool TryGetRendererBoundsInLocalSpace(Transform root, Transform boundsSpace, out Bounds bounds)
        {
            bounds = default;
            if (root == null || boundsSpace == null)
            {
                return false;
            }

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var hasBounds = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                EncapsulateWorldBounds(renderer.bounds, boundsSpace, ref bounds, ref hasBounds);
            }

            return hasBounds;
        }

        private static void EncapsulateWorldBounds(Bounds worldBounds, Transform boundsSpace, ref Bounds bounds, ref bool hasBounds)
        {
            var min = worldBounds.min;
            var max = worldBounds.max;
            EncapsulateBoundsPoint(new Vector3(min.x, min.y, min.z), boundsSpace, ref bounds, ref hasBounds);
            EncapsulateBoundsPoint(new Vector3(min.x, min.y, max.z), boundsSpace, ref bounds, ref hasBounds);
            EncapsulateBoundsPoint(new Vector3(min.x, max.y, min.z), boundsSpace, ref bounds, ref hasBounds);
            EncapsulateBoundsPoint(new Vector3(min.x, max.y, max.z), boundsSpace, ref bounds, ref hasBounds);
            EncapsulateBoundsPoint(new Vector3(max.x, min.y, min.z), boundsSpace, ref bounds, ref hasBounds);
            EncapsulateBoundsPoint(new Vector3(max.x, min.y, max.z), boundsSpace, ref bounds, ref hasBounds);
            EncapsulateBoundsPoint(new Vector3(max.x, max.y, min.z), boundsSpace, ref bounds, ref hasBounds);
            EncapsulateBoundsPoint(new Vector3(max.x, max.y, max.z), boundsSpace, ref bounds, ref hasBounds);
        }

        private static void EncapsulateBoundsPoint(Vector3 worldPoint, Transform boundsSpace, ref Bounds bounds, ref bool hasBounds)
        {
            var localPoint = boundsSpace.InverseTransformPoint(worldPoint);
            if (!hasBounds)
            {
                bounds = new Bounds(localPoint, Vector3.zero);
                hasBounds = true;
                return;
            }

            bounds.Encapsulate(localPoint);
        }

        private void LoadBookCatalog()
        {
            bookCatalogItems = loadBooksUseCase != null
                ? loadBooksUseCase.Execute() ?? System.Array.Empty<BookCatalogItem>()
                : System.Array.Empty<BookCatalogItem>();
            bookCatalogLoaded = true;

            if (Application.isPlaying)
            {
                Debug.Log($"Bookshelf catalog loaded: {bookCatalogItems.Count} item(s).");
            }
        }

        private void EnsureBookCatalog()
        {
            if (bookCatalogLoaded)
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

        public void RefreshBookCoverVisuals(BookEntity book)
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
            if (!SupportsBookPageSubmesh(renderer))
            {
                RebuildBookPageEdges(book);
            }
            if (TryGetBookCoverSkinSprite(book.coverSkinResourcePath, out var coverSkinSprite))
            {
                RebuildBookCoverSkin(book, coverSkinSprite);
                return;
            }

            RebuildBookCoverLabels(book, coverColor);
        }

        public void ApplyBookCoverSkin(BookEntity book, string resourcePath)
        {
            if (book == null)
            {
                return;
            }

            book.coverSkinResourcePath = string.IsNullOrWhiteSpace(resourcePath) ? string.Empty : resourcePath.Trim();
            RefreshBookCoverVisuals(book);
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
            var coverMaterial = CreateBookCoverMaterial(coverColor);
            if (SupportsBookPageSubmesh(renderer))
            {
                RemoveBookPageEdges(book);
                renderer.sharedMaterials = new[] { coverMaterial, GetBookPageEdgeMaterial() };
                LogBookVisualBounds(book, renderer, false);
                return;
            }

            renderer.sharedMaterial = coverMaterial;
        }

        private static bool SupportsBookPageSubmesh(MeshRenderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            var meshFilter = renderer.GetComponent<MeshFilter>();
            return meshFilter != null &&
                   meshFilter.sharedMesh != null &&
                   meshFilter.sharedMesh.subMeshCount >= 2 &&
                   string.Equals(meshFilter.sharedMesh.name, "ProceduralBookVisualMesh", System.StringComparison.Ordinal);
        }

        private void RemoveBookCoverLabels(BookEntity book)
        {
            var existingLabels = book.transform.Find(BookCoverLabelsName);
            if (existingLabels != null)
            {
                DestroySpawnedObject(existingLabels.gameObject);
            }
        }

        private void RemoveBookPageEdges(BookEntity book)
        {
            var existingEdges = book.transform.Find(BookPageEdgesName);
            if (existingEdges != null)
            {
                DestroySpawnedObject(existingEdges.gameObject);
            }
        }

        private void RebuildBookPageEdges(BookEntity book)
        {
            if (book == null)
            {
                return;
            }

            RemoveBookPageEdges(book);
            var material = GetBookPageEdgeMaterial();
            if (material == null)
            {
                return;
            }

            var coverRenderer = book.GetComponent<MeshRenderer>();
            if (coverRenderer == null)
            {
                return;
            }

            var edgesRoot = new GameObject(BookPageEdgesName);
            edgesRoot.transform.SetParent(book.transform, false);
            edgesRoot.transform.localPosition = Vector3.zero;
            edgesRoot.transform.localRotation = Quaternion.identity;
            edgesRoot.transform.localScale = Vector3.one;

            var inset = Mathf.Clamp(BookPageEdgeInset, 0.001f, 0.08f);
            var surfaceThickness = Mathf.Clamp(BookPageEdgeSurfaceThickness, 0.001f, 0.03f);
            var halfSurfaceThickness = surfaceThickness * 0.5f;
            var surfaceMax = 0.5f - BookPageEdgeSurfaceEpsilon;
            var surfaceMin = -0.5f + BookPageEdgeSurfaceEpsilon;
            var coverSpan = Mathf.Max(0.01f, 1f - inset * 2f);

            CreateBookPageEdgeBox(
                edgesRoot.transform,
                "ForePageEdge",
                new Vector3(0f, 0f, surfaceMax - halfSurfaceThickness),
                new Vector3(coverSpan, coverSpan, surfaceThickness),
                material);
            CreateBookPageEdgeBox(
                edgesRoot.transform,
                "TopPageEdge",
                new Vector3(0f, surfaceMax - halfSurfaceThickness, 0f),
                new Vector3(coverSpan, surfaceThickness, coverSpan),
                material);
            CreateBookPageEdgeBox(
                edgesRoot.transform,
                "BottomPageEdge",
                new Vector3(0f, surfaceMin + halfSurfaceThickness, 0f),
                new Vector3(coverSpan, surfaceThickness, coverSpan),
                material);

            LogBookPageEdgeBounds(book, coverRenderer, edgesRoot);
        }

        private void CreateBookPageEdgeBox(Transform parent, string objectName, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            edge.name = objectName;
            edge.transform.SetParent(parent, false);
            edge.transform.localPosition = localPosition;
            edge.transform.localRotation = Quaternion.identity;
            edge.transform.localScale = localScale;

            var collider = edge.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyImmediate(collider);
            }

            var renderer = edge.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private void LogBookPageEdgeBounds(BookEntity book, MeshRenderer coverRenderer, GameObject edgesRoot)
        {
            if (book == null || coverRenderer == null || edgesRoot == null)
            {
                return;
            }

            var pageRenderers = edgesRoot.GetComponentsInChildren<Renderer>(true);
            if (pageRenderers == null || pageRenderers.Length == 0)
            {
                return;
            }

            var coverBounds = CalculateLocalBounds(book.transform, new Renderer[] { coverRenderer });
            var pageBounds = CalculateLocalBounds(book.transform, pageRenderers);
            var protrudes =
                pageBounds.min.x < coverBounds.min.x - 0.0001f ||
                pageBounds.max.x > coverBounds.max.x + 0.0001f ||
                pageBounds.min.y < coverBounds.min.y - 0.0001f ||
                pageBounds.max.y > coverBounds.max.y + 0.0001f ||
                pageBounds.min.z < coverBounds.min.z - 0.0001f ||
                pageBounds.max.z > coverBounds.max.z + 0.0001f;

            Debug.Log($"[BookVisual] coverBounds={coverBounds} pageEdgeBounds={pageBounds}");
            Debug.Log($"[BookVisual] pageEdge protrudes outside cover: {protrudes}");
            if (protrudes)
            {
                Debug.LogWarning($"[BookVisual] Page edge bounds exceeded cover bounds; inset constants need review book={book.title}");
            }
        }

        private void LogBookVisualBounds(BookEntity book, MeshRenderer renderer, bool protrudes)
        {
            if (book == null || renderer == null)
            {
                return;
            }

            var bounds = CalculateLocalBounds(book.transform, new Renderer[] { renderer });
            Debug.Log($"[BookVisual] coverBounds={bounds} pageEdgeBounds={bounds}");
            Debug.Log($"[BookVisual] pageEdge protrudes outside cover: {protrudes}");
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

        private void RebuildBookCoverSkin(BookEntity book, Sprite coverSprite)
        {
            if (book == null || coverSprite == null)
            {
                return;
            }

            var labelsRoot = new GameObject(BookCoverLabelsName);
            labelsRoot.transform.SetParent(book.transform, false);
            labelsRoot.transform.localPosition = Vector3.zero;
            labelsRoot.transform.localRotation = Quaternion.identity;
            labelsRoot.transform.localScale = Vector3.one;

            CreateBookCoverImageCanvas(
                labelsRoot.transform,
                "FrontCoverSkinCanvas",
                coverSprite,
                new Vector3(BookCoverSurfaceOffset, 0.02f, 0f),
                Quaternion.Euler(0f, -90f, 0f));
        }

        public static bool TryGetBookCoverSkinSprite(string resourcePath, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return false;
            }

            var trimmedPath = resourcePath.Trim();
            if (BookCoverSkinSpriteCache.TryGetValue(trimmedPath, out sprite) && sprite != null)
            {
                TouchBookCoverSkinSpriteCacheKey(trimmedPath);
                return true;
            }

            if (BookCoverSkinSpriteCache.ContainsKey(trimmedPath))
            {
                BookCoverSkinSpriteCache.Remove(trimmedPath);
            }

            var texture = Resources.Load<Texture2D>(trimmedPath);
            if (texture == null)
            {
                return false;
            }

            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            if (sprite == null)
            {
                return false;
            }

            AddBookCoverSkinSpriteToCache(trimmedPath, sprite);
            return true;
        }

        public static void ClearBookCoverSkinSpriteCache()
        {
            foreach (var sprite in BookCoverSkinSpriteCache.Values)
            {
                ReleaseCachedCoverSkinSprite(sprite);
            }

            BookCoverSkinSpriteCache.Clear();
            BookCoverSkinSpriteCacheKeys.Clear();
        }

        private static void AddBookCoverSkinSpriteToCache(string resourcePath, Sprite sprite)
        {
            if (string.IsNullOrWhiteSpace(resourcePath) || sprite == null)
            {
                return;
            }

            while (BookCoverSkinSpriteCache.Count >= BookCoverSkinSpriteCacheLimit &&
                   BookCoverSkinSpriteCacheKeys.Count > 0)
            {
                var oldestPath = BookCoverSkinSpriteCacheKeys.Dequeue();
                if (HasNewerBookCoverSkinSpriteCacheTouch(oldestPath))
                {
                    continue;
                }

                if (!BookCoverSkinSpriteCache.TryGetValue(oldestPath, out var oldestSprite))
                {
                    continue;
                }

                BookCoverSkinSpriteCache.Remove(oldestPath);
                ReleaseCachedCoverSkinSprite(oldestSprite);
            }

            BookCoverSkinSpriteCache[resourcePath] = sprite;
            TouchBookCoverSkinSpriteCacheKey(resourcePath);
        }

        private static void TouchBookCoverSkinSpriteCacheKey(string resourcePath)
        {
            if (!string.IsNullOrWhiteSpace(resourcePath))
            {
                BookCoverSkinSpriteCacheKeys.Enqueue(resourcePath);
            }
        }

        private static bool HasNewerBookCoverSkinSpriteCacheTouch(string resourcePath)
        {
            foreach (var queuedPath in BookCoverSkinSpriteCacheKeys)
            {
                if (string.Equals(queuedPath, resourcePath, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ReleaseCachedCoverSkinSprite(Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(sprite);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(sprite);
            }
        }

        private static void CreateBookCoverImageCanvas(
            Transform parent,
            string name,
            Sprite sprite,
            Vector3 localPosition,
            Quaternion localRotation)
        {
            var labelObject = new GameObject(name, typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = localPosition;
            labelObject.transform.localRotation = localRotation;
            labelObject.transform.localScale = GetBookCoverCanvasScale();

            var rect = labelObject.GetComponent<RectTransform>();
            rect.sizeDelta = BookCoverCanvasSize;
            rect.pivot = new Vector2(0.5f, 0.5f);

            var canvas = labelObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 20;

            var imageObject = new GameObject("Skin", typeof(RectTransform));
            imageObject.transform.SetParent(labelObject.transform, false);
            var imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.pivot = new Vector2(0.5f, 0.5f);
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            imageRect.anchoredPosition = Vector2.zero;
            imageRect.localScale = Vector3.one;

            var image = imageObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
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

            var isFrontCover = kind == BookLabelKind.Cover;
            if (isFrontCover)
            {
                CreateFrontCoverTextLabels(labelObject.transform, text, fontSize, color);
                return;
            }

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
            label.fontStyle = FontStyle.Bold;
            label.color = color;
            label.supportRichText = false;
            label.raycastTarget = false;

            var outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.96f, 0.86f, 0.18f);
            outline.effectDistance = isSpineTitle ? new Vector2(0.35f, -0.35f) : new Vector2(0.5f, -0.5f);
        }

        private static void CreateFrontCoverTextLabels(Transform parent, string text, int baseFontSize, Color color)
        {
            SplitFrontCoverText(text, out var title, out var author);
            var titleFontSize = Mathf.Max(baseFontSize + 1, Mathf.RoundToInt(baseFontSize * 1.08f));

            var titleLabel = CreateFrontCoverTextLabel(
                parent,
                "TitleLabel",
                title,
                new Vector2(0f, 0.34f),
                new Vector2(1f, 1f),
                new Vector2(BookCoverCanvasPadding, BookCoverCanvasPadding),
                new Vector2(-BookCoverCanvasPadding, -BookCoverCanvasPadding),
                titleFontSize + 10,
                titleFontSize + 10,
                Mathf.Max(8, titleFontSize - 6),
                FontStyle.Bold,
                color,
                0.86f);

            var authorLabel = CreateFrontCoverTextLabel(
                parent,
                "AuthorLabel",
                author,
                new Vector2(0f, 0f),
                new Vector2(1f, 0.36f),
                new Vector2(BookCoverCanvasPadding, BookCoverCanvasPadding),
                new Vector2(-BookCoverCanvasPadding, -BookCoverCanvasPadding),
                baseFontSize + 3,
                baseFontSize + 3,
                Mathf.Max(8, baseFontSize - 6),
                FontStyle.Normal,
                color,
                0.9f);

            Debug.Log($"[BookCoverText] Focus cover title text={title}");
            Debug.Log($"[BookCoverText] title fontSize before={baseFontSize}");
            Debug.Log($"[BookCoverText] title fontSize after={titleLabel.fontSize}");
            Debug.Log($"[BookCoverText] title bold applied={titleLabel.fontStyle == FontStyle.Bold}");
            Debug.Log($"[BookCoverText] author fontSize unchanged={authorLabel.fontSize}");
            Debug.Log($"[BookCoverText] title autoSize={titleLabel.resizeTextForBestFit}");
        }

        private static Text CreateFrontCoverTextLabel(
            Transform parent,
            string name,
            string text,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            int fontSize,
            int resizeTextMaxSize,
            int resizeTextMinSize,
            FontStyle fontStyle,
            Color color,
            float lineSpacing)
        {
            var textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = anchorMin;
            textRect.anchorMax = anchorMax;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = offsetMin;
            textRect.offsetMax = offsetMax;
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
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = resizeTextMinSize;
            label.resizeTextMaxSize = resizeTextMaxSize;
            label.lineSpacing = lineSpacing;
            label.fontStyle = fontStyle;
            label.color = color;
            label.supportRichText = false;
            label.raycastTarget = false;

            var outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.96f, 0.86f, 0.18f);
            outline.effectDistance = new Vector2(0.5f, -0.5f);

            return label;
        }

        private static void SplitFrontCoverText(string text, out string title, out string author)
        {
            title = text ?? string.Empty;
            author = string.Empty;

            var separatorIndex = title.IndexOf("\n\n", System.StringComparison.Ordinal);
            if (separatorIndex < 0)
            {
                return;
            }

            author = title.Substring(separatorIndex + 2);
            title = title.Substring(0, separatorIndex);
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

        private Material GetBookPageEdgeMaterial()
        {
            if (bookPageEdgeMaterial != null)
            {
                return bookPageEdgeMaterial;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                return null;
            }

            var pageColor = new Color(1f, 0.985f, 0.94f, 1f);
            bookPageEdgeMaterial = new Material(shader)
            {
                name = "BookPageEdgeMaterial"
            };

            var texture = LoadBookPageEdgeTexture();
            if (texture != null)
            {
                ApplyBookPageEdgeTextureSettings(bookPageEdgeMaterial, texture);
            }

            bookPageEdgeMaterial.color = pageColor;
            if (bookPageEdgeMaterial.HasProperty("_BaseColor"))
            {
                bookPageEdgeMaterial.SetColor("_BaseColor", pageColor);
            }

            if (bookPageEdgeMaterial.HasProperty("_Metallic"))
            {
                bookPageEdgeMaterial.SetFloat("_Metallic", 0f);
            }

            if (bookPageEdgeMaterial.HasProperty("_Smoothness"))
            {
                bookPageEdgeMaterial.SetFloat("_Smoothness", 0.10f);
            }

            LogBookPageEdgeMaterialSettings(bookPageEdgeMaterial, texture);

            return bookPageEdgeMaterial;
        }

        private static void ApplyBookPageEdgeTextureSettings(Material material, Texture texture)
        {
            if (material == null || texture == null)
            {
                return;
            }

            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            var tiling = new Vector2(3f, 1f);
            var offset = Vector2.zero;

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", tiling);
                material.SetTextureOffset("_BaseMap", offset);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
                material.SetTextureScale("_MainTex", tiling);
                material.SetTextureOffset("_MainTex", offset);
            }

            material.mainTexture = texture;
            material.mainTextureScale = tiling;
            material.mainTextureOffset = offset;
        }

        private static void LogBookPageEdgeMaterialSettings(Material material, Texture texture)
        {
            if (material == null)
            {
                return;
            }

            var tiling = Vector2.one;
            if (material.HasProperty("_BaseMap"))
            {
                tiling = material.GetTextureScale("_BaseMap");
            }
            else if (material.HasProperty("_MainTex"))
            {
                tiling = material.GetTextureScale("_MainTex");
            }

            var baseColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
            Debug.Log($"[BookTexture] PageEdge material={material.name}");
            Debug.Log($"[BookTexture] PageEdge texture={(texture != null ? texture.name : "null")}");
            Debug.Log($"[BookTexture] PageEdge tiling={tiling}");
            Debug.Log($"[BookTexture] PageEdge baseColor={baseColor}");
        }

        private Texture2D LoadBookPageEdgeTexture()
        {
            var resourceTexture = Resources.Load<Texture2D>(BookPageEdgeTextureResourcePath);
            if (resourceTexture != null)
            {
                Debug.Log($"[BookTexture] PageEdge texture source=Resources path={BookPageEdgeTextureResourcePath}");
                return resourceTexture;
            }

            Debug.LogWarning($"[BookTexture] PageEdge texture missing at Resources path={BookPageEdgeTextureResourcePath}; using generated fallback.");
            return GetGeneratedBookPageEdgeTexture();
        }

        private Texture2D GetGeneratedBookPageEdgeTexture()
        {
            if (generatedBookPageEdgeTexture != null)
            {
                return generatedBookPageEdgeTexture;
            }

            const int textureWidth = 64;
            const int textureHeight = 64;
            var paper = new Color(0.91f, 0.86f, 0.74f, 1f);
            var line = new Color(0.62f, 0.55f, 0.43f, 1f);
            generatedBookPageEdgeTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
            {
                name = "GeneratedBookPageEdgeTexture",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            for (var y = 0; y < textureHeight; y++)
            {
                for (var x = 0; x < textureWidth; x++)
                {
                    var isLine = x % 6 == 0 || x % 17 == 0;
                    var subtleNoise = ((x * 13 + y * 7) % 11) / 255f;
                    var color = isLine ? line : paper;
                    color.r = Mathf.Clamp01(color.r + subtleNoise);
                    color.g = Mathf.Clamp01(color.g + subtleNoise);
                    color.b = Mathf.Clamp01(color.b + subtleNoise);
                    generatedBookPageEdgeTexture.SetPixel(x, y, color);
                }
            }

            generatedBookPageEdgeTexture.Apply(false, false);
            return generatedBookPageEdgeTexture;
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
            return new Color(0.10f, 0.10f, 0.10f, 1f);
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
            var bookObject = new GameObject($"Shelf{variantIndex}_Book_{sectionIndex}_{bookIndex}");
            var meshFilter = bookObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = CreateBookVisualMesh();
            var renderer = bookObject.AddComponent<MeshRenderer>();
            var collider = bookObject.AddComponent<BoxCollider>();
            collider.size = Vector3.one;
            bookObject.name = $"Shelf{variantIndex}_Book_{sectionIndex}_{bookIndex}";
            bookObject.transform.SetParent(targetBooksRoot, false);

            var preset = SelectBookSizePreset(bookObject.name);
            bookObject.transform.localScale = new Vector3(preset.Thickness, preset.Height, preset.Depth);

            var coverColor = ResolveBookCoverColorFromSeed(bookObject.name);
            renderer.sharedMaterials = new[] { CreateBookCoverMaterial(coverColor), GetBookPageEdgeMaterial() };

            var entity = bookObject.AddComponent<BookEntity>();
            entity.tintRenderers = new[] { renderer };
            entity.Initialize(bookObject.name, preset.Thickness, preset.Height, preset.Depth, coverColor);
            entity.sizePresetId = preset.Id;
            Debug.Log($"[BookshelfPlacement] Selected preset: {preset.Id} for bookId={entity.bookId}");
            return entity;
        }

        private static BookSizePreset SelectBookSizePreset(string deterministicSeed, string sizePresetId = null)
        {
            return BookSizePresets.Resolve(sizePresetId, deterministicSeed);
        }

        private static void ApplyPresetToBook(BookEntity book, string deterministicSeed, string sizePresetId = null)
        {
            if (book == null)
            {
                return;
            }

            var preset = SelectBookSizePreset(deterministicSeed, sizePresetId);
            book.ApplySizePreset(preset);
            Debug.Log($"[BookshelfPlacement] Selected preset: {preset.Id} for bookId={book.bookId}");
            Debug.Log($"[BookPlacement] Selected preset={preset.Id} height={preset.Height:F3} thickness={preset.Thickness:F3}");
        }

        private static Mesh CreateBookVisualMesh()
        {
            var mesh = new Mesh { name = "ProceduralBookVisualMesh" };
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var coverTriangles = new List<int>();
            var pageTriangles = new List<int>();

            AddBookFace(vertices, uvs, coverTriangles,
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(0.5f, -0.5f, 0.5f));
            AddBookFace(vertices, uvs, coverTriangles,
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, -0.5f));
            AddBookFace(vertices, uvs, coverTriangles,
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f));

            AddBookFaceRotatedUv(vertices, uvs, pageTriangles,
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f));
            AddBookFaceRotatedUv(vertices, uvs, pageTriangles,
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, -0.5f));
            AddBookFaceRotatedUv(vertices, uvs, pageTriangles,
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, 0.5f));

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(coverTriangles, 0);
            mesh.SetTriangles(pageTriangles, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddBookFace(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 bottomLeft,
            Vector3 topLeft,
            Vector3 topRight,
            Vector3 bottomRight)
        {
            var start = vertices.Count;
            vertices.Add(bottomLeft);
            vertices.Add(topLeft);
            vertices.Add(topRight);
            vertices.Add(bottomRight);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f));
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static void AddBookFaceRotatedUv(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 bottomLeft,
            Vector3 topLeft,
            Vector3 topRight,
            Vector3 bottomRight)
        {
            var start = vertices.Count;
            vertices.Add(bottomLeft);
            vertices.Add(topLeft);
            vertices.Add(topRight);
            vertices.Add(bottomRight);
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f));
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
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

            index = ResolveNavigableBookshelfIndex(index, out var fallbackUsed);
            LogBookshelfNavigationState("show_bookshelf", index, fallbackUsed);
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
                GetNavigableBookshelfCount() <= 1)
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
                booksRoot == null)
            {
                return false;
            }

            if (bookshelfCount > 0 && bookshelfSections.Count > 0)
            {
                SaveActiveBookshelfLayoutSnapshot();
            }

            var newIndex = bookshelfCount;
            EnsureBookshelfDataForCurrentOrder();
            bookshelfCount++;
            bookshelfLayoutSnapshots[newIndex] = CreateEmptyBookshelfLayoutSnapshot();
            bookshelfNames[newIndex] = GetDefaultBookshelfName(newIndex);
            activeBookshelfIndex = newIndex;
            pendingBookshelfIndex = -1;
            MarkNavigableBookshelfIndicesDirty();
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
            MarkNavigableBookshelfIndicesDirty();
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

        private int GetNavigableBookshelfCount()
        {
            BuildNavigableBookshelfIndices();
            return navigableBookshelfIndices.Count;
        }

        private int ResolveNavigableBookshelfIndex(int requestedIndex, out bool fallbackUsed)
        {
            BuildNavigableBookshelfIndices();

            var wrappedRequestedIndex = GetWrappedBookshelfIndex(requestedIndex);
            if (navigableBookshelfIndices.Count == 0)
            {
                fallbackUsed = false;
                return wrappedRequestedIndex;
            }

            if (navigableBookshelfIndices.Contains(wrappedRequestedIndex))
            {
                fallbackUsed = false;
                return wrappedRequestedIndex;
            }

            fallbackUsed = true;
            var wrappedDisplayIndex = GetWrappedBookshelfIndex(DisplayBookshelfIndex);
            if (navigableBookshelfIndices.Contains(wrappedDisplayIndex))
            {
                return wrappedDisplayIndex;
            }

            return navigableBookshelfIndices[0];
        }

        private int GetNavigableDisplayBookshelfIndex()
        {
            BuildNavigableBookshelfIndices();
            var displayIndex = GetWrappedBookshelfIndex(DisplayBookshelfIndex);
            var position = navigableBookshelfIndices.IndexOf(displayIndex);
            return position >= 0 ? position : 0;
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

            MarkNavigableBookshelfIndicesDirty();
            RebuildActiveBookshelfVariant(index);
            BookshelfSwitchStateChanged?.Invoke();
            return true;
        }

        public bool ResetAllBookshelfDesignStyles()
        {
            if (IsBookshelfSwitching ||
                booksRoot == null ||
                bookshelfSections.Count == 0)
            {
                return false;
            }

            if (bookshelfDesignStyles.Count == 0)
            {
                return true;
            }

            SaveActiveBookshelfLayoutSnapshot();
            bookshelfDesignStyles.Clear();
            MarkNavigableBookshelfIndicesDirty();
            RebuildActiveBookshelfVariant(GetWrappedBookshelfIndex(activeBookshelfIndex));
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
                Debug.Log("[BookshelfPlacement] No available cell, creating new bookshelf");
                Debug.Log("[BookPlacement] No cell available, create new bookshelf");
                DestroySpawnedObject(book.gameObject);
                if (!AddBookshelf())
                {
                    return null;
                }

                var newBook = CreateConfiguredUserBook(title, author, publisher, description, orientation, isDecoration);
                if (newBook == null)
                {
                    return null;
                }

                var newSection = FindFirstSectionWithSpace(newBook);
                if (newSection == null)
                {
                    DestroySpawnedObject(newBook.gameObject);
                    return null;
                }

                Debug.Log($"[BookshelfPlacement] Created bookshelf={activeBookshelfIndex} and placed book");
                Debug.Log($"[BookPlacement] Created bookshelf={activeBookshelfIndex}");
                return TryPlaceConfiguredBookInSection(newBook, newSection, newSection.books.Count, true);
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
                targetSection = FindFirstSectionWithSpace(book);
                insertIndex = targetSection != null ? targetSection.books.Count : 0;
                if (targetSection == null)
                {
                    Debug.Log("[BookshelfPlacement] No available cell, creating new bookshelf");
                    Debug.Log("[BookPlacement] No cell available, create new bookshelf");
                    DestroySpawnedObject(book.gameObject);
                    if (!AddBookshelf())
                    {
                        return null;
                    }

                    book = CreateConfiguredUserBook(title, author, publisher, description, orientation, isDecoration);
                    if (book == null)
                    {
                        return null;
                    }

                    targetSection = FindFirstSectionWithSpace(book);
                    insertIndex = targetSection != null ? targetSection.books.Count : 0;
                    if (targetSection == null)
                    {
                        DestroySpawnedObject(book.gameObject);
                        return null;
                    }

                    Debug.Log($"[BookshelfPlacement] Created bookshelf={activeBookshelfIndex} and placed book");
                    Debug.Log($"[BookPlacement] Created bookshelf={activeBookshelfIndex}");
                }
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
            return new Vector3(
                BookSizePresets.Medium.Thickness,
                BookSizePresets.Medium.Height,
                BookSizePresets.Medium.Depth);
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
            ApplyPresetToBook(book, CreateBookSizeSeed(book));
            Debug.Log($"[BookPlacement] Add book title={book.title}");
            Debug.Log($"[BookPlacement] Selected preset={book.sizePresetId} height={book.height:F3} thickness={book.thickness:F3}");
            book.SetOrientation(orientation);
            RefreshBookCoverVisuals(book);
            return book;
        }

        private static string CreateBookSizeSeed(BookEntity book)
        {
            if (book == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(book.title))
            {
                return !string.IsNullOrWhiteSpace(book.author)
                    ? $"{book.title}|{book.author}"
                    : book.title;
            }

            if (!string.IsNullOrWhiteSpace(book.bookId))
            {
                return book.bookId;
            }

            return book.gameObject != null ? book.gameObject.name : string.Empty;
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

            targetSection.alignment = ShelfSectionAlignment.Left;
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

            var cellIndex = targetSection.row * columns + targetSection.column;
            var offsetXInCell = ComputeOffsetXInCellWithStateSpacing(targetSection, book);
            book.SetPlacementInfo(activeBookshelfIndex, cellIndex, offsetXInCell);
            Debug.Log($"[BookshelfPlacement] Placed book in bookshelf={activeBookshelfIndex} cell={cellIndex} offset={offsetXInCell:F3}");
            Debug.Log($"[BookPlacement] Placed bookshelf={activeBookshelfIndex} cell={cellIndex} x={book.transform.localPosition.x:F3}");
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
            if (section != null && candidateBook != null && !CanSectionFitCandidate(section, candidateBook, state != null ? state.BookPadding : 0.01f))
            {
                section = null;
            }
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
            var startScale = targetLocalScale;
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

                if (section.isAdvertisementSection)
                {
                    continue;
                }

                var spacing = state != null ? state.BookPadding : 0.01f;
                var usedWidth = ComputeUsedWidth(section, spacing);
                var requiredWidth = candidate != null
                    ? candidate.GetLayoutFootprint() + GetRequiredPairSpacing(section, candidate, spacing)
                    : 0f;
                var edgePadding = BookFootprintUtility.GetEdgePadding(spacing);
                var capacity = Mathf.Max(0f, section.Width - edgePadding * 2f);
                Debug.Log($"[BookshelfPlacement] Try cell={i} usedWidth={usedWidth:F3} requiredWidth={requiredWidth:F3} capacity={capacity:F3}");
                Debug.Log($"[BookPlacement] Try bookshelf={activeBookshelfIndex} cell={i} usedWidth={usedWidth:F3} required={requiredWidth:F3} capacity={capacity:F3}");

                if (candidate == null || CanSectionFitCandidate(section, candidate, spacing))
                {
                    return section;
                }

                Debug.Log("[BookPlacement] Cell full, move next cell");
            }

            return null;
        }

        private static bool CanSectionFitCandidate(ShelfSection section, BookEntity candidate, float spacing)
        {
            if (section == null || section.isAdvertisementSection || candidate == null)
            {
                return false;
            }

            var safeSpacing = Mathf.Max(0f, spacing);
            var usedWidth = ComputeUsedWidth(section, safeSpacing);
            var requiredWidth = candidate.GetLayoutFootprint() + GetRequiredPairSpacing(section, candidate, safeSpacing);
            var edgePadding = BookFootprintUtility.GetEdgePadding(safeSpacing);
            var capacity = Mathf.Max(0f, section.Width - edgePadding * 2f);
            var remaining = capacity - usedWidth;
            var canFit = usedWidth + requiredWidth <= capacity;
            if (!canFit)
            {
                Debug.Log($"[BookshelfFit] Reject book: remaining={remaining:F3} required={requiredWidth:F3} orientation={candidate.orientation} spacing={GetRequiredPairSpacing(section, candidate, safeSpacing):F3} edgePadding={edgePadding:F3}");
            }

            return canFit;
        }

        private static float ComputeUsedWidth(ShelfSection section, float spacing)
        {
            if (section == null)
            {
                return 0f;
            }

            var usedWidth = 0f;
            BookEntity previousBook = null;
            for (var i = section.books.Count - 1; i >= 0; i--)
            {
                if (section.books[i] == null)
                {
                    section.books.RemoveAt(i);
                }
            }

            for (var i = 0; i < section.books.Count; i++)
            {
                var book = section.books[i];
                if (book == null)
                {
                    continue;
                }

                if (previousBook != null)
                {
                    usedWidth += BookFootprintUtility.GetPairSpacing(spacing, previousBook, book);
                }

                usedWidth += book.GetLayoutFootprint();
                previousBook = book;
            }

            return usedWidth;
        }

        private static float GetRequiredPairSpacing(ShelfSection section, BookEntity candidate, float spacing)
        {
            var lastBook = GetLastBook(section);
            return lastBook != null
                ? BookFootprintUtility.GetPairSpacing(spacing, lastBook, candidate)
                : 0f;
        }

        private static BookEntity GetLastBook(ShelfSection section)
        {
            if (section == null)
            {
                return null;
            }

            for (var i = section.books.Count - 1; i >= 0; i--)
            {
                if (section.books[i] != null)
                {
                    return section.books[i];
                }
            }

            return null;
        }

        private static float ComputeOffsetXInCell(ShelfSection section, BookEntity targetBook)
        {
            if (section == null || targetBook == null)
            {
                return 0f;
            }

            var spacing = 0.01f;
            var offset = BookFootprintUtility.GetEdgePadding(spacing);
            BookEntity previousBook = null;
            for (var i = 0; i < section.books.Count; i++)
            {
                var book = section.books[i];
                if (book == null)
                {
                    continue;
                }

                if (book == targetBook)
                {
                    return offset;
                }

                offset += book.GetLayoutFootprint();
                previousBook = book;
                var nextBook = FindNextBook(section.books, i + 1);
                if (nextBook != null)
                {
                    offset += BookFootprintUtility.GetPairSpacing(spacing, previousBook, nextBook);
                }
            }

            return offset;
        }

        private float ComputeOffsetXInCellWithStateSpacing(ShelfSection section, BookEntity targetBook)
        {
            return ComputeOffsetXInCell(section, targetBook, state != null ? state.BookPadding : 0.01f);
        }

        private static float ComputeOffsetXInCell(ShelfSection section, BookEntity targetBook, float spacing)
        {
            if (section == null || targetBook == null)
            {
                return 0f;
            }

            var offset = BookFootprintUtility.GetEdgePadding(spacing);
            BookEntity previousBook = null;
            for (var i = 0; i < section.books.Count; i++)
            {
                var book = section.books[i];
                if (book == null)
                {
                    continue;
                }

                if (book == targetBook)
                {
                    return offset;
                }

                offset += book.GetLayoutFootprint();
                previousBook = book;
                var nextBook = FindNextBook(section.books, i + 1);
                if (nextBook != null)
                {
                    offset += BookFootprintUtility.GetPairSpacing(spacing, previousBook, nextBook);
                }
            }

            return offset;
        }

        private static BookEntity FindNextBook(List<BookEntity> books, int startIndex)
        {
            if (books == null)
            {
                return null;
            }

            for (var i = startIndex; i < books.Count; i++)
            {
                if (books[i] != null)
                {
                    return books[i];
                }
            }

            return null;
        }

        private bool StartBookshelfSwitch(int direction)
        {
            BuildNavigableBookshelfIndices();
            if (navigableBookshelfIndices.Count <= 1)
            {
                LogBookshelfNavigationState("switch_single_candidate", GetWrappedBookshelfIndex(activeBookshelfIndex), true);
                Debug.Log("[BookshelfNav] switch skipped reason=single_candidate");
                return false;
            }

            var activeWrappedIndex = GetWrappedBookshelfIndex(activeBookshelfIndex);
            var activeCandidatePosition = navigableBookshelfIndices.IndexOf(activeWrappedIndex);
            if (activeCandidatePosition < 0)
            {
                activeCandidatePosition = 0;
            }

            var nextCandidatePosition = (activeCandidatePosition + (direction > 0 ? 1 : -1) + navigableBookshelfIndices.Count) % navigableBookshelfIndices.Count;
            var targetIndex = navigableBookshelfIndices[nextCandidatePosition];
            LogBookshelfNavigationState(direction > 0 ? "switch_next" : "switch_previous", targetIndex, false);
            if (targetIndex == activeWrappedIndex)
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

            yield return AnimateAttachedBookshelfSwitch(signedFullAngle, BookshelfSwitchRotationDuration);

            SwapActiveBookshelfToTransitionTarget(targetIndex);
            FinishBookshelfSwitching();
        }

        private void PrepareTransitionBookshelf(int targetIndex)
        {
            DestroyTransitionBookshelf();
            ApplyAttachedSwitchLayout();

            transitionBookshelfRoot = new GameObject("BookshelfTransitionRoot").transform;
            transitionBookshelfRoot.SetParent(bookshelfTurntableRoot, false);
            transitionBookshelfRoot.localPosition = GetAttachedSwitchBackSlotPosition();
            transitionBookshelfRoot.localRotation = Quaternion.Euler(0f, BookshelfSwitchFullAngle, 0f);
            transitionBookshelfRoot.localScale = bookshelfRootBaseScale;

            transitionBooksRoot = new GameObject("BooksRoot").transform;
            transitionBooksRoot.SetParent(transitionBookshelfRoot, false);

            BuildBookshelfShell(transitionBookshelfRoot, transitionBookshelfSections, false, GetBookshelfMaterial(targetIndex), ResolveRenderedBookshelfDesignStyle(targetIndex));
            SpawnBooks(transitionBookshelfSections, targetIndex, transitionBooksRoot, false);
            RestoreBookshelfLayoutSnapshot(targetIndex, transitionBookshelfSections, transitionBooksRoot);
            LayoutBookshelfSectionsImmediately(transitionBookshelfSections);
            SpawnAdvertisementBook(transitionBookshelfSections, transitionBooksRoot);
            TransitionShellPrepared?.Invoke(transitionBookshelfRoot);
            transitionBookshelfRoot.gameObject.SetActive(false);
        }

        private void ApplyAttachedSwitchLayout()
        {
            if (bookshelfTurntableRoot == null || bookshelfRoot == null)
            {
                return;
            }

            bookshelfTurntableRoot.localPosition = new Vector3(0f, 0f, GetAttachedSwitchRestTurntableZ());
            bookshelfTurntableRoot.localRotation = Quaternion.identity;
            bookshelfTurntableRoot.localScale = turntableRootBaseScale;
            bookshelfRoot.localPosition = GetAttachedSwitchFrontSlotPosition();
            bookshelfRoot.localRotation = Quaternion.identity;
            bookshelfRoot.localScale = bookshelfRootBaseScale;
        }

        private void SwapActiveBookshelfToTransitionTarget(int targetIndex)
        {
            EnsureActiveBookshelfVisible();
            activeBookshelfIndex = targetIndex;
            RebuildActiveBookshelfVariant(activeBookshelfIndex);
            ResetBookshelfTransformForLayout();
            DestroyTransitionBookshelf();
        }

        private void RebuildActiveBookshelfVariant(int variantIndex)
        {
            var resolvedVariantIndex = ResolveNavigableBookshelfIndex(variantIndex, out var fallbackUsed);
            activeBookshelfIndex = resolvedVariantIndex;
            pendingBookshelfIndex = -1;
            LogBookshelfNavigationState("rebuild_active_variant", resolvedVariantIndex, fallbackUsed);
            ClearBookshelfShellChildren();
            ClearBookshelfBooks();
            BuildBookshelfShell(bookshelfRoot, bookshelfSections, true, GetBookshelfMaterial(resolvedVariantIndex), ResolveRenderedBookshelfDesignStyle(resolvedVariantIndex));
            if (state != null)
            {
                state.ResetSections(bookshelfSections);
            }

            SpawnBooks(bookshelfSections, resolvedVariantIndex, booksRoot, true);
            RestoreBookshelfLayoutSnapshot(resolvedVariantIndex, bookshelfSections, booksRoot);
            state.LayoutAll(state, false);
            SpawnAdvertisementBook(bookshelfSections, booksRoot);

            BookshelfShellRebuilt?.Invoke();
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
                if (source.isAdvertisementSection)
                {
                    snapshot.Sections.Add(sectionSnapshot);
                    continue;
                }

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
                        book.coverSkinResourcePath,
                        book.isDecoration,
                        book.bookshelfItemId,
                        book.userBookId,
                        book.bookshelfId,
                        book.bookshelfSectionId,
                        book.readingRecordId,
                        book.reportVisibility,
                        book.reportTitle,
                        book.reportBody,
                        book.reportUpdatedLabel,
                        averageRating: book.averageRating,
                        totalLikeCount: ResolveSnapshotLikeCount(book.totalLikeCount, book.isLiked),
                        reviewCount: book.reviewCount,
                        hasAggregateBookStats: book.hasAggregateBookStats,
                        bookshelfIndex: book.bookshelfIndex,
                        cellIndex: book.cellIndex,
                        offsetXInCell: book.offsetXInCell,
                        sizePresetId: book.sizePresetId));
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
                if (IsAdvertisementSection(section.Row, section.Column))
                {
                    continue;
                }

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
                var section = ResolveServerBookSectionSnapshot(snapshot, row, column, source);
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
                    string.Empty,
                    false,
                    source.BookshelfItemId,
                    source.UserBookId,
                    shelf.BookshelfId,
                    section.SectionId,
                    source.ReadingRecordId,
                    source.ReportVisibility,
                    source.ReportTitle,
                    source.ReportBody,
                    source.ReportUpdatedLabel,
                    averageRating: source.AverageRating,
                    totalLikeCount: ResolveSnapshotLikeCount(source.TotalLikeCount, source.IsLiked),
                    reviewCount: source.ReviewCount,
                    hasAggregateBookStats: source.HasAggregateBookStats,
                    bookshelfIndex: variantIndex,
                    cellIndex: row * columns + column,
                    sizePresetId: SelectBookSizePreset(CreateServerBookKey(variantIndex, source, section.Books.Count)).Id));
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

        private SectionLayoutSnapshot ResolveServerBookSectionSnapshot(
            BookshelfLayoutSnapshot snapshot,
            int row,
            int column,
            BookshelfRuntimeBookSnapshot source)
        {
            if (!IsAdvertisementSection(row, column))
            {
                return FindSectionSnapshotByGrid(snapshot, row, column);
            }

            var fallback = FindNearestAvailableUserSectionSnapshot(snapshot, row, column);
            if (fallback == null)
            {
                Debug.LogWarning(
                    $"[Bookshelf] Server book at reserved advertisement section could not be relocated: bookId={source?.BookId} itemId={source?.BookshelfItemId}");
                return null;
            }

            Debug.LogWarning(
                $"[Bookshelf] Server book at reserved advertisement section was relocated to row={fallback.Row}, column={fallback.Column}: bookId={source?.BookId} itemId={source?.BookshelfItemId}");
            return fallback;
        }

        private SectionLayoutSnapshot FindNearestAvailableUserSectionSnapshot(BookshelfLayoutSnapshot snapshot, int row, int column)
        {
            if (snapshot == null)
            {
                return null;
            }

            SectionLayoutSnapshot best = null;
            var bestScore = int.MaxValue;
            for (var i = 0; i < snapshot.Sections.Count; i++)
            {
                var section = snapshot.Sections[i];
                if (section == null || IsAdvertisementSection(section.Row, section.Column))
                {
                    continue;
                }

                var distance = Mathf.Abs(section.Row - row) + Mathf.Abs(section.Column - column);
                var score = section.Books.Count * 100 + distance;
                if (score < bestScore)
                {
                    best = section;
                    bestScore = score;
                }
            }

            return best;
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

            var isLiked = userBook?.IsLiked ?? false;
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
                isLiked,
                readingRecord == null || readingRecord.VisibilityScope == VisibilityScope.Public,
                averageRating: book != null ? (float)book.AverageRating : 0f,
                totalLikeCount: book != null ? ResolveSnapshotLikeCount((int)book.LikeCount, isLiked) : 0,
                reviewCount: book != null ? Mathf.Max(0, book.RatingCount) : 0,
                hasAggregateBookStats: book != null,
                sizePresetId: SelectBookSizePreset(bookKey).Id);
        }

        private static int ResolveSnapshotLikeCount(int likeCount, bool isLiked)
        {
            var count = Mathf.Max(0, likeCount);
            return isLiked ? Mathf.Max(1, count) : count;
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
            if (!useServerBookshelfSnapshots)
            {
                EnsureBookCatalog();
            }

            EnsureBookshelfNamesForCurrentOrder();
            for (var i = 0; i < bookshelfCount; i++)
            {
                if (!bookshelfLayoutSnapshots.ContainsKey(i))
                {
                    bookshelfLayoutSnapshots[i] = useServerBookshelfSnapshots
                        ? CreateEmptyBookshelfLayoutSnapshot()
                        : CreateDefaultBookshelfLayoutSnapshot(i);
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
                if (targetSection.isAdvertisementSection)
                {
                    continue;
                }

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

                    var resolvedSection = ResolveRestoredBookSection(targetSections, targetSection, book);
                    if (resolvedSection != targetSection)
                    {
                        Debug.LogWarning(
                            $"[BookPlacement] Restored book moved to avoid overlap title={book.title} " +
                            $"fromCell={targetSection.row * columns + targetSection.column} " +
                            $"toCell={(resolvedSection != null ? resolvedSection.row * columns + resolvedSection.column : -1)}");
                    }

                    if (resolvedSection == null)
                    {
                        resolvedSection = targetSection;
                        Debug.LogWarning($"[BookPlacement] No restore cell available; keeping original cell title={book.title}");
                    }

                    book.SetSection(resolvedSection);
                    resolvedSection.books.Add(book);
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

        private ShelfSection ResolveRestoredBookSection(List<ShelfSection> sections, ShelfSection requestedSection, BookEntity book)
        {
            if (sections == null || requestedSection == null || book == null)
            {
                return requestedSection;
            }

            var spacing = state != null ? state.BookPadding : 0.01f;
            var requestedIndex = GetSectionLinearIndex(requestedSection);
            for (var i = 0; i < sections.Count; i++)
            {
                var section = sections[i];
                if (section == null || section.isAdvertisementSection)
                {
                    continue;
                }

                if (GetSectionLinearIndex(section) < requestedIndex)
                {
                    continue;
                }

                var usedWidth = ComputeUsedWidth(section, spacing);
                var requiredWidth = book.GetLayoutFootprint() + GetRequiredPairSpacing(section, book, spacing);
                var edgePadding = BookFootprintUtility.GetEdgePadding(spacing);
                var capacity = Mathf.Max(0f, section.Width - edgePadding * 2f);
                Debug.Log($"[BookPlacement] Restore try bookshelf={activeBookshelfIndex} cell={GetSectionLinearIndex(section)} usedWidth={usedWidth:F3} required={requiredWidth:F3} capacity={capacity:F3}");

                if (CanSectionFitCandidate(section, book, spacing))
                {
                    return section;
                }
            }

            return null;
        }

        private int GetSectionLinearIndex(ShelfSection section)
        {
            return section != null ? section.row * columns + section.column : -1;
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

            var previousReadingRecordId = book.readingRecordId;
            var previousReportTitle = book.reportTitle;
            var previousReportBody = book.reportBody;
            var previousReportUpdatedLabel = book.reportUpdatedLabel;
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
            book.averageRating = Mathf.Clamp(placement.AverageRating, 0f, 5f);
            book.totalLikeCount = ResolveSnapshotLikeCount(placement.TotalLikeCount, placement.IsLiked);
            book.reviewCount = Mathf.Max(0, placement.ReviewCount);
            book.hasAggregateBookStats = placement.HasAggregateBookStats;
            book.rating = Mathf.RoundToInt(book.hasAggregateBookStats ? book.averageRating : book.ownerRating);
            book.readingStatus = placement.ReadingStatus;
            book.isRead = placement.ReadingStatus == ReadingStatus.Read;
            book.isLiked = placement.IsLiked;
            book.isPublic = placement.IsPublic;
            book.reportVisibility = placement.ReportVisibility;
            ApplySnapshotReportFields(
                book,
                placement,
                previousReadingRecordId,
                previousReportTitle,
                previousReportBody,
                previousReportUpdatedLabel);
            book.isPaidCoverOwned = placement.IsPaidCoverOwned;
            book.coverSkinResourcePath = placement.CoverSkinResourcePath;
            book.isDecoration = placement.IsDecoration;
            ApplyPresetToBook(book, CreateBookSizeSeed(book), placement.SizePresetId);
            book.SetPlacementInfo(
                placement.BookshelfIndex >= 0 ? placement.BookshelfIndex : activeBookshelfIndex,
                placement.CellIndex,
                placement.OffsetXInCell);
            RefreshBookCoverVisuals(book);
        }

        private static void ApplySnapshotReportFields(
            BookEntity book,
            BookPlacementSnapshot placement,
            string previousReadingRecordId,
            string previousReportTitle,
            string previousReportBody,
            string previousReportUpdatedLabel)
        {
            if (book == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(placement.ReadingRecordId))
            {
                book.reportTitle = string.Empty;
                book.reportBody = string.Empty;
                book.reportUpdatedLabel = string.Empty;
                return;
            }

            var canReusePreviousReport =
                !string.IsNullOrWhiteSpace(previousReadingRecordId) &&
                string.Equals(previousReadingRecordId, placement.ReadingRecordId, System.StringComparison.Ordinal);
            book.reportTitle = !string.IsNullOrWhiteSpace(placement.ReportTitle)
                ? placement.ReportTitle
                : canReusePreviousReport ? previousReportTitle : book.reportTitle;
            book.reportBody = !string.IsNullOrWhiteSpace(placement.ReportBody)
                ? placement.ReportBody
                : canReusePreviousReport ? previousReportBody : book.reportBody;
            book.reportUpdatedLabel = !string.IsNullOrWhiteSpace(placement.ReportUpdatedLabel)
                ? placement.ReportUpdatedLabel
                : canReusePreviousReport ? previousReportUpdatedLabel : book.reportUpdatedLabel;
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
                var x = GetSectionLayoutStartX(section, padding);
                for (var j = 0; j < section.books.Count; j++)
                {
                    var book = section.books[j];
                    if (book == null)
                    {
                        continue;
                    }

                    var footprint = book.GetLayoutFootprint();
                    book.SetPlacementInfo(activeBookshelfIndex, section.row * columns + section.column, x - section.leftBound);
                    book.transform.localPosition = new Vector3(
                        x + footprint * 0.5f,
                        section.BookBaseY + book.height * 0.5f,
                        section.GetFrontAlignedBookCenterZ(book, 1f, frontInset));
                    book.transform.localRotation = BookFootprintUtility.GetVisualRotation(book.orientation);
                    book.transform.localScale = book.BaseLocalScale;
                    var nextBook = FindNextBook(section.books, j + 1);
                    x += footprint + (nextBook != null ? BookFootprintUtility.GetPairSpacing(padding, book, nextBook) : 0f);
                }
            }
        }

        private static float GetSectionLayoutStartX(ShelfSection section, float padding)
        {
            if (section == null)
            {
                return 0f;
            }

            var contentWidth = GetSectionContentWidth(section, padding);
            var edgePadding = BookFootprintUtility.GetEdgePadding(padding);
            var leftStart = section.leftBound + edgePadding;
            var rightStart = section.rightBound - edgePadding - contentWidth;
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

            return materialFactory.GetBookshelfWoodMaterial(ResolveRenderedBookshelfDesignStyle(index));
        }

        private static float GetSectionContentWidth(ShelfSection section, float padding)
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

                width += book.GetLayoutFootprint();
                bookCount++;

                var nextBook = FindNextBook(section.books, i + 1);
                if (nextBook != null)
                {
                    width += BookFootprintUtility.GetPairSpacing(padding, book, nextBook);
                }
            }

            return width;
        }

        private IEnumerator AnimateAttachedBookshelfSwitch(float signedFullAngle, float duration)
        {
            var totalDuration = Mathf.Max(0.25f, duration);
            var slideDuration = Mathf.Max(0.05f, totalDuration * bookshelfSwitchSlideDurationRatio);
            var rotateDuration = Mathf.Max(0.05f, totalDuration - slideDuration * 2f);
            var restZ = GetAttachedSwitchRestTurntableZ();
            var safeZ = GetAttachedSwitchSafeTurntableZ();

            // Pull the attached pair away from the wall, rotate both shelves as one rigid pair,
            // then dock them back so the active shelf lands on the original wall position.
            yield return AnimateTurntableZ(restZ, safeZ, slideDuration, 0f);

            if (transitionBookshelfRoot != null)
            {
                transitionBookshelfRoot.gameObject.SetActive(true);
            }

            yield return AnimateTurntableRotation(0f, signedFullAngle, rotateDuration);

            if (bookshelfRoot != null)
            {
                bookshelfRoot.gameObject.SetActive(false);
            }

            yield return AnimateTurntableZ(safeZ, restZ, slideDuration, signedFullAngle);

            if (bookshelfRoot != null)
            {
                bookshelfRoot.gameObject.SetActive(true);
            }
        }

        private void EnsureActiveBookshelfVisible()
        {
            if (bookshelfRoot != null && !bookshelfRoot.gameObject.activeSelf)
            {
                bookshelfRoot.gameObject.SetActive(true);
            }
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

        private IEnumerator AnimateTurntableZ(float startZ, float endZ, float duration, float yAngle)
        {
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = SmootherStep(Mathf.Clamp01(elapsed / duration));
                SetTurntablePose(yAngle, Mathf.Lerp(startZ, endZ, t));
                yield return null;
            }

            SetTurntablePose(yAngle, endZ);
        }

        private static float SmootherStep(float t)
        {
            t = Mathf.Clamp01(t);
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

        private void SetTurntablePose(float yAngle, float z)
        {
            if (bookshelfTurntableRoot == null)
            {
                return;
            }

            bookshelfTurntableRoot.localPosition = new Vector3(0f, 0f, z);
            SetTurntableRotation(yAngle);
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
            DestroyAdvertisementBooksUnderRoot(booksRoot);
        }

        private static void DestroyAdvertisementBooksUnderRoot(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var advertisementBooks = new List<GameObject>();
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child != null && child.name == AdvertisementBookInstanceName)
                {
                    advertisementBooks.Add(child.gameObject);
                }
            }

            for (var i = 0; i < advertisementBooks.Count; i++)
            {
                DestroySpawnedObject(advertisementBooks[i]);
            }
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

        private static int CountUserBookSections(List<ShelfSection> sections)
        {
            if (sections == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < sections.Count; i++)
            {
                var section = sections[i];
                if (section != null && !section.isAdvertisementSection)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountOccupiedSections(List<ShelfSection> sections)
        {
            if (sections == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < sections.Count; i++)
            {
                var section = sections[i];
                if (section == null || section.isAdvertisementSection)
                {
                    continue;
                }

                for (var j = 0; j < section.books.Count; j++)
                {
                    if (section.books[j] != null)
                    {
                        count++;
                        break;
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
            // LeftWall inner face X = wall center(-RoomWidth*0.5 + 0.05, line 694) + thickness*0.5(0.04).
            // 0.015 inset 으로 guestbook 표면이 벽 안쪽에 살짝 떠 있음. RoomWidth 변경 시 자동 따라감.
            // (MR #147 review Finding 1: 이전 -1.995 hardcode 가 4.2 폭 기준이라 5 폭에서 벽에서 0.4m 떠 있던 회귀 fix.)
            const float GuestbookInsetFromInnerFace = 0.015f;
            var guestbookX = -RoomWidth * 0.5f + 0.05f + BackWallThickness * 0.5f + GuestbookInsetFromInnerFace;
            guestbook.transform.localPosition = new Vector3(guestbookX, 1.18f, 0.12f);
            guestbook.transform.localRotation = Quaternion.identity;
            generatedGeometry.Add(guestbook);

            CreateDashboardBezelFrame(guestbook.transform, new Vector3(0.006f, 0.64f, 1.13f), new Vector2(0.62f, 1.10f), materialFactory.GetWhiteboardFrameMaterial());
            CreateBox("Surface", guestbook.transform, new Vector3(0.005f, 0f, 0f), new Vector3(0.004f, 0.62f, 1.10f), materialFactory.GetWhiteboardSurfaceMaterial());

            var hitArea = new GameObject("GuestbookHitArea");
            hitArea.transform.SetParent(guestbook.transform, false);
            hitArea.transform.localPosition = new Vector3(0.006f, 0f, 0f);
            var collider = hitArea.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.05f, 0.70f, 1.20f);
            hitArea.AddComponent<GuestbookBoardHitArea>();
            generatedGeometry.Add(hitArea);
        }

        private void CreateDashboardBezelFrame(Transform parent, Vector3 outerSize, Vector2 innerSize, Material material)
        {
            var depth = Mathf.Max(0.001f, outerSize.x);
            var outerHeight = Mathf.Max(0.001f, outerSize.y);
            var outerWidth = Mathf.Max(0.001f, outerSize.z);
            var innerHeight = Mathf.Clamp(innerSize.x, 0.001f, outerHeight);
            var innerWidth = Mathf.Clamp(innerSize.y, 0.001f, outerWidth);
            var horizontalThickness = Mathf.Max(0.001f, (outerHeight - innerHeight) * 0.5f);
            var verticalThickness = Mathf.Max(0.001f, (outerWidth - innerWidth) * 0.5f);
            var halfInnerHeight = innerHeight * 0.5f;
            var halfInnerWidth = innerWidth * 0.5f;

            CreateBox(
                "FrameTop",
                parent,
                new Vector3(0f, halfInnerHeight + horizontalThickness * 0.5f, 0f),
                new Vector3(depth, horizontalThickness, outerWidth),
                material);
            CreateBox(
                "FrameBottom",
                parent,
                new Vector3(0f, -halfInnerHeight - horizontalThickness * 0.5f, 0f),
                new Vector3(depth, horizontalThickness, outerWidth),
                material);
            CreateBox(
                "FrameLeft",
                parent,
                new Vector3(0f, 0f, -halfInnerWidth - verticalThickness * 0.5f),
                new Vector3(depth, innerHeight, verticalThickness),
                material);
            CreateBox(
                "FrameRight",
                parent,
                new Vector3(0f, 0f, halfInnerWidth + verticalThickness * 0.5f),
                new Vector3(depth, innerHeight, verticalThickness),
                material);
        }

        private void CreateBookshelfFocusHitArea(Transform parent)
        {
            CreateBookshelfFocusHitArea(
                parent,
                new Vector3(0f, height * 0.5f, -depth * 0.5f - 0.05f),
                new Vector3(width + 0.10f, height + 0.10f, 0.18f));
        }

        private void CreateBookshelfFocusHitArea(Transform parent, Vector3 localPosition, Vector3 size)
        {
            var hitArea = new GameObject("BookshelfFocusHitArea");
            hitArea.transform.SetParent(parent, false);
            hitArea.transform.localPosition = localPosition;
            var collider = hitArea.AddComponent<BoxCollider>();
            collider.size = size;
            hitArea.AddComponent<BookshelfFocusHitArea>();
            generatedGeometry.Add(hitArea);
        }

        private void BuildNotebookStation()
        {
            var guestbook = roomRoot != null ? roomRoot.Find("GuestbookBoard") : null;
            var stationParent = guestbook != null ? guestbook : roomRoot;
            var stationPosition = guestbook != null
                ? MainSceneNotebookStationLocalPosition
                : FallbackNotebookStationLocalPosition;

            if (notebookStationAsset != null)
            {
                var stationInstance = Instantiate(notebookStationAsset, stationParent, false);
                stationInstance.name = "NotebookStation";
                stationInstance.transform.localPosition = stationPosition;
                stationInstance.transform.localRotation = Quaternion.identity;
                stationInstance.transform.localScale = Vector3.one;
                ApplyNotebookStationFallbackMaterials(stationInstance);
                generatedGeometry.Add(stationInstance);
                return;
            }

            var notebookStation = new GameObject("NotebookStation");
            notebookStation.transform.SetParent(stationParent, false);
            notebookStation.transform.localPosition = stationPosition;
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

        private void ApplyNotebookStationFallbackMaterials(GameObject stationInstance)
        {
            if (stationInstance == null || materialFactory == null)
            {
                return;
            }

            var aluminum = materialFactory.CreateBookMaterial(new Color(0.72f, 0.73f, 0.71f));
            var brightAluminum = materialFactory.CreateBookMaterial(new Color(0.82f, 0.83f, 0.80f));
            var darkGlass = materialFactory.CreateBookMaterial(new Color(0.015f, 0.018f, 0.022f));
            var screenGlow = materialFactory.CreateBookMaterial(new Color(0.10f, 0.16f, 0.24f));
            var keyboardTone = materialFactory.CreateBookMaterial(new Color(0.035f, 0.038f, 0.043f));
            var keyLetterTone = materialFactory.CreateBookMaterial(new Color(0.12f, 0.125f, 0.13f));
            var trackpadTone = materialFactory.CreateBookMaterial(new Color(0.60f, 0.61f, 0.59f));
            var trackpadInsetTone = materialFactory.CreateBookMaterial(new Color(0.68f, 0.69f, 0.67f));

            var renderers = stationInstance.GetComponentsInChildren<MeshRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                var material = ResolveNotebookPartMaterial(
                    renderer.gameObject.name,
                    aluminum,
                    brightAluminum,
                    darkGlass,
                    screenGlow,
                    keyboardTone,
                    keyLetterTone,
                    trackpadTone,
                    trackpadInsetTone);

                if (material != null)
                {
                    renderer.sharedMaterial = material;
                }
            }
        }

        private static Material ResolveNotebookPartMaterial(
            string name,
            Material aluminum,
            Material brightAluminum,
            Material darkGlass,
            Material screenGlow,
            Material keyboardTone,
            Material keyLetterTone,
            Material trackpadTone,
            Material trackpadInsetTone)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (name.StartsWith("Key"))
            {
                return keyLetterTone;
            }

            switch (name)
            {
                case "UnibodyBase":
                case "HingeBar":
                case "LowerBezelLip":
                case "ScreenLid":
                    return aluminum;
                case "FrontChamfer":
                    return brightAluminum;
                case "KeyboardWell":
                case "HingeShadowGap":
                    return keyboardTone;
                case "Trackpad":
                    return trackpadTone;
                case "TrackpadInset":
                    return trackpadInsetTone;
                case "BlackBezel":
                case "CameraDot":
                    return darkGlass;
                case "DisplayPanel":
                    return screenGlow;
                default:
                    return null;
            }
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
                string coverSkinResourcePath = null,
                bool isDecoration = false,
                string bookshelfItemId = null,
                string userBookId = null,
                string bookshelfId = null,
                string bookshelfSectionId = null,
                string readingRecordId = null,
                VisibilityScope reportVisibility = VisibilityScope.Public,
                string reportTitle = null,
                string reportBody = null,
                string reportUpdatedLabel = null,
                float averageRating = 0f,
                int totalLikeCount = 0,
                int reviewCount = 0,
                bool hasAggregateBookStats = false,
                int bookshelfIndex = -1,
                int cellIndex = -1,
                float offsetXInCell = 0f,
                string sizePresetId = null)
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
                CoverSkinResourcePath = coverSkinResourcePath;
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
                AverageRating = averageRating;
                TotalLikeCount = totalLikeCount;
                ReviewCount = reviewCount;
                HasAggregateBookStats = hasAggregateBookStats;
                BookshelfIndex = bookshelfIndex;
                CellIndex = cellIndex;
                OffsetXInCell = offsetXInCell;
                SizePresetId = sizePresetId;
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
            public string CoverSkinResourcePath { get; }
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
            public float AverageRating { get; }
            public int TotalLikeCount { get; }
            public int ReviewCount { get; }
            public bool HasAggregateBookStats { get; }
            public int BookshelfIndex { get; }
            public int CellIndex { get; }
            public float OffsetXInCell { get; }
            public string SizePresetId { get; }
        }
    }

    internal static class BookshelfSponsoredBook
    {
        public const string RuntimeBookId = "advertisement_book";
        public const string CatalogBookId = "1017";
        public const string Source = "KAKAO";
        public const string Isbn13 = "9788968570087";
        public const string Isbn10 = "8968570086";
        public const string PublishedDate = "2014-08-08";
        public const string Title = "넨도, 디자인 이야기";
        public const string Author = "사토 오오키, 가와카미 노리코";
        public const string Publisher = "미디어샘";
        public const string CoverImageUrl = "https://search1.kakaocdn.net/thumb/R120x174.q85/?fname=http%3A%2F%2Ft1.daumcdn.net%2Flbook%2Fimage%2F944988%3Ftimestamp%3D20260227110911";
        public const string SourceUrl = "https://search.daum.net/search?w=bookpage&bookId=944988&q=%EB%84%A8%EB%8F%84%2C+%EB%94%94%EC%9E%90%EC%9D%B4%EC%95%BC%EA%B8%B0";
        public const string Description =
            "독창적 디자인 발상법과 회사 경영법『넨도, 디자인 이야기』. 이 책은 전 세계 연 250개 이상 기업의 디자인을 맡아 매출을 상승 시킨 디자인 회사 넨도의 창업자 오오키의 디자인 발상법과 회상 경영법을 소개한다. 작은 디자인회사인 넨도의 무엇이 세계기업을 열광하게 했는지 생생한 프로젝트 경험과 디자인 발상법을 10가지로 정리하여 들려준다. 저자는 그동안 진행한 다양한 디자인 프로젝트를 소개하고 어떻게 아이디어를 얻고 매출 상승으로 직결시켰는지 상세히 설명한다.";

        public static bool IsAdvertisementBook(BookEntity book)
        {
            return book != null &&
                   (string.Equals(book.bookId, RuntimeBookId, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(book.source, "advertisement", System.StringComparison.OrdinalIgnoreCase));
        }

        public static bool MatchesSponsoredCatalog(BookEntity book)
        {
            if (book == null)
            {
                return false;
            }

            return string.Equals(book.bookId, CatalogBookId, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(book.isbn13, Isbn13, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(book.isbn10, Isbn10, System.StringComparison.OrdinalIgnoreCase);
        }

        public static void ApplyDisplayMetadata(BookEntity book)
        {
            if (book == null)
            {
                return;
            }

            book.source = Source;
            book.isbn13 = Isbn13;
            book.isbn10 = Isbn10;
            book.publishedDate = PublishedDate;
            book.coverImageUrl = CoverImageUrl;
            book.sourceUrl = SourceUrl;
            book.title = Title;
            book.author = Author;
            book.publisher = Publisher;
            book.description = Description;
            book.memoPreview = Description;
            book.hasAggregateBookStats = true;
            book.averageRating = 0f;
            book.totalLikeCount = 0;
            book.reviewCount = 0;
        }
    }
}
