using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
                Color accentColor)
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

        private sealed class FeedRecordViewModel
        {
            public FeedRecordViewModel(
                string id,
                string authorId,
                string author,
                string spaceTitle,
                string bookId,
                string title,
                string excerpt,
                int likes,
                int comments,
                Color accentColor)
            {
                Id = id;
                AuthorId = authorId;
                Author = author;
                SpaceTitle = spaceTitle;
                BookId = bookId;
                Title = title;
                Excerpt = excerpt;
                Likes = likes;
                Comments = comments;
                AccentColor = accentColor;
            }

            public string Id { get; }
            public string AuthorId { get; }
            public string Author { get; }
            public string SpaceTitle { get; }
            public string BookId { get; }
            public string Title { get; }
            public string Excerpt { get; }
            public int Likes { get; }
            public int Comments { get; }
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
        private static readonly string[] SuggestedSearches = { "소설", "에세이", "심리", "베스트셀러", "SF", "자기계발", "인문" };

        private static readonly string[] HeaderTabs = { "책 시장", "다른 공간", "피드", "꾸미기 시장" };
        private static readonly string[] SubTabs = { "전체 탐색", "무료", "유료", "추천 책", "책 랭킹" };
        private static readonly string[] Categories = { "전체", "읽는 중", "읽음", "안 읽음", "책장 1", "책장 2" };
        private static readonly string[] Genres = { "전체", "소설", "에세이", "인문", "심리", "경제/경영", "SF/판타지" };
        private static readonly string[] RankingSorts = { "인기순", "별점순", "리뷰순", "조회순" };
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

        private readonly FeedRecordViewModel[] feedRecords =
        {
            new("f1", "u-hansu", "한수정", "밤의 문장 작업실", "r1", "오늘의 문장 기록", "저녁에 읽은 짧은 산문을 책상 왼쪽 노트에 옮겨 적었습니다.", 348, 42, Accent),
            new("f2", "u-mina", "조민아", "끝없는 방의 서가", "r4", "공간이 기억을 가질 때", "새 오브젝트를 배치하니 SF 서가의 분위기가 훨씬 선명해졌습니다.", 512, 61, Violet),
            new("f3", "u-jaehoon", "이재훈", "일요일 경제 책상", "r6", "주말 경제 루틴", "한 챕터씩 읽고 대시보드에 요약을 붙이는 방식이 잘 맞았습니다.", 286, 35, Gold),
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
        private readonly HashSet<string> likedRecordIds = new();
        private readonly HashSet<string> commentedRecordIds = new();
        private readonly HashSet<string> reportedRecordIds = new();
        private readonly HashSet<string> blockedAuthorIds = new();
        private readonly HashSet<string> wishedObjectIds = new();
        private readonly HashSet<string> ownedObjectIds = new();

        private UnityAction closeRequested;
        private Font uiFont;
        private VisualElement root;
        private VisualElement heroPanel;
        private VisualElement searchContainer;
        private VisualElement suggestionsContainer;
        private VisualElement subTabsRow;
        private VisualElement bookMarketContent;
        private VisualElement otherSpacesContent;
        private VisualElement feedContent;
        private VisualElement objectMarketContent;
        private VisualElement objectList;
        private VisualElement recommendList;
        private VisualElement rankingGrid;
        private VisualElement detailCover;
        private ScrollView recommendScrollView;
        private TextField searchField;
        private Button clearSearchButton;
        private Label headerSubtitleLabel;
        private Label heroEyebrowLabel;
        private Label heroTitleLabel;
        private Label heroCopyLabel;
        private Label pageBalanceLabel;
        private Label resultLabel;
        private Label objectResultLabel;
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
        private int toastHideVersion;
        private int dashboardShowVersion;

        private string query = string.Empty;
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
        private bool initialized;
        private bool visible;

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
            pageScroll.verticalScroller.valueChanged += _ => UpdateScrollThumb();
            pageScroll.schedule.Execute(() => HideNativeScroller(pageScroll)).StartingIn(1);
            shell.Add(pageScroll);

            BuildHero(pageScroll.contentContainer);
            BuildSubTabs(pageScroll.contentContainer);
            BuildMain(pageScroll.contentContainer);
            BuildFooter(pageScroll.contentContainer);
            BuildToast(dashboard);
            BuildScrollRail(dashboard);
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

            heroPanel = null;
            searchContainer = null;
            suggestionsContainer = null;
            subTabsRow = null;
            bookMarketContent = null;
            otherSpacesContent = null;
            feedContent = null;
            objectMarketContent = null;
            objectList = null;
            recommendList = null;
            rankingGrid = null;
            detailCover = null;
            recommendScrollView = null;
            searchField = null;
            clearSearchButton = null;
            headerSubtitleLabel = null;
            heroEyebrowLabel = null;
            heroTitleLabel = null;
            heroCopyLabel = null;
            pageBalanceLabel = null;
            resultLabel = null;
            objectResultLabel = null;
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
            close.style.width = 38;
            close.style.height = 38;
            close.style.marginLeft = 8;
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
                pageIndex = 0;
                RefreshFilters();
                RefreshLists();
            });
            search.Add(searchField);

            clearSearchButton = CloseIconButton("검색어 지우기");
            clearSearchButton.style.width = 34;
            clearSearchButton.style.height = 34;
            clearSearchButton.style.marginRight = 6;
            clearSearchButton.style.backgroundColor = Rgba(0x0E, 0x1D, 0x2A, 0.06f);
            clearSearchButton.clicked += ClearSearch;
            search.Add(clearSearchButton);

            var searchButton = TextButton("검색", true);
            searchButton.tooltip = "검색";
            searchButton.style.width = 68;
            searchButton.style.flexShrink = 0;
            searchButton.style.height = 38;
            searchButton.style.paddingLeft = 12;
            searchButton.style.paddingRight = 12;
            searchButton.style.backgroundColor = Accent;
            searchButton.style.color = Color.white;
            Border(searchButton, Rgba(0x58, 0xB9, 0xFF, 0.86f));
            searchButton.clicked += () =>
            {
                var normalizedQuery = (query ?? string.Empty).Trim();
                activeSearchSuggestion = SuggestedSearches.Contains(normalizedQuery) ? normalizedQuery : string.Empty;
                pageIndex = 0;
                RefreshFilters();
                RefreshLists();
            };
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
            BuildSidebar(bookMarketContent);

            var center = new VisualElement { name = "center-content" };
            center.style.flexDirection = FlexDirection.Column;
            center.style.width = StyleKeyword.Auto;
            center.style.alignSelf = Align.Stretch;
            center.style.minWidth = 0;
            center.style.marginTop = 12;
            bookMarketContent.Add(center);

            BuildDetail(center);
            BuildRecommend(center);
            BuildRanking(center);

            otherSpacesContent = CreateModeContent("other-spaces-content");
            otherSpacesContent.style.display = DisplayStyle.None;
            body.Add(otherSpacesContent);
            BuildOtherSpaces(otherSpacesContent);

            feedContent = CreateModeContent("feed-content");
            feedContent.style.display = DisplayStyle.None;
            body.Add(feedContent);
            BuildFeed(feedContent);

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

        private void BuildFeed(VisualElement parent)
        {
            BuildSectionHeader(
                parent,
                "SOCIAL FEED · 기록",
                "피드",
                "좋아요, 댓글, 신고, 차단, 팔로우, 책 담기, 공간 이동 액션을 mock 상태로 처리합니다.");

            foreach (var record in feedRecords)
            {
                parent.Add(CreateFeedCard(record));
            }
        }

        private VisualElement CreateFeedCard(FeedRecordViewModel record)
        {
            var card = new VisualElement { name = $"feed-{record.Id}" };
            card.style.minHeight = 178;
            card.style.marginBottom = 12;
            card.style.paddingLeft = 16;
            card.style.paddingRight = 16;
            card.style.paddingTop = 14;
            card.style.paddingBottom = 14;
            card.style.backgroundColor = Rgba(0xFF, 0xFF, 0xFF, 0.80f);
            Round(card, 18);
            Border(card, Rgba(0x0E, 0x1D, 0x2A, 0.10f));

            var top = Row("feed-top");
            top.style.alignItems = Align.Center;
            card.Add(top);

            var avatar = Text(record.Author.Substring(0, 1), 15, Surface, FontStyle.Bold);
            avatar.style.width = 38;
            avatar.style.height = 38;
            avatar.style.flexShrink = 0;
            avatar.style.unityTextAlign = TextAnchor.MiddleCenter;
            avatar.style.backgroundColor = record.AccentColor;
            Round(avatar, 19);
            top.Add(avatar);

            var titleColumn = new VisualElement();
            titleColumn.style.marginLeft = 12;
            titleColumn.style.flexGrow = 1f;
            titleColumn.style.minWidth = 0;
            top.Add(titleColumn);

            var author = Text($"{record.Author} · {record.SpaceTitle}", 12, SoftInk, FontStyle.Normal);
            author.style.minHeight = 18;
            titleColumn.Add(author);

            var title = Text(record.Title, 17, Ink, FontStyle.Bold);
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.minHeight = 28;
            titleColumn.Add(title);

            var excerpt = Text(record.Excerpt, 12, SoftInk, FontStyle.Normal);
            excerpt.style.marginTop = 12;
            excerpt.style.whiteSpace = WhiteSpace.Normal;
            excerpt.style.minHeight = 40;
            card.Add(excerpt);

            var linkedBook = FindBookById(record.BookId);
            if (linkedBook != null)
            {
                var bookLine = Text($"연결된 책 · {linkedBook.Title}", 11, AccentDeep, FontStyle.Bold);
                bookLine.style.marginTop = 8;
                card.Add(bookLine);
            }

            var metrics = Row("feed-metrics");
            metrics.style.marginTop = 10;
            metrics.style.alignItems = Align.Center;
            card.Add(metrics);
            metrics.Add(MetricLabel($"좋아요 {record.Likes:N0}", AccentDeep));
            metrics.Add(MetricLabel($"댓글 {record.Comments:N0}", Ink));

            var actions = Row("feed-actions");
            actions.style.marginTop = 12;
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.alignItems = Align.Center;
            card.Add(actions);

            var like = TextButton(string.Empty, false);
            like.style.height = 32;
            like.style.marginRight = 7;
            like.style.marginBottom = 6;
            like.clicked += () =>
            {
                ToggleSet(likedRecordIds, record.Id);
                RefreshToggleButton(like, likedRecordIds.Contains(record.Id), "좋아요 취소", "좋아요");
                ShowToast(likedRecordIds.Contains(record.Id) ? "피드 기록을 좋아요했습니다." : "피드 좋아요를 취소했습니다.");
            };
            actions.Add(like);
            RefreshToggleButton(like, likedRecordIds.Contains(record.Id), "좋아요 취소", "좋아요");

            var comment = TextButton(commentedRecordIds.Contains(record.Id) ? "댓글 완료" : "댓글", false);
            comment.style.height = 32;
            comment.style.marginRight = 7;
            comment.style.marginBottom = 6;
            comment.clicked += () =>
            {
                commentedRecordIds.Add(record.Id);
                comment.text = "댓글 완료";
                RefreshToggleButton(comment, true, "댓글 완료", "댓글");
                ShowToast("댓글 입력 mock 처리 완료");
            };
            actions.Add(comment);
            RefreshToggleButton(comment, commentedRecordIds.Contains(record.Id), "댓글 완료", "댓글");

            var addBook = TextButton("책 담기", true);
            addBook.style.height = 32;
            addBook.style.marginRight = 7;
            addBook.style.marginBottom = 6;
            addBook.clicked += () => AddBookToShelf(FindBookById(record.BookId));
            actions.Add(addBook);

            var follow = TextButton(string.Empty, false);
            follow.style.height = 32;
            follow.style.marginRight = 7;
            follow.style.marginBottom = 6;
            follow.clicked += () =>
            {
                ToggleSet(followedUserIds, record.AuthorId);
                RefreshFollowButton(follow, record.AuthorId);
                ShowToast(followedUserIds.Contains(record.AuthorId) ? $"{record.Author}님을 팔로우했습니다." : $"{record.Author}님 팔로우를 해제했습니다.");
            };
            actions.Add(follow);
            RefreshFollowButton(follow, record.AuthorId);

            var moveSpace = TextButton("공간 이동", false);
            moveSpace.style.height = 32;
            moveSpace.style.marginRight = 7;
            moveSpace.style.marginBottom = 6;
            moveSpace.clicked += () => ShowToast($"작성자 공간으로 이동 · {record.SpaceTitle}");
            actions.Add(moveSpace);

            var report = TextButton(reportedRecordIds.Contains(record.Id) ? "신고 완료" : "신고", false);
            report.style.height = 32;
            report.style.marginRight = 7;
            report.style.marginBottom = 6;
            report.clicked += () =>
            {
                reportedRecordIds.Add(record.Id);
                report.text = "신고 완료";
                report.SetEnabled(false);
                ShowToast("피드 기록을 신고했습니다.");
            };
            report.SetEnabled(!reportedRecordIds.Contains(record.Id));
            actions.Add(report);

            var block = TextButton(blockedAuthorIds.Contains(record.AuthorId) ? "차단됨" : "차단", false);
            block.style.height = 32;
            block.style.marginBottom = 6;
            block.clicked += () =>
            {
                blockedAuthorIds.Add(record.AuthorId);
                block.text = "차단됨";
                block.SetEnabled(false);
                ShowToast($"{record.Author}님을 차단했습니다.");
            };
            block.SetEnabled(!blockedAuthorIds.Contains(record.AuthorId));
            actions.Add(block);

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
            scrollRail.pickingMode = PickingMode.Ignore;
            scrollRail.style.position = Position.Absolute;
            scrollRail.style.right = 4;
            scrollRail.style.top = 72;
            scrollRail.style.bottom = 20;
            scrollRail.style.width = 5;
            scrollRail.style.backgroundColor = Rgba(0x58, 0xB9, 0xFF, 0.18f);
            Round(scrollRail, 3);
            parent.Add(scrollRail);

            scrollThumb = new VisualElement { name = "notebook-scroll-thumb" };
            scrollThumb.pickingMode = PickingMode.Ignore;
            scrollThumb.style.position = Position.Absolute;
            scrollThumb.style.left = 0;
            scrollThumb.style.right = 0;
            scrollThumb.style.top = 4;
            scrollThumb.style.height = 64;
            scrollThumb.style.backgroundColor = Rgba(0x58, 0xB9, 0xFF, 0.78f);
            Round(scrollThumb, 3);
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

        private void ScrollRecommendBy(float delta)
        {
            if (recommendScrollView == null)
            {
                return;
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
            var max = Mathf.Max(0f, contentWidth - viewWidth);
            var offset = recommendScrollView.scrollOffset;
            offset.x = Mathf.Clamp(offset.x + delta, 0f, max);
            recommendScrollView.scrollOffset = offset;
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
            var railHeight = Mathf.Max(scrollRail.resolvedStyle.height, scrollRail.layout.height, 1f);
            var contentHeight = Mathf.Max(pageScrollView.contentContainer.resolvedStyle.height, 1040f);
            var viewHeight = Mathf.Max(pageScrollView.resolvedStyle.height, 1f);
            var thumbHeight = Mathf.Clamp(railHeight * viewHeight / contentHeight, 44f, 112f);
            var maxThumbTop = Mathf.Max(0f, railHeight - thumbHeight - 8f);
            var ratio = max <= 0f ? 0f : Mathf.Clamp01(pageScrollView.scrollOffset.y / max);

            scrollThumb.style.height = thumbHeight;
            scrollThumb.style.top = 4f + (maxThumbTop * ratio);
        }

        private void RefreshAll()
        {
            RefreshWallet();
            RefreshFilters();
            RefreshCommunityPanels();
            RefreshModeVisibility();
            RefreshLists();
            RefreshObjectMarket();
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

            if (feedContent != null)
            {
                feedContent.Clear();
                BuildFeed(feedContent);
            }
        }

        private void RefreshModeVisibility()
        {
            var bookMarket = activeHeaderTab == HeaderTabs[0];
            var otherSpaces = activeHeaderTab == HeaderTabs[1];
            var feed = activeHeaderTab == HeaderTabs[2];
            var objectMarket = activeHeaderTab == HeaderTabs[3];

            if (headerSubtitleLabel != null)
            {
                headerSubtitleLabel.text = activeHeaderTab switch
                {
                    "다른 공간" => "LAPTOP · OTHER SPACES",
                    "피드" => "LAPTOP · SOCIAL FEED",
                    "꾸미기 시장" => "LAPTOP · OBJECT MARKET",
                    _ => "LAPTOP · BOOK MARKET",
                };
            }

            if (heroPanel != null)
            {
                heroPanel.style.height = bookMarket ? 198 : 132;
            }

            if (heroEyebrowLabel != null)
            {
                heroEyebrowLabel.text = activeHeaderTab switch
                {
                    "다른 공간" => "DOTSHELF COMMUNITY SPACES",
                    "피드" => "DOTSHELF SOCIAL RECORDS",
                    "꾸미기 시장" => "DOTSHELF OBJECT MARKET",
                    _ => "DOTSHELF CURATED MARKET",
                };
            }

            if (heroTitleLabel != null)
            {
                heroTitleLabel.text = activeHeaderTab;
            }

            if (heroCopyLabel != null)
            {
                heroCopyLabel.text = activeHeaderTab switch
                {
                    "다른 공간" => "다른 사용자의 서재를 방문하고 팔로우해보세요.",
                    "피드" => "기록과 공간 활동을 빠르게 확인하고 반응할 수 있습니다.",
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
                subTabsRow.style.display = bookMarket ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (bookMarketContent != null)
            {
                bookMarketContent.style.display = bookMarket ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (otherSpacesContent != null)
            {
                otherSpacesContent.style.display = otherSpaces ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (feedContent != null)
            {
                feedContent.style.display = feed ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (objectMarketContent != null)
            {
                objectMarketContent.style.display = objectMarket ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (pageScrollView != null)
            {
                pageScrollView.contentContainer.style.minHeight = bookMarket ? 1600 : 980;
            }
        }

        private void RefreshLists()
        {
            var recommendFiltered = FilterBooks(false).ToArray();
            var rankingFiltered = FilterBooks(true).ToArray();
            var detailBook = ResolveDetailBook(rankingFiltered, recommendFiltered);

            RefreshRecommend(recommendFiltered);
            RefreshRanking(rankingFiltered);
            RefreshDetail(detailBook);
        }

        private IEnumerable<BookViewModel> FilterBooks(bool includeGenre)
        {
            var needle = query?.Trim() ?? string.Empty;
            var filtered = books
                .Where(book => MatchesSearch(book, needle))
                .Where(MatchesCategory)
                .Where(MatchesSubTab)
                .Where(book => !includeGenre || MatchesGenre(book));

            return SortBooks(filtered);
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
                "조회순" => source
                    .OrderByDescending(book => book.Views)
                    .ThenByDescending(book => book.Records)
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
            return selectedBookIndex >= 0 && selectedBookIndex < books.Length
                ? books[selectedBookIndex]
                : null;
        }

        private bool MatchesSearch(BookViewModel book, string needle)
        {
            return string.IsNullOrWhiteSpace(needle) ||
                   Contains(book.Title, needle) ||
                   Contains(book.Author, needle) ||
                   Contains(book.Publisher, needle);
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
                "추천 책" => book.Rating >= 4.7f,
                _ => true,
            };
        }

        private bool MatchesGenre(BookViewModel book)
        {
            return activeGenre == "전체" || book.Genre == activeGenre;
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

            var source = activeSubTab == "책 랭킹"
                ? ordered.Take(6)
                : ordered.Where(book => book.Rating >= 4.6f).Take(8);

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

            var action = TextButton(addedBookIds.Contains(book.Id)
                ? "책장에 있음"
                : book.Price == 0 ? "담기" : "구매", true);
            action.style.height = 32;
            action.style.width = Length.Percent(100);
            action.style.marginTop = 6;
            action.style.backgroundColor = addedBookIds.Contains(book.Id)
                ? Success
                : book.Price == 0 ? Night : AccentDeep;
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
                addButton.SetEnabled(true);
                addButton.text = alreadyAdded ? "✓ 내 책장에 있음" : "+ 내 책장에 추가";
                addButton.style.backgroundColor = alreadyAdded ? Success : Accent;
            }
        }

        private void SelectBook(BookViewModel book)
        {
            var index = Array.IndexOf(books, book);
            if (index < 0)
            {
                return;
            }

            selectedBookIndex = index;
            RefreshDetail(book);
        }

        private void ChangePage(int delta)
        {
            var totalPages = Mathf.Max(1, Mathf.CeilToInt(FilterBooks(true).Count() / (float)RankingPageSize));
            pageIndex = Mathf.Clamp(pageIndex + delta, 0, totalPages - 1);
            RefreshLists();
        }

        private void ActivateHeaderTab(string tab)
        {
            if (string.IsNullOrEmpty(tab) || activeHeaderTab == tab)
            {
                return;
            }

            activeHeaderTab = tab;
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
            }
            else
            {
                activeSearchSuggestion = suggestion;
                query = suggestion;
            }

            searchField?.SetValueWithoutNotify(query);
            pageIndex = 0;
            RefreshFilters();
            RefreshLists();
        }

        private void ClearSearch()
        {
            query = string.Empty;
            activeSearchSuggestion = string.Empty;
            searchField?.SetValueWithoutNotify(query);
            pageIndex = 0;
            RefreshFilters();
            RefreshLists();
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
                RefreshDetail(book);
                ShowToast($"이미 내 책장에 있어요 · {book.Title}");
                return;
            }

            if (book.Price > 0 && pages < book.Price)
            {
                RefreshDetail(book);
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
            ShowToast(book.Price > 0 ? $"구매 완료 · {book.Title}" : $"내 책장에 추가됨 · {book.Title}");
        }

        private BookViewModel FindBookById(string id)
        {
            return books.FirstOrDefault(book => book.Id == id);
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

        private static bool Contains(string source, string value)
        {
            return source != null &&
                   value != null &&
                   source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
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
