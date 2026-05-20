using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BookshelfPorting.Runtime
{
    internal readonly struct BookDetailPanelCallbacks
    {
        public BookDetailPanelCallbacks(
            UnityAction openWriteReport,
            UnityAction openReadReport,
            UnityAction openPlacementEditor,
            UnityAction toggleReadState,
            UnityAction toggleLikeState,
            UnityAction cycleOwnerRating,
            UnityAction toggleVisibility,
            UnityAction addAdvertisementBook,
            UnityAction close,
            UnityAction deleteBook)
        {
            OpenWriteReport = openWriteReport;
            OpenReadReport = openReadReport;
            OpenPlacementEditor = openPlacementEditor;
            ToggleReadState = toggleReadState;
            ToggleLikeState = toggleLikeState;
            CycleOwnerRating = cycleOwnerRating;
            ToggleVisibility = toggleVisibility;
            AddAdvertisementBook = addAdvertisementBook;
            Close = close;
            DeleteBook = deleteBook;
        }

        public UnityAction OpenWriteReport { get; }
        public UnityAction OpenReadReport { get; }
        public UnityAction OpenPlacementEditor { get; }
        public UnityAction ToggleReadState { get; }
        public UnityAction ToggleLikeState { get; }
        public UnityAction CycleOwnerRating { get; }
        public UnityAction ToggleVisibility { get; }
        public UnityAction AddAdvertisementBook { get; }
        public UnityAction Close { get; }
        public UnityAction DeleteBook { get; }
    }

    internal readonly struct BookDetailPanelViewModel
    {
        public BookDetailPanelViewModel(
            string identityKey,
            string title,
            string meta,
            string averageRating,
            string totalLikes,
            string reviewCount,
            string description,
            bool hasReport,
            bool canDeleteBook,
            bool canUseOwnerActions,
            bool canAddAdvertisementBook,
            bool isLiked,
            bool isAdvertisementBook,
            bool isConfirmingDelete)
        {
            IdentityKey = identityKey;
            Title = title;
            Meta = meta;
            AverageRating = averageRating;
            TotalLikes = totalLikes;
            ReviewCount = reviewCount;
            Description = description;
            HasReport = hasReport;
            CanDeleteBook = canDeleteBook;
            CanUseOwnerActions = canUseOwnerActions;
            CanAddAdvertisementBook = canAddAdvertisementBook;
            IsLiked = isLiked;
            IsAdvertisementBook = isAdvertisementBook;
            IsConfirmingDelete = isConfirmingDelete;
        }

        public string IdentityKey { get; }
        public string Title { get; }
        public string Meta { get; }
        public string AverageRating { get; }
        public string TotalLikes { get; }
        public string ReviewCount { get; }
        public string Description { get; }
        public bool HasReport { get; }
        public bool CanDeleteBook { get; }
        public bool CanUseOwnerActions { get; }
        public bool CanAddAdvertisementBook { get; }
        public bool IsLiked { get; }
        public bool IsAdvertisementBook { get; }
        public bool IsConfirmingDelete { get; }
    }

    internal sealed class BookDetailPanelController
    {
        private const float OverlayButtonBottomPaddingExtra = 12f;
        private const string LikeReactionIconPath = "Assets/App/UI/Icons/Reactions/icon_like_heart.png";

        private static readonly Color32 DeleteButtonBackground = new(196, 78, 72, 255);
        private static readonly Color32 DeleteButtonHighlighted = new(176, 62, 58, 255);
        private static readonly Color32 DeleteButtonConfirmBackground = new(176, 50, 48, 255);
        private static readonly Color32 DeleteButtonConfirmHighlighted = new(148, 39, 38, 255);
        private static readonly Color32 DeleteButtonDisabledBackground = new(224, 219, 214, 130);
        private static readonly Color32 DeleteButtonBorder = new(236, 178, 168, 190);
        private static readonly Color32 DeleteButtonDisabledText = new(142, 133, 124, 190);
        private static readonly Color32 LikeButtonIdleBackground = new(247, 243, 238, 235);
        private static readonly Color32 LikeButtonIdleHighlight = new(255, 249, 244, 255);
        private static readonly Color32 LikeButtonActiveBackground = new(128, 74, 106, 242);
        private static readonly Color32 LikeButtonActiveHighlight = new(150, 88, 122, 255);
        private static readonly Color32 LikeButtonDisabledBackground = new(224, 219, 214, 140);
        private static readonly Color32 LikeIconIdleColor = new(232, 226, 235, 255);
        private static readonly Color32 LikeCountIdleColor = new(86, 67, 93, 255);
        private static readonly Color32 LikeActiveColor = new(247, 173, 194, 255);
        private static Sprite cachedLikeReactionSprite;
        private static Texture2D fallbackLikeReactionTexture;

        private readonly Text eyebrowText;
        private readonly Text titleText;
        private readonly Text metaText;
        private readonly Text averageRatingChipText;
        private readonly Text totalLikeChipText;
        private readonly Text reviewCountChipText;
        private readonly Text descriptionText;
        private readonly ScrollRect descriptionScrollRect;
        private readonly RectTransform descriptionContent;
        private readonly Scrollbar descriptionScrollbar;
        private readonly Button readToggleButton;
        private readonly Button likeToggleButton;
        private Image likeToggleIconImage;
        private TMP_Text likeToggleCountText;
        private readonly Button ratingCycleButton;
        private readonly Button visibilityToggleButton;
        private readonly Button writeReportButton;
        private readonly Button readReportButton;
        private readonly Button addAdvertisementBookButton;
        private readonly Button placementButton;
        private readonly Button closeButton;
        private readonly Button deleteButton;
        private string renderedIdentityKey;

        public BookDetailPanelController(Transform parent, BookDetailPanelCallbacks callbacks)
        {
            Panel = RuntimeHudFactory.CreatePanel(parent, "BookDetailPanel", new Vector2(-24f, -78f), new Vector2(380f, 436f), new Vector2(1f, 1f));
            RuntimeHudStyle.StyleBookDetailPanel(Panel.GetComponent<Image>());
            Panel.SetActive(false);

            RuntimeHudFactory.CreateImage(Panel.transform, "BookPanelAccent", new Vector2(20f, -22f), new Vector2(72f, 3f), new Vector2(0f, 1f), new Color32(197, 128, 67, 170), false);
            eyebrowText = RuntimeHudFactory.CreateLabel(Panel.transform, "BookPanelEyebrowText", new Vector2(20f, -30f), new Vector2(340f, 18f), TextAnchor.UpperLeft);
            eyebrowText.fontSize = 13;
            eyebrowText.fontStyle = FontStyle.Bold;
            eyebrowText.color = new Color32(151, 104, 64, 255);

            titleText = RuntimeHudFactory.CreateLabel(Panel.transform, "BookTitleText", new Vector2(20f, -54f), new Vector2(340f, 46f), TextAnchor.UpperLeft);
            titleText.fontSize = 22;
            titleText.fontStyle = FontStyle.Bold;
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            titleText.verticalOverflow = VerticalWrapMode.Truncate;
            titleText.color = new Color32(49, 42, 36, 255);

            metaText = RuntimeHudFactory.CreateLabel(Panel.transform, "BookMetaText", new Vector2(20f, -112f), new Vector2(340f, 72f), TextAnchor.UpperLeft);
            metaText.fontSize = 14;
            metaText.horizontalOverflow = HorizontalWrapMode.Wrap;
            metaText.color = new Color32(84, 74, 66, 255);
            metaText.lineSpacing = 1.08f;

            RuntimeHudFactory.CreateImage(Panel.transform, "BookAverageRatingChip", new Vector2(20f, -190f), new Vector2(106f, 32f), new Vector2(0f, 1f), new Color32(255, 248, 236, 230));
            averageRatingChipText = RuntimeHudFactory.CreateLabel(Panel.transform, "BookAverageRatingChipText", new Vector2(20f, -190f), new Vector2(106f, 32f), TextAnchor.MiddleCenter);
            StyleChipText(averageRatingChipText, new Color32(63, 47, 38, 255));

            RuntimeHudFactory.CreateImage(Panel.transform, "BookTotalLikeChip", new Vector2(136f, -190f), new Vector2(106f, 32f), new Vector2(0f, 1f), new Color32(238, 248, 241, 225));
            totalLikeChipText = RuntimeHudFactory.CreateLabel(Panel.transform, "BookTotalLikeChipText", new Vector2(136f, -190f), new Vector2(106f, 32f), TextAnchor.MiddleCenter);
            StyleChipText(totalLikeChipText, new Color32(53, 94, 73, 255));

            RuntimeHudFactory.CreateImage(Panel.transform, "BookReviewCountChip", new Vector2(252f, -190f), new Vector2(106f, 32f), new Vector2(0f, 1f), new Color32(245, 237, 226, 230));
            reviewCountChipText = RuntimeHudFactory.CreateLabel(Panel.transform, "BookReviewCountChipText", new Vector2(252f, -190f), new Vector2(106f, 32f), TextAnchor.MiddleCenter);
            StyleChipText(reviewCountChipText, new Color32(84, 74, 66, 255));

            RuntimeHudFactory.CreateImage(Panel.transform, "BookPanelDivider", new Vector2(20f, -198f), new Vector2(340f, 1f), new Vector2(0f, 1f), new Color32(205, 186, 168, 126));
            var descriptionViewport = RuntimeHudFactory.CreateScrollViewport(Panel.transform, "BookDescriptionViewport", new Vector2(20f, -214f), new Vector2(328f, 52f), new Vector2(0f, 1f));
            descriptionScrollRect = descriptionViewport.GetComponent<ScrollRect>();
            descriptionContent = RuntimeHudFactory.CreateScrollContent(descriptionViewport.transform, "BookDescriptionContent", new Vector2(328f, 52f));
            descriptionScrollbar = RuntimeHudFactory.CreateVerticalScrollbar(Panel.transform, "BookDescriptionScrollbar", new Vector2(352f, -214f), new Vector2(8f, 52f), new Vector2(0f, 1f));
            RuntimeHudStyle.StyleScrollbar(descriptionScrollbar, new Color32(151, 104, 64, 220), new Color32(238, 231, 221, 150));
            if (descriptionScrollRect != null)
            {
                descriptionScrollRect.viewport = descriptionViewport.GetComponent<RectTransform>();
                descriptionScrollRect.content = descriptionContent;
                descriptionScrollRect.verticalScrollbar = descriptionScrollbar;
                descriptionScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            }

            descriptionText = RuntimeHudFactory.CreateLabel(descriptionContent, "BookPanelHintText", Vector2.zero, new Vector2(328f, 52f), TextAnchor.UpperLeft);
            descriptionText.fontSize = 13;
            descriptionText.color = new Color32(124, 111, 101, 255);
            descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descriptionText.verticalOverflow = VerticalWrapMode.Overflow;

            writeReportButton = RuntimeHudFactory.CreateButton(Panel.transform, "WriteReportButton", "리뷰 작성", new Vector2(20f, -282f), callbacks.OpenWriteReport, new Vector2(104f, 32f), new Vector2(0f, 1f));
            readReportButton = RuntimeHudFactory.CreateButton(Panel.transform, "ReadReportButton", "독후감 보기", new Vector2(136f, -282f), callbacks.OpenReadReport, new Vector2(104f, 32f), new Vector2(0f, 1f));
            addAdvertisementBookButton = RuntimeHudFactory.CreateButton(Panel.transform, "AddAdvertisementBookButton", "책장에 추가하기", new Vector2(136f, -282f), callbacks.AddAdvertisementBook, new Vector2(104f, 32f), new Vector2(0f, 1f));
            placementButton = RuntimeHudFactory.CreateButton(Panel.transform, "DetailPlacementButton", "배치 변경", new Vector2(252f, -282f), callbacks.OpenPlacementEditor, new Vector2(108f, 32f), new Vector2(0f, 1f));
            readToggleButton = RuntimeHudFactory.CreateButton(Panel.transform, "ReadToggleButton", "상태 변경", new Vector2(20f, -322f), callbacks.ToggleReadState, new Vector2(164f, 34f), new Vector2(0f, 1f));
            likeToggleButton = RuntimeHudFactory.CreateButton(Panel.transform, "LikeToggleButton", "좋아요", new Vector2(196f, -322f), callbacks.ToggleLikeState, new Vector2(164f, 34f), new Vector2(0f, 1f));
            ratingCycleButton = RuntimeHudFactory.CreateButton(Panel.transform, "RatingCycleButton", "별점", new Vector2(20f, -360f), callbacks.CycleOwnerRating, new Vector2(164f, 34f), new Vector2(0f, 1f));
            visibilityToggleButton = RuntimeHudFactory.CreateButton(Panel.transform, "VisibilityToggleButton", "공개 범위 변경", new Vector2(196f, -360f), callbacks.ToggleVisibility, new Vector2(164f, 34f), new Vector2(0f, 1f));
            closeButton = RuntimeHudFactory.CreateButton(Panel.transform, "CloseBookDetailButton", "닫기", new Vector2(20f, -400f), callbacks.Close, new Vector2(340f, 32f), new Vector2(0f, 1f));
            deleteButton = RuntimeHudFactory.CreateButton(Panel.transform, "DeleteDetailBookButton", "책 삭제", new Vector2(20f, -400f), callbacks.DeleteBook, new Vector2(340f, 32f), new Vector2(0f, 1f));
        }

        public GameObject Panel { get; }

        public bool IsVisible => Panel != null && Panel.activeSelf;

        public void SetActive(bool isActive)
        {
            if (Panel != null)
            {
                Panel.SetActive(isActive);
                if (!isActive)
                {
                    renderedIdentityKey = null;
                }
            }
        }

        public void Refresh(BookDetailPanelViewModel model)
        {
            if (!IsVisible)
            {
                return;
            }

            titleText.text = model.Title;
            eyebrowText.text = "책 상세";
            metaText.text = model.Meta;
            averageRatingChipText.text = $"★ {model.AverageRating}";
            totalLikeChipText.text = $"좋아요 {model.TotalLikes}개";
            reviewCountChipText.text = $"리뷰 {model.ReviewCount}개";
            descriptionText.text = model.Description;
            var nextIdentityKey = string.IsNullOrWhiteSpace(model.IdentityKey) ? model.Title : model.IdentityKey;
            var resetDescriptionScroll = !string.Equals(renderedIdentityKey, nextIdentityKey, System.StringComparison.Ordinal);
            renderedIdentityKey = nextIdentityKey;
            RefreshDescriptionScrollContent(resetDescriptionScroll);

            SetButtonActive(readToggleButton, false);
            var canToggleLike = model.CanUseOwnerActions && !model.IsAdvertisementBook;
            SetButtonActive(likeToggleButton, canToggleLike);
            likeToggleButton.interactable = canToggleLike;
            RuntimeHudStyle.SetButtonLabel(likeToggleButton, "좋아요");
            RefreshLikeToggleDisplay(model.TotalLikes, model.IsLiked);
            SetButtonActive(ratingCycleButton, false);
            SetButtonActive(visibilityToggleButton, false);

            SetButtonActive(writeReportButton, model.CanUseOwnerActions && !model.IsAdvertisementBook);
            writeReportButton.interactable = model.CanUseOwnerActions && !model.IsAdvertisementBook;
            RuntimeHudStyle.SetButtonLabel(writeReportButton, "리뷰 작성");
            StyleStandardButton(writeReportButton, true, 13);

            SetButtonActive(readReportButton, !model.IsAdvertisementBook);
            readReportButton.interactable = model.HasReport;
            StyleStandardButton(readReportButton, model.HasReport, 13);

            SetButtonActive(addAdvertisementBookButton, model.CanAddAdvertisementBook);
            addAdvertisementBookButton.interactable = model.CanAddAdvertisementBook;
            StyleStandardButton(addAdvertisementBookButton, model.CanAddAdvertisementBook, 13);

            SetButtonActive(deleteButton, model.CanDeleteBook);
            deleteButton.interactable = model.CanDeleteBook;
            RuntimeHudStyle.SetButtonLabel(deleteButton, model.IsConfirmingDelete ? "한 번 더 눌러 제거" : "책장에서 제거");
            StyleDeleteButton(deleteButton, model.CanDeleteBook, model.IsConfirmingDelete, 13);

            SetButtonActive(placementButton, false);
            placementButton.interactable = false;
            StyleStandardButton(placementButton, true, 13);

            SetButtonActive(closeButton, false);
            RuntimeHudStyle.StyleButton(
                closeButton,
                new Color32(245, 237, 226, 245),
                new Color32(255, 248, 236, 255),
                new Color32(224, 219, 214, 140),
                14,
                new Color32(84, 74, 66, 255),
                true);
        }

        public void ApplyResponsiveLayout(float panelWidth, float panelHeight)
        {
            var padding = Mathf.Clamp(panelWidth * 0.058f, 26f, 36f);
            var contentWidth = Mathf.Max(320f, panelWidth - padding * 2f);
            RuntimeHudLayout.SetChildRect(Panel, "BookPanelAccent", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -22f), new Vector2(84f, 3f));
            RuntimeHudLayout.SetChildActive(Panel, "BookPanelAccent", false);
            var topActionY = -30f;
            var likeButtonWidth = 72f;
            var reviewButtonWidth = 112f;
            var topActionGap = 8f;
            var topActionsWidth = likeButtonWidth + topActionGap + reviewButtonWidth;
            var headingWidth = Mathf.Max(96f, contentWidth - topActionsWidth - 14f);
            SetComponentRect(eyebrowText, new Vector2(padding, -30f), new Vector2(headingWidth, 18f));
            SetComponentRect(titleText, new Vector2(padding, -54f), new Vector2(headingWidth, 56f));
            SetComponentRect(metaText, new Vector2(padding, -122f), new Vector2(contentWidth, 68f));

            SetComponentRect(likeToggleButton, new Vector2(padding + contentWidth - likeButtonWidth, topActionY), new Vector2(likeButtonWidth, 32f));
            SetComponentRect(writeReportButton, new Vector2(padding + contentWidth - likeButtonWidth - topActionGap - reviewButtonWidth, topActionY), new Vector2(reviewButtonWidth, 32f));

            var chipGap = Mathf.Clamp(contentWidth * 0.024f, 8f, 12f);
            var chipWidth = (contentWidth - chipGap * 2f) / 3f;
            var chipHeight = 34f;
            var chipY = -202f;
            RuntimeHudLayout.SetChildRect(Panel, "BookAverageRatingChip", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, chipY), new Vector2(chipWidth, chipHeight));
            SetComponentRect(averageRatingChipText, new Vector2(padding, chipY), new Vector2(chipWidth, chipHeight));
            RuntimeHudLayout.SetChildRect(Panel, "BookTotalLikeChip", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + chipWidth + chipGap, chipY), new Vector2(chipWidth, chipHeight));
            SetComponentRect(totalLikeChipText, new Vector2(padding + chipWidth + chipGap, chipY), new Vector2(chipWidth, chipHeight));
            RuntimeHudLayout.SetChildRect(Panel, "BookReviewCountChip", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding + (chipWidth + chipGap) * 2f, chipY), new Vector2(chipWidth, chipHeight));
            SetComponentRect(reviewCountChipText, new Vector2(padding + (chipWidth + chipGap) * 2f, chipY), new Vector2(chipWidth, chipHeight));
            RuntimeHudLayout.SetChildRect(Panel, "BookPanelDivider", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padding, -250f), new Vector2(contentWidth, 1f));
            RuntimeHudLayout.SetChildActive(Panel, "BookPanelDivider", false);

            var buttonHeight = 34f;
            var actionY = -(panelHeight - padding - OverlayButtonBottomPaddingExtra - buttonHeight);
            var deleteY = actionY + buttonHeight + 10f;
            var hintY = -268f;
            var hintHeight = Mathf.Max(92f, -deleteY - Mathf.Abs(hintY) - 18f);

            var scrollbarWidth = 8f;
            var scrollbarGap = 6f;
            var descriptionWidth = Mathf.Max(80f, contentWidth - scrollbarWidth - scrollbarGap);
            SetComponentRect(descriptionScrollRect, new Vector2(padding, hintY), new Vector2(descriptionWidth, hintHeight));
            SetComponentRect(descriptionScrollbar, new Vector2(padding + descriptionWidth + scrollbarGap, hintY), new Vector2(scrollbarWidth, hintHeight));
            SetComponentRect(descriptionText, Vector2.zero, new Vector2(descriptionWidth, hintHeight));
            SetComponentRect(readReportButton, new Vector2(padding, actionY), new Vector2(contentWidth, buttonHeight));
            SetComponentRect(addAdvertisementBookButton, new Vector2(padding, actionY), new Vector2(contentWidth, buttonHeight));
            SetComponentRect(deleteButton, new Vector2(padding, deleteY), new Vector2(contentWidth, buttonHeight));
            RefreshDescriptionScrollContent(false);
        }

        private void EnsureLikeToggleContent()
        {
            if (likeToggleButton == null || (likeToggleIconImage != null && likeToggleCountText != null))
            {
                return;
            }

            (likeToggleIconImage, likeToggleCountText) = BuildLikeToggleContent(likeToggleButton);
        }

        private static (Image IconImage, TMP_Text CountText) BuildLikeToggleContent(Button button)
        {
            if (button == null)
            {
                return (null, null);
            }

            var defaultLabel = button.transform.Find("Label");
            if (defaultLabel != null)
            {
                defaultLabel.gameObject.SetActive(false);
            }

            var contentRect = RuntimeHudFactory.CreateContainer(
                button.transform,
                "LikeReactionContent",
                Vector2.zero,
                Vector2.zero,
                new Vector2(0.5f, 0.5f));
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.offsetMin = new Vector2(8f, 3f);
            contentRect.offsetMax = new Vector2(-8f, -3f);

            var layout = contentRect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 6f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var iconObject = new GameObject("LikeHeartIcon");
            iconObject.transform.SetParent(contentRect, false);
            var icon = iconObject.AddComponent<Image>();
            icon.sprite = GetLikeReactionSprite();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var iconLayout = iconObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = 16f;
            iconLayout.preferredHeight = 16f;
            iconLayout.minWidth = 16f;
            iconLayout.minHeight = 16f;

            var countObject = new GameObject("LikeCountText");
            countObject.transform.SetParent(contentRect, false);
            var countText = countObject.AddComponent<TextMeshProUGUI>();
            countText.text = "0";
            countText.fontSize = 15f;
            countText.fontStyle = FontStyles.Bold;
            countText.alignment = TextAlignmentOptions.Center;
            countText.margin = Vector4.zero;
            countText.raycastTarget = false;
            countText.textWrappingMode = TextWrappingModes.NoWrap;
            countText.overflowMode = TextOverflowModes.Overflow;
            var countLayout = countObject.AddComponent<LayoutElement>();
            countLayout.minWidth = 10f;
            countLayout.preferredWidth = 22f;
            countLayout.preferredHeight = 18f;

            return (icon, countText);
        }

        private void RefreshLikeToggleDisplay(string totalLikes, bool isLiked)
        {
            EnsureLikeToggleContent();
            RuntimeHudStyle.StyleButton(
                likeToggleButton,
                isLiked ? LikeButtonActiveBackground : LikeButtonIdleBackground,
                isLiked ? LikeButtonActiveHighlight : LikeButtonIdleHighlight,
                LikeButtonDisabledBackground,
                18,
                isLiked ? LikeActiveColor : LikeCountIdleColor,
                true);

            var iconColor = isLiked ? LikeActiveColor : LikeIconIdleColor;
            if (likeToggleIconImage != null)
            {
                likeToggleIconImage.sprite = GetLikeReactionSprite();
                likeToggleIconImage.color = iconColor;
            }

            if (likeToggleCountText != null)
            {
                likeToggleCountText.text = string.IsNullOrWhiteSpace(totalLikes) ? "0" : totalLikes;
                likeToggleCountText.color = isLiked ? LikeActiveColor : LikeCountIdleColor;
            }
        }

        private static Sprite GetLikeReactionSprite()
        {
            if (cachedLikeReactionSprite != null)
            {
                return cachedLikeReactionSprite;
            }

#if UNITY_EDITOR
            cachedLikeReactionSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(LikeReactionIconPath);
            if (cachedLikeReactionSprite != null)
            {
                return cachedLikeReactionSprite;
            }
#endif

            cachedLikeReactionSprite = CreateFallbackLikeReactionSprite();
            return cachedLikeReactionSprite;
        }

        private static Sprite CreateFallbackLikeReactionSprite()
        {
            const int size = 64;
            fallbackLikeReactionTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "GeneratedLikeHeartIcon",
                hideFlags = HideFlags.HideAndDontSave
            };
            fallbackLikeReactionTexture.wrapMode = TextureWrapMode.Clamp;
            fallbackLikeReactionTexture.filterMode = FilterMode.Bilinear;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var px = (x + 0.5f) / size * 2f - 1f;
                    var py = (y + 0.5f) / size * 2f - 1f + 0.08f;
                    var heart = Mathf.Pow(px * px + py * py - 0.42f, 3f) - px * px * py * py * py;
                    var filledAlpha = Mathf.Clamp01((0.018f - heart) * 42f);
                    var innerAlpha = Mathf.Clamp01((-0.045f - heart) * 42f);
                    var alpha = Mathf.Clamp01(filledAlpha - innerAlpha);
                    fallbackLikeReactionTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            fallbackLikeReactionTexture.Apply();
            var sprite = Sprite.Create(
                fallbackLikeReactionTexture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = "GeneratedLikeHeartIcon";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private void RefreshDescriptionScrollContent(bool resetToTop)
        {
            if (descriptionText == null || descriptionContent == null || descriptionScrollRect == null)
            {
                return;
            }

            var viewportHeight = RuntimeHudLayout.GetScrollViewportHeight(descriptionScrollRect, Mathf.Max(52f, descriptionText.rectTransform.rect.height));
            var contentWidth = descriptionScrollRect.viewport != null && descriptionScrollRect.viewport.rect.width > 1f
                ? descriptionScrollRect.viewport.rect.width
                : Mathf.Max(1f, descriptionText.rectTransform.rect.width);
            var preferredHeight = Mathf.Max(viewportHeight, descriptionText.preferredHeight + 8f);
            descriptionContent.sizeDelta = new Vector2(0f, preferredHeight);
            RuntimeHudLayout.SetComponentRect(descriptionText, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(contentWidth, preferredHeight));
            RuntimeHudLayout.ApplyScrollState(descriptionScrollRect, descriptionScrollbar, descriptionContent, preferredHeight > viewportHeight + 1f, resetToTop, 24f);
        }

        private static void StyleChipText(Text text, Color32 color)
        {
            text.fontSize = 13;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static void StyleStandardButton(Button button, bool isEnabled, int fontSize)
        {
            RuntimeHudStyle.StyleButton(
                button,
                isEnabled ? new Color32(255, 255, 255, 245) : new Color32(238, 231, 221, 142),
                new Color32(241, 235, 230, 255),
                new Color32(224, 219, 214, 120),
                fontSize,
                isEnabled ? new Color32(63, 47, 38, 255) : new Color32(124, 111, 101, 142),
                true);
        }

        private static void StyleDeleteButton(Button button, bool isEnabled, bool isConfirming, int fontSize)
        {
            RuntimeHudStyle.StyleButton(
                button,
                isEnabled
                    ? isConfirming ? DeleteButtonConfirmBackground : DeleteButtonBackground
                    : DeleteButtonDisabledBackground,
                isEnabled
                    ? isConfirming ? DeleteButtonConfirmHighlighted : DeleteButtonHighlighted
                    : DeleteButtonDisabledBackground,
                DeleteButtonDisabledBackground,
                fontSize,
                isEnabled ? Color.white : DeleteButtonDisabledText,
                true);
            RuntimeHudStyle.AddSoftOutline(button.gameObject, isEnabled ? DeleteButtonBorder : new Color32(255, 255, 255, 90), new Vector2(1f, -1f));
        }

        private static void SetButtonActive(Button button, bool isActive)
        {
            if (button != null)
            {
                button.gameObject.SetActive(isActive);
            }
        }

        private static void SetComponentRect(Component target, Vector2 anchoredPosition, Vector2 size)
        {
            RuntimeHudLayout.SetComponentRect(target, new Vector2(0f, 1f), new Vector2(0f, 1f), anchoredPosition, size);
            if (target is Button button)
            {
                RuntimeHudLayout.InsetTextToParent(RuntimeHudStyle.GetButtonLabelText(button), 10f, 3f);
            }
        }
    }
}
