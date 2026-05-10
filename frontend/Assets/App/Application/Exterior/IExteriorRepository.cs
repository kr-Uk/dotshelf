using System;

namespace App.Application.Exterior
{
    /// <summary>
    /// 외부 환경(창밖 풍경 + 자연광 + 분위기) customization 의 repository.
    /// Tier 1: catalog 기반 local 구현만 존재. Tier 2 에서 BE API 구현 추가 예정.
    /// callback-async 패턴 (App/Application/Dashboard/ISpaceSettingsRepository 와 동일).
    /// </summary>
    public interface IExteriorRepository
    {
        /// <summary>
        /// 현재 적용된 외부 옵션의 assetId 조회. 0 = default (base APK 내장).
        /// </summary>
        void LoadCurrent(
            Action<long> onSuccess,
            Action<ExteriorApiError> onError);

        /// <summary>
        /// 외부 옵션 적용. assetId == 0 이면 default 적용.
        /// 카탈로그에 없거나 (Tier 2 에서) 미소유 시 onError.
        /// </summary>
        void Apply(
            long assetId,
            Action onSuccess,
            Action<ExteriorApiError> onError);
    }
}
