using System;
using App.Application.Dashboard;

namespace App.Infrastructure.Api.Dashboard
{
    public sealed class ApiFollowRepository : IFollowRepository
    {
        private readonly NotebookApiClient client;

        public ApiFollowRepository(NotebookApiClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public void LoadFollowers(long userId, string cursor, int limit, Action<UserListPage> onSuccess, Action<DashboardApiError> onError)
        {
            client.GetUserFollowers(
                userId,
                cursor,
                limit,
                onSuccess: dto => onSuccess?.Invoke(DashboardDtoMapper.MapUserListPage(dto)),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        public void LoadFollowings(long userId, string cursor, int limit, Action<UserListPage> onSuccess, Action<DashboardApiError> onError)
        {
            client.GetUserFollowings(
                userId,
                cursor,
                limit,
                onSuccess: dto => onSuccess?.Invoke(DashboardDtoMapper.MapUserListPage(dto)),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        public void Follow(long targetUserId, Action<FollowToggleResult> onSuccess, Action<DashboardApiError> onError)
        {
            client.FollowUser(
                targetUserId,
                onSuccess: dto => onSuccess?.Invoke(new FollowToggleResult { Following = dto != null && dto.Active, Mutual = false }),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        public void Unfollow(long targetUserId, Action<FollowToggleResult> onSuccess, Action<DashboardApiError> onError)
        {
            client.UnfollowUser(
                targetUserId,
                onSuccess: dto => onSuccess?.Invoke(new FollowToggleResult { Following = dto != null && dto.Active, Mutual = false }),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        public void Block(long targetUserId, Action<BlockToggleResult> onSuccess, Action<DashboardApiError> onError)
        {
            client.BlockUser(
                targetUserId,
                onSuccess: dto => onSuccess?.Invoke(new BlockToggleResult { Blocked = dto != null && dto.Active }),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }
    }
}
