using System;

namespace App.Application.Dashboard
{
    public sealed class UpdateIntroductionUseCase
    {
        public const int MaxLength = 255;

        private readonly ISpaceSettingsRepository repository;

        public UpdateIntroductionUseCase(ISpaceSettingsRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void Execute(
            string introduction,
            Action onSuccess,
            Action<DashboardApiError> onError)
        {
            string normalized = introduction ?? string.Empty;
            if (normalized.Length > MaxLength)
            {
                onError?.Invoke(new DashboardApiError { Code = "INTRODUCTION_TOO_LONG", Message = $"한 줄 소개는 {MaxLength}자 이하로 입력하세요." });
                return;
            }

            repository.UpdateIntroduction(normalized, onSuccess, onError);
        }
    }
}
