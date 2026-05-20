using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace BookshelfPorting.Runtime.UI
{
    // UI Toolkit overlay — 광고책 추가 (책장 선택).
    // 책 상세에서 "책장에 추가하기" 진입 시, 추가할 대상 책장을 고르는 picker.
    //
    // 흐름:
    //   1) Awake 시 Pretendard + design tokens USS + view USS 로드, PanelSettings 동적 생성 후 UIDocument attach
    //   2) Manager 가 콜백 props 설정 후 Show(snapshot) 호출
    //   3) 책장 행 click → ShelfSelected(index) / 닫기·backdrop → Closed
    //
    // 책장관리 overlay(BookshelfManageOverlayController)와 동일 패턴.

    public readonly struct AdBookAddShelfItem
    {
        public int Index { get; }
        public string Name { get; }
        public bool CanUse { get; }   // 서버 배치 준비 여부 — false 면 행 disabled

        public AdBookAddShelfItem(int index, string name, bool canUse)
        {
            Index = index;
            Name = name;
            CanUse = canUse;
        }
    }

    public readonly struct AdBookAddSnapshot
    {
        public IReadOnlyList<AdBookAddShelfItem> Shelves { get; }
        public string Status { get; }

        public AdBookAddSnapshot(IReadOnlyList<AdBookAddShelfItem> shelves, string status)
        {
            Shelves = shelves;
            Status = status;
        }
    }

    public sealed class AdBookAddOverlayController : MonoBehaviour
    {
        private const string UxmlResourcePath = "Bookshelf/AdBookAddView";
        private const string UssResourcePath = "Bookshelf/AdBookAddView";
        private const string DesignTokensResourcePath = "UI/DotshelfDesignTokens";
        private const int OverlaySortingOrder = 5900;
        private const string HiddenClass = "hidden";
        private const string ShelfRowClass = "shelf-ad-shelf-row";
        private const string ShelfNameClass = "shelf-ad-shelf-name";
        private const string ShelfArrowClass = "shelf-ad-shelf-arrow";
        private const string EmptyClass = "shelf-ad-empty";

        private UIDocument document;
        private VisualElement root;
        private VisualElement backdrop;
        private Button closeButton;
        private ScrollView shelfList;
        private Label statusLabel;

        public event Action Closed;

        public Action<int> ShelfSelected { get; set; }   // 책장 인덱스

        public bool IsVisible => root != null && !root.ClassListContains(HiddenClass);

        private void Awake()
        {
            var visualTree = Resources.Load<VisualTreeAsset>(UxmlResourcePath);
            if (visualTree == null)
            {
                Debug.LogWarning($"[AdBookAddOverlay] VisualTreeAsset not found at Resources/{UxmlResourcePath}.uxml — overlay disabled.");
                enabled = false;
                return;
            }

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "AdBookAddOverlayPanelSettings (runtime)";
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
                Debug.LogWarning($"[AdBookAddOverlay] Design tokens not found at Resources/{DesignTokensResourcePath}.uss — overlay 가 token 없이 fallback 으로 렌더링됩니다.");
            }

            var styleSheet = Resources.Load<StyleSheet>(UssResourcePath);
            if (styleSheet != null)
            {
                document.rootVisualElement.styleSheets.Add(styleSheet);
            }
            else
            {
                Debug.LogWarning($"[AdBookAddOverlay] StyleSheet not found at Resources/{UssResourcePath}.uss — overlay will render without custom styles.");
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
            shelfList = docRoot.Q<ScrollView>("shelf-list");
            if (shelfList != null)
            {
                shelfList.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                shelfList.mode = ScrollViewMode.Vertical;
            }
            statusLabel = docRoot.Q<Label>("status");
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

        public void Show(AdBookAddSnapshot snapshot)
        {
            if (root == null)
            {
                return;
            }

            ApplySnapshotData(snapshot);
            root.RemoveFromClassList(HiddenClass);
        }

        public void UpdateSnapshot(AdBookAddSnapshot snapshot)
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

        private void ApplySnapshotData(AdBookAddSnapshot snapshot)
        {
            RebuildShelfList(snapshot.Shelves);
            if (statusLabel != null)
            {
                statusLabel.text = snapshot.Status ?? string.Empty;
            }
        }

        private void RebuildShelfList(IReadOnlyList<AdBookAddShelfItem> shelves)
        {
            if (shelfList == null)
            {
                return;
            }

            shelfList.Clear();
            if (shelves == null || shelves.Count == 0)
            {
                var empty = new Label("표시할 책장이 없습니다.");
                empty.AddToClassList(EmptyClass);
                empty.pickingMode = PickingMode.Ignore;
                shelfList.Add(empty);
                return;
            }

            for (var i = 0; i < shelves.Count; i++)
            {
                shelfList.Add(BuildShelfRow(shelves[i]));
            }
        }

        private VisualElement BuildShelfRow(AdBookAddShelfItem item)
        {
            var row = new Button();
            row.AddToClassList(ShelfRowClass);
            row.SetEnabled(item.CanUse);
            var index = item.Index;
            row.clicked += () => ShelfSelected?.Invoke(index);

            var name = new Label(string.IsNullOrEmpty(item.Name) ? "(이름 없음)" : item.Name);
            name.AddToClassList(ShelfNameClass);
            name.pickingMode = PickingMode.Ignore;
            row.Add(name);

            var arrow = new Label("›");
            arrow.AddToClassList(ShelfArrowClass);
            arrow.pickingMode = PickingMode.Ignore;
            row.Add(arrow);

            return row;
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
