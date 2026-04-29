using System;

namespace App.Application.Dashboard
{
    public sealed class DeleteGuestbookUseCase
    {
        private readonly IGuestbookRepository repository;

        public DeleteGuestbookUseCase(IGuestbookRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void Execute(
            long guestbookId,
            Action onSuccess,
            Action<DashboardApiError> onError)
        {
            repository.Delete(guestbookId, onSuccess, onError);
        }
    }
}
