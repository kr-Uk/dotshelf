using System;
using System.Collections.Generic;
using App.Infrastructure.Api.Dtos;
using UnityEngine;
using UnityEngine.Networking;

namespace App.Infrastructure.Api
{
    public sealed class NotebookApiClient
    {
        private readonly APIManager api;

        public NotebookApiClient(APIManager api)
        {
            this.api = api != null ? api : APIManager.Instance;
        }

        public bool VerboseLogEnabled => api != null && api.EnableVerboseLog;
        public long CurrentUserId => api != null ? api.CurrentUserId : 0L;

        public Coroutine CheckHealth(Action<ApiHealthCheckResult> onComplete, string path = "/health")
        {
            return RequireApi().CheckHealth(onComplete, path);
        }

        public Coroutine SocialLogin(SocialLoginRequestDto request, Action<AuthTokenResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<SocialLoginRequestDto, AuthTokenResponseDto>("/v1/auth/social-login", NormalizeSocialLoginRequest(request), onSuccess, onError);
        }

        public Coroutine RefreshToken(TokenRefreshRequestDto request, Action<AuthTokenResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<TokenRefreshRequestDto, AuthTokenResponseDto>("/v1/auth/token/refresh", request, onSuccess, onError);
        }

        public Coroutine Logout(LogoutRequestDto request, Action<VoidResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<LogoutRequestDto, VoidResponseDto>("/v1/auth/logout", request, onSuccess, onError);
        }

        public Coroutine SearchBooks(string keyword, Action<BookListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return SearchBooks(keyword, 1, 24, onSuccess, onError);
        }

        public Coroutine SearchBooks(string keyword, int page, int size, Action<BookListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            var query = new Dictionary<string, string>
            {
                ["keyword"] = keyword,
                ["page"] = Mathf.Max(1, page).ToString(),
                ["size"] = Mathf.Max(1, size).ToString()
            };
            return Get<BookListResponseDto>(WithQuery("/v1/books/search", query), onSuccess, onError);
        }

        public Coroutine GetBooks(string category, int limit, Action<BookListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            var normalizedCategory = string.IsNullOrWhiteSpace(category)
                ? "RANKING"
                : category.Trim().ToUpperInvariant();
            var validLimit = Mathf.Max(1, limit);

            switch (normalizedCategory)
            {
                case "RECOMMENDED":
                case "RECOMMEND":
                    return Get<BookListResponseDto>(
                        WithQuery("/v1/books/recommended", Query("limit", validLimit.ToString())),
                        onSuccess,
                        onError);
                case "RANKING":
                case "POPULAR":
                    return GetRankingBooks(validLimit, onSuccess, onError);
                default:
                    Debug.LogWarning($"Unknown notebook book category '{category}'. Falling back to RANKING.");
                    return GetRankingBooks(validLimit, onSuccess, onError);
            }
        }

        private Coroutine GetRankingBooks(int limit, Action<BookListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError)
        {
            var query = new Dictionary<string, string>
            {
                ["sort"] = "POPULAR",
                ["limit"] = limit.ToString()
            };
            return Get<BookListResponseDto>(WithQuery("/v1/books/ranking", query), onSuccess, onError);
        }

        public Coroutine GetBook(long bookId, Action<BookResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<BookResponseDto>($"/v1/books/{bookId}", onSuccess, onError);
        }

        public Coroutine GetBookReviews(long bookId, string sort, int limit, Action<BookReviewListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            var query = new Dictionary<string, string>
            {
                ["sort"] = string.IsNullOrWhiteSpace(sort) ? "POPULAR" : sort,
                ["limit"] = Mathf.Max(1, limit).ToString()
            };
            return Get<BookReviewListResponseDto>(WithQuery($"/v1/books/{bookId}/reviews", query), onSuccess, onError);
        }

        public Coroutine CreateBookReview(long bookId, BookReviewRequestDto request, Action<BookReviewResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<BookReviewRequestDto, BookReviewResponseDto>($"/v1/books/{bookId}/reviews", request, onSuccess, onError);
        }

        public Coroutine AddBookToShelf(long bookshelfId, AddBookToShelfRequestDto request, Action<UserBookResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<AddBookToShelfRequestDto, UserBookResponseDto>($"/v1/bookshelves/{bookshelfId}/books", request, onSuccess, onError);
        }

        public Coroutine AddKakaoBookToShelf(long bookshelfId, AddKakaoBookToShelfRequestDto request, Action<UserBookResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<AddKakaoBookToShelfRequestDto, UserBookResponseDto>($"/v1/bookshelves/{bookshelfId}/books/kakao", request, onSuccess, onError);
        }

        public Coroutine UpdateUserBook(long userBookId, UpdateUserBookRequestDto request, Action<UserBookResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Patch<UpdateUserBookRequestDto, UserBookResponseDto>($"/v1/user-books/{userBookId}", request, onSuccess, onError);
        }

        public Coroutine MoveUserBook(long userBookId, MoveUserBookRequestDto request, Action<UserBookResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<MoveUserBookRequestDto, UserBookResponseDto>($"/v1/user-books/{userBookId}/move", request, onSuccess, onError);
        }

        public Coroutine ArchiveUserBook(long userBookId, Action<UserBookResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<UserBookResponseDto>($"/v1/user-books/{userBookId}/archive", onSuccess, onError);
        }

        public Coroutine DeleteUserBook(long userBookId, Action<VoidResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Delete<VoidResponseDto>($"/v1/user-books/{userBookId}", onSuccess, onError);
        }

        public Coroutine RemoveBookshelfItem(long itemId, Action<BookshelfItemResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Delete<BookshelfItemResponseDto>($"/v1/bookshelf-items/{itemId}", onSuccess, onError);
        }

        public Coroutine GetArchivedUserBooks(Action<UserBookListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<UserBookListResponseDto>("/v1/user-books/archived", onSuccess, onError);
        }

        public Coroutine GetUserBooks(
            string storageStatus,
            string readingStatus,
            long? bookshelfId,
            long? sectionId,
            string cursor,
            int limit,
            Action<UserBookPlacementListResponseDto> onSuccess,
            Action<ApiErrorResponseDto> onError = null)
        {
            var query = BuildCursorQuery(cursor, limit);
            if (!string.IsNullOrWhiteSpace(storageStatus))
            {
                query["storageStatus"] = storageStatus;
            }

            if (!string.IsNullOrWhiteSpace(readingStatus))
            {
                query["readingStatus"] = readingStatus;
            }

            if (bookshelfId.HasValue && bookshelfId.Value > 0L)
            {
                query["bookshelfId"] = bookshelfId.Value.ToString();
            }

            if (sectionId.HasValue && sectionId.Value > 0L)
            {
                query["sectionId"] = sectionId.Value.ToString();
            }

            return Get<UserBookPlacementListResponseDto>(WithQuery("/v1/user-books", query), onSuccess, onError);
        }

        public Coroutine LikeUserBook(long userBookId, Action<ToggleResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<ToggleResponseDto>($"/v1/user-books/{userBookId}/like", onSuccess, onError);
        }

        public Coroutine UnlikeUserBook(long userBookId, Action<ToggleResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Delete<ToggleResponseDto>($"/v1/user-books/{userBookId}/like", onSuccess, onError);
        }

        public Coroutine GetAssets(string category, string sort, int limit, Action<AssetListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            var query = new Dictionary<string, string>
            {
                ["category"] = category,
                ["sort"] = string.IsNullOrWhiteSpace(sort) ? "popular" : sort,
                ["limit"] = Mathf.Max(1, limit).ToString()
            };
            return Get<AssetListResponseDto>(WithQuery("/v1/assets", query), onSuccess, onError);
        }

        public Coroutine GetAssetCategories(Action<AssetCategoryListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<AssetCategoryListResponseDto>("/v1/assets/categories", onSuccess, onError);
        }

        public Coroutine GetAsset(long assetId, Action<AssetResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<AssetResponseDto>($"/v1/assets/{assetId}", onSuccess, onError);
        }

        public Coroutine GetAssetInventory(Action<InventoryResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<InventoryResponseDto>("/v1/assets/me/inventory", onSuccess, onError);
        }

        public Coroutine GetAssetDownloadManifest(long assetId, Action<DownloadManifestResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<DownloadManifestResponseDto>($"/v1/assets/{assetId}/download-manifest", onSuccess, onError);
        }

        public Coroutine WishAsset(long assetId, Action<ToggleResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<ToggleResponseDto>($"/v1/assets/{assetId}/wishlist", onSuccess, onError);
        }

        public Coroutine UnwishAsset(long assetId, Action<ToggleResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Delete<ToggleResponseDto>($"/v1/assets/{assetId}/wishlist", onSuccess, onError);
        }

        public Coroutine PurchaseAsset(long assetId, Action<AssetPurchaseResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<AssetPurchaseResponseDto>($"/v1/assets/{assetId}/purchase", onSuccess, onError);
        }

        public Coroutine GetMySpace(Action<SpaceResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<SpaceResponseDto>("/v1/spaces/me", onSuccess, onError);
        }

        public Coroutine GetSpaces(string sort, int limit, Action<SpaceListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return GetSpaces(null, sort, null, limit, onSuccess, onError);
        }

        public Coroutine GetSpaces(string keyword, string sort, string cursor, int limit, Action<SpaceListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            var query = new Dictionary<string, string>
            {
                ["sort"] = string.IsNullOrWhiteSpace(sort) ? "POPULAR" : sort,
                ["limit"] = Mathf.Max(1, limit).ToString()
            };

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query["keyword"] = keyword.Trim();
            }

            // /v1/spaces currently supports keyword/sort/limit only.
            // Keep cursor in the public method signature for Application-layer
            // compatibility, but do not send it until the backend returns a real
            // nextCursor for this endpoint.

            return Get<SpaceListResponseDto>(WithQuery("/v1/spaces", query), onSuccess, onError);
        }

        public Coroutine GetSpace(long spaceId, Action<SpaceResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<SpaceResponseDto>($"/v1/spaces/{spaceId}", onSuccess, onError);
        }

        public Coroutine GetSpaceAssetManifests(long spaceId, Action<SpaceAssetManifestListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<SpaceAssetManifestListResponseDto>($"/v1/spaces/{spaceId}/asset-manifests", onSuccess, onError);
        }

        public Coroutine GetSpaceDashboard(long spaceId, string period, Action<DashboardResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<DashboardResponseDto>(WithQuery($"/v1/spaces/{spaceId}/dashboard", Query("period", string.IsNullOrWhiteSpace(period) ? "daily" : period)), onSuccess, onError);
        }

        public Coroutine RecordSpaceVisit(long spaceId, Action<ToggleResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<ToggleResponseDto>($"/v1/spaces/{spaceId}/visits", onSuccess, onError);
        }

        public Coroutine LikeSpace(long spaceId, Action<ToggleResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<ToggleResponseDto>($"/v1/spaces/{spaceId}/like", onSuccess, onError);
        }

        public Coroutine UnlikeSpace(long spaceId, Action<ToggleResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Delete<ToggleResponseDto>($"/v1/spaces/{spaceId}/like", onSuccess, onError);
        }

        public Coroutine SaveMySpaceLayout(LayoutSaveRequestDto request, Action<LayoutSaveResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Put<LayoutSaveRequestDto, LayoutSaveResponseDto>("/v1/spaces/me/layout", request, onSuccess, onError);
        }

        public Coroutine GetSpaceLayout(long spaceId, Action<LayoutResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<LayoutResponseDto>($"/v1/spaces/{spaceId}/layout", onSuccess, onError);
        }

        public Coroutine CreateMySpaceThumbnailUploadUrl(SpaceThumbnailUploadUrlRequestDto request, Action<SpaceThumbnailUploadUrlResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<SpaceThumbnailUploadUrlRequestDto, SpaceThumbnailUploadUrlResponseDto>("/v1/spaces/me/thumbnail-upload-url", request, onSuccess, onError);
        }

        public Coroutine UploadRawObject(string uploadUrl, byte[] body, string contentType, Action onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return RequireApi().PutRaw(uploadUrl, body, contentType, onSuccess, onError);
        }

        public Coroutine ConfirmMySpaceThumbnail(SpaceThumbnailConfirmRequestDto request, Action<SpaceThumbnailUploadResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Patch<SpaceThumbnailConfirmRequestDto, SpaceThumbnailUploadResponseDto>("/v1/spaces/me/thumbnail", request, onSuccess, onError);
        }

        public Coroutine ApplyMySpaceTheme(ThemeApplyRequestDto request, Action<SpaceResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Patch<ThemeApplyRequestDto, SpaceResponseDto>("/v1/spaces/me/theme", request, onSuccess, onError);
        }

        // 카테고리별 개별 적용 6 endpoint — backend SpaceController의 PATCH /me/{wallpaper,floor,...}
        // body: {inventoryId: long?} (null이면 unset). response: SpaceMutationResponseDto.
        public Coroutine ChangeMyWallpaper(CategoryInventoryRequestDto request, Action<SpaceMutationResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Patch<CategoryInventoryRequestDto, SpaceMutationResponseDto>("/v1/spaces/me/wallpaper", request, onSuccess, onError);
        }

        public Coroutine ChangeMyFloor(CategoryInventoryRequestDto request, Action<SpaceMutationResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Patch<CategoryInventoryRequestDto, SpaceMutationResponseDto>("/v1/spaces/me/floor", request, onSuccess, onError);
        }

        public Coroutine ChangeMyDesk(CategoryInventoryRequestDto request, Action<SpaceMutationResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Patch<CategoryInventoryRequestDto, SpaceMutationResponseDto>("/v1/spaces/me/desk", request, onSuccess, onError);
        }

        public Coroutine ChangeMyDashboard(CategoryInventoryRequestDto request, Action<SpaceMutationResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Patch<CategoryInventoryRequestDto, SpaceMutationResponseDto>("/v1/spaces/me/dashboard", request, onSuccess, onError);
        }

        public Coroutine ChangeMyInteriorLight(CategoryInventoryRequestDto request, Action<SpaceMutationResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Patch<CategoryInventoryRequestDto, SpaceMutationResponseDto>("/v1/spaces/me/interior-light", request, onSuccess, onError);
        }

        public Coroutine ChangeMyExteriorLight(CategoryInventoryRequestDto request, Action<SpaceMutationResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Patch<CategoryInventoryRequestDto, SpaceMutationResponseDto>("/v1/spaces/me/exterior-light", request, onSuccess, onError);
        }

        // 전체 테마 적용(모드 명시) — backend POST /me/theme/{inventoryId}/apply?mode=RESET|KEEP_BLOCKERS_ONLY
        // mode 값은 UTF-8 string("RESET" / "KEEP_BLOCKERS_ONLY"). KEEP_BLOCKERS_ONLY는 현재 501 NOT_IMPLEMENTED 반환.
        public Coroutine ApplyMySpaceThemeWithMode(long inventoryId, string mode, Action<SpaceMutationResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            var path = WithQuery($"/v1/spaces/me/theme/{inventoryId}/apply", Query("mode", mode));
            return Post<SpaceMutationResponseDto>(path, onSuccess, onError);
        }

        // 테마 자산이 묶고 있는 카테고리별 자식 자산 ID 매핑 조회.
        public Coroutine GetThemeContents(long assetId, Action<ThemeContentsResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<ThemeContentsResponseDto>($"/v1/assets/{assetId}/contents", onSuccess, onError);
        }

        public Coroutine UpdateMySpaceVisibility(VisibilityRequestDto request, Action<SpaceResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Patch<VisibilityRequestDto, SpaceResponseDto>("/v1/spaces/me/visibility", request, onSuccess, onError);
        }

        public Coroutine UpdateMyGuestbookVisibility(VisibilityRequestDto request, Action<SpaceResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Patch<VisibilityRequestDto, SpaceResponseDto>("/v1/spaces/me/guestbook-visibility", request, onSuccess, onError);
        }

        public Coroutine GetSpaceTemplates(Action<SpaceTemplateListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<SpaceTemplateListResponseDto>("/v1/spaces/templates", onSuccess, onError);
        }

        public Coroutine GetFollowingFeed(Action<FeedEventListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<FeedEventListResponseDto>("/v1/feed/following", onSuccess, onError);
        }

        public Coroutine GetRecommendedRecords(Action<FeedEventListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<FeedEventListResponseDto>("/v1/feed/recommended-records", onSuccess, onError);
        }

        public Coroutine GetRecommendedBooks(Action<FeedEventListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<FeedEventListResponseDto>("/v1/feed/recommended-books", onSuccess, onError);
        }

        public Coroutine GetBookPromotions(Action<BookPromotionListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<BookPromotionListResponseDto>("/v1/feed/book-promotions", onSuccess, onError);
        }

        public Coroutine ChargePages(PageChargeRequestDto request, Action<WalletResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<PageChargeRequestDto, WalletResponseDto>("/v1/payments/page-charge", request, onSuccess, onError);
        }

        public Coroutine GetWallet(Action<WalletResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<WalletResponseDto>("/v1/payments/wallet", onSuccess, onError);
        }

        public Coroutine GetPurchases(Action<PurchaseHistoryListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<PurchaseHistoryListResponseDto>("/v1/payments/purchases", onSuccess, onError);
        }

        public Coroutine GetPaymentMethods(Action<PaymentMethodListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<PaymentMethodListResponseDto>("/v1/payments/methods", onSuccess, onError);
        }

        public Coroutine GetMe(Action<UserProfileResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<UserProfileResponseDto>("/v1/users/me", onSuccess, onError);
        }

        public Coroutine UpdateMe(UpdateProfileRequestDto request, Action<UserProfileResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Patch<UpdateProfileRequestDto, UserProfileResponseDto>("/v1/users/me", request, onSuccess, onError);
        }

        public Coroutine CreateMyProfileImageUploadUrl(ProfileImageUploadUrlRequestDto request, Action<ProfileImageUploadUrlResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<ProfileImageUploadUrlRequestDto, ProfileImageUploadUrlResponseDto>("/v1/users/me/profile-image/upload-url", request, onSuccess, onError);
        }

        public Coroutine ConfirmMyProfileImage(ProfileImageConfirmRequestDto request, Action<UserProfileResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Patch<ProfileImageConfirmRequestDto, UserProfileResponseDto>("/v1/users/me/profile-image", request, onSuccess, onError);
        }

        public Coroutine DeleteMe(Action<VoidResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Delete<VoidResponseDto>("/v1/users/me", onSuccess, onError);
        }

        public Coroutine GetUserProfile(long userId, Action<UserProfileResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<UserProfileResponseDto>($"/v1/users/{userId}", onSuccess, onError);
        }

        public Coroutine CheckNickname(string nickname, Action<NicknameAvailabilityResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<NicknameAvailabilityResponseDto>(WithQuery("/v1/users/nickname/check", Query("nickname", nickname)), onSuccess, onError);
        }

        public Coroutine FollowUser(long targetUserId, Action<ToggleResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<ToggleResponseDto>($"/v1/users/{targetUserId}/follow", onSuccess, onError);
        }

        public Coroutine UnfollowUser(long targetUserId, Action<ToggleResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Delete<ToggleResponseDto>($"/v1/users/{targetUserId}/follow", onSuccess, onError);
        }

        public Coroutine GetFollowers(Action<UserListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<UserListResponseDto>("/v1/users/me/followers", onSuccess, onError);
        }

        public Coroutine GetFollowers(string cursor, int limit, Action<UserListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<UserListResponseDto>(WithQuery("/v1/users/me/followers", BuildCursorQuery(cursor, limit)), onSuccess, onError);
        }

        public Coroutine GetFollowings(Action<UserListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<UserListResponseDto>("/v1/users/me/followings", onSuccess, onError);
        }

        public Coroutine GetFollowings(string cursor, int limit, Action<UserListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<UserListResponseDto>(WithQuery("/v1/users/me/followings", BuildCursorQuery(cursor, limit)), onSuccess, onError);
        }

        public Coroutine GetUserFollowers(long userId, string cursor, int limit, Action<UserListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<UserListResponseDto>(WithQuery($"/v1/users/{userId}/followers", BuildCursorQuery(cursor, limit)), onSuccess, onError);
        }

        public Coroutine GetUserFollowings(long userId, string cursor, int limit, Action<UserListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<UserListResponseDto>(WithQuery($"/v1/users/{userId}/followings", BuildCursorQuery(cursor, limit)), onSuccess, onError);
        }

        public Coroutine BlockUser(long targetUserId, Action<ToggleResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<ToggleResponseDto>($"/v1/users/{targetUserId}/block", onSuccess, onError);
        }

        public Coroutine UnblockUser(long targetUserId, Action<ToggleResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Delete<ToggleResponseDto>($"/v1/users/{targetUserId}/block", onSuccess, onError);
        }

        public Coroutine GetBlocks(Action<UserListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<UserListResponseDto>("/v1/users/me/blocks", onSuccess, onError);
        }

        public Coroutine GetNotificationSettings(Action<NotificationSettingResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<NotificationSettingResponseDto>("/v1/users/me/notification-settings", onSuccess, onError);
        }

        public Coroutine UpdateNotificationSettings(NotificationSettingRequestDto request, Action<NotificationSettingResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Patch<NotificationSettingRequestDto, NotificationSettingResponseDto>("/v1/users/me/notification-settings", request, onSuccess, onError);
        }

        public Coroutine GetFeedSettings(Action<FeedSettingResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<FeedSettingResponseDto>("/v1/users/me/feed-settings", onSuccess, onError);
        }

        public Coroutine UpdateFeedSettings(FeedSettingRequestDto request, Action<FeedSettingResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Patch<FeedSettingRequestDto, FeedSettingResponseDto>("/v1/users/me/feed-settings", request, onSuccess, onError);
        }

        public Coroutine GetGuestbooks(long spaceId, Action<GuestbookListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<GuestbookListResponseDto>($"/v1/spaces/{spaceId}/guestbooks", onSuccess, onError);
        }

        public Coroutine GetGuestbooks(long spaceId, string cursor, int limit, Action<GuestbookListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<GuestbookListResponseDto>(WithQuery($"/v1/spaces/{spaceId}/guestbooks", BuildCursorQuery(cursor, limit)), onSuccess, onError);
        }

        public Coroutine CreateGuestbook(long spaceId, GuestbookRequestDto request, Action<GuestbookResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<GuestbookRequestDto, GuestbookResponseDto>($"/v1/spaces/{spaceId}/guestbooks", request, onSuccess, onError);
        }

        public Coroutine DeleteGuestbook(long guestbookId, Action<VoidResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Delete<VoidResponseDto>($"/v1/guestbooks/{guestbookId}", onSuccess, onError);
        }

        public Coroutine ReplyGuestbook(long guestbookId, GuestbookReplyRequestDto request, Action<GuestbookReplyResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<GuestbookReplyRequestDto, GuestbookReplyResponseDto>($"/v1/guestbooks/{guestbookId}/replies", request, onSuccess, onError);
        }

        public Coroutine DeleteGuestbookReply(long guestbookId, long replyId, Action<VoidResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Delete<VoidResponseDto>($"/v1/guestbooks/{guestbookId}/replies/{replyId}", onSuccess, onError);
        }

        public Coroutine GetGuestbookPostItWhitelist(Action<GuestbookPostItWhitelistResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<GuestbookPostItWhitelistResponseDto>("/v1/spaces/me/guestbook-postit-whitelist", onSuccess, onError);
        }

        public Coroutine UpdateGuestbookPostItWhitelist(GuestbookPostItWhitelistRequestDto request, Action<GuestbookPostItWhitelistResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Put<GuestbookPostItWhitelistRequestDto, GuestbookPostItWhitelistResponseDto>("/v1/spaces/me/guestbook-postit-whitelist", request, onSuccess, onError);
        }

        public Coroutine GetNotifications(Action<NotificationListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<NotificationListResponseDto>("/v1/notifications", onSuccess, onError);
        }

        public Coroutine MarkNotificationRead(long notificationId, Action<NotificationResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Patch<NotificationResponseDto>($"/v1/notifications/{notificationId}/read", onSuccess, onError);
        }

        public Coroutine MarkAllNotificationsRead(Action<VoidResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Patch<VoidResponseDto>("/v1/notifications/read-all", onSuccess, onError);
        }

        public Coroutine ReportContent(ReportRequestDto request, Action<ReportResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<ReportRequestDto, ReportResponseDto>("/v1/reports", request, onSuccess, onError);
        }

        public Coroutine GetMyReports(Action<ReportListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<ReportListResponseDto>("/v1/reports/me", onSuccess, onError);
        }

        public Coroutine GetReadingRecord(long recordId, Action<ReadingRecordResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<ReadingRecordResponseDto>($"/v1/reading-records/{recordId}", onSuccess, onError);
        }

        public Coroutine GetUserBookReadingRecord(long userBookId, Action<ReadingRecordResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<ReadingRecordResponseDto>($"/v1/user-books/{userBookId}/reading-record", onSuccess, onError);
        }

        public Coroutine GetUserReadingRecords(long userId, string sort, string cursor, int limit, Action<ReadingRecordPageResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            var query = BuildCursorQuery(cursor, limit);
            if (!string.IsNullOrWhiteSpace(sort))
            {
                query["sort"] = sort;
            }
            return Get<ReadingRecordPageResponseDto>(WithQuery($"/v1/users/{userId}/reading-records", query), onSuccess, onError);
        }

        public Coroutine GetSpaceOwnedBooks(long spaceId, string sort, string cursor, int limit, Action<OwnedBookListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            var query = BuildCursorQuery(cursor, limit);
            if (!string.IsNullOrWhiteSpace(sort))
            {
                query["sort"] = sort;
            }
            return Get<OwnedBookListResponseDto>(WithQuery($"/v1/spaces/{spaceId}/books", query), onSuccess, onError);
        }

        public Coroutine CreateReadingRecord(ReadingRecordRequestDto request, Action<ReadingRecordResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<ReadingRecordRequestDto, ReadingRecordResponseDto>("/v1/reading-records", request, onSuccess, onError);
        }

        public Coroutine UpdateReadingRecord(long recordId, ReadingRecordRequestDto request, Action<ReadingRecordResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Patch<ReadingRecordRequestDto, ReadingRecordResponseDto>($"/v1/reading-records/{recordId}", request, onSuccess, onError);
        }

        public Coroutine DeleteReadingRecord(long recordId, Action<VoidResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Delete<VoidResponseDto>($"/v1/reading-records/{recordId}", onSuccess, onError);
        }

        public Coroutine LikeReadingRecord(long recordId, Action<ToggleResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<ToggleResponseDto>($"/v1/reading-records/{recordId}/like", onSuccess, onError);
        }

        public Coroutine UnlikeReadingRecord(long recordId, Action<ToggleResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Delete<ToggleResponseDto>($"/v1/reading-records/{recordId}/like", onSuccess, onError);
        }

        public Coroutine GetReadingRecordComments(long recordId, Action<CommentListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<CommentListResponseDto>($"/v1/reading-records/{recordId}/comments", onSuccess, onError);
        }

        public Coroutine CreateReadingRecordComment(long recordId, CommentRequestDto request, Action<CommentResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<CommentRequestDto, CommentResponseDto>($"/v1/reading-records/{recordId}/comments", request, onSuccess, onError);
        }

        public Coroutine DeleteComment(long commentId, Action<VoidResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Delete<VoidResponseDto>($"/v1/comments/{commentId}", onSuccess, onError);
        }

        public Coroutine LikeComment(long commentId, Action<ToggleResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<ToggleResponseDto>($"/v1/comments/{commentId}/like", onSuccess, onError);
        }

        public Coroutine UnlikeComment(long commentId, Action<ToggleResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Delete<ToggleResponseDto>($"/v1/comments/{commentId}/like", onSuccess, onError);
        }

        public Coroutine GetBookshelves(long spaceId, Action<BookshelfListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<BookshelfListResponseDto>($"/v1/spaces/{spaceId}/bookshelves", onSuccess, onError);
        }

        public Coroutine GetBookshelfItems(long bookshelfId, Action<BookshelfRenderResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<BookshelfRenderResponseDto>($"/v1/bookshelves/{bookshelfId}/items", onSuccess, onError);
        }

        public Coroutine CreateBookshelf(CreateBookshelfRequestDto request, Action<BookshelfResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<CreateBookshelfRequestDto, BookshelfResponseDto>("/v1/bookshelves", request, onSuccess, onError);
        }

        public Coroutine UpdateBookshelf(long bookshelfId, UpdateBookshelfRequestDto request, Action<BookshelfResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Patch<UpdateBookshelfRequestDto, BookshelfResponseDto>($"/v1/bookshelves/{bookshelfId}", request, onSuccess, onError);
        }

        public Coroutine DeleteBookshelf(long bookshelfId, Action<VoidResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Delete<VoidResponseDto>($"/v1/bookshelves/{bookshelfId}", onSuccess, onError);
        }

        public Coroutine ReorderBookshelves(ReorderBookshelvesRequestDto request, Action<VoidResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Put<ReorderBookshelvesRequestDto, VoidResponseDto>("/v1/bookshelves/order", request, onSuccess, onError);
        }

        public Coroutine UpdateSectionAlignment(long bookshelfId, long sectionId, SectionAlignmentRequestDto request, Action<SectionResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Patch<SectionAlignmentRequestDto, SectionResponseDto>($"/v1/bookshelves/{bookshelfId}/sections/{sectionId}/alignment", request, onSuccess, onError);
        }

        public Coroutine AddInteriorObject(long bookshelfId, InteriorObjectRequestDto request, Action<BookshelfItemResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<InteriorObjectRequestDto, BookshelfItemResponseDto>($"/v1/bookshelves/{bookshelfId}/interior-objects", request, onSuccess, onError);
        }

        public Coroutine MoveBookshelfItem(long itemId, BookshelfItemRequestDto request, Action<BookshelfItemResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Patch<BookshelfItemRequestDto, BookshelfItemResponseDto>($"/v1/bookshelf-items/{itemId}", request, onSuccess, onError);
        }

        private Coroutine Get<T>(string path, Action<T> onSuccess, Action<ApiErrorResponseDto> onError)
        {
            return RequireApi().Get<ApiEnvelopeDto<T>>(path, envelope => HandleEnvelope(path, envelope, onSuccess, onError), onError);
        }

        private Coroutine Post<TResponse>(string path, Action<TResponse> onSuccess, Action<ApiErrorResponseDto> onError)
        {
            return RequireApi().Post<ApiEnvelopeDto<TResponse>>(path, envelope => HandleEnvelope(path, envelope, onSuccess, onError), onError);
        }

        private Coroutine Post<TRequest, TResponse>(string path, TRequest request, Action<TResponse> onSuccess, Action<ApiErrorResponseDto> onError)
        {
            return RequireApi().Post<TRequest, ApiEnvelopeDto<TResponse>>(path, request, envelope => HandleEnvelope(path, envelope, onSuccess, onError), onError);
        }

        private Coroutine Put<TRequest, TResponse>(string path, TRequest request, Action<TResponse> onSuccess, Action<ApiErrorResponseDto> onError)
        {
            return RequireApi().Put<TRequest, ApiEnvelopeDto<TResponse>>(path, request, envelope => HandleEnvelope(path, envelope, onSuccess, onError), onError);
        }

        private Coroutine Patch<TRequest, TResponse>(string path, TRequest request, Action<TResponse> onSuccess, Action<ApiErrorResponseDto> onError)
        {
            return RequireApi().Patch<TRequest, ApiEnvelopeDto<TResponse>>(path, request, envelope => HandleEnvelope(path, envelope, onSuccess, onError), onError);
        }

        private Coroutine Patch<TResponse>(string path, Action<TResponse> onSuccess, Action<ApiErrorResponseDto> onError)
        {
            return RequireApi().Patch<ApiEnvelopeDto<TResponse>>(path, envelope => HandleEnvelope(path, envelope, onSuccess, onError), onError);
        }

        private Coroutine Delete<TResponse>(string path, Action<TResponse> onSuccess, Action<ApiErrorResponseDto> onError)
        {
            return RequireApi().Delete<ApiEnvelopeDto<TResponse>>(path, envelope => HandleEnvelope(path, envelope, onSuccess, onError), onError);
        }

        private APIManager RequireApi()
        {
            if (api == null)
            {
                throw new InvalidOperationException("NotebookApiClient requires an APIManager instance in the scene.");
            }

            return api;
        }

        private static void HandleEnvelope<T>(
            string path,
            ApiEnvelopeDto<T> envelope,
            Action<T> onSuccess,
            Action<ApiErrorResponseDto> onError)
        {
            if (envelope != null && envelope.Succeeded)
            {
                onSuccess?.Invoke(envelope.Result);
                return;
            }

            var error = new ApiErrorResponseDto
            {
                Code = envelope?.Code,
                Message = envelope?.Message,
                Error = envelope?.Message
            };
            Debug.LogWarning($"Notebook API returned unsuccessful envelope: {path} {error.Code} {error.Message}");
            onError?.Invoke(error);
        }

        private static Dictionary<string, string> Query(string key, string value)
        {
            return new Dictionary<string, string> { [key] = value };
        }

        private static Dictionary<string, string> BuildCursorQuery(string cursor, int limit)
        {
            var query = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(cursor))
            {
                query["cursor"] = cursor;
            }
            if (limit > 0)
            {
                query["limit"] = limit.ToString();
            }
            return query;
        }

        private static SocialLoginRequestDto NormalizeSocialLoginRequest(SocialLoginRequestDto request)
        {
            if (request == null)
            {
                return request;
            }

            return new SocialLoginRequestDto
            {
                Provider = request.Provider,
                TokenType = request.TokenType,
                Token = FirstNonEmpty(request.Token),
                RedirectUri = request.RedirectUri,
                Nonce = request.Nonce
            };
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static string WithQuery(string path, Dictionary<string, string> query)
        {
            if (query == null || query.Count == 0)
            {
                return path;
            }

            var parts = new List<string>(query.Count);
            foreach (var pair in query)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                {
                    continue;
                }

                parts.Add($"{UnityWebRequest.EscapeURL(pair.Key)}={UnityWebRequest.EscapeURL(pair.Value)}");
            }

            return parts.Count == 0 ? path : $"{path}?{string.Join("&", parts)}";
        }
    }
}
