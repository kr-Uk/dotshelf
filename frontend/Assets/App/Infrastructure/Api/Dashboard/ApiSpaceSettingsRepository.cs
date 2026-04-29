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
            UpdateProfileRequestDto request = new UpdateProfileRequestDto
            {
                Introduction = introduction ?? string.Empty
            };

            client.UpdateMe(
                request,
                onSuccess: _ => onSuccess?.Invoke(),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
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
    }
}
