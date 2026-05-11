using System;
using App.Application.Exterior;

namespace App.Infrastructure.Catalogs
{
    /// <summary>
    /// catalog SO 기반 IExteriorRepository 구현. Tier 1 default impl.
    /// — 적용 대상 SO 는 ExteriorCatalogAsset 에서 lookup.
    /// — 실제 씬 적용 (RenderSettings/Sun/Prefab spawn) 은 생성자로 주입된
    ///   delegate (보통 ExteriorApplier.Apply) 가 수행.
    /// — 현재 적용 상태는 in-memory. Tier 2 에서 BE PATCH /v1/spaces/me/exterior 또는
    ///   PlayerPrefs 로 persist.
    ///
    /// 의도: Infrastructure 가 Features (MonoBehaviour) 를 직접 모르도록 delegate 로
    /// 약결합. asmdef 의존 그래프 위반 방지.
    /// </summary>
    public sealed class CatalogExteriorRepository : IExteriorRepository
    {
        private readonly ExteriorCatalogAsset _catalog;
        private readonly Action<ExteriorAsset> _applyToScene;

        // in-memory current state. 0 = default.
        private long _currentAssetId;

        public CatalogExteriorRepository(
            ExteriorCatalogAsset catalog,
            Action<ExteriorAsset> applyToScene)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _applyToScene = applyToScene ?? throw new ArgumentNullException(nameof(applyToScene));
            _currentAssetId = 0;
        }

        public void LoadCurrent(
            Action<long> onSuccess,
            Action<ExteriorApiError> onError)
        {
            onSuccess?.Invoke(_currentAssetId);
        }

        public void Apply(
            long assetId,
            Action onSuccess,
            Action<ExteriorApiError> onError)
        {
            var asset = _catalog.Resolve(assetId);
            if (asset == null)
            {
                onError?.Invoke(new ExteriorApiError
                {
                    Code = "EXT_NOT_FOUND",
                    Message = $"ExteriorAsset(id={assetId}) not found and no default fallback in catalog."
                });
                return;
            }

            try
            {
                _applyToScene(asset);
                _currentAssetId = assetId;
                onSuccess?.Invoke();
            }
            catch (Exception ex)
            {
                onError?.Invoke(new ExteriorApiError
                {
                    Code = "EXT_APPLY_FAILED",
                    Message = ex.Message,
                    Error = ex.ToString()
                });
            }
        }
    }
}
