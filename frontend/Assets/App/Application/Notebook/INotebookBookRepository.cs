using System;

namespace App.Application.Notebook
{
    public interface INotebookBookRepository
    {
        void SearchBooks(
            string keyword,
            int page,
            int size,
            Action<NotebookBookSearchResult> onSuccess,
            Action<NotebookApiError> onError);

        void AddBookToShelf(
            long bookshelfId,
            NotebookBookAddRequest request,
            Action<NotebookBookAddResult> onSuccess,
            Action<NotebookApiError> onError);
    }
}
