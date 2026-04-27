using System.Collections.Generic;
using Newtonsoft.Json;

namespace App.Infrastructure.Api.Dtos
{
    public sealed class BookListResponseDto
    {
        [JsonProperty("items")]
        public List<BookListItemDto> Items { get; set; }

        [JsonProperty("nextCursor")]
        public string NextCursor { get; set; }
    }

    public sealed class BookListItemDto
    {
        [JsonProperty("bookId")]
        public string BookId { get; set; }

        [JsonProperty("isbn")]
        public string Isbn { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("publisher")]
        public string Publisher { get; set; }

        [JsonProperty("coverImageUrl")]
        public string CoverImageUrl { get; set; }

        [JsonProperty("avgRating")]
        public decimal AvgRating { get; set; }

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
}
