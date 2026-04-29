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

        private VisualElement root;
        private Label nicknameLabel;
        private Label introductionLabel;
        private Label followingCountLabel;
        private Label followersCountLabel;
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
        private VisualElement booksCarousel;
        private VisualElement recordsCarousel;

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

            loadDashboardUseCase = new LoadDashboardUseCase(dashboardRepo);
            toggleLikeUseCase = new ToggleSpaceLikeUseCase(dashboardRepo);
            loadGuestbookPageUseCase = new LoadGuestbookPageUseCase(guestbookRepo);
            loadOwnedBooksUseCase = new LoadOwnedBooksPageUseCase(libraryRepo);
            loadReadingRecordsUseCase = new LoadReadingRecordsPageUseCase(libraryRepo);
        }

        private void QueryUI()
        {
            nicknameLabel = root.Q<Label>("nickname-label");
            introductionLabel = root.Q<Label>("introduction-label");
            followingCountLabel = root.Q<Label>("following-count-label");
            followersCountLabel = root.Q<Label>("followers-count-label");
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
            booksCarousel = root.Q<VisualElement>("recent-books-carousel");
            recordsCarousel = root.Q<VisualElement>("recent-records-carousel");
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
        }

        private void UnwireEvents()
        {
            if (likeToggleButton != null) likeToggleButton.clicked -= OnLikeButtonClicked;
            if (guestbookPrevButton != null) guestbookPrevButton.clicked -= OnGuestbookPrev;
            if (guestbookNextButton != null) guestbookNextButton.clicked -= OnGuestbookNext;
            if (editButton != null) editButton.clicked -= OnEditClicked;
            if (writeGuestbookButton != null) writeGuestbookButton.clicked -= OnWriteGuestbookClicked;
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
            if (followingCountLabel != null) followingCountLabel.text = $"팔로잉 {snapshot.FollowingCount}";
            if (followersCountLabel != null) followersCountLabel.text = $"팔로워 {snapshot.FollowerCount}";
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
    }
}
