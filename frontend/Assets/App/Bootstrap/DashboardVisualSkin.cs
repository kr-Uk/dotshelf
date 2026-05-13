using System.Collections.Generic;
using UnityEngine;

namespace App.Bootstrap
{
    // 테마의 ThemeSlot.Dashboard spec 으로 functional dashboard root (GuestbookBoard
    // 또는 DashboardScreenPanel) 위에 attach 된 visual skin GameObject 에 부착되는 marker.
    // DashboardVisualSkinApplier 가 attach/clear 양 경로에서 lookup 키로 사용.
    //
    // BookshelfVisualSkin 과 다른 점: 책장 procedural rebuild 같은 동적 재생성이 dashboard
    // 쪽엔 없어서 ShellRebuilt 같은 event 구독이 필요 없음. 단순 metadata + hidden renderer
    // 보관만 담당.
    //
    // 보관 항목:
    //   themeId   — spawn 한 테마 assetId.
    //   assetKey  — spec.AssetKey. clear 후 다른 자산으로 재부착할 때 동일성 확인 용.
    //   hiddenFunctionalRenderers — attach 시점에 disable 한 functional dashboard root 자식
    //       Renderer 들. clear 시 원복해 native dashboard board mesh 가 다시 보이게 한다.
    //       DashboardScreenPanel (UIDocument) / NotebookStation / GuestbookHitArea 자식 +
    //       skin 본인 자식은 hide 대상에서 제외 (UI panel/노트북/raycast 보존).
    //       runtime-only (SerializeField 미지정) — scene save / Inspector 직렬화 시 destroyed
    //       Renderer 의 fake-null reference 가 따라가 stale 직렬화 회귀 방지 (MR #174 beshu #3).
    [DisallowMultipleComponent]
    public sealed class DashboardVisualSkin : MonoBehaviour
    {
        public const string SkinChildName = "ThemeDashboardVisual";

        [SerializeField] private long themeId;
        [SerializeField] private string assetKey;
        private readonly List<Renderer> hiddenFunctionalRenderers = new List<Renderer>();

        public long ThemeId
        {
            get => themeId;
            set => themeId = value;
        }

        public string AssetKey
        {
            get => assetKey;
            set => assetKey = value;
        }

        public List<Renderer> HiddenFunctionalRenderers => hiddenFunctionalRenderers;
    }
}
