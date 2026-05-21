using App.Domain.Decoration;

namespace App.Application.Decoration
{
    // 데코 메타데이터 (Kind / 사이즈 multiplier override) 조회 추상화. 현재 구현체는 로컬
    // ScriptableObject(EditableDecorationCatalog) 를 wrap. 향후 샵/DB 합류 시 API 백엔드
    // 구현체로 교체하면 RoomEditModeController 의 코드는 그대로 둘 수 있다.
    //
    // PlacementCategory / AllowRotation 은 InventoryEntry 가 이미 들고 있으므로 이 인터페이스
    // 에서는 생략 (관심사가 다름 — 이 인터페이스는 "스케일 정규화" 에만 책임).
    public interface IDecorationMetadataProvider
    {
        // assetIdOrPath 는 catalog 의 SourceAssetPath 또는 향후 서버 assetId 등. 매칭 실패 시
        // false 를 반환하고 out 인자는 default. 호출부는 fallback 체인으로 진행.
        bool TryGet(string assetIdOrPath, out DecorationMetadata metadata);
    }

    public readonly struct DecorationMetadata
    {
        public readonly DecorationObjectKind Kind;
        // 0 이면 비활성. 1.0 = 기본 정규화 크기 유지, 0.8 = 80%, 1.2 = 120%. NormalizeSpawnedScale
        // 이 Kind 기반 등방 정규화 결과에 추가로 곱한다.
        public readonly float ScaleMultiplierOverride;

        public DecorationMetadata(DecorationObjectKind kind, float scaleMultiplierOverride)
        {
            Kind = kind;
            ScaleMultiplierOverride = scaleMultiplierOverride;
        }

        public static readonly DecorationMetadata None = new DecorationMetadata(DecorationObjectKind.None, 0f);
    }
}
