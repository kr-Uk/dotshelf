using System.Collections;
using System.Collections.Generic;
using App.Application.Notebook;
using Dotshelf.Scripts.Dashboard;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Networking;
using UnityEngine.UI;
using UIDocument = UnityEngine.UIElements.UIDocument;
using RuntimePanelUtils = UnityEngine.UIElements.RuntimePanelUtils;

namespace BookshelfPorting.Runtime
{
    public enum ExperienceFocusArea
    {
        QuarterView,
        Bookshelf,
        Guestbook,
        Notebook
    }

    public enum MainCameraViewMode
    {
        MainView,
        QuarterView
    }

    public class BookshelfExperienceManager : MonoBehaviour
    {
        private struct ReportCommentViewData
        {
            public ReportCommentViewData(long id, string authorLabel, string content, int likeCount, bool deleted)
            {
                Id = id;
                AuthorLabel = authorLabel;
                Content = content;
                LikeCount = likeCount;
                Deleted = deleted;
            }

            public long Id { get; }
            public string AuthorLabel { get; }
            public string Content { get; }
            public int LikeCount { get; }
            public bool Deleted { get; }
        }

        private const int GuestbookVisibleRows = 3;
        private const int GuestbookVisibleColumns = 3;
        private const int GuestbookMaxPageButtons = 4;
        private const int GuestbookMaxPinnedNotes = 96;
        private const float GuestbookPageFadeDuration = 0.18f;
        private const float GuestbookBoardPadding = 32f;
        private const float GuestbookNoteToPaginationGap = 28f;
        private const float GuestbookPaginationHeight = 40f;
        private const float GuestbookPaginationToPanelGap = 24f;
        private const float GuestbookInterfacePanelHeight = 184f;
        private const float HudInputTextLeftPadding = 34f;
        private const float HudInputTextRightPadding = 24f;
        private const float HudInputTextVerticalPadding = 10f;
        private const float OverlayButtonBottomPaddingExtra = 18f;
        private const int BookAddMaxResultCount = 64;
        private const int BookAddCoverSpriteCacheLimit = 48;
        private const float BookAddResultRowHeight = 124f;
        private const float BookAddResultViewportHeight = 260f;
        private const int BookSearchMaxResultCount = 96;
        private const float BookSearchResultRowHeight = 66f;
        private const float BookSearchResultViewportHeight = 260f;
        private const int ShelfManageMaxRowCount = 24;
        private const float ShelfManageRowHeight = 64f;
        private const float ShelfManageViewportHeight = 220f;
        private const int PendingMutationRetryLimit = 50;
        // Runtime HUD selector keeps one row of shelf chips readable without
        // turning the add/search overlays into full shelf-management screens.
        private const int BookshelfSelectorMaxButtons = 12;
        private const int BookRatingChoiceCount = 10;
        private const float BookRatingStep = 0.5f;
        private const float BookshelfTransientStatusDuration = 2.8f;
        private static readonly Color32 DeleteButtonBackground = new(196, 78, 72, 255);
        private static readonly Color32 DeleteButtonHighlighted = new(176, 62, 58, 255);
        private static readonly Color32 DeleteButtonConfirmBackground = new(176, 50, 48, 255);
        private static readonly Color32 DeleteButtonConfirmHighlighted = new(148, 39, 38, 255);
        private static readonly Color32 DeleteButtonDisabledBackground = new(224, 219, 214, 130);
        private static readonly Color32 DeleteButtonBorder = new(236, 178, 168, 190);
        private static readonly Color32 DeleteButtonDisabledText = new(142, 133, 124, 190);
        private static readonly Vector2 HudReferenceResolution = new(1600f, 1000f);
        private static readonly Vector2 GuestbookBoardSize = new(1380f, 960f);
        private static readonly Vector2 GuestbookPreviewCardSize = new(248f, 142f);
        private static readonly Vector2 GuestbookFocusedNoteBaseSize = new(246f, 156f);
        // hitArea local +X 방향 offset. native dashboard (visual skin 미적용) 케이스의 fallback —
        // hitArea collider 표면 바로 앞 3mm. visual skin (vintage 등) 이 적용되면 MountExternal
        // DashboardPanel 이 skin 의 renderer bounds.center 기준으로 위치를 재계산하므로 이 값은
        // 무시됨.
        private static readonly Vector3 ExternalDashboardPanelSurfaceOffset = new(0.003f, 0f, 0f);
        private static readonly Quaternion ExternalDashboardPanelLocalRotation = Quaternion.Euler(0f, 90f, 0f);

        // Visual skin 적용 시 panel 의 boardNormal-front 방향 추가 offset. z-fight 방지 + 자연스럽
        // 게 밀착되도록 0.003 m (3mm). 너무 크면 panel 이 frame 에서 분리되어 보임. 너무 작으면
        // z-fight.
        private const float ExternalDashboardPanelTinyForwardOffset = 0.003f;
        private const string DashboardVisualSkinChildName = "ThemeDashboardVisual"; // App.Bootstrap.DashboardVisualSkin.SkinChildName 과 동일
        // Flip local X so the room-facing UI Toolkit panel reads left-to-right on the board.
        private static readonly Vector2 ExternalDashboardPanelReferenceSize = new(1920f, 1080f);
        private const float ExternalDashboardPanelFallbackWorldScale = 0.00060f;
        private const float ExternalDashboardPanelHorizontalInset = 0.10f;
        private const float ExternalDashboardPanelVerticalInset = 0.06f;
        private static readonly Vector2[] GuestbookPreviewOffsets =
        {
            new Vector2(-336f, 154f),
            new Vector2(286f, 126f),
            new Vector2(-38f, 12f),
            new Vector2(294f, -96f)
        };
        private static readonly float[] GuestbookPreviewRotations = { -3.4f, 2.2f, -1.6f, 3.1f };
        private static readonly float[] GuestbookPreviewScales = { 1.02f, 0.99f, 1.03f, 0.98f };
        private static readonly string[] GuestbookPreviewMessages =
        {
            "Window light was perfect.",
            "Hyunwoo stopped by at noon.",
            "Return the blue poetry book.",
            "Back next Friday."
        };
        private static readonly string[] GuestbookPreviewCaptions = { "Minji", "Hyunwoo", "Seoyeon", "Host" };
        private static readonly Vector2[] GuestbookFocusedNoteAnchors =
        {
            new Vector2(-0.84f, 0.74f),
            new Vector2(-0.08f, 0.82f),
            new Vector2(0.72f, 0.66f),
            new Vector2(-0.52f, 0.14f),
            new Vector2(0.08f, 0.22f),
            new Vector2(0.78f, 0.06f),
            new Vector2(-0.74f, -0.62f),
            new Vector2(-0.02f, -0.48f),
            new Vector2(0.62f, -0.72f)
        };
        private static readonly float[] GuestbookFocusedNoteRotations = { -3.8f, 2.6f, -4.4f, 1.7f, -1.6f, 3.8f, -2.7f, 4.1f, -3.2f };
        private static readonly float[] GuestbookFocusedNoteScales = { 1.01f, 0.98f, 1.04f, 0.97f, 1.00f, 1.03f, 0.98f, 1.05f, 0.96f };
        private static Sprite roundedUiSprite;
        private static Texture2D roundedUiTexture;
        private static Sprite searchIconRingSprite;
        private static Texture2D searchIconRingTexture;

        private enum BookSearchFilter
        {
            All,
            Reading,
            Read,
            Liked
        }

        private enum BookAddBrowseMode
        {
            Search,
            Recommended,
            Ranking
        }

        private enum ShelfBookSortMode
        {
            Title,
            Author,
            ReadingStatus,
            Liked
        }

        private enum BookshelfOverlayMode
        {
            None,
            NameEdit,
            DeleteConfirm,
            Design,
            BookAdd,
            BookAddConfirm,
            Manage,
            UnsavedConfirm
        }

        private enum BookshelfBottomNavTab
        {
            Home,
            Search,
            Add,
            Edit
        }

        private enum FirstBookshelfTutorialMode
        {
            None,
            CreateFirstShelf,
            FeatureGuide
        }

        private readonly struct BookReportMutationSnapshot
        {
            public readonly string ReadingRecordId;
            public readonly string ReportTitle;
            public readonly string ReportBody;
            public readonly string ReportUpdatedLabel;
            public readonly string MemoPreview;
            public readonly bool ReportLiked;
            public readonly int ReportLikeCount;
            public readonly int ReportCommentCount;
            public readonly App.Domain.VisibilityScope ReportVisibility;
            public readonly bool IsPublic;
            public readonly App.Domain.ReadingStatus ReadingStatus;
            public readonly bool IsRead;

            public BookReportMutationSnapshot(BookEntity book, BookMetaData meta)
            {
                ReadingRecordId = book != null ? book.readingRecordId : null;
                ReportTitle = meta != null ? meta.reportTitle : book != null ? book.reportTitle : string.Empty;
                ReportBody = meta != null ? meta.reportBody : book != null ? book.reportBody : string.Empty;
                ReportUpdatedLabel = meta != null ? meta.reportUpdatedLabel : book != null ? book.reportUpdatedLabel : string.Empty;
                MemoPreview = meta != null ? meta.memoPreview : book != null ? book.memoPreview : string.Empty;
                ReportLiked = meta != null ? meta.reportLiked : book != null && book.reportLiked;
                ReportLikeCount = meta != null ? meta.reportLikeCount : book != null ? book.reportLikeCount : 0;
                ReportCommentCount = meta != null ? meta.reportCommentCount : book != null ? book.reportCommentCount : 0;
                ReportVisibility = meta != null ? meta.reportVisibility : book != null ? book.reportVisibility : App.Domain.VisibilityScope.Public;
                IsPublic = meta != null ? meta.isPublic : book == null || book.isPublic;
                ReadingStatus = meta != null ? meta.readingStatus : book != null ? book.readingStatus : App.Domain.ReadingStatus.Unread;
                IsRead = meta != null ? meta.isRead : book != null && book.isRead;
            }

            public void ApplyTo(BookMetaData meta)
            {
                if (meta == null)
                {
                    return;
                }

                meta.reportTitle = ReportTitle;
                meta.reportBody = ReportBody;
                meta.reportUpdatedLabel = ReportUpdatedLabel;
                meta.memoPreview = MemoPreview;
                meta.reportLiked = ReportLiked;
                meta.reportLikeCount = ReportLikeCount;
                meta.reportCommentCount = ReportCommentCount;
                meta.reportVisibility = ReportVisibility;
                meta.isPublic = IsPublic;
                meta.readingStatus = ReadingStatus;
                meta.isRead = IsRead;
            }

            public void ApplyTo(BookEntity book)
            {
                if (book == null)
                {
                    return;
                }

                book.readingRecordId = ReadingRecordId;
                book.reportTitle = ReportTitle;
                book.reportBody = ReportBody;
                book.reportUpdatedLabel = ReportUpdatedLabel;
                book.memoPreview = MemoPreview;
                book.reportLiked = ReportLiked;
                book.reportLikeCount = ReportLikeCount;
                book.reportCommentCount = ReportCommentCount;
                book.reportVisibility = ReportVisibility;
                book.isPublic = IsPublic;
                book.readingStatus = ReadingStatus;
                book.isRead = IsRead;
            }
        }

        private readonly struct BookStateMutationSnapshot
        {
            public readonly App.Domain.ReadingStatus ReadingStatus;
            public readonly bool IsRead;
            public readonly float OwnerRating;
            public readonly float AverageRating;
            public readonly int TotalLikeCount;
            public readonly int ReviewCount;
            public readonly bool HasAggregateBookStats;
            public readonly bool IsLiked;
            public readonly int DisplayRating;

            public BookStateMutationSnapshot(BookEntity book, BookMetaData meta)
            {
                ReadingStatus = meta != null ? meta.readingStatus : book != null ? book.readingStatus : App.Domain.ReadingStatus.Unread;
                IsRead = meta != null ? meta.isRead : book != null && book.isRead;
                OwnerRating = meta != null ? meta.ownerRating : book != null ? book.ownerRating : 0f;
                AverageRating = meta != null ? meta.averageRating : book != null ? book.averageRating : 0f;
                TotalLikeCount = meta != null ? meta.totalLikeCount : book != null ? book.totalLikeCount : 0;
                ReviewCount = meta != null ? meta.reviewCount : book != null ? book.reviewCount : 0;
                HasAggregateBookStats = meta != null ? meta.hasAggregateBookStats : book != null && book.hasAggregateBookStats;
                IsLiked = meta != null ? meta.isLiked : book != null && book.isLiked;
                DisplayRating = book != null ? book.rating : Mathf.RoundToInt(Mathf.Clamp(HasAggregateBookStats ? AverageRating : OwnerRating, 0f, 5f));
            }

            public void ApplyTo(BookMetaData meta)
            {
                if (meta == null)
                {
                    return;
                }

                meta.readingStatus = ReadingStatus;
                meta.isRead = IsRead;
                meta.ownerRating = OwnerRating;
                meta.averageRating = AverageRating;
                meta.totalLikeCount = TotalLikeCount;
                meta.reviewCount = ReviewCount;
                meta.hasAggregateBookStats = HasAggregateBookStats;
                meta.isLiked = IsLiked;
            }

            public void ApplyTo(BookEntity book)
            {
                if (book == null)
                {
                    return;
                }

                book.readingStatus = ReadingStatus;
                book.isRead = IsRead;
                book.ownerRating = OwnerRating;
                book.averageRating = AverageRating;
                book.totalLikeCount = TotalLikeCount;
                book.reviewCount = ReviewCount;
                book.hasAggregateBookStats = HasAggregateBookStats;
                book.isLiked = IsLiked;
                book.rating = DisplayRating;
            }

            public bool Matches(BookEntity book, BookMetaData meta)
            {
                if (book == null)
                {
                    return false;
                }

                return book.readingStatus == ReadingStatus &&
                       book.isRead == IsRead &&
                       Mathf.Approximately(book.ownerRating, OwnerRating) &&
                       Mathf.Approximately(book.averageRating, AverageRating) &&
                       book.totalLikeCount == TotalLikeCount &&
                       book.reviewCount == ReviewCount &&
                       book.hasAggregateBookStats == HasAggregateBookStats &&
                       book.isLiked == IsLiked &&
                       (meta == null || MatchesMeta(meta));
            }

            private bool MatchesMeta(BookMetaData meta)
            {
                return meta.readingStatus == ReadingStatus &&
                       meta.isRead == IsRead &&
                       Mathf.Approximately(meta.ownerRating, OwnerRating) &&
                       Mathf.Approximately(meta.averageRating, AverageRating) &&
                       meta.totalLikeCount == TotalLikeCount &&
                       meta.reviewCount == ReviewCount &&
                       meta.hasAggregateBookStats == HasAggregateBookStats &&
                       meta.isLiked == IsLiked;
            }
        }

        private sealed class BookMutationIdentitySnapshot
        {
            public BookMutationIdentitySnapshot(BookEntity book)
            {
                UserBookId = Normalize(book != null ? book.userBookId : null);
                BookshelfItemId = Normalize(book != null ? book.bookshelfItemId : null);
                BookshelfId = Normalize(book != null ? book.bookshelfId : null);
                BookshelfSectionId = Normalize(book != null ? book.bookshelfSectionId : null);
            }

            private string UserBookId { get; }
            private string BookshelfItemId { get; }
            private string BookshelfId { get; }
            private string BookshelfSectionId { get; }

            public bool Matches(BookEntity book)
            {
                return book != null &&
                       MatchesValue(UserBookId, book.userBookId) &&
                       MatchesValue(BookshelfItemId, book.bookshelfItemId) &&
                       MatchesValue(BookshelfId, book.bookshelfId) &&
                       MatchesValue(BookshelfSectionId, book.bookshelfSectionId);
            }

            private static string Normalize(string value)
            {
                return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            }

            private static bool MatchesValue(string expected, string actual)
            {
                return string.IsNullOrWhiteSpace(expected) ||
                       string.Equals(expected, Normalize(actual), System.StringComparison.Ordinal);
            }
        }

        private sealed class PendingMutationRetry
        {
            public PendingMutationRetry(string key, string operationName, string message, System.Action retryAction)
            {
                Key = key;
                OperationName = operationName;
                Message = message;
                RetryAction = retryAction;
                AttemptCount = 1;
            }

            public string Key { get; }
            public string OperationName { get; private set; }
            public string Message { get; private set; }
            public System.Action RetryAction { get; private set; }
            public int AttemptCount { get; private set; }

            public void Update(string operationName, string message, System.Action retryAction)
            {
                OperationName = operationName;
                Message = message;
                RetryAction = retryAction;
                AttemptCount++;
            }
        }

        private sealed class BookAddCatalogItem
        {
            public BookAddCatalogItem(
                string title,
                string author,
                string publisher,
                string summary,
                BookOrientation orientation = BookOrientation.Front,
                string bookId = null,
                string source = null,
                string isbn13 = null,
                string isbn10 = null,
                string publishedDate = null,
                string coverImageUrl = null,
                string sourceUrl = null,
                long viewCount = 0L,
                long likeCount = 0L,
                long bookshelfAddCount = 0L,
                decimal averageRating = 0m,
                int reviewCount = 0)
            {
                Title = title;
                Author = author;
                Publisher = publisher;
                Summary = summary;
                Orientation = orientation;
                BookId = bookId;
                Source = source;
                Isbn13 = isbn13;
                Isbn10 = isbn10;
                PublishedDate = publishedDate;
                CoverImageUrl = coverImageUrl;
                SourceUrl = sourceUrl;
                ViewCount = viewCount;
                LikeCount = likeCount;
                BookshelfAddCount = bookshelfAddCount;
                AverageRating = averageRating;
                ReviewCount = reviewCount;
            }

            public string Title { get; }
            public string Author { get; }
            public string Publisher { get; }
            public string Summary { get; }
            public BookOrientation Orientation { get; }
            public string BookId { get; }
            public string Source { get; }
            public string Isbn13 { get; }
            public string Isbn10 { get; }
            public string PublishedDate { get; }
            public string CoverImageUrl { get; }
            public string SourceUrl { get; }
            public long ViewCount { get; }
            public long LikeCount { get; }
            public long BookshelfAddCount { get; }
            public decimal AverageRating { get; }
            public int ReviewCount { get; }
        }

        private sealed class GuestbookNoteData
        {
            public string authorName;
            public string message;
            public string timestampLabel;
            public Color color;
        }

        [SerializeField] private BookshelfState state = null;
        [SerializeField] private CameraController cameraController = null;
        [SerializeField] private BookInteractionController interactionController = null;
        [SerializeField] private BookshelfGenerator bookshelfGenerator = null;
        [SerializeField] private BookshelfRuntimeDataStore dataStore = null;
        [SerializeField] private BookshelfApiSyncController bookshelfApiSyncController = null;
        [SerializeField] private NotebookScreenController notebookScreenController = null;
        [SerializeField] private NotebookUiSceneLoader notebookUiSceneLoader = null;
        private NotebookScreenController subscribedNotebookScreenController;

        private SearchNotebookBooksUseCase searchNotebookBooksUseCase;
        private LoadNotebookMarketBooksUseCase loadNotebookMarketBooksUseCase;
        private AddNotebookBookToShelfUseCase addNotebookBookToShelfUseCase;
        private NotebookRuntimeUseCase notebookRuntimeUseCase;

        private Canvas hudCanvas;
        private bool isNotebookFocusWaitingForScene;
        private int notebookFocusRequestVersion;
        private Button overlayPreviousFocusButton;
        private Button overlayNextFocusButton;
        private Button bookshelfEditModeButton;
        private GameObject bookshelfIndexPanel;
        private Text bookshelfIndexText;
        private RectTransform bookshelfIndexDotsRoot;
        private GameObject firstBookshelfTutorialBackdrop;
        private GameObject firstBookshelfTutorialPanel;
        private Text firstBookshelfTutorialTitleText;
        private Text firstBookshelfTutorialMessageText;
        private Text firstBookshelfTutorialStepsText;
        private Button firstBookshelfTutorialAddButton;
        private Button emptyBookshelfAddButton;
        private bool isFirstBookshelfTutorialOpen;
        private FirstBookshelfTutorialMode firstBookshelfTutorialMode = FirstBookshelfTutorialMode.None;
        private bool pendingFirstBookshelfFeatureGuide;
        private bool hasCompletedFirstBookshelfFeatureGuide;
        private bool isFirstBookshelfCreateInProgress;
        private bool isBookshelfCreateRequestInFlight;
        private bool isEmptyBookshelfTutorialEntryPending;
        private int firstBookshelfFeatureGuideStepIndex;
        private Button addBookshelfButton;
        private Button openBookshelfNameEditButton;
        private Button openBookshelfDesignButton;
        private Button deleteBookshelfButton;
        private Button openBookAddButton;
        private Button openBookSearchButton;
        private Button openBookshelfManageButton;
        private GameObject bookshelfNameModalBackdrop;
        private GameObject bookshelfNameEditPanel;
        private Text bookshelfNameEditLabelText;
        private InputField bookshelfNameInputField;
        private Button saveBookshelfNameButton;
        private Button closeBookshelfNameEditButton;
        private GameObject bookshelfDeleteConfirmPanel;
        private Text bookshelfDeleteConfirmTitleText;
        private Text bookshelfDeleteConfirmMessageText;
        private Button confirmDeleteBookshelfButton;
        private Button cancelDeleteBookshelfButton;
        private GameObject bookshelfDesignPanel;
        private Text bookshelfDesignTitleText;
        private Text bookshelfDesignHintText;
        private Button defaultBookshelfDesignButton;
        private Button warmBookshelfDesignButton;
        private Button darkBookshelfDesignButton;
        private Button closeBookshelfDesignButton;
        private GameObject bookAddPanel;
        private Text bookAddTitleText;
        private Text bookAddHintText;
        private Text bookAddStatusText;
        private Text bookAddShelfSelectorLabelText;
        private InputField bookAddSearchInputField;
        private Button bookAddSearchModeButton;
        private Button bookAddRecommendedModeButton;
        private Button bookAddRankingModeButton;
        private Button closeBookAddButton;
        private readonly Button[] bookAddShelfSelectorButtons = new Button[BookshelfSelectorMaxButtons];
        private RectTransform bookAddRowsContent;
        private ScrollRect bookAddScrollRect;
        private Scrollbar bookAddScrollbar;
        private readonly Button[] bookAddResultButtons = new Button[BookAddMaxResultCount];
        private readonly Text[] bookAddResultTexts = new Text[BookAddMaxResultCount];
        private readonly Image[] bookAddResultCoverImages = new Image[BookAddMaxResultCount];
        private readonly int[] bookAddResultCatalogIndices = new int[BookAddMaxResultCount];
        private readonly List<BookAddCatalogItem> runtimeBookAddCatalog = new List<BookAddCatalogItem>();
        private readonly List<BookAddCatalogItem> apiBookAddCatalog = new List<BookAddCatalogItem>();
        private readonly Dictionary<string, Sprite> bookAddCoverSpriteCache = new Dictionary<string, Sprite>();
        private readonly LinkedList<string> bookAddCoverSpriteLru = new LinkedList<string>();
        private readonly Dictionary<Image, Coroutine> bookAddCoverLoadCoroutines = new Dictionary<Image, Coroutine>();
        private readonly Dictionary<Image, string> bookAddCoverImageUrls = new Dictionary<Image, string>();
        private GameObject bookAddConfirmPanel;
        private Text bookAddConfirmTitleText;
        private Text bookAddConfirmMetaText;
        private Text bookAddConfirmSummaryText;
        private Button bookAddConfirmUnreadButton;
        private Button bookAddConfirmReadingButton;
        private Button bookAddConfirmReadButton;
        private Button bookAddConfirmSpineButton;
        private Button bookAddConfirmAngledButton;
        private Button bookAddConfirmFrontButton;
        private Button confirmBookAddButton;
        private Button cancelBookAddConfirmButton;
        private GameObject bookSearchPanel;
        private Text bookSearchTitleText;
        private Text bookSearchStatusText;
        private Text bookSearchShelfSelectorLabelText;
        private InputField bookSearchInputField;
        private Button closeBookSearchButton;
        private Button bookSearchAllFilterButton;
        private Button bookSearchReadingFilterButton;
        private Button bookSearchReadFilterButton;
        private Button bookSearchLikedFilterButton;
        private readonly Button[] bookSearchShelfSelectorButtons = new Button[BookshelfSelectorMaxButtons];
        private RectTransform bookSearchRowsContent;
        private ScrollRect bookSearchScrollRect;
        private Scrollbar bookSearchScrollbar;
        private readonly Button[] bookSearchResultButtons = new Button[BookSearchMaxResultCount];
        private readonly Text[] bookSearchResultTexts = new Text[BookSearchMaxResultCount];
        private readonly List<BookEntity> bookSearchResults = new List<BookEntity>();
        private GameObject bookshelfManagePanel;
        private Text bookshelfManageTitleText;
        private Text bookshelfManageSummaryText;
        private Text bookshelfManageStatusText;
        private RectTransform bookshelfManageRowsContent;
        private ScrollRect bookshelfManageScrollRect;
        private Scrollbar bookshelfManageScrollbar;
        private readonly Button[] bookshelfManageRowButtons = new Button[ShelfManageMaxRowCount];
        private readonly Text[] bookshelfManageRowTexts = new Text[ShelfManageMaxRowCount];
        private readonly BookshelfManageDragHandle[] bookshelfManageDragHandles = new BookshelfManageDragHandle[ShelfManageMaxRowCount];
        private readonly Image[] bookshelfManageDragHandleImages = new Image[ShelfManageMaxRowCount];
        private readonly Dictionary<string, PendingMutationRetry> pendingMutationRetries = new Dictionary<string, PendingMutationRetry>();
        private readonly List<string> pendingMutationRetryOrder = new List<string>();
        private Button shelfManageSortTitleButton;
        private Button shelfManageSortAuthorButton;
        private Button shelfManageSortStatusButton;
        private Button shelfManageSortLikedButton;
        private Button shelfManagePreviousButton;
        private Button shelfManageNextButton;
        private Button shelfManageVisibilityButton;
        private Button shelfManageRenameButton;
        private Button shelfManageAddButton;
        private Button shelfManageDeleteButton;
        private Button shelfManageCloseButton;
        private GameObject bookshelfUnsavedConfirmPanel;
        private Text bookshelfUnsavedConfirmTitleText;
        private Text bookshelfUnsavedConfirmMessageText;
        private Button confirmBookshelfUnsavedSaveButton;
        private Button cancelBookshelfUnsavedSaveButton;
        private GameObject deleteResultBackdrop;
        private GameObject deleteResultDialogPanel;
        private Text deleteResultTitleText;
        private Text deleteResultMessageText;
        private Button retryDeleteResultDialogButton;
        private Button closeDeleteResultDialogButton;
        private GameObject bookshelfStatusPanel;
        private Text bookshelfStatusTitleText;
        private Text bookshelfStatusMessageText;
        private Button bookshelfStatusAddButton;
        private int bookshelfNameInputIndex = -1;
        private int pendingDeleteBookshelfIndex = -1;
        private int pendingBookAddCatalogIndex = -1;
        private int pendingBookAddBookshelfIndex = -1;
        private BookAddCatalogItem pendingBookAddCatalogItem;
        private BookOrientation pendingBookAddOrientation = BookOrientation.Front;
        private App.Domain.ReadingStatus pendingBookAddReadingStatus = App.Domain.ReadingStatus.Unread;
        private bool isBookAddPlacementSelectionActive;
        private bool isBookshelfNameEditPanelOpen;
        private bool isBookshelfDeleteConfirmPanelOpen;
        private bool isBookshelfDesignPanelOpen;
        private bool isBookAddPanelOpen;
        private bool isBookAddConfirmPanelOpen;
        private bool isBookSearchPanelOpen;
        private bool isBookshelfManagePanelOpen;
        private bool isBookshelfUnsavedConfirmPanelOpen;
        private bool isDeleteResultDialogOpen;
        private bool deleteResultDialogIsSuccess;
        private string deleteResultDialogTitle = string.Empty;
        private string deleteResultDialogMessage = string.Empty;
        private string deleteResultRetryLabel = "재시도";
        private System.Action deleteResultRetryAction;
        private bool hasUnsavedBookshelfEditChanges;
        private System.Action pendingBookshelfNavigationAfterSave;
        private BookSearchFilter activeBookSearchFilter = BookSearchFilter.All;
        private bool isBookshelfTransientStatusVisible;
        private bool bookshelfTransientStatusIsError;
        private string bookshelfTransientStatusTitle = string.Empty;
        private string bookshelfTransientStatusMessage = string.Empty;
        private float bookshelfTransientStatusExpiresAt;
        private string bookAddFeedbackMessage = string.Empty;
        // LocalMock was removed; this remains installer-controlled until the
        // remaining local fallback UI branches are deleted in a focused cleanup.
        private bool usesHttpApiMode;
        private bool shouldResetBookAddScrollToTop;
        private bool shouldResetBookSearchScrollToTop;
        private bool isBookAddApiSearchPending;
        private int bookAddApiSearchVersion;
        private string bookAddApiSearchQuery = string.Empty;
        private string bookAddApiSearchMessage = string.Empty;
        private BookAddBrowseMode activeBookAddBrowseMode = BookAddBrowseMode.Search;
        private BookAddBrowseMode bookAddApiCatalogMode = BookAddBrowseMode.Search;
        private int bookshelfManageDragSourceIndex = -1;
        private int bookshelfManageDragTargetIndex = -1;
        private bool bookshelfManageScrollWasEnabled;
        private readonly List<Image> bookshelfIndexDots = new List<Image>();

        private GameObject guestbookPanel;
        private GameObject guestbookPreviewPanel;
        private Canvas guestbookBoardCanvas;

        // (v1.0 dashboard 통합 — MR #61) GuestbookBoard 자리에 우리 새 dashboard prefab을 띄우는 모드.
        // useExternalDashboardPanel = true 일 때 옛 guestbookPanel/Preview 생성을 skip하고 externalDashboardPanel만 사용.
        // false면 종전과 동일 (옛 방명록 UI). cleanup은 phase 5 별도 PR에서.
        [Header("Dashboard Integration (v1.0)")]
        [Tooltip("GuestbookBoard 자리에 새 UI Toolkit dashboard prefab을 띄우는 모드. " +
                "MR #61 시점에 UI Toolkit World Space input 라우팅이 BookshelfPorting raycast와 충돌해 " +
                "click 동작 안 함. input fix 후 true로 토글 (default false로 옛 guestbookBoardCanvas 유지).")]
        [SerializeField] private bool useExternalDashboardPanel = false;
        [Tooltip("GuestbookBoard 자리에 띄울 새 dashboard prefab 인스턴스. 보통 scene root에 두고 코드가 reparent.")]
        [SerializeField] private GameObject externalDashboardPanel;

        // SerializeField 의도 보존: useExternalDashboardPanel 자체는 Inspector 가 박은 값을 그대로 유지하고,
        // 회귀 fallback 으로만 켜는 runtime override flag 를 분리. 두 값을 모두 보고 효과 판정
        // (ShouldUseExternalDashboardPanel). MR !159 review #3 응답.
        private bool runtimeUseExternalDashboardPanel;
        private bool ShouldUseExternalDashboardPanel => useExternalDashboardPanel || runtimeUseExternalDashboardPanel;
        private UnityEngine.UIElements.VisualElement focusedDashboardPressedElement;
        // MountExternalDashboardPanel 진단 log throttle — 첫 호출 + skin attach/detach 상태 변경
        // 시점 1회씩 log. visual skin 적용/제거 시 panel 위치 정책이 바뀌므로 그 시점에 재 log.
        private bool externalDashboardPanelMountLogged;
        private bool externalDashboardPanelMountLastHadSkin;

        [SerializeField] private bool guestbookOwnerMode = true;
        private bool isGuestbookPublic = true;
        private int guestbookVisitorCount = 123;
        private int guestbookLikeCount = 45;
        private readonly List<GuestbookNoteData> guestbookNotes = new List<GuestbookNoteData>();
        private readonly RectTransform[] guestbookNoteCards = new RectTransform[12];
        private readonly Text[] guestbookNoteMessageTexts = new Text[12];
        private readonly Text[] guestbookNoteAuthorTexts = new Text[12];
        private readonly Button[] guestbookReplyButtons = new Button[12];
        private readonly Button[] guestbookPageButtons = new Button[GuestbookMaxPageButtons];
        private readonly int[] guestbookPageButtonPageIndices = new int[GuestbookMaxPageButtons];
        private Text guestbookVisitorStatsText;
        private Text guestbookLikeStatsText;
        private InputField guestbookInputField;
        private Button guestbookSubmitButton;
        private Text guestbookCharCountText;
        private Text guestbookBoardStatusText;
        private Text guestbookEmptyStateText;
        private Text guestbookPrivacyText;
        private Button guestbookVisibilityToggleButton;
        private Button guestbookPreviousPageButton;
        private Button guestbookNextPageButton;
        private RectTransform guestbookInterfacePanelRect;
        private RectTransform guestbookHeaderRowRect;
        private RectTransform guestbookPaginationRect;
        private RectTransform guestbookInputRowRect;
        private RectTransform guestbookGridRect;
        private RectTransform guestbookGridOverlayRect;
        private CanvasGroup guestbookGridCanvasGroup;
        private Coroutine guestbookPageFadeCoroutine;
        private int guestbookCurrentPageIndex;
        private int guestbookColumnCount = 3;
        private int guestbookCardsPerPage = 9;
        private Vector2 guestbookCurrentCardSize = GuestbookFocusedNoteBaseSize;
        private readonly Color[] guestbookNoteColors =
        {
            new Color32(255, 249, 196, 255),
            new Color32(232, 245, 233, 255),
            new Color32(243, 229, 245, 255)
        };

        private GameObject bookDetailPanel;
        private BookDetailPanelController bookDetailPanelController;
        private GameObject bookFocusToolbarPanel;
        private Button bookFocusDetailButton;
        private Button bookFocusReportButton;
        private Button bookFocusRatingButton;
        private Button bookFocusWriteReportButton;
        private Text bookFocusRatingText;
        private GameObject bookReviewWritePanel;
        private Text bookReviewWriteTitleText;
        private Text bookReviewWriteBookText;
        private Text bookReviewRatingText;
        private Text bookReviewStatusText;
        private InputField bookReviewContentInputField;
        private Button saveBookReviewButton;
        private Button cancelBookReviewButton;
        private readonly Button[] bookReviewRatingButtons = new Button[BookRatingChoiceCount];
        private bool isBookReviewWritePanelOpen;
        private bool isBookReviewSaving;
        private string bookReviewStatusMessage = string.Empty;
        private bool bookReviewStatusIsError;
        private BookEntity activeReviewBook;
        private float stagedBookReviewRating;
        private GameObject bookReportWritePanel;
        private Text bookReportWriteTitleText;
        private Text bookReportWriteBookText;
        private InputField bookReportTitleInputField;
        private InputField bookReportBodyInputField;
        private Button bookReportPublicButton;
        private Button bookReportFriendsButton;
        private Button bookReportPrivateButton;
        private Button saveBookReportButton;
        private Button cancelBookReportButton;
        private GameObject bookReportReadPanel;
        private Text bookReportReadTitleText;
        private Text bookReportReadMetaText;
        private Text bookReportReadBodyText;
        private Text bookReportCommentsTitleText;
        private Text bookReportCommentsText;
        private Text bookReportSocialText;
        private Button bookReportLikeButton;
        private InputField bookReportCommentInputField;
        private Button bookReportCommentButton;
        private Button editBookReportButton;
        private Button toggleReportVisibilityButton;
        private Button deleteBookReportButton;
        private Button closeBookReportReadButton;
        private bool isBookReportWritePanelOpen;
        private bool isBookReportReadPanelOpen;
        private App.Domain.VisibilityScope stagedBookReportVisibility = App.Domain.VisibilityScope.Public;
        private BookEntity activeReportBook;
        private readonly List<ReportCommentViewData> activeReportComments = new List<ReportCommentViewData>();
        private BookEntity activeReportCommentsBook;
        private bool activeReportCommentsLoaded;
        private long nextLocalReportCommentId = -1L;
        private GameObject bookEditPanel;
        private Text bookEditEyebrowText;
        private Text bookEditTitleText;
        private Text bookEditMetaText;
        private Text bookEditHintText;
        private Button deleteBookButton;
        private Button closeBookEditButton;
        private bool isBookEditPanelOpen;
        private BookEntity pendingDeleteBook;
        private BookEntity pendingDeleteReportBook;
        private GameObject sectionEditPanel;
        private Text sectionEditEyebrowText;
        private Text sectionEditTitleText;
        private Text sectionEditMetaText;
        private Text sectionEditHintText;
        private Button alignLeftButton;
        private Button alignCenterButton;
        private Button alignRightButton;
        private Button closeSectionEditButton;
        private ShelfSection activeEditSection;
        private Button openBookPlacementEditorButton;
        private GameObject bookPlacementEditorPanel;
        private Button placementSpineButton;
        private Button placementAngledButton;
        private Button placementFrontButton;
        private Button cancelPlacementButton;
        private Button confirmPlacementButton;
        private BookEntity placementEditBook;
        private BookOrientation placementOriginalOrientation;
        private BookOrientation placementStagedOrientation;
        private bool isBookPlacementEditorOpen;

        private GameObject hamburgerMenuPanel;
        private Button hamburgerButton;
        private Button focusBackButton;
        private bool isHamburgerMenuOpen;
        private GameObject bookshelfBottomNavPanel;
        private Button bookshelfNavHomeButton;
        private Button bookshelfNavSearchButton;
        private Button bookshelfNavAddButton;
        private Button bookshelfNavEditButton;
        private int lastResponsiveLayoutWidth = -1;
        private int lastResponsiveLayoutHeight = -1;
        private readonly BookshelfOverlayStateController overlayStateController = new BookshelfOverlayStateController();

        public ExperienceFocusArea CurrentFocusArea { get; private set; } = ExperienceFocusArea.QuarterView;
        public bool IsBookshelfFocused => CurrentFocusArea == ExperienceFocusArea.Bookshelf;
        public bool IsBookshelfEditMode { get; private set; }
        public bool IsBookAddPlacementSelectionActive => isBookAddPlacementSelectionActive;
        public bool IsBookshelfWorldInputBlocked => CurrentFocusArea == ExperienceFocusArea.Bookshelf && HasBlockingBookshelfOverlay();
        public BookEntity ActiveDetailBook { get; private set; }

        // Tracks which of the two main-scene poses (MainView default, QuarterView via
        // toggle) is currently active. Independent of bookshelf/guestbook/notebook focus —
        // those don't change which main pose the camera should return to next.
        private MainCameraViewMode currentMainViewMode = MainCameraViewMode.MainView;
        private MainCameraViewMode bookshelfReturnMainViewMode = MainCameraViewMode.MainView;
        public MainCameraViewMode CurrentMainViewMode => currentMainViewMode;

        // Set by the onboarding flow during Step 3. Originally only gated the
        // floating mode-change / settings chrome (SettingsScreenController),
        // but the onboarding "first book" experience now also hides the
        // bookshelf-focus chrome (back button, top index panel, left status
        // panel, right action buttons, bottom nav, hamburger menu, edit-mode
        // button, page-flip arrows) and auto-opens the book-add panel. The
        // setter wires those side-effects through OnOnboardingStep3StateChanged
        // so any caller flipping this flag does not need to know about them.
        // Cleared back to false after onboarding completes — the regular
        // per-frame layout sync resumes naturally.
        public bool IsOnboardingStep3Active
        {
            get => isOnboardingStep3Active;
            set
            {
                if (isOnboardingStep3Active == value)
                {
                    return;
                }
                isOnboardingStep3Active = value;
                OnOnboardingStep3StateChanged(value);
            }
        }
        private bool isOnboardingStep3Active;
        private Coroutine onboardingAutoOpenBookAddRoutine;

        // Set by RoomEditModeController while the player is in room-decoration edit
        // mode. While true, the main scene's input handlers (click-to-focus on
        // bookshelf / notebook / guestbook, F1–F4 shortcuts, mode toggles) are
        // short-circuited so the only interaction the player can perform is the
        // edit-mode select / rotate / delete that the edit overlay itself drives.
        public bool AreSceneInteractionsSuppressed { get; set; }

        public void Configure(
            BookshelfState bookshelfState,
            CameraController controller,
            BookInteractionController interaction,
            BookshelfRuntimeDataStore runtimeDataStore,
            BookshelfGenerator generator)
        {
            if (cameraController != null)
            {
                cameraController.ModeChanged -= HandleCameraModeChanged;
                cameraController.TransitionCompleted -= HandleCameraTransitionCompleted;
            }

            if (bookshelfGenerator != null)
            {
                bookshelfGenerator.BookshelfSwitchStateChanged -= HandleBookshelfSwitchStateChanged;
                bookshelfGenerator.Generated -= HandleBookshelfGenerated;
            }

            state = bookshelfState;
            cameraController = controller;
            interactionController = interaction;
            bookshelfGenerator = generator;
            dataStore = runtimeDataStore;

            if (isActiveAndEnabled)
            {
                if (cameraController != null)
                {
                    cameraController.ModeChanged += HandleCameraModeChanged;
                    cameraController.TransitionCompleted += HandleCameraTransitionCompleted;
                    SyncFromCameraMode(cameraController.CurrentMode);
                }

                if (bookshelfGenerator != null)
                {
                    bookshelfGenerator.BookshelfSwitchStateChanged += HandleBookshelfSwitchStateChanged;
                    bookshelfGenerator.Generated += HandleBookshelfGenerated;
                }
            }
        }

        public void ConfigureBookshelfApiSync(BookshelfApiSyncController syncController)
        {
            bookshelfApiSyncController = syncController;
        }

        public void ConfigureApiMode(bool usesHttpApi)
        {
            usesHttpApiMode = usesHttpApi;
        }

        public void ConfigureNotebookUiSceneLoader(NotebookUiSceneLoader sceneLoader)
        {
            notebookUiSceneLoader = sceneLoader;
        }

        public void NotifyBookshelfApiSynced()
        {
            RefreshBookshelfIndexIndicator(CurrentFocusArea == ExperienceFocusArea.Bookshelf && (state == null || !state.IsViewingBook));
            RefreshHud();
        }

        public void NotifyBookPlacementCommitted(BookEntity book)
        {
            SyncBookPlacementOrRefresh(book, "책 배치 저장");
        }

        private void Awake()
        {
            EnsureEventSystem();
            EnsureHud();
            EnsureExternalDashboardPanelWired();
        }

        private void Start()
        {
            if (bookshelfGenerator == null)
            {
                bookshelfGenerator = FindFirstObjectByType<BookshelfGenerator>();
                if (isActiveAndEnabled && bookshelfGenerator != null)
                {
                    bookshelfGenerator.BookshelfSwitchStateChanged -= HandleBookshelfSwitchStateChanged;
                    bookshelfGenerator.BookshelfSwitchStateChanged += HandleBookshelfSwitchStateChanged;
                    bookshelfGenerator.Generated -= HandleBookshelfGenerated;
                    bookshelfGenerator.Generated += HandleBookshelfGenerated;
                }
            }

            ConfigureNotebookScreen();
            EnsureGuestbookBoardUi();
            RefreshHud();
        }

        private void Update()
        {
            ApplyResponsiveFocusOverlayLayout();
            ExpireBookshelfTransientStatusIfNeeded();
            SyncExternalDashboardPanelPresentation();

            if (AreSceneInteractionsSuppressed)
            {
                // Edit mode owns input on the main scene — bail before any focus
                // shortcut, click-to-focus, or hit test runs.
                return;
            }

            // (v1.0 dashboard 통합 — MR #61) Guestbook focus 중이고 외부 dashboard prefab 모드면
            // UI Toolkit이 자체 입력 처리. BookshelfExperienceManager의 자체 raycast가
            // dashboard 안 클릭을 가로채지 않도록 input 처리 skip.
            // (focus 진입/이탈은 다른 hit area 또는 ESC로 처리됨.)
            if (ShouldUseExternalDashboardPanel && CurrentFocusArea == ExperienceFocusArea.Guestbook)
            {
                HandleFocusedDashboardPanelFallbackInput();
                return;
            }

            if (HandleFocusShortcuts())
            {
                return;
            }

            if (cameraController != null && cameraController.IsTransitioning)
            {
                return;
            }

            if (Mouse.current == null)
            {
                return;
            }

            var pointer = Mouse.current.position.ReadValue();
            if (isBookAddPlacementSelectionActive)
            {
                if (IsPointerOverUi())
                {
                    bookshelfGenerator?.HideBookAddPlacementGuide();
                    return;
                }

                if (Mouse.current.rightButton.wasPressedThisFrame)
                {
                    CancelBookAddPlacementSelection(true, true);
                    return;
                }

                UpdateBookAddPlacementGuide(pointer);
                if (!Mouse.current.leftButton.wasPressedThisFrame)
                {
                    return;
                }

                HandleBookAddPlacementSelection(pointer);
                return;
            }

            if (IsPointerOverUi())
            {
                return;
            }

            if (!Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (TryHitNotebook())
            {
                FocusNotebook();
                return;
            }

            if (TryHitGuestbookBoard())
            {
                FocusGuestbook();
                return;
            }

            if (CurrentFocusArea != ExperienceFocusArea.Bookshelf && TryHitBookshelf())
            {
                FocusBookshelf();
                return;
            }
        }

        // Resilience layer that re-applies the onboarding Step3 UI override
        // after Update has finished. Cost model (verified in code review):
        //   - flag off  → 1 bool read, exits immediately. This is the
        //     overwhelming common case once onboarding is past Step3.
        //   - flag on, all targets already hidden → 16 null/activeSelf reads.
        //     SetActive is NOT called — HideOnboardingChromeIfActive's
        //     activeSelf guard makes that path a noop. Net cost: 16 cheap
        //     property reads, no Unity messaging churn.
        //   - flag on, an external sync (RefreshFocusBackButton,
        //     SyncBookshelfBottomNav, BookshelfStatusPanel.SetActive(true),
        //     etc.) re-activated a target this frame → that single target
        //     gets exactly one SetActive(false) call, here, before paint.
        //
        // Heavier alternatives considered (not adopted in this MR):
        //   1. Add `if (isOnboardingStep3Active) return;` guards inside the
        //      ~5 per-frame sync methods that toggle these GameObjects.
        //      Touches more surface area in BookshelfExperienceManager and
        //      every new chrome sync added later has to remember the guard.
        //   2. Re-parent the 16 targets under an inactive container on
        //      Step3 entry. Cleaner runtime cost (zero LateUpdate work) but
        //      breaks layout/sibling order assumptions in the existing
        //      build code, and onboarding is the only flow that needs it.
        //   3. Replace SetActive with CanvasGroup.alpha=0 + interactable=
        //      false on each target. Same blast radius as (2) and adds 16
        //      CanvasGroup components to GameObjects that otherwise don't
        //      need them.
        // Resilience layer wins on contained blast radius — the entire
        // override lives in this method + EnforceOnboardingStep3UiHidden.
        // Migrate to (1) or (2) only if profiling actually attributes
        // measurable cost here.
        private void LateUpdate()
        {
            SyncExternalDashboardPanelPresentation();

            if (isOnboardingStep3Active)
            {
                EnforceOnboardingStep3UiHidden();
            }
        }

        private void OnOnboardingStep3StateChanged(bool active)
        {
            if (active)
            {
                EnforceOnboardingStep3UiHidden();

                // Auto-open the book-add panel once the focus state allows it.
                // CanOpenBookAddPanel may be false at this exact frame (Step3
                // can be flipped before FocusBookshelf has finished), so retry
                // with a short timeout instead of giving up immediately.
                if (onboardingAutoOpenBookAddRoutine != null)
                {
                    StopCoroutine(onboardingAutoOpenBookAddRoutine);
                }
                onboardingAutoOpenBookAddRoutine = StartCoroutine(AutoOpenBookAddPanelWhenReadyAsync());
            }
            else
            {
                if (onboardingAutoOpenBookAddRoutine != null)
                {
                    StopCoroutine(onboardingAutoOpenBookAddRoutine);
                    onboardingAutoOpenBookAddRoutine = null;
                }
                // No explicit re-show: chrome is restored by the existing
                // SyncBookshelfBottomNav / focus-mode visibility code paths
                // that run when onboarding ends and FocusMainView() is called.
            }
        }

        private IEnumerator AutoOpenBookAddPanelWhenReadyAsync()
        {
            const float TimeoutSeconds = 6f;
            var deadline = Time.unscaledTime + TimeoutSeconds;
            while (Time.unscaledTime < deadline)
            {
                if (!isOnboardingStep3Active)
                {
                    yield break;
                }
                if (CanOpenBookAddPanel())
                {
                    OpenBookAddPanel();
                    onboardingAutoOpenBookAddRoutine = null;
                    yield break;
                }
                yield return null;
            }
            // Timed out: the user can still finish the onboarding via the
            // step3 panel "완료" button (it lives on the OnboardingScreenController
            // canvas, not on the chrome we just hid). Leaving the routine null
            // signals "not retrying" — re-flipping the flag will create a new
            // routine.
            onboardingAutoOpenBookAddRoutine = null;
        }

        // Hides the focus-mode chrome that the onboarding "first book"
        // experience must not expose. UI elements may not be built yet (Awake
        // / Start order) so each branch null-checks; activeSelf check inside
        // HideOnboardingChromeIfActive skips SetActive churn when the
        // GameObject is already hidden — this is what keeps the LateUpdate
        // resilience layer cheap (see LateUpdate's cost model comment).
        //
        // Caller policy: invoked from (a) LateUpdate while step3 is active,
        // and (b) OnOnboardingStep3StateChanged once on the entry edge so
        // the user does not see chrome flicker for one frame between the
        // flag flipping and the next LateUpdate firing.
        private void EnforceOnboardingStep3UiHidden()
        {
            HideOnboardingChromeIfActive(focusBackButton != null ? focusBackButton.gameObject : null);
            HideOnboardingChromeIfActive(bookshelfStatusPanel);
            HideOnboardingChromeIfActive(bookshelfBottomNavPanel);
            HideOnboardingChromeIfActive(bookshelfIndexPanel);
            HideOnboardingChromeIfActive(hamburgerButton != null ? hamburgerButton.gameObject : null);
            HideOnboardingChromeIfActive(hamburgerMenuPanel);
            HideOnboardingChromeIfActive(bookshelfEditModeButton != null ? bookshelfEditModeButton.gameObject : null);
            HideOnboardingChromeIfActive(openBookAddButton != null ? openBookAddButton.gameObject : null);
            HideOnboardingChromeIfActive(openBookSearchButton != null ? openBookSearchButton.gameObject : null);
            HideOnboardingChromeIfActive(openBookshelfManageButton != null ? openBookshelfManageButton.gameObject : null);
            HideOnboardingChromeIfActive(openBookshelfDesignButton != null ? openBookshelfDesignButton.gameObject : null);
            HideOnboardingChromeIfActive(addBookshelfButton != null ? addBookshelfButton.gameObject : null);
            HideOnboardingChromeIfActive(openBookshelfNameEditButton != null ? openBookshelfNameEditButton.gameObject : null);
            HideOnboardingChromeIfActive(deleteBookshelfButton != null ? deleteBookshelfButton.gameObject : null);
            HideOnboardingChromeIfActive(overlayPreviousFocusButton != null ? overlayPreviousFocusButton.gameObject : null);
            HideOnboardingChromeIfActive(overlayNextFocusButton != null ? overlayNextFocusButton.gameObject : null);
        }

        private static void HideOnboardingChromeIfActive(GameObject go)
        {
            if (go != null && go.activeSelf)
            {
                go.SetActive(false);
            }
        }

        private void OnEnable()
        {
            // 신규 가입 path 에서 BookshelfExperienceManager 의 prefab override 가 안 적용되어
            // externalDashboardPanel/useExternalDashboardPanel 이 default(null/false) 인 회귀 방어.
            // 재로그인 path 와 비교 진단 (DiagDashboardRace) 로 case 별 SerializeField 값이 다른 게
            // 확인됨 — root cause (왜 prefab override 가 case 별 다르게 적용되나) 는 별도 추적, 본 fix 는
            // fast-path fallback 으로 즉시 회복.
            EnsureExternalDashboardPanelWired();

            if (cameraController != null)
            {
                cameraController.ModeChanged += HandleCameraModeChanged;
                cameraController.TransitionCompleted += HandleCameraTransitionCompleted;
                SyncFromCameraMode(cameraController.CurrentMode);
            }

            if (bookshelfGenerator != null)
            {
                // -= 먼저 해 duplicate subscribe 방지. Configure / Start 가 먼저 wire 한 같은 generator 가
                // 그대로 들어와도 핸들러가 두 번 발사되지 않게 idempotent 보장. MR !159 review #2 응답.
                bookshelfGenerator.BookshelfSwitchStateChanged -= HandleBookshelfSwitchStateChanged;
                bookshelfGenerator.BookshelfSwitchStateChanged += HandleBookshelfSwitchStateChanged;
                bookshelfGenerator.Generated -= HandleBookshelfGenerated;
                bookshelfGenerator.Generated += HandleBookshelfGenerated;
            }
        }

        private void EnsureExternalDashboardPanelWired()
        {
            if (externalDashboardPanel != null && ShouldUseExternalDashboardPanel)
            {
                return;
            }

            if (externalDashboardPanel == null)
            {
                // DashboardSceneInstaller 는 DashboardScreenPanel prefab 의 root 에 attach 되어 있어
                // installer 의 GameObject = panel root 와 동일. inactive 포함해 scene 검색.
                // prefab asset 은 FindObjectsByType 결과에 안 들어오므로 안전.
                var installers = FindObjectsByType<DashboardSceneInstaller>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (installers.Length > 1)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[BookshelfExperienceManager] DashboardSceneInstaller instance count={installers.Length} (>1). " +
                        $"Wiring first (name='{installers[0]?.gameObject.name}'). Scene 중복 점검 권고.");
                }
                if (installers.Length > 0 && installers[0] != null)
                {
                    externalDashboardPanel = installers[0].gameObject;
                    UnityEngine.Debug.LogWarning(
                        $"[BookshelfExperienceManager] externalDashboardPanel auto-wired to '{externalDashboardPanel.name}'. " +
                        "Scene prefab override missing (regression on new-signup path); fast-path fallback engaged.");
                }
            }

            // SerializeField (useExternalDashboardPanel) 는 Inspector 의도 그대로 보존하고
            // runtime override flag 만 켠다. 두 값의 or = ShouldUseExternalDashboardPanel.
            if (externalDashboardPanel != null && !ShouldUseExternalDashboardPanel)
            {
                runtimeUseExternalDashboardPanel = true;
            }
        }

        private void OnDisable()
        {
            if (cameraController != null)
            {
                cameraController.ModeChanged -= HandleCameraModeChanged;
                cameraController.TransitionCompleted -= HandleCameraTransitionCompleted;
            }

            if (bookshelfGenerator != null)
            {
                bookshelfGenerator.BookshelfSwitchStateChanged -= HandleBookshelfSwitchStateChanged;
                bookshelfGenerator.Generated -= HandleBookshelfGenerated;
            }

            DetachNotebookApiCallbacks();
            CancelBookAddCoverLoads();
            ClearBookAddCoverSpriteCache();
        }

        public void FocusQuarterView()
        {
            if (TryPromptUnsavedBookshelfChanges(FocusQuarterViewImmediate))
            {
                return;
            }

            FocusQuarterViewImmediate();
        }

        private void FocusQuarterViewImmediate()
        {
            if (!CanStartCameraFocus())
            {
                return;
            }

            CancelPendingNotebookFocusRequest();
            PrepareQuarterViewReturn();
            currentMainViewMode = MainCameraViewMode.QuarterView;
            cameraController.MoveToMode(CameraMode.Overview);
        }

        public void FocusMainView()
        {
            FocusMainViewImmediate();
        }

        private void FocusMainViewImmediate()
        {
            if (!CanStartCameraFocus())
            {
                return;
            }

            CancelPendingNotebookFocusRequest();
            PrepareQuarterViewReturn();
            currentMainViewMode = MainCameraViewMode.MainView;
            cameraController.MoveToMainView();

            // MoveToMainView intentionally suppresses CameraController.ModeChanged so
            // the main-view-mode toggle state survives across re-entry. The downside is
            // that HandleCameraModeChanged → SyncFromCameraMode never runs, which leaves
            // CurrentFocusArea stuck at whatever it was before (e.g. Bookshelf when this
            // is called from the onboarding-completion path). Sync explicitly so the
            // bookshelf HUD/zoom/back-button state clears as the camera leaves focus.
            SyncFromCameraMode(CameraMode.Overview);
        }

        // Instant variant of FocusMainView for callers that need the camera to be at
        // the MainView pose RIGHT NOW (no animated transition) — used by edit-mode
        // entry so the editor never inherits a QuarterView / Frontal pose.
        public void SnapToMainView()
        {
            if (cameraController == null)
            {
                return;
            }

            PrepareQuarterViewReturn();
            CancelPendingNotebookFocusRequest();
            currentMainViewMode = MainCameraViewMode.MainView;
            cameraController.ApplyMainView();
            SyncFromCameraMode(CameraMode.Overview);
        }

        // Toggles only the main-scene camera pose (MainView ↔ QuarterView). When the
        // camera is in a focus mode (Frontal/Whiteboard/Notebook) this is a no-op, so
        // pressing the button mid-focus never disrupts those flows.
        public void ToggleMainViewMode()
        {
            if (cameraController == null || cameraController.IsTransitioning)
            {
                return;
            }

            if (cameraController.CurrentMode != CameraMode.Overview)
            {
                return;
            }

            if (currentMainViewMode == MainCameraViewMode.MainView)
            {
                currentMainViewMode = MainCameraViewMode.QuarterView;
                cameraController.MoveToQuarterView();
            }
            else
            {
                currentMainViewMode = MainCameraViewMode.MainView;
                cameraController.MoveToMainView();
            }
        }

        public void FocusBookshelf()
        {
            if (!CanStartCameraFocus())
            {
                return;
            }

            RememberBookshelfReturnMainViewMode();
            CancelPendingNotebookFocusRequest();
            cameraController.MoveToMode(CameraMode.Frontal);
        }

        public void FocusGuestbook()
        {
            if (TryPromptUnsavedBookshelfChanges(FocusGuestbookImmediate))
            {
                return;
            }

            FocusGuestbookImmediate();
        }

        private void FocusGuestbookImmediate()
        {
            if (!CanStartCameraFocus())
            {
                return;
            }

            CancelPendingNotebookFocusRequest();
            CancelBookPlacementEdit(false);
            SetBookshelfEditMode(false);
            var guestbookHitArea = FindFirstObjectByType<GuestbookBoardHitArea>(FindObjectsInactive.Include);
            cameraController.MoveToWhiteboardSurface(guestbookHitArea != null ? guestbookHitArea.transform : null);
        }

        public void FocusNotebook()
        {
            if (TryPromptUnsavedBookshelfChanges(FocusNotebookImmediate))
            {
                return;
            }

            FocusNotebookImmediate();
        }

        private void FocusNotebookImmediate()
        {
            if (!CanStartCameraFocus())
            {
                return;
            }

            CancelBookPlacementEdit(false);
            SetBookshelfEditMode(false);
            var focusRequestVersion = BeginNotebookFocusRequest();
            if (!EnsureNotebookUiReadyForFocus(focusRequestVersion))
            {
                return;
            }

            FocusNotebookImmediateWithLoadedUi();
        }

        private void FocusNotebookImmediateWithLoadedUi()
        {
            if (CurrentFocusArea == ExperienceFocusArea.Notebook && notebookScreenController != null)
            {
                notebookScreenController.SetImmersiveMode(true);
                notebookScreenController.ActivateDashboard();
                return;
            }

            if (notebookScreenController != null)
            {
                notebookScreenController.SetImmersiveMode(false);
                notebookScreenController.Show();
            }

            if (notebookScreenController != null)
            {
                notebookScreenController.QueueDashboardActivation();
            }

            if (notebookScreenController != null && notebookScreenController.ScreenTransform != null)
            {
                cameraController.MoveToNotebookScreen(notebookScreenController.ScreenTransform);
                return;
            }

            cameraController.MoveToMode(CameraMode.Notebook);
        }

        public void CloseNotebookFocus()
        {
            // (MR #157 review skh1205 #1 / simonjiho #2): CanStartCameraFocus 가드를 외부에서
            // 먼저 확인 — FocusMainViewImmediate 내부 가드 fail 시 카메라 이동은 skip 되는데
            // NotifyFocusExited 만 호출되어 "카메라는 노트북에 남아있는데 UI 는 focus 종료" race
            // (재진입 / immersive state 불일치) 가 열리던 회귀. 이전 동작 (가드 fail 시 둘 다 skip) 복원.
            if (!CanStartCameraFocus())
            {
                return;
            }

            // 책장/노트북/대시보드 포커스 복귀 시점 통일 — 항상 MainView (책장을 바라보는 시점) 로
            // 복귀. FocusMainViewImmediate 가 CancelPendingNotebookFocusRequest + MoveToMainView +
            // SyncFromCameraMode 내장 (내부 CanStartCameraFocus 가드는 redundant 지만 무해).
            FocusMainViewImmediate();
            notebookUiSceneLoader?.NotifyFocusExited();
        }

        private bool HandleFocusShortcuts()
        {
            if (cameraController != null && cameraController.IsTransitioning)
            {
                return true;
            }

            if (Keyboard.current == null)
            {
                return false;
            }

            if (Keyboard.current.escapeKey.wasPressedThisFrame && isBookAddPlacementSelectionActive)
            {
                CancelBookAddPlacementSelection(true, true);
                return true;
            }

            if (Keyboard.current.f1Key.wasPressedThisFrame)
            {
                FocusQuarterView();
                return true;
            }

            if (Keyboard.current.f2Key.wasPressedThisFrame)
            {
                FocusBookshelf();
                return true;
            }

            if (Keyboard.current.f3Key.wasPressedThisFrame)
            {
                FocusGuestbook();
                return true;
            }

            if (Keyboard.current.f4Key.wasPressedThisFrame)
            {
                FocusNotebook();
                return true;
            }

            return false;
        }

        private bool CanStartCameraFocus()
        {
            return cameraController != null &&
                   !cameraController.IsTransitioning &&
                   (bookshelfGenerator == null || !bookshelfGenerator.IsBookshelfSwitching);
        }

        public bool CanHandleFrontalZoomInput()
        {
            return overlayStateController.CanHandleFrontalZoomInput(
                CurrentFocusArea,
                state != null && state.IsViewingBook,
                state != null && state.ActiveDraggedBook != null,
                IsPointerOverUi(),
                CaptureBookshelfOverlayState());
        }

        private bool TryPromptUnsavedBookshelfChanges(System.Action afterSaveAction)
        {
            if (!IsBookshelfEditMode || !hasUnsavedBookshelfEditChanges || isBookshelfUnsavedConfirmPanelOpen)
            {
                return false;
            }

            pendingBookshelfNavigationAfterSave = afterSaveAction;
            SetBookshelfOverlayMode(BookshelfOverlayMode.UnsavedConfirm);
            RefreshHud();
            return true;
        }

        private void MarkBookshelfEditDirty()
        {
            if (IsBookshelfEditMode)
            {
                hasUnsavedBookshelfEditChanges = true;
            }
        }

        private void MarkBookshelfEditSaved()
        {
            hasUnsavedBookshelfEditChanges = false;
        }

        public void HandleBookViewed(BookEntity book)
        {
            CloseBookPlacementEditor(false);
            ActiveDetailBook = book;
            isBookEditPanelOpen = false;
            pendingDeleteBook = null;
            activeEditSection = null;
            if (bookDetailPanel != null)
            {
                bookDetailPanel.SetActive(true);
            }

            if (bookEditPanel != null)
            {
                bookEditPanel.SetActive(false);
            }

            if (sectionEditPanel != null)
            {
                sectionEditPanel.SetActive(false);
            }

            CloseBookReportPanelsSilently();
            RequestServerBookSnapshot(book);
            RefreshBookDetailPanel();
            RefreshHud();
        }

        public void ToggleFocusedBookDetail(BookEntity book)
        {
            if (bookDetailPanel != null && bookDetailPanel.activeSelf && ActiveDetailBook == book)
            {
                RefreshBookDetailPanel();
                RefreshHud();
                return;
            }

            OpenBookDetailPanel(book);
            RefreshHud();
        }

        public void OpenBookEditPanel(BookEntity book)
        {
            if (book == null ||
                dataStore == null ||
                bookEditPanel == null ||
                !IsBookshelfEditMode ||
                (bookshelfGenerator != null && bookshelfGenerator.IsBookshelfSwitching) ||
                (state != null && state.ActiveDraggedBook != null))
            {
                return;
            }

            CloseOverlayPanels(bookEditPanel);
            CloseBookPlacementEditor(false);
            ActiveDetailBook = book;
            pendingDeleteBook = null;
            isBookEditPanelOpen = true;
            bookEditPanel.SetActive(true);
            RefreshBookEditPanel();
            RefreshHud();
        }

        public void OpenSectionAlignmentPanel(ShelfSection section)
        {
            if (section == null ||
                section.isAdvertisementSection ||
                sectionEditPanel == null ||
                !IsBookshelfEditMode ||
                (bookshelfGenerator != null && bookshelfGenerator.IsBookshelfSwitching) ||
                (state != null && state.ActiveDraggedBook != null))
            {
                return;
            }

            CloseOverlayPanels(sectionEditPanel);
            CloseBookPlacementEditor(false);
            ActiveDetailBook = null;
            pendingDeleteBook = null;
            isBookEditPanelOpen = false;
            activeEditSection = section;
            sectionEditPanel.SetActive(true);
            RefreshSectionEditPanel();
            RefreshHud();
        }

        public void HandleBookClosed()
        {
            ActiveDetailBook = null;
            isBookEditPanelOpen = false;
            pendingDeleteBook = null;
            activeEditSection = null;

            if (bookDetailPanel != null)
            {
                bookDetailPanel.SetActive(false);
            }

            if (bookEditPanel != null)
            {
                bookEditPanel.SetActive(false);
            }

            if (sectionEditPanel != null)
            {
                sectionEditPanel.SetActive(false);
            }

            if (bookReviewWritePanel != null)
            {
                bookReviewWritePanel.SetActive(false);
            }

            isBookReviewWritePanelOpen = false;
            isBookReviewSaving = false;
            activeReviewBook = null;
            SetBookReviewStatus(string.Empty);
            CloseBookReportPanelsSilently();
            RefreshHud();
        }

        public BookshelfRuntimeSnapshotData CreateRuntimeSnapshot()
        {
            return dataStore != null
                ? dataStore.CreateSnapshot(CurrentFocusArea, ActiveDetailBook)
                : new BookshelfRuntimeSnapshotData();
        }

        private void OpenBookDetailPanel(BookEntity book)
        {
            if (book == null || dataStore == null || bookDetailPanel == null)
            {
                return;
            }

            CloseOverlayPanels(bookDetailPanel);
            ActiveDetailBook = book;
            pendingDeleteBook = null;
            isBookEditPanelOpen = false;
            bookDetailPanel.SetActive(true);
            RequestServerBookSnapshot(book);
            RefreshBookDetailPanel();
            RefreshHud();
        }

        private void RequestServerBookSnapshot(BookEntity book)
        {
            if (book == null ||
                book.isDecoration ||
                bookshelfApiSyncController == null ||
                !bookshelfApiSyncController.IsServerSyncEnabled)
            {
                return;
            }

            bookshelfApiSyncController.FetchBookDetails(book, updatedBook =>
            {
                ApplyBookEntityToMeta(updatedBook);
                if (ActiveDetailBook == updatedBook)
                {
                    RefreshBookDetailPanel();
                    RefreshHud();
                }
            });

            bookshelfApiSyncController.FetchReadingRecord(book, (updatedBook, success) =>
            {
                if (!success)
                {
                    return;
                }

                ApplyBookEntityToMeta(updatedBook);
                if (ActiveDetailBook == updatedBook || activeReportBook == updatedBook)
                {
                    RefreshHud();
                }
            });
        }

        private void ApplyBookEntityToMeta(BookEntity book)
        {
            if (book == null || dataStore == null)
            {
                return;
            }

            var meta = dataStore.GetOrCreateBookMeta(book);
            if (meta == null)
            {
                return;
            }

            meta.title = string.IsNullOrWhiteSpace(book.title) ? meta.title : book.title;
            meta.author = string.IsNullOrWhiteSpace(book.author) ? meta.author : book.author;
            meta.publisher = string.IsNullOrWhiteSpace(book.publisher) ? meta.publisher : book.publisher;
            meta.description = string.IsNullOrWhiteSpace(book.description) ? meta.description : book.description;
            meta.memoPreview = string.IsNullOrWhiteSpace(book.memoPreview) ? meta.memoPreview : book.memoPreview;
            MergeOwnerRatingIntoMeta(book, meta);
            if (book.hasAggregateBookStats)
            {
                meta.averageRating = Mathf.Clamp(book.averageRating, 0f, 5f);
                meta.totalLikeCount = Mathf.Max(0, book.totalLikeCount);
                meta.reviewCount = Mathf.Max(0, book.reviewCount);
                meta.hasAggregateBookStats = true;
            }
            meta.isLiked = book.isLiked;
            meta.isPaidCoverOwned = book.isPaidCoverOwned;
            meta.readingStatus = book.readingStatus;
            meta.isRead = book.readingStatus == App.Domain.ReadingStatus.Read;
            meta.reportVisibility = book.reportVisibility;
            meta.isPublic = book.reportVisibility == App.Domain.VisibilityScope.Public;
            meta.reportTitle = book.reportTitle;
            meta.reportBody = book.reportBody;
            meta.reportUpdatedLabel = book.reportUpdatedLabel;
            meta.reportLiked = book.reportLiked;
            meta.reportLikeCount = book.reportLikeCount;
            meta.reportCommentCount = book.reportCommentCount;
            dataStore.ApplyBookMeta(book, meta);
        }

        private static void MergeOwnerRatingIntoMeta(BookEntity book, BookMetaData meta)
        {
            if (book == null || meta == null)
            {
                return;
            }

            var incomingOwnerRating = SnapRatingToStep(Mathf.Clamp(book.ownerRating, 0f, 5f));
            var cachedOwnerRating = SnapRatingToStep(Mathf.Clamp(meta.ownerRating, 0f, 5f));
            if (incomingOwnerRating > 0f || cachedOwnerRating <= 0f)
            {
                meta.ownerRating = incomingOwnerRating;
                book.ownerRating = incomingOwnerRating;
                return;
            }

            meta.ownerRating = cachedOwnerRating;
            book.ownerRating = cachedOwnerRating;
        }

        private void CloseBookDetailPanel()
        {
            if (bookDetailPanel != null)
            {
                bookDetailPanel.SetActive(false);
            }

            if (bookEditPanel != null)
            {
                bookEditPanel.SetActive(false);
            }

            if (sectionEditPanel != null)
            {
                sectionEditPanel.SetActive(false);
            }

            if (isBookEditPanelOpen)
            {
                ActiveDetailBook = null;
            }

            activeEditSection = null;
            isBookEditPanelOpen = false;
            pendingDeleteBook = null;
            RefreshHud();
        }

        private void OpenBookPlacementEditor()
        {
            if (state == null ||
                !state.IsViewingBook ||
                state.ActiveDraggedBook != null ||
                CurrentFocusArea != ExperienceFocusArea.Bookshelf ||
                IsBookshelfEditMode ||
                (bookshelfGenerator != null && bookshelfGenerator.IsBookshelfSwitching))
            {
                return;
            }

            var book = ActiveDetailBook != null ? ActiveDetailBook : state.ActiveViewedBook;
            if (book == null)
            {
                return;
            }

            if (bookReportWritePanel != null)
            {
                bookReportWritePanel.SetActive(false);
                isBookReportWritePanelOpen = false;
            }

            if (bookReportReadPanel != null)
            {
                bookReportReadPanel.SetActive(false);
                isBookReportReadPanelOpen = false;
            }

            if (bookReviewWritePanel != null)
            {
                bookReviewWritePanel.SetActive(false);
                isBookReviewWritePanelOpen = false;
                isBookReviewSaving = false;
                activeReviewBook = null;
                SetBookReviewStatus(string.Empty);
            }

            placementEditBook = book;
            placementOriginalOrientation = book.orientation;
            placementStagedOrientation = book.orientation;
            isBookPlacementEditorOpen = true;
            ActiveDetailBook = null;
            activeEditSection = null;
            pendingDeleteBook = null;
            isBookEditPanelOpen = false;
            if (bookDetailPanel != null)
            {
                bookDetailPanel.SetActive(false);
            }

            if (interactionController != null && state.IsViewingBook)
            {
                interactionController.CloseViewedBook(true);
            }

            RefreshHud();
        }

        private void PreviewBookPlacement(BookOrientation orientation)
        {
            if (!isBookPlacementEditorOpen ||
                placementEditBook == null ||
                state == null)
            {
                return;
            }

            state.ChangeBookOrientation(placementEditBook, orientation, this);
            placementStagedOrientation = placementEditBook.orientation;
            RefreshBookPlacementEditor();
        }

        private void ConfirmBookPlacementEdit()
        {
            if (placementEditBook != null)
            {
                SyncBookPlacementOrRefresh(placementEditBook, "책 배치 저장");
            }

            CloseBookPlacementEditor(false);
            RefreshHud();
        }

        private void CancelBookPlacementEdit()
        {
            CancelBookPlacementEdit(true);
        }

        private void CancelBookPlacementEdit(bool refresh)
        {
            if (isBookPlacementEditorOpen &&
                placementEditBook != null &&
                state != null &&
                placementEditBook.orientation != placementOriginalOrientation)
            {
                state.ChangeBookOrientation(placementEditBook, placementOriginalOrientation, this);
            }

            CloseBookPlacementEditor(false);
            if (refresh)
            {
                RefreshHud();
            }
        }

        private void CloseBookPlacementEditor(bool refresh)
        {
            isBookPlacementEditorOpen = false;
            placementEditBook = null;
            placementOriginalOrientation = BookOrientation.Spine;
            placementStagedOrientation = BookOrientation.Spine;

            if (bookPlacementEditorPanel != null)
            {
                bookPlacementEditorPanel.SetActive(false);
            }

            if (openBookPlacementEditorButton != null)
            {
                openBookPlacementEditorButton.gameObject.SetActive(false);
            }

            if (refresh)
            {
                RefreshHud();
            }
        }

        private void DeleteActiveBook()
        {
            if (!isBookEditPanelOpen ||
                !IsBookshelfEditMode ||
                ActiveDetailBook == null ||
                state == null)
            {
                return;
            }

            if (pendingDeleteBook != ActiveDetailBook)
            {
                pendingDeleteBook = ActiveDetailBook;
                RefreshBookEditPanel();
                return;
            }

            var book = ActiveDetailBook;
            var sourceSection = book.CurrentSection;
            var retryKey = BuildMutationRetryKey("책 삭제", book);
            var retryAction = CreateBookRetryAction(
                book,
                "책 삭제",
                () => RetryRemoveBook(book, sourceSection, false));

            pendingDeleteBook = null;
            var didStartDeleteSync = CanStartBookshelfApiMutation() &&
                                     bookshelfApiSyncController.RemoveBook(
                                          book,
                                          () =>
                                          {
                                              ClearPendingMutationRetry(retryKey);
                                              RemoveBookLocally(book, sourceSection, false);
                                              ShowDeleteResultDialog("책장에서 제거 완료", "책장에서 책을 제거했습니다.", true);
                                          },
                                          error => RefreshServerStateAfterMutationFailure(
                                              "책 삭제",
                                              error,
                                              retryAction,
                                              retryKey));
            if (didStartDeleteSync)
            {
                ActiveDetailBook = null;
                isBookEditPanelOpen = false;
                if (bookDetailPanel != null)
                {
                    bookDetailPanel.SetActive(false);
                }

                if (bookEditPanel != null)
                {
                    bookEditPanel.SetActive(false);
                }

                RefreshHud();
                return;
            }

            if (usesHttpApiMode && !CanStartBookshelfApiMutation())
            {
                RefreshServerStateAfterMutationFailure(
                    "책 삭제",
                    "책장 API 동기화가 준비되지 않아 책 삭제를 서버에 요청할 수 없습니다.",
                    null,
                    retryKey);
                return;
            }

            if (bookshelfApiSyncController != null && bookshelfApiSyncController.IsServerSyncEnabled)
            {
                RefreshServerStateAfterMutationFailure(
                    "책 삭제",
                    "서버 삭제 요청에 필요한 서버 식별자를 찾을 수 없습니다.",
                    null,
                    retryKey);
                return;
            }

            RemoveBookLocally(book, sourceSection, false);
            ShowDeleteResultDialog("책장에서 제거 완료", "책장에서 책을 제거했습니다.", true);
        }

        private void DeleteActiveBookFromDetail()
        {
            if (ActiveDetailBook == null ||
                state == null ||
                !CanUseOwnerBookActions())
            {
                return;
            }

            if (pendingDeleteBook != ActiveDetailBook)
            {
                pendingDeleteBook = ActiveDetailBook;
                RefreshBookDetailPanel();
                return;
            }

            var book = ActiveDetailBook;
            var sourceSection = book.CurrentSection;
            var retryKey = BuildMutationRetryKey("책 삭제", book);
            var retryAction = CreateBookRetryAction(
                book,
                "책 삭제",
                () => RetryRemoveBook(book, sourceSection, true));

            pendingDeleteBook = null;
            pendingDeleteReportBook = null;
            var didStartDeleteSync = CanStartBookshelfApiMutation() &&
                                     bookshelfApiSyncController.RemoveBook(
                                          book,
                                          () =>
                                          {
                                              ClearPendingMutationRetry(retryKey);
                                              RemoveBookLocally(book, sourceSection, true);
                                              ShowDeleteResultDialog("책장에서 제거 완료", "책장에서 책을 제거했습니다.", true);
                                          },
                                          error => RefreshServerStateAfterMutationFailure(
                                              "책 삭제",
                                              error,
                                              retryAction,
                                              retryKey));
            if (didStartDeleteSync)
            {
                ActiveDetailBook = null;
                activeReportBook = null;
                activeReviewBook = null;
                isBookReportReadPanelOpen = false;
                isBookReportWritePanelOpen = false;
                isBookReviewWritePanelOpen = false;
                isBookReviewSaving = false;
                if (bookDetailPanel != null)
                {
                    bookDetailPanel.SetActive(false);
                }

                if (bookReportReadPanel != null)
                {
                    bookReportReadPanel.SetActive(false);
                }

                if (bookReportWritePanel != null)
                {
                    bookReportWritePanel.SetActive(false);
                }

                if (bookReviewWritePanel != null)
                {
                    bookReviewWritePanel.SetActive(false);
                }

                RefreshHud();
                return;
            }

            if (usesHttpApiMode && !CanStartBookshelfApiMutation())
            {
                RefreshServerStateAfterMutationFailure(
                    "책 삭제",
                    "책장 API 동기화가 준비되지 않아 책 삭제를 서버에 요청할 수 없습니다.",
                    null,
                    retryKey);
                return;
            }

            if (bookshelfApiSyncController != null && bookshelfApiSyncController.IsServerSyncEnabled)
            {
                RefreshServerStateAfterMutationFailure(
                    "책 삭제",
                    "서버 삭제 요청에 필요한 서버 식별자를 찾을 수 없습니다.",
                    null,
                    retryKey);
                return;
            }

            RemoveBookLocally(book, sourceSection, true);
            ShowDeleteResultDialog("책장에서 제거 완료", "책장에서 책을 제거했습니다.", true);
        }

        private void RetryRemoveBook(BookEntity book, ShelfSection sourceSection, bool closeReportPanels)
        {
            if (book == null)
            {
                RefreshServerStateAfterMutationFailure("책 삭제", "삭제할 책을 찾을 수 없습니다.");
                return;
            }

            var retryKey = BuildMutationRetryKey("책 삭제", book);
            if (!CanStartBookshelfApiMutation())
            {
                isFirstBookshelfCreateInProgress = false;
                if (usesHttpApiMode)
                {
                    RefreshServerStateAfterMutationFailure(
                        "책 삭제",
                        "책장 API 동기화가 준비되지 않아 책 삭제를 서버에 요청할 수 없습니다.",
                        null,
                        retryKey);
                    return;
                }

                ClearPendingMutationRetry(retryKey);
                RemoveBookLocally(book, sourceSection, closeReportPanels);
                ShowDeleteResultDialog("책장에서 제거 완료", "책장에서 책을 제거했습니다.", true);
                return;
            }

            var retryAction = CreateBookRetryAction(
                book,
                "책 삭제",
                () => RetryRemoveBook(book, sourceSection, closeReportPanels));
            var didStartDeleteSync = bookshelfApiSyncController.RemoveBook(
                book,
                () =>
                {
                    ClearPendingMutationRetry(retryKey);
                    RemoveBookLocally(book, sourceSection, closeReportPanels);
                    ShowDeleteResultDialog("책장에서 제거 완료", "책장에서 책을 제거했습니다.", true);
                },
                error => RefreshServerStateAfterMutationFailure(
                    "책 삭제",
                    error,
                    retryAction,
                    retryKey));

            if (!didStartDeleteSync)
            {
                RefreshServerStateAfterMutationFailure(
                    "책 삭제",
                    "서버 삭제 요청에 필요한 서버 식별자를 찾을 수 없습니다.",
                    null,
                    retryKey);
            }
        }

        private void RemoveBookLocally(BookEntity book, ShelfSection sourceSection, bool closeReportPanels)
        {
            if (book == null || state == null)
            {
                return;
            }

            if (state.ActiveViewedBook == book && interactionController != null)
            {
                interactionController.CloseViewedBook(true);
            }

            state.RemoveBook(book);
            if (sourceSection != null)
            {
                state.LayoutSection(sourceSection, this, true);
            }

            if (ActiveDetailBook == book)
            {
                ActiveDetailBook = null;
            }

            if (activeReportBook == book)
            {
                activeReportBook = null;
            }

            if (activeReviewBook == book)
            {
                activeReviewBook = null;
                isBookReviewWritePanelOpen = false;
                isBookReviewSaving = false;
                SetBookReviewStatus(string.Empty);
            }

            isBookEditPanelOpen = false;
            if (closeReportPanels)
            {
                isBookReportReadPanelOpen = false;
                isBookReportWritePanelOpen = false;
            }

            if (bookDetailPanel != null)
            {
                bookDetailPanel.SetActive(false);
            }

            if (bookEditPanel != null)
            {
                bookEditPanel.SetActive(false);
            }

            if (closeReportPanels)
            {
                if (bookReportReadPanel != null)
                {
                    bookReportReadPanel.SetActive(false);
                }

                if (bookReportWritePanel != null)
                {
                    bookReportWritePanel.SetActive(false);
                }

                if (bookReviewWritePanel != null)
                {
                    bookReviewWritePanel.SetActive(false);
                }
            }

            Destroy(book.gameObject);
            MarkBookshelfEditDirty();
            RefreshHud();
        }

        private static string BuildMutationRetryKey(string operationName, object target)
        {
            var normalizedOperation = string.IsNullOrWhiteSpace(operationName) ? "mutation" : operationName.Trim();
            var targetKey = "global";
            if (target is UnityEngine.Object unityObject)
            {
                targetKey = unityObject != null ? unityObject.GetInstanceID().ToString() : "destroyed";
            }
            else if (target != null)
            {
                targetKey = target.ToString();
                if (string.IsNullOrWhiteSpace(targetKey))
                {
                    targetKey = target.GetHashCode().ToString();
                }
            }

            return $"{normalizedOperation}::{targetKey}";
        }

        private System.Action RegisterPendingMutationRetry(
            string retryKey,
            string operationName,
            string errorMessage,
            System.Action retryAction)
        {
            if (retryAction == null)
            {
                return null;
            }

            var key = string.IsNullOrWhiteSpace(retryKey)
                ? BuildMutationRetryKey(operationName, null)
                : retryKey.Trim();
            var message = NormalizeMutationErrorMessage(errorMessage);
            if (pendingMutationRetries.TryGetValue(key, out var entry))
            {
                entry.Update(operationName, message, retryAction);
                pendingMutationRetryOrder.Remove(key);
                pendingMutationRetryOrder.Add(key);
            }
            else
            {
                while (pendingMutationRetries.Count >= PendingMutationRetryLimit &&
                       pendingMutationRetryOrder.Count > 0)
                {
                    RemovePendingMutationRetry(pendingMutationRetryOrder[0]);
                }

                entry = new PendingMutationRetry(key, operationName, message, retryAction);
                pendingMutationRetries[key] = entry;
                pendingMutationRetryOrder.Add(key);
            }

            return () => RunPendingMutationRetry(key);
        }

        private void ClearPendingMutationRetry(string retryKey)
        {
            if (string.IsNullOrWhiteSpace(retryKey) || pendingMutationRetries.Count == 0)
            {
                return;
            }

            RemovePendingMutationRetry(retryKey.Trim());
        }

        private void RemovePendingMutationRetry(string retryKey)
        {
            if (string.IsNullOrWhiteSpace(retryKey))
            {
                return;
            }

            pendingMutationRetries.Remove(retryKey);
            pendingMutationRetryOrder.Remove(retryKey);
        }

        private void RunPendingMutationRetry(string retryKey)
        {
            if (string.IsNullOrWhiteSpace(retryKey))
            {
                return;
            }

            var key = retryKey.Trim();
            if (!pendingMutationRetries.TryGetValue(key, out var retry))
            {
                RefreshHud();
                return;
            }

            RemovePendingMutationRetry(key);
            ShowBookshelfTransientStatus("재시도 중", $"{retry.OperationName} 요청을 다시 보냅니다.", false);
            InvokePendingMutationRetry(retry);
            RefreshHud();
        }

        private void RunAllPendingMutationRetriesFromStatusPanel()
        {
            if (pendingMutationRetries.Count == 0)
            {
                RefreshHud();
                return;
            }

            var retries = new List<PendingMutationRetry>(pendingMutationRetries.Count);
            for (var i = 0; i < pendingMutationRetryOrder.Count; i++)
            {
                var key = pendingMutationRetryOrder[i];
                if (pendingMutationRetries.TryGetValue(key, out var retry))
                {
                    retries.Add(retry);
                }
            }

            pendingMutationRetries.Clear();
            pendingMutationRetryOrder.Clear();
            ShowBookshelfTransientStatus("재시도 중", $"{retries.Count}개 저장 요청을 다시 보냅니다.", false);
            for (var i = 0; i < retries.Count; i++)
            {
                InvokePendingMutationRetry(retries[i]);
            }

            RefreshHud();
        }

        private void InvokePendingMutationRetry(PendingMutationRetry retry)
        {
            if (retry == null || retry.RetryAction == null)
            {
                return;
            }

            try
            {
                retry.RetryAction.Invoke();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                RegisterPendingMutationRetry(
                    retry.Key,
                    retry.OperationName,
                    exception.Message,
                    retry.RetryAction);
                ShowBookshelfTransientStatus(
                    $"{retry.OperationName} 실패",
                    "재시도 중 오류가 발생했습니다. 다시 시도할 수 있습니다.",
                    true);
            }
        }

        private string GetPendingMutationRetryStatusMessage()
        {
            if (pendingMutationRetries.Count == 0)
            {
                return string.Empty;
            }

            PendingMutationRetry latest = null;
            for (var i = pendingMutationRetryOrder.Count - 1; i >= 0; i--)
            {
                if (pendingMutationRetries.TryGetValue(pendingMutationRetryOrder[i], out latest))
                {
                    break;
                }
            }

            if (latest == null)
            {
                return string.Empty;
            }

            return pendingMutationRetries.Count == 1
                ? $"{latest.OperationName} 요청이 아직 서버에 반영되지 않았습니다. {latest.Message}"
                : $"{latest.OperationName} 외 {pendingMutationRetries.Count - 1}개 요청이 아직 서버에 반영되지 않았습니다. 전체 재시도할 수 있습니다.";
        }

        private void RefreshServerStateAfterMutationFailure(
            string operationName,
            string errorMessage,
            System.Action retryAction = null,
            string retryKey = null)
        {
            Debug.LogWarning($"{operationName} failed on server. Refreshing bookshelf state. {errorMessage}");
            var pendingRetryAction = retryAction != null
                ? RegisterPendingMutationRetry(retryKey, operationName, errorMessage, retryAction)
                : null;
            if (retryAction == null)
            {
                ClearPendingMutationRetry(retryKey);
            }

            ShowBookshelfTransientStatus(
                $"{operationName} 실패",
                retryAction == null ? "최신 책장 상태를 다시 불러옵니다." : "재시도할 수 있습니다.",
                true);
            ShowDeleteResultDialog(
                $"{operationName} 실패",
                retryAction == null
                    ? $"서버 요청을 완료하지 못해 최신 상태로 다시 불러옵니다.\n{NormalizeMutationErrorMessage(errorMessage)}"
                    : $"서버 요청을 완료하지 못했습니다. 이 세션에서 다시 시도할 수 있습니다.\n{NormalizeMutationErrorMessage(errorMessage)}",
                false,
                pendingRetryAction,
                "재시도");
            if (retryAction == null)
            {
                bookshelfApiSyncController?.RefreshFromServer();
            }
        }

        private System.Action CreateBookRetryAction(BookEntity book, string operationName, System.Action syncAction)
        {
            var identity = new BookMutationIdentitySnapshot(book);
            return () =>
            {
                if (!CanRunBookRetry(identity, book, operationName))
                {
                    return;
                }

                syncAction?.Invoke();
            };
        }

        private System.Action CreateBookStateRetryAction(
            BookEntity book,
            BookStateMutationSnapshot expectedCurrent,
            BookStateMutationSnapshot desired,
            string operationName,
            System.Action syncAction)
        {
            var identity = new BookMutationIdentitySnapshot(book);
            return () =>
            {
                if (!CanRunBookRetry(identity, book, operationName))
                {
                    return;
                }

                var meta = book != null && dataStore != null ? dataStore.GetOrCreateBookMeta(book) : null;
                if (!expectedCurrent.Matches(book, meta))
                {
                    ShowBookshelfTransientStatus($"{operationName} 재시도 보류", "이후 다른 변경이 있어 최신 상태를 다시 불러옵니다.", true);
                    ShowDeleteResultDialog(
                        $"{operationName} 재시도 보류",
                        "이후 다른 변경이 있어 이전 저장 요청을 다시 보내지 않았습니다. 최신 책장 상태를 다시 불러옵니다.",
                        false);
                    bookshelfApiSyncController?.RefreshFromServer();
                    RefreshHud();
                    return;
                }

                RetryBookStateMutation(book, desired, syncAction);
            };
        }

        private bool CanRunBookRetry(BookMutationIdentitySnapshot identity, BookEntity book, string operationName)
        {
            if (book == null || identity == null)
            {
                ShowDeleteResultDialog(
                    $"{operationName} 재시도 보류",
                    "대상 책을 찾을 수 없어 최신 책장 상태를 다시 불러옵니다.",
                    false);
                bookshelfApiSyncController?.RefreshFromServer();
                RefreshHud();
                return false;
            }

            if (!identity.Matches(book))
            {
                ShowDeleteResultDialog(
                    $"{operationName} 재시도 보류",
                    "책의 서버 식별자가 변경되어 이전 저장 요청을 다시 보내지 않았습니다. 최신 책장 상태를 다시 불러옵니다.",
                    false);
                bookshelfApiSyncController?.RefreshFromServer();
                RefreshHud();
                return false;
            }

            return true;
        }

        private void SyncBookshelfCreateOrRefresh(string name, string operationName)
        {
            if (isBookshelfCreateRequestInFlight)
            {
                RefreshHud();
                return;
            }

            if (!CanStartBookshelfApiMutation())
            {
                if (usesHttpApiMode)
                {
                    RefreshServerStateAfterMutationFailure(
                        operationName,
                        "책장 API 동기화가 준비되지 않아 새 책장을 서버에 생성할 수 없습니다.");
                }

                return;
            }

            isBookshelfCreateRequestInFlight = usesHttpApiMode;
            RefreshHud();

            if (bookshelfGenerator != null &&
                bookshelfGenerator.BookshelfCount <= 0 &&
                pendingFirstBookshelfFeatureGuide)
            {
                isFirstBookshelfCreateInProgress = true;
            }

            var retryKey = BuildMutationRetryKey(operationName, name);
            if (!bookshelfApiSyncController.CreateBookshelf(
                    name,
                    () =>
                    {
                        isFirstBookshelfCreateInProgress = false;
                        isBookshelfCreateRequestInFlight = false;
                        ClearPendingMutationRetry(retryKey);
                        ShowBookshelfTransientStatus("책장 저장 완료", "새 책장이 서버에 저장되었습니다.", false);
                        RefreshHud();
                    },
                    error =>
                    {
                        isFirstBookshelfCreateInProgress = false;
                        isBookshelfCreateRequestInFlight = false;
                        RefreshServerStateAfterMutationFailure(
                            operationName,
                            error,
                            () => SyncBookshelfCreateOrRefresh(name, operationName),
                            retryKey);
                    }))
            {
                isFirstBookshelfCreateInProgress = false;
                isBookshelfCreateRequestInFlight = false;
                RefreshServerStateAfterMutationFailure(
                    operationName,
                    "책장 추가 요청을 시작할 수 없습니다.",
                    () => SyncBookshelfCreateOrRefresh(name, operationName),
                    retryKey);
            }
        }

        private bool EnsureBookshelfApiMutationReady(string operationName)
        {
            if (!usesHttpApiMode)
            {
                return true;
            }

            if (bookshelfApiSyncController != null &&
                bookshelfApiSyncController.IsServerSyncEnabled &&
                bookshelfApiSyncController.HasAccessToken)
            {
                return true;
            }

            var title = $"{operationName} 보류";
            var message = "서버 책장 정보를 불러온 뒤 변경할 수 있습니다. 로그인 상태와 API 연결을 확인해 주세요.";
            ShowBookshelfTransientStatus(title, message, true);
            ShowDeleteResultDialog(title, message, false);
            return false;
        }

        private bool EnsureBookshelfApiMutationReady(int displayIndex, string operationName)
        {
            if (!EnsureBookshelfApiMutationReady(operationName))
            {
                return false;
            }

            if (!usesHttpApiMode)
            {
                return true;
            }

            if (bookshelfGenerator != null &&
                displayIndex >= 0 &&
                !string.IsNullOrWhiteSpace(bookshelfGenerator.GetBookshelfServerId(displayIndex)))
            {
                return true;
            }

            var title = $"{operationName} 보류";
            var message = "대상 책장의 서버 식별자가 아직 준비되지 않았습니다. 새로고침 후 다시 시도해 주세요.";
            ShowBookshelfTransientStatus(title, message, true);
            ShowDeleteResultDialog(title, message, false);
            return false;
        }

        private bool EnsureBookshelfReorderApiReady(string operationName)
        {
            if (!EnsureBookshelfApiMutationReady(operationName))
            {
                return false;
            }

            if (!usesHttpApiMode || bookshelfGenerator == null)
            {
                return true;
            }

            if (bookshelfGenerator.GetBookshelfServerIdsInDisplayOrder().Count == bookshelfGenerator.BookshelfCount)
            {
                return true;
            }

            var title = $"{operationName} 보류";
            var message = "모든 책장의 서버 식별자가 준비된 뒤 순서를 변경할 수 있습니다. 새로고침 후 다시 시도해 주세요.";
            ShowBookshelfTransientStatus(title, message, true);
            ShowDeleteResultDialog(title, message, false);
            return false;
        }

        private string BuildDefaultNewBookshelfName()
        {
            var nextIndex = bookshelfGenerator != null ? bookshelfGenerator.BookshelfCount + 1 : 1;
            return $"{Mathf.Max(1, nextIndex)}번 책장";
        }

        private bool CanStartBookshelfApiMutation()
        {
            return bookshelfApiSyncController != null &&
                   bookshelfApiSyncController.IsServerSyncEnabled &&
                   (!usesHttpApiMode || bookshelfApiSyncController.HasAccessToken);
        }

        private void SyncBookshelfUpdateOrRefresh(int displayIndex, string operationName)
        {
            if (!CanStartBookshelfApiMutation())
            {
                if (usesHttpApiMode)
                {
                    RefreshServerStateAfterMutationFailure(
                        operationName,
                        "책장 API 동기화가 준비되지 않아 서버에 저장할 수 없습니다.");
                }

                return;
            }

            var retryKey = BuildMutationRetryKey(operationName, displayIndex);
            if (!bookshelfApiSyncController.UpdateBookshelf(
                    displayIndex,
                    () =>
                    {
                        ClearPendingMutationRetry(retryKey);
                        ShowBookshelfTransientStatus("책장 저장 완료", $"{operationName}이 서버에 반영되었습니다.", false);
                    },
                    error => RefreshServerStateAfterMutationFailure(
                        operationName,
                        error,
                        () => SyncBookshelfUpdateOrRefresh(displayIndex, operationName),
                        retryKey)))
            {
                RefreshServerStateAfterMutationFailure(
                    operationName,
                    "책장 서버 식별자를 찾을 수 없습니다.",
                    () => SyncBookshelfUpdateOrRefresh(displayIndex, operationName),
                    retryKey);
            }
        }

        private void SyncBookshelfReorderOrRefresh(string operationName)
        {
            if (!CanStartBookshelfApiMutation())
            {
                if (usesHttpApiMode)
                {
                    RefreshServerStateAfterMutationFailure(
                        operationName,
                        "책장 API 동기화가 준비되지 않아 책장 순서를 서버에 저장할 수 없습니다.");
                }

                return;
            }

            var retryKey = BuildMutationRetryKey(operationName, "all");
            if (!bookshelfApiSyncController.ReorderBookshelves(
                    () =>
                    {
                        ClearPendingMutationRetry(retryKey);
                        ShowBookshelfTransientStatus("책장 순서 저장 완료", "책장 순서가 서버에 반영되었습니다.", false);
                    },
                    error => RefreshServerStateAfterMutationFailure(
                        operationName,
                        error,
                        () => SyncBookshelfReorderOrRefresh(operationName),
                        retryKey)))
            {
                RefreshServerStateAfterMutationFailure(
                    operationName,
                    "책장 순서 저장에 필요한 서버 식별자를 찾을 수 없습니다.",
                    () => SyncBookshelfReorderOrRefresh(operationName),
                    retryKey);
            }
        }

        private void SyncBookPlacementOrRefresh(BookEntity book, string operationName)
        {
            if (!CanStartBookshelfApiMutation())
            {
                if (usesHttpApiMode)
                {
                    RefreshServerStateAfterMutationFailure(
                        operationName,
                        "책장 API 동기화가 준비되지 않아 책 위치를 서버에 저장할 수 없습니다.");
                }

                return;
            }

            var retryKey = BuildMutationRetryKey(operationName, book);
            var retryAction = CreateBookRetryAction(
                book,
                operationName,
                () => SyncBookPlacementOrRefresh(book, operationName));
            if (!bookshelfApiSyncController.SyncBookPlacement(
                    book,
                    _ =>
                    {
                        ClearPendingMutationRetry(retryKey);
                        ShowBookshelfTransientStatus("책 배치 저장 완료", "책 위치가 서버에 반영되었습니다.", false);
                        RefreshHud();
                    },
                    error => RefreshServerStateAfterMutationFailure(
                        operationName,
                        error,
                        retryAction,
                        retryKey)))
            {
                RefreshServerStateAfterMutationFailure(
                    operationName,
                    "책 배치 저장에 필요한 서버 식별자를 찾을 수 없습니다.",
                    null,
                    retryKey);
            }
        }

        private void SyncBookStateOrRollback(
            BookEntity book,
            BookStateMutationSnapshot rollback,
            BookStateMutationSnapshot desired,
            string operationName)
        {
            if (!CanStartBookshelfApiMutation())
            {
                if (usesHttpApiMode)
                {
                    RollbackBookStateMutation(
                        book,
                        rollback,
                        operationName,
                        "책장 API 동기화가 준비되지 않아 책 상태를 서버에 저장할 수 없습니다.");
                }

                return;
            }

            var retryKey = BuildMutationRetryKey(operationName, book);
            var retryAction = CreateBookStateRetryAction(
                book,
                rollback,
                desired,
                operationName,
                () => SyncBookStateOrRollback(book, rollback, desired, operationName));
            if (!bookshelfApiSyncController.SyncBookState(
                    book,
                    updatedBook =>
                    {
                        ClearPendingMutationRetry(retryKey);
                        ApplyBookEntityToMeta(updatedBook);
                        ShowBookshelfTransientStatus("책 상태 저장 완료", $"{operationName}이 서버에 반영되었습니다.", false);
                        RefreshBookDetailPanel();
                        RefreshHud();
                    },
                    error => RollbackBookStateMutation(
                        book,
                        rollback,
                        operationName,
                        error,
                        retryAction,
                        retryKey)))
            {
                RollbackBookStateMutation(
                    book,
                    rollback,
                    operationName,
                    "책 상태 저장에 필요한 서버 식별자를 찾을 수 없습니다.",
                    null,
                    retryKey);
            }
        }

        private void SyncBookLikeOrRollback(
            BookEntity book,
            BookStateMutationSnapshot rollback,
            BookStateMutationSnapshot desired,
            string operationName)
        {
            if (!CanStartBookshelfApiMutation())
            {
                if (usesHttpApiMode)
                {
                    RollbackBookStateMutation(
                        book,
                        rollback,
                        operationName,
                        "책장 API 동기화가 준비되지 않아 좋아요를 서버에 저장할 수 없습니다.");
                }

                return;
            }

            var retryKey = BuildMutationRetryKey(operationName, book);
            var retryAction = CreateBookStateRetryAction(
                book,
                rollback,
                desired,
                operationName,
                () => SyncBookLikeOrRollback(book, rollback, desired, operationName));
            if (!bookshelfApiSyncController.SyncBookLike(
                    book,
                    updatedBook =>
                    {
                        ClearPendingMutationRetry(retryKey);
                        ApplyBookEntityToMeta(updatedBook);
                        ShowBookshelfTransientStatus("좋아요 저장 완료", "좋아요 상태가 서버에 반영되었습니다.", false);
                        RefreshBookDetailPanel();
                        RefreshHud();
                    },
                    error => RollbackBookStateMutation(
                        book,
                        rollback,
                        operationName,
                        error,
                        retryAction,
                        retryKey)))
            {
                RollbackBookStateMutation(
                    book,
                    rollback,
                    operationName,
                    "좋아요 저장에 필요한 서버 식별자를 찾을 수 없습니다.",
                    null,
                    retryKey);
            }
        }

        private void RollbackBookStateMutation(
            BookEntity book,
            BookStateMutationSnapshot rollback,
            string operationName,
            string errorMessage,
            System.Action retryAction = null,
            string retryKey = null)
        {
            Debug.LogWarning($"{operationName} failed on server. Restoring local book state. {errorMessage}");
            var pendingRetryAction = retryAction != null
                ? RegisterPendingMutationRetry(retryKey, operationName, errorMessage, retryAction)
                : null;
            if (retryAction == null)
            {
                ClearPendingMutationRetry(retryKey);
            }

            ShowBookshelfTransientStatus(
                $"{operationName} 실패",
                retryAction == null ? "서버 요청을 완료하지 못해 변경 전 상태로 되돌렸습니다." : "변경 전 상태로 되돌렸습니다. 재시도할 수 있습니다.",
                true);
            rollback.ApplyTo(book);

            if (dataStore != null && book != null)
            {
                var meta = dataStore.GetOrCreateBookMeta(book);
                rollback.ApplyTo(meta);
                dataStore.ApplyBookMeta(book, meta);
            }

            ShowDeleteResultDialog(
                $"{operationName} 실패",
                $"서버 요청을 완료하지 못해 변경 전 상태로 되돌렸습니다.\n{NormalizeMutationErrorMessage(errorMessage)}",
                false,
                pendingRetryAction,
                "재시도");
            RefreshBookDetailPanel();
            RefreshHud();
        }

        private void RetryBookStateMutation(BookEntity book, BookStateMutationSnapshot desired, System.Action syncAction)
        {
            desired.ApplyTo(book);
            if (dataStore != null && book != null)
            {
                var meta = dataStore.GetOrCreateBookMeta(book);
                desired.ApplyTo(meta);
                dataStore.ApplyBookMeta(book, meta);
            }

            RefreshBookDetailPanel();
            RefreshHud();
            syncAction?.Invoke();
        }

        private void ToggleReadState()
        {
            if (TryCloseBookDetailFromFocusedBookPointer())
            {
                return;
            }

            var book = ActiveDetailBook != null ? ActiveDetailBook : (state != null ? state.ActiveViewedBook : null);
            if (book == null || dataStore == null || bookReviewWritePanel == null || !CanUseOwnerBookActions())
            {
                return;
            }

            var meta = dataStore.GetOrCreateBookMeta(book);
            if (meta == null)
            {
                return;
            }

            var rollback = new BookStateMutationSnapshot(book, meta);
            meta.readingStatus = GetNextReadingStatus(meta.readingStatus);
            meta.isRead = meta.readingStatus == App.Domain.ReadingStatus.Read;
            dataStore.ApplyBookMeta(book, meta);
            var desired = new BookStateMutationSnapshot(book, meta);
            SyncBookStateOrRollback(book, rollback, desired, "독서 상태 변경");
            RefreshBookDetailPanel();
            RefreshHud();
        }

        private void ToggleLikeState()
        {
            if (TryCloseBookDetailFromFocusedBookPointer())
            {
                return;
            }

            var book = ActiveDetailBook != null ? ActiveDetailBook : (state != null ? state.ActiveViewedBook : null);
            if (book == null ||
                dataStore == null ||
                !CanUseOwnerBookActions())
            {
                return;
            }

            var meta = dataStore.GetOrCreateBookMeta(book);
            if (meta == null)
            {
                return;
            }

            var rollback = new BookStateMutationSnapshot(book, meta);
            var wasLiked = meta.isLiked;
            meta.isLiked = !meta.isLiked;
            if (meta.hasAggregateBookStats && wasLiked != meta.isLiked)
            {
                meta.totalLikeCount = Mathf.Max(0, meta.totalLikeCount + (meta.isLiked ? 1 : -1));
            }
            dataStore.ApplyBookMeta(book, meta);
            var desired = new BookStateMutationSnapshot(book, meta);
            SyncBookLikeOrRollback(book, rollback, desired, "좋아요 변경");
            RefreshBookDetailPanel();
            RefreshHud();
        }

        private void CycleOwnerRating()
        {
            OpenBookReviewWritePanel();
        }

        private void OpenBookReviewWritePanel()
        {
            if (TryCloseBookDetailFromFocusedBookPointer())
            {
                return;
            }

            var book = ActiveDetailBook != null ? ActiveDetailBook : (state != null ? state.ActiveViewedBook : null);
            if (book == null || dataStore == null || bookReviewWritePanel == null || !CanUseOwnerBookActions())
            {
                return;
            }

            if (!TryResolveBookReviewApiId(book, out _))
            {
                ShowDeleteResultDialog("리뷰 작성 불가", "도서 서버 식별자를 찾을 수 없습니다.", false);
                return;
            }

            var meta = dataStore.GetOrCreateBookMeta(book);
            CloseOverlayPanels(bookReviewWritePanel);
            activeReviewBook = book;
            stagedBookReviewRating = meta != null && meta.ownerRating > 0f
                ? SnapRatingToStep(Mathf.Clamp(meta.ownerRating, BookRatingStep, 5f))
                : 0f;
            isBookReviewWritePanelOpen = true;
            isBookReviewSaving = false;
            SetBookReviewStatus(string.Empty);
            if (bookReviewContentInputField != null)
            {
                bookReviewContentInputField.text = string.Empty;
            }

            RefreshHud();
        }

        private void SetStagedBookReviewRating(float rating)
        {
            stagedBookReviewRating = Mathf.Clamp(SnapRatingToStep(rating), BookRatingStep, 5f);
            SetBookReviewStatus(string.Empty);
            RefreshBookReviewWritePanel();
        }

        private void SaveBookReview()
        {
            if (isBookReviewSaving)
            {
                return;
            }

            var book = activeReviewBook;
            if (book == null)
            {
                CloseBookReviewWritePanel();
                return;
            }

            if (!TryResolveBookReviewApiId(book, out var bookId))
            {
                SetBookReviewStatus("도서 서버 식별자를 찾을 수 없습니다.", true);
                RefreshBookReviewWritePanel();
                return;
            }

            var rating = Mathf.Clamp(SnapRatingToStep(stagedBookReviewRating), 0f, 5f);
            if (rating < BookRatingStep)
            {
                SetBookReviewStatus("별점을 선택해 주세요.", true);
                RefreshBookReviewWritePanel();
                return;
            }

            var content = bookReviewContentInputField != null ? bookReviewContentInputField.text : string.Empty;
            content = string.IsNullOrWhiteSpace(content) ? string.Empty : content.Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                SetBookReviewStatus("리뷰 내용을 입력해 주세요.", true);
                RefreshBookReviewWritePanel();
                return;
            }

            if (content.Length > 1000)
            {
                content = content.Substring(0, 1000);
            }

            if (notebookRuntimeUseCase == null)
            {
                SetBookReviewStatus("리뷰 저장 기능이 준비되지 않았습니다.", true);
                RefreshBookReviewWritePanel();
                return;
            }

            isBookReviewSaving = true;
            SetBookReviewStatus("리뷰 저장 중입니다.");
            RefreshBookReviewWritePanel();
            notebookRuntimeUseCase.CreateBookReview(
                bookId,
                rating,
                content,
                review => HandleBookReviewCreated(book, rating, review),
                HandleBookReviewCreateFailed);
        }

        private void HandleBookReviewCreated(BookEntity book, float fallbackRating, NotebookReviewItem review)
        {
            isBookReviewSaving = false;

            if (book != null && dataStore != null)
            {
                var meta = dataStore.GetOrCreateBookMeta(book);
                if (meta != null)
                {
                    var nextRating = review != null && review.Rating > 0f ? review.Rating : fallbackRating;
                    meta.ownerRating = Mathf.Clamp(SnapRatingToStep(nextRating), BookRatingStep, 5f);
                    ApplyCreatedReviewToLocalAggregate(meta, meta.ownerRating);
                    dataStore.ApplyBookMeta(book, meta);
                }

                if (bookshelfApiSyncController != null && bookshelfApiSyncController.IsServerSyncEnabled)
                {
                    bookshelfApiSyncController.FetchBookDetails(book, updatedBook =>
                    {
                        ApplyBookEntityToMeta(updatedBook);
                        RefreshBookDetailPanel();
                        RefreshHud();
                    });
                }
            }

            isBookReviewWritePanelOpen = false;
            activeReviewBook = null;
            if (bookReviewWritePanel != null)
            {
                bookReviewWritePanel.SetActive(false);
            }

            RefreshBookDetailPanel();
            RefreshHud();
        }

        private void HandleBookReviewCreateFailed(NotebookApiError error)
        {
            isBookReviewSaving = false;
            var message = error != null ? FirstNonEmpty(FirstNonEmpty(error.Message, error.Error), "리뷰 저장에 실패했습니다.") : "리뷰 저장에 실패했습니다.";
            SetBookReviewStatus(NormalizeMutationErrorMessage(message), true);
            RefreshBookReviewWritePanel();
            RefreshHud();
        }

        private void CloseBookReviewWritePanel()
        {
            isBookReviewWritePanelOpen = false;
            isBookReviewSaving = false;
            activeReviewBook = null;
            SetBookReviewStatus(string.Empty);
            if (bookReviewContentInputField != null)
            {
                bookReviewContentInputField.text = string.Empty;
            }

            if (bookReviewWritePanel != null)
            {
                bookReviewWritePanel.SetActive(false);
            }

            RefreshHud();
        }

        private void SetBookReviewStatus(string message, bool isError = false)
        {
            bookReviewStatusMessage = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
            bookReviewStatusIsError = isError;
        }

        private static bool TryResolveBookReviewApiId(BookEntity book, out long bookId)
        {
            bookId = 0;
            return book != null &&
                   !string.IsNullOrWhiteSpace(book.bookId) &&
                   long.TryParse(book.bookId.Trim(), out bookId) &&
                   bookId > 0;
        }

        private static void ApplyCreatedReviewToLocalAggregate(BookMetaData meta, float rating)
        {
            if (meta == null)
            {
                return;
            }

            var nextRating = Mathf.Clamp(SnapRatingToStep(rating), BookRatingStep, 5f);
            var count = meta.hasAggregateBookStats ? Mathf.Max(0, meta.reviewCount) : 0;
            var total = (meta.hasAggregateBookStats ? Mathf.Clamp(meta.averageRating, 0f, 5f) : 0f) * count;
            total += nextRating;
            count++;
            meta.reviewCount = count;
            meta.averageRating = Mathf.Clamp(total / count, 0f, 5f);
            meta.hasAggregateBookStats = true;
        }

        private static float GetRatingChoiceValue(int index)
        {
            return Mathf.Clamp((index + 1) * BookRatingStep, BookRatingStep, 5f);
        }

        private static float SnapRatingToStep(float rating)
        {
            return Mathf.Round(Mathf.Clamp(rating, 0f, 5f) / BookRatingStep) * BookRatingStep;
        }

        private void ToggleVisibilityState()
        {
            if (TryCloseBookDetailFromFocusedBookPointer())
            {
                return;
            }

            var meta = dataStore.GetOrCreateBookMeta(ActiveDetailBook);
            if (meta == null)
            {
                return;
            }

            var rollback = new BookReportMutationSnapshot(ActiveDetailBook, meta);
            meta.reportVisibility = meta.reportVisibility == App.Domain.VisibilityScope.Public
                ? App.Domain.VisibilityScope.Private
                : App.Domain.VisibilityScope.Public;
            meta.isPublic = meta.reportVisibility == App.Domain.VisibilityScope.Public;
            dataStore.ApplyBookMeta(ActiveDetailBook, meta);
            var book = ActiveDetailBook;
            var didStartSave = CanStartBookshelfApiMutation() &&
                               bookshelfApiSyncController.SaveReadingRecord(
                                   book,
                                   HandleBookReportSaved,
                                   error => HandleBookReportSaveFailure(book, rollback, error));
            if (!didStartSave)
            {
                if (usesHttpApiMode && !CanStartBookshelfApiMutation())
                {
                    HandleBookReportSaveFailure(book, rollback, "책장 API 동기화가 준비되지 않아 독후감 공개 범위를 서버에 저장할 수 없습니다.");
                    return;
                }

                if (bookshelfApiSyncController != null && bookshelfApiSyncController.IsServerSyncEnabled)
                {
                    HandleBookReportSaveFailure(book, rollback, "독후감 공개 범위 저장에 필요한 서버 식별자를 찾을 수 없습니다.");
                    return;
                }
            }

            RefreshBookDetailPanel();
            RefreshHud();
        }

        private bool TryCloseBookDetailFromFocusedBookPointer()
        {
            if (bookDetailPanel == null ||
                !bookDetailPanel.activeSelf ||
                ActiveDetailBook == null ||
                Mouse.current == null ||
                Camera.main == null)
            {
                return false;
            }

            if (IsPointerOverUi())
            {
                return false;
            }

            var pointer = Mouse.current.position.ReadValue();
            var ray = Camera.main.ScreenPointToRay(pointer);
            var hits = Physics.RaycastAll(ray, 100f);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (var i = 0; i < hits.Length; i++)
            {
                var hitArea = hits[i].collider.GetComponent<BookInteractionHitArea>();
                if (hitArea != null)
                {
                    if (hitArea.Owner == ActiveDetailBook)
                    {
                        CloseBookDetailPanel();
                        return true;
                    }

                    continue;
                }

                var book = hits[i].collider.GetComponentInParent<BookEntity>();
                if (book == null)
                {
                    continue;
                }

                if (book == ActiveDetailBook)
                {
                    CloseBookDetailPanel();
                    return true;
                }

                return false;
            }

            return false;
        }

        public void OpenGuestbookPanel()
        {
            EnsureGuestbookBoardUi();
            CloseOverlayPanels(guestbookPanel);
            SetGuestbookFocusPresentation(true);
            RefreshGuestbookPanel();
        }

        public void CloseGuestbookPanel()
        {
            if (guestbookPageFadeCoroutine != null)
            {
                StopCoroutine(guestbookPageFadeCoroutine);
                guestbookPageFadeCoroutine = null;
            }

            if (guestbookPanel != null)
            {
                guestbookPanel.SetActive(false);
            }

            SetGuestbookFocusPresentation(false);
        }

        private void HandleCameraModeChanged(CameraMode mode)
        {
            if (mode != CameraMode.Frontal &&
                state != null &&
                state.IsViewingBook &&
                interactionController != null)
            {
                interactionController.CloseViewedBook(true);
            }

            SyncFromCameraMode(mode);
        }

        private void HandleCameraTransitionCompleted(CameraMode mode)
        {
            if (mode == CameraMode.Notebook && CurrentFocusArea == ExperienceFocusArea.Notebook)
            {
                if (notebookScreenController != null)
                {
                    notebookScreenController.EnterImmersiveDashboard();
                }
                else
                {
                    ShowNotebookScreen();
                }
            }

            RefreshHud();
        }

        private void HandleBookshelfSwitchStateChanged()
        {
            if (bookshelfGenerator != null && bookshelfGenerator.IsBookshelfSwitching && IsBookshelfEditMode)
            {
                SetBookshelfEditMode(false);
            }

            RefreshHud();

            // 책장 swap (a→b 등) 시점에도 GuestbookBoardHitArea 의 transform 이 갱신될 수 있어
            // DashboardScreenPanel mount 위치/scale 을 재계산. SyncExternalDashboardPanelPresentation
            // 자체에 useExternalDashboardPanel/null 가드가 들어있어 정상 케이스에 부수효과 없음 (idempotent).
            // 첫 generate race 는 Generated 이벤트로 별도 회복 (HandleBookshelfGenerated).
            SyncExternalDashboardPanelPresentation();
        }

        private void HandleBookshelfGenerated()
        {
            // 신규 가입 직후 BookshelfExperienceManager.OnEnable 시점에 BookshelfGenerator.Generate() 가
            // 아직 호출되지 않아 FindFirstObjectByType<GuestbookBoardHitArea>() == null →
            // MountExternalDashboardPanel false → externalDashboardPanel.SetActive(false) →
            // DashboardScreenController.OnEnable 자체가 발사되지 않는 race 가 있었다 (빈 액자 회귀).
            //
            // BookshelfGenerator.Generate() 가 완료되면 BuildGuestbookBoard() 내부에서 hitArea 가 막
            // AddComponent 된 시점이라, 여기서 panel mount 를 재시도하면 깨끗하게 회복된다.
            // 기존 유저 재로그인 케이스는 OnEnable 시점에 이미 mount 가 성공한 상태라 부수효과 없음 (idempotent).
            SyncExternalDashboardPanelPresentation();
        }

        private void SyncFromCameraMode(CameraMode mode)
        {
            switch (mode)
            {
                case CameraMode.Frontal:
                    CurrentFocusArea = ExperienceFocusArea.Bookshelf;
                    break;
                case CameraMode.Whiteboard:
                    CurrentFocusArea = ExperienceFocusArea.Guestbook;
                    break;
                case CameraMode.Notebook:
                    CurrentFocusArea = ExperienceFocusArea.Notebook;
                    break;
                default:
                    CurrentFocusArea = ExperienceFocusArea.QuarterView;
                    break;
            }

            if (CurrentFocusArea != ExperienceFocusArea.Bookshelf && IsBookshelfEditMode)
            {
                SetBookshelfEditMode(false);
            }

            if (CurrentFocusArea != ExperienceFocusArea.Bookshelf)
            {
                CancelBookPlacementEdit(false);
            }

            if (CurrentFocusArea == ExperienceFocusArea.Notebook)
            {
                CloseOverlayPanels();
                isHamburgerMenuOpen = false;
            }
            else
            {
                if (notebookScreenController != null)
                {
                    notebookScreenController.SetImmersiveMode(false);
                }

                ShowNotebookScreen();
            }

            if (CurrentFocusArea == ExperienceFocusArea.Guestbook)
            {
                OpenGuestbookPanel();
            }
            else
            {
                CloseGuestbookPanel();
            }

            isEmptyBookshelfTutorialEntryPending = false;

            RefreshHud();
        }

        private void EnsureHud()
        {
            var canvasObject = new GameObject("ExperienceHud");
            hudCanvas = canvasObject.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var canvasScaler = canvasObject.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = HudReferenceResolution;
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            hamburgerButton = CreateButton(canvasObject.transform, "HamburgerButton", "Menu", new Vector2(-20f, -20f), ToggleHamburgerMenu, new Vector2(72f, 44f), new Vector2(1f, 1f));
            hamburgerButton.GetComponentInChildren<Text>().fontSize = 26;
            StyleHudPanel(hamburgerButton.GetComponent<Image>(), new Color(0.08f, 0.09f, 0.11f, 0.78f));

            focusBackButton = CreateButton(canvasObject.transform, "FocusBackButton", "←", new Vector2(34f, -34f), ReturnFromFocusedArea, new Vector2(66f, 66f), new Vector2(0f, 1f));
            StyleHudPanel(focusBackButton.GetComponent<Image>(), new Color(0.08f, 0.09f, 0.11f, 0.78f));

            hamburgerMenuPanel = CreatePanel(canvasObject.transform, "HamburgerMenuPanel", new Vector2(-20f, -74f), new Vector2(190f, 204f), new Vector2(1f, 1f));
            StyleHudPanel(hamburgerMenuPanel.GetComponent<Image>(), new Color(0.08f, 0.09f, 0.11f, 0.84f));
            CreateButton(hamburgerMenuPanel.transform, "QuarterViewButton", "Quarter View", new Vector2(-10f, -10f), () => RunMenuAction(FocusQuarterView));
            CreateButton(hamburgerMenuPanel.transform, "BookshelfButton", "Bookshelf", new Vector2(-10f, -58f), () => RunMenuAction(FocusBookshelf));
            CreateButton(hamburgerMenuPanel.transform, "NotebookButton", "Notebook", new Vector2(-10f, -106f), () => RunMenuAction(FocusNotebook));
            CreateButton(hamburgerMenuPanel.transform, "GuestbookButton", "Guestbook", new Vector2(-10f, -154f), () => RunMenuAction(FocusGuestbook));
            overlayPreviousFocusButton = CreateButton(canvasObject.transform, "OverlayPreviousFocusButton", "‹", new Vector2(220f, 0f), ShowPreviousBookshelf, new Vector2(52f, 52f), new Vector2(0f, 0.5f));
            overlayNextFocusButton = CreateButton(canvasObject.transform, "OverlayNextFocusButton", "›", new Vector2(-220f, 0f), ShowNextBookshelf, new Vector2(52f, 52f), new Vector2(1f, 0.5f));
            PrepareBookshelfNavigationButton(overlayPreviousFocusButton);
            PrepareBookshelfNavigationButton(overlayNextFocusButton);

            bookshelfEditModeButton = CreateButton(canvasObject.transform, "BookshelfEditModeButton", "저장", new Vector2(-70f, -34f), ToggleBookshelfEditMode, new Vector2(86f, 44f), new Vector2(1f, 1f));
            bookshelfEditModeButton.GetComponentInChildren<Text>().fontSize = 15;

            bookshelfIndexPanel = CreatePanel(canvasObject.transform, "BookshelfIndexPanel", new Vector2(0f, -18f), new Vector2(252f, 72f), new Vector2(0.5f, 1f));
            StyleBookshelfIndexPanel(bookshelfIndexPanel.GetComponent<Image>());
            bookshelfIndexText = CreateLabel(bookshelfIndexPanel.transform, "BookshelfIndexText", new Vector2(0f, -14f), new Vector2(212f, 28f), TextAnchor.MiddleCenter);
            var bookshelfIndexTextRect = bookshelfIndexText.GetComponent<RectTransform>();
            if (bookshelfIndexTextRect != null)
            {
                bookshelfIndexTextRect.anchorMin = new Vector2(0.5f, 1f);
                bookshelfIndexTextRect.anchorMax = new Vector2(0.5f, 1f);
                bookshelfIndexTextRect.pivot = new Vector2(0.5f, 1f);
                bookshelfIndexTextRect.anchoredPosition = new Vector2(0f, -14f);
                bookshelfIndexTextRect.sizeDelta = new Vector2(212f, 28f);
            }

            bookshelfIndexText.fontSize = 19;
            bookshelfIndexText.fontStyle = FontStyle.Bold;
            bookshelfIndexText.color = new Color32(63, 47, 38, 255);
            bookshelfIndexDotsRoot = CreateHudContainer(bookshelfIndexPanel.transform, "BookshelfIndexDots", new Vector2(0f, -52f), new Vector2(112f, 8f), new Vector2(0.5f, 1f));
            ConfigureHorizontalLayout(bookshelfIndexDotsRoot, TextAnchor.MiddleCenter, 6f);

            bookshelfBottomNavPanel = CreatePanel(canvasObject.transform, "BookshelfBottomNavPanel", new Vector2(0f, 28f), new Vector2(680f, 104f), new Vector2(0.5f, 0f));
            StyleBookshelfIndexPanel(bookshelfBottomNavPanel.GetComponent<Image>());
            bookshelfNavHomeButton = CreateBottomNavButton(bookshelfBottomNavPanel.transform, "BookshelfNavHomeButton", "⌂\n책장", 0, FocusBookshelfHomeFromBottom);
            bookshelfNavSearchButton = CreateBottomNavButton(bookshelfBottomNavPanel.transform, "BookshelfNavSearchButton", "검색", 1, ToggleBookSearchPanel);
            bookshelfNavAddButton = CreateBottomNavButton(bookshelfBottomNavPanel.transform, "BookshelfNavAddButton", "+\n추가", 2, OpenBookAddFromBottom);
            bookshelfNavEditButton = CreateBottomNavButton(bookshelfBottomNavPanel.transform, "BookshelfNavEditButton", "✎\n편집", 3, ToggleBookshelfEditMode);

            addBookshelfButton = CreateButton(canvasObject.transform, "AddBookshelfButton", "책장 추가", new Vector2(-128f, -68f), AddBookshelf, new Vector2(92f, 34f), new Vector2(0.5f, 1f));
            addBookshelfButton.GetComponentInChildren<Text>().fontSize = 14;

            emptyBookshelfAddButton = CreateButton(canvasObject.transform, "EmptyBookshelfAddButton", "+\n책장 추가", Vector2.zero, AddEmptyBookshelfFromPlaceholder, new Vector2(150f, 150f), new Vector2(0.5f, 0.5f));
            emptyBookshelfAddButton.gameObject.SetActive(false);

            openBookshelfNameEditButton = CreateButton(canvasObject.transform, "OpenBookshelfNameEditButton", "이름 변경", new Vector2(0f, -68f), OpenBookshelfNameEditPanel, new Vector2(128f, 34f), new Vector2(0.5f, 1f));
            openBookshelfNameEditButton.GetComponentInChildren<Text>().fontSize = 14;

            deleteBookshelfButton = CreateButton(canvasObject.transform, "DeleteBookshelfButton", "책장 삭제", new Vector2(128f, -68f), DeleteActiveBookshelf, new Vector2(92f, 34f), new Vector2(0.5f, 1f));
            deleteBookshelfButton.GetComponentInChildren<Text>().fontSize = 14;

            openBookshelfDesignButton = CreateButton(canvasObject.transform, "OpenBookshelfDesignButton", "디자인변경", new Vector2(-184f, -34f), OpenBookshelfDesignPanel, new Vector2(122f, 44f), new Vector2(1f, 1f));
            openBookshelfDesignButton.GetComponentInChildren<Text>().fontSize = 14;

            openBookAddButton = CreateButton(canvasObject.transform, "OpenBookAddButton", "책 추가", new Vector2(-268f, -20f), OpenBookAddPanel, new Vector2(104f, 44f), new Vector2(1f, 1f));
            openBookAddButton.GetComponentInChildren<Text>().fontSize = 15;

            openBookSearchButton = CreateButton(canvasObject.transform, "OpenBookSearchButton", "검색", new Vector2(-144f, -20f), ToggleBookSearchPanel, new Vector2(94f, 44f), new Vector2(1f, 1f));
            openBookSearchButton.GetComponentInChildren<Text>().fontSize = 15;

            openBookshelfManageButton = CreateButton(canvasObject.transform, "OpenBookshelfManageButton", "책장관리", new Vector2(-312f, -34f), OpenBookshelfManagePanel, new Vector2(112f, 44f), new Vector2(1f, 1f));
            openBookshelfManageButton.GetComponentInChildren<Text>().fontSize = 14;

            bookshelfNameModalBackdrop = CreateModalBackdrop(canvasObject.transform, "BookshelfNameModalBackdrop", new Color(0.16f, 0.11f, 0.07f, 0.36f));
            bookshelfNameModalBackdrop.SetActive(false);

            firstBookshelfTutorialBackdrop = CreateModalBackdrop(canvasObject.transform, "FirstBookshelfTutorialBackdrop", new Color(0.16f, 0.11f, 0.07f, 0.46f));
            firstBookshelfTutorialBackdrop.SetActive(false);
            firstBookshelfTutorialPanel = CreatePanel(canvasObject.transform, "FirstBookshelfTutorialPanel", Vector2.zero, new Vector2(620f, 360f), new Vector2(0.5f, 0.5f));
            StyleBookshelfIndexPanel(firstBookshelfTutorialPanel.GetComponent<Image>());
            firstBookshelfTutorialPanel.SetActive(false);
            firstBookshelfTutorialTitleText = CreateLabel(firstBookshelfTutorialPanel.transform, "FirstBookshelfTutorialTitleText", new Vector2(38f, -38f), new Vector2(544f, 36f), TextAnchor.UpperLeft);
            firstBookshelfTutorialTitleText.text = "첫 책장을 만들어볼까요?";
            firstBookshelfTutorialTitleText.fontSize = 24;
            firstBookshelfTutorialTitleText.fontStyle = FontStyle.Bold;
            firstBookshelfTutorialTitleText.color = new Color32(45, 69, 56, 255);
            firstBookshelfTutorialMessageText = CreateLabel(firstBookshelfTutorialPanel.transform, "FirstBookshelfTutorialMessageText", new Vector2(38f, -86f), new Vector2(544f, 72f), TextAnchor.UpperLeft);
            firstBookshelfTutorialMessageText.text = "아직 책장이 없습니다. 책장은 책을 전시하고 정리하는 기본 공간입니다. 먼저 책장 하나를 만들고 이름을 정해보세요.";
            firstBookshelfTutorialMessageText.fontSize = 17;
            firstBookshelfTutorialMessageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            firstBookshelfTutorialMessageText.verticalOverflow = VerticalWrapMode.Truncate;
            firstBookshelfTutorialMessageText.color = new Color32(92, 82, 72, 255);
            firstBookshelfTutorialStepsText = CreateLabel(firstBookshelfTutorialPanel.transform, "FirstBookshelfTutorialStepsText", new Vector2(38f, -178f), new Vector2(544f, 86f), TextAnchor.UpperLeft);
            firstBookshelfTutorialStepsText.text = "1. 책장 추가\n2. 책장 이름 정하기\n3. 검색 또는 추가에서 첫 책 꽂기";
            firstBookshelfTutorialStepsText.fontSize = 17;
            firstBookshelfTutorialStepsText.lineSpacing = 1.18f;
            firstBookshelfTutorialStepsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            firstBookshelfTutorialStepsText.color = new Color32(64, 84, 68, 255);
            firstBookshelfTutorialAddButton = CreateButton(firstBookshelfTutorialPanel.transform, "FirstBookshelfTutorialAddButton", "책장 추가하기", new Vector2(384f, -292f), AddFirstBookshelfFromTutorial, new Vector2(198f, 48f), new Vector2(0f, 1f));

            bookshelfNameEditPanel = CreatePanel(canvasObject.transform, "BookshelfNameEditPanel", new Vector2(0f, -118f), new Vector2(380f, 92f), new Vector2(0.5f, 1f));
            StyleBookshelfIndexPanel(bookshelfNameEditPanel.GetComponent<Image>());
            bookshelfNameEditPanel.SetActive(false);
            bookshelfNameEditLabelText = CreateLabel(bookshelfNameEditPanel.transform, "BookshelfNameEditLabelText", new Vector2(20f, -20f), new Vector2(340f, 18f), TextAnchor.UpperLeft);
            bookshelfNameEditLabelText.text = "책장 이름";
            bookshelfNameEditLabelText.fontSize = 13;
            bookshelfNameEditLabelText.fontStyle = FontStyle.Bold;
            bookshelfNameEditLabelText.color = new Color32(151, 104, 64, 255);
            bookshelfNameInputField = CreateHudInputField(bookshelfNameEditPanel.transform, "BookshelfNameInputField", new Vector2(8f, -46f), new Vector2(270f, 34f), "책장 이름");
            bookshelfNameInputField.characterLimit = 18;
            saveBookshelfNameButton = CreateButton(bookshelfNameEditPanel.transform, "SaveBookshelfNameButton", "저장", new Vector2(290f, -46f), SaveBookshelfName, new Vector2(54f, 34f), new Vector2(0f, 1f));
            closeBookshelfNameEditButton = CreateButton(bookshelfNameEditPanel.transform, "CloseBookshelfNameEditButton", "×", new Vector2(348f, -46f), CloseBookshelfNameEditPanel, new Vector2(28f, 34f), new Vector2(0f, 1f));

            bookshelfDeleteConfirmPanel = CreatePanel(canvasObject.transform, "BookshelfDeleteConfirmPanel", new Vector2(0f, -132f), new Vector2(388f, 146f), new Vector2(0.5f, 1f));
            StyleBookshelfIndexPanel(bookshelfDeleteConfirmPanel.GetComponent<Image>());
            bookshelfDeleteConfirmPanel.SetActive(false);
            CreateHudImage(bookshelfDeleteConfirmPanel.transform, "BookshelfDeleteAccent", new Vector2(20f, -22f), new Vector2(72f, 3f), new Vector2(0f, 1f), DeleteButtonBackground, false);
            bookshelfDeleteConfirmTitleText = CreateLabel(bookshelfDeleteConfirmPanel.transform, "BookshelfDeleteConfirmTitleText", new Vector2(20f, -32f), new Vector2(348f, 24f), TextAnchor.UpperLeft);
            bookshelfDeleteConfirmTitleText.text = "책장 삭제";
            bookshelfDeleteConfirmTitleText.fontSize = 17;
            bookshelfDeleteConfirmTitleText.fontStyle = FontStyle.Bold;
            bookshelfDeleteConfirmTitleText.color = new Color32(84, 48, 42, 255);
            bookshelfDeleteConfirmMessageText = CreateLabel(bookshelfDeleteConfirmPanel.transform, "BookshelfDeleteConfirmMessageText", new Vector2(20f, -62f), new Vector2(348f, 56f), TextAnchor.UpperLeft);
            bookshelfDeleteConfirmMessageText.fontSize = 15;
            bookshelfDeleteConfirmMessageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookshelfDeleteConfirmMessageText.color = new Color32(84, 74, 66, 255);
            confirmDeleteBookshelfButton = CreateButton(bookshelfDeleteConfirmPanel.transform, "ConfirmDeleteBookshelfButton", "삭제", new Vector2(198f, -106f), ConfirmDeleteBookshelf, new Vector2(82f, 32f), new Vector2(0f, 1f));
            cancelDeleteBookshelfButton = CreateButton(bookshelfDeleteConfirmPanel.transform, "CancelDeleteBookshelfButton", "취소", new Vector2(286f, -106f), CloseBookshelfDeleteConfirmPanel, new Vector2(82f, 32f), new Vector2(0f, 1f));

            bookshelfDesignPanel = CreatePanel(canvasObject.transform, "BookshelfDesignPanel", new Vector2(0f, -148f), new Vector2(480f, 210f), new Vector2(0.5f, 1f));
            StyleBookshelfIndexPanel(bookshelfDesignPanel.GetComponent<Image>());
            bookshelfDesignPanel.SetActive(false);
            CreateHudImage(bookshelfDesignPanel.transform, "BookshelfDesignAccent", new Vector2(24f, -24f), new Vector2(84f, 3f), new Vector2(0f, 1f), new Color32(197, 128, 67, 170), false);
            bookshelfDesignTitleText = CreateLabel(bookshelfDesignPanel.transform, "BookshelfDesignTitleText", new Vector2(24f, -36f), new Vector2(432f, 24f), TextAnchor.UpperLeft);
            bookshelfDesignTitleText.text = "책장 디자인 변경";
            bookshelfDesignTitleText.fontSize = 17;
            bookshelfDesignTitleText.fontStyle = FontStyle.Bold;
            bookshelfDesignTitleText.color = new Color32(63, 47, 38, 255);
            bookshelfDesignHintText = CreateLabel(bookshelfDesignPanel.transform, "BookshelfDesignHintText", new Vector2(24f, -70f), new Vector2(432f, 32f), TextAnchor.UpperLeft);
            bookshelfDesignHintText.text = "현재 책장에 적용할 분위기를 선택하세요.";
            bookshelfDesignHintText.fontSize = 14;
            bookshelfDesignHintText.color = new Color32(124, 111, 101, 255);
            defaultBookshelfDesignButton = CreateButton(bookshelfDesignPanel.transform, "DefaultBookshelfDesignButton", "기본", new Vector2(24f, -116f), () => SetBookshelfDesignStyle(BookshelfThemeStyle.Default), new Vector2(128f, 38f), new Vector2(0f, 1f));
            warmBookshelfDesignButton = CreateButton(bookshelfDesignPanel.transform, "WarmBookshelfDesignButton", "웜 우드", new Vector2(176f, -116f), () => SetBookshelfDesignStyle(BookshelfThemeStyle.Warm), new Vector2(128f, 38f), new Vector2(0f, 1f));
            darkBookshelfDesignButton = CreateButton(bookshelfDesignPanel.transform, "DarkBookshelfDesignButton", "다크 우드", new Vector2(328f, -116f), () => SetBookshelfDesignStyle(BookshelfThemeStyle.Dark), new Vector2(128f, 38f), new Vector2(0f, 1f));
            closeBookshelfDesignButton = CreateButton(bookshelfDesignPanel.transform, "CloseBookshelfDesignButton", "닫기", new Vector2(24f, -166f), CloseBookshelfDesignPanel, new Vector2(432f, 34f), new Vector2(0f, 1f));

            bookAddPanel = CreatePanel(canvasObject.transform, "BookAddPanel", new Vector2(0f, -128f), new Vector2(560f, 498f), new Vector2(0.5f, 1f));
            StyleBookshelfIndexPanel(bookAddPanel.GetComponent<Image>());
            bookAddPanel.SetActive(false);
            CreateHudImage(bookAddPanel.transform, "BookAddAccent", new Vector2(24f, -24f), new Vector2(84f, 3f), new Vector2(0f, 1f), new Color32(87, 139, 106, 170), false);
            bookAddTitleText = CreateLabel(bookAddPanel.transform, "BookAddTitleText", new Vector2(24f, -36f), new Vector2(456f, 24f), TextAnchor.UpperLeft);
            bookAddTitleText.text = "책 추가";
            bookAddTitleText.fontSize = 17;
            bookAddTitleText.fontStyle = FontStyle.Bold;
            bookAddTitleText.color = new Color32(44, 66, 53, 255);
            bookAddHintText = CreateLabel(bookAddPanel.transform, "BookAddHintText", new Vector2(24f, -70f), new Vector2(512f, 34f), TextAnchor.UpperLeft);
            bookAddHintText.text = "책을 검색해 선택하고, 배치와 독서 상태를 정한 뒤 현재 책장에 추가합니다.";
            bookAddHintText.fontSize = 13;
            bookAddHintText.color = new Color32(99, 113, 103, 255);
            bookAddShelfSelectorLabelText = CreateLabel(bookAddPanel.transform, "BookAddShelfSelectorLabelText", new Vector2(24f, -106f), new Vector2(512f, 18f), TextAnchor.UpperLeft);
            bookAddShelfSelectorLabelText.text = "추가할 책장";
            bookAddShelfSelectorLabelText.fontSize = 12;
            bookAddShelfSelectorLabelText.color = new Color32(99, 113, 103, 255);
            for (var i = 0; i < bookAddShelfSelectorButtons.Length; i++)
            {
                var shelfIndex = i;
                bookAddShelfSelectorButtons[i] = CreateButton(bookAddPanel.transform, $"BookAddShelfSelectorButton_{i}", string.Empty, new Vector2(24f + i * 96f, -132f), () => SelectBookAddTargetBookshelf(shelfIndex), new Vector2(88f, 30f), new Vector2(0f, 1f));
            }
            bookAddSearchModeButton = CreateButton(bookAddPanel.transform, "BookAddSearchModeButton", "검색", new Vector2(24f, -108f), () => SetBookAddBrowseMode(BookAddBrowseMode.Search), new Vector2(92f, 30f), new Vector2(0f, 1f));
            bookAddRecommendedModeButton = CreateButton(bookAddPanel.transform, "BookAddRecommendedModeButton", "추천", new Vector2(124f, -108f), () => SetBookAddBrowseMode(BookAddBrowseMode.Recommended), new Vector2(92f, 30f), new Vector2(0f, 1f));
            bookAddRankingModeButton = CreateButton(bookAddPanel.transform, "BookAddRankingModeButton", "랭킹", new Vector2(224f, -108f), () => SetBookAddBrowseMode(BookAddBrowseMode.Ranking), new Vector2(92f, 30f), new Vector2(0f, 1f));
            bookAddSearchInputField = CreateHudInputField(bookAddPanel.transform, "BookAddSearchInputField", new Vector2(24f, -148f), new Vector2(512f, 36f), "제목 또는 저자 검색");
            bookAddSearchInputField.characterLimit = 40;
            bookAddSearchInputField.onValueChanged.AddListener(HandleBookAddSearchChanged);
            closeBookAddButton = CreateButton(bookAddPanel.transform, "CloseBookAddButton", "×", new Vector2(508f, -28f), CloseBookAddPanel, new Vector2(28f, 32f), new Vector2(0f, 1f));
            var addRowsViewport = CreateHudScrollViewport(bookAddPanel.transform, "BookAddRowsViewport", new Vector2(24f, -194f), new Vector2(500f, BookAddResultViewportHeight), new Vector2(0f, 1f));
            bookAddScrollRect = addRowsViewport.GetComponent<ScrollRect>();
            bookAddRowsContent = CreateHudScrollContent(addRowsViewport.transform, "BookAddRowsContent", new Vector2(500f, BookAddMaxResultCount * BookAddResultRowHeight));
            bookAddScrollbar = CreateHudVerticalScrollbar(bookAddPanel.transform, "BookAddScrollbar", new Vector2(532f, -194f), new Vector2(8f, BookAddResultViewportHeight), new Vector2(0f, 1f));
            StyleHudScrollbar(bookAddScrollbar, new Color32(87, 139, 106, 220), new Color32(238, 231, 221, 160));
            if (bookAddScrollRect != null)
            {
                bookAddScrollRect.viewport = addRowsViewport.GetComponent<RectTransform>();
                bookAddScrollRect.content = bookAddRowsContent;
                bookAddScrollRect.verticalScrollbar = bookAddScrollbar;
                bookAddScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            }

            for (var i = 0; i < BookAddMaxResultCount; i++)
            {
                var resultSlot = i;
                bookAddResultCatalogIndices[i] = -1;
                bookAddResultButtons[i] = CreateButton(bookAddRowsContent, $"BookAddResultButton_{i}", string.Empty, new Vector2(0f, -i * BookAddResultRowHeight), () => OpenBookAddConfirmPanel(resultSlot), new Vector2(500f, BookAddResultRowHeight - 10f), new Vector2(0f, 1f));
                bookAddResultTexts[i] = bookAddResultButtons[i].GetComponentInChildren<Text>();
                bookAddResultCoverImages[i] = CreateHudImage(bookAddResultButtons[i].transform, $"BookAddResultCover_{i}", new Vector2(14f, -12f), new Vector2(62f, BookAddResultRowHeight - 34f), new Vector2(0f, 1f), new Color32(238, 231, 221, 240));
                if (bookAddResultCoverImages[i] != null)
                {
                    bookAddResultCoverImages[i].raycastTarget = false;
                    bookAddResultCoverImages[i].preserveAspect = true;
                    bookAddResultCoverImages[i].transform.SetAsFirstSibling();
                }

                if (bookAddResultTexts[i] != null)
                {
                    bookAddResultTexts[i].fontSize = 12;
                    bookAddResultTexts[i].alignment = TextAnchor.MiddleLeft;
                    bookAddResultTexts[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                    bookAddResultTexts[i].verticalOverflow = VerticalWrapMode.Truncate;
                    var labelRect = bookAddResultTexts[i].rectTransform;
                    labelRect.offsetMin = new Vector2(92f, 8f);
                    labelRect.offsetMax = new Vector2(-18f, -8f);
                    labelRect.anchoredPosition = Vector2.zero;
                    labelRect.sizeDelta = Vector2.zero;
                }
            }

            bookAddStatusText = CreateLabel(bookAddPanel.transform, "BookAddStatusText", new Vector2(24f, -446f), new Vector2(512f, 20f), TextAnchor.UpperLeft);
            bookAddStatusText.fontSize = 13;
            bookAddStatusText.color = new Color32(99, 113, 103, 255);

            bookAddConfirmPanel = CreatePanel(canvasObject.transform, "BookAddConfirmPanel", new Vector2(0f, -132f), new Vector2(500f, 408f), new Vector2(0.5f, 1f));
            StyleBookshelfIndexPanel(bookAddConfirmPanel.GetComponent<Image>());
            bookAddConfirmPanel.SetActive(false);
            CreateHudImage(bookAddConfirmPanel.transform, "BookAddConfirmAccent", new Vector2(24f, -24f), new Vector2(84f, 3f), new Vector2(0f, 1f), new Color32(87, 139, 106, 170), false);
            bookAddConfirmTitleText = CreateLabel(bookAddConfirmPanel.transform, "BookAddConfirmTitleText", new Vector2(24f, -38f), new Vector2(452f, 40f), TextAnchor.UpperLeft);
            bookAddConfirmTitleText.fontSize = 20;
            bookAddConfirmTitleText.fontStyle = FontStyle.Bold;
            bookAddConfirmTitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookAddConfirmTitleText.color = new Color32(44, 66, 53, 255);
            bookAddConfirmMetaText = CreateLabel(bookAddConfirmPanel.transform, "BookAddConfirmMetaText", new Vector2(24f, -88f), new Vector2(452f, 24f), TextAnchor.UpperLeft);
            bookAddConfirmMetaText.fontSize = 14;
            bookAddConfirmMetaText.color = new Color32(99, 113, 103, 255);
            bookAddConfirmSummaryText = CreateLabel(bookAddConfirmPanel.transform, "BookAddConfirmSummaryText", new Vector2(24f, -124f), new Vector2(452f, 62f), TextAnchor.UpperLeft);
            bookAddConfirmSummaryText.fontSize = 13;
            bookAddConfirmSummaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookAddConfirmSummaryText.color = new Color32(84, 74, 66, 255);
            var readingStatusLabel = CreateLabel(bookAddConfirmPanel.transform, "BookAddConfirmReadingStatusLabel", new Vector2(24f, -196f), new Vector2(452f, 18f), TextAnchor.UpperLeft);
            readingStatusLabel.text = "독서 상태";
            readingStatusLabel.fontSize = 12;
            readingStatusLabel.color = new Color32(99, 113, 103, 255);
            bookAddConfirmUnreadButton = CreateButton(bookAddConfirmPanel.transform, "BookAddConfirmUnreadButton", "안 읽음", new Vector2(24f, -220f), () => SetPendingBookAddReadingStatus(App.Domain.ReadingStatus.Unread), new Vector2(104f, 34f), new Vector2(0f, 1f));
            bookAddConfirmReadingButton = CreateButton(bookAddConfirmPanel.transform, "BookAddConfirmReadingButton", "읽는 중", new Vector2(138f, -220f), () => SetPendingBookAddReadingStatus(App.Domain.ReadingStatus.Reading), new Vector2(104f, 34f), new Vector2(0f, 1f));
            bookAddConfirmReadButton = CreateButton(bookAddConfirmPanel.transform, "BookAddConfirmReadButton", "읽음", new Vector2(252f, -220f), () => SetPendingBookAddReadingStatus(App.Domain.ReadingStatus.Read), new Vector2(104f, 34f), new Vector2(0f, 1f));
            var placementLabel = CreateLabel(bookAddConfirmPanel.transform, "BookAddConfirmPlacementLabel", new Vector2(24f, -270f), new Vector2(452f, 18f), TextAnchor.UpperLeft);
            placementLabel.text = "책 배치";
            placementLabel.fontSize = 12;
            placementLabel.color = new Color32(99, 113, 103, 255);
            bookAddConfirmSpineButton = CreateButton(bookAddConfirmPanel.transform, "BookAddConfirmSpineButton", "측면", new Vector2(24f, -294f), () => SetPendingBookAddOrientation(BookOrientation.Spine), new Vector2(92f, 34f), new Vector2(0f, 1f));
            bookAddConfirmAngledButton = CreateButton(bookAddConfirmPanel.transform, "BookAddConfirmAngledButton", "45°", new Vector2(126f, -294f), () => SetPendingBookAddOrientation(BookOrientation.Angled45), new Vector2(92f, 34f), new Vector2(0f, 1f));
            bookAddConfirmFrontButton = CreateButton(bookAddConfirmPanel.transform, "BookAddConfirmFrontButton", "정면", new Vector2(228f, -294f), () => SetPendingBookAddOrientation(BookOrientation.Front), new Vector2(92f, 34f), new Vector2(0f, 1f));
            confirmBookAddButton = CreateButton(bookAddConfirmPanel.transform, "ConfirmBookAddButton", "추가", new Vector2(304f, -350f), ConfirmAddCatalogBookToShelf, new Vector2(92f, 34f), new Vector2(0f, 1f));
            cancelBookAddConfirmButton = CreateButton(bookAddConfirmPanel.transform, "CancelBookAddConfirmButton", "취소", new Vector2(404f, -350f), CloseBookAddConfirmPanel, new Vector2(72f, 34f), new Vector2(0f, 1f));

            bookSearchPanel = CreatePanel(canvasObject.transform, "BookSearchPanel", new Vector2(24f, -96f), new Vector2(360f, 438f), new Vector2(0f, 1f));
            StyleBookDetailPanel(bookSearchPanel.GetComponent<Image>());
            bookSearchPanel.SetActive(false);
            CreateHudImage(bookSearchPanel.transform, "BookSearchAccent", new Vector2(20f, -22f), new Vector2(72f, 3f), new Vector2(0f, 1f), new Color32(73, 117, 147, 170), false);
            bookSearchTitleText = CreateLabel(bookSearchPanel.transform, "BookSearchTitleText", new Vector2(20f, -36f), new Vector2(286f, 24f), TextAnchor.UpperLeft);
            bookSearchTitleText.text = "책 찾기";
            bookSearchTitleText.fontSize = 18;
            bookSearchTitleText.fontStyle = FontStyle.Bold;
            bookSearchTitleText.color = new Color32(45, 50, 58, 255);
            closeBookSearchButton = CreateButton(bookSearchPanel.transform, "CloseBookSearchButton", "×", new Vector2(312f, -28f), CloseBookSearchPanel, new Vector2(28f, 32f), new Vector2(0f, 1f));
            bookSearchInputField = CreateHudInputField(bookSearchPanel.transform, "BookSearchInputField", new Vector2(20f, -76f), new Vector2(320f, 36f), "제목 또는 저자");
            bookSearchInputField.characterLimit = 40;
            bookSearchInputField.onValueChanged.AddListener(HandleBookSearchChanged);
            bookSearchShelfSelectorLabelText = CreateLabel(bookSearchPanel.transform, "BookSearchShelfSelectorLabelText", new Vector2(20f, -120f), new Vector2(320f, 18f), TextAnchor.UpperLeft);
            bookSearchShelfSelectorLabelText.text = "검색할 책장";
            bookSearchShelfSelectorLabelText.fontSize = 12;
            bookSearchShelfSelectorLabelText.color = new Color32(99, 113, 103, 255);
            for (var i = 0; i < bookSearchShelfSelectorButtons.Length; i++)
            {
                var shelfIndex = i;
                bookSearchShelfSelectorButtons[i] = CreateButton(bookSearchPanel.transform, $"BookSearchShelfSelectorButton_{i}", string.Empty, new Vector2(20f + i * 76f, -146f), () => SelectBookSearchTargetBookshelf(shelfIndex), new Vector2(70f, 30f), new Vector2(0f, 1f));
            }
            bookSearchAllFilterButton = CreateButton(bookSearchPanel.transform, "BookSearchAllFilterButton", "전체", new Vector2(20f, -124f), () => SetBookSearchFilter(BookSearchFilter.All), new Vector2(68f, 30f), new Vector2(0f, 1f));
            bookSearchReadingFilterButton = CreateButton(bookSearchPanel.transform, "BookSearchReadingFilterButton", "읽는 중", new Vector2(94f, -124f), () => SetBookSearchFilter(BookSearchFilter.Reading), new Vector2(78f, 30f), new Vector2(0f, 1f));
            bookSearchReadFilterButton = CreateButton(bookSearchPanel.transform, "BookSearchReadFilterButton", "완독", new Vector2(178f, -124f), () => SetBookSearchFilter(BookSearchFilter.Read), new Vector2(68f, 30f), new Vector2(0f, 1f));
            bookSearchLikedFilterButton = CreateButton(bookSearchPanel.transform, "BookSearchLikedFilterButton", "좋아요", new Vector2(252f, -124f), () => SetBookSearchFilter(BookSearchFilter.Liked), new Vector2(88f, 30f), new Vector2(0f, 1f));
            var searchRowsViewport = CreateHudScrollViewport(bookSearchPanel.transform, "BookSearchRowsViewport", new Vector2(20f, -166f), new Vector2(306f, BookSearchResultViewportHeight), new Vector2(0f, 1f));
            bookSearchScrollRect = searchRowsViewport.GetComponent<ScrollRect>();
            bookSearchRowsContent = CreateHudScrollContent(searchRowsViewport.transform, "BookSearchRowsContent", new Vector2(306f, BookSearchMaxResultCount * BookSearchResultRowHeight));
            bookSearchScrollbar = CreateHudVerticalScrollbar(bookSearchPanel.transform, "BookSearchScrollbar", new Vector2(332f, -166f), new Vector2(8f, BookSearchResultViewportHeight), new Vector2(0f, 1f));
            if (bookSearchScrollRect != null)
            {
                bookSearchScrollRect.viewport = searchRowsViewport.GetComponent<RectTransform>();
                bookSearchScrollRect.content = bookSearchRowsContent;
                bookSearchScrollRect.verticalScrollbar = bookSearchScrollbar;
                bookSearchScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            }

            for (var i = 0; i < BookSearchMaxResultCount; i++)
            {
                var resultSlot = i;
                bookSearchResultButtons[i] = CreateButton(bookSearchRowsContent, $"BookSearchResultButton_{i}", string.Empty, new Vector2(0f, -i * BookSearchResultRowHeight), () => OpenBookSearchResult(resultSlot), new Vector2(306f, 38f), new Vector2(0f, 1f));
                bookSearchResultTexts[i] = bookSearchResultButtons[i].GetComponentInChildren<Text>();
                if (bookSearchResultTexts[i] != null)
                {
                    bookSearchResultTexts[i].fontSize = 12;
                    bookSearchResultTexts[i].alignment = TextAnchor.MiddleLeft;
                    bookSearchResultTexts[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                    bookSearchResultTexts[i].verticalOverflow = VerticalWrapMode.Truncate;
                    bookSearchResultTexts[i].rectTransform.offsetMin = new Vector2(14f, 1f);
                    bookSearchResultTexts[i].rectTransform.offsetMax = new Vector2(-12f, -1f);
                }
            }

            bookSearchStatusText = CreateLabel(bookSearchPanel.transform, "BookSearchStatusText", new Vector2(20f, -410f), new Vector2(320f, 18f), TextAnchor.UpperLeft);
            bookSearchStatusText.fontSize = 12;
            bookSearchStatusText.color = new Color32(99, 113, 103, 255);

            bookshelfManagePanel = CreatePanel(canvasObject.transform, "BookshelfManagePanel", new Vector2(0f, -128f), new Vector2(560f, 456f), new Vector2(0.5f, 1f));
            StyleBookshelfIndexPanel(bookshelfManagePanel.GetComponent<Image>());
            bookshelfManagePanel.SetActive(false);
            CreateHudImage(bookshelfManagePanel.transform, "BookshelfManageAccent", new Vector2(24f, -24f), new Vector2(84f, 3f), new Vector2(0f, 1f), new Color32(197, 128, 67, 170), false);
            bookshelfManageTitleText = CreateLabel(bookshelfManagePanel.transform, "BookshelfManageTitleText", new Vector2(24f, -38f), new Vector2(512f, 24f), TextAnchor.UpperLeft);
            bookshelfManageTitleText.text = "책장 관리";
            bookshelfManageTitleText.fontSize = 18;
            bookshelfManageTitleText.fontStyle = FontStyle.Bold;
            bookshelfManageTitleText.color = new Color32(63, 47, 38, 255);
            CreateHudImage(bookshelfManagePanel.transform, "BookshelfManageSummaryCard", new Vector2(24f, -66f), new Vector2(512f, 42f), new Vector2(0f, 1f), new Color32(255, 248, 236, 205));
            bookshelfManageSummaryText = CreateLabel(bookshelfManagePanel.transform, "BookshelfManageSummaryText", new Vector2(40f, -76f), new Vector2(480f, 24f), TextAnchor.UpperLeft);
            bookshelfManageSummaryText.fontSize = 13;
            bookshelfManageSummaryText.fontStyle = FontStyle.Bold;
            bookshelfManageSummaryText.color = new Color32(84, 74, 66, 255);
            var listLabel = CreateLabel(bookshelfManagePanel.transform, "BookshelfManageListLabel", new Vector2(24f, -114f), new Vector2(220f, 18f), TextAnchor.UpperLeft);
            listLabel.text = "책장 목록";
            listLabel.fontSize = 12;
            listLabel.color = new Color32(124, 111, 101, 255);
            var manageRowsViewport = CreateHudScrollViewport(bookshelfManagePanel.transform, "BookshelfManageRowsViewport", new Vector2(24f, -136f), new Vector2(500f, ShelfManageViewportHeight), new Vector2(0f, 1f));
            bookshelfManageScrollRect = manageRowsViewport.GetComponent<ScrollRect>();
            bookshelfManageRowsContent = CreateHudScrollContent(manageRowsViewport.transform, "BookshelfManageRowsContent", new Vector2(500f, ShelfManageMaxRowCount * ShelfManageRowHeight));
            bookshelfManageScrollbar = CreateHudVerticalScrollbar(bookshelfManagePanel.transform, "BookshelfManageScrollbar", new Vector2(532f, -136f), new Vector2(8f, ShelfManageViewportHeight), new Vector2(0f, 1f));
            StyleHudScrollbar(bookshelfManageScrollbar, new Color32(197, 128, 67, 220), new Color32(238, 231, 221, 160));
            if (bookshelfManageScrollRect != null)
            {
                bookshelfManageScrollRect.viewport = manageRowsViewport.GetComponent<RectTransform>();
                bookshelfManageScrollRect.content = bookshelfManageRowsContent;
                bookshelfManageScrollRect.verticalScrollbar = bookshelfManageScrollbar;
                bookshelfManageScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            }

            for (var i = 0; i < ShelfManageMaxRowCount; i++)
            {
                var rowIndex = i;
                bookshelfManageRowButtons[i] = CreateButton(bookshelfManageRowsContent, $"BookshelfManageRowButton_{i}", string.Empty, new Vector2(0f, -i * ShelfManageRowHeight), () => SelectManagedBookshelf(rowIndex), new Vector2(500f, 38f), new Vector2(0f, 1f));
                bookshelfManageDragHandles[i] = CreateBookshelfManageDragHandle(bookshelfManageRowButtons[i].transform, $"BookshelfManageDragHandle_{i}");
                bookshelfManageDragHandles[i].Configure(this, rowIndex);
                bookshelfManageDragHandleImages[i] = bookshelfManageDragHandles[i].GetComponent<Image>();
                bookshelfManageRowTexts[i] = bookshelfManageRowButtons[i].GetComponentInChildren<Text>();
                if (bookshelfManageRowTexts[i] != null)
                {
                    bookshelfManageRowTexts[i].fontSize = 13;
                    bookshelfManageRowTexts[i].alignment = TextAnchor.MiddleLeft;
                    bookshelfManageRowTexts[i].rectTransform.offsetMin = new Vector2(58f, 1f);
                    bookshelfManageRowTexts[i].rectTransform.offsetMax = new Vector2(-12f, -1f);
                }
            }

            var sortLabel = CreateLabel(bookshelfManagePanel.transform, "BookshelfManageSortLabel", new Vector2(24f, -302f), new Vector2(220f, 18f), TextAnchor.UpperLeft);
            sortLabel.text = "현재 책장 정렬";
            sortLabel.fontSize = 12;
            sortLabel.color = new Color32(124, 111, 101, 255);
            shelfManageSortTitleButton = CreateButton(bookshelfManagePanel.transform, "ShelfManageSortTitleButton", "제목순", new Vector2(24f, -324f), () => SortActiveBookshelfBooks(ShelfBookSortMode.Title), new Vector2(104f, 32f), new Vector2(0f, 1f));
            shelfManageSortAuthorButton = CreateButton(bookshelfManagePanel.transform, "ShelfManageSortAuthorButton", "저자순", new Vector2(136f, -324f), () => SortActiveBookshelfBooks(ShelfBookSortMode.Author), new Vector2(104f, 32f), new Vector2(0f, 1f));
            shelfManageSortStatusButton = CreateButton(bookshelfManagePanel.transform, "ShelfManageSortStatusButton", "상태순", new Vector2(248f, -324f), () => SortActiveBookshelfBooks(ShelfBookSortMode.ReadingStatus), new Vector2(104f, 32f), new Vector2(0f, 1f));
            shelfManageSortLikedButton = CreateButton(bookshelfManagePanel.transform, "ShelfManageSortLikedButton", "좋아요", new Vector2(360f, -324f), () => SortActiveBookshelfBooks(ShelfBookSortMode.Liked), new Vector2(104f, 32f), new Vector2(0f, 1f));
            shelfManagePreviousButton = CreateButton(bookshelfManagePanel.transform, "ShelfManagePreviousButton", "이전 책장", new Vector2(24f, -370f), ShowPreviousManagedBookshelf, new Vector2(104f, 34f), new Vector2(0f, 1f));
            shelfManageNextButton = CreateButton(bookshelfManagePanel.transform, "ShelfManageNextButton", "다음 책장", new Vector2(136f, -370f), ShowNextManagedBookshelf, new Vector2(104f, 34f), new Vector2(0f, 1f));
            shelfManageVisibilityButton = CreateButton(bookshelfManagePanel.transform, "ShelfManageVisibilityButton", "공개 범위", new Vector2(24f, -370f), CycleManagedBookshelfVisibility, new Vector2(144f, 34f), new Vector2(0f, 1f));
            shelfManageRenameButton = CreateButton(bookshelfManagePanel.transform, "ShelfManageRenameButton", "이름 변경", new Vector2(180f, -370f), OpenManagedBookshelfNameEditPanel, new Vector2(108f, 34f), new Vector2(0f, 1f));
            shelfManageAddButton = CreateButton(bookshelfManagePanel.transform, "ShelfManageAddButton", "추가", new Vector2(300f, -370f), AddBookshelfFromManagePanel, new Vector2(84f, 34f), new Vector2(0f, 1f));
            shelfManageDeleteButton = CreateButton(bookshelfManagePanel.transform, "ShelfManageDeleteButton", "삭제", new Vector2(396f, -370f), DeleteActiveBookshelf, new Vector2(84f, 34f), new Vector2(0f, 1f));
            shelfManageCloseButton = CreateButton(bookshelfManagePanel.transform, "ShelfManageCloseButton", "닫기", new Vector2(288f, -370f), CloseBookshelfManagePanel, new Vector2(96f, 34f), new Vector2(0f, 1f));
            bookshelfManageStatusText = CreateLabel(bookshelfManagePanel.transform, "BookshelfManageStatusText", new Vector2(24f, -414f), new Vector2(512f, 28f), TextAnchor.UpperLeft);
            bookshelfManageStatusText.fontSize = 12;
            bookshelfManageStatusText.color = new Color32(124, 111, 101, 255);

            bookshelfUnsavedConfirmPanel = CreatePanel(canvasObject.transform, "BookshelfUnsavedConfirmPanel", new Vector2(0f, -166f), new Vector2(452f, 166f), new Vector2(0.5f, 1f));
            StyleBookshelfIndexPanel(bookshelfUnsavedConfirmPanel.GetComponent<Image>());
            bookshelfUnsavedConfirmPanel.SetActive(false);
            CreateHudImage(bookshelfUnsavedConfirmPanel.transform, "BookshelfUnsavedConfirmAccent", new Vector2(24f, -24f), new Vector2(84f, 3f), new Vector2(0f, 1f), new Color32(197, 128, 67, 170), false);
            bookshelfUnsavedConfirmTitleText = CreateLabel(bookshelfUnsavedConfirmPanel.transform, "BookshelfUnsavedConfirmTitleText", new Vector2(24f, -38f), new Vector2(404f, 24f), TextAnchor.UpperLeft);
            bookshelfUnsavedConfirmTitleText.text = "수정된 사항이 있습니다.";
            bookshelfUnsavedConfirmTitleText.fontSize = 18;
            bookshelfUnsavedConfirmTitleText.fontStyle = FontStyle.Bold;
            bookshelfUnsavedConfirmTitleText.color = new Color32(63, 47, 38, 255);
            bookshelfUnsavedConfirmMessageText = CreateLabel(bookshelfUnsavedConfirmPanel.transform, "BookshelfUnsavedConfirmMessageText", new Vector2(24f, -74f), new Vector2(404f, 42f), TextAnchor.UpperLeft);
            bookshelfUnsavedConfirmMessageText.text = "저장 후 이동하시겠습니까?";
            bookshelfUnsavedConfirmMessageText.fontSize = 15;
            bookshelfUnsavedConfirmMessageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookshelfUnsavedConfirmMessageText.color = new Color32(84, 74, 66, 255);
            confirmBookshelfUnsavedSaveButton = CreateButton(bookshelfUnsavedConfirmPanel.transform, "ConfirmBookshelfUnsavedSaveButton", "저장 후 이동", new Vector2(188f, -118f), ConfirmBookshelfUnsavedNavigation, new Vector2(122f, 34f), new Vector2(0f, 1f));
            cancelBookshelfUnsavedSaveButton = CreateButton(bookshelfUnsavedConfirmPanel.transform, "CancelBookshelfUnsavedSaveButton", "취소", new Vector2(322f, -118f), CancelBookshelfUnsavedNavigation, new Vector2(82f, 34f), new Vector2(0f, 1f));

            deleteResultBackdrop = CreateModalBackdrop(canvasObject.transform, "DeleteResultBackdrop", new Color(0.12f, 0.08f, 0.05f, 0.42f));
            deleteResultBackdrop.SetActive(false);
            deleteResultDialogPanel = CreatePanel(canvasObject.transform, "DeleteResultDialogPanel", Vector2.zero, new Vector2(452f, 184f), new Vector2(0.5f, 0.5f));
            StyleBookshelfIndexPanel(deleteResultDialogPanel.GetComponent<Image>());
            deleteResultDialogPanel.SetActive(false);
            deleteResultTitleText = CreateLabel(deleteResultDialogPanel.transform, "DeleteResultTitleText", new Vector2(28f, -34f), new Vector2(396f, 28f), TextAnchor.UpperLeft);
            deleteResultTitleText.fontSize = 19;
            deleteResultTitleText.fontStyle = FontStyle.Bold;
            deleteResultTitleText.color = new Color32(63, 47, 38, 255);
            deleteResultMessageText = CreateLabel(deleteResultDialogPanel.transform, "DeleteResultMessageText", new Vector2(28f, -76f), new Vector2(396f, 54f), TextAnchor.UpperLeft);
            deleteResultMessageText.fontSize = 15;
            deleteResultMessageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            deleteResultMessageText.color = new Color32(84, 74, 66, 255);
            retryDeleteResultDialogButton = CreateButton(deleteResultDialogPanel.transform, "RetryDeleteResultDialogButton", "재시도", new Vector2(194f, -130f), RunDeleteResultRetryAction, new Vector2(106f, 36f), new Vector2(0f, 1f));
            closeDeleteResultDialogButton = CreateButton(deleteResultDialogPanel.transform, "CloseDeleteResultDialogButton", "확인", new Vector2(318f, -130f), CloseDeleteResultDialog, new Vector2(106f, 36f), new Vector2(0f, 1f));

            bookshelfStatusPanel = CreatePanel(canvasObject.transform, "BookshelfStatusPanel", new Vector2(24f, 24f), new Vector2(320f, 128f), new Vector2(0f, 0f));
            StyleBookDetailPanel(bookshelfStatusPanel.GetComponent<Image>());
            bookshelfStatusPanel.SetActive(false);
            bookshelfStatusTitleText = CreateLabel(bookshelfStatusPanel.transform, "BookshelfStatusTitleText", new Vector2(18f, -20f), new Vector2(284f, 22f), TextAnchor.UpperLeft);
            bookshelfStatusTitleText.fontSize = 17;
            bookshelfStatusTitleText.fontStyle = FontStyle.Bold;
            bookshelfStatusTitleText.color = new Color32(49, 42, 36, 255);
            bookshelfStatusMessageText = CreateLabel(bookshelfStatusPanel.transform, "BookshelfStatusMessageText", new Vector2(18f, -50f), new Vector2(284f, 36f), TextAnchor.UpperLeft);
            bookshelfStatusMessageText.fontSize = 13;
            bookshelfStatusMessageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookshelfStatusMessageText.color = new Color32(84, 74, 66, 255);
            bookshelfStatusAddButton = CreateButton(bookshelfStatusPanel.transform, "BookshelfStatusAddButton", "책 추가", new Vector2(18f, -92f), OpenBookAddFromStatusPanel, new Vector2(96f, 28f), new Vector2(0f, 1f));

            bookDetailPanelController = new BookDetailPanelController(
                canvasObject.transform,
                new BookDetailPanelCallbacks(
                    OpenBookReportWritePanel,
                    OpenBookReportReadPanel,
                    OpenBookPlacementEditor,
                    ToggleReadState,
                    ToggleLikeState,
                    CycleOwnerRating,
                    ToggleVisibilityState,
                    CloseBookDetailPanel,
                    DeleteActiveBookFromDetail));
            bookDetailPanel = bookDetailPanelController.Panel;

            bookFocusToolbarPanel = CreatePanel(canvasObject.transform, "BookFocusToolbarPanel", new Vector2(0f, 24f), new Vector2(470f, 58f), new Vector2(0.5f, 0f));
            StyleBookshelfIndexPanel(bookFocusToolbarPanel.GetComponent<Image>());
            bookFocusToolbarPanel.SetActive(false);
            bookFocusDetailButton = CreateButton(bookFocusToolbarPanel.transform, "BookFocusDetailButton", "상태", new Vector2(174f, 0f), ToggleReadState, new Vector2(74f, 36f), new Vector2(0f, 0.5f));
            bookFocusRatingButton = CreateButton(bookFocusToolbarPanel.transform, "BookFocusRatingButton", "리뷰 작성", new Vector2(254f, 0f), OpenBookReviewWritePanel, new Vector2(74f, 36f), new Vector2(0f, 0.5f));
            bookFocusReportButton = CreateButton(bookFocusToolbarPanel.transform, "BookFocusReportButton", "좋아요", new Vector2(334f, 0f), ToggleLikeState, new Vector2(86f, 36f), new Vector2(0f, 0.5f));
            bookFocusWriteReportButton = CreateButton(canvasObject.transform, "BookFocusWriteReportButton", "독후감 작성", new Vector2(0f, 128f), OpenActiveViewedBookReport, new Vector2(160f, 44f), new Vector2(0.5f, 0f));
            bookFocusWriteReportButton.gameObject.SetActive(false);
            bookFocusRatingText = CreateLabel(bookFocusToolbarPanel.transform, "BookFocusRatingText", new Vector2(0f, 22f), new Vector2(360f, 18f), TextAnchor.MiddleCenter);
            bookFocusRatingText.fontSize = 13;
            bookFocusRatingText.color = new Color32(84, 74, 66, 255);
            bookFocusRatingText.gameObject.SetActive(false);

            bookReviewWritePanel = CreatePanel(canvasObject.transform, "BookReviewWritePanel", new Vector2(-24f, -78f), new Vector2(430f, 390f), new Vector2(1f, 1f));
            StyleBookDetailPanel(bookReviewWritePanel.GetComponent<Image>());
            bookReviewWritePanel.SetActive(false);
            CreateHudImage(bookReviewWritePanel.transform, "BookReviewWriteAccent", new Vector2(20f, -22f), new Vector2(72f, 3f), new Vector2(0f, 1f), new Color32(197, 128, 67, 170), false);
            bookReviewWriteTitleText = CreateLabel(bookReviewWritePanel.transform, "BookReviewWriteTitleText", new Vector2(20f, -36f), new Vector2(390f, 24f), TextAnchor.UpperLeft);
            bookReviewWriteTitleText.text = "리뷰 작성";
            bookReviewWriteTitleText.fontSize = 18;
            bookReviewWriteTitleText.fontStyle = FontStyle.Bold;
            bookReviewWriteTitleText.color = new Color32(45, 50, 58, 255);
            bookReviewWriteBookText = CreateLabel(bookReviewWritePanel.transform, "BookReviewWriteBookText", new Vector2(20f, -68f), new Vector2(390f, 28f), TextAnchor.UpperLeft);
            bookReviewWriteBookText.fontSize = 13;
            bookReviewWriteBookText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookReviewWriteBookText.color = new Color32(84, 74, 66, 255);
            bookReviewRatingText = CreateLabel(bookReviewWritePanel.transform, "BookReviewRatingText", new Vector2(20f, -112f), new Vector2(390f, 22f), TextAnchor.UpperLeft);
            bookReviewRatingText.fontSize = 14;
            bookReviewRatingText.fontStyle = FontStyle.Bold;
            bookReviewRatingText.color = new Color32(72, 58, 47, 255);
            for (var i = 0; i < bookReviewRatingButtons.Length; i++)
            {
                var rating = GetRatingChoiceValue(i);
                var row = i / 5;
                var column = i % 5;
                bookReviewRatingButtons[i] = CreateButton(
                    bookReviewWritePanel.transform,
                    $"BookReviewRatingButton_{i + 1}",
                    $"{FormatRatingValue(rating)}★",
                    new Vector2(20f + column * 78f, -146f - row * 42f),
                    () => SetStagedBookReviewRating(rating),
                    new Vector2(70f, 34f),
                    new Vector2(0f, 1f));
            }
            bookReviewContentInputField = CreateHudInputField(bookReviewWritePanel.transform, "BookReviewContentInputField", new Vector2(20f, -238f), new Vector2(390f, 88f), "리뷰 내용을 입력하세요");
            bookReviewContentInputField.characterLimit = 1000;
            bookReviewContentInputField.lineType = InputField.LineType.MultiLineNewline;
            ApplyHudInputTextPadding(bookReviewContentInputField);
            if (bookReviewContentInputField.textComponent != null)
            {
                bookReviewContentInputField.textComponent.alignment = TextAnchor.UpperLeft;
            }
            bookReviewContentInputField.onValueChanged.AddListener(_ =>
            {
                if (!isBookReviewSaving)
                {
                    SetBookReviewStatus(string.Empty);
                    RefreshBookReviewWritePanel();
                }
            });
            bookReviewStatusText = CreateLabel(bookReviewWritePanel.transform, "BookReviewStatusText", new Vector2(20f, -338f), new Vector2(214f, 36f), TextAnchor.UpperLeft);
            bookReviewStatusText.fontSize = 12;
            bookReviewStatusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookReviewStatusText.color = new Color32(124, 111, 101, 255);
            saveBookReviewButton = CreateButton(bookReviewWritePanel.transform, "SaveBookReviewButton", "저장", new Vector2(250f, -342f), SaveBookReview, new Vector2(76f, 34f), new Vector2(0f, 1f));
            cancelBookReviewButton = CreateButton(bookReviewWritePanel.transform, "CancelBookReviewButton", "취소", new Vector2(334f, -342f), CloseBookReviewWritePanel, new Vector2(76f, 34f), new Vector2(0f, 1f));

            bookReportWritePanel = CreatePanel(canvasObject.transform, "BookReportWritePanel", new Vector2(-24f, -78f), new Vector2(430f, 474f), new Vector2(1f, 1f));
            StyleBookDetailPanel(bookReportWritePanel.GetComponent<Image>());
            bookReportWritePanel.SetActive(false);
            CreateHudImage(bookReportWritePanel.transform, "BookReportWriteAccent", new Vector2(20f, -22f), new Vector2(72f, 3f), new Vector2(0f, 1f), new Color32(88, 133, 117, 170), false);
            bookReportWriteTitleText = CreateLabel(bookReportWritePanel.transform, "BookReportWriteTitleText", new Vector2(20f, -36f), new Vector2(390f, 24f), TextAnchor.UpperLeft);
            bookReportWriteTitleText.text = "독후감 작성";
            bookReportWriteTitleText.fontSize = 18;
            bookReportWriteTitleText.fontStyle = FontStyle.Bold;
            bookReportWriteTitleText.color = new Color32(45, 50, 58, 255);
            bookReportWriteBookText = CreateLabel(bookReportWritePanel.transform, "BookReportWriteBookText", new Vector2(20f, -68f), new Vector2(390f, 28f), TextAnchor.UpperLeft);
            bookReportWriteBookText.fontSize = 13;
            bookReportWriteBookText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookReportWriteBookText.color = new Color32(84, 74, 66, 255);
            bookReportTitleInputField = CreateHudInputField(bookReportWritePanel.transform, "BookReportTitleInputField", new Vector2(20f, -112f), new Vector2(390f, 36f), "독후감 제목");
            bookReportTitleInputField.characterLimit = 40;
            bookReportBodyInputField = CreateHudInputField(bookReportWritePanel.transform, "BookReportBodyInputField", new Vector2(20f, -160f), new Vector2(390f, 180f), "내용을 입력하세요");
            bookReportBodyInputField.characterLimit = 1500;
            bookReportBodyInputField.lineType = InputField.LineType.MultiLineNewline;
            ApplyHudInputTextPadding(bookReportBodyInputField);
            if (bookReportBodyInputField.textComponent != null)
            {
                bookReportBodyInputField.textComponent.alignment = TextAnchor.UpperLeft;
            }

            bookReportPrivateButton = CreateButton(bookReportWritePanel.transform, "BookReportPrivateButton", "비공개", new Vector2(20f, -356f), () => SetStagedReportVisibility(App.Domain.VisibilityScope.Private), new Vector2(84f, 32f), new Vector2(0f, 1f));
            bookReportPublicButton = CreateButton(bookReportWritePanel.transform, "BookReportPublicButton", "전체공개", new Vector2(112f, -356f), () => SetStagedReportVisibility(App.Domain.VisibilityScope.Public), new Vector2(84f, 32f), new Vector2(0f, 1f));
            bookReportFriendsButton = CreateButton(bookReportWritePanel.transform, "BookReportFriendsButton", "친구 공개", new Vector2(204f, -356f), () => SetStagedReportVisibility(App.Domain.VisibilityScope.Followers), new Vector2(96f, 32f), new Vector2(0f, 1f));
            saveBookReportButton = CreateButton(bookReportWritePanel.transform, "SaveBookReportButton", "저장", new Vector2(250f, -420f), SaveBookReport, new Vector2(76f, 34f), new Vector2(0f, 1f));
            cancelBookReportButton = CreateButton(bookReportWritePanel.transform, "CancelBookReportButton", "취소", new Vector2(334f, -420f), CloseBookReportPanels, new Vector2(76f, 34f), new Vector2(0f, 1f));

            bookReportReadPanel = CreatePanel(canvasObject.transform, "BookReportReadPanel", new Vector2(-24f, -78f), new Vector2(430f, 436f), new Vector2(1f, 1f));
            StyleBookDetailPanel(bookReportReadPanel.GetComponent<Image>());
            bookReportReadPanel.SetActive(false);
            CreateHudImage(bookReportReadPanel.transform, "BookReportReadAccent", new Vector2(20f, -22f), new Vector2(72f, 3f), new Vector2(0f, 1f), new Color32(197, 128, 67, 170), false);
            bookReportReadTitleText = CreateLabel(bookReportReadPanel.transform, "BookReportReadTitleText", new Vector2(20f, -38f), new Vector2(390f, 44f), TextAnchor.UpperLeft);
            bookReportReadTitleText.fontSize = 20;
            bookReportReadTitleText.fontStyle = FontStyle.Bold;
            bookReportReadTitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookReportReadTitleText.color = new Color32(49, 42, 36, 255);
            bookReportReadMetaText = CreateLabel(bookReportReadPanel.transform, "BookReportReadMetaText", new Vector2(20f, -92f), new Vector2(390f, 26f), TextAnchor.UpperLeft);
            bookReportReadMetaText.fontSize = 13;
            bookReportReadMetaText.color = new Color32(124, 111, 101, 255);
            CreateHudImage(bookReportReadPanel.transform, "BookReportReadDivider", new Vector2(20f, -132f), new Vector2(390f, 1f), new Vector2(0f, 1f), new Color32(205, 186, 168, 126));
            bookReportReadBodyText = CreateLabel(bookReportReadPanel.transform, "BookReportReadBodyText", new Vector2(20f, -150f), new Vector2(390f, 190f), TextAnchor.UpperLeft);
            bookReportReadBodyText.fontSize = 14;
            bookReportReadBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookReportReadBodyText.verticalOverflow = VerticalWrapMode.Truncate;
            bookReportReadBodyText.lineSpacing = 1.16f;
            bookReportReadBodyText.color = new Color32(63, 47, 38, 255);
            bookReportCommentsTitleText = CreateLabel(bookReportReadPanel.transform, "BookReportCommentsTitleText", new Vector2(20f, -286f), new Vector2(390f, 22f), TextAnchor.UpperLeft);
            bookReportCommentsTitleText.fontSize = 13;
            bookReportCommentsTitleText.fontStyle = FontStyle.Bold;
            bookReportCommentsTitleText.color = new Color32(72, 58, 47, 255);
            bookReportCommentsText = CreateLabel(bookReportReadPanel.transform, "BookReportCommentsText", new Vector2(20f, -310f), new Vector2(390f, 72f), TextAnchor.UpperLeft);
            bookReportCommentsText.fontSize = 13;
            bookReportCommentsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookReportCommentsText.verticalOverflow = VerticalWrapMode.Truncate;
            bookReportCommentsText.lineSpacing = 1.08f;
            bookReportCommentsText.color = new Color32(83, 72, 63, 255);
            bookReportSocialText = CreateLabel(bookReportReadPanel.transform, "BookReportSocialText", new Vector2(20f, -318f), new Vector2(390f, 22f), TextAnchor.UpperLeft);
            bookReportSocialText.fontSize = 12;
            bookReportSocialText.color = new Color32(124, 111, 101, 255);
            bookReportCommentInputField = CreateHudInputField(bookReportReadPanel.transform, "BookReportCommentInputField", new Vector2(20f, -346f), new Vector2(280f, 32f), "댓글");
            bookReportCommentInputField.characterLimit = 140;
            bookReportLikeButton = CreateButton(bookReportReadPanel.transform, "BookReportLikeButton", "공감", new Vector2(306f, -346f), ToggleActiveReportLike, new Vector2(52f, 32f), new Vector2(0f, 1f));
            bookReportCommentButton = CreateButton(bookReportReadPanel.transform, "BookReportCommentButton", "등록", new Vector2(364f, -346f), SubmitActiveReportComment, new Vector2(46f, 32f), new Vector2(0f, 1f));
            editBookReportButton = CreateButton(bookReportReadPanel.transform, "EditBookReportButton", "수정", new Vector2(20f, -390f), OpenBookReportWritePanel, new Vector2(86f, 32f), new Vector2(0f, 1f));
            toggleReportVisibilityButton = CreateButton(bookReportReadPanel.transform, "ToggleReportVisibilityButton", "공개 범위", new Vector2(118f, -390f), ToggleReportVisibility, new Vector2(112f, 32f), new Vector2(0f, 1f));
            deleteBookReportButton = CreateButton(bookReportReadPanel.transform, "DeleteBookReportButton", "삭제", new Vector2(242f, -390f), DeleteActiveBookReport, new Vector2(68f, 32f), new Vector2(0f, 1f));
            closeBookReportReadButton = CreateButton(bookReportReadPanel.transform, "CloseBookReportReadButton", "닫기", new Vector2(322f, -390f), CloseBookReportPanels, new Vector2(88f, 32f), new Vector2(0f, 1f));

            openBookPlacementEditorButton = CreateButton(bookFocusToolbarPanel.transform, "OpenBookPlacementEditorButton", "배치 변경", new Vector2(0f, 0f), OpenBookPlacementEditor, new Vector2(136f, 44f), new Vector2(0.5f, 0.5f));
            openBookPlacementEditorButton.GetComponentInChildren<Text>().fontSize = 16;
            openBookPlacementEditorButton.gameObject.SetActive(false);

            bookPlacementEditorPanel = CreatePanel(canvasObject.transform, "BookPlacementEditorPanel", new Vector2(0f, 24f), new Vector2(454f, 58f), new Vector2(0.5f, 0f));
            StyleBookshelfIndexPanel(bookPlacementEditorPanel.GetComponent<Image>());
            bookPlacementEditorPanel.SetActive(false);
            placementSpineButton = CreateButton(bookPlacementEditorPanel.transform, "PlacementSpineButton", "측면", new Vector2(14f, 0f), () => PreviewBookPlacement(BookOrientation.Spine), new Vector2(78f, 36f), new Vector2(0f, 0.5f));
            placementAngledButton = CreateButton(bookPlacementEditorPanel.transform, "PlacementAngledButton", "45도", new Vector2(98f, 0f), () => PreviewBookPlacement(BookOrientation.Angled45), new Vector2(78f, 36f), new Vector2(0f, 0.5f));
            placementFrontButton = CreateButton(bookPlacementEditorPanel.transform, "PlacementFrontButton", "정면", new Vector2(182f, 0f), () => PreviewBookPlacement(BookOrientation.Front), new Vector2(78f, 36f), new Vector2(0f, 0.5f));
            cancelPlacementButton = CreateButton(bookPlacementEditorPanel.transform, "CancelPlacementButton", "취소", new Vector2(282f, 0f), CancelBookPlacementEdit, new Vector2(70f, 36f), new Vector2(0f, 0.5f));
            confirmPlacementButton = CreateButton(bookPlacementEditorPanel.transform, "ConfirmPlacementButton", "확인", new Vector2(358f, 0f), ConfirmBookPlacementEdit, new Vector2(82f, 36f), new Vector2(0f, 0.5f));

            bookEditPanel = CreatePanel(canvasObject.transform, "BookEditPanel", new Vector2(-24f, -78f), new Vector2(360f, 436f), new Vector2(1f, 1f));
            StyleBookDetailPanel(bookEditPanel.GetComponent<Image>());
            bookEditPanel.SetActive(false);
            CreateHudImage(bookEditPanel.transform, "BookEditAccent", new Vector2(20f, -22f), new Vector2(72f, 3f), new Vector2(0f, 1f), new Color32(197, 128, 67, 170), false);
            bookEditEyebrowText = CreateLabel(bookEditPanel.transform, "BookEditEyebrowText", new Vector2(20f, -30f), new Vector2(320f, 18f), TextAnchor.UpperLeft);
            bookEditEyebrowText.fontSize = 13;
            bookEditEyebrowText.fontStyle = FontStyle.Bold;
            bookEditEyebrowText.color = new Color32(151, 104, 64, 255);
            bookEditTitleText = CreateLabel(bookEditPanel.transform, "BookEditTitleText", new Vector2(20f, -54f), new Vector2(320f, 46f), TextAnchor.UpperLeft);
            bookEditTitleText.fontSize = 20;
            bookEditTitleText.fontStyle = FontStyle.Bold;
            bookEditTitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookEditTitleText.color = new Color32(49, 42, 36, 255);
            bookEditMetaText = CreateLabel(bookEditPanel.transform, "BookEditMetaText", new Vector2(20f, -112f), new Vector2(320f, 72f), TextAnchor.UpperLeft);
            bookEditMetaText.fontSize = 14;
            bookEditMetaText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookEditMetaText.color = new Color32(84, 74, 66, 255);
            bookEditMetaText.lineSpacing = 1.08f;
            CreateHudImage(bookEditPanel.transform, "BookEditDivider", new Vector2(20f, -204f), new Vector2(320f, 1f), new Vector2(0f, 1f), new Color32(205, 186, 168, 126));
            bookEditHintText = CreateLabel(bookEditPanel.transform, "BookEditHintText", new Vector2(20f, -220f), new Vector2(320f, 42f), TextAnchor.UpperLeft);
            bookEditHintText.fontSize = 13;
            bookEditHintText.color = new Color32(124, 111, 101, 255);
            bookEditHintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            deleteBookButton = CreateButton(bookEditPanel.transform, "DeleteBookButton", "책 삭제", new Vector2(20f, -336f), DeleteActiveBook, new Vector2(320f, 34f), new Vector2(0f, 1f));
            closeBookEditButton = CreateButton(bookEditPanel.transform, "CloseBookEditButton", "닫기", new Vector2(20f, -380f), CloseBookDetailPanel, new Vector2(320f, 32f), new Vector2(0f, 1f));

            sectionEditPanel = CreatePanel(canvasObject.transform, "SectionEditPanel", new Vector2(-24f, -78f), new Vector2(360f, 436f), new Vector2(1f, 1f));
            StyleBookDetailPanel(sectionEditPanel.GetComponent<Image>());
            sectionEditPanel.SetActive(false);
            CreateHudImage(sectionEditPanel.transform, "SectionEditAccent", new Vector2(20f, -22f), new Vector2(72f, 3f), new Vector2(0f, 1f), new Color32(197, 128, 67, 170), false);
            sectionEditEyebrowText = CreateLabel(sectionEditPanel.transform, "SectionEditEyebrowText", new Vector2(20f, -30f), new Vector2(320f, 18f), TextAnchor.UpperLeft);
            sectionEditEyebrowText.fontSize = 13;
            sectionEditEyebrowText.fontStyle = FontStyle.Bold;
            sectionEditEyebrowText.color = new Color32(151, 104, 64, 255);
            sectionEditTitleText = CreateLabel(sectionEditPanel.transform, "SectionEditTitleText", new Vector2(20f, -54f), new Vector2(320f, 44f), TextAnchor.UpperLeft);
            sectionEditTitleText.fontSize = 20;
            sectionEditTitleText.fontStyle = FontStyle.Bold;
            sectionEditTitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            sectionEditTitleText.color = new Color32(49, 42, 36, 255);
            sectionEditMetaText = CreateLabel(sectionEditPanel.transform, "SectionEditMetaText", new Vector2(20f, -112f), new Vector2(320f, 88f), TextAnchor.UpperLeft);
            sectionEditMetaText.fontSize = 14;
            sectionEditMetaText.horizontalOverflow = HorizontalWrapMode.Wrap;
            sectionEditMetaText.color = new Color32(84, 74, 66, 255);
            sectionEditMetaText.lineSpacing = 1.08f;
            CreateHudImage(sectionEditPanel.transform, "SectionEditDivider", new Vector2(20f, -214f), new Vector2(320f, 1f), new Vector2(0f, 1f), new Color32(205, 186, 168, 126));
            sectionEditHintText = CreateLabel(sectionEditPanel.transform, "SectionEditHintText", new Vector2(20f, -230f), new Vector2(320f, 44f), TextAnchor.UpperLeft);
            sectionEditHintText.fontSize = 13;
            sectionEditHintText.color = new Color32(124, 111, 101, 255);
            sectionEditHintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            alignLeftButton = CreateButton(sectionEditPanel.transform, "AlignLeftButton", "좌정렬", new Vector2(20f, -318f), () => SetActiveSectionAlignment(ShelfSectionAlignment.Left), new Vector2(100f, 34f), new Vector2(0f, 1f));
            alignCenterButton = CreateButton(sectionEditPanel.transform, "AlignCenterButton", "가운데", new Vector2(130f, -318f), () => SetActiveSectionAlignment(ShelfSectionAlignment.Center), new Vector2(100f, 34f), new Vector2(0f, 1f));
            alignRightButton = CreateButton(sectionEditPanel.transform, "AlignRightButton", "우정렬", new Vector2(240f, -318f), () => SetActiveSectionAlignment(ShelfSectionAlignment.Right), new Vector2(100f, 34f), new Vector2(0f, 1f));
            closeSectionEditButton = CreateButton(sectionEditPanel.transform, "CloseSectionEditButton", "닫기", new Vector2(20f, -380f), CloseBookDetailPanel, new Vector2(320f, 32f), new Vector2(0f, 1f));

            guestbookPanel = null;

            RefreshHud();
        }

        private Button CreateBottomNavButton(
            Transform parent,
            string name,
            string label,
            int slotIndex,
            UnityEngine.Events.UnityAction onClick)
        {
            var x = -222f + slotIndex * 148f;
            var button = CreateButton(parent, name, label, new Vector2(x, 0f), onClick, new Vector2(128f, 76f), new Vector2(0.5f, 0.5f));
            var text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.fontSize = 15;
                text.lineSpacing = 1.02f;
                text.alignment = TextAnchor.MiddleCenter;
            }

            return button;
        }

        private static BookshelfManageDragHandle CreateBookshelfManageDragHandle(Transform parent, string name)
        {
            var handleObject = new GameObject(name);
            handleObject.transform.SetParent(parent, false);

            var image = handleObject.AddComponent<Image>();
            ApplyRoundedSprite(image);
            image.color = new Color32(245, 237, 226, 228);
            image.raycastTarget = true;

            var rect = handleObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(10f, -4f);
            rect.sizeDelta = new Vector2(34f, 30f);
            rect.localScale = Vector3.one;

            for (var i = 0; i < 3; i++)
            {
                CreateHudImage(
                    handleObject.transform,
                    $"HamburgerLine_{i}",
                    new Vector2(8f, -8f - i * 7f),
                    new Vector2(18f, 2f),
                    new Vector2(0f, 1f),
                    new Color32(84, 74, 66, 230));
            }

            return handleObject.AddComponent<BookshelfManageDragHandle>();
        }

        private void ToggleHamburgerMenu()
        {
            isHamburgerMenuOpen = !isHamburgerMenuOpen;
            RefreshHud();
        }

        private void ToggleBookshelfEditMode()
        {
            if (IsBookshelfEditMode)
            {
                MarkBookshelfEditSaved();
            }

            SetBookshelfEditMode(!IsBookshelfEditMode);
        }

        private void ConfirmBookshelfUnsavedNavigation()
        {
            var nextAction = pendingBookshelfNavigationAfterSave;
            pendingBookshelfNavigationAfterSave = null;
            isBookshelfUnsavedConfirmPanelOpen = false;
            MarkBookshelfEditSaved();
            SetBookshelfEditMode(false);
            RefreshHud();
            nextAction?.Invoke();
        }

        private void CancelBookshelfUnsavedNavigation()
        {
            pendingBookshelfNavigationAfterSave = null;
            isBookshelfUnsavedConfirmPanelOpen = false;
            RefreshHud();
        }

        private void SetBookshelfOverlayMode(BookshelfOverlayMode mode)
        {
            isBookAddPlacementSelectionActive = false;
            bookshelfGenerator?.HideBookAddPlacementGuide();

            isBookshelfNameEditPanelOpen = mode == BookshelfOverlayMode.NameEdit;
            isBookshelfDeleteConfirmPanelOpen = mode == BookshelfOverlayMode.DeleteConfirm;
            isBookshelfDesignPanelOpen = mode == BookshelfOverlayMode.Design;
            isBookAddPanelOpen = mode == BookshelfOverlayMode.BookAdd;
            isBookAddConfirmPanelOpen = mode == BookshelfOverlayMode.BookAddConfirm;
            isBookshelfManagePanelOpen = mode == BookshelfOverlayMode.Manage;
            isBookshelfUnsavedConfirmPanelOpen = mode == BookshelfOverlayMode.UnsavedConfirm;

            if (mode != BookshelfOverlayMode.NameEdit)
            {
                bookshelfNameInputIndex = -1;
            }

            if (mode != BookshelfOverlayMode.DeleteConfirm)
            {
                pendingDeleteBookshelfIndex = -1;
            }

            if (mode != BookshelfOverlayMode.BookAddConfirm)
            {
                pendingBookAddCatalogIndex = -1;
                pendingBookAddCatalogItem = null;
            }

            if (mode != BookshelfOverlayMode.BookAdd && mode != BookshelfOverlayMode.BookAddConfirm)
            {
                bookAddFeedbackMessage = string.Empty;
            }

            if (mode != BookshelfOverlayMode.UnsavedConfirm)
            {
                pendingBookshelfNavigationAfterSave = null;
            }
        }

        private void FocusBookshelfHomeFromBottom()
        {
            if (CurrentFocusArea != ExperienceFocusArea.Bookshelf)
            {
                FocusBookshelf();
                return;
            }

            if (TryPromptUnsavedBookshelfChanges(FocusBookshelfHomeFromBottom))
            {
                return;
            }

            if (IsBookshelfEditMode)
            {
                SetBookshelfEditMode(false);
            }

            CloseOverlayPanels();
            CloseBookshelfNameEditPanel();
            CloseBookshelfDeleteConfirmPanel();
            CloseBookshelfDesignPanel();
            CancelBookAddPlacementSelection(false, false);
            CloseBookAddPanel(false);
            CloseBookAddConfirmPanelSilently();
            CloseBookshelfManagePanel();
            RefreshHud();
        }

        private void OpenBookAddFromBottom()
        {
            if (CurrentFocusArea != ExperienceFocusArea.Bookshelf ||
                (state != null && state.IsViewingBook))
            {
                return;
            }

            if (TryPromptUnsavedBookshelfChanges(OpenBookAddFromBottom))
            {
                return;
            }

            if (IsBookshelfEditMode)
            {
                SetBookshelfEditMode(false);
            }

            OpenBookAddPanel();
        }

        private void SetBookshelfEditMode(bool enabled)
        {
            var canEnable = enabled &&
                            CurrentFocusArea == ExperienceFocusArea.Bookshelf &&
                            (cameraController == null || !cameraController.IsTransitioning) &&
                            (bookshelfGenerator == null || !bookshelfGenerator.IsBookshelfSwitching);
            var nextState = canEnable;
            if (IsBookshelfEditMode == nextState)
            {
                RefreshHud();
                return;
            }

            IsBookshelfEditMode = nextState;
            if (IsBookshelfEditMode)
            {
                hasUnsavedBookshelfEditChanges = false;
                CancelBookPlacementEdit(false);
                isHamburgerMenuOpen = false;
                ActiveDetailBook = null;
                isBookSearchPanelOpen = false;
                SetBookshelfOverlayMode(BookshelfOverlayMode.None);
                CancelBookAddPlacementSelection(false, false);
                bookSearchResults.Clear();
                if (state != null && state.IsViewingBook && interactionController != null)
                {
                    interactionController.CloseViewedBook(true);
                }

                if (bookDetailPanel != null)
                {
                    bookDetailPanel.SetActive(false);
                }

                if (bookEditPanel != null)
                {
                    bookEditPanel.SetActive(false);
                }

                if (bookSearchPanel != null)
                {
                    bookSearchPanel.SetActive(false);
                }

                if (sectionEditPanel != null)
                {
                    sectionEditPanel.SetActive(false);
                }

                activeEditSection = null;
            }
            else if (isBookEditPanelOpen)
            {
                ActiveDetailBook = null;
                pendingDeleteBook = null;
                isBookEditPanelOpen = false;
                if (bookDetailPanel != null)
                {
                    bookDetailPanel.SetActive(false);
                }

                if (bookEditPanel != null)
                {
                    bookEditPanel.SetActive(false);
                }
            }
            else if (sectionEditPanel != null && sectionEditPanel.activeSelf)
            {
                sectionEditPanel.SetActive(false);
                activeEditSection = null;
            }

            if (!IsBookshelfEditMode)
            {
                hasUnsavedBookshelfEditChanges = false;
                SetBookshelfOverlayMode(BookshelfOverlayMode.None);
                CancelBookAddPlacementSelection(false, false);
                CloseBookAddPanel(false);
                if (bookshelfManagePanel != null)
                {
                    bookshelfManagePanel.SetActive(false);
                }

                if (sectionEditPanel != null)
                {
                    sectionEditPanel.SetActive(false);
                }

                activeEditSection = null;
            }

            RefreshHud();
        }

        private void RunMenuAction(UnityEngine.Events.UnityAction action)
        {
            action?.Invoke();
            isHamburgerMenuOpen = false;
            RefreshHud();
        }

        private void ReturnFromFocusedArea()
        {
            if (state != null && state.IsViewingBook)
            {
                CloseActiveViewedBook();
                return;
            }

            if (CurrentFocusArea == ExperienceFocusArea.Bookshelf)
            {
                if (TryPromptUnsavedBookshelfChanges(ReturnToBookshelfEntryMainViewImmediate))
                {
                    return;
                }

                ReturnToBookshelfEntryMainViewImmediate();
                return;
            }

            // 노트북/대시보드 (Guestbook) 포커스 종료 → 책장과 동일하게 MainView (책장 바라보는 시점) 로 복귀.
            // 이전엔 FocusQuarterView 라 시점 일관성 깨졌음 (책장은 MainView, 노트북/대시보드는 QuarterView).
            // 명시 user choice (F1 단축키, 햄버거 메뉴 QuarterView 버튼) 는 별도 경로라 영향 없음.
            FocusMainView();
        }

        private void RememberBookshelfReturnMainViewMode()
        {
            if (cameraController != null && cameraController.CurrentMode != CameraMode.Overview)
            {
                return;
            }

            bookshelfReturnMainViewMode = currentMainViewMode;
        }

        private void ReturnToBookshelfEntryMainViewImmediate()
        {
            if (bookshelfReturnMainViewMode == MainCameraViewMode.MainView)
            {
                FocusMainViewImmediate();
                return;
            }

            FocusQuarterViewImmediate();
        }

        private void PrepareQuarterViewReturn()
        {
            CancelBookPlacementEdit(false);
            CancelBookAddPlacementSelection(false, false);
            SetBookshelfEditMode(false);
            isHamburgerMenuOpen = false;

            if (state != null && state.IsViewingBook && interactionController != null)
            {
                interactionController.CloseViewedBook(true);
            }

            if (bookDetailPanel != null)
            {
                bookDetailPanel.SetActive(false);
            }

            if (bookEditPanel != null)
            {
                bookEditPanel.SetActive(false);
            }

            if (sectionEditPanel != null)
            {
                sectionEditPanel.SetActive(false);
            }

            activeEditSection = null;

            CloseOverlayPanels();
            CloseGuestbookPanel();

            if (notebookScreenController != null)
            {
                notebookScreenController.SetImmersiveMode(false);
                notebookScreenController.ReturnToScreenSaver();
                notebookScreenController.Show();
            }
        }

        private void RefreshHud()
        {
            var isBookshelfFocused = CurrentFocusArea == ExperienceFocusArea.Bookshelf;
            var isViewingBook = state != null && state.IsViewingBook;

            if (hamburgerMenuPanel != null)
            {
                hamburgerMenuPanel.SetActive(false);
            }

            if (hamburgerButton != null)
            {
                hamburgerButton.gameObject.SetActive(false);
            }

            if (focusBackButton != null)
            {
                var showBackButton = CurrentFocusArea != ExperienceFocusArea.QuarterView &&
                                     CurrentFocusArea != ExperienceFocusArea.Notebook;
                var backRect = focusBackButton.GetComponent<RectTransform>();
                if (backRect != null)
                {
                    backRect.anchoredPosition = isBookshelfFocused ? new Vector2(34f, -34f) : new Vector2(20f, -20f);
                    backRect.sizeDelta = isBookshelfFocused ? new Vector2(66f, 66f) : new Vector2(120f, 44f);
                }

                var showBookCloseButton = isBookshelfFocused && state != null && state.IsViewingBook;
                SetButtonLabel(focusBackButton, showBookCloseButton ? "X" : (isBookshelfFocused ? "←" : "Back"));
                focusBackButton.gameObject.SetActive(showBackButton);
                focusBackButton.interactable = showBackButton && (cameraController == null || !cameraController.IsTransitioning);
                StyleHudButton(
                    focusBackButton,
                    new Color32(255, 255, 255, 235),
                    new Color32(241, 235, 230, 255),
                    new Color32(224, 219, 214, 255),
                    isBookshelfFocused ? 34 : 16,
                    new Color32(45, 50, 58, 255),
                    true);
            }

            RefreshBookshelfOverlay();
            RefreshBookshelfNameEditPanel();
            RefreshBookSearchPanel();
            RefreshBookshelfManagePanel();
            RefreshBookshelfStatusPanel();
            RefreshGuestbookPanel();
            RefreshBookDetailPanel();
            RefreshBookFocusToolbar();
            RefreshBookReviewWritePanel();
            RefreshBookReportWritePanel();
            RefreshBookReportReadPanel();
            RefreshBookEditPanel();
            RefreshSectionEditPanel();
            RefreshBookPlacementEditor();
            ApplyResponsiveFocusOverlayLayout(true);
            RefreshDeleteResultDialog();
            RequestEmptyBookshelfTutorialEntry();
            RefreshFirstBookshelfTutorialPanel();
            RefreshEmptyBookshelfAddButton();
        }

        private void ApplyResponsiveFocusOverlayLayout(bool force = false)
        {
            if (hudCanvas == null)
            {
                return;
            }

            var canvasSize = GetHudCanvasSize();
            if (canvasSize.x < 1f || canvasSize.y < 1f)
            {
                return;
            }

            var layoutWidth = Mathf.RoundToInt(canvasSize.x);
            var layoutHeight = Mathf.RoundToInt(canvasSize.y);
            if (!force &&
                layoutWidth == lastResponsiveLayoutWidth &&
                layoutHeight == lastResponsiveLayoutHeight)
            {
                return;
            }

            lastResponsiveLayoutWidth = layoutWidth;
            lastResponsiveLayoutHeight = layoutHeight;

            var safeMargins = GetHudSafeMargins(canvasSize);
            var aspect = canvasSize.x / Mathf.Max(1f, canvasSize.y);
            var isCompact = aspect < 1.45f;
            var shortSide = Mathf.Min(canvasSize.x, canvasSize.y);
            var edge = Mathf.Clamp(shortSide * 0.034f, 28f, 56f);
            var topOffset = safeMargins.w + Mathf.Clamp(canvasSize.y * 0.038f, 36f, 58f);
            var bottomOffset = safeMargins.y + Mathf.Clamp(canvasSize.y * 0.034f, 34f, 56f);

            ApplyResponsiveBackButtonLayout(safeMargins, edge, topOffset, canvasSize);
            ApplyResponsiveBookshelfIndexLayout(canvasSize, topOffset, isCompact);
            ApplyResponsiveBookshelfTopActionLayout(safeMargins, edge, topOffset, canvasSize);
            ApplyResponsiveBookshelfNavigationLayout(canvasSize, safeMargins, edge, bottomOffset, isCompact);
            ApplyResponsiveBookshelfOverlayPanelLayout(canvasSize, safeMargins, edge, topOffset, bottomOffset, isCompact);
            ApplyResponsiveBookFocusLayout(canvasSize, safeMargins, edge, bottomOffset, aspect, isCompact);
        }

        private void ApplyResponsiveBackButtonLayout(Vector4 safeMargins, float edge, float topOffset, Vector2 canvasSize)
        {
            if (focusBackButton == null || !focusBackButton.gameObject.activeSelf)
            {
                return;
            }

            var isBookshelfFocus = CurrentFocusArea == ExperienceFocusArea.Bookshelf;
            if (!isBookshelfFocus)
            {
                return;
            }

            var size = Mathf.Clamp(canvasSize.y * 0.072f, 74f, 94f);
            SetHudComponentRect(
                focusBackButton,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(safeMargins.x + edge, -topOffset),
                new Vector2(size, size));

            var label = focusBackButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontSize = Mathf.RoundToInt(Mathf.Clamp(size * 0.42f, 30f, 38f));
                label.alignment = TextAnchor.MiddleCenter;
                StretchTextToParent(label);
            }
        }

        private void ApplyResponsiveBookshelfIndexLayout(Vector2 canvasSize, float topOffset, bool isCompact)
        {
            if (bookshelfIndexPanel == null)
            {
                return;
            }

            var panelWidth = Mathf.Clamp(canvasSize.x * (isCompact ? 0.46f : 0.18f), 300f, 420f);
            var panelHeight = Mathf.Clamp(canvasSize.y * 0.08f, 88f, 112f);
            SetHudObjectRect(
                bookshelfIndexPanel,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -topOffset),
                new Vector2(panelWidth, panelHeight));

            if (bookshelfIndexText != null)
            {
                SetHudComponentRect(
                    bookshelfIndexText,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -Mathf.Clamp(panelHeight * 0.26f, 23f, 30f)),
                    new Vector2(panelWidth - 56f, Mathf.Clamp(panelHeight * 0.34f, 30f, 38f)));
                bookshelfIndexText.fontSize = Mathf.RoundToInt(Mathf.Clamp(panelHeight * 0.24f, 20f, 26f));
                bookshelfIndexText.alignment = TextAnchor.MiddleCenter;
            }

            if (bookshelfIndexDotsRoot != null)
            {
                SetHudComponentRect(
                    bookshelfIndexDotsRoot,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -(panelHeight - 30f)),
                    new Vector2(Mathf.Clamp(panelWidth * 0.46f, 108f, 176f), 12f));
            }
        }

        private void ApplyResponsiveBookshelfTopActionLayout(Vector4 safeMargins, float edge, float topOffset, Vector2 canvasSize)
        {
            var buttonHeight = Mathf.Clamp(canvasSize.y * 0.05f, 48f, 62f);
            var gap = Mathf.Clamp(canvasSize.x * 0.008f, 12f, 18f);
            var saveWidth = Mathf.Clamp(canvasSize.x * 0.064f, 112f, 136f);
            var designWidth = Mathf.Clamp(canvasSize.x * 0.084f, 144f, 178f);
            var manageWidth = Mathf.Clamp(canvasSize.x * 0.078f, 136f, 166f);
            var rightOffset = safeMargins.z + edge;

            SetTopRightButtonRect(bookshelfEditModeButton, rightOffset, topOffset, saveWidth, buttonHeight);
            SetTopRightButtonRect(openBookshelfDesignButton, rightOffset + saveWidth + gap, topOffset, designWidth, buttonHeight);
            SetTopRightButtonRect(openBookshelfManageButton, rightOffset + saveWidth + gap + designWidth + gap, topOffset, manageWidth, buttonHeight);
        }

        private static void SetTopRightButtonRect(Button button, float rightOffset, float topOffset, float width, float height)
        {
            if (button == null)
            {
                return;
            }

            SetHudComponentRect(
                button,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-rightOffset, -topOffset),
                new Vector2(width, height));

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontSize = Mathf.RoundToInt(Mathf.Clamp(height * 0.31f, 14f, 18f));
                StretchTextToParent(label);
            }
        }

        private void ApplyResponsiveBookshelfNavigationLayout(
            Vector2 canvasSize,
            Vector4 safeMargins,
            float edge,
            float bottomOffset,
            bool isCompact)
        {
            var sideButtonSize = Mathf.Clamp(canvasSize.y * 0.066f, 64f, 84f);
            var sideButtonInset = Mathf.Clamp(canvasSize.x * (isCompact ? 0.055f : 0.15f), 96f, 320f);
            sideButtonInset = Mathf.Max(sideButtonInset, edge + safeMargins.x);

            SetHudComponentRect(
                overlayPreviousFocusButton,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(sideButtonInset, 0f),
                new Vector2(sideButtonSize, sideButtonSize));
            SetHudComponentRect(
                overlayNextFocusButton,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-Mathf.Max(sideButtonInset, edge + safeMargins.z), 0f),
                new Vector2(sideButtonSize, sideButtonSize));

            if (bookshelfBottomNavPanel == null)
            {
                return;
            }

            var panelWidth = Mathf.Clamp(canvasSize.x * (isCompact ? 0.88f : 0.46f), 700f, 980f);
            var panelHeight = Mathf.Clamp(canvasSize.y * 0.12f, 128f, 158f);
            SetHudObjectRect(
                bookshelfBottomNavPanel,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, bottomOffset),
                new Vector2(panelWidth, panelHeight));

            LayoutBookshelfBottomNavButtons(panelWidth, panelHeight);
        }

        private void LayoutBookshelfBottomNavButtons(float panelWidth, float panelHeight)
        {
            var padding = Mathf.Clamp(panelWidth * 0.06f, 42f, 62f);
            var gap = Mathf.Clamp(panelWidth * 0.032f, 22f, 34f);
            var buttonWidth = (panelWidth - padding * 2f - gap * 3f) / 4f;
            var buttonHeight = panelHeight - Mathf.Clamp(panelHeight * 0.31f, 40f, 50f);
            var startX = -panelWidth * 0.5f + padding + buttonWidth * 0.5f;

            LayoutBottomNavButton(bookshelfNavHomeButton, startX, buttonWidth, buttonHeight, panelHeight);
            LayoutBottomNavButton(bookshelfNavSearchButton, startX + (buttonWidth + gap), buttonWidth, buttonHeight, panelHeight);
            LayoutBottomNavButton(bookshelfNavAddButton, startX + (buttonWidth + gap) * 2f, buttonWidth, buttonHeight, panelHeight);
            LayoutBottomNavButton(bookshelfNavEditButton, startX + (buttonWidth + gap) * 3f, buttonWidth, buttonHeight, panelHeight);
        }

        private static void LayoutBottomNavButton(Button button, float x, float width, float height, float panelHeight)
        {
            if (button == null)
            {
                return;
            }

            SetHudComponentRect(
                button,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(x, 0f),
                new Vector2(width, height));

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontSize = Mathf.RoundToInt(Mathf.Clamp(panelHeight * 0.17f, 18f, 24f));
                label.alignment = TextAnchor.MiddleCenter;
                label.lineSpacing = 0.98f;
                StretchTextToParent(label);
            }
        }

        private void ApplyResponsiveBookshelfOverlayPanelLayout(
            Vector2 canvasSize,
            Vector4 safeMargins,
            float edge,
            float topOffset,
            float bottomOffset,
            bool isCompact)
        {
            var modalTop = safeMargins.w + Mathf.Clamp(canvasSize.y * 0.12f, 116f, 158f);
            var widePanelWidth = Mathf.Clamp(canvasSize.x * (isCompact ? 0.88f : 0.62f), 760f, 980f);
            var mediumPanelWidth = Mathf.Clamp(canvasSize.x * (isCompact ? 0.74f : 0.42f), 600f, 760f);
            var largeOverlayWidth = Mathf.Clamp(canvasSize.x * 0.80f, 760f, Mathf.Max(760f, canvasSize.x - safeMargins.x - safeMargins.z - edge * 2f));
            var largeOverlayHeight = Mathf.Clamp(canvasSize.y * 0.80f, 620f, Mathf.Max(620f, canvasSize.y - safeMargins.y - safeMargins.w - edge * 2f));

            ApplyBookshelfNameEditPanelLayout(mediumPanelWidth, 128f, modalTop);
            ApplyBookshelfDeleteConfirmPanelLayout(mediumPanelWidth, 184f, modalTop + 8f);
            ApplyBookshelfDesignPanelLayout(Mathf.Clamp(mediumPanelWidth + 48f, 620f, 760f), 282f, modalTop + 16f);
            ApplyBookAddPanelLayout(largeOverlayWidth, largeOverlayHeight, modalTop + 8f);
            ApplyBookAddConfirmPanelLayout(Mathf.Clamp(mediumPanelWidth + 24f, 620f, 740f), 520f, modalTop + 12f);
            ApplyBookSearchPanelLayout(canvasSize, safeMargins, edge, topOffset, bottomOffset, isCompact);
            ApplyBookshelfManagePanelLayout(widePanelWidth, Mathf.Clamp(canvasSize.y * 0.78f, 720f, 860f), modalTop + 8f);
            ApplyBookshelfUnsavedConfirmPanelLayout(Mathf.Clamp(mediumPanelWidth, 560f, 680f), 204f, modalTop + 28f);
            ApplyDeleteResultDialogLayout(Mathf.Clamp(mediumPanelWidth, 520f, 680f), 210f);
            ApplyBookshelfStatusPanelLayout(canvasSize, safeMargins, edge, bottomOffset);
        }

        private void ApplyBookshelfNameEditPanelLayout(float panelWidth, float panelHeight, float top)
        {
            if (bookshelfNameEditPanel == null)
            {
                return;
            }

            var padding = 34f;
            var buttonGap = 12f;
            var closeWidth = 40f;
            var saveWidth = 74f;
            var inputWidth = Mathf.Max(260f, panelWidth - padding * 2f - saveWidth - closeWidth - buttonGap * 2f);

            SetHudObjectRect(bookshelfNameEditPanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(panelWidth, panelHeight));
            SetHudComponentRect(bookshelfNameEditLabelText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -24f), new Vector2(panelWidth - padding * 2f, 20f));
            SetHudComponentRect(bookshelfNameInputField, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -60f), new Vector2(inputWidth, 42f));
            SetHudComponentRect(saveBookshelfNameButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + inputWidth + buttonGap, -60f), new Vector2(saveWidth, 42f));
            SetHudComponentRect(closeBookshelfNameEditButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + inputWidth + buttonGap + saveWidth + buttonGap, -60f), new Vector2(closeWidth, 42f));
        }

        private void ApplyBookshelfDeleteConfirmPanelLayout(float panelWidth, float panelHeight, float top)
        {
            if (bookshelfDeleteConfirmPanel == null)
            {
                return;
            }

            var padding = 34f;
            var contentWidth = panelWidth - padding * 2f;
            var buttonWidth = 100f;
            var buttonHeight = 38f;
            var gap = 12f;
            var buttonY = -(panelHeight - padding - OverlayButtonBottomPaddingExtra - buttonHeight);
            var cancelX = padding + contentWidth - buttonWidth;

            SetHudObjectRect(bookshelfDeleteConfirmPanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(panelWidth, panelHeight));
            SetHudChildRect(bookshelfDeleteConfirmPanel, "BookshelfDeleteAccent", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -24f), new Vector2(90f, 3f));
            SetHudChildActive(bookshelfDeleteConfirmPanel, "BookshelfDeleteAccent", false);
            SetHudComponentRect(bookshelfDeleteConfirmTitleText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -38f), new Vector2(contentWidth, 26f));
            SetHudComponentRect(bookshelfDeleteConfirmMessageText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -76f), new Vector2(contentWidth, 60f));
            SetHudComponentRect(confirmDeleteBookshelfButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(cancelX - gap - buttonWidth, buttonY), new Vector2(buttonWidth, buttonHeight));
            SetHudComponentRect(cancelDeleteBookshelfButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(cancelX, buttonY), new Vector2(buttonWidth, buttonHeight));
        }

        private void ApplyBookshelfDesignPanelLayout(float panelWidth, float panelHeight, float top)
        {
            if (bookshelfDesignPanel == null)
            {
                return;
            }

            var padding = 38f;
            var contentWidth = panelWidth - padding * 2f;
            var gap = 16f;
            var optionWidth = (contentWidth - gap * 2f) / 3f;
            var optionY = -130f;

            SetHudObjectRect(bookshelfDesignPanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(panelWidth, panelHeight));
            SetHudChildRect(bookshelfDesignPanel, "BookshelfDesignAccent", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -28f), new Vector2(96f, 3f));
            SetHudChildActive(bookshelfDesignPanel, "BookshelfDesignAccent", false);
            SetHudComponentRect(bookshelfDesignTitleText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -44f), new Vector2(contentWidth, 28f));
            SetHudComponentRect(bookshelfDesignHintText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -82f), new Vector2(contentWidth, 38f));
            SetHudComponentRect(defaultBookshelfDesignButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, optionY), new Vector2(optionWidth, 42f));
            SetHudComponentRect(warmBookshelfDesignButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + optionWidth + gap, optionY), new Vector2(optionWidth, 42f));
            SetHudComponentRect(darkBookshelfDesignButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + (optionWidth + gap) * 2f, optionY), new Vector2(optionWidth, 42f));
            SetHudComponentRect(closeBookshelfDesignButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -(panelHeight - padding - OverlayButtonBottomPaddingExtra - 40f)), new Vector2(contentWidth, 40f));
        }

        private void ApplyBookAddPanelLayout(float panelWidth, float panelHeight, float top)
        {
            if (bookAddPanel == null)
            {
                return;
            }

            var padding = 48f;
            var contentWidth = panelWidth - padding * 2f;
            var scrollbarWidth = 10f;
            var scrollbarGap = 12f;
            var rowsWidth = contentWidth - scrollbarWidth - scrollbarGap;
            var modeGap = 10f;
            var modeWidth = Mathf.Min(112f, (contentWidth - modeGap * 2f) / 3f);
            var selectorLabelY = -126f;
            var selectorY = -156f;
            var modeY = -214f;
            var inputY = -262f;
            var viewportY = -324f;
            var statusHeight = 24f;
            var statusY = -(panelHeight - padding - statusHeight);
            var viewportHeight = Mathf.Max(260f, -statusY - Mathf.Abs(viewportY) - 24f);

            SetHudObjectRect(bookAddPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(panelWidth, panelHeight));
            SetHudChildRect(bookAddPanel, "BookAddAccent", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -28f), new Vector2(96f, 3f));
            SetHudChildActive(bookAddPanel, "BookAddAccent", false);
            SetHudComponentRect(bookAddTitleText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -44f), new Vector2(contentWidth - 48f, 28f));
            SetHudComponentRect(bookAddHintText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -84f), new Vector2(contentWidth, 44f));
            SetHudComponentRect(bookAddShelfSelectorLabelText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, selectorLabelY), new Vector2(contentWidth, 18f));
            LayoutBookshelfSelectorButtons(bookAddShelfSelectorButtons, padding, selectorY, contentWidth, 34f, Mathf.Min(116f, contentWidth * 0.18f));
            SetHudComponentRect(bookAddSearchModeButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, modeY), new Vector2(modeWidth, 34f));
            SetHudComponentRect(bookAddRecommendedModeButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + modeWidth + modeGap, modeY), new Vector2(modeWidth, 34f));
            SetHudComponentRect(bookAddRankingModeButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + (modeWidth + modeGap) * 2f, modeY), new Vector2(modeWidth, 34f));
            SetHudComponentRect(bookAddSearchInputField, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, inputY), new Vector2(contentWidth, 42f));
            SetHudComponentRect(closeBookAddButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(panelWidth - padding - 34f, -28f), new Vector2(34f, 34f));
            SetHudChildRect(bookAddPanel, "BookAddRowsViewport", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, viewportY), new Vector2(rowsWidth, viewportHeight));
            SetHudComponentRect(bookAddScrollbar, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + rowsWidth + scrollbarGap, viewportY), new Vector2(scrollbarWidth, viewportHeight));
            SetHudComponentRect(bookAddStatusText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, statusY), new Vector2(contentWidth, statusHeight));
            ResizeBookAddRows(rowsWidth);
        }

        private void ApplyBookAddConfirmPanelLayout(float panelWidth, float panelHeight, float top)
        {
            if (bookAddConfirmPanel == null)
            {
                return;
            }

            var padding = 42f;
            var contentWidth = panelWidth - padding * 2f;
            var gap = 14f;
            var buttonHeight = 38f;
            var optionWidth = Mathf.Min(128f, (contentWidth - gap * 2f) / 3f);
            var actionY = -(panelHeight - padding - OverlayButtonBottomPaddingExtra - buttonHeight);
            var cancelWidth = 88f;
            var addWidth = 102f;
            var cancelX = padding + contentWidth - cancelWidth;

            SetHudObjectRect(bookAddConfirmPanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(panelWidth, panelHeight));
            SetHudChildRect(bookAddConfirmPanel, "BookAddConfirmAccent", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -28f), new Vector2(96f, 3f));
            SetHudChildActive(bookAddConfirmPanel, "BookAddConfirmAccent", false);
            SetHudComponentRect(bookAddConfirmTitleText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -44f), new Vector2(contentWidth, 50f));
            SetHudComponentRect(bookAddConfirmMetaText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -104f), new Vector2(contentWidth, 26f));
            SetHudComponentRect(bookAddConfirmSummaryText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -140f), new Vector2(contentWidth, 72f));
            SetHudChildRect(bookAddConfirmPanel, "BookAddConfirmReadingStatusLabel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -226f), new Vector2(contentWidth, 18f));
            SetHudComponentRect(bookAddConfirmUnreadButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -252f), new Vector2(optionWidth, 38f));
            SetHudComponentRect(bookAddConfirmReadingButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + optionWidth + gap, -252f), new Vector2(optionWidth, 38f));
            SetHudComponentRect(bookAddConfirmReadButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + (optionWidth + gap) * 2f, -252f), new Vector2(optionWidth, 38f));
            SetHudChildRect(bookAddConfirmPanel, "BookAddConfirmPlacementLabel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -316f), new Vector2(contentWidth, 18f));
            SetHudComponentRect(bookAddConfirmSpineButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -342f), new Vector2(optionWidth, 38f));
            SetHudComponentRect(bookAddConfirmAngledButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + optionWidth + gap, -342f), new Vector2(optionWidth, 38f));
            SetHudComponentRect(bookAddConfirmFrontButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + (optionWidth + gap) * 2f, -342f), new Vector2(optionWidth, 38f));
            SetHudComponentRect(confirmBookAddButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(cancelX - gap - addWidth, actionY), new Vector2(addWidth, buttonHeight));
            SetHudComponentRect(cancelBookAddConfirmButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(cancelX, actionY), new Vector2(cancelWidth, buttonHeight));
        }

        private void ApplyBookSearchPanelLayout(
            Vector2 canvasSize,
            Vector4 safeMargins,
            float edge,
            float topOffset,
            float bottomOffset,
            bool isCompact)
        {
            if (bookSearchPanel == null)
            {
                return;
            }

            var panelWidth = Mathf.Clamp(canvasSize.x * (isCompact ? 0.88f : 0.72f), 760f, 1040f);
            var panelHeight = Mathf.Clamp(canvasSize.y * 0.74f, 640f, 820f);
            var padding = 48f;
            var contentWidth = panelWidth - padding * 2f;
            var scrollbarWidth = 10f;
            var scrollbarGap = 12f;
            var rowsWidth = contentWidth - scrollbarWidth - scrollbarGap;
            var selectorLabelY = -144f;
            var selectorY = -174f;
            var filterY = -226f;
            var viewportY = -278f;
            var statusHeight = 24f;
            var statusY = -(panelHeight - padding - statusHeight);
            var viewportHeight = Mathf.Max(260f, -statusY - Mathf.Abs(viewportY) - 24f);
            var filterGap = 10f;
            var filterWidth = (contentWidth - filterGap * 3f) / 4f;

            SetHudObjectRect(bookSearchPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, Mathf.Clamp(canvasSize.y * 0.06f, 64f, 96f)), new Vector2(panelWidth, panelHeight));
            SetHudChildRect(bookSearchPanel, "BookSearchAccent", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -28f), new Vector2(96f, 3f));
            SetHudChildActive(bookSearchPanel, "BookSearchAccent", false);
            SetHudComponentRect(bookSearchTitleText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -44f), new Vector2(contentWidth - 48f, 28f));
            SetHudComponentRect(closeBookSearchButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(panelWidth - padding - 34f, -28f), new Vector2(34f, 34f));
            SetHudComponentRect(bookSearchInputField, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -92f), new Vector2(contentWidth, 44f));
            SetHudComponentRect(bookSearchShelfSelectorLabelText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, selectorLabelY), new Vector2(contentWidth, 18f));
            LayoutBookshelfSelectorButtons(bookSearchShelfSelectorButtons, padding, selectorY, contentWidth, 32f, Mathf.Min(96f, contentWidth * 0.22f));
            SetHudComponentRect(bookSearchAllFilterButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, filterY), new Vector2(filterWidth, 34f));
            SetHudComponentRect(bookSearchReadingFilterButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + filterWidth + filterGap, filterY), new Vector2(filterWidth, 34f));
            SetHudComponentRect(bookSearchReadFilterButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + (filterWidth + filterGap) * 2f, filterY), new Vector2(filterWidth, 34f));
            SetHudComponentRect(bookSearchLikedFilterButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + (filterWidth + filterGap) * 3f, filterY), new Vector2(filterWidth, 34f));
            SetHudChildRect(bookSearchPanel, "BookSearchRowsViewport", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, viewportY), new Vector2(rowsWidth, viewportHeight));
            SetHudComponentRect(bookSearchScrollbar, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + rowsWidth + scrollbarGap, viewportY), new Vector2(scrollbarWidth, viewportHeight));
            SetHudComponentRect(bookSearchStatusText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, statusY), new Vector2(contentWidth, statusHeight));
            ResizeBookSearchRows(rowsWidth);
        }

        private void ApplyBookshelfManagePanelLayout(float panelWidth, float panelHeight, float top)
        {
            if (bookshelfManagePanel == null)
            {
                return;
            }

            var padding = 48f;
            var contentWidth = panelWidth - padding * 2f;
            var scrollbarWidth = 10f;
            var scrollbarGap = 12f;
            var rowsWidth = contentWidth - scrollbarWidth - scrollbarGap;
            var viewportY = -160f;
            var viewportHeight = Mathf.Clamp(panelHeight - 420f, 190f, 330f);
            var sortLabelY = viewportY - viewportHeight - 28f;
            var sortButtonY = sortLabelY - 28f;
            var actionY = sortButtonY - 58f;
            var statusY = -(panelHeight - padding - 28f);
            var gap = 12f;
            var sortWidth = (contentWidth - gap * 3f) / 4f;

            SetHudObjectRect(bookshelfManagePanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(panelWidth, panelHeight));
            SetHudChildRect(bookshelfManagePanel, "BookshelfManageAccent", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -28f), new Vector2(96f, 3f));
            SetHudChildActive(bookshelfManagePanel, "BookshelfManageAccent", false);
            SetHudComponentRect(bookshelfManageTitleText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -44f), new Vector2(contentWidth, 28f));
            SetHudChildRect(bookshelfManagePanel, "BookshelfManageSummaryCard", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -76f), new Vector2(contentWidth, 46f));
            SetHudComponentRect(bookshelfManageSummaryText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + 18f, -88f), new Vector2(contentWidth - 36f, 24f));
            SetHudChildRect(bookshelfManagePanel, "BookshelfManageListLabel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -126f), new Vector2(contentWidth, 18f));
            SetHudChildRect(bookshelfManagePanel, "BookshelfManageRowsViewport", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, viewportY), new Vector2(rowsWidth, viewportHeight));
            SetHudComponentRect(bookshelfManageScrollbar, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + rowsWidth + scrollbarGap, viewportY), new Vector2(scrollbarWidth, viewportHeight));
            SetHudChildRect(bookshelfManagePanel, "BookshelfManageSortLabel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, sortLabelY), new Vector2(contentWidth, 18f));
            SetHudComponentRect(shelfManageSortTitleButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, sortButtonY), new Vector2(sortWidth, 36f));
            SetHudComponentRect(shelfManageSortAuthorButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + sortWidth + gap, sortButtonY), new Vector2(sortWidth, 36f));
            SetHudComponentRect(shelfManageSortStatusButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + (sortWidth + gap) * 2f, sortButtonY), new Vector2(sortWidth, 36f));
            SetHudComponentRect(shelfManageSortLikedButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + (sortWidth + gap) * 3f, sortButtonY), new Vector2(sortWidth, 36f));
            SetHudComponentRect(shelfManagePreviousButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, actionY), new Vector2(0f, 0f));
            SetHudComponentRect(shelfManageNextButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, actionY), new Vector2(0f, 0f));
            var actionGap = 8f;
            var visibilityWidth = 128f;
            var renameWidth = 102f;
            var addWidth = 72f;
            var deleteWidth = 72f;
            var closeWidth = 88f;
            SetHudComponentRect(shelfManageVisibilityButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, actionY), new Vector2(visibilityWidth, 40f));
            SetHudComponentRect(shelfManageRenameButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + visibilityWidth + actionGap, actionY), new Vector2(renameWidth, 40f));
            SetHudComponentRect(shelfManageAddButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + visibilityWidth + renameWidth + actionGap * 2f, actionY), new Vector2(addWidth, 40f));
            SetHudComponentRect(shelfManageDeleteButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + visibilityWidth + renameWidth + addWidth + actionGap * 3f, actionY), new Vector2(deleteWidth, 40f));
            SetHudComponentRect(shelfManageCloseButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + contentWidth - closeWidth, actionY), new Vector2(closeWidth, 40f));
            SetHudComponentRect(bookshelfManageStatusText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, statusY), new Vector2(contentWidth, 28f));
            ResizeBookshelfManageRows(rowsWidth);
        }

        private void ApplyBookshelfUnsavedConfirmPanelLayout(float panelWidth, float panelHeight, float top)
        {
            if (bookshelfUnsavedConfirmPanel == null)
            {
                return;
            }

            var padding = 38f;
            var contentWidth = panelWidth - padding * 2f;
            var buttonHeight = 38f;
            var cancelWidth = 94f;
            var saveWidth = 146f;
            var gap = 14f;
            var buttonY = -(panelHeight - padding - OverlayButtonBottomPaddingExtra - buttonHeight);
            var cancelX = padding + contentWidth - cancelWidth;

            SetHudObjectRect(bookshelfUnsavedConfirmPanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(panelWidth, panelHeight));
            SetHudChildRect(bookshelfUnsavedConfirmPanel, "BookshelfUnsavedConfirmAccent", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -28f), new Vector2(96f, 3f));
            SetHudChildActive(bookshelfUnsavedConfirmPanel, "BookshelfUnsavedConfirmAccent", false);
            SetHudComponentRect(bookshelfUnsavedConfirmTitleText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -44f), new Vector2(contentWidth, 30f));
            SetHudComponentRect(bookshelfUnsavedConfirmMessageText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -88f), new Vector2(contentWidth, 48f));
            SetHudComponentRect(confirmBookshelfUnsavedSaveButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(cancelX - gap - saveWidth, buttonY), new Vector2(saveWidth, buttonHeight));
            SetHudComponentRect(cancelBookshelfUnsavedSaveButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(cancelX, buttonY), new Vector2(cancelWidth, buttonHeight));
        }

        private void ApplyDeleteResultDialogLayout(float panelWidth, float panelHeight)
        {
            if (deleteResultDialogPanel == null)
            {
                return;
            }

            var padding = 32f;
            var contentWidth = panelWidth - padding * 2f;
            var buttonWidth = 116f;
            var buttonHeight = 38f;
            var buttonGap = 14f;
            var buttonY = -panelHeight + padding + OverlayButtonBottomPaddingExtra;
            var hasRetry = deleteResultRetryAction != null;
            var closeX = padding + contentWidth - buttonWidth;
            var retryX = closeX - buttonGap - buttonWidth;

            SetHudObjectRect(deleteResultDialogPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(panelWidth, panelHeight));
            SetHudComponentRect(deleteResultTitleText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -40f), new Vector2(contentWidth, 30f));
            SetHudComponentRect(deleteResultMessageText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -86f), new Vector2(contentWidth, 58f));
            SetHudComponentRect(retryDeleteResultDialogButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(retryX, buttonY), new Vector2(buttonWidth, buttonHeight));
            SetHudComponentRect(closeDeleteResultDialogButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(closeX, buttonY), new Vector2(buttonWidth, buttonHeight));
            if (retryDeleteResultDialogButton != null)
            {
                retryDeleteResultDialogButton.gameObject.SetActive(hasRetry);
            }
        }

        private void ShowDeleteResultDialog(string title, string message, bool isSuccess)
        {
            ShowDeleteResultDialog(title, message, isSuccess, null, null);
        }

        private void ShowDeleteResultDialog(
            string title,
            string message,
            bool isSuccess,
            System.Action retryAction,
            string retryLabel = null)
        {
            deleteResultDialogTitle = string.IsNullOrWhiteSpace(title) ? (isSuccess ? "삭제 완료" : "삭제 실패") : title;
            deleteResultDialogMessage = string.IsNullOrWhiteSpace(message)
                ? isSuccess ? "요청한 항목을 삭제했습니다." : "삭제 요청을 완료하지 못했습니다."
                : message;
            deleteResultDialogIsSuccess = isSuccess;
            deleteResultRetryAction = retryAction;
            deleteResultRetryLabel = string.IsNullOrWhiteSpace(retryLabel) ? "재시도" : retryLabel.Trim();
            isDeleteResultDialogOpen = true;
            RefreshDeleteResultDialog();
        }

        private void CloseDeleteResultDialog()
        {
            CloseDeleteResultDialog(true);
        }

        private void CloseDeleteResultDialog(bool refresh)
        {
            isDeleteResultDialogOpen = false;
            deleteResultRetryAction = null;
            deleteResultRetryLabel = "재시도";
            if (deleteResultBackdrop != null)
            {
                deleteResultBackdrop.SetActive(false);
            }

            if (deleteResultDialogPanel != null)
            {
                deleteResultDialogPanel.SetActive(false);
            }

            if (refresh)
            {
                RefreshHud();
            }
        }

        private void RefreshDeleteResultDialog()
        {
            if (deleteResultDialogPanel == null)
            {
                return;
            }

            var show = isDeleteResultDialogOpen;
            if (deleteResultBackdrop != null)
            {
                deleteResultBackdrop.SetActive(show);
            }

            deleteResultDialogPanel.SetActive(show);
            if (!show)
            {
                return;
            }

            if (deleteResultBackdrop != null)
            {
                deleteResultBackdrop.transform.SetAsLastSibling();
            }

            deleteResultDialogPanel.transform.SetAsLastSibling();
            if (deleteResultTitleText != null)
            {
                deleteResultTitleText.text = deleteResultDialogTitle;
                deleteResultTitleText.color = deleteResultDialogIsSuccess
                    ? new Color32(44, 84, 60, 255)
                    : new Color32(112, 46, 42, 255);
            }

            if (deleteResultMessageText != null)
            {
                deleteResultMessageText.text = deleteResultDialogMessage;
            }

            var hasRetry = deleteResultRetryAction != null;
            if (retryDeleteResultDialogButton != null)
            {
                retryDeleteResultDialogButton.gameObject.SetActive(hasRetry);
                retryDeleteResultDialogButton.interactable = hasRetry;
                SetButtonLabel(retryDeleteResultDialogButton, deleteResultRetryLabel);
                StyleHudButton(
                    retryDeleteResultDialogButton,
                    new Color32(238, 248, 241, 245),
                    new Color32(247, 255, 250, 255),
                    new Color32(224, 219, 214, 140),
                    15,
                    new Color32(53, 94, 73, 255),
                    true);
            }

            StyleHudButton(
                closeDeleteResultDialogButton,
                deleteResultDialogIsSuccess ? new Color32(66, 116, 84, 245) : DeleteButtonBackground,
                deleteResultDialogIsSuccess ? new Color32(76, 132, 96, 255) : DeleteButtonHighlighted,
                new Color32(224, 219, 214, 140),
                15,
                Color.white,
                true);
        }

        private void RunDeleteResultRetryAction()
        {
            var retry = deleteResultRetryAction;
            if (retry == null)
            {
                CloseDeleteResultDialog();
                return;
            }

            CloseDeleteResultDialog(false);
            ShowBookshelfTransientStatus("재시도 중", "서버 요청을 다시 보냅니다.", false);
            retry.Invoke();
            RefreshHud();
        }

        private bool IsBookshelfTransientStatusActive()
        {
            return isBookshelfTransientStatusVisible &&
                   bookshelfTransientStatusExpiresAt > 0f &&
                   Time.unscaledTime < bookshelfTransientStatusExpiresAt;
        }

        private void ShowBookshelfTransientStatus(string title, string message, bool isError)
        {
            bookshelfTransientStatusTitle = string.IsNullOrWhiteSpace(title)
                ? (isError ? "요청 실패" : "저장 완료")
                : title.Trim();
            bookshelfTransientStatusMessage = string.IsNullOrWhiteSpace(message)
                ? (isError ? "요청을 완료하지 못했습니다." : "변경사항이 반영되었습니다.")
                : message.Trim();
            bookshelfTransientStatusIsError = isError;
            isBookshelfTransientStatusVisible = true;
            bookshelfTransientStatusExpiresAt = Time.unscaledTime + BookshelfTransientStatusDuration;
            RefreshHud();
        }

        private void ExpireBookshelfTransientStatusIfNeeded()
        {
            if (!isBookshelfTransientStatusVisible ||
                bookshelfTransientStatusExpiresAt <= 0f ||
                Time.unscaledTime < bookshelfTransientStatusExpiresAt)
            {
                return;
            }

            isBookshelfTransientStatusVisible = false;
            bookshelfTransientStatusTitle = string.Empty;
            bookshelfTransientStatusMessage = string.Empty;
            RefreshHud();
        }

        private void CloseBookshelfTransientStatusFromStatusPanel()
        {
            isBookshelfTransientStatusVisible = false;
            bookshelfTransientStatusTitle = string.Empty;
            bookshelfTransientStatusMessage = string.Empty;
            RefreshHud();
        }

        private static string NormalizeMutationErrorMessage(string errorMessage)
        {
            return string.IsNullOrWhiteSpace(errorMessage)
                ? "네트워크 또는 서버 응답을 확인해 주세요."
                : errorMessage.Trim();
        }

        private void ApplyBookshelfStatusPanelLayout(Vector2 canvasSize, Vector4 safeMargins, float edge, float bottomOffset)
        {
            if (bookshelfStatusPanel == null)
            {
                return;
            }

            var panelWidth = Mathf.Clamp(canvasSize.x * 0.22f, 380f, 500f);
            var panelHeight = 166f;
            var padding = 28f;
            var contentWidth = panelWidth - padding * 2f;

            SetHudObjectRect(bookshelfStatusPanel, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(safeMargins.x + edge, bottomOffset + 16f), new Vector2(panelWidth, panelHeight));
            SetHudComponentRect(bookshelfStatusTitleText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -24f), new Vector2(contentWidth, 24f));
            SetHudComponentRect(bookshelfStatusMessageText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -60f), new Vector2(contentWidth, 44f));
            SetHudComponentRect(bookshelfStatusAddButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -118f), new Vector2(118f, 34f));
        }

        private void ResizeBookAddRows(float rowWidth)
        {
            for (var i = 0; i < bookAddResultButtons.Length; i++)
            {
                SetHudComponentRect(
                    bookAddResultButtons[i],
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, -i * BookAddResultRowHeight),
                    new Vector2(rowWidth, BookAddResultRowHeight - 10f));
                SetHudComponentRect(
                    bookAddResultCoverImages[i],
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(14f, -12f),
                    new Vector2(64f, BookAddResultRowHeight - 34f));
                if (bookAddResultTexts[i] != null)
                {
                    bookAddResultTexts[i].rectTransform.offsetMin = new Vector2(94f, 8f);
                    bookAddResultTexts[i].rectTransform.offsetMax = new Vector2(-18f, -8f);
                }
            }
        }

        private void ResizeBookSearchRows(float rowWidth)
        {
            for (var i = 0; i < bookSearchResultButtons.Length; i++)
            {
                SetHudComponentRect(
                    bookSearchResultButtons[i],
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, -i * BookSearchResultRowHeight),
                    new Vector2(rowWidth, BookSearchResultRowHeight - 8f));
                if (bookSearchResultTexts[i] != null)
                {
                    bookSearchResultTexts[i].rectTransform.offsetMin = new Vector2(18f, 2f);
                    bookSearchResultTexts[i].rectTransform.offsetMax = new Vector2(-16f, -2f);
                }
            }
        }

        private void ResizeBookshelfManageRows(float rowWidth)
        {
            if (bookshelfManageRowsContent != null)
            {
                bookshelfManageRowsContent.sizeDelta = new Vector2(0f, ShelfManageMaxRowCount * ShelfManageRowHeight);
            }

            for (var i = 0; i < bookshelfManageRowButtons.Length; i++)
            {
                SetHudComponentRect(
                    bookshelfManageRowButtons[i],
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, -i * ShelfManageRowHeight),
                    new Vector2(rowWidth, ShelfManageRowHeight - 6f));
                if (bookshelfManageDragHandles[i] != null)
                {
                    SetHudComponentRect(
                        bookshelfManageDragHandles[i],
                        new Vector2(0f, 1f),
                        new Vector2(0f, 1f),
                        new Vector2(Mathf.Max(14f, rowWidth - 52f), -5f),
                        new Vector2(38f, 32f));
                }

                if (bookshelfManageRowTexts[i] != null)
                {
                    bookshelfManageRowTexts[i].rectTransform.offsetMin = new Vector2(18f, 2f);
                    bookshelfManageRowTexts[i].rectTransform.offsetMax = new Vector2(-68f, -2f);
                }
            }
        }

        private void ApplyResponsiveBookFocusLayout(
            Vector2 canvasSize,
            Vector4 safeMargins,
            float edge,
            float bottomOffset,
            float aspect,
            bool isCompact)
        {
            var toolbarWidth = Mathf.Clamp(canvasSize.x * (isCompact ? 0.88f : 0.48f), 680f, 960f);
            var toolbarHeight = Mathf.Clamp(canvasSize.y * 0.1f, 104f, 134f);
            LayoutBookFocusToolbar(bookFocusToolbarPanel, toolbarWidth, toolbarHeight, bottomOffset, isCompact);
            LayoutBookPlacementToolbar(
                bookPlacementEditorPanel,
                Mathf.Clamp(toolbarWidth * 0.44f, 320f, 440f),
                Mathf.Clamp(toolbarHeight * 0.48f, 52f, 64f),
                bottomOffset,
                canvasSize);

            if (bookFocusWriteReportButton != null)
            {
                SetHudComponentRect(
                    bookFocusWriteReportButton,
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, bottomOffset + toolbarHeight + Mathf.Clamp(canvasSize.y * 0.028f, 24f, 40f)),
                    new Vector2(Mathf.Clamp(toolbarWidth * 0.22f, 158f, 220f), Mathf.Clamp(toolbarHeight * 0.42f, 44f, 54f)));
            }

            var sidePanelWidth = Mathf.Clamp(canvasSize.x * (isCompact ? 0.58f : 0.28f), 460f, 620f);
            var sidePanelTop = safeMargins.w + Mathf.Clamp(canvasSize.y * 0.078f, 76f, 112f);
            var sidePanelBottomGuard = bottomOffset + toolbarHeight + edge;
            var allowedHeight = Mathf.Max(360f, canvasSize.y - sidePanelTop - sidePanelBottomGuard);
            var detailPanelHeight = Mathf.Min(Mathf.Clamp(canvasSize.y * 0.44f, 480f, 600f), allowedHeight);
            var reportPanelHeight = Mathf.Min(Mathf.Clamp(canvasSize.y * 0.50f, 540f, 680f), allowedHeight);
            var writePanelWidth = Mathf.Clamp(canvasSize.x * 0.80f, 760f, Mathf.Max(760f, canvasSize.x - safeMargins.x - safeMargins.z - edge * 2f));
            var writePanelHeight = Mathf.Clamp(canvasSize.y * 0.80f, 620f, Mathf.Max(620f, canvasSize.y - safeMargins.y - safeMargins.w - edge * 2f));
            var sidePanelAnchor = aspect < 1.25f ? new Vector2(0.5f, 1f) : new Vector2(1f, 1f);
            var sidePanelPivot = aspect < 1.25f ? new Vector2(0.5f, 1f) : new Vector2(1f, 1f);
            var sidePanelPosition = aspect < 1.25f
                ? new Vector2(0f, -sidePanelTop)
                : new Vector2(-(safeMargins.z + edge), -sidePanelTop);

            SetHudObjectRect(bookDetailPanel, sidePanelAnchor, sidePanelPivot, sidePanelPosition, new Vector2(sidePanelWidth, detailPanelHeight));
            SetHudObjectRect(bookReportReadPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(writePanelWidth, writePanelHeight));
            SetHudObjectRect(bookReportWritePanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(writePanelWidth, writePanelHeight));
            SetHudObjectRect(bookReviewWritePanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(writePanelWidth, writePanelHeight));
            SetHudObjectRect(bookEditPanel, sidePanelAnchor, sidePanelPivot, sidePanelPosition, new Vector2(sidePanelWidth, detailPanelHeight));
            SetHudObjectRect(sectionEditPanel, sidePanelAnchor, sidePanelPivot, sidePanelPosition, new Vector2(sidePanelWidth, detailPanelHeight));

            ApplyBookDetailPanelResponsiveLayout(sidePanelWidth, detailPanelHeight);
            ApplyBookReportWritePanelResponsiveLayout(writePanelWidth, writePanelHeight);
            ApplyBookReportReadPanelResponsiveLayout(writePanelWidth, writePanelHeight);
            ApplyBookReviewWritePanelResponsiveLayout(writePanelWidth, writePanelHeight);
            ApplyBookEditPanelResponsiveLayout(sidePanelWidth, detailPanelHeight);
            ApplySectionEditPanelResponsiveLayout(sidePanelWidth, detailPanelHeight);
        }

        private void LayoutBookFocusToolbar(GameObject panel, float panelWidth, float panelHeight, float bottomOffset, bool isCompact)
        {
            if (panel == null)
            {
                return;
            }

            SetHudObjectRect(
                panel,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, bottomOffset),
                new Vector2(panelWidth, panelHeight));

            var padding = Mathf.Clamp(panelWidth * 0.066f, 44f, 64f);
            var gap = Mathf.Clamp(panelWidth * 0.024f, 18f, 28f);
            var buttonWidth = (panelWidth - padding * 2f - gap * 3f) / 4f;
            var buttonHeight = panelHeight - Mathf.Clamp(panelHeight * 0.48f, 54f, 66f);
            var startX = -panelWidth * 0.5f + padding + buttonWidth * 0.5f;
            var fontSize = Mathf.RoundToInt(Mathf.Clamp(panelHeight * 0.15f, 14f, 18f));

            var detailButtonX = startX;
            var ratingButtonX = startX + (buttonWidth + gap);
            var likeButtonX = startX + (buttonWidth + gap) * 2f;
            var placementButtonX = startX + (buttonWidth + gap) * 3f;
            LayoutToolbarButton(bookFocusDetailButton, detailButtonX, buttonWidth, buttonHeight, fontSize);
            LayoutToolbarButton(bookFocusRatingButton, ratingButtonX, buttonWidth, buttonHeight, fontSize);
            LayoutToolbarButton(bookFocusReportButton, likeButtonX, buttonWidth, buttonHeight, fontSize);
            LayoutToolbarButton(openBookPlacementEditorButton, placementButtonX, buttonWidth, buttonHeight, fontSize);

            if (bookFocusRatingText != null)
            {
                SetHudComponentRect(
                    bookFocusRatingText,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -Mathf.Clamp(panelHeight * 0.12f, 12f, 18f)),
                    new Vector2(panelWidth - padding * 2f, Mathf.Clamp(panelHeight * 0.22f, 20f, 28f)));
                bookFocusRatingText.fontSize = Mathf.RoundToInt(Mathf.Clamp(panelHeight * 0.13f, 13f, 17f));
                bookFocusRatingText.alignment = TextAnchor.MiddleCenter;
            }

        }

        private void LayoutBookPlacementToolbar(GameObject panel, float panelWidth, float panelHeight, float bottomOffset, Vector2 canvasSize)
        {
            if (panel == null)
            {
                return;
            }

            var anchoredPosition = new Vector2(0f, bottomOffset);
            var anchor = new Vector2(0.5f, 0f);
            var pivot = new Vector2(0.5f, 0f);
            if (TryResolveBookAnchoredHudPoint(placementEditBook, canvasSize, out var underBookPosition))
            {
                anchor = new Vector2(0.5f, 0.5f);
                pivot = new Vector2(0.5f, 1f);
                underBookPosition.y -= Mathf.Clamp(panelHeight * 0.14f, 8f, 12f);
                underBookPosition.x = Mathf.Clamp(underBookPosition.x, -canvasSize.x * 0.5f + panelWidth * 0.5f + 18f, canvasSize.x * 0.5f - panelWidth * 0.5f - 18f);
                underBookPosition.y = Mathf.Clamp(underBookPosition.y, -canvasSize.y * 0.5f + panelHeight + 18f, canvasSize.y * 0.5f - 24f);
                anchoredPosition = underBookPosition;
            }

            SetHudObjectRect(
                panel,
                anchor,
                pivot,
                anchoredPosition,
                new Vector2(panelWidth, panelHeight));

            var padding = Mathf.Clamp(panelWidth * 0.036f, 10f, 16f);
            var gap = Mathf.Clamp(panelWidth * 0.014f, 5f, 8f);
            var buttonWidth = (panelWidth - padding * 2f - gap * 4f) / 5f;
            var buttonHeight = Mathf.Max(34f, panelHeight - Mathf.Clamp(panelHeight * 0.20f, 10f, 14f));
            var startX = -panelWidth * 0.5f + padding + buttonWidth * 0.5f;
            var fontSize = Mathf.RoundToInt(Mathf.Clamp(panelHeight * 0.23f, 12f, 15f));

            LayoutToolbarButton(placementSpineButton, startX, buttonWidth, buttonHeight, fontSize);
            LayoutToolbarButton(placementAngledButton, startX + (buttonWidth + gap), buttonWidth, buttonHeight, fontSize);
            LayoutToolbarButton(placementFrontButton, startX + (buttonWidth + gap) * 2f, buttonWidth, buttonHeight, fontSize);
            LayoutToolbarButton(cancelPlacementButton, startX + (buttonWidth + gap) * 3f, buttonWidth, buttonHeight, fontSize);
            LayoutToolbarButton(confirmPlacementButton, startX + (buttonWidth + gap) * 4f, buttonWidth, buttonHeight, fontSize);
        }

        private static void LayoutToolbarButton(Button button, float x, float width, float height, int fontSize)
        {
            if (button == null)
            {
                return;
            }

            SetHudComponentRect(
                button,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(x, 0f),
                new Vector2(width, height));

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontSize = fontSize;
                label.alignment = TextAnchor.MiddleCenter;
                StretchTextToParent(label);
            }
        }

        private static void LayoutBookshelfSelectorButtons(Button[] buttons, float x, float y, float width, float height, float preferredButtonWidth)
        {
            if (buttons == null || buttons.Length == 0)
            {
                return;
            }

            const float gap = 8f;
            var visibleCount = 0;
            for (var i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].gameObject.activeSelf)
                {
                    visibleCount++;
                }
            }

            if (visibleCount <= 0)
            {
                return;
            }

            var buttonWidth = Mathf.Min(preferredButtonWidth, (width - gap * (visibleCount - 1)) / visibleCount);
            buttonWidth = Mathf.Max(62f, buttonWidth);
            var cursorX = x;
            for (var i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button == null || !button.gameObject.activeSelf)
                {
                    continue;
                }

                SetHudComponentRect(
                    button,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(cursorX, y),
                    new Vector2(buttonWidth, height));
                cursorX += buttonWidth + gap;
            }
        }

        private void ApplyBookDetailPanelResponsiveLayout(float panelWidth, float panelHeight)
        {
            bookDetailPanelController?.ApplyResponsiveLayout(panelWidth, panelHeight);
        }

        private void ApplyBookReportWritePanelResponsiveLayout(float panelWidth, float panelHeight)
        {
            var padding = Mathf.Clamp(panelWidth * 0.065f, 28f, 40f);
            var contentWidth = Mathf.Max(320f, panelWidth - padding * 2f);
            var buttonHeight = 38f;
            var bottomButtonY = -(panelHeight - padding - OverlayButtonBottomPaddingExtra - buttonHeight);
            var visibilityY = bottomButtonY + 74f;
            var bodyY = -174f;
            var bodyHeight = Mathf.Max(190f, -visibilityY - Mathf.Abs(bodyY) - 30f);
            var toggleGap = 14f;
            var toggleWidth = Mathf.Min(160f, (contentWidth - toggleGap * 2f) / 3f);
            var cancelWidth = 104f;
            var saveWidth = 116f;
            var actionGap = 12f;
            var cancelX = padding + contentWidth - cancelWidth;
            var saveX = cancelX - actionGap - saveWidth;

            SetHudChildRect(bookReportWritePanel, "BookReportWriteAccent", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -24f), new Vector2(90f, 3f));
            SetHudChildActive(bookReportWritePanel, "BookReportWriteAccent", false);
            SetHudComponentRect(bookReportWriteTitleText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -38f), new Vector2(contentWidth, 26f));
            SetHudComponentRect(bookReportWriteBookText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -72f), new Vector2(contentWidth, 34f));
            SetHudComponentRect(bookReportTitleInputField, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -120f), new Vector2(contentWidth, 42f));
            SetHudComponentRect(bookReportBodyInputField, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, bodyY), new Vector2(contentWidth, bodyHeight));
            SetHudComponentRect(bookReportPrivateButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, visibilityY), new Vector2(toggleWidth, 36f));
            SetHudComponentRect(bookReportPublicButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + toggleWidth + toggleGap, visibilityY), new Vector2(toggleWidth, 36f));
            SetHudComponentRect(bookReportFriendsButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + (toggleWidth + toggleGap) * 2f, visibilityY), new Vector2(toggleWidth, 36f));
            SetHudComponentRect(saveBookReportButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(saveX, bottomButtonY), new Vector2(saveWidth, buttonHeight));
            SetHudComponentRect(cancelBookReportButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(cancelX, bottomButtonY), new Vector2(cancelWidth, buttonHeight));

            if (bookReportBodyInputField != null)
            {
                if (bookReportBodyInputField.textComponent != null)
                {
                    bookReportBodyInputField.textComponent.alignment = TextAnchor.UpperLeft;
                }

                if (bookReportBodyInputField.placeholder is Text placeholderText)
                {
                    placeholderText.alignment = TextAnchor.UpperLeft;
                }
            }
        }

        private void ApplyBookReviewWritePanelResponsiveLayout(float panelWidth, float panelHeight)
        {
            var padding = Mathf.Clamp(panelWidth * 0.065f, 28f, 40f);
            var contentWidth = Mathf.Max(320f, panelWidth - padding * 2f);
            var buttonHeight = 38f;
            var bottomButtonY = -(panelHeight - padding - OverlayButtonBottomPaddingExtra - buttonHeight);
            var ratingButtonGap = Mathf.Clamp(contentWidth * 0.018f, 8f, 14f);
            var ratingButtonWidth = Mathf.Min(128f, (contentWidth - ratingButtonGap * 4f) / 5f);
            var ratingButtonHeight = 38f;
            var ratingRowOneY = -154f;
            var ratingRowTwoY = -200f;
            var bodyY = -264f;
            var statusY = bottomButtonY + 2f;
            var bodyHeight = Mathf.Max(150f, Mathf.Abs(statusY) - Mathf.Abs(bodyY) - 18f);
            var cancelWidth = 104f;
            var saveWidth = 116f;
            var actionGap = 12f;
            var cancelX = padding + contentWidth - cancelWidth;
            var saveX = cancelX - actionGap - saveWidth;
            var statusWidth = Mathf.Max(220f, saveX - padding - actionGap);

            SetHudChildRect(bookReviewWritePanel, "BookReviewWriteAccent", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -24f), new Vector2(90f, 3f));
            SetHudChildActive(bookReviewWritePanel, "BookReviewWriteAccent", false);
            SetHudComponentRect(bookReviewWriteTitleText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -38f), new Vector2(contentWidth, 26f));
            SetHudComponentRect(bookReviewWriteBookText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -72f), new Vector2(contentWidth, 34f));
            SetHudComponentRect(bookReviewRatingText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -118f), new Vector2(contentWidth, 24f));

            for (var i = 0; i < bookReviewRatingButtons.Length; i++)
            {
                var row = i / 5;
                var column = i % 5;
                SetHudComponentRect(
                    bookReviewRatingButtons[i],
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(padding + (ratingButtonWidth + ratingButtonGap) * column, row == 0 ? ratingRowOneY : ratingRowTwoY),
                    new Vector2(ratingButtonWidth, ratingButtonHeight));
            }

            SetHudComponentRect(bookReviewContentInputField, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, bodyY), new Vector2(contentWidth, bodyHeight));
            SetHudComponentRect(bookReviewStatusText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, statusY), new Vector2(statusWidth, 40f));
            SetHudComponentRect(saveBookReviewButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(saveX, bottomButtonY), new Vector2(saveWidth, buttonHeight));
            SetHudComponentRect(cancelBookReviewButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(cancelX, bottomButtonY), new Vector2(cancelWidth, buttonHeight));

            if (bookReviewContentInputField != null)
            {
                if (bookReviewContentInputField.textComponent != null)
                {
                    bookReviewContentInputField.textComponent.alignment = TextAnchor.UpperLeft;
                }

                if (bookReviewContentInputField.placeholder is Text placeholderText)
                {
                    placeholderText.alignment = TextAnchor.UpperLeft;
                }
            }
        }

        private void ApplyBookReportReadPanelResponsiveLayout(float panelWidth, float panelHeight)
        {
            var padding = Mathf.Clamp(panelWidth * 0.065f, 28f, 40f);
            var contentWidth = Mathf.Max(320f, panelWidth - padding * 2f);
            var buttonHeight = 36f;
            var buttonY = -(panelHeight - padding - OverlayButtonBottomPaddingExtra - buttonHeight);
            var bodyY = -164f;
            var commentY = buttonY + buttonHeight + 12f;
            var socialY = commentY + 30f;
            var commentsTitleHeight = 20f;
            var commentsGap = 12f;
            var commentsMinHeight = 76f;
            var contentBottomY = socialY + 16f;
            var contentSpan = Mathf.Max(240f, bodyY - contentBottomY);
            var maxBodyHeight = Mathf.Max(110f, contentSpan - commentsTitleHeight - commentsGap * 2f - commentsMinHeight);
            var bodyHeight = Mathf.Min(Mathf.Clamp(contentSpan * 0.54f, 126f, 260f), maxBodyHeight);
            var commentsTitleY = bodyY - bodyHeight - commentsGap;
            var commentsTextY = commentsTitleY - commentsTitleHeight;
            var commentsHeight = Mathf.Max(commentsMinHeight, commentsTextY - contentBottomY);
            var closeWidth = 92f;
            var editWidth = 90f;
            var visibilityWidth = 126f;
            var deleteWidth = 82f;
            var commentButtonWidth = 56f;
            var likeWidth = 62f;
            var gap = 12f;

            SetHudChildRect(bookReportReadPanel, "BookReportReadAccent", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -24f), new Vector2(90f, 3f));
            SetHudChildActive(bookReportReadPanel, "BookReportReadAccent", false);
            SetHudComponentRect(bookReportReadTitleText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -40f), new Vector2(contentWidth, 56f));
            SetHudComponentRect(bookReportReadMetaText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -106f), new Vector2(contentWidth, 28f));
            SetHudChildRect(bookReportReadPanel, "BookReportReadDivider", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -144f), new Vector2(contentWidth, 1f));
            SetHudChildActive(bookReportReadPanel, "BookReportReadDivider", false);
            SetHudComponentRect(bookReportReadBodyText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, bodyY), new Vector2(contentWidth, bodyHeight));
            SetHudComponentRect(bookReportCommentsTitleText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, commentsTitleY), new Vector2(contentWidth, commentsTitleHeight));
            SetHudComponentRect(bookReportCommentsText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, commentsTextY), new Vector2(contentWidth, commentsHeight));
            SetHudComponentRect(bookReportSocialText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, socialY), new Vector2(contentWidth, 24f));
            SetHudComponentRect(bookReportCommentInputField, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, commentY), new Vector2(contentWidth - likeWidth - commentButtonWidth - gap * 2f, 34f));
            SetHudComponentRect(bookReportLikeButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + contentWidth - likeWidth - commentButtonWidth - gap, commentY), new Vector2(likeWidth, 34f));
            SetHudComponentRect(bookReportCommentButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + contentWidth - commentButtonWidth, commentY), new Vector2(commentButtonWidth, 34f));
            SetHudComponentRect(editBookReportButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, buttonY), new Vector2(editWidth, buttonHeight));
            SetHudComponentRect(toggleReportVisibilityButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + editWidth + gap, buttonY), new Vector2(visibilityWidth, buttonHeight));
            SetHudComponentRect(deleteBookReportButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + editWidth + visibilityWidth + gap * 2f, buttonY), new Vector2(deleteWidth, buttonHeight));
            SetHudComponentRect(closeBookReportReadButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + contentWidth - closeWidth, buttonY), new Vector2(closeWidth, buttonHeight));
        }

        private void ApplyBookEditPanelResponsiveLayout(float panelWidth, float panelHeight)
        {
            var padding = Mathf.Clamp(panelWidth * 0.058f, 26f, 36f);
            var contentWidth = Mathf.Max(320f, panelWidth - padding * 2f);
            var buttonHeight = 36f;
            var closeY = -(panelHeight - padding - OverlayButtonBottomPaddingExtra - buttonHeight);
            var deleteY = closeY + 48f;

            SetHudChildRect(bookEditPanel, "BookEditAccent", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -22f), new Vector2(84f, 3f));
            SetHudChildActive(bookEditPanel, "BookEditAccent", false);
            SetHudComponentRect(bookEditEyebrowText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -30f), new Vector2(contentWidth, 18f));
            SetHudComponentRect(bookEditTitleText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -56f), new Vector2(contentWidth, 56f));
            SetHudComponentRect(bookEditMetaText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -126f), new Vector2(contentWidth, 82f));
            SetHudChildRect(bookEditPanel, "BookEditDivider", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -220f), new Vector2(contentWidth, 1f));
            SetHudChildActive(bookEditPanel, "BookEditDivider", false);
            SetHudComponentRect(bookEditHintText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -238f), new Vector2(contentWidth, 58f));
            SetHudComponentRect(deleteBookButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, deleteY), new Vector2(contentWidth, buttonHeight));
            SetHudComponentRect(closeBookEditButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, closeY), new Vector2(contentWidth, buttonHeight));
        }

        private void ApplySectionEditPanelResponsiveLayout(float panelWidth, float panelHeight)
        {
            var padding = Mathf.Clamp(panelWidth * 0.058f, 26f, 36f);
            var contentWidth = Mathf.Max(320f, panelWidth - padding * 2f);
            var buttonHeight = 36f;
            var closeY = -(panelHeight - padding - OverlayButtonBottomPaddingExtra - buttonHeight);
            var alignY = closeY + 72f;
            var gap = 12f;
            var alignWidth = (contentWidth - gap * 2f) / 3f;

            SetHudChildRect(sectionEditPanel, "SectionEditAccent", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -22f), new Vector2(84f, 3f));
            SetHudChildActive(sectionEditPanel, "SectionEditAccent", false);
            SetHudComponentRect(sectionEditEyebrowText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -30f), new Vector2(contentWidth, 18f));
            SetHudComponentRect(sectionEditTitleText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -56f), new Vector2(contentWidth, 56f));
            SetHudComponentRect(sectionEditMetaText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -126f), new Vector2(contentWidth, 96f));
            SetHudChildRect(sectionEditPanel, "SectionEditDivider", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -232f), new Vector2(contentWidth, 1f));
            SetHudChildActive(sectionEditPanel, "SectionEditDivider", false);
            SetHudComponentRect(sectionEditHintText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -250f), new Vector2(contentWidth, 58f));
            SetHudComponentRect(alignLeftButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, alignY), new Vector2(alignWidth, buttonHeight));
            SetHudComponentRect(alignCenterButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + alignWidth + gap, alignY), new Vector2(alignWidth, buttonHeight));
            SetHudComponentRect(alignRightButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + (alignWidth + gap) * 2f, alignY), new Vector2(alignWidth, buttonHeight));
            SetHudComponentRect(closeSectionEditButton, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, closeY), new Vector2(contentWidth, buttonHeight));
        }

        private Vector2 GetHudCanvasSize()
        {
            var rect = hudCanvas != null ? hudCanvas.transform as RectTransform : null;
            if (rect != null && rect.rect.width > 1f && rect.rect.height > 1f)
            {
                return rect.rect.size;
            }

            return new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
        }

        private static bool TryResolveBookAnchoredHudPoint(BookEntity book, Vector2 canvasSize, out Vector2 anchoredPosition)
        {
            anchoredPosition = Vector2.zero;
            var sceneCamera = Camera.main;
            if (book == null || sceneCamera == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return false;
            }

            var basePosition = book.StoredScale != Vector3.zero ? book.StoredPosition : book.transform.position;
            var worldPoint = basePosition - Vector3.up * Mathf.Max(0.08f, book.height * 0.62f);
            var screenPoint = sceneCamera.WorldToScreenPoint(worldPoint);
            if (screenPoint.z <= 0f)
            {
                return false;
            }

            anchoredPosition = new Vector2(
                screenPoint.x / Screen.width * canvasSize.x - canvasSize.x * 0.5f,
                screenPoint.y / Screen.height * canvasSize.y - canvasSize.y * 0.5f);
            return true;
        }

        private bool TryResolveActiveBookshelfHudBounds(Vector2 canvasSize, out Rect hudBounds)
        {
            hudBounds = default;
            var sceneCamera = Camera.main;
            if (bookshelfGenerator == null || bookshelfGenerator.BooksRoot == null || sceneCamera == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return false;
            }

            var shelfRoot = bookshelfGenerator.BooksRoot.parent;
            if (shelfRoot == null)
            {
                return false;
            }

            var renderers = shelfRoot.GetComponentsInChildren<Renderer>(true);
            var hasBounds = false;
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                var bounds = renderer.bounds;
                var extents = bounds.extents;
                var center = bounds.center;
                for (var x = -1; x <= 1; x += 2)
                {
                    for (var y = -1; y <= 1; y += 2)
                    {
                        for (var z = -1; z <= 1; z += 2)
                        {
                            var world = center + Vector3.Scale(extents, new Vector3(x, y, z));
                            var screen = sceneCamera.WorldToScreenPoint(world);
                            if (screen.z <= 0f)
                            {
                                continue;
                            }

                            var hud = new Vector2(screen.x / Screen.width * canvasSize.x, screen.y / Screen.height * canvasSize.y);
                            min = Vector2.Min(min, hud);
                            max = Vector2.Max(max, hud);
                            hasBounds = true;
                        }
                    }
                }
            }

            if (!hasBounds || max.x <= min.x || max.y <= min.y)
            {
                return false;
            }

            hudBounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        private bool TryResolveActiveBookshelfNavigationHudPoints(Vector2 canvasSize, out Vector2 previousPoint, out Vector2 nextPoint)
        {
            previousPoint = Vector2.zero;
            nextPoint = Vector2.zero;
            var sceneCamera = Camera.main;
            if (bookshelfGenerator == null ||
                sceneCamera == null ||
                Screen.width <= 0 ||
                Screen.height <= 0 ||
                !bookshelfGenerator.TryGetBookshelfNavigationAnchors(out var previousAnchor, out var nextAnchor))
            {
                return false;
            }

            return TryProjectWorldToHudPoint(sceneCamera, previousAnchor, canvasSize, out previousPoint) &&
                   TryProjectWorldToHudPoint(sceneCamera, nextAnchor, canvasSize, out nextPoint);
        }

        private static bool TryProjectWorldToHudPoint(Camera sceneCamera, Vector3 worldPoint, Vector2 canvasSize, out Vector2 hudPoint)
        {
            hudPoint = Vector2.zero;
            var screenPoint = sceneCamera.WorldToScreenPoint(worldPoint);
            if (screenPoint.z <= 0f || Screen.width <= 0 || Screen.height <= 0)
            {
                return false;
            }

            hudPoint = new Vector2(
                screenPoint.x / Screen.width * canvasSize.x,
                screenPoint.y / Screen.height * canvasSize.y);
            return true;
        }

        private static Vector2 ClampBookshelfNavigationPoint(Vector2 point, Vector2 canvasSize, Vector4 safeMargins, float edge, float buttonSize)
        {
            var half = buttonSize * 0.5f;
            return new Vector2(
                Mathf.Clamp(point.x, safeMargins.x + edge + half, canvasSize.x - safeMargins.z - edge - half),
                Mathf.Clamp(point.y, safeMargins.y + edge + half, canvasSize.y - safeMargins.w - edge - half));
        }

        private static Vector4 GetHudSafeMargins(Vector2 canvasSize)
        {
            var safeArea = Screen.safeArea;
            if (safeArea.width <= 0f || safeArea.height <= 0f || Screen.width <= 0 || Screen.height <= 0)
            {
                return Vector4.zero;
            }

            var xScale = canvasSize.x / Screen.width;
            var yScale = canvasSize.y / Screen.height;
            return new Vector4(
                safeArea.xMin * xScale,
                safeArea.yMin * yScale,
                (Screen.width - safeArea.xMax) * xScale,
                (Screen.height - safeArea.yMax) * yScale);
        }

        private static void SetHudObjectRect(GameObject target, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            RuntimeHudLayout.SetObjectRect(target, anchor, pivot, anchoredPosition, size);
        }

        private static void SetHudComponentRect(Component target, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            if (target == null)
            {
                return;
            }

            RuntimeHudLayout.SetComponentRect(target, anchor, pivot, anchoredPosition, size);
            if (target is Button button)
            {
                var label = GetButtonLabelText(button);
                if (label != null)
                {
                    StretchTextToParent(label);
                }
            }
            else if (target is InputField inputField)
            {
                ApplyHudInputTextPadding(inputField);
            }
        }

        private static void SetHudChildRect(GameObject parent, string childName, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            RuntimeHudLayout.SetChildRect(parent, childName, anchor, pivot, anchoredPosition, size);
        }

        private static void SetHudChildActive(GameObject parent, string childName, bool isActive)
        {
            RuntimeHudLayout.SetChildActive(parent, childName, isActive);
        }

        private static void SetHudRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            RuntimeHudLayout.SetRect(rect, anchor, pivot, anchoredPosition, size);
        }

        private static void StretchTextToParent(Text text)
        {
            RuntimeHudLayout.InsetTextToParent(text, 10f, 3f);
        }

        private void RefreshBookshelfOverlay()
        {
            var isBookshelfFocused = CurrentFocusArea == ExperienceFocusArea.Bookshelf;
            var isBookshelfSwitching = bookshelfGenerator != null && bookshelfGenerator.IsBookshelfSwitching;
            var isViewingBook = state != null && state.IsViewingBook;
            var isDraggingBook = state != null && state.ActiveDraggedBook != null;
            var isPlacementEditing = isBookPlacementEditorOpen && placementEditBook != null;
            var showBookshelfOverlay = isBookshelfFocused && !isViewingBook && !isPlacementEditing;
            var canSwitchBookshelf = bookshelfGenerator != null &&
                                     bookshelfGenerator.BookshelfCount > 1 &&
                                     !isBookshelfSwitching &&
                                     !IsBookshelfEditMode &&
                                     !isDraggingBook &&
                                     !isPlacementEditing &&
                                     !isBookAddPlacementSelectionActive;
            if (overlayPreviousFocusButton != null)
            {
                overlayPreviousFocusButton.gameObject.SetActive(showBookshelfOverlay);
                overlayPreviousFocusButton.interactable = canSwitchBookshelf;
                StyleBookshelfNavigationButton(overlayPreviousFocusButton, canSwitchBookshelf);
            }

            if (overlayNextFocusButton != null)
            {
                overlayNextFocusButton.gameObject.SetActive(showBookshelfOverlay);
                overlayNextFocusButton.interactable = canSwitchBookshelf;
                StyleBookshelfNavigationButton(overlayNextFocusButton, canSwitchBookshelf);
            }

            if (bookshelfEditModeButton != null)
            {
                var canToggleEdit = isBookshelfFocused && !isViewingBook && !isBookshelfSwitching && !isDraggingBook && !isPlacementEditing;
                bookshelfEditModeButton.gameObject.SetActive(showBookshelfOverlay && IsBookshelfEditMode);
                bookshelfEditModeButton.interactable = canToggleEdit;
                SetButtonLabel(bookshelfEditModeButton, "저장");
                StyleHudButton(
                    bookshelfEditModeButton,
                    new Color32(255, 248, 236, 235),
                    new Color32(255, 255, 255, 245),
                    new Color32(224, 219, 214, 140),
                    14,
                    new Color32(63, 47, 38, 255),
                    true);
            }

            if (openBookSearchButton != null)
            {
                openBookSearchButton.gameObject.SetActive(false);
            }

            var isBookAddFlowOpen = isBookAddPanelOpen || isBookAddConfirmPanelOpen || isBookAddPlacementSelectionActive;
            RefreshBookshelfBottomNav(showBookshelfOverlay && !isDraggingBook && !isBookshelfSwitching && !isBookAddFlowOpen);
            RefreshBookshelfIndexIndicator(showBookshelfOverlay && !isBookAddFlowOpen);
        }

        private void RefreshBookshelfBottomNav(bool shouldShow)
        {
            if (bookshelfBottomNavPanel == null)
            {
                return;
            }

            bookshelfBottomNavPanel.SetActive(shouldShow);
            if (!shouldShow)
            {
                return;
            }

            var canInteract = CurrentFocusArea == ExperienceFocusArea.Bookshelf &&
                              (state == null || (!state.IsViewingBook && state.ActiveDraggedBook == null)) &&
                              (bookshelfGenerator == null || !bookshelfGenerator.IsBookshelfSwitching);
            var canSearch = canInteract && !isBookPlacementEditorOpen;
            var canMutate = canInteract && !isBookPlacementEditorOpen && CanMutateOwnedBookshelf();
            var canAdd = canMutate && CanUseBookshelfServerPlacement(bookshelfGenerator != null ? bookshelfGenerator.DisplayBookshelfIndex : -1);
            var canEdit = canMutate;

            var activeTab = ResolveBookshelfBottomNavTab();
            StyleBookshelfBottomNavButton(bookshelfNavHomeButton, activeTab == BookshelfBottomNavTab.Home, canInteract, "⌂\n책장");
            StyleBookshelfBottomNavButton(bookshelfNavSearchButton, activeTab == BookshelfBottomNavTab.Search, canSearch, "검색");
            StyleBookshelfBottomNavButton(bookshelfNavAddButton, activeTab == BookshelfBottomNavTab.Add, canAdd, "+\n추가");
            StyleBookshelfBottomNavButton(bookshelfNavEditButton, activeTab == BookshelfBottomNavTab.Edit, canEdit, "✎\n편집");
        }

        private BookshelfBottomNavTab ResolveBookshelfBottomNavTab()
        {
            if (isBookAddPanelOpen || isBookAddConfirmPanelOpen || isBookAddPlacementSelectionActive)
            {
                return BookshelfBottomNavTab.Add;
            }

            if (isBookSearchPanelOpen)
            {
                return BookshelfBottomNavTab.Search;
            }

            return IsBookshelfEditMode ? BookshelfBottomNavTab.Edit : BookshelfBottomNavTab.Home;
        }

        private static void StyleBookshelfBottomNavButton(Button button, bool isActive, bool isInteractable, string label)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = isInteractable;
            var isSearch = label == "검색";
            SetButtonLabel(button, isSearch ? "\n검색" : label);
            StyleHudButton(
                button,
                isActive ? new Color32(197, 128, 67, 238) : new Color32(255, 248, 236, 0),
                isActive ? new Color32(214, 145, 82, 255) : new Color32(255, 255, 255, 82),
                new Color32(224, 219, 214, 52),
                19,
                isActive ? Color.white : new Color32(63, 47, 38, 255),
                true);

            var text = GetButtonLabelText(button);
            if (text != null)
            {
                text.alignment = TextAnchor.MiddleCenter;
                text.lineSpacing = 0.98f;
                text.fontSize = 20;
                StretchTextToParent(text);
            }

            EnsureBottomNavSearchIcon(button, isSearch, isActive ? Color.white : new Color32(63, 47, 38, 255));
        }

        private static void EnsureBottomNavSearchIcon(Button button, bool visible, Color iconColor)
        {
            if (button == null)
            {
                return;
            }

            var iconRoot = button.transform.Find("SearchIconRoot") as RectTransform;
            if (iconRoot == null)
            {
                var rootObject = new GameObject("SearchIconRoot", typeof(RectTransform));
                rootObject.transform.SetParent(button.transform, false);
                iconRoot = rootObject.GetComponent<RectTransform>();
            }

            iconRoot.anchorMin = new Vector2(0.5f, 0.62f);
            iconRoot.anchorMax = new Vector2(0.5f, 0.62f);
            iconRoot.pivot = new Vector2(0.5f, 0.5f);
            iconRoot.anchoredPosition = Vector2.zero;
            iconRoot.sizeDelta = new Vector2(24f, 22f);
            iconRoot.localScale = Vector3.one;
            iconRoot.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            var ringRect = EnsureSearchIconRing(iconRoot);
            ringRect.anchorMin = new Vector2(0.5f, 0.5f);
            ringRect.anchorMax = new Vector2(0.5f, 0.5f);
            ringRect.pivot = new Vector2(0.5f, 0.5f);
            ringRect.anchoredPosition = new Vector2(-2.5f, 2.5f);
            ringRect.sizeDelta = new Vector2(15f, 15f);
            ringRect.localScale = Vector3.one;
            ringRect.localRotation = Quaternion.identity;

            var handleRect = EnsureSearchIconHandle(iconRoot);
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.anchoredPosition = new Vector2(5.8f, -5.8f);
            handleRect.sizeDelta = new Vector2(8f, 2.5f);
            handleRect.localScale = Vector3.one;
            handleRect.localRotation = Quaternion.Euler(0f, 0f, -45f);

            var images = iconRoot.GetComponentsInChildren<Image>(true);
            for (var i = 0; i < images.Length; i++)
            {
                images[i].color = iconColor;
            }

            var labels = iconRoot.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                labels[i].color = iconColor;
            }
        }

        private static RectTransform EnsureSearchIconRing(RectTransform iconRoot)
        {
            var ringRect = iconRoot.Find("SearchIconRing") as RectTransform;
            if (ringRect == null)
            {
                var ringObject = new GameObject("SearchIconRing", typeof(RectTransform));
                ringObject.transform.SetParent(iconRoot, false);
                ringRect = ringObject.GetComponent<RectTransform>();
            }

            var legacyText = ringRect.GetComponent<Text>();
            if (legacyText != null)
            {
                legacyText.enabled = false;
            }

            var ringImage = ringRect.GetComponent<Image>();
            if (ringImage == null)
            {
                ringImage = ringRect.gameObject.AddComponent<Image>();
            }

            ringImage.sprite = GetSearchIconRingSprite();
            ringImage.type = Image.Type.Simple;
            ringImage.preserveAspect = true;
            ringImage.raycastTarget = false;
            return ringRect;
        }

        private static RectTransform EnsureSearchIconHandle(RectTransform iconRoot)
        {
            var handleRect = iconRoot.Find("SearchIconHandle") as RectTransform;
            if (handleRect == null)
            {
                var handleObject = new GameObject("SearchIconHandle", typeof(RectTransform));
                handleObject.transform.SetParent(iconRoot, false);
                handleRect = handleObject.GetComponent<RectTransform>();
            }

            var handle = handleRect.GetComponent<Image>();
            if (handle == null)
            {
                handle = handleRect.gameObject.AddComponent<Image>();
            }

            ApplyRoundedSprite(handle);
            handle.raycastTarget = false;
            return handleRect;
        }

        private void RefreshBookshelfIndexIndicator(bool shouldShow)
        {
            if (bookshelfIndexPanel != null)
            {
                bookshelfIndexPanel.SetActive(shouldShow && bookshelfGenerator != null && bookshelfGenerator.BookshelfCount > 0);
            }

            if (!shouldShow || bookshelfGenerator == null || bookshelfGenerator.BookshelfCount <= 0)
            {
                RefreshBookshelfIndexDots(0, 0);
                return;
            }

            var totalCount = Mathf.Max(1, bookshelfGenerator.BookshelfCount);
            var displayIndex = Mathf.Clamp(bookshelfGenerator.DisplayBookshelfIndex, 0, totalCount - 1);
            if (bookshelfIndexText != null)
            {
                bookshelfIndexText.text = bookshelfGenerator.GetBookshelfName(displayIndex);
            }

            RefreshBookshelfIndexDots(displayIndex, totalCount);
        }

        private void RequestEmptyBookshelfTutorialEntry()
        {
            if (!ShouldEnterEmptyBookshelfTutorial())
            {
                return;
            }

            isEmptyBookshelfTutorialEntryPending = true;
            TryEnterEmptyBookshelfTutorialFocus();
        }

        private void TryEnterEmptyBookshelfTutorialFocus()
        {
            if (!isEmptyBookshelfTutorialEntryPending)
            {
                return;
            }

            if (!ShouldEnterEmptyBookshelfTutorial())
            {
                isEmptyBookshelfTutorialEntryPending = false;
                return;
            }

            if (CurrentFocusArea == ExperienceFocusArea.Bookshelf)
            {
                isEmptyBookshelfTutorialEntryPending = false;
                return;
            }

            if (CurrentFocusArea != ExperienceFocusArea.QuarterView || !CanStartCameraFocus())
            {
                return;
            }

            FocusBookshelf();
        }

        private bool ShouldEnterEmptyBookshelfTutorial()
        {
            // server snapshot 모드에서는 첫 fetch 완료 전에는 BookshelfCount==0 이 "비어있음"이 아닌
            // "아직 모름". 이 시점 튜토리얼/FocusBookshelf 발동을 차단해 race 회피.
            if (bookshelfGenerator != null &&
                bookshelfGenerator.UseServerBookshelfSnapshots &&
                !bookshelfGenerator.HasReceivedServerSnapshot)
            {
                return false;
            }

            return bookshelfGenerator != null &&
                   bookshelfGenerator.BookshelfCount <= 0 &&
                   CanMutateOwnedBookshelf() &&
                   !isFirstBookshelfCreateInProgress &&
                   !isBookAddPlacementSelectionActive &&
                   !isBookSearchPanelOpen &&
                   !isBookPlacementEditorOpen &&
                   !HasOpenBookshelfModal() &&
                   !isDeleteResultDialogOpen &&
                   !isBookReviewWritePanelOpen &&
                   !isBookReportWritePanelOpen &&
                   !isBookReportReadPanelOpen &&
                   activeEditSection == null &&
                   (state == null || (!state.IsViewingBook && state.ActiveDraggedBook == null));
        }

        private void RefreshFirstBookshelfTutorialPanel()
        {
            if (firstBookshelfTutorialPanel == null || firstBookshelfTutorialBackdrop == null)
            {
                return;
            }

            var mode = ResolveFirstBookshelfTutorialMode();
            var shouldShow = mode != FirstBookshelfTutorialMode.None;
            isFirstBookshelfTutorialOpen = shouldShow;
            firstBookshelfTutorialMode = mode;
            firstBookshelfTutorialBackdrop.SetActive(shouldShow);
            firstBookshelfTutorialPanel.SetActive(shouldShow);
            if (!shouldShow)
            {
                return;
            }

            ApplyFirstBookshelfTutorialContent(mode);
            SuppressBookshelfModalBackgroundHud();
            firstBookshelfTutorialBackdrop.transform.SetAsLastSibling();
            firstBookshelfTutorialPanel.transform.SetAsLastSibling();

            var canvasSize = GetHudCanvasSize();
            var panelWidth = Mathf.Clamp(canvasSize.x * 0.48f, 580f, 760f);
            var panelHeight = Mathf.Clamp(canvasSize.y * 0.40f, 340f, 430f);
            SetHudObjectRect(
                firstBookshelfTutorialPanel,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(panelWidth, panelHeight));

            var padding = Mathf.Clamp(panelWidth * 0.06f, 34f, 48f);
            SetHudComponentRect(
                firstBookshelfTutorialTitleText,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(padding, -padding),
                new Vector2(panelWidth - padding * 2f, 40f));
            SetHudComponentRect(
                firstBookshelfTutorialMessageText,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(padding, -(padding + 56f)),
                new Vector2(panelWidth - padding * 2f, 86f));
            SetHudComponentRect(
                firstBookshelfTutorialStepsText,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(padding, -(padding + 160f)),
                new Vector2(panelWidth - padding * 2f, 94f));
            SetHudComponentRect(
                firstBookshelfTutorialAddButton,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-padding, padding),
                new Vector2(Mathf.Clamp(panelWidth * 0.30f, 178f, 220f), 52f));

            StyleHudButton(
                firstBookshelfTutorialAddButton,
                new Color32(78, 105, 83, 245),
                new Color32(92, 121, 96, 255),
                new Color32(224, 219, 214, 140),
                17,
                Color.white,
                true);
        }

        private FirstBookshelfTutorialMode ResolveFirstBookshelfTutorialMode()
        {
            if (!CanShowFirstBookshelfTutorialOverCurrentUi())
            {
                return FirstBookshelfTutorialMode.None;
            }

            if (ShouldShowFirstBookshelfFeatureGuide())
            {
                return FirstBookshelfTutorialMode.FeatureGuide;
            }

            if (ShouldEnterEmptyBookshelfTutorial() &&
                CurrentFocusArea == ExperienceFocusArea.Bookshelf)
            {
                return FirstBookshelfTutorialMode.CreateFirstShelf;
            }

            return FirstBookshelfTutorialMode.None;
        }

        private bool CanShowFirstBookshelfTutorialOverCurrentUi()
        {
            return !isBookshelfNameEditPanelOpen &&
                   !isBookshelfDeleteConfirmPanelOpen &&
                   !isBookshelfDesignPanelOpen &&
                   !isBookAddPanelOpen &&
                   !isBookAddConfirmPanelOpen &&
                   !isBookshelfManagePanelOpen &&
                   !isBookshelfUnsavedConfirmPanelOpen &&
                   !isDeleteResultDialogOpen &&
                   !isBookSearchPanelOpen &&
                   !isBookAddPlacementSelectionActive &&
                   !isBookEditPanelOpen &&
                   !isBookPlacementEditorOpen &&
                   !isBookReportWritePanelOpen &&
                   !isBookReportReadPanelOpen &&
                   !isBookReviewWritePanelOpen &&
                   activeEditSection == null;
        }

        private bool ShouldShowFirstBookshelfFeatureGuide()
        {
            return pendingFirstBookshelfFeatureGuide &&
                   !hasCompletedFirstBookshelfFeatureGuide &&
                   bookshelfGenerator != null &&
                   bookshelfGenerator.BookshelfCount > 0;
        }

        private void ApplyFirstBookshelfTutorialContent(FirstBookshelfTutorialMode mode)
        {
            if (mode == FirstBookshelfTutorialMode.FeatureGuide)
            {
                var step = Mathf.Clamp(firstBookshelfFeatureGuideStepIndex, 0, 2);
                if (firstBookshelfTutorialTitleText != null)
                {
                    firstBookshelfTutorialTitleText.text = "책장에서 할 수 있는 일";
                }

                if (firstBookshelfTutorialMessageText != null)
                {
                    firstBookshelfTutorialMessageText.text = "첫 책장이 준비되었습니다. 아래 안내를 끝까지 확인한 뒤 책장 사용을 시작할 수 있습니다.";
                }

                if (firstBookshelfTutorialStepsText != null)
                {
                    firstBookshelfTutorialStepsText.text = step switch
                    {
                        0 => "1/3  하단의 추가 버튼으로 책을 검색하고 원하는 칸에 꽂을 수 있습니다.",
                        1 => "2/3  책을 클릭하면 읽음 상태, 리뷰, 좋아요, 독후감을 관리할 수 있습니다.",
                        _ => "3/3  편집에서 책장 이름, 공개 범위, 디자인을 관리할 수 있습니다."
                    };
                }

                if (firstBookshelfTutorialAddButton != null)
                {
                    firstBookshelfTutorialAddButton.onClick.RemoveAllListeners();
                    firstBookshelfTutorialAddButton.onClick.AddListener(AdvanceFirstBookshelfFeatureGuide);
                    SetButtonLabel(firstBookshelfTutorialAddButton, step < 2 ? "다음" : "시작하기");
                }

                return;
            }

            if (firstBookshelfTutorialTitleText != null)
            {
                firstBookshelfTutorialTitleText.text = "첫 책장을 만들어볼까요?";
            }

            if (firstBookshelfTutorialMessageText != null)
            {
                firstBookshelfTutorialMessageText.text = "아직 책장이 없습니다. 책장은 책을 전시하고 정리하는 기본 공간입니다. 먼저 책장 하나를 만들고 이름을 정해보세요.";
            }

            if (firstBookshelfTutorialStepsText != null)
            {
                firstBookshelfTutorialStepsText.text = "1. 책장 추가\n2. 책장 이름 정하기\n3. 검색 또는 추가에서 첫 책 꽂기";
            }

            if (firstBookshelfTutorialAddButton != null)
            {
                firstBookshelfTutorialAddButton.onClick.RemoveAllListeners();
                firstBookshelfTutorialAddButton.onClick.AddListener(AddFirstBookshelfFromTutorial);
                SetButtonLabel(firstBookshelfTutorialAddButton, "책장 추가하기");
            }
        }

        private void QueueFirstBookshelfFeatureGuide()
        {
            if (hasCompletedFirstBookshelfFeatureGuide)
            {
                return;
            }

            pendingFirstBookshelfFeatureGuide = true;
            firstBookshelfFeatureGuideStepIndex = 0;
            if (onboardingAutoOpenBookAddRoutine != null)
            {
                StopCoroutine(onboardingAutoOpenBookAddRoutine);
                onboardingAutoOpenBookAddRoutine = null;
            }
        }

        private void AdvanceFirstBookshelfFeatureGuide()
        {
            if (firstBookshelfTutorialMode != FirstBookshelfTutorialMode.FeatureGuide)
            {
                return;
            }

            if (firstBookshelfFeatureGuideStepIndex < 2)
            {
                firstBookshelfFeatureGuideStepIndex++;
            }
            else
            {
                pendingFirstBookshelfFeatureGuide = false;
                hasCompletedFirstBookshelfFeatureGuide = true;
                firstBookshelfFeatureGuideStepIndex = 0;
                firstBookshelfTutorialMode = FirstBookshelfTutorialMode.None;
                isFirstBookshelfTutorialOpen = false;
            }

            RefreshHud();
        }

        private void AddFirstBookshelfFromTutorial()
        {
            if (bookshelfGenerator == null || !CanMutateOwnedBookshelf())
            {
                return;
            }

            isFirstBookshelfTutorialOpen = false;
            QueueFirstBookshelfFeatureGuide();
            isFirstBookshelfCreateInProgress = true;
            if (!IsBookshelfEditMode)
            {
                SetBookshelfEditMode(true);
            }

            AddBookshelf();
            if (bookshelfGenerator.BookshelfCount > 0)
            {
                OpenBookshelfNameEditPanel();
            }
        }

        private void RefreshEmptyBookshelfAddButton()
        {
            if (emptyBookshelfAddButton == null)
            {
                return;
            }

            var shouldShow = ShouldShowEmptyBookshelfAddButton();
            emptyBookshelfAddButton.gameObject.SetActive(shouldShow);
            if (!shouldShow)
            {
                return;
            }

            var canvasSize = GetHudCanvasSize();
            var buttonSize = Mathf.Clamp(Mathf.Min(canvasSize.x, canvasSize.y) * 0.15f, 118f, 172f);
            SetHudComponentRect(
                emptyBookshelfAddButton,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(buttonSize, buttonSize));
            StyleHudButton(
                emptyBookshelfAddButton,
                new Color32(255, 248, 236, 232),
                new Color32(255, 255, 255, 250),
                new Color32(224, 219, 214, 170),
                Mathf.RoundToInt(Mathf.Clamp(buttonSize * 0.18f, 20f, 28f)),
                new Color32(63, 47, 38, 255),
                true);

            var label = GetButtonLabelText(emptyBookshelfAddButton);
            if (label != null)
            {
                label.alignment = TextAnchor.MiddleCenter;
                label.lineSpacing = 1.0f;
                StretchTextToParent(label);
            }
        }

        private bool ShouldShowEmptyBookshelfAddButton()
        {
            return bookshelfGenerator != null &&
                   bookshelfGenerator.BookshelfCount <= 0 &&
                   !isFirstBookshelfCreateInProgress &&
                   CurrentFocusArea == ExperienceFocusArea.Bookshelf &&
                   CanMutateOwnedBookshelf() &&
                   !isFirstBookshelfTutorialOpen &&
                   !isBookshelfNameEditPanelOpen &&
                   !isBookshelfDeleteConfirmPanelOpen &&
                   !isBookshelfDesignPanelOpen &&
                   !isBookAddPanelOpen &&
                   !isBookAddConfirmPanelOpen &&
                   !isBookshelfManagePanelOpen &&
                   !isBookshelfUnsavedConfirmPanelOpen &&
                   !isDeleteResultDialogOpen &&
                   !isBookSearchPanelOpen &&
                   !isBookAddPlacementSelectionActive &&
                   !isBookEditPanelOpen &&
                   !isBookPlacementEditorOpen &&
                   !isBookReportWritePanelOpen &&
                   !isBookReportReadPanelOpen &&
                   !isBookReviewWritePanelOpen &&
                   activeEditSection == null &&
                   (state == null || (!state.IsViewingBook && state.ActiveDraggedBook == null));
        }

        private void AddEmptyBookshelfFromPlaceholder()
        {
            if (isBookshelfCreateRequestInFlight ||
                bookshelfGenerator == null ||
                !CanMutateOwnedBookshelf())
            {
                return;
            }

            QueueFirstBookshelfFeatureGuide();
            isFirstBookshelfCreateInProgress = true;
            if (!IsBookshelfEditMode)
            {
                SetBookshelfEditMode(true);
            }

            AddBookshelf();
            if (bookshelfGenerator.BookshelfCount > 0)
            {
                OpenBookshelfNameEditPanel();
            }
        }

        private void RefreshBookshelfNameEditPanel()
        {
            if (bookshelfNameEditPanel == null ||
                addBookshelfButton == null ||
                openBookshelfNameEditButton == null ||
                openBookshelfDesignButton == null ||
                deleteBookshelfButton == null ||
                openBookAddButton == null ||
                openBookshelfManageButton == null ||
                bookshelfDeleteConfirmPanel == null ||
                bookshelfDesignPanel == null ||
                bookAddPanel == null ||
                bookAddConfirmPanel == null ||
                bookshelfManagePanel == null ||
                bookshelfUnsavedConfirmPanel == null)
            {
                return;
            }

            var isBookshelfFocused = CurrentFocusArea == ExperienceFocusArea.Bookshelf;
            var isViewingBook = state != null && state.IsViewingBook;
            var isDraggingBook = state != null && state.ActiveDraggedBook != null;
            var isBookshelfSwitching = bookshelfGenerator != null && bookshelfGenerator.IsBookshelfSwitching;
            var canShowBookshelfOverlay = isBookshelfFocused &&
                                          !isViewingBook &&
                                          !isDraggingBook &&
                                          !isBookshelfSwitching &&
                                          bookshelfGenerator != null;
            var shouldShowEditControls = IsBookshelfEditMode && canShowBookshelfOverlay && CanMutateOwnedBookshelf();

            var displayIndex = canShowBookshelfOverlay
                ? Mathf.Clamp(bookshelfGenerator.DisplayBookshelfIndex, 0, Mathf.Max(1, bookshelfGenerator.BookshelfCount) - 1)
                : -1;
            var canDeleteBookshelf = shouldShowEditControls && bookshelfGenerator.BookshelfCount > 1;

            addBookshelfButton.gameObject.SetActive(false);
            if (emptyBookshelfAddButton != null)
            {
                emptyBookshelfAddButton.gameObject.SetActive(false);
            }

            openBookAddButton.gameObject.SetActive(false);

            openBookshelfManageButton.gameObject.SetActive(shouldShowEditControls);
            openBookshelfManageButton.interactable = shouldShowEditControls;
            StyleHudButton(
                openBookshelfManageButton,
                new Color32(255, 248, 236, 235),
                new Color32(255, 255, 255, 245),
                new Color32(224, 219, 214, 140),
                14,
                new Color32(63, 47, 38, 255),
                true);

            openBookshelfNameEditButton.gameObject.SetActive(false);

            deleteBookshelfButton.gameObject.SetActive(false);
            deleteBookshelfButton.interactable = canDeleteBookshelf;
            SetButtonLabel(deleteBookshelfButton, canDeleteBookshelf
                ? "책장 삭제"
                : "삭제 불가");

            openBookshelfDesignButton.gameObject.SetActive(shouldShowEditControls);
            openBookshelfDesignButton.interactable = shouldShowEditControls;
            StyleHudButton(
                openBookshelfDesignButton,
                new Color32(255, 248, 236, 235),
                new Color32(255, 255, 255, 245),
                new Color32(224, 219, 214, 140),
                14,
                new Color32(63, 47, 38, 255),
                true);

            var showNamePanel = shouldShowEditControls && isBookshelfNameEditPanelOpen;
            var showDeletePanel = shouldShowEditControls && isBookshelfDeleteConfirmPanelOpen;
            var showDesignPanel = shouldShowEditControls && isBookshelfDesignPanelOpen;
            var showBookAddPanel = canShowBookshelfOverlay && isBookAddPanelOpen;
            var showBookAddConfirmPanel = canShowBookshelfOverlay && isBookAddConfirmPanelOpen;
            var showBookshelfManagePanel = shouldShowEditControls && isBookshelfManagePanelOpen;
            var showUnsavedConfirmPanel = isBookshelfUnsavedConfirmPanelOpen;
            var showModal = showNamePanel || showDeletePanel || showDesignPanel || showBookAddPanel || showBookAddConfirmPanel || showBookshelfManagePanel || showUnsavedConfirmPanel;
            if (showModal)
            {
                SuppressBookshelfModalBackgroundHud();
            }

            if (bookshelfNameModalBackdrop != null)
            {
                bookshelfNameModalBackdrop.SetActive(showModal);
            }

            bookshelfNameEditPanel.SetActive(showNamePanel);
            bookshelfDeleteConfirmPanel.SetActive(showDeletePanel);
            bookshelfDesignPanel.SetActive(showDesignPanel);
            bookAddPanel.SetActive(showBookAddPanel);
            bookAddConfirmPanel.SetActive(showBookAddConfirmPanel);
            bookshelfManagePanel.SetActive(showBookshelfManagePanel);
            bookshelfUnsavedConfirmPanel.SetActive(showUnsavedConfirmPanel);
            if (showUnsavedConfirmPanel)
            {
                StyleHudButton(
                    confirmBookshelfUnsavedSaveButton,
                    new Color32(197, 128, 67, 245),
                    new Color32(214, 145, 82, 255),
                    new Color32(224, 219, 214, 140),
                    14,
                    Color.white,
                    true);
                StyleHudButton(
                    cancelBookshelfUnsavedSaveButton,
                    new Color32(245, 237, 226, 245),
                    new Color32(255, 248, 236, 255),
                    new Color32(224, 219, 214, 140),
                    14,
                    new Color32(84, 74, 66, 255),
                    true);
            }

            if (showModal)
            {
                if (bookshelfNameModalBackdrop != null)
                {
                    bookshelfNameModalBackdrop.transform.SetAsLastSibling();
                }

                if (showUnsavedConfirmPanel)
                {
                    bookshelfUnsavedConfirmPanel.transform.SetAsLastSibling();
                }
                else if (showNamePanel)
                {
                    bookshelfNameEditPanel.transform.SetAsLastSibling();
                }
                else if (showDeletePanel)
                {
                    bookshelfDeleteConfirmPanel.transform.SetAsLastSibling();
                }
                else if (showDesignPanel)
                {
                    bookshelfDesignPanel.transform.SetAsLastSibling();
                }
                else if (showBookAddPanel)
                {
                    bookAddPanel.transform.SetAsLastSibling();
                }
                else if (showBookAddConfirmPanel)
                {
                    bookAddConfirmPanel.transform.SetAsLastSibling();
                }
                else
                {
                    bookshelfManagePanel.transform.SetAsLastSibling();
                }

            }

            if (!canShowBookshelfOverlay || bookshelfGenerator == null)
            {
                SetBookshelfOverlayMode(BookshelfOverlayMode.None);
                return;
            }

            if (!shouldShowEditControls)
            {
                bookshelfNameInputIndex = -1;
                pendingDeleteBookshelfIndex = -1;
                isBookshelfNameEditPanelOpen = false;
                isBookshelfDeleteConfirmPanelOpen = false;
                isBookshelfDesignPanelOpen = false;
                isBookshelfManagePanelOpen = false;
            }
            else
            {
                RefreshBookshelfDeleteConfirmPanel(displayIndex);
                RefreshBookshelfDesignPanel(displayIndex);
                RefreshBookshelfManagePanel();
            }

            RefreshBookAddPanel(displayIndex);
            RefreshBookAddConfirmPanel();

            if (!showNamePanel)
            {
                return;
            }

            if (bookshelfNameInputField != null &&
                (!bookshelfNameInputField.isFocused || bookshelfNameInputIndex != displayIndex))
            {
                bookshelfNameInputField.text = bookshelfGenerator.GetBookshelfName(displayIndex);
                bookshelfNameInputIndex = displayIndex;
            }

            if (bookshelfNameEditLabelText != null)
            {
                bookshelfNameEditLabelText.text = $"{displayIndex + 1}번 책장 이름";
            }

            if (saveBookshelfNameButton != null)
            {
                StyleHudButton(
                    saveBookshelfNameButton,
                    new Color32(197, 128, 67, 235),
                    new Color32(214, 145, 82, 255),
                    new Color32(224, 219, 214, 140),
                    14,
                    Color.white,
                    true);
            }

            if (closeBookshelfNameEditButton != null)
            {
                StyleHudButton(
                    closeBookshelfNameEditButton,
                    new Color32(245, 237, 226, 245),
                    new Color32(255, 248, 236, 255),
                    new Color32(224, 219, 214, 140),
                    16,
                    new Color32(84, 74, 66, 255),
                    true);
            }
        }

        private void SuppressBookshelfModalBackgroundHud()
        {
            if (bookshelfBottomNavPanel != null)
            {
                bookshelfBottomNavPanel.SetActive(false);
            }

            if (bookshelfIndexPanel != null)
            {
                bookshelfIndexPanel.SetActive(false);
            }

            if (focusBackButton != null)
            {
                focusBackButton.gameObject.SetActive(false);
            }

            if (bookshelfEditModeButton != null)
            {
                bookshelfEditModeButton.gameObject.SetActive(false);
            }

            if (openBookshelfManageButton != null)
            {
                openBookshelfManageButton.gameObject.SetActive(false);
            }

            if (openBookshelfDesignButton != null)
            {
                openBookshelfDesignButton.gameObject.SetActive(false);
            }

            if (emptyBookshelfAddButton != null)
            {
                emptyBookshelfAddButton.gameObject.SetActive(false);
            }
        }

        private void OpenBookshelfNameEditPanel()
        {
            if (!IsBookshelfEditMode ||
                bookshelfGenerator == null ||
                bookshelfNameInputField == null ||
                bookshelfGenerator.IsBookshelfSwitching)
            {
                return;
            }

            var displayIndex = Mathf.Clamp(bookshelfGenerator.DisplayBookshelfIndex, 0, Mathf.Max(1, bookshelfGenerator.BookshelfCount) - 1);
            SetBookshelfOverlayMode(BookshelfOverlayMode.NameEdit);
            bookshelfNameInputField.text = bookshelfGenerator.GetBookshelfName(displayIndex);
            bookshelfNameInputIndex = displayIndex;
            RefreshBookshelfNameEditPanel();
        }

        private void CloseBookshelfNameEditPanel()
        {
            isBookshelfNameEditPanelOpen = false;
            bookshelfNameInputIndex = -1;
            if (bookshelfNameEditPanel != null)
            {
                bookshelfNameEditPanel.SetActive(false);
            }

            if (bookshelfNameModalBackdrop != null && !HasOpenBookshelfModal())
            {
                bookshelfNameModalBackdrop.SetActive(false);
            }
        }

        private void SaveBookshelfName()
        {
            if (!IsBookshelfEditMode ||
                bookshelfGenerator == null ||
                bookshelfNameInputField == null ||
                bookshelfGenerator.IsBookshelfSwitching)
            {
                return;
            }

            var displayIndex = Mathf.Clamp(bookshelfGenerator.DisplayBookshelfIndex, 0, Mathf.Max(1, bookshelfGenerator.BookshelfCount) - 1);
            var targetIndex = bookshelfNameInputIndex >= 0
                ? Mathf.Clamp(bookshelfNameInputIndex, 0, Mathf.Max(1, bookshelfGenerator.BookshelfCount) - 1)
                : displayIndex;
            if (!EnsureBookshelfApiMutationReady(targetIndex, "책장 이름 변경"))
            {
                RefreshBookshelfNameEditPanel();
                return;
            }

            var previousName = bookshelfGenerator.GetBookshelfName(targetIndex);
            bookshelfGenerator.SetBookshelfName(targetIndex, bookshelfNameInputField.text);
            bookshelfNameInputField.text = bookshelfGenerator.GetBookshelfName(targetIndex);
            if (bookshelfNameInputField.text != previousName)
            {
                MarkBookshelfEditDirty();
                SyncBookshelfUpdateOrRefresh(targetIndex, "책장 이름 변경");
            }

            SetBookshelfOverlayMode(BookshelfOverlayMode.None);
            RefreshBookshelfIndexIndicator(CurrentFocusArea == ExperienceFocusArea.Bookshelf && (state == null || !state.IsViewingBook));
            RefreshBookshelfNameEditPanel();
        }

        private void OpenBookshelfDesignPanel()
        {
            if (!CanMutateBookshelfCollection())
            {
                return;
            }

            CloseOverlayPanels(bookshelfDesignPanel);
            if (usesHttpApiMode)
            {
                bookshelfApiSyncController?.RefreshBookshelfSkinCatalog();
            }

            SetBookshelfOverlayMode(BookshelfOverlayMode.Design);
            RefreshHud();
        }

        private void CloseBookshelfDesignPanel()
        {
            isBookshelfDesignPanelOpen = false;
            if (bookshelfDesignPanel != null)
            {
                bookshelfDesignPanel.SetActive(false);
            }

            if (bookshelfNameModalBackdrop != null && !HasOpenBookshelfModal())
            {
                bookshelfNameModalBackdrop.SetActive(false);
            }

            RefreshHud();
        }

        private void OpenBookAddPanel()
        {
            if (!CanOpenBookAddPanel())
            {
                if (CurrentFocusArea == ExperienceFocusArea.Bookshelf &&
                    bookshelfGenerator != null &&
                    bookshelfGenerator.BookshelfCount > 0 &&
                    !CanUseBookshelfServerPlacement(bookshelfGenerator.DisplayBookshelfIndex))
                {
                    ShowBookshelfServerPlacementPending();
                }

                return;
            }

            CloseOverlayPanels(bookAddPanel);
            HideNonBookAddOverlays();
            SetBookshelfOverlayMode(BookshelfOverlayMode.BookAdd);

            if (bookAddSearchInputField != null)
            {
                bookAddSearchInputField.text = string.Empty;
            }

            activeBookAddBrowseMode = BookAddBrowseMode.Ranking;
            shouldResetBookAddScrollToTop = true;
            RefreshHud();
        }

        private void HideNonBookAddOverlays()
        {
            isBookshelfNameEditPanelOpen = false;
            bookshelfNameInputIndex = -1;
            if (bookshelfNameEditPanel != null)
            {
                bookshelfNameEditPanel.SetActive(false);
            }

            isBookshelfDeleteConfirmPanelOpen = false;
            pendingDeleteBookshelfIndex = -1;
            if (bookshelfDeleteConfirmPanel != null)
            {
                bookshelfDeleteConfirmPanel.SetActive(false);
            }

            isBookshelfDesignPanelOpen = false;
            if (bookshelfDesignPanel != null)
            {
                bookshelfDesignPanel.SetActive(false);
            }

            ResetBookshelfManageDragState();
            isBookshelfManagePanelOpen = false;
            if (bookshelfManagePanel != null)
            {
                bookshelfManagePanel.SetActive(false);
            }

            isBookshelfUnsavedConfirmPanelOpen = false;
            pendingBookshelfNavigationAfterSave = null;
            if (bookshelfUnsavedConfirmPanel != null)
            {
                bookshelfUnsavedConfirmPanel.SetActive(false);
            }
        }

        private void CloseBookAddPanel()
        {
            CloseBookAddPanel(true);
        }

        private void CloseBookAddPanel(bool refresh)
        {
            CancelBookAddPlacementSelection(false, false);
            isBookAddPanelOpen = false;
            isBookAddConfirmPanelOpen = false;
            pendingBookAddCatalogIndex = -1;
            pendingBookAddBookshelfIndex = -1;
            pendingBookAddCatalogItem = null;
            bookAddFeedbackMessage = string.Empty;
            shouldResetBookAddScrollToTop = true;
            for (var i = 0; i < bookAddResultCatalogIndices.Length; i++)
            {
                bookAddResultCatalogIndices[i] = -1;
                BindBookAddCoverImage(i, null);
            }

            CancelBookAddCoverLoads();
            ClearBookAddCoverSpriteCache();

            if (bookAddPanel != null)
            {
                bookAddPanel.SetActive(false);
            }

            if (bookshelfNameModalBackdrop != null &&
                !isBookshelfNameEditPanelOpen &&
                !isBookshelfDeleteConfirmPanelOpen &&
                !HasOpenBookshelfModal())
            {
                bookshelfNameModalBackdrop.SetActive(false);
            }

            if (refresh)
            {
                RefreshHud();
            }
        }

        private void HandleBookAddSearchChanged(string _)
        {
            bookAddFeedbackMessage = string.Empty;
            shouldResetBookAddScrollToTop = true;
            activeBookAddBrowseMode = BookAddBrowseMode.Search;
            var query = bookAddSearchInputField != null ? bookAddSearchInputField.text : string.Empty;
            RequestBookAddApiSearch(query);
            if (isBookAddPanelOpen)
            {
                RefreshBookAddPanel(bookshelfGenerator != null ? bookshelfGenerator.DisplayBookshelfIndex : -1);
            }
        }

        private void SelectBookAddTargetBookshelf(int index)
        {
            if (bookshelfGenerator == null ||
                index < 0 ||
                index >= bookshelfGenerator.BookshelfCount ||
                !CanOpenBookAddPanel())
            {
                return;
            }

            if (!CanUseBookshelfServerPlacement(index))
            {
                ShowBookshelfServerPlacementPending();
                return;
            }

            if (index != bookshelfGenerator.DisplayBookshelfIndex)
            {
                bookshelfGenerator.ShowBookshelf(index);
                shouldResetBookAddScrollToTop = true;
            }

            SetBookshelfOverlayMode(BookshelfOverlayMode.BookAdd);
            RefreshHud();
        }

        private void SetBookAddBrowseMode(BookAddBrowseMode mode)
        {
            if (activeBookAddBrowseMode == mode)
            {
                return;
            }

            activeBookAddBrowseMode = mode;
            bookAddFeedbackMessage = string.Empty;
            shouldResetBookAddScrollToTop = true;
            if (mode == BookAddBrowseMode.Search)
            {
                RequestBookAddApiSearch(bookAddSearchInputField != null ? bookAddSearchInputField.text : string.Empty);
            }
            else
            {
                RequestBookAddMarketBooks(mode);
            }

            RefreshHud();
        }

        private void RefreshBookAddPanel(int displayIndex)
        {
            if (!isBookAddPanelOpen || bookAddPanel == null)
            {
                return;
            }

            var canAdd = CanOpenBookAddPanel();
            var shelfName = bookshelfGenerator != null && displayIndex >= 0
                ? bookshelfGenerator.GetBookshelfName(displayIndex)
                : "현재 책장";
            if (bookAddTitleText != null)
            {
                bookAddTitleText.text = $"{shelfName}에 책 추가";
            }

            if (bookAddHintText != null)
            {
            var sectionSummary = bookshelfGenerator != null && bookshelfGenerator.ActiveBookshelfSectionCount > 0
                    ? $" 현재 빈 섹션 {bookshelfGenerator.ActiveBookshelfEmptySectionCount}/{bookshelfGenerator.ActiveBookshelfSectionCount}개."
                    : string.Empty;
                bookAddHintText.text = $"책장을 고른 뒤 원하는 책을 선택하고, 꽂을 위치를 직접 지정해 내 책장 분위기를 완성합니다.{sectionSummary}";
            }

            RefreshBookshelfSelectorButtons(bookAddShelfSelectorButtons, displayIndex, CanOpenBookAddPanel());

            if (bookAddSearchInputField != null)
            {
                bookAddSearchInputField.interactable = canAdd && activeBookAddBrowseMode == BookAddBrowseMode.Search;
            }

            StyleBookAddBrowseButton(bookAddSearchModeButton, activeBookAddBrowseMode == BookAddBrowseMode.Search);
            StyleBookAddBrowseButton(bookAddRecommendedModeButton, activeBookAddBrowseMode == BookAddBrowseMode.Recommended);
            StyleBookAddBrowseButton(bookAddRankingModeButton, activeBookAddBrowseMode == BookAddBrowseMode.Ranking);

            var query = activeBookAddBrowseMode == BookAddBrowseMode.Search && bookAddSearchInputField != null
                ? bookAddSearchInputField.text
                : string.Empty;
            if (activeBookAddBrowseMode == BookAddBrowseMode.Search)
            {
                RequestBookAddApiSearch(query);
            }
            else
            {
                RequestBookAddMarketBooks(activeBookAddBrowseMode);
            }

            var catalogItems = GetBookAddCatalogItems(query);
            var visibleSlot = 0;
            var matchCount = 0;
            for (var i = 0; i < catalogItems.Count; i++)
            {
                var item = catalogItems[i];
                if (!DoesCatalogItemMatch(item, query))
                {
                    continue;
                }

                if (visibleSlot < bookAddResultButtons.Length)
                {
                    var button = bookAddResultButtons[visibleSlot];
                    var isAlreadyAdded = IsCatalogBookAlreadyOnActiveShelf(i);
                    bookAddResultCatalogIndices[visibleSlot] = i;
                    if (button != null)
                    {
                        button.gameObject.SetActive(true);
                        button.interactable = canAdd && !isAlreadyAdded;
                        SetButtonLabel(button, BuildBookAddResultLabel(item, isAlreadyAdded));
                        StyleBookAddResultButton(button, !isAlreadyAdded);
                        LayoutBookAddResultRow(visibleSlot, item);
                    }
                }

                visibleSlot++;
                matchCount++;
            }

            var visibleCount = Mathf.Min(matchCount, bookAddResultButtons.Length);
            var viewportHeight = GetHudScrollViewportHeight(bookAddScrollRect, BookAddResultViewportHeight);
            if (bookAddRowsContent != null)
            {
                var contentHeight = Mathf.Max(viewportHeight, visibleCount * BookAddResultRowHeight);
                bookAddRowsContent.sizeDelta = new Vector2(0f, contentHeight);
            }

            var canScroll = visibleCount * BookAddResultRowHeight > viewportHeight + 1f;
            ApplyHudScrollState(bookAddScrollRect, bookAddScrollbar, bookAddRowsContent, canScroll, shouldResetBookAddScrollToTop);
            shouldResetBookAddScrollToTop = false;

            for (var i = visibleCount; i < bookAddResultButtons.Length; i++)
            {
                bookAddResultCatalogIndices[i] = -1;
                if (bookAddResultButtons[i] != null)
                {
                    bookAddResultButtons[i].gameObject.SetActive(false);
                }

                BindBookAddCoverImage(i, null);
            }

            if (closeBookAddButton != null)
            {
                StyleHudButton(
                    closeBookAddButton,
                    new Color32(245, 237, 226, 245),
                    new Color32(255, 248, 236, 255),
                    new Color32(224, 219, 214, 140),
                    16,
                    new Color32(84, 74, 66, 255),
                    true);
            }

            if (bookAddStatusText != null)
            {
                if (!string.IsNullOrWhiteSpace(bookAddFeedbackMessage))
                {
                    bookAddStatusText.text = bookAddFeedbackMessage;
                }
                else if (isBookAddApiSearchPending)
                {
                    bookAddStatusText.text = activeBookAddBrowseMode == BookAddBrowseMode.Search
                        ? "서버에서 책을 검색하는 중입니다."
                        : "서버에서 책 목록을 불러오는 중입니다.";
                }
                else if (!string.IsNullOrWhiteSpace(bookAddApiSearchMessage))
                {
                    bookAddStatusText.text = matchCount > 0
                        ? $"{bookAddApiSearchMessage} · 표시 결과 {matchCount}개"
                        : bookAddApiSearchMessage;
                }
                else if (matchCount == 0)
                {
                    bookAddStatusText.text = "검색 결과가 없습니다.";
                }
                else
                {
                    bookAddStatusText.text = matchCount > bookAddResultButtons.Length
                        ? $"검색 결과 {matchCount}개 중 {visibleCount}개 표시 · 스크롤"
                        : canScroll
                            ? $"검색 결과 {matchCount}개 · 스크롤"
                            : $"검색 결과 {matchCount}개";
                }
            }
        }

        private void OpenBookAddConfirmPanel(int resultSlot)
        {
            if (!CanOpenBookAddPanel() ||
                resultSlot < 0 ||
                resultSlot >= bookAddResultCatalogIndices.Length)
            {
                return;
            }

            var catalogIndex = bookAddResultCatalogIndices[resultSlot];
            var item = GetBookAddCatalogItem(catalogIndex);
            if (item == null)
            {
                return;
            }

            if (IsCatalogBookAlreadyOnActiveShelf(item))
            {
                bookAddFeedbackMessage = GetCatalogBookDuplicateMessage(item);
                RefreshBookAddPanel(bookshelfGenerator != null ? bookshelfGenerator.DisplayBookshelfIndex : -1);
                return;
            }

            pendingBookAddCatalogIndex = catalogIndex;
            pendingBookAddBookshelfIndex = bookshelfGenerator != null ? bookshelfGenerator.DisplayBookshelfIndex : -1;
            pendingBookAddCatalogItem = item;
            pendingBookAddOrientation = item.Orientation;
            pendingBookAddReadingStatus = App.Domain.ReadingStatus.Unread;
            bookAddFeedbackMessage = string.Empty;
            SetBookshelfOverlayMode(BookshelfOverlayMode.BookAddConfirm);
            RefreshBookshelfNameEditPanel();
        }

        private void SetPendingBookAddOrientation(BookOrientation orientation)
        {
            pendingBookAddOrientation = orientation;
            RefreshBookAddConfirmPanel();
        }

        private void SetPendingBookAddReadingStatus(App.Domain.ReadingStatus status)
        {
            pendingBookAddReadingStatus = status;
            RefreshBookAddConfirmPanel();
        }

        private void CloseBookAddConfirmPanel()
        {
            CloseBookAddConfirmPanelSilently();
            if (CanOpenBookAddPanel())
            {
                SetBookshelfOverlayMode(BookshelfOverlayMode.BookAdd);
            }
            else
            {
                SetBookshelfOverlayMode(BookshelfOverlayMode.None);
            }

            if (bookshelfNameModalBackdrop != null && !HasOpenBookshelfModal())
            {
                bookshelfNameModalBackdrop.SetActive(false);
            }

            RefreshHud();
        }

        private void CloseBookAddConfirmPanelSilently()
        {
            isBookAddConfirmPanelOpen = false;
            pendingBookAddCatalogIndex = -1;
            pendingBookAddBookshelfIndex = -1;
            pendingBookAddCatalogItem = null;
            if (bookAddConfirmPanel != null)
            {
                bookAddConfirmPanel.SetActive(false);
            }
        }

        private void ConfirmAddCatalogBookToShelf()
        {
            if (!CanOpenBookAddPanel() ||
                pendingBookAddCatalogItem == null)
            {
                CloseBookAddConfirmPanel();
                return;
            }

            BeginBookAddPlacementSelection();
            RefreshHud();
        }

        private void BeginBookAddPlacementSelection()
        {
            if (!CanOpenBookAddPanel() ||
                pendingBookAddCatalogItem == null)
            {
                CloseBookAddConfirmPanel();
                return;
            }

            if (!CanUseBookshelfServerPlacement(bookshelfGenerator != null ? bookshelfGenerator.DisplayBookshelfIndex : -1))
            {
                CloseBookAddConfirmPanel();
                ShowBookshelfServerPlacementPending();
                return;
            }

            if (IsCatalogBookAlreadyOnActiveShelf(pendingBookAddCatalogItem))
            {
                bookAddFeedbackMessage = GetCatalogBookDuplicateMessage(pendingBookAddCatalogItem);
                SetBookshelfOverlayMode(BookshelfOverlayMode.BookAdd);
                RefreshHud();
                return;
            }

            isBookAddPlacementSelectionActive = true;
            pendingBookAddBookshelfIndex = bookshelfGenerator != null ? bookshelfGenerator.ActiveBookshelfIndex : pendingBookAddBookshelfIndex;
            isBookAddPanelOpen = false;
            isBookAddConfirmPanelOpen = false;
            bookAddFeedbackMessage = string.Empty;

            if (bookAddPanel != null)
            {
                bookAddPanel.SetActive(false);
            }

            if (bookAddConfirmPanel != null)
            {
                bookAddConfirmPanel.SetActive(false);
            }

            if (bookshelfNameModalBackdrop != null)
            {
                bookshelfNameModalBackdrop.SetActive(false);
            }
        }

        private void UpdateBookAddPlacementGuide(Vector2 pointer)
        {
            if (bookshelfGenerator == null || Camera.main == null)
            {
                return;
            }

            bookshelfGenerator.ShowBookAddPlacementGuide(Camera.main, pointer, pendingBookAddOrientation);
        }

        private void HandleBookAddPlacementSelection(Vector2 pointer)
        {
            if (!isBookAddPlacementSelectionActive)
            {
                return;
            }

            if (!CanOpenBookAddPanel())
            {
                CancelBookAddPlacementSelection(false, true);
                return;
            }

            if (pendingBookAddBookshelfIndex >= 0 &&
                bookshelfGenerator != null &&
                bookshelfGenerator.ActiveBookshelfIndex != pendingBookAddBookshelfIndex)
            {
                bookAddFeedbackMessage = "책을 추가할 책장이 바뀌었습니다. 다시 위치를 선택해 주세요.";
                CancelBookAddPlacementSelection(true, true);
                return;
            }

            if (!AddCatalogBookToShelfAtPointer(pendingBookAddCatalogItem, pendingBookAddOrientation, pointer))
            {
                RefreshHud();
                return;
            }

            isBookAddPlacementSelectionActive = false;
            bookshelfGenerator?.HideBookAddPlacementGuide();
            pendingBookAddCatalogIndex = -1;
            pendingBookAddBookshelfIndex = -1;
            pendingBookAddCatalogItem = null;
            SetBookshelfOverlayMode(BookshelfOverlayMode.None);
            RefreshHud();
        }

        private void CancelBookAddPlacementSelectionFromStatusPanel()
        {
            CancelBookAddPlacementSelection(true, true);
        }

        private void CancelBookAddPlacementSelection(bool reopenAddPanel, bool refresh)
        {
            if (!isBookAddPlacementSelectionActive && !reopenAddPanel)
            {
                return;
            }

            isBookAddPlacementSelectionActive = false;
            bookshelfGenerator?.HideBookAddPlacementGuide();
            pendingBookAddCatalogIndex = -1;
            pendingBookAddBookshelfIndex = -1;
            pendingBookAddCatalogItem = null;
            bookAddFeedbackMessage = string.Empty;

            if (reopenAddPanel && CanOpenBookAddPanel())
            {
                SetBookshelfOverlayMode(BookshelfOverlayMode.BookAdd);
                shouldResetBookAddScrollToTop = true;
            }

            if (refresh)
            {
                RefreshHud();
            }
        }

        private void RefreshBookAddConfirmPanel()
        {
            if (!isBookAddConfirmPanelOpen || bookAddConfirmPanel == null)
            {
                return;
            }

            var item = pendingBookAddCatalogItem;
            if (item == null)
            {
                CloseBookAddConfirmPanel();
                return;
            }

            if (bookAddConfirmTitleText != null)
            {
                bookAddConfirmTitleText.text = item.Title;
            }

            if (bookAddConfirmMetaText != null)
            {
                var shelfName = bookshelfGenerator != null
                    ? bookshelfGenerator.GetBookshelfName(pendingBookAddBookshelfIndex >= 0 ? pendingBookAddBookshelfIndex : bookshelfGenerator.DisplayBookshelfIndex)
                    : "현재 책장";
                bookAddConfirmMetaText.text = $"{item.Author} · {item.Publisher} · {shelfName}";
            }

            if (bookAddConfirmSummaryText != null)
            {
                bookAddConfirmSummaryText.text = item.Summary;
            }

            StyleBookPlacementOptionButton(bookAddConfirmSpineButton, pendingBookAddOrientation == BookOrientation.Spine);
            StyleBookPlacementOptionButton(bookAddConfirmAngledButton, pendingBookAddOrientation == BookOrientation.Angled45);
            StyleBookPlacementOptionButton(bookAddConfirmFrontButton, pendingBookAddOrientation == BookOrientation.Front);
            StyleBookAddReadingStatusButton(bookAddConfirmUnreadButton, pendingBookAddReadingStatus == App.Domain.ReadingStatus.Unread);
            StyleBookAddReadingStatusButton(bookAddConfirmReadingButton, pendingBookAddReadingStatus == App.Domain.ReadingStatus.Reading);
            StyleBookAddReadingStatusButton(bookAddConfirmReadButton, pendingBookAddReadingStatus == App.Domain.ReadingStatus.Read);
            StyleHudButton(
                confirmBookAddButton,
                new Color32(88, 133, 117, 245),
                new Color32(72, 114, 100, 255),
                new Color32(224, 219, 214, 140),
                14,
                Color.white,
                true);
            StyleHudButton(
                cancelBookAddConfirmButton,
                new Color32(245, 237, 226, 245),
                new Color32(255, 248, 236, 255),
                new Color32(224, 219, 214, 140),
                14,
                new Color32(84, 74, 66, 255),
                true);
        }

        private void AddCatalogBookToShelf(int catalogIndex, BookOrientation orientation)
        {
            var item = GetBookAddCatalogItem(catalogIndex);
            if (!CanOpenBookAddPanel() ||
                catalogIndex < 0 ||
                item == null)
            {
                return;
            }

            if (IsCatalogBookAlreadyOnActiveShelf(item))
            {
                bookAddFeedbackMessage = GetCatalogBookDuplicateMessage(item);
                RefreshHud();
                return;
            }

            var book = bookshelfGenerator.AddBookToActiveBookshelf(
                item.Title,
                item.Author,
                item.Publisher,
                item.Summary,
                orientation);
            if (book == null)
            {
                bookAddFeedbackMessage = "현재 책장에 빈 공간이 없습니다.";
            }
            else
            {
                PrepareCatalogBookForApiCreation(book, item);
                if (dataStore != null)
                {
                    var meta = dataStore.GetOrCreateBookMeta(book);
                    if (meta != null)
                    {
                        meta.readingStatus = pendingBookAddReadingStatus;
                        meta.isRead = pendingBookAddReadingStatus == App.Domain.ReadingStatus.Read;
                    }
                }

                book.readingStatus = pendingBookAddReadingStatus;
                book.isRead = pendingBookAddReadingStatus == App.Domain.ReadingStatus.Read;
                SyncAddedBookOrRollback(book, book.CurrentSection, bookshelfGenerator != null ? bookshelfGenerator.ActiveBookshelfIndex : -1, item, orientation);
            }

            RefreshHud();
        }

        private bool AddCatalogBookToShelfAtPointer(BookAddCatalogItem item, BookOrientation orientation, Vector2 pointer)
        {
            if (!CanOpenBookAddPanel() ||
                item == null ||
                Camera.main == null)
            {
                return false;
            }

            if (IsCatalogBookAlreadyOnActiveShelf(item))
            {
                bookAddFeedbackMessage = GetCatalogBookDuplicateMessage(item);
                return false;
            }

            var book = bookshelfGenerator.AddBookToActiveBookshelfAtPointer(
                Camera.main,
                pointer,
                item.Title,
                item.Author,
                item.Publisher,
                item.Summary,
                orientation);
            if (book == null)
            {
                bookAddFeedbackMessage = "선택한 위치에 책을 꽂을 공간이 없습니다. 다른 칸이나 책 사이를 클릭하세요.";
                return false;
            }

            PrepareCatalogBookForApiCreation(book, item);
            if (dataStore != null)
            {
                var meta = dataStore.GetOrCreateBookMeta(book);
                if (meta != null)
                {
                    meta.readingStatus = pendingBookAddReadingStatus;
                    meta.isRead = pendingBookAddReadingStatus == App.Domain.ReadingStatus.Read;
                }
            }

            book.readingStatus = pendingBookAddReadingStatus;
            book.isRead = pendingBookAddReadingStatus == App.Domain.ReadingStatus.Read;
            SyncAddedBookOrRollback(book, book.CurrentSection, bookshelfGenerator != null ? bookshelfGenerator.ActiveBookshelfIndex : -1, item, orientation);
            return true;
        }

        private void SyncAddedBookOrRollback(
            BookEntity book,
            ShelfSection sourceSection,
            int targetBookshelfIndex,
            BookAddCatalogItem item,
            BookOrientation orientation)
        {
            if (book == null || item == null)
            {
                return;
            }

            var readingStatus = book.readingStatus;
            var successMessage = $"{item.Title} 추가 완료 · {GetReadingStatusLabel(readingStatus)} · {GetOrientationLabel(orientation)}";
            var hasServerSync = bookshelfApiSyncController != null && bookshelfApiSyncController.IsServerSyncEnabled;
            if (!hasServerSync)
            {
                RollbackAddedBookAfterApiFailure(
                    book,
                    sourceSection,
                    item.Title,
                    "책장 API 동기화가 준비되지 않아 책 추가를 서버에 요청할 수 없습니다.");
                ShowBookshelfServerPlacementPending();
                return;
            }

            if (!CanUseBookshelfServerPlacement(targetBookshelfIndex))
            {
                RollbackAddedBookAfterApiFailure(
                    book,
                    sourceSection,
                    item.Title,
                    "새 책장 정보가 아직 서버와 동기화되지 않았습니다. 잠시 후 다시 시도해 주세요.");
                ShowBookshelfServerPlacementPending();
                return;
            }

            bookAddFeedbackMessage = $"{item.Title} 추가 요청 중입니다.";
            var didStartAddSync = bookshelfApiSyncController.AddBookToShelf(
                book,
                targetBookshelfIndex,
                updatedBook =>
                {
                    ApplyBookEntityToMeta(updatedBook);
                    bookAddFeedbackMessage = successMessage;
                    ShowBookshelfTransientStatus("책 추가 저장 완료", $"{item.Title}이(가) 서버에 저장되었습니다.", false);
                    RefreshHud();
                },
                error => RollbackAddedBookAfterApiFailure(book, sourceSection, item.Title, error),
                (updatedBook, error) => HandleAddedBookStateSyncFailure(updatedBook, item.Title, error));
            if (!didStartAddSync)
            {
                RollbackAddedBookAfterApiFailure(
                    book,
                    sourceSection,
                    item.Title,
                    "책장/섹션 서버 식별자를 찾을 수 없습니다.");
            }
        }

        private void HandleAddedBookStateSyncFailure(BookEntity book, string title, string errorMessage)
        {
            if (book == null)
            {
                RollbackAddedBookAfterApiFailure(book, null, title, errorMessage);
                return;
            }

            ApplyBookEntityToMeta(book);
            var meta = dataStore != null ? dataStore.GetOrCreateBookMeta(book) : null;
            var desired = new BookStateMutationSnapshot(book, meta);
            var retryKey = BuildMutationRetryKey("책 추가 상태 저장", book);
            var retryAction = CreateBookStateRetryAction(
                book,
                desired,
                desired,
                "책 추가 상태 저장",
                () => SyncBookStateOrRollback(book, desired, desired, "책 추가 상태 저장"));
            var pendingRetryAction = RegisterPendingMutationRetry(
                retryKey,
                "책 추가 상태 저장",
                errorMessage,
                retryAction);

            bookAddFeedbackMessage = $"{title} 추가 완료 · 상태 저장 대기";
            ShowBookshelfTransientStatus("책 추가 완료 · 상태 저장 실패", "책은 추가됐고 독서 상태 저장만 재시도할 수 있습니다.", true);
            ShowDeleteResultDialog(
                "책 추가 완료 · 상태 저장 실패",
                $"책은 서버에 추가됐지만 독서 상태/별점 저장을 완료하지 못했습니다.\n{NormalizeMutationErrorMessage(errorMessage)}",
                false,
                pendingRetryAction,
                "재시도");
            RefreshHud();
        }

        private void RollbackAddedBookAfterApiFailure(
            BookEntity book,
            ShelfSection sourceSection,
            string title,
            string errorMessage)
        {
            Debug.LogWarning($"Book add failed on server. Rolling back local book. {errorMessage}");
            if (book != null)
            {
                if (state != null)
                {
                    if (state.ActiveViewedBook == book && interactionController != null)
                    {
                        interactionController.CloseViewedBook(true);
                    }

                    state.RemoveBook(book);
                    if (sourceSection != null)
                    {
                        state.LayoutSection(sourceSection, this, true);
                    }
                }

                Destroy(book.gameObject);
            }

            bookAddFeedbackMessage = $"{title} 추가 실패";
            ShowDeleteResultDialog(
                "책 추가 실패",
                $"서버 요청을 완료하지 못해 추가한 책을 되돌렸습니다.\n{NormalizeMutationErrorMessage(errorMessage)}",
                false);
            RefreshHud();
        }

        private bool HasOpenBookshelfModal()
        {
            return overlayStateController.HasOpenBookshelfModal(CaptureBookshelfOverlayState());
        }

        private bool HasBlockingBookshelfOverlay()
        {
            return overlayStateController.HasBlockingBookshelfOverlay(CaptureBookshelfOverlayState());
        }

        private BookshelfOverlayStateSnapshot CaptureBookshelfOverlayState()
        {
            return new BookshelfOverlayStateSnapshot(
                isBookshelfNameEditPanelOpen,
                isBookshelfDeleteConfirmPanelOpen,
                isBookshelfDesignPanelOpen,
                isBookAddPanelOpen,
                isBookAddConfirmPanelOpen,
                isBookshelfManagePanelOpen,
                isBookshelfUnsavedConfirmPanelOpen,
                isDeleteResultDialogOpen,
                isBookSearchPanelOpen,
                isBookAddPlacementSelectionActive,
                isBookEditPanelOpen,
                isBookPlacementEditorOpen,
                isBookReportWritePanelOpen,
                isBookReportReadPanelOpen,
                isBookReviewWritePanelOpen,
                isFirstBookshelfTutorialOpen,
                activeEditSection != null);
        }

        private void ToggleBookSearchPanel()
        {
            if (isBookSearchPanelOpen)
            {
                CloseBookSearchPanel();
                return;
            }

            OpenBookSearchPanel();
        }

        private void OpenBookSearchPanel()
        {
            if (CurrentFocusArea != ExperienceFocusArea.Bookshelf ||
                (state != null && state.IsViewingBook) ||
                (bookshelfGenerator != null && bookshelfGenerator.IsBookshelfSwitching))
            {
                return;
            }

            if (TryPromptUnsavedBookshelfChanges(OpenBookSearchPanel))
            {
                return;
            }

            if (IsBookshelfEditMode)
            {
                SetBookshelfEditMode(false);
            }

            CloseOverlayPanels(bookSearchPanel);
            isBookSearchPanelOpen = true;
            activeBookSearchFilter = BookSearchFilter.All;
            shouldResetBookSearchScrollToTop = true;
            if (bookSearchInputField != null)
            {
                bookSearchInputField.text = string.Empty;
            }

            RefreshHud();
        }

        private void CloseBookSearchPanel()
        {
            isBookSearchPanelOpen = false;
            bookSearchResults.Clear();
            if (bookSearchPanel != null)
            {
                bookSearchPanel.SetActive(false);
            }

            RefreshHud();
        }

        private void HandleBookSearchChanged(string _)
        {
            shouldResetBookSearchScrollToTop = true;
            RefreshBookSearchPanel();
        }

        private void SetBookSearchFilter(BookSearchFilter filter)
        {
            activeBookSearchFilter = filter;
            shouldResetBookSearchScrollToTop = true;
            RefreshBookSearchPanel();
        }

        private void SelectBookSearchTargetBookshelf(int index)
        {
            if (bookshelfGenerator == null ||
                index < 0 ||
                index >= bookshelfGenerator.BookshelfCount ||
                CurrentFocusArea != ExperienceFocusArea.Bookshelf)
            {
                return;
            }

            if (index != bookshelfGenerator.DisplayBookshelfIndex)
            {
                bookshelfGenerator.ShowBookshelf(index);
                shouldResetBookSearchScrollToTop = true;
            }

            isBookSearchPanelOpen = true;
            RefreshHud();
        }

        private void RefreshBookSearchPanel()
        {
            if (bookSearchPanel == null)
            {
                return;
            }

            var canShow = isBookSearchPanelOpen &&
                          CurrentFocusArea == ExperienceFocusArea.Bookshelf &&
                          !IsBookshelfEditMode &&
                          (state == null || (!state.IsViewingBook && state.ActiveDraggedBook == null)) &&
                          (bookshelfGenerator == null || !bookshelfGenerator.IsBookshelfSwitching);
            bookSearchPanel.SetActive(canShow);
            if (!canShow)
            {
                isBookSearchPanelOpen = false;
                bookSearchResults.Clear();
                return;
            }

            if (bookSearchTitleText != null)
            {
                bookSearchTitleText.text = bookshelfGenerator != null
                    ? $"{bookshelfGenerator.DisplayBookshelfName} 책 찾기"
                    : "책 찾기";
            }

            RefreshBookshelfSelectorButtons(bookSearchShelfSelectorButtons, bookshelfGenerator != null ? bookshelfGenerator.DisplayBookshelfIndex : -1, CurrentFocusArea == ExperienceFocusArea.Bookshelf);

            var query = bookSearchInputField != null ? bookSearchInputField.text : string.Empty;
            bookSearchResults.Clear();
            var allBooks = new List<BookEntity>();
            var allMatchCount = 0;
            var readingMatchCount = 0;
            var readMatchCount = 0;
            var likedMatchCount = 0;
            CollectActiveShelfBooks(allBooks);
            for (var i = 0; i < allBooks.Count; i++)
            {
                var book = allBooks[i];
                var matchesQuery = DoesBookMatchSearch(book, query, BookSearchFilter.All);
                if (!matchesQuery)
                {
                    continue;
                }

                allMatchCount++;
                if (DoesBookMatchSearch(book, query, BookSearchFilter.Reading))
                {
                    readingMatchCount++;
                }

                if (DoesBookMatchSearch(book, query, BookSearchFilter.Read))
                {
                    readMatchCount++;
                }

                if (DoesBookMatchSearch(book, query, BookSearchFilter.Liked))
                {
                    likedMatchCount++;
                }

                if (activeBookSearchFilter == BookSearchFilter.All ||
                    DoesBookMatchSearch(book, query, activeBookSearchFilter))
                {
                    bookSearchResults.Add(book);
                }
            }

            var visibleCount = Mathf.Min(bookSearchResults.Count, bookSearchResultButtons.Length);
            var viewportHeight = GetHudScrollViewportHeight(bookSearchScrollRect, BookSearchResultViewportHeight);
            if (bookSearchRowsContent != null)
            {
                var contentHeight = Mathf.Max(viewportHeight, visibleCount * BookSearchResultRowHeight);
                bookSearchRowsContent.sizeDelta = new Vector2(0f, contentHeight);
            }

            var canScroll = visibleCount * BookSearchResultRowHeight > viewportHeight + 1f;
            ApplyHudScrollState(bookSearchScrollRect, bookSearchScrollbar, bookSearchRowsContent, canScroll, shouldResetBookSearchScrollToTop);
            shouldResetBookSearchScrollToTop = false;

            for (var i = 0; i < bookSearchResultButtons.Length; i++)
            {
                var button = bookSearchResultButtons[i];
                if (button == null)
                {
                    continue;
                }

                var isVisible = i < visibleCount;
                button.gameObject.SetActive(isVisible);
                if (!isVisible)
                {
                    continue;
                }

                var book = bookSearchResults[i];
                var meta = dataStore != null ? dataStore.GetOrCreateBookMeta(book) : null;
                var title = TryGetBookDisplayTitle(book);
                var author = meta != null ? meta.author : book.author;
                var status = meta != null ? GetReadingStatusLabel(meta.readingStatus) : GetReadingStatusLabel(book.readingStatus);
                SetButtonLabel(button, $"{title}\n{author} · {status}");
                StyleBookAddResultButton(button);
            }

            SetButtonLabel(bookSearchAllFilterButton, $"전체 {allMatchCount}");
            SetButtonLabel(bookSearchReadingFilterButton, $"읽는 중 {readingMatchCount}");
            SetButtonLabel(bookSearchReadFilterButton, $"완독 {readMatchCount}");
            SetButtonLabel(bookSearchLikedFilterButton, $"좋아요 {likedMatchCount}");
            StyleBookSearchFilterButton(bookSearchAllFilterButton, activeBookSearchFilter == BookSearchFilter.All);
            StyleBookSearchFilterButton(bookSearchReadingFilterButton, activeBookSearchFilter == BookSearchFilter.Reading);
            StyleBookSearchFilterButton(bookSearchReadFilterButton, activeBookSearchFilter == BookSearchFilter.Read);
            StyleBookSearchFilterButton(bookSearchLikedFilterButton, activeBookSearchFilter == BookSearchFilter.Liked);
            StyleHudButton(
                closeBookSearchButton,
                new Color32(245, 237, 226, 245),
                new Color32(255, 248, 236, 255),
                new Color32(224, 219, 214, 140),
                16,
                new Color32(84, 74, 66, 255),
                true);

            if (bookSearchStatusText != null)
            {
                var sectionSummary = bookshelfGenerator != null && bookshelfGenerator.ActiveBookshelfSectionCount > 0
                    ? $" · 빈 섹션 {bookshelfGenerator.ActiveBookshelfEmptySectionCount}/{bookshelfGenerator.ActiveBookshelfSectionCount}"
                    : string.Empty;
                bookSearchStatusText.text = bookSearchResults.Count == 0
                    ? "일치하는 책이 없습니다."
                    : bookSearchResults.Count > bookSearchResultButtons.Length
                        ? $"{bookSearchResults.Count}권 중 {visibleCount}권 표시 · 스크롤{sectionSummary}"
                        : canScroll
                            ? $"{bookSearchResults.Count}권 · 스크롤{sectionSummary}"
                            : $"{bookSearchResults.Count}권{sectionSummary}";
            }

            bookSearchPanel.transform.SetAsLastSibling();
        }

        private void OpenBookSearchResult(int slot)
        {
            if (slot < 0 || slot >= bookSearchResults.Count)
            {
                return;
            }

            var book = bookSearchResults[slot];
            CloseBookSearchPanel();
            if (interactionController != null)
            {
                interactionController.ViewBookFromUi(book);
            }
            else
            {
                OpenBookDetailPanel(book);
            }
        }

        private void OpenBookshelfManagePanel()
        {
            if (!CanMutateBookshelfCollection())
            {
                return;
            }

            CloseOverlayPanels(bookshelfManagePanel);
            SetBookshelfOverlayMode(BookshelfOverlayMode.Manage);
            RefreshHud();
        }

        private void CloseBookshelfManagePanel()
        {
            ResetBookshelfManageDragState();
            isBookshelfManagePanelOpen = false;
            if (bookshelfManagePanel != null)
            {
                bookshelfManagePanel.SetActive(false);
            }

            if (bookshelfNameModalBackdrop != null && !HasOpenBookshelfModal())
            {
                bookshelfNameModalBackdrop.SetActive(false);
            }

            RefreshHud();
        }

        private void RefreshBookshelfManagePanel()
        {
            if (!isBookshelfManagePanelOpen ||
                bookshelfManagePanel == null ||
                bookshelfGenerator == null)
            {
                return;
            }

            var activeIndex = Mathf.Clamp(bookshelfGenerator.DisplayBookshelfIndex, 0, Mathf.Max(1, bookshelfGenerator.BookshelfCount) - 1);
            if (bookshelfManageTitleText != null)
            {
                bookshelfManageTitleText.text = "책장 관리";
            }

            if (bookshelfManageSummaryText != null)
            {
                bookshelfManageSummaryText.text = BuildBookshelfManageSummary(activeIndex);
            }

            var viewportHeight = GetHudScrollViewportHeight(bookshelfManageScrollRect, ShelfManageViewportHeight);
            if (bookshelfManageRowsContent != null)
            {
                var rowContentHeight = Mathf.Max(viewportHeight, bookshelfGenerator.BookshelfCount * ShelfManageRowHeight);
                bookshelfManageRowsContent.sizeDelta = new Vector2(0f, rowContentHeight);
            }

            var canScrollBookshelves = bookshelfGenerator.BookshelfCount * ShelfManageRowHeight > viewportHeight + 1f;
            ApplyHudScrollState(
                bookshelfManageScrollRect,
                bookshelfManageScrollbar,
                bookshelfManageRowsContent,
                canScrollBookshelves && bookshelfManageDragSourceIndex < 0,
                false);

            for (var i = 0; i < bookshelfManageRowButtons.Length; i++)
            {
                var button = bookshelfManageRowButtons[i];
                if (button == null)
                {
                    continue;
                }

                var showRow = i < bookshelfGenerator.BookshelfCount;
                button.gameObject.SetActive(showRow);
                if (!showRow)
                {
                    StyleBookshelfManageDragHandle(i, false, false, false);
                    continue;
                }

                var isActive = i == activeIndex;
                var isDragging = i == bookshelfManageDragSourceIndex;
                var isDropTarget = bookshelfManageDragSourceIndex >= 0 &&
                                   i == bookshelfManageDragTargetIndex &&
                                   bookshelfManageDragTargetIndex != bookshelfManageDragSourceIndex;
                var visibility = GetVisibilityLabel(bookshelfGenerator.GetBookshelfVisibility(i));
                var rowPrefix = isDropTarget ? "→ " : isActive ? "● " : "○ ";
                SetButtonLabel(button, $"{rowPrefix}{i + 1}. {bookshelfGenerator.GetBookshelfName(i)}   ·   {visibility}");
                StyleHudButton(
                    button,
                    isDragging ? new Color32(255, 233, 208, 245) :
                    isDropTarget ? new Color32(238, 248, 241, 245) :
                    isActive ? new Color32(255, 248, 236, 245) : new Color32(255, 255, 255, 230),
                    isDropTarget ? new Color32(247, 255, 250, 255) : new Color32(255, 255, 255, 255),
                    new Color32(224, 219, 214, 140),
                    13,
                    isDropTarget ? new Color32(53, 94, 73, 255) :
                    isActive ? new Color32(63, 47, 38, 255) : new Color32(84, 74, 66, 255),
                    isActive || isDropTarget);
                StyleBookshelfManageDragHandle(i, true, isDragging, isDropTarget);
            }

            var canSortBooks = CanMutateBookshelfCollection() && bookshelfGenerator.ActiveBookshelfBookCount > 1;
            if (shelfManageSortTitleButton != null)
            {
                shelfManageSortTitleButton.interactable = canSortBooks;
            }

            if (shelfManageSortAuthorButton != null)
            {
                shelfManageSortAuthorButton.interactable = canSortBooks;
            }

            if (shelfManageSortStatusButton != null)
            {
                shelfManageSortStatusButton.interactable = canSortBooks;
            }

            if (shelfManageSortLikedButton != null)
            {
                shelfManageSortLikedButton.interactable = canSortBooks;
            }

            StyleBookshelfManageSortButton(shelfManageSortTitleButton, canSortBooks);
            StyleBookshelfManageSortButton(shelfManageSortAuthorButton, canSortBooks);
            StyleBookshelfManageSortButton(shelfManageSortStatusButton, canSortBooks);
            StyleBookshelfManageSortButton(shelfManageSortLikedButton, canSortBooks);

            var canSwitchManagedShelf = CanMutateBookshelfCollection() && bookshelfGenerator.BookshelfCount > 1;
            StyleBookshelfManageSortButton(shelfManagePreviousButton, canSwitchManagedShelf);
            StyleBookshelfManageSortButton(shelfManageNextButton, canSwitchManagedShelf);
            if (shelfManagePreviousButton != null)
            {
                shelfManagePreviousButton.gameObject.SetActive(false);
                shelfManagePreviousButton.interactable = false;
            }

            if (shelfManageNextButton != null)
            {
                shelfManageNextButton.gameObject.SetActive(false);
                shelfManageNextButton.interactable = false;
            }

            StyleHudButton(
                shelfManageVisibilityButton,
                new Color32(238, 248, 241, 235),
                new Color32(247, 255, 250, 255),
                new Color32(224, 219, 214, 140),
                13,
                new Color32(53, 94, 73, 255),
                true);
            SetButtonLabel(shelfManageVisibilityButton, $"공개: {GetVisibilityLabel(bookshelfGenerator.GetBookshelfVisibility(activeIndex))}");
            StyleHudButton(
                shelfManageRenameButton,
                new Color32(255, 248, 236, 235),
                new Color32(255, 255, 255, 245),
                new Color32(224, 219, 214, 140),
                13,
                new Color32(63, 47, 38, 255),
                true);
            SetButtonLabel(shelfManageRenameButton, "이름 변경");
            if (shelfManageRenameButton != null)
            {
                shelfManageRenameButton.interactable = CanMutateBookshelfCollection() && bookshelfGenerator.BookshelfCount > 0;
            }

            var canAddManagedShelf = CanMutateBookshelfCollection() && !isBookshelfCreateRequestInFlight;
            StyleHudButton(
                shelfManageAddButton,
                canAddManagedShelf ? new Color32(238, 248, 241, 235) : new Color32(236, 231, 224, 210),
                canAddManagedShelf ? new Color32(247, 255, 250, 255) : new Color32(236, 231, 224, 210),
                new Color32(224, 219, 214, 140),
                13,
                canAddManagedShelf ? new Color32(53, 94, 73, 255) : new Color32(142, 132, 122, 255),
                canAddManagedShelf);
            SetButtonLabel(shelfManageAddButton, isBookshelfCreateRequestInFlight ? "추가 중" : "추가");
            if (shelfManageAddButton != null)
            {
                shelfManageAddButton.interactable = canAddManagedShelf;
            }

            var canDeleteManagedShelf = CanMutateBookshelfCollection() && bookshelfGenerator.BookshelfCount > 1;
            StyleDeleteActionButton(shelfManageDeleteButton, canDeleteManagedShelf, false, 13);
            if (shelfManageDeleteButton != null)
            {
                shelfManageDeleteButton.interactable = canDeleteManagedShelf;
            }

            StyleHudButton(
                shelfManageCloseButton,
                new Color32(245, 237, 226, 245),
                new Color32(255, 248, 236, 255),
                new Color32(224, 219, 214, 140),
                13,
                new Color32(84, 74, 66, 255),
                true);

            if (bookshelfManageStatusText != null)
            {
                bookshelfManageStatusText.text = isBookshelfCreateRequestInFlight
                    ? "새 책장을 서버에 저장하는 중입니다. 완료될 때까지 추가 버튼을 사용할 수 없습니다."
                    : bookshelfManageDragSourceIndex >= 0 &&
                                                 bookshelfManageDragTargetIndex >= 0 &&
                                                 bookshelfManageDragSourceIndex < bookshelfGenerator.BookshelfCount &&
                                                 bookshelfManageDragTargetIndex < bookshelfGenerator.BookshelfCount
                    ? $"{bookshelfManageDragSourceIndex + 1}번 책장을 {bookshelfManageDragTargetIndex + 1}번째 위치로 이동 중입니다. 놓으면 순서가 바뀝니다."
                    : "목록에서 책장을 선택하고 이름 변경, 공개 범위, 순서를 관리할 수 있습니다.";
            }
        }

        private void OpenManagedBookshelfNameEditPanel()
        {
            if (!CanMutateBookshelfCollection())
            {
                return;
            }

            OpenBookshelfNameEditPanel();
        }

        private void SelectManagedBookshelf(int rowIndex)
        {
            if (bookshelfGenerator == null || rowIndex < 0 || rowIndex >= bookshelfGenerator.BookshelfCount)
            {
                return;
            }

            if (rowIndex != bookshelfGenerator.DisplayBookshelfIndex &&
                bookshelfGenerator.ShowBookshelf(rowIndex))
            {
                RefreshHud();
                return;
            }

            RefreshBookshelfManagePanel();
        }

        private void ShowPreviousManagedBookshelf()
        {
            if (!CanMutateBookshelfCollection() || bookshelfGenerator == null)
            {
                return;
            }

            if (bookshelfGenerator.ShowPreviousBookshelf())
            {
                isBookshelfManagePanelOpen = true;
                RefreshHud();
            }
        }

        private void ShowNextManagedBookshelf()
        {
            if (!CanMutateBookshelfCollection() || bookshelfGenerator == null)
            {
                return;
            }

            if (bookshelfGenerator.ShowNextBookshelf())
            {
                isBookshelfManagePanelOpen = true;
                RefreshHud();
            }
        }

        private void CycleManagedBookshelfVisibility()
        {
            if (!CanMutateBookshelfCollection() || bookshelfGenerator == null)
            {
                return;
            }

            var displayIndex = bookshelfGenerator.DisplayBookshelfIndex;
            if (!EnsureBookshelfApiMutationReady(displayIndex, "책장 공개 범위 변경"))
            {
                RefreshHud();
                return;
            }

            bookshelfGenerator.CycleActiveBookshelfVisibility();
            MarkBookshelfEditDirty();
            SyncBookshelfUpdateOrRefresh(displayIndex, "책장 공개 범위 변경");
            RefreshHud();
        }

        private void AddBookshelfFromManagePanel()
        {
            if (isBookshelfCreateRequestInFlight ||
                !CanMutateBookshelfCollection() ||
                bookshelfGenerator == null)
            {
                return;
            }

            var isFirstBookshelf = bookshelfGenerator.BookshelfCount <= 0;
            if (isFirstBookshelf)
            {
                QueueFirstBookshelfFeatureGuide();
                isFirstBookshelfCreateInProgress = true;
            }

            if (usesHttpApiMode)
            {
                if (!EnsureBookshelfApiMutationReady("책장 추가"))
                {
                    if (isFirstBookshelf)
                    {
                        isFirstBookshelfCreateInProgress = false;
                    }

                    RefreshHud();
                    return;
                }

                ShowBookshelfTransientStatus("책장 저장 중", "새 책장을 서버에 생성하고 있습니다.", false);
                SyncBookshelfCreateOrRefresh(BuildDefaultNewBookshelfName(), "책장 추가");
                isBookshelfManagePanelOpen = true;
                RefreshHud();
                return;
            }

            if (bookshelfGenerator.AddBookshelf())
            {
                if (isFirstBookshelf)
                {
                    isFirstBookshelfCreateInProgress = false;
                }

                MarkBookshelfEditDirty();
                SyncBookshelfCreateOrRefresh(bookshelfGenerator.DisplayBookshelfName, "책장 추가");
                isBookshelfManagePanelOpen = true;
                RefreshHud();
            }
            else if (isFirstBookshelf)
            {
                isFirstBookshelfCreateInProgress = false;
            }
        }

        private void BeginBookshelfManageRowDrag(int rowIndex, PointerEventData eventData)
        {
            if (!CanDragBookshelfManageRow(rowIndex))
            {
                return;
            }

            bookshelfManageDragSourceIndex = rowIndex;
            bookshelfManageDragTargetIndex = rowIndex;
            if (bookshelfManageScrollRect != null)
            {
                bookshelfManageScrollWasEnabled = bookshelfManageScrollRect.enabled;
                bookshelfManageScrollRect.enabled = false;
            }

            eventData?.Use();
            RefreshBookshelfManagePanel();
        }

        private void UpdateBookshelfManageRowDrag(int rowIndex, PointerEventData eventData)
        {
            if (bookshelfManageDragSourceIndex != rowIndex ||
                bookshelfManageDragSourceIndex < 0 ||
                bookshelfGenerator == null)
            {
                return;
            }

            AutoScrollBookshelfManageRows(eventData);
            var nextTargetIndex = ResolveBookshelfManageDropIndex(eventData);
            if (nextTargetIndex != bookshelfManageDragTargetIndex)
            {
                bookshelfManageDragTargetIndex = nextTargetIndex;
                RefreshBookshelfManagePanel();
            }

            eventData?.Use();
        }

        private void EndBookshelfManageRowDrag(int rowIndex, PointerEventData eventData)
        {
            if (bookshelfManageDragSourceIndex != rowIndex)
            {
                ResetBookshelfManageDragState();
                RefreshBookshelfManagePanel();
                return;
            }

            var sourceIndex = bookshelfManageDragSourceIndex;
            var targetIndex = bookshelfManageDragTargetIndex;
            ResetBookshelfManageDragState();
            eventData?.Use();

            if (bookshelfGenerator != null &&
                sourceIndex >= 0 &&
                targetIndex >= 0 &&
                sourceIndex != targetIndex)
            {
                if (!EnsureBookshelfReorderApiReady("책장 순서 변경"))
                {
                    RefreshBookshelfManagePanel();
                    return;
                }

                if (!bookshelfGenerator.MoveBookshelfOrder(sourceIndex, targetIndex))
                {
                    RefreshBookshelfManagePanel();
                    return;
                }

                MarkBookshelfEditDirty();
                SyncBookshelfReorderOrRefresh("책장 순서 변경");
                isBookshelfManagePanelOpen = true;
                RefreshHud();
                return;
            }

            RefreshBookshelfManagePanel();
        }

        private bool CanDragBookshelfManageRow(int rowIndex)
        {
            return isBookshelfManagePanelOpen &&
                   CanMutateBookshelfCollection() &&
                   bookshelfGenerator != null &&
                   !bookshelfGenerator.IsBookshelfSwitching &&
                   bookshelfGenerator.BookshelfCount > 1 &&
                   rowIndex >= 0 &&
                   rowIndex < bookshelfGenerator.BookshelfCount;
        }

        private void AutoScrollBookshelfManageRows(PointerEventData eventData)
        {
            if (eventData == null ||
                bookshelfManageScrollRect == null ||
                bookshelfManageRowsContent == null ||
                bookshelfManageScrollRect.viewport == null)
            {
                return;
            }

            var viewport = bookshelfManageScrollRect.viewport;
            var viewportHeight = viewport.rect.height;
            var contentHeight = bookshelfManageRowsContent.rect.height;
            var scrollableHeight = contentHeight - viewportHeight;
            if (scrollableHeight <= 1f)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    viewport,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var viewportPoint))
            {
                return;
            }

            const float edgeBand = 28f;
            var direction = 0f;
            if (viewportPoint.y > -edgeBand)
            {
                direction = 1f;
            }
            else if (viewportPoint.y < -viewportHeight + edgeBand)
            {
                direction = -1f;
            }

            if (Mathf.Approximately(direction, 0f))
            {
                return;
            }

            var normalizedStep = Mathf.Clamp(ShelfManageRowHeight * 0.42f / scrollableHeight, 0.018f, 0.12f);
            bookshelfManageScrollRect.verticalNormalizedPosition = Mathf.Clamp01(
                bookshelfManageScrollRect.verticalNormalizedPosition + direction * normalizedStep);
            Canvas.ForceUpdateCanvases();
        }

        private int ResolveBookshelfManageDropIndex(PointerEventData eventData)
        {
            if (bookshelfManageRowsContent == null || bookshelfGenerator == null || eventData == null)
            {
                return Mathf.Clamp(bookshelfManageDragTargetIndex, 0, Mathf.Max(0, bookshelfGenerator != null ? bookshelfGenerator.BookshelfCount - 1 : 0));
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    bookshelfManageRowsContent,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var localPoint))
            {
                return Mathf.Clamp(bookshelfManageDragTargetIndex, 0, bookshelfGenerator.BookshelfCount - 1);
            }

            var rawIndex = Mathf.FloorToInt((-localPoint.y + ShelfManageRowHeight * 0.5f) / ShelfManageRowHeight);
            return Mathf.Clamp(rawIndex, 0, bookshelfGenerator.BookshelfCount - 1);
        }

        private void ResetBookshelfManageDragState()
        {
            bookshelfManageDragSourceIndex = -1;
            bookshelfManageDragTargetIndex = -1;
            if (bookshelfManageScrollRect != null)
            {
                bookshelfManageScrollRect.enabled = bookshelfManageScrollWasEnabled;
            }
        }

        private string BuildBookshelfManageSummary(int activeIndex)
        {
            if (bookshelfGenerator == null)
            {
                return "현재 책장 정보를 불러오는 중";
            }

            var total = 0;
            var reading = 0;
            var read = 0;
            var liked = 0;
            if (state != null)
            {
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
                        if (book == null)
                        {
                            continue;
                        }

                        total++;
                        var meta = dataStore != null ? dataStore.GetOrCreateBookMeta(book) : null;
                        var status = meta != null ? meta.readingStatus : book.readingStatus;
                        if (status == App.Domain.ReadingStatus.Reading)
                        {
                            reading++;
                        }
                        else if (status == App.Domain.ReadingStatus.Read)
                        {
                            read++;
                        }

                        if (meta != null ? meta.isLiked : book.isLiked)
                        {
                            liked++;
                        }
                    }
                }
            }

            var sectionCount = bookshelfGenerator.ActiveBookshelfSectionCount;
            var emptySectionCount = bookshelfGenerator.ActiveBookshelfEmptySectionCount;
            var sectionSummary = sectionCount > 0
                ? $"빈 섹션 {emptySectionCount}/{sectionCount}"
                : "섹션 정보 없음";

            return $"{bookshelfGenerator.GetBookshelfName(activeIndex)} · {total}권 · {sectionSummary}   읽는 중 {reading}   완독 {read}   좋아요 {liked}";
        }

        private void SortActiveBookshelfBooks(ShelfBookSortMode sortMode)
        {
            if (!CanMutateBookshelfCollection() || state == null || state.Sections.Count == 0)
            {
                return;
            }

            var visualSections = new List<ShelfSection>();
            var originalBySection = new Dictionary<ShelfSection, List<BookEntity>>();
            var originalBooks = new List<BookEntity>();
            for (var i = 0; i < state.Sections.Count; i++)
            {
                var section = state.Sections[i];
                if (section == null)
                {
                    continue;
                }

                visualSections.Add(section);
                originalBySection[section] = new List<BookEntity>(section.books);
                for (var j = 0; j < section.books.Count; j++)
                {
                    var book = section.books[j];
                    if (book != null && !originalBooks.Contains(book))
                    {
                        originalBooks.Add(book);
                    }
                }
            }

            if (originalBooks.Count < 2)
            {
                RefreshBookshelfManagePanel();
                return;
            }

            var sortedBooks = new List<BookEntity>(originalBooks);
            sortedBooks.Sort((left, right) => CompareBooksForSort(left, right, sortMode));
            if (AreBooksInSameOrder(originalBooks, sortedBooks))
            {
                RefreshBookshelfManagePanel();
                return;
            }

            visualSections.Sort(CompareShelfSectionsForVisualOrder);
            for (var i = 0; i < state.Sections.Count; i++)
            {
                state.Sections[i]?.books.Clear();
            }

            for (var i = 0; i < sortedBooks.Count; i++)
            {
                sortedBooks[i]?.SetSection(null);
            }

            var sectionCursor = 0;
            for (var i = 0; i < sortedBooks.Count; i++)
            {
                var book = sortedBooks[i];
                if (book == null)
                {
                    continue;
                }

                var placed = false;
                for (var sectionIndex = sectionCursor; sectionIndex < visualSections.Count; sectionIndex++)
                {
                    var targetSection = visualSections[sectionIndex];
                    if (state.TryPlaceBookAt(book, targetSection, targetSection.books.Count, state, false))
                    {
                        sectionCursor = sectionIndex;
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    RestoreBookshelfSectionOrder(originalBySection);
                    state.LayoutAll(state, true);
                    RefreshHud();
                    return;
                }
            }

            state.LayoutAll(state, true);
            MarkBookshelfEditDirty();
            RefreshHud();
        }

        private void RestoreBookshelfSectionOrder(Dictionary<ShelfSection, List<BookEntity>> originalBySection)
        {
            if (state == null || originalBySection == null)
            {
                return;
            }

            for (var i = 0; i < state.Sections.Count; i++)
            {
                var section = state.Sections[i];
                if (section == null)
                {
                    continue;
                }

                section.books.Clear();
                if (!originalBySection.TryGetValue(section, out var originalBooks))
                {
                    continue;
                }

                for (var j = 0; j < originalBooks.Count; j++)
                {
                    var book = originalBooks[j];
                    if (book == null)
                    {
                        continue;
                    }

                    section.books.Add(book);
                    book.SetSection(section);
                }
            }
        }

        private int CompareBooksForSort(BookEntity left, BookEntity right, ShelfBookSortMode sortMode)
        {
            if (left == right)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var primary = 0;
            switch (sortMode)
            {
                case ShelfBookSortMode.Author:
                    primary = CompareSortText(GetBookSortAuthor(left), GetBookSortAuthor(right));
                    break;
                case ShelfBookSortMode.ReadingStatus:
                    primary = GetReadingStatusSortWeight(GetBookReadingStatus(left)).CompareTo(GetReadingStatusSortWeight(GetBookReadingStatus(right)));
                    break;
                case ShelfBookSortMode.Liked:
                    var leftLiked = IsBookLiked(left);
                    var rightLiked = IsBookLiked(right);
                    primary = leftLiked == rightLiked ? 0 : leftLiked ? -1 : 1;
                    break;
                default:
                    primary = CompareSortText(TryGetBookDisplayTitle(left), TryGetBookDisplayTitle(right));
                    break;
            }

            if (primary != 0)
            {
                return primary;
            }

            var titleCompare = CompareSortText(TryGetBookDisplayTitle(left), TryGetBookDisplayTitle(right));
            return titleCompare != 0 ? titleCompare : CompareSortText(left.bookId, right.bookId);
        }

        private string GetBookSortAuthor(BookEntity book)
        {
            if (book == null)
            {
                return string.Empty;
            }

            var meta = dataStore != null ? dataStore.GetOrCreateBookMeta(book) : null;
            return meta != null && !string.IsNullOrWhiteSpace(meta.author)
                ? meta.author
                : book.author;
        }

        private App.Domain.ReadingStatus GetBookReadingStatus(BookEntity book)
        {
            if (book == null)
            {
                return App.Domain.ReadingStatus.Unread;
            }

            var meta = dataStore != null ? dataStore.GetOrCreateBookMeta(book) : null;
            return meta != null ? meta.readingStatus : book.readingStatus;
        }

        private bool IsBookLiked(BookEntity book)
        {
            if (book == null)
            {
                return false;
            }

            var meta = dataStore != null ? dataStore.GetOrCreateBookMeta(book) : null;
            return meta != null ? meta.isLiked : book.isLiked;
        }

        private static int GetReadingStatusSortWeight(App.Domain.ReadingStatus status)
        {
            switch (status)
            {
                case App.Domain.ReadingStatus.Reading:
                    return 0;
                case App.Domain.ReadingStatus.Unread:
                    return 1;
                case App.Domain.ReadingStatus.Read:
                    return 2;
                default:
                    return 3;
            }
        }

        private static int CompareShelfSectionsForVisualOrder(ShelfSection left, ShelfSection right)
        {
            if (left == right)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var rowCompare = right.row.CompareTo(left.row);
            return rowCompare != 0 ? rowCompare : left.column.CompareTo(right.column);
        }

        private static bool AreBooksInSameOrder(IReadOnlyList<BookEntity> left, IReadOnlyList<BookEntity> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (var i = 0; i < left.Count; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static int CompareSortText(string left, string right)
        {
            var leftEmpty = string.IsNullOrWhiteSpace(left);
            var rightEmpty = string.IsNullOrWhiteSpace(right);
            if (leftEmpty && rightEmpty)
            {
                return 0;
            }

            if (leftEmpty)
            {
                return 1;
            }

            if (rightEmpty)
            {
                return -1;
            }

            return string.Compare(left.Trim(), right.Trim(), System.StringComparison.CurrentCultureIgnoreCase);
        }

        private void RefreshBookshelfStatusPanel()
        {
            if (bookshelfStatusPanel == null)
            {
                return;
            }

            var isBookshelfFocused = CurrentFocusArea == ExperienceFocusArea.Bookshelf;
            var isViewingBook = state != null && state.IsViewingBook;
            var isDraggingBook = state != null && state.ActiveDraggedBook != null;
            var isSwitching = bookshelfGenerator != null && bookshelfGenerator.IsBookshelfSwitching;
            var showPlacementSelection = isBookshelfFocused &&
                                         isBookAddPlacementSelectionActive &&
                                         !isViewingBook &&
                                         !isDraggingBook &&
                                         !IsBookshelfEditMode &&
                                         !isSwitching;
            var showPendingRetries = isBookshelfFocused &&
                                     pendingMutationRetries.Count > 0 &&
                                     !isViewingBook &&
                                     !isDraggingBook &&
                                     !IsBookshelfEditMode &&
                                     !isBookAddPlacementSelectionActive &&
                                     !isBookSearchPanelOpen &&
                                     !isBookAddPanelOpen &&
                                     !isBookAddConfirmPanelOpen &&
                                     !isBookshelfManagePanelOpen &&
                                     !isBookPlacementEditorOpen &&
                                     !HasOpenBookshelfModal() &&
                                     !isSwitching;
            var showTransient = isBookshelfFocused &&
                                IsBookshelfTransientStatusActive() &&
                                !isViewingBook &&
                                !isDraggingBook &&
                                !IsBookshelfEditMode &&
                                !isBookAddPlacementSelectionActive &&
                                !isBookSearchPanelOpen &&
                                !isBookAddPanelOpen &&
                                !isBookAddConfirmPanelOpen &&
                                !isBookshelfManagePanelOpen &&
                                !HasOpenBookshelfModal() &&
                                !isSwitching;
            var showServerPlacementPending = isBookshelfFocused &&
                                             !isViewingBook &&
                                             !isDraggingBook &&
                                             !IsBookshelfEditMode &&
                                             !isBookAddPlacementSelectionActive &&
                                             !isBookSearchPanelOpen &&
                                             !isBookAddPanelOpen &&
                                             !isBookAddConfirmPanelOpen &&
                                             !isBookshelfManagePanelOpen &&
                                             !isBookPlacementEditorOpen &&
                                             !HasOpenBookshelfModal() &&
                                             bookshelfGenerator != null &&
                                             bookshelfGenerator.BookshelfCount > 0 &&
                                             !CanUseBookshelfServerPlacement(bookshelfGenerator.DisplayBookshelfIndex);
            var hasNoBookshelves = bookshelfGenerator != null && bookshelfGenerator.BookshelfCount <= 0;
            var canShowEmptyState = isBookshelfFocused &&
                                    !isViewingBook &&
                                    !isDraggingBook &&
                                    !IsBookshelfEditMode &&
                                    !isBookAddPlacementSelectionActive &&
                                    !isBookSearchPanelOpen &&
                                    !isBookPlacementEditorOpen &&
                                    !HasOpenBookshelfModal() &&
                                    bookshelfGenerator != null;
            var showNoBookshelf = canShowEmptyState && hasNoBookshelves;
            var showEmpty = canShowEmptyState && !hasNoBookshelves && bookshelfGenerator.ActiveBookshelfBookCount == 0;
            var showSwitching = isBookshelfFocused && isSwitching;
            var show = showPlacementSelection || showPendingRetries || showTransient || showServerPlacementPending || showNoBookshelf || showEmpty || showSwitching;
            bookshelfStatusPanel.SetActive(show);
            if (!show)
            {
                return;
            }

            if (bookshelfStatusTitleText != null)
            {
                bookshelfStatusTitleText.text = showSwitching
                    ? "책장 이동 중"
                    : showPlacementSelection
                        ? "책 추가"
                        : showPendingRetries
                            ? $"{pendingMutationRetries.Count}개 변경 저장 필요"
                            : showTransient
                                ? bookshelfTransientStatusTitle
                                : showServerPlacementPending
                                    ? "책장 저장 중"
                                    : showNoBookshelf
                                        ? "첫 책장 필요"
                                        : "빈 책장";
                bookshelfStatusTitleText.color = (showPendingRetries || showTransient && bookshelfTransientStatusIsError)
                    ? new Color32(112, 46, 42, 255)
                    : showTransient
                        ? new Color32(44, 84, 60, 255)
                        : new Color32(49, 42, 36, 255);
            }

            if (bookshelfStatusMessageText != null)
            {
                if (showSwitching)
                {
                    bookshelfStatusMessageText.text = "새 책장 화면을 준비하고 있습니다.";
                }
                else if (showPlacementSelection)
                {
                    var selectedAddItem = pendingBookAddCatalogItem;
                    var title = selectedAddItem != null ? selectedAddItem.Title : "선택한 책";
                    bookshelfStatusMessageText.text = string.IsNullOrWhiteSpace(bookAddFeedbackMessage)
                        ? $"{title}을(를) 꽂을 칸이나 책 사이를 클릭하세요. Esc 또는 우클릭으로 취소할 수 있습니다."
                        : bookAddFeedbackMessage;
                }
                else if (showPendingRetries)
                {
                    bookshelfStatusMessageText.text = GetPendingMutationRetryStatusMessage();
                }
                else if (showTransient)
                {
                    bookshelfStatusMessageText.text = bookshelfTransientStatusMessage;
                }
                else if (showServerPlacementPending)
                {
                    bookshelfStatusMessageText.text = "새 책장/섹션 정보를 서버에서 불러오는 중입니다. 완료되면 책을 추가할 수 있습니다.";
                }
                else if (showNoBookshelf)
                {
                    bookshelfStatusMessageText.text = "아직 책장이 없습니다. 화면 가운데 + 버튼이나 아래 버튼으로 첫 책장을 추가하세요.";
                }
                else
                {
                    var sectionCount = bookshelfGenerator != null ? bookshelfGenerator.ActiveBookshelfSectionCount : 0;
                    var emptyCount = bookshelfGenerator != null ? bookshelfGenerator.ActiveBookshelfEmptySectionCount : 0;
                    bookshelfStatusMessageText.text = sectionCount > 0
                        ? $"빈 섹션 {emptyCount}/{sectionCount}개가 열려 있습니다. 여기에 첫 책을 추가해보세요."
                        : "여기에 첫 책을 추가해보세요.";
                }
            }

            if (bookshelfStatusAddButton != null)
            {
                var showActionButton = showPlacementSelection || showPendingRetries || showTransient || showServerPlacementPending || showNoBookshelf || showEmpty;
                bookshelfStatusAddButton.gameObject.SetActive(showActionButton);
                if (showActionButton)
                {
                    bookshelfStatusAddButton.onClick.RemoveAllListeners();
                    if (showPlacementSelection)
                    {
                        bookshelfStatusAddButton.onClick.AddListener(CancelBookAddPlacementSelectionFromStatusPanel);
                    }
                    else if (showPendingRetries)
                    {
                        bookshelfStatusAddButton.onClick.AddListener(RunAllPendingMutationRetriesFromStatusPanel);
                    }
                    else if (showTransient)
                    {
                        bookshelfStatusAddButton.onClick.AddListener(CloseBookshelfTransientStatusFromStatusPanel);
                    }
                    else if (showServerPlacementPending)
                    {
                        bookshelfStatusAddButton.onClick.AddListener(ShowBookshelfServerPlacementPending);
                    }
                    else if (showNoBookshelf)
                    {
                        bookshelfStatusAddButton.onClick.AddListener(AddEmptyBookshelfFromPlaceholder);
                    }
                    else
                    {
                        bookshelfStatusAddButton.onClick.AddListener(OpenBookAddFromStatusPanel);
                    }

                    SetButtonLabel(bookshelfStatusAddButton, showPlacementSelection ? "취소" : showPendingRetries ? "전체 재시도" : showTransient ? "닫기" : showServerPlacementPending ? "새로고침" : showNoBookshelf ? "책장 추가" : "책 추가");
                }

                StyleHudButton(
                    bookshelfStatusAddButton,
                    (showPendingRetries || showTransient && bookshelfTransientStatusIsError) ? new Color32(255, 235, 232, 238) : new Color32(238, 248, 241, 235),
                    (showPendingRetries || showTransient && bookshelfTransientStatusIsError) ? new Color32(255, 242, 238, 255) : new Color32(247, 255, 250, 255),
                    new Color32(224, 219, 214, 140),
                    13,
                    (showPendingRetries || showTransient && bookshelfTransientStatusIsError) ? new Color32(112, 46, 42, 255) : new Color32(53, 94, 73, 255),
                    true);
            }
        }

        private void OpenBookAddFromStatusPanel()
        {
            if (CurrentFocusArea != ExperienceFocusArea.Bookshelf)
            {
                return;
            }

            OpenBookAddPanel();
        }

        private void CollectActiveShelfBooks(List<BookEntity> target)
        {
            target.Clear();
            if (state == null)
            {
                return;
            }

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
                    if (book != null && !target.Contains(book))
                    {
                        target.Add(book);
                    }
                }
            }
        }

        private bool DoesBookMatchSearch(BookEntity book, string query, BookSearchFilter filter)
        {
            if (book == null)
            {
                return false;
            }

            var meta = dataStore != null ? dataStore.GetOrCreateBookMeta(book) : null;
            if (filter == BookSearchFilter.Reading && (meta == null || meta.readingStatus != App.Domain.ReadingStatus.Reading))
            {
                return false;
            }

            if (filter == BookSearchFilter.Read && (meta == null || meta.readingStatus != App.Domain.ReadingStatus.Read))
            {
                return false;
            }

            if (filter == BookSearchFilter.Liked && (meta == null || !meta.isLiked))
            {
                return false;
            }

            var normalizedQuery = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
            if (string.IsNullOrEmpty(normalizedQuery))
            {
                return true;
            }

            return ContainsCatalogText(TryGetBookDisplayTitle(book), normalizedQuery) ||
                   ContainsCatalogText(meta != null ? meta.author : book.author, normalizedQuery) ||
                   ContainsCatalogText(meta != null ? meta.publisher : book.publisher, normalizedQuery);
        }

        private static void StyleBookSearchFilterButton(Button button, bool isActive)
        {
            StyleHudButton(
                button,
                isActive ? new Color32(45, 50, 58, 245) : new Color32(255, 255, 255, 235),
                isActive ? new Color32(64, 70, 79, 255) : new Color32(241, 235, 230, 255),
                new Color32(224, 219, 214, 140),
                12,
                isActive ? Color.white : new Color32(63, 47, 38, 255),
                true);
        }

        private void RefreshBookshelfSelectorButtons(Button[] buttons, int activeIndex, bool canInteract)
        {
            if (buttons == null)
            {
                return;
            }

            var count = bookshelfGenerator != null ? bookshelfGenerator.BookshelfCount : 0;
            var visibleCount = Mathf.Min(count, buttons.Length);
            for (var i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button == null)
                {
                    continue;
                }

                var show = i < visibleCount;
                button.gameObject.SetActive(show);
                if (!show)
                {
                    continue;
                }

                var isActive = i == activeIndex;
                button.interactable = canInteract && !isActive && (bookshelfGenerator == null || !bookshelfGenerator.IsBookshelfSwitching);
                SetButtonLabel(button, $"{i + 1}번");
                StyleBookshelfSelectorButton(button, isActive);
            }
        }

        private static void StyleBookshelfSelectorButton(Button button, bool isActive)
        {
            StyleHudButton(
                button,
                isActive ? new Color32(197, 128, 67, 245) : new Color32(255, 255, 255, 235),
                isActive ? new Color32(214, 145, 82, 255) : new Color32(241, 235, 230, 255),
                new Color32(224, 219, 214, 140),
                12,
                isActive ? Color.white : new Color32(63, 47, 38, 255),
                isActive);
        }

        private static void StyleBookAddReadingStatusButton(Button button, bool isActive)
        {
            StyleHudButton(
                button,
                isActive ? new Color32(88, 133, 117, 245) : new Color32(255, 255, 255, 238),
                isActive ? new Color32(72, 114, 100, 255) : new Color32(247, 255, 250, 255),
                new Color32(224, 219, 214, 140),
                12,
                isActive ? Color.white : new Color32(44, 66, 53, 255),
                true);
        }

        private static void StyleBookshelfManageSortButton(Button button, bool isEnabled)
        {
            StyleHudButton(
                button,
                isEnabled ? new Color32(255, 255, 255, 238) : new Color32(224, 219, 214, 120),
                isEnabled ? new Color32(255, 248, 236, 255) : new Color32(224, 219, 214, 150),
                new Color32(224, 219, 214, 140),
                12,
                isEnabled ? new Color32(63, 47, 38, 255) : new Color32(142, 133, 124, 190),
                true);
        }

        private void StyleBookshelfManageDragHandle(int rowIndex, bool isVisible, bool isDragging, bool isDropTarget)
        {
            if (rowIndex < 0 || rowIndex >= bookshelfManageDragHandles.Length)
            {
                return;
            }

            var handle = bookshelfManageDragHandles[rowIndex];
            if (handle == null)
            {
                return;
            }

            var canDrag = isVisible && bookshelfGenerator != null && bookshelfGenerator.BookshelfCount > 1;
            handle.gameObject.SetActive(canDrag);
            handle.Configure(this, rowIndex);
            if (!canDrag)
            {
                return;
            }

            var image = bookshelfManageDragHandleImages[rowIndex];
            if (image == null)
            {
                image = handle.GetComponent<Image>();
                bookshelfManageDragHandleImages[rowIndex] = image;
            }

            if (image != null)
            {
                ApplyRoundedSprite(image);
                image.color = isDragging
                    ? new Color32(197, 128, 67, 230)
                    : isDropTarget
                        ? new Color32(88, 133, 117, 210)
                        : new Color32(245, 237, 226, 228);
            }

            handle.SetLineColor(isDragging || isDropTarget
                ? Color.white
                : new Color32(84, 74, 66, 230));
        }

        private static string GetVisibilityLabel(App.Domain.VisibilityScope visibility)
        {
            switch (visibility)
            {
                case App.Domain.VisibilityScope.Followers:
                    return "친구 공개";
                case App.Domain.VisibilityScope.Private:
                    return "비공개";
                default:
                    return "공개";
            }
        }

        private IReadOnlyList<BookAddCatalogItem> GetBookAddCatalogItems(string query = null)
        {
            runtimeBookAddCatalog.Clear();
            var normalizedQuery = NormalizeBookAddSearchQuery(query);
            var hasServerSearchResults = bookAddApiCatalogMode == activeBookAddBrowseMode &&
                                         (activeBookAddBrowseMode != BookAddBrowseMode.Search ||
                                          (!string.IsNullOrEmpty(normalizedQuery) &&
                                           string.Equals(normalizedQuery, bookAddApiSearchQuery, System.StringComparison.OrdinalIgnoreCase))) &&
                                         apiBookAddCatalog.Count > 0;

            if (hasServerSearchResults)
            {
                for (var i = 0; i < apiBookAddCatalog.Count; i++)
                {
                    runtimeBookAddCatalog.Add(apiBookAddCatalog[i]);
                }
            }

            if (!usesHttpApiMode)
            {
                var appCatalog = bookshelfGenerator != null ? bookshelfGenerator.BookCatalogItems : null;
                if (appCatalog != null)
                {
                    for (var i = 0; i < appCatalog.Count; i++)
                    {
                        var item = CreateBookAddCatalogItem(appCatalog[i]);
                        if (item != null && !ContainsBookAddCatalogItem(runtimeBookAddCatalog, item))
                        {
                            runtimeBookAddCatalog.Add(item);
                        }
                    }
                }
            }

            return runtimeBookAddCatalog;
        }

        private BookAddCatalogItem GetBookAddCatalogItem(int index)
        {
            var query = bookAddSearchInputField != null ? bookAddSearchInputField.text : string.Empty;
            var catalog = GetBookAddCatalogItems(query);
            return index >= 0 && index < catalog.Count ? catalog[index] : null;
        }

        private static BookAddCatalogItem CreateBookAddCatalogItem(App.Application.Books.BookCatalogItem source)
        {
            var book = source?.Book;
            if (book == null || string.IsNullOrWhiteSpace(book.Title))
            {
                return null;
            }

            return new BookAddCatalogItem(
                book.Title,
                string.IsNullOrWhiteSpace(book.Author) ? "저자 미상" : book.Author,
                string.IsNullOrWhiteSpace(book.Publisher) ? "출판사 미상" : book.Publisher,
                string.IsNullOrWhiteSpace(book.Description) ? "책 설명이 없습니다." : book.Description,
                GetBookOrientationFromDisplayStyle(source.Placement?.DisplayStyle),
                book.BookId,
                "LOCAL",
                book.Isbn13,
                null,
                book.PublishedDate?.ToString("yyyy-MM-dd"),
                book.CoverImageUrl,
                null,
                book.ViewCount,
                book.LikeCount,
                book.BookshelfAddCount,
                book.AverageRating,
                book.RatingCount);
        }

        private static BookAddCatalogItem CreateBookAddCatalogItem(NotebookBookSearchItem source)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.Title))
            {
                return null;
            }

            var sourceType = string.IsNullOrWhiteSpace(source.Source) ? "LOCAL" : source.Source.Trim();
            var localBookId = string.Equals(sourceType, "KAKAO", System.StringComparison.OrdinalIgnoreCase)
                ? null
                : FirstNonEmpty(source.BookId, source.Id > 0 ? source.Id.ToString() : null);

            return new BookAddCatalogItem(
                source.Title,
                string.IsNullOrWhiteSpace(source.Author) ? "저자 미상" : source.Author,
                string.IsNullOrWhiteSpace(source.Publisher) ? "출판사 미상" : source.Publisher,
                string.IsNullOrWhiteSpace(source.Description) ? "책 설명이 없습니다." : source.Description,
                BookOrientation.Front,
                localBookId,
                sourceType,
                source.Isbn13,
                source.Isbn10,
                source.PublishedDate,
                source.CoverImageUrl,
                source.SourceUrl,
                source.ViewCount,
                source.LikeCount,
                source.BookshelfAddCount > 0 ? source.BookshelfAddCount : source.PlacementCount,
                source.AvgRating,
                source.ReviewCount);
        }

        private void RequestBookAddApiSearch(string query)
        {
            if (activeBookAddBrowseMode != BookAddBrowseMode.Search)
            {
                return;
            }

            var normalizedQuery = NormalizeBookAddSearchQuery(query);
            if (string.IsNullOrEmpty(normalizedQuery))
            {
                bookAddApiSearchVersion++;
                apiBookAddCatalog.Clear();
                bookAddApiSearchQuery = string.Empty;
                bookAddApiSearchMessage = usesHttpApiMode ? "검색어를 입력해 주세요." : string.Empty;
                isBookAddApiSearchPending = false;
                bookAddApiCatalogMode = BookAddBrowseMode.Search;
                return;
            }

            if (searchNotebookBooksUseCase == null)
            {
                apiBookAddCatalog.Clear();
                bookAddApiSearchMessage = usesHttpApiMode
                    ? "도서 검색 API가 준비되지 않았습니다."
                    : string.Empty;
                isBookAddApiSearchPending = false;
                return;
            }

            if (bookAddApiCatalogMode == BookAddBrowseMode.Search &&
                string.Equals(normalizedQuery, bookAddApiSearchQuery, System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var requestVersion = ++bookAddApiSearchVersion;
            bookAddApiSearchQuery = normalizedQuery;
            bookAddApiSearchMessage = string.Empty;
            isBookAddApiSearchPending = true;
            bookAddApiCatalogMode = BookAddBrowseMode.Search;
            apiBookAddCatalog.Clear();

            searchNotebookBooksUseCase.Execute(
                normalizedQuery,
                1,
                Mathf.Min(BookAddMaxResultCount, 50),
                result =>
                {
                    if (requestVersion != bookAddApiSearchVersion)
                    {
                        return;
                    }

                    apiBookAddCatalog.Clear();
                    var items = result?.Items;
                    if (items != null)
                    {
                        for (var i = 0; i < items.Count; i++)
                        {
                            var catalogItem = CreateBookAddCatalogItem(items[i]);
                            if (catalogItem != null && !ContainsBookAddCatalogItem(apiBookAddCatalog, catalogItem))
                            {
                                apiBookAddCatalog.Add(catalogItem);
                            }
                        }
                    }

                    isBookAddApiSearchPending = false;
                    bookAddApiSearchMessage = apiBookAddCatalog.Count == 0 ? "서버 검색 결과가 없습니다." : string.Empty;
                    RefreshHud();
                },
                error =>
                {
                    if (requestVersion != bookAddApiSearchVersion)
                    {
                        return;
                    }

                    apiBookAddCatalog.Clear();
                    isBookAddApiSearchPending = false;
                    bookAddApiSearchMessage = string.IsNullOrWhiteSpace(error?.Message)
                        ? "서버 검색을 불러오지 못했습니다."
                        : $"서버 검색 실패: {error.Message}";
                    RefreshHud();
                });
        }

        private void RequestBookAddMarketBooks(BookAddBrowseMode mode)
        {
            if (mode == BookAddBrowseMode.Search)
            {
                RequestBookAddApiSearch(bookAddSearchInputField != null ? bookAddSearchInputField.text : string.Empty);
                return;
            }

            if (loadNotebookMarketBooksUseCase == null)
            {
                bookAddApiSearchMessage = "서버 책 목록을 불러올 수 없습니다.";
                return;
            }

            if (isBookAddApiSearchPending && bookAddApiCatalogMode == mode)
            {
                return;
            }

            if (apiBookAddCatalog.Count > 0 && bookAddApiCatalogMode == mode)
            {
                return;
            }

            var requestVersion = ++bookAddApiSearchVersion;
            bookAddApiCatalogMode = mode;
            bookAddApiSearchQuery = string.Empty;
            bookAddApiSearchMessage = string.Empty;
            isBookAddApiSearchPending = true;
            apiBookAddCatalog.Clear();

            var category = mode == BookAddBrowseMode.Recommended ? "RECOMMENDED" : "RANKING";
            loadNotebookMarketBooksUseCase.Execute(
                category,
                Mathf.Min(BookAddMaxResultCount, 50),
                result =>
                {
                    if (requestVersion != bookAddApiSearchVersion || activeBookAddBrowseMode != mode)
                    {
                        return;
                    }

                    apiBookAddCatalog.Clear();
                    AddNotebookSearchItemsToBookAddCatalog(result?.Items, apiBookAddCatalog);
                    isBookAddApiSearchPending = false;
                    bookAddApiSearchMessage = apiBookAddCatalog.Count == 0
                        ? $"{GetBookAddBrowseModeLabel(mode)} 결과가 없습니다."
                        : string.Empty;
                    RefreshHud();
                },
                error =>
                {
                    if (requestVersion != bookAddApiSearchVersion || activeBookAddBrowseMode != mode)
                    {
                        return;
                    }

                    apiBookAddCatalog.Clear();
                    isBookAddApiSearchPending = false;
                    bookAddApiSearchMessage = string.IsNullOrWhiteSpace(error?.Message)
                        ? $"{GetBookAddBrowseModeLabel(mode)} 목록을 불러오지 못했습니다."
                        : $"{GetBookAddBrowseModeLabel(mode)} 실패: {error.Message}";
                    RefreshHud();
                });
        }

        private static void AddNotebookSearchItemsToBookAddCatalog(
            IReadOnlyList<NotebookBookSearchItem> items,
            List<BookAddCatalogItem> target)
        {
            if (items == null || target == null)
            {
                return;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var catalogItem = CreateBookAddCatalogItem(items[i]);
                if (catalogItem != null && !ContainsBookAddCatalogItem(target, catalogItem))
                {
                    target.Add(catalogItem);
                }
            }
        }

        private static void PrepareCatalogBookForApiCreation(BookEntity book, BookAddCatalogItem item)
        {
            if (book == null || item == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(item.BookId))
            {
                book.bookId = item.BookId;
            }

            book.source = string.IsNullOrWhiteSpace(item.Source) ? book.source : item.Source;
            book.isbn13 = item.Isbn13;
            book.isbn10 = item.Isbn10;
            book.publishedDate = item.PublishedDate;
            book.coverImageUrl = item.CoverImageUrl;
            book.sourceUrl = item.SourceUrl;
            book.title = string.IsNullOrWhiteSpace(item.Title) ? book.title : item.Title;
            book.author = string.IsNullOrWhiteSpace(item.Author) ? book.author : item.Author;
            book.publisher = string.IsNullOrWhiteSpace(item.Publisher) ? book.publisher : item.Publisher;
            book.description = string.IsNullOrWhiteSpace(item.Summary) ? book.description : item.Summary;
            book.memoPreview = book.description;

            // This helper is only for the add-book flow. It must create a new
            // server placement from the catalog bookId, so any catalog snapshot
            // IDs are cleared before BookshelfApiSyncController chooses the API.
            book.userBookId = null;
            book.bookshelfItemId = null;
            book.bookshelfId = null;
            book.bookshelfSectionId = null;
            book.readingRecordId = null;
        }

        private static string NormalizeBookAddSearchQuery(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string FirstNonEmpty(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static BookOrientation GetBookOrientationFromDisplayStyle(App.Domain.DisplayStyle? displayStyle)
        {
            switch (displayStyle)
            {
                case App.Domain.DisplayStyle.Angled45:
                    return BookOrientation.Angled45;
                case App.Domain.DisplayStyle.Front:
                    return BookOrientation.Front;
                default:
                    return BookOrientation.Spine;
            }
        }

        private static bool ContainsBookAddCatalogItem(IReadOnlyList<BookAddCatalogItem> catalog, BookAddCatalogItem target)
        {
            if (catalog == null || target == null)
            {
                return false;
            }

            var title = NormalizeCatalogIdentity(target.Title);
            var author = NormalizeCatalogIdentity(target.Author);
            for (var i = 0; i < catalog.Count; i++)
            {
                var item = catalog[i];
                if (item != null &&
                    NormalizeCatalogIdentity(item.Title) == title &&
                    NormalizeCatalogIdentity(item.Author) == author)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool DoesCatalogItemMatch(BookAddCatalogItem item, string query)
        {
            if (item == null)
            {
                return false;
            }

            var normalizedQuery = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
            if (string.IsNullOrEmpty(normalizedQuery))
            {
                return true;
            }

            return ContainsCatalogText(item.Title, normalizedQuery) ||
                   ContainsCatalogText(item.Author, normalizedQuery) ||
                   ContainsCatalogText(item.Publisher, normalizedQuery);
        }

        private bool IsCatalogBookAlreadyOnActiveShelf(int catalogIndex)
        {
            var item = GetBookAddCatalogItem(catalogIndex);
            return IsCatalogBookAlreadyOnActiveShelf(item);
        }

        private bool IsCatalogBookAlreadyOnActiveShelf(BookAddCatalogItem item)
        {
            if (bookshelfGenerator == null ||
                item == null)
            {
                return false;
            }

            return bookshelfGenerator.ActiveBookshelfContainsBook(item.Title, item.Author);
        }

        private string GetCatalogBookDuplicateMessage(int catalogIndex)
        {
            var item = GetBookAddCatalogItem(catalogIndex);
            return GetCatalogBookDuplicateMessage(item);
        }

        private static string GetCatalogBookDuplicateMessage(BookAddCatalogItem item)
        {
            var title = item != null ? item.Title : "선택한 책";
            return $"{title}은(는) 이미 현재 책장에 있습니다.";
        }

        private static string NormalizeCatalogIdentity(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        private static bool ContainsCatalogText(string source, string query)
        {
            return !string.IsNullOrEmpty(source) &&
                   source.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetBookAddBrowseModeLabel(BookAddBrowseMode mode)
        {
            return mode switch
            {
                BookAddBrowseMode.Recommended => "추천",
                BookAddBrowseMode.Ranking => "랭킹",
                _ => "검색"
            };
        }

        private static string BuildBookAddResultLabel(BookAddCatalogItem item, bool isAlreadyAdded)
        {
            if (item == null)
            {
                return string.Empty;
            }

            var title = FirstNonEmpty(item.Title, "제목 없음");
            var meta = $"{FirstNonEmpty(item.Author, "저자 미상")} · {FirstNonEmpty(item.Publisher, "출판사 미상")}";
            var summary = TruncateBookAddResultText(FirstNonEmpty(item.Summary, "책 설명이 없습니다."), 78);
            var stats = BuildBookAddStatsLine(item);
            var suffix = isAlreadyAdded ? "\n이미 이 책장에 추가됨" : string.Empty;
            return string.IsNullOrWhiteSpace(stats)
                ? $"{title}\n{meta}\n{summary}{suffix}"
                : $"{title}\n{meta}\n{summary}\n{stats}{suffix}";
        }

        private static string BuildBookAddStatsLine(BookAddCatalogItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            var stats = new List<string>(4);
            if (item.ViewCount > 0L)
            {
                stats.Add($"조회 {FormatCompactCount(item.ViewCount)}");
            }

            if (item.LikeCount > 0L)
            {
                stats.Add($"좋아요 {FormatCompactCount(item.LikeCount)}");
            }

            if (item.BookshelfAddCount > 0L)
            {
                stats.Add($"책장 추가 {FormatCompactCount(item.BookshelfAddCount)}");
            }

            if (item.AverageRating > 0m)
            {
                stats.Add($"평점 {item.AverageRating:0.0}");
            }

            return stats.Count == 0 ? string.Empty : string.Join(" · ", stats);
        }

        private static string TruncateBookAddResultText(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var compact = value.Replace("\r", " ").Replace("\n", " ").Trim();
            while (compact.Contains("  "))
            {
                compact = compact.Replace("  ", " ");
            }

            if (compact.Length <= maxLength)
            {
                return compact;
            }

            return compact.Substring(0, Mathf.Max(0, maxLength - 1)).TrimEnd() + "...";
        }

        private static string FormatCompactCount(long count)
        {
            if (count >= 100000000L)
            {
                return $"{count / 100000000d:0.#}억";
            }

            if (count >= 10000L)
            {
                return $"{count / 10000d:0.#}만";
            }

            return count.ToString("N0");
        }

        private void LayoutBookAddResultRow(int slotIndex, BookAddCatalogItem item)
        {
            if (slotIndex < 0 || slotIndex >= bookAddResultButtons.Length)
            {
                return;
            }

            var label = bookAddResultTexts[slotIndex];
            if (label != null)
            {
                label.alignment = TextAnchor.UpperLeft;
                label.fontSize = 12;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.lineSpacing = 1.05f;
                label.rectTransform.offsetMin = new Vector2(94f, 8f);
                label.rectTransform.offsetMax = new Vector2(-18f, -8f);
            }

            BindBookAddCoverImage(slotIndex, item);
        }

        private void BindBookAddCoverImage(int slotIndex, BookAddCatalogItem item)
        {
            if (slotIndex < 0 || slotIndex >= bookAddResultCoverImages.Length)
            {
                return;
            }

            var image = bookAddResultCoverImages[slotIndex];
            if (image == null)
            {
                return;
            }

            var url = item != null ? item.CoverImageUrl : null;
            if (bookAddCoverImageUrls.TryGetValue(image, out var currentUrl) &&
                string.Equals(currentUrl, url ?? string.Empty, System.StringComparison.Ordinal) &&
                (string.IsNullOrWhiteSpace(url) || image.sprite != null))
            {
                return;
            }

            StopBookAddCoverLoad(image);
            bookAddCoverImageUrls[image] = url ?? string.Empty;
            ResetBookAddCoverImage(image);
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            if (TryGetBookAddCoverSprite(url, out var cachedSprite))
            {
                ApplyBookAddCoverSprite(image, cachedSprite);
                return;
            }

            bookAddCoverLoadCoroutines[image] = StartCoroutine(LoadBookAddCoverImage(image, url));
        }

        private IEnumerator LoadBookAddCoverImage(Image image, string url)
        {
            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                request.timeout = 8;
                yield return request.SendWebRequest();

                bookAddCoverLoadCoroutines.Remove(image);
                if (image == null ||
                    !bookAddCoverImageUrls.TryGetValue(image, out var activeUrl) ||
                    !string.Equals(activeUrl, url, System.StringComparison.Ordinal))
                {
                    yield break;
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    yield break;
                }

                var texture = DownloadHandlerTexture.GetContent(request);
                if (texture == null)
                {
                    yield break;
                }

                var sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                CacheBookAddCoverSprite(url, sprite);
                ApplyBookAddCoverSprite(image, sprite);
            }
        }

        private static void ApplyBookAddCoverSprite(Image image, Sprite sprite)
        {
            if (image == null || sprite == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
        }

        private static void ResetBookAddCoverImage(Image image)
        {
            if (image == null)
            {
                return;
            }

            ApplyRoundedSprite(image);
            image.sprite = GetRoundedUiSprite();
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
            image.color = new Color32(238, 231, 221, 240);
        }

        private void StopBookAddCoverLoad(Image image)
        {
            if (image == null)
            {
                return;
            }

            if (bookAddCoverLoadCoroutines.TryGetValue(image, out var coroutine) && coroutine != null)
            {
                StopCoroutine(coroutine);
            }

            bookAddCoverLoadCoroutines.Remove(image);
        }

        private void CancelBookAddCoverLoads()
        {
            foreach (var pair in bookAddCoverLoadCoroutines)
            {
                if (pair.Value != null)
                {
                    StopCoroutine(pair.Value);
                }
            }

            bookAddCoverLoadCoroutines.Clear();
        }

        private bool TryGetBookAddCoverSprite(string url, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            if (!bookAddCoverSpriteCache.TryGetValue(url, out sprite) || sprite == null)
            {
                RemoveBookAddCoverSpriteCacheEntry(url);
                return false;
            }

            TouchBookAddCoverSpriteCacheKey(url);
            return true;
        }

        private void CacheBookAddCoverSprite(string url, Sprite sprite)
        {
            if (string.IsNullOrWhiteSpace(url) || sprite == null)
            {
                return;
            }

            if (bookAddCoverSpriteCache.TryGetValue(url, out var previous) && previous != null && previous != sprite)
            {
                DestroyBookAddCoverSprite(previous);
            }

            bookAddCoverSpriteCache[url] = sprite;
            TouchBookAddCoverSpriteCacheKey(url);
            TrimBookAddCoverSpriteCache();
        }

        private void TouchBookAddCoverSpriteCacheKey(string url)
        {
            for (var node = bookAddCoverSpriteLru.First; node != null; node = node.Next)
            {
                if (!string.Equals(node.Value, url, System.StringComparison.Ordinal))
                {
                    continue;
                }

                bookAddCoverSpriteLru.Remove(node);
                break;
            }

            bookAddCoverSpriteLru.AddLast(url);
        }

        private void TrimBookAddCoverSpriteCache()
        {
            while (bookAddCoverSpriteCache.Count > BookAddCoverSpriteCacheLimit && bookAddCoverSpriteLru.First != null)
            {
                RemoveBookAddCoverSpriteCacheEntry(bookAddCoverSpriteLru.First.Value);
            }
        }

        private void RemoveBookAddCoverSpriteCacheEntry(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            if (bookAddCoverSpriteCache.TryGetValue(url, out var sprite))
            {
                DestroyBookAddCoverSprite(sprite);
                bookAddCoverSpriteCache.Remove(url);
            }

            for (var node = bookAddCoverSpriteLru.First; node != null;)
            {
                var next = node.Next;
                if (string.Equals(node.Value, url, System.StringComparison.Ordinal))
                {
                    bookAddCoverSpriteLru.Remove(node);
                }

                node = next;
            }
        }

        private void ClearBookAddCoverSpriteCache()
        {
            foreach (var pair in bookAddCoverSpriteCache)
            {
                DestroyBookAddCoverSprite(pair.Value);
            }

            bookAddCoverSpriteCache.Clear();
            bookAddCoverSpriteLru.Clear();
        }

        private static void DestroyBookAddCoverSprite(Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            var texture = sprite.texture;
            Destroy(sprite);
            if (texture != null)
            {
                Destroy(texture);
            }
        }

        private static void StyleBookAddBrowseButton(Button button, bool isActive)
        {
            StyleHudButton(
                button,
                isActive ? new Color32(88, 133, 117, 245) : new Color32(255, 255, 255, 235),
                isActive ? new Color32(105, 150, 132, 255) : new Color32(247, 255, 250, 255),
                new Color32(224, 219, 214, 140),
                13,
                isActive ? Color.white : new Color32(53, 94, 73, 255),
                isActive);
        }

        private static void StyleBookAddResultButton(Button button, bool isAvailable = true)
        {
            StyleHudButton(
                button,
                isAvailable ? new Color32(255, 255, 255, 238) : new Color32(242, 237, 229, 210),
                isAvailable ? new Color32(247, 255, 250, 255) : new Color32(242, 237, 229, 210),
                new Color32(224, 219, 214, 140),
                13,
                isAvailable ? new Color32(44, 66, 53, 255) : new Color32(138, 126, 114, 255),
                false);
            if (button != null)
            {
                AddSoftOutline(
                    button.gameObject,
                    isAvailable ? new Color32(193, 215, 199, 150) : new Color32(224, 219, 214, 130),
                    new Vector2(1f, -1f));
            }

            var label = button != null ? button.GetComponentInChildren<Text>() : null;
            if (label != null)
            {
                label.alignment = TextAnchor.MiddleLeft;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.lineSpacing = 1.04f;
            }
        }

        private void RefreshBookshelfDesignPanel(int displayIndex)
        {
            if (!isBookshelfDesignPanelOpen ||
                bookshelfGenerator == null ||
                bookshelfDesignPanel == null)
            {
                return;
            }

            var style = bookshelfGenerator.GetBookshelfDesignStyle(displayIndex);
            StyleBookshelfDesignButton(defaultBookshelfDesignButton, BookshelfThemeStyle.Default, style == BookshelfThemeStyle.Default, CanUseBookshelfDesignStyle(BookshelfThemeStyle.Default));
            StyleBookshelfDesignButton(warmBookshelfDesignButton, BookshelfThemeStyle.Warm, style == BookshelfThemeStyle.Warm, CanUseBookshelfDesignStyle(BookshelfThemeStyle.Warm));
            StyleBookshelfDesignButton(darkBookshelfDesignButton, BookshelfThemeStyle.Dark, style == BookshelfThemeStyle.Dark, CanUseBookshelfDesignStyle(BookshelfThemeStyle.Dark));
            StyleHudButton(
                closeBookshelfDesignButton,
                new Color32(245, 237, 226, 245),
                new Color32(255, 248, 236, 255),
                new Color32(224, 219, 214, 140),
                14,
                new Color32(84, 74, 66, 255),
                true);
        }

        private void SetBookshelfDesignStyle(BookshelfThemeStyle style)
        {
            if (!CanMutateBookshelfCollection())
            {
                return;
            }

            var displayIndex = bookshelfGenerator != null ? bookshelfGenerator.DisplayBookshelfIndex : -1;
            if (!EnsureBookshelfApiMutationReady(displayIndex, "책장 디자인 변경"))
            {
                RefreshHud();
                return;
            }

            if (usesHttpApiMode && !BookshelfSkinResolver.ResolveInventoryId(style).HasValue)
            {
                bookshelfApiSyncController?.RefreshBookshelfSkinCatalog();
                var message = BookshelfSkinResolver.HasLoadedApiAssets
                    ? "해당 책장 디자인은 서버 카탈로그에 없어 저장할 수 없습니다."
                    : "서버 책장 스킨 목록을 불러오는 중입니다. 잠시 후 다시 시도해 주세요.";
                ShowBookshelfTransientStatus(
                    "책장 디자인 변경 불가",
                    message,
                    true);
                RefreshHud();
                return;
            }

            var previous = displayIndex >= 0 ? bookshelfGenerator.GetBookshelfDesignStyle(displayIndex) : style;
            if (bookshelfGenerator.SetActiveBookshelfDesignStyle(style))
            {
                if (previous != style)
                {
                    MarkBookshelfEditDirty();
                    SyncBookshelfUpdateOrRefresh(displayIndex, "책장 디자인 변경");
                }

                RefreshHud();
            }
        }

        private bool CanUseBookshelfDesignStyle(BookshelfThemeStyle style)
        {
            if (!usesHttpApiMode)
            {
                return true;
            }

            if (!BookshelfSkinResolver.HasLoadedApiAssets)
            {
                return true;
            }

            return BookshelfSkinResolver.HasResolvableInventoryId(style);
        }

        private static void StyleBookshelfDesignButton(Button button, BookshelfThemeStyle style, bool isActive, bool isEnabled)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = isEnabled;

            Color32 background;
            Color32 highlighted;
            Color32 textColor;
            switch (style)
            {
                case BookshelfThemeStyle.Warm:
                    background = isActive ? new Color32(220, 151, 90, 255) : new Color32(255, 237, 210, 245);
                    highlighted = new Color32(239, 171, 106, 255);
                    textColor = isActive ? Color.white : new Color32(122, 70, 34, 255);
                    break;
                case BookshelfThemeStyle.Dark:
                    background = isActive ? new Color32(86, 67, 55, 255) : new Color32(232, 224, 216, 245);
                    highlighted = new Color32(105, 83, 68, 255);
                    textColor = isActive ? Color.white : new Color32(70, 54, 45, 255);
                    break;
                default:
                    background = isActive ? new Color32(197, 128, 67, 255) : new Color32(255, 255, 255, 235);
                    highlighted = isActive ? new Color32(214, 145, 82, 255) : new Color32(241, 235, 230, 255);
                    textColor = isActive ? Color.white : new Color32(63, 47, 38, 255);
                    break;
            }

            StyleHudButton(
                button,
                background,
                highlighted,
                new Color32(224, 219, 214, 140),
                14,
                textColor,
                isEnabled);
        }

        private void RefreshBookshelfDeleteConfirmPanel(int displayIndex)
        {
            if (!isBookshelfDeleteConfirmPanelOpen ||
                bookshelfGenerator == null ||
                bookshelfDeleteConfirmPanel == null)
            {
                return;
            }

            var bookshelfName = bookshelfGenerator.GetBookshelfName(displayIndex);
            var bookCount = Mathf.Max(0, bookshelfGenerator.ActiveBookshelfBookCount);
            if (bookshelfDeleteConfirmMessageText != null)
            {
                bookshelfDeleteConfirmMessageText.text =
                    bookCount > 0
                        ? $"{bookshelfName}을 정말로 삭제하시겠습니까?\n이 책장에 있는 책 {bookCount}권은 인접한 책장으로 이동됩니다."
                        : $"{bookshelfName}을 정말로 삭제하시겠습니까?";
            }

            StyleDeleteActionButton(confirmDeleteBookshelfButton, true, true, 14);
            StyleHudButton(
                cancelDeleteBookshelfButton,
                new Color32(245, 237, 226, 245),
                new Color32(255, 248, 236, 255),
                new Color32(224, 219, 214, 140),
                14,
                new Color32(84, 74, 66, 255),
                true);
        }

        private void AddBookshelf()
        {
            if (isBookshelfCreateRequestInFlight || !CanMutateBookshelfCollection())
            {
                return;
            }

            var isFirstBookshelf = bookshelfGenerator != null && bookshelfGenerator.BookshelfCount <= 0;
            if (isFirstBookshelf)
            {
                QueueFirstBookshelfFeatureGuide();
                isFirstBookshelfCreateInProgress = true;
            }

            CloseBookshelfNameEditPanel();
            CloseBookshelfDeleteConfirmPanel();
            CloseBookshelfDesignPanel();
            CloseBookAddPanel(false);
            CloseBookAddConfirmPanelSilently();
            CloseOverlayPanels();
            pendingDeleteBookshelfIndex = -1;
            ActiveDetailBook = null;
            activeEditSection = null;

            if (usesHttpApiMode)
            {
                if (!EnsureBookshelfApiMutationReady("책장 추가"))
                {
                    if (isFirstBookshelf)
                    {
                        isFirstBookshelfCreateInProgress = false;
                    }

                    RefreshHud();
                    return;
                }

                ShowBookshelfTransientStatus("책장 저장 중", "새 책장을 서버에 생성하고 있습니다.", false);
                SyncBookshelfCreateOrRefresh(BuildDefaultNewBookshelfName(), "책장 추가");
                RefreshHud();
                return;
            }

            if (bookshelfGenerator.AddBookshelf())
            {
                if (isFirstBookshelf)
                {
                    isFirstBookshelfCreateInProgress = false;
                }

                MarkBookshelfEditDirty();
                SyncBookshelfCreateOrRefresh(bookshelfGenerator.DisplayBookshelfName, "책장 추가");
                RefreshHud();
            }
            else if (isFirstBookshelf)
            {
                isFirstBookshelfCreateInProgress = false;
            }
        }

        private void DeleteActiveBookshelf()
        {
            if (!CanMutateBookshelfCollection() || bookshelfGenerator.BookshelfCount <= 1)
            {
                pendingDeleteBookshelfIndex = -1;
                isBookshelfDeleteConfirmPanelOpen = false;
                RefreshHud();
                return;
            }

            var displayIndex = Mathf.Clamp(bookshelfGenerator.DisplayBookshelfIndex, 0, Mathf.Max(1, bookshelfGenerator.BookshelfCount) - 1);
            pendingDeleteBookshelfIndex = displayIndex;
            SetBookshelfOverlayMode(BookshelfOverlayMode.DeleteConfirm);
            RefreshHud();
        }

        private void ConfirmDeleteBookshelf()
        {
            if (!CanMutateBookshelfCollection() ||
                !isBookshelfDeleteConfirmPanelOpen ||
                bookshelfGenerator.BookshelfCount <= 1)
            {
                CloseBookshelfDeleteConfirmPanel();
                return;
            }

            var displayIndex = Mathf.Clamp(bookshelfGenerator.DisplayBookshelfIndex, 0, Mathf.Max(1, bookshelfGenerator.BookshelfCount) - 1);
            if (pendingDeleteBookshelfIndex != displayIndex)
            {
                CloseBookshelfDeleteConfirmPanel();
                return;
            }

            CloseBookshelfNameEditPanel();
            CloseBookshelfDesignPanel();
            CloseBookAddPanel(false);
            CloseBookAddConfirmPanelSilently();
            CloseOverlayPanels();
            ActiveDetailBook = null;
            activeEditSection = null;
            pendingDeleteBookshelfIndex = -1;
            isBookshelfDeleteConfirmPanelOpen = false;
            if (bookshelfDeleteConfirmPanel != null)
            {
                bookshelfDeleteConfirmPanel.SetActive(false);
            }

            if (bookshelfNameModalBackdrop != null)
            {
                bookshelfNameModalBackdrop.SetActive(false);
            }

            var deletedBookshelfServerId = bookshelfGenerator.GetBookshelfServerId(displayIndex);
            if (!EnsureBookshelfApiMutationReady(displayIndex, "책장 삭제"))
            {
                RefreshHud();
                return;
            }

            if (bookshelfGenerator.DeleteActiveBookshelf())
            {
                MarkBookshelfEditDirty();
                var didStartDeleteSync = bookshelfApiSyncController != null &&
                                         bookshelfApiSyncController.DeleteBookshelf(
                                             deletedBookshelfServerId,
                                             () => ShowDeleteResultDialog("책장 삭제 완료", "책장을 삭제했습니다.", true),
                                             error => RefreshServerStateAfterMutationFailure("책장 삭제", error));
                if (!didStartDeleteSync)
                {
                    if (bookshelfApiSyncController != null && bookshelfApiSyncController.IsServerSyncEnabled)
                    {
                        RefreshServerStateAfterMutationFailure("책장 삭제", "책장 서버 식별자를 찾을 수 없습니다.");
                    }
                    else
                    {
                        ShowDeleteResultDialog("책장 삭제 완료", "책장을 삭제했습니다.", true);
                    }
                }
            }

            RefreshHud();
        }

        private void CloseBookshelfDeleteConfirmPanel()
        {
            isBookshelfDeleteConfirmPanelOpen = false;
            pendingDeleteBookshelfIndex = -1;
            if (bookshelfDeleteConfirmPanel != null)
            {
                bookshelfDeleteConfirmPanel.SetActive(false);
            }

            if (bookshelfNameModalBackdrop != null && !HasOpenBookshelfModal())
            {
                bookshelfNameModalBackdrop.SetActive(false);
            }

            RefreshHud();
        }

        private bool CanMutateBookshelfCollection()
        {
            return CanMutateOwnedBookshelf() &&
                   IsBookshelfEditMode &&
                   CurrentFocusArea == ExperienceFocusArea.Bookshelf &&
                   bookshelfGenerator != null &&
                   !bookshelfGenerator.IsBookshelfSwitching &&
                   (state == null || (!state.IsViewingBook && state.ActiveDraggedBook == null));
        }

        private bool CanUseBookshelfServerPlacement(int bookshelfIndex)
        {
            if (bookshelfApiSyncController == null || !bookshelfApiSyncController.IsServerSyncEnabled)
            {
                return !usesHttpApiMode;
            }

            if (usesHttpApiMode && !bookshelfApiSyncController.HasAccessToken)
            {
                return false;
            }

            return bookshelfGenerator != null &&
                   bookshelfIndex >= 0 &&
                   bookshelfGenerator.HasServerPlacementIdsForBookshelf(bookshelfIndex);
        }

        private void ShowBookshelfServerPlacementPending()
        {
            if (usesHttpApiMode &&
                (bookshelfApiSyncController == null ||
                 !bookshelfApiSyncController.IsServerSyncEnabled ||
                 !bookshelfApiSyncController.HasAccessToken))
            {
                bookAddFeedbackMessage = "책장 API 동기화가 준비되지 않았습니다.";
                ShowBookshelfTransientStatus(
                    "책장 동기화 필요",
                    "서버 책장 정보를 불러온 뒤 책을 추가할 수 있습니다. 로그인 상태와 API 연결을 확인해 주세요.",
                    true);
                return;
            }

            if (bookshelfApiSyncController != null &&
                bookshelfApiSyncController.IsServerSyncEnabled &&
                !bookshelfApiSyncController.IsRefreshing)
            {
                bookshelfApiSyncController.RefreshFromServer();
            }

            bookAddFeedbackMessage = "새 책장 정보를 서버와 동기화하는 중입니다.";
            ShowBookshelfTransientStatus(
                "책장 저장 중",
                "새 책장/섹션 정보를 서버에서 불러온 뒤 책을 추가할 수 있습니다. 잠시 후 다시 시도해 주세요.",
                false);
        }

        private bool CanOpenBookAddPanel()
        {
            return CanMutateOwnedBookshelf() &&
                   CurrentFocusArea == ExperienceFocusArea.Bookshelf &&
                   bookshelfGenerator != null &&
                   bookshelfGenerator.BookshelfCount > 0 &&
                   CanUseBookshelfServerPlacement(bookshelfGenerator.DisplayBookshelfIndex) &&
                   !bookshelfGenerator.IsBookshelfSwitching &&
                   !isBookPlacementEditorOpen &&
                   !isFirstBookshelfTutorialOpen &&
                   !isBookshelfNameEditPanelOpen &&
                   !isBookshelfDeleteConfirmPanelOpen &&
                   !isBookshelfDesignPanelOpen &&
                   !isBookshelfManagePanelOpen &&
                   !isBookshelfUnsavedConfirmPanelOpen &&
                   !isDeleteResultDialogOpen &&
                   (state == null || (!state.IsViewingBook && state.ActiveDraggedBook == null));
        }

        private bool CanMutateOwnedBookshelf()
        {
            return guestbookOwnerMode;
        }

        private bool CanUseOwnerBookActions()
        {
            return CanMutateOwnedBookshelf() &&
                   CurrentFocusArea == ExperienceFocusArea.Bookshelf &&
                   (ActiveDetailBook == null || !ActiveDetailBook.isDecoration) &&
                   (state == null || state.ActiveViewedBook == null || !state.ActiveViewedBook.isDecoration);
        }

        private void RefreshBookshelfIndexDots(int displayIndex, int totalCount)
        {
            if (bookshelfIndexDotsRoot == null)
            {
                return;
            }

            var visibleDotCount = Mathf.Max(0, totalCount);
            if (visibleDotCount == 0)
            {
                for (var i = 0; i < bookshelfIndexDots.Count; i++)
                {
                    if (bookshelfIndexDots[i] != null)
                    {
                        bookshelfIndexDots[i].gameObject.SetActive(false);
                    }
                }

                return;
            }

            var activeDotIndex = Mathf.Clamp(displayIndex, 0, visibleDotCount - 1);
            var spacing = visibleDotCount <= 6 ? 6f : visibleDotCount <= 12 ? 4f : 2f;
            var inactiveWidth = visibleDotCount <= 8
                ? 8f
                : Mathf.Clamp((156f - 18f - spacing * (visibleDotCount - 1)) / Mathf.Max(1, visibleDotCount - 1), 4f, 7f);
            var activeWidth = visibleDotCount <= 8 ? 24f : Mathf.Clamp(inactiveWidth * 2.5f, 10f, 18f);
            var dotHeight = Mathf.Clamp(inactiveWidth, 4f, 8f);
            var totalDotsWidth = activeWidth + inactiveWidth * (visibleDotCount - 1) + spacing * (visibleDotCount - 1);
            bookshelfIndexDotsRoot.sizeDelta = new Vector2(Mathf.Min(156f, totalDotsWidth), 8f);
            var layoutGroup = bookshelfIndexDotsRoot.GetComponent<HorizontalLayoutGroup>();
            if (layoutGroup != null)
            {
                layoutGroup.spacing = spacing;
            }

            while (bookshelfIndexDots.Count < visibleDotCount)
            {
                var dot = CreateHudImage(
                    bookshelfIndexDotsRoot,
                    $"BookshelfIndexDot_{bookshelfIndexDots.Count + 1}",
                    Vector2.zero,
                    new Vector2(8f, 8f),
                    new Vector2(0.5f, 0.5f),
                    new Color32(255, 255, 255, 96));
                var layoutElement = dot.gameObject.AddComponent<LayoutElement>();
                bookshelfIndexDots.Add(dot);
            }

            for (var i = 0; i < bookshelfIndexDots.Count; i++)
            {
                var dot = bookshelfIndexDots[i];
                if (dot == null)
                {
                    continue;
                }

                var isActive = i == activeDotIndex;
                dot.gameObject.SetActive(i < visibleDotCount);
                dot.color = isActive
                    ? new Color32(197, 128, 67, 235)
                    : new Color32(96, 74, 61, 70);

                var width = isActive ? activeWidth : inactiveWidth;
                var layoutElement = dot.GetComponent<LayoutElement>();
                if (layoutElement != null)
                {
                    layoutElement.preferredWidth = width;
                    layoutElement.minWidth = width;
                    layoutElement.preferredHeight = dotHeight;
                    layoutElement.minHeight = dotHeight;
                }

                var rect = dot.rectTransform;
                rect.sizeDelta = new Vector2(width, dotHeight);
            }
        }

        private string TryGetBookDisplayTitle(BookEntity book)
        {
            if (book == null)
            {
                return string.Empty;
            }

            var meta = dataStore != null ? dataStore.GetOrCreateBookMeta(book) : null;
            if (meta != null && !string.IsNullOrWhiteSpace(meta.title))
            {
                return meta.title;
            }

            return string.IsNullOrWhiteSpace(book.bookId) ? "Untitled Book" : book.bookId;
        }

        private static string GetSectionAlignmentLabel(ShelfSectionAlignment alignment)
        {
            switch (alignment)
            {
                case ShelfSectionAlignment.Center:
                    return "가운데";
                case ShelfSectionAlignment.Right:
                    return "우정렬";
                default:
                    return "좌정렬";
            }
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

        private static App.Domain.ReadingStatus GetNextReadingStatus(App.Domain.ReadingStatus status)
        {
            switch (status)
            {
                case App.Domain.ReadingStatus.Unread:
                    return App.Domain.ReadingStatus.Reading;
                case App.Domain.ReadingStatus.Reading:
                    return App.Domain.ReadingStatus.Read;
                default:
                    return App.Domain.ReadingStatus.Unread;
            }
        }

        private static string GetOwnerRatingLabel(float rating)
        {
            var snapped = SnapRatingToStep(Mathf.Clamp(rating, 0f, 5f));
            return snapped <= 0f ? "미지정" : $"{FormatRatingValue(snapped)}/5";
        }

        private static string GetOwnerRatingButtonLabel(float rating)
        {
            var snapped = SnapRatingToStep(Mathf.Clamp(rating, 0f, 5f));
            return snapped <= 0f ? "별점" : $"{FormatRatingValue(snapped)}★";
        }

        private static string FormatRatingValue(float rating)
        {
            var snapped = SnapRatingToStep(rating);
            return Mathf.Approximately(snapped, Mathf.Round(snapped))
                ? Mathf.RoundToInt(snapped).ToString()
                : snapped.ToString("0.0");
        }

        private static string GetAverageRatingLabel(float rating)
        {
            return rating <= 0f ? "평가 없음" : $"{Mathf.Clamp(rating, 0f, 5f):0.0}/5";
        }

        private static float ResolveDisplayAverageRating(BookMetaData meta)
        {
            if (meta == null)
            {
                return 0f;
            }

            if (meta.hasAggregateBookStats)
            {
                return Mathf.Clamp(meta.averageRating, 0f, 5f);
            }

            return Mathf.Clamp(meta.ownerRating, 0f, 5f);
        }

        private static int ResolveDisplayLikeCount(BookMetaData meta)
        {
            if (meta == null)
            {
                return 0;
            }

            if (meta.hasAggregateBookStats)
            {
                return meta.isLiked
                    ? Mathf.Max(1, meta.totalLikeCount)
                    : Mathf.Max(0, meta.totalLikeCount);
            }

            return meta.isLiked ? 1 : 0;
        }

        private static string ResolveFallbackText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? (string.IsNullOrWhiteSpace(fallback) ? "정보가 없습니다." : fallback) : value;
        }

        private static Color32 GetReadingStatusButtonColor(App.Domain.ReadingStatus status)
        {
            switch (status)
            {
                case App.Domain.ReadingStatus.Read:
                    return new Color32(173, 122, 79, 255);
                case App.Domain.ReadingStatus.Reading:
                    return new Color32(88, 133, 117, 255);
                default:
                    return new Color32(255, 255, 255, 245);
            }
        }

        private static Color32 GetReadingStatusButtonHighlight(App.Domain.ReadingStatus status)
        {
            switch (status)
            {
                case App.Domain.ReadingStatus.Read:
                    return new Color32(151, 104, 64, 255);
                case App.Domain.ReadingStatus.Reading:
                    return new Color32(72, 114, 100, 255);
                default:
                    return new Color32(241, 235, 230, 255);
            }
        }

        private static string GetOrientationLabel(BookOrientation orientation)
        {
            switch (orientation)
            {
                case BookOrientation.Front:
                    return "정면";
                case BookOrientation.Angled45:
                    return "45도";
                default:
                    return "측면";
            }
        }

        private void ShowPreviousBookshelf()
        {
            SwitchBookshelf(-1);
        }

        private void ShowNextBookshelf()
        {
            SwitchBookshelf(1);
        }

        private void SwitchBookshelf(int direction)
        {
            if (direction == 0 ||
                CurrentFocusArea != ExperienceFocusArea.Bookshelf ||
                isBookPlacementEditorOpen ||
                isBookAddPlacementSelectionActive ||
                bookshelfGenerator == null ||
                bookshelfGenerator.IsBookshelfSwitching)
            {
                return;
            }

            if (IsBookshelfEditMode)
            {
                if (TryPromptUnsavedBookshelfChanges(() => SwitchBookshelf(direction)))
                {
                    return;
                }

                SetBookshelfEditMode(false);
            }

            if (state != null && state.ActiveDraggedBook != null)
            {
                return;
            }

            if (interactionController != null &&
                state != null &&
                state.IsViewingBook)
            {
                interactionController.CloseViewedBook(true);
            }

            ActiveDetailBook = null;

            if (bookDetailPanel != null)
            {
                bookDetailPanel.SetActive(false);
            }

            var switched = direction > 0
                ? bookshelfGenerator.ShowNextBookshelf()
                : bookshelfGenerator.ShowPreviousBookshelf();

            if (switched)
            {
                RefreshHud();
            }
        }

        private void RefreshBookDetailPanel()
        {
            if (bookDetailPanelController == null || !bookDetailPanelController.IsVisible || ActiveDetailBook == null || dataStore == null)
            {
                return;
            }

            var meta = dataStore.GetOrCreateBookMeta(ActiveDetailBook);
            if (meta == null)
            {
                return;
            }

            var section = ActiveDetailBook.CurrentSection;
            var averageRating = ResolveDisplayAverageRating(meta);
            var totalLikeCount = ResolveDisplayLikeCount(meta);
            var hasReport = !string.IsNullOrWhiteSpace(meta.reportBody);
            var canDeleteBook = CanUseOwnerBookActions();
            var viewModel = new BookDetailPanelViewModel(
                TryGetBookDisplayTitle(ActiveDetailBook),
                $"저자      {ResolveFallbackText(meta.author, "저자 미상")}\n" +
                $"출판사    {ResolveFallbackText(meta.publisher, "출판사 미상")}\n" +
                $"저장 위치  {(section != null ? $"{section.row + 1}행 {section.column + 1}열" : "책장 밖")}",
                GetAverageRatingLabel(averageRating),
                totalLikeCount.ToString(),
                Mathf.Max(0, meta.reviewCount).ToString(),
                ResolveFallbackText(meta.description, meta.memoPreview),
                hasReport,
                canDeleteBook,
                pendingDeleteBook == ActiveDetailBook);
            bookDetailPanelController.Refresh(viewModel);
        }

        private void RefreshBookFocusToolbar()
        {
            if (bookFocusToolbarPanel == null)
            {
                return;
            }

            var canShow = CurrentFocusArea == ExperienceFocusArea.Bookshelf &&
                          state != null &&
                          state.IsViewingBook &&
                          state.ActiveViewedBook != null &&
                          !IsBookshelfEditMode &&
                          !isBookPlacementEditorOpen &&
                          !isBookReviewWritePanelOpen &&
                          !isBookReportWritePanelOpen &&
                          !isBookReportReadPanelOpen;
            var canUseOwnerActions = canShow && CanUseOwnerBookActions();
            bookFocusToolbarPanel.SetActive(canUseOwnerActions);
            if (bookFocusWriteReportButton != null)
            {
                bookFocusWriteReportButton.gameObject.SetActive(canUseOwnerActions);
                bookFocusWriteReportButton.interactable = canUseOwnerActions;
            }

            if (bookFocusRatingText != null)
            {
                bookFocusRatingText.text = string.Empty;
                bookFocusRatingText.gameObject.SetActive(false);
            }

            if (!canUseOwnerActions)
            {
                return;
            }

            var activeBook = state.ActiveViewedBook;
            var meta = dataStore != null ? dataStore.GetOrCreateBookMeta(activeBook) : null;
            if (meta != null)
            {
                SetButtonLabel(bookFocusDetailButton, GetReadingStatusLabel(meta.readingStatus));
                SetButtonLabel(bookFocusRatingButton, "리뷰 작성");
                SetButtonLabel(bookFocusReportButton, meta.isLiked ? "좋아요 취소" : "좋아요");
            }
            else
            {
                SetButtonLabel(bookFocusRatingButton, "리뷰 작성");
                SetButtonLabel(bookFocusReportButton, "좋아요");
            }

            StyleHudButton(
                bookFocusDetailButton,
                meta != null ? GetReadingStatusButtonColor(meta.readingStatus) : new Color32(255, 255, 255, 235),
                meta != null ? GetReadingStatusButtonHighlight(meta.readingStatus) : new Color32(241, 235, 230, 255),
                new Color32(224, 219, 214, 140),
                14,
                meta != null && meta.readingStatus != App.Domain.ReadingStatus.Unread ? Color.white : new Color32(63, 47, 38, 255),
                meta != null && meta.readingStatus != App.Domain.ReadingStatus.Unread);
            if (bookFocusDetailButton != null)
            {
                bookFocusDetailButton.interactable = canUseOwnerActions;
            }
            if (bookFocusRatingButton != null)
            {
                bookFocusRatingButton.gameObject.SetActive(canUseOwnerActions);
                bookFocusRatingButton.interactable = canUseOwnerActions;
                if (canUseOwnerActions)
                {
                    StyleHudButton(
                        bookFocusRatingButton,
                        meta != null && meta.ownerRating > 0f ? new Color32(197, 128, 67, 245) : new Color32(255, 255, 255, 235),
                        meta != null && meta.ownerRating > 0f ? new Color32(214, 145, 82, 255) : new Color32(241, 235, 230, 255),
                        new Color32(224, 219, 214, 140),
                        14,
                        meta != null && meta.ownerRating > 0f ? Color.white : new Color32(63, 47, 38, 255),
                        true);
                }
            }
            if (openBookPlacementEditorButton != null)
            {
                var canPlace = CanMutateOwnedBookshelf() &&
                               state != null &&
                               state.IsViewingBook &&
                               state.ActiveDraggedBook == null &&
                               !IsBookshelfEditMode &&
                               !isBookPlacementEditorOpen &&
                               (bookshelfGenerator == null || !bookshelfGenerator.IsBookshelfSwitching);
                openBookPlacementEditorButton.gameObject.SetActive(canPlace);
                openBookPlacementEditorButton.interactable = canPlace;
                StyleHudButton(
                    openBookPlacementEditorButton,
                    new Color32(255, 255, 255, 238),
                    new Color32(241, 235, 230, 255),
                    new Color32(224, 219, 214, 140),
                    14,
                    new Color32(45, 50, 58, 255),
                    true);
            }

            if (bookFocusReportButton != null)
            {
                bookFocusReportButton.gameObject.SetActive(canUseOwnerActions);
                bookFocusReportButton.interactable = canUseOwnerActions;
                if (canUseOwnerActions)
                {
                    StyleHudButton(
                        bookFocusReportButton,
                        meta != null && meta.isLiked ? new Color32(173, 122, 79, 245) : new Color32(238, 248, 241, 235),
                        meta != null && meta.isLiked ? new Color32(151, 104, 64, 255) : new Color32(247, 255, 250, 255),
                        new Color32(224, 219, 214, 140),
                        14,
                        meta != null && meta.isLiked ? Color.white : new Color32(53, 94, 73, 255),
                        meta != null && meta.isLiked);
                }
            }
            if (bookFocusWriteReportButton != null)
            {
                SetButtonLabel(bookFocusWriteReportButton, meta != null && !string.IsNullOrWhiteSpace(meta.reportBody) ? "독후감 보기" : "독후감 작성");
                StyleHudButton(
                    bookFocusWriteReportButton,
                    new Color32(88, 133, 117, 245),
                    new Color32(105, 150, 132, 255),
                    new Color32(224, 219, 214, 140),
                    15,
                    Color.white,
                    true);
            }

            bookFocusToolbarPanel.transform.SetAsLastSibling();
            if (bookFocusWriteReportButton != null && bookFocusWriteReportButton.gameObject.activeSelf)
            {
                bookFocusWriteReportButton.transform.SetAsLastSibling();
            }
        }

        private void RefreshBookReviewWritePanel()
        {
            if (bookReviewWritePanel == null)
            {
                return;
            }

            var shouldShow = isBookReviewWritePanelOpen &&
                             activeReviewBook != null &&
                             CurrentFocusArea == ExperienceFocusArea.Bookshelf &&
                             state != null &&
                             state.IsViewingBook &&
                             state.ActiveViewedBook != null &&
                             !IsBookshelfEditMode;
            bookReviewWritePanel.SetActive(shouldShow);
            if (!shouldShow)
            {
                return;
            }

            var bookTitle = activeReviewBook != null && !string.IsNullOrWhiteSpace(activeReviewBook.title)
                ? activeReviewBook.title.Trim()
                : "제목 없음";
            if (bookReviewWriteTitleText != null)
            {
                bookReviewWriteTitleText.text = "리뷰 작성";
            }

            if (bookReviewWriteBookText != null)
            {
                bookReviewWriteBookText.text = bookTitle;
            }

            if (bookReviewRatingText != null)
            {
                bookReviewRatingText.text = stagedBookReviewRating >= BookRatingStep
                    ? $"별점 {FormatRatingValue(stagedBookReviewRating)} / 5"
                    : "별점";
            }

            for (var i = 0; i < bookReviewRatingButtons.Length; i++)
            {
                var rating = GetRatingChoiceValue(i);
                var isSelected = Mathf.Approximately(rating, stagedBookReviewRating);
                var label = $"{FormatRatingValue(rating)}★";
                SetButtonLabel(bookReviewRatingButtons[i], isSelected ? $"✓ {label}" : label);
                StyleHudButton(
                    bookReviewRatingButtons[i],
                    isSelected ? new Color32(197, 128, 67, 245) : new Color32(255, 255, 255, 240),
                    isSelected ? new Color32(214, 145, 82, 255) : new Color32(241, 235, 230, 255),
                    new Color32(224, 219, 214, 140),
                    14,
                    isSelected ? Color.white : new Color32(63, 47, 38, 255),
                    isSelected);
                if (bookReviewRatingButtons[i] != null)
                {
                    bookReviewRatingButtons[i].interactable = !isBookReviewSaving;
                }
            }

            var hasContent = bookReviewContentInputField != null &&
                             !string.IsNullOrWhiteSpace(bookReviewContentInputField.text);
            if (bookReviewContentInputField != null)
            {
                bookReviewContentInputField.interactable = !isBookReviewSaving;
            }

            var canSave = !isBookReviewSaving &&
                          stagedBookReviewRating >= BookRatingStep &&
                          hasContent;
            if (saveBookReviewButton != null)
            {
                saveBookReviewButton.interactable = canSave;
                StyleHudButton(
                    saveBookReviewButton,
                    canSave ? new Color32(88, 133, 117, 245) : new Color32(224, 219, 214, 140),
                    canSave ? new Color32(105, 150, 132, 255) : new Color32(224, 219, 214, 140),
                    new Color32(224, 219, 214, 140),
                    14,
                    canSave ? Color.white : new Color32(128, 118, 108, 255),
                    true);
            }

            if (cancelBookReviewButton != null)
            {
                cancelBookReviewButton.interactable = !isBookReviewSaving;
                StyleHudButton(
                    cancelBookReviewButton,
                    new Color32(255, 255, 255, 238),
                    new Color32(241, 235, 230, 255),
                    new Color32(224, 219, 214, 140),
                    14,
                    new Color32(63, 47, 38, 255),
                    false);
            }

            if (bookReviewStatusText != null)
            {
                bookReviewStatusText.text = bookReviewStatusMessage;
                bookReviewStatusText.color = bookReviewStatusIsError
                    ? new Color32(156, 64, 52, 255)
                    : new Color32(124, 111, 101, 255);
            }

            bookReviewWritePanel.transform.SetAsLastSibling();
        }

        private void OpenActiveViewedBookDetail()
        {
            var book = state != null ? state.ActiveViewedBook : null;
            if (book != null)
            {
                OpenBookDetailPanel(book);
            }
        }

        private void OpenActiveViewedBookReport()
        {
            var book = state != null ? state.ActiveViewedBook : ActiveDetailBook;
            if (book == null)
            {
                return;
            }

            var meta = dataStore != null ? dataStore.GetOrCreateBookMeta(book) : null;
            if (meta != null && !string.IsNullOrWhiteSpace(meta.reportBody))
            {
                OpenBookReportReadPanel(book);
            }
            else if (TryOpenReportAfterServerRefresh(book))
            {
                return;
            }
            else
            {
                OpenBookReportWritePanel(book);
            }
        }

        private bool TryOpenReportAfterServerRefresh(BookEntity book)
        {
            if (book == null || bookshelfApiSyncController == null || !bookshelfApiSyncController.IsServerSyncEnabled)
            {
                return false;
            }

            return bookshelfApiSyncController.FetchReadingRecord(book, (updatedBook, success) =>
            {
                if (updatedBook == null)
                {
                    return;
                }

                if (success)
                {
                    ApplyBookEntityToMeta(updatedBook);
                }

                var refreshedMeta = dataStore != null ? dataStore.GetOrCreateBookMeta(updatedBook) : null;
                if (refreshedMeta != null && !string.IsNullOrWhiteSpace(refreshedMeta.reportBody))
                {
                    OpenBookReportReadPanel(updatedBook);
                }
                else
                {
                    OpenBookReportWritePanel(updatedBook);
                }

                RefreshHud();
            });
        }

        private void CloseActiveViewedBook()
        {
            if (interactionController != null && state != null && state.IsViewingBook)
            {
                interactionController.CloseViewedBook(true);
            }
        }

        private void OpenBookReportWritePanel()
        {
            OpenBookReportWritePanel(ActiveDetailBook != null ? ActiveDetailBook : (state != null ? state.ActiveViewedBook : null));
        }

        private void OpenBookReportWritePanel(BookEntity book)
        {
            if (book == null || dataStore == null || bookReportWritePanel == null)
            {
                return;
            }

            CloseOverlayPanels(bookReportWritePanel);
            activeReportBook = book;
            ActiveDetailBook = book;
            isBookReportReadPanelOpen = false;
            isBookReportWritePanelOpen = true;

            var meta = dataStore.GetOrCreateBookMeta(book);
            stagedBookReportVisibility = meta != null ? meta.reportVisibility : App.Domain.VisibilityScope.Public;
            if (bookReportTitleInputField != null)
            {
                bookReportTitleInputField.text = meta != null && !string.IsNullOrWhiteSpace(meta.reportTitle)
                    ? meta.reportTitle
                    : TryGetBookDisplayTitle(book);
            }

            if (bookReportBodyInputField != null)
            {
                bookReportBodyInputField.text = meta != null ? meta.reportBody : string.Empty;
            }

            RefreshHud();
        }

        private void OpenBookReportReadPanel()
        {
            OpenBookReportReadPanel(ActiveDetailBook != null ? ActiveDetailBook : (state != null ? state.ActiveViewedBook : null));
        }

        private void OpenBookReportReadPanel(BookEntity book)
        {
            if (book == null || dataStore == null || bookReportReadPanel == null)
            {
                return;
            }

            var meta = dataStore.GetOrCreateBookMeta(book);
            if (meta == null || string.IsNullOrWhiteSpace(meta.reportBody))
            {
                OpenBookReportWritePanel(book);
                return;
            }

            CloseOverlayPanels(bookReportReadPanel);
            activeReportBook = book;
            ActiveDetailBook = book;
            pendingDeleteReportBook = null;
            isBookReportWritePanelOpen = false;
            isBookReportReadPanelOpen = true;
            RequestReportSocialSnapshot(book);
            RefreshHud();
        }

        private void RequestReportSocialSnapshot(BookEntity book)
        {
            if (book == null || bookshelfApiSyncController == null || !bookshelfApiSyncController.IsServerSyncEnabled)
            {
                RequestReportComments(book);
                return;
            }

            var requested = bookshelfApiSyncController.FetchReadingRecord(book, (updatedBook, success) =>
            {
                if (success)
                {
                    ApplyBookEntityToMeta(updatedBook);
                }

                RequestReportComments(updatedBook);
                RefreshHud();
            });
            if (!requested)
            {
                RequestReportComments(book);
            }
        }

        private void RequestReportComments(BookEntity book)
        {
            EnsureActiveReportCommentContext(book);
            if (book == null)
            {
                return;
            }

            if (bookshelfApiSyncController == null || !bookshelfApiSyncController.IsServerSyncEnabled)
            {
                activeReportCommentsLoaded = true;
                RefreshHud();
                return;
            }

            if (!HasReadingRecordServerId(book))
            {
                activeReportComments.Clear();
                activeReportCommentsLoaded = true;
                RefreshHud();
                return;
            }

            activeReportCommentsLoaded = false;
            var didStartLoad = bookshelfApiSyncController.FetchReadingRecordComments(book, comments =>
            {
                EnsureActiveReportCommentContext(book);
                activeReportComments.Clear();
                if (comments != null)
                {
                    foreach (var comment in comments)
                    {
                        if (comment == null)
                        {
                            continue;
                        }

                        activeReportComments.Add(CreateReportCommentViewData(comment.Id, comment.UserId, comment.Content, comment.LikeCount, comment.Deleted));
                    }
                }

                activeReportCommentsLoaded = true;
                ApplyBookEntityToMeta(book);
                RefreshHud();
            }, _ =>
            {
                activeReportCommentsLoaded = true;
                RefreshHud();
            });
            if (!didStartLoad)
            {
                activeReportCommentsLoaded = true;
                RefreshHud();
            }
        }

        private void EnsureActiveReportCommentContext(BookEntity book)
        {
            if (activeReportCommentsBook == book)
            {
                return;
            }

            activeReportCommentsBook = book;
            activeReportComments.Clear();
            activeReportCommentsLoaded = false;
        }

        private ReportCommentViewData CreateReportCommentViewData(long id, long userId, string content, int likeCount, bool deleted)
        {
            var authorLabel = userId > 0L ? $"사용자 {userId}" : "나";
            return new ReportCommentViewData(
                id,
                authorLabel,
                string.IsNullOrWhiteSpace(content) ? string.Empty : content.Trim(),
                Mathf.Max(0, likeCount),
                deleted);
        }

        private void AddActiveReportComment(BookEntity book, string authorLabel, string content)
        {
            EnsureActiveReportCommentContext(book);
            activeReportCommentsLoaded = true;
            activeReportComments.Add(new ReportCommentViewData(
                nextLocalReportCommentId--,
                string.IsNullOrWhiteSpace(authorLabel) ? "나" : authorLabel.Trim(),
                string.IsNullOrWhiteSpace(content) ? string.Empty : content.Trim(),
                0,
                false));
        }

        private void RestoreActiveReportComments(
            BookEntity commentsBook,
            IReadOnlyList<ReportCommentViewData> comments,
            bool loaded)
        {
            activeReportCommentsBook = commentsBook;
            activeReportComments.Clear();
            if (comments != null)
            {
                for (var i = 0; i < comments.Count; i++)
                {
                    activeReportComments.Add(comments[i]);
                }
            }

            activeReportCommentsLoaded = loaded;
        }

        private void ClearActiveReportComments()
        {
            activeReportCommentsBook = null;
            activeReportComments.Clear();
            activeReportCommentsLoaded = false;
        }

        private void RefreshBookReportWritePanel()
        {
            if (bookReportWritePanel == null)
            {
                return;
            }

            var show = isBookReportWritePanelOpen &&
                       activeReportBook != null &&
                       CurrentFocusArea == ExperienceFocusArea.Bookshelf &&
                       !IsBookshelfEditMode;
            bookReportWritePanel.SetActive(show);
            if (!show)
            {
                return;
            }

            if (bookReportWriteBookText != null)
            {
                var meta = dataStore != null ? dataStore.GetOrCreateBookMeta(activeReportBook) : null;
                bookReportWriteBookText.text = meta != null
                    ? $"{TryGetBookDisplayTitle(activeReportBook)} · {meta.author}"
                    : TryGetBookDisplayTitle(activeReportBook);
            }

            StyleReportVisibilityButton(bookReportPrivateButton, stagedBookReportVisibility == App.Domain.VisibilityScope.Private);
            StyleReportVisibilityButton(bookReportPublicButton, stagedBookReportVisibility == App.Domain.VisibilityScope.Public);
            StyleReportVisibilityButton(bookReportFriendsButton, stagedBookReportVisibility == App.Domain.VisibilityScope.Followers);
            StyleHudButton(
                saveBookReportButton,
                new Color32(88, 133, 117, 245),
                new Color32(72, 114, 100, 255),
                new Color32(224, 219, 214, 140),
                14,
                Color.white,
                true);
            StyleHudButton(
                cancelBookReportButton,
                new Color32(245, 237, 226, 245),
                new Color32(255, 248, 236, 255),
                new Color32(224, 219, 214, 140),
                14,
                new Color32(84, 74, 66, 255),
                true);
            bookReportWritePanel.transform.SetAsLastSibling();
        }

        private void RefreshBookReportReadPanel()
        {
            if (bookReportReadPanel == null)
            {
                return;
            }

            var show = isBookReportReadPanelOpen &&
                       activeReportBook != null &&
                       CurrentFocusArea == ExperienceFocusArea.Bookshelf &&
                       !IsBookshelfEditMode;
            bookReportReadPanel.SetActive(show);
            if (!show)
            {
                return;
            }

            var meta = dataStore != null ? dataStore.GetOrCreateBookMeta(activeReportBook) : null;
            if (meta == null)
            {
                return;
            }

            if (bookReportReadTitleText != null)
            {
                bookReportReadTitleText.text = string.IsNullOrWhiteSpace(meta.reportTitle)
                    ? TryGetBookDisplayTitle(activeReportBook)
                    : meta.reportTitle;
            }

            if (bookReportReadMetaText != null)
            {
                var date = string.IsNullOrWhiteSpace(meta.reportUpdatedLabel) ? "방금" : meta.reportUpdatedLabel;
                bookReportReadMetaText.text = $"{TryGetBookDisplayTitle(activeReportBook)} · {date} · {GetVisibilityLabel(meta.reportVisibility)}";
            }

            if (bookReportReadBodyText != null)
            {
                bookReportReadBodyText.text = string.IsNullOrWhiteSpace(meta.reportBody)
                    ? "작성된 독후감이 없습니다."
                    : meta.reportBody;
            }

            if (bookReportSocialText != null)
            {
                bookReportSocialText.text = $"공감 {Mathf.Max(0, meta.reportLikeCount)}개 · 댓글 {Mathf.Max(0, meta.reportCommentCount)}개";
            }

            RefreshBookReportComments(meta);

            var canUseReportSocial = CanUseReportSocialControls(meta);
            if (bookReportCommentInputField != null)
            {
                bookReportCommentInputField.interactable = canUseReportSocial;
            }

            if (editBookReportButton != null)
            {
                editBookReportButton.gameObject.SetActive(guestbookOwnerMode);
            }

            if (toggleReportVisibilityButton != null)
            {
                toggleReportVisibilityButton.gameObject.SetActive(guestbookOwnerMode);
            }

            if (deleteBookReportButton != null)
            {
                deleteBookReportButton.gameObject.SetActive(guestbookOwnerMode);
            }

            StyleHudButton(
                editBookReportButton,
                new Color32(45, 50, 58, 245),
                new Color32(64, 70, 79, 255),
                new Color32(224, 219, 214, 140),
                14,
                Color.white,
                true);
            StyleHudButton(
                bookReportLikeButton,
                meta.reportLiked ? new Color32(173, 122, 79, 245) : new Color32(238, 248, 241, 235),
                meta.reportLiked ? new Color32(151, 104, 64, 255) : new Color32(247, 255, 250, 255),
                new Color32(224, 219, 214, 140),
                13,
                meta.reportLiked ? Color.white : new Color32(53, 94, 73, 255),
                meta.reportLiked);
            SetButtonLabel(bookReportLikeButton, meta.reportLiked ? "취소" : "공감");
            if (bookReportLikeButton != null)
            {
                bookReportLikeButton.interactable = canUseReportSocial;
            }

            StyleHudButton(
                bookReportCommentButton,
                new Color32(88, 133, 117, 245),
                new Color32(105, 150, 132, 255),
                new Color32(224, 219, 214, 140),
                13,
                Color.white,
                true);
            if (bookReportCommentButton != null)
            {
                bookReportCommentButton.interactable = canUseReportSocial;
            }

            StyleHudButton(
                toggleReportVisibilityButton,
                meta.reportVisibility == App.Domain.VisibilityScope.Public ? new Color32(238, 248, 241, 235) : new Color32(255, 255, 255, 235),
                meta.reportVisibility == App.Domain.VisibilityScope.Public ? new Color32(247, 255, 250, 255) : new Color32(241, 235, 230, 255),
                new Color32(224, 219, 214, 140),
                14,
                meta.reportVisibility == App.Domain.VisibilityScope.Public ? new Color32(53, 94, 73, 255) : new Color32(63, 47, 38, 255),
                true);
            SetButtonLabel(toggleReportVisibilityButton, GetVisibilityLabel(meta.reportVisibility));
            var isConfirmingReportDelete = pendingDeleteReportBook == activeReportBook;
            StyleDeleteActionButton(deleteBookReportButton, guestbookOwnerMode, isConfirmingReportDelete, 14);
            SetButtonLabel(deleteBookReportButton, isConfirmingReportDelete ? "확인" : "삭제");
            StyleHudButton(
                closeBookReportReadButton,
                new Color32(245, 237, 226, 245),
                new Color32(255, 248, 236, 255),
                new Color32(224, 219, 214, 140),
                14,
                new Color32(84, 74, 66, 255),
                true);
            bookReportReadPanel.transform.SetAsLastSibling();
        }

        private void RefreshBookReportComments(BookMetaData meta)
        {
            EnsureActiveReportCommentContext(activeReportBook);
            var visibleCommentCount = Mathf.Max(meta != null ? meta.reportCommentCount : 0, activeReportComments.Count);
            if (bookReportCommentsTitleText != null)
            {
                bookReportCommentsTitleText.text = visibleCommentCount > 0
                    ? $"댓글 {visibleCommentCount}개"
                    : "댓글";
            }

            if (bookReportCommentsText == null)
            {
                return;
            }

            bookReportCommentsText.text = BuildReportCommentsText();
            bookReportCommentsText.color = activeReportCommentsLoaded && activeReportComments.Count > 0
                ? new Color32(83, 72, 63, 255)
                : new Color32(143, 132, 121, 255);
        }

        private string BuildReportCommentsText()
        {
            if (!activeReportCommentsLoaded)
            {
                return "댓글을 불러오는 중입니다.";
            }

            if (activeReportComments.Count == 0)
            {
                return "아직 댓글이 없습니다.";
            }

            var lines = new List<string>();
            var visibleCount = Mathf.Min(activeReportComments.Count, 4);
            for (var i = 0; i < visibleCount; i++)
            {
                var comment = activeReportComments[i];
                var content = comment.Deleted
                    ? "삭제된 댓글입니다."
                    : NormalizeReportCommentContent(comment.Content);
                var line = $"{comment.AuthorLabel}  {content}";
                if (comment.LikeCount > 0)
                {
                    line += $" · 공감 {comment.LikeCount}";
                }

                lines.Add(line);
            }

            if (activeReportComments.Count > visibleCount)
            {
                lines.Add($"댓글 {activeReportComments.Count - visibleCount}개 더 있음");
            }

            return string.Join("\n", lines);
        }

        private static string NormalizeReportCommentContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return "(내용 없음)";
            }

            return content.Trim().Replace("\r", " ").Replace("\n", " ");
        }

        private bool CanUseReportSocialControls(BookMetaData meta)
        {
            if (activeReportBook == null || meta == null || string.IsNullOrWhiteSpace(meta.reportBody))
            {
                return false;
            }

            if (!CanStartBookshelfApiMutation())
            {
                return !usesHttpApiMode;
            }

            return HasReadingRecordServerId(activeReportBook);
        }

        private static bool HasReadingRecordServerId(BookEntity book)
        {
            return book != null &&
                   long.TryParse(book.readingRecordId, out var recordId) &&
                   recordId > 0L;
        }

        private void ToggleActiveReportLike()
        {
            if (activeReportBook == null || dataStore == null)
            {
                return;
            }

            var meta = dataStore.GetOrCreateBookMeta(activeReportBook);
            if (meta == null)
            {
                return;
            }

            if (!CanUseReportSocialControls(meta))
            {
                ShowDeleteResultDialog(
                    "공감 준비 중",
                    "독후감 저장 정보가 아직 준비되지 않았습니다. 잠시 후 다시 시도해주세요.",
                    false);
                return;
            }

            var previousLiked = meta.reportLiked;
            var previousLikeCount = meta.reportLikeCount;
            var nextLiked = !meta.reportLiked;
            meta.reportLiked = nextLiked;
            meta.reportLikeCount = Mathf.Max(0, meta.reportLikeCount + (nextLiked ? 1 : -1));
            dataStore.ApplyBookMeta(activeReportBook, meta);
            var book = activeReportBook;
            var didStartSync = CanStartBookshelfApiMutation() &&
                               bookshelfApiSyncController.ToggleReadingRecordLike(book, nextLiked, updatedBook =>
            {
                ApplyBookEntityToMeta(updatedBook);
                RefreshHud();
            }, error =>
            {
                RestoreReportLikeAfterFailure(book, previousLiked, previousLikeCount, error);
            });
            if (!didStartSync)
            {
                if (usesHttpApiMode && !CanStartBookshelfApiMutation())
                {
                    RestoreReportLikeAfterFailure(book, previousLiked, previousLikeCount, "책장 API 동기화가 준비되지 않아 공감을 서버에 저장할 수 없습니다.");
                    return;
                }

                if (bookshelfApiSyncController != null && bookshelfApiSyncController.IsServerSyncEnabled)
                {
                    RestoreReportLikeAfterFailure(book, previousLiked, previousLikeCount, "독후감 서버 식별자를 찾을 수 없습니다.");
                    return;
                }
            }

            RefreshHud();
        }

        private void RestoreReportLikeAfterFailure(BookEntity book, bool previousLiked, int previousLikeCount, string errorMessage)
        {
            if (dataStore != null && book != null)
            {
                var meta = dataStore.GetOrCreateBookMeta(book);
                if (meta != null)
                {
                    meta.reportLiked = previousLiked;
                    meta.reportLikeCount = Mathf.Max(0, previousLikeCount);
                    dataStore.ApplyBookMeta(book, meta);
                }
            }

            ShowDeleteResultDialog(
                "공감 저장 실패",
                $"서버 요청을 완료하지 못해 공감 상태를 되돌렸습니다.\n{NormalizeMutationErrorMessage(errorMessage)}",
                false);
            RefreshHud();
        }

        private void SubmitActiveReportComment()
        {
            if (activeReportBook == null || dataStore == null || bookReportCommentInputField == null)
            {
                return;
            }

            var content = bookReportCommentInputField.text;
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            var meta = dataStore.GetOrCreateBookMeta(activeReportBook);
            if (!CanUseReportSocialControls(meta))
            {
                ShowDeleteResultDialog(
                    "댓글 준비 중",
                    "독후감 저장 정보가 아직 준비되지 않았습니다. 잠시 후 다시 시도해주세요.",
                    false);
                return;
            }

            var previousCommentCount = meta != null ? meta.reportCommentCount : 0;
            var previousCommentsBook = activeReportCommentsBook;
            var previousCommentsLoaded = activeReportCommentsLoaded;
            var previousComments = new List<ReportCommentViewData>(activeReportComments);
            if (meta != null)
            {
                meta.reportCommentCount = Mathf.Max(0, meta.reportCommentCount + 1);
                dataStore.ApplyBookMeta(activeReportBook, meta);
            }

            bookReportCommentInputField.text = string.Empty;
            var book = activeReportBook;
            AddActiveReportComment(book, "나", content);
            var didStartSync = CanStartBookshelfApiMutation() &&
                               bookshelfApiSyncController.CreateReadingRecordComment(
                                    book,
                                    content,
                                   _ => RequestReportComments(book),
                                   error => RestoreReportCommentAfterFailure(
                                       book,
                                       previousCommentCount,
                                       content,
                                       error,
                                       previousCommentsBook,
                                        previousComments,
                                        previousCommentsLoaded));
            if (!didStartSync)
            {
                if (usesHttpApiMode && !CanStartBookshelfApiMutation())
                {
                    RestoreReportCommentAfterFailure(
                        book,
                        previousCommentCount,
                        content,
                        "책장 API 동기화가 준비되지 않아 댓글을 서버에 저장할 수 없습니다.",
                        previousCommentsBook,
                        previousComments,
                        previousCommentsLoaded);
                    return;
                }

                if (bookshelfApiSyncController != null && bookshelfApiSyncController.IsServerSyncEnabled)
                {
                    RestoreReportCommentAfterFailure(
                        book,
                        previousCommentCount,
                        content,
                        "독후감 서버 식별자를 찾을 수 없습니다.",
                        previousCommentsBook,
                        previousComments,
                        previousCommentsLoaded);
                    return;
                }
            }

            RefreshHud();
        }

        private void RestoreReportCommentAfterFailure(
            BookEntity book,
            int previousCommentCount,
            string content,
            string errorMessage,
            BookEntity previousCommentsBook,
            IReadOnlyList<ReportCommentViewData> previousComments,
            bool previousCommentsLoaded)
        {
            if (dataStore != null && book != null)
            {
                var meta = dataStore.GetOrCreateBookMeta(book);
                if (meta != null)
                {
                    meta.reportCommentCount = Mathf.Max(0, previousCommentCount);
                    dataStore.ApplyBookMeta(book, meta);
                }
            }

            RestoreActiveReportComments(previousCommentsBook, previousComments, previousCommentsLoaded);
            if (bookReportCommentInputField != null)
            {
                bookReportCommentInputField.text = content ?? string.Empty;
            }

            ShowDeleteResultDialog(
                "댓글 등록 실패",
                $"서버 요청을 완료하지 못해 댓글 입력을 복원했습니다.\n{NormalizeMutationErrorMessage(errorMessage)}",
                false);
            RefreshHud();
        }

        private void DeleteActiveBookReport()
        {
            if (activeReportBook == null || dataStore == null || !guestbookOwnerMode)
            {
                return;
            }

            if (pendingDeleteReportBook != activeReportBook)
            {
                pendingDeleteReportBook = activeReportBook;
                RefreshBookReportReadPanel();
                return;
            }

            var book = activeReportBook;
            var meta = dataStore.GetOrCreateBookMeta(book);
            var rollback = new BookReportMutationSnapshot(book, meta);
            if (meta != null)
            {
                meta.reportTitle = string.Empty;
                meta.reportBody = string.Empty;
                meta.reportUpdatedLabel = string.Empty;
                meta.memoPreview = string.IsNullOrWhiteSpace(meta.description) ? string.Empty : meta.description;
                meta.reportLiked = false;
                meta.reportLikeCount = 0;
                meta.reportCommentCount = 0;
                dataStore.ApplyBookMeta(book, meta);
            }

            var didStartDeleteSync = CanStartBookshelfApiMutation() &&
                                     bookshelfApiSyncController.DeleteReadingRecord(
                                          book,
                                          () => ShowDeleteResultDialog("독후감 삭제 완료", "독후감을 삭제했습니다.", true),
                                          error => RestoreBookReportAfterDeleteFailure(book, rollback, error));
            if (!didStartDeleteSync)
            {
                if (usesHttpApiMode && !CanStartBookshelfApiMutation())
                {
                    RestoreBookReportAfterDeleteFailure(book, rollback, "책장 API 동기화가 준비되지 않아 독후감 삭제를 서버에 요청할 수 없습니다.");
                    return;
                }

                if (bookshelfApiSyncController != null && bookshelfApiSyncController.IsServerSyncEnabled)
                {
                    RestoreBookReportAfterDeleteFailure(book, rollback, "독후감 서버 식별자를 찾을 수 없습니다.");
                    return;
                }

                ShowDeleteResultDialog("독후감 삭제 완료", "독후감을 삭제했습니다.", true);
            }

            book.readingRecordId = null;
            pendingDeleteReportBook = null;
            isBookReportReadPanelOpen = false;
            activeReportBook = null;
            if (bookReportReadPanel != null)
            {
                bookReportReadPanel.SetActive(false);
            }

            RefreshBookDetailPanel();
            RefreshHud();
        }

        private void RestoreBookReportAfterDeleteFailure(
            BookEntity book,
            BookReportMutationSnapshot rollback,
            string errorMessage)
        {
            Debug.LogWarning($"Reading record delete failed on server. Restoring local report state. {errorMessage}");
            rollback.ApplyTo(book);

            if (dataStore != null && book != null)
            {
                var meta = dataStore.GetOrCreateBookMeta(book);
                rollback.ApplyTo(meta);
                dataStore.ApplyBookMeta(book, meta);
            }

            ShowDeleteResultDialog(
                "독후감 삭제 실패",
                $"서버 요청을 완료하지 못해 독후감을 복원했습니다.\n{NormalizeMutationErrorMessage(errorMessage)}",
                false);
            RefreshBookDetailPanel();
            RefreshHud();
        }

        private void SetStagedReportVisibility(App.Domain.VisibilityScope visibility)
        {
            stagedBookReportVisibility = visibility;
            RefreshBookReportWritePanel();
        }

        private void SaveBookReport()
        {
            if (activeReportBook == null || dataStore == null)
            {
                return;
            }

            var meta = dataStore.GetOrCreateBookMeta(activeReportBook);
            if (meta == null)
            {
                return;
            }

            var nextReportTitle = bookReportTitleInputField != null ? bookReportTitleInputField.text.Trim() : string.Empty;
            var nextReportBody = bookReportBodyInputField != null ? bookReportBodyInputField.text.Trim() : string.Empty;
            var rollback = new BookReportMutationSnapshot(activeReportBook, meta);
            meta.reportVisibility = stagedBookReportVisibility;
            meta.isPublic = meta.reportVisibility == App.Domain.VisibilityScope.Public;

            if (string.IsNullOrWhiteSpace(nextReportBody))
            {
                meta.reportTitle = string.Empty;
                meta.reportBody = string.Empty;
                meta.reportUpdatedLabel = string.Empty;
                meta.memoPreview = string.Empty;
                meta.reportLiked = false;
                meta.reportLikeCount = 0;
                meta.reportCommentCount = 0;
                meta.isRead = false;
                meta.readingStatus = App.Domain.ReadingStatus.Unread;

                dataStore.ApplyBookMeta(activeReportBook, meta);
                var reportBook = activeReportBook;
                var didStartDeleteSync = CanStartBookshelfApiMutation() &&
                                         bookshelfApiSyncController.DeleteReadingRecord(
                                              reportBook,
                                              () => ShowDeleteResultDialog("독후감 삭제 완료", "빈 내용으로 저장되어 독후감을 삭제했습니다.", true),
                                              error => RestoreBookReportAfterDeleteFailure(reportBook, rollback, error));
                if (!didStartDeleteSync)
                {
                    if (usesHttpApiMode && !CanStartBookshelfApiMutation())
                    {
                        RestoreBookReportAfterDeleteFailure(reportBook, rollback, "책장 API 동기화가 준비되지 않아 독후감 삭제를 서버에 요청할 수 없습니다.");
                        return;
                    }

                    if (bookshelfApiSyncController != null && bookshelfApiSyncController.IsServerSyncEnabled)
                    {
                        RestoreBookReportAfterDeleteFailure(reportBook, rollback, "독후감 서버 식별자를 찾을 수 없습니다.");
                        return;
                    }

                    ShowDeleteResultDialog("독후감 삭제 완료", "빈 내용으로 저장되어 독후감을 삭제했습니다.", true);
                }

                bookshelfApiSyncController?.SyncBookState(reportBook);
                isBookReportWritePanelOpen = true;
                isBookReportReadPanelOpen = false;
                RefreshHud();
                return;
            }

            meta.reportTitle = nextReportTitle;
            meta.reportBody = nextReportBody;
            meta.reportUpdatedLabel = System.DateTime.Now.ToString("yyyy.MM.dd");
            meta.memoPreview = meta.reportBody.Length > 92 ? $"{meta.reportBody.Substring(0, 92)}..." : meta.reportBody;
            meta.isRead = true;
            meta.readingStatus = App.Domain.ReadingStatus.Read;

            dataStore.ApplyBookMeta(activeReportBook, meta);
            var book = activeReportBook;
            var didStartSave = CanStartBookshelfApiMutation() &&
                               bookshelfApiSyncController.SaveReadingRecord(
                                    book,
                                    HandleBookReportSaved,
                                    error => HandleBookReportSaveFailure(book, rollback, error));
            if (!didStartSave)
            {
                if (usesHttpApiMode && !CanStartBookshelfApiMutation())
                {
                    HandleBookReportSaveFailure(book, rollback, "책장 API 동기화가 준비되지 않아 독후감을 서버에 저장할 수 없습니다.");
                    return;
                }

                if (bookshelfApiSyncController != null && bookshelfApiSyncController.IsServerSyncEnabled)
                {
                    HandleBookReportSaveFailure(book, rollback, "독후감 저장에 필요한 서버 식별자를 찾을 수 없습니다.");
                    return;
                }
            }

            bookshelfApiSyncController?.SyncBookState(book);
            isBookReportWritePanelOpen = false;
            isBookReportReadPanelOpen = true;
            RefreshHud();
        }

        private void HandleBookReportSaved(BookEntity updatedBook)
        {
            ApplyBookEntityToMeta(updatedBook);
            if (updatedBook != null && activeReportBook == updatedBook)
            {
                RequestReportComments(updatedBook);
            }

            RefreshBookDetailPanel();
            RefreshHud();
        }

        private void HandleBookReportSaveFailure(BookEntity book, BookReportMutationSnapshot rollback, string errorMessage)
        {
            Debug.LogWarning($"Reading record save failed on server. Restoring local report state. {errorMessage}");
            if (book != null)
            {
                activeReportBook = book;
                rollback.ApplyTo(book);
            }

            if (dataStore != null && book != null)
            {
                var meta = dataStore.GetOrCreateBookMeta(book);
                rollback.ApplyTo(meta);
                dataStore.ApplyBookMeta(book, meta);
            }

            ShowDeleteResultDialog(
                "독후감 저장 실패",
                $"서버 요청을 완료하지 못해 독후감을 변경 전 상태로 되돌렸습니다.\n{NormalizeMutationErrorMessage(errorMessage)}",
                false);
            RefreshBookDetailPanel();
            RefreshHud();
        }

        private void ToggleReportVisibility()
        {
            if (activeReportBook == null || dataStore == null)
            {
                return;
            }

            var meta = dataStore.GetOrCreateBookMeta(activeReportBook);
            if (meta == null)
            {
                return;
            }

            var rollback = new BookReportMutationSnapshot(activeReportBook, meta);
            meta.reportVisibility = meta.reportVisibility switch
            {
                App.Domain.VisibilityScope.Public => App.Domain.VisibilityScope.Followers,
                App.Domain.VisibilityScope.Followers => App.Domain.VisibilityScope.Private,
                _ => App.Domain.VisibilityScope.Public
            };
            meta.isPublic = meta.reportVisibility == App.Domain.VisibilityScope.Public;
            dataStore.ApplyBookMeta(activeReportBook, meta);
            var book = activeReportBook;
            var didStartSave = CanStartBookshelfApiMutation() &&
                               bookshelfApiSyncController.SaveReadingRecord(
                                   book,
                                   HandleBookReportSaved,
                                   error => HandleBookReportSaveFailure(book, rollback, error));
            if (!didStartSave)
            {
                if (usesHttpApiMode && !CanStartBookshelfApiMutation())
                {
                    HandleBookReportSaveFailure(book, rollback, "책장 API 동기화가 준비되지 않아 독후감 공개 범위를 서버에 저장할 수 없습니다.");
                    return;
                }

                if (bookshelfApiSyncController != null && bookshelfApiSyncController.IsServerSyncEnabled)
                {
                    HandleBookReportSaveFailure(book, rollback, "독후감 공개 범위 저장에 필요한 서버 식별자를 찾을 수 없습니다.");
                    return;
                }
            }

            RefreshHud();
        }

        private void CloseBookReportPanels()
        {
            var detailBook = ActiveDetailBook != null ? ActiveDetailBook : (state != null ? state.ActiveViewedBook : null);
            CloseBookReportPanelsSilently();
            if (detailBook != null &&
                state != null &&
                state.IsViewingBook &&
                bookDetailPanel != null)
            {
                ActiveDetailBook = detailBook;
                bookDetailPanel.SetActive(true);
                RefreshBookDetailPanel();
            }

            RefreshHud();
        }

        private void CloseBookReportPanelsSilently()
        {
            isBookReportWritePanelOpen = false;
            isBookReportReadPanelOpen = false;
            activeReportBook = null;
            pendingDeleteReportBook = null;
            ClearActiveReportComments();
            if (bookReportWritePanel != null)
            {
                bookReportWritePanel.SetActive(false);
            }

            if (bookReportReadPanel != null)
            {
                bookReportReadPanel.SetActive(false);
            }
        }

        private static void StyleReportVisibilityButton(Button button, bool isActive)
        {
            StyleHudButton(
                button,
                isActive ? new Color32(88, 133, 117, 245) : new Color32(255, 255, 255, 235),
                isActive ? new Color32(72, 114, 100, 255) : new Color32(241, 235, 230, 255),
                new Color32(224, 219, 214, 140),
                13,
                isActive ? Color.white : new Color32(63, 47, 38, 255),
                true);
        }

        private void RefreshBookEditPanel()
        {
            if (bookEditPanel == null || !bookEditPanel.activeSelf || ActiveDetailBook == null || dataStore == null)
            {
                return;
            }

            var meta = dataStore.GetOrCreateBookMeta(ActiveDetailBook);
            if (meta == null)
            {
                return;
            }

            var section = ActiveDetailBook.CurrentSection;
            if (bookEditEyebrowText != null)
            {
                bookEditEyebrowText.text = "책장 편집";
            }

            if (bookEditTitleText != null)
            {
                bookEditTitleText.text = TryGetBookDisplayTitle(ActiveDetailBook);
            }

            if (bookEditMetaText != null)
            {
                bookEditMetaText.text = $"위치      {(section != null ? $"{section.row + 1}행 {section.column + 1}열" : "책장 밖")}\n" +
                                        $"상태      {GetReadingStatusLabel(meta.readingStatus)}\n" +
                                        $"공개      {GetVisibilityLabel(meta.reportVisibility)}";
            }

            if (bookEditHintText != null)
            {
                bookEditHintText.text = "삭제하면 이 책은 현재 책장 배치에서 제거됩니다.";
            }

            if (deleteBookButton != null)
            {
                var isConfirmingDelete = pendingDeleteBook == ActiveDetailBook;
                deleteBookButton.gameObject.SetActive(true);
                SetButtonLabel(deleteBookButton, isConfirmingDelete ? "한 번 더 눌러 제거" : "책장에서 제거");
                StyleDeleteActionButton(deleteBookButton, true, isConfirmingDelete, 14);
            }

            if (closeBookEditButton != null)
            {
                StyleHudButton(
                    closeBookEditButton,
                    new Color32(245, 237, 226, 245),
                    new Color32(255, 248, 236, 255),
                    new Color32(224, 219, 214, 140),
                    14,
                    new Color32(84, 74, 66, 255),
                    true);
            }
        }

        private void SetActiveSectionAlignment(ShelfSectionAlignment alignment)
        {
            if (!IsBookshelfEditMode ||
                activeEditSection == null ||
                state == null)
            {
                return;
            }

            var previous = activeEditSection.alignment;
            var displayIndex = bookshelfGenerator != null ? bookshelfGenerator.DisplayBookshelfIndex : -1;
            if (!EnsureBookshelfApiMutationReady(displayIndex, "책장 칸 정렬 변경"))
            {
                RefreshSectionEditPanel();
                RefreshHud();
                return;
            }

            state.SetSectionAlignment(activeEditSection, alignment, this, true);
            if (previous != activeEditSection.alignment)
            {
                MarkBookshelfEditDirty();
                if (CanStartBookshelfApiMutation())
                {
                    bookshelfApiSyncController.SyncSectionAlignment(activeEditSection);
                }
            }

            RefreshSectionEditPanel();
            RefreshHud();
        }

        private void RefreshSectionEditPanel()
        {
            if (sectionEditPanel == null || !sectionEditPanel.activeSelf || activeEditSection == null)
            {
                return;
            }

            if (sectionEditEyebrowText != null)
            {
                sectionEditEyebrowText.text = "책장 편집";
            }

            if (sectionEditTitleText != null)
            {
                sectionEditTitleText.text = "섹션 정렬";
            }

            if (sectionEditMetaText != null)
            {
                sectionEditMetaText.text = $"선택 위치  {activeEditSection.row + 1}행 {activeEditSection.column + 1}열\n" +
                                           $"책 수      {activeEditSection.books.Count}권\n" +
                                           $"현재 정렬  {GetSectionAlignmentLabel(activeEditSection.alignment)}";
            }

            if (sectionEditHintText != null)
            {
                sectionEditHintText.text = "섹션 안의 책을 좌, 가운데, 우 기준으로 재배치합니다.";
            }

            StyleSectionAlignmentButton(alignLeftButton, activeEditSection.alignment == ShelfSectionAlignment.Left);
            StyleSectionAlignmentButton(alignCenterButton, activeEditSection.alignment == ShelfSectionAlignment.Center);
            StyleSectionAlignmentButton(alignRightButton, activeEditSection.alignment == ShelfSectionAlignment.Right);

            if (closeSectionEditButton != null)
            {
                StyleHudButton(
                    closeSectionEditButton,
                    new Color32(245, 237, 226, 245),
                    new Color32(255, 248, 236, 255),
                    new Color32(224, 219, 214, 140),
                    14,
                    new Color32(84, 74, 66, 255),
                    true);
            }
        }

        private static void StyleSectionAlignmentButton(Button button, bool isActive)
        {
            if (button == null)
            {
                return;
            }

            StyleHudButton(
                button,
                isActive ? new Color32(197, 128, 67, 255) : new Color32(255, 255, 255, 235),
                isActive ? new Color32(214, 145, 82, 255) : new Color32(241, 235, 230, 255),
                new Color32(224, 219, 214, 255),
                14,
                isActive ? Color.white : new Color32(63, 47, 38, 255),
                true);
        }

        private void RefreshBookPlacementEditor()
        {
            var isBookshelfFocused = CurrentFocusArea == ExperienceFocusArea.Bookshelf;
            var isViewingBook = state != null && state.IsViewingBook;
            var isDraggingBook = state != null && state.ActiveDraggedBook != null;
            var isBookshelfSwitching = bookshelfGenerator != null && bookshelfGenerator.IsBookshelfSwitching;
            var canOpenEditor = isBookshelfFocused &&
                                isViewingBook &&
                                guestbookOwnerMode &&
                                !IsBookshelfEditMode &&
                                !isDraggingBook &&
                                !isBookshelfSwitching &&
                                !isBookPlacementEditorOpen &&
                                (ActiveDetailBook != null || state.ActiveViewedBook != null);

            if (openBookPlacementEditorButton != null)
            {
                openBookPlacementEditorButton.gameObject.SetActive(canOpenEditor);
                openBookPlacementEditorButton.interactable = canOpenEditor;
                StyleHudButton(
                    openBookPlacementEditorButton,
                    new Color32(255, 255, 255, 238),
                    new Color32(241, 235, 230, 255),
                    new Color32(224, 219, 214, 140),
                    16,
                    new Color32(45, 50, 58, 255),
                    true);
            }

            if (bookPlacementEditorPanel == null)
            {
                return;
            }

            if (isBookPlacementEditorOpen && placementEditBook == null)
            {
                CloseBookPlacementEditor(false);
            }

            var showPanel = isBookPlacementEditorOpen &&
                            placementEditBook != null &&
                            isBookshelfFocused &&
                            !IsBookshelfEditMode &&
                            !isDraggingBook &&
                            !isBookshelfSwitching;

            bookPlacementEditorPanel.SetActive(showPanel);
            if (!showPanel)
            {
                return;
            }

            placementStagedOrientation = placementEditBook.orientation;
            StyleBookPlacementOptionButton(placementSpineButton, placementStagedOrientation == BookOrientation.Spine);
            StyleBookPlacementOptionButton(placementAngledButton, placementStagedOrientation == BookOrientation.Angled45);
            StyleBookPlacementOptionButton(placementFrontButton, placementStagedOrientation == BookOrientation.Front);
            StyleHudButton(
                cancelPlacementButton,
                new Color32(245, 237, 226, 245),
                new Color32(255, 248, 236, 255),
                new Color32(224, 219, 214, 140),
                14,
                new Color32(84, 74, 66, 255),
                true);
            StyleHudButton(
                confirmPlacementButton,
                new Color32(197, 128, 67, 245),
                new Color32(214, 145, 82, 255),
                new Color32(224, 219, 214, 140),
                14,
                Color.white,
                true);
            bookPlacementEditorPanel.transform.SetAsLastSibling();
        }

        private static void StyleBookPlacementOptionButton(Button button, bool isActive)
        {
            if (button == null)
            {
                return;
            }

            StyleHudButton(
                button,
                isActive ? new Color32(45, 50, 58, 245) : new Color32(255, 255, 255, 235),
                isActive ? new Color32(64, 70, 79, 255) : new Color32(241, 235, 230, 255),
                new Color32(224, 219, 214, 140),
                14,
                isActive ? Color.white : new Color32(63, 47, 38, 255),
                true);
        }

        private void EnsureGuestbookBoardUi()
        {
            if (ShouldUseExternalDashboardPanel)
            {
                SetExternalDashboardPanelPresentation(CurrentFocusArea == ExperienceFocusArea.Guestbook);
                return;
            }

            // Legacy mode keeps the UI Toolkit dashboard out of the scene so it
            // cannot render or steal raycasts from the old uGUI board.
            if (externalDashboardPanel != null && externalDashboardPanel.activeSelf)
            {
                externalDashboardPanel.SetActive(false);
            }

            if (guestbookBoardCanvas != null)
            {
                return;
            }

            var guestbookBoard = FindFirstObjectByType<GuestbookBoardHitArea>();
            if (guestbookBoard == null)
            {
                return;
            }

            SeedGuestbookNotes();

            var canvasObject = new GameObject("GuestbookBoardCanvas");
            canvasObject.transform.SetParent(guestbookBoard.transform, false);
            canvasObject.transform.localPosition = new Vector3(-0.058f, 0f, 0f);
            canvasObject.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            canvasObject.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);

            guestbookBoardCanvas = canvasObject.AddComponent<Canvas>();
            guestbookBoardCanvas.renderMode = RenderMode.WorldSpace;
            guestbookBoardCanvas.worldCamera = Camera.main;
            guestbookBoardCanvas.sortingOrder = 5;
            canvasObject.AddComponent<GraphicRaycaster>();

            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1720f, 1220f);
            canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
            canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            canvasRect.pivot = new Vector2(0.5f, 0.5f);

            guestbookPreviewPanel = CreateWorldPanel(canvasObject.transform, "GuestbookPreviewPanel", GuestbookBoardSize);
            var previewRect = guestbookPreviewPanel.GetComponent<RectTransform>();
            previewRect.anchorMin = new Vector2(0.5f, 0.5f);
            previewRect.anchorMax = new Vector2(0.5f, 0.5f);
            previewRect.pivot = new Vector2(0.5f, 0.5f);
            previewRect.anchoredPosition = Vector2.zero;
            guestbookPreviewPanel.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            guestbookPreviewPanel.GetComponent<Image>().raycastTarget = false;
            guestbookPreviewPanel.AddComponent<WorldSpaceRaycastPassthrough>();
            guestbookPreviewPanel.AddComponent<RectMask2D>();
            CreateGuestbookPreviewNotes(guestbookPreviewPanel.transform);

            guestbookPanel = CreateWorldPanel(canvasObject.transform, "GuestbookBoardPanel", GuestbookBoardSize);
            var boardRect = guestbookPanel.GetComponent<RectTransform>();
            boardRect.anchorMin = new Vector2(0.5f, 0.5f);
            boardRect.anchorMax = new Vector2(0.5f, 0.5f);
            boardRect.pivot = new Vector2(0.5f, 0.5f);
            boardRect.anchoredPosition = Vector2.zero;
            StyleGuestbookBoardSurface(guestbookPanel);
            guestbookPanel.AddComponent<WorldSpaceRaycastPassthrough>();
            guestbookPanel.SetActive(false);

            var boardContentRoot = CreateWorldContainer(
                guestbookPanel.transform,
                "BoardContentRoot",
                GuestbookBoardSize - Vector2.one * GuestbookBoardPadding * 2f,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero);
            boardContentRoot.gameObject.AddComponent<RectMask2D>();

            var noteAreaSize = new Vector2(
                boardContentRoot.sizeDelta.x,
                boardContentRoot.sizeDelta.y - GuestbookInterfacePanelHeight - GuestbookPaginationHeight - GuestbookPaginationToPanelGap - GuestbookNoteToPaginationGap);

            guestbookGridRect = CreateWorldContainer(
                boardContentRoot,
                "GuestbookNoteLayer",
                noteAreaSize,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero);
            guestbookGridRect.gameObject.AddComponent<RectMask2D>();
            guestbookGridCanvasGroup = guestbookGridRect.gameObject.AddComponent<CanvasGroup>();
            guestbookGridCanvasGroup.alpha = 1f;

            guestbookGridOverlayRect = CreateWorldContainer(
                boardContentRoot,
                "GuestbookNoteOverlay",
                noteAreaSize,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero);

            guestbookEmptyStateText = CreateWorldLabel(
                guestbookGridOverlayRect,
                "EmptyState",
                Vector2.zero,
                new Vector2(760f, 44f),
                22,
                new Color32(138, 125, 118, 255),
                TextAnchor.MiddleCenter);
            guestbookEmptyStateText.fontStyle = FontStyle.Bold;
            var emptyRect = guestbookEmptyStateText.rectTransform;
            emptyRect.anchorMin = new Vector2(0.5f, 0.5f);
            emptyRect.anchorMax = new Vector2(0.5f, 0.5f);
            emptyRect.pivot = new Vector2(0.5f, 0.5f);
            emptyRect.anchoredPosition = Vector2.zero;

            guestbookPrivacyText = CreateWorldLabel(
                guestbookGridOverlayRect,
                "PrivacyText",
                Vector2.zero,
                new Vector2(760f, 52f),
                24,
                new Color32(141, 110, 99, 255),
                TextAnchor.MiddleCenter);
            guestbookPrivacyText.fontStyle = FontStyle.Bold;
            var privacyRect = guestbookPrivacyText.rectTransform;
            privacyRect.anchorMin = new Vector2(0.5f, 0.5f);
            privacyRect.anchorMax = new Vector2(0.5f, 0.5f);
            privacyRect.pivot = new Vector2(0.5f, 0.5f);
            privacyRect.anchoredPosition = Vector2.zero;

            for (var i = 0; i < guestbookNoteCards.Length; i++)
            {
                var noteCard = CreateWorldPanel(guestbookGridRect, $"GuestbookNote_{i}", GuestbookFocusedNoteBaseSize);
                var noteRect = noteCard.GetComponent<RectTransform>();
                noteRect.anchorMin = new Vector2(0.5f, 0.5f);
                noteRect.anchorMax = new Vector2(0.5f, 0.5f);
                noteRect.pivot = new Vector2(0.5f, 0.5f);
                noteRect.anchoredPosition = Vector2.zero;
                noteCard.AddComponent<UiHoverScaleEffect>();
                StyleGuestbookCard(noteCard);

                guestbookNoteMessageTexts[i] = CreateWorldLabel(
                    noteCard.transform,
                    "Message",
                    new Vector2(20f, -22f),
                    new Vector2(320f, 104f),
                    22,
                    new Color32(78, 59, 51, 255));
                guestbookNoteMessageTexts[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                guestbookNoteMessageTexts[i].verticalOverflow = VerticalWrapMode.Truncate;

                guestbookNoteAuthorTexts[i] = CreateWorldLabel(
                    noteCard.transform,
                    "Author",
                    new Vector2(20f, -146f),
                    new Vector2(180f, 24f),
                    16,
                    new Color32(138, 125, 118, 255),
                    TextAnchor.MiddleLeft);

                var slotIndex = i;
                guestbookReplyButtons[i] = CreateButton(
                    noteCard.transform,
                    "ReplyButton",
                    "Reply",
                    new Vector2(-20f, 20f),
                    () => ReplyToGuestbookNote(slotIndex),
                    new Vector2(92f, 34f),
                    new Vector2(1f, 0f));
                StyleGuestbookActionButton(
                    guestbookReplyButtons[i],
                    new Color32(255, 255, 255, 255),
                    new Color32(241, 235, 230, 255),
                    new Color32(240, 236, 232, 255),
                    14,
                    new Color32(78, 59, 51, 255));

                guestbookNoteCards[i] = noteRect;
            }

            guestbookPaginationRect = CreateWorldContainer(
                boardContentRoot,
                "PaginationRoot",
                new Vector2(boardContentRoot.sizeDelta.x - 56f, GuestbookPaginationHeight),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, GuestbookInterfacePanelHeight + GuestbookPaginationToPanelGap));
            ConfigureHorizontalLayout(guestbookPaginationRect, TextAnchor.MiddleCenter, 14f);

            guestbookPreviousPageButton = CreateButton(
                guestbookPaginationRect,
                "GuestbookPrevButton",
                "< Prev",
                Vector2.zero,
                () => ChangeGuestbookPage(-1),
                new Vector2(96f, 40f),
                new Vector2(0.5f, 0.5f));
            ConfigureFixedLayoutElement(guestbookPreviousPageButton.GetComponent<RectTransform>(), 96f, 40f);

            var pageNumberRowRect = CreateWorldContainer(
                guestbookPaginationRect,
                "PageNumbersRow",
                new Vector2(0f, 40f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero);
            ConfigureHorizontalLayout(pageNumberRowRect, TextAnchor.MiddleCenter, 12f);
            ConfigureContentSizeFitter(pageNumberRowRect);
            ConfigureFixedLayoutElement(pageNumberRowRect, -1f, 40f, 72f);

            guestbookNextPageButton = CreateButton(
                guestbookPaginationRect,
                "GuestbookNextButton",
                "Next >",
                Vector2.zero,
                () => ChangeGuestbookPage(1),
                new Vector2(96f, 40f),
                new Vector2(0.5f, 0.5f));
            ConfigureFixedLayoutElement(guestbookNextPageButton.GetComponent<RectTransform>(), 96f, 40f);

            for (var i = 0; i < guestbookPageButtons.Length; i++)
            {
                var slotIndex = i;
                guestbookPageButtons[i] = CreateButton(
                    pageNumberRowRect,
                    $"GuestbookPageButton_{i}",
                    (i + 1).ToString(),
                    Vector2.zero,
                    () => GoToGuestbookPageFromSlot(slotIndex),
                    new Vector2(48f, 40f),
                    new Vector2(0.5f, 0.5f));
                ConfigureFixedLayoutElement(guestbookPageButtons[i].GetComponent<RectTransform>(), 48f, 40f);
                guestbookPageButtonPageIndices[i] = i;
            }

            guestbookInterfacePanelRect = CreateWorldContainer(
                boardContentRoot,
                "GuestbookInterfacePanel",
                new Vector2(boardContentRoot.sizeDelta.x, GuestbookInterfacePanelHeight),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                Vector2.zero);
            var interfacePanelImage = guestbookInterfacePanelRect.gameObject.AddComponent<Image>();
            StyleGuestbookInterfacePanel(interfacePanelImage);

            guestbookHeaderRowRect = CreateWorldContainer(
                guestbookInterfacePanelRect,
                "HeaderRow",
                new Vector2(guestbookInterfacePanelRect.sizeDelta.x - 56f, 44f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -18f));

            guestbookVisitorStatsText = CreateWorldLabel(
                guestbookHeaderRowRect,
                "VisitorStats",
                new Vector2(0f, -2f),
                new Vector2(176f, 40f),
                24,
                new Color32(78, 59, 51, 255),
                TextAnchor.MiddleLeft);
            guestbookVisitorStatsText.fontStyle = FontStyle.Bold;

            guestbookLikeStatsText = CreateWorldLabel(
                guestbookHeaderRowRect,
                "LikeStats",
                new Vector2(192f, -2f),
                new Vector2(152f, 40f),
                24,
                new Color32(78, 59, 51, 255),
                TextAnchor.MiddleLeft);
            guestbookLikeStatsText.fontStyle = FontStyle.Bold;

            guestbookVisibilityToggleButton = CreateButton(
                guestbookHeaderRowRect,
                "GuestbookVisibilityToggle",
                "Board Open",
                Vector2.zero,
                ToggleGuestbookVisibility,
                new Vector2(188f, 44f),
                new Vector2(1f, 1f));
            StyleGuestbookActionButton(
                guestbookVisibilityToggleButton,
                new Color32(141, 110, 99, 255),
                new Color32(123, 95, 85, 255),
                new Color32(187, 171, 162, 255),
                18,
                Color.white,
                true);

            guestbookInputRowRect = CreateWorldContainer(
                guestbookInterfacePanelRect,
                "InputRow",
                new Vector2(guestbookInterfacePanelRect.sizeDelta.x - 56f, 72f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -84f));

            guestbookBoardStatusText = CreateWorldLabel(
                guestbookInterfacePanelRect,
                "BoardStatus",
                new Vector2(28f, -162f),
                new Vector2(guestbookInterfacePanelRect.sizeDelta.x - 56f, 18f),
                15,
                new Color32(138, 125, 118, 255),
                TextAnchor.MiddleLeft);

            guestbookInputField = CreateWorldInputField(
                guestbookInputRowRect,
                "GuestbookInputField",
                Vector2.zero,
                new Vector2(guestbookInputRowRect.sizeDelta.x - 164f, 72f),
                "Write a note... (max 30 chars)");
            guestbookInputField.characterLimit = 30;
            guestbookInputField.onValueChanged.AddListener(UpdateGuestbookCharCount);

            guestbookSubmitButton = CreateButton(
                guestbookInputRowRect,
                "GuestbookSubmitButton",
                "Pin",
                Vector2.zero,
                SubmitGuestbookMessage,
                new Vector2(148f, 72f),
                new Vector2(1f, 1f));
            StyleGuestbookActionButton(
                guestbookSubmitButton,
                new Color32(141, 110, 99, 255),
                new Color32(123, 95, 85, 255),
                new Color32(187, 171, 162, 255),
                20,
                Color.white,
                true);

            guestbookCharCountText = CreateWorldLabel(
                guestbookInputField.transform,
                "CharCount",
                new Vector2(guestbookInputField.GetComponent<RectTransform>().sizeDelta.x - 90f, -48f),
                new Vector2(70f, 18f),
                14,
                new Color32(138, 125, 118, 255),
                TextAnchor.MiddleRight);

            guestbookInterfacePanelRect.SetAsLastSibling();
            UpdateGuestbookCharCount(string.Empty);
            SetGuestbookFocusPresentation(CurrentFocusArea == ExperienceFocusArea.Guestbook);
            RefreshGuestbookPanel();
        }

        private void CreateGuestbookPreviewNotes(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (var i = 0; i < GuestbookPreviewMessages.Length; i++)
            {
                var noteCard = CreateWorldPanel(parent, $"PreviewNote_{i}", GuestbookPreviewCardSize);
                var noteRect = noteCard.GetComponent<RectTransform>();
                noteRect.anchorMin = new Vector2(0.5f, 0.5f);
                noteRect.anchorMax = new Vector2(0.5f, 0.5f);
                noteRect.pivot = new Vector2(0.5f, 0.5f);
                noteRect.anchoredPosition = GuestbookPreviewOffsets[i];
                noteRect.localRotation = Quaternion.Euler(0f, 0f, GuestbookPreviewRotations[i]);
                noteRect.localScale = Vector3.one * GuestbookPreviewScales[i];
                StyleGuestbookCard(noteCard);

                var cardImage = noteCard.GetComponent<Image>();
                if (cardImage != null)
                {
                    cardImage.color = GetGuestbookNoteColor(i);
                    cardImage.raycastTarget = false;
                }

                var messageText = CreateWorldLabel(
                    noteCard.transform,
                    "PreviewMessage",
                    new Vector2(18f, -22f),
                    new Vector2(GuestbookPreviewCardSize.x - 36f, 70f),
                    20,
                    new Color32(78, 59, 51, 255));
                messageText.text = GuestbookPreviewMessages[i];
                messageText.raycastTarget = false;

                var captionText = CreateWorldLabel(
                    noteCard.transform,
                    "PreviewCaption",
                    new Vector2(18f, -104f),
                    new Vector2(140f, 18f),
                    13,
                    new Color32(138, 125, 118, 255),
                    TextAnchor.MiddleLeft);
                captionText.text = GuestbookPreviewCaptions[i];
                captionText.raycastTarget = false;
            }
        }

        private void SetGuestbookFocusPresentation(bool isFocused)
        {
            // (v1.0 dashboard 통합) 외부 prefab 모드면 우리 DashboardScreenPanel만 토글.
            // 월드 보드가 생성된 뒤에는 매 프레임 위치를 보정해 방 뷰에서도 항상 보이게 둔다.
            if (ShouldUseExternalDashboardPanel)
            {
                SetExternalDashboardPanelPresentation(isFocused);
                return;
            }

            // 옛 path — 외부 패널 reference가 있어도 사용 안 하므로 강제로 hidden
            if (externalDashboardPanel != null && externalDashboardPanel.activeSelf)
            {
                externalDashboardPanel.SetActive(false);
            }

            if (guestbookPanel != null)
            {
                guestbookPanel.SetActive(isFocused);
            }

            if (guestbookPreviewPanel != null)
            {
                guestbookPreviewPanel.SetActive(!isFocused);
            }
        }

        private void SetExternalDashboardPanelPresentation(bool isFocused)
        {
            if (externalDashboardPanel == null)
            {
                return;
            }

            if (!MountExternalDashboardPanel())
            {
                SetGuestbookBoardHitAreaColliderEnabled(true);
                if (externalDashboardPanel.activeSelf)
                {
                    externalDashboardPanel.SetActive(false);
                }

                return;
            }

            var controller = externalDashboardPanel.GetComponent<DashboardScreenController>();
            if (controller == null)
            {
                controller = externalDashboardPanel.GetComponentInChildren<DashboardScreenController>(true);
            }

            if (controller != null)
            {
                controller.SetDimmed(!isFocused);
                controller.SetInteractionEnabled(isFocused);
            }

            SetGuestbookBoardHitAreaColliderEnabled(!isFocused);

            if (!externalDashboardPanel.activeSelf)
            {
                externalDashboardPanel.SetActive(true);
            }
        }

        private static void SetGuestbookBoardHitAreaColliderEnabled(bool enabled)
        {
            var hitAreas = FindObjectsByType<GuestbookBoardHitArea>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < hitAreas.Length; i++)
            {
                var hitArea = hitAreas[i];
                if (hitArea == null)
                {
                    continue;
                }

                var colliders = hitArea.GetComponents<Collider>();
                for (var j = 0; j < colliders.Length; j++)
                {
                    if (colliders[j] != null)
                    {
                        colliders[j].enabled = enabled;
                    }
                }
            }
        }

        private void SyncExternalDashboardPanelPresentation()
        {
            if (!ShouldUseExternalDashboardPanel || externalDashboardPanel == null)
            {
                return;
            }

            SetExternalDashboardPanelPresentation(CurrentFocusArea == ExperienceFocusArea.Guestbook);
        }

        private bool MountExternalDashboardPanel()
        {
            if (externalDashboardPanel == null)
            {
                return false;
            }

            var dynamicHitArea = FindFirstObjectByType<GuestbookBoardHitArea>();
            if (dynamicHitArea == null)
            {
                return false;
            }

            var panelTransform = externalDashboardPanel.transform;
            if (panelTransform.parent != null)
            {
                panelTransform.SetParent(null, true);
            }

            // Position 결정 — visual skin 이 attach 돼 있으면 그 renderer bounds.center 기준,
            // 없으면 (native dashboard) hitArea + ExternalDashboardPanelSurfaceOffset.
            //
            // visual skin 케이스 (vintage_dashboard 등):
            //   - finalPosition = visualBounds.center + frontNormal * (halfThickness + tinyOffset)
            //   - frontNormal = hitArea.right (= hitArea local +X in world — board outward axis).
            //     hitArea collider 가 (size.z=width, .y=height, .x=thickness) 컨벤션으로 setup 됐고
            //     native dashboard 에서 ExternalDashboardPanelSurfaceOffset.x>0 이 작동했으므로
            //     local +X 가 outward 방향.
            //   - in-plane(Y/Z 평면) 위치도 visualBounds.center 기준으로 정렬 → screen 이 visual
            //     mesh 정중앙에 위치 (좌우/상하 치우침 없음). 사용자 요구 "screen center == visual center".
            //   - halfThickness 는 hitArea.right axis 에 사영한 bounds 두께의 절반 — skin 의 두께가
            //     얼마든 panel 이 정확히 front face 바로 앞에 위치.
            //   - tinyOffset (3mm) — z-fight 방지 최소 거리. 사용자 요구 "붕 뜬 느낌 제거".
            Transform visualSkin = FindDashboardVisualSkin(dynamicHitArea.transform);
            Vector3 hitAreaRight = dynamicHitArea.transform.right;
            Vector3 finalPosition;
            bool usedVisualSkin = false;
            Bounds visualBounds = new Bounds();
            float halfThickness = 0f;
            if (visualSkin != null && TryComputeRendererBounds(visualSkin.gameObject, out visualBounds))
            {
                Vector3 boundsSize = visualBounds.size;
                halfThickness = (Mathf.Abs(boundsSize.x * hitAreaRight.x)
                                + Mathf.Abs(boundsSize.y * hitAreaRight.y)
                                + Mathf.Abs(boundsSize.z * hitAreaRight.z)) * 0.5f;
                finalPosition = visualBounds.center + hitAreaRight * (halfThickness + ExternalDashboardPanelTinyForwardOffset);
                usedVisualSkin = true;
            }
            else
            {
                finalPosition = dynamicHitArea.transform.TransformPoint(ExternalDashboardPanelSurfaceOffset);
            }

            panelTransform.position = finalPosition;
            panelTransform.rotation = dynamicHitArea.transform.rotation * ExternalDashboardPanelLocalRotation;
            var panelScale = ResolveExternalDashboardPanelScale(dynamicHitArea);
            panelTransform.localScale = new Vector3(-panelScale, panelScale, panelScale);

            // Log throttle — 첫 호출 + skin attach/detach 상태 변경 시 1회씩.
            if (!externalDashboardPanelMountLogged || usedVisualSkin != externalDashboardPanelMountLastHadSkin)
            {
                float distFromVisualCenter = usedVisualSkin ? Vector3.Distance(panelTransform.position, visualBounds.center) : -1f;
                Debug.Log($"[ExternalDashboardPanel] MountExternalDashboardPanel result — " +
                          $"usedVisualSkin={usedVisualSkin} " +
                          (usedVisualSkin
                              ? $"visualBounds.center={visualBounds.center} visualBounds.size={visualBounds.size} halfThickness={halfThickness:F4} tinyOffset={ExternalDashboardPanelTinyForwardOffset:F4} "
                              : $"fallback=hitArea+SurfaceOffset({ExternalDashboardPanelSurfaceOffset}) ") +
                          $"frontNormal(hitArea.right)={hitAreaRight} " +
                          $"finalPanelWorldPosition={panelTransform.position} " +
                          $"panel.distFromVisualCenter={distFromVisualCenter:F4} " +
                          $"panel.forward={panelTransform.forward} " +
                          $"panel.rotation.eulerAngles={panelTransform.rotation.eulerAngles} " +
                          $"panel.localScale={panelTransform.localScale} " +
                          $"hitArea.position={dynamicHitArea.transform.position} " +
                          $"hitArea.up(local+Y)={dynamicHitArea.transform.up} hitArea.forward(local+Z)={dynamicHitArea.transform.forward} " +
                          $"panelScale={panelScale:F5}");
                externalDashboardPanelMountLogged = true;
                externalDashboardPanelMountLastHadSkin = usedVisualSkin;
            }
            return true;
        }

        // ThemeDashboardVisual 검색 — hitArea 의 parent (GuestbookBoard 기본) → hitArea 자신 →
        // scene 전체 순. visual skin 이 attach 안 됐으면 null.
        private static Transform FindDashboardVisualSkin(Transform hitAreaTransform)
        {
            if (hitAreaTransform == null) return null;

            var parent = hitAreaTransform.parent;
            if (parent != null)
            {
                var skin = parent.Find(DashboardVisualSkinChildName);
                if (skin != null) return skin;
            }
            var directChild = hitAreaTransform.Find(DashboardVisualSkinChildName);
            if (directChild != null) return directChild;

            var all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].gameObject.name == DashboardVisualSkinChildName)
                {
                    return all[i];
                }
            }
            return null;
        }

        // Combined renderer world-AABB bounds. ParticleSystem/Trail/Line / 비활성 renderer / 빈
        // bounds 제외. 최소 1개 valid renderer 가 있으면 true.
        private static bool TryComputeRendererBounds(GameObject go, out Bounds bounds)
        {
            bounds = new Bounds();
            if (go == null) return false;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return false;
            bool hasAny = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (!r.gameObject.activeInHierarchy) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var b = r.bounds;
                if (b.size.sqrMagnitude < 1e-8f) continue;
                if (!hasAny) { bounds = b; hasAny = true; }
                else { bounds.Encapsulate(b); }
            }
            return hasAny;
        }

        private static float ResolveExternalDashboardPanelScale(GuestbookBoardHitArea dynamicHitArea)
        {
            if (dynamicHitArea == null)
            {
                return ExternalDashboardPanelFallbackWorldScale;
            }

            var collider = dynamicHitArea.GetComponent<BoxCollider>();
            if (collider == null)
            {
                return ExternalDashboardPanelFallbackWorldScale;
            }

            var usableWidth = Mathf.Max(0.1f, collider.size.z - ExternalDashboardPanelHorizontalInset);
            var usableHeight = Mathf.Max(0.1f, collider.size.y - ExternalDashboardPanelVerticalInset);
            var scaleByWidth = usableWidth / ExternalDashboardPanelReferenceSize.x;
            var scaleByHeight = usableHeight / ExternalDashboardPanelReferenceSize.y;
            return Mathf.Min(scaleByWidth, scaleByHeight);
        }

        private void SeedGuestbookNotes()
        {
            if (guestbookNotes.Count > 0)
            {
                return;
            }

            if (dataStore != null)
            {
                var entries = dataStore.GetGuestbookEntries();
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    guestbookNotes.Add(new GuestbookNoteData
                    {
                        authorName = entry.authorName,
                        message = entry.message.Length > 30 ? entry.message.Substring(0, 30) : entry.message,
                        timestampLabel = entry.timestampLabel,
                        color = GetGuestbookNoteColor(i)
                    });
                }
            }

            if (guestbookNotes.Count == 0)
            {
                guestbookNotes.Add(new GuestbookNoteData { authorName = "Minji", message = "The room feels warm and calm.", timestampLabel = "Today", color = GetGuestbookNoteColor(0) });
                guestbookNotes.Add(new GuestbookNoteData { authorName = "Hyunwoo", message = "I will leave a book pick next time.", timestampLabel = "1h ago", color = GetGuestbookNoteColor(1) });
                guestbookNotes.Add(new GuestbookNoteData { authorName = "Seoyeon", message = "The afternoon light is perfect here.", timestampLabel = "Yesterday", color = GetGuestbookNoteColor(2) });
            }
        }

        private void RefreshGuestbookPanel(bool animatePageChange = false)
        {
            if (guestbookPanel == null || guestbookBoardCanvas == null)
            {
                return;
            }

            RefreshGuestbookGridMetrics();

            if (guestbookVisitorStatsText != null)
            {
                guestbookVisitorStatsText.text = $"Visitors {guestbookVisitorCount}";
            }

            if (guestbookLikeStatsText != null)
            {
                guestbookLikeStatsText.text = $"Likes {guestbookLikeCount}";
            }

            var canWrite = isGuestbookPublic || guestbookOwnerMode;
            if (guestbookInputField != null)
            {
                guestbookInputField.interactable = canWrite;
            }

            if (guestbookSubmitButton != null)
            {
                guestbookSubmitButton.interactable = canWrite;
            }

            if (guestbookVisibilityToggleButton != null)
            {
                guestbookVisibilityToggleButton.interactable = guestbookOwnerMode;
                var buttonLabel = guestbookVisibilityToggleButton.GetComponentInChildren<Text>();
                if (buttonLabel != null)
                {
                    buttonLabel.text = isGuestbookPublic ? "Board Open" : "Board Closed";
                }
            }

            RefreshGuestbookPagination();

            if (guestbookPageFadeCoroutine != null)
            {
                StopCoroutine(guestbookPageFadeCoroutine);
                guestbookPageFadeCoroutine = null;
            }

            if (animatePageChange && guestbookPanel.activeSelf && guestbookGridCanvasGroup != null)
            {
                guestbookPageFadeCoroutine = StartCoroutine(AnimateGuestbookPageTransition());
            }
            else
            {
                if (guestbookGridCanvasGroup != null)
                {
                    guestbookGridCanvasGroup.alpha = 1f;
                }

                ApplyGuestbookPageContent();
            }

            if (guestbookBoardStatusText != null && string.IsNullOrWhiteSpace(guestbookBoardStatusText.text))
            {
                guestbookBoardStatusText.text = canWrite
                    ? "Leave a short note and pin it to the board."
                    : "Board is closed right now.";
            }
        }

        private void RefreshGuestbookGridMetrics()
        {
            if (guestbookGridRect == null)
            {
                return;
            }

            guestbookColumnCount = GuestbookVisibleColumns;
            guestbookCardsPerPage = guestbookColumnCount * GuestbookVisibleRows;

            var noteLayerSize = new Vector2(
                GuestbookBoardSize.x - GuestbookBoardPadding * 2f,
                GuestbookBoardSize.y - GuestbookBoardPadding * 2f - GuestbookInterfacePanelHeight - GuestbookPaginationHeight - GuestbookPaginationToPanelGap - GuestbookNoteToPaginationGap);

            guestbookGridRect.sizeDelta = noteLayerSize;
            if (guestbookGridOverlayRect != null)
            {
                guestbookGridOverlayRect.sizeDelta = noteLayerSize;
            }

            var cardScale = Mathf.Clamp01(Mathf.Min(
                noteLayerSize.x / (GuestbookFocusedNoteBaseSize.x * 3.9f),
                noteLayerSize.y / (GuestbookFocusedNoteBaseSize.y * 3.35f)));
            cardScale = Mathf.Max(0.84f, cardScale);

            var cardWidth = Mathf.Round(GuestbookFocusedNoteBaseSize.x * cardScale);
            var cardHeight = Mathf.Round(GuestbookFocusedNoteBaseSize.y * cardScale);
            guestbookCurrentCardSize = new Vector2(cardWidth, cardHeight);

            for (var i = 0; i < guestbookNoteCards.Length; i++)
            {
                UpdateGuestbookCardLayout(i, cardWidth, cardHeight);
            }

            if (guestbookEmptyStateText != null)
            {
                guestbookEmptyStateText.rectTransform.sizeDelta = new Vector2(noteLayerSize.x - 80f, 44f);
            }

            if (guestbookPrivacyText != null)
            {
                guestbookPrivacyText.rectTransform.sizeDelta = new Vector2(noteLayerSize.x - 80f, 52f);
            }

            guestbookCurrentPageIndex = Mathf.Clamp(guestbookCurrentPageIndex, 0, GetGuestbookTotalPages() - 1);

            if (guestbookInterfacePanelRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(guestbookInterfacePanelRect);
            }
        }

        private void UpdateGuestbookCardLayout(int index, float cardWidth, float cardHeight)
        {
            if (index < 0 || index >= guestbookNoteCards.Length || guestbookNoteCards[index] == null)
            {
                return;
            }

            guestbookNoteCards[index].sizeDelta = new Vector2(cardWidth, cardHeight);

            var messageText = guestbookNoteMessageTexts[index];
            if (messageText != null)
            {
                messageText.rectTransform.anchoredPosition = new Vector2(18f, -20f);
                messageText.rectTransform.sizeDelta = new Vector2(cardWidth - 36f, cardHeight - 84f);
                messageText.fontSize = Mathf.Clamp(Mathf.RoundToInt(cardHeight * 0.12f), 18, 21);
            }

            var authorText = guestbookNoteAuthorTexts[index];
            if (authorText != null)
            {
                authorText.rectTransform.anchoredPosition = new Vector2(18f, -(cardHeight - 40f));
                authorText.rectTransform.sizeDelta = new Vector2(cardWidth - 132f, 20f);
                authorText.fontSize = Mathf.Clamp(Mathf.RoundToInt(cardHeight * 0.09f), 14, 15);
            }

            var replyButton = guestbookReplyButtons[index];
            if (replyButton != null)
            {
                var replyRect = replyButton.GetComponent<RectTransform>();
                replyRect.anchorMin = new Vector2(1f, 0f);
                replyRect.anchorMax = new Vector2(1f, 0f);
                replyRect.pivot = new Vector2(1f, 0f);
                replyRect.anchoredPosition = new Vector2(-18f, 18f);
                replyRect.sizeDelta = new Vector2(Mathf.Clamp(cardWidth * 0.36f, 82f, 94f), 32f);

                var replyLabel = replyButton.GetComponentInChildren<Text>();
                if (replyLabel != null)
                {
                    replyLabel.fontSize = 13;
                }
            }
        }

        private void ApplyGuestbookPageContent()
        {
            var showPrivacyMessage = !isGuestbookPublic;
            var noteCount = showPrivacyMessage ? 0 : guestbookNotes.Count;
            var startIndex = guestbookCurrentPageIndex * guestbookCardsPerPage;

            if (guestbookPrivacyText != null)
            {
                guestbookPrivacyText.text = "Board is currently closed.";
                guestbookPrivacyText.gameObject.SetActive(showPrivacyMessage);
            }

            if (guestbookEmptyStateText != null)
            {
                guestbookEmptyStateText.text = "No notes yet. Pin the first memo.";
                guestbookEmptyStateText.gameObject.SetActive(!showPrivacyMessage && noteCount == 0);
            }

            for (var slotIndex = 0; slotIndex < guestbookNoteCards.Length; slotIndex++)
            {
                if (guestbookNoteCards[slotIndex] == null)
                {
                    continue;
                }

                var noteIndex = startIndex + slotIndex;
                var isVisible = !showPrivacyMessage && slotIndex < guestbookCardsPerPage && noteIndex < noteCount;
                guestbookNoteCards[slotIndex].gameObject.SetActive(isVisible);
                guestbookNoteCards[slotIndex].anchoredPosition = Vector2.zero;
                guestbookNoteCards[slotIndex].localRotation = Quaternion.identity;

                var hoverEffect = guestbookNoteCards[slotIndex].GetComponent<UiHoverScaleEffect>();
                if (hoverEffect != null)
                {
                    hoverEffect.SetBaseScale(1f);
                }
                else
                {
                    guestbookNoteCards[slotIndex].localScale = Vector3.one;
                }

                if (!isVisible)
                {
                    continue;
                }

                var note = guestbookNotes[noteIndex];
                var cardImage = guestbookNoteCards[slotIndex].GetComponent<Image>();
                if (cardImage != null)
                {
                    cardImage.color = note.color.a > 0f ? note.color : GetGuestbookNoteColor(noteIndex);
                }

                if (guestbookNoteMessageTexts[slotIndex] != null)
                {
                    guestbookNoteMessageTexts[slotIndex].text = note.message;
                }

                if (guestbookNoteAuthorTexts[slotIndex] != null)
                {
                    guestbookNoteAuthorTexts[slotIndex].text = $"by {note.authorName}";
                }

                if (guestbookReplyButtons[slotIndex] != null)
                {
                    guestbookReplyButtons[slotIndex].gameObject.SetActive(true);
                    guestbookReplyButtons[slotIndex].interactable = guestbookOwnerMode;
                }

                guestbookNoteCards[slotIndex].anchoredPosition = GetGuestbookFocusedNotePosition(slotIndex, note, noteIndex);
                guestbookNoteCards[slotIndex].localRotation = Quaternion.Euler(0f, 0f, GetGuestbookFocusedNoteRotation(slotIndex, note, noteIndex));
                var noteScale = GetGuestbookFocusedNoteScale(slotIndex, note, noteIndex);
                if (hoverEffect != null)
                {
                    hoverEffect.SetBaseScale(noteScale);
                }
                else
                {
                    guestbookNoteCards[slotIndex].localScale = Vector3.one * noteScale;
                }
                guestbookNoteCards[slotIndex].SetSiblingIndex(slotIndex);
            }
        }

        private Vector2 GetGuestbookFocusedNotePosition(int slotIndex, GuestbookNoteData note, int noteIndex)
        {
            var anchor = GuestbookFocusedNoteAnchors[slotIndex % GuestbookFocusedNoteAnchors.Length];
            var horizontalSpan = Mathf.Max(0f, (guestbookGridRect.sizeDelta.x - guestbookCurrentCardSize.x - 120f) * 0.5f);
            var verticalSpan = Mathf.Max(0f, (guestbookGridRect.sizeDelta.y - guestbookCurrentCardSize.y - 88f) * 0.5f);
            var seed = GetGuestbookNoteSeed(noteIndex, note);

            var basePosition = new Vector2(anchor.x * horizontalSpan, anchor.y * verticalSpan);
            var jitter = new Vector2(
                GetGuestbookNoteVariation(seed, 0.37f, -18f, 18f),
                GetGuestbookNoteVariation(seed, 1.13f, -14f, 14f));

            return new Vector2(
                Mathf.Clamp(basePosition.x + jitter.x, -horizontalSpan, horizontalSpan),
                Mathf.Clamp(basePosition.y + jitter.y, -verticalSpan, verticalSpan));
        }

        private float GetGuestbookFocusedNoteRotation(int slotIndex, GuestbookNoteData note, int noteIndex)
        {
            var seed = GetGuestbookNoteSeed(noteIndex, note);
            var baseRotation = GuestbookFocusedNoteRotations[slotIndex % GuestbookFocusedNoteRotations.Length];
            var rotationJitter = GetGuestbookNoteVariation(seed, 0.79f, -0.9f, 0.9f);
            return Mathf.Clamp(baseRotation + rotationJitter, -5f, 5f);
        }

        private float GetGuestbookFocusedNoteScale(int slotIndex, GuestbookNoteData note, int noteIndex)
        {
            var seed = GetGuestbookNoteSeed(noteIndex, note);
            var baseScale = GuestbookFocusedNoteScales[slotIndex % GuestbookFocusedNoteScales.Length];
            var scaleJitter = GetGuestbookNoteVariation(seed, 1.61f, -0.015f, 0.015f);
            return Mathf.Clamp(baseScale + scaleJitter, 0.95f, 1.05f);
        }

        private static int GetGuestbookNoteSeed(int noteIndex, GuestbookNoteData note)
        {
            unchecked
            {
                var seed = noteIndex * 397;
                seed = (seed * 397) ^ (note?.authorName?.GetHashCode() ?? 0);
                seed = (seed * 397) ^ (note?.message?.GetHashCode() ?? 0);
                return seed;
            }
        }

        private static float GetGuestbookNoteVariation(int seed, float salt, float min, float max)
        {
            var value = Mathf.Sin(seed * 0.0173f + salt * 12.9898f) * 43758.5453f;
            value -= Mathf.Floor(value);
            return Mathf.Lerp(min, max, value);
        }

        private void RefreshGuestbookPagination()
        {
            var totalPages = isGuestbookPublic ? GetGuestbookTotalPages() : 1;
            guestbookCurrentPageIndex = Mathf.Clamp(guestbookCurrentPageIndex, 0, totalPages - 1);

            if (guestbookPreviousPageButton != null)
            {
                guestbookPreviousPageButton.interactable = isGuestbookPublic && guestbookCurrentPageIndex > 0;
                StyleGuestbookActionButton(
                    guestbookPreviousPageButton,
                    new Color32(255, 255, 255, 235),
                    new Color32(241, 235, 230, 255),
                    new Color32(232, 225, 220, 255),
                    16,
                    new Color32(78, 59, 51, 255));
            }

            if (guestbookNextPageButton != null)
            {
                guestbookNextPageButton.interactable = isGuestbookPublic && guestbookCurrentPageIndex < totalPages - 1;
                StyleGuestbookActionButton(
                    guestbookNextPageButton,
                    new Color32(255, 255, 255, 235),
                    new Color32(241, 235, 230, 255),
                    new Color32(232, 225, 220, 255),
                    16,
                    new Color32(78, 59, 51, 255));
            }

            var windowStart = Mathf.Clamp(
                guestbookCurrentPageIndex - 1,
                0,
                Mathf.Max(0, totalPages - GuestbookMaxPageButtons));

            for (var i = 0; i < guestbookPageButtons.Length; i++)
            {
                if (guestbookPageButtons[i] == null)
                {
                    continue;
                }

                var pageIndex = windowStart + i;
                var isVisible = pageIndex < totalPages;
                guestbookPageButtons[i].gameObject.SetActive(isVisible);
                guestbookPageButtonPageIndices[i] = isVisible ? pageIndex : -1;

                if (!isVisible)
                {
                    continue;
                }

                var isCurrentPage = pageIndex == guestbookCurrentPageIndex;
                var pageLabel = guestbookPageButtons[i].GetComponentInChildren<Text>();
                if (pageLabel != null)
                {
                    pageLabel.text = (pageIndex + 1).ToString();
                }

                guestbookPageButtons[i].interactable = isGuestbookPublic && !isCurrentPage;
                StyleGuestbookActionButton(
                    guestbookPageButtons[i],
                    isCurrentPage ? new Color32(141, 110, 99, 255) : new Color32(255, 255, 255, 235),
                    isCurrentPage ? new Color32(123, 95, 85, 255) : new Color32(241, 235, 230, 255),
                    isCurrentPage ? new Color32(141, 110, 99, 255) : new Color32(232, 225, 220, 255),
                    16,
                    isCurrentPage ? Color.white : new Color32(78, 59, 51, 255),
                    isCurrentPage);
            }

            if (guestbookPaginationRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(guestbookPaginationRect);
            }
        }

        private int GetGuestbookTotalPages()
        {
            return Mathf.Max(1, Mathf.CeilToInt(guestbookNotes.Count / Mathf.Max(1f, guestbookCardsPerPage)));
        }

        private void ChangeGuestbookPage(int direction)
        {
            if (!isGuestbookPublic)
            {
                return;
            }

            GoToGuestbookPage(guestbookCurrentPageIndex + direction);
        }

        private void GoToGuestbookPageFromSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= guestbookPageButtonPageIndices.Length)
            {
                return;
            }

            GoToGuestbookPage(guestbookPageButtonPageIndices[slotIndex]);
        }

        private void GoToGuestbookPage(int pageIndex)
        {
            var clampedPageIndex = Mathf.Clamp(pageIndex, 0, GetGuestbookTotalPages() - 1);
            if (clampedPageIndex == guestbookCurrentPageIndex)
            {
                return;
            }

            guestbookCurrentPageIndex = clampedPageIndex;
            RefreshGuestbookPanel(true);
        }

        private IEnumerator AnimateGuestbookPageTransition()
        {
            yield return FadeCanvasGroup(guestbookGridCanvasGroup, guestbookGridCanvasGroup.alpha, 0f, GuestbookPageFadeDuration * 0.5f);
            ApplyGuestbookPageContent();
            yield return FadeCanvasGroup(guestbookGridCanvasGroup, guestbookGridCanvasGroup.alpha, 1f, GuestbookPageFadeDuration * 0.5f);
            guestbookPageFadeCoroutine = null;
        }

        private static IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float from, float to, float duration)
        {
            if (canvasGroup == null)
            {
                yield break;
            }

            if (duration <= 0f)
            {
                canvasGroup.alpha = to;
                yield break;
            }

            var elapsed = 0f;
            canvasGroup.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }

            canvasGroup.alpha = to;
        }

        private void UpdateGuestbookCharCount(string value)
        {
            if (guestbookCharCountText != null)
            {
                guestbookCharCountText.text = $"{(value ?? string.Empty).Length}/30";
            }
        }

        private void SubmitGuestbookMessage()
        {
            if (guestbookInputField == null)
            {
                return;
            }

            var trimmed = (guestbookInputField.text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                if (guestbookBoardStatusText != null)
                {
                    guestbookBoardStatusText.text = "Write a short note before pinning it.";
                }

                return;
            }

            if (trimmed.Length > 30)
            {
                trimmed = trimmed.Substring(0, 30);
            }

            guestbookNotes.Insert(0, new GuestbookNoteData
            {
                authorName = guestbookOwnerMode ? "Host" : "Visitor",
                message = trimmed,
                timestampLabel = "Just now",
                color = GetGuestbookNoteColor(0)
            });

            if (guestbookNotes.Count > GuestbookMaxPinnedNotes)
            {
                guestbookNotes.RemoveAt(guestbookNotes.Count - 1);
            }

            guestbookCurrentPageIndex = 0;
            guestbookInputField.text = string.Empty;

            if (guestbookBoardStatusText != null)
            {
                guestbookBoardStatusText.text = "A new note was pinned to the board.";
            }

            RefreshGuestbookPanel();
        }

        private void ReplyToGuestbookNote(int slotIndex)
        {
            var noteIndex = guestbookCurrentPageIndex * guestbookCardsPerPage + slotIndex;
            if (!guestbookOwnerMode || noteIndex < 0 || noteIndex >= guestbookNotes.Count)
            {
                return;
            }

            if (guestbookBoardStatusText != null)
            {
                guestbookBoardStatusText.text = $"Reply draft prepared for {guestbookNotes[noteIndex].authorName}.";
            }
        }

        private void ToggleGuestbookVisibility()
        {
            if (!guestbookOwnerMode)
            {
                return;
            }

            isGuestbookPublic = !isGuestbookPublic;
            guestbookCurrentPageIndex = 0;

            if (guestbookBoardStatusText != null)
            {
                guestbookBoardStatusText.text = isGuestbookPublic
                    ? "Board visibility changed to public."
                    : "Board visibility changed to private.";
            }

            RefreshGuestbookPanel();
        }

        private Color GetGuestbookNoteColor(int noteIndex)
        {
            return guestbookNoteColors[Mathf.Abs(noteIndex) % guestbookNoteColors.Length];
        }

        private static void StyleGuestbookBoardSurface(GameObject panel)
        {
            var image = panel.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            ApplyRoundedSprite(image);
            image.color = new Color32(245, 241, 232, 255);
            image.raycastTarget = true;
            AddSoftShadow(panel, new Color(0f, 0f, 0f, 0.08f), new Vector2(0f, -10f));
        }

        private static void StyleGuestbookInterfacePanel(Image image)
        {
            if (image == null)
            {
                return;
            }

            ApplyRoundedSprite(image);
            image.color = new Color32(255, 251, 246, 232);
            image.raycastTarget = true;
            AddSoftShadow(image.gameObject, new Color(0f, 0f, 0f, 0.07f), new Vector2(0f, -6f));
        }

        private static void StyleGuestbookCard(GameObject noteCard)
        {
            var image = noteCard.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            ApplyRoundedSprite(image);
            image.color = new Color32(255, 249, 196, 255);
            image.raycastTarget = true;
            AddSoftShadow(noteCard, new Color(0f, 0f, 0f, 0.08f), new Vector2(6f, -8f));
            EnsureGuestbookTapeStrip(noteCard.transform, "TapeLeft", new Vector2(0.31f, 1f), new Vector2(68f, 20f), -7f);
            EnsureGuestbookTapeStrip(noteCard.transform, "TapeRight", new Vector2(0.69f, 1f), new Vector2(68f, 20f), 6f);
        }

        private static void EnsureGuestbookTapeStrip(Transform parent, string name, Vector2 anchor, Vector2 size, float rotationZ)
        {
            if (parent == null)
            {
                return;
            }

            var tapeTransform = parent.Find(name);
            Image tapeImage;
            RectTransform tapeRect;
            if (tapeTransform == null)
            {
                var tapeObject = new GameObject(name);
                tapeObject.transform.SetParent(parent, false);
                tapeRect = tapeObject.AddComponent<RectTransform>();
                tapeImage = tapeObject.AddComponent<Image>();
            }
            else
            {
                tapeRect = tapeTransform as RectTransform;
                tapeImage = tapeTransform.GetComponent<Image>();
                if (tapeImage == null)
                {
                    tapeImage = tapeTransform.gameObject.AddComponent<Image>();
                }
            }

            ApplyRoundedSprite(tapeImage);
            tapeImage.color = new Color32(255, 255, 255, 102);
            tapeImage.raycastTarget = false;

            tapeRect.anchorMin = anchor;
            tapeRect.anchorMax = anchor;
            tapeRect.pivot = new Vector2(0.5f, 0.5f);
            tapeRect.anchoredPosition = new Vector2(0f, -12f);
            tapeRect.sizeDelta = size;
            tapeRect.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
            tapeRect.localScale = Vector3.one;
            tapeRect.SetAsFirstSibling();
        }

        private static void StyleGuestbookActionButton(Button button, Color background, Color highlightedBackground, Color disabledBackground, int fontSize, Color textColor, bool isBold = false)
        {
            RuntimeHudStyle.StyleButton(button, background, highlightedBackground, disabledBackground, fontSize, textColor, isBold);
        }

        private static RectTransform CreateWorldContainer(Transform parent, string name, Vector2 size, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition)
        {
            var container = new GameObject(name);
            container.transform.SetParent(parent, false);

            var rect = container.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static void ConfigureHorizontalLayout(RectTransform rectTransform, TextAnchor alignment, float spacing)
        {
            if (rectTransform == null)
            {
                return;
            }

            var layoutGroup = rectTransform.GetComponent<HorizontalLayoutGroup>();
            if (layoutGroup == null)
            {
                layoutGroup = rectTransform.gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            layoutGroup.childAlignment = alignment;
            layoutGroup.spacing = spacing;
            layoutGroup.padding = new RectOffset();
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;
            layoutGroup.childScaleWidth = false;
            layoutGroup.childScaleHeight = false;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;
        }

        private static void ConfigureContentSizeFitter(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return;
            }

            var fitter = rectTransform.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = rectTransform.gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static void ConfigureFixedLayoutElement(RectTransform rectTransform, float width, float height, float minWidth = -1f)
        {
            if (rectTransform == null)
            {
                return;
            }

            var layoutElement = rectTransform.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = rectTransform.gameObject.AddComponent<LayoutElement>();
            }

            if (width >= 0f)
            {
                layoutElement.preferredWidth = width;
                layoutElement.minWidth = minWidth >= 0f ? minWidth : width;
            }
            else if (minWidth >= 0f)
            {
                layoutElement.minWidth = minWidth;
            }

            if (height >= 0f)
            {
                layoutElement.preferredHeight = height;
                layoutElement.minHeight = height;
            }
        }

        private static void ApplyRoundedSprite(Image image)
        {
            RuntimeHudStyle.ApplyRoundedSprite(image);
        }

        private static Sprite GetRoundedUiSprite()
        {
            if (roundedUiSprite == null)
            {
                const int textureSize = 32;
                const float cornerRadius = 11f;
                roundedUiTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
                {
                    name = "GeneratedUiSprite",
                    hideFlags = HideFlags.HideAndDontSave
                };

                roundedUiTexture.wrapMode = TextureWrapMode.Clamp;
                roundedUiTexture.filterMode = FilterMode.Bilinear;
                for (var y = 0; y < textureSize; y++)
                {
                    for (var x = 0; x < textureSize; x++)
                    {
                        var nearestX = Mathf.Clamp(x, cornerRadius, textureSize - 1f - cornerRadius);
                        var nearestY = Mathf.Clamp(y, cornerRadius, textureSize - 1f - cornerRadius);
                        var distance = Vector2.Distance(new Vector2(x, y), new Vector2(nearestX, nearestY));
                        var alpha = Mathf.Clamp01(cornerRadius + 0.5f - distance);
                        roundedUiTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }

                roundedUiTexture.Apply();

                roundedUiSprite = Sprite.Create(
                    roundedUiTexture,
                    new Rect(0f, 0f, textureSize, textureSize),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect,
                    new Vector4(12f, 12f, 12f, 12f));
                roundedUiSprite.name = "GeneratedUiSprite";
                roundedUiSprite.hideFlags = HideFlags.HideAndDontSave;
            }

            return roundedUiSprite;
        }

        private static Sprite GetSearchIconRingSprite()
        {
            if (searchIconRingSprite == null)
            {
                const int textureSize = 64;
                const float outerRadius = 26f;
                const float innerRadius = 18f;
                var center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
                searchIconRingTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
                {
                    name = "GeneratedSearchIconRing",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };

                for (var y = 0; y < textureSize; y++)
                {
                    for (var x = 0; x < textureSize; x++)
                    {
                        var distance = Vector2.Distance(new Vector2(x, y), center);
                        var outerAlpha = Mathf.Clamp01(outerRadius + 1f - distance);
                        var innerAlpha = Mathf.Clamp01(distance - innerRadius + 1f);
                        var alpha = Mathf.Min(outerAlpha, innerAlpha);
                        searchIconRingTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }

                searchIconRingTexture.Apply();
                searchIconRingSprite = Sprite.Create(
                    searchIconRingTexture,
                    new Rect(0f, 0f, textureSize, textureSize),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect);
                searchIconRingSprite.name = "GeneratedSearchIconRing";
                searchIconRingSprite.hideFlags = HideFlags.HideAndDontSave;
            }

            return searchIconRingSprite;
        }

        private static void AddSoftShadow(GameObject target, Color color, Vector2 distance)
        {
            RuntimeHudStyle.AddSoftShadow(target, color, distance);
        }

        private static void AddSoftOutline(GameObject target, Color color, Vector2 distance)
        {
            RuntimeHudStyle.AddSoftOutline(target, color, distance);
        }

        private static void StyleHudPanel(Image image, Color background)
        {
            RuntimeHudStyle.StylePanel(image, background);
        }

        private static void StyleBookDetailPanel(Image image)
        {
            RuntimeHudStyle.StyleBookDetailPanel(image);
        }

        private static void StyleBookshelfIndexPanel(Image image)
        {
            RuntimeHudStyle.StyleBookshelfIndexPanel(image);
        }

        private static void PrepareBookshelfNavigationButton(Button button)
        {
            RuntimeHudStyle.PrepareBookshelfNavigationButton(button);
        }

        private static void StyleBookshelfNavigationButton(Button button, bool isInteractable)
        {
            RuntimeHudStyle.StyleBookshelfNavigationButton(button, isInteractable);
        }

        private static void StyleHudButton(Button button, Color background, Color highlightedBackground, Color disabledBackground, int fontSize, Color textColor, bool isBold = false)
        {
            StyleGuestbookActionButton(button, background, highlightedBackground, disabledBackground, fontSize, textColor, isBold);
        }

        private static void StyleDeleteActionButton(Button button, bool isEnabled = true, bool isConfirming = false, int fontSize = 14)
        {
            StyleHudButton(
                button,
                isEnabled
                    ? isConfirming ? DeleteButtonConfirmBackground : DeleteButtonBackground
                    : DeleteButtonDisabledBackground,
                isEnabled
                    ? isConfirming ? DeleteButtonConfirmHighlighted : DeleteButtonHighlighted
                    : DeleteButtonDisabledBackground,
                DeleteButtonDisabledBackground,
                fontSize,
                isEnabled ? Color.white : DeleteButtonDisabledText,
                true);
            if (button != null)
            {
                AddSoftOutline(button.gameObject, isEnabled ? DeleteButtonBorder : new Color32(255, 255, 255, 90), new Vector2(1f, -1f));
            }
        }

        private static void SetButtonLabel(Button button, string label)
        {
            RuntimeHudStyle.SetButtonLabel(button, label);
        }

        private static Text GetButtonLabelText(Button button)
        {
            return RuntimeHudStyle.GetButtonLabelText(button);
        }

        private void CloseOverlayPanels(GameObject except = null)
        {
            CancelBookAddPlacementSelection(false, false);

            if (bookPlacementEditorPanel != null && bookPlacementEditorPanel != except)
            {
                CancelBookPlacementEdit(false);
            }

            if (bookDetailPanel != null && bookDetailPanel != except)
            {
                bookDetailPanel.SetActive(false);
            }

            if (bookSearchPanel != null && bookSearchPanel != except)
            {
                isBookSearchPanelOpen = false;
                bookSearchPanel.SetActive(false);
                bookSearchResults.Clear();
            }

            if (bookReportWritePanel != null && bookReportWritePanel != except)
            {
                bookReportWritePanel.SetActive(false);
                isBookReportWritePanelOpen = false;
            }

            if (bookReportReadPanel != null && bookReportReadPanel != except)
            {
                bookReportReadPanel.SetActive(false);
                isBookReportReadPanelOpen = false;
            }

            if (bookReviewWritePanel != null && bookReviewWritePanel != except)
            {
                bookReviewWritePanel.SetActive(false);
                isBookReviewWritePanelOpen = false;
                isBookReviewSaving = false;
                SetBookReviewStatus(string.Empty);
            }

            if (except != bookReportWritePanel && except != bookReportReadPanel)
            {
                activeReportBook = null;
            }

            if (except != bookReviewWritePanel)
            {
                activeReviewBook = null;
            }

            if (bookEditPanel != null && bookEditPanel != except)
            {
                bookEditPanel.SetActive(false);
                if (isBookEditPanelOpen)
                {
                    ActiveDetailBook = null;
                    pendingDeleteBook = null;
                    isBookEditPanelOpen = false;
                }
            }

            if (sectionEditPanel != null && sectionEditPanel != except)
            {
                sectionEditPanel.SetActive(false);
                activeEditSection = null;
            }

            if (guestbookPanel != null && guestbookPanel != except)
            {
                guestbookPanel.SetActive(false);
            }

            if (bookAddPanel != null && bookAddPanel != except)
            {
                CloseBookAddPanel(false);
            }

            if (bookAddConfirmPanel != null && bookAddConfirmPanel != except)
            {
                isBookAddConfirmPanelOpen = false;
                pendingBookAddCatalogIndex = -1;
                pendingBookAddCatalogItem = null;
                bookAddConfirmPanel.SetActive(false);
            }

            if (bookshelfManagePanel != null && bookshelfManagePanel != except)
            {
                ResetBookshelfManageDragState();
                isBookshelfManagePanelOpen = false;
                bookshelfManagePanel.SetActive(false);
            }

            if (bookshelfUnsavedConfirmPanel != null && bookshelfUnsavedConfirmPanel != except)
            {
                isBookshelfUnsavedConfirmPanelOpen = false;
                pendingBookshelfNavigationAfterSave = null;
                bookshelfUnsavedConfirmPanel.SetActive(false);
            }

            if (deleteResultDialogPanel != null && deleteResultDialogPanel != except)
            {
                CloseDeleteResultDialog(false);
            }
        }

        private static GameObject CreateWorldPanel(Transform parent, string name, Vector2 size)
        {
            var panelObject = new GameObject(name);
            panelObject.transform.SetParent(parent, false);
            var image = panelObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.92f);

            var rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            return panelObject;
        }

        private static Text CreateWorldLabel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, int fontSize, Color color)
        {
            return CreateWorldLabel(parent, name, anchoredPosition, size, fontSize, color, TextAnchor.UpperLeft);
        }

        private static Text CreateWorldLabel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, int fontSize, Color color, TextAnchor alignment)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            var text = textObject.AddComponent<Text>();
            text.font = RuntimeUiFont.Resolve();
            text.color = color;
            text.alignment = alignment;
            text.fontSize = fontSize;
            text.text = string.Empty;
            text.supportRichText = false;

            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return text;
        }

        private static InputField CreateWorldInputField(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, string placeholder)
        {
            var inputObject = new GameObject(name);
            inputObject.transform.SetParent(parent, false);

            var image = inputObject.AddComponent<Image>();
            ApplyRoundedSprite(image);
            image.color = Color.white;

            var inputField = inputObject.AddComponent<InputField>();
            inputField.targetGraphic = image;
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.caretWidth = 2;
            var rect = inputObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            AddSoftShadow(inputObject, new Color(0f, 0f, 0f, 0.06f), new Vector2(0f, -3f));

            var colors = inputField.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.99f, 0.99f, 0.99f, 1f);
            colors.pressedColor = new Color(0.98f, 0.98f, 0.98f, 1f);
            colors.selectedColor = new Color(0.99f, 0.99f, 0.99f, 1f);
            colors.disabledColor = new Color(0.95f, 0.94f, 0.92f, 1f);
            colors.fadeDuration = 0.1f;
            inputField.colors = colors;

            var placeholderText = CreateWorldLabel(
                inputObject.transform,
                "Placeholder",
                new Vector2(18f, -20f),
                new Vector2(size.x - 132f, 32f),
                19,
                new Color32(167, 154, 146, 255),
                TextAnchor.MiddleLeft);
            placeholderText.text = placeholder;
            var text = CreateWorldLabel(
                inputObject.transform,
                "Text",
                new Vector2(18f, -20f),
                new Vector2(size.x - 132f, 32f),
                19,
                new Color32(78, 59, 51, 255),
                TextAnchor.MiddleLeft);

            inputField.textComponent = text;
            inputField.placeholder = placeholderText;
            return inputField;
        }

        private static InputField CreateHudInputField(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, string placeholder)
        {
            var inputObject = new GameObject(name);
            inputObject.transform.SetParent(parent, false);

            var image = inputObject.AddComponent<Image>();
            ApplyRoundedSprite(image);
            image.color = new Color32(255, 255, 255, 242);
            inputObject.AddComponent<RectMask2D>();

            var inputField = inputObject.AddComponent<InputField>();
            inputField.targetGraphic = image;
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.caretWidth = 2;
            inputField.selectionColor = new Color32(197, 128, 67, 96);

            var rect = inputObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            AddSoftOutline(inputObject, new Color32(205, 186, 168, 126), new Vector2(1f, -1f));

            var colors = inputField.colors;
            colors.normalColor = new Color32(255, 255, 255, 242);
            colors.highlightedColor = new Color32(255, 248, 236, 255);
            colors.pressedColor = new Color32(255, 248, 236, 255);
            colors.selectedColor = new Color32(255, 248, 236, 255);
            colors.disabledColor = new Color32(238, 231, 221, 142);
            colors.fadeDuration = 0.1f;
            inputField.colors = colors;

            var placeholderText = CreateInputLabel(inputObject.transform, "Placeholder", placeholder, size, new Color32(151, 136, 124, 255));
            var text = CreateInputLabel(inputObject.transform, "Text", string.Empty, size, new Color32(63, 47, 38, 255));
            inputField.textComponent = text;
            inputField.placeholder = placeholderText;
            ApplyHudInputTextPadding(inputField);
            return inputField;
        }

        private static GameObject CreateModalBackdrop(Transform parent, string name, Color color)
        {
            return RuntimeHudFactory.CreateModalBackdrop(parent, name, color);
        }

        private static Text CreateInputLabel(Transform parent, string name, string value, Vector2 parentSize, Color color)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            var text = textObject.AddComponent<Text>();
            text.font = RuntimeUiFont.Resolve();
            text.text = value;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.fontSize = 15;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;

            ApplyHudInputTextRect(text, false);
            return text;
        }

        private static void ApplyHudInputTextPadding(InputField inputField)
        {
            if (inputField == null)
            {
                return;
            }

            var isMultiLine = inputField.lineType != InputField.LineType.SingleLine;
            ApplyHudInputTextRect(inputField.textComponent, isMultiLine);
            if (inputField.placeholder is Text placeholderText)
            {
                ApplyHudInputTextRect(placeholderText, isMultiLine);
            }
        }

        private static void ApplyHudInputTextRect(Text text, bool isMultiLine)
        {
            if (text == null)
            {
                return;
            }

            text.alignment = isMultiLine ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
            var parentRect = text.transform.parent != null ? text.transform.parent.GetComponent<RectTransform>() : null;
            var parentSize = parentRect != null ? parentRect.sizeDelta : Vector2.zero;
            var width = Mathf.Max(24f, parentSize.x - HudInputTextLeftPadding - HudInputTextRightPadding);
            var height = Mathf.Max(18f, parentSize.y - HudInputTextVerticalPadding * 2f);
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(HudInputTextLeftPadding, -HudInputTextVerticalPadding);
            rect.sizeDelta = new Vector2(width, height);
        }

        private sealed class BookshelfManageDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            private BookshelfExperienceManager owner;
            private Image[] lineImages;
            private int rowIndex = -1;

            public void Configure(BookshelfExperienceManager manager, int index)
            {
                owner = manager;
                rowIndex = index;
            }

            public void SetLineColor(Color color)
            {
                lineImages ??= GetComponentsInChildren<Image>(true);
                for (var i = 0; i < lineImages.Length; i++)
                {
                    if (lineImages[i] != null && lineImages[i].gameObject != gameObject)
                    {
                        lineImages[i].color = color;
                    }
                }
            }

            public void OnBeginDrag(PointerEventData eventData)
            {
                owner?.BeginBookshelfManageRowDrag(rowIndex, eventData);
            }

            public void OnDrag(PointerEventData eventData)
            {
                owner?.UpdateBookshelfManageRowDrag(rowIndex, eventData);
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                owner?.EndBookshelfManageRowDrag(rowIndex, eventData);
            }
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
        {
            return RuntimeHudFactory.CreateButton(parent, name, label, anchoredPosition, onClick);
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick, Vector2 size, Vector2 anchor)
        {
            return RuntimeHudFactory.CreateButton(parent, name, label, anchoredPosition, onClick, size, anchor);
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
        {
            return RuntimeHudFactory.CreatePanel(parent, name, anchoredPosition, size, anchor);
        }

        private static RectTransform CreateHudContainer(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
        {
            return RuntimeHudFactory.CreateContainer(parent, name, anchoredPosition, size, anchor);
        }

        private static GameObject CreateHudScrollViewport(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
        {
            return RuntimeHudFactory.CreateScrollViewport(parent, name, anchoredPosition, size, anchor);
        }

        private static RectTransform CreateHudScrollContent(Transform parent, string name, Vector2 size)
        {
            return RuntimeHudFactory.CreateScrollContent(parent, name, size);
        }

        private static Scrollbar CreateHudVerticalScrollbar(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
        {
            return RuntimeHudFactory.CreateVerticalScrollbar(parent, name, anchoredPosition, size, anchor);
        }

        private static void StyleHudScrollbar(Scrollbar scrollbar, Color32 handleColor, Color32 trackColor)
        {
            RuntimeHudStyle.StyleScrollbar(scrollbar, handleColor, trackColor);
        }

        private static float GetHudScrollViewportHeight(ScrollRect scrollRect, float fallbackHeight)
        {
            return RuntimeHudLayout.GetScrollViewportHeight(scrollRect, fallbackHeight);
        }

        private static void ApplyHudScrollState(
            ScrollRect scrollRect,
            Scrollbar scrollbar,
            RectTransform content,
            bool canScroll,
            bool resetToTop,
            float scrollSensitivity = 24f)
        {
            RuntimeHudLayout.ApplyScrollState(scrollRect, scrollbar, content, canScroll, resetToTop, scrollSensitivity);
        }

        private static void ClampHudScrollContentToViewport(ScrollRect scrollRect, RectTransform content)
        {
            RuntimeHudLayout.ClampScrollContentToViewport(scrollRect, content);
        }

        private static Image CreateHudImage(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchor, Color color, bool isVisible = true)
        {
            return RuntimeHudFactory.CreateImage(parent, name, anchoredPosition, size, anchor, color, isVisible);
        }

        private static Text CreateLabel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, TextAnchor alignment)
        {
            return RuntimeHudFactory.CreateLabel(parent, name, anchoredPosition, size, alignment);
        }

        private bool TryHitGuestbookBoard()
        {
            var sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                return false;
            }

            var ray = sceneCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            return TryHitClosest<GuestbookBoardHitArea>(ray);
        }

        private bool TryHitNotebook()
        {
            var sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                return false;
            }

            var ray = sceneCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            return TryHitClosest<NotebookHitArea>(ray);
        }

        private bool TryHitBookshelf()
        {
            var sceneCamera = Camera.main;
            if (sceneCamera == null || Mouse.current == null)
            {
                return false;
            }

            return TryHitBookshelf(Mouse.current.position.ReadValue());
        }

        private bool TryHitBookshelf(Vector2 pointer)
        {
            var sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                return false;
            }

            var ray = sceneCamera.ScreenPointToRay(pointer);
            var hits = Physics.RaycastAll(ray, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            if (hits.Length == 0)
            {
                return false;
            }

            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (collider == null || ShouldIgnoreSceneHitCollider(collider))
                {
                    continue;
                }

                if (collider.GetComponentInParent<BookEntity>() != null ||
                    collider.GetComponent<BookshelfFocusHitArea>() != null)
                {
                    return true;
                }

                if (collider.GetComponent<NotebookHitArea>() != null ||
                    collider.GetComponent<GuestbookBoardHitArea>() != null)
                {
                    return false;
                }
            }

            return false;
        }

        private void ConfigureNotebookScreen()
        {
            var roomNotebookScreenController = FindRoomNotebookScreenController();
            if (roomNotebookScreenController != null)
            {
                notebookScreenController = roomNotebookScreenController;
            }
            else if (notebookUiSceneLoader != null && !notebookUiSceneLoader.OwnsController(notebookScreenController))
            {
                notebookScreenController = notebookUiSceneLoader.RefreshController();
            }

            if (notebookScreenController == null && notebookUiSceneLoader == null)
            {
                var controllers = FindObjectsByType<NotebookScreenController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                notebookScreenController = controllers != null && controllers.Length > 0 ? controllers[0] : null;
            }

            if (notebookScreenController == null || Camera.main == null)
            {
                return;
            }

            var screenTransform = notebookScreenController.transform.Find("Screen");
            notebookScreenController.Configure(
                screenTransform,
                Camera.main,
                // onCloseDashboard — dashboard header 의 X (NotebookToolkitController.cs:1134) 콜백.
                // 사용자 의도: "X 한 번 = 노트북 focus 종료 = 메인 view 복귀". 이전 wiring 은
                // ReturnToScreenSaver 만 호출해 노트북 focus 안에 머물면서 UI 만 screen saver 로
                // 전환했는데, screen saver 의 ad 이미지를 다시 누르면 FocusNotebook → ActivateDashboard
                // 로 되돌아가 사용자 시각에서 "X → 대시보드로 다시 이동" 으로 보이는 회귀가 발생.
                // ReturnToScreenSaver 를 먼저 호출해 다음 진입 시 screen saver 부터 보이도록 reset
                // 한 뒤 CloseNotebookFocus 로 camera 를 메인 view 로 빼낸다.
                () =>
                {
                    notebookScreenController.ReturnToScreenSaver();
                    CloseNotebookFocus();
                },
                CloseNotebookFocus,
                FocusNotebook,
                searchNotebookBooksUseCase,
                loadNotebookMarketBooksUseCase,
                addNotebookBookToShelfUseCase,
                notebookRuntimeUseCase);
            AttachNotebookApiCallbacks();
            notebookUiSceneLoader?.MeasureCanvasActivationCost(notebookScreenController);
        }

        private NotebookScreenController FindRoomNotebookScreenController()
        {
            var controllers = FindObjectsByType<NotebookScreenController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (controllers == null || controllers.Length == 0)
            {
                return null;
            }

            for (var i = 0; i < controllers.Length; i++)
            {
                var controller = controllers[i];
                if (controller != null && (notebookUiSceneLoader == null || !notebookUiSceneLoader.OwnsController(controller)))
                {
                    return controller;
                }
            }

            return null;
        }

        private void AttachNotebookApiCallbacks()
        {
            if (subscribedNotebookScreenController != null && subscribedNotebookScreenController == notebookScreenController)
            {
                return;
            }

            DetachNotebookApiCallbacks();

            subscribedNotebookScreenController = notebookScreenController;
            if (subscribedNotebookScreenController != null)
            {
                subscribedNotebookScreenController.BookAddedToShelfThroughApi += HandleNotebookBookAddedToShelfThroughApi;
            }
        }

        private void DetachNotebookApiCallbacks()
        {
            if (subscribedNotebookScreenController == null)
            {
                return;
            }

            subscribedNotebookScreenController.BookAddedToShelfThroughApi -= HandleNotebookBookAddedToShelfThroughApi;
            subscribedNotebookScreenController = null;
        }

        private void HandleNotebookBookAddedToShelfThroughApi()
        {
            if (bookshelfApiSyncController == null || !bookshelfApiSyncController.IsServerSyncEnabled)
            {
                ShowBookshelfTransientStatus("책 추가 저장 완료", "책은 서버에 저장됐지만 이 화면에서는 책장 새로고침을 실행할 수 없습니다.", false);
                RefreshHud();
                return;
            }

            ShowBookshelfTransientStatus("책 추가 저장 완료", "책은 서버에 저장됐고 책장 새로고침을 시작합니다.", false);
            bookshelfApiSyncController.RefreshFromServer(HandleNotebookBookAddRefreshCompleted);
            RefreshHud();
        }

        private void HandleNotebookBookAddRefreshCompleted(bool success, string message)
        {
            if (success)
            {
                ShowBookshelfTransientStatus("책장 새로고침 완료", "추가된 책을 책장에 반영했습니다.", false);
                RefreshHud();
                return;
            }

            var detail = NormalizeMutationErrorMessage(message);
            ShowBookshelfTransientStatus(
                "책 추가 완료 · 새로고침 실패",
                "책은 서버에 저장됐지만 책장 내용을 다시 불러오지 못했습니다.",
                true);
            ShowDeleteResultDialog(
                "책 추가 완료 · 새로고침 실패",
                $"책 추가 요청은 성공했습니다. 다만 책장 렌더 조회가 실패해 화면은 이전 책장 상태를 유지합니다.\n{detail}",
                false,
                () => bookshelfApiSyncController?.RefreshFromServer(HandleNotebookBookAddRefreshCompleted),
                "새로고침");
            RefreshHud();
        }

        private int BeginNotebookFocusRequest()
        {
            if (!isNotebookFocusWaitingForScene)
            {
                notebookFocusRequestVersion++;
            }

            return notebookFocusRequestVersion;
        }

        private void CancelPendingNotebookFocusRequest()
        {
            if (!isNotebookFocusWaitingForScene)
            {
                return;
            }

            notebookFocusRequestVersion++;
            isNotebookFocusWaitingForScene = false;
        }

        private bool EnsureNotebookUiReadyForFocus(int focusRequestVersion)
        {
            ConfigureNotebookScreen();
            if (notebookScreenController != null)
            {
                BookshelfSceneInstaller.AlignNotebookAnchorToLoadedNotebook();
                return true;
            }

            if (notebookUiSceneLoader == null)
            {
                return true;
            }

            if (isNotebookFocusWaitingForScene)
            {
                return false;
            }

            isNotebookFocusWaitingForScene = true;
            notebookUiSceneLoader.EnsureLoaded(controller =>
            {
                isNotebookFocusWaitingForScene = false;
                if (focusRequestVersion != notebookFocusRequestVersion || !isActiveAndEnabled)
                {
                    return;
                }

                notebookScreenController = controller;
                ConfigureNotebookScreen();
                if (notebookScreenController == null)
                {
                    ShowNotebookUiLoadFailure();
                    return;
                }

                BookshelfSceneInstaller.AlignNotebookAnchorToLoadedNotebook();
                FocusNotebookImmediateWithLoadedUi();
            });
            return false;
        }

        private void ShowNotebookUiLoadFailure()
        {
            Debug.LogError("Notebook UI scene load completed without a NotebookScreenController.");
            if (bookshelfStatusPanel == null)
            {
                return;
            }

            bookshelfStatusPanel.SetActive(true);
            if (bookshelfStatusTitleText != null)
            {
                bookshelfStatusTitleText.text = "노트북 화면 오류";
            }

            if (bookshelfStatusMessageText != null)
            {
                bookshelfStatusMessageText.text = "노트북 UI를 불러오지 못했습니다. 잠시 후 다시 시도해주세요.";
            }

            if (bookshelfStatusAddButton != null)
            {
                bookshelfStatusAddButton.gameObject.SetActive(false);
            }
        }

        public void ConfigureNotebookBookUseCases(
            SearchNotebookBooksUseCase searchUseCase,
            LoadNotebookMarketBooksUseCase loadMarketUseCase,
            AddNotebookBookToShelfUseCase addUseCase,
            NotebookRuntimeUseCase runtimeUseCase = null)
        {
            searchNotebookBooksUseCase = searchUseCase;
            loadNotebookMarketBooksUseCase = loadMarketUseCase;
            addNotebookBookToShelfUseCase = addUseCase;
            notebookRuntimeUseCase = runtimeUseCase;
            notebookScreenController?.ConfigureNotebookBookUseCases(
                searchNotebookBooksUseCase,
                loadNotebookMarketBooksUseCase,
                addNotebookBookToShelfUseCase,
                notebookRuntimeUseCase);
        }

        private void ShowNotebookScreen()
        {
            if (notebookScreenController == null)
            {
                ConfigureNotebookScreen();
            }

            if (notebookScreenController != null)
            {
                notebookScreenController.Show();
            }
        }

        private void HideNotebookScreen()
        {
            if (notebookScreenController != null)
            {
                notebookScreenController.Hide();
            }
        }

        private static bool TryHitClosest<T>(Ray ray) where T : Component
        {
            var hits = Physics.RaycastAll(ray, 100f);
            if (hits.Length == 0)
            {
                return false;
            }

            var closestDistance = float.MaxValue;
            Collider closestCollider = null;
            for (var i = 0; i < hits.Length; i++)
            {
                var candidate = hits[i].collider;
                if (candidate == null || ShouldIgnoreSceneHitCollider(candidate))
                {
                    continue;
                }

                if (hits[i].distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hits[i].distance;
                closestCollider = candidate;
            }

            return closestCollider != null && closestCollider.GetComponent<T>() != null;
        }

        private static bool ShouldIgnoreSceneHitCollider(Collider collider)
        {
            if (collider == null)
            {
                return true;
            }

            var dashboard = collider.GetComponentInParent<DashboardScreenController>();
            return dashboard != null && !dashboard.InteractionEnabled;
        }

        private void HandleFocusedDashboardPanelFallbackInput()
        {
            if (Mouse.current == null || externalDashboardPanel == null)
            {
                focusedDashboardPressedElement = null;
                return;
            }

            var leftButton = Mouse.current.leftButton;
            if (!leftButton.wasPressedThisFrame && !leftButton.wasReleasedThisFrame)
            {
                return;
            }

            var pointerScreen = Mouse.current.position.ReadValue();
            if (!ShouldUseFocusedDashboardFallback(pointerScreen))
            {
                focusedDashboardPressedElement = null;
                return;
            }

            if (leftButton.wasPressedThisFrame)
            {
                focusedDashboardPressedElement = PickFocusedDashboardElement(pointerScreen);
            }

            if (!leftButton.wasReleasedThisFrame)
            {
                return;
            }

            var releasedElement = PickFocusedDashboardElement(pointerScreen);
            var pressedButton = FindUiToolkitButtonAncestor(focusedDashboardPressedElement);
            var releasedButton = FindUiToolkitButtonAncestor(releasedElement);
            focusedDashboardPressedElement = null;

            if (pressedButton == null || pressedButton != releasedButton)
            {
                return;
            }

            InvokeUiToolkitButtonClick(pressedButton);
        }

        private bool ShouldUseFocusedDashboardFallback(Vector2 pointerScreen)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return true;
            }

            var eventData = new PointerEventData(eventSystem)
            {
                position = pointerScreen
            };
            var results = new List<RaycastResult>();
            eventSystem.RaycastAll(eventData, results);
            for (var i = 0; i < results.Count; i++)
            {
                var module = results[i].module;
                if (module == null)
                {
                    continue;
                }

                var moduleTypeName = module.GetType().FullName;
                if (moduleTypeName == "UnityEngine.UIElements.PanelRaycaster" ||
                    moduleTypeName == "UnityEngine.UIElements.WorldDocumentRaycaster")
                {
                    return false;
                }
            }

            return true;
        }

        private UnityEngine.UIElements.VisualElement PickFocusedDashboardElement(Vector2 pointerScreen)
        {
            var document = externalDashboardPanel != null
                ? externalDashboardPanel.GetComponentInChildren<UIDocument>(true)
                : null;
            if (document == null || !document.isActiveAndEnabled)
            {
                return null;
            }

            var dashboard = document.GetComponentInParent<DashboardScreenController>();
            if (dashboard != null && !dashboard.InteractionEnabled)
            {
                return null;
            }

            var root = document.rootVisualElement;
            if (root == null || root.panel == null)
            {
                return null;
            }

            var panelPosition = RuntimePanelUtils.ScreenToPanel(root.panel, pointerScreen);
            return root.panel.Pick(panelPosition);
        }

        private static UnityEngine.UIElements.Button FindUiToolkitButtonAncestor(UnityEngine.UIElements.VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
            {
                if (current is UnityEngine.UIElements.Button button &&
                    button.enabledInHierarchy &&
                    button.resolvedStyle.display != UnityEngine.UIElements.DisplayStyle.None &&
                    button.resolvedStyle.visibility != UnityEngine.UIElements.Visibility.Hidden)
                {
                    return button;
                }
            }

            return null;
        }

        private static void InvokeUiToolkitButtonClick(UnityEngine.UIElements.Button button)
        {
            if (button == null)
            {
                return;
            }

            try
            {
                using (var submitEvent = UnityEngine.UIElements.NavigationSubmitEvent.GetPooled())
                {
                    submitEvent.target = button;
                    button.SendEvent(submitEvent);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(
                    $"[Dashboard] UI Toolkit submit fallback failed for button '{button.name}': {ex.GetType().Name} {ex.Message}");
            }

        }

        private static bool IsPointerOverUi()
        {
            if (Mouse.current == null)
            {
                return false;
            }

            Vector2 pointerScreen = Mouse.current.position.ReadValue();
            if (EventSystem.current != null && IsPointerOverBlockingEventSystemUi(pointerScreen))
            {
                return true;
            }

            // Dashboard UIDocument hits are tracked by DashboardRaycastFilter so
            // this hot path does not scan every UIDocument in the scene.
            return DashboardRaycastFilter.IsPointerOverInteractiveDashboard(pointerScreen);
        }

        // origin/dev 의 의도된 동작 — RaycastAll 결과를 DashboardRaycastFilter.IsBlockingRaycastResult
        // 가 분류한다. 그 필터는 WorldSpaceRaycastPassthrough 마커가 붙은 WorldSpace 캔버스 hit
        // (guestbookPreviewPanel / guestbookPanel 등) 은 non-blocking 으로 처리해 메인룸 클릭이
        // 통과되도록 보장. 마커가 없는 WorldSpace 캔버스(활성 dashboard 등) 와 ScreenSpace UI 는
        // 정상 차단.
        private static bool IsPointerOverBlockingEventSystemUi(Vector2 pointerScreen)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            var eventData = new PointerEventData(eventSystem)
            {
                position = pointerScreen
            };
            var results = new List<RaycastResult>();
            eventSystem.RaycastAll(eventData, results);
            for (var i = 0; i < results.Count; i++)
            {
                if (!DashboardRaycastFilter.IsBlockingRaycastResult(results[i], pointerScreen))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }
    }
}
