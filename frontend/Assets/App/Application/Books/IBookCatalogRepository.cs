using System.Collections.Generic;

namespace App.Application.Books
{
    public interface IBookCatalogRepository
    {
        IReadOnlyList<BookCatalogItem> GetMyBookshelfBooks();
    }
}
