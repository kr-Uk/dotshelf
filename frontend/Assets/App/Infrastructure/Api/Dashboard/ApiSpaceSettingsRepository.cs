using System;
using System.Collections.Generic;
using App.Application.Dashboard;
using App.Domain.Dashboard;
using App.Infrastructure.Api.Dtos;

namespace App.Infrastructure.Api.Dashboard
{
    public sealed class ApiSpaceSettingsRepository : ISpaceSettingsRepository
    {
        private readonly NotebookApiClient client;

        public ApiSpaceSettingsRepository(NotebookApiClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public void UpdateIntroduction(string introduction, Action onSuccess, Action<DashboardApiError> onError)
        {
            client.GetMe(
                onSuccess: currentProfile =>
                {
                    UpdateProfileRequestDto request = new UpdateProfileRequestDto
                    {
                        Nickname = currentProfile.Nickname,
                        ProfileImageUrl = currentProfile.ProfileImageUrl,
                        Introduction = introduction ?? string.Empty
                    };
                    client.UpdateMe(
                        request,
                        onSuccess: _ => onSuccess?.Invoke(),
                        onError: err => onError?.Invoke(DashboardDtoMapper.MapError(err)));
                },
                onError: err => onError?.Invoke(DashboardDtoMapper.MapError(err)));
        }

        public void UpdateGuestbookVisibility(VisibilityScope scope, Action onSuccess, Action<DashboardApiError> onError)
        {
            VisibilityRequestDto request = new VisibilityRequestDto
            {
                VisibilityScope = DashboardDtoMapper.FormatVisibility(scope)
            };

            client.UpdateMyGuestbookVisibility(
                request,
                onSuccess: _ => onSuccess?.Invoke(),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        public void LoadGuestbookPostItWhitelist(Action<GuestbookPostItWhitelist> onSuccess, Action<DashboardApiError> onError)
        {
            client.GetGuestbookPostItWhitelist(
                onSuccess: dto => onSuccess?.Invoke(DashboardDtoMapper.MapWhitelist(dto)),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        public void UpdateGuestbookPostItWhitelist(GuestbookPostItWhitelist whitelist, Action onSuccess, Action<DashboardApiError> onError)
        {
            List<long> ids = new List<long>(whitelist.AllowedAssetIds);
            GuestbookPostItWhitelistRequestDto request = new GuestbookPostItWhitelistRequestDto
            {
                AllowAll = whitelist.AllowAll,
                AllowedAssetIds = ids
            };

            client.UpdateGuestbookPostItWhitelist(
                request,
                onSuccess: _ => onSuccess?.Invoke(),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        public void UpdateCurrentTheme(long? themeInventoryId, Action onSuccess, Action<DashboardApiError> onError)
        {
            ThemeApplyRequestDto request = new ThemeApplyRequestDto
            {
                InventoryId = themeInventoryId
            };

            client.ApplyMySpaceTheme(
                request,
                onSuccess: _ => onSuccess?.Invoke(),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        public void ChangeWallpaper(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            client.ChangeMyWallpaper(BuildCategoryRequest(inventoryId),
                onSuccess: dto => onSuccess?.Invoke(dto?.ThemeReset ?? false),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        public void ChangeFloor(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            client.ChangeMyFloor(BuildCategoryRequest(inventoryId),
                onSuccess: dto => onSuccess?.Invoke(dto?.ThemeReset ?? false),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        public void ChangeDesk(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            client.ChangeMyDesk(BuildCategoryRequest(inventoryId),
                onSuccess: dto => onSuccess?.Invoke(dto?.ThemeReset ?? false),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        public void ChangeDashboard(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            client.ChangeMyDashboard(BuildCategoryRequest(inventoryId),
                onSuccess: dto => onSuccess?.Invoke(dto?.ThemeReset ?? false),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        public void ChangeNotebook(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            client.ChangeMyNotebook(BuildCategoryRequest(inventoryId),
                onSuccess: dto => onSuccess?.Invoke(dto?.ThemeReset ?? false),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        public void ChangeInteriorLight(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            client.ChangeMyInteriorLight(BuildCategoryRequest(inventoryId),
                onSuccess: dto => onSuccess?.Invoke(dto?.ThemeReset ?? false),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        public void ChangeExteriorLight(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            client.ChangeMyExteriorLight(BuildCategoryRequest(inventoryId),
                onSuccess: dto => onSuccess?.Invoke(dto?.ThemeReset ?? false),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        public void ApplyThemeWithMode(long inventoryId, ThemeApplyMode mode, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            // Short-circuit KEEP_BLOCKERS_ONLY on the FE: backend currently
            // returns 501 NOT_IMPLEMENTED (Layout 도메인 통합 후 활성화 예정).
            // Skipping the round-trip means callers see a stable error code
            // regardless of when the BE gate flips, and we do not advertise a
            // mode the server cannot fulfill. Remove this branch when BE
            // implements KEEP_BLOCKERS_ONLY. Message lookup is kept in
            // DashboardErrorMessages.Format so callers get the same Korean
            // string regardless of whether this short-circuit fires or BE
            // returns the same code from server-side. Error is left null —
            // it mirrors raw BE response.error which has no client-side
            // analogue here.
            if (mode == ThemeApplyMode.KeepBlockersOnly)
            {
                onError?.Invoke(new DashboardApiError
                {
                    Code = "THEME_MODE_NOT_IMPLEMENTED",
                    Message = "이 모드는 아직 지원되지 않습니다."
                });
                return;
            }
            // Default-throw on unknown modes so a future ThemeApplyMode value
            // cannot silently fall through as RESET. KEEP_BLOCKERS_ONLY is
            // gated above; only Reset reaches this switch today.
            string modeStr = mode switch
            {
                ThemeApplyMode.Reset => "RESET",
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported ThemeApplyMode")
            };
            client.ApplyMySpaceThemeWithMode(inventoryId, modeStr,
                onSuccess: dto => onSuccess?.Invoke(dto?.ThemeReset ?? false),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        private static CategoryInventoryRequestDto BuildCategoryRequest(long? inventoryId)
        {
            return new CategoryInventoryRequestDto { InventoryId = inventoryId };
        }
    }
}
