using System;
using System.Collections.Generic;

namespace App.Application.Notebook
{
    public sealed class NotebookBookSearchResult
    {
        public NotebookBookSearchResult(IReadOnlyList<NotebookBookSearchItem> items)
        {
            Items = items ?? Array.Empty<NotebookBookSearchItem>();
        }

        public IReadOnlyList<NotebookBookSearchItem> Items { get; }
    }

    public sealed class NotebookBookSearchItem
    {
        public long Id { get; set; }
        public string Source { get; set; }
        public string BookId { get; set; }
        public string Isbn { get; set; }
        public string Isbn13 { get; set; }
        public string Isbn10 { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Publisher { get; set; }
        public string PublishedDate { get; set; }
        public string Description { get; set; }
        public string CoverImageUrl { get; set; }
        public string SourceUrl { get; set; }
        public decimal AvgRating { get; set; }
        public int LikeCount { get; set; }
        public int PurchaseCount { get; set; }
        public int RecordCount { get; set; }
        public int PlacementCount { get; set; }
        public bool HasPaidCover { get; set; }
    }

    public sealed class NotebookBookAddRequest
    {
        public string Source { get; set; }
        public long? BookId { get; set; }
        public string Isbn13 { get; set; }
        public string Isbn10 { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Publisher { get; set; }
        public string PublishedDate { get; set; }
        public string Description { get; set; }
        public string CoverImageUrl { get; set; }
        public long SectionId { get; set; }
        public int SlotNo { get; set; }
        public string DisplayStyle { get; set; }
    }

    public sealed class NotebookBookAddResult
    {
        public long Id { get; set; }
        public long BookId { get; set; }
    }

    public sealed class NotebookApiError
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
    }
}
