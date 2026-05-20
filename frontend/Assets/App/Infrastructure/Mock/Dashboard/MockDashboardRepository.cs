using System;
using App.Application.Dashboard;
using App.Domain.Dashboard;

namespace App.Infrastructure.Mock.Dashboard
{
    public sealed class MockDashboardRepository : IDashboardRepository
    {
        private readonly object stateLock = new object();
        private bool myLiked;
        private long myLikeCount = 12;
        private bool otherLiked;
        private long otherLikeCount = 87;

        public void LoadDashboard(long spaceId, Action<DashboardSnapshot> onSuccess, Action<DashboardApiError> onError)
        {
            onSuccess(BuildSnapshot(spaceId, isMine: false));
        }

        public void LoadMyDashboard(Action<DashboardSnapshot> onSuccess, Action<DashboardApiError> onError)
        {
            onSuccess(BuildSnapshot(spaceId: 1, isMine: true));
        }

        public void ToggleSpaceLike(long spaceId, bool liked, Action<LikeToggleResult> onSuccess, Action<DashboardApiError> onError)
        {
            lock (stateLock)
            {
                if (spaceId == 1)
                {
                    if (liked != myLiked)
                    {
                        myLikeCount += liked ? 1 : -1;
                        myLiked = liked;
                    }
                    onSuccess(new LikeToggleResult { Liked = myLiked, LikeCount = myLikeCount });
                    return;
                }

                if (liked != otherLiked)
                {
                    otherLikeCount += liked ? 1 : -1;
                    otherLiked = liked;
                }
                onSuccess(new LikeToggleResult { Liked = otherLiked, LikeCount = otherLikeCount });
            }
        }

        private DashboardSnapshot BuildSnapshot(long spaceId, bool isMine)
        {
            bool liked;
            long likeCount;
            lock (stateLock)
            {
                liked = isMine ? myLiked : otherLiked;
                likeCount = isMine ? myLikeCount : otherLikeCount;
            }

            Space space = new Space(
                id: spaceId,
                ownerUserId: isMine ? 1 : 2,
                visibility: VisibilityScope.Public,
                guestbookVisibility: VisibilityScope.Public,
                visitCount: isMine ? 254 : 1023,
                likeCount: likeCount,
                liked: liked,
                layoutVersion: 3,
                currentThemeInventoryId: MockSpaceCustomizationState.CurrentThemeInventoryId,
                currentWallpaperInventoryId: MockSpaceCustomizationState.CurrentWallpaperInventoryId,
                currentFloorInventoryId: MockSpaceCustomizationState.CurrentFloorInventoryId,
                currentDeskInventoryId: MockSpaceCustomizationState.CurrentDeskInventoryId,
                currentDashboardInventoryId: MockSpaceCustomizationState.CurrentDashboardInventoryId,
                currentNotebookInventoryId: MockSpaceCustomizationState.CurrentNotebookInventoryId,
                currentInteriorLightInventoryId: MockSpaceCustomizationState.CurrentInteriorLightInventoryId,
                currentExteriorLightInventoryId: MockSpaceCustomizationState.CurrentExteriorLightInventoryId);

            return new DashboardSnapshot(
                space: space,
                ownerNickname: isMine ? "내 닉네임" : "책읽는고양이",
                ownerProfileImageUrl: null,
                ownerIntroduction: isMine ? "오늘도 한 페이지" : "장르 가리지 않는 잡식 독자입니다.",
                followerCount: isMine ? 18 : 342,
                followingCount: isMine ? 24 : 187,
                bookshelfCount: 4,
                bookCount: isMine ? 47 : 189,
                readingRecordCount: isMine ? 12 : 73,
                weeklyVisitorCount: isMine ? 31 : 156);
        }
    }
}
