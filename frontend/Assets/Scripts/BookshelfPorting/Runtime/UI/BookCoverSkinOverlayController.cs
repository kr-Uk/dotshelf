using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace BookshelfPorting.Runtime.UI
{
    // UI Toolkit overlay — 책 표지 스킨 변경.
    // 책 상세에서 진입, 이 책에 적용할 표지 스킨(기본 / 스폰서 표지)을 고른다.
    // 표지 미리보기 이미지는 정적 Resources 에셋 — controller 가 Resources.Load 로 로드.
    //
    // 흐름:
    //   1) Awake 시 Pretendard + design tokens USS + view USS 로드, PanelSettings 동적 생성 후 UIDocument attach
    //   2) Manager 가 콜백 props 설정 후 Show(snapshot) 호출
    //   3) 표지 카드 click → OptionSelected(index) / 닫기·backdrop → Closed
    //   4) Manager 가 적용 후 UpdateSnapshot 으로 selected 카드 갱신
    //
    // 책장관리 overlay(BookshelfManageOverlayController)와 동일 패턴.

    public readonly struct BookCoverSkinOptionItem
    {
        public string Label { get; }
        public string Description { get; }
        public string ResourcePath { get; }   // 빈 문자열 = 기본(이미지 없음)
        public bool IsSelected { get; }

        public BookCoverSkinOptionItem(string label, string description, string resourcePath, bool isSelected)
        {
            Label = label;
            Description = description;
            ResourcePath = resourcePath;
            IsSelected = isSelected;
        }
    }

    public readonly struct BookCoverSkinSnapshot
    {
        public string Hint { get; }
        public IReadOnlyList<BookCoverSkinOptionItem> Options { get; }

        public BookCoverSkinSnapshot(string hint, IReadOnlyList<BookCoverSkinOptionItem> options)
        {
            Hint = hint;
            Options = options;
        }
    }

    public sealed class BookCoverSkinOverlayController : MonoBehaviour
    {
        private const string UxmlResourcePath = "Bookshelf/BookCoverSkinView";
        private const string UssResourcePath = "Bookshelf/BookCoverSkinView";
        private const string DesignTokensResourcePath = "UI/DotshelfDesignTokens";
        private const int OverlaySortingOrder = 5900;
        private const string HiddenClass = "hidden";
        private const string SelectedClass = "selected";
        private const string OptionClass = "shelf-skin-option";
        private const string CoverClass = "shelf-skin-cover";
        private const string OptionLabelClass = "shelf-skin-option-label";
        private const string OptionDescClass = "shelf-skin-option-desc";
        private const string OptionBadgeClass = "shelf-skin-option-badge";

        private UIDocument document;
        private VisualElement root;
        private VisualElement backdrop;
        private Button closeButton;
        private Label hintLabel;
        private VisualElement optionsRow;

        public event Action Closed;

        public Action<int> OptionSelected { get; set; }

        public bool IsVisible => root != null && !root.ClassListContains(HiddenClass);

        private void Awake()
        {
            var visualTree = Resources.Load<VisualTreeAsset>(UxmlResourcePath);
            if (visualTree == null)
            {
                Debug.LogWarning($"[BookCoverSkinOverlay] VisualTreeAsset not found at Resources/{UxmlResourcePath}.uxml — overlay disabled.");
                enabled = false;
                return;
            }

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "BookCoverSkinOverlayPanelSettings (runtime)";
            panelSettings.sortingOrder = OverlaySortingOrder;
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            panelSettings.targetDisplay = 0;

            BorrowThemeStyleSheet(panelSettings);

            document = gameObject.GetComponent<UIDocument>();
            if (document == null)
            {
                document = gameObject.AddComponent<UIDocument>();
            }

            document.panelSettings = panelSettings;
            document.visualTreeAsset = visualTree;

            var uiFont = BookshelfPorting.Runtime.RuntimeUiFont.Resolve();
            if (uiFont != null)
            {
                document.rootVisualElement.style.unityFont = new StyleFont(uiFont);
            }

            var tokensSheet = Resources.Load<StyleSheet>(DesignTokensResourcePath);
            if (tokensSheet != null)
            {
                document.rootVisualElement.styleSheets.Add(tokensSheet);
            }
            else
            {
                Debug.LogWarning($"[BookCoverSkinOverlay] Design tokens not found at Resources/{DesignTokensResourcePath}.uss — overlay 가 token 없이 fallback 으로 렌더링됩니다.");
            }

            var styleSheet = Resources.Load<StyleSheet>(UssResourcePath);
            if (styleSheet != null)
            {
                document.rootVisualElement.styleSheets.Add(styleSheet);
            }
            else
            {
                Debug.LogWarning($"[BookCoverSkinOverlay] StyleSheet not found at Resources/{UssResourcePath}.uss — overlay will render without custom styles.");
            }

            QueryElements();

            if (root == null)
            {
                enabled = false;
                return;
            }

            if (!root.ClassListContains(HiddenClass))
            {
                root.AddToClassList(HiddenClass);
            }

            WireCallbacks();
        }

        private void QueryElements()
        {
            var docRoot = document.rootVisualElement;
            root = docRoot.Q<VisualElement>("root");
            backdrop = docRoot.Q<VisualElement>("backdrop");
            closeButton = docRoot.Q<Button>("close-button");
            hintLabel = docRoot.Q<Label>("hint");
            optionsRow = docRoot.Q<VisualElement>("options");
        }

        private void WireCallbacks()
        {
            if (closeButton != null) closeButton.clicked += OnCloseClicked;
            if (backdrop != null) backdrop.RegisterCallback<ClickEvent>(OnBackdropClicked);
        }

        private void OnDestroy()
        {
            if (backdrop != null)
            {
                backdrop.UnregisterCallback<ClickEvent>(OnBackdropClicked);
            }

            // 코드로 ScriptableObject.CreateInstance<PanelSettings>() 로 만든 인스턴스 — 명시적으로 Destroy 하지 않으면
            // scene reload / play mode toggle 누적 leak. (themeStyleSheet 는 빌려온 reference 라 건드리지 않음.)
            if (document != null && document.panelSettings != null)
            {
                var settings = document.panelSettings;
                document.panelSettings = null;
                Destroy(settings);
            }
        }

        public void Show(BookCoverSkinSnapshot snapshot)
        {
            if (root == null)
            {
                return;
            }

            ApplySnapshotData(snapshot);
            root.RemoveFromClassList(HiddenClass);
        }

        public void UpdateSnapshot(BookCoverSkinSnapshot snapshot)
        {
            if (root == null || root.ClassListContains(HiddenClass))
            {
                return;
            }

            ApplySnapshotData(snapshot);
        }

        public void Hide()
        {
            if (root == null)
            {
                return;
            }

            root.AddToClassList(HiddenClass);
        }

        private void ApplySnapshotData(BookCoverSkinSnapshot snapshot)
        {
            if (hintLabel != null)
            {
                hintLabel.text = snapshot.Hint ?? string.Empty;
            }

            RebuildOptions(snapshot.Options);
        }

        private void RebuildOptions(IReadOnlyList<BookCoverSkinOptionItem> options)
        {
            if (optionsRow == null)
            {
                return;
            }

            optionsRow.Clear();
            if (options == null)
            {
                return;
            }

            for (var i = 0; i < options.Count; i++)
            {
                optionsRow.Add(BuildOptionCard(options[i], i));
            }
        }

        private VisualElement BuildOptionCard(BookCoverSkinOptionItem item, int index)
        {
            var card = new Button();
            card.AddToClassList(OptionClass);
            if (item.IsSelected)
            {
                card.AddToClassList(SelectedClass);
            }

            var optionIndex = index;
            card.clicked += () => OptionSelected?.Invoke(optionIndex);

            var cover = new Image();
            cover.AddToClassList(CoverClass);
            cover.scaleMode = ScaleMode.ScaleToFit;
            cover.pickingMode = PickingMode.Ignore;
            // 빈 ResourcePath("기본")는 이미지 없이 paper 톤 빈 사각형 — USS 배경으로 표현.
            if (!string.IsNullOrEmpty(item.ResourcePath))
            {
                var sprite = Resources.Load<Sprite>(item.ResourcePath);
                if (sprite != null)
                {
                    cover.sprite = sprite;
                }
            }
            card.Add(cover);

            var label = new Label(string.IsNullOrEmpty(item.Label) ? "(이름 없음)" : item.Label);
            label.AddToClassList(OptionLabelClass);
            label.pickingMode = PickingMode.Ignore;
            card.Add(label);

            var desc = new Label(item.Description ?? string.Empty);
            desc.AddToClassList(OptionDescClass);
            desc.pickingMode = PickingMode.Ignore;
            card.Add(desc);

            var badge = new Label("✓ 적용 중");
            badge.AddToClassList(OptionBadgeClass);
            badge.pickingMode = PickingMode.Ignore;
            card.Add(badge);

            return card;
        }

        private void OnCloseClicked()
        {
            Hide();
            Closed?.Invoke();
        }

        private void OnBackdropClicked(ClickEvent evt)
        {
            if (evt.target != backdrop)
            {
                return;
            }

            Hide();
            Closed?.Invoke();
        }

        // 코드 생성 PanelSettings 는 themeStyleSheet 가 비어 있어 기본 컨트롤이 unstyled 로 뜬다.
        // 씬의 다른 UIDocument 에서 theme 을 빌려와 일관된 기본 스타일을 확보.
        private static void BorrowThemeStyleSheet(PanelSettings target)
        {
            var existingDocs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < existingDocs.Length; i++)
            {
                var doc = existingDocs[i];
                if (doc == null || doc.panelSettings == null || doc.panelSettings == target)
                {
                    continue;
                }

                var theme = doc.panelSettings.themeStyleSheet;
                if (theme != null)
                {
                    target.themeStyleSheet = theme;
                    return;
                }
            }
        }
    }
}
