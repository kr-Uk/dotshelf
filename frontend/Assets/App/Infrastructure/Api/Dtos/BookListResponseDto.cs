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
        public List<BookListItemDto> EffectiveItems => Items ?? Books ?? new List<BookListItemDto>();
    }

    public class BookListItemDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; }

        [JsonProperty("bookId")]
        public string BookId { get; set; }

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

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("coverImageUrl")]
        public string CoverImageUrl { get; set; }

        [JsonProperty("sourceUrl")]
        public string SourceUrl { get; set; }

        [JsonProperty("avgRating")]
        public decimal AvgRating { get; set; }

        [JsonProperty("reviewCount")]
        public int ReviewCount { get; set; }

        [JsonProperty("viewCount")]
        public int ViewCount { get; set; }

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

        [JsonProperty("purchaseCount")]
        public int PurchaseCount { get; set; }

        [JsonProperty("likeCount")]
        public int LikeCount { get; set; }

        [JsonProperty("recordCount")]
        public int RecordCount { get; set; }

        [JsonProperty("hasPaidCover")]
        public bool HasPaidCover { get; set; }
    }

    public sealed class BookResponseDto : BookListItemDto
    {
    }
}
