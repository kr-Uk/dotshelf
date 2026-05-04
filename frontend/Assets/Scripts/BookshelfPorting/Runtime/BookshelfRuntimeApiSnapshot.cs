using System.Collections.Generic;
using App.Domain;

namespace BookshelfPorting.Runtime
{
    public sealed class BookshelfRuntimeApiSnapshot
    {
        public string BookshelfId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public VisibilityScope VisibilityScope { get; set; } = VisibilityScope.Public;
        public int DisplayOrder { get; set; }
        public string BodyInventoryId { get; set; }
        public string SkinInventoryId { get; set; }
        public bool HasThemeStyle { get; set; }
        public BookshelfThemeStyle ThemeStyle { get; set; } = BookshelfThemeStyle.Default;
        public List<BookshelfRuntimeSectionSnapshot> Sections { get; } = new List<BookshelfRuntimeSectionSnapshot>();
        public List<BookshelfRuntimeBookSnapshot> Books { get; } = new List<BookshelfRuntimeBookSnapshot>();
    }

    public sealed class BookshelfRuntimeSectionSnapshot
    {
        public string SectionId { get; set; }
        public int Row { get; set; }
        public int Column { get; set; }
        public ShelfSectionAlignment Alignment { get; set; } = ShelfSectionAlignment.Left;
    }

    public sealed class BookshelfRuntimeBookSnapshot
    {
        public string BookshelfItemId { get; set; }
        public string UserBookId { get; set; }
        public string BookId { get; set; }
        public string SectionId { get; set; }
        public int Row { get; set; }
        public int Column { get; set; }
        public int SlotNo { get; set; } = 1;
        public BookOrientation Orientation { get; set; } = BookOrientation.Spine;
        public string Title { get; set; }
        public string Author { get; set; }
        public string Publisher { get; set; }
        public string Description { get; set; }
        public string MemoPreview { get; set; }
        public float AverageRating { get; set; }
        public int TotalLikeCount { get; set; }
        public int ReviewCount { get; set; }
        public bool HasAggregateBookStats { get; set; }
        public float OwnerRating { get; set; }
        public ReadingStatus ReadingStatus { get; set; } = ReadingStatus.Unread;
        public bool IsLiked { get; set; }
        public bool IsPaidCoverOwned { get; set; }
        public string ReadingRecordId { get; set; }
        public string ReportTitle { get; set; }
        public string ReportBody { get; set; }
        public string ReportUpdatedLabel { get; set; }
        public VisibilityScope ReportVisibility { get; set; } = VisibilityScope.Public;
    }
}
