using System;

namespace App.Application.Dashboard
{
    public sealed class ToggleSpaceLikeUseCase
    {
        private readonly IDashboardRepository repository;

        public ToggleSpaceLikeUseCase(IDashboardRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void Execute(
            long spaceId,
            bool liked,
            Action<LikeToggleResult> onSuccess,
            Action<DashboardApiError> onError)
        {
            repository.ToggleSpaceLike(spaceId, liked, onSuccess, onError);
        }
    }
}
