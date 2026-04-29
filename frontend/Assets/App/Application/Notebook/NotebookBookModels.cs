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

    public enum NotebookShelfDisplayStyle
    {
        Spine
    }

    public sealed class NotebookShelfPlacement
    {
        public NotebookShelfPlacement(long sectionId, int slotNo, NotebookShelfDisplayStyle displayStyle)
        {
            SectionId = sectionId;
            SlotNo = slotNo;
            DisplayStyle = displayStyle;
        }

        public long SectionId { get; }
        public int SlotNo { get; }
        public NotebookShelfDisplayStyle DisplayStyle { get; }
    }

    public sealed class AddNotebookBookToShelfCommand
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
