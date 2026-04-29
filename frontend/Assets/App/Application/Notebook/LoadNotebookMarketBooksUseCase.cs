using System;

namespace App.Application.Notebook
{
    public sealed class LoadNotebookMarketBooksUseCase
    {
        private readonly INotebookBookRepository repository;

        public LoadNotebookMarketBooksUseCase(INotebookBookRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void Execute(
            string category,
            int limit,
            Action<NotebookBookSearchResult> onSuccess,
            Action<NotebookApiError> onError)
        {
            repository.LoadMarketBooks(category, limit, onSuccess, onError);
        }
    }
}
