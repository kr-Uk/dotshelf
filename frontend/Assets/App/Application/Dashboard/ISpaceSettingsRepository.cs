using System;
using App.Domain.Dashboard;

namespace App.Application.Dashboard
{
    public interface ISpaceSettingsRepository
    {
        void UpdateIntroduction(
            string introduction,
            Action onSuccess,
            Action<DashboardApiError> onError);

        void UpdateGuestbookVisibility(
            VisibilityScope scope,
            Action onSuccess,
            Action<DashboardApiError> onError);

        void LoadGuestbookPostItWhitelist(
            Action<GuestbookPostItWhitelist> onSuccess,
            Action<DashboardApiError> onError);

        void UpdateGuestbookPostItWhitelist(
            GuestbookPostItWhitelist whitelist,
            Action onSuccess,
            Action<DashboardApiError> onError);

        void UpdateCurrentTheme(
            long? themeInventoryId,
            Action onSuccess,
            Action<DashboardApiError> onError);
    }
}
