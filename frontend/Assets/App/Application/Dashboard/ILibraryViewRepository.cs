using System;

namespace App.Application.Dashboard
{
    public interface ILibraryViewRepository
    {
        void LoadOwnedBooks(
            long spaceId,
            OwnedBookSort sort,
            string cursor,
            int limit,
            Action<OwnedBookListPage> onSuccess,
            Action<DashboardApiError> onError);

        void LoadReadingRecords(
            long userId,
            ReadingRecordSort sort,
            string cursor,
            int limit,
            Action<ReadingRecordListPage> onSuccess,
            Action<DashboardApiError> onError);
    }
}
