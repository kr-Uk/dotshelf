using System;

namespace App.Application.Dashboard
{
    public sealed class LoadFollowersPageUseCase
    {
        public const int DefaultLimit = 30;

        private readonly IFollowRepository repository;

        public LoadFollowersPageUseCase(IFollowRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void Execute(
            long userId,
            string cursor,
            Action<UserListPage> onSuccess,
            Action<DashboardApiError> onError)
        {
            repository.LoadFollowers(userId, cursor, DefaultLimit, onSuccess, onError);
        }
    }
}
