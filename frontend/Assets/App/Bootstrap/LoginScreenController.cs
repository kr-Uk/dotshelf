using App.Auth;
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
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public sealed class LoginScreenController : MonoBehaviour
    {
        private const string CanvasName = "LoginCanvas";
        private const string PreferredKoreanFontAssetName = "NotoSansKR-Regular";
        private const string FallbackKoreanFontAssetName = "NOTOSANSKR SDF";
        private const float CardWidth = 900f;
        private const float CardHeight = 640f;
        private const float PrimaryControlWidth = 380f;
        private const float PrimaryControlHeight = 56f;
        private const float SocialButtonHeight = 52f;
        private const float LoginPaneWidth = 360f;
        private const float SocialPaneWidth = 300f;
        private const int UiLayer = 5;
        private const int CanvasSortingOrder = 5000;
        private static readonly bool EnableDebugLogs = false;

        private static readonly Color BackgroundColor = Hex("#F4EFE6");
        private static readonly Color CardColor = Hex("#FFFDF9");
        private static readonly Color AccentTint = Hex("#D9C7A7");
        private static readonly Color PrimaryColor = Hex("#2B5C8A");
        private static readonly Color PrimaryPressedColor = Hex("#21496D");
        private static readonly Color PrimaryHighlightColor = Hex("#3A749F");
        private static readonly Color SecondaryColor = Hex("#F8F1E4");
        private static readonly Color AppleColor = Hex("#111111");
        private static readonly Color KakaoColor = Hex("#FEE500");
        private static readonly Color GoogleColor = Hex("#FFFFFF");
        private static readonly Color BorderColor = Hex("#D8CCBC");
        private static readonly Color TextColor = Hex("#2D2418");
        private static readonly Color MutedTextColor = Hex("#7C6A56");
        private static readonly Color ErrorColor = Hex("#C14E4E");
        private static readonly Color InputColor = Hex("#FDF9F3");

        private Canvas demoCanvas;
        private TMP_FontAsset fontAsset;
        private TMP_FontAsset fallbackFontAsset;
        private Sprite whiteSprite;
        private Sprite roundedRectSprite;

        private GameObject loginScreen;
        private GameObject signupScreen;

        private TMP_InputField loginIdInput;
        private TMP_InputField loginPasswordInput;
        private TMP_Text loginMessageLabel;
        private Button loginButton;
        private Button showSignupButton;
        private Button appleLoginButton;
        private Button kakaoLoginButton;
        private Button googleLoginButton;
        private Button guestEntryButton;

        private TMP_InputField signupEmailInput;
        private TMP_InputField signupPasswordInput;
        private TMP_InputField signupConfirmPasswordInput;
        private TMP_Text signupMessageLabel;
        private Button signupSubmitButton;
        private Button backToLoginButton;

        private bool isInitialized;
        private bool listenersRegistered;

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
            WarmupLoginGlyphs(fontAsset);

            BuildDemoCanvas();
            ApplyFontToLoginCanvas();
            EnsureEventSystem();
            DisableLegacyLoginUi();
            ShowLoginScreen();
            Canvas.ForceUpdateCanvases();
        }

        private void Awake()
        {
            InitializeIfNeeded();
        }

        private void OnEnable()
        {
            InitializeIfNeeded();
            RegisterListeners();
        }

        private void OnDisable()
        {
            UnregisterListeners();
        }

        public void OnLoginButtonClicked()
        {
            var userId = loginIdInput != null ? loginIdInput.text.Trim() : string.Empty;
            var password = loginPasswordInput != null ? loginPasswordInput.text.Trim() : string.Empty;

            if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(password))
            {
                SetMessage(loginMessageLabel, "아이디/이메일과 비밀번호를 입력해주세요.");
                return;
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                SetMessage(loginMessageLabel, "아이디 또는 이메일을 입력해주세요.");
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                SetMessage(loginMessageLabel, "비밀번호를 입력해주세요.");
                return;
            }

            SetMessage(loginMessageLabel, "로그인에 성공했습니다. 메인 화면으로 이동합니다.");
            LogDebug($"[LoginDemo] Login validated for '{userId}'.");

            SessionState.MarkLoggedIn();

            // Mask the brief frames between scene unload and BookshelfSceneInstaller
            // finishing its setup so the player never sees the empty default scene
            // between login and main view. Cover self-destructs once MainScene is
            // ready.
            MainSceneTransitionCover.Show("메인 화면으로 이동 중입니다.");
            SetCanvasVisible(false);
            SceneFlowManager.GoToMain();
        }

        public void OnShowSignupButtonClicked()
        {
            SetMessage(loginMessageLabel, string.Empty);
            SetMessage(signupMessageLabel, string.Empty);

            if (signupEmailInput != null && string.IsNullOrWhiteSpace(signupEmailInput.text) && loginIdInput != null)
            {
                signupEmailInput.text = loginIdInput.text.Trim();
            }

            if (loginScreen != null)
            {
                loginScreen.SetActive(false);
            }

            if (signupScreen != null)
            {
                signupScreen.SetActive(true);
            }
        }

        public void OnSignupSubmitButtonClicked()
        {
            var email = signupEmailInput != null ? signupEmailInput.text.Trim() : string.Empty;
            var password = signupPasswordInput != null ? signupPasswordInput.text.Trim() : string.Empty;
            var confirmPassword = signupConfirmPasswordInput != null ? signupConfirmPasswordInput.text.Trim() : string.Empty;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                SetMessage(signupMessageLabel, "이메일, 비밀번호, 비밀번호 확인을 모두 입력해주세요.");
                return;
            }

            if (!email.Contains("@"))
            {
                SetMessage(signupMessageLabel, "이메일 형식을 확인해주세요.");
                return;
            }

            if (password != confirmPassword)
            {
                SetMessage(signupMessageLabel, "비밀번호가 일치하지 않습니다.");
                return;
            }

            SetMessage(signupMessageLabel, string.Empty);
            LogDebug($"[LoginDemo] Signup validated for '{email}'.");

            // OnboardingScreenController is paired with this controller on the
            // LoginScene's prefab (LoginSceneController.Awake guarantees it),
            // so we never AddComponent at runtime — that would lose any
            // [SerializeField] values configured in the inspector.
            var onboardingController = GetComponent<OnboardingScreenController>();
            if (onboardingController == null)
            {
                Debug.LogError("[LoginDemo] OnboardingScreenController is missing from LoginScene prefab — signup cannot start onboarding.");
                return;
            }

            onboardingController.InitializeIfNeeded();
            onboardingController.StartOnboarding();
        }

        public void OnBackToLoginButtonClicked()
        {
            ShowLoginScreen();
        }

        public void OnAppleLoginButtonClicked()
        {
            LogDebug("[LoginDemo] Apple login button clicked.");
            HandleSocialLoginSuccess("Apple");
        }

        public void OnKakaoLoginButtonClicked()
        {
            LogDebug("[LoginDemo] Kakao login button clicked.");
            HandleSocialLoginSuccess("Kakao");
        }

        public void OnGoogleLoginButtonClicked()
        {
            LogDebug("[LoginDemo] Google login button clicked.");
            HandleSocialLoginSuccess("Google");
        }

        public void OnGuestEntryButtonClicked()
        {
            LogDebug("[LoginDemo] Guest entry button clicked.");
            SetMessage(loginMessageLabel, "게스트로 입장합니다. 테마 설정을 진행해주세요.");

            SessionState.MarkGuest();

            // Guest entry skips onboarding step 1 (profile) and step 3 (bookshelf
            // focus). The user only chooses a theme on step 2; the existing
            // completion modal then handles transition to the main scene.
            //
            // The OnboardingScreenController is paired with this controller on
            // the LoginScene's prefab (LoginSceneController.Awake guarantees
            // it), so GetComponent always returns a real instance. We don't
            // AddComponent at runtime — that would lose any [SerializeField]
            // values the inspector configured and hide the dependency from
            // anyone reading the prefab.
            var onboardingController = GetComponent<OnboardingScreenController>();
            if (onboardingController == null)
            {
                Debug.LogError("[LoginDemo] OnboardingScreenController is missing from LoginScene prefab — guest entry cannot start.");
                return;
            }

            onboardingController.InitializeIfNeeded();
            onboardingController.StartOnboardingAsGuest();
        }

        private void HandleSocialLoginSuccess(string providerName)
        {
            SetMessage(loginMessageLabel, $"{providerName} 로그인에 성공했습니다. 메인 화면으로 이동합니다.");

            SessionState.MarkLoggedIn();

            MainSceneTransitionCover.Show("메인 화면으로 이동 중입니다.");
            SetCanvasVisible(false);
            SceneFlowManager.GoToMain();
        }

        public void SetCanvasVisible(bool isVisible)
        {
            if (demoCanvas == null)
            {
                return;
            }

            demoCanvas.gameObject.SetActive(isVisible);
        }

        private static void LogDebug(string message)
        {
            if (!EnableDebugLogs)
            {
                return;
            }

            Debug.Log(message);
        }

        private void RegisterListeners()
        {
            if (listenersRegistered)
            {
                return;
            }

            AddButtonListener(loginButton, OnLoginButtonClicked);
            AddButtonListener(showSignupButton, OnShowSignupButtonClicked);
            AddButtonListener(signupSubmitButton, OnSignupSubmitButtonClicked);
            AddButtonListener(backToLoginButton, OnBackToLoginButtonClicked);
            AddButtonListener(appleLoginButton, OnAppleLoginButtonClicked);
            AddButtonListener(kakaoLoginButton, OnKakaoLoginButtonClicked);
            AddButtonListener(googleLoginButton, OnGoogleLoginButtonClicked);
            AddButtonListener(guestEntryButton, OnGuestEntryButtonClicked);

            AddInputListener(loginIdInput, ClearLoginMessage);
            AddInputListener(loginPasswordInput, ClearLoginMessage);
            AddInputListener(signupEmailInput, ClearSignupMessage);
            AddInputListener(signupPasswordInput, ClearSignupMessage);
            AddInputListener(signupConfirmPasswordInput, ClearSignupMessage);

            listenersRegistered = true;
        }

        private void UnregisterListeners()
        {
            if (!listenersRegistered)
            {
                return;
            }

            RemoveButtonListener(loginButton, OnLoginButtonClicked);
            RemoveButtonListener(showSignupButton, OnShowSignupButtonClicked);
            RemoveButtonListener(signupSubmitButton, OnSignupSubmitButtonClicked);
            RemoveButtonListener(backToLoginButton, OnBackToLoginButtonClicked);
            RemoveButtonListener(appleLoginButton, OnAppleLoginButtonClicked);
            RemoveButtonListener(kakaoLoginButton, OnKakaoLoginButtonClicked);
            RemoveButtonListener(googleLoginButton, OnGoogleLoginButtonClicked);
            RemoveButtonListener(guestEntryButton, OnGuestEntryButtonClicked);

            RemoveInputListener(loginIdInput, ClearLoginMessage);
            RemoveInputListener(loginPasswordInput, ClearLoginMessage);
            RemoveInputListener(signupEmailInput, ClearSignupMessage);
            RemoveInputListener(signupPasswordInput, ClearSignupMessage);
            RemoveInputListener(signupConfirmPasswordInput, ClearSignupMessage);

            listenersRegistered = false;
        }

        private void BuildDemoCanvas()
        {
            var existingCanvasObject = FindExistingDemoCanvasObject();
            if (existingCanvasObject != null)
            {
                Destroy(existingCanvasObject);
            }

            var canvasObject = new GameObject(CanvasName, typeof(RectTransform));
            canvasObject.layer = UiLayer;
            SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene);

            demoCanvas = canvasObject.AddComponent<Canvas>();
            demoCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            demoCanvas.overrideSorting = true;
            demoCanvas.sortingOrder = CanvasSortingOrder;
            demoCanvas.pixelPerfect = false;
            demoCanvas.targetDisplay = 0;

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

            CreateAccentBlob(background.transform, "TopAccent", new Vector2(0.82f, 0.89f), new Vector2(260f, 260f));
            CreateAccentBlob(background.transform, "BottomAccent", new Vector2(0.16f, 0.16f), new Vector2(220f, 220f));

            var card = CreateImage(background.transform, "LoginCard", CardColor);
            card.sprite = roundedRectSprite;
            card.type = Image.Type.Sliced;
            card.pixelsPerUnitMultiplier = 1f;

            var cardRect = card.rectTransform;
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(CardWidth, CardHeight);
            cardRect.anchoredPosition = Vector2.zero;

            var cardShadow = card.gameObject.AddComponent<Shadow>();
            cardShadow.effectColor = WithAlpha(TextColor, 0.15f);
            cardShadow.effectDistance = new Vector2(0f, -14f);

            var topBand = CreateImage(card.transform, "TopBand", WithAlpha(AccentTint, 0.18f));
            topBand.sprite = roundedRectSprite;
            topBand.type = Image.Type.Sliced;
            topBand.raycastTarget = false;
            var topBandRect = topBand.rectTransform;
            topBandRect.anchorMin = new Vector2(0f, 1f);
            topBandRect.anchorMax = new Vector2(1f, 1f);
            topBandRect.pivot = new Vector2(0.5f, 1f);
            topBandRect.offsetMin = new Vector2(0f, -20f);
            topBandRect.offsetMax = new Vector2(0f, 0f);

            loginScreen = CreateRectObject("LoginScreen", card.transform);
            StretchToParent((RectTransform)loginScreen.transform);

            signupScreen = CreateRectObject("SignupScreen", card.transform);
            StretchToParent((RectTransform)signupScreen.transform);

            BuildLoginScreen(loginScreen.transform);
            BuildSignupScreen(signupScreen.transform);
        }

        private void BuildLoginScreen(Transform parent)
        {
            var layoutRoot = CreateRectObject("LoginLayoutRoot", parent);
            StretchWithPadding((RectTransform)layoutRoot.transform, new Vector2(56f, 44f), new Vector2(-56f, -44f));

            var horizontalLayout = layoutRoot.AddComponent<HorizontalLayoutGroup>();
            horizontalLayout.padding = new RectOffset(0, 0, 0, 0);
            horizontalLayout.spacing = 28f;
            horizontalLayout.childAlignment = TextAnchor.MiddleCenter;
            horizontalLayout.childControlWidth = false;
            horizontalLayout.childControlHeight = true;
            horizontalLayout.childForceExpandWidth = false;
            horizontalLayout.childForceExpandHeight = true;

            var leftPane = CreateColumn(layoutRoot.transform, "LoginPane", LoginPaneWidth, 18f);
            ConfigurePaneLayout(leftPane, TextAnchor.MiddleCenter);

            var leftHeader = CreateLayoutText(
                leftPane.transform,
                "LoginGuideText",
                "아이디 / 비밀번호를 입력해주세요",
                20f,
                FontStyles.Bold,
                TextColor,
                TextAlignmentOptions.Left,
                48f);
            leftHeader.margin = new Vector4(0f, 0f, 0f, 4f);

            loginIdInput = CreateStackInput(leftPane.transform, "LoginIdField", "아이디 입력", false);
            loginPasswordInput = CreateStackInput(leftPane.transform, "LoginPasswordField", "비밀번호 입력", true);
            loginButton = CreateStackButton(leftPane.transform, "LoginButton", "로그인", PrimaryColor, Color.white, false, BorderColor, PrimaryControlHeight);
            showSignupButton = CreateStackButton(leftPane.transform, "ShowSignupButton", "회원가입", SecondaryColor, TextColor, true, BorderColor, PrimaryControlHeight);
            loginMessageLabel = CreateLayoutText(
                leftPane.transform,
                "LoginMessage",
                string.Empty,
                14f,
                FontStyles.Normal,
                ErrorColor,
                TextAlignmentOptions.Left,
                24f);

            var divider = CreateImage(layoutRoot.transform, "Divider", WithAlpha(BorderColor, 0.9f));
            divider.raycastTarget = false;
            var dividerLayout = divider.gameObject.AddComponent<LayoutElement>();
            dividerLayout.minWidth = 2f;
            dividerLayout.preferredWidth = 2f;
            dividerLayout.flexibleWidth = 0f;
            dividerLayout.flexibleHeight = 1f;

            var dividerRect = divider.rectTransform;
            dividerRect.anchorMin = new Vector2(0.5f, 0f);
            dividerRect.anchorMax = new Vector2(0.5f, 1f);
            dividerRect.pivot = new Vector2(0.5f, 0.5f);
            dividerRect.sizeDelta = new Vector2(2f, 0f);

            var rightPane = CreateColumn(layoutRoot.transform, "SocialPane", SocialPaneWidth, 16f);
            ConfigurePaneLayout(rightPane, TextAnchor.MiddleCenter);

            var socialHeader = CreateLayoutText(
                rightPane.transform,
                "SocialGuideText",
                "소셜 로그인",
                18f,
                FontStyles.Bold,
                MutedTextColor,
                TextAlignmentOptions.Center,
                40f);
            socialHeader.characterSpacing = 1f;

            appleLoginButton = CreateStackButton(rightPane.transform, "AppleButton", "Apple 로그인", AppleColor, Color.white, false, BorderColor, SocialButtonHeight);
            kakaoLoginButton = CreateStackButton(rightPane.transform, "KakaoButton", "Kakao 로그인", KakaoColor, TextColor, false, BorderColor, SocialButtonHeight);
            googleLoginButton = CreateStackButton(rightPane.transform, "GoogleButton", "Google 로그인", GoogleColor, TextColor, true, BorderColor, SocialButtonHeight);
            guestEntryButton = CreateStackButton(rightPane.transform, "GuestEntryButton", "게스트로 입장하기", SecondaryColor, TextColor, true, BorderColor, SocialButtonHeight);
        }

        private void BuildSignupScreen(Transform parent)
        {
            CreateText(parent, "Brand", "BOOK ROOM", 14f, FontStyles.Bold, MutedTextColor, TextAlignmentOptions.Center, new Vector2(0f, -34f), new Vector2(260f, 22f), 6f);
            CreateText(parent, "Title", "회원가입", 36f, FontStyles.Bold, TextColor, TextAlignmentOptions.Center, new Vector2(0f, -86f), new Vector2(360f, 42f));
            CreateText(parent, "Subtitle", "이메일과 비밀번호만 받는 데모 화면입니다.", 16f, FontStyles.Normal, MutedTextColor, TextAlignmentOptions.Center, new Vector2(0f, -126f), new Vector2(380f, 24f));

            signupEmailInput = CreateLabeledInput(parent, "SignupEmailField", "이메일", "이메일 입력", new Vector2(0f, -196f), false);
            signupPasswordInput = CreateLabeledInput(parent, "SignupPasswordField", "비밀번호", "비밀번호 입력", new Vector2(0f, -284f), true);
            signupConfirmPasswordInput = CreateLabeledInput(parent, "SignupConfirmPasswordField", "비밀번호 확인", "비밀번호 다시 입력", new Vector2(0f, -372f), true);

            signupMessageLabel = CreateText(parent, "SignupMessage", string.Empty, 14f, FontStyles.Normal, ErrorColor, TextAlignmentOptions.Center, new Vector2(0f, -430f), new Vector2(380f, 22f));
            signupSubmitButton = CreateButton(parent, "SignupButton", "회원가입", PrimaryColor, Color.white, new Vector2(0f, -492f), new Vector2(PrimaryControlWidth, PrimaryControlHeight), false, BorderColor);
            backToLoginButton = CreateButton(parent, "BackToLoginButton", "로그인으로 돌아가기", SecondaryColor, TextColor, new Vector2(0f, -556f), new Vector2(PrimaryControlWidth, PrimaryControlHeight), true, BorderColor);
        }

        private void ShowLoginScreen()
        {
            SetMessage(loginMessageLabel, string.Empty);
            SetMessage(signupMessageLabel, string.Empty);

            if (loginScreen != null)
            {
                loginScreen.SetActive(true);
            }

            if (signupScreen != null)
            {
                signupScreen.SetActive(false);
            }
        }

        private void ClearLoginMessage(string _)
        {
            SetMessage(loginMessageLabel, string.Empty);
        }

        private void ClearSignupMessage(string _)
        {
            SetMessage(signupMessageLabel, string.Empty);
        }

        private void DisableLegacyLoginUi()
        {
            if (TryGetComponent<Canvas>(out var rootCanvas))
            {
                rootCanvas.enabled = false;
            }

            if (TryGetComponent<LoginUiStyler>(out var styler))
            {
                styler.enabled = false;
            }

            var scene = gameObject.scene;
            if (!scene.IsValid())
            {
                return;
            }

            foreach (var rootObject in scene.GetRootGameObjects())
            {
                if (rootObject == null || rootObject == gameObject)
                {
                    continue;
                }

                if (rootObject.name == "Canvas")
                {
                    rootObject.SetActive(false);
                }
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

        private TMP_InputField CreateLabeledInput(Transform parent, string objectName, string labelText, string placeholderText, Vector2 anchoredPosition, bool isPassword)
        {
            var container = CreateRectObject(objectName, parent);
            var containerRect = (RectTransform)container.transform;
            containerRect.anchorMin = new Vector2(0.5f, 1f);
            containerRect.anchorMax = new Vector2(0.5f, 1f);
            containerRect.pivot = new Vector2(0.5f, 1f);
            containerRect.sizeDelta = new Vector2(PrimaryControlWidth, 72f);
            containerRect.anchoredPosition = anchoredPosition;

            CreateText(
                container.transform,
                "Label",
                labelText,
                12f,
                FontStyles.Bold,
                MutedTextColor,
                TextAlignmentOptions.Left,
                new Vector2(-PrimaryControlWidth * 0.5f, 0f),
                new Vector2(PrimaryControlWidth, 18f),
                4f,
                new Vector2(0f, 1f));

            var fieldBackground = CreateImage(container.transform, "FieldBackground", InputColor);
            fieldBackground.sprite = roundedRectSprite;
            fieldBackground.type = Image.Type.Sliced;
            var fieldRect = fieldBackground.rectTransform;
            fieldRect.anchorMin = new Vector2(0.5f, 1f);
            fieldRect.anchorMax = new Vector2(0.5f, 1f);
            fieldRect.pivot = new Vector2(0.5f, 1f);
            fieldRect.sizeDelta = new Vector2(PrimaryControlWidth, PrimaryControlHeight);
            fieldRect.anchoredPosition = new Vector2(0f, -20f);

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
                if (candidate != null && string.Equals(candidate.name, fontAssetName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        private void ApplyFontToLoginCanvas()
        {
            if (demoCanvas == null || fontAsset == null)
            {
                return;
            }

            var tmpTexts = demoCanvas.GetComponentsInChildren<TMP_Text>(true);
            foreach (var tmpText in tmpTexts)
            {
                ApplyFontToText(tmpText);
            }

            var inputFields = demoCanvas.GetComponentsInChildren<TMP_InputField>(true);
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
                    fallbackTable = new System.Collections.Generic.List<TMP_FontAsset>();
                    text.font.fallbackFontAssetTable = fallbackTable;
                }

                if (!fallbackTable.Contains(fallbackFontAsset))
                {
                    fallbackTable.Add(fallbackFontAsset);
                }
            }
        }

        private void WarmupLoginGlyphs(TMP_FontAsset targetFontAsset)
        {
            if (targetFontAsset == null || targetFontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            {
                return;
            }

            var commonText =
                "BOOK ROOM" +
                "로그인" +
                "회원가입" +
                "아이디 / 비밀번호를 입력해주세요" +
                "데모용 화면입니다. 입력 검증만 동작합니다." +
                "이메일과 비밀번호만 받는 데모 화면입니다." +
                "아이디 / 이메일" +
                "아이디 입력" +
                "아이디 또는 이메일 입력" +
                "비밀번호" +
                "비밀번호 입력" +
                "소셜 로그인" +
                "Apple 로그인" +
                "Kakao 로그인" +
                "Google 로그인" +
                "게스트로 입장하기" +
                "이메일" +
                "이메일 입력" +
                "비밀번호 확인" +
                "비밀번호 다시 입력" +
                "로그인으로 돌아가기" +
                "아이디/이메일과 비밀번호를 입력해주세요." +
                "아이디 또는 이메일을 입력해주세요." +
                "비밀번호를 입력해주세요." +
                "이메일, 비밀번호, 비밀번호 확인을 모두 입력해주세요." +
                "이메일 형식을 확인해주세요." +
                "비밀번호가 일치하지 않습니다.";

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

                dynamicFontAsset.name = $"{sourceFontAsset.name}_LoginDynamic";
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
            texture.name = "LoginUiWhite";
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            sprite.name = "LoginUiWhiteSprite";
            return sprite;
        }

        private Sprite CreateRoundedRectSprite(int size, int radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "LoginUiRounded";
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

            sprite.name = "LoginUiRoundedSprite";
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

        private void SetMessage(TMP_Text label, string message)
        {
            if (label == null)
            {
                return;
            }

            label.text = message;
        }

        private static void AddButtonListener(Button button, UnityEngine.Events.UnityAction callback)
        {
            if (button != null)
            {
                button.onClick.AddListener(callback);
            }
        }

        private static void RemoveButtonListener(Button button, UnityEngine.Events.UnityAction callback)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(callback);
            }
        }

        private static void AddInputListener(TMP_InputField inputField, UnityEngine.Events.UnityAction<string> callback)
        {
            if (inputField != null)
            {
                inputField.onValueChanged.AddListener(callback);
            }
        }

        private static void RemoveInputListener(TMP_InputField inputField, UnityEngine.Events.UnityAction<string> callback)
        {
            if (inputField != null)
            {
                inputField.onValueChanged.RemoveListener(callback);
            }
        }

        private static GameObject CreateRectObject(string objectName, Transform parent)
        {
            var gameObject = new GameObject(objectName, typeof(RectTransform));
            gameObject.layer = UiLayer;
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private RectTransform CreateColumn(Transform parent, string objectName, float preferredWidth, float spacing)
        {
            var columnObject = CreateRectObject(objectName, parent);
            var columnRect = (RectTransform)columnObject.transform;

            var layoutElement = columnObject.AddComponent<LayoutElement>();
            layoutElement.minWidth = preferredWidth;
            layoutElement.preferredWidth = preferredWidth;
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 1f;

            var verticalLayout = columnObject.AddComponent<VerticalLayoutGroup>();
            verticalLayout.padding = new RectOffset(0, 0, 6, 6);
            verticalLayout.spacing = spacing;
            verticalLayout.childAlignment = TextAnchor.MiddleCenter;
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = true;
            verticalLayout.childForceExpandWidth = true;
            verticalLayout.childForceExpandHeight = false;

            return columnRect;
        }

        private static void ConfigurePaneLayout(RectTransform pane, TextAnchor alignment)
        {
            if (pane == null)
            {
                return;
            }

            if (pane.TryGetComponent<VerticalLayoutGroup>(out var layoutGroup))
            {
                layoutGroup.childAlignment = alignment;
            }
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
            var text = CreateText(parent, objectName, value, fontSize, fontStyle, color, alignment, Vector2.zero, Vector2.zero);
            var rectTransform = text.rectTransform;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;

            var layoutElement = text.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.flexibleWidth = 1f;
            layoutElement.minHeight = preferredHeight;

            return text;
        }

        private TMP_InputField CreateStackInput(Transform parent, string objectName, string placeholderText, bool isPassword)
        {
            var fieldBackground = CreateImage(parent, objectName, InputColor);
            fieldBackground.sprite = roundedRectSprite;
            fieldBackground.type = Image.Type.Sliced;

            var fieldLayout = fieldBackground.gameObject.AddComponent<LayoutElement>();
            fieldLayout.minHeight = PrimaryControlHeight;
            fieldLayout.preferredHeight = PrimaryControlHeight;
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

        private Button CreateStackButton(
            Transform parent,
            string objectName,
            string labelText,
            Color backgroundColor,
            Color labelColor,
            bool outlined,
            Color outlineColor,
            float preferredHeight)
        {
            var button = CreateButton(parent, objectName, labelText, backgroundColor, labelColor, Vector2.zero, Vector2.zero, outlined, outlineColor);
            var rectTransform = ((RectTransform)button.transform);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;

            var layoutElement = button.gameObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = preferredHeight;
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.flexibleWidth = 1f;

            return button;
        }

        private GameObject FindExistingDemoCanvasObject()
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
            ColorUtility.TryParseHtmlString(htmlColor, out var color);
            return color;
        }
    }
}
