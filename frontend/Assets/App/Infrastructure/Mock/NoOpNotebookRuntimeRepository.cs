using System;
using System.Collections.Generic;
using System.Linq;
using App.Application.Notebook;

namespace App.Infrastructure.Mock
{
    public sealed class NoOpNotebookRuntimeRepository : INotebookRuntimeRepository
    {
        private const string DisabledCode = "NOTEBOOK_API_DISABLED";
        private const string DisabledMessage = "Notebook runtime API repository is disabled.";
        // Demo seed for LocalMock: user initially follows 201/204 and liked spaces 3001/3003.
        private static readonly long[] DemoSeedFollowedUserIds = { 201L, 204L };
        private static readonly long[] DemoSeedLikedSpaceIds = { 3001L, 3003L };
        private readonly HashSet<long> followedUserIds = new HashSet<long>(DemoSeedFollowedUserIds);
        private readonly HashSet<long> likedSpaceIds = new HashSet<long>(DemoSeedLikedSpaceIds);
        private readonly List<NotebookSpaceItem> spaces;
        private readonly Dictionary<long, List<NotebookUserItem>> followersByUser = new Dictionary<long, List<NotebookUserItem>>();
        private readonly Dictionary<long, List<NotebookUserItem>> followingsByUser = new Dictionary<long, List<NotebookUserItem>>();

        public NoOpNotebookRuntimeRepository()
        {
            spaces = BuildSpaces();
            foreach (var space in spaces)
            {
                followersByUser[space.OwnerUserId] = BuildUsers(space.OwnerUserId, "팔로워", 18, 4);
                followingsByUser[space.OwnerUserId] = BuildUsers(space.OwnerUserId, "팔로잉", 15, 3);
            }
        }

        public void LoadWallet(Action<NotebookWalletResult> onSuccess, Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        public void ChargePages(int pagesAmount, Action<NotebookWalletResult> onSuccess, Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        public void LoadPurchaseHistory(Action<NotebookPurchaseHistoryResult> onSuccess, Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        public void LoadPaymentMethods(Action<NotebookPaymentMethodResult> onSuccess, Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        public void LoadAssets(string category, string sort, int limit, Action<NotebookAssetListResult> onSuccess, Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        public void WishAsset(long assetId, bool wished, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        public void PurchaseAsset(long assetId, Action<NotebookAssetPurchaseResult> onSuccess, Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        public void LoadSpaces(int limit, Action<NotebookSpaceListResult> onSuccess, Action<NotebookApiError> onError)
        {
            LoadSpaces(null, null, limit, onSuccess, onError);
        }

        public void LoadSpaces(string keyword, string cursor, int limit, Action<NotebookSpaceListResult> onSuccess, Action<NotebookApiError> onError)
        {
            var normalized = string.IsNullOrWhiteSpace(keyword) ? string.Empty : keyword.Trim();
            var filtered = spaces
                .Where(space => string.IsNullOrEmpty(normalized) ||
                                Contains(space.SpaceName, normalized) ||
                                Contains(space.OwnerName, normalized) ||
                                Contains(space.Description, normalized))
                .OrderByDescending(space => space.LikeCount)
                .ThenByDescending(space => space.VisitCount)
                .Take(Math.Max(1, limit))
                .Select(CloneSpace)
                .ToArray();

            foreach (var space in filtered)
            {
                space.Following = followedUserIds.Contains(space.OwnerUserId);
                space.Liked = likedSpaceIds.Contains(space.Id);
            }

            onSuccess?.Invoke(new NotebookSpaceListResult(filtered, null));
        }

        public void LoadDefaultShelfTarget(Action<NotebookShelfTarget> onSuccess, Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        public void LikeSpace(long spaceId, bool liked, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError)
        {
            if (liked)
            {
                likedSpaceIds.Add(spaceId);
            }
            else
            {
                likedSpaceIds.Remove(spaceId);
            }

            var space = spaces.FirstOrDefault(item => item.Id == spaceId);
            var count = space?.LikeCount ?? 0L;
            onSuccess?.Invoke(new NotebookToggleResult { Active = liked, Count = count });
        }

        public void FollowUser(long userId, bool following, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError)
        {
            if (following)
            {
                followedUserIds.Add(userId);
            }
            else
            {
                followedUserIds.Remove(userId);
            }

            var count = followersByUser.TryGetValue(userId, out var followers) ? followers.Count : 0;
            onSuccess?.Invoke(new NotebookToggleResult { Active = following, Count = count });
        }

        public void LoadUserFollowers(long userId, string cursor, int limit, Action<NotebookUserListResult> onSuccess, Action<NotebookApiError> onError)
        {
            onSuccess?.Invoke(SliceUsers(GetUsers(followersByUser, userId, "팔로워", 18, 4), cursor, limit));
        }

        public void LoadUserFollowings(long userId, string cursor, int limit, Action<NotebookUserListResult> onSuccess, Action<NotebookApiError> onError)
        {
            onSuccess?.Invoke(SliceUsers(GetUsers(followingsByUser, userId, "팔로잉", 15, 3), cursor, limit));
        }

        public void LoadBookReviews(long bookId, Action<NotebookReviewListResult> onSuccess, Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        public void CreateBookReview(long bookId, float rating, string content, Action<NotebookReviewItem> onSuccess, Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        public void SetUserBookFavorite(long userBookId, bool favorite, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        private static NotebookApiError CreateDisabledError()
        {
            return new NotebookApiError
            {
                Code = DisabledCode,
                Message = DisabledMessage,
                Error = DisabledMessage
            };
        }

        private static List<NotebookSpaceItem> BuildSpaces()
        {
            return new List<NotebookSpaceItem>
            {
                new NotebookSpaceItem { Id = 3001L, OwnerUserId = 201L, OwnerName = "하린", SpaceName = "햇살이 머무는 책방", Mood = "따뜻한 서재", Description = "노란 조명과 식물 사이에 책장을 배치한 공간입니다.", FollowerCount = 1240L, LikeCount = 842L, VisitCount = 5820L, IsOwnSpace = false },
                new NotebookSpaceItem { Id = 3002L, OwnerUserId = 202L, OwnerName = "도윤", SpaceName = "밤의 기록실", Mood = "집중형 책상", Description = "검은 원목 책상과 낮은 조도로 긴 독서 시간을 버틸 수 있게 꾸몄습니다.", FollowerCount = 980L, LikeCount = 790L, VisitCount = 4310L, IsOwnSpace = false },
                new NotebookSpaceItem { Id = 3003L, OwnerUserId = 203L, OwnerName = "서아", SpaceName = "창가의 북라운지", Mood = "밝은 휴식", Description = "큰 창과 낮은 소파를 중심으로 책과 음악을 함께 즐기는 방입니다.", FollowerCount = 1534L, LikeCount = 1020L, VisitCount = 7244L, IsOwnSpace = false },
                new NotebookSpaceItem { Id = 3004L, OwnerUserId = 204L, OwnerName = "민준", SpaceName = "작업과 독서 사이", Mood = "하이브리드", Description = "독서 기록과 작업 노트를 한 화면에서 정리할 수 있는 실용적인 공간입니다.", FollowerCount = 721L, LikeCount = 568L, VisitCount = 3388L, IsOwnSpace = false },
                new NotebookSpaceItem { Id = 3005L, OwnerUserId = 205L, OwnerName = "유나", SpaceName = "초록 선반의 방", Mood = "식물과 책", Description = "초록색 벽지와 낮은 책장으로 차분하게 시선을 정리했습니다.", FollowerCount = 639L, LikeCount = 410L, VisitCount = 2910L, IsOwnSpace = false },
                new NotebookSpaceItem { Id = 3006L, OwnerUserId = 1L, OwnerName = "나", SpaceName = "나의 공간", Mood = "MY SPACE", Description = "LocalMock에서 본인 공간으로 표시되는 샘플입니다.", FollowerCount = 32L, LikeCount = 12L, VisitCount = 160L, IsOwnSpace = true }
            };
        }

        private static NotebookSpaceItem CloneSpace(NotebookSpaceItem source)
        {
            return new NotebookSpaceItem
            {
                Id = source.Id,
                OwnerUserId = source.OwnerUserId,
                OwnerName = source.OwnerName,
                OwnerProfileImageUrl = source.OwnerProfileImageUrl,
                SpaceName = source.SpaceName,
                Mood = source.Mood,
                Description = source.Description,
                ThumbnailUrl = source.ThumbnailUrl,
                FollowerCount = source.FollowerCount,
                LikeCount = source.LikeCount,
                VisitCount = source.VisitCount,
                Following = source.Following,
                Liked = source.Liked,
                IsOwnSpace = source.IsOwnSpace
            };
        }

        private List<NotebookUserItem> GetUsers(Dictionary<long, List<NotebookUserItem>> source, long userId, string prefix, int count, int mutualEvery)
        {
            if (!source.TryGetValue(userId, out var users))
            {
                users = BuildUsers(userId, prefix, count, mutualEvery);
                source[userId] = users;
            }

            return users;
        }

        private NotebookUserListResult SliceUsers(List<NotebookUserItem> source, string cursor, int limit)
        {
            var start = ParseCursor(cursor);
            var take = Math.Min(Math.Max(1, limit), Math.Max(0, source.Count - start));
            if (take <= 0)
            {
                return new NotebookUserListResult(Array.Empty<NotebookUserItem>(), null);
            }

            var page = source
                .Skip(start)
                .Take(take)
                .Select(user => new NotebookUserItem
                {
                    Id = user.Id,
                    Nickname = user.Nickname,
                    ProfileImageUrl = user.ProfileImageUrl,
                    Mutual = user.Mutual,
                    Following = followedUserIds.Contains(user.Id) || user.Following,
                    Blocked = user.Blocked
                })
                .ToArray();

            var nextCursor = start + take < source.Count ? (start + take).ToString() : null;
            return new NotebookUserListResult(page, nextCursor);
        }

        private static List<NotebookUserItem> BuildUsers(long ownerId, string prefix, int count, int mutualEvery)
        {
            var users = new List<NotebookUserItem>(count);
            for (var i = 1; i <= count; i++)
            {
                var id = ownerId * 100 + i;
                var mutual = i % mutualEvery == 0;
                users.Add(new NotebookUserItem
                {
                    Id = id,
                    Nickname = $"{prefix}{i}",
                    Mutual = mutual,
                    Following = mutual,
                    Blocked = false
                });
            }

            return users;
        }

        private static bool Contains(string source, string value)
        {
            return !string.IsNullOrWhiteSpace(source) &&
                   !string.IsNullOrWhiteSpace(value) &&
                   source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int ParseCursor(string cursor)
        {
            if (string.IsNullOrWhiteSpace(cursor))
            {
                return 0;
            }

            return int.TryParse(cursor, out var parsed) ? Math.Max(0, parsed) : 0;
        }
    }
}
