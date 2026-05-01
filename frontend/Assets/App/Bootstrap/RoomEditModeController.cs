using System;
using System.Collections.Generic;
using App.Application.Dashboard;
using App.Infrastructure.Api;
using App.Infrastructure.Api.Dashboard;
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

        private struct InventoryEntry
        {
            public string DisplayName;
            public GameObject Prefab;
            public string SourceAssetPath;
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
        private GameObject selectionPanelRoot;

        private string selectedToolbarKey = "ALL";
        private readonly Dictionary<string, Image> toolbarBackgrounds = new Dictionary<string, Image>();
        private readonly Dictionary<string, TMP_Text> toolbarLabels = new Dictionary<string, TMP_Text>();

        private RoomThemeCatalogAsset themeCatalog;
        private RoomThemeApplier themeApplier;
        private UpdateSpaceThemeUseCase themeUseCase;
        private RoomThemeAsset previousAppliedTheme;
        // 빠른 테마 연타 시 stale callback 무시. 매 클릭마다 ++해서 onSuccess/onError에
        // 캡쳐된 version과 현재 version이 다르면 응답 무시. (MR 72의 walletDetailRequestVersion 동일 패턴)
        private int themeRequestVersion;

        private GameObject placedItemsRoot;
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
            BuildSelectionPanel();

            CloseDecorationPopupInternal();
            ExitEditModeInternal();
            HideReadyModal();
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
                    inventoryEntries.Add(new InventoryEntry
                    {
                        DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.Prefab.name : entry.DisplayName,
                        Prefab = entry.Prefab,
                        SourceAssetPath = entry.SourceAssetPath ?? string.Empty,
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

        // Clears the inventory content row and rebuilds it from the entries that
        // match the currently selected toolbar category. Called on init and on every
        // category click so the inventory always reflects the active tab.
        private void RebuildInventorySlots()
        {
            if (inventoryContentRoot == null)
            {
                return;
            }

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
            // 묶음 탭(FURNITURE/OBJECT_SKIN)은 후속 PR에서 sub-탭화.
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

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                var entry = catalog.Entries[i];
                if (entry == null) continue;
                BuildCustomizationSlot(parent, i, category, entry);
            }
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

        // 현재 적용 중인 카테고리 항목인지 검사. Space.Current*InventoryId와 entry.AssetId 비교.
        // 우선은 RoomThemeApplier.CurrentTheme의 spec를 reference로 사용 — 후속에서 Space 도메인 직접 매핑 가능.
        private bool IsCategoryEntryCurrent(CustomizationCategory category, long assetId)
        {
            // TODO: Space 도메인의 6 카테고리 ID와 비교. 현재는 themeApplier의 CurrentTheme만 참고
            // (전체 테마 적용 후 카테고리 강조는 후속 마디에서 정확히 처리).
            return false;
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

            EnsureThemeUseCase();
            if (themeUseCase == null)
            {
                Debug.LogWarning("[RoomEditModeController] ISpaceSettingsRepository 미주입 — 백엔드 PATCH skip (visual만 적용).");
                return;
            }

            long? inventoryId = theme.AssetId > 0 ? (long?)theme.AssetId : null;
            // 연타 시 stale callback 무시: 캡쳐된 version과 현재 version이 다르면 이번 요청은
            // 이미 다음 요청에 의해 덮였으므로 응답 무시. (rollback도, success log도 안 함)
            int requestVersion = ++themeRequestVersion;
            themeUseCase.Execute(
                inventoryId,
                onSuccess: () =>
                {
                    if (requestVersion != themeRequestVersion)
                    {
                        return;
                    }
                    Debug.Log($"[RoomEditModeController] PATCH /spaces/me/theme OK → inventoryId={inventoryId}.");
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

                    Debug.LogWarning($"[RoomEditModeController] PATCH 실패 ({error.Code}/{error.Message}) — 이전 테마로 롤백.");
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
                themeUseCase = new UpdateSpaceThemeUseCase(repo);
            }
            catch (Exception ex)
            {
                // 어느 단계(NotebookApiClient ctor / Repository ctor / UseCase ctor)에서 깨졌는지
                // stack까지 같이 출력해야 디버깅 가능.
                Debug.LogError($"[RoomEditModeController] UpdateSpaceThemeUseCase 생성 실패 ({ex.GetType().Name}).");
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

            // visual swap — 카테고리별로 적용 (현재는 단일 항목 swap 골격, RoomThemeApplier 카테고리
            // swap helper 없으면 룸 root traverse하며 직접 처리)
            ApplyEntryVisual(category, entry);
            RebuildInventorySlots();

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
                    Debug.LogWarning("[RoomEditModeController] 커스터마이징 불가 테마 자동 해제 — 룸 전체 reload 필요(TODO).");
                    // TODO: Space 재로드 + RoomThemeApplier.Apply(default theme) + 모든 책장 skin 초기화 visual
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

        // 카테고리별 visual swap. RoomThemeApplier가 RoomThemeAsset 단위로 처리하므로,
        // CustomizationEntry 단일 항목은 룸 root traverse + sharedMaterial swap 또는
        // light 직접 set로 처리. 메쉬/placement는 후속.
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

            // 라이트 카테고리는 씬의 Directional Light 직접 변경 (Applier 의존 없음)
            if (category == CustomizationCategory.ExteriorLight)
            {
                var dirLight = FindFirstDirectionalLight();
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

            // 머티리얼 카테고리는 룸 root에서 패턴 매칭하여 sharedMaterial swap.
            // RoomThemeApplier가 같은 패턴을 알지만 단일 entry용 helper가 없어 inline.
            var mat = entry.PrimaryMaterial;
            if (mat == null) return;

            // 카테고리별 GameObject 이름 패턴은 RoomThemeApplier와 동일.
            string[] patterns = ResolveMaterialSwapPatterns(category);
            if (patterns == null || patterns.Length == 0) return;

            var roomRootTransform = FindRoomRoot();
            if (roomRootTransform == null) return;
            int swapped = 0;
            foreach (var renderer in roomRootTransform.GetComponentsInChildren<Renderer>(true))
            {
                var name = renderer.gameObject.name;
                bool match = false;
                if (category == CustomizationCategory.Wallpaper)
                {
                    match = name.Contains("Wall");
                }
                else if (category == CustomizationCategory.Floor)
                {
                    match = name == "Floor";
                }
                else
                {
                    foreach (var p in patterns) { if (name.StartsWith(p)) { match = true; break; } }
                }
                if (match)
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

        private static string[] ResolveMaterialSwapPatterns(CustomizationCategory category)
        {
            switch (category)
            {
                case CustomizationCategory.Wallpaper:     return new[] { "Wall" };
                case CustomizationCategory.Floor:         return new[] { "Floor" };
                case CustomizationCategory.BookshelfSkin: return new[] { "BookshelfRoot", "Bookshelf_" };
                case CustomizationCategory.Desk:          return new[] { "DeskRoot", "NotebookStation", "Desk_" };
                case CustomizationCategory.DashboardSkin: return new[] { "DashboardScreenPanel", "GuestbookBoard", "Dashboard_" };
                default: return null;
            }
        }

        private Transform FindRoomRoot()
        {
            var root = GameObject.Find("RoomRoot");
            return root != null ? root.transform : null;
        }

        private Light FindFirstDirectionalLight()
        {
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional) return l;
            }
            return null;
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
