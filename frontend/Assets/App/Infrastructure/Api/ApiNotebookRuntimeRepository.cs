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
        private const string UnsupportedCode = "NOTEBOOK_API_UNSUPPORTED";
        private readonly NotebookApiClient client;

        public ApiNotebookRuntimeRepository(NotebookApiClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public void LoadWallet(Action<NotebookWalletResult> onSuccess, Action<NotebookApiError> onError)
        {
            client.GetWallet(
                response => onSuccess?.Invoke(new NotebookWalletResult { BalancePages = response?.BalancePages ?? 0 }),
                error => onError?.Invoke(MapError(error)));
        }

        public void ChargePages(int pagesAmount, Action<NotebookWalletResult> onSuccess, Action<NotebookApiError> onError)
        {
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
            client.GetFollowingFeed(
                response => onSuccess?.Invoke(new NotebookSpaceListResult(MapSpaces(response, limit))),
                error => onError?.Invoke(MapError(error)));
        }

        public void LikeSpace(long spaceId, bool liked, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError)
        {
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
            onError?.Invoke(Unsupported("Book review list endpoint is not available for notebook books yet."));
        }

        public void CreateBookReview(long bookId, float rating, string content, Action<NotebookReviewItem> onSuccess, Action<NotebookApiError> onError)
        {
            onError?.Invoke(Unsupported("Book review creation endpoint requires a userBookId/readingRecord contract."));
        }

        public void SetBookFavorite(long bookId, bool favorite, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError)
        {
            var success = new Action<ToggleResponseDto>(response => onSuccess?.Invoke(MapToggle(response)));
            var error = new Action<ApiErrorResponseDto>(apiError => onError?.Invoke(MapError(apiError)));
            if (favorite)
            {
                client.LikeUserBook(bookId, success, error);
            }
            else
            {
                client.UnlikeUserBook(bookId, success, error);
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
                Category = FirstNonEmpty(asset.AssetType, asset.Tier, "OBJECT"),
                Tier = asset.Tier,
                AssetType = asset.AssetType,
                PreviewImageUrl = asset.PreviewImageUrl,
                PricePages = asset.PricePages,
                Popularity = Math.Max(0, 10000 - index * 137),
                Owned = asset.Owned,
                Wished = asset.Wished
            }).ToArray();
        }

        private static IReadOnlyList<NotebookSpaceItem> MapSpaces(FeedEventListResponseDto response, int limit)
        {
            var events = response?.Events;
            if (events == null || events.Count == 0)
            {
                return Array.Empty<NotebookSpaceItem>();
            }

            return events
                .Where(e => e.TargetId > 0 || e.ActorUserId > 0)
                .Take(Mathf.Max(1, limit))
                .Select((e, index) =>
                {
                    var spaceId = e.TargetId > 0 ? e.TargetId : e.ActorUserId;
                    return new NotebookSpaceItem
                    {
                        Id = spaceId,
                        OwnerUserId = e.ActorUserId,
                        OwnerName = $"User {e.ActorUserId}",
                        SpaceName = $"Space {spaceId}",
                        Description = $"{FirstNonEmpty(e.EventType, "activity")} / {FirstNonEmpty(e.OccurredAt, "recent")}",
                        FollowerCount = Math.Max(0, 1200 - index * 83),
                        LikeCount = Math.Max(0, 600 - index * 41),
                        VisitCount = Math.Max(0, 3200 - index * 173)
                    };
                })
                .ToArray();
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

        private static NotebookApiError Unsupported(string message)
        {
            return new NotebookApiError
            {
                Code = UnsupportedCode,
                Message = message,
                Error = message
            };
        }

        private static string NormalizeCategory(string category)
        {
            return string.IsNullOrWhiteSpace(category) ? "all" : category.Trim();
        }

        private static string NormalizeSort(string sort)
        {
            if (string.IsNullOrWhiteSpace(sort))
            {
                return "popular";
            }

            var normalized = sort.Trim().ToLowerInvariant();
            return normalized == "recent" || normalized == "price" ? normalized : "popular";
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
