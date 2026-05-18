using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using App.Auth;
using App.Infrastructure.Api;
using App.Infrastructure.Api.Dtos;
using BookshelfPorting.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace App.Bootstrap
{
    [DefaultExecutionOrder(-700)]
    [DisallowMultipleComponent]
    public sealed class SettingsScreenController : MonoBehaviour
    {
        private const string CanvasName = "SettingsCanvas";
        private const string PreferredKoreanFontAssetName = "NotoSansKR-Regular";
        private const string FallbackKoreanFontAssetName = "NOTOSANSKR SDF";
        private const int UiLayer = 5;
        private const int CanvasSortingOrder = 6000;
        private const float CardWidth = 880f;
        private const float CardHeight = 1500f;
        private const float HeaderHeight = 96f;
        private const float PrimaryControlHeight = 72f;
        private const float SecondaryControlHeight = 60f;
        private const float SettingsButtonWidth = 132f;
        private const float SettingsButtonHeight = 60f;
        private const float SettingsButtonMargin = 32f;
        private const float SettingsButtonStackGap = 12f;
        private const float LeftPaneRatio = 0.3f;
        private const float LeftMenuItemHeight = 60f;

        private static readonly Color BackgroundColor = Hex("#F4EFE6");
        private static readonly Color CardColor = Hex("#FFFDF9");
        private static readonly Color AccentTint = Hex("#D9C7A7");
        private static readonly Color PrimaryColor = Hex("#2B5C8A");
        private static readonly Color PrimaryPressedColor = Hex("#21496D");
        private static readonly Color PrimaryHighlightColor = Hex("#3A749F");
        private static readonly Color SecondaryColor = Hex("#F8F1E4");
        private static readonly Color BorderColor = Hex("#D8CCBC");
        private static readonly Color TextColor = Hex("#2D2418");
        private static readonly Color MutedTextColor = Hex("#7C6A56");
        private static readonly Color SubtleTextColor = Hex("#A39079");
        private static readonly Color ErrorColor = Hex("#C14E4E");
        private static readonly Color SuccessColor = Hex("#2F8B57");
        private static readonly Color InputColor = Hex("#FDF9F3");
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color ToggleOnColor = Hex("#2B5C8A");
        private static readonly Color ToggleOffColor = Hex("#CBBFA9");
        private static readonly Color SelectedTabColor = Hex("#2B5C8A");

        private static readonly string[] PrivacyOptionLabels = { "전체 공개", "친구만 공개", "비공개" };

        private enum ViewKey
        {
            None,
            Menu,
            ProfileEdit,
            ProfileEditForm,
            Payment,
            Notification,
            ReportHistory,
            TermsPolicy,
            Withdrawal
        }

        private Canvas settingsCanvas;
        private TMP_FontAsset fontAsset;
        private TMP_FontAsset fallbackFontAsset;
        private Sprite whiteSprite;
        private Sprite roundedRectSprite;

        private GameObject settingsButtonRoot;
        private GameObject modeChangeButtonRoot;
        private BookshelfExperienceManager cachedExperienceManager;
        private RoomEditModeController cachedEditModeController;
        private BookshelfApiSyncController cachedBookshelfApiSyncController;
        private GameObject panelRoot;
        private GameObject dimBackground;
        private GameObject cardRoot;
        private GameObject contentRoot;
        private TMP_Text headerTitleLabel;
        private Button headerCloseButton;

        private readonly Dictionary<ViewKey, GameObject> viewRoots = new Dictionary<ViewKey, GameObject>();

        private GameObject profileEditView;
        private GameObject profileEditFormView;
        private GameObject paymentView;
        private GameObject notificationView;
        private GameObject reportHistoryView;
        private GameObject termsPolicyView;
        private GameObject withdrawalView;

        private GameObject withdrawalConfirmPopup;

        private TMP_Text noticeToastLabel;
        private GameObject noticeToastRoot;

        private TMP_Text profilePrivacyHintLabel;
        private readonly List<Button> privacyOptionButtons = new List<Button>();
        private readonly List<Image> privacyOptionBackgrounds = new List<Image>();
        private readonly List<TMP_Text> privacyOptionLabelsList = new List<TMP_Text>();
        private int selectedPrivacyIndex = 0;

        private TMP_Text profileEditFormMessageLabel;
        private TMP_InputField profileFormNicknameInput;
        private TMP_InputField profileFormPasswordInput;
        private TMP_InputField profileFormConfirmPasswordInput;
        private Image profileSummaryAvatarImage;
        private Image profileFormAvatarImage;
        private Texture2D selectedProfileImageTexture;
        private Sprite selectedProfileImageSprite;
        private byte[] selectedProfileImageBytes;
        private string selectedProfileImageContentType;
        private Texture2D remoteProfileImageTexture;
        private Sprite remoteProfileImageSprite;
        private string loadedRemoteProfileImageUrl;
        private Coroutine remoteProfileImageLoadRoutine;
        private int remoteProfileImageLoadVersion;

        // ---- Account API wiring (logout / profile / visibility / withdrawal) ----
        // Cached BE clients (lazy-initialized when the user opens Settings) and
        // per-call in-flight flags so a slow round-trip cannot be fired twice
        // by a fast double-click. cachedProfile holds the last GetMe response
        // so the partial PATCH (nickname-only UI) can preserve introduction /
        // profileImageUrl that the form does not surface — sending them blank
        // would silently wipe BE state. suppressVisibilityRequest gates the
        // BE call inside SelectPrivacyOption so applying a server-fetched
        // value during initial load does not echo back as an update.
        private NotebookApiClient settingsApiClient;
        private bool isLogoutInFlight;
        private bool isProfileLoadInFlight;
        private bool isProfileSaveInFlight;
        private bool isSpaceVisibilityLoadInFlight;
        private bool isVisibilityUpdateInFlight;
        private bool isWithdrawalInFlight;
        private bool isNotificationUpdateInFlight;
        private bool isNotificationLoadInFlight;
        private UserProfileResponseDto cachedProfile;
        private bool suppressVisibilityRequest;
        private int confirmedPrivacyIndex;
        // Suppresses the per-toggle PATCH while LoadCurrentNotificationSettings
        // is applying the server response — otherwise SetIsOnWithoutNotify
        // would fire onValueChanged → OnNotificationToggleChanged → echo PATCH.
        private bool suppressNotificationToggleListener;

        private GameObject paymentMethodsTabContent;
        private GameObject purchaseHistoryTabContent;
        private Button paymentMethodsTabButton;
        private Button purchaseHistoryTabButton;
        private Image paymentMethodsTabIndicator;
        private Image purchaseHistoryTabIndicator;
        private GameObject paymentMethodsListRoot;
        private GameObject purchaseHistoryListRoot;
        private TMP_Text paymentMethodsMessageLabel;
        private TMP_Text purchaseHistoryMessageLabel;
        private TMP_Text paymentWalletBalanceLabel;
        private bool isWalletLoadInFlight;
        private bool isPaymentMethodsLoadInFlight;
        private bool isPurchaseHistoryLoadInFlight;

        private GameObject reportListTabContent;
        private GameObject sanctionListTabContent;
        private Button reportListTabButton;
        private Button sanctionListTabButton;
        private Image reportListTabIndicator;
        private Image sanctionListTabIndicator;
        private GameObject reportListRoot;
        private GameObject blockedUsersListRoot;
        private TMP_Text reportListMessageLabel;
        private TMP_Text blockedUsersMessageLabel;
        private bool isReportListLoadInFlight;
        private bool isBlockListLoadInFlight;
        private bool isUnblockInFlight;

        private TMP_Text termsContentLabel;
        private readonly List<Button> termsTabButtons = new List<Button>();
        private readonly List<Image> termsTabIndicators = new List<Image>();
        private int selectedTermsIndex = 0;

        private Toggle masterNotificationToggle;
        private readonly List<Toggle> dependentNotificationToggles = new List<Toggle>();
        private readonly List<TMP_Text> dependentNotificationLabels = new List<TMP_Text>();

        private bool isInitialized;
        private ViewKey currentView = ViewKey.None;
        private ViewKey withdrawalReturnView = ViewKey.ProfileEdit;

        private readonly List<Button> leftMenuButtons = new List<Button>();
        private readonly List<ViewKey> leftMenuButtonKeys = new List<ViewKey>();
        private readonly List<Image> leftMenuButtonBackgrounds = new List<Image>();
        private readonly List<TMP_Text> leftMenuButtonLabels = new List<TMP_Text>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterAutoBootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != SceneKeys.Main)
            {
                return;
            }

            EnsureControllerExists(scene);
        }

        private static void EnsureControllerExists(Scene scene)
        {
            if (UnityEngine.Object.FindFirstObjectByType<SettingsScreenController>() != null)
            {
                return;
            }

            var hostObject = new GameObject("SettingsScreenController");
            if (scene.IsValid())
            {
                SceneManager.MoveGameObjectToScene(hostObject, scene);
            }

            hostObject.AddComponent<SettingsScreenController>();
        }

        private void Awake()
        {
            InitializeIfNeeded();
        }

        private void OnDestroy()
        {
            CancelRemoteProfileImageLoad();
            ReleaseSelectedProfileImagePreview();
            ReleaseRemoteProfileImagePreview();
        }

        public void InitializeIfNeeded()
        {
            if (isInitialized)
            {
                return;
            }

            isInitialized = true;
            whiteSprite = CreateWhiteSprite();
            roundedRectSprite = CreateRoundedRectSprite(64, 14);
            fontAsset = ResolveFontAsset();
            WarmupGlyphs(fontAsset);

            BuildCanvas();
            BuildModeChangeButton();
            BuildSettingsButton();
            BuildSettingsPanel();
            ApplyFontToCanvas();
            EnsureEventSystem();

            SetPanelVisible(false);
        }

        public void OpenSettings()
        {
            if (IsViewingOtherUserSpace())
            {
                return;
            }

            InitializeIfNeeded();
            SetPanelVisible(true);
            ShowProfileEdit();
            // Pull fresh profile from BE so the form reflects the current
            // server-side nickname/introduction even if the user changed it
            // from a different device or session.
            LoadCurrentProfile();
            LoadCurrentSpaceVisibility();
            LoadCurrentNotificationSettings();
        }

        public void CloseSettings()
        {
            HideAllNotices();
            DiscardSelectedProfileImageEditSession();
            SetPanelVisible(false);
            currentView = ViewKey.None;
        }

        public void ShowMenu()
        {
            ShowProfileEdit();
        }

        public void ShowProfileEdit()
        {
            DiscardSelectedProfileImageEditSession();
            ApplyProfileAvatarPreview();
            ShowView(ViewKey.ProfileEdit, "계정");
        }

        public void ShowPayment()
        {
            ShowView(ViewKey.Payment, "결제");
            ShowPaymentMethodsTab();
        }

        public void ShowNotification()
        {
            ShowView(ViewKey.Notification, "알림 설정");
            RefreshNotificationDependentEnabled();
        }

        public void ShowReportHistory()
        {
            ShowView(ViewKey.ReportHistory, "신고 / 제재 내역");
            ShowReportListTab();
        }

        public void ShowTermsPolicy()
        {
            ShowView(ViewKey.TermsPolicy, "약관 / 정책");
            selectedTermsIndex = 0;
            SelectTermsTab(selectedTermsIndex);
        }

        public void ShowWithdrawal()
        {
            withdrawalReturnView = currentView == ViewKey.None ? ViewKey.ProfileEdit : currentView;
            if (withdrawalReturnView == ViewKey.Withdrawal)
            {
                withdrawalReturnView = ViewKey.ProfileEdit;
            }
            ShowView(ViewKey.Withdrawal, "회원탈퇴");
            HideWithdrawalConfirmPopup();
        }

        private void ShowProfileEditForm()
        {
            ResetProfileEditForm();
            ApplyProfileAvatarPreview();
            ShowView(ViewKey.ProfileEditForm, "프로필 수정");
        }

        private void ShowView(ViewKey key, string title)
        {
            InitializeIfNeeded();

            if (!viewRoots.TryGetValue(key, out var targetRoot) || targetRoot == null)
            {
                return;
            }

            currentView = key;

            foreach (var entry in viewRoots)
            {
                if (entry.Value == null)
                {
                    continue;
                }
                entry.Value.SetActive(entry.Key == key);
            }

            if (headerTitleLabel != null)
            {
                headerTitleLabel.text = title;
            }

            HideAllNotices();
            RefreshLeftMenuHighlight();
        }

        private void SetPanelVisible(bool isVisible)
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(isVisible);
            }

            UpdateMainSceneChromeVisibility();
        }

        // Floating settings + mode-change buttons only show on the unfocused main view
        // (CurrentFocusArea == QuarterView, i.e. CameraMode.Overview). They stay hidden
        // while: the settings panel is open, onboarding Step 3 is in progress, the
        // camera is in any focus mode (bookshelf / guestbook / notebook), the player
        // is in room edit mode, or the room is another user's read-only visit preview.
        private void UpdateMainSceneChromeVisibility()
        {
            var panelOpen = panelRoot != null && panelRoot.activeSelf;
            var experienceManager = ResolveExperienceManager();
            var inMainScene = experienceManager != null &&
                              experienceManager.CurrentFocusArea == ExperienceFocusArea.QuarterView;
            var step3Active = experienceManager != null && experienceManager.IsOnboardingStep3Active;
            var editModeActive = ResolveEditModeController() != null && ResolveEditModeController().IsEditModeActive;
            var shouldShow = !panelOpen && inMainScene && !step3Active && !editModeActive && !IsViewingOtherUserSpace();

            if (settingsButtonRoot != null && settingsButtonRoot.activeSelf != shouldShow)
            {
                settingsButtonRoot.SetActive(shouldShow);
            }

            if (modeChangeButtonRoot != null && modeChangeButtonRoot.activeSelf != shouldShow)
            {
                modeChangeButtonRoot.SetActive(shouldShow);
            }
        }

        private BookshelfExperienceManager ResolveExperienceManager()
        {
            if (cachedExperienceManager != null)
            {
                return cachedExperienceManager;
            }

            cachedExperienceManager = UnityEngine.Object.FindFirstObjectByType<BookshelfExperienceManager>(FindObjectsInactive.Include);
            return cachedExperienceManager;
        }

        private RoomEditModeController ResolveEditModeController()
        {
            if (cachedEditModeController != null)
            {
                return cachedEditModeController;
            }

            cachedEditModeController = UnityEngine.Object.FindFirstObjectByType<RoomEditModeController>(FindObjectsInactive.Include);
            return cachedEditModeController;
        }

        private BookshelfApiSyncController ResolveBookshelfApiSyncController()
        {
            if (cachedBookshelfApiSyncController != null)
            {
                return cachedBookshelfApiSyncController;
            }

            cachedBookshelfApiSyncController = UnityEngine.Object.FindFirstObjectByType<BookshelfApiSyncController>(FindObjectsInactive.Include);
            return cachedBookshelfApiSyncController;
        }

        private bool IsViewingOtherUserSpace()
        {
            var apiSync = ResolveBookshelfApiSyncController();
            return apiSync != null && apiSync.IsViewingOtherUserSpace;
        }

        private void Update()
        {
            if (!isInitialized)
            {
                return;
            }

            UpdateMainSceneChromeVisibility();
        }

        private void HideAllNotices()
        {
            HideNoticeToast();
            HideWithdrawalConfirmPopup();
        }

        // ---------------------------------------------------------------------
        // Canvas + 설정 버튼
        // ---------------------------------------------------------------------

        private void BuildCanvas()
        {
            var existingCanvasObject = FindExistingCanvasObject();
            if (existingCanvasObject != null)
            {
                Destroy(existingCanvasObject);
            }

            var canvasObject = new GameObject(CanvasName, typeof(RectTransform));
            canvasObject.layer = UiLayer;
            SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene);

            settingsCanvas = canvasObject.AddComponent<Canvas>();
            settingsCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            settingsCanvas.overrideSorting = true;
            settingsCanvas.sortingOrder = CanvasSortingOrder;
            settingsCanvas.pixelPerfect = false;
            settingsCanvas.targetDisplay = 0;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var canvasRect = (RectTransform)canvasObject.transform;
            StretchToParent(canvasRect);
        }

        private void BuildSettingsButton()
        {
            if (settingsCanvas == null)
            {
                return;
            }

            settingsButtonRoot = CreateRectObject("SettingsButtonRoot", settingsCanvas.transform);
            var rootRect = (RectTransform)settingsButtonRoot.transform;
            rootRect.anchorMin = new Vector2(1f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(1f, 1f);
            rootRect.sizeDelta = new Vector2(SettingsButtonWidth, SettingsButtonHeight);
            // Stacked below the mode-change button (which sits at the top corner).
            var stackedY = -(SettingsButtonMargin + SettingsButtonHeight + SettingsButtonStackGap);
            rootRect.anchoredPosition = new Vector2(-SettingsButtonMargin, stackedY);

            var button = CreateButton(
                settingsButtonRoot.transform,
                "SettingsButton",
                "설정",
                CardColor,
                TextColor,
                Vector2.zero,
                new Vector2(SettingsButtonWidth, SettingsButtonHeight),
                true,
                BorderColor);
            var buttonRect = (RectTransform)button.transform;
            buttonRect.anchorMin = new Vector2(0f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;
            buttonRect.anchoredPosition = Vector2.zero;
            button.onClick.AddListener(OpenSettings);

            var shadow = button.gameObject.AddComponent<Shadow>();
            shadow.effectColor = WithAlpha(TextColor, 0.18f);
            shadow.effectDistance = new Vector2(0f, -3f);
        }

        private void BuildModeChangeButton()
        {
            if (settingsCanvas == null)
            {
                return;
            }

            modeChangeButtonRoot = CreateRectObject("ModeChangeButtonRoot", settingsCanvas.transform);
            var rootRect = (RectTransform)modeChangeButtonRoot.transform;
            rootRect.anchorMin = new Vector2(1f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(1f, 1f);
            rootRect.sizeDelta = new Vector2(SettingsButtonWidth, SettingsButtonHeight);
            rootRect.anchoredPosition = new Vector2(-SettingsButtonMargin, -SettingsButtonMargin);

            var button = CreateButton(
                modeChangeButtonRoot.transform,
                "ModeChangeButton",
                "모드 변경",
                PrimaryColor,
                Color.white,
                Vector2.zero,
                new Vector2(SettingsButtonWidth, SettingsButtonHeight),
                true,
                BorderColor);
            var buttonRect = (RectTransform)button.transform;
            buttonRect.anchorMin = new Vector2(0f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;
            buttonRect.anchoredPosition = Vector2.zero;
            button.onClick.AddListener(OnModeChangeClicked);

            var shadow = button.gameObject.AddComponent<Shadow>();
            shadow.effectColor = WithAlpha(TextColor, 0.18f);
            shadow.effectDistance = new Vector2(0f, -3f);
        }

        private void OnModeChangeClicked()
        {
            if (IsViewingOtherUserSpace())
            {
                return;
            }

            var manager = UnityEngine.Object.FindFirstObjectByType<BookshelfExperienceManager>();
            if (manager == null)
            {
                return;
            }
            manager.ToggleMainViewMode();
        }

        private void BuildSettingsPanel()
        {
            panelRoot = CreateRectObject("SettingsPanel", settingsCanvas.transform);
            var rootRect = (RectTransform)panelRoot.transform;
            StretchToParent(rootRect);

            dimBackground = CreateImage(panelRoot.transform, "DimBackground", DimColor).gameObject;
            StretchToParent((RectTransform)dimBackground.transform);
            var dimButton = dimBackground.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(CloseSettings);

            cardRoot = CreateRectObject("Card", panelRoot.transform);
            var cardImage = cardRoot.AddComponent<Image>();
            cardImage.sprite = roundedRectSprite;
            cardImage.type = Image.Type.Sliced;
            cardImage.color = CardColor;
            cardImage.raycastTarget = true;
            var cardRect = (RectTransform)cardRoot.transform;
            cardRect.anchorMin = new Vector2(0f, 0f);
            cardRect.anchorMax = new Vector2(1f, 1f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.offsetMin = new Vector2(60f, 100f);
            cardRect.offsetMax = new Vector2(-60f, -100f);

            var cardShadow = cardRoot.AddComponent<Shadow>();
            cardShadow.effectColor = WithAlpha(TextColor, 0.2f);
            cardShadow.effectDistance = new Vector2(0f, -10f);

            BuildHeader(cardRoot.transform);
            BuildHorizontalSplit(cardRoot.transform);

            BuildAllViews();
            BuildWithdrawalConfirmPopup(panelRoot.transform);
            BuildNoticeToast();

            RefreshLeftMenuHighlight();
        }

        private void BuildHorizontalSplit(Transform parent)
        {
            var split = CreateRectObject("Split", parent);
            var splitRect = (RectTransform)split.transform;
            splitRect.anchorMin = new Vector2(0f, 0f);
            splitRect.anchorMax = new Vector2(1f, 1f);
            splitRect.pivot = new Vector2(0.5f, 0.5f);
            splitRect.offsetMin = new Vector2(24f, 24f);
            splitRect.offsetMax = new Vector2(-24f, -HeaderHeight - 12f);

            BuildLeftMenuPane(split.transform);
            BuildVerticalDivider(split.transform);
            contentRoot = BuildRightContentArea(split.transform);
        }

        private void BuildLeftMenuPane(Transform parent)
        {
            var pane = CreateRectObject("LeftMenuPane", parent);
            var paneRect = (RectTransform)pane.transform;
            paneRect.anchorMin = new Vector2(0f, 0f);
            paneRect.anchorMax = new Vector2(LeftPaneRatio, 1f);
            paneRect.pivot = new Vector2(0f, 0.5f);
            paneRect.offsetMin = Vector2.zero;
            paneRect.offsetMax = Vector2.zero;

            var paneLayout = pane.AddComponent<VerticalLayoutGroup>();
            paneLayout.padding = new RectOffset(12, 16, 8, 8);
            paneLayout.spacing = 6f;
            paneLayout.childAlignment = TextAnchor.UpperCenter;
            paneLayout.childControlHeight = false;
            paneLayout.childControlWidth = true;
            paneLayout.childForceExpandHeight = false;
            paneLayout.childForceExpandWidth = true;

            leftMenuButtons.Clear();
            leftMenuButtonKeys.Clear();
            leftMenuButtonBackgrounds.Clear();
            leftMenuButtonLabels.Clear();

            AddLeftMenuItem(pane.transform, "계정", ViewKey.ProfileEdit, ShowProfileEdit);
            AddLeftMenuItem(pane.transform, "결제", ViewKey.Payment, ShowPayment);
            AddLeftMenuItem(pane.transform, "알림", ViewKey.Notification, ShowNotification);
            AddLeftMenuItem(pane.transform, "피드 설정", ViewKey.None, () => ShowNoticeToast("피드 설정은 현재 MVP 범위에 포함되지 않습니다."));
            AddLeftMenuItem(pane.transform, "약관 / 정책", ViewKey.TermsPolicy, ShowTermsPolicy);
            AddLeftMenuItem(pane.transform, "이용관련", ViewKey.ReportHistory, ShowReportHistory);
        }

        private void AddLeftMenuItem(Transform parent, string labelText, ViewKey highlightKey, Action onClick)
        {
            var item = CreateRectObject($"LeftMenuItem_{labelText}", parent);
            ConfigureLayoutHeight(item, LeftMenuItemHeight);

            var bg = item.AddComponent<Image>();
            bg.sprite = roundedRectSprite;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(1f, 1f, 1f, 0f);
            bg.raycastTarget = true;

            var label = CreateText(
                item.transform,
                "Label",
                labelText,
                19f,
                FontStyles.Normal,
                TextColor,
                TextAlignmentOptions.MidlineLeft,
                Vector2.zero,
                Vector2.zero,
                0f,
                new Vector2(0.5f, 0.5f));
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(20f, 0f);
            labelRect.offsetMax = new Vector2(-12f, 0f);

            var button = item.AddComponent<Button>();
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = WithAlpha(PrimaryColor, 0.08f);
            colors.pressedColor = WithAlpha(PrimaryColor, 0.16f);
            colors.selectedColor = new Color(1f, 1f, 1f, 0f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick?.Invoke());

            leftMenuButtons.Add(button);
            leftMenuButtonKeys.Add(highlightKey);
            leftMenuButtonBackgrounds.Add(bg);
            leftMenuButtonLabels.Add(label);
        }

        private void BuildVerticalDivider(Transform parent)
        {
            var divider = CreateImage(parent, "VerticalDivider", WithAlpha(BorderColor, 0.7f));
            divider.raycastTarget = false;
            var rect = divider.rectTransform;
            rect.anchorMin = new Vector2(LeftPaneRatio, 0.04f);
            rect.anchorMax = new Vector2(LeftPaneRatio, 0.96f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(8f, 0f);
        }

        private GameObject BuildRightContentArea(Transform parent)
        {
            var area = CreateRectObject("RightContentScroll", parent);
            var areaRect = (RectTransform)area.transform;
            areaRect.anchorMin = new Vector2(LeftPaneRatio, 0f);
            areaRect.anchorMax = new Vector2(1f, 1f);
            areaRect.pivot = new Vector2(1f, 0.5f);
            areaRect.offsetMin = new Vector2(20f, 0f);
            areaRect.offsetMax = Vector2.zero;

            var scroll = area.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            var viewport = CreateRectObject("Viewport", area.transform);
            var viewportRect = (RectTransform)viewport.transform;
            StretchToParent(viewportRect);

            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
            viewportImage.raycastTarget = true;
            viewport.AddComponent<RectMask2D>();

            var content = CreateRectObject("Content", viewport.transform);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;
            contentRect.anchoredPosition = Vector2.zero;

            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(4, 12, 4, 16);
            contentLayout.spacing = 0f;
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlHeight = false;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childForceExpandWidth = true;

            var contentFitter = content.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            return content;
        }

        private static ViewKey GetMenuHighlightKey(ViewKey view)
        {
            switch (view)
            {
                case ViewKey.ProfileEditForm:
                case ViewKey.Withdrawal:
                    return ViewKey.ProfileEdit;
                default:
                    return view;
            }
        }

        private void RefreshLeftMenuHighlight()
        {
            var highlight = GetMenuHighlightKey(currentView);

            for (var i = 0; i < leftMenuButtons.Count; i++)
            {
                var key = leftMenuButtonKeys[i];
                var isSelected = key != ViewKey.None && key == highlight;

                if (i < leftMenuButtonBackgrounds.Count && leftMenuButtonBackgrounds[i] != null)
                {
                    leftMenuButtonBackgrounds[i].color = isSelected
                        ? WithAlpha(PrimaryColor, 0.18f)
                        : new Color(1f, 1f, 1f, 0f);
                }

                if (i < leftMenuButtonLabels.Count && leftMenuButtonLabels[i] != null)
                {
                    leftMenuButtonLabels[i].color = isSelected ? PrimaryColor : TextColor;
                    leftMenuButtonLabels[i].fontStyle = isSelected ? FontStyles.Bold : FontStyles.Normal;
                }
            }
        }

        private void BuildHeader(Transform parent)
        {
            var header = CreateRectObject("Header", parent);
            var headerRect = (RectTransform)header.transform;
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.sizeDelta = new Vector2(0f, HeaderHeight);
            headerRect.anchoredPosition = new Vector2(0f, 0f);

            var divider = CreateImage(header.transform, "Divider", WithAlpha(BorderColor, 0.7f));
            divider.raycastTarget = false;
            var dividerRect = divider.rectTransform;
            dividerRect.anchorMin = new Vector2(0f, 0f);
            dividerRect.anchorMax = new Vector2(1f, 0f);
            dividerRect.pivot = new Vector2(0.5f, 0f);
            dividerRect.sizeDelta = new Vector2(-48f, 1f);
            dividerRect.anchoredPosition = new Vector2(0f, 0f);

            headerTitleLabel = CreateText(
                header.transform,
                "Title",
                "설정",
                30f,
                FontStyles.Bold,
                TextColor,
                TextAlignmentOptions.Center,
                Vector2.zero,
                new Vector2(560f, HeaderHeight),
                0f,
                new Vector2(0.5f, 0.5f));
            var titleRect = headerTitleLabel.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);

            headerCloseButton = CreateButton(
                header.transform,
                "CloseButton",
                "닫기",
                SecondaryColor,
                TextColor,
                Vector2.zero,
                new Vector2(120f, 56f),
                true,
                BorderColor);
            var closeRect = (RectTransform)headerCloseButton.transform;
            closeRect.anchorMin = new Vector2(1f, 0.5f);
            closeRect.anchorMax = new Vector2(1f, 0.5f);
            closeRect.pivot = new Vector2(1f, 0.5f);
            closeRect.anchoredPosition = new Vector2(-24f, 0f);
            headerCloseButton.onClick.AddListener(CloseSettings);
        }

        // ---------------------------------------------------------------------
        // 모든 View 생성
        // ---------------------------------------------------------------------

        private void BuildAllViews()
        {
            profileEditView = BuildProfileEditView(contentRoot.transform);
            profileEditFormView = BuildProfileEditFormView(contentRoot.transform);
            paymentView = BuildPaymentView(contentRoot.transform);
            notificationView = BuildNotificationView(contentRoot.transform);
            reportHistoryView = BuildReportHistoryView(contentRoot.transform);
            termsPolicyView = BuildTermsPolicyView(contentRoot.transform);
            withdrawalView = BuildWithdrawalView(contentRoot.transform);

            viewRoots[ViewKey.ProfileEdit] = profileEditView;
            viewRoots[ViewKey.ProfileEditForm] = profileEditFormView;
            viewRoots[ViewKey.Payment] = paymentView;
            viewRoots[ViewKey.Notification] = notificationView;
            viewRoots[ViewKey.ReportHistory] = reportHistoryView;
            viewRoots[ViewKey.TermsPolicy] = termsPolicyView;
            viewRoots[ViewKey.Withdrawal] = withdrawalView;

            foreach (var entry in viewRoots)
            {
                if (entry.Value != null)
                {
                    entry.Value.SetActive(false);
                }
            }
        }

        // ---------------------------------------------------------------------
        // ProfileEditView
        // ---------------------------------------------------------------------

        private GameObject BuildProfileEditView(Transform parent)
        {
            var view = CreateLayoutView("ProfileEditView", parent, 18f);

            var profileRow = CreateRectObject("ProfileRow", view.transform);
            ConfigureLayoutHeight(profileRow, 200f);
            var profileRowRect = (RectTransform)profileRow.transform;
            profileRowRect.sizeDelta = new Vector2(0f, 200f);

            var avatar = CreateImage(profileRow.transform, "Avatar", AccentTint);
            avatar.sprite = roundedRectSprite;
            avatar.type = Image.Type.Sliced;
            avatar.raycastTarget = false;
            profileSummaryAvatarImage = avatar;
            var avatarRect = avatar.rectTransform;
            avatarRect.anchorMin = new Vector2(0f, 0.5f);
            avatarRect.anchorMax = new Vector2(0f, 0.5f);
            avatarRect.pivot = new Vector2(0f, 0.5f);
            avatarRect.sizeDelta = new Vector2(160f, 160f);
            avatarRect.anchoredPosition = new Vector2(8f, 0f);

            CreateText(
                profileRow.transform,
                "Nickname",
                "북룸유저",
                26f,
                FontStyles.Bold,
                TextColor,
                TextAlignmentOptions.Left,
                new Vector2(200f, 30f),
                new Vector2(420f, 36f),
                0f,
                new Vector2(0f, 0.5f));

            CreateText(
                profileRow.transform,
                "Email",
                "demo@bookroom.app",
                18f,
                FontStyles.Normal,
                MutedTextColor,
                TextAlignmentOptions.Left,
                new Vector2(200f, -10f),
                new Vector2(420f, 28f),
                0f,
                new Vector2(0f, 0.5f));

            var editButton = CreateButton(profileRow.transform, "EditButton", "수정", PrimaryColor, Color.white, Vector2.zero, new Vector2(120f, 52f), false, BorderColor);
            var editRect = (RectTransform)editButton.transform;
            editRect.anchorMin = new Vector2(1f, 0.5f);
            editRect.anchorMax = new Vector2(1f, 0.5f);
            editRect.pivot = new Vector2(1f, 0.5f);
            editRect.anchoredPosition = new Vector2(-8f, 0f);
            editButton.onClick.AddListener(ShowProfileEditForm);

            CreateSectionHeader(view.transform, "공간 공개 범위");

            var privacyRow = CreateRectObject("PrivacyRow", view.transform);
            ConfigureLayoutHeight(privacyRow, 70f);
            var privacyHorizontal = privacyRow.AddComponent<HorizontalLayoutGroup>();
            privacyHorizontal.spacing = 12f;
            privacyHorizontal.childAlignment = TextAnchor.MiddleCenter;
            privacyHorizontal.childControlHeight = true;
            privacyHorizontal.childControlWidth = true;
            privacyHorizontal.childForceExpandHeight = true;
            privacyHorizontal.childForceExpandWidth = true;

            privacyOptionButtons.Clear();
            privacyOptionBackgrounds.Clear();
            privacyOptionLabelsList.Clear();

            for (var i = 0; i < PrivacyOptionLabels.Length; i++)
            {
                var captureIndex = i;
                var optionButton = CreateButton(privacyRow.transform, $"PrivacyOption_{i}", PrivacyOptionLabels[i], SecondaryColor, TextColor, Vector2.zero, Vector2.zero, true, BorderColor);
                var optionRect = (RectTransform)optionButton.transform;
                optionRect.anchorMin = new Vector2(0.5f, 0.5f);
                optionRect.anchorMax = new Vector2(0.5f, 0.5f);
                optionRect.pivot = new Vector2(0.5f, 0.5f);
                optionRect.sizeDelta = Vector2.zero;
                optionButton.onClick.AddListener(() => SelectPrivacyOption(captureIndex));

                privacyOptionButtons.Add(optionButton);
                if (optionButton.targetGraphic is Image bg)
                {
                    privacyOptionBackgrounds.Add(bg);
                }

                var optionLabel = optionButton.GetComponentInChildren<TMP_Text>(true);
                if (optionLabel != null)
                {
                    privacyOptionLabelsList.Add(optionLabel);
                }
            }

            profilePrivacyHintLabel = CreateLayoutText(view.transform, "PrivacyHint", "선택한 공개 범위는 데모 화면에서만 적용됩니다.", 14f, FontStyles.Italic, MutedTextColor, TextAlignmentOptions.Left, 22f);
            SelectPrivacyOption(selectedPrivacyIndex);

            var logoutButton = CreateButton(view.transform, "LogoutButton", "로그아웃", PrimaryColor, Color.white, Vector2.zero, Vector2.zero, false, BorderColor);
            ConfigureLayoutHeight(logoutButton.gameObject, SecondaryControlHeight);
            logoutButton.onClick.AddListener(OnLogoutButtonClicked);

            // 앱 종료. 에디터에서는 플레이 모드를 빠져나오고, 빌드에서는
            // Application.Quit()로 프로세스를 종료한다. 두 경로를 한 곳에
            // 묶어두면 데모 시연 중 디바이스에서도, 에디터에서도 같은
            // 버튼으로 정상 종료할 수 있다.
            var quitButton = CreateButton(view.transform, "QuitButton", "앱 종료", SecondaryColor, ErrorColor, Vector2.zero, Vector2.zero, true, BorderColor);
            ConfigureLayoutHeight(quitButton.gameObject, SecondaryControlHeight);
            quitButton.onClick.AddListener(QuitApplication);

            return view;
        }

        private static void QuitApplication()
        {
            Debug.Log("Quit application requested");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            // App.Application namespace와 이름 충돌 방지 — fully qualified로 명시.
            UnityEngine.Application.Quit();
#endif
        }

        private void SelectPrivacyOption(int index)
        {
            selectedPrivacyIndex = Mathf.Clamp(index, 0, PrivacyOptionLabels.Length - 1);

            for (var i = 0; i < privacyOptionBackgrounds.Count; i++)
            {
                var bg = privacyOptionBackgrounds[i];
                if (bg == null)
                {
                    continue;
                }

                bg.color = i == selectedPrivacyIndex ? WithAlpha(PrimaryColor, 0.18f) : SecondaryColor;
            }

            for (var i = 0; i < privacyOptionLabelsList.Count; i++)
            {
                var label = privacyOptionLabelsList[i];
                if (label == null)
                {
                    continue;
                }

                label.color = i == selectedPrivacyIndex ? PrimaryColor : TextColor;
                label.fontStyle = i == selectedPrivacyIndex ? FontStyles.Bold : FontStyles.Normal;
            }

            if (profilePrivacyHintLabel != null)
            {
                profilePrivacyHintLabel.text = $"선택: {PrivacyOptionLabels[selectedPrivacyIndex]}";
            }

            SubmitVisibilityChange();
        }

        private void ApplyPrivacyOptionWithoutRequest(int index)
        {
            suppressVisibilityRequest = true;
            try
            {
                SelectPrivacyOption(index);
            }
            finally
            {
                suppressVisibilityRequest = false;
            }
        }

        private void SubmitVisibilityChange()
        {
            // Skip BE while applying a server-fetched value (the initial load
            // path also calls SelectPrivacyOption to sync the highlight; we
            // don't want to echo that back as a PATCH).
            if (suppressVisibilityRequest)
            {
                return;
            }

            if (isVisibilityUpdateInFlight)
            {
                ApplyPrivacyOptionWithoutRequest(confirmedPrivacyIndex);
                ShowNoticeToast("이전 공개 범위 변경 처리가 끝난 후 다시 시도해주세요.");
                return;
            }

#if UNITY_EDITOR
            if (DevMainSceneMode.IsActive)
            {
                return;
            }
#endif

            var request = new VisibilityRequestDto
            {
                VisibilityScope = MapPrivacyIndexToScope(selectedPrivacyIndex)
            };
            var requestedPrivacyIndex = selectedPrivacyIndex;
            var rollbackPrivacyIndex = confirmedPrivacyIndex;

            var client = EnsureSettingsApiClient();
            if (client == null)
            {
                ApplyPrivacyOptionWithoutRequest(rollbackPrivacyIndex);
                ShowNoticeToast("공개 범위 변경에 실패했습니다. 잠시 후 다시 시도해주세요.");
                return;
            }

            isVisibilityUpdateInFlight = true;
            var hasRetried = false;
            void DoUpdateMySpaceVisibility(NotebookApiClient apiClient)
            {
                apiClient.UpdateMySpaceVisibility(
                    request,
                    onSuccess: response =>
                    {
                        isVisibilityUpdateInFlight = false;
                        confirmedPrivacyIndex = response != null
                            ? MapScopeToPrivacyIndex(response.VisibilityScope)
                            : requestedPrivacyIndex;
                        ApplyPrivacyOptionWithoutRequest(confirmedPrivacyIndex);
                    },
                    onError: error =>
                    {
                        if (!hasRetried && SessionRefresh.IsAuthFailure(error))
                        {
                            hasRetried = true;
                            SessionRefresh.TryRefreshAndRetry(
                                onRetry: () => DoUpdateMySpaceVisibility(apiClient),
                                onAbort: () =>
                                {
                                    isVisibilityUpdateInFlight = false;
                                    ApplyPrivacyOptionWithoutRequest(rollbackPrivacyIndex);
                                    ShowNoticeToast("세션이 만료되었습니다. 다시 로그인해주세요.");
                                });
                            return;
                        }

                        isVisibilityUpdateInFlight = false;
                        ApplyPrivacyOptionWithoutRequest(rollbackPrivacyIndex);
                        ShowNoticeToast("공개 범위 변경에 실패했습니다. 잠시 후 다시 시도해주세요.");
                        Debug.LogWarning($"[Settings] UpdateMySpaceVisibility failed: code={error?.Code} message={error?.Message}");
                    });
            }
            DoUpdateMySpaceVisibility(client);
        }

        private static string MapPrivacyIndexToScope(int index)
        {
            // Mirror App.Domain.Dashboard.VisibilityScope as serialized by
            // DashboardDtoMapper.FormatVisibility — keep the strings in sync
            // if BE ever renames the enum. Caller is the radio handler, so
            // index is bounded by PrivacyOptionLabels.Length, but we still
            // guard explicitly: a silent default-to-PUBLIC for an out-of-band
            // index would mis-apply the user's privacy preference, which is
            // worse than failing loudly.
            switch (index)
            {
                case 0: return "PUBLIC";
                case 1: return "FOLLOWERS_ONLY";
                case 2: return "PRIVATE";
                default:
                    Debug.LogWarning(
                        $"[Settings] MapPrivacyIndexToScope received out-of-range index={index} — falling back to PUBLIC.");
                    return "PUBLIC";
            }
        }

        private static int MapScopeToPrivacyIndex(string scope)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                return 0;
            }
            return scope.Trim().ToUpperInvariant() switch
            {
                "FOLLOWERS_ONLY" => 1,
                "FOLLOWERS" => 1,
                "PRIVATE" => 2,
                _ => 0,
            };
        }

        // ---------------------------------------------------------------------
        // ProfileEditFormView
        // ---------------------------------------------------------------------

        private GameObject BuildProfileEditFormView(Transform parent)
        {
            var view = CreateLayoutView("ProfileEditFormView", parent, 16f);

            var backRow = CreateRectObject("BackRow", view.transform);
            ConfigureLayoutHeight(backRow, 60f);
            var backButton = CreateButton(backRow.transform, "BackButton", "뒤로", SecondaryColor, TextColor, Vector2.zero, new Vector2(140f, 52f), true, BorderColor);
            var backRect = (RectTransform)backButton.transform;
            backRect.anchorMin = new Vector2(0f, 0.5f);
            backRect.anchorMax = new Vector2(0f, 0.5f);
            backRect.pivot = new Vector2(0f, 0.5f);
            backRect.anchoredPosition = new Vector2(8f, 0f);
            backButton.onClick.AddListener(ShowProfileEdit);

            var avatarRow = CreateRectObject("AvatarRow", view.transform);
            ConfigureLayoutHeight(avatarRow, 180f);
            var avatar = CreateImage(avatarRow.transform, "Avatar", AccentTint);
            avatar.sprite = roundedRectSprite;
            avatar.type = Image.Type.Sliced;
            avatar.raycastTarget = false;
            profileFormAvatarImage = avatar;
            var avatarRect = avatar.rectTransform;
            avatarRect.anchorMin = new Vector2(0.5f, 0.5f);
            avatarRect.anchorMax = new Vector2(0.5f, 0.5f);
            avatarRect.pivot = new Vector2(1f, 0.5f);
            avatarRect.sizeDelta = new Vector2(160f, 160f);
            avatarRect.anchoredPosition = new Vector2(-12f, 0f);

            var changePhotoButton = CreateButton(avatarRow.transform, "ChangePhoto", "사진 변경", SecondaryColor, TextColor, Vector2.zero, new Vector2(180f, 60f), true, BorderColor);
            var changeRect = (RectTransform)changePhotoButton.transform;
            changeRect.anchorMin = new Vector2(0.5f, 0.5f);
            changeRect.anchorMax = new Vector2(0.5f, 0.5f);
            changeRect.pivot = new Vector2(0f, 0.5f);
            changeRect.anchoredPosition = new Vector2(12f, 0f);
            changePhotoButton.onClick.AddListener(OnProfileImageChangeRequested);

            profileFormNicknameInput = CreateLabeledInput(view.transform, "NicknameField", "닉네임", "닉네임 입력", false);
            profileFormPasswordInput = CreateLabeledInput(view.transform, "PasswordField", "비밀번호", "비밀번호 입력", true);
            profileFormConfirmPasswordInput = CreateLabeledInput(view.transform, "ConfirmPasswordField", "비밀번호 확인", "비밀번호 다시 입력", true);

            profileEditFormMessageLabel = CreateLayoutText(view.transform, "Message", string.Empty, 16f, FontStyles.Normal, ErrorColor, TextAlignmentOptions.Left, 28f);

            var actionRow = CreateRectObject("ActionRow", view.transform);
            ConfigureLayoutHeight(actionRow, PrimaryControlHeight);
            var actionHorizontal = actionRow.AddComponent<HorizontalLayoutGroup>();
            actionHorizontal.spacing = 12f;
            actionHorizontal.childControlHeight = true;
            actionHorizontal.childControlWidth = true;
            actionHorizontal.childForceExpandWidth = true;
            actionHorizontal.childForceExpandHeight = true;

            var withdrawalButton = CreateButton(actionRow.transform, "WithdrawalButton", "회원탈퇴", SecondaryColor, ErrorColor, Vector2.zero, Vector2.zero, true, BorderColor);
            ResetButtonRectForLayout(withdrawalButton);
            withdrawalButton.onClick.AddListener(ShowWithdrawal);

            var submitButton = CreateButton(actionRow.transform, "SubmitButton", "완료", PrimaryColor, Color.white, Vector2.zero, Vector2.zero, false, BorderColor);
            ResetButtonRectForLayout(submitButton);
            submitButton.onClick.AddListener(OnProfileEditFormSubmitReviewed);

            return view;
        }

        private void OnProfileEditFormSubmit()
        {
            if (isProfileSaveInFlight)
            {
                return;
            }

            var nickname = profileFormNicknameInput != null ? profileFormNicknameInput.text.Trim() : string.Empty;
            var password = profileFormPasswordInput != null ? profileFormPasswordInput.text : string.Empty;
            var confirmPassword = profileFormConfirmPasswordInput != null ? profileFormConfirmPasswordInput.text : string.Empty;

            if (string.IsNullOrWhiteSpace(nickname))
            {
                SetMessage(profileEditFormMessageLabel, "닉네임을 입력해주세요.", ErrorColor);
                return;
            }

            // BE has no password field on the social-login user record yet, so
            // the password inputs stay client-only — we still validate the
            // confirmation match for UX, but the value is never sent.
            if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
            {
                SetMessage(profileEditFormMessageLabel, "비밀번호가 일치하지 않습니다.", ErrorColor);
                return;
            }

#if UNITY_EDITOR
            if (DevMainSceneMode.IsActive)
            {
                SetMessage(profileEditFormMessageLabel, "[DEV] 변경사항이 로컬에만 적용되었습니다.", MutedTextColor);
                ShowProfileEdit();
                return;
            }
#endif

            // Preserve introduction / profileImageUrl from the cached profile
            // (the form does not expose them). If GetMe failed earlier the
            // cache is null, so the PATCH may overwrite those fields with
            // null — acceptable because BE is only @Size-validated, not
            // @NotBlank, on those two columns.
            var request = new UpdateProfileRequestDto
            {
                Nickname = nickname,
                ProfileImageUrl = cachedProfile?.ProfileImageUrl,
                Introduction = cachedProfile?.Introduction
            };

            var client = EnsureSettingsApiClient();
            if (client == null)
            {
                SetMessage(profileEditFormMessageLabel, "프로필 저장에 실패했습니다. 다시 시도해주세요.", ErrorColor);
                return;
            }

            isProfileSaveInFlight = true;
            SetMessage(profileEditFormMessageLabel, "프로필을 저장하는 중입니다...", MutedTextColor);

            string fullUrl = App.Domain.ApiEndpointDefaults.DefaultBaseUrl.TrimEnd('/') + "/v1/users/me";
            Debug.Log(
                $"[Settings] PATCH /v1/users/me 요청 → {fullUrl} payload={{nickname='{request.Nickname}',hasIntro={request.Introduction != null},hasProfileImg={request.ProfileImageUrl != null}}}");

            client.UpdateMe(
                request,
                onSuccess: response =>
                {
                    cachedProfile = response;
                    if (HasSelectedProfileImageForUpload())
                    {
                        UploadSelectedProfileImage(
                            client,
                            onSuccess: uploadedProfile =>
                            {
                                isProfileSaveInFlight = false;
                                cachedProfile = uploadedProfile ?? cachedProfile;
                                ReleaseSelectedProfileImagePreview();
                                ClearSelectedProfileImageUploadState();
                                LoadRemoteProfileImage(cachedProfile?.ProfileImageUrl);
                                Debug.Log(
                                    $"[Settings] PATCH /v1/users/me ?묐떟 ??STATUS=2xx (onSuccess) " +
                                    $"BODY={{id={cachedProfile?.Id},nickname='{cachedProfile?.Nickname}',onboarded={cachedProfile?.Onboarded}}}");
                                SetMessage(profileEditFormMessageLabel, "?꾨줈?꾩씠 ??λ릺?덉뒿?덈떎.", SuccessColor);
                                ShowProfileEdit();
                            },
                            onError: error =>
                            {
                                isProfileSaveInFlight = false;
                                SetMessage(profileEditFormMessageLabel, "?꾨줈?꾩? ??λ릺?덉?留? ?대?吏 ?낅줈??泥섎━?먯꽌 ?ㅽ뙣?덉뒿?덈떎. ?ㅼ떆 ?쒕룄?댁＜?몄슂.", ErrorColor);
                                Debug.LogWarning(
                                    $"[Settings] Profile image upload failed: code={error?.Code} message={error?.Message} body={ApiManagerProvider.TrimForLog(error?.Body)}");
                            });
                        return;
                    }

                    isProfileSaveInFlight = false;
                    Debug.Log(
                        $"[Settings] PATCH /v1/users/me 응답 → STATUS=2xx (onSuccess) " +
                        $"BODY={{id={response?.Id},nickname='{response?.Nickname}',onboarded={response?.Onboarded}}}");
                    SetMessage(profileEditFormMessageLabel, "프로필이 저장되었습니다.", SuccessColor);
                    ShowProfileEdit();
                },
                onError: error =>
                {
                    isProfileSaveInFlight = false;
                    SetMessage(profileEditFormMessageLabel, "프로필 저장에 실패했습니다. 다시 시도해주세요.", ErrorColor);
                    Debug.LogWarning(
                        $"[Settings] PATCH /v1/users/me 실패 → STATUS={(error != null ? error.StatusCode.ToString() : "?")} " +
                        $"code={error?.Code} message={error?.Message} body={ApiManagerProvider.TrimForLog(error?.Body)}");
                });
        }

        private void ResetProfileEditForm()
        {
            DiscardSelectedProfileImageEditSession();

            if (profileFormNicknameInput != null)
            {
                profileFormNicknameInput.text = string.Empty;
            }
            if (profileFormPasswordInput != null)
            {
                profileFormPasswordInput.text = string.Empty;
            }
            if (profileFormConfirmPasswordInput != null)
            {
                profileFormConfirmPasswordInput.text = string.Empty;
            }

            SetMessage(profileEditFormMessageLabel, string.Empty, ErrorColor);
        }

        private void OnProfileEditFormSubmitReviewed()
        {
            if (isProfileSaveInFlight)
            {
                return;
            }

            var nickname = profileFormNicknameInput != null ? profileFormNicknameInput.text.Trim() : string.Empty;
            var password = profileFormPasswordInput != null ? profileFormPasswordInput.text : string.Empty;
            var confirmPassword = profileFormConfirmPasswordInput != null ? profileFormConfirmPasswordInput.text : string.Empty;

            if (string.IsNullOrWhiteSpace(nickname))
            {
                SetMessage(profileEditFormMessageLabel, "Enter a nickname.", ErrorColor);
                return;
            }

            if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
            {
                SetMessage(profileEditFormMessageLabel, "Passwords do not match.", ErrorColor);
                return;
            }

#if UNITY_EDITOR
            if (DevMainSceneMode.IsActive)
            {
                SetMessage(profileEditFormMessageLabel, "[DEV] Changes were applied locally only.", MutedTextColor);
                ShowProfileEdit();
                return;
            }
#endif

            var request = new UpdateProfileRequestDto
            {
                Nickname = nickname,
                ProfileImageUrl = cachedProfile?.ProfileImageUrl,
                Introduction = cachedProfile?.Introduction
            };

            var client = EnsureSettingsApiClient();
            if (client == null)
            {
                SetMessage(profileEditFormMessageLabel, "Failed to save profile. Please try again.", ErrorColor);
                return;
            }

            isProfileSaveInFlight = true;
            SetMessage(profileEditFormMessageLabel, "Saving profile...", MutedTextColor);

            var hasRetried = false;
            void DoUpdateProfile(NotebookApiClient apiClient)
            {
                apiClient.UpdateMe(
                    request,
                    onSuccess: response =>
                    {
                        cachedProfile = response;
                        if (HasSelectedProfileImageForUpload())
                        {
                            UploadSelectedProfileImage(
                                apiClient,
                                onSuccess: uploadedProfile =>
                                {
                                    isProfileSaveInFlight = false;
                                    cachedProfile = uploadedProfile ?? cachedProfile;
                                    DiscardSelectedProfileImageEditSession();
                                    LoadRemoteProfileImage(cachedProfile?.ProfileImageUrl);
                                    SetMessage(profileEditFormMessageLabel, "Profile saved.", SuccessColor);
                                    ShowProfileEdit();
                                },
                                onError: error =>
                                {
                                    isProfileSaveInFlight = false;
                                    SetMessage(profileEditFormMessageLabel, "Profile saved, but profile image upload failed. Please try again.", ErrorColor);
                                    Debug.LogWarning(
                                        $"[Settings] Profile image upload failed: code={error?.Code} message={error?.Message} body={ApiManagerProvider.TrimForLog(error?.Body)}");
                                });
                            return;
                        }

                        isProfileSaveInFlight = false;
                        SetMessage(profileEditFormMessageLabel, "Profile saved.", SuccessColor);
                        ShowProfileEdit();
                    },
                    onError: error =>
                    {
                        if (!hasRetried && SessionRefresh.IsAuthFailure(error))
                        {
                            hasRetried = true;
                            SessionRefresh.TryRefreshAndRetry(
                                onRetry: () => DoUpdateProfile(apiClient),
                                onAbort: () =>
                                {
                                    isProfileSaveInFlight = false;
                                    SetMessage(profileEditFormMessageLabel, "Session expired. Please sign in again.", ErrorColor);
                                });
                            return;
                        }

                        isProfileSaveInFlight = false;
                        SetMessage(profileEditFormMessageLabel, "Failed to save profile. Please try again.", ErrorColor);
                        Debug.LogWarning(
                            $"[Settings] PATCH /v1/users/me failed STATUS={(error != null ? error.StatusCode.ToString() : "?")} code={error?.Code} message={error?.Message} body={ApiManagerProvider.TrimForLog(error?.Body)}");
                    });
            }
            DoUpdateProfile(client);
        }

        private void OnProfileImageChangeRequested()
        {
            if (!TryOpenImageFilePanelReviewed(out var selectedPath, out var pickerError))
            {
                if (!string.IsNullOrWhiteSpace(pickerError))
                {
                    ShowNoticeToast(pickerError);
                }
                return;
            }

            if (!TryLoadSelectedProfileImageReviewed(selectedPath, out var loadError))
            {
                if (!string.IsNullOrWhiteSpace(loadError))
                {
                    ShowNoticeToast(loadError);
                }
            }
        }

        private bool TryOpenImageFilePanelReviewed(out string selectedPath, out string errorMessage)
        {
            selectedPath = string.Empty;
            errorMessage = null;

            if (TryOpenWithStandaloneFileBrowser(out selectedPath, out var handledByPlugin))
            {
                return true;
            }

            if (handledByPlugin)
            {
                return false;
            }

#if UNITY_EDITOR
            selectedPath = UnityEditor.EditorUtility.OpenFilePanelWithFilters(
                "Select profile image",
                string.Empty,
                new[] { "Image Files", "png,jpg,jpeg" });
            return !string.IsNullOrWhiteSpace(selectedPath);
#else
            errorMessage = "Profile image selection is not supported in this build yet.";
            return false;
#endif
        }

        private bool TryLoadSelectedProfileImageReviewed(string imagePath, out string errorMessage)
        {
            errorMessage = null;
            if (!ProfileImageSelectionUtility.TryLoadPreviewTexture(
                    imagePath,
                    "SettingsProfilePreview",
                    out var imageBytes,
                    out var texture,
                    out var contentType,
                    out errorMessage))
            {
                return false;
            }

            DiscardSelectedProfileImageEditSession();
            selectedProfileImageBytes = imageBytes;
            selectedProfileImageContentType = contentType;
            selectedProfileImageTexture = texture;
            selectedProfileImageSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            selectedProfileImageSprite.name = "SettingsProfilePreviewSprite";
            selectedProfileImageSprite.hideFlags = HideFlags.HideAndDontSave;
            ApplyProfileAvatarPreview(profileFormAvatarImage);
            return true;
        }

        private void DiscardSelectedProfileImageEditSession()
        {
            ReleaseSelectedProfileImagePreview();
            ClearSelectedProfileImageUploadState();
        }

        private void OnProfileImageChangeButtonClicked()
        {
            if (!TryOpenImageFilePanel(out var selectedPath))
            {
                return;
            }

            if (!TryLoadSelectedProfileImage(selectedPath))
            {
                ShowNoticeToast("프로필 이미지를 불러오지 못했습니다.");
            }
        }

        private bool HasSelectedProfileImageForUpload()
        {
            return selectedProfileImageBytes != null &&
                   selectedProfileImageBytes.Length > 0 &&
                   !string.IsNullOrWhiteSpace(selectedProfileImageContentType);
        }

        private void UploadSelectedProfileImage(NotebookApiClient client, Action<UserProfileResponseDto> onSuccess, Action<ApiErrorResponseDto> onError)
        {
            if (client == null)
            {
                onError?.Invoke(new ApiErrorResponseDto
                {
                    Code = "PROFILE_IMAGE_CLIENT_UNAVAILABLE",
                    Message = "Profile image upload client is unavailable."
                });
                return;
            }

            if (!HasSelectedProfileImageForUpload())
            {
                onSuccess?.Invoke(null);
                return;
            }

            var imageBytes = selectedProfileImageBytes;
            var contentType = selectedProfileImageContentType;
            client.CreateMyProfileImageUploadUrl(
                new ProfileImageUploadUrlRequestDto
                {
                    ContentType = contentType,
                    ByteLength = imageBytes.LongLength
                },
                ticket =>
                {
                    if (ticket == null || string.IsNullOrWhiteSpace(ticket.UploadUrl) || string.IsNullOrWhiteSpace(ticket.Key))
                    {
                        onError?.Invoke(new ApiErrorResponseDto
                        {
                            Code = "PROFILE_IMAGE_UPLOAD_URL_EMPTY",
                            Message = "Profile image upload URL was not returned."
                        });
                        return;
                    }

                    client.UploadRawObject(
                        ticket.UploadUrl,
                        imageBytes,
                        contentType,
                        () => client.ConfirmMyProfileImage(
                            new ProfileImageConfirmRequestDto { Key = ticket.Key },
                            response =>
                            {
                                ClearSelectedProfileImageUploadState();
                                onSuccess?.Invoke(response);
                            },
                            error => onError?.Invoke(error)),
                        error => onError?.Invoke(error));
                },
                error => onError?.Invoke(error));
        }

        private bool TryOpenImageFilePanel(out string selectedPath)
        {
            selectedPath = string.Empty;

            if (TryOpenWithStandaloneFileBrowser(out selectedPath, out var handledByPlugin))
            {
                return true;
            }

            if (handledByPlugin)
            {
                return false;
            }

#if UNITY_EDITOR
            selectedPath = UnityEditor.EditorUtility.OpenFilePanelWithFilters(
                "프로필 이미지 선택",
                string.Empty,
                new[] { "Image Files", "png,jpg,jpeg" });

            return !string.IsNullOrWhiteSpace(selectedPath);
#else
            Debug.LogWarning("Profile image selection is unavailable in this environment.");
            return false;
#endif
        }

        private bool TryOpenWithStandaloneFileBrowser(out string selectedPath, out bool handled)
        {
            selectedPath = string.Empty;
            handled = false;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type fileBrowserType;
                try
                {
                    fileBrowserType = assembly.GetType("SFB.StandaloneFileBrowser");
                }
                catch
                {
                    fileBrowserType = null;
                }

                if (fileBrowserType == null)
                {
                    continue;
                }

                var extensionFilterType = assembly.GetType("SFB.ExtensionFilter");
                if (extensionFilterType == null)
                {
                    continue;
                }

                handled = true;

                try
                {
                    var filter = Activator.CreateInstance(extensionFilterType, "Image Files", new[] { "png", "jpg", "jpeg" });
                    var openFilePanel = fileBrowserType.GetMethod(
                        "OpenFilePanel",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(string), typeof(string), extensionFilterType.MakeArrayType(), typeof(bool) },
                        null);

                    if (openFilePanel == null)
                    {
                        continue;
                    }

                    var filters = Array.CreateInstance(extensionFilterType, 1);
                    filters.SetValue(filter, 0);
                    var result = openFilePanel.Invoke(null, new object[] { "프로필 이미지 선택", string.Empty, filters, false });
                    if (result is string[] paths && paths.Length > 0 && !string.IsNullOrWhiteSpace(paths[0]))
                    {
                        selectedPath = paths[0];
                        return true;
                    }

                    return false;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private bool TryLoadSelectedProfileImage(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return false;
            }

            var extension = Path.GetExtension(imagePath);
            if (!string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                var imageBytes = File.ReadAllBytes(imagePath);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.name = "SettingsProfilePreview";
                texture.hideFlags = HideFlags.HideAndDontSave;

                if (!texture.LoadImage(imageBytes))
                {
                    Destroy(texture);
                    return false;
                }

                ReleaseSelectedProfileImagePreview();
                selectedProfileImageBytes = imageBytes;
                selectedProfileImageContentType = GetImageContentType(extension);
                selectedProfileImageTexture = texture;
                selectedProfileImageSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                selectedProfileImageSprite.name = "SettingsProfilePreviewSprite";
                selectedProfileImageSprite.hideFlags = HideFlags.HideAndDontSave;
                ApplyProfileAvatarPreview();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Settings] Failed to read selected profile image: {exception.Message}");
                return false;
            }
        }

        private void ApplyProfileAvatarPreview()
        {
            ApplyProfileAvatarPreview(profileSummaryAvatarImage);
            ApplyProfileAvatarPreview(profileFormAvatarImage);
        }

        private void ApplyProfileAvatarPreview(Image target)
        {
            if (target == null)
            {
                return;
            }

            if (selectedProfileImageSprite != null)
            {
                target.sprite = selectedProfileImageSprite;
                target.color = Color.white;
                target.type = Image.Type.Simple;
                target.preserveAspect = true;
                return;
            }

            if (remoteProfileImageSprite != null)
            {
                target.sprite = remoteProfileImageSprite;
                target.color = Color.white;
                target.type = Image.Type.Simple;
                target.preserveAspect = true;
                return;
            }

            target.sprite = roundedRectSprite;
            target.color = AccentTint;
            target.type = Image.Type.Sliced;
            target.preserveAspect = false;
        }

        private void ReleaseSelectedProfileImagePreview()
        {
            if (selectedProfileImageSprite != null)
            {
                UnityObjectSafeDestroy.Destroy(selectedProfileImageSprite);
                selectedProfileImageSprite = null;
            }

            if (selectedProfileImageTexture != null)
            {
                UnityObjectSafeDestroy.Destroy(selectedProfileImageTexture);
                selectedProfileImageTexture = null;
            }
        }

        private void LoadRemoteProfileImage(string imageUrl)
        {
            if (selectedProfileImageSprite != null)
            {
                ApplyProfileAvatarPreview();
                return;
            }

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                CancelRemoteProfileImageLoad();
                ReleaseRemoteProfileImagePreview();
                loadedRemoteProfileImageUrl = null;
                ApplyProfileAvatarPreview();
                return;
            }

            var normalizedUrl = imageUrl.Trim();
            if (remoteProfileImageSprite != null &&
                string.Equals(loadedRemoteProfileImageUrl, normalizedUrl, StringComparison.Ordinal))
            {
                ApplyProfileAvatarPreview();
                return;
            }

            CancelRemoteProfileImageLoad();
            var loadVersion = ++remoteProfileImageLoadVersion;
            remoteProfileImageLoadRoutine = StartCoroutine(LoadRemoteProfileImageRoutine(normalizedUrl, loadVersion));
        }

        private IEnumerator LoadRemoteProfileImageRoutine(string imageUrl, int loadVersion)
        {
            using (var request = UnityWebRequestTexture.GetTexture(imageUrl))
            {
                yield return request.SendWebRequest();

                if (loadVersion != remoteProfileImageLoadVersion)
                {
                    yield break;
                }

                remoteProfileImageLoadRoutine = null;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Settings] Failed to load remote profile image: url={imageUrl} error={request.error}");
                    ReleaseRemoteProfileImagePreview();
                    loadedRemoteProfileImageUrl = null;
                    ApplyProfileAvatarPreview();
                    yield break;
                }

                var texture = DownloadHandlerTexture.GetContent(request);
                if (texture == null)
                {
                    ReleaseRemoteProfileImagePreview();
                    loadedRemoteProfileImageUrl = null;
                    ApplyProfileAvatarPreview();
                    yield break;
                }

                ReleaseRemoteProfileImagePreview();
                remoteProfileImageTexture = texture;
                remoteProfileImageTexture.name = "SettingsRemoteProfilePreview";
                remoteProfileImageTexture.hideFlags = HideFlags.HideAndDontSave;
                remoteProfileImageSprite = Sprite.Create(
                    remoteProfileImageTexture,
                    new Rect(0f, 0f, remoteProfileImageTexture.width, remoteProfileImageTexture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                remoteProfileImageSprite.name = "SettingsRemoteProfilePreviewSprite";
                remoteProfileImageSprite.hideFlags = HideFlags.HideAndDontSave;
                loadedRemoteProfileImageUrl = imageUrl;
                ApplyProfileAvatarPreview();
            }
        }

        private void CancelRemoteProfileImageLoad()
        {
            remoteProfileImageLoadVersion++;
            if (remoteProfileImageLoadRoutine != null)
            {
                StopCoroutine(remoteProfileImageLoadRoutine);
                remoteProfileImageLoadRoutine = null;
            }
        }

        private void ReleaseRemoteProfileImagePreview()
        {
            if (remoteProfileImageSprite != null)
            {
                UnityObjectSafeDestroy.Destroy(remoteProfileImageSprite);
                remoteProfileImageSprite = null;
            }

            if (remoteProfileImageTexture != null)
            {
                UnityObjectSafeDestroy.Destroy(remoteProfileImageTexture);
                remoteProfileImageTexture = null;
            }

            loadedRemoteProfileImageUrl = null;
        }

        private void ClearSelectedProfileImageUploadState()
        {
            selectedProfileImageBytes = null;
            selectedProfileImageContentType = null;
        }

        private static string GetImageContentType(string extension)
        {
            if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
            {
                return "image/png";
            }

            return "image/jpeg";
        }

        // ---------------------------------------------------------------------
        // WithdrawalView + Confirm popup
        // ---------------------------------------------------------------------

        private GameObject BuildWithdrawalView(Transform parent)
        {
            var view = CreateLayoutView("WithdrawalView", parent, 18f);

            CreateLayoutText(view.transform, "Title", "회원탈퇴 안내", 26f, FontStyles.Bold, TextColor, TextAlignmentOptions.Left, 36f);
            CreateLayoutText(view.transform, "Body", "계정을 삭제하면 이용 데이터가 삭제됩니다.\n탈퇴 후에는 일부 기능이 영구적으로 제한될 수 있으니 신중히 결정해주세요.", 18f, FontStyles.Normal, TextColor, TextAlignmentOptions.TopLeft, 84f);

            var consentRow = CreateRectObject("ConsentRow", view.transform);
            ConfigureLayoutHeight(consentRow, 64f);
            var consentRect = (RectTransform)consentRow.transform;
            consentRect.sizeDelta = new Vector2(0f, 64f);

            var consentToggle = CreateToggle(consentRow.transform, "ConsentToggle", "안내 사항을 확인하였습니다.");
            var consentToggleRect = (RectTransform)consentToggle.transform;
            consentToggleRect.anchorMin = new Vector2(0f, 0f);
            consentToggleRect.anchorMax = new Vector2(1f, 1f);
            consentToggleRect.offsetMin = Vector2.zero;
            consentToggleRect.offsetMax = Vector2.zero;

            var actionRow = CreateRectObject("ActionRow", view.transform);
            ConfigureLayoutHeight(actionRow, PrimaryControlHeight);
            var actionHorizontal = actionRow.AddComponent<HorizontalLayoutGroup>();
            actionHorizontal.spacing = 12f;
            actionHorizontal.childControlHeight = true;
            actionHorizontal.childControlWidth = true;
            actionHorizontal.childForceExpandWidth = true;
            actionHorizontal.childForceExpandHeight = true;

            var cancelButton = CreateButton(actionRow.transform, "CancelButton", "취소", SecondaryColor, TextColor, Vector2.zero, Vector2.zero, true, BorderColor);
            ResetButtonRectForLayout(cancelButton);
            cancelButton.onClick.AddListener(OnWithdrawalCancel);

            var confirmButton = CreateButton(actionRow.transform, "ConfirmButton", "탈퇴하기", ErrorColor, Color.white, Vector2.zero, Vector2.zero, false, BorderColor);
            ResetButtonRectForLayout(confirmButton);
            confirmButton.onClick.AddListener(() =>
            {
                if (consentToggle != null && !consentToggle.isOn)
                {
                    ShowNoticeToast("안내 사항 확인에 체크해주세요.");
                    return;
                }
                ShowWithdrawalConfirmPopup();
            });

            return view;
        }

        private void BuildWithdrawalConfirmPopup(Transform parent)
        {
            withdrawalConfirmPopup = CreateRectObject("WithdrawalConfirmPopup", parent);
            StretchToParent((RectTransform)withdrawalConfirmPopup.transform);

            var dim = CreateImage(withdrawalConfirmPopup.transform, "Dim", new Color(0f, 0f, 0f, 0.4f));
            StretchToParent(dim.rectTransform);
            dim.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

            var card = CreateImage(withdrawalConfirmPopup.transform, "Card", CardColor);
            card.sprite = roundedRectSprite;
            card.type = Image.Type.Sliced;
            var cardRect = card.rectTransform;
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(560f, 320f);
            cardRect.anchoredPosition = Vector2.zero;

            CreateText(card.transform, "Title", "탈퇴 안내", 24f, FontStyles.Bold, TextColor, TextAlignmentOptions.Center, new Vector2(0f, -42f), new Vector2(480f, 32f));
            CreateText(card.transform, "Body", "현재 기능이 제한될 수 있습니다.", 18f, FontStyles.Normal, MutedTextColor, TextAlignmentOptions.Center, new Vector2(0f, -118f), new Vector2(480f, 60f));

            var doneButton = CreateButton(card.transform, "DoneButton", "완료하기", PrimaryColor, Color.white, new Vector2(0f, -240f), new Vector2(360f, 60f), false, BorderColor);
            var doneRect = (RectTransform)doneButton.transform;
            doneRect.anchorMin = new Vector2(0.5f, 1f);
            doneRect.anchorMax = new Vector2(0.5f, 1f);
            doneRect.pivot = new Vector2(0.5f, 1f);
            doneButton.onClick.AddListener(OnWithdrawalConfirmDone);

            withdrawalConfirmPopup.SetActive(false);
        }

        private void ShowWithdrawalConfirmPopup()
        {
            if (withdrawalConfirmPopup != null)
            {
                withdrawalConfirmPopup.SetActive(true);
            }
        }

        private void HideWithdrawalConfirmPopup()
        {
            if (withdrawalConfirmPopup != null)
            {
                withdrawalConfirmPopup.SetActive(false);
            }
        }

        private void OnWithdrawalConfirmDone()
        {
            if (isWithdrawalInFlight)
            {
                return;
            }

#if UNITY_EDITOR
            if (DevMainSceneMode.IsActive)
            {
                FinishAccountClearAndReturnToLogin();
                return;
            }
#endif

            var withdrawalClient = EnsureSettingsApiClient();
            if (withdrawalClient == null)
            {
                ShowNoticeToast("회원 탈퇴에 실패했습니다. 잠시 후 다시 시도해주세요.");
                return;
            }

            isWithdrawalInFlight = true;
            withdrawalClient.DeleteMe(
                onSuccess: _ =>
                {
                    isWithdrawalInFlight = false;
                    FinishAccountClearAndReturnToLogin();
                },
                onError: error =>
                {
                    isWithdrawalInFlight = false;
                    ShowNoticeToast("회원 탈퇴에 실패했습니다. 잠시 후 다시 시도해주세요.");
                    Debug.LogWarning($"[Settings] DeleteMe failed: code={error?.Code} message={error?.Message}");
                });
        }

        private void OnLogoutButtonClicked()
        {
            if (isLogoutInFlight)
            {
                return;
            }
            isLogoutInFlight = true;

#if UNITY_EDITOR
            if (DevMainSceneMode.IsActive)
            {
                FinishAccountClearAndReturnToLogin();
                return;
            }
#endif

            // No refresh token to revoke — clear locally and skip the
            // round-trip. Same path used when the refresh token is missing
            // for any reason.
            var refreshToken = AuthSession.RefreshToken;
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                FinishAccountClearAndReturnToLogin();
                return;
            }

            // Switched from AuthApiClient.PostLogout (success/error both routed
            // to onComplete) to NotebookApiClient.Logout so BE failures get an
            // explicit warn log. Local-clear policy is unchanged: keeping a
            // stale local session after a failed revoke is worse than the
            // revoke failure itself, so both branches still fall through to
            // FinishAccountClearAndReturnToLogin. BE-side revoke retry is
            // out of scope here — surfacing the failure in logs is the goal.
            var client = EnsureSettingsApiClient();
            if (client == null)
            {
                FinishAccountClearAndReturnToLogin();
                return;
            }

            var request = new LogoutRequestDto { RefreshToken = refreshToken };
            client.Logout(
                request,
                onSuccess: _ => FinishAccountClearAndReturnToLogin(),
                onError: error =>
                {
                    Debug.LogWarning(
                        $"[Settings] Logout BE call failed (local session cleared anyway): code={error?.Code} message={error?.Message}");
                    FinishAccountClearAndReturnToLogin();
                });
        }

        private void FinishAccountClearAndReturnToLogin()
        {
            isLogoutInFlight = false;
            isWithdrawalInFlight = false;
            HideWithdrawalConfirmPopup();
            AuthSession.Clear();
            if (APIManager.Instance != null)
            {
                APIManager.Instance.SetAccessToken(string.Empty);
            }
            CloseSettings();
            SceneFlowManager.GoToLogin();
        }

        private void LoadCurrentProfile()
        {
            if (isProfileLoadInFlight)
            {
                return;
            }

#if UNITY_EDITOR
            if (DevMainSceneMode.IsActive)
            {
                return;
            }
#endif

            var loadClient = EnsureSettingsApiClient();
            if (loadClient == null)
            {
                // No toast — initial load failure is not user-driven, the
                // EnsureSettingsApiClient warn log is enough trace.
                return;
            }

            isProfileLoadInFlight = true;
            // hasRetried lifts a single auto-retry through the closure: if
            // the first GetMe gets a 401/403 and SessionRefresh succeeds,
            // we re-issue the request once. Any further failure (real auth
            // failure, refresh fail, second 401) takes the normal error
            // path so the user does not loop indefinitely.
            var hasRetried = false;
            string getMeUrl = App.Domain.ApiEndpointDefaults.DefaultBaseUrl.TrimEnd('/') + "/v1/users/me";
            void DoLoadCurrentProfile(NotebookApiClient apiClient)
            {
                Debug.Log($"[Settings] GET /v1/users/me 요청 → {getMeUrl}");
                apiClient.GetMe(
                    onSuccess: profile =>
                    {
                        isProfileLoadInFlight = false;
                        cachedProfile = profile;
                        Debug.Log(
                            $"[Settings] GET /v1/users/me 응답 → STATUS=2xx (onSuccess) " +
                            $"BODY={{id={profile?.Id},nickname='{profile?.Nickname}',onboarded={profile?.Onboarded}}}");
                        ApplyProfileToUi(profile);
                    },
                    onError: error =>
                    {
                        if (!hasRetried && SessionRefresh.IsAuthFailure(error))
                        {
                            hasRetried = true;
                            SessionRefresh.TryRefreshAndRetry(
                                onRetry: () => DoLoadCurrentProfile(apiClient),
                                onAbort: () =>
                                {
                                    isProfileLoadInFlight = false;
                                    // Refresh impossible (no refresh token /
                                    // refresh failed). Surface the same
                                    // load-failure UX; FinishAccountClear...
                                    // is reserved for explicit logout/withdraw.
                                    ShowNoticeToast("세션이 만료되었습니다. 다시 로그인해주세요.");
                                });
                            return;
                        }
                        isProfileLoadInFlight = false;
                        ShowNoticeToast("프로필 정보를 불러오지 못했습니다.");
                        Debug.LogWarning(
                            $"[Settings] GET /v1/users/me 실패 → STATUS={(error != null ? error.StatusCode.ToString() : "?")} " +
                            $"code={error?.Code} message={error?.Message} body={ApiManagerProvider.TrimForLog(error?.Body)}");
                    });
            }
            DoLoadCurrentProfile(loadClient);
        }

        private void ApplyProfileToUi(UserProfileResponseDto profile)
        {
            if (profile == null)
            {
                return;
            }

            DiscardSelectedProfileImageEditSession();
            ApplyProfileAvatarPreview();
            LoadRemoteProfileImage(profile.ProfileImageUrl);

            if (profileFormNicknameInput != null)
            {
                profileFormNicknameInput.text = profile.Nickname ?? string.Empty;
            }
        }

        private void LoadCurrentSpaceVisibility()
        {
            if (isSpaceVisibilityLoadInFlight)
            {
                return;
            }

#if UNITY_EDITOR
            if (DevMainSceneMode.IsActive)
            {
                return;
            }
#endif

            var client = EnsureSettingsApiClient();
            if (client == null)
            {
                return;
            }

            isSpaceVisibilityLoadInFlight = true;
            var hasRetried = false;
            void DoLoadCurrentSpaceVisibility(NotebookApiClient apiClient)
            {
                apiClient.GetMySpace(
                    onSuccess: space =>
                    {
                        isSpaceVisibilityLoadInFlight = false;
                        ApplySpaceVisibilityToUi(space);
                    },
                    onError: error =>
                    {
                        if (!hasRetried && SessionRefresh.IsAuthFailure(error))
                        {
                            hasRetried = true;
                            SessionRefresh.TryRefreshAndRetry(
                                onRetry: () => DoLoadCurrentSpaceVisibility(apiClient),
                                onAbort: () =>
                                {
                                    isSpaceVisibilityLoadInFlight = false;
                                });
                            return;
                        }

                        isSpaceVisibilityLoadInFlight = false;
                        Debug.LogWarning(
                            $"[Settings] GetMySpace for visibility failed: code={error?.Code} message={error?.Message}");
                    });
            }
            DoLoadCurrentSpaceVisibility(client);
        }

        private void ApplySpaceVisibilityToUi(SpaceResponseDto space)
        {
            if (space == null)
            {
                return;
            }

            confirmedPrivacyIndex = MapScopeToPrivacyIndex(space.VisibilityScope);
            ApplyPrivacyOptionWithoutRequest(confirmedPrivacyIndex);
        }

        // Returns null if APIManager.Instance is unavailable — callers must
        // null-check before invoking. Mirrors the resilience pattern used in
        // SpaceVisitorOverlayController and OnboardingScreenController so a
        // missing AppRoot (e.g. settings opened during a scene transition,
        // or a hot-reload race) cannot throw NRE out of the constructor.
        // ---- Notification settings wiring ----
        // Toggle index → BE field name (NotificationSettingRequestDto property).
        // Index follows BuildNotificationView's CreateNotificationCheckboxRow
        // call order:
        //   0: 방명록 추가          → notifyNewGuestbook
        //   1: 방명록 답글          → notifyGuestbookReply
        //   2: 책 추가              → notifyBookAddedByOthers
        //   3: 독서 기록 좋아요     → notifyReadingRecordReaction
        //   4: 독서 기록 댓글       → notifyReadingRecordComment
        // BE field notifyNewFollower exists (and gates would work) but there
        // is no UserFollowed event publisher on the BE side yet, so adding a
        // UI toggle here would be a setting that the server never acts on —
        // intentionally out of scope for this PR.
        private void OnNotificationToggleChanged(int index, bool value)
        {
            if (suppressNotificationToggleListener)
            {
                return;
            }

            if (isNotificationUpdateInFlight)
            {
                // Another PATCH is in flight — revert this toggle visually
                // so the user sees that the click was rejected (we cannot
                // reliably enqueue without complicating the rollback path).
                RevertNotificationToggle(index, !value);
                ShowNoticeToast("이전 변경 처리가 끝난 후 다시 시도해주세요.");
                return;
            }

#if UNITY_EDITOR
            if (DevMainSceneMode.IsActive)
            {
                // Dev mode: optimistic only, no BE round-trip.
                return;
            }
#endif

            var request = BuildNotificationPartialRequest(index, value);
            if (request == null)
            {
                // Defensive: BuildNotificationPartialRequest only returns null
                // for indices outside the known range (currently 0..4). Keep
                // the local UI change so the user is not stranded, but skip
                // the network call — there is no field to PATCH.
                Debug.LogWarning(
                    $"[Settings] Unknown notification toggle index={index}; skipping PATCH.");
                return;
            }

            var client = EnsureSettingsApiClient();
            if (client == null)
            {
                RevertNotificationToggle(index, !value);
                ShowNoticeToast("알림 설정 변경에 실패했습니다. 잠시 후 다시 시도해주세요.");
                return;
            }

            isNotificationUpdateInFlight = true;
            // Same single-retry-on-401 pattern as LoadCurrentProfile: if the
            // PATCH races a session expiry, refresh once then re-issue. Any
            // further failure takes the normal toast+revert path.
            var hasRetried = false;
            void DoUpdateNotificationSetting(NotebookApiClient apiClient, NotificationSettingRequestDto requestDto)
            {
                apiClient.UpdateNotificationSettings(
                    requestDto,
                    onSuccess: _ =>
                    {
                        isNotificationUpdateInFlight = false;
                    },
                    onError: error =>
                    {
                        if (!hasRetried && SessionRefresh.IsAuthFailure(error))
                        {
                            hasRetried = true;
                            SessionRefresh.TryRefreshAndRetry(
                                onRetry: () => DoUpdateNotificationSetting(apiClient, requestDto),
                                onAbort: () =>
                                {
                                    isNotificationUpdateInFlight = false;
                                    RevertNotificationToggle(index, !value);
                                    ShowNoticeToast("세션이 만료되었습니다. 다시 로그인해주세요.");
                                });
                            return;
                        }
                        isNotificationUpdateInFlight = false;
                        RevertNotificationToggle(index, !value);
                        ShowNoticeToast("알림 설정 변경에 실패했습니다. 잠시 후 다시 시도해주세요.");
                        Debug.LogWarning(
                            $"[Settings] UpdateMyNotificationSettings failed: code={error?.Code} message={error?.Message}");
                    });
            }
            DoUpdateNotificationSetting(client, request);
        }

        private static NotificationSettingRequestDto BuildNotificationPartialRequest(int index, bool value)
        {
            var request = new NotificationSettingRequestDto();
            switch (index)
            {
                case 0: request.NotifyNewGuestbook = value; return request;
                case 1: request.NotifyGuestbookReply = value; return request;
                case 2: request.NotifyBookAddedByOthers = value; return request;
                case 3: request.NotifyReadingRecordReaction = value; return request;
                case 4: request.NotifyReadingRecordComment = value; return request;
                default: return null;
            }
        }

        private void RevertNotificationToggle(int index, bool toValue)
        {
            if (index < 0 || index >= dependentNotificationToggles.Count)
            {
                return;
            }
            var toggle = dependentNotificationToggles[index];
            if (toggle == null)
            {
                return;
            }
            // SetIsOnWithoutNotify avoids retriggering OnNotificationToggleChanged
            // which would otherwise loop on the rollback path.
            toggle.SetIsOnWithoutNotify(toValue);
        }

        private void LoadCurrentNotificationSettings()
        {
            if (isNotificationLoadInFlight)
            {
                return;
            }
#if UNITY_EDITOR
            if (DevMainSceneMode.IsActive)
            {
                return;
            }
#endif

            var client = EnsureSettingsApiClient();
            if (client == null)
            {
                return;
            }

            isNotificationLoadInFlight = true;
            // Same single-retry-on-401 pattern as LoadCurrentProfile. Background
            // load — onAbort stays silent because LoadCurrentProfile fires on
            // the same screen-open trigger and already surfaces the
            // session-expired toast; duplicating here would stack toasts.
            var hasRetried = false;
            void DoLoadCurrentNotificationSettings(NotebookApiClient apiClient)
            {
                apiClient.GetNotificationSettings(
                    onSuccess: response =>
                    {
                        isNotificationLoadInFlight = false;
                        ApplyNotificationSettingsToUi(response);
                    },
                    onError: error =>
                    {
                        if (!hasRetried && SessionRefresh.IsAuthFailure(error))
                        {
                            hasRetried = true;
                            SessionRefresh.TryRefreshAndRetry(
                                onRetry: () => DoLoadCurrentNotificationSettings(apiClient),
                                onAbort: () =>
                                {
                                    isNotificationLoadInFlight = false;
                                });
                            return;
                        }
                        isNotificationLoadInFlight = false;
                        Debug.LogWarning(
                            $"[Settings] GetMyNotificationSettings failed: code={error?.Code} message={error?.Message}");
                    });
            }
            DoLoadCurrentNotificationSettings(client);
        }

        private void ApplyNotificationSettingsToUi(NotificationSettingResponseDto response)
        {
            if (response == null)
            {
                return;
            }
            // Suppress listener so SetIsOnWithoutNotify doesn't fan out into
            // PATCH-back loops while we apply the server snapshot.
            suppressNotificationToggleListener = true;
            try
            {
                ApplyToggleAtIndex(0, response.NotifyNewGuestbook);
                ApplyToggleAtIndex(1, response.NotifyGuestbookReply);
                ApplyToggleAtIndex(2, response.NotifyBookAddedByOthers);
                ApplyToggleAtIndex(3, response.NotifyReadingRecordReaction);
                ApplyToggleAtIndex(4, response.NotifyReadingRecordComment);
            }
            finally
            {
                suppressNotificationToggleListener = false;
            }
        }

        private void ApplyToggleAtIndex(int index, bool value)
        {
            if (index < 0 || index >= dependentNotificationToggles.Count)
            {
                return;
            }
            var toggle = dependentNotificationToggles[index];
            if (toggle != null)
            {
                toggle.SetIsOnWithoutNotify(value);
            }
        }

        // 부트스트랩 미경유 진입(예: 단독 씬 테스트, 씬 전환 race) 에서도 Settings BE 호출이
        // skip 되지 않도록 ApiManagerProvider fallback 체인을 통해 확보. 종전엔
        // APIManager.Instance==null 이면 LogWarning 만 찍고 return null 이라 PATCH/GET 자체가
        // 안 나갔던 문제를 해소한다 (OnboardingScreenController 와 동일 정책).
        private NotebookApiClient EnsureSettingsApiClient()
        {
            APIManager api = ApiManagerProvider.EnsureInstance();
            if (api == null)
            {
                Debug.LogWarning("[Settings] APIManager 확보 실패 — fallback 생성도 실패.");
                return null;
            }
            if (settingsApiClient == null)
            {
                settingsApiClient = new NotebookApiClient(api);
            }
            return settingsApiClient;
        }

        private void OnWithdrawalCancel()
        {
            HideWithdrawalConfirmPopup();
            if (withdrawalReturnView == ViewKey.ProfileEdit)
            {
                ShowProfileEdit();
            }
            else if (withdrawalReturnView == ViewKey.ProfileEditForm)
            {
                ShowProfileEdit();
            }
            else
            {
                ShowMenu();
            }
        }

        // ---------------------------------------------------------------------
        // PaymentView (탭: 결제수단 / 구매내역)
        // ---------------------------------------------------------------------

        private GameObject BuildPaymentView(Transform parent)
        {
            var view = CreateLayoutView("PaymentView", parent, 12f);

            var tabRow = BuildTabRow(view.transform, out paymentMethodsTabButton, out paymentMethodsTabIndicator, "결제수단", out purchaseHistoryTabButton, out purchaseHistoryTabIndicator, "구매내역");
            ConfigureLayoutHeight(tabRow, 64f);

            paymentMethodsTabButton.onClick.AddListener(ShowPaymentMethodsTab);
            purchaseHistoryTabButton.onClick.AddListener(ShowPurchaseHistoryTab);

            paymentMethodsTabContent = BuildPaymentMethodsTab(view.transform);
            purchaseHistoryTabContent = BuildPurchaseHistoryTab(view.transform);

            return view;
        }

        private GameObject BuildPaymentMethodsTab(Transform parent)
        {
            var root = CreateLayoutView("PaymentMethodsTab", parent, 14f);

            paymentMethodsMessageLabel = CreateLayoutText(
                root.transform,
                "PaymentMethodsMessage",
                "결제수단을 불러오는 중입니다.",
                16f,
                FontStyles.Normal,
                MutedTextColor,
                TextAlignmentOptions.Left,
                40f);

            paymentMethodsListRoot = CreateLayoutView(
                "PaymentMethodsList",
                root.transform,
                10f,
                new RectOffset(0, 0, 0, 0));

            var addButton = CreateButton(root.transform, "AddPaymentMethod", "결제수단 추가", PrimaryColor, Color.white, Vector2.zero, Vector2.zero, false, BorderColor);
            ConfigureLayoutHeight(addButton.gameObject, PrimaryControlHeight);
            addButton.onClick.AddListener(() => ShowNoticeToast("결제수단 등록은 아직 서버 API가 준비되지 않았습니다."));

            return root;
        }

        private void CreatePaymentMethodCard(Transform parent, string methodLabel, bool isPrimary)
        {
            var safeName = string.IsNullOrWhiteSpace(methodLabel) ? "Unknown" : methodLabel.Trim();
            var card = CreateRectObject($"PaymentMethod_{safeName}", parent);
            ConfigureLayoutHeight(card, 116f);

            var bg = card.AddComponent<Image>();
            bg.sprite = roundedRectSprite;
            bg.type = Image.Type.Sliced;
            bg.color = SecondaryColor;

            var outline = card.AddComponent<Outline>();
            outline.effectColor = WithAlpha(BorderColor, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);

            var label = CreateText(
                card.transform,
                "Label",
                FormatPaymentMethodLabel(methodLabel),
                20f,
                FontStyles.Bold,
                TextColor,
                TextAlignmentOptions.MidlineLeft,
                Vector2.zero,
                new Vector2(360f, 30f),
                0f,
                new Vector2(0f, 0.5f));
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(0f, 0.5f);
            labelRect.anchoredPosition = new Vector2(28f, isPrimary ? 16f : 0f);

            if (isPrimary)
            {
                var primaryBadge = CreateText(
                    card.transform,
                    "PrimaryBadge",
                    "사용 가능",
                    14f,
                    FontStyles.Bold,
                    PrimaryColor,
                    TextAlignmentOptions.MidlineLeft,
                    Vector2.zero,
                    new Vector2(360f, 22f),
                    0f,
                    new Vector2(0f, 0.5f));
                var primaryBadgeRect = primaryBadge.rectTransform;
                primaryBadgeRect.anchorMin = new Vector2(0f, 0.5f);
                primaryBadgeRect.anchorMax = new Vector2(0f, 0.5f);
                primaryBadgeRect.anchoredPosition = new Vector2(28f, -18f);
            }
        }

        private GameObject BuildPurchaseHistoryTab(Transform parent)
        {
            var root = CreateLayoutView("PurchaseHistoryTab", parent, 12f);

            paymentWalletBalanceLabel = CreateLayoutText(
                root.transform,
                "WalletBalance",
                "보유 페이지: 불러오는 중",
                18f,
                FontStyles.Bold,
                TextColor,
                TextAlignmentOptions.Left,
                42f);

            purchaseHistoryMessageLabel = CreateLayoutText(
                root.transform,
                "PurchaseHistoryMessage",
                "구매내역을 불러오는 중입니다.",
                16f,
                FontStyles.Normal,
                MutedTextColor,
                TextAlignmentOptions.Left,
                40f);

            purchaseHistoryListRoot = CreateLayoutView(
                "PurchaseHistoryList",
                root.transform,
                8f,
                new RectOffset(0, 0, 0, 0));

            return root;
        }

        private void CreatePurchaseHistoryRow(Transform parent, string date, string itemName, string amount)
        {
            var row = CreateRectObject($"Purchase_{date}", parent);
            ConfigureLayoutHeight(row, 72f);

            var bg = row.AddComponent<Image>();
            bg.sprite = roundedRectSprite;
            bg.type = Image.Type.Sliced;
            bg.color = WithAlpha(SecondaryColor, 0.7f);

            var dateText = CreateText(row.transform, "Date", date, 16f, FontStyles.Bold, MutedTextColor, TextAlignmentOptions.MidlineLeft, Vector2.zero, new Vector2(120f, 50f), 0f, new Vector2(0f, 0.5f));
            var dateRect = dateText.rectTransform;
            dateRect.anchorMin = new Vector2(0f, 0.5f);
            dateRect.anchorMax = new Vector2(0f, 0.5f);
            dateRect.anchoredPosition = new Vector2(24f, 0f);

            var nameText = CreateText(row.transform, "Name", itemName, 18f, FontStyles.Normal, TextColor, TextAlignmentOptions.MidlineLeft, Vector2.zero, new Vector2(420f, 30f), 0f, new Vector2(0f, 0.5f));
            var nameRect = nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 0.5f);
            nameRect.anchorMax = new Vector2(0f, 0.5f);
            nameRect.anchoredPosition = new Vector2(160f, 0f);

            var amountText = CreateText(row.transform, "Amount", amount, 18f, FontStyles.Bold, PrimaryColor, TextAlignmentOptions.MidlineRight, Vector2.zero, new Vector2(160f, 30f), 0f, new Vector2(1f, 0.5f));
            var amountRect = amountText.rectTransform;
            amountRect.anchorMin = new Vector2(1f, 0.5f);
            amountRect.anchorMax = new Vector2(1f, 0.5f);
            amountRect.anchoredPosition = new Vector2(-24f, 0f);
        }

        private void ShowPaymentMethodsTab()
        {
            if (paymentMethodsTabContent != null)
            {
                paymentMethodsTabContent.SetActive(true);
            }
            if (purchaseHistoryTabContent != null)
            {
                purchaseHistoryTabContent.SetActive(false);
            }
            UpdateTabIndicator(paymentMethodsTabIndicator, true);
            UpdateTabIndicator(purchaseHistoryTabIndicator, false);
            LoadPaymentMethodsForSettings();
        }

        private void ShowPurchaseHistoryTab()
        {
            if (paymentMethodsTabContent != null)
            {
                paymentMethodsTabContent.SetActive(false);
            }
            if (purchaseHistoryTabContent != null)
            {
                purchaseHistoryTabContent.SetActive(true);
            }
            UpdateTabIndicator(paymentMethodsTabIndicator, false);
            UpdateTabIndicator(purchaseHistoryTabIndicator, true);
            LoadWalletForSettings();
            LoadPurchaseHistoryForSettings();
        }

        private void LoadPaymentMethodsForSettings()
        {
            if (isPaymentMethodsLoadInFlight)
            {
                return;
            }

#if UNITY_EDITOR
            if (DevMainSceneMode.IsActive)
            {
                RenderPaymentMethods(null, "DEV 모드에서는 결제수단 API를 호출하지 않습니다.");
                return;
            }
#endif

            var client = EnsureSettingsApiClient();
            if (client == null)
            {
                RenderPaymentMethods(null, "결제 API 설정이 없습니다.");
                return;
            }

            isPaymentMethodsLoadInFlight = true;
            SetPaymentMethodsMessage("결제수단을 불러오는 중입니다.");
            ClearChildren(paymentMethodsListRoot?.transform);

            var hasRetried = false;
            void DoLoadPaymentMethods(NotebookApiClient apiClient)
            {
                apiClient.GetPaymentMethods(
                    onSuccess: response =>
                    {
                        isPaymentMethodsLoadInFlight = false;
                        RenderPaymentMethods(response?.Methods);
                    },
                    onError: error =>
                    {
                        if (!hasRetried && SessionRefresh.IsAuthFailure(error))
                        {
                            hasRetried = true;
                            SessionRefresh.TryRefreshAndRetry(
                                onRetry: () => DoLoadPaymentMethods(apiClient),
                                onAbort: () =>
                                {
                                    isPaymentMethodsLoadInFlight = false;
                                    RenderPaymentMethods(null, "세션이 만료되었습니다. 다시 로그인해주세요.");
                                });
                            return;
                        }

                        isPaymentMethodsLoadInFlight = false;
                        RenderPaymentMethods(null, "결제수단을 불러오지 못했습니다.");
                        Debug.LogWarning(
                            $"[Settings] GetPaymentMethods failed: code={error?.Code} message={error?.Message}");
                    });
            }
            DoLoadPaymentMethods(client);
        }

        private void RenderPaymentMethods(IReadOnlyList<string> methods, string emptyMessage = null)
        {
            ClearChildren(paymentMethodsListRoot?.transform);

            var renderedCount = 0;
            if (methods != null)
            {
                for (var i = 0; i < methods.Count; i++)
                {
                    var method = methods[i];
                    if (string.IsNullOrWhiteSpace(method))
                    {
                        continue;
                    }

                    CreatePaymentMethodCard(paymentMethodsListRoot.transform, method, renderedCount == 0);
                    renderedCount++;
                }
            }

            if (renderedCount == 0)
            {
                SetPaymentMethodsMessage(emptyMessage ?? "등록된 결제수단이 없습니다.");
                return;
            }

            SetPaymentMethodsMessage(null);
        }

        private void LoadWalletForSettings()
        {
            if (paymentWalletBalanceLabel == null || isWalletLoadInFlight)
            {
                return;
            }

#if UNITY_EDITOR
            if (DevMainSceneMode.IsActive)
            {
                paymentWalletBalanceLabel.text = "보유 페이지: DEV";
                return;
            }
#endif

            var client = EnsureSettingsApiClient();
            if (client == null)
            {
                paymentWalletBalanceLabel.text = "보유 페이지: 불러오기 실패";
                return;
            }

            isWalletLoadInFlight = true;
            paymentWalletBalanceLabel.text = "보유 페이지: 불러오는 중";

            var hasRetried = false;
            void DoLoadWallet(NotebookApiClient apiClient)
            {
                apiClient.GetWallet(
                    onSuccess: response =>
                    {
                        isWalletLoadInFlight = false;
                        var balance = Mathf.Max(0, response?.BalancePages ?? 0);
                        paymentWalletBalanceLabel.text = $"보유 페이지: {balance:N0} P";
                    },
                    onError: error =>
                    {
                        if (!hasRetried && SessionRefresh.IsAuthFailure(error))
                        {
                            hasRetried = true;
                            SessionRefresh.TryRefreshAndRetry(
                                onRetry: () => DoLoadWallet(apiClient),
                                onAbort: () =>
                                {
                                    isWalletLoadInFlight = false;
                                    paymentWalletBalanceLabel.text = "보유 페이지: 세션 만료";
                                });
                            return;
                        }

                        isWalletLoadInFlight = false;
                        paymentWalletBalanceLabel.text = "보유 페이지: 불러오기 실패";
                        Debug.LogWarning(
                            $"[Settings] GetWallet failed: code={error?.Code} message={error?.Message}");
                    });
            }
            DoLoadWallet(client);
        }

        private void LoadPurchaseHistoryForSettings()
        {
            if (isPurchaseHistoryLoadInFlight)
            {
                return;
            }

#if UNITY_EDITOR
            if (DevMainSceneMode.IsActive)
            {
                RenderPurchaseHistory(null, "DEV 모드에서는 구매내역 API를 호출하지 않습니다.");
                return;
            }
#endif

            var client = EnsureSettingsApiClient();
            if (client == null)
            {
                RenderPurchaseHistory(null, "결제 API 설정이 없습니다.");
                return;
            }

            isPurchaseHistoryLoadInFlight = true;
            SetPurchaseHistoryMessage("구매내역을 불러오는 중입니다.");
            ClearChildren(purchaseHistoryListRoot?.transform);

            var hasRetried = false;
            void DoLoadPurchaseHistory(NotebookApiClient apiClient)
            {
                apiClient.GetPurchases(
                    onSuccess: response =>
                    {
                        isPurchaseHistoryLoadInFlight = false;
                        RenderPurchaseHistory(response?.Purchases);
                    },
                    onError: error =>
                    {
                        if (!hasRetried && SessionRefresh.IsAuthFailure(error))
                        {
                            hasRetried = true;
                            SessionRefresh.TryRefreshAndRetry(
                                onRetry: () => DoLoadPurchaseHistory(apiClient),
                                onAbort: () =>
                                {
                                    isPurchaseHistoryLoadInFlight = false;
                                    RenderPurchaseHistory(null, "세션이 만료되었습니다. 다시 로그인해주세요.");
                                });
                            return;
                        }

                        isPurchaseHistoryLoadInFlight = false;
                        RenderPurchaseHistory(null, "구매내역을 불러오지 못했습니다.");
                        Debug.LogWarning(
                            $"[Settings] GetPurchases failed: code={error?.Code} message={error?.Message}");
                    });
            }
            DoLoadPurchaseHistory(client);
        }

        private void RenderPurchaseHistory(IReadOnlyList<PurchaseHistoryResponseDto> purchases, string emptyMessage = null)
        {
            ClearChildren(purchaseHistoryListRoot?.transform);

            var renderedCount = 0;
            if (purchases != null)
            {
                for (var i = 0; i < purchases.Count; i++)
                {
                    var purchase = purchases[i];
                    if (purchase == null)
                    {
                        continue;
                    }

                    CreatePurchaseHistoryRow(
                        purchaseHistoryListRoot.transform,
                        FormatPurchaseStatus(purchase.Status),
                        $"Asset #{purchase.AssetId}",
                        $"{Mathf.Max(0, purchase.PricePages):N0} P");
                    renderedCount++;
                }
            }

            if (renderedCount == 0)
            {
                SetPurchaseHistoryMessage(emptyMessage ?? "구매내역이 없습니다.");
                return;
            }

            SetPurchaseHistoryMessage(null);
        }

        private void SetPaymentMethodsMessage(string message)
        {
            if (paymentMethodsMessageLabel == null)
            {
                return;
            }

            paymentMethodsMessageLabel.text = message ?? string.Empty;
            paymentMethodsMessageLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
        }

        private void SetPurchaseHistoryMessage(string message)
        {
            if (purchaseHistoryMessageLabel == null)
            {
                return;
            }

            purchaseHistoryMessageLabel.text = message ?? string.Empty;
            purchaseHistoryMessageLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
        }

        private static string FormatPaymentMethodLabel(string method)
        {
            var normalized = string.IsNullOrWhiteSpace(method) ? string.Empty : method.Trim();
            return normalized.ToUpperInvariant() switch
            {
                "INICIS" => "KG이니시스",
                "CARD" => "카드",
                "KAKAO_PAY" => "카카오페이",
                "NAVER_PAY" => "네이버페이",
                _ => string.IsNullOrWhiteSpace(normalized) ? "알 수 없는 결제수단" : normalized
            };
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
                "REFUNDED" => "환불됨",
                _ => string.IsNullOrWhiteSpace(status) ? "상태 없음" : status
            };
        }

        // ---------------------------------------------------------------------
        // NotificationView
        // ---------------------------------------------------------------------

        private GameObject BuildNotificationView(Transform parent)
        {
            var view = CreateLayoutView("NotificationView", parent, 14f);

            dependentNotificationToggles.Clear();
            dependentNotificationLabels.Clear();

            var masterRow = CreateRectObject("MasterRow", view.transform);
            ConfigureLayoutHeight(masterRow, 80f);

            var masterBg = masterRow.AddComponent<Image>();
            masterBg.sprite = roundedRectSprite;
            masterBg.type = Image.Type.Sliced;
            masterBg.color = WithAlpha(PrimaryColor, 0.1f);

            CreateText(
                masterRow.transform,
                "Label",
                "전체 알림",
                22f,
                FontStyles.Bold,
                TextColor,
                TextAlignmentOptions.MidlineLeft,
                Vector2.zero,
                new Vector2(360f, 60f),
                0f,
                new Vector2(0f, 0.5f));
            var masterLabelRect = ((RectTransform)masterRow.transform.GetChild(masterRow.transform.childCount - 1));
            masterLabelRect.anchorMin = new Vector2(0f, 0.5f);
            masterLabelRect.anchorMax = new Vector2(0f, 0.5f);
            masterLabelRect.anchoredPosition = new Vector2(28f, 0f);

            masterNotificationToggle = CreateToggle(masterRow.transform, "MasterToggle", string.Empty);
            var masterToggleRect = (RectTransform)masterNotificationToggle.transform;
            masterToggleRect.anchorMin = new Vector2(1f, 0.5f);
            masterToggleRect.anchorMax = new Vector2(1f, 0.5f);
            masterToggleRect.pivot = new Vector2(1f, 0.5f);
            masterToggleRect.sizeDelta = new Vector2(80f, 56f);
            masterToggleRect.anchoredPosition = new Vector2(-28f, 0f);
            masterNotificationToggle.isOn = true;
            masterNotificationToggle.onValueChanged.AddListener(_ => RefreshNotificationDependentEnabled());

            CreateSectionHeader(view.transform, "방명록");
            CreateNotificationCheckboxRow(view.transform, "방명록 추가", true);
            CreateNotificationCheckboxRow(view.transform, "방명록 답글", true);

            CreateSectionHeader(view.transform, "책 / 독서 기록");
            CreateNotificationCheckboxRow(view.transform, "책 추가", true);
            CreateNotificationCheckboxRow(view.transform, "독서 기록 좋아요", false);
            CreateNotificationCheckboxRow(view.transform, "독서 기록 댓글", true);

            return view;
        }

        private void CreateNotificationCheckboxRow(Transform parent, string labelText, bool initialOn)
        {
            var row = CreateRectObject($"NotificationRow_{labelText}", parent);
            ConfigureLayoutHeight(row, 64f);

            var label = CreateText(
                row.transform,
                "Label",
                labelText,
                18f,
                FontStyles.Normal,
                TextColor,
                TextAlignmentOptions.MidlineLeft,
                Vector2.zero,
                new Vector2(440f, 36f),
                0f,
                new Vector2(0f, 0.5f));
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(0f, 0.5f);
            labelRect.anchoredPosition = new Vector2(20f, 0f);
            dependentNotificationLabels.Add(label);

            var toggle = CreateToggle(row.transform, $"Toggle_{labelText}", string.Empty);
            var toggleRect = (RectTransform)toggle.transform;
            toggleRect.anchorMin = new Vector2(1f, 0.5f);
            toggleRect.anchorMax = new Vector2(1f, 0.5f);
            toggleRect.pivot = new Vector2(1f, 0.5f);
            toggleRect.sizeDelta = new Vector2(64f, 50f);
            toggleRect.anchoredPosition = new Vector2(-20f, 0f);
            toggle.isOn = initialOn;
            // Capture the index by-value so the closure points at this row's
            // slot in dependentNotificationToggles regardless of later adds.
            var capturedIndex = dependentNotificationToggles.Count;
            dependentNotificationToggles.Add(toggle);
            toggle.onValueChanged.AddListener(value => OnNotificationToggleChanged(capturedIndex, value));
        }

        private void RefreshNotificationDependentEnabled()
        {
            var enabled = masterNotificationToggle == null || masterNotificationToggle.isOn;

            foreach (var toggle in dependentNotificationToggles)
            {
                if (toggle == null)
                {
                    continue;
                }
                toggle.interactable = enabled;
            }

            foreach (var label in dependentNotificationLabels)
            {
                if (label == null)
                {
                    continue;
                }
                label.color = enabled ? TextColor : SubtleTextColor;
            }
        }

        // ---------------------------------------------------------------------
        // ReportHistoryView (신고내역 / 제재내역)
        // ---------------------------------------------------------------------

        private GameObject BuildReportHistoryView(Transform parent)
        {
            var view = CreateLayoutView("ReportHistoryView", parent, 12f);

            var tabRow = BuildTabRow(view.transform, out reportListTabButton, out reportListTabIndicator, "신고내역", out sanctionListTabButton, out sanctionListTabIndicator, "제재내역");
            ConfigureLayoutHeight(tabRow, 64f);

            reportListTabButton.onClick.AddListener(ShowReportListTab);
            sanctionListTabButton.onClick.AddListener(ShowSanctionListTab);

            reportListTabContent = BuildReportListTab(view.transform);
            sanctionListTabContent = BuildSanctionListTab(view.transform);

            return view;
        }

        private GameObject BuildReportListTab(Transform parent)
        {
            var root = CreateLayoutView("ReportListTab", parent, 12f);

            reportListMessageLabel = CreateLayoutText(
                root.transform,
                "ReportListMessage",
                "신고내역을 불러오는 중입니다.",
                16f,
                FontStyles.Normal,
                MutedTextColor,
                TextAlignmentOptions.Left,
                40f);
            reportListRoot = CreateLayoutView(
                "ReportList",
                root.transform,
                8f,
                new RectOffset(0, 0, 0, 0));

            return root;
        }

        private void CreateReportRow(Transform parent, string targetName, string reason, string status, Color statusColor)
        {
            var row = CreateRectObject($"ReportRow_{targetName}", parent);
            ConfigureLayoutHeight(row, 88f);

            var bg = row.AddComponent<Image>();
            bg.sprite = roundedRectSprite;
            bg.type = Image.Type.Sliced;
            bg.color = SecondaryColor;

            var outline = row.AddComponent<Outline>();
            outline.effectColor = WithAlpha(BorderColor, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);

            var nameText = CreateText(row.transform, "TargetName", targetName, 18f, FontStyles.Bold, TextColor, TextAlignmentOptions.MidlineLeft, Vector2.zero, new Vector2(360f, 28f), 0f, new Vector2(0f, 0.5f));
            var nameRect = nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 0.5f);
            nameRect.anchorMax = new Vector2(0f, 0.5f);
            nameRect.anchoredPosition = new Vector2(24f, 16f);

            var reasonText = CreateText(row.transform, "Reason", $"사유: {reason}", 15f, FontStyles.Normal, MutedTextColor, TextAlignmentOptions.MidlineLeft, Vector2.zero, new Vector2(360f, 24f), 0f, new Vector2(0f, 0.5f));
            var reasonRect = reasonText.rectTransform;
            reasonRect.anchorMin = new Vector2(0f, 0.5f);
            reasonRect.anchorMax = new Vector2(0f, 0.5f);
            reasonRect.anchoredPosition = new Vector2(24f, -14f);

            var statusText = CreateText(row.transform, "Status", status, 16f, FontStyles.Bold, statusColor, TextAlignmentOptions.MidlineRight, Vector2.zero, new Vector2(140f, 32f), 0f, new Vector2(1f, 0.5f));
            var statusRect = statusText.rectTransform;
            statusRect.anchorMin = new Vector2(1f, 0.5f);
            statusRect.anchorMax = new Vector2(1f, 0.5f);
            statusRect.anchoredPosition = new Vector2(-24f, 0f);
        }

        private GameObject BuildSanctionListTab(Transform parent)
        {
            var root = CreateLayoutView("SanctionListTab", parent, 12f);

            blockedUsersMessageLabel = CreateLayoutText(
                root.transform,
                "BlockedUsersMessage",
                "차단 목록을 불러오는 중입니다.",
                16f,
                FontStyles.Normal,
                MutedTextColor,
                TextAlignmentOptions.Left,
                40f);
            blockedUsersListRoot = CreateLayoutView(
                "BlockedUsersList",
                root.transform,
                8f,
                new RectOffset(0, 0, 0, 0));

            return root;
        }

        private void CreateBlockedUserRow(Transform parent, UserSummaryResponseDto user)
        {
            var nickname = string.IsNullOrWhiteSpace(user?.Nickname) ? $"User #{user?.Id ?? 0L}" : user.Nickname.Trim();
            var row = CreateRectObject($"BlockedUserRow_{nickname}", parent);
            ConfigureLayoutHeight(row, 96f);

            var bg = row.AddComponent<Image>();
            bg.sprite = roundedRectSprite;
            bg.type = Image.Type.Sliced;
            bg.color = SecondaryColor;

            var outline = row.AddComponent<Outline>();
            outline.effectColor = WithAlpha(BorderColor, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);

            var nameText = CreateText(row.transform, "Nickname", $"{nickname} 차단", 18f, FontStyles.Bold, TextColor, TextAlignmentOptions.MidlineLeft, Vector2.zero, new Vector2(420f, 28f), 0f, new Vector2(0f, 0.5f));
            var nameRect = nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 0.5f);
            nameRect.anchorMax = new Vector2(0f, 0.5f);
            nameRect.anchoredPosition = new Vector2(24f, 18f);

            var dateText = CreateText(row.transform, "BlockedDate", $"User ID {user?.Id ?? 0L}", 15f, FontStyles.Normal, MutedTextColor, TextAlignmentOptions.MidlineLeft, Vector2.zero, new Vector2(420f, 22f), 0f, new Vector2(0f, 0.5f));
            var dateRect = dateText.rectTransform;
            dateRect.anchorMin = new Vector2(0f, 0.5f);
            dateRect.anchorMax = new Vector2(0f, 0.5f);
            dateRect.anchoredPosition = new Vector2(24f, -14f);

            var releaseButton = CreateButton(row.transform, "ReleaseButton", "해제", SecondaryColor, TextColor, Vector2.zero, new Vector2(96f, 52f), true, BorderColor);
            var releaseRect = (RectTransform)releaseButton.transform;
            releaseRect.anchorMin = new Vector2(1f, 0.5f);
            releaseRect.anchorMax = new Vector2(1f, 0.5f);
            releaseRect.pivot = new Vector2(1f, 0.5f);
            releaseRect.anchoredPosition = new Vector2(-20f, 0f);
            var targetUserId = user?.Id ?? 0L;
            releaseButton.onClick.AddListener(() => UnblockUserFromSettings(targetUserId));
        }

        private void ShowReportListTab()
        {
            if (reportListTabContent != null)
            {
                reportListTabContent.SetActive(true);
            }
            if (sanctionListTabContent != null)
            {
                sanctionListTabContent.SetActive(false);
            }
            UpdateTabIndicator(reportListTabIndicator, true);
            UpdateTabIndicator(sanctionListTabIndicator, false);
            LoadReportListForSettings();
        }

        private void ShowSanctionListTab()
        {
            if (reportListTabContent != null)
            {
                reportListTabContent.SetActive(false);
            }
            if (sanctionListTabContent != null)
            {
                sanctionListTabContent.SetActive(true);
            }
            UpdateTabIndicator(reportListTabIndicator, false);
            UpdateTabIndicator(sanctionListTabIndicator, true);
            LoadBlockedUsersForSettings();
        }

        private void LoadReportListForSettings()
        {
            if (isReportListLoadInFlight)
            {
                return;
            }

#if UNITY_EDITOR
            if (DevMainSceneMode.IsActive)
            {
                RenderReportList(null, "DEV 모드에서는 신고내역 API를 호출하지 않습니다.");
                return;
            }
#endif

            var client = EnsureSettingsApiClient();
            if (client == null)
            {
                RenderReportList(null, "신고 API 설정이 없습니다.");
                return;
            }

            isReportListLoadInFlight = true;
            SetReportListMessage("신고내역을 불러오는 중입니다.");
            ClearChildren(reportListRoot?.transform);

            var hasRetried = false;
            void DoLoadReports(NotebookApiClient apiClient)
            {
                apiClient.GetMyReports(
                    onSuccess: response =>
                    {
                        isReportListLoadInFlight = false;
                        RenderReportList(response?.Reports);
                    },
                    onError: error =>
                    {
                        if (!hasRetried && SessionRefresh.IsAuthFailure(error))
                        {
                            hasRetried = true;
                            SessionRefresh.TryRefreshAndRetry(
                                onRetry: () => DoLoadReports(apiClient),
                                onAbort: () =>
                                {
                                    isReportListLoadInFlight = false;
                                    RenderReportList(null, "세션이 만료되었습니다. 다시 로그인해주세요.");
                                });
                            return;
                        }

                        isReportListLoadInFlight = false;
                        RenderReportList(null, "신고내역을 불러오지 못했습니다.");
                        Debug.LogWarning(
                            $"[Settings] GetMyReports failed: code={error?.Code} message={error?.Message}");
                    });
            }
            DoLoadReports(client);
        }

        private void RenderReportList(IReadOnlyList<ReportResponseDto> reports, string emptyMessage = null)
        {
            ClearChildren(reportListRoot?.transform);

            var renderedCount = 0;
            if (reports != null)
            {
                for (var i = 0; i < reports.Count; i++)
                {
                    var report = reports[i];
                    if (report == null)
                    {
                        continue;
                    }

                    CreateReportRow(
                        reportListRoot.transform,
                        FormatReportTarget(report),
                        FormatReportReason(report.ReasonType),
                        FormatReportStatus(report.Status),
                        ReportStatusColor(report.Status));
                    renderedCount++;
                }
            }

            if (renderedCount == 0)
            {
                SetReportListMessage(emptyMessage ?? "신고내역이 없습니다.");
                return;
            }

            SetReportListMessage(null);
        }

        private void LoadBlockedUsersForSettings()
        {
            if (isBlockListLoadInFlight)
            {
                return;
            }

#if UNITY_EDITOR
            if (DevMainSceneMode.IsActive)
            {
                RenderBlockedUsers(null, "DEV 모드에서는 차단 목록 API를 호출하지 않습니다.");
                return;
            }
#endif

            var client = EnsureSettingsApiClient();
            if (client == null)
            {
                RenderBlockedUsers(null, "사용자 API 설정이 없습니다.");
                return;
            }

            isBlockListLoadInFlight = true;
            SetBlockedUsersMessage("차단 목록을 불러오는 중입니다.");
            ClearChildren(blockedUsersListRoot?.transform);

            var hasRetried = false;
            void DoLoadBlocks(NotebookApiClient apiClient)
            {
                apiClient.GetBlocks(
                    onSuccess: response =>
                    {
                        isBlockListLoadInFlight = false;
                        RenderBlockedUsers(response?.Users);
                    },
                    onError: error =>
                    {
                        if (!hasRetried && SessionRefresh.IsAuthFailure(error))
                        {
                            hasRetried = true;
                            SessionRefresh.TryRefreshAndRetry(
                                onRetry: () => DoLoadBlocks(apiClient),
                                onAbort: () =>
                                {
                                    isBlockListLoadInFlight = false;
                                    RenderBlockedUsers(null, "세션이 만료되었습니다. 다시 로그인해주세요.");
                                });
                            return;
                        }

                        isBlockListLoadInFlight = false;
                        RenderBlockedUsers(null, "차단 목록을 불러오지 못했습니다.");
                        Debug.LogWarning(
                            $"[Settings] GetBlocks failed: code={error?.Code} message={error?.Message}");
                    });
            }
            DoLoadBlocks(client);
        }

        private void RenderBlockedUsers(IReadOnlyList<UserSummaryResponseDto> users, string emptyMessage = null)
        {
            ClearChildren(blockedUsersListRoot?.transform);

            var renderedCount = 0;
            if (users != null)
            {
                for (var i = 0; i < users.Count; i++)
                {
                    var user = users[i];
                    if (user == null)
                    {
                        continue;
                    }

                    CreateBlockedUserRow(blockedUsersListRoot.transform, user);
                    renderedCount++;
                }
            }

            if (renderedCount == 0)
            {
                SetBlockedUsersMessage(emptyMessage ?? "차단한 사용자가 없습니다.");
                return;
            }

            SetBlockedUsersMessage(null);
        }

        private void UnblockUserFromSettings(long targetUserId)
        {
            if (targetUserId <= 0L || isUnblockInFlight)
            {
                return;
            }

#if UNITY_EDITOR
            if (DevMainSceneMode.IsActive)
            {
                ShowNoticeToast("DEV 모드에서는 차단 해제 API를 호출하지 않습니다.");
                return;
            }
#endif

            var client = EnsureSettingsApiClient();
            if (client == null)
            {
                ShowNoticeToast("차단 해제에 실패했습니다. 잠시 후 다시 시도해주세요.");
                return;
            }

            isUnblockInFlight = true;
            var hasRetried = false;
            void DoUnblock(NotebookApiClient apiClient)
            {
                apiClient.UnblockUser(
                    targetUserId,
                    onSuccess: _ =>
                    {
                        isUnblockInFlight = false;
                        ShowNoticeToast("차단을 해제했습니다.");
                        LoadBlockedUsersForSettings();
                    },
                    onError: error =>
                    {
                        if (!hasRetried && SessionRefresh.IsAuthFailure(error))
                        {
                            hasRetried = true;
                            SessionRefresh.TryRefreshAndRetry(
                                onRetry: () => DoUnblock(apiClient),
                                onAbort: () =>
                                {
                                    isUnblockInFlight = false;
                                    ShowNoticeToast("세션이 만료되었습니다. 다시 로그인해주세요.");
                                });
                            return;
                        }

                        isUnblockInFlight = false;
                        ShowNoticeToast("차단 해제에 실패했습니다. 잠시 후 다시 시도해주세요.");
                        Debug.LogWarning(
                            $"[Settings] UnblockUser failed: targetUserId={targetUserId} code={error?.Code} message={error?.Message}");
                    });
            }
            DoUnblock(client);
        }

        private void SetReportListMessage(string message)
        {
            if (reportListMessageLabel == null)
            {
                return;
            }

            reportListMessageLabel.text = message ?? string.Empty;
            reportListMessageLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
        }

        private void SetBlockedUsersMessage(string message)
        {
            if (blockedUsersMessageLabel == null)
            {
                return;
            }

            blockedUsersMessageLabel.text = message ?? string.Empty;
            blockedUsersMessageLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
        }

        private static string FormatReportTarget(ReportResponseDto report)
        {
            if (report == null)
            {
                return "신고 대상 없음";
            }

            var targetType = string.IsNullOrWhiteSpace(report.TargetType) ? "TARGET" : report.TargetType.Trim();
            return $"{targetType} #{report.TargetId}";
        }

        private static string FormatReportReason(string reasonType)
        {
            var normalized = string.IsNullOrWhiteSpace(reasonType) ? string.Empty : reasonType.Trim().ToUpperInvariant();
            return normalized switch
            {
                "SPAM" => "스팸",
                "HARASSMENT" => "괴롭힘",
                "ABUSE" => "욕설/비방",
                "INAPPROPRIATE" => "부적절한 콘텐츠",
                "COPYRIGHT" => "저작권 침해",
                _ => string.IsNullOrWhiteSpace(reasonType) ? "기타" : reasonType
            };
        }

        private static string FormatReportStatus(string status)
        {
            var normalized = string.IsNullOrWhiteSpace(status) ? string.Empty : status.Trim().ToUpperInvariant();
            return normalized switch
            {
                "PENDING" => "신고 접수",
                "IN_REVIEW" => "처리 중",
                "RESOLVED" => "처리 완료",
                "REJECTED" => "반려",
                _ => string.IsNullOrWhiteSpace(status) ? "상태 없음" : status
            };
        }

        private static Color ReportStatusColor(string status)
        {
            var normalized = string.IsNullOrWhiteSpace(status) ? string.Empty : status.Trim().ToUpperInvariant();
            return normalized switch
            {
                "RESOLVED" => SuccessColor,
                "REJECTED" => MutedTextColor,
                "IN_REVIEW" => Hex("#D58A2A"),
                _ => PrimaryColor
            };
        }

        // ---------------------------------------------------------------------
        // TermsPolicyView
        // ---------------------------------------------------------------------

        private GameObject BuildTermsPolicyView(Transform parent)
        {
            var view = CreateLayoutView("TermsPolicyView", parent, 12f);

            var tabRow = CreateRectObject("TermsTabRow", view.transform);
            ConfigureLayoutHeight(tabRow, 64f);

            var tabHorizontal = tabRow.AddComponent<HorizontalLayoutGroup>();
            tabHorizontal.spacing = 8f;
            tabHorizontal.childControlHeight = true;
            tabHorizontal.childControlWidth = true;
            tabHorizontal.childForceExpandWidth = true;
            tabHorizontal.childForceExpandHeight = true;

            termsTabButtons.Clear();
            termsTabIndicators.Clear();

            string[] termsTabs = { "이용약관", "개인정보처리방침", "환불정책" };
            for (var i = 0; i < termsTabs.Length; i++)
            {
                var captureIndex = i;
                var tabButton = CreateTabButton(tabRow.transform, termsTabs[i], out var indicator);
                tabButton.onClick.AddListener(() => SelectTermsTab(captureIndex));
                termsTabButtons.Add(tabButton);
                termsTabIndicators.Add(indicator);
            }

            var contentArea = CreateRectObject("TermsContentArea", view.transform);
            ConfigureLayoutHeight(contentArea, 800f);

            var contentBg = contentArea.AddComponent<Image>();
            contentBg.sprite = roundedRectSprite;
            contentBg.type = Image.Type.Sliced;
            contentBg.color = WithAlpha(SecondaryColor, 0.65f);

            termsContentLabel = CreateText(
                contentArea.transform,
                "TermsContent",
                string.Empty,
                16f,
                FontStyles.Normal,
                TextColor,
                TextAlignmentOptions.TopLeft,
                Vector2.zero,
                Vector2.zero,
                0f,
                new Vector2(0.5f, 0.5f));
            var termsContentRect = termsContentLabel.rectTransform;
            termsContentRect.anchorMin = Vector2.zero;
            termsContentRect.anchorMax = Vector2.one;
            termsContentRect.offsetMin = new Vector2(28f, 28f);
            termsContentRect.offsetMax = new Vector2(-28f, -28f);

            return view;
        }

        private void SelectTermsTab(int index)
        {
            selectedTermsIndex = Mathf.Clamp(index, 0, termsTabButtons.Count - 1);

            for (var i = 0; i < termsTabIndicators.Count; i++)
            {
                UpdateTabIndicator(termsTabIndicators[i], i == selectedTermsIndex);
            }

            if (termsContentLabel == null)
            {
                return;
            }

            switch (selectedTermsIndex)
            {
                case 0:
                    termsContentLabel.text = GetDummyTermsOfService();
                    break;
                case 1:
                    termsContentLabel.text = GetDummyPrivacyPolicy();
                    break;
                default:
                    termsContentLabel.text = GetDummyRefundPolicy();
                    break;
            }
        }

        private static string GetDummyTermsOfService()
        {
            return
                "[이용약관 - 데모 본문]\n\n" +
                "제 1조 (목적)\n" +
                "본 약관은 BookRoom 서비스의 이용 조건과 절차를 규정하는 데모 약관입니다.\n\n" +
                "제 2조 (서비스 이용)\n" +
                "회원은 본 약관에 동의함으로써 데모 환경 내 모든 기능을 시험할 수 있습니다.\n\n" +
                "제 3조 (개인정보 보호)\n" +
                "데모 본문이며 실제 약관 효력을 갖지 않습니다.";
        }

        private static string GetDummyPrivacyPolicy()
        {
            return
                "[개인정보처리방침 - 데모 본문]\n\n" +
                "1. 수집 항목: 닉네임, 이메일 (데모용)\n" +
                "2. 이용 목적: 데모 시연을 위한 식별 및 화면 구성\n" +
                "3. 보유 기간: 데모 종료 후 즉시 폐기\n\n" +
                "본 본문은 실제 정책이 아닌 데모용 더미 텍스트입니다.";
        }

        private static string GetDummyRefundPolicy()
        {
            return
                "[환불정책 - 데모 본문]\n\n" +
                "1. 디지털 콘텐츠는 구매 후 7일 이내 환불 요청이 가능합니다.\n" +
                "2. 이미 사용한 콘텐츠 / 적용한 꾸미기 아이템은 환불 대상에서 제외됩니다.\n" +
                "3. 환불 요청은 [문의하기]를 통해 접수해주세요.\n\n" +
                "본 본문은 실제 정책이 아닌 데모용 더미 텍스트입니다.";
        }

        // ---------------------------------------------------------------------
        // Notice toast
        // ---------------------------------------------------------------------

        private void BuildNoticeToast()
        {
            noticeToastRoot = CreateRectObject("NoticeToast", panelRoot.transform);
            var rootRect = (RectTransform)noticeToastRoot.transform;
            rootRect.anchorMin = new Vector2(0.5f, 0f);
            rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.sizeDelta = new Vector2(640f, 80f);
            rootRect.anchoredPosition = new Vector2(0f, 60f);

            var bg = noticeToastRoot.AddComponent<Image>();
            bg.sprite = roundedRectSprite;
            bg.type = Image.Type.Sliced;
            bg.color = WithAlpha(TextColor, 0.92f);
            bg.raycastTarget = false;

            noticeToastLabel = CreateText(
                noticeToastRoot.transform,
                "Label",
                string.Empty,
                18f,
                FontStyles.Bold,
                Color.white,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.zero,
                0f,
                new Vector2(0.5f, 0.5f));
            var labelRect = noticeToastLabel.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(24f, 8f);
            labelRect.offsetMax = new Vector2(-24f, -8f);

            noticeToastRoot.SetActive(false);
        }

        private void ShowNoticeToast(string message)
        {
            if (noticeToastLabel != null)
            {
                noticeToastLabel.text = message;
            }
            if (noticeToastRoot != null)
            {
                noticeToastRoot.SetActive(true);
            }
        }

        private void HideNoticeToast()
        {
            if (noticeToastRoot != null)
            {
                noticeToastRoot.SetActive(false);
            }
        }

        // ---------------------------------------------------------------------
        // 공용 UI 헬퍼
        // ---------------------------------------------------------------------

        private GameObject BuildTabRow(
            Transform parent,
            out Button leftButton,
            out Image leftIndicator,
            string leftLabel,
            out Button rightButton,
            out Image rightIndicator,
            string rightLabel)
        {
            var row = CreateRectObject("TabRow", parent);
            var rowHorizontal = row.AddComponent<HorizontalLayoutGroup>();
            rowHorizontal.spacing = 8f;
            rowHorizontal.childControlHeight = true;
            rowHorizontal.childControlWidth = true;
            rowHorizontal.childForceExpandWidth = true;
            rowHorizontal.childForceExpandHeight = true;

            leftButton = CreateTabButton(row.transform, leftLabel, out leftIndicator);
            rightButton = CreateTabButton(row.transform, rightLabel, out rightIndicator);
            return row;
        }

        private Button CreateTabButton(Transform parent, string labelText, out Image indicator)
        {
            var tabRoot = CreateRectObject($"Tab_{labelText}", parent);

            var bg = tabRoot.AddComponent<Image>();
            bg.sprite = roundedRectSprite;
            bg.type = Image.Type.Sliced;
            bg.color = SecondaryColor;

            var label = CreateText(
                tabRoot.transform,
                "Label",
                labelText,
                18f,
                FontStyles.Bold,
                TextColor,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.zero,
                0f,
                new Vector2(0.5f, 0.5f));
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            indicator = CreateImage(tabRoot.transform, "Indicator", SelectedTabColor);
            indicator.raycastTarget = false;
            var indicatorRect = indicator.rectTransform;
            indicatorRect.anchorMin = new Vector2(0f, 0f);
            indicatorRect.anchorMax = new Vector2(1f, 0f);
            indicatorRect.pivot = new Vector2(0.5f, 0f);
            indicatorRect.offsetMin = new Vector2(8f, 0f);
            indicatorRect.offsetMax = new Vector2(-8f, 4f);
            indicator.gameObject.SetActive(false);

            var button = tabRoot.AddComponent<Button>();
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = SecondaryColor;
            colors.highlightedColor = Blend(SecondaryColor, Color.white, 0.08f);
            colors.pressedColor = Blend(SecondaryColor, TextColor, 0.06f);
            colors.selectedColor = Blend(SecondaryColor, Color.white, 0.05f);
            colors.disabledColor = WithAlpha(SecondaryColor, 0.5f);
            button.colors = colors;
            return button;
        }

        private void UpdateTabIndicator(Image indicator, bool isSelected)
        {
            if (indicator == null)
            {
                return;
            }
            indicator.gameObject.SetActive(isSelected);
        }

        private void CreateSectionHeader(Transform parent, string title)
        {
            CreateLayoutText(parent, $"Section_{title}", title, 18f, FontStyles.Bold, MutedTextColor, TextAlignmentOptions.Left, 32f);
        }

        private TMP_Text CreateLayoutText(
            Transform parent,
            string objectName,
            string value,
            float fontSize,
            FontStyles fontStyle,
            Color color,
            TextAlignmentOptions alignment,
            float preferredHeight)
        {
            var text = CreateText(parent, objectName, value, fontSize, fontStyle, color, alignment, Vector2.zero, Vector2.zero, 0f, new Vector2(0.5f, 0.5f));
            var rectTransform = text.rectTransform;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;

            var layoutElement = text.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.minHeight = preferredHeight;
            layoutElement.flexibleWidth = 1f;
            return text;
        }

        private TMP_InputField CreateLabeledInput(Transform parent, string objectName, string labelText, string placeholderText, bool isPassword)
        {
            var container = CreateRectObject(objectName, parent);
            ConfigureLayoutHeight(container, 110f);

            var labelRect = CreateText(
                container.transform,
                "Label",
                labelText,
                14f,
                FontStyles.Bold,
                MutedTextColor,
                TextAlignmentOptions.MidlineLeft,
                Vector2.zero,
                new Vector2(360f, 22f),
                0f,
                new Vector2(0f, 1f));
            var labelTextRect = labelRect.rectTransform;
            labelTextRect.anchorMin = new Vector2(0f, 1f);
            labelTextRect.anchorMax = new Vector2(0f, 1f);
            labelTextRect.anchoredPosition = new Vector2(8f, -4f);

            var fieldBackground = CreateImage(container.transform, "Field", InputColor);
            fieldBackground.sprite = roundedRectSprite;
            fieldBackground.type = Image.Type.Sliced;
            var fieldRect = fieldBackground.rectTransform;
            fieldRect.anchorMin = new Vector2(0f, 0f);
            fieldRect.anchorMax = new Vector2(1f, 0f);
            fieldRect.pivot = new Vector2(0.5f, 0f);
            fieldRect.sizeDelta = new Vector2(0f, 68f);
            fieldRect.anchoredPosition = Vector2.zero;

            var outline = fieldBackground.gameObject.AddComponent<Outline>();
            outline.effectColor = WithAlpha(BorderColor, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);

            var input = fieldBackground.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = fieldBackground;
            input.textViewport = CreateViewport(fieldBackground.transform);
            input.contentType = isPassword ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.caretColor = TextColor;
            input.selectionColor = WithAlpha(PrimaryColor, 0.25f);
            input.customCaretColor = true;

            var textComponent = CreateText(input.textViewport, "Text", string.Empty, 18f, FontStyles.Normal, TextColor, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero, 0f, new Vector2(0.5f, 0.5f));
            StretchWithPadding(textComponent.rectTransform, new Vector2(16f, 10f), new Vector2(-16f, -10f));
            textComponent.textWrappingMode = TextWrappingModes.NoWrap;
            textComponent.overflowMode = TextOverflowModes.Ellipsis;

            var placeholder = CreateText(input.textViewport, "Placeholder", placeholderText, 18f, FontStyles.Normal, WithAlpha(MutedTextColor, 0.7f), TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero, 0f, new Vector2(0.5f, 0.5f));
            StretchWithPadding(placeholder.rectTransform, new Vector2(16f, 10f), new Vector2(-16f, -10f));
            placeholder.textWrappingMode = TextWrappingModes.NoWrap;
            placeholder.overflowMode = TextOverflowModes.Ellipsis;

            input.textComponent = textComponent;
            input.placeholder = placeholder;
            return input;
        }

        private GameObject CreateLayoutView(string objectName, Transform parent, float spacing = 14f, RectOffset paddingOverride = null)
        {
            var view = CreateRectObject(objectName, parent);

            var layout = view.AddComponent<VerticalLayoutGroup>();
            layout.padding = paddingOverride ?? new RectOffset(8, 8, 8, 8);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var fitter = view.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return view;
        }

        private void ConfigureLayoutHeight(GameObject target, float height)
        {
            if (target == null)
            {
                return;
            }
            var layoutElement = target.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = target.AddComponent<LayoutElement>();
            }
            layoutElement.preferredHeight = height;
            layoutElement.minHeight = height;
            layoutElement.flexibleWidth = 1f;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                UnityObjectSafeDestroy.Destroy(child.gameObject);
            }
        }

        private void ResetButtonRectForLayout(Button button)
        {
            if (button == null)
            {
                return;
            }
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        private Toggle CreateToggle(Transform parent, string objectName, string labelText)
        {
            var toggleRoot = CreateRectObject(objectName, parent);
            var toggleRect = (RectTransform)toggleRoot.transform;
            toggleRect.sizeDelta = new Vector2(64f, 50f);

            var background = CreateImage(toggleRoot.transform, "Background", ToggleOffColor);
            background.sprite = roundedRectSprite;
            background.type = Image.Type.Sliced;
            var bgRect = background.rectTransform;
            bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.sizeDelta = new Vector2(64f, 36f);
            bgRect.anchoredPosition = Vector2.zero;

            var checkmark = CreateImage(background.transform, "Checkmark", Color.white);
            checkmark.sprite = roundedRectSprite;
            checkmark.type = Image.Type.Sliced;
            var checkRect = checkmark.rectTransform;
            checkRect.anchorMin = new Vector2(0f, 0.5f);
            checkRect.anchorMax = new Vector2(0f, 0.5f);
            checkRect.pivot = new Vector2(0f, 0.5f);
            checkRect.sizeDelta = new Vector2(28f, 28f);
            checkRect.anchoredPosition = new Vector2(4f, 0f);

            var toggle = toggleRoot.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = null;
            toggle.transition = Selectable.Transition.ColorTint;
            var colors = toggle.colors;
            colors.normalColor = ToggleOffColor;
            colors.selectedColor = ToggleOnColor;
            colors.highlightedColor = Blend(ToggleOffColor, Color.white, 0.05f);
            colors.pressedColor = Blend(ToggleOffColor, TextColor, 0.05f);
            colors.disabledColor = WithAlpha(ToggleOffColor, 0.5f);
            toggle.colors = colors;
            toggle.isOn = true;

            toggle.onValueChanged.AddListener(isOn => UpdateToggleAppearance(toggle, background, checkmark, isOn));
            UpdateToggleAppearance(toggle, background, checkmark, toggle.isOn);

            if (!string.IsNullOrEmpty(labelText))
            {
                var label = CreateText(
                    toggleRoot.transform,
                    "Label",
                    labelText,
                    16f,
                    FontStyles.Normal,
                    TextColor,
                    TextAlignmentOptions.MidlineLeft,
                    Vector2.zero,
                    new Vector2(360f, 36f),
                    0f,
                    new Vector2(0f, 0.5f));
                var labelRect = label.rectTransform;
                labelRect.anchorMin = new Vector2(1f, 0.5f);
                labelRect.anchorMax = new Vector2(1f, 0.5f);
                labelRect.anchoredPosition = new Vector2(160f, 0f);
            }

            return toggle;
        }

        private void UpdateToggleAppearance(Toggle toggle, Image background, Image checkmark, bool isOn)
        {
            if (background != null)
            {
                background.color = isOn ? ToggleOnColor : ToggleOffColor;
            }
            if (checkmark != null)
            {
                if (!checkmark.gameObject.activeSelf)
                {
                    checkmark.gameObject.SetActive(true);
                }
                checkmark.enabled = true;
                checkmark.color = Color.white;
                var rect = checkmark.rectTransform;
                rect.anchorMin = isOn ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
                rect.anchorMax = rect.anchorMin;
                rect.pivot = isOn ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
                rect.anchoredPosition = isOn ? new Vector2(-4f, 0f) : new Vector2(4f, 0f);
            }
        }

        private void SetMessage(TMP_Text label, string message, Color color)
        {
            if (label == null)
            {
                return;
            }
            label.text = message ?? string.Empty;
            label.color = color;
        }

        // ---------------------------------------------------------------------
        // 기본 UI 헬퍼 (LoginScreenController 패턴 재사용)
        // ---------------------------------------------------------------------

        private GameObject CreateRectObject(string objectName, Transform parent)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.layer = UiLayer;
            go.transform.SetParent(parent, false);
            return go;
        }

        private Image CreateImage(Transform parent, string objectName, Color color)
        {
            var imageObject = CreateRectObject(objectName, parent);
            var image = imageObject.AddComponent<Image>();
            image.sprite = whiteSprite;
            image.color = color;
            image.type = Image.Type.Simple;
            image.raycastTarget = true;
            return image;
        }

        private Button CreateButton(
            Transform parent,
            string objectName,
            string labelText,
            Color backgroundColor,
            Color labelColor,
            Vector2 anchoredPosition,
            Vector2 size,
            bool outlined,
            Color outlineColor)
        {
            var buttonImage = CreateImage(parent, objectName, backgroundColor);
            buttonImage.sprite = roundedRectSprite;
            buttonImage.type = Image.Type.Sliced;

            var buttonRect = buttonImage.rectTransform;
            buttonRect.anchorMin = new Vector2(0.5f, 1f);
            buttonRect.anchorMax = new Vector2(0.5f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            buttonRect.sizeDelta = size;
            buttonRect.anchoredPosition = anchoredPosition;

            if (outlined)
            {
                var outline = buttonImage.gameObject.AddComponent<Outline>();
                outline.effectColor = WithAlpha(outlineColor, 0.95f);
                outline.effectDistance = new Vector2(1f, -1f);
            }

            var button = buttonImage.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.transition = Selectable.Transition.ColorTint;

            var colors = button.colors;
            colors.normalColor = backgroundColor;
            colors.highlightedColor = Blend(backgroundColor, Color.white, 0.08f);
            colors.pressedColor = outlined ? Blend(backgroundColor, TextColor, 0.06f) : PrimaryPressedColor;
            colors.selectedColor = outlined ? Blend(backgroundColor, Color.white, 0.08f) : PrimaryHighlightColor;
            colors.disabledColor = WithAlpha(backgroundColor, 0.5f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var label = CreateText(buttonImage.transform, "Label", labelText, 18f, FontStyles.Bold, labelColor, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero, 0f, new Vector2(0.5f, 0.5f));
            StretchToParent(label.rectTransform);

            return button;
        }

        private RectTransform CreateViewport(Transform parent)
        {
            var viewportObject = CreateRectObject("TextArea", parent);
            var viewport = (RectTransform)viewportObject.transform;
            StretchToParent(viewport);
            return viewport;
        }

        private TMP_Text CreateText(
            Transform parent,
            string objectName,
            string value,
            float fontSize,
            FontStyles fontStyle,
            Color color,
            TextAlignmentOptions alignment,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            float characterSpacing = 0f,
            Vector2? pivotOverride = null)
        {
            var textObject = CreateRectObject(objectName, parent);
            var rectTransform = (RectTransform)textObject.transform;
            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rectTransform.pivot = pivotOverride ?? new Vector2(0.5f, 1f);
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.anchoredPosition = anchoredPosition;

            var text = textObject.AddComponent<TextMeshProUGUI>();
            ApplyFontToText(text);
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = alignment;
            text.characterSpacing = characterSpacing;
            text.raycastTarget = false;
            return text;
        }

        // ---------------------------------------------------------------------
        // 폰트 / 스프라이트 / 컬러 유틸
        // ---------------------------------------------------------------------

        private void ApplyFontToCanvas()
        {
            if (settingsCanvas == null || fontAsset == null)
            {
                return;
            }

            var tmpTexts = settingsCanvas.GetComponentsInChildren<TMP_Text>(true);
            foreach (var tmpText in tmpTexts)
            {
                ApplyFontToText(tmpText);
            }

            var inputFields = settingsCanvas.GetComponentsInChildren<TMP_InputField>(true);
            foreach (var inputField in inputFields)
            {
                if (inputField == null)
                {
                    continue;
                }
                ApplyFontToText(inputField.textComponent);
                if (inputField.placeholder is TMP_Text placeholderText)
                {
                    ApplyFontToText(placeholderText);
                }
            }
        }

        private void ApplyFontToText(TMP_Text text)
        {
            if (text == null || fontAsset == null)
            {
                return;
            }

            text.font = fontAsset;
            if (fontAsset.material != null)
            {
                text.fontSharedMaterial = fontAsset.material;
            }

            if (fallbackFontAsset != null && text.font != null && text.font != fallbackFontAsset)
            {
                var fallbackTable = text.font.fallbackFontAssetTable;
                if (fallbackTable == null)
                {
                    fallbackTable = new List<TMP_FontAsset>();
                    text.font.fallbackFontAssetTable = fallbackTable;
                }

                if (!fallbackTable.Contains(fallbackFontAsset))
                {
                    fallbackTable.Add(fallbackFontAsset);
                }
            }
        }

        private TMP_FontAsset ResolveFontAsset()
        {
            var preferredFontAsset = FindFontAssetByName(PreferredKoreanFontAssetName);
            if (preferredFontAsset != null)
            {
                fallbackFontAsset = preferredFontAsset;
                return preferredFontAsset;
            }

            preferredFontAsset = FindFontAssetByName(FallbackKoreanFontAssetName);
            if (preferredFontAsset != null)
            {
                fallbackFontAsset = preferredFontAsset;
                return preferredFontAsset;
            }

            fallbackFontAsset = TMP_Settings.defaultFontAsset;

            var existingText = UnityEngine.Object.FindFirstObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include);
            var candidateFontAsset = existingText != null ? existingText.font : TMP_Settings.defaultFontAsset;

            if (TryCreateDynamicFontAsset(candidateFontAsset, out var dynamicFontAsset))
            {
                fallbackFontAsset = candidateFontAsset;
                return dynamicFontAsset;
            }

            if (TryCreateDynamicFontAsset(fallbackFontAsset, out dynamicFontAsset))
            {
                return dynamicFontAsset;
            }

            if (candidateFontAsset != null)
            {
                return candidateFontAsset;
            }

            return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        private TMP_FontAsset FindFontAssetByName(string fontAssetName)
        {
            if (string.IsNullOrWhiteSpace(fontAssetName))
            {
                return null;
            }

            var fontAssets = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (var candidate in fontAssets)
            {
                if (candidate != null && string.Equals(candidate.name, fontAssetName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
            return null;
        }

        private bool TryCreateDynamicFontAsset(TMP_FontAsset sourceFontAsset, out TMP_FontAsset dynamicFontAsset)
        {
            dynamicFontAsset = null;
            if (sourceFontAsset == null || sourceFontAsset.sourceFontFile == null)
            {
                return false;
            }

            try
            {
                dynamicFontAsset = TMP_FontAsset.CreateFontAsset(
                    sourceFontAsset.sourceFontFile,
                    90,
                    9,
                    UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                    1024,
                    1024,
                    AtlasPopulationMode.Dynamic,
                    true);
                if (dynamicFontAsset == null)
                {
                    return false;
                }
                dynamicFontAsset.name = $"{sourceFontAsset.name}_SettingsDynamic";
                dynamicFontAsset.hideFlags = HideFlags.HideAndDontSave;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void WarmupGlyphs(TMP_FontAsset targetFontAsset)
        {
            if (targetFontAsset == null || targetFontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            {
                return;
            }

            var commonText =
                "설정 닫기 뒤로 완료" +
                "계정 결제 알림 피드 설정 이용약관 약관 정책 신고 제재 내역 회원탈퇴 로그아웃" +
                "전체 공개 친구만 공개 비공개" +
                "공간 공개 범위" +
                "수정 사진 변경 닉네임 비밀번호 비밀번호 확인" +
                "결제수단 구매내역 결제수단 추가" +
                "프리미엄 꾸미기 아이템 유료 폰트 테마 패키지" +
                "신고내역 제재내역 처리 중 처리 완료 신고 접수" +
                "이용약관 개인정보처리방침 환불정책" +
                "전체 알림 방명록 추가 방명록 답글 책 추가 좋아요 댓글 포화책 추가" +
                "현재 기능이 제한될 수 있습니다 완료하기" +
                "닉네임을 입력해주세요 비밀번호가 일치하지 않습니다";
            targetFontAsset.TryAddCharacters(commonText, out _);
        }

        private Sprite CreateWhiteSprite()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "SettingsUiWhite";
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            sprite.name = "SettingsUiWhiteSprite";
            return sprite;
        }

        private Sprite CreateRoundedRectSprite(int size, int radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "SettingsUiRounded";
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            var pixels = new Color32[size * size];
            var solid = new Color32(255, 255, 255, 255);
            var transparent = new Color32(255, 255, 255, 0);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    pixels[(y * size) + x] = IsInsideRoundedRect(x, y, size, radius) ? solid : transparent;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var border = new Vector4(radius, radius, radius, radius);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect,
                border);
            sprite.name = "SettingsUiRoundedSprite";
            return sprite;
        }

        private static bool IsInsideRoundedRect(int x, int y, int size, int radius)
        {
            if (x >= radius && x < size - radius)
            {
                return true;
            }
            if (y >= radius && y < size - radius)
            {
                return true;
            }

            var cornerCenterX = x < radius ? radius - 1 : size - radius;
            var cornerCenterY = y < radius ? radius - 1 : size - radius;
            var dx = x - cornerCenterX;
            var dy = y - cornerCenterY;
            return (dx * dx) + (dy * dy) <= radius * radius;
        }

        private GameObject FindExistingCanvasObject()
        {
            var scene = gameObject.scene;
            if (!scene.IsValid())
            {
                return null;
            }

            foreach (var rootObject in scene.GetRootGameObjects())
            {
                if (rootObject != null && rootObject.name == CanvasName)
                {
                    return rootObject;
                }
            }
            return null;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
        }

        private static void StretchWithPadding(RectTransform rectTransform, Vector2 minOffset, Vector2 maxOffset)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = minOffset;
            rectTransform.offsetMax = maxOffset;
            rectTransform.anchoredPosition = Vector2.zero;
        }

        private static Color Blend(Color from, Color to, float t)
        {
            return Color.Lerp(from, to, Mathf.Clamp01(t));
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static Color Hex(string htmlColor)
        {
            if (ColorUtility.TryParseHtmlString(htmlColor, out var color))
            {
                return color;
            }
            return Color.magenta;
        }

        private void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
            eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
