using System;
using System.Collections.Generic;
using App.Application.Dashboard;
using App.Infrastructure.Api;
using App.Infrastructure.Api.Dashboard;
using App.Infrastructure.Api.Dtos;
using App.Infrastructure.Catalogs;
using BookshelfPorting.Runtime;
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
        // when edit mode starts. Values match MainScene.unity Floor's
        // pre-wall-thickness footprint as a safe default.
        private const float FloorMinX = -2.0f;
        private const float FloorMaxX = 2.0f;
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
        }

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
        private GameObject bottomMenuRoot;
        private GameObject inventoryContentRoot;
        private GameObject returnButtonRoot;
        private GameObject readyModalRoot;
        private GameObject themeApplyModeModalRoot;
        private GameObject customizationLockedModalRoot;
        private TMP_Text customizationLockedBodyText;
        private CustomizationCategory pendingCustomizationCategory;
        private CustomizationEntry pendingCustomizationEntry;
        private GameObject selectionPanelRoot;
        private Button selectionRotateButton;

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
        private bool gridSnapEnabled = true;
        private GameObject gridDisplayRoot;

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

        // Decoration catalog loaded from Resources (built by the editor menu item).
        private readonly List<InventoryEntry> inventoryEntries = new List<InventoryEntry>();

        private BookshelfExperienceManager cachedExperienceManager;
        private Camera cachedSceneCamera;

        private struct RendererTint
        {
            public Renderer Renderer;
            public Color OriginalColor;
        }

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
                    });
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
        // catalog entry; "가구" surfaces anything coming from Assets/Arts/Models/Props
        // (and the Furniture sibling folder) per spec; the remaining categories are
        // wireframe placeholders for now and intentionally show an empty list until
        // their content pipelines exist.
        private static bool MatchesCategory(InventoryEntry entry, string categoryKey)
        {
            switch (categoryKey)
            {
                case "ALL":
                    return true;
                case "FURNITURE":
                    var path = entry.SourceAssetPath ?? string.Empty;
                    return path.IndexOf("/Props/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           path.IndexOf("/Furniture/", StringComparison.OrdinalIgnoreCase) >= 0;
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

            BuildBottomMenu(editOverlayRoot.transform);
            BuildPillarboxFills(editOverlayRoot.transform);
            BuildReturnButton(editOverlayRoot.transform);
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
            themeApplier.Apply(theme);
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
                return;
            }

            EnsureThemeUseCase();
            if (themeUseCase == null)
            {
                Debug.LogWarning("[RoomEditModeController] ISpaceSettingsRepository 미주입 — 백엔드 theme apply skip (visual만 적용).");
                return;
            }

            if (theme.AssetId <= 0)
            {
                Debug.LogWarning("[RoomEditModeController] theme AssetId가 없어 백엔드 theme apply skip (visual만 적용).");
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
                        ApplyThemeDefaultEntries(theme);
                    }
                },
                onError: error =>
                {
                    if (requestVersion != themeRequestVersion)
                    {
                        return;
                    }
                    // Code가 비어있으면 백엔드 응답 에러가 아니라 네트워크 connection 실패
                    // (dev 환경에서 백엔드 미기동 등) — visual은 유지하고 warning만.
                    bool isNetworkFailure = string.IsNullOrEmpty(error?.Code);
                    if (isNetworkFailure)
                    {
                        Debug.LogWarning($"[RoomEditModeController] PATCH connection 실패 ({error?.Message}) — visual은 유지, 다음 로드 시 서버와 동기화.");
                        return;
                    }

                    Debug.LogWarning($"[RoomEditModeController] theme apply 실패 ({error.Code}/{error.Message}) — 이전 테마로 롤백.");
                    if (previousAppliedTheme != null && previousAppliedTheme != theme)
                    {
                        themeApplier.Apply(previousAppliedTheme);
                        RebuildInventorySlots();
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

            // 1) 이전 테마 spawn deco 정리 — 사용자 직접 placement 는 보존 (ThemeSpawnedMarker 로 구분)
            ClearThemeSpawnedDecos();

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
            //    DecorationItemMarker + ThemeSpawnedMarker 추가 → 사용자가 drag/select/delete 가능.
            //    다른 테마 적용 시 ClearThemeSpawnedDecos 가 정리.
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

        // 테마 변경 시 이전 테마 spawn deco 정리. ThemeSpawnedMarker 가진 GameObject 만 destroy
        // — 사용자 직접 placement (marker 없음) 는 보존. selection/drag 상태도 정리.
        private void ClearThemeSpawnedDecos()
        {
            if (placedItemsRoot == null) return;
            var markers = placedItemsRoot.GetComponentsInChildren<ThemeSpawnedMarker>(true);
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] == null) continue;
                var go = markers[i].gameObject;
                if (selectedObject == go) selectedObject = null;
                if (draggingObject == go) draggingObject = null;
                if (UnityEngine.Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
        }

        // 테마 floorDecoSpec → placedItemsRoot 자식 spawn. PlaceInventoryEntry 와 동일 시스템 —
        // DecorationItemMarker + collider → 사용자가 클릭해 drag/delete 가능. ThemeSpawnedMarker
        // 로 테마 자동 spawn 식별.
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
            go.AddComponent<ThemeSpawnedMarker>();
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

            Vector3 worldPos;
            Quaternion worldRot;
            if (themeApplier != null && themeApplier.TryGetWallSurfaceAnchor(spec.WallSide, out var anchorPos, out var anchorRot))
            {
                worldPos = anchorPos + anchorRot * spec.LocalPosition;
                worldRot = anchorRot * Quaternion.Euler(spec.LocalRotation);
            }
            else
            {
                Debug.LogWarning($"[RoomEditModeController] WallDecoSpec WallSide={spec.WallSide} anchor 못 찾음 — localPosition 을 world 로 fallback.");
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
            go.AddComponent<ThemeSpawnedMarker>();
            EnsureCollidersForGameObject(go);
        }

        // 일반화된 collider 보장 — EnsureCollidersForWallDeco 와 동일 로직 (mesh 자식에 MeshCollider).
        // WallMaskQuad 는 stencil 전용이라 제외.
        private static void EnsureCollidersForGameObject(GameObject go)
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
                collider.convex = false;
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
                ApplyCategoryMeshSwap(category, entry.Prefab, roomRootTransform);
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
        private void ApplyCategoryMeshSwap(CustomizationCategory category, GameObject prefab, Transform roomRoot)
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

            // 2) native renderer 들 disable (visual hide, GameObject 자체는 유지 — collider/
            // 기능 컴포넌트 보존). 카테고리 매칭 transform 의 모든 자식 renderer 까지 hide —
            // NotebookStation 같은 root 의 자식 (UnibodyBase/Trackpad 등) 은 prefix 매칭 안
            // 되어 renderer 자체 검색만으론 missed. transform 단위로 매칭 후 GetComponentsInChildren
            // 으로 자식까지 모두 hide → 노트북 visual 도 같이 사라짐.
            Transform anchor = null;
            var allTransforms = roomRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                var t = allTransforms[i];
                if (!MatchesCategoryName(category, t.gameObject.name)) continue;
                if (anchor == null) anchor = t;
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    r.enabled = false;
                }
            }
            if (anchor == null)
            {
                Debug.LogWarning($"[RoomEditModeController] {category} mesh swap — native anchor 못 찾음, prefab spawn skip.");
                return;
            }

            // 3) prefab 을 roomRoot 자식으로 spawn — anchor 의 world position/rotation 적용.
            // 직접 anchor 자식이 아닌 이유: anchor 가 wall 처럼 non-uniform scale 을 가질 수
            // 있어 prefab 이 찌그러질 수 있음. roomRoot (보통 scale 1) 자식 + anchor world
            // 좌표 적용으로 wall mesh scale propagation [P1] 회피.
            var go = Instantiate(prefab, roomRoot);
            go.name = swapName;
            go.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            go.transform.localScale = Vector3.one;
        }

        // mesh swap 복원 — native renderer 들 enable + spawned mesh swap 정리.
        private void RestoreCategoryNativeMesh(CustomizationCategory category, Transform roomRoot)
        {
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
            if (themeApplier != null && themeCatalog != null)
            {
                var resolvedTheme = themeCatalog.Resolve(space.CurrentThemeInventoryId);
                if (resolvedTheme != null && resolvedTheme != themeApplier.CurrentTheme)
                {
                    themeApplier.Apply(resolvedTheme);
                }
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
            if (entry == null && catalog.Entries.Count > 0)
            {
                entry = catalog.Entries[0];
            }
            if (entry == null) return;

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

        private void BuildSelectionPanel()
        {
            selectionPanelRoot = CreateRectObject("SelectionPanel", canvas.transform);
            var rect = (RectTransform)selectionPanelRoot.transform;
            // Anchor to the top edge of the bottom menu (BottomMenuFraction from
            // bottom of canvas). Sits 16 units above so rotate/delete is reachable
            // without overlapping the toolbar.
            rect.anchorMin = new Vector2(0.5f, BottomMenuFraction);
            rect.anchorMax = new Vector2(0.5f, BottomMenuFraction);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(280f, 84f);
            rect.anchoredPosition = new Vector2(0f, 16f);

            var bg = CreateImage(selectionPanelRoot.transform, "Background", WithAlpha(CardColor, 0.97f));
            bg.sprite = roundedRectSprite;
            bg.type = Image.Type.Sliced;
            StretchToParent(bg.rectTransform);
            var outline = bg.gameObject.AddComponent<Outline>();
            outline.effectColor = WithAlpha(BorderColor, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);

            var rotateButton = CreateButton(selectionPanelRoot.transform, "RotateButton", "회전",
                SecondaryColor, TextColor, new Vector2(112f, 60f), true, BorderColor);
            var rotateRect = (RectTransform)rotateButton.transform;
            rotateRect.anchorMin = new Vector2(0f, 0.5f);
            rotateRect.anchorMax = new Vector2(0f, 0.5f);
            rotateRect.pivot = new Vector2(0f, 0.5f);
            rotateRect.anchoredPosition = new Vector2(16f, 0f);
            rotateButton.onClick.AddListener(RotateSelectedObject);
            selectionRotateButton = rotateButton;

            var deleteButton = CreateButton(selectionPanelRoot.transform, "DeleteButton", "삭제",
                Hex("#C14E4E"), Color.white, new Vector2(112f, 60f), false, BorderColor);
            var deleteRect = (RectTransform)deleteButton.transform;
            deleteRect.anchorMin = new Vector2(1f, 0.5f);
            deleteRect.anchorMax = new Vector2(1f, 0.5f);
            deleteRect.pivot = new Vector2(1f, 0.5f);
            deleteRect.anchoredPosition = new Vector2(-16f, 0f);
            deleteButton.onClick.AddListener(DeleteSelectedObject);
        }

        // Update / visibility ------------------------------------------------------

        private void Update()
        {
            if (!isInitialized)
            {
                return;
            }

            UpdateButtonVisibility();
            UpdateSelectionPanel();

            if (IsEditModeActive)
            {
                HandleEditModeInput();
            }
            else if (IsDecorationPopupOpen)
            {
                HandleDecorationPopupOutsideClick();
            }
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

            // Force-exit edit mode if the player ends up in a focus mode some other way.
            if (IsEditModeActive && experienceManager != null && !inMainScene)
            {
                ExitEditModeInternal();
            }
        }

        private void UpdateSelectionPanel()
        {
            var shouldShow = IsEditModeActive && selectedObject != null;
            if (selectionPanelRoot != null && selectionPanelRoot.activeSelf != shouldShow)
            {
                selectionPanelRoot.SetActive(shouldShow);
            }

            // 회전 비활성 entry — wall deco marker.AllowRotation == false 면 회전 버튼 disabled.
            if (selectionRotateButton != null)
            {
                bool allowRotation = true;
                if (selectedObject != null)
                {
                    var marker = selectedObject.GetComponent<WallDecoMarker>();
                    if (marker != null && !marker.AllowRotation) allowRotation = false;
                }
                if (selectionRotateButton.interactable != allowRotation)
                {
                    selectionRotateButton.interactable = allowRotation;
                }
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
            ExitEditModeInternal();
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
            EndCameraViewportEdit();
            if (editOverlayRoot != null)
            {
                editOverlayRoot.SetActive(false);
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
        }

        // Shrink the main camera's viewport rect so it renders only into the area
        // above the bottom menu. We do NOT touch clearFlags, backgroundColor, FOV,
        // transform, post-processing, or any Volume — only camera.rect. The bottom
        // fraction is computed from the BottomMenu's actual on-screen pixel height
        // so the boundary aligns exactly with the menu's top edge regardless of
        // device aspect or canvas scaler match factor.
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
            if (editModeCamera != null && savedCameraRectValid)
            {
                editModeCamera.rect = savedCameraRect;
            }
            editModeCamera = null;
            savedCameraRectValid = false;
        }

        private void OnDestroy()
        {
            // Defensive: if the controller is torn down while edit mode is active,
            // restore the camera viewport so a re-loaded scene doesn't inherit a
            // partial rect.
            EndCameraViewportEdit();
            RoomEditThemeFilter.Changed -= HandleThemeFilterChanged;
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

            // Step 1 — uniform-scale the model so its largest XZ extent matches a
            // sensible per-category target (small props ~0.5m, furniture ~1.2m).
            // Aspect ratio is preserved (one scale factor on all axes), so models
            // never appear squashed.
            NormalizeSpawnedScale(go, entry.SourceAssetPath);

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
            if (rendererBounds.HasValue)
            {
                var spawnBounds = ComputeRendererBounds(go) ?? rendererBounds.Value;
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
                var spawnBounds = ComputeRendererBounds(go) ?? rendererBounds.Value;
                if (!TryFindClearSpawnPosition(go, spawnBounds, go.transform.position, out var clearPosition))
                {
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

            go.AddComponent<DecorationItemMarker>();
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

        // True if a box the size of `spawnBounds` placed at `position` (in
        // world space) overlaps any non-self, non-shell, non-trigger
        // collider. Mirrors the half-extent shaping used by the drag-time
        // collision check so spawn and drag agree on what "blocked" means.
        private static bool IsSpawnPositionBlockedAt(GameObject self, Bounds spawnBounds, Vector3 position)
        {
            var halfExtents = new Vector3(
                Mathf.Max(0.005f, spawnBounds.extents.x),
                Mathf.Max(0.005f, spawnBounds.extents.y - BlockerMargin),
                Mathf.Max(0.005f, spawnBounds.extents.z));

            // spawnBounds.center is at the object's *current* world position.
            // Translate the center by the candidate-vs-current delta to query
            // the box at the candidate position.
            var sampleCenter = spawnBounds.center + (position - self.transform.position);
            var hits = Physics.OverlapBox(sampleCenter, halfExtents, Quaternion.identity, BlockerLayerMask, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit == null || hit.isTrigger) continue;
                if (self != null && hit.transform.IsChildOf(self.transform)) continue;

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
        private static void NormalizeSpawnedScale(GameObject go, string sourceAssetPath)
        {
            if (TryGetCanonicalScaleByAssetPath(sourceAssetPath, out var canonical))
            {
                go.transform.localScale = canonical;
                return;
            }

            var bounds = ComputeRendererBounds(go);
            if (!bounds.HasValue)
            {
                return;
            }

            var size = bounds.Value.size;
            var largestXZ = Mathf.Max(size.x, size.z);
            if (largestXZ < 0.0001f)
            {
                return;
            }

            var targetSize = ResolveTargetMeterSize(sourceAssetPath);
            var scaleMultiplier = targetSize / largestXZ;
            // Keep the multiplier within a sane range so a tiny mesh isn't blown
            // up to room scale or a giant mesh shrunk to a speck even if its
            // imported size is wildly off.
            scaleMultiplier = Mathf.Clamp(scaleMultiplier, 0.05f, 20f);
            go.transform.localScale *= scaleMultiplier;
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

        private static float ResolveTargetMeterSize(string sourceAssetPath)
        {
            if (string.IsNullOrEmpty(sourceAssetPath))
            {
                return DefaultGenericTargetSizeMeters;
            }
            if (sourceAssetPath.IndexOf("/Props/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return DefaultPropTargetSizeMeters;
            }
            if (sourceAssetPath.IndexOf("/Furniture/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return DefaultFurnitureTargetSizeMeters;
            }
            return DefaultGenericTargetSizeMeters;
        }

        private static Bounds? ComputeRendererBounds(GameObject go)
        {
            // includeInactive = true so a .glb whose visual root is
            // temporarily disabled still reports a usable AABB. UpdateDrag
            // gates ClampToFloorBounds + ResolveBlockerCollisions on
            // bounds.HasValue — without this the dragged object would
            // skip BOTH the wall clamp and the blocker check whenever its
            // renderer hierarchy contained an inactive node, so it could
            // tunnel into walls / fixtures unconstrained.
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return null;
            }
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
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

        private void BeginEditModePointerPress(Camera camera, Vector2 pointer)
        {
            // Anything that the canvas raycaster considers UI (bottom menu,
            // rotate / delete buttons, return button) just stops here.
            if (IsPointerOverUi())
            {
                return;
            }

            var pixelRect = camera.pixelRect;
            if (pointer.x < pixelRect.xMin || pointer.x > pixelRect.xMax ||
                pointer.y < pixelRect.yMin || pointer.y > pixelRect.yMax)
            {
                DeselectCurrentObject();
                return;
            }

            var ray = camera.ScreenPointToRay(pointer);
            if (Physics.Raycast(ray, out var hit, 100f))
            {
                var marker = hit.collider.GetComponentInParent<DecorationItemMarker>();
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
                    return;
                }
            }

            DeselectCurrentObject();
        }

        // Drag lifecycle ----------------------------------------------------------

        private void BeginDrag(GameObject target, Ray initialRay)
        {
            draggingObject = target;
            dragLastValidPosition = target.transform.position;

            // Capture grab offset so the model doesn't snap its center under the
            // pointer at drag start — instead it stays where it was relative to
            // where the player grabbed it.
            if (TryRaycastFloorPlane(initialRay, out var grabPoint))
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

            // Re-run scene-shape detection in case any wall, fixture, or
            // decoration was added / moved / scaled since edit mode was
            // entered. The grid display rebuild below picks up any change.
            RefreshPlayableBounds();
            EnsureBlockerColliders();
            EnsureDecorationRootColliders();
            RebuildBlockerRendererCache(target);
            ShowGrid();
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

            var candidate = new Vector3(rawX, draggingObject.transform.position.y, rawZ);
            var currentPosition = draggingObject.transform.position;
            var bounds = ComputeRendererBounds(draggingObject);
            if (bounds.HasValue)
            {
                candidate = ClampToFloorBounds(currentPosition, candidate, bounds.Value);
                candidate = ResolveBlockerCollisions(currentPosition, candidate, bounds.Value);
            }

            draggingObject.transform.position = candidate;
            dragLastValidPosition = candidate;
        }

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

            // candidate transform 임시 적용 → bounds 계산 → invalid 면 rollback.
            // candidate 기준 bounds 정확히 측정해야 obstruction check 정확. transform
            // 변경 후 Renderer.bounds 가 즉시 갱신되므로 1 프레임 안에 검사 + 필요 시 rollback.
            var prevPosition = draggingObject.transform.position;
            var prevRotation = draggingObject.transform.rotation;
            var prevWall = marker.CurrentWall;

            draggingObject.transform.position = hitPoint;
            draggingObject.transform.rotation = WallFacingRotation(newWall);

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
                marker.CurrentWall = newWall;
                dragLastValidPosition = hitPoint;
            }
            else
            {
                // 책장/대시보드 영역 진입 차단 — 마지막 valid 위치/회전 유지. 사용자가
                // mouse 를 obstruction 밖으로 옮기면 다음 frame 에서 자동 follow.
                draggingObject.transform.position = prevPosition;
                draggingObject.transform.rotation = prevRotation;
                marker.CurrentWall = prevWall;
            }

            ApplyWallDecoValidityFeedback(draggingObject, isFree);
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
            var tint = isValid ? Color.white : new Color(1f, 0.55f, 0.55f, 1f);
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
            // Position is always inside the room because UpdateDrag clamped it
            // every frame, so we don't need to revert anywhere. Just close out
            // the drag state and hide the grid overlay.
            draggingObject = null;
            HideGrid();
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
        private Vector3 ClampToFloorBounds(Vector3 currentPosition, Vector3 candidate, Bounds currentBounds)
        {
            // current bounds are in world space, anchored to the object's current
            // position. Translate by the candidate-vs-current delta to get the
            // bounds the object would have at the candidate position.
            var delta = candidate - currentPosition;
            var futureCenter = currentBounds.center + delta;
            var extents = currentBounds.extents;

            var minCenterX = playableMinX + extents.x;
            var maxCenterX = playableMaxX - extents.x;
            var minCenterZ = playableMinZ + extents.z;
            var maxCenterZ = playableMaxZ - extents.z;

            // Defensive: if the object is wider than the room (shouldn't happen
            // post-NormalizeSpawnedScale, but a tweaked catalog could), keep
            // min ≤ max so Mathf.Clamp returns something deterministic.
            if (minCenterX > maxCenterX) { var mid = (minCenterX + maxCenterX) * 0.5f; minCenterX = maxCenterX = mid; }
            if (minCenterZ > maxCenterZ) { var mid = (minCenterZ + maxCenterZ) * 0.5f; minCenterZ = maxCenterZ = mid; }

            var clampedCenterX = Mathf.Clamp(futureCenter.x, minCenterX, maxCenterX);
            var clampedCenterZ = Mathf.Clamp(futureCenter.z, minCenterZ, maxCenterZ);

            return new Vector3(
                candidate.x + (clampedCenterX - futureCenter.x),
                candidate.y,
                candidate.z + (clampedCenterZ - futureCenter.z));
        }

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
        }

        private void RotateSelectedObject()
        {
            if (selectedObject == null) return;
            // entry-level allowRotation flag — 창문처럼 fixed orientation 이어야 하는
            // wall deco 는 회전 무시. UpdateSelectionPanel 에서 버튼도 disabled 상태이지만
            // 키보드/외부 트리거 가능성 방어.
            var marker = selectedObject.GetComponent<WallDecoMarker>();
            if (marker != null && !marker.AllowRotation)
            {
                return;
            }
            // Don't rotate mid-drag — would invalidate the bounds-based placement
            // check until the next move tick. Forces drag to commit first.
            if (draggingObject != null)
            {
                FinishDrag();
            }
            selectedObject.transform.Rotate(Vector3.up, SelectionRotateStepDegrees, Space.World);
        }

        private void DeleteSelectedObject()
        {
            if (selectedObject == null) return;
            if (draggingObject != null)
            {
                // Drop the in-flight drag state cleanly before destroying so the
                // next pointer event doesn't see a torn-down object.
                draggingObject = null;
                HideGrid();
            }
            var toDestroy = selectedObject;
            DeselectCurrentObject();
            Destroy(toDestroy);
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

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
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
