using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace BookshelfPorting.Runtime.UI
{
    // UI Toolkit overlay — 책장관리 진입점.
    // 구성: Header(저장) + Hero card(현재 책장 상세 + visibility chip + sort chip + 삭제) + List(다른 책장) + Add card.
    //
    // 흐름:
    //   1) Awake 시 UXML/USS 로드, UIDocument attach
    //   2) Manager 가 콜백 props 설정 후 Show(snapshot) 호출
    //   3) Hero 의 chip click → 콜백 (visibility set / sort)
    //   4) 행 click → RowSelected (활성 책장 전환)
    //   5) Manager 가 데이터 변경 후 다시 Show(snapshot) 호출로 갱신
    //   6) Close → Closed event → manager 가 edit mode 종료
    //
    // drag/drop 순서 변경은 다음 phase 에서 결선 예정.

    public readonly struct BookshelfManageRowItem
    {
        public int Index { get; }
        public string Name { get; }
        public string VisibilityLabel { get; }
        public bool IsActive { get; }

        public BookshelfManageRowItem(int index, string name, string visibilityLabel, bool isActive)
        {
            Index = index;
            Name = name;
            VisibilityLabel = visibilityLabel;
            IsActive = isActive;
        }
    }

    public readonly struct BookshelfManageSnapshot
    {
        public IReadOnlyList<BookshelfManageRowItem> Rows { get; }
        public string ActiveShelfNumber { get; }   // "01", "02" 형식 — Hero 좌측 큰 번호
        public string ActiveShelfName { get; }
        public string ActiveShelfStats { get; }
        public int ActiveVisibilityScope { get; }  // 0=Public, 1=Followers, 2=Private, -1=없음
        public string Status { get; }
        public bool StatusIsError { get; }
        public bool CanSortBooks { get; }
        public bool CanSetVisibility { get; }
        public bool CanRename { get; }
        public bool CanAdd { get; }
        public bool CanDelete { get; }
        public bool IsAddInProgress { get; }       // true 면 add button 안에 "추가 중..." + disabled

        public BookshelfManageSnapshot(
            IReadOnlyList<BookshelfManageRowItem> rows,
            string activeShelfNumber,
            string activeShelfName,
            string activeShelfStats,
            int activeVisibilityScope,
            string status,
            bool statusIsError,
            bool canSortBooks,
            bool canSetVisibility,
            bool canRename,
            bool canAdd,
            bool canDelete,
            bool isAddInProgress)
        {
            Rows = rows;
            ActiveShelfNumber = activeShelfNumber;
            ActiveShelfName = activeShelfName;
            ActiveShelfStats = activeShelfStats;
            ActiveVisibilityScope = activeVisibilityScope;
            Status = status;
            StatusIsError = statusIsError;
            CanSortBooks = canSortBooks;
            CanSetVisibility = canSetVisibility;
            CanRename = canRename;
            CanAdd = canAdd;
            CanDelete = canDelete;
            IsAddInProgress = isAddInProgress;
        }
    }

    public enum BookshelfManageSortMode
    {
        Title = 0,
        Author = 1,
        ReadingStatus = 2,
        Liked = 3,
    }

    public sealed class BookshelfManageOverlayController : MonoBehaviour
    {
        private const string UxmlResourcePath = "Bookshelf/BookshelfManageView";
        private const string UssResourcePath = "Bookshelf/BookshelfManageView";
        // 공용 디자인 토큰 — 다른 overlay 도 같은 path 로 load 해서 색/spacing/type 일관 유지.
        private const string DesignTokensResourcePath = "UI/DotshelfDesignTokens";
        private const int OverlaySortingOrder = 5900;
        private const string HiddenClass = "hidden";
        private const string ActiveRowClass = "active";
        private const string SelectedChipClass = "selected";
        private const string ErrorClass = "error";

        private UIDocument document;
        private VisualElement root;
        private VisualElement backdrop;
        private Button closeButton;

        private Label heroNumberLabel;
        private Label heroNameLabel;
        private Label heroStatsLabel;
        private Button renameButton;
        private Button deleteButton;
        private Button addButton;

        // 인라인 이름 편집 모드
        private VisualElement heroViewContainer;
        private VisualElement heroEditContainer;
        private TextField nameInput;
        private Button nameSaveButton;
        private Button nameCancelButton;
        private string activeShelfNameSnapshot;

        // 삭제 확인 sub-modal
        private VisualElement deleteConfirmRoot;
        private VisualElement deleteConfirmBackdrop;
        private Label deleteConfirmMessage;
        private Button deleteConfirmButton;
        private Button deleteCancelButton;

        // 삭제 결과 sub-modal (성공/실패)
        private VisualElement deleteResultRoot;
        private VisualElement deleteResultBackdrop;
        private VisualElement deleteResultCard;
        private Label deleteResultEyebrow;
        private Label deleteResultTitle;
        private Label deleteResultMessage;
        private Button deleteResultOkButton;

        private Button visibilityPublicChip;
        private Button visibilityFollowersChip;
        private Button visibilityPrivateChip;

        private Button sortTitleChip;
        private Button sortAuthorChip;
        private Button sortStatusChip;
        private Button sortLikedChip;

        private ScrollView rowsScroll;
        private Label statusLabel;

        public event Action Closed;

        public Action<int> RowSelected { get; set; }
        public Action<BookshelfManageSortMode> SortRequested { get; set; }
        public Action<int> SetVisibilityRequested { get; set; } // 0=Public, 1=Followers, 2=Private
        public Action<string> NameChangeRequested { get; set; } // 인라인 TextField 의 새 이름 (trimmed)
        public Action AddRequested { get; set; }
        public Action DeleteRequested { get; set; }              // "이 책장 삭제" 클릭 → sub-modal 열기 요청
        public Action DeleteConfirmed { get; set; }              // sub-modal 의 "삭제" 클릭 → 실제 삭제
        public Action DeleteCancelled { get; set; }              // sub-modal 의 "취소" 또는 backdrop → 취소
        public Action<int, int> ReorderRequested { get; set; } // (sourceIndex, targetIndex)

        // ===== Drag state (grip long-press → row reorder) =====
        private const string DraggingClass = "dragging";
        private const string ArmingClass = "arming";
        private const string DropAboveClass = "drop-above";
        private const string DropBelowClass = "drop-below";
        private const string DroppedClass = "dropped";
        private const long LongPressMs = 220;
        private const float LongPressCancelDistancePx = 8f;
        private const long DropBounceClearMs = 460;

        private int dragSourceIndex = -1;
        private int dragTargetIndex = -1;
        private bool dropAboveTarget;          // cursor 가 target row 의 상반에 있는지 (= "위에 drop")
        private VisualElement draggedRow;
        private readonly List<VisualElement> activeRowElements = new List<VisualElement>();

        // Long-press 진입 대기 (PointerDown ~ LongPressMs 동안 release/이동 안 하면 drag 시작)
        private bool isDragArmed;              // long-press 통과 후 진짜 drag 진행 중
        private int pendingPointerId = -1;
        private Vector2 pendingPointerStartLocal;
        private VisualElement pendingGrip;
        private VisualElement pendingRow;
        private int pendingRowIndex = -1;
        private IVisualElementScheduledItem longPressTimer;

        // Floating ghost — drag 중 cursor 따라다니는 미니 카드
        private VisualElement dragGhost;

        // Drop bounce — drop 직후 새 위치 행에 짧게 .dropped 클래스
        private int pendingDroppedRowIndex = -1;

        // Row click debounce — UI Toolkit ClickEvent 가 같은 frame 안에서 여러 번 fire 되거나
        // async ShowBookshelf 와 동시 진입하는 케이스 방지. (frameCount + rowIndex 페어로 한 frame 1회만 통과.)
        private int lastRowClickFrame = -1;
        private int lastRowClickIndex = -1;

        public bool IsVisible => root != null && !root.ClassListContains(HiddenClass);

        private void Awake()
        {
            var visualTree = Resources.Load<VisualTreeAsset>(UxmlResourcePath);
            if (visualTree == null)
            {
                Debug.LogWarning($"[BookshelfManageOverlay] VisualTreeAsset not found at Resources/{UxmlResourcePath}.uxml — overlay disabled.");
                enabled = false;
                return;
            }

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "BookshelfManageOverlayPanelSettings (runtime)";
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

            // 0) Pretendard 폰트 적용 — RuntimeUiFont 헬퍼가 Resources/Fonts/Pretendard-Regular →
            //    OS Pretendard → Noto Sans KR → Apple SD Gothic Neo 폴백. root 에 한 번 set 하면 모든 자식이 inherit.
            var uiFont = BookshelfPorting.Runtime.RuntimeUiFont.Resolve();
            if (uiFont != null)
            {
                document.rootVisualElement.style.unityFont = new StyleFont(uiFont);
            }

            // 1) 공용 디자인 토큰 USS 를 먼저 load — var(--ds-*) cascade source.
            var tokensSheet = Resources.Load<StyleSheet>(DesignTokensResourcePath);
            if (tokensSheet != null)
            {
                document.rootVisualElement.styleSheets.Add(tokensSheet);
            }
            else
            {
                Debug.LogWarning($"[BookshelfManageOverlay] Design tokens not found at Resources/{DesignTokensResourcePath}.uss — overlay 가 token 없이 fallback 으로 렌더링됩니다.");
            }

            // 2) view USS 추가 — token sheet 다음에 add 해서 var() 참조가 cascade.
            var styleSheet = Resources.Load<StyleSheet>(UssResourcePath);
            if (styleSheet != null)
            {
                document.rootVisualElement.styleSheets.Add(styleSheet);
            }
            else
            {
                Debug.LogWarning($"[BookshelfManageOverlay] StyleSheet not found at Resources/{UssResourcePath}.uss — overlay will render without custom styles.");
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

            heroNumberLabel = docRoot.Q<Label>("active-number");
            heroNameLabel = docRoot.Q<Label>("active-name");
            heroStatsLabel = docRoot.Q<Label>("active-stats");
            renameButton = docRoot.Q<Button>("rename-button");
            deleteButton = docRoot.Q<Button>("delete-button");
            addButton = docRoot.Q<Button>("add-button");

            heroViewContainer = docRoot.Q<VisualElement>("hero-view");
            heroEditContainer = docRoot.Q<VisualElement>("hero-edit");
            nameInput = docRoot.Q<TextField>("name-input");
            nameSaveButton = docRoot.Q<Button>("name-save");
            nameCancelButton = docRoot.Q<Button>("name-cancel");

            deleteConfirmRoot = docRoot.Q<VisualElement>("delete-confirm-modal");
            deleteConfirmBackdrop = docRoot.Q<VisualElement>("delete-confirm-backdrop");
            deleteConfirmMessage = docRoot.Q<Label>("delete-confirm-message");
            deleteConfirmButton = docRoot.Q<Button>("delete-confirm-button");
            deleteCancelButton = docRoot.Q<Button>("delete-cancel-button");

            deleteResultRoot = docRoot.Q<VisualElement>("delete-result-modal");
            deleteResultBackdrop = docRoot.Q<VisualElement>("delete-result-backdrop");
            deleteResultCard = docRoot.Q<VisualElement>("delete-result-card");
            deleteResultEyebrow = docRoot.Q<Label>("delete-result-eyebrow");
            deleteResultTitle = docRoot.Q<Label>("delete-result-title");
            deleteResultMessage = docRoot.Q<Label>("delete-result-message");
            deleteResultOkButton = docRoot.Q<Button>("delete-result-ok-button");

            visibilityPublicChip = docRoot.Q<Button>("vis-public");
            visibilityFollowersChip = docRoot.Q<Button>("vis-followers");
            visibilityPrivateChip = docRoot.Q<Button>("vis-private");

            sortTitleChip = docRoot.Q<Button>("sort-title");
            sortAuthorChip = docRoot.Q<Button>("sort-author");
            sortStatusChip = docRoot.Q<Button>("sort-status");
            sortLikedChip = docRoot.Q<Button>("sort-liked");

            rowsScroll = docRoot.Q<ScrollView>("rows");
            if (rowsScroll != null)
            {
                // 행의 텍스트가 길어도 가로 스크롤로 노출되지 않도록 강제 비활성.
                rowsScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                rowsScroll.mode = ScrollViewMode.Vertical;
            }
            statusLabel = docRoot.Q<Label>("status");
        }

        private void WireCallbacks()
        {
            if (closeButton != null) closeButton.clicked += OnCloseClicked;
            if (backdrop != null) backdrop.RegisterCallback<ClickEvent>(OnBackdropClicked);

            // 이름 변경 버튼 → 인라인 편집 모드 진입
            if (renameButton != null) renameButton.clicked += EnterNameEditMode;
            if (nameCancelButton != null) nameCancelButton.clicked += ExitNameEditMode;
            if (nameSaveButton != null) nameSaveButton.clicked += TrySaveName;
            if (nameInput != null)
            {
                // Enter = 저장, Esc = 취소. TextField 의 KeyDownEvent 는 capture trickle 모드라
                // RegisterCallback 으로 잡으면 입력 영향 없이 hot-key 처리 가능.
                nameInput.RegisterCallback<KeyDownEvent>(OnNameInputKeyDown, TrickleDown.TrickleDown);
                nameInput.RegisterValueChangedCallback(_ => RefreshNameSaveButtonState());
            }

            if (deleteButton != null) deleteButton.clicked += () => DeleteRequested?.Invoke();
            if (addButton != null) addButton.clicked += () => AddRequested?.Invoke();

            // Sub-modal — 삭제 확인.
            if (deleteConfirmButton != null) deleteConfirmButton.clicked += OnDeleteConfirmClicked;
            if (deleteCancelButton != null) deleteCancelButton.clicked += OnDeleteCancelClicked;
            if (deleteConfirmBackdrop != null) deleteConfirmBackdrop.RegisterCallback<ClickEvent>(OnDeleteConfirmBackdropClicked);

            // Sub-modal — 삭제 결과 (성공/실패).
            if (deleteResultOkButton != null) deleteResultOkButton.clicked += HideDeleteResult;
            if (deleteResultBackdrop != null) deleteResultBackdrop.RegisterCallback<ClickEvent>(OnDeleteResultBackdropClicked);

            if (visibilityPublicChip != null) visibilityPublicChip.clicked += () => SetVisibilityRequested?.Invoke(0);
            if (visibilityFollowersChip != null) visibilityFollowersChip.clicked += () => SetVisibilityRequested?.Invoke(1);
            if (visibilityPrivateChip != null) visibilityPrivateChip.clicked += () => SetVisibilityRequested?.Invoke(2);

            if (sortTitleChip != null) sortTitleChip.clicked += () => SortRequested?.Invoke(BookshelfManageSortMode.Title);
            if (sortAuthorChip != null) sortAuthorChip.clicked += () => SortRequested?.Invoke(BookshelfManageSortMode.Author);
            if (sortStatusChip != null) sortStatusChip.clicked += () => SortRequested?.Invoke(BookshelfManageSortMode.ReadingStatus);
            if (sortLikedChip != null) sortLikedChip.clicked += () => SortRequested?.Invoke(BookshelfManageSortMode.Liked);
        }

        private void OnDestroy()
        {
            if (backdrop != null)
            {
                backdrop.UnregisterCallback<ClickEvent>(OnBackdropClicked);
            }
        }

        public void Show(BookshelfManageSnapshot snapshot)
        {
            if (root == null)
            {
                return;
            }

            // snapshot 갱신은 외부 상태 변화 신호 — 편집 / 삭제 confirm 중이었다면 종료.
            // (사용자가 다른 행 click / chip 토글 / 책장 추가-삭제 등으로 데이터가 바뀐 경우)
            // 단, delete-result 는 사용자가 "확인" 누를 때까지 유지 (자동 dismiss 안 함) — 비동기 응답 후 표시되므로
            // RefreshHud → Show 흐름 중에 사라지면 사용자가 결과를 못 봄.
            ExitNameEditMode();
            HideDeleteConfirm();

            UpdateHero(snapshot);
            UpdateVisibilityChips(snapshot.ActiveVisibilityScope, snapshot.CanSetVisibility);
            UpdateSortChips(snapshot.CanSortBooks);
            RebuildRows(snapshot.Rows);
            ApplyEnabled(renameButton, snapshot.CanRename);
            ApplyEnabled(deleteButton, snapshot.CanDelete);
            UpdateAddButton(snapshot.IsAddInProgress, snapshot.CanAdd);
            ApplyStatus(snapshot.Status, snapshot.StatusIsError);

            root.RemoveFromClassList(HiddenClass);
        }

        public void Hide()
        {
            if (root == null)
            {
                return;
            }

            // drag / 편집 / 삭제 sub-modal 진행 중에 외부에서 Hide 호출 시 잔존 상태 방지.
            ResetDragState();
            ExitNameEditMode();
            HideDeleteConfirm();
            HideDeleteResult();
            root.AddToClassList(HiddenClass);
        }

        // ===== Delete confirm sub-modal =====

        // Manager 가 책장 이름과 책 수를 넘겨 호출 (사용자가 "이 책장 삭제" 누름 직후).
        public void ShowDeleteConfirm(string bookshelfName, int bookCount)
        {
            if (deleteConfirmRoot == null)
            {
                return;
            }

            if (deleteConfirmMessage != null)
            {
                var displayName = string.IsNullOrEmpty(bookshelfName) ? "(이름 없음)" : bookshelfName;
                deleteConfirmMessage.text = bookCount > 0
                    ? $"{displayName}을 정말로 삭제하시겠습니까?\n이 책장에 있는 책 {bookCount}권은 인접한 책장으로 이동됩니다."
                    : $"{displayName}을 정말로 삭제하시겠습니까?";
            }

            deleteConfirmRoot.RemoveFromClassList(HiddenClass);
        }

        public void HideDeleteConfirm()
        {
            if (deleteConfirmRoot == null)
            {
                return;
            }
            deleteConfirmRoot.AddToClassList(HiddenClass);
        }

        public bool IsDeleteConfirmVisible =>
            deleteConfirmRoot != null && !deleteConfirmRoot.ClassListContains(HiddenClass);

        private void OnDeleteConfirmClicked()
        {
            HideDeleteConfirm();
            DeleteConfirmed?.Invoke();
        }

        private void OnDeleteCancelClicked()
        {
            HideDeleteConfirm();
            DeleteCancelled?.Invoke();
        }

        private void OnDeleteConfirmBackdropClicked(ClickEvent evt)
        {
            if (evt.target != deleteConfirmBackdrop)
            {
                return;
            }
            HideDeleteConfirm();
            DeleteCancelled?.Invoke();
        }

        // ===== Delete result sub-modal (성공/실패) =====

        // Manager 가 책장 삭제 success/failure 응답 후 호출.
        public void ShowDeleteResult(string title, string message, bool isSuccess)
        {
            if (deleteResultRoot == null)
            {
                return;
            }

            // 다른 sub-modal 정리 (confirm + result 동시 표시 회피).
            HideDeleteConfirm();

            if (deleteResultEyebrow != null)
            {
                deleteResultEyebrow.text = isSuccess ? "DONE  ·  완료" : "ERROR  ·  실패";
            }
            if (deleteResultTitle != null)
            {
                deleteResultTitle.text = string.IsNullOrWhiteSpace(title)
                    ? (isSuccess ? "삭제 완료" : "삭제 실패")
                    : title;
            }
            if (deleteResultMessage != null)
            {
                deleteResultMessage.text = string.IsNullOrWhiteSpace(message)
                    ? (isSuccess ? "요청한 항목을 삭제했습니다." : "삭제 요청을 완료하지 못했습니다.")
                    : message;
            }

            if (deleteResultCard != null)
            {
                if (isSuccess)
                {
                    deleteResultCard.RemoveFromClassList("is-failure");
                }
                else if (!deleteResultCard.ClassListContains("is-failure"))
                {
                    deleteResultCard.AddToClassList("is-failure");
                }
            }

            // 일반 케이스 — OK 버튼 활성. (Pending 케이스는 별도 메서드에서 disabled 유지.)
            if (deleteResultOkButton != null)
            {
                deleteResultOkButton.SetEnabled(true);
            }

            deleteResultRoot.RemoveFromClassList(HiddenClass);
        }

        // Pending 상태로 result modal 표시 — 응답은 받았지만 list 갱신 대기 중.
        // OK 버튼 disabled + "잠시 후 목록에 표시됩니다..." 안내. MarkResultModalReady 호출되면 활성화.
        public void ShowResultModalPending(string title)
        {
            if (deleteResultRoot == null)
            {
                return;
            }

            HideDeleteConfirm();

            if (deleteResultEyebrow != null)
            {
                deleteResultEyebrow.text = "DONE  ·  완료";
            }
            if (deleteResultTitle != null)
            {
                deleteResultTitle.text = string.IsNullOrWhiteSpace(title) ? "완료" : title;
            }
            if (deleteResultMessage != null)
            {
                deleteResultMessage.text = "잠시 후 목록에 표시됩니다...";
            }
            if (deleteResultCard != null)
            {
                deleteResultCard.RemoveFromClassList("is-failure");
            }
            if (deleteResultOkButton != null)
            {
                deleteResultOkButton.SetEnabled(false);
            }

            deleteResultRoot.RemoveFromClassList(HiddenClass);
        }

        // Pending → Ready 전환. 메시지 swap + OK 활성.
        public void MarkResultModalReady(string message)
        {
            if (deleteResultMessage != null)
            {
                deleteResultMessage.text = string.IsNullOrWhiteSpace(message)
                    ? "요청이 완료되었습니다."
                    : message;
            }
            if (deleteResultOkButton != null)
            {
                deleteResultOkButton.SetEnabled(true);
            }
        }

        public void HideDeleteResult()
        {
            if (deleteResultRoot == null)
            {
                return;
            }
            deleteResultRoot.AddToClassList(HiddenClass);
            // 다음 사용 시 disabled 잔존 방지.
            if (deleteResultOkButton != null)
            {
                deleteResultOkButton.SetEnabled(true);
            }
        }

        public bool IsDeleteResultVisible =>
            deleteResultRoot != null && !deleteResultRoot.ClassListContains(HiddenClass);

        private void OnDeleteResultBackdropClicked(ClickEvent evt)
        {
            if (evt.target != deleteResultBackdrop)
            {
                return;
            }
            HideDeleteResult();
        }

        private void UpdateHero(BookshelfManageSnapshot snapshot)
        {
            if (heroNumberLabel != null)
            {
                heroNumberLabel.text = string.IsNullOrEmpty(snapshot.ActiveShelfNumber) ? "—" : snapshot.ActiveShelfNumber;
            }

            if (heroNameLabel != null)
            {
                heroNameLabel.text = string.IsNullOrEmpty(snapshot.ActiveShelfName) ? "—" : snapshot.ActiveShelfName;
            }

            if (heroStatsLabel != null)
            {
                heroStatsLabel.text = snapshot.ActiveShelfStats ?? string.Empty;
            }

            // 인라인 편집 진입 시 TextField 초기값으로 쓸 활성 책장 이름 캐시.
            activeShelfNameSnapshot = snapshot.ActiveShelfName ?? string.Empty;
        }

        // ===== Inline name edit =====

        private void EnterNameEditMode()
        {
            if (heroViewContainer == null || heroEditContainer == null || nameInput == null)
            {
                return;
            }

            heroViewContainer.AddToClassList(HiddenClass);
            heroEditContainer.RemoveFromClassList(HiddenClass);

            nameInput.SetValueWithoutNotify(activeShelfNameSnapshot ?? string.Empty);
            RefreshNameSaveButtonState();

            // 다음 frame 에 포커스 — 현재 frame 에서는 element 가 막 visible 됐을 수 있음.
            // SelectAll 은 Unity 6 전 버전에서 안정적인 TextField API.
            nameInput.schedule.Execute(() =>
            {
                nameInput.Focus();
                nameInput.SelectAll();
            }).ExecuteLater(10);
        }

        private void ExitNameEditMode()
        {
            if (heroViewContainer == null || heroEditContainer == null)
            {
                return;
            }

            heroEditContainer.AddToClassList(HiddenClass);
            heroViewContainer.RemoveFromClassList(HiddenClass);
        }

        private void TrySaveName()
        {
            if (nameInput == null)
            {
                return;
            }

            var newName = nameInput.value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(newName))
            {
                return;
            }

            ExitNameEditMode();
            NameChangeRequested?.Invoke(newName);
        }

        private void RefreshNameSaveButtonState()
        {
            if (nameSaveButton == null)
            {
                return;
            }

            var hasValue = !string.IsNullOrWhiteSpace(nameInput?.value);
            nameSaveButton.SetEnabled(hasValue);
        }

        private void OnNameInputKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                TrySaveName();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                ExitNameEditMode();
                evt.StopPropagation();
            }
        }

        private void UpdateVisibilityChips(int activeScope, bool canSet)
        {
            ApplyChipSelected(visibilityPublicChip, activeScope == 0);
            ApplyChipSelected(visibilityFollowersChip, activeScope == 1);
            ApplyChipSelected(visibilityPrivateChip, activeScope == 2);

            ApplyEnabled(visibilityPublicChip, canSet);
            ApplyEnabled(visibilityFollowersChip, canSet);
            ApplyEnabled(visibilityPrivateChip, canSet);
        }

        private void UpdateSortChips(bool canSort)
        {
            ApplyEnabled(sortTitleChip, canSort);
            ApplyEnabled(sortAuthorChip, canSort);
            ApplyEnabled(sortStatusChip, canSort);
            ApplyEnabled(sortLikedChip, canSort);
        }

        private void RebuildRows(IReadOnlyList<BookshelfManageRowItem> items)
        {
            if (rowsScroll == null)
            {
                return;
            }

            // RebuildRows 직전 drag 진행 중이었을 가능성을 위해 상태 정리.
            // (단, pendingDroppedRowIndex 는 reset 직전 캡처 — drop bounce 처리용.)
            var bounceIndex = pendingDroppedRowIndex;
            pendingDroppedRowIndex = -1;
            ResetDragState();

            rowsScroll.Clear();
            activeRowElements.Clear();
            if (items == null)
            {
                return;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var row = BuildRow(item);
                rowsScroll.Add(row);
                activeRowElements.Add(row);
            }

            // Drop bounce — drop 후 새로 만들어진 row 중 real index == bounceIndex 인 row 에 .dropped 짧게.
            // bounceIndex 는 real 책장 인덱스 (navigable list 안 element 인덱스가 아님).
            if (bounceIndex >= 0)
            {
                for (var i = 0; i < activeRowElements.Count; i++)
                {
                    if (ResolveRealIndexAt(i) != bounceIndex)
                    {
                        continue;
                    }
                    var droppedRow = activeRowElements[i];
                    droppedRow.AddToClassList(DroppedClass);
                    droppedRow.schedule.Execute(() => droppedRow.RemoveFromClassList(DroppedClass))
                        .ExecuteLater(DropBounceClearMs);
                    break;
                }
            }
        }

        private VisualElement BuildRow(BookshelfManageRowItem item)
        {
            var row = new VisualElement();
            row.AddToClassList("shelf-manage-row");
            row.pickingMode = PickingMode.Position;
            if (item.IsActive)
            {
                row.AddToClassList(ActiveRowClass);
            }

            var rowIndex = item.Index;
            // userData 에 실제 책장 인덱스(전체 인덱스) 저장 — drag 의 hit-test 결과(element 인덱스)를
            // 실제 책장 인덱스로 변환할 때 사용. navigable list 만 표시할 경우 element index ≠ real index.
            row.userData = rowIndex;

            // Grip — 잡고 drag 하면 행 reorder.
            var grip = new VisualElement();
            grip.AddToClassList("shelf-manage-row-grip");
            grip.pickingMode = PickingMode.Position;
            var gripIcon = new Label("≡");
            gripIcon.AddToClassList("shelf-manage-row-grip-icon");
            gripIcon.pickingMode = PickingMode.Ignore;
            grip.Add(gripIcon);
            grip.RegisterCallback<PointerDownEvent>(evt => OnGripPointerDown(evt, grip, row, rowIndex));
            grip.RegisterCallback<PointerMoveEvent>(evt => OnGripPointerMove(evt, grip));
            grip.RegisterCallback<PointerUpEvent>(evt => OnGripPointerUp(evt, grip));
            grip.RegisterCallback<PointerCancelEvent>(evt => OnGripPointerCancel(evt, grip));
            row.Add(grip);

            // 자식 label 은 picking 받지 않게 해서 click target 을 row 로 일원화한다.
            // 자식이 target 이 되면 ClickEvent bubble 단계에서 row callback 이 여러 번 fire 되는 케이스 차단.
            var marker = new Label(item.IsActive ? "●" : "○");
            marker.AddToClassList("shelf-manage-row-marker");
            marker.pickingMode = PickingMode.Ignore;
            row.Add(marker);

            var nameLabel = new Label(string.IsNullOrEmpty(item.Name) ? "(이름 없음)" : item.Name);
            nameLabel.AddToClassList("shelf-manage-row-name");
            nameLabel.pickingMode = PickingMode.Ignore;
            row.Add(nameLabel);

            var visibilityLabel = new Label(item.VisibilityLabel ?? string.Empty);
            visibilityLabel.AddToClassList("shelf-manage-row-visibility");
            visibilityLabel.pickingMode = PickingMode.Ignore;
            row.Add(visibilityLabel);

            // 행 본문 click → 책장 전환. frame 기반 debounce 로 같은 frame 안 중복 호출 차단.
            // grip click 은 grip 의 PointerDown StopImmediatePropagation 으로 별도 분리됨.
            row.RegisterCallback<ClickEvent>(evt =>
            {
                if (Time.frameCount == lastRowClickFrame && lastRowClickIndex == rowIndex)
                {
                    evt.StopImmediatePropagation();
                    return;
                }
                lastRowClickFrame = Time.frameCount;
                lastRowClickIndex = rowIndex;
                RowSelected?.Invoke(rowIndex);
                evt.StopImmediatePropagation();
            });

            return row;
        }

        // ===== Grip drag handlers (long-press + ghost + drop indicator + bounce) =====
        //
        // 흐름:
        //   1) PointerDown on grip → CapturePointer + long-press timer 시작 + row .arming 클래스 (lift hint)
        //   2) LongPressMs 동안 release/움직임 없으면 → isDragArmed = true, ghost 생성, .arming 제거 + .dragging
        //   2') 그 전 release 또는 큰 이동 → drag 시작 안 함, .arming 제거
        //   3) PointerMove (drag armed) → ghost 위치 update + hit test → cursor 가 row 상/하반인지 판정
        //      → row 에 .drop-above 또는 .drop-below 클래스 + dropAboveTarget 기록
        //   4) PointerUp → ghost 제거. source != computed-target 이면 ReorderRequested(source, target)
        //   5) Manager → MoveBookshelfOrder + Sync → Show(snapshot) → RebuildRows
        //      RebuildRows 끝에 pendingDroppedRowIndex 위치 row 에 .dropped 짧게 → bounce.

        private void OnGripPointerDown(PointerDownEvent evt, VisualElement grip, VisualElement row, int rowIndex)
        {
            if (evt.button != 0)
            {
                return;
            }

            CancelLongPressTimer();
            ResetDragState();

            pendingPointerId = evt.pointerId;
            pendingPointerStartLocal = evt.localPosition;
            pendingGrip = grip;
            pendingRow = row;
            pendingRowIndex = rowIndex;
            row.AddToClassList(ArmingClass);

            grip.CapturePointer(evt.pointerId);
            evt.StopImmediatePropagation();

            // ExecuteLater 는 void 반환이라 chain 불가 — schedule.Execute 결과를 먼저 보관 후 지연 적용.
            longPressTimer = grip.schedule.Execute(BeginArmedDrag);
            longPressTimer.ExecuteLater(LongPressMs);
        }

        private void BeginArmedDrag()
        {
            longPressTimer = null;
            if (pendingRow == null || pendingRowIndex < 0)
            {
                CancelPendingDrag();
                return;
            }

            isDragArmed = true;
            dragSourceIndex = pendingRowIndex;
            dragTargetIndex = pendingRowIndex;
            dropAboveTarget = true;
            draggedRow = pendingRow;
            draggedRow.RemoveFromClassList(ArmingClass);
            draggedRow.AddToClassList(DraggingClass);

            BuildDragGhost();
            UpdateDragGhostPositionFromGrip();
        }

        private void OnGripPointerMove(PointerMoveEvent evt, VisualElement grip)
        {
            if (!grip.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            // long-press 대기 중에 사용자가 일정 거리 이상 움직이면 drag 의도 아님 → cancel.
            if (!isDragArmed)
            {
                var dx = evt.localPosition.x - pendingPointerStartLocal.x;
                var dy = evt.localPosition.y - pendingPointerStartLocal.y;
                if ((dx * dx) + (dy * dy) > LongPressCancelDistancePx * LongPressCancelDistancePx)
                {
                    CancelPendingDrag();
                }
                return;
            }

            // Drag armed — ghost 위치 + drop indicator 갱신.
            var worldPos = grip.LocalToWorld(evt.localPosition);
            UpdateDragGhostPositionAt(worldPos);

            var newTargetElement = HitTestRowIndex(worldPos);
            if (newTargetElement < 0)
            {
                return;
            }

            var newTargetReal = ResolveRealIndexAt(newTargetElement);
            if (newTargetReal < 0)
            {
                return;
            }

            // cursor 가 target row 의 상/하반 중 어디인지 판정.
            var targetRow = activeRowElements[newTargetElement];
            var rowBound = targetRow.worldBound;
            var newDropAbove = worldPos.y < rowBound.center.y;

            if (newTargetReal == dragTargetIndex && newDropAbove == dropAboveTarget)
            {
                return;
            }

            ClearDropIndicatorClasses();
            dragTargetIndex = newTargetReal;
            dropAboveTarget = newDropAbove;
            ApplyDropIndicator(newTargetElement, newDropAbove);
        }

        private void OnGripPointerUp(PointerUpEvent evt, VisualElement grip)
        {
            CancelLongPressTimer();

            if (grip.HasPointerCapture(evt.pointerId))
            {
                grip.ReleasePointer(evt.pointerId);
            }

            if (!isDragArmed)
            {
                // long-press 통과 못 함 — 그냥 click. 아무 일 안 함 (row click 은 row 본문 전용).
                CancelPendingDrag();
                return;
            }

            // 최종 target index 계산 — dropAboveTarget 이 false 면 target+1 위치에 drop.
            var source = dragSourceIndex;
            var hover = dragTargetIndex;
            var target = ComputeFinalTargetIndex(source, hover, dropAboveTarget);

            ResetDragState();

            if (source >= 0 && target >= 0 && source != target)
            {
                // Bounce 위치 — reorder 후 row list 가 새로 만들어지면 target 위치에 dropped class.
                pendingDroppedRowIndex = target;
                ReorderRequested?.Invoke(source, target);
            }
        }

        private void OnGripPointerCancel(PointerCancelEvent evt, VisualElement grip)
        {
            CancelLongPressTimer();
            if (grip.HasPointerCapture(evt.pointerId))
            {
                grip.ReleasePointer(evt.pointerId);
            }
            CancelPendingDrag();
            ResetDragState();
        }

        private static int ComputeFinalTargetIndex(int source, int hoverIndex, bool dropAbove)
        {
            // MoveBookshelfOrder(source, target) 의미는 source 를 target 위치로 이동.
            // hover 가 source 보다 위 (i<source): drop-above 면 hover, drop-below 면 hover+1
            // hover 가 source 보다 아래 (i>source): drop-above 면 hover-1 (source 자리가 빠지면서), drop-below 면 hover
            if (hoverIndex == source)
            {
                return source;
            }

            if (dropAbove)
            {
                return hoverIndex < source ? hoverIndex : hoverIndex - 1;
            }
            else
            {
                return hoverIndex < source ? hoverIndex + 1 : hoverIndex;
            }
        }

        // targetElementIndex 는 activeRowElements 안 인덱스 (시각 처리용).
        // 비교는 real index 기준 (dragSourceIndex 가 real index).
        private void ApplyDropIndicator(int targetElementIndex, bool dropAbove)
        {
            if (targetElementIndex < 0 || targetElementIndex >= activeRowElements.Count)
            {
                return;
            }

            var targetReal = ResolveRealIndexAt(targetElementIndex);
            if (targetReal == dragSourceIndex)
            {
                return;
            }

            var row = activeRowElements[targetElementIndex];
            row.AddToClassList(dropAbove ? DropAboveClass : DropBelowClass);
        }

        // activeRowElements 안 인덱스 → 실제 책장 인덱스 (row.userData) 변환.
        private int ResolveRealIndexAt(int elementIndex)
        {
            if (elementIndex < 0 || elementIndex >= activeRowElements.Count)
            {
                return -1;
            }
            var row = activeRowElements[elementIndex];
            return row?.userData is int i ? i : -1;
        }

        private void ClearDropIndicatorClasses()
        {
            for (var i = 0; i < activeRowElements.Count; i++)
            {
                activeRowElements[i].RemoveFromClassList(DropAboveClass);
                activeRowElements[i].RemoveFromClassList(DropBelowClass);
            }
        }

        private int HitTestRowIndex(Vector2 worldPos)
        {
            for (var i = 0; i < activeRowElements.Count; i++)
            {
                if (activeRowElements[i].worldBound.Contains(worldPos))
                {
                    return i;
                }
            }
            return -1;
        }

        // ===== Long-press / pending drag lifecycle =====

        private void CancelLongPressTimer()
        {
            if (longPressTimer != null)
            {
                longPressTimer.Pause();
                longPressTimer = null;
            }
        }

        private void CancelPendingDrag()
        {
            CancelLongPressTimer();
            if (pendingRow != null)
            {
                pendingRow.RemoveFromClassList(ArmingClass);
            }
            pendingPointerId = -1;
            pendingGrip = null;
            pendingRow = null;
            pendingRowIndex = -1;
        }

        private void ResetDragState()
        {
            DestroyDragGhost();
            if (draggedRow != null)
            {
                draggedRow.RemoveFromClassList(DraggingClass);
            }
            ClearDropIndicatorClasses();
            isDragArmed = false;
            dragSourceIndex = -1;
            dragTargetIndex = -1;
            dropAboveTarget = false;
            draggedRow = null;
            CancelPendingDrag();
        }

        // ===== Floating ghost (cursor 따라다니는 미니 카드) =====

        private void BuildDragGhost()
        {
            if (document == null || pendingRow == null)
            {
                return;
            }

            DestroyDragGhost();

            // source row 의 이름 label 에서 텍스트 추출 (visible 행이라 worldBound 도 valid).
            var rowNameLabel = pendingRow.Q<Label>(className: "shelf-manage-row-name");
            var nameText = rowNameLabel?.text ?? string.Empty;

            dragGhost = new VisualElement();
            dragGhost.AddToClassList("shelf-manage-drag-ghost");
            dragGhost.pickingMode = PickingMode.Ignore;
            dragGhost.style.position = Position.Absolute;

            var numberLabel = new Label((dragSourceIndex + 1).ToString("00"));
            numberLabel.AddToClassList("shelf-manage-drag-ghost-number");
            numberLabel.pickingMode = PickingMode.Ignore;
            dragGhost.Add(numberLabel);

            var nameLabel = new Label(nameText);
            nameLabel.AddToClassList("shelf-manage-drag-ghost-name");
            nameLabel.pickingMode = PickingMode.Ignore;
            dragGhost.Add(nameLabel);

            // Panel root 에 직접 add — modal 전체 위에 떠 있고 hit-test 영향 X.
            // worldPos (panel 좌표) 와 같은 origin 이라 좌표계 일관.
            document.rootVisualElement.Add(dragGhost);
        }

        // long-press 통과 직후 (PointerMove 이전) 한 번 호출되는 초기 위치 — grip 위치 사용.
        private void UpdateDragGhostPositionFromGrip()
        {
            if (pendingGrip == null)
            {
                return;
            }

            var gripBound = pendingGrip.worldBound;
            UpdateDragGhostPositionAt(new Vector2(gripBound.center.x, gripBound.center.y));
        }

        // Pointer 의 panel(root) 좌표 기반으로 ghost 위치 update.
        // ghost 가 cursor 우상단 약간 오프셋에 떠 cursor 자체를 가리지 않게 한다.
        private void UpdateDragGhostPositionAt(Vector2 worldPos)
        {
            if (dragGhost == null || document == null)
            {
                return;
            }

            var panelRoot = document.rootVisualElement;
            var panelBound = panelRoot.worldBound;
            var x = worldPos.x - panelBound.x + 14f;
            var y = worldPos.y - panelBound.y - 22f;
            dragGhost.style.left = x;
            dragGhost.style.top = y;
        }

        private void DestroyDragGhost()
        {
            if (dragGhost != null)
            {
                dragGhost.RemoveFromHierarchy();
                dragGhost = null;
            }
        }

        // 추가 진행 중이면 add button 자체가 "추가 중..." 텍스트 + disabled + USS .is-adding 클래스로 시각 강조.
        private const string AddDefaultText = "＋    새 책장 추가";
        private const string AddInProgressText = "···    새 책장을 추가하는 중입니다";
        private const string AddingClass = "is-adding";

        private void UpdateAddButton(bool inProgress, bool canAdd)
        {
            if (addButton == null)
            {
                return;
            }

            if (inProgress)
            {
                addButton.text = AddInProgressText;
                addButton.SetEnabled(false);
                if (!addButton.ClassListContains(AddingClass))
                {
                    addButton.AddToClassList(AddingClass);
                }
            }
            else
            {
                addButton.text = AddDefaultText;
                addButton.SetEnabled(canAdd);
                addButton.RemoveFromClassList(AddingClass);
            }
        }

        private static void ApplyChipSelected(Button chip, bool selected)
        {
            if (chip == null)
            {
                return;
            }

            if (selected)
            {
                if (!chip.ClassListContains(SelectedChipClass))
                {
                    chip.AddToClassList(SelectedChipClass);
                }
            }
            else
            {
                chip.RemoveFromClassList(SelectedChipClass);
            }
        }

        private static void ApplyEnabled(VisualElement element, bool enabled)
        {
            if (element == null)
            {
                return;
            }

            element.SetEnabled(enabled);
        }

        private void ApplyStatus(string text, bool isError)
        {
            if (statusLabel == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(text))
            {
                statusLabel.text = string.Empty;
                if (!statusLabel.ClassListContains(HiddenClass))
                {
                    statusLabel.AddToClassList(HiddenClass);
                }
                return;
            }

            statusLabel.text = text;
            statusLabel.RemoveFromClassList(HiddenClass);
            if (isError)
            {
                if (!statusLabel.ClassListContains(ErrorClass))
                {
                    statusLabel.AddToClassList(ErrorClass);
                }
            }
            else
            {
                statusLabel.RemoveFromClassList(ErrorClass);
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
