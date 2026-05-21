using System;
using App.Application.Notebook;
using App.Auth;
using App.Infrastructure.Api;
using App.Infrastructure.Api.Dtos;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace App.Bootstrap
{
    // Top-right overlay shown only while visiting another user's space.
    //
    // Owner detection runs against AuthSession.UserId — if the visited space's
    // OwnerUserId matches the local user, the overlay stays hidden so that
    // visiting your own space (the default GetMySpace response on MainScene
    // boot) never surfaces a "follow yourself" affordance.
    //
    // Lives on its own Canvas (sortingOrder above the bookshelf experience
    // manager's HUD) so the overlay survives camera transitions / focus mode
    // changes without coupling to BookshelfExperienceManager. The like button
    // intentionally stays anchored top-right on every frame regardless of
    // 3D camera state, per spec.
    [DefaultExecutionOrder(-700)]
    [DisallowMultipleComponent]
    public sealed class SpaceVisitorOverlayController : MonoBehaviour
    {
        // Driver hook for code paths that already have a SpaceResponseDto in
        // hand (BookshelfApiSyncController.RefreshFromServer, future "visit
        // other user's space" flows). Static-singleton style keeps callers
        // from having to wire a scene reference; the surface is just one
        // method, so a richer DI/event setup is overkill.
        public static SpaceVisitorOverlayController Instance { get; private set; }

        private const string CanvasName = "SpaceVisitorOverlayCanvas";
        private const int CanvasSortingOrder = 4500;
        private const int UiLayer = 5;

        private static readonly Color CardColor = new Color(0.06f, 0.07f, 0.09f, 0.78f);
        private static readonly Color FollowIdleColor = new Color32(163, 128, 105, 255);
        private static readonly Color FollowActiveColor = new Color32(163, 128, 105, 255);
        private static readonly Color LikeIdleColor = new Color(1f, 1f, 1f, 0.88f);
        private static readonly Color LikeActiveBackgroundColor = new Color32(246, 242, 236, 245);
        private static readonly Color LikeIconColor = new Color32(217, 65, 93, 255);
        private static readonly Color LabelColor = Color.white;
        private static readonly Color ErrorColor = new Color(0.95f, 0.55f, 0.55f, 1f);

        private Canvas overlayCanvas;
        private GameObject overlayRoot;
        private Button followButton;
        private Image followBackground;
        private TMP_Text followLabel;
        private Button likeButton;
        private Image likeBackground;
        private TMP_Text likeLabel;
        private Image likeHeartImage;
        private TMP_Text likeCountLabel;
        private TMP_Text errorLabel;

        private TMP_FontAsset fontAsset;
        private Sprite roundedRectSprite;
        private Sprite heartFilledSprite;
        private Sprite heartOutlineSprite;

        // Last-known server state for the visited space. currentSpaceId /
        // currentOwnerUserId stay nullable so a missing call (overlay never
        // received a SetCurrentSpace) cannot accidentally hit the network on
        // a button click.
        private long? currentSpaceId;
        private long? currentOwnerUserId;
        private bool isLiked;
        private bool isFollowed;
        // Tri-state for follow: BE's SpaceResponse has no `following` field
        // yet, so the initial value is "unknown" until either (a) the user
        // clicks Follow once and we observe BE's ToggleResponse.active, or
        // (b) BE adds the field and SetCurrentSpace populates it. Either
        // path flips this to true and from there the toggle is normal.
        private bool isFollowKnown;
        private bool isLikeInFlight;
        private bool isFollowInFlight;
        private bool loadingBlocked;
        private bool hasVisibleVisitorSpace;
        private long currentLikeCount;

        private NotebookApiClient apiClient;

        // ---- Auto bootstrap: spawn a singleton at app start so callers do
        // not have to remember to drop the component on every scene that
        // could surface another user's space. AfterSceneLoad gives us a
        // valid EventSystem and Canvas root by the time BuildOverlay runs.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (Instance != null)
            {
                return;
            }
            // FindObjectsInactive.Include catches a hand-attached instance
            // sitting on an inactive GameObject — we should not duplicate.
            var existing = FindFirstObjectByType<SpaceVisitorOverlayController>(FindObjectsInactive.Include);
            if (existing != null)
            {
                return;
            }
            var go = new GameObject(nameof(SpaceVisitorOverlayController));
            DontDestroyOnLoad(go);
            go.AddComponent<SpaceVisitorOverlayController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Cover the hand-attach path (someone drops the component into a
            // scene) — the auto-bootstrap path already calls DontDestroyOnLoad
            // before AddComponent, but we should not assume the constructor
            // path. Only root GameObjects can be marked DDOL.
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            BuildOverlay();
            HideOverlay();
        }

        private void OnEnable()
        {
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        }

        // Defense against the rapid-scene-transition class of bugs: if the
        // user logs out (main → login) or transitions through Bootstrap, any
        // in-flight optimistic state belongs to the previous space and must
        // not bleed into the next scene. The next valid SetCurrentSpace call
        // (e.g. BookshelfApiSyncController forwarding GetMySpace) will set
        // up the correct state again.
        private void HandleSceneUnloaded(Scene _)
        {
            ResetState();
            HideOverlay();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // External entry point: callers pass the space they just navigated to.
        // Hides for own space, shows + initializes button states otherwise.
        // Idempotent — calling repeatedly with the same dto is safe.
        public void SetCurrentSpace(SpaceResponseDto space, NotebookSpaceVisitRequest visitRequest = null)
        {
            if (space == null || space.Id <= 0)
            {
                ResetState();
                HideOverlay();
                return;
            }

            currentSpaceId = space.Id;
            currentOwnerUserId = space.OwnerUserId;
            var hasMatchingVisitState = visitRequest != null &&
                                        visitRequest.SpaceId == space.Id &&
                                        visitRequest.OwnerUserId == space.OwnerUserId;
            isLiked = hasMatchingVisitState ? visitRequest.Liked : space.Liked;
            currentLikeCount = hasMatchingVisitState ? Math.Max(0L, visitRequest.LikeCount) : Math.Max(0L, space.LikeCount);
            isFollowed = hasMatchingVisitState && visitRequest.Following;
            isFollowKnown = hasMatchingVisitState;

            var myUserId = AuthSession.UserId;
            // myUserId == 0 means "guest, no own user record" — they can
            // never own a visited space, so the same-owner check below is
            // automatically false for them and the overlay shows.
            if (myUserId != 0 && currentOwnerUserId.HasValue && currentOwnerUserId.Value == myUserId)
            {
                hasVisibleVisitorSpace = false;
                HideOverlay();
                return;
            }

            // Block self-follow as a defense-in-depth: even if myUserId is 0
            // (guest), BE's follow endpoint requires a target userId > 0.
            if (!currentOwnerUserId.HasValue || currentOwnerUserId.Value <= 0)
            {
                hasVisibleVisitorSpace = false;
                HideOverlay();
                return;
            }

            hasVisibleVisitorSpace = true;
            ShowOverlay();
            RefreshButtonVisuals();
            SetMessage(string.Empty);
        }

        public void HideOverlay()
        {
            hasVisibleVisitorSpace = false;
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }
        }

        public void SetLoadingBlocked(bool blocked)
        {
            loadingBlocked = blocked;
            SetButtonsInteractable(!blocked);
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(hasVisibleVisitorSpace && !loadingBlocked);
            }
        }

        private void ShowOverlay()
        {
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(hasVisibleVisitorSpace && !loadingBlocked);
                overlayRoot.transform.SetAsLastSibling();
            }
        }

        private void ResetState()
        {
            currentSpaceId = null;
            currentOwnerUserId = null;
            isLiked = false;
            isFollowed = false;
            isFollowKnown = false;
            currentLikeCount = 0L;
            // In-flight flags too: a stale callback from a now-irrelevant
            // request must not be allowed to write to a fresh space's state.
            isFollowInFlight = false;
            isLikeInFlight = false;
            loadingBlocked = false;
            hasVisibleVisitorSpace = false;
            SetMessage(string.Empty);
        }

        // ---- Button handlers (optimistic toggle + rollback on failure) ----

        private void OnFollowButtonClicked()
        {
            if (loadingBlocked || isFollowInFlight || !currentOwnerUserId.HasValue || currentOwnerUserId.Value <= 0)
            {
                return;
            }

            // Capture previous state BEFORE flipping so a failure can restore
            // exactly what the user saw a moment ago. `previousKnown` is part
            // of that snapshot — a failed first-click must roll back not just
            // the bool but also the "we now know the server state" claim.
            var previousFollowed = isFollowed;
            var previousKnown = isFollowKnown;

            // First click on an unknown follow state: force POST /follow even
            // if the user is already following on BE. BE's follow endpoint is
            // idempotent, and the response's `active` becomes our source of
            // truth (handled in OnComplete). After that, the toggle is normal.
            bool willCallFollow = !previousKnown || !previousFollowed;

            isFollowed = willCallFollow;
            RefreshButtonVisuals();
            SetMessage(string.Empty);

#if UNITY_EDITOR
            if (DevMainSceneMode.IsActive)
            {
                // Dev mode: optimistic UI only, no BE round-trip. Mark known
                // so a second click toggles in the opposite direction next
                // time (matches the user's expectation of a working toggle
                // even without a backend).
                isFollowKnown = true;
                return;
            }
#endif

            var targetUserId = currentOwnerUserId.Value;
            var client = EnsureApiClient();
            if (client == null)
            {
                // APIManager not available (real mode) — keep optimistic
                // visual but do not pretend the BE accepted the change. We
                // also avoid setting in-flight so the user can retry once
                // APIManager is up. EnsureApiClient already logged a warn.
                return;
            }

            isFollowInFlight = true;
            SetButtonsInteractable(!loadingBlocked);

            void OnFailure(ApiErrorResponseDto error)
            {
                isFollowInFlight = false;
                if (!currentOwnerUserId.HasValue || currentOwnerUserId.Value != targetUserId)
                {
                    return;
                }
                isFollowed = previousFollowed;
                isFollowKnown = previousKnown;
                RefreshButtonVisuals();
                SetMessage("처리에 실패했습니다. 다시 시도해주세요.");
                Debug.LogWarning(
                    $"[SpaceVisitorOverlay] Follow toggle failed: code={error?.Code} message={error?.Message}");
            }

            void OnComplete(ToggleResponseDto response)
            {
                isFollowInFlight = false;
                if (!currentOwnerUserId.HasValue || currentOwnerUserId.Value != targetUserId)
                {
                    return;
                }
                if (response != null)
                {
                    // BE truth: a follow that was already true returns
                    // active=true; a fresh unfollow returns active=false.
                    // Either way, this is the value to trust from now on.
                    isFollowed = response.Active;
                    isFollowKnown = true;
                    RefreshButtonVisuals();
                }
            }

            if (willCallFollow)
            {
                client.FollowUser(targetUserId, OnComplete, OnFailure);
            }
            else
            {
                client.UnfollowUser(targetUserId, OnComplete, OnFailure);
            }
        }

        private void OnLikeButtonClicked()
        {
            if (loadingBlocked || isLikeInFlight || !currentSpaceId.HasValue || currentSpaceId.Value <= 0)
            {
                return;
            }

            var previous = isLiked;
            var previousCount = currentLikeCount;
            isLiked = !previous;
            currentLikeCount = Math.Max(0L, currentLikeCount + (isLiked ? 1L : -1L));
            RefreshButtonVisuals();
            SetMessage(string.Empty);

#if UNITY_EDITOR
            if (DevMainSceneMode.IsActive)
            {
                // Dev mode: optimistic UI only, no BE round-trip.
                return;
            }
#endif

            var targetSpaceId = currentSpaceId.Value;
            var client = EnsureApiClient();
            if (client == null)
            {
                return;
            }

            isLikeInFlight = true;
            SetButtonsInteractable(!loadingBlocked);

            void OnFailure(ApiErrorResponseDto error)
            {
                isLikeInFlight = false;
                if (!currentSpaceId.HasValue || currentSpaceId.Value != targetSpaceId)
                {
                    return;
                }
                isLiked = previous;
                currentLikeCount = previousCount;
                RefreshButtonVisuals();
                SetMessage("처리에 실패했습니다. 다시 시도해주세요.");
                Debug.LogWarning(
                    $"[SpaceVisitorOverlay] Like toggle failed: code={error?.Code} message={error?.Message}");
            }

            void OnComplete(ToggleResponseDto response)
            {
                isLikeInFlight = false;
                if (!currentSpaceId.HasValue || currentSpaceId.Value != targetSpaceId)
                {
                    return;
                }
                if (response != null)
                {
                    isLiked = response.Active;
                    currentLikeCount = Math.Max(0L, response.Count);
                    RefreshButtonVisuals();
                }
            }

            if (previous)
            {
                client.UnlikeSpace(targetSpaceId, OnComplete, OnFailure);
            }
            else
            {
                client.LikeSpace(targetSpaceId, OnComplete, OnFailure);
            }
        }

        // Returns null if APIManager is not available — callers must handle
        // null without throwing. Dev mode is handled at the call site (above
        // EnsureApiClient) so that even if APIManager happens to exist in a
        // dev session, the BE call is still skipped.
        private NotebookApiClient EnsureApiClient()
        {
            if (APIManager.Instance == null)
            {
                Debug.LogWarning("[SpaceVisitorOverlay] APIManager.Instance is null — BE call skipped.");
                return null;
            }
            if (apiClient == null)
            {
                apiClient = new NotebookApiClient(APIManager.Instance);
            }
            return apiClient;
        }

        // ---- UI build ----

        private void BuildOverlay()
        {
            roundedRectSprite = CreateRoundedRectSprite(48, 12);
            heartFilledSprite = CreateHeartSprite(64, true);
            heartOutlineSprite = CreateHeartSprite(64, false);
            fontAsset = ResolveFontAsset();

            var canvasObject = new GameObject(CanvasName, typeof(RectTransform));
            canvasObject.layer = UiLayer;
            DontDestroyOnLoad(canvasObject);
            // SceneManager move avoided on purpose — DontDestroyOnLoad keeps
            // the overlay alive across scene transitions (e.g. logout →
            // login → main again would otherwise rebuild on every entry).
            // Attach as child of this controller so disposing the controller
            // disposes the canvas.
            canvasObject.transform.SetParent(transform, false);

            overlayCanvas = canvasObject.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = CanvasSortingOrder;
            overlayCanvas.pixelPerfect = false;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            EnsureEventSystem();

            overlayRoot = CreateRect("OverlayRoot", canvasObject.transform);
            var rootRect = (RectTransform)overlayRoot.transform;
            rootRect.anchorMin = new Vector2(1f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(1f, 1f);
            rootRect.anchoredPosition = new Vector2(-28f, -28f);
            rootRect.sizeDelta = new Vector2(286f, 70f);

            var card = CreateImage(overlayRoot.transform, "Card", CardColor);
            card.sprite = roundedRectSprite;
            card.type = Image.Type.Sliced;
            card.raycastTarget = false;
            StretchToParent(card.rectTransform);

            var horizontal = CreateRect("ButtonRow", overlayRoot.transform);
            var horizontalRect = (RectTransform)horizontal.transform;
            horizontalRect.anchorMin = new Vector2(0f, 0f);
            horizontalRect.anchorMax = new Vector2(1f, 1f);
            horizontalRect.offsetMin = new Vector2(12f, 12f);
            horizontalRect.offsetMax = new Vector2(-12f, -12f);
            var layoutGroup = horizontal.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.spacing = 12f;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = true;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = true;

            (followButton, followBackground, followLabel) = CreatePillButton(
                horizontal.transform, "FollowButton", "팔로우", FollowIdleColor, new Vector2(142f, 0f));
            followButton.onClick.AddListener(OnFollowButtonClicked);

            (likeButton, likeBackground, likeLabel) = CreatePillButton(
                horizontal.transform, "LikeButton", "♡", LikeIdleColor, new Vector2(92f, 0f));
            likeButton.onClick.AddListener(OnLikeButtonClicked);
            if (likeLabel != null)
            {
                likeLabel.gameObject.SetActive(false);
            }
            BuildLikeButtonContent(likeButton.transform);

            errorLabel = CreateText(overlayRoot.transform, "ErrorLabel", string.Empty, 13f, ErrorColor, TextAlignmentOptions.Center);
            var errorRect = errorLabel.rectTransform;
            errorRect.anchorMin = new Vector2(0f, 0f);
            errorRect.anchorMax = new Vector2(1f, 0f);
            errorRect.pivot = new Vector2(0.5f, 1f);
            errorRect.anchoredPosition = new Vector2(0f, -4f);
            errorRect.sizeDelta = new Vector2(0f, 22f);
            errorLabel.gameObject.SetActive(false);
        }

        private (Button button, Image background, TMP_Text label) CreatePillButton(
            Transform parent, string objectName, string text, Color backgroundColor, Vector2 preferredSize)
        {
            var image = CreateImage(parent, objectName, backgroundColor);
            image.sprite = roundedRectSprite;
            image.type = Image.Type.Sliced;

            var layoutElement = image.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = preferredSize.x;
            layoutElement.minWidth = preferredSize.x;
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 1f;

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.85f, 0.85f, 0.85f, 0.6f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var label = CreateText(image.transform, "Label", text, 16f, LabelColor, TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
            label.raycastTarget = false;
            StretchToParent(label.rectTransform);

            return (button, image, label);
        }

        private void BuildLikeButtonContent(Transform parent)
        {
            var contentRoot = CreateRect("LikeContent", parent);
            var contentRect = (RectTransform)contentRoot.transform;
            StretchToParent(contentRect);
            var layout = contentRoot.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            likeHeartImage = CreateImage(contentRoot.transform, "HeartIcon", LikeIconColor);
            likeHeartImage.sprite = heartOutlineSprite;
            likeHeartImage.preserveAspect = true;
            likeHeartImage.raycastTarget = false;
            var iconRect = likeHeartImage.rectTransform;
            iconRect.sizeDelta = new Vector2(22f, 22f);
            var iconLayout = likeHeartImage.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = 22f;
            iconLayout.preferredHeight = 22f;
            iconLayout.minWidth = 22f;
            iconLayout.minHeight = 22f;

            likeCountLabel = CreateText(contentRoot.transform, "LikeCount", "0", 15f, LikeIconColor, TextAlignmentOptions.MidlineLeft);
            likeCountLabel.fontStyle = FontStyles.Bold;
            likeCountLabel.raycastTarget = false;
            var countLayout = likeCountLabel.gameObject.AddComponent<LayoutElement>();
            countLayout.preferredWidth = 26f;
            countLayout.minWidth = 18f;
            countLayout.preferredHeight = 22f;
        }

        private void RefreshButtonVisuals()
        {
            if (followLabel != null)
            {
                followLabel.text = isFollowed ? "팔로잉" : "팔로우";
            }
            if (followBackground != null)
            {
                followBackground.color = isFollowed ? FollowActiveColor : FollowIdleColor;
            }
            if (likeLabel != null)
            {
                likeLabel.text = $"{(isLiked ? "♥" : "♡")} {currentLikeCount:N0}";
                likeLabel.color = LikeIconColor;
            }
            if (likeHeartImage != null)
            {
                likeHeartImage.sprite = isLiked ? heartFilledSprite : heartOutlineSprite;
                likeHeartImage.color = LikeIconColor;
            }
            if (likeCountLabel != null)
            {
                likeCountLabel.text = currentLikeCount.ToString("N0");
                likeCountLabel.color = LikeIconColor;
            }
            if (likeBackground != null)
            {
                likeBackground.color = isLiked ? LikeActiveBackgroundColor : LikeIdleColor;
            }
            SetButtonsInteractable(!loadingBlocked);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (followButton != null)
            {
                followButton.interactable = interactable && !isFollowInFlight;
            }
            if (likeButton != null)
            {
                likeButton.interactable = interactable && !isLikeInFlight;
            }
        }

        private void SetMessage(string message)
        {
            if (errorLabel == null)
            {
                return;
            }
            if (string.IsNullOrEmpty(message))
            {
                errorLabel.text = string.Empty;
                errorLabel.gameObject.SetActive(false);
            }
            else
            {
                errorLabel.text = message;
                errorLabel.gameObject.SetActive(true);
            }
        }

        // ---- Helper / styling primitives ----

        private static GameObject CreateRect(string objectName, Transform parent)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.layer = UiLayer;
            go.transform.SetParent(parent, false);
            return go;
        }

        private Image CreateImage(Transform parent, string objectName, Color color)
        {
            var go = CreateRect(objectName, parent);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.type = Image.Type.Simple;
            image.raycastTarget = true;
            return image;
        }

        private TMP_Text CreateText(Transform parent, string objectName, string text, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            var go = CreateRect(objectName, parent);
            var label = go.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null)
            {
                label.font = fontAsset;
            }
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
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

        private static Sprite CreateHeartSprite(int size, bool filled)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = filled ? "SpaceVisitorHeartFilled" : "SpaceVisitorHeartOutline",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            var solid = new Color32(255, 255, 255, 255);
            var transparent = new Color32(255, 255, 255, 0);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var inside = IsInsideHeart(x, y, size);
                    var visible = filled ? inside : inside && IsHeartEdge(x, y, size);
                    pixels[(y * size) + x] = visible ? solid : transparent;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect);
        }

        private static bool IsHeartEdge(int x, int y, int size)
        {
            const int radius = 2;
            for (var oy = -radius; oy <= radius; oy++)
            {
                for (var ox = -radius; ox <= radius; ox++)
                {
                    if (ox == 0 && oy == 0)
                    {
                        continue;
                    }
                    if (!IsInsideHeart(x + ox, y + oy, size))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool IsInsideHeart(int x, int y, int size)
        {
            if (x < 0 || y < 0 || x >= size || y >= size)
            {
                return false;
            }

            var normalizedX = (((x + 0.5f) / size) - 0.5f) * 2.55f;
            var normalizedY = (((y + 0.5f) / size) - 0.42f) * 2.45f;
            var value = Math.Pow((normalizedX * normalizedX) + (normalizedY * normalizedY) - 1f, 3) -
                        (normalizedX * normalizedX * normalizedY * normalizedY * normalizedY);
            return value <= 0d;
        }

        private static Sprite CreateRoundedRectSprite(int size, int radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "SpaceVisitorOverlayRounded",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

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
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect,
                border);
        }

        private static bool IsInsideRoundedRect(int x, int y, int size, int radius)
        {
            if (x >= radius && x < size - radius) return true;
            if (y >= radius && y < size - radius) return true;
            var cx = x < radius ? radius - 1 : size - radius;
            var cy = y < radius ? radius - 1 : size - radius;
            var dx = x - cx;
            var dy = y - cy;
            return (dx * dx) + (dy * dy) <= radius * radius;
        }

        private static TMP_FontAsset ResolveFontAsset()
        {
            // Best effort: pick whatever default the project's TMP settings
            // expose. If null, TMP_Text falls back at runtime; the only
            // visible glyphs (Korean labels + ♡♥) are widely supported.
            var defaults = TMP_Settings.defaultFontAsset;
            return defaults;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<StandaloneInputModule>();
        }
    }
}
