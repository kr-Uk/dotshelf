using System.Collections.Generic;

namespace App.Domain
{
    public sealed class ShelfSection
    {
        public int Row { get; }
        public int Col { get; }
        public List<string> BookIds { get; } = new List<string>();

        public ShelfSection(int row, int col)
        {
            Row = row;
            Col = col;
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

