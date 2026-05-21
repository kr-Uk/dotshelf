using System;
using System.Collections.Generic;
using App.Application.Dashboard;
using App.Domain.Dashboard;

namespace App.Infrastructure.Mock.Dashboard
{
    internal static class MockSpaceCustomizationState
    {
        public static long? CurrentThemeInventoryId;
        public static long? CurrentWallpaperInventoryId;
        public static long? CurrentFloorInventoryId;
        public static long? CurrentDeskInventoryId = 200030L;
        public static long? CurrentBookshelfSkinInventoryId;
        public static long? CurrentDashboardInventoryId;
        public static long? CurrentNotebookInventoryId;
        public static long? CurrentInteriorLightInventoryId;
        public static long? CurrentExteriorLightInventoryId;

        public static void ResetCategoryInventories()
        {
            CurrentWallpaperInventoryId = null;
            CurrentFloorInventoryId = null;
            CurrentDeskInventoryId = null;
            CurrentBookshelfSkinInventoryId = null;
            CurrentDashboardInventoryId = null;
            CurrentNotebookInventoryId = null;
            CurrentInteriorLightInventoryId = null;
            CurrentExteriorLightInventoryId = null;
        }
    }

    public sealed class MockSpaceSettingsRepository : ISpaceSettingsRepository
    {
        private string introduction = "오늘도 한 페이지";
        private VisibilityScope guestbookVisibility = VisibilityScope.Public;
        private GuestbookPostItWhitelist whitelist = new GuestbookPostItWhitelist(true, null);

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
            MockSpaceCustomizationState.CurrentThemeInventoryId = themeInventoryId;
            onSuccess();
        }

        // 카테고리별 적용 6 + 전체 테마(mode) — Mock은 로컬 필드에 저장만 하고 themeReset=false로 응답.
        // 실제 자동 reset 로직(allowsCustomization 검사)은 백엔드 책임이라 Mock에서는 시뮬레이션 안 함.
        // (UI 작업자가 백엔드 없이 Mock 모드로 Play 시 카테고리 클릭 → visual swap만 동작 확인 가능)
        public void ChangeWallpaper(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            MockSpaceCustomizationState.CurrentWallpaperInventoryId = inventoryId; onSuccess?.Invoke(false);
        }
        public void ChangeFloor(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            MockSpaceCustomizationState.CurrentFloorInventoryId = inventoryId; onSuccess?.Invoke(false);
        }
        public void ChangeDesk(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            MockSpaceCustomizationState.CurrentDeskInventoryId = inventoryId; onSuccess?.Invoke(false);
        }
        public void ChangeBookshelfSkin(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            MockSpaceCustomizationState.CurrentBookshelfSkinInventoryId = inventoryId; onSuccess?.Invoke(false);
        }
        public void ChangeDashboard(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            MockSpaceCustomizationState.CurrentDashboardInventoryId = inventoryId; onSuccess?.Invoke(false);
        }
        public void ChangeNotebook(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            MockSpaceCustomizationState.CurrentNotebookInventoryId = inventoryId; onSuccess?.Invoke(false);
        }
        public void ChangeInteriorLight(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            MockSpaceCustomizationState.CurrentInteriorLightInventoryId = inventoryId; onSuccess?.Invoke(false);
        }
        public void ChangeExteriorLight(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            MockSpaceCustomizationState.CurrentExteriorLightInventoryId = inventoryId; onSuccess?.Invoke(false);
        }
        public void ApplyThemeWithMode(long inventoryId, ThemeApplyMode mode, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            MockSpaceCustomizationState.CurrentThemeInventoryId = inventoryId;
            // RESET 모드는 모든 카테고리 ID 초기화 시뮬레이션 + themeReset=true
            if (mode == ThemeApplyMode.Reset)
            {
                MockSpaceCustomizationState.ResetCategoryInventories();
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
        public long? CurrentThemeInventoryId => MockSpaceCustomizationState.CurrentThemeInventoryId;
        public long? CurrentDeskInventoryId => MockSpaceCustomizationState.CurrentDeskInventoryId;
        public long? CurrentBookshelfSkinInventoryId => MockSpaceCustomizationState.CurrentBookshelfSkinInventoryId;
    }
}
