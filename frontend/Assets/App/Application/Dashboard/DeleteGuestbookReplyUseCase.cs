using System;

namespace App.Application.Dashboard
{
    public sealed class DeleteGuestbookReplyUseCase
    {
        private readonly IGuestbookRepository repository;

        public DeleteGuestbookReplyUseCase(IGuestbookRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void Execute(
            long guestbookId,
            long replyId,
            Action onSuccess,
            Action<DashboardApiError> onError)
        {
            repository.DeleteReply(guestbookId, replyId, onSuccess, onError);
        }
    }
}
