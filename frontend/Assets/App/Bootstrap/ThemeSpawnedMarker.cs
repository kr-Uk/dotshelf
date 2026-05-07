using UnityEngine;

namespace App.Bootstrap
{
    /// <summary>
    /// 테마 적용 시 자동으로 spawn 된 deco 식별 marker.
    /// 사용자가 직접 인벤토리에서 placement 한 deco 와 구분 위해. 테마 변경 시 이 marker
    /// 가진 deco 만 정리 — 사용자 직접 placement 는 보존.
    /// 이 marker 가 붙은 GameObject 는 사용자가 자유롭게 drag/delete 가능 — 다만 테마
    /// 변경 시 함께 정리됨.
    /// </summary>
    public sealed class ThemeSpawnedMarker : MonoBehaviour
    {
    }
}
