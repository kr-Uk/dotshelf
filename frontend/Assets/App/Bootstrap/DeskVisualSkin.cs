using System.Collections.Generic;
using UnityEngine;

namespace App.Bootstrap
{
    // DeskCatalog entry 의 prefab 으로 functional Desk transform 위에 attach 된 visual skin.
    // GameObject 에 부착되는 marker. RoomEditModeController 는 직접 reference 하지 않고
    // DeskVisualSkinApplier 가 attach/clear 양 경로에서 lookup 키로 사용.
    //
    // 보관 항목:
    //   entryAssetId — spawn 한 catalog entry assetId. 디버깅 + 같은 entry 재적용 시 식별.
    //   hiddenFunctionalRenderers — attach 시 disable 한 Desk 자식 Renderer 들 (Notebook subtree
    //       제외). clear 시 원복해 functional desk mesh (Desktop/DeskLegA-D/table_natural) 가
    //       다시 보이게 함.
    //   nativeLaptopLocalPosition — attach 직전의 Notebook localPosition 스냅샷. attach 시점에
    //       skin top 으로 laptop y 가 옮겨지므로, clear 시 native (procedural 책 두께 offset 기준)
    //       위치로 정확히 복원하기 위한 reference.
    //
    // BookshelfVisualSkin 와 달리 Desk 는 rebuild 이벤트가 없다 (씬 init 시 1회 빌드 후 고정).
    // 따라서 BookshelfShellRebuilt 같은 이벤트 subscribe 가 필요 없음 — 순수 데이터 marker.
    [DisallowMultipleComponent]
    public sealed class DeskVisualSkin : MonoBehaviour
    {
        public const string SkinChildName = "ThemeDeskVisual";

        [SerializeField] private long entryAssetId;
        // [SerializeField] 미지정 — runtime-only. scene save 시 destroyed Renderer 의 fake-null
        // reference 직렬화 회귀 방지 (MR #174 beshu #3 / hiddenFunctionalRenderers stale 가드).
        private readonly List<Renderer> hiddenFunctionalRenderers = new List<Renderer>();
        // Notebook hierarchy (laptop body + 화면 mesh) 의 Renderer 들. entry.HideLaptopRenderers=true 일
        // 때 DeskVisualSkinApplier 가 attach 시 disable 한 후 여기에 캡처 → ClearSkinUnder 가 enable
        // 원복. Canvas UI 는 CanvasRenderer 라 이 리스트에 안 들어감 → monitor 모드에서 dashboard /
        // screen saver UI 만 살아남음. 위와 같은 이유로 SerializeField 미지정.
        private readonly List<Renderer> hiddenLaptopRenderers = new List<Renderer>();
        [SerializeField] private Vector3 nativeLaptopLocalPosition;
        [SerializeField] private Quaternion nativeLaptopLocalRotation = Quaternion.identity;
        [SerializeField] private Vector3 nativeLaptopLocalScale = Vector3.one;
        [SerializeField] private bool hasNativeLaptopCapture;

        // Notebook/Screen transform 의 native (override 적용 전) 값. monitor 모드에서 entry 의
        // notebookScreen* override 가 적용되면 Screen 의 localPos/Rot/Scale 이 변경되는데, 테마
        // 전환 시 native 로 정확히 복원해야 procedural 노트북 다시 보일 때 디스플레이 각도가 안 깨짐.
        [SerializeField] private Vector3 nativeScreenLocalPosition;
        [SerializeField] private Quaternion nativeScreenLocalRotation = Quaternion.identity;
        [SerializeField] private Vector3 nativeScreenLocalScale = Vector3.one;
        [SerializeField] private bool hasNativeScreenCapture;

        public long EntryAssetId
        {
            get => entryAssetId;
            set => entryAssetId = value;
        }

        public List<Renderer> HiddenFunctionalRenderers => hiddenFunctionalRenderers;
        public List<Renderer> HiddenLaptopRenderers => hiddenLaptopRenderers;

        public Vector3 NativeLaptopLocalPosition
        {
            get => nativeLaptopLocalPosition;
            set => nativeLaptopLocalPosition = value;
        }

        public Quaternion NativeLaptopLocalRotation
        {
            get => nativeLaptopLocalRotation;
            set => nativeLaptopLocalRotation = value;
        }

        public Vector3 NativeLaptopLocalScale
        {
            get => nativeLaptopLocalScale;
            set => nativeLaptopLocalScale = value;
        }

        public bool HasNativeLaptopCapture
        {
            get => hasNativeLaptopCapture;
            set => hasNativeLaptopCapture = value;
        }

        public Vector3 NativeScreenLocalPosition
        {
            get => nativeScreenLocalPosition;
            set => nativeScreenLocalPosition = value;
        }

        public Quaternion NativeScreenLocalRotation
        {
            get => nativeScreenLocalRotation;
            set => nativeScreenLocalRotation = value;
        }

        public Vector3 NativeScreenLocalScale
        {
            get => nativeScreenLocalScale;
            set => nativeScreenLocalScale = value;
        }

        public bool HasNativeScreenCapture
        {
            get => hasNativeScreenCapture;
            set => hasNativeScreenCapture = value;
        }
    }
}
