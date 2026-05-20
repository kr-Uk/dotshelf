using System;

namespace App.Domain
{
    public enum ShelfItemType
    {
        Book,
        Decor
    }

    public enum FacingDirection
    {
        Front,
        Back,
        Left,
        Right
    }

    public enum DisplayStyle
    {
        Spine,
        Front,
        Angled45
    }

    public enum ShelfItemStatus
    {
        Active,
        Removed
    }

    public sealed class BookPlacement
    {
        public string Id { get; }
        public string PlacementId => Id;
        public string BookshelfId { get; }
        public string ShelfId => BookshelfId;
        public string BookshelfSectionId { get; set; }
        public ShelfItemType ItemType { get; set; } = ShelfItemType.Book;
        public string UserBookId { get; set; }
        public string AssetInventoryId { get; set; }
        public int? SlotNo { get; set; }
        public FacingDirection FacingDirection { get; set; } = FacingDirection.Front;
        public DisplayStyle DisplayStyle { get; set; } = DisplayStyle.Spine;
        public decimal RotationDeg { get; set; }
        public decimal? PosX { get; set; }
        public decimal? PosY { get; set; }
        public decimal? PosZ { get; set; }
        public int BookshelfIndex { get; set; }
        public int CellIndex { get; set; }
        public decimal? OffsetXInCell { get; set; }
        public string SizePresetId { get; set; }
        public decimal ScaleX { get; set; } = 1m;
        public decimal ScaleY { get; set; } = 1m;
        public ShelfItemStatus Status { get; set; } = ShelfItemStatus.Active;
        public int SectionRow { get; set; }
        public int SectionCol { get; set; }
        public int SlotIndex
        {
            get => SlotNo.HasValue ? Math.Max(0, SlotNo.Value - 1) : 0;
            set => SlotNo = Math.Max(0, value) + 1;
        }
        public DateTimeOffset CreatedAt { get; }
        public DateTimeOffset UpdatedAt { get; set; }

        public BookPlacement(
            string id,
            string bookshelfId,
            string bookshelfSectionId,
            string userBookId,
            int row,
            int col,
            int slotNo,
            DateTimeOffset? createdAt = null,
            DateTimeOffset? updatedAt = null)
        {
            Id = id;
            BookshelfId = bookshelfId;
            BookshelfSectionId = bookshelfSectionId;
            UserBookId = userBookId;
            SectionRow = row;
            SectionCol = col;
            SlotNo = slotNo;
            CreatedAt = createdAt ?? DateTimeOffset.MinValue;
            UpdatedAt = updatedAt ?? DateTimeOffset.MinValue;
        }

        public BookPlacement Clone()
        {
            return new BookPlacement(Id, BookshelfId, BookshelfSectionId, UserBookId, SectionRow, SectionCol, SlotNo ?? 1, CreatedAt, UpdatedAt)
            {
                ItemType = ItemType,
                AssetInventoryId = AssetInventoryId,
                FacingDirection = FacingDirection,
                DisplayStyle = DisplayStyle,
                RotationDeg = RotationDeg,
                PosX = PosX,
                PosY = PosY,
                PosZ = PosZ,
                BookshelfIndex = BookshelfIndex,
                CellIndex = CellIndex,
                OffsetXInCell = OffsetXInCell,
                SizePresetId = SizePresetId,
                ScaleX = ScaleX,
                ScaleY = ScaleY,
                Status = Status
            };
        }
    }
}

