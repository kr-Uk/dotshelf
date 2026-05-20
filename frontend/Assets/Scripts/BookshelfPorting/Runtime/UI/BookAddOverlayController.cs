using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace BookshelfPorting.Runtime.UI
{
    // UI Toolkit overlay — 책 추가.
    // 검색 / 추천 / 랭킹 모드로 책을 둘러보고 현재(또는 선택한) 책장에 추가할 책을 고른다.
    //
    // 흐름:
    //   1) Awake 시 Pretendard + design tokens USS + view USS 로드, PanelSettings 동적 생성 후 UIDocument attach
    //   2) Manager 가 콜백 props 설정 후 Show(snapshot) 호출
    //   3) 모드 탭 → ModeSelected / 검색 input → QueryChanged / 책장 chip → ShelfSelected
    //      결과 행 click → ResultSelected(catalogIndex) / 닫기·backdrop → Closed
    //   4) Manager 가 RefreshBookAddPanel 에서 카탈로그 조회 후 UpdateSnapshot 으로 결과 갱신
    //
    // 검색어는 overlay 의 TextField 가 소유 — Manager 는 Query 프로퍼티로 읽는다.
    // 결과 행의 책 표지 이미지는 후속 task (현재 텍스트 위주).
    // 책장관리 overlay(BookshelfManageOverlayController)와 동일 패턴.

    public readonly struct BookAddRowItem
    {
        public int CatalogIndex { get; }
        public string Title { get; }
        public string Meta { get; }       // "저자 · 출판사"
        public string Summary { get; }
        public string Stats { get; }      // "조회 · 좋아요 · 책장 추가" (없으면 빈 문자열)
        public bool IsAlreadyAdded { get; }

        public BookAddRowItem(int catalogIndex, string title, string meta, string summary, string stats, bool isAlreadyAdded)
        {
            CatalogIndex = catalogIndex;
            Title = title;
            Meta = meta;
            Summary = summary;
            Stats = stats;
            IsAlreadyAdded = isAlreadyAdded;
        }
    }

    public readonly struct BookAddSnapshot
    {
        public string Title { get; }
        public string Hint { get; }
        public int ShelfCount { get; }
        public int ActiveShelfIndex { get; }
        public int ActiveMode { get; }     // 0=검색, 1=추천, 2=랭킹
        public bool SearchEnabled { get; } // 검색 input 활성 여부 (검색 모드 + 추가 가능)
        public bool CanAdd { get; }        // 결과 행 클릭 가능 여부
        public IReadOnlyList<BookAddRowItem> Rows { get; }
        public string Status { get; }
        public bool ResetScroll { get; }

        public BookAddSnapshot(
            string title,
            string hint,
            int shelfCount,
            int activeShelfIndex,
            int activeMode,
            bool searchEnabled,
            bool canAdd,
            IReadOnlyList<BookAddRowItem> rows,
            string status,
            bool resetScroll)
        {
            Title = title;
            Hint = hint;
            ShelfCount = shelfCount;
            ActiveShelfIndex = activeShelfIndex;
            ActiveMode = activeMode;
            SearchEnabled = searchEnabled;
            CanAdd = canAdd;
            Rows = rows;
            Status = status;
            ResetScroll = resetScroll;
        }
    }

    public sealed class BookAddOverlayController : MonoBehaviour
    {
        private const string UxmlResourcePath = "Bookshelf/BookAddView";
        private const string UssResourcePath = "Bookshelf/BookAddView";
        private const string DesignTokensResourcePath = "UI/DotshelfDesignTokens";
        private const int OverlaySortingOrder = 5900;
        private const string HiddenClass = "hidden";
        private const string SelectedClass = "selected";
        private const string AddedClass = "is-added";
        private const string ShelfChipClass = "shelf-add-shelf-chip";
        private const string ResultRowClass = "shelf-add-result-row";
        private const string ResultTitleClass = "shelf-add-result-title";
        private const string ResultMetaClass = "shelf-add-result-meta";
        private const string ResultSummaryClass = "shelf-add-result-summary";
        private const string ResultStatsClass = "shelf-add-result-stats";
        private const string ResultBadgeClass = "shelf-add-result-badge";
        private const string EmptyClass = "shelf-add-empty";

        private UIDocument document;
        private VisualElement root;
        private VisualElement backdrop;
        private Button closeButton;
        private Label titleLabel;
        private Label hintLabel;
        private VisualElement shelfSelectorRow;
        private Button modeSearch;
        private Button modeRecommended;
        private Button modeRanking;
        private TextField searchField;
        private ScrollView resultsScroll;
        private Label statusLabel;

        public event Action Closed;

        public Action QueryChanged { get; set; }
        public Action<int> ModeSelected { get; set; }      // 0=검색, 1=추천, 2=랭킹
        public Action<int> ShelfSelected { get; set; }
        public Action<int> ResultSelected { get; set; }    // catalogIndex

        public bool IsVisible => root != null && !root.ClassListContains(HiddenClass);

        // 검색어는 overlay 의 TextField 가 단일 소유 — Manager 가 RefreshBookAddPanel 에서 읽는다.
        public string Query => searchField != null ? (searchField.value ?? string.Empty) : string.Empty;

        private void Awake()
        {
            var visualTree = Resources.Load<VisualTreeAsset>(UxmlResourcePath);
            if (visualTree == null)
            {
                Debug.LogWarning($"[BookAddOverlay] VisualTreeAsset not found at Resources/{UxmlResourcePath}.uxml — overlay disabled.");
                enabled = false;
                return;
            }

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "BookAddOverlayPanelSettings (runtime)";
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
                Debug.LogWarning($"[BookAddOverlay] Design tokens not found at Resources/{DesignTokensResourcePath}.uss — overlay 가 token 없이 fallback 으로 렌더링됩니다.");
            }

            var styleSheet = Resources.Load<StyleSheet>(UssResourcePath);
            if (styleSheet != null)
            {
                document.rootVisualElement.styleSheets.Add(styleSheet);
            }
            else
            {
                Debug.LogWarning($"[BookAddOverlay] StyleSheet not found at Resources/{UssResourcePath}.uss — overlay will render without custom styles.");
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
            titleLabel = docRoot.Q<Label>("title");
            hintLabel = docRoot.Q<Label>("hint");
            shelfSelectorRow = docRoot.Q<VisualElement>("shelf-selector");
            modeSearch = docRoot.Q<Button>("mode-search");
            modeRecommended = docRoot.Q<Button>("mode-recommended");
            modeRanking = docRoot.Q<Button>("mode-ranking");
            searchField = docRoot.Q<TextField>("search-input");
            resultsScroll = docRoot.Q<ScrollView>("results");
            if (resultsScroll != null)
            {
                resultsScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                resultsScroll.mode = ScrollViewMode.Vertical;
            }
            statusLabel = docRoot.Q<Label>("status");
        }

        private void WireCallbacks()
        {
            if (closeButton != null) closeButton.clicked += OnCloseClicked;
            if (backdrop != null) backdrop.RegisterCallback<ClickEvent>(OnBackdropClicked);
            if (searchField != null) searchField.RegisterValueChangedCallback(_ => QueryChanged?.Invoke());
            if (modeSearch != null) modeSearch.clicked += () => ModeSelected?.Invoke(0);
            if (modeRecommended != null) modeRecommended.clicked += () => ModeSelected?.Invoke(1);
            if (modeRanking != null) modeRanking.clicked += () => ModeSelected?.Invoke(2);
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

        // Modal 첫 진입 — 검색어를 초기화한 뒤 데이터 그림.
        public void Show(BookAddSnapshot snapshot)
        {
            if (root == null)
            {
                return;
            }

            // 새 진입은 빈 검색어로 시작 — QueryChanged 가 안 터지도록 SetValueWithoutNotify.
            searchField?.SetValueWithoutNotify(string.Empty);
            ApplySnapshotData(snapshot);
            root.RemoveFromClassList(HiddenClass);
        }

        // Visible 상태에서 데이터만 갱신 — 검색어(TextField)는 건드리지 않는다(사용자 입력 보호).
        public void UpdateSnapshot(BookAddSnapshot snapshot)
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

        private void ApplySnapshotData(BookAddSnapshot snapshot)
        {
            if (titleLabel != null)
            {
                titleLabel.text = string.IsNullOrEmpty(snapshot.Title) ? "책 추가" : snapshot.Title;
            }

            if (hintLabel != null)
            {
                hintLabel.text = snapshot.Hint ?? string.Empty;
            }

            RebuildShelfChips(snapshot.ShelfCount, snapshot.ActiveShelfIndex);

            ApplyModeTab(modeSearch, 0, snapshot.ActiveMode);
            ApplyModeTab(modeRecommended, 1, snapshot.ActiveMode);
            ApplyModeTab(modeRanking, 2, snapshot.ActiveMode);

            if (searchField != null)
            {
                searchField.SetEnabled(snapshot.SearchEnabled);
            }

            RebuildResults(snapshot.Rows, snapshot.CanAdd);
            if (snapshot.ResetScroll && resultsScroll != null)
            {
                resultsScroll.scrollOffset = Vector2.zero;
            }

            if (statusLabel != null)
            {
                statusLabel.text = snapshot.Status ?? string.Empty;
            }
        }

        private static void ApplyModeTab(Button tab, int index, int activeMode)
        {
            if (tab == null)
            {
                return;
            }

            if (index == activeMode)
            {
                if (!tab.ClassListContains(SelectedClass))
                {
                    tab.AddToClassList(SelectedClass);
                }
            }
            else
            {
                tab.RemoveFromClassList(SelectedClass);
            }
        }

        private void RebuildShelfChips(int count, int activeIndex)
        {
            if (shelfSelectorRow == null)
            {
                return;
            }

            shelfSelectorRow.Clear();
            for (var i = 0; i < count; i++)
            {
                var index = i;
                var isActive = i == activeIndex;
                var chip = new Button { text = $"{i + 1}번" };
                chip.AddToClassList(ShelfChipClass);
                if (isActive)
                {
                    chip.AddToClassList(SelectedClass);
                }

                chip.SetEnabled(!isActive);
                chip.clicked += () => ShelfSelected?.Invoke(index);
                shelfSelectorRow.Add(chip);
            }
        }

        private void RebuildResults(IReadOnlyList<BookAddRowItem> rows, bool canAdd)
        {
            if (resultsScroll == null)
            {
                return;
            }

            resultsScroll.Clear();
            if (rows == null || rows.Count == 0)
            {
                var empty = new Label("표시할 책이 없습니다.");
                empty.AddToClassList(EmptyClass);
                empty.pickingMode = PickingMode.Ignore;
                resultsScroll.Add(empty);
                return;
            }

            for (var i = 0; i < rows.Count; i++)
            {
                resultsScroll.Add(BuildResultRow(rows[i], canAdd));
            }
        }

        private VisualElement BuildResultRow(BookAddRowItem item, bool canAdd)
        {
            var row = new Button();
            row.AddToClassList(ResultRowClass);
            if (item.IsAlreadyAdded)
            {
                row.AddToClassList(AddedClass);
            }

            // 이미 추가된 책 / 추가 불가 상태는 클릭 차단.
            row.SetEnabled(canAdd && !item.IsAlreadyAdded);
            var catalogIndex = item.CatalogIndex;
            row.clicked += () => ResultSelected?.Invoke(catalogIndex);

            var title = new Label(string.IsNullOrEmpty(item.Title) ? "(제목 없음)" : item.Title);
            title.AddToClassList(ResultTitleClass);
            title.pickingMode = PickingMode.Ignore;
            row.Add(title);

            var meta = new Label(item.Meta ?? string.Empty);
            meta.AddToClassList(ResultMetaClass);
            meta.pickingMode = PickingMode.Ignore;
            row.Add(meta);

            if (!string.IsNullOrEmpty(item.Summary))
            {
                var summary = new Label(item.Summary);
                summary.AddToClassList(ResultSummaryClass);
                summary.pickingMode = PickingMode.Ignore;
                row.Add(summary);
            }

            if (!string.IsNullOrEmpty(item.Stats))
            {
                var stats = new Label(item.Stats);
                stats.AddToClassList(ResultStatsClass);
                stats.pickingMode = PickingMode.Ignore;
                row.Add(stats);
            }

            if (item.IsAlreadyAdded)
            {
                var badge = new Label("이미 이 책장에 있음");
                badge.AddToClassList(ResultBadgeClass);
                badge.pickingMode = PickingMode.Ignore;
                row.Add(badge);
            }

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
