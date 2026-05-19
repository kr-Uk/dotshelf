using System;
using App.Application.Dashboard;
using App.Domain.Dashboard;
using UnityEngine;

namespace App.Infrastructure.Api.Dashboard
{
    public sealed class ApiDashboardRepository : IDashboardRepository
    {

        private readonly NotebookApiClient client;

        public ApiDashboardRepository(NotebookApiClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public void LoadDashboard(long spaceId, Action<DashboardSnapshot> onSuccess, Action<DashboardApiError> onError)
        {
            if (spaceId <= 0L)
            {
                Debug.LogWarning($"[Dashboard][ApiRepo] LoadDashboard skip — invalid spaceId={spaceId}.");
                onError?.Invoke(new DashboardApiError { Code = "INVALID_INPUT", Message = "잘못된 공간 정보입니다." });
                return;
            }

            FetchDashboard(spaceId, onSuccess, onError);
        }

        public void LoadMyDashboard(Action<DashboardSnapshot> onSuccess, Action<DashboardApiError> onError)
        {
            client.GetMySpace(
                onSuccess: spaceDto =>
                {
                    if (spaceDto == null)
                    {
                        onError?.Invoke(new DashboardApiError { Code = "SPACE_NOT_FOUND", Message = "내 공간을 찾을 수 없습니다." });
                        return;
                    }

                    App.Core.AppLog.Dashboard($"[Dashboard][ApiRepo] LoadMyDashboard resolved /v1/spaces/me → id={spaceDto.Id}. fetching dashboard.");
                    FetchDashboard(spaceDto.Id, onSuccess, onError);
                },
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        private void FetchDashboard(long spaceId, Action<DashboardSnapshot> onSuccess, Action<DashboardApiError> onError)
        {
            App.Core.AppLog.Dashboard($"[Dashboard][ApiRepo] GET /v1/spaces/{spaceId}/dashboard?period=weekly");
            client.GetSpaceDashboard(
                spaceId,
                period: "weekly",
                onSuccess: dto => onSuccess?.Invoke(DashboardDtoMapper.MapSnapshot(dto)),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        public void ToggleSpaceLike(long spaceId, bool liked, Action<LikeToggleResult> onSuccess, Action<DashboardApiError> onError)
        {
            if (liked)
            {
                client.LikeSpace(
                    spaceId,
                    onSuccess: dto => onSuccess?.Invoke(new LikeToggleResult { Liked = dto != null && dto.Active, LikeCount = dto?.Count ?? 0 }),
                    onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
                return;
            }

            client.UnlikeSpace(
                spaceId,
                onSuccess: dto => onSuccess?.Invoke(new LikeToggleResult { Liked = dto != null && dto.Active, LikeCount = dto?.Count ?? 0 }),
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }
    }
}
