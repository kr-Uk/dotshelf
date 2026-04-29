using System;
using App.Domain.Dashboard;

namespace App.Application.Dashboard
{
    public interface IGuestbookRepository
    {
        void LoadPage(
            long spaceId,
            string cursor,
            int limit,
            Action<GuestbookListPage> onSuccess,
            Action<DashboardApiError> onError);

        void Write(
            WriteGuestbookCommand command,
            Action<Guestbook> onSuccess,
            Action<DashboardApiError> onError);

        void WriteReply(
            WriteGuestbookReplyCommand command,
            Action<GuestbookReply> onSuccess,
            Action<DashboardApiError> onError);

        void Delete(
            long guestbookId,
            Action onSuccess,
            Action<DashboardApiError> onError);

        void DeleteReply(
            long guestbookId,
            long replyId,
            Action onSuccess,
            Action<DashboardApiError> onError);

        void Report(
            ReportGuestbookCommand command,
            Action onSuccess,
            Action<DashboardApiError> onError);
    }
}
