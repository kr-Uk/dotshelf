using System;

namespace App.Application.Dashboard
{
    public sealed class UnfollowUserUseCase
    {
        private readonly IFollowRepository repository;

        public UnfollowUserUseCase(IFollowRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void Execute(
            long targetUserId,
            Action<FollowToggleResult> onSuccess,
            Action<DashboardApiError> onError)
        {
            repository.Unfollow(targetUserId, onSuccess, onError);
        }
    }
}
