using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace App.Infrastructure.Api.Dtos
{
    [Serializable]
    public sealed class VoidResponseDto
    {
    }

    [Serializable]
    public sealed class ToggleResponseDto
    {
        [JsonProperty("active")]
        public bool Active { get; set; }

        [JsonProperty("count")]
        public long Count { get; set; }
    }

    [Serializable]
    public sealed class IdResponseDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }
    }

    [Serializable]
    public sealed class AuthTokenResponseDto
    {
        [JsonProperty("userId")]
        public long UserId { get; set; }

        [JsonProperty("accessToken")]
        public string AccessToken { get; set; }

        [JsonProperty("refreshToken")]
        public string RefreshToken { get; set; }

        [JsonProperty("tokenType")]
        public string TokenType { get; set; }

        [JsonProperty("expiresIn")]
        public long ExpiresIn { get; set; }

        [JsonProperty("newUser")]
        public bool NewUser { get; set; }

        [JsonProperty("onboarded")]
        public bool Onboarded { get; set; }
    }

    [Serializable]
    public sealed class SocialLoginRequestDto
    {
        [JsonProperty("provider")]
        public string Provider { get; set; }

        [JsonProperty("tokenType")]
        public string TokenType { get; set; }

        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("redirectUri")]
        public string RedirectUri { get; set; }

        // Required by BE for provider=APPLE; the value must match the nonce
        // claim baked into the Apple identityToken. See AuthDtos.java in
        // backend/src/main/java/com/dotshelf/domain/auth/dto/.
        [JsonProperty("nonce")]
        public string Nonce { get; set; }
    }

    // Browser-OAuth (desktop) login: Unity opens an external browser, OAuth
    // provider redirects back to a loopback URL with `?code=...&state=...`,
    // and Unity ships the code to BE here. BE swaps the code for provider
    // tokens server-side using its own client_secret + the same redirectUri.
    //
    // Posted to the unified /v1/auth/social-login endpoint (same path as the
    // mobile token-flow). BE disambiguates the two flows by inspecting
    // `tokenType` — code-flow always sends `AUTHORIZATION_CODE`, token-flow
    // sends ID_TOKEN / ACCESS_TOKEN. The DTOs are kept separate on the client
    // because code/redirectUri/state are meaningless for token-flow and
    // mixing them into one shape obscures intent.
    [Serializable]
    public sealed class SocialLoginByCodeRequestDto
    {
        [JsonProperty("provider")]
        public string Provider { get; set; }

        // Discriminator for the unified /social-login endpoint. Always
        // "AUTHORIZATION_CODE" for the desktop browser-OAuth flow.
        [JsonProperty("tokenType")]
        public string TokenType { get; set; }

        // The current BE record SocialLoginRequest only declares
        // (provider, tokenType, token, nonce) and validates `token` with
        // @NotBlank. Until BE adds first-class `code` support, populate
        // `token` with the same authorization-code value so the request
        // passes validation and BE's code-exchange path
        // (GoogleTokenVerifier#exchangeCodeForIdToken,
        // KakaoTokenVerifier#exchangeCodeForAccessToken) consumes it.
        // BE deserialization is lenient (FAIL_ON_UNKNOWN_PROPERTIES=false)
        // so the additional `code`/`redirectUri`/`state` fields are ignored
        // for now and stay forward-compatible if BE later adopts them.
        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        // Must match the redirect_uri used in the authorize request, otherwise
        // the OAuth provider rejects the code exchange.
        [JsonProperty("redirectUri")]
        public string RedirectUri { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        // OIDC nonce, sent to the authorize endpoint and (for Google/Apple)
        // baked into the resulting id_token. BE re-validates against this.
        [JsonProperty("nonce")]
        public string Nonce { get; set; }

        // PKCE code_verifier (RFC 7636) — authorize 시점에 SHA256 hash해서 code_challenge로 보낸 원본.
        // BE는 이 값을 token 교환 호출에 그대로 forward해서 OAuth 제공자가 challenge 검증함.
        // null이면 BE는 legacy server pair (client_secret) 흐름으로 fallback.
        [JsonProperty("codeVerifier")]
        public string CodeVerifier { get; set; }
    }

    [Serializable]
    public sealed class TokenRefreshRequestDto
    {
        [JsonProperty("refreshToken")]
        public string RefreshToken { get; set; }
    }

    [Serializable]
    public sealed class LogoutRequestDto
    {
        [JsonProperty("refreshToken")]
        public string RefreshToken { get; set; }
    }


    [Serializable]
    public sealed class AddBookToShelfRequestDto
    {
        [JsonProperty("bookId")]
        public long BookId { get; set; }

        [JsonProperty("sectionId")]
        public long SectionId { get; set; }

        [JsonProperty("slotNo")]
        public int SlotNo { get; set; }

        [JsonProperty("facingDirection")]
        public string FacingDirection { get; set; }

        [JsonProperty("displayStyle")]
        public string DisplayStyle { get; set; }
    }

    [Serializable]
    public sealed class AddKakaoBookToShelfRequestDto
    {
        [JsonProperty("isbn13")]
        public string Isbn13 { get; set; }

        [JsonProperty("isbn10")]
        public string Isbn10 { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("publisher")]
        public string Publisher { get; set; }

        [JsonProperty("publishedDate")]
        public string PublishedDate { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("coverImageUrl")]
        public string CoverImageUrl { get; set; }

        [JsonProperty("sourceUrl")]
        public string SourceUrl { get; set; }

        [JsonProperty("sectionId")]
        public long SectionId { get; set; }

        [JsonProperty("slotNo")]
        public int SlotNo { get; set; }

        [JsonProperty("facingDirection")]
        public string FacingDirection { get; set; }

        [JsonProperty("displayStyle")]
        public string DisplayStyle { get; set; }
    }

    [Serializable]
    public sealed class UpdateUserBookRequestDto
    {
        [JsonProperty("readingStatus")]
        public string ReadingStatus { get; set; }

        [JsonProperty("ownerRating")]
        public decimal? OwnerRating { get; set; }

        [JsonProperty("selectedCoverProductId")]
        public long? SelectedCoverProductId { get; set; }
    }

    [Serializable]
    public sealed class MoveUserBookRequestDto
    {
        [JsonProperty("bookshelfId")]
        public long BookshelfId { get; set; }

        [JsonProperty("sectionId")]
        public long SectionId { get; set; }

        [JsonProperty("slotNo")]
        public int SlotNo { get; set; }

        [JsonProperty("facingDirection")]
        public string FacingDirection { get; set; }

        [JsonProperty("displayStyle")]
        public string DisplayStyle { get; set; }
    }

    [Serializable]
    public sealed class UserBookPlacementSummaryResponseDto
    {
        [JsonProperty("bookshelfItemId")]
        public long? BookshelfItemId { get; set; }

        [JsonProperty("bookshelfId")]
        public long? BookshelfId { get; set; }

        [JsonProperty("sectionId")]
        public long? SectionId { get; set; }

        [JsonProperty("slotNo")]
        public int? SlotNo { get; set; }

        [JsonProperty("facingDirection")]
        public string FacingDirection { get; set; }

        [JsonProperty("displayStyle")]
        public string DisplayStyle { get; set; }

        [JsonProperty("rotationDeg")]
        public decimal RotationDeg { get; set; }
    }

    [Serializable]
    public sealed class UserBookResponseDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("userId")]
        public long UserId { get; set; }

        [JsonProperty("bookId")]
        public long BookId { get; set; }

        [JsonProperty("readingStatus")]
        public string ReadingStatus { get; set; }

        [JsonProperty("storageStatus")]
        public string StorageStatus { get; set; }

        [JsonProperty("ownerRating")]
        public decimal? OwnerRating { get; set; }

        [JsonProperty("liked")]
        public bool Liked { get; set; }

        [JsonProperty("placement")]
        public UserBookPlacementSummaryResponseDto Placement { get; set; }
    }

    [Serializable]
    public sealed class UserBookListResponseDto
    {
        [JsonProperty("books")]
        public List<UserBookResponseDto> Books { get; set; }
    }

    [Serializable]
    public sealed class UserBookPlacementResponseDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("userId")]
        public long UserId { get; set; }

        [JsonProperty("bookId")]
        public long BookId { get; set; }

        [JsonProperty("readingStatus")]
        public string ReadingStatus { get; set; }

        [JsonProperty("storageStatus")]
        public string StorageStatus { get; set; }

        [JsonProperty("ownerRating")]
        public decimal? OwnerRating { get; set; }

        [JsonProperty("liked")]
        public bool Liked { get; set; }

        [JsonProperty("bookshelfItemId")]
        public long? BookshelfItemId { get; set; }

        [JsonProperty("bookshelfId")]
        public long? BookshelfId { get; set; }

        [JsonProperty("sectionId")]
        public long? SectionId { get; set; }

        [JsonProperty("slotNo")]
        public int? SlotNo { get; set; }

        [JsonProperty("facingDirection")]
        public string FacingDirection { get; set; }

        [JsonProperty("displayStyle")]
        public string DisplayStyle { get; set; }
    }

    [Serializable]
    public sealed class UserBookPlacementListResponseDto
    {
        [JsonProperty("books")]
        public List<UserBookPlacementResponseDto> Books { get; set; }

        [JsonProperty("nextCursor")]
        public string NextCursor { get; set; }
    }

    [Serializable]
    public sealed class AssetResponseDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("categoryId")]
        public long CategoryId { get; set; }

        [JsonProperty("categoryCode")]
        public string CategoryCode { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("tier")]
        public string Tier { get; set; }

        [JsonProperty("assetType")]
        public string AssetType { get; set; }

        [JsonProperty("previewImageUrl")]
        public string PreviewImageUrl { get; set; }

        [JsonProperty("pricePages")]
        public int PricePages { get; set; }

        [JsonProperty("owned")]
        public bool Owned { get; set; }

        [JsonProperty("wished")]
        public bool Wished { get; set; }

        // 전체 테마 자산에서만 의미 — false면 적용 후 카테고리 변경 시 자동 해제(themeReset).
        [JsonProperty("allowsCustomization")]
        public bool AllowsCustomization { get; set; } = true;
    }

    [Serializable]
    public sealed class AssetListResponseDto
    {
        [JsonProperty("assets")]
        public List<AssetResponseDto> Assets { get; set; }
    }

    [Serializable]
    public sealed class AssetCategoryResponseDto
    {
        [JsonProperty("categoryId")]
        public long CategoryId { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("displayOrder")]
        public int DisplayOrder { get; set; }
    }

    [Serializable]
    public sealed class AssetCategoryListResponseDto
    {
        [JsonProperty("categories")]
        public List<AssetCategoryResponseDto> Categories { get; set; }
    }

    [Serializable]
    public sealed class InventoryResponseDto
    {
        [JsonProperty("assetIds")]
        public List<long> AssetIds { get; set; }

        [JsonProperty("items")]
        public List<InventoryItemResponseDto> Items { get; set; }
    }

    [Serializable]
    public sealed class InventoryItemResponseDto
    {
        [JsonProperty("inventoryId")]
        public long InventoryId { get; set; }

        [JsonProperty("assetId")]
        public long AssetId { get; set; }
    }

    [Serializable]
    public sealed class AssetPurchaseResponseDto
    {
        [JsonProperty("purchaseId")]
        public long PurchaseId { get; set; }

        [JsonProperty("assetId")]
        public long AssetId { get; set; }

        [JsonProperty("inventoryId")]
        public long InventoryId { get; set; }

        [JsonProperty("pricePages")]
        public int PricePages { get; set; }
    }

    [Serializable]
    public sealed class SpaceResponseDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("ownerUserId")]
        public long OwnerUserId { get; set; }

        [JsonProperty("spaceName")]
        public string SpaceName { get; set; }

        [JsonProperty("visibilityScope")]
        public string VisibilityScope { get; set; }

        [JsonProperty("guestbookVisibilityScope")]
        public string GuestbookVisibilityScope { get; set; }

        [JsonProperty("visitCount")]
        public long VisitCount { get; set; }

        [JsonProperty("likeCount")]
        public long LikeCount { get; set; }

        [JsonProperty("liked")]
        public bool Liked { get; set; }

        [JsonProperty("layoutVersion")]
        public long LayoutVersion { get; set; }

        [JsonProperty("thumbnailUrl")]
        public string ThumbnailUrl { get; set; }

        [JsonProperty("currentThemeInventoryId")]
        public long? CurrentThemeInventoryId { get; set; }

        [JsonProperty("currentWallpaperInventoryId")]
        public long? CurrentWallpaperInventoryId { get; set; }

        [JsonProperty("currentFloorInventoryId")]
        public long? CurrentFloorInventoryId { get; set; }

        [JsonProperty("currentDeskInventoryId")]
        public long? CurrentDeskInventoryId { get; set; }

        [JsonProperty("currentDashboardInventoryId")]
        public long? CurrentDashboardInventoryId { get; set; }

        [JsonProperty("currentInteriorLightInventoryId")]
        public long? CurrentInteriorLightInventoryId { get; set; }

        [JsonProperty("currentExteriorLightInventoryId")]
        public long? CurrentExteriorLightInventoryId { get; set; }
    }

    [Serializable]
    public sealed class SpaceThumbnailUploadUrlRequestDto
    {
        [JsonProperty("contentType")]
        public string ContentType { get; set; }

        [JsonProperty("byteLength")]
        public long ByteLength { get; set; }
    }

    [Serializable]
    public sealed class SpaceThumbnailUploadUrlResponseDto
    {
        [JsonProperty("uploadUrl")]
        public string UploadUrl { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("publicUrl")]
        public string PublicUrl { get; set; }

        [JsonProperty("expiresInSeconds")]
        public long ExpiresInSeconds { get; set; }
    }

    [Serializable]
    public sealed class SpaceThumbnailConfirmRequestDto
    {
        [JsonProperty("key")]
        public string Key { get; set; }
    }

    [Serializable]
    public sealed class SpaceThumbnailUploadResponseDto
    {
        [JsonProperty("spaceId")]
        public long SpaceId { get; set; }

        [JsonProperty("thumbnailKey")]
        public string ThumbnailKey { get; set; }

        [JsonProperty("thumbnailUrl")]
        public string ThumbnailUrl { get; set; }

        [JsonProperty("layoutVersion")]
        public long LayoutVersion { get; set; }
    }

    /// <summary>
    /// 카테고리별 PATCH 6 endpoint 공통 body. inventoryId가 null이면 unset (기본 외형).
    /// </summary>
    [Serializable]
    public sealed class CategoryInventoryRequestDto
    {
        [JsonProperty("inventoryId")]
        public long? InventoryId { get; set; }
    }

    /// <summary>
    /// 카테고리별 PATCH + theme apply 공통 응답. themeReset == true면 클라이언트가
    /// 룸 전체 reload (커스터마이징 불가 테마 자동 해제).
    /// </summary>
    [Serializable]
    public sealed class SpaceMutationResponseDto
    {
        [JsonProperty("space")]
        public SpaceResponseDto Space { get; set; }

        [JsonProperty("themeReset")]
        public bool ThemeReset { get; set; }
    }

    /// <summary>
    /// 전체 테마 자산이 묶고 있는 카테고리별 자식 자산 ID. compatibleTargets JSON에서 파싱됨.
    /// 미설정 키는 null. API 명세서 "compatibleTargets JSON 스키마" 참조.
    /// </summary>
    [Serializable]
    public sealed class ThemeContentsResponseDto
    {
        [JsonProperty("themeAssetId")]
        public long ThemeAssetId { get; set; }

        [JsonProperty("wallpaper")]
        public long? Wallpaper { get; set; }

        [JsonProperty("floor")]
        public long? Floor { get; set; }

        [JsonProperty("desk")]
        public long? Desk { get; set; }

        [JsonProperty("dashboard")]
        public long? Dashboard { get; set; }

        [JsonProperty("interiorLight")]
        public long? InteriorLight { get; set; }

        [JsonProperty("exteriorLight")]
        public long? ExteriorLight { get; set; }

        [JsonProperty("bookshelfSkinDefault")]
        public long? BookshelfSkinDefault { get; set; }
    }

    [Serializable]
    public sealed class SpaceListItemResponseDto
    {
        [JsonProperty("spaceId")]
        public long SpaceId { get; set; }

        [JsonProperty("ownerUserId")]
        public long OwnerUserId { get; set; }

        [JsonProperty("ownerNickname")]
        public string OwnerNickname { get; set; }

        [JsonProperty("ownerProfileImageUrl")]
        public string OwnerProfileImageUrl { get; set; }

        [JsonProperty("spaceName")]
        public string SpaceName { get; set; }

        [JsonProperty("mood")]
        public string Mood { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("thumbnailUrl")]
        public string ThumbnailUrl { get; set; }

        [JsonProperty("visibilityScope")]
        public string VisibilityScope { get; set; }

        [JsonProperty("visitCount")]
        public long VisitCount { get; set; }

        [JsonProperty("likeCount")]
        public long LikeCount { get; set; }

        [JsonProperty("followerCount")]
        public long FollowerCount { get; set; }

        [JsonProperty("liked")]
        public bool Liked { get; set; }

        [JsonProperty("following")]
        public bool Following { get; set; }
    }

    [Serializable]
    public sealed class SpaceListResponseDto
    {
        [JsonProperty("spaces")]
        public List<SpaceListItemResponseDto> Spaces { get; set; }

        [JsonProperty("nextCursor")]
        public string NextCursor { get; set; }
    }

    [Serializable]
    public sealed class DashboardResponseDto
    {
        [JsonProperty("space")]
        public SpaceResponseDto Space { get; set; }

        [JsonProperty("followerCount")]
        public long FollowerCount { get; set; }

        [JsonProperty("followingCount")]
        public long FollowingCount { get; set; }

        [JsonProperty("bookshelfCount")]
        public long BookshelfCount { get; set; }

        [JsonProperty("bookCount")]
        public long BookCount { get; set; }

        [JsonProperty("readingRecordCount")]
        public long ReadingRecordCount { get; set; }

        [JsonProperty("weeklyVisitorCount")]
        public long WeeklyVisitorCount { get; set; }

        [JsonProperty("ownerNickname")]
        public string OwnerNickname { get; set; }

        [JsonProperty("ownerProfileImageUrl")]
        public string OwnerProfileImageUrl { get; set; }

        [JsonProperty("ownerIntroduction")]
        public string OwnerIntroduction { get; set; }
    }

    [Serializable]
    public sealed class LayoutSaveRequestDto
    {
        [JsonProperty("layoutPayload")]
        public string LayoutPayload { get; set; }

        [JsonProperty("baseLayoutVersion")]
        public long? BaseLayoutVersion { get; set; }
    }

    [Serializable]
    public sealed class LayoutSaveResponseDto
    {
        [JsonProperty("spaceId")]
        public long SpaceId { get; set; }

        [JsonProperty("layoutVersion")]
        public long LayoutVersion { get; set; }
    }

    [Serializable]
    public sealed class VisibilityRequestDto
    {
        [JsonProperty("visibilityScope")]
        public string VisibilityScope { get; set; }
    }

    [Serializable]
    public sealed class ThemeApplyRequestDto
    {
        [JsonProperty("inventoryId")]
        public long? InventoryId { get; set; }
    }

    [Serializable]
    public sealed class SpaceTemplateResponseDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("previewImageUrl")]
        public string PreviewImageUrl { get; set; }

        [JsonProperty("layoutPayload")]
        public string LayoutPayload { get; set; }
    }

    [Serializable]
    public sealed class SpaceTemplateListResponseDto
    {
        [JsonProperty("templates")]
        public List<SpaceTemplateResponseDto> Templates { get; set; }
    }

    [Serializable]
    public sealed class FeedEventResponseDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("actorUserId")]
        public long ActorUserId { get; set; }

        [JsonProperty("eventType")]
        public string EventType { get; set; }

        [JsonProperty("targetType")]
        public string TargetType { get; set; }

        [JsonProperty("targetId")]
        public long TargetId { get; set; }

        [JsonProperty("occurredAt")]
        public string OccurredAt { get; set; }
    }

    [Serializable]
    public sealed class FeedEventListResponseDto
    {
        [JsonProperty("events")]
        public List<FeedEventResponseDto> Events { get; set; }
    }

    [Serializable]
    public sealed class BookPromotionResponseDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("bookId")]
        public long BookId { get; set; }

        [JsonProperty("promotionType")]
        public string PromotionType { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("imageUrl")]
        public string ImageUrl { get; set; }

        [JsonProperty("landingUrl")]
        public string LandingUrl { get; set; }

        [JsonProperty("sponsorName")]
        public string SponsorName { get; set; }
    }

    [Serializable]
    public sealed class BookPromotionListResponseDto
    {
        [JsonProperty("promotions")]
        public List<BookPromotionResponseDto> Promotions { get; set; }
    }

    [Serializable]
    public sealed class BookReviewRequestDto
    {
        [JsonProperty("rating")]
        public decimal Rating { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }
    }

    [Serializable]
    public sealed class BookReviewResponseDto
    {
        [JsonProperty("reviewId")]
        public long ReviewId { get; set; }

        [JsonProperty("bookId")]
        public long BookId { get; set; }

        [JsonProperty("userId")]
        public long UserId { get; set; }

        [JsonProperty("nickname")]
        public string Nickname { get; set; }

        [JsonProperty("profileImageUrl")]
        public string ProfileImageUrl { get; set; }

        [JsonProperty("rating")]
        public decimal Rating { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("likeCount")]
        public int LikeCount { get; set; }

        [JsonProperty("liked")]
        public bool Liked { get; set; }

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; }

        [JsonProperty("updatedAt")]
        public string UpdatedAt { get; set; }
    }

    [Serializable]
    public sealed class BookReviewListResponseDto
    {
        [JsonProperty("reviews")]
        public List<BookReviewResponseDto> Reviews { get; set; }

        [JsonProperty("nextCursor")]
        public string NextCursor { get; set; }
    }

    [Serializable]
    public sealed class PageChargeRequestDto
    {
        [JsonProperty("pagesAmount")]
        public int PagesAmount { get; set; }

        [JsonProperty("amountKrw")]
        public int AmountKrw { get; set; }

        [JsonProperty("platform")]
        public string Platform { get; set; }
    }

    [Serializable]
    public sealed class WalletResponseDto
    {
        [JsonProperty("userId")]
        public long UserId { get; set; }

        [JsonProperty("balancePages")]
        public int BalancePages { get; set; }
    }

    [Serializable]
    public sealed class PurchaseHistoryResponseDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("assetId")]
        public long AssetId { get; set; }

        [JsonProperty("pricePages")]
        public int PricePages { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }
    }

    [Serializable]
    public sealed class PurchaseHistoryListResponseDto
    {
        [JsonProperty("purchases")]
        public List<PurchaseHistoryResponseDto> Purchases { get; set; }
    }

    [Serializable]
    public sealed class PaymentMethodListResponseDto
    {
        [JsonProperty("methods")]
        public List<string> Methods { get; set; }
    }

    [Serializable]
    public sealed class UserProfileResponseDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("nickname")]
        public string Nickname { get; set; }

        [JsonProperty("profileImageUrl")]
        public string ProfileImageUrl { get; set; }

        [JsonProperty("introduction")]
        public string Introduction { get; set; }

        [JsonProperty("onboarded")]
        public bool Onboarded { get; set; }

        [JsonProperty("mutual")]
        public bool Mutual { get; set; }

        [JsonProperty("following")]
        public bool Following { get; set; }

        [JsonProperty("blocked")]
        public bool Blocked { get; set; }
    }

    [Serializable]
    public sealed class UpdateProfileRequestDto
    {
        [JsonProperty("nickname")]
        public string Nickname { get; set; }

        [JsonProperty("profileImageUrl")]
        public string ProfileImageUrl { get; set; }

        [JsonProperty("introduction")]
        public string Introduction { get; set; }
    }

    [Serializable]
    public sealed class NicknameAvailabilityResponseDto
    {
        [JsonProperty("available")]
        public bool Available { get; set; }
    }

    [Serializable]
    public sealed class UserSummaryResponseDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("nickname")]
        public string Nickname { get; set; }

        [JsonProperty("profileImageUrl")]
        public string ProfileImageUrl { get; set; }

        [JsonProperty("mutual")]
        public bool Mutual { get; set; }

        [JsonProperty("following")]
        public bool Following { get; set; }

        [JsonProperty("blocked")]
        public bool Blocked { get; set; }
    }

    [Serializable]
    public sealed class UserListResponseDto
    {
        [JsonProperty("users")]
        public List<UserSummaryResponseDto> Users { get; set; }

        [JsonProperty("nextCursor")]
        public string NextCursor { get; set; }
    }

    [Serializable]
    public sealed class NotificationSettingRequestDto
    {
        [JsonProperty("notifyNewGuestbook")]
        public bool? NotifyNewGuestbook { get; set; }

        [JsonProperty("notifyGuestbookReply")]
        public bool? NotifyGuestbookReply { get; set; }

        [JsonProperty("notifyReadingRecordReaction")]
        public bool? NotifyReadingRecordReaction { get; set; }

        [JsonProperty("notifyReadingRecordComment")]
        public bool? NotifyReadingRecordComment { get; set; }

        [JsonProperty("notifyNewFollower")]
        public bool? NotifyNewFollower { get; set; }

        [JsonProperty("notifyBookAddedByOthers")]
        public bool? NotifyBookAddedByOthers { get; set; }
    }

    [Serializable]
    public sealed class NotificationSettingResponseDto
    {
        [JsonProperty("notifyNewGuestbook")]
        public bool NotifyNewGuestbook { get; set; }

        [JsonProperty("notifyGuestbookReply")]
        public bool NotifyGuestbookReply { get; set; }

        [JsonProperty("notifyReadingRecordReaction")]
        public bool NotifyReadingRecordReaction { get; set; }

        [JsonProperty("notifyReadingRecordComment")]
        public bool NotifyReadingRecordComment { get; set; }

        [JsonProperty("notifyNewFollower")]
        public bool NotifyNewFollower { get; set; }

        [JsonProperty("notifyBookAddedByOthers")]
        public bool NotifyBookAddedByOthers { get; set; }
    }

    [Serializable]
    public sealed class FeedSettingRequestDto
    {
        [JsonProperty("showBookAdded")]
        public bool? ShowBookAdded { get; set; }

        [JsonProperty("showReadingRecord")]
        public bool? ShowReadingRecord { get; set; }

        [JsonProperty("showSpaceVisit")]
        public bool? ShowSpaceVisit { get; set; }

        [JsonProperty("showGuestbook")]
        public bool? ShowGuestbook { get; set; }

        [JsonProperty("showReadingRecordComment")]
        public bool? ShowReadingRecordComment { get; set; }
    }

    [Serializable]
    public sealed class FeedSettingResponseDto
    {
        [JsonProperty("showBookAdded")]
        public bool ShowBookAdded { get; set; }

        [JsonProperty("showReadingRecord")]
        public bool ShowReadingRecord { get; set; }

        [JsonProperty("showSpaceVisit")]
        public bool ShowSpaceVisit { get; set; }

        [JsonProperty("showGuestbook")]
        public bool ShowGuestbook { get; set; }

        [JsonProperty("showReadingRecordComment")]
        public bool ShowReadingRecordComment { get; set; }
    }

    [Serializable]
    public sealed class GuestbookRequestDto
    {
        [JsonProperty("postItInventoryId")]
        public long? PostItInventoryId { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("visibilityScope")]
        public string VisibilityScope { get; set; }

        [JsonProperty("anonymous")]
        public bool Anonymous { get; set; }
    }

    [Serializable]
    public sealed class GuestbookResponseDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("spaceId")]
        public long SpaceId { get; set; }

        [JsonProperty("writerUserId")]
        public long WriterUserId { get; set; }

        [JsonProperty("writerNickname")]
        public string WriterNickname { get; set; }

        [JsonProperty("writerProfileImageUrl")]
        public string WriterProfileImageUrl { get; set; }

        [JsonProperty("anonymous")]
        public bool Anonymous { get; set; }

        [JsonProperty("postItInventoryId")]
        public long? PostItInventoryId { get; set; }

        [JsonProperty("postItPreviewImageUrl")]
        public string PostItPreviewImageUrl { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("visibilityScope")]
        public string VisibilityScope { get; set; }

        [JsonProperty("replyCount")]
        public int ReplyCount { get; set; }

        [JsonProperty("reply")]
        public GuestbookReplyResponseDto Reply { get; set; }

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; }
    }

    [Serializable]
    public sealed class GuestbookListResponseDto
    {
        [JsonProperty("guestbooks")]
        public List<GuestbookResponseDto> Guestbooks { get; set; }

        [JsonProperty("nextCursor")]
        public string NextCursor { get; set; }
    }

    [Serializable]
    public sealed class GuestbookReplyRequestDto
    {
        [JsonProperty("postItInventoryId")]
        public long? PostItInventoryId { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("visibilityScope")]
        public string VisibilityScope { get; set; }
    }

    [Serializable]
    public sealed class GuestbookReplyResponseDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("guestbookId")]
        public long GuestbookId { get; set; }

        [JsonProperty("writerUserId")]
        public long WriterUserId { get; set; }

        [JsonProperty("writerNickname")]
        public string WriterNickname { get; set; }

        [JsonProperty("postItInventoryId")]
        public long? PostItInventoryId { get; set; }

        [JsonProperty("postItPreviewImageUrl")]
        public string PostItPreviewImageUrl { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("visibilityScope")]
        public string VisibilityScope { get; set; }

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; }
    }

    [Serializable]
    public sealed class GuestbookPostItWhitelistRequestDto
    {
        [JsonProperty("allowAll")]
        public bool AllowAll { get; set; }

        [JsonProperty("allowedAssetIds")]
        public List<long> AllowedAssetIds { get; set; }
    }

    [Serializable]
    public sealed class GuestbookPostItWhitelistResponseDto
    {
        [JsonProperty("allowAll")]
        public bool AllowAll { get; set; }

        [JsonProperty("allowedAssetIds")]
        public List<long> AllowedAssetIds { get; set; }
    }

    [Serializable]
    public sealed class NotificationResponseDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("senderUserId")]
        public long? SenderUserId { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("targetType")]
        public string TargetType { get; set; }

        [JsonProperty("targetId")]
        public long? TargetId { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("read")]
        public bool Read { get; set; }
    }

    [Serializable]
    public sealed class NotificationListResponseDto
    {
        [JsonProperty("notifications")]
        public List<NotificationResponseDto> Notifications { get; set; }
    }

    [Serializable]
    public sealed class ReportRequestDto
    {
        [JsonProperty("reportedUserId")]
        public long? ReportedUserId { get; set; }

        [JsonProperty("targetType")]
        public string TargetType { get; set; }

        [JsonProperty("targetId")]
        public long TargetId { get; set; }

        [JsonProperty("reasonType")]
        public string ReasonType { get; set; }

        [JsonProperty("reasonDetail")]
        public string ReasonDetail { get; set; }
    }

    [Serializable]
    public sealed class ReportResponseDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("reporterUserId")]
        public long ReporterUserId { get; set; }

        [JsonProperty("reportedUserId")]
        public long? ReportedUserId { get; set; }

        [JsonProperty("targetType")]
        public string TargetType { get; set; }

        [JsonProperty("targetId")]
        public long TargetId { get; set; }

        [JsonProperty("reasonType")]
        public string ReasonType { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }
    }

    [Serializable]
    public sealed class ReportListResponseDto
    {
        [JsonProperty("reports")]
        public List<ReportResponseDto> Reports { get; set; }
    }

    [Serializable]
    public sealed class ReadingRecordRequestDto
    {
        [JsonProperty("userBookId")]
        public long UserBookId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("visibilityScope")]
        public string VisibilityScope { get; set; }
    }

    [Serializable]
    public sealed class ReadingRecordResponseDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("userBookId")]
        public long UserBookId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("visibilityScope")]
        public string VisibilityScope { get; set; }

        [JsonProperty("likeCount")]
        public int LikeCount { get; set; }

        [JsonProperty("commentCount")]
        public int CommentCount { get; set; }

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; }

        [JsonProperty("updatedAt")]
        public string UpdatedAt { get; set; }
    }

    /// <summary>
    /// 대시보드 \"독후감 풀 목록\" 모달 응답 (v1.0 갱신).
    /// 정렬·cursor 페이지네이션 지원, content/likeCount/commentCount/viewCount 노출.
    /// </summary>
    [Serializable]
    public sealed class ReadingRecordSummaryDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("bookId")]
        public long BookId { get; set; }

        [JsonProperty("bookTitle")]
        public string BookTitle { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("visibilityScope")]
        public string VisibilityScope { get; set; }

        [JsonProperty("likeCount")]
        public int LikeCount { get; set; }

        [JsonProperty("commentCount")]
        public int CommentCount { get; set; }

        [JsonProperty("viewCount")]
        public int ViewCount { get; set; }

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; }
    }

    [Serializable]
    public sealed class ReadingRecordPageResponseDto
    {
        [JsonProperty("items")]
        public List<ReadingRecordSummaryDto> Items { get; set; }

        [JsonProperty("nextCursor")]
        public string NextCursor { get; set; }
    }

    /// <summary>
    /// 대시보드 \"보유 책 풀 목록\" 모달 응답 (v1.0 갱신).
    /// title/author/publisher/publishedDate/coverImageUrl/addedAt — 카드에 노출되는 필드만.
    /// </summary>
    [Serializable]
    public sealed class OwnedBookSummaryDto
    {
        [JsonProperty("userBookId")]
        public long UserBookId { get; set; }

        [JsonProperty("bookId")]
        public long BookId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("publisher")]
        public string Publisher { get; set; }

        [JsonProperty("publishedDate")]
        public string PublishedDate { get; set; }

        [JsonProperty("coverImageUrl")]
        public string CoverImageUrl { get; set; }

        [JsonProperty("addedAt")]
        public string AddedAt { get; set; }
    }

    [Serializable]
    public sealed class OwnedBookListResponseDto
    {
        [JsonProperty("items")]
        public List<OwnedBookSummaryDto> Items { get; set; }

        [JsonProperty("nextCursor")]
        public string NextCursor { get; set; }
    }

    [Serializable]
    public sealed class CommentRequestDto
    {
        [JsonProperty("parentCommentId")]
        public long? ParentCommentId { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }
    }

    [Serializable]
    public sealed class CommentResponseDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("readingRecordId")]
        public long ReadingRecordId { get; set; }

        [JsonProperty("userId")]
        public long UserId { get; set; }

        [JsonProperty("parentCommentId")]
        public long? ParentCommentId { get; set; }

        [JsonProperty("depth")]
        public int Depth { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("likeCount")]
        public int LikeCount { get; set; }

        [JsonProperty("deleted")]
        public bool Deleted { get; set; }
    }

    [Serializable]
    public sealed class CommentListResponseDto
    {
        [JsonProperty("comments")]
        public List<CommentResponseDto> Comments { get; set; }
    }

    [Serializable]
    public sealed class BookshelfResponseDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("spaceId")]
        public long SpaceId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("visibilityScope")]
        public string VisibilityScope { get; set; }

        [JsonProperty("displayOrder")]
        public int DisplayOrder { get; set; }

        [JsonProperty("bodyInventoryId")]
        public long? BodyInventoryId { get; set; }

        [JsonProperty("skinInventoryId")]
        public long? SkinInventoryId { get; set; }
    }

    [Serializable]
    public sealed class BookshelfListResponseDto
    {
        [JsonProperty("bookshelves")]
        public List<BookshelfResponseDto> Bookshelves { get; set; }
    }

    [Serializable]
    public sealed class BookshelfRenderResponseDto
    {
        [JsonProperty("bookshelf")]
        public BookshelfResponseDto Bookshelf { get; set; }

        [JsonProperty("sections")]
        public List<SectionResponseDto> Sections { get; set; }

        [JsonProperty("items")]
        public List<BookshelfRenderItemResponseDto> Items { get; set; }
    }

    [Serializable]
    public sealed class CreateBookshelfRequestDto
    {
        [JsonProperty("spaceId")]
        public long SpaceId { get; set; }

        [JsonProperty("bodyInventoryId")]
        public long? BodyInventoryId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("visibilityScope")]
        public string VisibilityScope { get; set; }
    }

    [Serializable]
    public sealed class UpdateBookshelfRequestDto
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("visibilityScope")]
        public string VisibilityScope { get; set; }

        [JsonProperty("skinInventoryId")]
        public long? SkinInventoryId { get; set; }
    }

    [Serializable]
    public sealed class ReorderBookshelvesRequestDto
    {
        [JsonProperty("bookshelfIds")]
        public List<long> BookshelfIds { get; set; }
    }

    [Serializable]
    public sealed class SectionAlignmentRequestDto
    {
        [JsonProperty("alignment")]
        public string Alignment { get; set; }
    }

    [Serializable]
    public sealed class SectionResponseDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("rowNo")]
        public int RowNo { get; set; }

        [JsonProperty("columnNo")]
        public int ColumnNo { get; set; }

        [JsonProperty("maxItemCount")]
        public int MaxItemCount { get; set; }

        [JsonProperty("alignment")]
        public string Alignment { get; set; }
    }

    [Serializable]
    public sealed class InteriorObjectRequestDto
    {
        [JsonProperty("assetInventoryId")]
        public long AssetInventoryId { get; set; }

        [JsonProperty("sectionId")]
        public long SectionId { get; set; }

        [JsonProperty("slotNo")]
        public int SlotNo { get; set; }

        [JsonProperty("facingDirection")]
        public string FacingDirection { get; set; }

        [JsonProperty("displayStyle")]
        public string DisplayStyle { get; set; }
    }

    [Serializable]
    public sealed class BookshelfItemRequestDto
    {
        [JsonProperty("bookshelfId")]
        public long BookshelfId { get; set; }

        [JsonProperty("sectionId")]
        public long SectionId { get; set; }

        [JsonProperty("slotNo")]
        public int SlotNo { get; set; }

        [JsonProperty("facingDirection")]
        public string FacingDirection { get; set; }

        [JsonProperty("displayStyle")]
        public string DisplayStyle { get; set; }
    }

    [Serializable]
    public sealed class BookSummaryResponseDto
    {
        [JsonProperty("bookId")]
        public long BookId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("publisher")]
        public string Publisher { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("isbn13")]
        public string Isbn13 { get; set; }

        [JsonProperty("isbn10")]
        public string Isbn10 { get; set; }

        [JsonProperty("coverImageUrl")]
        public string CoverImageUrl { get; set; }

        [JsonProperty("avgRating")]
        public decimal? AvgRating { get; set; }

        [JsonProperty("averageRating")]
        public decimal? AverageRating { get; set; }

        [JsonProperty("likeCount")]
        public int? LikeCount { get; set; }

        [JsonProperty("reviewCount")]
        public int? ReviewCount { get; set; }

        [JsonProperty("recordCount")]
        public int? RecordCount { get; set; }
    }

    [Serializable]
    public sealed class UserBookSummaryResponseDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("userId")]
        public long UserId { get; set; }

        [JsonProperty("bookId")]
        public long BookId { get; set; }

        [JsonProperty("readingStatus")]
        public string ReadingStatus { get; set; }

        [JsonProperty("storageStatus")]
        public string StorageStatus { get; set; }

        [JsonProperty("ownerRating")]
        public decimal? OwnerRating { get; set; }

        [JsonProperty("liked")]
        public bool Liked { get; set; }

        [JsonProperty("selectedCoverProductId")]
        public long? SelectedCoverProductId { get; set; }
    }

    [Serializable]
    public sealed class BookshelfRenderItemResponseDto
    {
        [JsonProperty("bookshelfItemId")]
        public long BookshelfItemId { get; set; }

        [JsonProperty("itemType")]
        public string ItemType { get; set; }

        [JsonProperty("sectionId")]
        public long SectionId { get; set; }

        [JsonProperty("sectionRow")]
        public int? SectionRow { get; set; }

        [JsonProperty("sectionColumn")]
        public int? SectionColumn { get; set; }

        [JsonProperty("slotNo")]
        public int? SlotNo { get; set; }

        [JsonProperty("facingDirection")]
        public string FacingDirection { get; set; }

        [JsonProperty("displayStyle")]
        public string DisplayStyle { get; set; }

        [JsonProperty("rotationDeg")]
        public decimal RotationDeg { get; set; }

        [JsonProperty("userBookId")]
        public long? UserBookId { get; set; }

        [JsonProperty("userBook")]
        public UserBookSummaryResponseDto UserBook { get; set; }

        [JsonProperty("book")]
        public BookSummaryResponseDto Book { get; set; }

        [JsonProperty("assetInventoryId")]
        public long? AssetInventoryId { get; set; }

        [JsonProperty("asset")]
        public AssetResponseDto Asset { get; set; }

        [JsonProperty("readingRecord")]
        public ReadingRecordResponseDto ReadingRecord { get; set; }
    }

    [Serializable]
    public sealed class BookshelfItemResponseDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("bookshelfId")]
        public long BookshelfId { get; set; }

        [JsonProperty("sectionId")]
        public long SectionId { get; set; }

        [JsonProperty("slotNo")]
        public int SlotNo { get; set; }

        [JsonProperty("facingDirection")]
        public string FacingDirection { get; set; }

        [JsonProperty("itemType")]
        public string ItemType { get; set; }

        [JsonProperty("userBookId")]
        public long? UserBookId { get; set; }

        [JsonProperty("assetInventoryId")]
        public long? AssetInventoryId { get; set; }

        [JsonProperty("displayStyle")]
        public string DisplayStyle { get; set; }

        [JsonProperty("rotationDeg")]
        public decimal RotationDeg { get; set; }
    }
}
