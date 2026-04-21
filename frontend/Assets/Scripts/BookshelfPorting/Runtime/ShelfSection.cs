using System.Collections.Generic;
using UnityEngine;

namespace BookshelfPorting.Runtime
{
    [System.Serializable]
    public class ShelfSection
    {
        public int row;
        public int column;
        public float leftBound;
        public float rightBound;
        public float shelfTopY;
        public float sectionHeight;
        public float frontZ;
        public readonly List<BookEntity> books = new List<BookEntity>();

        public float Width => rightBound - leftBound;
        public Vector3 Center => new Vector3((leftBound + rightBound) * 0.5f, shelfTopY + sectionHeight * 0.5f, frontZ);

        public ShelfSection(int row, int column, float leftBound, float rightBound, float shelfTopY, float sectionHeight, float frontZ)
        {
            this.row = row;
            this.column = column;
            this.leftBound = leftBound;
            this.rightBound = rightBound;
            this.shelfTopY = shelfTopY;
            this.sectionHeight = sectionHeight;
            this.frontZ = frontZ;
        }

        public bool CanFit(BookEntity book, float padding)
        {
            if (book == null)
            {
                return false;
            }

            var required = padding;
            for (var i = books.Count - 1; i >= 0; i--)
            {
                if (books[i] == null)
                {
                    books.RemoveAt(i);
                    continue;
                }

                required += books[i].GetFootprint() + padding;
            }

            required += book.GetFootprint();
            return required <= Width;
        }
    }
}
