using System;
using System.Collections.Generic;
using App.Auth;
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
    // Owns the floating "게스트" badge in the main scene's top-left corner and
    // the data-migration modal that opens when the badge is tapped. The badge
    // is only visible while the current session is in guest mode (see
    // SessionState) and only when the regular main-scene chrome would also
    // appear (QuarterView focus, no edit mode, no onboarding overlay).
    [DefaultExecutionOrder(-680)]
    [DisallowMultipleComponent]
    public sealed class GuestBadgeController : MonoBehaviour
    {
        private const string CanvasName = "GuestBadgeCanvas";
        private const string PreferredKoreanFontAssetName = "NotoSansKR-Regular";
        private const string FallbackKoreanFontAssetName = "NOTOSANSKR SDF";
        private const int UiLayer = 5;
        private const int CanvasSortingOrder = 5900;

        // Mirror SettingsScreenController's button metrics so the guest badge
        // visually pairs with the existing top-right buttons even though it
        // sits on the opposite corner.
        private const float BadgeWidth = 132f;
        private const float BadgeHeight = 60f;
        private const float BadgeMargin = 32f;

        private static readonly Color BackgroundColor = Hex("#F4EFE6");
        private static readonly Color CardColor = Hex("#FFFDF9");
        private static readonly Color PrimaryColor = Hex("#2B5C8A");
        private static readonly Color PrimaryPressedColor = Hex("#21496D");
        private static readonly Color PrimaryHighlightColor = Hex("#3A749F");
        private static readonly Color SecondaryColor = Hex("#F8F1E4");
        private static readonly Color BorderColor = Hex("#D8CCBC");
        private static readonly Color TextColor = Hex("#2D2418");
        private static readonly Color MutedTextColor = Hex("#7C6A56");
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.55f);

        private Canvas badgeCanvas;
        private TMP_FontAsset fontAsset;
        private TMP_FontAsset fallbackFontAsset;
        private Sprite whiteSprite;
        private Sprite roundedRectSprite;

        private GameObject badgeRoot;
        private GameObject modalRoot;

        private BookshelfExperienceManager cachedExperienceManager;
        private RoomEditModeController cachedEditModeController;

        private bool isInitialized;

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
            if (UnityEngine.Object.FindFirstObjectByType<GuestBadgeController>() != null)
            {
                return;
            }

            var hostObject = new GameObject(nameof(GuestBadgeController));
            if (scene.IsValid())
            {
                SceneManager.MoveGameObjectToScene(hostObject, scene);
            }

            hostObject.AddComponent<GuestBadgeController>();
        }

        private void Awake()
        {
            InitializeIfNeeded();
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
            BuildBadge();
            BuildModal();
            ApplyFontToCanvas();
            EnsureEventSystem();

            SetModalVisible(false);
            UpdateBadgeVisibility();
        }

        private void Update()
        {
            if (!isInitialized)
            {
                return;
            }

            UpdateBadgeVisibility();
        }

        // Visible only while the player is on the main view (QuarterView), not
        // in edit mode, not running onboarding step 3, and the session is
        // marked as guest. Mirrors SettingsScreenController's chrome rules so
        // the badge doesn't fight with the settings/mode-change buttons or the
        // edit-mode toolbar.
        private void UpdateBadgeVisibility()
        {
            var modalOpen = modalRoot != null && modalRoot.activeSelf;
            var experienceManager = ResolveExperienceManager();
            var inMainScene = experienceManager != null &&
                              experienceManager.CurrentFocusArea == ExperienceFocusArea.QuarterView;
            var step3Active = experienceManager != null && experienceManager.IsOnboardingStep3Active;
            var editModeActive = ResolveEditModeController() != null && ResolveEditModeController().IsEditModeActive;
            var shouldShow = SessionState.IsGuest && !modalOpen && inMainScene && !step3Active && !editModeActive;

            if (badgeRoot != null && badgeRoot.activeSelf != shouldShow)
            {
                badgeRoot.SetActive(shouldShow);
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

        // ---------------------------------------------------------------------
        // Canvas / 배지 / 모달 빌드
        // ---------------------------------------------------------------------

        private void BuildCanvas()
        {
            var existingCanvas = FindExistingCanvasObject();
            if (existingCanvas != null)
            {
                Destroy(existingCanvas);
            }

            var canvasObject = new GameObject(CanvasName, typeof(RectTransform));
            canvasObject.layer = UiLayer;
            SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene);

            badgeCanvas = canvasObject.AddComponent<Canvas>();
            badgeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            badgeCanvas.overrideSorting = true;
            badgeCanvas.sortingOrder = CanvasSortingOrder;
            badgeCanvas.pixelPerfect = false;
            badgeCanvas.targetDisplay = 0;

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

        private void BuildBadge()
        {
            if (badgeCanvas == null)
            {
                return;
            }

            badgeRoot = CreateRectObject("GuestBadgeRoot", badgeCanvas.transform);
            var rootRect = (RectTransform)badgeRoot.transform;
            // Top-left corner so we don't collide with the settings + mode-change
            // buttons (top-right) and the bookshelf/notebook/whiteboard popup
            // affordances (bottom-center / scene world).
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.sizeDelta = new Vector2(BadgeWidth, BadgeHeight);
            rootRect.anchoredPosition = new Vector2(BadgeMargin, -BadgeMargin);

            var button = CreateButton(
                badgeRoot.transform,
                "GuestBadgeButton",
                "게스트",
                CardColor,
                TextColor,
                Vector2.zero,
                new Vector2(BadgeWidth, BadgeHeight),
                true,
                BorderColor);

            var buttonRect = (RectTransform)button.transform;
            buttonRect.anchorMin = new Vector2(0f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;
            buttonRect.anchoredPosition = Vector2.zero;
            button.onClick.AddListener(OpenModal);

            var shadow = button.gameObject.AddComponent<Shadow>();
            shadow.effectColor = WithAlpha(TextColor, 0.18f);
            shadow.effectDistance = new Vector2(0f, -3f);
        }

        private void BuildModal()
        {
            if (badgeCanvas == null)
            {
                return;
            }

            modalRoot = CreateRectObject("GuestModalRoot", badgeCanvas.transform);
            StretchToParent((RectTransform)modalRoot.transform);

            var dim = CreateImage(modalRoot.transform, "Dim", DimColor);
            StretchToParent(dim.rectTransform);
            // Block clicks behind the modal so only modal buttons are interactable.
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;

            var card = CreateImage(modalRoot.transform, "Card", CardColor);
            card.sprite = roundedRectSprite;
            card.type = Image.Type.Sliced;
            var cardRect = card.rectTransform;
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(640f, 540f);
            cardRect.anchoredPosition = Vector2.zero;

            var cardShadow = card.gameObject.AddComponent<Shadow>();
            cardShadow.effectColor = WithAlpha(TextColor, 0.18f);
            cardShadow.effectDistance = new Vector2(0f, -10f);

            CreateText(
                card.transform,
                "Title",
                "게스트 데이터 저장",
                26f,
                FontStyles.Bold,
                TextColor,
                TextAlignmentOptions.Center,
                new Vector2(0f, -44f),
                new Vector2(560f, 38f));

            CreateText(
                card.transform,
                "Body",
                "게스트로 이용하던 공간, 책, 꾸미기 데이터를\n계정에 저장할 수 있어요.\n\n어떤 방식으로 시작할까요?",
                17f,
                FontStyles.Normal,
                MutedTextColor,
                TextAlignmentOptions.Center,
                new Vector2(0f, -180f),
                new Vector2(560f, 160f));

            // Buttons stacked vertically with breathing room between them so a
            // long Korean label never collides with the next button.
            var continueButton = CreateButton(
                card.transform,
                "ContinueWithDataButton",
                "기존 데이터 이어서 시작하기",
                PrimaryColor,
                Color.white,
                new Vector2(0f, -340f),
                new Vector2(440f, 60f),
                false,
                BorderColor);
            continueButton.onClick.AddListener(OnContinueWithExistingDataClicked);

            var freshButton = CreateButton(
                card.transform,
                "FreshSpaceButton",
                "새 공간으로 시작하기",
                SecondaryColor,
                TextColor,
                new Vector2(0f, -412f),
                new Vector2(440f, 60f),
                true,
                BorderColor);
            freshButton.onClick.AddListener(OnStartFreshClicked);

            var backButton = CreateButton(
                card.transform,
                "BackButton",
                "뒤로",
                CardColor,
                MutedTextColor,
                new Vector2(0f, -484f),
                new Vector2(440f, 56f),
                true,
                BorderColor);
            backButton.onClick.AddListener(OnBackClicked);

            modalRoot.SetActive(false);
        }

        // ---------------------------------------------------------------------
        // 모달 동작
        // ---------------------------------------------------------------------

        private void OpenModal()
        {
            SetModalVisible(true);
        }

        private void OnBackClicked()
        {
            SetModalVisible(false);
        }

        private void OnContinueWithExistingDataClicked()
        {
            SetModalVisible(false);

            // TODO: Once the server-side migration endpoint exists, hook the
            // upload of the local guest snapshot (room layout + book metadata
            // + decoration items) into the post-login flow so the freshly
            // linked account inherits the player's guest progress. The player
            // first needs an account, so we go straight to the login screen
            // and let the regular login / signup flow take it from there.
            SessionState.MarkGuest();
            SceneFlowManager.GoToLogin();
        }

        private void OnStartFreshClicked()
        {
            SetModalVisible(false);

            // TODO: Once the account-link UX exists, mark the guest snapshot
            // as discarded and reset to an empty room on the new account.
            // For now, the player needs to authenticate first, so redirect
            // to the login screen and let the regular login / signup flow
            // proceed; the discard step will hook in there later.
            SessionState.MarkGuest();
            SceneFlowManager.GoToLogin();
        }

        private void SetModalVisible(bool isVisible)
        {
            if (modalRoot == null)
            {
                return;
            }

            if (isVisible)
            {
                modalRoot.transform.SetAsLastSibling();
            }

            modalRoot.SetActive(isVisible);
            UpdateBadgeVisibility();
        }

        // ---------------------------------------------------------------------
        // UI 헬퍼 (다른 Bootstrap 컨트롤러와 동일한 패턴)
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
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
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
        // 폰트 / 스프라이트
        // ---------------------------------------------------------------------

        private void ApplyFontToCanvas()
        {
            if (badgeCanvas == null || fontAsset == null)
            {
                return;
            }

            var tmpTexts = badgeCanvas.GetComponentsInChildren<TMP_Text>(true);
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
                    GlyphRenderMode.SDFAA,
                    1024,
                    1024,
                    AtlasPopulationMode.Dynamic,
                    true);

                if (dynamicFontAsset == null)
                {
                    return false;
                }

                dynamicFontAsset.name = $"{sourceFontAsset.name}_GuestBadgeDynamic";
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

            const string commonText =
                "게스트" +
                "게스트 데이터 저장" +
                "게스트로 이용하던 공간, 책, 꾸미기 데이터를" +
                "계정에 저장할 수 있어요." +
                "어떤 방식으로 시작할까요?" +
                "기존 데이터 이어서 시작하기" +
                "새 공간으로 시작하기" +
                "뒤로";

            targetFontAsset.TryAddCharacters(commonText, out _);
        }

        private Sprite CreateWhiteSprite()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "GuestBadgeUiWhite";
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            sprite.name = "GuestBadgeUiWhiteSprite";
            return sprite;
        }

        private Sprite CreateRoundedRectSprite(int size, int radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "GuestBadgeUiRounded";
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

            sprite.name = "GuestBadgeUiRoundedSprite";
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
            return ColorUtility.TryParseHtmlString(htmlColor, out var color) ? color : Color.magenta;
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
