using System;

namespace App.Application.Dashboard
{
    public sealed class LoadThemeContentsUseCase
    {
        private readonly IThemeContentsRepository repository;

        public LoadThemeContentsUseCase(IThemeContentsRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void Execute(long themeAssetId, Action<ThemeContents> onSuccess, Action<DashboardApiError> onError)
        {
            repository.LoadThemeContents(themeAssetId, onSuccess, onError);
        }
    }
}
