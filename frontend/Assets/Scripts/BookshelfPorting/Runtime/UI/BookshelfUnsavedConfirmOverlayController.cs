using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace BookshelfPorting.Runtime.UI
{
    // UI Toolkit overlay — 미저장 변경 확인 다이얼로그.
    // 책장 편집 중 unsaved 변경이 있는데 다른 화면으로 이동하려 할 때 critical confirm 으로 가로막는다.
    //
    // 흐름:
    //   1) Awake 시 Pretendard + design tokens USS + view USS 로드, PanelSettings 동적 생성 후 UIDocument attach
    //   2) Manager 가 콜백 props 설정 후 Show() 호출 (정적 콘텐츠 — snapshot 없음)
    //   3) [저장 후 이동] → ConfirmRequested / [취소]·backdrop → CancelRequested
    //
    // 책장관리 overlay(BookshelfManageOverlayController)와 동일 패턴. snapshot / drag 없음.
    // sortingOrder 6400 — 일반 overlay(5900) 보다 위 (critical confirm).

    public sealed class BookshelfUnsavedConfirmOverlayController : MonoBehaviour
    {
        private const string UxmlResourcePath = "Bookshelf/BookshelfUnsavedConfirmView";
        private const string UssResourcePath = "Bookshelf/BookshelfUnsavedConfirmView";
        // 공용 디자인 토큰 — 다른 overlay 와 같은 path 로 load 해서 색/spacing/type 일관 유지.
        private const string DesignTokensResourcePath = "UI/DotshelfDesignTokens";
        // critical confirm — DotshelfDesignTokens.uss 의 sorting 가이드 (overlay 5900 / critical-modal 6400).
        private const int OverlaySortingOrder = 6400;
        private const string HiddenClass = "hidden";

        private UIDocument document;
        private VisualElement root;
        private VisualElement backdrop;
        private Button confirmButton;
        private Button cancelButton;

        // [저장 후 이동] / [취소]·backdrop 콜백.
        public Action ConfirmRequested { get; set; }
        public Action CancelRequested { get; set; }

        public bool IsVisible => root != null && !root.ClassListContains(HiddenClass);

        private void Awake()
        {
            var visualTree = Resources.Load<VisualTreeAsset>(UxmlResourcePath);
            if (visualTree == null)
            {
                Debug.LogWarning($"[BookshelfUnsavedConfirmOverlay] VisualTreeAsset not found at Resources/{UxmlResourcePath}.uxml — overlay disabled.");
                enabled = false;
                return;
            }

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "BookshelfUnsavedConfirmOverlayPanelSettings (runtime)";
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
                Debug.LogWarning($"[BookshelfUnsavedConfirmOverlay] Design tokens not found at Resources/{DesignTokensResourcePath}.uss — overlay 가 token 없이 fallback 으로 렌더링됩니다.");
            }

            var styleSheet = Resources.Load<StyleSheet>(UssResourcePath);
            if (styleSheet != null)
            {
                document.rootVisualElement.styleSheets.Add(styleSheet);
            }
            else
            {
                Debug.LogWarning($"[BookshelfUnsavedConfirmOverlay] StyleSheet not found at Resources/{UssResourcePath}.uss — overlay will render without custom styles.");
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
            confirmButton = docRoot.Q<Button>("confirm-button");
            cancelButton = docRoot.Q<Button>("cancel-button");
        }

        private void WireCallbacks()
        {
            if (confirmButton != null) confirmButton.clicked += OnConfirmClicked;
            if (cancelButton != null) cancelButton.clicked += OnCancelClicked;
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

        public void Show()
        {
            if (root == null)
            {
                return;
            }

            root.RemoveFromClassList(HiddenClass);
        }

        public void Hide()
        {
            if (root == null)
            {
                return;
            }

            root.AddToClassList(HiddenClass);
        }

        // 버튼/backdrop 은 overlay 가 먼저 self-hide 후 콜백 — 책장관리 sub-modal 과 동일.
        private void OnConfirmClicked()
        {
            Hide();
            ConfirmRequested?.Invoke();
        }

        private void OnCancelClicked()
        {
            Hide();
            CancelRequested?.Invoke();
        }

        private void OnBackdropClicked(ClickEvent evt)
        {
            if (evt.target != backdrop)
            {
                return;
            }

            // backdrop 클릭 = 취소 (이동 안 함 — 안전한 기본값).
            Hide();
            CancelRequested?.Invoke();
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
