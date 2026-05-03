using System;
using System.Collections.Generic;
using App.Application.Dashboard;
using App.Domain.Dashboard;

namespace App.Infrastructure.Mock.Dashboard
{
    public sealed class MockSpaceSettingsRepository : ISpaceSettingsRepository
    {
        private string introduction = "오늘도 한 페이지";
        private VisibilityScope guestbookVisibility = VisibilityScope.Public;
        private GuestbookPostItWhitelist whitelist = new GuestbookPostItWhitelist(true, null);
        private long? currentThemeInventoryId;

        public void UpdateIntroduction(string newIntroduction, Action onSuccess, Action<DashboardApiError> onError)
        {
            introduction = newIntroduction ?? string.Empty;
            onSuccess();
        }

        public void UpdateGuestbookVisibility(VisibilityScope scope, Action onSuccess, Action<DashboardApiError> onError)
        {
            guestbookVisibility = scope;
            onSuccess();
        }

        public void LoadGuestbookPostItWhitelist(Action<GuestbookPostItWhitelist> onSuccess, Action<DashboardApiError> onError)
        {
            onSuccess(whitelist);
        }

        public void UpdateGuestbookPostItWhitelist(GuestbookPostItWhitelist newWhitelist, Action onSuccess, Action<DashboardApiError> onError)
        {
            List<long> ids = new List<long>(newWhitelist.AllowedAssetIds);
            whitelist = new GuestbookPostItWhitelist(newWhitelist.AllowAll, ids);
            onSuccess();
        }

        public void UpdateCurrentTheme(long? themeInventoryId, Action onSuccess, Action<DashboardApiError> onError)
        {
            currentThemeInventoryId = themeInventoryId;
            onSuccess();
        }

        public string CurrentIntroduction => introduction;
        public VisibilityScope CurrentGuestbookVisibility => guestbookVisibility;
        public long? CurrentThemeInventoryId => currentThemeInventoryId;
    }
}
