using System.Collections.Generic;

namespace App.Application.Books
{
    public sealed class LoadMyBookshelfBooksUseCase
    {
        private readonly IBookCatalogRepository repository;

        public LoadMyBookshelfBooksUseCase(IBookCatalogRepository repository)
        {
            this.repository = repository;
        }

        public IReadOnlyList<BookCatalogItem> Execute()
        {
            return repository.GetMyBookshelfBooks();
        }
    }
}
