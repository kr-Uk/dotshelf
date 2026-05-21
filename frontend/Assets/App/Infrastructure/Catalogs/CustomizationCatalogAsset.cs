using System;
using System.Collections.Generic;
using UnityEngine;

namespace App.Infrastructure.Catalogs
{
    /// <summary>
    /// v1.0 customization 카테고리 분류. 항시 7종 + 설치 3종.
    /// RoomEditModeController toolbar 키와 일치 (WALLPAPER, FLOOR, DESK, ...).
    /// </summary>
    public enum CustomizationCategory
    {
        // 항시 7종 — 외형만 변경, 위치/크기 고정
        Wallpaper,
        Floor,
        BookshelfSkin,    // 책장 스킨 기본값(테마 RESET 시 적용). 인스턴스별 외형은 Bookshelf.skinInventoryId
        DashboardSkin,
        Desk,             // 노트북 & 책상 세트
        InteriorLight,
        ExteriorLight,    // 창문 유무와 무관, 항시 동작 (창문 있으면 컷아웃 통과)

        // 설치/삭제 3종 — placement 가능
        BookshelfDeco,    // 책장 섹션 내부에 배치
        FloorDeco,
        WallDeco,         // 창문(Window) 포함
        Notebook          // 책상 위 노트북/모니터형 소품. 기존 enum 값 보존을 위해 끝에 추가.
    }

    /// <summary>
    /// 한 카탈로그 항목. 카테고리별로 의미 있는 필드만 채우고 나머지는 비움.
    /// — 단순 머티리얼(Wallpaper/Floor/Bookshelf/Dashboard): materials[0]만 사용
    /// — Desk: materials[0]=책상, [1]=상판, [2]=의자 (옵션)
    /// — InteriorLight/ExteriorLight: lightColor + intensity + colorTemperature + (옵션) prefab
    /// — Deco: prefab + placement(anchor/rotation/scale)
    /// </summary>
    [Serializable]
    public sealed class CustomizationEntry
    {
        [Header("Identity")]
        [SerializeField] private long assetId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite previewSprite;
        // 테마 태그 — 빈 문자열이면 무테마(공통) entry. 상단 메뉴 테마 필터(Editor/RoomEditThemeFilterMenu)
        // 적용 시 빈 태그는 어떤 테마에서도 노출, 비빈 태그는 일치 시에만 노출.
        [SerializeField] private string themeTag;
        // local preview 전용 entry 표시 — 클릭 시 서버 PATCH 를 발사하지 않고 로컬 씬 visual swap 만 수행.
        // placeholder 카탈로그가 생성한 entry 는 사용자 inventory 에 없어 PATCH 가 403 (S006) 으로 거부되므로,
        // 이 플래그가 true 면 RoomEditModeController.OnCustomizationEntrySelected 가 PATCH 단계를 skip.
        [SerializeField] private bool isLocalPreview;

        [Header("Materials (Wallpaper/Floor/Bookshelf/Dashboard/Desk)")]
        [SerializeField] private Material[] materials;

        [Header("Light (InteriorLight/ExteriorLight)")]
        [SerializeField] private Color lightColor = Color.white;
        [SerializeField, Range(0f, 4f)] private float lightIntensity = 1f;
        [SerializeField, Range(2700f, 8000f)] private float colorTemperatureKelvin = 6500f;

        [Header("Prefab (Light fixture / Deco mesh)")]
        [SerializeField] private GameObject prefab;

        [Header("Placement (Deco / Desk mesh swap)")]
        [Tooltip("mesh swap 시 native anchor (예: DeskRoot) 의 world position 에 더할 offset.")]
        [SerializeField] private Vector3 placementAnchor;
        [Tooltip("mesh swap 시 native anchor rotation 위에 추가로 적용할 회전 (Euler).")]
        [SerializeField] private Vector3 placementRotation;
        [Tooltip("spawn 된 prefab 의 transform.localScale. Vector3.one 기본.")]
        [SerializeField] private Vector3 placementScale = Vector3.one;
        [SerializeField] private bool usePlacementTransformAsWorld;
        [SerializeField] private bool lockVisualTransform;
        [SerializeField] private bool treatAsDeskSurface;

        [Header("Desk 전용 — NotebookStation override (모니터 진입 위치 fit)")]
        [Tooltip("true 면 Desk swap 시 NotebookStation 의 transform 을 아래 값들로 override. " +
                 "false 면 native 그대로. 다른 책상 모델 적용 시 모니터 화면/클릭 인터랙션 위치 fit.")]
        [SerializeField] private bool overrideNotebookStation;
        [Tooltip("true 면 NotebookStation 을 MeshSwap_DeskMonitor local 기준으로 배치. false 면 기존 GuestbookBoard/native 기준.")]
        [SerializeField] private bool lockNotebookStationToMonitor;
        [Tooltip("Desk 카테고리에서는 NotebookStation 기준 좌표. DashboardSkin 카테고리에서는 DashboardBoard 자체 transform.")]
        [SerializeField] private Vector3 notebookStationPosition;
        [Tooltip("NotebookStation 의 localRotation (부모 기준 Euler). transform.localRotation 으로 적용.")]
        [SerializeField] private Vector3 notebookStationRotation;
        [Tooltip("NotebookStation 의 localScale (부모 기준).")]
        [SerializeField] private Vector3 notebookStationScale = Vector3.one;

        [Header("Desk 전용 — Monitor sub-prefab (책상 + 모니터 한 entry, 사용자 placement 불가)")]
        [Tooltip("책상 mesh swap 시 함께 spawn 되는 monitor mesh. NotebookStation 과 같은 위치/회전/" +
                 "스케일로 fit 하면 노트북 클릭 hit area 와 monitor 화면이 정확 align.")]
        [SerializeField] private GameObject monitorPrefab;
        [SerializeField] private Vector3 monitorPosition;
        [SerializeField] private Vector3 monitorRotation;
        [SerializeField] private Vector3 monitorScale = Vector3.one;

        [Header("Desk 전용 — Skin scale multiplier (auto-fit 위 fine-tune)")]
        [Tooltip("DeskVisualSkinApplier 가 functional desk bounds 에 자동 fit 한 scale 위에 추가로 " +
                 "componentwise 곱하는 multiplier. (1,1,1) 또는 zero 면 multiplier 적용 안 함 (auto-fit " +
                 "결과 그대로). preserveSkinNativeScale=true 면 multiplier 도 무시 (raw GLB scale 유지). " +
                 "laptop y 는 skin 의 새 top 을 기준으로 자동 따라감 (수동 조정 불필요).")]
        [SerializeField] private Vector3 skinScaleMultiplier = Vector3.one;

        [Header("Desk 전용 — Skin native scale 보존")]
        [Tooltip("true 면 DeskVisualSkinApplier 가 functional desk bounds 에 강제로 맞추는 auto-fit " +
                 "을 skip 하고 GLB 의 raw localScale=one (원본 모델 크기/비율) 그대로 유지한다. 위치만 " +
                 "functional desk bottom (XZ 중심 + Y bottom-align) 으로 정렬해 책상이 floor 에 자연스럽게 " +
                 "놓이게 한다. laptop y 는 skin 의 실제 top 을 측정해 그 위에 올림. 예: vintage_table 처럼 " +
                 "GLB 가 이미 적절한 크기로 export 된 자산. false (default) 면 기존 auto-fit + skinScaleMultiplier " +
                 "동작.")]
        [SerializeField] private bool preserveSkinNativeScale;

        [Header("Desk 전용 — Skin localPosition 직접 override (Desk-local)")]
        [Tooltip("preserveSkinNativeScale=true 일 때 DeskVisualSkinApplier 의 bounds-based 위치 정렬 (XZ " +
                 "center / Y bottom-align) 대신 이 값을 skin GameObject 의 localPosition (Desk transform " +
                 "기준) 으로 직접 사용. zero 면 bounds-based 정렬 유지 (default). 디자이너가 Unity Inspector " +
                 "에서 skin transform 을 시각적으로 잡은 좌표를 그대로 박을 때 사용 — 예: deepsea_desk 처럼 " +
                 "GLB pivot/bounds 가 평범하지 않아 bounds 정렬이 어색한 경우.")]
        [SerializeField] private Vector3 skinLocalPositionOverride;

        [Header("Desk 전용 — Laptop hierarchy renderer 숨김 (monitor 모드)")]
        [Tooltip("true 면 Desk skin attach 시 Notebook GameObject (= laptop) 자식의 모든 Renderer (laptop " +
                 "body + 화면 mesh ScreenLid/BlackBezel/DisplayPanel 등) 를 disable. 테마 전환 시 enable " +
                 "원복. Canvas UI 는 CanvasRenderer 라 영향 없음 → ScreenSaverCanvas / DashboardCanvas 만 " +
                 "남아 monitor 화면처럼 보임. lockNotebookStationToMonitor=true 와 짝으로 사용 — " +
                 "NotebookStation 이 monitor 위치로 이동한 상태에서 laptop mesh 가 monitor 와 시각 충돌하는 " +
                 "걸 해소. 예: DeepSea 테마 (GoodVibes_Monitor 단독 사용).")]
        [SerializeField] private bool hideLaptopRenderers;

        [Header("Desk 전용 — Notebook/Screen monitor fit modulation (auto-fit 위 offset)")]
        [Tooltip("monitor 모드 (hideLaptopRenderers + lockNotebookStationToMonitor) 의 AlignNotebookScreenToMonitor " +
                 "auto-fit position (= monitorBounds.center) 에 더할 monitor-local 좌표계 offset. " +
                 "monitor.TransformVector 로 monitor 의 right/up/forward 축에 매핑된 후 world 에 적용. " +
                 "zero (default) 면 pure auto-fit. 예: (0, 0, -0.01) → monitor 의 -Z 방향으로 1cm offset " +
                 "(z-fight 회피).")]
        [SerializeField] private Vector3 notebookScreenLocalPosition;
        [Tooltip("monitor 모드 auto-fit rotation (= monitorTransform.rotation) 에 right-multiply 하는 " +
                 "Euler offset. zero (default) 면 pure auto-fit (Screen.rotation = monitor.rotation). " +
                 "monitor 의 local forward(+Z) 가 디스플레이 normal 이 아닌 경우 (GLB export 축이 달라서 " +
                 "화면이 옆이나 뒤로 누워 보이는 경우) 보정값 박기. " +
                 "예: (0, 180, 0) 로 디스플레이 반대 면이 monitor.forward 인 GLB 보정, (90, 0, 0) 로 X 축 " +
                 "회전 보정. 디자이너 Inspector 튜닝 후 catalog 에 박기.")]
        [SerializeField] private Vector3 notebookScreenLocalRotation;
        [Tooltip("monitor 모드 auto-fit uniform scale (contain fit) 에 componentwise 곱하는 multiplier. " +
                 "(1,1,1) 또는 zero (default) 면 pure auto-fit. 예: (1.1, 1.1, 1) → 가로/세로 110% 로 " +
                 "약간 cover 하듯 채움, (0.9, 0.9, 1) → 90% 로 margin 두기.")]
        [SerializeField] private Vector3 notebookScreenScaleMultiplier = Vector3.one;

        [Header("Desk 전용 — ScreenSaverCanvas AdImage RectTransform override (monitor 모드)")]
        [Tooltip("true 면 Desk skin attach (monitor 모드) 시 ScreenSaverCanvas 자식 AdImage 의 " +
                 "RectTransform 을 아래 adImage* 필드로 강제 set. CanvasScale 과 무관하게 monitor " +
                 "디스플레이 전체를 AdImage 가 채우게 됨. NotebookScreenController 의 procedural laptop " +
                 "화면 비율 (800×450 inset) 을 monitor 화면 비율로 맞추기 위함. CameraFocusPoint / " +
                 "camera 이동 로직은 영향 없음. 예: DeepSea (GoodVibes_Monitor 단독 사용).")]
        [SerializeField] private bool stretchCanvasAdImageOnDesk;
        [Tooltip("AdImage RectTransform.anchorMin (default (0,0)).")]
        [SerializeField] private Vector2 adImageAnchorMin = new Vector2(0f, 0f);
        [Tooltip("AdImage RectTransform.anchorMax (default (1,1) — stretch full).")]
        [SerializeField] private Vector2 adImageAnchorMax = new Vector2(1f, 1f);
        [Tooltip("AdImage RectTransform.pivot (default (0.5, 0.5)).")]
        [SerializeField] private Vector2 adImagePivot = new Vector2(0.5f, 0.5f);
        [Tooltip("AdImage RectTransform.offsetMin = (Left, Bottom). Inspector 의 Left/Bottom 값과 동일. " +
                 "예: Inspector Left=-5, Bottom=4 → (-5, 4).")]
        [SerializeField] private Vector2 adImageOffsetMin;
        [Tooltip("AdImage RectTransform.offsetMax = (-Right, -Top). Inspector 의 Right/Top 부호를 뒤집은 값. " +
                 "예: Inspector Right=5, Top=-4 → offsetMax = (-5, -(-4)) = (-5, 4).")]
        [SerializeField] private Vector2 adImageOffsetMax;
        [Tooltip("AdImage 의 localScale. zero (default) 면 Vector3.one 으로 fallback. 디자이너 측정값 " +
                 "박기. 예: DeepSea (0.9922, 1.0775, 1).")]
        [SerializeField] private Vector3 adImageLocalScale = Vector3.one;

        [Header("Desk 전용 — ScreenSaverCanvas RectTransform override")]
        [Tooltip("true 면 Desk skin attach 시 ScreenSaverCanvas (Screen 자식, NotebookScreenController " +
                 "런타임 생성) 의 RectTransform 을 아래 4개 필드 값으로 강제. AlignNotebookScreenToMonitor " +
                 "의 ResetCanvasLocalRotations / auto-fit 이후에 적용되므로 디자이너 지정값이 최종 우선. " +
                 "CameraFocusPoint / camera 이동 로직은 영향 없음 (ScreenSaverCanvas 만 변경, Screen 자체나 " +
                 "CameraFocusPoint 는 안 건드림). 예: DeepSea — Inspector 에서 monitor 화면에 맞춘 값 박기.")]
        [SerializeField] private bool overrideScreenSaverCanvasTransform;
        [Tooltip("overrideScreenSaverCanvasTransform=true 일 때 ScreenSaverCanvas.localPosition 으로 set " +
                 "할 값 (Screen 기준 local). Inspector 의 Pos X/Y/Z 가 그대로.")]
        [SerializeField] private Vector3 screenSaverCanvasLocalPosition;
        [Tooltip("ScreenSaverCanvas.localEulerAngles override. Inspector 의 Rotation 그대로. 예: " +
                 "(-90, 0, -180).")]
        [SerializeField] private Vector3 screenSaverCanvasLocalRotation;
        [Tooltip("ScreenSaverCanvas.localScale override. zero (default) 면 Vector3.one 으로 fallback. " +
                 "예: (0.0005, 0.0005, 0.0005) — monitor 화면 fit 용 매우 작은 scale.")]
        [SerializeField] private Vector3 screenSaverCanvasLocalScale = Vector3.one;
        [Tooltip("ScreenSaverCanvas.sizeDelta override (Inspector 의 Width / Height). zero (default) 면 " +
                 "기존 sizeDelta 유지 (NotebookScreenController.ScreenSaverCanvasSize = 800×450). " +
                 "예: (800, 450).")]
        [SerializeField] private Vector2 screenSaverCanvasSizeDelta;

        [Header("Desk 전용 — ScreenSaverCanvas anchor/pivot override")]
        [Tooltip("true 면 아래 anchorMin/anchorMax/pivot 값을 ScreenSaverCanvas RectTransform 에 강제 적용. " +
                 "false (default) 면 NotebookScreenController.CreateCanvas 가 만든 Unity RectTransform 기본값 " +
                 "(anchorMin=anchorMax=pivot=(0.5,0.5)) 유지. anchor 가 stretch 가 아닌 fixed 위치라 " +
                 "anchoredPosition 의 좌표계가 anchorMin/Max 에 따라 달라지므로, anchored 변경 시 같이 박을 것. " +
                 "예: Vintage — anchorMin=(0,0) anchorMax=(0,0) pivot=(0.5,0.5) — 부모 좌하단 기준 + canvas " +
                 "중심에 pivot. DeepSea 는 이 flag 가 꺼져 있어 영향 없음.")]
        [SerializeField] private bool overrideScreenSaverCanvasAnchors;
        [Tooltip("overrideScreenSaverCanvasAnchors=true 일 때 RectTransform.anchorMin 으로 set.")]
        [SerializeField] private Vector2 screenSaverCanvasAnchorMin = new Vector2(0.5f, 0.5f);
        [Tooltip("overrideScreenSaverCanvasAnchors=true 일 때 RectTransform.anchorMax 로 set.")]
        [SerializeField] private Vector2 screenSaverCanvasAnchorMax = new Vector2(0.5f, 0.5f);
        [Tooltip("overrideScreenSaverCanvasAnchors=true 일 때 RectTransform.pivot 으로 set.")]
        [SerializeField] private Vector2 screenSaverCanvasPivot = new Vector2(0.5f, 0.5f);

        [Header("Desk 전용 — 노트북 포커스 카메라 진입 mode")]
        [Tooltip("FocusNotebook 진입 시 CameraController.MoveToNotebookScreen 이 사용할 mode (int). " +
                 "BookshelfPorting.Runtime.NotebookFocusCameraMode enum 의 정수값과 1:1 매핑.\n" +
                 "  0 = ScreenAnchorBaked (default / vintage / 기본 procedural laptop). Screen 자식 " +
                 "CameraFocusPoint 의 prefab-baked offset 사용. laptop 슬랜트 framing 에 fit. " +
                 "테이블 높이가 달라도 CameraFocusPoint 가 Screen-local 좌표라 Screen 따라가서 자동 적응.\n" +
                 "  1 = ScreenForwardGeometry (DeepSea monitor 모드). screen.forward × world distance 로 " +
                 "geometry-driven derive. AlignNotebookScreenToMonitor 로 Screen 이 upright 회전된 " +
                 "monitor 모드 desk 에 fit.\n" +
                 "신규 desk entry 는 monitor 모드 (hideLaptopRenderers + lockNotebookStationToMonitor) 면 " +
                 "1 로, 일반 laptop-on-desk 면 0 (default) 으로 박기.")]
        [SerializeField] private int notebookFocusCameraMode;

        [Header("Desk 전용 — Notebook 자식 transform override (DeskVisualSkinApplier 의 laptop Y 자동 재정렬 우회)")]
        [Tooltip("zero (default) 면 DeskVisualSkinApplier 가 skin top 기준으로 laptop.localPosition.y 만 " +
                 "자동 재정렬하는 기존 로직 그대로 사용. 비-zero (활성 판정 = scaleOverride.sqrMagnitude > 0) " +
                 "면 자동 Y 재정렬 SKIP 하고 아래 3 필드를 Notebook transform 의 localPosition/" +
                 "localEulerAngles/localScale 에 그대로 박는다. 디자이너가 책상 위 Notebook 위치/회전을 " +
                 "Inspector 에서 정확히 지정해야 할 때 사용. native rot/scale 은 marker 에 캡처 + clear 시 복원.")]
        [SerializeField] private Vector3 notebookLocalPositionOverride;
        [Tooltip("OverrideNotebookLocalTransform 활성 시 Notebook.localEulerAngles 로 set 할 값.")]
        [SerializeField] private Vector3 notebookLocalRotationOverride;
        [Tooltip("활성 판정 + Notebook.localScale. zero (default) 면 override 비활성, " +
                 "(1,1,1) 또는 그 외 비-zero 면 활성.")]
        [SerializeField] private Vector3 notebookLocalScaleOverride;

        public long AssetId => assetId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite PreviewSprite => previewSprite;
        public string ThemeTag => themeTag;
        public bool IsLocalPreview => isLocalPreview;
        public IReadOnlyList<Material> Materials => materials;
        public Material PrimaryMaterial => (materials != null && materials.Length > 0) ? materials[0] : null;
        public Color LightColor => lightColor;
        public float LightIntensity => lightIntensity;
        public float ColorTemperatureKelvin => colorTemperatureKelvin;
        public GameObject Prefab => prefab;
        public Vector3 PlacementAnchor => placementAnchor;
        public Vector3 PlacementRotation => placementRotation;
        public Vector3 PlacementScale => placementScale;
        public bool UsePlacementTransformAsWorld => usePlacementTransformAsWorld;
        public bool LockVisualTransform => lockVisualTransform;
        public bool TreatAsDeskSurface => treatAsDeskSurface;
        public bool OverrideNotebookStation => overrideNotebookStation;
        public bool LockNotebookStationToMonitor => lockNotebookStationToMonitor;
        public Vector3 NotebookStationPosition => notebookStationPosition;
        public Vector3 NotebookStationRotation => notebookStationRotation;
        public Vector3 NotebookStationScale => notebookStationScale;
        public GameObject MonitorPrefab => monitorPrefab;
        public Vector3 MonitorPosition => monitorPosition;
        public Vector3 MonitorRotation => monitorRotation;
        public Vector3 MonitorScale => monitorScale;
        public Vector3 SkinScaleMultiplier => skinScaleMultiplier;
        public bool PreserveSkinNativeScale => preserveSkinNativeScale;
        public Vector3 SkinLocalPositionOverride => skinLocalPositionOverride;
        public bool HideLaptopRenderers => hideLaptopRenderers;
        public Vector3 NotebookScreenLocalPosition => notebookScreenLocalPosition;
        public Vector3 NotebookScreenLocalRotation => notebookScreenLocalRotation;
        public Vector3 NotebookScreenScaleMultiplier => notebookScreenScaleMultiplier;
        public bool StretchCanvasAdImageOnDesk => stretchCanvasAdImageOnDesk;
        public Vector2 AdImageAnchorMin => adImageAnchorMin;
        public Vector2 AdImageAnchorMax => adImageAnchorMax;
        public Vector2 AdImagePivot => adImagePivot;
        public Vector2 AdImageOffsetMin => adImageOffsetMin;
        public Vector2 AdImageOffsetMax => adImageOffsetMax;
        public Vector3 AdImageLocalScale => adImageLocalScale;
        public bool OverrideScreenSaverCanvasTransform => overrideScreenSaverCanvasTransform;
        public Vector3 ScreenSaverCanvasLocalPosition => screenSaverCanvasLocalPosition;
        public Vector3 ScreenSaverCanvasLocalRotation => screenSaverCanvasLocalRotation;
        public Vector3 ScreenSaverCanvasLocalScale => screenSaverCanvasLocalScale;
        public Vector2 ScreenSaverCanvasSizeDelta => screenSaverCanvasSizeDelta;
        public bool OverrideScreenSaverCanvasAnchors => overrideScreenSaverCanvasAnchors;
        public Vector2 ScreenSaverCanvasAnchorMin => screenSaverCanvasAnchorMin;
        public Vector2 ScreenSaverCanvasAnchorMax => screenSaverCanvasAnchorMax;
        public Vector2 ScreenSaverCanvasPivot => screenSaverCanvasPivot;
        public int NotebookFocusCameraMode => notebookFocusCameraMode;

        public Vector3 NotebookLocalPositionOverride => notebookLocalPositionOverride;
        public Vector3 NotebookLocalRotationOverride => notebookLocalRotationOverride;
        public Vector3 NotebookLocalScaleOverride => notebookLocalScaleOverride;
        // 활성 판정 — scaleOverride sqrMagnitude > 1e-10 일 때만 verbatim 분기. 다른 entry 는
        // default zero 라 false → 기존 자동 Y 재정렬 동작 보존.
        public bool OverrideNotebookLocalTransform => notebookLocalScaleOverride.sqrMagnitude > 1e-10f;
    }

    /// <summary>
    /// 카테고리 하나에 해당하는 항목 리스트.
    /// 자산 1종당 한 SO 파일 — Resources/Catalogs/{Category}Catalog.asset
    /// (DesignPreviewPackCreatorUtility가 placeholder entries 자동 생성).
    /// </summary>
    [CreateAssetMenu(menuName = "Dotshelf/Customization Catalog", fileName = "CustomizationCatalog")]
    public sealed class CustomizationCatalogAsset : ScriptableObject
    {
        [SerializeField] private CustomizationCategory category;
        [SerializeField] private List<CustomizationEntry> entries = new List<CustomizationEntry>();

        public CustomizationCategory Category => category;
        public IReadOnlyList<CustomizationEntry> Entries => entries;

        public CustomizationEntry FindByAssetId(long assetId)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].AssetId == assetId)
                {
                    return entries[i];
                }
            }
            return null;
        }
    }
}
