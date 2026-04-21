using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BookshelfPorting.Runtime
{
    public enum ExperienceFocusArea
    {
        QuarterView,
        Bookshelf,
        Guestbook,
        Notebook
    }

    public class BookshelfExperienceManager : MonoBehaviour
    {
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
        private static readonly Vector2 GuestbookBoardSize = new(1380f, 960f);
        private static readonly Vector2 GuestbookPreviewCardSize = new(248f, 142f);
        private static readonly Vector2 GuestbookFocusedNoteBaseSize = new(246f, 156f);
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
        [SerializeField] private MaterialFactory materialFactory = null;
        [SerializeField] private BookshelfRuntimeDataStore dataStore = null;
        [SerializeField] private bool isEditMode;
        [SerializeField] private NotebookScreenController notebookScreenController = null;

        private Canvas hudCanvas;
        private Button editModeButton;
        private GameObject editModePanel;
        private Text overlayModeText;
        private Text editSelectionText;
        private Text overlayHintText;
        private Button overlayEditModeButton;
        private Button overlayPreviousFocusButton;
        private Button overlayNextFocusButton;
        private GameObject bookshelfToolbarPanel;
        private Button spineOrientationButton;
        private Button frontOrientationButton;
        private Button angledOrientationButton;
        private Button arrangePopupButton;
        private GameObject arrangePopupPanel;
        private Button[] arrangeOptionButtons = new Button[3];
        private Text arrangePopupHintText;
        private Button closeBookViewButton;
        private GameObject editModeHintPanel;
        private Text editModeHintText;

        private GameObject guestbookPanel;
        private GameObject guestbookPreviewPanel;
        private Canvas guestbookBoardCanvas;
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
        private Text bookTitleText;
        private Text bookMetaText;
        private Button readToggleButton;
        private Button visibilityToggleButton;

        private readonly Button[] themeButtons = new Button[3];
        private GameObject hamburgerMenuPanel;
        private Button hamburgerButton;
        private Button focusBackButton;
        private bool isHamburgerMenuOpen;
        private bool isArrangePopupOpen;

        public ExperienceFocusArea CurrentFocusArea { get; private set; } = ExperienceFocusArea.QuarterView;
        public bool IsEditMode => isEditMode;
        public bool IsBookshelfFocused => CurrentFocusArea == ExperienceFocusArea.Bookshelf;
        public BookEntity SelectedEditBook { get; private set; }
        public BookEntity ActiveDetailBook { get; private set; }

        public void Configure(
            BookshelfState bookshelfState,
            CameraController controller,
            BookInteractionController interaction,
            MaterialFactory factory,
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
            }

            state = bookshelfState;
            cameraController = controller;
            interactionController = interaction;
            bookshelfGenerator = generator;
            materialFactory = factory;
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
                }
            }
        }

        private void Awake()
        {
            EnsureEventSystem();
            EnsureHud();
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
                }
            }

            ConfigureNotebookScreen();
            EnsureGuestbookBoardUi();
            RefreshHud();
        }

        private void Update()
        {
            if (HandleFocusShortcuts())
            {
                return;
            }

            if (cameraController != null && cameraController.IsTransitioning)
            {
                return;
            }

            if (Mouse.current == null || IsPointerOverUi())
            {
                return;
            }

            if (!Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (CurrentFocusArea == ExperienceFocusArea.QuarterView && TryHitBookshelf())
            {
                FocusBookshelf();
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
        }

        private void OnEnable()
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
            }
        }

        public void FocusQuarterView()
        {
            if (!CanStartCameraFocus())
            {
                return;
            }

            PrepareQuarterViewReturn();
            cameraController.MoveToMode(CameraMode.Overview);
        }

        public void FocusBookshelf()
        {
            if (!CanStartCameraFocus())
            {
                return;
            }

            cameraController.MoveToMode(CameraMode.Frontal);
        }

        public void FocusGuestbook()
        {
            if (!CanStartCameraFocus())
            {
                return;
            }

            cameraController.MoveToMode(CameraMode.Whiteboard);
        }

        public void FocusNotebook()
        {
            if (!CanStartCameraFocus())
            {
                return;
            }

            ConfigureNotebookScreen();
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
            if (!CanStartCameraFocus())
            {
                return;
            }

            cameraController.MoveToMode(CameraMode.Overview);
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

        public void ToggleEditMode()
        {
            if (CurrentFocusArea != ExperienceFocusArea.Bookshelf ||
                (bookshelfGenerator != null && bookshelfGenerator.IsBookshelfSwitching))
            {
                return;
            }

            isEditMode = !isEditMode;
            isArrangePopupOpen = false;

            if (isEditMode)
            {
                CloseOverlayPanels();
                if (state != null && state.IsViewingBook && interactionController != null)
                {
                    interactionController.CloseViewedBook(true);
                }
            }
            else
            {
                ClearEditSelection();
            }

            RefreshHud();
        }

        public void SelectEditBook(BookEntity book)
        {
            if (!isEditMode)
            {
                return;
            }

            SelectedEditBook = book;
            RefreshHud();
        }

        public void ClearEditSelection()
        {
            SelectedEditBook = null;
            RefreshHud();
        }

        public void ApplyTheme(BookshelfThemeStyle theme)
        {
            if (materialFactory == null)
            {
                return;
            }

            materialFactory.ApplyTheme(theme);
            isArrangePopupOpen = false;
            RefreshHud();
        }

        public void HandleBookViewed(BookEntity book)
        {
            ActiveDetailBook = book;
            if (bookDetailPanel != null)
            {
                bookDetailPanel.SetActive(false);
            }

            RefreshHud();
        }

        public void ToggleFocusedBookDetail(BookEntity book)
        {
            if (isEditMode)
            {
                return;
            }

            if (bookDetailPanel != null && bookDetailPanel.activeSelf && ActiveDetailBook == book)
            {
                CloseBookDetailPanel();
                return;
            }

            OpenBookDetailPanel(book);
            RefreshHud();
        }

        public void HandleBookClosed()
        {
            ActiveDetailBook = null;
            if (bookDetailPanel != null)
            {
                bookDetailPanel.SetActive(false);
            }

            RefreshHud();
        }

        public BookshelfRuntimeSnapshotData CreateRuntimeSnapshot()
        {
            return dataStore != null
                ? dataStore.CreateSnapshot(CurrentFocusArea, isEditMode, SelectedEditBook != null ? SelectedEditBook : ActiveDetailBook)
                : new BookshelfRuntimeSnapshotData();
        }

        private void OpenBookDetailPanel(BookEntity book)
        {
            if (book == null || dataStore == null)
            {
                return;
            }

            CloseOverlayPanels(bookDetailPanel);
            ActiveDetailBook = book;
            bookDetailPanel.SetActive(true);
            RefreshBookDetailPanel();
            RefreshHud();
        }

        private void CloseBookDetailPanel()
        {
            if (bookDetailPanel != null)
            {
                bookDetailPanel.SetActive(false);
            }

            RefreshHud();
        }

        private void ToggleReadState()
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

            meta.isRead = !meta.isRead;
            RefreshBookDetailPanel();
            RefreshHud();
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

            meta.isPublic = !meta.isPublic;
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
            RefreshHud();
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

            if (CurrentFocusArea != ExperienceFocusArea.Bookshelf && isEditMode)
            {
                isEditMode = false;
            }

            if (CurrentFocusArea != ExperienceFocusArea.Bookshelf)
            {
                SelectedEditBook = null;
                isArrangePopupOpen = false;
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

            RefreshHud();
        }

        private void EnsureHud()
        {
            var canvasObject = new GameObject("ExperienceHud");
            hudCanvas = canvasObject.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            hamburgerButton = CreateButton(canvasObject.transform, "HamburgerButton", "Menu", new Vector2(-20f, -20f), ToggleHamburgerMenu, new Vector2(72f, 44f), new Vector2(1f, 1f));
            hamburgerButton.GetComponentInChildren<Text>().fontSize = 26;
            StyleHudPanel(hamburgerButton.GetComponent<Image>(), new Color(0.08f, 0.09f, 0.11f, 0.78f));

            focusBackButton = CreateButton(canvasObject.transform, "FocusBackButton", "Back", new Vector2(20f, -20f), ReturnToQuarterView, new Vector2(120f, 44f), new Vector2(0f, 1f));
            StyleHudPanel(focusBackButton.GetComponent<Image>(), new Color(0.08f, 0.09f, 0.11f, 0.78f));

            hamburgerMenuPanel = CreatePanel(canvasObject.transform, "HamburgerMenuPanel", new Vector2(-20f, -74f), new Vector2(190f, 204f), new Vector2(1f, 1f));
            StyleHudPanel(hamburgerMenuPanel.GetComponent<Image>(), new Color(0.08f, 0.09f, 0.11f, 0.84f));
            CreateButton(hamburgerMenuPanel.transform, "QuarterViewButton", "Quarter View", new Vector2(-10f, -10f), () => RunMenuAction(FocusQuarterView));
            CreateButton(hamburgerMenuPanel.transform, "BookshelfButton", "Bookshelf", new Vector2(-10f, -58f), () => RunMenuAction(FocusBookshelf));
            CreateButton(hamburgerMenuPanel.transform, "GuestbookButton", "Guestbook", new Vector2(-10f, -106f), () => RunMenuAction(FocusGuestbook));
            editModeButton = CreateButton(hamburgerMenuPanel.transform, "EditModeButton", "Edit Mode: Off", new Vector2(-10f, -154f), () => RunMenuAction(ToggleEditMode));

            editModePanel = CreatePanel(canvasObject.transform, "BookshelfStatusPanel", new Vector2(20f, -76f), new Vector2(372f, 110f), new Vector2(0f, 1f));
            StyleHudPanel(editModePanel.GetComponent<Image>(), new Color(0.08f, 0.10f, 0.13f, 0.78f));
            overlayModeText = CreateLabel(editModePanel.transform, "OverlayModeText", new Vector2(16f, -14f), new Vector2(140f, 18f), TextAnchor.UpperLeft);
            overlayModeText.fontSize = 13;
            overlayModeText.fontStyle = FontStyle.Bold;
            overlayModeText.color = new Color32(255, 235, 204, 255);
            editSelectionText = CreateLabel(editModePanel.transform, "EditSelectionText", new Vector2(16f, -36f), new Vector2(340f, 24f), TextAnchor.UpperLeft);
            editSelectionText.fontSize = 18;
            editSelectionText.fontStyle = FontStyle.Bold;
            editSelectionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            overlayHintText = CreateLabel(editModePanel.transform, "OverlayHintText", new Vector2(16f, -64f), new Vector2(340f, 34f), TextAnchor.UpperLeft);
            overlayHintText.fontSize = 13;
            overlayHintText.color = new Color32(219, 224, 229, 255);
            overlayHintText.horizontalOverflow = HorizontalWrapMode.Wrap;

            overlayEditModeButton = CreateButton(canvasObject.transform, "OverlayEditModeButton", "Edit Mode", new Vector2(-20f, -20f), ToggleEditMode, new Vector2(132f, 44f), new Vector2(1f, 1f));
            overlayPreviousFocusButton = CreateButton(canvasObject.transform, "OverlayPreviousFocusButton", "<", new Vector2(20f, 0f), ShowPreviousBookshelf, new Vector2(46f, 84f), new Vector2(0f, 0.5f));
            overlayNextFocusButton = CreateButton(canvasObject.transform, "OverlayNextFocusButton", ">", new Vector2(-20f, 0f), ShowNextBookshelf, new Vector2(46f, 84f), new Vector2(1f, 0.5f));

            bookshelfToolbarPanel = CreatePanel(canvasObject.transform, "BookshelfToolbarPanel", new Vector2(0f, 20f), new Vector2(872f, 84f), new Vector2(0.5f, 0f));
            StyleHudPanel(bookshelfToolbarPanel.GetComponent<Image>(), new Color(0.08f, 0.10f, 0.13f, 0.74f));

            var rotateHeader = CreateLabel(bookshelfToolbarPanel.transform, "RotateHeader", new Vector2(18f, -12f), new Vector2(64f, 16f), TextAnchor.UpperLeft);
            rotateHeader.text = "Rotate";
            rotateHeader.fontSize = 12;
            rotateHeader.fontStyle = FontStyle.Bold;
            rotateHeader.color = new Color32(255, 235, 204, 255);

            spineOrientationButton = CreateButton(bookshelfToolbarPanel.transform, "SpineOrientationButton", "Spine", new Vector2(18f, 0f), () => ApplyOrientationFromUi(BookOrientation.Spine), new Vector2(92f, 38f), new Vector2(0f, 0.5f));
            frontOrientationButton = CreateButton(bookshelfToolbarPanel.transform, "FrontOrientationButton", "Front", new Vector2(120f, 0f), () => ApplyOrientationFromUi(BookOrientation.Front), new Vector2(92f, 38f), new Vector2(0f, 0.5f));
            angledOrientationButton = CreateButton(bookshelfToolbarPanel.transform, "AngledOrientationButton", "Angled45", new Vector2(222f, 0f), () => ApplyOrientationFromUi(BookOrientation.Angled45), new Vector2(108f, 38f), new Vector2(0f, 0.5f));

            var arrangeHeader = CreateLabel(bookshelfToolbarPanel.transform, "ArrangeHeader", new Vector2(348f, -12f), new Vector2(70f, 16f), TextAnchor.UpperLeft);
            arrangeHeader.text = "Arrange";
            arrangeHeader.fontSize = 12;
            arrangeHeader.fontStyle = FontStyle.Bold;
            arrangeHeader.color = new Color32(255, 235, 204, 255);
            arrangePopupButton = CreateButton(bookshelfToolbarPanel.transform, "ArrangePopupButton", "Arrange", new Vector2(348f, 0f), ToggleArrangePopup, new Vector2(90f, 38f), new Vector2(0f, 0.5f));

            arrangePopupPanel = CreatePanel(bookshelfToolbarPanel.transform, "ArrangePopupPanel", new Vector2(346f, 88f), new Vector2(252f, 108f), new Vector2(0f, 0f));
            StyleHudPanel(arrangePopupPanel.GetComponent<Image>(), new Color(0.08f, 0.10f, 0.13f, 0.90f));
            arrangeOptionButtons[0] = CreateButton(arrangePopupPanel.transform, "ArrangeLeftButton", "Left", new Vector2(12f, -12f), null, new Vector2(68f, 32f), new Vector2(0f, 1f));
            arrangeOptionButtons[1] = CreateButton(arrangePopupPanel.transform, "ArrangeCenterButton", "Center", new Vector2(88f, -12f), null, new Vector2(72f, 32f), new Vector2(0f, 1f));
            arrangeOptionButtons[2] = CreateButton(arrangePopupPanel.transform, "ArrangeRightButton", "Right", new Vector2(168f, -12f), null, new Vector2(68f, 32f), new Vector2(0f, 1f));
            arrangePopupHintText = CreateLabel(arrangePopupPanel.transform, "ArrangePopupHintText", new Vector2(12f, -56f), new Vector2(228f, 40f), TextAnchor.UpperLeft);
            arrangePopupHintText.fontSize = 12;
            arrangePopupHintText.color = new Color32(219, 224, 229, 255);
            arrangePopupHintText.text = "Current shelf placement uses drag-and-drop with highlighted slots.";
            arrangePopupHintText.horizontalOverflow = HorizontalWrapMode.Wrap;

            var themeHeader = CreateLabel(bookshelfToolbarPanel.transform, "ThemeHeader", new Vector2(458f, -12f), new Vector2(64f, 16f), TextAnchor.UpperLeft);
            themeHeader.text = "Theme";
            themeHeader.fontSize = 12;
            themeHeader.fontStyle = FontStyle.Bold;
            themeHeader.color = new Color32(255, 235, 204, 255);
            themeButtons[0] = CreateButton(bookshelfToolbarPanel.transform, "ThemeDefaultButton", "Default", new Vector2(458f, 0f), () => ApplyTheme(BookshelfThemeStyle.Default), new Vector2(82f, 38f), new Vector2(0f, 0.5f));
            themeButtons[1] = CreateButton(bookshelfToolbarPanel.transform, "ThemeWarmButton", "Warm", new Vector2(550f, 0f), () => ApplyTheme(BookshelfThemeStyle.Warm), new Vector2(74f, 38f), new Vector2(0f, 0.5f));
            themeButtons[2] = CreateButton(bookshelfToolbarPanel.transform, "ThemeDarkButton", "Dark", new Vector2(634f, 0f), () => ApplyTheme(BookshelfThemeStyle.Dark), new Vector2(74f, 38f), new Vector2(0f, 0.5f));

            var viewHeader = CreateLabel(bookshelfToolbarPanel.transform, "ViewHeader", new Vector2(728f, -12f), new Vector2(54f, 16f), TextAnchor.UpperLeft);
            viewHeader.text = "View";
            viewHeader.fontSize = 12;
            viewHeader.fontStyle = FontStyle.Bold;
            viewHeader.color = new Color32(255, 235, 204, 255);
            closeBookViewButton = CreateButton(bookshelfToolbarPanel.transform, "CloseBookViewButton", "Back", new Vector2(728f, 0f), CloseViewedBookFromUi, new Vector2(108f, 38f), new Vector2(0f, 0.5f));

            editModeHintPanel = CreatePanel(canvasObject.transform, "EditModeHintPanel", new Vector2(0f, 112f), new Vector2(420f, 34f), new Vector2(0.5f, 0f));
            StyleHudPanel(editModeHintPanel.GetComponent<Image>(), new Color(0.09f, 0.11f, 0.14f, 0.72f));
            editModeHintText = CreateLabel(editModeHintPanel.transform, "EditModeHintText", Vector2.zero, new Vector2(400f, 22f), TextAnchor.MiddleCenter);
            editModeHintText.fontSize = 13;
            editModeHintText.fontStyle = FontStyle.Bold;
            editModeHintText.text = "Drag books to move them. Available sections will be highlighted.";

            bookDetailPanel = CreatePanel(canvasObject.transform, "BookDetailPanel", new Vector2(-20f, -76f), new Vector2(332f, 238f), new Vector2(1f, 1f));
            StyleHudPanel(bookDetailPanel.GetComponent<Image>(), new Color(0.08f, 0.10f, 0.13f, 0.84f));
            bookDetailPanel.SetActive(false);
            bookTitleText = CreateLabel(bookDetailPanel.transform, "BookTitleText", new Vector2(16f, -16f), new Vector2(296f, 28f), TextAnchor.UpperLeft);
            bookTitleText.fontSize = 20;
            bookTitleText.fontStyle = FontStyle.Bold;
            bookTitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bookMetaText = CreateLabel(bookDetailPanel.transform, "BookMetaText", new Vector2(16f, -54f), new Vector2(296f, 102f), TextAnchor.UpperLeft);
            bookMetaText.fontSize = 14;
            bookMetaText.horizontalOverflow = HorizontalWrapMode.Wrap;
            readToggleButton = CreateButton(bookDetailPanel.transform, "ReadToggleButton", "Mark Read", new Vector2(16f, -168f), ToggleReadState, new Vector2(138f, 36f), new Vector2(0f, 1f));
            visibilityToggleButton = CreateButton(bookDetailPanel.transform, "VisibilityToggleButton", "Public", new Vector2(170f, -168f), ToggleVisibilityState, new Vector2(142f, 36f), new Vector2(0f, 1f));
            CreateButton(bookDetailPanel.transform, "CloseBookDetailButton", "Close", new Vector2(16f, -212f), CloseBookDetailPanel, new Vector2(296f, 34f), new Vector2(0f, 1f));

            guestbookPanel = null;

            RefreshHud();
        }

        private void ToggleHamburgerMenu()
        {
            isHamburgerMenuOpen = !isHamburgerMenuOpen;
            RefreshHud();
        }

        private void RunMenuAction(UnityEngine.Events.UnityAction action)
        {
            action?.Invoke();
            isHamburgerMenuOpen = false;
            RefreshHud();
        }

        private void ReturnToQuarterView()
        {
            FocusQuarterView();
        }

        private void PrepareQuarterViewReturn()
        {
            isHamburgerMenuOpen = false;
            isArrangePopupOpen = false;
            isEditMode = false;
            SelectedEditBook = null;

            if (state != null && state.IsViewingBook && interactionController != null)
            {
                interactionController.CloseViewedBook(true);
            }

            if (bookDetailPanel != null)
            {
                bookDetailPanel.SetActive(false);
            }

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
                hamburgerMenuPanel.SetActive(isHamburgerMenuOpen && !isBookshelfFocused);
            }

            if (hamburgerButton != null)
            {
                hamburgerButton.gameObject.SetActive(CurrentFocusArea != ExperienceFocusArea.Notebook && !isBookshelfFocused);
            }

            if (focusBackButton != null)
            {
                var showBackButton = CurrentFocusArea != ExperienceFocusArea.QuarterView;
                focusBackButton.gameObject.SetActive(showBackButton);
                focusBackButton.interactable = showBackButton && (cameraController == null || !cameraController.IsTransitioning);
                StyleHudButton(
                    focusBackButton,
                    new Color32(255, 255, 255, 235),
                    new Color32(241, 235, 230, 255),
                    new Color32(224, 219, 214, 255),
                    16,
                    new Color32(45, 50, 58, 255),
                    true);
            }

            if (editModeButton != null)
            {
                editModeButton.GetComponentInChildren<Text>().text = $"Edit Mode: {(isEditMode ? "On" : "Off")}";
                editModeButton.interactable = CurrentFocusArea == ExperienceFocusArea.Bookshelf;
            }

            if (editModePanel != null)
            {
                editModePanel.SetActive(isBookshelfFocused && isEditMode && !isViewingBook);
            }

            RefreshBookshelfOverlay();
            RefreshThemeButtons();
            RefreshGuestbookPanel();
            RefreshBookDetailPanel();
        }

        private void RefreshBookshelfOverlay()
        {
            var isBookshelfFocused = CurrentFocusArea == ExperienceFocusArea.Bookshelf;
            var isBookshelfSwitching = bookshelfGenerator != null && bookshelfGenerator.IsBookshelfSwitching;
            var isViewingBook = state != null && state.IsViewingBook;
            var canSwitchBookshelf = !isBookshelfSwitching && bookshelfGenerator != null && bookshelfGenerator.BookshelfCount > 1;
            if (!isBookshelfFocused)
            {
                isArrangePopupOpen = false;
            }

            if (overlayEditModeButton != null)
            {
                overlayEditModeButton.gameObject.SetActive(isBookshelfFocused && !isViewingBook);
                overlayEditModeButton.interactable = isBookshelfFocused && !isBookshelfSwitching && !isViewingBook;
                var editLabel = isEditMode ? "Edit Mode On" : "Edit Mode Off";
                SetButtonLabel(overlayEditModeButton, editLabel);
                StyleHudButton(
                    overlayEditModeButton,
                    isEditMode ? new Color32(173, 122, 79, 255) : new Color32(255, 255, 255, 235),
                    isEditMode ? new Color32(151, 104, 64, 255) : new Color32(241, 235, 230, 255),
                    new Color32(224, 219, 214, 255),
                    16,
                    isEditMode ? Color.white : new Color32(45, 50, 58, 255),
                    true);
            }

            if (overlayPreviousFocusButton != null)
            {
                overlayPreviousFocusButton.gameObject.SetActive(isBookshelfFocused && !isViewingBook);
                overlayPreviousFocusButton.interactable = canSwitchBookshelf;
                StyleHudButton(
                    overlayPreviousFocusButton,
                    new Color32(255, 255, 255, 216),
                    new Color32(241, 235, 230, 255),
                    new Color32(224, 219, 214, 255),
                    28,
                    new Color32(45, 50, 58, 255),
                    true);
            }

            if (overlayNextFocusButton != null)
            {
                overlayNextFocusButton.gameObject.SetActive(isBookshelfFocused && !isViewingBook);
                overlayNextFocusButton.interactable = canSwitchBookshelf;
                StyleHudButton(
                    overlayNextFocusButton,
                    new Color32(255, 255, 255, 216),
                    new Color32(241, 235, 230, 255),
                    new Color32(224, 219, 214, 255),
                    28,
                    new Color32(45, 50, 58, 255),
                    true);
            }

            if (bookshelfToolbarPanel != null)
            {
                bookshelfToolbarPanel.SetActive(isBookshelfFocused && isEditMode && !isViewingBook);
            }

            if (arrangePopupPanel != null)
            {
                arrangePopupPanel.SetActive(isBookshelfFocused && isEditMode && !isViewingBook && isArrangePopupOpen && !isBookshelfSwitching);
            }

            if (editModeHintPanel != null)
            {
                editModeHintPanel.SetActive(isBookshelfFocused && !isViewingBook && isEditMode);
            }

            if (!isBookshelfFocused)
            {
                return;
            }

            var contextBook = GetBookshelfContextBook();
            var selectedTitle = TryGetBookDisplayTitle(contextBook);

            if (overlayModeText != null)
            {
                overlayModeText.text = isEditMode ? "Current Mode: Edit Mode" : "Current Mode: View Mode";
            }

            if (editSelectionText != null)
            {
                var selectionPrefix = isViewingBook ? "Active Book" : isEditMode ? "Selected Book" : "Book";
                editSelectionText.text = string.IsNullOrWhiteSpace(selectedTitle)
                    ? "No book selected"
                    : $"{selectionPrefix}: {selectedTitle}";
            }

            if (overlayHintText != null)
            {
                overlayHintText.text = isBookshelfSwitching
                    ? "Shelf is changing. Wait for the next shelf to settle."
                    : isEditMode
                    ? "Drag a selected book to move it. Available sections will highlight."
                    : isViewingBook
                        ? "Click the active book for details. Use the toolbar to rotate or close the view."
                        : "Click a book to inspect it. Turn on Edit Mode to move books.";
            }

            var canRotateBook = contextBook != null && !isBookshelfSwitching;
            UpdateOrientationButton(spineOrientationButton, "Spine", canRotateBook, contextBook != null && contextBook.orientation == BookOrientation.Spine);
            UpdateOrientationButton(frontOrientationButton, "Front", canRotateBook, contextBook != null && contextBook.orientation == BookOrientation.Front);
            UpdateOrientationButton(angledOrientationButton, "Angled45", canRotateBook, contextBook != null && contextBook.orientation == BookOrientation.Angled45);

            if (arrangePopupButton != null)
            {
                arrangePopupButton.gameObject.SetActive(true);
                arrangePopupButton.interactable = !isBookshelfSwitching;
                StyleHudButton(
                    arrangePopupButton,
                    new Color32(255, 255, 255, 235),
                    new Color32(241, 235, 230, 255),
                    new Color32(224, 219, 214, 255),
                    15,
                    new Color32(45, 50, 58, 255));
            }

            for (var i = 0; i < arrangeOptionButtons.Length; i++)
            {
                if (arrangeOptionButtons[i] == null)
                {
                    continue;
                }

                arrangeOptionButtons[i].interactable = false;
                StyleHudButton(
                    arrangeOptionButtons[i],
                    new Color32(224, 226, 230, 255),
                    new Color32(224, 226, 230, 255),
                    new Color32(224, 226, 230, 255),
                    13,
                    new Color32(118, 126, 135, 255));
            }

            if (closeBookViewButton != null)
            {
                closeBookViewButton.interactable = isViewingBook;
                StyleHudButton(
                    closeBookViewButton,
                    isViewingBook ? new Color32(173, 122, 79, 255) : new Color32(255, 255, 255, 235),
                    isViewingBook ? new Color32(151, 104, 64, 255) : new Color32(241, 235, 230, 255),
                    new Color32(224, 219, 214, 255),
                    15,
                    isViewingBook ? Color.white : new Color32(45, 50, 58, 255),
                    isViewingBook);
            }
        }

        private void UpdateOrientationButton(Button button, string label, bool isInteractable, bool isSelected)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = isInteractable;
            SetButtonLabel(button, label);
            StyleHudButton(
                button,
                isSelected ? new Color32(173, 122, 79, 255) : new Color32(255, 255, 255, 235),
                isSelected ? new Color32(151, 104, 64, 255) : new Color32(241, 235, 230, 255),
                new Color32(224, 219, 214, 255),
                15,
                isSelected ? Color.white : new Color32(45, 50, 58, 255),
                isSelected);
        }

        private BookEntity GetBookshelfContextBook()
        {
            if (state != null && state.ActiveViewedBook != null)
            {
                return state.ActiveViewedBook;
            }

            if (SelectedEditBook != null)
            {
                return SelectedEditBook;
            }

            return ActiveDetailBook;
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

        private void ApplyOrientationFromUi(BookOrientation orientation)
        {
            if (interactionController == null ||
                state == null ||
                (bookshelfGenerator != null && bookshelfGenerator.IsBookshelfSwitching))
            {
                return;
            }

            var target = GetBookshelfContextBook();
            if (target == null)
            {
                return;
            }

            state.ChangeBookOrientation(target, orientation, interactionController);
            isArrangePopupOpen = false;
            RefreshHud();
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
                bookshelfGenerator == null ||
                bookshelfGenerator.IsBookshelfSwitching)
            {
                return;
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
            SelectedEditBook = null;
            isArrangePopupOpen = false;

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

        private void CloseViewedBookFromUi()
        {
            if (interactionController == null || state == null || !state.IsViewingBook)
            {
                return;
            }

            interactionController.CloseViewedBook(true);
            isArrangePopupOpen = false;
            RefreshHud();
        }

        private void ToggleArrangePopup()
        {
            if (CurrentFocusArea != ExperienceFocusArea.Bookshelf ||
                (bookshelfGenerator != null && bookshelfGenerator.IsBookshelfSwitching))
            {
                return;
            }

            isArrangePopupOpen = !isArrangePopupOpen;
            RefreshHud();
        }

        private void RefreshBookDetailPanel()
        {
            if (bookDetailPanel == null || !bookDetailPanel.activeSelf || ActiveDetailBook == null || dataStore == null)
            {
                return;
            }

            var meta = dataStore.GetOrCreateBookMeta(ActiveDetailBook);
            if (meta == null)
            {
                return;
            }

            bookTitleText.text = TryGetBookDisplayTitle(ActiveDetailBook);
            bookMetaText.text =
                $"Reading Status\n{(meta.isRead ? "Read" : "Unread")}\n\n" +
                $"Visibility\n{(meta.isPublic ? "Public" : "Private")}\n\n" +
                $"Memo Preview\n{meta.memoPreview}";

            SetButtonLabel(readToggleButton, meta.isRead ? "Mark Unread" : "Mark Read");
            StyleHudButton(
                readToggleButton,
                meta.isRead ? new Color32(173, 122, 79, 255) : new Color32(255, 255, 255, 235),
                meta.isRead ? new Color32(151, 104, 64, 255) : new Color32(241, 235, 230, 255),
                new Color32(224, 219, 214, 255),
                14,
                meta.isRead ? Color.white : new Color32(45, 50, 58, 255),
                meta.isRead);

            SetButtonLabel(visibilityToggleButton, meta.isPublic ? "Public" : "Private");
            StyleHudButton(
                visibilityToggleButton,
                meta.isPublic ? new Color32(88, 133, 117, 255) : new Color32(255, 255, 255, 235),
                meta.isPublic ? new Color32(72, 114, 100, 255) : new Color32(241, 235, 230, 255),
                new Color32(224, 219, 214, 255),
                14,
                meta.isPublic ? Color.white : new Color32(45, 50, 58, 255),
                meta.isPublic);
        }

        private void EnsureGuestbookBoardUi()
        {
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
            guestbookPreviewPanel.AddComponent<RectMask2D>();
            CreateGuestbookPreviewNotes(guestbookPreviewPanel.transform);

            guestbookPanel = CreateWorldPanel(canvasObject.transform, "GuestbookBoardPanel", GuestbookBoardSize);
            var boardRect = guestbookPanel.GetComponent<RectTransform>();
            boardRect.anchorMin = new Vector2(0.5f, 0.5f);
            boardRect.anchorMax = new Vector2(0.5f, 0.5f);
            boardRect.pivot = new Vector2(0.5f, 0.5f);
            boardRect.anchoredPosition = Vector2.zero;
            StyleGuestbookBoardSurface(guestbookPanel);
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
            if (guestbookPanel != null)
            {
                guestbookPanel.SetActive(isFocused);
            }

            if (guestbookPreviewPanel != null)
            {
                guestbookPreviewPanel.SetActive(!isFocused);
            }
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
            if (button == null)
            {
                return;
            }

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                ApplyRoundedSprite(image);
                image.color = background;
            }

            AddSoftShadow(button.gameObject, new Color(0f, 0f, 0f, 0.06f), new Vector2(0f, -2f));

            var colors = button.colors;
            colors.normalColor = background;
            colors.highlightedColor = highlightedBackground;
            colors.pressedColor = Color.Lerp(highlightedBackground, Color.black, 0.12f);
            colors.selectedColor = highlightedBackground;
            colors.disabledColor = disabledBackground;
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontSize = fontSize;
                label.color = textColor;
                label.fontStyle = isBold ? FontStyle.Bold : FontStyle.Normal;
            }
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
            if (image == null)
            {
                return;
            }

            image.sprite = GetRoundedUiSprite();
            image.type = Image.Type.Sliced;
        }

        private static Sprite GetRoundedUiSprite()
        {
            if (roundedUiSprite == null)
            {
                roundedUiTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    name = "GeneratedUiSprite",
                    hideFlags = HideFlags.HideAndDontSave
                };
                roundedUiTexture.SetPixel(0, 0, Color.white);
                roundedUiTexture.Apply();

                roundedUiSprite = Sprite.Create(roundedUiTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
                roundedUiSprite.name = "GeneratedUiSprite";
                roundedUiSprite.hideFlags = HideFlags.HideAndDontSave;
            }

            return roundedUiSprite;
        }

        private static void AddSoftShadow(GameObject target, Color color, Vector2 distance)
        {
            if (target == null)
            {
                return;
            }

            var shadow = target.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = target.AddComponent<Shadow>();
            }

            shadow.effectColor = color;
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private static void StyleHudPanel(Image image, Color background)
        {
            if (image == null)
            {
                return;
            }

            ApplyRoundedSprite(image);
            image.color = background;
            AddSoftShadow(image.gameObject, new Color(0f, 0f, 0f, 0.12f), new Vector2(0f, -5f));
        }

        private static void StyleHudButton(Button button, Color background, Color highlightedBackground, Color disabledBackground, int fontSize, Color textColor, bool isBold = false)
        {
            StyleGuestbookActionButton(button, background, highlightedBackground, disabledBackground, fontSize, textColor, isBold);
        }

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null)
            {
                return;
            }

            var text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
            }
        }

        private void RefreshThemeButtons()
        {
            if (materialFactory == null)
            {
                return;
            }

            UpdateThemeButton(themeButtons[0], materialFactory.CurrentTheme == BookshelfThemeStyle.Default, "Default");
            UpdateThemeButton(themeButtons[1], materialFactory.CurrentTheme == BookshelfThemeStyle.Warm, "Warm");
            UpdateThemeButton(themeButtons[2], materialFactory.CurrentTheme == BookshelfThemeStyle.Dark, "Dark");
        }

        private void CloseOverlayPanels(GameObject except = null)
        {
            if (bookDetailPanel != null && bookDetailPanel != except)
            {
                bookDetailPanel.SetActive(false);
            }

            if (guestbookPanel != null && guestbookPanel != except)
            {
                guestbookPanel.SetActive(false);
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
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

        private static void UpdateThemeButton(Button button, bool isSelected, string label)
        {
            if (button == null)
            {
                return;
            }

            SetButtonLabel(button, label);
            StyleHudButton(
                button,
                isSelected ? new Color32(173, 122, 79, 255) : new Color32(255, 255, 255, 235),
                isSelected ? new Color32(151, 104, 64, 255) : new Color32(241, 235, 230, 255),
                new Color32(224, 219, 214, 255),
                14,
                isSelected ? Color.white : new Color32(45, 50, 58, 255),
                isSelected);
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
        {
            return CreateButton(parent, name, label, anchoredPosition, onClick, new Vector2(170f, 40f), new Vector2(1f, 1f));
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick, Vector2 size, Vector2 anchor)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.11f, 0.12f, 0.14f, 0.86f);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var labelText = CreateLabel(buttonObject.transform, "Label", Vector2.zero, size, TextAnchor.MiddleCenter);
            labelText.text = label;
            labelText.fontSize = 18;

            return button;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
        {
            var panelObject = new GameObject(name);
            panelObject.transform.SetParent(parent, false);

            var image = panelObject.AddComponent<Image>();
            image.color = new Color(0.06f, 0.07f, 0.09f, 0.88f);
            image.raycastTarget = false;

            var rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return panelObject;
        }

        private static Text CreateLabel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, TextAnchor alignment)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = Color.white;
            text.alignment = alignment;
            text.text = string.Empty;
            text.raycastTarget = false;

            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = alignment == TextAnchor.UpperLeft ? new Vector2(0f, 1f) : Vector2.zero;
            rect.anchorMax = alignment == TextAnchor.UpperLeft ? new Vector2(0f, 1f) : Vector2.one;
            rect.pivot = alignment == TextAnchor.UpperLeft ? new Vector2(0f, 1f) : new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            return text;
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
                if (collider == null)
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
            if (notebookScreenController == null)
            {
                notebookScreenController = FindFirstObjectByType<NotebookScreenController>();
            }

            if (notebookScreenController == null || Camera.main == null)
            {
                return;
            }

            var screenTransform = notebookScreenController.transform.Find("Screen");
            notebookScreenController.Configure(screenTransform, Camera.main, () => notebookScreenController.ReturnToScreenSaver());
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
                if (hits[i].distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hits[i].distance;
                closestCollider = hits[i].collider;
            }

            return closestCollider != null && closestCollider.GetComponent<T>() != null;
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
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
