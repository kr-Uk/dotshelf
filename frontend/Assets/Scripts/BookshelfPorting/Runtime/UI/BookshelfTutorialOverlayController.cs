using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace BookshelfPorting.Runtime.UI
{
    // UI Toolkit overlay — 첫 책장 튜토리얼 / 온보딩.
    // 2-모드 공용 (CreateFirstShelf: 첫 책장 만들기 / FeatureGuide: 기능 안내 다단계).
    // 콘텐츠(title/message/steps/버튼 라벨)는 Manager 가 mode·step 에 맞춰 snapshot 으로 주입.
    //
    // 흐름:
    //   1) Awake 시 Pretendard + design tokens USS + view USS 로드, PanelSettings 동적 생성 후 UIDocument attach
    //   2) Manager 의 RefreshFirstBookshelfTutorialPanel 이 mode 해석 → Show/UpdateSnapshot/Hide
    //   3) primary 버튼 → PrimaryActionRequested 콜백 (Manager 가 mode 에 맞춰 라우팅)
    //
    // 강제 온보딩 — close 버튼/backdrop dismiss 없음. backdrop 은 picking-mode 로 뒤 입력만 차단.
    // 책장관리 overlay(BookshelfManageOverlayController)와 동일 패턴.

    public readonly struct BookshelfTutorialSnapshot
    {
        public string Title { get; }
        public string Message { get; }
        public string Steps { get; }
        public string PrimaryLabel { get; }

        public BookshelfTutorialSnapshot(string title, string message, string steps, string primaryLabel)
        {
            Title = title;
            Message = message;
            Steps = steps;
            PrimaryLabel = primaryLabel;
        }
    }

    public sealed class BookshelfTutorialOverlayController : MonoBehaviour
    {
        private const string UxmlResourcePath = "Bookshelf/BookshelfTutorialView";
        private const string UssResourcePath = "Bookshelf/BookshelfTutorialView";
        // 공용 디자인 토큰 — 다른 overlay 와 같은 path 로 load 해서 색/spacing/type 일관 유지.
        private const string DesignTokensResourcePath = "UI/DotshelfDesignTokens";
        private const int OverlaySortingOrder = 5900;
        private const string HiddenClass = "hidden";

        private UIDocument document;
        private VisualElement root;
        private Label titleLabel;
        private Label messageLabel;
        private Label stepsLabel;
        private Button primaryButton;

        // primary 버튼 click 콜백 — Manager 가 현재 mode 에 맞춰 라우팅 (책장 추가 / 안내 다음 단계).
        public Action PrimaryActionRequested { get; set; }

        public bool IsVisible => root != null && !root.ClassListContains(HiddenClass);

        private void Awake()
        {
            var visualTree = Resources.Load<VisualTreeAsset>(UxmlResourcePath);
            if (visualTree == null)
            {
                Debug.LogWarning($"[BookshelfTutorialOverlay] VisualTreeAsset not found at Resources/{UxmlResourcePath}.uxml — overlay disabled.");
                enabled = false;
                return;
            }

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "BookshelfTutorialOverlayPanelSettings (runtime)";
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
                Debug.LogWarning($"[BookshelfTutorialOverlay] Design tokens not found at Resources/{DesignTokensResourcePath}.uss — overlay 가 token 없이 fallback 으로 렌더링됩니다.");
            }

            var styleSheet = Resources.Load<StyleSheet>(UssResourcePath);
            if (styleSheet != null)
            {
                document.rootVisualElement.styleSheets.Add(styleSheet);
            }
            else
            {
                Debug.LogWarning($"[BookshelfTutorialOverlay] StyleSheet not found at Resources/{UssResourcePath}.uss — overlay will render without custom styles.");
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
            titleLabel = docRoot.Q<Label>("title");
            messageLabel = docRoot.Q<Label>("message");
            stepsLabel = docRoot.Q<Label>("steps");
            primaryButton = docRoot.Q<Button>("primary-button");
        }

        private void WireCallbacks()
        {
            if (primaryButton != null) primaryButton.clicked += OnPrimaryClicked;
        }

        private void OnDestroy()
        {
            // 코드로 ScriptableObject.CreateInstance<PanelSettings>() 로 만든 인스턴스 — 명시적으로 Destroy 하지 않으면
            // scene reload / play mode toggle 누적 leak. (themeStyleSheet 는 빌려온 reference 라 건드리지 않음.)
            if (document != null && document.panelSettings != null)
            {
                var settings = document.panelSettings;
                document.panelSettings = null;
                Destroy(settings);
            }
        }

        // Modal 진입. 튜토리얼은 transient state 가 없어 Show / UpdateSnapshot 모두 데이터만 그린다.
        public void Show(BookshelfTutorialSnapshot snapshot)
        {
            if (root == null)
            {
                return;
            }

            ApplySnapshotData(snapshot);
            root.RemoveFromClassList(HiddenClass);
        }

        // Visible 상태에서 데이터만 갱신 — FeatureGuide step 진행 시 콘텐츠 재반영.
        public void UpdateSnapshot(BookshelfTutorialSnapshot snapshot)
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

        private void ApplySnapshotData(BookshelfTutorialSnapshot snapshot)
        {
            if (titleLabel != null) titleLabel.text = snapshot.Title ?? string.Empty;
            if (messageLabel != null) messageLabel.text = snapshot.Message ?? string.Empty;
            if (stepsLabel != null) stepsLabel.text = snapshot.Steps ?? string.Empty;
            if (primaryButton != null) primaryButton.text = snapshot.PrimaryLabel ?? string.Empty;
        }

        private void OnPrimaryClicked()
        {
            PrimaryActionRequested?.Invoke();
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
