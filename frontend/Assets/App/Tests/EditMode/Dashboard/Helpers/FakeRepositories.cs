using System;
using System.Collections.Generic;
using App.Application.Dashboard;
using App.Domain.Dashboard;

namespace App.Tests.EditMode.Dashboard.Helpers
{
    internal sealed class FakeDashboardRepository : IDashboardRepository
    {
        public int LoadDashboardCalls;
        public int LoadMyDashboardCalls;
        public int ToggleSpaceLikeCalls;
        public long LastToggleSpaceId;
        public bool LastToggleLiked;
        public DashboardSnapshot SnapshotToReturn;
        public DashboardApiError ErrorToReturn;
        public LikeToggleResult LikeResultToReturn;

        public void LoadDashboard(long spaceId, Action<DashboardSnapshot> onSuccess, Action<DashboardApiError> onError)
        {
            LoadDashboardCalls++;
            DispatchSnapshot(onSuccess, onError);
        }

        public void LoadMyDashboard(Action<DashboardSnapshot> onSuccess, Action<DashboardApiError> onError)
        {
            LoadMyDashboardCalls++;
            DispatchSnapshot(onSuccess, onError);
        }

        public void ToggleSpaceLike(long spaceId, bool liked, Action<LikeToggleResult> onSuccess, Action<DashboardApiError> onError)
        {
            ToggleSpaceLikeCalls++;
            LastToggleSpaceId = spaceId;
            LastToggleLiked = liked;
            if (ErrorToReturn != null)
            {
                onError(ErrorToReturn);
                return;
            }

            onSuccess(LikeResultToReturn ?? new LikeToggleResult { Liked = liked, LikeCount = 1 });
        }

        private void DispatchSnapshot(Action<DashboardSnapshot> onSuccess, Action<DashboardApiError> onError)
        {
            if (ErrorToReturn != null)
            {
                onError(ErrorToReturn);
                return;
            }

            onSuccess(SnapshotToReturn ?? CreateDefaultSnapshot());
        }

        private static DashboardSnapshot CreateDefaultSnapshot()
        {
            Space space = new Space(
                id: 1,
                ownerUserId: 10,
                visibility: VisibilityScope.Public,
                guestbookVisibility: VisibilityScope.Public,
                visitCount: 0,
                likeCount: 0,
                liked: false,
                layoutVersion: 1);
            return new DashboardSnapshot(space, "owner", null, "hi", 0, 0, 0, 0, 0, 0);
        }
    }

    internal sealed class FakeGuestbookRepository : IGuestbookRepository
    {
        public WriteGuestbookCommand LastWriteCommand;
        public WriteGuestbookReplyCommand LastReplyCommand;
        public ReportGuestbookCommand LastReportCommand;
        public int LoadPageCalls;
        public int DeleteCalls;
        public int DeleteReplyCalls;
        public DashboardApiError ErrorToReturn;

        public void LoadPage(long spaceId, string cursor, int limit, Action<GuestbookListPage> onSuccess, Action<DashboardApiError> onError)
        {
            LoadPageCalls++;
            if (ErrorToReturn != null)
            {
                onError(ErrorToReturn);
                return;
            }

            onSuccess(new GuestbookListPage(new List<Guestbook>(), null));
        }

        public void Write(WriteGuestbookCommand command, Action<Guestbook> onSuccess, Action<DashboardApiError> onError)
        {
            LastWriteCommand = command;
            if (ErrorToReturn != null)
            {
                onError(ErrorToReturn);
                return;
            }

            onSuccess(new Guestbook(
                id: 100,
                spaceId: command.SpaceId,
                writerUserId: 99,
                displayedNickname: command.Anonymous ? "익명" : "me",
                anonymous: command.Anonymous,
                postItInventoryId: command.PostItInventoryId,
                content: command.Content,
                visibility: VisibilityScope.Public,
                createdAt: DateTimeOffset.UtcNow));
        }

        public void WriteReply(WriteGuestbookReplyCommand command, Action<GuestbookReply> onSuccess, Action<DashboardApiError> onError)
        {
            LastReplyCommand = command;
            if (ErrorToReturn != null)
            {
                onError(ErrorToReturn);
                return;
            }

            onSuccess(new GuestbookReply(
                id: 200,
                guestbookId: command.GuestbookId,
                writerUserId: 1,
                writerNickname: "owner",
                postItInventoryId: command.PostItInventoryId,
                content: command.Content,
                createdAt: DateTimeOffset.UtcNow));
        }

        public void Delete(long guestbookId, Action onSuccess, Action<DashboardApiError> onError)
        {
            DeleteCalls++;
            if (ErrorToReturn != null)
            {
                onError(ErrorToReturn);
                return;
            }

            onSuccess();
        }

        public void DeleteReply(long guestbookId, long replyId, Action onSuccess, Action<DashboardApiError> onError)
        {
            DeleteReplyCalls++;
            if (ErrorToReturn != null)
            {
                onError(ErrorToReturn);
                return;
            }

            onSuccess();
        }

        public void Report(ReportGuestbookCommand command, Action onSuccess, Action<DashboardApiError> onError)
        {
            LastReportCommand = command;
            if (ErrorToReturn != null)
            {
                onError(ErrorToReturn);
                return;
            }

            onSuccess();
        }
    }

    internal sealed class FakeSpaceSettingsRepository : ISpaceSettingsRepository
    {
        public string LastIntroduction;
        public VisibilityScope? LastVisibility;
        public GuestbookPostItWhitelist LastWhitelist;
        public int LoadWhitelistCalls;
        public DashboardApiError ErrorToReturn;
        public GuestbookPostItWhitelist WhitelistToReturn;

        public void UpdateIntroduction(string introduction, Action onSuccess, Action<DashboardApiError> onError)
        {
            LastIntroduction = introduction;
            if (ErrorToReturn != null)
            {
                onError(ErrorToReturn);
                return;
            }

            onSuccess();
        }

        public void UpdateGuestbookVisibility(VisibilityScope scope, Action onSuccess, Action<DashboardApiError> onError)
        {
            LastVisibility = scope;
            if (ErrorToReturn != null)
            {
                onError(ErrorToReturn);
                return;
            }

            onSuccess();
        }

        public void LoadGuestbookPostItWhitelist(Action<GuestbookPostItWhitelist> onSuccess, Action<DashboardApiError> onError)
        {
            LoadWhitelistCalls++;
            if (ErrorToReturn != null)
            {
                onError(ErrorToReturn);
                return;
            }

            onSuccess(WhitelistToReturn ?? new GuestbookPostItWhitelist(true, null));
        }

        public void UpdateGuestbookPostItWhitelist(GuestbookPostItWhitelist whitelist, Action onSuccess, Action<DashboardApiError> onError)
        {
            LastWhitelist = whitelist;
            if (ErrorToReturn != null)
            {
                onError(ErrorToReturn);
                return;
            }

            onSuccess();
        }

        public long? LastThemeInventoryId;

        public void UpdateCurrentTheme(long? themeInventoryId, Action onSuccess, Action<DashboardApiError> onError)
        {
            LastThemeInventoryId = themeInventoryId;
            if (ErrorToReturn != null)
            {
                onError(ErrorToReturn);
                return;
            }

            onSuccess();
        }
    }

    internal sealed class FakeFollowRepository : IFollowRepository
    {
        public int LoadFollowersCalls;
        public int LoadFollowingsCalls;
        public long LastFollowTarget;
        public long LastUnfollowTarget;
        public long LastBlockTarget;
        public DashboardApiError ErrorToReturn;

        public void LoadFollowers(long userId, string cursor, int limit, Action<UserListPage> onSuccess, Action<DashboardApiError> onError)
        {
            LoadFollowersCalls++;
            if (ErrorToReturn != null)
            {
                onError(ErrorToReturn);
                return;
            }

            onSuccess(new UserListPage(new List<UserSummary>(), null));
        }

        public void LoadFollowings(long userId, string cursor, int limit, Action<UserListPage> onSuccess, Action<DashboardApiError> onError)
        {
            LoadFollowingsCalls++;
            if (ErrorToReturn != null)
            {
                onError(ErrorToReturn);
                return;
            }

            onSuccess(new UserListPage(new List<UserSummary>(), null));
        }

        public void Follow(long targetUserId, Action<FollowToggleResult> onSuccess, Action<DashboardApiError> onError)
        {
            LastFollowTarget = targetUserId;
            if (ErrorToReturn != null)
            {
                onError(ErrorToReturn);
                return;
            }

            onSuccess(new FollowToggleResult { Following = true, Mutual = false });
        }

        public void Unfollow(long targetUserId, Action<FollowToggleResult> onSuccess, Action<DashboardApiError> onError)
        {
            LastUnfollowTarget = targetUserId;
            if (ErrorToReturn != null)
            {
                onError(ErrorToReturn);
                return;
            }

            onSuccess(new FollowToggleResult { Following = false, Mutual = false });
        }

        public void Block(long targetUserId, Action<BlockToggleResult> onSuccess, Action<DashboardApiError> onError)
        {
            LastBlockTarget = targetUserId;
            if (ErrorToReturn != null)
            {
                onError(ErrorToReturn);
                return;
            }

            onSuccess(new BlockToggleResult { Blocked = true });
        }
    }

    internal sealed class FakeLibraryViewRepository : ILibraryViewRepository
    {
        public int LoadOwnedBooksCalls;
        public int LoadReadingRecordsCalls;
        public OwnedBookSort LastOwnedSort;
        public ReadingRecordSort LastRecordSort;
        public DashboardApiError ErrorToReturn;

        public void LoadOwnedBooks(long spaceId, OwnedBookSort sort, string cursor, int limit, Action<OwnedBookListPage> onSuccess, Action<DashboardApiError> onError)
        {
            LoadOwnedBooksCalls++;
            LastOwnedSort = sort;
            if (ErrorToReturn != null)
            {
                onError(ErrorToReturn);
                return;
            }

            onSuccess(new OwnedBookListPage(new List<OwnedBookItem>(), null));
        }

        public void LoadReadingRecords(long userId, ReadingRecordSort sort, string cursor, int limit, Action<ReadingRecordListPage> onSuccess, Action<DashboardApiError> onError)
        {
            LoadReadingRecordsCalls++;
            LastRecordSort = sort;
            if (ErrorToReturn != null)
            {
                onError(ErrorToReturn);
                return;
            }

            onSuccess(new ReadingRecordListPage(new List<ReadingRecordItem>(), null));
        }
    }
}
