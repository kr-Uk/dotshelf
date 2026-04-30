using System;
using App.Domain.Dashboard;

namespace App.Application.Dashboard
{
    public sealed class WriteGuestbookUseCase
    {
        public const int MaxContentLength = 30;

        private readonly IGuestbookRepository repository;

        public WriteGuestbookUseCase(IGuestbookRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void Execute(
            WriteGuestbookCommand command,
            Action<Guestbook> onSuccess,
            Action<DashboardApiError> onError)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (string.IsNullOrWhiteSpace(command.Content))
            {
                onError?.Invoke(new DashboardApiError { Code = "GUESTBOOK_CONTENT_BLANK", Message = "방명록 내용을 입력하세요." });
                return;
            }

            if (command.Content.Length > MaxContentLength)
            {
                onError?.Invoke(new DashboardApiError { Code = "GUESTBOOK_CONTENT_TOO_LONG", Message = $"방명록은 {MaxContentLength}자 이하로 작성하세요." });
                return;
            }

            if (command.PostItInventoryId <= 0)
            {
                onError?.Invoke(new DashboardApiError { Code = "GUESTBOOK_POSTIT_REQUIRED", Message = "포스트잇 스타일을 선택하세요." });
                return;
            }

            repository.Write(command, onSuccess, onError);
        }
    }
}
