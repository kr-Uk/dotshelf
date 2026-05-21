using System.Collections.Generic;
using Newtonsoft.Json;

namespace App.Infrastructure.Api.Dtos
{
    public sealed class BookListResponseDto
    {
        [JsonProperty("items")]
        public List<BookListItemDto> Items { get; set; }

        [JsonProperty("books")]
        public List<BookListItemDto> Books { get; set; }

        [JsonProperty("nextCursor")]
        public string NextCursor { get; set; }

        [JsonIgnore]
        public List<BookListItemDto> EffectiveItems =>
            Items != null && Items.Count > 0 ? Items : Books ?? new List<BookListItemDto>();
    }

    public class BookListItemDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; }

        [JsonProperty("bookId")]
        public string BookId { get; set; }

        [JsonProperty("book_id")]
        private string BookIdSnake
        {
            set => BookId = value;
        }

        [JsonProperty("isbn")]
        public string Isbn { get; set; }

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

        [JsonProperty("published_date")]
        private string PublishedDateSnake
        {
            set => PublishedDate = value;
        }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("coverImageUrl")]
        public string CoverImageUrl { get; set; }

        [JsonProperty("cover_image_url")]
        private string CoverImageUrlSnake
        {
            set => CoverImageUrl = value;
        }

        [JsonProperty("thumbnailUrl")]
        private string ThumbnailUrl
        {
            set
            {
                if (string.IsNullOrWhiteSpace(CoverImageUrl))
                {
                    CoverImageUrl = value;
                }
            }
        }

        [JsonProperty("thumbnail_url")]
        private string ThumbnailUrlSnake
        {
            set
            {
                if (string.IsNullOrWhiteSpace(CoverImageUrl))
                {
                    CoverImageUrl = value;
                }
            }
        }

        [JsonProperty("sourceUrl")]
        public string SourceUrl { get; set; }

        [JsonProperty("source_url")]
        private string SourceUrlSnake
        {
            set => SourceUrl = value;
        }

        [JsonProperty("avgRating")]
        public decimal AvgRating { get; set; }

        [JsonProperty("reviewCount")]
        public int ReviewCount { get; set; }

        [JsonProperty("review_count")]
        private int ReviewCountSnake
        {
            set => ReviewCount = value;
        }

        [JsonProperty("viewCount")]
        public int ViewCount { get; set; }

        [JsonProperty("view_count")]
        private int ViewCountSnake
        {
            set => ViewCount = value;
        }

        [JsonProperty("pricePages")]
        public int PricePages { get; set; }

        [JsonProperty("rank")]
        public int Rank { get; set; }

        [JsonProperty("owned")]
        public bool Owned { get; set; }

        [JsonProperty("userBookId")]
        public long? UserBookId { get; set; }

        [JsonProperty("placementCount")]
        public int PlacementCount { get; set; }

        [JsonProperty("placement_count")]
        private int PlacementCountSnake
        {
            set => PlacementCount = value;
        }

        [JsonProperty("bookshelfAddCount")]
        public int BookshelfAddCount { get; set; }

        [JsonProperty("bookshelf_add_count")]
        private int BookshelfAddCountSnake
        {
            set => BookshelfAddCount = value;
        }

        [JsonProperty("purchaseCount")]
        public int PurchaseCount { get; set; }

        [JsonProperty("likeCount")]
        public int LikeCount { get; set; }

        [JsonProperty("like_count")]
        private int LikeCountSnake
        {
            set => LikeCount = value;
        }

        [JsonProperty("recordCount")]
        public int RecordCount { get; set; }

        [JsonProperty("record_count")]
        private int RecordCountSnake
        {
            set => RecordCount = value;
        }

        [JsonProperty("hasPaidCover")]
        public bool HasPaidCover { get; set; }

        [JsonIgnore]
        public int EffectiveBookshelfAddCount => BookshelfAddCount > 0 ? BookshelfAddCount : PlacementCount;
    }

    public sealed class BookResponseDto : BookListItemDto
    {
    }
}
