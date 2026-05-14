using System;
using System.Collections;
using System.Collections.Generic;
using App.Application.Dashboard;
using App.Core;
using App.Domain.Dashboard;
using App.Infrastructure.Catalogs;
using App.Infrastructure.Mock.Dashboard;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace Dotshelf.Scripts.Dashboard
{
    /// <summary>
    /// 대시보드 메인 화면. UIDocument를 호스팅하고 Mock/Api Repository를 주입한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class DashboardScreenController : MonoBehaviour
    {
        public enum ViewMode
        {
            Mine,
            Visitor
        }

        [Header("View Assets")]
        [SerializeField] private VisualTreeAsset viewAsset;
        [SerializeField] private StyleSheet stylesheet;
        [SerializeField] private PostItStyleCatalogAsset postItCatalog;

        [Tooltip("World Space에 dashboard를 띄울 때 사용할 PanelSettings. " +
                "비워두면 UIDocument의 기본 PanelSettings(보통 Screen Space Overlay) 사용. " +
                "(v1.0 dashboard 통합) MainScene에서 GuestbookBoard 화이트보드 표면에 그릴 때 World Space PanelSettings SO를 주입.")]
        [SerializeField] private UnityEngine.UIElements.PanelSettings overridePanelSettings;

        [Header("Mode")]
        [SerializeField] private ViewMode viewMode = ViewMode.Mine;
        // Default 0 (미정). 옛 default 1L 은 dev DB 에 spaceId=1 이 없어 GET /v1/spaces/1/dashboard 호출 시 404/500 유발.
        // 이제 외부에서 visitedSpaceId 를 명시 set 하지 않으면 LoadInitialData/카드 클릭 콜백이 dashboard 호출 자체를 skip.
        [SerializeField] private long visitedSpaceId;
        // prefab 의 직렬화 default 는 true 지만 SceneInstaller.Configure(... isMockMode) 가 실제 주입된 repo 에 맞춰
        // runtime 에 덮어쓴다. OnVisitUser/LoadInitialData 의 real-API mode 가드는 이 필드 (post-Configure) 에 의존.
        [SerializeField] private bool useMockRepositories = true;

        // dashboard fetch 발사 출처 — 같은 호출이 어디서 나왔는지 다음 사고 시 로그에서 즉시 식별하기 위함.
        // /v1/spaces/1/dashboard 가 다시 찍히면 source 가 어떤 값인지로 ingress 경로를 좁힐 수 있다.
        private enum DashboardCallSource
        {
            MySpace,           // ExecuteForMine — /v1/spaces/me 응답 id 로 fetch (source of truth)
            VisitedExplicit,   // SerializeField visitedSpaceId 로 fetch (LoadInitialData)
            UserClick,         // user list 카드 클릭 → OnVisitUser
            DashboardEditRefresh // 편집 모달 저장 후 본인 dashboard 재로드
        }

        // PlayerPrefs 의 legacy visitedSpaceId 키들을 한 번만 청소하기 위한 1-shot guard.
        // 이번 빌드 부터는 visitedSpaceId 를 영속화하지 않으므로 과거 빌드가 남긴 key 가 살아 있을 가능성만 차단.
        private static bool legacyDashboardPrefsCleaned;

        private UIDocument document;

        private const int CarouselItemLimit = 10;
        private const int MaxRemoteBookCoverWidth = 2048;
        private const int MaxRemoteBookCoverHeight = 3072;

        private bool configured;

        private LoadDashboardUseCase loadDashboardUseCase;
        private ToggleSpaceLikeUseCase toggleLikeUseCase;
        private LoadGuestbookPageUseCase loadGuestbookPageUseCase;
        private LoadOwnedBooksPageUseCase loadOwnedBooksUseCase;
        private LoadReadingRecordsPageUseCase loadReadingRecordsUseCase;
        private LoadFollowersPageUseCase loadFollowersUseCase;
        private LoadFollowingsPageUseCase loadFollowingsUseCase;
        private FollowUserUseCase followUseCase;
        private UnfollowUserUseCase unfollowUseCase;
        private BlockUserUseCase blockUseCase;
        private WriteGuestbookUseCase writeGuestbookUseCase;
        private WriteGuestbookReplyUseCase writeGuestbookReplyUseCase;
        private DeleteGuestbookUseCase deleteGuestbookUseCase;
        private DeleteGuestbookReplyUseCase deleteGuestbookReplyUseCase;
        private ReportGuestbookUseCase reportGuestbookUseCase;
        private UpdateIntroductionUseCase updateIntroductionUseCase;
        private UpdateGuestbookVisibilityUseCase updateGuestbookVisibilityUseCase;
        private UpdateGuestbookPostItWhitelistUseCase updateWhitelistUseCase;

        private VisualElement root;
        private Label nicknameLabel;
        private Label introductionLabel;
        private Button followingCountButton;
        private Button followersCountButton;
        private Button likeToggleButton;
        private Label likeCountLabel;
        private Label bookCountLabel;
        private Label recordCountLabel;
        private Label guestbookCountLabel;
        private Label weeklyVisitorLabel;
        private VisualElement guestbookGrid;
        private Button guestbookPrevButton;
        private Button guestbookNextButton;
        private Label guestbookPageLabel;
        private Button editButton;
        private Button writeGuestbookButton;
        private Button returnMineButton;
        private ScrollView booksCarousel;
        private ScrollView recordsCarousel;

        private RemoteImageLoader coverImageLoader;

        private VisualElement userListModalRoot;
        private VisualElement userListModalBackdrop;
        private VisualElement userListModalList;
        private Label userListModalTitle;
        private Button userListModalCloseButton;
        private Label userListModalEmpty;

        private VisualElement guestbookWriteModalRoot;
        private TextField guestbookWriteContent;
        private Label guestbookWriteCounter;
        private ScrollView guestbookWriteStyleList;
        private Toggle guestbookWriteAnonymousToggle;
        private Button guestbookWriteCloseButton;
        private Button guestbookWriteCancelButton;
        private Button guestbookWriteSubmitButton;
        private long selectedPostItStyleId;
        private VisualElement selectedPostItStyleCell;

        // PostItStyleCatalogAsset → IPostItStyleCatalog 어댑터 (lazy init).
        private IPostItStyleCatalog postItStyleCatalog;

        private VisualElement guestbookDetailModalRoot;
        private VisualElement guestbookDetailCard;
        private Label guestbookDetailContentText;
        private Label guestbookDetailMeta;
        private Button guestbookDetailCloseButton;
        private Button guestbookDetailReportButton;
        private Button guestbookDetailDeleteButton;
        private VisualElement guestbookDetailReplyExisting;
        private Label guestbookDetailReplyContent;
        private Label guestbookDetailReplyMeta;
        private Button guestbookDetailReplyDeleteButton;
        private VisualElement guestbookDetailReplyForm;
        private TextField guestbookDetailReplyInput;
        private Label guestbookDetailReplyCounter;
        private ScrollView guestbookDetailReplyStyleList;
        private Button guestbookDetailReplySubmitButton;
        private long selectedReplyPostItStyleId;
        private VisualElement selectedReplyPostItStyleCell;
        private Guestbook currentDetailGuestbook;

        // Mock 환경에서 본인 user id를 1로 가정. Api 합류 시 SessionService에서 받아 주입 예정.
        private const long MockSelfUserId = 1L;

        private Button ownedBooksViewAllButton;
        private VisualElement ownedBooksModalRoot;
        private VisualElement ownedBooksGrid;
        private Button ownedBooksCloseButton;
        private Button ownedBooksLoadMoreButton;
        private Button ownedBooksSortAddedDescButton;
        private Button ownedBooksSortPublishedDescButton;
        private Button ownedBooksSortPublishedAscButton;
        private Button ownedBooksSortTitleButton;
        private OwnedBookSort currentOwnedBooksSort = OwnedBookSort.AddedDesc;
        private string ownedBooksNextCursor;

        private Button readingRecordsViewAllButton;
        private VisualElement readingRecordsModalRoot;
        private VisualElement readingRecordsList;
        private Button readingRecordsCloseButton;
        private Button readingRecordsLoadMoreButton;
        private Button recordsSortLatestButton;
        private Button recordsSortOldestButton;
        private Button recordsSortMostLikedButton;
        private Button recordsSortMostViewedButton;
        private Button recordsSortMostCommentedButton;
        private ReadingRecordSort currentRecordsSort = ReadingRecordSort.Latest;
        private string readingRecordsNextCursor;

        private VisualElement dashboardEditModalRoot;
        private TextField dashboardEditIntroduction;
        private Button visibilityPublicButton;
        private Button visibilityFollowersButton;
        private Button visibilityPrivateButton;
        private Button dashboardEditCloseButton;
        private Button dashboardEditCancelButton;
        private Button dashboardEditSubmitButton;
        private VisibilityScope selectedVisibility;

        private enum UserListMode
        {
            Followers,
            Followings
        }

        private UserListMode currentUserListMode;

        private DashboardSnapshot currentSnapshot;
        private bool likeRequestInFlight;
        private bool interactionEnabled = true;
        private readonly List<string> guestbookCursorHistory = new List<string> { null };
        private int currentGuestbookPageIndex;
        private string nextGuestbookCursor;

        // ====== Cached event handlers (Awake에서 1회 인스턴스화 → WireEvents/UnwireEvents가 정확한 인스턴스로 register/unregister) ======

        // 람다 핸들러 (인자 캡처 있어 메서드 그룹으로 못 쓰는 것)
        private Action onOwnedBooksLoadMoreHandler;
        private Action onOwnedBooksSortAddedDescHandler;
        private Action onOwnedBooksSortPublishedDescHandler;
        private Action onOwnedBooksSortPublishedAscHandler;
        private Action onOwnedBooksSortTitleHandler;
        private Action onReadingRecordsLoadMoreHandler;
        private Action onRecordsSortLatestHandler;
        private Action onRecordsSortOldestHandler;
        private Action onRecordsSortMostLikedHandler;
        private Action onRecordsSortMostViewedHandler;
        private Action onRecordsSortMostCommentedHandler;
        private Action onVisibilityPublicHandler;
        private Action onVisibilityFollowersHandler;
        private Action onVisibilityPrivateHandler;

        // EventCallback 핸들러 (RegisterCallback / RegisterValueChangedCallback 용)
        private EventCallback<ClickEvent> onUserListModalBackdropHandler;
        private EventCallback<ChangeEvent<string>> onWriteContentChangedHandler;
        private EventCallback<ChangeEvent<string>> onReplyContentChangedHandler;

        // OnEnable cycle 중복 등록 방지
        private bool eventsWired;

        // 캐러셀 드래그 스크롤 — 캐러셀별 state + 핸들러 보관
        private sealed class CarouselDragState
        {
            public bool dragging;
            public Vector2 startMouse;
            public Vector2 startOffset;
            public int activePointerId = -1;
            public EventCallback<PointerDownEvent> onDown;
            public EventCallback<PointerMoveEvent> onMove;
            public EventCallback<PointerUpEvent> onUp;
        }

        private readonly CarouselDragState booksCarouselDrag = new CarouselDragState();
        private readonly CarouselDragState recordsCarouselDrag = new CarouselDragState();

        private void Awake()
        {
            coverImageLoader = new RemoteImageLoader(maxCacheSize: 32);
            EnsureDocument();
            if (document == null)
            {
                Debug.LogError("[Dashboard] UIDocument component is missing.");
                return;
            }

            if (viewAsset != null)
            {
                document.visualTreeAsset = viewAsset;
            }
            if (overridePanelSettings != null)
            {
                // (v1.0 dashboard 통합) MainScene에서 World Space PanelSettings 주입 시 적용.
                // 비워두면 testbed 씬의 기본 PanelSettings(Screen Space Overlay) 그대로.
                document.panelSettings = overridePanelSettings;
            }
            CacheEventHandlers();
            // Mock 빌드는 OnEnable에서 fallback으로. Configure가 호출됐으면 그걸 우선.
        }

        private void CacheEventHandlers()
        {
            onOwnedBooksLoadMoreHandler = () => LoadOwnedBooksPage(replace: false);
            onOwnedBooksSortAddedDescHandler = () => OnOwnedBooksSortChanged(OwnedBookSort.AddedDesc);
            onOwnedBooksSortPublishedDescHandler = () => OnOwnedBooksSortChanged(OwnedBookSort.PublishedDesc);
            onOwnedBooksSortPublishedAscHandler = () => OnOwnedBooksSortChanged(OwnedBookSort.PublishedAsc);
            onOwnedBooksSortTitleHandler = () => OnOwnedBooksSortChanged(OwnedBookSort.TitleAsc);

            onReadingRecordsLoadMoreHandler = () => LoadReadingRecordsPage(replace: false);
            onRecordsSortLatestHandler = () => OnRecordsSortChanged(ReadingRecordSort.Latest);
            onRecordsSortOldestHandler = () => OnRecordsSortChanged(ReadingRecordSort.Oldest);
            onRecordsSortMostLikedHandler = () => OnRecordsSortChanged(ReadingRecordSort.MostLiked);
            onRecordsSortMostViewedHandler = () => OnRecordsSortChanged(ReadingRecordSort.MostViewed);
            onRecordsSortMostCommentedHandler = () => OnRecordsSortChanged(ReadingRecordSort.MostCommented);

            onVisibilityPublicHandler = () => OnVisibilitySelected(VisibilityScope.Public);
            onVisibilityFollowersHandler = () => OnVisibilitySelected(VisibilityScope.FollowersOnly);
            onVisibilityPrivateHandler = () => OnVisibilitySelected(VisibilityScope.Private);

            onUserListModalBackdropHandler = _ => CloseUserListModal();
            onWriteContentChangedHandler = evt =>
            {
                int len = evt.newValue?.Length ?? 0;
                if (guestbookWriteCounter != null) guestbookWriteCounter.text = $"{len} / 30";
            };
            onReplyContentChangedHandler = evt =>
            {
                int len = evt.newValue?.Length ?? 0;
                if (guestbookDetailReplyCounter != null) guestbookDetailReplyCounter.text = $"{len} / 30";
            };
        }

        private void EnsureEventHandlersCached()
        {
            if (onOwnedBooksLoadMoreHandler != null &&
                onUserListModalBackdropHandler != null &&
                onWriteContentChangedHandler != null)
            {
                return;
            }

            CacheEventHandlers();
        }

        private void EnsureDocument()
        {
            if (document == null)
            {
                document = GetComponent<UIDocument>();
            }
        }

        private const string DimmedClass = "dashboard-root--dimmed";
        private const string InteractiveClass = "dashboard-root--interactive";
        // focus 진입 전에도 보이게(사용자 D2 요청) — full로 시작하고 BookshelfExperienceManager가
        // focus 해제 시점에 SetDimmed(true) 호출. 외부 panel 모드 활성 시 항상 visible.
        private bool dimmed = false;

        public void SetDimmed(bool isDimmed)
        {
            if (dimmed == isDimmed) return;
            dimmed = isDimmed;
            ApplyDimmedClass();
        }

        private void ApplyDimmedClass()
        {
            if (root == null) return;
            if (dimmed) root.AddToClassList(DimmedClass);
            else root.RemoveFromClassList(DimmedClass);
        }

        public bool InteractionEnabled => interactionEnabled;

        public void SetInteractionEnabled(bool enabled)
        {
            if (interactionEnabled == enabled)
            {
                ApplyInteractionState();
                return;
            }

            interactionEnabled = enabled;
            ApplyInteractionState();
        }

        private void ApplyInteractionState()
        {
            if (root == null) return;
            root.SetEnabled(interactionEnabled);
            if (interactionEnabled) root.AddToClassList(InteractiveClass);
            else root.RemoveFromClassList(InteractiveClass);
        }

        public void ShowMineDashboard()
        {
            if (viewMode == ViewMode.Mine && visitedSpaceId == 0L)
            {
                return;
            }

            viewMode = ViewMode.Mine;
            visitedSpaceId = 0L;
            ApplyViewMode();
            ReloadDashboardIfReady();
        }

        public void ShowVisitedSpaceDashboard(long spaceId)
        {
            if (spaceId <= 0)
            {
                return;
            }

            if (viewMode == ViewMode.Visitor && visitedSpaceId == spaceId)
            {
                return;
            }

            viewMode = ViewMode.Visitor;
            visitedSpaceId = spaceId;
            ApplyViewMode();
            ReloadDashboardIfReady();
        }

        private void ReloadDashboardIfReady()
        {
            if (!Application.isPlaying ||
                !isActiveAndEnabled ||
                root == null ||
                !configured ||
                !HasConfiguredUseCases())
            {
                return;
            }

            LoadInitialData();
        }

        /// <summary>
        /// 전체 대시보드를 새로고침한다. 외부 호출용 (새로고침 버튼, 이벤트 핸들러 등).
        /// </summary>
        public void RefreshAll()
        {
            ReloadDashboardIfReady();
        }

        /// <summary>
        /// 최근 추가한 책 캐러셀만 새로고침한다. 책 추가 이벤트 핸들러용.
        /// </summary>
        public void RefreshBooks()
        {
            if (!configured || !HasConfiguredUseCases())
            {
                return;
            }

            LoadRecentBooks();
        }

        /// <summary>
        /// 최근 작성한 독후감 캐러셀만 새로고침한다. 독후감 작성 이벤트 핸들러용.
        /// </summary>
        public void RefreshRecords()
        {
            if (!configured || !HasConfiguredUseCases())
            {
                return;
            }

            LoadRecentRecords();
        }

        public void Configure(
            IDashboardRepository dashboardRepo,
            IGuestbookRepository guestbookRepo,
            ISpaceSettingsRepository spaceSettingsRepo,
            IFollowRepository followRepo,
            ILibraryViewRepository libraryRepo)
        {
            // 호환 유지용 — isMockMode 미지정 시 prefab field 그대로 사용. (SceneInstaller 는 항상 새 overload 사용 권장.)
            Configure(dashboardRepo, guestbookRepo, spaceSettingsRepo, followRepo, libraryRepo, useMockRepositories);
        }

        // SceneInstaller 가 effectiveMode (Mock/Api) 와 함께 호출하는 경로.
        // useMockRepositories 필드를 주입된 repo 와 동기화해서 OnVisitUser 등의 real-API 가드가 정확히 동작하게 한다.
        // (MR #114 이후에도 필드가 prefab default(true) 로 남아 있어 real-API 모드인데도 mock 분기로 빠지는 버그가 있었다.)
        public void Configure(
            IDashboardRepository dashboardRepo,
            IGuestbookRepository guestbookRepo,
            ISpaceSettingsRepository spaceSettingsRepo,
            IFollowRepository followRepo,
            ILibraryViewRepository libraryRepo,
            bool isMockMode)
        {
            useMockRepositories = isMockMode;
            BuildUseCasesFromRepos(dashboardRepo, guestbookRepo, spaceSettingsRepo, followRepo, libraryRepo);
            configured = true;

            if (Application.isPlaying && isActiveAndEnabled && root != null)
            {
                ApplyViewMode();
                LoadInitialData();
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureDocument();
            if (document == null)
            {
                Debug.LogError("[Dashboard] UIDocument component is missing.");
                return;
            }

            if (!configured || !HasConfiguredUseCases())
            {
                BuildMockUseCases();
                configured = true;
            }

            root = document.rootVisualElement;
            if (root == null)
            {
                Debug.LogWarning("[Dashboard] UIDocument rootVisualElement is null.");
                return;
            }

            DashboardRaycastFilter.Register(this);

            if (stylesheet != null && !root.styleSheets.Contains(stylesheet))
            {
                root.styleSheets.Add(stylesheet);
            }

            ApplyDimmedClass();
            ApplyInteractionState();

            QueryUI();
            if (!eventsWired)
            {
                WireEvents();
                eventsWired = true;
            }
            ApplyViewMode();
            LoadInitialData();
        }

        private void OnDisable()
        {
            DashboardRaycastFilter.Unregister(this);

            if (eventsWired)
            {
                UnwireEvents();
                eventsWired = false;
            }

            coverImageLoader?.Dispose();
        }

        private void BuildMockUseCases()
        {
            useMockRepositories = true;
            // SceneInstaller가 없는 경우의 fallback. Mock으로 단독 동작.
            BuildUseCasesFromRepos(
                new MockDashboardRepository(),
                new MockGuestbookRepository(),
                new MockSpaceSettingsRepository(),
                new MockFollowRepository(),
                new MockLibraryViewRepository());
        }

        private bool HasConfiguredUseCases()
        {
            return loadDashboardUseCase != null &&
                   toggleLikeUseCase != null &&
                   loadGuestbookPageUseCase != null &&
                   loadOwnedBooksUseCase != null &&
                   loadReadingRecordsUseCase != null &&
                   loadFollowersUseCase != null &&
                   loadFollowingsUseCase != null &&
                   followUseCase != null &&
                   unfollowUseCase != null &&
                   blockUseCase != null &&
                   writeGuestbookUseCase != null &&
                   writeGuestbookReplyUseCase != null &&
                   deleteGuestbookUseCase != null &&
                   deleteGuestbookReplyUseCase != null &&
                   reportGuestbookUseCase != null &&
                   updateIntroductionUseCase != null &&
                   updateGuestbookVisibilityUseCase != null &&
                   updateWhitelistUseCase != null;
        }

        private void BuildUseCasesFromRepos(
            IDashboardRepository dashboardRepo,
            IGuestbookRepository guestbookRepo,
            ISpaceSettingsRepository spaceSettingsRepo,
            IFollowRepository followRepo,
            ILibraryViewRepository libraryRepo)
        {
            loadDashboardUseCase = new LoadDashboardUseCase(dashboardRepo);
            toggleLikeUseCase = new ToggleSpaceLikeUseCase(dashboardRepo);
            loadGuestbookPageUseCase = new LoadGuestbookPageUseCase(guestbookRepo);
            loadOwnedBooksUseCase = new LoadOwnedBooksPageUseCase(libraryRepo);
            loadReadingRecordsUseCase = new LoadReadingRecordsPageUseCase(libraryRepo);
            loadFollowersUseCase = new LoadFollowersPageUseCase(followRepo);
            loadFollowingsUseCase = new LoadFollowingsPageUseCase(followRepo);
            followUseCase = new FollowUserUseCase(followRepo);
            unfollowUseCase = new UnfollowUserUseCase(followRepo);
            blockUseCase = new BlockUserUseCase(followRepo);
            writeGuestbookUseCase = new WriteGuestbookUseCase(guestbookRepo);
            writeGuestbookReplyUseCase = new WriteGuestbookReplyUseCase(guestbookRepo);
            deleteGuestbookUseCase = new DeleteGuestbookUseCase(guestbookRepo);
            deleteGuestbookReplyUseCase = new DeleteGuestbookReplyUseCase(guestbookRepo);
            reportGuestbookUseCase = new ReportGuestbookUseCase(guestbookRepo);
            updateIntroductionUseCase = new UpdateIntroductionUseCase(spaceSettingsRepo);
            updateGuestbookVisibilityUseCase = new UpdateGuestbookVisibilityUseCase(spaceSettingsRepo);
            updateWhitelistUseCase = new UpdateGuestbookPostItWhitelistUseCase(spaceSettingsRepo);
        }

        private void QueryUI()
        {
            nicknameLabel = root.Q<Label>("nickname-label");
            introductionLabel = root.Q<Label>("introduction-label");
            followingCountButton = root.Q<Button>("following-count-label");
            followersCountButton = root.Q<Button>("followers-count-label");
            likeToggleButton = root.Q<Button>("like-toggle-button");
            likeCountLabel = root.Q<Label>("like-count-label");
            bookCountLabel = root.Q<Label>("book-count-label");
            recordCountLabel = root.Q<Label>("record-count-label");
            guestbookCountLabel = root.Q<Label>("guestbook-count-label");
            weeklyVisitorLabel = root.Q<Label>("weekly-visitor-label");
            guestbookGrid = root.Q<VisualElement>("guestbook-grid");
            guestbookPrevButton = root.Q<Button>("guestbook-prev-button");
            guestbookNextButton = root.Q<Button>("guestbook-next-button");
            guestbookPageLabel = root.Q<Label>("guestbook-page-label");
            editButton = root.Q<Button>("edit-dashboard-button");
            writeGuestbookButton = root.Q<Button>("write-guestbook-button");
            returnMineButton = root.Q<Button>("return-mine-button");
            booksCarousel = root.Q<ScrollView>("recent-books-carousel");
            recordsCarousel = root.Q<ScrollView>("recent-records-carousel");

            userListModalRoot = root.Q<VisualElement>("user-list-modal");
            userListModalBackdrop = root.Q<VisualElement>("user-list-modal-backdrop");
            userListModalList = root.Q<VisualElement>("user-list-modal-list");
            userListModalTitle = root.Q<Label>("user-list-modal-title");
            userListModalCloseButton = root.Q<Button>("user-list-modal-close-button");
            userListModalEmpty = root.Q<Label>("user-list-modal-empty");

            guestbookWriteModalRoot = root.Q<VisualElement>("guestbook-write-modal");
            guestbookWriteContent = root.Q<TextField>("guestbook-write-content");
            guestbookWriteCounter = root.Q<Label>("guestbook-write-counter");
            guestbookWriteStyleList = root.Q<ScrollView>("guestbook-write-style-list");
            guestbookWriteAnonymousToggle = root.Q<Toggle>("guestbook-write-anonymous-toggle");
            guestbookWriteCloseButton = root.Q<Button>("guestbook-write-close-button");
            guestbookWriteCancelButton = root.Q<Button>("guestbook-write-cancel-button");
            guestbookWriteSubmitButton = root.Q<Button>("guestbook-write-submit-button");

            guestbookDetailModalRoot = root.Q<VisualElement>("guestbook-detail-modal");
            guestbookDetailCard = root.Q<VisualElement>("guestbook-detail-card");
            guestbookDetailContentText = root.Q<Label>("guestbook-detail-content-text");
            guestbookDetailMeta = root.Q<Label>("guestbook-detail-meta");
            guestbookDetailCloseButton = root.Q<Button>("guestbook-detail-close-button");
            guestbookDetailReportButton = root.Q<Button>("guestbook-detail-report-button");
            guestbookDetailDeleteButton = root.Q<Button>("guestbook-detail-delete-button");
            guestbookDetailReplyExisting = root.Q<VisualElement>("guestbook-detail-reply-existing");
            guestbookDetailReplyContent = root.Q<Label>("guestbook-detail-reply-content");
            guestbookDetailReplyMeta = root.Q<Label>("guestbook-detail-reply-meta");
            guestbookDetailReplyDeleteButton = root.Q<Button>("guestbook-detail-reply-delete-button");
            guestbookDetailReplyForm = root.Q<VisualElement>("guestbook-detail-reply-form");
            guestbookDetailReplyInput = root.Q<TextField>("guestbook-detail-reply-input");
            guestbookDetailReplyCounter = root.Q<Label>("guestbook-detail-reply-counter");
            guestbookDetailReplyStyleList = root.Q<ScrollView>("guestbook-detail-reply-style-list");
            guestbookDetailReplySubmitButton = root.Q<Button>("guestbook-detail-reply-submit-button");

            ownedBooksViewAllButton = root.Q<Button>("recent-books-view-all-button");
            ownedBooksModalRoot = root.Q<VisualElement>("owned-books-modal");
            ownedBooksGrid = root.Q<VisualElement>("owned-books-grid");
            ownedBooksCloseButton = root.Q<Button>("owned-books-close-button");
            ownedBooksLoadMoreButton = root.Q<Button>("owned-books-load-more-button");
            ownedBooksSortAddedDescButton = root.Q<Button>("owned-books-sort-added-desc");
            ownedBooksSortPublishedDescButton = root.Q<Button>("owned-books-sort-published-desc");
            ownedBooksSortPublishedAscButton = root.Q<Button>("owned-books-sort-published-asc");
            ownedBooksSortTitleButton = root.Q<Button>("owned-books-sort-title");

            readingRecordsViewAllButton = root.Q<Button>("recent-records-view-all-button");
            readingRecordsModalRoot = root.Q<VisualElement>("reading-records-modal");
            readingRecordsList = root.Q<VisualElement>("reading-records-list");
            readingRecordsCloseButton = root.Q<Button>("reading-records-close-button");
            readingRecordsLoadMoreButton = root.Q<Button>("reading-records-load-more-button");
            recordsSortLatestButton = root.Q<Button>("records-sort-latest");
            recordsSortOldestButton = root.Q<Button>("records-sort-oldest");
            recordsSortMostLikedButton = root.Q<Button>("records-sort-most-liked");
            recordsSortMostViewedButton = root.Q<Button>("records-sort-most-viewed");
            // (MR #61 리뷰 #8) v1.0 시점에 ReadingRecord.viewCount 증가 로직 미합류 — 모든 record viewCount=0이라
            // MOST_VIEWED 정렬은 secondary tiebreaker(id DESC)만 작동해 LATEST와 동일하게 보임.
            // 사용자가 정렬 버그로 오인하는 것 방지 위해 옵션 비활성화. 증가 로직 follow-up 합류 시 SetEnabled(true).
            if (recordsSortMostViewedButton != null)
            {
                recordsSortMostViewedButton.SetEnabled(false);
                recordsSortMostViewedButton.tooltip = "조회수 집계 준비 중";
            }
            recordsSortMostCommentedButton = root.Q<Button>("records-sort-most-commented");

            dashboardEditModalRoot = root.Q<VisualElement>("dashboard-edit-modal");
            dashboardEditIntroduction = root.Q<TextField>("dashboard-edit-introduction");
            visibilityPublicButton = root.Q<Button>("visibility-public");
            visibilityFollowersButton = root.Q<Button>("visibility-followers");
            visibilityPrivateButton = root.Q<Button>("visibility-private");
            dashboardEditCloseButton = root.Q<Button>("dashboard-edit-close-button");
            dashboardEditCancelButton = root.Q<Button>("dashboard-edit-cancel-button");
            dashboardEditSubmitButton = root.Q<Button>("dashboard-edit-submit-button");
        }

        private void WireEvents()
        {
            EnsureEventHandlersCached();

            // 메서드 그룹 (인자 없는 무인자) — clicked += MethodName 패턴
            if (likeToggleButton != null) likeToggleButton.clicked += OnLikeButtonClicked;
            if (guestbookPrevButton != null) guestbookPrevButton.clicked += OnGuestbookPrev;
            if (guestbookNextButton != null) guestbookNextButton.clicked += OnGuestbookNext;
            if (editButton != null) editButton.clicked += OnEditClicked;
            if (writeGuestbookButton != null) writeGuestbookButton.clicked += OnWriteGuestbookClicked;
            if (returnMineButton != null) returnMineButton.clicked += OnReturnMineClicked;
            if (followersCountButton != null) followersCountButton.clicked += OnFollowersClicked;
            if (followingCountButton != null) followingCountButton.clicked += OnFollowingsClicked;
            if (userListModalCloseButton != null) userListModalCloseButton.clicked += CloseUserListModal;
            if (userListModalBackdrop != null) userListModalBackdrop.RegisterCallback(onUserListModalBackdropHandler);

            if (guestbookWriteCloseButton != null) guestbookWriteCloseButton.clicked += CloseGuestbookWriteModal;
            if (guestbookWriteCancelButton != null) guestbookWriteCancelButton.clicked += CloseGuestbookWriteModal;
            if (guestbookWriteSubmitButton != null) guestbookWriteSubmitButton.clicked += OnSubmitGuestbookWrite;
            if (guestbookWriteContent != null) guestbookWriteContent.RegisterValueChangedCallback(onWriteContentChangedHandler);

            if (guestbookDetailCloseButton != null) guestbookDetailCloseButton.clicked += CloseGuestbookDetailModal;
            if (guestbookDetailReportButton != null) guestbookDetailReportButton.clicked += OnReportGuestbook;
            if (guestbookDetailDeleteButton != null) guestbookDetailDeleteButton.clicked += OnDeleteGuestbook;
            if (guestbookDetailReplyDeleteButton != null) guestbookDetailReplyDeleteButton.clicked += OnDeleteGuestbookReply;
            if (guestbookDetailReplySubmitButton != null) guestbookDetailReplySubmitButton.clicked += OnSubmitGuestbookReply;
            if (guestbookDetailReplyInput != null) guestbookDetailReplyInput.RegisterValueChangedCallback(onReplyContentChangedHandler);

            if (ownedBooksViewAllButton != null) ownedBooksViewAllButton.clicked += OpenOwnedBooksModal;
            if (ownedBooksCloseButton != null) ownedBooksCloseButton.clicked += CloseOwnedBooksModal;
            if (ownedBooksLoadMoreButton != null) ownedBooksLoadMoreButton.clicked += onOwnedBooksLoadMoreHandler;
            if (ownedBooksSortAddedDescButton != null) ownedBooksSortAddedDescButton.clicked += onOwnedBooksSortAddedDescHandler;
            if (ownedBooksSortPublishedDescButton != null) ownedBooksSortPublishedDescButton.clicked += onOwnedBooksSortPublishedDescHandler;
            if (ownedBooksSortPublishedAscButton != null) ownedBooksSortPublishedAscButton.clicked += onOwnedBooksSortPublishedAscHandler;
            if (ownedBooksSortTitleButton != null) ownedBooksSortTitleButton.clicked += onOwnedBooksSortTitleHandler;

            if (readingRecordsViewAllButton != null) readingRecordsViewAllButton.clicked += OpenReadingRecordsModal;
            if (readingRecordsCloseButton != null) readingRecordsCloseButton.clicked += CloseReadingRecordsModal;
            if (readingRecordsLoadMoreButton != null) readingRecordsLoadMoreButton.clicked += onReadingRecordsLoadMoreHandler;
            if (recordsSortLatestButton != null) recordsSortLatestButton.clicked += onRecordsSortLatestHandler;
            if (recordsSortOldestButton != null) recordsSortOldestButton.clicked += onRecordsSortOldestHandler;
            if (recordsSortMostLikedButton != null) recordsSortMostLikedButton.clicked += onRecordsSortMostLikedHandler;
            if (recordsSortMostViewedButton != null) recordsSortMostViewedButton.clicked += onRecordsSortMostViewedHandler;
            if (recordsSortMostCommentedButton != null) recordsSortMostCommentedButton.clicked += onRecordsSortMostCommentedHandler;

            if (dashboardEditCloseButton != null) dashboardEditCloseButton.clicked += CloseDashboardEditModal;
            if (dashboardEditCancelButton != null) dashboardEditCancelButton.clicked += CloseDashboardEditModal;
            if (dashboardEditSubmitButton != null) dashboardEditSubmitButton.clicked += OnSubmitDashboardEdit;
            if (visibilityPublicButton != null) visibilityPublicButton.clicked += onVisibilityPublicHandler;
            if (visibilityFollowersButton != null) visibilityFollowersButton.clicked += onVisibilityFollowersHandler;
            if (visibilityPrivateButton != null) visibilityPrivateButton.clicked += onVisibilityPrivateHandler;
            // 캐러셀 드래그 스크롤
            if (booksCarousel != null) InstallCarouselDrag(booksCarousel, booksCarouselDrag);
            if (recordsCarousel != null) InstallCarouselDrag(recordsCarousel, recordsCarouselDrag);
        }

        private void UnwireEvents()
        {
            // WireEvents의 거울. 등록한 핸들러 인스턴스 그대로 사용해서 정확히 unregister.
            if (likeToggleButton != null) likeToggleButton.clicked -= OnLikeButtonClicked;
            if (guestbookPrevButton != null) guestbookPrevButton.clicked -= OnGuestbookPrev;
            if (guestbookNextButton != null) guestbookNextButton.clicked -= OnGuestbookNext;
            if (editButton != null) editButton.clicked -= OnEditClicked;
            if (writeGuestbookButton != null) writeGuestbookButton.clicked -= OnWriteGuestbookClicked;
            if (returnMineButton != null) returnMineButton.clicked -= OnReturnMineClicked;
            if (followersCountButton != null) followersCountButton.clicked -= OnFollowersClicked;
            if (followingCountButton != null) followingCountButton.clicked -= OnFollowingsClicked;
            if (userListModalCloseButton != null) userListModalCloseButton.clicked -= CloseUserListModal;
            if (userListModalBackdrop != null && onUserListModalBackdropHandler != null)
                userListModalBackdrop.UnregisterCallback(onUserListModalBackdropHandler);

            if (guestbookWriteCloseButton != null) guestbookWriteCloseButton.clicked -= CloseGuestbookWriteModal;
            if (guestbookWriteCancelButton != null) guestbookWriteCancelButton.clicked -= CloseGuestbookWriteModal;
            if (guestbookWriteSubmitButton != null) guestbookWriteSubmitButton.clicked -= OnSubmitGuestbookWrite;
            if (guestbookWriteContent != null && onWriteContentChangedHandler != null)
                guestbookWriteContent.UnregisterValueChangedCallback(onWriteContentChangedHandler);

            if (guestbookDetailCloseButton != null) guestbookDetailCloseButton.clicked -= CloseGuestbookDetailModal;
            if (guestbookDetailReportButton != null) guestbookDetailReportButton.clicked -= OnReportGuestbook;
            if (guestbookDetailDeleteButton != null) guestbookDetailDeleteButton.clicked -= OnDeleteGuestbook;
            if (guestbookDetailReplyDeleteButton != null) guestbookDetailReplyDeleteButton.clicked -= OnDeleteGuestbookReply;
            if (guestbookDetailReplySubmitButton != null) guestbookDetailReplySubmitButton.clicked -= OnSubmitGuestbookReply;
            if (guestbookDetailReplyInput != null && onReplyContentChangedHandler != null)
                guestbookDetailReplyInput.UnregisterValueChangedCallback(onReplyContentChangedHandler);

            if (ownedBooksViewAllButton != null) ownedBooksViewAllButton.clicked -= OpenOwnedBooksModal;
            if (ownedBooksCloseButton != null) ownedBooksCloseButton.clicked -= CloseOwnedBooksModal;
            if (ownedBooksLoadMoreButton != null) ownedBooksLoadMoreButton.clicked -= onOwnedBooksLoadMoreHandler;
            if (ownedBooksSortAddedDescButton != null) ownedBooksSortAddedDescButton.clicked -= onOwnedBooksSortAddedDescHandler;
            if (ownedBooksSortPublishedDescButton != null) ownedBooksSortPublishedDescButton.clicked -= onOwnedBooksSortPublishedDescHandler;
            if (ownedBooksSortPublishedAscButton != null) ownedBooksSortPublishedAscButton.clicked -= onOwnedBooksSortPublishedAscHandler;
            if (ownedBooksSortTitleButton != null) ownedBooksSortTitleButton.clicked -= onOwnedBooksSortTitleHandler;

            if (readingRecordsViewAllButton != null) readingRecordsViewAllButton.clicked -= OpenReadingRecordsModal;
            if (readingRecordsCloseButton != null) readingRecordsCloseButton.clicked -= CloseReadingRecordsModal;
            if (readingRecordsLoadMoreButton != null) readingRecordsLoadMoreButton.clicked -= onReadingRecordsLoadMoreHandler;
            if (recordsSortLatestButton != null) recordsSortLatestButton.clicked -= onRecordsSortLatestHandler;
            if (recordsSortOldestButton != null) recordsSortOldestButton.clicked -= onRecordsSortOldestHandler;
            if (recordsSortMostLikedButton != null) recordsSortMostLikedButton.clicked -= onRecordsSortMostLikedHandler;
            if (recordsSortMostViewedButton != null) recordsSortMostViewedButton.clicked -= onRecordsSortMostViewedHandler;
            if (recordsSortMostCommentedButton != null) recordsSortMostCommentedButton.clicked -= onRecordsSortMostCommentedHandler;

            if (dashboardEditCloseButton != null) dashboardEditCloseButton.clicked -= CloseDashboardEditModal;
            if (dashboardEditCancelButton != null) dashboardEditCancelButton.clicked -= CloseDashboardEditModal;
            if (dashboardEditSubmitButton != null) dashboardEditSubmitButton.clicked -= OnSubmitDashboardEdit;
            if (visibilityPublicButton != null) visibilityPublicButton.clicked -= onVisibilityPublicHandler;
            if (visibilityFollowersButton != null) visibilityFollowersButton.clicked -= onVisibilityFollowersHandler;
            if (visibilityPrivateButton != null) visibilityPrivateButton.clicked -= onVisibilityPrivateHandler;
            if (booksCarousel != null) UninstallCarouselDrag(booksCarousel, booksCarouselDrag);
            if (recordsCarousel != null) UninstallCarouselDrag(recordsCarousel, recordsCarouselDrag);
        }

        private void ApplyViewMode()
        {
            // 본인 → 편집 / 방문자 → 방명록 작성. 같은 슬롯에서 토글.
            bool isMine = viewMode == ViewMode.Mine;
            if (editButton != null)
            {
                editButton.style.display = isMine ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (writeGuestbookButton != null)
            {
                writeGuestbookButton.style.display = isMine ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (returnMineButton != null)
            {
                // \"내 공간으로\" 버튼은 Visitor 모드에서만 노출. (#6 follow-up — 방문 후 복귀 동선)
                if (isMine) returnMineButton.AddToClassList("hidden");
                else returnMineButton.RemoveFromClassList("hidden");
            }
            if (likeToggleButton != null)
            {
                // 본인 공간엔 클릭 비활성 (정적 표시), 방문자 공간엔 토글 가능.
                if (isMine)
                {
                    likeToggleButton.AddToClassList("read-only");
                }
                else
                {
                    likeToggleButton.RemoveFromClassList("read-only");
                }
            }
        }

        /// <summary>
        /// Visitor 모드에서 본인 공간으로 복귀. (#6 follow-up)
        /// </summary>
        private void OnReturnMineClicked()
        {
            if (viewMode == ViewMode.Mine) return;
            viewMode = ViewMode.Mine;
            ApplyViewMode();
            LoadInitialData();
        }

        private void LoadInitialData()
        {
            currentGuestbookPageIndex = 0;
            guestbookCursorHistory.Clear();
            guestbookCursorHistory.Add(null);

            CleanupLegacyDashboardPrefsOnce();

            if (viewMode == ViewMode.Mine)
            {
                // 본인 공간 — GET /v1/spaces/me 응답의 id 를 사용 (Repository 가 처리). source of truth = BE.
                ShowDashboardLoadingState();
                LogDashboardFetch(DashboardCallSource.MySpace, explicitSpaceId: null);
                loadDashboardUseCase.ExecuteForMine(OnDashboardLoaded, OnInitialDashboardError);
            }
            else if (visitedSpaceId > 0)
            {
                if (!TryGuardStaleSpaceIdOne(visitedSpaceId, DashboardCallSource.VisitedExplicit))
                {
                    ShowDashboardLoadingState();
                    LogDashboardFetch(DashboardCallSource.VisitedExplicit, explicitSpaceId: visitedSpaceId);
                    loadDashboardUseCase.Execute(visitedSpaceId, OnDashboardLoaded, OnInitialDashboardError);
                }
                else
                {
                    OnInitialDashboardError(new DashboardApiError
                    {
                        Code = "S003",
                        Message = "Invalid dashboard space id."
                    });
                }
            }
            else
            {
                // visitedSpaceId 미설정 — 외부 caller (Search 등) 가 set 하기 전에는 호출 자체를 skip.
                // 이전에는 SerializeField default 1L 이 그대로 전파돼 GET /v1/spaces/1/dashboard 가 호출됨.
                Debug.LogWarning(
                    $"[Dashboard] visitedSpaceId 미설정 (viewMode={viewMode}) — dashboard 호출 skip. " +
                    "공간 카드 클릭으로 spaceId 가 set 된 후 OnVisitUser → loadDashboardUseCase.Execute 경로로 호출됩니다.");
                OnInitialDashboardError(new DashboardApiError
                {
                    Code = "S003",
                    Message = "Dashboard target space id is not set."
                });
            }
        }

        // real-API 모드에서 spaceId == 1 의 stale 호출을 일괄 차단하는 stop-gap 가드.
        // dev/test BE 에 spaceId=1 row 가 없어 404/500 을 유발해 온 회귀를 막기 위한 임시 방어 — 근본적으로는
        // 호출 site 에서 실제 spaceId 가 들어오게 해야 한다. mock 모드에서는 Mock repo 가 fake snapshot 을 만들어
        // API hit 가 없으므로 가드 적용 안 함. 본인 공간 (DashboardCallSource.MySpace) 은 /v1/spaces/me 응답에서
        // 1 이 나오면 그게 source of truth 이므로 차단하지 않는다. 호출자가 차단된 경우 true 반환.
        private bool TryGuardStaleSpaceIdOne(long targetSpaceId, DashboardCallSource source)
        {
            if (useMockRepositories)
            {
                return false;
            }
            if (source == DashboardCallSource.MySpace)
            {
                return false;
            }
            if (targetSpaceId != 1L)
            {
                return false;
            }

            Debug.LogWarning(
                $"[Dashboard] real API 모드에서 spaceId=1 호출을 stop-gap 으로 차단 (source={source}, viewMode={viewMode}). " +
                "이건 stale ingress 진단용 임시 방어입니다 — 실제 spaceId 가 어디서 1 로 흘러들어왔는지 호출 chain 을 확인하세요.");
            return true;
        }

        // 사고 재현 시 어떤 경로에서 dashboard fetch 가 발사됐는지 즉시 보이도록 표준 prefix + 모든 컨텍스트를 한 줄로.
        // explicitSpaceId 가 null 이면 ExecuteForMine 경로 — 실제 spaceId 는 /v1/spaces/me 응답이 와봐야 결정된다.
        private void LogDashboardFetch(DashboardCallSource source, long? explicitSpaceId)
        {
            string url = explicitSpaceId.HasValue
                ? $"/v1/spaces/{explicitSpaceId.Value}/dashboard?period=weekly"
                : "/v1/spaces/me → /v1/spaces/{id}/dashboard?period=weekly";
            string targetText = explicitSpaceId.HasValue ? explicitSpaceId.Value.ToString() : "(unresolved — /me 응답 후 결정)";
            Debug.Log(
                $"[Dashboard][fetch] source={source} viewMode={viewMode} useMockRepositories={useMockRepositories} " +
                $"targetSpaceId={targetText} url={url}");
        }

        // 이 빌드는 visitedSpaceId / dashboard target id 를 PlayerPrefs 에 저장하지 않는다. 그러나 이전 (검토되지 않은)
        // 브랜치/실험 빌드가 영속화한 키가 사용자 단말에 남아 있을 수 있어, 후보 key 를 한 번만 선별 삭제한다.
        // - DeleteAll 은 사용 금지 (auth/login 토큰 등 다른 도메인 키 보호)
        // - 키가 실제로 존재했고 stale 값 (<=1) 인 경우만 삭제 + 명시 로그
        private void CleanupLegacyDashboardPrefsOnce()
        {
            if (legacyDashboardPrefsCleaned)
            {
                return;
            }
            legacyDashboardPrefsCleaned = true;

            string[] legacyKeys =
            {
                "Dotshelf.Dashboard.VisitedSpaceId",
                "Dotshelf.Dashboard.LastTargetSpaceId",
                "Dotshelf.Dashboard.TargetSpaceId",
                "DashboardVisitedSpaceId"
            };

            for (int i = 0; i < legacyKeys.Length; i++)
            {
                string key = legacyKeys[i];
                if (!PlayerPrefs.HasKey(key))
                {
                    continue;
                }

                long legacyValue = 0L;
                long.TryParse(PlayerPrefs.GetString(key, "0"), out legacyValue);
                PlayerPrefs.DeleteKey(key);
                if (legacyValue <= 1L)
                {
                    Debug.Log($"[Dashboard] Cleared stale visitedSpaceId={legacyValue} (PlayerPrefs key='{key}').");
                }
                else
                {
                    Debug.Log($"[Dashboard] Cleared legacy PlayerPrefs key='{key}' value={legacyValue} (이 빌드는 dashboard target id 를 영속화하지 않음).");
                }
            }
            PlayerPrefs.Save();
        }

        private void OnDashboardLoaded(DashboardSnapshot snapshot)
        {
            currentSnapshot = snapshot;
            RenderHeader(snapshot);
            LoadGuestbookPage(null);
            LoadRecentBooks();
            LoadRecentRecords();
        }

        private void OnInitialDashboardError(DashboardApiError error)
        {
            currentSnapshot = null;
            OnError(error);
            RenderDashboardLoadFailure(error);
        }

        private void ShowDashboardLoadingState()
        {
            if (nicknameLabel != null) nicknameLabel.text = "Loading...";
            if (introductionLabel != null) introductionLabel.text = string.Empty;
            ClearDashboardContent();
            SetDashboardCounts("0");
        }

        private void RenderDashboardLoadFailure(DashboardApiError error)
        {
            string userMessage = DashboardErrorMessages.Format(error);
            if (nicknameLabel != null) nicknameLabel.text = "Dashboard unavailable";
            if (introductionLabel != null) introductionLabel.text = userMessage;
            ClearDashboardContent();
            SetDashboardCounts("0");
        }

        private void ClearDashboardContent()
        {
            if (booksCarousel != null) booksCarousel.Clear();
            if (recordsCarousel != null) recordsCarousel.Clear();
            if (guestbookGrid != null) guestbookGrid.Clear();
            if (guestbookPageLabel != null) guestbookPageLabel.text = "-";
            if (guestbookPrevButton != null) guestbookPrevButton.SetEnabled(false);
            if (guestbookNextButton != null) guestbookNextButton.SetEnabled(false);
        }

        private void SetDashboardCounts(string value)
        {
            if (followingCountButton != null) followingCountButton.text = value;
            if (followersCountButton != null) followersCountButton.text = value;
            if (likeCountLabel != null) likeCountLabel.text = value;
            if (bookCountLabel != null) bookCountLabel.text = value;
            if (recordCountLabel != null) recordCountLabel.text = value;
            if (guestbookCountLabel != null) guestbookCountLabel.text = $"방명록 {value}";
            if (weeklyVisitorLabel != null) weeklyVisitorLabel.text = value;
        }

        private void LoadRecentBooks()
        {
            long spaceId = currentSnapshot?.Space.Id ?? visitedSpaceId;
            loadOwnedBooksUseCase.Execute(spaceId, OwnedBookSort.AddedDesc, null,
                onSuccess: RenderBooksCarousel,
                onError: OnError);
        }

        private void LoadRecentRecords()
        {
            long ownerId = currentSnapshot?.Space.OwnerUserId ?? 0L;
            loadReadingRecordsUseCase.Execute(ownerId, ReadingRecordSort.Latest, null,
                onSuccess: RenderRecordsCarousel,
                onError: OnError);
        }

        private void RenderBooksCarousel(OwnedBookListPage page)
        {
            if (booksCarousel == null)
            {
                return;
            }

            booksCarousel.Clear();
            int count = Math.Min(CarouselItemLimit, page.Items.Count);
            for (int i = 0; i < count; i++)
            {
                booksCarousel.Add(BuildBookCell(page.Items[i]));
            }
        }

        private VisualElement BuildBookCell(OwnedBookItem item)
        {
            VisualElement cell = new VisualElement();
            cell.AddToClassList("carousel-cell");

            VisualElement cover = new VisualElement();
            cover.AddToClassList("book-cover");
            cell.Add(cover);

            if (!string.IsNullOrWhiteSpace(item.CoverImageUrl))
            {
                var url = item.CoverImageUrl;
                if (coverImageLoader.TryGet(url, out var cached))
                {
                    ApplyCoverTexture(cover, cached);
                }
                else
                {
                    StartCoroutine(DownloadCover(url, cover));
                }
            }

            Label title = new Label(item.Title ?? string.Empty);
            title.AddToClassList("book-cell-title");
            cell.Add(title);

            return cell;
        }

        private static void ApplyCoverTexture(VisualElement element, Texture2D texture)
        {
            if (element == null || texture == null)
            {
                return;
            }

            element.style.backgroundImage = new StyleBackground(texture);
            element.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            element.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            element.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
        }

        // NOTE: RemoteImageLoader.Load(url, callback)를 쓰지 않고 인라인 코루틴으로 다운로드한다.
        // 이유: RemoteImageLoader는 App.Core.asmdef에 있고, 이 컨트롤러는 Assembly-CSharp에 있다.
        // cross-asmdef 경계를 넘어 반환된 IEnumerator를 StartCoroutine에 넘기면
        // MoveNext 호출이 실패해 코루틴이 실행되지 않는 Unity 제한이 있다.
        // 따라서 캐시(TryGet/Put)만 사용하고 다운로드는 여기서 직접 수행한다.
        private IEnumerator DownloadCover(string url, VisualElement target)
        {
            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                request.timeout = coverImageLoader?.TimeoutSeconds ?? 8;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    yield break;
                }

                var texture = DownloadHandlerTexture.GetContent(request);
                if (texture == null)
                {
                    yield break;
                }

                if (texture.width > MaxRemoteBookCoverWidth || texture.height > MaxRemoteBookCoverHeight)
                {
                    UnityEngine.Debug.LogWarning($"[Dashboard] Skipped oversized book cover {texture.width}x{texture.height}: {url}");
                    UnityEngine.Object.Destroy(texture);
                    yield break;
                }

                // 항상 캐시에 넣어 Texture2D 소유권을 이전한다.
                // target이 detach된 경우에도 캐시가 소유하므로 텍스처 누수가 없다.
                // Put은 이미 같은 URL이 캐시에 있으면 duplicate를 destroy하고
                // canonical 텍스처를 반환한다 (concurrent download race 방지).
                var canonical = coverImageLoader?.Put(url, texture) ?? texture;
                if (target?.panel != null)
                {
                    ApplyCoverTexture(target, canonical);
                }
            }
        }

        private void RenderRecordsCarousel(ReadingRecordListPage page)
        {
            if (recordsCarousel == null)
            {
                return;
            }

            recordsCarousel.Clear();
            int count = Math.Min(CarouselItemLimit, page.Items.Count);
            for (int i = 0; i < count; i++)
            {
                recordsCarousel.Add(BuildRecordCell(page.Items[i]));
            }
        }

        private static VisualElement BuildRecordCell(ReadingRecordItem item)
        {
            VisualElement cell = new VisualElement();
            cell.AddToClassList("record-cell");

            Label title = new Label(item.BookTitle ?? string.Empty);
            title.AddToClassList("record-cell-title");
            cell.Add(title);

            Label content = new Label(TruncateContent(item.Content, 50));
            content.AddToClassList("record-cell-content");
            cell.Add(content);

            VisualElement metaRow = new VisualElement();
            metaRow.AddToClassList("record-cell-meta");
            Label likes = new Label($"♥ {item.LikeCount}");
            likes.AddToClassList("record-cell-meta-item");
            metaRow.Add(likes);
            Label comments = new Label($"💬 {item.CommentCount}");
            comments.AddToClassList("record-cell-meta-item");
            metaRow.Add(comments);
            cell.Add(metaRow);

            return cell;
        }

        private static string TruncateContent(string content, int maxLength)
        {
            if (string.IsNullOrEmpty(content))
            {
                return string.Empty;
            }

            return content.Length <= maxLength
                ? content
                : content.Substring(0, maxLength) + "…";
        }

        private void RenderHeader(DashboardSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            if (nicknameLabel != null) nicknameLabel.text = snapshot.OwnerNickname;
            if (introductionLabel != null) introductionLabel.text = snapshot.OwnerIntroduction;
            if (followingCountButton != null) followingCountButton.text = $"팔로잉 {snapshot.FollowingCount}";
            if (followersCountButton != null) followersCountButton.text = $"팔로워 {snapshot.FollowerCount}";
            if (likeCountLabel != null) likeCountLabel.text = snapshot.Space.LikeCount.ToString();
            if (bookCountLabel != null) bookCountLabel.text = snapshot.BookCount.ToString();
            if (recordCountLabel != null) recordCountLabel.text = snapshot.ReadingRecordCount.ToString();
            if (weeklyVisitorLabel != null) weeklyVisitorLabel.text = $"이번주 방문자 {snapshot.WeeklyVisitorCount}";

            UpdateLikeButton(snapshot.Space.Liked);
        }

        private void UpdateLikeButton(bool liked)
        {
            if (likeToggleButton == null)
            {
                return;
            }

            likeToggleButton.text = liked ? "♥" : "♡";
            if (liked)
            {
                likeToggleButton.AddToClassList("liked");
            }
            else
            {
                likeToggleButton.RemoveFromClassList("liked");
            }
        }

        private void OnLikeButtonClicked()
        {
            if (viewMode == ViewMode.Mine || likeRequestInFlight || currentSnapshot == null)
            {
                return;
            }

            likeRequestInFlight = true;
            bool nextLiked = !currentSnapshot.Space.Liked;
            UpdateLikeButton(nextLiked); // 낙관적 업데이트

            toggleLikeUseCase.Execute(
                currentSnapshot.Space.Id,
                nextLiked,
                onSuccess: result =>
                {
                    likeRequestInFlight = false;
                    if (likeCountLabel != null)
                    {
                        likeCountLabel.text = result.LikeCount.ToString();
                    }
                    UpdateLikeButton(result.Liked);
                    // snapshot도 갱신 — 다음 토글이 새로운 상태에서 시작되도록
                    currentSnapshot = RebuildSnapshotWithLike(currentSnapshot, result);
                },
                onError: error =>
                {
                    likeRequestInFlight = false;
                    UpdateLikeButton(currentSnapshot.Space.Liked); // 롤백
                    Debug.LogWarning($"[Dashboard] Like toggle failed: {error.Code} {error.Message}");
                });
        }

        private static DashboardSnapshot RebuildSnapshotWithLike(DashboardSnapshot snapshot, LikeToggleResult result)
        {
            App.Domain.Dashboard.Space updated = new App.Domain.Dashboard.Space(
                id: snapshot.Space.Id,
                ownerUserId: snapshot.Space.OwnerUserId,
                visibility: snapshot.Space.Visibility,
                guestbookVisibility: snapshot.Space.GuestbookVisibility,
                visitCount: snapshot.Space.VisitCount,
                likeCount: result.LikeCount,
                liked: result.Liked,
                layoutVersion: snapshot.Space.LayoutVersion,
                currentThemeInventoryId: snapshot.Space.CurrentThemeInventoryId);

            return new DashboardSnapshot(
                space: updated,
                ownerNickname: snapshot.OwnerNickname,
                ownerProfileImageUrl: snapshot.OwnerProfileImageUrl,
                ownerIntroduction: snapshot.OwnerIntroduction,
                followerCount: snapshot.FollowerCount,
                followingCount: snapshot.FollowingCount,
                bookshelfCount: snapshot.BookshelfCount,
                bookCount: snapshot.BookCount,
                readingRecordCount: snapshot.ReadingRecordCount,
                weeklyVisitorCount: snapshot.WeeklyVisitorCount);
        }

        private void LoadGuestbookPage(string cursor)
        {
            long spaceId = currentSnapshot?.Space.Id ?? visitedSpaceId;
            loadGuestbookPageUseCase.Execute(spaceId, cursor,
                onSuccess: page =>
                {
                    nextGuestbookCursor = page.NextCursor;
                    RenderGuestbookGrid(page);
                    UpdateGuestbookCount(page);
                    UpdateGuestbookPagination();
                },
                onError: OnError);
        }

        private void RenderGuestbookGrid(GuestbookListPage page)
        {
            if (guestbookGrid == null)
            {
                return;
            }

            guestbookGrid.Clear();
            for (int i = 0; i < page.Items.Count; i++)
            {
                guestbookGrid.Add(BuildGuestbookCell(page.Items[i]));
            }
        }

        private VisualElement BuildGuestbookCell(Guestbook guestbook)
        {
            VisualElement cell = new VisualElement();
            cell.AddToClassList("guestbook-cell");
            cell.userData = guestbook;

            if (guestbook.PostItInventoryId > 0)
            {
                IReadOnlyList<PostItStyle> styles = AvailablePostItStyles();
                for (int s = 0; s < styles.Count; s++)
                {
                    if (styles[s].AssetId == guestbook.PostItInventoryId)
                    {
                        cell.style.backgroundColor = HexToColor(styles[s].ColorHex);
                        break;
                    }
                }
            }

            Label content = new Label(guestbook.Content);
            content.AddToClassList("guestbook-cell-content");
            cell.Add(content);

            if (guestbook.HasReply)
            {
                Label reply = new Label("↳ " + guestbook.Reply.Content);
                reply.AddToClassList("guestbook-cell-reply");
                cell.Add(reply);
            }

            Label nickname = new Label(string.IsNullOrEmpty(guestbook.DisplayedNickname) ? "익명" : guestbook.DisplayedNickname);
            nickname.AddToClassList("guestbook-cell-nickname");
            cell.Add(nickname);

            cell.RegisterCallback<ClickEvent>(_ => OnGuestbookCellClicked(guestbook));
            return cell;
        }

        private void OnGuestbookCellClicked(Guestbook guestbook)
        {
            OpenGuestbookDetailModal(guestbook);
        }

        // ===== 방명록 상세/답장 모달 =====

        private long GetMyUserId()
        {
            // viewMode == Mine이면 본인 = 공간 주인. Visitor면 mock self id.
            return viewMode == ViewMode.Mine
                ? (currentSnapshot?.Space.OwnerUserId ?? MockSelfUserId)
                : MockSelfUserId;
        }

        private void OpenGuestbookDetailModal(Guestbook guestbook)
        {
            currentDetailGuestbook = guestbook;
            if (guestbookDetailContentText != null) guestbookDetailContentText.text = guestbook.Content;
            if (guestbookDetailCard != null && guestbook.PostItInventoryId > 0)
            {
                IReadOnlyList<PostItStyle> styles = AvailablePostItStyles();
                for (int s = 0; s < styles.Count; s++)
                {
                    if (styles[s].AssetId == guestbook.PostItInventoryId)
                    {
                        guestbookDetailCard.style.backgroundColor = HexToColor(styles[s].ColorHex);
                        break;
                    }
                }
            }
            if (guestbookDetailMeta != null)
            {
                string nick = guestbook.Anonymous ? "익명" : guestbook.DisplayedNickname;
                guestbookDetailMeta.text = $"{nick} · {FormatDate(guestbook.CreatedAt)}";
            }

            long myId = GetMyUserId();
            long ownerId = currentSnapshot?.Space.OwnerUserId ?? 0L;
            bool canDelete = myId == ownerId || myId == guestbook.WriterUserId;
            ToggleVisibility(guestbookDetailDeleteButton, canDelete);

            if (guestbook.HasReply)
            {
                ToggleVisibility(guestbookDetailReplyExisting, true);
                ToggleVisibility(guestbookDetailReplyForm, false);
                if (guestbookDetailReplyContent != null) guestbookDetailReplyContent.text = guestbook.Reply.Content;
                if (guestbookDetailReplyMeta != null)
                {
                    guestbookDetailReplyMeta.text = $"{guestbook.Reply.WriterNickname} · {FormatDate(guestbook.Reply.CreatedAt)}";
                }
                bool canDeleteReply = myId == ownerId || myId == guestbook.Reply.WriterUserId;
                ToggleVisibility(guestbookDetailReplyDeleteButton, canDeleteReply);
            }
            else
            {
                ToggleVisibility(guestbookDetailReplyExisting, false);
                bool canWriteReply = viewMode == ViewMode.Mine; // 답장은 공간 주인만
                ToggleVisibility(guestbookDetailReplyForm, canWriteReply);
                if (canWriteReply)
                {
                    if (guestbookDetailReplyInput != null) guestbookDetailReplyInput.value = string.Empty;
                    if (guestbookDetailReplyCounter != null) guestbookDetailReplyCounter.text = "0 / 30";
                    BuildPostItStylePicker(guestbookDetailReplyStyleList, OnReplyPostItStyleSelected);
                }
            }

            if (guestbookDetailModalRoot != null) guestbookDetailModalRoot.RemoveFromClassList("hidden");
        }

        private void CloseGuestbookDetailModal()
        {
            if (guestbookDetailModalRoot != null) guestbookDetailModalRoot.AddToClassList("hidden");
            currentDetailGuestbook = null;
            selectedReplyPostItStyleId = 0;
            selectedReplyPostItStyleCell = null;
        }

        private void OnReplyPostItStyleSelected(long styleId, VisualElement cell)
        {
            if (selectedReplyPostItStyleCell != null) selectedReplyPostItStyleCell.RemoveFromClassList("selected");
            selectedReplyPostItStyleId = styleId;
            selectedReplyPostItStyleCell = cell;
            cell.AddToClassList("selected");
        }

        private void OnSubmitGuestbookReply()
        {
            if (currentDetailGuestbook == null) return;

            string content = guestbookDetailReplyInput?.value ?? string.Empty;
            WriteGuestbookReplyCommand command = new WriteGuestbookReplyCommand
            {
                GuestbookId = currentDetailGuestbook.Id,
                PostItInventoryId = selectedReplyPostItStyleId,
                Content = content
            };

            if (guestbookDetailReplySubmitButton != null) guestbookDetailReplySubmitButton.SetEnabled(false);
            writeGuestbookReplyUseCase.Execute(command,
                onSuccess: _ =>
                {
                    if (guestbookDetailReplySubmitButton != null) guestbookDetailReplySubmitButton.SetEnabled(true);
                    CloseGuestbookDetailModal();
                    LoadGuestbookPage(null);
                },
                onError: error =>
                {
                    if (guestbookDetailReplySubmitButton != null) guestbookDetailReplySubmitButton.SetEnabled(true);
                    OnError(error);
                });
        }

        private void OnDeleteGuestbook()
        {
            if (currentDetailGuestbook == null) return;
            long id = currentDetailGuestbook.Id;
            deleteGuestbookUseCase.Execute(id,
                onSuccess: () =>
                {
                    CloseGuestbookDetailModal();
                    LoadGuestbookPage(null);
                },
                onError: OnError);
        }

        private void OnDeleteGuestbookReply()
        {
            if (currentDetailGuestbook == null || !currentDetailGuestbook.HasReply) return;
            long gid = currentDetailGuestbook.Id;
            long rid = currentDetailGuestbook.Reply.Id;
            deleteGuestbookReplyUseCase.Execute(gid, rid,
                onSuccess: () =>
                {
                    CloseGuestbookDetailModal();
                    LoadGuestbookPage(null);
                },
                onError: OnError);
        }

        private void OnReportGuestbook()
        {
            if (currentDetailGuestbook == null) return;
            ReportGuestbookCommand command = new ReportGuestbookCommand
            {
                Target = GuestbookReportTarget.Guestbook,
                TargetId = currentDetailGuestbook.Id,
                ReasonType = "INAPPROPRIATE",
                ReasonDetail = string.Empty
            };
            reportGuestbookUseCase.Execute(command,
                onSuccess: () =>
                {
                    Debug.Log("[Dashboard] Reported guestbook " + command.TargetId);
                    CloseGuestbookDetailModal();
                },
                onError: OnError);
        }

        private static void ToggleVisibility(VisualElement element, bool visible)
        {
            if (element == null) return;
            if (visible) element.RemoveFromClassList("hidden");
            else element.AddToClassList("hidden");
        }

        private static string FormatDate(DateTimeOffset dt)
        {
            if (dt == DateTimeOffset.MinValue) return string.Empty;
            return dt.ToLocalTime().ToString("MM/dd HH:mm");
        }

        private static Color HexToColor(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length < 6) return new Color(1f, 0.96f, 0.78f);
            int r = Convert.ToInt32(hex.Substring(0, 2), 16);
            int g = Convert.ToInt32(hex.Substring(2, 2), 16);
            int b = Convert.ToInt32(hex.Substring(4, 2), 16);
            return new Color(r / 255f, g / 255f, b / 255f);
        }

        public void CloseAllModals()
        {
            CloseGuestbookDetailModal();
            CloseGuestbookWriteModal();
            CloseDashboardEditModal();
            CloseOwnedBooksModal();
            CloseReadingRecordsModal();
            CloseUserListModal();
        }

        private void UpdateGuestbookCount(GuestbookListPage page)
        {
            if (guestbookCountLabel == null)
            {
                return;
            }

            // P0c는 단일 페이지만 카운트로 표시. 추후 백엔드 totalCount 응답에 맞춰 갱신.
            guestbookCountLabel.text = $"방명록 {page.Items.Count}";
        }

        private void UpdateGuestbookPagination()
        {
            if (guestbookPageLabel != null)
            {
                guestbookPageLabel.text = (currentGuestbookPageIndex + 1).ToString();
            }
            if (guestbookPrevButton != null)
            {
                guestbookPrevButton.SetEnabled(currentGuestbookPageIndex > 0);
            }
            if (guestbookNextButton != null)
            {
                guestbookNextButton.SetEnabled(!string.IsNullOrEmpty(nextGuestbookCursor));
            }
        }

        private void OnGuestbookPrev()
        {
            if (currentGuestbookPageIndex <= 0)
            {
                return;
            }

            currentGuestbookPageIndex--;
            string cursor = guestbookCursorHistory[currentGuestbookPageIndex];
            // 다음 페이지 캐시 무효화
            if (guestbookCursorHistory.Count > currentGuestbookPageIndex + 1)
            {
                guestbookCursorHistory.RemoveRange(currentGuestbookPageIndex + 1, guestbookCursorHistory.Count - currentGuestbookPageIndex - 1);
            }
            LoadGuestbookPage(cursor);
        }

        private void OnGuestbookNext()
        {
            if (string.IsNullOrEmpty(nextGuestbookCursor))
            {
                return;
            }

            currentGuestbookPageIndex++;
            if (guestbookCursorHistory.Count <= currentGuestbookPageIndex)
            {
                guestbookCursorHistory.Add(nextGuestbookCursor);
            }
            else
            {
                guestbookCursorHistory[currentGuestbookPageIndex] = nextGuestbookCursor;
            }
            LoadGuestbookPage(nextGuestbookCursor);
        }

        private void OnEditClicked()
        {
            OpenDashboardEditModal();
        }

        // ===== 대시보드 편집 모달 =====

        private void OpenDashboardEditModal()
        {
            if (dashboardEditModalRoot == null || currentSnapshot == null) return;

            if (dashboardEditIntroduction != null)
            {
                dashboardEditIntroduction.value = currentSnapshot.OwnerIntroduction ?? string.Empty;
            }

            selectedVisibility = currentSnapshot.Space.GuestbookVisibility;
            UpdateVisibilityButtons();

            dashboardEditModalRoot.RemoveFromClassList("hidden");
        }

        private void CloseDashboardEditModal()
        {
            if (dashboardEditModalRoot != null) dashboardEditModalRoot.AddToClassList("hidden");
        }

        private void OnVisibilitySelected(VisibilityScope scope)
        {
            selectedVisibility = scope;
            UpdateVisibilityButtons();
        }

        private void UpdateVisibilityButtons()
        {
            SetSelected(visibilityPublicButton, selectedVisibility == VisibilityScope.Public);
            SetSelected(visibilityFollowersButton, selectedVisibility == VisibilityScope.FollowersOnly);
            SetSelected(visibilityPrivateButton, selectedVisibility == VisibilityScope.Private);
        }

        private static void SetSelected(VisualElement element, bool selected)
        {
            if (element == null) return;
            if (selected) element.AddToClassList("selected");
            else element.RemoveFromClassList("selected");
        }

        private void OnSubmitDashboardEdit()
        {
            if (currentSnapshot == null || dashboardEditSubmitButton == null) return;
            dashboardEditSubmitButton.SetEnabled(false);

            string newIntroduction = dashboardEditIntroduction?.value ?? string.Empty;
            VisibilityScope newVisibility = selectedVisibility;

            updateIntroductionUseCase.Execute(newIntroduction,
                onSuccess: () =>
                {
                    updateGuestbookVisibilityUseCase.Execute(newVisibility,
                        onSuccess: () => FinishDashboardEdit(null, savedIntroduction: true, savedVisibility: true),
                        onError: err => FinishDashboardEdit(err, savedIntroduction: true, savedVisibility: false));
                },
                onError: err => FinishDashboardEdit(err, savedIntroduction: false, savedVisibility: false));
        }

        private void FinishDashboardEdit(DashboardApiError error, bool savedIntroduction, bool savedVisibility)
        {
            if (dashboardEditSubmitButton != null) dashboardEditSubmitButton.SetEnabled(true);
            if (error != null)
            {
                Debug.LogWarning($"[Dashboard] Edit 부분 실패: intro={savedIntroduction}, visibility={savedVisibility}. 에러={error.Code} {error.Message}");
                OnError(error);
                if (savedIntroduction || savedVisibility)
                {
                    if (viewMode == ViewMode.Mine)
                    {
                        LogDashboardFetch(DashboardCallSource.DashboardEditRefresh, explicitSpaceId: null);
                        loadDashboardUseCase.ExecuteForMine(OnDashboardLoaded, OnError);
                    }
                }
                return;
            }
            CloseDashboardEditModal();
            if (viewMode == ViewMode.Mine)
            {
                LogDashboardFetch(DashboardCallSource.DashboardEditRefresh, explicitSpaceId: null);
                loadDashboardUseCase.ExecuteForMine(OnDashboardLoaded, OnError);
            }
        }

        private void OnWriteGuestbookClicked()
        {
            OpenGuestbookWriteModal();
        }

        // ===== 방명록 작성 모달 =====

        private void OpenGuestbookWriteModal()
        {
            if (guestbookWriteModalRoot == null) return;

            if (guestbookWriteContent != null) guestbookWriteContent.value = string.Empty;
            if (guestbookWriteCounter != null) guestbookWriteCounter.text = "0 / 30";
            if (guestbookWriteAnonymousToggle != null) guestbookWriteAnonymousToggle.value = false;
            BuildPostItStylePicker(guestbookWriteStyleList, OnPostItStyleSelected);

            guestbookWriteModalRoot.RemoveFromClassList("hidden");
        }

        private void CloseGuestbookWriteModal()
        {
            if (guestbookWriteModalRoot != null)
            {
                guestbookWriteModalRoot.AddToClassList("hidden");
            }
            selectedPostItStyleId = 0;
            selectedPostItStyleCell = null;
        }

        private void BuildPostItStylePicker(ScrollView container, Action<long, VisualElement> onSelected)
        {
            if (container == null) return;
            container.Clear();
            IReadOnlyList<PostItStyle> styles = AvailablePostItStyles();
            for (int i = 0; i < styles.Count; i++)
            {
                long id = styles[i].AssetId;
                string displayName = string.IsNullOrEmpty(styles[i].DisplayName) ? $"스타일 {id}" : styles[i].DisplayName;
                VisualElement cell = new VisualElement();
                cell.AddToClassList("postit-style-cell");
                cell.style.backgroundColor = HexToColor(styles[i].ColorHex);
                Label name = new Label(displayName);
                name.AddToClassList("postit-style-cell-name");
                cell.Add(name);
                cell.RegisterCallback<ClickEvent>(_ => onSelected(id, cell));
                container.Add(cell);
            }
            selectedPostItStyleId = 0;
            selectedPostItStyleCell = null;
        }

        /// <summary>
        /// PostItStyleCatalogAsset에서 사용 가능한 스타일 목록을 반환. (#7 follow-up — 하드코딩 제거)
        /// catalog 미주입 시 LogError + 빈 리스트 (개발자가 SO 주입을 즉시 인지하도록).
        /// </summary>
        private IReadOnlyList<PostItStyle> AvailablePostItStyles()
        {
            if (postItStyleCatalog != null) return postItStyleCatalog.GetAllStyles();
            if (postItCatalog == null)
            {
                Debug.LogError("[Dashboard] PostItStyleCatalogAsset 미주입. picker가 비어있습니다. Inspector에서 catalog SO를 연결하세요.");
                return Array.Empty<PostItStyle>();
            }
            postItStyleCatalog = new PostItStyleCatalogAdapter(postItCatalog);
            return postItStyleCatalog.GetAllStyles();
        }

        private void OnPostItStyleSelected(long styleId, VisualElement cell)
        {
            if (selectedPostItStyleCell != null)
            {
                selectedPostItStyleCell.RemoveFromClassList("selected");
            }
            selectedPostItStyleId = styleId;
            selectedPostItStyleCell = cell;
            cell.AddToClassList("selected");
        }

        private void OnSubmitGuestbookWrite()
        {
            if (currentSnapshot == null) return;

            string content = guestbookWriteContent?.value ?? string.Empty;
            bool anonymous = guestbookWriteAnonymousToggle?.value ?? false;

            WriteGuestbookCommand command = new WriteGuestbookCommand
            {
                SpaceId = currentSnapshot.Space.Id,
                PostItInventoryId = selectedPostItStyleId,
                Content = content,
                Anonymous = anonymous
            };

            if (guestbookWriteSubmitButton != null) guestbookWriteSubmitButton.SetEnabled(false);

            writeGuestbookUseCase.Execute(command,
                onSuccess: _ =>
                {
                    if (guestbookWriteSubmitButton != null) guestbookWriteSubmitButton.SetEnabled(true);
                    CloseGuestbookWriteModal();
                    LoadGuestbookPage(null); // refresh
                },
                onError: error =>
                {
                    if (guestbookWriteSubmitButton != null) guestbookWriteSubmitButton.SetEnabled(true);
                    OnError(error);
                });
        }

        private void OnError(DashboardApiError error)
        {
            string userMessage = DashboardErrorMessages.Format(error);
            Debug.LogWarning($"[Dashboard] API error: code={error?.Code} server={error?.Message} → user={userMessage}");
            // TODO: 실제 사용자 노출은 토스트/배너 컴포넌트 합류 후 — 현재는 콘솔 로그.
        }

        // ===== 팔로워/팔로잉 모달 =====

        private void OnFollowersClicked()
        {
            OpenUserListModal(UserListMode.Followers);
        }

        private void OnFollowingsClicked()
        {
            OpenUserListModal(UserListMode.Followings);
        }

        private void OpenUserListModal(UserListMode mode)
        {
            currentUserListMode = mode;
            if (userListModalTitle != null)
            {
                userListModalTitle.text = mode == UserListMode.Followers ? "팔로워" : "팔로잉";
            }
            if (userListModalRoot != null)
            {
                userListModalRoot.RemoveFromClassList("hidden");
            }
            LoadUserListPage();
        }

        private void CloseUserListModal()
        {
            if (userListModalRoot != null)
            {
                userListModalRoot.AddToClassList("hidden");
            }
            if (userListModalList != null)
            {
                userListModalList.Clear();
            }
        }

        private void LoadUserListPage()
        {
            long userId = currentSnapshot?.Space.OwnerUserId ?? 0L;
            if (currentUserListMode == UserListMode.Followers)
            {
                loadFollowersUseCase.Execute(userId, null,
                    onSuccess: RenderUserList,
                    onError: OnError);
            }
            else
            {
                loadFollowingsUseCase.Execute(userId, null,
                    onSuccess: RenderUserList,
                    onError: OnError);
            }
        }

        private void RenderUserList(UserListPage page)
        {
            if (userListModalList == null)
            {
                return;
            }

            userListModalList.Clear();
            if (page.Items.Count == 0)
            {
                if (userListModalEmpty != null) userListModalEmpty.RemoveFromClassList("hidden");
                return;
            }

            if (userListModalEmpty != null) userListModalEmpty.AddToClassList("hidden");

            for (int i = 0; i < page.Items.Count; i++)
            {
                userListModalList.Add(BuildUserRow(page.Items[i]));
            }
        }

        private VisualElement BuildUserRow(UserSummary user)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("user-row");

            VisualElement avatar = new VisualElement();
            avatar.AddToClassList("user-row-image");
            row.Add(avatar);

            VisualElement textBlock = new VisualElement();
            textBlock.AddToClassList("user-row-text");
            Label nickname = new Label(user.Nickname);
            nickname.AddToClassList("user-row-nickname");
            textBlock.Add(nickname);
            if (user.Mutual)
            {
                Label tag = new Label("맞팔");
                tag.AddToClassList("user-row-tag");
                textBlock.Add(tag);
            }
            row.Add(textBlock);

            VisualElement actions = new VisualElement();
            actions.AddToClassList("user-row-actions");

            if (user.SpaceId.HasValue && user.SpaceId.Value > 0)
            {
                bool isSelf = currentSnapshot != null && user.Id == currentSnapshot.Space.OwnerUserId;
                if (!isSelf)
                {
                    Button visitBtn = new Button { text = "방문" };
                    visitBtn.AddToClassList("user-row-action-button");
                    visitBtn.clicked += () => OnVisitUser(user);
                    actions.Add(visitBtn);
                }
            }

            bool isMineSpace = viewMode == ViewMode.Mine;
            if (isMineSpace && currentUserListMode == UserListMode.Followers)
            {
                // 내 공간 팔로워 모달 — 차단만 노출 (자기 자신을 팔로우/언팔할 일 없음)
                Button blockBtn = new Button { text = "차단" };
                blockBtn.AddToClassList("user-row-action-button");
                blockBtn.AddToClassList("danger");
                blockBtn.clicked += () => OnBlockUser(row, user);
                actions.Add(blockBtn);
            }
            else if (isMineSpace && currentUserListMode == UserListMode.Followings)
            {
                // 내 공간 팔로잉 모달 — 언팔로우만
                Button followBtn = new Button();
                followBtn.AddToClassList("user-row-action-button");
                UpdateFollowButton(followBtn, true);
                followBtn.clicked += () => OnToggleFollow(followBtn, user);
                actions.Add(followBtn);
            }
            else
            {
                // 타인 공간 모달 — 팔로우/언팔로우 토글
                Button followBtn = new Button();
                followBtn.AddToClassList("user-row-action-button");
                UpdateFollowButton(followBtn, user.Following);
                followBtn.clicked += () => OnToggleFollow(followBtn, user);
                actions.Add(followBtn);
            }

            row.Add(actions);
            return row;
        }

        private void OnVisitUser(UserSummary user)
        {
            CloseUserListModal();
            if (currentSnapshot != null && user.Id == currentSnapshot.Space.OwnerUserId)
            {
                return;
            }
            if (!user.SpaceId.HasValue || user.SpaceId.Value <= 0)
            {
                Debug.LogWarning($"[Dashboard] OnVisitUser — user.Id={user.Id} 에 유효한 spaceId 없음. visit skip.");
                return;
            }
            long targetSpaceId = user.SpaceId.Value;
            if (TryGuardStaleSpaceIdOne(targetSpaceId, DashboardCallSource.UserClick))
            {
                return;
            }
            viewMode = ViewMode.Visitor;
            visitedSpaceId = targetSpaceId;
            ApplyViewMode();
            ShowDashboardLoadingState();
            LogDashboardFetch(DashboardCallSource.UserClick, explicitSpaceId: targetSpaceId);
            loadDashboardUseCase.Execute(targetSpaceId, OnDashboardLoaded, OnInitialDashboardError);
        }

        private static void UpdateFollowButton(Button button, bool following)
        {
            // (#8 follow-up) — i18n에 안전한 상태 보존: button.text 매칭 대신 userData에 bool 박아둠.
            button.text = following ? "팔로잉" : "팔로우";
            button.userData = following;
            if (following)
            {
                button.RemoveFromClassList("primary");
            }
            else
            {
                button.AddToClassList("primary");
            }
        }

        private void OnToggleFollow(Button button, UserSummary user)
        {
            bool wasFollowing = button.userData is bool flag && flag;
            UpdateFollowButton(button, !wasFollowing);
            button.SetEnabled(false);
            if (wasFollowing)
            {
                unfollowUseCase.Execute(user.Id,
                    onSuccess: result =>
                    {
                        button.SetEnabled(true);
                        UpdateFollowButton(button, result.Following);
                    },
                    onError: error =>
                    {
                        button.SetEnabled(true);
                        UpdateFollowButton(button, wasFollowing);
                        OnError(error);
                    });
            }
            else
            {
                followUseCase.Execute(user.Id,
                    onSuccess: result =>
                    {
                        button.SetEnabled(true);
                        UpdateFollowButton(button, result.Following);
                    },
                    onError: error =>
                    {
                        button.SetEnabled(true);
                        UpdateFollowButton(button, wasFollowing);
                        OnError(error);
                    });
            }
        }

        private void OnBlockUser(VisualElement row, UserSummary user)
        {
            blockUseCase.Execute(user.Id,
                onSuccess: _ => row.RemoveFromHierarchy(),
                onError: OnError);
        }

        // ===== 보유 책 풀 목록 모달 =====

        private void OpenOwnedBooksModal()
        {
            if (ownedBooksModalRoot == null) return;
            currentOwnedBooksSort = OwnedBookSort.AddedDesc;
            UpdateOwnedBooksSortButtons();
            ownedBooksNextCursor = null;
            LoadOwnedBooksPage(replace: true);
            ownedBooksModalRoot.RemoveFromClassList("hidden");
        }

        private void CloseOwnedBooksModal()
        {
            if (ownedBooksModalRoot != null) ownedBooksModalRoot.AddToClassList("hidden");
        }

        private void OnOwnedBooksSortChanged(OwnedBookSort sort)
        {
            currentOwnedBooksSort = sort;
            UpdateOwnedBooksSortButtons();
            ownedBooksNextCursor = null;
            LoadOwnedBooksPage(replace: true);
        }

        private void UpdateOwnedBooksSortButtons()
        {
            SetSelected(ownedBooksSortAddedDescButton, currentOwnedBooksSort == OwnedBookSort.AddedDesc);
            SetSelected(ownedBooksSortPublishedDescButton, currentOwnedBooksSort == OwnedBookSort.PublishedDesc);
            SetSelected(ownedBooksSortPublishedAscButton, currentOwnedBooksSort == OwnedBookSort.PublishedAsc);
            SetSelected(ownedBooksSortTitleButton, currentOwnedBooksSort == OwnedBookSort.TitleAsc);
        }

        private void LoadOwnedBooksPage(bool replace)
        {
            long spaceId = currentSnapshot?.Space.Id ?? visitedSpaceId;
            string cursor = replace ? null : ownedBooksNextCursor;
            loadOwnedBooksUseCase.Execute(spaceId, currentOwnedBooksSort, cursor,
                onSuccess: page =>
                {
                    if (replace && ownedBooksGrid != null) ownedBooksGrid.Clear();
                    if (ownedBooksGrid != null)
                    {
                        for (int i = 0; i < page.Items.Count; i++)
                        {
                            ownedBooksGrid.Add(BuildBookGridCell(page.Items[i]));
                        }
                    }
                    ownedBooksNextCursor = page.NextCursor;
                    UpdateLoadMoreButton(ownedBooksLoadMoreButton, page.HasMore);
                },
                onError: OnError);
        }

        private static VisualElement BuildBookGridCell(OwnedBookItem item)
        {
            VisualElement cell = new VisualElement();
            cell.AddToClassList("book-grid-cell");

            VisualElement cover = new VisualElement();
            cover.AddToClassList("book-grid-cover");
            cell.Add(cover);

            Label title = new Label(item.Title ?? string.Empty);
            title.AddToClassList("book-grid-title");
            cell.Add(title);

            return cell;
        }

        // ===== 독후감 풀 목록 모달 =====

        private void OpenReadingRecordsModal()
        {
            if (readingRecordsModalRoot == null) return;
            currentRecordsSort = ReadingRecordSort.Latest;
            UpdateRecordsSortButtons();
            readingRecordsNextCursor = null;
            LoadReadingRecordsPage(replace: true);
            readingRecordsModalRoot.RemoveFromClassList("hidden");
        }

        private void CloseReadingRecordsModal()
        {
            if (readingRecordsModalRoot != null) readingRecordsModalRoot.AddToClassList("hidden");
        }

        private void OnRecordsSortChanged(ReadingRecordSort sort)
        {
            currentRecordsSort = sort;
            UpdateRecordsSortButtons();
            readingRecordsNextCursor = null;
            LoadReadingRecordsPage(replace: true);
        }

        private void UpdateRecordsSortButtons()
        {
            SetSelected(recordsSortLatestButton, currentRecordsSort == ReadingRecordSort.Latest);
            SetSelected(recordsSortOldestButton, currentRecordsSort == ReadingRecordSort.Oldest);
            SetSelected(recordsSortMostLikedButton, currentRecordsSort == ReadingRecordSort.MostLiked);
            SetSelected(recordsSortMostViewedButton, currentRecordsSort == ReadingRecordSort.MostViewed);
            SetSelected(recordsSortMostCommentedButton, currentRecordsSort == ReadingRecordSort.MostCommented);
        }

        private void LoadReadingRecordsPage(bool replace)
        {
            long ownerId = currentSnapshot?.Space.OwnerUserId ?? 0L;
            string cursor = replace ? null : readingRecordsNextCursor;
            loadReadingRecordsUseCase.Execute(ownerId, currentRecordsSort, cursor,
                onSuccess: page =>
                {
                    if (replace && readingRecordsList != null) readingRecordsList.Clear();
                    if (readingRecordsList != null)
                    {
                        for (int i = 0; i < page.Items.Count; i++)
                        {
                            readingRecordsList.Add(BuildRecordListItem(page.Items[i]));
                        }
                    }
                    readingRecordsNextCursor = page.NextCursor;
                    UpdateLoadMoreButton(readingRecordsLoadMoreButton, page.HasMore);
                },
                onError: OnError);
        }

        private static VisualElement BuildRecordListItem(ReadingRecordItem item)
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("record-list-item");

            Label title = new Label(item.BookTitle ?? string.Empty);
            title.AddToClassList("record-list-item-title");
            card.Add(title);

            Label content = new Label(TruncateContent(item.Content, 200));
            content.AddToClassList("record-list-item-content");
            card.Add(content);

            VisualElement metaRow = new VisualElement();
            metaRow.AddToClassList("record-list-item-meta");
            Label likes = new Label($"♥ {item.LikeCount}");
            likes.AddToClassList("record-list-item-meta-item");
            metaRow.Add(likes);
            Label comments = new Label($"💬 {item.CommentCount}");
            comments.AddToClassList("record-list-item-meta-item");
            metaRow.Add(comments);
            Label views = new Label($"👁 {item.ViewCount}");
            views.AddToClassList("record-list-item-meta-item");
            metaRow.Add(views);
            card.Add(metaRow);

            return card;
        }

        private static void UpdateLoadMoreButton(Button button, bool hasMore)
        {
            if (button == null) return;
            if (hasMore)
            {
                button.RemoveFromClassList("hidden");
                button.SetEnabled(true);
            }
            else
            {
                button.AddToClassList("hidden");
            }
        }

        // ===== 캐러셀 드래그 스크롤 =====

        /// <summary>
        /// ScrollView에 마우스 좌클릭+드래그 가로 스크롤 핸들러 설치. 5px 이상 이동해야 드래그.
        /// state에 핸들러 인스턴스를 보관해서 UninstallCarouselDrag로 정확히 unregister 가능.
        /// </summary>
        private static void InstallCarouselDrag(ScrollView scrollView, CarouselDragState state)
        {
            const float dragThreshold = 5f;

            // 처음 한 번만 핸들러 인스턴스화 (Awake에선 capture할 ScrollView를 모르므로 여기서)
            if (state.onDown == null)
            {
                state.onDown = evt =>
                {
                    if (evt.button != 0) return;
                    state.dragging = false;
                    state.startMouse = evt.position;
                    state.startOffset = scrollView.scrollOffset;
                    state.activePointerId = evt.pointerId;
                };
                state.onMove = evt =>
                {
                    if (state.activePointerId != evt.pointerId) return;
                    Vector2 current = evt.position;
                    float dx = current.x - state.startMouse.x;
                    if (!state.dragging && Mathf.Abs(dx) >= dragThreshold)
                    {
                        state.dragging = true;
                        scrollView.CapturePointer(evt.pointerId);
                    }
                    if (state.dragging)
                    {
                        scrollView.scrollOffset = new Vector2(state.startOffset.x - dx, state.startOffset.y);
                        evt.StopPropagation();
                    }
                };
                state.onUp = evt =>
                {
                    if (state.activePointerId != evt.pointerId) return;
                    if (state.dragging && scrollView.HasPointerCapture(evt.pointerId))
                    {
                        scrollView.ReleasePointer(evt.pointerId);
                        evt.StopPropagation();
                    }
                    state.activePointerId = -1;
                    state.dragging = false;
                };
            }

            scrollView.RegisterCallback(state.onDown);
            scrollView.RegisterCallback(state.onMove);
            scrollView.RegisterCallback(state.onUp);
        }

        private static void UninstallCarouselDrag(ScrollView scrollView, CarouselDragState state)
        {
            if (state.onDown != null) scrollView.UnregisterCallback(state.onDown);
            if (state.onMove != null) scrollView.UnregisterCallback(state.onMove);
            if (state.onUp != null) scrollView.UnregisterCallback(state.onUp);
        }
    }

}
