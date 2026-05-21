using App.Infrastructure.Catalogs;
using UnityEngine;

namespace App.Bootstrap
{
    // 테마가 spawn 한 placement 자식에 부착. 다음 테마 적용 시 RoomEditModeController 가
    // 이 marker 를 보고 "교체 대상 (같은 슬롯/자산)" 인지 "보존 대상 (사용자 placement)" 인지
    // 결정한다.
    //
    // 직접 인스턴스화 metadata:
    //   themeId    — spawn 한 테마 assetId. 디버깅 + 같은 테마 재적용 시 식별.
    //   themeSlot  — spec.ThemeSlot. 슬롯 기반 매칭의 1차 키.
    //   assetKey   — spec.AssetKey. 자산 기반 매칭의 2차 키.
    //
    // 사용자가 직접 placement 한 (PlaceInventoryEntry 경유) GameObject 에는 부착되지 않음 —
    // 그래서 정책상 "보존" 분류.
    [DisallowMultipleComponent]
    public sealed class ThemeSlotMarker : MonoBehaviour
    {
        [SerializeField] private long themeId;
        [SerializeField] private ThemeSlot themeSlot = ThemeSlot.None;
        [SerializeField] private string assetKey;

        public long ThemeId
        {
            get => themeId;
            set => themeId = value;
        }

        public ThemeSlot ThemeSlot
        {
            get => themeSlot;
            set => themeSlot = value;
        }

        public string AssetKey
        {
            get => assetKey;
            set => assetKey = value;
        }
    }
}
