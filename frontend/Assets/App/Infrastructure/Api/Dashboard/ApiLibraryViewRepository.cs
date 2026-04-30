using System;
using App.Application.Dashboard;

namespace App.Infrastructure.Api.Dashboard
{
    /// <summary>
    /// 백엔드 정렬 파라미터(추가날짜/가나다/좋아요/조회수/댓글수)가 합류되기 전까지의 임시 어댑터.
    /// 클라이언트에서 정렬·페이징을 직접 적용하거나 백엔드 합류 후 쿼리 파라미터로 위임한다.
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
            // TODO: 백엔드에 보유 책 목록(/v1/spaces/{spaceId}/books?sort=&cursor=&limit=) 합류 후 매핑.
            //       현재는 빈 페이지 반환. UI는 mock 모드에서 작업 가능.
            onSuccess?.Invoke(new OwnedBookListPage(System.Array.Empty<OwnedBookItem>(), null));
        }

        public void LoadReadingRecords(long userId, ReadingRecordSort sort, string cursor, int limit, Action<ReadingRecordListPage> onSuccess, Action<DashboardApiError> onError)
        {
            // TODO: 기존 GetUserReadingRecords에 sort/cursor 파라미터 합류 후 매핑.
            onSuccess?.Invoke(new ReadingRecordListPage(System.Array.Empty<ReadingRecordItem>(), null));
        }
    }
}
