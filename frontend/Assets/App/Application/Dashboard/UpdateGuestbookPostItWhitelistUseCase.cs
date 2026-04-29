using System;

namespace App.Application.Dashboard
{
    public sealed class UpdateGuestbookPostItWhitelistUseCase
    {
        private readonly ISpaceSettingsRepository repository;

        public UpdateGuestbookPostItWhitelistUseCase(ISpaceSettingsRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void LoadCurrent(
            Action<GuestbookPostItWhitelist> onSuccess,
            Action<DashboardApiError> onError)
        {
            repository.LoadGuestbookPostItWhitelist(onSuccess, onError);
        }

        public void Execute(
            GuestbookPostItWhitelist whitelist,
            Action onSuccess,
            Action<DashboardApiError> onError)
        {
            if (whitelist == null)
            {
                throw new ArgumentNullException(nameof(whitelist));
            }

            repository.UpdateGuestbookPostItWhitelist(whitelist, onSuccess, onError);
        }
    }
}
