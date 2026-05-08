using System;
using System.Collections.Generic;
using System.Linq;
using App.Application.Notebook;

namespace App.Infrastructure.Mock
{
    public sealed class NoOpNotebookRuntimeRepository : INotebookRuntimeRepository
    {
        // Demo seed for LocalMock: user initially follows 201/204 and liked spaces 3001/3003.
        private static readonly long[] DemoSeedFollowedUserIds = { 201L, 204L };
        private static readonly long[] DemoSeedLikedSpaceIds = { 3001L, 3003L };
        private readonly HashSet<long> followedUserIds = new HashSet<long>(DemoSeedFollowedUserIds);
        private readonly HashSet<long> likedSpaceIds = new HashSet<long>(DemoSeedLikedSpaceIds);
        private readonly HashSet<long> wishedAssetIds = new HashSet<long> { 7002L };
        private readonly HashSet<long> ownedAssetIds = new HashSet<long> { 7001L };
        private readonly List<NotebookSpaceItem> spaces;
        private readonly List<NotebookAssetItem> assets;
        private readonly Dictionary<long, List<NotebookReviewItem>> reviewsByBook = new Dictionary<long, List<NotebookReviewItem>>();
        private readonly Dictionary<long, List<NotebookUserItem>> followersByUser = new Dictionary<long, List<NotebookUserItem>>();
        private readonly Dictionary<long, List<NotebookUserItem>> followingsByUser = new Dictionary<long, List<NotebookUserItem>>();
        private int walletBalancePages = 4820;
        private long nextReviewId = 50000L;

        public NoOpNotebookRuntimeRepository()
        {
            spaces = BuildSpaces();
            assets = BuildAssets();
            foreach (var space in spaces)
            {
                followersByUser[space.OwnerUserId] = BuildUsers(space.OwnerUserId, "팔로워", 18, 4);
                followingsByUser[space.OwnerUserId] = BuildUsers(space.OwnerUserId, "팔로잉", 15, 3);
            }
        }

        public void LoadWallet(Action<NotebookWalletResult> onSuccess, Action<NotebookApiError> onError)
        {
            onSuccess?.Invoke(new NotebookWalletResult { BalancePages = walletBalancePages });
        }

        public void ChargePages(int pagesAmount, Action<NotebookWalletResult> onSuccess, Action<NotebookApiError> onError)
        {
            walletBalancePages += Math.Max(1, pagesAmount);
            onSuccess?.Invoke(new NotebookWalletResult { BalancePages = walletBalancePages });
        }

        public void LoadPurchaseHistory(Action<NotebookPurchaseHistoryResult> onSuccess, Action<NotebookApiError> onError)
        {
            var purchases = ownedAssetIds
                .Select((assetId, index) =>
                {
                    var asset = assets.FirstOrDefault(item => item.Id == assetId);
                    return new NotebookPurchaseHistoryItem
                    {
                        Id = 8000L + index,
                        AssetId = assetId,
                        PricePages = asset?.PricePages ?? 0,
                        Status = "PAID"
                    };
                })
                .ToArray();
            onSuccess?.Invoke(new NotebookPurchaseHistoryResult(purchases));
        }

        public void LoadPaymentMethods(Action<NotebookPaymentMethodResult> onSuccess, Action<NotebookApiError> onError)
        {
            onSuccess?.Invoke(new NotebookPaymentMethodResult(new[] { "LocalMock Pages", "Test Card" }));
        }

        public void LoadAssets(string category, string sort, int limit, Action<NotebookAssetListResult> onSuccess, Action<NotebookApiError> onError)
        {
            var normalizedCategory = string.IsNullOrWhiteSpace(category) ? string.Empty : category.Trim();
            var normalizedSort = string.IsNullOrWhiteSpace(sort) ? "popular" : sort.Trim().ToLowerInvariant();
            IEnumerable<NotebookAssetItem> source = assets;
            if (!string.IsNullOrEmpty(normalizedCategory) && !string.Equals(normalizedCategory, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                source = source.Where(asset =>
                    string.Equals(asset.Category, normalizedCategory, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(asset.CategoryCode, normalizedCategory, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(asset.AssetType, normalizedCategory, StringComparison.OrdinalIgnoreCase));
            }

            source = normalizedSort == "price"
                ? source.OrderBy(asset => asset.PricePages).ThenBy(asset => asset.Name)
                : normalizedSort == "latest" || normalizedSort == "recent"
                    ? source.OrderByDescending(asset => asset.Id)
                    : source.OrderByDescending(asset => asset.Popularity).ThenBy(asset => asset.Name);

            var result = source
                .Take(Math.Max(1, limit))
                .Select(CloneAsset)
                .ToArray();

            onSuccess?.Invoke(new NotebookAssetListResult(result));
        }

        public void WishAsset(long assetId, bool wished, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError)
        {
            if (wished)
            {
                wishedAssetIds.Add(assetId);
            }
            else
            {
                wishedAssetIds.Remove(assetId);
            }

            onSuccess?.Invoke(new NotebookToggleResult { Active = wished, Count = wishedAssetIds.Count });
        }

        public void PurchaseAsset(long assetId, Action<NotebookAssetPurchaseResult> onSuccess, Action<NotebookApiError> onError)
        {
            var asset = assets.FirstOrDefault(item => item.Id == assetId);
            if (asset == null)
            {
                onError?.Invoke(new NotebookApiError { Code = "NOTEBOOK_ASSET_NOT_FOUND", Message = "Mock asset was not found." });
                return;
            }

            if (!ownedAssetIds.Contains(assetId))
            {
                walletBalancePages = Math.Max(0, walletBalancePages - Math.Max(0, asset.PricePages));
                ownedAssetIds.Add(assetId);
            }

            onSuccess?.Invoke(new NotebookAssetPurchaseResult
            {
                AssetId = assetId,
                InventoryId = assetId + 10000L,
                PricePages = asset.PricePages
            });
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
                                Contains(space.OwnerName, normalized))
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

        public void UploadMySpaceThumbnail(byte[] imageBytes, string contentType, Action<NotebookSpaceThumbnailUploadResult> onSuccess, Action<NotebookApiError> onError)
        {
            onSuccess?.Invoke(new NotebookSpaceThumbnailUploadResult
            {
                SpaceId = 3004L,
                ThumbnailKey = "spaces/3004/thumbnail/mock.jpg",
                ThumbnailUrl = string.Empty,
                LayoutVersion = 1L
            });
        }

        public void LoadDefaultShelfTarget(Action<NotebookShelfTarget> onSuccess, Action<NotebookApiError> onError)
        {
            onSuccess?.Invoke(new NotebookShelfTarget
            {
                BookshelfId = 1L,
                SectionId = 1L,
                SlotNo = 1
            });
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
            onSuccess?.Invoke(new NotebookReviewListResult(GetBookReviews(bookId).ToArray()));
        }

        public void CreateBookReview(long bookId, float rating, string content, Action<NotebookReviewItem> onSuccess, Action<NotebookApiError> onError)
        {
            var review = new NotebookReviewItem
            {
                Id = ++nextReviewId,
                Author = "LocalMock",
                Rating = Math.Max(0.5f, Math.Min(5f, rating)),
                Content = string.IsNullOrWhiteSpace(content) ? "리뷰 내용 없음" : content.Trim()
            };
            GetBookReviews(bookId).Insert(0, review);
            onSuccess?.Invoke(review);
        }

        public void SetUserBookFavorite(long userBookId, bool favorite, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError)
        {
            onSuccess?.Invoke(new NotebookToggleResult { Active = favorite, Count = favorite ? 1 : 0 });
        }

        private NotebookAssetItem CloneAsset(NotebookAssetItem source)
        {
            return new NotebookAssetItem
            {
                Id = source.Id,
                Code = source.Code,
                Name = source.Name,
                Category = source.Category,
                CategoryCode = source.CategoryCode,
                Tier = source.Tier,
                AssetType = source.AssetType,
                PreviewImageUrl = source.PreviewImageUrl,
                PricePages = source.PricePages,
                Popularity = source.Popularity,
                Owned = ownedAssetIds.Contains(source.Id) || source.Owned,
                Wished = wishedAssetIds.Contains(source.Id) || source.Wished
            };
        }

        private List<NotebookReviewItem> GetBookReviews(long bookId)
        {
            if (!reviewsByBook.TryGetValue(bookId, out var reviews))
            {
                reviews = new List<NotebookReviewItem>
                {
                    new NotebookReviewItem { Id = bookId * 10 + 1, Author = "민서", Rating = 4.8f, Content = "책장에 올려두고 다시 펼치고 싶은 문장이 많았어요." },
                    new NotebookReviewItem { Id = bookId * 10 + 2, Author = "하린", Rating = 4.6f, Content = "분위기가 차분해서 노트북 화면에서도 잘 어울립니다." },
                    new NotebookReviewItem { Id = bookId * 10 + 3, Author = "지우", Rating = 4.4f, Content = "짧게 읽기 좋고 리뷰 작성 흐름 확인용으로도 충분합니다." }
                };
                reviewsByBook[bookId] = reviews;
            }

            return reviews;
        }

        private static List<NotebookAssetItem> BuildAssets()
        {
            return new List<NotebookAssetItem>
            {
                Asset(7001L, "warm-wood-shelf", "따뜻한 원목 책장", "BOOKSHELF", "BOOKSHELF", "PREMIUM", "BOOKSHELF", 0, 920, true),
                Asset(7002L, "blue-desk-lamp", "블루 데스크 램프", "INTERIOR", "INTERIOR", "RARE", "LIGHTING", 900, 780, false),
                Asset(7003L, "mint-wallpaper", "민트 벽지", "THEME", "THEME", "BASIC", "WALLPAPER", 600, 720, false),
                Asset(7004L, "reading-chair", "독서 의자", "INTERIOR", "INTERIOR", "PREMIUM", "FURNITURE", 1300, 690, false),
                Asset(7005L, "night-sky-theme", "밤하늘 테마", "THEME", "THEME", "RARE", "THEME", 1500, 640, false),
                Asset(7006L, "small-plant", "책상 위 작은 화분", "INTERIOR", "INTERIOR", "BASIC", "DECOR", 350, 610, false)
            };
        }

        private static NotebookAssetItem Asset(
            long id,
            string code,
            string name,
            string category,
            string categoryCode,
            string tier,
            string assetType,
            int pricePages,
            int popularity,
            bool owned)
        {
            return new NotebookAssetItem
            {
                Id = id,
                Code = code,
                Name = name,
                Category = category,
                CategoryCode = categoryCode,
                Tier = tier,
                AssetType = assetType,
                PricePages = pricePages,
                Popularity = popularity,
                Owned = owned
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
