using System;
using System.Collections.Generic;
using App.Application.Dashboard;
using App.Application.Decoration;
using App.Auth;
using App.Domain.Decoration;
using App.Infrastructure.Api;
using App.Infrastructure.Api.Dashboard;
using App.Infrastructure.Api.Dtos;
using App.Infrastructure.Catalogs;
using App.Infrastructure.Decoration;
using BookshelfPorting.Runtime;
using Dotshelf.Scripts.Dashboard;
using Dotshelf.Scripts.RoomTheme;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace App.Bootstrap
{
    // Owns the floating "꾸미기" button on the main scene, the 편집/샵 popup, the edit
    // mode overlay (return button, bottom toolbar, inventory) and the placement /
    // selection / rotate / delete interactions for editable decorations.
    //
    // Visibility coordination: SettingsScreenController polls IsEditModeActive to hide
    // its own floating buttons during edit mode. This controller hides its decoration
    // button during bookshelf focus / onboarding step 3 / edit mode.
    [DefaultExecutionOrder(-650)]
    [DisallowMultipleComponent]
    public sealed class RoomEditModeController : MonoBehaviour
    {
        private const string CanvasName = "RoomEditCanvas";
        private const int CanvasSortingOrder = 5800;
        private const int UiLayer = 5;

        // Match the Korean TMP font names used by Login/Onboarding/Settings so this
        // controller picks up the same project font instead of falling back to
        // LiberationSans SDF (which has no Hangul glyphs and prints □ for them).
        private const string PreferredKoreanFontAssetName = "NotoSansKR-Regular";
        private const string FallbackKoreanFontAssetName = "NOTOSANSKR SDF";

        // Stack the decoration button below SettingsScreenController's two buttons.
        // Mirrors SettingsButtonWidth/Height/Margin/StackGap so the column is even.
        private const float ButtonWidth = 132f;
        private const float ButtonHeight = 60f;
        private const float ButtonMargin = 32f;
        private const float ButtonStackGap = 12f;

        // 우측 오브젝트 조작 카드 (BuildSelectionPanel) 디멘션. Save/Return column 보다 살짝 넓되
        // 화면을 과하게 가리지 않도록 컴팩트. SelectionButtonInnerWidth = SelectionCardWidth - SelectionCardPadding*2.
        private const float SelectionCardWidth = 144f;
        private const int SelectionCardPadding = 12;
        private const float SelectionCardSpacing = 8f;
        private const float SelectionButtonInnerWidth = 120f;
        private const float SelectionButtonHeight = 44f;

        // Placement defaults: in front of the bookshelf, on the floor (Floor surface y=0).
        // Bookshelf turntable sits around z>0.5, so place items in the open floor area.
        private static readonly Vector3 InventoryPlacementCenter = new Vector3(0f, 0.0f, -0.6f);
        private const float InventorySpawnSpacing = 0.45f;
        // Keep the *initial* spawn position from sliding back into the bookshelf
        // footprint when the inventory has piled up many items. Drag-time
        // movement no longer uses this — the player can freely drag a
        // decoration up to (but not into) the bookshelf via collider checks.
        private const float InventorySpawnMaxZ = 0.5f;
        private const float SelectionRotateStepDegrees = 45f;

        // Fallback room footprint, used only if RefreshPlayableBounds can't
        // find the named scene geometry (Floor / LeftWall / RightWall /
        // BackWall_*). Real bounds at runtime are populated by that method
        // when edit mode starts.
        //
        // BookshelfGenerator (RoomWidth=5): LeftWall 중심 X = -5*0.5+0.05 = -2.45,
        // 벽 두께 0.08 → 안쪽 면(playable inner edge) = -2.45 + 0.04 = -2.41.
        // FloorMin/MaxX 는 wall center 가 아닌 inner face 기준 (MR #147 review Finding 3).
        // Z 축은 RoomDepth=5 와 별개로 BookshelfGenerator 의 BackWall 위치(Z=1.45) 가
        // 책장 alcove 때문에 비대칭이라 ±2.4 fallback 유지 — RefreshPlayableBounds 가
        // 정확한 BackWall strip 의 Z 를 읽어 덮어씀.
        private const float FloorMinX = -2.41f;
        private const float FloorMaxX = 2.41f;
        private const float FloorMinZ = -2.4f;
        private const float FloorMaxZ = 2.4f;

        // Safe upper bound for the back-side movement boundary: the front
        // (room-facing) face of the back wall in the BookshelfGenerator
        // build (BackWallZ 1.45m − BackWallThickness 0.08m × 0.5 = 1.41m).
        // Used as both the initial value for `maxZ` in RefreshPlayableBounds
        // AND a hard cap on every detected candidate, so a missed scene-
        // scan (renamed wall, inactive root, asset reload race) can never
        // raise the boundary past the visible wall plane and let dragged
        // objects tunnel into the wall / out of the room. The constant
        // mirrors the value baked into BookshelfGenerator — if that scene
        // builder changes the wall depth, this needs to follow.
        private const float BackWallReferenceZ = 1.41f;
        private const float FloorPlaneY = 0f;
        // Minimum clearance kept between a dragged object and any static
        // blocker (bookshelf / notebook / whiteboard / hamster / etc.) so
        // meshes don't visually z-fight at the contact face. The OverlapBox
        // is *expanded* by this margin so a candidate position whose face is
        // closer than the margin to a blocker counts as overlapping. Kept
        // tiny — well under one minor grid step (0.1m) so it doesn't
        // perceptibly shorten travel range.
        private const float BlockerMargin = 0.02f;

        // Layers ignored by the drag-time blocker collision check. UI / camera
        // raycast layers and similar non-physical helpers shouldn't block
        // movement even if a stray collider lands on one of them. The mask is
        // computed once at static init: every layer except those listed here.
        private static readonly int BlockerLayerMask = ~(
            (1 << 5)  /* UI */    |
            (1 << 2)  /* Ignore Raycast */
        );

        // Grid + snap defaults. The grid origin is anchored to the playable
        // area's bottom-left corner — i.e. the wall-floor intersection, set
        // at runtime by RefreshPlayableBounds — so the first grid line is
        // flush with the visible wall instead of starting halfway across
        // the floor.
        //
        // Two grid layers are drawn while dragging:
        //   - Major cells (MajorGridStep, 0.5m): heavier line, opaque-ish
        //     green. Reads clearly against the warm floor.
        //   - Minor cells (MinorGridStep, 0.1m): thinner, lower-alpha green.
        //     Implies a 1/5 subdivision inside each major cell so objects
        //     can be nudged in fine increments.
        // Snapping uses MinorGridStep so dragged objects move in the same
        // small steps the player sees on the minor grid lines.
        private const float MajorGridStep = 0.5f;
        private const float MinorGridStep = 0.1f;
        private const float GridLineThickness = 0.015f;
        private const float GridMinorLineThickness = 0.006f;
        private const float GridSurfaceY = 0.005f;
        // Tolerance applied to the grid-line generation loop so that the line
        // sitting exactly on the FloorMax edge isn't dropped due to floating
        // point accumulation when (FloorMax - FloorMin) is an integer multiple
        // of the step size. Kept tiny — well below half a cell.
        private const float GridLoopEpsilon = 0.0001f;
        // Half a minor step is the threshold for "this minor position is
        // actually on a major line"; positions inside that window are skipped
        // when drawing minor lines so the heavier major line wins visually
        // instead of compositing with the dim minor line.
        private const float GridMajorAlignmentEpsilon = MinorGridStep * 0.5f;
        // Bright neon-green so the grid reads clearly against the room's warm
        // floor tones during drag, while keeping ~60% alpha so it doesn't bury
        // the model itself. Minor lines use the same hue but a much lower
        // alpha so the major grid stays visually dominant.
        private static readonly Color GridLineColor = new Color(0.36f, 1f, 0.55f, 0.62f);
        private static readonly Color GridMinorLineColor = new Color(0.36f, 1f, 0.55f, 0.22f);

        // Inventory spawn-time size normalization by source-folder heuristic. The
        // spawned model is uniformly scaled so its largest XZ extent matches the
        // target so a small prop doesn't appear room-sized and a piece of
        // furniture doesn't appear miniature. Aspect ratio is preserved.
        private const float DefaultPropTargetSizeMeters = 0.5f;
        private const float DefaultFurnitureTargetSizeMeters = 1.2f;
        private const float DefaultGenericTargetSizeMeters = 0.8f;

        // Canonical localScale per asset / per scene-instance name. These
        // mirror BookshelfGenerator's per-decoration scale constants so an
        // inventory-spawned BookStack ends up the same size as the BookStack
        // already placed in the scene by the room generator. The lookup key
        // is the asset filename (without extension) for inventory spawns,
        // and the GameObject name for scene-side normalization.
        //
        // Lookup is canonical-first: any entry here wins over the
        // category-based size heuristic above. Add new entries when the
        // scene gets a new "fixed" decoration whose scale differs from the
        // category default.
        private static readonly Dictionary<string, Vector3> CanonicalScaleByAssetFileName = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase)
        {
            { "book_stack", Vector3.one * 0.2f },
            { "lamp_floor", Vector3.one * 0.8f },
            { "rug", Vector3.one * 0.6f },
            { "rug_natural", Vector3.one * 0.6f },
            { "clock_hamham", Vector3.one * 0.42f },
        };

        private static readonly Dictionary<string, Vector3> CanonicalScaleByGameObjectName = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase)
        {
            { "BookStack", Vector3.one * 0.2f },
            { "LampFloor", Vector3.one * 0.8f },
            { "RugNatural", Vector3.one * 0.6f },
        };


        // Bottom menu occupies the bottom 1/3 of the canvas via anchors. The camera
        // viewport rect is fitted into the top 2/3 with the original screen aspect
        // preserved (left/right pillarbox filled with beige).
        private const float BottomMenuFraction = 1f / 3f;
        // Compact category strip — the chips are 56 high with 10px vertical padding,
        // so 96 leaves a small breathing room above and below the buttons.
        private const float ToolbarStripHeight = 96f;

        private static readonly Color BackgroundColor = Hex("#F4EFE6");
        private static readonly Color BottomMenuColor = Hex("#FBF6EC");
        private static readonly Color CardColor = Hex("#FFFDF9");
        private static readonly Color PrimaryColor = Hex("#2B5C8A");
        private static readonly Color SecondaryColor = Hex("#F8F1E4");
        private static readonly Color BorderColor = Hex("#D8CCBC");
        private static readonly Color TextColor = Hex("#2D2418");
        private static readonly Color MutedTextColor = Hex("#7C6A56");
        private static readonly Color SelectionTint = new Color(1f, 0.85f, 0.35f, 1f);
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.55f);

        private static readonly (string Key, string Label)[] ToolbarItems =
        {
            ("ALL", "전체"),
            ("FURNITURE", "가구"),
            ("WALLPAPER", "벽지"),
            ("LIGHT_INNER", "내부조명"),
            ("LIGHT_OUTER", "외부조명"),
            ("OBJECT_SKIN", "필수 오브젝트 스킨"),
            ("THEME_FULL", "전체 테마"),
            ("SHOP", "샵"),
        };

        // FURNITURE 탭 선택 시 노출되는 sub-tab. CustomizationCategory의 외형형 가구 3종.
        // (의자는 Desk = '노트북 & 책상 세트'에 묶여있어 별도 sub-tab 불필요.)
        private static readonly (string Key, string Label)[] FurnitureSubItems =
        {
            ("DESK", "책상"),
            ("BOOKSHELF_SKIN", "책장"),
            ("DASHBOARD_SKIN", "대시보드"),
        };

        private struct InventoryEntry
        {
            public string DisplayName;
            public GameObject Prefab;
            public string SourceAssetPath;
            // 설치 카테고리 — placement 분기에 사용 (BookshelfDeco/FloorDeco/WallDeco).
            // default(0)=Wallpaper 인데 호환성 위해 명시적으로 unset 표현하려면 BookshelfDeco
            // 같은 placement 카테고리 default 채택. 현재 LoadInventoryEntries 는 floor 위
            // placement 만 만들어와서 default = FloorDeco 가 맞다.
            public CustomizationCategory PlacementCategory;
            // 회전 허용 여부. default true. 창문처럼 회전 비활성 entry 는 false.
            public bool AllowRotation;
            // 데코 종류(무엇인가) — NormalizeSpawnedScale 의 카테고리 표준 크기 lookup 키.
            // None 이면 legacy canonical / 폴더 휴리스틱 fallback 으로 backward compatible.
            public DecorationObjectKind Kind;
            // 카테고리 표준 크기에 추가로 곱하는 등방 multiplier. 0=비활성, 1.0=기본,
            // 0.8=80%, 1.2=120%. 비등방(X/Y/Z 개별) override 는 미래 확장.
            public float ScaleMultiplierOverride;
        }

        // 인벤토리 동일 자산 1회 배치 제한용 캐시. Key 는 BuildInventoryKey 가 만든 stable
        // identity (SourceAssetPath → DisplayName → Prefab.name 순). value 는 씬에 살아있는
        // GameObject reference — Destroy 후 dangling 이 되면 PruneInvalidPlacedEntries 가 정리.
        // 서버 인벤토리(assetId/inventoryId) 도입 시 BuildInventoryKey 만 교체하면 그대로 호환.
        private readonly Dictionary<string, GameObject> placedByInventoryKey = new Dictionary<string, GameObject>();

        // Public state ------------------------------------------------------------

        public bool IsEditModeActive { get; private set; }
        public bool IsDecorationPopupOpen { get; private set; }

        // Internal -----------------------------------------------------------------

        private bool isInitialized;
        private TMP_FontAsset fontAsset;
        private TMP_FontAsset fallbackFontAsset;
        private Sprite whiteSprite;
        private Sprite roundedRectSprite;

        private Canvas canvas;
        private GameObject decorationButtonRoot;
        private GameObject decorationPopupRoot;
        private GameObject editOverlayRoot;
        // CanvasGroup 게이트 — IsEditModeActive=false 일 때 blocksRaycasts/interactable=false 로 강제.
        // SetActive race window 와 무관하게 메인씬에서 편집 UI 가 raycast 를 절대 못 막게 보장.
        private CanvasGroup editOverlayCanvasGroup;
        private CanvasGroup selectionPanelCanvasGroup;
        private GameObject bottomMenuRoot;
        private GameObject inventoryContentRoot;
        private GameObject returnButtonRoot;
        private GameObject saveButtonRoot;
        private GameObject unsavedChangesModalRoot;
        // 미저장 모달이 떠 있는 동안 selection action UI(좌/우 회전·삭제 패널)를 숨기고
        // 편집 입력(드래그/회전/삭제/카메라 orbit)을 무시하기 위한 플래그. 모달이 닫히면
        // 다음 Update 의 UpdateSelectionPanel 이 selectedObject 가 살아있는 경우에 한해
        // 패널을 다시 노출한다.
        private bool isUnsavedChangesModalOpen;
        private GameObject saveStatusToastRoot;
        private TMP_Text saveStatusToastText;
        private float saveStatusToastHideTime;
        private GameObject readyModalRoot;
        private GameObject themeApplyModeModalRoot;
        private GameObject customizationLockedModalRoot;
        private TMP_Text customizationLockedBodyText;
        private CustomizationCategory pendingCustomizationCategory;
        private CustomizationEntry pendingCustomizationEntry;
        private GameObject selectionPanelRoot;
        // origin/dev 합류 — WallDecoMarker.AllowRotation==false 면 두 버튼 모두 disabled.
        // 좌/우 회전 분리(이번 브랜치) 와 양립시키기 위해 단일 → 좌/우 두 필드로 확장.
        private Button selectionRotateLeftButton;
        private Button selectionRotateRightButton;

        private string selectedToolbarKey = "ALL";
        private readonly Dictionary<string, Image> toolbarBackgrounds = new Dictionary<string, Image>();
        private readonly Dictionary<string, TMP_Text> toolbarLabels = new Dictionary<string, TMP_Text>();

        // FURNITURE sub-tab. selectedToolbarKey == "FURNITURE" 일 때만 의미 있음.
        private string selectedFurnitureSubKey = "DESK";
        private GameObject furnitureSubToolbarRoot;
        private GameObject inventoryTitleObject;
        private readonly Dictionary<string, Image> furnitureSubBackgrounds = new Dictionary<string, Image>();
        private readonly Dictionary<string, TMP_Text> furnitureSubLabels = new Dictionary<string, TMP_Text>();

        private RoomThemeCatalogAsset themeCatalog;
        private RoomThemeApplier themeApplier;
        private ApplyThemeWithModeUseCase themeUseCase;
        private RoomThemeAsset previousAppliedTheme;
        private RoomThemeAsset pendingThemeApply;
        // 빠른 테마 연타 시 stale callback 무시. 매 클릭마다 ++해서 onSuccess/onError에
        // 캡쳐된 version과 현재 version이 다르면 응답 무시. (MR 72의 walletDetailRequestVersion 동일 패턴)
        private int themeRequestVersion;

        private GameObject placedItemsRoot;

        // Wall placement obstruction registry — RightWall/LeftWall/BackWall surface 의
        // 책장/대시보드 점유 영역 추적. EnsureWallObstructionRegistry 가 lazy 빌드.
        private WallObstructionRegistry wallObstructionRegistry;
        private bool wallObstructionRegistryBuilt;
        private GameObject selectedObject;
        private readonly List<RendererTint> selectedRendererTints = new List<RendererTint>();

        // Drag state. While `draggingObject` is non-null the controller is
        // actively repositioning the selected decoration on every mouse move.
        // `dragLastValidPosition` is updated each frame after clamping, so it
        // doubles as the "settled" position the object will hold on release.
        private GameObject draggingObject;
        private Vector3 dragLastValidPosition;
        private Vector3 dragGrabOffset;             // pointer-floor-hit minus object world XZ at drag start
        // 드래그 시작 시점 position — FinishDrag 에서 epsilon 비교용. 사용자가 단순 선택/확인 클릭 시
        // 좌표가 변하지 않았는데 dirty 가 켜져서 "미저장 변경사항" 모달이 뜨는 회귀 가드.
        // MR #120 skh1205 리뷰 #3 반영.
        private Vector3 dragStartPosition;
        // 드래그 시작 시점 rotation — drop 위치가 invalid 일 때 기존 오브젝트(pending 아님) 를
        // 시작 pose 로 rollback 하는 데 사용. 새 드래그 UX(이동은 자유, 배치만 차단) 도입과 함께 추가.
        private Quaternion dragStartRotation;
        private bool gridSnapEnabled = true;
        private GameObject gridDisplayRoot;

        // 배치 불가 영역 시각화(MR #133/#134) — 드래그 중 사용자의 raw 타깃 위치가 방 footprint
        // 밖이거나 다른 blocker 와 겹치면 [EditPlacement] 로그 + 바닥 footprint UI 가 빨간색으로 전환.
        // 오브젝트 본체 material 은 더 이상 빨강으로 칠하지 않는다(MR #134, AC:HHP 스타일). 검증
        // 로직은 기존 ClampToFloorBounds / IsSpawnPositionBlockedAt / WallObstructionRegistry
        // 를 그대로 재사용하고 결과만 시각/로그로 반영. 상태 전이 시점에만 로그를 남긴다.
        private bool dragLastPlacementValid = true;
        private string dragLastInvalidReason;
        private bool dragValidStateInitialized;
        // 드래그 중 사용자의 raw 타깃 위치(clamp/resolve 적용 전). UpdatePlacementFootprint 가
        // 검증 probe 로 사용 — 실제 transform 은 항상 valid 로 잡혀있지만 사용자가 벽/책장 쪽으로
        // 밀어붙이는 동안 raw 위치는 invalid. footprint 색상이 그 의도를 시각화.
        private Vector3 lastFloorDragRawCandidate;

        // Runtime-derived playable area, populated when edit mode is entered
        // by scanning the actual scene geometry. The grid display, the floor
        // clamp, and any visual reference all read from these fields so the
        // visible grid edges match the real walls — the "where the floor
        // meets the wall" line — instead of an arbitrary hardcoded box. The
        // class constants above are kept as a safe fallback if a scene scan
        // doesn't find the expected named GameObjects.
        private float playableMinX = FloorMinX;
        private float playableMaxX = FloorMaxX;
        private float playableMinZ = FloorMinZ;
        private float playableMaxZ = FloorMaxZ;

        // Snapshot of original localScale values for any scene decoration we
        // canonicalize at edit-mode entry, keyed by Transform reference. On
        // ExitEditMode we restore these values so a designer's manual scale
        // tweak in the inspector isn't permanently overwritten by the edit
        // mode normalization.
        private readonly Dictionary<Transform, Vector3> savedDecorationScales = new Dictionary<Transform, Vector3>();

        // Edit-mode viewport state. We don't capture into a RenderTexture or spawn
        // any auxiliary cameras — instead we shrink the existing main camera's
        // viewport rect to the top portion of the screen (above the bottom menu)
        // and restore the saved rect when leaving edit mode. This preserves the
        // scene's lighting / Volume / post-processing exactly because the same
        // camera is rendering the same way, just into a smaller pixel region.
        private Camera editModeCamera;
        private Rect savedCameraRect;
        private bool savedCameraRectValid;
        // 편집 모드 진입 시 카메라 배경(clearFlags + backgroundColor) 캐싱 — 벽 fade 시 방 바깥이
        // skybox/기본 회색으로 비치는 것을 막기 위해 진입 중에는 SolidColor + black 으로 강제하고,
        // 종료 시 메인룸 setting(skybox 등) 으로 원복. rect/pose 캐싱과 같은 라이프사이클.
        private CameraClearFlags savedCameraClearFlags;
        private Color savedCameraBackgroundColor;
        private bool savedCameraBackgroundValid;

        // 동물의 숲 해피홈 계열 편집 UX — 자유 orbit + 미니 큐브 UI 조합.
        //
        // 이전 버전의 "preset cycle" 모델 (Default + 4개 시점 순환) 은 제거하고, orbit 상태
        // (yaw/pitch/distance) 를 직접 조작하는 자유 회전 + W/S/스크롤 줌 + 큐브 면 클릭으로
        // 즉시 점프하는 모델로 전환. CameraPreset enum 자체는 wall fade 카테고리 식별용으로
        // 여전히 사용 (큐브 면 클릭 / 기본 시점 / A·D 회전 결과 분류).
        private enum CameraPreset
        {
            Default,    // 기본 편집뷰 (orbit 기본값 — 방을 자연스럽게 내려다보는 사선)
            TopDown,    // 큐브 상면 — 위에서 직각 내려보기
            Front,      // 큐브 전면 — 카메라 -Z 방향 outside, +Z 향함 (책장 뒷벽 시점)
            Back,       // 큐브 후면 — 카메라 +Z 방향 outside, -Z 향함 (열린 면 시점)
            LeftAngle,  // 큐브 좌면 — 카메라 -X outside, +X 향함
            RightAngle, // 큐브 우면 — 카메라 +X outside, -X 향함
        }
        private static readonly Dictionary<CameraPreset, string> CameraPresetLabels = new Dictionary<CameraPreset, string>
        {
            { CameraPreset.Default,    "기본 편집뷰" },
            { CameraPreset.TopDown,    "탑다운 뷰" },
            { CameraPreset.Front,      "정면 뷰" },
            { CameraPreset.Back,       "후면 뷰" },
            { CameraPreset.LeftAngle,  "좌측 뷰" },
            { CameraPreset.RightAngle, "우측 뷰" },
        };

        // ---- Orbit camera state ----
        // 카메라 pose = (orbitCenter, orbitYaw, orbitPitch, orbitDistance) — selectedObject 무관.
        // orbitCenter 는 카메라가 회전하는 기준점:
        //   - 기본 시점 / TopDown: roomCenter (방 중앙)
        //   - 벽면 포커스(전/좌/우/후): 해당 벽 중앙 midpoint → distance 가 그대로 "카메라-벽 거리"
        //     가 되어 벽면을 가까이 볼 수 있음.
        // yaw 0° = -Z 쪽 (orbitCenter 의 전면), 90° = +X (orbitCenter 의 우측),
        // 180° = +Z (orbitCenter 의 후면), 270° = -X (orbitCenter 의 좌측).
        // pitch 0° = 수평, 90° = 직각 위에서 내려봄. distance = orbitCenter 에서의 반경.
        private Vector3 orbitCenter;
        private float orbitYaw;
        private float orbitPitch;
        private float orbitDistance;

        // 기본값 (Default 뷰 진입 시 사용)
        private const float DefaultOrbitYaw = 25f;
        private const float DefaultOrbitPitch = 18f;
        private const float DefaultOrbitDistance = 4.2f;
        // Absolute hard limit — 어떤 시점에서도 이 범위를 벗어나지 못함. 시점별 SerializeField
        // (editZoomMin/Max...) 가 더 좁은 범위를 정의하면 그 값이 우선 적용된다. const 는 시점별
        // 값이 미설정/이상값일 때 fallback + 안전 가드 역할.
        private const float MinOrbitDistance = 0.5f;
        private const float MaxOrbitDistance = 8.0f;
        private const float OrbitMinPitch = 5f;
        private const float OrbitMaxPitch = 89f;
        // A/D 회전 속도 (deg/sec). 줌 속도/감도는 디자이너 튜닝을 위해 SerializeField 로 노출 (아래).
        private const float OrbitYawSpeed = 90f;

        // 편집 모드 줌 튜닝 — 시점(CameraPreset) 별 (min, max) 분리. 동물의 숲 해피홈 식
        // "벽면 포커스 시점은 좁고 일정한 거리, 사선/탑다운은 방 전체가 보이는 거리" 정책.
        // 마우스 휠/W/S 모두 같은 clamp(GetOrbitDistanceRangeForPreset 결과) 를 사용하므로
        // 입력 종류에 따른 회귀 없음. 메인룸/책장/노트북 포커스 카메라는 이 controller 와 무관 →
        // 영향 없음.
        [SerializeField, Range(0.5f, 5.0f)] private float editZoomKeySpeed = 3.0f;
        [SerializeField, Range(0.1f, 2.0f)] private float editZoomScrollSensitivity = 0.6f;
        [SerializeField] private float editZoomMinDefault = 2.0f;
        [SerializeField] private float editZoomMaxDefault = 6.0f;
        [SerializeField] private float editZoomMinWallFocus = 1.5f;
        [SerializeField] private float editZoomMaxWallFocus = 4.5f;
        [SerializeField] private float editZoomMinTopDown = 3.5f;
        [SerializeField] private float editZoomMaxTopDown = 7.0f;

        // 시점별 줌 (min, max) 반환. SerializeField 값을 hard limit (MinOrbitDistance,
        // MaxOrbitDistance) 안으로 클램프 — 디자이너가 인스펙터에서 0 또는 큰 값 실수 입력해도
        // 안전. min > max 인 경우 (잘못된 인스펙터 값) 도 swap 으로 방어.
        private (float min, float max) GetOrbitDistanceRangeForPreset(CameraPreset preset)
        {
            float rawMin, rawMax;
            switch (preset)
            {
                case CameraPreset.TopDown:
                    rawMin = editZoomMinTopDown; rawMax = editZoomMaxTopDown; break;
                case CameraPreset.Front:
                case CameraPreset.Back:
                case CameraPreset.LeftAngle:
                case CameraPreset.RightAngle:
                    rawMin = editZoomMinWallFocus; rawMax = editZoomMaxWallFocus; break;
                case CameraPreset.Default:
                default:
                    rawMin = editZoomMinDefault; rawMax = editZoomMaxDefault; break;
            }
            float min = Mathf.Clamp(rawMin, MinOrbitDistance, MaxOrbitDistance);
            float max = Mathf.Clamp(rawMax, MinOrbitDistance, MaxOrbitDistance);
            if (min > max) (min, max) = (max, min);
            return (min, max);
        }

        // 한 프레임 내에서 99% 도달하는 시간(초). 작을수록 빠른 추격, 클수록 부드러움.
        private const float CameraPresetLerpTimeConstant = 0.18f;
        // 현재 활성 wall-fade 카테고리. 큐브 면 클릭/Default/A·D 회전 결과로 갱신.
        private CameraPreset currentCameraPreset = CameraPreset.Default;
        // 직전에 wall fade 가 적용된 카테고리 — A/D 연속 회전 중 quadrant 가 바뀔 때만 ApplyWallVisibility 재호출.
        private CameraPreset lastAppliedFadePreset = CameraPreset.Default;
        // "기본 시점" 모드인지 — 진입/Default 버튼 클릭 시 true. 자유 회전/줌/큐브 클릭 시 false.
        // wall fade 정책: true 면 항상 fade=none (Default 정책), false 면 ClassifyByOrbit 결과 사용.
        private bool isInDefaultView = true;
        private Vector3 cameraPresetTargetPosition;
        private Quaternion cameraPresetTargetRotation;
        private bool cameraPresetTargetActive;
        // 진입 시점의 카메라 pose — ExitEditMode 의 복구 기준점.
        private Vector3 savedCameraPosition;
        private Quaternion savedCameraRotation;
        private bool savedCameraPoseValid;

        // 편집 모드 전용 벽 occlusion 처리 — 동물의 숲/심즈류 가구 배치 UX 처럼 카메라 시야를
        // 가리는 벽을 임시로 alpha fade. 벽 GameObject 자체는 건드리지 않고 (Collider 유지 →
        // 충돌/배치 검증 그대로 동작) Renderer 의 material instance 만 transparent 로 swap.
        // 종료 시 sharedMaterial 원본 reference 로 되돌리고 instance material 은 Destroy.
        private enum WallGroup { Left, Right, Back, Front, Ceiling }
        private sealed class WallRendererCache
        {
            public Renderer Renderer;
            public Material InstanceMaterial;   // 우리가 만든 instance — Destroy 책임 있음.
            public Material SharedOriginal;     // 진입 시 sharedMaterial 스냅샷.
            public bool RendererWasEnabled;
            public WallGroup Group;
        }
        private readonly List<WallRendererCache> wallRendererCaches = new List<WallRendererCache>();
        private bool wallVisibilityCollected;
        // 0.18 → 내부가 충분히 보이면서도 벽 윤곽선은 살짝 인지되는 정도. 디자이너가 인스펙터에서
        // 바로 튜닝할 수 있도록 SerializeField 노출 — MR #120 simonjiho 리뷰 #5 반영.
        // [0..1] 클램프는 ApplyWallFadeState 에서 매 적용 시 보장.
        [SerializeField, Range(0f, 1f)] private float wallFadeAlpha = 0.18f;

        // ---- Decoration save / dirty state ----
        // 명시적 "저장" 버튼 흐름. PlaceInventoryEntry / FinishDrag / RotateSelectedBy /
        // DeleteSelectedObject 직후 SetDirty 만 호출하고, 실제 영속화는 OnSaveButtonClicked 에서만.
        // 네트워크 부담 + 의도치 않은 부분 저장 방지.
        private bool isDirty;
        private SaveDecorationsUseCase saveDecorationsUseCase;
        private LoadDecorationsUseCase loadDecorationsUseCase;
        private IDecorationRepository decorationRepository;

        // 편집 진입 시점 marker 스냅샷 — "저장하지 않고 나가기" 롤백 기준점.
        // InstanceAtEntry 는 Unity 의 destroyed-pattern 으로 살아있는지 판별. 살아있으면 transform 만
        // 복원, destroy 됐으면 SourcePrefab 으로 재생성. SourcePrefab 이 null 이면 복원 불가 (scene-baked
        // marker 등 inventoryEntries 에 없는 케이스 — 진단 로그 후 skip).
        private struct DecorationSnapshot
        {
            public string InventoryKey;
            public GameObject SourcePrefab;
            public string DisplayName;
            public GameObject InstanceAtEntry;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 LocalScale;
        }
        private readonly List<DecorationSnapshot> editEntrySnapshot = new List<DecorationSnapshot>();

        // Scene-init saved decoration load guard (MR #128).
        // BookshelfSceneInstaller.InstallIntoActiveScene 가 끝나면서 LoadSavedDecorationsAfterSceneInstall
        // 을 호출했고 그 결과가 성공이었는지 — succeeded 의미. EnterEditMode 가 같은 데이터를 다시
        // 읽어 marker 를 중복 spawn 하거나 transform 을 두 번 갱신하지 않게 차단한다.
        //
        // 중요: 시도 여부가 아니라 "성공한 적 있는가" 를 기록한다. 파일 손상/JSON 파싱 실패/향후
        // ApiDecorationRepository 네트워크 실패 등으로 onError 가 발사되면 이 플래그는 false 로
        // 남고, 다음 EnterEditMode 진입 시 fallback 경로로 한 번 더 시도된다 (MR #128 skh1205 리뷰
        // #2 / revbot 리뷰 반영 — 이전엔 시도 즉시 true 로 set 해서 실패가 영구 차단됐었다).
        private bool hasLoadedSavedDecorationsOnSceneInit;

        // Pending placement (Preview) state (MR #122).
        // 인벤토리에서 새로 추가된 오브젝트는 "배치 확정" 버튼을 누르기 전까지 반투명 ghost 로
        // 표시한다. 확정 전엔 CollectDecorationLayoutItems 가 저장 대상에서 제외하고, 미저장
        // 종료 시 RollbackToEditEntrySnapshot 의 alive 비교로 자동 destroy 된다 (편집 entry
        // 시점 snapshot 에 없는 항목은 모두 제거되는 기존 동작 그대로).
        //
        //   pendingPlacements        — pending 상태인 GameObject set.
        //   pendingMaterialBackups   — Renderer 별 원본 sharedMaterials[] 배열. 확정/롤백 시
        //                              그대로 r.sharedMaterials 에 복원해 원본 머터리얼 자산을
        //                              건드리지 않는다 (spec: sharedMaterial 직접 수정 금지).
        //   previewMaterialTemplate  — runtime-생성 transparent 템플릿. Renderer 마다 별도
        //                              new Material(template) 인스턴스를 생성해 분리하므로,
        //                              wall validity 의 빨간 tint 가 다른 pending 항목으로
        //                              번지지 않는다. 자식 Renderer 도 동일 처리.
        private readonly HashSet<GameObject> pendingPlacements = new HashSet<GameObject>();
        private readonly Dictionary<Renderer, Material[]> pendingMaterialBackups = new Dictionary<Renderer, Material[]>();
        private Material previewMaterialTemplate;
        private GameObject confirmPlacementButtonRoot;
        private Button confirmPlacementButton;

        // Decoration catalog loaded from Resources (built by the editor menu item).
        private readonly List<InventoryEntry> inventoryEntries = new List<InventoryEntry>();

        private BookshelfExperienceManager cachedExperienceManager;
        private BookshelfApiSyncController cachedBookshelfApiSyncController;
        private Camera cachedSceneCamera;

        private struct RendererTint
        {
            public Renderer Renderer;
            public Color OriginalColor;
        }

        // 배치 미리보기 footprint UI (MR #134) — 오브젝트 본체 색을 건드리지 않고 바닥에
        // 초록(valid)/빨강(invalid) Quad+Border 로 배치 가능 여부를 표시한다. Animal Crossing:
        // Happy Home Paradise 의 가구 배치 UI 와 같은 방식. material/mesh 모두 hide 플래그로
        // 자산화되지 않게 만들고 Dispose 에서 명시 destroy — Play Mode 종료 후 Scene/Prefab
        // 에 잔류하지 않는다. drag/pending 양쪽에서 같은 인스턴스를 재사용 (중복 생성 방지).
        private sealed class PlacementFootprintPreview
        {
            private static readonly Color ValidFillColor = new Color(0.35f, 0.95f, 0.50f, 0.30f);
            private static readonly Color ValidBorderColor = new Color(0.20f, 0.85f, 0.40f, 0.95f);
            private static readonly Color InvalidFillColor = new Color(1.00f, 0.45f, 0.45f, 0.32f);
            private static readonly Color InvalidBorderColor = new Color(1.00f, 0.30f, 0.30f, 0.95f);

            private const float FootprintY = FloorPlaneY + 0.01f; // 바닥 z-fight 회피.
            private const float BorderLift = 0.001f;              // border 가 fill 위로 살짝 떠서 그려지도록.
            private const float BorderWidth = 0.02f;
            private const float MinSize = 0.05f;
            // Unity 빌트인 layer 2 = "Ignore Raycast". BlockerLayerMask 의 ~(1<<5 | 1<<2)
            // 와 직결되어 정책 동기화 필요 — 사용자 layer 이름 변경에 흔들리지 않게 정수
            // 상수로 고정. 빌트인 layer index 는 Unity 가 보장.
            // **fillObject / borderObject 에만 적용**. root 는 빈 GameObject (Renderer/Collider
            // 없음) 이므로 layer 변경 불필요 — 자식 layer 가 raycast/OverlapBox 에 직접 보인다.
            private const int IgnoreRaycastLayer = 2;

            private GameObject root;
            private GameObject fillObject;
            private GameObject borderObject;
            private MeshRenderer fillRenderer;
            private LineRenderer borderLine;
            private Material fillMaterial;
            private Material borderMaterial;
            private Mesh quadMesh;

            private bool isShown;
            private bool? lastIsValid;
            private string lastReason;
            private string lastObjectName;

            public bool IsShown => isShown;

            public void Show(Scene scene, Bounds worldBounds, bool isValid, string reason, string objectName)
            {
                EnsureBuilt(scene);
                ApplyTransform(worldBounds);
                ApplyColor(isValid);

                if (!isShown)
                {
                    if (root != null) root.SetActive(true);
                    isShown = true;
                    Debug.Log($"[EditFootprint] show object='{objectName ?? "<null>"}' valid={(isValid ? "True" : "False")}");
                }
                else if (lastIsValid != isValid || lastObjectName != objectName || lastReason != reason)
                {
                    if (isValid)
                    {
                        Debug.Log($"[EditFootprint] update object='{objectName ?? "<null>"}' valid=True");
                    }
                    else
                    {
                        Debug.Log($"[EditFootprint] update object='{objectName ?? "<null>"}' valid=False reason='{reason ?? "Unknown"}'");
                    }
                }

                lastIsValid = isValid;
                lastReason = reason;
                lastObjectName = objectName;
            }

            public void Hide()
            {
                if (!isShown) return;
                if (root != null) root.SetActive(false);
                isShown = false;
                lastIsValid = null;
                lastReason = null;
                lastObjectName = null;
                Debug.Log("[EditFootprint] hide");
            }

            public void Dispose()
            {
                Hide();
                if (root != null) UnityEngine.Object.Destroy(root);
                if (fillMaterial != null) UnityEngine.Object.Destroy(fillMaterial);
                if (borderMaterial != null) UnityEngine.Object.Destroy(borderMaterial);
                if (quadMesh != null) UnityEngine.Object.Destroy(quadMesh);
                root = null;
                fillObject = null;
                borderObject = null;
                fillRenderer = null;
                borderLine = null;
                fillMaterial = null;
                borderMaterial = null;
                quadMesh = null;
            }

            private void EnsureBuilt(Scene scene)
            {
                if (root != null) return;

                var shader = Shader.Find("Sprites/Default");
                fillMaterial = new Material(shader)
                {
                    name = "FootprintFill",
                    hideFlags = HideFlags.HideAndDontSave,
                    color = ValidFillColor,
                };
                borderMaterial = new Material(shader)
                {
                    name = "FootprintBorder",
                    hideFlags = HideFlags.HideAndDontSave,
                    color = ValidBorderColor,
                };

                root = new GameObject("EditPlacementFootprint");
                // 회귀 fix — 이전엔 HideAndDontSave 였으나 비정상 종료(domain reload / Play 강제 stop)
                // 시 stale root 가 DontDestroyOnLoad 에 살아남는 회귀에서 사용자가 Inspector 로 식별
                // 못 하는 dead helper 가 됐다. DontSave 면 자산화는 여전히 막히지만 Hierarchy/Inspector
                // 는 정상 — 회귀 시 사용자가 즉시 식별/삭제 가능 + 향후 디버깅에서 좌표/색 확인 용이.
                root.hideFlags = HideFlags.DontSave;
                if (scene.IsValid()) SceneManager.MoveGameObjectToScene(root, scene);
                root.SetActive(false);

                quadMesh = BuildUnitQuadXZ();

                fillObject = new GameObject("Fill");
                fillObject.hideFlags = HideFlags.DontSave;
                // 자기 자신이 blocker 로 잡히는 회귀 차단 — EnsureBlockerColliders 가 활성 씬의
                // 모든 Renderer GO 에 BoxCollider 를 자동 부착하는데, fill quad 가 그 대상에 들어가
                // OverlapBox 에 obstacle 로 잡혀 "Fill" hit 으로 확정 버튼 disable / 회전 revert 가
                // 발생했다. BlockerLayerMask 가 IgnoreRaycast(layer 2) 를 제외하므로 layer 만 옮겨도
                // OverlapBox 가 무시. **fillObject 한정** — placed decoration / inventory placement
                // 본체에는 절대 적용하지 않는다 (선택 raycast 가 같은 layer 를 무시하면 선택 불가).
                fillObject.layer = IgnoreRaycastLayer;
                fillObject.transform.SetParent(root.transform, false);
                var filter = fillObject.AddComponent<MeshFilter>();
                filter.sharedMesh = quadMesh;
                fillRenderer = fillObject.AddComponent<MeshRenderer>();
                fillRenderer.sharedMaterial = fillMaterial;
                fillRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                fillRenderer.receiveShadows = false;
                fillRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                fillRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                fillRenderer.allowOcclusionWhenDynamic = false;

                borderObject = new GameObject("Border");
                borderObject.hideFlags = HideFlags.DontSave;
                // fillObject 와 동일 이유 — border line 도 LineRenderer 로 잡힐 가능성에 대비.
                // **borderObject 한정** — placed decoration 본체에는 절대 적용 X.
                borderObject.layer = IgnoreRaycastLayer;
                borderObject.transform.SetParent(root.transform, false);
                borderLine = borderObject.AddComponent<LineRenderer>();
                borderLine.useWorldSpace = true;
                borderLine.positionCount = 5;
                borderLine.loop = false;
                borderLine.startWidth = BorderWidth;
                borderLine.endWidth = BorderWidth;
                borderLine.numCornerVertices = 0;
                borderLine.numCapVertices = 0;
                borderLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                borderLine.receiveShadows = false;
                borderLine.material = borderMaterial;
                borderLine.alignment = LineAlignment.View;
            }

            private void ApplyTransform(Bounds worldBounds)
            {
                var sx = Mathf.Max(MinSize, worldBounds.size.x);
                var sz = Mathf.Max(MinSize, worldBounds.size.z);
                var cx = worldBounds.center.x;
                var cz = worldBounds.center.z;

                fillObject.transform.position = new Vector3(cx, FootprintY, cz);
                fillObject.transform.rotation = Quaternion.identity;
                fillObject.transform.localScale = new Vector3(sx, 1f, sz);

                var halfX = sx * 0.5f;
                var halfZ = sz * 0.5f;
                var y = FootprintY + BorderLift;
                borderLine.SetPosition(0, new Vector3(cx - halfX, y, cz - halfZ));
                borderLine.SetPosition(1, new Vector3(cx + halfX, y, cz - halfZ));
                borderLine.SetPosition(2, new Vector3(cx + halfX, y, cz + halfZ));
                borderLine.SetPosition(3, new Vector3(cx - halfX, y, cz + halfZ));
                borderLine.SetPosition(4, new Vector3(cx - halfX, y, cz - halfZ));
            }

            private void ApplyColor(bool isValid)
            {
                fillMaterial.color = isValid ? ValidFillColor : InvalidFillColor;
                borderMaterial.color = isValid ? ValidBorderColor : InvalidBorderColor;
            }

            private static Mesh BuildUnitQuadXZ()
            {
                var mesh = new Mesh
                {
                    name = "FootprintUnitQuad",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                mesh.vertices = new[]
                {
                    new Vector3(-0.5f, 0f, -0.5f),
                    new Vector3( 0.5f, 0f, -0.5f),
                    new Vector3( 0.5f, 0f,  0.5f),
                    new Vector3(-0.5f, 0f,  0.5f),
                };
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                mesh.uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f),
                };
                mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        private PlacementFootprintPreview placementFootprint;

        // Auto-bootstrap ----------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterAutoBootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != SceneKeys.Main)
            {
                return;
            }
            EnsureControllerExists(scene);
        }

        private static void EnsureControllerExists(Scene scene)
        {
            if (UnityEngine.Object.FindFirstObjectByType<RoomEditModeController>() != null)
            {
                return;
            }

            var hostObject = new GameObject(nameof(RoomEditModeController));
            if (scene.IsValid())
            {
                SceneManager.MoveGameObjectToScene(hostObject, scene);
            }
            hostObject.AddComponent<RoomEditModeController>();
        }

        private void Awake()
        {
            InitializeIfNeeded();
            // 상단 메뉴 'Dotshelf > Theme > Filter Inventory By > ...' 가 RoomEditThemeFilter.ActiveTheme 을
            // 바꾸면 Changed 가 발사된다 → 편집 모드 활성 상태면 인벤토리 다시 그려 필터 반영.
            RoomEditThemeFilter.Changed += HandleThemeFilterChanged;
        }

        // MR #128 skh1205 리뷰 #1 반영: 이전 구현은 controller.Start() 에서 자체적으로 load 를
        // 트리거했으나, 실제 부트스트랩 흐름(SceneEntry.LoadMainSceneRoutine → additive load →
        // sceneLoaded → controller AddComponent → Start; 그 다음에서야 RunSceneInstallers 가
        // installer.InstallScene 호출)에서는 Start 가 InstallScene 보다 *먼저* 실행될 수 있다.
        // BookshelfSceneInstaller.Awake 는 active scene 이 Main 이 아니면 early-return 하므로
        // additive load 시점에 InstallScene 이 안 도는 경로 존재. 따라서 자체 Start 트리거를
        // 제거하고, InstallScene 이 끝나는 지점에서 BookshelfSceneInstaller 가 명시적으로
        // LoadSavedDecorationsAfterSceneInstall() 을 호출하는 방식으로 단일화 (B안).

        private void HandleThemeFilterChanged()
        {
            if (!IsEditModeActive) return;
            ApplyThemeFilterLabel();
            RebuildInventorySlots();
        }

        private void InitializeIfNeeded()
        {
            if (isInitialized)
            {
                return;
            }
            isInitialized = true;

            whiteSprite = CreateWhiteSprite();
            roundedRectSprite = CreateRoundedRectSprite(64, 14);
            fontAsset = ResolveFontAsset();
            WarmupGlyphs(fontAsset);
            EnsureEventSystem();
            LoadInventoryEntries();

            BuildCanvas();
            BuildDecorationButton();
            BuildDecorationPopup();
            BuildEditOverlay();
            BuildReadyModal();
            BuildThemeApplyModeModal();
            BuildCustomizationLockedModal();
            BuildSelectionPanel();

            // 회귀 cleanup — 이전 세션 비정상 종료(domain reload / Play 강제 stop) 로 footprint
            // preview root 가 DontDestroyOnLoad 또는 다른 scene 에 stale 로 남은 경우 제거.
            // PlacementFootprintPreview 는 lazy build 라 controller 가 새로 만들어진 시점엔
            // root 도 새로 만들어야 정상. exact name match 로 다른 GameObject 오인 destroy 차단.
            // FindObjectsByType 가 hideFlags 무관하게 잡아주므로 HideAndDontSave 잔류물도 식별.
            var staleFootprints = GameObject.FindObjectsByType<GameObject>(
                UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
            for (var i = 0; i < staleFootprints.Length; i++)
            {
                var stale = staleFootprints[i];
                if (stale != null && stale.name == "EditPlacementFootprint")
                {
                    Debug.Log($"[EditFootprint] cleanup stale root scene='{stale.scene.name}' hideFlags={stale.hideFlags}");
                    UnityEngine.Object.Destroy(stale);
                }
            }

            placementFootprint = new PlacementFootprintPreview();

            CloseDecorationPopupInternal();
            ExitEditModeInternal();
            HideReadyModal();
            HideThemeApplyModeModal();
            HideCustomizationLockedModal();
        }

        // Resolve the persisted EditableDecorationCatalog (built by the Tools menu in
        // the editor, shipped as a Resources asset). When that asset is missing or
        // empty AND we're running inside the Unity editor, fall back to scanning
        // Assets/Arts/Models via AssetDatabase so the inventory still populates
        // without the player having to bake the catalog first. In standalone
        // builds the editor branch is compiled out and the catalog is the single
        // source of truth.
        private void LoadInventoryEntries()
        {
            inventoryEntries.Clear();

            var catalog = Resources.Load<EditableDecorationCatalog>("EditableDecorationCatalog");
            if (catalog != null)
            {
                for (var i = 0; i < catalog.Entries.Count; i++)
                {
                    var entry = catalog.Entries[i];
                    if (entry == null || entry.Prefab == null) continue;
                    // catalog Entry 의 PlacementCategory 가 Wallpaper(=enum 0, unset 의미)
                    // 면 FloorDeco 로 fallback — 기존 catalog 자산 호환.
                    var placement = entry.PlacementCategory == CustomizationCategory.Wallpaper
                        ? CustomizationCategory.FloorDeco
                        : entry.PlacementCategory;
                    inventoryEntries.Add(new InventoryEntry
                    {
                        DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.Prefab.name : entry.DisplayName,
                        Prefab = entry.Prefab,
                        SourceAssetPath = entry.SourceAssetPath ?? string.Empty,
                        PlacementCategory = placement,
                        AllowRotation = entry.AllowRotation,
                        Kind = entry.Kind,
                        ScaleMultiplierOverride = entry.ScaleMultiplierOverride,
                    });
                    // 회귀 진단 — catalog → InventoryEntry 매핑 결과를 1회 로그. AllowRotation 이
                    // FloorDeco 에 false 로 새거나 Kind 가 None 으로 회귀했는지 즉시 식별 가능.
                    Debug.Log(
                        $"[EditCatalog] entry='{entry.DisplayName}' prefab='{entry.Prefab.name}' " +
                        $"kind={entry.Kind} placement={placement} allowRotation={entry.AllowRotation} " +
                        $"scaleOverride={entry.ScaleMultiplierOverride:0.##} sourcePath='{entry.SourceAssetPath}'");
                }
            }

#if UNITY_EDITOR
            if (inventoryEntries.Count == 0)
            {
                AddEditorTimeFallbackEntries();
                if (inventoryEntries.Count > 0)
                {
                    Debug.Log($"[RoomEditModeController] Catalog missing/empty — discovered {inventoryEntries.Count} model(s) under Assets/Arts/Models via AssetDatabase fallback. Run Tools > Dotshelf > Refresh Decoration Catalog to bake into a build-friendly Resources asset.");
                }
            }
#endif

            if (inventoryEntries.Count == 0)
            {
                Debug.LogWarning("[RoomEditModeController] No decoration entries available. Run Tools > Dotshelf > Refresh Decoration Catalog (in editor) or include EditableDecorationCatalog.asset under Assets/Resources for builds.");
            }
        }

#if UNITY_EDITOR
        // Editor-only: enumerate everything that imports as a GameObject under
        // Assets/Arts/Models so 가구 / 전체 categories light up immediately on a
        // fresh checkout without forcing the developer to run the menu first.
        private void AddEditorTimeFallbackEntries()
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Arts/Models" });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path)) continue;
                var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                if (ext != ".glb" && ext != ".fbx" && ext != ".prefab" && ext != ".obj") continue;

                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                inventoryEntries.Add(new InventoryEntry
                {
                    DisplayName = ToDisplayName(System.IO.Path.GetFileNameWithoutExtension(path)),
                    Prefab = prefab,
                    SourceAssetPath = path,
                    PlacementCategory = CustomizationCategory.FloorDeco,
                    AllowRotation = true,
                });
            }

            inventoryEntries.Sort((a, b) =>
                string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase));
        }

        private static string ToDisplayName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "decoration";
            var parts = raw.Replace('-', ' ').Replace('_', ' ').Split(' ');
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0) continue;
                parts[i] = char.ToUpperInvariant(parts[i][0]) +
                           (parts[i].Length > 1 ? parts[i].Substring(1).ToLowerInvariant() : string.Empty);
            }
            return string.Join(" ", parts);
        }
#endif

        // Map toolbar category → which inventory entries to surface. "전체" lists every
        // catalog entry; "가구" 탭은 바닥 placement 가능한 모든 항목 (이전 /Props/ + /Furniture/
        // 폴더 의미와 동일). 분류는 PlacementCategory 메타데이터로 결정 — GLB 평탄화(MR #154)
        // 이후 폴더 경로 기반은 무효. 남은 카테고리들은 wireframe placeholders 라
        // content pipeline 생기기 전까지 빈 list 의도.
        // (MR #154 review #2: 주석은 metadata 기반인데 코드는 여전히 path 기반이라 평탄화 후
        // FURNITURE 탭이 비는 회귀 fix.)
        private static bool MatchesCategory(InventoryEntry entry, string categoryKey)
        {
            switch (categoryKey)
            {
                case "ALL":
                    return true;
                case "FURNITURE":
                    // WallDeco (창문/네온사인) 와 BookshelfDeco 는 별도 placement → 가구 탭 제외.
                    // 나머지 (Wallpaper loader fallback → FloorDeco 포함, FloorDeco 자체)는 바닥 데코로 surface.
                    return entry.PlacementCategory != CustomizationCategory.WallDeco
                        && entry.PlacementCategory != CustomizationCategory.BookshelfDeco;
                default:
                    return false;
            }
        }

        // Build canvas -------------------------------------------------------------

        private void BuildCanvas()
        {
            var canvasObject = new GameObject(CanvasName, typeof(RectTransform));
            canvasObject.layer = UiLayer;
            SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene);

            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = CanvasSortingOrder;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            canvasObject.AddComponent<GraphicRaycaster>();

            var rect = (RectTransform)canvasObject.transform;
            StretchToParent(rect);
        }

        private void BuildDecorationButton()
        {
            decorationButtonRoot = CreateRectObject("DecorationButtonRoot", canvas.transform);
            var rect = (RectTransform)decorationButtonRoot.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            // Stack: ModeChange (top, y = -32), Settings (y = -104), Decoration (y = -176).
            var stackedY = -(ButtonMargin + (ButtonHeight + ButtonStackGap) * 2);
            rect.anchoredPosition = new Vector2(-ButtonMargin, stackedY);

            var button = CreateButton(
                decorationButtonRoot.transform,
                "DecorationButton",
                "꾸미기",
                CardColor,
                TextColor,
                new Vector2(ButtonWidth, ButtonHeight),
                true,
                BorderColor);
            var buttonRect = (RectTransform)button.transform;
            StretchToParent(buttonRect);
            button.onClick.AddListener(OnDecorationButtonClicked);

            var shadow = button.gameObject.AddComponent<Shadow>();
            shadow.effectColor = WithAlpha(TextColor, 0.18f);
            shadow.effectDistance = new Vector2(0f, -3f);
        }

        private void BuildDecorationPopup()
        {
            decorationPopupRoot = CreateRectObject("DecorationPopup", canvas.transform);
            var rect = (RectTransform)decorationPopupRoot.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight * 2 + ButtonStackGap + 24f);
            // Sits directly below the decoration button.
            var popupY = -(ButtonMargin + (ButtonHeight + ButtonStackGap) * 3 - ButtonStackGap);
            rect.anchoredPosition = new Vector2(-ButtonMargin, popupY);

            var card = CreateImage(decorationPopupRoot.transform, "Card", CardColor);
            card.sprite = roundedRectSprite;
            card.type = Image.Type.Sliced;
            StretchToParent(card.rectTransform);
            var cardOutline = card.gameObject.AddComponent<Outline>();
            cardOutline.effectColor = WithAlpha(BorderColor, 0.95f);
            cardOutline.effectDistance = new Vector2(1f, -1f);
            var cardShadow = card.gameObject.AddComponent<Shadow>();
            cardShadow.effectColor = WithAlpha(TextColor, 0.20f);
            cardShadow.effectDistance = new Vector2(0f, -4f);

            var editButton = CreateButton(
                decorationPopupRoot.transform, "EditButton", "편집",
                PrimaryColor, Color.white, new Vector2(ButtonWidth - 24f, ButtonHeight - 8f),
                false, BorderColor);
            var editRect = (RectTransform)editButton.transform;
            editRect.anchorMin = new Vector2(0.5f, 1f);
            editRect.anchorMax = new Vector2(0.5f, 1f);
            editRect.pivot = new Vector2(0.5f, 1f);
            editRect.anchoredPosition = new Vector2(0f, -12f);
            editButton.onClick.AddListener(OnEditMenuItemClicked);

            var shopButton = CreateButton(
                decorationPopupRoot.transform, "ShopButton", "샵",
                SecondaryColor, TextColor, new Vector2(ButtonWidth - 24f, ButtonHeight - 8f),
                true, BorderColor);
            var shopRect = (RectTransform)shopButton.transform;
            shopRect.anchorMin = new Vector2(0.5f, 1f);
            shopRect.anchorMax = new Vector2(0.5f, 1f);
            shopRect.pivot = new Vector2(0.5f, 1f);
            shopRect.anchoredPosition = new Vector2(0f, -(12f + ButtonHeight - 8f + ButtonStackGap));
            shopButton.onClick.AddListener(ShowReadyModal);
        }

        // The main camera renders the room directly into the top portion of the
        // screen (via Camera.rect). Pillarbox panels on the left/right of the
        // camera viewport fill what's left of the top area with beige so the
        // pillarboxing reads as part of the room palette instead of black bars.
        private void BuildEditOverlay()
        {
            editOverlayRoot = CreateRectObject("EditOverlay", canvas.transform);
            StretchToParent((RectTransform)editOverlayRoot.transform);

            // CanvasGroup 게이트 — UpdateButtonVisibility 가 매 frame IsEditModeActive 따라
            // blocksRaycasts/interactable 토글. SetActive 와 이중 안전망으로 메인씬 회귀 방지.
            editOverlayCanvasGroup = editOverlayRoot.AddComponent<CanvasGroup>();
            editOverlayCanvasGroup.blocksRaycasts = false;
            editOverlayCanvasGroup.interactable = false;

            BuildBottomMenu(editOverlayRoot.transform);
            BuildPillarboxFills(editOverlayRoot.transform);
            BuildReturnButton(editOverlayRoot.transform);
            BuildSaveButton(editOverlayRoot.transform);
            BuildMiniCubeUI(editOverlayRoot.transform);
            BuildDefaultViewButton(editOverlayRoot.transform);
            BuildUnsavedChangesModal(editOverlayRoot.transform);
            BuildSaveStatusToast(editOverlayRoot.transform);
        }

        // "저장" 버튼 — Return 버튼 바로 아래 (top-right column). 편집 모드 동안만 노출 (editOverlayRoot
        // 자식이라 자동). 클릭 시 현재 배치 markers → SaveDecorationsUseCase 호출.
        private void BuildSaveButton(Transform parent)
        {
            saveButtonRoot = CreateRectObject("SaveButtonRoot", parent);
            var rect = (RectTransform)saveButtonRoot.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            // Return 버튼 바로 아래 — Return 은 (-Margin, -Margin), Save 는 그 아래 ButtonStackGap.
            rect.anchoredPosition = new Vector2(-ButtonMargin, -ButtonMargin - ButtonHeight - ButtonStackGap);

            var button = CreateButton(
                saveButtonRoot.transform, "SaveButton", "저장",
                PrimaryColor, Color.white, new Vector2(ButtonWidth, ButtonHeight),
                false, BorderColor);
            StretchToParent((RectTransform)button.transform);
            button.onClick.AddListener(OnSaveButtonClicked);
        }

        // 미저장 변경사항 안내 모달 — 돌아가기 버튼 클릭 시 isDirty 면 노출. 3개 액션 버튼.
        private void BuildUnsavedChangesModal(Transform parent)
        {
            unsavedChangesModalRoot = CreateRectObject("UnsavedChangesModal", parent);
            var modalRect = (RectTransform)unsavedChangesModalRoot.transform;
            StretchToParent(modalRect);

            // 반투명 배경 — 클릭이 뒤로 통과하지 않도록 raycastTarget=true.
            var dim = CreateImage(unsavedChangesModalRoot.transform, "Dim", new Color(0f, 0f, 0f, 0.45f));
            StretchToParent(dim.rectTransform);

            // 카드.
            var card = CreateImage(unsavedChangesModalRoot.transform, "Card", CardColor);
            card.sprite = roundedRectSprite;
            card.type = Image.Type.Sliced;
            var cardRect = card.rectTransform;
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(820f, 280f);
            cardRect.anchoredPosition = Vector2.zero;
            var cardOutline = card.gameObject.AddComponent<Outline>();
            cardOutline.effectColor = WithAlpha(BorderColor, 0.95f);
            cardOutline.effectDistance = new Vector2(1f, -1f);

            var title = CreateText(card.transform, "Title", "저장하지 않은 변경사항", 24f,
                FontStyles.Bold, TextColor, TextAlignmentOptions.Center);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(32f, -76f);
            titleRect.offsetMax = new Vector2(-32f, -28f);

            var body = CreateText(card.transform, "Body",
                "저장하지 않은 변경사항이 있습니다.\n저장하지 않고 나가시겠습니까?",
                17f, FontStyles.Normal, MutedTextColor, TextAlignmentOptions.Center);
            var bodyRect = body.rectTransform;
            bodyRect.anchorMin = new Vector2(0f, 0.34f);
            bodyRect.anchorMax = new Vector2(1f, 0.74f);
            bodyRect.pivot = new Vector2(0.5f, 0.5f);
            bodyRect.offsetMin = new Vector2(32f, 0f);
            bodyRect.offsetMax = new Vector2(-32f, 0f);

            // 3 버튼 — 가로 배치. 좌: 저장하고 나가기 (primary), 가운데: 저장하지 않고 나가기 (danger),
            // 우: 취소 (secondary). HorizontalLayoutGroup + LayoutElement 로 정렬 — 이전 수동
            // anchoredPosition 배치는 좁은 spacing(12px) + 좁은 가운데 버튼(196px) 으로 텍스트가
            // 답답해 보였고, 해상도/카드 폭 변동에 취약했다. 이제 spacing 18 + LayoutElement
            // preferredWidth 로 균형 정렬.
            var buttonRow = CreateRectObject("ButtonRow", card.transform);
            var buttonRowRect = (RectTransform)buttonRow.transform;
            buttonRowRect.anchorMin = new Vector2(0f, 0f);
            buttonRowRect.anchorMax = new Vector2(1f, 0f);
            buttonRowRect.pivot = new Vector2(0.5f, 0f);
            buttonRowRect.offsetMin = new Vector2(32f, 28f);
            buttonRowRect.offsetMax = new Vector2(-32f, 28f + 56f);
            var rowLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.spacing = 18f;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            var saveExitButton = CreateButton(buttonRow.transform, "SaveAndExitButton", "저장하고 나가기",
                PrimaryColor, Color.white, new Vector2(240f, 56f), false, BorderColor);
            ApplyButtonLabelHorizontalMargin(saveExitButton, 18f);
            var saveExitLayout = saveExitButton.gameObject.AddComponent<LayoutElement>();
            saveExitLayout.preferredWidth = 240f;
            saveExitLayout.preferredHeight = 56f;
            saveExitButton.onClick.AddListener(OnSaveAndExitClicked);

            var discardExitButton = CreateButton(buttonRow.transform, "DiscardAndExitButton", "저장하지 않고 나가기",
                Hex("#C14E4E"), Color.white, new Vector2(290f, 56f), false, BorderColor);
            ApplyButtonLabelHorizontalMargin(discardExitButton, 20f);
            var discardExitLayout = discardExitButton.gameObject.AddComponent<LayoutElement>();
            discardExitLayout.preferredWidth = 290f;
            discardExitLayout.preferredHeight = 56f;
            discardExitButton.onClick.AddListener(OnDiscardAndExitClicked);

            var cancelButton = CreateButton(buttonRow.transform, "CancelButton", "취소",
                CardColor, TextColor, new Vector2(170f, 56f), true, BorderColor);
            ApplyButtonLabelHorizontalMargin(cancelButton, 18f);
            var cancelLayout = cancelButton.gameObject.AddComponent<LayoutElement>();
            cancelLayout.preferredWidth = 170f;
            cancelLayout.preferredHeight = 56f;
            cancelButton.onClick.AddListener(OnCancelExitClicked);

            unsavedChangesModalRoot.SetActive(false);
        }

        // 짧은 안내 토스트 — 저장 성공/실패 시 ~2초 표시 후 자동 사라짐.
        private void BuildSaveStatusToast(Transform parent)
        {
            saveStatusToastRoot = CreateRectObject("SaveStatusToast", parent);
            var rootRect = (RectTransform)saveStatusToastRoot.transform;
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.sizeDelta = new Vector2(360f, 56f);
            rootRect.anchoredPosition = new Vector2(0f, -24f);

            var bg = CreateImage(saveStatusToastRoot.transform, "Background", WithAlpha(TextColor, 0.85f));
            bg.sprite = roundedRectSprite;
            bg.type = Image.Type.Sliced;
            StretchToParent(bg.rectTransform);

            saveStatusToastText = CreateText(saveStatusToastRoot.transform, "Text", "저장되었습니다",
                15f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            StretchToParent(saveStatusToastText.rectTransform);

            saveStatusToastRoot.SetActive(false);
        }

        // 좌측 상단 미니 큐브 UI — 동물의 숲 해피홈 식 "방향 선택 도구". 5개 면(상/전/후/좌/우)을
        // 클릭하면 해당 시점으로 카메라 점프. 바닥은 클릭 대상에서 제외(요구사항). 큐브 형태를
        // 단순 cross/T 레이아웃으로 표현 — 실제 3D 큐브 렌더보다 클릭 영역이 명확하고 모바일 터치
        // 친화적. 면 라벨은 한국어로 표기 (위/전/후/좌/우).
        private void BuildMiniCubeUI(Transform parent)
        {
            var root = CreateRectObject("MiniCubeUI", parent);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(132f, 132f);
            rect.anchoredPosition = new Vector2(16f, -16f);

            // 옅은 카드 배경 — 큐브 박스 느낌을 주기 위한 outline + shadow.
            var bg = CreateImage(root.transform, "Background", WithAlpha(CardColor, 0.92f));
            bg.sprite = roundedRectSprite;
            bg.type = Image.Type.Sliced;
            StretchToParent(bg.rectTransform);
            var bgOutline = bg.gameObject.AddComponent<Outline>();
            bgOutline.effectColor = WithAlpha(BorderColor, 0.9f);
            bgOutline.effectDistance = new Vector2(1f, -1f);

            // cross 레이아웃 — 가운데 행에 좌/전/우, 위에 상, 아래에 후. 각 셀 38×38 + 2 px gap.
            //   .  [위]  .
            //   [좌][전][우]
            //   .  [후]  .
            const float cellSize = 38f;
            const float gap = 2f;
            const float margin = 8f;

            // (col, row) 좌표를 panel 내 anchoredPosition 으로. row=0 이 panel top, col=0 이 panel left.
            void AddFaceButton(string name, string label, int col, int row, CameraPreset preset)
            {
                var btn = CreateButton(root.transform, name, label,
                    SecondaryColor, TextColor, new Vector2(cellSize, cellSize), true, BorderColor);
                var br = (RectTransform)btn.transform;
                br.anchorMin = new Vector2(0f, 1f);
                br.anchorMax = new Vector2(0f, 1f);
                br.pivot = new Vector2(0f, 1f);
                br.sizeDelta = new Vector2(cellSize, cellSize);
                float x = margin + col * (cellSize + gap);
                float y = -(margin + row * (cellSize + gap));
                br.anchoredPosition = new Vector2(x, y);
                btn.onClick.AddListener(() => OnCubeFaceClicked(preset));
            }

            // 레이아웃: "전" 을 윗변(top center) 에, "위" 를 가운데(middle center) 에 배치.
            // 사용자 요구사항: 방 배치 도식에서 "전면 벽" 이 cube 윗면처럼 가장 prominent 위치에
            // 보여야 직관적. "위(탑다운)" 는 cube 정중앙에서 전체를 내려보는 의미로 가운데.
            //   .  [전]  .
            //   [좌][위][우]
            //   .  [후]  .
            AddFaceButton("Front", "전", 1, 0, CameraPreset.Front);
            AddFaceButton("Left",  "좌", 0, 1, CameraPreset.LeftAngle);
            AddFaceButton("Top",   "위", 1, 1, CameraPreset.TopDown);
            AddFaceButton("Right", "우", 2, 1, CameraPreset.RightAngle);
            AddFaceButton("Back",  "후", 1, 2, CameraPreset.Back);
        }

        // "기본 시점" 버튼 — 어느 cycle 시점에서든 1클릭으로 Default 프리셋 복귀.
        // "시점 변경" 버튼 옆 (16 + 140 + 8 = 164 px 오프셋) 에 같은 줄에 배치 — SelectionPanel
        // (중앙 하단, 360 px 폭) 과 겹치지 않게 좌측 모서리에 모음. 편집 모드 동안 항상 노출.
        private void BuildDefaultViewButton(Transform parent)
        {
            var root = CreateRectObject("DefaultViewButton", parent);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0f, BottomMenuFraction);
            rect.anchorMax = new Vector2(0f, BottomMenuFraction);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(140f, 60f);
            // 기존 "시점 변경" cycle 버튼이 (16, 16) 에 있어서 옆으로 빗겨 놨었지만 cycle 버튼을 제거했으므로
            // 이제 (16, 16) 좌측 하단 모서리에 단독 배치. SelectionPanel(중앙) / MiniCube(좌상단)와 충돌 없음.
            rect.anchoredPosition = new Vector2(16f, 16f);

            var button = CreateButton(root.transform, "Button", "기본 시점",
                PrimaryColor, Color.white, new Vector2(140f, 60f), false, BorderColor);
            StretchToParent((RectTransform)button.transform);
            button.onClick.AddListener(OnDefaultViewClicked);
        }

        // Two beige rectangles flanking the camera viewport. Anchored in canvas
        // fractions that match the camera rect formula in ApplyEditModeCameraRect
        // (square viewport centered in the top BottomMenuFraction). raycastTarget
        // is off so clicks pass through to HandleEditModeInput (which treats
        // outside-the-viewport taps as deselect).
        private void BuildPillarboxFills(Transform parent)
        {
            var horizontalMargin = (1f - (1f - BottomMenuFraction)) / 2f;

            var left = CreateImage(parent, "LeftPillarboxFill", BackgroundColor);
            left.raycastTarget = false;
            var leftRect = left.rectTransform;
            leftRect.anchorMin = new Vector2(0f, BottomMenuFraction);
            leftRect.anchorMax = new Vector2(horizontalMargin, 1f);
            leftRect.pivot = new Vector2(0.5f, 0.5f);
            leftRect.sizeDelta = Vector2.zero;
            leftRect.anchoredPosition = Vector2.zero;

            var right = CreateImage(parent, "RightPillarboxFill", BackgroundColor);
            right.raycastTarget = false;
            var rightRect = right.rectTransform;
            rightRect.anchorMin = new Vector2(1f - horizontalMargin, BottomMenuFraction);
            rightRect.anchorMax = new Vector2(1f, 1f);
            rightRect.pivot = new Vector2(0.5f, 0.5f);
            rightRect.sizeDelta = Vector2.zero;
            rightRect.anchoredPosition = Vector2.zero;
        }

        // Single panel: toolbar strip on top + inventory list below, sharing the same
        // background image so they read as one menu (no gap, no separate cards).
        private void BuildBottomMenu(Transform parent)
        {
            bottomMenuRoot = CreateRectObject("BottomMenu", parent);
            var rect = (RectTransform)bottomMenuRoot.transform;
            // Pin to the bottom 1/3 of the canvas via anchors so the menu always
            // takes exactly one third of the screen height regardless of the
            // CanvasScaler match factor or device aspect.
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, BottomMenuFraction);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            var bg = bottomMenuRoot.AddComponent<Image>();
            bg.color = BottomMenuColor;
            bg.sprite = roundedRectSprite;
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;

            var topStroke = CreateImage(bottomMenuRoot.transform, "TopStroke", WithAlpha(BorderColor, 0.85f));
            topStroke.raycastTarget = false;
            var strokeRect = topStroke.rectTransform;
            strokeRect.anchorMin = new Vector2(0f, 1f);
            strokeRect.anchorMax = new Vector2(1f, 1f);
            strokeRect.pivot = new Vector2(0.5f, 1f);
            strokeRect.sizeDelta = new Vector2(0f, 1f);
            strokeRect.anchoredPosition = Vector2.zero;

            BuildToolbarStrip(bottomMenuRoot.transform);
            BuildInventoryList(bottomMenuRoot.transform);
        }

        // Toolbar lives flush against the top of the bottom menu panel — its scroll
        // viewport is transparent so the unified menu background shows through.
        private void BuildToolbarStrip(Transform parent)
        {
            var strip = CreateRectObject("ToolbarStrip", parent);
            var rect = (RectTransform)strip.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, ToolbarStripHeight);
            rect.anchoredPosition = new Vector2(0f, -16f);

            var divider = CreateImage(strip.transform, "BottomDivider", WithAlpha(BorderColor, 0.55f));
            divider.raycastTarget = false;
            var dividerRect = divider.rectTransform;
            dividerRect.anchorMin = new Vector2(0f, 0f);
            dividerRect.anchorMax = new Vector2(1f, 0f);
            dividerRect.pivot = new Vector2(0.5f, 0f);
            dividerRect.sizeDelta = new Vector2(-32f, 1f);
            dividerRect.anchoredPosition = Vector2.zero;

            var scrollRect = strip.AddComponent<ScrollRect>();
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.1f;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;
            scrollRect.scrollSensitivity = 30f;

            var viewport = CreateRectObject("Viewport", strip.transform);
            var viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = new Vector2(0f, 0f);
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            viewportRect.offsetMin = new Vector2(0f, 4f);
            viewportRect.offsetMax = new Vector2(0f, -4f);
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.0001f);
            viewportImage.raycastTarget = true;
            viewport.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportRect;

            var content = CreateRectObject("Content", viewport.transform);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            scrollRect.content = contentRect;

            var layout = content.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 10, 10);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            for (var i = 0; i < ToolbarItems.Length; i++)
            {
                BuildToolbarButton(content.transform, ToolbarItems[i].Key, ToolbarItems[i].Label);
            }

            ApplyToolbarSelectionVisuals();
        }

        private void BuildToolbarButton(Transform parent, string key, string label)
        {
            var background = CreateImage(parent, $"Toolbar_{key}", SecondaryColor);
            background.sprite = roundedRectSprite;
            background.type = Image.Type.Sliced;
            var rect = background.rectTransform;
            // Compact category chip: tighter width buckets, lower height, smaller
            // font so more of the row fits without horizontal scrolling.
            var widthForLabel = label.Length >= 9 ? 168f
                                : label.Length >= 6 ? 132f
                                : 96f;
            rect.sizeDelta = new Vector2(widthForLabel, 56f);

            var layoutElement = background.gameObject.AddComponent<LayoutElement>();
            layoutElement.minWidth = rect.sizeDelta.x;
            layoutElement.preferredWidth = rect.sizeDelta.x;
            layoutElement.minHeight = 56f;
            layoutElement.preferredHeight = 56f;

            var outline = background.gameObject.AddComponent<Outline>();
            outline.effectColor = WithAlpha(BorderColor, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);

            var labelText = CreateText(background.transform, "Label", label, 16f, FontStyles.Bold, TextColor, TextAlignmentOptions.Center);
            labelText.overflowMode = TextOverflowModes.Ellipsis;
            StretchToParent(labelText.rectTransform);

            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => OnToolbarButtonClicked(key));

            toolbarBackgrounds[key] = background;
            toolbarLabels[key] = labelText;
        }

        // InventoryListContainer 내부 title 영역 위에 가로 strip으로 sub-tab 3개.
        // FURNITURE 선택 시만 활성화 (UpdateFurnitureSubToolbarVisibility). 단일 row,
        // title과 anchor 영역 거의 동일 (top, height 28) — title은 그 시점에 SetActive(false).
        private void BuildFurnitureSubToolbar(Transform parent)
        {
            furnitureSubToolbarRoot = CreateRectObject("FurnitureSubToolbar", parent);
            var rect = (RectTransform)furnitureSubToolbarRoot.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-48f, 36f);
            rect.anchoredPosition = new Vector2(0f, -8f);

            var layout = furnitureSubToolbarRoot.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            for (var i = 0; i < FurnitureSubItems.Length; i++)
            {
                BuildFurnitureSubButton(furnitureSubToolbarRoot.transform, FurnitureSubItems[i].Key, FurnitureSubItems[i].Label);
            }

            ApplyFurnitureSubSelectionVisuals();
            furnitureSubToolbarRoot.SetActive(false);
        }

        // 책장 스킨은 BE PATCH 경로 미지원 (ISpaceSettingsRepository.ChangeBookshelfSkin 없음 +
        // SpaceResponseDto.CurrentBookshelfSkinInventoryId 없음 + GetCategoryIdFromSpace/
        // OptimisticallyUpdateServerSpace 미지원). 클릭 가능하면 visual만 바뀌고 PATCH는 안 나가
        // 다음 reload 에서 선택이 사라짐 — '저장 안 되는 UI'가 됨.
        // 후속 PR에서 BE 경로 + DTO 필드 + repository 연결까지 묶어서 활성화 예정.
        private static bool IsFurnitureSubDisabled(string key) => key == "BOOKSHELF_SKIN";

        private void BuildFurnitureSubButton(Transform parent, string key, string label)
        {
            bool disabled = IsFurnitureSubDisabled(key);

            var background = CreateImage(parent, $"FurnitureSub_{key}", disabled ? CardColor : SecondaryColor);
            background.sprite = roundedRectSprite;
            background.type = Image.Type.Sliced;
            var rect = background.rectTransform;
            rect.sizeDelta = new Vector2(disabled ? 132f : 96f, 32f);

            var layoutElement = background.gameObject.AddComponent<LayoutElement>();
            layoutElement.minWidth = rect.sizeDelta.x;
            layoutElement.preferredWidth = rect.sizeDelta.x;
            layoutElement.minHeight = rect.sizeDelta.y;
            layoutElement.preferredHeight = rect.sizeDelta.y;

            var outline = background.gameObject.AddComponent<Outline>();
            outline.effectColor = WithAlpha(BorderColor, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);

            string displayLabel = disabled ? $"{label} 준비 중" : label;
            var labelText = CreateText(background.transform, "Label", displayLabel, 14f, FontStyles.Bold,
                disabled ? MutedTextColor : TextColor, TextAlignmentOptions.Center);
            labelText.overflowMode = TextOverflowModes.Ellipsis;
            StretchToParent(labelText.rectTransform);

            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            button.interactable = !disabled;
            if (!disabled)
            {
                button.onClick.AddListener(() => OnFurnitureSubClicked(key));
            }

            furnitureSubBackgrounds[key] = background;
            furnitureSubLabels[key] = labelText;
        }

        private void OnFurnitureSubClicked(string key)
        {
            if (selectedFurnitureSubKey == key) return;
            selectedFurnitureSubKey = key;
            ApplyFurnitureSubSelectionVisuals();
            RebuildInventorySlots();
        }

        private void ApplyFurnitureSubSelectionVisuals()
        {
            foreach (var pair in furnitureSubBackgrounds)
            {
                furnitureSubLabels.TryGetValue(pair.Key, out var label);
                if (IsFurnitureSubDisabled(pair.Key))
                {
                    // disabled 키는 항상 disabled 색상 유지 — 사용자 클릭 불가지만 방어적.
                    pair.Value.color = CardColor;
                    if (label != null) label.color = MutedTextColor;
                    continue;
                }
                var isSelected = pair.Key == selectedFurnitureSubKey;
                pair.Value.color = isSelected ? PrimaryColor : SecondaryColor;
                if (label != null) label.color = isSelected ? Color.white : TextColor;
            }
        }

        private void UpdateFurnitureSubToolbarVisibility()
        {
            bool showSub = selectedToolbarKey == "FURNITURE";
            if (furnitureSubToolbarRoot != null && furnitureSubToolbarRoot.activeSelf != showSub)
            {
                furnitureSubToolbarRoot.SetActive(showSub);
            }
            if (inventoryTitleObject != null && inventoryTitleObject.activeSelf == showSub)
            {
                inventoryTitleObject.SetActive(!showSub);
            }
        }

        // toolbar sub-key → CustomizationCategory 매핑.
        private static bool TryResolveFurnitureSubCategory(string subKey, out CustomizationCategory category)
        {
            switch (subKey)
            {
                case "DESK":            category = CustomizationCategory.Desk;          return true;
                case "BOOKSHELF_SKIN":  category = CustomizationCategory.BookshelfSkin; return true;
                case "DASHBOARD_SKIN":  category = CustomizationCategory.DashboardSkin; return true;
                default:                category = default;                              return false;
            }
        }

        // Inventory list occupies the rest of the bottom menu panel beneath the
        // toolbar strip. Background is intentionally transparent so the panel reads
        // as one continuous beige surface.
        private void BuildInventoryList(Transform parent)
        {
            var listContainer = CreateRectObject("InventoryListContainer", parent);
            var rect = (RectTransform)listContainer.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(0f, 0f);
            rect.offsetMax = new Vector2(0f, -(ToolbarStripHeight + 16f));

            var title = CreateText(listContainer.transform, "Title", "보유 인벤토리", 18f, FontStyles.Bold, MutedTextColor, TextAlignmentOptions.Left);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.sizeDelta = new Vector2(-48f, 28f);
            titleRect.anchoredPosition = new Vector2(28f, -12f);
            inventoryTitleObject = title.gameObject;

            // FURNITURE 탭 선택 시 title 자리에 sub-toolbar 노출 — 책상/책장/대시보드.
            BuildFurnitureSubToolbar(listContainer.transform);

            var scrollRect = listContainer.AddComponent<ScrollRect>();
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.1f;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;
            scrollRect.scrollSensitivity = 30f;

            var viewport = CreateRectObject("Viewport", listContainer.transform);
            var viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = new Vector2(0f, 0f);
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            viewportRect.offsetMin = new Vector2(20f, 16f);
            viewportRect.offsetMax = new Vector2(-20f, -52f);
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.0001f);
            viewportImage.raycastTarget = true;
            viewport.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportRect;

            inventoryContentRoot = CreateRectObject("Content", viewport.transform);
            var contentRect = (RectTransform)inventoryContentRoot.transform;
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            scrollRect.content = contentRect;

            var layout = inventoryContentRoot.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = inventoryContentRoot.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            RebuildInventorySlots();
        }

        // 현재 적용된 상단 메뉴 테마 필터를 인벤토리 영역 상단에 표시. 매 RebuildInventorySlots 마다 갱신.
        private TMP_Text themeFilterLabel;

        private void ApplyThemeFilterLabel()
        {
            if (inventoryContentRoot == null) return;
            var bottomRoot = bottomMenuRoot != null ? bottomMenuRoot.transform : inventoryContentRoot.transform.parent;
            if (bottomRoot == null) return;

            if (themeFilterLabel == null)
            {
                var go = new GameObject("ThemeFilterLabel", typeof(RectTransform));
                go.transform.SetParent(bottomRoot, false);
                themeFilterLabel = go.AddComponent<TextMeshProUGUI>();
                themeFilterLabel.font = fontAsset;
                themeFilterLabel.fontSize = 18f;
                themeFilterLabel.color = TextColor;
                themeFilterLabel.alignment = TextAlignmentOptions.MidlineLeft;
                var rt = themeFilterLabel.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(24f, -8f);
                rt.sizeDelta = new Vector2(360f, 24f);
            }
            themeFilterLabel.text = $"현재 테마 필터: {RoomEditThemeFilter.DescribeActive()}";
        }

        // Clears the inventory content row and rebuilds it from the entries that
        // match the currently selected toolbar category. Called on init and on every
        // category click so the inventory always reflects the active tab.
        private void RebuildInventorySlots()
        {
            if (inventoryContentRoot == null)
            {
                return;
            }

            ApplyThemeFilterLabel();

            for (var i = inventoryContentRoot.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(inventoryContentRoot.transform.GetChild(i).gameObject);
            }

            if (selectedToolbarKey == "THEME_FULL")
            {
                BuildThemeSlots(inventoryContentRoot.transform);
                return;
            }

            // v1.0 customization — 1:1 카테고리 매핑이 명확한 toolbar 키는 CustomizationCatalog로 처리.
            // FURNITURE는 sub-tab(책상/책장스킨/대시보드스킨)으로 분기. OBJECT_SKIN은 후속 PR에서 sub-탭화.
            if (selectedToolbarKey == "FURNITURE")
            {
                if (TryResolveFurnitureSubCategory(selectedFurnitureSubKey, out var furnitureCategory))
                {
                    BuildCustomizationSlots(inventoryContentRoot.transform, furnitureCategory);
                    return;
                }
            }

            if (TryResolveCustomizationCategory(selectedToolbarKey, out var category))
            {
                BuildCustomizationSlots(inventoryContentRoot.transform, category);
                return;
            }

            var slotIndex = 0;
            for (var i = 0; i < inventoryEntries.Count; i++)
            {
                var entry = inventoryEntries[i];
                if (!MatchesCategory(entry, selectedToolbarKey))
                {
                    continue;
                }
                BuildInventorySlot(inventoryContentRoot.transform, slotIndex, entry);
                slotIndex++;
            }
        }

        // toolbar 키 → CustomizationCategory 매핑. 1:1 명확 매핑만 true 반환,
        // 묶음 탭(FURNITURE/OBJECT_SKIN/ALL)은 false.
        private static bool TryResolveCustomizationCategory(string toolbarKey, out CustomizationCategory category)
        {
            switch (toolbarKey)
            {
                case "WALLPAPER":   category = CustomizationCategory.Wallpaper; return true;
                case "LIGHT_INNER": category = CustomizationCategory.InteriorLight; return true;
                case "LIGHT_OUTER": category = CustomizationCategory.ExteriorLight; return true;
                default:            category = default; return false;
            }
        }

        private readonly System.Collections.Generic.Dictionary<CustomizationCategory, CustomizationCatalogAsset> categoryCatalogCache
            = new System.Collections.Generic.Dictionary<CustomizationCategory, CustomizationCatalogAsset>();

        private void BuildCustomizationSlots(Transform parent, CustomizationCategory category)
        {
            var catalog = LoadCategoryCatalog(category);
            if (catalog == null || catalog.Entries == null || catalog.Entries.Count == 0)
            {
                Debug.LogWarning($"[RoomEditModeController] {category} 카탈로그 비어있음 — Resources/Catalogs/{category}Catalog.asset에 entries 추가 또는 'Dotshelf > Design Preview > Generate Placeholder Catalogs' 메뉴 실행.");
                return;
            }

            int rendered = 0;
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                var entry = catalog.Entries[i];
                if (entry == null) continue;
                // 상단 메뉴 테마 필터 적용. 빈 ThemeTag (legacy/공통) 는 항상 통과.
                if (!RoomEditThemeFilter.MatchesActive(entry.ThemeTag)) continue;
                BuildCustomizationSlot(parent, i, category, entry);
                rendered++;
            }
            Debug.Log($"[RoomEditModeController] Theme={RoomEditThemeFilter.DescribeActive()}, Category={category}, ResultCount={rendered}");
        }

        private CustomizationCatalogAsset LoadCategoryCatalog(CustomizationCategory category)
        {
            if (categoryCatalogCache.TryGetValue(category, out var cached) && cached != null)
            {
                return cached;
            }
            var loaded = Resources.Load<CustomizationCatalogAsset>($"Catalogs/{category}Catalog");
            if (loaded != null)
            {
                categoryCatalogCache[category] = loaded;
            }
            return loaded;
        }

        // CustomizationEntry 슬롯 — 테마 슬롯과 같은 모양 (108x128 카드 + 썸네일 + 라벨).
        // 클릭 시 OnCustomizationEntrySelected 호출 → visual swap + PATCH wiring.
        private void BuildCustomizationSlot(Transform parent, int slotIndex, CustomizationCategory category, CustomizationEntry entry)
        {
            var slot = CreateImage(parent, $"{category}Slot_{slotIndex}", CardColor);
            slot.sprite = roundedRectSprite;
            slot.type = Image.Type.Sliced;
            var rect = slot.rectTransform;
            rect.sizeDelta = new Vector2(108f, 128f);

            var layout = slot.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 108f; layout.preferredWidth = 108f;
            layout.minHeight = 128f; layout.preferredHeight = 128f;

            var outline = slot.gameObject.AddComponent<Outline>();
            outline.effectColor = WithAlpha(BorderColor, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);

            // 현재 적용 중인 항목은 강조 (Space 도메인의 카테고리별 ID와 비교)
            if (IsCategoryEntryCurrent(category, entry.AssetId))
            {
                outline.effectColor = TextColor;
                outline.effectDistance = new Vector2(2f, -2f);
            }

            var thumb = CreateImage(slot.transform, "Thumb", Hex("#E8DFCC"));
            if (entry.PreviewSprite != null)
            {
                thumb.sprite = entry.PreviewSprite;
                thumb.type = Image.Type.Simple;
                thumb.preserveAspect = true;
            }
            else if (entry.PrimaryMaterial != null)
            {
                // PreviewSprite 없으면 머티리얼 색을 썸네일로 사용 (placeholder 단계)
                thumb.sprite = roundedRectSprite;
                thumb.type = Image.Type.Sliced;
                thumb.color = entry.PrimaryMaterial.HasProperty("_BaseColor") ? entry.PrimaryMaterial.GetColor("_BaseColor") : entry.PrimaryMaterial.color;
            }
            else
            {
                thumb.sprite = roundedRectSprite;
                thumb.type = Image.Type.Sliced;
            }
            var thumbRect = thumb.rectTransform;
            thumbRect.anchorMin = new Vector2(0.5f, 1f);
            thumbRect.anchorMax = new Vector2(0.5f, 1f);
            thumbRect.pivot = new Vector2(0.5f, 1f);
            thumbRect.sizeDelta = new Vector2(80f, 80f);
            thumbRect.anchoredPosition = new Vector2(0f, -8f);

            var labelText = CreateText(slot.transform, "Label",
                string.IsNullOrEmpty(entry.DisplayName) ? category.ToString() : entry.DisplayName,
                12f, FontStyles.Bold, TextColor, TextAlignmentOptions.Center);
            labelText.overflowMode = TextOverflowModes.Ellipsis;
            var labelRect = labelText.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.sizeDelta = new Vector2(-8f, 24f);
            labelRect.anchoredPosition = new Vector2(0f, 6f);

            var button = slot.gameObject.AddComponent<Button>();
            button.targetGraphic = slot;
            var capturedEntry = entry;
            var capturedCategory = category;
            button.onClick.AddListener(() => OnCustomizationEntrySelected(capturedCategory, capturedEntry));
        }

        // 현재 적용 중인 카테고리 항목인지 검사.
        // 서버 Space 6 카테고리 ID 캐시. ReloadRoomState 응답 + 카테고리별 PATCH optimistic 갱신으로
        // 동기화. IsCategoryEntryCurrent의 single source of truth — \"현재 사용 중\" highlight가
        // 서버 상태와 일치 (다른 디바이스 변경, 새 로그인 등 시나리오에서도 정확).
        private SpaceResponseDto lastServerSpace;

        // 서버 응답 받기 전 (초기 진입 / GET 실패) fallback. lastServerSpace 있으면 그것 우선.
        private readonly System.Collections.Generic.Dictionary<CustomizationCategory, long> lastAppliedEntries
            = new System.Collections.Generic.Dictionary<CustomizationCategory, long>();

        private bool IsCategoryEntryCurrent(CustomizationCategory category, long assetId)
        {
            if (assetId <= 0) return false;
            // 1) 서버 상태 우선: lastServerSpace의 카테고리 ID와 직접 비교.
            //    serverId가 null이면 카테고리 미적용(기본 외형) — catalog의 첫 entry가 \"current\"로 표시.
            if (lastServerSpace != null)
            {
                var serverId = GetCategoryIdFromSpace(category, lastServerSpace);
                if (serverId.HasValue && serverId.Value > 0)
                {
                    return serverId.Value == assetId;
                }
                // 서버상 미적용 → catalog default(첫 entry)가 \"current\"
                var catalog = LoadCategoryCatalog(category);
                if (catalog?.Entries != null && catalog.Entries.Count > 0)
                {
                    var defaultEntry = catalog.Entries[0];
                    return defaultEntry != null && defaultEntry.AssetId == assetId;
                }
                return false;
            }
            // 2) 서버 응답 받기 전 fallback — 클라이언트 클릭 캐시
            return lastAppliedEntries.TryGetValue(category, out var current) && current == assetId;
        }

        private static long? GetCategoryIdFromSpace(CustomizationCategory category, SpaceResponseDto space)
        {
            switch (category)
            {
                case CustomizationCategory.Wallpaper:     return space.CurrentWallpaperInventoryId;
                case CustomizationCategory.Floor:         return space.CurrentFloorInventoryId;
                case CustomizationCategory.Desk:          return space.CurrentDeskInventoryId;
                case CustomizationCategory.DashboardSkin: return space.CurrentDashboardInventoryId;
                case CustomizationCategory.InteriorLight: return space.CurrentInteriorLightInventoryId;
                case CustomizationCategory.ExteriorLight: return space.CurrentExteriorLightInventoryId;
                default: return null;
            }
        }

        // PATCH onSuccess (themeReset=false 정상 변경) 후 lastServerSpace 한 카테고리만 optimistic 갱신.
        // 매 PATCH마다 GET 추가 호출 회피. themeReset=true일 때만 ReloadRoomState로 풀 동기화.
        private void OptimisticallyUpdateServerSpace(CustomizationCategory category, long? inventoryId)
        {
            if (lastServerSpace == null) return;
            switch (category)
            {
                case CustomizationCategory.Wallpaper:     lastServerSpace.CurrentWallpaperInventoryId = inventoryId; break;
                case CustomizationCategory.Floor:         lastServerSpace.CurrentFloorInventoryId = inventoryId; break;
                case CustomizationCategory.Desk:          lastServerSpace.CurrentDeskInventoryId = inventoryId; break;
                case CustomizationCategory.DashboardSkin: lastServerSpace.CurrentDashboardInventoryId = inventoryId; break;
                case CustomizationCategory.InteriorLight: lastServerSpace.CurrentInteriorLightInventoryId = inventoryId; break;
                case CustomizationCategory.ExteriorLight: lastServerSpace.CurrentExteriorLightInventoryId = inventoryId; break;
            }
        }

        // 전체 테마 카테고리 — 데코 인벤토리 대신 RoomThemeCatalogAsset의 테마들을
        // 같은 슬롯 그리드에 채운다. 셀 클릭 시 RoomThemeApplier로 visual swap만 수행
        // (백엔드 PATCH /spaces/me/theme wiring은 후속 마디 — A3-4).
        private void BuildThemeSlots(Transform parent)
        {
            if (themeCatalog == null)
            {
                themeCatalog = Resources.Load<RoomThemeCatalogAsset>("RoomThemeCatalog");
            }
            if (themeCatalog == null || themeCatalog.Entries == null || themeCatalog.Entries.Count == 0)
            {
                Debug.LogWarning("[RoomEditModeController] RoomThemeCatalog 비어있음 — Resources/RoomThemeCatalog.asset에 카탈로그 + 테마 항목을 추가하세요.");
                return;
            }

            for (var i = 0; i < themeCatalog.Entries.Count; i++)
            {
                var theme = themeCatalog.Entries[i];
                if (theme == null) continue;
                BuildThemeSlot(parent, i, theme);
            }
        }

        private void BuildThemeSlot(Transform parent, int slotIndex, RoomThemeAsset theme)
        {
            var slot = CreateImage(parent, $"ThemeSlot_{slotIndex}", CardColor);
            slot.sprite = roundedRectSprite;
            slot.type = Image.Type.Sliced;
            var rect = slot.rectTransform;
            rect.sizeDelta = new Vector2(108f, 128f);

            var layout = slot.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 108f;
            layout.preferredWidth = 108f;
            layout.minHeight = 128f;
            layout.preferredHeight = 128f;

            var outline = slot.gameObject.AddComponent<Outline>();
            outline.effectColor = WithAlpha(BorderColor, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);

            // 현재 적용된 테마는 강조
            bool isCurrent = themeApplier != null && themeApplier.CurrentTheme == theme;
            if (isCurrent)
            {
                outline.effectColor = TextColor;
                outline.effectDistance = new Vector2(2f, -2f);
            }

            // 썸네일 — PreviewSprite가 있으면 그대로, 없으면 placeholder
            var thumb = CreateImage(slot.transform, "Thumb", Hex("#E8DFCC"));
            if (theme.PreviewSprite != null)
            {
                thumb.sprite = theme.PreviewSprite;
                thumb.type = Image.Type.Simple;
                thumb.preserveAspect = true;
            }
            else
            {
                thumb.sprite = roundedRectSprite;
                thumb.type = Image.Type.Sliced;
            }
            var thumbRect = thumb.rectTransform;
            thumbRect.anchorMin = new Vector2(0.5f, 1f);
            thumbRect.anchorMax = new Vector2(0.5f, 1f);
            thumbRect.pivot = new Vector2(0.5f, 1f);
            thumbRect.sizeDelta = new Vector2(80f, 80f);
            thumbRect.anchoredPosition = new Vector2(0f, -8f);

            if (theme.PreviewSprite == null)
            {
                var glyph = CreateText(thumb.transform, "Glyph",
                    ResolveSlotInitial(theme.DisplayName), 28f, FontStyles.Bold, MutedTextColor, TextAlignmentOptions.Center);
                StretchToParent(glyph.rectTransform);
            }

            var labelText = CreateText(slot.transform, "Label", string.IsNullOrEmpty(theme.DisplayName) ? "테마" : theme.DisplayName,
                12f, FontStyles.Bold, TextColor, TextAlignmentOptions.Center);
            labelText.overflowMode = TextOverflowModes.Ellipsis;
            var labelRect = labelText.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.sizeDelta = new Vector2(-8f, 24f);
            labelRect.anchoredPosition = new Vector2(0f, 6f);

            var button = slot.gameObject.AddComponent<Button>();
            button.targetGraphic = slot;
            var capturedTheme = theme;
            button.onClick.AddListener(() => OnThemeSelected(capturedTheme));
        }

        private void OnThemeSelected(RoomThemeAsset theme)
        {
            if (theme == null) return;
            // 진단 로그 — "전체 테마" 클릭이 편집 모드/현재 카메라 프리셋과 어떻게 상호작용했는지
            // 이번 wall fade 회귀 버그의 핵심 단서. 적용 흐름은 ShowThemeApplyModeModal → ApplyThemeSelected
            // 거기서 RefreshWallVisibilityAfterMaterialMutation 가 호출됨.
            Debug.Log(
                $"[RoomEdit][Theme][click] '{theme.DisplayName}' editMode={IsEditModeActive} " +
                $"preset={(IsEditModeActive ? currentCameraPreset.ToString() : "n/a")} " +
                $"wallVisibilityCollected={wallVisibilityCollected}");
            ShowThemeApplyModeModal(theme);
        }

        private void ApplyThemeSelected(RoomThemeAsset theme, ThemeApplyMode mode)
        {
            if (theme == null) return;
            if (themeApplier == null)
            {
                themeApplier = FindFirstObjectByType<RoomThemeApplier>();
            }
            if (themeApplier == null)
            {
                Debug.LogWarning("[RoomEditModeController] RoomThemeApplier 인스턴스 없음 — MainRoomScene에 추가하세요.");
                return;
            }

            // D9(i) 낙관적 적용: 즉시 visual swap → 백엔드 PATCH → 실패 시 rollback.
            previousAppliedTheme = themeApplier.CurrentTheme;
            Debug.Log($"[RoomEdit][Theme] ApplyThemeSelected '{theme.DisplayName}' editMode={IsEditModeActive} preset={(IsEditModeActive ? currentCameraPreset.ToString() : "n/a")}");

            // [MR 135 review beshu Finding 3 / skh1205 #2] PATCH 실패 시 사용자 placement 가
            // 이미 destroy 된 상태로 남는 데이터 손실 회피 — 기존 placement 를 destroy 하지 않고
            // hidden hold root 로 detach. PATCH 성공 시 destroy, 실패 시 reattach.
            // local preview (PATCH skip) 에는 무관 — DiscardPlacementRollbackHold 로 즉시 destroy.
            DetachPlacementForRollback();

            themeApplier.Apply(theme);
            // 벽 sharedMaterial 이 swap 됐으므로 편집 모드 중이면 fade 즉시 재적용 (탑다운/사선에서 occlusion 복귀 방지).
            RefreshWallVisibilityAfterMaterialMutation("ApplyThemeSelected");
            RebuildInventorySlots();

            // 테마의 default entries 자동 적용 — visual 즉시 반영. PATCH 결과 무관 (401/네트워크
            // 실패해도 산뜻 자산 mesh swap 적용). 사용자가 BE 인증 안 된 dev 환경에서도 검증 가능.
            // PATCH 성공 후엔 BE 응답 themeReset 의 ReloadRoomState 가 server 권위 상태로 재동기화.
            ApplyThemeDefaultEntries(theme);

            // local preview 전용 테마 — placeholder (가구/벽지/바닥 미제작). 명시 플래그 + assetId<=0 fallback.
            // RoomThemeApplier 는 라이팅만 영향. POST /spaces/me/theme/{id}/apply 는 skip (401/403 회피).
            if (IsLocalPreviewTheme(theme))
            {
                Debug.Log($"[RoomEditModeController] Skip PATCH for local preview theme: {theme.DisplayName} (assetId={theme.AssetId}, mode={mode})");
                DiscardPlacementRollbackHold();
                return;
            }

            EnsureThemeUseCase();
            if (themeUseCase == null)
            {
                Debug.LogWarning("[RoomEditModeController] ISpaceSettingsRepository 미주입 — 백엔드 theme apply skip (visual만 적용).");
                DiscardPlacementRollbackHold();
                return;
            }

            if (theme.AssetId <= 0)
            {
                Debug.LogWarning("[RoomEditModeController] theme AssetId가 없어 백엔드 theme apply skip (visual만 적용).");
                DiscardPlacementRollbackHold();
                return;
            }

            long inventoryId = theme.AssetId;
            // 연타 시 stale callback 무시: 캡쳐된 version과 현재 version이 다르면 이번 요청은
            // 이미 다음 요청에 의해 덮였으므로 응답 무시. (rollback도, success log도 안 함)
            int requestVersion = ++themeRequestVersion;
            themeUseCase.Execute(
                inventoryId,
                mode,
                onSuccess: themeReset =>
                {
                    if (requestVersion != themeRequestVersion)
                    {
                        return;
                    }
                    Debug.Log($"[RoomEditModeController] POST /spaces/me/theme/{inventoryId}/apply OK → mode={mode}, themeReset={themeReset}.");
                    if (themeReset || mode == ThemeApplyMode.Reset)
                    {
                        ApplyThemeResetVisualFallback();
                        // reset 후 default entries 재적용 (fallback 이 lastAppliedEntries clear 함).
                        // DetachPlacementForRollback 가 다시 hold root 로 detach 하므로 그 직후 discard.
                        ApplyThemeDefaultEntries(theme);
                    }
                    // PATCH 성공 — rollback hold root 의 이전 placement 는 더 이상 필요 없으므로 destroy.
                    DiscardPlacementRollbackHold();
                },
                onError: error =>
                {
                    // [Reentry race 가드 — MR 135 v2 review #2] 같은 frame 연속 클릭 (산뜻 → 모던 등)
                    // 시 themeRequestVersion 이 증분되어 stale 콜백은 여기서 즉시 return.
                    // 새 테마 click 의 DetachPlacementForRollback 이 hold root 를 이미 교체했어도
                    // stale onError 가 ReattachPlacementFromRollback 으로 잘못 복구하지 않도록 보장.
                    if (requestVersion != themeRequestVersion)
                    {
                        return;
                    }
                    bool isNetworkFailure = string.IsNullOrEmpty(error?.Code);
                    if (isNetworkFailure)
                    {
                        // PATCH connection 실패 (dev 환경 BE 미기동 / 네트워크 단절). 클라이언트는
                        // 새 테마 visual + default placement 를 그대로 유지 — 사용자 정책 "테마
                        // 적용 시 모든 placement 초기화" 일관 적용. hold root 의 이전 사용자
                        // placement 는 destroy (새 테마 default 가 placedItemsRoot 에 이미 spawn
                        // 되어 있어 reattach 시 이중 노출 + 정책 위반).
                        // 다음 ReloadRoomState 또는 다음 PATCH 성공 시 server 권위 상태와 sync.
                        Debug.LogWarning($"[RoomEditModeController] PATCH connection 실패 ({error?.Message}) — 새 테마 visual + default placement 유지, hold root 의 이전 placement 는 정책상 discard. 다음 로드 시 서버와 동기화.");
                        DiscardPlacementRollbackHold();
                        return;
                    }

                    Debug.LogWarning($"[RoomEditModeController] theme apply 실패 ({error.Code}/{error.Message}) — 이전 테마로 롤백.");
                    if (previousAppliedTheme != null && previousAppliedTheme != theme)
                    {
                        themeApplier.Apply(previousAppliedTheme);
                        // mesh swap + 새로 spawn 한 theme deco 정리, 그 후 hold root 의 이전 placement 복구.
                        RestoreAllMeshSwaps();
                        ClearAllPlacedDecos();
                        ReattachPlacementFromRollback();
                        RefreshWallVisibilityAfterMaterialMutation("ApplyThemeSelected/rollback");
                        RebuildInventorySlots();
                    }
                    else
                    {
                        // 동일 테마 재클릭 등 rollback 대상이 없는 경우 — hold root 만 정리.
                        DiscardPlacementRollbackHold();
                    }
                });
        }

        private void EnsureThemeUseCase()
        {
            if (themeUseCase != null) return;
            var apiManager = APIManager.Instance;
            if (apiManager == null)
            {
                Debug.LogWarning("[RoomEditModeController] APIManager.Instance == null — UpdateSpaceThemeUseCase 생성 skip.");
                return;
            }
            try
            {
                var apiClient = new NotebookApiClient(apiManager);
                var repo = new ApiSpaceSettingsRepository(apiClient);
                themeUseCase = new ApplyThemeWithModeUseCase(repo);
            }
            catch (Exception ex)
            {
                // 어느 단계(NotebookApiClient ctor / Repository ctor / UseCase ctor)에서 깨졌는지
                // stack까지 같이 출력해야 디버깅 가능.
                Debug.LogError($"[RoomEditModeController] ApplyThemeWithModeUseCase 생성 실패 ({ex.GetType().Name}).");
                Debug.LogException(ex);
            }
        }

        // 카테고리 카탈로그 셀 클릭 시 호출. visual swap + 백엔드 PATCH wiring (낙관적 적용).
        // themeReset 응답 시 룸 전체 reload 필요 (TODO: Space 재로드 hook 추가 — 후속 마디).
        // 빠른 연타 시 stale callback 무시 — categoryRequestVersion 카운터.
        private int categoryRequestVersion;

        private void OnCustomizationEntrySelected(CustomizationCategory category, CustomizationEntry entry)
        {
            if (entry == null) return;

            // 현재 적용 테마가 customization-disabled면 사전 확인 모달.
            // 사용자가 "계속" 클릭 시 ProceedCustomizationEntry로 정상 흐름 재진입.
            // 이 경로에서는 PATCH 응답 themeReset=true를 거쳐 자연스럽게 테마가 해제되고,
            // 다음 클릭부터는 CurrentTheme.AllowsCustomization=true가 되어 모달 미발동.
            if (IsCurrentThemeCustomizationLocked())
            {
                ShowCustomizationLockedModal(category, entry);
                return;
            }

            ProceedCustomizationEntry(category, entry);
        }

        private void ProceedCustomizationEntry(CustomizationCategory category, CustomizationEntry entry)
        {
            // visual swap — 카테고리별로 적용 + 강조 상태 갱신
            ApplyEntryVisual(category, entry);
            if (entry.AssetId > 0) lastAppliedEntries[category] = entry.AssetId;
            RebuildInventorySlots();

            // local preview 전용 entry — 사용자 inventory 에 없는 placeholder. PATCH 시 401(인증)/403(S006) 으로 거부되므로
            // 백엔드 호출 자체를 skip 하고 RoomLighting 하위에 Preview Light 만 갱신.
            // 명시적 IsLocalPreview 플래그 + assetId 범위 fallback 으로 robust 판정 (catalog 재생성 안 해도 동작).
            if (IsLocalPreviewEntry(entry))
            {
                if (category == CustomizationCategory.InteriorLight || category == CustomizationCategory.ExteriorLight)
                {
                    ApplyLocalLightingPreview(category, entry);
                    Debug.Log($"[RoomEditModeController] Local preview lighting applied: {category}/{entry.DisplayName} (assetId={entry.AssetId})");
                }
                Debug.Log($"[RoomEditModeController] Skip PATCH for local preview item: {category}/{entry.DisplayName} (assetId={entry.AssetId})");
                return;
            }

            // 백엔드 PATCH wiring
            EnsureSpaceSettingsRepository();
            if (spaceSettingsRepo == null)
            {
                Debug.LogWarning("[RoomEditModeController] ISpaceSettingsRepository 미준비 — 백엔드 PATCH skip (visual만 적용).");
                return;
            }

            int requestVersion = ++categoryRequestVersion;
            long? invId = entry.AssetId > 0 ? (long?)entry.AssetId : null;

            Action<bool> onSuccess = themeReset =>
            {
                if (requestVersion != categoryRequestVersion) return;
                Debug.Log($"[RoomEditModeController] PATCH /spaces/me/{category.ToString().ToLower()} OK → invId={invId}, themeReset={themeReset}.");
                if (themeReset)
                {
                    // 커스터마이징 불가 테마 자동 해제 — 두 단계 처리:
                    // 1) 즉시 클라이언트 fallback: 책장 skin reset + lastAppliedEntries 비움.
                    //    사용자에게 latency 0으로 visual 변화 노출 (네트워크 응답 대기 없이).
                    // 2) 서버 권위 동기화: lastServerSpace 무효화 후 ReloadRoomState 비동기 호출.
                    //    GET /spaces/me로 6 카테고리 ID 권위 상태 받아 visual 재적용.
                    //
                    // race window: ReloadRoomState 응답 도착 전까지 lastServerSpace는 reset 전
                    // 옛 값. 즉시 null invalidate해서 응답 올 때까지 IsCategoryEntryCurrent가
                    // lastAppliedEntries fallback (방금 Clear됨)으로 동작 — stale highlight 회피.
                    Debug.Log("[RoomEditModeController] 커스터마이징 불가 테마 자동 해제 → 클라이언트 fallback + 서버 reload.");
                    ApplyThemeResetVisualFallback();
                    lastServerSpace = null;
                    ReloadRoomState();
                }
                else
                {
                    // 정상 카테고리 변경 — 서버 Space 캐시를 invId로 optimistic 갱신.
                    // GET /spaces/me 추가 호출 회피 (round-trip 절감).
                    OptimisticallyUpdateServerSpace(category, invId);
                    RebuildInventorySlots();
                }
            };
            Action<DashboardApiError> onError = error =>
            {
                if (requestVersion != categoryRequestVersion) return;
                bool isNetworkFailure = string.IsNullOrEmpty(error?.Code);
                if (isNetworkFailure)
                {
                    Debug.LogWarning($"[RoomEditModeController] PATCH connection 실패 ({error?.Message}) — visual은 유지.");
                    return;
                }
                Debug.LogWarning($"[RoomEditModeController] PATCH 실패 ({error.Code}/{error.Message}).");
                // TODO: previousEntry 저장 후 rollback (현재는 visual만 적용, 다음 로드 시 서버와 동기화)
            };

            switch (category)
            {
                case CustomizationCategory.Wallpaper:
                    spaceSettingsRepo.ChangeWallpaper(invId, onSuccess, onError); break;
                case CustomizationCategory.Floor:
                    spaceSettingsRepo.ChangeFloor(invId, onSuccess, onError); break;
                case CustomizationCategory.Desk:
                    spaceSettingsRepo.ChangeDesk(invId, onSuccess, onError); break;
                case CustomizationCategory.DashboardSkin:
                    spaceSettingsRepo.ChangeDashboard(invId, onSuccess, onError); break;
                case CustomizationCategory.InteriorLight:
                    spaceSettingsRepo.ChangeInteriorLight(invId, onSuccess, onError); break;
                case CustomizationCategory.ExteriorLight:
                    spaceSettingsRepo.ChangeExteriorLight(invId, onSuccess, onError); break;
                default:
                    Debug.LogWarning($"[RoomEditModeController] {category}는 카테고리별 PATCH 미지원 (책장/벽/바닥 데코는 별도 endpoint).");
                    break;
            }
        }

        private void ApplyThemeResetVisualFallback()
        {
            lastAppliedEntries.Clear();

            var generator = FindFirstObjectByType<BookshelfGenerator>();
            if (generator != null)
            {
                generator.ResetAllBookshelfDesignStyles();
            }

            // ResetAllBookshelfDesignStyles 가 벽 renderer 까지 건드릴 가능성에 대한 방어적 refresh.
            // IsEditModeActive=false 면 noop — 일반 메인뷰에서는 부담 없음.
            RefreshWallVisibilityAfterMaterialMutation("ApplyThemeResetVisualFallback");

            RebuildInventorySlots();
        }

        // 테마의 defaultEntries 따라 카테고리별 entry 자동 적용. 산뜻 테마 = Desk:201 등록
        // 시 DeskCatalog 의 assetId=201 entry (GoodVibes_Desk) 자동 swap. 사용자가 추후
        // 다른 entry 선택하면 그것이 우선 (PATCH 별개 흐름).
        // PATCH 는 안 보냄 — visual 만. 사용자가 명시적으로 entry 클릭해야 BE 저장.
        private void ApplyThemeDefaultEntries(RoomThemeAsset theme)
        {
            if (theme == null) return;
            Debug.Log($"[RoomEditModeController] ApplyThemeDefaultEntries 진입 — theme={theme.DisplayName}, defaults={(theme.DefaultEntries?.Count ?? 0)}, floorDecos={(theme.FloorDecoSpecs?.Count ?? 0)}, wallDecos={(theme.WallDecoSpecs?.Count ?? 0)}");

            // 1) 이전 테마 deco + 사용자 직접 placement 모두 정리. 필수 배치 (책상/노트북/책장/
            //    벽지/바닥/조명) 는 native + mesh swap 시스템이라 placedItemsRoot 자식이 아니라
            //    영향 없음. 비필수 placement deco 는 모두 초기화 후 새 테마 spawn 만.
            ClearAllPlacedDecos();

            // 1.5) 이전 mesh swap 모두 native 복원. theme 의 defaultEntries 에 해당 카테고리
            // entry 가 있으면 step 2 에서 다시 swap 적용. 없으면 native 유지 (기본 책상/책장 복귀).
            // 이게 없으면 이전 산뜻 테마의 mesh swap 이 default theme 적용 후에도 유지됨.
            RestoreAllMeshSwaps();

            // 2) 항시 카테고리 (Wallpaper/Floor/Desk/BookshelfSkin/DashboardSkin/InteriorLight/
            //    ExteriorLight) — ApplyEntryVisual 로 mesh / material / light swap
            var defaults = theme.DefaultEntries;
            if (defaults != null)
            {
                for (int i = 0; i < defaults.Count; i++)
                {
                    var def = defaults[i];
                    if (def == null) continue;
                    var catalog = LoadCategoryCatalog(def.Category);
                    if (catalog == null || catalog.Entries == null)
                    {
                        Debug.LogWarning($"[RoomEditModeController] catalog 못 찾음: {def.Category} — Resources/Catalogs/{def.Category}Catalog.asset 확인.");
                        continue;
                    }
                    var entry = catalog.FindByAssetId(def.AssetId);
                    if (entry == null)
                    {
                        Debug.LogWarning($"[RoomEditModeController] {theme.DisplayName} default entry {def.Category}:{def.AssetId} 못 찾음 — catalog 에 등록되어 있는지 확인.");
                        continue;
                    }
                    ApplyEntryVisual(def.Category, entry);
                    if (entry.AssetId > 0) lastAppliedEntries[def.Category] = entry.AssetId;
                }
            }

            // 3) 설치 카테고리 (FloorDeco/WallDeco) — placedItemsRoot 자식으로 spawn,
            //    DecorationItemMarker 추가 → 사용자가 drag/select/delete 가능.
            //    다른 테마 적용 시 ClearAllPlacedDecos 가 정리.
            if (theme.FloorDecoSpecs != null)
            {
                for (int i = 0; i < theme.FloorDecoSpecs.Count; i++)
                {
                    SpawnThemeFloorDecoFromSpec(theme.FloorDecoSpecs[i]);
                }
            }
            if (theme.WallDecoSpecs != null)
            {
                for (int i = 0; i < theme.WallDecoSpecs.Count; i++)
                {
                    SpawnThemeWallDecoFromSpec(theme.WallDecoSpecs[i]);
                }
            }

            RebuildInventorySlots();
        }

        // 테마 변경 시 placedItemsRoot 자식 모두 destroy — 이전 테마 spawn + 사용자 직접
        // placement 둘 다. 사용자 요구: "필수 배치 오브젝트 빼고는 테마 적용할 때마다 모든
        // 배치 오브젝트 초기화". 필수 (책상/책장/벽지/바닥/조명/모니터) 는 native 또는 mesh swap
        // 시스템 (roomRoot 자식) 이라 영향 X.
        // 사용자 placement 보존 원하면 별도 marker (KeepOnThemeChange 등) 시스템 도입 필요.
        private void ClearAllPlacedDecos()
        {
            if (placedItemsRoot == null) return;
            for (int i = placedItemsRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = placedItemsRoot.transform.GetChild(i).gameObject;
                if (selectedObject == child) selectedObject = null;
                if (draggingObject == child) draggingObject = null;
                if (UnityEngine.Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

        // [MR 135 review beshu Finding 3 / skh1205 #2] PATCH 실패 시 placement 복구용 hold root.
        // 테마 적용 PATCH 가 비동기 wait 중일 때 placedItemsRoot 자식을 destroy 하지 않고 detach 보존.
        // PATCH 성공 → DiscardPlacementRollbackHold 가 destroy. PATCH 실패 → ReattachPlacementFromRollback
        // 가 placedItemsRoot 로 다시 reparent. lifecycle 은 ApplyThemeSelected 진입 시 begin /
        // onSuccess|onError 에서 end 보장.
        private GameObject placementRollbackHoldRoot;

        // ApplyThemeSelected 진입 시 호출 — placedItemsRoot 자식을 hidden hold root 로 detach.
        // 이전에 detach 된 hold 가 남아 있으면 (예: 같은 프레임 연속 클릭) destroy 후 다시 시작.
        private void DetachPlacementForRollback()
        {
            DiscardPlacementRollbackHold();
            if (placedItemsRoot == null) return;
            if (placedItemsRoot.transform.childCount == 0) return;

            placementRollbackHoldRoot = new GameObject("PlacedDecorations_RollbackHold");
            // placedItemsRoot 의 부모 (보통 Scene root) 자식. 미리 비활성화 — visible 하지 않게.
            var parent = placedItemsRoot.transform.parent;
            if (parent != null)
            {
                placementRollbackHoldRoot.transform.SetParent(parent, false);
            }
            placementRollbackHoldRoot.SetActive(false);

            for (int i = placedItemsRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = placedItemsRoot.transform.GetChild(i);
                // 선택/drag 상태 정리 — hold 중엔 inactive 라 클릭 불가지만 cached 참조는 끊어둠.
                if (selectedObject == child.gameObject) selectedObject = null;
                if (draggingObject == child.gameObject) draggingObject = null;
                child.SetParent(placementRollbackHoldRoot.transform, true); // worldPositionStays=true
            }
        }

        // PATCH 성공 또는 visual 유지 (network failure) 경로 — hold root + 자식 모두 destroy.
        private void DiscardPlacementRollbackHold()
        {
            if (placementRollbackHoldRoot == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(placementRollbackHoldRoot);
            else DestroyImmediate(placementRollbackHoldRoot);
            placementRollbackHoldRoot = null;
        }

        // PATCH 실패 (BE 응답 에러) 경로 — hold root 자식을 placedItemsRoot 로 다시 reparent.
        // 호출 직전에 RestoreAllMeshSwaps + ClearAllPlacedDecos 로 새로 spawn 된 theme deco/mesh swap
        // 정리해야 user placement 복구 후 visual 이 일관됨. selection/drag 상태는 reset.
        private void ReattachPlacementFromRollback()
        {
            if (placementRollbackHoldRoot == null) return;
            if (placedItemsRoot == null)
            {
                EnsurePlacedItemsRoot();
                if (placedItemsRoot == null)
                {
                    DiscardPlacementRollbackHold();
                    return;
                }
            }
            placementRollbackHoldRoot.SetActive(true);
            for (int i = placementRollbackHoldRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = placementRollbackHoldRoot.transform.GetChild(i);
                child.SetParent(placedItemsRoot.transform, true);
            }
            DiscardPlacementRollbackHold();
        }

        // 테마 floorDecoSpec → placedItemsRoot 자식 spawn. PlaceInventoryEntry 와 동일 시스템 —
        // DecorationItemMarker + collider → 사용자가 클릭해 drag/delete 가능. 테마 변경 시
        // ClearAllPlacedDecos 가 일괄 정리 (사용자 placement 와 같은 lifecycle).
        private void SpawnThemeFloorDecoFromSpec(RoomThemeDecoSpec spec)
        {
            if (spec == null || spec.Prefab == null) return;
            EnsurePlacedItemsRoot();

            var go = Instantiate(spec.Prefab);
            go.name = $"ThemeDeco_Floor_{SanitizeName(spec.Prefab.name)}";
            go.transform.SetParent(placedItemsRoot.transform, true);
            // localPosition 을 룸 원점 기준 world 로 적용 (placedItemsRoot scale 1 가정).
            go.transform.position = spec.LocalPosition;
            go.transform.localEulerAngles = spec.LocalRotation;
            go.transform.localScale = spec.LocalScale;

            go.AddComponent<DecorationItemMarker>();
            EnsureCollidersForGameObject(go);
        }

        // 테마 wallDecoSpec → placedItemsRoot 자식 + WallDecoMarker (drag wall surface 동작). spec 의
        // localPosition 은 wall surface anchor frame 기준 — themeApplier.TryGetWallSurfaceAnchor
        // 로 world coord 변환. anchor 못 찾으면 fallback (localPosition 그대로 world).
        private void SpawnThemeWallDecoFromSpec(RoomThemeDecoSpec spec)
        {
            if (spec == null || spec.Prefab == null) return;
            EnsurePlacedItemsRoot();
            if (themeApplier == null) themeApplier = FindFirstObjectByType<RoomThemeApplier>();

            // wall surface anchor 변환 복구 — spec.LocalPosition 은 wall surface frame 기준
            // local. wall placement 시스템 (drag 매칭 + obstruction) 일관성 유지. 사용자가 캡처한
            // world 좌표를 spec 에 박을 때는 anchor frame 으로 변환 필요 (helper: anchor 의 inverse
            // rotation × (worldPos - anchor.position)).
            Vector3 worldPos;
            Quaternion worldRot;
            if (themeApplier != null && themeApplier.TryGetWallSurfaceAnchor(spec.WallSide, out var anchorPos, out var anchorRot))
            {
                worldPos = anchorPos + anchorRot * spec.LocalPosition;
                worldRot = anchorRot * Quaternion.Euler(spec.LocalRotation);
            }
            else
            {
                Debug.LogWarning($"[RoomEditModeController] WallDecoSpec WallSide={spec.WallSide} anchor 못 찾음 — localPosition 을 world fallback.");
                worldPos = spec.LocalPosition;
                worldRot = Quaternion.Euler(spec.LocalRotation);
            }

            var go = Instantiate(spec.Prefab);
            go.name = $"ThemeDeco_Wall_{SanitizeName(spec.Prefab.name)}";
            go.transform.SetParent(placedItemsRoot.transform, true);
            go.transform.SetPositionAndRotation(worldPos, worldRot);
            go.transform.localScale = spec.LocalScale;

            var marker = go.AddComponent<WallDecoMarker>();
            marker.CurrentWall = spec.WallSide;
            marker.AllowRotation = false; // 테마 자동 spawn 은 회전 비활성 (사용자가 위치만 옮김)

            go.AddComponent<DecorationItemMarker>();
            EnsureCollidersForGameObject(go);
        }

        // 일반화된 collider 보장 — root 에 BoxCollider 를 renderer bounds 에 맞춰 합성.
        // [MR 135 fix] PlaceInventoryEntry Step 4 와 동일 패턴 — GLB 가 root 에 mesh 없는 빈
        // MeshCollider 만 들고 import 되는 케이스 (Hierarchy 자식 없음 + Inspector Mesh 비어있음 +
        // Physics.Raycast hit 0) 회피. 기존 collider 가 있어도 추가로 BoxCollider 를 root 에
        // 박아서 raycast selection 100% 보장. 비용은 collider 1개 추가뿐이라 무시 가능.
        // WallMaskQuad 의 collider 추가는 stencil 전용 mesh 라 명시적으로 skip.
        private static void EnsureCollidersForGameObject(GameObject go)
        {
            // mesh 자식에도 MeshCollider 합성 (정밀 hit 가 필요한 케이스 대비 — drag/select 는
            // root BoxCollider 만으로도 동작하지만, 향후 다른 사용처에서 mesh-precise hit 필요할
            // 수도 있어 retain).
            var meshFilters = go.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                var mf = meshFilters[i];
                if (mf == null || mf.sharedMesh == null) continue;
                if (mf.gameObject.name == "WallMaskQuad") continue;
                if (mf.GetComponent<Collider>() != null) continue;
                var collider = mf.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mf.sharedMesh;
                collider.convex = false;
            }

            // root 에 BoxCollider 가 이미 있으면 skip (중복 회피). 없으면 renderer bounds 로 합성.
            // PlaceInventoryEntry 와 동일 좌표 변환 (world → local, 음수 size 방지 Mathf.Abs).
            if (go.GetComponent<BoxCollider>() == null)
            {
                var rendererBounds = ComputeRendererBounds(go);
                if (rendererBounds.HasValue)
                {
                    var box = go.AddComponent<BoxCollider>();
                    var localCenter = go.transform.InverseTransformPoint(rendererBounds.Value.center);
                    var localSize = go.transform.InverseTransformVector(rendererBounds.Value.size);
                    box.center = localCenter;
                    box.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
                }
            }
        }

        // 카테고리별 visual swap. RoomThemeApplier의 RoomRoot/DirectionalLight reference를
        // 재사용해 GameObject.Find/FindObjectsByType 매 클릭마다 호출 안 하도록.
        // 매칭 패턴은 RoomGameObjectPatterns로 통합(RoomThemeApplier와 동일).
        private void ApplyEntryVisual(CustomizationCategory category, CustomizationEntry entry)
        {
            if (themeApplier == null)
            {
                themeApplier = FindFirstObjectByType<Dotshelf.Scripts.RoomTheme.RoomThemeApplier>();
                if (themeApplier == null)
                {
                    Debug.LogWarning("[RoomEditModeController] RoomThemeApplier 인스턴스 없음 — visual swap skip.");
                    return;
                }
            }

            // 라이트 카테고리: applier가 들고 있는 Directional Light + ambient 직접 set
            if (category == CustomizationCategory.ExteriorLight)
            {
                var dirLight = themeApplier.DirectionalLight;
                if (dirLight != null)
                {
                    dirLight.color = entry.LightColor;
                    dirLight.useColorTemperature = true;
                    dirLight.colorTemperature = entry.ColorTemperatureKelvin;
                    dirLight.intensity = entry.LightIntensity;
                }
                return;
            }
            if (category == CustomizationCategory.InteriorLight)
            {
                RenderSettings.ambientLight = entry.LightColor;
                RenderSettings.ambientIntensity = entry.LightIntensity;
                return;
            }

            var roomRootTransform = themeApplier.RoomRoot;
            if (roomRootTransform == null) return;

            // mesh swap — entry.Prefab 있으면 native renderer disable + prefab 자식 spawn.
            // Desk/BookshelfSkin/DashboardSkin 처럼 mesh 자체가 바뀌어야 하는 카테고리. 기존
            // material-only swap 과 공존 — prefab 우선, 없으면 material swap.
            if (entry.Prefab != null && IsMeshSwapCategory(category))
            {
                ApplyCategoryMeshSwap(category, entry, roomRootTransform);
                return;
            }
            // entry.Prefab 가 null 이지만 이전에 mesh swap 됐으면 복원. (다른 entry 선택 시
            // native mesh 가 다시 보이도록.)
            if (IsMeshSwapCategory(category))
            {
                RestoreCategoryNativeMesh(category, roomRootTransform);
            }

            var mat = entry.PrimaryMaterial;
            if (mat == null) return;

            int swapped = 0;
            foreach (var renderer in roomRootTransform.GetComponentsInChildren<Renderer>(true))
            {
                if (MatchesCategoryName(category, renderer.gameObject.name))
                {
                    renderer.sharedMaterial = mat;
                    swapped++;
                }
            }
            if (swapped == 0)
            {
                Debug.LogWarning($"[RoomEditModeController] {category} 매칭 0개 (entry={entry.DisplayName}).");
            }
            // 벽지(Wallpaper) / 바닥 등 카테고리는 벽 renderer 의 sharedMaterial 을 swap 해서
            // 편집 모드의 wall fade instance 를 풀어버린다 → 즉시 재적용.
            RefreshWallVisibilityAfterMaterialMutation($"ApplyEntryVisual/{category}");
        }

        // mesh swap 가능 카테고리 — 외형 mesh 자체가 entry 별로 다른 카테고리들.
        // Wallpaper/Floor 는 평면 material swap 으로 충분 → 제외.
        // InteriorLight/ExteriorLight 는 light component property — 별도 처리 (위쪽 분기).
        private static bool IsMeshSwapCategory(CustomizationCategory category)
        {
            return category == CustomizationCategory.Desk
                || category == CustomizationCategory.BookshelfSkin
                || category == CustomizationCategory.DashboardSkin;
        }

        // category 매칭 native renderer 들 disable + prefab 을 첫 매칭 anchor 자식으로 spawn.
        // NotebookStation 같은 자식 transform 은 native 그대로 → 인터랙션 보존.
        // 이전 swap spawn 한 prefab 있으면 destroy 후 새로.
        // entry 단위로 받음 — entry.Prefab + entry.PlacementAnchor/Rotation/Scale + entry.NotebookStationOffset
        // 모두 활용. 사용자가 catalog Inspector 에서 mesh swap 시 fitting (위치/회전/스케일/모니터 위치)
        // 직접 지정 가능.
        private void ApplyCategoryMeshSwap(CustomizationCategory category, CustomizationEntry entry, Transform roomRoot)
        {
            // 1) 이전 mesh swap 된 GameObject 정리 (이름 prefix 로 식별)
            string swapName = $"MeshSwap_{category}";
            for (int i = roomRoot.childCount - 1; i >= 0; i--)
            {
                var child = roomRoot.GetChild(i);
                if (child.gameObject.name == swapName)
                {
                    if (UnityEngine.Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }

            // 2) primary anchor 검색 — 카테고리별 정확한 root transform 우선. "DeskRoot" /
            //    "BookshelfRoot" 같은 이름 정확 매칭 우선. 첫 매칭 transform (예: NotebookStation
            //    또는 Desk_*Strip) 이 anchor 가 되면 GLB prefab spawn 위치가 native 와 어긋남.
            var anchor = FindPrimaryAnchor(roomRoot, category);

            // 3) native renderer 들 disable (visual hide, GameObject 자체는 유지 — collider/
            // 기능 컴포넌트 보존). 카테고리 매칭 transform 의 모든 자식 renderer 까지 hide —
            // 단 **NotebookStation hierarchy 는 절대 hide 안 함**. 노트북은 고정 interactive
            // 오브젝트 (클릭 시 dashboard UI) 라 Desk/Dashboard 등 어떤 mesh swap 에서도 visual
            // 보존 필수. 과거엔 Dashboard 패턴의 GuestbookBoard 매칭 → 자식 scan 으로 NotebookStation
            // 자식 (UnibodyBase/Trackpad) 도 같이 disable 되어 편집 모드 진입 시 노트북이 사라지는
            // 회귀 발생. 매칭 transform 자체가 NotebookStation 인 경우는 Desk 패턴에서 이미 제외됐고,
            // 부모 매칭 (GuestbookBoard) 경유 자식 disable 만 여기서 guard.
            var allTransforms = roomRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                var t = allTransforms[i];
                if (!MatchesCategoryName(category, t.gameObject.name)) continue;
                if (anchor == null) anchor = t; // primary 못 찾으면 첫 매칭 fallback
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    if (IsUnderNotebookStation(r.transform, t)) continue;
                    r.enabled = false;
                }
            }
            if (anchor == null)
            {
                Debug.LogWarning($"[RoomEditModeController] {category} mesh swap — native anchor 못 찾음, prefab spawn skip.");
                return;
            }

            // 4) prefab spawn — entry.PlacementAnchor/Rotation/Scale 을 world 로 직접 해석.
            // 사용자가 Unity scene 에서 시각으로 잡은 transform 값을 catalog Inspector 에 그대로
            // 입력 가능. native anchor 위치 무시 — 책상별 크기/높이 자유 fit.
            var go = Instantiate(entry.Prefab, roomRoot);
            go.name = swapName;
            go.transform.SetPositionAndRotation(entry.PlacementAnchor, Quaternion.Euler(entry.PlacementRotation));
            go.transform.localScale = entry.PlacementScale != Vector3.zero ? entry.PlacementScale : Vector3.one;

            // 5) Desk 카테고리 — NotebookStation override (모니터 진입 위치/스케일 fit).
            //    OverrideNotebookStation=true 면 entry 의 NotebookStation* 값 적용. false 면 native.
            if (category == CustomizationCategory.Desk)
            {
                ApplyNotebookStationPlacement(roomRoot, entry);
                // Monitor sub-prefab — 책상의 일부로 함께 spawn (사용자 placement 불가, 고정).
                ApplyMonitorMeshSwap(roomRoot, entry);
            }
        }

        // 모든 mesh swap 카테고리 native 복원 — RestoreCategoryNativeMesh 의 일괄 호출. 이전
        // 테마의 mesh swap (e.g. 산뜻 책상) 을 native (procedural 책상/노트북) 로 되돌림.
        // 새 테마의 defaultEntries 가 다시 swap 하면 그 시점에 새 mesh 로 교체.
        private void RestoreAllMeshSwaps()
        {
            if (themeApplier == null) themeApplier = FindFirstObjectByType<RoomThemeApplier>();
            var roomRoot = themeApplier?.RoomRoot;
            if (roomRoot == null) return;
            RestoreCategoryNativeMesh(CustomizationCategory.Desk, roomRoot);
            RestoreCategoryNativeMesh(CustomizationCategory.BookshelfSkin, roomRoot);
            RestoreCategoryNativeMesh(CustomizationCategory.DashboardSkin, roomRoot);
        }

        // Desk entry 의 monitorPrefab spawn — book 책상 mesh swap 의 일부. 별도 GameObject (이름
        // MeshSwap_DeskMonitor) 로 roomRoot 자식. 다음 desk 적용 또는 RestoreCategoryNativeMesh 시
        // 같이 정리.
        private void ApplyMonitorMeshSwap(Transform roomRoot, CustomizationEntry entry)
        {
            // 이전 monitor swap 정리
            const string monitorSwapName = "MeshSwap_DeskMonitor";
            for (int i = roomRoot.childCount - 1; i >= 0; i--)
            {
                var child = roomRoot.GetChild(i);
                if (child.gameObject.name == monitorSwapName)
                {
                    if (UnityEngine.Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }

            if (entry.MonitorPrefab == null) return;

            var go = Instantiate(entry.MonitorPrefab, roomRoot);
            go.name = monitorSwapName;
            go.transform.SetPositionAndRotation(entry.MonitorPosition, Quaternion.Euler(entry.MonitorRotation));
            go.transform.localScale = entry.MonitorScale != Vector3.zero ? entry.MonitorScale : Vector3.one;
        }

        // NotebookStation 의 원래 transform 보존용 — 첫 swap 호출 시 저장, restore 시 복원.
        private Vector3? nativeNotebookStationLocalPos;
        private Quaternion? nativeNotebookStationLocalRot;
        private Vector3? nativeNotebookStationLocalScale;

        private void ApplyNotebookStationPlacement(Transform roomRoot, CustomizationEntry entry)
        {
            var notebookStation = FindChildByName(roomRoot, "NotebookStation");
            if (notebookStation == null) return;
            // 첫 호출 시 native 보존
            if (!nativeNotebookStationLocalPos.HasValue)
            {
                nativeNotebookStationLocalPos = notebookStation.localPosition;
                nativeNotebookStationLocalRot = notebookStation.localRotation;
                nativeNotebookStationLocalScale = notebookStation.localScale;
            }
            if (!entry.OverrideNotebookStation)
            {
                // override 안 함 → native 복원
                notebookStation.localPosition = nativeNotebookStationLocalPos.Value;
                notebookStation.localRotation = nativeNotebookStationLocalRot.Value;
                notebookStation.localScale = nativeNotebookStationLocalScale.Value;
                return;
            }
            // override — entry 값을 NotebookStation 의 부모 (GuestbookBoard) 기준 local 로 적용.
            // Inspector 의 Transform 은 default Local 이므로 사용자 캡처값 그대로 박힘.
            notebookStation.localPosition = entry.NotebookStationPosition;
            notebookStation.localRotation = Quaternion.Euler(entry.NotebookStationRotation);
            notebookStation.localScale = entry.NotebookStationScale != Vector3.zero ? entry.NotebookStationScale : Vector3.one;
        }

        private void RestoreNotebookStationPosition(Transform roomRoot)
        {
            if (!nativeNotebookStationLocalPos.HasValue) return;
            var notebookStation = FindChildByName(roomRoot, "NotebookStation");
            if (notebookStation == null) return;
            notebookStation.localPosition = nativeNotebookStationLocalPos.Value;
            notebookStation.localRotation = nativeNotebookStationLocalRot ?? Quaternion.identity;
            notebookStation.localScale = nativeNotebookStationLocalScale ?? Vector3.one;
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            var allTransforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                if (allTransforms[i].gameObject.name == name) return allTransforms[i];
            }
            return null;
        }

        // renderer 의 부모 체인 (matchedTransform 경계까지) 에 NotebookStation 이름의 transform 이
        // 있는지 검사. 있으면 그 renderer 는 노트북 hierarchy 일부 → mesh swap 시 disable 금지.
        // 노트북은 고정 interactive 오브젝트라 모든 카테고리 mesh swap 에서 visual 보존.
        // (MR #157 review simonjiho #1: hardcoded 문자열 → RoomGameObjectPatterns.NotebookStation 상수 통일.)
        private static bool IsUnderNotebookStation(Transform renderer, Transform matchedTransform)
        {
            var current = renderer;
            while (current != null)
            {
                if (current.gameObject.name == Dotshelf.Scripts.RoomTheme.RoomGameObjectPatterns.NotebookStation) return true;
                if (current == matchedTransform) return false; // matched 까지 올라갔으면 노트북 부모 없었음
                current = current.parent;
            }
            return false;
        }

        // 카테고리별 primary anchor — native 룸의 root transform. mesh swap 시 이 위치/회전을
        // 그대로 GLB prefab 에 적용해 native 와 일치시킴.
        private static Transform FindPrimaryAnchor(Transform roomRoot, CustomizationCategory category)
        {
            string primaryName = null;
            switch (category)
            {
                case CustomizationCategory.Desk:           primaryName = "DeskRoot"; break;
                case CustomizationCategory.BookshelfSkin:  primaryName = "BookshelfRoot"; break;
                case CustomizationCategory.DashboardSkin:  primaryName = "DashboardScreenPanel"; break;
                default: return null;
            }
            var allTransforms = roomRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                if (allTransforms[i].gameObject.name == primaryName) return allTransforms[i];
            }
            return null;
        }

        // mesh swap 복원 — native renderer 들 enable + spawned mesh swap 정리 + Desk 의 경우
        // NotebookStation localPosition + Monitor sub-prefab 도 native 복원.
        private void RestoreCategoryNativeMesh(CustomizationCategory category, Transform roomRoot)
        {
            if (category == CustomizationCategory.Desk)
            {
                RestoreNotebookStationPosition(roomRoot);
                // monitor sub-prefab 정리
                const string monitorSwapName = "MeshSwap_DeskMonitor";
                for (int i = roomRoot.childCount - 1; i >= 0; i--)
                {
                    var child = roomRoot.GetChild(i);
                    if (child.gameObject.name == monitorSwapName)
                    {
                        if (UnityEngine.Application.isPlaying) Destroy(child.gameObject);
                        else DestroyImmediate(child.gameObject);
                    }
                }
            }
            string swapName = $"MeshSwap_{category}";
            for (int i = roomRoot.childCount - 1; i >= 0; i--)
            {
                var child = roomRoot.GetChild(i);
                if (child.gameObject.name == swapName)
                {
                    if (UnityEngine.Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }
            // ApplyCategoryMeshSwap 와 대칭 — transform 단위 매칭 후 자식 renderer 까지 enable.
            var allTransforms = roomRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                var t = allTransforms[i];
                if (!MatchesCategoryName(category, t.gameObject.name)) continue;
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    r.enabled = true;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Local preview detection + RoomLighting GameObject 생성
        //
        // catalog asset 의 isLocalPreview 가 disk 에 박힌 상태가 아니어도 (이전 placeholder 메뉴 실행본 등),
        // assetId 범위만으로 placeholder 판정해 PATCH 401/403 차단. DesignPreviewPackCreatorUtility 가
        // 사용하는 ID range 는 100000+ (legacy 무테마) / 200000+ (5 테마별). 진짜 서버 inventory item 은
        // 이 범위 밖이라고 가정 (현재 BE schema 상 assetId 가 BIGINT auto-increment 라 실제 운영 ID 와 충돌 없음).
        private const long LocalPreviewAssetIdMin = 100000L;
        private const long LocalPreviewAssetIdMax = 299999L;

        private static bool IsLocalPreviewEntry(CustomizationEntry entry)
        {
            if (entry == null) return false;
            if (entry.IsLocalPreview) return true;
            return entry.AssetId >= LocalPreviewAssetIdMin && entry.AssetId <= LocalPreviewAssetIdMax;
        }

        private static bool IsLocalPreviewTheme(RoomThemeAsset theme)
        {
            if (theme == null) return false;
            if (theme.IsLocalPreview) return true;
            // assetId == 0 이면 backend 미연결 — 기존 ApplyThemeSelected 도 이미 0 일 때 PATCH skip 했음.
            return theme.AssetId <= 0;
        }

        // local preview 클릭 시 RoomLighting 하위에 Preview Light GameObject 를 생성/갱신.
        // 사용자 요구사항: RoomLighting/Preview_InternalLight, RoomLighting/Preview_ExternalLight.
        // 같은 카테고리 재클릭 시 기존 Light 컴포넌트 재사용 → 중복 생성 차단.
        // 다른 테마 조명을 클릭하면 같은 자식이 색/강도만 갱신 (이름 동일 → Find 매칭).
        private const string PreviewLightingRootName = "RoomLighting";
        private const string PreviewInternalLightName = "Preview_InternalLight";
        private const string PreviewExternalLightName = "Preview_ExternalLight";

        // RoomLighting 루트 cache — ApplyLocalLightingPreview 가 매 클릭마다 GameObject.Find(전체 씬 scan)
        // 호출하지 않도록. 씬 reload / 외부 destroy 로 transform null 이 되면 재해결.
        // (MR 109 review — simonjiho #4 / 0w0n2x #2)
        private Transform cachedPreviewLightingRoot;

        private Transform ResolveOrCreatePreviewLightingRoot()
        {
            if (cachedPreviewLightingRoot != null)
            {
                return cachedPreviewLightingRoot;
            }
            var existing = GameObject.Find(PreviewLightingRootName);
            if (existing == null)
            {
                existing = new GameObject(PreviewLightingRootName);
            }
            cachedPreviewLightingRoot = existing.transform;
            return cachedPreviewLightingRoot;
        }

        private void ApplyLocalLightingPreview(CustomizationCategory category, CustomizationEntry entry)
        {
            var rootTransform = ResolveOrCreatePreviewLightingRoot();
            var rootGo = rootTransform.gameObject;

            string childName;
            bool isInterior;
            if (category == CustomizationCategory.InteriorLight)
            {
                childName = PreviewInternalLightName;
                isInterior = true;
            }
            else if (category == CustomizationCategory.ExteriorLight)
            {
                childName = PreviewExternalLightName;
                isInterior = false;
            }
            else
            {
                return;
            }

            var existing = rootGo.transform.Find(childName);
            GameObject lightGo = existing != null
                ? existing.gameObject
                : new GameObject(childName);
            if (existing == null)
            {
                lightGo.transform.SetParent(rootGo.transform, false);
            }

            var light = lightGo.GetComponent<Light>();
            if (light == null)
            {
                light = lightGo.AddComponent<Light>();
            }

            if (isInterior)
            {
                // 천장 가까운 룸 중심에 Point — RoomHeight 약 2.8m 이므로 y=2.4 정도.
                light.type = LightType.Point;
                lightGo.transform.localPosition = new Vector3(0f, 2.4f, 0f);
                light.range = 6f;
            }
            else
            {
                // 외부광 — 통창(back wall, +Z 방향) 너머에서 안쪽으로 비춤. Natural 은 intensity 자체가 강함.
                light.type = LightType.Directional;
                lightGo.transform.localPosition = Vector3.zero;
                lightGo.transform.localRotation = Quaternion.Euler(35f, 200f, 0f);
            }

            light.color = entry.LightColor;
            light.useColorTemperature = true;
            light.colorTemperature = entry.ColorTemperatureKelvin;
            light.intensity = entry.LightIntensity;
            // 프리뷰는 그림자 비활성 — 모바일/Editor 성능 + URP 리얼타임 그림자 budget 보존.
            light.shadows = LightShadows.None;
        }

        // RoomGameObjectPatterns에 통합된 매칭 규칙을 카테고리별로 위임. RoomThemeApplier와 동일.
        private static bool MatchesCategoryName(CustomizationCategory category, string name)
        {
            switch (category)
            {
                case CustomizationCategory.Wallpaper:     return Dotshelf.Scripts.RoomTheme.RoomGameObjectPatterns.IsWallName(name);
                case CustomizationCategory.Floor:         return name == Dotshelf.Scripts.RoomTheme.RoomGameObjectPatterns.Floor;
                case CustomizationCategory.BookshelfSkin: return Dotshelf.Scripts.RoomTheme.RoomGameObjectPatterns.MatchesPrefix(name, Dotshelf.Scripts.RoomTheme.RoomGameObjectPatterns.Bookshelf);
                case CustomizationCategory.Desk:          return Dotshelf.Scripts.RoomTheme.RoomGameObjectPatterns.MatchesPrefix(name, Dotshelf.Scripts.RoomTheme.RoomGameObjectPatterns.Desk);
                case CustomizationCategory.DashboardSkin: return Dotshelf.Scripts.RoomTheme.RoomGameObjectPatterns.MatchesPrefix(name, Dotshelf.Scripts.RoomTheme.RoomGameObjectPatterns.Dashboard);
                default: return false;
            }
        }

        private ISpaceSettingsRepository spaceSettingsRepo;

        private void EnsureSpaceSettingsRepository()
        {
            if (spaceSettingsRepo != null) return;
            var apiManager = APIManager.Instance;
            if (apiManager == null) return;
            try
            {
                var apiClient = new NotebookApiClient(apiManager);
                spaceSettingsRepo = new ApiSpaceSettingsRepository(apiClient);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoomEditModeController] ApiSpaceSettingsRepository 생성 실패 ({ex.GetType().Name}).");
                Debug.LogException(ex);
            }
        }

        // Server Space 상태로 룸 visual 재동기화. themeReset 응답 후 / 테마 적용 응답 후 호출.
        // GET /v1/spaces/me → 6 카테고리 ID 읽기 → catalog에서 entry 찾아 visual 재적용 + lastAppliedEntries 갱신.
        // 책장 skin / Layout placement 재 spawn은 follow-up (RoomThemeApplier 설치 spec list 합류 시점).
        //
        // race fix: GET이 비동기라 응답 도착 전에 사용자가 다른 카테고리 PATCH를 시작할 수 있음.
        // 그 경우 늦게 도착한 GET 응답이 사용자의 새 선택을 stale 상태로 덮으면 안 됨. 호출 시점의
        // categoryRequestVersion을 캡처하고, 응답 시 카운터가 증가했으면 (PATCH가 그 사이 발생)
        // GET 결과 무시 + lastServerSpace는 다음 PATCH의 OptimisticallyUpdateServerSpace에 의존.
        private void ReloadRoomState(Action onComplete = null)
        {
            var apiManager = APIManager.Instance;
            if (apiManager == null)
            {
                Debug.LogWarning("[RoomEditModeController] APIManager.Instance == null — ReloadRoomState skip.");
                onComplete?.Invoke();
                return;
            }

            NotebookApiClient apiClient;
            try
            {
                apiClient = new NotebookApiClient(apiManager);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoomEditModeController] NotebookApiClient 생성 실패 ({ex.GetType().Name}).");
                Debug.LogException(ex);
                onComplete?.Invoke();
                return;
            }

            int capturedCategoryVersion = categoryRequestVersion;
            apiClient.GetMySpace(
                space =>
                {
                    if (categoryRequestVersion != capturedCategoryVersion)
                    {
                        Debug.Log("[RoomEditModeController] stale GET /spaces/me 응답 — 이후 PATCH 발생으로 무시.");
                        onComplete?.Invoke();
                        return;
                    }
                    ApplySpaceVisualsToRoom(space);
                    onComplete?.Invoke();
                },
                error =>
                {
                    Debug.LogWarning($"[RoomEditModeController] GET /spaces/me 실패 — reload skip ({error?.Code}/{error?.Message}).");
                    onComplete?.Invoke();
                });
        }

        // Space 응답의 6 카테고리 ID로 visual 재적용. catalog에 매칭 entry 없으면 default(첫 entry).
        // null/0 ID는 \"카테고리별 미적용\" 의미 — default로 fallback (테마 reset 후 default 외형 복귀).
        private void ApplySpaceVisualsToRoom(SpaceResponseDto space)
        {
            if (space == null) return;
            lastServerSpace = space;  // IsCategoryEntryCurrent의 single source of truth 갱신

            // [Review #104 P2] server에 저장된 테마 inventory ID로 RoomThemeApplier.CurrentTheme도
            // 동기화. 동기화 안 하면 이전 세션에서 customization-disabled 테마를 적용해둔 사용자가
            // 다시 입장해도 RoomThemeApplier.CurrentTheme=null이라 IsCurrentThemeCustomizationLocked
            // 가 항상 false → 사전 확인 모달 미발동 → 사용자는 themeReset=true 응답을 받고서야 테마
            // 해제를 인지하게 됨.
            if (themeApplier == null) themeApplier = FindFirstObjectByType<RoomThemeApplier>();
            // 테마가 server reload 로 변경됐으면 default entries (mesh swap + 자동 placement deco)
            // 까지 재적용 — 안 그러면 [MR 135 review beshu Finding 2] 처럼 클릭 직후엔 보이던
            // 산뜻 책상/네온/창문/러그/침대/쿠션이 재입장 / ReloadRoomState 후 사라지는 회귀.
            // 단 동일 테마 reload 시엔 placement 보존 (사용자가 옮겼을 가능성). themeApplier.Apply 도
            // 동일 가드.
            bool themeChanged = false;
            RoomThemeAsset resolvedTheme = null;
            if (themeApplier != null && themeCatalog != null)
            {
                resolvedTheme = themeCatalog.Resolve(space.CurrentThemeInventoryId);
                if (resolvedTheme != null && resolvedTheme != themeApplier.CurrentTheme)
                {
                    themeApplier.Apply(resolvedTheme);
                    RefreshWallVisibilityAfterMaterialMutation("ReloadRoomState");
                    themeChanged = true;
                }
            }

            // 테마 default entries 적용 — themeChanged 시에만. ApplyCategoryById 보다 먼저 호출해서
            // server-saved 카테고리 (있다면) 가 default 위에 덮어쓰도록.
            if (themeChanged && resolvedTheme != null)
            {
                ApplyThemeDefaultEntries(resolvedTheme);
            }

            ApplyCategoryById(CustomizationCategory.Wallpaper, space.CurrentWallpaperInventoryId);
            ApplyCategoryById(CustomizationCategory.Floor, space.CurrentFloorInventoryId);
            ApplyCategoryById(CustomizationCategory.Desk, space.CurrentDeskInventoryId);
            ApplyCategoryById(CustomizationCategory.DashboardSkin, space.CurrentDashboardInventoryId);
            ApplyCategoryById(CustomizationCategory.InteriorLight, space.CurrentInteriorLightInventoryId);
            ApplyCategoryById(CustomizationCategory.ExteriorLight, space.CurrentExteriorLightInventoryId);
            RebuildInventorySlots();
        }

        private void ApplyCategoryById(CustomizationCategory category, long? inventoryId)
        {
            var catalog = LoadCategoryCatalog(category);
            if (catalog == null || catalog.Entries == null || catalog.Entries.Count == 0) return;

            CustomizationEntry entry = null;
            if (inventoryId.HasValue && inventoryId.Value > 0)
            {
                for (int i = 0; i < catalog.Entries.Count; i++)
                {
                    var e = catalog.Entries[i];
                    if (e != null && e.AssetId == inventoryId.Value)
                    {
                        entry = e;
                        break;
                    }
                }
            }

            // (MR #157 review skh1205 #2): mesh swap 카테고리 (Desk/BookshelfSkin/DashboardSkin) 에서
            // inventoryId 가 null/0 = "customize 안 됨" 이면 catalog Entries[0] fallback 으로 가지 말고
            // **native 상태 유지**. 이전 정책 (Entries[0] fallback) 은 DeskCatalog Entries[0].AssetId=200030
            // (Goodvibes Desk) 인 fresh session 에서 첫 reload 가 Goodvibes mesh swap 을 실행해 고정
            // 노트북/테이블이 의도치 않게 바뀌던 회귀 fix.
            //
            // 정책:
            //   - mesh swap 카테고리 + null/0: lastAppliedEntries 에 prev 가 있을 때만 (=이전에 swap
            //     됐던 흔적 있을 때만) RestoreCategoryNativeMesh 호출. fresh session 매 reload 는 noop.
            //   - 비-mesh-swap 카테고리 (Wallpaper/Floor/InteriorLight/ExteriorLight) 는 material/light
            //     만 바뀌므로 Entries[0] fallback 유지 — 디자이너 의도된 default 외형.
            if (entry == null && IsMeshSwapCategory(category))
            {
                if (lastAppliedEntries.ContainsKey(category)
                    && themeApplier != null
                    && themeApplier.RoomRoot != null)
                {
                    RestoreCategoryNativeMesh(category, themeApplier.RoomRoot);
                }
                lastAppliedEntries.Remove(category);
                return;
            }
            if (entry == null && catalog.Entries.Count > 0)
            {
                entry = catalog.Entries[0];
            }
            if (entry == null) return;

            // Idempotency: 이미 같은 entry 가 적용되어 있으면 visual 작업 skip.
            // (MR #154 user 보고: 편집 모드 진입 시 ReloadRoomState 가 매번 ApplyCategoryById 호출 →
            //  catalog Entries[0] fallback → ApplyCategoryMeshSwap 가 native 메시 destroy/respawn 회귀 fix.)
            //
            // 직접 user 클릭 / ApplyThemeDefaultEntries 같은 다른 호출 경로는 ApplyEntryVisual 을
            // 직접 호출하므로 이 guard 영향 없음.
            if (lastAppliedEntries.TryGetValue(category, out var prevId) && prevId == entry.AssetId)
            {
                return;
            }

            ApplyEntryVisual(category, entry);
            if (entry.AssetId > 0) lastAppliedEntries[category] = entry.AssetId;
            else lastAppliedEntries.Remove(category);
        }

        private void BuildInventorySlot(Transform parent, int slotIndex, InventoryEntry entry)
        {
            var slot = CreateImage(parent, $"InventorySlot_{slotIndex}", CardColor);
            slot.sprite = roundedRectSprite;
            slot.type = Image.Type.Sliced;
            var rect = slot.rectTransform;
            // Compact card: ~108×128 leaves room for ~9 slots across a 1080-wide
            // canvas before scrolling kicks in (with the 8px spacing).
            rect.sizeDelta = new Vector2(108f, 128f);

            var layout = slot.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 108f;
            layout.preferredWidth = 108f;
            layout.minHeight = 128f;
            layout.preferredHeight = 128f;

            var outline = slot.gameObject.AddComponent<Outline>();
            outline.effectColor = WithAlpha(BorderColor, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);

            // Placeholder thumbnail tile — beige square + monogram from the model
            // name until proper model previews are wired in.
            var thumb = CreateImage(slot.transform, "Thumb", Hex("#E8DFCC"));
            thumb.sprite = roundedRectSprite;
            thumb.type = Image.Type.Sliced;
            var thumbRect = thumb.rectTransform;
            thumbRect.anchorMin = new Vector2(0.5f, 1f);
            thumbRect.anchorMax = new Vector2(0.5f, 1f);
            thumbRect.pivot = new Vector2(0.5f, 1f);
            thumbRect.sizeDelta = new Vector2(80f, 80f);
            thumbRect.anchoredPosition = new Vector2(0f, -8f);

            var glyph = CreateText(thumb.transform, "Glyph",
                ResolveSlotInitial(entry.DisplayName), 28f, FontStyles.Bold, MutedTextColor, TextAlignmentOptions.Center);
            StretchToParent(glyph.rectTransform);

            var labelText = CreateText(slot.transform, "Label", entry.DisplayName, 12f, FontStyles.Bold, TextColor, TextAlignmentOptions.Center);
            labelText.overflowMode = TextOverflowModes.Ellipsis;
            var labelRect = labelText.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.sizeDelta = new Vector2(-8f, 24f);
            labelRect.anchoredPosition = new Vector2(0f, 6f);

            var button = slot.gameObject.AddComponent<Button>();
            button.targetGraphic = slot;
            var capturedEntry = entry;
            button.onClick.AddListener(() => PlaceInventoryEntry(capturedEntry));

            // 동일 inventoryKey 가 이미 배치되어 있으면 슬롯 자체를 비활성 + "배치됨" 오버레이로 가린다.
            // 삭제하면 RebuildInventorySlots 가 다시 돌면서 자동 복구된다.
            var key = BuildInventoryKey(entry);
            if (IsInventoryKeyPlaced(key))
            {
                button.interactable = false;
                slot.color = WithAlpha(CardColor, 0.55f);
                thumb.color = WithAlpha(thumb.color, 0.55f);
                glyph.color = WithAlpha(MutedTextColor, 0.55f);
                labelText.color = WithAlpha(TextColor, 0.55f);

                var placedTag = CreateImage(slot.transform, "PlacedTag", Hex("#3A3A3A"));
                placedTag.sprite = roundedRectSprite;
                placedTag.type = Image.Type.Sliced;
                var tagRect = placedTag.rectTransform;
                tagRect.anchorMin = new Vector2(0.5f, 1f);
                tagRect.anchorMax = new Vector2(0.5f, 1f);
                tagRect.pivot = new Vector2(0.5f, 1f);
                tagRect.sizeDelta = new Vector2(64f, 22f);
                tagRect.anchoredPosition = new Vector2(0f, -4f);

                var tagLabel = CreateText(placedTag.transform, "Label", "배치됨", 11f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
                StretchToParent(tagLabel.rectTransform);
            }
        }

        // 인벤토리 자산 식별 키.
        //
        // TODO(server-inventory): EditableDecorationCatalog 에 inventoryId / assetId 가 추가되면
        // "asset:{AssetId}" / "inv:{InventoryId}" 가 정식 키. 그 시점에는 fallback 들을 dev-only
        // assertion 으로 좁히고 prod 에서는 서버 ID 만 신뢰. PR !120 review #2 (beshu) 참조.
        //
        // 현재 우선순위 (안정성 큰 순):
        //   1. SourceAssetPath: 카탈로그 asset path — 자산이 이동/리네이밍되지 않는 한 고유.
        //      "path:" prefix 로 다른 fallback 키와 절대 충돌하지 않게 namespace 분리.
        //   2. Prefab.GetInstanceID(): asset GUID 기반 런타임 InstanceID. 한 세션 내에선 자산당 고유 →
        //      DisplayName 충돌(동명 자산) 보다 안전. 단 세션 간 안정성은 없음.
        //   3. DisplayName: 마지막 fallback. 동명 자산 위험 인지 후 사용.
        // 호출부(BuildInventorySlot / PlaceInventoryEntry / DeleteSelectedObject / DecorationItemMarker.InventoryKey
        //  scene scan) 모두 같은 메서드를 거치므로 여기서 prefix 만 일관되면 양쪽 매칭이 자동.
        private static string BuildInventoryKey(InventoryEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.SourceAssetPath))
            {
                return "path:" + entry.SourceAssetPath;
            }
            if (entry.Prefab != null)
            {
                return "prefab-iid:" + entry.Prefab.GetInstanceID();
            }
            if (!string.IsNullOrEmpty(entry.DisplayName))
            {
                return "name:" + entry.DisplayName;
            }
            return null;
        }

        // 평탄화(MR #154) 이전 저장본 호환 alias — 사용자 layout JSON 의 InventoryKey 에 박힌 old
        // path (예: "path:Assets/Arts/Models/Props/book_stack.glb") 가 새 평탄 경로
        // ("path:Assets/Arts/Models/book_stack.glb") 와 매칭되도록 lookup table.
        //
        // (MR #154 review #3: SourceAssetPath 가 저장 key 라 단순 이동으로도 사용자 layout 복원
        // 실패하는 회귀 fix. TryFindInventoryEntryByKey 에서 한 번 통과 → catalog 새 key 로
        // 재매칭 → SaveDecorationsUseCase 다음 저장 시 자동 마이그레이션.)
        //
        // 장기적으로는 SourceAssetPath 대신 catalog stable id / Unity GUID 기반 key 로 전환하는 게
        // 정공법 — 그 전까지 alias map 이 호환 layer.
        private static readonly Dictionary<string, string> LegacyInventoryKeyAliases =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "path:Assets/Arts/Models/Props/book_stack.glb",                        "path:Assets/Arts/Models/book_stack.glb" },
                { "path:Assets/Arts/Models/Props/clock_hamham.glb",                      "path:Assets/Arts/Models/clock_hamham.glb" },
                { "path:Assets/Arts/Models/Props/dashboard_natural.glb",                 "path:Assets/Arts/Models/dashboard_natural.glb" },
                { "path:Assets/Arts/Models/Props/doll_hamham.glb",                       "path:Assets/Arts/Models/doll_hamham.glb" },
                { "path:Assets/Arts/Models/Furniture/lamp_floor.glb",                    "path:Assets/Arts/Models/lamp_floor.glb" },
                { "path:Assets/Arts/Models/Furniture/rug_natural.glb",                   "path:Assets/Arts/Models/rug_natural.glb" },
                { "path:Assets/Arts/Models/Furniture/table_natural.glb",                 "path:Assets/Arts/Models/table_natural.glb" },
                { "path:Assets/Arts/Models/Furniture/GoodVibes/GoodVibes_Bed.glb",       "path:Assets/Arts/Models/GoodVibes_Bed.glb" },
                { "path:Assets/Arts/Models/Furniture/GoodVibes/GoodVibes_Desk.glb",      "path:Assets/Arts/Models/GoodVibes_Desk.glb" },
                { "path:Assets/Arts/Models/Furniture/GoodVibes/GoodVibes_Monitor.glb",   "path:Assets/Arts/Models/GoodVibes_Monitor.glb" },
                { "path:Assets/Arts/Models/Furniture/GoodVibes/GoodVibes_NeonSign.glb",  "path:Assets/Arts/Models/GoodVibes_NeonSign.glb" },
                { "path:Assets/Arts/Models/Props/GoodVibes/GoodVibes_Rug.glb",           "path:Assets/Arts/Models/GoodVibes_Rug.glb" },
                { "path:Assets/Arts/Models/Props/GoodVibes/GoodVibes_ToastPillow.glb",   "path:Assets/Arts/Models/GoodVibes_ToastPillow.glb" },
                { "path:Assets/Arts/Models/Window/GoodVibes_Window.glb",                 "path:Assets/Arts/Models/GoodVibes_Window.glb" },
            };

        // 캐시에 키가 있고 GameObject 가 살아있으면 true. 죽은 reference 는 lazy-clean.
        private bool IsInventoryKeyPlaced(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (!placedByInventoryKey.TryGetValue(key, out var existing))
            {
                return false;
            }
            if (existing == null)
            {
                placedByInventoryKey.Remove(key);
                return false;
            }
            return true;
        }

        // [#5 P1] EnterEditMode 시 scene 전체의 DecorationItemMarker 를 훑어 placedByInventoryKey 를
        // 재구성. controller-local cache 만 가지면 다음 케이스에서 슬롯이 재활성화됨:
        //   - 저장 데이터에서 복원된 배치 오브젝트
        //   - 씬에 미리 박혀있는 marker
        //   - controller reload 등으로 dict 만 초기화된 케이스
        // "씬에 1개만 배치" 가 요구사항이므로 placedItemsRoot 하위가 아닌 scene 전체 스캔.
        private void RebuildPlacedByInventoryKeyFromScene()
        {
            placedByInventoryKey.Clear();
            var markers = FindObjectsByType<DecorationItemMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int restored = 0, skippedNoKey = 0, dupes = 0;
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                if (m == null) continue;
                if (string.IsNullOrEmpty(m.InventoryKey))
                {
                    skippedNoKey++;
                    continue;
                }
                if (placedByInventoryKey.ContainsKey(m.InventoryKey))
                {
                    // 같은 key 중복 — 이미 위반 상태지만 여기서 추가 처리하지 않음 (사용자가 삭제로 정리).
                    dupes++;
                    continue;
                }
                placedByInventoryKey[m.InventoryKey] = m.gameObject;
                restored++;
            }
            Debug.Log(
                $"[RoomEdit][Inventory] 진입 시 scene 스캔 — 등록 {restored}, 키 없음 skip {skippedNoKey}, 중복 skip {dupes} / total marker {markers.Length}");
        }

        private static string ResolveSlotInitial(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return "■";
            var trimmed = displayName.Trim();
            return trimmed.Length > 0 ? trimmed.Substring(0, 1).ToUpperInvariant() : "■";
        }

        private void BuildReturnButton(Transform parent)
        {
            returnButtonRoot = CreateRectObject("ReturnButtonRoot", parent);
            var rect = (RectTransform)returnButtonRoot.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            rect.anchoredPosition = new Vector2(-ButtonMargin, -ButtonMargin);

            var button = CreateButton(
                returnButtonRoot.transform, "ReturnButton", "돌아가기",
                CardColor, TextColor, new Vector2(ButtonWidth, ButtonHeight),
                true, BorderColor);
            StretchToParent((RectTransform)button.transform);
            button.onClick.AddListener(ExitEditMode);

            var shadow = button.gameObject.AddComponent<Shadow>();
            shadow.effectColor = WithAlpha(TextColor, 0.18f);
            shadow.effectDistance = new Vector2(0f, -3f);
        }

        private void BuildReadyModal()
        {
            readyModalRoot = CreateRectObject("ReadyModal", canvas.transform);
            var rect = (RectTransform)readyModalRoot.transform;
            StretchToParent(rect);

            var dim = CreateImage(readyModalRoot.transform, "Dim", DimColor);
            StretchToParent(dim.rectTransform);
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(HideReadyModal);

            var card = CreateImage(readyModalRoot.transform, "Card", CardColor);
            card.sprite = roundedRectSprite;
            card.type = Image.Type.Sliced;
            var cardRect = card.rectTransform;
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(560f, 280f);
            cardRect.anchoredPosition = Vector2.zero;
            var cardOutline = card.gameObject.AddComponent<Outline>();
            cardOutline.effectColor = WithAlpha(BorderColor, 0.95f);
            cardOutline.effectDistance = new Vector2(1f, -1f);

            var label = CreateText(card.transform, "Label", "준비 중입니다.", 28f, FontStyles.Bold, TextColor, TextAlignmentOptions.Center);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0.45f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.offsetMin = new Vector2(24f, 0f);
            labelRect.offsetMax = new Vector2(-24f, -16f);

            var confirm = CreateButton(card.transform, "ConfirmButton", "확인",
                PrimaryColor, Color.white, new Vector2(180f, 64f), false, BorderColor);
            var confirmRect = (RectTransform)confirm.transform;
            confirmRect.anchorMin = new Vector2(0.5f, 0f);
            confirmRect.anchorMax = new Vector2(0.5f, 0f);
            confirmRect.pivot = new Vector2(0.5f, 0f);
            confirmRect.anchoredPosition = new Vector2(0f, 32f);
            confirm.onClick.AddListener(HideReadyModal);
        }

        private void BuildThemeApplyModeModal()
        {
            themeApplyModeModalRoot = CreateRectObject("ThemeApplyModeModal", canvas.transform);
            var rect = (RectTransform)themeApplyModeModalRoot.transform;
            StretchToParent(rect);

            var dim = CreateImage(themeApplyModeModalRoot.transform, "Dim", DimColor);
            StretchToParent(dim.rectTransform);
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(HideThemeApplyModeModal);

            var card = CreateImage(themeApplyModeModalRoot.transform, "Card", CardColor);
            card.sprite = roundedRectSprite;
            card.type = Image.Type.Sliced;
            var cardRect = card.rectTransform;
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(700f, 330f);
            cardRect.anchoredPosition = Vector2.zero;
            var cardOutline = card.gameObject.AddComponent<Outline>();
            cardOutline.effectColor = WithAlpha(BorderColor, 0.95f);
            cardOutline.effectDistance = new Vector2(1f, -1f);

            var title = CreateText(card.transform, "Title", "테마 적용 방식", 26f, FontStyles.Bold, TextColor, TextAlignmentOptions.Center);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(32f, -78f);
            titleRect.offsetMax = new Vector2(-32f, -28f);

            var body = CreateText(
                card.transform,
                "Body",
                "전체 초기화는 현재 커스터마이징과 책장 스킨을 기본값으로 정리한 뒤 테마를 적용합니다.\n현재 배치 유지는 배치 구조를 유지한 상태에서 테마만 적용합니다.",
                18f,
                FontStyles.Normal,
                MutedTextColor,
                TextAlignmentOptions.Center);
            var bodyRect = body.rectTransform;
            bodyRect.anchorMin = new Vector2(0f, 0.34f);
            bodyRect.anchorMax = new Vector2(1f, 0.78f);
            bodyRect.pivot = new Vector2(0.5f, 0.5f);
            bodyRect.offsetMin = new Vector2(48f, 0f);
            bodyRect.offsetMax = new Vector2(-48f, 0f);
            body.enableWordWrapping = true;

            var resetButton = CreateButton(card.transform, "ResetButton", "전체 초기화",
                PrimaryColor, Color.white, new Vector2(180f, 58f), false, BorderColor);
            var resetRect = (RectTransform)resetButton.transform;
            resetRect.anchorMin = new Vector2(0.5f, 0f);
            resetRect.anchorMax = new Vector2(0.5f, 0f);
            resetRect.pivot = new Vector2(0.5f, 0f);
            resetRect.anchoredPosition = new Vector2(-210f, 34f);
            resetButton.onClick.AddListener(() => ConfirmThemeApplyMode(ThemeApplyMode.Reset));

            var keepButton = CreateButton(card.transform, "KeepButton", "배치 유지 준비 중",
                SecondaryColor, MutedTextColor, new Vector2(190f, 58f), true, BorderColor);
            var keepRect = (RectTransform)keepButton.transform;
            keepRect.anchorMin = new Vector2(0.5f, 0f);
            keepRect.anchorMax = new Vector2(0.5f, 0f);
            keepRect.pivot = new Vector2(0.5f, 0f);
            keepRect.anchoredPosition = new Vector2(0f, 34f);
            keepButton.interactable = false;

            var cancelButton = CreateButton(card.transform, "CancelButton", "취소",
                CardColor, TextColor, new Vector2(140f, 58f), true, BorderColor);
            var cancelRect = (RectTransform)cancelButton.transform;
            cancelRect.anchorMin = new Vector2(0.5f, 0f);
            cancelRect.anchorMax = new Vector2(0.5f, 0f);
            cancelRect.pivot = new Vector2(0.5f, 0f);
            cancelRect.anchoredPosition = new Vector2(195f, 34f);
            cancelButton.onClick.AddListener(HideThemeApplyModeModal);
        }

        // 커스터마이징 불가 테마(AllowsCustomization=false)가 적용된 상태에서 카테고리를 변경하면
        // 백엔드가 themeReset=true 응답으로 테마를 자동 해제. 사용자에게 그 결과를 미리 알리고
        // 의사결정 기회를 주기 위한 사전 확인 모달.
        private void BuildCustomizationLockedModal()
        {
            customizationLockedModalRoot = CreateRectObject("CustomizationLockedModal", canvas.transform);
            var rect = (RectTransform)customizationLockedModalRoot.transform;
            StretchToParent(rect);

            var dim = CreateImage(customizationLockedModalRoot.transform, "Dim", DimColor);
            StretchToParent(dim.rectTransform);
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(HideCustomizationLockedModal);

            var card = CreateImage(customizationLockedModalRoot.transform, "Card", CardColor);
            card.sprite = roundedRectSprite;
            card.type = Image.Type.Sliced;
            var cardRect = card.rectTransform;
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(660f, 290f);
            cardRect.anchoredPosition = Vector2.zero;
            var cardOutline = card.gameObject.AddComponent<Outline>();
            cardOutline.effectColor = WithAlpha(BorderColor, 0.95f);
            cardOutline.effectDistance = new Vector2(1f, -1f);

            var title = CreateText(card.transform, "Title", "테마 해제 안내", 26f, FontStyles.Bold, TextColor, TextAlignmentOptions.Center);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(32f, -78f);
            titleRect.offsetMax = new Vector2(-32f, -28f);

            customizationLockedBodyText = CreateText(
                card.transform,
                "Body",
                string.Empty,
                18f,
                FontStyles.Normal,
                MutedTextColor,
                TextAlignmentOptions.Center);
            var bodyRect = customizationLockedBodyText.rectTransform;
            bodyRect.anchorMin = new Vector2(0f, 0.32f);
            bodyRect.anchorMax = new Vector2(1f, 0.78f);
            bodyRect.pivot = new Vector2(0.5f, 0.5f);
            bodyRect.offsetMin = new Vector2(48f, 0f);
            bodyRect.offsetMax = new Vector2(-48f, 0f);
            customizationLockedBodyText.enableWordWrapping = true;

            var proceedButton = CreateButton(card.transform, "ProceedButton", "계속",
                PrimaryColor, Color.white, new Vector2(180f, 58f), false, BorderColor);
            var proceedRect = (RectTransform)proceedButton.transform;
            proceedRect.anchorMin = new Vector2(0.5f, 0f);
            proceedRect.anchorMax = new Vector2(0.5f, 0f);
            proceedRect.pivot = new Vector2(0.5f, 0f);
            proceedRect.anchoredPosition = new Vector2(-100f, 34f);
            proceedButton.onClick.AddListener(ConfirmCustomizationLockedProceed);

            var cancelLockedButton = CreateButton(card.transform, "CancelButton", "취소",
                CardColor, TextColor, new Vector2(140f, 58f), true, BorderColor);
            var cancelLockedRect = (RectTransform)cancelLockedButton.transform;
            cancelLockedRect.anchorMin = new Vector2(0.5f, 0f);
            cancelLockedRect.anchorMax = new Vector2(0.5f, 0f);
            cancelLockedRect.pivot = new Vector2(0.5f, 0f);
            cancelLockedRect.anchoredPosition = new Vector2(100f, 34f);
            cancelLockedButton.onClick.AddListener(HideCustomizationLockedModal);
        }

        // 우측 세로 카드 — 헤더 + (배치 확정) + 좌회전/우회전/삭제. 따뜻한 베이지/크림톤 + 둥근 모서리 +
        // 은은한 outline & shadow. VerticalLayoutGroup + ContentSizeFitter 로 confirm 토글 시 카드 높이 자동
        // 재계산 (confirm 숨김 시 헤더 바로 아래에 회전/삭제 버튼이 자연스럽게 정렬). confirmPlacementButtonRoot
        // 는 confirmPlacementButton.gameObject 그 자체를 가리켜 UpdateSelectionPanel 의 SetActive 토글이 그대로
        // 동작 — 두 root SetActive 흐름의 의미를 보존하면서 시각적으로 단일 카드.
        private void BuildSelectionPanel()
        {
            selectionPanelRoot = CreateRectObject("SelectionPanel", canvas.transform);
            var rect = (RectTransform)selectionPanelRoot.transform;
            // 우상단 anchor + -ButtonMargin x-offset = 저장/돌아가기 column 과 동일 우측 정렬.
            // 카드 height 는 ContentSizeFitter 가 자식 합산으로 결정 (sizeDelta.y 초기값은 무의미).
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(SelectionCardWidth, 0f);
            // 저장 버튼 bottom = -ButtonMargin - ButtonHeight - ButtonStackGap - ButtonHeight = -164.
            // 24px gap → -188 (카드 top). pivot y=1 이라 카드 아래로 자라난다.
            rect.anchoredPosition = new Vector2(-ButtonMargin, -188f);

            // CanvasGroup 게이트 — UpdateSelectionPanel 이 매 frame blocksRaycasts/interactable 를
            // shouldShow 로 토글. SetActive 와 별개로 raycast 차단을 명시 제어해 메인씬 회귀 차단.
            selectionPanelCanvasGroup = selectionPanelRoot.AddComponent<CanvasGroup>();
            selectionPanelCanvasGroup.blocksRaycasts = false;
            selectionPanelCanvasGroup.interactable = false;

            // 카드 배경 — 반투명 크림 + 베이지 outline + 부드러운 그림자. LayoutElement.ignoreLayout=true 로
            // VerticalLayoutGroup 가 stretch 한 background 를 자식 슬롯으로 셈하지 않게 가드.
            // raycastTarget=true 유지 — 편집 모드에서 카드 padding/spacing 영역의 클릭이 뒤의 데코로
            // 새지 않도록 background 가 click eat 한다 (CanvasGroup.blocksRaycasts=false 일 땐 자동 무력화).
            var bg = CreateImage(selectionPanelRoot.transform, "Background", WithAlpha(CardColor, 0.97f));
            bg.sprite = roundedRectSprite;
            bg.type = Image.Type.Sliced;
            StretchToParent(bg.rectTransform);
            bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var outline = bg.gameObject.AddComponent<Outline>();
            outline.effectColor = WithAlpha(BorderColor, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);
            var bgShadow = bg.gameObject.AddComponent<Shadow>();
            bgShadow.effectColor = WithAlpha(TextColor, 0.10f);
            bgShadow.effectDistance = new Vector2(0f, -3f);

            var layout = selectionPanelRoot.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(SelectionCardPadding, SelectionCardPadding, SelectionCardPadding, SelectionCardPadding);
            layout.spacing = SelectionCardSpacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var fitter = selectionPanelRoot.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // 헤더 — 인벤토리 카드의 muted 캡션 톤과 동일. 장식용이라 raycastTarget=false (Background 가
            // 카드 영역 클릭을 이미 차단하므로 헤더는 raycast 안 받아도 됨).
            var header = CreateText(selectionPanelRoot.transform, "Header", "오브젝트 조작",
                13f, FontStyles.Bold, MutedTextColor, TextAlignmentOptions.Center);
            header.raycastTarget = false;
            var headerLE = header.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 22f;

            // 헤더와 액션 버튼 사이 1px divider — outline 색을 한 단계 더 흐리게. 장식용 raycastTarget=false.
            var divider = CreateImage(selectionPanelRoot.transform, "Divider", WithAlpha(BorderColor, 0.55f));
            divider.raycastTarget = false;
            var dividerLE = divider.gameObject.AddComponent<LayoutElement>();
            dividerLE.preferredHeight = 1f;
            dividerLE.minHeight = 1f;

            // 배치 확정 — primary green. confirmPlacementButtonRoot 를 GameObject 자체로 두면
            // UpdateSelectionPanel 의 SetActive 토글이 VerticalLayoutGroup 의 child enable 토글이 되어
            // 카드가 자동 reflow.
            confirmPlacementButton = CreateButton(
                selectionPanelRoot.transform, "ConfirmButton", "✓ 배치 확정",
                Hex("#3F8C5C"), Color.white, new Vector2(SelectionButtonInnerWidth, SelectionButtonHeight), false, BorderColor);
            confirmPlacementButtonRoot = confirmPlacementButton.gameObject;
            var confirmLE = confirmPlacementButton.gameObject.AddComponent<LayoutElement>();
            confirmLE.preferredHeight = SelectionButtonHeight;
            confirmPlacementButton.onClick.AddListener(OnConfirmPlacementClicked);

            // 좌/우 회전 — secondary cream 톤. 회전 아이콘(↺/↻) + 한글 라벨 병기.
            // 두 버튼 모두 selectionRotateLeftButton / selectionRotateRightButton 으로 보관해
            // UpdateSelectionPanel 의 AllowRotation 게이트가 그대로 동시 토글.
            var rotateLeftButton = CreateButton(selectionPanelRoot.transform, "RotateLeftButton", "↺ 좌회전",
                SecondaryColor, TextColor, new Vector2(SelectionButtonInnerWidth, SelectionButtonHeight), true, BorderColor);
            rotateLeftButton.gameObject.AddComponent<LayoutElement>().preferredHeight = SelectionButtonHeight;
            rotateLeftButton.onClick.AddListener(RotateSelectedCounterClockwise);
            selectionRotateLeftButton = rotateLeftButton;

            var rotateRightButton = CreateButton(selectionPanelRoot.transform, "RotateRightButton", "↻ 우회전",
                SecondaryColor, TextColor, new Vector2(SelectionButtonInnerWidth, SelectionButtonHeight), true, BorderColor);
            rotateRightButton.gameObject.AddComponent<LayoutElement>().preferredHeight = SelectionButtonHeight;
            rotateRightButton.onClick.AddListener(RotateSelectedClockwise);
            selectionRotateRightButton = rotateRightButton;

            // 삭제 — danger red. 다른 버튼과 라운딩/높이 동일, 색상으로만 위험 액션 신호.
            var deleteButton = CreateButton(selectionPanelRoot.transform, "DeleteButton", "삭제",
                Hex("#C14E4E"), Color.white, new Vector2(SelectionButtonInnerWidth, SelectionButtonHeight), false, BorderColor);
            deleteButton.gameObject.AddComponent<LayoutElement>().preferredHeight = SelectionButtonHeight;
            deleteButton.onClick.AddListener(DeleteSelectedObject);

            // 초기 상태 — 카드 자체를 비활성. 우상단 anchor 로 옮긴 이후 카드가 책장/대시보드 raycast
            // 영역과 겹쳐서, BuildSelectionPanel ~ 첫 Update 사이 1프레임 race 만으로도 메인씬 클릭이
            // BookshelfExperienceManager.IsPointerOverUi 에서 blocking hit 으로 잡혔다. Update 의
            // UpdateSelectionPanel 이 진실의 원천이지만, race window 를 닫기 위해 시작은 inactive 로.
            selectionPanelRoot.SetActive(false);
            // pending placement 없을 땐 confirm 숨김. UpdateSelectionPanel 이 매 frame 재평가.
            confirmPlacementButtonRoot.SetActive(false);
        }

        // Pending placement helpers ------------------------------------------------

        // 런타임-생성 transparent 템플릿. URP/Unlit 우선, fallback 으로 built-in Unlit/Color
        // 또는 Sprites/Default (어떤 파이프라인에서도 transparent 지원). 자식 Renderer 마다
        // 이 템플릿을 base 로 new Material(template) 인스턴스를 만들어 r.sharedMaterials 에
        // 채운다 — 같은 템플릿을 모든 renderer 가 공유하면 wall-validity 빨간 tint 가 한 번
        // 발사될 때 다른 pending 항목까지 번지므로 분리.
        private Material EnsurePreviewMaterialTemplate()
        {
            if (previewMaterialTemplate != null) return previewMaterialTemplate;
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Sprites/Default");
            var m = new Material(shader) { name = "RoomEditPreviewTemplate" };
            // URP/Unlit transparent 활성화 — Surface=Transparent + alpha blend.
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = 3000;
            // 옅은 청회색 ghost — 알파 0.45. 너무 투명하면 식별 안 되고 너무 진하면 일반과 구분 약함.
            var ghostColor = new Color(0.55f, 0.78f, 1f, 0.45f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", ghostColor);
            if (m.HasProperty("_Color")) m.SetColor("_Color", ghostColor);
            previewMaterialTemplate = m;
            return previewMaterialTemplate;
        }

        // PlaceInventoryEntry 가 새로 spawn 한 GameObject 를 preview 상태로 진입시킨다.
        //   1. 자식 포함 모든 Renderer 의 sharedMaterials 배열을 그대로 백업.
        //   2. 동일 길이의 preview 배열로 교체 — 각 submesh slot 은 template 의 인스턴스 클론.
        //      (자식 Renderer 마다 instance 분리 → wall validity tint 가 cross-leak 안 됨.)
        // sharedMaterials setter 는 인자 배열 요소를 그대로 reference 저장하므로 인스턴스를
        // 새로 만든 뒤 그 인스턴스를 채워야 sharedMaterial 직접 수정이 아닌 인스턴스 기반 처리.
        private void BeginPendingPlacement(GameObject target)
        {
            if (target == null) return;
            if (pendingPlacements.Contains(target)) return;
            pendingPlacements.Add(target);
            var template = EnsurePreviewMaterialTemplate();

            var renderers = target.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;

                // 원본 배열 clone — 이후 누가 r.sharedMaterials 를 갈아끼워도 우리 백업이 깨지지 않도록.
                var orig = r.sharedMaterials;
                if (orig == null) continue;
                var bak = new Material[orig.Length];
                for (int j = 0; j < orig.Length; j++) bak[j] = orig[j];
                pendingMaterialBackups[r] = bak;

                // Renderer 별로 instance 1개를 만들어 모든 submesh 에 배정. 이러면 wall validity 가
                // r.material._BaseColor 를 빨갛게 칠해도 다른 Renderer / 다른 pending 에 영향 X.
                var instance = new Material(template) { name = template.name + "_Instance" };
                var preview = new Material[orig.Length];
                for (int j = 0; j < orig.Length; j++) preview[j] = instance;
                r.sharedMaterials = preview;
            }
            Debug.Log($"[RoomEdit][Preview] BeginPending '{target.name}' — renderers={renderers.Length}");
        }

        // 배치 확정 버튼 핸들러 — 현재 위치가 valid 한지 확인하고 preview 상태 해제.
        // invalid 면 토스트만 띄우고 상태 유지 (사용자가 위치를 옮겨 다시 누르면 됨).
        private void OnConfirmPlacementClicked()
        {
            if (!IsEditModeActive || selectedObject == null) return;
            if (!pendingPlacements.Contains(selectedObject))
            {
                // 이미 확정된 항목 — 버튼이 보이고 있는 게 이상한 상황이지만 방어.
                return;
            }

            // MR #133 — 바닥/벽 양쪽 동일 path 로 검증. UpdateSelectionPanel 이 disabled 처리하므로
            // 정상 경로에선 여기까지 invalid 가 도달하지 않지만, 키보드/외부 트리거 방어로 재확인.
            if (!EvaluateConfirmEnabled(selectedObject))
            {
                Debug.LogWarning($"[EditPlacement] confirm rejected object='{selectedObject.name}' reason='InvalidPlacement'");
                ShowSaveStatusToast("배치 불가 — 다른 오브젝트와 겹칩니다", isError: true, durationSec: 2f);
                return;
            }

            ConfirmPendingPlacement(selectedObject);
        }

        // preview 상태 해제 — 원본 sharedMaterials 복원 + set 에서 제거. 이후로는 일반 항목과
        // 동일하게 저장/롤백 흐름에 합류한다. 현재 선택 중인 항목이면 SelectObject 의 gold tint
        // 를 다시 적용하기 위해 deselect → select 사이클을 돈다 (BeginPending 시점에 gold tint 를
        // skip 했으므로 selectedRendererTints 가 비어있어 deselect 는 no-op).
        private void ConfirmPendingPlacement(GameObject target)
        {
            if (target == null || !pendingPlacements.Contains(target)) return;

            bool wasSelected = (selectedObject == target);
            if (wasSelected) DeselectCurrentObject();

            RestorePendingMaterials(target);
            pendingPlacements.Remove(target);

            if (wasSelected) SelectObject(target);
            Debug.Log($"[RoomEdit][Preview] Confirmed '{target.name}' — pending 해제 + 원본 머터리얼 복원.");
        }

        // Renderer.sharedMaterials 에 백업 배열을 그대로 다시 set. 우리가 만든 instance 머터리얼은
        // 참조가 끊어져 Unity GC 가 정리. 자식 포함 모든 Renderer 처리 — BeginPendingPlacement 와 대칭.
        private void RestorePendingMaterials(GameObject target)
        {
            if (target == null) return;
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                if (pendingMaterialBackups.TryGetValue(r, out var bak))
                {
                    r.sharedMaterials = bak;
                    pendingMaterialBackups.Remove(r);
                }
            }
        }

        // GameObject 가 destroy 되거나 (Delete / Rollback) 편집 모드를 벗어날 때, 백업/세트를 정리.
        // material 복원은 이미 destroy 직전이라 의미 없으므로 backup dictionary 의 stale entry
        // 만 청소한다. set 에서도 제거.
        private void CleanupPendingState(GameObject target)
        {
            if (target == null) return;
            pendingPlacements.Remove(target);
            // backup dictionary 에서 destroy 대상의 Renderer 항목만 제거.
            // ToList 미사용 — 큰 dict 가 아니라 in-place 정리.
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) pendingMaterialBackups.Remove(renderers[i]);
            }
        }

        // 모든 pending 항목 정리 — Rollback / ExitEditMode 후 호출. 확정된 항목은 이미
        // RestorePendingMaterials 가 자기 backup entry 를 제거했고, pending 인 채로 destroy 된
        // 항목의 backup entry 는 어차피 stale 이므로 dictionary 전체를 clear 한다.
        private void ClearAllPendingState()
        {
            pendingPlacements.Clear();
            pendingMaterialBackups.Clear();
        }

        // Update / visibility ------------------------------------------------------

        private void Update()
        {
            if (!isInitialized)
            {
                return;
            }

            DiagnoseEditUiBlockingMainSceneIfClicked();

            UpdateButtonVisibility();
            UpdateSelectionPanel();
            UpdateSaveStatusToast();

            if (IsEditModeActive)
            {
                // 미저장 모달이 떠 있는 동안에는 카메라 orbit/객체 선택·드래그 입력을 모두 차단.
                // 모달 Dim 이 raycast 를 가로채긴 하지만, 키보드(A/D/W/S) orbit 입력은 EventSystem
                // 우회로 들어오므로 별도 가드 필요.
                if (!isUnsavedChangesModalOpen)
                {
                    HandleEditCameraInput();
                    HandleEditModeInput();
                }
            }
            else if (IsDecorationPopupOpen)
            {
                HandleDecorationPopupOutsideClick();
            }

            UpdatePlacementFootprint();
        }

        // 매 frame footprint UI visible/위치/색 갱신 (MR #134).
        // 표시 조건: 편집 모드 활성 + 모달 닫힘 + 선택된 floor 오브젝트가 (pending OR drag 중) 일 때.
        // 그 외엔 hide. 위치는 ComputeRendererBounds 의 world AABB 기준, 색은 EvaluateFloorPlacementValid
        // 의 결과(기존 검증 로직 그대로 재사용). 매 frame 호출되지만 show/hide/색상 변화 시점에만 로그.
        private void UpdatePlacementFootprint()
        {
            if (placementFootprint == null) return;

            if (!IsEditModeActive || isUnsavedChangesModalOpen || selectedObject == null)
            {
                placementFootprint.Hide();
                return;
            }

            bool isDragging = draggingObject == selectedObject;
            bool isPending = pendingPlacements.Contains(selectedObject);
            if (!isDragging && !isPending)
            {
                placementFootprint.Hide();
                return;
            }

            // wall deco 는 바닥에 footprint 가 의미 없음 — 벽 표면에서 별도 visual feedback 이
            // ApplyWallDecoValidityFeedback 으로 동작 중. floor 항목만 footprint UI 사용.
            if (selectedObject.GetComponent<WallDecoMarker>() != null)
            {
                placementFootprint.Hide();
                return;
            }

            // 회귀 fix — 시각 footprint 와 validation OverlapBox 가 동일 source 사용. 이전엔
            // ComputeRendererBounds 가 책상 위 모니터/인형 머리/램프 갓 같은 상부 mesh 까지
            // XZ AABB 에 포함시켜 preview 가 실제 floor footprint 보다 *작아 보이는데* validation
            // 은 *큰 영역* 으로 검사 → 사용자가 "공간 충분해 보이는데 배치 불가" 회귀.
            var bounds = ComputeFloorFootprintBounds(selectedObject);
            if (!bounds.HasValue)
            {
                placementFootprint.Hide();
                return;
            }

            // 드래그 중엔 raw 타깃 위치 기준으로 검증 — clamp/resolve 로 실제 transform 은 valid 로
            // 잡혀있어도 사용자가 벽/책장 쪽으로 밀고 있으면 빨강으로 보여줘야 한다. 드래그 아닌
            // pending(spawn 직후 정지 상태) 은 transform.position 검증.
            var probe = isDragging ? lastFloorDragRawCandidate : selectedObject.transform.position;
            bool isValid = EvaluateFloorPlacementValid(selectedObject, probe, bounds, out var reason);

#if UNITY_EDITOR
            // 진단 — preview 와 validation 이 같은 footprint 를 보고 있는지 frame-by-frame 검증.
            // size 가 다르면 즉시 식별 가능. 회귀 발견 후 #if UNITY_EDITOR 가드 안에 두므로 release 빌드
            // 에는 영향 없음.
            Debug.Log(
                $"[EditFootprint] obj='{selectedObject.name}' previewCenterXZ=({bounds.Value.center.x:0.##},{bounds.Value.center.z:0.##}) " +
                $"previewSizeXZ=({bounds.Value.size.x:0.##},{bounds.Value.size.z:0.##}) probe=({probe.x:0.##},{probe.z:0.##}) " +
                $"valid={isValid} reason='{reason ?? "None"}'");
#endif

            placementFootprint.Show(gameObject.scene, bounds.Value, isValid, reason, selectedObject.name);
        }

        // 편집 카메라 키/스크롤 입력 — A/D 회전, W/S 줌, 마우스 스크롤 줌. 텍스트 입력 포커스 중이거나
        // 오브젝트 드래그 중에는 skip 해서 의도치 않은 카메라 이동 방지. orbit 상태가 변하면
        // cameraPresetTargetActive=true 로 lerp 활성, classification 변경 시 wall fade 갱신.
        private void HandleEditCameraInput()
        {
            if (IsTextInputFocused()) return;
            if (draggingObject != null) return;

            float dt = Time.unscaledDeltaTime;
            bool changed = false;

            // 키보드 카메라 입력 — **정확히 1개 키만 눌렸을 때만 동작**. W/S/A/D 중 2개 이상
            // 동시에 눌리면 줌·회전 모두 차단하고, 진행 중인 lerp 잔상까지 즉시 snap 으로 제거.
            //
            //   pressedCount == 1 → 해당 동작 (W=줌인, S=줌아웃, A=좌회전, D=우회전)
            //   pressedCount == 0 → 입력 없음 — 이전 단일 키로 마감된 orbit 값으로 lerp 자연 settle 진행
            //   pressedCount >= 2 → orbit yaw/pitch/distance delta 0 + HaltCameraInputResidual 로
            //                       카메라 transform 을 현재 orbit target 으로 즉시 snap.
            //                       이렇게 하면 LateUpdate 의 exponential smoothing 이 zero-gap 이라
            //                       프레임당 추가 이동량 0 → "관성 잔상" 없음.
            //
            // 마우스 휠 줌은 별도 경로(아래 Mouse.scroll) 라 멀티키 게이팅과 무관 — 정상 동작.
            var kb = Keyboard.current;
            bool wPressed = kb != null && kb.wKey.isPressed;
            bool sPressed = kb != null && kb.sKey.isPressed;
            bool aPressed = kb != null && kb.aKey.isPressed;
            bool dPressed = kb != null && kb.dKey.isPressed;
            int pressedCount =
                (wPressed ? 1 : 0) + (sPressed ? 1 : 0) +
                (aPressed ? 1 : 0) + (dPressed ? 1 : 0);

            float yawInput = 0f;
            float zoomInput = 0f;

            if (pressedCount == 1)
            {
                if (wPressed)      zoomInput = -1f;
                else if (sPressed) zoomInput = +1f;
                else if (aPressed) yawInput  = -1f;
                else if (dPressed) yawInput  = +1f;
            }
            else if (pressedCount >= 2)
            {
                // orbit 상태(yaw/pitch/distance) 는 직전 단일 키 입력으로 마감된 값 보존 — 키를
                // 떼면 그 자리에서 자연스럽게 멈춤. 카메라 transform 만 즉시 target 으로 snap 해서
                // LateUpdate lerp 의 settling drift 를 끊는다.
                HaltCameraInputResidual();
            }

            if (Mathf.Abs(yawInput) > 0.001f)
            {
                orbitYaw += yawInput * OrbitYawSpeed * dt;
                changed = true;
            }
            if (Mathf.Abs(zoomInput) > 0.001f)
            {
                orbitDistance += zoomInput * editZoomKeySpeed * dt;
                changed = true;
            }

            // 마우스 스크롤 — distance 단위 점프. y>0 = 스크롤 업 = 확대.
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scrollY = mouse.scroll.ReadValue().y;
                // Unity 의 raw scroll 값은 OS 마다 스케일이 다름. 절댓값 ≥120(Win) 또는 ≥1(macOS line-step) 보정.
                if (Mathf.Abs(scrollY) > 0.01f)
                {
                    float normalized = Mathf.Clamp(scrollY / 120f, -1f, 1f);
                    if (Mathf.Abs(scrollY) < 1.5f && Mathf.Abs(scrollY) > 0.01f)
                    {
                        // line-step 환경 fallback.
                        normalized = Mathf.Sign(scrollY);
                    }
                    orbitDistance -= normalized * editZoomScrollSensitivity;
                    changed = true;
                }
            }

            if (!changed) return;

            // Default 모드에서 첫 orbit 입력 — 현재 saved pose 기준으로 yaw/pitch/distance 를
            // 역산해서 점프 없이 이어가도록. 이게 없으면 Default(메인뷰 pose) → orbit 기본값(25°,18°,4.2m)
            // 으로 갑자기 점프하는 것처럼 lerp 됨.
            if (isInDefaultView)
            {
                DeriveOrbitFromSavedPose();
                // 위에서 누적된 입력 delta 도 다시 적용 — DeriveOrbitFromSavedPose 가 yaw 등을 덮어썼으므로.
                if (Mathf.Abs(yawInput) > 0.001f) orbitYaw += yawInput * OrbitYawSpeed * dt;
                if (Mathf.Abs(zoomInput) > 0.001f) orbitDistance += zoomInput * editZoomKeySpeed * dt;
            }

            // 클램프 + 변경 후처리. orbitPitch 먼저 clamp → ClassifyByOrbit (pitch 65° 임계값 사용)
            // → 시점별 (min,max) 로 distance clamp. 입력 종류(W/S/스크롤) 무관 같은 clamp 기준.
            orbitPitch = Mathf.Clamp(orbitPitch, OrbitMinPitch, OrbitMaxPitch);
            isInDefaultView = false;
            cameraPresetTargetActive = true;

            // wall fade 카테고리 갱신 — quadrant 가 바뀐 경우만 ApplyWallVisibilityForPreset 호출.
            currentCameraPreset = ClassifyByOrbit();
            var (zMin, zMax) = GetOrbitDistanceRangeForPreset(currentCameraPreset);
            orbitDistance = Mathf.Clamp(orbitDistance, zMin, zMax);
            ApplyWallVisibilityForCurrentView("OrbitInput");

            if (Mathf.Abs(yawInput) > 0.001f)
            {
                Debug.Log($"[RoomEdit][Camera] orbit 회전 yaw={orbitYaw:0.#}° pitch={orbitPitch:0.#}° preset={currentCameraPreset}");
            }
            if (Mathf.Abs(zoomInput) > 0.001f || (mouse != null && Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f))
            {
                Debug.Log($"[RoomEdit][Camera] orbit 줌 distance={orbitDistance:0.##}m");
            }
        }

        // 키보드 W/S/A/D 중 2개 이상 동시 입력 감지 시 호출 — 진행 중인 LateUpdate 의 exponential
        // smoothing lerp 잔상을 끊는다. 카메라 transform 을 현재 orbit target 으로 즉시 snap 하면
        // 다음 프레임 LateUpdate 의 Vector3.Lerp(current, target, t) 가 zero-gap 이 되어 추가 움직임 0.
        //
        // **orbit 상태(yaw/pitch/distance) 는 건드리지 않는다** — 직전 단일 키 입력으로 마감된
        // 값 그대로 보존해야, 사용자가 키를 떼고 다시 단일 키 누르면 그 자리에서 자연스럽게 이어감.
        // Default 모드에선 orbit 미파생 상태이므로 savedCameraPose 로 snap.
        private void HaltCameraInputResidual()
        {
            if (editModeCamera == null) return;

            Vector3 targetPos;
            Quaternion targetRot;
            if (isInDefaultView && savedCameraPoseValid)
            {
                targetPos = savedCameraPosition;
                targetRot = savedCameraRotation;
            }
            else
            {
                ComputeOrbitPose(out targetPos, out targetRot);
            }

            editModeCamera.transform.SetPositionAndRotation(targetPos, targetRot);
            // LateUpdate 가 다음 프레임에 다시 같은 target 을 계산해 lerp 하지만 gap 이 0 이라 noop.
            // cameraPresetTargetPosition/Rotation 도 동기화 — LateUpdate 에서 재계산되지만 명시적으로
            // 맞춰 두면 외부에서 이 필드를 읽을 때 stale 값이 보이지 않음.
            cameraPresetTargetPosition = targetPos;
            cameraPresetTargetRotation = targetRot;

            // wall fade catch-up — multi-key 진입이 edit mode 의 첫 입력이라 ClassifyByOrbit 결과가
            // currentCameraPreset 에 아직 반영 안 됐을 가능성. preset 분류가 바뀐 경우에만 갱신
            // (idempotent — 같으면 ApplyWallVisibilityForCurrentView 의 change-detection 이 skip).
            // (MR #147 review #2: HaltCameraInputResidual 에서 wall fade stale 가능성 fix.)
            var nextPreset = isInDefaultView ? CameraPreset.Default : ClassifyByOrbit();
            if (nextPreset != currentCameraPreset)
            {
                currentCameraPreset = nextPreset;
                ApplyWallVisibilityForCurrentView("HaltResidual");
            }
        }

        // EventSystem 의 currentSelectedGameObject 가 InputField/TMP_InputField 컴포넌트를 가지면
        // 사용자가 텍스트 입력 중이라고 판단 — W/S/A/D 카메라 입력 무시.
        private static bool IsTextInputFocused()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var go = es.currentSelectedGameObject;
            if (go == null) return false;
            // TMP_InputField 와 legacy InputField 둘 다 검사.
            if (go.GetComponent<TMP_InputField>() != null) return true;
            if (go.GetComponent<UnityEngine.UI.InputField>() != null) return true;
            return false;
        }

        private void UpdateButtonVisibility()
        {
            var experienceManager = ResolveExperienceManager();
            var inMainScene = experienceManager != null &&
                              experienceManager.CurrentFocusArea == ExperienceFocusArea.QuarterView;
            var step3Active = experienceManager != null && experienceManager.IsOnboardingStep3Active;
            var canShowDecorationButton = inMainScene && !step3Active && !IsEditModeActive;

            if (decorationButtonRoot != null && decorationButtonRoot.activeSelf != canShowDecorationButton)
            {
                decorationButtonRoot.SetActive(canShowDecorationButton);
            }
            if (!canShowDecorationButton && IsDecorationPopupOpen)
            {
                CloseDecorationPopupInternal();
            }

            if (editOverlayRoot != null && editOverlayRoot.activeSelf != IsEditModeActive)
            {
                editOverlayRoot.SetActive(IsEditModeActive);
            }
            // CanvasGroup 게이트 — SetActive 와 별개로 raycast 차단을 명시 제어. 메인씬에서 어떤
            // race 도 편집 UI 가 책장/대시보드 클릭을 가로채지 못하게 하드 가드.
            if (editOverlayCanvasGroup != null)
            {
                editOverlayCanvasGroup.blocksRaycasts = IsEditModeActive;
                editOverlayCanvasGroup.interactable = IsEditModeActive;
            }

            // Force-exit edit mode if the player ends up in a focus mode some other way.
            if (IsEditModeActive && experienceManager != null && !inMainScene)
            {
                ExitEditModeInternal();
            }
        }

        // 메인씬(IsEditModeActive=false) 에서 좌클릭 발생 시점에만 RaycastAll 을 한 번 돌려,
        // RoomEditModeController 가 만든 편집 전용 UI(editOverlayRoot / selectionPanelRoot 하위)
        // 가 책장/대시보드 클릭을 막고 있는지 검증한다. hit 이 있으면 [EditUiHit][WARN] 으로 어떤
        // GameObject/canvas/raycastTarget 이 차단 원인인지 출력 — 회귀 발생 시 1차 진단 자료.
        // 매 frame 이 아니라 click frame 한정이라 비용 거의 없음.
        private void DiagnoseEditUiBlockingMainSceneIfClicked()
        {
            if (IsEditModeActive) return;
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            var es = EventSystem.current;
            if (es == null) return;

            var pointer = Mouse.current.position.ReadValue();
            var pointerEvent = new PointerEventData(es) { position = pointer };
            var hits = new List<RaycastResult>();
            es.RaycastAll(pointerEvent, hits);

            bool headerLogged = false;
            for (int i = 0; i < hits.Count; i++)
            {
                var go = hits[i].gameObject;
                if (go == null) continue;
                if (!IsUnderEditOnlyRoot(go)) continue;

                if (!headerLogged)
                {
                    Debug.LogWarning($"[EditUiHit][WARN] edit UI is blocking main scene while edit mode is inactive — pointer={pointer} hits={hits.Count}");
                    headerLogged = true;
                }
                var canvas = go.GetComponentInParent<Canvas>();
                var canvasName = canvas != null ? canvas.name : "<none>";
                var renderMode = canvas != null ? canvas.renderMode.ToString() : "<n/a>";
                var graphic = go.GetComponent<Graphic>();
                var raycastTarget = graphic != null ? graphic.raycastTarget.ToString() : "<noGraphic>";
                var canvasGroup = go.GetComponentInParent<CanvasGroup>();
                var blocksRaycasts = canvasGroup != null ? canvasGroup.blocksRaycasts.ToString() : "<noGroup>";
                Debug.LogWarning($"[EditUiHit][WARN] hit='{go.name}' canvas='{canvasName}' renderMode={renderMode} raycastTarget={raycastTarget} blocksRaycasts={blocksRaycasts}");
            }
        }

        // editOverlayRoot 또는 selectionPanelRoot 하위인지 판정. decorationButtonRoot/popup 은
        // 메인씬에서도 의도적으로 활성인 정상 UI 라 진단 대상에서 제외.
        private bool IsUnderEditOnlyRoot(GameObject go)
        {
            var t = go.transform;
            while (t != null)
            {
                var current = t.gameObject;
                if (current == editOverlayRoot || current == selectionPanelRoot)
                {
                    return true;
                }
                t = t.parent;
            }
            return false;
        }

        private void UpdateSelectionPanel()
        {
            // 미저장 모달이 떠 있는 동안에는 selection action UI(좌/우 회전·삭제) 를
            // 숨겨서 모달과 시각적으로 겹치지 않게 한다. 취소 시 selectedObject 가 그대로
            // 살아있으면 모달 닫힌 다음 Update 에서 자동 복귀.
            var shouldShow = IsEditModeActive && selectedObject != null && !isUnsavedChangesModalOpen;
            if (selectionPanelRoot != null && selectionPanelRoot.activeSelf != shouldShow)
            {
                selectionPanelRoot.SetActive(shouldShow);
            }
            // CanvasGroup 게이트 — shouldShow=false 면 blocksRaycasts/interactable 도 false. 카드의
            // Background/Header/Divider Image 가 raycast 를 절대 못 막게 보장 (메인씬 회귀 차단).
            if (selectionPanelCanvasGroup != null)
            {
                selectionPanelCanvasGroup.blocksRaycasts = shouldShow;
                selectionPanelCanvasGroup.interactable = shouldShow;
            }

            // 회전 비활성 entry — wall deco marker.AllowRotation == false 면 좌/우 회전 버튼 모두 disabled.
            // 좌/우 분리 (이번 브랜치) + AllowRotation 게이트 (origin/dev) 양립.
            bool allowRotation = true;
            if (selectedObject != null)
            {
                var marker = selectedObject.GetComponent<WallDecoMarker>();
                if (marker != null && !marker.AllowRotation) allowRotation = false;
            }
            // state-transition 시점 한 번만 [EditRotate] 게이트 진단 — 매 frame 도배 방지.
            if (selectionRotateLeftButton != null && selectionRotateLeftButton.interactable != allowRotation)
            {
                selectionRotateLeftButton.interactable = allowRotation;
                if (selectedObject != null)
                {
                    Debug.Log(
                        $"[EditRotate] gate update obj={selectedObject.name} allowRotation={allowRotation} " +
                        $"hasWallMarker={selectedObject.GetComponent<WallDecoMarker>() != null}");
                }
            }
            if (selectionRotateRightButton != null && selectionRotateRightButton.interactable != allowRotation)
            {
                selectionRotateRightButton.interactable = allowRotation;
            }

            // MR #122 — 배치 확정 버튼은 pending(=preview ghost) 항목이 선택돼 있을 때만 노출.
            // 미저장 모달이 열려있을 땐 selectionPanel 과 동일하게 숨김.
            var shouldShowConfirm = shouldShow && selectedObject != null && pendingPlacements.Contains(selectedObject);
            if (confirmPlacementButtonRoot != null && confirmPlacementButtonRoot.activeSelf != shouldShowConfirm)
            {
                confirmPlacementButtonRoot.SetActive(shouldShowConfirm);
            }

            // MR #133 — 현재 위치가 invalid 면 확정 버튼 disabled. 매 frame 평가하지만
            // EvaluateConfirmEnabled 는 ComputeRendererBounds + Physics.OverlapBox 1회 (drag tick
            // 과 동일 비용) 라 부담 없음.
            if (shouldShowConfirm && confirmPlacementButton != null)
            {
                bool canConfirm = EvaluateConfirmEnabled(selectedObject);
                if (confirmPlacementButton.interactable != canConfirm)
                {
                    confirmPlacementButton.interactable = canConfirm;
                }
            }
        }

        // 확정 버튼 활성화 판단 — 현재 transform 위치가 valid 한지 검사.
        // 바닥 항목: ClampToFloorBounds + IsSpawnPositionBlockedAt.
        // 벽 항목: WallObstructionRegistry.IsRectFreeOnWall.
        // 양쪽 모두 기존 검증 로직을 그대로 재사용.
        private bool EvaluateConfirmEnabled(GameObject go)
        {
            if (go == null) return false;

            var wallMarker = go.GetComponent<WallDecoMarker>();
            if (wallMarker != null)
            {
                // wall deco 는 벽 면적 기준 — 전체 mesh AABB 가 wall registry rect 에 들어가는지 검사.
                var rendererBounds = ComputeRendererBounds(go);
                if (!rendererBounds.HasValue) return true;
                var rect = ProjectBoundsToWallRect(rendererBounds.Value, wallMarker.CurrentWall);
                var registry = EnsureWallObstructionRegistry();
                return registry == null || registry.IsRectFreeOnWall(wallMarker.CurrentWall, rect);
            }

            // 회귀 fix — floor 항목은 ComputeFloorFootprintBounds (preview 와 동일 source) 사용.
            // 이전엔 ComputeRendererBounds 가 상부 mesh 까지 포함해 confirm 검사가 preview 보다
            // 큰 영역을 검사 → 시각적으로 충분해 보이는데 confirm 비활성 회귀.
            var bounds = ComputeFloorFootprintBounds(go);
            if (!bounds.HasValue) return true;
            return EvaluateFloorPlacementValid(go, go.transform.position, bounds, out _);
        }

        // 토스트 자동 숨김 — saveStatusToastHideTime 까지만 노출.
        private void UpdateSaveStatusToast()
        {
            if (saveStatusToastRoot == null || !saveStatusToastRoot.activeSelf) return;
            if (Time.unscaledTime >= saveStatusToastHideTime)
            {
                saveStatusToastRoot.SetActive(false);
            }
        }

        // Decoration button & popup ------------------------------------------------

        private void OnDecorationButtonClicked()
        {
            if (IsDecorationPopupOpen)
            {
                CloseDecorationPopupInternal();
            }
            else
            {
                OpenDecorationPopupInternal();
            }
        }

        private void OpenDecorationPopupInternal()
        {
            IsDecorationPopupOpen = true;
            if (decorationPopupRoot != null)
            {
                decorationPopupRoot.SetActive(true);
                decorationPopupRoot.transform.SetAsLastSibling();
            }
        }

        private void CloseDecorationPopupInternal()
        {
            IsDecorationPopupOpen = false;
            if (decorationPopupRoot != null)
            {
                decorationPopupRoot.SetActive(false);
            }
        }

        private void HandleDecorationPopupOutsideClick()
        {
            if (!IsLeftMouseDownThisFrame())
            {
                return;
            }

            var pointerEvent = new PointerEventData(EventSystem.current)
            {
                position = ReadPointerPosition(),
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerEvent, hits);

            for (var i = 0; i < hits.Count; i++)
            {
                var hit = hits[i].gameObject;
                if (hit == null) continue;
                if (IsDescendantOf(hit.transform, decorationPopupRoot != null ? decorationPopupRoot.transform : null) ||
                    IsDescendantOf(hit.transform, decorationButtonRoot != null ? decorationButtonRoot.transform : null))
                {
                    return;
                }
            }
            CloseDecorationPopupInternal();
        }

        private void OnEditMenuItemClicked()
        {
            CloseDecorationPopupInternal();
            EnterEditMode();
        }

        // Edit mode entry / exit ---------------------------------------------------

        private void EnterEditMode()
        {
            if (IsEditModeActive)
            {
                return;
            }

            var experienceManager = ResolveExperienceManager();
            if (experienceManager != null && experienceManager.CurrentFocusArea != ExperienceFocusArea.QuarterView)
            {
                return;
            }

            IsEditModeActive = true;

            // Edit 모드 진입 시 서버 Space 상태 비동기 동기화 — IsCategoryEntryCurrent /
            // OptimisticallyUpdateServerSpace의 single source of truth(`lastServerSpace`)
            // 가 null인 채로 첫 PATCH 받으면 optimistic 갱신이 silent skip → single
            // source of truth 의도 무력화. 진입 시점에 한 번 GET /spaces/me 호출해
            // 캐시 채움. 응답 도착 전까지 lastAppliedEntries fallback이 동작.
            ReloadRoomState();

            // Order matters: canonical-scale existing decorations FIRST so
            // their renderer bounds reflect the final size, THEN fit blocker
            // BoxColliders against those bounds, THEN compute the playable
            // area. The grid display is invalidated so the next ShowGrid
            // rebuilds it against the freshly recomputed bounds.
            NormalizeExistingDecorationScales();
            EnsureBlockerColliders();
            EnsureDecorationRootColliders();
            RefreshPlayableBounds();
            if (gridDisplayRoot != null)
            {
                Destroy(gridDisplayRoot);
                gridDisplayRoot = null;
            }

            if (editOverlayRoot != null)
            {
                editOverlayRoot.SetActive(true);
            }
            ApplyToolbarSelectionVisuals();

            // Force the camera to the canonical MainView pose (instant snap, no
            // animation) BEFORE shrinking its viewport — guarantees the edit
            // session starts from the same composition the player sees on the
            // normal main view, regardless of whether they were in MainView or
            // QuarterView a moment ago. Position / rotation / FOV are all reset.
            if (experienceManager != null)
            {
                experienceManager.SnapToMainView();
            }
            BeginCameraViewportEdit();

            // [#5 P1] 진입 시 scene 전체에서 DecorationItemMarker 를 스캔해 placedByInventoryKey 를
            // 재구성. controller-local cache 만 의존하면 (a) 저장에서 복원된 오브젝트, (b) scene
            // 에 미리 배치된 marker, (c) 다른 코드가 만든 marker 가 누락되어 슬롯이 다시 활성
            // 상태로 보이는 경우 발생. "씬에 1개만" 이라는 요구사항을 만족시키려면 scene 전체 스캔.
            RebuildPlacedByInventoryKeyFromScene();

            // 진입 시 dirty 초기화 (이전 세션의 dirty 가 남아있으면 즉시 모달이 뜨는 회귀 방지).
            isDirty = false;
            // MR #128: 저장된 layout 은 BookshelfSceneInstaller.InstallIntoActiveScene 끝점에서
            // 이미 1회 load 됐다. 같은 데이터를 EnterEditMode 마다 다시 읽으면 (a) marker transform
            // 이 두 번 갱신되어 NormalizeExistingDecorationScales/RefreshPlayableBounds 직후 다시
            // 덮어쓰여지는 race, (b) 향후 비동기 repo 도입 시 in-flight load 와 충돌 가능성이 있어
            // 명시적으로 skip. init load 가 fire 안 됐거나(installer 비호출 경로) 실패했으면
            // (파일 손상 / 권한 / 향후 API 네트워크 실패) fallback 으로 한 번 더 시도.
            // 성공 시에만 가드 플래그를 set 하므로 실패가 영구 차단되지 않는다 (skh1205 리뷰 #2 /
            // revbot 리뷰 반영).
            if (!hasLoadedSavedDecorationsOnSceneInit)
            {
                TryRestoreSavedDecorations(onCompleted: success =>
                {
                    if (success) hasLoadedSavedDecorationsOnSceneInit = true;
                });
            }
            // 진입 시점 marker 스냅샷 — "저장하지 않고 나가기" 롤백 기준점. load 후 캡처 순서가 중요:
            // 사용자가 진입 화면에서 본 상태가 곧 롤백 목표 상태가 되도록.
            TakeEditEntrySnapshot();

            // 동물의 숲 스타일 — 시점 전환에서 벽이 시야를 가리지 않게 fade 시스템 활성화.
            // 진입 시 Default 프리셋으로 시작하므로 fade=none 으로 시작 (메인뷰 그대로).
            CollectWallRenderers();
            ApplyWallVisibilityForPreset(currentCameraPreset);

            // Block bookshelf / notebook / guestbook focus and the F1–F4 shortcuts
            // while edit mode is owning the scene. The edit overlay's own click
            // handler (decoration select / rotate / delete) keeps working because
            // it lives in this controller and reads input independently.
            if (experienceManager != null)
            {
                experienceManager.AreSceneInteractionsSuppressed = true;
            }
        }

        // Scan the active scene for the room-shell geometry and recompute
        // (playableMinX..playableMaxZ) so the grid display and floor clamp
        // use the same bounds the player actually sees from inside the
        // room:
        //   - minX / maxX: inner faces of the left / right walls (the wall
        //     surfaces visible from inside the room — NOT the floor edges,
        //     since the floor mesh extends slightly past the walls)
        //   - minZ: front edge of the floor (no front wall in this layout)
        //   - maxZ: room-interior face of the back wall — the wall surface
        //     the player actually sees at the bookshelf side. The bookshelf
        //     is partially embedded in the back wall (alcove), so we must
        //     NOT use the floor's far edge or the bookshelf bounds; the
        //     back wall's *front* face (the smallest min.z among back-wall
        //     renderers) is the visible boundary line.
        //
        // Detection runs in three layers, taking the most-room-inner result:
        //   1. Exact-name match on known wall GameObjects
        //   2. Prefix match on "BackWall" for any named back-wall strip
        //   3. Position-based scan: wall-shaped renderers (thin in Z, tall,
        //      wide) in the back half of the room footprint, *excluding*
        //      anything under BookshelfRoot / BookshelfTurntableRoot so the
        //      bookshelf itself never participates as the "wall"
        // Falls back to the FloorMin/Max fallback constants if all three
        // layers fail, so the bounds are always defined.
        private void RefreshPlayableBounds()
        {
            var minX = FloorMinX;
            var maxX = FloorMaxX;
            var minZ = FloorMinZ;
            // Initialize maxZ to BackWallReferenceZ (the visible wall front
            // face), NOT FloorMaxZ — the floor mesh extends past the wall by
            // its thickness, and using it as the fallback would let dragged
            // objects tunnel into the wall / out of the room whenever the
            // name-based wall detection below misses (renamed wall, inactive
            // root, asset reload race). Detection can only LOWER this value
            // further (when the bookshelf protrudes forward of the wall).
            var maxZ = BackWallReferenceZ;

            var scene = gameObject.scene;
            if (!scene.IsValid())
            {
                playableMinX = minX;
                playableMaxX = maxX;
                playableMinZ = minZ;
                playableMaxZ = maxZ;
                return;
            }

            var floorFound = false;
            var floorBounds = new Bounds();
            var leftWallInnerX = float.NegativeInfinity;
            var rightWallInnerX = float.PositiveInfinity;
            var backWallFrontZ = float.PositiveInfinity;

            var rootObjects = scene.GetRootGameObjects();

            // Pass 1 — name-based detection for floor + walls.
            for (var i = 0; i < rootObjects.Length; i++)
            {
                var rootObject = rootObjects[i];
                if (rootObject == null || !rootObject.activeInHierarchy)
                {
                    continue;
                }

                var renderers = rootObject.GetComponentsInChildren<Renderer>(false);
                for (var r = 0; r < renderers.Length; r++)
                {
                    var renderer = renderers[r];
                    if (renderer == null || !renderer.enabled)
                    {
                        continue;
                    }

                    var goName = renderer.gameObject.name;
                    if (goName == "Floor")
                    {
                        if (!floorFound)
                        {
                            floorBounds = renderer.bounds;
                            floorFound = true;
                        }
                        else
                        {
                            floorBounds.Encapsulate(renderer.bounds);
                        }
                    }
                    else if (goName == "LeftWall")
                    {
                        // max.x of the left wall = its inner (room-facing) face.
                        leftWallInnerX = Mathf.Max(leftWallInnerX, renderer.bounds.max.x);
                    }
                    else if (goName == "RightWall")
                    {
                        // min.x of the right wall = its inner face.
                        rightWallInnerX = Mathf.Min(rightWallInnerX, renderer.bounds.min.x);
                    }
                    else if (goName.StartsWith("BackWall", StringComparison.OrdinalIgnoreCase))
                    {
                        // min.z of any "BackWall*" renderer = its front
                        // (room-facing) face. Prefix match so this catches
                        // BackWall_LeftStrip / RightStrip / TopStrip plus
                        // any future variants with the same prefix.
                        backWallFrontZ = Mathf.Min(backWallFrontZ, renderer.bounds.min.z);
                    }
                }
            }

            // Pass 2 — position-based fallback for the back wall. Only runs
            // if Pass 1's name-based detection found nothing, otherwise the
            // size-and-position heuristic could match an unrelated thin tall
            // decoration (frame, poster, lamp shade) sitting in the back
            // half and overwrite Pass 1's correct backWallFrontZ with a
            // smaller value. Renderers under the bookshelf subtree
            // (BookshelfTurntableRoot / BookshelfRoot) are excluded so the
            // bookshelf itself can't be mistaken for the back wall — the
            // user explicitly does NOT want bookshelf bounds as the
            // boundary reference.
            if (floorFound && float.IsPositiveInfinity(backWallFrontZ))
            {
                var floorCenterZ = (floorBounds.min.z + floorBounds.max.z) * 0.5f;
                for (var i = 0; i < rootObjects.Length; i++)
                {
                    var rootObject = rootObjects[i];
                    if (rootObject == null || !rootObject.activeInHierarchy)
                    {
                        continue;
                    }

                    var renderers = rootObject.GetComponentsInChildren<Renderer>(false);
                    for (var r = 0; r < renderers.Length; r++)
                    {
                        var renderer = renderers[r];
                        if (renderer == null || !renderer.enabled) continue;
                        if (IsUnderBookshelfSubtree(renderer.transform)) continue;
                        if (IsUnderRoomShell(renderer.transform)) continue;

                        var rb = renderer.bounds;
                        // Wall-shaped: thin in Z (<=0.25m so the bookshelf's
                        // 0.25m depth edge case doesn't sneak in), tall +
                        // wide enough to be a wall. Must sit in the back
                        // half of the room footprint.
                        if (rb.size.z >= 0.25f) continue;
                        if (rb.size.y < 0.5f) continue;
                        if (rb.size.x < 0.5f) continue;
                        if (rb.center.z <= floorCenterZ) continue;
                        backWallFrontZ = Mathf.Min(backWallFrontZ, rb.min.z);
                    }
                }
            }

            if (floorFound)
            {
                // Floor edges are the safest fallback for X and front-Z when
                // walls aren't found — the visible grid then matches the
                // floor mesh exactly.
                minX = floorBounds.min.x;
                maxX = floorBounds.max.x;
                minZ = floorBounds.min.z;
            }

            // Wall inner faces win over floor edges so the grid matches the
            // visible wall surface (the wall sits slightly inside the floor
            // mesh due to its thickness).
            if (!float.IsNegativeInfinity(leftWallInnerX))
            {
                minX = leftWallInnerX;
            }

            if (!float.IsPositiveInfinity(rightWallInnerX))
            {
                maxX = rightWallInnerX;
            }

            // The bookshelf is partially embedded into the back wall — across
            // the alcove span the player sees the bookshelf's front face
            // (where the shelf openings are) as the room-inner boundary,
            // not the missing back-wall surface behind it. Pick the smallest
            // (closest-to-room-interior) of the two so whichever is more
            // forward wins as the boundary at every X.
            var bookshelfFrontZ = float.PositiveInfinity;
            for (var i = 0; i < rootObjects.Length; i++)
            {
                var rootObject = rootObjects[i];
                if (rootObject == null || !rootObject.activeInHierarchy)
                {
                    continue;
                }
                var renderers = rootObject.GetComponentsInChildren<Renderer>(false);
                for (var r = 0; r < renderers.Length; r++)
                {
                    var renderer = renderers[r];
                    if (renderer == null || !renderer.enabled) continue;
                    if (!IsUnderBookshelfSubtree(renderer.transform)) continue;
                    bookshelfFrontZ = Mathf.Min(bookshelfFrontZ, renderer.bounds.min.z);
                }
            }

            // Combine every back-side boundary candidate with the safe upper
            // bound (BackWallReferenceZ, already in `maxZ` from initialization).
            // Mathf.Min, never replace — a missed candidate must not be able
            // to push the boundary further back than the visible wall plane.
            // The remaining candidates pull `maxZ` FORWARD when the bookshelf
            // (or any other detected blocker face) protrudes into the room.
            if (!float.IsPositiveInfinity(backWallFrontZ))
            {
                maxZ = Mathf.Min(maxZ, backWallFrontZ);
            }
            if (!float.IsPositiveInfinity(bookshelfFrontZ))
            {
                maxZ = Mathf.Min(maxZ, bookshelfFrontZ);
            }

            // Defensive: keep min < max in each axis so downstream Mathf.Clamp
            // calls stay well-defined even if the scene scan returned a
            // degenerate result.
            if (minX < maxX)
            {
                playableMinX = minX;
                playableMaxX = maxX;
            }
            if (minZ < maxZ)
            {
                playableMinZ = minZ;
                playableMaxZ = maxZ;
            }
#if UNITY_EDITOR
            // 진단 — playable bounds 가 좌하단(X<0, Z<0) 영역을 포함하는지 확인. 만약 playableMinX
            // 또는 playableMinZ 가 0 이상이면 좌하단 영역 자체가 ClampToFloorBounds 에서 push-back
            // → drag/rotate 가 그 영역으로 들어가지 못하는 회귀 가능.
            Debug.Log($"[EditPlayable] refresh playableX=[{playableMinX:0.##},{playableMaxX:0.##}] " +
                      $"playableZ=[{playableMinZ:0.##},{playableMaxZ:0.##}] " +
                      $"floorFound={floorFound} backWallFrontZ={backWallFrontZ:0.##}");
#endif
        }

        // True if `t` or any of its ancestors is named "BookshelfRoot" or
        // "BookshelfTurntableRoot" — i.e. the bookshelf furniture itself.
        // Used by the position-based back-wall detection so the bookshelf
        // (which is partially embedded into the wall) isn't mistaken for
        // the wall front face.
        private static bool IsUnderBookshelfSubtree(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                var n = p.name;
                if (string.IsNullOrEmpty(n)) continue;
                if (n == "BookshelfRoot" || n == "BookshelfTurntableRoot")
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsUnderRoomShell(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                if (RoomShellExactNames.Contains(p.name))
                {
                    return true;
                }
            }
            return false;
        }

        public void ExitEditMode()
        {
            // 미저장 변경사항이 있으면 모달로 사용자 확인. 없으면 기존처럼 즉시 종료.
            if (isDirty)
            {
                Debug.Log("[RoomEdit][Save] 돌아가기 클릭 — dirty=true → 미저장 모달 표시");
                ShowUnsavedChangesModal();
                return;
            }
            Debug.Log("[RoomEdit][Save] 돌아가기 클릭 — dirty=false → 즉시 편집 종료");
            ExitEditModeInternal();
        }

        private void ShowUnsavedChangesModal()
        {
            if (unsavedChangesModalRoot == null) return;
            unsavedChangesModalRoot.transform.SetAsLastSibling();
            unsavedChangesModalRoot.SetActive(true);
            // 플래그 set 즉시 다음 UpdateSelectionPanel 이 selection action UI 를 숨긴다.
            // 또한 HandleEditModeInput / HandleEditCameraInput / Rotate / Delete 입력도 차단.
            isUnsavedChangesModalOpen = true;
            // 같은 프레임 내 입력 leak 방지 — 모달 표시 직전 selection 패널이 떠 있을 수 있으므로
            // 즉시 비활성화. 이후 닫힐 때 selectedObject 가 살아있으면 다음 Update 에서 자동 복귀.
            if (selectionPanelRoot != null && selectionPanelRoot.activeSelf)
            {
                selectionPanelRoot.SetActive(false);
            }
        }

        private void HideUnsavedChangesModal()
        {
            if (unsavedChangesModalRoot != null)
            {
                unsavedChangesModalRoot.SetActive(false);
            }
            // 모달 닫는 모든 경로(취소/저장하고 나가기/저장하지 않고 나가기) 에서 flag clear.
            // 취소 시: UpdateSelectionPanel 이 selectedObject 살아있으면 패널 재표시.
            // 저장/폐기 시: ExitEditModeInternal 의 DeselectCurrentObject 가 selectedObject=null
            // 로 만들어 패널은 계속 숨김.
            isUnsavedChangesModalOpen = false;
        }

        private void OnSaveAndExitClicked()
        {
            Debug.Log("[RoomEdit][Save] 미저장 모달: '저장하고 나가기' 선택");
            HideUnsavedChangesModal();
            // 저장 성공 시 종료, 실패 시 편집 모드 유지 + dirty 유지 + 모달 다시 표시.
            PerformSave(
                onSuccess: () =>
                {
                    Debug.Log("[RoomEdit][Save] 저장 성공 후 편집 종료");
                    ExitEditModeInternal();
                },
                onError: msg =>
                {
                    Debug.LogError($"[RoomEdit][Save] 저장 실패 → 편집 모드 유지. 에러: {msg}");
                    // dirty 는 PerformSave 내부에서 그대로 유지됨 (성공 시에만 ClearDirty).
                    ShowSaveStatusToast($"저장 실패: {msg}", isError: true, durationSec: 4f);
                });
        }

        private void OnDiscardAndExitClicked()
        {
            Debug.Log("[RoomEdit][Save] 미저장 모달: '저장하지 않고 나가기' 선택 — 변경사항 폐기 + 씬 롤백");
            HideUnsavedChangesModal();
            // 핵심: 저장만 skip 하면 메인뷰에 편집 중 변경사항이 그대로 남는 회귀 — 진입 시점 스냅샷으로
            // 씬 자체를 되돌려야 함. (배치한 객체 destroy / 이동한 객체 transform 복원 / 삭제한 객체 재생성)
            RollbackToEditEntrySnapshot();
            ClearDirty("discard via modal");
            ExitEditModeInternal();
        }

        private void OnCancelExitClicked()
        {
            Debug.Log("[RoomEdit][Save] 미저장 모달: '취소' 선택 — 편집 모드 유지");
            HideUnsavedChangesModal();
        }

        private void ExitEditModeInternal()
        {
            // If the player exits edit mode mid-drag, drop the dragged object at
            // its last valid position (don't strand it where the pointer was).
            if (draggingObject != null)
            {
                FinishDrag();
            }
            DeselectCurrentObject();
            IsEditModeActive = false;
            CloseDecorationPopupInternal();
            HideReadyModal();
            HideGrid();
            // 벽 alpha/Renderer 상태를 원본으로 복구 — Camera transform 복구보다 먼저 해서
            // 메인뷰 카메라 컨트롤러가 다음 프레임에 보는 씬이 정상 상태이도록.
            RestoreWallVisibility("ExitEditModeInternal");
            EndCameraViewportEdit();
            // 편집 UI raycast 즉시 차단 — SetActive 만으로는 다음 Update 까지 race window 가 남는다.
            // CanvasGroup.blocksRaycasts/interactable=false 를 함께 set 해서 메인씬 클릭이 책장/
            // 대시보드 focus 까지 도달하도록 보장. UpdateButtonVisibility/UpdateSelectionPanel 이
            // 다음 frame 부터 같은 값을 재계산하므로 idempotent.
            if (editOverlayCanvasGroup != null)
            {
                editOverlayCanvasGroup.blocksRaycasts = false;
                editOverlayCanvasGroup.interactable = false;
            }
            if (editOverlayRoot != null)
            {
                editOverlayRoot.SetActive(false);
            }
            if (selectionPanelCanvasGroup != null)
            {
                selectionPanelCanvasGroup.blocksRaycasts = false;
                selectionPanelCanvasGroup.interactable = false;
            }
            if (selectionPanelRoot != null)
            {
                selectionPanelRoot.SetActive(false);
            }
            if (confirmPlacementButtonRoot != null)
            {
                confirmPlacementButtonRoot.SetActive(false);
            }
            // footprint preview 는 3D mesh + LineRenderer 라 EventSystem raycast 는 안 받지만,
            // 시각 잔류 방지 + 다음 EnterEditMode 가 깨끗한 상태에서 시작하도록 hide.
            if (placementFootprint != null)
            {
                placementFootprint.Hide();
            }

            // Restore any localScale we overrode when entering edit mode so a
            // designer's manual inspector tweak survives an edit session.
            RestoreSavedDecorationScales();

            // Re-enable bookshelf / notebook / guestbook focus and shortcut handling
            // for the normal main view.
            var experienceManager = ResolveExperienceManager();
            if (experienceManager != null)
            {
                experienceManager.AreSceneInteractionsSuppressed = false;
            }

            // Snapshot 의 GameObject reference 해제 — 다음 진입 시 stale reference 들고 있지 않도록.
            // 다음 EnterEditMode 가 TakeEditEntrySnapshot 으로 다시 캡처하므로 여기서 비워도 안전.
            editEntrySnapshot.Clear();

            // MR #122 — pending(=미확정 preview) 항목이 ExitEditMode 까지 살아있다면 사용자가
            // "배치 확정"을 누르지 않고 편집 모드를 떠난 것. 저장 경로에선 CollectDecorationLayoutItems
            // 가 이미 영속화 대상에서 제외했으므로 메인 화면에 ghost 가 남지 않도록 여기서 destroy.
            // 미저장 경로(RollbackToEditEntrySnapshot 후 ExitEditModeInternal 호출)에선 set 이 이미
            // 비어있어 이 루프는 no-op + ClearAllPendingState 도 dangling key 청소만 한다.
            // 또한 placedByInventoryKey 엔트리도 함께 제거 — 다음 편집 모드 진입 시 인벤토리
            // 슬롯이 영구 disabled 로 남는 회귀 차단.
            if (pendingPlacements.Count > 0)
            {
                var toDestroy = new List<GameObject>(pendingPlacements);
                for (int i = 0; i < toDestroy.Count; i++)
                {
                    var go = toDestroy[i];
                    if (go == null) continue;
                    var marker = go.GetComponent<DecorationItemMarker>();
                    var key = marker != null ? marker.InventoryKey : null;
                    if (!string.IsNullOrEmpty(key) && placedByInventoryKey.TryGetValue(key, out var tracked) && tracked == go)
                    {
                        placedByInventoryKey.Remove(key);
                    }
                    Destroy(go);
                }
                Debug.Log($"[RoomEdit][Preview] ExitEditMode 에서 미확정 preview {toDestroy.Count}개 destroy.");
            }
            ClearAllPendingState();
        }

        // Shrink the main camera's viewport rect so it renders only into the area
        // above the bottom menu, AND apply edit-mode camera background override
        // (clearFlags=SolidColor + backgroundColor=black) so wall fade reveals show
        // black instead of the main-room skybox/gradient. Both rect and the background
        // override are restored on EndCameraViewportEdit via the matching saved* fields.
        // FOV / post-processing / Volume 은 건드리지 않음. The bottom fraction is
        // computed from the BottomMenu's actual on-screen pixel height so the
        // boundary aligns exactly with the menu's top edge regardless of device
        // aspect or canvas scaler match factor.
        // (MR #147 review Finding 5: 이전 주석은 "clearFlags/backgroundColor 안 건드림"
        // 이었으나 검은 배경 override 추가로 실제 동작과 어긋났던 부분 정리.)
        private void BeginCameraViewportEdit()
        {
            var camera = ResolveSceneCamera();
            if (camera == null)
            {
                return;
            }

            editModeCamera = camera;
            savedCameraRect = camera.rect;
            savedCameraRectValid = true;
            ApplyEditModeCameraRect();

            // 카메라 배경 캐싱 + 검은색 강제 — 벽 fade 로 방 바깥이 보일 때 skybox/기본 회색 대신
            // 검은색이 깔리도록. rect 와 같이 진입 시 캐싱 → 종료 시 원복.
            savedCameraClearFlags = camera.clearFlags;
            savedCameraBackgroundColor = camera.backgroundColor;
            savedCameraBackgroundValid = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            // EnterEditMode 가 직전에 SnapToMainView 를 호출했으므로 여기 pose 가 메인뷰 기준
            // 좌표 — Default 프리셋 target 이자 Exit 시 복구점. 프리셋 인덱스는 0 으로 리셋.
            savedCameraPosition = camera.transform.position;
            savedCameraRotation = camera.transform.rotation;
            savedCameraPoseValid = true;
            // 진입 시 = SnapToMainView 직후의 카메라 pose 가 곧 "기본 편집뷰". target=savedCameraPose 로
            // 두면 LateUpdate 가 그 자리에서 lerp 결과 = 정지 (이미 그 위치). cameraPresetTargetActive=true
            // 로 두는 이유는 사용자가 즉시 큐브/A·D 입력 시 lerp loop 가 작동하도록.
            ApplyDefaultViewState();
            cameraPresetTargetActive = true;
            Debug.Log(
                "[RoomEdit][Camera] 편집 모드 진입 — 기본 편집뷰 = MainView (SnapToMainView 진입 pose) " +
                $"position={savedCameraPosition} rotation={savedCameraRotation.eulerAngles} fov={camera.fieldOfView:0.#}");
        }

        private void ApplyEditModeCameraRect()
        {
            if (editModeCamera == null)
            {
                return;
            }

            // Preserve the camera's full-screen aspect ratio inside the top
            // (1 - BottomMenuFraction) region so the room renders with the exact
            // same composition the player sees on the normal main view. Using
            // width = height in normalized rect terms produces a viewport whose
            // pixel aspect equals Screen.width/Screen.height (i.e. the fullscreen
            // camera aspect). The remaining horizontal slack appears as the
            // pillarbox area covered by BuildPillarboxFills.
            var topFraction = 1f - BottomMenuFraction;
            var rectW = topFraction;
            var rectH = topFraction;
            var rectX = (1f - rectW) / 2f;
            var rectY = BottomMenuFraction;
            editModeCamera.rect = new Rect(rectX, rectY, rectW, rectH);
        }

        private void EndCameraViewportEdit()
        {
            if (editModeCamera != null)
            {
                if (savedCameraRectValid)
                {
                    editModeCamera.rect = savedCameraRect;
                }
                // 프리셋으로 옮겨진 transform 도 진입 시 메인뷰 pose 로 즉시 복구. 이후 메인뷰
                // 컨트롤러(BookshelfExperienceManager) 가 다시 카메라 주도권을 가져간다.
                if (savedCameraPoseValid)
                {
                    editModeCamera.transform.SetPositionAndRotation(savedCameraPosition, savedCameraRotation);
                }
                // 카메라 배경 원복 — 메인룸 skybox/기존 backgroundColor 로 복귀.
                if (savedCameraBackgroundValid)
                {
                    editModeCamera.clearFlags = savedCameraClearFlags;
                    editModeCamera.backgroundColor = savedCameraBackgroundColor;
                }
            }
            editModeCamera = null;
            savedCameraRectValid = false;
            savedCameraPoseValid = false;
            savedCameraBackgroundValid = false;
            cameraPresetTargetActive = false;
        }

        private void OnDestroy()
        {
            // Defensive: if the controller is torn down while edit mode is active,
            // restore the camera viewport so a re-loaded scene doesn't inherit a
            // partial rect. 벽 alpha/Renderer 도 같이 원복 — 안 그러면 다음 진입 시 invalid
            // sharedMaterial reference + leaked instance material 이 남는다.
            RestoreWallVisibility("OnDestroy");
            EndCameraViewportEdit();
            RoomEditThemeFilter.Changed -= HandleThemeFilterChanged;
            // MR #134 — footprint preview 의 runtime-생성 mesh/Material/GameObject 를 명시 destroy.
            // hideFlags=HideAndDontSave 라 자산화는 안 되지만 instance leak 방지.
            if (placementFootprint != null)
            {
                placementFootprint.Dispose();
                placementFootprint = null;
            }
        }

        // MR #120 simonjiho 리뷰 #6 반영 — 비정상 종료(scene unload, GameObject SetActive(false),
        // domain reload 직전) 경로에서도 instance material leak / sharedMaterial 미복구 회귀를 막는
        // defensive fallback. ExitEditMode 정상 흐름은 RestoreWallVisibility 를 이미 호출하므로 여기서는
        // wallVisibilityCollected==true 인 경우(=fade instance 가 아직 살아있는 경우) 만 추가로 정리.
        private void OnDisable()
        {
            if (!wallVisibilityCollected) return;
            RestoreWallVisibility("OnDisable");
        }

        // ---------- Animal Crossing 해피홈식 자유 orbit 카메라 ----------
        //
        // pose = (orbitYaw, orbitPitch, orbitDistance) 로만 결정 — selectedObject 무참조
        // (오브젝트 이동/드래그 중에도 카메라 follow X). 입력 종류:
        //   - 큐브 면 클릭: 해당 면 고정값으로 orbit 점프
        //   - "기본 시점" 버튼: 기본 orbit 값 으로 점프
        //   - A/D: yaw 연속 회전. W/S: distance 줄임/늘림. 스크롤: distance 단위 점프
        // LateUpdate 가 매 프레임 ComputeOrbitPose 로 target 을 재계산하지만 결과는 orbit 만
        // 의존하므로 객체 이동에 영향받지 않음.

        // 큐브 면 클릭 — 해당 면을 향하는 고정 시점으로 즉시 점프. lerp 는 LateUpdate 가 처리.
        private void OnCubeFaceClicked(CameraPreset face)
        {
            if (!IsEditModeActive) return;

            switch (face)
            {
                case CameraPreset.TopDown:
                {
                    // 정면뷰 축 정렬 — yaw 0° 로 고정. orbit 중심은 방 중앙. pitch 89° (직각 위, 90°는 짐벌락).
                    orbitCenter = GetRoomCenter();
                    orbitYaw = 0f;
                    orbitPitch = 89f;
                    var roomSpan = Mathf.Max(playableMaxX - playableMinX, playableMaxZ - playableMinZ);
                    var (tdMin, tdMax) = GetOrbitDistanceRangeForPreset(CameraPreset.TopDown);
                    orbitDistance = Mathf.Clamp(roomSpan * 1.1f, tdMin, tdMax);
                    break;
                }
                case CameraPreset.Front:
                case CameraPreset.Back:
                case CameraPreset.LeftAngle:
                case CameraPreset.RightAngle:
                {
                    // 벽면 포커스 — orbit 중심을 해당 벽 중앙으로. distance 는 ComputeWallFocusDistance
                    // 로 자동 산정 (벽 width/height + FOV + aspect 기반) → 벽 전체가 화면에 들어오면서
                    // 편집 가능한 크기로 보임. 이전 1.5m 하드코딩은 벽 일부만 보이는 과확대 회귀.
                    orbitCenter = GetWallFocusCenter(face);
                    orbitPitch = 4f;
                    orbitDistance = ComputeWallFocusDistance(face);
                    orbitYaw = face switch
                    {
                        CameraPreset.Front      => 0f,    // 책장(+Z 벽) 의 -Z 쪽 → 벽 정면
                        CameraPreset.Back       => 180f,  // 가상 정면 (-Z) 의 +Z 쪽
                        CameraPreset.LeftAngle  => 90f,   // 좌측 벽(-X) 의 +X 쪽 → 벽 정면
                        CameraPreset.RightAngle => 270f,  // 우측 벽(+X) 의 -X 쪽
                        _ => 0f,
                    };
                    break;
                }
                default:
                    return;
            }
            var (cubeMin, cubeMax) = GetOrbitDistanceRangeForPreset(face);
            orbitDistance = Mathf.Clamp(orbitDistance, cubeMin, cubeMax);
            orbitPitch = Mathf.Clamp(orbitPitch, OrbitMinPitch, OrbitMaxPitch);

            isInDefaultView = false;
            currentCameraPreset = face;
            cameraPresetTargetActive = true;
            ApplyWallVisibilityForCurrentView("CubeFace");

            // 진단 로그 — 카메라 pose, target, 벽 bounds, framing 거리, FOV 모두 노출.
            ComputeOrbitPose(out var camPos, out var camRot);
            float fov = editModeCamera != null ? editModeCamera.fieldOfView : -1f;
            float aspect = editModeCamera != null ? editModeCamera.aspect : -1f;
            string targetWallName = face switch
            {
                CameraPreset.Front      => "BackWall (책장)",
                CameraPreset.Back       => "FrontWall (가상, open 측)",
                CameraPreset.LeftAngle  => "LeftWall",
                CameraPreset.RightAngle => "RightWall",
                CameraPreset.TopDown    => "(없음 — 탑다운)",
                _ => "?",
            };
            bool isWallFocus = face == CameraPreset.Front || face == CameraPreset.Back ||
                               face == CameraPreset.LeftAngle || face == CameraPreset.RightAngle;
            if (isWallFocus)
            {
                var dims = GetWallDimensions(face);
                // vertical-fit / horizontal-fit 둘 다 distance 보다 작으면 벽 전체가 화면 안에 들어옴.
                float halfFovY = fov * 0.5f * Mathf.Deg2Rad;
                float tanHalf = Mathf.Tan(halfFovY);
                float verticalNeed  = (dims.y * WallFocusFramingPadding) / (2f * tanHalf);
                float horizontalNeed = (dims.x * WallFocusFramingPadding) / (2f * tanHalf * Mathf.Max(aspect, 0.001f));
                bool wallFitsInView = orbitDistance >= verticalNeed && orbitDistance >= horizontalNeed;
                Debug.Log(
                    $"[RoomEdit][Camera] 큐브 면 클릭 → {face} ({CameraPresetLabels[face]}) " +
                    $"target-wall={targetWallName} wall-bounds={dims.x:0.##}m×{dims.y:0.##}m " +
                    $"computed-distance={orbitDistance:0.##}m (vertical-fit={verticalNeed:0.##}m, horizontal-fit={horizontalNeed:0.##}m, padding={WallFocusFramingPadding:0.00}) " +
                    $"wall-fits-in-view={wallFitsInView} " +
                    $"orbitCenter={orbitCenter} camera-pos={camPos} camera-target={orbitCenter} fov={fov:0.#} aspect={aspect:0.##} " +
                    $"yaw={orbitYaw:0.#}° pitch={orbitPitch:0.#}° " +
                    "selectedObject-기준-focus=NO");
            }
            else
            {
                Debug.Log(
                    $"[RoomEdit][Camera] 큐브 면 클릭 → {face} ({CameraPresetLabels[face]}) " +
                    $"orbitCenter={orbitCenter} camera-pos={camPos} fov={fov:0.#} " +
                    $"yaw={orbitYaw:0.#}° pitch={orbitPitch:0.#}° distance={orbitDistance:0.##}m " +
                    "탑다운 (yaw=0° 정렬) — 모든 벽+천장 fade selectedObject-기준-focus=NO");
            }
        }

        // "기본 시점" 버튼 — 메인씬에서 사용자가 보던 기본 카메라 시점으로 복귀.
        //
        // **중요**: 이전 구현은 orbit 기본값 (yaw=25°/pitch=18°/distance=4.2m) 을 적용했는데,
        // 이는 우측 사선 비슷한 시점이었음. MR !120 review beshu [P1]: 사용자 기대는 "메인씬에서
        // 보던 기본 시점" 이라는 명시. EnterEditMode 가 진입 직후 BookshelfExperienceManager.SnapToMainView()
        // 를 호출하고 그 직후의 카메라 transform 을 savedCameraPosition/Rotation 에 저장하므로,
        // 그 값이 곧 "메인씬 기본 카메라 pose". 이것을 그대로 target 으로 사용 → 메인씬과 동일.
        // savedCameraPoseValid=false 인 비정상 경로 (뷰포트 begin 미수행) 면 orbit 기본값 fallback.
        // selectedObject 를 절대 참조하지 않으므로 객체 follow / focus 없음.
        private void OnDefaultViewClicked()
        {
            if (!IsEditModeActive) return;

            // 이전 view 저장 — 로그/디버깅 용 + TopDown→Default 전환 case 식별.
            var previousPreset = currentCameraPreset;
            bool wasTopDown = previousPreset == CameraPreset.TopDown;

            ApplyDefaultViewState();
            cameraPresetTargetActive = true;

            // **명시 reset (#3 회귀 방어)**: change-detection (ApplyWallVisibilityForCurrentView) 우회.
            // ApplyDefaultViewState 가 lastAppliedFadePreset=Default 로 설정해서 ApplyWallVisibilityForCurrentView
            // 가 target==lastAppliedFadePreset 으로 보고 skip 하던 회귀 — 모든 벽이 TopDown fade 상태로
            // 남던 #3 버그. 여기서 직접 Apply 호출 + lastAppliedFadePreset 동기화로 강제 복구.
            ApplyWallVisibilityForPreset(CameraPreset.Default);
            lastAppliedFadePreset = CameraPreset.Default;
            int fadedAfterReset = CountFadedWalls();

            if (savedCameraPoseValid)
            {
                float fov = editModeCamera != null ? editModeCamera.fieldOfView : -1f;
                Debug.Log(
                    "[RoomEdit][Camera] 기본 시점 복귀 — preset=MainView (SnapToMainView 진입 pose) " +
                    $"이전 view={previousPreset} (TopDown 여부={wasTopDown}) → wall fade reset 후 잔여 faded={fadedAfterReset}/{wallRendererCaches.Count} " +
                    $"position={savedCameraPosition} rotation={savedCameraRotation.eulerAngles} fov={fov:0.#} " +
                    $"selectedObject-기준-focus=NO selected={(selectedObject != null ? selectedObject.name : "<none>")}");
            }
            else
            {
                Debug.LogWarning(
                    "[RoomEdit][Camera] 기본 시점 복귀 — savedCameraPose 무효 → orbit 기본값 fallback. " +
                    $"이전 view={previousPreset} 잔여 faded={fadedAfterReset}");
            }
        }

        // ---------- Decoration 저장 / 로드 / dirty 추적 ----------

        // 명시적 "저장" 버튼 핸들러. 현재 씬의 DecorationItemMarker 들을 DecorationLayoutItem 으로
        // 직렬화 후 SaveDecorationsUseCase 로 영속화. 성공 시 ClearDirty + 토스트.
        private void OnSaveButtonClicked()
        {
            if (!IsEditModeActive) return;
            Debug.Log("[RoomEdit][Save] 저장 버튼 클릭");
            PerformSave(
                onSuccess: () => ShowSaveStatusToast("저장되었습니다", isError: false, durationSec: 2.0f),
                onError: msg => ShowSaveStatusToast($"저장 실패: {msg}", isError: true, durationSec: 4f));
        }

        // 저장 핵심 — UseCase 보장 + 마커 수집 + 진단 로그 + 콜백.
        private void PerformSave(Action onSuccess, Action<string> onError)
        {
            EnsureDecorationUseCases();
            if (saveDecorationsUseCase == null)
            {
                Debug.LogError("[RoomEdit][Save] SaveDecorationsUseCase 생성 실패 — 저장 불가");
                onError?.Invoke("저장 시스템 초기화 실패");
                return;
            }

            var items = CollectDecorationLayoutItems();
            Debug.Log($"[RoomEdit][Save] 저장 대상 데코레이션 {items.Count}개");
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                Debug.Log(
                    $"  [{i}] assetId={it.AssetId} pos=({it.PositionX:0.##},{it.PositionY:0.##},{it.PositionZ:0.##}) " +
                    $"rotY={it.RotationY:0.#}° scale=({it.ScaleX:0.##},{it.ScaleY:0.##},{it.ScaleZ:0.##})");
            }

            saveDecorationsUseCase.Execute(items,
                onSuccess: () =>
                {
                    Debug.Log($"[RoomEdit][Save] 저장 성공 — {items.Count}개 영속화 완료");
                    ClearDirty("save success");
                    TryUploadRoomThumbnail();
                    onSuccess?.Invoke();
                },
                onError: msg =>
                {
                    Debug.LogError($"[RoomEdit][Save] 저장 실패 — {msg}");
                    // dirty 유지 — 사용자가 다시 저장 시도하거나 discard 선택할 때까지.
                    onError?.Invoke(msg);
                });
        }

        // 씬의 DecorationItemMarker 들을 DecorationLayoutItem 으로 변환. InventoryKey 가 비어있거나
        // 세션 간 안정적이지 않은 fallback (prefab-iid:) 인 경우 식별 불가 → skip + 경고 로그.
        // MR #120 skh1205 리뷰 #4 반영 — Prefab.GetInstanceID() 는 한 세션 내에서만 유효한
        // 런타임 ID 라 저장 파일에 들어가는 순간 다음 실행에서 매칭이 깨진다.
        private List<DecorationLayoutItem> CollectDecorationLayoutItems()
        {
            var list = new List<DecorationLayoutItem>();
            var markers = FindObjectsByType<DecorationItemMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int skipped = 0, unstable = 0, pending = 0;
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                if (m == null) continue;
                // MR #122 — preview 상태(미확정 신규 추가) 항목은 저장에서 제외. 사용자가 "배치 확정"
                // 누른 항목만 영속화 대상. 미저장 종료 시엔 RollbackToEditEntrySnapshot 가 alive
                // 비교로 자동 destroy 한다.
                if (pendingPlacements.Contains(m.gameObject))
                {
                    pending++;
                    continue;
                }
                if (string.IsNullOrEmpty(m.InventoryKey))
                {
                    skipped++;
                    Debug.LogWarning($"[RoomEdit][Save] '{m.gameObject.name}' InventoryKey 비어있음 — 저장 skip");
                    continue;
                }
                if (!IsPersistableInventoryKey(m.InventoryKey))
                {
                    unstable++;
                    Debug.LogWarning(
                        $"[RoomEdit][Save] '{m.gameObject.name}' key='{m.InventoryKey}' — 세션 간 안정적이지 않은 식별자 (prefab-iid). " +
                        "다음 실행에서 매칭이 깨지므로 저장에서 제외. catalog 의 SourceAssetPath 를 채워야 함.");
                    continue;
                }
                var t = m.transform;
                list.Add(new DecorationLayoutItem
                {
                    AssetId = m.InventoryKey,
                    PositionX = t.position.x,
                    PositionY = t.position.y,
                    PositionZ = t.position.z,
                    RotationY = t.rotation.eulerAngles.y,
                    ScaleX = t.localScale.x,
                    ScaleY = t.localScale.y,
                    ScaleZ = t.localScale.z,
                });
            }
            if (skipped > 0 || unstable > 0 || pending > 0)
            {
                Debug.LogWarning(
                    $"[RoomEdit][Save] 저장 제외 — InventoryKey 누락 {skipped}개, prefab-iid (불안정) {unstable}개, " +
                    $"미확정 preview {pending}개");
            }
            return list;
        }

        // 세션 간 안정적인 영속화 키만 true. BuildInventoryKey 가 발행하는 prefix:
        //   - "path:..."     → 자산 경로 기반. SourceAssetPath 가 바뀌지 않는 한 안정. 저장 OK.
        //   - "asset:..."    → 서버 inventory assetId. 미래 도입 예정. 저장 OK.
        //   - "inv:..."      → 서버 inventory inventoryId. 미래 도입 예정. 저장 OK.
        //   - "name:..."     → DisplayName fallback. 동명 자산 충돌 위험은 있지만 빌드 간 동일하므로 허용.
        //   - "prefab-iid:..." → Prefab.GetInstanceID() 런타임 ID. 세션 간 안정성 X. 저장 NO.
        private static bool IsPersistableInventoryKey(string key)
        {
            return !string.IsNullOrEmpty(key) && !key.StartsWith("prefab-iid:", StringComparison.Ordinal);
        }

        // UseCase / Repository 지연 초기화 — InitializeOnce 시점에 호출.
        // TODO(server-decoration-api): 백엔드 데코레이션 API 추가 시 LocalFileDecorationRepository 를
        // ApiDecorationRepository 로 교체하면 됨 (interface 동일).
        private void EnsureDecorationUseCases()
        {
            if (saveDecorationsUseCase != null && loadDecorationsUseCase != null) return;
            try
            {
                if (decorationRepository == null)
                {
                    // user/space scope — 같은 기기에 다른 계정으로 로그인해도 layout 이 섞이지 않게
                    // userId 를 파일명에 포함. AuthSession.UserId 가 0 이면 anon fallback (대부분 dev 경로).
                    // MR #120 skh1205 리뷰 #2 반영.
                    decorationRepository = new LocalFileDecorationRepository(AuthSession.UserId);
                }
                if (saveDecorationsUseCase == null)
                {
                    saveDecorationsUseCase = new SaveDecorationsUseCase(decorationRepository);
                }
                if (loadDecorationsUseCase == null)
                {
                    loadDecorationsUseCase = new LoadDecorationsUseCase(decorationRepository);
                }
                Debug.Log($"[RoomEdit][Save] decoration use cases ready (repo={decorationRepository.GetType().Name})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoomEdit][Save] EnsureDecorationUseCases 실패: {ex.GetType().Name} {ex.Message}");
            }
        }

        // dirty 상태 마킹 — 인벤토리 배치/이동/회전/삭제 직후 호출. 저장 성공/모달 discard 시 ClearDirty.
        private void SetDirty(string reason)
        {
            if (isDirty) return;
            isDirty = true;
            Debug.Log($"[RoomEdit][Save] dirty=true (reason={reason})");
        }

        private void ClearDirty(string reason)
        {
            if (!isDirty) return;
            isDirty = false;
            Debug.Log($"[RoomEdit][Save] dirty=false (reason={reason})");
        }

        // 편집 진입 시점의 marker 상태를 스냅샷. "저장하지 않고 나가기" 롤백의 기준점.
        // 호출 순서: RebuildPlacedByInventoryKeyFromScene → TryRestoreSavedDecorations → TakeEditEntrySnapshot
        // (저장본 load 까지 끝난 직후 캡처해야 사용자가 본 진입 화면과 정확히 동일한 상태 보존).
        private void TakeEditEntrySnapshot()
        {
            editEntrySnapshot.Clear();
            var markers = FindObjectsByType<DecorationItemMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int unmatched = 0;
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                if (m == null) continue;
                var snap = new DecorationSnapshot
                {
                    InventoryKey = m.InventoryKey,
                    InstanceAtEntry = m.gameObject,
                    Position = m.transform.position,
                    Rotation = m.transform.rotation,
                    LocalScale = m.transform.localScale,
                };
                if (TryFindInventoryEntryByKey(m.InventoryKey, out var entry))
                {
                    snap.SourcePrefab = entry.Prefab;
                    snap.DisplayName = entry.DisplayName;
                }
                else
                {
                    unmatched++;
                    snap.SourcePrefab = null;
                    snap.DisplayName = m.gameObject.name;
                }
                editEntrySnapshot.Add(snap);
            }
            Debug.Log(
                $"[RoomEdit][Snapshot] entry 시점 marker {editEntrySnapshot.Count}개 캡처 " +
                $"(인벤토리 매칭 실패 {unmatched}개 — 편집 중 삭제 시 복원 불가).");
        }

        // InventoryKey 로 inventoryEntries 에서 매칭. BuildInventoryKey 가 일관된 prefix 를 발행하므로
        // 단순 string 비교로 충분.
        // 추가로 평탄화(MR #154) 이전 저장본 호환 — old path key 면 LegacyInventoryKeyAliases 로
        // 새 평탄 경로 key 로 매핑 후 매칭. 한 번 매칭되면 다음 저장 시 새 key 로 자동 갱신.
        private bool TryFindInventoryEntryByKey(string key, out InventoryEntry entry)
        {
            if (!string.IsNullOrEmpty(key))
            {
                if (LegacyInventoryKeyAliases.TryGetValue(key, out var aliased))
                {
                    key = aliased;
                }
                for (int i = 0; i < inventoryEntries.Count; i++)
                {
                    if (BuildInventoryKey(inventoryEntries[i]) == key)
                    {
                        entry = inventoryEntries[i];
                        return true;
                    }
                }
            }
            entry = default;
            return false;
        }

        // "저장하지 않고 나가기" 롤백 — 진입 시점 스냅샷 기준으로 씬 상태 복원.
        // 동작:
        //   1) 진행 중 드래그/선택 정리 (다음 destroy 가 dangling 참조를 만들지 않게).
        //   2) snapshot 에 살아있는 marker 들의 reference set 구축.
        //   3) 현재 marker 중 set 에 없는 것 (= 편집 중 새로 배치된 것) Destroy.
        //   4) snapshot 항목별:
        //        - InstanceAtEntry 살아있음 → transform/scale 복원 (이동/회전 되돌림).
        //        - InstanceAtEntry 죽음 + SourcePrefab 있음 → 재생성 (삭제 되돌림).
        //        - SourcePrefab 도 없음 → 복원 불가 (warning 로그, drop).
        //   5) placedByInventoryKey 재구성 + 인벤토리 슬롯 갱신.
        private void RollbackToEditEntrySnapshot()
        {
            Debug.Log($"[RoomEdit][Rollback] 시작 — 스냅샷 {editEntrySnapshot.Count}개 기준으로 씬 복원");

            // 1) drag/select 정리.
            if (draggingObject != null) FinishDrag();
            DeselectCurrentObject();

            // 2) snapshot 의 살아있는 instance set.
            var aliveAtSnapshot = new HashSet<GameObject>();
            for (int i = 0; i < editEntrySnapshot.Count; i++)
            {
                var inst = editEntrySnapshot[i].InstanceAtEntry;
                if (inst != null) aliveAtSnapshot.Add(inst);
            }

            // 3) 편집 중 새로 배치된 marker destroy.
            var currentMarkers = FindObjectsByType<DecorationItemMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int newlyDestroyed = 0;
            for (int i = 0; i < currentMarkers.Length; i++)
            {
                var m = currentMarkers[i];
                if (m == null) continue;
                if (!aliveAtSnapshot.Contains(m.gameObject))
                {
                    Destroy(m.gameObject);
                    newlyDestroyed++;
                }
            }

            // 4) snapshot 항목 처리.
            int transformRestored = 0, recreated = 0, lost = 0;
            for (int i = 0; i < editEntrySnapshot.Count; i++)
            {
                var snap = editEntrySnapshot[i];
                if (snap.InstanceAtEntry != null)
                {
                    var t = snap.InstanceAtEntry.transform;
                    t.SetPositionAndRotation(snap.Position, snap.Rotation);
                    t.localScale = snap.LocalScale;
                    transformRestored++;
                }
                else if (snap.SourcePrefab != null)
                {
                    RecreateMarkerFromSnapshot(snap);
                    recreated++;
                }
                else
                {
                    lost++;
                    Debug.LogWarning(
                        $"[RoomEdit][Rollback] '{snap.InventoryKey}' prefab 없음 — 복원 skip " +
                        "(snapshot 시점 inventoryEntries 매칭 실패한 marker. scene-baked 또는 catalog 변경).");
                }
            }

            // 5) dictionary + 인벤토리 슬롯 재구성.
            placedByInventoryKey.Clear();
            RebuildPlacedByInventoryKeyFromScene();
            RebuildInventorySlots();

            // 6) MR #122 — pending state 정리. 신규 추가분이 위 destroy 루프에서 사라졌으므로
            // pendingPlacements set 도 비우고 dangling Renderer key 가 backup dict 에 남지 않게.
            ClearAllPendingState();

            Debug.Log(
                $"[RoomEdit][Rollback] 완료 — 새로 추가분 destroy={newlyDestroyed}, " +
                $"transform 복원={transformRestored}, 재생성={recreated}, 복원 불가={lost}");
        }

        // PlaceInventoryEntry 의 GameObject 생성/collider/marker 부분만 추출 — 롤백 전용.
        // ring search / bounds clamp 는 skip (snapshot 의 transform 은 이미 valid 한 placement 였음).
        private void RecreateMarkerFromSnapshot(DecorationSnapshot snap)
        {
            if (snap.SourcePrefab == null) return;
            EnsurePlacedItemsRoot();

            var go = Instantiate(snap.SourcePrefab);
            go.name = $"Decoration_{SanitizeName(snap.DisplayName)}_{System.Guid.NewGuid():N}";
            go.transform.SetParent(placedItemsRoot.transform, true);
            go.transform.SetPositionAndRotation(snap.Position, snap.Rotation);
            go.transform.localScale = snap.LocalScale;

            // collider 합성 (없으면) — PlaceInventoryEntry 와 동일 패턴, raycast selection 위해 필요.
            var rendererBounds = ComputeRendererBounds(go);
            if (rendererBounds.HasValue && go.GetComponentInChildren<Collider>() == null)
            {
                var bounds = rendererBounds.Value;
                var collider = go.AddComponent<BoxCollider>();
                var localCenter = go.transform.InverseTransformPoint(bounds.center);
                var localSize = go.transform.InverseTransformVector(bounds.size);
                collider.center = localCenter;
                collider.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
            }

            var marker = go.AddComponent<DecorationItemMarker>();
            marker.InventoryKey = snap.InventoryKey;
        }

        // BookshelfSceneInstaller.InstallIntoActiveScene 가 룸/책장/scene-baked decoration 생성을
        // 모두 끝낸 직후 호출하는 진입점. 저장된 layout 을 메인 화면에서 즉시 보이도록 1회 복원
        // (MR #128). 호출 책임은 installer 측에 있다 — controller 자체의 Start() 에서 트리거하면
        // InstallScene 보다 먼저 실행될 수 있어 marker 매칭이 깨지는 순서 버그 발생 (MR #128
        // skh1205 리뷰 #1).
        //
        // 동작:
        //   1. 이미 성공적으로 1회 load 했으면 no-op (중복 spawn / 이중 갱신 차단).
        //   2. UI/inventoryEntries 가 아직 빌드 안 됐을 수 있으므로 InitializeIfNeeded 보장.
        //   3. scene-baked marker 와 placedByInventoryKey 동기화.
        //   4. TryRestoreSavedDecorations 호출 — 성공 시에만 hasLoadedSavedDecorationsOnSceneInit
        //      = true 로 set. 실패 시 false 유지 → 다음 EnterEditMode 가 fallback 으로 재시도
        //      (MR #128 skh1205 리뷰 #2 / revbot 리뷰 — flag race 수정).
        internal void LoadSavedDecorationsAfterSceneInstall()
        {
            if (hasLoadedSavedDecorationsOnSceneInit) return;

            InitializeIfNeeded();
            // 씬에 미리 배치된 marker (scene-baked 또는 다른 코드가 만든 것) 와 controller-local
            // cache 동기화. TryRestoreSavedDecorations 의 매칭 분기가 의존.
            RebuildPlacedByInventoryKeyFromScene();
            TryRestoreSavedDecorations(onCompleted: success =>
            {
                if (success) hasLoadedSavedDecorationsOnSceneInit = true;
            });
        }

        // 저장된 layout 복원.
        //   - 씬에 marker 가 이미 있으면 transform 만 갱신 (e.g. scene-baked 자산).
        //   - 씬에 marker 가 없으면 inventoryEntries 에서 prefab 매칭해 Instantiate (앱 재시작 후 복원 경로).
        //   - prefab 매칭 실패하면 lost 로 집계 + 경고 (catalog 누락 / 이름 변경).
        // MR #120 skh1205 리뷰 #1 반영 — 이전에는 unmatched 가 그냥 drop 되어 fresh scene 에서
        // 사용자가 저장 토스트는 봤는데 방은 비어있는 회귀가 있었다.
        // MR #128: 호출 경로가 "EnterEditMode 단독" → "InstallScene 끝점 + EnterEditMode fallback".
        // onCompleted(bool success) 콜백으로 성공/실패를 호출자에게 전달하므로 호출자는 그 결과로
        // hasLoadedSavedDecorationsOnSceneInit 같은 외부 가드 플래그를 정확히 관리할 수 있다.
        // success=true: items 적용 완료 (빈 리스트 포함 — 정상 응답이므로 retry 불필요).
        // success=false: useCase 미초기화 또는 repository onError 발사된 경우 — 호출자가 retry 가능.
        private void TryRestoreSavedDecorations(Action<bool> onCompleted = null)
        {
            EnsureDecorationUseCases();
            if (loadDecorationsUseCase == null)
            {
                onCompleted?.Invoke(false);
                return;
            }
            loadDecorationsUseCase.Execute(
                onSuccess: items =>
                {
                    if (items == null || items.Count == 0)
                    {
                        Debug.Log("[RoomEdit][Load] 저장된 데코레이션 없음 (빈 리스트)");
                        onCompleted?.Invoke(true);
                        return;
                    }
                    int matched = 0, recreated = 0, lost = 0;
                    for (int i = 0; i < items.Count; i++)
                    {
                        var it = items[i];
                        if (placedByInventoryKey.TryGetValue(it.AssetId, out var existing) && existing != null)
                        {
                            existing.transform.position = new Vector3(it.PositionX, it.PositionY, it.PositionZ);
                            existing.transform.rotation = Quaternion.Euler(0f, it.RotationY, 0f);
                            existing.transform.localScale = new Vector3(it.ScaleX, it.ScaleY, it.ScaleZ);
                            matched++;
                            continue;
                        }

                        if (TryInstantiateSavedItem(it))
                        {
                            recreated++;
                        }
                        else
                        {
                            lost++;
                        }
                    }
                    if (recreated > 0)
                    {
                        // 슬롯 disabled/배치됨 표시를 새로 생성된 인스턴스에 맞춰 갱신.
                        RebuildInventorySlots();
                    }
                    Debug.Log(
                        $"[RoomEdit][Load] 복원 완료 — 기존 marker 매칭 갱신 {matched}개, prefab 재생성 {recreated}개, 복원 불가 {lost}개");
                    onCompleted?.Invoke(true);
                },
                onError: msg =>
                {
                    Debug.LogWarning($"[RoomEdit][Load] 저장 데이터 로드 실패: {msg}");
                    onCompleted?.Invoke(false);
                });
        }

        // 저장된 layout item 1개를 prefab Instantiate 해서 씬에 복원. 성공 시 placedByInventoryKey 등록.
        // PlaceInventoryEntry 의 ring-search/bounds-clamp 는 건너뛴다 — 저장 시점에 valid 한 좌표였고
        // 같은 placement 를 그대로 되살리는 게 의도. 충돌은 사용자가 다음 편집 모드에서 직접 정리.
        private bool TryInstantiateSavedItem(DecorationLayoutItem it)
        {
            if (it == null || string.IsNullOrEmpty(it.AssetId))
            {
                Debug.LogWarning("[RoomEdit][Load] AssetId 비어있는 item — skip");
                return false;
            }
            if (!TryFindInventoryEntryByKey(it.AssetId, out var entry) || entry.Prefab == null)
            {
                Debug.LogWarning(
                    $"[RoomEdit][Load] '{it.AssetId}' prefab 매칭 실패 — 카탈로그 누락 또는 path 변경. 복원 skip.");
                return false;
            }

            EnsurePlacedItemsRoot();
            var go = Instantiate(entry.Prefab);
            go.name = $"Decoration_{SanitizeName(entry.DisplayName)}_{System.Guid.NewGuid():N}";
            go.transform.SetParent(placedItemsRoot.transform, true);
            go.transform.SetPositionAndRotation(
                new Vector3(it.PositionX, it.PositionY, it.PositionZ),
                Quaternion.Euler(0f, it.RotationY, 0f));
            // 저장 시점의 scale 그대로 — NormalizeSpawnedScale 의 등방성 가정과 어긋나도
            // 사용자가 본 그대로 복원하는 invariant 우선.
            go.transform.localScale = new Vector3(it.ScaleX, it.ScaleY, it.ScaleZ);

            // collider 합성 (없으면) — RecreateMarkerFromSnapshot/PlaceInventoryEntry 와 동일 패턴.
            var rendererBounds = ComputeRendererBounds(go);
            if (rendererBounds.HasValue && go.GetComponentInChildren<Collider>() == null)
            {
                var bounds = rendererBounds.Value;
                var collider = go.AddComponent<BoxCollider>();
                var localCenter = go.transform.InverseTransformPoint(bounds.center);
                var localSize = go.transform.InverseTransformVector(bounds.size);
                collider.center = localCenter;
                collider.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
            }

            var marker = go.AddComponent<DecorationItemMarker>();
            marker.InventoryKey = it.AssetId;

            // 1회 배치 제한 캐시 등록 — 안 하면 사용자가 인벤토리에서 같은 자산 재배치 시도 시
            // disabled 표시가 안 떠서 이중 배치 가능.
            placedByInventoryKey[it.AssetId] = go;
            return true;
        }

        // 토스트 — 짧은 안내 메시지. 색상은 success/error 구분.
        private void ShowSaveStatusToast(string message, bool isError, float durationSec)
        {
            if (saveStatusToastRoot == null || saveStatusToastText == null) return;
            saveStatusToastText.text = message;
            saveStatusToastText.color = Color.white;
            var bg = saveStatusToastRoot.GetComponentInChildren<Image>();
            if (bg != null)
            {
                bg.color = isError ? new Color(0.76f, 0.31f, 0.31f, 0.9f) : WithAlpha(TextColor, 0.85f);
            }
            saveStatusToastRoot.SetActive(true);
            saveStatusToastRoot.transform.SetAsLastSibling();
            saveStatusToastHideTime = Time.unscaledTime + Mathf.Max(0.5f, durationSec);
        }

        // 현재 fade 적용된 벽(InstanceMaterial != null) 개수 — 디버그 로그 / 회귀 검증용.
        private int CountFadedWalls()
        {
            int n = 0;
            for (int i = 0; i < wallRendererCaches.Count; i++)
            {
                if (wallRendererCaches[i].InstanceMaterial != null) n++;
            }
            return n;
        }

        // savedCameraPosition 을 기준으로 orbit (yaw/pitch/distance) 역산. Default → orbit 전환 시
        // 카메라 점프 없이 자연스러운 추격을 위해 사용. 좌표계: ComputeOrbitPose 의 역연산.
        //   horizontal·sin(yaw) = offset.x, vertical = offset.y, -horizontal·cos(yaw) = offset.z
        //   → yaw = atan2(offset.x, -offset.z),  pitch = asin(offset.y / distance)
        private void DeriveOrbitFromSavedPose()
        {
            if (!savedCameraPoseValid) return;
            // saved pose 는 메인뷰 → orbit 중심도 방 중앙으로 reset.
            orbitCenter = GetRoomCenter();
            var offset = savedCameraPosition - orbitCenter;
            float dist = offset.magnitude;
            if (dist < 0.01f) return; // 이상치 방어

            orbitDistance = Mathf.Clamp(dist, MinOrbitDistance, MaxOrbitDistance);
            orbitPitch = Mathf.Clamp(
                Mathf.Asin(Mathf.Clamp(offset.y / dist, -1f, 1f)) * Mathf.Rad2Deg,
                OrbitMinPitch, OrbitMaxPitch);
            orbitYaw = Mathf.Atan2(offset.x, -offset.z) * Mathf.Rad2Deg;
        }

        // Default 모드 활성화 — orbit 값도 기본값으로 동기화해서 다음 A/D 첫 입력 시 자연스럽게
        // orbit 진입 (DeriveOrbitFromSavedPose 가 saved pose 기준으로 다시 보정함).
        // **lastAppliedFadePreset 은 손대지 않음 (#3 회귀 방어)**: ApplyWallVisibilityForCurrentView 의
        // change-detection 이 잘못 skip 되어 TopDown→Default 전환 시 fade 가 풀리지 않는 버그 발생.
        // 호출자(OnDefaultViewClicked / EnterEditMode 후속 ApplyWallVisibilityForPreset)가 wall fade 를
        // 명시 적용한 후 lastAppliedFadePreset 을 직접 갱신.
        private void ApplyDefaultViewState()
        {
            // orbit 중심을 방 중앙으로 — Default → orbit 자유회전(A/D) 시 방 전체 기준으로 도는게 자연스러움.
            // 벽면 포커스 후 Default 클릭하면 orbitCenter 가 wall midpoint 로 남아있을 수 있어 명시 reset.
            orbitCenter = GetRoomCenter();
            orbitYaw = DefaultOrbitYaw;
            orbitPitch = DefaultOrbitPitch;
            orbitDistance = DefaultOrbitDistance;
            isInDefaultView = true;
            currentCameraPreset = CameraPreset.Default;
        }

        // 매 프레임 호출 — Default 모드면 saved main view pose, 그 외에는 orbit 상태 재계산.
        // 둘 다 selectedObject 절대 무참조 → 객체 follow X.
        private void LateUpdate()
        {
            if (!IsEditModeActive) return;
            if (editModeCamera == null) return;
            if (!cameraPresetTargetActive) return;

            if (isInDefaultView && savedCameraPoseValid)
            {
                // 메인씬 기본 시점 그대로 — orbit 계산 무시.
                cameraPresetTargetPosition = savedCameraPosition;
                cameraPresetTargetRotation = savedCameraRotation;
            }
            else
            {
                ComputeOrbitPose(out cameraPresetTargetPosition, out cameraPresetTargetRotation);
            }

            // exponential smoothing: 99% 도달까지 ~CameraPresetLerpTimeConstant×4.6 초.
            var t = 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.0001f, CameraPresetLerpTimeConstant));
            var tr = editModeCamera.transform;
            tr.position = Vector3.Lerp(tr.position, cameraPresetTargetPosition, t);
            tr.rotation = Quaternion.Slerp(tr.rotation, cameraPresetTargetRotation, t);
        }

        // orbit (yaw/pitch/distance) → world (position/rotation). orbitCenter (방 중앙 또는 벽 중앙)
        // 기준점으로 구면좌표계 → 직교좌표 변환. 카메라는 항상 orbitCenter 를 lookAt.
        // yaw 0° = -Z 측, 90° = +X, 180° = +Z, 270° = -X. pitch 0° = 수평, 89° ≈ 직각 위에서.
        private void ComputeOrbitPose(out Vector3 position, out Quaternion rotation)
        {
            float yawRad = orbitYaw * Mathf.Deg2Rad;
            float pitchRad = orbitPitch * Mathf.Deg2Rad;
            float horizontal = orbitDistance * Mathf.Cos(pitchRad);
            float vertical   = orbitDistance * Mathf.Sin(pitchRad);

            var offset = new Vector3(
                horizontal * Mathf.Sin(yawRad),
                vertical,
                -horizontal * Mathf.Cos(yawRad));

            position = orbitCenter + offset;
            rotation = Quaternion.LookRotation(orbitCenter - position, Vector3.up);
        }

        // 방 중앙 — Default / TopDown 의 orbit 중심.
        private Vector3 GetRoomCenter()
        {
            return new Vector3(
                (playableMinX + playableMaxX) * 0.5f,
                1.0f,
                (playableMinZ + playableMaxZ) * 0.5f);
        }

        // 벽면 포커스 view 의 orbit 중심 — 해당 벽의 안쪽 면 중앙. distance 가 곧 카메라-벽 거리가 됨.
        // y=1.4m: 룸 천장 ~2.8m 의 절반 (벽 중앙 높이). x/z 는 벽 위치.
        private Vector3 GetWallFocusCenter(CameraPreset face)
        {
            const float wallMidY = WallFocusHeight;
            float centerX = (playableMinX + playableMaxX) * 0.5f;
            float centerZ = (playableMinZ + playableMaxZ) * 0.5f;
            switch (face)
            {
                case CameraPreset.Front:      return new Vector3(centerX, wallMidY, playableMaxZ);  // 책장 뒷벽
                case CameraPreset.Back:       return new Vector3(centerX, wallMidY, playableMinZ);  // 정면 open 측 (가상 벽)
                case CameraPreset.LeftAngle:  return new Vector3(playableMinX, wallMidY, centerZ);  // 좌측 벽
                case CameraPreset.RightAngle: return new Vector3(playableMaxX, wallMidY, centerZ);  // 우측 벽
                default:                      return GetRoomCenter();
            }
        }

        // 벽 dimensions (width × height) — wall focus framing 계산용.
        // BookshelfGenerator 가 만든 룸 높이 (~2.8m) 와 playable footprint span 으로 계산.
        // 향후 RoomThemeApplier.RoomRoot 에서 wall renderer.bounds 직접 query 도 가능하지만 일단 footprint 기반.
        private const float RoomCeilingHeight = 2.8f;
        private const float WallFocusHeight = RoomCeilingHeight * 0.5f;
        // 벽이 화면 가장자리 살짝 안쪽에 위치하도록 — 너무 1.0 (꽉 차게) 이면 잘릴 수 있고,
        // 1.10 (10% 여유) 정도면 편집 시 손가락 잘림 없이 자연스러움.
        private const float WallFocusFramingPadding = 1.10f;

        private Vector2 GetWallDimensions(CameraPreset face)
        {
            switch (face)
            {
                case CameraPreset.Front:
                case CameraPreset.Back:
                    return new Vector2(playableMaxX - playableMinX, RoomCeilingHeight);
                case CameraPreset.LeftAngle:
                case CameraPreset.RightAngle:
                    return new Vector2(playableMaxZ - playableMinZ, RoomCeilingHeight);
                default:
                    return new Vector2(0f, 0f);
            }
        }

        // 벽 bounds + camera FOV + aspect ratio 로 "벽 전체가 화면에 들어오는" 거리 계산.
        // vertical-fit: d ≥ H * pad / (2·tan(FOV_y/2))
        // horizontal-fit: d ≥ W * pad / (2·tan(FOV_y/2)·aspect)
        // → 두 distance 의 max 를 사용해야 좌우/상하 둘 다 잘리지 않음. 결과는 [Min, Max]OrbitDistance 로 클램프.
        private float ComputeWallFocusDistance(CameraPreset face)
        {
            var dims = GetWallDimensions(face);
            float wallWidth = dims.x;
            float wallHeight = dims.y;
            if (wallWidth <= 0f || wallHeight <= 0f)
            {
                return Mathf.Clamp(2.5f, MinOrbitDistance, MaxOrbitDistance);
            }

            // FOV/aspect — 카메라 미초기화 (BeginCameraViewportEdit 전 호출 등) 케이스 fallback.
            float fovY = (editModeCamera != null && editModeCamera.fieldOfView > 0f)
                ? editModeCamera.fieldOfView
                : 52f;
            float aspect = (editModeCamera != null && editModeCamera.aspect > 0f)
                ? editModeCamera.aspect
                : (16f / 9f);

            float halfFovY = fovY * 0.5f * Mathf.Deg2Rad;
            float tanHalf = Mathf.Tan(halfFovY);
            if (tanHalf < 1e-4f)
            {
                return Mathf.Clamp(2.5f, MinOrbitDistance, MaxOrbitDistance);
            }

            float distVertical = (wallHeight * WallFocusFramingPadding) / (2f * tanHalf);
            float distHorizontal = (wallWidth * WallFocusFramingPadding) / (2f * tanHalf * aspect);
            float chosen = Mathf.Max(distVertical, distHorizontal);
            return Mathf.Clamp(chosen, MinOrbitDistance, MaxOrbitDistance);
        }

        // orbit yaw/pitch → wall fade 카테고리. pitch ≥ 65° 면 TopDown,
        // 그 외에는 yaw quadrant 로 분류 (45°/135°/225°/315° 경계).
        // 매핑: 카메라 위치 yaw → 사용자가 보는 쪽 = preset 의미.
        //   yaw 0° (-Z 측) → Front (앞 벽 focus, fade Front)
        //   yaw 90° (+X 측, 좌측 벽을 봄) → LeftAngle (= "좌" 의미, fade Right)
        //   yaw 180° (+Z 측) → Back (fade Back)
        //   yaw 270° (-X 측, 우측 벽을 봄) → RightAngle (= "우" 의미, fade Left)
        private CameraPreset ClassifyByOrbit()
        {
            if (orbitPitch >= 65f) return CameraPreset.TopDown;
            float yawNorm = ((orbitYaw % 360f) + 360f) % 360f;
            if (yawNorm < 45f || yawNorm >= 315f) return CameraPreset.Front;
            if (yawNorm < 135f) return CameraPreset.LeftAngle;   // +X 측 → 좌측 벽 focus
            if (yawNorm < 225f) return CameraPreset.Back;
            return CameraPreset.RightAngle;                       // -X 측 → 우측 벽 focus
        }

        // 현재 view 상태 → wall fade 적용. isInDefaultView=true 면 무조건 Default(=fade 없음).
        // A/D 회전 중에는 ClassifyByOrbit 로 quadrant 가 바뀔 때만 ApplyWallVisibilityForPreset 재호출
        // (불필요한 Material instance 재생성 회피).
        private void ApplyWallVisibilityForCurrentView(string contextTag)
        {
            var target = isInDefaultView ? CameraPreset.Default : currentCameraPreset;
            if (target == lastAppliedFadePreset) return;
            ApplyWallVisibilityForPreset(target);
            lastAppliedFadePreset = target;
            Debug.Log($"[RoomEdit][WallFade] view 갱신 (context={contextTag}) → {target}");
        }

        // ---------- Edit-mode wall occlusion fade ----------
        //
        // 진입 시 한 번 collect, 프리셋 바뀔 때마다 ApplyWallVisibilityForPreset, 종료 시
        // RestoreWallVisibility. Material 은 instance(.material) 로 변경하고 종료 시
        // sharedMaterial 원본 reference 로 되돌리면서 instance 는 Destroy → 메모리 누수 방지.
        // Collider 는 절대 건드리지 않음 → 기존 충돌/배치 검증 영향 없음.
        private void CollectWallRenderers()
        {
            if (wallVisibilityCollected) return;
            wallRendererCaches.Clear();

            // RoomThemeApplier 가 가진 RoomRoot 를 우선 사용 — 절차적으로 빌드된 룸의
            // 정확한 부모. 못 찾으면 활성 씬 전체 root scan 으로 fallback.
            if (themeApplier == null) themeApplier = FindFirstObjectByType<RoomThemeApplier>();
            Transform roomRoot = themeApplier != null ? themeApplier.RoomRoot : null;

            Renderer[] renderers;
            if (roomRoot != null)
            {
                renderers = roomRoot.GetComponentsInChildren<Renderer>(true);
            }
            else
            {
                var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                var list = new List<Renderer>();
                for (int i = 0; i < roots.Length; i++)
                {
                    list.AddRange(roots[i].GetComponentsInChildren<Renderer>(true));
                }
                renderers = list.ToArray();
            }

            int wallCount = 0, ceilingCount = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                if (!TryClassifyWallName(r.gameObject.name, out var group)) continue;

                wallRendererCaches.Add(new WallRendererCache
                {
                    Renderer = r,
                    InstanceMaterial = null,
                    SharedOriginal = r.sharedMaterial,
                    RendererWasEnabled = r.enabled,
                    Group = group,
                });
                if (group == WallGroup.Ceiling) ceilingCount++;
                else wallCount++;
            }
            wallVisibilityCollected = true;

            Debug.Log($"[RoomEdit][WallFade] 수집 완료 — wall={wallCount} ceiling={ceilingCount} total={wallRendererCaches.Count} (roomRoot={(roomRoot != null ? roomRoot.name : "<scene-scan>")})");
        }

        private static bool TryClassifyWallName(string name, out WallGroup group)
        {
            group = default;
            if (string.IsNullOrEmpty(name)) return false;
            if (name.Equals("LeftWall", StringComparison.OrdinalIgnoreCase)) { group = WallGroup.Left; return true; }
            if (name.Equals("RightWall", StringComparison.OrdinalIgnoreCase)) { group = WallGroup.Right; return true; }
            if (name.StartsWith("BackWall", StringComparison.OrdinalIgnoreCase)) { group = WallGroup.Back; return true; }
            if (name.Equals("FrontWall", StringComparison.OrdinalIgnoreCase)) { group = WallGroup.Front; return true; }
            if (name.Equals("Ceiling", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("AlcoveCeiling", StringComparison.OrdinalIgnoreCase))
            {
                group = WallGroup.Ceiling;
                return true;
            }
            return false;
        }

        // 프리셋별 어떤 벽 그룹을 fade 시킬지 — hard-coded 매핑이 dot-product 동적 결정보다
        // 예측 가능하고 디버깅하기 쉬워서 의도적으로 단순화. preset 추가 시 여기만 손대면 됨.
        //
        // **정책**: "카메라쪽(opposite-of-focus) 벽 fade" — wall-focus preset 에서 카메라는 방
        // 안쪽으로 들어와 focus 벽(=orbitCenter 가 놓인 벽)을 정면으로 본다. focus 벽은 사용자가
        // deco 를 보려는 대상이라 절대 fade 금지 (이전 회귀 — LeftAngle 에서 LeftWall 이 fade 되어
        // 사용자가 보려던 벽이 alpha=0.18 로 흐려졌던 버그). 대신 카메라 반대편(=카메라 뒤편)
        // 벽 1장을 fade 해서 Animal Crossing dollhouse 식 "그 면을 봤을 때 반대측 벽이 사라지는" UX 유지.
        //
        // 매핑 표 (line 5003-5006 yaw + line 5711-5714 orbitCenter 기반, 카메라가 방 내부에 위치):
        //   preset       | focus (orbitCenter)         | 카메라 offset 방향  | 카메라쪽 wall → fade
        //   Front        | BackWall  (+Z, 책장)         | -Z                 | FrontWall (-Z, 가상 벽 — prefab 렌더러 없음 → 사실상 noop)
        //   Back         | FrontWall (-Z, 가상)         | +Z                 | BackWall  (+Z, strip 3개)
        //   LeftAngle    | LeftWall  (-X)               | +X                 | RightWall (+X)
        //   RightAngle   | RightWall (+X)               | -X                 | LeftWall  (-X)
        private static bool ShouldFadeWall(WallGroup group, CameraPreset preset)
        {
            switch (preset)
            {
                case CameraPreset.Default:
                    return false; // 메인뷰 사선 시점 — fade 없음.
                case CameraPreset.TopDown:
                    return true;  // 모든 벽 + 천장 fade — 위에서 내려보면 둘 다 시야 차단.
                case CameraPreset.Front:
                    // focus=BackWall(책장 정면). 카메라쪽=FrontWall(가상 — 매칭 렌더러 없어 noop).
                    return group == WallGroup.Front;
                case CameraPreset.Back:
                    // focus=FrontWall(가상). 카메라쪽=BackWall(strip 3개) — 카메라 뒤편 fade.
                    return group == WallGroup.Back;
                case CameraPreset.LeftAngle:
                    // focus=LeftWall(정면 시야 — opaque 유지 필수). 카메라쪽=RightWall(+X).
                    return group == WallGroup.Right;
                case CameraPreset.RightAngle:
                    // focus=RightWall(정면 시야 — opaque 유지 필수). 카메라쪽=LeftWall(-X).
                    return group == WallGroup.Left;
                default:
                    return false;
            }
        }

        private void ApplyWallVisibilityForPreset(CameraPreset preset)
        {
            if (!wallVisibilityCollected) return;

            int faded = 0, restored = 0;
            // 매핑 검증 로그용 — fade 적용된 실제 GameObject 이름을 모아서 ShouldFadeWall 의 표대로
            // 동작하는지 화면 없이도 한눈에 확인. 회귀 시 어느 벽이 잘못 fade 됐는지 즉시 식별.
            var fadedNames = new List<string>(wallRendererCaches.Count);
            for (int i = 0; i < wallRendererCaches.Count; i++)
            {
                var cache = wallRendererCaches[i];
                if (cache.Renderer == null) continue;
                bool shouldFade = ShouldFadeWall(cache.Group, preset);
                ApplyWallFadeState(cache, shouldFade);
                if (shouldFade)
                {
                    faded++;
                    fadedNames.Add(cache.Renderer.gameObject.name);
                }
                else
                {
                    restored++;
                }
            }
            // target-group 라벨 — ShouldFadeWall 매핑 표의 expected fade 대상. 실제 names 와 대조해
            // mismatch (예: LeftAngle 인데 LeftWall 이 fade 됐다 = focus 벽 fade 회귀) 즉시 검출.
            string targetGroupLabel = preset switch
            {
                CameraPreset.Default    => "(없음)",
                CameraPreset.TopDown    => "All+Ceiling",
                CameraPreset.Front      => "Front (카메라쪽, 가상)",
                CameraPreset.Back       => "Back (카메라쪽)",
                CameraPreset.LeftAngle  => "Right (카메라쪽)",
                CameraPreset.RightAngle => "Left (카메라쪽)",
                _ => "?",
            };
            string fadedNamesJoined = fadedNames.Count == 0 ? "(없음)" : string.Join(",", fadedNames);
            Debug.Log(
                $"[RoomEdit][WallFade] preset={preset} target-group={targetGroupLabel} " +
                $"faded={faded} names=[{fadedNamesJoined}] kept-opaque={restored} " +
                $"alpha={Mathf.Clamp01(wallFadeAlpha):0.00}");
        }

        // shouldFade=true 면 instance material 만들어서 transparent + alpha 적용,
        // false 면 sharedMaterial 원본으로 되돌리고 instance 정리.
        private void ApplyWallFadeState(WallRendererCache cache, bool shouldFade)
        {
            if (cache.Renderer == null) return;

            if (!shouldFade)
            {
                if (cache.InstanceMaterial != null)
                {
                    cache.Renderer.sharedMaterial = cache.SharedOriginal;
                    Destroy(cache.InstanceMaterial);
                    cache.InstanceMaterial = null;
                }
                if (cache.Renderer.enabled != cache.RendererWasEnabled)
                {
                    cache.Renderer.enabled = cache.RendererWasEnabled;
                }
                return;
            }

            // Renderer 가 OFF 상태였으면 일단 켜야 fade 가 보임.
            if (!cache.Renderer.enabled)
            {
                cache.Renderer.enabled = true;
            }

            if (cache.InstanceMaterial == null)
            {
                // .material 접근 시 Unity 가 자동으로 instance 생성 + assign. 이걸 우리가 쥐고 있다가
                // 종료 시 Destroy 한다.
                cache.InstanceMaterial = cache.Renderer.material;
                ConfigureMaterialAsTransparent(cache.InstanceMaterial);
            }

            // _BaseColor (URP) 우선, 없으면 _Color (Standard) 로 fallback.
            // [#3 Must Fix] 둘 다 없는 custom shader 면 alpha 적용이 silent fail → 벽이 그대로
            // occlusion. 이 경우 통째 숨김(enabled=false) 으로 fallback 해서 사용자 시야는 보장.
            // 종료 시 RendererWasEnabled 로 복구되므로 디자이너가 원본을 바꿀 일은 없음.
            // 디자이너가 인스펙터에서 0~1 밖 값을 입력하더라도 안전하도록 매 적용 시 클램프.
            var alpha = Mathf.Clamp01(wallFadeAlpha);
            if (cache.InstanceMaterial.HasProperty("_BaseColor"))
            {
                var c = cache.InstanceMaterial.GetColor("_BaseColor");
                c.a = alpha;
                cache.InstanceMaterial.SetColor("_BaseColor", c);
            }
            else if (cache.InstanceMaterial.HasProperty("_Color"))
            {
                var c = cache.InstanceMaterial.GetColor("_Color");
                c.a = alpha;
                cache.InstanceMaterial.SetColor("_Color", c);
            }
            else
            {
                Debug.LogWarning(
                    $"[RoomEdit][WallFade] '{cache.Renderer.gameObject.name}' material 에 _BaseColor/_Color 둘 다 없음 " +
                    $"(shader={cache.InstanceMaterial.shader?.name}). alpha fade 불가 → Renderer 통째 숨김 fallback.");
                cache.Renderer.enabled = false;
            }
        }

        // URP Lit/Standard 호환 — opaque material 의 surface mode 와 blend state 를 transparent
        // 로 전환. Shader 의 _Surface keyword 가 _SURFACE_TYPE_TRANSPARENT 일 때만 알파가 보이므로
        // keyword 토글이 핵심. 이 작업은 instance material 에만 적용 → shared 원본 보존.
        private static void ConfigureMaterialAsTransparent(Material material)
        {
            if (material == null) return;
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
        }

        private void RestoreWallVisibility(string contextTag = "exit")
        {
            if (!wallVisibilityCollected) return;

            // 편집 모드가 아직 활성인 상태에서 호출되면 사용자가 곧장 시야를 잃는다 (이번 버그의 핵심).
            // exit/destroy 외 경로에서 호출되면 즉시 알 수 있게 warning. context tag 로 caller 식별.
            // ExitEditModeInternal 은 호출 직전에 IsEditModeActive=false 로 내리므로 정상 경로는 warning 안 나옴.
            // OnDisable 도 lifecycle teardown(컴포넌트 비활성/scene unload) 경로라 warning 제외.
            if (IsEditModeActive && contextTag != "OnDestroy" && contextTag != "OnDisable")
            {
                Debug.LogWarning(
                    $"[RoomEdit][WallFade] ⚠ RestoreWallVisibility 가 편집 모드 도중 호출됨 (context={contextTag}) — 의도된 호출이 아니라면 wall fade 가 풀려서 occlusion 이 돌아옴. " +
                    "Theme/customization 적용 직후라면 RefreshWallVisibilityAfterMaterialMutation 으로 대체할 것.");
            }

            int restored = 0, destroyed = 0;
            for (int i = 0; i < wallRendererCaches.Count; i++)
            {
                var cache = wallRendererCaches[i];
                if (cache.Renderer != null)
                {
                    if (cache.InstanceMaterial != null)
                    {
                        cache.Renderer.sharedMaterial = cache.SharedOriginal;
                    }
                    cache.Renderer.enabled = cache.RendererWasEnabled;
                    restored++;
                }
                if (cache.InstanceMaterial != null)
                {
                    Destroy(cache.InstanceMaterial);
                    cache.InstanceMaterial = null;
                    destroyed++;
                }
            }
            Debug.Log($"[RoomEdit][WallFade] 복구 완료 (context={contextTag}) — restored={restored}/{wallRendererCaches.Count} destroyed-instances={destroyed}");
            wallRendererCaches.Clear();
            wallVisibilityCollected = false;
        }

        // 테마 / customization 적용 직후 호출. 외부에서 renderer.sharedMaterial 이 실제로 swap 된
        // 경우에만 SharedOriginal 을 재스냅샷한다 — fade instance 가 그대로 sharedMaterial 자리에
        // 있는데 destroy + 그 destroyed reference 를 SharedOriginal 로 저장하던 [P1 #4] 회귀 가드.
        //
        // 동작:
        //   - 편집 모드가 아니면 noop
        //   - cache 가 아직 안 만들어졌으면 collect
        //   - dangling Renderer 가 있으면 [#1] 전체 재수집 (룸 재생성/씬 reload 가드)
        //   - 각 cache 별로:
        //       (a) InstanceMaterial 이 sharedMaterial 자리에 그대로면 → 외부 swap 없었음.
        //           벽 fade 가 살아있으니 SharedOriginal/InstanceMaterial 모두 변경하지 말 것.
        //       (b) sharedMaterial 이 우리 instance 가 아니면 → 외부에서 swap 됨.
        //           orphan instance 정리 + 새 sharedMaterial 을 SharedOriginal 로 재스냅샷.
        //   - RendererWasEnabled 는 fade 가 임시로 켰을 수 있으니 absolutely 절대 갱신하지 않는다 — 진입 시 수집 값 유지.
        //   - 끝에 ApplyWallVisibilityForPreset 으로 현재 프리셋 fade 재적용.
        private void RefreshWallVisibilityAfterMaterialMutation(string contextTag)
        {
            if (!IsEditModeActive)
            {
                return;
            }
            if (!wallVisibilityCollected)
            {
                CollectWallRenderers();
            }
            else
            {
                // [#1 Must Fix] dangling renderer 감지 — Unity null pattern (destroyed) 인 cache 가
                // 하나라도 있으면 룸이 재생성됐을 가능성 있음. 부분 갱신 시도하지 말고 전체 재수집.
                bool hasDangling = false;
                for (int i = 0; i < wallRendererCaches.Count; i++)
                {
                    if (wallRendererCaches[i].Renderer == null) { hasDangling = true; break; }
                }
                if (hasDangling)
                {
                    Debug.LogWarning(
                        $"[RoomEdit][WallFade] dangling renderer 감지 (context={contextTag}) — 룸 재생성 가능성. " +
                        "cache 전체 재수집.");
                    // 살아있는 instance material 정리.
                    for (int i = 0; i < wallRendererCaches.Count; i++)
                    {
                        var c = wallRendererCaches[i];
                        if (c.InstanceMaterial != null)
                        {
                            Destroy(c.InstanceMaterial);
                            c.InstanceMaterial = null;
                        }
                    }
                    wallRendererCaches.Clear();
                    wallVisibilityCollected = false;
                    CollectWallRenderers();
                }
                else
                {
                    int externalSwapDetected = 0, fadeKept = 0;
                    for (int i = 0; i < wallRendererCaches.Count; i++)
                    {
                        var cache = wallRendererCaches[i];
                        if (cache.Renderer == null) continue;

                        // [P1 #4] sharedMaterial 이 우리 instance 자리에 그대로 있으면 → 외부 swap 없었음.
                        // 예: ApplyEntryVisual/Floor 처럼 bookshelf/floor 만 swap 한 경우 벽 material 은 무손상.
                        // 이 cache 는 손대지 않는다 (fade 유지). 그렇지 않으면 destroyed reference 를
                        // SharedOriginal 로 저장하는 회귀 발생 [P1 #4].
                        bool fadeInstanceStillInstalled =
                            cache.InstanceMaterial != null &&
                            ReferenceEquals(cache.Renderer.sharedMaterial, cache.InstanceMaterial);

                        if (fadeInstanceStillInstalled)
                        {
                            fadeKept++;
                            continue;
                        }

                        // 외부 swap 발생 (벽 카테고리 등). orphan 된 우리 instance 가 있으면 정리하고
                        // 새 sharedMaterial 을 새 원본으로 잡는다. 종료 시 이걸로 복구해야 사용자가
                        // 적용한 새 wallpaper 선택이 유지됨.
                        if (cache.InstanceMaterial != null)
                        {
                            Destroy(cache.InstanceMaterial);
                            cache.InstanceMaterial = null;
                        }
                        cache.SharedOriginal = cache.Renderer.sharedMaterial;
                        // RendererWasEnabled 는 의도적으로 갱신하지 않음 — fade 가 임시로 enabled=true
                        // 로 켜놨다면 외부 swap 직후의 값이 진실이 아니다. CollectWallRenderers 시점의
                        // 원본 enabled 가 종료 시 복구의 기준.
                        externalSwapDetected++;
                    }
                    Debug.Log(
                        $"[RoomEdit][WallFade] cache 재스냅샷 (context={contextTag}) — " +
                        $"외부 swap 감지 {externalSwapDetected}, fade 유지 {fadeKept} / total {wallRendererCaches.Count}");
                }
            }
            // theme/customization swap 직후 — 같은 preset 이라도 새 sharedMaterial 기준으로 fade
            // 다시 만들어야 하므로 ApplyWallVisibilityForCurrentView 의 변경 감지 건너뛰고 직접 호출.
            var fadeTarget = isInDefaultView ? CameraPreset.Default : currentCameraPreset;
            ApplyWallVisibilityForPreset(fadeTarget);
            lastAppliedFadePreset = fadeTarget;
            Debug.Log($"[RoomEdit][WallFade] preset 재적용 (context={contextTag}) — preset={fadeTarget}");
        }

        // Toolbar ------------------------------------------------------------------

        private void OnToolbarButtonClicked(string key)
        {
            if (key == "SHOP")
            {
                ShowReadyModal();
                return;
            }
            selectedToolbarKey = key;
            ApplyToolbarSelectionVisuals();
            UpdateFurnitureSubToolbarVisibility();
            RebuildInventorySlots();
        }

        private void ApplyToolbarSelectionVisuals()
        {
            foreach (var pair in toolbarBackgrounds)
            {
                var isSelected = pair.Key == selectedToolbarKey && pair.Key != "SHOP";
                pair.Value.color = isSelected ? PrimaryColor : SecondaryColor;
                if (toolbarLabels.TryGetValue(pair.Key, out var label) && label != null)
                {
                    label.color = isSelected ? Color.white : TextColor;
                }
            }
        }

        // Ready modal --------------------------------------------------------------

        private void ShowReadyModal()
        {
            if (readyModalRoot != null)
            {
                readyModalRoot.transform.SetAsLastSibling();
                readyModalRoot.SetActive(true);
            }
        }

        private void HideReadyModal()
        {
            if (readyModalRoot != null)
            {
                readyModalRoot.SetActive(false);
            }
        }

        private void ShowThemeApplyModeModal(RoomThemeAsset theme)
        {
            if (theme == null)
            {
                return;
            }

            pendingThemeApply = theme;
            if (themeApplyModeModalRoot == null)
            {
                ApplyThemeSelected(theme, ThemeApplyMode.Reset);
                return;
            }

            themeApplyModeModalRoot.transform.SetAsLastSibling();
            themeApplyModeModalRoot.SetActive(true);
        }

        private void HideThemeApplyModeModal()
        {
            pendingThemeApply = null;
            if (themeApplyModeModalRoot != null)
            {
                themeApplyModeModalRoot.SetActive(false);
            }
        }

        private void ConfirmThemeApplyMode(ThemeApplyMode mode)
        {
            var theme = pendingThemeApply;
            HideThemeApplyModeModal();
            ApplyThemeSelected(theme, mode);
        }

        private bool IsCurrentThemeCustomizationLocked()
        {
            if (themeApplier == null)
            {
                themeApplier = FindFirstObjectByType<RoomThemeApplier>();
            }
            var current = themeApplier?.CurrentTheme;
            return current != null && !current.AllowsCustomization;
        }

        private void ShowCustomizationLockedModal(CustomizationCategory category, CustomizationEntry entry)
        {
            pendingCustomizationCategory = category;
            pendingCustomizationEntry = entry;

            if (customizationLockedModalRoot == null)
            {
                ProceedCustomizationEntry(category, entry);
                return;
            }

            if (customizationLockedBodyText != null)
            {
                var current = themeApplier?.CurrentTheme;
                string themeName = current != null && !string.IsNullOrEmpty(current.DisplayName)
                    ? current.DisplayName
                    : "현재";
                customizationLockedBodyText.text =
                    $"'{themeName}' 테마는 커스터마이징을 허용하지 않습니다.\n진행하면 테마가 해제되고 적용된 커스터마이징이 초기화됩니다.";
            }

            customizationLockedModalRoot.transform.SetAsLastSibling();
            customizationLockedModalRoot.SetActive(true);
        }

        private void HideCustomizationLockedModal()
        {
            pendingCustomizationCategory = default;
            pendingCustomizationEntry = null;
            if (customizationLockedModalRoot != null)
            {
                customizationLockedModalRoot.SetActive(false);
            }
        }

        private void ConfirmCustomizationLockedProceed()
        {
            var category = pendingCustomizationCategory;
            var entry = pendingCustomizationEntry;
            HideCustomizationLockedModal();
            if (entry == null)
            {
                return;
            }

            // [Review #104 P1] visual swap 전에 클라이언트 테마 상태를 unlocked default로 reset.
            // 이렇게 안 하면 PATCH 스킵(spaceSettingsRepo == null) / 네트워크 실패 / 서버가
            // themeReset=false 반환 등의 경로에서 'locked CurrentTheme + 새 customization visual'
            // 혼합 상태가 남아 다음 카테고리 클릭에서 모달이 또 노출됨.
            //
            // 사용자는 모달의 '계속'으로 '테마가 해제된다'에 이미 동의했으므로 클라이언트는 즉시
            // 그 약속한 상태로 전환. PATCH 성공 시 themeReset=true → ReloadRoomState가 서버 권위
            // 상태로 다시 동기화. PATCH 실패 시 클라이언트는 unlocked 상태로 남고 다음 reload에서
            // 서버 상태에 따라 정정됨.
            ResetThemeToDefaultLocally();
            ApplyThemeResetVisualFallback();

            ProceedCustomizationEntry(category, entry);
        }

        // 테마 카탈로그의 DefaultTheme(있으면)을 즉시 RoomThemeApplier에 Apply. 클라이언트 측
        // CurrentTheme/AllowsCustomization 상태만 갱신 — 서버 PATCH는 별개 흐름.
        private void ResetThemeToDefaultLocally()
        {
            if (themeApplier == null) themeApplier = FindFirstObjectByType<RoomThemeApplier>();
            if (themeApplier == null || themeCatalog == null) return;
            var defaultTheme = themeCatalog.DefaultTheme;
            if (defaultTheme != null && defaultTheme != themeApplier.CurrentTheme)
            {
                themeApplier.Apply(defaultTheme);
                RefreshWallVisibilityAfterMaterialMutation("ResetThemeToDefaultLocally");
            }
        }

        // Inventory placement ------------------------------------------------------

        private void PlaceInventoryEntry(InventoryEntry entry)
        {
            if (!IsEditModeActive)
            {
                return;
            }
            if (entry.Prefab == null)
            {
                return;
            }

            // 1회 배치 제한 — 동일 자산이 이미 씬에 살아있으면 새로 만들지 않는다.
            // 슬롯은 이미 disabled 처리되지만 race(첫 클릭 ~ Rebuild 사이) 방어로 여기서도 한 번 더 차단.
            // WallDeco/BookshelfDeco 분기보다 먼저 검사 — PlaceWallDecoEntry 가 향후 inventoryKey
            // 시스템에 합류해도 별도 수정 없이 1회 제한이 자동 적용되도록.
            var inventoryKey = BuildInventoryKey(entry);
            if (IsInventoryKeyPlaced(inventoryKey))
            {
                Debug.Log($"[RoomEdit][Inventory] 배치 차단 — '{entry.DisplayName}' (key={inventoryKey}) 는 이미 씬에 1개 배치되어 있습니다.");
                return;
            }

            // placement category 분기. WallDeco 는 wall surface 부착 placement 로 별도 흐름.
            // BookshelfDeco 는 후속 (책장 섹션 anchoring 시스템 합류 시점). 그 외 (FloorDeco/
            // 미지정 default) 는 기존 floor placement 유지.
            switch (entry.PlacementCategory)
            {
                case CustomizationCategory.WallDeco:
                    PlaceWallDecoEntry(entry);
                    return;
                case CustomizationCategory.BookshelfDeco:
                    Debug.LogWarning($"[RoomEditModeController] BookshelfDeco placement 미지원 — '{entry.DisplayName}' skip. 후속 PR.");
                    return;
            }

            EnsurePlacedItemsRoot();

            var go = Instantiate(entry.Prefab);
            go.name = $"Decoration_{SanitizeName(entry.DisplayName)}_{System.Guid.NewGuid():N}";
            go.transform.SetParent(placedItemsRoot.transform, true);

            // Step 1 — uniform-scale the model so its measured bounds match a per-Kind
            // target along the right axis. lookup chain (highest priority first):
            //   (a) entry.ScaleMultiplierOverride * (Kind 표준 크기) — designer override
            //   (b) Kind != None → DecorationSizeProfileTable lookup (target + axis)
            //   (c) legacy CanonicalScaleByAssetFileName 정확매칭 dict (점진 폐기)
            //   (d) 폴더 휴리스틱 (/Props/ 0.5m, /Furniture/ 1.2m) — backward compatible
            // 등방 multiplier (모든 축에 동일 적용) 라 비율은 항상 보존된다.
            NormalizeSpawnedScale(go, entry);
            // 회귀 fix — transform.localScale 변경 직후 PhysX 의 collider transform 은 stale.
            // 다음 step 의 TryFindClearSpawnPosition 이 Physics.OverlapBox 로 검사하므로 명시 sync
            // 안 하면 self collider 가 새 scale 위치에 없는 상태로 hit 결과 부정확.
            Physics.SyncTransforms();

            // Step 2 — initial X/Z layout: spread placements in a 3-wide grid
            // centered on the open floor area so multiple drops don't stack.
            var existingCount = placedItemsRoot.transform.childCount - 1;
            var lateral = (existingCount % 3 - 1) * InventorySpawnSpacing;
            var depth = (existingCount / 3) * InventorySpawnSpacing;

            // Step 3 — bounds-based vertical alignment: drop so the model's lowest
            // renderer point rests on the floor (y = 0) regardless of where the
            // source prefab placed its pivot.
            var rendererBounds = ComputeRendererBounds(go);
            var verticalOffset = rendererBounds.HasValue ? -rendererBounds.Value.min.y : 0f;
            var spawnPosition = InventoryPlacementCenter + new Vector3(lateral, verticalOffset, depth);
            go.transform.position = spawnPosition;

            // Step 3b — clamp the initial spawn position into the room footprint
            // and keep the auto-stagger from sliding into the bookshelf
            // footprint when the inventory has 10+ items. Drag-time movement
            // doesn't use InventorySpawnMaxZ — the player can later drag an
            // item up to the bookshelf via the collider-based blocker check.
            // 회귀 fix — clamp/spawn-overlap 검사 모두 ComputeFloorFootprintBounds (preview source)
            // 사용. 이전엔 ComputeRendererBounds 라 책상 위 모니터 같은 상부 mesh 가 spawn 검사
            // 영역에 포함돼 책상 옆 좁은 통로 spawn 이 false-positive blocker 로 reject 됐다.
            if (rendererBounds.HasValue)
            {
                var spawnBounds = ComputeFloorFootprintBounds(go) ?? rendererBounds.Value;
                var pos = go.transform.position;
                if (pos.z + spawnBounds.extents.z > InventorySpawnMaxZ)
                {
                    pos.z = InventorySpawnMaxZ - spawnBounds.extents.z;
                    go.transform.position = pos;
                }

                go.transform.position = ClampToFloorBounds(go.transform.position, go.transform.position, spawnBounds);
            }

            // Step 3c — reject spawn positions that overlap an existing
            // blocker (wall, fixture, previously placed decoration). Without
            // this the auto-stagger can drop the new item directly inside
            // the bookshelf or another decoration; later drag-time checks
            // would then treat that overlap as "already overlapping" and
            // continue letting the player tunnel through the same blocker.
            // Search a few minor-grid rings around the staggered position
            // for a clear cell; if none is available, abort the spawn.
            if (rendererBounds.HasValue)
            {
                var spawnBounds = ComputeFloorFootprintBounds(go) ?? rendererBounds.Value;
                // 회귀 진단 — Kind 정규화 후 size 와 footprint 가 어떻게 나왔는지 spawn 시점에 한 번 출력.
                Debug.Log(
                    $"[EditPlacement] spawn obj='{entry.DisplayName}' kind={entry.Kind} " +
                    $"placement={entry.PlacementCategory} scale={go.transform.localScale} " +
                    $"boundsSize={spawnBounds.size} footprintXZ=({spawnBounds.size.x:0.##},{spawnBounds.size.z:0.##})");
                if (!TryFindClearSpawnPosition(go, spawnBounds, go.transform.position, out var clearPosition))
                {
                    Debug.LogWarning(
                        $"[EditPlacement] spawn rejected obj='{entry.DisplayName}' reason='NoClearRing' " +
                        $"footprintXZ=({spawnBounds.size.x:0.##},{spawnBounds.size.z:0.##}) " +
                        $"desiredPos={go.transform.position}");
                    Destroy(go);
                    return;
                }
                go.transform.position = clearPosition;
            }

            // Step 4 — collider for raycast selection. .glb / .fbx imports don't
            // ship with one, so we synthesize a BoxCollider matching the post-
            // scale renderer bounds. Skipped if the prefab already brings a
            // collider chain.
            if (rendererBounds.HasValue && go.GetComponentInChildren<Collider>() == null)
            {
                var refreshedBounds = ComputeRendererBounds(go) ?? rendererBounds.Value;
                var collider = go.AddComponent<BoxCollider>();
                var localCenter = go.transform.InverseTransformPoint(refreshedBounds.center);
                var localSize = go.transform.InverseTransformVector(refreshedBounds.size);
                collider.center = localCenter;
                collider.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
            }

            var marker = go.AddComponent<DecorationItemMarker>();
            marker.InventoryKey = inventoryKey;

            if (!string.IsNullOrEmpty(inventoryKey))
            {
                placedByInventoryKey[inventoryKey] = go;
                Debug.Log($"[RoomEdit][Inventory] 배치 — '{entry.DisplayName}' key={inventoryKey} pos={go.transform.position}");
            }

            // 인벤토리 슬롯이 비활성화된 상태로 즉시 보이도록 슬롯 다시 빌드.
            RebuildInventorySlots();
            SetDirty("PlaceInventoryEntry");

            // MR #122 — 신규 spawn 은 곧바로 pending 상태로 진입. 사용자가 "배치 확정" 누를
            // 때까지 반투명 ghost. 자동 선택해서 드래그/회전/삭제/확정 toolbar 가 즉시 보이도록.
            BeginPendingPlacement(go);
            SelectObject(go);
        }

        // Search outward from `desiredPosition` in MinorGridStep rings for a
        // position whose spawn-bounds-shaped OverlapBox doesn't collide with
        // any existing blocker. Returns true with the first clear position;
        // false if every ring up to MaxSpawnSearchRings is blocked, in which
        // case the caller should refuse the spawn entirely. The candidate
        // position is also re-checked against ClampToFloorBounds and
        // InventorySpawnMaxZ so we don't relocate the item outside the
        // playable area or into the bookshelf alcove.
        private bool TryFindClearSpawnPosition(GameObject self, Bounds spawnBounds, Vector3 desiredPosition, out Vector3 result)
        {
            const int MaxSpawnSearchRings = 20;

            if (!IsSpawnPositionBlockedAt(self, spawnBounds, desiredPosition))
            {
                result = desiredPosition;
                return true;
            }

            for (var ring = 1; ring <= MaxSpawnSearchRings; ring++)
            {
                for (var ox = -ring; ox <= ring; ox++)
                {
                    for (var oz = -ring; oz <= ring; oz++)
                    {
                        // Walk the perimeter of the current ring only.
                        if (Mathf.Abs(ox) != ring && Mathf.Abs(oz) != ring) continue;

                        var candidate = desiredPosition + new Vector3(ox * MinorGridStep, 0f, oz * MinorGridStep);

                        // Stay inside the playable footprint and the spawn-Z
                        // cap. ClampToFloorBounds returns the same position
                        // when the candidate is already inside, otherwise it
                        // pushes it back in — we treat that "pushed back"
                        // case as out-of-bounds and skip.
                        var clamped = ClampToFloorBounds(candidate, candidate, spawnBounds);
                        if (Mathf.Abs(clamped.x - candidate.x) > 1e-4f || Mathf.Abs(clamped.z - candidate.z) > 1e-4f) continue;
                        if (clamped.z + spawnBounds.extents.z > InventorySpawnMaxZ) continue;

                        if (!IsSpawnPositionBlockedAt(self, spawnBounds, clamped))
                        {
                            result = clamped;
                            return true;
                        }
                    }
                }
            }

            result = desiredPosition;
            return false;
        }

        // floor 데코의 footprint 검사 시 Y 축 검사 두께 — 사실상 XZ 평면 검사로 만들어 "벽이나 천장
        // obstacle 과의 Y 겹침" 으로 invalid 판정되는 회귀를 차단한다. 0.30m 면 책상/책장 아래
        // 다리 부분은 hit, 벽은 isShell 이라 어차피 무시. 천장 시설은 검사면을 벗어나 hit 안 됨.
        private const float FloorPlacementProbeHalfHeight = 0.30f;

        // True if a box the size of `spawnBounds` placed at `position` (in
        // world space) overlaps any non-self, non-shell, non-trigger
        // collider. **호출 계약** — `spawnBounds` 는 ComputeFloorFootprintBounds 의 결과 (world
        // AABB, 시각 footprint preview 와 동일 source). orientation 은 항상 identity 로 OverlapBox
        // 호출 — 회전된 객체는 호출자가 ComputeFloorFootprintBounds 를 *회전 후* 다시 계산해서
        // 새 World AABB 를 전달한다. 이렇게 하면 preview 사각형과 검증 영역이 **항상 동일**.
        //
        // 이전 구현 회귀 — `Quaternion orientation` 인자를 받아 OverlapBox 에 전달했으나, 회전 시
        // World AABB 가 √2 배 부풀어 (45°) preview 와 어긋났다. minXZ heuristic 으로 deflate 했지만
        // 여전히 preview 와 size 불일치. 단순화: orientation 인자 폐지, identity 로 통일.
        private static bool IsSpawnPositionBlockedAt(GameObject self, Bounds spawnBounds, Vector3 position)
        {
            var halfExtents = new Vector3(
                Mathf.Max(0.005f, spawnBounds.extents.x),
                FloorPlacementProbeHalfHeight,
                Mathf.Max(0.005f, spawnBounds.extents.z));

            // spawnBounds.center is at the object's *current* world position.
            // Translate the center by the candidate-vs-current delta to query
            // the box at the candidate position. probe slab 의 Y 중심은 floor 위 probe height — 항상
            // floor 를 기준으로 Y 검사 (객체 자체 Y 와 무관해 floor 인접 obstacle 만 잡힘).
            var sampleCenter = new Vector3(
                spawnBounds.center.x + (position.x - self.transform.position.x),
                FloorPlaneY + FloorPlacementProbeHalfHeight,
                spawnBounds.center.z + (position.z - self.transform.position.z));
            var hits = Physics.OverlapBox(sampleCenter, halfExtents, Quaternion.identity, BlockerLayerMask, QueryTriggerInteraction.Ignore);
#if UNITY_EDITOR
            // 진단 — preview 와 동일 size 인지 확인. ComputeFloorFootprintBounds 결과 size 그대로
            // OverlapBox 에 전달되는지 frame-by-frame 검증.
            Debug.Log(
                $"[EditValidation] obj='{(self != null ? self.name : "<null>")}' validationCenterXZ=({sampleCenter.x:0.##},{sampleCenter.z:0.##}) " +
                $"validationSizeXZ=({halfExtents.x * 2f:0.##},{halfExtents.z * 2f:0.##}) hits={hits.Length}");
#endif
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit == null || hit.isTrigger) continue;

                // 강한 self filter — IsChildOf 외에 transform.root 와 attachedRigidbody 비교까지.
                // GLB 가 nested rig 갖고 있거나 designer 가 BoxCollider 를 자식 sub-mesh 에 붙인 경우도
                // 자기 자신은 절대 obstacle 로 잡히지 않게 한다.
                if (self != null)
                {
                    if (hit.transform == self.transform) continue;
                    if (hit.transform.IsChildOf(self.transform)) continue;
                    if (hit.transform.root == self.transform.root && self.transform == self.transform.root) continue;
                    if (hit.attachedRigidbody != null && hit.attachedRigidbody.gameObject == self) continue;
                }

                var isShell = false;
                for (var t = hit.transform; t != null; t = t.parent)
                {
                    if (RoomShellExactNames.Contains(t.name))
                    {
                        isShell = true;
                        break;
                    }
                }
                if (isShell) continue;

                // 진단 — 실제 차단을 만든 hit collider 를 출력. 사용자가 "충분해 보이는데" 라고 한
                // 케이스에서 어떤 obstacle 이 잡히는지 즉시 식별.
                Debug.Log(
                    $"[EditMove] hit blocker='{hit.gameObject.name}' collider='{hit.GetType().Name}' " +
                    $"hitPos={hit.bounds.center} hitSize={hit.bounds.size} " +
                    $"selfName='{(self != null ? self.name : "<null>")}' isSelf=False");
                return true;
            }

            return false;
        }

        // Uniform-scale newly spawned decoration so its largest XZ extent matches
        // the target meter size for its source folder. This keeps small props
        // (Props/) from appearing room-sized and large furniture (Furniture/)
        // from appearing miniature without per-model size metadata.
        //
        // If the asset has a canonical scale registered (matching the size
        // BookshelfGenerator uses for the same model in the scene), that
        // wins over the category heuristic so an inventory-spawned BookStack
        // ends up the same size as the BookStack already placed in the room.
        private static void NormalizeSpawnedScale(GameObject go, InventoryEntry entry)
        {
            // (a) Kind 가 알려져 있으면 표준 크기 + fit 축으로 등방 multiplier 산출, designer override 곱.
            //     Override 가 0 이 아니면 추가 multiplier 로 해석 (1.0=기본, 0.8=80%, 1.2=120%).
            if (entry.Kind != DecorationObjectKind.None &&
                DecorationSizeProfileTable.TryGet(entry.Kind, out var profile))
            {
                var bounds = ComputeRendererBounds(go);
                if (bounds.HasValue)
                {
                    var size = bounds.Value.size;
                    var measured = SelectFitAxisLength(size, profile.Axis);
                    if (measured > 0.0001f)
                    {
                        var baseMultiplier = profile.TargetMeters / measured;
                        var overrideMultiplier = entry.ScaleMultiplierOverride > 0f ? entry.ScaleMultiplierOverride : 1f;
                        var scaleMultiplier = Mathf.Clamp(baseMultiplier * overrideMultiplier, 0.05f, 20f);
                        go.transform.localScale *= scaleMultiplier;
                        return;
                    }
                }
                // bounds 측정 실패 — Kind 기반 정규화 못 함. 다음 fallback 으로 진행.
            }

            // (b) Legacy canonical 정확매칭 dict (book_stack 0.2 등) — Kind 시스템 마이그레이션 전
            //     기존 catalog 의 회귀 방지를 위해 유지. 모든 entry 가 Kind 를 채우면 자연스럽게 unused.
            if (TryGetCanonicalScaleByAssetPath(entry.SourceAssetPath, out var canonical))
            {
                go.transform.localScale = canonical;
                return;
            }

            // (c) 폴더 휴리스틱 — 가장 보수적인 fallback. backward compatible 핵심 경로.
            var fallbackBounds = ComputeRendererBounds(go);
            if (!fallbackBounds.HasValue)
            {
                return;
            }
            var fallbackSize = fallbackBounds.Value.size;
            var largestXZ = Mathf.Max(fallbackSize.x, fallbackSize.z);
            if (largestXZ < 0.0001f)
            {
                return;
            }
            var targetSize = ResolveTargetMeterSize(entry.SourceAssetPath);
            var fallbackMultiplier = Mathf.Clamp(targetSize / largestXZ, 0.05f, 20f);
            go.transform.localScale *= fallbackMultiplier;
        }

        // FitAxis 에 해당하는 bounds.size 성분 — LongestXZ 는 max(x,z), 나머지는 해당 축 그대로.
        private static float SelectFitAxisLength(Vector3 boundsSize, DecorationFitAxis axis)
        {
            switch (axis)
            {
                case DecorationFitAxis.X:         return boundsSize.x;
                case DecorationFitAxis.Y:         return boundsSize.y;
                case DecorationFitAxis.Z:         return boundsSize.z;
                case DecorationFitAxis.LongestXZ: default:
                    return Mathf.Max(boundsSize.x, boundsSize.z);
            }
        }

        private static bool TryGetCanonicalScaleByAssetPath(string sourceAssetPath, out Vector3 scale)
        {
            scale = Vector3.one;
            if (string.IsNullOrEmpty(sourceAssetPath))
            {
                return false;
            }
            var fileName = System.IO.Path.GetFileNameWithoutExtension(sourceAssetPath);
            return CanonicalScaleByAssetFileName.TryGetValue(fileName ?? string.Empty, out scale);
        }

        // Walk the active scene and force any decoration whose name is in
        // CanonicalScaleByGameObjectName to its canonical localScale.
        // Equivalent to having BookshelfGenerator and the inventory spawn
        // share a single scale source — without modifying BookshelfGenerator.
        // After scaling the renderer bounds will be the same regardless of
        // where the instance came from, so collider fitting (next step in
        // EnterEditMode) produces a matching BoxCollider in both cases.
        private void NormalizeExistingDecorationScales()
        {
            var scene = gameObject.scene;
            if (!scene.IsValid()) return;

            var rootObjects = scene.GetRootGameObjects();
            for (var i = 0; i < rootObjects.Length; i++)
            {
                var rootObject = rootObjects[i];
                if (rootObject == null) continue;
                ApplyCanonicalScaleByName(rootObject.transform);
            }
        }

        // Snapshot the original localScale into savedDecorationScales BEFORE
        // overwriting, so RestoreSavedDecorationScales (called on ExitEditMode)
        // can put back whatever the designer had configured. Without this
        // snapshot a manual inspector tweak would be silently lost on first
        // edit-mode entry.
        private void ApplyCanonicalScaleByName(Transform t)
        {
            if (t == null) return;
            if (CanonicalScaleByGameObjectName.TryGetValue(t.gameObject.name, out var scale))
            {
                if (t.localScale != scale)
                {
                    if (!savedDecorationScales.ContainsKey(t))
                    {
                        savedDecorationScales[t] = t.localScale;
                    }
                    t.localScale = scale;
                }
            }
            for (var i = 0; i < t.childCount; i++)
            {
                ApplyCanonicalScaleByName(t.GetChild(i));
            }
        }

        // Reverse of NormalizeExistingDecorationScales. Walks the snapshot
        // map and writes each saved localScale back, then clears the map so
        // a fresh edit-mode entry starts from whatever the scene currently
        // has (which may have been edited externally between sessions).
        private void RestoreSavedDecorationScales()
        {
            if (savedDecorationScales.Count == 0)
            {
                return;
            }

            foreach (var entry in savedDecorationScales)
            {
                if (entry.Key == null) continue;
                entry.Key.localScale = entry.Value;
            }
            savedDecorationScales.Clear();
        }

        // Walk the active scene and ensure every blocker-eligible GameObject
        // (a GameObject with a Renderer that isn't part of the room shell or
        // the dragged object) has a non-trigger BoxCollider sized to its
        // current renderer bounds. Walls produced via primitives have their
        // colliders intentionally stripped at room build time, so before
        // this runs Physics.OverlapBox can't see them; after this runs the
        // physics system has consistent collider geometry for every visible
        // scene object that should block movement.
        private void EnsureBlockerColliders()
        {
            var scene = gameObject.scene;
            if (!scene.IsValid()) return;

            var rootObjects = scene.GetRootGameObjects();
            for (var i = 0; i < rootObjects.Length; i++)
            {
                var rootObject = rootObjects[i];
                if (rootObject == null || !rootObject.activeInHierarchy) continue;
                EnsureBlockerCollidersRecursive(rootObject.transform);
            }
        }

        private void EnsureBlockerCollidersRecursive(Transform t)
        {
            if (t == null) return;
            var go = t.gameObject;
            var name = go.name;

            // Skip room shell entirely (descendants too) — floors and ceilings
            // shouldn't block dragged objects' XZ movement.
            if (RoomShellExactNames.Contains(name)) return;

            // Skip the dragged object's hierarchy if a drag is in flight.
            if (draggingObject != null && t.IsChildOf(draggingObject.transform)) return;

            var renderer = go.GetComponent<Renderer>();
            // Check for any collider in the subtree (not just on this exact
            // GameObject) so we don't stack a redundant BoxCollider on top of
            // a nested-mesh prefab that already ships with one.
            if (renderer != null && renderer.enabled && go.GetComponentInChildren<Collider>() == null)
            {
                FitBoxColliderToRenderer(go.AddComponent<BoxCollider>(), renderer);
            }

            for (var i = 0; i < t.childCount; i++)
            {
                EnsureBlockerCollidersRecursive(t.GetChild(i));
            }
        }

        // Belt-and-braces collider pass for inventory items (children of
        // PlacedDecorations) and the four scene-built decoration roots
        // (LampFloor / BookStack / ClockHamham / HamhamDisplay). The
        // recursive walk in EnsureBlockerCollidersRecursive only fits a
        // BoxCollider on a GO that has a *direct* Renderer; a parent root
        // whose mesh sits on a deep .glb child (or on an inactive node)
        // therefore ends up without a single AABB the OverlapBox query can
        // reliably hit. This pass synthesizes one combined-bounds
        // BoxCollider per decoration root using `GetComponentsInChildren<Renderer>(true)`
        // (includes inactive) so the entire visual footprint is queryable
        // even when individual sub-meshes are disabled at runtime.
        // Scene-built decoration / fixture roots that should each carry a
        // single combined-bounds BoxCollider so OverlapBox queries reliably
        // see the entire footprint (not just whichever sub-mesh got an
        // individual fit). Includes:
        //   - LampFloor / BookStack / ClockHamham / HamhamDisplay — the four
        //     scene-built decorations from BookshelfGenerator.
        //   - NotebookStation / Desk / table_natural — the desk + table fixture.
        //     The table's mesh is a .glb instance whose top + legs are
        //     separate child renderers; without a combined collider the
        //     player can drag a small item between the legs and tunnel
        //     through the desk footprint.
        //
        // GuestbookBoard is intentionally NOT in this list. It owns
        // NotebookStation as a child (parented in BuildNotebookStation when
        // the board exists), so a combined-bounds BoxCollider on the board
        // would balloon out to engulf the desk + every notebook part too,
        // walling off the empty space between the board and the desk that
        // the player needs to navigate through. The whiteboard's own
        // frame / surface still get individual-renderer BoxColliders from
        // EnsureBlockerCollidersRecursive, which is enough to block a
        // dragged decoration at the board itself.
        // Names match the runtime GameObject.name values produced by
        // BookshelfGenerator (instantiated .glb roots use the asset's file
        // name without extension, e.g. "table_natural").
        private static readonly string[] DecorationRootSceneNames =
        {
            "LampFloor",
            "BookStack",
            "ClockHamham",
            "HamhamDisplay",
            "NotebookStation",
            "Desk",
            "table_natural",
        };

        private void EnsureDecorationRootColliders()
        {
            if (placedItemsRoot != null)
            {
                for (var i = 0; i < placedItemsRoot.transform.childCount; i++)
                {
                    var child = placedItemsRoot.transform.GetChild(i);
                    if (child == null) continue;
                    EnsureRootCombinedCollider(child.gameObject);
                }
            }

            for (var i = 0; i < DecorationRootSceneNames.Length; i++)
            {
                var go = GameObject.Find(DecorationRootSceneNames[i]);
                if (go == null) continue;
                EnsureRootCombinedCollider(go);
            }
        }

        // Add (or refit) a single BoxCollider on `root` whose world-space
        // AABB matches the union of every active+inactive child Renderer's
        // bounds. Leaves any existing child colliders alone (they continue
        // to function as raycast targets / extra blocker geometry); the
        // root-level box is purely an additional reliable hit shape for
        // OverlapBox queries.
        private static void EnsureRootCombinedCollider(GameObject root)
        {
            if (root == null) return;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return;

            var hasBounds = false;
            var combined = new Bounds();
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                if (!hasBounds)
                {
                    combined = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(r.bounds);
                }
            }
            if (!hasBounds) return;

            var bc = root.GetComponent<BoxCollider>();
            if (bc == null) bc = root.AddComponent<BoxCollider>();

            // World AABB → local center/size. Dividing by lossyScale converts
            // world size back into the box collider's local frame so its
            // post-scale world coverage matches the renderer union exactly.
            var lossy = root.transform.lossyScale;
            bc.center = root.transform.InverseTransformPoint(combined.center);
            bc.size = new Vector3(
                combined.size.x / Mathf.Max(0.001f, Mathf.Abs(lossy.x)),
                combined.size.y / Mathf.Max(0.001f, Mathf.Abs(lossy.y)),
                combined.size.z / Mathf.Max(0.001f, Mathf.Abs(lossy.z)));
            bc.isTrigger = false;
            bc.enabled = true;
        }

        // Size a BoxCollider against its renderer so the collider OBB tracks
        // the mesh exactly — including for rotated transforms.
        // `Renderer.localBounds` is the mesh AABB in the renderer's local
        // frame, so it's invariant under transform rotation. Scaling the
        // collider's size from the local AABB (and using the local center
        // verbatim) gives an OBB that rotates correctly with the transform.
        // Using `Renderer.bounds` (world-space AABB) instead would inflate
        // the collider whenever the transform is rotated off-axis.
        private static void FitBoxColliderToRenderer(BoxCollider bc, Renderer renderer)
        {
            if (bc == null || renderer == null) return;
            var lb = renderer.localBounds;
            bc.center = lb.center;
            bc.size = new Vector3(
                Mathf.Abs(lb.size.x),
                Mathf.Abs(lb.size.y),
                Mathf.Abs(lb.size.z));
            bc.isTrigger = false;
        }

        // Kind=None + canonical 매칭도 없는 edge case 의 마지막 fallback. 평탄화(MR #154) 이전엔
        // /Props/ → 0.5m, /Furniture/ → 1.2m 폴더 휴리스틱이었는데 평탄 구조에선 폴더 의미가
        // 없어졌으므로 항상 Generic 0.8m 반환. Kind 별 표준 크기는 NormalizeSpawnedScale 의
        // 우선 lookup 에서 결정되며 이 함수는 그 외 fallback 만 담당.
        // (MR #154 review #6: /Props//Furniture/ dead code 정리.)
        private static float ResolveTargetMeterSize(string sourceAssetPath)
        {
            return DefaultGenericTargetSizeMeters;
        }

        private static Bounds? ComputeRendererBounds(GameObject go)
        {
            // includeInactive = true so a .glb whose visual root is
            // temporarily disabled still reports a usable AABB. UpdateDrag
            // gates ClampToFloorBounds on bounds.HasValue — without this the
            // dragged object would skip the room-wall clamp whenever its
            // renderer hierarchy contained an inactive node, and could tunnel
            // outside the playable footprint unconstrained.
            // (Overlap with other objects is no longer resolved during drag —
            // it is reported via EvaluateFloorPlacementValid for footprint
            // coloring + confirm-button disable + FinishDrag rollback only.)
            //
            // 회귀 fix — 비대상 Renderer 가 bounds 에 끼어들어 ClampToFloorBounds 가 너무 빡빡해지고
            // OBB 검사가 부풀려지는 회귀 차단:
            //   - renderer.enabled == false : 디자이너가 명시적으로 끈 LOD/helper 메시. 시각/충돌 대상 X.
            //   - !gameObject.activeInHierarchy : (root inactive 호환은 includeInactive=true 가 잡지만)
            //     깊은 자식에서 비활성된 helper 는 footprint 무시.
            //   - ParticleSystemRenderer : 파티클의 동적 bounds 가 매우 큼. 충돌 대상 X.
            //   - TrailRenderer / LineRenderer : 마찬가지로 부풀림 원인.
            //   - 첫 valid renderer 의 bounds 를 base 로 encapsulate — 비대상 renderer 가 첫 자리면
            //     base 가 origin (0,0,0) 이라 모든 bounds 가 거대해지는 회귀 사례 차단.
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return null;
            }
            Bounds? aggregate = null;
            int included = 0, skipped = 0;
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) { skipped++; continue; }
                if (!r.enabled) { skipped++; continue; }
                if (r.gameObject == null || !r.gameObject.activeInHierarchy) { skipped++; continue; }
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) { skipped++; continue; }
                // SkinnedMeshRenderer 가 첫 frame 에 sentinel bounds 를 반환할 수 있어 size 0 이면 skip.
                var rb = r.bounds;
                if (rb.size.sqrMagnitude < 1e-8f) { skipped++; continue; }
                if (!aggregate.HasValue) aggregate = rb;
                else { var b = aggregate.Value; b.Encapsulate(rb); aggregate = b; }
                included++;
            }
            if (!aggregate.HasValue && renderers.Length > 0)
            {
                // 모든 Renderer 가 필터됐으면 fallback — 첫 renderer.bounds 라도 반환해 회귀 (null) 회피.
                aggregate = renderers[0].bounds;
            }
            return aggregate;
        }

        // 시각 footprint preview 와 placement validation 이 동일 영역을 검사하도록 만드는 단일 source.
        // ComputeRendererBounds 와 차이 — 책상 위 모니터, 인형 머리, 램프 갓 같은 *상부* mesh 를
        // XZ AABB 에 포함하지 않는다. 결과: preview 사각형 크기 = OverlapBox 검사 영역 = 사용자가
        // 시각적으로 보는 floor footprint.
        //
        // 규칙 — Renderer.bounds.min.y < FloorFootprintHeightThreshold(0.6m) 인 mesh 만 encapsulate.
        // 떠있는 wall deco 처럼 모든 mesh 가 floor 위에 떠있는 케이스는 가장 낮은 Renderer 의 XZ
        // 사용 (호출부 wall deco 분기가 별도 처리하므로 fallback 진입 빈도 낮음).
        //
        // 결과 size.y 는 무의미 — XZ 만 placement 판정/시각화에 쓰인다. 호출부가 size.y 를 무시하고
        // FloorPlacementProbeHalfHeight 로 별도의 Y slab 을 생성한다.
        //
        // ComputeRendererBounds 는 verticalOffset 계산 / Kind scale 정규화 / collider 합성 등 *전체*
        // mesh size 가 필요한 경로에 그대로 유지 — 본 메서드는 placement 전용.
        private const float FloorFootprintHeightThreshold = 0.6f;
        private static Bounds? ComputeFloorFootprintBounds(GameObject go)
        {
            if (go == null) return null;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return null;

            Bounds? aggregate = null;
            Renderer lowestRenderer = null;
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                if (!r.enabled) continue;
                if (r.gameObject == null || !r.gameObject.activeInHierarchy) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var rb = r.bounds;
                if (rb.size.sqrMagnitude < 1e-8f) continue;
                if (lowestRenderer == null || rb.min.y < lowestRenderer.bounds.min.y) lowestRenderer = r;
                // 위로 떠있는 mesh (모니터/머리/갓) 는 footprint 비대상 — 시각도 검증도 무시.
                if (rb.min.y > FloorFootprintHeightThreshold) continue;
                if (!aggregate.HasValue) aggregate = rb;
                else { var b = aggregate.Value; b.Encapsulate(rb); aggregate = b; }
            }
            if (!aggregate.HasValue && lowestRenderer != null)
            {
                // 모든 mesh 가 floor 위로 떠있음 — 가장 낮은 Renderer 의 XZ 사용 (보수적 fallback).
                aggregate = lowestRenderer.bounds;
            }
            return aggregate;
        }

        private static string SanitizeName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "decoration";
            return raw.Replace(' ', '_');
        }

        private void EnsurePlacedItemsRoot()
        {
            if (placedItemsRoot != null)
            {
                return;
            }
            placedItemsRoot = new GameObject("PlacedDecorations");
            SceneManager.MoveGameObjectToScene(placedItemsRoot, gameObject.scene);
        }

        // Wall placement --------------------------------------------------------

        // 룸의 책장(BackWall)/대시보드(LeftWall) bounds 를 측정해 wall obstruction 등록.
        // 룸 GameObject 가 BookshelfGenerator 에 의해 만들어진 후에 호출. 매번 재측정해
        // 책장/대시보드 위치 변경에도 자동 정합 (e.g. theme 이 다른 prefab 사용 시).
        private WallObstructionRegistry EnsureWallObstructionRegistry()
        {
            if (wallObstructionRegistry == null)
            {
                wallObstructionRegistry = new WallObstructionRegistry();
            }
            // 첫 호출 시만 build — 이후 캐싱. 매 프레임 build 하면 GetComponentsInChildren
            // 중복 호출로 GC pressure. 룸 재생성 시 InvalidateWallObstructionRegistry()
            // 호출 (현재 호출처 없음 — 룸이 한 번만 만들어진다고 가정).
            if (!wallObstructionRegistryBuilt)
            {
                if (themeApplier == null)
                {
                    themeApplier = FindFirstObjectByType<RoomThemeApplier>();
                }
                var roomRoot = themeApplier?.RoomRoot;
                if (roomRoot != null)
                {
                    wallObstructionRegistry.Build(roomRoot);
                    wallObstructionRegistryBuilt = true;
                }
            }
            return wallObstructionRegistry;
        }

        // 룸 재생성/씬 reload 후 호출 — 다음 EnsureWallObstructionRegistry 가 다시 Build.
        private void InvalidateWallObstructionRegistry()
        {
            wallObstructionRegistryBuilt = false;
        }

        // Wall deco placement — RightWall 가운데 spawn (default). 사용자는 spawn 후
        // 드래그로 wall surface 따라 이동 (drag clamp + obstruction check 은 후속 commit).
        // 회전 비활성 entry (allowRotation=false) 는 Selection panel 의 rotate 버튼 disable
        // (후속 commit).
        //
        // 후속 commit roadmap:
        // - drag wall surface clamp (raycast 로 hit wall 의 surface YZ 또는 XY 평면 따라 이동)
        // - obstruction check 실패 시 invalid feedback (붉은 tint)
        // - hit wall 변경 (RightWall → LeftWall 등 사용자 드래그로 wall 전환)
        private void PlaceWallDecoEntry(InventoryEntry entry)
        {
            if (entry.Prefab == null) return;

            EnsurePlacedItemsRoot();
            EnsureWallObstructionRegistry();

            var go = Instantiate(entry.Prefab);
            go.name = $"WallDeco_{SanitizeName(entry.DisplayName)}_{System.Guid.NewGuid():N}";
            go.transform.SetParent(placedItemsRoot.transform, true);

            // default placement — RightWall 표면 가운데. RightWall x = +RoomWidth/2 - 0.05.
            // RoomWidth/Height 상수는 BookshelfGenerator private 이라 정확값 query 불가 →
            // themeApplier.RoomRoot 의 RightWall transform 으로 위치 산출.
            Transform rightWall = null;
            var roomRoot = themeApplier?.RoomRoot;
            if (roomRoot != null)
            {
                var transforms = roomRoot.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i].gameObject.name == "RightWall")
                    {
                        rightWall = transforms[i];
                        break;
                    }
                }
            }

            if (rightWall != null)
            {
                // RightWall inner face = wall.position.x - WallHalfThickness(0.04).
                // wall 표면에 정확히 위로 spawn. mesh 두께가 wall 안으로 박히는 부분은
                // prefab 안에서 GLB instance 의 localZ 을 살짝 양수 (회전 후 -X = 룸 안쪽)
                // 으로 띄워 사용자가 fitting.
                go.transform.position = new Vector3(rightWall.position.x - 0.04f, rightWall.position.y, rightWall.position.z);
                go.transform.rotation = WallFacingRotation(WallSide.Right);
            }
            else
            {
                Debug.LogWarning("[RoomEditModeController] RightWall transform not found — wall deco spawn 위치 불확실. RoomThemeApplier.RoomRoot wiring 확인.");
                go.transform.position = Vector3.zero;
            }

            // marker 추가 — UpdateDrag 가 wall placement 드래그로 분기하기 위함.
            var marker = go.AddComponent<WallDecoMarker>();
            marker.CurrentWall = WallSide.Right;
            marker.AllowRotation = entry.AllowRotation;

            // DecorationItemMarker 도 추가 — BeginEditModePointerPress 가 mouse down 시
            // 이 컴포넌트 검사해 select+drag 시작. 없으면 사용자 클릭 무시됨.
            // floor placement (PlaceInventoryEntry) 와 동일 마커 시스템 공유.
            go.AddComponent<DecorationItemMarker>();

            // collider 보장 — Meshy GLB 는 import 시 collider 자동 생성 안 함.
            // 없으면 mouse down raycast 가 deco mesh 를 hit 못 해 select/drag 진입 불가.
            EnsureCollidersForWallDeco(go);

            // MR #122 — wall placement 도 floor 와 동일하게 pending preview 로 진입.
            // 위치/회전/wall-snap 로직은 무변경 — 머터리얼만 반투명 ghost. 사용자가
            // "배치 확정" 누르기 전까지는 (a) 미확정, (b) 미저장 종료 시 자동 destroy.
            BeginPendingPlacement(go);

            // selection 으로 자동 잡아 사용자가 즉시 드래그 가능하게.
            SelectObject(go);
        }

        // wall deco 의 모든 mesh 자식에 MeshCollider 추가 (없으면). WallMaskQuad 는 stencil
        // 전용이라 collider 추가 X — 사용자 raycast 가 그걸 hit 하면 안 되고 wall 까지 통과해야 함.
        private static void EnsureCollidersForWallDeco(GameObject go)
        {
            var meshFilters = go.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                var mf = meshFilters[i];
                if (mf == null || mf.sharedMesh == null) continue;
                if (mf.gameObject.name == "WallMaskQuad") continue;
                if (mf.GetComponent<Collider>() != null) continue;
                var collider = mf.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mf.sharedMesh;
                collider.convex = false; // raycast 용도라 충돌 처리 불필요
            }
        }

        // ray 와 4 wall 의 inner face plane 교차 → 가장 가까운 wall 의 hit 위치 반환.
        // wall transform 은 BookshelfGenerator.CreateBox 가 만든 cube — collider 는 없지만
        // position/scale 으로 plane 정의 가능. wall thickness 0.08 가정 (= half 0.04).
        // BookshelfGenerator 의 다른 wall thickness 가 도입되면 그 값으로 수정 필요.
        private bool TryRaycastWallSurface(Ray ray, out WallSide hitWall, out Vector3 hitPoint)
        {
            hitWall = WallSide.None;
            hitPoint = Vector3.zero;
            const float WallHalfThickness = 0.04f;

            var roomRoot = themeApplier?.RoomRoot;
            if (roomRoot == null) return false;

            float closestDistance = float.MaxValue;
            var transforms = roomRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                var wall = WallNameToSide(t.gameObject.name);
                if (wall == WallSide.None) continue;

                Vector3 planePoint;
                Vector3 planeNormal;
                switch (wall)
                {
                    case WallSide.Left:
                        planePoint = new Vector3(t.position.x + WallHalfThickness, t.position.y, t.position.z);
                        planeNormal = Vector3.right; break;
                    case WallSide.Right:
                        planePoint = new Vector3(t.position.x - WallHalfThickness, t.position.y, t.position.z);
                        planeNormal = Vector3.left; break;
                    case WallSide.Back:
                        planePoint = new Vector3(t.position.x, t.position.y, t.position.z - WallHalfThickness);
                        planeNormal = Vector3.back; break;
                    case WallSide.Front:
                        planePoint = new Vector3(t.position.x, t.position.y, t.position.z + WallHalfThickness);
                        planeNormal = Vector3.forward; break;
                    default: continue;
                }

                float denom = Vector3.Dot(ray.direction, planeNormal);
                // dotshelf camera 는 룸 앞쪽 (z=-2.45) 위치, ray 가 +Z 방향으로 룸 안쪽 향함.
                // BackWall planeNormal = -Z. ray.direction · normal = (+) · (-) = -. 즉 사용자가
                // 보는 wall inner face 시점은 denom < 0. (camera 가 룸 안쪽으로 ray 발사하면
                // wall 의 inner face normal 방향과 반대.) 양수면 wall 뒷면 시점 reject.
                if (denom >= -0.0001f) continue;
                float distance = Vector3.Dot(planePoint - ray.origin, planeNormal) / denom;
                if (distance < 0f) continue; // 뒤쪽

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    hitWall = wall;
                    hitPoint = ray.origin + ray.direction * distance;
                }
            }
            return hitWall != WallSide.None;
        }

        // wall 의 안쪽 face (룸 내부 향하는 방향) 으로 deco 회전.
        // 좌표계: +Z 가 default forward, wall 별로 deco 가 룸 안쪽 향하게 Y 축 회전.
        private static Quaternion WallFacingRotation(WallSide wall)
        {
            switch (wall)
            {
                case WallSide.Left:  return Quaternion.Euler(0f, 90f, 0f);   // +X 향함
                case WallSide.Right: return Quaternion.Euler(0f, -90f, 0f);  // -X 향함
                case WallSide.Back:  return Quaternion.Euler(0f, 180f, 0f);  // -Z 향함
                case WallSide.Front: return Quaternion.identity;             // +Z 향함
                default:             return Quaternion.identity;
            }
        }

        private static WallSide WallNameToSide(string name)
        {
            switch (name)
            {
                case "LeftWall":  return WallSide.Left;
                case "RightWall": return WallSide.Right;
                case "FrontWall": return WallSide.Front;
                // BackWall 자체는 BookshelfGenerator.BuildBackWallAlcove 가 alcove 때문에
                // 통째 wall 안 만들고 strip 3개 (LeftStrip/RightStrip/TopStrip) 로 분할 생성.
                // 모든 strip 의 z 좌표는 동일 (BackWallZ const) 이라 plane 으로 동치 — 어떤
                // strip 이 매칭돼도 같은 BackWall 로 취급.
                case "BackWall":
                case "BackWall_LeftStrip":
                case "BackWall_RightStrip":
                case "BackWall_TopStrip":  return WallSide.Back;
                default:                   return WallSide.None;
            }
        }

        // Edit-mode click → selection ---------------------------------------------

        // Mouse-down / mouse-held / mouse-up routing for select + drag. The
        // bottom menu and rotate/delete buttons are opaque UI so EventSystem
        // reports those clicks as "over UI" — HandleEditModeInput bails in that
        // case so toolbar drags and slot clicks never trigger world raycasts.
        private void HandleEditModeInput()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            var camera = ResolveSceneCamera();
            if (camera == null)
            {
                if (draggingObject != null) FinishDrag();
                return;
            }

            var pointer = ReadPointerPosition();

            if (mouse.leftButton.wasPressedThisFrame)
            {
                BeginEditModePointerPress(camera, pointer);
                return;
            }

            if (draggingObject != null)
            {
                if (mouse.leftButton.isPressed)
                {
                    UpdateDrag(camera, pointer);
                }
                else if (mouse.leftButton.wasReleasedThisFrame)
                {
                    FinishDrag();
                }
            }
        }

        // MR #129 임시 진단 로그 — 편집 모드에서 데코 클릭/이동이 안 되는 원인을 확정하기
        // 위해 [EditPress] prefix 로 각 gate 통과 여부를 콘솔에 찍는다. 원인 확인되면 이 메서드
        // 안의 모든 Debug.Log 라인과 본 주석을 제거할 것 — 기능 로직(early-return / SelectObject /
        // BeginDrag / DeselectCurrentObject 호출 순서)은 무변경.
        private void BeginEditModePointerPress(Camera camera, Vector2 pointer)
        {
            // Anything that the canvas raycaster considers UI (bottom menu,
            // rotate / delete buttons, return button) just stops here.
            var overUi = IsPointerOverUi();
#if UNITY_EDITOR
            // 진단 강화 — pointer 의 화면 사분면(좌하단 회귀 영역 식별용) + UI hit 시 어떤 element 인지.
            var screenW = Screen.width;
            var screenH = Screen.height;
            var quadrant = (pointer.x < screenW * 0.5f ? "L" : "R") + (pointer.y < screenH * 0.5f ? "B" : "T");
            Debug.Log($"[EditPress] enter pointer={pointer} screen=({screenW}x{screenH}) quadrant={quadrant} " +
                      $"overUi={overUi} editMode={IsEditModeActive} initialized={isInitialized} modalOpen={isUnsavedChangesModalOpen}");
#else
            Debug.Log($"[EditPress] enter pointer={pointer} overUi={overUi} editMode={IsEditModeActive} initialized={isInitialized} modalOpen={isUnsavedChangesModalOpen}");
#endif
            if (overUi)
            {
                Debug.Log("[EditPress] blocked by IsPointerOverUi");
                return;
            }

            var pixelRect = camera.pixelRect;
            if (pointer.x < pixelRect.xMin || pointer.x > pixelRect.xMax ||
                pointer.y < pixelRect.yMin || pointer.y > pixelRect.yMax)
            {
                Debug.Log($"[EditPress] blocked by pixelRect pointer={pointer} rect={pixelRect}");
                DeselectCurrentObject();
                return;
            }

            var ray = camera.ScreenPointToRay(pointer);
#if UNITY_EDITOR
            // 진단 — ray 자체 (origin/direction) + 카메라 위치. 좌하단 회귀에서 ray 가 의도한 floor
            // 방향으로 가는지 확인 — 카메라 view rotation 회귀 시 ray 가 엉뚱한 방향일 가능성 식별.
            Debug.Log($"[EditPress] ray origin={ray.origin} direction={ray.direction} cameraPos={camera.transform.position}");
#endif
            // 회귀 fix — 단일 Physics.Raycast 는 ray 의 *첫* collider 만 반환. 좌하단/책상 앞 영역
            // 처럼 DecorationItemMarker 없는 fixture(책상·노트북·기타 helper) 가 placed decoration
            // 위층에 collider 를 깔고 있으면 첫 hit 이 fixture → marker 미발견 → DeselectCurrentObject
            // 로 빠져 그 영역의 인형/데코는 영원히 선택 불가 회귀.
            // RaycastAll + 거리순 정렬로 모든 hit 을 훑어 marker 있는 첫 collider 를 선택. fixture
            // collider 는 자연스럽게 통과. RaycastNonAlloc 은 buffer 관리 부담 + 빈도 낮은 click 경로
            // 라 RaycastAll 의 GC 비용이 무시할만함.
            var hits = Physics.RaycastAll(ray, 100f);
            if (hits != null && hits.Length > 0)
            {
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                for (var i = 0; i < hits.Length; i++)
                {
                    var hit = hits[i];
                    if (hit.collider == null) continue;
                    var marker = hit.collider.GetComponentInParent<DecorationItemMarker>();
#if UNITY_EDITOR
                    // 진단 강화 — 좌하단 영역 selection 회귀 추적용. layer / collider 타입 / world bounds /
                    // hierarchy path 까지 출력해 어떤 helper / fixture / placed 가 ray 를 가로막는지 식별.
                    var col = hit.collider;
                    var layerName = LayerMask.LayerToName(col.gameObject.layer);
                    var path = BuildHierarchyPath(col.transform);
                    Debug.Log($"[EditPress] hit[{i}] name='{col.name}' path='{path}' " +
                              $"layer={col.gameObject.layer}({layerName}) colliderType={col.GetType().Name} " +
                              $"distance={hit.distance:0.##} hitPoint={hit.point} colliderBounds={col.bounds} " +
                              $"marker={(marker != null ? marker.name : "<null>")} " +
                              $"isTrigger={col.isTrigger} enabled={col.enabled}");
#else
                    Debug.Log($"[EditPress] hit[{i}]='{hit.collider.name}' hitObject='{hit.collider.gameObject.name}' " +
                              $"distance={hit.distance:0.##} marker={(marker != null ? marker.name : "<null>")}");
#endif
                    if (marker != null)
                    {
                        // Select first if it isn't already selected, then begin a drag
                        // immediately. The same gesture (press + release without
                        // movement) ends up as a no-op drag, which is fine — a click
                        // on the already-selected object stays selected.
                        if (selectedObject != marker.gameObject)
                        {
                            SelectObject(marker.gameObject);
                        }
                        BeginDrag(marker.gameObject, ray);
                        Debug.Log($"[EditPress] BeginDrag fired on '{marker.gameObject.name}' via hit[{i}]");
                        return;
                    }
                }
                Debug.Log($"[EditPress] {hits.Length} hit(s) but none has DecorationItemMarker → DeselectCurrentObject");
            }
            else
            {
                Debug.Log("[EditPress] Physics.RaycastAll missed");
            }

            Debug.Log("[EditPress] no draggable target → DeselectCurrentObject");
            DeselectCurrentObject();
        }

        // Drag lifecycle ----------------------------------------------------------

        private void BeginDrag(GameObject target, Ray initialRay)
        {
            draggingObject = target;
            dragLastValidPosition = target.transform.position;
            // FinishDrag 가 epsilon 비교로 실제 이동 여부를 판별하기 위한 anchor + drop 시 invalid 면
            // 기존(pending 아님) 오브젝트를 시작 pose 로 rollback 하는 anchor.
            dragStartPosition = target.transform.position;
            dragStartRotation = target.transform.rotation;

            // Capture grab offset so the model doesn't snap its center under the
            // pointer at drag start — instead it stays where it was relative to
            // where the player grabbed it.
            bool floorHit = TryRaycastFloorPlane(initialRay, out var grabPoint);
            if (floorHit)
            {
                dragGrabOffset = new Vector3(
                    target.transform.position.x - grabPoint.x,
                    0f,
                    target.transform.position.z - grabPoint.z);
            }
            else
            {
                dragGrabOffset = Vector3.zero;
            }
#if UNITY_EDITOR
            // 진단 — BeginDrag 진입 + floor raycast 결과. 좌하단에서 floor raycast 가 실패하면
            // grabOffset 이 0 으로 fallback → drag 시 객체가 pointer 로 점프하는 회귀 식별.
            Debug.Log($"[EditDrag] BeginDrag target='{target.name}' startPos={dragStartPosition} " +
                      $"floorHit={floorHit} grabPoint={(floorHit ? grabPoint.ToString() : "<miss>")} " +
                      $"grabOffset={dragGrabOffset}");
#endif

            // Re-run scene-shape detection in case any wall, fixture, or
            // decoration was added / moved / scaled since edit mode was
            // entered. The grid display rebuild below picks up any change.
            RefreshPlayableBounds();
            EnsureBlockerColliders();
            EnsureDecorationRootColliders();
            RebuildBlockerRendererCache(target);
            ShowGrid();

            // 새 드래그 — 배치 유효성 추적 초기화. UpdateDrag 첫 tick 에서 transition 으로 인식돼
            // 시작 시 한 번 [EditPlacement] valid=... 로그가 남도록 dragValidStateInitialized=false.
            dragValidStateInitialized = false;
            dragLastPlacementValid = true;
            dragLastInvalidReason = null;
            // [EditDrag] dedup 도 동일하게 리셋 — 새 드래그 시작 첫 tick 에서 한 번 로그 남게.
            dragLastEditDragLogInitialized = false;
            // footprint 가 첫 frame 에 좌표 0,0,0 으로 깜빡이지 않도록 현재 위치를 초기 probe 로.
            lastFloorDragRawCandidate = target.transform.position;
        }

        private void UpdateDrag(Camera camera, Vector2 pointer)
        {
            if (draggingObject == null) return;

            // If the pointer leaves the camera viewport (over UI / pillarbox),
            // freeze the object where it is rather than firing garbage rays.
            // The drag continues until the player releases the mouse button.
            var pixelRect = camera.pixelRect;
            if (pointer.x < pixelRect.xMin || pointer.x > pixelRect.xMax ||
                pointer.y < pixelRect.yMin || pointer.y > pixelRect.yMax)
            {
                return;
            }

            // wall deco 면 wall surface 따라 별도 흐름으로 라우트. floor placement 드래그
            // 와 시스템이 다름 — raycast 로 hit wall 의 표면 위치 + obstruction check.
            var wallMarker = draggingObject.GetComponent<WallDecoMarker>();
            if (wallMarker != null)
            {
                UpdateWallDrag(camera, pointer, wallMarker);
                return;
            }

            var ray = camera.ScreenPointToRay(pointer);
            if (!TryRaycastFloorPlane(ray, out var floorHit))
            {
                return;
            }

            var rawX = floorHit.x + dragGrabOffset.x;
            var rawZ = floorHit.z + dragGrabOffset.z;
            if (gridSnapEnabled)
            {
                // Snap to MinorGridStep cells so movement matches the visible
                // minor grid lines (0.1m at default settings). Cell-CENTER
                // snap anchored to (playableMinX, playableMinZ), which sits
                // at the wall-floor corner where the visible grid starts.
                // The +step/2 offset places objects in the middle of each
                // minor cell instead of on grid intersections.
                rawX = playableMinX + Mathf.Floor((rawX - playableMinX) / MinorGridStep) * MinorGridStep + MinorGridStep * 0.5f;
                rawZ = playableMinZ + Mathf.Floor((rawZ - playableMinZ) / MinorGridStep) * MinorGridStep + MinorGridStep * 0.5f;
            }

            var rawCandidate = new Vector3(rawX, draggingObject.transform.position.y, rawZ);
            var candidate = rawCandidate;
            var currentPosition = draggingObject.transform.position;
            // 회귀 fix — drag tick 도 preview/validation 과 동일 source 사용. ClampToFloorBounds 가
            // preview 사각형 기준으로 동작 → 사용자가 시각적으로 본 footprint 가 방 밖으로 안 가면
            // 드래그 가능. 이전엔 ComputeRendererBounds 라 책상 위 모니터 같은 상부 mesh 까지
            // clamp 영역 계산에 포함됐다.
            var bounds = ComputeFloorFootprintBounds(draggingObject);
            // 새 UX(이번 변경) — 이동 검증과 배치 검증 분리.
            //   movement gate (드래그 중) : ClampToFloorBounds 만. 방 경계 안에서는 다른 오브젝트와
            //                              겹치는 위치로도 자유롭게 이동 가능.
            //   placement gate (확정 시점) : EvaluateFloorPlacementValid 가 ClampToFloorBounds +
            //                              IsSpawnPositionBlockedAt 둘 다 본다. footprint 색 / 확정
            //                              버튼 disabled / FinishDrag rollback 결정에 사용.
            // ResolveBlockerCollisions 호출은 의도적으로 제거 — 그게 슬라이딩/완전차단으로 이동을
            // 막아서 사용자가 겹치는 영역을 지나갈 수 없었던 회귀의 원인.
            if (bounds.HasValue)
            {
                candidate = ClampToFloorBounds(currentPosition, candidate, bounds.Value);
            }

            draggingObject.transform.position = candidate;
            dragLastValidPosition = candidate;

            // MR #134 — 오브젝트 본체 material 은 건드리지 않는다 (AC:HHP 스타일). raw 타깃 위치를
            // 저장만 해두면 다음 Update 의 UpdatePlacementFootprint 가 footprint UI 색을 갱신한다.
            // [EditPlacement] 로그는 검증 transition 시점 추적을 위해 그대로 유지.
            lastFloorDragRawCandidate = rawCandidate;
            bool isValid = EvaluateFloorPlacementValid(draggingObject, rawCandidate, bounds, out var invalidReason);
            UpdatePlacementValidityState(draggingObject, isValid, invalidReason);
            // 이동 검증/배치 검증 분리 추적 — moveAllowed 는 항상 inRoom (clamp 적용 결과 raw 와의 일치).
            bool inRoom = bounds.HasValue
                ? Mathf.Abs(rawCandidate.x - candidate.x) < 1e-4f && Mathf.Abs(rawCandidate.z - candidate.z) < 1e-4f
                : true;
            LogEditDragTransition(moveAllowed: inRoom, inRoom: inRoom, placementValid: isValid, reason: invalidReason);
            // 진단 — drag tick 의 핵심 수치 (state-transition 시점만, dedup 은 LogEditDragTransition 가 담당).
            if (bounds.HasValue)
            {
                LogEditMoveTickIfChanged(draggingObject, candidate, bounds.Value, isValid, invalidReason);
            }
        }

        // 매 frame 도배 방지 — 직전 출력과 (valid, reason, intCellX, intCellZ) 가 같으면 skip.
        // grid snap 단계 (0.1m) 보다 큰 변화만 잡혀서 cursor 흔들림에 도배되지 않는다.
        private bool _editMoveLogInitialized;
        private bool _editMoveLogLastValid;
        private string _editMoveLogLastReason;
        private int _editMoveLogLastCellX, _editMoveLogLastCellZ;
        private void LogEditMoveTickIfChanged(GameObject obj, Vector3 position, Bounds bounds, bool isValid, string reason)
        {
            int cellX = Mathf.RoundToInt(position.x * 10f);
            int cellZ = Mathf.RoundToInt(position.z * 10f);
            string r = reason ?? "None";
            if (_editMoveLogInitialized && _editMoveLogLastValid == isValid && _editMoveLogLastReason == r &&
                _editMoveLogLastCellX == cellX && _editMoveLogLastCellZ == cellZ)
            {
                return;
            }
            _editMoveLogLastValid = isValid;
            _editMoveLogLastReason = r;
            _editMoveLogLastCellX = cellX;
            _editMoveLogLastCellZ = cellZ;
            _editMoveLogInitialized = true;
            Debug.Log(
                $"[EditMove] obj='{obj.name}' position={position} valid={isValid} reason='{r}' " +
                $"boundsSize={bounds.size} footprintXZ=({bounds.size.x:0.##},{bounds.size.z:0.##})");
        }

        // 배치 유효성 평가 — 기존 검증 로직(ClampToFloorBounds / IsSpawnPositionBlockedAt)
        // 을 재사용해 (valid, reason) 을 반환. drag tick / 확정 버튼 disabled 상태 양쪽에서 호출.
        // bounds 가 없으면(렌더러 없음 등) 보수적으로 valid 처리 — 시각화 대상에서 제외.
        private bool EvaluateFloorPlacementValid(GameObject go, Vector3 candidate, Bounds? cachedBounds, out string reason)
        {
            reason = null;
            if (go == null) return true;
            // 회귀 fix — 호출자가 cachedBounds 를 안 주면 ComputeFloorFootprintBounds (preview 동일
            // source) 로 계산. 이전 ComputeRendererBounds 는 위쪽 mesh 까지 포함해 preview 보다
            // 큰 영역으로 검증했다.
            var b = cachedBounds ?? ComputeFloorFootprintBounds(go);
            if (!b.HasValue) return true;

            var bounds = b.Value;
            var clamped = ClampToFloorBounds(candidate, candidate, bounds);
            if (Mathf.Abs(clamped.x - candidate.x) > 1e-4f || Mathf.Abs(clamped.z - candidate.z) > 1e-4f)
            {
                reason = "OutOfRoom";
                return false;
            }
            if (IsSpawnPositionBlockedAt(go, bounds, candidate))
            {
                reason = "OverlapWithObstacle";
                return false;
            }
            return true;
        }

        // 이동 검증(moveAllowed/inRoom) 과 배치 검증(placementValid) 분리 추적.
        // 사용자 spec: '드래그 이동 가능 여부' 와 '최종 배치 가능 여부' 를 구분된 신호로 출력.
        // 매 frame 도배 방지 — 직전 tick 과 (moveAllowed, inRoom, placementValid, reason) 4-tuple
        // 이 동일하면 skip. 새 드래그가 시작되면 BeginDrag 가 dragValidStateInitialized=false 로 리셋.
        private bool dragLastMoveAllowed;
        private bool dragLastInRoom;
        private bool dragLastEditDragLogInitialized;
        private void LogEditDragTransition(bool moveAllowed, bool inRoom, bool placementValid, string reason)
        {
            if (dragLastEditDragLogInitialized
                && dragLastMoveAllowed == moveAllowed
                && dragLastInRoom == inRoom
                && dragLastPlacementValid == placementValid
                && dragLastInvalidReason == reason)
            {
                return;
            }
            var reasonText = reason ?? "None";
            Debug.Log($"[EditDrag] moveAllowed={moveAllowed} inRoom={inRoom} placementValid={placementValid} reason='{reasonText}'");
            dragLastMoveAllowed = moveAllowed;
            dragLastInRoom = inRoom;
            dragLastEditDragLogInitialized = true;
            // dragLastPlacementValid / dragLastInvalidReason 는 UpdatePlacementValidityState 가 별도로
            // 갱신하지만, 이 helper 가 placementValid 를 인자로 받을 때 그 호출이 같은 frame 안 선후순서
            // 어느 쪽이든 [EditDrag] 의 dedup 키는 자기 시점의 값으로 유지된다.
        }

        // 상태 전이(valid ↔ invalid) 시점에만 로그. 매 프레임 도배 방지.
        private void UpdatePlacementValidityState(GameObject go, bool isValid, string reason)
        {
            if (dragValidStateInitialized && dragLastPlacementValid == isValid && dragLastInvalidReason == reason)
            {
                return;
            }
            var name = go != null ? go.name : "<null>";
            if (isValid)
            {
                Debug.Log($"[EditPlacement] placement valid=True object='{name}'");
            }
            else
            {
                Debug.Log($"[EditPlacement] placement valid=False object='{name}' reason='{reason ?? "Unknown"}'");
            }
            dragLastPlacementValid = isValid;
            dragLastInvalidReason = reason;
            dragValidStateInitialized = true;
        }

        // (MR #134 — 바닥 배치 항목의 본체 색은 더 이상 빨갛게 칠하지 않는다. 검증 결과는
        // PlacementFootprintPreview 의 바닥 footprint UI 에서 초록/빨강으로 표시. 본 메서드는
        // 제거됨.)

        // Wall deco 드래그 — 마우스 raycast 로 hit 한 wall 의 surface 위치로 이동 + 회전.
        // 다른 wall 로 hit 옮겨가면 자동으로 wall 전환 (deco 회전도 새 wall 안쪽 면 향하게).
        // obstruction check — registry 와 비교해 invalid 영역이면 붉은 tint feedback.
        // FrontWall 은 카메라 쪽 벽이라 hit 안 되는 게 일반적. Left/Right/Back 만 실질 후보.
        private void UpdateWallDrag(Camera camera, Vector2 pointer, WallDecoMarker marker)
        {
            // BookshelfGenerator.CreateBox 가 wall collider 명시 제거 → Physics.Raycast 로
            // wall hit 못 함. 대신 wall 의 inner face plane 과 ray 의 수학적 교차 — collider
            // 의존성 없음 + self-raycast 회피 자동 (plane 만 본다).
            if (!TryRaycastWallSurface(camera.ScreenPointToRay(pointer), out var newWall, out var hitPoint))
            {
                return;
            }

            // 새 UX(이번 변경) — wall deco 도 floor 와 동일하게 이동 검증과 배치 검증 분리.
            //   movement : 항상 hitPoint+rotation 적용. 책장/대시보드 영역 위로도 자유롭게 이동.
            //   placement: registry.IsRectFreeOnWall 결과를 footprint feedback / FinishDrag rollback /
            //              확정 버튼 disabled 에만 사용. 더 이상 위치 자체를 차단하지 않음.
            // 이전 동작(invalid → prev pose 로 rollback)은 사용자가 obstruction 영역을 통과 못하게
            // 하는 회귀였음.
            draggingObject.transform.position = hitPoint;
            draggingObject.transform.rotation = WallFacingRotation(newWall);
            marker.CurrentWall = newWall;

            bool isFree = true;
            var bounds = ComputeRendererBounds(draggingObject);
            if (bounds.HasValue)
            {
                var rect = ProjectBoundsToWallRect(bounds.Value, newWall);
                var registry = EnsureWallObstructionRegistry();
                isFree = registry.IsRectFreeOnWall(newWall, rect);
            }

            if (isFree)
            {
                dragLastValidPosition = hitPoint;
            }

            ApplyWallDecoValidityFeedback(draggingObject, isFree);
            // MR #133 — wall deco 도 동일한 [EditPlacement] 로그 경로로 통합.
            UpdatePlacementValidityState(draggingObject, isFree, isFree ? null : "OverlapOnWall");
            // 이동 검증/배치 검증 분리 추적 — wall 표면 hit 가 잡힌 시점이라 moveAllowed=true (방 안).
            LogEditDragTransition(moveAllowed: true, inRoom: true, placementValid: isFree, reason: isFree ? null : "OverlapOnWall");
        }

        // World AABB → wall surface (u, v) Rect — WallObstructionRegistry 와 동일 좌표계.
        private static Rect ProjectBoundsToWallRect(Bounds worldBounds, WallSide wall)
        {
            float vMin = worldBounds.min.y;
            float vMax = worldBounds.max.y;
            float uMin, uMax;
            switch (wall)
            {
                case WallSide.Back:
                case WallSide.Front:
                    uMin = worldBounds.min.x;
                    uMax = worldBounds.max.x;
                    break;
                case WallSide.Left:
                case WallSide.Right:
                    uMin = worldBounds.min.z;
                    uMax = worldBounds.max.z;
                    break;
                default:
                    return Rect.zero;
            }
            return new Rect(uMin, vMin, uMax - uMin, vMax - vMin);
        }

        // valid: 원래 색 / invalid: 붉은 tint. material instance 사용 — 원본 protect.
        // 단순 tint 누적 회피를 위해 _BaseColor 직접 set (multiply 아님).
        // 단점: 원래 material 의 _BaseColor 를 모름 — 일단 white(valid) / soft red(invalid) 하드.
        // follow-up 에서 원래 색 캐싱 + 정확 복원 (또는 별도 outline 시각).
        private void ApplyWallDecoValidityFeedback(GameObject deco, bool isValid)
        {
            var renderers = deco.GetComponentsInChildren<Renderer>(true);
            // MR #122 — pending(=ghost preview) 상태 wall deco 는 알파 0.45 유지하고 RGB 만
            // 기존 valid/invalid 색으로 덮어쓴다. 풀-알파(1f) 로 덮으면 ghost 가 불투명해져
            // preview 상태가 시각적으로 깨진다. 확정된 항목은 기존 동작(완전 불투명) 유지.
            bool isPending = pendingPlacements.Contains(deco);
            var tint = isPending
                ? (isValid ? new Color(0.55f, 0.78f, 1f, 0.45f) : new Color(1f, 0.55f, 0.55f, 0.45f))
                : (isValid ? Color.white : new Color(1f, 0.55f, 0.55f, 1f));
            for (int i = 0; i < renderers.Length; i++)
            {
                var mat = renderers[i].material;
                if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", tint);
                }
                else if (mat.HasProperty("_Color"))
                {
                    mat.SetColor("_Color", tint);
                }
            }
        }

        private void FinishDrag()
        {
            // 새 UX(이번 변경) — UpdateDrag 가 더 이상 ResolveBlockerCollisions 를 적용하지 않으므로
            // drop 시점의 위치가 invalid(겹침)일 수 있다. 정책:
            //   pending placement (신규로 인벤토리에서 추가됐고 아직 확정 안 됨)
            //     → 그대로 둔다. 사용자가 다시 드래그하거나 위치를 바꿔서 valid 만들거나, 다른 곳에 둔
            //       채로 confirm 버튼을 누름 (확정 버튼은 EvaluateConfirmEnabled 가 invalid 면 disable).
            //   기존(이미 확정된) 오브젝트
            //     → 시작 pose 로 rollback. "겹친 채로 commit" 회귀 차단.
            //
            // 이전 구현은 wasDragging 만 보고 SetDirty 를 항상 호출했다. 사용자가 마커를 단순
            // 선택만 해도 BeginDrag 가 같이 시작되므로 release 시 좌표 변화가 없어도 dirty 가 켜져서
            // 미저장 모달이 뜨는 회귀 (MR #120 skh1205 리뷰 #3) 가 있었다. 시작 좌표와 현재 좌표를
            // epsilon 비교해 실제 이동이 발생한 경우에만 dirty 처리.
            var dragged = draggingObject;
            draggingObject = null;
            HideGrid();
            if (dragged == null) return;

            // 1) drop 시점 placement 검증 — floor 와 wall 항목별 기존 검증 로직 그대로 재사용.
            bool dropValid = EvaluateConfirmEnabled(dragged);
            bool isPending = pendingPlacements.Contains(dragged);

            // 2) 기존(non-pending) 오브젝트 + invalid drop → 시작 pose 로 rollback.
            if (!dropValid && !isPending)
            {
                Debug.Log($"[EditDrag] drop invalid on existing object='{dragged.name}' → rollback to start pose");
                dragged.transform.position = dragStartPosition;
                dragged.transform.rotation = dragStartRotation;
            }

            // MR #134 — wall deco 만 기존 body tint feedback 유지 (Animal Crossing 스타일 footprint
            // UI 는 floor 항목 전용). floor 는 본체 색을 건드리지 않으므로 release 시 복구할 게 없다.
            // 단, 사용자가 invalid frame 에서 곧바로 release 하면 wall deco tint 가 빨강으로 남을
            // 가능성이 있어 valid 로 한 번 정리.
            var wallMarker = dragged.GetComponent<WallDecoMarker>();
            if (wallMarker != null)
            {
                ApplyWallDecoValidityFeedback(dragged, true);
            }
            // 다음 Update 의 UpdatePlacementFootprint 가 selectedObject 가 더 이상 dragging 아니라는
            // 조건으로 자동 hide 하지만, 비-pending 항목이면 release 직후엔 selectedObject 만 남아
            // 조건 미달로 hide 가 호출됨. pending 항목이면 isPending=true 로 footprint 가 계속 보인다.
            dragValidStateInitialized = false;
            dragLastPlacementValid = true;
            dragLastInvalidReason = null;

            // grid snap 단계(MinorGridStep ~ 0.1m) 보다 한참 작은 epsilon — 부동소수 잡음만 무시.
            const float MovementEpsilonSqr = 1e-6f;
            var moved = (dragged.transform.position - dragStartPosition).sqrMagnitude > MovementEpsilonSqr;
            if (moved)
            {
                SetDirty("FinishDrag");
            }
        }

        // Floor placement plane (room floor surface is y = 0 in world).
        private static bool TryRaycastFloorPlane(Ray ray, out Vector3 hitPoint)
        {
            var plane = new Plane(Vector3.up, new Vector3(0f, FloorPlaneY, 0f));
            if (plane.Raycast(ray, out var distance))
            {
                hitPoint = ray.GetPoint(distance);
                return true;
            }
            hitPoint = Vector3.zero;
            return false;
        }

        // Clamp the candidate so the object's XZ bounds stay entirely inside
        // the playable room footprint. The visible drag grid covers the same
        // range (see EnsureGridDisplay), so this clamp matches "what the
        // player sees". Bounds come from RefreshPlayableBounds, so the
        // back-wall side of the room (where the bookshelf is mounted) is the
        // wall's front face rather than the far edge of the floor mesh.
        // Collision with bookshelf / furniture / other placed decorations is
        // handled separately by ResolveBlockerCollisions.
        // 회귀 fix — footprint *edge* 가 visible grid edge 를 살짝 넘는 정도까지 허용.
        // 이전엔 footprint center 가 [playableMin+extents, playableMax-extents] 정확히 안에
        // 있어야 push-back 안 됐는데, 사용자가 grid 가장자리 셀에 객체를 놓으면:
        //   (a) ComputeFloorFootprintBounds 가 mesh 의 모자/꼬리/그림자 plane 같은 옆 mesh 까지
        //       포함해 footprint extents 가 visible mesh 보다 큼
        //   (b) 부동소수 노이즈 / 회전 후 AABB 부풀림
        //   (c) wall thickness(0.04m) 와 footprint edge 가 1mm 차이로 겹침
        // 셋 중 어느 케이스든 footprint edge 가 grid edge 를 미세하게 넘어가 OutOfRoom 깜빡임.
        // tolerance = 0.05m: wall thickness 0.04m 안쪽 + 1cm epsilon. wall 안쪽으로 5cm 까지
        // footprint edge 가 들어가도 OK — 시각적으로는 grid 가장자리 셀에 들어간 상태로 인식.
        // 실제 wall 너머로 큰 이탈은 여전히 차단 (extents 일부만 넘어가는 케이스만 허용).
        private const float PlayableBoundsTolerance = 0.05f;

        private Vector3 ClampToFloorBounds(Vector3 currentPosition, Vector3 candidate, Bounds currentBounds)
        {
            // current bounds are in world space, anchored to the object's current
            // position. Translate by the candidate-vs-current delta to get the
            // bounds the object would have at the candidate position.
            var delta = candidate - currentPosition;
            var futureCenter = currentBounds.center + delta;
            var extents = currentBounds.extents;

            // PlayableBoundsTolerance 만큼 effective playable 을 확장 — footprint edge 가 grid
            // edge 를 5cm 까지 넘어가도 push-back 안 됨. 사용자 시점 "grid 셀에 닿은 만큼 OK".
            var minCenterX = playableMinX + extents.x - PlayableBoundsTolerance;
            var maxCenterX = playableMaxX - extents.x + PlayableBoundsTolerance;
            var minCenterZ = playableMinZ + extents.z - PlayableBoundsTolerance;
            var maxCenterZ = playableMaxZ - extents.z + PlayableBoundsTolerance;

            // Defensive: if the object is wider than the room (shouldn't happen
            // post-NormalizeSpawnedScale, but a tweaked catalog could), keep
            // min ≤ max so Mathf.Clamp returns something deterministic.
            if (minCenterX > maxCenterX) { var mid = (minCenterX + maxCenterX) * 0.5f; minCenterX = maxCenterX = mid; }
            if (minCenterZ > maxCenterZ) { var mid = (minCenterZ + maxCenterZ) * 0.5f; minCenterZ = maxCenterZ = mid; }

            var clampedCenterX = Mathf.Clamp(futureCenter.x, minCenterX, maxCenterX);
            var clampedCenterZ = Mathf.Clamp(futureCenter.z, minCenterZ, maxCenterZ);

#if UNITY_EDITOR
            // 진단 — push-back 발생 시만 로그 (idle 시 도배 X). 어느 axis 가 어느 방향으로 넘어갔는지
            // 명시 → 회귀 추적 시 "minX 측인지 maxZ 측인지" 즉시 식별. tolerance 가 적용된 effective
            // bounds 도 함께 출력해 실제 clamp 기준선 확인.
            if (Mathf.Abs(clampedCenterX - futureCenter.x) > 1e-4f || Mathf.Abs(clampedCenterZ - futureCenter.z) > 1e-4f)
            {
                var overflow = "";
                if (futureCenter.x < minCenterX) overflow += " minX";
                if (futureCenter.x > maxCenterX) overflow += " maxX";
                if (futureCenter.z < minCenterZ) overflow += " minZ";
                if (futureCenter.z > maxCenterZ) overflow += " maxZ";
                Debug.Log($"[EditClamp] push-back overflow={overflow.Trim()} " +
                          $"inputCenter=({futureCenter.x:0.###},{futureCenter.z:0.###}) " +
                          $"clampedCenter=({clampedCenterX:0.###},{clampedCenterZ:0.###}) " +
                          $"extents=({extents.x:0.###},{extents.z:0.###}) " +
                          $"playableRaw X=[{playableMinX:0.###},{playableMaxX:0.###}] Z=[{playableMinZ:0.###},{playableMaxZ:0.###}] " +
                          $"effectiveCenter X=[{minCenterX:0.###},{maxCenterX:0.###}] Z=[{minCenterZ:0.###},{maxCenterZ:0.###}] " +
                          $"tolerance={PlayableBoundsTolerance:0.##}");
            }
#endif

            return new Vector3(
                candidate.x + (clampedCenterX - futureCenter.x),
                candidate.y,
                candidate.z + (clampedCenterZ - futureCenter.z));
        }

#if UNITY_EDITOR
        // 진단 helper — collider hierarchy path 를 "/Root/Parent/Child" 형식으로 빌드.
        // 좌하단 영역에서 어떤 prefab/fixture 의 어느 깊이 자식 collider 가 hit 되는지 식별.
        private static string BuildHierarchyPath(Transform t)
        {
            if (t == null) return "<null>";
            var sb = new System.Text.StringBuilder();
            for (var cur = t; cur != null; cur = cur.parent)
            {
                if (sb.Length > 0) sb.Insert(0, '/');
                sb.Insert(0, cur.name);
            }
            return sb.ToString();
        }
#endif

        // GameObjects whose renderers share the dragged object's resting
        // y=0 plane (floor / floor-wood) or sit above the room volume
        // (ceiling), plus the drag-grid display. Skipped at cache-build time
        // so the bounds intersection test never false-positives against
        // them. Walls / furniture / other placed decorations are NOT in this
        // list — they must block dragged objects from clipping. Exact-name
        // match (not substring) so unrelated objects like "LampFloor" or
        // "RotatingWallPanel" aren't accidentally excluded.
        private static readonly HashSet<string> RoomShellExactNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Floor",
            "FloorWood",
            "Ceiling",
            "AlcoveCeiling",
            "DecorationDragGrid",
            // PlacementFootprintPreview 의 root GameObject 이름 (line 651 "EditPlacementFootprint").
            // EnsureBlockerCollidersRecursive 가 이 이름을 만나면 자식 traversal 종료 →
            // Fill/Border quad 에 BoxCollider 자동 부착 차단. 1차 차단(IgnoreRaycastLayer) 보강의
            // 2차 방어. **HashSet exact match (OrdinalIgnoreCase)** 라 placed decoration root
            // (`Decoration_{Name}_{Guid}` 패턴) 와 절대 매치되지 않는다 — 선택/드래그 영향 X.
            "EditPlacementFootprint",
        };

        // Reject candidate positions whose bounds would overlap any cached
        // blocker renderer. Uses axis-separated rejection so the dragged
        // object can slide along a blocker face instead of getting stuck at
        // the first contact. If both single-axis sub-moves are also blocked,
        // the dragged object stays at its current position.
        private Vector3 ResolveBlockerCollisions(Vector3 currentPosition, Vector3 candidate, Bounds currentBounds)
        {
            if (Mathf.Approximately(candidate.x, currentPosition.x) && Mathf.Approximately(candidate.z, currentPosition.z))
            {
                return candidate;
            }

            if (!CandidateOverlapsBlocker(candidate, currentBounds, currentPosition))
            {
                return candidate;
            }

            // Try one axis at a time so a head-on collision still allows
            // sliding along the perpendicular axis.
            var xOnly = new Vector3(candidate.x, candidate.y, currentPosition.z);
            var zOnly = new Vector3(currentPosition.x, candidate.y, candidate.z);

            var xOnlyClear = !CandidateOverlapsBlocker(xOnly, currentBounds, currentPosition);
            var zOnlyClear = !CandidateOverlapsBlocker(zOnly, currentBounds, currentPosition);

            if (xOnlyClear && zOnlyClear)
            {
                var dxSq = (xOnly - currentPosition).sqrMagnitude;
                var dzSq = (zOnly - currentPosition).sqrMagnitude;
                return dxSq >= dzSq ? xOnly : zOnly;
            }

            if (xOnlyClear)
            {
                return xOnly;
            }

            if (zOnlyClear)
            {
                return zOnly;
            }

            // No safe sub-move available — keep the object exactly where it is.
            return currentPosition;
        }

        // True if moving the dragged object from `currentPosition` to
        // `candidate` would push its bounds into any non-shell collider
        // in the scene (walls, bookshelf, furniture, other placed
        // decorations).
        //
        // Implementation choices:
        //   1. Multi-step sampling along the path at MinorGridStep
        //      intervals — guarantees a fast drag can't tunnel through a
        //      thin blocker (e.g. an 0.08m wall) just because the start
        //      and end positions both land clear of it.
        //   2. Physics.OverlapBox at each sample — leverages Unity's
        //      spatial physics for fast collider queries. Walls and other
        //      blockers got non-trigger BoxColliders in EnsureBlockerColliders
        //      at edit-mode entry, so this single check handles every
        //      blocker uniformly regardless of whether the prefab shipped
        //      with a collider.
        //   3. Strict "already overlapping" filter via HasVolumetricOverlap
        //      — only skip a blocker if its bounds genuinely intersect the
        //      stationary bounds with non-zero volume. Touching faces
        //      don't trigger the skip, so sliding along a wall still has
        //      every blocker checked for new collisions.
        private bool CandidateOverlapsBlocker(Vector3 candidate, Bounds currentBounds, Vector3 currentPosition)
        {
            var delta = candidate - currentPosition;
            var horizontalDistance = new Vector2(delta.x, delta.z).magnitude;
            var sampleCount = Mathf.Max(1, Mathf.CeilToInt(horizontalDistance / MinorGridStep));

            // Box half-extents for the OverlapBox query: world AABB of the
            // dragged object's renderers, with a tiny Y shrink so a y=0
            // resting plane doesn't false-positive against any floor-
            // coplanar geometry that doesn't have a collider but somehow
            // grew one. The dragged-object's own collider is filtered out
            // by IsBlockerCollider, so we don't worry about self-hits.
            var halfExtents = new Vector3(
                Mathf.Max(0.005f, currentBounds.extents.x),
                Mathf.Max(0.005f, currentBounds.extents.y - BlockerMargin),
                Mathf.Max(0.005f, currentBounds.extents.z));

            var stationaryBounds = new Bounds(currentBounds.center, currentBounds.size);

            for (var step = 1; step <= sampleCount; step++)
            {
                var t = (float)step / sampleCount;
                var sampleCenter = currentBounds.center + delta * t;
                var hits = Physics.OverlapBox(sampleCenter, halfExtents, Quaternion.identity, BlockerLayerMask, QueryTriggerInteraction.Ignore);

                for (var i = 0; i < hits.Length; i++)
                {
                    var hit = hits[i];
                    if (!IsBlockerCollider(hit))
                    {
                        continue;
                    }

                    if (HasVolumetricOverlap(stationaryBounds, hit.bounds))
                    {
                        // Already overlapping at the start position — let
                        // the player slide out of an existing overlap
                        // rather than locking them in.
                        continue;
                    }

                    return true;
                }
            }

            return false;
        }

        // True if `c` is a non-trigger collider that should block the
        // dragged object: not the dragged object's own collider, not part
        // of the room shell (floor / ceiling / drag grid).
        private bool IsBlockerCollider(Collider c)
        {
            if (c == null || c.isTrigger) return false;
            if (draggingObject != null && c.transform.IsChildOf(draggingObject.transform)) return false;

            for (var t = c.transform; t != null; t = t.parent)
            {
                var n = t.name;
                if (!string.IsNullOrEmpty(n) && RoomShellExactNames.Contains(n))
                {
                    return false;
                }
            }

            return true;
        }

        // True only when `a` and `b` share a non-empty *interior* — i.e.
        // their overlap region has positive volume on every axis.
        // Bounds.Intersects returns true even when the boxes merely touch
        // at a face, which causes false "already overlapping" hits when a
        // dragged object sits flush against a blocker. This stricter test
        // skips touching-face cases and only filters out genuine
        // volumetric overlaps (legacy stuck states, etc.).
        private static bool HasVolumetricOverlap(Bounds a, Bounds b)
        {
            const float Epsilon = 0.0005f;
            return a.min.x < b.max.x - Epsilon && a.max.x > b.min.x + Epsilon &&
                   a.min.y < b.max.y - Epsilon && a.max.y > b.min.y + Epsilon &&
                   a.min.z < b.max.z - Epsilon && a.max.z > b.min.z + Epsilon;
        }

        // Kept as a no-op shim for compatibility with the existing
        // BeginDrag call site. The collision check now uses
        // Physics.OverlapBox at multiple samples along the path, so no
        // cached renderer list is needed.
        private void RebuildBlockerRendererCache(GameObject draggedRoot)
        {
        }

        // Floor grid -------------------------------------------------------------

        private void ShowGrid()
        {
            EnsureGridDisplay();
            if (gridDisplayRoot != null)
            {
                gridDisplayRoot.SetActive(true);
            }
        }

        private void HideGrid()
        {
            if (gridDisplayRoot != null)
            {
                gridDisplayRoot.SetActive(false);
            }
        }

        // Build a two-layer grid of LineRenderers along the floor. The major
        // layer steps every MajorGridStep meters and reads clearly; the minor
        // layer steps every MinorGridStep meters with much lower alpha to imply
        // the snap subdivision inside each major cell. Material uses
        // Sprites/Default so the alpha applies cleanly without URP shader
        // plumbing. Lines step from the bottom-left floor corner so the very
        // first line meets the wall (where wall + floor meet) instead of
        // starting halfway across the floor.
        private void EnsureGridDisplay()
        {
            if (gridDisplayRoot != null)
            {
                return;
            }

            gridDisplayRoot = new GameObject("DecorationDragGrid");
            SceneManager.MoveGameObjectToScene(gridDisplayRoot, gameObject.scene);
            gridDisplayRoot.transform.position = new Vector3(0f, GridSurfaceY, 0f);

            var spriteShader = Shader.Find("Sprites/Default");
            var majorMaterial = new Material(spriteShader)
            {
                color = GridLineColor,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var minorMaterial = new Material(spriteShader)
            {
                color = GridMinorLineColor,
                hideFlags = HideFlags.HideAndDontSave,
            };

            // Snapshot bounds locally so the grid uses the same numbers
            // throughout — defends against a stray RefreshPlayableBounds call
            // mid-build (shouldn't happen but cheap to guard).
            var minX = playableMinX;
            var maxX = playableMaxX;
            var minZ = playableMinZ;
            var maxZ = playableMaxZ;

            // Minor lines first (drawn underneath in scene order). Skip
            // positions that coincide with a major-step line so the heavier
            // major line at that position isn't muddied by a co-located minor
            // line. GridLoopEpsilon includes the line at maxX / maxZ when the
            // room dimensions are an exact multiple of the step size.
            for (var x = minX; x <= maxX + GridLoopEpsilon; x += MinorGridStep)
            {
                if (IsAlignedToMajorStep(x, minX))
                {
                    continue;
                }

                CreateGridLine(gridDisplayRoot.transform,
                    new Vector3(x, 0f, minZ),
                    new Vector3(x, 0f, maxZ),
                    minorMaterial,
                    GridMinorLineThickness);
            }
            for (var z = minZ; z <= maxZ + GridLoopEpsilon; z += MinorGridStep)
            {
                if (IsAlignedToMajorStep(z, minZ))
                {
                    continue;
                }

                CreateGridLine(gridDisplayRoot.transform,
                    new Vector3(minX, 0f, z),
                    new Vector3(maxX, 0f, z),
                    minorMaterial,
                    GridMinorLineThickness);
            }

            // Major lines on top so the major spacing remains the dominant
            // visual reference even when the minor grid is dense.
            for (var x = minX; x <= maxX + GridLoopEpsilon; x += MajorGridStep)
            {
                CreateGridLine(gridDisplayRoot.transform,
                    new Vector3(x, 0f, minZ),
                    new Vector3(x, 0f, maxZ),
                    majorMaterial);
            }
            for (var z = minZ; z <= maxZ + GridLoopEpsilon; z += MajorGridStep)
            {
                CreateGridLine(gridDisplayRoot.transform,
                    new Vector3(minX, 0f, z),
                    new Vector3(maxX, 0f, z),
                    majorMaterial);
            }

            // Outer frame (heavier than the inner grid so the playable area reads
            // clearly even if the snap lines are very thin).
            CreateGridLine(gridDisplayRoot.transform,
                new Vector3(minX, 0f, minZ), new Vector3(maxX, 0f, minZ), majorMaterial, GridLineThickness * 2f);
            CreateGridLine(gridDisplayRoot.transform,
                new Vector3(maxX, 0f, minZ), new Vector3(maxX, 0f, maxZ), majorMaterial, GridLineThickness * 2f);
            CreateGridLine(gridDisplayRoot.transform,
                new Vector3(minX, 0f, maxZ), new Vector3(maxX, 0f, maxZ), majorMaterial, GridLineThickness * 2f);
            CreateGridLine(gridDisplayRoot.transform,
                new Vector3(minX, 0f, minZ), new Vector3(minX, 0f, maxZ), majorMaterial, GridLineThickness * 2f);

            gridDisplayRoot.SetActive(false);
        }

        // True when `value` is within half a minor step of a multiple of
        // MajorGridStep measured from `origin`. Used to skip minor-grid lines
        // that would land on top of a major-grid line.
        private static bool IsAlignedToMajorStep(float value, float origin)
        {
            var offset = value - origin;
            var nearestMajor = Mathf.Round(offset / MajorGridStep) * MajorGridStep;
            return Mathf.Abs(offset - nearestMajor) <= GridMajorAlignmentEpsilon;
        }

        private static void CreateGridLine(Transform parent, Vector3 start, Vector3 end, Material material, float thicknessOverride = -1f)
        {
            var lineObject = new GameObject("GridLine");
            lineObject.transform.SetParent(parent, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            var thickness = thicknessOverride > 0f ? thicknessOverride : GridLineThickness;
            line.startWidth = thickness;
            line.endWidth = thickness;
            line.material = material;
            line.numCornerVertices = 0;
            line.numCapVertices = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        private void SelectObject(GameObject target)
        {
            if (selectedObject == target)
            {
                return;
            }
            DeselectCurrentObject();
            selectedObject = target;

            // MR #122 — pending(=preview ghost) 항목은 gold selection tint 를 적용하지 않는다.
            // 반투명 머터리얼 자체가 "선택됨 + 미확정" 시각 cue. 여기서 추가로 _BaseColor 를
            // 건드리면 ghost 색이 깨지고, ConfirmPendingPlacement 에서 머터리얼 복원 시 stale
            // tint backup 이 원본에 새겨져버린다.
            if (pendingPlacements.Contains(target))
            {
                return;
            }

            var renderers = target.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r.material == null) continue;
                var original = r.material.HasProperty("_BaseColor") ? r.material.GetColor("_BaseColor")
                              : r.material.HasProperty("_Color") ? r.material.GetColor("_Color")
                              : Color.white;
                selectedRendererTints.Add(new RendererTint { Renderer = r, OriginalColor = original });
                if (r.material.HasProperty("_BaseColor"))
                {
                    r.material.SetColor("_BaseColor", SelectionTint);
                }
                else if (r.material.HasProperty("_Color"))
                {
                    r.material.SetColor("_Color", SelectionTint);
                }
            }
        }

        private void DeselectCurrentObject()
        {
            for (var i = 0; i < selectedRendererTints.Count; i++)
            {
                var entry = selectedRendererTints[i];
                if (entry.Renderer == null || entry.Renderer.material == null) continue;
                if (entry.Renderer.material.HasProperty("_BaseColor"))
                {
                    entry.Renderer.material.SetColor("_BaseColor", entry.OriginalColor);
                }
                else if (entry.Renderer.material.HasProperty("_Color"))
                {
                    entry.Renderer.material.SetColor("_Color", entry.OriginalColor);
                }
            }
            selectedRendererTints.Clear();
            selectedObject = null;
            // 회귀 fix — selectedObject 가 null 로 떨어지면 다음 Update 의 UpdatePlacementFootprint
            // 가 어차피 Hide 하지만, 그 사이 1 frame 동안 빨간 footprint 가 잔류할 수 있다.
            // 명시 호출로 즉시 정리 — 사용자가 deselect 직후 빨간 사각형이 잔상처럼 남는다고
            // 보고한 회귀 차단. Hide 는 root.SetActive(false) 만 호출, idempotent 라 추가 호출 비용 X.
            placementFootprint?.Hide();
        }

        // 좌회전(반시계방향) 45° — 버튼 핸들러용.
        private void RotateSelectedCounterClockwise()
        {
            Debug.Log(
                $"[EditRotate] button clicked direction=left selectedObject='{(selectedObject != null ? selectedObject.name : "<null>")}' " +
                $"buttonInteractable={(selectionRotateLeftButton != null ? selectionRotateLeftButton.interactable.ToString() : "<null>")}");
            RotateSelectedBy(-SelectionRotateStepDegrees);
        }

        // 우회전(시계방향) 45° — 버튼 핸들러용. Unity 의 Vector3.up 을 축으로 한 양수 각도는
        // 위에서 내려본 시점에서 시계방향 회전이다 (left-handed Y-up).
        private void RotateSelectedClockwise()
        {
            Debug.Log(
                $"[EditRotate] button clicked direction=right selectedObject='{(selectedObject != null ? selectedObject.name : "<null>")}' " +
                $"buttonInteractable={(selectionRotateRightButton != null ? selectionRotateRightButton.interactable.ToString() : "<null>")}");
            RotateSelectedBy(SelectionRotateStepDegrees);
        }

        // 부호 있는 각도(±45°) 로 Y축 회전 후 충돌/방 범위 검증. 실패 시 회전 전 quaternion 으로
        // 즉시 복구. 검증은 PlaceInventoryEntry / UpdateDrag 가 쓰는 ComputeRendererBounds +
        // ClampToFloorBounds + IsSpawnPositionBlockedAt 를 그대로 재사용해서 "배치 검증 = 회전
        // 검증" 으로 일관성을 맞춘다 — 회전 후 AABB 가 방 footprint 를 벗어나거나, 다른 blocker
        // (벽/책장/노트북/기존 배치) 와 겹치면 reverted.
        private void RotateSelectedBy(float signedDegrees)
        {
            if (selectedObject == null)
            {
                Debug.Log($"[EditRotate] aborted reason=selectedObject-null delta={signedDegrees:+#0;-#0;0}°");
                return;
            }
            // 모달 표시 중에는 회전 버튼 자체가 selection 패널과 함께 숨겨져 있어 클릭이 닿지 않지만,
            // 같은 프레임에 stale invocation 이 들어올 가능성에 대비해 방어.
            if (isUnsavedChangesModalOpen)
            {
                Debug.Log($"[EditRotate] aborted reason=unsaved-modal-open obj={selectedObject.name}");
                return;
            }

            // entry-level allowRotation flag — 창문처럼 fixed orientation 이어야 하는
            // wall deco 는 회전 무시. UpdateSelectionPanel 에서 버튼도 disabled 상태이지만
            // 키보드/외부 트리거 가능성 방어.
            var wallMarker = selectedObject.GetComponent<WallDecoMarker>();
            bool allowRotation = !(wallMarker != null && !wallMarker.AllowRotation);
            if (!allowRotation)
            {
                Debug.Log($"[EditRotate] aborted reason=allowRotation-false obj={selectedObject.name} hasWallMarker={wallMarker != null}");
                return;
            }
            // Don't rotate mid-drag — would invalidate the bounds-based placement
            // check until the next move tick. Forces drag to commit first.
            if (draggingObject != null)
            {
                FinishDrag();
            }

            var direction = signedDegrees >= 0f ? "CW" : "CCW";
            var prevRotation = selectedObject.transform.rotation;
            var prevYaw = prevRotation.eulerAngles.y;
            var prevPosition = selectedObject.transform.position;

            // 회귀 fix — "사용자 시점 제자리 회전".
            // 문제: GLB 임포트 시 prefab pivot 이 mesh visual center 와 다르면 (예: pivot 이 mesh
            // 한쪽 모서리, 발바닥, 또는 model export 원점) Transform.Rotate 가 transform.position 을
            // 그대로 두더라도 mesh 가 pivot 주위로 sweep → 사용자가 보기엔 "옆으로 튀고", footprint
            // center 도 sweep 따라 이동 → 이전엔 wall 에 닿는 좌하단 영역에서 회전 검증이 false
            // positive 로 막혔다.
            // 해결: 회전 *전* footprint center 를 anchor 로 캐시. 회전 후 새 footprint center 의
            // XZ drift 만큼 transform.position 을 보정 → mesh visual center (footprint center) 가
            // 회전 전과 동일한 XZ 에 머문다. Y 는 보정 안 함 (verticalOffset 유지: mesh.min.y == FloorY).
            // RotateAround 미사용 — Rotate + 명시 position 보정 조합으로 의도가 명확.
            var preFootprint = ComputeFloorFootprintBounds(selectedObject);
            var preFootprintCenterXZ = preFootprint.HasValue
                ? new Vector3(preFootprint.Value.center.x, prevPosition.y, preFootprint.Value.center.z)
                : prevPosition;

#if UNITY_EDITOR
            Debug.Log($"[BeforeRotate] obj='{selectedObject.name}' worldPos={prevPosition} " +
                      $"localPos={selectedObject.transform.localPosition} rotation={prevRotation.eulerAngles} " +
                      $"footprintCenter={(preFootprint.HasValue ? preFootprint.Value.center.ToString() : "<null>")} " +
                      $"footprintSize={(preFootprint.HasValue ? preFootprint.Value.size.ToString() : "<null>")}");
#endif

            selectedObject.transform.Rotate(Vector3.up, signedDegrees, Space.World);
            // 회귀 fix — 회전 직후 PhysX collider transform stale → 다음 IsSpawnPositionBlockedAt
            // 가 잘못된 OverlapBox 결과를 받음. 명시 sync 로 검증 시점 collider 위치/회전 보정.
            Physics.SyncTransforms();
            var newYaw = selectedObject.transform.rotation.eulerAngles.y;

            // pivot offset 보정 — 회전 후 footprint center 가 회전 전 anchor 와 다르면 그 차이만큼
            // transform.position 을 XZ 로 이동 → 시각적 제자리 회전. drift 가 거의 0 이면 (pivot 이
            // mesh center 와 일치) no-op.
            var midFootprint = ComputeFloorFootprintBounds(selectedObject);
            Vector3 driftCorrection = Vector3.zero;
            if (midFootprint.HasValue)
            {
                driftCorrection = new Vector3(
                    preFootprintCenterXZ.x - midFootprint.Value.center.x,
                    0f,
                    preFootprintCenterXZ.z - midFootprint.Value.center.z);
                if (driftCorrection.sqrMagnitude > 1e-8f)
                {
                    selectedObject.transform.position += driftCorrection;
                    Physics.SyncTransforms();
                }
            }

            string revertReason = null;
            var postRotateFootprint = ComputeFloorFootprintBounds(selectedObject);
#if UNITY_EDITOR
            Debug.Log($"[AfterRotate] obj='{selectedObject.name}' worldPos={selectedObject.transform.position} " +
                      $"localPos={selectedObject.transform.localPosition} rotation={selectedObject.transform.rotation.eulerAngles} " +
                      $"footprintCenter={(postRotateFootprint.HasValue ? postRotateFootprint.Value.center.ToString() : "<null>")} " +
                      $"footprintSize={(postRotateFootprint.HasValue ? postRotateFootprint.Value.size.ToString() : "<null>")} " +
                      $"pivotDriftCorrection={driftCorrection}");
#endif
            if (postRotateFootprint.HasValue)
            {
                var bounds = postRotateFootprint.Value;
                var pos = selectedObject.transform.position;

                // 회귀 fix — 회전 후 OutOfRoom 판정을 *footprint center* 기준으로 단순화.
                // 이전엔 ClampToFloorBounds 의 push-back distance 로 판정했는데, 회전 후 World AABB
                // 가 √2 배까지 부풀어 (45° 회전 시) extents 일부가 5cm tolerance 를 초과 → push-back
                // → false-positive OutOfRoom. 사용자 시점에서는 mesh 가 wall 을 뚫지 않으면 통과
                // 해야 한다. 실제 wall 침범은 아래 IsSpawnPositionBlockedAt 의 wall collider hit
                // (RoomShellExactNames 에 wall 이 없어 blocker 로 잡힘) 이 잡으므로 OutOfRoom 은
                // center 가 raw playable + tolerance 밖일 때만 true. drag/spawn 은 ClampToFloorBounds
                // 의 push-back 그대로 — drag 는 candidate 를 부드럽게 정지시키는 의미라 다른 정책.
                var fpCenter = bounds.center;
                bool outOfRoom =
                    fpCenter.x < playableMinX - PlayableBoundsTolerance ||
                    fpCenter.x > playableMaxX + PlayableBoundsTolerance ||
                    fpCenter.z < playableMinZ - PlayableBoundsTolerance ||
                    fpCenter.z > playableMaxZ + PlayableBoundsTolerance;
                bool blocked = !outOfRoom && IsSpawnPositionBlockedAt(selectedObject, bounds, pos);
#if UNITY_EDITOR
                // 진단 — footprint min/max + center + extents + raw/effective playable + 어느 axis
                // 가 얼마나 넘어갔는지. OutOfRoom 회귀 시 즉시 root cause 확인 가능.
                string overflowAxis = "";
                float overflowAmount = 0f;
                if (outOfRoom)
                {
                    if (fpCenter.x < playableMinX - PlayableBoundsTolerance)
                    { overflowAxis = "minX"; overflowAmount = (playableMinX - PlayableBoundsTolerance) - fpCenter.x; }
                    else if (fpCenter.x > playableMaxX + PlayableBoundsTolerance)
                    { overflowAxis = "maxX"; overflowAmount = fpCenter.x - (playableMaxX + PlayableBoundsTolerance); }
                    else if (fpCenter.z < playableMinZ - PlayableBoundsTolerance)
                    { overflowAxis = "minZ"; overflowAmount = (playableMinZ - PlayableBoundsTolerance) - fpCenter.z; }
                    else if (fpCenter.z > playableMaxZ + PlayableBoundsTolerance)
                    { overflowAxis = "maxZ"; overflowAmount = fpCenter.z - (playableMaxZ + PlayableBoundsTolerance); }
                }
                Debug.Log($"[EditRotateRoomCheck] obj='{selectedObject.name}' source=Rotate " +
                          $"beforePos={prevPosition} afterPos={pos} " +
                          $"beforeRot={prevYaw:0.#}° afterRot={newYaw:0.#}° " +
                          $"footprintMin={bounds.min} footprintMax={bounds.max} " +
                          $"footprintCenter={bounds.center} footprintExtents={bounds.extents} " +
                          $"playableRaw X=[{playableMinX:0.###},{playableMaxX:0.###}] Z=[{playableMinZ:0.###},{playableMaxZ:0.###}] " +
                          $"playableEffective X=[{playableMinX - PlayableBoundsTolerance:0.###},{playableMaxX + PlayableBoundsTolerance:0.###}] " +
                          $"Z=[{playableMinZ - PlayableBoundsTolerance:0.###},{playableMaxZ + PlayableBoundsTolerance:0.###}] " +
                          $"tolerance={PlayableBoundsTolerance:0.##}m outOfRoom={outOfRoom} " +
                          $"exceededAxis='{overflowAxis}' exceededAmount={overflowAmount:0.###}m blocked={blocked}");
#endif
                if (outOfRoom)
                {
                    revertReason = "out-of-room";
                }
                else if (blocked)
                {
                    revertReason = "blocker-overlap";
                }
            }

            if (revertReason != null)
            {
                // revert: rotation + position 둘 다 회전 전으로 복원 (pivot 보정으로 position 이
                // 이동했을 수 있으므로 명시적으로 prevPosition 복귀).
                selectedObject.transform.SetPositionAndRotation(prevPosition, prevRotation);
                Physics.SyncTransforms();
                Debug.Log(
                    $"[RoomEdit][Rotate] dir={direction} delta={signedDegrees:+#0;-#0;0}° from={prevYaw:0.#}° to={newYaw:0.#}° " +
                    $"result=reverted reason={revertReason} obj={selectedObject.name}");
                Debug.Log(
                    $"[EditRotate] obj={selectedObject.name} allowRotation={allowRotation} " +
                    $"before={prevYaw:0.#}° after={newYaw:0.#}° validation=fail reason={revertReason}");
                // MR #133 — 회전 시도가 invalid 로 reverted 됐음을 placement 로그 채널에도 동일 형식으로 기록.
                // 회전 자체는 즉시 revert 되어 시각 상태가 valid 로 유지되므로 별도 tint 변경은 없다.
                var placementReason = revertReason == "out-of-room" ? "OutOfRoom" : "OverlapWithObstacle";
                Debug.Log($"[EditPlacement] placement valid=False object='{selectedObject.name}' reason='{placementReason}' source='Rotate'");
                return;
            }

            Debug.Log(
                $"[RoomEdit][Rotate] dir={direction} delta={signedDegrees:+#0;-#0;0}° from={prevYaw:0.#}° to={newYaw:0.#}° " +
                $"result=ok obj={selectedObject.name}");
            Debug.Log(
                $"[EditRotate] obj={selectedObject.name} allowRotation={allowRotation} " +
                $"before={prevYaw:0.#}° after={newYaw:0.#}° validation=ok");
            SetDirty($"RotateSelected/{direction}");
        }

        private void DeleteSelectedObject()
        {
            if (selectedObject == null) return;
            // 모달이 열린 동안 stale invocation 차단 (RotateSelectedBy 와 동일 이유).
            if (isUnsavedChangesModalOpen) return;
            if (draggingObject != null)
            {
                // Drop the in-flight drag state cleanly before destroying so the
                // next pointer event doesn't see a torn-down object.
                draggingObject = null;
                HideGrid();
            }
            var toDestroy = selectedObject;

            // 인벤토리 1회 배치 제한 캐시에서도 제거 — 안 하면 같은 슬롯이 영구 disabled 로 남는다.
            // marker 가 없는 case (사전에 부착 실패 등) 도 방어.
            var marker = toDestroy != null ? toDestroy.GetComponent<DecorationItemMarker>() : null;
            var deletedKey = marker != null ? marker.InventoryKey : null;
            if (!string.IsNullOrEmpty(deletedKey) && placedByInventoryKey.TryGetValue(deletedKey, out var tracked) && tracked == toDestroy)
            {
                placedByInventoryKey.Remove(deletedKey);
                Debug.Log($"[RoomEdit][Inventory] 삭제 — key={deletedKey} 인벤토리 재활성화.");
            }

            // MR #122 — pending 상태였으면 set/backup 도 정리. destroy 후엔 Renderer 도 사라지므로
            // material 복원은 의미 없음 — backup dictionary 의 stale entry 만 청소한다.
            CleanupPendingState(toDestroy);

            DeselectCurrentObject();
            Destroy(toDestroy);

            // 슬롯의 disabled/배치됨 표시 해제. 같은 자산을 다시 클릭해서 배치 가능하게 됨.
            RebuildInventorySlots();
            SetDirty("DeleteSelectedObject");
        }

        // Helpers ------------------------------------------------------------------

        private BookshelfExperienceManager ResolveExperienceManager()
        {
            if (cachedExperienceManager != null)
            {
                return cachedExperienceManager;
            }
            cachedExperienceManager = UnityEngine.Object.FindFirstObjectByType<BookshelfExperienceManager>(FindObjectsInactive.Include);
            return cachedExperienceManager;
        }

        private void TryUploadRoomThumbnail()
        {
            var apiSync = ResolveBookshelfApiSyncController();
            if (apiSync == null)
            {
                return;
            }

            if (!apiSync.UploadCurrentRoomThumbnail(
                    onFailed: error => Debug.LogWarning($"[RoomEditModeController] Room thumbnail upload skipped/failed: {error}")))
            {
                Debug.LogWarning("[RoomEditModeController] Room thumbnail upload skipped: API sync is unavailable.");
            }
        }

        private BookshelfApiSyncController ResolveBookshelfApiSyncController()
        {
            if (cachedBookshelfApiSyncController != null)
            {
                return cachedBookshelfApiSyncController;
            }

            cachedBookshelfApiSyncController = UnityEngine.Object.FindFirstObjectByType<BookshelfApiSyncController>(FindObjectsInactive.Include);
            return cachedBookshelfApiSyncController;
        }

        private Camera ResolveSceneCamera()
        {
            if (cachedSceneCamera != null)
            {
                return cachedSceneCamera;
            }
            cachedSceneCamera = Camera.main;
            return cachedSceneCamera;
        }

        private static bool IsLeftMouseDownThisFrame()
        {
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        }

        private static Vector2 ReadPointerPosition()
        {
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        }

        // 편집 모드에서 클릭이 "차단해야 할 UI" 위인지 판정 (MR #129).
        //
        // 한 줄짜리 IsPointerOverGameObject() 는 일부 Unity Input System 설정에서 WorldSpace
        // 캔버스 hit 까지 true 로 보고해 데코 클릭/드래그가 막히는 회귀가 있어 RaycastAll 기반으로
        // 명시적으로 분기. 두 종류의 "월드 위 UI" 를 차단 사유에서 제외:
        //   1. uGUI WorldSpace Canvas hit (노트북 스크린 / guestbook preview 등) — Canvas.renderMode
        //      로 식별.
        //   2. UI Toolkit WorldSpace UIDocument hit (dashboard 등) — module 이
        //      UnityEngine.UIElements.WorldDocumentRaycaster 인 경우 식별. uGUI 와 별개 시스템이라
        //      Canvas 가 없어 1번 체크에 안 잡힘 — 별도 분기 필요.
        // 둘 다 본질적으로 월드 오브젝트의 일부이므로 클릭 = 월드 클릭. 실제 HUD(편집 패널/모달/
        // 팝업/인벤토리/취소·저장 버튼 등 ScreenSpaceOverlay·ScreenSpaceCamera Canvas, 또는
        // ScreenSpace UIDocument = UIElements.PanelRaycaster) hit 만 차단 사유로 인정해 return true.
        //
        // MR #129 임시 진단 로그 — [EditUiHit] prefix 로 RaycastAll hit 별 세부 정보(name/module/
        // canvas/renderMode/sortingLayer·Order/raycastTarget)를 출력해 어떤 UI 가 실제로 차단을
        // 만드는지 확정한다. 원인 fix 후 Debug.Log 라인들은 제거할 것.
        private static bool IsPointerOverUi()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            var pointerPosition = ReadPointerPosition();
            var pointerEvent = new PointerEventData(eventSystem)
            {
                position = pointerPosition,
            };
            var hits = new List<RaycastResult>();
            eventSystem.RaycastAll(pointerEvent, hits);

            Debug.Log($"[EditUiHit] RaycastAll → {hits.Count} hits at pointer={pointerPosition}");

            for (var i = 0; i < hits.Count; i++)
            {
                var hitGo = hits[i].gameObject;
                var moduleName = hits[i].module != null ? hits[i].module.GetType().Name : "<null>";
                var moduleFullName = hits[i].module != null ? hits[i].module.GetType().FullName : "<null>";
                var hitGoName = hitGo != null ? hitGo.name : "<null>";
                var canvas = hitGo != null ? hitGo.GetComponentInParent<Canvas>() : null;
                var canvasName = canvas != null ? canvas.name : "<none>";
                var renderMode = canvas != null ? canvas.renderMode.ToString() : "<n/a>";
                var sortingLayer = canvas != null ? canvas.sortingLayerName : "<n/a>";
                var sortingOrder = canvas != null ? canvas.sortingOrder.ToString() : "<n/a>";
                var graphic = hitGo != null ? hitGo.GetComponent<UnityEngine.UI.Graphic>() : null;
                var raycastTarget = graphic != null ? graphic.raycastTarget.ToString() : "<noGraphic>";

                if (hitGo == null)
                {
                    Debug.Log($"[EditUiHit] [{i}] gameObject=null module={moduleName} → skip (no GO)");
                    continue;
                }

                // UI Toolkit (UIElements) WorldSpace UIDocument hit — uGUI Canvas 와 별개 시스템.
                // dashboard 같은 WorldSpace UIDocument 는 본질적으로 월드 오브젝트의 일부이므로
                // 차단 사유 아님. PanelRaycaster (ScreenSpace UIDocument) 는 여기서 skip 안 됨 →
                // 아래 fall-through 경로에서 BLOCKED.
                if (moduleFullName == "UnityEngine.UIElements.WorldDocumentRaycaster")
                {
                    Debug.Log($"[EditUiHit] [{i}] hit='{hitGoName}' module={moduleName} → SKIP (UI Toolkit WorldSpace)");
                    continue;
                }

                if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
                {
                    // uGUI WorldSpace 캔버스 = 월드 오브젝트의 일부. 클릭 = 월드 클릭이지 UI 차단 아님.
                    Debug.Log($"[EditUiHit] [{i}] hit='{hitGoName}' module={moduleName} canvas='{canvasName}' renderMode={renderMode} sortingLayer='{sortingLayer}' sortingOrder={sortingOrder} raycastTarget={raycastTarget} → SKIP (WorldSpace)");
                    continue;
                }

                // ScreenSpaceOverlay / ScreenSpaceCamera 캔버스 또는 ScreenSpace UIDocument
                // (UIElements.PanelRaycaster) 는 진짜 HUD 로 간주해 차단.
                Debug.Log($"[EditUiHit] [{i}] hit='{hitGoName}' module={moduleName} canvas='{canvasName}' renderMode={renderMode} sortingLayer='{sortingLayer}' sortingOrder={sortingOrder} raycastTarget={raycastTarget} → BLOCKED");
                return true;
            }

            return false;
        }

        private static bool IsDescendantOf(Transform candidate, Transform ancestor)
        {
            if (ancestor == null || candidate == null) return false;
            var t = candidate;
            while (t != null)
            {
                if (t == ancestor) return true;
                t = t.parent;
            }
            return false;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (existing != null) return;
            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            eventSystemObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
        }

        // UI factory helpers (lightweight subset of patterns used by the other
        // controllers in this project — kept self-contained on purpose so this
        // controller can be enabled/disabled independently).

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UiLayer;
            go.transform.SetParent(parent, false);
            return go;
        }

        private Image CreateImage(Transform parent, string name, Color color)
        {
            var go = CreateRectObject(name, parent);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.sprite = whiteSprite;
            image.type = Image.Type.Simple;
            return image;
        }

        private TMP_Text CreateText(Transform parent, string name, string text, float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment)
        {
            var go = CreateRectObject(name, parent);
            var label = go.AddComponent<TextMeshProUGUI>();
            ApplyFontToText(label);
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        // Apply the resolved Korean TMP font + matching shared material, and register
        // the fallback asset so any missing glyph falls through to a font that has it
        // instead of rendering as □.
        private void ApplyFontToText(TMP_Text text)
        {
            if (text == null || fontAsset == null)
            {
                return;
            }

            text.font = fontAsset;
            if (fontAsset.material != null)
            {
                text.fontSharedMaterial = fontAsset.material;
            }

            if (fallbackFontAsset != null && text.font != null && text.font != fallbackFontAsset)
            {
                var fallbackTable = text.font.fallbackFontAssetTable;
                if (fallbackTable == null)
                {
                    fallbackTable = new List<TMP_FontAsset>();
                    text.font.fallbackFontAssetTable = fallbackTable;
                }

                if (!fallbackTable.Contains(fallbackFontAsset))
                {
                    fallbackTable.Add(fallbackFontAsset);
                }
            }
        }

        private Button CreateButton(Transform parent, string name, string label, Color background, Color labelColor, Vector2 size, bool outlined, Color outlineColor)
        {
            var image = CreateImage(parent, name, background);
            image.sprite = roundedRectSprite;
            image.type = Image.Type.Sliced;
            var rect = image.rectTransform;
            rect.sizeDelta = size;

            if (outlined)
            {
                var outline = image.gameObject.AddComponent<Outline>();
                outline.effectColor = WithAlpha(outlineColor, 0.95f);
                outline.effectDistance = new Vector2(1f, -1f);
            }

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var text = CreateText(image.transform, "Label", label, Mathf.Min(26f, size.y * 0.45f), FontStyles.Bold, labelColor, TextAlignmentOptions.Center);
            StretchToParent(text.rectTransform);

            return button;
        }

        // 버튼 Label 텍스트의 좌우 inset margin 을 부여한다. CreateButton 의 default 는
        // StretchToParent 로 텍스트가 버튼 RectTransform 전체를 채우는데, 한글이 길면
        // (예: "저장하지 않고 나가기") 좌우 경계까지 텍스트가 닿아 답답해 보인다.
        // TMP_Text.margin 은 Vector4(left, top, right, bottom).
        private static void ApplyButtonLabelHorizontalMargin(Button button, float horizontalMargin)
        {
            if (button == null) return;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;
            var m = label.margin;
            label.margin = new Vector4(horizontalMargin, m.y, horizontalMargin, m.w);
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        private static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var color))
            {
                return color;
            }
            return Color.white;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        private static Sprite CreateWhiteSprite()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "RoomEditCanvas_White",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateRoundedRectSprite(int size, int radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RoomEditCanvas_Rounded",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var inX = Mathf.Clamp(x, radius, size - 1 - radius);
                    var inY = Mathf.Clamp(y, radius, size - 1 - radius);
                    var dx = x - inX;
                    var dy = y - inY;
                    var distSq = dx * dx + dy * dy;
                    var alpha = distSq <= radius * radius ? (byte)255 : (byte)0;
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var border = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        // Mirrors the resolution path used by Login/Onboarding/Settings controllers:
        //   1. Look up the project's Korean TMP font assets by name (resolves regardless
        //      of whether they live under a Resources folder).
        //   2. Fall back to a runtime-built dynamic atlas from any TMP font already in
        //      the scene (covers the case where the project ships only Built-in TMP
        //      defaults but a different font instance is in use).
        //   3. Last resort: the TMP default. This keeps the UI legible even if the
        //      Korean assets were renamed or unloaded.
        private TMP_FontAsset ResolveFontAsset()
        {
            var preferredFontAsset = FindFontAssetByName(PreferredKoreanFontAssetName);
            if (preferredFontAsset != null)
            {
                fallbackFontAsset = preferredFontAsset;
                return preferredFontAsset;
            }

            preferredFontAsset = FindFontAssetByName(FallbackKoreanFontAssetName);
            if (preferredFontAsset != null)
            {
                fallbackFontAsset = preferredFontAsset;
                return preferredFontAsset;
            }

            fallbackFontAsset = TMP_Settings.defaultFontAsset;

            var existingText = UnityEngine.Object.FindFirstObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include);
            var candidateFontAsset = existingText != null ? existingText.font : TMP_Settings.defaultFontAsset;

            if (TryCreateDynamicFontAsset(candidateFontAsset, out var dynamicFontAsset))
            {
                fallbackFontAsset = candidateFontAsset;
                return dynamicFontAsset;
            }

            if (TryCreateDynamicFontAsset(fallbackFontAsset, out dynamicFontAsset))
            {
                return dynamicFontAsset;
            }

            if (candidateFontAsset != null)
            {
                return candidateFontAsset;
            }

            return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        private static TMP_FontAsset FindFontAssetByName(string fontAssetName)
        {
            if (string.IsNullOrWhiteSpace(fontAssetName))
            {
                return null;
            }

            var fontAssets = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (var candidate in fontAssets)
            {
                if (candidate != null && string.Equals(candidate.name, fontAssetName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
            return null;
        }

        private static bool TryCreateDynamicFontAsset(TMP_FontAsset sourceFontAsset, out TMP_FontAsset dynamicFontAsset)
        {
            dynamicFontAsset = null;
            if (sourceFontAsset == null || sourceFontAsset.sourceFontFile == null)
            {
                return false;
            }

            try
            {
                dynamicFontAsset = TMP_FontAsset.CreateFontAsset(
                    sourceFontAsset.sourceFontFile,
                    90,
                    9,
                    UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                    1024,
                    1024,
                    AtlasPopulationMode.Dynamic,
                    true);
                if (dynamicFontAsset == null)
                {
                    return false;
                }
                dynamicFontAsset.name = $"{sourceFontAsset.name}_RoomEditDynamic";
                dynamicFontAsset.hideFlags = HideFlags.HideAndDontSave;
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Pre-warm the dynamic atlas with the strings this controller's UI actually
        // displays so the player doesn't see a one-frame □ flash the first time a
        // panel/popup/modal appears.
        private static void WarmupGlyphs(TMP_FontAsset targetFontAsset)
        {
            if (targetFontAsset == null || targetFontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            {
                return;
            }

            const string roomEditCharacters =
                "꾸미기 편집 샵 돌아가기 " +
                "전체 가구 벽지 내부조명 외부조명 필수 오브젝트 스킨 전체 테마 " +
                "보유 인벤토리 " +
                "회전 삭제 " +
                "준비 중입니다 확인";
            targetFontAsset.TryAddCharacters(roomEditCharacters, out _);
        }
    }
}
