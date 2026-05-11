using System.Collections.Generic;
using UnityEngine;

namespace BookshelfPorting.Runtime
{
    public enum ShelfSectionAlignment
    {
        Left,
        Center,
        Right
    }

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
        public float frontBoundZ;
        public float backBoundZ;
        public ShelfSectionAlignment alignment = ShelfSectionAlignment.Left;
        public bool isAdvertisementSection;
        public readonly List<BookEntity> books = new List<BookEntity>();

        public float Width => rightBound - leftBound;
        public float Depth => backBoundZ - frontBoundZ;
        public Vector3 Center => new Vector3((leftBound + rightBound) * 0.5f, shelfTopY + sectionHeight * 0.5f, frontZ);

        public ShelfSection(
            int row,
            int column,
            float leftBound,
            float rightBound,
            float shelfTopY,
            float sectionHeight,
            float frontZ,
            float frontBoundZ,
            float backBoundZ)
        {
            this.row = row;
            this.column = column;
            this.leftBound = leftBound;
            this.rightBound = rightBound;
            this.shelfTopY = shelfTopY;
            this.sectionHeight = sectionHeight;
            this.frontZ = frontZ;
            this.frontBoundZ = frontBoundZ;
            this.backBoundZ = backBoundZ;
        }

        public bool CanFit(BookEntity book, float padding)
        {
            if (isAdvertisementSection || book == null)
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

        public float GetFrontAlignedBookCenterZ(BookEntity book, float scale, float inset)
        {
            if (book == null || Depth <= 0f)
            {
                return frontZ;
            }

            var safeInset = Mathf.Max(0f, inset);
            var depthExtent = BookFootprintUtility.GetDepthExtent(book.thickness, book.depth, book.orientation) * Mathf.Max(0f, scale);
            var frontSafeCenter = frontBoundZ + safeInset + depthExtent * 0.5f;
            var backSafeCenter = backBoundZ - safeInset - depthExtent * 0.5f;
            if (frontSafeCenter > backSafeCenter)
            {
                return (frontBoundZ + backBoundZ) * 0.5f;
            }

            return frontSafeCenter;
        }
    }
}
