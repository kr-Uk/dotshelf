using System;

namespace App.Application.Notebook
{
    public sealed class AddNotebookBookToShelfUseCase
    {
        private readonly INotebookBookRepository repository;

        public AddNotebookBookToShelfUseCase(INotebookBookRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void Execute(
            long bookshelfId,
            AddNotebookBookToShelfCommand command,
            NotebookShelfPlacement placement,
            Action<NotebookBookAddResult> onSuccess,
            Action<NotebookApiError> onError)
        {
            repository.AddBookToShelf(bookshelfId, command, placement, onSuccess, onError);
        }
    }
}
