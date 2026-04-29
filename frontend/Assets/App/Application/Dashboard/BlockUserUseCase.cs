using System;

namespace App.Application.Dashboard
{
    public sealed class BlockUserUseCase
    {
        private readonly IFollowRepository repository;

        public BlockUserUseCase(IFollowRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void Execute(
            long targetUserId,
            Action<BlockToggleResult> onSuccess,
            Action<DashboardApiError> onError)
        {
            repository.Block(targetUserId, onSuccess, onError);
        }
    }
}
