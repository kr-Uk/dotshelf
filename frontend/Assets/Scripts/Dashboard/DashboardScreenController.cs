using System;
using System.Collections.Generic;
using App.Application.Dashboard;
using App.Domain.Dashboard;
using App.Infrastructure.Catalogs;
using App.Infrastructure.Mock.Dashboard;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dotshelf.Scripts.Dashboard
{
    /// <summary>
    /// 대시보드 메인 화면. UIDocument를 호스팅하고 Mock/Api Repository를 주입한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class DashboardScreenController : MonoBehaviour
    {
        public enum ViewMode
        {
            Mine,
            Visitor
        }

        [Header("View Assets")]
        [SerializeField] private VisualTreeAsset viewAsset;
        [SerializeField] private StyleSheet stylesheet;
        [SerializeField] private PostItStyleCatalogAsset postItCatalog;

        [Header("Mode")]
        [SerializeField] private ViewMode viewMode = ViewMode.Mine;
        [SerializeField] private long visitedSpaceId = 1L;
        [SerializeField] private bool useMockRepositories = true;

        private UIDocument document;

        private const int CarouselItemLimit = 10;

        private LoadDashboardUseCase loadDashboardUseCase;
        private ToggleSpaceLikeUseCase toggleLikeUseCase;
        private LoadGuestbookPageUseCase loadGuestbookPageUseCase;
        private LoadOwnedBooksPageUseCase loadOwnedBooksUseCase;
        private LoadReadingRecordsPageUseCase loadReadingRecordsUseCase;
        private LoadFollowersPageUseCase loadFollowersUseCase;
        private LoadFollowingsPageUseCase loadFollowingsUseCase;
        private FollowUserUseCase followUseCase;
        private UnfollowUserUseCase unfollowUseCase;
        private BlockUserUseCase blockUseCase;

        private VisualElement root;
        private Label nicknameLabel;
        private Label introductionLabel;
        private Button followingCountButton;
        private Button followersCountButton;
        private Button likeToggleButton;
        private Label likeCountLabel;
        private Label bookCountLabel;
        private Label recordCountLabel;
        private Label guestbookCountLabel;
        private Label weeklyVisitorLabel;
        private VisualElement guestbookGrid;
        private Button guestbookPrevButton;
        private Button guestbookNextButton;
        private Label guestbookPageLabel;
        private Button editButton;
        private Button writeGuestbookButton;
        private ScrollView booksCarousel;
        private ScrollView recordsCarousel;

        private VisualElement userListModalRoot;
        private VisualElement userListModalBackdrop;
        private VisualElement userListModalList;
        private Label userListModalTitle;
        private Button userListModalCloseButton;
        private Label userListModalEmpty;

        private enum UserListMode
        {
            Followers,
            Followings
        }

        private UserListMode currentUserListMode;

        private DashboardSnapshot currentSnapshot;
        private bool likeRequestInFlight;
        private readonly List<string> guestbookCursorHistory = new List<string> { null };
        private int currentGuestbookPageIndex;
        private string nextGuestbookCursor;

        private void Awake()
        {
            document = GetComponent<UIDocument>();
            if (viewAsset != null)
            {
                document.visualTreeAsset = viewAsset;
            }
            BuildUseCases();
        }

        private void OnEnable()
        {
            root = document.rootVisualElement;
            if (root == null)
            {
                Debug.LogWarning("[Dashboard] UIDocument rootVisualElement is null.");
                return;
            }

            if (stylesheet != null && !root.styleSheets.Contains(stylesheet))
            {
                root.styleSheets.Add(stylesheet);
            }

            QueryUI();
            WireEvents();
            ApplyViewMode();
            LoadInitialData();
        }

        private void OnDisable()
        {
            UnwireEvents();
        }

        private void BuildUseCases()
        {
            // P0c는 Mock-only로 가동. Api 토글은 P1+에서 SceneInstaller로 위임 예정.
            IDashboardRepository dashboardRepo = new MockDashboardRepository();
            IGuestbookRepository guestbookRepo = new MockGuestbookRepository();
            ILibraryViewRepository libraryRepo = new MockLibraryViewRepository();
            IFollowRepository followRepo = new MockFollowRepository();

            loadDashboardUseCase = new LoadDashboardUseCase(dashboardRepo);
            toggleLikeUseCase = new ToggleSpaceLikeUseCase(dashboardRepo);
            loadGuestbookPageUseCase = new LoadGuestbookPageUseCase(guestbookRepo);
            loadOwnedBooksUseCase = new LoadOwnedBooksPageUseCase(libraryRepo);
            loadReadingRecordsUseCase = new LoadReadingRecordsPageUseCase(libraryRepo);
            loadFollowersUseCase = new LoadFollowersPageUseCase(followRepo);
            loadFollowingsUseCase = new LoadFollowingsPageUseCase(followRepo);
            followUseCase = new FollowUserUseCase(followRepo);
            unfollowUseCase = new UnfollowUserUseCase(followRepo);
            blockUseCase = new BlockUserUseCase(followRepo);
        }

        private void QueryUI()
        {
            nicknameLabel = root.Q<Label>("nickname-label");
            introductionLabel = root.Q<Label>("introduction-label");
            followingCountButton = root.Q<Button>("following-count-label");
            followersCountButton = root.Q<Button>("followers-count-label");
            likeToggleButton = root.Q<Button>("like-toggle-button");
            likeCountLabel = root.Q<Label>("like-count-label");
            bookCountLabel = root.Q<Label>("book-count-label");
            recordCountLabel = root.Q<Label>("record-count-label");
            guestbookCountLabel = root.Q<Label>("guestbook-count-label");
            weeklyVisitorLabel = root.Q<Label>("weekly-visitor-label");
            guestbookGrid = root.Q<VisualElement>("guestbook-grid");
            guestbookPrevButton = root.Q<Button>("guestbook-prev-button");
            guestbookNextButton = root.Q<Button>("guestbook-next-button");
            guestbookPageLabel = root.Q<Label>("guestbook-page-label");
            editButton = root.Q<Button>("edit-dashboard-button");
            writeGuestbookButton = root.Q<Button>("write-guestbook-button");
            booksCarousel = root.Q<ScrollView>("recent-books-carousel");
            recordsCarousel = root.Q<ScrollView>("recent-records-carousel");

            userListModalRoot = root.Q<VisualElement>("user-list-modal");
            userListModalBackdrop = root.Q<VisualElement>("user-list-modal-backdrop");
            userListModalList = root.Q<VisualElement>("user-list-modal-list");
            userListModalTitle = root.Q<Label>("user-list-modal-title");
            userListModalCloseButton = root.Q<Button>("user-list-modal-close-button");
            userListModalEmpty = root.Q<Label>("user-list-modal-empty");
        }

        private void WireEvents()
        {
            if (likeToggleButton != null)
            {
                likeToggleButton.clicked += OnLikeButtonClicked;
            }
            if (guestbookPrevButton != null)
            {
                guestbookPrevButton.clicked += OnGuestbookPrev;
            }
            if (guestbookNextButton != null)
            {
                guestbookNextButton.clicked += OnGuestbookNext;
            }
            if (editButton != null)
            {
                editButton.clicked += OnEditClicked;
            }
            if (writeGuestbookButton != null)
            {
                writeGuestbookButton.clicked += OnWriteGuestbookClicked;
            }
            if (booksCarousel != null)
            {
                EnableHorizontalDragScroll(booksCarousel);
            }
            if (recordsCarousel != null)
            {
                EnableHorizontalDragScroll(recordsCarousel);
            }
            if (followersCountButton != null)
            {
                followersCountButton.clicked += OnFollowersClicked;
            }
            if (followingCountButton != null)
            {
                followingCountButton.clicked += OnFollowingsClicked;
            }
            if (userListModalCloseButton != null)
            {
                userListModalCloseButton.clicked += CloseUserListModal;
            }
            if (userListModalBackdrop != null)
            {
                userListModalBackdrop.RegisterCallback<ClickEvent>(_ => CloseUserListModal());
            }
        }

        private void UnwireEvents()
        {
            if (likeToggleButton != null) likeToggleButton.clicked -= OnLikeButtonClicked;
            if (guestbookPrevButton != null) guestbookPrevButton.clicked -= OnGuestbookPrev;
            if (guestbookNextButton != null) guestbookNextButton.clicked -= OnGuestbookNext;
            if (editButton != null) editButton.clicked -= OnEditClicked;
            if (writeGuestbookButton != null) writeGuestbookButton.clicked -= OnWriteGuestbookClicked;
            if (followersCountButton != null) followersCountButton.clicked -= OnFollowersClicked;
            if (followingCountButton != null) followingCountButton.clicked -= OnFollowingsClicked;
            if (userListModalCloseButton != null) userListModalCloseButton.clicked -= CloseUserListModal;
        }

        private void ApplyViewMode()
        {
            // 본인 → 편집 / 방문자 → 방명록 작성. 같은 슬롯에서 토글.
            bool isMine = viewMode == ViewMode.Mine;
            if (editButton != null)
            {
                editButton.style.display = isMine ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (writeGuestbookButton != null)
            {
                writeGuestbookButton.style.display = isMine ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (likeToggleButton != null)
            {
                // 본인 공간엔 클릭 비활성 (정적 표시), 방문자 공간엔 토글 가능.
                if (isMine)
                {
                    likeToggleButton.AddToClassList("read-only");
                }
                else
                {
                    likeToggleButton.RemoveFromClassList("read-only");
                }
            }
        }

        private void LoadInitialData()
        {
            currentGuestbookPageIndex = 0;
            guestbookCursorHistory.Clear();
            guestbookCursorHistory.Add(null);

            if (viewMode == ViewMode.Mine)
            {
                loadDashboardUseCase.ExecuteForMine(OnDashboardLoaded, OnError);
            }
            else
            {
                loadDashboardUseCase.Execute(visitedSpaceId, OnDashboardLoaded, OnError);
            }
        }

        private void OnDashboardLoaded(DashboardSnapshot snapshot)
        {
            currentSnapshot = snapshot;
            RenderHeader(snapshot);
            LoadGuestbookPage(null);
            LoadRecentBooks();
            LoadRecentRecords();
        }

        private void LoadRecentBooks()
        {
            long spaceId = currentSnapshot?.Space.Id ?? visitedSpaceId;
            loadOwnedBooksUseCase.Execute(spaceId, OwnedBookSort.AddedDesc, null,
                onSuccess: RenderBooksCarousel,
                onError: OnError);
        }

        private void LoadRecentRecords()
        {
            long ownerId = currentSnapshot?.Space.OwnerUserId ?? 0L;
            loadReadingRecordsUseCase.Execute(ownerId, ReadingRecordSort.Latest, null,
                onSuccess: RenderRecordsCarousel,
                onError: OnError);
        }

        private void RenderBooksCarousel(OwnedBookListPage page)
        {
            if (booksCarousel == null)
            {
                return;
            }

            booksCarousel.Clear();
            int count = Math.Min(CarouselItemLimit, page.Items.Count);
            for (int i = 0; i < count; i++)
            {
                booksCarousel.Add(BuildBookCell(page.Items[i]));
            }
        }

        private static VisualElement BuildBookCell(OwnedBookItem item)
        {
            VisualElement cell = new VisualElement();
            cell.AddToClassList("carousel-cell");

            VisualElement cover = new VisualElement();
            cover.AddToClassList("book-cover");
            cell.Add(cover);

            Label title = new Label(item.Title ?? string.Empty);
            title.AddToClassList("book-cell-title");
            cell.Add(title);

            return cell;
        }

        private void RenderRecordsCarousel(ReadingRecordListPage page)
        {
            if (recordsCarousel == null)
            {
                return;
            }

            recordsCarousel.Clear();
            int count = Math.Min(CarouselItemLimit, page.Items.Count);
            for (int i = 0; i < count; i++)
            {
                recordsCarousel.Add(BuildRecordCell(page.Items[i]));
            }
        }

        private static VisualElement BuildRecordCell(ReadingRecordItem item)
        {
            VisualElement cell = new VisualElement();
            cell.AddToClassList("record-cell");

            Label title = new Label(item.BookTitle ?? string.Empty);
            title.AddToClassList("record-cell-title");
            cell.Add(title);

            Label content = new Label(TruncateContent(item.Content, 50));
            content.AddToClassList("record-cell-content");
            cell.Add(content);

            VisualElement metaRow = new VisualElement();
            metaRow.AddToClassList("record-cell-meta");
            Label likes = new Label($"♥ {item.LikeCount}");
            likes.AddToClassList("record-cell-meta-item");
            metaRow.Add(likes);
            Label comments = new Label($"💬 {item.CommentCount}");
            comments.AddToClassList("record-cell-meta-item");
            metaRow.Add(comments);
            cell.Add(metaRow);

            return cell;
        }

        private static string TruncateContent(string content, int maxLength)
        {
            if (string.IsNullOrEmpty(content))
            {
                return string.Empty;
            }

            return content.Length <= maxLength
                ? content
                : content.Substring(0, maxLength) + "…";
        }

        private void RenderHeader(DashboardSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            if (nicknameLabel != null) nicknameLabel.text = snapshot.OwnerNickname;
            if (introductionLabel != null) introductionLabel.text = snapshot.OwnerIntroduction;
            if (followingCountButton != null) followingCountButton.text = $"팔로잉 {snapshot.FollowingCount}";
            if (followersCountButton != null) followersCountButton.text = $"팔로워 {snapshot.FollowerCount}";
            if (likeCountLabel != null) likeCountLabel.text = snapshot.Space.LikeCount.ToString();
            if (bookCountLabel != null) bookCountLabel.text = snapshot.BookCount.ToString();
            if (recordCountLabel != null) recordCountLabel.text = snapshot.ReadingRecordCount.ToString();
            if (weeklyVisitorLabel != null) weeklyVisitorLabel.text = $"이번주 방문자 {snapshot.WeeklyVisitorCount}";

            UpdateLikeButton(snapshot.Space.Liked);
        }

        private void UpdateLikeButton(bool liked)
        {
            if (likeToggleButton == null)
            {
                return;
            }

            likeToggleButton.text = liked ? "♥" : "♡";
            if (liked)
            {
                likeToggleButton.AddToClassList("liked");
            }
            else
            {
                likeToggleButton.RemoveFromClassList("liked");
            }
        }

        private void OnLikeButtonClicked()
        {
            if (viewMode == ViewMode.Mine || likeRequestInFlight || currentSnapshot == null)
            {
                return;
            }

            likeRequestInFlight = true;
            bool nextLiked = !currentSnapshot.Space.Liked;
            UpdateLikeButton(nextLiked); // 낙관적 업데이트

            toggleLikeUseCase.Execute(
                currentSnapshot.Space.Id,
                nextLiked,
                onSuccess: result =>
                {
                    likeRequestInFlight = false;
                    if (likeCountLabel != null)
                    {
                        likeCountLabel.text = result.LikeCount.ToString();
                    }
                    UpdateLikeButton(result.Liked);
                    // snapshot도 갱신 — 다음 토글이 새로운 상태에서 시작되도록
                    currentSnapshot = RebuildSnapshotWithLike(currentSnapshot, result);
                },
                onError: error =>
                {
                    likeRequestInFlight = false;
                    UpdateLikeButton(currentSnapshot.Space.Liked); // 롤백
                    Debug.LogWarning($"[Dashboard] Like toggle failed: {error.Code} {error.Message}");
                });
        }

        private static DashboardSnapshot RebuildSnapshotWithLike(DashboardSnapshot snapshot, LikeToggleResult result)
        {
            App.Domain.Dashboard.Space updated = new App.Domain.Dashboard.Space(
                id: snapshot.Space.Id,
                ownerUserId: snapshot.Space.OwnerUserId,
                visibility: snapshot.Space.Visibility,
                guestbookVisibility: snapshot.Space.GuestbookVisibility,
                visitCount: snapshot.Space.VisitCount,
                likeCount: result.LikeCount,
                liked: result.Liked,
                layoutVersion: snapshot.Space.LayoutVersion,
                currentThemeInventoryId: snapshot.Space.CurrentThemeInventoryId);

            return new DashboardSnapshot(
                space: updated,
                ownerNickname: snapshot.OwnerNickname,
                ownerProfileImageUrl: snapshot.OwnerProfileImageUrl,
                ownerIntroduction: snapshot.OwnerIntroduction,
                followerCount: snapshot.FollowerCount,
                followingCount: snapshot.FollowingCount,
                bookshelfCount: snapshot.BookshelfCount,
                bookCount: snapshot.BookCount,
                readingRecordCount: snapshot.ReadingRecordCount,
                weeklyVisitorCount: snapshot.WeeklyVisitorCount);
        }

        private void LoadGuestbookPage(string cursor)
        {
            long spaceId = currentSnapshot?.Space.Id ?? visitedSpaceId;
            loadGuestbookPageUseCase.Execute(spaceId, cursor,
                onSuccess: page =>
                {
                    nextGuestbookCursor = page.NextCursor;
                    RenderGuestbookGrid(page);
                    UpdateGuestbookCount(page);
                    UpdateGuestbookPagination();
                },
                onError: OnError);
        }

        private void RenderGuestbookGrid(GuestbookListPage page)
        {
            if (guestbookGrid == null)
            {
                return;
            }

            guestbookGrid.Clear();
            for (int i = 0; i < page.Items.Count; i++)
            {
                guestbookGrid.Add(BuildGuestbookCell(page.Items[i]));
            }
        }

        private VisualElement BuildGuestbookCell(Guestbook guestbook)
        {
            VisualElement cell = new VisualElement();
            cell.AddToClassList("guestbook-cell");
            cell.userData = guestbook;

            Label content = new Label(guestbook.Content);
            content.AddToClassList("guestbook-cell-content");
            cell.Add(content);

            if (guestbook.HasReply)
            {
                Label reply = new Label("↳ " + guestbook.Reply.Content);
                reply.AddToClassList("guestbook-cell-reply");
                cell.Add(reply);
            }

            Label nickname = new Label(string.IsNullOrEmpty(guestbook.DisplayedNickname) ? "익명" : guestbook.DisplayedNickname);
            nickname.AddToClassList("guestbook-cell-nickname");
            cell.Add(nickname);

            cell.RegisterCallback<ClickEvent>(_ => OnGuestbookCellClicked(guestbook));
            return cell;
        }

        private void OnGuestbookCellClicked(Guestbook guestbook)
        {
            // P2에서 답장/신고/삭제 모달 진입. 여기선 로그만.
            Debug.Log($"[Dashboard] Guestbook clicked: id={guestbook.Id} content={guestbook.Content}");
        }

        private void UpdateGuestbookCount(GuestbookListPage page)
        {
            if (guestbookCountLabel == null)
            {
                return;
            }

            // P0c는 단일 페이지만 카운트로 표시. 추후 백엔드 totalCount 응답에 맞춰 갱신.
            guestbookCountLabel.text = $"방명록 {page.Items.Count}";
        }

        private void UpdateGuestbookPagination()
        {
            if (guestbookPageLabel != null)
            {
                guestbookPageLabel.text = (currentGuestbookPageIndex + 1).ToString();
            }
            if (guestbookPrevButton != null)
            {
                guestbookPrevButton.SetEnabled(currentGuestbookPageIndex > 0);
            }
            if (guestbookNextButton != null)
            {
                guestbookNextButton.SetEnabled(!string.IsNullOrEmpty(nextGuestbookCursor));
            }
        }

        private void OnGuestbookPrev()
        {
            if (currentGuestbookPageIndex <= 0)
            {
                return;
            }

            currentGuestbookPageIndex--;
            string cursor = guestbookCursorHistory[currentGuestbookPageIndex];
            // 다음 페이지 캐시 무효화
            if (guestbookCursorHistory.Count > currentGuestbookPageIndex + 1)
            {
                guestbookCursorHistory.RemoveRange(currentGuestbookPageIndex + 1, guestbookCursorHistory.Count - currentGuestbookPageIndex - 1);
            }
            LoadGuestbookPage(cursor);
        }

        private void OnGuestbookNext()
        {
            if (string.IsNullOrEmpty(nextGuestbookCursor))
            {
                return;
            }

            currentGuestbookPageIndex++;
            if (guestbookCursorHistory.Count <= currentGuestbookPageIndex)
            {
                guestbookCursorHistory.Add(nextGuestbookCursor);
            }
            else
            {
                guestbookCursorHistory[currentGuestbookPageIndex] = nextGuestbookCursor;
            }
            LoadGuestbookPage(nextGuestbookCursor);
        }

        private void OnEditClicked()
        {
            // P2c: 대시보드 편집 모달 진입 예정.
            Debug.Log("[Dashboard] Edit clicked (modal pending P2).");
        }

        private void OnWriteGuestbookClicked()
        {
            // P2a: 방명록 작성 모달 진입 예정.
            Debug.Log("[Dashboard] Write guestbook clicked (modal pending P2).");
        }

        private void OnError(DashboardApiError error)
        {
            Debug.LogWarning($"[Dashboard] API error: {error?.Code} {error?.Message}");
        }

        // ===== 팔로워/팔로잉 모달 =====

        private void OnFollowersClicked()
        {
            OpenUserListModal(UserListMode.Followers);
        }

        private void OnFollowingsClicked()
        {
            OpenUserListModal(UserListMode.Followings);
        }

        private void OpenUserListModal(UserListMode mode)
        {
            currentUserListMode = mode;
            if (userListModalTitle != null)
            {
                userListModalTitle.text = mode == UserListMode.Followers ? "팔로워" : "팔로잉";
            }
            if (userListModalRoot != null)
            {
                userListModalRoot.RemoveFromClassList("hidden");
            }
            LoadUserListPage();
        }

        private void CloseUserListModal()
        {
            if (userListModalRoot != null)
            {
                userListModalRoot.AddToClassList("hidden");
            }
            if (userListModalList != null)
            {
                userListModalList.Clear();
            }
        }

        private void LoadUserListPage()
        {
            long userId = currentSnapshot?.Space.OwnerUserId ?? 0L;
            if (currentUserListMode == UserListMode.Followers)
            {
                loadFollowersUseCase.Execute(userId, null,
                    onSuccess: RenderUserList,
                    onError: OnError);
            }
            else
            {
                loadFollowingsUseCase.Execute(userId, null,
                    onSuccess: RenderUserList,
                    onError: OnError);
            }
        }

        private void RenderUserList(UserListPage page)
        {
            if (userListModalList == null)
            {
                return;
            }

            userListModalList.Clear();
            if (page.Items.Count == 0)
            {
                if (userListModalEmpty != null) userListModalEmpty.RemoveFromClassList("hidden");
                return;
            }

            if (userListModalEmpty != null) userListModalEmpty.AddToClassList("hidden");

            for (int i = 0; i < page.Items.Count; i++)
            {
                userListModalList.Add(BuildUserRow(page.Items[i]));
            }
        }

        private VisualElement BuildUserRow(UserSummary user)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("user-row");

            VisualElement avatar = new VisualElement();
            avatar.AddToClassList("user-row-image");
            row.Add(avatar);

            VisualElement textBlock = new VisualElement();
            textBlock.AddToClassList("user-row-text");
            Label nickname = new Label(user.Nickname);
            nickname.AddToClassList("user-row-nickname");
            textBlock.Add(nickname);
            if (user.Mutual)
            {
                Label tag = new Label("맞팔");
                tag.AddToClassList("user-row-tag");
                textBlock.Add(tag);
            }
            row.Add(textBlock);

            VisualElement actions = new VisualElement();
            actions.AddToClassList("user-row-actions");

            // 팔로우/언팔로우
            Button followBtn = new Button();
            followBtn.AddToClassList("user-row-action-button");
            UpdateFollowButton(followBtn, user.Following);
            followBtn.clicked += () => OnToggleFollow(followBtn, user);
            actions.Add(followBtn);

            // 본인 공간일 때 팔로워 모달에 한해서만 차단 노출
            bool canBlock = viewMode == ViewMode.Mine && currentUserListMode == UserListMode.Followers;
            if (canBlock)
            {
                Button blockBtn = new Button { text = "차단" };
                blockBtn.AddToClassList("user-row-action-button");
                blockBtn.AddToClassList("danger");
                blockBtn.clicked += () => OnBlockUser(row, user);
                actions.Add(blockBtn);
            }

            row.Add(actions);
            return row;
        }

        private static void UpdateFollowButton(Button button, bool following)
        {
            button.text = following ? "팔로잉" : "팔로우";
            if (following)
            {
                button.RemoveFromClassList("primary");
            }
            else
            {
                button.AddToClassList("primary");
            }
        }

        private void OnToggleFollow(Button button, UserSummary user)
        {
            bool wasFollowing = button.text == "팔로잉";
            UpdateFollowButton(button, !wasFollowing);
            button.SetEnabled(false);
            if (wasFollowing)
            {
                unfollowUseCase.Execute(user.Id,
                    onSuccess: result =>
                    {
                        button.SetEnabled(true);
                        UpdateFollowButton(button, result.Following);
                    },
                    onError: error =>
                    {
                        button.SetEnabled(true);
                        UpdateFollowButton(button, wasFollowing);
                        OnError(error);
                    });
            }
            else
            {
                followUseCase.Execute(user.Id,
                    onSuccess: result =>
                    {
                        button.SetEnabled(true);
                        UpdateFollowButton(button, result.Following);
                    },
                    onError: error =>
                    {
                        button.SetEnabled(true);
                        UpdateFollowButton(button, wasFollowing);
                        OnError(error);
                    });
            }
        }

        private void OnBlockUser(VisualElement row, UserSummary user)
        {
            blockUseCase.Execute(user.Id,
                onSuccess: _ => row.RemoveFromHierarchy(),
                onError: OnError);
        }

        // ===== 캐러셀 드래그 스크롤 =====

        /// <summary>
        /// ScrollView 영역에 마우스 좌클릭+드래그로 가로 스크롤할 수 있게 한다.
        /// 5px 이상 이동해야 드래그로 인식 → 짧은 클릭은 자식 셀로 전달.
        /// </summary>
        private static void EnableHorizontalDragScroll(ScrollView scrollView)
        {
            const float dragThreshold = 5f;
            bool dragging = false;
            Vector2 startMouse = Vector2.zero;
            Vector2 startOffset = Vector2.zero;
            int activePointerId = -1;

            scrollView.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }
                dragging = false;
                startMouse = evt.position;
                startOffset = scrollView.scrollOffset;
                activePointerId = evt.pointerId;
            });

            scrollView.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (activePointerId != evt.pointerId)
                {
                    return;
                }
                Vector2 current = evt.position;
                float dx = current.x - startMouse.x;
                if (!dragging && Mathf.Abs(dx) >= dragThreshold)
                {
                    dragging = true;
                    scrollView.CapturePointer(evt.pointerId);
                }
                if (dragging)
                {
                    scrollView.scrollOffset = new Vector2(startOffset.x - dx, startOffset.y);
                    evt.StopPropagation();
                }
            });

            scrollView.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (activePointerId != evt.pointerId)
                {
                    return;
                }
                if (dragging && scrollView.HasPointerCapture(evt.pointerId))
                {
                    scrollView.ReleasePointer(evt.pointerId);
                    evt.StopPropagation();
                }
                activePointerId = -1;
                dragging = false;
            });
        }
    }
}
