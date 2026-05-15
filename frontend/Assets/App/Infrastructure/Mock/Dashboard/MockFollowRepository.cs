using System;
using System.Collections.Generic;
using App.Application.Dashboard;
using App.Domain.Dashboard;

namespace App.Infrastructure.Mock.Dashboard
{
    public sealed class MockFollowRepository : IFollowRepository
    {
        private const int PageSize = 30;

        private readonly Dictionary<long, bool> followingMap = new Dictionary<long, bool>();
        private readonly Dictionary<long, bool> blockedMap = new Dictionary<long, bool>();
        private readonly List<UserSummary> followers;
        private readonly List<UserSummary> followings;

        public MockFollowRepository()
        {
            followers = BuildUsers(prefix: "팔로워", count: 18, mutualEvery: 4);
            followings = BuildUsers(prefix: "팔로잉", count: 24, mutualEvery: 3);
            for (int i = 0; i < followings.Count; i++)
            {
                followingMap[followings[i].Id] = true;
            }
        }

        public void LoadFollowers(long userId, string cursor, int limit, Action<UserListPage> onSuccess, Action<DashboardApiError> onError)
        {
            onSuccess(SlicePage(followers, cursor, limit > 0 ? limit : PageSize));
        }

        public void LoadFollowings(long userId, string cursor, int limit, Action<UserListPage> onSuccess, Action<DashboardApiError> onError)
        {
            onSuccess(SlicePage(followings, cursor, limit > 0 ? limit : PageSize));
        }

        public void Follow(long targetUserId, Action<FollowToggleResult> onSuccess, Action<DashboardApiError> onError)
        {
            followingMap[targetUserId] = true;
            bool mutual = followers.Exists(u => u.Id == targetUserId);
            onSuccess(new FollowToggleResult { Following = true, Mutual = mutual });
        }

        public void Unfollow(long targetUserId, Action<FollowToggleResult> onSuccess, Action<DashboardApiError> onError)
        {
            followingMap.Remove(targetUserId);
            onSuccess(new FollowToggleResult { Following = false, Mutual = false });
        }

        public void Block(long targetUserId, Action<BlockToggleResult> onSuccess, Action<DashboardApiError> onError)
        {
            blockedMap[targetUserId] = true;
            followingMap.Remove(targetUserId);
            followers.RemoveAll(u => u.Id == targetUserId);
            onSuccess(new BlockToggleResult { Blocked = true });
        }

        private UserListPage SlicePage(List<UserSummary> source, string cursor, int limit)
        {
            int startIndex = ParseCursor(cursor);
            int take = Math.Min(limit, Math.Max(0, source.Count - startIndex));
            if (take <= 0)
            {
                return new UserListPage(Array.Empty<UserSummary>(), null);
            }

            List<UserSummary> slice = source.GetRange(startIndex, take);
            string nextCursor = startIndex + take < source.Count ? (startIndex + take).ToString() : null;
            return new UserListPage(slice, nextCursor);
        }

        private static List<UserSummary> BuildUsers(string prefix, int count, int mutualEvery)
        {
            List<UserSummary> list = new List<UserSummary>(count);
            for (int i = 1; i <= count; i++)
            {
                bool mutual = i % mutualEvery == 0;
                list.Add(new UserSummary(
                    id: 1000 + i,
                    nickname: $"{prefix}{i}",
                    profileImageUrl: null,
                    mutual: mutual,
                    following: mutual,
                    blocked: false,
                    spaceId: 1000 + i));
            }
            return list;
        }

        private static int ParseCursor(string cursor)
        {
            if (string.IsNullOrEmpty(cursor))
            {
                return 0;
            }

            return int.TryParse(cursor, out int parsed) ? parsed : 0;
        }
    }
}
