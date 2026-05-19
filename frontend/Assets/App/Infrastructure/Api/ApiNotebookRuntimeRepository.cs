using System;
using System.Collections.Generic;
using System.Linq;
using App.Application.Notebook;
using App.Infrastructure.Api.Dtos;
using UnityEngine;

namespace App.Infrastructure.Api
{
    public sealed class ApiNotebookRuntimeRepository : INotebookRuntimeRepository
    {
        private const string AuthRequiredCode = "NOTEBOOK_API_AUTH_REQUIRED";
        private readonly NotebookApiClient client;
        private readonly Func<bool> canUseAuthenticatedApis;

        public ApiNotebookRuntimeRepository(NotebookApiClient client, Func<bool> canUseAuthenticatedApis = null)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.canUseAuthenticatedApis = canUseAuthenticatedApis ?? (() => true);
        }

        public void LoadWallet(Action<NotebookWalletResult> onSuccess, Action<NotebookApiError> onError)
        {
            if (!RequireAuthenticatedApi(onError, "Wallet API requires an access token."))
            {
                return;
            }

            client.GetWallet(
                response => onSuccess?.Invoke(new NotebookWalletResult { BalancePages = response?.BalancePages ?? 0 }),
                error => onError?.Invoke(MapError(error)));
        }

        public void ChargePages(int pagesAmount, Action<NotebookWalletResult> onSuccess, Action<NotebookApiError> onError)
        {
            if (!RequireAuthenticatedApi(onError, "Page charge API requires an access token."))
            {
                return;
            }

            client.ChargePages(
                new PageChargeRequestDto
                {
                    PagesAmount = Mathf.Max(1, pagesAmount),
                    AmountKrw = Mathf.Max(1, pagesAmount),
                    Platform = "UNITY"
                },
                response => onSuccess?.Invoke(new NotebookWalletResult { BalancePages = response?.BalancePages ?? 0 }),
                error => onError?.Invoke(MapError(error)));
        }

        public void LoadPurchaseHistory(Action<NotebookPurchaseHistoryResult> onSuccess, Action<NotebookApiError> onError)
        {
            if (!RequireAuthenticatedApi(onError, "Purchase history API requires an access token."))
            {
                return;
            }

            client.GetPurchases(
                response => onSuccess?.Invoke(new NotebookPurchaseHistoryResult(MapPurchaseHistory(response))),
                error => onError?.Invoke(MapError(error)));
        }

        public void LoadPaymentMethods(Action<NotebookPaymentMethodResult> onSuccess, Action<NotebookApiError> onError)
        {
            if (!RequireAuthenticatedApi(onError, "Payment method API requires an access token."))
            {
                return;
            }

            client.GetPaymentMethods(
                response =>
                {
                    IReadOnlyList<string> methods = response?.Methods != null
                        ? response.Methods
                        : Array.Empty<string>();
                    onSuccess?.Invoke(new NotebookPaymentMethodResult(methods));
                },
                error => onError?.Invoke(MapError(error)));
        }

        public void LoadAssets(string category, string sort, int limit, Action<NotebookAssetListResult> onSuccess, Action<NotebookApiError> onError)
        {
            client.GetAssets(
                NormalizeCategory(category),
                NormalizeSort(sort),
                Mathf.Max(1, limit),
                response => onSuccess?.Invoke(new NotebookAssetListResult(MapAssets(response))),
                error => onError?.Invoke(MapError(error)));
        }

        public void LoadAssetInventory(Action<NotebookAssetInventoryResult> onSuccess, Action<NotebookApiError> onError)
        {
            if (!RequireAuthenticatedApi(onError, "Asset inventory API requires an access token."))
            {
                return;
            }

            client.GetAssetInventory(
                response => onSuccess?.Invoke(new NotebookAssetInventoryResult(MapAssetInventory(response))),
                error => onError?.Invoke(MapError(error)));
        }

        public void LoadAssetDownloadManifest(long assetId, Action<NotebookAssetDownloadManifestResult> onSuccess, Action<NotebookApiError> onError)
        {
            if (!RequireAuthenticatedApi(onError, "Asset download manifest API requires an access token."))
            {
                return;
            }

            client.GetAssetDownloadManifest(
                assetId,
                response => onSuccess?.Invoke(MapAssetDownloadManifest(response)),
                error => onError?.Invoke(MapError(error)));
        }

        public void LoadSpaceAssetDownloadManifests(long spaceId, Action<NotebookSpaceAssetManifestListResult> onSuccess, Action<NotebookApiError> onError)
        {
            client.GetSpaceAssetManifests(
                spaceId,
                response => onSuccess?.Invoke(MapSpaceAssetDownloadManifests(response)),
                error => onError?.Invoke(MapError(error)));
        }

        public void WishAsset(long assetId, bool wished, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError)
        {
            if (!RequireAuthenticatedApi(onError, "Asset wishlist API requires an access token."))
            {
                return;
            }

            var success = new Action<ToggleResponseDto>(response => onSuccess?.Invoke(MapToggle(response)));
            var error = new Action<ApiErrorResponseDto>(apiError => onError?.Invoke(MapError(apiError)));
            if (wished)
            {
                client.WishAsset(assetId, success, error);
            }
            else
            {
                client.UnwishAsset(assetId, success, error);
            }
        }

        public void PurchaseAsset(long assetId, Action<NotebookAssetPurchaseResult> onSuccess, Action<NotebookApiError> onError)
        {
            if (!RequireAuthenticatedApi(onError, "Asset purchase API requires an access token."))
            {
                return;
            }

            client.PurchaseAsset(
                assetId,
                response => onSuccess?.Invoke(new NotebookAssetPurchaseResult
                {
                    AssetId = response?.AssetId ?? assetId,
                    InventoryId = response?.InventoryId ?? 0L,
                    PricePages = response?.PricePages ?? 0
                }),
                error => onError?.Invoke(MapError(error)));
        }

        public void LoadSpaces(int limit, Action<NotebookSpaceListResult> onSuccess, Action<NotebookApiError> onError)
        {
            LoadSpaces(null, null, limit, onSuccess, onError);
        }

        public void LoadSpaces(string keyword, string cursor, int limit, Action<NotebookSpaceListResult> onSuccess, Action<NotebookApiError> onError)
        {
            client.GetSpaces(
                keyword,
                "POPULAR",
                cursor,
                Mathf.Max(1, limit),
                response => onSuccess?.Invoke(new NotebookSpaceListResult(MapSpaces(response, limit, client.CurrentUserId), null)),
                error => onError?.Invoke(MapError(error)));
        }

        public void UploadMySpaceThumbnail(byte[] imageBytes, string contentType, Action<NotebookSpaceThumbnailUploadResult> onSuccess, Action<NotebookApiError> onError)
        {
            if (!RequireAuthenticatedApi(onError, "Space thumbnail upload API requires an access token."))
            {
                return;
            }

            if (imageBytes == null || imageBytes.Length == 0)
            {
                onError?.Invoke(new NotebookApiError
                {
                    Code = "NOTEBOOK_SPACE_THUMBNAIL_EMPTY",
                    Message = "Space thumbnail image bytes are empty."
                });
                return;
            }

            var normalizedContentType = string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType.Trim();
            client.CreateMySpaceThumbnailUploadUrl(
                new SpaceThumbnailUploadUrlRequestDto
                {
                    ContentType = normalizedContentType,
                    ByteLength = imageBytes.LongLength
                },
                ticket =>
                {
                    if (ticket == null || string.IsNullOrWhiteSpace(ticket.UploadUrl) || string.IsNullOrWhiteSpace(ticket.Key))
                    {
                        onError?.Invoke(new NotebookApiError
                        {
                            Code = "NOTEBOOK_SPACE_THUMBNAIL_UPLOAD_URL_EMPTY",
                            Message = "Space thumbnail upload URL was not returned."
                        });
                        return;
                    }

                    client.UploadRawObject(
                        ticket.UploadUrl,
                        imageBytes,
                        normalizedContentType,
                        () => client.ConfirmMySpaceThumbnail(
                            new SpaceThumbnailConfirmRequestDto { Key = ticket.Key },
                            response => onSuccess?.Invoke(MapSpaceThumbnailUpload(response)),
                            error => onError?.Invoke(MapError(error))),
                        error => onError?.Invoke(MapError(error)));
                },
                error => onError?.Invoke(MapError(error)));
        }

        public void LoadDefaultShelfTarget(Action<NotebookShelfTarget> onSuccess, Action<NotebookApiError> onError)
        {
            if (!RequireAuthenticatedApi(onError, "Default bookshelf placement API requires an access token."))
            {
                return;
            }

            client.GetMySpace(
                space =>
                {
                    if (space == null || space.Id <= 0)
                    {
                        onError?.Invoke(new NotebookApiError { Code = "NOTEBOOK_SPACE_NOT_FOUND", Message = "My space was not found." });
                        return;
                    }

                    client.GetBookshelves(
                        space.Id,
                        shelves =>
                        {
                            var bookshelf = shelves?.Bookshelves?
                                .Where(item => item != null && item.Id > 0)
                                .OrderBy(item => item.DisplayOrder)
                                .ThenBy(item => item.Id)
                                .FirstOrDefault();
                            if (bookshelf == null)
                            {
                                onError?.Invoke(new NotebookApiError { Code = "NOTEBOOK_BOOKSHELF_NOT_FOUND", Message = "Default bookshelf was not found." });
                                return;
                            }

                            // 진단 — items API path 자리에 spaceId 가 들어가는지 가시화 + BE 버그 의심 케이스 경고.
                            if (bookshelf.SpaceId > 0 && bookshelf.SpaceId == bookshelf.Id)
                            {
                                Debug.LogWarning(
                                    $"[Bookshelf] shelf.SpaceId == shelf.Id ({bookshelf.Id}) — BE 가 같은 값 반환 중. items 500 가능성.");
                            }
                            Debug.Log($"[Bookshelf] GET /v1/bookshelves/{bookshelf.Id}/items (spaceId={bookshelf.SpaceId})");

                            client.GetBookshelfItems(
                                bookshelf.Id,
                                render => ResolveShelfTarget(bookshelf.Id, render, onSuccess, onError),
                                error => onError?.Invoke(MapError(error)));
                        },
                        error => onError?.Invoke(MapError(error)));
                },
                error => onError?.Invoke(MapError(error)));
        }

        public void LikeSpace(long spaceId, bool liked, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError)
        {
            if (!RequireAuthenticatedApi(onError, "Space like API requires an access token."))
            {
                return;
            }

            var success = new Action<ToggleResponseDto>(response => onSuccess?.Invoke(MapToggle(response)));
            var error = new Action<ApiErrorResponseDto>(apiError => onError?.Invoke(MapError(apiError)));
            if (liked)
            {
                client.LikeSpace(spaceId, success, error);
            }
            else
            {
                client.UnlikeSpace(spaceId, success, error);
            }
        }

        public void FollowUser(long userId, bool following, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError)
        {
            if (!RequireAuthenticatedApi(onError, "User follow API requires an access token."))
            {
                return;
            }

            var success = new Action<ToggleResponseDto>(response => onSuccess?.Invoke(MapToggle(response)));
            var error = new Action<ApiErrorResponseDto>(apiError => onError?.Invoke(MapError(apiError)));
            if (following)
            {
                client.FollowUser(userId, success, error);
            }
            else
            {
                client.UnfollowUser(userId, success, error);
            }
        }

        public void LoadUserFollowers(long userId, string cursor, int limit, Action<NotebookUserListResult> onSuccess, Action<NotebookApiError> onError)
        {
            client.GetUserFollowers(
                userId,
                cursor,
                Mathf.Max(1, limit),
                response => onSuccess?.Invoke(new NotebookUserListResult(MapUsers(response), response?.NextCursor)),
                error => onError?.Invoke(MapError(error)));
        }

        public void LoadUserFollowings(long userId, string cursor, int limit, Action<NotebookUserListResult> onSuccess, Action<NotebookApiError> onError)
        {
            client.GetUserFollowings(
                userId,
                cursor,
                Mathf.Max(1, limit),
                response => onSuccess?.Invoke(new NotebookUserListResult(MapUsers(response), response?.NextCursor)),
                error => onError?.Invoke(MapError(error)));
        }

        public void LoadBookReviews(long bookId, Action<NotebookReviewListResult> onSuccess, Action<NotebookApiError> onError)
        {
            client.GetBookReviews(
                bookId,
                "POPULAR",
                20,
                response =>
                {
                    var reviews = MapReviews(response);
                    LogVerbose($"Book review API loaded {reviews.Count} item(s). bookId={bookId}");
                    onSuccess?.Invoke(new NotebookReviewListResult(reviews));
                },
                error => onError?.Invoke(MapError(error)));
        }

        public void CreateBookReview(long bookId, float rating, string content, Action<NotebookReviewItem> onSuccess, Action<NotebookApiError> onError)
        {
            if (!RequireAuthenticatedApi(onError, "Book review creation API requires an access token."))
            {
                return;
            }

            var normalizedContent = string.IsNullOrWhiteSpace(content) ? string.Empty : content.Trim();
            client.CreateBookReview(
                bookId,
                new BookReviewRequestDto
                {
                    Rating = Convert.ToDecimal(Mathf.Clamp(rating, 0.5f, 5f)),
                    Content = normalizedContent
                },
                response =>
                {
                    LogVerbose($"Book review API created. bookId={bookId}, reviewId={response?.ReviewId ?? 0}");
                    onSuccess?.Invoke(MapReview(response));
                },
                error => onError?.Invoke(MapError(error)));
        }

        public void SetUserBookFavorite(long userBookId, bool favorite, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError)
        {
            if (!RequireAuthenticatedApi(onError, "User book favorite API requires an access token."))
            {
                return;
            }

            var success = new Action<ToggleResponseDto>(response => onSuccess?.Invoke(MapToggle(response)));
            var error = new Action<ApiErrorResponseDto>(apiError => onError?.Invoke(MapError(apiError)));
            if (favorite)
            {
                client.LikeUserBook(userBookId, success, error);
            }
            else
            {
                client.UnlikeUserBook(userBookId, success, error);
            }
        }

        private static IReadOnlyList<NotebookAssetItem> MapAssets(AssetListResponseDto response)
        {
            var assets = response?.Assets;
            if (assets == null || assets.Count == 0)
            {
                return Array.Empty<NotebookAssetItem>();
            }

            return assets.Select((asset, index) => MapAsset(asset, index, 0L)).ToArray();
        }

        private static IReadOnlyList<NotebookAssetItem> MapAssetInventory(InventoryResponseDto response)
        {
            var items = response?.Items;
            if (items == null || items.Count == 0)
            {
                return Array.Empty<NotebookAssetItem>();
            }

            return items
                .Where(item => item != null)
                .Select((item, index) =>
                {
                    var asset = item.Asset ?? new AssetResponseDto { Id = item.AssetId };
                    return MapAsset(asset, index, item.InventoryId);
                })
                .ToArray();
        }

        private static NotebookAssetItem MapAsset(AssetResponseDto asset, int index, long inventoryId)
        {
            var category = ResolveAssetCategoryCode(asset);
            return new NotebookAssetItem
            {
                Id = asset?.Id ?? 0L,
                InventoryId = inventoryId,
                Code = asset?.Code,
                Name = FirstNonEmpty(asset?.Name, asset?.Code, $"Asset {index + 1}"),
                Category = category,
                CategoryCode = category,
                Tier = asset?.Tier,
                AssetType = asset?.AssetType,
                CollectionCode = asset?.CollectionCode,
                Theme = asset?.Theme,
                Placement = asset?.Placement,
                PreviewImageUrl = asset?.PreviewImageUrl,
                UnityResourceKey = asset?.UnityResourceKey,
                FileSize = asset?.FileSize,
                FileHash = asset?.FileHash,
                ContentType = asset?.ContentType,
                Version = asset != null && asset.Version > 0 ? asset.Version : 1,
                PricePages = asset?.PricePages ?? 0,
                Popularity = 0,
                Owned = asset?.Owned ?? inventoryId > 0L,
                Wished = asset?.Wished ?? false
            };
        }

        private static NotebookAssetDownloadManifestResult MapAssetDownloadManifest(DownloadManifestResponseDto response)
        {
            var files = response?.Files == null
                ? Array.Empty<NotebookAssetDownloadFile>()
                : response.Files
                    .Where(file => file != null)
                    .Select(file => new NotebookAssetDownloadFile
                    {
                        AssetId = file.AssetId,
                        Code = file.Code,
                        Name = file.Name,
                        AssetType = file.AssetType,
                        Role = file.Role,
                        Url = file.Url,
                        ContentType = file.ContentType,
                        FileSize = file.FileSize,
                        FileHash = file.FileHash,
                        Version = file.Version > 0 ? file.Version : 1,
                        Required = file.Required,
                        DefaultTransform = file.DefaultTransform
                    })
                    .ToArray();

            return new NotebookAssetDownloadManifestResult
            {
                AssetId = response?.AssetId ?? 0L,
                Code = response?.Code,
                Name = response?.Name,
                AssetType = response?.AssetType,
                CollectionCode = response?.CollectionCode,
                Theme = response?.Theme,
                Placement = response?.Placement,
                UnityResourceKey = response?.UnityResourceKey,
                PreviewImageUrl = response?.PreviewImageUrl,
                Version = response != null && response.Version > 0 ? response.Version : 1,
                Files = files
            };
        }

        private static NotebookSpaceAssetManifestListResult MapSpaceAssetDownloadManifests(SpaceAssetManifestListResponseDto response)
        {
            var manifests = response?.Manifests == null
                ? Array.Empty<NotebookAssetDownloadManifestResult>()
                : response.Manifests
                    .Where(manifest => manifest != null)
                    .Select(MapAssetDownloadManifest)
                    .ToArray();

            return new NotebookSpaceAssetManifestListResult(manifests)
            {
                SpaceId = response?.SpaceId ?? 0L,
                LayoutVersion = response?.LayoutVersion ?? 0L
            };
        }

        private static IReadOnlyList<NotebookSpaceItem> MapSpaces(SpaceListResponseDto response, int limit, long currentUserId)
        {
            var spaces = response?.Spaces;
            if (spaces == null || spaces.Count == 0)
            {
                return Array.Empty<NotebookSpaceItem>();
            }

            return spaces
                .Where(space => space != null && space.SpaceId > 0)
                .Take(Mathf.Max(1, limit))
                .Select(space => new NotebookSpaceItem
                {
                    Id = space.SpaceId,
                    OwnerUserId = space.OwnerUserId,
                    OwnerName = FirstNonEmpty(space.OwnerNickname, $"User {space.OwnerUserId}"),
                    OwnerProfileImageUrl = space.OwnerProfileImageUrl,
                    SpaceName = FirstNonEmpty(space.SpaceName, $"Space {space.SpaceId}"),
                    Mood = FirstNonEmpty(space.Mood, "COMMUNITY"),
                    Description = FirstNonEmpty(space.Description, space.Mood, space.VisibilityScope),
                    ThumbnailUrl = space.ThumbnailUrl,
                    FollowerCount = Math.Max(0, space.FollowerCount),
                    LikeCount = Math.Max(0, space.LikeCount),
                    VisitCount = Math.Max(0, space.VisitCount),
                    Following = space.Following,
                    Liked = space.Liked,
                    IsOwnSpace = currentUserId > 0L && space.OwnerUserId == currentUserId
                })
                .ToArray();
        }

        private static NotebookSpaceThumbnailUploadResult MapSpaceThumbnailUpload(SpaceThumbnailUploadResponseDto response)
        {
            return new NotebookSpaceThumbnailUploadResult
            {
                SpaceId = response?.SpaceId ?? 0L,
                ThumbnailKey = response?.ThumbnailKey,
                ThumbnailUrl = response?.ThumbnailUrl,
                LayoutVersion = response?.LayoutVersion ?? 0L
            };
        }

        private static IReadOnlyList<NotebookUserItem> MapUsers(UserListResponseDto response)
        {
            var users = response?.Users;
            if (users == null || users.Count == 0)
            {
                return Array.Empty<NotebookUserItem>();
            }

            return users
                .Where(user => user != null && user.Id > 0)
                .Select(user => new NotebookUserItem
                {
                    Id = user.Id,
                    Nickname = FirstNonEmpty(user.Nickname, $"User {user.Id}"),
                    ProfileImageUrl = user.ProfileImageUrl,
                    Mutual = user.Mutual,
                    Following = user.Following,
                    Blocked = user.Blocked,
                    SpaceId = user.SpaceId
                })
                .ToArray();
        }

        private static IReadOnlyList<NotebookReviewItem> MapReviews(BookReviewListResponseDto response)
        {
            var reviews = response?.Reviews;
            if (reviews == null || reviews.Count == 0)
            {
                return Array.Empty<NotebookReviewItem>();
            }

            return reviews
                .Where(review => review != null)
                .Select(MapReview)
                .ToArray();
        }

        private static IReadOnlyList<NotebookPurchaseHistoryItem> MapPurchaseHistory(PurchaseHistoryListResponseDto response)
        {
            var purchases = response?.Purchases;
            if (purchases == null || purchases.Count == 0)
            {
                return Array.Empty<NotebookPurchaseHistoryItem>();
            }

            return purchases
                .Where(purchase => purchase != null)
                .Select(purchase => new NotebookPurchaseHistoryItem
                {
                    Id = purchase.Id,
                    AssetId = purchase.AssetId,
                    PricePages = purchase.PricePages,
                    Status = FirstNonEmpty(purchase.Status, "UNKNOWN")
                })
                .ToArray();
        }

        private static NotebookReviewItem MapReview(BookReviewResponseDto review)
        {
            if (review == null)
            {
                return null;
            }

            return new NotebookReviewItem
            {
                Id = review.ReviewId,
                Author = FirstNonEmpty(review.Nickname, $"User {review.UserId}"),
                Rating = Mathf.Clamp((float)review.Rating, 0f, 5f),
                Content = FirstNonEmpty(review.Content, string.Empty)
            };
        }

        private static void ResolveShelfTarget(
            long bookshelfId,
            BookshelfRenderResponseDto render,
            Action<NotebookShelfTarget> onSuccess,
            Action<NotebookApiError> onError)
        {
            var section = render?.Sections?
                .Where(item => item != null && item.Id > 0)
                .OrderBy(item => item.RowNo)
                .ThenBy(item => item.ColumnNo)
                .ThenBy(item => item.Id)
                .FirstOrDefault();
            if (section == null)
            {
                onError?.Invoke(new NotebookApiError { Code = "NOTEBOOK_SECTION_NOT_FOUND", Message = "Default bookshelf section was not found." });
                return;
            }

            var usedSlots = new HashSet<int>((render.Items ?? new List<BookshelfRenderItemResponseDto>())
                .Where(item => item != null && item.SectionId == section.Id && item.SlotNo.HasValue && item.SlotNo.Value > 0)
                .Select(item => item.SlotNo.Value));
            var max = Math.Max(1, section.MaxItemCount);
            for (var slot = 1; slot <= max; slot++)
            {
                if (usedSlots.Contains(slot))
                {
                    continue;
                }

                onSuccess?.Invoke(new NotebookShelfTarget
                {
                    BookshelfId = bookshelfId,
                    SectionId = section.Id,
                    SlotNo = slot
                });
                return;
            }

            onError?.Invoke(new NotebookApiError { Code = "NOTEBOOK_SHELF_FULL", Message = "No empty slot is available in the default bookshelf section." });
        }

        private static NotebookToggleResult MapToggle(ToggleResponseDto response)
        {
            return new NotebookToggleResult
            {
                Active = response != null && response.Active,
                Count = response?.Count ?? 0L
            };
        }

        private static NotebookApiError MapError(ApiErrorResponseDto error)
        {
            return new NotebookApiError
            {
                Code = error?.Code,
                Message = error?.Message,
                Error = error?.Error
            };
        }

        private bool RequireAuthenticatedApi(Action<NotebookApiError> onError, string message)
        {
            if (canUseAuthenticatedApis())
            {
                return true;
            }

            onError?.Invoke(new NotebookApiError
            {
                Code = AuthRequiredCode,
                Message = message,
                Error = message
            });
            return false;
        }

        private void LogVerbose(string message)
        {
            if (client != null && client.VerboseLogEnabled)
            {
                Debug.Log(message);
            }
        }

        private static string NormalizeCategory(string category)
        {
            return string.IsNullOrWhiteSpace(category) ? string.Empty : category.Trim();
        }

        private static string NormalizeSort(string sort)
        {
            if (string.IsNullOrWhiteSpace(sort))
            {
                return "popular";
            }

            var normalized = sort.Trim().ToLowerInvariant();
            if (normalized == "recent")
            {
                return "latest";
            }

            return normalized == "latest" || normalized == "price" ? normalized : "popular";
        }

        private static string ResolveAssetCategoryCode(AssetResponseDto asset)
        {
            if (!string.IsNullOrWhiteSpace(asset?.CategoryCode))
            {
                return asset.CategoryCode;
            }

            return asset?.CategoryId switch
            {
                1001L => "BOOKSHELF",
                1002L => "INTERIOR",
                1003L => "THEME",
                _ => FirstNonEmpty(asset?.AssetType, asset?.Tier, "OBJECT")
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
    }
}
