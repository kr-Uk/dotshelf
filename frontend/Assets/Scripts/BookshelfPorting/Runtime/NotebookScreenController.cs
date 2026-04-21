using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BookshelfPorting.Runtime
{
    public class NotebookScreenController : MonoBehaviour
    {
        private static readonly string[] DefaultAdFolders =
        {
            "Assets/Art/UI/LaptopAds",
            "Assets/Arts/UI/LaptopAds"
        };
        private const string MissingAdsWarning = "NotebookScreenController: No ad sprites assigned. Assign Sprite assets from Assets/Art/UI/LaptopAds or Assets/Arts/UI/LaptopAds to adImages.";
        private enum NotebookUiState
        {
            ScreenSaver,
            Dashboard
        }

        private sealed class DummyBook
        {
            public DummyBook(string title, string author, string publisher, string summary, string shelf, string status, float rating, int views, int reviewCount, Color coverColor, params DummyReview[] reviews)
            {
                Title = title;
                Author = author;
                Publisher = publisher;
                Summary = summary;
                Shelf = shelf;
                Status = status;
                Rating = rating;
                Views = views;
                ReviewCount = reviewCount;
                CoverColor = coverColor;
                Reviews = reviews;
            }

            public string Title { get; }
            public string Author { get; }
            public string Publisher { get; }
            public string Summary { get; }
            public string Shelf { get; }
            public string Status { get; }
            public float Rating { get; }
            public int Views { get; }
            public int ReviewCount { get; }
            public Color CoverColor { get; }
            public DummyReview[] Reviews { get; }
        }

        private sealed class DummyReview
        {
            public DummyReview(string author, float rating, string line)
            {
                Author = author;
                Rating = rating;
                Line = line;
            }

            public string Author { get; }
            public float Rating { get; }
            public string Line { get; }
        }

        private sealed class DummyUser
        {
            public DummyUser(string nickname, string intro, string roomName, Color profileColor, bool isFollowing = false)
            {
                Nickname = nickname;
                Intro = intro;
                RoomName = roomName;
                ProfileColor = profileColor;
                IsFollowing = isFollowing;
            }

            public string Nickname { get; }
            public string Intro { get; }
            public string RoomName { get; }
            public Color ProfileColor { get; }
            public bool IsFollowing { get; set; }
        }

        private const float CanvasScale = 0.00044f;
        private const float TransitionDuration = 0.22f;
        private const float HiddenScaleMultiplier = 0.96f;
        private static readonly Vector2 DefaultCanvasSize = new(600f, 340f);
        private static readonly Vector2 DashboardCanvasSize = new(850f, 480f);
        private static readonly string[] BookCategories = { "전체", "읽는 중", "읽음", "안 읽음", "내 책장 1", "내 책장 2" };
        private static readonly string[] ReadingStatuses = { "읽음", "읽는 중", "안 읽음" };
        private static readonly DummyBook[] DummyBooks =
        {
            new("모순", "양귀자", "쓰다", "복잡한 감정과 선택의 결을 따라가며 삶의 균열을 정밀하게 포착하는 장편소설.", "내 책장 1", "읽는 중", 4.3f, 1234, 12, new Color(0.86f, 0.68f, 0.42f, 1f), new DummyReview("minji", 4.5f, "문장이 차분해서 오래 남는 이야기였어요."), new DummyReview("reader_02", 4.0f, "인물 감정선이 섬세하게 이어집니다."), new DummyReview("hana", 4.4f, "끝까지 긴장감을 놓치지 않게 해줘요.")),
            new("불편한 편의점", "김호연", "나무옆의자", "편의점을 중심으로 낯선 사람들이 느슨하게 연결되며 서로를 회복시키는 이야기.", "전체", "읽음", 4.6f, 3840, 28, new Color(0.52f, 0.74f, 0.91f, 1f), new DummyReview("soo", 4.8f, "따뜻하고 편하게 읽히는 소설입니다."), new DummyReview("bookcat", 4.6f, "장면마다 캐릭터가 살아 있어요."), new DummyReview("eun", 4.3f, "가볍게 시작했는데 여운이 컸어요.")),
            new("아몬드", "손원평", "창비", "감정을 잘 느끼지 못하는 소년이 타인과의 관계를 통해 세계를 배워가는 성장담.", "내 책장 2", "안 읽음", 4.5f, 2911, 19, new Color(0.94f, 0.79f, 0.47f, 1f), new DummyReview("jun", 4.7f, "짧지만 밀도가 높은 성장 서사예요."), new DummyReview("mari", 4.4f, "감정의 의미를 다시 보게 됩니다.")),
            new("작별인사", "김영하", "복복서가", "인간성과 기억, 존재의 경계를 차분하게 밀어붙이는 SF 장편.", "읽는 중", "읽는 중", 4.1f, 1642, 9, new Color(0.64f, 0.70f, 0.88f, 1f), new DummyReview("neo", 4.2f, "SF 설정이 어렵지 않고 몰입감이 좋습니다."), new DummyReview("sena", 4.0f, "질문을 오래 남기는 타입의 작품이에요.")),
            new("달러구트 꿈 백화점", "이미예", "팩토리나인", "잠든 사이 방문하는 꿈 백화점을 배경으로 한 판타지 힐링 서사.", "읽음", "읽음", 4.4f, 4123, 31, new Color(0.79f, 0.62f, 0.90f, 1f), new DummyReview("anna", 4.5f, "설정이 귀엽고 읽는 내내 편안했어요."), new DummyReview("jin", 4.3f, "에피소드 구성이 좋아서 끊기지 않아요.")),
            new("메리골드 마음 세탁소", "윤정은", "북로망스", "마음을 씻어내고 다시 살아갈 힘을 건네는 따뜻한 판타지 소설.", "안 읽음", "안 읽음", 4.2f, 2085, 14, new Color(0.72f, 0.84f, 0.58f, 1f), new DummyReview("leo", 4.1f, "지친 날 가볍게 읽기 좋은 책입니다."), new DummyReview("narin", 4.3f, "메시지가 또렷하고 부담 없이 읽혀요."))
        };

        private static readonly DummyUser[] DummyUsers =
        {
            new("bookmori", "오늘은 에세이 책장을 정리하고 있어요.", "모리의 서재", new Color(0.89f, 0.62f, 0.52f, 1f), true),
            new("hana.reads", "SF와 추리소설을 번갈아 읽습니다.", "하나의 북룸", new Color(0.54f, 0.72f, 0.88f, 1f)),
            new("papercloud", "짧은 문장과 긴 리뷰를 좋아해요.", "클라우드 북스페이스", new Color(0.74f, 0.78f, 0.57f, 1f)),
            new("jun_archive", "읽는 중인 책과 메모를 천천히 기록합니다.", "준의 아카이브", new Color(0.62f, 0.63f, 0.84f, 1f), true),
            new("mintpage", "그림책, 여행기, 독립출판물을 모읍니다.", "민트 페이지 룸", new Color(0.50f, 0.80f, 0.73f, 1f)),
            new("seo_shelf", "책장 분위기를 계절마다 바꾸는 중이에요.", "서의 책장", new Color(0.84f, 0.67f, 0.79f, 1f)),
            new("reader.ryu", "요즘은 고전문학 다시 읽기 챌린지 중.", "류의 독서 공간", new Color(0.93f, 0.74f, 0.46f, 1f))
        };

        private Canvas screenSaverCanvas;
        private Canvas dashboardCanvas;
        private CanvasGroup screenSaverCanvasGroup;
        private CanvasGroup dashboardCanvasGroup;
        private Coroutine transitionRoutine;
        [SerializeField] private Image screenSaverImage;
        [SerializeField] private Sprite[] adImages;
        [SerializeField] private bool useRandomAd;
        private Image screenSaverBackground;
        private RectTransform screenSaverImageRect;
        private RectTransform dashboardAppFrameRect;
        private RectTransform dashboardContentWindowRect;
        private Image dashboardRootBackground;
        private GameObject booksContentRoot;
        private GameObject usersContentRoot;
        private GameObject settingsContentRoot;
        private readonly Button[] categoryButtons = new Button[BookCategories.Length];
        private readonly Button[] statusButtons = new Button[ReadingStatuses.Length];
        private readonly Button[] bookItemButtons = new Button[DummyBooks.Length];
        private InputField searchField;
        private InputField userSearchField;
        private readonly GameObject[] userItemCards = new GameObject[DummyUsers.Length];
        private readonly int[] visibleUserIndices = new int[DummyUsers.Length];
        private readonly Text[] userNameTexts = new Text[DummyUsers.Length];
        private readonly Text[] userIntroTexts = new Text[DummyUsers.Length];
        private readonly Image[] userProfileImages = new Image[DummyUsers.Length];
        private readonly Button[] userFollowButtons = new Button[DummyUsers.Length];
        private readonly Button[] userVisitButtons = new Button[DummyUsers.Length];
        private Image userDetailProfileImage;
        private Text userDetailNicknameText;
        private Text userDetailIntroText;
        private Text userDetailRoomText;
        private Text userVisitMessageText;
        private Text followingListText;
        private InputField settingsNicknameField;
        private InputField settingsImagePathField;
        private Image settingsProfilePreviewImage;
        private Text settingsProfileNameText;
        private Text settingsNotificationValueText;
        private Text settingsStatusMessageText;
        private GameObject settingsDeletePopup;
        private Text settingsDeletePopupMessageText;
        private Text selectedCategoryText;
        private Text detailTitleText;
        private Text detailAuthorText;
        private Text detailPublisherText;
        private Text detailSummaryText;
        private Text detailShelfText;
        private Text detailMetaText;
        private Text statusGuideText;
        private readonly Text[] reviewTexts = new Text[3];
        private readonly Button[] pageButtons = new Button[5];
        private Button previousPageButton;
        private Button nextPageButton;
        private Image detailCoverImage;
        private int selectedCategoryIndex;
        private int selectedBookIndex;
        private int selectedStatusIndex = 1;
        private int currentPageIndex;
        private int selectedUserIndex;
        private string userSearchQuery = string.Empty;
        private string settingsProfileNickname = "bookmori";
        private string settingsProfileImagePath = "default/profile_blue";
        private bool settingsNotificationsEnabled = true;
        private Color settingsProfileColor = new Color(0.54f, 0.72f, 0.88f, 1f);
        private const int BooksPerPage = 4;
        private Text contentText;
        private bool useImmersiveMode;
        private readonly Button[] tabButtons = new Button[3];
        private string selectedTab = "Books";
        private NotebookUiState currentState = NotebookUiState.ScreenSaver;
        private bool openDashboardOnShow;

        public Transform ScreenTransform { get; private set; }

        private void Awake()
        {
            EnsureAdImages();
            WarnIfAdsMissing();
        }

        public void Configure(Transform screenTransform, Camera eventCamera, UnityAction onCloseDashboard)
        {
            ScreenTransform = screenTransform;
            EnsureCanvases(screenTransform, eventCamera, onCloseDashboard);
            ApplyPresentationMode();
            ApplySelectedAd();
            ShowStateInstant(currentState);
        }

        public void SetImmersiveMode(bool immersive)
        {
            useImmersiveMode = immersive;
            ApplyPresentationMode();
        }

        public void EnterImmersiveDashboard()
        {
            if (screenSaverCanvas == null || dashboardCanvas == null)
            {
                return;
            }

            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }

            openDashboardOnShow = false;
            currentState = NotebookUiState.Dashboard;
            useImmersiveMode = true;
            ApplyPresentationMode();
            ShowStateInstant(NotebookUiState.Dashboard);
        }

        public void Show()
        {
            if (screenSaverCanvas == null || dashboardCanvas == null)
            {
                return;
            }

            if (openDashboardOnShow)
            {
                openDashboardOnShow = false;
                SetState(NotebookUiState.Dashboard, true);
                return;
            }

            ShowStateInstant(currentState);
        }

        public void Hide()
        {
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }

            if (screenSaverCanvas != null)
            {
                screenSaverCanvas.gameObject.SetActive(false);
            }

            if (dashboardCanvas != null)
            {
                dashboardCanvas.gameObject.SetActive(false);
            }
        }

        public void ActivateDashboard(bool animate = true)
        {
            openDashboardOnShow = false;
            SetState(NotebookUiState.Dashboard, animate);
        }

        public void ReturnToScreenSaver(bool animate = true)
        {
            openDashboardOnShow = false;
            SetState(NotebookUiState.ScreenSaver, animate);
        }

        public void QueueDashboardActivation()
        {
            openDashboardOnShow = true;
        }

        private void EnsureCanvases(Transform screenTransform, Camera eventCamera, UnityAction onCloseDashboard)
        {
            if ((screenSaverCanvas != null && dashboardCanvas != null) || screenTransform == null)
            {
                return;
            }

            screenSaverCanvas = CreateCanvas(screenTransform, "ScreenSaverCanvas", eventCamera);
            screenSaverCanvasGroup = screenSaverCanvas.gameObject.AddComponent<CanvasGroup>();
            screenSaverCanvasGroup.alpha = 1f;
            BuildScreenSaver(screenSaverCanvas.transform);

            dashboardCanvas = CreateCanvas(screenTransform, "DashboardCanvas", eventCamera);
            dashboardCanvasGroup = dashboardCanvas.gameObject.AddComponent<CanvasGroup>();
            dashboardCanvasGroup.alpha = currentState == NotebookUiState.Dashboard ? 1f : 0f;
            BuildDashboard(dashboardCanvas.transform, onCloseDashboard);
        }

        private void BuildScreenSaver(Transform parent)
        {
            screenSaverBackground = parent.gameObject.AddComponent<Image>();
            screenSaverBackground.color = new Color(0.07f, 0.09f, 0.12f, 0.98f);

            var adImageObject = new GameObject("AdImage");
            adImageObject.transform.SetParent(parent, false);
            screenSaverImage = adImageObject.AddComponent<Image>();
            screenSaverImageRect = screenSaverImage.rectTransform;
            screenSaverImage.preserveAspect = false;
            screenSaverImage.color = Color.white;
            StretchToFill(screenSaverImageRect);
        }

        private void BuildDashboard(Transform parent, UnityAction onCloseDashboard)
        {
            dashboardRootBackground = parent.gameObject.AddComponent<Image>();
            dashboardRootBackground.color = new Color(0.96f, 0.97f, 0.99f, 0f);

            const float headerHeight = 38f;
            const float toolbarHeight = 44f;
            const float pagePadding = 12f;
            var appFrameSize = new Vector2(DashboardCanvasSize.x * 0.92f, DashboardCanvasSize.y * 0.88f);

            var appFrame = CreatePanel(parent, "AppFrame", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Color(0.96f, 0.97f, 0.99f, 1f));
            dashboardAppFrameRect = appFrame.GetComponent<RectTransform>();
            dashboardAppFrameRect.anchorMin = new Vector2(0.5f, 0.5f);
            dashboardAppFrameRect.anchorMax = new Vector2(0.5f, 0.5f);
            dashboardAppFrameRect.pivot = new Vector2(0.5f, 0.5f);
            dashboardAppFrameRect.anchoredPosition = Vector2.zero;
            dashboardAppFrameRect.sizeDelta = appFrameSize;
            dashboardAppFrameRect.localScale = Vector3.one;

            var headerBar = CreatePanel(appFrame.transform, "HeaderBar", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Color(0.93f, 0.94f, 0.96f, 0.98f));
            StretchToTop(headerBar.GetComponent<RectTransform>(), headerHeight);
            CreateLabel(appFrame.transform, "HeaderTitle", "Notebook", new Vector2(14f, -8f), new Vector2(190f, 18f), 14, TextAnchor.UpperLeft, new Color(0.10f, 0.11f, 0.12f, 1f), new Vector2(0f, 1f));
            CreateLabel(appFrame.transform, "HeaderStatus", "Search  Wi-Fi  10:09", new Vector2(-14f, -8f), new Vector2(158f, 18f), 13, TextAnchor.UpperRight, new Color(0.10f, 0.11f, 0.12f, 1f), new Vector2(1f, 1f));

            var appWindow = CreatePanel(appFrame.transform, "ContentWindow", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Color(0.96f, 0.97f, 0.99f, 1f));
            dashboardContentWindowRect = appWindow.GetComponent<RectTransform>();
            StretchWithOffsets(dashboardContentWindowRect, 0f, 0f, headerHeight, 0f);

            var toolbar = CreatePanel(appWindow.transform, "WindowToolbar", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Color(0.76f, 0.78f, 0.81f, 0.98f));
            StretchToTop(toolbar.GetComponent<RectTransform>(), toolbarHeight);
            var windowBody = CreatePanel(appWindow.transform, "WindowBody", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Color(0.98f, 0.98f, 0.99f, 1f));
            StretchWithOffsets(windowBody.GetComponent<RectTransform>(), 0f, 0f, toolbarHeight, 0f);
            CreateLabel(appWindow.transform, "WindowTitle", "Bookshelf Library", new Vector2(0f, -22f), new Vector2(260f, 26f), 17, TextAnchor.MiddleCenter, new Color(0.12f, 0.13f, 0.15f, 1f), new Vector2(0.5f, 1f), FontStyle.Bold);

            var closeButton = CreateButton(appWindow.transform, "CloseButton", "X", new Vector2(-10f, -6f), onCloseDashboard, new Vector2(32f, 32f), new Vector2(1f, 1f));
            closeButton.GetComponentInChildren<Text>().fontSize = 17;

            tabButtons[0] = CreateButton(appWindow.transform, "BooksTab", "Books", new Vector2(-122f, -76f), () => SelectTab("Books"), new Vector2(118f, 34f), new Vector2(0.5f, 1f));
            tabButtons[1] = CreateButton(appWindow.transform, "UsersTab", "Users", new Vector2(0f, -76f), () => SelectTab("Users"), new Vector2(118f, 34f), new Vector2(0.5f, 1f));
            tabButtons[2] = CreateButton(appWindow.transform, "SettingsTab", "Settings", new Vector2(128f, -76f), () => SelectTab("Settings"), new Vector2(134f, 34f), new Vector2(0.5f, 1f));
            tabButtons[0].GetComponentInChildren<Text>().fontSize = 16;
            tabButtons[1].GetComponentInChildren<Text>().fontSize = 16;
            tabButtons[2].GetComponentInChildren<Text>().fontSize = 16;

            var contentPanel = CreatePanel(windowBody.transform, "ContentPanel", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Color(0.95f, 0.96f, 0.98f, 0.55f));
            StretchWithOffsets(contentPanel.GetComponent<RectTransform>(), pagePadding, pagePadding, 56f, pagePadding);
            BuildBooksContent(contentPanel.transform);
            BuildUsersContent(contentPanel.transform);
            BuildSettingsContent(contentPanel.transform);
            contentText = CreateLabel(windowBody.transform, "ContentText", string.Empty, Vector2.zero, Vector2.zero, 18, TextAnchor.UpperLeft, new Color(0.16f, 0.17f, 0.18f, 1f), new Vector2(0.5f, 0.5f));
            StretchWithOffsets(contentText.GetComponent<RectTransform>(), pagePadding + 16f, pagePadding + 16f, 72f, pagePadding + 12f);

            Refresh();
        }

        private void BuildBooksContent(Transform parent)
        {
            booksContentRoot = new GameObject("BooksContentRoot");
            booksContentRoot.transform.SetParent(parent, false);
            var rootRect = booksContentRoot.AddComponent<RectTransform>();
            StretchToFill(rootRect);

            var leftPanel = CreatePanel(booksContentRoot.transform, "SidebarPanel", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Color(0.97f, 0.98f, 0.99f, 0.92f));
            var leftRect = leftPanel.GetComponent<RectTransform>();
            leftRect.anchorMin = new Vector2(0f, 0f);
            leftRect.anchorMax = new Vector2(0.22f, 1f);
            leftRect.offsetMin = Vector2.zero;
            leftRect.offsetMax = new Vector2(-8f, 0f);

            CreateLabel(leftPanel.transform, "SidebarTitle", "분류", new Vector2(16f, -16f), new Vector2(120f, 24f), 16, TextAnchor.UpperLeft, new Color(0.18f, 0.20f, 0.22f, 1f), new Vector2(0f, 1f), FontStyle.Bold);

            for (var i = 0; i < BookCategories.Length; i++)
            {
                var index = i;
                categoryButtons[i] = CreateButton(leftPanel.transform, $"CategoryButton_{i}", BookCategories[i], new Vector2(14f, -50f - (i * 42f)), () => SelectCategory(index), new Vector2(132f, 34f), new Vector2(0f, 1f));
                categoryButtons[i].GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                categoryButtons[i].GetComponentInChildren<Text>().color = new Color(0.22f, 0.24f, 0.27f, 1f);
            }

            var centerPanel = CreatePanel(booksContentRoot.transform, "LibraryPanel", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Color(0.99f, 0.99f, 1f, 0.95f));
            var centerRect = centerPanel.GetComponent<RectTransform>();
            centerRect.anchorMin = new Vector2(0.22f, 0f);
            centerRect.anchorMax = new Vector2(0.64f, 1f);
            centerRect.offsetMin = new Vector2(8f, 0f);
            centerRect.offsetMax = new Vector2(-8f, 0f);

            selectedCategoryText = CreateLabel(centerPanel.transform, "SelectedCategoryText", "전체", new Vector2(18f, -16f), new Vector2(120f, 22f), 16, TextAnchor.UpperLeft, new Color(0.16f, 0.18f, 0.20f, 1f), new Vector2(0f, 1f), FontStyle.Bold);
            searchField = CreateSearchField(centerPanel.transform, new Vector2(18f, -48f), new Vector2(236f, 34f), "제목, 저자, 출판사 검색");

            for (var i = 0; i < DummyBooks.Length; i++)
            {
                var card = CreatePanel(centerPanel.transform, $"BookItem_{i}", new Vector2(18f, -96f - (i * 56f)), new Vector2(0f, 48f), new Vector2(0f, 1f), new Color(0.97f, 0.98f, 0.99f, 1f));
                var cardRect = card.GetComponent<RectTransform>();
                cardRect.anchorMin = new Vector2(0f, 1f);
                cardRect.anchorMax = new Vector2(1f, 1f);
                cardRect.offsetMin = new Vector2(18f, -144f - (i * 56f));
                cardRect.offsetMax = new Vector2(-18f, -96f - (i * 56f));

                var image = CreatePanel(card.transform, "Thumbnail", new Vector2(12f, -8f), new Vector2(28f, 32f), new Vector2(0f, 1f), DummyBooks[i].CoverColor);
                image.GetComponent<Image>().color = DummyBooks[i].CoverColor;
                CreateLabel(card.transform, "ThumbLetter", DummyBooks[i].Title.Substring(0, 1), new Vector2(26f, -23f), new Vector2(28f, 20f), 14, TextAnchor.MiddleCenter, Color.white, new Vector2(0f, 1f), FontStyle.Bold);
                CreateLabel(card.transform, "BookTitle", DummyBooks[i].Title, new Vector2(52f, -10f), new Vector2(180f, 18f), 15, TextAnchor.UpperLeft, new Color(0.15f, 0.17f, 0.20f, 1f), new Vector2(0f, 1f), FontStyle.Bold);
                CreateLabel(card.transform, "BookAuthor", DummyBooks[i].Author, new Vector2(52f, -28f), new Vector2(180f, 16f), 13, TextAnchor.UpperLeft, new Color(0.40f, 0.43f, 0.47f, 1f), new Vector2(0f, 1f));
                CreateLabel(card.transform, "BookMeta", $"★{DummyBooks[i].Rating:0.0}   조회수 {DummyBooks[i].Views:N0}   리뷰 {DummyBooks[i].ReviewCount}개", new Vector2(52f, -43f), new Vector2(220f, 14f), 11, TextAnchor.UpperLeft, new Color(0.50f, 0.53f, 0.57f, 1f), new Vector2(0f, 1f));

                var button = card.AddComponent<Button>();
                button.targetGraphic = card.GetComponent<Image>();
                var index = i;
                button.onClick.AddListener(() => SelectBook(index));
                bookItemButtons[i] = button;
            }

            var rightPanel = CreatePanel(booksContentRoot.transform, "DetailPanel", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Color(0.97f, 0.98f, 0.99f, 0.96f));
            var rightRect = rightPanel.GetComponent<RectTransform>();
            rightRect.anchorMin = new Vector2(0.64f, 0f);
            rightRect.anchorMax = new Vector2(1f, 1f);
            rightRect.offsetMin = new Vector2(8f, 0f);
            rightRect.offsetMax = Vector2.zero;

            detailCoverImage = CreatePanel(rightPanel.transform, "DetailCover", new Vector2(18f, -18f), new Vector2(104f, 136f), new Vector2(0f, 1f), Color.gray).GetComponent<Image>();
            detailTitleText = CreateLabel(rightPanel.transform, "DetailTitle", string.Empty, new Vector2(136f, -18f), new Vector2(150f, 42f), 19, TextAnchor.UpperLeft, new Color(0.14f, 0.16f, 0.18f, 1f), new Vector2(0f, 1f), FontStyle.Bold);
            detailAuthorText = CreateLabel(rightPanel.transform, "DetailAuthor", string.Empty, new Vector2(136f, -68f), new Vector2(150f, 18f), 14, TextAnchor.UpperLeft, new Color(0.36f, 0.39f, 0.43f, 1f), new Vector2(0f, 1f));
            detailPublisherText = CreateLabel(rightPanel.transform, "DetailPublisher", string.Empty, new Vector2(136f, -90f), new Vector2(150f, 18f), 13, TextAnchor.UpperLeft, new Color(0.46f, 0.49f, 0.53f, 1f), new Vector2(0f, 1f));
            detailMetaText = CreateLabel(rightPanel.transform, "DetailMeta", string.Empty, new Vector2(18f, -162f), new Vector2(250f, 18f), 13, TextAnchor.UpperLeft, new Color(0.28f, 0.31f, 0.35f, 1f), new Vector2(0f, 1f), FontStyle.Bold);
            CreateLabel(rightPanel.transform, "SummaryHeader", "줄거리", new Vector2(18f, -172f), new Vector2(80f, 20f), 14, TextAnchor.UpperLeft, new Color(0.17f, 0.19f, 0.22f, 1f), new Vector2(0f, 1f), FontStyle.Bold);
            detailSummaryText = CreateLabel(rightPanel.transform, "DetailSummary", string.Empty, new Vector2(18f, -198f), new Vector2(266f, 66f), 13, TextAnchor.UpperLeft, new Color(0.30f, 0.33f, 0.36f, 1f), new Vector2(0f, 1f));
            CreateLabel(rightPanel.transform, "ReviewHeader", "리뷰 미리보기", new Vector2(18f, -274f), new Vector2(100f, 20f), 14, TextAnchor.UpperLeft, new Color(0.17f, 0.19f, 0.22f, 1f), new Vector2(0f, 1f), FontStyle.Bold);
            for (var i = 0; i < reviewTexts.Length; i++)
            {
                reviewTexts[i] = CreateLabel(rightPanel.transform, $"ReviewText_{i}", string.Empty, new Vector2(18f, -300f - (i * 36f)), new Vector2(266f, 30f), 12, TextAnchor.UpperLeft, new Color(0.34f, 0.37f, 0.41f, 1f), new Vector2(0f, 1f));
            }

            var moreReviewsButton = CreateButton(rightPanel.transform, "MoreReviewsButton", "리뷰 더보기", new Vector2(18f, -410f), () => { }, new Vector2(96f, 28f), new Vector2(0f, 1f));
            moreReviewsButton.GetComponent<Image>().color = new Color(0.90f, 0.93f, 0.97f, 1f);
            moreReviewsButton.GetComponentInChildren<Text>().fontSize = 12;
            CreateLabel(rightPanel.transform, "StatusHeader", "독서 상태", new Vector2(18f, -446f), new Vector2(90f, 20f), 14, TextAnchor.UpperLeft, new Color(0.17f, 0.19f, 0.22f, 1f), new Vector2(0f, 1f), FontStyle.Bold);

            for (var i = 0; i < ReadingStatuses.Length; i++)
            {
                var statusIndex = i;
                statusButtons[i] = CreateButton(rightPanel.transform, $"StatusButton_{i}", ReadingStatuses[i], () => { }, new Vector2(18f + (i * 88f), -474f), new Vector2(78f, 30f), new Vector2(0f, 1f));
                statusButtons[i].GetComponentInChildren<Text>().fontSize = 13;
                statusButtons[i].interactable = false;
            }

            statusGuideText = CreateLabel(rightPanel.transform, "StatusGuideText", "독서 상태는 별도 화면에서 설정할 수 있습니다", new Vector2(18f, -510f), new Vector2(250f, 18f), 12, TextAnchor.UpperLeft, new Color(0.53f, 0.56f, 0.60f, 1f), new Vector2(0f, 1f));
            detailShelfText = CreateLabel(rightPanel.transform, "DetailShelf", string.Empty, new Vector2(18f, -536f), new Vector2(180f, 18f), 13, TextAnchor.UpperLeft, new Color(0.41f, 0.44f, 0.48f, 1f), new Vector2(0f, 1f));
            var addButton = CreateButton(rightPanel.transform, "AddToShelfButton", "내 책장에 추가", () => { }, new Vector2(18f, -566f), new Vector2(248f, 38f), new Vector2(0f, 1f));
            addButton.GetComponent<Image>().color = new Color(0.18f, 0.46f, 0.84f, 0.96f);
            addButton.GetComponentInChildren<Text>().fontSize = 15;
            addButton.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
            statusGuideText.text = string.Empty;

            previousPageButton = CreateButton(centerPanel.transform, "PreviousPageButton", "<", new Vector2(18f, 14f), () => ChangePage(-1), new Vector2(28f, 24f), new Vector2(0f, 0f));
            previousPageButton.GetComponentInChildren<Text>().fontSize = 13;
            nextPageButton = CreateButton(centerPanel.transform, "NextPageButton", ">", new Vector2(250f, 14f), () => ChangePage(1), new Vector2(28f, 24f), new Vector2(0f, 0f));
            nextPageButton.GetComponentInChildren<Text>().fontSize = 13;

            for (var i = 0; i < pageButtons.Length; i++)
            {
                var pageIndex = i;
                pageButtons[i] = CreateButton(centerPanel.transform, $"PageButton_{i}", (i + 1).ToString(), new Vector2(54f + (i * 34f), 14f), () => SelectPage(pageIndex), new Vector2(28f, 24f), new Vector2(0f, 0f));
                pageButtons[i].GetComponentInChildren<Text>().fontSize = 12;
            }

            SetupDetailScrollContent(rightPanel.transform);
        }

        private void BuildUsersContent(Transform parent)
        {
            usersContentRoot = new GameObject("UsersContentRoot");
            usersContentRoot.transform.SetParent(parent, false);
            var rootRect = usersContentRoot.AddComponent<RectTransform>();
            StretchToFill(rootRect);

            var leftPanel = CreatePanel(usersContentRoot.transform, "UsersListPanel", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Color(0.98f, 0.99f, 1f, 0.96f));
            var leftRect = leftPanel.GetComponent<RectTransform>();
            leftRect.anchorMin = new Vector2(0f, 0f);
            leftRect.anchorMax = new Vector2(0.60f, 1f);
            leftRect.offsetMin = Vector2.zero;
            leftRect.offsetMax = new Vector2(-8f, 0f);

            CreateLabel(leftPanel.transform, "UsersHeader", "User Search", new Vector2(18f, -16f), new Vector2(180f, 24f), 16, TextAnchor.UpperLeft, new Color(0.16f, 0.18f, 0.20f, 1f), new Vector2(0f, 1f), FontStyle.Bold);
            userSearchField = CreateSearchField(leftPanel.transform, new Vector2(18f, -48f), new Vector2(210f, 34f), "닉네임 검색");
            userSearchField.onValueChanged.AddListener(OnUserSearchChanged);

            var searchButton = CreateButton(leftPanel.transform, "UserSearchButton", "검색", new Vector2(-18f, -48f), ApplyUserSearch, new Vector2(64f, 34f), new Vector2(1f, 1f));
            searchButton.GetComponent<Image>().color = new Color(0.22f, 0.45f, 0.84f, 0.96f);
            searchButton.GetComponentInChildren<Text>().fontSize = 14;
            searchButton.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;

            var listViewport = CreateScrollViewport(leftPanel.transform, "UsersListViewport");
            StretchWithOffsets(listViewport.GetComponent<RectTransform>(), 12f, 12f, 94f, 12f);
            var listContent = CreateScrollContent(listViewport.transform, "UsersListContent", DummyUsers.Length * 64f + 72f);

            for (var i = 0; i < DummyUsers.Length; i++)
            {
                var card = CreatePanel(listContent.transform, $"UserItem_{i}", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Color(0.97f, 0.98f, 0.99f, 1f));
                var cardRect = card.GetComponent<RectTransform>();
                cardRect.anchorMin = new Vector2(0f, 1f);
                cardRect.anchorMax = new Vector2(1f, 1f);
                cardRect.offsetMin = new Vector2(6f, -62f - (i * 64f));
                cardRect.offsetMax = new Vector2(-6f, -6f - (i * 64f));

                userProfileImages[i] = CreatePanel(card.transform, "ProfileImage", new Vector2(12f, -10f), new Vector2(40f, 40f), new Vector2(0f, 1f), DummyUsers[i].ProfileColor).GetComponent<Image>();
                CreateLabel(card.transform, "ProfileLetter", DummyUsers[i].Nickname.Substring(0, 1).ToUpperInvariant(), new Vector2(32f, -30f), new Vector2(26f, 20f), 14, TextAnchor.MiddleCenter, Color.white, new Vector2(0f, 1f), FontStyle.Bold);
                userNameTexts[i] = CreateLabel(card.transform, "Nickname", string.Empty, new Vector2(64f, -10f), new Vector2(140f, 18f), 14, TextAnchor.UpperLeft, new Color(0.15f, 0.17f, 0.20f, 1f), new Vector2(0f, 1f), FontStyle.Bold);
                userIntroTexts[i] = CreateLabel(card.transform, "Intro", string.Empty, new Vector2(64f, -30f), new Vector2(188f, 26f), 12, TextAnchor.UpperLeft, new Color(0.42f, 0.45f, 0.49f, 1f), new Vector2(0f, 1f));

                var cardButton = card.AddComponent<Button>();
                cardButton.targetGraphic = card.GetComponent<Image>();
                var slotIndex = i;
                cardButton.onClick.AddListener(() => SelectVisibleUser(slotIndex));

                userFollowButtons[i] = CreateButton(card.transform, "FollowButton", "팔로우", new Vector2(-96f, -14f), () => ToggleFollowVisibleUser(slotIndex), new Vector2(78f, 26f), new Vector2(1f, 1f));
                userFollowButtons[i].GetComponentInChildren<Text>().fontSize = 12;

                userVisitButtons[i] = CreateButton(card.transform, "VisitButton", "공간 방문", new Vector2(-10f, -14f), () => VisitVisibleUser(slotIndex), new Vector2(78f, 26f), new Vector2(1f, 1f));
                userVisitButtons[i].GetComponent<Image>().color = new Color(0.91f, 0.93f, 0.97f, 1f);
                userVisitButtons[i].GetComponentInChildren<Text>().fontSize = 12;
                userVisitButtons[i].GetComponentInChildren<Text>().color = new Color(0.22f, 0.25f, 0.29f, 1f);

                userItemCards[i] = card;
            }

            var rightPanel = CreatePanel(usersContentRoot.transform, "UsersDetailPanel", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Color(0.97f, 0.98f, 0.99f, 0.96f));
            var rightRect = rightPanel.GetComponent<RectTransform>();
            rightRect.anchorMin = new Vector2(0.60f, 0f);
            rightRect.anchorMax = new Vector2(1f, 1f);
            rightRect.offsetMin = new Vector2(8f, 0f);
            rightRect.offsetMax = Vector2.zero;

            CreateLabel(rightPanel.transform, "SelectedUserHeader", "Selected User", new Vector2(18f, -16f), new Vector2(160f, 24f), 16, TextAnchor.UpperLeft, new Color(0.16f, 0.18f, 0.20f, 1f), new Vector2(0f, 1f), FontStyle.Bold);
            userDetailProfileImage = CreatePanel(rightPanel.transform, "UserDetailProfile", new Vector2(18f, -52f), new Vector2(72f, 72f), new Vector2(0f, 1f), DummyUsers[0].ProfileColor).GetComponent<Image>();
            userDetailNicknameText = CreateLabel(rightPanel.transform, "UserDetailNickname", string.Empty, new Vector2(104f, -52f), new Vector2(156f, 22f), 17, TextAnchor.UpperLeft, new Color(0.14f, 0.16f, 0.18f, 1f), new Vector2(0f, 1f), FontStyle.Bold);
            userDetailIntroText = CreateLabel(rightPanel.transform, "UserDetailIntro", string.Empty, new Vector2(104f, -78f), new Vector2(160f, 40f), 12, TextAnchor.UpperLeft, new Color(0.39f, 0.42f, 0.46f, 1f), new Vector2(0f, 1f));
            userDetailRoomText = CreateLabel(rightPanel.transform, "UserDetailRoom", string.Empty, new Vector2(18f, -136f), new Vector2(250f, 18f), 13, TextAnchor.UpperLeft, new Color(0.24f, 0.28f, 0.33f, 1f), new Vector2(0f, 1f), FontStyle.Bold);
            userVisitMessageText = CreateLabel(rightPanel.transform, "UserVisitMessage", "사용자를 선택해 공간 흐름을 확인하세요.", new Vector2(18f, -164f), new Vector2(252f, 36f), 12, TextAnchor.UpperLeft, new Color(0.45f, 0.48f, 0.52f, 1f), new Vector2(0f, 1f));

            CreateLabel(rightPanel.transform, "FollowingHeader", "Following", new Vector2(18f, -222f), new Vector2(120f, 22f), 15, TextAnchor.UpperLeft, new Color(0.16f, 0.18f, 0.20f, 1f), new Vector2(0f, 1f), FontStyle.Bold);
            followingListText = CreateLabel(rightPanel.transform, "FollowingList", string.Empty, new Vector2(18f, -250f), new Vector2(252f, 150f), 12, TextAnchor.UpperLeft, new Color(0.34f, 0.37f, 0.41f, 1f), new Vector2(0f, 1f));
        }

        private void BuildSettingsContent(Transform parent)
        {
            settingsContentRoot = new GameObject("SettingsContentRoot");
            settingsContentRoot.transform.SetParent(parent, false);
            var rootRect = settingsContentRoot.AddComponent<RectTransform>();
            StretchToFill(rootRect);

            var settingsPanel = CreatePanel(settingsContentRoot.transform, "SettingsPanel", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Color(0.98f, 0.99f, 1f, 0.96f));
            StretchToFill(settingsPanel.GetComponent<RectTransform>());

            CreateLabel(settingsPanel.transform, "SettingsHeader", "Settings", new Vector2(18f, -16f), new Vector2(180f, 24f), 16, TextAnchor.UpperLeft, new Color(0.16f, 0.18f, 0.20f, 1f), new Vector2(0f, 1f), FontStyle.Bold);

            var profileSection = CreatePanel(settingsPanel.transform, "ProfileSection", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Color(0.97f, 0.98f, 0.99f, 1f));
            var profileRect = profileSection.GetComponent<RectTransform>();
            profileRect.anchorMin = new Vector2(0f, 1f);
            profileRect.anchorMax = new Vector2(1f, 1f);
            profileRect.offsetMin = new Vector2(18f, -192f);
            profileRect.offsetMax = new Vector2(-18f, -52f);

            CreateLabel(profileSection.transform, "ProfileSectionTitle", "프로필 편집", new Vector2(16f, -14f), new Vector2(120f, 22f), 15, TextAnchor.UpperLeft, new Color(0.15f, 0.17f, 0.20f, 1f), new Vector2(0f, 1f), FontStyle.Bold);
            settingsProfilePreviewImage = CreatePanel(profileSection.transform, "ProfilePreview", new Vector2(16f, -48f), new Vector2(72f, 72f), new Vector2(0f, 1f), settingsProfileColor).GetComponent<Image>();
            settingsProfileNameText = CreateLabel(profileSection.transform, "ProfilePreviewName", settingsProfileNickname, new Vector2(104f, -52f), new Vector2(180f, 22f), 16, TextAnchor.UpperLeft, new Color(0.15f, 0.17f, 0.20f, 1f), new Vector2(0f, 1f), FontStyle.Bold);
            var changeImageButton = CreateButton(profileSection.transform, "ChangeImageButton", "이미지 변경", new Vector2(16f, -128f), ApplySettingsImagePreview, new Vector2(96f, 28f), new Vector2(0f, 1f));
            changeImageButton.GetComponent<Image>().color = new Color(0.91f, 0.93f, 0.97f, 1f);
            changeImageButton.GetComponentInChildren<Text>().fontSize = 12;
            changeImageButton.GetComponentInChildren<Text>().color = new Color(0.22f, 0.25f, 0.29f, 1f);

            CreateLabel(profileSection.transform, "NicknameLabel", "닉네임", new Vector2(16f, -138f), new Vector2(72f, 18f), 12, TextAnchor.UpperLeft, new Color(0.40f, 0.43f, 0.47f, 1f), new Vector2(0f, 1f));
            settingsNicknameField = CreateSearchField(profileSection.transform, new Vector2(16f, -160f), new Vector2(212f, 32f), "닉네임 입력");
            settingsNicknameField.text = settingsProfileNickname;

            CreateLabel(profileSection.transform, "ImagePathLabel", "이미지 경로", new Vector2(244f, -138f), new Vector2(88f, 18f), 12, TextAnchor.UpperLeft, new Color(0.40f, 0.43f, 0.47f, 1f), new Vector2(0f, 1f));
            settingsImagePathField = CreateSearchField(profileSection.transform, new Vector2(244f, -160f), new Vector2(210f, 32f), "기본 이미지 경로");
            settingsImagePathField.text = settingsProfileImagePath;
            settingsImagePathField.onValueChanged.AddListener(OnSettingsImagePathChanged);

            var saveButton = CreateButton(profileSection.transform, "ProfileSaveButton", "저장", new Vector2(-16f, -160f), SaveSettingsProfile, new Vector2(78f, 32f), new Vector2(1f, 1f));
            saveButton.GetComponent<Image>().color = new Color(0.22f, 0.45f, 0.84f, 0.96f);
            saveButton.GetComponentInChildren<Text>().fontSize = 13;
            saveButton.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;

            var cancelButton = CreateButton(profileSection.transform, "ProfileCancelButton", "취소", new Vector2(-100f, -160f), ResetSettingsProfileInputs, new Vector2(78f, 32f), new Vector2(1f, 1f));
            cancelButton.GetComponent<Image>().color = new Color(0.91f, 0.93f, 0.97f, 1f);
            cancelButton.GetComponentInChildren<Text>().fontSize = 13;
            cancelButton.GetComponentInChildren<Text>().color = new Color(0.22f, 0.25f, 0.29f, 1f);

            var notificationSection = CreatePanel(settingsPanel.transform, "NotificationSection", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Color(0.97f, 0.98f, 0.99f, 1f));
            var notificationRect = notificationSection.GetComponent<RectTransform>();
            notificationRect.anchorMin = new Vector2(0f, 1f);
            notificationRect.anchorMax = new Vector2(1f, 1f);
            notificationRect.offsetMin = new Vector2(18f, -276f);
            notificationRect.offsetMax = new Vector2(-18f, -204f);

            CreateLabel(notificationSection.transform, "NotificationTitle", "알림 설정", new Vector2(16f, -14f), new Vector2(120f, 22f), 15, TextAnchor.UpperLeft, new Color(0.15f, 0.17f, 0.20f, 1f), new Vector2(0f, 1f), FontStyle.Bold);
            CreateLabel(notificationSection.transform, "NotificationLabel", "알림 받기", new Vector2(16f, -42f), new Vector2(88f, 18f), 13, TextAnchor.UpperLeft, new Color(0.33f, 0.36f, 0.40f, 1f), new Vector2(0f, 1f));
            settingsNotificationValueText = CreateLabel(notificationSection.transform, "NotificationValue", string.Empty, new Vector2(112f, -42f), new Vector2(80f, 18f), 13, TextAnchor.UpperLeft, new Color(0.20f, 0.44f, 0.80f, 1f), new Vector2(0f, 1f), FontStyle.Bold);
            var notificationToggleButton = CreateButton(notificationSection.transform, "NotificationToggleButton", "토글", new Vector2(-16f, -34f), ToggleSettingsNotifications, new Vector2(74f, 30f), new Vector2(1f, 1f));
            notificationToggleButton.GetComponent<Image>().color = new Color(0.90f, 0.93f, 0.97f, 1f);
            notificationToggleButton.GetComponentInChildren<Text>().fontSize = 12;
            notificationToggleButton.GetComponentInChildren<Text>().color = new Color(0.22f, 0.25f, 0.29f, 1f);

            var accountSection = CreatePanel(settingsPanel.transform, "AccountSection", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Color(0.97f, 0.98f, 0.99f, 1f));
            var accountRect = accountSection.GetComponent<RectTransform>();
            accountRect.anchorMin = new Vector2(0f, 1f);
            accountRect.anchorMax = new Vector2(1f, 1f);
            accountRect.offsetMin = new Vector2(18f, -388f);
            accountRect.offsetMax = new Vector2(-18f, -288f);

            CreateLabel(accountSection.transform, "AccountSectionTitle", "계정 관리", new Vector2(16f, -14f), new Vector2(120f, 22f), 15, TextAnchor.UpperLeft, new Color(0.15f, 0.17f, 0.20f, 1f), new Vector2(0f, 1f), FontStyle.Bold);
            var logoutButton = CreateButton(accountSection.transform, "LogoutButton", "로그아웃", new Vector2(16f, -54f), LogoutSettingsAccount, new Vector2(110f, 34f), new Vector2(0f, 1f));
            logoutButton.GetComponent<Image>().color = new Color(0.91f, 0.93f, 0.97f, 1f);
            logoutButton.GetComponentInChildren<Text>().color = new Color(0.22f, 0.25f, 0.29f, 1f);

            var deleteButton = CreateButton(accountSection.transform, "DeleteAccountButton", "계정 삭제", new Vector2(136f, -54f), ShowSettingsDeletePopup, new Vector2(110f, 34f), new Vector2(0f, 1f));
            deleteButton.GetComponent<Image>().color = new Color(0.87f, 0.42f, 0.40f, 0.96f);
            deleteButton.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;

            settingsStatusMessageText = CreateLabel(settingsPanel.transform, "SettingsStatusMessage", "설정 흐름을 확인할 수 있습니다.", new Vector2(18f, -408f), new Vector2(420f, 18f), 12, TextAnchor.UpperLeft, new Color(0.40f, 0.43f, 0.47f, 1f), new Vector2(0f, 1f));

            profileSection.transform.Find("NicknameLabel")?.gameObject.SetActive(false);
            profileSection.transform.Find("ImagePathLabel")?.gameObject.SetActive(false);
            settingsStatusMessageText.gameObject.SetActive(false);
            settingsDeletePopup = CreatePanel(settingsPanel.transform, "DeletePopup", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Color(0f, 0f, 0f, 0.22f));
            StretchToFill(settingsDeletePopup.GetComponent<RectTransform>());

            var popupCard = CreatePanel(settingsDeletePopup.transform, "DeletePopupCard", Vector2.zero, new Vector2(320f, 168f), new Vector2(0.5f, 0.5f), new Color(0.98f, 0.99f, 1f, 1f));
            settingsDeletePopupMessageText = CreateLabel(popupCard.transform, "DeletePopupMessage", "정말 계정을 삭제하시겠습니까?", new Vector2(24f, -34f), new Vector2(272f, 42f), 15, TextAnchor.UpperLeft, new Color(0.15f, 0.17f, 0.20f, 1f), new Vector2(0f, 1f), FontStyle.Bold);
            var cancelDeleteButton = CreateButton(popupCard.transform, "DeletePopupCancel", "취소", new Vector2(24f, -118f), HideSettingsDeletePopup, new Vector2(120f, 34f), new Vector2(0f, 1f));
            cancelDeleteButton.GetComponent<Image>().color = new Color(0.91f, 0.93f, 0.97f, 1f);
            cancelDeleteButton.GetComponentInChildren<Text>().color = new Color(0.22f, 0.25f, 0.29f, 1f);

            var confirmDeleteButton = CreateButton(popupCard.transform, "DeletePopupConfirm", "확인", new Vector2(176f, -118f), ConfirmSettingsDelete, new Vector2(120f, 34f), new Vector2(0f, 1f));
            confirmDeleteButton.GetComponent<Image>().color = new Color(0.87f, 0.42f, 0.40f, 0.96f);
            confirmDeleteButton.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;

            settingsDeletePopup.SetActive(false);
            RefreshSettingsTab();
        }

        private void ApplyCanvasTransform(Transform canvasTransform)
        {
            if (canvasTransform == null)
            {
                return;
            }

            var depthOffset = canvasTransform.name == "DashboardCanvas" ? -0.0008f : -0.0004f;
            canvasTransform.localPosition = new Vector3(0f, -0.022f, depthOffset);
            canvasTransform.localRotation = Quaternion.Euler(-90f, 0f, 180f);
            canvasTransform.localScale = Vector3.one * CanvasScale;
        }

        private void ApplyPresentationMode()
        {
            ApplyCanvasPresentation(screenSaverCanvas, false);
            ApplyCanvasPresentation(dashboardCanvas, true);
        }

        private void ApplyCanvasPresentation(Canvas canvas, bool isDashboard)
        {
            if (canvas == null)
            {
                return;
            }

            var rect = canvas.GetComponent<RectTransform>();
            if (useImmersiveMode)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                canvas.overrideSorting = true;
                canvas.sortingOrder = isDashboard ? 210 : 209;
                StretchToFill(rect);
                ApplyImmersiveLayout(isDashboard);
                return;
            }

            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.overrideSorting = true;
            canvas.sortingOrder = isDashboard ? 2 : 1;
            rect.sizeDelta = isDashboard ? DashboardCanvasSize : DefaultCanvasSize;
            ApplyCanvasTransform(canvas.transform);
            ApplyWorldLayout(isDashboard);
        }

        private void ApplyImmersiveLayout(bool isDashboard)
        {
            if (isDashboard)
            {
                if (dashboardRootBackground != null)
                {
                    dashboardRootBackground.color = new Color(0f, 0f, 0f, 0f);
                }

                if (dashboardAppFrameRect != null)
                {
                    StretchWithOffsets(dashboardAppFrameRect, 36f, 36f, 26f, 36f);
                }

                if (dashboardContentWindowRect != null)
                {
                    StretchWithOffsets(dashboardContentWindowRect, 0f, 0f, 38f, 0f);
                }

                return;
            }

            if (screenSaverBackground != null)
            {
                screenSaverBackground.color = new Color(0f, 0f, 0f, 0f);
            }

            if (screenSaverImageRect != null)
            {
                StretchWithOffsets(screenSaverImageRect, 28f, 28f, 28f, 28f);
            }
        }

        private void ApplyWorldLayout(bool isDashboard)
        {
            if (isDashboard)
            {
                if (dashboardRootBackground != null)
                {
                    dashboardRootBackground.color = new Color(0.96f, 0.97f, 0.99f, 0f);
                }

                if (dashboardAppFrameRect != null)
                {
                    dashboardAppFrameRect.anchorMin = new Vector2(0.5f, 0.5f);
                    dashboardAppFrameRect.anchorMax = new Vector2(0.5f, 0.5f);
                    dashboardAppFrameRect.pivot = new Vector2(0.5f, 0.5f);
                    dashboardAppFrameRect.anchoredPosition = Vector2.zero;
                    dashboardAppFrameRect.sizeDelta = new Vector2(DashboardCanvasSize.x * 0.92f, DashboardCanvasSize.y * 0.88f);
                    dashboardAppFrameRect.localScale = Vector3.one;
                }

                if (dashboardContentWindowRect != null)
                {
                    StretchWithOffsets(dashboardContentWindowRect, 0f, 0f, 38f, 0f);
                }

                return;
            }

            if (screenSaverBackground != null)
            {
                screenSaverBackground.color = new Color(0.07f, 0.09f, 0.12f, 0.98f);
            }

            if (screenSaverImageRect != null)
            {
                StretchToFill(screenSaverImageRect);
            }
        }

        private void SetState(NotebookUiState nextState, bool animate)
        {
            if (screenSaverCanvas == null || dashboardCanvas == null)
            {
                return;
            }

            if (nextState == NotebookUiState.ScreenSaver)
            {
                ApplySelectedAd();
            }

            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }

            var nextCanvas = GetCanvas(nextState);
            var nextGroup = GetCanvasGroup(nextState);
            var currentCanvas = GetCanvas(currentState);
            var currentGroup = GetCanvasGroup(currentState);

            if (!animate || currentCanvas == null || !currentCanvas.gameObject.activeSelf)
            {
                currentState = nextState;
                ShowStateInstant(nextState);
                return;
            }

            currentState = nextState;
            transitionRoutine = StartCoroutine(TransitionCanvas(currentCanvas, currentGroup, nextCanvas, nextGroup));
        }

        private IEnumerator TransitionCanvas(Canvas fromCanvas, CanvasGroup fromGroup, Canvas toCanvas, CanvasGroup toGroup)
        {
            yield return FadeCanvas(fromCanvas, fromGroup, 1f, 0f, 1f, HiddenScaleMultiplier);
            fromCanvas.gameObject.SetActive(false);
            toCanvas.gameObject.SetActive(true);
            yield return FadeCanvas(toCanvas, toGroup, 0f, 1f, HiddenScaleMultiplier, 1f);
            transitionRoutine = null;
        }

        private IEnumerator FadeCanvas(Canvas canvas, CanvasGroup group, float fromAlpha, float toAlpha, float fromScaleMultiplier, float toScaleMultiplier)
        {
            if (canvas == null || group == null)
            {
                yield break;
            }

            canvas.gameObject.SetActive(true);
            var canvasTransform = canvas.transform;
            var elapsed = 0f;
            while (elapsed < TransitionDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / TransitionDuration);
                var eased = Mathf.SmoothStep(0f, 1f, t);
                group.alpha = Mathf.Lerp(fromAlpha, toAlpha, eased);
                if (!useImmersiveMode)
                {
                    var scale = CanvasScale * Mathf.Lerp(fromScaleMultiplier, toScaleMultiplier, eased);
                    canvasTransform.localScale = Vector3.one * scale;
                }
                yield return null;
            }

            group.alpha = toAlpha;
            canvasTransform.localScale = useImmersiveMode
                ? Vector3.one
                : Vector3.one * (CanvasScale * toScaleMultiplier);
        }

        private void ShowStateInstant(NotebookUiState state)
        {
            if (screenSaverCanvas == null || dashboardCanvas == null)
            {
                return;
            }

            if (state == NotebookUiState.ScreenSaver)
            {
                ApplySelectedAd();
            }

            var activeCanvas = GetCanvas(state);
            var activeGroup = GetCanvasGroup(state);
            var inactiveCanvas = GetCanvas(state == NotebookUiState.ScreenSaver ? NotebookUiState.Dashboard : NotebookUiState.ScreenSaver);
            var inactiveGroup = GetCanvasGroup(state == NotebookUiState.ScreenSaver ? NotebookUiState.Dashboard : NotebookUiState.ScreenSaver);

            if (inactiveCanvas != null)
            {
                inactiveCanvas.gameObject.SetActive(false);
            }

            if (inactiveGroup != null)
            {
                inactiveGroup.alpha = 0f;
            }

            if (activeCanvas != null)
            {
                activeCanvas.gameObject.SetActive(true);
                activeCanvas.transform.localScale = useImmersiveMode ? Vector3.one : Vector3.one * CanvasScale;
            }

            if (activeGroup != null)
            {
                activeGroup.alpha = 1f;
            }

            if (state == NotebookUiState.Dashboard)
            {
                Refresh();
            }
        }

        private void SelectTab(string tab)
        {
            selectedTab = tab;
            Refresh();
        }

        private void Refresh()
        {
            if (contentText == null)
            {
                return;
            }

            var isBooks = selectedTab == "Books";
            var isUsers = selectedTab == "Users";
            var isSettings = selectedTab == "Settings";
            if (booksContentRoot != null)
            {
                booksContentRoot.SetActive(isBooks);
            }

            if (usersContentRoot != null)
            {
                usersContentRoot.SetActive(isUsers);
            }

            if (settingsContentRoot != null)
            {
                settingsContentRoot.SetActive(isSettings);
            }

            contentText.gameObject.SetActive(!isBooks && !isUsers && !isSettings);

            switch (selectedTab)
            {
                case "Users":
                    RefreshUsersTab();
                    break;
                case "Settings":
                    RefreshSettingsTab();
                    break;
                default:
                    RefreshBooksTab();
                    break;
            }

            UpdateTabButton(tabButtons[0], selectedTab == "Books");
            UpdateTabButton(tabButtons[1], selectedTab == "Users");
            UpdateTabButton(tabButtons[2], selectedTab == "Settings");
        }

        private void RefreshBooksTab()
        {
            if (selectedCategoryText != null)
            {
                selectedCategoryText.text = BookCategories[Mathf.Clamp(selectedCategoryIndex, 0, BookCategories.Length - 1)];
            }

            for (var i = 0; i < categoryButtons.Length; i++)
            {
                if (categoryButtons[i] == null)
                {
                    continue;
                }

                var isSelected = i == selectedCategoryIndex;
                categoryButtons[i].GetComponent<Image>().color = isSelected
                    ? new Color(0.85f, 0.91f, 0.98f, 1f)
                    : new Color(0.95f, 0.96f, 0.98f, 0f);
                categoryButtons[i].GetComponentInChildren<Text>().fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal;
            }

            for (var i = 0; i < bookItemButtons.Length; i++)
            {
                if (bookItemButtons[i] == null)
                {
                    continue;
                }

                var pageStart = currentPageIndex * BooksPerPage;
                var pageEnd = Mathf.Min(pageStart + BooksPerPage, DummyBooks.Length);
                var isVisible = i >= pageStart && i < pageEnd;
                bookItemButtons[i].gameObject.SetActive(isVisible);
                if (!isVisible)
                {
                    continue;
                }

                var listedBook = DummyBooks[i];
                var titleLabel = bookItemButtons[i].transform.Find("BookTitle")?.GetComponent<Text>();
                if (titleLabel != null)
                {
                    titleLabel.text = listedBook.Title;
                }

                var authorLabel = bookItemButtons[i].transform.Find("BookAuthor")?.GetComponent<Text>();
                if (authorLabel != null)
                {
                    authorLabel.text = listedBook.Author;
                }

                var publisherLabel = bookItemButtons[i].transform.Find("BookMeta")?.GetComponent<Text>();
                if (publisherLabel != null)
                {
                    publisherLabel.text = listedBook.Publisher;
                    publisherLabel.fontSize = 12;
                }

                var isSelected = i == selectedBookIndex;
                bookItemButtons[i].GetComponent<Image>().color = isSelected
                    ? new Color(0.90f, 0.95f, 0.99f, 1f)
                    : new Color(0.97f, 0.98f, 0.99f, 1f);
            }

            var totalPages = Mathf.CeilToInt((float)DummyBooks.Length / BooksPerPage);
            if (previousPageButton != null)
            {
                previousPageButton.interactable = currentPageIndex > 0;
            }

            if (nextPageButton != null)
            {
                nextPageButton.interactable = currentPageIndex < totalPages - 1;
            }

            for (var i = 0; i < pageButtons.Length; i++)
            {
                if (pageButtons[i] == null)
                {
                    continue;
                }

                var isVisible = i < totalPages;
                pageButtons[i].gameObject.SetActive(isVisible);
                if (!isVisible)
                {
                    continue;
                }

                var isCurrent = i == currentPageIndex;
                pageButtons[i].GetComponent<Image>().color = isCurrent
                    ? new Color(0.83f, 0.90f, 0.99f, 1f)
                    : new Color(0.93f, 0.94f, 0.96f, 1f);
                pageButtons[i].GetComponentInChildren<Text>().fontStyle = isCurrent ? FontStyle.Bold : FontStyle.Normal;
            }

            var book = DummyBooks[Mathf.Clamp(selectedBookIndex, 0, DummyBooks.Length - 1)];
            if (detailCoverImage != null)
            {
                detailCoverImage.color = book.CoverColor;
            }

            if (detailTitleText != null)
            {
                detailTitleText.text = book.Title;
            }

            if (detailAuthorText != null)
            {
                detailAuthorText.text = book.Author;
            }

            if (detailPublisherText != null)
            {
                detailPublisherText.text = $"출판사  {book.Publisher}";
            }

            if (detailMetaText != null)
            {
                detailMetaText.text = $"★ {book.Rating:0.0}   리뷰 {book.ReviewCount}개   조회수 {book.Views:N0}";
            }

            if (detailSummaryText != null)
            {
                detailSummaryText.text = book.Summary;
            }

            for (var i = 0; i < reviewTexts.Length; i++)
            {
                if (reviewTexts[i] == null)
                {
                    continue;
                }

                reviewTexts[i].text = i < book.Reviews.Length
                    ? $"{book.Reviews[i].Author}  ★{book.Reviews[i].Rating:0.0}  {book.Reviews[i].Line}"
                    : string.Empty;
            }

            if (detailShelfText != null)
            {
                detailShelfText.text = $"현재 분류  {book.Shelf}";
            }

            for (var i = 0; i < statusButtons.Length; i++)
            {
                if (statusButtons[i] == null)
                {
                    continue;
                }

                var isSelected = i == selectedStatusIndex;
                statusButtons[i].GetComponent<Image>().color = isSelected
                    ? new Color(0.88f, 0.89f, 0.91f, 1f)
                    : new Color(0.93f, 0.94f, 0.96f, 1f);
                statusButtons[i].GetComponentInChildren<Text>().color = new Color(0.55f, 0.57f, 0.61f, 1f);
                statusButtons[i].GetComponentInChildren<Text>().fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        private void SelectCategory(int index)
        {
            selectedCategoryIndex = Mathf.Clamp(index, 0, BookCategories.Length - 1);
            Refresh();
        }

        private void SelectBook(int index)
        {
            selectedBookIndex = Mathf.Clamp(index, 0, DummyBooks.Length - 1);
            currentPageIndex = Mathf.Clamp(selectedBookIndex / BooksPerPage, 0, Mathf.Max(0, Mathf.CeilToInt((float)DummyBooks.Length / BooksPerPage) - 1));
            selectedStatusIndex = System.Array.IndexOf(ReadingStatuses, DummyBooks[selectedBookIndex].Status);
            if (selectedStatusIndex < 0)
            {
                selectedStatusIndex = 1;
            }

            Refresh();
        }

        private void ChangePage(int delta)
        {
            var totalPages = Mathf.CeilToInt((float)DummyBooks.Length / BooksPerPage);
            currentPageIndex = Mathf.Clamp(currentPageIndex + delta, 0, Mathf.Max(0, totalPages - 1));
            selectedBookIndex = Mathf.Clamp(currentPageIndex * BooksPerPage, 0, DummyBooks.Length - 1);
            selectedStatusIndex = System.Array.IndexOf(ReadingStatuses, DummyBooks[selectedBookIndex].Status);
            if (selectedStatusIndex < 0)
            {
                selectedStatusIndex = 1;
            }

            Refresh();
        }

        private void SelectPage(int pageIndex)
        {
            currentPageIndex = Mathf.Clamp(pageIndex, 0, Mathf.Max(0, Mathf.CeilToInt((float)DummyBooks.Length / BooksPerPage) - 1));
            selectedBookIndex = Mathf.Clamp(currentPageIndex * BooksPerPage, 0, DummyBooks.Length - 1);
            selectedStatusIndex = System.Array.IndexOf(ReadingStatuses, DummyBooks[selectedBookIndex].Status);
            if (selectedStatusIndex < 0)
            {
                selectedStatusIndex = 1;
            }

            Refresh();
        }

        private void OnUserSearchChanged(string value)
        {
            userSearchQuery = value ?? string.Empty;
            if (selectedTab == "Users")
            {
                RefreshUsersTab();
            }
        }

        private void ApplyUserSearch()
        {
            userSearchQuery = userSearchField != null ? userSearchField.text : string.Empty;
            RefreshUsersTab();
        }

        private void RefreshUsersTab()
        {
            var filteredCount = 0;
            var normalizedQuery = string.IsNullOrWhiteSpace(userSearchQuery) ? string.Empty : userSearchQuery.Trim().ToLowerInvariant();

            for (var i = 0; i < DummyUsers.Length; i++)
            {
                if (string.IsNullOrEmpty(normalizedQuery) || DummyUsers[i].Nickname.ToLowerInvariant().Contains(normalizedQuery))
                {
                    visibleUserIndices[filteredCount] = i;
                    filteredCount++;
                }
            }

            if (filteredCount == 0)
            {
                selectedUserIndex = 0;
            }
            else if (!ContainsVisibleUser(filteredCount, selectedUserIndex))
            {
                selectedUserIndex = visibleUserIndices[0];
            }

            for (var i = 0; i < userItemCards.Length; i++)
            {
                if (userItemCards[i] == null)
                {
                    continue;
                }

                var isVisible = i < filteredCount;
                userItemCards[i].SetActive(isVisible);
                if (!isVisible)
                {
                    continue;
                }

                var userIndex = visibleUserIndices[i];
                var user = DummyUsers[userIndex];
                userProfileImages[i].color = user.ProfileColor;
                userNameTexts[i].text = user.Nickname;
                userIntroTexts[i].text = user.Intro;

                var isSelected = userIndex == selectedUserIndex;
                userItemCards[i].GetComponent<Image>().color = isSelected
                    ? new Color(0.90f, 0.95f, 0.99f, 1f)
                    : new Color(0.97f, 0.98f, 0.99f, 1f);

                var followButtonImage = userFollowButtons[i].GetComponent<Image>();
                var followLabel = userFollowButtons[i].GetComponentInChildren<Text>();
                followLabel.text = user.IsFollowing ? "팔로잉" : "팔로우";
                followButtonImage.color = user.IsFollowing
                    ? new Color(0.80f, 0.86f, 0.96f, 1f)
                    : new Color(0.22f, 0.45f, 0.84f, 0.96f);
                followLabel.color = user.IsFollowing
                    ? new Color(0.16f, 0.27f, 0.47f, 1f)
                    : Color.white;
            }

            if (filteredCount == 0)
            {
                if (userDetailProfileImage != null)
                {
                    userDetailProfileImage.color = new Color(0.85f, 0.87f, 0.90f, 1f);
                }

                if (userDetailNicknameText != null)
                {
                    userDetailNicknameText.text = "검색 결과가 없습니다";
                }

                if (userDetailIntroText != null)
                {
                    userDetailIntroText.text = "다른 닉네임으로 다시 검색해보세요.";
                }

                if (userDetailRoomText != null)
                {
                    userDetailRoomText.text = string.Empty;
                }
            }
            else
            {
                ApplySelectedUserDetail(DummyUsers[selectedUserIndex]);
            }

            if (followingListText != null)
            {
                var followingNames = DummyUsers.Where(user => user.IsFollowing).Select(user => $"• {user.Nickname}");
                followingListText.text = followingNames.Any()
                    ? string.Join("\n", followingNames)
                    : "아직 팔로우한 사용자가 없습니다.";
            }
        }

        private bool ContainsVisibleUser(int filteredCount, int userIndex)
        {
            for (var i = 0; i < filteredCount; i++)
            {
                if (visibleUserIndices[i] == userIndex)
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplySelectedUserDetail(DummyUser user)
        {
            if (userDetailProfileImage != null)
            {
                userDetailProfileImage.color = user.ProfileColor;
            }

            if (userDetailNicknameText != null)
            {
                userDetailNicknameText.text = user.Nickname;
            }

            if (userDetailIntroText != null)
            {
                userDetailIntroText.text = user.Intro;
            }

            if (userDetailRoomText != null)
            {
                userDetailRoomText.text = $"공간 이름  {user.RoomName}";
            }
        }

        private void SelectVisibleUser(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= visibleUserIndices.Length || !userItemCards[slotIndex].activeSelf)
            {
                return;
            }

            selectedUserIndex = visibleUserIndices[slotIndex];
            RefreshUsersTab();
        }

        private void ToggleFollowVisibleUser(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= visibleUserIndices.Length || !userItemCards[slotIndex].activeSelf)
            {
                return;
            }

            var user = DummyUsers[visibleUserIndices[slotIndex]];
            user.IsFollowing = !user.IsFollowing;
            selectedUserIndex = visibleUserIndices[slotIndex];
            RefreshUsersTab();
        }

        private void VisitVisibleUser(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= visibleUserIndices.Length || !userItemCards[slotIndex].activeSelf)
            {
                return;
            }

            selectedUserIndex = visibleUserIndices[slotIndex];
            var user = DummyUsers[selectedUserIndex];
            ApplySelectedUserDetail(user);
            if (userVisitMessageText != null)
            {
                userVisitMessageText.text = $"{user.Nickname}님의 공간으로 이동";
            }

            RefreshUsersTab();
        }

        private void SaveSettingsProfile()
        {
            settingsProfileNickname = string.IsNullOrWhiteSpace(settingsNicknameField != null ? settingsNicknameField.text : string.Empty)
                ? "reader_profile"
                : settingsNicknameField.text.Trim();
            settingsProfileImagePath = string.IsNullOrWhiteSpace(settingsImagePathField != null ? settingsImagePathField.text : string.Empty)
                ? "default/profile_blue"
                : settingsImagePathField.text.Trim();
            settingsProfileColor = ResolveProfileColor(settingsProfileImagePath);

            if (settingsStatusMessageText != null)
            {
                settingsStatusMessageText.text = "프로필 정보가 저장되었습니다.";
            }

            RefreshSettingsTab();
        }

        private void OnSettingsImagePathChanged(string value)
        {
            ApplySettingsImagePreview();
        }

        private void ApplySettingsImagePreview()
        {
            var previewPath = string.IsNullOrWhiteSpace(settingsImagePathField != null ? settingsImagePathField.text : string.Empty)
                ? settingsProfileImagePath
                : settingsImagePathField.text.Trim();
            var previewColor = ResolveProfileColor(previewPath);

            if (settingsProfilePreviewImage != null)
            {
                settingsProfilePreviewImage.color = previewColor;
            }
        }

        private void ResetSettingsProfileInputs()
        {
            if (settingsNicknameField != null)
            {
                settingsNicknameField.text = settingsProfileNickname;
            }

            if (settingsImagePathField != null)
            {
                settingsImagePathField.text = settingsProfileImagePath;
            }

            if (settingsProfilePreviewImage != null)
            {
                settingsProfilePreviewImage.color = settingsProfileColor;
            }
        }

        private void ToggleSettingsNotifications()
        {
            settingsNotificationsEnabled = !settingsNotificationsEnabled;
            if (settingsStatusMessageText != null)
            {
                settingsStatusMessageText.text = settingsNotificationsEnabled
                    ? "알림을 받도록 설정했습니다."
                    : "알림을 끄도록 설정했습니다.";
            }

            RefreshSettingsTab();
        }

        private void LogoutSettingsAccount()
        {
            if (settingsStatusMessageText != null)
            {
                settingsStatusMessageText.text = "로그아웃되었습니다.";
            }

            ReturnToScreenSaver();
        }

        private void ShowSettingsDeletePopup()
        {
            if (settingsDeletePopup != null)
            {
                settingsDeletePopup.SetActive(true);
            }
        }

        private void HideSettingsDeletePopup()
        {
            if (settingsDeletePopup != null)
            {
                settingsDeletePopup.SetActive(false);
            }
        }

        private void ConfirmSettingsDelete()
        {
            if (settingsStatusMessageText != null)
            {
                settingsStatusMessageText.text = "계정 삭제 확인 흐름만 검증되었습니다.";
            }

            HideSettingsDeletePopup();
        }

        private void RefreshSettingsTab()
        {
            if (settingsProfilePreviewImage != null)
            {
                settingsProfilePreviewImage.color = settingsProfileColor;
            }

            if (settingsProfileNameText != null)
            {
                settingsProfileNameText.text = settingsProfileNickname;
            }

            if (settingsNicknameField != null && settingsNicknameField.text != settingsProfileNickname)
            {
                settingsNicknameField.text = settingsProfileNickname;
            }

            if (settingsImagePathField != null && settingsImagePathField.text != settingsProfileImagePath)
            {
                settingsImagePathField.text = settingsProfileImagePath;
            }

            if (settingsNotificationValueText != null)
            {
                settingsNotificationValueText.text = settingsNotificationsEnabled ? "ON" : "OFF";
                settingsNotificationValueText.color = settingsNotificationsEnabled
                    ? new Color(0.20f, 0.44f, 0.80f, 1f)
                    : new Color(0.55f, 0.57f, 0.61f, 1f);
            }

            if (settingsDeletePopupMessageText != null)
            {
                settingsDeletePopupMessageText.text = "정말 계정을 삭제하시겠습니까?";
            }
        }

        private static Color ResolveProfileColor(string imagePath)
        {
            var normalized = string.IsNullOrWhiteSpace(imagePath) ? string.Empty : imagePath.Trim().ToLowerInvariant();
            if (normalized.Contains("green"))
            {
                return new Color(0.50f, 0.80f, 0.73f, 1f);
            }

            if (normalized.Contains("pink"))
            {
                return new Color(0.84f, 0.67f, 0.79f, 1f);
            }

            if (normalized.Contains("gold"))
            {
                return new Color(0.93f, 0.74f, 0.46f, 1f);
            }

            return new Color(0.54f, 0.72f, 0.88f, 1f);
        }

        private static Canvas CreateCanvas(Transform parent, string name, Camera eventCamera)
        {
            var canvasObject = new GameObject(name);
            canvasObject.transform.SetParent(parent, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = eventCamera;
            canvas.overrideSorting = true;
            canvas.sortingOrder = name == "DashboardCanvas" ? 2 : 1;
            canvasObject.AddComponent<GraphicRaycaster>();

            var rect = canvas.GetComponent<RectTransform>();
            rect.sizeDelta = name == "DashboardCanvas" ? DashboardCanvasSize : DefaultCanvasSize;
            return canvas;
        }

        private Canvas GetCanvas(NotebookUiState state)
        {
            return state == NotebookUiState.ScreenSaver ? screenSaverCanvas : dashboardCanvas;
        }

        private CanvasGroup GetCanvasGroup(NotebookUiState state)
        {
            return state == NotebookUiState.ScreenSaver ? screenSaverCanvasGroup : dashboardCanvasGroup;
        }

        private void ApplySelectedAd()
        {
            if (screenSaverImage == null)
            {
                return;
            }

            EnsureAdImages();
            if (adImages == null || adImages.Length == 0)
            {
                screenSaverImage.sprite = null;
                screenSaverImage.color = new Color(0.92f, 0.86f, 0.72f, 1f);
                WarnIfAdsMissing();
                return;
            }

            var selectedIndex = useRandomAd && adImages.Length > 1
                ? Random.Range(0, adImages.Length)
                : 0;

            screenSaverImage.sprite = adImages[selectedIndex];
            screenSaverImage.color = Color.white;
            screenSaverImage.SetNativeSize();
            StretchToFill(screenSaverImage.rectTransform);
        }

        private void EnsureAdImages()
        {
            if (adImages != null && adImages.Length > 0)
            {
                return;
            }

#if UNITY_EDITOR
            var guids = AssetDatabase.FindAssets("t:Sprite", DefaultAdFolders)
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .OrderBy(path => path)
                .ToArray();
            if (guids == null || guids.Length == 0)
            {
                return;
            }

            adImages = new Sprite[guids.Length];
            for (var i = 0; i < guids.Length; i++)
            {
                adImages[i] = AssetDatabase.LoadAssetAtPath<Sprite>(guids[i]);
            }
#endif
        }

        private void WarnIfAdsMissing()
        {
            if (adImages == null || adImages.Length == 0)
            {
                Debug.LogWarning(MissingAdsWarning, this);
            }
        }

        private static void StretchToFill(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }

        private static void StretchWithOffsets(RectTransform rectTransform, float left, float right, float top, float bottom)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = new Vector2(left, bottom);
            rectTransform.offsetMax = new Vector2(-right, -top);
            rectTransform.localScale = Vector3.one;
        }

        private static void StretchToTop(RectTransform rectTransform, float height)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.offsetMin = new Vector2(0f, -height);
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
        {
            return CreatePanel(parent, name, anchoredPosition, size, anchor, new Color(0.08f, 0.11f, 0.14f, 0.94f));
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchor, Color color)
        {
            var panelObject = new GameObject(name);
            panelObject.transform.SetParent(parent, false);

            var image = panelObject.AddComponent<Image>();
            image.color = color;

            var rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return panelObject;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, UnityAction onClick, Vector2 size, Vector2 anchor)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.25f, 0.29f, 0.96f);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            CreateLabel(buttonObject.transform, "Label", label, Vector2.zero, size, 16, TextAnchor.MiddleCenter);
            return button;
        }

        private static Button CreateButton(Transform parent, string name, string label, UnityAction onClick, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
        {
            return CreateButton(parent, name, label, anchoredPosition, onClick, size, anchor);
        }

        private static InputField CreateSearchField(Transform parent, Vector2 anchoredPosition, Vector2 size, string placeholder)
        {
            var inputObject = new GameObject("SearchField");
            inputObject.transform.SetParent(parent, false);

            var image = inputObject.AddComponent<Image>();
            image.color = new Color(0.95f, 0.96f, 0.98f, 1f);

            var inputField = inputObject.AddComponent<InputField>();
            var rect = inputObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var placeholderText = CreateLabel(inputObject.transform, "Placeholder", placeholder, new Vector2(12f, -8f), new Vector2(240f, 18f), 13, TextAnchor.UpperLeft, new Color(0.57f, 0.60f, 0.64f, 1f), new Vector2(0f, 1f));
            var text = CreateLabel(inputObject.transform, "Text", string.Empty, new Vector2(12f, -8f), new Vector2(240f, 18f), 13, TextAnchor.UpperLeft, new Color(0.18f, 0.20f, 0.22f, 1f), new Vector2(0f, 1f));
            inputField.textComponent = text;
            inputField.placeholder = placeholderText;
            return inputField;
        }

        private static GameObject CreateScrollViewport(Transform parent, string name)
        {
            var viewport = new GameObject(name);
            viewport.transform.SetParent(parent, false);
            var image = viewport.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.002f);
            viewport.AddComponent<RectMask2D>();

            var rect = viewport.GetComponent<RectTransform>();
            StretchToFill(rect);
            return viewport;
        }

        private static RectTransform CreateScrollContent(Transform viewport, string name, float height)
        {
            var content = new GameObject(name);
            content.transform.SetParent(viewport, false);
            var rect = content.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.offsetMin = new Vector2(0f, -Mathf.Max(height, 1f));
            rect.offsetMax = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, Mathf.Max(height, 1f));
            rect.localScale = Vector3.one;

            var scrollRect = viewport.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = rect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 18f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            return rect;
        }

        private static void SetupDetailScrollContent(Transform detailPanel)
        {
            if (detailPanel == null)
            {
                return;
            }

            var existingViewport = detailPanel.Find("DetailViewport");
            if (existingViewport != null)
            {
                return;
            }

            var viewport = CreateScrollViewport(detailPanel, "DetailViewport");
            var viewportRect = viewport.GetComponent<RectTransform>();
            StretchToFill(viewportRect);
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportRect.localScale = Vector3.one;

            var detailPanelRect = detailPanel as RectTransform;
            var minimumHeight = detailPanelRect != null ? Mathf.Max(detailPanelRect.rect.height, 620f) : 620f;
            var content = CreateScrollContent(viewport.transform, "DetailContent", minimumHeight);

            var children = new System.Collections.Generic.List<Transform>();
            for (var i = 0; i < detailPanel.childCount; i++)
            {
                var child = detailPanel.GetChild(i);
                if (child != viewport.transform)
                {
                    children.Add(child);
                }
            }

            for (var i = 0; i < children.Count; i++)
            {
                children[i].SetParent(content, false);

                if (!children[i].gameObject.activeSelf)
                {
                    children[i].gameObject.SetActive(true);
                }

                children[i].localScale = Vector3.one;

                var canvasGroup = children[i].GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                }
            }

            var contentHeight = minimumHeight;
            for (var i = 0; i < children.Count; i++)
            {
                var childRect = children[i] as RectTransform;
                if (childRect == null)
                {
                    continue;
                }

                childRect.localScale = Vector3.one;

                if (childRect.anchorMin != childRect.anchorMax)
                {
                    childRect.anchorMin = new Vector2(0f, 1f);
                    childRect.anchorMax = new Vector2(0f, 1f);
                }

                childRect.pivot = childRect.anchorMin;
                childRect.offsetMin = new Vector2(childRect.offsetMin.x, childRect.offsetMin.y);
                childRect.offsetMax = new Vector2(childRect.offsetMax.x, childRect.offsetMax.y);

                var childBottom = Mathf.Abs(childRect.anchoredPosition.y) + childRect.rect.height;
                contentHeight = Mathf.Max(contentHeight, childBottom + 24f);
            }

            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
            content.offsetMin = new Vector2(0f, -contentHeight);
            content.offsetMax = Vector2.zero;

            Canvas.ForceUpdateCanvases();
        }

        private static Text CreateLabel(Transform parent, string name, string textValue, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment)
        {
            return CreateLabel(parent, name, textValue, anchoredPosition, size, fontSize, alignment, Color.white, alignment == TextAnchor.UpperLeft ? new Vector2(0f, 1f) : Vector2.zero, FontStyle.Normal);
        }

        private static Text CreateLabel(Transform parent, string name, string textValue, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment, Color color)
        {
            return CreateLabel(parent, name, textValue, anchoredPosition, size, fontSize, alignment, color, alignment == TextAnchor.UpperLeft ? new Vector2(0f, 1f) : Vector2.zero, FontStyle.Normal);
        }

        private static Text CreateLabel(Transform parent, string name, string textValue, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment, Color color, Vector2 anchor)
        {
            return CreateLabel(parent, name, textValue, anchoredPosition, size, fontSize, alignment, color, anchor, FontStyle.Normal);
        }

        private static Text CreateLabel(Transform parent, string name, string textValue, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment, Color color, Vector2 anchor, FontStyle fontStyle)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = color;
            text.alignment = alignment;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.text = textValue;

            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return text;
        }

        private static void UpdateTabButton(Button button, bool isSelected)
        {
            if (button == null)
            {
                return;
            }

            button.GetComponent<Image>().color = isSelected
                ? new Color(0.12f, 0.39f, 0.84f, 0.96f)
                : new Color(0.31f, 0.34f, 0.38f, 0.96f);
        }
    }
}
