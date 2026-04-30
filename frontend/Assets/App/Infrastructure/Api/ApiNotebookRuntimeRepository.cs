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

        public void LoadAssets(string category, string sort, int limit, Action<NotebookAssetListResult> onSuccess, Action<NotebookApiError> onError)
        {
            client.GetAssets(
                NormalizeCategory(category),
                NormalizeSort(sort),
                Mathf.Max(1, limit),
                response => onSuccess?.Invoke(new NotebookAssetListResult(MapAssets(response))),
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
            client.GetSpaces(
                "POPULAR",
                Mathf.Max(1, limit),
                response => onSuccess?.Invoke(new NotebookSpaceListResult(MapSpaces(response, limit))),
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

        public void LoadBookReviews(long bookId, Action<NotebookReviewListResult> onSuccess, Action<NotebookApiError> onError)
        {
            client.GetBookReviews(
                bookId,
                "POPULAR",
                20,
                response =>
                {
                    var reviews = MapReviews(response);
                    Debug.Log($"Book review API loaded {reviews.Count} item(s). bookId={bookId}");
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

            client.CreateBookReview(
                bookId,
                new BookReviewRequestDto
                {
                    Rating = Convert.ToDecimal(Mathf.Clamp(rating, 0.5f, 5f)),
                    Content = content
                },
                response =>
                {
                    Debug.Log($"Book review API created. bookId={bookId}, reviewId={response?.ReviewId ?? 0}");
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

            return assets.Select((asset, index) => new NotebookAssetItem
            {
                Id = asset.Id,
                Code = asset.Code,
                Name = FirstNonEmpty(asset.Name, asset.Code, $"Asset {index + 1}"),
                Category = ResolveAssetCategoryCode(asset),
                CategoryCode = ResolveAssetCategoryCode(asset),
                Tier = asset.Tier,
                AssetType = asset.AssetType,
                PreviewImageUrl = asset.PreviewImageUrl,
                PricePages = asset.PricePages,
                Popularity = 0,
                Owned = asset.Owned,
                Wished = asset.Wished
            }).ToArray();
        }

        private static IReadOnlyList<NotebookSpaceItem> MapSpaces(SpaceListResponseDto response, int limit)
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
                    SpaceName = FirstNonEmpty(space.SpaceName, $"Space {space.SpaceId}"),
                    Description = FirstNonEmpty(space.Description, space.Mood, space.VisibilityScope),
                    FollowerCount = Math.Max(0, space.FollowerCount),
                    LikeCount = Math.Max(0, space.LikeCount),
                    VisitCount = Math.Max(0, space.VisitCount),
                    Following = space.Following,
                    Liked = space.Liked
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

            var usedSlots = new HashSet<int>((render.Items ?? new List<BookshelfItemResponseDto>())
                .Where(item => item != null && item.SectionId == section.Id && item.SlotNo > 0)
                .Select(item => item.SlotNo));
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
