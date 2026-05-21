using System;

namespace App.Domain.Decoration
{
    // 데코레이션 배치 1개의 영속화 단위. Domain 은 UnityEngine 미참조 정책이라 Vector3/Quaternion
    // 대신 primitive float 만 사용. JsonUtility / 백엔드 DTO 둘 다 [Serializable] + public field
    // 접근을 요구하므로 그 형식을 따름.
    //
    // AssetId: DecorationItemMarker.InventoryKey 값을 그대로 사용 (path:.. / prefab-iid:.. / name:..).
    //   서버 인벤토리 도입 시 BuildInventoryKey 가 "asset:..." / "inv:..." 를 발행해도 호환.
    // Position: world position 기준 (RoomEditModeController.placedItemsRoot 가 scene root 라 world=local).
    // RotationY: Y축 단일 회전 각 (현재 편집은 Y축 회전만 지원). Quaternion 풀 저장 대신 float 1개.
    // Scale: NormalizeSpawnedScale 이 등방성 scale 만 적용하지만 안전을 위해 3축 보존.
    [Serializable]
    public sealed class DecorationLayoutItem
    {
        public string AssetId;
        public float PositionX;
        public float PositionY;
        public float PositionZ;
        public float RotationY;
        public float ScaleX;
        public float ScaleY;
        public float ScaleZ;
    }
}
