using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BookshelfPorting.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace App.Bootstrap
{
    [DefaultExecutionOrder(-880)]
    [DisallowMultipleComponent]
    public sealed class OnboardingScreenController : MonoBehaviour
    {
        private const string CanvasName = "OnboardingCanvas";
        private const string PreferredKoreanFontAssetName = "NotoSansKR-Regular";
        private const string FallbackKoreanFontAssetName = "NOTOSANSKR SDF";
        private const float CardWidth = 920f;
        private const float CardHeight = 980f;
        private const float PrimaryControlHeight = 56f;
        private const float ProfilePreviewSize = 176f;
        private const float ThemeButtonWidth = 152f;
        private const int UiLayer = 5;
        private const int CanvasSortingOrder = 5100;
        private const float MainSceneLoadTimeoutSeconds = 8f;
        private const float MainViewReadyTimeoutSeconds = 8f;
        private const float BookshelfFocusTimeoutSeconds = 8f;
        private const float BookshelfFocusRetryIntervalSeconds = 0.75f;
        private const float MainScenePreviewDelaySeconds = 0.7f;
        private const float MainScenePreviewCoverAlpha = 0.62f;
        private static readonly bool EnableDebugLogs = false;

        private static readonly string[] ThemeNames =
        {
            "내추럴",
            "클래식",
            "모던",
            "빈티지",
            "파스텔"
        };

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
        private static readonly Color ErrorColor = Hex("#C14E4E");
        private static readonly Color SuccessColor = Hex("#2F8B57");
        private static readonly Color InputColor = Hex("#FDF9F3");
        private static readonly Color PreviewAccentColor = Hex("#EFE6D6");

        [SerializeField] private int currentStep = 1;

        private Canvas onboardingCanvas;
        private TMP_FontAsset fontAsset;
        private TMP_FontAsset fallbackFontAsset;
        private Sprite whiteSprite;
        private Sprite roundedRectSprite;

        private GameObject backgroundRoot;
        private GameObject cardRoot;
        private GameObject step1Panel;
        private GameObject step2Panel;
        private GameObject step3Panel;
        private GameObject completionModalRoot;

        private Image profilePreviewImage;
        private TMP_InputField nicknameInput;
        private TMP_InputField introInput;
        private TMP_Text step1MessageLabel;
        private Button step1NextButton;

        private TMP_Text step2MessageLabel;
        private Image themePreviewImage;
        private TMP_Text themePreviewTitleLabel;
        private TMP_Text themePreviewCaptionLabel;

        private TMP_Text step3OverlayStatusLabel;
        private GameObject step3LoadingCover;
        private Image step3LoadingCoverImage;
        private GameObject step3LoadingContentRoot;
        private GameObject step3StatusRoot;
        private TMP_Text step3LoadingMessageLabel;

        private readonly Dictionary<string, Button> themeButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly Dictionary<string, Image> themeButtonImages = new Dictionary<string, Image>(StringComparer.Ordinal);
        private readonly Dictionary<string, Outline> themeButtonOutlines = new Dictionary<string, Outline>(StringComparer.Ordinal);
        private readonly Dictionary<string, TMP_Text> themeButtonLabels = new Dictionary<string, TMP_Text>(StringComparer.Ordinal);

        private string selectedTheme = string.Empty;
        private bool nicknameChecked;
        private Texture2D profileTexture;
        private Sprite profileSprite;
        private bool isInitialized;
        private bool hasEnabledCrossScenePersistence;
        private bool isMainViewReady;
        private bool isBookshelfFocusRequested;
        private bool isBookshelfFocused;
        private Coroutine step3TransitionRoutine;

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
            WarmupOnboardingGlyphs(fontAsset);

            BuildOnboardingCanvas();
            ApplyFontToOnboardingCanvas();
            EnsureEventSystem();
            SetCanvasVisible(false);
            ResetFlowState();
            ShowStep1();
            Canvas.ForceUpdateCanvases();
        }

        private void Awake()
        {
            InitializeIfNeeded();
        }

        public void StartOnboarding()
        {
            InitializeIfNeeded();
            ResetFlowState();
            GetComponent<LoginScreenController>()?.SetCanvasVisible(false);
            SetCanvasVisible(true);
            ShowStep1();
        }

        public void ShowStep(int step)
        {
            InitializeIfNeeded();

            currentStep = Mathf.Clamp(step, 1, 3);
            var isStep3Overlay = currentStep == 3;

            SetStepPresentationMode(isStep3Overlay);

            if (step1Panel != null)
            {
                step1Panel.SetActive(currentStep == 1);
            }

            if (step2Panel != null)
            {
                step2Panel.SetActive(currentStep == 2);
            }

            if (step3Panel != null)
            {
                step3Panel.SetActive(isStep3Overlay);
            }

            if (currentStep == 1)
            {
                RefreshStep1ValidationUi();
            }

            if (currentStep == 2)
            {
                UpdateThemePreview(selectedTheme);
            }

            if (isStep3Overlay)
            {
                EnterBookshelfFocusForOnboarding();
            }
            else
            {
                CancelStep3Transition();
            }

            Canvas.ForceUpdateCanvases();
        }

        public void ShowStep1()
        {
            ShowStep(1);
        }

        public void ShowStep2()
        {
            ShowStep(2);
        }

        public void ShowStep3()
        {
            ShowStep(3);
        }

        public void CompleteOnboarding()
        {
            LogDebug("Onboarding completed");

            CancelStep3Transition();
            SetCanvasVisible(false);

            if (onboardingCanvas != null)
            {
                Destroy(onboardingCanvas.gameObject);
                onboardingCanvas = null;
            }

            var loginScreenController = GetComponent<LoginScreenController>();
            if (loginScreenController != null)
            {
                Destroy(loginScreenController);
            }

            Destroy(gameObject);
        }

        private static void LogDebug(string message)
        {
            if (!EnableDebugLogs)
            {
                return;
            }

            Debug.Log(message);
        }

        private void SetCanvasVisible(bool isVisible)
        {
            if (onboardingCanvas == null)
            {
                return;
            }

            onboardingCanvas.gameObject.SetActive(isVisible);
        }

        private void ResetFlowState()
        {
            nicknameChecked = false;
            selectedTheme = string.Empty;
            currentStep = 1;
            ResetStep3FlowState();

            if (nicknameInput != null)
            {
                nicknameInput.text = string.Empty;
            }

            if (introInput != null)
            {
                introInput.text = string.Empty;
            }

            SetMessage(step2MessageLabel, string.Empty, ErrorColor);
            SetStep1Feedback(string.Empty, MutedTextColor, false);
            ResetProfilePreview();
            UpdateThemeSelectionVisuals();
            UpdateThemePreview(string.Empty);
            SetStep3OverlayStatus("메인 화면과 책장 포커스 화면으로 이동할 준비가 되었습니다.");
            SetStepPresentationMode(false);
        }

        private void BuildOnboardingCanvas()
        {
            var existingCanvasObject = FindExistingCanvasObject();
            if (existingCanvasObject != null)
            {
                Destroy(existingCanvasObject);
            }

            var canvasObject = new GameObject(CanvasName, typeof(RectTransform));
            canvasObject.layer = UiLayer;
            SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene);

            onboardingCanvas = canvasObject.AddComponent<Canvas>();
            onboardingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            onboardingCanvas.overrideSorting = true;
            onboardingCanvas.sortingOrder = CanvasSortingOrder;
            onboardingCanvas.pixelPerfect = false;
            onboardingCanvas.targetDisplay = 0;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
            canvasRect.anchoredPosition = Vector2.zero;
            canvasRect.sizeDelta = Vector2.zero;

            var background = CreateImage(canvasObject.transform, "Background", BackgroundColor);
            StretchToParent(background.rectTransform);
            backgroundRoot = background.gameObject;

            CreateAccentBlob(background.transform, "TopAccent", new Vector2(0.84f, 0.88f), new Vector2(280f, 280f));
            CreateAccentBlob(background.transform, "BottomAccent", new Vector2(0.18f, 0.18f), new Vector2(240f, 240f));

            var card = CreateImage(background.transform, "OnboardingCard", CardColor);
            card.sprite = roundedRectSprite;
            card.type = Image.Type.Sliced;
            cardRoot = card.gameObject;

            var cardRect = card.rectTransform;
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(CardWidth, CardHeight);
            cardRect.anchoredPosition = Vector2.zero;

            var cardShadow = card.gameObject.AddComponent<Shadow>();
            cardShadow.effectColor = WithAlpha(TextColor, 0.15f);
            cardShadow.effectDistance = new Vector2(0f, -14f);

            var contentRoot = CreateRectObject("ContentRoot", card.transform);
            StretchWithPadding((RectTransform)contentRoot.transform, new Vector2(56f, 52f), new Vector2(-56f, -52f));

            step1Panel = BuildStep1Panel(contentRoot.transform);
            step2Panel = BuildStep2Panel(contentRoot.transform);
            step3Panel = BuildStep3Panel(canvasObject.transform);
            completionModalRoot = BuildCompletionModal(canvasObject.transform);
        }

        private GameObject BuildCompletionModal(Transform parent)
        {
            var modalRoot = CreateRectObject("CompletionModalRoot", parent);
            StretchToParent((RectTransform)modalRoot.transform);

            var dim = CreateImage(modalRoot.transform, "Dim", new Color(0f, 0f, 0f, 0.55f));
            StretchToParent(dim.rectTransform);
            // Block clicks behind the modal so only the 확인 button is interactable.
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;

            var card = CreateImage(modalRoot.transform, "Card", CardColor);
            card.sprite = roundedRectSprite;
            card.type = Image.Type.Sliced;
            var cardRect = card.rectTransform;
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(640f, 360f);
            cardRect.anchoredPosition = Vector2.zero;

            var cardShadow = card.gameObject.AddComponent<Shadow>();
            cardShadow.effectColor = WithAlpha(TextColor, 0.18f);
            cardShadow.effectDistance = new Vector2(0f, -10f);

            var title = CreateText(
                card.transform,
                "Title",
                "온보딩이 완료되었습니다",
                26f,
                FontStyles.Bold,
                TextColor,
                TextAlignmentOptions.Center,
                new Vector2(0f, -56f),
                new Vector2(540f, 40f));

            var body = CreateText(
                card.transform,
                "Body",
                "이제 나만의 책장을 자유롭게 꾸며보세요.",
                18f,
                FontStyles.Normal,
                MutedTextColor,
                TextAlignmentOptions.Center,
                new Vector2(0f, -148f),
                new Vector2(540f, 60f));

            var confirmButton = CreateButton(
                card.transform,
                "ConfirmButton",
                "확인",
                PrimaryColor,
                Color.white,
                new Vector2(0f, -284f),
                new Vector2(280f, PrimaryControlHeight),
                false,
                BorderColor);
            confirmButton.onClick.AddListener(OnCompletionConfirmClicked);

            modalRoot.SetActive(false);
            return modalRoot;
        }

        private void ShowCompletionModal()
        {
            if (completionModalRoot == null)
            {
                return;
            }

            // Push to the front so it visually overlays the Step 3 navigation card.
            completionModalRoot.transform.SetAsLastSibling();
            completionModalRoot.SetActive(true);
        }

        private void HideCompletionModal()
        {
            if (completionModalRoot != null)
            {
                completionModalRoot.SetActive(false);
            }
        }

        private void OnCompletionConfirmClicked()
        {
            HideCompletionModal();

            // Return the main-scene camera to the new MainView default. This must run
            // before CompleteOnboarding's tear-down because the onboarding GameObject —
            // and this controller — get destroyed there.
            if (TryGetBookshelfExperienceManager(out var experienceManager) && experienceManager != null)
            {
                experienceManager.FocusMainView();
            }

            CompleteOnboarding();
        }

        private GameObject BuildStep1Panel(Transform parent)
        {
            var panel = CreateRectObject("Step1_ProfilePanel", parent);
            StretchToParent((RectTransform)panel.transform);

            var column = CreateColumn(panel.transform, "Layout", 18f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
            StretchToParent(column);

            CreateSectionText(column, "StepCaption", "STEP 1", 14f, FontStyles.Bold, MutedTextColor, TextAlignmentOptions.Center, 28f, 2f);
            CreateSectionText(column, "Title", "프로필을 설정해주세요", 32f, FontStyles.Bold, TextColor, TextAlignmentOptions.Center, 44f);
            CreateSectionText(column, "Subtitle", "프로필 이미지, 닉네임, 한줄소개를 먼저 입력합니다.", 16f, FontStyles.Normal, MutedTextColor, TextAlignmentOptions.Center, 28f);

            CreateSpacer(column, 12f);

            var profileFrame = CreateImage(column, "ProfileFrame", InputColor);
            profileFrame.sprite = roundedRectSprite;
            profileFrame.type = Image.Type.Sliced;
            profileFrame.raycastTarget = false;

            var profileLayout = profileFrame.gameObject.AddComponent<LayoutElement>();
            profileLayout.preferredWidth = ProfilePreviewSize;
            profileLayout.preferredHeight = ProfilePreviewSize;
            profileLayout.minWidth = ProfilePreviewSize;
            profileLayout.minHeight = ProfilePreviewSize;

            var profileOutline = profileFrame.gameObject.AddComponent<Outline>();
            profileOutline.effectColor = WithAlpha(BorderColor, 0.95f);
            profileOutline.effectDistance = new Vector2(1f, -1f);

            profilePreviewImage = profileFrame;
            profilePreviewImage.preserveAspect = true;

            CreateText(
                profileFrame.transform,
                "ProfilePlaceholder",
                "프로필 이미지",
                18f,
                FontStyles.Bold,
                MutedTextColor,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.zero);

            var photoButtonRow = CreateRow(column, "PhotoButtonRow", 0f, TextAnchor.MiddleCenter);
            var photoButton = CreateFixedButton(photoButtonRow, "AddPhotoButton", "사진 추가", SecondaryColor, TextColor, true, BorderColor, new Vector2(220f, PrimaryControlHeight));
            photoButton.onClick.AddListener(OnAddPhotoButtonClicked);

            CreateSpacer(column, 12f);

            CreateSectionText(column, "NicknameLabel", "닉네임", 15f, FontStyles.Bold, MutedTextColor, TextAlignmentOptions.Left, 24f);

            var nicknameRow = CreateRow(column, "NicknameRow", 14f, TextAnchor.MiddleCenter);
            nicknameInput = CreateFlexibleInput(nicknameRow, "NicknameInputField", "닉네임을 입력하세요", false);
            nicknameInput.onValueChanged.AddListener(OnNicknameChanged);

            var duplicateButton = CreateFixedButton(nicknameRow, "DuplicateCheckButton", "중복확인", PrimaryColor, Color.white, false, BorderColor, new Vector2(180f, PrimaryControlHeight));
            duplicateButton.onClick.AddListener(OnDuplicateCheckButtonClicked);

            step1MessageLabel = CreateSectionText(column, "Step1Message", string.Empty, 15f, FontStyles.Normal, MutedTextColor, TextAlignmentOptions.Left, 28f);

            CreateSectionText(column, "IntroLabel", "한줄소개", 15f, FontStyles.Bold, MutedTextColor, TextAlignmentOptions.Left, 24f);
            introInput = CreateFlexibleInput(column, "IntroInputField", "한줄소개를 입력하세요", false);

            CreateFlexibleSpacer(column);

            var navigationRow = CreateRow(column, "Step1NavigationRow", 14f, TextAnchor.MiddleRight);
            CreateFlexibleSpacer(navigationRow);
            step1NextButton = CreateFixedButton(navigationRow, "NextStepButton", "다음 단계", PrimaryColor, Color.white, false, BorderColor, new Vector2(220f, PrimaryControlHeight));
            step1NextButton.onClick.AddListener(OnStep1NextButtonClicked);
            step1NextButton.gameObject.SetActive(false);

            return panel;
        }

        private GameObject BuildStep2Panel(Transform parent)
        {
            var panel = CreateRectObject("Step2_ThemePanel", parent);
            StretchToParent((RectTransform)panel.transform);

            var column = CreateColumn(panel.transform, "Layout", 18f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
            StretchToParent(column);

            CreateSectionText(column, "StepCaption", "STEP 2", 14f, FontStyles.Bold, MutedTextColor, TextAlignmentOptions.Center, 28f, 2f);
            CreateSectionText(column, "Title", "테마를 선택해주세요", 32f, FontStyles.Bold, TextColor, TextAlignmentOptions.Center, 44f);
            CreateSectionText(column, "Subtitle", "방과 책장의 분위기를 먼저 정해둘 수 있습니다.", 16f, FontStyles.Normal, MutedTextColor, TextAlignmentOptions.Center, 28f);

            CreateSpacer(column, 8f);

            var randomRow = CreateRow(column, "RandomRow", 0f, TextAnchor.MiddleCenter);
            var randomButton = CreateFixedButton(randomRow, "RandomThemeButton", "랜덤", SecondaryColor, TextColor, true, BorderColor, new Vector2(220f, PrimaryControlHeight));
            randomButton.onClick.AddListener(OnRandomThemeButtonClicked);

            CreateSpacer(column, 8f);

            themePreviewImage = CreateImage(column, "ThemePreviewImage", PreviewAccentColor);
            themePreviewImage.sprite = roundedRectSprite;
            themePreviewImage.type = Image.Type.Sliced;
            themePreviewImage.raycastTarget = false;

            var previewLayout = themePreviewImage.gameObject.AddComponent<LayoutElement>();
            previewLayout.preferredHeight = 300f;
            previewLayout.minHeight = 300f;
            previewLayout.flexibleWidth = 1f;

            var previewOutline = themePreviewImage.gameObject.AddComponent<Outline>();
            previewOutline.effectColor = WithAlpha(BorderColor, 0.92f);
            previewOutline.effectDistance = new Vector2(1f, -1f);

            var previewContent = CreateRectObject("ThemePreviewContent", themePreviewImage.transform);
            StretchWithPadding((RectTransform)previewContent.transform, new Vector2(32f, 32f), new Vector2(-32f, -32f));

            var previewColumn = CreateColumn(previewContent.transform, "Layout", 14f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
            StretchToParent(previewColumn);

            themePreviewTitleLabel = CreateSectionText(previewColumn, "ThemePreviewTitle", "테마 미리보기", 26f, FontStyles.Bold, TextColor, TextAlignmentOptions.Center, 40f);
            themePreviewCaptionLabel = CreateSectionText(previewColumn, "ThemePreviewCaption", "테마를 선택하면 이 영역이 변경됩니다.", 16f, FontStyles.Normal, MutedTextColor, TextAlignmentOptions.Center, 30f);

            var previewShelf = CreateImage(previewColumn, "PreviewShelf", WithAlpha(CardColor, 0.78f));
            previewShelf.sprite = roundedRectSprite;
            previewShelf.type = Image.Type.Sliced;
            previewShelf.raycastTarget = false;
            var previewShelfLayout = previewShelf.gameObject.AddComponent<LayoutElement>();
            previewShelfLayout.preferredHeight = 150f;
            previewShelfLayout.minHeight = 150f;
            previewShelfLayout.flexibleWidth = 1f;

            CreatePreviewAccentBar(previewShelf.transform, "ShelfTop", new Vector2(0.5f, 0.32f), new Vector2(460f, 12f));
            CreatePreviewAccentBar(previewShelf.transform, "ShelfBottom", new Vector2(0.5f, 0.68f), new Vector2(460f, 12f));
            CreatePreviewBook(previewShelf.transform, "PreviewBookA", new Vector2(0.28f, 0.25f), Hex("#CFA47C"));
            CreatePreviewBook(previewShelf.transform, "PreviewBookB", new Vector2(0.42f, 0.25f), Hex("#7C93B7"));
            CreatePreviewBook(previewShelf.transform, "PreviewBookC", new Vector2(0.56f, 0.25f), Hex("#A87F7F"));
            CreatePreviewBook(previewShelf.transform, "PreviewBookD", new Vector2(0.38f, 0.62f), Hex("#91A27A"));
            CreatePreviewBook(previewShelf.transform, "PreviewBookE", new Vector2(0.52f, 0.62f), Hex("#D0B373"));

            CreateSpacer(column, 8f);

            var themeButtonRow = CreateRow(column, "ThemeButtonRow", 10f, TextAnchor.MiddleCenter);

            themeButtons.Clear();
            themeButtonImages.Clear();
            themeButtonOutlines.Clear();
            themeButtonLabels.Clear();

            foreach (var themeName in ThemeNames)
            {
                var button = CreateThemeButton(themeButtonRow, themeName);
                themeButtons[themeName] = button;
            }

            step2MessageLabel = CreateSectionText(column, "Step2Message", string.Empty, 15f, FontStyles.Normal, ErrorColor, TextAlignmentOptions.Center, 28f);

            CreateFlexibleSpacer(column);

            var navigationRow = CreateRow(column, "Step2NavigationRow", 14f, TextAnchor.MiddleCenter);
            var previousButton = CreateFixedButton(navigationRow, "PreviousStepButton", "이전 단계", SecondaryColor, TextColor, true, BorderColor, new Vector2(220f, PrimaryControlHeight));
            previousButton.onClick.AddListener(ShowStep1);
            CreateFlexibleSpacer(navigationRow);
            var nextButton = CreateFixedButton(navigationRow, "NextStepButton", "다음 단계", PrimaryColor, Color.white, false, BorderColor, new Vector2(220f, PrimaryControlHeight));
            nextButton.onClick.AddListener(OnStep2NextButtonClicked);

            UpdateThemeSelectionVisuals();
            UpdateThemePreview(string.Empty);

            return panel;
        }

        private GameObject BuildStep3Panel(Transform parent)
        {
            var panel = CreateRectObject("Step3_BookshelfFocusPanel", parent);
            StretchToParent((RectTransform)panel.transform);

            var loadingCover = CreateImage(panel.transform, "LoadingCover", BackgroundColor);
            loadingCover.sprite = whiteSprite;
            loadingCover.type = Image.Type.Simple;
            StretchToParent(loadingCover.rectTransform);
            step3LoadingCover = loadingCover.gameObject;
            step3LoadingCoverImage = loadingCover;

            var loadingContent = CreateRectObject("LoadingContent", loadingCover.transform);
            StretchToParent((RectTransform)loadingContent.transform);
            step3LoadingContentRoot = loadingContent;

            var loadingCard = CreateImage(loadingContent.transform, "LoadingCard", WithAlpha(CardColor, 0.96f));
            loadingCard.sprite = roundedRectSprite;
            loadingCard.type = Image.Type.Sliced;
            loadingCard.raycastTarget = false;

            var loadingCardRect = loadingCard.rectTransform;
            loadingCardRect.anchorMin = new Vector2(0.5f, 0.5f);
            loadingCardRect.anchorMax = new Vector2(0.5f, 0.5f);
            loadingCardRect.pivot = new Vector2(0.5f, 0.5f);
            loadingCardRect.sizeDelta = new Vector2(560f, 220f);
            loadingCardRect.anchoredPosition = new Vector2(0f, -12f);

            var loadingCardShadow = loadingCard.gameObject.AddComponent<Shadow>();
            loadingCardShadow.effectColor = WithAlpha(TextColor, 0.12f);
            loadingCardShadow.effectDistance = new Vector2(0f, -10f);

            var loadingColumn = CreateColumn(loadingCard.transform, "Layout", 14f, new RectOffset(28, 28, 28, 28), TextAnchor.MiddleCenter);
            StretchToParent(loadingColumn);

            CreateSectionText(loadingColumn, "LoadingTitle", "메인 화면으로 이동 중입니다.", 28f, FontStyles.Bold, TextColor, TextAlignmentOptions.Center, 40f);
            step3LoadingMessageLabel = CreateSectionText(loadingColumn, "LoadingMessage", "책장 포커스 화면을 준비하고 있습니다.", 17f, FontStyles.Normal, MutedTextColor, TextAlignmentOptions.Center, 56f);

            var loadingTitleLabel = loadingCard.transform.Find("Layout/LoadingTitle")?.GetComponent<TMP_Text>();
            if (loadingTitleLabel != null)
            {
                loadingTitleLabel.text = "책장 준비 중입니다.";
            }

            if (step3LoadingMessageLabel != null)
            {
                step3LoadingMessageLabel.text = "나만의 책장 화면으로 이동하고 있어요.";
            }

            var statusChip = CreateImage(panel.transform, "StatusChip", WithAlpha(CardColor, 0.95f));
            statusChip.sprite = roundedRectSprite;
            statusChip.type = Image.Type.Sliced;
            step3StatusRoot = statusChip.gameObject;

            var statusChipRect = statusChip.rectTransform;
            statusChipRect.anchorMin = new Vector2(0.5f, 1f);
            statusChipRect.anchorMax = new Vector2(0.5f, 1f);
            statusChipRect.pivot = new Vector2(0.5f, 1f);
            statusChipRect.sizeDelta = new Vector2(600f, 82f);
            statusChipRect.anchoredPosition = new Vector2(0f, -36f);

            var statusShadow = statusChip.gameObject.AddComponent<Shadow>();
            statusShadow.effectColor = WithAlpha(TextColor, 0.12f);
            statusShadow.effectDistance = new Vector2(0f, -8f);

            step3OverlayStatusLabel = CreateText(
                statusChip.transform,
                "StatusLabel",
                "메인 화면과 책장 포커스 화면으로 이동할 준비가 되었습니다.",
                20f,
                FontStyles.Bold,
                TextColor,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.zero);
            StretchToParent(step3OverlayStatusLabel.rectTransform);

            var navigationCard = CreateImage(panel.transform, "NavigationCard", WithAlpha(CardColor, 0.97f));
            navigationCard.sprite = roundedRectSprite;
            navigationCard.type = Image.Type.Sliced;

            var navigationRect = navigationCard.rectTransform;
            navigationRect.anchorMin = new Vector2(0.5f, 0f);
            navigationRect.anchorMax = new Vector2(0.5f, 0f);
            navigationRect.pivot = new Vector2(0.5f, 0f);
            navigationRect.sizeDelta = new Vector2(760f, 120f);
            navigationRect.anchoredPosition = new Vector2(0f, 36f);

            var navigationShadow = navigationCard.gameObject.AddComponent<Shadow>();
            navigationShadow.effectColor = WithAlpha(TextColor, 0.12f);
            navigationShadow.effectDistance = new Vector2(0f, -10f);

            var navigationRow = CreateRow(navigationCard.transform, "NavigationRow", 18f, TextAnchor.MiddleCenter);
            StretchWithPadding((RectTransform)navigationRow.transform, new Vector2(24f, 18f), new Vector2(-24f, -18f));

            var previousButton = CreateFixedButton(navigationRow, "PreviousStepButton", "이전 단계", SecondaryColor, TextColor, true, BorderColor, new Vector2(220f, PrimaryControlHeight));
            previousButton.onClick.AddListener(ShowStep2);
            CreateFlexibleSpacer(navigationRow);
            var completeButton = CreateFixedButton(navigationRow, "CompleteButton", "완료", PrimaryColor, Color.white, false, BorderColor, new Vector2(220f, PrimaryControlHeight));
            completeButton.onClick.AddListener(ShowCompletionModal);

            SetStep3LoadingPanelVisible(true);
            SetStep3StatusVisible(false);
            SetStep3LoadingCoverVisible(false);
            panel.SetActive(false);
            return panel;
        }

        private void CreatePreviewAccentBar(Transform parent, string objectName, Vector2 anchor, Vector2 size)
        {
            var image = CreateImage(parent, objectName, WithAlpha(BorderColor, 0.95f));
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }

        private void CreatePreviewBook(Transform parent, string objectName, Vector2 anchor, Color color)
        {
            var image = CreateImage(parent, objectName, color);
            image.sprite = roundedRectSprite;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(34f, 84f);
            rect.anchoredPosition = Vector2.zero;
        }

        private void OnAddPhotoButtonClicked()
        {
            if (!TryOpenImageFilePanel(out var selectedPath))
            {
                return;
            }

            if (!TryLoadProfilePreview(selectedPath))
            {
                Debug.LogWarning("[OnboardingDemo] Failed to load selected profile image.");
            }
        }

        private void OnDuplicateCheckButtonClicked()
        {
            var nickname = nicknameInput != null ? nicknameInput.text.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(nickname))
            {
                nicknameChecked = false;
                SetStep1Feedback("닉네임을 입력해주세요.", ErrorColor, false);
                return;
            }

            nicknameChecked = true;
            SetStep1Feedback("사용 가능한 닉네임입니다.", SuccessColor, true);
                LogDebug($"[OnboardingDemo] Nickname available: '{nickname}'.");
        }

        private void OnNicknameChanged(string _)
        {
            nicknameChecked = false;
            SetStep1Feedback("닉네임 중복확인을 해주세요.", MutedTextColor, false);
        }

        private void OnStep1NextButtonClicked()
        {
            var nickname = nicknameInput != null ? nicknameInput.text.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(nickname))
            {
                SetStep1Feedback("닉네임을 입력해주세요.", ErrorColor, false);
                return;
            }

            if (!nicknameChecked)
            {
                SetStep1Feedback("닉네임 중복확인을 해주세요.", ErrorColor, false);
                return;
            }

            ShowStep2();
        }

        private void OnRandomThemeButtonClicked()
        {
            if (ThemeNames.Length == 0)
            {
                return;
            }

            var themeName = ThemeNames[UnityEngine.Random.Range(0, ThemeNames.Length)];
            SelectTheme(themeName);
            LogDebug($"[OnboardingDemo] Random theme selected: '{GetThemeDebugName(themeName)}'.");
        }

        private void OnStep2NextButtonClicked()
        {
            if (string.IsNullOrWhiteSpace(selectedTheme))
            {
                SetMessage(step2MessageLabel, "테마를 선택해주세요.", ErrorColor);
                return;
            }

            SetMessage(step2MessageLabel, string.Empty, ErrorColor);
            ShowStep3();
        }

        private void SelectTheme(string themeName)
        {
            selectedTheme = themeName ?? string.Empty;
            SetMessage(step2MessageLabel, string.Empty, ErrorColor);
            UpdateThemeSelectionVisuals();
            UpdateThemePreview(selectedTheme);

            if (!string.IsNullOrWhiteSpace(selectedTheme))
            {
                LogDebug($"Selected theme: {GetThemeDebugName(selectedTheme)}");
            }
        }

        private void RefreshStep1ValidationUi()
        {
            if (step1NextButton == null)
            {
                return;
            }

            step1NextButton.gameObject.SetActive(nicknameChecked);
        }

        private void SetStep1Feedback(string message, Color color, bool showNextButton)
        {
            SetMessage(step1MessageLabel, message, color);
            if (step1NextButton != null)
            {
                step1NextButton.gameObject.SetActive(showNextButton);
            }
        }

        private void UpdateThemeSelectionVisuals()
        {
            foreach (var entry in themeButtonImages)
            {
                var themeName = entry.Key;
                var buttonImage = entry.Value;
                if (buttonImage == null)
                {
                    continue;
                }

                var isSelected = string.Equals(selectedTheme, themeName, StringComparison.Ordinal);
                buttonImage.color = isSelected ? PrimaryColor : SecondaryColor;

                if (themeButtonOutlines.TryGetValue(themeName, out var outline) && outline != null)
                {
                    outline.effectColor = isSelected ? WithAlpha(PrimaryColor, 1f) : WithAlpha(BorderColor, 0.95f);
                }

                if (themeButtonLabels.TryGetValue(themeName, out var label) && label != null)
                {
                    label.color = isSelected ? Color.white : TextColor;
                }
            }
        }

        private Button CreateThemeButton(Transform parent, string themeName)
        {
            var button = CreateFixedButton(parent, $"{themeName}Button", themeName, SecondaryColor, TextColor, true, BorderColor, new Vector2(ThemeButtonWidth, 52f));
            button.onClick.AddListener(() => SelectTheme(themeName));

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                themeButtonImages[themeName] = image;
            }

            if (button.TryGetComponent<Outline>(out var outline))
            {
                themeButtonOutlines[themeName] = outline;
            }

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                themeButtonLabels[themeName] = label;
            }

            return button;
        }

        private void UpdateThemePreview(string themeName)
        {
            if (themePreviewImage == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(themeName))
            {
                themePreviewImage.color = PreviewAccentColor;
                SetMessage(themePreviewTitleLabel, "테마 미리보기", TextColor);
                SetMessage(themePreviewCaptionLabel, "테마를 선택하면 이 영역이 변경됩니다.", MutedTextColor);
                return;
            }

            themePreviewImage.color = GetThemePreviewColor(themeName);
            SetMessage(themePreviewTitleLabel, $"{themeName} 테마", TextColor);
            SetMessage(themePreviewCaptionLabel, GetThemePreviewCaption(themeName), MutedTextColor);
        }

        private static string GetThemeDebugName(string themeName)
        {
            switch (themeName)
            {
                case "내추럴":
                    return "Natural";
                case "클래식":
                    return "Classic";
                case "모던":
                    return "Modern";
                case "빈티지":
                    return "Vintage";
                case "파스텔":
                    return "Pastel";
                default:
                    return "Unknown";
            }
        }

        private static string GetThemePreviewCaption(string themeName)
        {
            switch (themeName)
            {
                case "내추럴":
                    return "따뜻한 우드 톤과 차분한 분위기를 가진 공간입니다.";
                case "클래식":
                    return "짙은 컬러와 균형감 있는 전통적인 분위기입니다.";
                case "모던":
                    return "정리된 선과 선명한 대비가 중심인 현대적인 분위기입니다.";
                case "빈티지":
                    return "세월감 있는 소재와 부드러운 채도가 어우러진 분위기입니다.";
                case "파스텔":
                    return "밝고 부드러운 컬러감이 강조된 가벼운 분위기입니다.";
                default:
                    return "테마를 선택하면 이 영역이 변경됩니다.";
            }
        }

        private static Color GetThemePreviewColor(string themeName)
        {
            switch (themeName)
            {
                case "내추럴":
                    return Hex("#E5D5BE");
                case "클래식":
                    return Hex("#D2C5B2");
                case "모던":
                    return Hex("#D6D9DF");
                case "빈티지":
                    return Hex("#E0D0C4");
                case "파스텔":
                    return Hex("#F1E4EC");
                default:
                    return PreviewAccentColor;
            }
        }

        private void SetStepPresentationMode(bool isStep3Overlay)
        {
            if (backgroundRoot != null)
            {
                backgroundRoot.SetActive(!isStep3Overlay);
            }

            if (cardRoot != null)
            {
                cardRoot.SetActive(!isStep3Overlay);
            }

            if (step3Panel != null)
            {
                step3Panel.SetActive(isStep3Overlay);
            }

            SetStep3LoadingPanelVisible(true);
            SetStep3StatusVisible(false);

            if (!isStep3Overlay)
            {
                SetStep3LoadingPanelVisible(false);
                SetStep3LoadingCoverVisible(false);
            }
        }

        private void SetStep3OverlayStatus(string message)
        {
            SetMessage(step3OverlayStatusLabel, message, TextColor);
        }

        private void SetStep3LoadingCoverVisible(bool isVisible)
        {
            if (step3LoadingCover == null)
            {
                return;
            }

            step3LoadingCover.SetActive(isVisible);
        }

        private void SetStep3LoadingCoverAlpha(float alpha)
        {
            if (step3LoadingCoverImage == null)
            {
                return;
            }

            step3LoadingCoverImage.color = WithAlpha(BackgroundColor, Mathf.Clamp01(alpha));
        }

        private void SetStep3LoadingPanelVisible(bool isVisible)
        {
            if (step3LoadingContentRoot == null)
            {
                return;
            }

            step3LoadingContentRoot.SetActive(isVisible);
        }

        private void SetStep3StatusVisible(bool isVisible)
        {
            if (step3StatusRoot == null)
            {
                return;
            }

            step3StatusRoot.SetActive(isVisible);
        }

        private void EnterBookshelfFocusForOnboarding()
        {
            CancelStep3Transition();
            ResetStep3FlowState();
            SetStep3LoadingPanelVisible(true);
            SetStep3StatusVisible(false);
            SetStep3LoadingCoverVisible(true);
            SetStep3LoadingCoverAlpha(1f);
            SetStep3OverlayStatus("메인 화면을 준비 중입니다.");
            step3TransitionRoutine = StartCoroutine(EnterMainAndBookshelfFocusFlow());
        }

#if false
        private IEnumerator EnterBookshelfFocusRoutine()
        {
            EnsureCrossScenePersistence();

#if false
            if (SceneManager.GetActiveScene().name != SceneKeys.Main)
            {
                SetStep3OverlayStatus("메인 화면으로 이동 중입니다.");
                SceneFlowManager.GoToMain();

                var loadDeadline = Time.unscaledTime + MainSceneLoadTimeoutSeconds;
                while (SceneManager.GetActiveScene().name != SceneKeys.Main && Time.unscaledTime < loadDeadline)
                {
                    yield return null;
                }

                if (SceneManager.GetActiveScene().name != SceneKeys.Main)
                {
                SetStep3OverlayStatus("메인 화면으로 이동 중입니다.");
                SceneFlowManager.GoToMain();

                var loadDeadline = Time.unscaledTime + MainSceneLoadTimeoutSeconds;
                while (SceneManager.GetActiveScene().name != SceneKeys.Main && Time.unscaledTime < loadDeadline)
                {
                    yield return null;
                }

                if (SceneManager.GetActiveScene().name != SceneKeys.Main)
                {
                SetStep3OverlayStatus("메인 화면으로 이동 중입니다.");
                SceneFlowManager.GoToMain();

                while (SceneManager.GetActiveScene().name != SceneKeys.Main)
                {
                    yield return null;
                }

                yield return null;
                yield return new WaitForEndOfFrame();
                yield return null;
            }

            yield return new WaitForSecondsRealtime(MainScenePreviewDelaySeconds);

            SetStep3LoadingCoverVisible(false);
            yield return new WaitForSecondsRealtime(MainScenePreviewDelaySeconds);

            SetStep3LoadingCoverAlpha(MainScenePreviewCoverAlpha);

            BookshelfExperienceManager experienceManager = null;
            for (var attempt = 0; attempt < 180; attempt++)
            {
                if (TryGetBookshelfExperienceManager(out experienceManager))
                {
                    break;
                }

                yield return null;
            }

            if (experienceManager == null)
            {
                SetStep3OverlayStatus("책장 포커스 연결을 확인해주세요.");
                Debug.LogWarning("[OnboardingDemo] BookshelfExperienceManager was not ready after loading MainScene.");
                step3TransitionRoutine = null;
                yield break;
            }

#endif
            if (SceneManager.GetActiveScene().name != SceneKeys.Main)
            {
                SetStep3OverlayStatus("메인 화면으로 이동 중입니다.");
                SceneFlowManager.GoToMain();

                var loadDeadline = Time.unscaledTime + MainSceneLoadTimeoutSeconds;
                while (SceneManager.GetActiveScene().name != SceneKeys.Main && Time.unscaledTime < loadDeadline)
                {
                    yield return null;
                }

                if (SceneManager.GetActiveScene().name != SceneKeys.Main)
                {
                    SetStep3OverlayStatus("메인 화면 연결을 확인해주세요.");
                    Debug.LogWarning("[OnboardingDemo] MainScene did not become active before the Step 3 timeout.");
                    step3TransitionRoutine = null;
                    yield break;
                }
            }

            yield return null;
            yield return new WaitForEndOfFrame();
            yield return null;

            SetStep3OverlayStatus("책장 포커스 화면으로 이동 중입니다.");
            experienceManager.FocusBookshelf();

            var cameraController = UnityEngine.Object.FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
            var isFocused = false;
            for (var attempt = 0; attempt < 240; attempt++)
            {
                if (cameraController == null)
                {
                    cameraController = UnityEngine.Object.FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
                }

                if (experienceManager.IsBookshelfFocused && (cameraController == null || !cameraController.IsTransitioning))
                {
                    isFocused = true;
                    break;
                }

                yield return null;
            }

            if (isFocused)
            {
                SetStep3LoadingCoverVisible(false);
                SetStep3OverlayStatus("책장 포커스 화면이 표시되었습니다.");
            }
            else
            {
                SetStep3OverlayStatus("책장 포커스 연결을 확인해주세요.");
                Debug.LogWarning("[OnboardingDemo] Failed to complete bookshelf focus transition using existing scene flow.");
            }

            step3TransitionRoutine = null;
        }
#endif

        private bool TryGetBookshelfExperienceManager(out BookshelfExperienceManager experienceManager)
        {
            experienceManager = null;

            if (SceneManager.GetActiveScene().name != SceneKeys.Main)
            {
                return false;
            }

            experienceManager = UnityEngine.Object.FindFirstObjectByType<BookshelfExperienceManager>(FindObjectsInactive.Include);
            if (experienceManager == null)
            {
                BookshelfAutoBootstrap.BootstrapScene();
                experienceManager = UnityEngine.Object.FindFirstObjectByType<BookshelfExperienceManager>(FindObjectsInactive.Include);
            }

            return experienceManager != null;
        }

        private IEnumerator EnterMainAndBookshelfFocusFlow()
        {
            EnsureCrossScenePersistence();
            SetStep3LoadingPanelVisible(true);
            SetStep3StatusVisible(false);
            SetStep3LoadingCoverVisible(true);
            SetStep3LoadingCoverAlpha(1f);
            SetStep3OverlayStatus("메인 화면을 준비 중입니다.");

            if (SceneManager.GetActiveScene().name != SceneKeys.Main)
            {
                SetStep3OverlayStatus("메인 화면으로 이동 중입니다.");
                SceneFlowManager.GoToMain();

                var loadDeadline = Time.unscaledTime + MainSceneLoadTimeoutSeconds;
                while (SceneManager.GetActiveScene().name != SceneKeys.Main && Time.unscaledTime < loadDeadline)
                {
                    yield return null;
                }

                if (SceneManager.GetActiveScene().name != SceneKeys.Main)
                {
                    SetStep3OverlayStatus("메인 화면 연결을 확인해주세요.");
                    Debug.LogWarning("[OnboardingDemo] MainScene did not become active before the Step 3 timeout.");
                    step3TransitionRoutine = null;
                    yield break;
                }
            }

            yield return null;
            yield return new WaitForEndOfFrame();
            yield return null;

            SetStep3OverlayStatus("메인 화면을 준비 중입니다.");

            BookshelfExperienceManager experienceManager = null;
            CameraController cameraController = null;
            BookshelfGenerator bookshelfGenerator = null;

            // Step 3 special case: the player must see QuarterView (not the new MainView
            // default) before the bookshelf-focus transition kicks in. Snap before the
            // loading cover hides so there is no MainView flash.
            if (TryGetBookshelfSceneDependencies(out _, out var initialCameraController, out _) &&
                initialCameraController != null)
            {
                initialCameraController.ApplyQuarterView();
            }

            SetStep3LoadingCoverVisible(false);
            yield return new WaitForSecondsRealtime(MainScenePreviewDelaySeconds);
            var readyDeadline = Time.unscaledTime + MainViewReadyTimeoutSeconds;
            while (Time.unscaledTime < readyDeadline)
            {
                if (TryGetBookshelfSceneDependencies(out experienceManager, out cameraController, out bookshelfGenerator) &&
                    IsMainViewReadyForBookshelfFocus(experienceManager, cameraController, bookshelfGenerator))
                {
                    isMainViewReady = true;
                    break;
                }

                yield return null;
            }

            if (!isMainViewReady)
            {
                Debug.LogWarning("[OnboardingDemo] Main view was not fully ready before requesting bookshelf focus. A fallback focus attempt will still run.");
                if (!TryGetBookshelfSceneDependencies(out experienceManager, out cameraController, out bookshelfGenerator))
                {
                    SetStep3OverlayStatus("책장 포커스 연결을 확인해주세요.");
                    Debug.LogWarning("[OnboardingDemo] BookshelfExperienceManager was not ready after loading MainScene.");
                    step3TransitionRoutine = null;
                    yield break;
                }
            }

            SetStep3OverlayStatus("책장 포커스 화면으로 이동 중입니다.");
            var focusDeadline = Time.unscaledTime + BookshelfFocusTimeoutSeconds;
            var nextRetryAt = Time.unscaledTime;
            while (Time.unscaledTime < focusDeadline)
            {
                if (!TryGetBookshelfSceneDependencies(out experienceManager, out cameraController, out bookshelfGenerator))
                {
                    yield return null;
                    continue;
                }

                if (experienceManager.IsBookshelfFocused && !IsCameraTransitioning(cameraController))
                {
                    isBookshelfFocused = true;
                    break;
                }

                if (IsMainViewReadyForBookshelfFocus(experienceManager, cameraController, bookshelfGenerator) &&
                    Time.unscaledTime >= nextRetryAt)
                {
                    experienceManager.FocusBookshelf();
                    isBookshelfFocusRequested = true;
                    nextRetryAt = Time.unscaledTime + BookshelfFocusRetryIntervalSeconds;
                }

                yield return null;
            }

            if (!isBookshelfFocused && isMainViewReady && !isBookshelfFocusRequested && experienceManager != null)
            {
                Debug.LogWarning("[OnboardingDemo] Step 3 focus request was delayed until the timeout window. Triggering one last focus request.");
                experienceManager.FocusBookshelf();
                yield return null;
                yield return new WaitForEndOfFrame();
                isBookshelfFocused = experienceManager.IsBookshelfFocused;
            }

            if (isBookshelfFocused)
            {
                SetStep3LoadingCoverVisible(false);
                SetStep3OverlayStatus("책장 포커스 화면이 표시되었습니다.");
            }
            else
            {
                SetStep3OverlayStatus("책장 포커스 연결을 확인해주세요.");
                Debug.LogWarning("[OnboardingDemo] Failed to complete bookshelf focus transition using existing scene flow before the Step 3 timeout.");
            }

            step3TransitionRoutine = null;
        }

        private bool EnsureMainSceneIsReady()
        {
            if (SceneManager.GetActiveScene().name == SceneKeys.Main)
            {
                return true;
            }

            SetStep3OverlayStatus("메인 화면으로 이동 중입니다.");
            SceneFlowManager.GoToMain();

            var loadDeadline = Time.unscaledTime + MainSceneLoadTimeoutSeconds;
            while (SceneManager.GetActiveScene().name != SceneKeys.Main && Time.unscaledTime < loadDeadline)
            {
                // Waited by caller coroutine through repeated invocation.
                return false;
            }

            return SceneManager.GetActiveScene().name == SceneKeys.Main;
        }

        private bool TryGetBookshelfSceneDependencies(
            out BookshelfExperienceManager experienceManager,
            out CameraController cameraController,
            out BookshelfGenerator bookshelfGenerator)
        {
            experienceManager = null;
            cameraController = null;
            bookshelfGenerator = null;

            if (SceneManager.GetActiveScene().name != SceneKeys.Main)
            {
                return false;
            }

            experienceManager = UnityEngine.Object.FindFirstObjectByType<BookshelfExperienceManager>(FindObjectsInactive.Include);
            if (experienceManager == null)
            {
                BookshelfAutoBootstrap.BootstrapScene();
                experienceManager = UnityEngine.Object.FindFirstObjectByType<BookshelfExperienceManager>(FindObjectsInactive.Include);
            }

            cameraController = UnityEngine.Object.FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
            bookshelfGenerator = UnityEngine.Object.FindFirstObjectByType<BookshelfGenerator>(FindObjectsInactive.Include);
            return experienceManager != null && cameraController != null;
        }

        private static bool IsMainViewReadyForBookshelfFocus(
            BookshelfExperienceManager experienceManager,
            CameraController cameraController,
            BookshelfGenerator bookshelfGenerator)
        {
            return experienceManager != null &&
                   experienceManager.isActiveAndEnabled &&
                   cameraController != null &&
                   cameraController.isActiveAndEnabled &&
                   !cameraController.IsTransitioning &&
                   (bookshelfGenerator == null || !bookshelfGenerator.IsBookshelfSwitching);
        }

        private static bool IsCameraTransitioning(CameraController cameraController)
        {
            return cameraController != null && cameraController.IsTransitioning;
        }

        private void ResetStep3FlowState()
        {
            isMainViewReady = false;
            isBookshelfFocusRequested = false;
            isBookshelfFocused = false;
        }

        private void EnsureCrossScenePersistence()
        {
            if (hasEnabledCrossScenePersistence)
            {
                return;
            }

            hasEnabledCrossScenePersistence = true;
            UnityEngine.Object.DontDestroyOnLoad(gameObject);

            if (onboardingCanvas != null)
            {
                UnityEngine.Object.DontDestroyOnLoad(onboardingCanvas.gameObject);
            }
        }

        private void CancelStep3Transition()
        {
            if (step3TransitionRoutine != null)
            {
                StopCoroutine(step3TransitionRoutine);
                step3TransitionRoutine = null;
            }

            SetStep3LoadingPanelVisible(false);
            SetStep3StatusVisible(false);
            SetStep3LoadingCoverVisible(false);
            ResetStep3FlowState();
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

            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                LogDebug("[OnboardingDemo] Profile image selection canceled.");
                return false;
            }

            return true;
#else
            Debug.LogWarning("파일 선택 기능은 현재 데모 환경에서만 지원됩니다.");
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

                    LogDebug("[OnboardingDemo] Profile image selection canceled.");
                    return false;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private bool TryLoadProfilePreview(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                LogDebug("[OnboardingDemo] Profile image selection canceled.");
                return false;
            }

            var extension = Path.GetExtension(imagePath);
            if (!string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("[OnboardingDemo] Unsupported image file selected.");
                return false;
            }

            try
            {
                var imageBytes = File.ReadAllBytes(imagePath);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.name = "OnboardingProfilePreview";
                texture.hideFlags = HideFlags.HideAndDontSave;

                if (!texture.LoadImage(imageBytes))
                {
                    Destroy(texture);
                    return false;
                }

                ReleaseProfilePreview();
                profileTexture = texture;
                profileSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                profileSprite.name = "OnboardingProfilePreviewSprite";
                profileSprite.hideFlags = HideFlags.HideAndDontSave;

                if (profilePreviewImage != null)
                {
                    profilePreviewImage.sprite = profileSprite;
                    profilePreviewImage.color = Color.white;
                    profilePreviewImage.type = Image.Type.Simple;
                    profilePreviewImage.preserveAspect = true;
                }

                var placeholder = profilePreviewImage != null ? profilePreviewImage.GetComponentInChildren<TMP_Text>(true) : null;
                if (placeholder != null)
                {
                    placeholder.gameObject.SetActive(false);
                }

                LogDebug($"[OnboardingDemo] Loaded profile preview from '{imagePath}'.");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[OnboardingDemo] Failed to read selected image: {exception.Message}");
                return false;
            }
        }

        private void ResetProfilePreview()
        {
            ReleaseProfilePreview();

            if (profilePreviewImage != null)
            {
                profilePreviewImage.sprite = roundedRectSprite;
                profilePreviewImage.type = Image.Type.Sliced;
                profilePreviewImage.color = InputColor;
                profilePreviewImage.preserveAspect = false;
            }

            var placeholder = profilePreviewImage != null ? profilePreviewImage.GetComponentInChildren<TMP_Text>(true) : null;
            if (placeholder != null)
            {
                placeholder.gameObject.SetActive(true);
            }
        }

        private void ReleaseProfilePreview()
        {
            if (profileSprite != null)
            {
                Destroy(profileSprite);
                profileSprite = null;
            }

            if (profileTexture != null)
            {
                Destroy(profileTexture);
                profileTexture = null;
            }
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

        private TMP_InputField CreateFlexibleInput(Transform parent, string objectName, string placeholderText, bool isPassword)
        {
            var fieldBackground = CreateImage(parent, objectName, InputColor);
            fieldBackground.sprite = roundedRectSprite;
            fieldBackground.type = Image.Type.Sliced;

            var fieldLayout = fieldBackground.gameObject.AddComponent<LayoutElement>();
            fieldLayout.preferredHeight = PrimaryControlHeight;
            fieldLayout.minHeight = PrimaryControlHeight;
            fieldLayout.flexibleWidth = 1f;

            var fieldOutline = fieldBackground.gameObject.AddComponent<Outline>();
            fieldOutline.effectColor = WithAlpha(BorderColor, 0.95f);
            fieldOutline.effectDistance = new Vector2(1f, -1f);

            var input = fieldBackground.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = fieldBackground;
            input.textViewport = CreateViewport(fieldBackground.transform);
            input.contentType = isPassword ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.caretColor = TextColor;
            input.selectionColor = WithAlpha(PrimaryColor, 0.25f);
            input.customCaretColor = true;

            var textComponent = CreateText(input.textViewport, "Text", string.Empty, 17f, FontStyles.Normal, TextColor, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero, 0f);
            StretchWithPadding(textComponent.rectTransform, new Vector2(16f, 10f), new Vector2(-16f, -10f));
            textComponent.textWrappingMode = TextWrappingModes.NoWrap;
            textComponent.overflowMode = TextOverflowModes.Ellipsis;

            var placeholder = CreateText(input.textViewport, "Placeholder", placeholderText, 17f, FontStyles.Normal, WithAlpha(MutedTextColor, 0.7f), TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero, 0f);
            StretchWithPadding(placeholder.rectTransform, new Vector2(16f, 10f), new Vector2(-16f, -10f));
            placeholder.textWrappingMode = TextWrappingModes.NoWrap;
            placeholder.overflowMode = TextOverflowModes.Ellipsis;

            input.textComponent = textComponent;
            input.placeholder = placeholder;
            return input;
        }

        private Button CreateFixedButton(
            Transform parent,
            string objectName,
            string labelText,
            Color backgroundColor,
            Color labelColor,
            bool outlined,
            Color outlineColor,
            Vector2 size)
        {
            var button = CreateButton(parent, objectName, labelText, backgroundColor, labelColor, Vector2.zero, size, outlined, outlineColor);
            var rectTransform = (RectTransform)button.transform;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;

            var layoutElement = button.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = size.x;
            layoutElement.minWidth = size.x;
            layoutElement.preferredHeight = size.y;
            layoutElement.minHeight = size.y;
            layoutElement.flexibleWidth = 0f;

            return button;
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

            var label = CreateText(buttonImage.transform, "Label", labelText, 16f, FontStyles.Bold, labelColor, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero);
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

        private TMP_Text CreateSectionText(
            Transform parent,
            string objectName,
            string value,
            float fontSize,
            FontStyles fontStyle,
            Color color,
            TextAlignmentOptions alignment,
            float preferredHeight,
            float characterSpacing = 0f)
        {
            var text = CreateText(parent, objectName, value, fontSize, fontStyle, color, alignment, Vector2.zero, Vector2.zero, characterSpacing);
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

        private void CreateAccentBlob(Transform parent, string objectName, Vector2 anchor, Vector2 size)
        {
            var image = CreateImage(parent, objectName, WithAlpha(AccentTint, 0.22f));
            image.sprite = roundedRectSprite;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;

            var rect = image.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
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

        private void ApplyFontToOnboardingCanvas()
        {
            if (onboardingCanvas == null || fontAsset == null)
            {
                return;
            }

            var tmpTexts = onboardingCanvas.GetComponentsInChildren<TMP_Text>(true);
            foreach (var tmpText in tmpTexts)
            {
                ApplyFontToText(tmpText);
            }

            var inputFields = onboardingCanvas.GetComponentsInChildren<TMP_InputField>(true);
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

        private void WarmupOnboardingGlyphs(TMP_FontAsset targetFontAsset)
        {
            if (targetFontAsset == null || targetFontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            {
                return;
            }

            const string commonText =
                "STEP 1STEP 2STEP 3" +
                "프로필을 설정해주세요" +
                "프로필 이미지, 닉네임, 한줄소개를 먼저 입력합니다." +
                "프로필 이미지" +
                "사진 추가" +
                "닉네임" +
                "닉네임을 입력하세요" +
                "중복확인" +
                "한줄소개" +
                "한줄소개를 입력하세요" +
                "사용 가능한 닉네임입니다." +
                "닉네임을 입력해주세요." +
                "닉네임 중복확인을 해주세요." +
                "다음 단계" +
                "테마를 선택해주세요" +
                "방과 책장의 분위기를 먼저 정해둘 수 있습니다." +
                "랜덤" +
                "내추럴클래식모던빈티지파스텔" +
                "테마 미리보기" +
                "테마를 선택하면 이 영역이 변경됩니다." +
                "메인 화면으로 이동 중입니다." +
                "책장 포커스 화면으로 이동 중입니다." +
                "책장 포커스 화면이 표시되었습니다." +
                "책장 포커스 연결을 확인해주세요." +
                "메인 화면과 책장 포커스 화면으로 이동할 준비가 되었습니다." +
                "이전 단계완료";

            targetFontAsset.TryAddCharacters(commonText, out _);
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
                    GlyphRenderMode.SDFAA,
                    1024,
                    1024,
                    AtlasPopulationMode.Dynamic,
                    true);

                if (dynamicFontAsset == null)
                {
                    return false;
                }

                dynamicFontAsset.name = $"{sourceFontAsset.name}_OnboardingDynamic";
                dynamicFontAsset.hideFlags = HideFlags.HideAndDontSave;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private Sprite CreateWhiteSprite()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "OnboardingUiWhite";
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            sprite.name = "OnboardingUiWhiteSprite";
            return sprite;
        }

        private Sprite CreateRoundedRectSprite(int size, int radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "OnboardingUiRounded";
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

            sprite.name = "OnboardingUiRoundedSprite";
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

        private static GameObject CreateRectObject(string objectName, Transform parent)
        {
            var result = new GameObject(objectName, typeof(RectTransform));
            result.layer = UiLayer;
            result.transform.SetParent(parent, false);
            return result;
        }

        private static RectTransform CreateColumn(Transform parent, string objectName, float spacing, RectOffset padding, TextAnchor alignment)
        {
            var columnObject = CreateRectObject(objectName, parent);
            var rectTransform = (RectTransform)columnObject.transform;

            var layoutGroup = columnObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = spacing;
            layoutGroup.padding = padding;
            layoutGroup.childAlignment = alignment;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            return rectTransform;
        }

        private static RectTransform CreateRow(Transform parent, string objectName, float spacing, TextAnchor alignment)
        {
            var rowObject = CreateRectObject(objectName, parent);
            var rectTransform = (RectTransform)rowObject.transform;

            var layoutGroup = rowObject.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.spacing = spacing;
            layoutGroup.childAlignment = alignment;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            var layoutElement = rowObject.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;
            layoutElement.minHeight = PrimaryControlHeight;

            return rectTransform;
        }

        private static void CreateSpacer(Transform parent, float preferredHeight)
        {
            var spacer = CreateRectObject("Spacer", parent);
            var layoutElement = spacer.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.minHeight = preferredHeight;
            layoutElement.flexibleWidth = 1f;
        }

        private static void CreateFlexibleSpacer(Transform parent)
        {
            var spacer = CreateRectObject("FlexibleSpacer", parent);
            var layoutElement = spacer.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;
            layoutElement.flexibleHeight = 1f;
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

        private static void SetMessage(TMP_Text label, string message, Color color)
        {
            if (label == null)
            {
                return;
            }

            label.text = message;
            label.color = color;
        }

        private static Color Blend(Color from, Color to, float t)
        {
            return Color.Lerp(from, to, Mathf.Clamp01(t));
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static Color Hex(string html)
        {
            return ColorUtility.TryParseHtmlString(html, out var color) ? color : Color.white;
        }
    }
}
