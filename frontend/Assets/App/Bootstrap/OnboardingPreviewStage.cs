using System.Collections.Generic;
using App.Config;
using App.Infrastructure.Catalogs;
using BookshelfPorting.Runtime;
using UnityEngine;

namespace App.Bootstrap
{
    // 온보딩 Step2 우측 preview 의 *prefab composition 기반* render stage.
    //
    // MainScene 통째 로드 (SceneFlowManager.GoToMain) 대신 MainSceneCompositionConfig 의 visual prefab
    // 들만 별도 stage root 에 instantiate → 별도 PreviewCamera 가 RenderTexture 로 렌더 → onboarding
    // canvas 의 RawImage 가 그 RT 표시. MainScene 자체 / main camera / scene singleton 흐름과 완전 격리.
    //
    // **RuntimeSystems prefab 제외** (사용자 선택): manager 들 (BookshelfSceneInstaller / BookshelfExperienceManager
    // / RoomEditModeController 등) 의 Awake 가 Auth/Api/Repository 호출 시 onboarding 단계 부팅 미완료
    // crash 회피. BookshelfGenerator 는 BookshelfRigPrefab 안의 component 라 그 prefab instantiate 시
    // 자동 procedural 책장 mesh 생성 (부작용 최소).
    //
    // ApplyTheme: RoomThemeAsset 의 material 필드 (WallMaterial / FloorMaterial / BookshelfMaterial /
    // FurnitureMaterial / DeskTopMaterial / DashboardMaterial) 를 stage root 의 자식 Renderer 에 직접 swap.
    // RoomGameObjectPatterns (Floor / *Wall / BookshelfRoot / etc) 의 GameObject 이름 매칭 — 기존
    // RoomThemeApplier 와 동일 규칙. 단 manager 부재라 RoomThemeApplier 자체는 호출 안 함.
    //
    // Lifecycle:
    //   - Initialize(config): 5 visual prefab instantiate + PreviewCamera + Light + RT
    //   - ApplyTheme / ApplyRandomSelection: material swap (visual skin / deco prefab 은 Phase A2)
    //   - Dispose: stage root + RT 모두 destroy. Step3 진입 또는 Step1 복귀 시 호출.
    internal sealed class OnboardingPreviewStage
    {
        private const int PreviewLayer = 1; // TransparentFX — 다른 카메라 culling 제외용
        // 16:9 landscape RT — 우측 preview 영역 비율 (좌측 UI 28% 후 ~72% 가로 영역). 1:1 정사각형
        // 보다 자연스러운 인테리어 framing + RawImage stretch 시 왜곡 최소.
        private const int RtWidth = 1600;
        private const int RtHeight = 900;
        // origin 근처. float precision 안전. 다른 카메라 cullingMask 가 default 인 environment 도 layer 격리로 차단.
        private static readonly Vector3 StageWorldOrigin = Vector3.zero;

        // **LOCKED** preview camera preset — PreviewStage 전용 fixed room 기준. OverviewAnchor 자동
        // 적용 안 함 (사용자 의도: MainScene anchor 와 PreviewStage 좌표계 충돌 회피).
        private static readonly Vector3 LockedCameraPosition = new Vector3(0f, 1.35f, -4.8f);
        private static readonly Vector3 LockedCameraEulerAngles = new Vector3(6f, 0f, 0f);
        private const float LockedCameraFov = 42f;

        // **Fixed room geometry** — 모든 procedural wall/floor/ceiling 이 이 좌표계 기준. NormalizeRoomRootPivot
        // 이 RoomBackground 의 floor top 을 y=0 으로 맞춰 일관 alignment 보장. Fixed 값이라 wall/ceiling
        // 모두 같은 좌표계에서 정확히 맞물림.
        // **MainScene 방 크기 5m × 5m × 2.8m** — 사용자 명시 (2026-05-13). PreviewStage 의 procedural
        // wall/ceiling 좌표 기준. furniture 위치는 prefab 원본 좌표 유지 — room 만 5x5 box.
        private const float RoomWidth = 5.0f;
        private const float RoomDepth = 5.0f;
        private const float RoomHeight = 2.8f;
        private const float RoomMaxX = RoomWidth * 0.5f;   // 2.5
        private const float RoomMinX = -RoomWidth * 0.5f;  // -2.5
        private const float RoomMaxZ = RoomDepth * 0.5f;   // 2.5
        private const float RoomMinZ = -RoomDepth * 0.5f;  // -2.5
        private const float RoomFloorY = 0f;
        private const float RoomCeilingY = RoomHeight;     // 2.8
        private const float WallCenterY = RoomHeight * 0.5f; // 1.4

        // **Polished furniture target positions** — 책장 화면 중앙, desk 좌측 foreground, dashboard 좌측 wall.
        private static readonly Vector3 BookshelfTargetPos = new Vector3(0.25f, 0f, 1.05f);
        private static readonly Vector3 NotebookTargetPos = new Vector3(-1.45f, 0f, -0.1f);
        // Dashboard 는 wall-mount — bottom 이 아니라 center 기준. 좌측 wall (x=-2.1) 의 안쪽 face 에
        // 부착, mid-height 1.35m.
        private static readonly Vector3 DashboardTargetPos = new Vector3(-2.05f, 1.35f, 0.25f);
        // Bookshelf scale ↑ — 존재감 강화. Dashboard scale ↓ — 과한 크기 축소.
        private const float BookshelfScaleMultiplier = 1.10f;
        private const float DashboardScaleMultiplier = 0.80f;

        private GameObject stageRoot;
        private GameObject previewRoomRoot;
        private GameObject roomBackgroundInstance;
        private GameObject bookshelfRigInstance;
        private GameObject notebookStationInstance;
        private GameObject cameraAnchorsInstance;
        private GameObject dashboardScreenPanelInstance;
        private Camera previewCamera;
        private Light previewLight;       // key light — ApplyTheme 가 InteriorLightColor 로 갱신
        private Light fillLight;          // fill light — cool subtle bounce 시뮬, ApplyTheme 미영향
        private GameObject ceilingInstance; // 천장 quad — open box 느낌 제거용
        private GameObject backWallInstance;  // +Z 방향 back wall — 책장 뒤
        private GameObject leftWallInstance;  // -X 방향 left wall
        private GameObject rightWallInstance; // +X 방향 right wall — 책장 우측
        // 테마별 spawn 된 deco GameObject — ApplyTheme 마다 이전 spawn destroy + 새 spawn.
        // RoomThemeApplier.spawnedThemeDecos 와 동일 정책 (외부 GameObject 영향 없이 본인 spawn 만 추적).
        private readonly List<GameObject> spawnedThemeDecos = new List<GameObject>();

        // **MaterialFactory + BookshelfState** — BookshelfGenerator 가 책장 mesh 생성에 필요.
        // RuntimeSystems 제외 정책으로 default Wire 없음 → BookshelfGenerator.materialFactory=null → 책장
        // mesh 가 회색 default. 우리가 stageRoot 에 동적 add + BookshelfGenerator.Configure 호출 →
        // BookshelfSceneInstaller 와 동일한 방식으로 정상 visual 보장. MainScene 의 RoomThemeApplier 가
        // 호출하는 materialFactory.ApplyTheme(BookshelfThemeStyle) 도 동일하게 ApplyTheme 안에서 호출.
        private MaterialFactory previewMaterialFactory;
        private BookshelfState previewBookshelfState;
        private RenderTexture renderTexture;

        public bool IsReady { get; private set; }
        public RenderTexture RenderTexture => renderTexture;
        // 현재 preview camera 의 world transform 노출 — Inspector override toggle ON 시 자동 sync 용.
        public Vector3 CurrentCameraPosition => previewCamera != null ? previewCamera.transform.position : Vector3.zero;
        public Vector3 CurrentCameraEulerAngles => previewCamera != null ? previewCamera.transform.eulerAngles : Vector3.zero;
        public float CurrentCameraFov => previewCamera != null ? previewCamera.fieldOfView : 42f;

        public bool Initialize(MainSceneCompositionConfig config)
        {
            if (IsReady) return true;
            if (config == null)
            {
                Debug.LogWarning("[OnboardingPreviewStage] Initialize 실패 — MainSceneCompositionConfig null. " +
                                 "LoginScene 의 LoginSceneController GameObject Inspector 에 wire 필요. sprite/procedural fallback.");
                return false;
            }
            try
            {
                Debug.Log("[OnboardingPreviewStage] Initialize prefab preview stage");
                Debug.Log("[OnboardingPreviewStage] MainScene direct preview disabled (사용자 의도)");

                renderTexture = new RenderTexture(RtWidth, RtHeight, 24, RenderTextureFormat.Default)
                {
                    name = "OnboardingPreviewStageRT",
                    antiAliasing = 2,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                if (!renderTexture.Create())
                {
                    Debug.LogWarning("[OnboardingPreviewStage] RenderTexture.Create() returned false — fallback.");
                    Dispose();
                    return false;
                }

                stageRoot = new GameObject("OnboardingThemePreviewStage");
                // **명시 root 분리**: 어떤 부모도 가지면 안 됨 (특히 Onboarding Canvas / PreviewRoomRoot
                // 자식 등). SetParent(null, true) 로 worldPositionStays=true 유지 + 부모 끊기.
                // 후속에 PreviewStage 가 회귀로 Canvas 아래에 들어가는 경우 진단 로그가 잡아냄.
                stageRoot.transform.SetParent(null, true);
                stageRoot.transform.position = StageWorldOrigin;
                stageRoot.transform.localScale = Vector3.one;
                stageRoot.transform.localRotation = Quaternion.identity;
                stageRoot.layer = PreviewLayer;
                Debug.Log(
                    $"[OnboardingPreviewStage] Root parent={(stageRoot.transform.parent != null ? stageRoot.transform.parent.name : "null")} " +
                    $"position={stageRoot.transform.position} layer={LayerMask.LayerToName(stageRoot.layer)}({stageRoot.layer})");

                previewRoomRoot = new GameObject("PreviewRoomRoot");
                previewRoomRoot.transform.SetParent(stageRoot.transform, false);
                previewRoomRoot.layer = PreviewLayer;

                // 5 visual prefab instantiate (RuntimeSystems 제외).
                if (config.RoomBackgroundPrefab != null)
                {
                    roomBackgroundInstance = Object.Instantiate(config.RoomBackgroundPrefab, previewRoomRoot.transform);
                    roomBackgroundInstance.name = "Preview_RoomBackground";
                    SetLayerRecursive(roomBackgroundInstance, PreviewLayer);
                }
                else
                {
                    Debug.LogWarning("[OnboardingPreviewStage] RoomBackgroundPrefab null — wall/floor 안 보임.");
                }
                if (config.BookshelfRigPrefab != null)
                {
                    bookshelfRigInstance = Object.Instantiate(config.BookshelfRigPrefab, previewRoomRoot.transform);
                    bookshelfRigInstance.name = "Preview_BookshelfRig";
                    SetLayerRecursive(bookshelfRigInstance, PreviewLayer);
                    Debug.Log($"[OnboardingPreviewStage] Spawn bookshelf prefab={config.BookshelfRigPrefab.name}");

                    // **BookshelfGenerator wire** — RuntimeSystems 제외 정책으로 default null wire.
                    // stageRoot 에 MaterialFactory + BookshelfState 동적 add → BookshelfGenerator.Configure
                    // → 책장 mesh 가 정상 visual 로 생성. MainScene 의 BookshelfSceneInstaller 동일 패턴.
                    if (previewMaterialFactory == null)
                    {
                        previewMaterialFactory = stageRoot.AddComponent<MaterialFactory>();
                        previewBookshelfState = stageRoot.AddComponent<BookshelfState>();
                        Debug.Log("[OnboardingPreviewStage] MaterialFactory + BookshelfState added to stageRoot");
                    }
                    var generator = bookshelfRigInstance.GetComponentInChildren<BookshelfGenerator>(true);
                    if (generator != null)
                    {
                        generator.Configure(previewBookshelfState, previewMaterialFactory);
                        Debug.Log($"[OnboardingPreviewStage] BookshelfGenerator configured with MaterialFactory (MainScene 방식 동일)");
                    }
                    else
                    {
                        Debug.LogWarning("[OnboardingPreviewStage] BookshelfGenerator not found in BookshelfRig — 책장 mesh 자동 생성 안 됨.");
                    }
                }
                if (config.NotebookStationPrefab != null)
                {
                    notebookStationInstance = Object.Instantiate(config.NotebookStationPrefab, previewRoomRoot.transform);
                    notebookStationInstance.name = "Preview_NotebookStation";
                    SetLayerRecursive(notebookStationInstance, PreviewLayer);
                    Debug.Log($"[OnboardingPreviewStage] Spawn desk/laptop prefab={config.NotebookStationPrefab.name}");
                }
                if (config.CameraAnchorsPrefab != null)
                {
                    cameraAnchorsInstance = Object.Instantiate(config.CameraAnchorsPrefab, previewRoomRoot.transform);
                    cameraAnchorsInstance.name = "Preview_CameraAnchors";
                    SetLayerRecursive(cameraAnchorsInstance, PreviewLayer);
                }
                if (config.DashboardScreenPanelPrefab != null)
                {
                    dashboardScreenPanelInstance = Object.Instantiate(config.DashboardScreenPanelPrefab, previewRoomRoot.transform);
                    dashboardScreenPanelInstance.name = "Preview_DashboardScreenPanel";
                    SetLayerRecursive(dashboardScreenPanelInstance, PreviewLayer);
                    Debug.Log($"[OnboardingPreviewStage] Spawn dashboard prefab={config.DashboardScreenPanelPrefab.name}");
                }

                // Preview-only optimize — prefab 안의 collider / rigidbody / audio / particle / animator disable.
                DisablePreviewOnlyComponents(previewRoomRoot);

                // **핫핑크 fix**: prefab 안의 material 들이 Built-in shader 사용 또는 missing 시 URP 가
                // magenta fallback 렌더 → 우측 preview 가 핫핑크 한 덩어리. 모든 renderer 의 material
                // 검사 + broken 이면 URP/Lit fallback material 로 교체. renderer.materials 사용해 instance
                // clone (sharedMaterial 원본 prefab 오염 회피).
                ValidateAndFallbackMaterials(previewRoomRoot);

                // **사용자 정책 (2026-05-13 최종)**: PreviewStage 는 visual-only preview.
                //   - prefab 의 original scale / transform 유지 (multiplier / reposition 없음)
                //   - 정상 material 보존 (broken shader 만 fallback)
                //   - 5m × 5m × 2.8m room 의 procedural wall/ceiling 만 보충 (back wall 부재 회귀 fix)
                //   - SpawnThemeDecos 의 spec.LocalPosition 은 RoomThemeAsset 원본 데이터 (허용)
                //   - RuntimeSystems / Auth / API / Repository 호출 안 함
                Debug.Log("[OnboardingPreviewStage] Visual-only preview, RuntimeSystems skipped");
                Debug.Log("[OnboardingPreviewStage] Preserve GLB original scale");
                Debug.Log("[OnboardingPreviewStage] Preserve original material");
                Debug.Log($"[OnboardingPreviewStage] Room size applied width={RoomWidth} depth={RoomDepth} height={RoomHeight}");

                // 5m × 5m × 2.8m procedural room shell — back/left/right wall + ceiling + 몰딩.
                // 이전 turn 에서 호출 해제됐던 것을 사용자 명시 요청에 따라 복원. furniture transform 은 안 건드림.
                CreateRoomShell();
                Debug.Log("[OnboardingPreviewStage] Back wall created behind bookshelf");

                // Lighting — RuntimeSystems 제외로 MainScene light 가 없으므로 cozy 2-light preset.
                BuildCozyLightingPreset(stageRoot.transform);
                BuildPreviewCamera(stageRoot.transform);

                if (previewCamera != null)
                {
                    previewCamera.Render();
                }
                IsReady = true;
                Debug.Log("[OnboardingPreviewStage] Initialize OK — preview stage ready.");
                Debug.Log($"[OnboardingPreviewStage] RenderTexture size={RtWidth}x{RtHeight}");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[OnboardingPreviewStage] Initialize 예외 — {ex.GetType().Name}: {ex.Message}. fallback.");
                Dispose();
                return false;
            }
        }

        // ApplyTheme: stage root 자식 Renderer 의 sharedMaterial 을 RoomThemeAsset 의 material 로 swap.
        // GameObject 이름 매칭 (RoomGameObjectPatterns 의 Floor / *Wall / BookshelfRoot 등) — 기존
        // RoomThemeApplier 의 ApplyMaterials 와 동일 규칙. visual skin / deco prefab 은 Phase A2.
        public void ApplyTheme(RoomThemeAsset theme)
        {
            if (!IsReady || theme == null) return;
            Debug.Log($"[OnboardingPreviewStage] Apply theme={theme.name}");

            var renderers = previewRoomRoot.GetComponentsInChildren<Renderer>(true);
            int wallSwap = 0, floorSwap = 0, bookshelfSwap = 0, deskSwap = 0, dashboardSwap = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                var name = r.gameObject.name;

                // **instance copy 사용** (sharedMaterial 직접 swap 하면 원본 prefab asset 오염). r.material
                // 은 첫 slot 의 instance 만 set — multi-slot renderer 도 안전.
                // broken theme material 은 SafeResolveMaterial 가 fallback 으로 자동 교체.
                if (theme.FloorMaterial != null && name.Contains("Floor"))
                {
                    r.material = SafeResolveMaterial(theme.FloorMaterial, PreviewKind.Floor);
                    floorSwap++;
                    continue;
                }
                if (theme.WallMaterial != null && name.Contains("Wall"))
                {
                    r.material = SafeResolveMaterial(theme.WallMaterial, PreviewKind.Wall);
                    wallSwap++;
                    continue;
                }
                if (theme.BookshelfMaterial != null && IsUnderNamedAncestor(r.transform, "BookshelfRoot"))
                {
                    r.material = SafeResolveMaterial(theme.BookshelfMaterial, PreviewKind.Bookshelf);
                    bookshelfSwap++;
                    continue;
                }
                if ((theme.FurnitureMaterial != null || theme.DeskTopMaterial != null) &&
                    (name.Contains("Desk") || name.Contains("Desktop")))
                {
                    var deskMat = theme.DeskTopMaterial != null ? theme.DeskTopMaterial : theme.FurnitureMaterial;
                    r.material = SafeResolveMaterial(deskMat, PreviewKind.Desk);
                    deskSwap++;
                    continue;
                }
                if (theme.DashboardMaterial != null &&
                    (IsUnderNamedAncestor(r.transform, "GuestbookBoard") ||
                     IsUnderNamedAncestor(r.transform, "DashboardScreenPanel")))
                {
                    r.material = SafeResolveMaterial(theme.DashboardMaterial, PreviewKind.Dashboard);
                    dashboardSwap++;
                    continue;
                }
            }
            // light update
            if (previewLight != null)
            {
                var lc = theme.InteriorLightColor;
                if (lc.a < 0.01f) lc = new Color(1f, 0.96f, 0.9f);
                previewLight.color = lc;
                previewLight.intensity = Mathf.Clamp(theme.AmbientIntensity > 0f ? theme.AmbientIntensity * 1.4f : 1.4f, 0.6f, 2.4f);
            }

            // **Theme deco spawn** — bookshelf/floor/wall deco prefab 들 instantiate.
            // 회색 base prefab 만 보이는 회귀 fix — 테마 카드 클릭 시 vintage_shelf / pastel_lamp 등
            // 테마별 실제 visual 자산이 화면에 들어감. RoomThemeApplier.SpawnDecoSpecs 와 동일 로직.
            SpawnThemeDecos(theme);

            if (theme.WallMaterial != null)
                Debug.Log($"[OnboardingPreviewStage] Apply theme wall material={theme.WallMaterial.name}");
            if (theme.FloorMaterial != null)
                Debug.Log($"[OnboardingPreviewStage] Apply theme floor material={theme.FloorMaterial.name}");

            // **MaterialFactory.ApplyTheme(BookshelfThemeStyle)** — MainScene 의 RoomThemeApplier.ApplyMaterials
            // 끝점과 동일. 책장 mesh 의 _BaseColor 가 style 별 tint 로 set 됨.
            if (previewMaterialFactory != null)
            {
                var style = (BookshelfThemeStyle)theme.BookshelfStyleIndex;
                if (System.Enum.IsDefined(typeof(BookshelfThemeStyle), style))
                {
                    previewMaterialFactory.ApplyTheme(style);
                    Debug.Log($"[OnboardingPreviewStage] Apply theme bookshelf style={style} (MaterialFactory.ApplyTheme)");
                }
            }

            if (previewCamera != null) previewCamera.Render();
            Debug.Log(
                $"[OnboardingPreviewStage] Apply theme done — wall={wallSwap} floor={floorSwap} " +
                $"bookshelf={bookshelfSwap} desk={deskSwap} dashboard={dashboardSwap}");

            // 매칭 0 → prefab 안의 renderer 이름이 내 가정 ("Floor"/"Wall"/"BookshelfRoot" 등) 과 다름.
            // 첫 20개 renderer 이름 + parent 이름 출력 → 사용자가 콘솔 보고 실제 이름 알려주면 매칭 규칙 보정.
            if (wallSwap + floorSwap + bookshelfSwap + deskSwap + dashboardSwap == 0 && renderers.Length > 0)
            {
                var sb = new System.Text.StringBuilder("[OnboardingPreviewStage] material swap 0 — renderer 이름 진단 (첫 20개):");
                int count = Mathf.Min(renderers.Length, 20);
                for (int i = 0; i < count; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    var parentName = r.transform.parent != null ? r.transform.parent.gameObject.name : "null";
                    sb.Append("\n  [").Append(i).Append("] ").Append(r.gameObject.name).Append(" (parent=").Append(parentName).Append(")");
                }
                sb.Append("\n  총 renderer 수=").Append(renderers.Length);
                Debug.LogWarning(sb.ToString());
            }
        }

        // ApplyRandomSelection: baseTheme material swap + slot 별 deco. visual skin / slot prefab 은 Phase A2.
        public void ApplyRandomSelection(OnboardingRandomThemeSelection selection)
        {
            if (!IsReady || selection == null || selection.BaseTheme == null) return;
            ApplyTheme(selection.BaseTheme);
            Debug.Log($"[OnboardingPreviewStage] Apply random slots={selection.SelectedSpecs.Count} (base material only, slot prefab spawn Phase A2)");
        }

        // 테마의 deco prefab 들 (bookshelf/floor/wall) 을 previewRoomRoot 자식으로 instantiate.
        // 이전 테마의 spawn 은 ClearSpawnedThemeDecos 로 먼저 destroy → 새 spec 들 spawn.
        //
        // previewPrefab 우선 (lightweight LOD), 없으면 spec.Prefab fallback. RoomThemeApplier 의
        // SpawnDecoSpecs 와 동일 좌표계 (spec.LocalPosition/Rotation/Scale 그대로 적용) —
        // MainScene 의 themeDecoRoot 가 roomRoot 의 자식이고, 우리 previewRoomRoot 가 같은 역할.
        //
        // wall deco 도 wall surface anchor 변환 없이 단순 local 좌표 사용 — preview 라 정확한
        // wall mount 매칭보다 visual 표현이 우선. WallSide.None 인 deco 와 동일하게 처리.
        private void SpawnThemeDecos(RoomThemeAsset theme)
        {
            ClearSpawnedThemeDecos();
            if (theme == null || previewRoomRoot == null) return;

            int spawned = 0;
            spawned += SpawnSpecList(theme.BookshelfDecoSpecs, "bookshelf");
            spawned += SpawnSpecList(theme.FloorDecoSpecs, "floor");
            spawned += SpawnSpecList(theme.WallDecoSpecs, "wall");

            int total = (theme.BookshelfDecoSpecs?.Count ?? 0)
                        + (theme.FloorDecoSpecs?.Count ?? 0)
                        + (theme.WallDecoSpecs?.Count ?? 0);
            Debug.Log($"[OnboardingPreviewStage] Theme deco spawned count={spawned}/{total} (theme={theme.name})");
        }

        private int SpawnSpecList(IReadOnlyList<RoomThemeDecoSpec> specs, string label)
        {
            if (specs == null) return 0;
            int n = 0;
            for (int i = 0; i < specs.Count; i++)
            {
                var spec = specs[i];
                if (spec == null) continue;
                // previewPrefab 우선 (LOD/trimmed 버전), 없으면 본 prefab.
                var prefab = spec.PreviewPrefab != null ? spec.PreviewPrefab : spec.Prefab;
                if (prefab == null) continue;
                var go = Object.Instantiate(prefab, previewRoomRoot.transform);
                // **GLB 원본 transform 유지** — spec.LocalPosition/Rotation/Scale = RoomThemeAsset 원본 데이터.
                go.transform.localPosition = spec.LocalPosition;
                go.transform.localEulerAngles = spec.LocalRotation;
                go.transform.localScale = spec.LocalScale;
                SetLayerRecursive(go, PreviewLayer);
                DisablePreviewOnlyComponents(go);
                spawnedThemeDecos.Add(go);
                // 슬롯별 로그 — bookshelf/desk/dashboard 매핑 추적.
                if (spec.ThemeSlot == ThemeSlot.Shelf)
                {
                    Debug.Log($"[OnboardingPreviewStage] Apply theme bookshelf prefab={prefab.name}");
                }
                else if (spec.ThemeSlot == ThemeSlot.Table || spec.ThemeSlot == ThemeSlot.TableMini)
                {
                    Debug.Log($"[OnboardingPreviewStage] Apply theme desk prefab={prefab.name}");
                }
                else if (spec.ThemeSlot == ThemeSlot.Dashboard)
                {
                    Debug.Log($"[OnboardingPreviewStage] Apply theme dashboard prefab={prefab.name}");
                }
                n++;
            }
            if (n > 0) Debug.Log($"[OnboardingPreviewStage] Spawn {label} deco count={n}");
            return n;
        }

        private void ClearSpawnedThemeDecos()
        {
            for (int i = spawnedThemeDecos.Count - 1; i >= 0; i--)
            {
                var go = spawnedThemeDecos[i];
                if (go == null) continue;
                go.SetActive(false);
                Object.Destroy(go);
            }
            spawnedThemeDecos.Clear();
        }

        // 수동 카메라 override — auto-fit 결과 무시하고 Inspector 값 그대로 적용.
        //
        // LoginSceneController.OverridePreviewCamera=true 일 때 호출됨. position/euler 은 world space.
        // RenderTexture 즉시 갱신 위해 previewCamera.Render() 도 호출.
        public void OverrideCameraTransform(Vector3 worldPos, Vector3 eulerAngles, float fov)
        {
            if (!IsReady || previewCamera == null) return;
            previewCamera.transform.position = worldPos;
            previewCamera.transform.eulerAngles = eulerAngles;
            previewCamera.fieldOfView = Mathf.Clamp(fov, 5f, 120f);
            previewCamera.Render();
            Debug.Log(
                $"[OnboardingPreviewStage] Camera manual override applied " +
                $"position={worldPos} euler={eulerAngles} fov={previewCamera.fieldOfView}");
        }

        public void Dispose()
        {
            // 명시적 destroy 안 해도 stageRoot.Destroy 가 자식 cascade 처리하지만, list 비워 두면
            // dispose 후 race 로 ApplyTheme 가 다시 호출되는 회귀에 안전.
            ClearSpawnedThemeDecos();
            if (renderTexture != null)
            {
                renderTexture.Release();
                Object.Destroy(renderTexture);
                renderTexture = null;
            }
            if (stageRoot != null)
            {
                Object.Destroy(stageRoot);
                stageRoot = null;
            }
            previewRoomRoot = null;
            roomBackgroundInstance = null;
            bookshelfRigInstance = null;
            notebookStationInstance = null;
            cameraAnchorsInstance = null;
            dashboardScreenPanelInstance = null;
            ceilingInstance = null;
            backWallInstance = null;
            leftWallInstance = null;
            rightWallInstance = null;
            previewMaterialFactory = null;
            previewBookshelfState = null;
            previewCamera = null;
            previewLight = null;
            fillLight = null;
            IsReady = false;
            Debug.Log("[OnboardingPreviewStage] Dispose");
        }

        // Room root pivot 정규화 — 사용자 보고 "room 이 공중에 떠 있는 miniature box" 회귀 fix.
        //
        // 원인: prefab 들은 MainScene 의 world 좌표에서 추출된 transform 을 가짐. Instantiate(prefab, parent)
        // 가 prefab.transform 의 localPosition 을 그대로 사용 → instance 의 localPosition 이 비-zero. 결과적으로
        // previewRoomRoot 의 자식 (=room geometry) 들이 world origin 에서 멀리 떨어진 위치에 배치됨.
        // camera 가 world origin 근처를 기준으로 fit 시도하면 "방이 멀리 떠 있는" 형국.
        //
        // 처리: previewRoomRoot.position 을 보정해
        //   - floor top y → world y=0 (furniture 가 정확히 floor 위)
        //   - room center (x, z) → world (0, 0) (lateral 정렬)
        // 가 되도록 한다. prefab 의 내부 layout (책장/책상의 상대 위치) 은 그대로 유지.
        private void NormalizeRoomRootPivot()
        {
            if (previewRoomRoot == null) return;

            var renderers = previewRoomRoot.GetComponentsInChildren<Renderer>(true);
            Bounds? roomBounds = null;
            Bounds? floorBounds = null;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var b = r.bounds;
                if (b.size.sqrMagnitude < 1e-6f) continue;
                if (roomBounds == null) roomBounds = b;
                else { var rb = roomBounds.Value; rb.Encapsulate(b); roomBounds = rb; }

                if (ClassifyRenderer(r) == PreviewKind.Floor)
                {
                    if (floorBounds == null) floorBounds = b;
                    else { var fb = floorBounds.Value; fb.Encapsulate(b); floorBounds = fb; }
                }
            }

            if (!roomBounds.HasValue)
            {
                Debug.LogWarning("[OnboardingPreviewStage] Room root pivot normalize skip — no valid renderers.");
                return;
            }

            // Floor top y — furniture 가 앉을 평면. Floor classifier 매칭이면 max.y, 아니면 room.min.y fallback.
            float floorTopY;
            if (floorBounds.HasValue)
            {
                floorTopY = floorBounds.Value.max.y;
            }
            else
            {
                floorTopY = roomBounds.Value.min.y;
                Debug.LogWarning(
                    $"[OnboardingPreviewStage] No Floor classified — fallback roomBounds.min.y={floorTopY:F3} for floor top y.");
            }

            var rb_ = roomBounds.Value;
            var offset = new Vector3(-rb_.center.x, -floorTopY, -rb_.center.z);
            previewRoomRoot.transform.position += offset;

            Debug.Log("[OnboardingPreviewStage] Room root normalized");
            Debug.Log($"[OnboardingPreviewStage] Room root offset={offset}");
            Debug.Log($"[OnboardingPreviewStage] Floor aligned y=0 (was floorTopY={floorTopY:F3})");
            Debug.Log($"[OnboardingPreviewStage] Room scale normalized — roomBounds.size={rb_.size}");
        }

        // Furniture 를 fixed target 좌표로 재배치. bounds bottom-center 가 target 에 오도록 offset 계산.
        // Bookshelf / NotebookStation 은 floor-grounded (target.y = 0, instance bottom = 0). Dashboard 는
        // wall-mount 이므로 instance.position 을 target 으로 직접 set (rotation 도 좌측 wall 향하게 90°).
        private void RepositionFurnitureToTargetPositions()
        {
            // Bookshelf — scale ↑ 후 bottom-center snap. scale 먼저 → 변경된 bounds 기준으로 snap.
            if (bookshelfRigInstance != null)
            {
                bookshelfRigInstance.transform.localScale = Vector3.one * BookshelfScaleMultiplier;
            }
            SnapInstanceBottomCenterTo(bookshelfRigInstance, BookshelfTargetPos, "Bookshelf polished");

            // Desk/Notebook — 좌측 foreground, 살짝 안쪽으로 이동해 잘림 회피.
            SnapInstanceBottomCenterTo(notebookStationInstance, NotebookTargetPos, "Desk polished");

            // Dashboard — 좌측 wall mount. scale ↓ + 90° 회전.
            if (dashboardScreenPanelInstance != null)
            {
                dashboardScreenPanelInstance.transform.localScale = Vector3.one * DashboardScaleMultiplier;
                dashboardScreenPanelInstance.transform.position = DashboardTargetPos;
                dashboardScreenPanelInstance.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                Debug.Log(
                    $"[OnboardingPreviewStage] Dashboard polished target={DashboardTargetPos} " +
                    $"scale={DashboardScaleMultiplier} rotation=(0, 90, 0)");
            }

            // Rug — 사용자 명시 target (0.25, 0.01, -0.55). RoomBackground prefab 의 내부 element 라
            // 별도 instance 없으면 skip. "Rug" 또는 "Carpet" 이름의 renderer 가 있으면 reposition.
            TryRepositionInternalElement("Rug", new Vector3(0.25f, 0.01f, -0.55f));

            Debug.Log("[OnboardingPreviewStage] Final composition polished");
        }

        // RoomBackground prefab 안의 특정 이름 element 를 찾아 reposition. element 의 transform.position 을
        // world target 으로 직접 set. 못 찾으면 no-op (rug 가 prefab 의 mesh 일부일 수도 있어 안전 skip).
        private void TryRepositionInternalElement(string nameContains, Vector3 worldTarget)
        {
            if (previewRoomRoot == null) return;
            var allTransforms = previewRoomRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                var t = allTransforms[i];
                if (t == null) continue;
                if (!t.gameObject.name.Contains(nameContains, System.StringComparison.OrdinalIgnoreCase)) continue;
                t.position = worldTarget;
                Debug.Log($"[OnboardingPreviewStage] {nameContains} polished target={worldTarget} (matched={t.gameObject.name})");
                return;
            }
            Debug.Log($"[OnboardingPreviewStage] {nameContains} polished skip — no matching GameObject in prefab.");
        }

        // instance 의 renderer bounds bottom-center 를 target 위치로 이동. 가구가 floor 위에 grounded
        // 되도록 보장. target.y 가 0 이면 instance bottom = world y 0 = floor surface.
        private void SnapInstanceBottomCenterTo(GameObject inst, Vector3 target, string label)
        {
            if (inst == null) return;
            var b = ComputeRenderersBounds(inst);
            if (!b.HasValue) return;
            var currentBottomCenter = new Vector3(b.Value.center.x, b.Value.min.y, b.Value.center.z);
            var offset = target - currentBottomCenter;
            inst.transform.position += offset;
            Debug.Log($"[OnboardingPreviewStage] Furniture repositioned {label} target={target} offset={offset}");
        }

        // RoomBackground prefab 의 기존 wall renderer (PreviewKind.Wall) 를 hide — procedural wall 과
        // 중복 / z-fight / "분리된 판처럼 보임" 회귀 방지. Floor / Bookshelf / Desk / Dashboard 는 보존.
        private void HideExistingRedundantWalls()
        {
            if (previewRoomRoot == null) return;
            var renderers = previewRoomRoot.GetComponentsInChildren<Renderer>(true);
            int floorCount = 0;
            int wallCount = 0;
            int hiddenWalls = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                var kind = ClassifyRenderer(r);
                if (kind == PreviewKind.Floor) floorCount++;
                else if (kind == PreviewKind.Wall)
                {
                    wallCount++;
                    if (r.enabled)
                    {
                        r.enabled = false;
                        hiddenWalls++;
                    }
                }
            }
            Debug.Log($"[OnboardingPreviewStage] Existing room surfaces detected floor={floorCount} wall={wallCount}");
            Debug.Log($"[OnboardingPreviewStage] Hidden existing walls count={hiddenWalls}");
        }

        // Room shell — fixed coordinate procedural surfaces (back/left/right wall + ceiling + ceiling molding).
        // 모든 surface 가 같은 좌표계 (RoomMin/Max constants) 기준이라 wall/ceiling/floor 가 정확히 맞물림.
        // stageRoot 의 자식 (world origin 기준 local = world). previewRoomRoot 의 normalize offset 과 독립.
        private void CreateRoomShell()
        {
            if (stageRoot == null) return;
            Debug.Log("[OnboardingPreviewStage] PreviewStage coordinate system locked");
            Debug.Log($"[OnboardingPreviewStage] Room size width={RoomWidth} depth={RoomDepth} height={RoomHeight}");

            // **Warm cream wallpaper tone** — user-spec polish 값 (회색/파랑 회귀 fix).
            var wallColor = new Color(0.93f, 0.88f, 0.78f);
            var ceilingColor = new Color(0.97f, 0.94f, 0.88f);
            var moldingColor = new Color(0.98f, 0.95f, 0.90f);
            Debug.Log("[OnboardingPreviewStage] Warm wall material applied");

            // Back wall (+Z, normal -Z 향함 — 카메라 -Z 에서 visible)
            backWallInstance = BuildProceduralQuadSurface(
                "Preview_BackWall",
                new Vector3(0f, WallCenterY, RoomMaxZ),
                Quaternion.Euler(0f, 180f, 0f),
                new Vector3(RoomWidth, RoomHeight, 1f),
                wallColor);
            Debug.Log("[OnboardingPreviewStage] Back wall created");

            // Left wall (-X, normal +X 향함 — room 내부 visible)
            leftWallInstance = BuildProceduralQuadSurface(
                "Preview_LeftWall",
                new Vector3(RoomMinX, WallCenterY, 0f),
                Quaternion.Euler(0f, 90f, 0f),
                new Vector3(RoomDepth, RoomHeight, 1f),
                wallColor);
            Debug.Log("[OnboardingPreviewStage] Left wall created");

            // Right wall (+X, normal -X 향함 — room 내부 visible)
            rightWallInstance = BuildProceduralQuadSurface(
                "Preview_RightWall",
                new Vector3(RoomMaxX, WallCenterY, 0f),
                Quaternion.Euler(0f, -90f, 0f),
                new Vector3(RoomDepth, RoomHeight, 1f),
                wallColor);
            Debug.Log("[OnboardingPreviewStage] Right wall created");

            // Ceiling (+Y, normal -Y 향함 — camera below visible). **double-sided material** 사용으로
            // 만약 카메라가 천장 위로 가는 회귀가 있어도 ceiling 이 invisible 회귀 방지.
            ceilingInstance = BuildProceduralQuadSurface(
                "Preview_Ceiling",
                new Vector3(0f, RoomCeilingY, 0f),
                Quaternion.Euler(90f, 0f, 0f),
                new Vector3(RoomWidth, RoomDepth, 1f),
                ceilingColor);
            Debug.Log(
                $"[OnboardingPreviewStage] Ceiling created position=(0, {RoomCeilingY:F2}, 0) " +
                $"scale=({RoomWidth:F2}, {RoomDepth:F2}) visibleFromBelow=true");

            // Ceiling molding — 천장-벽 모서리에 white trim cube (3개 변).
            CreateCeilingMoldingFixed(moldingColor);

            Debug.Log(
                "[OnboardingPreviewStage] Procedural surfaces created back=true left=true right=true ceiling=true");
            Debug.Log("[OnboardingPreviewStage] Room structure sealed");
        }

        // Procedural quad surface — wall/ceiling 공통 builder. stageRoot 자식 (world origin 기준).
        // double-sided material 사용 — normal 방향 잘못 set 해도 양쪽 visible.
        private GameObject BuildProceduralQuadSurface(string name, Vector3 worldPos, Quaternion worldRot, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.layer = PreviewLayer;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(stageRoot.transform, false);
            // stageRoot 가 world origin 이라 local = world. 명시적으로 set.
            go.transform.localPosition = worldPos;
            go.transform.localRotation = worldRot;
            go.transform.localScale = scale;

            var rend = go.GetComponent<MeshRenderer>();
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = true;
            rend.material = CreateUrpLitMaterialDoubleSided(name + "_Mat", color);
            return go;
        }

        // URP/Lit material 의 culling 을 None (double-sided) 으로 set.
        //   _Cull 0 = Off (double-sided), 1 = Front, 2 = Back (default)
        // URP shader 의 표면 normal 방향과 무관하게 양면 모두 렌더 — wall/ceiling 의 normal 실수 시 안전망.
        private static Material CreateUrpLitMaterialDoubleSided(string matName, Color color)
        {
            var mat = CreateUrpLitMaterial(matName, color);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            if (mat.HasProperty("_CullMode")) mat.SetFloat("_CullMode", 0f);
            mat.doubleSidedGI = true;
            return mat;
        }

        // 천장-벽 모서리 white 몰딩 trim — fixed coordinate 좌표 사용. back/left/right 3변.
        private void CreateCeilingMoldingFixed(Color moldingColor)
        {
            float trimY = RoomCeilingY - 0.05f;
            float trimHeight = 0.10f;
            float trimThickness = 0.06f;

            BuildMoldingCube(
                "Preview_CeilingMolding_Back",
                new Vector3(0f, trimY, RoomMaxZ),
                new Vector3(RoomWidth, trimHeight, trimThickness),
                moldingColor);
            BuildMoldingCube(
                "Preview_CeilingMolding_Right",
                new Vector3(RoomMaxX, trimY, 0f),
                new Vector3(trimThickness, trimHeight, RoomDepth),
                moldingColor);
            BuildMoldingCube(
                "Preview_CeilingMolding_Left",
                new Vector3(RoomMinX, trimY, 0f),
                new Vector3(trimThickness, trimHeight, RoomDepth),
                moldingColor);

            Debug.Log("[OnboardingPreviewStage] Ceiling molding created");
        }

        private void BuildMoldingCube(string name, Vector3 worldPos, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = PreviewLayer;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(stageRoot.transform, false);
            go.transform.localPosition = worldPos;
            go.transform.localScale = size;
            var rend = go.GetComponent<MeshRenderer>();
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = true;
            rend.material = CreateUrpLitMaterial(name + "_Mat", color);
        }

        // 단일 root 의 자식 renderer bounds 합산 — NormalizeRoomScale 등이 재사용.
        private static Bounds? ComputeRenderersBounds(GameObject root)
        {
            if (root == null) return null;
            var rs = root.GetComponentsInChildren<Renderer>(true);
            Bounds? combined = null;
            for (int i = 0; i < rs.Length; i++)
            {
                var r = rs[i];
                if (r == null || !r.enabled) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var b = r.bounds;
                if (b.size.sqrMagnitude < 1e-6f) continue;
                if (combined == null) combined = b;
                else { var cv = combined.Value; cv.Encapsulate(b); combined = cv; }
            }
            return combined;
        }

        private void BuildPreviewCamera(Transform parent)
        {
            var camGo = new GameObject("OnboardingPreviewStageCamera");
            camGo.transform.SetParent(parent, false);
            camGo.layer = PreviewLayer;

            previewCamera = camGo.AddComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.93f, 0.89f, 0.82f, 1f); // cream cozy
            int previewMask = 1 << PreviewLayer;
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) previewMask &= ~(1 << uiLayer);
            previewCamera.cullingMask = previewMask;
            previewCamera.orthographic = false;
            previewCamera.fieldOfView = LockedCameraFov;
            previewCamera.nearClipPlane = 0.1f;
            previewCamera.farClipPlane = 100f;
            previewCamera.targetTexture = renderTexture;
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = true;
            previewCamera.enabled = true;

            // **MainScene 기본 시점 (FrontalAnchor) 우선** — CameraAnchorsPrefab 안의 "FrontalAnchor" Transform 사용.
            // MainScene 의 CameraController.MainView (TryComputeMainViewPose) 와 동일 — 책장 벽면 정면에서 바라보는 시점.
            // anchor 못 찾으면 LOCKED 값 fallback.
            if (TryApplyFrontalAnchorPose(camGo.transform, out var anchorFov))
            {
                previewCamera.fieldOfView = anchorFov;
                Debug.Log(
                    $"[OnboardingPreviewStage] Camera applied MainScene FrontalAnchor pose — " +
                    $"position={camGo.transform.position} euler={camGo.transform.eulerAngles} fov={anchorFov}");
            }
            else
            {
                camGo.transform.position = LockedCameraPosition;
                camGo.transform.eulerAngles = LockedCameraEulerAngles;
                previewCamera.fieldOfView = LockedCameraFov;
                Debug.LogWarning(
                    "[OnboardingPreviewStage] FrontalAnchor not found in CameraAnchorsPrefab — fallback to LOCKED values. " +
                    "CameraAnchorsPrefab wire 확인 필요.");
            }

            bool uiExcluded = uiLayer < 0 || (previewCamera.cullingMask & (1 << uiLayer)) == 0;
            Debug.Log(
                $"[OnboardingPreviewStage] Final camera position={camGo.transform.position} " +
                $"euler={camGo.transform.eulerAngles} fov={previewCamera.fieldOfView}");
            Debug.Log(
                $"[OnboardingPreviewStage] Camera cullingMask=0x{previewCamera.cullingMask:X} " +
                $"(PreviewLayer={LayerMask.LayerToName(PreviewLayer)}/{PreviewLayer}, uiLayer={uiLayer})");
            Debug.Log($"[OnboardingPreviewStage] UI layer excluded={uiExcluded}");
        }

        // CameraAnchorsPrefab 안의 FrontalAnchor 자식 찾아 카메라 transform 에 적용.
        // FrontalAnchor 의 local position (0, 0.95, -2.05) + identity-ish rotation (pitch ≈ 1.76°) —
        // 책장 벽면 (+Z wall) 을 정면에서 바라보는 MainScene 의 MainView pose.
        // fov 는 CameraController 의 mainViewFieldOfView default (52f).
        private bool TryApplyFrontalAnchorPose(Transform cameraTransform, out float fov)
        {
            fov = 52f; // MainScene CameraController.mainViewFieldOfView default
            if (cameraAnchorsInstance == null) return false;
            var anchorXform = FindChildRecursive(cameraAnchorsInstance.transform, "FrontalAnchor");
            if (anchorXform == null) return false;
            cameraTransform.position = anchorXform.position;
            cameraTransform.rotation = anchorXform.rotation;
            return true;
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindChildRecursive(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        // Cozy lighting preset — 2 directional light (key + fill).
        //
        // Key (warm 1.0/0.93/0.80, intensity 1.2): upper-front-right 에서 비치는 따뜻한 indoor light.
        //   ApplyTheme 가 theme.InteriorLightColor 로 color/intensity 갱신 — `previewLight` 별칭 유지.
        // Fill (cool 0.82/0.88/1.0, intensity 0.42, no shadow): 반대편에서 비추는 subtle bounce 보정.
        //   너무 밝지 않게, 따뜻한 key 와 cool 대비로 cozy 인테리어 느낌. 그림자 없음 — 강한 직사광이 아님.
        // 그림자 strength 0.55 → 0.32 로 softening — 실내 햇빛 느낌 (preview/debug hard shadow 제거).
        private void BuildCozyLightingPreset(Transform parent)
        {
            // Key light — warm
            var keyGo = new GameObject("OnboardingPreviewStage_KeyLight");
            keyGo.transform.SetParent(parent, false);
            keyGo.transform.localPosition = new Vector3(2f, 4f, -2f);
            keyGo.transform.localEulerAngles = new Vector3(38f, -28f, 0f);
            keyGo.layer = PreviewLayer;

            previewLight = keyGo.AddComponent<Light>();
            previewLight.type = LightType.Directional;
            previewLight.color = new Color(1f, 0.94f, 0.84f);
            // intensity 살짝 ↓ (1.0 → 0.9) — 천장/벽 washout 회피. shadow strength ↓ (0.22 → 0.18) — 더 soft.
            previewLight.intensity = 0.9f;
            previewLight.cullingMask = ~0; // URP layer light culling 미동작 가드
            previewLight.shadows = LightShadows.Soft;
            previewLight.shadowStrength = 0.18f;
            previewLight.shadowBias = 0.05f;

            // Fill light — **warm neutral** (cool blue 제거). 회색/파랑 wall 회귀 fix.
            var fillGo = new GameObject("OnboardingPreviewStage_FillLight");
            fillGo.transform.SetParent(parent, false);
            fillGo.transform.localPosition = new Vector3(-2f, 2.5f, 2f);
            fillGo.transform.localEulerAngles = new Vector3(20f, 155f, 0f);
            fillGo.layer = PreviewLayer;

            fillLight = fillGo.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            // (0.88, 0.92, 1.0) cool blue → (0.96, 0.93, 0.85) warm neutral. wall 의 reflection 톤 통일.
            fillLight.color = new Color(0.96f, 0.93f, 0.85f);
            fillLight.intensity = 0.45f;
            fillLight.cullingMask = ~0;
            fillLight.shadows = LightShadows.None;

            Debug.Log("[OnboardingPreviewStage] Cozy lighting preset applied");
        }

        // theme material 이 broken (Built-in Standard / missing shader) 이면 fallback 사용.
        // theme material 이 정상이면 그대로 swap (단 instance copy 는 호출자가 r.material 로 처리).
        private Material SafeResolveMaterial(Material themeMat, PreviewKind kind)
        {
            if (IsBrokenMaterial(themeMat))
            {
                Debug.LogWarning(
                    $"[OnboardingPreviewStage] theme material broken (shader={(themeMat != null && themeMat.shader != null ? themeMat.shader.name : "null")}) " +
                    $"kind={kind} — fallback material 사용.");
                return GetFallbackMaterial(kind);
            }
            return themeMat;
        }

        // prefab 안 renderer 의 material 검사 + broken 이면 URP/Lit fallback 교체. instance copy 만
        // 사용해 prefab asset 원본 오염 0. renderer.materials get 자체가 clone 생성 비용 있지만 1회만.
        private void ValidateAndFallbackMaterials(GameObject root)
        {
            if (root == null) return;
            Debug.Log("[OnboardingPreviewStage] Material validation start");
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            int brokenCount = 0;
            int replacedCount = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                if (r is ParticleSystemRenderer) continue;
                var shareds = r.sharedMaterials;
                bool needReplace = false;
                for (int s = 0; s < shareds.Length; s++)
                {
                    if (IsBrokenMaterial(shareds[s]))
                    {
                        brokenCount++;
                        needReplace = true;
                        var matName = shareds[s] != null ? shareds[s].name : "null";
                        var shaderName = shareds[s] != null && shareds[s].shader != null ? shareds[s].shader.name : "null";
                        Debug.Log(
                            $"[OnboardingPreviewStage] Broken material detected renderer={r.gameObject.name} " +
                            $"material={matName} shader={shaderName}");
                    }
                }
                if (!needReplace) continue;

                // renderer.materials get → instance clone. 그 중 broken 만 fallback 으로 교체 + set.
                var instances = r.materials;
                for (int s = 0; s < instances.Length; s++)
                {
                    if (IsBrokenMaterial(instances[s]))
                    {
                        var kind = ClassifyRenderer(r);
                        instances[s] = GetFallbackMaterial(kind);
                        replacedCount++;
                        Debug.Log($"[OnboardingPreviewStage] Apply fallback material kind={kind} renderer={r.gameObject.name}");
                    }
                }
                r.materials = instances;
            }
            // 정책: 정상 material 은 보존, broken shader (null/Standard/Hidden/InternalErrorShader/Legacy) 만 fallback.
            // fallback 색상도 카테고리별 warm tone (cream/brown/wood/navy/beige) — 회색 X.
            int preservedCount = renderers.Length - brokenCount;
            Debug.Log($"[OnboardingPreviewStage] Preserve original material count={preservedCount} (정상 material 보존)");
            Debug.Log($"[OnboardingPreviewStage] Fallback material only for broken shader count={replacedCount}");
            Debug.Log($"[OnboardingPreviewStage] Pink/invalid material count={brokenCount} replaced={replacedCount}");
        }

        private static bool IsBrokenMaterial(Material mat)
        {
            if (mat == null) return true;
            if (mat.shader == null) return true;
            var name = mat.shader.name;
            // Unity 의 missing shader fallback 들 (magenta 렌더 원인).
            if (string.IsNullOrEmpty(name)) return true;
            if (name == "Hidden/InternalErrorShader") return true;
            if (name.StartsWith("Hidden/") && name.Contains("Error")) return true;
            // Built-in pipeline shader 들 — URP 에서 magenta 렌더. URP/Lit, URP/Unlit, URP/SimpleLit
            // 등 URP 계열은 OK. Standard / Legacy 는 broken 처리.
            if (name == "Standard") return true;
            if (name.StartsWith("Legacy Shaders/")) return true;
            return false;
        }

        // Renderer 가 어떤 카테고리 (Wall/Floor/Bookshelf/Desk/Dashboard/Other) 의 부분인지 결정 — fallback
        // material 색상 매핑용. parent chain 이름 기반.
        private enum PreviewKind { Wall, Floor, Bookshelf, Desk, Dashboard, Other }
        private static PreviewKind ClassifyRenderer(Renderer r)
        {
            if (r == null) return PreviewKind.Other;
            var name = r.gameObject.name;
            // walk parent chain — 어느 큰 그룹에 속하는지.
            var t = r.transform;
            while (t != null)
            {
                var n = t.gameObject.name;
                if (n.Contains("Wall")) return PreviewKind.Wall;
                if (n.Contains("Floor")) return PreviewKind.Floor;
                if (n.Contains("Bookshelf") || n == "BookshelfRoot") return PreviewKind.Bookshelf;
                if (n.Contains("Desk") || n.Contains("Notebook") || n.Contains("Laptop")) return PreviewKind.Desk;
                if (n.Contains("Dashboard") || n.Contains("Guestbook")) return PreviewKind.Dashboard;
                t = t.parent;
            }
            // 자기 자신 이름 fallback
            if (name.Contains("Wall")) return PreviewKind.Wall;
            if (name.Contains("Floor")) return PreviewKind.Floor;
            return PreviewKind.Other;
        }

        private readonly Dictionary<PreviewKind, Material> fallbackMaterialCache = new Dictionary<PreviewKind, Material>();
        private Material GetFallbackMaterial(PreviewKind kind)
        {
            if (fallbackMaterialCache.TryGetValue(kind, out var cached) && cached != null) return cached;
            Color c;
            switch (kind)
            {
                case PreviewKind.Wall: c = new Color(0.92f, 0.86f, 0.74f); break; // warm cream
                case PreviewKind.Floor: c = new Color(0.55f, 0.40f, 0.30f); break; // warm wood brown
                case PreviewKind.Bookshelf: c = new Color(0.85f, 0.78f, 0.66f); break; // off-white / light wood
                case PreviewKind.Desk: c = new Color(0.70f, 0.55f, 0.40f); break; // light wood
                case PreviewKind.Dashboard: c = new Color(0.25f, 0.30f, 0.40f); break; // muted navy
                default: c = new Color(0.80f, 0.75f, 0.68f); break; // neutral beige
            }
            var mat = CreateUrpLitMaterial($"OnboardingPreviewFallback_{kind}", c);
            fallbackMaterialCache[kind] = mat;
            return mat;
        }

        private static Material CreateUrpLitMaterial(string matName, Color color)
        {
            // URP/Lit 우선. URP project 정상이면 정확한 shader. 못 찾으면 Standard, Diffuse 순 fallback.
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Hidden/Internal-Colored")
                         ?? Shader.Find("Diffuse");
            var mat = new Material(shader) { name = matName };
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            return mat;
        }

        // Furniture bounds 기반 camera auto-fit.
        //
        // 기존 문제: room 전체 (wall/floor 포함) bounds 가 framing 기준 → wall/floor 가 furniture 보다
        // 훨씬 크므로 distance 가 비현실적으로 멀어지고 pitch 도 wrong sign 이라 카메라가 floor 아래로
        // 내려가서 위에서 내려다보는 게 아니라 천장 쪽을 비스듬히 보는 형국. 책장/책상/대시보드가 작게
        // 떠 있고 wall/floor 가 dominate.
        //
        // 새 알고리즘:
        //   1. renderer 들을 Wall/Floor vs Furniture (Bookshelf/Desk/Dashboard/Other) 로 ClassifyRenderer 로 분류
        //   2. furnitureBounds 만으로 distance/lookAt 결정 (wall/floor 는 제외)
        //   3. roomBounds 는 far clip / debug log 용으로만 사용
        //   4. pitch 정의를 "카메라 elevation 위 target 위에서 내려다봄" 으로 명확화 (dir.y = -sin(pitch))
        //   5. left-UI 28% 보정은 yaw 부호에 의존하지 않게 camera.right 기반 명시 shift 사용
        private void FitCameraToCombinedBounds()
        {
            if (previewCamera == null || previewRoomRoot == null) return;
            var renderers = previewRoomRoot.GetComponentsInChildren<Renderer>(true);

            Bounds? furnitureBounds = null;
            Bounds? roomBounds = null;
            int excludedCount = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                // ceiling 은 framing 에 영향 0 — 단순 background plane.
                if (ceilingInstance != null && r.gameObject == ceilingInstance) continue;
                var b = r.bounds;
                if (b.size.sqrMagnitude < 1e-6f) continue;

                // roomBounds — 전체. far clip / debug 용.
                if (roomBounds == null) roomBounds = b;
                else { var rb = roomBounds.Value; rb.Encapsulate(b); roomBounds = rb; }

                // furnitureBounds — Wall/Floor 제외. camera framing 의 실제 기준.
                var kind = ClassifyRenderer(r);
                if (kind == PreviewKind.Wall || kind == PreviewKind.Floor)
                {
                    excludedCount++;
                    continue;
                }
                if (furnitureBounds == null) furnitureBounds = b;
                else { var fb = furnitureBounds.Value; fb.Encapsulate(b); furnitureBounds = fb; }
            }

            if (!furnitureBounds.HasValue)
            {
                if (!roomBounds.HasValue)
                {
                    Debug.LogWarning("[OnboardingPreviewStageCamera] no valid renderers — camera fit skip.");
                    return;
                }
                Debug.LogWarning("[OnboardingPreviewStageCamera] no furniture renderer (Bookshelf/Desk/Dashboard/Other) — fallback roomBounds.");
                furnitureBounds = roomBounds;
            }

            var furnCenter = furnitureBounds.Value.center;
            var furnSize = furnitureBounds.Value.size;
            var roomCenter = roomBounds.HasValue ? roomBounds.Value.center : furnCenter;
            var roomSize = roomBounds.HasValue ? roomBounds.Value.size : furnSize;

            Debug.Log($"[OnboardingPreviewStageCamera] furnitureBounds center={furnCenter} size={furnSize}");
            Debug.Log($"[OnboardingPreviewStageCamera] roomBounds center={roomCenter} size={roomSize}");
            Debug.Log($"[OnboardingPreviewStageCamera] excludedFromFit=Wall/Floor count={excludedCount}");

            // FOV — 42° (사용자 권장 38~48, wide 왜곡 회피 + 가구 자연스러운 표현).
            float fov = 42f;
            previewCamera.fieldOfView = fov;

            // *frontal quarter view* — cozy interior preview 의 핵심.
            //   yaw: 180° (flip 유지, 가구가 +Z 쪽이라 -Z 에서 +Z 바라봄) + 12° (사용자 권장 8~18, 살짝 대각선).
            //   pitch: 10° (사용자 권장 5~15, 사람 눈높이 살짝 위 — 너무 top-down 아니게, 너무 flat 도 아니게).
            //   결과: 사람이 방 입구 살짝 옆에서 안을 들여다보는 듯한 정면 4분지 시점.
            float yawDeg = 180f + 12f;
            float pitchDeg = 10f;
            float yawRad = yawDeg * Mathf.Deg2Rad;
            float pitchRad = pitchDeg * Mathf.Deg2Rad;
            var dir = new Vector3(
                Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
                -Mathf.Sin(pitchRad),
                -Mathf.Cos(yawRad) * Mathf.Cos(pitchRad));

            // distance — furniture 의 가장 큰 dimension 기준 (wall/floor dominate 차단). margin 1.15 —
            // 가구가 frame 의 ~85% 채우는 cozy showcase 거리. 1.25 → 1.15 줄여 더 가까이 → "miniature
            // diorama" 회귀 제거.
            float maxFurnDim = Mathf.Max(furnSize.x, furnSize.y, furnSize.z);
            float fovRad = fov * Mathf.Deg2Rad;
            float distance = (maxFurnDim * 0.5f) / Mathf.Tan(fovRad * 0.5f) * 1.15f;
            distance = Mathf.Max(distance, maxFurnDim * 0.7f);

            // **furniture-weighted center** — bookshelf(3) / desk(2) / other(2) / dashboard(1) / wall+floor(0).
            // furniture bounds center 가 dashboard 의 wall 위치 같은 외곽 요소에 끌려가는 회귀를 weight 로 차단.
            var weightedCenter = ComputeFurnitureWeightedCenter();
            // y 살짝 위로 (사람 눈높이 살짝 위 느낌). x/z 는 weighted center 사용.
            // 좌측 28% UI 보정: camera.right 기반 -shift → furniture frame 우측 배치.
            var worldUp = Vector3.up;
            var cameraRight = Vector3.Cross(worldUp, dir).normalized;
            var lookAt = new Vector3(weightedCenter.x, weightedCenter.y + furnSize.y * 0.05f, weightedCenter.z)
                         - cameraRight * (furnSize.x * 0.08f);

            previewCamera.transform.position = lookAt - dir * distance;
            previewCamera.transform.LookAt(lookAt, worldUp);

            // far clip — distance + room dimension 의 2배. wall/floor 가 시야 안에 들어와도 잘리지 않게.
            float maxRoomDim = Mathf.Max(roomSize.x, roomSize.y, roomSize.z);
            previewCamera.farClipPlane = Mathf.Max(previewCamera.farClipPlane, distance + maxRoomDim * 2f);

            // 카메라 위치가 room back wall 밖이라면 그 back wall 을 hide → 사용자가 room 내부를 들여다보는 시점.
            // 측면 wall 은 유지 (cozy 느낌).
            HideObstructingWalls(previewCamera.transform.position, lookAt);

            Debug.Log("[OnboardingPreviewStage] Furniture-centered framing applied");
            Debug.Log("[OnboardingPreviewStage] Camera repositioned for interior view");
            Debug.Log("[OnboardingPreviewStage] Camera adjusted for frontal quarter view");
            Debug.Log($"[OnboardingPreviewStage] Furniture weighted center={weightedCenter}");
            Debug.Log($"[OnboardingPreviewStageCamera] lookAt={lookAt}");
            Debug.Log($"[OnboardingPreviewStageCamera] cameraPos={previewCamera.transform.position}");
            Debug.Log($"[OnboardingPreviewStageCamera] distance={distance:F2}");
            Debug.Log($"[OnboardingPreviewStage] Final camera position={previewCamera.transform.position}");
            Debug.Log($"[OnboardingPreviewStage] Final camera rotation={previewCamera.transform.rotation.eulerAngles}");
            Debug.Log($"[OnboardingPreviewStage] Final camera fov={fov}");
        }

        // Furniture-weighted center — 카메라 target 계산용.
        //
        // weight:
        //   Bookshelf=3, Desk=2, Other=2, Dashboard=1, Wall=0, Floor=0
        //
        // bookshelf 가 메인 visual focal point 라 가장 무거움. dashboard 는 wall 에 붙어 있어 lookAt 중심이
        // 그쪽으로 끌리면 framing 이 한쪽으로 치우침 → weight 낮춤. wall/floor 는 0 (영향 0).
        private Vector3 ComputeFurnitureWeightedCenter()
        {
            if (previewRoomRoot == null) return Vector3.zero;
            var renderers = previewRoomRoot.GetComponentsInChildren<Renderer>(true);
            Vector3 weighted = Vector3.zero;
            float totalWeight = 0f;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (ceilingInstance != null && r.gameObject == ceilingInstance) continue;
                var b = r.bounds;
                if (b.size.sqrMagnitude < 1e-6f) continue;

                var kind = ClassifyRenderer(r);
                float weight;
                switch (kind)
                {
                    case PreviewKind.Bookshelf: weight = 3.0f; break;
                    case PreviewKind.Desk: weight = 2.0f; break;
                    case PreviewKind.Other: weight = 2.0f; break;
                    case PreviewKind.Dashboard: weight = 1.0f; break;
                    default: weight = 0f; break;
                }
                if (weight <= 0f) continue;
                weighted += b.center * weight;
                totalWeight += weight;
            }
            if (totalWeight < 0.001f) return Vector3.zero;
            return weighted / totalWeight;
        }

        // Back wall hiding — 카메라가 room back wall 바깥에 위치할 때 (보통 그러함, furniture-based distance
        // 가 wall 거리보다 큼) 카메라와 lookAt 사이에 있는 wall 만 hide. dot product 로 판단:
        //   cameraBack = (camPos - lookAt).normalized — lookAt 에서 camera 쪽 방향
        //   wallSide = (wall.center - lookAt) — lookAt 에서 wall 쪽 방향
        //   dot(wallSide, cameraBack) > 0.35 → wall 이 카메라 쪽에 있음 → hide
        //
        // 측면 wall (lookAt 과 perpendicular) 은 dot ~0 → keep (cozy room 측면이 보임).
        private void HideObstructingWalls(Vector3 camPos, Vector3 lookAt)
        {
            if (previewRoomRoot == null) return;
            var renderers = previewRoomRoot.GetComponentsInChildren<Renderer>(true);
            var cameraBack = (camPos - lookAt);
            if (cameraBack.sqrMagnitude < 1e-6f) return;
            cameraBack.Normalize();

            int hidden = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (ClassifyRenderer(r) != PreviewKind.Wall) continue;

                var wallSide = r.bounds.center - lookAt;
                float dot = Vector3.Dot(wallSide, cameraBack);
                if (dot > 0.35f)
                {
                    r.enabled = false;
                    hidden++;
                }
            }
            Debug.Log($"[OnboardingPreviewStage] Empty void reduced — hidden obstructing walls count={hidden}");
        }

        private static void DisablePreviewOnlyComponents(GameObject root)
        {
            if (root == null) return;
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
            var rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                rigidbodies[i].detectCollisions = false;
                rigidbodies[i].isKinematic = true;
            }
            var audios = root.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audios.Length; i++) audios[i].enabled = false;
            var particles = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                var emit = particles[i].emission;
                emit.enabled = false;
            }
            var animators = root.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++) animators[i].enabled = false;
            var anims = root.GetComponentsInChildren<Animation>(true);
            for (int i = 0; i < anims.Length; i++) anims[i].enabled = false;
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

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            var t = go.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                SetLayerRecursive(t.GetChild(i).gameObject, layer);
            }
        }
    }
}
