using System;

namespace App.Application.Dashboard
{
    // 카테고리별 적용 + 전체 테마(mode) 적용 7 UseCase. 모두 ISpaceSettingsRepository 위에 한 줄짜리 위임.
    // onSuccess(bool themeReset) — true면 클라이언트가 룸 전체 reload.

    public sealed class ChangeWallpaperUseCase
    {
        private readonly ISpaceSettingsRepository repository;
        public ChangeWallpaperUseCase(ISpaceSettingsRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }
        public void Execute(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            repository.ChangeWallpaper(inventoryId, onSuccess, onError);
        }
    }

    public sealed class ChangeFloorUseCase
    {
        private readonly ISpaceSettingsRepository repository;
        public ChangeFloorUseCase(ISpaceSettingsRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }
        public void Execute(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            repository.ChangeFloor(inventoryId, onSuccess, onError);
        }
    }

    public sealed class ChangeDeskUseCase
    {
        private readonly ISpaceSettingsRepository repository;
        public ChangeDeskUseCase(ISpaceSettingsRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }
        public void Execute(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            repository.ChangeDesk(inventoryId, onSuccess, onError);
        }
    }

    public sealed class ChangeDashboardSkinUseCase
    {
        private readonly ISpaceSettingsRepository repository;
        public ChangeDashboardSkinUseCase(ISpaceSettingsRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }
        public void Execute(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            repository.ChangeDashboard(inventoryId, onSuccess, onError);
        }
    }

    public sealed class ChangeInteriorLightUseCase
    {
        private readonly ISpaceSettingsRepository repository;
        public ChangeInteriorLightUseCase(ISpaceSettingsRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }
        public void Execute(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            repository.ChangeInteriorLight(inventoryId, onSuccess, onError);
        }
    }

    public sealed class ChangeExteriorLightUseCase
    {
        private readonly ISpaceSettingsRepository repository;
        public ChangeExteriorLightUseCase(ISpaceSettingsRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }
        public void Execute(long? inventoryId, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            repository.ChangeExteriorLight(inventoryId, onSuccess, onError);
        }
    }

    public sealed class ApplyThemeWithModeUseCase
    {
        private readonly ISpaceSettingsRepository repository;
        public ApplyThemeWithModeUseCase(ISpaceSettingsRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }
        public void Execute(long inventoryId, ThemeApplyMode mode, Action<bool> onSuccess, Action<DashboardApiError> onError)
        {
            repository.ApplyThemeWithMode(inventoryId, mode, onSuccess, onError);
        }
    }
}
