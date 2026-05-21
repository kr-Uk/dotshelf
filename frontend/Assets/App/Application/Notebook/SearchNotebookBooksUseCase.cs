using System;

namespace App.Application.Notebook
{
    public sealed class SearchNotebookBooksUseCase
    {
        private readonly INotebookBookRepository repository;

        public SearchNotebookBooksUseCase(INotebookBookRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void Execute(
            string keyword,
            int page,
            int size,
            Action<NotebookBookSearchResult> onSuccess,
            Action<NotebookApiError> onError)
        {
            repository.SearchBooks(keyword, page, size, onSuccess, onError);
        }
    }
}
