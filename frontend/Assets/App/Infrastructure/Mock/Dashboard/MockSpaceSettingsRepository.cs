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

        // 카테고리별 적용 6 + 전체 테마(mode) — Mock은 로컬 필드에 저장만 하고 themeReset=false로 응답.
        // 실제 자동 reset 로직(allowsCustomization 검사)은 백엔드 책임이라 Mock에서는 시뮬레이션 안 함.
        // (UI 작업자가 백엔드 없이 Mock 모드로 Play 시 카테고리 클릭 → visual swap만 동작 확인 가능)
        private long? wallpaperInvId;
        private long? floorInvId;
        private long? deskInvId;
        private long? dashboardInvId;
        private long? interiorLightInvId;
        private long? exteriorLightInvId;

        public void ChangeWallpaper(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            wallpaperInvId = inventoryId; onSuccess?.Invoke(false);
        }
        public void ChangeFloor(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            floorInvId = inventoryId; onSuccess?.Invoke(false);
        }
        public void ChangeDesk(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            deskInvId = inventoryId; onSuccess?.Invoke(false);
        }
        public void ChangeDashboard(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            dashboardInvId = inventoryId; onSuccess?.Invoke(false);
        }
        public void ChangeInteriorLight(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            interiorLightInvId = inventoryId; onSuccess?.Invoke(false);
        }
        public void ChangeExteriorLight(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            exteriorLightInvId = inventoryId; onSuccess?.Invoke(false);
        }
        public void ApplyThemeWithMode(long inventoryId, ThemeApplyMode mode, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            currentThemeInventoryId = inventoryId;
            // RESET 모드는 모든 카테고리 ID 초기화 시뮬레이션 + themeReset=true
            if (mode == ThemeApplyMode.Reset)
            {
                wallpaperInvId = floorInvId = deskInvId = dashboardInvId = interiorLightInvId = exteriorLightInvId = null;
                onSuccess?.Invoke(true);
            }
            else
            {
                // KEEP_BLOCKERS_ONLY는 백엔드에서 501 거절. Mock은 단순 적용으로 폴백.
                onSuccess?.Invoke(false);
            }
        }

        public string CurrentIntroduction => introduction;
        public VisibilityScope CurrentGuestbookVisibility => guestbookVisibility;
        public long? CurrentThemeInventoryId => currentThemeInventoryId;
    }
}
