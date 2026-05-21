using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace BookshelfPorting.Runtime.UI
{
    // UI Toolkit overlay — 책 추가 확인.
    // 책 추가에서 고른 책을 보여주고 독서 상태(안읽음/읽는중/읽음) + 책 배치(측면/45°/정면)를 정한 뒤 추가.
    //
    // 흐름:
    //   1) Awake 시 Pretendard + design tokens USS + view USS 로드, PanelSettings 동적 생성 후 UIDocument attach
    //   2) Manager 가 콜백 props 설정 후 Show(snapshot) 호출
    //   3) 상태 chip → ReadingStatusSelected / 배치 chip → OrientationSelected
    //      [추가] → ConfirmRequested / [취소]·backdrop → Closed
    //   4) Manager 가 SetPending* 후 UpdateSnapshot 으로 chip 선택 갱신
    //
    // 책장관리 overlay(BookshelfManageOverlayController)와 동일 패턴.

    public readonly struct BookAddConfirmSnapshot
    {
        public string Title { get; }
        public string Meta { get; }
        public string Summary { get; }
        public int ReadingStatus { get; }   // 0=안읽음, 1=읽는중, 2=읽음
        public int Orientation { get; }     // 0=측면, 1=45°, 2=정면

        public BookAddConfirmSnapshot(string title, string meta, string summary, int readingStatus, int orientation)
        {
            Title = title;
            Meta = meta;
            Summary = summary;
            ReadingStatus = readingStatus;
            Orientation = orientation;
        }
    }

    public sealed class BookAddConfirmOverlayController : MonoBehaviour
    {
        private const string UxmlResourcePath = "Bookshelf/BookAddConfirmView";
        private const string UssResourcePath = "Bookshelf/BookAddConfirmView";
        private const string DesignTokensResourcePath = "UI/DotshelfDesignTokens";
        private const int OverlaySortingOrder = 5900;
        private const string HiddenClass = "hidden";
        private const string SelectedClass = "selected";

        private UIDocument document;
        private VisualElement root;
        private VisualElement backdrop;
        private Button closeButton;
        private Label bookTitleLabel;
        private Label bookMetaLabel;
        private Label bookSummaryLabel;
        private Button statusUnread;
        private Button statusReading;
        private Button statusRead;
        private Button orientSpine;
        private Button orientAngled;
        private Button orientFront;
        private Button cancelButton;
        private Button confirmButton;

        public event Action Closed;

        public Action<int> ReadingStatusSelected { get; set; }   // 0=안읽음, 1=읽는중, 2=읽음
        public Action<int> OrientationSelected { get; set; }      // 0=측면, 1=45°, 2=정면
        public Action ConfirmRequested { get; set; }

        public bool IsVisible => root != null && !root.ClassListContains(HiddenClass);

        private void Awake()
        {
            var visualTree = Resources.Load<VisualTreeAsset>(UxmlResourcePath);
            if (visualTree == null)
            {
                Debug.LogWarning($"[BookAddConfirmOverlay] VisualTreeAsset not found at Resources/{UxmlResourcePath}.uxml — overlay disabled.");
                enabled = false;
                return;
            }

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "BookAddConfirmOverlayPanelSettings (runtime)";
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
                Debug.LogWarning($"[BookAddConfirmOverlay] Design tokens not found at Resources/{DesignTokensResourcePath}.uss — overlay 가 token 없이 fallback 으로 렌더링됩니다.");
            }

            var styleSheet = Resources.Load<StyleSheet>(UssResourcePath);
            if (styleSheet != null)
            {
                document.rootVisualElement.styleSheets.Add(styleSheet);
            }
            else
            {
                Debug.LogWarning($"[BookAddConfirmOverlay] StyleSheet not found at Resources/{UssResourcePath}.uss — overlay will render without custom styles.");
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
            bookTitleLabel = docRoot.Q<Label>("book-title");
            bookMetaLabel = docRoot.Q<Label>("book-meta");
            bookSummaryLabel = docRoot.Q<Label>("book-summary");
            statusUnread = docRoot.Q<Button>("status-unread");
            statusReading = docRoot.Q<Button>("status-reading");
            statusRead = docRoot.Q<Button>("status-read");
            orientSpine = docRoot.Q<Button>("orient-spine");
            orientAngled = docRoot.Q<Button>("orient-angled");
            orientFront = docRoot.Q<Button>("orient-front");
            cancelButton = docRoot.Q<Button>("cancel-button");
            confirmButton = docRoot.Q<Button>("confirm-button");
        }

        private void WireCallbacks()
        {
            if (closeButton != null) closeButton.clicked += OnCloseClicked;
            if (cancelButton != null) cancelButton.clicked += OnCloseClicked;
            if (backdrop != null) backdrop.RegisterCallback<ClickEvent>(OnBackdropClicked);
            if (confirmButton != null) confirmButton.clicked += OnConfirmClicked;

            if (statusUnread != null) statusUnread.clicked += () => ReadingStatusSelected?.Invoke(0);
            if (statusReading != null) statusReading.clicked += () => ReadingStatusSelected?.Invoke(1);
            if (statusRead != null) statusRead.clicked += () => ReadingStatusSelected?.Invoke(2);

            if (orientSpine != null) orientSpine.clicked += () => OrientationSelected?.Invoke(0);
            if (orientAngled != null) orientAngled.clicked += () => OrientationSelected?.Invoke(1);
            if (orientFront != null) orientFront.clicked += () => OrientationSelected?.Invoke(2);
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

        public void Show(BookAddConfirmSnapshot snapshot)
        {
            if (root == null)
            {
                return;
            }

            ApplySnapshotData(snapshot);
            root.RemoveFromClassList(HiddenClass);
        }

        public void UpdateSnapshot(BookAddConfirmSnapshot snapshot)
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

        private void ApplySnapshotData(BookAddConfirmSnapshot snapshot)
        {
            if (bookTitleLabel != null)
            {
                bookTitleLabel.text = string.IsNullOrEmpty(snapshot.Title) ? "(제목 없음)" : snapshot.Title;
            }

            if (bookMetaLabel != null)
            {
                bookMetaLabel.text = snapshot.Meta ?? string.Empty;
            }

            if (bookSummaryLabel != null)
            {
                bookSummaryLabel.text = snapshot.Summary ?? string.Empty;
            }

            ApplyChip(statusUnread, snapshot.ReadingStatus == 0);
            ApplyChip(statusReading, snapshot.ReadingStatus == 1);
            ApplyChip(statusRead, snapshot.ReadingStatus == 2);

            ApplyChip(orientSpine, snapshot.Orientation == 0);
            ApplyChip(orientAngled, snapshot.Orientation == 1);
            ApplyChip(orientFront, snapshot.Orientation == 2);
        }

        private static void ApplyChip(Button chip, bool selected)
        {
            if (chip == null)
            {
                return;
            }

            if (selected)
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

        // 추가 결과(성공=배치 모드 진입 / 실패=패널 유지)는 Manager 가 상태로 제어 — overlay 가 self-hide 하지 않음.
        private void OnConfirmClicked()
        {
            ConfirmRequested?.Invoke();
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
