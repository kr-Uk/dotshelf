using System;
using System.Collections;
using System.Linq;
using App.Application.Notebook;
using App.Features.Notebook.Presentation;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using PanelSettings = UnityEngine.UIElements.PanelSettings;
using UIDocument = UnityEngine.UIElements.UIDocument;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BookshelfPorting.Runtime
{
    public class NotebookScreenController : MonoBehaviour
    {
        private static readonly string[] DefaultAdFolders =
        {
            "Assets/Resources/UI/Ads",
            "Assets/Arts/UI/Ads"
        };
        private const string AdResourcesPath = "UI/Ads";
        private const string MissingAdsWarning = "NotebookScreenController: No ad sprites assigned. Assign Sprite assets under Assets/Resources/UI/Ads or to adImages.";
        private const string NotebookPanelSettingsResource = "NotebookPanelSettings";

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

        private const float CanvasScale = 0.000542f;
        private const float ScreenSaverDepthOffset = -0.0012f;
        private const float DashboardDepthOffset = -0.0008f;
        private const float TransitionDuration = 0.22f;
        private const float HiddenScaleMultiplier = 0.96f;
        private static readonly Vector2 ScreenSaverCanvasSize = new(800f, 450f);
        private static readonly Vector2 DashboardCanvasSize = new(850f, 480f);
        private static readonly string[] AdvertisedBookTitles = Array.Empty<string>();
        private static Font cachedUiFont;
        private static Sprite roundedPanelSprite;
        private static Sprite roundedPillSprite;
        private static Sprite fallbackAdSprite;
        private static readonly string[] BookCategories = { "전체", "읽는 중", "읽음", "안 읽음", "책장 1", "책장 2" };
        private static readonly string[] ReadingStatuses = { "읽음", "읽는 중", "안 읽음" };
        private static readonly DummyBook[] DummyBooks = Array.Empty<DummyBook>();

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
        // 편집 모드 등에서 SetSceneInteractionSuppressed(true) 호출 시 true. theme swap 후 reassert
        // hook 이 이 flag 를 읽어 canvas 가 다시 active 로 돌아가지 않게 보장.
        private bool sceneInteractionSuppressed;
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
        private readonly int[] visibleBookIndices = new int[DummyBooks.Length];
        private InputField searchField;
        private Text bookSearchStateText;
        private Text bookResultCountText;
        private Button addSelectedBookButton;
        private Text addSelectedBookLabel;
        private string bookSearchQuery = string.Empty;
        private bool selectedBookQueuedForAdd;
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
        private const int BooksPerPage = 3;
        private Text contentText;
        private bool useImmersiveMode;
        private readonly Button[] tabButtons = new Button[3];
        private string selectedTab = "Books";
        private NotebookUiState currentState = NotebookUiState.ScreenSaver;
        private bool openDashboardOnShow;
        private bool openAdvertisedBookOnDashboard;
        private int selectedAdIndex;
        private NotebookToolkitController toolkitController;
        private NotebookToolkitController subscribedToolkitController;
        private SearchNotebookBooksUseCase searchNotebookBooksUseCase;
        private LoadNotebookMarketBooksUseCase loadNotebookMarketBooksUseCase;
        private AddNotebookBookToShelfUseCase addNotebookBookToShelfUseCase;
        private NotebookRuntimeUseCase notebookRuntimeUseCase;

        public Transform ScreenTransform { get; private set; }
        public event Action BookAddedToShelfThroughApi;
        public event Action<NotebookSpaceVisitRequest> SpaceVisitRequested;

        // notebook 이 immersive overlay 모드인지 — 이때 ScreenSaverCanvas / DashboardCanvas / 노트북 UIDocument 가
        // ScreenSpaceOverlay 로 화면 전체를 덮는다. main scene 의 mouse click 은 모두 이 UI 를 위한 것으로
        // 간주해야 하며, BookshelfExperienceManager 의 raycast-based focus 전환 (FocusGuestbook 등) 이 같은
        // frame 에 spurious 하게 fire 되지 않도록 click handler 가 이 속성을 확인해 skip 한다.
        public bool IsImmersiveModeActive => useImmersiveMode;

        // 룸 편집 모드 등 외부 컨텍스트에서 노트북 화면 클릭으로 인한 focus 전환을 차단해야 할 때 호출.
        // suppress=true 면 ScreenSaverCanvas / DashboardCanvas GameObject 를 SetActive(false) 해 visual +
        // GraphicRaycaster (AdImage Button.onClick 으로 인한 FocusNotebook spurious fire) 동시 차단.
        // suppress=false 면 currentState 에 맞게 정확한 canvas 만 재활성. NotebookToolkitController UIDocument
        // (immersive Dashboard) 는 이 호출 범위 밖 — 편집 모드는 QuarterView 에서만 진입하므로 immersive
        // 상태 진입 자체가 차단됨.
        public void SetSceneInteractionSuppressed(bool suppressed)
        {
            sceneInteractionSuppressed = suppressed;
            if (suppressed)
            {
                if (screenSaverCanvas != null) screenSaverCanvas.gameObject.SetActive(false);
                if (dashboardCanvas != null) dashboardCanvas.gameObject.SetActive(false);
            }
            else
            {
                if (screenSaverCanvas != null)
                {
                    screenSaverCanvas.gameObject.SetActive(currentState == NotebookUiState.ScreenSaver);
                }
                if (dashboardCanvas != null)
                {
                    dashboardCanvas.gameObject.SetActive(currentState == NotebookUiState.Dashboard);
                }
            }
            Debug.Log($"[NotebookFocusBlock] SetSceneInteractionSuppressed suppressed={suppressed} " +
                      $"screenSaverActive={(screenSaverCanvas != null ? screenSaverCanvas.gameObject.activeSelf : false)} " +
                      $"dashboardActive={(dashboardCanvas != null ? dashboardCanvas.gameObject.activeSelf : false)} currentState={currentState}");
        }

        // 편집 모드 진입 후 theme swap 등으로 canvas state 가 다시 흔들렸을 때 외부 (예: DeskVisualSkinApplier
        // 의 theme 적용 직후) 에서 호출해 suppress 상태 재확인용. 현재 suppression 이 active 면 두 canvas
        // 를 다시 SetActive(false). 비활성이면 no-op (정상 상태 유지).
        public void ReassertSceneInteractionSuppressionIfActive(string reason)
        {
            if (!sceneInteractionSuppressed) return;
            if (screenSaverCanvas != null && screenSaverCanvas.gameObject.activeSelf)
            {
                screenSaverCanvas.gameObject.SetActive(false);
                Debug.Log($"[NotebookFocusBlock] reasserted ScreenSaverCanvas SetActive(false) reason={reason}");
            }
            if (dashboardCanvas != null && dashboardCanvas.gameObject.activeSelf)
            {
                dashboardCanvas.gameObject.SetActive(false);
                Debug.Log($"[NotebookFocusBlock] reasserted DashboardCanvas SetActive(false) reason={reason}");
            }
        }

        public bool IsSceneInteractionSuppressed => sceneInteractionSuppressed;

        private void Awake()
        {
            EnsureAdImages();
            NormalizeSerializedScreenSaverCanvas();
            WarnIfAdsMissing();
        }

        public void Configure(
            Transform screenTransform,
            Camera eventCamera,
            UnityAction onCloseDashboard,
            UnityAction onCloseScreenSaver = null,
            UnityAction onOpenScreenSaver = null,
            SearchNotebookBooksUseCase searchUseCase = null,
            LoadNotebookMarketBooksUseCase loadMarketUseCase = null,
            AddNotebookBookToShelfUseCase addUseCase = null,
            NotebookRuntimeUseCase runtimeUseCase = null)
        {
            ConfigureNotebookBookUseCases(searchUseCase, loadMarketUseCase, addUseCase, runtimeUseCase);
            ScreenTransform = screenTransform;
            EnsureCanvases(screenTransform, eventCamera, onCloseDashboard, onCloseScreenSaver, onOpenScreenSaver);
            ApplyPresentationMode();
            ApplySelectedAd();
            ShowStateInstant(currentState);
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
            toolkitController?.ConfigureNotebookBookUseCases(
                searchNotebookBooksUseCase,
                loadNotebookMarketBooksUseCase,
                addNotebookBookToShelfUseCase,
                notebookRuntimeUseCase);
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

            if (toolkitController != null)
            {
                toolkitController.HideDashboard();
            }
        }

        public void ActivateDashboard(bool animate = true)
        {
            openDashboardOnShow = false;
            SetState(NotebookUiState.Dashboard, animate);
        }

        public void OpenOtherSpacesTab()
        {
            toolkitController?.OpenOtherSpacesTab();
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

        private void EnsureCanvases(
            Transform screenTransform,
            Camera eventCamera,
            UnityAction onCloseDashboard,
            UnityAction onCloseScreenSaver,
            UnityAction onOpenScreenSaver)
        {
            if (screenTransform == null)
            {
                return;
            }

            EnsureToolkitController(screenTransform, onCloseDashboard);
            RemoveExistingCanvas(screenTransform, "DashboardCanvas");
            dashboardCanvas = null;
            dashboardCanvasGroup = null;

            if (screenSaverCanvas != null && dashboardCanvas != null)
            {
                RemoveLegacyDashboardContent();
                return;
            }

            RemoveExistingCanvas(screenTransform, "ScreenSaverCanvas");

            screenSaverCanvas = CreateCanvas(screenTransform, "ScreenSaverCanvas", eventCamera);
            screenSaverCanvasGroup = screenSaverCanvas.gameObject.AddComponent<CanvasGroup>();
            screenSaverCanvasGroup.alpha = 1f;
            BuildScreenSaver(screenSaverCanvas.transform, onCloseScreenSaver, onOpenScreenSaver);

            dashboardCanvas = CreateCanvas(screenTransform, "DashboardCanvas", eventCamera);
            dashboardCanvasGroup = dashboardCanvas.gameObject.AddComponent<CanvasGroup>();
            dashboardCanvasGroup.alpha = currentState == NotebookUiState.Dashboard ? 1f : 0f;
            RemoveLegacyDashboardContent();
            // The legacy dashboard is intentionally not generated anymore.
            // NotebookToolkitController owns the notebook dashboard surface.
        }

        private void RemoveLegacyDashboardContent()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (canvas == null || canvas.name != "DashboardCanvas")
                {
                    continue;
                }

                ClearLegacyCanvas(canvas);
                canvas.gameObject.SetActive(false);
            }

            if (dashboardCanvas == null)
            {
                return;
            }

            var parent = dashboardCanvas.transform.parent;
            if (parent != null)
            {
                for (var i = parent.childCount - 1; i >= 0; i--)
                {
                    var sibling = parent.GetChild(i);
                    if (sibling == dashboardCanvas.transform || sibling.name != "DashboardCanvas")
                    {
                        continue;
                    }

                    DestroyLegacyObject(sibling.gameObject);
                }
            }

            ClearLegacyCanvas(dashboardCanvas);

            dashboardRootBackground = null;
            dashboardAppFrameRect = null;
            dashboardContentWindowRect = null;
            booksContentRoot = null;
            usersContentRoot = null;
            settingsContentRoot = null;
            contentText = null;
        }

        private void ClearLegacyCanvas(Canvas targetCanvas)
        {
            if (targetCanvas == null)
            {
                return;
            }

            var canvasTransform = targetCanvas.transform;
            for (var i = canvasTransform.childCount - 1; i >= 0; i--)
            {
                var child = canvasTransform.GetChild(i);
                DestroyLegacyObject(child.gameObject);
            }

            var rootImage = targetCanvas.GetComponent<Image>();
            if (rootImage != null)
            {
                rootImage.enabled = false;
                if (Application.isPlaying)
                {
                    Destroy(rootImage);
                }
                else
                {
                    DestroyImmediate(rootImage);
                }
            }
        }

        private void DestroyLegacyObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            target.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void EnsureToolkitController(Transform screenTransform, UnityAction onCloseDashboard)
        {
            if (screenTransform == null)
            {
                return;
            }

            var document = screenTransform.GetComponent<UIDocument>();
            if (document == null)
            {
                document = screenTransform.gameObject.AddComponent<UIDocument>();
            }

            if (document.panelSettings == null)
            {
                var panelSettings = Resources.Load<PanelSettings>(NotebookPanelSettingsResource);
                if (panelSettings != null)
                {
                    document.panelSettings = panelSettings;
                }
            }

            document.sortingOrder = 320;
            if (document.panelSettings != null)
            {
                document.panelSettings.sortingOrder = 320;
            }

            if (toolkitController == null)
            {
                toolkitController = screenTransform.GetComponent<NotebookToolkitController>();
            }

            if (toolkitController == null)
            {
                toolkitController = screenTransform.GetComponentInChildren<NotebookToolkitController>(true);
            }

            if (toolkitController == null && document != null)
            {
                toolkitController = screenTransform.gameObject.AddComponent<NotebookToolkitController>();
            }

            toolkitController?.Configure(
                onCloseDashboard,
                searchNotebookBooksUseCase,
                loadNotebookMarketBooksUseCase,
                addNotebookBookToShelfUseCase,
                notebookRuntimeUseCase);
            AttachToolkitCallbacks();
        }

        private void AttachToolkitCallbacks()
        {
            if (subscribedToolkitController != null && subscribedToolkitController == toolkitController)
            {
                return;
            }

            if (subscribedToolkitController != null)
            {
                subscribedToolkitController.BookAddedToShelfThroughApi -= HandleToolkitBookAddedToShelfThroughApi;
                subscribedToolkitController.SpaceVisitRequested -= HandleToolkitSpaceVisitRequested;
            }

            subscribedToolkitController = toolkitController;
            if (subscribedToolkitController != null)
            {
                subscribedToolkitController.BookAddedToShelfThroughApi += HandleToolkitBookAddedToShelfThroughApi;
                subscribedToolkitController.SpaceVisitRequested += HandleToolkitSpaceVisitRequested;
            }
        }

        private void HandleToolkitBookAddedToShelfThroughApi()
        {
            BookAddedToShelfThroughApi?.Invoke();
        }

        private void HandleToolkitSpaceVisitRequested(NotebookSpaceVisitRequest request)
        {
            SpaceVisitRequested?.Invoke(request);
        }

        private void OnDestroy()
        {
            if (subscribedToolkitController != null)
            {
                subscribedToolkitController.BookAddedToShelfThroughApi -= HandleToolkitBookAddedToShelfThroughApi;
                subscribedToolkitController.SpaceVisitRequested -= HandleToolkitSpaceVisitRequested;
                subscribedToolkitController = null;
            }
        }

        private bool HasToolkitDashboard()
        {
            return toolkitController != null;
        }

        private static void RemoveExistingCanvas(Transform parent, string canvasName)
        {
            if (parent == null)
            {
                return;
            }

            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child == null || child.name != canvasName)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void BuildScreenSaver(Transform parent, UnityAction onCloseScreenSaver, UnityAction onOpenScreenSaver)
        {
            screenSaverBackground = parent.gameObject.AddComponent<Image>();
            screenSaverBackground.color = new Color(0.07f, 0.09f, 0.12f, 0.98f);
            screenSaverBackground.raycastTarget = false;

            var adImageObject = new GameObject("AdImage");
            adImageObject.transform.SetParent(parent, false);
            screenSaverImage = adImageObject.AddComponent<Image>();
            screenSaverImageRect = screenSaverImage.rectTransform;
            screenSaverImage.preserveAspect = false;
            screenSaverImage.color = Color.white;
            screenSaverImage.raycastTarget = true;
            StretchToFill(screenSaverImageRect);

            var openButton = adImageObject.AddComponent<Button>();
            openButton.transition = Selectable.Transition.ColorTint;
            openButton.targetGraphic = screenSaverImage;
            var colors = openButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.97f, 1f, 1f);
            colors.pressedColor = new Color(0.84f, 0.92f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
            openButton.colors = colors;
            openButton.onClick.AddListener(() =>
            {
                if (onOpenScreenSaver != null)
                {
                    onOpenScreenSaver.Invoke();
                    return;
                }

                ActivateDashboard();
            });

            if (onCloseScreenSaver == null)
            {
                return;
            }

            var closeButton = CreateButton(
                parent,
                "ScreenSaverCloseButton",
                "X",
                new Vector2(-14f, -14f),
                onCloseScreenSaver,
                new Vector2(34f, 34f),
                new Vector2(1f, 1f));
            closeButton.transform.SetAsLastSibling();
            closeButton.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.10f, 0.72f);

            var closeLabel = closeButton.GetComponentInChildren<Text>();
            if (closeLabel != null)
            {
                closeLabel.fontSize = 17;
                closeLabel.fontStyle = RuntimeUiFont.ResolveStyle(closeLabel.font, FontStyle.Bold);
                closeLabel.color = Color.white;
            }
        }

        private void NormalizeSerializedScreenSaverCanvas()
        {
            if (screenSaverImage == null)
            {
                return;
            }

            var canvas = screenSaverImage.GetComponentInParent<Canvas>(true);
            if (canvas == null || canvas.name != "ScreenSaverCanvas")
            {
                return;
            }

            var rect = canvas.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = ScreenSaverCanvasSize;
                ApplyCanvasTransform(canvas.transform);
            }

            screenSaverCanvas = canvas;
            screenSaverCanvasGroup = canvas.GetComponent<CanvasGroup>();
            screenSaverImageRect = screenSaverImage.rectTransform;
            screenSaverImage.preserveAspect = false;
            screenSaverImage.raycastTarget = true;
            StretchToFill(screenSaverImageRect);
            ApplySelectedAd();
        }

        private void BuildDashboard(Transform parent, UnityAction onCloseDashboard)
        {
            RemoveLegacyDashboardContent();
        }

        private void ApplyCanvasTransform(Transform canvasTransform)
        {
            if (canvasTransform == null)
            {
                return;
            }

            var depthOffset = canvasTransform.name == "DashboardCanvas" ? DashboardDepthOffset : ScreenSaverDepthOffset;
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
            rect.sizeDelta = isDashboard ? DashboardCanvasSize : ScreenSaverCanvasSize;
            ApplyCanvasTransform(canvas.transform);
            ApplyWorldLayout(isDashboard);
        }

        private void ApplyImmersiveLayout(bool isDashboard)
        {
            if (isDashboard)
            {
                if (dashboardRootBackground != null)
                {
                    dashboardRootBackground.color = new Color(0.91f, 0.93f, 0.90f, 1f);
                }

                if (dashboardAppFrameRect != null)
                {
                    var availableWidth = Mathf.Max(320f, Screen.width - 20f);
                    var availableHeight = Mathf.Max(240f, Screen.height - 20f);
                    var scale = Mathf.Min(availableWidth / DashboardCanvasSize.x, availableHeight / DashboardCanvasSize.y);

                    dashboardAppFrameRect.anchorMin = new Vector2(0.5f, 0.5f);
                    dashboardAppFrameRect.anchorMax = new Vector2(0.5f, 0.5f);
                    dashboardAppFrameRect.pivot = new Vector2(0.5f, 0.5f);
                    dashboardAppFrameRect.anchoredPosition = Vector2.zero;
                    dashboardAppFrameRect.sizeDelta = DashboardCanvasSize;
                    dashboardAppFrameRect.localScale = Vector3.one * scale;
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
                StretchToFill(screenSaverImageRect);
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

            if ((nextState == NotebookUiState.Dashboard && HasToolkitDashboard()) ||
                (currentState == NotebookUiState.Dashboard && HasToolkitDashboard()))
            {
                currentState = nextState;
                ShowStateInstant(nextState);
                return;
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

            var toolkitShown = false;
            if (state == NotebookUiState.Dashboard)
            {
                RemoveLegacyDashboardContent();

                if (toolkitController != null)
                {
                    toolkitShown = toolkitController.ShowDashboard(useImmersiveMode);
                    if (!toolkitShown)
                    {
                        Refresh();
                    }
                }
                else
                {
                    Refresh();
                }
            }
            else if (toolkitController != null)
            {
                toolkitController.HideDashboard();
            }

            if (activeCanvas != null)
            {
                activeCanvas.gameObject.SetActive(state != NotebookUiState.Dashboard && !toolkitShown);
                activeCanvas.transform.localScale = useImmersiveMode ? Vector3.one : Vector3.one * CanvasScale;
            }

            if (activeGroup != null)
            {
                activeGroup.alpha = state == NotebookUiState.Dashboard || toolkitShown ? 0f : 1f;
            }

            if (state == NotebookUiState.Dashboard)
            {
                OpenPendingAdvertisedBookDetail();
            }
        }

        private void OpenPendingAdvertisedBookDetail()
        {
            if (!openAdvertisedBookOnDashboard)
            {
                return;
            }

            if (toolkitController != null)
            {
                toolkitController.OpenAdvertisedBookDetail(GetSelectedAdvertisedBookTitle());
                openAdvertisedBookOnDashboard = false;
                return;
            }

            openAdvertisedBookOnDashboard = false;
            if (DummyBooks.Length == 0)
            {
                Refresh();
                return;
            }

            selectedTab = "Books";
            SelectBook(0);
            Refresh();
        }

        private string GetSelectedAdvertisedBookTitle()
        {
            return selectedAdIndex >= 0 && selectedAdIndex < AdvertisedBookTitles.Length
                ? AdvertisedBookTitles[selectedAdIndex]
                : string.Empty;
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
            var category = BookCategories[Mathf.Clamp(selectedCategoryIndex, 0, BookCategories.Length - 1)];
            if (selectedCategoryText != null)
            {
                selectedCategoryText.text = $"{category} 도서";
            }

            for (var i = 0; i < categoryButtons.Length; i++)
            {
                if (categoryButtons[i] == null)
                {
                    continue;
                }

                var isSelected = i == selectedCategoryIndex;
                categoryButtons[i].GetComponent<Image>().color = isSelected
                    ? new Color(0.22f, 0.45f, 0.38f, 1f)
                    : new Color(0.95f, 0.96f, 0.98f, 0f);
                var categoryLabel = categoryButtons[i].GetComponentInChildren<Text>();
                categoryLabel.fontStyle = RuntimeUiFont.ResolveStyle(categoryLabel.font, isSelected ? FontStyle.Bold : FontStyle.Normal);
                categoryLabel.color = isSelected
                    ? Color.white
                    : new Color(0.84f, 0.86f, 0.82f, 1f);
            }

            var filteredCount = CollectFilteredBooks();
            var totalPages = Mathf.Max(1, Mathf.CeilToInt((float)filteredCount / BooksPerPage));
            currentPageIndex = Mathf.Clamp(currentPageIndex, 0, totalPages - 1);
            var pageStart = currentPageIndex * BooksPerPage;
            var visibleOnPage = Mathf.Clamp(filteredCount - pageStart, 0, BooksPerPage);

            if (bookResultCountText != null)
            {
                bookResultCountText.text = $"{filteredCount}권";
            }

            if (bookSearchStateText != null)
            {
                bookSearchStateText.text = filteredCount == 0
                    ? "검색 결과가 없습니다."
                    : string.Empty;
            }

            if (filteredCount == 0)
            {
                for (var i = 0; i < bookItemButtons.Length; i++)
                {
                    if (bookItemButtons[i] != null)
                    {
                        bookItemButtons[i].gameObject.SetActive(false);
                    }
                }

                RefreshPagination(0);
                ApplyEmptyBookDetail();
                return;
            }

            if (!ContainsFilteredBook(filteredCount, selectedBookIndex))
            {
                selectedBookIndex = visibleBookIndices[pageStart];
                selectedBookQueuedForAdd = false;
            }

            for (var i = 0; i < bookItemButtons.Length; i++)
            {
                if (bookItemButtons[i] == null)
                {
                    continue;
                }

                var isVisible = i < visibleOnPage;
                bookItemButtons[i].gameObject.SetActive(isVisible);
                if (!isVisible)
                {
                    continue;
                }

                var bookIndex = visibleBookIndices[pageStart + i];
                ApplyBookCard(bookItemButtons[i], DummyBooks[bookIndex], bookIndex == selectedBookIndex);
            }

            RefreshPagination(totalPages);
            ApplyBookDetail(DummyBooks[Mathf.Clamp(selectedBookIndex, 0, DummyBooks.Length - 1)]);
        }

        private int CollectFilteredBooks()
        {
            var count = 0;
            for (var i = 0; i < DummyBooks.Length; i++)
            {
                if (!BookMatchesFilter(DummyBooks[i]))
                {
                    continue;
                }

                visibleBookIndices[count] = i;
                count++;
            }

            return count;
        }

        private bool BookMatchesFilter(DummyBook book)
        {
            if (book == null)
            {
                return false;
            }

            var category = BookCategories[Mathf.Clamp(selectedCategoryIndex, 0, BookCategories.Length - 1)];
            if (category == "읽는 중" && book.Status != "읽는 중")
            {
                return false;
            }

            if (category == "읽음" && book.Status != "읽음")
            {
                return false;
            }

            if (category == "안 읽음" && book.Status != "안 읽음")
            {
                return false;
            }

            if (category == "책장 1" && book.Shelf != "내 책장 1")
            {
                return false;
            }

            if (category == "책장 2" && book.Shelf != "내 책장 2")
            {
                return false;
            }

            var query = string.IsNullOrWhiteSpace(bookSearchQuery)
                ? string.Empty
                : bookSearchQuery.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(query))
            {
                return true;
            }

            return ContainsIgnoreCase(book.Title, query)
                || ContainsIgnoreCase(book.Author, query)
                || ContainsIgnoreCase(book.Publisher, query)
                || ContainsIgnoreCase(book.Summary, query);
        }

        private static bool ContainsIgnoreCase(string value, string normalizedQuery)
        {
            return !string.IsNullOrEmpty(value) && value.ToLowerInvariant().Contains(normalizedQuery);
        }

        private bool ContainsFilteredBook(int filteredCount, int bookIndex)
        {
            for (var i = 0; i < filteredCount; i++)
            {
                if (visibleBookIndices[i] == bookIndex)
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyBookCard(Button button, DummyBook book, bool isSelected)
        {
            if (button == null || book == null)
            {
                return;
            }

            var thumbnail = button.transform.Find("Thumbnail")?.GetComponent<Image>();
            if (thumbnail != null)
            {
                thumbnail.color = book.CoverColor;
            }

            var letterLabel = button.transform.Find("ThumbLetter")?.GetComponent<Text>();
            if (letterLabel != null)
            {
                letterLabel.text = string.IsNullOrEmpty(book.Title) ? "?" : book.Title.Substring(0, 1);
            }

            var titleLabel = button.transform.Find("BookTitle")?.GetComponent<Text>();
            if (titleLabel != null)
            {
                titleLabel.text = book.Title;
            }

            var authorLabel = button.transform.Find("BookAuthor")?.GetComponent<Text>();
            if (authorLabel != null)
            {
                authorLabel.text = $"{book.Author} · {book.Publisher}";
            }

            var metaLabel = button.transform.Find("BookMeta")?.GetComponent<Text>();
            if (metaLabel != null)
            {
                metaLabel.text = $"★ {book.Rating:0.0} · 리뷰 {book.ReviewCount}개 · 조회 {book.Views:N0}";
                metaLabel.fontSize = 11;
            }

            button.GetComponent<Image>().color = isSelected
                ? new Color(0.90f, 0.96f, 0.92f, 1f)
                : new Color(0.98f, 0.99f, 1f, 1f);
        }

        private void RefreshPagination(int totalPages)
        {
            if (previousPageButton != null)
            {
                previousPageButton.interactable = totalPages > 0 && currentPageIndex > 0;
            }

            if (nextPageButton != null)
            {
                nextPageButton.interactable = totalPages > 0 && currentPageIndex < totalPages - 1;
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
                    ? new Color(0.23f, 0.43f, 0.37f, 1f)
                    : new Color(0.90f, 0.91f, 0.87f, 1f);
                pageButtons[i].GetComponentInChildren<Text>().color = isCurrent
                    ? Color.white
                    : new Color(0.23f, 0.25f, 0.23f, 1f);
                var pageLabel = pageButtons[i].GetComponentInChildren<Text>();
                pageLabel.fontStyle = RuntimeUiFont.ResolveStyle(pageLabel.font, isCurrent ? FontStyle.Bold : FontStyle.Normal);
            }
        }

        private void ApplyBookDetail(DummyBook book)
        {
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
                detailMetaText.text = $"★ {book.Rating:0.0}   리뷰 {book.ReviewCount}개   조회 {book.Views:N0}";
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
                    ? $"{book.Reviews[i].Author}  ★ {book.Reviews[i].Rating:0.0}  {book.Reviews[i].Line}"
                    : string.Empty;
            }

            if (detailShelfText != null)
            {
                detailShelfText.text = $"현재 위치  {book.Shelf}";
            }

            if (statusGuideText != null && !selectedBookQueuedForAdd)
            {
                statusGuideText.text = string.Empty;
            }

            for (var i = 0; i < statusButtons.Length; i++)
            {
                if (statusButtons[i] == null)
                {
                    continue;
                }

                var isSelected = i == selectedStatusIndex;
                statusButtons[i].GetComponent<Image>().color = isSelected
                    ? new Color(0.24f, 0.43f, 0.37f, 1f)
                    : new Color(0.90f, 0.89f, 0.84f, 1f);
                var statusLabel = statusButtons[i].GetComponentInChildren<Text>();
                statusLabel.color = isSelected ? Color.white : new Color(0.45f, 0.43f, 0.38f, 1f);
                statusLabel.fontStyle = RuntimeUiFont.ResolveStyle(statusLabel.font, isSelected ? FontStyle.Bold : FontStyle.Normal);
            }

            if (addSelectedBookButton != null)
            {
                addSelectedBookButton.interactable = true;
                addSelectedBookButton.GetComponent<Image>().color = selectedBookQueuedForAdd
                    ? new Color(0.17f, 0.56f, 0.39f, 0.96f)
                    : new Color(0.18f, 0.43f, 0.36f, 0.96f);
            }

            if (addSelectedBookLabel != null)
            {
                addSelectedBookLabel.text = selectedBookQueuedForAdd ? "✓ 추가 준비 완료" : "＋ 내 책장에 추가";
            }
        }

        private void ApplyEmptyBookDetail()
        {
            if (detailCoverImage != null)
            {
                detailCoverImage.color = new Color(0.85f, 0.88f, 0.92f, 1f);
            }

            if (detailTitleText != null)
            {
                detailTitleText.text = "책을 찾을 수 없습니다";
            }

            if (detailAuthorText != null)
            {
                detailAuthorText.text = string.Empty;
            }

            if (detailPublisherText != null)
            {
                detailPublisherText.text = string.Empty;
            }

            if (detailMetaText != null)
            {
                detailMetaText.text = string.Empty;
            }

            if (detailSummaryText != null)
            {
                detailSummaryText.text = "검색어를 바꾸거나 다른 분류를 선택해주세요.";
            }

            for (var i = 0; i < reviewTexts.Length; i++)
            {
                if (reviewTexts[i] != null)
                {
                    reviewTexts[i].text = string.Empty;
                }
            }

            if (detailShelfText != null)
            {
                detailShelfText.text = string.Empty;
            }

            if (statusGuideText != null)
            {
                statusGuideText.text = string.Empty;
            }

            if (addSelectedBookButton != null)
            {
                addSelectedBookButton.interactable = false;
                addSelectedBookButton.GetComponent<Image>().color = new Color(0.78f, 0.82f, 0.88f, 0.96f);
            }

            if (addSelectedBookLabel != null)
            {
                addSelectedBookLabel.text = "＋ 선택 후 추가";
            }
        }

        private void SelectCategory(int index)
        {
            selectedCategoryIndex = Mathf.Clamp(index, 0, BookCategories.Length - 1);
            currentPageIndex = 0;
            selectedBookQueuedForAdd = false;
            Refresh();
        }

        private static string GetCategoryIcon(int index)
        {
            switch (index)
            {
                case 1:
                    return "◐";
                case 2:
                    return "✓";
                case 3:
                    return "○";
                case 4:
                    return "▤";
                case 5:
                    return "▥";
                default:
                    return "▦";
            }
        }

        private void SelectVisibleBook(int slotIndex)
        {
            var filteredCount = CollectFilteredBooks();
            var absoluteIndex = currentPageIndex * BooksPerPage + slotIndex;
            if (absoluteIndex < 0 || absoluteIndex >= filteredCount)
            {
                return;
            }

            SelectBook(visibleBookIndices[absoluteIndex]);
        }

        private void SelectBook(int index)
        {
            if (DummyBooks.Length == 0)
            {
                selectedBookIndex = 0;
                selectedStatusIndex = 1;
                selectedBookQueuedForAdd = false;
                Refresh();
                return;
            }

            selectedBookIndex = Mathf.Clamp(index, 0, DummyBooks.Length - 1);
            selectedBookQueuedForAdd = false;
            selectedStatusIndex = System.Array.IndexOf(ReadingStatuses, DummyBooks[selectedBookIndex].Status);
            if (selectedStatusIndex < 0)
            {
                selectedStatusIndex = 1;
            }

            Refresh();
        }

        private void ChangePage(int delta)
        {
            var filteredCount = CollectFilteredBooks();
            var totalPages = Mathf.Max(1, Mathf.CeilToInt((float)filteredCount / BooksPerPage));
            currentPageIndex = Mathf.Clamp(currentPageIndex + delta, 0, Mathf.Max(0, totalPages - 1));

            var pageStart = currentPageIndex * BooksPerPage;
            if (filteredCount > 0 && pageStart < filteredCount)
            {
                SelectBook(visibleBookIndices[pageStart]);
                return;
            }

            Refresh();
        }

        private void SelectPage(int pageIndex)
        {
            var filteredCount = CollectFilteredBooks();
            var totalPages = Mathf.Max(1, Mathf.CeilToInt((float)filteredCount / BooksPerPage));
            currentPageIndex = Mathf.Clamp(pageIndex, 0, Mathf.Max(0, totalPages - 1));

            var pageStart = currentPageIndex * BooksPerPage;
            if (filteredCount > 0 && pageStart < filteredCount)
            {
                SelectBook(visibleBookIndices[pageStart]);
                return;
            }

            Refresh();
        }

        private void OnBookSearchChanged(string value)
        {
            bookSearchQuery = value ?? string.Empty;
            currentPageIndex = 0;
            selectedBookQueuedForAdd = false;
            if (selectedTab == "Books")
            {
                RefreshBooksTab();
            }
        }

        private void ApplyBookSearch()
        {
            bookSearchQuery = searchField != null ? searchField.text : string.Empty;
            currentPageIndex = 0;
            selectedBookQueuedForAdd = false;
            RefreshBooksTab();
        }

        private void QueueSelectedBookForAdd()
        {
            if (DummyBooks.Length == 0)
            {
                return;
            }

            selectedBookQueuedForAdd = true;
            var book = DummyBooks[Mathf.Clamp(selectedBookIndex, 0, DummyBooks.Length - 1)];
            if (statusGuideText != null)
            {
                statusGuideText.text = $"{book.Title} 추가 요청 준비 완료";
                statusGuideText.color = new Color(0.17f, 0.45f, 0.31f, 1f);
            }

            RefreshBooksTab();
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
            rect.sizeDelta = name == "DashboardCanvas" ? DashboardCanvasSize : ScreenSaverCanvasSize;
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
                screenSaverImage.sprite = GetFallbackAdSprite();
                screenSaverImage.color = Color.white;
                screenSaverImage.preserveAspect = false;
                screenSaverImage.raycastTarget = true;
                selectedAdIndex = 0;
                StretchToFill(screenSaverImage.rectTransform);
                WarnIfAdsMissing();
                return;
            }

            var selectedIndex = useRandomAd && adImages.Length > 1
                ? UnityEngine.Random.Range(0, adImages.Length)
                : 0;

            screenSaverImage.sprite = adImages[selectedIndex];
            screenSaverImage.color = Color.white;
            screenSaverImage.preserveAspect = false;
            screenSaverImage.raycastTarget = true;
            selectedAdIndex = selectedIndex;
            StretchToFill(screenSaverImage.rectTransform);
        }

        private void EnsureAdImages()
        {
            if (adImages != null && adImages.Length > 0)
            {
                return;
            }

            var resourceAds = Resources.LoadAll<Sprite>(AdResourcesPath);
            if (resourceAds != null && resourceAds.Length > 0)
            {
                adImages = resourceAds
                    .Where(sprite => sprite != null)
                    .OrderBy(sprite => sprite.name)
                    .ToArray();
                if (adImages.Length > 0)
                {
                    return;
                }
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

        private static Sprite GetFallbackAdSprite()
        {
            if (fallbackAdSprite != null)
            {
                return fallbackAdSprite;
            }

            const int width = 960;
            const int height = 540;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                var v = y / (float)(height - 1);
                for (var x = 0; x < width; x++)
                {
                    var u = x / (float)(width - 1);
                    var sky = Color.Lerp(new Color(0.12f, 0.18f, 0.26f), new Color(0.28f, 0.58f, 0.78f), u);
                    var warm = Color.Lerp(new Color(0.92f, 0.78f, 0.56f), new Color(0.48f, 0.68f, 0.82f), v);
                    var color = Color.Lerp(sky, warm, 0.32f);

                    var shelfBand = Mathf.Abs(v - 0.58f) < 0.055f || Mathf.Abs(v - 0.78f) < 0.035f;
                    if (shelfBand)
                    {
                        color = Color.Lerp(color, new Color(0.94f, 0.84f, 0.66f), 0.45f);
                    }

                    var glow = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(0.72f, 0.34f)) * 2.2f);
                    color = Color.Lerp(color, new Color(0.86f, 0.96f, 1f), glow * 0.28f);
                    pixels[y * width + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            fallbackAdSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
            fallbackAdSprite.name = "GeneratedNotebookAd";
            fallbackAdSprite.hideFlags = HideFlags.HideAndDontSave;
            return fallbackAdSprite;
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
            ApplyPanelChrome(panelObject, color);

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
            image.color = new Color(0.23f, 0.25f, 0.24f, 0.96f);
            ApplyButtonChrome(buttonObject);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            var colors = button.colors;
            colors.highlightedColor = new Color(0.32f, 0.48f, 0.42f, 1f);
            colors.pressedColor = new Color(0.16f, 0.29f, 0.25f, 1f);
            colors.selectedColor = new Color(0.22f, 0.45f, 0.38f, 1f);
            colors.disabledColor = new Color(0.64f, 0.66f, 0.63f, 0.75f);
            button.colors = colors;

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
            image.color = new Color(0.96f, 0.97f, 0.99f, 1f);
            ApplyPanelChrome(inputObject, image.color);

            var inputField = inputObject.AddComponent<InputField>();
            inputField.targetGraphic = image;
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.selectionColor = new Color(0.28f, 0.48f, 0.86f, 0.28f);
            var rect = inputObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var textWidth = Mathf.Max(24f, size.x - 24f);
            var placeholderText = CreateLabel(inputObject.transform, "Placeholder", placeholder, new Vector2(12f, -8f), new Vector2(textWidth, 18f), 13, TextAnchor.UpperLeft, new Color(0.57f, 0.60f, 0.64f, 1f), new Vector2(0f, 1f));
            var text = CreateLabel(inputObject.transform, "Text", string.Empty, new Vector2(12f, -8f), new Vector2(textWidth, 18f), 13, TextAnchor.UpperLeft, new Color(0.18f, 0.20f, 0.22f, 1f), new Vector2(0f, 1f));
            inputField.textComponent = text;
            inputField.placeholder = placeholderText;
            return inputField;
        }

        private static void ApplyRoundedImage(Image image, bool pill)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = pill ? GetRoundedPillSprite() : GetRoundedPanelSprite();
            image.type = Image.Type.Sliced;
        }

        private static Sprite GetRoundedPanelSprite()
        {
            if (roundedPanelSprite == null)
            {
                roundedPanelSprite = CreateRoundedSprite(256, 48);
            }

            return roundedPanelSprite;
        }

        private static Sprite GetRoundedPillSprite()
        {
            if (roundedPillSprite == null)
            {
                roundedPillSprite = CreateRoundedSprite(256, 96);
            }

            return roundedPillSprite;
        }

        private static Sprite CreateRoundedSprite(int size, int radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.ARGB32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            var cornerCenter = radius - 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x < radius ? cornerCenter - x : x >= size - radius ? x - (size - radius - 0.5f) : 0f;
                    var dy = y < radius ? cornerCenter - y : y >= size - radius ? y - (size - radius - 0.5f) : 0f;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = dx == 0f && dy == 0f
                        ? 1f
                        : Mathf.Clamp01(radius + 0.5f - distance);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var border = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static void ApplyPanelChrome(GameObject target, Color baseColor)
        {
            if (target == null || baseColor.a <= 0.02f)
            {
                return;
            }

            ApplyRoundedImage(target.GetComponent<Image>(), false);

            var luminance = baseColor.r * 0.2126f + baseColor.g * 0.7152f + baseColor.b * 0.0722f;
            var outline = target.AddComponent<Outline>();
            outline.effectDistance = new Vector2(1f, -1f);
            outline.effectColor = luminance > 0.45f
                ? new Color(0.18f, 0.20f, 0.18f, 0.12f)
                : new Color(1f, 1f, 1f, 0.08f);

            if (baseColor.a < 0.45f)
            {
                return;
            }

            var shadow = target.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(0f, -2f);
            shadow.effectColor = new Color(0f, 0f, 0f, luminance > 0.45f ? 0.11f : 0.18f);
            shadow.useGraphicAlpha = true;
        }

        private static void ApplyButtonChrome(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            ApplyRoundedImage(target.GetComponent<Image>(), true);

            var outline = target.AddComponent<Outline>();
            outline.effectDistance = new Vector2(1f, -1f);
            outline.effectColor = new Color(1f, 1f, 1f, 0.12f);

            var shadow = target.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(0f, -2f);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.16f);
            shadow.useGraphicAlpha = true;
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
            viewportRect.offsetMin = new Vector2(0f, 62f);
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
            text.font = ResolveUiFont();
            text.color = color;
            text.alignment = alignment;
            text.fontSize = fontSize;
            text.fontStyle = RuntimeUiFont.ResolveStyle(text.font, fontStyle);
            text.text = textValue;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(8, fontSize - 4);
            text.resizeTextMaxSize = fontSize;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.lineSpacing = 1f;
            text.raycastTarget = false;

            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return text;
        }

        private static Font ResolveUiFont()
        {
            cachedUiFont ??= RuntimeUiFont.Resolve();
            return cachedUiFont;
        }

        private static void UpdateTabButton(Button button, bool isSelected)
        {
            if (button == null)
            {
                return;
            }

            button.GetComponent<Image>().color = isSelected
                ? new Color(0.20f, 0.43f, 0.36f, 0.96f)
                : new Color(0.80f, 0.83f, 0.78f, 0.96f);

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.color = isSelected ? Color.white : new Color(0.18f, 0.20f, 0.19f, 1f);
                label.fontStyle = RuntimeUiFont.ResolveStyle(label.font, isSelected ? FontStyle.Bold : FontStyle.Normal);
            }
        }
    }
}
