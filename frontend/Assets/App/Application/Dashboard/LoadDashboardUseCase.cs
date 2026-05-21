using System;
using App.Domain.Dashboard;

namespace App.Application.Dashboard
{
    public sealed class LoadDashboardUseCase
    {
        private readonly IDashboardRepository repository;

        public LoadDashboardUseCase(IDashboardRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void ExecuteForMine(
            Action<DashboardSnapshot> onSuccess,
            Action<DashboardApiError> onError)
        {
            repository.LoadMyDashboard(onSuccess, onError);
        }

        public void Execute(
            long spaceId,
            Action<DashboardSnapshot> onSuccess,
            Action<DashboardApiError> onError)
        {
            repository.LoadDashboard(spaceId, onSuccess, onError);
        }
    }
}
