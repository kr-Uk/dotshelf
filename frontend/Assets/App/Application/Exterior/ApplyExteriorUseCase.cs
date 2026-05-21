using System;

namespace App.Application.Exterior
{
    /// <summary>
    /// 외부 옵션 1개를 현재 룸에 적용. Tier 1 은 default 적용 시 호출.
    /// Tier 2 에서 사용자 갤러리 선택 / RoomTheme default 동기화에서 사용.
    /// LoadDashboardUseCase 와 동일 패턴: sealed + ctor DI + callback signature.
    /// </summary>
    public sealed class ApplyExteriorUseCase
    {
        private readonly IExteriorRepository _repository;

        public ApplyExteriorUseCase(IExteriorRepository repository)
        {
            _repository = repository;
        }

        /// <summary>
        /// assetId == 0 이면 default 적용.
        /// </summary>
        public void Execute(
            long assetId,
            Action onSuccess,
            Action<ExteriorApiError> onError)
        {
            _repository.Apply(assetId, onSuccess, onError);
        }

        /// <summary>
        /// 현재 적용된 옵션 (또는 0 = default) 을 다시 apply. 룸 진입 시 복원용.
        /// </summary>
        public void ExecuteCurrent(
            Action onSuccess,
            Action<ExteriorApiError> onError)
        {
            _repository.LoadCurrent(
                assetId => _repository.Apply(assetId, onSuccess, onError),
                onError);
        }
    }
}
