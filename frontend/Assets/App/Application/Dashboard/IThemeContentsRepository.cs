using System;

namespace App.Application.Dashboard
{
    public interface IThemeContentsRepository
    {
        void LoadThemeContents(long themeAssetId, Action<ThemeContents> onSuccess, Action<DashboardApiError> onError);
    }
}
