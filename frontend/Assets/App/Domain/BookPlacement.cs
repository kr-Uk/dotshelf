namespace App.Domain
{
    public sealed class BookPlacement
    {
        public string PlacementId { get; }
        public string UserBookId { get; }
        public string ShelfId { get; set; }
        public int SectionRow { get; set; }
        public int SectionCol { get; set; }
        public int SlotIndex { get; set; }

        public BookPlacement(string placementId, string userBookId, string shelfId, int row, int col, int slotIndex)
        {
            PlacementId = placementId;
            UserBookId = userBookId;
            ShelfId = shelfId;
            SectionRow = row;
            SectionCol = col;
            SlotIndex = slotIndex;
        }

        public BookPlacement Clone()
        {
            return new BookPlacement(PlacementId, UserBookId, ShelfId, SectionRow, SectionCol, SlotIndex);
        }
    }
}

