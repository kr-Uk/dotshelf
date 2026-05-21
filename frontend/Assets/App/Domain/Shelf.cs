using System;

namespace App.Domain
{
    public enum BookshelfStatus
    {
        Active,
        Removed
    }

    public sealed class Shelf
    {
        public const int RowCount = 4;
        public const int ColumnCount = 4;

        public string Id { get; }
        public string ShelfId { get; }
        public string SpaceId { get; }
        public string BodyInventoryId { get; }
        public string SkinInventoryId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public VisibilityScope VisibilityScope { get; set; } = VisibilityScope.Public;
        public int DisplayOrder { get; set; }
        public int OrderIndex
        {
            get => DisplayOrder;
            set => DisplayOrder = value;
        }
        public BookshelfStatus Status { get; set; } = BookshelfStatus.Active;
        public DateTimeOffset CreatedAt { get; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset? DeletedAt { get; set; }
        public ShelfSection[,] Sections { get; }

        public Shelf(string shelfId, int orderIndex)
            : this(shelfId, "space-me", "body-default", shelfId, orderIndex)
        {
        }

        public Shelf(string id, string spaceId, string bodyInventoryId, string name, int displayOrder, DateTimeOffset? createdAt = null, DateTimeOffset? updatedAt = null)
        {
            Id = id;
            ShelfId = id;
            SpaceId = spaceId;
            BodyInventoryId = bodyInventoryId;
            Name = name;
            DisplayOrder = displayOrder;
            CreatedAt = createdAt ?? DateTimeOffset.MinValue;
            UpdatedAt = updatedAt ?? DateTimeOffset.MinValue;
            Sections = new ShelfSection[RowCount, ColumnCount];

            for (var row = 0; row < RowCount; row++)
            {
                for (var col = 0; col < ColumnCount; col++)
                {
                    var sectionId = $"{ShelfId}-section-{row}-{col}";
                    Sections[row, col] = new ShelfSection(sectionId, Id, row, col, 3);
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

