using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using App.Application.Dashboard;
using App.Application.Decoration;
using App.Auth;
using App.Domain.Decoration;
using App.Infrastructure.Api;
using App.Infrastructure.Api.Dashboard;
using App.Infrastructure.Api.Dtos;
using App.Infrastructure.Assets;
using App.Infrastructure.Catalogs;
using App.Infrastructure.Decoration;
using BookshelfPorting.Runtime;
using Dotshelf.Scripts.Dashboard;
using Dotshelf.Scripts.RoomTheme;
using GLTFast;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
            ("FURNITURE", "필수"),
            ("WALLPAPER", "벽지"),
            ("LIGHT_INNER", "내부조명"),
            ("LIGHT_OUTER", "외부조명"),
            ("OBJECT_SKIN", "필수 오브젝트 스킨"),
            ("THEME_FULL", "전체 테마"),
            ("SHOP", "샵"),
        };

        // FURNITURE 탭 선택 시 노출되는 sub-tab. CustomizationCategory의 필수 외형 요소.
        // (의자는 Desk = '노트북 & 책상 세트'에 묶여있어 별도 sub-tab 불필요.)
        private static readonly (string Key, string Label)[] FurnitureSubItems =
        {
            ("DESK", "책상"),
            ("BOOKSHELF_SKIN", "책장"),
            ("DASHBOARD_SKIN", "대시보드"),
            ("NOTEBOOK", "노트북"),
        };

        private static readonly CustomizationCategory[] AllCatalogCategories =
        {
            CustomizationCategory.Desk,
            CustomizationCategory.BookshelfSkin,
            CustomizationCategory.DashboardSkin,
            CustomizationCategory.Notebook,
            CustomizationCategory.Wallpaper,
            CustomizationCategory.InteriorLight,
            CustomizationCategory.ExteriorLight,
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
            public bool IsRemoteAsset;
            public long RemoteAssetId;
            public long RemoteInventoryId;
            public string PreviewImageUrl;
            public string ThemeTag;
            public string ServerAssetType;
            public string ServerCategoryCode;
        }

        [Serializable]
        private sealed class DecorationLayoutPayload
        {
            public List<DecorationLayoutItem> items = new List<DecorationLayoutItem>();
        }

        // 인벤토리 동일 자산 1회 배치 제한용 캐시. Key 는 BuildInventoryKey 가 만든 stable
        // identity (SourceAssetPath → DisplayName → Prefab.name 순). value 는 씬에 살아있는
        // GameObject reference — Destroy 후 dangling 이 되면 PruneInvalidPlacedEntries 가 정리.
        // 서버 인벤토리(assetId/inventoryId) 도입 시 BuildInventoryKey 만 교체하면 그대로 호환.
        private readonly Dictionary<string, GameObject> placedByInventoryKey = new Dictionary<string, GameObject>();

        // Public state ------------------------------------------------------------

        public bool IsEditModeActive { get; private set; }
        public bool IsDecorationPopupOpen { get; private set; }
        public bool IsReadOnlySpacePreview { get; private set; }

        public void SetMainDecorationButtonSuppressed(bool suppressed)
        {
            if (suppressMainDecorationButton == suppressed)
            {
                return;
            }

            suppressMainDecorationButton = suppressed;
            UpdateButtonVisibility();
        }

        public void OpenDecorationEditFromQuickMenu()
        {
            InitializeIfNeeded();

            if (IsReadOnlySpacePreview || IsEditModeActive)
            {
                return;
            }

            CloseDecorationPopupInternal();
            EnterEditMode();
        }

        public void SetReadOnlySpacePreview(bool isReadOnly)
        {
            if (!isReadOnly)
            {
                ClearVisibleSpaceAssetLookupMaps();
            }
            else
            {
                InvalidatePendingThemeContentsReapplyRequests("read-only space preview");
            }

            if (IsReadOnlySpacePreview == isReadOnly)
            {
                return;
            }

            IsReadOnlySpacePreview = isReadOnly;
            if (isReadOnly && IsDecorationPopupOpen)
            {
                CloseDecorationPopupInternal();
            }

            UpdateButtonVisibility();
        }

        public void RegisterVisibleSpaceAssetManifests(IReadOnlyList<DownloadManifestResponseDto> manifests)
        {
            InitializeIfNeeded();
            ClearVisibleSpaceAssetLookupMaps();
            if (manifests == null)
            {
                return;
            }

            for (var i = 0; i < manifests.Count; i++)
            {
                var manifest = manifests[i];
                if (manifest != null && manifest.AssetId > 0L)
                {
                    visibleRemoteAssetManifests[manifest.AssetId] = manifest;
                    if (manifest.InventoryId.HasValue && manifest.InventoryId.Value > 0L)
                    {
                        visibleAssetIdByInventoryId[manifest.InventoryId.Value] = manifest.AssetId;
                    }

                    var resourceKey = NormalizeUnityResourceKey(manifest.UnityResourceKey);
                    if (!string.IsNullOrEmpty(resourceKey))
                    {
                        visibleAssetIdByResourceKey[resourceKey] = manifest.AssetId;
                    }

                    if (TryBuildLocalEntryFromManifest(manifest, out var localEntry))
                    {
                        visibleLocalAssetEntries[manifest.AssetId] = localEntry;
                        if (manifest.InventoryId.HasValue && manifest.InventoryId.Value > 0L)
                        {
                            visibleLocalAssetEntriesByInventoryId[manifest.InventoryId.Value] = localEntry;
                        }
                        if (!string.IsNullOrEmpty(resourceKey))
                        {
                            visibleLocalAssetEntriesByResourceKey[resourceKey] = localEntry;
                        }
                    }
                }
            }
        }

        public void RegisterOwnedAssetInventory(InventoryResponseDto inventory)
        {
            InitializeIfNeeded();
            MergeRemoteAssetInventory(inventory);
            RebuildInventorySlots();
        }

        private void ClearVisibleSpaceAssetLookupMaps()
        {
            visibleRemoteAssetManifests.Clear();
            visibleLocalAssetEntries.Clear();
            visibleLocalAssetEntriesByInventoryId.Clear();
            visibleAssetIdByInventoryId.Clear();
            visibleLocalAssetEntriesByResourceKey.Clear();
            visibleAssetIdByResourceKey.Clear();
        }

        private bool TryBuildLocalEntryFromManifest(DownloadManifestResponseDto manifest, out InventoryEntry entry)
        {
            entry = default;
            if (manifest == null || manifest.AssetId <= 0L)
            {
                return false;
            }

            if (!TryFindLocalCatalogEntryByUnityResourceKey(manifest.UnityResourceKey, out var localEntry))
            {
                return false;
            }

            // Server-linked local entry: instantiate the prefab already shipped in Unity,
            // but preserve RemoteAssetId so layout save/restore uses asset:{id}.
            localEntry.DisplayName = FirstNonEmpty(manifest.Name, manifest.Code, localEntry.DisplayName);
            localEntry.RemoteAssetId = manifest.AssetId;
            localEntry.RemoteInventoryId = manifest.InventoryId ?? 0L;
            localEntry.IsRemoteAsset = false;
            localEntry.PreviewImageUrl = FirstNonEmpty(manifest.PreviewImageUrl, localEntry.PreviewImageUrl);
            localEntry.ThemeTag = FirstNonEmpty(NormalizeThemeTag(manifest.Theme), localEntry.ThemeTag);
            localEntry.ServerAssetType = manifest.AssetType;
            entry = localEntry;
            return true;
        }

        // Internal -----------------------------------------------------------------

        private bool isInitialized;
        private TMP_FontAsset fontAsset;
        private TMP_FontAsset fallbackFontAsset;
        private Sprite whiteSprite;
        private Sprite roundedRectSprite;

        private Canvas canvas;
        private GameObject decorationButtonRoot;
        private GameObject decorationPopupRoot;
        private bool suppressMainDecorationButton;
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
        private LoadThemeContentsUseCase themeContentsUseCase;
        private RoomThemeAsset previousAppliedTheme;
        private RoomThemeAsset pendingThemeApply;

        // 테마 spec → spawn 대상 GameObject 변환. MVP 는 spec.Prefab 즉시 반환 (로컬). 미래
        // S3 도입 시 RemoteThemeAssetLoader 로 교체만 하면 spawn 코드 변화 없음.
        private readonly IThemeAssetLoader themeAssetLoader = new LocalThemeAssetLoader();
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

        // 대시보드(GuestbookBoard) focus 진입 시 placedItemsRoot 자식(가구/장식)을 반투명으로
        // fade — 사용자가 대시보드를 정면으로 볼 때 가구가 화면을 가리지 않도록 임시 처리.
        // Renderer.material instance 만 swap, sharedMaterial 원본은 보존. 이탈 시 원복.
        // wall fade 와 동일한 instance-material 패턴이라 동시 활성화돼도 서로 다른 Renderer
        // 집합을 다룬다 (placedItemsRoot 자식 vs scene 의 LeftWall/RightWall/...).
        private sealed class PlacedItemFadeCache
        {
            public Renderer Renderer;
            // multi-material Renderer 대응 (MR #182 skh1205 리뷰 #3): material slot 전체 snapshot/instance.
            // hidden fallback 시 InstanceMaterials 는 Destroy 후 null. 첫 slot 만 instance 처리하던
            // 이전 구조는 submesh 가 여러 material slot 가진 책상/가구 GLB 에서 일부 면이 opaque 로 남는
            // 회귀 (fade 적용 로그는 성공인데 시각적으로는 가림).
            public Material[] InstanceMaterials;
            public Material[] SharedOriginals;
            public bool RendererWasEnabled;
        }
        private readonly List<PlacedItemFadeCache> placedItemFadeCaches = new List<PlacedItemFadeCache>();
        private bool placedItemFadeActive;
        [SerializeField, Range(0f, 1f)] private float guestbookFocusFadeAlpha = 0.15f;

        // NotebookStation 자식 ScreenSaverCanvas 등 World-Space Canvas — Graphic/CanvasRenderer 라
        // Renderer.material alpha 로는 못 가린다. Canvas.enabled 토글로 별도 처리.
        private struct CanvasFadeCache
        {
            public Canvas Canvas;
            public bool WasEnabled;
        }
        private readonly List<CanvasFadeCache> canvasFadeCaches = new List<CanvasFadeCache>();

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
        private readonly Dictionary<CustomizationCategory, CustomizationEntry> pendingCategoryVisualSaves =
            new Dictionary<CustomizationCategory, CustomizationEntry>();
        private bool hasPendingThemeVisualSave;
        private RoomThemeAsset pendingThemeVisualSave;
        private ThemeApplyMode pendingThemeVisualSaveMode;
        private RoomThemeAsset editEntryThemeSnapshot;
        private SpaceResponseDto editEntryServerSpaceSnapshot;
        private readonly Dictionary<CustomizationCategory, long> editEntryAppliedEntriesSnapshot =
            new Dictionary<CustomizationCategory, long>();

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
        private RemoteAssetCacheService remoteAssetCacheService;
        private bool isRemoteAssetInventoryLoading;
        private int remoteAssetInventoryRequestVersion;
        private bool remoteAssetInventoryLoaded;
        private bool pendingSpaceVisualsReapplyAfterInventory;
        private int pendingSpaceVisualsReapplyCategoryVersion;
        private readonly Dictionary<long, long> ownedAssetIdByInventoryId = new Dictionary<long, long>();
        private readonly Dictionary<long, long> ownedInventoryIdByAssetId = new Dictionary<long, long>();
        private readonly Dictionary<long, AssetResponseDto> ownedAssetsByAssetId = new Dictionary<long, AssetResponseDto>();
        private readonly Dictionary<long, ThemeContents> themeContentsCache = new Dictionary<long, ThemeContents>();
        private readonly Dictionary<long, int> themeContentsInFlightVersions = new Dictionary<long, int>();
        private readonly Dictionary<long, int> themeContentsRequestVersions = new Dictionary<long, int>();
        private readonly Dictionary<long, string> themeContentsSignatures = new Dictionary<long, string>();
        private readonly Dictionary<long, float> themeContentsLastRefreshRealtime = new Dictionary<long, float>();
        private readonly Dictionary<long, DownloadManifestResponseDto> visibleRemoteAssetManifests = new Dictionary<long, DownloadManifestResponseDto>();
        private readonly Dictionary<long, InventoryEntry> visibleLocalAssetEntries = new Dictionary<long, InventoryEntry>();
        private readonly Dictionary<long, InventoryEntry> visibleLocalAssetEntriesByInventoryId = new Dictionary<long, InventoryEntry>();
        private readonly Dictionary<long, long> visibleAssetIdByInventoryId = new Dictionary<long, long>();
        private readonly Dictionary<string, InventoryEntry> visibleLocalAssetEntriesByResourceKey = new Dictionary<string, InventoryEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> visibleAssetIdByResourceKey = new Dictionary<string, long>(StringComparer.Ordinal);
        private int externalDecorationLayoutVersion;
        private readonly Dictionary<string, Sprite> inventoryPreviewSpriteCache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly HashSet<string> inventoryPreviewLoadsInFlight = new HashSet<string>(StringComparer.Ordinal);
        private const int MaxInventoryPreviewLoadsInFlight = 4;
        private const float ThemeContentsBackgroundRefreshMinIntervalSeconds = 15f;

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
        private sealed class ThemeContentTransform
        {
            public string AssetKey { get; set; }
            public string PlacementCategory { get; set; }
            public string WallSide { get; set; }
            public string ThemeSlot { get; set; }
            public float PositionX { get; set; }
            public float PositionY { get; set; }
            public float PositionZ { get; set; }
            public float RotationX { get; set; }
            public float RotationY { get; set; }
            public float RotationZ { get; set; }
            public float ScaleX { get; set; } = 1f;
            public float ScaleY { get; set; } = 1f;
            public float ScaleZ { get; set; } = 1f;

            public Vector3 LocalPosition => new Vector3(PositionX, PositionY, PositionZ);
            public Vector3 LocalRotation => new Vector3(RotationX, RotationY, RotationZ);
            public Vector3 LocalScale => new Vector3(ScaleX, ScaleY, ScaleZ);

            public bool HasUsableScale =>
                IsFinite(ScaleX) && IsFinite(ScaleY) && IsFinite(ScaleZ) &&
                Mathf.Abs(ScaleX) > 1e-6f &&
                Mathf.Abs(ScaleY) > 1e-6f &&
                Mathf.Abs(ScaleZ) > 1e-6f;

            private static bool IsFinite(float value)
            {
                return !float.IsNaN(value) && !float.IsInfinity(value);
            }
        }

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
            // PlayerPrefs 에 저장된 마지막 카테고리별 entry 복원 (local-preview 테마 wallpaper 등
            // 앱 재시작 후 visual 유지). InstallScene/ReloadRoomState 보다 먼저 채워두면
            // server null 응답 시 fallback 회피로 in-memory visual 도 일관 유지.
            RestoreLastAppliedEntriesFromPrefs();
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
            remoteAssetCacheService ??= new RemoteAssetCacheService();

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

        private void RefreshRemoteAssetInventory()
        {
            if (isRemoteAssetInventoryLoading)
            {
                return;
            }

            var apiManager = APIManager.Instance;
            if (apiManager == null || !apiManager.HasAccessToken)
            {
                return;
            }

            NotebookApiClient apiClient;
            try
            {
                apiClient = new NotebookApiClient(apiManager);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RoomEdit][RemoteAsset] NotebookApiClient unavailable: {ex.GetType().Name} {ex.Message}");
                return;
            }

            isRemoteAssetInventoryLoading = true;
            var requestVersion = ++remoteAssetInventoryRequestVersion;
            apiClient.GetAssetInventory(
                inventory =>
                {
                    if (requestVersion != remoteAssetInventoryRequestVersion)
                    {
                        return;
                    }

                    isRemoteAssetInventoryLoading = false;
                    MergeRemoteAssetInventory(inventory);
                    RebuildInventorySlots();
                },
                error =>
                {
                    if (requestVersion == remoteAssetInventoryRequestVersion)
                    {
                        isRemoteAssetInventoryLoading = false;
                    }

                    Debug.LogWarning($"[RoomEdit][RemoteAsset] inventory load failed: {error?.Code}/{error?.Message}");
                });
        }

        private void MergeRemoteAssetInventory(InventoryResponseDto inventory)
        {
            ownedAssetIdByInventoryId.Clear();
            ownedInventoryIdByAssetId.Clear();
            ownedAssetsByAssetId.Clear();
            remoteAssetInventoryLoaded = true;

            for (var i = inventoryEntries.Count - 1; i >= 0; i--)
            {
                if (inventoryEntries[i].IsRemoteAsset)
                {
                    inventoryEntries.RemoveAt(i);
                    continue;
                }

                var localEntry = inventoryEntries[i];
                localEntry.RemoteAssetId = 0L;
                localEntry.RemoteInventoryId = 0L;
                localEntry.PreviewImageUrl = string.Empty;
                localEntry.ThemeTag = string.Empty;
                localEntry.ServerAssetType = string.Empty;
                localEntry.ServerCategoryCode = string.Empty;
                inventoryEntries[i] = localEntry;
            }

            var items = inventory?.Items;
            if (items == null || items.Count == 0)
            {
                return;
            }

            var added = 0;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null || item.AssetId <= 0L)
                {
                    continue;
                }

                IndexOwnedInventoryItem(item);

                var asset = item.Asset;
                if (DoesServerAssetTypeMatch(asset, "THEME"))
                {
                    continue;
                }

                var displayName = FirstNonEmpty(asset?.Name, asset?.Code, $"Asset {item.AssetId}");
                if (TryBindOwnedLocalAsset(item, asset, displayName))
                {
                    added++;
                    continue;
                }

                inventoryEntries.Add(new InventoryEntry
                {
                    DisplayName = displayName,
                    Prefab = null,
                    SourceAssetPath = string.Empty,
                    PlacementCategory = ResolveRemotePlacementCategory(asset),
                    AllowRotation = true,
                    Kind = ResolveRemoteDecorationKind(asset),
                    ScaleMultiplierOverride = 0f,
                    IsRemoteAsset = true,
                    RemoteAssetId = item.AssetId,
                    RemoteInventoryId = item.InventoryId,
                    PreviewImageUrl = asset?.PreviewImageUrl,
                    ThemeTag = ResolveRemoteThemeTag(asset),
                    ServerAssetType = asset?.AssetType,
                    ServerCategoryCode = asset?.CategoryCode
                });
                added++;
            }

            if (added > 0)
            {
                Debug.Log($"[RoomEdit][RemoteAsset] merged {added} owned remote asset(s) into decoration inventory.");
            }

            if (pendingSpaceVisualsReapplyAfterInventory &&
                pendingSpaceVisualsReapplyCategoryVersion == categoryRequestVersion &&
                lastServerSpace != null)
            {
                pendingSpaceVisualsReapplyAfterInventory = false;
                ApplyServerSpaceVisuals(lastServerSpace);
            }

            if (themeApplier != null && themeApplier.CurrentTheme != null)
            {
                RequestThemeContentsBackgroundRefresh(themeApplier.CurrentTheme, "RefreshRemoteAssetInventory");
            }
        }

        private void IndexOwnedInventoryItem(InventoryItemResponseDto item)
        {
            if (item == null || item.InventoryId <= 0L || item.AssetId <= 0L)
            {
                return;
            }

            ownedAssetIdByInventoryId[item.InventoryId] = item.AssetId;
            if (!ownedInventoryIdByAssetId.ContainsKey(item.AssetId))
            {
                ownedInventoryIdByAssetId[item.AssetId] = item.InventoryId;
            }

            if (item.Asset != null)
            {
                ownedAssetsByAssetId[item.AssetId] = item.Asset;
            }
        }

        private bool TryResolveOwnedInventoryIdForAsset(long assetId, out long inventoryId)
        {
            inventoryId = 0L;
            if (assetId <= 0L)
            {
                return false;
            }

            if (ownedInventoryIdByAssetId.TryGetValue(assetId, out inventoryId) && inventoryId > 0L)
            {
                return true;
            }

            if (!remoteAssetInventoryLoaded && !isRemoteAssetInventoryLoading)
            {
                RefreshRemoteAssetInventory();
            }

            return false;
        }

        private bool TryResolveOwnedAssetIdForInventory(long inventoryId, out long assetId)
        {
            assetId = 0L;
            if (inventoryId <= 0L)
            {
                return false;
            }

            if (isApplyingReadOnlySpacePreviewVisuals)
            {
                return visibleAssetIdByInventoryId.TryGetValue(inventoryId, out assetId) && assetId > 0L;
            }

            if (ownedAssetIdByInventoryId.TryGetValue(inventoryId, out assetId) && assetId > 0L)
            {
                return true;
            }

            if (!remoteAssetInventoryLoaded)
            {
                RequestSpaceVisualsReapplyAfterInventory();
                if (!isRemoteAssetInventoryLoading)
                {
                    RefreshRemoteAssetInventory();
                }
            }

            return false;
        }

        private void RequestSpaceVisualsReapplyAfterInventory()
        {
            pendingSpaceVisualsReapplyAfterInventory = true;
            pendingSpaceVisualsReapplyCategoryVersion = categoryRequestVersion;
        }

        private bool TryBindOwnedLocalAsset(InventoryItemResponseDto item, AssetResponseDto asset, string displayName)
        {
            if (item == null || item.AssetId <= 0L || string.IsNullOrWhiteSpace(asset?.UnityResourceKey))
            {
                return false;
            }

            var normalizedResourceKey = NormalizeUnityResourceKey(asset.UnityResourceKey);
            for (var i = 0; i < inventoryEntries.Count; i++)
            {
                var entry = inventoryEntries[i];
                if (entry.IsRemoteAsset || string.IsNullOrWhiteSpace(entry.SourceAssetPath))
                {
                    continue;
                }

                if (!string.Equals(NormalizeUnityResourceKey(entry.SourceAssetPath), normalizedResourceKey, StringComparison.Ordinal))
                {
                    continue;
                }

                // Owned built-in asset: local instantiate path, server identity for persistence.
                entry.DisplayName = FirstNonEmpty(displayName, entry.DisplayName);
                entry.RemoteAssetId = item.AssetId;
                entry.RemoteInventoryId = item.InventoryId;
                entry.PreviewImageUrl = asset.PreviewImageUrl;
                entry.ThemeTag = FirstNonEmpty(ResolveRemoteThemeTag(asset), entry.ThemeTag);
                entry.ServerAssetType = asset.AssetType;
                entry.ServerCategoryCode = asset.CategoryCode;
                inventoryEntries[i] = entry;
                return true;
            }

            return false;
        }

        private static string ResolveRemoteThemeTag(AssetResponseDto asset)
        {
            var explicitTheme = NormalizeThemeTag(asset?.Theme);
            if (!string.IsNullOrEmpty(explicitTheme))
            {
                return explicitTheme;
            }

            return NormalizeThemeTag(FirstNonEmpty(asset?.CollectionCode, asset?.Code, asset?.Name, string.Empty));
        }

        private static string NormalizeThemeTag(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Trim()
                .Replace("-", string.Empty)
                .Replace("_", string.Empty)
                .Replace(" ", string.Empty)
                .ToUpperInvariant();

            if (normalized.Contains("DEEPSEA")) return "DeepSea";
            if (normalized.Contains("FANTASY")) return "Fantasy";
            if (normalized.Contains("SPACE")) return "Space";
            if (normalized.Contains("SPOOKY")) return "Spooky";
            if (normalized.Contains("VINTAGE")) return "Vintage";
            if (normalized.Contains("GOODVIBES")) return "GoodVibes";
            if (normalized.Contains("PASTEL")) return "Pastel";
            if (normalized.Contains("NATURAL")) return "Natural";
            if (normalized.Contains("CLASSIC")) return "Classic";
            if (normalized.Contains("MODERN")) return "Modern";

            return value.Trim();
        }

        private bool TryResolveOwnedInventoryIdForTheme(RoomThemeAsset theme, out long inventoryId, out long serverAssetId)
        {
            inventoryId = 0L;
            serverAssetId = 0L;
            if (theme == null)
            {
                return false;
            }

            if (theme.AssetId > 0L && TryResolveOwnedInventoryIdForAsset(theme.AssetId, out inventoryId))
            {
                serverAssetId = theme.AssetId;
                return true;
            }

            var themeTag = ResolveThemeFilterTag(theme);
            if (string.IsNullOrEmpty(themeTag) || themeTag == RoomEditThemeFilter.AllThemesValue)
            {
                return false;
            }

            foreach (var pair in ownedAssetsByAssetId)
            {
                var asset = pair.Value;
                if (!DoesServerAssetTypeMatch(asset, "THEME"))
                {
                    continue;
                }

                if (!ThemeTagsEqual(themeTag, ResolveRemoteThemeTag(asset)))
                {
                    continue;
                }

                if (ownedInventoryIdByAssetId.TryGetValue(pair.Key, out inventoryId) && inventoryId > 0L)
                {
                    serverAssetId = pair.Key;
                    return true;
                }
            }

            if (!remoteAssetInventoryLoaded && !isRemoteAssetInventoryLoading)
            {
                RefreshRemoteAssetInventory();
            }

            return false;
        }

        private bool TryResolveOwnedServerThemeAssetId(RoomThemeAsset theme, out long serverAssetId)
        {
            serverAssetId = 0L;
            if (theme == null)
            {
                return false;
            }

            if (theme.AssetId > 0L && ownedAssetsByAssetId.ContainsKey(theme.AssetId))
            {
                serverAssetId = theme.AssetId;
                return true;
            }

            if (TryResolveOwnedInventoryIdForTheme(theme, out _, out serverAssetId))
            {
                return serverAssetId > 0L;
            }

            return false;
        }

        private RoomThemeAsset ResolveThemeForServerAssetId(long serverAssetId)
        {
            if (themeCatalog == null)
            {
                return null;
            }

            var exact = serverAssetId > 0L ? themeCatalog.FindByAssetId(serverAssetId) : null;
            if (exact != null)
            {
                return exact;
            }

            if (serverAssetId > 0L &&
                ownedAssetsByAssetId.TryGetValue(serverAssetId, out var asset) &&
                TryFindThemeByTag(ResolveRemoteThemeTag(asset), out var taggedTheme))
            {
                return taggedTheme;
            }

            return themeCatalog.Resolve(serverAssetId > 0L ? serverAssetId : (long?)null);
        }

        private bool TryFindThemeByTag(string themeTag, out RoomThemeAsset theme)
        {
            theme = null;
            var normalizedTag = NormalizeThemeTag(themeTag);
            if (string.IsNullOrEmpty(normalizedTag) || normalizedTag == RoomEditThemeFilter.AllThemesValue || themeCatalog == null)
            {
                return false;
            }

            var entries = themeCatalog.Entries;
            if (entries == null)
            {
                return false;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var candidate = entries[i];
                if (candidate != null && ThemeTagsEqual(normalizedTag, ResolveThemeFilterTag(candidate)))
                {
                    theme = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveOwnedInventoryIdForEntry(CustomizationCategory category, CustomizationEntry entry, out long inventoryId)
        {
            inventoryId = 0L;
            if (entry == null)
            {
                return false;
            }

            if (entry.AssetId > 0L && TryResolveOwnedInventoryIdForAsset(entry.AssetId, out inventoryId))
            {
                return true;
            }

            foreach (var pair in ownedAssetsByAssetId)
            {
                if (!DoesServerAssetMatchEntry(category, pair.Value, entry))
                {
                    continue;
                }

                if (ownedInventoryIdByAssetId.TryGetValue(pair.Key, out inventoryId) && inventoryId > 0L)
                {
                    return true;
                }
            }

            if (!remoteAssetInventoryLoaded && !isRemoteAssetInventoryLoading)
            {
                RefreshRemoteAssetInventory();
            }

            return false;
        }

        private bool TryFindCatalogEntryForServerAssetId(CustomizationCategory category, long serverAssetId, CustomizationCatalogAsset catalog, out CustomizationEntry entry)
        {
            entry = null;
            if (catalog == null || catalog.Entries == null || serverAssetId <= 0L)
            {
                return false;
            }

            entry = catalog.FindByAssetId(serverAssetId);
            if (entry != null)
            {
                return true;
            }

            if (!ownedAssetsByAssetId.TryGetValue(serverAssetId, out var serverAsset))
            {
                return false;
            }

            for (var i = 0; i < catalog.Entries.Count; i++)
            {
                var candidate = catalog.Entries[i];
                if (candidate != null && DoesServerAssetMatchEntry(category, serverAsset, candidate))
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool TryFindCatalogEntryForServerAssetId(CustomizationCategory category, long serverAssetId, out CustomizationEntry entry)
        {
            return TryFindCatalogEntryForServerAssetId(category, serverAssetId, LoadCategoryCatalog(category), out entry);
        }

        private static bool DoesServerAssetMatchEntry(CustomizationCategory category, AssetResponseDto serverAsset, CustomizationEntry entry)
        {
            if (serverAsset == null || entry == null)
            {
                return false;
            }

            if (!DoesServerAssetMatchCategory(category, serverAsset))
            {
                return false;
            }

            var entryTheme = NormalizeThemeTag(entry.ThemeTag);
            if (!string.IsNullOrEmpty(entryTheme) && ThemeTagsEqual(entryTheme, ResolveRemoteThemeTag(serverAsset)))
            {
                return true;
            }

            var assetText = NormalizeIdentifier(string.Concat(serverAsset.Code, " ", serverAsset.Name, " ", serverAsset.CollectionCode, " ", serverAsset.Theme));
            return !string.IsNullOrEmpty(entryTheme) && assetText.Contains(NormalizeIdentifier(entryTheme));
        }

        private static bool DoesServerAssetMatchCategory(CustomizationCategory category, AssetResponseDto serverAsset)
        {
            var expected = ExpectedServerAssetType(category);
            if (string.IsNullOrEmpty(expected) || serverAsset == null)
            {
                return false;
            }

            return DoesServerAssetTypeMatch(serverAsset, expected);
        }

        private static bool DoesServerAssetTypeMatch(AssetResponseDto serverAsset, string expectedType)
        {
            if (serverAsset == null || string.IsNullOrWhiteSpace(expectedType))
            {
                return false;
            }

            var expected = NormalizeIdentifier(expectedType);
            return NormalizeIdentifier(serverAsset.AssetType) == expected ||
                   NormalizeIdentifier(serverAsset.CategoryCode) == expected ||
                   NormalizeIdentifier(serverAsset.Placement) == expected;
        }

        private static string ExpectedServerAssetType(CustomizationCategory category)
        {
            switch (category)
            {
                case CustomizationCategory.Wallpaper: return "WALLPAPER";
                case CustomizationCategory.Floor: return "FLOOR";
                case CustomizationCategory.Desk: return "DESK_SET";
                case CustomizationCategory.DashboardSkin: return "DASHBOARD_SKIN";
                case CustomizationCategory.Notebook: return "NOTEBOOK";
                case CustomizationCategory.BookshelfSkin: return "BOOKSHELF_SKIN";
                case CustomizationCategory.InteriorLight: return "INTERIOR_LIGHT";
                case CustomizationCategory.ExteriorLight: return "EXTERIOR_LIGHT";
                default: return string.Empty;
            }
        }

        private static bool ThemeTagsEqual(string left, string right)
        {
            var normalizedLeft = NormalizeThemeTag(left);
            var normalizedRight = NormalizeThemeTag(right);
            return !string.IsNullOrEmpty(normalizedLeft) &&
                   !string.IsNullOrEmpty(normalizedRight) &&
                   string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal);
        }

        private static string NormalizeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Trim()
                .Replace("-", string.Empty)
                .Replace("_", string.Empty)
                .Replace(" ", string.Empty)
                .ToUpperInvariant();
        }

        private static CustomizationCategory ResolveRemotePlacementCategory(AssetResponseDto asset)
        {
            switch (NormalizeIdentifier(asset?.AssetType))
            {
                case "WALLPAPER": return CustomizationCategory.Wallpaper;
                case "FLOOR": return CustomizationCategory.Floor;
                case "BOOKSHELFSKIN": return CustomizationCategory.BookshelfSkin;
                case "DASHBOARDSKIN": return CustomizationCategory.DashboardSkin;
                case "DESKSET":
                case "DESK": return CustomizationCategory.Desk;
                case "INTERIORLIGHT": return CustomizationCategory.InteriorLight;
                case "EXTERIORLIGHT": return CustomizationCategory.ExteriorLight;
                case "NOTEBOOK": return CustomizationCategory.Notebook;
            }

            var placement = FirstNonEmpty(asset?.Placement, asset?.AssetType, asset?.CategoryCode, string.Empty).ToUpperInvariant();
            if (placement.Contains("WALL") || placement.Contains("POSTER") || placement.Contains("WINDOW"))
            {
                return CustomizationCategory.WallDeco;
            }

            if (placement.Contains("BOOKSHELF_DECOR"))
            {
                return CustomizationCategory.BookshelfDeco;
            }

            return CustomizationCategory.FloorDeco;
        }

        private static DecorationObjectKind ResolveRemoteDecorationKind(AssetResponseDto asset)
        {
            // AssetType can be a broad value such as "ASSET"; include name/code so
            // built-in catalog rows like SPOOKY_DESK still resolve their object kind.
            var value = string.Concat(
                asset?.AssetType, " ",
                asset?.CategoryCode, " ",
                asset?.Placement, " ",
                asset?.Name, " ",
                asset?.Code).ToUpperInvariant();
            if (value.Contains("DESK") || value.Contains("TABLE")) return DecorationObjectKind.Desk;
            if (value.Contains("CHAIR")) return DecorationObjectKind.Chair;
            if (value.Contains("BOOKSHELF") || value.Contains("SHELF")) return DecorationObjectKind.Bookshelf;
            if (value.Contains("BED")) return DecorationObjectKind.Bed;
            if (value.Contains("RUG") || value.Contains("CARPET")) return DecorationObjectKind.Rug;
            if (value.Contains("LAMP") || value.Contains("LIGHT")) return DecorationObjectKind.Lamp;
            if (value.Contains("PLANT") || value.Contains("FLOWER")) return DecorationObjectKind.Plant;
            if (value.Contains("PILLOW") || value.Contains("CUSHION")) return DecorationObjectKind.Pillow;
            if (value.Contains("MONITOR") || value.Contains("DASHBOARD")) return DecorationObjectKind.Monitor;
            return DecorationObjectKind.Decoration;
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
                if (IsBookshelfModelPath(path)) continue;
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

        private static bool IsBookshelfModelPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   path.StartsWith("Assets/Arts/Models/Bookshelves", System.StringComparison.OrdinalIgnoreCase);
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

        private static bool TryResolveRemoteCustomizationCategory(InventoryEntry entry, out CustomizationCategory category)
        {
            category = entry.PlacementCategory;
            if (!entry.IsRemoteAsset)
            {
                return false;
            }

            switch (entry.PlacementCategory)
            {
                case CustomizationCategory.Wallpaper:
                case CustomizationCategory.Floor:
                case CustomizationCategory.BookshelfSkin:
                case CustomizationCategory.DashboardSkin:
                case CustomizationCategory.Desk:
                case CustomizationCategory.InteriorLight:
                case CustomizationCategory.ExteriorLight:
                case CustomizationCategory.Notebook:
                    return true;
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

            // 저장은 상단 저장 버튼에서만 수행한다. 이 모달은 남아있는 호출 경로가 열리더라도
            // 나가기/취소만 제공해서 exit 경로가 저장 API나 썸네일 업로드를 호출하지 않게 한다.
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

            var discardExitButton = CreateButton(buttonRow.transform, "DiscardAndExitButton", "나가기",
                Hex("#C14E4E"), Color.white, new Vector2(240f, 56f), false, BorderColor);
            ApplyButtonLabelHorizontalMargin(discardExitButton, 20f);
            var discardExitLayout = discardExitButton.gameObject.AddComponent<LayoutElement>();
            discardExitLayout.preferredWidth = 240f;
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

        // BookshelfSkin has no backend PATCH endpoint yet, so the button stays
        // enabled and the selection is routed through the mock category state.
        private static bool IsFurnitureSubDisabled(string key) => false;

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

            string displayLabel = label;
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
                case "NOTEBOOK":        category = CustomizationCategory.Notebook;      return true;
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
            //themeFilterLabel.text = $"현재 테마 필터: {RoomEditThemeFilter.DescribeActive()}";
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
                DestroyEditorSafe(inventoryContentRoot.transform.GetChild(i).gameObject);
            }

            if (selectedToolbarKey == "THEME_FULL")
            {
                BuildThemeSlots(inventoryContentRoot.transform);
                return;
            }

            if (selectedToolbarKey == "ALL")
            {
                BuildAllAssetSlots(inventoryContentRoot.transform);
                return;
            }

            // v1.0 customization — 1:1 카테고리 매핑이 명확한 toolbar 키는 CustomizationCatalog로 처리.
            // FURNITURE는 sub-tab(책상/책장스킨/대시보드스킨)으로 분기. OBJECT_SKIN은 후속 PR에서 sub-탭화.
            if (selectedToolbarKey == "FURNITURE")
            {
                if (TryResolveFurnitureSubCategory(selectedFurnitureSubKey, out var furnitureCategory))
                {
                    BuildCustomizationSlots(inventoryContentRoot.transform, furnitureCategory);
                    BuildRemoteFurnitureSlots(inventoryContentRoot.transform, selectedFurnitureSubKey);
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
                if (!RoomEditThemeFilter.MatchesActive(entry.ThemeTag))
                {
                    continue;
                }

                if (ShouldSuppressRemoteCatalogDuplicate(entry))
                {
                    continue;
                }

                if (!MatchesCategory(entry, selectedToolbarKey))
                {
                    continue;
                }
                BuildInventorySlot(inventoryContentRoot.transform, slotIndex, entry);
                slotIndex++;
            }
        }

        private void BuildAllAssetSlots(Transform parent)
        {
            int slotIndex = 0;
            for (int c = 0; c < AllCatalogCategories.Length; c++)
            {
                var category = AllCatalogCategories[c];
                var catalog = LoadCategoryCatalog(category);
                if (catalog == null || catalog.Entries == null || catalog.Entries.Count == 0)
                {
                    continue;
                }

                for (int i = 0; i < catalog.Entries.Count; i++)
                {
                    var entry = catalog.Entries[i];
                    if (entry == null) continue;
                    if (!RoomEditThemeFilter.MatchesActive(entry.ThemeTag)) continue;
                    BuildCustomizationSlot(parent, slotIndex, category, entry);
                    slotIndex++;
                }
            }

            for (var i = 0; i < inventoryEntries.Count; i++)
            {
                var entry = inventoryEntries[i];
                if (!RoomEditThemeFilter.MatchesActive(entry.ThemeTag))
                {
                    continue;
                }

                if (ShouldSuppressRemoteCatalogDuplicate(entry))
                {
                    continue;
                }

                if (!MatchesCategory(entry, selectedToolbarKey))
                {
                    continue;
                }

                BuildInventorySlot(parent, slotIndex, entry);
                slotIndex++;
            }

            Debug.Log($"[RoomEditModeController] All assets shown. CatalogCategories={AllCatalogCategories.Length}, ResultCount={slotIndex}");
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
            bool applyThemeFilter = ShouldApplyThemeFilterToCustomizationCategory(category);
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                var entry = catalog.Entries[i];
                if (entry == null) continue;
                // 상단 메뉴 테마 필터 적용. 빈 ThemeTag (legacy/공통) 는 항상 통과.
                if (applyThemeFilter && !RoomEditThemeFilter.MatchesActive(entry.ThemeTag)) continue;
                BuildCustomizationSlot(parent, i, category, entry);
                rendered++;
            }
            Debug.Log($"[RoomEditModeController] Theme={RoomEditThemeFilter.DescribeActive()}, Category={category}, ResultCount={rendered}");
        }

        private bool ShouldApplyThemeFilterToCustomizationCategory(CustomizationCategory category)
        {
            return true;
        }

        private void BuildRemoteFurnitureSlots(Transform parent, string subKey)
        {
            var slotIndex = 0;
            for (var i = 0; i < inventoryEntries.Count; i++)
            {
                var entry = inventoryEntries[i];
                if (!RoomEditThemeFilter.MatchesActive(entry.ThemeTag))
                {
                    continue;
                }

                if (!MatchesRemoteFurnitureSubCategory(entry, subKey))
                {
                    continue;
                }

                if (ShouldSuppressRemoteCatalogDuplicate(entry))
                {
                    continue;
                }

                BuildInventorySlot(parent, slotIndex, entry);
                slotIndex++;
            }
        }

        private bool ShouldSuppressRemoteCatalogDuplicate(InventoryEntry entry)
        {
            if (!TryResolveRemoteCustomizationCategory(entry, out var category))
            {
                return false;
            }

            var catalog = LoadCategoryCatalog(category);
            if (catalog == null || catalog.Entries == null)
            {
                return false;
            }

            for (var i = 0; i < catalog.Entries.Count; i++)
            {
                if (DoesRemoteInventoryEntryMatchCatalogEntry(category, entry, catalog.Entries[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool DoesRemoteInventoryEntryMatchCatalogEntry(
            CustomizationCategory category,
            InventoryEntry remoteEntry,
            CustomizationEntry catalogEntry)
        {
            if (!remoteEntry.IsRemoteAsset || catalogEntry == null)
            {
                return false;
            }

            if (!TryResolveRemoteCustomizationCategory(remoteEntry, out var remoteCategory) ||
                remoteCategory != category)
            {
                return false;
            }

            if (remoteEntry.RemoteAssetId > 0L &&
                catalogEntry.AssetId > 0L &&
                remoteEntry.RemoteAssetId == catalogEntry.AssetId)
            {
                return true;
            }

            var catalogTheme = NormalizeThemeTag(catalogEntry.ThemeTag);
            if (string.IsNullOrEmpty(catalogTheme))
            {
                return false;
            }

            if (ThemeTagsEqual(catalogTheme, remoteEntry.ThemeTag))
            {
                return true;
            }

            var remoteText = NormalizeIdentifier(string.Concat(
                remoteEntry.ServerAssetType, " ",
                remoteEntry.ServerCategoryCode, " ",
                remoteEntry.DisplayName));
            return !string.IsNullOrEmpty(remoteText) &&
                   remoteText.Contains(NormalizeIdentifier(catalogTheme));
        }

        private static bool MatchesRemoteFurnitureSubCategory(InventoryEntry entry, string subKey)
        {
            if (!entry.IsRemoteAsset)
            {
                return false;
            }

            if (TryResolveRemoteCustomizationCategory(entry, out var fixedCategory))
            {
                switch (subKey)
                {
                    case "BOOKSHELF_SKIN":
                        return fixedCategory == CustomizationCategory.BookshelfSkin;
                    case "DASHBOARD_SKIN":
                        return fixedCategory == CustomizationCategory.DashboardSkin;
                    case "DESK":
                        return fixedCategory == CustomizationCategory.Desk;
                    case "NOTEBOOK":
                        return fixedCategory == CustomizationCategory.Notebook;
                    default:
                        return false;
                }
            }

            if (entry.PlacementCategory == CustomizationCategory.WallDeco ||
                entry.PlacementCategory == CustomizationCategory.BookshelfDeco)
            {
                return false;
            }

            switch (subKey)
            {
                case "BOOKSHELF_SKIN":
                    return entry.Kind == DecorationObjectKind.Bookshelf;
                case "DASHBOARD_SKIN":
                    return entry.Kind == DecorationObjectKind.Monitor;
                case "DESK":
                    return entry.Kind != DecorationObjectKind.Bookshelf &&
                           entry.Kind != DecorationObjectKind.Monitor;
                default:
                    return false;
            }
        }

        private CustomizationCatalogAsset LoadCategoryCatalog(CustomizationCategory category)
        {
            if (categoryCatalogCache.TryGetValue(category, out var cached) && cached != null)
            {
                return cached;
            }
            var path = $"Catalogs/{category}Catalog";
            var loaded = Resources.Load<CustomizationCatalogAsset>(path);
            if (loaded != null)
            {
                categoryCatalogCache[category] = loaded;
                // 어느 Resources path 에서 몇 개 entry 를 가진 catalog SO 가 로드됐는지 한 줄 로그.
                // Resources.Load 는 같은 path 의 첫 매칭 asset 만 로드 (여러 개 있을 수 없지만 사용자가
                // 의심 시 곧장 확인 가능). entries.Count + 첫 몇 개 assetId 로 stale cache 도 검증.
                var entries = loaded.Entries;
                var assetIds = new System.Text.StringBuilder();
                if (entries != null)
                {
                    int max = System.Math.Min(entries.Count, 8);
                    for (int i = 0; i < max; i++)
                    {
                        if (i > 0) assetIds.Append(',');
                        assetIds.Append(entries[i] != null ? entries[i].AssetId.ToString() : "null");
                    }
                    if (entries.Count > max) assetIds.Append(",...");
                }
                Debug.Log($"[CatalogLoad] category={category} Resources.Load(\"{path}\") → instanceId={loaded.GetInstanceID()} name={loaded.name} entries.Count={(entries?.Count ?? 0)} assetIds=[{assetIds}]");
            }
            else
            {
                Debug.LogWarning($"[CatalogLoad] category={category} Resources.Load(\"{path}\") → null. Resources/Catalogs/{category}Catalog.asset 존재/import 확인.");
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
        private int suppressLastAppliedEntriesPersistenceDepth;
        private bool isApplyingReadOnlySpacePreviewVisuals;
        private bool spaceVisualsRequireAuthoritativeReapply;
        private bool hasPrePreviewAuthoritativeVisualSnapshot;
        private RoomThemeAsset prePreviewAuthoritativeTheme;
        private readonly Dictionary<CustomizationCategory, long> prePreviewAuthoritativeAppliedEntries =
            new Dictionary<CustomizationCategory, long>();

        // 로컬 PlayerPrefs persistence — local-preview 테마(빈티지/Natural 등 PATCH skip 대상)
        // 가 적용한 wallpaper/floor/light entry 를 앱 재시작 후에도 유지하기 위함. 본 컨트롤러
        // 인스턴스가 살아 있는 동안엔 in-memory dictionary 만으로 충분하지만, 앱 종료/재실행
        // 시엔 server 권위 상태에 의존하게 되어 server null → Entries[0] fallback 으로 회귀.
        // (MR #157 후속 — 사용자 보고: 빈티지 wallpaper 가 메인씬 OK → 편집모드 재진입 시 사라짐.)
        //
        // 형식: 카테고리(int):assetId(long) 쌍을 ';' 구분. 예: "0:200004;1:0;5:200015"
        // assetId 가 long.MinValue 면 "명시적 reset" — Restore 시 dictionary 에 안 채움.
        //
        // **소유 경계 (MR #174 skh1205 리뷰 #1 반영)**: 이 base prefix 는 그대로 두되,
        // 실제 PlayerPrefs key 는 ScopedKey() 를 통해 `<base>.user<UserId>` 로 변환된다.
        // 한 기기에서 A → B 로 계정이 바뀐 뒤 server 가 B 의 theme 을 모르면 A 의 마지막
        // local-preview 가 B 의 방에 복원되는 회귀를 막기 위함. AuthSession.UserId == 0
        // (비로그인/owner 미상) 시점에는 Persist/Restore 양쪽 모두 skip 한다.
        private const string LastAppliedEntriesPrefsKey = "RoomEditMode.LastAppliedEntries.v1";
        private bool lastAppliedEntriesRestored;

        // local-preview 테마는 PATCH skip 이라 server 가 모름 → 풀 로그아웃/로그인 후 자동 복원 못함.
        // 사용자 보고 회귀: Vintage 적용 → 로그아웃 → 로그인 → 메인씬에 walls/floor 가 default 로 보임.
        // 마지막 적용한 local-preview 테마의 ScriptableObject 이름 (예: "RoomTheme_Vintage") 을 저장해
        // 다음 세션 ApplySpaceVisualsToRoom 에서 server null 응답이면 PlayerPrefs 값으로 복원.
        // non-local-preview 테마를 적용하면 이 키를 비워둠 (server 가 권위 상태 보유).
        // **소유 경계 — LastAppliedEntriesPrefsKey 와 동일하게 ScopedKey() 로 user 별 분리.**
        private const string LastAppliedLocalPreviewThemePrefsKey = "RoomEditMode.LastAppliedLocalPreviewTheme.v1";

        // 마지막 적용 모드 — "THEME" (단일 RoomThemeAsset) / "RANDOM" (slot mix). 복원 시 분기 키.
        // 일반 ApplyThemeSelected 가 "THEME" 으로, ApplyExternalRandomThemeSelection 이 "RANDOM" 으로 set.
        // 값이 없거나 비로그인이면 THEME default — backward compat (기존 사용자 동작 보존).
        private const string LastAppliedThemeModePrefsKey = "RoomEditMode.LastAppliedThemeMode.v1";
        private const string ThemeModeSingle = "THEME";
        private const string ThemeModeRandom = "RANDOM";

        // 온보딩 랜덤 조합 (slot 별 mix) 직렬화. THEME 모드 키와 분리 — 같은 user 가 단일 테마 ↔ 랜덤
        // 조합을 토글해도 양쪽 마지막 상태 보존 (mode 키가 우선순위 결정).
        private const string LastAppliedRandomThemeSelectionPrefsKey = "RoomEditMode.LastAppliedRandomThemeSelection.v1";
        private const long FantasyThemeAssetId = 400000L;
        private const long FantasyDeskAssetId = 400030L;

        // AuthSession.UserId 로 scoped PlayerPrefs key 생성. 0 (비로그인/anon) 이면 null →
        // 호출자가 Persist/Restore 를 skip. 사용자 간 local-preview 누출 차단의 단일 진입점.
        private static string ScopedKey(string baseKey)
        {
            long uid = AuthSession.UserId;
            if (uid <= 0) return null;
            return $"{baseKey}.user{uid}";
        }

        private static bool IsFantasyTheme(RoomThemeAsset theme)
        {
            if (theme == null)
            {
                return false;
            }

            if (theme.AssetId == FantasyThemeAssetId)
            {
                return true;
            }

            return string.Equals(theme.name, "RoomTheme_Fantasy", StringComparison.Ordinal) ||
                   string.Equals(theme.DisplayName, "판타지", StringComparison.Ordinal);
        }

        private static bool IsFantasyDeskEntry(CustomizationEntry entry)
        {
            return entry != null && entry.AssetId == FantasyDeskAssetId;
        }

        private static bool IsFantasyDeskCatalogEntry(CustomizationCategory category, CustomizationEntry entry)
        {
            return category == CustomizationCategory.Desk && IsFantasyDeskEntry(entry);
        }

        private static void WarnMissingDeskPrefab(CustomizationCategory category, CustomizationEntry entry, string source)
        {
            if (category != CustomizationCategory.Desk || entry == null || entry.Prefab != null)
            {
                return;
            }

            Debug.LogWarning(
                $"[FantasyDesk] Missing desk prefab source={source} category={category} assetId={entry.AssetId} " +
                $"themeTag={entry.ThemeTag} displayName={entry.DisplayName}");
        }

#if UNITY_EDITOR
        private static readonly System.Reflection.FieldInfo CustomizationEntryPrefabField =
            typeof(CustomizationEntry).GetField("prefab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        private static bool TryRepairFantasyDeskPrefabReference(CustomizationCatalogAsset catalog, CustomizationEntry entry, string source)
        {
            if (!IsFantasyDeskCatalogEntry(CustomizationCategory.Desk, entry) || entry.Prefab != null)
            {
                return false;
            }

            var fallback = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Arts/Models/fantasy_table.glb");
            if (fallback == null)
            {
                Debug.LogWarning($"[FantasyDesk] DeskCatalog repair failed source={source} assetId={entry.AssetId} path=Assets/Arts/Models/fantasy_table.glb");
                return false;
            }

            if (CustomizationEntryPrefabField == null)
            {
                Debug.LogWarning($"[FantasyDesk] DeskCatalog repair failed source={source} assetId={entry.AssetId} reason=prefab_field_not_found");
                return false;
            }

            CustomizationEntryPrefabField.SetValue(entry, fallback);
            if (catalog != null)
            {
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);
            }

            Debug.Log($"[FantasyDesk] DeskCatalog prefab repaired source={source} assetId={entry.AssetId} prefab={fallback.name}");
            return true;
        }
#else
        private static bool TryRepairFantasyDeskPrefabReference(CustomizationCatalogAsset catalog, CustomizationEntry entry, string source)
        {
            return false;
        }
#endif

        private bool TryResolveProtectedFantasyLocalPreviewTheme(out RoomThemeAsset theme, out string source)
        {
            theme = null;
            source = null;

            var mode = TryFindLastAppliedThemeMode();
            if (string.Equals(mode, ThemeModeRandom, StringComparison.Ordinal))
            {
                return false;
            }

            if (themeApplier != null &&
                themeApplier.CurrentTheme != null &&
                IsLocalPreviewTheme(themeApplier.CurrentTheme) &&
                IsFantasyTheme(themeApplier.CurrentTheme))
            {
                theme = themeApplier.CurrentTheme;
                source = "current_theme";
                return true;
            }

            var savedTheme = TryFindLastAppliedLocalPreviewTheme();
            if (savedTheme != null && IsFantasyTheme(savedTheme))
            {
                theme = savedTheme;
                source = "player_prefs";
                return true;
            }

            return false;
        }

        internal bool TryGetCurrentTheme(out RoomThemeAsset theme)
        {
            theme = themeApplier != null ? themeApplier.CurrentTheme : null;
            return theme != null;
        }

        private void LogFantasyDeskHierarchyState(string source)
        {
            if (themeApplier == null)
            {
                themeApplier = FindFirstObjectByType<RoomThemeApplier>();
            }

            var roomRoot = themeApplier != null ? themeApplier.RoomRoot : null;
            if (roomRoot == null)
            {
                Debug.Log($"[FantasyDesk] {source} roomRoot=null");
                return;
            }

            var notebook = FindChildByName(roomRoot, "Notebook");
            var skin = FindChildByName(roomRoot, DeskVisualSkin.SkinChildName);
            if (skin == null)
            {
                var skins = roomRoot.GetComponentsInChildren<DeskVisualSkin>(true);
                for (int i = 0; i < skins.Length; i++)
                {
                    if (skins[i] != null && skins[i].EntryAssetId == FantasyDeskAssetId)
                    {
                        skin = skins[i].transform;
                        break;
                    }
                }
            }

            Debug.Log(
                $"[FantasyDesk] {source} currentTheme={(themeApplier.CurrentTheme != null ? themeApplier.CurrentTheme.name : "null")} " +
                $"skin={(skin != null ? skin.name : "null")} skinWorldPos={(skin != null ? skin.position.ToString() : "null")} " +
                $"skinWorldRot={(skin != null ? skin.eulerAngles.ToString() : "null")} skinWorldScale={(skin != null ? skin.lossyScale.ToString() : "null")} " +
                $"skinLocalPos={(skin != null ? skin.localPosition.ToString() : "null")} skinLocalRot={(skin != null ? skin.localEulerAngles.ToString() : "null")} " +
                $"skinLocalScale={(skin != null ? skin.localScale.ToString() : "null")} " +
                $"notebookLocalPos={(notebook != null ? notebook.localPosition.ToString() : "null")} " +
                $"notebookLocalRot={(notebook != null ? notebook.localEulerAngles.ToString() : "null")} " +
                $"notebookLocalScale={(notebook != null ? notebook.localScale.ToString() : "null")} " +
                $"notebookWorldPos={(notebook != null ? notebook.position.ToString() : "null")} " +
                $"notebookWorldRot={(notebook != null ? notebook.eulerAngles.ToString() : "null")} " +
                $"notebookWorldScale={(notebook != null ? notebook.lossyScale.ToString() : "null")}");
        }

        internal void LogCurrentDeskNotebookHierarchyState(string source)
        {
            if (themeApplier == null)
            {
                themeApplier = FindFirstObjectByType<RoomThemeApplier>();
            }

            var theme = themeApplier != null ? themeApplier.CurrentTheme : null;
            var roomRoot = themeApplier != null ? themeApplier.RoomRoot : null;
            if (roomRoot == null)
            {
                Debug.Log($"[VintageDesk] {source} roomRoot=null theme={(theme != null ? theme.DisplayName : "null")}");
                return;
            }

            var notebookStation = FindNotebookStation(roomRoot);
            var desk = FindChildByName(roomRoot, "Desk");
            var notebook = FindChildByName(roomRoot, "Notebook");
            Debug.Log(
                $"[VintageDesk] {source} theme={(theme != null ? theme.DisplayName : "null")} assetId={(theme != null ? theme.AssetId.ToString() : "null")} " +
                $"notebookStation={(notebookStation != null ? notebookStation.name : "null")} " +
                $"notebookStationLocalPos={(notebookStation != null ? notebookStation.localPosition.ToString() : "null")} " +
                $"notebookStationLocalRot={(notebookStation != null ? notebookStation.localEulerAngles.ToString() : "null")} " +
                $"notebookStationLocalScale={(notebookStation != null ? notebookStation.localScale.ToString() : "null")} " +
                $"desk={(desk != null ? desk.name : "null")} deskLocalPos={(desk != null ? desk.localPosition.ToString() : "null")} " +
                $"deskLocalRot={(desk != null ? desk.localEulerAngles.ToString() : "null")} deskLocalScale={(desk != null ? desk.localScale.ToString() : "null")} " +
                $"notebook={(notebook != null ? notebook.name : "null")} notebookLocalPos={(notebook != null ? notebook.localPosition.ToString() : "null")} " +
                $"notebookLocalRot={(notebook != null ? notebook.localEulerAngles.ToString() : "null")} notebookLocalScale={(notebook != null ? notebook.localScale.ToString() : "null")} " +
                $"notebookWorldPos={(notebook != null ? notebook.position.ToString() : "null")} notebookWorldRot={(notebook != null ? notebook.eulerAngles.ToString() : "null")} " +
                $"notebookWorldScale={(notebook != null ? notebook.lossyScale.ToString() : "null")}");
        }

        private void PersistLastAppliedEntries()
        {
            if (suppressLastAppliedEntriesPersistenceDepth > 0)
            {
                return;
            }

            var key = ScopedKey(LastAppliedEntriesPrefsKey);
            if (key == null) return; // 비로그인/owner 미상 — disk 에 안 남김.
            try
            {
                var sb = new System.Text.StringBuilder();
                bool first = true;
                foreach (var kv in lastAppliedEntries)
                {
                    if (!first) sb.Append(';');
                    sb.Append((int)kv.Key).Append(':').Append(kv.Value);
                    first = false;
                }
                UnityEngine.PlayerPrefs.SetString(key, sb.ToString());
                UnityEngine.PlayerPrefs.Save();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[RoomEditModeController] PersistLastAppliedEntries 실패 — {ex.GetType().Name}: {ex.Message}");
            }
        }

        // 컨트롤러 처음 깨어났을 때 1회 호출. dictionary 가 비어 있을 때만 disk 값으로 채워
        // 이미 in-memory 에 누적된 더 신선한 값을 덮어쓰지 않는다.
        //
        // Awake 시점엔 AuthSession.UserId 가 아직 0 (로그인 전) 일 수 있어 ScopedKey 가 null
        // 이면 lastAppliedEntriesRestored 도 set 하지 않는다 — 로그인 완료 후 다시 호출되면
        // 그 때 진짜 user-scoped key 로 복원. (정상 흐름: LoginScreenController 가 AuthSession
        // 채운 뒤 Main scene 진입 → BookshelfSceneInstaller 가 본 컨트롤러 재호출.)
        private void RestoreLastAppliedEntriesFromPrefs()
        {
            if (lastAppliedEntriesRestored) return;

            var key = ScopedKey(LastAppliedEntriesPrefsKey);
            if (key == null) return; // 비로그인/owner 미상 — server snapshot 이후 재호출 위임.
            lastAppliedEntriesRestored = true;

            if (!UnityEngine.PlayerPrefs.HasKey(key)) return;
            var serialized = UnityEngine.PlayerPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(serialized)) return;

            try
            {
                var pairs = serialized.Split(';');
                int restored = 0;
                for (int i = 0; i < pairs.Length; i++)
                {
                    var p = pairs[i];
                    if (string.IsNullOrEmpty(p)) continue;
                    var sep = p.IndexOf(':');
                    if (sep <= 0 || sep >= p.Length - 1) continue;
                    if (!int.TryParse(p.Substring(0, sep), out var catInt)) continue;
                    if (!long.TryParse(p.Substring(sep + 1), out var assetId)) continue;
                    if (assetId <= 0) continue;
                    var cat = (CustomizationCategory)catInt;
                    if (!lastAppliedEntries.ContainsKey(cat))
                    {
                        lastAppliedEntries[cat] = assetId;
                        restored++;
                    }
                }
                if (restored > 0)
                {
                    Debug.Log($"[RoomEditModeController][CategoryRestore] PlayerPrefs 에서 lastAppliedEntries {restored}개 복원 (앱 재시작 후 visual 유지용).");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[RoomEditModeController] RestoreLastAppliedEntriesFromPrefs 파싱 실패 — {ex.GetType().Name}: {ex.Message}. 키 삭제 후 진행.");
                UnityEngine.PlayerPrefs.DeleteKey(key);
            }
        }

        // local-preview 테마 적용 시점에 호출. 다음 세션 (앱 재시작 / 로그아웃-로그인) 의
        // ApplySpaceVisualsToRoom 가 server null 응답을 받아도 이 키로 복원해 wall/floor 유지.
        private void PersistLastAppliedLocalPreviewTheme(RoomThemeAsset theme)
        {
            var key = ScopedKey(LastAppliedLocalPreviewThemePrefsKey);
            if (key == null) return; // 비로그인/owner 미상 — disk 에 안 남김.
            try
            {
                if (theme == null || !IsLocalPreviewTheme(theme))
                {
                    UnityEngine.PlayerPrefs.DeleteKey(key);
                }
                else
                {
                    UnityEngine.PlayerPrefs.SetString(key, theme.name);
                }
                UnityEngine.PlayerPrefs.Save();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[RoomEditModeController] PersistLastAppliedLocalPreviewTheme 실패 — {ex.GetType().Name}: {ex.Message}");
            }
        }

        // PlayerPrefs 에 저장된 local-preview 테마 이름으로 catalog 에서 lookup. 못 찾으면 null.
        // ScriptableObject.name 은 자산 파일명 (예: "RoomTheme_Vintage") 이라 unique.
        private RoomThemeAsset TryFindLastAppliedLocalPreviewTheme()
        {
            var key = ScopedKey(LastAppliedLocalPreviewThemePrefsKey);
            if (key == null) return null; // 비로그인/owner 미상 — 다른 사용자 마지막 테마 복원 차단.
            if (!UnityEngine.PlayerPrefs.HasKey(key)) return null;
            var savedName = UnityEngine.PlayerPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(savedName)) return null;
            if (themeCatalog == null) themeCatalog = Resources.Load<RoomThemeCatalogAsset>("RoomThemeCatalog");
            if (themeCatalog == null || themeCatalog.Entries == null) return null;
            for (int i = 0; i < themeCatalog.Entries.Count; i++)
            {
                var theme = themeCatalog.Entries[i];
                if (theme == null) continue;
                if (theme.name == savedName && IsLocalPreviewTheme(theme))
                {
                    return theme;
                }
            }
            // 저장된 이름이 catalog 에 없음 — 자산 삭제/이름 변경 등. 키 정리.
            var fallbackTheme = themeCatalog.DefaultTheme;
            UnityEngine.PlayerPrefs.DeleteKey(key);
            UnityEngine.PlayerPrefs.Save();
            return fallbackTheme;
        }

        private bool IsCategoryEntryCurrent(CustomizationCategory category, long assetId)
        {
            if (assetId <= 0) return false;
            if (category == CustomizationCategory.BookshelfSkin &&
                lastAppliedEntries.TryGetValue(category, out var appliedBookshelfSkin))
            {
                return appliedBookshelfSkin == assetId;
            }
            // 1) 서버 상태 우선: lastServerSpace의 카테고리 ID와 직접 비교.
            //    serverId가 null이면 카테고리 미적용(기본 외형) — catalog의 첫 entry가 \"current\"로 표시.
            if (lastServerSpace != null)
            {
                var catalog = LoadCategoryCatalog(category);
                var serverInventoryId = GetCategoryIdFromSpace(category, lastServerSpace);
                if (serverInventoryId.HasValue && serverInventoryId.Value > 0)
                {
                    if (TryResolveOwnedAssetIdForInventory(serverInventoryId.Value, out var serverAssetId))
                    {
                        return serverAssetId == assetId;
                    }

                    var localMockEntry = catalog?.FindByAssetId(serverInventoryId.Value);
                    return localMockEntry != null &&
                           IsLocalPreviewEntry(localMockEntry) &&
                           serverInventoryId.Value == assetId;
                }
                // 서버상 미적용 → catalog default(첫 entry)가 \"current\"
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
                case CustomizationCategory.Notebook:      return space.CurrentNotebookInventoryId;
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
                case CustomizationCategory.Notebook:      lastServerSpace.CurrentNotebookInventoryId = inventoryId; break;
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

        // 외부 (온보딩 Step3 / 시나리오 테스트 등) 에서 호출 가능한 public 진입점.
        //
        // ApplyThemeSelected 와 동일한 흐름이지만 *편집 모드 미진입 상태* 에서도 안전하게 호출 가능.
        // RoomThemeApplier 가 scene 에 있고 InitializeIfNeeded 가 끝나면 호출자 책임은 없다.
        //
        // 5개 온보딩 테마 (Natural / Classic / Modern / Vintage / Pastel) 는 모두 isLocalPreview=true
        // 이므로 ApplyThemeSelected 내부에서 PATCH `/spaces/me/theme/.../apply` 가 자동 skip 됨 →
        // 백엔드 의존 없이 즉시 visual swap. 동시에 PersistLastAppliedLocalPreviewTheme 가 user-scoped
        // PlayerPrefs 에 저장 → 다음 세션 SceneInstall 시점에 RestoreLastLocalPreviewThemeOnSceneInstall
        // 가 자동 복원.
        //
        // mode=Reset 으로 고정한 이유: 온보딩 진입은 "이 사용자의 첫 룸 상태 결정" 이라 placement
        // reset 의도가 맞음. 편집 모드에서 사용자가 명시 클릭하는 경로는 ShowThemeApplyModeModal
        // 로 사용자 선택을 받는 별개 흐름이라 영향 없음.
        public bool ApplyExternalThemeSelection(RoomThemeAsset theme, string source)
        {
            if (theme == null)
            {
                Debug.LogWarning(
                    $"[RoomEditModeController] ApplyExternalThemeSelection theme=null source={source} — skip.");
                return false;
            }
            InitializeIfNeeded();
            Debug.Log(
                $"[RoomEditModeController] ApplyExternalThemeSelection enter theme='{theme.DisplayName}' " +
                $"(assetName={theme.name}, isLocalPreview={theme.IsLocalPreview}) source={source}");
            // 일반 단일 테마 모드 — 이전에 RANDOM 적용 상태였으면 mode/selection 키 정리.
            PersistLastAppliedThemeMode(ThemeModeSingle);
            ClearLastAppliedRandomThemeSelection();
            ApplyThemeSelected(theme, ThemeApplyMode.Reset);
            return true;
        }

        // 온보딩 Step2 전용 preview API — visual 만 적용 (themeApplier.Apply + default entries + deco spawn),
        // PlayerPrefs / mode 키 저장은 모두 skip. 사용자가 카드를 둘러보는 동안 in-session visual 만 갱신.
        // "다음 단계" 클릭 시점에 ApplyExternalThemeSelection (persist 포함) 으로 최종 confirm.
        // "이전 단계" / 취소 시 호출자가 snapshot 으로 rollback (themeApplier.Apply(snapshot)) 가능.
        internal bool ApplyExternalThemePreview(RoomThemeAsset theme, string source)
        {
            if (theme == null)
            {
                Debug.LogWarning($"[RoomEditModeController] ApplyExternalThemePreview theme=null source={source} — skip.");
                return false;
            }
            InitializeIfNeeded();
            Debug.Log($"[RoomEditModeController] ApplyExternalThemePreview enter theme='{theme.DisplayName}' source={source} (no persist).");
            ApplyThemeSelected(theme, ThemeApplyMode.Reset, persist: false);
            return true;
        }

        // 온보딩 Step2 전용 random preview API — visual 만 적용, PlayerPrefs / mode 저장 skip.
        internal bool ApplyExternalRandomThemePreview(OnboardingRandomThemeSelection selection, string source)
        {
            return ApplyExternalRandomThemeSelection(selection, source, persist: false);
        }

        // 온보딩 Step2 "랜덤" 버튼 클릭 → 다음 단계 → 본 메서드.
        // OnboardingRandomThemeBuilder 가 생성한 slot mix 선택을 MainScene 에 적용 + user-scoped 저장.
        //
        // 적용 흐름 (ApplyThemeSelected 의 흐름을 *복제 + 변형* — slot deco 부분만 selection 으로 교체):
        //   1) themeApplier.Apply(baseTheme) — material / lighting / wall fade swap
        //   2) RefreshWallVisibilityAfterMaterialMutation — 편집 fade 일관
        //   3) ClearAllThemeSpawnedDecos / RestoreAllMeshSwaps — 이전 잔존물 정리
        //   4) baseTheme.DefaultEntries 적용 — 카테고리별 default 자산 (Wall/Floor/Desk/Light)
        //   5) **selection.SelectedSpecs 의 slot 별 spec 으로 SpawnThemeFloorDecoFromSpec/Wall 호출**
        //      — baseTheme 의 FloorDecoSpecs/WallDecoSpecs 는 사용하지 않음.
        //   6) RebuildInventorySlots
        //   7) PersistLastAppliedLocalPreviewTheme(baseTheme) + Mode=RANDOM + Selection 직렬화 저장
        //
        // baseTheme.IsLocalPreview 여야 함 — onboarding 5개 테마는 모두 true 라 안전. PATCH 자동 skip.
        // internal — OnboardingRandomThemeSelection 자체가 internal sealed class 라 CS0051 회피 위해
        // 메서드 access level 을 internal 로. 호출자 (OnboardingScreenController) 도 같은 assembly 라 OK.
        // persist=false 호출 가능 — 온보딩 Step2 preview 동안 visual 만 적용, PlayerPrefs 저장 skip.
        internal bool ApplyExternalRandomThemeSelection(OnboardingRandomThemeSelection selection, string source, bool persist = true)
        {
            if (selection == null || selection.SelectedSpecs == null || selection.SelectedSpecs.Count == 0)
            {
                Debug.LogWarning(
                    $"[RoomEditModeController] ApplyExternalRandomThemeSelection skip — selection null/empty. source={source}");
                return false;
            }
            InitializeIfNeeded();

            var baseTheme = selection.BaseTheme;
            if (baseTheme == null)
            {
                Debug.LogWarning(
                    $"[RoomEditModeController] ApplyExternalRandomThemeSelection: baseTheme null — skip. source={source}");
                return false;
            }
            if (themeApplier == null)
            {
                themeApplier = FindFirstObjectByType<RoomThemeApplier>();
            }
            if (themeApplier == null)
            {
                Debug.LogWarning(
                    "[RoomEditModeController] RoomThemeApplier 인스턴스 없음 — random selection apply skip.");
                return false;
            }

            Debug.Log(
                $"[RoomEditModeController] ApplyExternalRandomThemeSelection enter " +
                $"baseTheme='{baseTheme.name}' slots={selection.SelectedSpecs.Count} source={source} " +
                $"summary=[{selection.BuildSummary()}]");

            // (1) base material / lighting
            previousAppliedTheme = themeApplier.CurrentTheme;
            themeApplier.Apply(baseTheme);
            SyncInventoryThemeFilterForTheme(baseTheme);
            RefreshWallVisibilityAfterMaterialMutation("ApplyExternalRandomThemeSelection");
            RebuildInventorySlots();

            // (2) 이전 theme deco / mesh swap 정리
            ClearAllThemeSpawnedDecos();
            RemovePlacementsForIncomingTheme(baseTheme);
            RestoreAllMeshSwaps();

            // (3) baseTheme 의 카테고리 default (Wall/Floor/Desk/Light entries) 적용 — 일반
            //     ApplyThemeDefaultEntries 의 동일 분기. floorDecoSpecs/wallDecoSpecs spawn 은 *skip*
            //     해야 하므로 ApplyThemeDefaultEntries 를 통째 호출하지 않고 default entries 만 직접 적용.
            var defaults = baseTheme.DefaultEntries;
            if (defaults != null)
            {
                for (int i = 0; i < defaults.Count; i++)
                {
                    var def = defaults[i];
                    if (def == null) continue;
                    var catalog = LoadCategoryCatalog(def.Category);
                    if (catalog == null || catalog.Entries == null) continue;
                    var entry = catalog.FindByAssetId(def.AssetId);
                    if (entry == null) continue;
                    ApplyEntryVisual(def.Category, entry);
                    if (entry.AssetId > 0) lastAppliedEntries[def.Category] = entry.AssetId;
                }
                if (persist) PersistLastAppliedEntries();
            }

            // (4) 랜덤 selection 의 slot 별 spec → SpawnThemeFloorDecoFromSpec / SpawnThemeWallDecoFromSpec.
            //     Floor/Bookshelf/Dashboard 는 Floor 경로, Wall 은 Wall 경로. Floor 안에서
            //     ThemeSlot.Shelf / Dashboard 면 자동으로 BookshelfVisualSkin / DashboardVisualSkin 분기.
            int spawned = 0;
            foreach (var kv in selection.SelectedSpecs)
            {
                var entry = kv.Value;
                if (entry == null || entry.Spec == null) continue;
                switch (entry.Category)
                {
                    case OnboardingRandomThemeSelection.DecoCategory.Wall:
                        SpawnThemeWallDecoFromSpec(entry.Spec, baseTheme.AssetId);
                        break;
                    case OnboardingRandomThemeSelection.DecoCategory.Floor:
                    case OnboardingRandomThemeSelection.DecoCategory.Bookshelf:
                    default:
                        SpawnThemeFloorDecoFromSpec(entry.Spec, baseTheme.AssetId);
                        break;
                }
                spawned++;
            }
            RebuildInventorySlots();

            // (5) 영속화 — base theme name + mode=RANDOM + serialized selection. persist=false 면 skip.
            if (persist && !IsEditModeActive)
            {
                PersistLastAppliedLocalPreviewTheme(baseTheme);
                PersistLastAppliedThemeMode(ThemeModeRandom);
                PersistLastAppliedRandomThemeSelection(selection);
            }

            // (6) PATCH 흐름은 ApplyThemeSelected 와 동일 — baseTheme.IsLocalPreview 면 skip.
            //     onboarding 5개 테마는 모두 isLocalPreview=true 라 PATCH 호출 없음.
            if (!IsLocalPreviewTheme(baseTheme))
            {
                Debug.LogWarning(
                    $"[RoomEditModeController] ApplyExternalRandomThemeSelection: baseTheme '{baseTheme.name}' " +
                    "isLocalPreview=false — PATCH 흐름은 random selection 에서 지원하지 않음. visual 만 적용.");
            }

            Debug.Log(
                $"[RoomEditModeController] Apply random selection OK. slots={selection.SelectedSpecs.Count} " +
                $"spawned={spawned} baseTheme={baseTheme.name}");
            return true;
        }

        // 일반 ApplyThemeSelected 흐름이 호출. RANDOM 모드에서 단일 테마로 돌아가면 mode 키 갱신.
        private void PersistLastAppliedThemeMode(string mode)
        {
            var key = ScopedKey(LastAppliedThemeModePrefsKey);
            if (key == null) return;
            try
            {
                UnityEngine.PlayerPrefs.SetString(key, mode);
                UnityEngine.PlayerPrefs.Save();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[RoomEditModeController] PersistLastAppliedThemeMode 실패 — {ex.GetType().Name}: {ex.Message}");
            }
        }

        private string TryFindLastAppliedThemeMode()
        {
            var key = ScopedKey(LastAppliedThemeModePrefsKey);
            if (key == null) return ThemeModeSingle; // 비로그인 default — 단일 테마.
            if (!UnityEngine.PlayerPrefs.HasKey(key)) return ThemeModeSingle;
            var v = UnityEngine.PlayerPrefs.GetString(key, ThemeModeSingle);
            return string.IsNullOrEmpty(v) ? ThemeModeSingle : v;
        }

        private void PersistLastAppliedRandomThemeSelection(OnboardingRandomThemeSelection selection)
        {
            var key = ScopedKey(LastAppliedRandomThemeSelectionPrefsKey);
            if (key == null) return;
            if (selection == null)
            {
                UnityEngine.PlayerPrefs.DeleteKey(key);
                UnityEngine.PlayerPrefs.Save();
                return;
            }
            try
            {
                UnityEngine.PlayerPrefs.SetString(key, selection.SerializeToPrefs());
                UnityEngine.PlayerPrefs.Save();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[RoomEditModeController] PersistLastAppliedRandomThemeSelection 실패 — {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void ClearLastAppliedRandomThemeSelection()
        {
            var key = ScopedKey(LastAppliedRandomThemeSelectionPrefsKey);
            if (key == null) return;
            UnityEngine.PlayerPrefs.DeleteKey(key);
            UnityEngine.PlayerPrefs.Save();
        }

        private OnboardingRandomThemeSelection TryFindLastAppliedRandomThemeSelection()
        {
            var key = ScopedKey(LastAppliedRandomThemeSelectionPrefsKey);
            if (key == null) return null;
            if (!UnityEngine.PlayerPrefs.HasKey(key)) return null;
            var serialized = UnityEngine.PlayerPrefs.GetString(key, string.Empty);
            return string.IsNullOrEmpty(serialized) ? null : OnboardingRandomThemeBuilder.BuildFromSerialized(serialized);
        }

        // persist=false 호출 가능 — 온보딩 Step2 preview 처럼 사용자가 여러 카드를 둘러보는
        // 동안 PlayerPrefs 저장은 하지 않고 visual 만 적용하는 모드. 호출자가 명시.
        private void SyncInventoryThemeFilterForTheme(RoomThemeAsset theme)
        {
            // Applying a full room theme must not narrow the decorate inventory.
            // Theme filtering is only for the explicit editor/runtime filter control.
            RoomEditThemeFilter.ClearSilently();
        }

        private static string ResolveThemeFilterTag(RoomThemeAsset theme)
        {
            if (theme == null)
            {
                return RoomEditThemeFilter.AllThemesValue;
            }

            switch (theme.AssetId)
            {
                case 300000L: return "DeepSea";
                case 400000L: return "Fantasy";
                case 500000L: return "Space";
                case 600000L: return "Spooky";
            }

            var fromName = NormalizeThemeTag(theme.name);
            if (!string.IsNullOrEmpty(fromName))
            {
                if (fromName == "DeepSea" ||
                    fromName == "Fantasy" ||
                    fromName == "Space" ||
                    fromName == "Spooky" ||
                    fromName == "Vintage" ||
                    fromName == "GoodVibes" ||
                    fromName == "Pastel" ||
                    fromName == "Natural" ||
                    fromName == "Classic" ||
                    fromName == "Modern")
                {
                    return fromName;
                }
            }

            return RoomEditThemeFilter.AllThemesValue;
        }

        private void ApplyThemeSelected(RoomThemeAsset theme, ThemeApplyMode mode, bool persist = true)
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
            //
            // 슬롯 정책 (MR #157 후속): incoming 테마가 차지할 슬롯/자산에 매칭되는 placement
            // 만 hold 로 detach. 매칭 안 되는 사용자 placement 는 placedItemsRoot 에 그대로
            // 남아 테마 교체 후에도 visible 유지.
            var resetAllPlacements = mode == ThemeApplyMode.Reset;
            DetachPlacementForRollback(theme, resetAllPlacements);

            themeApplier.Apply(theme);
            SyncInventoryThemeFilterForTheme(theme);
            // 벽 sharedMaterial 이 swap 됐으므로 편집 모드 중이면 fade 즉시 재적용 (탑다운/사선에서 occlusion 복귀 방지).
            RefreshWallVisibilityAfterMaterialMutation("ApplyThemeSelected");
            RebuildInventorySlots();
            if (persist && !IsEditModeActive)
            {
                // local-preview 테마 영속화 — 다음 세션 (로그아웃/로그인) 의 ApplySpaceVisualsToRoom 가
                // server null 응답을 받았을 때 이 PlayerPrefs 값으로 복원해 wall/floor 자동 적용.
                // non-local-preview 면 서버 권위라 키 제거.
                PersistLastAppliedLocalPreviewTheme(theme);
                // mode=THEME 으로 갱신 + 이전 RANDOM selection 정리. 일반 단일 테마 흐름 진입이라
                // RestoreLastLocalPreviewThemeOnSceneInstall 가 다음 세션에 mode 보고 단일 흐름 선택.
                PersistLastAppliedThemeMode(ThemeModeSingle);
                ClearLastAppliedRandomThemeSelection();
                InvalidateThemeContentsCache(theme, "ApplyThemeSelected");
            }

            // 테마의 default entries 자동 적용 — visual 즉시 반영. PATCH 결과 무관 (401/네트워크
            // 실패해도 산뜻 자산 mesh swap 적용). 사용자가 BE 인증 안 된 dev 환경에서도 검증 가능.
            // PATCH 성공 후엔 BE 응답 themeReset 의 ReloadRoomState 가 server 권위 상태로 재동기화.
            ApplyThemeDefaultEntries(
                theme,
                persistEntries: persist && !IsEditModeActive,
                resetExistingPlacements: resetAllPlacements);

            if (persist && IsEditModeActive)
            {
                hasPendingThemeVisualSave = true;
                pendingThemeVisualSave = theme;
                pendingThemeVisualSaveMode = mode;
                SetDirty($"PendingTheme/{theme.DisplayName}");
                RebuildInventorySlots();
                return;
            }

            // persist=false (온보딩 Step2 preview 등) — PATCH 도 skip. visual 만 적용하고 종료.
            if (!persist)
            {
                DiscardPlacementRollbackHold();
                return;
            }


            // local preview 전용 테마 — placeholder (가구/벽지/바닥 미제작). 명시 플래그 + assetId<=0 fallback.
            // RoomThemeApplier 는 라이팅만 영향. POST /spaces/me/theme/{id}/apply 는 skip (401/403 회피).
            var hasServerThemeInventory = false;
            var resolvedServerThemeAssetId = 0L;
            var resolvedThemeInventoryId = 0L;
            hasServerThemeInventory = TryResolveOwnedInventoryIdForTheme(theme, out resolvedThemeInventoryId, out resolvedServerThemeAssetId);

            if (IsLocalPreviewTheme(theme) && !hasServerThemeInventory)
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

            if (theme.AssetId <= 0 && !hasServerThemeInventory)
            {
                Debug.LogWarning("[RoomEditModeController] theme AssetId가 없어 백엔드 theme apply skip (visual만 적용).");
                DiscardPlacementRollbackHold();
                return;
            }

            long inventoryId;
            if (hasServerThemeInventory)
            {
                inventoryId = resolvedThemeInventoryId;
            }
            else if (!TryResolveOwnedInventoryIdForTheme(theme, out inventoryId, out resolvedServerThemeAssetId))
            {
                Debug.LogWarning(
                    $"[RoomEditModeController] theme '{theme.DisplayName}' assetId={theme.AssetId} 의 보유 inventoryId를 찾지 못해 " +
                    "백엔드 theme apply skip. GET /v1/assets/me/inventory 응답 도착 후 다시 시도하세요.");
                DiscardPlacementRollbackHold();
                return;
            }

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
                        ApplyThemeDefaultEntries(theme, persistEntries: persist, resetExistingPlacements: resetAllPlacements);
                        DiscardPlacementRollbackHold();
                        PersistThemeResetLayoutIfNeeded(resetAllPlacements, "theme apply success");
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
                        // mesh swap + 이번 테마가 새로 spawn 한 theme deco 만 정리 (사용자 보존
                        // placement 는 placedItemsRoot 에 그대로 남아 있으므로 건드리지 않음).
                        // 그 후 hold root 의 매칭 이전 placement (slot 교체 대상이었던 것들) 만 reattach.
                        RestoreAllMeshSwaps();
                        RemoveSpawnedThemeDecos(theme.AssetId);
                        ReattachPlacementFromRollback();
                        SyncInventoryThemeFilterForTheme(previousAppliedTheme);
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
        private void EnsureThemeContentsUseCase()
        {
            if (themeContentsUseCase != null) return;
            var apiManager = APIManager.Instance;
            if (apiManager == null || !apiManager.HasAccessToken)
            {
                return;
            }

            try
            {
                var apiClient = new NotebookApiClient(apiManager);
                var repo = new ApiThemeContentsRepository(apiClient);
                themeContentsUseCase = new LoadThemeContentsUseCase(repo);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoomEditModeController] LoadThemeContentsUseCase create failed ({ex.GetType().Name}).");
                Debug.LogException(ex);
            }
        }

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

            if (IsEditModeActive)
            {
                pendingCategoryVisualSaves[category] = entry;
                SetDirty($"PendingCategory/{category}");
                return;
            }

            PersistLastAppliedEntries();

            // local preview 전용 entry — 사용자 inventory 에 없는 placeholder. PATCH 시 401(인증)/403(S006) 으로 거부되므로
            // 백엔드 호출 자체를 skip 하고 RoomLighting 하위에 Preview Light 만 갱신.
            // 명시적 IsLocalPreview 플래그 + assetId 범위 fallback 으로 robust 판정 (catalog 재생성 안 해도 동작).
            long? serverInventoryIdForPreview = null;
            if (IsLocalPreviewEntry(entry))
            {
                if (category == CustomizationCategory.InteriorLight || category == CustomizationCategory.ExteriorLight)
                {
                    ApplyLocalLightingPreview(category, entry);
                    Debug.Log($"[RoomEditModeController] Local preview lighting applied: {category}/{entry.DisplayName} (assetId={entry.AssetId})");
                }

                if (!TryResolveOwnedInventoryIdForEntry(category, entry, out var resolvedPreviewInventoryId))
                {
                    Debug.Log($"[RoomEditModeController] Skip PATCH for local preview item without server inventory: {category}/{entry.DisplayName} (assetId={entry.AssetId})");
                    return;
                }

                serverInventoryIdForPreview = resolvedPreviewInventoryId;
                Debug.Log($"[RoomEditModeController] Local catalog item is server-backed; PATCH will use inventoryId={resolvedPreviewInventoryId}: {category}/{entry.DisplayName} (assetId={entry.AssetId})");
            }

            // 백엔드 PATCH wiring
            EnsureSpaceSettingsRepository();
            if (spaceSettingsRepo == null)
            {
                Debug.LogWarning("[RoomEditModeController] ISpaceSettingsRepository 미준비 — 백엔드 PATCH skip (visual만 적용).");
                return;
            }

            int requestVersion = ++categoryRequestVersion;
            long? invId = null;
            if (entry.AssetId > 0)
            {
                if (serverInventoryIdForPreview.HasValue)
                {
                    invId = serverInventoryIdForPreview.Value;
                }
                else if (!TryResolveOwnedInventoryIdForEntry(category, entry, out var resolvedInventoryId))
                {
                    Debug.LogWarning(
                        $"[RoomEditModeController] {category}/{entry.DisplayName} assetId={entry.AssetId} 의 보유 inventoryId를 찾지 못해 " +
                        "백엔드 PATCH skip. GET /v1/assets/me/inventory 응답 도착 후 다시 시도하세요.");
                    return;
                }
                else
                {
                    invId = resolvedInventoryId;
                }
            }

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
                case CustomizationCategory.Notebook:
                    spaceSettingsRepo.ChangeNotebook(invId, onSuccess, onError); break;
                case CustomizationCategory.InteriorLight:
                    spaceSettingsRepo.ChangeInteriorLight(invId, onSuccess, onError); break;
                case CustomizationCategory.ExteriorLight:
                    spaceSettingsRepo.ChangeExteriorLight(invId, onSuccess, onError); break;
                default:
                    Debug.LogWarning($"[RoomEditModeController] {category}는 카테고리별 PATCH 미지원 (책장/벽/바닥 데코는 별도 endpoint).");
                    break;
            }
        }

        private void ApplyRemoteCategoryInventoryEntry(CustomizationCategory category, InventoryEntry entry)
        {
            if (!entry.IsRemoteAsset || entry.RemoteAssetId <= 0L)
            {
                Debug.LogWarning($"[RoomEditModeController] Remote category apply skipped: invalid remote asset for {category}/{entry.DisplayName}.");
                return;
            }

            if (!TryFindCatalogEntryForServerAssetId(category, entry.RemoteAssetId, out var localEntry) || localEntry == null)
            {
                Debug.LogWarning(
                    $"[RoomEditModeController] Remote category asset has no local visual mapping: {category}/{entry.DisplayName} " +
                    $"(assetId={entry.RemoteAssetId}, assetType={entry.ServerAssetType}).");
                return;
            }

            ProceedCustomizationEntry(category, localEntry);
        }

        private void ApplyThemeResetVisualFallback(bool persistEntries = true)
        {
            lastAppliedEntries.Clear();
            if (persistEntries)
            {
                PersistLastAppliedEntries();
            }

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
        // persistEntries=false — 온보딩 Step2 preview. lastAppliedEntries dict 는 그대로 갱신
        // (in-session visual 일관성) 되지만 PlayerPrefs 저장 호출은 skip → 다음 세션에 영향 없음.
        private void ApplyThemeDefaultEntries(
            RoomThemeAsset theme,
            bool persistEntries = true,
            bool resetExistingPlacements = false,
            ThemeContents serverContents = null,
            bool requestServerContents = true)
        {
            if (theme == null) return;
            var effectiveContents = serverContents;
            if (effectiveContents == null &&
                TryResolveOwnedServerThemeAssetId(theme, out var cachedServerThemeId) &&
                themeContentsCache.TryGetValue(cachedServerThemeId, out var cachedContents))
            {
                effectiveContents = cachedContents;
            }
            var hasServerContents = effectiveContents != null;
            var hasServerPlacementContents = HasThemePlacementContents(effectiveContents);

            Debug.Log($"[RoomEditModeController] ApplyThemeDefaultEntries 진입 — theme={theme.DisplayName}, defaults={(theme.DefaultEntries?.Count ?? 0)}, floorDecos={(theme.FloorDecoSpecs?.Count ?? 0)}, wallDecos={(theme.WallDecoSpecs?.Count ?? 0)}");

            // 정책: 테마 적용 = 그 테마 자산만 보이는 상태. 이전 테마가 spawn 한 모든
            // ThemeDeco_* (ThemeSlotMarker 부착) 를 destroy. 사용자 직접 placement
            // (마커 없음) 는 보존. MR #157 의 슬롯 매칭 정책은 spec 에 themeSlot 이 비어
            // 있을 때 중복 spawn 회귀를 일으켜 폐기. RemovePlacementsForIncomingTheme
            // 는 leftover hold-root 매칭 보강 용도로 호출 유지.
            if (resetExistingPlacements)
            {
                ClearAllPlacedDecos();
                ClearSlotVisualSkinsForThemeReset();
            }
            else
            {
                ClearAllThemeSpawnedDecos();
                RemovePlacementsForIncomingTheme(theme);
                // ThemeSlotMarker 없이 placedItemsRoot 에 남아있는 saved-restored / user-placed
                // 자산 중 incoming 테마와 다른 prefix 를 가진 것들을 추가로 destroy. 위 두 cleanup
                // 이 못 잡는 회귀 (이전 세션 spooky 자산이 vintage 적용 후에도 남는 케이스) 차단.
                RemoveDecosWithMismatchedThemePrefix(theme);
            }

            // 1.5) 이전 mesh swap 모두 native 복원. theme 의 defaultEntries 에 해당 카테고리
            // entry 가 있으면 step 2 에서 다시 swap 적용. 없으면 native 유지 (기본 책상/책장 복귀).
            // 이게 없으면 이전 산뜻 테마의 mesh swap 이 default theme 적용 후에도 유지됨.
            RestoreAllMeshSwaps();

            // 2) 항시 카테고리 (Wallpaper/Floor/Desk/BookshelfSkin/DashboardSkin/InteriorLight/
            //    ExteriorLight) — ApplyEntryVisual 로 mesh / material / light swap
            var defaults = theme.DefaultEntries;
            if (!hasServerContents && defaults != null)
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
                    TryRepairFantasyDeskPrefabReference(catalog, entry, "ApplyThemeDefaultEntries");
                    if (IsFantasyTheme(theme) && def.Category == CustomizationCategory.Desk)
                    {
                        Debug.Log($"[FantasyDesk] ApplyThemeDefaultEntries theme={theme.DisplayName} category={def.Category} assetId={def.AssetId}");
                    }
                    ApplyEntryVisual(def.Category, entry);
                    if (entry.AssetId > 0) lastAppliedEntries[def.Category] = entry.AssetId;
                }
                if (persistEntries) PersistLastAppliedEntries();
            }

            // 3) 설치 카테고리 (FloorDeco/WallDeco) — placedItemsRoot 자식으로 spawn,
            //    DecorationItemMarker + ThemeSlotMarker 추가 → 사용자가 drag/select/delete 가능 +
            //    다음 테마 적용 시 슬롯 매칭 destroy 대상으로 식별.
            if (effectiveContents != null)
            {
                ApplyServerCategoryDefaults(effectiveContents, persistEntries);
            }

            var spawnedFromServerContents = 0;
            if (hasServerPlacementContents)
            {
                spawnedFromServerContents = SpawnThemeChildrenFromContents(theme, effectiveContents);
            }

            if (!hasServerPlacementContents ||
                (hasServerPlacementContents && spawnedFromServerContents == 0 && HasLocalThemePlacementSpecs(theme)))
            {
                if (hasServerPlacementContents)
                {
                    Debug.LogWarning($"[RoomEdit][ThemeContents] server children for theme={theme.DisplayName} did not match local specs; using local theme placement specs.");
                }

                SpawnLocalThemePlacementSpecs(theme);
            }

            if (!hasServerContents && requestServerContents)
            {
                RequestThemeContentsReapplyIfAvailable(theme, persistEntries, resetExistingPlacements);
            }

            RebuildInventorySlots();
        }

        // 슬롯 기반 교체 — incoming 테마의 spec 들이 채우려는 슬롯/자산 집합을 모은 뒤
        // placedItemsRoot 자식 중 매칭만 destroy. 슬롯 무관한 사용자 placement (slot=None,
        // assetKey 매칭 안 되는 InventoryKey) 는 보존. 모든 테마에 공통 적용 — 빈티지만이 아니라
        // Natural/Classic/Modern/Pastel/GoodVibes 도 같은 경로.
        //
        // 매칭 규칙 (강 → 약):
        //   1. ThemeSlotMarker.Slot 동일 (incoming spec 들의 슬롯 집합과 매칭)
        //   2. DecorationItemMarker.InventoryKey 동일 (incoming spec 들의 assetKey 와 매칭)
        //
        // 좌표 기반 매칭은 의도적으로 사용 안 함 — 사용자가 슬롯 자산을 옮겨도 슬롯/자산 신원으로 식별.
        // Shelf and dashboard theme assets are attached to functional scene roots, not placedItemsRoot.
        // RESET must clear them separately so a previous theme skin cannot survive the full reset.
        private static bool HasThemePlacementContents(ThemeContents contents)
        {
            var children = contents?.Children;
            if (children == null || children.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child == null)
                {
                    continue;
                }

                if (child.AssetId > 0L ||
                    !string.IsNullOrWhiteSpace(child.AssetType) ||
                    !string.IsNullOrWhiteSpace(child.Role) ||
                    !string.IsNullOrWhiteSpace(child.DefaultTransform))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasLocalThemePlacementSpecs(RoomThemeAsset theme)
        {
            return (theme?.FloorDecoSpecs != null && theme.FloorDecoSpecs.Count > 0) ||
                   (theme?.WallDecoSpecs != null && theme.WallDecoSpecs.Count > 0);
        }

        private void SpawnLocalThemePlacementSpecs(RoomThemeAsset theme)
        {
            if (theme == null)
            {
                return;
            }

            if (theme.FloorDecoSpecs != null)
            {
                for (int i = 0; i < theme.FloorDecoSpecs.Count; i++)
                {
                    SpawnThemeFloorDecoFromSpec(theme.FloorDecoSpecs[i], theme.AssetId);
                }
            }

            if (theme.WallDecoSpecs != null)
            {
                for (int i = 0; i < theme.WallDecoSpecs.Count; i++)
                {
                    SpawnThemeWallDecoFromSpec(theme.WallDecoSpecs[i], theme.AssetId);
                }
            }
        }

        private void ApplyThemeCategoryDefaultsForMissingServerCategories(
            RoomThemeAsset theme,
            SpaceResponseDto space,
            bool persistEntries = true)
        {
            if (theme == null || space == null)
            {
                return;
            }

            if (TryGetCachedThemeContents(theme, out var contents))
            {
                var serverChanged = false;
                serverChanged |= TryApplyServerCategoryDefaultIfMissing(space, CustomizationCategory.Wallpaper, contents.Wallpaper);
                serverChanged |= TryApplyServerCategoryDefaultIfMissing(space, CustomizationCategory.Floor, contents.Floor);
                serverChanged |= TryApplyServerCategoryDefaultIfMissing(space, CustomizationCategory.Desk, contents.Desk);
                serverChanged |= TryApplyServerCategoryDefaultIfMissing(space, CustomizationCategory.DashboardSkin, contents.Dashboard);
                serverChanged |= TryApplyServerCategoryDefaultIfMissing(space, CustomizationCategory.BookshelfSkin, contents.BookshelfSkinDefault);
                serverChanged |= TryApplyServerCategoryDefaultIfMissing(space, CustomizationCategory.InteriorLight, contents.InteriorLight);
                serverChanged |= TryApplyServerCategoryDefaultIfMissing(space, CustomizationCategory.ExteriorLight, contents.ExteriorLight);
                if (serverChanged && persistEntries)
                {
                    PersistLastAppliedEntries();
                }
                return;
            }

            var changed = false;
            var defaults = theme.DefaultEntries;
            if (defaults != null)
            {
                for (int i = 0; i < defaults.Count; i++)
                {
                    var def = defaults[i];
                    if (def == null || !IsServerCategoryInventoryMissing(def.Category, space))
                    {
                        continue;
                    }

                    changed |= TryApplyLocalThemeCategoryDefault(theme, def, "ApplySpaceVisualsToRoom.missingServerCategory");
                }
            }

            if (changed && persistEntries)
            {
                PersistLastAppliedEntries();
            }
        }

        private bool TryApplyLocalThemeCategoryDefault(RoomThemeAsset theme, CategoryDefault def, string source)
        {
            if (theme == null || def == null)
            {
                return false;
            }

            var catalog = LoadCategoryCatalog(def.Category);
            if (catalog == null || catalog.Entries == null)
            {
                Debug.LogWarning($"[RoomEditModeController] catalog not found for theme default: {def.Category}.");
                return false;
            }

            var entry = catalog.FindByAssetId(def.AssetId);
            if (entry == null)
            {
                Debug.LogWarning($"[RoomEditModeController] {theme.DisplayName} default entry {def.Category}:{def.AssetId} has no local catalog mapping.");
                return false;
            }

            TryRepairFantasyDeskPrefabReference(catalog, entry, source);
            if (IsFantasyTheme(theme) && def.Category == CustomizationCategory.Desk)
            {
                Debug.Log($"[FantasyDesk] {source} theme={theme.DisplayName} category={def.Category} assetId={def.AssetId}");
            }

            ApplyEntryVisual(def.Category, entry);
            if (entry.AssetId > 0L)
            {
                lastAppliedEntries[def.Category] = entry.AssetId;
            }
            else
            {
                lastAppliedEntries.Remove(def.Category);
            }

            return true;
        }

        private bool TryApplyServerCategoryDefaultIfMissing(SpaceResponseDto space, CustomizationCategory category, long? serverAssetId)
        {
            return IsServerCategoryInventoryMissing(category, space) &&
                   TryApplyServerCategoryDefault(category, serverAssetId);
        }

        private static bool IsServerCategoryInventoryMissing(CustomizationCategory category, SpaceResponseDto space)
        {
            if (space == null)
            {
                return true;
            }

            var inventoryId = GetCategoryIdFromSpace(category, space);
            return !inventoryId.HasValue || inventoryId.Value <= 0L;
        }

        private bool ShouldPreserveMissingServerCategoryVisual(CustomizationCategory category)
        {
            if (!lastAppliedEntries.ContainsKey(category))
            {
                return false;
            }

            if (themeApplier == null)
            {
                themeApplier = FindFirstObjectByType<RoomThemeApplier>();
            }

            if (themeApplier == null || themeApplier.CurrentTheme == null)
            {
                return true;
            }

            if (TryGetCachedThemeContents(themeApplier.CurrentTheme, out _))
            {
                return ThemeHasCategoryDefault(themeApplier.CurrentTheme, category);
            }

            return true;
        }

        private bool ShouldSkipCatalogFallbackForServerOmittedCategory(CustomizationCategory category)
        {
            if (themeApplier == null)
            {
                themeApplier = FindFirstObjectByType<RoomThemeApplier>();
            }

            return themeApplier != null &&
                   themeApplier.CurrentTheme != null &&
                   TryGetCachedThemeContents(themeApplier.CurrentTheme, out _) &&
                   !ThemeHasCategoryDefault(themeApplier.CurrentTheme, category);
        }

        private bool ThemeHasCategoryDefault(RoomThemeAsset theme, CustomizationCategory category)
        {
            if (theme == null)
            {
                return false;
            }

            if (TryGetCachedThemeContents(theme, out var contents))
            {
                return TryGetThemeContentsCategoryDefault(contents, category, out _);
            }

            var defaults = theme.DefaultEntries;
            if (defaults != null)
            {
                for (int i = 0; i < defaults.Count; i++)
                {
                    var def = defaults[i];
                    if (def != null && def.Category == category && def.AssetId > 0L)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryGetCachedThemeContents(RoomThemeAsset theme, out ThemeContents contents)
        {
            contents = null;
            return theme != null &&
                   TryResolveOwnedServerThemeAssetId(theme, out var serverThemeAssetId) &&
                   themeContentsCache.TryGetValue(serverThemeAssetId, out contents) &&
                   contents != null;
        }

        private static bool TryGetThemeContentsCategoryDefault(ThemeContents contents, CustomizationCategory category, out long serverAssetId)
        {
            serverAssetId = 0L;
            if (contents == null)
            {
                return false;
            }

            long? value;
            switch (category)
            {
                case CustomizationCategory.Wallpaper:     value = contents.Wallpaper; break;
                case CustomizationCategory.Floor:         value = contents.Floor; break;
                case CustomizationCategory.Desk:          value = contents.Desk; break;
                case CustomizationCategory.DashboardSkin: value = contents.Dashboard; break;
                case CustomizationCategory.BookshelfSkin: value = contents.BookshelfSkinDefault; break;
                case CustomizationCategory.InteriorLight: value = contents.InteriorLight; break;
                case CustomizationCategory.ExteriorLight: value = contents.ExteriorLight; break;
                default: value = null; break;
            }

            if (!value.HasValue || value.Value <= 0L)
            {
                return false;
            }

            serverAssetId = value.Value;
            return true;
        }

        private void ApplyServerCategoryDefaults(ThemeContents contents, bool persistEntries)
        {
            if (contents == null)
            {
                return;
            }

            var changed = false;
            changed |= ApplyServerCategoryDefaultAuthoritative(CustomizationCategory.Wallpaper, contents.Wallpaper);
            changed |= ApplyServerCategoryDefaultAuthoritative(CustomizationCategory.Floor, contents.Floor);
            changed |= ApplyServerCategoryDefaultAuthoritative(CustomizationCategory.Desk, contents.Desk);
            changed |= ApplyServerCategoryDefaultAuthoritative(CustomizationCategory.DashboardSkin, contents.Dashboard);
            changed |= ApplyServerCategoryDefaultAuthoritative(CustomizationCategory.BookshelfSkin, contents.BookshelfSkinDefault);
            changed |= ApplyServerCategoryDefaultAuthoritative(CustomizationCategory.InteriorLight, contents.InteriorLight);
            changed |= ApplyServerCategoryDefaultAuthoritative(CustomizationCategory.ExteriorLight, contents.ExteriorLight);

            if (changed && persistEntries)
            {
                PersistLastAppliedEntries();
            }
        }

        private bool ApplyServerCategoryDefaultAuthoritative(CustomizationCategory category, long? serverAssetId)
        {
            if (serverAssetId.HasValue && serverAssetId.Value > 0L)
            {
                return TryApplyServerCategoryDefault(category, serverAssetId);
            }

            return lastAppliedEntries.Remove(category);
        }

        private bool TryApplyServerCategoryDefault(CustomizationCategory category, long? serverAssetId)
        {
            if (!serverAssetId.HasValue || serverAssetId.Value <= 0L)
            {
                return false;
            }

            var catalog = LoadCategoryCatalog(category);
            if (catalog == null || catalog.Entries == null)
            {
                return false;
            }

            if (!TryFindCatalogEntryForServerAssetId(category, serverAssetId.Value, catalog, out var entry) || entry == null)
            {
                Debug.LogWarning($"[RoomEdit][ThemeContents] {category}: server assetId={serverAssetId.Value} has no matching local catalog entry.");
                return false;
            }

            TryRepairFantasyDeskPrefabReference(catalog, entry, "ThemeContents");
            ApplyEntryVisual(category, entry);
            if (entry.AssetId > 0L)
            {
                lastAppliedEntries[category] = entry.AssetId;
            }
            else
            {
                lastAppliedEntries.Remove(category);
            }

            return true;
        }

        private int SpawnThemeChildrenFromContents(RoomThemeAsset theme, ThemeContents contents)
        {
            var children = contents?.Children;
            if (theme == null || children == null || children.Count == 0)
            {
                return 0;
            }

            var spawned = 0;
            var unmatched = 0;
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child == null)
                {
                    continue;
                }

                var transform = ParseThemeContentTransform(child.DefaultTransform);
                var assetKey = ResolveThemeChildAssetKey(child, transform);
                if (!TryFindThemeChildSpec(theme, assetKey, child.Role, transform, out var spec, out var isWallSpec))
                {
                    unmatched++;
                    continue;
                }

                if (isWallSpec || IsWallPlacement(transform))
                {
                    SpawnThemeWallDecoFromSpec(spec, theme.AssetId, transform);
                }
                else
                {
                    SpawnThemeFloorDecoFromSpec(spec, theme.AssetId, transform);
                }
                spawned++;
            }

            if (spawned > 0 || unmatched > 0)
            {
                Debug.Log($"[RoomEdit][ThemeContents] theme={theme.DisplayName} spawnedChildren={spawned}, unmatchedChildren={unmatched}.");
            }

            return spawned;
        }

        private ThemeContentTransform ParseThemeContentTransform(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<ThemeContentTransform>(raw);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RoomEdit][ThemeContents] defaultTransform parse failed: {ex.GetType().Name} {ex.Message}");
                return null;
            }
        }

        private string ResolveThemeChildAssetKey(ThemeContentItem child, ThemeContentTransform transform)
        {
            var key = FirstNonEmpty(transform?.AssetKey);
            if (!string.IsNullOrEmpty(key))
            {
                return key;
            }

            if (child != null &&
                child.AssetId > 0L &&
                ownedAssetsByAssetId.TryGetValue(child.AssetId, out var asset))
            {
                return FirstNonEmpty(asset.UnityResourceKey);
            }

            return string.Empty;
        }

        private bool TryFindThemeChildSpec(
            RoomThemeAsset theme,
            string assetKey,
            string role,
            ThemeContentTransform transform,
            out RoomThemeDecoSpec spec,
            out bool isWallSpec)
        {
            spec = null;
            isWallSpec = false;
            if (theme == null)
            {
                return false;
            }

            var inventoryKey = BuildInventoryKeyFromAssetKey(assetKey);
            if (!string.IsNullOrEmpty(inventoryKey))
            {
                if (TryFindSpecByAssetKeyMatch(theme.WallDecoSpecs, inventoryKey, out spec))
                {
                    isWallSpec = true;
                    return true;
                }
                if (TryFindSpecByAssetKeyMatch(theme.FloorDecoSpecs, inventoryKey, out spec))
                {
                    return true;
                }
                if (TryFindSpecByAssetKeyMatch(theme.BookshelfDecoSpecs, inventoryKey, out spec))
                {
                    return true;
                }
            }

            if (TryParseThemeSlot(FirstNonEmpty(transform?.ThemeSlot, role), out var slot))
            {
                if (TryFindSpecBySlot(theme.WallDecoSpecs, slot, out spec))
                {
                    isWallSpec = true;
                    return true;
                }
                if (TryFindSpecBySlot(theme.FloorDecoSpecs, slot, out spec))
                {
                    return true;
                }
                if (TryFindSpecBySlot(theme.BookshelfDecoSpecs, slot, out spec))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseThemeSlot(string raw, out ThemeSlot slot)
        {
            slot = ThemeSlot.None;
            var normalized = NormalizeIdentifier(raw);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            switch (normalized)
            {
                case "SHELF": slot = ThemeSlot.Shelf; return true;
                case "TABLE": slot = ThemeSlot.Table; return true;
                case "TABLEMINI": slot = ThemeSlot.TableMini; return true;
                case "LAMP": slot = ThemeSlot.Lamp; return true;
                case "FRAME": slot = ThemeSlot.Frame; return true;
                case "RUG": slot = ThemeSlot.Rug; return true;
                case "BED": slot = ThemeSlot.Bed; return true;
                case "WINDOW": slot = ThemeSlot.Window; return true;
                case "PLANT": slot = ThemeSlot.Plant; return true;
                case "DASHBOARD": slot = ThemeSlot.Dashboard; return true;
                default: return false;
            }
        }

        private static bool IsWallPlacement(ThemeContentTransform transform)
        {
            if (transform == null)
            {
                return false;
            }

            var placement = NormalizeIdentifier(transform.PlacementCategory);
            return placement == "WALLDECO" || placement == "WALL";
        }

        private static WallSide ResolveWallSide(RoomThemeDecoSpec spec, ThemeContentTransform transform)
        {
            if (TryParseWallSide(transform?.WallSide, out var side))
            {
                return side;
            }

            return spec != null ? spec.WallSide : WallSide.None;
        }

        private static bool TryParseWallSide(string raw, out WallSide side)
        {
            side = WallSide.None;
            switch (NormalizeIdentifier(raw))
            {
                case "LEFT": side = WallSide.Left; return true;
                case "RIGHT": side = WallSide.Right; return true;
                case "BACK": side = WallSide.Back; return true;
                case "FRONT": side = WallSide.Front; return true;
                default: return false;
            }
        }

        private void InvalidateThemeContentsCache(RoomThemeAsset theme, string source)
        {
            if (!TryResolveThemeContentsCacheKey(theme, out var serverThemeAssetId))
            {
                return;
            }

            themeContentsCache.Remove(serverThemeAssetId);
            themeContentsInFlightVersions.Remove(serverThemeAssetId);
            themeContentsSignatures.Remove(serverThemeAssetId);
            themeContentsLastRefreshRealtime.Remove(serverThemeAssetId);
            BumpThemeContentsRequestVersion(serverThemeAssetId);
            Debug.Log($"[RoomEdit][ThemeContents] cache invalidated themeAssetId={serverThemeAssetId} source={source}.");
        }

        private bool TryResolveThemeContentsCacheKey(RoomThemeAsset theme, out long serverThemeAssetId)
        {
            if (TryResolveOwnedServerThemeAssetId(theme, out serverThemeAssetId) && serverThemeAssetId > 0L)
            {
                return true;
            }

            serverThemeAssetId = theme != null ? theme.AssetId : 0L;
            return serverThemeAssetId > 0L;
        }

        private int BumpThemeContentsRequestVersion(long serverThemeAssetId)
        {
            themeContentsRequestVersions.TryGetValue(serverThemeAssetId, out var version);
            version++;
            themeContentsRequestVersions[serverThemeAssetId] = version;
            return version;
        }

        private int NextThemeContentsRequestVersion(long serverThemeAssetId)
        {
            var version = BumpThemeContentsRequestVersion(serverThemeAssetId);
            themeContentsInFlightVersions[serverThemeAssetId] = version;
            return version;
        }

        private bool IsLatestThemeContentsRequest(long serverThemeAssetId, int requestVersion)
        {
            return themeContentsRequestVersions.TryGetValue(serverThemeAssetId, out var latestVersion) &&
                   latestVersion == requestVersion;
        }

        private void ClearThemeContentsInFlight(long serverThemeAssetId, int requestVersion)
        {
            if (themeContentsInFlightVersions.TryGetValue(serverThemeAssetId, out var inFlightVersion) &&
                inFlightVersion == requestVersion)
            {
                themeContentsInFlightVersions.Remove(serverThemeAssetId);
            }
        }

        private string BuildThemeContentsSignature(ThemeContents contents)
        {
            if (contents == null)
            {
                return string.Empty;
            }

            var sb = new System.Text.StringBuilder(256);
            sb.Append(contents.ThemeAssetId).Append('|')
              .Append(contents.Wallpaper ?? 0L).Append('|')
              .Append(contents.Floor ?? 0L).Append('|')
              .Append(contents.Desk ?? 0L).Append('|')
              .Append(contents.Dashboard ?? 0L).Append('|')
              .Append(contents.BookshelfSkinDefault ?? 0L).Append('|')
              .Append(contents.InteriorLight ?? 0L).Append('|')
              .Append(contents.ExteriorLight ?? 0L);

            var children = contents.Children;
            if (children != null)
            {
                for (int i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    if (child == null)
                    {
                        continue;
                    }

                    sb.Append("|c:")
                      .Append(child.AssetId).Append(',')
                      .Append(child.AssetType ?? string.Empty).Append(',')
                      .Append(child.Role ?? string.Empty).Append(',')
                      .Append(child.Required ? '1' : '0').Append(',')
                      .Append(child.DefaultTransform ?? string.Empty);
                }
            }

            return sb.ToString();
        }

        private void RequestThemeContentsBackgroundRefresh(RoomThemeAsset theme, string source)
        {
            if (IsReadOnlySpacePreview || isApplyingReadOnlySpacePreviewVisuals)
            {
                return;
            }

            if (!TryResolveOwnedServerThemeAssetId(theme, out var serverThemeAssetId) || serverThemeAssetId <= 0L)
            {
                return;
            }

            if (themeContentsInFlightVersions.ContainsKey(serverThemeAssetId))
            {
                return;
            }

            if (themeContentsLastRefreshRealtime.TryGetValue(serverThemeAssetId, out var lastRefreshAt) &&
                Time.realtimeSinceStartup - lastRefreshAt < ThemeContentsBackgroundRefreshMinIntervalSeconds)
            {
                return;
            }

            RequestThemeContentsReapplyIfAvailable(
                theme,
                persistEntries: true,
                resetExistingPlacements: false,
                forceRefresh: true,
                applyThemeDefaultsOnSuccess: false,
                source: source);
        }

        private bool IsCurrentThemeForContentsResponse(RoomThemeAsset expectedTheme, long expectedServerThemeAssetId)
        {
            if (expectedTheme == null)
            {
                return false;
            }

            if (themeApplier == null)
            {
                themeApplier = FindFirstObjectByType<RoomThemeApplier>();
            }

            var currentTheme = themeApplier != null ? themeApplier.CurrentTheme : null;
            if (currentTheme == null)
            {
                return false;
            }

            if (ReferenceEquals(currentTheme, expectedTheme))
            {
                return true;
            }

            if (currentTheme.AssetId > 0L &&
                expectedTheme.AssetId > 0L &&
                currentTheme.AssetId == expectedTheme.AssetId)
            {
                return true;
            }

            return expectedServerThemeAssetId > 0L &&
                   currentTheme.AssetId == expectedServerThemeAssetId;
        }

        private void RequestThemeContentsReapplyIfAvailable(
            RoomThemeAsset theme,
            bool persistEntries,
            bool resetExistingPlacements,
            bool forceRefresh = false,
            bool applyThemeDefaultsOnSuccess = true,
            string source = null)
        {
            if (theme == null)
            {
                return;
            }

            EnsureThemeContentsUseCase();
            if (themeContentsUseCase == null)
            {
                return;
            }

            if (!TryResolveOwnedServerThemeAssetId(theme, out var serverThemeAssetId) || serverThemeAssetId <= 0L)
            {
                return;
            }

            if (!forceRefresh && themeContentsCache.ContainsKey(serverThemeAssetId))
            {
                return;
            }

            if (themeContentsInFlightVersions.ContainsKey(serverThemeAssetId))
            {
                return;
            }

            var requestVersion = NextThemeContentsRequestVersion(serverThemeAssetId);
            var layoutVersionAtRequest = externalDecorationLayoutVersion;
            themeContentsUseCase.Execute(
                serverThemeAssetId,
                contents =>
                {
                    if (this == null)
                    {
                        return;
                    }

                    ClearThemeContentsInFlight(serverThemeAssetId, requestVersion);
                    if (!IsLatestThemeContentsRequest(serverThemeAssetId, requestVersion) || contents == null)
                    {
                        return;
                    }

                    var signature = BuildThemeContentsSignature(contents);
                    var unchanged = themeContentsCache.ContainsKey(serverThemeAssetId) &&
                                    themeContentsSignatures.TryGetValue(serverThemeAssetId, out var previousSignature) &&
                                    string.Equals(previousSignature, signature, StringComparison.Ordinal);

                    themeContentsCache[serverThemeAssetId] = contents;
                    themeContentsSignatures[serverThemeAssetId] = signature;
                    themeContentsLastRefreshRealtime[serverThemeAssetId] = Time.realtimeSinceStartup;

                    if (!IsCurrentThemeForContentsResponse(theme, serverThemeAssetId))
                    {
                        return;
                    }

                    if (!applyThemeDefaultsOnSuccess)
                    {
                        Debug.Log($"[RoomEdit][ThemeContents] cache refreshed themeAssetId={serverThemeAssetId} source={source ?? "refresh"}; visual reapply skipped.");
                        return;
                    }

                    if (IsReadOnlySpacePreview || isApplyingReadOnlySpacePreviewVisuals)
                    {
                        Debug.Log($"[RoomEdit][ThemeContents] reapply skipped in read-only preview themeAssetId={serverThemeAssetId} source={source ?? "refresh"}.");
                        return;
                    }

                    if (!resetExistingPlacements && externalDecorationLayoutVersion != layoutVersionAtRequest)
                    {
                        Debug.Log($"[RoomEdit][ThemeContents] reapply skipped because server layout changed themeAssetId={serverThemeAssetId} source={source ?? "refresh"}.");
                        return;
                    }

                    if (unchanged && !resetExistingPlacements)
                    {
                        Debug.Log($"[RoomEdit][ThemeContents] unchanged themeAssetId={serverThemeAssetId} source={source ?? "refresh"}; reapply skipped.");
                        return;
                    }

                    ApplyThemeDefaultEntries(theme, persistEntries, resetExistingPlacements, contents);
                    ReapplyExplicitServerCategoryOverridesIfCurrentTheme(theme);
                },
                error =>
                {
                    if (this == null)
                    {
                        return;
                    }

                    ClearThemeContentsInFlight(serverThemeAssetId, requestVersion);
                    Debug.LogWarning($"[RoomEdit][ThemeContents] load failed themeAssetId={serverThemeAssetId}: {error?.Code}/{error?.Message}");
                });
        }

        private static void ClearSlotVisualSkinsForThemeReset()
        {
            BookshelfVisualSkinApplier.ClearSkin("theme reset");
            DashboardVisualSkinApplier.ClearSkin("theme reset");
        }

        // RESET mode makes the theme defaults the new persisted room layout.
        private void PersistThemeResetLayoutIfNeeded(bool shouldPersist, string reason)
        {
            if (!shouldPersist)
            {
                return;
            }

            if (IsReadOnlySpacePreview)
            {
                Debug.Log($"[RoomEdit][Theme] skipped reset layout persist in read-only space preview ({reason}).");
                return;
            }

            if (UnityEngine.Application.isPlaying && isActiveAndEnabled)
            {
                StartCoroutine(PersistThemeResetLayoutNextFrame(reason));
                return;
            }

            PersistThemeResetLayoutNow(reason);
        }

        private IEnumerator PersistThemeResetLayoutNextFrame(string reason)
        {
            yield return null;
            PersistThemeResetLayoutNow(reason);
        }

        private void PersistThemeResetLayoutNow(string reason)
        {
            if (IsReadOnlySpacePreview)
            {
                Debug.Log($"[RoomEdit][Theme] skipped reset layout persist in read-only space preview ({reason}).");
                return;
            }

            EnsureDecorationUseCases();
            var items = CollectDecorationLayoutItems(scopeToPlacedItemsRoot: true);
            Debug.Log($"[RoomEdit][Theme] Persist reset layout ({reason}) - {items.Count} item(s).");

            if (saveDecorationsUseCase == null)
            {
                TrySaveLayoutSnapshotToServer(items);
                TryUploadRoomThumbnail();
                return;
            }

            saveDecorationsUseCase.Execute(
                items,
                onSuccess: () =>
                {
                    TrySaveLayoutSnapshotToServer(items);
                    TryUploadRoomThumbnail();
                    ClearDirty("theme reset layout persisted");
                },
                onError: msg =>
                {
                    Debug.LogWarning($"[RoomEdit][Theme] local reset layout save failed ({reason}): {msg}");
                    TrySaveLayoutSnapshotToServer(items);
                    TryUploadRoomThumbnail();
                });
        }

        private void RemovePlacementsForIncomingTheme(RoomThemeAsset theme)
        {
            if (placedItemsRoot == null) return;
            if (theme == null) return;

            // incoming 테마가 차지할 슬롯 + assetKey 집합
            var incomingSlots = new HashSet<ThemeSlot>();
            var incomingAssetKeys = new HashSet<string>(StringComparer.Ordinal);
            CollectIncomingSlotsAndKeys(theme.FloorDecoSpecs, incomingSlots, incomingAssetKeys);
            CollectIncomingSlotsAndKeys(theme.WallDecoSpecs, incomingSlots, incomingAssetKeys);
            CollectIncomingSlotsAndKeys(theme.BookshelfDecoSpecs, incomingSlots, incomingAssetKeys);

            int removed = 0, preserved = 0;
            for (int i = placedItemsRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = placedItemsRoot.transform.GetChild(i).gameObject;
                if (ShouldReplaceForIncomingTheme(child, incomingSlots, incomingAssetKeys))
                {
                    if (selectedObject == child) selectedObject = null;
                    if (draggingObject == child) draggingObject = null;
                    var marker = child.GetComponent<DecorationItemMarker>();
                    var key = marker != null ? marker.InventoryKey : null;
                    if (!string.IsNullOrEmpty(key) && placedByInventoryKey.TryGetValue(key, out var tracked) && tracked == child)
                    {
                        placedByInventoryKey.Remove(key);
                    }
                    if (UnityEngine.Application.isPlaying) Destroy(child);
                    else DestroyImmediate(child);
                    removed++;
                }
                else
                {
                    preserved++;
                }
            }
            Debug.Log($"[RoomEditModeController][ThemePolicy] '{theme.DisplayName}' incomingSlots=[{string.Join(",", incomingSlots)}] incomingAssetKeys.count={incomingAssetKeys.Count} → removed={removed}, preserved={preserved}.");

            // ThemeSlot.Shelf 는 placedItemsRoot 가 아닌 BookshelfRoot 자식 (visual skin) 으로
            // 산다 — 위 loop 에서 정리되지 않는다. incoming 에 Shelf 슬롯이 있으면 TryAttach 가
            // 자체적으로 기존 skin 을 destroy 후 새 skin 으로 교체하므로 여기서 별도 호출 불필요.
            // incoming 에 Shelf 가 없을 때만 (예: 빈티지 → 모던/기본) 기존 skin 명시적 clear.
            if (!incomingSlots.Contains(ThemeSlot.Shelf))
            {
                BookshelfVisualSkinApplier.ClearSkin("RemovePlacementsForIncomingTheme");
            }

            // ThemeSlot.Dashboard 도 동일 — GuestbookBoard/DashboardScreenPanel 자식으로 살아서
            // placedItemsRoot loop 에서 정리 안 됨. incoming 에 Dashboard 슬롯이 있으면 TryAttach
            // 가 idempotent 교체. 없으면 (다른 테마로 전환) 명시적 clear.
            if (!incomingSlots.Contains(ThemeSlot.Dashboard))
            {
                DashboardVisualSkinApplier.ClearSkin("RemovePlacementsForIncomingTheme");
            }
        }

        private static void CollectIncomingSlotsAndKeys(IReadOnlyList<RoomThemeDecoSpec> specs, HashSet<ThemeSlot> slots, HashSet<string> assetKeys)
        {
            if (specs == null) return;
            for (int i = 0; i < specs.Count; i++)
            {
                var spec = specs[i];
                if (spec == null) continue;
                if (spec.ThemeSlot != ThemeSlot.None) slots.Add(spec.ThemeSlot);
                if (!string.IsNullOrEmpty(spec.AssetKey)) assetKeys.Add(BuildInventoryKeyFromAssetKey(spec.AssetKey));
            }
        }

        private static bool ShouldReplaceForIncomingTheme(GameObject child, HashSet<ThemeSlot> incomingSlots, HashSet<string> incomingAssetKeys)
        {
            if (child == null) return false;
            // 1차 — ThemeSlotMarker 의 슬롯이 incoming 슬롯 집합에 있으면 교체.
            var slotMarker = child.GetComponent<ThemeSlotMarker>();
            if (slotMarker != null && slotMarker.ThemeSlot != ThemeSlot.None && incomingSlots.Contains(slotMarker.ThemeSlot))
            {
                return true;
            }
            // 2차 — InventoryKey 가 incoming assetKey 집합에 있으면 교체 (동일 자산 중복 방지).
            var deco = child.GetComponent<DecorationItemMarker>();
            if (deco != null && !string.IsNullOrEmpty(deco.InventoryKey) && incomingAssetKeys.Contains(deco.InventoryKey))
            {
                return true;
            }
            return false;
        }

        // 현재 RoomThemeApplier 가 마지막 적용한 테마에 해당 ThemeSlot 의 spec 이 있는지 확인.
        // 있으면 spec 반환. saved layout 의 transform 이 preset 을 덮어쓰지 않도록 restore
        // 경로에서 가드용 — 사용자가 빈티지 버튼을 누른 이후엔 빈티지 spec 이 항상 priority.
        private bool IsSlotInCurrentTheme(ThemeSlot slot, out RoomThemeDecoSpec spec)
        {
            spec = null;
            if (themeApplier == null) themeApplier = FindFirstObjectByType<Dotshelf.Scripts.RoomTheme.RoomThemeApplier>();
            var theme = themeApplier != null ? themeApplier.CurrentTheme : null;
            if (theme == null) return false;
            if (TryFindSpecBySlot(theme.FloorDecoSpecs, slot, out spec)) return true;
            if (TryFindSpecBySlot(theme.WallDecoSpecs, slot, out spec)) return true;
            if (TryFindSpecBySlot(theme.BookshelfDecoSpecs, slot, out spec)) return true;
            return false;
        }

        private static bool TryFindSpecBySlot(IReadOnlyList<RoomThemeDecoSpec> specs, ThemeSlot slot, out RoomThemeDecoSpec found)
        {
            found = null;
            if (specs == null) return false;
            for (int i = 0; i < specs.Count; i++)
            {
                var s = specs[i];
                if (s != null && s.ThemeSlot == slot)
                {
                    found = s;
                    return true;
                }
            }
            return false;
        }

        // assetKey → InventoryKey 변환. EditableDecorationCatalog 의 SourceAssetPath 와
        // 매핑되는 키 형식을 그대로 따라 동일 자산이 사용자 placement / 테마 spawn 양쪽에서
        // 동일하게 식별되도록 정렬. BuildInventoryKey 와 같은 prefix 사용.
        internal static string BuildInventoryKeyFromAssetKey(string assetKey)
        {
            if (string.IsNullOrEmpty(assetKey)) return null;
            // assetKey 가 이미 prefix 를 가진 경우 (e.g. "path:Assets/Arts/Models/..." or "asset:1234") 그대로.
            if (assetKey.StartsWith("path:", StringComparison.Ordinal) ||
                assetKey.StartsWith("asset:", StringComparison.Ordinal) ||
                assetKey.StartsWith("inv:", StringComparison.Ordinal) ||
                assetKey.StartsWith("name:", StringComparison.Ordinal))
            {
                return assetKey;
            }
            // bare path 면 path: prefix 부착, 그 외엔 asset: 로 wrapping.
            if (assetKey.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return "path:" + assetKey;
            }
            return "asset:" + assetKey;
        }

        // 빈티지 자산 bounds 기반 스케일 보정. GLB 원본 크기가 자산마다 제각각이라 spec.LocalScale
        // 숫자만으론 화면 크기를 보장 못 함 — 기준 책장 Renderer bounds 와 비교해 uniform factor 로
        // 보정해 같은 assetKey 면 theme spawn / inventory placement / restore 어디서든 동일한
        // 최종 크기가 나오도록 강제.
        //
        // 호출 전제: transform.localScale 가 spec/preset/saved 값으로 이미 셋업된 상태.
        // 호출 후: localScale *= uniformFactor (factor 는 보정 정책에 따라 산출).
        //
        // 보정 정책 (uniform scale, 비율 보존):
        //   vintage_shelf       → 기준 책장 height 1:1 매칭
        //   vintage_table       → 기준 책장 height 의 40%
        //   vintage_table_mini  → vintage_table 의 65% = 기준 책장 height 의 26%
        //   vintage_frame       → preset 그대로 (사용자 명시 — 0.3 raw localScale 유지)
        //   vintage_lamp / vintage_rug → preset 그대로 (사용자 명시 — 각 0.7 / 1.0 raw localScale)
        //   non-vintage assetKey → no-op
        //
        // reference shelf 못 찾거나 bounds 측정 실패 시 preset 그대로 유지 + warning.
        private void ApplyVintageScaleNormalization(GameObject go, string assetKey, Vector3 presetScaleHint, string source)
        {
            if (go == null || string.IsNullOrEmpty(assetKey)) return;
            if (assetKey.IndexOf("vintage_", System.StringComparison.OrdinalIgnoreCase) < 0) return;

            var preBounds = ComputeRendererBounds(go);
            if (!preBounds.HasValue) return;
            if (!TryGetReferenceShelfBounds(out var refBounds)) return;

            float factor;
            // 매칭 우선순위: table_mini 가 vintage_table 의 substring 이라 먼저 검사.
            if (assetKey.IndexOf("vintage_shelf", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                factor = refBounds.size.y / Mathf.Max(preBounds.Value.size.y, 1e-4f);
            }
            else if (assetKey.IndexOf("vintage_table_mini", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                factor = (refBounds.size.y * 0.26f) / Mathf.Max(preBounds.Value.size.y, 1e-4f);
            }
            else if (assetKey.IndexOf("vintage_table", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                factor = (refBounds.size.y * 0.4f) / Mathf.Max(preBounds.Value.size.y, 1e-4f);
            }
            else
            {
                // lamp / rug / frame — preset 그대로.
                return;
            }

            if (float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 0f) return;
            var safeFactor = Mathf.Clamp(factor, 0.01f, 100f);
            go.transform.localScale *= safeFactor;
            Physics.SyncTransforms();
        }

        // 기준 책장 bounds. 정책: deterministic + 작은 fallback chain — 절대 room walls 등 다른 객체에
        // 잡혀선 안 됨 (이전 회귀: fallback 으로 BookshelfGenerator transform 전체를 스캔 →
        // RoomRoot 자식 (Floor/LeftWall 등) 까지 union 되어 refBoundsSize=(5, 2.98, 5) 결과
        // → vintage_shelf 가 방 높이 만큼 거대해짐).
        //
        // 우선순위:
        //   1. BookshelfGenerator.CanonicalShellSize (Inspector 의 width/height/depth 직접 사용) —
        //      bookshelf shell 이 아직 빌드 안 됐어도 (bookshelfCount=0) deterministic.
        //   2. BookshelfRoot 자식 Renderer.bounds union (1차에서 BookshelfGenerator 인스턴스 못 찾은
        //      경우 fallback).
        // 둘 다 실패하면 false → 호출자가 preset scale 유지.
        private bool TryGetReferenceShelfBounds(out Bounds bounds)
        {
            bounds = default;

            // 1차 — BookshelfGenerator 의 캐노니컬 치수 (Inspector serialized field). bookshelf 가
            // 아직 생성 안 됐거나 빈 상태여도 항상 valid. center 는 origin (size 만 사용).
            var generator = FindFirstObjectByType<BookshelfGenerator>(FindObjectsInactive.Include);
            if (generator != null)
            {
                var size = generator.CanonicalShellSize;
                if (size.x > 0f && size.y > 0f && size.z > 0f)
                {
                    bounds = new Bounds(Vector3.zero, size);
                    return true;
                }
            }

            // 2차 fallback — BookshelfRoot 자식 실측 (BookshelfGenerator 못 찾는 케이스 — 보통 발생 X).
            if (generator != null)
            {
                var t = generator.transform.Find("BookshelfTurntableRoot/BookshelfRoot")
                     ?? generator.transform.Find("BookshelfRoot");
                if (t != null && TryCollectShelfBoundsFromHierarchy(t, out var b))
                {
                    bounds = b;
                    return true;
                }
            }

            return false;
        }

        // BookshelfRoot 또는 roomRoot 하위에서 책장 mesh 의 union Renderer bounds 산출.
        // BooksRoot 자식 (개별 책 mesh) 은 제외 — 책장 frame/shelf board 만 reference 로 잡아야
        // vintage_shelf 의 frame 크기가 동일 기준으로 비교됨.
        private static bool TryCollectShelfBoundsFromHierarchy(Transform root, out Bounds bounds)
        {
            bounds = default;
            if (root == null) return false;

            Bounds? union = null;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (r.gameObject == null || !r.gameObject.activeInHierarchy) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                // BooksRoot 하위 (개별 책 mesh) 는 reference 에서 제외.
                if (IsUnderNamedAncestor(r.transform, "BooksRoot")) continue;
                var rb = r.bounds;
                if (rb.size.sqrMagnitude < 1e-8f) continue;
                if (union == null) union = rb;
                else { var b = union.Value; b.Encapsulate(rb); union = b; }
            }
            if (union == null) return false;
            bounds = union.Value;
            return true;
        }

        private static bool IsUnderNamedAncestor(Transform t, string ancestorName)
        {
            var cur = t;
            while (cur != null)
            {
                if (cur.gameObject.name == ancestorName) return true;
                cur = cur.parent;
            }
            return false;
        }

        // Floor 자산 바닥 contact — bounds.min.y = 0 이 되도록 transform.position.y 보정.
        // Wall 자산엔 호출 X (wall surface anchor 가 별도로 Y 결정).
        private void SnapBottomToFloor(GameObject go)
        {
            var bounds = ComputeRendererBounds(go);
            if (!bounds.HasValue) return;
            var pos = go.transform.position;
            pos.y -= bounds.Value.min.y;
            go.transform.position = pos;
        }

        // spawn 직후 호출 — DecorationItemMarker (InventoryKey 포함) + ThemeSlotMarker 합성 부착.
        // assetKey 있으면 InventoryKey 도 동기화해 저장/배치됨 표시까지 통합.
        private void AttachThemeMarkers(GameObject go, RoomThemeDecoSpec spec, long themeId)
        {
            if (go == null || spec == null) return;
            var deco = go.GetComponent<DecorationItemMarker>();
            if (deco == null) deco = go.AddComponent<DecorationItemMarker>();
            if (!string.IsNullOrEmpty(spec.AssetKey))
            {
                deco.InventoryKey = BuildInventoryKeyFromAssetKey(spec.AssetKey);
                // 동일 InventoryKey 중복 spawn 방지 등록.
                if (!string.IsNullOrEmpty(deco.InventoryKey))
                {
                    placedByInventoryKey[deco.InventoryKey] = go;
                }
            }

            var slot = go.GetComponent<ThemeSlotMarker>();
            if (slot == null) slot = go.AddComponent<ThemeSlotMarker>();
            slot.ThemeId = themeId;
            slot.ThemeSlot = spec.ThemeSlot;
            slot.AssetKey = spec.AssetKey;
        }

        // Theme-prefix isolation pass — incoming theme 과 다른 theme 의 자산을 strip.
        // ClearAllThemeSpawnedDecos 는 ThemeSlotMarker 자식만 destroy. saved-state restore
        // (TryInstantiateSavedItem) / 일부 user placement 는 DecorationItemMarker 만 부착해
        // ThemeSlotMarker 가 없는 자식이 남는다. 이런 자식이 InventoryKey 에 다른 theme 의 prefix
        // (예: "spooky_bed.glb") 를 갖고 있고 incoming 이 vintage 이면 두 테마 자산 혼재 회귀.
        // 본 메서드가 그 격차 보강.
        //
        // Theme-neutral (default_*, Meshy_*, ads_*, doll_*) 는 prefixes 에 없어 매칭 안 됨 → 보존.
        private static readonly string[] ThemeAssetPrefixes =
        {
            "spooky_",
            "vintage_",
            "deepsea_",
            "fantasy_",
            "space_",
            "goodvibes_",
        };

        private static string ResolveThemePrefixFromAsset(App.Infrastructure.Catalogs.RoomThemeAsset theme)
        {
            if (theme == null) return string.Empty;
            string n = theme.name;
            if (string.IsNullOrEmpty(n)) return string.Empty;
            const string roomThemePrefix = "RoomTheme_";
            if (n.StartsWith(roomThemePrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                n = n.Substring(roomThemePrefix.Length);
            }
            return n.ToLowerInvariant() + "_";
        }

        private void RemoveDecosWithMismatchedThemePrefix(App.Infrastructure.Catalogs.RoomThemeAsset theme)
        {
            if (placedItemsRoot == null) return;
            string currentPrefix = ResolveThemePrefixFromAsset(theme);
            int removed = 0;
            int inspected = 0;
            for (int i = placedItemsRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = placedItemsRoot.transform.GetChild(i).gameObject;
                var marker = child.GetComponent<DecorationItemMarker>();
                if (marker == null || string.IsNullOrEmpty(marker.InventoryKey)) continue;
                inspected++;

                string key = marker.InventoryKey;
                const string pathColon = "path:";
                if (key.StartsWith(pathColon, System.StringComparison.Ordinal))
                {
                    key = key.Substring(pathColon.Length);
                }
                int lastSlash = key.LastIndexOfAny(new[] { '/', '\\' });
                string fileName = (lastSlash >= 0 ? key.Substring(lastSlash + 1) : key).ToLowerInvariant();

                string matchedThemePrefix = null;
                for (int p = 0; p < ThemeAssetPrefixes.Length; p++)
                {
                    if (fileName.StartsWith(ThemeAssetPrefixes[p], System.StringComparison.Ordinal))
                    {
                        matchedThemePrefix = ThemeAssetPrefixes[p];
                        break;
                    }
                }
                if (matchedThemePrefix == null) continue;
                if (matchedThemePrefix == currentPrefix) continue;

                if (selectedObject == child) selectedObject = null;
                if (draggingObject == child) draggingObject = null;
                if (placedByInventoryKey.TryGetValue(marker.InventoryKey, out var tracked) && tracked == child)
                {
                    placedByInventoryKey.Remove(marker.InventoryKey);
                }
                if (UnityEngine.Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
                removed++;
            }
            if (removed > 0)
            {
                Debug.Log(
                    $"[RoomEditModeController][ThemePolicy] RemoveDecosWithMismatchedThemePrefix " +
                    $"theme={theme?.name} currentPrefix='{currentPrefix}' inspected={inspected} removed={removed}.");
            }
        }

        // 사용자 요구: 테마 적용 시 그 테마 자산만 보여야 함. placedItemsRoot 자식 중
        // ThemeSlotMarker 부착된 자식 (= 이전 테마들이 spawn 한 deco) 을 모두 destroy.
        // ThemeSlotMarker 없는 자식 (= 사용자 직접 placement) 은 보존.
        private void ClearAllThemeSpawnedDecos()
        {
            if (placedItemsRoot == null) return;
            int removed = 0;
            for (int i = placedItemsRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = placedItemsRoot.transform.GetChild(i).gameObject;
                if (child.GetComponent<ThemeSlotMarker>() == null) continue;
                if (UnityEngine.Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
                removed++;
            }
            if (removed > 0)
            {
                Debug.Log($"[RoomEditModeController] ClearAllThemeSpawnedDecos — destroyed {removed} theme-spawned decos.");
            }
        }

        // 지정 themeId 로 spawn 된 theme deco 만 destroy. 사용자 placement 와 다른 테마가
        // spawn 했던 잔여 deco 는 보존 (사실상 잔여는 RemovePlacementsForIncomingTheme 가
        // 이미 정리했어야 하지만, race 대비 themeId 매칭으로 한정). rollback 경로 전용.
        private void RemoveSpawnedThemeDecos(long themeId)
        {
            if (placedItemsRoot == null) return;
            int removed = 0;
            for (int i = placedItemsRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = placedItemsRoot.transform.GetChild(i).gameObject;
                var slot = child.GetComponent<ThemeSlotMarker>();
                if (slot == null) continue;
                if (slot.ThemeId != themeId) continue;
                if (selectedObject == child) selectedObject = null;
                if (draggingObject == child) draggingObject = null;
                var deco = child.GetComponent<DecorationItemMarker>();
                if (deco != null && !string.IsNullOrEmpty(deco.InventoryKey) &&
                    placedByInventoryKey.TryGetValue(deco.InventoryKey, out var tracked) && tracked == child)
                {
                    placedByInventoryKey.Remove(deco.InventoryKey);
                }
                if (UnityEngine.Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
                removed++;
            }
            Debug.Log($"[RoomEditModeController][ThemePolicy] RemoveSpawnedThemeDecos themeId={themeId} → removed={removed}");
        }

        // 테마 변경 시 placedItemsRoot 자식 모두 destroy — 이전 테마 spawn + 사용자 직접
        // placement 둘 다. 더 이상 표준 테마 적용 경로에선 호출되지 않음 (정책 변경 — 슬롯 매칭만
        // 교체). retain 이유: 외부에서 강제 reset 이 필요한 케이스 (ApplyThemeResetVisualFallback
        // 등) 호환 + 향후 확장 위해.
        private void ClearAllPlacedDecos()
        {
            if (placedItemsRoot == null) return;
            placedByInventoryKey.Clear();
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

        // ApplyThemeSelected 진입 시 호출 — incoming 테마가 차지할 슬롯/자산에 매칭되는
        // placement 만 hidden hold root 로 detach. 매칭 안 되는 사용자 placement 는 placedItemsRoot
        // 에 그대로 남아 보존. 일반 모드에서는 이전 hold 를 버리고 새 PATCH rollback 범위를
        // 시작한다. 편집 모드에서는 저장하지 않고 나가기를 위해 진입 시점 배치를 보존해야 하므로
        // 기존 hold 를 유지하고 추가로 교체 대상이 되는 배치만 같은 hold 에 누적한다.
        private void DetachPlacementForRollback(RoomThemeAsset incomingTheme, bool detachAllPlacements = false)
        {
            if (!IsEditModeActive)
            {
                DiscardPlacementRollbackHold();
            }

            if (placedItemsRoot == null) return;
            if (placedItemsRoot.transform.childCount == 0) return;

            // incoming 테마의 슬롯/자산 집합 — RemovePlacementsForIncomingTheme 와 동일 매칭 키.
            var incomingSlots = new HashSet<ThemeSlot>();
            var incomingAssetKeys = new HashSet<string>(StringComparer.Ordinal);
            if (incomingTheme != null)
            {
                CollectIncomingSlotsAndKeys(incomingTheme.FloorDecoSpecs, incomingSlots, incomingAssetKeys);
                CollectIncomingSlotsAndKeys(incomingTheme.WallDecoSpecs, incomingSlots, incomingAssetKeys);
                CollectIncomingSlotsAndKeys(incomingTheme.BookshelfDecoSpecs, incomingSlots, incomingAssetKeys);
            }

            int detached = 0, preserved = 0;
            for (int i = placedItemsRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = placedItemsRoot.transform.GetChild(i);
                // 매칭 안 됨 → 사용자 placement 로 간주, placedItemsRoot 에 그대로 보존.
                if (!detachAllPlacements && !ShouldReplaceForIncomingTheme(child.gameObject, incomingSlots, incomingAssetKeys))
                {
                    preserved++;
                    continue;
                }

                if (placementRollbackHoldRoot == null)
                {
                    placementRollbackHoldRoot = new GameObject("PlacedDecorations_RollbackHold");
                    var parent = placedItemsRoot.transform.parent;
                    if (parent != null)
                    {
                        placementRollbackHoldRoot.transform.SetParent(parent, false);
                    }
                    placementRollbackHoldRoot.SetActive(false);
                }

                // 선택/drag 상태 정리 — hold 중엔 inactive 라 클릭 불가지만 cached 참조는 끊어둠.
                if (selectedObject == child.gameObject) selectedObject = null;
                if (draggingObject == child.gameObject) draggingObject = null;
                // placedByInventoryKey 도 정리 — hold 중인 자식은 "placed" 가 아니라 인벤토리
                // 슬롯에서 다시 선택 가능해야 함. 매칭 자식이 다시 reattach 되면 RebuildPlacedByInventoryKeyFromScene 가 복원.
                var marker = child.GetComponent<DecorationItemMarker>();
                if (marker != null && !string.IsNullOrEmpty(marker.InventoryKey) &&
                    placedByInventoryKey.TryGetValue(marker.InventoryKey, out var tracked) && tracked == child.gameObject)
                {
                    placedByInventoryKey.Remove(marker.InventoryKey);
                }
                child.SetParent(placementRollbackHoldRoot.transform, true); // worldPositionStays=true
                detached++;
            }
            Debug.Log($"[RoomEditModeController][ThemePolicy] DetachPlacementForRollback '{incomingTheme?.DisplayName}' — detached(replace target)={detached}, preserved(user)={preserved}");
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
        // DecorationItemMarker + collider → 사용자가 클릭해 drag/delete 가능. 테마 변경 시엔
        // RemovePlacementsForIncomingTheme 가 슬롯/자산 매칭만 destroy (사용자 placement 보존).
        //
        // Scale 정책 (사용자 명시 요구): spec.LocalScale 은 raw final localScale.
        // 어떤 정규화/곱셈도 거치지 않고 transform.localScale 에 그대로 박힌다 — 디자이너가
        // Inspector 에서 박은 값과 GameObject.transform.localScale 이 1:1.
        private void SpawnThemeFloorDecoFromSpec(RoomThemeDecoSpec spec, long themeId, ThemeContentTransform transformOverride = null)
        {
            if (spec == null)
            {
                Debug.LogWarning($"[SpawnThemeFloorDecoFromSpec] spec null — themeId={themeId} skip.");
                return;
            }

            var localPosition = transformOverride != null ? transformOverride.LocalPosition : spec.LocalPosition;
            var localRotation = transformOverride != null ? transformOverride.LocalRotation : spec.LocalRotation;
            var localScale = transformOverride != null && transformOverride.HasUsableScale ? transformOverride.LocalScale : spec.LocalScale;
            var prefabName = spec.Prefab != null ? spec.Prefab.name : "null";
            Debug.Log($"[SpawnThemeFloorDecoFromSpec] enter themeId={themeId} assetKey={spec.AssetKey} themeSlot={spec.ThemeSlot} prefab={prefabName} localPosition={localPosition} localScale={localScale} anchorTag={spec.AnchorTag}");

            // misclassification heuristic — spec.ThemeSlot 가 None 인데 assetKey 에 shelf/dashboard
            // 가 들어가면 디자이너가 ThemeSlot 빠뜨린 회귀일 가능성 큼. 일반 placedItemsRoot 로
            // 떨어지면 "skin 안 붙고 방에 떠있음" 회귀가 됨. 정책상 spec.ThemeSlot 가 single source
            // of truth 라 자동 보정은 안 하고 warning 만 — spec 자체를 고치도록 강제.
            if (spec.ThemeSlot == ThemeSlot.None && !string.IsNullOrEmpty(spec.AssetKey))
            {
                if (spec.AssetKey.IndexOf("shelf", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.LogWarning($"[SpawnThemeFloorDecoFromSpec] heuristic mismatch — assetKey '{spec.AssetKey}' 에 'shelf' 가 포함됐는데 ThemeSlot=None. " +
                                     "spec 에 themeSlot=Shelf 로 박지 않으면 일반 floor placement 경로로 떨어져 BookshelfRoot 가 아닌 placedItemsRoot 자식이 됩니다. spec 수정 필요.");
                }
                else if (spec.AssetKey.IndexOf("dashboard", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.LogWarning($"[SpawnThemeFloorDecoFromSpec] heuristic mismatch — assetKey '{spec.AssetKey}' 에 'dashboard' 가 포함됐는데 ThemeSlot=None. themeSlot=Dashboard 로 명시 필요.");
                }
            }

            // ThemeSlot.Shelf 는 일반 floor placement (placedItemsRoot 자식) 가 아니라 functional
            // BookshelfRoot 위 visual skin 으로 적용. placedItemsRoot 에 들어가지 않으므로
            // RemovePlacementsForIncomingTheme 의 slot 매칭 destroy 대상이 아니며, 별도 정리는
            // BookshelfVisualSkinApplier.ClearSkin 으로 수행 (RemovePlacementsForIncomingTheme
            // 가 incoming 슬롯에 Shelf 없으면 호출). 부착 자체가 idempotent — 기존 skin 은
            // TryAttach 내부에서 destroy 후 새 prefab 으로 교체.
            //
            // 일반화: 빈티지 전용 분기 아님. 다른 테마 (modern_shelf, classic_shelf, ...) 도
            // ThemeSlot.Shelf 로 spec 작성하면 동일 경로로 합류.
            //
            // 중요: TryAttach 성공/실패 무관 return — Shelf 슬롯 자산이 일반 placedItemsRoot
            // 으로 fall-through 하지 않도록 한다. 실패 시엔 skin 이 안 붙고 native 책장만 보이는
            // 게 정책상 맞음 (떠있는 일반 deco 로 나타나면 안 됨).
            if (spec.ThemeSlot == ThemeSlot.Shelf)
            {
                Debug.Log($"[SpawnThemeFloorDecoFromSpec] branch=Shelf → BookshelfVisualSkinApplier.TryAttach assetKey={spec.AssetKey}");
                bool attached = BookshelfVisualSkinApplier.TryAttach(spec, themeId, themeAssetLoader, "SpawnThemeFloorDecoFromSpec");
                Debug.Log($"[SpawnThemeFloorDecoFromSpec] branch=Shelf TryAttach result={attached} — 정책상 placedItemsRoot 경로 진입 안 함.");
                return;
            }

            // ThemeSlot.Dashboard — Shelf 와 동일 정책. functional dashboard root (GuestbookBoard
            // /DashboardScreenPanel) 자식 visual skin 으로 attach. placedItemsRoot 에 들어가지
            // 않으므로 RemovePlacementsForIncomingTheme 의 slot 매칭 destroy 대상 아님 — 별도
            // 정리는 DashboardVisualSkinApplier.ClearSkin 으로 수행. DashboardScreenController/
            // UIDocument/Collider/raycast 모두 보존 (skin 의 collider 는 disable).
            if (spec.ThemeSlot == ThemeSlot.Dashboard)
            {
                Debug.Log($"[SpawnThemeFloorDecoFromSpec] branch=Dashboard → DashboardVisualSkinApplier.TryAttach assetKey={spec.AssetKey}");
                bool attached = DashboardVisualSkinApplier.TryAttach(spec, themeId, themeAssetLoader, "SpawnThemeFloorDecoFromSpec");
                Debug.Log($"[SpawnThemeFloorDecoFromSpec] branch=Dashboard TryAttach result={attached} — placedItemsRoot 경로 진입 안 함.");
                return;
            }

            Debug.Log($"[SpawnThemeFloorDecoFromSpec] branch=GeneralFloorPlacement (themeSlot={spec.ThemeSlot}) → placedItemsRoot 자식으로 spawn.");

            var prefab = themeAssetLoader.Resolve(spec);
            if (prefab == null)
            {
                Debug.LogWarning($"[SpawnThemeFloorDecoFromSpec] prefab resolve 실패 — assetKey={spec.AssetKey}. spec.Prefab 가 Inspector 에서 wired 됐는지/fileID 가 정확한지 확인. skip.");
                return;
            }
            EnsurePlacedItemsRoot();

            var go = Instantiate(prefab);
            go.name = $"ThemeDeco_Floor_{SanitizeName(prefab.name)}";
            go.transform.SetParent(placedItemsRoot.transform, true);
            go.transform.position = localPosition;
            go.transform.localEulerAngles = localRotation;
            go.transform.localScale = localScale;

            // 빈티지 자산만 bounds 기반 추가 보정 — GLB 원본 크기 차이를 흡수해 같은 assetKey 면
            // 어디서 spawn 되든 동일 크기 보장. non-vintage 는 spec.LocalScale 그대로.
            ApplyVintageScaleNormalization(go, spec.AssetKey, localScale, "SpawnThemeFloorDecoFromSpec");
            // 바닥 contact — spec.LocalPosition.y == 0 일 때만 자동 snap. 디자이너가 명시적으로
            // y > 0 박으면 (예: lamp 가 책상 위, rug 의 z-fight 0.012 offset) 그 의도 존중.
            if (Mathf.Approximately(localPosition.y, 0f))
            {
                SnapBottomToFloor(go);
            }

            AttachThemeMarkers(go, spec, themeId);
            EnsureCollidersForGameObject(go);
        }

        // 테마 wallDecoSpec → placedItemsRoot 자식 + WallDecoMarker (drag wall surface 동작). spec 의
        // localPosition 은 wall surface anchor frame 기준 — themeApplier.TryGetWallSurfaceAnchor
        // 로 world coord 변환. anchor 못 찾으면 fallback (localPosition 그대로 world).
        private void SpawnThemeWallDecoFromSpec(RoomThemeDecoSpec spec, long themeId, ThemeContentTransform transformOverride = null)
        {
            if (spec == null) return;
            var prefab = themeAssetLoader.Resolve(spec);
            if (prefab == null) return;
            EnsurePlacedItemsRoot();
            if (themeApplier == null) themeApplier = FindFirstObjectByType<RoomThemeApplier>();
            bool useLockedManualTransform = IsTransformLockedWallDecoAssetKey(spec.AssetKey);
            var localPosition = transformOverride != null ? transformOverride.LocalPosition : spec.LocalPosition;
            var localRotation = transformOverride != null ? transformOverride.LocalRotation : spec.LocalRotation;
            var localScale = transformOverride != null && transformOverride.HasUsableScale ? transformOverride.LocalScale : spec.LocalScale;
            var wallSide = ResolveWallSide(spec, transformOverride);

            // wall surface anchor 변환 복구 — spec.LocalPosition 은 wall surface frame 기준
            // local. wall placement 시스템 (drag 매칭 + obstruction) 일관성 유지. 사용자가 캡처한
            // world 좌표를 spec 에 박을 때는 anchor frame 으로 변환 필요 (helper: anchor 의 inverse
            // rotation × (worldPos - anchor.position)).
            Vector3 worldPos;
            Quaternion worldRot;
            if (!useLockedManualTransform &&
                themeApplier != null &&
                themeApplier.TryGetWallSurfaceAnchor(wallSide, out var anchorPos, out var anchorRot))
            {
                worldPos = anchorPos + anchorRot * localPosition;
                worldRot = anchorRot * Quaternion.Euler(localRotation);
            }
            else
            {
                Debug.LogWarning($"[RoomEditModeController] WallDecoSpec WallSide={spec.WallSide} anchor 못 찾음 — localPosition 을 world fallback.");
                if (useLockedManualTransform)
                {
                    worldRot = Quaternion.Euler(localRotation);
                }
                worldPos = localPosition;
                worldRot = Quaternion.Euler(localRotation);
            }

            var go = Instantiate(prefab);
            go.name = $"ThemeDeco_Wall_{SanitizeName(prefab.name)}";
            go.transform.SetParent(placedItemsRoot.transform, true);
            go.transform.SetPositionAndRotation(worldPos, worldRot);
            go.transform.localScale = localScale;

            // 빈티지 frame: 사용자 명시 transform 정확 적용 — anchor 변환 우회하고 spec 값을
            // localPosition/Rotation 그대로 박는다. placedItemsRoot 가 origin 이라 world == local.
            // 다른 wall deco (GoodVibes_NeonSign 등) 의 anchor 정책은 그대로 유지.
            if (IsAnchorBypassedWallDecoAssetKey(spec.AssetKey))
            {
                go.transform.localPosition = localPosition;
                go.transform.localEulerAngles = localRotation;
                go.transform.localScale = localScale;
            }

            // 빈티지 자산 bounds 기반 추가 보정. wall 자산은 SnapBottomToFloor 안 함 — anchor 의
            // wall surface Y 가 이미 액자 적정 높이.
            if (!useLockedManualTransform)
            {
                ApplyVintageScaleNormalization(go, spec.AssetKey, localScale, "SpawnThemeWallDecoFromSpec");
            }
            else
            {
                AttachThemeDecoTransformLock(go, spec, transformOverride);
            }

            var marker = go.AddComponent<WallDecoMarker>();
            marker.CurrentWall = wallSide;
            marker.AllowRotation = false; // 테마 자동 spawn 은 회전 비활성 (사용자가 위치만 옮김)

            AttachThemeMarkers(go, spec, themeId);
            EnsureCollidersForGameObject(go);
        }

        private static bool IsVintageFrameAssetKey(string assetKey)
        {
            return !string.IsNullOrEmpty(assetKey)
                && assetKey.IndexOf("vintage_frame", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // SpawnThemeWallDecoFromSpec 에서 wall surface anchor 변환을 우회하고 spec.LocalPosition/
        // Rotation/Scale 을 placedItemsRoot frame (= world) 의 값으로 직접 박는 자산 화이트리스트.
        //   - vintage_frame: 디자이너가 액자 위치 직접 측정.
        //   - space_*: 우주 테마 wall deco — Inspector 명시값 그대로 노출.
        // 다른 wall deco (GoodVibes_NeonSign 등) 는 매칭 안 되므로 기존 anchor 변환 유지.
        private static bool IsAnchorBypassedWallDecoAssetKey(string assetKey)
        {
            if (string.IsNullOrEmpty(assetKey)) return false;
            if (assetKey.IndexOf("vintage_frame", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            int lastSlash = assetKey.LastIndexOfAny(new[] { '/', '\\' });
            string fileName = lastSlash >= 0 ? assetKey.Substring(lastSlash + 1) : assetKey;
            if (fileName.StartsWith("space_", System.StringComparison.OrdinalIgnoreCase)) return true;
            if (fileName.StartsWith("spooky_window", System.StringComparison.OrdinalIgnoreCase)) return true;
            if (fileName.StartsWith("fantasy_window", System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool IsTransformLockedWallDecoAssetKey(string assetKey)
        {
            if (string.IsNullOrEmpty(assetKey)) return false;
            int lastSlash = assetKey.LastIndexOfAny(new[] { '/', '\\' });
            string fileName = lastSlash >= 0 ? assetKey.Substring(lastSlash + 1) : assetKey;
            return fileName.StartsWith("spooky_window", System.StringComparison.OrdinalIgnoreCase) ||
                   fileName.StartsWith("fantasy_window", System.StringComparison.OrdinalIgnoreCase);
        }

        private static void AttachThemeDecoTransformLock(GameObject go, RoomThemeDecoSpec spec, ThemeContentTransform transformOverride = null)
        {
            if (go == null || spec == null) return;
            var transformLock = go.GetComponent<ThemeDecoTransformLock>();
            if (transformLock == null)
            {
                transformLock = go.AddComponent<ThemeDecoTransformLock>();
            }

            var localPosition = transformOverride != null ? transformOverride.LocalPosition : spec.LocalPosition;
            var localRotation = transformOverride != null ? transformOverride.LocalRotation : spec.LocalRotation;
            var localScale = transformOverride != null && transformOverride.HasUsableScale ? transformOverride.LocalScale : spec.LocalScale;
            transformLock.Configure(spec.AssetKey, localPosition, localRotation, localScale);
        }

        // 메인씬 진입 후 (로그인 → 메인씬, 또는 BookshelfSceneInstaller 이후) saved layout 복원
        // 시점에 BookshelfGenerator 의 책장 mesh bounds 가 아직 valid 하지 않은 경우 대비. ref
        // shelf bounds 가 측정 가능해질 때까지 frame 단위로 대기 후 placedItemsRoot 내 vintage_*
        // 자산들을 일괄 재정규화. 재정규화는 idempotent — 이미 정규화된 자산은 factor≈1.0 로 no-op.
        //
        // 사용자 보고 케이스: 빈티지 적용 → 저장 → 로그아웃 → 로그인 → 메인씬 진입 시 vintage 자산이
        // raw GLB 크기로 표시. 원인 = TryInstantiateSavedItem 의 ApplyVintageScaleNormalization 가
        // ref bookshelf bounds 못 찾아 skip → saved scale (또는 normalize 안 된 값) 그대로 적용.
        private System.Collections.IEnumerator RenormalizeVintagePlacementsWhenBookshelfReady(string source)
        {
            // 최대 ~2초 (120 frame @ 60fps) 대기. BookshelfGenerator.Generate 가 동기적으로 완료해도
            // procedural mesh 의 Renderer.bounds 가 다음 frame 에야 valid 해지는 케이스 보호.
            const int maxFrames = 120;
            int frames = 0;
            while (frames < maxFrames && !TryGetReferenceShelfBounds(out _))
            {
                frames++;
                yield return null;
            }
            if (frames >= maxFrames) yield break;
            RenormalizeAllVintagePlacements(source);

            // ThemeSlot.Shelf 는 BookshelfRoot 자식 (skin) 으로 attach 되므로 BookshelfGenerator
            // 가 ready 안 됐을 때 TryAttach 가 실패해 skin 이 누락된 상태로 남을 수 있다. 여기서
            // 현재 테마의 Shelf spec 을 재시도 — TryAttach 는 idempotent (기존 skin 이 이미 attach
            // 됐다면 destroy 후 재생성, factor 동일이라 시각 변화 없음).
            TryReapplyShelfSkinFromCurrentTheme(source);

            // ThemeSlot.Dashboard 도 동일 — 저장된 layout 복원 시점에 GuestbookBoard/
            // DashboardScreenPanel 이 아직 instantiate 안 됐거나 native renderer.bounds 가
            // valid 하지 않으면 TryAttach 가 skip 된다. 여기서 재시도 — idempotent.
            TryReapplyDashboardSkinFromCurrentTheme(source);
        }

        // 현재 RoomThemeApplier 가 마지막 적용한 테마에 ThemeSlot.Shelf spec 이 있으면 다시
        // BookshelfVisualSkinApplier.TryAttach 호출. race condition (BookshelfGenerator 가 아직
        // 안 생겼을 때 ApplyThemeDefaultEntries 가 먼저 spawn 시도) 복구용. 메인씬 진입 후
        // RenormalizeVintagePlacementsWhenBookshelfReady 가 BookshelfGenerator ready 를 기다린
        // 다음 호출한다.
        private void TryReapplyShelfSkinFromCurrentTheme(string source)
        {
            if (themeApplier == null) themeApplier = FindFirstObjectByType<RoomThemeApplier>();
            var theme = themeApplier != null ? themeApplier.CurrentTheme : null;
            if (theme == null || theme.FloorDecoSpecs == null) return;
            for (int i = 0; i < theme.FloorDecoSpecs.Count; i++)
            {
                var spec = theme.FloorDecoSpecs[i];
                if (spec == null) continue;
                if (spec.ThemeSlot != ThemeSlot.Shelf) continue;
                BookshelfVisualSkinApplier.TryAttach(spec, theme.AssetId, themeAssetLoader, source);
                break; // 한 테마에 Shelf 슬롯은 1개만 (slot 정의상 unique)
            }
        }

        // TryReapplyShelfSkinFromCurrentTheme 의 dashboard 버전. 현재 테마에 ThemeSlot.Dashboard
        // spec 이 있으면 다시 DashboardVisualSkinApplier.TryAttach 호출. 메인씬 진입 후
        // GuestbookBoard 가 ready 되는 시점이 race 인 케이스 복구용. TryAttach 는 idempotent —
        // 이미 같은 marker 가 붙어 있어도 destroy 후 재생성 (factor 동일이라 시각 변화 없음).
        private void TryReapplyDashboardSkinFromCurrentTheme(string source)
        {
            if (themeApplier == null) themeApplier = FindFirstObjectByType<RoomThemeApplier>();
            var theme = themeApplier != null ? themeApplier.CurrentTheme : null;
            if (theme == null || theme.FloorDecoSpecs == null) return;
            for (int i = 0; i < theme.FloorDecoSpecs.Count; i++)
            {
                var spec = theme.FloorDecoSpecs[i];
                if (spec == null) continue;
                if (spec.ThemeSlot != ThemeSlot.Dashboard) continue;
                DashboardVisualSkinApplier.TryAttach(spec, theme.AssetId, themeAssetLoader, source);
                break; // 한 테마에 Dashboard 슬롯은 1개만 (slot 정의상 unique)
            }
        }

        // catalog (RoomThemeCatalog) 에서 assetKey 가 매칭되는 vintage spec 찾기. saved layout 의
        // InventoryKey ("path:Assets/...") 와 spec.AssetKey ("Assets/...") prefix 차이 모두 흡수.
        // 한 자산이 여러 테마에 등장하면 첫 번째 매칭 반환 — vintage 는 RoomTheme_Vintage 에만
        // 있다고 가정 (다른 테마에서 vintage_ asset 재사용하지 않음).
        private bool TryFindVintageSpecByAssetKey(string savedKey, out RoomThemeDecoSpec spec)
        {
            spec = null;
            if (string.IsNullOrEmpty(savedKey)) return false;
            if (themeCatalog == null) themeCatalog = Resources.Load<RoomThemeCatalogAsset>("RoomThemeCatalog");
            if (themeCatalog == null || themeCatalog.Entries == null) return false;

            for (int i = 0; i < themeCatalog.Entries.Count; i++)
            {
                var theme = themeCatalog.Entries[i];
                if (theme == null) continue;
                if (TryFindSpecByAssetKeyMatch(theme.FloorDecoSpecs, savedKey, out spec)) return true;
                if (TryFindSpecByAssetKeyMatch(theme.WallDecoSpecs, savedKey, out spec)) return true;
                if (TryFindSpecByAssetKeyMatch(theme.BookshelfDecoSpecs, savedKey, out spec)) return true;
            }
            return false;
        }

        private static bool TryFindSpecByAssetKeyMatch(IReadOnlyList<RoomThemeDecoSpec> specs, string savedKey, out RoomThemeDecoSpec found)
        {
            found = null;
            if (specs == null) return false;
            for (int i = 0; i < specs.Count; i++)
            {
                var s = specs[i];
                if (s == null || string.IsNullOrEmpty(s.AssetKey)) continue;
                // savedKey ("path:Assets/...") vs spec.AssetKey ("Assets/..."): BuildInventoryKey 결과로
                // 비교해 prefix 일치성 보장.
                var specInvKey = BuildInventoryKeyFromAssetKey(s.AssetKey);
                if (string.Equals(specInvKey, savedKey, System.StringComparison.OrdinalIgnoreCase))
                {
                    found = s;
                    return true;
                }
            }
            return false;
        }

        // placedItemsRoot 내 모든 vintage_* 자산을 ApplyVintageScaleNormalization 으로 재계산.
        // ThemeSlotMarker.AssetKey 우선, 없으면 DecorationItemMarker.InventoryKey 로 fallback.
        // 멀티-call 안전 — 정규화된 자산은 factor≈1.0 라 no-op.
        private int RenormalizeAllVintagePlacements(string source)
        {
            if (placedItemsRoot == null) return 0;
            int normalized = 0;
            for (int i = 0; i < placedItemsRoot.transform.childCount; i++)
            {
                var child = placedItemsRoot.transform.GetChild(i).gameObject;
                if (child == null) continue;
                string assetKey = null;
                var slotMarker = child.GetComponent<ThemeSlotMarker>();
                if (slotMarker != null && !string.IsNullOrEmpty(slotMarker.AssetKey))
                {
                    assetKey = slotMarker.AssetKey;
                }
                else
                {
                    var deco = child.GetComponent<DecorationItemMarker>();
                    if (deco != null && !string.IsNullOrEmpty(deco.InventoryKey))
                    {
                        assetKey = deco.InventoryKey;
                    }
                }
                if (string.IsNullOrEmpty(assetKey)) continue;
                if (assetKey.IndexOf("vintage_", System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                ApplyVintageScaleNormalization(child, assetKey, child.transform.localScale, source);
                normalized++;
            }
            return normalized;
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
            // Remote GLB decorations only need a broad hit target for select/drag.
            // Runtime MeshCollider creation bakes PhysX collision meshes and caused
            // multi-second profiler spikes when a room with many remote assets loaded.
            // root 에 BoxCollider 가 이미 있으면 skip (중복 회피). 없으면 renderer bounds 로 합성.
            // PlaceInventoryEntry 와 동일 좌표 변환 (world → local, 음수 size 방지 Mathf.Abs).
            EnsureRootBoxColliderFromRendererBounds(go, skipWallMaskQuad: true);
        }

        private static void EnsureRootBoxColliderFromRendererBounds(GameObject go, bool skipWallMaskQuad)
        {
            if (go == null || go.GetComponent<BoxCollider>() != null)
            {
                return;
            }

            var rendererBounds = ComputeRendererBounds(go, skipWallMaskQuad ? "WallMaskQuad" : null);
            if (!rendererBounds.HasValue)
            {
                return;
            }

            var box = go.AddComponent<BoxCollider>();
            var localCenter = go.transform.InverseTransformPoint(rendererBounds.Value.center);
            var localSize = go.transform.InverseTransformVector(rendererBounds.Value.size);
            box.center = localCenter;
            box.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        }

        // 카테고리별 visual swap. RoomThemeApplier의 RoomRoot/DirectionalLight reference를
        // 재사용해 GameObject.Find/FindObjectsByType 매 클릭마다 호출 안 하도록.
        // 매칭 패턴은 RoomGameObjectPatterns로 통합(RoomThemeApplier와 동일).
        private void ApplyEntryVisual(CustomizationCategory category, CustomizationEntry entry)
        {
            Debug.Log($"[ApplyEntryVisual] enter category={category} entry.AssetId={(entry != null ? entry.AssetId : -1)} entry.Prefab={(entry != null && entry.Prefab != null ? entry.Prefab.name : "null")} isMeshSwap={IsMeshSwapCategory(category)}");
            if (category == CustomizationCategory.Desk && IsFantasyDeskEntry(entry))
            {
                Debug.Log($"[FantasyDesk] ApplyEntryVisual category={category} assetId={entry.AssetId} prefab={(entry.Prefab != null ? entry.Prefab.name : "null")}");
            }
            WarnMissingDeskPrefab(category, entry, "ApplyEntryVisual");

            // Desk 카테고리는 새 DeskVisualSkinApplier 가 참조하는 fields 가 다양하므로, 실제 Unity 가
            // YAML 에서 deserialize 한 값을 한 줄로 dump. 사용자가 catalog asset 을 외부 편집했는데
            // Unity 가 reimport 안 해서 in-memory SO 가 stale 인 경우 (가장 흔한 회귀 원인) 를 즉시
            // 판별 가능. 값이 catalog YAML 과 다르면 → Project window 에서 DeskCatalog.asset 우클릭 →
            // Reimport 필요. 같으면 → DeskVisualSkinApplier / NotebookPlacement 다른 곳에서 덮어쓰기.
            if (category == CustomizationCategory.Desk && entry != null)
            {
                Debug.Log($"[DeskEntryDump] assetId={entry.AssetId} themeTag={entry.ThemeTag} prefab={(entry.Prefab != null ? entry.Prefab.name : "null")} " +
                          $"placementAnchor={entry.PlacementAnchor} placementRotation={entry.PlacementRotation} placementScale={entry.PlacementScale} " +
                          $"overrideNotebookStation={entry.OverrideNotebookStation} lockNotebookStationToMonitor={entry.LockNotebookStationToMonitor} " +
                          $"notebookStationPosition={entry.NotebookStationPosition} notebookStationRotation={entry.NotebookStationRotation} notebookStationScale={entry.NotebookStationScale} " +
                          $"monitorPrefab={(entry.MonitorPrefab != null ? entry.MonitorPrefab.name : "null")} monitorPosition={entry.MonitorPosition} monitorRotation={entry.MonitorRotation} monitorScale={entry.MonitorScale} " +
                          $"skinScaleMultiplier={entry.SkinScaleMultiplier} preserveSkinNativeScale={entry.PreserveSkinNativeScale} skinLocalPositionOverride={entry.SkinLocalPositionOverride} hideLaptopRenderers={entry.HideLaptopRenderers} " +
                          $"notebookScreenLocalPosition={entry.NotebookScreenLocalPosition} notebookScreenLocalRotation={entry.NotebookScreenLocalRotation} notebookScreenScaleMultiplier={entry.NotebookScreenScaleMultiplier}");
            }
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
            // Wallpaper 카테고리는 wall renderer 의 face 크기 비례 tiling 도 MPB 로 set —
            // RoomThemeApplier 가 set 한 MPB 는 sharedMaterial swap 에 영향받지 않지만,
            // 사용자가 wallpaper 만 변경하는 경로(테마 적용 없이 wallpaper 카테고리 swap) 에서도
            // 정규화가 유지되도록 여기서 한 번 더 set. patternWorldSize 는 themeApplier 의
            // 설정을 그대로 사용 — 단일 source of truth.
            bool isWallpaper = category == CustomizationCategory.Wallpaper;
            Vector2 wallPatternSize = themeApplier != null ? themeApplier.WallPatternWorldSize : new Vector2(1.0f, 2.8f);
            foreach (var renderer in roomRootTransform.GetComponentsInChildren<Renderer>(true))
            {
                if (MatchesCategoryName(category, renderer.gameObject.name))
                {
                    renderer.sharedMaterial = mat;
                    swapped++;
                    if (isWallpaper)
                    {
                        if (RoomGameObjectPatterns.IsCeilingName(renderer.gameObject.name))
                        {
                            WallTilingNormalizer.ApplyHorizontalSurface(renderer, wallPatternSize);
                        }
                        else
                        {
                            WallTilingNormalizer.Apply(renderer, wallPatternSize);
                        }
                    }
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
                || category == CustomizationCategory.DashboardSkin
                || category == CustomizationCategory.Notebook;
        }

        // category 매칭 native renderer 들 disable + prefab 을 첫 매칭 anchor 자식으로 spawn.
        // NotebookStation 같은 자식 transform 은 native 그대로 → 인터랙션 보존.
        // 이전 swap spawn 한 prefab 있으면 destroy 후 새로.
        // entry 단위로 받음 — entry.Prefab + entry.PlacementAnchor/Rotation/Scale + entry.NotebookStationOffset
        // 모두 활용. 사용자가 catalog Inspector 에서 mesh swap 시 fitting (위치/회전/스케일/모니터 위치)
        // 직접 지정 가능.
        private void ApplyBookshelfLayoutForSkinEntry(CustomizationEntry entry, string source)
        {
            if (!TryResolveBookshelfThemeStyle(entry, out var style))
            {
                Debug.LogWarning(
                    $"[RoomEditModeController] Bookshelf skin layout sync skipped: unresolved style " +
                    $"assetId={(entry != null ? entry.AssetId : 0L)} themeTag='{entry?.ThemeTag}' source={source}");
                return;
            }

            var generator = FindFirstObjectByType<BookshelfGenerator>(FindObjectsInactive.Include);
            if (generator == null)
            {
                Debug.LogWarning($"[RoomEditModeController] Bookshelf skin layout sync skipped: BookshelfGenerator missing source={source}");
                return;
            }

            var applied = generator.SetActiveBookshelfDesignStyle(style);
            Debug.Log(
                $"[RoomEditModeController] Bookshelf skin layout sync style={style} applied={applied} " +
                $"assetId={entry.AssetId} themeTag='{entry.ThemeTag}' source={source}");
        }

        private static bool TryResolveBookshelfThemeStyle(CustomizationEntry entry, out BookshelfThemeStyle style)
        {
            style = BookshelfThemeStyle.Default;
            if (entry == null)
            {
                return false;
            }

            switch (entry.AssetId)
            {
                case 200020L:
                    style = BookshelfThemeStyle.Vintage;
                    return true;
                case 300020L:
                    style = BookshelfThemeStyle.DeepSea;
                    return true;
                case 400020L:
                    style = BookshelfThemeStyle.Fantasy;
                    return true;
                case 500020L:
                    style = BookshelfThemeStyle.Space;
                    return true;
                case 600020L:
                    style = BookshelfThemeStyle.Spooky;
                    return true;
            }

            if (TryResolveBookshelfThemeStyleByTag(NormalizeThemeTag(entry.ThemeTag), out style))
            {
                return true;
            }

            var keyword = string.Concat(entry.DisplayName, " ", entry.Description, " ",
                entry.Prefab != null ? entry.Prefab.name : string.Empty);
            return TryResolveBookshelfThemeStyleByTag(NormalizeThemeTag(keyword), out style);
        }

        private static bool TryResolveBookshelfThemeStyleByTag(string tag, out BookshelfThemeStyle style)
        {
            style = BookshelfThemeStyle.Default;
            switch (tag)
            {
                case "Vintage":
                    style = BookshelfThemeStyle.Vintage;
                    return true;
                case "DeepSea":
                    style = BookshelfThemeStyle.DeepSea;
                    return true;
                case "Fantasy":
                    style = BookshelfThemeStyle.Fantasy;
                    return true;
                case "Space":
                    style = BookshelfThemeStyle.Space;
                    return true;
                case "Spooky":
                    style = BookshelfThemeStyle.Spooky;
                    return true;
                case "GoodVibes":
                    style = BookshelfThemeStyle.GoodVibes;
                    return true;
                default:
                    return false;
            }
        }

        private void ApplyCategoryMeshSwap(CustomizationCategory category, CustomizationEntry entry, Transform roomRoot)
        {
            // Desk 만 별도 분기 — DeskVisualSkinApplier 로 delegate. 이전 mesh swap (roomRoot 자식
            // MeshSwap_Desk + NotebookStation transform override) 은 vintage_table 같이 raw 크기가
            // 다른 GLB 에 대해 hardcoded placementAnchor/Scale 로 좌표/크기가 어긋나는 회귀가 있었음
            // (laptop 이 벽 쪽에 떠 있고 책상이 다른 위치). 새 경로는 functional Desk transform
            // 위에 visual overlay 로 부착 + functional bounds 자동 fit + laptop y 만 skin top 으로
            // 재정렬. BookshelfVisualSkin 와 동일 정책.
            //
            // 잔존 책임: Monitor sub-prefab spawn (GoodVibes 등 entry.monitorPrefab 사용) 은
            // ApplyMonitorMeshSwap 가 그대로 처리. 이전 MeshSwap_Desk GameObject (roomRoot 자식)
            // 이 남아 있으면 정리 (legacy 잔존물 cleanup).
            if (category == CustomizationCategory.Desk)
            {
                RestoreCategoryNativeMesh(CustomizationCategory.Desk, roomRoot);

                if (IsFantasyDeskEntry(entry))
                {
                    Debug.Log($"[FantasyDesk] ApplyCategoryMeshSwap category={category} prefab={(entry.Prefab != null ? entry.Prefab.name : "null")} assetId={entry.AssetId}");
                    if (entry.Prefab == null)
                    {
                        Debug.LogWarning($"[FantasyDesk] ApplyCategoryMeshSwap aborted category={category} assetId={entry.AssetId} reason=prefab_null");
                    }
                }
                // legacy MeshSwap_Desk (roomRoot 자식) 잔존물 정리 — 이전 경로 출신
                string legacySwapName = $"MeshSwap_{category}";
                for (int i = roomRoot.childCount - 1; i >= 0; i--)
                {
                    var child = roomRoot.GetChild(i);
                    if (child.gameObject.name == legacySwapName)
                    {
                        if (UnityEngine.Application.isPlaying) Destroy(child.gameObject);
                        else DestroyImmediate(child.gameObject);
                    }
                }

                if (entry.MonitorPrefab != null)
                {
                    Debug.Log($"[MonitorScreen] Apply monitor skin theme={entry.ThemeTag} prefab={entry.MonitorPrefab.name}");
                }

                DeskVisualSkinApplier.TryAttach(entry, roomRoot, "ApplyCategoryMeshSwap");
                if (IsFantasyDeskEntry(entry))
                {
                    LogFantasyDeskHierarchyState("ApplyCategoryMeshSwap.afterTryAttach");
                }
                // theme swap 직후 — 편집 모드 진입 후 desk skin 을 바꾸면 ApplyCanvasPresentation /
                // FadeCanvas 등 부수 효과로 ScreenSaverCanvas / DashboardCanvas 가 SetActive(true)
                // 로 돌아갈 가능성이 있어, suppression 이 active 면 즉시 다시 hide. 비활성이면 no-op.
                // 어떤 테마 (Vintage / DeepSea / GoodVibes 등) 든 편집 모드에서는 노트북 클릭으로
                // FocusNotebook 진입이 안 되도록 visual + click 동시 차단.
                var experienceManagerForReassert = ResolveExperienceManager();
                if (experienceManagerForReassert != null)
                {
                    experienceManagerForReassert.ReassertNotebookScreenSuppressionIfActive($"theme_apply:assetId={entry.AssetId}");
                }
                var monitorTransform = ApplyMonitorMeshSwap(roomRoot, entry);

                // LockNotebookStationToMonitor=true (예: DeepSea + GoodVibes_Monitor 조합) 인 경우
                // NotebookStation 전체 transform 을 monitor 위로 옮긴다. 이렇게 해야 NotebookStation 의
                // 자손인 Notebook/Screen/CameraFocusPoint/ScreenSaverCanvas 가 모두 따라가서 노트북 화면
                // (camera focus + screen saver canvas) 이 monitor 의 디스플레이 패널에 align 됨.
                // DeskVisualSkinApplier 가 이 case 에서 laptop Y 조정을 skip 하므로 native laptop
                // localPosition 그대로인 상태에서 NotebookStation 이 옮겨가 monitor 화면과 일관성 유지.
                // 기존 nativeNotebookStationLocalPos 캡처/복원 로직 (ApplyNotebookStationPlacement 내부 +
                // RestoreNotebookStationPosition) 은 그대로라 테마 전환 시 native 복귀 보장.
                if (ShouldLockNotebookStationToMonitor(entry) && monitorTransform != null)
                {
                    ApplyNotebookStationPlacement(roomRoot, entry, monitorTransform);

                    // monitor 와 NotebookStation 이 모두 자리잡은 후 — Notebook/Screen 을 monitor 디스플레이
                    // 면에 fit. entry.notebookScreen* override 가 있으면 manual 모드 (이미 TryAttach
                    // 에서 적용됨), 없으면 auto-fit (monitor bounds 기반 position/rotation/scale 자동).
                    DeskVisualSkinApplier.AlignNotebookScreenToMonitor(roomRoot, entry, monitorTransform, "ApplyCategoryMeshSwap");

                    // NotebookStation 이 monitor 위로 재배치된 직후 — DeepSea 가 활성이면 ScreenSaverCanvas /
                    // AdImage override 재적용. 비활성 (다른 테마 / DeepSea 가 아직 RegisterActive 안 한 상태)
                    // 면 no-op. AlignNotebookScreenToMonitor 가 이미 RegisterActive 를 호출했지만 이 시점에
                    // 한 번 더 트리거해 station 재배치로 인한 transform 변화에 대비.
                    DeepSeaScreenOverride.ReapplyIfActive("notebook_station_placement");
                }
                return;
            }

            // 1) 이전 mesh swap 된 GameObject 정리 (이름 prefix 로 식별)
            if (category == CustomizationCategory.BookshelfSkin)
            {
                string legacySwapName = $"MeshSwap_{category}";
                for (int i = roomRoot.childCount - 1; i >= 0; i--)
                {
                    var child = roomRoot.GetChild(i);
                    if (child.gameObject.name == legacySwapName)
                    {
                        if (UnityEngine.Application.isPlaying) Destroy(child.gameObject);
                        else DestroyImmediate(child.gameObject);
                    }
                }

                ApplyBookshelfLayoutForSkinEntry(entry, "ApplyCategoryMeshSwap");
                BookshelfVisualSkinApplier.TryAttach(entry, entry.AssetId, "ApplyCategoryMeshSwap");
                return;
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

            if (category == CustomizationCategory.Notebook)
            {
                var notebookGo = Instantiate(entry.Prefab, roomRoot);
                notebookGo.name = swapName;
                if (entry.UsePlacementTransformAsWorld)
                {
                    notebookGo.transform.SetPositionAndRotation(entry.PlacementAnchor, Quaternion.Euler(entry.PlacementRotation));
                }
                else
                {
                    notebookGo.transform.localPosition = entry.PlacementAnchor;
                    notebookGo.transform.localEulerAngles = entry.PlacementRotation;
                }
                notebookGo.transform.localScale = entry.PlacementScale != Vector3.zero ? entry.PlacementScale : Vector3.one;
                DisableAllCollidersOnVisual(notebookGo);
                return;
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
            int nativeRendererHidden = 0;
            int notebookRenderersPreserved = 0;
            var allTransforms = roomRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                var t = allTransforms[i];
                if (!MatchesCategoryName(category, t.gameObject.name)) continue;
                if (anchor == null) anchor = t; // primary 못 찾으면 첫 매칭 fallback
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    if (IsUnderNotebookStation(r.transform, t)) { notebookRenderersPreserved++; continue; }
                    r.enabled = false;
                    nativeRendererHidden++;
                }
            }
            if (anchor != null && !IsSameOrChildOf(anchor, roomRoot))
            {
                SetRenderersEnabled(anchor, false);
            }
            if (anchor == null)
            {
                Debug.LogWarning($"[RoomEditModeController] {category} mesh swap — native anchor 못 찾음, prefab spawn skip.");
                return;
            }
            // 4) prefab spawn — entry.PlacementAnchor/Rotation/Scale 을 world 로 직접 해석.
            // 사용자가 Unity scene 에서 시각으로 잡은 transform 값을 catalog Inspector 에 그대로
            // 입력 가능. native anchor 위치 무시 — 책상별 크기/높이 자유 fit.
            // BookshelfSkin + entry.LockVisualTransform=true 면 world 대신 local 공간 적용 —
            // placementAnchor/Rotation/Scale 이 BookshelfRoot-local 값으로 해석되어 catalog Inspector
            // 에서 직접 튜닝 가능. AssetId 별 하드코딩 override 를 data 로 대체.
            var go = Instantiate(entry.Prefab, roomRoot);
            go.name = swapName;
            if (category == CustomizationCategory.BookshelfSkin && entry.LockVisualTransform)
            {
                go.transform.localPosition = entry.PlacementAnchor;
                go.transform.localEulerAngles = entry.PlacementRotation;
            }
            else
            {
                go.transform.SetPositionAndRotation(entry.PlacementAnchor, Quaternion.Euler(entry.PlacementRotation));
            }
            go.transform.localScale = entry.PlacementScale != Vector3.zero ? entry.PlacementScale : Vector3.one;
            if (category == CustomizationCategory.BookshelfSkin)
            {
                DisableAllCollidersOnVisual(go);
            }

            // 5) Desk 카테고리 — NotebookStation override (모니터 진입 위치/스케일 fit).
            //    OverrideNotebookStation=true 면 entry 의 NotebookStation* 값 적용. false 면 native.
            if (category == CustomizationCategory.Desk)
            {
                // Monitor sub-prefab — 책상의 일부로 함께 spawn (사용자 placement 불가, 고정).
                var monitor = ApplyMonitorMeshSwap(roomRoot, entry);
                ApplyNotebookStationPlacement(roomRoot, entry, monitor);

                Debug.Log($"[DeskMeshSwap] themeId={entry.AssetId} assetKey={(entry.Prefab != null ? entry.Prefab.name : "null")} meshSwapObject={go.name} nativeRendererHidden={nativeRendererHidden} notebookPreserved=true(preservedRenderers={notebookRenderersPreserved}) placementAnchor={entry.PlacementAnchor} placementScale={entry.PlacementScale}");
            }
            else if (category == CustomizationCategory.DashboardSkin)
            {
                ApplyDashboardBoardPlacement(roomRoot, entry);
            }
        }

        private static void DisableAllCollidersOnVisual(GameObject go)
        {
            if (go == null) return;

            var colliders = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
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
            RestoreCategoryNativeMesh(CustomizationCategory.Notebook, roomRoot);
        }

        // Desk entry 의 monitorPrefab spawn — book 책상 mesh swap 의 일부. 별도 GameObject (이름
        // MeshSwap_DeskMonitor) 로 roomRoot 자식. 다음 desk 적용 또는 RestoreCategoryNativeMesh 시
        // 같이 정리.
        //
        // **왜 NotebookStation 자식이 아니라 roomRoot 직속 자식인가** —
        // DeepSea 등 일부 entry 는 OverrideNotebookStation + LockNotebookStationToMonitor 가 true 라
        // NotebookStation transform 이 이 monitor 위치를 anchor 로 따라가야 한다 (ApplyNotebookStationPlacement).
        // monitor 를 NotebookStation 자식으로 두면 "parent 가 child 위치를 따라가는" 순환 의존이 되어
        // Unity transform graph 가 깨진다. 그래서 monitor 를 상위(roomRoot 직속) 에 두고 station 을
        // 거기에 맞춰 옮기는 구조. 발견성 비용은 있지만(같은 "노트북 디스플레이" 도메인이 두 root 로
        // 분산 — NotebookStation 자식 mesh + roomRoot 자식 MeshSwap_DeskMonitor) 이 의도된 구조다.
        // cross-cutting traverse (fade/hide/screenshot 등) 가 새로 필요하면 두 root 모두 잡을 것.
        private Transform ApplyMonitorMeshSwap(Transform roomRoot, CustomizationEntry entry)
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

            if (entry.MonitorPrefab == null) return null;

            var go = Instantiate(entry.MonitorPrefab, roomRoot);
            go.name = monitorSwapName;
            go.transform.SetPositionAndRotation(entry.MonitorPosition, Quaternion.Euler(entry.MonitorRotation));
            go.transform.localScale = entry.MonitorScale != Vector3.zero ? entry.MonitorScale : Vector3.one;
            return go.transform;
        }

        // NotebookStation 의 원래 transform 보존용 — 첫 swap 호출 시 저장, restore 시 복원.
        private Vector3? nativeNotebookStationLocalPos;
        private Quaternion? nativeNotebookStationLocalRot;
        private Vector3? nativeNotebookStationLocalScale;

        private void ApplyNotebookStationPlacement(Transform roomRoot, CustomizationEntry entry, Transform monitorAnchor = null)
        {
            var notebookStation = FindNotebookStation(roomRoot);
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
            var resolvedMonitorAnchor = monitorAnchor != null ? monitorAnchor : FindDeskMonitor(roomRoot);
            if (ShouldLockNotebookStationToMonitor(entry) && resolvedMonitorAnchor != null)
            {
                var worldPosition = resolvedMonitorAnchor.TransformPoint(entry.NotebookStationPosition);
                var worldRotation = resolvedMonitorAnchor.rotation * Quaternion.Euler(entry.NotebookStationRotation);
                notebookStation.SetPositionAndRotation(worldPosition, worldRotation);
                notebookStation.localScale = entry.NotebookStationScale != Vector3.zero ? entry.NotebookStationScale : Vector3.one;
                return;
            }

            if (IsSameOrChildOf(notebookStation, roomRoot))
            {
                notebookStation.localPosition = entry.NotebookStationPosition;
                notebookStation.localRotation = Quaternion.Euler(entry.NotebookStationRotation);
            }
            else
            {
                var dashboardBoard = FindDashboardBoard(roomRoot);
                if (dashboardBoard != null)
                {
                    var worldPosition = dashboardBoard.TransformPoint(entry.NotebookStationPosition);
                    var worldRotation = dashboardBoard.rotation * Quaternion.Euler(entry.NotebookStationRotation);
                    notebookStation.SetPositionAndRotation(worldPosition, worldRotation);
                }
                else
                {
                    notebookStation.SetPositionAndRotation(entry.NotebookStationPosition, Quaternion.Euler(entry.NotebookStationRotation));
                }
            }
            notebookStation.localScale = entry.NotebookStationScale != Vector3.zero ? entry.NotebookStationScale : Vector3.one;

            // bounds — desk mesh swap GameObject (MeshSwap_Desk) + notebookStation 의 world AABB.
            // 디자이너 시각 fit 디버깅용 (notebook 이 desk 위에 자연스럽게 정렬됐는지 수치 확인).
            var meshSwapGo = FindChildByName(roomRoot, $"MeshSwap_{CustomizationCategory.Desk}");
            var deskBounds = meshSwapGo != null ? ComputeRendererBounds(meshSwapGo.gameObject) : null;
            var notebookBounds = ComputeRendererBounds(notebookStation.gameObject);
            Debug.Log($"[NotebookPlacement] notebookStationPosition={notebookStation.localPosition} notebookStationRotation={notebookStation.localEulerAngles} notebookStationScale={notebookStation.localScale} finalNotebookWorldPosition={notebookStation.position} deskBounds={(deskBounds.HasValue ? deskBounds.Value.ToString() : "null")} notebookBounds={(notebookBounds.HasValue ? notebookBounds.Value.ToString() : "null")}");
        }

        private static bool ShouldLockNotebookStationToMonitor(CustomizationEntry entry)
        {
            return entry != null
                && entry.MonitorPrefab != null
                && entry.LockNotebookStationToMonitor;
        }

        private void RestoreNotebookStationPosition(Transform roomRoot)
        {
            if (!nativeNotebookStationLocalPos.HasValue) return;
            var notebookStation = FindNotebookStation(roomRoot);
            if (notebookStation == null) return;
            notebookStation.localPosition = nativeNotebookStationLocalPos.Value;
            notebookStation.localRotation = nativeNotebookStationLocalRot ?? Quaternion.identity;
            notebookStation.localScale = nativeNotebookStationLocalScale ?? Vector3.one;
        }

        private Vector3? nativeDashboardBoardLocalPos;
        private Quaternion? nativeDashboardBoardLocalRot;
        private Vector3? nativeDashboardBoardLocalScale;

        private void ApplyDashboardBoardPlacement(Transform roomRoot, CustomizationEntry entry)
        {
            var dashboardBoard = FindDashboardBoard(roomRoot);
            if (dashboardBoard == null) return;
            if (!nativeDashboardBoardLocalPos.HasValue)
            {
                nativeDashboardBoardLocalPos = dashboardBoard.localPosition;
                nativeDashboardBoardLocalRot = dashboardBoard.localRotation;
                nativeDashboardBoardLocalScale = dashboardBoard.localScale;
            }
            if (!entry.OverrideNotebookStation)
            {
                RestoreDashboardBoardPlacement(roomRoot);
                return;
            }

            dashboardBoard.localPosition = entry.NotebookStationPosition;
            dashboardBoard.localRotation = Quaternion.Euler(entry.NotebookStationRotation);
            dashboardBoard.localScale = entry.NotebookStationScale != Vector3.zero ? entry.NotebookStationScale : Vector3.one;
            SyncDashboardPanelToBoard(dashboardBoard);
            ReapplyActiveDeskOverlay(roomRoot);
        }

        private void RestoreDashboardBoardPlacement(Transform roomRoot)
        {
            if (!nativeDashboardBoardLocalPos.HasValue) return;
            var dashboardBoard = FindDashboardBoard(roomRoot);
            if (dashboardBoard == null) return;
            dashboardBoard.localPosition = nativeDashboardBoardLocalPos.Value;
            dashboardBoard.localRotation = nativeDashboardBoardLocalRot ?? Quaternion.identity;
            dashboardBoard.localScale = nativeDashboardBoardLocalScale ?? Vector3.one;
            SyncDashboardPanelToBoard(dashboardBoard);
        }

        private static void SyncDashboardPanelToBoard(Transform dashboardBoard)
        {
            if (dashboardBoard == null) return;
            var hitArea = FindChildByName(dashboardBoard, "GuestbookHitArea") ?? dashboardBoard;
            var panel = FindSceneTransformByName("DashboardScreenPanel");
            if (panel == null) return;
            DashboardPanelLayout.Mount(panel, hitArea);
        }

        private void ReapplyActiveDeskOverlay(Transform roomRoot)
        {
            if (!lastAppliedEntries.TryGetValue(CustomizationCategory.Desk, out var deskAssetId)) return;
            var deskCatalog = LoadCategoryCatalog(CustomizationCategory.Desk);
            var deskEntry = deskCatalog != null ? deskCatalog.FindByAssetId(deskAssetId) : null;
            if (deskEntry == null) return;
            if (ShouldLockNotebookStationToMonitor(deskEntry))
            {
                ApplyNotebookStationPlacement(roomRoot, deskEntry, FindDeskMonitor(roomRoot));
            }
            DeskVisualSkinApplier.ReapplyMonitorScreenState(roomRoot, deskEntry, "ReapplyActiveDeskOverlay");
        }

        public void ReapplyActiveDeskOverlayAfterFocusTransition(string source)
        {
            if (themeApplier == null || themeApplier.RoomRoot == null) return;
            if (IsVintageTheme(themeApplier.CurrentTheme))
            {
                Debug.Log($"[VintageDesk] ReapplyActiveDeskOverlayAfterFocusTransition skipped source={source} theme={(themeApplier.CurrentTheme != null ? themeApplier.CurrentTheme.DisplayName : "null")}");
                return;
            }
            ReapplyActiveDeskOverlay(themeApplier.RoomRoot);
            Debug.Log($"[MonitorScreen] Reapply screensaver canvas transform after focus return source={source}");
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

        // renderer 의 부모 체인 (matchedTransform 경계까지) 에 NotebookStation 또는 Notebook 이름의
        // transform 이 있는지 검사. 있으면 그 renderer 는 노트북 hierarchy 일부 → mesh swap 시
        // disable 금지. 노트북은 고정 interactive 오브젝트라 모든 카테고리 mesh swap 에서 visual 보존.
        //
        // Notebook 도 같이 검사하는 이유: 실제 hierarchy 가 NotebookStation > Desk > Notebook 이라
        // matchedTransform == Desk 인 경우 walk 가 Desk 에서 멈춰 NotebookStation 에 못 닿는다.
        // laptop 자체 GameObject 이름 ("Notebook") 으로 보호해야 vintage_table/GoodVibes_Desk swap
        // 시 책상 mesh 는 hide 되고 laptop 만 살아남는 의도 충족.
        // (MR #157 review simonjiho #1: hardcoded 문자열 → RoomGameObjectPatterns.* 상수 통일.)
        private static bool IsUnderNotebookStation(Transform renderer, Transform matchedTransform)
        {
            var current = renderer;
            while (current != null)
            {
                if (current.gameObject.name == Dotshelf.Scripts.RoomTheme.RoomGameObjectPatterns.NotebookStation) return true;
                if (current.gameObject.name == Dotshelf.Scripts.RoomTheme.RoomGameObjectPatterns.Notebook) return true;
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
                // Desk: 실제 GameObject 이름은 "Desk" (procedural BookshelfGenerator.BuildNotebookStation
                // line 4191 + prefab NotebookStation.prefab line 2357). 이전엔 "DeskRoot" 였으나 어떤
                // 경로에도 그 이름이 없어 FindPrimaryAnchor 가 항상 null 반환 → ApplyCategoryMeshSwap
                // 가 첫 매칭 transform fallback 만 의존. "Desk" 로 바꿔 명시적 anchor 확보.
                case CustomizationCategory.Desk:           primaryName = "Desk"; break;
                case CustomizationCategory.BookshelfSkin:  primaryName = "BookshelfRoot"; break;
                case CustomizationCategory.DashboardSkin:  primaryName = "GuestbookBoard"; break;
                default: return null;
            }

            var anchor = FindChildByName(roomRoot, primaryName);
            if (anchor != null) return anchor;

            if (category == CustomizationCategory.DashboardSkin)
            {
                anchor = FindSceneTransformByName("GuestbookBoard");
                if (anchor != null) return anchor;
                anchor = FindSceneTransformByName("DashboardScreenPanel");
                if (anchor != null) return anchor;
            }

            return FindSceneTransformByName(primaryName);
        }

        private static Transform FindSceneTransformByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return null;

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null) continue;
                if (root.name == name) return root.transform;

                var transforms = root.GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < transforms.Length; j++)
                {
                    var t = transforms[j];
                    if (t != null && t.gameObject.name == name) return t;
                }
            }
            return null;
        }

        private static Transform FindNotebookStation(Transform roomRoot)
        {
            return FindChildByName(roomRoot, "NotebookStation") ?? FindSceneTransformByName("NotebookStation");
        }

        private static Transform FindDeskMonitor(Transform roomRoot)
        {
            return FindChildByName(roomRoot, "MeshSwap_DeskMonitor") ?? FindSceneTransformByName("MeshSwap_DeskMonitor");
        }

        private static Transform FindDashboardBoard(Transform roomRoot)
        {
            return FindChildByName(roomRoot, "GuestbookBoard") ?? FindSceneTransformByName("GuestbookBoard");
        }

        private static bool IsSameOrChildOf(Transform child, Transform root)
        {
            if (child == null || root == null) return false;
            var current = child;
            while (current != null)
            {
                if (current == root) return true;
                current = current.parent;
            }
            return false;
        }

        private static bool IsVintageTheme(RoomThemeAsset theme)
        {
            if (theme == null)
            {
                return false;
            }

            return theme.AssetId == 200031L ||
                   (!string.IsNullOrWhiteSpace(theme.DisplayName) &&
                    theme.DisplayName.IndexOf("vintage", System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (!string.IsNullOrWhiteSpace(theme.name) &&
                    theme.name.IndexOf("vintage", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void SetRenderersEnabled(Transform root, bool enabled)
        {
            if (root == null) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = enabled;
            }
        }

        // mesh swap 복원 — native renderer 들 enable + spawned mesh swap 정리 + Desk 의 경우
        // NotebookStation localPosition + Monitor sub-prefab 도 native 복원.
        private void RestoreCategoryNativeMesh(CustomizationCategory category, Transform roomRoot)
        {
            bool wasDeskMeshSwapPresent = false;
            bool notebookTransformRestored = false;
            if (category == CustomizationCategory.Desk)
            {
                // 새 경로 (DeskVisualSkinApplier) — Desk 자식 ThemeDeskVisual 정리 + hidden
                // native renderer enable + laptop native localPosition 복원. marker 가 attach
                // 시 캡처한 데이터로 처리.
                DeskVisualSkinApplier.ClearSkin(roomRoot, "RestoreCategoryNativeMesh");
                // ClearSkin 직후 — TryAttach 와 같은 이유로 suppression 재확인 (편집 모드 도중 테마
                // 해제 케이스).
                var experienceManagerForReassert = ResolveExperienceManager();
                if (experienceManagerForReassert != null)
                {
                    experienceManagerForReassert.ReassertNotebookScreenSuppressionIfActive("theme_clear");
                }

                // legacy MeshSwap_Desk (roomRoot 자식) 잔존물도 정리 — 이전 경로 출신
                wasDeskMeshSwapPresent = FindChildByName(roomRoot, $"MeshSwap_{CustomizationCategory.Desk}") != null;

                // legacy NotebookStation transform override 복원 — 이전 경로에서 만들어진 capture
                // 가 남아 있으면 native 로 되돌림. 새 경로는 NotebookStation transform 자체를 안
                // 건드리므로 fresh session 에선 noop.
                bool hadOverride = nativeNotebookStationLocalPos.HasValue;
                RestoreNotebookStationPosition(roomRoot);
                notebookTransformRestored = hadOverride;
                SetRenderersEnabled(FindNotebookStation(roomRoot), true);
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
            else if (category == CustomizationCategory.BookshelfSkin)
            {
                BookshelfVisualSkinApplier.ClearSkin("RestoreCategoryNativeMesh");
            }
            else if (category == CustomizationCategory.DashboardSkin)
            {
                RestoreDashboardBoardPlacement(roomRoot);
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
            var externalAnchor = FindPrimaryAnchor(roomRoot, category);
            if (externalAnchor != null && !IsSameOrChildOf(externalAnchor, roomRoot))
            {
                SetRenderersEnabled(externalAnchor, true);
            }

            if (category == CustomizationCategory.Desk)
            {
                Debug.Log($"[DeskMeshSwapRestore] themeId=- meshSwapRestored={wasDeskMeshSwapPresent} notebookTransformRestored={notebookTransformRestored}");
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
                case CustomizationCategory.Wallpaper:     return Dotshelf.Scripts.RoomTheme.RoomGameObjectPatterns.IsWallpaperSurfaceName(name);
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
                    ApplyServerSpaceVisuals(space);
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
        public void ApplyServerSpaceVisuals(SpaceResponseDto space)
        {
            var forceAuthoritativeReapply = spaceVisualsRequireAuthoritativeReapply;
            try
            {
                ApplySpaceVisualsToRoom(space, persistState: true, forceAuthoritativeReapply);
                ClearPrePreviewAuthoritativeVisualSnapshot();
            }
            finally
            {
                if (forceAuthoritativeReapply)
                {
                    spaceVisualsRequireAuthoritativeReapply = false;
                }
            }
        }

        public void ApplyServerSpacePreviewVisuals(SpaceResponseDto space)
        {
            if (space == null)
            {
                return;
            }

            var previousServerSpace = lastServerSpace;
            var previousAppliedEntries = new Dictionary<CustomizationCategory, long>(lastAppliedEntries);
            CapturePrePreviewAuthoritativeVisualSnapshot(previousAppliedEntries);

            suppressLastAppliedEntriesPersistenceDepth++;
            isApplyingReadOnlySpacePreviewVisuals = true;
            try
            {
                lastAppliedEntries.Clear();
                ApplySpaceVisualsToRoom(space, persistState: false);
            }
            finally
            {
                isApplyingReadOnlySpacePreviewVisuals = false;
                suppressLastAppliedEntriesPersistenceDepth--;
                lastAppliedEntries.Clear();
                foreach (var pair in previousAppliedEntries)
                {
                    lastAppliedEntries[pair.Key] = pair.Value;
                }

                lastServerSpace = previousServerSpace;
                spaceVisualsRequireAuthoritativeReapply = true;
            }
        }

        public void RestoreAuthoritativeSpaceVisuals()
        {
            if (lastServerSpace == null)
            {
                RestorePrePreviewAuthoritativeVisualSnapshot();
                return;
            }

            ApplyServerSpaceVisuals(lastServerSpace);
        }

        private void CapturePrePreviewAuthoritativeVisualSnapshot(Dictionary<CustomizationCategory, long> appliedEntries)
        {
            if (themeApplier == null)
            {
                themeApplier = FindFirstObjectByType<RoomThemeApplier>();
            }

            prePreviewAuthoritativeTheme = themeApplier != null ? themeApplier.CurrentTheme : null;
            prePreviewAuthoritativeAppliedEntries.Clear();
            if (appliedEntries != null)
            {
                foreach (var pair in appliedEntries)
                {
                    prePreviewAuthoritativeAppliedEntries[pair.Key] = pair.Value;
                }
            }

            hasPrePreviewAuthoritativeVisualSnapshot = true;
        }

        private void RestorePrePreviewAuthoritativeVisualSnapshot()
        {
            if (!hasPrePreviewAuthoritativeVisualSnapshot)
            {
                return;
            }

            suppressLastAppliedEntriesPersistenceDepth++;
            try
            {
                if (themeApplier == null)
                {
                    themeApplier = FindFirstObjectByType<RoomThemeApplier>();
                }

                if (prePreviewAuthoritativeTheme != null && themeApplier != null)
                {
                    if (themeApplier.CurrentTheme != prePreviewAuthoritativeTheme)
                    {
                        themeApplier.Apply(prePreviewAuthoritativeTheme);
                        SyncInventoryThemeFilterForTheme(prePreviewAuthoritativeTheme);
                        RefreshWallVisibilityAfterMaterialMutation("RestoreAuthoritativeSpaceVisuals.prePreviewTheme");
                    }

                    ApplyThemeDefaultEntries(
                        prePreviewAuthoritativeTheme,
                        persistEntries: false,
                        resetExistingPlacements: false,
                        serverContents: null,
                        requestServerContents: false);
                }
                else
                {
                    RestoreAllMeshSwaps();
                }

                lastAppliedEntries.Clear();
                foreach (var pair in prePreviewAuthoritativeAppliedEntries)
                {
                    lastAppliedEntries[pair.Key] = pair.Value;
                    ApplyCatalogEntryVisualByAssetId(pair.Key, pair.Value, "RestoreAuthoritativeSpaceVisuals.prePreviewEntry");
                }

                RebuildInventorySlots();
            }
            finally
            {
                suppressLastAppliedEntriesPersistenceDepth--;
                ClearPrePreviewAuthoritativeVisualSnapshot();
            }
        }

        private void ClearPrePreviewAuthoritativeVisualSnapshot()
        {
            hasPrePreviewAuthoritativeVisualSnapshot = false;
            prePreviewAuthoritativeTheme = null;
            prePreviewAuthoritativeAppliedEntries.Clear();
        }

        private void ApplyCatalogEntryVisualByAssetId(CustomizationCategory category, long assetId, string source)
        {
            if (assetId <= 0L)
            {
                return;
            }

            var catalog = LoadCategoryCatalog(category);
            if (catalog == null || catalog.Entries == null)
            {
                return;
            }

            if (!TryFindCatalogEntryForServerAssetId(category, assetId, catalog, out var entry) || entry == null)
            {
                Debug.LogWarning($"[RoomEditModeController] {source}: {category} assetId={assetId} matched no local catalog entry.");
                return;
            }

            TryRepairFantasyDeskPrefabReference(catalog, entry, source);
            ApplyEntryVisual(category, entry);
        }

        private void ApplySpaceVisualsToRoom(SpaceResponseDto space, bool persistState, bool forceAuthoritativeReapply = false)
        {
            if (space == null) return;
            if (persistState)
            {
                lastServerSpace = space;  // IsCategoryEntryCurrent의 single source of truth 갱신
            }

            // [Review #104 P2] server에 저장된 테마 inventory ID로 RoomThemeApplier.CurrentTheme도
            // 동기화. 동기화 안 하면 이전 세션에서 customization-disabled 테마를 적용해둔 사용자가
            // 다시 입장해도 RoomThemeApplier.CurrentTheme=null이라 IsCurrentThemeCustomizationLocked
            // 가 항상 false → 사전 확인 모달 미발동 → 사용자는 themeReset=true 응답을 받고서야 테마
            // 해제를 인지하게 됨.
            if (themeApplier == null) themeApplier = FindFirstObjectByType<RoomThemeApplier>();

            // Local-preview 테마 보호 (사용자 회귀 보고: Vintage 적용 → 저장 → 메인씬 → 편집모드
            // 재진입 시 바닥/벽지 초기화). Vintage 같은 local-preview 테마는 PATCH skip 이라 server 가
            // 모름 → space.CurrentThemeInventoryId == null → resolvedTheme = Default → Apply(Default) 가
            // 벽/바닥 wipe. 후속 ApplyCategoryById 도 서버 null 이라 Entries[0] fallback.
            //
            // 두 단계 가드:
            //   (a) 현재 세션 내 client 에 local-preview 가 이미 적용 중이면 skip (in-memory 유지)
            //   (b) 풀 로그인 직후 (client.CurrentTheme = null) PlayerPrefs 에 마지막 local-preview
            //       이름이 저장돼 있으면 그 테마를 복원 적용 → wall/floor 자동 복원
            // 사용자가 명시적으로 다른 테마를 클릭하면 그 경로에서 정상 갱신 (PersistLastAppliedLocalPreviewTheme 갱신).
            if (persistState &&
                TryResolveProtectedFantasyLocalPreviewTheme(out var protectedFantasyTheme, out var protectedFantasySource))
            {
                Debug.Log(
                    $"[FantasyDesk] ApplySpaceVisualsToRoom protecting local-preview fantasy source={protectedFantasySource} " +
                    $"serverThemeId={space.CurrentThemeInventoryId?.ToString() ?? "null"} serverDeskId={space.CurrentDeskInventoryId?.ToString() ?? "null"}");
                if (themeApplier != null && themeApplier.CurrentTheme != protectedFantasyTheme)
                {
                    themeApplier.Apply(protectedFantasyTheme);
                    SyncInventoryThemeFilterForTheme(protectedFantasyTheme);
                    RefreshWallVisibilityAfterMaterialMutation("ApplySpaceVisualsToRoom.protectFantasyLocalPreview");
                }
                SyncInventoryThemeFilterForTheme(protectedFantasyTheme);
                ApplyThemeDefaultEntries(protectedFantasyTheme);
                RebuildInventorySlots();
                LogFantasyDeskHierarchyState("ApplySpaceVisualsToRoom.protectFantasyLocalPreview");
                return;
            }

            bool serverHasNoTheme = !space.CurrentThemeInventoryId.HasValue || space.CurrentThemeInventoryId.Value <= 0;
            bool clientHasLocalPreviewTheme = themeApplier != null && IsLocalPreviewTheme(themeApplier.CurrentTheme);
            if (persistState && serverHasNoTheme && clientHasLocalPreviewTheme)
            {
                themeApplier.Apply(themeApplier.CurrentTheme);
                SyncInventoryThemeFilterForTheme(themeApplier.CurrentTheme);
                RefreshWallVisibilityAfterMaterialMutation("ApplySpaceVisualsToRoom.reapplyCurrentLocalPreview");
                ApplyThemeDefaultEntries(themeApplier.CurrentTheme);
                RebuildInventorySlots();
                return;
            }
            if (persistState && serverHasNoTheme && themeApplier != null)
            {
                // (b) 풀 로그인 직후 client.CurrentTheme 은 보통 null/Default. PlayerPrefs 에 마지막
                //     local-preview 이름이 있으면 그 테마로 복원해 wall/floor 가 다음 세션에도 유지.
                var savedTheme = TryFindLastAppliedLocalPreviewTheme();
                if (savedTheme != null && savedTheme != themeApplier.CurrentTheme)
                {
                    themeApplier.Apply(savedTheme);
                    SyncInventoryThemeFilterForTheme(savedTheme);
                    RefreshWallVisibilityAfterMaterialMutation("ApplySpaceVisualsToRoom.restoreLocalPreview");
                    ApplyThemeDefaultEntries(savedTheme);
                    RebuildInventorySlots();
                    return;
                }
            }

            // 테마가 server reload 로 변경됐으면 default entries (mesh swap + 자동 placement deco)
            // 까지 재적용 — 안 그러면 [MR 135 review beshu Finding 2] 처럼 클릭 직후엔 보이던
            // 산뜻 책상/네온/창문/러그/침대/쿠션이 재입장 / ReloadRoomState 후 사라지는 회귀.
            // 단 동일 테마 reload 시엔 placement 보존 (사용자가 옮겼을 가능성). themeApplier.Apply 도
            // 동일 가드.
            bool themeChanged = false;
            RoomThemeAsset resolvedTheme = null;
            if (themeApplier != null && themeCatalog != null)
            {
                if (space.CurrentThemeInventoryId.HasValue && space.CurrentThemeInventoryId.Value > 0)
                {
                    if (TryResolveOwnedAssetIdForInventory(space.CurrentThemeInventoryId.Value, out var currentThemeAssetId))
                    {
                        resolvedTheme = ResolveThemeForServerAssetId(currentThemeAssetId);
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[RoomEditModeController] server theme inventoryId={space.CurrentThemeInventoryId.Value} 를 assetId로 매핑하지 못해 " +
                            "theme visual apply를 보류합니다.");
                    }
                }
                else
                {
                    resolvedTheme = themeCatalog.Resolve(null);
                }

                if (resolvedTheme != null && resolvedTheme != themeApplier.CurrentTheme)
                {
                    themeApplier.Apply(resolvedTheme);
                    SyncInventoryThemeFilterForTheme(resolvedTheme);
                    RefreshWallVisibilityAfterMaterialMutation("ReloadRoomState");
                    themeChanged = true;
                }
                if (resolvedTheme != null)
                {
                    SyncInventoryThemeFilterForTheme(resolvedTheme);
                }
            }

            // 테마 default entries 적용 — themeChanged 시에만. ApplyCategoryById 보다 먼저 호출해서
            // server-saved 카테고리 (있다면) 가 default 위에 덮어쓰도록.
            if (themeChanged && resolvedTheme != null)
            {
                ApplyThemeDefaultEntries(
                    resolvedTheme,
                    persistEntries: persistState,
                    resetExistingPlacements: false,
                    serverContents: null,
                    requestServerContents: persistState);
            }
            else if (resolvedTheme != null)
            {
                ApplyThemeCategoryDefaultsForMissingServerCategories(resolvedTheme, space, persistEntries: persistState);
            }

            ApplyCategoryById(CustomizationCategory.Wallpaper, space.CurrentWallpaperInventoryId, persistState, forceAuthoritativeReapply);
            ApplyCategoryById(CustomizationCategory.Floor, space.CurrentFloorInventoryId, persistState, forceAuthoritativeReapply);
            ApplyCategoryById(CustomizationCategory.Desk, space.CurrentDeskInventoryId, persistState, forceAuthoritativeReapply);
            ApplyCategoryById(CustomizationCategory.DashboardSkin, space.CurrentDashboardInventoryId, persistState, forceAuthoritativeReapply);
            ApplyCategoryById(CustomizationCategory.Notebook, space.CurrentNotebookInventoryId, persistState, forceAuthoritativeReapply);
            ApplyCategoryById(CustomizationCategory.InteriorLight, space.CurrentInteriorLightInventoryId, persistState, forceAuthoritativeReapply);
            ApplyCategoryById(CustomizationCategory.ExteriorLight, space.CurrentExteriorLightInventoryId, persistState, forceAuthoritativeReapply);
            RebuildInventorySlots();
            if (persistState && resolvedTheme != null)
            {
                RequestThemeContentsBackgroundRefresh(resolvedTheme, "ApplySpaceVisualsToRoom");
            }
            if (themeApplier != null && IsFantasyTheme(themeApplier.CurrentTheme))
            {
                Debug.Log(
                    $"[FantasyDesk] ApplySpaceVisualsToRoom.afterServerCategories theme={themeApplier.CurrentTheme.DisplayName} " +
                    $"serverDeskId={space.CurrentDeskInventoryId?.ToString() ?? "null"}");
                LogFantasyDeskHierarchyState("ApplySpaceVisualsToRoom.afterServerCategories");
            }
        }

        private void ReapplyExplicitServerCategoryOverridesIfCurrentTheme(RoomThemeAsset theme)
        {
            if (lastServerSpace == null || theme == null || !IsSpaceCurrentTheme(lastServerSpace, theme))
            {
                return;
            }

            ReapplyExplicitServerCategoryOverride(CustomizationCategory.Wallpaper, lastServerSpace.CurrentWallpaperInventoryId);
            ReapplyExplicitServerCategoryOverride(CustomizationCategory.Floor, lastServerSpace.CurrentFloorInventoryId);
            ReapplyExplicitServerCategoryOverride(CustomizationCategory.Desk, lastServerSpace.CurrentDeskInventoryId);
            ReapplyExplicitServerCategoryOverride(CustomizationCategory.DashboardSkin, lastServerSpace.CurrentDashboardInventoryId);
            ReapplyExplicitServerCategoryOverride(CustomizationCategory.Notebook, lastServerSpace.CurrentNotebookInventoryId);
            ReapplyExplicitServerCategoryOverride(CustomizationCategory.InteriorLight, lastServerSpace.CurrentInteriorLightInventoryId);
            ReapplyExplicitServerCategoryOverride(CustomizationCategory.ExteriorLight, lastServerSpace.CurrentExteriorLightInventoryId);
        }

        private void ReapplyExplicitServerCategoryOverride(CustomizationCategory category, long? inventoryId)
        {
            if (inventoryId.HasValue && inventoryId.Value > 0L)
            {
                ApplyCategoryById(category, inventoryId);
            }
        }

        private bool IsSpaceCurrentTheme(SpaceResponseDto space, RoomThemeAsset theme)
        {
            if (space == null || theme == null || !space.CurrentThemeInventoryId.HasValue || space.CurrentThemeInventoryId.Value <= 0L)
            {
                return false;
            }

            if (!TryResolveOwnedAssetIdForInventory(space.CurrentThemeInventoryId.Value, out var currentThemeAssetId))
            {
                return false;
            }

            if (TryResolveOwnedServerThemeAssetId(theme, out var themeAssetId) && themeAssetId > 0L)
            {
                return currentThemeAssetId == themeAssetId;
            }

            return ResolveThemeForServerAssetId(currentThemeAssetId) == theme;
        }

        private void ApplyCategoryById(
            CustomizationCategory category,
            long? inventoryId,
            bool persistState = true,
            bool forceAuthoritativeReapply = false)
        {
            var catalog = LoadCategoryCatalog(category);
            if (catalog == null || catalog.Entries == null || catalog.Entries.Count == 0) return;
            if (category == CustomizationCategory.Desk &&
                themeApplier != null &&
                IsFantasyTheme(themeApplier.CurrentTheme))
            {
                Debug.Log($"[FantasyDesk] ApplyCategoryById category=Desk inventoryId={inventoryId?.ToString() ?? "null"}");
            }

            CustomizationEntry entry = null;
            var hasExplicitInventory = inventoryId.HasValue && inventoryId.Value > 0;
            if (hasExplicitInventory)
            {
                if (!TryResolveOwnedAssetIdForInventory(inventoryId.Value, out var assetId))
                {
                    if (isApplyingReadOnlySpacePreviewVisuals)
                    {
                        Debug.LogWarning(
                            $"[RoomEditModeController][CategoryRestore] {category}: visited-space inventoryId={inventoryId.Value} " +
                            "was not present in visible manifests; local fallback is blocked for visitor isolation.");
                        ClearReadOnlyPreviewCategoryVisual(category, catalog);
                        return;
                    }

                    var localMockEntry = catalog.FindByAssetId(inventoryId.Value);
                    if (localMockEntry != null && IsLocalPreviewEntry(localMockEntry))
                    {
                        assetId = inventoryId.Value;
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[RoomEditModeController][CategoryRestore] {category}: server inventoryId={inventoryId.Value} 를 assetId로 매핑하지 못해 " +
                            "catalog fallback을 보류합니다.");
                        return;
                    }
                }

                if (!TryFindCatalogEntryForServerAssetId(category, assetId, catalog, out entry))
                {
                    Debug.LogWarning($"[RoomEditModeController][CategoryRestore] {category}: server assetId={assetId} matched no local catalog entry.");
                }
            }

            if (entry == null && isApplyingReadOnlySpacePreviewVisuals && hasExplicitInventory)
            {
                ClearReadOnlyPreviewCategoryVisual(category, catalog);
                return;
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
                if (!forceAuthoritativeReapply && ShouldPreserveMissingServerCategoryVisual(category))
                {
                    Debug.Log($"[RoomEditModeController][CategoryRestore] {category}: server inventoryId 없음 + current theme default 존재 → native restore skip, 현재 visual 유지.");
                    return;
                }

                if (lastAppliedEntries.ContainsKey(category)
                    && themeApplier != null
                    && themeApplier.RoomRoot != null)
                {
                    RestoreCategoryNativeMesh(category, themeApplier.RoomRoot);
                }
                lastAppliedEntries.Remove(category);
                if (persistState)
                {
                    PersistLastAppliedEntries();
                }
                return;
            }
            // (MR #157 후속, 사용자 보고 — 빈티지 wallpaper 가 메인씬 OK → 편집모드 재진입 시
            // 사라지는 회귀) 비-mesh-swap 카테고리에서도 server inventoryId null + 로컬 적용
            // 흔적이 있으면 Entries[0] fallback skip. 빈티지/Natural/Classic 등 local-preview
            // 테마는 PATCH 가 skip 되어 server 의 CurrentWallpaperInventoryId 는 영원히 null —
            // 그 상태에서 catalog.Entries[0] (Cream) fallback 이 in-memory vintage material 을
            // 덮어쓰던 버그. mesh-swap 카테고리는 위쪽 분기 (line 3461-3471) 에서 이미 동일
            // 가드가 있음 — 본 분기는 그 패턴을 wallpaper / floor / lights 에 확장.
            if (entry == null && lastAppliedEntries.ContainsKey(category))
            {
                if (!forceAuthoritativeReapply && ShouldPreserveMissingServerCategoryVisual(category))
                {
                    Debug.Log($"[RoomEditModeController][CategoryRestore] {category}: server inventoryId 없음 + 로컬 적용 흔적 (assetId={lastAppliedEntries[category]}) → Entries[0] fallback skip, 현재 visual 유지.");
                    return;
                }

                lastAppliedEntries.Remove(category);
                if (persistState)
                {
                    PersistLastAppliedEntries();
                }
            }
            if (entry == null && ShouldSkipCatalogFallbackForServerOmittedCategory(category))
            {
                return;
            }
            if (entry == null && catalog.Entries.Count > 0)
            {
                entry = catalog.Entries[0];
            }
            if (entry == null) return;
            TryRepairFantasyDeskPrefabReference(catalog, entry, "ApplyCategoryById");
            WarnMissingDeskPrefab(category, entry, "ApplyCategoryById.afterRepair");

            // Idempotency: 이미 같은 entry 가 적용되어 있으면 visual 작업 skip.
            // (MR #154 user 보고: 편집 모드 진입 시 ReloadRoomState 가 매번 ApplyCategoryById 호출 →
            //  catalog Entries[0] fallback → ApplyCategoryMeshSwap 가 native 메시 destroy/respawn 회귀 fix.)
            //
            // 직접 user 클릭 / ApplyThemeDefaultEntries 같은 다른 호출 경로는 ApplyEntryVisual 을
            // 직접 호출하므로 이 guard 영향 없음.
            if (!forceAuthoritativeReapply &&
                persistState &&
                lastAppliedEntries.TryGetValue(category, out var prevId) &&
                prevId == entry.AssetId)
            {
                if (category == CustomizationCategory.Desk && themeApplier != null && themeApplier.RoomRoot != null)
                {
                    ReapplyActiveDeskOverlay(themeApplier.RoomRoot);
                }
                return;
            }

            ApplyEntryVisual(category, entry);
            if (entry.AssetId > 0) lastAppliedEntries[category] = entry.AssetId;
            else lastAppliedEntries.Remove(category);
            if (persistState)
            {
                PersistLastAppliedEntries();
            }
        }

        private void ClearReadOnlyPreviewCategoryVisual(CustomizationCategory category, CustomizationCatalogAsset catalog)
        {
            if (themeApplier == null)
            {
                themeApplier = FindFirstObjectByType<RoomThemeApplier>();
            }

            if (IsMeshSwapCategory(category))
            {
                if (themeApplier != null && themeApplier.RoomRoot != null)
                {
                    RestoreCategoryNativeMesh(category, themeApplier.RoomRoot);
                }
                return;
            }

            var fallbackEntry = catalog != null && catalog.Entries != null && catalog.Entries.Count > 0
                ? catalog.Entries[0]
                : null;
            if (fallbackEntry == null)
            {
                return;
            }

            ApplyEntryVisual(category, fallbackEntry);
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
            if (!string.IsNullOrWhiteSpace(entry.PreviewImageUrl))
            {
                LoadInventoryPreview(entry.PreviewImageUrl, thumb, glyph);
            }

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
            if (TryResolveRemoteCustomizationCategory(capturedEntry, out var remoteCategory))
            {
                button.onClick.AddListener(() => ApplyRemoteCategoryInventoryEntry(remoteCategory, capturedEntry));
            }
            else
            {
                button.onClick.AddListener(() => PlaceInventoryEntry(capturedEntry));
            }

            // 동일 inventoryKey 가 이미 배치되어 있으면 슬롯 자체를 비활성 + "배치됨" 오버레이로 가린다.
            // 삭제하면 RebuildInventorySlots 가 다시 돌면서 자동 복구된다.
            if (IsInventoryEntryPlaced(entry))
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
        private void LoadInventoryPreview(string url, Image thumb, TMP_Text glyph)
        {
            if (string.IsNullOrWhiteSpace(url) || thumb == null)
            {
                return;
            }

            var normalizedUrl = url.Trim();
            if (inventoryPreviewSpriteCache.TryGetValue(normalizedUrl, out var cached) && cached != null)
            {
                ApplyInventoryPreview(thumb, glyph, cached);
                return;
            }

            if (selectedToolbarKey == "ALL" ||
                inventoryPreviewLoadsInFlight.Contains(normalizedUrl) ||
                inventoryPreviewLoadsInFlight.Count >= MaxInventoryPreviewLoadsInFlight)
            {
                return;
            }

            inventoryPreviewLoadsInFlight.Add(normalizedUrl);
            StartCoroutine(LoadInventoryPreviewCoroutine(normalizedUrl, thumb, glyph));
        }

        private IEnumerator LoadInventoryPreviewCoroutine(string url, Image thumb, TMP_Text glyph)
        {
            try
            {
                using (var request = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning($"[RoomEdit][RemoteAsset] preview image load failed: {request.responseCode} {request.error} ({url})");
                        yield break;
                    }

                    var texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(request);
                    if (texture == null)
                    {
                        Debug.LogWarning($"[RoomEdit][RemoteAsset] preview image response had no texture: {url}");
                        yield break;
                    }

                    var sprite = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                    inventoryPreviewSpriteCache[url] = sprite;
                    if (thumb != null)
                    {
                        ApplyInventoryPreview(thumb, glyph, sprite);
                    }
                }
            }
            finally
            {
                inventoryPreviewLoadsInFlight.Remove(url);
            }
        }

        private static void ApplyInventoryPreview(Image thumb, TMP_Text glyph, Sprite sprite)
        {
            if (thumb == null || sprite == null)
            {
                return;
            }

            thumb.sprite = sprite;
            thumb.type = Image.Type.Simple;
            thumb.preserveAspect = true;
            thumb.color = Color.white;
            if (glyph != null)
            {
                glyph.gameObject.SetActive(false);
            }
        }

        private static string BuildInventoryKey(InventoryEntry entry)
        {
            if (entry.RemoteAssetId > 0L)
            {
                return "asset:" + entry.RemoteAssetId;
            }
            if (entry.RemoteInventoryId > 0L)
            {
                return "inv:" + entry.RemoteInventoryId;
            }
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

        private bool IsInventoryEntryPlaced(InventoryEntry entry)
        {
            var primaryKey = BuildInventoryKey(entry);
            if (IsInventoryKeyPlaced(primaryKey))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(entry.SourceAssetPath))
            {
                return IsInventoryKeyPlaced(BuildInventoryKeyFromAssetKey(entry.SourceAssetPath));
            }

            return false;
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

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (var i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    return values[i].Trim();
                }
            }

            return string.Empty;
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

            // 하단 버튼 3종 — 카드 중앙 기준 균일 size / 균일 간격 / 수평 정렬.
            // - 같은 modal 안의 다른 컨트롤 (title / body) 이 모두 anchoredPosition 기반이라 일관성
            //   유지를 위해 HorizontalLayoutGroup 없이 좌표 계산만 명시.
            // - 폭 200 은 가장 긴 라벨 "배치 유지 준비 중" 기준. CreateButton 의 자동 font size
            //   (= size.y * 0.45 = 22.5) + 좌우 margin 10 으로 모든 라벨이 잘림 없이 fit.
            // - 카드 width 700, 행 총 width = 3·200 + 2·14 = 628 → 양옆 36px 여유 (충돌 없음).
            // - btnBottomY 34 는 기존 위치 보존 — 위 body anchor 와 안 겹침 (body 바닥 0.34*330=112, 버튼 top 84).
            // 다른 모달 (CustomizationLockedModal 등) 의 버튼 spec 은 건드리지 않음.
            const float btnHeight = 50f;
            const float btnWidth = 200f;
            const float btnSpacing = 14f;
            const float btnBottomY = 34f;
            const float btnLabelMargin = 10f;
            const float btnSidePos = btnWidth + btnSpacing;

            var resetButton = CreateButton(card.transform, "ResetButton", "전체 초기화",
                PrimaryColor, Color.white, new Vector2(btnWidth, btnHeight), false, BorderColor);
            var resetRect = (RectTransform)resetButton.transform;
            resetRect.anchorMin = new Vector2(0.5f, 0f);
            resetRect.anchorMax = new Vector2(0.5f, 0f);
            resetRect.pivot = new Vector2(0.5f, 0f);
            resetRect.anchoredPosition = new Vector2(-btnSidePos, btnBottomY);
            ApplyButtonLabelHorizontalMargin(resetButton, btnLabelMargin);
            resetButton.onClick.AddListener(() => ConfirmThemeApplyMode(ThemeApplyMode.Reset));

            var keepButton = CreateButton(card.transform, "KeepButton", "배치 유지 준비 중",
                SecondaryColor, MutedTextColor, new Vector2(btnWidth, btnHeight), true, BorderColor);
            var keepRect = (RectTransform)keepButton.transform;
            keepRect.anchorMin = new Vector2(0.5f, 0f);
            keepRect.anchorMax = new Vector2(0.5f, 0f);
            keepRect.pivot = new Vector2(0.5f, 0f);
            keepRect.anchoredPosition = new Vector2(0f, btnBottomY);
            ApplyButtonLabelHorizontalMargin(keepButton, btnLabelMargin);
            keepButton.interactable = false;
            // disabled 시각 강화 — CanvasGroup alpha 로 전체 톤 dim. 크기 / 좌표는 활성 버튼과 동일하게 유지.
            // blocksRaycasts=false 로 클릭 입력도 차단 (interactable=false 와 redundant 하지만 명시).
            var keepCanvasGroup = keepButton.gameObject.AddComponent<CanvasGroup>();
            keepCanvasGroup.alpha = 0.6f;
            keepCanvasGroup.interactable = false;
            keepCanvasGroup.blocksRaycasts = false;

            var cancelButton = CreateButton(card.transform, "CancelButton", "취소",
                CardColor, TextColor, new Vector2(btnWidth, btnHeight), true, BorderColor);
            var cancelRect = (RectTransform)cancelButton.transform;
            cancelRect.anchorMin = new Vector2(0.5f, 0f);
            cancelRect.anchorMax = new Vector2(0.5f, 0f);
            cancelRect.pivot = new Vector2(0.5f, 0f);
            cancelRect.anchoredPosition = new Vector2(btnSidePos, btnBottomY);
            ApplyButtonLabelHorizontalMargin(cancelButton, btnLabelMargin);
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
            var canShowDecorationButton = inMainScene && !step3Active && !IsEditModeActive && !IsReadOnlySpacePreview && !suppressMainDecorationButton;

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
                ExitEditMode();
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
            if (IsReadOnlySpacePreview)
            {
                return;
            }

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
            if (themeApplier != null && IsFantasyTheme(themeApplier.CurrentTheme))
            {
                LogFantasyDeskHierarchyState("EnterEditMode.afterReloadRequested");
            }
            RefreshRemoteAssetInventory();

            // Order matters: canonical-scale existing decorations FIRST so
            // their renderer bounds reflect the final size, THEN fit blocker
            // BoxColliders against those bounds, THEN compute the playable
            // area. The grid display is invalidated so the next ShowGrid
            // rebuilds it against the freshly recomputed bounds.
            NormalizeExistingDecorationScales();
            // 빈티지 자산 safety net — 메인씬 진입 시 deferred renormalize 가 실패했거나 (BookshelfRoot
            // 못 찾음 등) 사용자가 그 사이 직접 편집 모드로 들어온 경우 보호. 편집 모드 진입 시점엔
            // 책장이 확실히 mount 돼 있으므로 ref bounds 측정 가능. idempotent — 정규화된 자산은 no-op.
            RenormalizeAllVintagePlacements("EnterEditMode");
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
                    if (themeApplier != null && IsFantasyTheme(themeApplier.CurrentTheme))
                    {
                        LogFantasyDeskHierarchyState($"EnterEditMode.afterTryRestoreSavedDecorations success={success}");
                    }
                });
            }
            // 진입 시점 marker 스냅샷 — "저장하지 않고 나가기" 롤백 기준점. load 후 캡처 순서가 중요:
            // 사용자가 진입 화면에서 본 상태가 곧 롤백 목표 상태가 되도록.
            TakeEditEntrySnapshot();
            TakeEditVisualSnapshot();

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
                // 노트북 화면 클릭으로 인한 FocusNotebook 자동 진입 차단 — AreSceneInteractionsSuppressed 는
                // raycast-based focus 전환만 막고 ScreenSaverCanvas 의 AdImage Button.onClick 은 그대로
                // fire 됨 (월드 캔버스 GraphicRaycaster 경로). 편집 모드 동안 ScreenSaverCanvas / DashboardCanvas
                // GameObject 를 SetActive(false) 해 visual + 클릭 동시 차단.
                experienceManager.SetNotebookScreenSuppressed(true);
            }

            // DeepSea 활성 시 — 편집 모드 진입 직후 ScreenSaverCanvas / AdImage override 재적용.
            // 비활성 (다른 테마 / DeepSea 미적용) 이면 no-op. 편집 모드 전환 중 FadeCanvas / 카메라 viewport
            // edit 등이 canvas transform 을 흔들었을 수 있어 다시 안정화. 매 프레임 강제 없이 이 hook 한 번만.
            DeepSeaScreenOverride.ReapplyIfActive("edit_enter");
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
            // 나가기는 저장을 수행하지 않는다. 미저장 변경사항이 있으면 편집 진입 시점으로 되돌린다.
            if (isDirty)
            {
                Debug.Log("[RoomEdit][Save] exit clicked while dirty - discarding unsaved edit changes.");
                RestoreEditVisualSnapshot();
                ReattachPlacementFromRollback();
                RollbackToEditEntrySnapshot();
                ClearPendingEditVisualChanges();
                ClearDirty("discard via exit");
                ExitEditModeInternal();
                return;
            }
            Debug.Log("[RoomEdit][Save] exit clicked while clean - leaving edit mode.");
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
            Debug.Log("[RoomEdit][Save] save-and-exit is disabled; exit discards unsaved changes.");
            HideUnsavedChangesModal();
            RestoreEditVisualSnapshot();
            ReattachPlacementFromRollback();
            RollbackToEditEntrySnapshot();
            ClearPendingEditVisualChanges();
            ClearDirty("discard via disabled save-and-exit");
            ExitEditModeInternal();
        }

        private void OnDiscardAndExitClicked()
        {
            Debug.Log("[RoomEdit][Save] 미저장 모달: '저장하지 않고 나가기' 선택 — 변경사항 폐기 + 씬 롤백");
            HideUnsavedChangesModal();
            // 핵심: 저장만 skip 하면 메인뷰에 편집 중 변경사항이 그대로 남는 회귀 — 진입 시점 스냅샷으로
            // 씬 자체를 되돌려야 함. (배치한 객체 destroy / 이동한 객체 transform 복원 / 삭제한 객체 재생성)
            RestoreEditVisualSnapshot();
            ReattachPlacementFromRollback();
            RollbackToEditEntrySnapshot();
            ClearPendingEditVisualChanges();
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
                // 편집 모드 진입 시 hide 한 ScreenSaverCanvas / DashboardCanvas 를 currentState 에 맞춰 복원.
                experienceManager.SetNotebookScreenSuppressed(false);
            }

            // Snapshot 의 GameObject reference 해제 — 다음 진입 시 stale reference 들고 있지 않도록.
            // 다음 EnterEditMode 가 TakeEditEntrySnapshot 으로 다시 캡처하므로 여기서 비워도 안전.
            editEntrySnapshot.Clear();
            ClearPendingEditVisualChanges();
            ClearEditVisualSnapshot();

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

            // DeepSea 활성 시 — 편집 모드 종료 후 메인 복귀 직후 ScreenSaverCanvas / AdImage override 재적용.
            // 편집 중 카메라 viewport edit / wall fade / FadeCanvas 상태 전환 등으로 canvas transform 이
            // 흔들렸을 수 있어 메인 화면 진입과 동시에 안정화. 비활성이면 no-op.
            DeepSeaScreenOverride.ReapplyIfActive("edit_exit");
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
            if (placedItemFadeActive)
            {
                RestoreGuestbookFocusFade();
                placedItemFadeActive = false;
            }
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
            if (wallVisibilityCollected)
            {
                RestoreWallVisibility("OnDisable");
            }
            if (placedItemFadeActive)
            {
                RestoreGuestbookFocusFade();
                placedItemFadeActive = false;
            }
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
            if (IsReadOnlySpacePreview)
            {
                Debug.LogWarning("[RoomEdit][Save] skipped: read-only space preview cannot save layout.");
                onError?.Invoke("read-only space preview");
                return;
            }

            EnsureDecorationUseCases();
            if (saveDecorationsUseCase == null)
            {
                Debug.LogError("[RoomEdit][Save] SaveDecorationsUseCase 생성 실패 — 저장 불가");
                onError?.Invoke("저장 시스템 초기화 실패");
                return;
            }

            CommitPendingEditVisualChanges(
                () => SaveCurrentDecorationLayout(onSuccess, onError),
                onError);
        }

        private void CommitPendingEditVisualChanges(Action onSuccess, Action<string> onError)
        {
            if (!HasPendingEditVisualChanges())
            {
                onSuccess?.Invoke();
                return;
            }

            CommitPendingThemeVisualChange(
                () =>
                {
                    var categorySaves = new List<KeyValuePair<CustomizationCategory, CustomizationEntry>>(pendingCategoryVisualSaves);
                    CommitPendingCategoryVisualChanges(categorySaves, 0, onSuccess, onError);
                },
                onError);
        }

        private void CommitPendingThemeVisualChange(Action onSuccess, Action<string> onError)
        {
            if (!hasPendingThemeVisualSave || pendingThemeVisualSave == null)
            {
                onSuccess?.Invoke();
                return;
            }

            var theme = pendingThemeVisualSave;
            var mode = pendingThemeVisualSaveMode;
            if (IsLocalPreviewTheme(theme) &&
                !TryResolveOwnedInventoryIdForTheme(theme, out _, out _))
            {
                PersistLastAppliedLocalPreviewTheme(theme);
                PersistLastAppliedThemeMode(ThemeModeSingle);
                ClearLastAppliedRandomThemeSelection();
                OptimisticallyUpdateServerTheme(null);
                onSuccess?.Invoke();
                return;
            }

            if (!TryResolveOwnedInventoryIdForTheme(theme, out var inventoryId, out _))
            {
                onError?.Invoke($"theme inventory not found: {theme.DisplayName}");
                return;
            }

            EnsureThemeUseCase();
            if (themeUseCase == null)
            {
                onError?.Invoke("theme save api unavailable");
                return;
            }

            var requestVersion = ++themeRequestVersion;
            themeUseCase.Execute(
                inventoryId,
                mode,
                onSuccess: _ =>
                {
                    if (requestVersion != themeRequestVersion)
                    {
                        return;
                    }

                    PersistLastAppliedLocalPreviewTheme(theme);
                    PersistLastAppliedThemeMode(ThemeModeSingle);
                    ClearLastAppliedRandomThemeSelection();
                    InvalidateThemeContentsCache(theme, "save pending theme");
                    OptimisticallyUpdateServerTheme(inventoryId);
                    onSuccess?.Invoke();
                },
                onError: error =>
                {
                    if (requestVersion != themeRequestVersion)
                    {
                        return;
                    }

                    onError?.Invoke(DashboardErrorMessages.Format(error));
                });
        }

        private void CommitPendingCategoryVisualChanges(
            List<KeyValuePair<CustomizationCategory, CustomizationEntry>> categorySaves,
            int index,
            Action onSuccess,
            Action<string> onError)
        {
            if (categorySaves == null || index >= categorySaves.Count)
            {
                PersistLastAppliedEntries();
                onSuccess?.Invoke();
                return;
            }

            var pair = categorySaves[index];
            CommitPendingCategoryVisualChange(
                pair.Key,
                pair.Value,
                () => CommitPendingCategoryVisualChanges(categorySaves, index + 1, onSuccess, onError),
                onError);
        }

        private void CommitPendingCategoryVisualChange(
            CustomizationCategory category,
            CustomizationEntry entry,
            Action onSuccess,
            Action<string> onError)
        {
            if (entry == null)
            {
                onSuccess?.Invoke();
                return;
            }

            long? inventoryId = null;
            if (entry.AssetId > 0L)
            {
                if (!TryResolveOwnedInventoryIdForEntry(category, entry, out var resolvedInventoryId))
                {
                    if (IsLocalPreviewEntry(entry))
                    {
                        onSuccess?.Invoke();
                        return;
                    }

                    onError?.Invoke($"{category} inventory not found: {entry.DisplayName}");
                    return;
                }

                inventoryId = resolvedInventoryId;
            }

            EnsureSpaceSettingsRepository();
            if (spaceSettingsRepo == null)
            {
                onError?.Invoke("space settings api unavailable");
                return;
            }

            Action<bool> onCategorySuccess = themeReset =>
            {
                if (themeReset)
                {
                    OptimisticallyUpdateServerTheme(null);
                }

                OptimisticallyUpdateServerSpace(category, inventoryId);
                onSuccess?.Invoke();
            };
            Action<DashboardApiError> onCategoryError = error => onError?.Invoke(DashboardErrorMessages.Format(error));

            switch (category)
            {
                case CustomizationCategory.Wallpaper:
                    spaceSettingsRepo.ChangeWallpaper(inventoryId, onCategorySuccess, onCategoryError); break;
                case CustomizationCategory.Floor:
                    spaceSettingsRepo.ChangeFloor(inventoryId, onCategorySuccess, onCategoryError); break;
                case CustomizationCategory.Desk:
                    spaceSettingsRepo.ChangeDesk(inventoryId, onCategorySuccess, onCategoryError); break;
                case CustomizationCategory.DashboardSkin:
                    spaceSettingsRepo.ChangeDashboard(inventoryId, onCategorySuccess, onCategoryError); break;
                case CustomizationCategory.Notebook:
                    spaceSettingsRepo.ChangeNotebook(inventoryId, onCategorySuccess, onCategoryError); break;
                case CustomizationCategory.InteriorLight:
                    spaceSettingsRepo.ChangeInteriorLight(inventoryId, onCategorySuccess, onCategoryError); break;
                case CustomizationCategory.ExteriorLight:
                    spaceSettingsRepo.ChangeExteriorLight(inventoryId, onCategorySuccess, onCategoryError); break;
                default:
                    onSuccess?.Invoke();
                    break;
            }
        }

        private void OptimisticallyUpdateServerTheme(long? inventoryId)
        {
            if (lastServerSpace != null)
            {
                lastServerSpace.CurrentThemeInventoryId = inventoryId;
            }
        }

        private void SaveCurrentDecorationLayout(Action onSuccess, Action<string> onError)
        {
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
                    TrySaveLayoutSnapshotToServer(items);
                    DiscardPlacementRollbackHold();
                    ClearPendingEditVisualChanges();
                    ClearDirty("save success");
                    TryUploadRoomThumbnail();
                    TakeEditEntrySnapshot();
                    TakeEditVisualSnapshot();
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
        private void TrySaveLayoutSnapshotToServer(IReadOnlyList<DecorationLayoutItem> items)
        {
            if (IsReadOnlySpacePreview)
            {
                Debug.Log("[RoomEdit][ServerLayout] skipped: read-only space preview cannot save layout.");
                return;
            }

            var apiManager = APIManager.Instance;
            if (apiManager == null || !apiManager.HasAccessToken)
            {
                Debug.Log("[RoomEdit][ServerLayout] skipped: API token is missing.");
                return;
            }

            NotebookApiClient apiClient;
            try
            {
                apiClient = new NotebookApiClient(apiManager);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RoomEdit][ServerLayout] client init failed: {ex.GetType().Name} {ex.Message}");
                return;
            }

            var request = new LayoutSaveRequestDto
            {
                LayoutPayload = SerializeDecorationLayoutItems(items),
                BaseLayoutVersion = lastServerSpace != null && lastServerSpace.LayoutVersion > 0L ? (long?)lastServerSpace.LayoutVersion : null
            };

            apiClient.SaveMySpaceLayout(
                request,
                response =>
                {
                    if (lastServerSpace != null && response != null)
                    {
                        lastServerSpace.LayoutVersion = response.LayoutVersion;
                    }

                    Debug.Log($"[RoomEdit][ServerLayout] saved layout snapshot v{response?.LayoutVersion ?? 0L}.");
                },
                error =>
                {
                    Debug.LogWarning($"[RoomEdit][ServerLayout] save skipped/failed: {error?.Code}/{error?.Message}");
                });
        }

        private static string SerializeDecorationLayoutItems(IReadOnlyList<DecorationLayoutItem> items)
        {
            return JsonUtility.ToJson(new DecorationLayoutPayload
            {
                items = new List<DecorationLayoutItem>(items ?? Array.Empty<DecorationLayoutItem>())
            });
        }

        private List<DecorationLayoutItem> CollectDecorationLayoutItems(bool scopeToPlacedItemsRoot = false)
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
                if (scopeToPlacedItemsRoot && (placedItemsRoot == null || !m.transform.IsChildOf(placedItemsRoot.transform)))
                {
                    continue;
                }
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
        private void TakeEditVisualSnapshot()
        {
            if (themeApplier == null)
            {
                themeApplier = FindFirstObjectByType<RoomThemeApplier>();
            }

            editEntryThemeSnapshot = themeApplier != null ? themeApplier.CurrentTheme : null;
            editEntryServerSpaceSnapshot = CloneSpaceResponse(lastServerSpace);
            editEntryAppliedEntriesSnapshot.Clear();
            foreach (var pair in lastAppliedEntries)
            {
                editEntryAppliedEntriesSnapshot[pair.Key] = pair.Value;
            }

            pendingCategoryVisualSaves.Clear();
            hasPendingThemeVisualSave = false;
            pendingThemeVisualSave = null;
            pendingThemeVisualSaveMode = ThemeApplyMode.Reset;
        }

        private static SpaceResponseDto CloneSpaceResponse(SpaceResponseDto source)
        {
            if (source == null)
            {
                return null;
            }

            return new SpaceResponseDto
            {
                Id = source.Id,
                OwnerUserId = source.OwnerUserId,
                SpaceName = source.SpaceName,
                VisibilityScope = source.VisibilityScope,
                GuestbookVisibilityScope = source.GuestbookVisibilityScope,
                VisitCount = source.VisitCount,
                LikeCount = source.LikeCount,
                Liked = source.Liked,
                LayoutVersion = source.LayoutVersion,
                ThumbnailUrl = source.ThumbnailUrl,
                CurrentThemeInventoryId = source.CurrentThemeInventoryId,
                CurrentWallpaperInventoryId = source.CurrentWallpaperInventoryId,
                CurrentFloorInventoryId = source.CurrentFloorInventoryId,
                CurrentDeskInventoryId = source.CurrentDeskInventoryId,
                CurrentDashboardInventoryId = source.CurrentDashboardInventoryId,
                CurrentNotebookInventoryId = source.CurrentNotebookInventoryId,
                CurrentInteriorLightInventoryId = source.CurrentInteriorLightInventoryId,
                CurrentExteriorLightInventoryId = source.CurrentExteriorLightInventoryId
            };
        }

        private void RestoreEditVisualSnapshot()
        {
            suppressLastAppliedEntriesPersistenceDepth++;
            try
            {
                if (editEntryServerSpaceSnapshot != null)
                {
                    ApplySpaceVisualsToRoom(editEntryServerSpaceSnapshot, persistState: false, forceAuthoritativeReapply: true);
                }
                else if (editEntryThemeSnapshot != null)
                {
                    if (themeApplier == null)
                    {
                        themeApplier = FindFirstObjectByType<RoomThemeApplier>();
                    }

                    if (themeApplier != null && themeApplier.CurrentTheme != editEntryThemeSnapshot)
                    {
                        themeApplier.Apply(editEntryThemeSnapshot);
                        SyncInventoryThemeFilterForTheme(editEntryThemeSnapshot);
                        RefreshWallVisibilityAfterMaterialMutation("RestoreEditVisualSnapshot");
                    }

                    ApplyThemeDefaultEntries(
                        editEntryThemeSnapshot,
                        persistEntries: false,
                        resetExistingPlacements: false,
                        serverContents: null,
                        requestServerContents: false);
                }
                else
                {
                    RestoreAllMeshSwaps();
                }

                lastAppliedEntries.Clear();
                foreach (var pair in editEntryAppliedEntriesSnapshot)
                {
                    lastAppliedEntries[pair.Key] = pair.Value;
                    ApplyCatalogEntryVisualByAssetId(pair.Key, pair.Value, "RestoreEditVisualSnapshot");
                }

                if (editEntryServerSpaceSnapshot != null)
                {
                    lastServerSpace = CloneSpaceResponse(editEntryServerSpaceSnapshot);
                }

                RebuildInventorySlots();
            }
            finally
            {
                suppressLastAppliedEntriesPersistenceDepth--;
            }
        }

        private bool HasPendingEditVisualChanges()
        {
            return hasPendingThemeVisualSave || pendingCategoryVisualSaves.Count > 0;
        }

        private void ClearPendingEditVisualChanges()
        {
            pendingCategoryVisualSaves.Clear();
            hasPendingThemeVisualSave = false;
            pendingThemeVisualSave = null;
            pendingThemeVisualSaveMode = ThemeApplyMode.Reset;
        }

        private void ClearEditVisualSnapshot()
        {
            editEntryThemeSnapshot = null;
            editEntryServerSpaceSnapshot = null;
            editEntryAppliedEntriesSnapshot.Clear();
        }

        private bool TryFindLocalCatalogEntryByUnityResourceKey(string unityResourceKey, out InventoryEntry entry)
        {
            entry = default;
            if (string.IsNullOrWhiteSpace(unityResourceKey))
            {
                return false;
            }

            var normalizedResourceKey = NormalizeUnityResourceKey(unityResourceKey);
            for (var i = 0; i < inventoryEntries.Count; i++)
            {
                var candidate = inventoryEntries[i];
                if (candidate.IsRemoteAsset || string.IsNullOrWhiteSpace(candidate.SourceAssetPath))
                {
                    continue;
                }

                if (string.Equals(NormalizeUnityResourceKey(candidate.SourceAssetPath), normalizedResourceKey, StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool TryFindInventoryEntryByKey(string key, out InventoryEntry entry)
        {
            if (!string.IsNullOrEmpty(key))
            {
                if (LegacyInventoryKeyAliases.TryGetValue(key, out var aliased))
                {
                    key = aliased;
                }

                if (IsReadOnlySpacePreview)
                {
                    if (TryNormalizeInventoryResourceKey(key, out var visibleResourceKey))
                    {
                        if (visibleLocalAssetEntriesByResourceKey.TryGetValue(visibleResourceKey, out entry))
                        {
                            return true;
                        }

                        // Visitor path keys are the visited owner's asset refs. If we cannot
                        // resolve them through that owner's visible manifests, do not fall
                        // through to this user's local inventory/catalog.
                        entry = default;
                        return false;
                    }

                    if (TryParseInventoryKeyId(key, "inv:", out var visibleInventoryId) &&
                        visibleLocalAssetEntriesByInventoryId.TryGetValue(visibleInventoryId, out entry))
                    {
                        return true;
                    }

                    if (TryParseInventoryKeyId(key, "asset:", out var visibleAssetId) &&
                        visibleLocalAssetEntries.TryGetValue(visibleAssetId, out entry))
                    {
                        return true;
                    }
                    if (visibleAssetId > 0L)
                    {
                        // Visitor layouts must not fall through to this user's inventory.
                        // Return false so the remote-manifest restore path handles asset:{id}.
                        entry = default;
                        return false;
                    }

                    if (TryParseInventoryKeyId(key, "inv:", out visibleInventoryId) &&
                        visibleAssetIdByInventoryId.TryGetValue(visibleInventoryId, out var mappedAssetId) &&
                        visibleLocalAssetEntries.TryGetValue(mappedAssetId, out entry))
                    {
                        return true;
                    }
                    if (visibleInventoryId > 0L)
                    {
                        // inv:{id} belongs to the visited owner's inventory, not the local user.
                        entry = default;
                        return false;
                    }
                }

                for (int i = 0; i < inventoryEntries.Count; i++)
                {
                    if (InventoryEntryMatchesKey(inventoryEntries[i], key))
                    {
                        entry = inventoryEntries[i];
                        return true;
                    }
                }
            }
            entry = default;
            return false;
        }

        private static bool InventoryEntryMatchesKey(InventoryEntry entry, string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            var primaryKey = BuildInventoryKey(entry);
            if (string.Equals(primaryKey, key, StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(entry.SourceAssetPath) &&
                string.Equals(BuildInventoryKeyFromAssetKey(entry.SourceAssetPath), key, StringComparison.Ordinal))
            {
                return true;
            }

            if (TryParseInventoryKeyId(key, "asset:", out var assetId) && entry.RemoteAssetId == assetId)
            {
                return true;
            }

            if (TryParseInventoryKeyId(key, "inv:", out var inventoryId) && entry.RemoteInventoryId == inventoryId)
            {
                return true;
            }

            return false;
        }

        private static bool TryParseInventoryKeyId(string key, string prefix, out long id)
        {
            id = 0L;
            if (string.IsNullOrWhiteSpace(key) || !key.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            return long.TryParse(key.Substring(prefix.Length), out id) && id > 0L;
        }

        private static string NormalizeUnityResourceKey(string key)
        {
            return string.IsNullOrWhiteSpace(key)
                ? string.Empty
                : key.Trim().Replace('\\', '/').TrimStart('/').ToLowerInvariant();
        }

        private static bool TryNormalizeInventoryResourceKey(string key, out string resourceKey)
        {
            resourceKey = string.Empty;
            if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("path:", StringComparison.Ordinal))
            {
                return false;
            }

            resourceKey = NormalizeUnityResourceKey(key.Substring("path:".Length));
            return !string.IsNullOrEmpty(resourceKey);
        }

        private bool TryCreateRemoteInventoryEntryFromKey(string key, out InventoryEntry entry)
        {
            entry = default;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            long assetId = 0L;
            long inventoryId = 0L;
            if (key.StartsWith("asset:", StringComparison.Ordinal))
            {
                var rawAssetId = key.Substring("asset:".Length);
                if (!long.TryParse(rawAssetId, out assetId) || assetId <= 0L)
                {
                    return false;
                }

                if (IsReadOnlySpacePreview && !visibleRemoteAssetManifests.ContainsKey(assetId))
                {
                    return false;
                }
            }
            else if (!IsReadOnlySpacePreview && key.StartsWith("inv:", StringComparison.Ordinal))
            {
                var rawInventoryId = key.Substring("inv:".Length);
                if (!long.TryParse(rawInventoryId, out inventoryId) ||
                    inventoryId <= 0L ||
                    !ownedAssetIdByInventoryId.TryGetValue(inventoryId, out assetId) ||
                    assetId <= 0L)
                {
                    if (!remoteAssetInventoryLoaded && !isRemoteAssetInventoryLoading)
                    {
                        RefreshRemoteAssetInventory();
                    }

                    return false;
                }
            }
            else if (IsReadOnlySpacePreview && key.StartsWith("inv:", StringComparison.Ordinal))
            {
                var rawInventoryId = key.Substring("inv:".Length);
                if (!long.TryParse(rawInventoryId, out inventoryId) ||
                    inventoryId <= 0L ||
                    !visibleAssetIdByInventoryId.TryGetValue(inventoryId, out assetId) ||
                    assetId <= 0L)
                {
                    return false;
                }
            }
            else if (IsReadOnlySpacePreview && TryNormalizeInventoryResourceKey(key, out var resourceKey))
            {
                if (!visibleAssetIdByResourceKey.TryGetValue(resourceKey, out assetId) || assetId <= 0L)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

            visibleRemoteAssetManifests.TryGetValue(assetId, out var manifest);
            ownedAssetsByAssetId.TryGetValue(assetId, out var ownedAsset);
            entry = new InventoryEntry
            {
                DisplayName = FirstNonEmpty(manifest?.Name, manifest?.Code, ownedAsset?.Name, ownedAsset?.Code, "Remote Asset " + assetId),
                Prefab = null,
                SourceAssetPath = string.Empty,
                PlacementCategory = CustomizationCategory.FloorDeco,
                AllowRotation = true,
                Kind = DecorationObjectKind.Decoration,
                ScaleMultiplierOverride = 0f,
                IsRemoteAsset = true,
                RemoteAssetId = assetId,
                RemoteInventoryId = inventoryId,
                PreviewImageUrl = FirstNonEmpty(manifest?.PreviewImageUrl, ownedAsset?.PreviewImageUrl),
                ThemeTag = manifest == null ? ResolveRemoteThemeTag(ownedAsset) : NormalizeThemeTag(manifest.Theme),
                ServerAssetType = FirstNonEmpty(manifest?.AssetType, ownedAsset?.AssetType),
                ServerCategoryCode = ownedAsset?.CategoryCode
            };
            return true;
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
            // 사용자 보고 회귀 fix — 로그아웃/로그인 후 메인씬에 walls/floor 가 default 로 보이는 케이스.
            // local-preview 테마는 server 가 모르니 ReloadRoomState 도 복원 못함. 씬 install 시점에서
            // PlayerPrefs 의 마지막 local-preview 이름 lookup 후 자동 적용. 편집모드 진입 전부터 vintage
            // 벽지/바닥 visible.
            RestoreLastLocalPreviewThemeOnSceneInstall();
            TryRestoreSavedDecorations(onCompleted: success =>
            {
                if (success)
                {
                    hasLoadedSavedDecorationsOnSceneInit = true;
                    // 사용자 보고 회귀 fix — 로그인 → 메인씬 진입 시 vintage 자산이 raw GLB 크기로
                    // 표시되던 케이스. TryInstantiateSavedItem 의 즉시 normalize 가 ref shelf bounds
                    // 측정 실패 (BookshelfGenerator 의 procedural mesh 다음 frame 에 bounds valid)
                    // 로 skip 되는 race. ref bounds 가 valid 해질 때까지 frame 단위 대기 후 일괄
                    // 재정규화. 이미 올바른 자산은 factor≈1.0 라 visual 변화 없음.
                    if (isActiveAndEnabled)
                    {
                        StartCoroutine(RenormalizeVintagePlacementsWhenBookshelfReady("LoadSavedDecorationsAfterSceneInstall"));
                    }
                }
                if (themeApplier != null && IsFantasyTheme(themeApplier.CurrentTheme))
                {
                    LogFantasyDeskHierarchyState($"LoadSavedDecorationsAfterSceneInstall.afterRestore success={success}");
                }
            });
        }

        // 씬 install 시점에 PlayerPrefs 저장된 local-preview 테마 복원. server 가 알 수 없는 테마라
        // ApplySpaceVisualsToRoom 만 의존하면 풀 로그인 후 첫 메인씬에서 wall/floor 가 default 로 보인다.
        // 호출: BookshelfSceneInstaller 끝점 (Generate 직후) → walls/floor renderer 가 이미 존재해 swap 가능.
        //
        // mode 분기: LastAppliedThemeMode 가 "RANDOM" 이고 random selection 직렬화가 valid 하면
        // ApplyExternalRandomThemeSelection 으로 복원. 그 외 ("THEME" / 키 없음 / selection 못 찾음)
        // 기존 단일 테마 복원 흐름 — backward compat 보장.
        private void RestoreLastLocalPreviewThemeOnSceneInstall()
        {
            if (themeApplier == null) themeApplier = FindFirstObjectByType<RoomThemeApplier>();
            if (themeApplier == null) return;
            // 이미 local-preview 가 적용 중이면 (in-session 재진입) skip — 중복 Apply 회피.
            if (themeApplier.CurrentTheme != null && IsLocalPreviewTheme(themeApplier.CurrentTheme))
            {
                return;
            }

            // mode=RANDOM 경로 — selection valid 시 우선 복원.
            var mode = TryFindLastAppliedThemeMode();
            if (string.Equals(mode, ThemeModeRandom, System.StringComparison.Ordinal))
            {
                var savedSelection = TryFindLastAppliedRandomThemeSelection();
                if (savedSelection != null && savedSelection.SelectedSpecs.Count > 0 && savedSelection.BaseTheme != null)
                {
                    Debug.Log($"[RoomEditModeController] SceneInstall.restoreLocalPreview mode=RANDOM — applying random selection ({savedSelection.SelectedSpecs.Count} slots).");
                    ApplyExternalRandomThemeSelection(savedSelection, "SceneInstall.restoreRandom");
                    return;
                }
                Debug.LogWarning(
                    "[RoomEditModeController] SceneInstall.restoreLocalPreview mode=RANDOM 이지만 selection 복원 실패 — 단일 테마 fallback.");
            }

            // mode=THEME (기본) 또는 RANDOM 복원 실패 fallback — 기존 단일 테마 흐름.
            var savedTheme = TryFindLastAppliedLocalPreviewTheme();
            if (savedTheme == null) return;

            themeApplier.Apply(savedTheme);
            SyncInventoryThemeFilterForTheme(savedTheme);
            RefreshWallVisibilityAfterMaterialMutation("SceneInstall.restoreLocalPreview");
            ApplyThemeDefaultEntries(savedTheme);
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
            if (themeApplier != null && IsFantasyTheme(themeApplier.CurrentTheme))
            {
                Debug.Log("[FantasyDesk] TryRestoreSavedDecorations begin — decoration layout restore path does not target Desk/Notebook directly unless they were serialized as placements.");
            }
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
                            // (V4 진단) 테마 spawn 으로 새로 박힌 transform 을 저장된 layout 으로
                            // 덮어쓰는 케이스. ThemeSlotMarker 가 있고 그 슬롯이 현재 테마의 spec
                            // 에 포함되면 저장값 무시하고 spec 값 유지. preset 이 saved 보다 우선.
                            var slotMarker = existing.GetComponent<ThemeSlotMarker>();
                            if (slotMarker != null && slotMarker.ThemeSlot != ThemeSlot.None &&
                                IsSlotInCurrentTheme(slotMarker.ThemeSlot, out var presetSpec))
                            {
                                // saved transform 적용 skip — spec 값 그대로 유지.
                                existing.transform.position = presetSpec.LocalPosition;
                                existing.transform.localEulerAngles = presetSpec.LocalRotation;
                                existing.transform.localScale = presetSpec.LocalScale;
                                if (IsTransformLockedWallDecoAssetKey(slotMarker.AssetKey))
                                {
                                    existing.transform.localPosition = presetSpec.LocalPosition;
                                    existing.transform.localEulerAngles = presetSpec.LocalRotation;
                                    existing.transform.localScale = presetSpec.LocalScale;
                                    AttachThemeDecoTransformLock(existing, presetSpec);
                                }
                                // 빈티지 자산 bounds 기반 재보정 — saved 값이 stale 해도 매 restore 시
                                // 기준 책장 기준 동일 크기로 재계산. wall 자산 + designer 가 명시한
                                // preset y > 0 (lamp/rug) 은 floor snap skip — SpawnThemeFloorDecoFromSpec
                                // 와 동일한 정책.
                                if (!IsTransformLockedWallDecoAssetKey(slotMarker.AssetKey))
                                {
                                    ApplyVintageScaleNormalization(existing, slotMarker.AssetKey, presetSpec.LocalScale, "TryRestoreSavedDecorations.preset-override");
                                }
                                if (existing.GetComponent<WallDecoMarker>() == null && Mathf.Approximately(presetSpec.LocalPosition.y, 0f))
                                {
                                    SnapBottomToFloor(existing);
                                }
                                matched++;
                                continue;
                            }
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
        public void ApplyExternalDecorationLayoutPayload(string layoutPayload, bool clearExisting)
        {
            InitializeIfNeeded();
            EnsurePlacedItemsRoot();

            var restoreVersion = ++externalDecorationLayoutVersion;
            var items = ParseDecorationLayoutItems(layoutPayload);
            InvalidatePendingThemeContentsReapplyRequests("external decoration layout");
            if (clearExisting)
            {
                ClearAllPlacedDecos();
                placedByInventoryKey.Clear();
            }
            else
            {
                RebuildPlacedByInventoryKeyFromScene();
            }
            var restored = RestoreDecorationLayoutItems(items, restoreVersion);
            Debug.Log($"[RoomEdit][ServerLayout] restored {restored} decoration item(s) from server layout.");
        }

        public void InvalidateExternalDecorationLayoutRestores()
        {
            externalDecorationLayoutVersion++;
            InvalidatePendingThemeContentsReapplyRequests("external decoration layout restore invalidated");
        }

        private void InvalidatePendingThemeContentsReapplyRequests(string source)
        {
            if (themeContentsRequestVersions.Count == 0 && themeContentsInFlightVersions.Count == 0)
            {
                return;
            }

            var keys = new List<long>(themeContentsRequestVersions.Keys);
            foreach (var key in themeContentsInFlightVersions.Keys)
            {
                if (!keys.Contains(key))
                {
                    keys.Add(key);
                }
            }

            for (var i = 0; i < keys.Count; i++)
            {
                BumpThemeContentsRequestVersion(keys[i]);
            }

            themeContentsInFlightVersions.Clear();
            Debug.Log($"[RoomEdit][ThemeContents] pending reapply invalidated source={source} count={keys.Count}.");
        }

        private int RestoreDecorationLayoutItems(IReadOnlyList<DecorationLayoutItem> items, int restoreVersion)
        {
            if (items == null || items.Count == 0)
            {
                return 0;
            }

            var restored = 0;
            for (var i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it == null || string.IsNullOrEmpty(it.AssetId))
                {
                    continue;
                }

                if (placedByInventoryKey.TryGetValue(it.AssetId, out var existing) && existing != null)
                {
                    existing.transform.position = new Vector3(it.PositionX, it.PositionY, it.PositionZ);
                    existing.transform.rotation = Quaternion.Euler(0f, it.RotationY, 0f);
                    existing.transform.localScale = new Vector3(it.ScaleX, it.ScaleY, it.ScaleZ);
                    restored++;
                    continue;
                }

                if (TryInstantiateSavedItem(it, restoreVersion))
                {
                    restored++;
                }
            }

            return restored;
        }

        private static List<DecorationLayoutItem> ParseDecorationLayoutItems(string layoutPayload)
        {
            if (string.IsNullOrWhiteSpace(layoutPayload))
            {
                return new List<DecorationLayoutItem>();
            }

            try
            {
                var payload = JsonUtility.FromJson<DecorationLayoutPayload>(layoutPayload);
                return payload?.items ?? new List<DecorationLayoutItem>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RoomEdit][ServerLayout] layout payload parse failed: {ex.GetType().Name} {ex.Message}");
                return new List<DecorationLayoutItem>();
            }
        }

        private bool TryInstantiateSavedItem(DecorationLayoutItem it, int restoreVersion = 0)
        {
            if (it == null || string.IsNullOrEmpty(it.AssetId))
            {
                Debug.LogWarning("[RoomEdit][Load] AssetId 비어있는 item — skip");
                return false;
            }
            if (!TryFindInventoryEntryByKey(it.AssetId, out var entry) &&
                !TryCreateRemoteInventoryEntryFromKey(it.AssetId, out entry))
            {
                Debug.LogWarning(
                    $"[RoomEdit][Load] '{it.AssetId}' prefab 매칭 실패 — 카탈로그 누락 또는 path 변경. 복원 skip.");
                return false;
            }

            if (entry.IsRemoteAsset)
            {
                StartCoroutine(PlaceRemoteInventoryEntry(entry, it.AssetId, it, restoreVersion));
                return true;
            }

            if (entry.Prefab == null)
            {
                Debug.LogWarning(
                    $"[RoomEdit][Load] '{it.AssetId}' prefab matching failed; restore skipped.");
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
            // 빈티지 자산은 saved scale 무시 — catalog 의 spec.LocalScale 을 base 로 reset 후
            // bounds 기반 재계산. 이유: 사용자 보고 회귀 — 로그아웃/로그인 후 메인씬 진입 시 vintage
            // 자산이 raw GLB 크기로 표시되던 케이스 (saved scale 이 이전 세션의 stale 값이거나 ref
            // bounds 측정 실패로 normalize skip). 매 reload 시 동일 reference shelf bounds 기준으로
            // 결과가 결정되므로 saved 값에 무관하게 일관된 visual 보장.
            //
            // position 은 saved 그대로 (사용자의 final placement 존중 — floor snap 강제 X).
            // spec 못 찾으면 saved scale fallback.
            if (!string.IsNullOrEmpty(it.AssetId) && it.AssetId.IndexOf("vintage_", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Vector3 baseScale = TryFindVintageSpecByAssetKey(it.AssetId, out var canonicalSpec)
                    ? canonicalSpec.LocalScale
                    : new Vector3(it.ScaleX, it.ScaleY, it.ScaleZ);
                go.transform.localScale = baseScale;
                ApplyVintageScaleNormalization(go, it.AssetId, baseScale, "TryInstantiateSavedItem");
            }

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

        // 대시보드(GuestbookBoard) focus toggle — true 면 placedItemsRoot 자식 Renderer 들을
        // 반투명 instance material 로 swap, false 면 sharedMaterial 원본으로 원복.
        // wall fade 와 독립적으로 동작하며, 같은 ConfigureMaterialAsTransparent helper 를 공유한다.
        // 호출자: BookshelfExperienceManager.SyncFromCameraMode (focus area = Guestbook 진입/이탈).
        public void SetGuestbookFocusFadeActive(bool active)
        {
            if (active == placedItemFadeActive)
            {
                return;
            }

            if (active)
            {
                // MR #182 skh1205 리뷰 #2: ApplyGuestbookFocusFade 중간에 예외가 던져지면 일부 renderer
                // material instance / enabled / canvas.enabled 상태가 부분 적용된 채 leak 한다. 더구나
                // 여기서 active flag 가 안 set 되면 OnDisable/OnDestroy 의 cleanup 가드 (placedItemFadeActive
                // 체크) 도 안 돌아 leak 영구화. catch 에서 명시적 RestoreGuestbookFocusFade 로 부분 적용분
                // rollback 후 rethrow — 호출자 (BookshelfExperienceManager.ApplyGuestbookFocusRoomFade)
                // 의 swallow 까지 도달.
                try
                {
                    ApplyGuestbookFocusFade();
                }
                catch
                {
                    RestoreGuestbookFocusFade();
                    throw;
                }
                // fade 가 적용된 target 이 하나도 없으면(BookshelfGenerator 미완료 등) active flag 도
                // 마킹 안 해서 다음 호출에서 자연스럽게 retry. 정상 흐름에선 사용자 인터랙션 시점에
                // 이미 생성 완료라 race 가능성은 낮지만 방어적.
                if (placedItemFadeCaches.Count == 0 && canvasFadeCaches.Count == 0)
                {
                    Debug.Log("[GuestbookFocus][PlacedFade] skip-marking-active — no targets faded (placedItemsRoot/NotebookStation 모두 비어 있음).");
                    return;
                }
            }
            else
            {
                RestoreGuestbookFocusFade();
            }
            placedItemFadeActive = active;
        }

        private void ApplyGuestbookFocusFade()
        {
            placedItemFadeCaches.Clear();

            var alpha = Mathf.Clamp01(guestbookFocusFadeAlpha);

            // 대상 1) placedItemsRoot 자식 — 가구/장식.
            // 대상 2) NotebookStation 자식 — 책상(Desk) + 노트북(Notebook) mesh 등.
            //         단 DashboardScreenPanel 은 UIDocument 라 별도 처리(아래) → 자식 Renderer 도 제외.
            var collected = new List<Renderer>();
            int placedItemCount = 0, notebookStationCount = 0;

            if (placedItemsRoot != null)
            {
                var placedRenderers = placedItemsRoot.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < placedRenderers.Length; i++)
                {
                    if (placedRenderers[i] == null) continue;
                    collected.Add(placedRenderers[i]);
                    placedItemCount++;
                }
            }

            // FindNotebookStation(null) 은 내부에서 FindChildByName(null, ...) 를 거쳐 NPE 가 난다 —
            // null-safe 한 scene-wide name 검색을 직접 사용.
            var notebookStation = FindSceneTransformByName("NotebookStation");
            Transform dashboardPanelTransform = FindSceneTransformByName("DashboardScreenPanel");
            if (notebookStation != null)
            {
                var stationRenderers = notebookStation.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < stationRenderers.Length; i++)
                {
                    var r = stationRenderers[i];
                    if (r == null) continue;
                    if (dashboardPanelTransform != null && r.transform.IsChildOf(dashboardPanelTransform))
                    {
                        continue;
                    }
                    collected.Add(r);
                    notebookStationCount++;
                }
            }

            // Desk customization (DeepSea 등) 이 적용되면 노트북 디스플레이가 NotebookStation 자식이
            // 아니라 roomRoot 자식의 별도 prefab(MeshSwap_DeskMonitor) 으로 swap 되어 위 traverse 에
            // 안 잡힌다. 그 prefab 도 fade 대상에 포함.
            var monitorSwap = FindSceneTransformByName("MeshSwap_DeskMonitor");
            int monitorSwapCount = 0;
            if (monitorSwap != null)
            {
                var monitorRenderers = monitorSwap.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < monitorRenderers.Length; i++)
                {
                    var r = monitorRenderers[i];
                    if (r == null) continue;
                    collected.Add(r);
                    monitorSwapCount++;
                }
            }

            int faded = 0, skipped = 0, skippedDisabled = 0, hidden = 0;
            for (int i = 0; i < collected.Count; i++)
            {
                var r = collected[i];
                if (r == null) continue;

                // MR #182 skh1205 리뷰 #1: 원래 비활성 renderer 는 의도된 hidden (테마 swap / LOD
                // variant / 임시 hidden mesh 등). 우리가 fade 진입 시 enabled=true 로 켜면 포커스 중
                // 안 보여야 할 mesh 가 반투명으로 노출되는 회귀. 수집 대상에서 제외.
                if (!r.enabled)
                {
                    skippedDisabled++;
                    continue;
                }

                // pending placement(배치 미리보기 ghost) 의 material 은 이미 별도 instance —
                // 또 swap 하면 ghost 복구 시 reference 가 깨진다. 정상 흐름엔 Guestbook focus
                // 진입 직전 edit mode 가 종료돼 pending 이 비어 있지만 방어적으로 skip.
                if (pendingMaterialBackups.ContainsKey(r))
                {
                    skipped++;
                    continue;
                }

                // MR #182 skh1205 리뷰 #3: multi-material Renderer 의 모든 slot 처리. 첫 slot 만
                // instance 처리하던 이전 구조는 책상/가구 GLB submesh 가 multi-material 일 때
                // 일부 면이 opaque 그대로 남아 시각적으로 가려지지 않는 회귀.
                var sharedOriginals = r.sharedMaterials;
                var instanceMaterials = r.materials; // getter 가 모든 slot instance 자동 생성

                var cache = new PlacedItemFadeCache
                {
                    Renderer = r,
                    InstanceMaterials = instanceMaterials,
                    SharedOriginals = sharedOriginals,
                    RendererWasEnabled = true, // skip-disabled 가드로 항상 true
                };

                // shader 가 URP 표준 _Surface keyword 를 지원해야 alpha 변경이 시각적 transparent
                // 로 전환된다. Shader Graphs/glTF-pbrMetallicRoughness 같은 custom shader 는 _BaseColor
                // 는 있어도 _Surface 가 없어 alpha 변경 후에도 opaque 그대로 렌더링 — 결국 안 가려진다.
                // 그래서 _Surface 없으면 hidden fallback. 한 slot 이라도 fade 불가면 renderer 전체
                // hidden 으로 통일 (skh1205 #3 권장 — 일부 slot 만 fade 되면 같은 면 일관성 깨짐).
                bool allSlotsApplied = instanceMaterials.Length > 0;
                for (int s = 0; s < instanceMaterials.Length; s++)
                {
                    var mat = instanceMaterials[s];
                    if (mat == null) { allSlotsApplied = false; continue; }

                    ConfigureMaterialAsTransparent(mat);
                    bool supportsTransparentSurface = mat.HasProperty("_Surface");
                    bool slotApplied = false;
                    if (supportsTransparentSurface)
                    {
                        if (mat.HasProperty("_BaseColor"))
                        {
                            var c = mat.GetColor("_BaseColor");
                            c.a = alpha;
                            mat.SetColor("_BaseColor", c);
                            slotApplied = true;
                        }
                        else if (mat.HasProperty("_Color"))
                        {
                            var c = mat.GetColor("_Color");
                            c.a = alpha;
                            mat.SetColor("_Color", c);
                            slotApplied = true;
                        }
                    }
                    if (!slotApplied) allSlotsApplied = false;
                }

                if (!allSlotsApplied)
                {
                    // 하나라도 fade 불가 — renderer 통째 hidden fallback. instance 정리 +
                    // sharedMaterials 원본 복원. 이탈 시 RendererWasEnabled 로 enabled 복구.
                    r.enabled = false;
                    for (int s = 0; s < instanceMaterials.Length; s++)
                    {
                        if (instanceMaterials[s] != null) Destroy(instanceMaterials[s]);
                    }
                    cache.InstanceMaterials = null;
                    r.sharedMaterials = sharedOriginals;
                    hidden++;
                }
                else
                {
                    faded++;
                }

                placedItemFadeCaches.Add(cache);
            }

            // DashboardScreenPanel 자체는 "대시보드 화면 본체" 라 절대 끄면 안 된다 — 사용자가 보려는 대상.
            // dashboardPanelTransform 변수는 자식 traverse 시 제외 기준으로만 사용. UIDocument 토글은 X.

            // NotebookStation/placedItems/MeshSwap_DeskMonitor 자식의 World-Space Canvas
            // (ScreenSaverCanvas 등) 도 별도 토글. DashboardScreenPanel 자식 Canvas 는 대시보드 본체
            // 일부이므로 제외.
            canvasFadeCaches.Clear();
            CollectAndDisableCanvasesUnder(placedItemsRoot != null ? placedItemsRoot.transform : null, dashboardPanelTransform);
            CollectAndDisableCanvasesUnder(notebookStation, dashboardPanelTransform);
            CollectAndDisableCanvasesUnder(monitorSwap, dashboardPanelTransform);

            Debug.Log(
                $"[GuestbookFocus][PlacedFade] applied — faded={faded} hidden-fallback={hidden} " +
                $"skipped(pending={skipped},alreadyDisabled={skippedDisabled}) " +
                $"sources(placedItems={placedItemCount},notebookStation={notebookStationCount},monitorSwap={monitorSwapCount}) " +
                $"canvases-disabled={canvasFadeCaches.Count} alpha={alpha:0.00}");
        }

        private void CollectAndDisableCanvasesUnder(Transform root, Transform exclusionRoot)
        {
            if (root == null) return;
            var canvases = root.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (canvas == null) continue;
                if (exclusionRoot != null && canvas.transform.IsChildOf(exclusionRoot))
                {
                    continue;
                }
                canvasFadeCaches.Add(new CanvasFadeCache { Canvas = canvas, WasEnabled = canvas.enabled });
                canvas.enabled = false;
            }
        }

        private void RestoreGuestbookFocusFade()
        {
            int restored = 0, destroyed = 0;
            for (int i = 0; i < placedItemFadeCaches.Count; i++)
            {
                var cache = placedItemFadeCaches[i];
                if (cache.Renderer != null)
                {
                    // InstanceMaterials != null = faded path 였음 → sharedMaterials 원본 배열로 복구.
                    // null = hidden fallback path 였음 → Apply 시점에 이미 sharedMaterials 원본 복원돼
                    //        있으므로 enabled 만 되돌리면 충분.
                    if (cache.InstanceMaterials != null && cache.SharedOriginals != null)
                    {
                        cache.Renderer.sharedMaterials = cache.SharedOriginals;
                    }
                    cache.Renderer.enabled = cache.RendererWasEnabled;
                    restored++;
                }
                if (cache.InstanceMaterials != null)
                {
                    for (int s = 0; s < cache.InstanceMaterials.Length; s++)
                    {
                        if (cache.InstanceMaterials[s] != null)
                        {
                            Destroy(cache.InstanceMaterials[s]);
                            destroyed++;
                        }
                    }
                    cache.InstanceMaterials = null;
                }
            }

            // Canvas 원복.
            int canvasRestored = 0;
            for (int i = 0; i < canvasFadeCaches.Count; i++)
            {
                var c = canvasFadeCaches[i];
                if (c.Canvas != null)
                {
                    c.Canvas.enabled = c.WasEnabled;
                    canvasRestored++;
                }
            }
            canvasFadeCaches.Clear();

            Debug.Log($"[GuestbookFocus][PlacedFade] restored — restored={restored}/{placedItemFadeCaches.Count} destroyed-instances={destroyed} canvases-restored={canvasRestored}");
            placedItemFadeCaches.Clear();
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
            ResetThemeToDefaultLocally(persistLocalPreview: !IsEditModeActive);
            ApplyThemeResetVisualFallback(persistEntries: !IsEditModeActive);

            ProceedCustomizationEntry(category, entry);
        }

        // 테마 카탈로그의 DefaultTheme(있으면)을 즉시 RoomThemeApplier에 Apply. 클라이언트 측
        // CurrentTheme/AllowsCustomization 상태만 갱신 — 서버 PATCH는 별개 흐름.
        private void ResetThemeToDefaultLocally(bool persistLocalPreview = true)
        {
            if (themeApplier == null) themeApplier = FindFirstObjectByType<RoomThemeApplier>();
            if (themeApplier == null || themeCatalog == null) return;
            var defaultTheme = themeCatalog.DefaultTheme;
            if (defaultTheme != null && defaultTheme != themeApplier.CurrentTheme)
            {
                themeApplier.Apply(defaultTheme);
                SyncInventoryThemeFilterForTheme(defaultTheme);
                RefreshWallVisibilityAfterMaterialMutation("ResetThemeToDefaultLocally");
            }
            if (defaultTheme != null)
            {
                SyncInventoryThemeFilterForTheme(defaultTheme);
            }
            // 명시적 reset 이므로 PlayerPrefs 의 local-preview 키도 비움. 다음 세션에서 Default 로 시작.
            if (persistLocalPreview)
            {
                PersistLastAppliedLocalPreviewTheme(null);
            }
        }

        // Inventory placement ------------------------------------------------------

        private void PlaceInventoryEntry(InventoryEntry entry)
        {
            if (!IsEditModeActive)
            {
                return;
            }
            // 1회 배치 제한 — 동일 자산이 이미 씬에 살아있으면 새로 만들지 않는다.
            // 슬롯은 이미 disabled 처리되지만 race(첫 클릭 ~ Rebuild 사이) 방어로 여기서도 한 번 더 차단.
            // WallDeco/BookshelfDeco 분기보다 먼저 검사 — PlaceWallDecoEntry 가 향후 inventoryKey
            // 시스템에 합류해도 별도 수정 없이 1회 제한이 자동 적용되도록.
            var inventoryKey = BuildInventoryKey(entry);
            if (IsInventoryEntryPlaced(entry))
            {
                Debug.Log($"[RoomEdit][Inventory] 배치 차단 — '{entry.DisplayName}' (key={inventoryKey}) 는 이미 씬에 1개 배치되어 있습니다.");
                return;
            }

            // placement category 분기. WallDeco 는 wall surface 부착 placement 로 별도 흐름.
            // BookshelfDeco 는 후속 (책장 섹션 anchoring 시스템 합류 시점). 그 외 (FloorDeco/
            // 미지정 default) 는 기존 floor placement 유지.
            if (entry.IsRemoteAsset)
            {
                if (TryResolveRemoteCustomizationCategory(entry, out var remoteCategory))
                {
                    ApplyRemoteCategoryInventoryEntry(remoteCategory, entry);
                    return;
                }

                StartCoroutine(PlaceRemoteInventoryEntry(entry, inventoryKey, null));
                return;
            }

            if (entry.Prefab == null)
            {
                return;
            }

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
            // 빈티지 자산은 theme spawn 과 동일한 결과로 통일 — Kind 표준 size 위에 bounds 기반
            // 추가 보정을 얹어 같은 assetKey 면 어디서 spawn 되든 동일 크기. non-vintage 는 no-op.
            ApplyVintageScaleNormalization(go, entry.SourceAssetPath, go.transform.localScale, "PlaceInventoryEntry");
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

        private IEnumerator PlaceRemoteInventoryEntry(
            InventoryEntry entry,
            string inventoryKey,
            DecorationLayoutItem savedItem,
            int expectedExternalLayoutVersion = 0)
        {
            if (entry.RemoteAssetId <= 0L)
            {
                Debug.LogWarning($"[RoomEdit][RemoteAsset] invalid remote asset id for '{entry.DisplayName}'.");
                yield break;
            }

            GameObject go = null;
            string loadError = null;
            yield return LoadRemoteAssetGameObject(
                entry,
                loaded => go = loaded,
                error => loadError = error);

            if (expectedExternalLayoutVersion > 0 &&
                expectedExternalLayoutVersion != externalDecorationLayoutVersion)
            {
                if (go != null)
                {
                    Destroy(go);
                }

                Debug.Log(
                    $"[RoomEdit][RemoteAsset] stale server layout restore skipped for '{entry.DisplayName}' " +
                    $"version={expectedExternalLayoutVersion} current={externalDecorationLayoutVersion}.");
                yield break;
            }

            if (go == null)
            {
                Debug.LogWarning($"[RoomEdit][RemoteAsset] placement skipped for '{entry.DisplayName}': {loadError}");
                yield break;
            }

            EnsurePlacedItemsRoot();
            go.name = $"Decoration_{SanitizeName(entry.DisplayName)}_{Guid.NewGuid():N}";
            go.transform.SetParent(placedItemsRoot.transform, true);

            if (savedItem != null)
            {
                go.transform.SetPositionAndRotation(
                    new Vector3(savedItem.PositionX, savedItem.PositionY, savedItem.PositionZ),
                    Quaternion.Euler(0f, savedItem.RotationY, 0f));
                go.transform.localScale = new Vector3(savedItem.ScaleX, savedItem.ScaleY, savedItem.ScaleZ);
                EnsureDecorationCollider(go);
                var savedMarker = go.AddComponent<DecorationItemMarker>();
                savedMarker.InventoryKey = inventoryKey;
                if (!string.IsNullOrEmpty(inventoryKey))
                {
                    placedByInventoryKey[inventoryKey] = go;
                }
                yield break;
            }

            NormalizeSpawnedScale(go, entry);
            Physics.SyncTransforms();

            var existingCount = placedItemsRoot.transform.childCount - 1;
            var lateral = (existingCount % 3 - 1) * InventorySpawnSpacing;
            var depth = (existingCount / 3) * InventorySpawnSpacing;

            var rendererBounds = ComputeRendererBounds(go);
            var verticalOffset = rendererBounds.HasValue ? -rendererBounds.Value.min.y : 0f;
            go.transform.position = InventoryPlacementCenter + new Vector3(lateral, verticalOffset, depth);

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

                spawnBounds = ComputeFloorFootprintBounds(go) ?? ComputeRendererBounds(go) ?? rendererBounds.Value;
                if (!TryFindClearSpawnPosition(go, spawnBounds, go.transform.position, out var clearPosition))
                {
                    Debug.LogWarning($"[RoomEdit][RemoteAsset] spawn rejected for '{entry.DisplayName}': no clear floor cell.");
                    Destroy(go);
                    yield break;
                }
                go.transform.position = clearPosition;
            }

            EnsureDecorationCollider(go);

            var marker = go.AddComponent<DecorationItemMarker>();
            marker.InventoryKey = inventoryKey;
            if (!string.IsNullOrEmpty(inventoryKey))
            {
                placedByInventoryKey[inventoryKey] = go;
            }

            RebuildInventorySlots();
            SetDirty("PlaceRemoteInventoryEntry");
            BeginPendingPlacement(go);
            SelectObject(go);
        }

        private IEnumerator LoadRemoteAssetGameObject(InventoryEntry entry, Action<GameObject> onSuccess, Action<string> onError)
        {
            var hasVisibleManifest = visibleRemoteAssetManifests.TryGetValue(entry.RemoteAssetId, out var manifest);
            if (IsReadOnlySpacePreview && (!hasVisibleManifest || manifest == null))
            {
                onError?.Invoke("visible space manifest is missing.");
                yield break;
            }

            if (manifest == null)
            {
                var apiManager = APIManager.Instance;
                if (apiManager == null || !apiManager.HasAccessToken)
                {
                    onError?.Invoke("API token is missing.");
                    yield break;
                }

                NotebookApiClient apiClient;
                try
                {
                    apiClient = new NotebookApiClient(apiManager);
                }
                catch (Exception ex)
                {
                    onError?.Invoke($"NotebookApiClient failed: {ex.GetType().Name} {ex.Message}");
                    yield break;
                }

                string manifestError = null;
                var manifestDone = false;
                apiClient.GetAssetDownloadManifest(
                    entry.RemoteAssetId,
                    response =>
                    {
                        manifest = response;
                        manifestDone = true;
                    },
                    error =>
                    {
                        manifestError = $"{error?.Code}/{error?.Message}";
                        manifestDone = true;
                    });

                while (!manifestDone)
                {
                    yield return null;
                }

                if (manifest == null)
                {
                    onError?.Invoke($"download manifest failed: {manifestError}");
                    yield break;
                }
            }

            if (manifest == null)
            {
                onError?.Invoke("download manifest is missing.");
                yield break;
            }

            remoteAssetCacheService ??= new RemoteAssetCacheService();
            IReadOnlyList<string> cachedPaths = null;
            string cacheError = null;
            yield return remoteAssetCacheService.EnsureManifestCached(
                manifest,
                paths => cachedPaths = paths,
                error => cacheError = error);

            if (!string.IsNullOrWhiteSpace(cacheError))
            {
                onError?.Invoke(cacheError);
                yield break;
            }

            var modelPath = PickPrimaryRemoteModelPath(cachedPaths);
            if (string.IsNullOrWhiteSpace(modelPath))
            {
                onError?.Invoke("cached manifest has no loadable model file.");
                yield break;
            }

            yield return InstantiateRemoteModel(modelPath, entry.DisplayName, onSuccess, onError);
        }

        private IEnumerator InstantiateRemoteModel(string path, string displayName, Action<GameObject> onSuccess, Action<string> onError)
        {
            var extension = Path.GetExtension(path);
            if (string.Equals(extension, ".bundle", StringComparison.OrdinalIgnoreCase))
            {
                var bundle = AssetBundle.LoadFromFile(path);
                if (bundle == null)
                {
                    onError?.Invoke($"AssetBundle load failed: {path}");
                    yield break;
                }

                var assets = bundle.LoadAllAssets<GameObject>();
                if (assets == null || assets.Length == 0 || assets[0] == null)
                {
                    bundle.Unload(false);
                    onError?.Invoke($"AssetBundle has no GameObject: {path}");
                    yield break;
                }

                var instance = Instantiate(assets[0]);
                bundle.Unload(false);
                onSuccess?.Invoke(instance);
                yield break;
            }

            var root = new GameObject($"RemoteAsset_{SanitizeName(displayName)}");
            var gltf = new GltfImport();
            try
            {
                var loadTask = gltf.LoadFile(path);
                while (!loadTask.IsCompleted)
                {
                    yield return null;
                }

                if (loadTask.IsFaulted || !loadTask.Result)
                {
                    Destroy(root);
                    onError?.Invoke(loadTask.Exception != null ? loadTask.Exception.GetBaseException().Message : $"glTF load failed: {path}");
                    yield break;
                }

                var instantiateTask = gltf.InstantiateMainSceneAsync(root.transform);
                while (!instantiateTask.IsCompleted)
                {
                    yield return null;
                }

                if (instantiateTask.IsFaulted || !instantiateTask.Result)
                {
                    Destroy(root);
                    onError?.Invoke(instantiateTask.Exception != null ? instantiateTask.Exception.GetBaseException().Message : $"glTF instantiate failed: {path}");
                    yield break;
                }

                onSuccess?.Invoke(root);
            }
            finally
            {
                gltf.Dispose();
            }
        }

        private static string PickPrimaryRemoteModelPath(IReadOnlyList<string> paths)
        {
            if (paths == null || paths.Count == 0)
            {
                return null;
            }

            for (var i = 0; i < paths.Count; i++)
            {
                var ext = Path.GetExtension(paths[i]);
                if (string.Equals(ext, ".glb", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ext, ".gltf", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ext, ".bundle", StringComparison.OrdinalIgnoreCase))
                {
                    return paths[i];
                }
            }

            return paths[0];
        }

        private static void EnsureDecorationCollider(GameObject go)
        {
            if (go == null || go.GetComponentInChildren<Collider>() != null)
            {
                return;
            }

            EnsureRootBoxColliderFromRendererBounds(go, skipWallMaskQuad: false);
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
            "fantasy_table(Clone)",
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

        private static Bounds? ComputeRendererBounds(GameObject go, string excludedRendererObjectName = null)
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
                if (!string.IsNullOrEmpty(excludedRendererObjectName) && r.gameObject.name == excludedRendererObjectName) { skipped++; continue; }
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
                for (var i = 0; i < renderers.Length; i++)
                {
                    var fallbackRenderer = renderers[i];
                    if (fallbackRenderer == null) continue;
                    if (!string.IsNullOrEmpty(excludedRendererObjectName) && fallbackRenderer.gameObject.name == excludedRendererObjectName) continue;
                    aggregate = fallbackRenderer.bounds;
                    break;
                }
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
            EnsureRootBoxColliderFromRendererBounds(go, skipWallMaskQuad: true);
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

        private static void DestroyEditorSafe(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

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
            if (IsReadOnlySpacePreview)
            {
                Debug.Log("[RoomEditModeController] Room thumbnail upload skipped: read-only space preview.");
                return;
            }

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
                "전체 필수 책상 책장 대시보드 노트북 벽지 내부조명 외부조명 필수 오브젝트 스킨 전체 테마 " +
                "보유 인벤토리 " +
                "회전 삭제 " +
                "준비 중입니다 확인";
            targetFontAsset.TryAddCharacters(roomEditCharacters, out _);
        }
    }
}
