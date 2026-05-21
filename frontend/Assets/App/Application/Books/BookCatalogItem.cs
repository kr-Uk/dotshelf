using App.Domain;

namespace App.Application.Books
{
    public sealed class BookCatalogItem
    {
        public BookCatalogItem(Book book, UserBook userBook, BookPlacement placement, ReadingRecord readingRecord)
        {
            Book = book;
            UserBook = userBook;
            Placement = placement;
            ReadingRecord = readingRecord;
        }

        public Book Book { get; }
        public UserBook UserBook { get; }
        public BookPlacement Placement { get; }
        public ReadingRecord ReadingRecord { get; }
    }
}
