using System;
using System.Collections.Generic;

namespace App.Application.Notebook
{
    public sealed class NotebookWalletResult
    {
        public int BalancePages { get; set; }
    }

    public sealed class NotebookPurchaseHistoryResult
    {
        public NotebookPurchaseHistoryResult(IReadOnlyList<NotebookPurchaseHistoryItem> purchases)
        {
            Purchases = purchases ?? Array.Empty<NotebookPurchaseHistoryItem>();
        }

        public IReadOnlyList<NotebookPurchaseHistoryItem> Purchases { get; }
    }

    public sealed class NotebookPurchaseHistoryItem
    {
        public long Id { get; set; }
        public long AssetId { get; set; }
        public int PricePages { get; set; }
        public string Status { get; set; }
    }

    public sealed class NotebookPaymentMethodResult
    {
        public NotebookPaymentMethodResult(IReadOnlyList<string> methods)
        {
            Methods = methods ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> Methods { get; }
    }

    public sealed class NotebookToggleResult
    {
        public bool Active { get; set; }
        public long Count { get; set; }
    }

    public sealed class NotebookAssetListResult
    {
        public NotebookAssetListResult(IReadOnlyList<NotebookAssetItem> assets)
        {
            Assets = assets ?? Array.Empty<NotebookAssetItem>();
        }

        public IReadOnlyList<NotebookAssetItem> Assets { get; }
    }

    public sealed class NotebookAssetItem
    {
        public long Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string CategoryCode { get; set; }
        public string Tier { get; set; }
        public string AssetType { get; set; }
        public string PreviewImageUrl { get; set; }
        public int PricePages { get; set; }
        public int Popularity { get; set; }
        public bool Owned { get; set; }
        public bool Wished { get; set; }
    }

    public sealed class NotebookAssetPurchaseResult
    {
        public long AssetId { get; set; }
        public long InventoryId { get; set; }
        public int PricePages { get; set; }
    }

    public sealed class NotebookSpaceListResult
    {
        public NotebookSpaceListResult(IReadOnlyList<NotebookSpaceItem> spaces)
        {
            Spaces = spaces ?? Array.Empty<NotebookSpaceItem>();
        }

        public IReadOnlyList<NotebookSpaceItem> Spaces { get; }
    }

    public sealed class NotebookSpaceItem
    {
        public long Id { get; set; }
        public long OwnerUserId { get; set; }
        public string OwnerName { get; set; }
        public string SpaceName { get; set; }
        public string Description { get; set; }
        public long FollowerCount { get; set; }
        public long LikeCount { get; set; }
        public long VisitCount { get; set; }
        public bool Following { get; set; }
        public bool Liked { get; set; }
    }

    public sealed class NotebookShelfTarget
    {
        public long BookshelfId { get; set; }
        public long SectionId { get; set; }
        public int SlotNo { get; set; }
    }

    public sealed class NotebookReviewListResult
    {
        public NotebookReviewListResult(IReadOnlyList<NotebookReviewItem> reviews)
        {
            Reviews = reviews ?? Array.Empty<NotebookReviewItem>();
        }

        public IReadOnlyList<NotebookReviewItem> Reviews { get; }
    }

    public sealed class NotebookReviewItem
    {
        public long Id { get; set; }
        public string Author { get; set; }
        public float Rating { get; set; }
        public string Content { get; set; }
    }
}
