using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace BookshelfPorting.Runtime.UI
{
    // UI Toolkit overlay — 책 찾기.
    // 활성(또는 선택한) 책장의 책을 제목/저자로 검색 + 읽기상태 필터(전체/읽는중/완독/좋아요).
    //
    // 흐름:
    //   1) Awake 시 Pretendard + design tokens USS + view USS 로드, PanelSettings 동적 생성 후 UIDocument attach
    //   2) Manager 가 콜백 props 설정 후 Show(snapshot) 호출
    //   3) 검색 input 변경 → QueryChanged / 필터 chip → FilterSelected / 책장 chip → ShelfSelected
    //      결과 행 click → ResultSelected(slot) / 닫기·backdrop → Closed
    //   4) Manager 가 RefreshBookSearchPanel 에서 검색 로직 수행 후 UpdateSnapshot 으로 결과 갱신
    //
    // 검색어는 overlay 의 TextField 가 소유 — Manager 는 Query 프로퍼티로 읽는다.
    // 책장관리 overlay(BookshelfManageOverlayController)와 동일 패턴.

    public readonly struct BookSearchRowItem
    {
        public int Slot { get; }       // bookSearchResults 인덱스
        public string Title { get; }
        public string Meta { get; }    // "저자 · 상태"

        public BookSearchRowItem(int slot, string title, string meta)
        {
            Slot = slot;
            Title = title;
            Meta = meta;
        }
    }

    public readonly struct BookSearchSnapshot
    {
        public string Title { get; }
        public int ShelfCount { get; }
        public int ActiveShelfIndex { get; }
        public int ActiveFilter { get; }   // 0=전체, 1=읽는중, 2=완독, 3=좋아요
        public int AllCount { get; }
        public int ReadingCount { get; }
        public int ReadCount { get; }
        public int LikedCount { get; }
        public IReadOnlyList<BookSearchRowItem> Rows { get; }
        public string Status { get; }
        public bool ResetScroll { get; }

        public BookSearchSnapshot(
            string title,
            int shelfCount,
            int activeShelfIndex,
            int activeFilter,
            int allCount,
            int readingCount,
            int readCount,
            int likedCount,
            IReadOnlyList<BookSearchRowItem> rows,
            string status,
            bool resetScroll)
        {
            Title = title;
            ShelfCount = shelfCount;
            ActiveShelfIndex = activeShelfIndex;
            ActiveFilter = activeFilter;
            AllCount = allCount;
            ReadingCount = readingCount;
            ReadCount = readCount;
            LikedCount = likedCount;
            Rows = rows;
            Status = status;
            ResetScroll = resetScroll;
        }
    }

    public sealed class BookSearchOverlayController : MonoBehaviour
    {
        private const string UxmlResourcePath = "Bookshelf/BookSearchView";
        private const string UssResourcePath = "Bookshelf/BookSearchView";
        private const string DesignTokensResourcePath = "UI/DotshelfDesignTokens";
        private const int OverlaySortingOrder = 5900;
        private const string HiddenClass = "hidden";
        private const string SelectedClass = "selected";
        private const string ShelfChipClass = "shelf-search-shelf-chip";
        private const string ResultRowClass = "shelf-search-result-row";
        private const string ResultTitleClass = "shelf-search-result-title";
        private const string ResultMetaClass = "shelf-search-result-meta";
        private const string EmptyClass = "shelf-search-empty";

        private UIDocument document;
        private VisualElement root;
        private VisualElement backdrop;
        private Button closeButton;
        private TextField searchField;
        private Label titleLabel;
        private VisualElement shelfSelectorRow;
        private Button filterAll;
        private Button filterReading;
        private Button filterRead;
        private Button filterLiked;
        private ScrollView resultsScroll;
        private Label statusLabel;

        public event Action Closed;

        public Action QueryChanged { get; set; }              // 검색 input 변경
        public Action<int> FilterSelected { get; set; }        // 0=전체,1=읽는중,2=완독,3=좋아요
        public Action<int> ShelfSelected { get; set; }         // 책장 인덱스
        public Action<int> ResultSelected { get; set; }        // 결과 행 slot

        public bool IsVisible => root != null && !root.ClassListContains(HiddenClass);

        // 검색어는 overlay 의 TextField 가 단일 소유 — Manager 가 RefreshBookSearchPanel 에서 읽는다.
        public string Query => searchField != null ? (searchField.value ?? string.Empty) : string.Empty;

        private void Awake()
        {
            var visualTree = Resources.Load<VisualTreeAsset>(UxmlResourcePath);
            if (visualTree == null)
            {
                Debug.LogWarning($"[BookSearchOverlay] VisualTreeAsset not found at Resources/{UxmlResourcePath}.uxml — overlay disabled.");
                enabled = false;
                return;
            }

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "BookSearchOverlayPanelSettings (runtime)";
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
                Debug.LogWarning($"[BookSearchOverlay] Design tokens not found at Resources/{DesignTokensResourcePath}.uss — overlay 가 token 없이 fallback 으로 렌더링됩니다.");
            }

            var styleSheet = Resources.Load<StyleSheet>(UssResourcePath);
            if (styleSheet != null)
            {
                document.rootVisualElement.styleSheets.Add(styleSheet);
            }
            else
            {
                Debug.LogWarning($"[BookSearchOverlay] StyleSheet not found at Resources/{UssResourcePath}.uss — overlay will render without custom styles.");
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
            searchField = docRoot.Q<TextField>("search-input");
            titleLabel = docRoot.Q<Label>("title");
            shelfSelectorRow = docRoot.Q<VisualElement>("shelf-selector");
            filterAll = docRoot.Q<Button>("filter-all");
            filterReading = docRoot.Q<Button>("filter-reading");
            filterRead = docRoot.Q<Button>("filter-read");
            filterLiked = docRoot.Q<Button>("filter-liked");
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
            if (filterAll != null) filterAll.clicked += () => FilterSelected?.Invoke(0);
            if (filterReading != null) filterReading.clicked += () => FilterSelected?.Invoke(1);
            if (filterRead != null) filterRead.clicked += () => FilterSelected?.Invoke(2);
            if (filterLiked != null) filterLiked.clicked += () => FilterSelected?.Invoke(3);
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
        public void Show(BookSearchSnapshot snapshot)
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
        public void UpdateSnapshot(BookSearchSnapshot snapshot)
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

        private void ApplySnapshotData(BookSearchSnapshot snapshot)
        {
            if (titleLabel != null)
            {
                titleLabel.text = string.IsNullOrEmpty(snapshot.Title) ? "책 찾기" : snapshot.Title;
            }

            RebuildShelfChips(snapshot.ShelfCount, snapshot.ActiveShelfIndex);

            ApplyFilterChip(filterAll, 0, snapshot.ActiveFilter, $"전체 {snapshot.AllCount}");
            ApplyFilterChip(filterReading, 1, snapshot.ActiveFilter, $"읽는 중 {snapshot.ReadingCount}");
            ApplyFilterChip(filterRead, 2, snapshot.ActiveFilter, $"완독 {snapshot.ReadCount}");
            ApplyFilterChip(filterLiked, 3, snapshot.ActiveFilter, $"좋아요 {snapshot.LikedCount}");

            RebuildResults(snapshot.Rows);
            if (snapshot.ResetScroll && resultsScroll != null)
            {
                resultsScroll.scrollOffset = Vector2.zero;
            }

            if (statusLabel != null)
            {
                statusLabel.text = snapshot.Status ?? string.Empty;
            }
        }

        private static void ApplyFilterChip(Button chip, int index, int activeFilter, string text)
        {
            if (chip == null)
            {
                return;
            }

            chip.text = text;
            if (index == activeFilter)
            {
                if (!chip.ClassListContains(SelectedClass))
                {
                    chip.AddToClassList(SelectedClass);
                }
            }
            else
            {
                chip.RemoveFromClassList(SelectedClass);
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

                // 활성 책장은 다시 누를 필요 없음 — disabled.
                chip.SetEnabled(!isActive);
                chip.clicked += () => ShelfSelected?.Invoke(index);
                shelfSelectorRow.Add(chip);
            }
        }

        private void RebuildResults(IReadOnlyList<BookSearchRowItem> rows)
        {
            if (resultsScroll == null)
            {
                return;
            }

            resultsScroll.Clear();
            if (rows == null || rows.Count == 0)
            {
                var empty = new Label("일치하는 책이 없습니다.");
                empty.AddToClassList(EmptyClass);
                empty.pickingMode = PickingMode.Ignore;
                resultsScroll.Add(empty);
                return;
            }

            for (var i = 0; i < rows.Count; i++)
            {
                resultsScroll.Add(BuildResultRow(rows[i]));
            }
        }

        private VisualElement BuildResultRow(BookSearchRowItem item)
        {
            var row = new Button();
            row.AddToClassList(ResultRowClass);
            var slot = item.Slot;
            row.clicked += () => ResultSelected?.Invoke(slot);

            var title = new Label(string.IsNullOrEmpty(item.Title) ? "(제목 없음)" : item.Title);
            title.AddToClassList(ResultTitleClass);
            title.pickingMode = PickingMode.Ignore;
            row.Add(title);

            var meta = new Label(item.Meta ?? string.Empty);
            meta.AddToClassList(ResultMetaClass);
            meta.pickingMode = PickingMode.Ignore;
            row.Add(meta);

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
