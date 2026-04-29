using System;

namespace App.Application.Dashboard
{
    public sealed class LoadGuestbookPageUseCase
    {
        public const int DefaultLimit = 15;

        private readonly IGuestbookRepository repository;

        public LoadGuestbookPageUseCase(IGuestbookRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void Execute(
            long spaceId,
            string cursor,
            Action<GuestbookListPage> onSuccess,
            Action<DashboardApiError> onError)
        {
            repository.LoadPage(spaceId, cursor, DefaultLimit, onSuccess, onError);
        }
    }
}
