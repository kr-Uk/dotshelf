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
            // Bottom-of-stack 방어 — controller 가드를 우회한 호출이 들어와도 여기서 한 번 더 잡힌다.
            // /v1/spaces/1/dashboard 는 dev/test BE 의 알려진 stale 패턴이라 explicit-id 경로에서만 차단한다
            // (LoadMyDashboard 는 /v1/spaces/me 응답이 1 이어도 source-of-truth 이므로 이 메서드를 거치지 않게
            // FetchDashboard 로 우회 — 본인 공간은 차단 X.)
            if (spaceId <= 0L)
            {
                Debug.LogWarning($"[Dashboard][ApiRepo] LoadDashboard skip — invalid spaceId={spaceId}.");
                onError?.Invoke(new DashboardApiError { Code = "INVALID_INPUT", Message = "잘못된 공간 정보입니다." });
                return;
            }
            if (spaceId == 1L)
            {
                Debug.LogWarning(
                    $"[Dashboard][ApiRepo] LoadDashboard stop-gap — spaceId=1 차단. " +
                    "이 호출이 ingress 어디서 흘러들어왔는지 [Dashboard][fetch] log 위쪽 stack/source 를 확인하세요.");
                onError?.Invoke(new DashboardApiError { Code = "SPACE_NOT_FOUND", Message = "유효하지 않은 공간 ID 입니다." });
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

                    // /v1/spaces/me 응답의 id 가 source of truth — id 가 1 이어도 차단하지 않는다.
                    // 따라서 LoadDashboard 의 stop-gap 가드를 우회해 FetchDashboard 직접 호출.
                    Debug.Log($"[Dashboard][ApiRepo] LoadMyDashboard resolved /v1/spaces/me → id={spaceDto.Id}. fetching dashboard.");
                    FetchDashboard(spaceDto.Id, onSuccess, onError);
                },
                onError: error => onError?.Invoke(DashboardDtoMapper.MapError(error)));
        }

        // 실 HTTP fetch — LoadDashboard / LoadMyDashboard 가 각자 가드를 통과한 뒤 공통 진입점.
        private void FetchDashboard(long spaceId, Action<DashboardSnapshot> onSuccess, Action<DashboardApiError> onError)
        {
            Debug.Log($"[Dashboard][ApiRepo] GET /v1/spaces/{spaceId}/dashboard?period=weekly");
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
