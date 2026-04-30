using System;
using App.Application.Dashboard;

namespace App.Infrastructure.Api.Dashboard
{
    /// <summary>
    /// 대시보드 \"보유 책 풀 목록\"·\"독후감 풀 목록\" 모달의 BE 어댑터.
    /// v1.0 갱신에서 BE 엔드포인트 합류 (/v1/spaces/{spaceId}/books, /v1/users/{userId}/reading-records sort/cursor/limit).
    /// </summary>
    public sealed class ApiLibraryViewRepository : ILibraryViewRepository
    {
        private readonly NotebookApiClient client;

        public ApiLibraryViewRepository(NotebookApiClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public void LoadOwnedBooks(long spaceId, OwnedBookSort sort, string cursor, int limit, Action<OwnedBookListPage> onSuccess, Action<DashboardApiError> onError)
        {
            string sortString = DashboardDtoMapper.FormatOwnedBookSort(sort);
            client.GetSpaceOwnedBooks(
                spaceId,
                sortString,
                cursor,
                limit,
                dto => onSuccess?.Invoke(DashboardDtoMapper.MapOwnedBookPage(dto)),
                err => onError?.Invoke(DashboardDtoMapper.MapError(err)));
        }

        public void LoadReadingRecords(long userId, ReadingRecordSort sort, string cursor, int limit, Action<ReadingRecordListPage> onSuccess, Action<DashboardApiError> onError)
        {
            string sortString = DashboardDtoMapper.FormatReadingRecordSort(sort);
            client.GetUserReadingRecords(
                userId,
                sortString,
                cursor,
                limit,
                dto => onSuccess?.Invoke(DashboardDtoMapper.MapReadingRecordPage(dto)),
                err => onError?.Invoke(DashboardDtoMapper.MapError(err)));
        }
    }
}
