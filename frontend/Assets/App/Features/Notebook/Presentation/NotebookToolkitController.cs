using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using App.Application.Notebook;
using App.Application.Payment;
using App.Domain.Payment;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace App.Features.Notebook.Presentation
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class NotebookToolkitController : MonoBehaviour
    {
        private sealed class UiToolkitScrollIndicator
        {
            public readonly struct Options
            {
                public Options(
                    string trackName,
                    string handleName,
                    float width,
                    float top,
                    float right,
                    float bottom,
                    float minHandleHeight,
                    Color normalColor,
                    Color activeColor,
                    Color trackColor = default,
                    float handleInset = 0f,
                    float trackRadius = 0f,
                    float handleRadius = 0f)
                {
                    TrackName = trackName;
                    HandleName = handleName;
                    Width = width;
                    Top = top;
                    Right = right;
                    Bottom = bottom;
                    MinHandleHeight = minHandleHeight;
                    NormalColor = normalColor;
                    ActiveColor = activeColor;
                    TrackColor = trackColor;
                    HandleInset = handleInset;
                    TrackRadius = trackRadius;
                    HandleRadius = handleRadius;
                }

                public string TrackName { get; }
                public string HandleName { get; }
                public float Width { get; }
                public float Top { get; }
                public float Right { get; }
                public float Bottom { get; }
                public float MinHandleHeight { get; }
                public Color NormalColor { get; }
                public Color ActiveColor { get; }
                public Color TrackColor { get; }
                public float HandleInset { get; }
                public float TrackRadius { get; }
                public float HandleRadius { get; }
            }

            private readonly ScrollView scrollView;
            private readonly VisualElement measuredContent;
            private readonly Options options;
            private readonly VisualElement track;
            private readonly VisualElement handle;
            private readonly VisualElement trackHost;

            private bool isDragging;
            private float dragStartY;
            private float dragStartOffsetY;

            public UiToolkitScrollIndicator(ScrollView scrollView, VisualElement measuredContent, Options options, VisualElement trackParent = null)
            {
                this.scrollView = scrollView;
                this.measuredContent = measuredContent;
                this.options = options;

                HideNativeScroller(scrollView);

                trackHost = trackParent ?? scrollView;
                track = new VisualElement { name = options.TrackName };
                track.style.position = Position.Absolute;
                track.style.top = options.Top;
                track.style.right = options.Right;
                track.style.bottom = options.Bottom;
                track.style.width = options.Width;
                track.style.backgroundColor = options.TrackColor;
                track.pickingMode = PickingMode.Position;
                if (options.TrackRadius > 0f)
                {
                    SetRadius(track, options.TrackRadius);
                }
                if (trackParent != null)
                {
                    trackParent.Add(track);
                }
                else
                {
                    scrollView.hierarchy.Add(track);
                }

                handle = new VisualElement { name = options.HandleName };
                handle.style.position = Position.Absolute;
                handle.style.left = options.HandleInset;
                handle.style.top = 0;
                handle.style.width = Mathf.Max(1f, options.Width - options.HandleInset * 2f);
                handle.style.height = options.MinHandleHeight;
                handle.style.backgroundColor = options.NormalColor;
                handle.pickingMode = PickingMode.Position;
                SetRadius(handle, options.HandleRadius > 0f ? options.HandleRadius : options.Width * 0.5f);
                track.Add(handle);

                RegisterCallbacks();
                Update();
            }

            public void ResetToTop()
            {
                if (scrollView == null)
                {
                    return;
                }

                scrollView.scrollOffset = Vector2.zero;
                Update();
                scrollView.schedule.Execute(() =>
                {
                    if (scrollView == null)
                    {
                        return;
                    }

                    scrollView.scrollOffset = Vector2.zero;
                    Update();
                }).StartingIn(1);
                scrollView.schedule.Execute(Update).StartingIn(32);
            }

            public void Update()
            {
                if (scrollView == null || track == null || handle == null || measuredContent == null)
                {
                    return;
                }

                var trackHeight = ResolveTrackHeight();
                var viewportHeight = ResolveViewportHeight();
                var contentHeight = ResolveContentHeight();
                if (trackHeight <= 0f || viewportHeight <= 0f || contentHeight <= viewportHeight + 1f)
                {
                    track.style.display = DisplayStyle.None;
                    return;
                }

                track.style.display = DisplayStyle.Flex;

                var maxScrollY = Mathf.Max(1f, contentHeight - viewportHeight);
                var visibleRatio = Mathf.Clamp01(viewportHeight / contentHeight);
                var handleHeight = Mathf.Clamp(trackHeight * visibleRatio, options.MinHandleHeight, trackHeight);
                var maxHandleY = Mathf.Max(0f, trackHeight - handleHeight);
                var scrollRatio = Mathf.Clamp01(scrollView.scrollOffset.y / maxScrollY);

                handle.style.height = handleHeight;
                handle.style.top = maxHandleY * scrollRatio;
            }

            private void RegisterCallbacks()
            {
                scrollView.RegisterCallback<WheelEvent>(evt =>
                {
                    evt.StopPropagation();
                    Update();
                    scrollView.schedule.Execute(Update).StartingIn(1);
                });
                scrollView.RegisterCallback<GeometryChangedEvent>(_ => Update());
                scrollView.RegisterCallback<AttachToPanelEvent>(_ =>
                {
                    HideNativeScroller(scrollView);
                    Update();
                });
                scrollView.verticalScroller.valueChanged += _ => Update();

                handle.RegisterCallback<PointerEnterEvent>(_ => handle.style.backgroundColor = options.ActiveColor);
                handle.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    if (!isDragging)
                    {
                        handle.style.backgroundColor = options.NormalColor;
                    }
                });
                handle.RegisterCallback<PointerDownEvent>(evt =>
                {
                    isDragging = true;
                    dragStartY = evt.position.y;
                    dragStartOffsetY = scrollView.scrollOffset.y;
                    handle.CapturePointer(evt.pointerId);
                    handle.style.backgroundColor = options.ActiveColor;
                    evt.StopPropagation();
                });
                handle.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (!isDragging)
                    {
                        return;
                    }

                    var viewportHeight = ResolveViewportHeight();
                    var contentHeight = ResolveContentHeight();
                    var trackHeight = ResolveTrackHeight();
                    var handleHeight = ResolveHeight(handle);
                    var maxScrollY = Mathf.Max(0f, contentHeight - viewportHeight);
                    var maxHandleY = Mathf.Max(1f, trackHeight - handleHeight);
                    var scrollDelta = (evt.position.y - dragStartY) / maxHandleY * maxScrollY;
                    var nextY = Mathf.Clamp(dragStartOffsetY + scrollDelta, 0f, maxScrollY);
                    scrollView.scrollOffset = new Vector2(scrollView.scrollOffset.x, nextY);
                    Update();
                    evt.StopPropagation();
                });
                handle.RegisterCallback<PointerUpEvent>(evt =>
                {
                    isDragging = false;
                    handle.ReleasePointer(evt.pointerId);
                    handle.style.backgroundColor = options.NormalColor;
                    evt.StopPropagation();
                });
            }

            private float ResolveViewportHeight()
            {
                var scrollHeight = ResolveHeight(scrollView);
                var paddingTop = scrollView.contentContainer.resolvedStyle.paddingTop;
                var paddingBottom = scrollView.contentContainer.resolvedStyle.paddingBottom;
                return Mathf.Max(0f, scrollHeight - paddingTop - paddingBottom);
            }

            private float ResolveContentHeight()
            {
                var paddingTop = scrollView.contentContainer.resolvedStyle.paddingTop;
                var paddingBottom = scrollView.contentContainer.resolvedStyle.paddingBottom;
                var measuredHeight = ResolveHeight(measuredContent);
                var containerHeight = ResolveHeight(scrollView.contentContainer);
                var childrenBottom = 0f;
                foreach (var child in measuredContent.Children())
                {
                    var childY = child.layout.y;
                    var childHeight = ResolveHeight(child);
                    if (!float.IsNaN(childY) && childHeight > 0f)
                    {
                        childrenBottom = Mathf.Max(childrenBottom, childY + childHeight);
                    }
                }

                return Mathf.Max(containerHeight, measuredHeight, childrenBottom + paddingTop + paddingBottom);
            }

            private float ResolveTrackHeight()
            {
                var trackHeight = ResolveHeight(track);
                if (trackHeight > 0f)
                {
                    return trackHeight;
                }

                var hostHeight = ResolveHeight(trackHost);
                return Mathf.Max(0f, hostHeight - options.Top - options.Bottom);
            }

            private static float ResolveHeight(VisualElement element)
            {
                if (element == null)
                {
                    return 0f;
                }

                var resolved = element.resolvedStyle.height;
                if (!float.IsNaN(resolved) && resolved > 0f)
                {
                    return resolved;
                }

                var layout = element.layout.height;
                return !float.IsNaN(layout) && layout > 0f ? layout : 0f;
            }

            private static void HideNativeScroller(ScrollView scrollView)
            {
                var scroller = scrollView?.verticalScroller;
                if (scroller == null)
                {
                    return;
                }

                scrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;
                scroller.lowButton.style.display = DisplayStyle.None;
                scroller.highButton.style.display = DisplayStyle.None;
                scroller.style.display = DisplayStyle.None;
                scroller.style.width = 0;
                scroller.style.minWidth = 0;
            }

            private static void SetRadius(VisualElement element, float radius)
            {
                element.style.borderTopLeftRadius = radius;
                element.style.borderTopRightRadius = radius;
                element.style.borderBottomLeftRadius = radius;
                element.style.borderBottomRightRadius = radius;
            }
        }

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
                string apiSource = "API",
                long? serverBookId = null,
                string isbn13 = null,
                string isbn10 = null,
                string sourceUrl = null,
                string coverImageUrl = null,
                string publishedDate = null,
                long? userBookId = null)
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
                ApiSource = string.IsNullOrWhiteSpace(apiSource) ? "API" : apiSource;
                ServerBookId = serverBookId;
                Isbn13 = isbn13;
                Isbn10 = isbn10;
                SourceUrl = sourceUrl;
                CoverImageUrl = coverImageUrl;
                PublishedDate = publishedDate;
                UserBookId = userBookId;
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
            public long? UserBookId { get; }

            public BookViewModel WithServerStats(float rating, int reviews, int views, int records, long? userBookId)
            {
                return new BookViewModel(
                    Id,
                    Title,
                    Author,
                    Publisher,
                    Summary,
                    Genre,
                    Status,
                    Shelf,
                    Price,
                    rating,
                    reviews,
                    views,
                    records,
                    CoverColor,
                    AccentColor,
                    ApiSource,
                    ServerBookId,
                    Isbn13,
                    Isbn10,
                    SourceUrl,
                    CoverImageUrl,
                    PublishedDate,
                    userBookId ?? UserBookId);
            }
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
                string thumbnailUrl,
                int followers,
                int likes,
                int visits,
                Color accentColor,
                bool isOwnSpace = false)
            {
                Id = id;
                OwnerId = ownerId;
                Owner = owner;
                Title = title;
                Mood = mood;
                Description = description;
                ThumbnailKey = thumbnailKey;
                ThumbnailUrl = thumbnailUrl;
                Followers = followers;
                Likes = likes;
                Visits = visits;
                AccentColor = accentColor;
                IsOwnSpace = isOwnSpace;
            }

            public string Id { get; }
            public string OwnerId { get; }
            public string Owner { get; }
            public string Title { get; }
            public string Mood { get; }
            public string Description { get; }
            public string ThumbnailKey { get; }
            public string ThumbnailUrl { get; }
            public int Followers { get; set; }
            public int Likes { get; set; }
            public int Visits { get; }
            public Color AccentColor { get; }
            public bool IsOwnSpace { get; }
        }

        private sealed class UserViewModel
        {
            public UserViewModel(long id, string nickname, string profileImageUrl, bool mutual, bool following, bool blocked)
            {
                Id = id;
                Nickname = nickname;
                ProfileImageUrl = profileImageUrl;
                Mutual = mutual;
                Following = following;
                Blocked = blocked;
            }

            public long Id { get; }
            public string Nickname { get; }
            public string ProfileImageUrl { get; }
            public bool Mutual { get; }
            public bool Following { get; set; }
            public bool Blocked { get; }
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
                : this(
                    id,
                    name,
                    category,
                    LooksLikeRemoteUrl(description) ? description : string.Empty,
                    LooksLikeRemoteUrl(description) ? "Remote asset" : description,
                    price,
                    popularity,
                    releaseOrder,
                    color,
                    accentColor)
            {
            }

            public DecorObjectViewModel(
                string id,
                string name,
                string category,
                string previewImageUrl,
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
                PreviewImageUrl = previewImageUrl;
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
            public string PreviewImageUrl { get; }
            public string Description { get; }
            public int Price { get; }
            public int Popularity { get; }
            public int ReleaseOrder { get; }
            public Color Color { get; }
            public Color AccentColor { get; }

            private static bool LooksLikeRemoteUrl(string value)
            {
                return !string.IsNullOrWhiteSpace(value) &&
                       (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        value.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
            }
        }

        private const int RankingPageSize = 6;
        private const int DashboardSortingOrder = 320;
        private const int FallbackSortingOrder = -320;
        private const int DashboardInitializationRetryFrames = 30;
        private const int MaxSpaceThumbnailTextureCacheCount = 32;
        private const int MaxRemoteSpaceThumbnailWidth = 2048;
        private const int MaxRemoteSpaceThumbnailHeight = 1152;
        private const int MaxRemoteAssetPreviewWidth = 2048;
        private const int MaxRemoteAssetPreviewHeight = 2048;
        private const int MaxAssetPreviewTextureCacheCount = 64;
        private const int MaxBookCoverTextureCacheCount = 64;
        private const int MaxRemoteBookCoverWidth = 2048;
        private const int MaxRemoteBookCoverHeight = 3072;
        private const int SearchResultPageSize = 48;
        private const int MarketBookPageSize = 48;
        private const NotebookShelfDisplayStyle DefaultDisplayStyle = NotebookShelfDisplayStyle.Spine;
        private const string PanelSettingsResource = "NotebookPanelSettings";
        private const string CommonSearchIconPath = "Assets/App/UI/Icons/Common/icon_search.png";
        private const string CustomizationPreviewResourceRoot = "App/Composition/Previews/Customization";
        private const string CustomizationPreviewAssetRoot = "Assets/Resources/" + CustomizationPreviewResourceRoot;
        private const string CustomizationPreviewUrlPrefix = "resources://customization/";

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
        private static readonly Color HeartRed = Hex(0xD9415D);
        private static readonly Color Night = Hex(0x091626);
        private static readonly Color WarmPaper = Hex(0xFFF7E8);
        private static readonly Color Coral = Hex(0xFF8A65);
        private static readonly Color Violet = Hex(0x6E5BFF);
        private static readonly Color Gold = Hex(0xF4C95D);
        private static Sprite commonSearchIconSprite;
        private static Texture2D searchIconTexture;
        private static Texture2D closeIconTexture;
        private static Dictionary<string, Sprite> customizationPreviewSpriteByKey;
        private static readonly string[] SuggestedSearches = { "소설", "에세이", "심리", "베스트셀러", "SF", "자기계발", "인문" };

        private static readonly string[] HeaderTabs = { "책 시장", "다른 공간", "꾸미기 시장" };
        private static readonly string[] SubTabs = { "전체 탐색", "무료", "유료" };
        private static readonly string[] Categories = { "전체", "읽는 중", "읽음", "안 읽음", "책장 1", "책장 2" };
        private static readonly string[] Genres = { "전체", "소설", "에세이", "인문", "심리", "경제/경영", "SF/판타지" };
        private static readonly string[] RankingSorts = { "인기순", "별점순", "리뷰순" };
        private static readonly string[] ObjectSorts = { "인기순", "최신순", "가격순" };
        private static readonly string[] ObjectCategories = { "전체", "필수 배치 오브젝트", "기타 배치 오브젝트", "전체 테마" };

        [SerializeField] private UIDocument document;

        private readonly Dictionary<string, Button> headerTabButtons = new();
        private readonly Dictionary<string, Button> categoryButtons = new();
        private readonly Dictionary<string, Button> subTabButtons = new();
        private readonly Dictionary<string, Button> genreButtons = new();
        private readonly Dictionary<string, Button> searchSuggestionButtons = new();
        private readonly Dictionary<string, Button> rankingSortButtons = new();
        private readonly Dictionary<string, Button> objectSortButtons = new();
        private readonly Dictionary<string, Button> objectCategoryButtons = new();
        private readonly List<Button> pageButtons = new();
        private readonly HashSet<string> addedBookIds = new();
        private readonly HashSet<string> followedUserIds = new();
        private readonly HashSet<string> likedSpaceIds = new();
        private readonly HashSet<string> wishedObjectIds = new();
        private readonly HashSet<string> ownedObjectIds = new();
        private readonly HashSet<string> favoriteBookIds = new();
        private readonly Dictionary<string, long> bookUserBookIds = new();
        private readonly HashSet<string> pendingShelfBookIds = new();
        private readonly Dictionary<string, List<BookReviewViewModel>> bookReviewMap = new();
        private readonly List<UserViewModel> socialListUsers = new();
        private readonly HashSet<string> requestedBookReviewIds = new();
        private readonly HashSet<string> requestedBookDetailIds = new();
        private readonly Dictionary<string, Texture2D> spaceThumbnailTextureCache = new();
        private readonly LinkedList<string> spaceThumbnailTextureCacheOrder = new();
        private readonly Dictionary<string, LinkedListNode<string>> spaceThumbnailTextureCacheNodes = new();
        private readonly Dictionary<string, Texture2D> assetPreviewTextureCache = new();
        private readonly LinkedList<string> assetPreviewTextureCacheOrder = new();
        private readonly Dictionary<string, LinkedListNode<string>> assetPreviewTextureCacheNodes = new();
        private readonly Dictionary<string, Texture2D> bookCoverTextureCache = new();
        private readonly LinkedList<string> bookCoverTextureCacheOrder = new();
        private readonly Dictionary<string, LinkedListNode<string>> bookCoverTextureCacheNodes = new();
        private readonly Dictionary<string, int> bookCoverTextureUseCounts = new();
        private readonly Dictionary<string, Texture2D> pendingBookCoverTextureDestroy = new();
        private readonly List<Button> bookRatingStarButtons = new();
        private SpaceViewModel[] apiSpaces = Array.Empty<SpaceViewModel>();
        private DecorObjectViewModel[] apiDecorObjects = Array.Empty<DecorObjectViewModel>();
        private IReadOnlyList<NotebookPurchaseHistoryItem> purchaseHistoryItems = Array.Empty<NotebookPurchaseHistoryItem>();
        private IReadOnlyList<string> paymentMethodItems = Array.Empty<string>();

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
        private ScrollView bookDetailScrollView;
        private UiToolkitScrollIndicator bookDetailScrollIndicator;
        private ScrollView bookDetailSummaryScrollView;
        private UiToolkitScrollIndicator bookDetailSummaryScrollIndicator;
        private VisualElement otherSpacesContent;
        private VisualElement otherSpacesList;
        private VisualElement socialListPanel;
        private VisualElement socialListContent;
        private VisualElement objectMarketContent;
        private VisualElement objectList;
        private VisualElement recommendList;
        private VisualElement rankingGrid;
        private VisualElement bookDetailCover;
        private VisualElement bookDetailReviews;
        private VisualElement bookReviewPanel;
        private VisualElement walletPanel;
        private VisualElement walletPurchaseList;
        private VisualElement detailCover;
        private ScrollView recommendScrollView;
        private TextField searchField;
        private TextField otherSpacesSearchField;
        private TextField bookReviewInput;
        private Button clearSearchButton;
        private Button bookFavoriteButton;
        private Button bookDetailAddButton;
        private Button bookReviewMoreButton;
        private Button bookReviewToggleButton;
        private Button socialListMoreButton;
        private Label headerSubtitleLabel;
        private Label heroEyebrowLabel;
        private Label heroTitleLabel;
        private Label heroCopyLabel;
        private Label pageBalanceLabel;
        private Label walletPanelBalanceLabel;
        private Label walletMethodLabel;
        private Label resultLabel;
        private Label searchResultsTitleLabel;
        private Label searchResultsCountLabel;
        private Label searchResultsHintLabel;
        private Label objectResultLabel;
        private Label otherSpacesStatusLabel;
        private Label socialListTitleLabel;
        private Label socialListHintLabel;
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
        private SearchNotebookBooksUseCase searchNotebookBooksUseCase;
        private LoadNotebookMarketBooksUseCase loadNotebookMarketBooksUseCase;
        private AddNotebookBookToShelfUseCase addNotebookBookToShelfUseCase;
        private NotebookRuntimeUseCase notebookRuntimeUseCase;
        private PaymentUseCase paymentUseCase;

        // Payment overlay UI elements + state — KG이니시스 결제 흐름 (MR S14P31F206-265).
        // 기존 ChargePagesThroughApi(1000) deprecated 흐름을 대체. NotebookToolkit 안에 통합한 이유: composition root
        // 추가 변경 최소화 + UI Toolkit 같은 패턴 (VisualElement 코드 빌드) 일관.
        private VisualElement paymentOverlayRoot;
        private VisualElement paymentChargePanel;
        private VisualElement paymentPendingPanel;
        private VisualElement paymentHistoryPanel;
        private Label paymentPendingStatusLabel;
        private Label paymentHistoryEmptyLabel;
        private VisualElement paymentHistoryListBody;
        private ChargePreset paymentSelectedPreset = ChargePreset.Pages1000;
        private string paymentPendingOrderId;
        private Coroutine paymentPollCoroutine;
        private const float PaymentPollIntervalSeconds = 2f;
        private const float PaymentPollTimeoutSeconds = 300f;
        public event Action BookAddedToShelfThroughApi;
        public event Action<NotebookSpaceVisitRequest> SpaceVisitRequested;
        private BookViewModel[] marketBooks = Array.Empty<BookViewModel>();
        private BookViewModel[] recommendedBooks = Array.Empty<BookViewModel>();
        private BookViewModel[] searchResults = Array.Empty<BookViewModel>();
        private int toastHideVersion;
        private int dashboardShowVersion;
        private int searchRequestVersion;
        private int marketBooksRequestVersion;
        private int runtimeRequestVersion;
        private int otherSpacesRequestVersion;
        private int socialListRequestVersion;
        private int walletDetailRequestVersion;

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
        private string otherSpacesQuery = string.Empty;
        private string appliedOtherSpacesQuery = string.Empty;
        private string otherSpacesNextCursor = string.Empty;
        private string otherSpacesErrorMessage = string.Empty;
        private string socialListMode = string.Empty;
        private string socialListOwnerName = string.Empty;
        private string socialListCursor = string.Empty;
        private string walletHistoryMessage = string.Empty;
        private long socialListOwnerUserId;
        private int selectedBookIndex;
        private int pageIndex;
        private int pages;
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
        private bool marketBooksLoading;
        private bool marketBooksRequested;
        private bool runtimeDataRequested;
        private bool runtimeDataLoading;
        private bool otherSpacesLoading;
        private bool socialListLoading;
        private bool socialListPanelOpen;
        private bool walletPanelOpen;
        private bool walletDetailsLoading;
        private bool shelfTargetLoading;
        private BookViewModel selectedBookOverride;
        private bool pendingAdvertisedBookOpen;
        private string pendingAdvertisedBookTitle = string.Empty;
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

        private void OnDestroy()
        {
            ClearSpaceThumbnailTextureCache();
            ClearAssetPreviewTextureCache();
            ClearBookCoverTextureCache();
        }

        private void Update()
        {
            if (!visible || pageScrollView == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) &&
                IsBookDetailModalOpen())
            {
                CloseBookDetail();
                return;
            }

            if (IsBookDetailModalOpen())
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

        public void Configure(
            UnityAction onCloseRequested,
            SearchNotebookBooksUseCase searchUseCase = null,
            LoadNotebookMarketBooksUseCase loadMarketUseCase = null,
            AddNotebookBookToShelfUseCase addUseCase = null,
            NotebookRuntimeUseCase runtimeUseCase = null,
            PaymentUseCase paymentUseCase = null)
        {
            closeRequested = onCloseRequested;
            ConfigureNotebookBookUseCases(searchUseCase, loadMarketUseCase, addUseCase, runtimeUseCase, paymentUseCase);
            ResolveDocument();
        }

        public void ConfigureNotebookBookUseCases(
            SearchNotebookBooksUseCase searchUseCase,
            LoadNotebookMarketBooksUseCase loadMarketUseCase,
            AddNotebookBookToShelfUseCase addUseCase,
            NotebookRuntimeUseCase runtimeUseCase = null,
            PaymentUseCase paymentUseCase = null)
        {
            searchNotebookBooksUseCase = searchUseCase;
            loadNotebookMarketBooksUseCase = loadMarketUseCase;
            addNotebookBookToShelfUseCase = addUseCase;
            notebookRuntimeUseCase = runtimeUseCase;
            this.paymentUseCase = paymentUseCase;
            marketBooksRequested = false;
            marketBooksLoading = false;
            runtimeDataRequested = false;
            runtimeDataLoading = false;
            otherSpacesLoading = false;
            otherSpacesQuery = string.Empty;
            appliedOtherSpacesQuery = string.Empty;
            otherSpacesNextCursor = string.Empty;
            otherSpacesErrorMessage = string.Empty;
            socialListLoading = false;
            socialListPanelOpen = false;
            socialListUsers.Clear();
            socialListCursor = string.Empty;
            marketBooks = Array.Empty<BookViewModel>();
            recommendedBooks = Array.Empty<BookViewModel>();
            apiSpaces = Array.Empty<SpaceViewModel>();
            apiDecorObjects = Array.Empty<DecorObjectViewModel>();
            bookReviewMap.Clear();
            requestedBookReviewIds.Clear();
            requestedBookDetailIds.Clear();
            bookUserBookIds.Clear();
            shelfTargetLoading = false;
            selectedBookOverride = null;
            selectedBookIndex = 0;

            if (initialized && visible)
            {
                RefreshAll();
            }
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

        public void OpenOtherSpacesTab()
        {
            activeHeaderTab = HeaderTabs[1];
            searchPageOpen = false;
            CloseBookDetail();
            CloseSocialListPanel();
            pageIndex = 0;
            if (pageScrollView != null)
            {
                pageScrollView.scrollOffset = Vector2.zero;
            }

            if (initialized)
            {
                RefreshAll();
            }
        }

        public bool OpenAdvertisedBookDetail(string preferredTitle = null)
        {
            ResolveDocument();
            if (document == null)
            {
                return false;
            }

            pendingAdvertisedBookOpen = true;
            pendingAdvertisedBookTitle = preferredTitle ?? string.Empty;
            EnsureInitialized();
            return TryOpenPendingAdvertisedBookDetail(true);
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
            BuildWalletPanel(dashboard);
            BuildSocialListPanel(dashboard);
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
            bookDetailScrollView = null;
            bookDetailScrollIndicator = null;
            bookDetailSummaryScrollView = null;
            bookDetailSummaryScrollIndicator = null;
            otherSpacesContent = null;
            otherSpacesList = null;
            socialListPanel = null;
            socialListContent = null;
            objectMarketContent = null;
            objectList = null;
            recommendList = null;
            rankingGrid = null;
            bookDetailCover = null;
            bookDetailReviews = null;
            bookReviewPanel = null;
            walletPanel = null;
            walletPurchaseList = null;
            detailCover = null;
            recommendScrollView = null;
            searchField = null;
            otherSpacesSearchField = null;
            bookReviewInput = null;
            clearSearchButton = null;
            bookFavoriteButton = null;
            bookDetailAddButton = null;
            bookReviewMoreButton = null;
            bookReviewToggleButton = null;
            socialListMoreButton = null;
            headerSubtitleLabel = null;
            heroEyebrowLabel = null;
            heroTitleLabel = null;
            heroCopyLabel = null;
            pageBalanceLabel = null;
            walletPanelBalanceLabel = null;
            walletMethodLabel = null;
            resultLabel = null;
            searchResultsTitleLabel = null;
            searchResultsCountLabel = null;
            searchResultsHintLabel = null;
            objectResultLabel = null;
            otherSpacesStatusLabel = null;
            socialListTitleLabel = null;
            socialListHintLabel = null;
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
            walletPanelOpen = false;
            walletDetailsLoading = false;
            walletHistoryMessage = string.Empty;
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
            wallet.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                ToggleWalletPanel();
            });
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
                OpenPaymentChargeOverlay();
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

        private void BuildWalletPanel(VisualElement parent)
        {
            var panel = new VisualElement { name = "wallet-detail-panel" };
            walletPanel = panel;
            panel.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            panel.style.display = DisplayStyle.None;
            panel.style.position = Position.Absolute;
            panel.style.top = 70;
            panel.style.right = 52;
            panel.style.width = 320;
            panel.style.maxHeight = 430;
            panel.style.paddingLeft = 18;
            panel.style.paddingRight = 18;
            panel.style.paddingTop = 16;
            panel.style.paddingBottom = 16;
            panel.style.backgroundColor = Rgba(0xFF, 0xFF, 0xFF, 0.97f);
            panel.style.overflow = Overflow.Hidden;
            Round(panel, 18);
            Border(panel, Rgba(0x0E, 0x1D, 0x2A, 0.12f));
            parent.Add(panel);

            var titleRow = Row("wallet-detail-title");
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 12;
            panel.Add(titleRow);

            var title = Text("페이지 지갑", 18, Ink, FontStyle.Bold);
            titleRow.Add(title);

            var titleSpacer = new VisualElement();
            titleSpacer.style.flexGrow = 1f;
            titleRow.Add(titleSpacer);

            var close = CloseIconButton("지갑 닫기");
            close.style.width = 28;
            close.style.height = 28;
            close.style.backgroundColor = Color.clear;
            close.style.borderTopWidth = 0;
            close.style.borderRightWidth = 0;
            close.style.borderBottomWidth = 0;
            close.style.borderLeftWidth = 0;
            close.clicked += () => SetWalletPanelOpen(false, false);
            titleRow.Add(close);

            var balanceCard = Row("wallet-balance-card");
            balanceCard.style.alignItems = Align.Center;
            balanceCard.style.justifyContent = Justify.SpaceBetween;
            balanceCard.style.height = 78;
            balanceCard.style.paddingLeft = 14;
            balanceCard.style.paddingRight = 14;
            balanceCard.style.marginBottom = 12;
            balanceCard.style.backgroundColor = Rgba(0x58, 0xB9, 0xFF, 0.10f);
            Round(balanceCard, 14);
            Border(balanceCard, Rgba(0x58, 0xB9, 0xFF, 0.24f));
            panel.Add(balanceCard);

            var balanceColumn = new VisualElement();
            balanceColumn.style.flexDirection = FlexDirection.Column;
            balanceCard.Add(balanceColumn);

            var balanceLabel = Text("보유 페이지", 11, FaintInk, FontStyle.Normal);
            balanceLabel.style.marginBottom = 4;
            balanceColumn.Add(balanceLabel);

            walletPanelBalanceLabel = Text(string.Empty, 28, AccentDeep, FontStyle.Bold);
            balanceColumn.Add(walletPanelBalanceLabel);

            var charge = TextButton("+ 충전", true);
            charge.style.height = 36;
            charge.clicked += () =>
            {
                PulseElement(charge);
                OpenPaymentChargeOverlay();
            };
            balanceCard.Add(charge);

            var methodTitle = Text("결제 수단", 12, Ink, FontStyle.Bold);
            methodTitle.style.marginBottom = 6;
            panel.Add(methodTitle);

            walletMethodLabel = Text("불러오는 중", 12, SoftInk, FontStyle.Normal);
            walletMethodLabel.style.whiteSpace = WhiteSpace.Normal;
            walletMethodLabel.style.marginBottom = 14;
            panel.Add(walletMethodLabel);

            var historyRow = Row("wallet-history-header");
            historyRow.style.alignItems = Align.Center;
            historyRow.style.marginBottom = 8;
            panel.Add(historyRow);

            var historyTitle = Text("오브젝트 구매내역", 12, Ink, FontStyle.Bold);
            historyRow.Add(historyTitle);

            var historySpacer = new VisualElement();
            historySpacer.style.flexGrow = 1f;
            historyRow.Add(historySpacer);

            var refresh = TextButton("새로고침", false);
            refresh.style.height = 28;
            refresh.style.paddingLeft = 10;
            refresh.style.paddingRight = 10;
            refresh.clicked += () =>
            {
                PulseElement(refresh);
                RequestWalletDetails();
            };
            historyRow.Add(refresh);

            walletPurchaseList = new VisualElement { name = "wallet-purchase-list" };
            walletPurchaseList.style.flexDirection = FlexDirection.Column;
            walletPurchaseList.style.minHeight = 112;
            walletPurchaseList.style.maxHeight = 190;
            walletPurchaseList.style.overflow = Overflow.Hidden;
            panel.Add(walletPurchaseList);

            RefreshWalletPanel();
        }

        private void BuildSocialListPanel(VisualElement parent)
        {
            var panel = new VisualElement { name = "social-list-panel" };
            socialListPanel = panel;
            panel.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            panel.style.display = DisplayStyle.None;
            panel.style.position = Position.Absolute;
            panel.style.top = 108;
            panel.style.right = 42;
            panel.style.width = 360;
            panel.style.maxHeight = 620;
            panel.style.paddingLeft = 18;
            panel.style.paddingRight = 18;
            panel.style.paddingTop = 16;
            panel.style.paddingBottom = 16;
            panel.style.backgroundColor = Rgba(0xFF, 0xFF, 0xFF, 0.97f);
            panel.style.overflow = Overflow.Hidden;
            Round(panel, 20);
            Border(panel, Rgba(0x0E, 0x1D, 0x2A, 0.12f));
            parent.Add(panel);

            var titleRow = Row("social-list-title-row");
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 6;
            panel.Add(titleRow);

            socialListTitleLabel = Text("팔로워", 18, Ink, FontStyle.Bold);
            titleRow.Add(socialListTitleLabel);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            titleRow.Add(spacer);

            var close = CloseIconButton("목록 닫기");
            close.style.width = 28;
            close.style.height = 28;
            close.style.backgroundColor = Color.clear;
            close.style.borderTopWidth = 0;
            close.style.borderRightWidth = 0;
            close.style.borderBottomWidth = 0;
            close.style.borderLeftWidth = 0;
            close.clicked += CloseSocialListPanel;
            titleRow.Add(close);

            socialListHintLabel = Text(string.Empty, 11, FaintInk, FontStyle.Normal);
            socialListHintLabel.style.whiteSpace = WhiteSpace.Normal;
            socialListHintLabel.style.marginBottom = 12;
            panel.Add(socialListHintLabel);

            var listScroll = new ScrollView(ScrollViewMode.Vertical) { name = "social-list-scroll" };
            listScroll.style.maxHeight = 442;
            listScroll.style.minHeight = 260;
            listScroll.style.minWidth = 0;
            listScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            listScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            listScroll.contentContainer.style.flexDirection = FlexDirection.Column;
            socialListContent = listScroll.contentContainer;
            panel.Add(listScroll);

            var more = TextButton("더 보기", false);
            more.style.height = 36;
            more.style.marginTop = 12;
            more.clicked += () =>
            {
                PulseElement(more);
                RequestSocialListPage(true);
            };
            panel.Add(more);
            socialListMoreButton = more;

            RefreshSocialListPanel();
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
            searchIcon.style.backgroundImage = ResolveSearchIconBackground();
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
            panel.style.paddingLeft = 10;
            panel.style.paddingRight = 10;
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
            searchResultsList.style.paddingLeft = 0;
            searchResultsList.style.paddingRight = 0;
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
            bookDetailContent.pickingMode = PickingMode.Position;
            bookDetailContent.RegisterCallback<WheelEvent>(evt => evt.StopPropagation());
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
            panel.style.width = 680;
            panel.style.height = 600;
            panel.style.maxWidth = Length.Percent(94);
            panel.style.maxHeight = Length.Percent(90);
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.paddingLeft = 20;
            panel.style.paddingRight = 20;
            panel.style.paddingTop = 10;
            panel.style.paddingBottom = 12;
            panel.style.backgroundColor = Rgba(0xF2, 0xFA, 0xF5, 0.99f);
            panel.style.overflow = Overflow.Hidden;
            Round(panel, 28);
            Border(panel, Rgba(0x0E, 0x1D, 0x2A, 0.14f));
            bookDetailContent.Add(panel);

            var headerArea = Row("book-detail-header-area");
            headerArea.style.position = Position.Absolute;
            headerArea.style.top = 6;
            headerArea.style.right = 24;
            headerArea.style.width = 32;
            headerArea.style.height = 32;
            headerArea.style.flexShrink = 0;
            headerArea.style.alignItems = Align.Center;
            headerArea.style.justifyContent = Justify.FlexEnd;
            headerArea.style.marginBottom = 0;
            headerArea.pickingMode = PickingMode.Position;
            panel.Add(headerArea);

            var topLayerClose = CloseIconButton("닫기");
            topLayerClose.style.width = 30;
            topLayerClose.style.height = 30;
            topLayerClose.style.marginRight = 0;
            topLayerClose.style.marginTop = 0;
            topLayerClose.style.backgroundColor = Color.clear;
            topLayerClose.style.borderTopWidth = 0;
            topLayerClose.style.borderRightWidth = 0;
            topLayerClose.style.borderBottomWidth = 0;
            topLayerClose.style.borderLeftWidth = 0;
            topLayerClose.clicked += CloseBookDetail;
            headerArea.Add(topLayerClose);

            var detailScrollFrame = new VisualElement { name = "book-detail-scroll-frame" };
            detailScrollFrame.style.position = Position.Relative;
            detailScrollFrame.style.flexGrow = 1f;
            detailScrollFrame.style.minHeight = 0;
            detailScrollFrame.style.width = Length.Percent(100);
            detailScrollFrame.style.overflow = Overflow.Hidden;
            panel.Add(detailScrollFrame);
            headerArea.BringToFront();

            var detailScroll = new ScrollView(ScrollViewMode.Vertical) { name = "book-detail-scroll" };
            bookDetailScrollView = detailScroll;
            detailScroll.style.position = Position.Absolute;
            detailScroll.style.left = 0;
            detailScroll.style.right = 0;
            detailScroll.style.top = 0;
            detailScroll.style.bottom = 0;
            detailScroll.style.flexGrow = 1f;
            detailScroll.style.minHeight = 0;
            detailScroll.style.width = Length.Percent(100);
            detailScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            detailScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            detailScroll.RegisterCallback<WheelEvent>(evt => evt.StopPropagation());
            detailScroll.RegisterCallback<AttachToPanelEvent>(_ => HideNativeScroller(detailScroll));
            detailScroll.contentContainer.style.flexDirection = FlexDirection.Column;
            detailScroll.contentContainer.style.alignItems = Align.Stretch;
            detailScroll.contentContainer.style.paddingRight = 30;
            detailScrollFrame.Add(detailScroll);
            bookDetailScrollIndicator = new UiToolkitScrollIndicator(
                detailScroll,
                detailScroll.contentContainer,
                new UiToolkitScrollIndicator.Options(
                    "book-detail-scroll-rail",
                    "book-detail-scroll-thumb",
                    10f,
                    46f,
                    8f,
                    12f,
                    44f,
                    Rgba(0x58, 0xB9, 0xFF, 0.78f),
                    AccentDeep,
                    Rgba(0x58, 0xB9, 0xFF, 0.18f),
                    2f,
                    5f,
                    5f),
                panel);

            var top = Row("book-detail-top");
            top.style.width = Length.Percent(100);
            top.style.height = StyleKeyword.Auto;
            top.style.flexDirection = FlexDirection.Column;
            top.style.flexGrow = 0f;
            top.style.paddingLeft = 0;
            top.style.paddingRight = 0;
            top.style.paddingTop = 0;
            top.style.paddingBottom = 0;
            top.style.flexWrap = Wrap.NoWrap;
            top.style.alignItems = Align.Stretch;
            top.style.flexShrink = 0f;
            detailScroll.contentContainer.Add(top);

            var coverStage = new VisualElement { name = "book-detail-cover-stage" };
            coverStage.style.width = Length.Percent(100);
            coverStage.style.height = 188;
            coverStage.style.flexShrink = 0;
            coverStage.style.marginBottom = 8;
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
            coverStageWash.style.height = 54;
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
            bookDetailCover.style.width = 124;
            bookDetailCover.style.height = 178;
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
            info.style.paddingTop = 12;
            info.style.paddingBottom = 12;
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

            var synopsisScroll = new ScrollView(ScrollViewMode.Vertical) { name = "SynopsisScrollView" };
            bookDetailSummaryScrollView = synopsisScroll;
            synopsisScroll.style.marginTop = 14;
            synopsisScroll.style.height = 96;
            synopsisScroll.style.minHeight = 96;
            synopsisScroll.style.maxHeight = 96;
            synopsisScroll.style.flexShrink = 0f;
            synopsisScroll.style.overflow = Overflow.Hidden;
            synopsisScroll.style.backgroundColor = Rgba(0xF7, 0xFF, 0xF7, 0.86f);
            synopsisScroll.style.borderLeftWidth = 3;
            synopsisScroll.style.borderLeftColor = Accent;
            synopsisScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            synopsisScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            synopsisScroll.contentContainer.name = "Content";
            synopsisScroll.contentContainer.style.flexDirection = FlexDirection.Column;
            synopsisScroll.contentContainer.style.paddingLeft = 14;
            synopsisScroll.contentContainer.style.paddingRight = 24;
            synopsisScroll.contentContainer.style.paddingTop = 10;
            synopsisScroll.contentContainer.style.paddingBottom = 10;
            Round(synopsisScroll, 12);
            info.Add(synopsisScroll);

            bookDetailSummary = Text(string.Empty, 13, Ink, FontStyle.Italic);
            bookDetailSummary.name = "SynopsisText";
            bookDetailSummary.style.whiteSpace = WhiteSpace.Normal;
            bookDetailSummary.style.minHeight = 0;
            bookDetailSummary.style.height = StyleKeyword.Auto;
            bookDetailSummary.style.flexShrink = 0f;
            bookDetailSummary.style.overflow = Overflow.Visible;
            bookDetailSummary.style.unityTextAlign = TextAnchor.UpperLeft;
            synopsisScroll.Add(bookDetailSummary);
            bookDetailSummaryScrollIndicator = new UiToolkitScrollIndicator(
                synopsisScroll,
                bookDetailSummary,
                new UiToolkitScrollIndicator.Options(
                    "SynopsisScrollbarTrack",
                    "SynopsisScrollbarHandle",
                    7f,
                    10f,
                    7f,
                    10f,
                    28f,
                    Rgba(0x58, 0xB9, 0xFF, 0.46f),
                    Rgba(0x58, 0xB9, 0xFF, 0.62f)));

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
            priceColumn.style.height = 48;
            priceColumn.style.alignSelf = Align.Center;
            priceColumn.style.justifyContent = Justify.Center;
            purchasePanel.Add(priceColumn);

            var priceLabel = Text("표지 가격", 12, FaintInk, FontStyle.Normal);
            priceLabel.style.height = 16;
            priceLabel.style.letterSpacing = 2f;
            priceLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            priceColumn.Add(priceLabel);

            bookDetailPrice = Text(string.Empty, 21, AccentDeep, FontStyle.Bold);
            bookDetailPrice.style.height = 28;
            bookDetailPrice.style.marginTop = 0;
            bookDetailPrice.style.unityTextAlign = TextAnchor.MiddleLeft;
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
            reviewPanel.style.right = 78;
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
            var reviewHeading = Text("리뷰", 17, Ink, FontStyle.Bold);
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
                "OTHER SPACES · COMMUNITY",
                "다른 공간",
                "유저 닉네임으로 서재 주인을 찾고, 마음에 드는 공간을 팔로우해보세요.");

            var searchPanel = new VisualElement { name = "other-spaces-search-panel" };
            searchPanel.style.flexDirection = FlexDirection.Column;
            searchPanel.style.paddingLeft = 16;
            searchPanel.style.paddingRight = 16;
            searchPanel.style.paddingTop = 14;
            searchPanel.style.paddingBottom = 14;
            searchPanel.style.marginBottom = 16;
            searchPanel.style.backgroundColor = Rgba(0xFF, 0xFF, 0xFF, 0.78f);
            Round(searchPanel, 18);
            Border(searchPanel, Rgba(0x58, 0xB9, 0xFF, 0.18f));
            parent.Add(searchPanel);

            var searchRow = Row("other-spaces-search-row");
            searchRow.style.alignItems = Align.Center;
            searchRow.style.minHeight = 44;
            searchRow.style.minWidth = 0;
            searchRow.style.flexWrap = Wrap.Wrap;
            searchPanel.Add(searchRow);

            var inputFrame = CreateInputFrame(42);
            inputFrame.style.flexGrow = 1f;
            inputFrame.style.minWidth = 260;
            inputFrame.style.marginRight = 10;
            inputFrame.style.marginBottom = 6;
            inputFrame.style.flexDirection = FlexDirection.Row;
            inputFrame.style.alignItems = Align.Center;
            searchRow.Add(inputFrame);

            var inputHint = Text("닉네임", 11, FaintInk, FontStyle.Normal);
            inputHint.style.marginRight = 10;
            inputHint.style.flexShrink = 0;
            inputFrame.Add(inputHint);

            otherSpacesSearchField = new TextField { value = otherSpacesQuery };
            otherSpacesSearchField.style.flexGrow = 1f;
            otherSpacesSearchField.style.minWidth = 0;
            otherSpacesSearchField.style.fontSize = 13;
            otherSpacesSearchField.RegisterValueChangedCallback(evt => otherSpacesQuery = evt.newValue ?? string.Empty);
            otherSpacesSearchField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    evt.StopPropagation();
                    SearchOtherSpaces();
                }
            });
            RegisterFieldStyling(otherSpacesSearchField);
            inputFrame.Add(otherSpacesSearchField);

            var searchButton = TextButton("검색", true);
            searchButton.style.width = 72;
            searchButton.style.height = 38;
            searchButton.style.marginRight = 8;
            searchButton.style.marginBottom = 6;
            searchButton.clicked += () =>
            {
                PulseElement(searchButton);
                SearchOtherSpaces();
            };
            searchRow.Add(searchButton);

            otherSpacesStatusLabel = Text(GetOtherSpacesStatusText(), 11, FaintInk, FontStyle.Normal);
            otherSpacesStatusLabel.style.whiteSpace = WhiteSpace.Normal;
            otherSpacesStatusLabel.style.marginTop = 4;
            searchPanel.Add(otherSpacesStatusLabel);

            var grid = Row("other-spaces-grid");
            otherSpacesList = grid;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.alignItems = Align.Stretch;
            parent.Add(grid);

            foreach (var space in apiSpaces)
            {
                grid.Add(CreateSpaceCard(space));
            }

            if (apiSpaces.Length == 0)
            {
                grid.Add(EmptyState(GetOtherSpacesEmptyText()));
            }
        }

        private void SearchOtherSpaces()
        {
            otherSpacesQuery = otherSpacesSearchField?.value?.Trim() ?? otherSpacesQuery?.Trim() ?? string.Empty;
            RequestSpaces(otherSpacesQuery);
        }

        private void RequestSpaceVisit(SpaceViewModel space)
        {
            if (space == null)
            {
                return;
            }

            if (space.IsOwnSpace)
            {
                ShowToast("내 공간은 방 화면에서 바로 확인할 수 있습니다.");
                return;
            }

            if (!long.TryParse(space.Id, out var spaceId) || spaceId <= 0)
            {
                ShowToast("공간 정보를 불러올 수 없습니다.");
                return;
            }

            SpaceVisitRequested?.Invoke(new NotebookSpaceVisitRequest
            {
                SpaceId = spaceId,
                OwnerUserId = TryParseApiId(space.OwnerId, out var ownerUserId) ? ownerUserId : 0L,
                Following = followedUserIds.Contains(space.OwnerId),
                Liked = likedSpaceIds.Contains(space.Id),
                LikeCount = Mathf.Max(0, space.Likes),
                FollowerCount = Mathf.Max(0, space.Followers)
            });
        }

        private string GetOtherSpacesStatusText()
        {
            if (otherSpacesLoading)
            {
                return "공간 목록을 불러오는 중입니다.";
            }

            if (!string.IsNullOrWhiteSpace(otherSpacesErrorMessage))
            {
                return otherSpacesErrorMessage;
            }

            var count = apiSpaces?.Length ?? 0;
            if (string.IsNullOrWhiteSpace(appliedOtherSpacesQuery))
            {
                return count > 0 ? $"인기 공간 {count:N0}개를 보여줍니다." : "인기 공간을 불러오지 못했습니다.";
            }

            return count > 0
                ? $"닉네임 '{appliedOtherSpacesQuery}' 검색 결과 {count:N0}개"
                : $"닉네임 '{appliedOtherSpacesQuery}' 검색 결과가 없습니다.";
        }

        private string GetOtherSpacesEmptyText()
        {
            if (otherSpacesLoading || runtimeDataLoading)
            {
                return "공간 API를 불러오는 중입니다.";
            }

            if (!string.IsNullOrWhiteSpace(otherSpacesErrorMessage))
            {
                return otherSpacesErrorMessage;
            }

            return string.IsNullOrWhiteSpace(appliedOtherSpacesQuery)
                ? "표시할 공간 데이터가 없습니다."
                : "검색된 유저의 공간이 없습니다. 다른 닉네임을 입력해보세요.";
        }

        private VisualElement CreateSpaceCard(SpaceViewModel space)
        {
            var card = new VisualElement { name = $"space-{space.Id}" };
            card.style.width = Length.Percent(48.6f);
            card.style.minWidth = 300;
            card.style.minHeight = 0;
            card.style.marginRight = 10;
            card.style.marginBottom = 10;
            card.style.paddingLeft = 0;
            card.style.paddingRight = 0;
            card.style.paddingTop = 0;
            card.style.paddingBottom = 0;
            card.style.backgroundColor = Rgba(0xFF, 0xFF, 0xFF, 0.86f);
            card.style.overflow = Overflow.Hidden;
            Round(card, 18);
            Border(card, Rgba(0x0E, 0x1D, 0x2A, 0.10f));
            card.RegisterCallback<ClickEvent>(evt =>
            {
                if (IsSpaceCardActionTarget(evt.target as VisualElement))
                {
                    return;
                }

                RequestSpaceVisit(space);
            });

            var thumbnail = new VisualElement { name = "space-thumbnail" };
            thumbnail.style.minHeight = 169;
            thumbnail.style.aspectRatio = 16f / 9f;
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
            shade.style.backgroundColor = Rgba(0x0E, 0x1D, 0x2A, HasRemoteSpacePhoto(space) ? 0f : 0.18f);
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
            BlockCardClickThrough(visitOverlay);
            visitOverlay.clicked += () => RequestSpaceVisit(space);
            thumbnail.Add(visitOverlay);

            var content = new VisualElement { name = "space-card-content" };
            content.style.paddingLeft = 16;
            content.style.paddingRight = 16;
            content.style.paddingTop = 14;
            content.style.paddingBottom = 14;
            card.Add(content);

            var top = Row("space-top");
            top.style.alignItems = Align.Center;
            top.style.minWidth = 0;
            content.Add(top);

            var titleBox = new VisualElement();
            titleBox.style.flexGrow = 1f;
            titleBox.style.minWidth = 0;
            top.Add(titleBox);

            var owner = Text(space.Owner, 18, Ink, FontStyle.Bold);
            owner.style.whiteSpace = WhiteSpace.Normal;
            owner.style.minHeight = 42;
            owner.style.maxHeight = 48;
            titleBox.Add(owner);

            var avatar = Text(InitialOf(space.Owner, "U"), 16, Surface, FontStyle.Bold);
            avatar.style.width = 42;
            avatar.style.height = 42;
            avatar.style.flexShrink = 0;
            avatar.style.unityTextAlign = TextAnchor.MiddleCenter;
            avatar.style.backgroundColor = space.AccentColor;
            Round(avatar, 21);
            top.Add(avatar);

            var stats = Row("space-stats");
            stats.style.marginTop = 10;
            stats.style.minHeight = 26;
            stats.style.alignItems = Align.Center;
            stats.style.flexWrap = Wrap.Wrap;
            content.Add(stats);
            var followers = MetricButton($"팔로워 {space.Followers:N0}", AccentDeep, () => OpenSocialListPanel(space, "followers"));
            stats.Add(followers);
            stats.Add(MetricButton("팔로잉", AccentDeep, () => OpenSocialListPanel(space, "followings")));
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
            BlockCardClickThrough(visit);
            visit.clicked += () => RequestSpaceVisit(space);
            actions.Add(visit);

            var follow = TextButton(string.Empty, false);
            follow.style.height = 34;
            follow.style.minWidth = 76;
            follow.style.marginRight = 8;
            BlockCardClickThrough(follow);
            follow.clicked += () =>
            {
                ToggleSpaceFollowThroughApi(space, follow, followers);
            };
            if (space.IsOwnSpace)
            {
                follow.text = "내 공간";
                follow.SetEnabled(false);
                follow.style.backgroundColor = Rgba(0x0E, 0x1D, 0x2A, 0.06f);
                follow.style.color = FaintInk;
                Border(follow, Rgba(0x0E, 0x1D, 0x2A, 0.08f));
            }
            else
            {
                actions.Add(follow);
                RefreshFollowButton(follow, space.OwnerId);
            }

            var like = TextButton(string.Empty, false);
            like.style.height = 34;
            like.style.minWidth = 58;
            like.style.paddingLeft = 10;
            like.style.paddingRight = 10;
            BlockCardClickThrough(like);
            like.clicked += () =>
            {
                ToggleSpaceLikeThroughApi(space, like);
            };
            actions.Add(like);
            RefreshSpaceLikeButton(like, space);

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
                    RequestRuntimeAssets();
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
                    RequestRuntimeAssets();
                };
                objectCategoryButtons[category] = button;
                control.Add(button);
            }

            objectList = Row("object-list");
            objectList.style.width = Length.Percent(100);
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
            IEnumerable<DecorObjectViewModel> filtered = apiDecorObjects;
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

            if (items.Length == 0)
            {
                objectList.Add(EmptyState(runtimeDataLoading ? "오브젝트 API를 불러오는 중입니다." : "표시할 오브젝트 API 데이터가 없습니다."));
            }

            if (objectResultLabel != null)
            {
                objectResultLabel.text = $"{items.Length:N0}개";
            }
        }

        private VisualElement CreateObjectCard(DecorObjectViewModel item)
        {
            var card = new VisualElement { name = $"object-{item.Id}" };
            card.style.width = Length.Percent(19.12f);
            card.style.minWidth = 136;
            card.style.minHeight = 226;
            card.style.marginRight = 6;
            card.style.marginBottom = 7;
            card.style.paddingLeft = 7;
            card.style.paddingRight = 7;
            card.style.paddingTop = 7;
            card.style.paddingBottom = 7;
            card.style.backgroundColor = Rgba(0xFF, 0xFF, 0xFF, 0.82f);
            card.style.overflow = Overflow.Hidden;
            Round(card, 14);
            Border(card, Rgba(0x0E, 0x1D, 0x2A, 0.10f));
            card.RegisterCallback<ClickEvent>(_ => ShowToast($"오브젝트 상세 화면 · {item.Name}"));

            var preview = new VisualElement { name = "object-preview" };
            preview.style.height = 126;
            preview.style.marginLeft = 3;
            preview.style.marginRight = 3;
            preview.style.marginBottom = 5;
            preview.style.alignItems = Align.Center;
            preview.style.justifyContent = Justify.Center;
            preview.style.backgroundColor = Color.clear;
            preview.style.overflow = Overflow.Hidden;
            Round(preview, 12);
            card.Add(preview);

            if (!TryBuildObjectPreviewImage(preview, item.PreviewImageUrl))
            {
                var shape = new VisualElement();
                shape.style.width = 106;
                shape.style.height = 54;
                shape.style.backgroundColor = item.Color;
                Round(shape, 12);
                preview.Add(shape);
            }

            var category = Text(item.Category, 10, AccentDeep, FontStyle.Bold);
            category.style.letterSpacing = 0.8f;
            category.style.minHeight = 14;
            card.Add(category);

            var title = Text(item.Name, 13, Ink, FontStyle.Bold);
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.minHeight = 25;
            card.Add(title);

            var description = Text(item.Description, 11, SoftInk, FontStyle.Normal);
            description.style.marginTop = 1;
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.minHeight = 23;
            card.Add(description);

            var meta = Row("object-meta");
            meta.style.marginTop = 4;
            meta.style.alignItems = Align.Center;
            meta.style.flexWrap = Wrap.Wrap;
            card.Add(meta);
            meta.Add(MetricLabel($"{item.Price:N0}P", AccentDeep));
            meta.Add(MetricLabel($"인기 {item.Popularity:N0}", Ink));

            var actions = Row("object-actions");
            actions.style.marginTop = 5;
            actions.style.alignItems = Align.Center;
            actions.style.flexWrap = Wrap.Wrap;
            card.Add(actions);

            var detail = TextButton("상세 보기", false);
            detail.style.height = 28;
            detail.style.paddingLeft = 9;
            detail.style.paddingRight = 9;
            detail.style.marginRight = 5;
            detail.style.marginBottom = 3;
            detail.style.fontSize = 11;
            detail.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            detail.clicked += () => ShowToast($"오브젝트 상세 화면 · {item.Name}");
            actions.Add(detail);

            var wish = TextButton(string.Empty, false);
            wish.style.height = 28;
            wish.style.paddingLeft = 9;
            wish.style.paddingRight = 9;
            wish.style.marginRight = 5;
            wish.style.marginBottom = 3;
            wish.style.fontSize = 11;
            wish.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            wish.clicked += () =>
            {
                ToggleObjectWishThroughApi(item, wish);
            };
            actions.Add(wish);
            RefreshToggleButton(wish, wishedObjectIds.Contains(item.Id), "찜 해제", "찜하기");

            var purchase = TextButton(ownedObjectIds.Contains(item.Id) ? "소유 중" : "구매하기", true);
            purchase.style.height = 28;
            purchase.style.paddingLeft = 10;
            purchase.style.paddingRight = 10;
            purchase.style.marginBottom = 3;
            purchase.style.fontSize = 11;
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
            scroller.RegisterCallback<WheelEvent>(BlockRecommendWheelScroll, TrickleDown.TrickleDown);
            scroller.RegisterCallback<WheelEvent>(BlockRecommendWheelScroll);
            scroller.RegisterCallback<AttachToPanelEvent>(_ => HideNativeScroller(scroller));
            parent.Add(scroller);

            recommendList = new VisualElement { name = "recommend-list" };
            recommendList.style.flexDirection = FlexDirection.Row;
            recommendList.style.flexWrap = Wrap.NoWrap;
            recommendList.style.alignItems = Align.Stretch;
            scroller.contentContainer.Add(recommendList);
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
            if (IsBookDetailModalOpen())
            {
                evt.StopPropagation();
                evt.StopImmediatePropagation();
                return;
            }

        }

        private void BlockRecommendWheelScroll(WheelEvent evt)
        {
            var scroller = recommendScrollView;
            var lockedOffset = scroller?.scrollOffset ?? Vector2.zero;
            evt.StopPropagation();
            evt.StopImmediatePropagation();
            scroller?.schedule.Execute(() =>
            {
                if (scroller.panel != null)
                {
                    scroller.scrollOffset = lockedOffset;
                }
            }).ExecuteLater(0);
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

            if (recommendList != null)
            {
                foreach (var child in recommendList.Children())
                {
                    contentWidth = Mathf.Max(contentWidth, recommendList.layout.x + child.layout.xMax + child.resolvedStyle.marginRight);
                }
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
            EnsureMarketBooksRequested();
            EnsureRuntimeDataRequested();
            RefreshWallet();
            RefreshFilters();
            RefreshCommunityPanels();
            RefreshModeVisibility();
            RefreshLists();
            RefreshObjectMarket();
            RefreshBookDetailPage(GetSelectedBook());
            TryOpenPendingAdvertisedBookDetail(false);
        }

        private void RefreshWallet()
        {
            if (pageBalanceLabel != null)
            {
                pageBalanceLabel.text = $"{pages:N0} P";
            }

            RefreshWalletPanel();
        }

        private void ToggleWalletPanel()
        {
            SetWalletPanelOpen(!walletPanelOpen, true);
        }

        private void SetWalletPanelOpen(bool open, bool animate)
        {
            walletPanelOpen = open;
            if (walletPanel == null)
            {
                return;
            }

            if (open)
            {
                walletPanel.style.display = DisplayStyle.Flex;
                RefreshWalletPanel();
                SyncWalletFromApi();
                RequestWalletDetails();
                if (animate)
                {
                    SlideFadeInFromRight(walletPanel, 24, 52, 0.16f);
                }
                else
                {
                    walletPanel.style.opacity = 1f;
                    walletPanel.style.right = 52;
                }
            }
            else
            {
                walletPanel.style.display = DisplayStyle.None;
            }
        }

        private void RequestWalletDetails()
        {
            if (!walletPanelOpen)
            {
                return;
            }

            if (notebookRuntimeUseCase == null)
            {
                walletDetailsLoading = false;
                purchaseHistoryItems = Array.Empty<NotebookPurchaseHistoryItem>();
                paymentMethodItems = Array.Empty<string>();
                walletHistoryMessage = "결제 API가 설정되지 않았습니다.";
                RefreshWalletPanel();
                return;
            }

            var requestVersion = ++walletDetailRequestVersion;
            walletDetailsLoading = true;
            walletHistoryMessage = string.Empty;
            RefreshWalletPanel();

            notebookRuntimeUseCase.LoadPurchaseHistory(
                result =>
                {
                    if (requestVersion != walletDetailRequestVersion)
                    {
                        return;
                    }

                    purchaseHistoryItems = result?.Purchases ?? Array.Empty<NotebookPurchaseHistoryItem>();
                    walletDetailsLoading = false;
                    walletHistoryMessage = string.Empty;
                    RefreshWalletPanel();
                },
                error =>
                {
                    if (requestVersion != walletDetailRequestVersion)
                    {
                        return;
                    }

                    walletDetailsLoading = false;
                    purchaseHistoryItems = Array.Empty<NotebookPurchaseHistoryItem>();
                    walletHistoryMessage = "구매내역을 불러오지 못했습니다.";
                    UnityEngine.Debug.LogWarning($"Notebook purchase history API failed. code={error?.Code}, message={FirstText(error?.Message, error?.Error)}");
                    RefreshWalletPanel();
                });

            notebookRuntimeUseCase.LoadPaymentMethods(
                result =>
                {
                    if (requestVersion != walletDetailRequestVersion)
                    {
                        return;
                    }

                    paymentMethodItems = result?.Methods ?? Array.Empty<string>();
                    RefreshWalletPanel();
                },
                error =>
                {
                    if (requestVersion != walletDetailRequestVersion)
                    {
                        return;
                    }

                    paymentMethodItems = Array.Empty<string>();
                    UnityEngine.Debug.LogWarning($"Notebook payment methods API failed. code={error?.Code}, message={FirstText(error?.Message, error?.Error)}");
                    RefreshWalletPanel();
                });
        }

        private void RefreshWalletPanel()
        {
            if (walletPanelBalanceLabel != null)
            {
                walletPanelBalanceLabel.text = $"{pages:N0} P";
            }

            if (walletMethodLabel != null)
            {
                walletMethodLabel.text = paymentMethodItems.Count > 0
                    ? string.Join(", ", paymentMethodItems)
                    : "등록된 결제수단 없음";
            }

            if (walletPurchaseList == null)
            {
                return;
            }

            walletPurchaseList.Clear();
            if (!string.IsNullOrWhiteSpace(walletHistoryMessage))
            {
                walletPurchaseList.Add(CreateWalletEmptyState(walletHistoryMessage));
                return;
            }

            if (walletDetailsLoading)
            {
                walletPurchaseList.Add(CreateWalletEmptyState("구매내역을 불러오는 중입니다."));
                return;
            }

            if (purchaseHistoryItems.Count == 0)
            {
                walletPurchaseList.Add(CreateWalletEmptyState("아직 구매한 오브젝트가 없습니다."));
                return;
            }

            foreach (var item in purchaseHistoryItems.Take(5))
            {
                walletPurchaseList.Add(CreatePurchaseHistoryRow(item));
            }
        }

        private VisualElement CreateWalletEmptyState(string message)
        {
            var empty = Text(message, 12, FaintInk, FontStyle.Normal);
            empty.style.height = 72;
            empty.style.unityTextAlign = TextAnchor.MiddleCenter;
            empty.style.whiteSpace = WhiteSpace.Normal;
            empty.style.backgroundColor = Rgba(0x0E, 0x1D, 0x2A, 0.04f);
            Round(empty, 12);
            return empty;
        }

        private VisualElement CreatePurchaseHistoryRow(NotebookPurchaseHistoryItem item)
        {
            var row = Row("wallet-purchase-row");
            row.style.height = 44;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 7;
            row.style.paddingLeft = 12;
            row.style.paddingRight = 12;
            row.style.backgroundColor = Rgba(0x0E, 0x1D, 0x2A, 0.04f);
            Round(row, 12);
            Border(row, Rgba(0x0E, 0x1D, 0x2A, 0.06f));

            var left = new VisualElement();
            left.style.flexDirection = FlexDirection.Column;
            left.style.flexGrow = 1f;
            left.style.minWidth = 0;
            row.Add(left);

            var name = Text($"Asset #{item.AssetId}", 12, Ink, FontStyle.Bold);
            left.Add(name);

            var status = Text(FormatPurchaseStatus(item.Status), 10, FaintInk, FontStyle.Normal);
            status.style.marginTop = 2;
            left.Add(status);

            var price = Text($"{Mathf.Max(0, item.PricePages):N0} P", 12, AccentDeep, FontStyle.Bold);
            price.style.marginLeft = 10;
            row.Add(price);
            return row;
        }

        private static string FormatPurchaseStatus(string status)
        {
            var normalized = string.IsNullOrWhiteSpace(status) ? string.Empty : status.Trim().ToUpperInvariant();
            return normalized switch
            {
                "COMPLETED" => "구매 완료",
                "PAID" => "결제 완료",
                "CANCELED" => "취소됨",
                "FAILED" => "실패",
                _ => string.IsNullOrWhiteSpace(status) ? "상태 없음" : status
            };
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

            RefreshSocialListPanel();
        }

        private void RefreshSocialListPanel()
        {
            if (socialListPanel == null)
            {
                return;
            }

            socialListPanel.style.display = socialListPanelOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (!socialListPanelOpen)
            {
                return;
            }

            var modeLabel = socialListMode == "followings" ? "팔로잉" : "팔로워";
            if (socialListTitleLabel != null)
            {
                socialListTitleLabel.text = $"{socialListOwnerName}님의 {modeLabel}";
            }

            if (socialListHintLabel != null)
            {
                socialListHintLabel.text = socialListLoading
                    ? "목록을 불러오는 중입니다."
                    : $"{socialListUsers.Count:N0}명을 표시하고 있습니다.";
            }

            if (socialListContent != null)
            {
                socialListContent.Clear();
                if (socialListUsers.Count == 0)
                {
                    socialListContent.Add(EmptyState(socialListLoading ? "사용자 목록을 불러오는 중입니다." : "표시할 사용자가 없습니다."));
                }
                else
                {
                    foreach (var user in socialListUsers)
                    {
                        socialListContent.Add(CreateSocialUserRow(user));
                    }
                }
            }

            if (socialListMoreButton != null)
            {
                socialListMoreButton.style.display = !string.IsNullOrWhiteSpace(socialListCursor) && !socialListLoading
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
        }

        private VisualElement CreateSocialUserRow(UserViewModel user)
        {
            var row = Row("social-user-row");
            row.style.alignItems = Align.Center;
            row.style.minHeight = 64;
            row.style.marginBottom = 8;
            row.style.paddingLeft = 10;
            row.style.paddingRight = 10;
            row.style.paddingTop = 8;
            row.style.paddingBottom = 8;
            row.style.backgroundColor = Rgba(0x0E, 0x1D, 0x2A, 0.04f);
            Round(row, 14);
            Border(row, Rgba(0x0E, 0x1D, 0x2A, 0.06f));

            var avatar = Text(InitialOf(user?.Nickname, "U"), 15, Surface, FontStyle.Bold);
            avatar.style.width = 38;
            avatar.style.height = 38;
            avatar.style.flexShrink = 0;
            avatar.style.unityTextAlign = TextAnchor.MiddleCenter;
            avatar.style.backgroundColor = SearchCoverColor(0, user?.Id.ToString());
            Round(avatar, 19);
            row.Add(avatar);

            var info = new VisualElement();
            info.style.flexGrow = 1f;
            info.style.minWidth = 0;
            info.style.marginLeft = 10;
            row.Add(info);

            var nickname = Text(FirstText(user?.Nickname, $"User {user?.Id ?? 0}"), 13, Ink, FontStyle.Bold);
            nickname.style.whiteSpace = WhiteSpace.Normal;
            nickname.style.maxHeight = 34;
            info.Add(nickname);

            var meta = Text(user != null && user.Mutual ? "맞팔로우" : user != null && user.Following ? "팔로잉 중" : "새로운 사용자", 10, FaintInk, FontStyle.Normal);
            meta.style.marginTop = 3;
            info.Add(meta);

            var follow = TextButton(string.Empty, false);
            follow.style.height = 32;
            follow.style.minWidth = 76;
            follow.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            follow.clicked += () => ToggleUserFollowThroughApi(user, follow);
            row.Add(follow);
            RefreshSocialFollowButton(follow, user);
            return row;
        }

        private void OpenSocialListPanel(SpaceViewModel space, string mode)
        {
            if (space == null || !TryParseApiId(space.OwnerId, out var ownerUserId))
            {
                ShowToast("사용자 목록을 열 수 없습니다.");
                return;
            }

            socialListPanelOpen = true;
            socialListMode = mode == "followings" ? "followings" : "followers";
            socialListOwnerUserId = ownerUserId;
            socialListOwnerName = FirstText(space.Owner, "사용자");
            socialListCursor = string.Empty;
            socialListUsers.Clear();
            RefreshSocialListPanel();
            SlideFadeInFromRight(socialListPanel, 72, 42, 0.16f);
            RequestSocialListPage(false);
        }

        private void CloseSocialListPanel()
        {
            socialListPanelOpen = false;
            socialListUsers.Clear();
            socialListCursor = string.Empty;
            socialListLoading = false;
            RefreshSocialListPanel();
        }

        private void RequestSocialListPage(bool append)
        {
            if (!socialListPanelOpen || socialListOwnerUserId <= 0L)
            {
                return;
            }

            if (!append)
            {
                socialListUsers.Clear();
                socialListCursor = string.Empty;
            }

            var requestCursor = append ? socialListCursor : string.Empty;
            var requestVersion = ++socialListRequestVersion;
            socialListLoading = true;
            RefreshSocialListPanel();

            if (notebookRuntimeUseCase == null)
            {
                socialListLoading = false;
                RefreshSocialListPanel();
                ShowToast("사용자 목록 API가 준비되지 않았습니다.");
                return;
            }

            Action<NotebookUserListResult> onSuccess = result =>
            {
                if (requestVersion != socialListRequestVersion)
                {
                    return;
                }

                var users = MapRuntimeUsers(result);
                if (!append)
                {
                    socialListUsers.Clear();
                }

                socialListUsers.AddRange(users);
                socialListCursor = result?.NextCursor ?? string.Empty;
                socialListLoading = false;
                RefreshSocialListPanel();
            };

            Action<NotebookApiError> onError = error =>
            {
                if (requestVersion != socialListRequestVersion)
                {
                    return;
                }

                socialListLoading = false;
                RefreshSocialListPanel();
                UnityEngine.Debug.LogWarning($"Notebook social list API failed. mode={socialListMode}, user={socialListOwnerUserId}, code={error?.Code}, message={FirstText(error?.Message, error?.Error)}");
                ShowToast("사용자 목록을 불러오지 못했습니다.");
            };

            if (socialListMode == "followings")
            {
                notebookRuntimeUseCase.LoadUserFollowings(socialListOwnerUserId, requestCursor, 30, onSuccess, onError);
            }
            else
            {
                notebookRuntimeUseCase.LoadUserFollowers(socialListOwnerUserId, requestCursor, 30, onSuccess, onError);
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
            var recommendFiltered = FilterRecommendedBooks().ToArray();
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
                bookDetailCover.Add(CreateBookCover(book, 118, 172, true));
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
                ResetBookDetailSummaryScroll();
            }

            if (bookDetailPrice != null)
            {
                bookDetailPrice.text = book.Price == 0 ? "FREE" : $"{book.Price:N0} P";
            }

            if (bookDetailReviewCount != null)
            {
                bookDetailReviewCount.text = $"★ {book.Rating:0.0} · 리뷰 {DisplayReviewCount(book):N0}개 · 조회 {book.Views:N0}";
            }

            RefreshFavoriteBookButton(book);
            RefreshBookDetailAddButton(book);
            RefreshBookReviews(book);
            RefreshBookReviewToggle();
            RefreshRatingStars();
            bookDetailScrollIndicator?.Update();
        }

        private void ResetBookDetailSummaryScroll()
        {
            bookDetailSummaryScrollIndicator?.ResetToTop();
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
                reviews = new List<BookReviewViewModel>();
                bookReviewMap[book.Id] = reviews;
            }

            return reviews;
        }

        private int DisplayReviewCount(BookViewModel book)
        {
            return book == null ? 0 : Mathf.Max(0, Mathf.Max(book.Reviews, GetBookReviews(book).Count));
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

        private void RequestBookReviews(BookViewModel book)
        {
            if (book == null || requestedBookReviewIds.Contains(book.Id))
            {
                return;
            }

            requestedBookReviewIds.Add(book.Id);
            bookReviewMap[book.Id] = new List<BookReviewViewModel>();

            if (notebookRuntimeUseCase == null || !TryResolveBookApiId(book, out var bookId))
            {
                UnityEngine.Debug.LogWarning($"Book review API skipped. book={book.Id}");
                requestedBookReviewIds.Remove(book.Id);
                return;
            }

            notebookRuntimeUseCase.LoadBookReviews(
                bookId,
                result =>
                {
                    var reviews = new List<BookReviewViewModel>();
                    var items = result?.Reviews;
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            if (item == null)
                            {
                                continue;
                            }

                            reviews.Add(new BookReviewViewModel(
                                FirstText(item.Author, "API"),
                                Mathf.Clamp(item.Rating, 0f, 5f),
                                FirstText(item.Content, string.Empty)));
                        }
                    }

                    bookReviewMap[book.Id] = reviews;
                    RefreshBookDetailPage(book);
                },
                error =>
                {
                    UnityEngine.Debug.LogWarning($"Book review list API failed. book={book.Id}, code={error?.Code}, message={FirstText(error?.Message, error?.Error)}");
                    requestedBookReviewIds.Remove(book.Id);
                    bookReviewMap[book.Id] = new List<BookReviewViewModel>();
                    RefreshBookDetailPage(book);
                });
        }

        private void RequestBookDetail(BookViewModel book)
        {
            if (book == null || requestedBookDetailIds.Contains(book.Id))
            {
                return;
            }

            if (notebookRuntimeUseCase == null || !TryResolveBookApiId(book, out var bookId))
            {
                return;
            }

            requestedBookDetailIds.Add(book.Id);
            notebookRuntimeUseCase.LoadBookDetail(
                bookId,
                detail =>
                {
                    requestedBookDetailIds.Remove(book.Id);
                    var updatedBook = ApplyBookDetailStats(book, detail);
                    if (updatedBook != null)
                    {
                        ReplaceBookViewModel(book, updatedBook);
                        RefreshLists();
                        RefreshBookDetailPage(updatedBook);
                    }
                },
                error =>
                {
                    UnityEngine.Debug.LogWarning($"Book detail API failed. book={book.Id}, code={error?.Code}, message={FirstText(error?.Message, error?.Error)}");
                    requestedBookDetailIds.Remove(book.Id);
                });
        }

        private BookViewModel ApplyBookDetailStats(BookViewModel book, NotebookBookSearchItem detail)
        {
            if (book == null || detail == null)
            {
                return null;
            }

            var rating = detail.AvgRating > 0 ? Mathf.Clamp((float)detail.AvgRating, 0f, 5f) : 0f;
            var reviews = Mathf.Max(0, detail.ReviewCount);
            var views = Mathf.Max(0, detail.ViewCount);
            var records = Mathf.Max(0, detail.RecordCount);
            var userBookId = detail.UserBookId.HasValue && detail.UserBookId.Value > 0
                ? detail.UserBookId
                : book.UserBookId;

            SetLocalState(addedBookIds, book.Id, detail.Owned);
            if (userBookId.HasValue && userBookId.Value > 0)
            {
                bookUserBookIds[book.Id] = userBookId.Value;
            }

            return book.WithServerStats(rating, reviews, views, records, userBookId);
        }

        private void ReplaceBookViewModel(BookViewModel original, BookViewModel updated)
        {
            if (original == null || updated == null)
            {
                return;
            }

            marketBooks = ReplaceBookViewModel(marketBooks, original, updated);
            recommendedBooks = ReplaceBookViewModel(recommendedBooks, original, updated);
            searchResults = ReplaceBookViewModel(searchResults, original, updated);
            if (selectedBookOverride != null && SameBook(selectedBookOverride, original))
            {
                selectedBookOverride = updated;
            }
        }

        private static BookViewModel[] ReplaceBookViewModel(BookViewModel[] source, BookViewModel original, BookViewModel updated)
        {
            if (source == null || source.Length == 0)
            {
                return source;
            }

            BookViewModel[] copy = null;
            for (var i = 0; i < source.Length; i++)
            {
                if (!SameBook(source[i], original))
                {
                    continue;
                }

                copy ??= (BookViewModel[])source.Clone();
                copy[i] = updated;
            }

            return copy ?? source;
        }

        private static bool SameBook(BookViewModel left, BookViewModel right)
        {
            return left != null &&
                   right != null &&
                   string.Equals(left.Id, right.Id, StringComparison.Ordinal);
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

        private bool IsBookDetailModalOpen()
        {
            return bookDetailContent != null &&
                   bookDetailContent.style.display.value == DisplayStyle.Flex;
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
            RequestBookDetail(book);
            RequestBookReviews(book);
            RefreshBookDetailPage(book);
            if (bookDetailContent != null)
            {
                bookDetailContent.style.display = DisplayStyle.Flex;
                FadeInElement(bookDetailContent, 0.14f);
            }

            if (bookDetailScrollView != null)
            {
                bookDetailScrollView.scrollOffset = Vector2.zero;
            }

            bookDetailScrollIndicator?.ResetToTop();

            if (bookDetailModalPanel != null)
            {
                bookDetailModalPanel.style.height = 600;
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
                SlideFadeInFromRight(bookReviewPanel, 40, 78, 0.14f);
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
                        SlideFadeInFromRight(bookReviewPanel, 28, 78, 0.22f);
                    }
                    else
                    {
                        bookReviewPanel.style.right = 78;
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
                bookDetailModalPanel.style.height = 600;
                bookDetailModalPanel.style.marginRight = open ? 300 : 0;
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

            if (notebookRuntimeUseCase == null || !TryResolveUserBookId(book, out var userBookId))
            {
                ShowToast("책장에 추가된 책만 즐겨찾기할 수 있습니다.");
                return;
            }

            var nextFavorite = !favoriteBookIds.Contains(book.Id);
            notebookRuntimeUseCase.SetUserBookFavorite(
                userBookId,
                nextFavorite,
                result =>
                {
                    SetLocalState(favoriteBookIds, book.Id, result?.Active ?? nextFavorite);
                    RefreshFavoriteBookButton(book);
                    ShowToast(favoriteBookIds.Contains(book.Id) ? $"즐겨찾기에 추가 · {book.Title}" : $"즐겨찾기 해제 · {book.Title}");
                },
                error =>
                {
                    UnityEngine.Debug.LogWarning($"Book favorite API failed. book={book.Id}, code={error?.Code}, message={FirstText(error?.Message, error?.Error)}");
                    ShowToast("즐겨찾기 API 요청에 실패했습니다.");
                });
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

            if (notebookRuntimeUseCase == null || !TryResolveBookApiId(book, out var bookId))
            {
                ShowToast("리뷰 API에 필요한 책 식별자가 없습니다.");
                return;
            }

            var rating = selectedReviewRating;
            notebookRuntimeUseCase.CreateBookReview(
                bookId,
                rating,
                content,
                review =>
                {
                    GetBookReviews(book).Insert(0, new BookReviewViewModel(
                        FirstText(review?.Author, "나"),
                        review?.Rating > 0 ? review.Rating : rating,
                        FirstText(review?.Content, content)));
                    if (bookReviewInput != null)
                    {
                        bookReviewInput.value = string.Empty;
                    }

                    showingAllBookReviews = true;
                    RefreshBookDetailPage(book);
                    SetBookReviewsOpen(true, true);
                    ShowToast("리뷰가 등록되었습니다.");
                },
                error =>
                {
                    UnityEngine.Debug.LogWarning($"Book review API failed. book={book.Id}, code={error?.Code}, message={FirstText(error?.Message, error?.Error)}");
                    ShowToast("리뷰 API 요청에 실패했습니다.");
                });
        }

        private IEnumerable<BookViewModel> FilterBooks(bool includeGenre)
        {
            var filtered = marketBooks
                .Where(MatchesSubTab)
                .Where(book => !includeGenre || MatchesGenre(book));

            return SortBooks(filtered);
        }

        private IEnumerable<BookViewModel> FilterRecommendedBooks()
        {
            var source = recommendedBooks.Length > 0 ? recommendedBooks : BuildRecommendedFallbackBooks();
            return source.Where(MatchesSubTab);
        }

        private BookViewModel[] BuildRecommendedFallbackBooks()
        {
            if (marketBooks.Length == 0)
            {
                return Array.Empty<BookViewModel>();
            }

            var ordered = marketBooks
                .OrderByDescending(book => book.Rating)
                .ThenByDescending(book => book.Reviews)
                .ThenBy(book => book.Title)
                .ToArray();
            var recommended = ordered
                .Where(book => book.Rating >= 4.6f)
                .Take(8)
                .ToArray();

            return recommended.Length > 0 ? recommended : ordered.Take(8).ToArray();
        }

        private void EnsureMarketBooksRequested()
        {
            if (marketBooksRequested || marketBooksLoading)
            {
                return;
            }

            marketBooksRequested = true;
            RequestMarketBooks();
        }

        private void EnsureRuntimeDataRequested()
        {
            if (runtimeDataRequested || runtimeDataLoading)
            {
                return;
            }

            runtimeDataRequested = true;
            RequestRuntimeData();
        }

        private void RequestRuntimeData()
        {
            runtimeRequestVersion++;
            var requestVersion = runtimeRequestVersion;
            runtimeDataLoading = true;
            apiSpaces = Array.Empty<SpaceViewModel>();
            apiDecorObjects = Array.Empty<DecorObjectViewModel>();

            if (notebookRuntimeUseCase == null)
            {
                runtimeDataLoading = false;
                UnityEngine.Debug.LogWarning("Notebook runtime API use case is not configured; non-book notebook panels will remain empty.");
                return;
            }

            RequestWallet(requestVersion);
            RequestSpaces(string.Empty);
            RequestAssets(requestVersion);
        }

        private void CompleteRuntimeRequest(int requestVersion)
        {
            if (requestVersion != runtimeRequestVersion)
            {
                return;
            }

            runtimeDataLoading = false;
            RefreshWallet();
            RefreshCommunityPanels();
            RefreshObjectMarket();
        }

        private void RequestWallet(int requestVersion)
        {
            notebookRuntimeUseCase.LoadWallet(
                wallet =>
                {
                    if (requestVersion != runtimeRequestVersion)
                    {
                        return;
                    }

                    pages = Mathf.Max(0, wallet?.BalancePages ?? 0);
                    RefreshWallet();
                },
                error => UnityEngine.Debug.LogWarning($"Notebook wallet API failed. code={error?.Code}, message={FirstText(error?.Message, error?.Error)}"));
        }

        private void SyncWalletFromApi()
        {
            if (notebookRuntimeUseCase == null)
            {
                return;
            }

            notebookRuntimeUseCase.LoadWallet(
                wallet =>
                {
                    pages = Mathf.Max(0, wallet?.BalancePages ?? pages);
                    RefreshWallet();
                },
                error => UnityEngine.Debug.LogWarning($"Notebook wallet sync API failed. code={error?.Code}, message={FirstText(error?.Message, error?.Error)}"));
        }

        private void RequestSpaces(string keyword, string cursor = null)
        {
            var requestVersion = ++otherSpacesRequestVersion;
            otherSpacesLoading = true;
            otherSpacesErrorMessage = string.Empty;
            appliedOtherSpacesQuery = string.IsNullOrWhiteSpace(keyword) ? string.Empty : keyword.Trim();
            otherSpacesNextCursor = cursor ?? string.Empty;
            RefreshCommunityPanels();

            if (notebookRuntimeUseCase == null)
            {
                otherSpacesLoading = false;
                apiSpaces = Array.Empty<SpaceViewModel>();
                otherSpacesErrorMessage = "공간 API가 준비되지 않았습니다.";
                RefreshCommunityPanels();
                UnityEngine.Debug.LogWarning("Notebook spaces API use case is not configured.");
                return;
            }

            notebookRuntimeUseCase.LoadSpaces(
                appliedOtherSpacesQuery,
                cursor,
                20,
                result =>
                {
                    if (requestVersion != otherSpacesRequestVersion)
                    {
                        return;
                    }

                    apiSpaces = MapRuntimeSpaces(result).ToArray();
                    otherSpacesNextCursor = result?.NextCursor ?? string.Empty;
                    otherSpacesLoading = false;
                    RefreshCommunityPanels();
                },
                error =>
                {
                    if (requestVersion == otherSpacesRequestVersion)
                    {
                        apiSpaces = Array.Empty<SpaceViewModel>();
                        otherSpacesLoading = false;
                        otherSpacesErrorMessage = string.IsNullOrWhiteSpace(appliedOtherSpacesQuery)
                            ? "인기 공간을 불러오지 못했습니다. 잠시 후 다시 시도해주세요."
                            : $"'{appliedOtherSpacesQuery}' 검색 요청에 실패했습니다. 잠시 후 다시 시도해주세요.";
                        RefreshCommunityPanels();
                    }

                    UnityEngine.Debug.LogWarning($"Notebook spaces API failed. code={error?.Code}, message={FirstText(error?.Message, error?.Error)}");
                    ShowToast("공간 목록 API 요청에 실패했습니다.");
                });
        }

        private void RequestAssets(int requestVersion)
        {
            notebookRuntimeUseCase.LoadAssets(
                NormalizeObjectCategoryForApi(activeObjectCategory),
                NormalizeObjectSortForApi(activeObjectSort),
                48,
                result =>
                {
                    if (requestVersion != runtimeRequestVersion)
                    {
                        return;
                    }

                    apiDecorObjects = MapRuntimeAssets(result).ToArray();
                    SyncAssetLocalState(result);
                    CompleteRuntimeRequest(requestVersion);
                },
                error =>
                {
                    if (requestVersion == runtimeRequestVersion)
                    {
                        apiDecorObjects = Array.Empty<DecorObjectViewModel>();
                        CompleteRuntimeRequest(requestVersion);
                    }

                    UnityEngine.Debug.LogWarning($"Notebook assets API failed. code={error?.Code}, message={FirstText(error?.Message, error?.Error)}");
                });
        }

        private void RequestRuntimeAssets()
        {
            runtimeRequestVersion++;
            var requestVersion = runtimeRequestVersion;
            apiDecorObjects = Array.Empty<DecorObjectViewModel>();
            runtimeDataLoading = true;
            RefreshObjectMarket();

            if (notebookRuntimeUseCase == null)
            {
                runtimeDataLoading = false;
                UnityEngine.Debug.LogWarning("Notebook runtime API use case is not configured; object market will remain empty.");
                RefreshObjectMarket();
                return;
            }

            RequestAssets(requestVersion);
        }

        private IEnumerable<SpaceViewModel> MapRuntimeSpaces(NotebookSpaceListResult result)
        {
            var spacesFromApi = result?.Spaces;
            if (spacesFromApi == null)
            {
                yield break;
            }

            for (var i = 0; i < spacesFromApi.Count; i++)
            {
                var item = spacesFromApi[i];
                if (item == null)
                {
                    continue;
                }

                var id = item.Id.ToString();
                var ownerId = item.OwnerUserId.ToString();
                SetLocalState(followedUserIds, ownerId, item.Following);
                SetLocalState(likedSpaceIds, id, item.Liked);

                yield return new SpaceViewModel(
                    id,
                    ownerId,
                    FirstText(item.OwnerName, $"User {item.OwnerUserId}"),
                    FirstText(item.SpaceName, $"Space {item.Id}"),
                    FirstText(item.Mood, "API · COMMUNITY"),
                    FirstText(item.Description, "API에서 불러온 공간입니다."),
                    "api-space",
                    FirstText(item.ThumbnailUrl, string.Empty),
                    ClampToInt(item.FollowerCount),
                    ClampToInt(item.LikeCount),
                    ClampToInt(item.VisitCount),
                    SearchCoverColor(i, id),
                    item.IsOwnSpace);
            }
        }

        private IEnumerable<UserViewModel> MapRuntimeUsers(NotebookUserListResult result)
        {
            var users = result?.Users;
            if (users == null)
            {
                yield break;
            }

            foreach (var item in users)
            {
                if (item == null || item.Id <= 0L)
                {
                    continue;
                }

                var userId = item.Id.ToString();
                if (item.Following)
                {
                    followedUserIds.Add(userId);
                }
                else
                {
                    followedUserIds.Remove(userId);
                }

                yield return new UserViewModel(
                    item.Id,
                    FirstText(item.Nickname, $"User {item.Id}"),
                    item.ProfileImageUrl,
                    item.Mutual,
                    item.Following,
                    item.Blocked);
            }
        }

        private IEnumerable<DecorObjectViewModel> MapRuntimeAssets(NotebookAssetListResult result)
        {
            var assets = result?.Assets;
            if (assets == null)
            {
                yield break;
            }

            for (var i = 0; i < assets.Count; i++)
            {
                var item = assets[i];
                if (item == null)
                {
                    continue;
                }

                var id = item.Id.ToString();
                var category = ResolveObjectCategory(item);
                var description = FirstText(item.Tier, "BASIC") + " · " + FirstText(item.AssetType, "OBJECT");
                yield return new DecorObjectViewModel(
                    id,
                    FirstText(item.Name, item.Code, $"Object {item.Id}"),
                    category,
                    ResolveObjectPreviewImageUrl(item),
                    description,
                    Mathf.Max(0, item.PricePages),
                    Mathf.Max(0, item.Popularity),
                    assets.Count - i,
                    SearchCoverColor(i, id),
                    category == ObjectCategories[0] ? Accent : AccentDeep);
            }
        }

        private void SyncAssetLocalState(NotebookAssetListResult result)
        {
            var assets = result?.Assets;
            if (assets == null)
            {
                return;
            }

            foreach (var item in assets)
            {
                if (item == null)
                {
                    continue;
                }

                var id = item.Id.ToString();
                SetLocalState(ownedObjectIds, id, item.Owned);
                SetLocalState(wishedObjectIds, id, item.Wished);
            }
        }

        private static string ResolveObjectCategory(NotebookAssetItem item)
        {
            var value = FirstText(item?.CategoryCode, item?.Category, item?.AssetType, item?.Tier, string.Empty);
            if (string.IsNullOrWhiteSpace(value))
            {
                return ObjectCategories[0];
            }

            if (value.IndexOf("THEME", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ObjectCategories.Length > 3 ? ObjectCategories[3] : ObjectCategories[0];
            }

            if (value.IndexOf("BOOKSHELF", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("required", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("book", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ObjectCategories.Length > 1 ? ObjectCategories[1] : ObjectCategories[0];
            }

            return ObjectCategories.Length > 2 ? ObjectCategories[2] : ObjectCategories[0];
        }

        private static string ResolveObjectPreviewImageUrl(NotebookAssetItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            var remoteUrl = FirstText(item.PreviewImageUrl);
            if (!string.IsNullOrEmpty(remoteUrl))
            {
                return remoteUrl;
            }

            return ResolveLocalCustomizationPreviewPath(item);
        }

        private static string ResolveLocalCustomizationPreviewPath(NotebookAssetItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            var candidates = new List<string>();
            var id = item.Id > 0L ? item.Id.ToString() : string.Empty;
            var typeKey = NormalizePreviewKey(item.AssetType);
            if (!string.IsNullOrEmpty(id))
            {
                if (typeKey.Contains("bookshelf"))
                {
                    candidates.Add($"BookshelfSkin_{id}");
                }
                else if (typeKey.Contains("dashboard"))
                {
                    candidates.Add($"DashboardSkin_{id}");
                }
                else if (typeKey.Contains("desk"))
                {
                    candidates.Add($"Desk_{id}");
                }
                else if (typeKey.Contains("wallpaper"))
                {
                    candidates.Add($"Wallpaper_{id}");
                }
                else if (typeKey.Contains("floor"))
                {
                    candidates.Add($"Floor_{id}");
                }
                else if (typeKey.Contains("interiorlight"))
                {
                    candidates.Add($"InteriorLight_{id}");
                }
                else if (typeKey.Contains("exteriorlight"))
                {
                    candidates.Add($"ExteriorLight_{id}");
                }
                else if (typeKey.Contains("notebook"))
                {
                    candidates.Add($"Notebook_{id}");
                }
            }

            var resourceStem = PreviewStemFromResourceKey(item.UnityResourceKey);
            if (!string.IsNullOrEmpty(resourceStem))
            {
                candidates.Add($"Placement_{resourceStem}");
                candidates.Add(resourceStem);
            }

            var codeStem = PreviewStemFromCode(item.Code);
            if (!string.IsNullOrEmpty(codeStem))
            {
                candidates.Add($"Placement_{codeStem}");
                candidates.Add(codeStem);
            }

            var nameStem = PreviewStemFromCode(item.Name);
            if (!string.IsNullOrEmpty(nameStem))
            {
                candidates.Add($"Placement_{nameStem}");
                candidates.Add(nameStem);
            }

            return FindCustomizationPreviewPath(candidates);
        }

        private static string PreviewStemFromResourceKey(string resourceKey)
        {
            var value = FirstText(resourceKey);
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var hashIndex = value.IndexOf('#');
            if (hashIndex >= 0)
            {
                value = value.Substring(hashIndex + 1);
            }

            value = value.Replace('\\', '/');
            var slashIndex = value.LastIndexOf('/');
            if (slashIndex >= 0 && slashIndex < value.Length - 1)
            {
                value = value.Substring(slashIndex + 1);
            }

            var extensionIndex = value.LastIndexOf('.');
            if (extensionIndex > 0)
            {
                value = value.Substring(0, extensionIndex);
            }

            return PreviewStemFromCode(value);
        }

        private static string PreviewStemFromCode(string value)
        {
            value = FirstText(value);
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var chars = new List<char>(value.Length);
            var previousWasSeparator = false;
            foreach (var c in value)
            {
                if (char.IsLetterOrDigit(c))
                {
                    chars.Add(char.ToLowerInvariant(c));
                    previousWasSeparator = false;
                }
                else if (!previousWasSeparator && chars.Count > 0)
                {
                    chars.Add('_');
                    previousWasSeparator = true;
                }
            }

            if (chars.Count > 0 && chars[chars.Count - 1] == '_')
            {
                chars.RemoveAt(chars.Count - 1);
            }

            return new string(chars.ToArray());
        }

        private static string NormalizePreviewKey(string value)
        {
            value = FirstText(value);
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var chars = new List<char>(value.Length);
            foreach (var c in value)
            {
                if (char.IsLetterOrDigit(c))
                {
                    chars.Add(char.ToLowerInvariant(c));
                }
            }

            return new string(chars.ToArray());
        }

        private static string FindCustomizationPreviewPath(IEnumerable<string> candidates)
        {
            if (candidates == null)
            {
                return string.Empty;
            }

            EnsureCustomizationPreviewSpriteMap();
            foreach (var candidate in candidates)
            {
                var key = NormalizePreviewKey(candidate);
                if (!string.IsNullOrEmpty(key) &&
                    customizationPreviewSpriteByKey != null &&
                    customizationPreviewSpriteByKey.ContainsKey(key))
                {
                    return CustomizationPreviewUrlPrefix + key;
                }
            }
            return string.Empty;
        }

        private static void EnsureCustomizationPreviewSpriteMap()
        {
            if (customizationPreviewSpriteByKey != null)
            {
                return;
            }

            customizationPreviewSpriteByKey = new Dictionary<string, Sprite>();
            var sprites = Resources.LoadAll<Sprite>(CustomizationPreviewResourceRoot);
            foreach (var sprite in sprites)
            {
                if (sprite == null)
                {
                    continue;
                }

                var key = NormalizePreviewKey(sprite.name);
                if (!string.IsNullOrEmpty(key) && !customizationPreviewSpriteByKey.ContainsKey(key))
                {
                    customizationPreviewSpriteByKey.Add(key, sprite);
                }
            }

#if UNITY_EDITOR
            if (customizationPreviewSpriteByKey.Count == 0 &&
                UnityEditor.AssetDatabase.IsValidFolder(CustomizationPreviewAssetRoot))
            {
                var guids = UnityEditor.AssetDatabase.FindAssets("t:Texture2D", new[] { CustomizationPreviewAssetRoot });
                foreach (var guid in guids)
                {
                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite == null)
                    {
                        continue;
                    }

                    var key = NormalizePreviewKey(sprite.name);
                    if (!string.IsNullOrEmpty(key) && !customizationPreviewSpriteByKey.ContainsKey(key))
                    {
                        customizationPreviewSpriteByKey.Add(key, sprite);
                    }
                }
            }
#endif
        }

        private static bool TryGetCustomizationPreviewSprite(string previewUrl, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrWhiteSpace(previewUrl) ||
                !previewUrl.StartsWith(CustomizationPreviewUrlPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var key = previewUrl.Substring(CustomizationPreviewUrlPrefix.Length);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            EnsureCustomizationPreviewSpriteMap();
            return customizationPreviewSpriteByKey != null &&
                   customizationPreviewSpriteByKey.TryGetValue(key, out sprite) &&
                   sprite != null;
        }

        private static string NormalizeObjectCategoryForApi(string category)
        {
            if (string.IsNullOrWhiteSpace(category) || category == ObjectCategories[0])
            {
                return string.Empty;
            }

            if (ObjectCategories.Length > 3 && category == ObjectCategories[3])
            {
                return "THEME";
            }

            if (ObjectCategories.Length > 1 && category == ObjectCategories[1])
            {
                return "BOOKSHELF";
            }

            return "INTERIOR";
        }

        private static string NormalizeObjectSortForApi(string sort)
        {
            if (string.IsNullOrWhiteSpace(sort))
            {
                return "popular";
            }

            if (ObjectSorts.Length > 1 && sort == ObjectSorts[1])
            {
                return "latest";
            }

            if (ObjectSorts.Length > 2 && sort == ObjectSorts[2])
            {
                return "price";
            }

            return "popular";
        }

        private static int ClampToInt(long value)
        {
            if (value <= 0)
            {
                return 0;
            }

            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        private static void SetLocalState(HashSet<string> set, string key, bool active)
        {
            if (set == null || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (active)
            {
                set.Add(key);
            }
            else
            {
                set.Remove(key);
            }
        }

        private void RequestMarketBooks()
        {
            marketBooksRequestVersion++;
            var requestVersion = marketBooksRequestVersion;
            marketBooksLoading = true;
            marketBooks = Array.Empty<BookViewModel>();
            recommendedBooks = Array.Empty<BookViewModel>();

            if (loadNotebookMarketBooksUseCase == null)
            {
                marketBooksLoading = false;
                UnityEngine.Debug.LogWarning("Notebook market API use case is not configured; market sections will remain empty.");
                return;
            }

            try
            {
                loadNotebookMarketBooksUseCase.Execute(
                    "RANKING",
                    MarketBookPageSize,
                    response =>
                    {
                        if (requestVersion != marketBooksRequestVersion)
                        {
                            return;
                        }

                        marketBooksLoading = false;
                        marketBooks = MapApiSearchResults(response, string.Empty);
                        RequestRecommendedBooks(requestVersion);
                        selectedBookOverride = null;
                        selectedBookIndex = 0;
                        UnityEngine.Debug.Log($"Notebook market API loaded {marketBooks.Length} item(s).");
                        RefreshAll();
                    },
                    error =>
                    {
                        if (requestVersion != marketBooksRequestVersion)
                        {
                            return;
                        }

                        marketBooksLoading = false;
                        marketBooks = Array.Empty<BookViewModel>();
                        recommendedBooks = Array.Empty<BookViewModel>();
                        UnityEngine.Debug.LogWarning($"Notebook market API failed. code={error?.Code}, message={FirstText(error?.Message, error?.Error)}");
                        RefreshAll();
                    });
            }
            catch (Exception exception)
            {
                marketBooksLoading = false;
                marketBooks = Array.Empty<BookViewModel>();
                recommendedBooks = Array.Empty<BookViewModel>();
                UnityEngine.Debug.LogWarning("Notebook market API failed before send.");
                UnityEngine.Debug.LogException(exception);
            }
        }

        private void RequestRecommendedBooks(int requestVersion)
        {
            try
            {
                loadNotebookMarketBooksUseCase.Execute(
                    "RECOMMENDED",
                    8,
                    response =>
                    {
                        if (requestVersion != marketBooksRequestVersion)
                        {
                            return;
                        }

                        var apiRecommendedBooks = MapApiSearchResults(response, string.Empty);
                        recommendedBooks = apiRecommendedBooks.Length > 0
                            ? apiRecommendedBooks
                            : BuildRecommendedFallbackBooks();
                        if (apiRecommendedBooks.Length == 0)
                        {
                            UnityEngine.Debug.LogWarning("Notebook recommended API returned no items; using rating/review fallback.");
                        }

                        UnityEngine.Debug.Log($"Notebook recommended API loaded {recommendedBooks.Length} item(s).");
                        RefreshAll();
                    },
                    error =>
                    {
                        if (requestVersion != marketBooksRequestVersion)
                        {
                            return;
                        }

                        recommendedBooks = BuildRecommendedFallbackBooks();
                        UnityEngine.Debug.LogWarning($"Notebook recommended API failed. code={error?.Code}, message={FirstText(error?.Message, error?.Error)}");
                        RefreshAll();
                    });
            }
            catch (Exception exception)
            {
                recommendedBooks = BuildRecommendedFallbackBooks();
                UnityEngine.Debug.LogWarning("Notebook recommended API failed before send.");
                UnityEngine.Debug.LogException(exception);
                RefreshAll();
            }
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
                if (searchNotebookBooksUseCase == null)
                {
                    CompleteSearchWithApiError(requestVersion, "Notebook API disabled.");
                    return;
                }

                searchNotebookBooksUseCase.Execute(
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
                        if (IsApiDisabledError(error))
                        {
                            UnityEngine.Debug.LogWarning($"Search API disabled. keyword={keyword}");
                            CompleteSearchWithApiError(requestVersion, "Notebook API disabled.");
                            return;
                        }

                        UnityEngine.Debug.LogWarning($"Search API failed. keyword={keyword}, code={error?.Code}, message={FirstText(error?.Message, error?.Error)}");
                        CompleteSearchWithApiError(requestVersion, "검색 API 응답을 받지 못했습니다.");
                    });
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"Search API failed. keyword={keyword}");
                UnityEngine.Debug.LogException(exception);
                CompleteSearchWithApiError(requestVersion, "검색 API 응답을 받지 못했습니다.");
            }
        }

        private void CompleteSearchWithApiError(int requestVersion, string message)
        {
            if (requestVersion != searchRequestVersion)
            {
                return;
            }

            searchLoading = false;
            searchResults = Array.Empty<BookViewModel>();
            ShowToast(message);
            RefreshAll();
        }

        private static bool IsApiDisabledError(NotebookApiError error)
        {
            return string.Equals(error?.Code, "NOTEBOOK_API_DISABLED", StringComparison.OrdinalIgnoreCase);
        }

        private BookViewModel[] MapApiSearchResults(NotebookBookSearchResult response, string keyword)
        {
            var items = response?.Items;
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
                SetLocalState(addedBookIds, id, dto.Owned);
                if (dto.UserBookId.HasValue && dto.UserBookId.Value > 0)
                {
                    bookUserBookIds[id] = dto.UserBookId.Value;
                    SetLocalState(favoriteBookIds, id, false);
                }

                var rating = dto.AvgRating > 0 ? Mathf.Clamp((float)dto.AvgRating, 0f, 5f) : 0f;
                var reviews = Mathf.Max(0, dto.ReviewCount);
                var views = Mathf.Max(0, dto.ViewCount);
                var records = Mathf.Max(dto.RecordCount, dto.PlacementCount);
                var price = dto.PricePages > 0 ? dto.PricePages : dto.HasPaidCover ? 1200 : 0;
                var coverColor = SearchCoverColor(i, id);
                var status = dto.Owned && Categories.Length > 2 ? Categories[2] : Categories.Length > 3 ? Categories[3] : string.Empty;
                var shelf = Categories.Length > 4 ? Categories[4] : string.Empty;

                mapped.Add(new BookViewModel(
                    id,
                    title,
                    author,
                    publisher,
                    summary,
                    genre,
                    status,
                    shelf,
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
                    dto.PublishedDate,
                    dto.UserBookId));
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
            selectedBookIndex = Mathf.Max(0, Array.IndexOf(marketBooks, selectedBook));
            selectedBookOverride = null;
            return selectedBook;
        }

        private BookViewModel GetSelectedBook()
        {
            if (selectedBookOverride != null)
            {
                return selectedBookOverride;
            }

            return selectedBookIndex >= 0 && selectedBookIndex < marketBooks.Length
                ? marketBooks[selectedBookIndex]
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
            card.style.width = 184;
            card.style.minWidth = 176;
            card.style.maxWidth = 214;
            card.style.flexBasis = 184;
            card.style.flexGrow = 1f;
            card.style.height = 302;
            card.style.marginRight = 10;
            card.style.marginBottom = 12;
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.paddingTop = 10;
            card.style.paddingBottom = 10;
            card.style.alignItems = Align.Center;
            card.style.flexDirection = FlexDirection.Column;
            card.style.backgroundColor = index % 2 == 0 ? Rgba(0xFF, 0xFF, 0xFF, 0.94f) : Rgba(0xF4, 0xFB, 0xFF, 0.92f);
            card.style.overflow = Overflow.Hidden;
            Round(card, 14);
            Border(card, index == 0 ? Rgba(0x58, 0xB9, 0xFF, 0.36f) : Rgba(0x0E, 0x1D, 0x2A, 0.08f));
            card.RegisterCallback<ClickEvent>(_ => SelectBook(book));

            var coverStage = new VisualElement { name = "search-result-cover-stage" };
            coverStage.style.width = Length.Percent(100);
            coverStage.style.height = 122;
            coverStage.style.flexShrink = 0;
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
            top.style.flexShrink = 0;
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
            title.style.minHeight = 54;
            title.style.maxHeight = 58;
            title.style.overflow = Overflow.Hidden;
            title.style.flexShrink = 0;
            title.style.unityTextAlign = TextAnchor.UpperCenter;
            info.Add(title);

            var meta = Text($"{book.Author} · {book.Publisher}", 10, SoftInk, FontStyle.Normal);
            meta.style.fontSize = 9;
            meta.style.marginTop = 3;
            meta.style.whiteSpace = WhiteSpace.Normal;
            meta.style.minHeight = 34;
            meta.style.maxHeight = 38;
            meta.style.overflow = Overflow.Hidden;
            meta.style.flexShrink = 0;
            meta.style.unityTextAlign = TextAnchor.UpperCenter;
            info.Add(meta);

            var stats = Text($"★ {book.Rating:0.0} · 리뷰 {book.Reviews:N0} · 조회 {book.Views:N0}", 9, Ink, FontStyle.Normal);
            stats.style.marginTop = 6;
            stats.style.height = 20;
            stats.style.width = Length.Percent(100);
            stats.style.overflow = Overflow.Hidden;
            stats.style.flexShrink = 0;
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
            var source = filtered.Take(8);

            foreach (var book in source)
            {
                recommendList.Add(CreateRecommendCard(book));
            }

            if (recommendList.childCount == 0)
            {
                recommendList.Add(EmptyState(marketBooksLoading ? "책 시장 API를 불러오는 중입니다." : "조건에 맞는 추천 책이 없습니다."));
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
                rankingGrid.Add(EmptyState(marketBooksLoading ? "책 시장 API를 불러오는 중입니다." : "검색 결과가 없습니다."));
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
            card.style.marginBottom = 16;
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
            info.style.paddingTop = 0;
            card.Add(info);

            var badgeRow = Row("badges");
            badgeRow.style.height = 22;
            badgeRow.style.marginTop = -2;
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
            title.style.marginTop = 2;
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.minHeight = 21;
            title.style.maxHeight = 28;
            info.Add(title);

            var byline = Text($"{book.Author} · {book.Publisher}", 10, SoftInk, FontStyle.Normal);
            byline.style.marginTop = 0;
            byline.style.minHeight = 15;
            byline.style.whiteSpace = WhiteSpace.Normal;
            info.Add(byline);

            var stats = Row("ranking-stats");
            stats.style.marginTop = 2;
            stats.style.minHeight = 16;
            stats.style.alignItems = Align.Center;
            info.Add(stats);

            var rating = Text($"★ {book.Rating:0.0}", 9, AccentDeep, FontStyle.Normal);
            rating.style.marginRight = 7;
            stats.Add(rating);

            var reviews = Text($"리뷰 {book.Reviews:N0}", 9, Ink, FontStyle.Normal);
            reviews.style.marginRight = 7;
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

            var remoteUrl = string.IsNullOrWhiteSpace(book.CoverImageUrl) ? string.Empty : book.CoverImageUrl.Trim();
            cover.userData = remoteUrl;
            if (!string.IsNullOrEmpty(remoteUrl) && TryGetCachedBookCoverTexture(remoteUrl, out var cached))
            {
                ApplyBookCoverTexture(cover, cached);
                return cover;
            }

            BuildGeneratedBookCover(cover, book, width, height, includeText);
            if (!string.IsNullOrEmpty(remoteUrl))
            {
                StartCoroutine(LoadBookCoverTexture(remoteUrl, cover));
            }

            return cover;
        }

        private void BuildGeneratedBookCover(VisualElement cover, BookViewModel book, float width, float height, bool includeText)
        {
            cover.Q<VisualElement>("book-cover-generated")?.RemoveFromHierarchy();
            var generated = new VisualElement { name = "book-cover-generated" };
            generated.pickingMode = PickingMode.Ignore;
            generated.style.position = Position.Absolute;
            generated.style.left = 0;
            generated.style.right = 0;
            generated.style.top = 0;
            generated.style.bottom = 0;
            cover.Add(generated);

            var sheen = new VisualElement();
            sheen.style.position = Position.Absolute;
            sheen.style.left = 0;
            sheen.style.top = 0;
            sheen.style.width = width * 0.38f;
            sheen.style.height = height;
            sheen.style.backgroundColor = Rgba(0xFF, 0xFF, 0xFF, 0.10f);
            generated.Add(sheen);

            var bandTop = height * 0.34f;
            var bandHeight = height * 0.24f;

            var band = new VisualElement();
            band.style.position = Position.Absolute;
            band.style.left = 0;
            band.style.right = 0;
            band.style.top = bandTop;
            band.style.height = bandHeight;
            band.style.backgroundColor = book.AccentColor;
            band.style.opacity = 0.30f;
            generated.Add(band);

            var spine = new VisualElement();
            spine.style.position = Position.Absolute;
            spine.style.left = 0;
            spine.style.top = 0;
            spine.style.width = Mathf.Max(6, width * 0.08f);
            spine.style.height = height;
            spine.style.backgroundColor = Rgba(0x00, 0x00, 0x00, 0.22f);
            generated.Add(spine);

            if (includeText)
            {
                var title = Text(book.Title, width >= 100 ? 12 : 10, Surface, FontStyle.Bold);
                title.style.position = Position.Absolute;
                title.style.left = 12;
                title.style.right = 12;
                title.style.top = bandTop;
                title.style.height = Mathf.Max(38, bandHeight);
                title.style.whiteSpace = WhiteSpace.Normal;
                title.style.maxHeight = Mathf.Max(38, bandHeight);
                title.style.unityTextAlign = TextAnchor.MiddleCenter;
                generated.Add(title);

                var author = Text(book.Author.ToUpperInvariant(), 7, Rgba(0xF7, 0xFF, 0xF7, 0.78f), FontStyle.Normal);
                author.style.position = Position.Absolute;
                author.style.left = 10;
                author.style.right = 8;
                author.style.bottom = 9;
                author.style.maxHeight = 14;
                generated.Add(author);
            }
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
            var index = Array.IndexOf(marketBooks, book);
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

        private bool TryOpenPendingAdvertisedBookDetail(bool showLoadingToast)
        {
            if (!pendingAdvertisedBookOpen || !visible || !initialized)
            {
                return pendingAdvertisedBookOpen;
            }

            var book = ResolveAdvertisedBook(pendingAdvertisedBookTitle);
            if (book == null)
            {
                EnsureMarketBooksRequested();
                if (showLoadingToast)
                {
                    ShowToast("광고 도서를 불러오는 중입니다.");
                }

                return true;
            }

            pendingAdvertisedBookOpen = false;
            pendingAdvertisedBookTitle = string.Empty;
            activeHeaderTab = HeaderTabs[0];
            activeSubTab = "전체 탐색";
            searchPageOpen = false;
            pageIndex = 0;
            SetBookReviewsOpen(false, false);
            SelectBook(book);
            return true;
        }

        private BookViewModel ResolveAdvertisedBook(string preferredTitle)
        {
            var normalizedTitle = NormalizeText(preferredTitle);
            var candidates = recommendedBooks
                .Concat(marketBooks)
                .Concat(searchResults)
                .Where(book => book != null)
                .ToArray();

            if (candidates.Length == 0)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(normalizedTitle))
            {
                var exact = candidates.FirstOrDefault(book => NormalizeText(book.Title) == normalizedTitle);
                if (exact != null)
                {
                    return exact;
                }

                var contains = candidates.FirstOrDefault(book =>
                    NormalizeText(book.Title).Contains(normalizedTitle) ||
                    normalizedTitle.Contains(NormalizeText(book.Title)));
                if (contains != null)
                {
                    return contains;
                }
            }

            return recommendedBooks.FirstOrDefault(book => book != null) ??
                   marketBooks.FirstOrDefault(book => book != null) ??
                   searchResults.FirstOrDefault(book => book != null);
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
            if (tab != HeaderTabs[1])
            {
                CloseSocialListPanel();
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

            var index = Array.IndexOf(marketBooks, book);
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

            if (!CanUseShelfApi(book))
            {
                ShowToast("책장 추가 API에 필요한 책 식별자가 없습니다.");
                return;
            }

            AddBookToShelfThroughApi(book);
        }

        private bool CanUseShelfApi(BookViewModel book)
        {
            if (book == null)
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
            if (addNotebookBookToShelfUseCase == null || notebookRuntimeUseCase == null)
            {
                ShowToast("책장 추가 API가 설정되지 않았습니다.");
                return;
            }

            if (shelfTargetLoading || pendingShelfBookIds.Count > 0)
            {
                ShowToast("책장 추가 요청을 처리 중입니다.");
                return;
            }

            pendingShelfBookIds.Add(book.Id);
            RefreshLists();
            RefreshBookDetailPage(book);

            shelfTargetLoading = true;
            notebookRuntimeUseCase.LoadDefaultShelfTarget(
                target =>
                {
                    shelfTargetLoading = false;
                    SendBookAddThroughApi(book, target);
                },
                error =>
                {
                    shelfTargetLoading = false;
                    pendingShelfBookIds.Remove(book.Id);
                    UnityEngine.Debug.LogWarning($"Default shelf placement API failed. code={error?.Code}, message={FirstText(error?.Message, error?.Error)}");
                    RefreshLists();
                    RefreshBookDetailPage(book);
                    ShowToast("기본 책장 정보를 불러오지 못했습니다.");
                });
        }

        private void SendBookAddThroughApi(BookViewModel book, NotebookShelfTarget target)
        {
            try
            {
                addNotebookBookToShelfUseCase.Execute(
                    target.BookshelfId,
                    BuildAddCommand(book),
                    BuildShelfPlacement(target),
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

        private AddNotebookBookToShelfCommand BuildAddCommand(BookViewModel book)
        {
            return new AddNotebookBookToShelfCommand
            {
                Source = book.ApiSource,
                BookId = book.ServerBookId.GetValueOrDefault(),
                Isbn13 = book.Isbn13,
                Isbn10 = book.Isbn10,
                Title = book.Title,
                Author = book.Author,
                Publisher = book.Publisher,
                PublishedDate = book.PublishedDate,
                Description = book.Summary,
                CoverImageUrl = book.CoverImageUrl,
                SourceUrl = book.SourceUrl
            };
        }

        private static NotebookShelfPlacement BuildShelfPlacement(NotebookShelfTarget target)
        {
            return new NotebookShelfPlacement(
                target.SectionId,
                Mathf.Max(1, target.SlotNo),
                DefaultDisplayStyle);
        }

        private void CompleteApiBookAdded(BookViewModel book, NotebookBookAddResult response)
        {
            pendingShelfBookIds.Remove(book.Id);
            if (response != null && response.Id > 0)
            {
                bookUserBookIds[book.Id] = response.Id;
            }

            ApplyBookAddedFromApi(book);
            SyncWalletFromApi();
            UnityEngine.Debug.Log($"Book added to shelf through API. book={book.Id}, userBookId={response?.Id}");
            NotifyBookAddedToShelfThroughApi();
        }

        private void NotifyBookAddedToShelfThroughApi()
        {
            var handlers = BookAddedToShelfThroughApi;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action)handler)();
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception);
                }
            }
        }

        private void FailApiBookAdd(BookViewModel book, NotebookApiError error)
        {
            pendingShelfBookIds.Remove(book.Id);
            UnityEngine.Debug.LogWarning($"Book shelf API failed. book={book.Id}, code={error?.Code}, message={FirstText(error?.Message, error?.Error)}");
            RefreshLists();
            RefreshBookDetailPage(book);
            ShowToast("서버 책장 추가에 실패했습니다. 잠시 후 다시 시도해주세요.");
        }

        private void ApplyBookAddedFromApi(BookViewModel book)
        {
            if (book == null)
            {
                return;
            }

            var index = Array.IndexOf(marketBooks, book);
            if (index >= 0)
            {
                selectedBookIndex = index;
            }

            if (addedBookIds.Contains(book.Id))
            {
                ShowToast($"이미 내 책장에 있어요 · {book.Title}");
                return;
            }

            addedBookIds.Add(book.Id);
            RefreshLists();
            RefreshBookDetailPage(book);
            ShowToast(book.Price > 0 ? $"구매 완료 · {book.Title}" : $"내 책장에 추가됨 · {book.Title}");
        }

        // (구) ChargePagesThroughApi(int amount) — deprecated `/v1/payments/page-charge` (BE P006) 호출.
        // 새 결제 흐름은 OpenPaymentChargeOverlay → CreateChargeOrder → 외부 브라우저 + PendingOverlay 폴링.
        // 새 메서드는 본 파일 하단의 Payment 통합 영역 참조.

        private void ToggleSpaceFollowThroughApi(SpaceViewModel space, Button button, Button followersButton = null)
        {
            if (space == null || notebookRuntimeUseCase == null || !TryParseApiId(space.OwnerId, out var userId))
            {
                ShowToast("팔로우 API에 필요한 사용자 식별자가 없습니다.");
                return;
            }

            if (space.IsOwnSpace)
            {
                ShowToast("내 공간은 팔로우할 수 없습니다.");
                return;
            }

            var wasFollowing = followedUserIds.Contains(space.OwnerId);
            var nextFollowing = !followedUserIds.Contains(space.OwnerId);
            SetLocalState(followedUserIds, space.OwnerId, nextFollowing);
            RefreshFollowButton(button, space.OwnerId);
            button?.SetEnabled(false);
            notebookRuntimeUseCase.FollowUser(
                userId,
                nextFollowing,
                result =>
                {
                    SetLocalState(followedUserIds, space.OwnerId, result?.Active ?? nextFollowing);
                    space.Followers = result != null
                        ? ClampToInt(result.Count)
                        : Mathf.Max(0, space.Followers + (followedUserIds.Contains(space.OwnerId) == wasFollowing ? 0 : followedUserIds.Contains(space.OwnerId) ? 1 : -1));
                    RefreshFollowButton(button, space.OwnerId);
                    RefreshFollowerMetric(followersButton, space);
                    button?.SetEnabled(true);
                    ShowToast(followedUserIds.Contains(space.OwnerId) ? $"{space.Owner}님을 팔로우했습니다." : $"{space.Owner}님 팔로우를 해제했습니다.");
                },
                error =>
                {
                    SetLocalState(followedUserIds, space.OwnerId, wasFollowing);
                    button?.SetEnabled(true);
                    RefreshFollowButton(button, space.OwnerId);
                    RefreshFollowerMetric(followersButton, space);
                    UnityEngine.Debug.LogWarning($"Space follow API failed. user={space.OwnerId}, code={error?.Code}, message={FirstText(error?.Message, error?.Error)}");
                    ShowToast("팔로우 API 요청에 실패했습니다.");
                });
        }

        private void ToggleUserFollowThroughApi(UserViewModel user, Button button)
        {
            if (user == null || notebookRuntimeUseCase == null)
            {
                ShowToast("팔로우 API에 필요한 사용자 정보가 없습니다.");
                return;
            }

            var userKey = user.Id.ToString();
            var wasFollowing = followedUserIds.Contains(userKey) || user.Following;
            var nextFollowing = !wasFollowing;
            user.Following = nextFollowing;
            SetLocalState(followedUserIds, userKey, nextFollowing);
            RefreshSocialFollowButton(button, user);
            button?.SetEnabled(false);

            notebookRuntimeUseCase.FollowUser(
                user.Id,
                nextFollowing,
                result =>
                {
                    user.Following = result?.Active ?? nextFollowing;
                    SetLocalState(followedUserIds, userKey, user.Following);
                    RefreshSocialFollowButton(button, user);
                    button?.SetEnabled(true);
                    RefreshCommunityPanels();
                },
                error =>
                {
                    user.Following = wasFollowing;
                    SetLocalState(followedUserIds, userKey, wasFollowing);
                    RefreshSocialFollowButton(button, user);
                    button?.SetEnabled(true);
                    UnityEngine.Debug.LogWarning($"User follow API failed. user={user.Id}, code={error?.Code}, message={FirstText(error?.Message, error?.Error)}");
                    ShowToast("팔로우 API 요청에 실패했습니다.");
                });
        }

        private void ToggleSpaceLikeThroughApi(SpaceViewModel space, Button button)
        {
            if (space == null || notebookRuntimeUseCase == null || !TryParseApiId(space.Id, out var spaceId))
            {
                ShowToast("공간 좋아요 API에 필요한 공간 식별자가 없습니다.");
                return;
            }

            var wasLiked = likedSpaceIds.Contains(space.Id);
            var nextLiked = !likedSpaceIds.Contains(space.Id);
            SetLocalState(likedSpaceIds, space.Id, nextLiked);
            space.Likes = Mathf.Max(0, space.Likes + (nextLiked ? 1 : -1));
            RefreshSpaceLikeButton(button, space);
            button?.SetEnabled(false);
            notebookRuntimeUseCase.LikeSpace(
                spaceId,
                nextLiked,
                result =>
                {
                    SetLocalState(likedSpaceIds, space.Id, result?.Active ?? nextLiked);
                    if (result != null && result.Count >= 0L)
                    {
                        space.Likes = ClampToInt(result.Count);
                    }
                    RefreshSpaceLikeButton(button, space);
                    button?.SetEnabled(true);
                    ShowToast(likedSpaceIds.Contains(space.Id) ? $"공간을 좋아요했습니다 · {space.Title}" : $"공간 좋아요를 취소했습니다 · {space.Title}");
                },
                error =>
                {
                    SetLocalState(likedSpaceIds, space.Id, wasLiked);
                    space.Likes = Mathf.Max(0, space.Likes + (wasLiked == nextLiked ? 0 : wasLiked ? 1 : -1));
                    button?.SetEnabled(true);
                    RefreshSpaceLikeButton(button, space);
                    UnityEngine.Debug.LogWarning($"Space like API failed. space={space.Id}, code={error?.Code}, message={FirstText(error?.Message, error?.Error)}");
                    ShowToast("공간 좋아요 API 요청에 실패했습니다.");
                });
        }

        private void ToggleObjectWishThroughApi(DecorObjectViewModel item, Button button)
        {
            if (item == null || notebookRuntimeUseCase == null || !TryParseApiId(item.Id, out var assetId))
            {
                ShowToast("찜 API에 필요한 오브젝트 식별자가 없습니다.");
                return;
            }

            var nextWished = !wishedObjectIds.Contains(item.Id);
            button?.SetEnabled(false);
            notebookRuntimeUseCase.WishAsset(
                assetId,
                nextWished,
                result =>
                {
                    SetLocalState(wishedObjectIds, item.Id, result?.Active ?? nextWished);
                    RefreshToggleButton(button, wishedObjectIds.Contains(item.Id), "찜 해제", "찜하기");
                    button?.SetEnabled(true);
                    ShowToast(wishedObjectIds.Contains(item.Id) ? $"위시리스트에 추가 · {item.Name}" : $"위시리스트에서 제거 · {item.Name}");
                },
                error =>
                {
                    button?.SetEnabled(true);
                    RefreshToggleButton(button, wishedObjectIds.Contains(item.Id), "찜 해제", "찜하기");
                    UnityEngine.Debug.LogWarning($"Object wish API failed. asset={item.Id}, code={error?.Code}, message={FirstText(error?.Message, error?.Error)}");
                    ShowToast("찜 API 요청에 실패했습니다.");
                });
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

            if (!TryParseApiId(item.Id, out var assetId))
            {
                ShowToast("API 자산 ID가 없어 구매할 수 없습니다.");
                return;
            }

            if (notebookRuntimeUseCase == null)
            {
                ShowToast("오브젝트 구매 API가 설정되지 않았습니다.");
                return;
            }

            notebookRuntimeUseCase.PurchaseAsset(
                assetId,
                result =>
                {
                    ownedObjectIds.Add(item.Id);
                    wishedObjectIds.Remove(item.Id);
                    SyncWalletFromApi();
                    if (walletPanelOpen)
                    {
                        RequestWalletDetails();
                    }
                    RefreshObjectMarket();
                    ShowToast($"오브젝트 구매 완료 · {item.Name}");
                },
                error =>
                {
                    UnityEngine.Debug.LogWarning($"Object purchase API failed. asset={assetId}, code={error?.Code}, message={FirstText(error?.Message, error?.Error)}");
                    ShowToast("오브젝트 구매 API 요청에 실패했습니다.");
                });
        }

        private static bool TryParseApiId(string value, out long id)
        {
            id = 0L;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            if (long.TryParse(trimmed, out id) && id > 0)
            {
                return true;
            }

            var dash = trimmed.LastIndexOf('-');
            if (dash >= 0 && dash < trimmed.Length - 1)
            {
                return long.TryParse(trimmed.Substring(dash + 1), out id) && id > 0;
            }

            return false;
        }

        private static bool TryResolveBookApiId(BookViewModel book, out long id)
        {
            id = 0L;
            if (book == null)
            {
                return false;
            }

            if (book.ServerBookId.HasValue && book.ServerBookId.Value > 0)
            {
                id = book.ServerBookId.Value;
                return true;
            }

            return string.Equals(book.ApiSource, "LOCAL", StringComparison.OrdinalIgnoreCase)
                   && TryParseApiId(book.Id, out id);
        }

        private bool TryResolveUserBookId(BookViewModel book, out long id)
        {
            id = 0L;
            if (book == null)
            {
                return false;
            }

            if (book.UserBookId.HasValue && book.UserBookId.Value > 0)
            {
                id = book.UserBookId.Value;
                return true;
            }

            return bookUserBookIds.TryGetValue(book.Id, out id) && id > 0;
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

        private Button MetricButton(string text, Color color, Action onClick)
        {
            var button = TextButton(text, false);
            button.style.height = 24;
            button.style.paddingLeft = 8;
            button.style.paddingRight = 8;
            button.style.marginRight = 6;
            button.style.marginBottom = 4;
            button.style.fontSize = 10;
            button.style.color = color;
            button.style.backgroundColor = Rgba(0x0E, 0x1D, 0x2A, 0.05f);
            Border(button, Rgba(0x0E, 0x1D, 0x2A, 0.06f));
            BlockCardClickThrough(button);
            button.clicked += () =>
            {
                PulseElement(button);
                onClick?.Invoke();
            };
            return button;
        }

        private static void BlockCardClickThrough(VisualElement element)
        {
            if (element == null)
            {
                return;
            }

            element.AddToClassList("space-card-action");
            element.RegisterCallback<ClickEvent>(evt => StopCardActionEvent(evt));
        }

        private static void StopCardActionEvent(EventBase evt)
        {
            evt.StopPropagation();
        }

        private static bool IsSpaceCardActionTarget(VisualElement element)
        {
            while (element != null)
            {
                if (element.ClassListContains("space-card-action"))
                {
                    return true;
                }

                element = element.parent;
            }

            return false;
        }

        private void RefreshFollowButton(Button button, string userId)
        {
            RefreshToggleButton(button, followedUserIds.Contains(userId), "팔로잉", "팔로우");
        }

        private static void RefreshFollowerMetric(Button button, SpaceViewModel space)
        {
            if (button == null || space == null)
            {
                return;
            }

            button.text = $"팔로워 {space.Followers:N0}";
        }

        private void RefreshSocialFollowButton(Button button, UserViewModel user)
        {
            if (button == null || user == null)
            {
                return;
            }

            var selected = followedUserIds.Contains(user.Id.ToString()) || user.Following;
            RefreshToggleButton(button, selected, "팔로잉", "팔로우");
            if (user.Blocked)
            {
                button.text = "차단됨";
                button.SetEnabled(false);
            }
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

        private void RefreshSpaceLikeButton(Button button, SpaceViewModel space)
        {
            if (button == null || space == null)
            {
                return;
            }

            var selected = likedSpaceIds.Contains(space.Id);
            button.text = $"{(selected ? "♥" : "♡")} {Mathf.Max(0, space.Likes):N0}";
            button.tooltip = selected ? "공간 좋아요 취소" : "공간 좋아요";
            button.style.backgroundColor = selected ? Rgba(0xD9, 0x41, 0x5D, 0.12f) : Rgba(0xFF, 0xFF, 0xFF, 0.58f);
            button.style.color = HeartRed;
            button.style.fontSize = 15;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            Border(button, selected ? Rgba(0xD9, 0x41, 0x5D, 0.42f) : Rgba(0xD9, 0x41, 0x5D, 0.30f));
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

        private static string NormalizeText(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace(" ", string.Empty).ToLowerInvariant();
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

        private static string InitialOf(string value, string fallback)
        {
            var text = FirstText(value, fallback, "U");
            return text.Length > 0 ? text.Substring(0, 1) : "U";
        }

        private static long? ResolveServerBookId(NotebookBookSearchItem dto)
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
            button.style.backgroundImage = new StyleBackground(GetFallbackCloseIconTexture());
            button.style.color = Ink;
            button.style.unityFontStyleAndWeight = FontStyle.Normal;
            Border(button, Line);
            return button;
        }

        private IEnumerator LoadBookCoverTexture(string url, VisualElement cover)
        {
            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
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
                    UnityEngine.Debug.LogWarning($"[NotebookToolkit] Skipped oversized book cover {texture.width}x{texture.height}: {url}");
                    Destroy(texture);
                    yield break;
                }

                texture = CacheBookCoverTexture(url, texture);
                if (cover != null && cover.panel != null && Equals(cover.userData, url))
                {
                    ApplyBookCoverTexture(cover, texture);
                }
            }
        }

        private bool TryGetCachedBookCoverTexture(string url, out Texture2D texture)
        {
            if (pendingBookCoverTextureDestroy.TryGetValue(url, out texture) && texture != null)
            {
                pendingBookCoverTextureDestroy.Remove(url);
                bookCoverTextureCache[url] = texture;
                bookCoverTextureCacheNodes[url] = bookCoverTextureCacheOrder.AddLast(url);
                TrimBookCoverTextureCache();
                return true;
            }

            if (!bookCoverTextureCache.TryGetValue(url, out texture) || texture == null)
            {
                bookCoverTextureCache.Remove(url);
                if (bookCoverTextureCacheNodes.TryGetValue(url, out var staleNode))
                {
                    bookCoverTextureCacheOrder.Remove(staleNode);
                    bookCoverTextureCacheNodes.Remove(url);
                }

                texture = null;
                return false;
            }

            if (bookCoverTextureCacheNodes.TryGetValue(url, out var node))
            {
                bookCoverTextureCacheOrder.Remove(node);
                bookCoverTextureCacheOrder.AddLast(node);
            }

            return true;
        }

        private Texture2D CacheBookCoverTexture(string url, Texture2D texture)
        {
            if (string.IsNullOrEmpty(url) || texture == null)
            {
                return texture;
            }

            if (bookCoverTextureCache.TryGetValue(url, out var previous))
            {
                if (previous != null)
                {
                    if (previous != texture)
                    {
                        Destroy(texture);
                    }

                    if (bookCoverTextureCacheNodes.TryGetValue(url, out var existingNode))
                    {
                        bookCoverTextureCacheOrder.Remove(existingNode);
                        bookCoverTextureCacheOrder.AddLast(existingNode);
                    }
                    else
                    {
                        bookCoverTextureCacheNodes[url] = bookCoverTextureCacheOrder.AddLast(url);
                    }

                    return previous;
                }

                bookCoverTextureCache.Remove(url);
                if (bookCoverTextureCacheNodes.TryGetValue(url, out var staleNode))
                {
                    bookCoverTextureCacheOrder.Remove(staleNode);
                    bookCoverTextureCacheNodes.Remove(url);
                }
            }

            bookCoverTextureCache[url] = texture;
            bookCoverTextureCacheNodes[url] = bookCoverTextureCacheOrder.AddLast(url);
            TrimBookCoverTextureCache();
            return texture;
        }

        private void TrimBookCoverTextureCache()
        {
            while (bookCoverTextureCache.Count > MaxBookCoverTextureCacheCount && bookCoverTextureCacheOrder.First != null)
            {
                var oldestUrl = bookCoverTextureCacheOrder.First.Value;
                bookCoverTextureCacheOrder.RemoveFirst();
                bookCoverTextureCacheNodes.Remove(oldestUrl);

                if (bookCoverTextureCache.TryGetValue(oldestUrl, out var texture) && texture != null)
                {
                    DestroyBookCoverTextureWhenUnused(oldestUrl, texture);
                }

                bookCoverTextureCache.Remove(oldestUrl);
            }
        }

        private void DestroyBookCoverTextureWhenUnused(string url, Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(url) &&
                bookCoverTextureUseCounts.TryGetValue(url, out var useCount) &&
                useCount > 0)
            {
                pendingBookCoverTextureDestroy[url] = texture;
                return;
            }

            Destroy(texture);
        }

        private void ClearBookCoverTextureCache()
        {
            foreach (var texture in bookCoverTextureCache.Values)
            {
                if (texture != null)
                {
                    Destroy(texture);
                }
            }

            foreach (var texture in pendingBookCoverTextureDestroy.Values)
            {
                if (texture != null)
                {
                    Destroy(texture);
                }
            }

            bookCoverTextureCache.Clear();
            bookCoverTextureCacheOrder.Clear();
            bookCoverTextureCacheNodes.Clear();
            bookCoverTextureUseCounts.Clear();
            pendingBookCoverTextureDestroy.Clear();
        }

        private void ApplyBookCoverTexture(VisualElement cover, Texture2D texture)
        {
            if (cover == null || texture == null)
            {
                return;
            }

            cover.style.backgroundColor = Color.clear;
            cover.style.backgroundImage = new StyleBackground(texture);
            cover.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            cover.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            cover.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            cover.Q<VisualElement>("book-cover-generated")?.RemoveFromHierarchy();

            if (cover.userData is string url && !string.IsNullOrEmpty(url))
            {
                TrackBookCoverTextureUse(url, cover);
            }
        }

        private void TrackBookCoverTextureUse(string url, VisualElement cover)
        {
            bookCoverTextureUseCounts.TryGetValue(url, out var useCount);
            bookCoverTextureUseCounts[url] = useCount + 1;
            cover.RegisterCallback<DetachFromPanelEvent>(_ => ReleaseBookCoverTextureUse(url));
        }

        private void ReleaseBookCoverTextureUse(string url)
        {
            if (string.IsNullOrEmpty(url) ||
                !bookCoverTextureUseCounts.TryGetValue(url, out var useCount))
            {
                return;
            }

            useCount--;
            if (useCount > 0)
            {
                bookCoverTextureUseCounts[url] = useCount;
                return;
            }

            bookCoverTextureUseCounts.Remove(url);
            if (pendingBookCoverTextureDestroy.TryGetValue(url, out var texture) && texture != null)
            {
                pendingBookCoverTextureDestroy.Remove(url);
                Destroy(texture);
            }
        }

        private void BuildSpaceThumbnail(VisualElement thumbnail, SpaceViewModel space)
        {
            var key = string.IsNullOrEmpty(space.ThumbnailKey) ? space.Id : space.ThumbnailKey;
            var remoteUrl = string.IsNullOrWhiteSpace(space.ThumbnailUrl) ? string.Empty : space.ThumbnailUrl.Trim();
            if (!string.IsNullOrEmpty(remoteUrl) && TryGetCachedSpaceThumbnailTexture(remoteUrl, out var cached))
            {
                ApplySpaceThumbnailTexture(thumbnail, cached);
                return;
            }

            var resource = Resources.Load<Texture2D>($"SpaceThumbnails/{key}");
            if (resource != null)
            {
                ApplySpaceThumbnailTexture(thumbnail, resource);
                return;
            }

            BuildGeneratedSpaceThumbnail(thumbnail, space, key);
            if (!string.IsNullOrEmpty(remoteUrl))
            {
                thumbnail.userData = remoteUrl;
                StartCoroutine(LoadSpaceThumbnailTexture(remoteUrl, thumbnail));
            }
        }

        private static bool HasRemoteSpacePhoto(SpaceViewModel space)
        {
            return space != null && !string.IsNullOrWhiteSpace(space.ThumbnailUrl);
        }

        private IEnumerator LoadSpaceThumbnailTexture(string url, VisualElement thumbnail)
        {
            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
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

                if (texture.width > MaxRemoteSpaceThumbnailWidth || texture.height > MaxRemoteSpaceThumbnailHeight)
                {
                    UnityEngine.Debug.LogWarning($"[NotebookToolkit] Skipped oversized space thumbnail {texture.width}x{texture.height}: {url}");
                    Destroy(texture);
                    yield break;
                }

                CacheSpaceThumbnailTexture(url, texture);
                if (thumbnail != null && thumbnail.panel != null && Equals(thumbnail.userData, url))
                {
                    ApplySpaceThumbnailTexture(thumbnail, texture);
                }
            }
        }

        private bool TryGetCachedSpaceThumbnailTexture(string url, out Texture2D texture)
        {
            if (!spaceThumbnailTextureCache.TryGetValue(url, out texture) || texture == null)
            {
                spaceThumbnailTextureCache.Remove(url);
                if (spaceThumbnailTextureCacheNodes.TryGetValue(url, out var staleNode))
                {
                    spaceThumbnailTextureCacheOrder.Remove(staleNode);
                    spaceThumbnailTextureCacheNodes.Remove(url);
                }

                texture = null;
                return false;
            }

            if (spaceThumbnailTextureCacheNodes.TryGetValue(url, out var node))
            {
                spaceThumbnailTextureCacheOrder.Remove(node);
                spaceThumbnailTextureCacheOrder.AddLast(node);
            }

            return true;
        }

        private void CacheSpaceThumbnailTexture(string url, Texture2D texture)
        {
            if (spaceThumbnailTextureCache.TryGetValue(url, out var previous) && previous != null && previous != texture)
            {
                Destroy(previous);
            }

            spaceThumbnailTextureCache[url] = texture;

            if (spaceThumbnailTextureCacheNodes.TryGetValue(url, out var node))
            {
                spaceThumbnailTextureCacheOrder.Remove(node);
                spaceThumbnailTextureCacheOrder.AddLast(node);
            }
            else
            {
                node = spaceThumbnailTextureCacheOrder.AddLast(url);
                spaceThumbnailTextureCacheNodes[url] = node;
            }

            TrimSpaceThumbnailTextureCache();
        }

        private void TrimSpaceThumbnailTextureCache()
        {
            while (spaceThumbnailTextureCache.Count > MaxSpaceThumbnailTextureCacheCount && spaceThumbnailTextureCacheOrder.First != null)
            {
                var oldestUrl = spaceThumbnailTextureCacheOrder.First.Value;
                spaceThumbnailTextureCacheOrder.RemoveFirst();
                spaceThumbnailTextureCacheNodes.Remove(oldestUrl);

                if (spaceThumbnailTextureCache.TryGetValue(oldestUrl, out var texture) && texture != null)
                {
                    Destroy(texture);
                }

                spaceThumbnailTextureCache.Remove(oldestUrl);
            }
        }

        private void ClearSpaceThumbnailTextureCache()
        {
            foreach (var texture in spaceThumbnailTextureCache.Values)
            {
                if (texture != null)
                {
                    Destroy(texture);
                }
            }

            spaceThumbnailTextureCache.Clear();
            spaceThumbnailTextureCacheOrder.Clear();
            spaceThumbnailTextureCacheNodes.Clear();
        }

        private bool TryBuildObjectPreviewImage(VisualElement preview, string previewImageUrl)
        {
            var url = string.IsNullOrWhiteSpace(previewImageUrl) ? string.Empty : previewImageUrl.Trim();
            if (preview == null || string.IsNullOrEmpty(url))
            {
                return false;
            }

            if (TryApplyLocalObjectPreviewTexture(preview, url))
            {
                return true;
            }

            if (!IsRemoteImageUrl(url))
            {
                return false;
            }

            preview.userData = url;
            if (TryGetCachedAssetPreviewTexture(url, out var cached))
            {
                ApplyObjectPreviewTexture(preview, cached);
                return true;
            }

            StartCoroutine(LoadObjectPreviewTexture(url, preview));
            return false;
        }

        private static bool TryApplyLocalObjectPreviewTexture(VisualElement preview, string assetPath)
        {
            if (preview == null || string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            if (TryGetCustomizationPreviewSprite(assetPath, out var sprite))
            {
                preview.userData = assetPath;
                ApplyObjectPreviewSprite(preview, sprite);
                return true;
            }

#if UNITY_EDITOR
            if (assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite != null)
                {
                    preview.userData = assetPath;
                    ApplyObjectPreviewSprite(preview, sprite);
                    return true;
                }

                var texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (texture != null)
                {
                    preview.userData = assetPath;
                    ApplyObjectPreviewTexture(preview, texture);
                    return true;
                }
            }
#endif
            return false;
        }

        private bool TryGetCachedAssetPreviewTexture(string url, out Texture2D texture)
        {
            if (!assetPreviewTextureCache.TryGetValue(url, out texture) || texture == null)
            {
                assetPreviewTextureCache.Remove(url);
                if (assetPreviewTextureCacheNodes.TryGetValue(url, out var staleNode))
                {
                    assetPreviewTextureCacheOrder.Remove(staleNode);
                    assetPreviewTextureCacheNodes.Remove(url);
                }

                texture = null;
                return false;
            }

            if (assetPreviewTextureCacheNodes.TryGetValue(url, out var node))
            {
                assetPreviewTextureCacheOrder.Remove(node);
                assetPreviewTextureCacheOrder.AddLast(node);
            }

            return true;
        }

        private void CacheAssetPreviewTexture(string url, Texture2D texture)
        {
            if (assetPreviewTextureCache.TryGetValue(url, out var previous) && previous != null && previous != texture)
            {
                Destroy(previous);
            }

            assetPreviewTextureCache[url] = texture;

            if (assetPreviewTextureCacheNodes.TryGetValue(url, out var node))
            {
                assetPreviewTextureCacheOrder.Remove(node);
                assetPreviewTextureCacheOrder.AddLast(node);
            }
            else
            {
                node = assetPreviewTextureCacheOrder.AddLast(url);
                assetPreviewTextureCacheNodes[url] = node;
            }

            TrimAssetPreviewTextureCache();
        }

        private void TrimAssetPreviewTextureCache()
        {
            while (assetPreviewTextureCache.Count > MaxAssetPreviewTextureCacheCount && assetPreviewTextureCacheOrder.First != null)
            {
                var oldestUrl = assetPreviewTextureCacheOrder.First.Value;
                assetPreviewTextureCacheOrder.RemoveFirst();
                assetPreviewTextureCacheNodes.Remove(oldestUrl);

                if (assetPreviewTextureCache.TryGetValue(oldestUrl, out var texture) && texture != null)
                {
                    Destroy(texture);
                }

                assetPreviewTextureCache.Remove(oldestUrl);
            }
        }

        private static bool IsRemoteImageUrl(string url)
        {
            return !string.IsNullOrWhiteSpace(url) &&
                   (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        }

        private IEnumerator LoadObjectPreviewTexture(string url, VisualElement preview)
        {
            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
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

                if (texture.width > MaxRemoteAssetPreviewWidth || texture.height > MaxRemoteAssetPreviewHeight)
                {
                    UnityEngine.Debug.LogWarning($"[NotebookToolkit] Skipped oversized asset preview {texture.width}x{texture.height}: {url}");
                    Destroy(texture);
                    yield break;
                }

                CacheAssetPreviewTexture(url, texture);
                if (preview != null && preview.panel != null && Equals(preview.userData, url))
                {
                    ApplyObjectPreviewTexture(preview, texture);
                }
            }
        }

        private void ClearAssetPreviewTextureCache()
        {
            foreach (var texture in assetPreviewTextureCache.Values)
            {
                if (texture != null)
                {
                    Destroy(texture);
                }
            }

            assetPreviewTextureCache.Clear();
            assetPreviewTextureCacheOrder.Clear();
            assetPreviewTextureCacheNodes.Clear();
        }

        private static void ApplyObjectPreviewSprite(VisualElement preview, Sprite sprite)
        {
            preview.Clear();
            preview.style.backgroundColor = Color.clear;
            preview.style.backgroundImage = new StyleBackground(sprite);
            preview.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            preview.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            preview.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
        }

        private static void ApplyObjectPreviewTexture(VisualElement preview, Texture2D texture)
        {
            preview.Clear();
            preview.style.backgroundColor = Color.clear;
            preview.style.backgroundImage = new StyleBackground(texture);
            preview.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            preview.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            preview.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
        }

        private static void ApplySpaceThumbnailTexture(VisualElement thumbnail, Texture2D texture)
        {
            thumbnail.style.backgroundColor = Color.clear;
            thumbnail.style.backgroundImage = new StyleBackground(texture);
            thumbnail.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            thumbnail.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            thumbnail.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            thumbnail.Q<VisualElement>("space-thumbnail-fallback")?.RemoveFromHierarchy();
        }

        private static void BuildGeneratedSpaceThumbnail(VisualElement thumbnail, SpaceViewModel space, string key)
        {
            var accent = space.AccentColor;
            thumbnail.style.backgroundColor = Color.Lerp(Night, accent, 0.28f);

            var fallback = new VisualElement { name = "space-thumbnail-fallback" };
            fallback.pickingMode = PickingMode.Ignore;
            fallback.style.position = Position.Absolute;
            fallback.style.left = 0;
            fallback.style.right = 0;
            fallback.style.top = 0;
            fallback.style.bottom = 0;
            thumbnail.Add(fallback);

            var wall = RoomRect("photo-wall", 18, 20, 254, 88, Rgba(0xF7, 0xFF, 0xF7, 0.13f), 10);
            wall.style.borderTopWidth = 1;
            wall.style.borderRightWidth = 1;
            wall.style.borderBottomWidth = 1;
            wall.style.borderLeftWidth = 1;
            wall.style.borderTopColor = Rgba(0xFF, 0xFF, 0xFF, 0.13f);
            wall.style.borderRightColor = Rgba(0xFF, 0xFF, 0xFF, 0.13f);
            wall.style.borderBottomColor = Rgba(0xFF, 0xFF, 0xFF, 0.13f);
            wall.style.borderLeftColor = Rgba(0xFF, 0xFF, 0xFF, 0.13f);
            fallback.Add(wall);

            AddShelfPreview(fallback, 38, 56, 128, 44, accent, key == "endless-sf-shelf" ? 14 : 10);
            AddDeskPreview(fallback, 42, 94, 204, 18, key == "endless-sf-shelf" ? Color.Lerp(Night, Violet, 0.46f) : WarmPaper);
            AddNotebookPreview(fallback, key == "sunday-economy-desk" ? 146 : 116, 78, Accent);

            if (key == "sunday-economy-desk")
            {
                AddWindowPreview(fallback, 204, 36, 62, 56, Accent, true);
                AddChartPreview(fallback, 58, 38, 74, 46, Gold);
            }
            else if (key == "endless-sf-shelf")
            {
                AddWindowPreview(fallback, 204, 34, 58, 62, Accent, false);
                AddRoomRect(fallback, "neon-line", 212, 74, 42, 3, Surface, 1);
            }
            else if (key == "sentence-collector-shelf")
            {
                AddPinnedCardPreview(fallback, 204, 40, 54, 52, Gold);
            }
            else
            {
                AddLampPreview(fallback, 208, 66, Gold);
            }

            AddRoomRect(fallback, "photo-floor-glow", 0, 106, 320, 34, Rgba(0xFF, 0xFF, 0xFF, 0.08f), 0);
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

        private static StyleBackground ResolveSearchIconBackground()
        {
#if UNITY_EDITOR
            if (commonSearchIconSprite == null)
            {
                commonSearchIconSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(CommonSearchIconPath);
            }

            if (commonSearchIconSprite != null)
            {
                return new StyleBackground(commonSearchIconSprite);
            }
#endif
            return new StyleBackground(GetFallbackSearchIconTexture());
        }

        private static Texture2D GetFallbackSearchIconTexture()
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

        private static Texture2D GetFallbackCloseIconTexture()
        {
            if (closeIconTexture != null)
            {
                return closeIconTexture;
            }

            const int size = 32;
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
            var color = new Color32(14, 29, 42, 210);
            DrawIconLine(closeIconTexture, 10, 10, 22, 22, color);
            DrawIconLine(closeIconTexture, 22, 10, 10, 22, color);
            closeIconTexture.Apply(false, true);
            return closeIconTexture;
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

        // -----------------------------------------------------------------------------
        // Payment 통합 (KG이니시스 충전 흐름 — S14P31F206-265)
        // 기존 ChargePagesThroughApi(deprecated /v1/payments/page-charge) 대체.
        // 흐름: OpenPaymentChargeOverlay → 프리셋 선택 → StartCharge → Application.OpenURL +
        //       OpenPaymentPendingOverlay → 2초 간격 폴링 (max 5분) → DONE 시 wallet 갱신.
        // -----------------------------------------------------------------------------

        private void OpenPaymentChargeOverlay()
        {
            if (paymentUseCase == null)
            {
                ShowToast("결제 모듈이 아직 준비되지 않았습니다.");
                return;
            }
            EnsurePaymentOverlayBuilt();
            ShowPaymentPanel(paymentChargePanel);
            paymentOverlayRoot.style.display = DisplayStyle.Flex;
            RefreshPaymentChargePresetSelection();
        }

        private void OpenPaymentPendingOverlay(string orderId)
        {
            paymentPendingOrderId = orderId;
            EnsurePaymentOverlayBuilt();
            ShowPaymentPanel(paymentPendingPanel);
            if (paymentPendingStatusLabel != null)
            {
                paymentPendingStatusLabel.text = "결제창에서 결제를 완료해 주세요.\n완료 후 자동으로 잔액이 갱신됩니다.";
            }
            paymentOverlayRoot.style.display = DisplayStyle.Flex;
            StopPaymentPolling();
            paymentPollCoroutine = StartCoroutine(PaymentPollingRoutine(orderId));
        }

        private void OpenPaymentHistoryOverlay()
        {
            EnsurePaymentOverlayBuilt();
            ShowPaymentPanel(paymentHistoryPanel);
            paymentOverlayRoot.style.display = DisplayStyle.Flex;
            RequestPaymentTransactions();
        }

        private void ClosePaymentOverlay()
        {
            StopPaymentPolling();
            paymentPendingOrderId = null;
            if (paymentOverlayRoot != null)
            {
                paymentOverlayRoot.style.display = DisplayStyle.None;
            }
        }

        private void StopPaymentPolling()
        {
            if (paymentPollCoroutine != null)
            {
                StopCoroutine(paymentPollCoroutine);
                paymentPollCoroutine = null;
            }
        }

        private IEnumerator PaymentPollingRoutine(string orderId)
        {
            float elapsed = 0f;
            while (elapsed < PaymentPollTimeoutSeconds)
            {
                yield return new WaitForSeconds(PaymentPollIntervalSeconds);
                elapsed += PaymentPollIntervalSeconds;
                if (string.IsNullOrEmpty(paymentPendingOrderId) || paymentPendingOrderId != orderId)
                {
                    yield break;
                }
                paymentUseCase?.PollOrderStatus(
                    orderId,
                    result => HandlePaymentPollResult(result, orderId),
                    error => HandlePaymentPollError(error, orderId));
            }
            HandlePaymentPollTimeout(orderId);
        }

        private void HandlePaymentPollResult(PaymentPollResult result, string orderId)
        {
            if (result == null || paymentPendingOrderId != orderId)
            {
                return;
            }
            if (paymentPendingStatusLabel != null)
            {
                paymentPendingStatusLabel.text = $"상태: {result.Status}";
            }
            if (!result.IsTerminal)
            {
                return;
            }
            StopPaymentPolling();
            paymentPendingOrderId = null;
            if (result.Status == PaymentStatus.Done)
            {
                if (result.WalletBalancePagesAfter.HasValue)
                {
                    pages = Mathf.Max(0, result.WalletBalancePagesAfter.Value);
                    RefreshWallet();
                    if (walletPanelOpen)
                    {
                        RequestWalletDetails();
                    }
                }
                ShowToast("결제가 완료되었습니다.");
                ClosePaymentOverlay();
            }
            else
            {
                ShowToast(result.Status == PaymentStatus.Canceled
                    ? "결제가 취소되었습니다."
                    : "결제에 실패했습니다. 잠시 후 다시 시도해 주세요.");
                ClosePaymentOverlay();
            }
        }

        private void HandlePaymentPollError(PaymentApiError error, string orderId)
        {
            if (paymentPendingOrderId != orderId)
            {
                return;
            }
            UnityEngine.Debug.LogWarning(
                $"Payment poll failed. orderId={orderId}, code={error?.Code}, message={error?.Message}");
            // 폴링 중 일시 에러는 다음 tick 에서 재시도 — 즉시 fail 처리하지 않는다.
        }

        private void HandlePaymentPollTimeout(string orderId)
        {
            if (paymentPendingOrderId != orderId)
            {
                return;
            }
            StopPaymentPolling();
            paymentPendingOrderId = null;
            ShowToast("결제 확인 시간이 초과되었습니다. 결제 내역에서 상태를 확인해 주세요.");
            ClosePaymentOverlay();
        }

        private void EnsurePaymentOverlayBuilt()
        {
            if (paymentOverlayRoot != null)
            {
                return;
            }

            var doc = GetComponent<UIDocument>();
            var rootSurface = doc != null ? doc.rootVisualElement : null;
            if (rootSurface == null)
            {
                return;
            }

            paymentOverlayRoot = new VisualElement { name = "payment-overlay" };
            paymentOverlayRoot.style.position = Position.Absolute;
            paymentOverlayRoot.style.left = 0;
            paymentOverlayRoot.style.top = 0;
            paymentOverlayRoot.style.right = 0;
            paymentOverlayRoot.style.bottom = 0;
            paymentOverlayRoot.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            paymentOverlayRoot.style.display = DisplayStyle.None;
            paymentOverlayRoot.style.alignItems = Align.Center;
            paymentOverlayRoot.style.justifyContent = Justify.Center;
            paymentOverlayRoot.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            rootSurface.Add(paymentOverlayRoot);

            paymentChargePanel = BuildPaymentChargePanel();
            paymentPendingPanel = BuildPaymentPendingPanel();
            paymentHistoryPanel = BuildPaymentHistoryPanel();
            paymentOverlayRoot.Add(paymentChargePanel);
            paymentOverlayRoot.Add(paymentPendingPanel);
            paymentOverlayRoot.Add(paymentHistoryPanel);
            paymentChargePanel.style.display = DisplayStyle.Flex;
            paymentPendingPanel.style.display = DisplayStyle.None;
            paymentHistoryPanel.style.display = DisplayStyle.None;
        }

        private void ShowPaymentPanel(VisualElement target)
        {
            if (paymentChargePanel != null)
            {
                paymentChargePanel.style.display = target == paymentChargePanel ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (paymentPendingPanel != null)
            {
                paymentPendingPanel.style.display = target == paymentPendingPanel ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (paymentHistoryPanel != null)
            {
                paymentHistoryPanel.style.display = target == paymentHistoryPanel ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private VisualElement BuildPaymentChargePanel()
        {
            var panel = new VisualElement { name = "payment-charge-panel" };
            panel.style.width = 480;
            panel.style.paddingLeft = 28;
            panel.style.paddingRight = 28;
            panel.style.paddingTop = 28;
            panel.style.paddingBottom = 28;
            panel.style.backgroundColor = Color.white;
            Round(panel, 18);

            var title = Text("페이지 충전", 22, Ink, FontStyle.Bold);
            panel.Add(title);

            var subtitle = Text("결제 수단: 카드 결제만 가능 · 유효기간 1년 (서비스 운영 중 자동 갱신)", 11, SoftInk, FontStyle.Normal);
            subtitle.style.marginTop = 4;
            subtitle.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(subtitle);

            var presetGrid = new VisualElement();
            presetGrid.style.flexDirection = FlexDirection.Row;
            presetGrid.style.flexWrap = Wrap.Wrap;
            presetGrid.style.marginTop = 18;
            panel.Add(presetGrid);

            foreach (var preset in ChargePresets.All)
            {
                var pages = ChargePresets.PagesOf(preset);
                var presetButton = TextButton($"{pages:N0} P", true);
                presetButton.style.width = 130;
                presetButton.style.height = 48;
                presetButton.style.marginRight = 8;
                presetButton.style.marginBottom = 8;
                var capturedPreset = preset;
                presetButton.clicked += () =>
                {
                    paymentSelectedPreset = capturedPreset;
                    RefreshPaymentChargePresetSelection();
                };
                presetButton.userData = preset;
                presetGrid.Add(presetButton);
            }

            var notice = Text(
                "결제는 외부 브라우저의 KG이니시스 결제창에서 진행됩니다. 결제 완료 후 자동으로 페이지가 충전됩니다.",
                11,
                SoftInk,
                FontStyle.Normal);
            notice.style.marginTop = 12;
            notice.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(notice);

            var buttonsRow = Row("payment-charge-buttons");
            buttonsRow.style.marginTop = 18;
            buttonsRow.style.justifyContent = Justify.SpaceBetween;
            panel.Add(buttonsRow);

            var cancelButton = TextButton("취소", false);
            cancelButton.style.width = 100;
            cancelButton.style.height = 44;
            cancelButton.clicked += ClosePaymentOverlay;
            buttonsRow.Add(cancelButton);

            var startButton = TextButton("결제 시작", true);
            startButton.style.width = 140;
            startButton.style.height = 44;
            startButton.clicked += StartPaymentChargeFromOverlay;
            buttonsRow.Add(startButton);

            return panel;
        }

        private void RefreshPaymentChargePresetSelection()
        {
            if (paymentChargePanel == null)
            {
                return;
            }
            paymentChargePanel.Query<Button>().ForEach(button =>
            {
                if (button.userData is ChargePreset preset)
                {
                    button.style.borderTopWidth = preset == paymentSelectedPreset ? 2 : 1;
                    button.style.borderRightWidth = preset == paymentSelectedPreset ? 2 : 1;
                    button.style.borderBottomWidth = preset == paymentSelectedPreset ? 2 : 1;
                    button.style.borderLeftWidth = preset == paymentSelectedPreset ? 2 : 1;
                    button.style.borderTopColor = preset == paymentSelectedPreset ? Accent : Rgba(0x0E, 0x1D, 0x2A, 0.18f);
                    button.style.borderRightColor = preset == paymentSelectedPreset ? Accent : Rgba(0x0E, 0x1D, 0x2A, 0.18f);
                    button.style.borderBottomColor = preset == paymentSelectedPreset ? Accent : Rgba(0x0E, 0x1D, 0x2A, 0.18f);
                    button.style.borderLeftColor = preset == paymentSelectedPreset ? Accent : Rgba(0x0E, 0x1D, 0x2A, 0.18f);
                }
            });
        }

        private void StartPaymentChargeFromOverlay()
        {
            if (paymentUseCase == null)
            {
                ShowToast("결제 모듈이 준비되지 않았습니다.");
                return;
            }
            var buyerName = TryGetBuyerName();
            var buyerTel = TryGetBuyerTel();
            var buyerEmail = TryGetBuyerEmail();

            paymentUseCase.StartCharge(
                paymentSelectedPreset,
                buyerName,
                buyerTel,
                buyerEmail,
                result =>
                {
                    if (result == null || result.Order == null)
                    {
                        ShowToast("결제 주문 생성에 실패했습니다.");
                        return;
                    }
                    if (!string.IsNullOrWhiteSpace(result.FullPaymentPageUrl))
                    {
                        UnityEngine.Application.OpenURL(result.FullPaymentPageUrl);
                    }
                    OpenPaymentPendingOverlay(result.Order.OrderId);
                },
                error =>
                {
                    UnityEngine.Debug.LogWarning(
                        $"Charge order create failed. code={error?.Code}, message={error?.Message}");
                    ShowToast($"결제 주문 생성 실패: {error?.Message ?? "알 수 없는 오류"}");
                });
        }

        private string TryGetBuyerName()
        {
            // Phase 1 — 사용자 프로필 또는 입력 폼 미연동. BE 가 임시 필드 받게 placeholder.
            // Phase 2 에서 입력 폼 또는 user.me 응답에서 가져옴.
            return "DotShelf 사용자";
        }

        private string TryGetBuyerTel()
        {
            return "010-0000-0000";
        }

        private string TryGetBuyerEmail()
        {
            return "buyer@dotshelf.digital";
        }

        private VisualElement BuildPaymentPendingPanel()
        {
            var panel = new VisualElement { name = "payment-pending-panel" };
            panel.style.width = 420;
            panel.style.paddingLeft = 28;
            panel.style.paddingRight = 28;
            panel.style.paddingTop = 28;
            panel.style.paddingBottom = 28;
            panel.style.backgroundColor = Color.white;
            Round(panel, 18);

            var title = Text("결제 진행 중", 20, Ink, FontStyle.Bold);
            panel.Add(title);

            paymentPendingStatusLabel = Text(
                "결제창에서 결제를 완료해 주세요.\n완료 후 자동으로 잔액이 갱신됩니다.",
                12,
                SoftInk,
                FontStyle.Normal);
            paymentPendingStatusLabel.style.marginTop = 12;
            paymentPendingStatusLabel.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(paymentPendingStatusLabel);

            var buttonsRow = Row("payment-pending-buttons");
            buttonsRow.style.marginTop = 22;
            buttonsRow.style.justifyContent = Justify.FlexEnd;
            panel.Add(buttonsRow);

            var cancelButton = TextButton("닫기", false);
            cancelButton.style.width = 110;
            cancelButton.style.height = 44;
            cancelButton.clicked += ClosePaymentOverlay;
            buttonsRow.Add(cancelButton);

            return panel;
        }

        private VisualElement BuildPaymentHistoryPanel()
        {
            var panel = new VisualElement { name = "payment-history-panel" };
            panel.style.width = 540;
            panel.style.maxHeight = 600;
            panel.style.paddingLeft = 28;
            panel.style.paddingRight = 28;
            panel.style.paddingTop = 28;
            panel.style.paddingBottom = 28;
            panel.style.backgroundColor = Color.white;
            Round(panel, 18);

            var title = Text("페이지 충전·사용 내역", 20, Ink, FontStyle.Bold);
            panel.Add(title);

            paymentHistoryEmptyLabel = Text("내역을 불러오는 중...", 12, SoftInk, FontStyle.Normal);
            paymentHistoryEmptyLabel.style.marginTop = 12;
            panel.Add(paymentHistoryEmptyLabel);

            var scroll = new ScrollView();
            scroll.style.flexGrow = 1f;
            scroll.style.marginTop = 12;
            panel.Add(scroll);

            paymentHistoryListBody = new VisualElement();
            scroll.Add(paymentHistoryListBody);

            var buttonsRow = Row("payment-history-buttons");
            buttonsRow.style.marginTop = 16;
            buttonsRow.style.justifyContent = Justify.FlexEnd;
            panel.Add(buttonsRow);

            var closeButton = TextButton("닫기", false);
            closeButton.style.width = 110;
            closeButton.style.height = 44;
            closeButton.clicked += ClosePaymentOverlay;
            buttonsRow.Add(closeButton);

            return panel;
        }

        private void RequestPaymentTransactions()
        {
            if (paymentUseCase == null || paymentHistoryListBody == null)
            {
                return;
            }
            paymentHistoryListBody.Clear();
            if (paymentHistoryEmptyLabel != null)
            {
                paymentHistoryEmptyLabel.text = "내역을 불러오는 중...";
                paymentHistoryEmptyLabel.style.display = DisplayStyle.Flex;
            }
            paymentUseCase.FetchTransactions(
                100,
                result =>
                {
                    if (result == null || result.Transactions == null || result.Transactions.Count == 0)
                    {
                        if (paymentHistoryEmptyLabel != null)
                        {
                            paymentHistoryEmptyLabel.text = "내역이 없습니다.";
                        }
                        return;
                    }
                    if (paymentHistoryEmptyLabel != null)
                    {
                        paymentHistoryEmptyLabel.style.display = DisplayStyle.None;
                    }
                    foreach (var view in result.Transactions)
                    {
                        paymentHistoryListBody.Add(BuildPaymentHistoryRow(view));
                    }
                },
                error =>
                {
                    if (paymentHistoryEmptyLabel != null)
                    {
                        paymentHistoryEmptyLabel.text = $"불러오기 실패: {error?.Message ?? "알 수 없는 오류"}";
                    }
                });
        }

        private VisualElement BuildPaymentHistoryRow(PageTransactionView view)
        {
            var row = Row("payment-history-row");
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginBottom = 8;
            row.style.paddingTop = 8;
            row.style.paddingBottom = 8;
            BorderBottom(row, Rgba(0x0E, 0x1D, 0x2A, 0.08f));

            var left = new VisualElement();
            left.style.flexDirection = FlexDirection.Column;
            row.Add(left);

            var reasonLabel = Text(string.IsNullOrEmpty(view.Reason) ? view.Type : view.Reason, 13, Ink, FontStyle.Bold);
            left.Add(reasonLabel);

            var meta = Text(view.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), 10, FaintInk, FontStyle.Normal);
            left.Add(meta);

            var delta = Text(
                (view.PagesDelta >= 0 ? "+" : string.Empty) + view.PagesDelta.ToString("N0") + "P",
                14,
                view.PagesDelta >= 0 ? Accent : Ink,
                FontStyle.Bold);
            row.Add(delta);

            return row;
        }
    }
}
