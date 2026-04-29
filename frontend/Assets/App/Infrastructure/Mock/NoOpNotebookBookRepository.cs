using System;
using App.Application.Notebook;

namespace App.Infrastructure.Mock
{
    public sealed class NoOpNotebookBookRepository : INotebookBookRepository
    {
        private const string DisabledCode = "NOTEBOOK_API_DISABLED";
        private const string DisabledMessage = "Notebook API repository is disabled.";

        public void SearchBooks(
            string keyword,
            int page,
            int size,
            Action<NotebookBookSearchResult> onSuccess,
            Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        public void AddBookToShelf(
            long bookshelfId,
            AddNotebookBookToShelfCommand command,
            NotebookShelfPlacement placement,
            Action<NotebookBookAddResult> onSuccess,
            Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        private static NotebookApiError CreateDisabledError()
        {
            return new NotebookApiError
            {
                Code = DisabledCode,
                Message = DisabledMessage
            };
        }
    }
}
