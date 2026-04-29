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

        public Coroutine CheckHealth(Action<ApiHealthCheckResult> onComplete, string path = "/health")
        {
            return RequireApi().CheckHealth(onComplete, path);
        }

        public Coroutine StartGuestSession(Action<GuestSessionResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<GuestSessionResponseDto>("/v1/auth/guest-session", onSuccess, onError);
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

        public Coroutine ImportGuestData(GuestMigrationRequestDto request, Action<IdResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Post<GuestMigrationRequestDto, IdResponseDto>("/v1/auth/guest-migration", request, onSuccess, onError);
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
            var query = new Dictionary<string, string>
            {
                ["category"] = string.IsNullOrWhiteSpace(category) ? "RANKING" : category,
                ["limit"] = Mathf.Max(1, limit).ToString()
            };
            return Get<BookListResponseDto>(WithQuery("/v1/books", query), onSuccess, onError);
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
            var query = new Dictionary<string, string>
            {
                ["sort"] = string.IsNullOrWhiteSpace(sort) ? "POPULAR" : sort,
                ["limit"] = Mathf.Max(1, limit).ToString()
            };
            return Get<SpaceListResponseDto>(WithQuery("/v1/spaces", query), onSuccess, onError);
        }

        public Coroutine GetSpace(long spaceId, Action<SpaceResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<SpaceResponseDto>($"/v1/spaces/{spaceId}", onSuccess, onError);
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

        public Coroutine ApplyMySpaceTheme(ThemeApplyRequestDto request, Action<SpaceResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Patch<ThemeApplyRequestDto, SpaceResponseDto>("/v1/spaces/me/theme", request, onSuccess, onError);
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

        public Coroutine GetFollowings(Action<UserListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<UserListResponseDto>("/v1/users/me/followings", onSuccess, onError);
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

        public Coroutine GetUserReadingRecords(long userId, Action<ReadingRecordListResponseDto> onSuccess, Action<ApiErrorResponseDto> onError = null)
        {
            return Get<ReadingRecordListResponseDto>($"/v1/users/{userId}/reading-records", onSuccess, onError);
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
                Token = FirstNonEmpty(request.Token, request.IdToken, request.AccessToken)
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
