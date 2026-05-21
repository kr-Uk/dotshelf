using System;

namespace App.Application.Dashboard
{
    public sealed class LoadOwnedBooksPageUseCase
    {
        public const int DefaultLimit = 20;

        private readonly ILibraryViewRepository repository;

        public LoadOwnedBooksPageUseCase(ILibraryViewRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void Execute(
            long spaceId,
            OwnedBookSort sort,
            string cursor,
            Action<OwnedBookListPage> onSuccess,
            Action<DashboardApiError> onError)
        {
            repository.LoadOwnedBooks(spaceId, sort, cursor, DefaultLimit, onSuccess, onError);
        }
    }
}
