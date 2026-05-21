using System;

namespace App.Application.Dashboard
{
    public sealed class LoadReadingRecordsPageUseCase
    {
        public const int DefaultLimit = 20;

        private readonly ILibraryViewRepository repository;

        public LoadReadingRecordsPageUseCase(ILibraryViewRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void Execute(
            long userId,
            ReadingRecordSort sort,
            string cursor,
            Action<ReadingRecordListPage> onSuccess,
            Action<DashboardApiError> onError)
        {
            repository.LoadReadingRecords(userId, sort, cursor, DefaultLimit, onSuccess, onError);
        }
    }
}
