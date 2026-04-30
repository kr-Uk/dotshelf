using System;

namespace App.Application.Dashboard
{
    public interface IFollowRepository
    {
        void LoadFollowers(
            long userId,
            string cursor,
            int limit,
            Action<UserListPage> onSuccess,
            Action<DashboardApiError> onError);

        void LoadFollowings(
            long userId,
            string cursor,
            int limit,
            Action<UserListPage> onSuccess,
            Action<DashboardApiError> onError);

        void Follow(
            long targetUserId,
            Action<FollowToggleResult> onSuccess,
            Action<DashboardApiError> onError);

        void Unfollow(
            long targetUserId,
            Action<FollowToggleResult> onSuccess,
            Action<DashboardApiError> onError);

        void Block(
            long targetUserId,
            Action<BlockToggleResult> onSuccess,
            Action<DashboardApiError> onError);
    }
}
