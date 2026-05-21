using System;

namespace App.Application.Dashboard
{
    public sealed class UpdateSpaceThemeUseCase
    {
        private readonly ISpaceSettingsRepository repository;

        public UpdateSpaceThemeUseCase(ISpaceSettingsRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void Execute(
            long? themeInventoryId,
            Action onSuccess,
            Action<DashboardApiError> onError)
        {
            repository.UpdateCurrentTheme(themeInventoryId, onSuccess, onError);
        }
    }
}
