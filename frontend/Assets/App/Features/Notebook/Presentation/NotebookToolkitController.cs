using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using App.Infrastructure.Api;
using App.Infrastructure.Api.Dtos;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace App.Features.Notebook.Presentation
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class NotebookToolkitController : MonoBehaviour
    {
        private sealed class BookViewModel
        {
            public BookViewModel(
                string id,
                string title,
                string author,
                string publisher,
                string summary,
                string genre,
                string status,
                string shelf,
                int price,
                float rating,
                int reviews,
                int views,
                int records,
                Color coverColor,
                Color accentColor,
                string apiSource = "MOCK",
                long? serverBookId = null,
                string isbn13 = null,
                string isbn10 = null,
                string sourceUrl = null,
                string coverImageUrl = null,
                string publishedDate = null)
            {
                Id = id;
                Title = title;
                Author = author;
                Publisher = publisher;
                Summary = summary;
                Genre = genre;
                Status = status;
                Shelf = shelf;
                Price = price;
                Rating = rating;
                Reviews = reviews;
                Views = views;
                Records = records;
                CoverColor = coverColor;
                AccentColor = accentColor;
                ApiSource = string.IsNullOrWhiteSpace(apiSource) ? "MOCK" : apiSource;
                ServerBookId = serverBookId;
                Isbn13 = isbn13;
                Isbn10 = isbn10;
                SourceUrl = sourceUrl;
                CoverImageUrl = coverImageUrl;
                PublishedDate = publishedDate;
            }

            public string Id { get; }
            public string Title { get; }
            public string Author { get; }
            public string Publisher { get; }
            public string Summary { get; }
            public string Genre { get; }
            public string Status { get; }
            public string Shelf { get; }
            public int Price { get; }
            public float Rating { get; }
            public int Reviews { get; }
            public int Views { get; }
            public int Records { get; }
            public Color CoverColor { get; }
            public Color AccentColor { get; }
            public string ApiSource { get; }
            public long? ServerBookId { get; }
            public string Isbn13 { get; }
            public string Isbn10 { get; }
            public string SourceUrl { get; }
            public string CoverImageUrl { get; }
            public string PublishedDate { get; }
        }

        private sealed class BookReviewViewModel
        {
            public BookReviewViewModel(string author, float rating, string content)
            {
                Author = author;
                Rating = rating;
                Content = content;
            }

            public string Author { get; }
            public float Rating { get; }
            public string Content { get; }
        }

        private sealed class SpaceViewModel
        {
            public SpaceViewModel(
                string id,
                string ownerId,
                string owner,
                string title,
                string mood,
                string description,
                string thumbnailKey,
                int followers,
                int likes,
                int visits,
                Color accentColor)
            {
                Id = id;
                OwnerId = ownerId;
                Owner = owner;
                Title = title;
                Mood = mood;
                Description = description;
                ThumbnailKey = thumbnailKey;
                Followers = followers;
                Likes = likes;
                Visits = visits;
                AccentColor = accentColor;
            }

            public string Id { get; }
            public string OwnerId { get; }
            public string Owner { get; }
            public string Title { get; }
            public string Mood { get; }
            public string Description { get; }
            public string ThumbnailKey { get; }
            public int Followers { get; }
            public int Likes { get; }
            public int Visits { get; }
            public Color AccentColor { get; }
        }

        private sealed class DecorObjectViewModel
        {
            public DecorObjectViewModel(
                string id,
                string name,
                string category,
                string description,
                int price,
                int popularity,
                int releaseOrder,
                Color color,
                Color accentColor)
            {
                Id = id;
                Name = name;
                Category = category;
                Description = description;
                Price = price;
                Popularity = popularity;
                ReleaseOrder = releaseOrder;
                Color = color;
                AccentColor = accentColor;
            }

            public string Id { get; }
            public string Name { get; }
            public string Category { get; }
            public string Description { get; }
            public int Price { get; }
            public int Popularity { get; }
            public int ReleaseOrder { get; }
            public Color Color { get; }
            public Color AccentColor { get; }
        }

        private const int RankingPageSize = 6;
        private const int DashboardSortingOrder = 320;
        private const int FallbackSortingOrder = -320;
        private const int DashboardInitializationRetryFrames = 30;
        private const float RecommendWheelScrollScale = 56f;
        private const int SearchResultPageSize = 48;
        private const long DefaultBookshelfId = 1L;
        private const long DefaultSectionId = 1L;
        private const int DefaultSlotNo = 1;
        private const string DefaultDisplayStyle = "SPINE";
        private const string PanelSettingsResource = "NotebookPanelSettings";
        private static readonly Color PageBack = Hex(0xF0EEE9);
        private static readonly Color Surface = Hex(0xF7FFF7);
        private static readonly Color SurfaceAlt = Hex(0xEEF4EC);
        private static readonly Color Ink = Hex(0x0E1D2A);
        private static readonly Color SoftInk = Rgba(0x0E, 0x1D, 0x2A, 0.68f);
        private static readonly Color FaintInk = Rgba(0x0E, 0x1D, 0x2A, 0.42f);
        private static readonly Color Line = Rgba(0x0E, 0x1D, 0x2A, 0.12f);
        private static readonly Color StrongLine = Rgba(0x0E, 0x1D, 0x2A, 0.22f);
        private static readonly Color Accent = Hex(0x58B9FF);
        private static readonly Color AccentDeep = Hex(0x2D7EC0);
        private static readonly Color DeepGreen = Hex(0x183B31);
        private static readonly Color Success = Hex(0x3F9B68);
        private static readonly Color Warning = Hex(0xD98F36);
        private static readonly Color Night = Hex(0x091626);
        private static readonly Color WarmPaper = Hex(0xFFF7E8);
        private static readonly Color Coral = Hex(0xFF8A65);
        private static readonly Color Violet = Hex(0x6E5BFF);
        private static readonly Color Gold = Hex(0xF4C95D);
        private static Texture2D closeIconTexture;
        private static Texture2D searchIconTexture;
        private static Texture2D pointerCursorTexture;
        private static readonly string[] SuggestedSearches = { "소설", "에세이", "심리", "베스트셀러", "SF", "자기계발", "인문" };

        private static readonly string[] HeaderTabs = { "책 시장", "다른 공간", "꾸미기 시장" };
        private static readonly string[] SubTabs = { "전체 탐색", "무료", "유료" };
        private static readonly string[] Categories = { "전체", "읽는 중", "읽음", "안 읽음", "책장 1", "책장 2" };
        private static readonly string[] Genres = { "전체", "소설", "에세이", "인문", "심리", "경제/경영", "SF/판타지" };
        private static readonly string[] RankingSorts = { "인기순", "별점순", "리뷰순" };
        private static readonly string[] ObjectSorts = { "인기순", "최신순", "가격순" };
        private static readonly string[] ObjectCategories = { "전체", "필수 배치 오브젝트", "기타 배치 오브젝트", "전체 테마" };

        [SerializeField] private UIDocument document;

        private readonly BookViewModel[] books =
        {
            new("r1", "저녁의 문장들", "한수정", "밀림출판", "하루의 마지막에 읽기 좋은 짧은 문장과 산문을 모은 에세이입니다.", "에세이", "읽는 중", "책장 1", 0, 4.8f, 612, 13980, 3410, Hex(0x0A2540), Accent),
            new("r2", "조용한 심장", "Eliot Mara", "Oakrow Press", "내면의 리듬을 되찾는 심리 에세이. 느린 호흡으로 마음을 정리합니다.", "심리", "안 읽음", "책장 2", 1200, 4.7f, 430, 12640, 3020, Hex(0xEAF5FF), AccentDeep),
            new("r3", "숲으로 간 편지", "박이도", "초록서재", "자연과 편지로 엮은 계간 소설. 작은 계절의 움직임을 따라갑니다.", "소설", "읽음", "책장 1", 0, 4.9f, 884, 18420, 4112, Hex(0x12345A), Hex(0x9ED4FF)),
            new("r4", "끝없이 이어지는 방", "조민아", "검은잉크", "공간이 기억을 가질 수 있다면 어떤 방이 남을지 묻는 SF 장편입니다.", "SF/판타지", "안 읽음", "책장 2", 1800, 4.6f, 321, 10880, 2490, Hex(0x061422), Surface),
            new("r5", "햇빛 인사법", "Naomi Vann", "Lantern & Co.", "느린 계절을 기록하는 방법을 담은 산문집. 아침에 읽기 좋습니다.", "에세이", "읽는 중", "책장 1", 0, 4.5f, 278, 12510, 2844, Hex(0xC9E6FF), AccentDeep),
            new("r6", "일요일의 경제학", "이재훈", "월요서재", "교양으로 시작하는 주말 경제. 어렵지 않은 예시로 시장을 설명합니다.", "경제/경영", "안 읽음", "책장 2", 1500, 4.4f, 198, 11880, 2711, Hex(0x0A2540), Accent),
            new("r7", "문장 수집가", "김아진", "밤과책", "좋은 문장을 모으는 사람들의 기록. 독서 노트를 시작하고 싶게 합니다.", "인문", "읽음", "책장 1", 0, 4.8f, 740, 15210, 3611, Hex(0x2D7EC0), Ink),
            new("r8", "완벽하지 않은 여름", "서은재", "백지출판", "완벽하지 않은 계절 속에서 관계가 회복되는 이야기입니다.", "소설", "읽는 중", "책장 1", 1400, 4.9f, 923, 18420, 4112, Hex(0x1D5F8C), Hex(0x9ED4FF)),
            new("r9", "관계의 지도", "윤지수", "북노트", "관계의 거리와 방향을 다시 그려 보는 인문 에세이입니다.", "인문", "안 읽음", "책장 2", 1200, 4.7f, 710, 14322, 3402, Hex(0x1A3350), Hex(0x9ED4FF)),
            new("r10", "별이 된 서점", "문경하", "초록서재", "밤하늘에 떠오른 서점을 따라가는 따뜻한 판타지 소설입니다.", "SF/판타지", "읽음", "책장 2", 1800, 4.6f, 640, 13210, 3022, Hex(0x050D1A), Accent),
        };

        private readonly SpaceViewModel[] spaces =
        {
            new("s1", "u-hansu", "한수정", "밤의 문장 작업실", "에세이 · 차분한 서재", "책상 위 노트와 낮은 조명으로 만든 조용한 기록 공간입니다.", "night-writing-room", 1842, 734, 9230, Accent),
            new("s2", "u-jaehoon", "이재훈", "일요일 경제 책상", "경제/경영 · 실용형", "차트와 메모 보드가 함께 놓인 주말 독서 루틴 공간입니다.", "sunday-economy-desk", 1320, 521, 7088, Gold),
            new("s3", "u-mina", "조민아", "끝없는 방의 서가", "SF/판타지 · 몰입형", "어두운 벽면과 별빛 오브젝트로 세계관을 강조한 공간입니다.", "endless-sf-shelf", 2164, 1188, 11340, Violet),
            new("s4", "u-ajine", "김아진", "문장 수집 선반", "인문 · 큐레이션", "좋은 문장을 모아 둔 카드와 책등 색이 중심인 전시형 서재입니다.", "sentence-collector-shelf", 970, 408, 6124, Coral),
        };

        private readonly DecorObjectViewModel[] decorObjects =
        {
            new("o1", "월넛 책장", "필수 배치 오브젝트", "책 시장과 연결되는 기본 책장 오브젝트입니다.", 900, 9820, 6, Hex(0x815A3B), Gold),
            new("o2", "독서 대시보드", "필수 배치 오브젝트", "독서 진행률과 기록 수를 보여주는 노트북 옆 패널입니다.", 1200, 8640, 5, AccentDeep, Accent),
            new("o3", "스카이 노트북", "필수 배치 오브젝트", "노트북 포커싱 진입점을 강조하는 밝은 노트북 스킨입니다.", 1600, 9320, 7, Accent, Surface),
            new("o4", "아치형 창문 벽", "기타 배치 오브젝트", "서재 뒤편에 깊이감을 더하는 벽 장식입니다.", 700, 6030, 3, Hex(0xBEE2FF), AccentDeep),
            new("o5", "체크 패턴 바닥", "기타 배치 오브젝트", "책상 아래 공간을 정리해 보이게 만드는 바닥 패턴입니다.", 650, 5740, 2, Hex(0xF4C95D), Ink),
            new("o6", "푸른 편집자 테마", "전체 테마", "책장, 책상, 배경 조명을 한 번에 바꾸는 전체 테마입니다.", 2200, 10240, 8, Night, Accent),
        };

        private readonly Dictionary<string, Button> headerTabButtons = new();
        private readonly Dictionary<string, Button> categoryButtons = new();
        private readonly Dictionary<string, Button> subTabButtons = new();
        private readonly Dictionary<string, Button> genreButtons = new();
        private readonly Dictionary<string, Button> searchSuggestionButtons = new();
        private readonly Dictionary<string, Button> rankingSortButtons = new();
        private readonly Dictionary<string, Button> objectSortButtons = new();
        private readonly Dictionary<string, Button> objectCategoryButtons = new();
        private readonly List<Button> pageButtons = new();
        private readonly HashSet<string> addedBookIds = new() { "r3" };
        private readonly HashSet<string> followedUserIds = new();
        private readonly HashSet<string> likedSpaceIds = new();
        private readonly HashSet<string> wishedObjectIds = new();
        private readonly HashSet<string> ownedObjectIds = new();
        private readonly HashSet<string> favoriteBookIds = new();
        private readonly HashSet<string> pendingShelfBookIds = new();
        private readonly Dictionary<string, List<BookReviewViewModel>> bookReviewMap = new();
        private readonly List<Button> bookRatingStarButtons = new();

        private UnityAction closeRequested;
        private Font uiFont;
        private VisualElement root;
        private VisualElement heroPanel;
        private VisualElement searchContainer;
        private VisualElement suggestionsContainer;
        private VisualElement subTabsRow;
        private VisualElement bookMarketContent;
        private VisualElement searchResultsContent;
        private VisualElement searchResultsList;
        private VisualElement bookDetailContent;
        private VisualElement bookDetailModalPanel;
        private VisualElement otherSpacesContent;
        private VisualElement objectMarketContent;
        private VisualElement objectList;
        private VisualElement recommendList;
        private VisualElement rankingGrid;
        private VisualElement bookDetailCover;
        private VisualElement bookDetailReviews;
        private VisualElement bookReviewPanel;
        private VisualElement detailCover;
        private ScrollView recommendScrollView;
        private TextField searchField;
        private TextField bookReviewInput;
        private Button clearSearchButton;
        private Button bookFavoriteButton;
        private Button bookDetailAddButton;
        private Button bookReviewMoreButton;
        private Button bookReviewToggleButton;
        private Label headerSubtitleLabel;
        private Label heroEyebrowLabel;
        private Label heroTitleLabel;
        private Label heroCopyLabel;
        private Label pageBalanceLabel;
        private Label resultLabel;
        private Label searchResultsTitleLabel;
        private Label searchResultsCountLabel;
        private Label searchResultsHintLabel;
        private Label objectResultLabel;
        private Label bookDetailTitle;
        private Label bookDetailAuthor;
        private Label bookDetailPublisher;
        private Label bookDetailSummary;
        private Label bookDetailMeta;
        private Label bookDetailPrice;
        private Label bookDetailReviewCount;
        private Label detailTitle;
        private Label detailAuthor;
        private Label detailPublisher;
        private VisualElement detailMetaRow;
        private Label detailRating;
        private Label detailReviews;
        private Label detailViews;
        private Label detailSummary;
        private Label detailShelf;
        private Label detailPrice;
        private Label toastLabel;
        private VisualElement toast;
        private ScrollView pageScrollView;
        private VisualElement scrollRail;
        private VisualElement scrollThumb;
        private Button previousPageButton;
        private Button nextPageButton;
        private Button addButton;
        private Coroutine dashboardInitializationRoutine;
        private NotebookApiClient notebookApiClient;
        private BookViewModel[] searchResults = Array.Empty<BookViewModel>();
        private int toastHideVersion;
        private int dashboardShowVersion;
        private int searchRequestVersion;

        private string query = string.Empty;
        private string appliedSearchQuery = string.Empty;
        private string activeHeaderTab = HeaderTabs[0];
        private string activeSearchSuggestion = string.Empty;
        private string activeCategory = "전체";
        private string activeSubTab = "전체 탐색";
        private string activeGenre = "전체";
        private string activeRankingSort = RankingSorts[0];
        private string activeObjectSort = ObjectSorts[0];
        private string activeObjectCategory = ObjectCategories[0];
        private int selectedBookIndex;
        private int pageIndex;
        private int pages = 4820;
        private int pageScrollbarPointerId = -1;
        private float pageScrollbarStartPointerY;
        private float pageScrollbarStartOffsetY;
        private bool initialized;
        private bool visible;
        private bool draggingPageScrollbar;
        private bool showingAllBookReviews;
        private bool bookReviewsOpen;
        private bool searchPageOpen;
        private bool searchLoading;
        private BookViewModel selectedBookOverride;
        private float selectedReviewRating = 5f;

        public bool IsReady
        {
            get
            {
                ResolveDocument();
                return document != null && document.panelSettings != null;
            }
        }

        public bool IsDashboardVisible => visible && document != null && document.enabled && HasAttachedRoot();

        private void Awake()
        {
            ResolveDocument();
            if (document != null)
            {
                document.enabled = false;
            }
        }

        private void OnDisable()
        {
            visible = false;
        }

        private void Update()
        {
            if (!visible || pageScrollView == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) &&
                bookDetailContent != null &&
                bookDetailContent.style.display.value == DisplayStyle.Flex)
            {
                CloseBookDetail();
                return;
            }

            var wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f)
            {
                ScrollNotebookBy(-wheel * 90f);
            }

            if (Input.GetKeyDown(KeyCode.PageDown))
            {
                ScrollNotebookBy(280f);
            }
            else if (Input.GetKeyDown(KeyCode.PageUp))
            {
                ScrollNotebookBy(-280f);
            }
        }

        public void Configure(UnityAction onCloseRequested)
        {
            closeRequested = onCloseRequested;
            ResolveDocument();
        }

        public bool ShowDashboard(bool immersiveMode)
        {
            ResolveDocument();
            if (document == null)
            {
                return false;
            }

            visible = true;
            var showVersion = ++dashboardShowVersion;
            SendDocumentBehindFallback();
            document.enabled = true;
            EnsureInitialized();

            if (TryShowBuiltDashboard())
            {
                return true;
            }

            StartDashboardInitializationRetry(showVersion);
            return false;
        }

        public void HideDashboard()
        {
            visible = false;
            dashboardShowVersion++;
            if (dashboardInitializationRoutine != null)
            {
                StopCoroutine(dashboardInitializationRoutine);
                dashboardInitializationRoutine = null;
            }

            if (root != null)
            {
                root.style.display = DisplayStyle.None;
            }

            if (document != null)
            {
                document.enabled = false;
            }
        }

        private void StartDashboardInitializationRetry(int showVersion)
        {
            if (dashboardInitializationRoutine != null)
            {
                StopCoroutine(dashboardInitializationRoutine);
            }

            dashboardInitializationRoutine = StartCoroutine(ShowDashboardWhenInitialized(showVersion));
        }

        private IEnumerator ShowDashboardWhenInitialized(int showVersion)
        {
            for (var frame = 0; frame < DashboardInitializationRetryFrames; frame++)
            {
                if (!visible || showVersion != dashboardShowVersion)
                {
                    dashboardInitializationRoutine = null;
                    yield break;
                }

                yield return null;
                ResolveDocument();
                if (document == null)
                {
                    continue;
                }

                SendDocumentBehindFallback();
                document.enabled = true;
                EnsureInitialized();
                if (TryShowBuiltDashboard())
                {
                    dashboardInitializationRoutine = null;
                    yield break;
                }
            }

            if (visible && showVersion == dashboardShowVersion)
            {
                DisableBlankDocument();
                UnityEngine.Debug.LogWarning("NotebookToolkitController could not build dashboard in time; notebook dashboard remains hidden.");
            }

            dashboardInitializationRoutine = null;
        }

        private bool TryShowBuiltDashboard()
        {
            if (!HasAttachedRoot())
            {
                return false;
            }

            SetDocumentRenderPriority(DashboardSortingOrder, true);
            root.style.display = DisplayStyle.Flex;
            RefreshAll();
            return true;
        }

        private void SendDocumentBehindFallback()
        {
            SetDocumentRenderPriority(FallbackSortingOrder, false);
        }

        private void DisableBlankDocument()
        {
            if (root != null)
            {
                root.style.display = DisplayStyle.None;
            }

            if (document != null)
            {
                document.enabled = false;
            }
        }

        private void ResolveDocument()
        {
            if (document == null)
            {
                document = GetComponent<UIDocument>();
            }

            if (document != null && document.panelSettings == null)
            {
                var panelSettings = Resources.Load<PanelSettings>(PanelSettingsResource);
                if (panelSettings != null)
                {
                    document.panelSettings = panelSettings;
                }
            }

            if (document != null && document.panelSettings == null)
            {
                document.panelSettings = CreateRuntimePanelSettings();
            }

            if (document != null)
            {
                ConfigurePanelSettings(document.panelSettings);
                SetDocumentRenderPriority(DashboardSortingOrder, true);
            }
        }

        private static PanelSettings CreateRuntimePanelSettings()
        {
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "NotebookRuntimePanelSettings";
            ConfigurePanelSettings(panelSettings);
            return panelSettings;
        }

        private static void ConfigurePanelSettings(PanelSettings panelSettings)
        {
            if (panelSettings == null)
            {
                return;
            }

            panelSettings.renderMode = PanelRenderMode.ScreenSpaceOverlay;
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1200, 800);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0f;
            panelSettings.sortingOrder = DashboardSortingOrder;
            panelSettings.clearColor = true;
            panelSettings.colorClearValue = PageBack;
        }

        private void SetDocumentRenderPriority(int sortingOrder, bool clearColor)
        {
            if (document == null)
            {
                return;
            }

            document.sortingOrder = sortingOrder;
            if (document.panelSettings == null)
            {
                return;
            }

            document.panelSettings.sortingOrder = sortingOrder;
            document.panelSettings.clearColor = clearColor;
            if (clearColor)
            {
                document.panelSettings.colorClearValue = PageBack;
            }
        }

        private void EnsureInitialized()
        {
            var host = document != null ? document.rootVisualElement : null;
            if (initialized && root != null && host != null && root.parent == host && host.childCount > 0)
            {
                return;
            }

            initialized = false;
            root = null;

            if (host == null)
            {
                return;
            }

            uiFont = Resources.Load<Font>("Fonts/Pretendard-Regular");
            if (uiFont == null)
            {
                uiFont = Font.CreateDynamicFontFromOSFont("Pretendard", 16);
            }

            try
            {
                host.Clear();
                host.style.width = Length.Percent(100);
                host.style.height = Length.Percent(100);
                host.style.flexGrow = 1f;
                host.style.marginLeft = 0;
                host.style.marginRight = 0;
                host.style.marginTop = 0;
                host.style.marginBottom = 0;
                host.style.paddingLeft = 0;
                host.style.paddingRight = 0;
                host.style.paddingTop = 0;
                host.style.paddingBottom = 0;
                host.style.backgroundColor = PageBack;
                if (uiFont != null)
                {
                    host.style.unityFont = uiFont;
                }

                root = BuildRoot();
                host.Add(root);
                initialized = true;
                RefreshAll();
                root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
            catch (Exception exception)
            {
                initialized = false;
                root = null;
                host.Clear();
                UnityEngine.Debug.LogError($"NotebookToolkitController failed to initialize: {exception}");
            }
        }

        private bool HasAttachedRoot()
        {
            var host = document != null ? document.rootVisualElement : null;
            return initialized &&
                   root != null &&
                   host != null &&
                   root.parent == host &&
                   host.childCount > 0;
        }

        private VisualElement BuildRoot()
        {
            ResetBuildReferences();

            var dashboard = new VisualElement { name = "dotshelf-notebook-dashboard" };
            dashboard.style.position = Position.Absolute;
            dashboard.style.left = 0;
            dashboard.style.right = 0;
            dashboard.style.top = 0;
            dashboard.style.bottom = 0;
            dashboard.style.flexDirection = FlexDirection.Column;
            dashboard.style.backgroundColor = PageBack;
            dashboard.style.overflow = Overflow.Hidden;

            AddBackdrop(dashboard);

            var shell = new VisualElement { name = "dotshelf-shell" };
            shell.style.flexGrow = 1f;
            shell.style.flexDirection = FlexDirection.Column;
            shell.style.backgroundColor = Rgba(0xF7, 0xFF, 0xF7, 0.76f);
            shell.style.minWidth = 0;
            dashboard.Add(shell);

            BuildHeader(shell);
            var pageScroll = new ScrollView(ScrollViewMode.Vertical) { name = "notebook-page-scroll" };
            pageScrollView = pageScroll;
            pageScroll.style.flexGrow = 1f;
            pageScroll.style.minHeight = 0;
            pageScroll.style.minWidth = 0;
            pageScroll.style.width = Length.Percent(100);
            pageScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            pageScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            pageScroll.contentContainer.style.flexDirection = FlexDirection.Column;
            pageScroll.contentContainer.style.minHeight = 1600;
            pageScroll.contentContainer.style.minWidth = 0;
            pageScroll.RegisterCallback<AttachToPanelEvent>(_ => HideNativeScroller(pageScroll));
            pageScroll.RegisterCallback<GeometryChangedEvent>(_ => UpdateScrollThumb());
            pageScroll.RegisterCallback<WheelEvent>(OnNotebookPageWheel, TrickleDown.TrickleDown);
            pageScroll.verticalScroller.valueChanged += _ => UpdateScrollThumb();
            pageScroll.schedule.Execute(() => HideNativeScroller(pageScroll)).StartingIn(1);
            shell.Add(pageScroll);

            BuildHero(pageScroll.contentContainer);
            BuildSubTabs(pageScroll.contentContainer);
            BuildMain(pageScroll.contentContainer);
            BuildFooter(pageScroll.contentContainer);
            BuildScrollRail(dashboard);
            BuildBookDetailModal(dashboard);
            BuildToast(dashboard);
            UpdateScrollThumb();
            return dashboard;
        }

        private void ResetBuildReferences()
        {
            headerTabButtons.Clear();
            categoryButtons.Clear();
            subTabButtons.Clear();
            genreButtons.Clear();
            searchSuggestionButtons.Clear();
            rankingSortButtons.Clear();
            objectSortButtons.Clear();
            objectCategoryButtons.Clear();
            pageButtons.Clear();
            bookRatingStarButtons.Clear();
            searchResults = Array.Empty<BookViewModel>();

            heroPanel = null;
            searchContainer = null;
            suggestionsContainer = null;
            subTabsRow = null;
            bookMarketContent = null;
            searchResultsContent = null;
            searchResultsList = null;
            bookDetailContent = null;
            bookDetailModalPanel = null;
            otherSpacesContent = null;
            objectMarketContent = null;
            objectList = null;
            recommendList = null;
            rankingGrid = null;
            bookDetailCover = null;
            bookDetailReviews = null;
            bookReviewPanel = null;
            detailCover = null;
            recommendScrollView = null;
            searchField = null;
            bookReviewInput = null;
            clearSearchButton = null;
            bookFavoriteButton = null;
            bookDetailAddButton = null;
            bookReviewMoreButton = null;
            bookReviewToggleButton = null;
            headerSubtitleLabel = null;
            heroEyebrowLabel = null;
            heroTitleLabel = null;
            heroCopyLabel = null;
            pageBalanceLabel = null;
            resultLabel = null;
            searchResultsTitleLabel = null;
            searchResultsCountLabel = null;
            searchResultsHintLabel = null;
            objectResultLabel = null;
            bookDetailTitle = null;
            bookDetailAuthor = null;
            bookDetailPublisher = null;
            bookDetailSummary = null;
            bookDetailMeta = null;
            bookDetailPrice = null;
            bookDetailReviewCount = null;
            detailTitle = null;
            detailAuthor = null;
            detailPublisher = null;
            detailMetaRow = null;
            detailRating = null;
            detailReviews = null;
            detailViews = null;
            detailSummary = null;
            detailShelf = null;
            detailPrice = null;
            toastLabel = null;
            toast = null;
            pageScrollView = null;
            scrollRail = null;
            scrollThumb = null;
            previousPageButton = null;
            nextPageButton = null;
            addButton = null;
            draggingPageScrollbar = false;
            pageScrollbarPointerId = -1;
        }

        private void AddBackdrop(VisualElement parent)
        {
            var sky = new VisualElement { name = "soft-room-backdrop" };
            sky.style.position = Position.Absolute;
            sky.style.left = 0;
            sky.style.right = 0;
            sky.style.top = 0;
            sky.style.bottom = 0;
            sky.style.backgroundColor = Hex(0xF4FAF2);
            parent.Add(sky);

            var wash = new VisualElement { name = "editorial-color-wash" };
            wash.style.position = Position.Absolute;
            wash.style.left = 0;
            wash.style.right = 0;
            wash.style.top = 64;
            wash.style.height = 190;
            wash.style.backgroundColor = Rgba(0x58, 0xB9, 0xFF, 0.10f);
            wash.pickingMode = PickingMode.Ignore;
            parent.Add(wash);

            var warmStrip = new VisualElement { name = "warm-paper-strip" };
            warmStrip.style.position = Position.Absolute;
            warmStrip.style.left = 0;
            warmStrip.style.right = 0;
            warmStrip.style.top = 240;
            warmStrip.style.height = 68;
            warmStrip.style.backgroundColor = Rgba(0xFF, 0xF7, 0xE8, 0.72f);
            warmStrip.pickingMode = PickingMode.Ignore;
            parent.Add(warmStrip);

            var shelfLines = new VisualElement { name = "soft-shelf-lines" };
            shelfLines.style.position = Position.Absolute;
            shelfLines.style.left = 46;
            shelfLines.style.right = 46;
            shelfLines.style.top = 98;
            shelfLines.style.height = 126;
            shelfLines.style.opacity = 0.14f;
            shelfLines.style.flexDirection = FlexDirection.Row;
            shelfLines.style.justifyContent = Justify.SpaceBetween;
            parent.Add(shelfLines);

            for (var i = 0; i < 30; i++)
            {
                var book = new VisualElement();
                book.style.width = 12 + i % 5 * 4;
                book.style.height = 58 + i % 6 * 9;
                book.style.alignSelf = Align.FlexEnd;
                book.style.backgroundColor = (i % 5) switch
                {
                    0 => AccentDeep,
                    1 => DeepGreen,
                    2 => Coral,
                    3 => Gold,
                    _ => Violet,
                };
                Round(book, 2);
                shelfLines.Add(book);
            }
        }

        private void BuildHeader(VisualElement parent)
        {
            var header = Row("header");
            header.style.height = 64;
            header.style.flexShrink = 0;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 18;
            header.style.paddingRight = 16;
            header.style.backgroundColor = Rgba(0xF7, 0xFF, 0xF7, 0.94f);
            BorderBottom(header, Line);
            parent.Add(header);

            var left = Row("brand");
            left.style.width = 190;
            left.style.alignItems = Align.Center;
            header.Add(left);

            var back = IconButton("<", "뒤로");
            back.style.width = 34;
            back.style.height = 34;
            back.clicked += () => closeRequested?.Invoke();
            left.Add(back);
            back.style.display = DisplayStyle.None;

            var brandColumn = new VisualElement();
            brandColumn.style.marginLeft = 0;
            brandColumn.style.flexDirection = FlexDirection.Column;
            left.Add(brandColumn);

            var brand = Text("Dotshelf", 18, Ink, FontStyle.Bold);
            brandColumn.Add(brand);

            headerSubtitleLabel = Text("LAPTOP · BOOK MARKET", 9, FaintInk, FontStyle.Normal);
            headerSubtitleLabel.style.letterSpacing = 2;
            headerSubtitleLabel.style.marginTop = 1;
            brandColumn.Add(headerSubtitleLabel);

            var nav = Row("header-tabs");
            nav.style.flexGrow = 1f;
            nav.style.justifyContent = Justify.Center;
            nav.style.alignItems = Align.Center;
            header.Add(nav);

            foreach (var tab in HeaderTabs)
            {
                var button = TextButton(tab, false);
                button.style.height = 42;
                button.style.minWidth = tab.Length > 3 ? 96 : 76;
                button.style.marginLeft = 2;
                button.style.marginRight = 2;
                var captured = tab;
                button.clicked += () => ActivateHeaderTab(captured);
                headerTabButtons[tab] = button;
                nav.Add(button);
            }

            var right = Row("wallet-area");
            right.style.width = 280;
            right.style.alignItems = Align.Center;
            right.style.justifyContent = Justify.FlexEnd;
            header.Add(right);

            var wallet = Row("wallet");
            wallet.style.display = DisplayStyle.Flex;
            wallet.style.height = 36;
            wallet.style.alignItems = Align.Center;
            wallet.style.paddingLeft = 8;
            wallet.style.paddingRight = 12;
            wallet.style.backgroundColor = Rgba(0xFF, 0xFF, 0xFF, 0.72f);
            Round(wallet, 20);
            Border(wallet, Line);
            right.Add(wallet);

            var coin = Text("P", 13, Color.white, FontStyle.Bold);
            coin.style.width = 22;
            coin.style.height = 22;
            coin.style.unityTextAlign = TextAnchor.MiddleCenter;
            coin.style.backgroundColor = Accent;
            Round(coin, 13);
            wallet.Add(coin);

            pageBalanceLabel = Text(string.Empty, 14, Ink, FontStyle.Bold);
            pageBalanceLabel.style.marginLeft = 8;
            wallet.Add(pageBalanceLabel);

            var charge = TextButton("+ 충전", true);
            charge.text = "+ 충전";
            charge.style.display = DisplayStyle.Flex;
            charge.style.width = 66;
            charge.style.height = 36;
            charge.style.marginLeft = 8;
            charge.clicked += () =>
            {
                pages += 1000;
                ShowToast("+1,000P 충전 완료");
                RefreshWallet();
            };
            right.Add(charge);

            var close = CloseIconButton("닫기");
            close.style.width = 30;
            close.style.height = 30;
            close.style.marginLeft = 8;
            close.style.backgroundColor = Color.clear;
            close.style.borderTopWidth = 0;
            close.style.borderRightWidth = 0;
            close.style.borderBottomWidth = 0;
            close.style.borderLeftWidth = 0;
            close.clicked += () => closeRequested?.Invoke();
            right.Add(close);
        }

        private void BuildHero(VisualElement parent)
        {
            var hero = new VisualElement { name = "hero" };
            heroPanel = hero;
            hero.style.height = 198;
            hero.style.flexShrink = 0;
            hero.style.alignSelf = Align.Stretch;
            hero.style.minWidth = 0;
            hero.style.marginLeft = 20;
            hero.style.marginRight = 20;
            hero.style.marginTop = 10;
            hero.style.marginBottom = 8;
            hero.style.paddingLeft = 24;
            hero.style.paddingRight = 24;
            hero.style.paddingTop = 14;
            hero.style.paddingBottom = 14;
            hero.style.flexDirection = FlexDirection.Column;
            hero.style.backgroundColor = Rgba(0xFF, 0xF7, 0xE8, 0.92f);
            hero.style.overflow = Overflow.Hidden;
            Round(hero, 24);
            Border(hero, Rgba(0x0E, 0x1D, 0x2A, 0.10f));
            parent.Add(hero);

            var heroRail = new VisualElement { name = "hero-accent-rail" };
            heroRail.pickingMode = PickingMode.Ignore;
            heroRail.style.position = Position.Absolute;
            heroRail.style.left = 0;
            heroRail.style.top = 0;
            heroRail.style.bottom = 0;
            heroRail.style.width = 7;
            heroRail.style.backgroundColor = Accent;
            hero.Add(heroRail);

            var bookPattern = Row("hero-book-pattern");
            bookPattern.pickingMode = PickingMode.Ignore;
            bookPattern.style.position = Position.Absolute;
            bookPattern.style.right = 24;
            bookPattern.style.top = 18;
            bookPattern.style.bottom = 18;
            bookPattern.style.width = 250;
            bookPattern.style.alignItems = Align.FlexEnd;
            bookPattern.style.justifyContent = Justify.FlexEnd;
            bookPattern.style.opacity = 0.36f;
            hero.Add(bookPattern);

            var patternColors = new[] { Accent, Gold, Coral, DeepGreen, Violet, Surface, AccentDeep };
            for (var i = 0; i < 16; i++)
            {
                var spine = new VisualElement();
                spine.style.width = 8 + i % 4 * 6;
                spine.style.height = 68 + i % 6 * 12;
                spine.style.marginLeft = 4;
                spine.style.backgroundColor = patternColors[i % patternColors.Length];
                spine.style.alignSelf = Align.FlexEnd;
                Round(spine, 3);
                bookPattern.Add(spine);
            }

            heroEyebrowLabel = Text("DOTSHELF CURATED MARKET", 10, AccentDeep, FontStyle.Bold);
            heroEyebrowLabel.style.letterSpacing = 2.2f;
            heroEyebrowLabel.style.marginBottom = 4;
            hero.Add(heroEyebrowLabel);

            var titleRow = Row("hero-title");
            titleRow.style.alignItems = Align.Center;
            titleRow.style.minHeight = 36;
            titleRow.style.flexWrap = Wrap.Wrap;
            titleRow.style.marginBottom = 10;
            hero.Add(titleRow);

            heroTitleLabel = Text("책 시장", 32, Ink, FontStyle.Bold);
            titleRow.Add(heroTitleLabel);

            heroCopyLabel = Text("오늘 당신의 한 권을 찾아보세요.", 13, SoftInk, FontStyle.Normal);
            heroCopyLabel.style.marginLeft = 14;
            heroCopyLabel.style.marginBottom = 0;
            heroCopyLabel.style.flexGrow = 1f;
            heroCopyLabel.style.minWidth = 0;
            heroCopyLabel.style.maxWidth = 460;
            heroCopyLabel.style.whiteSpace = WhiteSpace.Normal;
            heroCopyLabel.style.minHeight = 20;
            titleRow.Add(heroCopyLabel);

            var search = Row("search");
            searchContainer = search;
            search.style.height = 50;
            search.style.alignItems = Align.Center;
            search.style.width = StyleKeyword.Auto;
            search.style.alignSelf = Align.Stretch;
            search.style.minWidth = 0;
            search.style.flexShrink = 1f;
            search.style.paddingLeft = 18;
            search.style.paddingRight = 8;
            search.style.backgroundColor = Rgba(0xFF, 0xFF, 0xFF, 0.94f);
            Round(search, 16);
            Border(search, Rgba(0x58, 0xB9, 0xFF, 0.58f));
            hero.Add(search);

            var searchIcon = new VisualElement { name = "search-icon" };
            searchIcon.style.width = 22;
            searchIcon.style.height = 22;
            searchIcon.style.marginLeft = 2;
            searchIcon.style.marginRight = 8;
            searchIcon.style.flexShrink = 0;
            searchIcon.style.backgroundImage = new StyleBackground(GetSearchIconTexture());
            searchIcon.pickingMode = PickingMode.Ignore;
            search.Add(searchIcon);

            searchField = new TextField { value = query };
            searchField.textEdition.placeholder = "책 제목, 저자, 출판사로 검색";
            searchField.style.flexGrow = 1f;
            searchField.style.minWidth = 0;
            searchField.style.height = 38;
            searchField.style.marginLeft = 0;
            searchField.style.marginRight = 10;
            searchField.style.backgroundColor = Color.clear;
            searchField.style.borderTopWidth = 0;
            searchField.style.borderRightWidth = 0;
            searchField.style.borderBottomWidth = 0;
            searchField.style.borderLeftWidth = 0;
            searchField.style.fontSize = 15;
            searchField.style.color = Ink;
            if (uiFont != null)
            {
                searchField.style.unityFont = uiFont;
            }
            searchField.RegisterCallback<AttachToPanelEvent>(_ => StyleTextField(searchField));
            searchField.schedule.Execute(() => StyleTextField(searchField)).StartingIn(1);
            searchField.RegisterValueChangedCallback(evt =>
            {
                query = evt.newValue ?? string.Empty;
                var normalizedQuery = query.Trim();
                activeSearchSuggestion = SuggestedSearches.Contains(normalizedQuery) ? normalizedQuery : string.Empty;
                RefreshFilters();
            });
            search.Add(searchField);

            clearSearchButton = CloseIconButton("검색어 지우기");
            clearSearchButton.style.width = 34;
            clearSearchButton.style.height = 34;
            clearSearchButton.style.marginRight = 6;
            clearSearchButton.style.backgroundColor = Color.clear;
            clearSearchButton.style.borderTopWidth = 0;
            clearSearchButton.style.borderRightWidth = 0;
            clearSearchButton.style.borderBottomWidth = 0;
            clearSearchButton.style.borderLeftWidth = 0;
            clearSearchButton.clicked += ClearSearch;
            search.Add(clearSearchButton);

            var searchButton = TextButton("검색", true);
            searchButton.tooltip = "검색";
            searchButton.style.width = 58;
            searchButton.style.flexShrink = 0;
            searchButton.style.height = 34;
            searchButton.style.paddingLeft = 10;
            searchButton.style.paddingRight = 10;
            searchButton.style.backgroundColor = Accent;
            searchButton.style.color = Color.white;
            Border(searchButton, Rgba(0x58, 0xB9, 0xFF, 0.86f));
            searchButton.clicked += SubmitSearch;
            search.Add(searchButton);

            var suggestions = Row("search-suggestions");
            suggestionsContainer = suggestions;
            suggestions.style.alignItems = Align.Center;
            suggestions.style.flexWrap = Wrap.Wrap;
            suggestions.style.marginTop = 10;
            suggestions.style.minHeight = 34;
            hero.Add(suggestions);

            var suggestionLabel = Text("추천 검색어", 10, FaintInk, FontStyle.Normal);
            suggestionLabel.style.letterSpacing = 1.6f;
            suggestionLabel.style.marginRight = 10;
            suggestionLabel.style.marginBottom = 6;
            suggestionLabel.style.minWidth = 70;
            suggestions.Add(suggestionLabel);

            foreach (var suggestion in SuggestedSearches)
            {
                var chip = TextButton(suggestion, false);
                chip.style.height = 26;
                chip.style.minWidth = Mathf.Max(54, suggestion.Length * 15);
                chip.style.marginRight = 8;
                chip.style.marginBottom = 6;
                chip.style.paddingLeft = 12;
                chip.style.paddingRight = 12;
                chip.style.backgroundColor = Rgba(0xFF, 0xFF, 0xFF, 0.62f);
                chip.style.color = SoftInk;
                Border(chip, Rgba(0x0E, 0x1D, 0x2A, 0.10f));
                var capturedSuggestion = suggestion;
                chip.clicked += () => ToggleSearchSuggestion(capturedSuggestion);
                searchSuggestionButtons[suggestion] = chip;
                suggestions.Add(chip);
            }
        }

        private void BuildSubTabs(VisualElement parent)
        {
            var tabs = Row("sub-tabs");
            subTabsRow = tabs;
            tabs.style.height = 54;
            tabs.style.flexShrink = 0;
            tabs.style.alignItems = Align.Center;
            tabs.style.paddingLeft = 24;
            tabs.style.paddingRight = 24;
            tabs.style.paddingTop = 6;
            tabs.style.paddingBottom = 6;
            tabs.style.backgroundColor = Rgba(0xFF, 0xFF, 0xFF, 0.34f);
            BorderBottom(tabs, Line);
            parent.Add(tabs);

            foreach (var tab in SubTabs)
            {
                var button = TextButton(tab, false);
                button.style.height = 34;
                button.style.width = tab == SubTabs[0] ? 112 : 84;
                button.style.marginRight = 8;
                button.style.backgroundColor = Rgba(0xFF, 0xFF, 0xFF, 0.54f);
                Border(button, Rgba(0x0E, 0x1D, 0x2A, 0.08f));
                var captured = tab;
                button.clicked += () =>
                {
                    activeSubTab = captured;
                    pageIndex = 0;
                    RefreshAll();
                };
                subTabButtons[tab] = button;
                tabs.Add(button);
            }
        }

        private void BuildMain(VisualElement parent)
        {
            var body = new VisualElement { name = "main" };
            body.style.flexShrink = 0;
            body.style.flexDirection = FlexDirection.Column;
            body.style.width = StyleKeyword.Auto;
            body.style.alignSelf = Align.Stretch;
            body.style.minWidth = 0;
            body.style.paddingLeft = 20;
            body.style.paddingRight = 20;
            body.style.paddingTop = 10;
            body.style.paddingBottom = 18;
            parent.Add(body);

            bookMarketContent = CreateModeContent("book-market-content");
            body.Add(bookMarketContent);

            var center = new VisualElement { name = "center-content" };
            center.style.flexDirection = FlexDirection.Column;
            center.style.width = StyleKeyword.Auto;
            center.style.alignSelf = Align.Stretch;
            center.style.minWidth = 0;
            center.style.marginTop = 0;
            bookMarketContent.Add(center);

            BuildRecommend(center);
            BuildRanking(center);

            searchResultsContent = CreateModeContent("search-results-content");
            searchResultsContent.style.display = DisplayStyle.None;
            body.Add(searchResultsContent);
            BuildSearchResultsPage(searchResultsContent);

            otherSpacesContent = CreateModeContent("other-spaces-content");
            otherSpacesContent.style.display = DisplayStyle.None;
            body.Add(otherSpacesContent);
            BuildOtherSpaces(otherSpacesContent);

            objectMarketContent = CreateModeContent("object-market-content");
            objectMarketContent.style.display = DisplayStyle.None;
            body.Add(objectMarketContent);
            BuildObjectMarket(objectMarketContent);
        }

        private static VisualElement CreateModeContent(string name)
        {
            return new VisualElement
            {
                name = name,
                style =
                {
                    flexDirection = FlexDirection.Column,
                    alignSelf = Align.Stretch,
                    minWidth = 0,
                },
            };
        }

        private void BuildSectionHeader(VisualElement parent, string eyebrow, string title, string copy)
        {
            var header = Row($"{parent.name}-header");
            header.style.alignItems = Align.FlexEnd;
            header.style.minHeight = 72;
            header.style.marginBottom = 14;
            parent.Add(header);

            var copyColumn = new VisualElement();
            copyColumn.style.flexGrow = 1f;
            copyColumn.style.minWidth = 0;
            header.Add(copyColumn);

            var eyebrowLabel = Text(eyebrow, 10, AccentDeep, FontStyle.Bold);
            eyebrowLabel.style.letterSpacing = 2;
            eyebrowLabel.style.minHeight = 18;
            copyColumn.Add(eyebrowLabel);

            var titleLabel = Text(title, 22, Ink, FontStyle.Bold);
            titleLabel.style.marginTop = 2;
            titleLabel.style.minHeight = 32;
            copyColumn.Add(titleLabel);

            var copyLabel = Text(copy, 12, SoftInk, FontStyle.Normal);
            copyLabel.style.marginTop = 4;
            copyLabel.style.whiteSpace = WhiteSpace.Normal;
            copyLabel.style.minHeight = 20;
            copyColumn.Add(copyLabel);
        }

        private void BuildSearchResultsPage(VisualElement parent)
        {
            parent.style.paddingTop = 0;

            var panel = new VisualElement { name = "search-results-panel" };
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.paddingLeft = 16;
            panel.style.paddingRight = 16;
            panel.style.paddingTop = 12;
            panel.style.paddingBottom = 18;
            panel.style.backgroundColor = Rgba(0xFA, 0xFE, 0xFB, 0.94f);
            panel.style.minHeight = 370;
            Round(panel, 18);
            Border(panel, Rgba(0x58, 0xB9, 0xFF, 0.16f));
            parent.Add(panel);

            searchResultsList = Row("search-results-list");
            searchResultsList.style.flexWrap = Wrap.Wrap;
            searchResultsList.style.alignItems = Align.Stretch;
            searchResultsList.style.marginTop = 0;
            searchResultsList.style.minHeight = 326;
            searchResultsList.style.paddingLeft = 2;
            searchResultsList.style.paddingRight = 2;
            panel.Add(searchResultsList);
        }

        private void BuildBookDetailModal(VisualElement parent)
        {
            bookDetailContent = new VisualElement { name = "book-detail-modal-overlay" };
            bookDetailContent.style.position = Position.Absolute;
            bookDetailContent.style.left = 0;
            bookDetailContent.style.right = 0;
            bookDetailContent.style.top = 0;
            bookDetailContent.style.bottom = 0;
            bookDetailContent.style.display = DisplayStyle.None;
            bookDetailContent.style.alignItems = Align.Center;
            bookDetailContent.style.justifyContent = Justify.Center;
            bookDetailContent.style.backgroundColor = Rgba(0x09, 0x16, 0x26, 0.52f);
            bookDetailContent.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == bookDetailContent)
                {
                    CloseBookDetail();
                }
            });
            parent.Add(bookDetailContent);

            var panel = new VisualElement { name = "book-detail-modal" };
            bookDetailModalPanel = panel;
            panel.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            panel.style.width = 600;
            panel.style.height = 650;
            panel.style.maxWidth = Length.Percent(92);
            panel.style.maxHeight = Length.Percent(96);
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.paddingLeft = 24;
            panel.style.paddingRight = 24;
            panel.style.paddingTop = 24;
            panel.style.paddingBottom = 22;
            panel.style.backgroundColor = Rgba(0xF2, 0xFA, 0xF5, 0.99f);
            panel.style.overflow = Overflow.Hidden;
            Round(panel, 28);
            Border(panel, Rgba(0x0E, 0x1D, 0x2A, 0.14f));
            bookDetailContent.Add(panel);

            var close = CloseIconButton("닫기");
            close.style.position = Position.Absolute;
            close.style.right = 14;
            close.style.top = 14;
            close.style.width = 36;
            close.style.height = 36;
            close.style.backgroundColor = Rgba(0xFF, 0xFF, 0xFF, 0.82f);
            close.clicked += CloseBookDetail;
            panel.Add(close);

            var top = Row("book-detail-top");
            top.style.width = Length.Percent(100);
            top.style.height = Length.Percent(100);
            top.style.flexDirection = FlexDirection.Column;
            top.style.flexGrow = 1f;
            top.style.paddingLeft = 0;
            top.style.paddingRight = 0;
            top.style.paddingTop = 0;
            top.style.paddingBottom = 0;
            top.style.flexWrap = Wrap.NoWrap;
            top.style.alignItems = Align.Stretch;
            top.style.flexShrink = 1f;
            panel.Add(top);

            var coverStage = new VisualElement { name = "book-detail-cover-stage" };
            coverStage.style.width = Length.Percent(100);
            coverStage.style.height = 232;
            coverStage.style.flexShrink = 0;
            coverStage.style.marginBottom = 16;
            coverStage.style.alignItems = Align.Center;
            coverStage.style.justifyContent = Justify.Center;
            coverStage.style.backgroundColor = Rgba(0xEA, 0xF5, 0xFF, 0.74f);
            coverStage.style.overflow = Overflow.Hidden;
            Round(coverStage, 22);
            Border(coverStage, Rgba(0x58, 0xB9, 0xFF, 0.20f));
            top.Add(coverStage);

            var coverStageWash = new VisualElement { name = "book-detail-cover-stage-wash" };
            coverStageWash.style.position = Position.Absolute;
            coverStageWash.style.left = 0;
            coverStageWash.style.right = 0;
            coverStageWash.style.bottom = 0;
            coverStageWash.style.height = 62;
            coverStageWash.style.backgroundColor = Rgba(0x0E, 0x1D, 0x2A, 0.06f);
            coverStage.Add(coverStageWash);

            var coverAccentBar = new VisualElement { name = "book-detail-cover-accent" };
            coverAccentBar.style.position = Position.Absolute;
            coverAccentBar.style.left = 22;
            coverAccentBar.style.top = 22;
            coverAccentBar.style.width = 5;
            coverAccentBar.style.height = 56;
            coverAccentBar.style.backgroundColor = Accent;
            Round(coverAccentBar, 3);
            coverStage.Add(coverAccentBar);

            bookDetailCover = new VisualElement { name = "book-detail-cover-slot" };
            bookDetailCover.style.width = 144;
            bookDetailCover.style.height = 208;
            bookDetailCover.style.flexShrink = 0;
            bookDetailCover.style.alignSelf = Align.Center;
            bookDetailCover.style.marginRight = 0;
            bookDetailCover.style.marginBottom = 0;
            bookDetailCover.style.alignItems = Align.Center;
            bookDetailCover.style.justifyContent = Justify.Center;
            coverStage.Add(bookDetailCover);

            var info = new VisualElement { name = "book-detail-info" };
            info.style.flexGrow = 1f;
            info.style.minWidth = 0;
            info.style.width = Length.Percent(100);
            info.style.flexDirection = FlexDirection.Column;
            info.style.paddingLeft = 18;
            info.style.paddingRight = 18;
            info.style.paddingTop = 16;
            info.style.paddingBottom = 16;
            info.style.backgroundColor = Rgba(0xFF, 0xFF, 0xFF, 0.74f);
            Round(info, 20);
            Border(info, Rgba(0x0E, 0x1D, 0x2A, 0.09f));
            top.Add(info);

            bookDetailMeta = Text(string.Empty, 11, AccentDeep, FontStyle.Bold);
            bookDetailMeta.style.letterSpacing = 5f;
            bookDetailMeta.style.marginBottom = 5;
            bookDetailMeta.style.unityTextAlign = TextAnchor.MiddleLeft;
            info.Add(bookDetailMeta);

            bookDetailTitle = Text(string.Empty, 25, Ink, FontStyle.Bold);
            bookDetailTitle.style.whiteSpace = WhiteSpace.Normal;
            bookDetailTitle.style.minHeight = 32;
            bookDetailTitle.style.unityTextAlign = TextAnchor.MiddleLeft;
            info.Add(bookDetailTitle);

            bookDetailAuthor = Text(string.Empty, 14, SoftInk, FontStyle.Normal);
            bookDetailAuthor.style.marginTop = 5;
            bookDetailAuthor.style.whiteSpace = WhiteSpace.Normal;
            bookDetailAuthor.style.unityTextAlign = TextAnchor.MiddleLeft;
            info.Add(bookDetailAuthor);

            bookDetailPublisher = Text(string.Empty, 13, FaintInk, FontStyle.Normal);
            bookDetailPublisher.style.marginTop = 4;
            bookDetailPublisher.style.whiteSpace = WhiteSpace.Normal;
            bookDetailPublisher.style.unityTextAlign = TextAnchor.MiddleLeft;
            info.Add(bookDetailPublisher);

            bookDetailSummary = Text(string.Empty, 13, Ink, FontStyle.Italic);
            bookDetailSummary.style.marginTop = 14;
            bookDetailSummary.style.paddingLeft = 14;
            bookDetailSummary.style.paddingRight = 12;
            bookDetailSummary.style.paddingTop = 10;
            bookDetailSummary.style.paddingBottom = 10;
            bookDetailSummary.style.whiteSpace = WhiteSpace.Normal;
            bookDetailSummary.style.minHeight = 48;
            bookDetailSummary.style.backgroundColor = Rgba(0xF7, 0xFF, 0xF7, 0.86f);
            bookDetailSummary.style.borderLeftWidth = 3;
            bookDetailSummary.style.borderLeftColor = Accent;
            Round(bookDetailSummary, 12);
            info.Add(bookDetailSummary);

            var purchasePanel = Row("book-detail-purchase");
            purchasePanel.style.height = 64;
            purchasePanel.style.marginTop = 12;
            purchasePanel.style.paddingLeft = 16;
            purchasePanel.style.paddingRight = 16;
            purchasePanel.style.paddingTop = 8;
            purchasePanel.style.paddingBottom = 8;
            purchasePanel.style.alignItems = Align.Center;
            purchasePanel.style.backgroundColor = Rgba(0x0E, 0x1D, 0x2A, 0.04f);
            Round(purchasePanel, 16);
            Border(purchasePanel, Rgba(0x58, 0xB9, 0xFF, 0.18f));
            info.Add(purchasePanel);

            var priceColumn = new VisualElement { name = "book-detail-price-column" };
            priceColumn.style.flexGrow = 1f;
            priceColumn.style.minWidth = 0;
            purchasePanel.Add(priceColumn);

            var priceLabel = Text("표지 가격", 12, FaintInk, FontStyle.Normal);
            priceLabel.style.letterSpacing = 2f;
            priceColumn.Add(priceLabel);

            bookDetailPrice = Text(string.Empty, 21, AccentDeep, FontStyle.Bold);
            bookDetailPrice.style.marginTop = 4;
            priceColumn.Add(bookDetailPrice);

            bookDetailAddButton = TextButton("구매하고 책장에 추가", true);
            bookDetailAddButton.style.width = 162;
            bookDetailAddButton.style.height = 40;
            bookDetailAddButton.style.fontSize = 13;
            bookDetailAddButton.style.backgroundColor = AccentDeep;
            bookDetailAddButton.clicked += () =>
            {
                PulseElement(bookDetailAddButton);
                AddBookToShelf(GetSelectedBook());
            };
            purchasePanel.Add(bookDetailAddButton);

            var actionRow = Row("book-detail-action-row");
            actionRow.style.marginTop = 12;
            actionRow.style.height = 36;
            actionRow.style.alignItems = Align.Center;
            actionRow.style.justifyContent = Justify.FlexStart;
            actionRow.style.flexWrap = Wrap.Wrap;
            info.Add(actionRow);

            bookFavoriteButton = TextButton("즐겨찾기", false);
            bookFavoriteButton.style.height = 32;
            bookFavoriteButton.style.marginRight = 10;
            bookFavoriteButton.clicked += () =>
            {
                PulseElement(bookFavoriteButton);
                ToggleFavoriteBook();
            };
            actionRow.Add(bookFavoriteButton);

            bookDetailReviewCount = Text(string.Empty, 12, Ink, FontStyle.Bold);
            bookDetailReviewCount.style.height = 32;
            bookDetailReviewCount.style.paddingLeft = 12;
            bookDetailReviewCount.style.paddingRight = 12;
            bookDetailReviewCount.style.marginRight = 8;
            bookDetailReviewCount.style.unityTextAlign = TextAnchor.MiddleCenter;
            bookDetailReviewCount.style.backgroundColor = Rgba(0x0E, 0x1D, 0x2A, 0.06f);
            Round(bookDetailReviewCount, 14);
            actionRow.Add(bookDetailReviewCount);

            bookReviewToggleButton = TextButton("리뷰 보기", false);
            bookReviewToggleButton.style.height = 32;
            bookReviewToggleButton.style.paddingLeft = 13;
            bookReviewToggleButton.style.paddingRight = 13;
            bookReviewToggleButton.style.backgroundColor = Rgba(0x58, 0xB9, 0xFF, 0.12f);
            bookReviewToggleButton.style.color = AccentDeep;
            Border(bookReviewToggleButton, Rgba(0x58, 0xB9, 0xFF, 0.34f));
            bookReviewToggleButton.clicked += () =>
            {
                PulseElement(bookReviewToggleButton);
                ToggleBookReviews();
            };
            actionRow.Add(bookReviewToggleButton);

            var reviewPanel = new VisualElement { name = "book-review-panel" };
            bookReviewPanel = reviewPanel;
            reviewPanel.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            reviewPanel.style.display = DisplayStyle.None;
            reviewPanel.style.position = Position.Absolute;
            reviewPanel.style.right = 22;
            reviewPanel.style.top = 30;
            reviewPanel.style.bottom = 30;
            reviewPanel.style.width = 360;
            reviewPanel.style.flexGrow = 0f;
            reviewPanel.style.flexShrink = 1f;
            reviewPanel.style.marginTop = 0;
            reviewPanel.style.paddingLeft = 20;
            reviewPanel.style.paddingRight = 20;
            reviewPanel.style.paddingTop = 18;
            reviewPanel.style.paddingBottom = 16;
            reviewPanel.style.backgroundColor = Rgba(0xFF, 0xFF, 0xFF, 0.96f);
            reviewPanel.style.borderLeftWidth = 0;
            reviewPanel.style.borderLeftColor = Color.clear;
            Round(reviewPanel, 18);
            Border(reviewPanel, Rgba(0x0E, 0x1D, 0x2A, 0.10f));
            bookDetailContent.Add(reviewPanel);

            var reviewTitle = Row("review-title-row");
            reviewTitle.style.height = 28;
            reviewTitle.style.alignItems = Align.Center;
            reviewTitle.style.marginBottom = 6;
            reviewPanel.Add(reviewTitle);
            var reviewHeading = Text("인기 리뷰", 17, Ink, FontStyle.Bold);
            reviewTitle.Add(reviewHeading);
            var reviewHint = Text("더보기를 누르면 전체 리뷰로 전환됩니다.", 11, FaintInk, FontStyle.Normal);
            reviewHint.style.marginLeft = 10;
            reviewTitle.Add(reviewHint);
            var reviewTitleSpacer = new VisualElement();
            reviewTitleSpacer.style.flexGrow = 1f;
            reviewTitle.Add(reviewTitleSpacer);
            bookReviewMoreButton = TextButton("전체보기", false);
            bookReviewMoreButton.style.height = 28;
            bookReviewMoreButton.style.paddingLeft = 10;
            bookReviewMoreButton.style.paddingRight = 10;
            bookReviewMoreButton.style.backgroundColor = Rgba(0x0E, 0x1D, 0x2A, 0.06f);
            bookReviewMoreButton.clicked += () =>
            {
                PulseElement(bookReviewMoreButton);
                ToggleReviewMode();
            };
            reviewTitle.Add(bookReviewMoreButton);

            var reviewCloseButton = CloseIconButton("리뷰 닫기");
            reviewCloseButton.style.width = 28;
            reviewCloseButton.style.height = 28;
            reviewCloseButton.style.marginLeft = 6;
            reviewCloseButton.style.backgroundColor = Rgba(0x0E, 0x1D, 0x2A, 0.04f);
            reviewCloseButton.clicked += () => SetBookReviewsOpen(false, true);
            reviewTitle.Add(reviewCloseButton);

            var reviewScroller = new ScrollView(ScrollViewMode.Vertical) { name = "book-detail-review-scroll" };
            reviewScroller.style.flexGrow = 1f;
            reviewScroller.style.minHeight = 0;
            reviewScroller.style.marginBottom = 10;
            reviewScroller.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            reviewScroller.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            reviewScroller.RegisterCallback<AttachToPanelEvent>(_ => HideNativeScroller(reviewScroller));
            reviewPanel.Add(reviewScroller);

            bookDetailReviews = reviewScroller.contentContainer;
            bookDetailReviews.style.flexDirection = FlexDirection.Column;

            var form = Row("review-form");
            form.style.marginTop = 8;
            form.style.flexDirection = FlexDirection.Column;
            form.style.flexWrap = Wrap.NoWrap;
            form.style.alignItems = Align.Stretch;
            reviewPanel.Add(form);

            var ratingColumn = new VisualElement { name = "rating-input-column" };
            ratingColumn.style.width = Length.Percent(100);
            ratingColumn.style.marginRight = 0;
            ratingColumn.style.marginBottom = 8;
            form.Add(ratingColumn);
            var ratingLabel = Text("평점", 10, FaintInk, FontStyle.Bold);
            ratingLabel.style.letterSpacing = 1.2f;
            ratingLabel.style.marginBottom = 5;
            ratingColumn.Add(ratingLabel);
            var ratingFrame = Row("star-rating-row");
            ratingFrame.style.height = 38;
            ratingFrame.style.alignItems = Align.Center;
            ratingFrame.style.paddingLeft = 2;
            ratingFrame.style.paddingRight = 2;
            ratingFrame.style.backgroundColor = Color.clear;
            ratingColumn.Add(ratingFrame);
            bookRatingStarButtons.Clear();
            for (var ratingIndex = 1; ratingIndex <= 5; ratingIndex++)
            {
                var capturedRating = ratingIndex;
                var star = TextButton("★", false);
                star.style.width = 34;
                star.style.height = 34;
                star.style.paddingLeft = 0;
                star.style.paddingRight = 0;
                star.style.marginRight = 4;
                star.style.fontSize = 22;
                star.style.backgroundColor = Rgba(0x0E, 0x1D, 0x2A, 0.05f);
                star.clicked += () =>
                {
                    SetSelectedReviewRating(capturedRating);
                    PulseElement(star);
                };
                bookRatingStarButtons.Add(star);
                ratingFrame.Add(star);
            }
            RefreshRatingStars();

            var reviewColumn = new VisualElement { name = "review-input-column" };
            reviewColumn.style.width = Length.Percent(100);
            reviewColumn.style.minWidth = 0;
            reviewColumn.style.marginRight = 0;
            reviewColumn.style.marginBottom = 8;
            form.Add(reviewColumn);
            var inputLabel = Text("리뷰 내용", 10, FaintInk, FontStyle.Bold);
            inputLabel.style.letterSpacing = 1.2f;
            inputLabel.style.marginBottom = 5;
            reviewColumn.Add(inputLabel);
            var reviewFrame = CreateInputFrame(58);
            reviewColumn.Add(reviewFrame);
            bookReviewInput = new TextField { multiline = true };
            bookReviewInput.textEdition.placeholder = "이 책을 읽고 느낀 점을 적어주세요.";
            bookReviewInput.style.flexGrow = 1f;
            bookReviewInput.style.height = 50;
            bookReviewInput.style.fontSize = 13;
            RegisterFieldStyling(bookReviewInput);
            reviewFrame.Add(bookReviewInput);

            var submit = TextButton("리뷰 등록", true);
            submit.style.width = Length.Percent(100);
            submit.style.height = 38;
            submit.style.marginBottom = 8;
            submit.clicked += () =>
            {
                PulseElement(submit);
                SubmitBookReview();
            };
            form.Add(submit);
        }

        private VisualElement CreateInputFrame(float height)
        {
            var frame = new VisualElement();
            frame.style.height = height;
            frame.style.paddingLeft = 12;
            frame.style.paddingRight = 12;
            frame.style.paddingTop = 4;
            frame.style.paddingBottom = 4;
            frame.style.backgroundColor = Rgba(0xFF, 0xFF, 0xFF, 0.88f);
            Round(frame, 12);
            Border(frame, Rgba(0x0E, 0x1D, 0x2A, 0.12f));
            return frame;
        }

        private void RegisterFieldStyling(TextField field)
        {
            if (field == null)
            {
                return;
            }

            field.RegisterCallback<AttachToPanelEvent>(_ => StyleTextField(field));
            field.schedule.Execute(() => StyleTextField(field)).StartingIn(1);
        }

        private void BuildOtherSpaces(VisualElement parent)
        {
            BuildSectionHeader(
                parent,
                "OTHER SPACES · 탐색",
                "다른 이의 공간",
                "명세의 방문하기, 팔로우, 공간 좋아요 액션을 바로 확인할 수 있습니다.");

            var grid = Row("other-spaces-grid");
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.alignItems = Align.Stretch;
            parent.Add(grid);

            foreach (var space in spaces)
            {
                grid.Add(CreateSpaceCard(space));
            }
        }

        private VisualElement CreateSpaceCard(SpaceViewModel space)
        {
            var card = new VisualElement { name = $"space-{space.Id}" };
            card.style.width = Length.Percent(48.7f);
            card.style.minWidth = 280;
            card.style.minHeight = 314;
            card.style.marginRight = 10;
            card.style.marginBottom = 10;
            card.style.paddingLeft = 0;
            card.style.paddingRight = 0;
            card.style.paddingTop = 0;
            card.style.paddingBottom = 0;
            card.style.backgroundColor = Rgba(0xFF, 0xFF, 0xFF, 0.78f);
            card.style.overflow = Overflow.Hidden;
            Round(card, 18);
            Border(card, Rgba(0x0E, 0x1D, 0x2A, 0.10f));
            UsePointerCursor(card);
            card.RegisterCallback<ClickEvent>(_ => ShowToast($"공간 상세로 이동 · {space.Title}"));

            var thumbnail = new VisualElement { name = "space-thumbnail" };
            thumbnail.style.height = 132;
            thumbnail.style.flexShrink = 0;
            thumbnail.style.overflow = Overflow.Hidden;
            BuildSpaceThumbnail(thumbnail, space);
            card.Add(thumbnail);

            var shade = new VisualElement { name = "space-thumbnail-shade" };
            shade.pickingMode = PickingMode.Ignore;
            shade.style.position = Position.Absolute;
            shade.style.left = 0;
            shade.style.right = 0;
            shade.style.top = 0;
            shade.style.bottom = 0;
            shade.style.backgroundColor = Rgba(0x0E, 0x1D, 0x2A, 0.18f);
            thumbnail.Add(shade);

            var photoBadge = Text("공간 사진", 9, Surface, FontStyle.Bold);
            photoBadge.style.position = Position.Absolute;
            photoBadge.style.left = 12;
            photoBadge.style.top = 10;
            photoBadge.style.height = 22;
            photoBadge.style.paddingLeft = 9;
            photoBadge.style.paddingRight = 9;
            photoBadge.style.letterSpacing = 1.4f;
            photoBadge.style.unityTextAlign = TextAnchor.MiddleCenter;
            photoBadge.style.backgroundColor = Rgba(0x0E, 0x1D, 0x2A, 0.46f);
            Round(photoBadge, 11);
            thumbnail.Add(photoBadge);

            var visitOverlay = TextButton("방문하기", true);
            visitOverlay.style.position = Position.Absolute;
            visitOverlay.style.right = 12;
            visitOverlay.style.bottom = 10;
            visitOverlay.style.height = 30;
            visitOverlay.style.backgroundColor = Rgba(0x58, 0xB9, 0xFF, 0.94f);
            visitOverlay.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            visitOverlay.clicked += () => ShowToast($"공간으로 이동합니다 · {space.Title}");
            thumbnail.Add(visitOverlay);

            var content = new VisualElement { name = "space-card-content" };
            content.style.paddingLeft = 16;
            content.style.paddingRight = 16;
            content.style.paddingTop = 14;
            content.style.paddingBottom = 14;
            card.Add(content);

            var top = Row("space-top");
            top.style.alignItems = Align.Center;
            content.Add(top);

            var titleBox = new VisualElement();
            titleBox.style.flexGrow = 1f;
            titleBox.style.minWidth = 0;
            top.Add(titleBox);

            var mood = Text(space.Mood, 10, AccentDeep, FontStyle.Bold);
            mood.style.letterSpacing = 1.3f;
            mood.style.minHeight = 18;
            titleBox.Add(mood);

            var title = Text(space.Title, 18, Ink, FontStyle.Bold);
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.minHeight = 28;
            titleBox.Add(title);

            var owner = Text(space.Owner, 12, SoftInk, FontStyle.Normal);
            owner.style.marginTop = 2;
            titleBox.Add(owner);

            var avatar = Text(space.Owner.Substring(0, 1), 16, Surface, FontStyle.Bold);
            avatar.style.width = 42;
            avatar.style.height = 42;
            avatar.style.flexShrink = 0;
            avatar.style.unityTextAlign = TextAnchor.MiddleCenter;
            avatar.style.backgroundColor = space.AccentColor;
            Round(avatar, 21);
            top.Add(avatar);

            var description = Text(space.Description, 12, SoftInk, FontStyle.Normal);
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.minHeight = 42;
            description.style.marginTop = 12;
            content.Add(description);

            var stats = Row("space-stats");
            stats.style.marginTop = 10;
            stats.style.minHeight = 22;
            stats.style.alignItems = Align.Center;
            content.Add(stats);
            stats.Add(MetricLabel($"팔로워 {space.Followers:N0}", AccentDeep));
            stats.Add(MetricLabel($"좋아요 {space.Likes:N0}", Ink));
            stats.Add(MetricLabel($"방문 {space.Visits:N0}", Ink));

            var actions = Row("space-actions");
            actions.style.marginTop = 12;
            actions.style.alignItems = Align.Center;
            actions.style.flexWrap = Wrap.Wrap;
            content.Add(actions);

            var visit = TextButton("방문하기", true);
            visit.style.height = 34;
            visit.style.marginRight = 8;
            visit.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            visit.clicked += () => ShowToast($"공간으로 이동합니다 · {space.Title}");
            actions.Add(visit);

            var follow = TextButton(string.Empty, false);
            follow.style.height = 34;
            follow.style.marginRight = 8;
            follow.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            follow.clicked += () =>
            {
                ToggleSet(followedUserIds, space.OwnerId);
                RefreshFollowButton(follow, space.OwnerId);
                ShowToast(followedUserIds.Contains(space.OwnerId) ? $"{space.Owner}님을 팔로우했습니다." : $"{space.Owner}님 팔로우를 해제했습니다.");
            };
            actions.Add(follow);
            RefreshFollowButton(follow, space.OwnerId);

            var like = TextButton(string.Empty, false);
            like.style.height = 34;
            like.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            like.clicked += () =>
            {
                ToggleSet(likedSpaceIds, space.Id);
                RefreshToggleButton(like, likedSpaceIds.Contains(space.Id), "좋아요 취소", "공간 좋아요");
                ShowToast(likedSpaceIds.Contains(space.Id) ? $"공간을 좋아요했습니다 · {space.Title}" : $"공간 좋아요를 취소했습니다 · {space.Title}");
            };
            actions.Add(like);
            RefreshToggleButton(like, likedSpaceIds.Contains(space.Id), "좋아요 취소", "공간 좋아요");

            return card;
        }

        private void BuildObjectMarket(VisualElement parent)
        {
            BuildSectionHeader(
                parent,
                "OBJECT MARKET · 꾸미기",
                "꾸미기 오브젝트 시장",
                "인기순, 최신순, 가격순 정렬과 카드 상세, 찜하기, 구매하기 액션을 제공합니다.");

            var control = Row("object-controls");
            control.style.minHeight = 50;
            control.style.alignItems = Align.Center;
            control.style.flexWrap = Wrap.Wrap;
            control.style.marginBottom = 10;
            parent.Add(control);

            var sortLabel = Text("정렬", 10, FaintInk, FontStyle.Bold);
            sortLabel.style.letterSpacing = 1.4f;
            sortLabel.style.marginRight = 8;
            control.Add(sortLabel);

            foreach (var sort in ObjectSorts)
            {
                var button = TextButton(sort, false);
                button.style.height = 34;
                button.style.width = 68;
                button.style.marginRight = 6;
                button.style.marginBottom = 6;
                var captured = sort;
                button.clicked += () =>
                {
                    activeObjectSort = captured;
                    RefreshFilters();
                    RefreshObjectMarket();
                };
                objectSortButtons[sort] = button;
                control.Add(button);
            }

            objectResultLabel = Text(string.Empty, 12, SoftInk, FontStyle.Normal);
            objectResultLabel.style.marginLeft = 8;
            objectResultLabel.style.marginRight = 14;
            objectResultLabel.style.marginBottom = 6;
            control.Add(objectResultLabel);

            foreach (var category in ObjectCategories)
            {
                var button = TextButton(category, false);
                button.style.height = 34;
                button.style.minWidth = Mathf.Max(58, category.Length * 14);
                button.style.marginRight = 6;
                button.style.marginBottom = 6;
                var captured = category;
                button.clicked += () =>
                {
                    activeObjectCategory = captured;
                    RefreshFilters();
                    RefreshObjectMarket();
                };
                objectCategoryButtons[category] = button;
                control.Add(button);
            }

            objectList = Row("object-list");
            objectList.style.flexWrap = Wrap.Wrap;
            objectList.style.alignItems = Align.Stretch;
            parent.Add(objectList);
        }

        private void RefreshObjectMarket()
        {
            if (objectList == null)
            {
                return;
            }

            objectList.Clear();
            IEnumerable<DecorObjectViewModel> filtered = decorObjects;
            if (activeObjectCategory != "전체")
            {
                filtered = filtered.Where(item => item.Category == activeObjectCategory);
            }

            var source = activeObjectSort switch
            {
                "최신순" => filtered.OrderByDescending(item => item.ReleaseOrder).ThenBy(item => item.Name),
                "가격순" => filtered.OrderBy(item => item.Price).ThenByDescending(item => item.Popularity),
                _ => filtered.OrderByDescending(item => item.Popularity).ThenBy(item => item.Name),
            };

            var items = source.ToArray();
            foreach (var item in items)
            {
                objectList.Add(CreateObjectCard(item));
            }

            if (objectResultLabel != null)
            {
                objectResultLabel.text = $"{items.Length:N0}개";
            }
        }

        private VisualElement CreateObjectCard(DecorObjectViewModel item)
        {
            var card = new VisualElement { name = $"object-{item.Id}" };
            card.style.width = Length.Percent(31.8f);
            card.style.minWidth = 250;
            card.style.minHeight = 252;
            card.style.marginRight = 10;
            card.style.marginBottom = 10;
            card.style.paddingLeft = 14;
            card.style.paddingRight = 14;
            card.style.paddingTop = 14;
            card.style.paddingBottom = 14;
            card.style.backgroundColor = Rgba(0xFF, 0xFF, 0xFF, 0.82f);
            card.style.overflow = Overflow.Hidden;
            Round(card, 18);
            Border(card, Rgba(0x0E, 0x1D, 0x2A, 0.10f));
            UsePointerCursor(card);
            card.RegisterCallback<ClickEvent>(_ => ShowToast($"오브젝트 상세 화면 · {item.Name}"));

            var preview = new VisualElement { name = "object-preview" };
            preview.style.height = 92;
            preview.style.marginBottom = 12;
            preview.style.backgroundColor = Rgba(0xF0, 0xEE, 0xE9, 0.82f);
            preview.style.overflow = Overflow.Hidden;
            Round(preview, 14);
            card.Add(preview);

            var shape = new VisualElement();
            shape.style.position = Position.Absolute;
            shape.style.left = 24;
            shape.style.top = 18;
            shape.style.width = 96;
            shape.style.height = 48;
            shape.style.backgroundColor = item.Color;
            Round(shape, 12);
            preview.Add(shape);

            var glow = new VisualElement();
            glow.style.position = Position.Absolute;
            glow.style.right = 22;
            glow.style.top = 16;
            glow.style.width = 54;
            glow.style.height = 54;
            glow.style.backgroundColor = item.AccentColor;
            glow.style.opacity = 0.72f;
            Round(glow, 27);
            preview.Add(glow);

            var category = Text(item.Category, 10, AccentDeep, FontStyle.Bold);
            category.style.letterSpacing = 1.1f;
            category.style.minHeight = 18;
            card.Add(category);

            var title = Text(item.Name, 17, Ink, FontStyle.Bold);
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.minHeight = 28;
            card.Add(title);

            var description = Text(item.Description, 12, SoftInk, FontStyle.Normal);
            description.style.marginTop = 6;
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.minHeight = 42;
            card.Add(description);

            var meta = Row("object-meta");
            meta.style.marginTop = 10;
            meta.style.alignItems = Align.Center;
            card.Add(meta);
            meta.Add(MetricLabel($"{item.Price:N0}P", AccentDeep));
            meta.Add(MetricLabel($"인기 {item.Popularity:N0}", Ink));

            var actions = Row("object-actions");
            actions.style.marginTop = 12;
            actions.style.alignItems = Align.Center;
            actions.style.flexWrap = Wrap.Wrap;
            card.Add(actions);

            var detail = TextButton("상세 보기", false);
            detail.style.height = 32;
            detail.style.marginRight = 7;
            detail.style.marginBottom = 6;
            detail.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            detail.clicked += () => ShowToast($"오브젝트 상세 화면 · {item.Name}");
            actions.Add(detail);

            var wish = TextButton(string.Empty, false);
            wish.style.height = 32;
            wish.style.marginRight = 7;
            wish.style.marginBottom = 6;
            wish.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            wish.clicked += () =>
            {
                ToggleSet(wishedObjectIds, item.Id);
                RefreshToggleButton(wish, wishedObjectIds.Contains(item.Id), "찜 해제", "찜하기");
                ShowToast(wishedObjectIds.Contains(item.Id) ? $"위시리스트에 추가 · {item.Name}" : $"위시리스트에서 제거 · {item.Name}");
            };
            actions.Add(wish);
            RefreshToggleButton(wish, wishedObjectIds.Contains(item.Id), "찜 해제", "찜하기");

            var purchase = TextButton(ownedObjectIds.Contains(item.Id) ? "소유 중" : "구매하기", true);
            purchase.style.height = 32;
            purchase.style.marginBottom = 6;
            purchase.style.backgroundColor = ownedObjectIds.Contains(item.Id) ? Success : AccentDeep;
            purchase.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            purchase.clicked += () => PurchaseObject(item);
            actions.Add(purchase);

            return card;
        }

        private void BuildSidebar(VisualElement parent)
        {
            var sidebar = new VisualElement { name = "sidebar" };
            sidebar.style.flexShrink = 0;
            sidebar.style.minHeight = 66;
            sidebar.style.flexDirection = FlexDirection.Row;
            sidebar.style.alignItems = Align.Center;
            sidebar.style.paddingLeft = 12;
            sidebar.style.paddingRight = 12;
            sidebar.style.paddingTop = 8;
            sidebar.style.paddingBottom = 8;
            sidebar.style.backgroundColor = Rgba(0x09, 0x16, 0x26, 0.94f);
            Round(sidebar, 16);
            Border(sidebar, Rgba(0x58, 0xB9, 0xFF, 0.22f));
            parent.Add(sidebar);

            var rail = new VisualElement { name = "filter-accent-rail" };
            rail.style.width = 4;
            rail.style.height = 40;
            rail.style.flexShrink = 0;
            rail.style.marginRight = 12;
            rail.style.backgroundColor = Gold;
            Round(rail, 4);
            sidebar.Add(rail);

            var heading = new VisualElement();
            heading.style.width = 116;
            heading.style.flexShrink = 0;
            sidebar.Add(heading);

            var title = Text("책장 필터", 15, Surface, FontStyle.Bold);
            heading.Add(title);
            var hint = Text("상태와 위치", 9, Rgba(0xF7, 0xFF, 0xF7, 0.56f), FontStyle.Normal);
            hint.style.marginTop = 2;
            heading.Add(hint);

            var chips = Row("category-chips");
            chips.style.flexGrow = 1f;
            chips.style.flexWrap = Wrap.Wrap;
            chips.style.alignItems = Align.Center;
            sidebar.Add(chips);

            for (var i = 0; i < Categories.Length; i++)
            {
                var category = Categories[i];
                var button = TextButton($"{CategoryIcon(i)} {category}", false);
                button.style.height = 32;
                button.style.minWidth = i >= 4 ? 94 : 80;
                button.style.marginLeft = 4;
                button.style.marginRight = 4;
                button.style.marginBottom = 4;
                button.style.justifyContent = Justify.FlexStart;
                button.style.backgroundColor = Color.clear;
                button.style.color = Rgba(0xF7, 0xFF, 0xF7, 0.82f);
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                var captured = category;
                button.clicked += () =>
                {
                    activeCategory = captured;
                    pageIndex = 0;
                    RefreshAll();
                };
                categoryButtons[category] = button;
                chips.Add(button);
            }
        }

        private void BuildRecommend(VisualElement parent)
        {
            var header = Row("recommend-header");
            header.style.height = 44;
            header.style.alignItems = Align.FlexEnd;
            parent.Add(header);

            var title = Text("오늘의 추천 책", 18, Ink, FontStyle.Bold);
            title.style.minHeight = 32;
            header.Add(title);

            var hint = Text("CURATED FOR YOU · 04·22", 10, FaintInk, FontStyle.Normal);
            hint.style.marginLeft = 12;
            hint.style.marginBottom = 4;
            hint.style.letterSpacing = 2;
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.minHeight = 22;
            header.Add(hint);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            spacer.style.minWidth = 12;
            header.Add(spacer);

            var previous = IconButton("<", "추천 책 이전");
            previous.style.width = 32;
            previous.style.height = 32;
            previous.style.marginRight = 6;
            previous.clicked += () => ScrollRecommendBy(-320f);
            header.Add(previous);

            var next = IconButton(">", "추천 책 다음");
            next.style.width = 32;
            next.style.height = 32;
            next.clicked += () => ScrollRecommendBy(320f);
            header.Add(next);

            var scroller = new ScrollView(ScrollViewMode.Horizontal) { name = "recommend-scroll" };
            recommendScrollView = scroller;
            scroller.style.height = 368;
            scroller.style.flexShrink = 0;
            scroller.style.marginTop = 8;
            scroller.style.marginBottom = 12;
            scroller.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroller.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            recommendList = scroller.contentContainer;
            recommendList.style.flexDirection = FlexDirection.Row;
            parent.Add(scroller);
        }

        private void BuildRanking(VisualElement parent)
        {
            var top = Row("ranking-top");
            top.style.height = 62;
            top.style.alignItems = Align.Center;
            parent.Add(top);

            var titleBox = new VisualElement();
            titleBox.style.flexGrow = 1f;
            titleBox.style.minWidth = 0;
            top.Add(titleBox);

            var eyebrow = Text("LIVE RANKING · 실시간 집계", 10, FaintInk, FontStyle.Normal);
            eyebrow.style.letterSpacing = 2;
            eyebrow.style.minHeight = 18;
            titleBox.Add(eyebrow);

            var title = Text("지금 인기 있는 책", 18, Ink, FontStyle.Bold);
            title.style.marginTop = 2;
            title.style.minHeight = 32;
            titleBox.Add(title);

            resultLabel = Text(string.Empty, 12, SoftInk, FontStyle.Normal);
            resultLabel.style.marginRight = 10;
            resultLabel.style.minWidth = 118;
            resultLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            top.Add(resultLabel);

            var sortRow = Row("ranking-sort-row");
            sortRow.style.alignItems = Align.Center;
            sortRow.style.marginRight = 4;
            top.Add(sortRow);

            var sortLabel = Text("정렬", 10, FaintInk, FontStyle.Bold);
            sortLabel.style.letterSpacing = 1.3f;
            sortLabel.style.marginRight = 6;
            sortRow.Add(sortLabel);

            foreach (var sort in RankingSorts)
            {
                var button = TextButton(sort, false);
                button.style.height = 30;
                button.style.width = 58;
                button.style.marginRight = 4;
                var captured = sort;
                button.clicked += () =>
                {
                    activeRankingSort = captured;
                    pageIndex = 0;
                    RefreshFilters();
                    RefreshLists();
                };
                rankingSortButtons[sort] = button;
                sortRow.Add(button);
            }

            var genreRow = Row("genre-row");
            genreRow.style.minHeight = 50;
            genreRow.style.alignItems = Align.Center;
            genreRow.style.flexWrap = Wrap.Wrap;
            parent.Add(genreRow);

            foreach (var genre in Genres)
            {
                var button = TextButton(genre, false);
                button.style.height = 38;
                button.style.width = genre.Length > 4 ? 92 : 66;
                button.style.marginRight = 5;
                button.style.marginBottom = 5;
                var captured = genre;
                button.clicked += () =>
                {
                    activeGenre = captured;
                    pageIndex = 0;
                    RefreshAll();
                };
                genreButtons[genre] = button;
                genreRow.Add(button);
            }

            rankingGrid = new VisualElement { name = "ranking-grid" };
            rankingGrid.style.flexShrink = 0;
            rankingGrid.style.minHeight = 0;
            rankingGrid.style.marginTop = 8;
            rankingGrid.style.flexDirection = FlexDirection.Row;
            rankingGrid.style.flexWrap = Wrap.Wrap;
            parent.Add(rankingGrid);

            var pager = Row("pager");
            pager.style.height = 36;
            pager.style.flexShrink = 0;
            pager.style.alignItems = Align.Center;
            pager.style.justifyContent = Justify.Center;
            pager.style.marginTop = 8;
            parent.Add(pager);

            previousPageButton = TextButton("<", false);
            previousPageButton.style.width = 34;
            previousPageButton.clicked += () => ChangePage(-1);
            pager.Add(previousPageButton);

            for (var i = 0; i < 4; i++)
            {
                var button = TextButton((i + 1).ToString(), false);
                button.style.width = 34;
                button.style.marginLeft = 4;
                button.style.marginRight = 4;
                var capturedPage = i;
                button.clicked += () =>
                {
                    pageIndex = capturedPage;
                    RefreshLists();
                };
                pageButtons.Add(button);
                pager.Add(button);
            }

            nextPageButton = TextButton(">", false);
            nextPageButton.style.width = 34;
            nextPageButton.clicked += () => ChangePage(1);
            pager.Add(nextPageButton);
        }

        private void BuildDetail(VisualElement parent)
        {
            var panel = new VisualElement { name = "detail" };
            panel.style.width = Length.Percent(100);
            panel.style.flexShrink = 0;
            panel.style.marginLeft = 0;
            panel.style.marginBottom = 18;
            panel.style.paddingLeft = 18;
            panel.style.paddingRight = 18;
            panel.style.paddingTop = 16;
            panel.style.paddingBottom = 16;
            panel.style.backgroundColor = Rgba(0xFF, 0xF7, 0xE8, 0.90f);
            Round(panel, 18);
            Border(panel, Rgba(0x0E, 0x1D, 0x2A, 0.14f));
            panel.style.borderLeftWidth = 4;
            panel.style.borderLeftColor = Accent;
            parent.Add(panel);

            var eyebrow = Text("선택한 책", 10, AccentDeep, FontStyle.Bold);
            eyebrow.style.letterSpacing = 2;
            panel.Add(eyebrow);

            var main = Row("detail-main");
            main.style.minHeight = 140;
            main.style.marginTop = 10;
            panel.Add(main);

            detailCover = new VisualElement { name = "detail-cover" };
            detailCover.style.width = 94;
            detailCover.style.height = 136;
            detailCover.style.flexShrink = 0;
            Round(detailCover, 5);
            main.Add(detailCover);

            var info = new VisualElement();
            info.style.marginLeft = 16;
            info.style.flexGrow = 1f;
            info.style.minWidth = 0;
            main.Add(info);

            detailTitle = Text(string.Empty, 18, Ink, FontStyle.Bold);
            detailTitle.style.whiteSpace = WhiteSpace.Normal;
            detailTitle.style.minHeight = 46;
            detailTitle.style.maxHeight = 68;
            info.Add(detailTitle);

            detailAuthor = Text(string.Empty, 12, SoftInk, FontStyle.Normal);
            detailAuthor.style.marginTop = 6;
            detailAuthor.style.minHeight = 18;
            info.Add(detailAuthor);

            detailPublisher = Text(string.Empty, 11, FaintInk, FontStyle.Normal);
            detailPublisher.style.marginTop = 4;
            detailPublisher.style.minHeight = 16;
            info.Add(detailPublisher);

            detailPrice = Text(string.Empty, 12, AccentDeep, FontStyle.Bold);
            detailPrice.style.marginTop = 10;
            info.Add(detailPrice);

            detailMetaRow = Row("detail-meta");
            detailMetaRow.style.marginTop = 12;
            detailMetaRow.style.minHeight = 22;
            detailMetaRow.style.alignItems = Align.Center;
            panel.Add(detailMetaRow);

            detailRating = Text(string.Empty, 12, AccentDeep, FontStyle.Bold);
            detailRating.style.marginRight = 14;
            detailMetaRow.Add(detailRating);

            detailReviews = Text(string.Empty, 12, Ink, FontStyle.Bold);
            detailReviews.style.marginRight = 14;
            detailMetaRow.Add(detailReviews);

            detailViews = Text(string.Empty, 12, Ink, FontStyle.Bold);
            detailMetaRow.Add(detailViews);

            var summaryLabel = Text("줄거리", 13, Ink, FontStyle.Bold);
            summaryLabel.style.marginTop = 14;
            panel.Add(summaryLabel);

            detailSummary = Text(string.Empty, 12, SoftInk, FontStyle.Normal);
            detailSummary.style.whiteSpace = WhiteSpace.Normal;
            detailSummary.style.minHeight = 64;
            detailSummary.style.marginTop = 6;
            panel.Add(detailSummary);

            detailShelf = Text(string.Empty, 10, FaintInk, FontStyle.Normal);
            detailShelf.style.marginTop = 8;
            panel.Add(detailShelf);

            addButton = TextButton("+ 내 책장에 추가", true);
            addButton.style.height = 38;
            addButton.style.marginTop = 12;
            addButton.style.alignSelf = Align.FlexStart;
            addButton.style.minWidth = 178;
            addButton.style.backgroundColor = Night;
            addButton.clicked += AddSelectedBook;
            panel.Add(addButton);
        }

        private void BuildFooter(VisualElement parent)
        {
            var footer = Row("footer");
            footer.style.height = 34;
            footer.style.flexShrink = 0;
            footer.style.alignItems = Align.Center;
            footer.style.justifyContent = Justify.Center;
            BorderTop(footer, Line);
            parent.Add(footer);

            var label = Text("DOTSHELF · BOOK MARKET · UNITY", 10, FaintInk, FontStyle.Normal);
            label.style.letterSpacing = 2;
            footer.Add(label);
        }

        private void BuildToast(VisualElement parent)
        {
            toast = Row("toast");
            toast.style.position = Position.Absolute;
            toast.style.bottom = 20;
            toast.style.alignSelf = Align.Center;
            toast.style.height = 40;
            toast.style.paddingLeft = 18;
            toast.style.paddingRight = 18;
            toast.style.alignItems = Align.Center;
            toast.style.backgroundColor = Ink;
            toast.style.display = DisplayStyle.None;
            Round(toast, 20);
            parent.Add(toast);

            toastLabel = Text(string.Empty, 13, Surface, FontStyle.Bold);
            toastLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            toast.Add(toastLabel);
        }

        private void BuildScrollRail(VisualElement parent)
        {
            scrollRail = new VisualElement { name = "notebook-scroll-rail" };
            scrollRail.pickingMode = PickingMode.Position;
            scrollRail.style.position = Position.Absolute;
            scrollRail.style.right = 4;
            scrollRail.style.top = 72;
            scrollRail.style.bottom = 20;
            scrollRail.style.width = 12;
            scrollRail.style.backgroundColor = Rgba(0x58, 0xB9, 0xFF, 0.18f);
            Round(scrollRail, 6);
            scrollRail.RegisterCallback<PointerDownEvent>(OnScrollRailPointerDown);
            scrollRail.RegisterCallback<PointerMoveEvent>(OnScrollRailPointerMove);
            scrollRail.RegisterCallback<PointerUpEvent>(OnScrollRailPointerUp);
            scrollRail.RegisterCallback<PointerCancelEvent>(OnScrollRailPointerCancel);
            parent.Add(scrollRail);

            scrollThumb = new VisualElement { name = "notebook-scroll-thumb" };
            scrollThumb.pickingMode = PickingMode.Position;
            scrollThumb.style.position = Position.Absolute;
            scrollThumb.style.left = 2;
            scrollThumb.style.right = 2;
            scrollThumb.style.top = 4;
            scrollThumb.style.height = 64;
            scrollThumb.style.backgroundColor = Rgba(0x58, 0xB9, 0xFF, 0.78f);
            Round(scrollThumb, 5);
            scrollRail.Add(scrollThumb);
        }

        private void ScrollNotebookBy(float delta)
        {
            var max = GetNotebookMaxScroll();
            var offset = pageScrollView.scrollOffset;
            var nextY = Mathf.Clamp(offset.y + delta, 0f, max);
            if (Mathf.Approximately(offset.y, nextY))
            {
                return;
            }

            offset.y = nextY;
            pageScrollView.scrollOffset = offset;
            UpdateScrollThumb();
        }

        private void SetNotebookScrollOffset(float y)
        {
            if (pageScrollView == null)
            {
                return;
            }

            var offset = pageScrollView.scrollOffset;
            offset.y = Mathf.Clamp(y, 0f, GetNotebookMaxScroll());
            pageScrollView.scrollOffset = offset;
            UpdateScrollThumb();
        }

        private void OnScrollRailPointerDown(PointerDownEvent evt)
        {
            if (pageScrollView == null || scrollRail == null || scrollThumb == null || evt.button != 0)
            {
                return;
            }

            draggingPageScrollbar = true;
            pageScrollbarPointerId = evt.pointerId;
            scrollRail.CapturePointer(evt.pointerId);

            if (evt.target != scrollThumb)
            {
                ScrollNotebookToPointer(evt.position.y);
            }

            pageScrollbarStartPointerY = evt.position.y;
            pageScrollbarStartOffsetY = pageScrollView.scrollOffset.y;
            scrollThumb.style.backgroundColor = AccentDeep;
            evt.StopPropagation();
        }

        private void OnScrollRailPointerMove(PointerMoveEvent evt)
        {
            if (!draggingPageScrollbar || pageScrollbarPointerId != evt.pointerId || scrollRail == null || !scrollRail.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            var max = GetNotebookMaxScroll();
            GetScrollThumbMetrics(out _, out var thumbTravel);
            if (max <= 0f || thumbTravel <= 0f)
            {
                evt.StopPropagation();
                return;
            }

            var pointerDelta = evt.position.y - pageScrollbarStartPointerY;
            SetNotebookScrollOffset(pageScrollbarStartOffsetY + pointerDelta / thumbTravel * max);
            evt.StopPropagation();
        }

        private void OnScrollRailPointerUp(PointerUpEvent evt)
        {
            if (pageScrollbarPointerId != evt.pointerId)
            {
                return;
            }

            EndScrollRailDrag(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnScrollRailPointerCancel(PointerCancelEvent evt)
        {
            if (pageScrollbarPointerId != evt.pointerId)
            {
                return;
            }

            EndScrollRailDrag(evt.pointerId);
            evt.StopPropagation();
        }

        private void EndScrollRailDrag(int pointerId)
        {
            if (scrollRail != null && scrollRail.HasPointerCapture(pointerId))
            {
                scrollRail.ReleasePointer(pointerId);
            }

            draggingPageScrollbar = false;
            pageScrollbarPointerId = -1;
            if (scrollThumb != null)
            {
                scrollThumb.style.backgroundColor = Rgba(0x58, 0xB9, 0xFF, 0.78f);
            }
        }

        private void ScrollNotebookToPointer(float panelY)
        {
            if (scrollRail == null)
            {
                return;
            }

            var max = GetNotebookMaxScroll();
            GetScrollThumbMetrics(out var thumbHeight, out var thumbTravel);
            if (max <= 0f || thumbTravel <= 0f)
            {
                return;
            }

            var localY = panelY - scrollRail.worldBound.y;
            var targetThumbTop = Mathf.Clamp(localY - thumbHeight * 0.5f - 4f, 0f, thumbTravel);
            SetNotebookScrollOffset(targetThumbTop / thumbTravel * max);
        }

        private void OnNotebookPageWheel(WheelEvent evt)
        {
            if (recommendScrollView == null ||
                recommendScrollView.resolvedStyle.display == DisplayStyle.None ||
                !recommendScrollView.worldBound.Contains(evt.mousePosition))
            {
                return;
            }

            var dominantDelta = Mathf.Abs(evt.delta.x) > Mathf.Abs(evt.delta.y)
                ? evt.delta.x
                : evt.delta.y;
            if (!Mathf.Approximately(dominantDelta, 0f))
            {
                ScrollRecommendBy(dominantDelta * RecommendWheelScrollScale);
            }

            evt.StopPropagation();
            evt.StopImmediatePropagation();
        }

        private void ScrollRecommendBy(float delta)
        {
            if (recommendScrollView == null)
            {
                return;
            }

            var max = GetRecommendMaxScroll();
            if (max <= 0f)
            {
                return;
            }

            var offset = recommendScrollView.scrollOffset;
            offset.x = Mathf.Clamp(offset.x + delta, 0f, max);
            recommendScrollView.scrollOffset = offset;
        }

        private float GetRecommendMaxScroll()
        {
            if (recommendScrollView == null)
            {
                return 0f;
            }

            var contentWidth = Mathf.Max(
                recommendScrollView.contentContainer.resolvedStyle.width,
                recommendScrollView.contentContainer.layout.width);
            foreach (var child in recommendScrollView.contentContainer.Children())
            {
                contentWidth = Mathf.Max(contentWidth, child.layout.xMax + child.resolvedStyle.marginRight);
            }

            var viewWidth = Mathf.Max(
                recommendScrollView.resolvedStyle.width,
                recommendScrollView.layout.width,
                1f);
            return Mathf.Max(0f, contentWidth - viewWidth);
        }

        private float GetNotebookMaxScroll()
        {
            if (pageScrollView == null)
            {
                return 0f;
            }

            var contentHeight = Mathf.Max(
                pageScrollView.contentContainer.resolvedStyle.height,
                pageScrollView.contentContainer.layout.height,
                1040f);
            var viewHeight = Mathf.Max(
                pageScrollView.resolvedStyle.height,
                pageScrollView.layout.height,
                1f);
            return Mathf.Max(0f, contentHeight - viewHeight);
        }

        private void UpdateScrollThumb()
        {
            if (pageScrollView == null || scrollRail == null || scrollThumb == null)
            {
                return;
            }

            var max = GetNotebookMaxScroll();
            GetScrollThumbMetrics(out var thumbHeight, out var thumbTravel);
            var ratio = max <= 0f ? 0f : Mathf.Clamp01(pageScrollView.scrollOffset.y / max);

            scrollThumb.style.height = thumbHeight;
            scrollThumb.style.top = 4f + (thumbTravel * ratio);
        }

        private void GetScrollThumbMetrics(out float thumbHeight, out float thumbTravel)
        {
            if (pageScrollView == null || scrollRail == null)
            {
                thumbHeight = 64f;
                thumbTravel = 0f;
                return;
            }

            var railHeight = Mathf.Max(scrollRail.resolvedStyle.height, scrollRail.layout.height, 1f);
            var contentHeight = Mathf.Max(
                pageScrollView.contentContainer.resolvedStyle.height,
                pageScrollView.contentContainer.layout.height,
                1040f);
            var viewHeight = Mathf.Max(
                pageScrollView.resolvedStyle.height,
                pageScrollView.layout.height,
                1f);
            thumbHeight = Mathf.Clamp(railHeight * viewHeight / contentHeight, 44f, 112f);
            thumbTravel = Mathf.Max(0f, railHeight - thumbHeight - 8f);
        }

        private void RefreshAll()
        {
            RefreshWallet();
            RefreshFilters();
            RefreshCommunityPanels();
            RefreshModeVisibility();
            RefreshLists();
            RefreshObjectMarket();
            RefreshBookDetailPage(GetSelectedBook());
        }

        private void RefreshWallet()
        {
            if (pageBalanceLabel != null)
            {
                pageBalanceLabel.text = $"{pages:N0} P";
            }
        }

        private void RefreshFilters()
        {
            foreach (var pair in headerTabButtons)
            {
                SetButtonSelected(pair.Value, pair.Key == activeHeaderTab);
            }

            foreach (var pair in searchSuggestionButtons)
            {
                SetSearchSuggestionSelected(pair.Value, pair.Key == activeSearchSuggestion);
            }

            if (clearSearchButton != null)
            {
                clearSearchButton.style.display = string.IsNullOrWhiteSpace(query) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            foreach (var pair in categoryButtons)
            {
                var selected = pair.Key == activeCategory;
                pair.Value.style.backgroundColor = selected ? Rgba(0x58, 0xB9, 0xFF, 0.24f) : Color.clear;
                pair.Value.style.color = selected ? Surface : Rgba(0xF7, 0xFF, 0xF7, 0.82f);
                Border(pair.Value, selected ? Rgba(0x58, 0xB9, 0xFF, 0.48f) : Rgba(0xF7, 0xFF, 0xF7, 0.12f));
            }

            foreach (var pair in subTabButtons)
            {
                SetButtonSelected(pair.Value, pair.Key == activeSubTab);
            }

            foreach (var pair in genreButtons)
            {
                SetButtonSelected(pair.Value, pair.Key == activeGenre);
            }

            foreach (var pair in rankingSortButtons)
            {
                SetButtonSelected(pair.Value, pair.Key == activeRankingSort);
            }

            foreach (var pair in objectSortButtons)
            {
                SetButtonSelected(pair.Value, pair.Key == activeObjectSort);
            }

            foreach (var pair in objectCategoryButtons)
            {
                SetButtonSelected(pair.Value, pair.Key == activeObjectCategory);
            }
        }

        private void RefreshCommunityPanels()
        {
            if (otherSpacesContent != null)
            {
                otherSpacesContent.Clear();
                BuildOtherSpaces(otherSpacesContent);
            }
        }

        private void RefreshModeVisibility()
        {
            var bookMarket = activeHeaderTab == HeaderTabs[0];
            var otherSpaces = activeHeaderTab == HeaderTabs[1];
            var objectMarket = activeHeaderTab == HeaderTabs[2];
            var searchPage = bookMarket && searchPageOpen;

            if (headerSubtitleLabel != null)
            {
                headerSubtitleLabel.text = activeHeaderTab switch
                {
                    "다른 공간" => "LAPTOP · OTHER SPACES",
                    "꾸미기 시장" => "LAPTOP · OBJECT MARKET",
                    _ => "LAPTOP · BOOK MARKET",
                };
            }

            if (heroPanel != null)
            {
                heroPanel.style.height = bookMarket ? searchPage ? 178 : 198 : 132;
            }

            if (heroEyebrowLabel != null)
            {
                heroEyebrowLabel.text = searchPage
                    ? "DOTSHELF SEARCH RESULT"
                    : activeHeaderTab switch
                {
                    "다른 공간" => "DOTSHELF COMMUNITY SPACES",
                    "꾸미기 시장" => "DOTSHELF OBJECT MARKET",
                    _ => "DOTSHELF CURATED MARKET",
                };
            }

            if (heroTitleLabel != null)
            {
                heroTitleLabel.text = searchPage ? "검색 결과" : activeHeaderTab;
            }

            if (heroCopyLabel != null)
            {
                heroCopyLabel.text = searchPage
                    ? $"'{appliedSearchQuery}' 검색 API 결과를 확인하세요."
                    : activeHeaderTab switch
                {
                    "다른 공간" => "다른 사용자의 서재를 방문하고 팔로우해보세요.",
                    "꾸미기 시장" => "내 공간에 배치할 오브젝트와 테마를 둘러보세요.",
                    _ => "오늘 당신의 한 권을 찾아보세요.",
                };
            }

            if (searchContainer != null)
            {
                searchContainer.style.display = bookMarket ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (suggestionsContainer != null)
            {
                suggestionsContainer.style.display = bookMarket ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (subTabsRow != null)
            {
                subTabsRow.style.display = bookMarket && !searchPage ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (bookMarketContent != null)
            {
                bookMarketContent.style.display = bookMarket && !searchPage ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (searchResultsContent != null)
            {
                searchResultsContent.style.display = searchPage ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (otherSpacesContent != null)
            {
                otherSpacesContent.style.display = otherSpaces ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (objectMarketContent != null)
            {
                objectMarketContent.style.display = objectMarket ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (pageScrollView != null)
            {
                pageScrollView.contentContainer.style.minHeight = searchPage ? 1120 : bookMarket ? 1320 : 980;
            }
        }

        private void RefreshLists()
        {
            var recommendFiltered = FilterBooks(false).ToArray();
            var rankingFiltered = FilterBooks(true).ToArray();

            RefreshRecommend(recommendFiltered);
            RefreshRanking(rankingFiltered);
            RefreshSearchResults(searchResults);
        }

        private void RefreshBookDetailPage(BookViewModel book)
        {
            if (book == null)
            {
                if (bookDetailTitle != null)
                {
                    bookDetailTitle.text = "책을 찾을 수 없습니다";
                }

                if (bookDetailReviews != null)
                {
                    bookDetailReviews.Clear();
                }

                return;
            }

            if (bookDetailCover != null)
            {
                bookDetailCover.Clear();
                bookDetailCover.Add(CreateBookCover(book, 136, 198, true));
                SlideFadeInElement(bookDetailCover, 8, 0, 0.16f);
            }

            if (bookDetailMeta != null)
            {
                bookDetailMeta.text = "DOTSHELF · BOOK DETAIL";
            }

            if (bookDetailTitle != null)
            {
                bookDetailTitle.text = book.Title;
            }

            if (bookDetailAuthor != null)
            {
                bookDetailAuthor.text = $"{book.Author} · {book.Publisher}";
            }

            if (bookDetailPublisher != null)
            {
                bookDetailPublisher.text = $"{book.Genre} · {book.Status} · {book.Shelf}";
            }

            if (bookDetailSummary != null)
            {
                bookDetailSummary.text = $"\"{book.Summary}\"";
            }

            if (bookDetailPrice != null)
            {
                bookDetailPrice.text = book.Price == 0 ? "FREE" : $"{book.Price:N0} P";
            }

            if (bookDetailReviewCount != null)
            {
                bookDetailReviewCount.text = $"★ {book.Rating:0.0} · 리뷰 {GetBookReviews(book).Count:N0}개 · 조회 {book.Views:N0}";
            }

            RefreshFavoriteBookButton(book);
            RefreshBookDetailAddButton(book);
            RefreshBookReviews(book);
            RefreshBookReviewToggle();
            RefreshRatingStars();
        }

        private void RefreshFavoriteBookButton(BookViewModel book)
        {
            if (bookFavoriteButton == null || book == null)
            {
                return;
            }

            var selected = favoriteBookIds.Contains(book.Id);
            bookFavoriteButton.text = selected ? "즐겨찾기 해제" : "즐겨찾기";
            bookFavoriteButton.style.backgroundColor = selected ? Gold : Rgba(0x0E, 0x1D, 0x2A, 0.06f);
            bookFavoriteButton.style.color = selected ? Ink : SoftInk;
            Border(bookFavoriteButton, selected ? Rgba(0xF4, 0xC9, 0x5D, 0.72f) : Line);
        }

        private void RefreshBookDetailAddButton(BookViewModel book)
        {
            if (bookDetailAddButton == null || book == null)
            {
                return;
            }

            var added = addedBookIds.Contains(book.Id);
            var pending = pendingShelfBookIds.Contains(book.Id);
            bookDetailAddButton.text = pending ? "추가 중" : added ? "책장에 있음" : book.Price == 0 ? "내 책장에 추가" : "구매하고 추가";
            bookDetailAddButton.style.backgroundColor = added ? Success : pending ? SoftInk : AccentDeep;
            bookDetailAddButton.style.color = Color.white;
            bookDetailAddButton.SetEnabled(!added && !pending);
        }

        private List<BookReviewViewModel> GetBookReviews(BookViewModel book)
        {
            if (book == null)
            {
                return new List<BookReviewViewModel>();
            }

            if (!bookReviewMap.TryGetValue(book.Id, out var reviews))
            {
                reviews = new List<BookReviewViewModel>
                {
                    new("서재 이웃", Mathf.Clamp(book.Rating, 0f, 5f), $"{book.Title}의 분위기와 문장이 오래 남았어요."),
                    new("Dotshelf 독자", Mathf.Clamp(book.Rating - 0.2f, 0f, 5f), book.Summary),
                    new("책장 큐레이터", Mathf.Clamp(book.Rating - 0.4f, 0f, 5f), "책장에 배치했을 때 표지 색과 분위기가 잘 살아납니다."),
                    new("심야 독자", Mathf.Clamp(book.Rating - 0.6f, 0f, 5f), "짧게 읽기에도 좋고, 다시 펼쳐볼 문장이 많았습니다."),
                };
                bookReviewMap[book.Id] = reviews;
            }

            return reviews;
        }

        private void RefreshBookReviews(BookViewModel book)
        {
            if (bookDetailReviews == null)
            {
                return;
            }

            bookDetailReviews.Clear();
            var reviews = GetPopularBookReviews(book).ToArray();
            var source = showingAllBookReviews ? reviews : reviews.Take(3);
            foreach (var review in source)
            {
                bookDetailReviews.Add(CreateBookReviewCard(review));
            }

            if (bookReviewMoreButton != null)
            {
                bookReviewMoreButton.style.display = reviews.Length > 3 ? DisplayStyle.Flex : DisplayStyle.None;
                bookReviewMoreButton.text = showingAllBookReviews ? "인기만" : $"전체 {reviews.Length}";
            }
        }

        private IEnumerable<BookReviewViewModel> GetPopularBookReviews(BookViewModel book)
        {
            return GetBookReviews(book)
                .OrderByDescending(review => review.Rating)
                .ThenBy(review => review.Author);
        }

        private VisualElement CreateBookReviewCard(BookReviewViewModel review)
        {
            var card = new VisualElement { name = "book-review-card" };
            card.style.marginBottom = 10;
            card.style.paddingLeft = 0;
            card.style.paddingRight = 0;
            card.style.paddingTop = 0;
            card.style.paddingBottom = 10;
            card.style.backgroundColor = Color.clear;
            card.style.borderBottomWidth = 1;
            card.style.borderBottomColor = Rgba(0x0E, 0x1D, 0x2A, 0.08f);

            var row = Row("book-review-meta");
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;
            card.Add(row);

            var author = Text(review.Author, 12, Ink, FontStyle.Bold);
            author.style.marginRight = 8;
            row.Add(author);

            var rating = Text($"★ {review.Rating:0.0}", 11, AccentDeep, FontStyle.Bold);
            row.Add(rating);

            var content = Text(review.Content, 12, SoftInk, FontStyle.Normal);
            content.style.whiteSpace = WhiteSpace.Normal;
            content.style.minHeight = 24;
            card.Add(content);
            return card;
        }

        private void CloseBookDetail()
        {
            if (bookDetailContent != null)
            {
                bookDetailContent.style.display = DisplayStyle.None;
            }
            SetBookReviewsOpen(false, false);
            showingAllBookReviews = false;
        }

        private void OpenBookDetailModal(BookViewModel book)
        {
            showingAllBookReviews = false;
            selectedReviewRating = 5f;
            SetBookReviewsOpen(false, false);
            RefreshBookDetailPage(book);
            if (bookDetailContent != null)
            {
                bookDetailContent.style.display = DisplayStyle.Flex;
                FadeInElement(bookDetailContent, 0.14f);
            }

            if (bookDetailModalPanel != null)
            {
                bookDetailModalPanel.style.height = 650;
                bookDetailModalPanel.style.marginRight = 0;
                SlideFadeInElement(bookDetailModalPanel, 12, 0, 0.18f);
            }
        }

        private void ToggleReviewMode()
        {
            showingAllBookReviews = !showingAllBookReviews;
            RefreshBookReviews(GetSelectedBook());
            if (bookReviewPanel != null && bookReviewsOpen)
            {
                SlideFadeInFromRight(bookReviewPanel, -10, 22, 0.14f);
            }
        }

        private void ToggleBookReviews()
        {
            SetBookReviewsOpen(!bookReviewsOpen, true);
        }

        private void SetBookReviewsOpen(bool open, bool animate)
        {
            bookReviewsOpen = open;
            RefreshBookReviewToggle();

            if (bookReviewPanel != null)
            {
                if (open)
                {
                    bookReviewPanel.style.display = DisplayStyle.Flex;
                    if (animate)
                    {
                        SlideFadeInFromRight(bookReviewPanel, -30, 22, 0.22f);
                    }
                    else
                    {
                        bookReviewPanel.style.right = 22;
                        bookReviewPanel.style.opacity = 1f;
                    }
                }
                else
                {
                    bookReviewPanel.style.display = DisplayStyle.None;
                }
            }

            if (bookDetailModalPanel != null)
            {
                bookDetailModalPanel.style.height = 650;
                bookDetailModalPanel.style.marginRight = open ? 392 : 0;
            }
        }

        private void RefreshBookReviewToggle()
        {
            if (bookReviewToggleButton == null)
            {
                return;
            }

            bookReviewToggleButton.text = bookReviewsOpen ? "리뷰 닫기" : "리뷰 보기";
            bookReviewToggleButton.style.backgroundColor = bookReviewsOpen ? AccentDeep : Rgba(0x58, 0xB9, 0xFF, 0.12f);
            bookReviewToggleButton.style.color = bookReviewsOpen ? Color.white : AccentDeep;
            Border(bookReviewToggleButton, bookReviewsOpen ? AccentDeep : Rgba(0x58, 0xB9, 0xFF, 0.34f));
        }

        private void SetSelectedReviewRating(float rating)
        {
            selectedReviewRating = Mathf.Clamp(rating, 1f, 5f);
            RefreshRatingStars();
        }

        private void RefreshRatingStars()
        {
            if (bookRatingStarButtons.Count == 0)
            {
                return;
            }

            for (var i = 0; i < bookRatingStarButtons.Count; i++)
            {
                var active = i < Mathf.RoundToInt(selectedReviewRating);
                var star = bookRatingStarButtons[i];
                star.text = active ? "★" : "☆";
                star.style.color = active ? AccentDeep : FaintInk;
                star.style.backgroundColor = active ? Rgba(0x58, 0xB9, 0xFF, 0.16f) : Rgba(0x0E, 0x1D, 0x2A, 0.05f);
                Border(star, active ? Rgba(0x58, 0xB9, 0xFF, 0.34f) : Rgba(0x0E, 0x1D, 0x2A, 0.08f));
            }
        }

        private void ToggleFavoriteBook()
        {
            var book = GetSelectedBook();
            if (book == null)
            {
                return;
            }

            ToggleSet(favoriteBookIds, book.Id);
            RefreshFavoriteBookButton(book);
            ShowToast(favoriteBookIds.Contains(book.Id) ? $"즐겨찾기에 추가 · {book.Title}" : $"즐겨찾기 해제 · {book.Title}");
        }

        private void SubmitBookReview()
        {
            var book = GetSelectedBook();
            if (book == null)
            {
                return;
            }

            var content = bookReviewInput?.value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(content))
            {
                ShowToast("리뷰 내용을 입력해주세요.");
                return;
            }

            var rating = selectedReviewRating;
            GetBookReviews(book).Insert(0, new BookReviewViewModel("나", rating, content));
            if (bookReviewInput != null)
            {
                bookReviewInput.value = string.Empty;
            }

            showingAllBookReviews = true;
            RefreshBookDetailPage(book);
            SetBookReviewsOpen(true, true);
            ShowToast("리뷰가 등록되었습니다.");
        }

        private IEnumerable<BookViewModel> FilterBooks(bool includeGenre)
        {
            var filtered = books
                .Where(MatchesSubTab)
                .Where(book => !includeGenre || MatchesGenre(book));

            return SortBooks(filtered);
        }

        private BookViewModel[] GetLocalSearchResults(string keyword)
        {
            var needle = keyword?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(needle))
            {
                return Array.Empty<BookViewModel>();
            }

            return SortBooks(books.Where(book => MatchesSearch(book, needle))).ToArray();
        }

        private NotebookApiClient GetNotebookApiClient()
        {
            return notebookApiClient ??= new NotebookApiClient(APIManager.Instance);
        }

        private void RequestSearchResults(string keyword)
        {
            searchRequestVersion++;
            var requestVersion = searchRequestVersion;
            appliedSearchQuery = keyword;
            searchResults = Array.Empty<BookViewModel>();
            searchLoading = true;
            RefreshAll();

            try
            {
                GetNotebookApiClient().SearchBooks(
                    keyword,
                    1,
                    SearchResultPageSize,
                    response =>
                    {
                        if (requestVersion != searchRequestVersion)
                        {
                            return;
                        }

                        searchLoading = false;
                        searchResults = MapApiSearchResults(response, keyword);
                        RefreshAll();
                    },
                    error =>
                    {
                        UnityEngine.Debug.LogWarning($"Search API failed, falling back to local. keyword={keyword}, code={error?.Code}, message={FirstText(error?.Message, error?.Error)}");
                        ShowToast("검색 API 응답을 받지 못해 로컬 결과를 표시합니다.");
                        CompleteSearchWithLocalFallback(requestVersion, keyword);
                    });
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"Search API failed, falling back to local. keyword={keyword}");
                UnityEngine.Debug.LogException(exception);
                ShowToast("검색 API 응답을 받지 못해 로컬 결과를 표시합니다.");
                CompleteSearchWithLocalFallback(requestVersion, keyword);
            }
        }

        private void CompleteSearchWithLocalFallback(int requestVersion, string keyword)
        {
            if (requestVersion != searchRequestVersion)
            {
                return;
            }

            searchLoading = false;
            searchResults = GetLocalSearchResults(keyword);
            RefreshAll();
        }

        private BookViewModel[] MapApiSearchResults(BookListResponseDto response, string keyword)
        {
            var items = response?.EffectiveItems;
            if (items == null || items.Count == 0)
            {
                return Array.Empty<BookViewModel>();
            }

            var mapped = new List<BookViewModel>(items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                var dto = items[i];
                if (dto == null)
                {
                    continue;
                }

                var title = FirstText(dto.Title, "제목 없음");
                var author = FirstText(dto.Author, "작가 미상");
                var publisher = FirstText(dto.Publisher, "출판사 미상");
                var summary = FirstText(dto.Description, "검색 API에서 전달된 책입니다.");
                var genre = ResolveSearchGenre(title, summary);
                var source = FirstText(dto.Source, dto.Id > 0 ? "LOCAL" : "KAKAO").ToUpperInvariant();
                var serverBookId = ResolveServerBookId(dto);
                var isbn13 = FirstText(dto.Isbn13, dto.Isbn);
                var isbn10 = dto.Isbn10;
                var sourceKey = source == "LOCAL" && serverBookId.HasValue
                    ? serverBookId.Value.ToString()
                    : FirstText(isbn13, isbn10, dto.SourceUrl, dto.BookId, $"api-{i + 1}");
                var id = $"{source.ToLowerInvariant()}-{sourceKey}";
                var rating = dto.AvgRating > 0 ? Mathf.Clamp((float)dto.AvgRating, 1f, 5f) : 4.2f;
                var reviews = Mathf.Max(dto.LikeCount, dto.PurchaseCount, dto.RecordCount, 1);
                var views = Mathf.Max(dto.PlacementCount + dto.RecordCount + dto.LikeCount, reviews * 12);
                var records = Mathf.Max(dto.RecordCount, dto.PlacementCount);
                var price = dto.HasPaidCover ? 1200 : 0;
                var coverColor = SearchCoverColor(i, id);

                mapped.Add(new BookViewModel(
                    id,
                    title,
                    author,
                    publisher,
                    summary,
                    genre,
                    "안 읽음",
                    "검색 결과",
                    price,
                    rating,
                    reviews,
                    views,
                    records,
                    coverColor,
                    Accent,
                    source,
                    serverBookId,
                    isbn13,
                    isbn10,
                    dto.SourceUrl,
                    dto.CoverImageUrl,
                    dto.PublishedDate));
            }

            return SortBooks(mapped.Where(book => MatchesSearch(book, keyword))).ToArray();
        }

        private IEnumerable<BookViewModel> SortBooks(IEnumerable<BookViewModel> source)
        {
            return activeRankingSort switch
            {
                "별점순" => source
                    .OrderByDescending(book => book.Rating)
                    .ThenByDescending(book => book.Reviews)
                    .ThenBy(book => book.Title),
                "리뷰순" => source
                    .OrderByDescending(book => book.Reviews)
                    .ThenByDescending(book => book.Rating)
                    .ThenBy(book => book.Title),
                _ => source
                    .OrderByDescending(book => book.Rating)
                    .ThenByDescending(book => book.Reviews)
                    .ThenBy(book => book.Title),
            };
        }

        private BookViewModel ResolveDetailBook(
            IReadOnlyList<BookViewModel> rankingFiltered,
            IReadOnlyList<BookViewModel> recommendFiltered)
        {
            var detailSource = rankingFiltered.Count > 0 ? rankingFiltered : recommendFiltered;
            if (detailSource.Count == 0)
            {
                return null;
            }

            var selectedBook = GetSelectedBook();
            if (selectedBook != null && detailSource.Any(book => book.Id == selectedBook.Id))
            {
                return selectedBook;
            }

            selectedBook = detailSource[0];
            selectedBookIndex = Mathf.Max(0, Array.IndexOf(books, selectedBook));
            return selectedBook;
        }

        private BookViewModel GetSelectedBook()
        {
            if (selectedBookOverride != null)
            {
                return selectedBookOverride;
            }

            return selectedBookIndex >= 0 && selectedBookIndex < books.Length
                ? books[selectedBookIndex]
                : null;
        }

        private bool MatchesSearch(BookViewModel book, string needle)
        {
            return string.IsNullOrWhiteSpace(needle) ||
                   Contains(book.Title, needle) ||
                   Contains(book.Author, needle) ||
                   Contains(book.Publisher, needle) ||
                   Contains(book.Genre, needle) ||
                   Contains(book.Summary, needle);
        }

        private bool MatchesCategory(BookViewModel book)
        {
            return activeCategory switch
            {
                "읽는 중" or "읽음" or "안 읽음" => book.Status == activeCategory,
                "책장 1" or "책장 2" => book.Shelf == activeCategory,
                _ => true,
            };
        }

        private bool MatchesSubTab(BookViewModel book)
        {
            return activeSubTab switch
            {
                "무료" => book.Price == 0,
                "유료" => book.Price > 0,
                _ => true,
            };
        }

        private bool MatchesGenre(BookViewModel book)
        {
            return activeGenre == "전체" || book.Genre == activeGenre;
        }

        private void RefreshSearchResults(BookViewModel[] results)
        {
            if (searchResultsList == null)
            {
                return;
            }

            searchResultsList.Clear();
            var normalizedQuery = (appliedSearchQuery ?? string.Empty).Trim();

            if (searchResultsTitleLabel != null)
            {
                searchResultsTitleLabel.text = string.IsNullOrWhiteSpace(normalizedQuery)
                    ? "검색 결과"
                    : $"'{normalizedQuery}' 검색 결과";
            }

            if (searchResultsHintLabel != null)
            {
                searchResultsHintLabel.text = searchLoading
                    ? "검색 API를 호출하고 있습니다."
                    : results.Length == 0
                    ? "검색어를 바꾸거나 추천 검색어를 선택해보세요."
                    : "검색 API 응답에 포함된 책만 그리드로 보여줍니다.";
            }

            if (searchResultsCountLabel != null)
            {
                searchResultsCountLabel.text = searchLoading ? "검색 중" : $"{results.Length:N0}권";
            }

            if (searchLoading)
            {
                var loading = EmptyState("검색 API 호출 중입니다.");
                loading.style.width = Length.Percent(100);
                searchResultsList.Add(loading);
                return;
            }

            if (results.Length == 0)
            {
                var empty = EmptyState("검색 결과가 없습니다.");
                empty.style.width = Length.Percent(100);
                searchResultsList.Add(empty);
                return;
            }

            for (var i = 0; i < results.Length; i++)
            {
                searchResultsList.Add(CreateSearchResultCard(results[i], i));
            }
        }

        private VisualElement CreateSearchResultCard(BookViewModel book, int index)
        {
            var card = new VisualElement { name = $"search-result-{book.Id}" };
            card.style.width = 218;
            card.style.height = 266;
            card.style.marginRight = 10;
            card.style.marginBottom = 10;
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.paddingTop = 10;
            card.style.paddingBottom = 10;
            card.style.alignItems = Align.Center;
            card.style.flexDirection = FlexDirection.Column;
            card.style.backgroundColor = index % 2 == 0 ? Rgba(0xFF, 0xFF, 0xFF, 0.94f) : Rgba(0xF4, 0xFB, 0xFF, 0.92f);
            Round(card, 14);
            Border(card, index == 0 ? Rgba(0x58, 0xB9, 0xFF, 0.36f) : Rgba(0x0E, 0x1D, 0x2A, 0.08f));
            UsePointerCursor(card);
            card.RegisterCallback<ClickEvent>(_ => SelectBook(book));

            var coverStage = new VisualElement { name = "search-result-cover-stage" };
            coverStage.style.width = Length.Percent(100);
            coverStage.style.height = 122;
            coverStage.style.alignItems = Align.Center;
            coverStage.style.justifyContent = Justify.Center;
            coverStage.style.backgroundColor = Rgba(0xEA, 0xF5, 0xFF, 0.74f);
            Round(coverStage, 12);
            card.Add(coverStage);

            coverStage.Add(CreateBookCover(book, 76, 112, true));

            var info = new VisualElement();
            info.style.marginTop = 8;
            info.style.flexGrow = 1f;
            info.style.minWidth = 0;
            info.style.width = Length.Percent(100);
            card.Add(info);

            var top = Row("search-result-top");
            top.style.alignItems = Align.Center;
            top.style.justifyContent = Justify.Center;
            top.style.minHeight = 22;
            info.Add(top);

            var price = Text(book.Price == 0 ? "FREE" : $"{book.Price:N0}P", 9, book.Price == 0 ? Success : AccentDeep, FontStyle.Bold);
            price.style.height = 18;
            price.style.paddingLeft = 6;
            price.style.paddingRight = 6;
            price.style.unityTextAlign = TextAnchor.MiddleCenter;
            price.style.backgroundColor = book.Price == 0 ? Rgba(0x3F, 0x9B, 0x68, 0.14f) : Rgba(0x58, 0xB9, 0xFF, 0.18f);
            Round(price, 4);
            top.Add(price);

            var genre = Text(book.Genre, 10, FaintInk, FontStyle.Normal);
            genre.style.marginLeft = 8;
            top.Add(genre);

            var title = Text(book.Title, 13, Ink, FontStyle.Bold);
            title.style.marginTop = 4;
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.minHeight = 32;
            title.style.maxHeight = 34;
            title.style.unityTextAlign = TextAnchor.UpperCenter;
            info.Add(title);

            var meta = Text($"{book.Author} · {book.Publisher}", 10, SoftInk, FontStyle.Normal);
            meta.style.marginTop = 2;
            meta.style.whiteSpace = WhiteSpace.Normal;
            meta.style.minHeight = 28;
            meta.style.maxHeight = 30;
            meta.style.unityTextAlign = TextAnchor.UpperCenter;
            info.Add(meta);

            var stats = Text($"★ {book.Rating:0.0} · 리뷰 {book.Reviews:N0} · 조회 {book.Views:N0}", 9, Ink, FontStyle.Normal);
            stats.style.marginTop = 4;
            stats.style.minHeight = 16;
            stats.style.unityTextAlign = TextAnchor.MiddleCenter;
            stats.style.backgroundColor = Rgba(0x0E, 0x1D, 0x2A, 0.04f);
            Round(stats, 999);
            info.Add(stats);

            return card;
        }

        private void RefreshRecommend(IReadOnlyCollection<BookViewModel> filtered)
        {
            if (recommendList == null)
            {
                return;
            }

            recommendList.Clear();
            var ordered = filtered
                .OrderByDescending(book => book.Rating)
                .ThenByDescending(book => book.Reviews)
                .ThenBy(book => book.Title);

            var source = ordered.Where(book => book.Rating >= 4.6f).Take(8);

            foreach (var book in source)
            {
                recommendList.Add(CreateRecommendCard(book));
            }

            if (recommendList.childCount == 0)
            {
                recommendList.Add(EmptyState("조건에 맞는 추천 책이 없습니다."));
            }
        }

        private void RefreshRanking(BookViewModel[] filtered)
        {
            if (rankingGrid == null)
            {
                return;
            }

            rankingGrid.Clear();
            var totalPages = Mathf.Max(1, Mathf.CeilToInt(filtered.Length / (float)RankingPageSize));
            pageIndex = Mathf.Clamp(pageIndex, 0, totalPages - 1);
            var page = filtered.Skip(pageIndex * RankingPageSize).Take(RankingPageSize).ToArray();

            for (var i = 0; i < page.Length; i++)
            {
                rankingGrid.Add(CreateRankingCard(page[i], pageIndex * RankingPageSize + i + 1));
            }

            if (page.Length == 0)
            {
                rankingGrid.Add(EmptyState("검색 결과가 없습니다."));
            }

            if (resultLabel != null)
            {
                resultLabel.text = $"{filtered.Length:N0}권";
            }

            for (var i = 0; i < pageButtons.Count; i++)
            {
                var button = pageButtons[i];
                button.style.display = i < totalPages ? DisplayStyle.Flex : DisplayStyle.None;
                button.text = (i + 1).ToString();
                SetButtonSelected(button, i == pageIndex);
            }

            if (previousPageButton != null)
            {
                previousPageButton.SetEnabled(pageIndex > 0);
            }

            if (nextPageButton != null)
            {
                nextPageButton.SetEnabled(pageIndex < totalPages - 1);
            }
        }

        private VisualElement CreateRecommendCard(BookViewModel book)
        {
            var card = new VisualElement { name = $"recommend-{book.Id}" };
            card.style.width = 178;
            card.style.height = 350;
            card.style.flexShrink = 0;
            card.style.marginRight = 18;
            card.style.paddingLeft = 8;
            card.style.paddingRight = 8;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.alignItems = Align.FlexStart;
            card.style.backgroundColor = Rgba(0xFF, 0xFF, 0xFF, 0.72f);
            Round(card, 14);
            Border(card, Rgba(0x0E, 0x1D, 0x2A, 0.08f));
            UsePointerCursor(card);
            card.RegisterCallback<ClickEvent>(_ => SelectBook(book));

            var cover = CreateBookCover(book, 138, 200, true);
            cover.style.alignSelf = Align.Center;

            var priceBadge = Text(book.Price == 0 ? "FREE" : $"{book.Price:N0}P", 12, Surface, FontStyle.Bold);
            priceBadge.style.position = Position.Absolute;
            priceBadge.style.left = 8;
            priceBadge.style.top = 10;
            priceBadge.style.height = 28;
            priceBadge.style.paddingLeft = 10;
            priceBadge.style.paddingRight = 10;
            priceBadge.style.unityTextAlign = TextAnchor.MiddleCenter;
            priceBadge.style.backgroundColor = book.Price == 0 ? Success : AccentDeep;
            Round(priceBadge, 6);
            cover.Add(priceBadge);
            card.Add(cover);

            var title = Text(book.Title, 15, Ink, FontStyle.Bold);
            title.style.marginTop = 12;
            title.style.width = Length.Percent(100);
            title.style.minHeight = 36;
            title.style.maxHeight = 42;
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.unityTextAlign = TextAnchor.UpperCenter;
            card.Add(title);

            var meta = Text($"{book.Author} · {(book.Price == 0 ? "FREE" : $"{book.Price:N0}P")}", 9, SoftInk, FontStyle.Normal);
            meta.style.width = Length.Percent(100);
            meta.text = $"{book.Author} · {book.Publisher}\n★ {book.Rating:0.0}";
            meta.style.fontSize = 12;
            meta.style.minHeight = 42;
            meta.style.marginTop = 0;
            meta.style.unityTextAlign = TextAnchor.UpperCenter;
            meta.style.whiteSpace = WhiteSpace.Normal;
            card.Add(meta);

            var added = addedBookIds.Contains(book.Id);
            var pending = pendingShelfBookIds.Contains(book.Id);
            var action = TextButton(pending
                ? "추가 중"
                : added ? "책장에 있음" : book.Price == 0 ? "담기" : "구매", true);
            action.style.height = 32;
            action.style.width = Length.Percent(100);
            action.style.marginTop = 6;
            action.style.backgroundColor = added
                ? Success
                : pending ? SoftInk : book.Price == 0 ? Night : AccentDeep;
            action.SetEnabled(!added && !pending);
            action.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            action.clicked += () => AddBookToShelf(book);
            card.Add(action);
            return card;
        }

        private VisualElement CreateRankingCard(BookViewModel book, int rank)
        {
            var card = Row($"ranking-{book.Id}");
            card.style.width = Length.Percent(48.5f);
            card.style.minWidth = 230;
            card.style.height = 134;
            card.style.marginRight = 6;
            card.style.marginBottom = 8;
            card.style.paddingLeft = 8;
            card.style.paddingRight = 8;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.alignItems = Align.Center;
            card.style.backgroundColor = rank <= 3 ? Rgba(0xFF, 0xF7, 0xE8, 0.94f) : Rgba(0xFF, 0xFF, 0xFF, 0.84f);
            Round(card, 14);
            Border(card, rank <= 3 ? Rgba(0x58, 0xB9, 0xFF, 0.32f) : Rgba(0x0E, 0x1D, 0x2A, 0.10f));
            UsePointerCursor(card);
            card.RegisterCallback<ClickEvent>(_ => SelectBook(book));

            var rankLabel = Text(rank.ToString("00"), rank <= 3 ? 22 : 18, rank <= 3 ? AccentDeep : Ink, FontStyle.Bold);
            rankLabel.style.width = 34;
            rankLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(rankLabel);

            card.Add(CreateBookCover(book, 60, 94, false));

            var info = new VisualElement();
            info.style.marginLeft = 8;
            info.style.flexGrow = 1f;
            info.style.minWidth = 0;
            info.style.paddingTop = 2;
            card.Add(info);

            var badgeRow = Row("badges");
            badgeRow.style.height = 22;
            badgeRow.style.alignItems = Align.Center;
            info.Add(badgeRow);

            var price = Text(book.Price == 0 ? "FREE" : $"{book.Price:N0}P", 9, book.Price == 0 ? Success : AccentDeep, FontStyle.Bold);
            price.style.height = 20;
            price.style.paddingLeft = 7;
            price.style.paddingRight = 7;
            price.style.unityTextAlign = TextAnchor.MiddleCenter;
            price.style.backgroundColor = book.Price == 0 ? Rgba(0x3F, 0x9B, 0x68, 0.14f) : Rgba(0x58, 0xB9, 0xFF, 0.18f);
            Round(price, 4);
            badgeRow.Add(price);

            var genre = Text(book.Genre, 10, FaintInk, FontStyle.Normal);
            genre.style.marginLeft = 8;
            genre.style.minWidth = 0;
            genre.style.flexShrink = 1f;
            badgeRow.Add(genre);

            var title = Text(book.Title, 12, Ink, FontStyle.Bold);
            title.style.marginTop = 4;
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.minHeight = 24;
            title.style.maxHeight = 30;
            info.Add(title);

            var byline = Text($"{book.Author} · {book.Publisher}", 10, SoftInk, FontStyle.Normal);
            byline.style.marginTop = 2;
            byline.style.minHeight = 16;
            byline.style.whiteSpace = WhiteSpace.Normal;
            info.Add(byline);

            var stats = Row("ranking-stats");
            stats.style.marginTop = 4;
            stats.style.minHeight = 18;
            stats.style.alignItems = Align.Center;
            info.Add(stats);

            var rating = Text($"★ {book.Rating:0.0}", 9, AccentDeep, FontStyle.Normal);
            rating.style.marginRight = 10;
            stats.Add(rating);

            var reviews = Text($"리뷰 {book.Reviews:N0}", 9, Ink, FontStyle.Normal);
            reviews.style.marginRight = 10;
            stats.Add(reviews);

            var views = Text($"조회 {book.Views:N0}", 9, Ink, FontStyle.Normal);
            stats.Add(views);

            return card;
        }

        private VisualElement CreateBookCover(BookViewModel book, float width, float height, bool includeText)
        {
            var cover = new VisualElement();
            cover.style.width = width;
            cover.style.height = height;
            cover.style.flexShrink = 0;
            cover.style.backgroundColor = book.CoverColor;
            cover.style.overflow = Overflow.Hidden;
            Round(cover, 5);

            var sheen = new VisualElement();
            sheen.style.position = Position.Absolute;
            sheen.style.left = 0;
            sheen.style.top = 0;
            sheen.style.width = width * 0.38f;
            sheen.style.height = height;
            sheen.style.backgroundColor = Rgba(0xFF, 0xFF, 0xFF, 0.10f);
            cover.Add(sheen);

            var band = new VisualElement();
            band.style.position = Position.Absolute;
            band.style.left = 0;
            band.style.right = 0;
            band.style.top = height * 0.34f;
            band.style.height = height * 0.24f;
            band.style.backgroundColor = book.AccentColor;
            band.style.opacity = 0.30f;
            cover.Add(band);

            var spine = new VisualElement();
            spine.style.position = Position.Absolute;
            spine.style.left = 0;
            spine.style.top = 0;
            spine.style.width = Mathf.Max(6, width * 0.08f);
            spine.style.height = height;
            spine.style.backgroundColor = Rgba(0x00, 0x00, 0x00, 0.22f);
            cover.Add(spine);

            if (includeText)
            {
                var title = Text(book.Title, width >= 100 ? 12 : 10, Surface, FontStyle.Bold);
                title.style.position = Position.Absolute;
                title.style.left = 12;
                title.style.right = 12;
                title.style.top = height * 0.38f;
                title.style.height = Mathf.Max(42, height * 0.24f);
                title.style.whiteSpace = WhiteSpace.Normal;
                title.style.maxHeight = Mathf.Max(42, height * 0.30f);
                title.style.unityTextAlign = TextAnchor.MiddleCenter;
                cover.Add(title);

                var author = Text(book.Author.ToUpperInvariant(), 7, Rgba(0xF7, 0xFF, 0xF7, 0.78f), FontStyle.Normal);
                author.style.position = Position.Absolute;
                author.style.left = 10;
                author.style.right = 8;
                author.style.bottom = 9;
                author.style.maxHeight = 14;
                cover.Add(author);
            }

            return cover;
        }

        private VisualElement EmptyState(string message)
        {
            var empty = new VisualElement();
            empty.style.flexGrow = 1f;
            empty.style.minHeight = 96;
            empty.style.alignItems = Align.Center;
            empty.style.justifyContent = Justify.Center;

            var label = Text(message, 14, FaintInk, FontStyle.Italic);
            empty.Add(label);
            return empty;
        }

        private void RefreshDetail(BookViewModel book)
        {
            if (book == null)
            {
                if (detailTitle != null)
                {
                    detailTitle.text = "책을 찾을 수 없습니다";
                }

                if (detailAuthor != null)
                {
                    detailAuthor.text = string.Empty;
                }

                if (detailPublisher != null)
                {
                    detailPublisher.text = string.Empty;
                }

                SetDetailMeta(null);

                if (detailSummary != null)
                {
                    detailSummary.text = "검색어를 바꾸거나 다른 분류를 선택해주세요.";
                }

                if (addButton != null)
                {
                    addButton.SetEnabled(false);
                    addButton.text = "+ 선택 후 추가";
                }

                return;
            }

            if (detailCover != null)
            {
                detailCover.Clear();
                detailCover.style.backgroundColor = Color.clear;
                detailCover.Add(CreateBookCover(book, 94, 136, true));
            }

            if (detailTitle != null)
            {
                detailTitle.text = book.Title;
            }

            if (detailAuthor != null)
            {
                detailAuthor.text = book.Author;
            }

            if (detailPublisher != null)
            {
                detailPublisher.text = book.Publisher;
            }

            if (detailPrice != null)
            {
                detailPrice.text = book.Price == 0 ? "Free" : $"{book.Price:N0} P";
                detailPrice.style.color = book.Price == 0 ? Success : AccentDeep;
            }

            SetDetailMeta(book);

            if (detailSummary != null)
            {
                detailSummary.text = book.Summary;
            }

            if (detailShelf != null)
            {
                detailShelf.text = $"현재 위치  {book.Shelf} · {book.Status}";
            }

            if (addButton != null)
            {
                var alreadyAdded = addedBookIds.Contains(book.Id);
                var pending = pendingShelfBookIds.Contains(book.Id);
                addButton.SetEnabled(!alreadyAdded && !pending);
                addButton.text = pending ? "추가 중" : alreadyAdded ? "✓ 내 책장에 있음" : "+ 내 책장에 추가";
                addButton.style.backgroundColor = alreadyAdded ? Success : pending ? SoftInk : Accent;
            }
        }

        private void SelectBook(BookViewModel book)
        {
            var index = Array.IndexOf(books, book);
            if (index < 0)
            {
                selectedBookOverride = book;
                OpenBookDetailModal(book);
                return;
            }

            selectedBookIndex = index;
            selectedBookOverride = null;
            OpenBookDetailModal(book);
        }

        private void ChangePage(int delta)
        {
            var totalPages = Mathf.Max(1, Mathf.CeilToInt(FilterBooks(true).Count() / (float)RankingPageSize));
            pageIndex = Mathf.Clamp(pageIndex + delta, 0, totalPages - 1);
            RefreshLists();
        }

        private void ActivateHeaderTab(string tab)
        {
            if (string.IsNullOrEmpty(tab))
            {
                return;
            }

            if (activeHeaderTab == tab)
            {
                return;
            }

            activeHeaderTab = tab;
            CloseBookDetail();
            if (tab != HeaderTabs[0])
            {
                searchPageOpen = false;
            }

            pageIndex = 0;
            if (pageScrollView != null)
            {
                pageScrollView.scrollOffset = Vector2.zero;
            }

            RefreshAll();
        }

        private void ToggleSearchSuggestion(string suggestion)
        {
            if (string.IsNullOrEmpty(suggestion))
            {
                return;
            }

            if (activeSearchSuggestion == suggestion)
            {
                activeSearchSuggestion = string.Empty;
                query = string.Empty;
                searchPageOpen = false;
                appliedSearchQuery = string.Empty;
                searchResults = Array.Empty<BookViewModel>();
                searchLoading = false;
                searchField?.SetValueWithoutNotify(query);
                pageIndex = 0;
                RefreshAll();
                return;
            }
            else
            {
                activeSearchSuggestion = suggestion;
                query = suggestion;
            }

            searchField?.SetValueWithoutNotify(query);
            pageIndex = 0;
            RefreshFilters();
        }

        private void ClearSearch()
        {
            query = string.Empty;
            activeSearchSuggestion = string.Empty;
            searchPageOpen = false;
            appliedSearchQuery = string.Empty;
            searchResults = Array.Empty<BookViewModel>();
            searchLoading = false;
            searchField?.SetValueWithoutNotify(query);
            pageIndex = 0;
            RefreshAll();
        }

        private void SubmitSearch()
        {
            var normalizedQuery = (query ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                searchPageOpen = false;
                appliedSearchQuery = string.Empty;
                searchResults = Array.Empty<BookViewModel>();
                searchLoading = false;
                ShowToast("검색어를 입력해주세요.");
                RefreshAll();
                return;
            }

            query = normalizedQuery;
            activeSearchSuggestion = SuggestedSearches.Contains(normalizedQuery)
                ? normalizedQuery
                : string.Empty;
            activeHeaderTab = HeaderTabs[0];
            searchPageOpen = true;
            CloseBookDetail();
            searchField?.SetValueWithoutNotify(query);
            pageIndex = 0;
            if (pageScrollView != null)
            {
                pageScrollView.scrollOffset = Vector2.zero;
            }

            RequestSearchResults(normalizedQuery);
        }

        private void CloseSearchPage()
        {
            searchPageOpen = false;
            pageIndex = 0;
            if (pageScrollView != null)
            {
                pageScrollView.scrollOffset = Vector2.zero;
            }

            RefreshAll();
        }

        private void AddSelectedBook()
        {
            AddBookToShelf(GetSelectedBook());
        }

        private void AddBookToShelf(BookViewModel book)
        {
            if (book == null)
            {
                return;
            }

            var index = Array.IndexOf(books, book);
            if (index >= 0)
            {
                selectedBookIndex = index;
            }

            if (addedBookIds.Contains(book.Id))
            {
                ShowToast($"이미 내 책장에 있어요 · {book.Title}");
                return;
            }

            if (pendingShelfBookIds.Contains(book.Id))
            {
                ShowToast("책장 추가 요청을 처리 중입니다.");
                return;
            }

            if (book.Price > 0 && pages < book.Price)
            {
                ShowToast($"{book.Price - pages:N0}P가 더 필요합니다.");
                return;
            }

            if (!CanUseShelfApi(book))
            {
                AddBookToShelfLocal(book);
                return;
            }

            AddBookToShelfThroughApi(book);
        }

        private bool CanUseShelfApi(BookViewModel book)
        {
            if (book == null || APIManager.Instance == null)
            {
                return false;
            }

            if (string.Equals(book.ApiSource, "LOCAL", StringComparison.OrdinalIgnoreCase))
            {
                return book.ServerBookId.HasValue;
            }

            return string.Equals(book.ApiSource, "KAKAO", StringComparison.OrdinalIgnoreCase) &&
                   (!string.IsNullOrWhiteSpace(book.Isbn13) || !string.IsNullOrWhiteSpace(book.Isbn10));
        }

        private void AddBookToShelfThroughApi(BookViewModel book)
        {
            pendingShelfBookIds.Add(book.Id);
            RefreshLists();
            RefreshBookDetailPage(book);

            try
            {
                if (string.Equals(book.ApiSource, "KAKAO", StringComparison.OrdinalIgnoreCase))
                {
                    GetNotebookApiClient().AddKakaoBookToShelf(
                        DefaultBookshelfId,
                        BuildKakaoAddRequest(book),
                        response => CompleteApiBookAdded(book, response),
                        error => FailApiBookAdd(book, error));
                    return;
                }

                GetNotebookApiClient().AddBookToShelf(
                    DefaultBookshelfId,
                    BuildLocalAddRequest(book),
                    response => CompleteApiBookAdded(book, response),
                    error => FailApiBookAdd(book, error));
            }
            catch (Exception exception)
            {
                pendingShelfBookIds.Remove(book.Id);
                UnityEngine.Debug.LogWarning($"Book shelf API request failed before send. book={book.Id}");
                UnityEngine.Debug.LogException(exception);
                RefreshLists();
                RefreshBookDetailPage(book);
                ShowToast("책장 추가 요청을 보내지 못했습니다.");
            }
        }

        private AddBookToShelfRequestDto BuildLocalAddRequest(BookViewModel book)
        {
            return new AddBookToShelfRequestDto
            {
                BookId = book.ServerBookId.GetValueOrDefault(),
                SectionId = DefaultSectionId,
                SlotNo = DefaultSlotNo,
                DisplayStyle = DefaultDisplayStyle
            };
        }

        private AddKakaoBookToShelfRequestDto BuildKakaoAddRequest(BookViewModel book)
        {
            return new AddKakaoBookToShelfRequestDto
            {
                Isbn13 = book.Isbn13,
                Isbn10 = book.Isbn10,
                Title = book.Title,
                Author = book.Author,
                Publisher = book.Publisher,
                PublishedDate = book.PublishedDate,
                Description = book.Summary,
                CoverImageUrl = book.CoverImageUrl,
                SectionId = DefaultSectionId,
                SlotNo = DefaultSlotNo,
                DisplayStyle = DefaultDisplayStyle
            };
        }

        private void CompleteApiBookAdded(BookViewModel book, UserBookResponseDto response)
        {
            pendingShelfBookIds.Remove(book.Id);
            AddBookToShelfLocal(book);
            UnityEngine.Debug.Log($"Book added to shelf through API. book={book.Id}, userBookId={response?.Id}");
        }

        private void FailApiBookAdd(BookViewModel book, ApiErrorResponseDto error)
        {
            pendingShelfBookIds.Remove(book.Id);
            UnityEngine.Debug.LogWarning($"Book shelf API failed. book={book.Id}, code={error?.Code}, message={FirstText(error?.Message, error?.Error)}");
            RefreshLists();
            RefreshBookDetailPage(book);
            ShowToast("서버 책장 추가에 실패했습니다. 잠시 후 다시 시도해주세요.");
        }

        private void AddBookToShelfLocal(BookViewModel book)
        {
            if (book == null)
            {
                return;
            }

            var index = Array.IndexOf(books, book);
            if (index >= 0)
            {
                selectedBookIndex = index;
            }

            if (addedBookIds.Contains(book.Id))
            {
                ShowToast($"이미 내 책장에 있어요 · {book.Title}");
                return;
            }

            if (book.Price > 0 && pages < book.Price)
            {
                ShowToast($"{book.Price - pages:N0}P가 더 필요합니다.");
                return;
            }

            if (book.Price > 0)
            {
                pages -= book.Price;
                RefreshWallet();
            }

            addedBookIds.Add(book.Id);
            RefreshLists();
            RefreshBookDetailPage(book);
            ShowToast(book.Price > 0 ? $"구매 완료 · {book.Title}" : $"내 책장에 추가됨 · {book.Title}");
        }

        private void PurchaseObject(DecorObjectViewModel item)
        {
            if (item == null)
            {
                return;
            }

            if (ownedObjectIds.Contains(item.Id))
            {
                ShowToast($"이미 소유 중입니다 · {item.Name}");
                return;
            }

            if (pages < item.Price)
            {
                ShowToast($"{item.Price - pages:N0}P가 더 필요합니다.");
                return;
            }

            pages -= item.Price;
            ownedObjectIds.Add(item.Id);
            wishedObjectIds.Remove(item.Id);
            RefreshWallet();
            RefreshObjectMarket();
            ShowToast($"오브젝트 구매 완료 · {item.Name}");
        }

        private Label MetricLabel(string text, Color color)
        {
            var label = Text(text, 10, color, FontStyle.Bold);
            label.style.height = 22;
            label.style.paddingLeft = 8;
            label.style.paddingRight = 8;
            label.style.marginRight = 6;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.backgroundColor = Rgba(0x0E, 0x1D, 0x2A, 0.05f);
            Round(label, 11);
            return label;
        }

        private static void ToggleSet(HashSet<string> set, string key)
        {
            if (set.Contains(key))
            {
                set.Remove(key);
                return;
            }

            set.Add(key);
        }

        private void RefreshFollowButton(Button button, string userId)
        {
            RefreshToggleButton(button, followedUserIds.Contains(userId), "팔로우 중", "팔로우");
        }

        private static void RefreshToggleButton(Button button, bool selected, string selectedText, string defaultText)
        {
            if (button == null)
            {
                return;
            }

            button.text = selected ? selectedText : defaultText;
            button.style.backgroundColor = selected ? Ink : Rgba(0xFF, 0xFF, 0xFF, 0.52f);
            button.style.color = selected ? Surface : SoftInk;
            Border(button, selected ? Ink : Line);
        }

        private void SetDetailMeta(BookViewModel book)
        {
            if (detailRating == null || detailReviews == null || detailViews == null)
            {
                return;
            }

            if (book == null)
            {
                detailRating.text = string.Empty;
                detailReviews.text = string.Empty;
                detailViews.text = string.Empty;
                return;
            }

            detailRating.text = $"★ {book.Rating:0.0}";
            detailReviews.text = $"리뷰 {book.Reviews:N0}";
            detailViews.text = $"조회 {book.Views:N0}";
        }

        private void ShowToast(string message)
        {
            if (toast == null || toastLabel == null)
            {
                return;
            }

            toastLabel.text = message;
            toast.style.display = DisplayStyle.Flex;
            var version = ++toastHideVersion;
            toast.schedule.Execute(() =>
            {
                if (toast != null && version == toastHideVersion)
                {
                    toast.style.display = DisplayStyle.None;
                }
            }).StartingIn(1800);
        }

        private static void FadeInElement(VisualElement element, float duration)
        {
            if (element == null)
            {
                return;
            }

            element.style.opacity = 0f;
            var start = Time.realtimeSinceStartup;
            element.schedule.Execute(() =>
            {
                var t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / Mathf.Max(0.01f, duration));
                element.style.opacity = Mathf.SmoothStep(0f, 1f, t);
            }).Every(16).Until(() => Time.realtimeSinceStartup - start >= duration);
            element.schedule.Execute(() => element.style.opacity = 1f).StartingIn((long)(duration * 1000f) + 18);
        }

        private static void SlideFadeInElement(VisualElement element, float fromTop, float toTop, float duration)
        {
            if (element == null)
            {
                return;
            }

            element.style.opacity = 0f;
            element.style.marginTop = fromTop;
            var start = Time.realtimeSinceStartup;
            element.schedule.Execute(() =>
            {
                var t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / Mathf.Max(0.01f, duration));
                var eased = Mathf.SmoothStep(0f, 1f, t);
                element.style.opacity = eased;
                element.style.marginTop = Mathf.Lerp(fromTop, toTop, eased);
            }).Every(16).Until(() => Time.realtimeSinceStartup - start >= duration);
            element.schedule.Execute(() =>
            {
                element.style.opacity = 1f;
                element.style.marginTop = toTop;
            }).StartingIn((long)(duration * 1000f) + 18);
        }

        private static void SlideFadeInFromRight(VisualElement element, float fromRight, float toRight, float duration)
        {
            if (element == null)
            {
                return;
            }

            element.style.opacity = 0f;
            element.style.right = fromRight;
            var start = Time.realtimeSinceStartup;
            element.schedule.Execute(() =>
            {
                var t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / Mathf.Max(0.01f, duration));
                var eased = Mathf.SmoothStep(0f, 1f, t);
                element.style.opacity = eased;
                element.style.right = Mathf.Lerp(fromRight, toRight, eased);
            }).Every(16).Until(() => Time.realtimeSinceStartup - start >= duration);
            element.schedule.Execute(() =>
            {
                element.style.opacity = 1f;
                element.style.right = toRight;
            }).StartingIn((long)(duration * 1000f) + 18);
        }

        private void AnimateModalHeight(float fromHeight, float toHeight, float duration)
        {
            if (bookDetailModalPanel == null)
            {
                return;
            }

            var start = Time.realtimeSinceStartup;
            bookDetailModalPanel.schedule.Execute(() =>
            {
                var t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / Mathf.Max(0.01f, duration));
                var eased = Mathf.SmoothStep(0f, 1f, t);
                bookDetailModalPanel.style.height = Mathf.Lerp(fromHeight, toHeight, eased);
            }).Every(16).Until(() => Time.realtimeSinceStartup - start >= duration);
            bookDetailModalPanel.schedule.Execute(() => bookDetailModalPanel.style.height = toHeight).StartingIn((long)(duration * 1000f) + 18);
        }

        private static void PulseElement(VisualElement element)
        {
            if (element == null)
            {
                return;
            }

            element.style.opacity = 0.62f;
            element.schedule.Execute(() => element.style.opacity = 1f).StartingIn(90);
        }

        private static bool Contains(string source, string value)
        {
            return source != null &&
                   value != null &&
                   source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FirstText(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static long? ResolveServerBookId(BookListItemDto dto)
        {
            if (dto == null)
            {
                return null;
            }

            if (dto.Id > 0)
            {
                return dto.Id;
            }

            return long.TryParse(dto.BookId, out var parsed) && parsed > 0 ? parsed : null;
        }

        private static string ResolveSearchGenre(string title, string summary)
        {
            var text = $"{title} {summary}";
            if (Contains(text, "경제") || Contains(text, "경영"))
            {
                return "경제/경영";
            }

            if (Contains(text, "SF") || Contains(text, "판타지"))
            {
                return "SF/판타지";
            }

            if (Contains(text, "심리"))
            {
                return "심리";
            }

            if (Contains(text, "인문"))
            {
                return "인문";
            }

            if (Contains(text, "에세이") || Contains(text, "산문"))
            {
                return "에세이";
            }

            return "소설";
        }

        private static Color SearchCoverColor(int index, string seed)
        {
            var colors = new[]
            {
                Hex(0x0A2540),
                Hex(0x1D5F8C),
                Hex(0x2D7EC0),
                Hex(0x12345A),
                Hex(0x061422),
                Hex(0x7FC2EF)
            };
            var hash = string.IsNullOrEmpty(seed) ? index : Mathf.Abs(seed.GetHashCode());
            return colors[(hash + index) % colors.Length];
        }

        private static string CategoryIcon(int index)
        {
            return index switch
            {
                1 => "◐",
                2 => "✓",
                3 => "○",
                4 => "▤",
                5 => "▥",
                _ => "▦",
            };
        }

        private Label Text(string text, int size, Color color, FontStyle style)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = style;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.overflow = Overflow.Visible;
            label.style.minWidth = 0;
            label.style.flexShrink = 1f;
            if (uiFont != null)
            {
                label.style.unityFont = uiFont;
            }

            return label;
        }

        private static VisualElement Row(string name)
        {
            return new VisualElement
            {
                name = name,
                style =
                {
                    flexDirection = FlexDirection.Row,
                },
            };
        }

        private static void UsePointerCursor(VisualElement element)
        {
            if (element == null)
            {
                return;
            }

            element.style.cursor = new StyleCursor(new UnityEngine.UIElements.Cursor
            {
                texture = GetPointerCursorTexture(),
                hotspot = new Vector2(9, 1)
            });
        }

        private Button TextButton(string text, bool primary)
        {
            var button = new Button { text = text };
            button.style.height = 38;
            button.style.paddingLeft = 12;
            button.style.paddingRight = 12;
            button.style.paddingTop = 0;
            button.style.paddingBottom = 0;
            button.style.backgroundColor = primary ? Accent : Color.clear;
            button.style.color = primary ? Color.white : SoftInk;
            button.style.fontSize = 12;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.minWidth = 0;
            button.style.flexShrink = 0;
            button.style.overflow = Overflow.Visible;
            if (uiFont != null)
            {
                button.style.unityFont = uiFont;
            }

            button.style.borderTopWidth = 0;
            button.style.borderRightWidth = 0;
            button.style.borderBottomWidth = 0;
            button.style.borderLeftWidth = 0;
            Round(button, 10);
            UsePointerCursor(button);
            return button;
        }

        private Button IconButton(string text, string tooltip)
        {
            var button = TextButton(text, false);
            button.tooltip = tooltip;
            button.style.width = 38;
            button.style.height = 38;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
            button.style.fontSize = text == "x" ? 17 : 22;
            button.style.backgroundColor = Rgba(0xFF, 0xFF, 0xFF, 0.66f);
            button.style.color = Ink;
            Border(button, Line);
            return button;
        }

        private Button CloseIconButton(string tooltip)
        {
            var button = TextButton(string.Empty, false);
            button.tooltip = tooltip;
            button.style.width = 38;
            button.style.height = 38;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.backgroundColor = Rgba(0xFF, 0xFF, 0xFF, 0.66f);
            button.style.backgroundImage = new StyleBackground(GetCloseIconTexture());
            button.style.color = Ink;
            Border(button, Line);
            return button;
        }

        private static void BuildSpaceThumbnail(VisualElement thumbnail, SpaceViewModel space)
        {
            var key = string.IsNullOrEmpty(space.ThumbnailKey) ? space.Id : space.ThumbnailKey;
            var resource = Resources.Load<Texture2D>($"SpaceThumbnails/{key}");
            if (resource != null)
            {
                thumbnail.style.backgroundImage = new StyleBackground(resource);
                thumbnail.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                return;
            }

            var accent = space.AccentColor;
            thumbnail.style.backgroundColor = Color.Lerp(Night, accent, 0.28f);

            var wall = RoomRect("photo-wall", 18, 20, 254, 88, Rgba(0xF7, 0xFF, 0xF7, 0.13f), 10);
            wall.style.borderTopWidth = 1;
            wall.style.borderRightWidth = 1;
            wall.style.borderBottomWidth = 1;
            wall.style.borderLeftWidth = 1;
            wall.style.borderTopColor = Rgba(0xFF, 0xFF, 0xFF, 0.13f);
            wall.style.borderRightColor = Rgba(0xFF, 0xFF, 0xFF, 0.13f);
            wall.style.borderBottomColor = Rgba(0xFF, 0xFF, 0xFF, 0.13f);
            wall.style.borderLeftColor = Rgba(0xFF, 0xFF, 0xFF, 0.13f);
            thumbnail.Add(wall);

            AddShelfPreview(thumbnail, 38, 56, 128, 44, accent, key == "endless-sf-shelf" ? 14 : 10);
            AddDeskPreview(thumbnail, 42, 94, 204, 18, key == "endless-sf-shelf" ? Color.Lerp(Night, Violet, 0.46f) : WarmPaper);
            AddNotebookPreview(thumbnail, key == "sunday-economy-desk" ? 146 : 116, 78, Accent);

            if (key == "sunday-economy-desk")
            {
                AddWindowPreview(thumbnail, 204, 36, 62, 56, Accent, true);
                AddChartPreview(thumbnail, 58, 38, 74, 46, Gold);
            }
            else if (key == "endless-sf-shelf")
            {
                AddWindowPreview(thumbnail, 204, 34, 58, 62, Accent, false);
                AddRoomRect(thumbnail, "neon-line", 212, 74, 42, 3, Surface, 1);
            }
            else if (key == "sentence-collector-shelf")
            {
                AddPinnedCardPreview(thumbnail, 204, 40, 54, 52, Gold);
            }
            else
            {
                AddLampPreview(thumbnail, 208, 66, Gold);
            }

            AddRoomRect(thumbnail, "photo-floor-glow", 0, 106, 320, 34, Rgba(0xFF, 0xFF, 0xFF, 0.08f), 0);
        }

        private static VisualElement RoomRect(string name, int left, int top, int width, int height, Color color, int radius)
        {
            var element = new VisualElement { name = name };
            element.pickingMode = PickingMode.Ignore;
            element.style.position = Position.Absolute;
            element.style.left = left;
            element.style.top = top;
            element.style.width = width;
            element.style.height = height;
            element.style.backgroundColor = color;
            Round(element, radius);
            return element;
        }

        private static void AddRoomRect(VisualElement parent, string name, int left, int top, int width, int height, Color color, int radius)
        {
            parent.Add(RoomRect(name, left, top, width, height, color, radius));
        }

        private static void AddShelfPreview(VisualElement parent, int left, int top, int width, int height, Color accent, int bookCount)
        {
            AddRoomRect(parent, "room-shelf-rail", left, top + height - 6, width, 6, Rgba(0x0E, 0x1D, 0x2A, 0.34f), 3);
            var cursor = left + 8;
            for (var i = 0; i < bookCount; i++)
            {
                var bookWidth = 5 + i % 3 * 4;
                var bookHeight = height - 8 - i % 5 * 3;
                var color = (i % 5) switch
                {
                    0 => accent,
                    1 => Gold,
                    2 => Coral,
                    3 => AccentDeep,
                    _ => Surface,
                };
                AddRoomRect(parent, $"room-book-{i}", cursor, top + height - 6 - bookHeight, bookWidth, bookHeight, Color.Lerp(color, Color.white, 0.08f), 1);
                cursor += bookWidth + 4;
                if (cursor > left + width - 10)
                {
                    break;
                }
            }
        }

        private static void AddDeskPreview(VisualElement parent, int left, int top, int width, int height, Color color)
        {
            AddRoomRect(parent, "room-desk", left, top, width, height, Color.Lerp(color, Color.black, 0.08f), 8);
            AddRoomRect(parent, "room-desk-left-leg", left + 14, top + height - 1, 8, 22, Rgba(0x0E, 0x1D, 0x2A, 0.34f), 2);
            AddRoomRect(parent, "room-desk-right-leg", left + width - 22, top + height - 1, 8, 22, Rgba(0x0E, 0x1D, 0x2A, 0.34f), 2);
        }

        private static void AddNotebookPreview(VisualElement parent, int left, int top, Color accent)
        {
            AddRoomRect(parent, "room-notebook-screen", left, top, 56, 28, Color.Lerp(accent, Surface, 0.35f), 4);
            AddRoomRect(parent, "room-notebook-display", left + 7, top + 6, 42, 15, Rgba(0x0E, 0x1D, 0x2A, 0.58f), 2);
            AddRoomRect(parent, "room-notebook-base", left - 8, top + 28, 72, 6, Rgba(0x0E, 0x1D, 0x2A, 0.26f), 3);
        }

        private static void AddLampPreview(VisualElement parent, int left, int top, Color color)
        {
            AddRoomRect(parent, "room-lamp-stand", left + 16, top - 24, 4, 34, Rgba(0x0E, 0x1D, 0x2A, 0.38f), 2);
            AddRoomRect(parent, "room-lamp-shade", left, top, 36, 17, color, 9);
            AddRoomRect(parent, "room-lamp-glow", left - 14, top + 8, 64, 38, Rgba(0xF4, 0xC9, 0x5D, 0.12f), 18);
        }

        private static void AddWindowPreview(VisualElement parent, int left, int top, int width, int height, Color color, bool daylight)
        {
            var fill = daylight ? Color.Lerp(color, Surface, 0.48f) : Color.Lerp(color, Night, 0.32f);
            AddRoomRect(parent, "room-window", left, top, width, height, fill, 8);
            AddRoomRect(parent, "room-window-v", left + width / 2 - 1, top + 5, 2, height - 10, Rgba(0x0E, 0x1D, 0x2A, 0.20f), 1);
            AddRoomRect(parent, "room-window-h", left + 5, top + height / 2 - 1, width - 10, 2, Rgba(0x0E, 0x1D, 0x2A, 0.20f), 1);
        }

        private static void AddChartPreview(VisualElement parent, int left, int top, int width, int height, Color color)
        {
            AddRoomRect(parent, "room-chart-board", left, top, width, height, Rgba(0xFF, 0xFF, 0xFF, 0.24f), 8);
            for (var i = 0; i < 4; i++)
            {
                AddRoomRect(parent, $"room-chart-bar-{i}", left + 12 + i * 13, top + height - 11 - i * 6, 7, 10 + i * 6, i % 2 == 0 ? Accent : color, 2);
            }
        }

        private static void AddPinnedCardPreview(VisualElement parent, int left, int top, int width, int height, Color color)
        {
            AddRoomRect(parent, "room-pin-board", left, top, width, height, Rgba(0xFF, 0xFF, 0xFF, 0.22f), 9);
            AddRoomRect(parent, "room-pin-card-a", left + 8, top + 10, width - 16, 18, Rgba(0xFF, 0xF7, 0xE8, 0.46f), 5);
            AddRoomRect(parent, "room-pin-line", left + 14, top + 19, width - 28, 3, color, 1);
            AddRoomRect(parent, "room-pin-card-b", left + 10, top + 33, width - 20, 14, Rgba(0xFF, 0xFF, 0xFF, 0.30f), 5);
        }

        private static Texture2D GetCloseIconTexture()
        {
            if (closeIconTexture != null)
            {
                return closeIconTexture;
            }

            const int size = 38;
            closeIconTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var clear = new Color32(0, 0, 0, 0);
            var pixels = new Color32[size * size];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            closeIconTexture.SetPixels32(pixels);
            var ink = new Color32(14, 29, 42, 255);
            DrawIconLine(closeIconTexture, 13, 13, 25, 25, ink);
            DrawIconLine(closeIconTexture, 25, 13, 13, 25, ink);
            closeIconTexture.Apply(false, true);
            return closeIconTexture;
        }

        private static Texture2D GetSearchIconTexture()
        {
            if (searchIconTexture != null)
            {
                return searchIconTexture;
            }

            const int size = 32;
            searchIconTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var clear = new Color32(0, 0, 0, 0);
            var pixels = new Color32[size * size];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            searchIconTexture.SetPixels32(pixels);
            var color = new Color32(14, 29, 42, 180);
            PaintIconCircle(searchIconTexture, 14, 14, 8, color);
            DrawIconLine(searchIconTexture, 20, 20, 27, 27, color);
            searchIconTexture.Apply(false, true);
            return searchIconTexture;
        }

        private static Texture2D GetPointerCursorTexture()
        {
            if (pointerCursorTexture != null)
            {
                return pointerCursorTexture;
            }

            const int size = 24;
            pointerCursorTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            var clear = new Color32(0, 0, 0, 0);
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            pointerCursorTexture.SetPixels32(pixels);

            var fill = new bool[size, size];
            FillCursorRect(fill, 8, 1, 3, 12);
            FillCursorRect(fill, 11, 7, 3, 9);
            FillCursorRect(fill, 14, 8, 3, 8);
            FillCursorRect(fill, 17, 9, 3, 7);
            FillCursorRect(fill, 4, 11, 16, 8);
            FillCursorRect(fill, 5, 18, 13, 2);
            FillCursorRect(fill, 7, 20, 9, 2);

            var outline = new Color32(14, 29, 42, 255);
            var white = new Color32(255, 255, 255, 255);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    if (fill[x, y])
                    {
                        PaintCursorPixel(pointerCursorTexture, x, y, white);
                    }
                    else if (HasCursorNeighbor(fill, x, y))
                    {
                        PaintCursorPixel(pointerCursorTexture, x, y, outline);
                    }
                }
            }

            pointerCursorTexture.Apply(false, true);
            return pointerCursorTexture;
        }

        private static void FillCursorRect(bool[,] fill, int left, int top, int width, int height)
        {
            var maxX = fill.GetLength(0);
            var maxY = fill.GetLength(1);
            for (var y = top; y < top + height; y++)
            {
                for (var x = left; x < left + width; x++)
                {
                    if (x >= 0 && y >= 0 && x < maxX && y < maxY)
                    {
                        fill[x, y] = true;
                    }
                }
            }
        }

        private static bool HasCursorNeighbor(bool[,] fill, int x, int y)
        {
            for (var yy = y - 1; yy <= y + 1; yy++)
            {
                for (var xx = x - 1; xx <= x + 1; xx++)
                {
                    if (xx == x && yy == y)
                    {
                        continue;
                    }

                    if (xx >= 0 && yy >= 0 && xx < fill.GetLength(0) && yy < fill.GetLength(1) && fill[xx, yy])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void PaintCursorPixel(Texture2D texture, int x, int topY, Color32 color)
        {
            if (x < 0 || topY < 0 || x >= texture.width || topY >= texture.height)
            {
                return;
            }

            texture.SetPixel(x, texture.height - 1 - topY, color);
        }

        private static void PaintIconCircle(Texture2D texture, int centerX, int centerY, int radius, Color32 color)
        {
            var inner = (radius - 1) * (radius - 1);
            var outer = (radius + 1) * (radius + 1);
            for (var y = centerY - radius - 2; y <= centerY + radius + 2; y++)
            {
                for (var x = centerX - radius - 2; x <= centerX + radius + 2; x++)
                {
                    var dx = x - centerX;
                    var dy = y - centerY;
                    var distance = dx * dx + dy * dy;
                    if (distance >= inner && distance <= outer)
                    {
                        PaintIconPixel(texture, x, y, color);
                    }
                }
            }
        }

        private static void DrawIconLine(Texture2D texture, int x0, int y0, int x1, int y1, Color32 color)
        {
            var dx = Mathf.Abs(x1 - x0);
            var dy = -Mathf.Abs(y1 - y0);
            var sx = x0 < x1 ? 1 : -1;
            var sy = y0 < y1 ? 1 : -1;
            var error = dx + dy;

            while (true)
            {
                PaintIconPixel(texture, x0, y0, color);
                PaintIconPixel(texture, x0 + 1, y0, color);
                PaintIconPixel(texture, x0, y0 + 1, color);

                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                var e2 = 2 * error;
                if (e2 >= dy)
                {
                    error += dy;
                    x0 += sx;
                }

                if (e2 <= dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        private static void PaintIconPixel(Texture2D texture, int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
            {
                return;
            }

            texture.SetPixel(x, y, color);
        }

        private void StyleTextField(TextField field)
        {
            if (field == null)
            {
                return;
            }

            field.style.backgroundColor = Color.clear;
            field.style.borderTopWidth = 0;
            field.style.borderRightWidth = 0;
            field.style.borderBottomWidth = 0;
            field.style.borderLeftWidth = 0;
            field.style.paddingLeft = 0;
            field.style.paddingRight = 0;
            field.style.paddingTop = 0;
            field.style.paddingBottom = 0;
            if (uiFont != null)
            {
                field.style.unityFont = uiFont;
            }

            field.Query<VisualElement>().ForEach(element =>
            {
                element.style.backgroundColor = Color.clear;
                element.style.borderTopWidth = 0;
                element.style.borderRightWidth = 0;
                element.style.borderBottomWidth = 0;
                element.style.borderLeftWidth = 0;
                element.style.marginLeft = 0;
                element.style.marginRight = 0;
                element.style.marginTop = 0;
                element.style.marginBottom = 0;
            });

            field.Query<TextElement>().ForEach(element =>
            {
                if (uiFont != null)
                {
                    element.style.unityFont = uiFont;
                }
            });
        }

        private static void SetButtonSelected(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            button.style.backgroundColor = selected ? Ink : Rgba(0xFF, 0xFF, 0xFF, 0.50f);
            button.style.color = selected ? Surface : SoftInk;
            Border(button, selected ? Ink : Line);
        }

        private static void SetSearchSuggestionSelected(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            button.style.backgroundColor = selected ? Ink : Rgba(0xFF, 0xFF, 0xFF, 0.62f);
            button.style.color = selected ? Surface : SoftInk;
            Border(button, selected ? Ink : Rgba(0x0E, 0x1D, 0x2A, 0.10f));
        }

        private static void HideNativeScroller(ScrollView scrollView)
        {
            var scroller = scrollView?.verticalScroller;
            if (scroller == null)
            {
                return;
            }

            scroller.style.display = DisplayStyle.None;
            scroller.style.width = 0;
            scroller.style.minWidth = 0;
            scroller.style.marginLeft = 0;
            scroller.style.marginRight = 0;
        }

        private static void Round(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        private static void Border(VisualElement element, Color color)
        {
            element.style.borderTopWidth = 1;
            element.style.borderRightWidth = 1;
            element.style.borderBottomWidth = 1;
            element.style.borderLeftWidth = 1;
            element.style.borderTopColor = color;
            element.style.borderRightColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftColor = color;
        }

        private static void BorderTop(VisualElement element, Color color)
        {
            element.style.borderTopWidth = 1;
            element.style.borderTopColor = color;
        }

        private static void BorderBottom(VisualElement element, Color color)
        {
            element.style.borderBottomWidth = 1;
            element.style.borderBottomColor = color;
        }

        private static Color Hex(int hex)
        {
            return new Color(
                ((hex >> 16) & 0xFF) / 255f,
                ((hex >> 8) & 0xFF) / 255f,
                (hex & 0xFF) / 255f,
                1f);
        }

        private static Color Rgba(byte r, byte g, byte b, float a)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a);
        }
    }
}
