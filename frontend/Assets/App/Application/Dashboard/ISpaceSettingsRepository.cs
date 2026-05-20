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

        // 카테고리별 개별 적용 6 메서드. 백엔드 PATCH /v1/spaces/me/{wallpaper,floor,...}.
        // onSuccess의 themeReset == true면 클라이언트가 룸 전체 reload (커스터마이징 불가
        // 테마 자동 해제). inventoryId가 null이면 unset(기본 외형).
        void ChangeWallpaper(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError);
        void ChangeFloor(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError);
        void ChangeDesk(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError);
        void ChangeBookshelfSkin(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError);
        void ChangeDashboard(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError);
        void ChangeNotebook(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError);
        void ChangeInteriorLight(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError);
        void ChangeExteriorLight(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError);

        // 전체 테마 적용(모드 명시). RESET = 모든 카테고리 + 책장 skin 초기화 후 테마 적용.
        // KEEP_BLOCKERS_ONLY = 현재 501 (Layout 도메인 통합 후 활성화 예정).
        void ApplyThemeWithMode(long inventoryId, ThemeApplyMode mode, Action<bool> onSuccess, Action<DashboardApiError> onError);
    }

    public enum ThemeApplyMode
    {
        Reset,
        KeepBlockersOnly
    }
}
