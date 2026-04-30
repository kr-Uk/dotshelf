using System;

namespace App.Application.Dashboard
{
    public sealed class ReportGuestbookUseCase
    {
        private readonly IGuestbookRepository repository;

        public ReportGuestbookUseCase(IGuestbookRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void Execute(
            ReportGuestbookCommand command,
            Action onSuccess,
            Action<DashboardApiError> onError)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (string.IsNullOrWhiteSpace(command.ReasonType))
            {
                onError?.Invoke(new DashboardApiError { Code = "REPORT_REASON_REQUIRED", Message = "신고 사유를 선택하세요." });
                return;
            }

            repository.Report(command, onSuccess, onError);
        }
    }
}
