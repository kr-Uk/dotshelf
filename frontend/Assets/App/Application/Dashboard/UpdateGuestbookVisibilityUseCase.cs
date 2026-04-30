using System;
using App.Domain.Dashboard;

namespace App.Application.Dashboard
{
    public sealed class UpdateGuestbookVisibilityUseCase
    {
        private readonly ISpaceSettingsRepository repository;

        public UpdateGuestbookVisibilityUseCase(ISpaceSettingsRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void Execute(
            VisibilityScope scope,
            Action onSuccess,
            Action<DashboardApiError> onError)
        {
            repository.UpdateGuestbookVisibility(scope, onSuccess, onError);
        }
    }
}
