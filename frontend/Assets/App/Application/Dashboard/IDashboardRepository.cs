using System;

namespace App.Application.Dashboard
{
    public interface IDashboardRepository
    {
        void LoadDashboard(
            long spaceId,
            Action<App.Domain.Dashboard.DashboardSnapshot> onSuccess,
            Action<DashboardApiError> onError);

        void LoadMyDashboard(
            Action<App.Domain.Dashboard.DashboardSnapshot> onSuccess,
            Action<DashboardApiError> onError);

        void ToggleSpaceLike(
            long spaceId,
            bool liked,
            Action<LikeToggleResult> onSuccess,
            Action<DashboardApiError> onError);
    }
}
