using System;

namespace App.Application.Dashboard
{
    public sealed class FollowUserUseCase
    {
        private readonly IFollowRepository repository;

        public FollowUserUseCase(IFollowRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void Execute(
            long targetUserId,
            Action<FollowToggleResult> onSuccess,
            Action<DashboardApiError> onError)
        {
            repository.Follow(targetUserId, onSuccess, onError);
        }
    }
}
