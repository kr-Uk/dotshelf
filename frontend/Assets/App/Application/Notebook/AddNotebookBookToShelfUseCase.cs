using System;

namespace App.Application.Notebook
{
    public sealed class AddNotebookBookToShelfUseCase
    {
        private readonly INotebookBookRepository repository;

        public AddNotebookBookToShelfUseCase(INotebookBookRepository repository)
        {
            this.repository = repository;
        }

        public void Execute(
            long bookshelfId,
            NotebookBookAddRequest request,
            Action<NotebookBookAddResult> onSuccess,
            Action<NotebookApiError> onError)
        {
            repository.AddBookToShelf(bookshelfId, request, onSuccess, onError);
        }
    }
}
