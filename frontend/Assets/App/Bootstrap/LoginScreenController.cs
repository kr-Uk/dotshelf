using System.Threading.Tasks;
using App.Auth;
using App.Config;
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
        private const float CardWidth = 720f;
        private const float CardHeight = 640f;
        private const float SocialButtonHeight = 52f;
        private const float SocialPaneWidth = 360f;
        private const int UiLayer = 5;
        private const int CanvasSortingOrder = 5000;

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

        private Canvas demoCanvas;
        private TMP_FontAsset fontAsset;
        private TMP_FontAsset fallbackFontAsset;
        private Sprite whiteSprite;
        private Sprite roundedRectSprite;

        private GameObject loginScreen;

        private TMP_Text loginMessageLabel;
        private Button appleLoginButton;
        private Button kakaoLoginButton;
        private Button googleLoginButton;
        private Button guestEntryButton;
        private Button signUpButton;

        private bool isInitialized;
        private bool listenersRegistered;
        private bool isLoginInFlight;
        private IAuthService authService;

        [Header("Development Social Login")]
        // Default off so a forgotten inspector token cannot be sent from a release build.
        // Editor/dev builds can flip this on per-scene; runtime guard below also restricts use.
        [SerializeField] private bool useDevelopmentSocialTokens = false;
        [SerializeField] private string googleDevelopmentToken;
        [SerializeField] private SocialTokenType googleDevelopmentTokenType = SocialTokenType.IdToken;
        [SerializeField] private string kakaoDevelopmentToken;
        [SerializeField] private SocialTokenType kakaoDevelopmentTokenType = SocialTokenType.AccessToken;
        [SerializeField] private string appleDevelopmentToken;
        [SerializeField] private SocialTokenType appleDevelopmentTokenType = SocialTokenType.IdToken;
        [SerializeField] private string appleDevelopmentNonce;

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
            SetMessage(loginMessageLabel, string.Empty);
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

        public void OnAppleLoginButtonClicked()
        {
            StartSocialLoginOrPromptSignUp(SocialProvider.Apple);
        }

        public void OnKakaoLoginButtonClicked()
        {
            StartSocialLoginOrPromptSignUp(SocialProvider.Kakao);
        }

        public void OnGoogleLoginButtonClicked()
        {
            StartSocialLoginOrPromptSignUp(SocialProvider.Google);
        }

        // External entry point — once a platform SDK obtains a provider token,
        // call this to ship it to BE /v1/auth/social-login. Unity does not
        // have native Google/Kakao/Apple SDKs wired up in this repo yet, so
        // the buttons currently surface a guide message; this method exists
        // so a future SDK integration can complete the flow without further
        // controller changes.
        public async void SubmitSocialLogin(
            SocialProvider provider,
            string providerToken,
            SocialTokenType tokenType = SocialTokenType.Default,
            string nonce = null)
        {
            if (isLoginInFlight)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(providerToken))
            {
                SetMessage(loginMessageLabel, "소셜 로그인 토큰을 받지 못했습니다. 다시 시도해주세요.");
                return;
            }

            EnsureAuthService();
            isLoginInFlight = true;
            SetInteractable(false);
            SetMessage(loginMessageLabel, $"{ProviderDisplayName(provider)} 로그인 중입니다...");

            try
            {
                var result = await authService.SocialLoginAsync(provider, providerToken, tokenType, nonce);
                ApplyLoginResult(result, ProviderDisplayName(provider));
            }
            finally
            {
                isLoginInFlight = false;
                SetInteractable(true);
            }
        }

        public async void OnGuestEntryButtonClicked()
        {
            if (isLoginInFlight)
            {
                Debug.Log("[LoginDemo] Guest button click ignored: another auth call is already in flight.");
                return;
            }

            Debug.Log("[LoginDemo] Guest button clicked -> calling /v1/auth/guest-session");
            EnsureAuthService();
            isLoginInFlight = true;
            SetInteractable(false);
            SetMessage(loginMessageLabel, "게스트 세션을 발급받는 중입니다...");

            GuestSessionResult result;
            try
            {
                result = await authService.StartGuestSessionAsync();
            }
            finally
            {
                isLoginInFlight = false;
                SetInteractable(true);
            }

            if (!result.IsSuccess)
            {
                Debug.LogWarning($"[LoginDemo] Guest session failed: code={result.ErrorCode} message={result.ErrorMessage}");
                SetMessage(loginMessageLabel, result.ErrorMessage);
                return;
            }

            Debug.Log($"[LoginDemo] Guest session OK: guestSessionId={result.GuestSessionId} expiresInSeconds={result.ExpiresInSeconds}");
            AuthSession.SaveGuestSessionId(result.GuestSessionId);
            // Read back from PlayerPrefs so the log proves what actually got
            // persisted, not just what we tried to write.
            Debug.Log($"[LoginDemo] PlayerPrefs[Dotshelf.Auth.GuestSessionId] = '{AuthSession.GuestSessionId}'");
            SessionState.MarkGuest();
            SetMessage(loginMessageLabel, "게스트로 입장합니다. 테마 설정을 진행해주세요.");

            // Guest entry skips onboarding step 1 (profile) and step 3 (bookshelf
            // focus). The user only chooses a theme on step 2; the existing
            // completion modal then handles transition to the main scene.
            var onboardingController = GetComponent<OnboardingScreenController>();
            if (onboardingController == null)
            {
                Debug.LogError("[LoginDemo] OnboardingScreenController is missing from LoginScene prefab — guest entry cannot start.");
                return;
            }

            onboardingController.InitializeIfNeeded();
            onboardingController.StartOnboardingAsGuest();
        }

        private void BeginNativeSocialLogin(SocialProvider provider, string displayName)
        {
            // Platform-side native login (Google/Kakao/Apple SDK) is not wired
            // into this Unity project yet. Once the SDK is integrated, that
            // code should call SubmitSocialLogin(...) with the provider token.
            // For now we surface a clear message instead of pretending to log
            // the user in.
            SetMessage(
                loginMessageLabel,
                $"{displayName} 로그인 SDK 연동이 준비되지 않았습니다. 게스트 입장으로 진행해주세요.");
        }

        private void ApplyLoginResult(LoginResult result, string providerDisplayName)
        {
            if (!result.IsSuccess)
            {
                SetMessage(loginMessageLabel, result.ErrorMessage);
                return;
            }

            AuthSession.SaveTokens(
                result.UserId,
                result.AccessToken,
                result.RefreshToken,
                result.ExpiresIn);
            SessionState.MarkLoggedIn();
            SetMessage(loginMessageLabel, $"{providerDisplayName} 로그인에 성공했습니다. 메인 화면으로 이동합니다.");

            MainSceneTransitionCover.Show("메인 화면으로 이동 중입니다.");
            SetCanvasVisible(false);
            SceneFlowManager.GoToMain();
        }

        private static string ProviderDisplayName(SocialProvider provider)
        {
            return provider switch
            {
                SocialProvider.Google => "Google",
                SocialProvider.Kakao => "Kakao",
                SocialProvider.Apple => "Apple",
                _ => provider.ToString()
            };
        }

        private void EnsureAuthService()
        {
            if (authService != null)
            {
                return;
            }

            // Pull base URL / timeout from AppApiConfig if a host (AppRoot/ConfigLoader)
            // is present in the scene. Without this the auth client always hits the
            // hardcoded production URL, ignoring dev/staging environment settings.
            var apiConfig = ResolveApiConfig();
            string overrideBaseUrl = null;
            int overrideTimeoutSeconds = 0;
            if (apiConfig != null)
            {
                overrideBaseUrl = apiConfig.ResolveBaseUrl();
                overrideTimeoutSeconds = apiConfig.RequestTimeoutSeconds;
            }

            var client = AuthApiClient.EnsureInstance(overrideBaseUrl, overrideTimeoutSeconds);
            authService = new BackendAuthService(client);
        }

        private static AppApiConfig ResolveApiConfig()
        {
            var appRoot = FindFirstObjectByType<AppRoot>(FindObjectsInactive.Include);
            if (appRoot != null && appRoot.ApiConfig != null)
            {
                return appRoot.ApiConfig;
            }

            var configLoader = FindFirstObjectByType<ConfigLoader>(FindObjectsInactive.Include);
            return configLoader != null ? configLoader.ApiConfig : null;
        }

        private void SetInteractable(bool interactable)
        {
            if (appleLoginButton != null) appleLoginButton.interactable = interactable;
            if (kakaoLoginButton != null) kakaoLoginButton.interactable = interactable;
            if (googleLoginButton != null) googleLoginButton.interactable = interactable;
            if (guestEntryButton != null) guestEntryButton.interactable = interactable;
            if (signUpButton != null) signUpButton.interactable = interactable;
        }

        private static void SetButtonLabel(Button button, string text)
        {
            if (button == null)
            {
                return;
            }

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = text;
            }
        }

        private void StartSocialLoginFromButton(SocialProvider provider)
        {
            if (TryGetDevelopmentSocialLoginPayload(provider, out var token, out var tokenType, out var nonce))
            {
                SubmitSocialLogin(provider, token, tokenType, nonce);
                return;
            }

            SetMessage(
                loginMessageLabel,
                $"{ProviderDisplayName(provider)} 로그인용 토큰이 없습니다. SDK 토큰을 연결하거나 LoginScreenController 개발용 토큰 값을 설정해주세요.");
        }

        public void OnSignUpButtonClicked()
        {
            var onboardingController = GetComponent<OnboardingScreenController>();
            if (onboardingController == null)
            {
                Debug.LogError("[LoginDemo] OnboardingScreenController is missing from LoginScene prefab — sign-up cannot start.");
                SetMessage(loginMessageLabel, "회원가입 화면을 열 수 없습니다. 잠시 후 다시 시도해주세요.");
                return;
            }

            onboardingController.InitializeIfNeeded();
            onboardingController.StartOnboarding();
        }

        private void StartSocialLoginOrPromptSignUp(SocialProvider provider)
        {
            if (TryGetDevelopmentSocialLoginPayload(provider, out var token, out var tokenType, out var nonce))
            {
                SubmitSocialLogin(provider, token, tokenType, nonce);
                return;
            }

            SetMessage(loginMessageLabel, "로그인 토큰을 확인할 수 없습니다. 먼저 회원가입을 진행해주세요.");
        }

        private bool TryGetDevelopmentSocialLoginPayload(
            SocialProvider provider,
            out string token,
            out SocialTokenType tokenType,
            out string nonce)
        {
            token = null;
            tokenType = SocialTokenType.Default;
            nonce = null;

            if (!useDevelopmentSocialTokens)
            {
                return false;
            }

#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
            // Hard-block release builds from ever using inspector-supplied test tokens,
            // even if useDevelopmentSocialTokens was accidentally checked on the prefab.
            Debug.LogWarning("[Auth] Development social tokens are disabled in release builds.");
            return false;
#else
            switch (provider)
            {
                case SocialProvider.Google:
                    token = googleDevelopmentToken;
                    tokenType = googleDevelopmentTokenType;
                    return !string.IsNullOrWhiteSpace(token);
                case SocialProvider.Kakao:
                    token = kakaoDevelopmentToken;
                    tokenType = kakaoDevelopmentTokenType;
                    return !string.IsNullOrWhiteSpace(token);
                case SocialProvider.Apple:
                    token = appleDevelopmentToken;
                    tokenType = appleDevelopmentTokenType;
                    nonce = appleDevelopmentNonce;
                    return !string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(nonce);
                default:
                    return false;
            }
#endif
        }

        public void SetCanvasVisible(bool isVisible)
        {
            if (demoCanvas == null)
            {
                return;
            }

            demoCanvas.gameObject.SetActive(isVisible);
        }

        private void RegisterListeners()
        {
            if (listenersRegistered)
            {
                return;
            }

            AddButtonListener(appleLoginButton, OnAppleLoginButtonClicked);
            AddButtonListener(kakaoLoginButton, OnKakaoLoginButtonClicked);
            AddButtonListener(googleLoginButton, OnGoogleLoginButtonClicked);
            AddButtonListener(guestEntryButton, OnGuestEntryButtonClicked);
            AddButtonListener(signUpButton, OnSignUpButtonClicked);

            listenersRegistered = true;
        }

        private void UnregisterListeners()
        {
            if (!listenersRegistered)
            {
                return;
            }

            RemoveButtonListener(appleLoginButton, OnAppleLoginButtonClicked);
            RemoveButtonListener(kakaoLoginButton, OnKakaoLoginButtonClicked);
            RemoveButtonListener(googleLoginButton, OnGoogleLoginButtonClicked);
            RemoveButtonListener(guestEntryButton, OnGuestEntryButtonClicked);
            RemoveButtonListener(signUpButton, OnSignUpButtonClicked);

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

            BuildLoginScreen(loginScreen.transform);
        }

        private void BuildLoginScreen(Transform parent)
        {
            var layoutRoot = CreateRectObject("LoginLayoutRoot", parent);
            StretchWithPadding((RectTransform)layoutRoot.transform, new Vector2(56f, 44f), new Vector2(-56f, -44f));

            var pane = CreateColumn(layoutRoot.transform, "SocialPane", SocialPaneWidth, 16f);
            ConfigurePaneLayout(pane, TextAnchor.MiddleCenter);

            // CreateColumn returns a RectTransform that expects to be sized by
            // a parent HorizontalLayoutGroup. Now that we render only this
            // single pane, anchor it to fill the layout root vertically and
            // size it to the configured pane width so the VerticalLayoutGroup
            // inside it has a width to lay out children against.
            pane.anchorMin = new Vector2(0.5f, 0f);
            pane.anchorMax = new Vector2(0.5f, 1f);
            pane.pivot = new Vector2(0.5f, 0.5f);
            pane.anchoredPosition = Vector2.zero;
            pane.sizeDelta = new Vector2(SocialPaneWidth, 0f);

            var brand = CreateLayoutText(
                pane.transform,
                "Brand",
                "BOOK ROOM",
                14f,
                FontStyles.Bold,
                MutedTextColor,
                TextAlignmentOptions.Center,
                22f);
            brand.characterSpacing = 6f;

            var title = CreateLayoutText(
                pane.transform,
                "Title",
                "로그인",
                28f,
                FontStyles.Bold,
                TextColor,
                TextAlignmentOptions.Center,
                40f);
            title.margin = new Vector4(0f, 0f, 0f, 8f);

            appleLoginButton = CreateStackButton(pane.transform, "AppleButton", "Apple 로그인", AppleColor, Color.white, false, BorderColor, SocialButtonHeight);
            kakaoLoginButton = CreateStackButton(pane.transform, "KakaoButton", "Kakao 로그인", KakaoColor, TextColor, false, BorderColor, SocialButtonHeight);
            googleLoginButton = CreateStackButton(pane.transform, "GoogleButton", "Google 로그인", GoogleColor, TextColor, true, BorderColor, SocialButtonHeight);
            guestEntryButton = CreateStackButton(pane.transform, "GuestEntryButton", "게스트로 입장하기", SecondaryColor, TextColor, true, BorderColor, SocialButtonHeight);

            if (false)
            {
                SetButtonLabel(appleLoginButton, "Apple 로그인 (준비 중)");
                SetButtonLabel(kakaoLoginButton, "Kakao 로그인 (준비 중)");
                SetButtonLabel(googleLoginButton, "Google 로그인 (준비 중)");
            }

            signUpButton = CreateStackButton(
                pane.transform,
                "SignUpButton",
                "회원가입",
                CardColor,
                PrimaryColor,
                true,
                BorderColor,
                44f);

            loginMessageLabel = CreateLayoutText(
                pane.transform,
                "LoginMessage",
                string.Empty,
                14f,
                FontStyles.Normal,
                ErrorColor,
                TextAlignmentOptions.Center,
                24f);
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
                "Apple 로그인" +
                "Kakao 로그인" +
                "Google 로그인" +
                "게스트로 입장하기" +
                "게스트 세션을 발급받는 중입니다..." +
                "게스트로 입장합니다. 테마 설정을 진행해주세요." +
                "Apple 로그인 중입니다..." +
                "Kakao 로그인 중입니다..." +
                "Google 로그인 중입니다..." +
                "Apple 로그인에 성공했습니다. 메인 화면으로 이동합니다." +
                "Kakao 로그인에 성공했습니다. 메인 화면으로 이동합니다." +
                "Google 로그인에 성공했습니다. 메인 화면으로 이동합니다." +
                "Apple 로그인 SDK 연동이 준비되지 않았습니다. 게스트 입장으로 진행해주세요." +
                "Kakao 로그인 SDK 연동이 준비되지 않았습니다. 게스트 입장으로 진행해주세요." +
                "Google 로그인 SDK 연동이 준비되지 않았습니다. 게스트 입장으로 진행해주세요." +
                "서버에 연결할 수 없습니다. 네트워크 상태를 확인해주세요." +
                "서버 오류가 발생했습니다. 잠시 후 다시 시도해주세요." +
                "게스트 세션을 시작하지 못했습니다. 네트워크 상태를 확인해주세요.";

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
