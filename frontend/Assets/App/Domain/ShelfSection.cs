using System;
using System.Collections.Generic;

namespace App.Domain
{
    public enum ShelfSectionAlignment
    {
        Left,
        Center,
        Right
    }

    public sealed class ShelfSection
    {
        public string Id { get; }
        public string BookshelfId { get; }
        public int Row { get; }
        public int Col { get; }
        public int RowNo => Row;
        public int ColumnNo => Col;
        public int MaxItemCount { get; set; }
        public ShelfSectionAlignment Alignment { get; set; } = ShelfSectionAlignment.Left;
        public decimal? PosX { get; set; }
        public decimal? PosY { get; set; }
        public decimal? Width { get; set; }
        public decimal? Height { get; set; }
        public int ZIndex { get; set; }
        public DateTimeOffset CreatedAt { get; }
        public DateTimeOffset UpdatedAt { get; set; }
        public List<string> BookIds { get; } = new List<string>();

        public ShelfSection(int row, int col)
            : this(string.Empty, string.Empty, row, col, 3)
        {
        }

        public ShelfSection(string id, string bookshelfId, int rowNo, int columnNo, int maxItemCount, DateTimeOffset? createdAt = null, DateTimeOffset? updatedAt = null)
        {
            Id = id;
            BookshelfId = bookshelfId;
            Row = rowNo;
            Col = columnNo;
            MaxItemCount = maxItemCount;
            CreatedAt = createdAt ?? DateTimeOffset.MinValue;
            UpdatedAt = updatedAt ?? DateTimeOffset.MinValue;
        }

        public void InsertBookAt(string userBookId, int slotIndex)
        {
            if (string.IsNullOrEmpty(userBookId))
            {
                return;
            }

            BookIds.Remove(userBookId);
            var index = slotIndex < 0 ? 0 : slotIndex > BookIds.Count ? BookIds.Count : slotIndex;
            BookIds.Insert(index, userBookId);
        }

        public void RemoveBook(string userBookId)
        {
            BookIds.Remove(userBookId);
        }

        public int IndexOf(string userBookId)
        {
            return BookIds.IndexOf(userBookId);
        }
    }
}

