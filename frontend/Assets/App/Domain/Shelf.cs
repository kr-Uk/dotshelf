namespace App.Domain
{
    public sealed class Shelf
    {
        public const int RowCount = 4;
        public const int ColumnCount = 4;

        public string ShelfId { get; }
        public int OrderIndex { get; set; }
        public ShelfSection[,] Sections { get; }

        public Shelf(string shelfId, int orderIndex)
        {
            ShelfId = shelfId;
            OrderIndex = orderIndex;
            Sections = new ShelfSection[RowCount, ColumnCount];

            for (var row = 0; row < RowCount; row++)
            {
                for (var col = 0; col < ColumnCount; col++)
                {
                    Sections[row, col] = new ShelfSection(row, col);
                }
            }
        }

        public ShelfSection GetSection(int row, int col)
        {
            if (row < 0 || row >= RowCount || col < 0 || col >= ColumnCount)
            {
                return null;
            }

            return Sections[row, col];
        }
    }
}

