using System;
using System.Collections.Generic;
using App.Application.Dashboard;
using App.Domain.Dashboard;
using App.Infrastructure.Catalogs;
using App.Infrastructure.Mock.Dashboard;
using UnityEngine;
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
        [SerializeField] private bool useMockRepositories = true;

        private UIDocument document;

        private const int CarouselItemLimit = 10;

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
        private Toggle dashboardEditAllowAllToggle;
        private ScrollView dashboardEditPostItList;
        private Button dashboardEditCloseButton;
        private Button dashboardEditCancelButton;
        private Button dashboardEditSubmitButton;
        private VisibilityScope selectedVisibility;
        private readonly HashSet<long> selectedAllowedAssetIds = new HashSet<long>();

        private enum UserListMode
        {
            Followers,
            Followings
        }

        private UserListMode currentUserListMode;

        private DashboardSnapshot currentSnapshot;
        private bool likeRequestInFlight;
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
        private EventCallback<ChangeEvent<bool>> onAllowAllToggleHandler;

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
            document = GetComponent<UIDocument>();
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
            onAllowAllToggleHandler = evt => RefreshWhitelistPickerSelections(evt.newValue);
        }

        private const string DimmedClass = "dashboard-root--dimmed";
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

        public void Configure(
            IDashboardRepository dashboardRepo,
            IGuestbookRepository guestbookRepo,
            ISpaceSettingsRepository spaceSettingsRepo,
            IFollowRepository followRepo,
            ILibraryViewRepository libraryRepo)
        {
            BuildUseCasesFromRepos(dashboardRepo, guestbookRepo, spaceSettingsRepo, followRepo, libraryRepo);
            configured = true;
        }

        private void OnEnable()
        {
            if (!configured)
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

            if (stylesheet != null && !root.styleSheets.Contains(stylesheet))
            {
                root.styleSheets.Add(stylesheet);
            }

            ApplyDimmedClass();

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
            if (eventsWired)
            {
                UnwireEvents();
                eventsWired = false;
            }
        }

        private void BuildMockUseCases()
        {
            // SceneInstaller가 없는 경우의 fallback. Mock으로 단독 동작.
            BuildUseCasesFromRepos(
                new MockDashboardRepository(),
                new MockGuestbookRepository(),
                new MockSpaceSettingsRepository(),
                new MockFollowRepository(),
                new MockLibraryViewRepository());
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
            dashboardEditAllowAllToggle = root.Q<Toggle>("dashboard-edit-allow-all-toggle");
            dashboardEditPostItList = root.Q<ScrollView>("dashboard-edit-postit-list");
            dashboardEditCloseButton = root.Q<Button>("dashboard-edit-close-button");
            dashboardEditCancelButton = root.Q<Button>("dashboard-edit-cancel-button");
            dashboardEditSubmitButton = root.Q<Button>("dashboard-edit-submit-button");
        }

        private void WireEvents()
        {
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
            if (dashboardEditAllowAllToggle != null) dashboardEditAllowAllToggle.RegisterValueChangedCallback(onAllowAllToggleHandler);

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
            if (dashboardEditAllowAllToggle != null && onAllowAllToggleHandler != null)
                dashboardEditAllowAllToggle.UnregisterValueChangedCallback(onAllowAllToggleHandler);

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
            loadDashboardUseCase.ExecuteForMine(OnDashboardLoaded, OnError);
        }

        private void LoadInitialData()
        {
            currentGuestbookPageIndex = 0;
            guestbookCursorHistory.Clear();
            guestbookCursorHistory.Add(null);

            if (viewMode == ViewMode.Mine)
            {
                // 본인 공간 — GET /v1/spaces/me 응답의 id 를 사용 (Repository 가 처리). source of truth = BE.
                loadDashboardUseCase.ExecuteForMine(OnDashboardLoaded, OnError);
            }
            else if (visitedSpaceId > 0)
            {
                Debug.Log($"[Dashboard] Loading visited space dashboard. spaceId={visitedSpaceId}");
                loadDashboardUseCase.Execute(visitedSpaceId, OnDashboardLoaded, OnError);
            }
            else
            {
                // visitedSpaceId 미설정 — 외부 caller (Search 등) 가 set 하기 전에는 호출 자체를 skip.
                // 이전에는 SerializeField default 1L 이 그대로 전파돼 GET /v1/spaces/1/dashboard 가 호출됨.
                Debug.LogWarning(
                    $"[Dashboard] visitedSpaceId 미설정 (viewMode={viewMode}) — dashboard 호출 skip. " +
                    "공간 카드 클릭으로 spaceId 가 set 된 후 OnVisitUser → loadDashboardUseCase.Execute 경로로 호출됩니다.");
            }
        }

        private void OnDashboardLoaded(DashboardSnapshot snapshot)
        {
            currentSnapshot = snapshot;
            RenderHeader(snapshot);
            LoadGuestbookPage(null);
            LoadRecentBooks();
            LoadRecentRecords();
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

        private static VisualElement BuildBookCell(OwnedBookItem item)
        {
            VisualElement cell = new VisualElement();
            cell.AddToClassList("carousel-cell");

            VisualElement cover = new VisualElement();
            cover.AddToClassList("book-cover");
            cell.Add(cover);

            Label title = new Label(item.Title ?? string.Empty);
            title.AddToClassList("book-cell-title");
            cell.Add(title);

            return cell;
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

            // 화이트리스트 현재 상태 로드
            updateWhitelistUseCase.LoadCurrent(
                onSuccess: whitelist =>
                {
                    selectedAllowedAssetIds.Clear();
                    if (whitelist.AllowedAssetIds != null)
                    {
                        for (int i = 0; i < whitelist.AllowedAssetIds.Count; i++)
                        {
                            selectedAllowedAssetIds.Add(whitelist.AllowedAssetIds[i]);
                        }
                    }
                    if (dashboardEditAllowAllToggle != null) dashboardEditAllowAllToggle.value = whitelist.AllowAll;
                    BuildWhitelistPicker();
                    RefreshWhitelistPickerSelections(whitelist.AllowAll);
                },
                onError: OnError);

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

        private void BuildWhitelistPicker()
        {
            if (dashboardEditPostItList == null) return;
            dashboardEditPostItList.Clear();
            IReadOnlyList<PostItStyle> styles = AvailablePostItStyles();
            for (int i = 0; i < styles.Count; i++)
            {
                long id = styles[i].AssetId;
                string displayName = string.IsNullOrEmpty(styles[i].DisplayName) ? $"스타일 {id}" : styles[i].DisplayName;
                VisualElement cell = new VisualElement();
                cell.AddToClassList("postit-style-cell");
                cell.userData = id;
                Label name = new Label(displayName);
                name.AddToClassList("postit-style-cell-name");
                cell.Add(name);
                cell.RegisterCallback<ClickEvent>(_ => OnWhitelistCellClicked(id, cell));
                dashboardEditPostItList.Add(cell);
            }
        }

        private void OnWhitelistCellClicked(long styleId, VisualElement cell)
        {
            if (dashboardEditAllowAllToggle != null && dashboardEditAllowAllToggle.value)
            {
                // allowAll 상태에선 개별 토글 무력화
                return;
            }

            if (selectedAllowedAssetIds.Contains(styleId))
            {
                selectedAllowedAssetIds.Remove(styleId);
                cell.RemoveFromClassList("selected");
            }
            else
            {
                selectedAllowedAssetIds.Add(styleId);
                cell.AddToClassList("selected");
            }
        }

        private void RefreshWhitelistPickerSelections(bool allowAll)
        {
            if (dashboardEditPostItList == null) return;
            foreach (VisualElement cell in dashboardEditPostItList.Children())
            {
                if (cell.userData is long id)
                {
                    bool selected = allowAll || selectedAllowedAssetIds.Contains(id);
                    if (selected) cell.AddToClassList("selected");
                    else cell.RemoveFromClassList("selected");
                }
            }
        }

        private void OnSubmitDashboardEdit()
        {
            if (currentSnapshot == null || dashboardEditSubmitButton == null) return;
            if (dashboardEditAllowAllToggle == null)
            {
                // 토글이 query에서 누락된 비정상 상황 — 보수적으로 submit 차단 (security default-deny).
                Debug.LogError("[Dashboard] allowAllToggle 미연결. 화이트리스트 fallback 안전 차단.");
                return;
            }
            dashboardEditSubmitButton.SetEnabled(false);

            string newIntroduction = dashboardEditIntroduction?.value ?? string.Empty;
            VisibilityScope newVisibility = selectedVisibility;
            bool allowAll = dashboardEditAllowAllToggle.value;
            List<long> allowedIds = new List<long>(selectedAllowedAssetIds);
            GuestbookPostItWhitelist whitelist = new GuestbookPostItWhitelist(allowAll, allowedIds);

            // sequential 실행 — 첫 실패에서 stop, 어디까지 저장됐는지 명시.
            updateIntroductionUseCase.Execute(newIntroduction,
                onSuccess: () =>
                {
                    updateGuestbookVisibilityUseCase.Execute(newVisibility,
                        onSuccess: () =>
                        {
                            updateWhitelistUseCase.Execute(whitelist,
                                onSuccess: () => FinishDashboardEdit(null, savedIntroduction: true, savedVisibility: true, savedWhitelist: true),
                                onError: err => FinishDashboardEdit(err, savedIntroduction: true, savedVisibility: true, savedWhitelist: false));
                        },
                        onError: err => FinishDashboardEdit(err, savedIntroduction: true, savedVisibility: false, savedWhitelist: false));
                },
                onError: err => FinishDashboardEdit(err, savedIntroduction: false, savedVisibility: false, savedWhitelist: false));
        }

        private void FinishDashboardEdit(DashboardApiError error, bool savedIntroduction, bool savedVisibility, bool savedWhitelist)
        {
            if (dashboardEditSubmitButton != null) dashboardEditSubmitButton.SetEnabled(true);
            if (error != null)
            {
                Debug.LogWarning($"[Dashboard] Edit 부분 실패: intro={savedIntroduction}, visibility={savedVisibility}, whitelist={savedWhitelist}. 에러={error.Code} {error.Message}");
                OnError(error);
                if (savedIntroduction || savedVisibility)
                {
                    // 부분 성공 시에도 화면은 갱신 (사용자가 어디까지 저장됐는지 다시 열어 확인 가능).
                    if (viewMode == ViewMode.Mine)
                    {
                        loadDashboardUseCase.ExecuteForMine(OnDashboardLoaded, OnError);
                    }
                }
                return;
            }
            CloseDashboardEditModal();
            if (viewMode == ViewMode.Mine)
            {
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

            // 방문 — 항상 표시 (와이어 페이지 2)
            Button visitBtn = new Button { text = "방문" };
            visitBtn.AddToClassList("user-row-action-button");
            visitBtn.clicked += () => OnVisitUser(user);
            actions.Add(visitBtn);

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
            // Mock 가정: user.Id == spaceId. Api 모드에선 user/space 도메인 분리되어 다른 값 가능.
            // (MR 114 review skh1205 [P2]) UserSummary.Id 는 follower/following API 의 사용자 ID — spaceId 보장 X.
            // UserSummary 에 spaceId 합류 또는 GET /v1/users/{id}/space 매핑 endpoint 추가 전까지 임시 방어:
            //   - mock 모드: 기존대로 user.Id == spaceId 가정 진행 (테스트 데이터 호환)
            //   - real API 모드: visit skip + 사용자 메시지 (spaceId 알 수 없으므로 잘못된 dashboard 호출 차단)
            if (currentSnapshot != null && user.Id == currentSnapshot.Space.OwnerUserId)
            {
                return;
            }
            if (!useMockRepositories)
            {
                Debug.LogWarning(
                    $"[Dashboard] OnVisitUser — real API 모드에서 user.Id({user.Id}) 를 spaceId 로 가정 불가. " +
                    "UserSummary 에 spaceId 합류 또는 user→space 매핑 API 가 필요합니다. visit skip.");
                return;
            }
            long targetSpaceId = user.Id;
            if (targetSpaceId <= 0)
            {
                Debug.LogWarning($"[Dashboard] OnVisitUser — invalid targetSpaceId={targetSpaceId} (user.Id 미설정). dashboard 호출 skip.");
                return;
            }
            viewMode = ViewMode.Visitor;
            visitedSpaceId = targetSpaceId;
            ApplyViewMode();
            Debug.Log($"[Dashboard] Visitor view (mock) → spaceId={targetSpaceId}");
            loadDashboardUseCase.Execute(targetSpaceId, OnDashboardLoaded, OnError);
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
