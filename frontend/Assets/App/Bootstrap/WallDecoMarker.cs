using App.Infrastructure.Catalogs;
using UnityEngine;

namespace App.Bootstrap
{
    /// <summary>
    /// Wall surface 에 부착된 deco GameObject 의 marker.
    /// PlaceWallDecoEntry 에서 spawn 시 자동 추가.
    /// RoomEditModeController.UpdateDrag 가 검사해 wall placement 드래그 분기로 라우트.
    /// CurrentWall — 현재 부착된 wall. 사용자가 드래그로 다른 wall 로 옮기면 갱신.
    /// AllowRotation — Selection panel 의 rotate 버튼 활성/비활성. 창문은 false.
    /// </summary>
    public sealed class WallDecoMarker : MonoBehaviour
    {
        [SerializeField] private WallSide currentWall = WallSide.Right;
        [SerializeField] private bool allowRotation = true;

        public WallSide CurrentWall
        {
            get => currentWall;
            set => currentWall = value;
        }

        public bool AllowRotation
        {
            get => allowRotation;
            set => allowRotation = value;
        }
    }
}
