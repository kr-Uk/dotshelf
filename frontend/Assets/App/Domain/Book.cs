using System;

namespace App.Domain
{
    public sealed class Book
    {
        public string Id { get; }
        public string BookId => Id;
        public string Isbn { get; }
        public string Isbn13 { get; }
        public string Isbn10 { get; }
        public string Title { get; }
        public string Author { get; }
        public string Publisher { get; }
        public DateTime? PublishedDate { get; }
        public string Description { get; }
        public string CoverImageUrl { get; }
        public string ThumbnailUrl => CoverImageUrl;
        public int? PageCount { get; }
        public string Language { get; }
        public long ViewCount { get; }
        public long LikeCount { get; }
        public long BookshelfAddCount { get; }
        public decimal RatingSum { get; }
        public int RatingCount { get; }
        public DateTimeOffset CreatedAt { get; }
        public DateTimeOffset UpdatedAt { get; }
        public decimal AverageRating => RatingCount <= 0 ? 0m : RatingSum / RatingCount;

        public Book(
            string id,
            string title,
            string author,
            string publisher = null,
            string description = null,
            string coverImageUrl = null,
            string isbn = null,
            string isbn13 = null,
            string isbn10 = null,
            DateTime? publishedDate = null,
            int? pageCount = null,
            string language = null,
            long viewCount = 0,
            long likeCount = 0,
            long bookshelfAddCount = 0,
            decimal ratingSum = 0m,
            int ratingCount = 0,
            DateTimeOffset? createdAt = null,
            DateTimeOffset? updatedAt = null)
        {
            Id = id;
            Isbn = isbn ?? isbn13 ?? isbn10;
            Isbn13 = isbn13;
            Isbn10 = isbn10;
            Title = title;
            Author = author;
            Publisher = publisher;
            PublishedDate = publishedDate;
            Description = description;
            CoverImageUrl = coverImageUrl;
            PageCount = pageCount;
            Language = language;
            ViewCount = viewCount;
            LikeCount = likeCount;
            BookshelfAddCount = bookshelfAddCount;
            RatingSum = ratingSum;
            RatingCount = ratingCount;
            CreatedAt = createdAt ?? DateTimeOffset.MinValue;
            UpdatedAt = updatedAt ?? DateTimeOffset.MinValue;
        }
    }
}

