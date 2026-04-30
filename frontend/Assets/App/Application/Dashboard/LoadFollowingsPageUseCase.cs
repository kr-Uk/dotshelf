using System;

namespace App.Application.Dashboard
{
    public sealed class LoadFollowingsPageUseCase
    {
        public const int DefaultLimit = 30;

        private readonly IFollowRepository repository;

        public LoadFollowingsPageUseCase(IFollowRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void Execute(
            long userId,
            string cursor,
            Action<UserListPage> onSuccess,
            Action<DashboardApiError> onError)
        {
            repository.LoadFollowings(userId, cursor, DefaultLimit, onSuccess, onError);
        }
    }
}
