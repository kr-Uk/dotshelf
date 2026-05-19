using System.Collections.Generic;
using App.Infrastructure.Catalogs;
using UnityEngine;

namespace App.Bootstrap
{
    // 온보딩 Step2 우측 preview 영역에 실시간 mini-room 을 렌더해주는 컴포넌트.
    //
    // 구조:
    //   - 작은 mock interior room (floor + 3 walls + bookshelf + desk + dashboard + lamp) 을
    //     world coord (10000,10000,10000) 근방에 1회 생성. 일반적인 main camera far clip(<1000m)
    //     밖이라 다른 카메라가 우연히 잡지 않음.
    //   - layer = TransparentFX (Unity default layer 1) — ProjectSettings/TagManager.asset 변경
    //     없이 격리 가능한 default layer. PreviewCamera.cullingMask 가 이 layer 만 포함해
    //     다른 카메라의 mesh 가 우연히 들어오지 않게 한다.
    //   - 별도 Camera (PreviewCamera) 가 mock room 만 RenderTexture (16:9) 로 렌더.
    //     targetTexture 가 set 돼 있어 main display 에 그리지 않는다.
    //
    // 테마 적용:
    //   RoomThemeAsset 의 material 필드 (WallMaterial / FloorMaterial / BookshelfMaterial /
    //   FurnitureMaterial / DeskTopMaterial / DashboardMaterial) 를 mock mesh 의 sharedMaterial
    //   에 직접 swap. null 필드는 PrimitiveType 의 default material 유지.
    //   Light 의 color/intensity 도 theme.InteriorLightColor / AmbientIntensity 로 갱신.
    //   같은 RoomThemeAsset 자산을 입력으로 쓰므로 MainScene 적용과 single source of truth 유지.
    //
    // RoomThemeApplier 를 그대로 재사용하지 않는 이유:
    //   RoomThemeApplier 는 BookshelfGenerator / MaterialFactory / ExteriorApplier 등 여러 의존성을
    //   wire 한 root 가 필요. mock preview 에서는 그 의존성을 모두 만족시키기 어려우므로 필드만 활용.
    //
    // 실패 처리:
    //   Initialize() 가 Camera / RenderTexture / Primitive 생성 어디서든 실패하면 false 반환 +
    //   Dispose 호출. 호출자 (OnboardingScreenController) 는 IsReady=false 일 때 기존 sprite preview
    //   로 fallback 한다.
    //
    // Lifecycle:
    //   - InitializeIfNeeded 시점 1회 Initialize (성공 시 IsReady=true).
    //   - SelectTheme → ApplyTheme(theme) 가 material swap + Camera.Render 강제 호출 → RT 즉시 갱신.
    //   - Step3 진입 시 또는 OnDestroy 시 Dispose — Camera/RT/mock room 모두 정리.
    internal sealed class OnboardingThemePreviewRenderer
    {
        // 16:9 — OnboardingPreviewStage(active path) 와 동일 비율. 정사각형 RT 회귀 차단 (사용자 보고 2026-05-14).
        // EnableMockRoomPreview=false 면 본 클래스 미사용 (dead-code) — active path 는 OnboardingPreviewStage.
        private const int RtWidth = 1600;
        private const int RtHeight = 900;
        // Unity default layer 1 (TransparentFX). Main camera 와는 layer + cullingMask 로 격리 —
        // PreviewCamera 만 이 layer 를 cull on 한다. PreviewLayer 격리에 의존하므로 main camera 의
        // cullingMask 에서 TransparentFX 가 빠져 있어야 함 (Unity Camera default cullingMask 는
        // Everything 이라 onboarding scene 의 default camera 가 있으면 mock room 이 main display
        // 에도 보일 수 있음 — onboarding scene 자체엔 보통 main camera 가 없어 안전).
        private const int PreviewLayer = 1;
        // 이전 (10000, 10000, 10000) 격리는 float precision 손실 (URP/Lit 의 normal 보간 + light
        // 계산이 큰 정수 좌표에서 부정확) 로 mesh shading 이 cream 한 톤으로 평탄해지는 회귀
        // 원인이었음 (사용자 콘솔 진단). 원점 근처로 이동해 정밀도 안전.
        // main camera 가 보지 않게 하는 격리는 layer + cullingMask 만으로 충분.
        private static readonly Vector3 PreviewWorldOrigin = Vector3.zero;

        private GameObject roomRoot;
        private Camera previewCamera;
        private RenderTexture renderTexture;
        private Light previewLight;
        private Renderer floorRenderer;
        private Renderer[] wallRenderers;
        private Renderer bookshelfRenderer;
        private Renderer deskRenderer;
        private Renderer dashboardRenderer;
        private Renderer lampRenderer;
        private Material defaultFloorMat;
        private Material defaultWallMat;
        private Material defaultBookshelfMat;
        private Material defaultDeskMat;
        private Material defaultDashboardMat;

        // Mock primitive furniture (bookshelf/desk/dashboard/lamp) 의 부모. 단일 테마 모드에선 활성 +
        // material swap, 랜덤 모드에선 비활성 (prefabSlotRoot 가 대신 표시). floor / walls 는 두 모드 모두
        // 활성 — wall/floor material 은 두 모드 모두 baseTheme/theme 으로 swap.
        private GameObject mockFurnitureRoot;
        // 랜덤 selection 의 slot 별 prefab instance 부모. SetRandomMode(true) 시 활성.
        private GameObject prefabSlotRoot;
        // slot → 현재 spawned instance + assetKey. 같은 slot+assetKey 인 경우 reuse,
        // assetKey 가 바뀌면 destroy 후 새 instantiate. selection 에서 빠진 slot 은 hide.
        private readonly Dictionary<ThemeSlot, PreviewSlotInstance> previewInstancePool
            = new Dictionary<ThemeSlot, PreviewSlotInstance>();

        private struct PreviewSlotInstance
        {
            public GameObject Instance;
            public string AssetKey;
        }

        public bool IsReady { get; private set; }
        public RenderTexture RenderTexture => renderTexture;

        // 마지막 Render 가 mesh 를 RT 에 실제로 그렸는지 — pixel sampling 결과. false 면 RT 는 거의
        // backgroundColor 한 색만 → 호출자가 UI 단계에서 RawImage hide + sprite fallback 전환 가능.
        public bool LastRenderHasMeshContent { get; private set; } = true;

        // 1회 호출. 성공 시 IsReady=true. 실패 시 Dispose + false (호출자가 sprite fallback).
        public bool Initialize()
        {
            if (IsReady) return true;
            try
            {
                renderTexture = new RenderTexture(RtWidth, RtHeight, 24, RenderTextureFormat.Default)
                {
                    name = "OnboardingThemePreviewRT",
                    antiAliasing = 2,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                if (!renderTexture.Create())
                {
                    Debug.LogWarning("[OnboardingPreview] RenderTexture.Create() returned false — sprite fallback.");
                    Dispose();
                    return false;
                }

                roomRoot = new GameObject("OnboardingPreviewRoom");
                roomRoot.transform.position = PreviewWorldOrigin;
                roomRoot.layer = PreviewLayer;

                BuildMockRoom(roomRoot.transform);
                BuildPreviewCamera(roomRoot.transform);
                BuildPreviewLight(roomRoot.transform);

                // 초기 렌더 — RawImage 가 활성화되는 첫 frame 에 검은 RT 가 보이지 않게 즉시 한 번 그림.
                if (previewCamera != null)
                {
                    previewCamera.Render();
                    App.Core.AppLog.Onboarding("[OnboardingPreview] Camera.Render called (Initialize).");
                }

                IsReady = true;
                App.Core.AppLog.Onboarding(
                    $"[OnboardingPreview] RenderTexture ready=true size={renderTexture.width}x{renderTexture.height} " +
                    $"created={renderTexture.IsCreated()} cameraEnabled={(previewCamera != null ? previewCamera.enabled : false)} " +
                    $"camera.target={(previewCamera != null && previewCamera.targetTexture != null ? "RT" : "null")}");
                LogFrustumDiagnostics();
                ValidateRtHasMeshContent("Initialize");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[OnboardingPreview] Initialize 실패 — sprite fallback. {ex.GetType().Name}: {ex.Message}");
                Dispose();
                return false;
            }
        }

        // 호출자 (OnboardingScreenController) 가 SelectTheme → Resolver → ApplyTheme 흐름으로 호출.
        // RoomThemeAsset 의 material 필드를 mock mesh sharedMaterial 에 swap + light 갱신 + 즉시 render.
        //
        // 단일 테마 모드 — mock primitive furniture (PreviewBookshelf/Desk/Dashboard/Lamp) 가 visible.
        // SetRandomMode(false) 가 prefab pool 을 hide 하고 mock furniture 를 활성.
        public void ApplyTheme(RoomThemeAsset theme)
        {
            if (!IsReady || theme == null) return;

            // 이전이 random 모드였다면 prefab pool 비활성 + mock furniture 활성 — 단일 테마 시각으로 복귀.
            SetRandomMode(false);

            if (floorRenderer != null)
            {
                floorRenderer.sharedMaterial = theme.FloorMaterial != null ? theme.FloorMaterial : defaultFloorMat;
            }
            if (wallRenderers != null)
            {
                var wallMat = theme.WallMaterial != null ? theme.WallMaterial : defaultWallMat;
                for (int i = 0; i < wallRenderers.Length; i++)
                {
                    if (wallRenderers[i] != null) wallRenderers[i].sharedMaterial = wallMat;
                }
            }
            if (bookshelfRenderer != null)
            {
                bookshelfRenderer.sharedMaterial = theme.BookshelfMaterial != null
                    ? theme.BookshelfMaterial
                    : defaultBookshelfMat;
            }
            if (deskRenderer != null)
            {
                // Desk: DeskTopMaterial 우선 → FurnitureMaterial → default.
                var deskMat = theme.DeskTopMaterial != null
                    ? theme.DeskTopMaterial
                    : (theme.FurnitureMaterial != null ? theme.FurnitureMaterial : defaultDeskMat);
                deskRenderer.sharedMaterial = deskMat;
            }
            if (dashboardRenderer != null)
            {
                dashboardRenderer.sharedMaterial = theme.DashboardMaterial != null
                    ? theme.DashboardMaterial
                    : defaultDashboardMat;
            }
            if (lampRenderer != null && lampRenderer.material != null)
            {
                // Lamp 는 accent tint — material 자체는 instance 화 (sharedMaterial 이 아니라 .material)
                // 해서 다른 mesh 와 분리. 단 instantiate 비용은 1회만 (이후엔 같은 instance reuse).
                var accent = theme.AccentPrimary;
                if (accent.a < 0.01f) accent = Color.white;
                lampRenderer.material.color = accent;
            }

            if (previewLight != null)
            {
                var lc = theme.InteriorLightColor;
                if (lc.a < 0.01f) lc = new Color(1f, 0.96f, 0.9f);
                previewLight.color = lc;
                previewLight.intensity = Mathf.Clamp(theme.AmbientIntensity > 0f ? theme.AmbientIntensity * 1.1f : 1.1f, 0.6f, 2.2f);
            }

            // 즉시 render — 다음 frame 대기 없이 RT 갱신해 SelectTheme 클릭과 RawImage 갱신 사이 latency 0.
            if (previewCamera != null)
            {
                previewCamera.Render();
                App.Core.AppLog.Onboarding($"[OnboardingPreview] Camera.Render called (ApplyTheme theme='{theme.name}').");
                ValidateRtHasMeshContent($"ApplyTheme:{theme.name}");
            }
        }

        // OnboardingScreenController.OnDestroy / Step3 진입 시 호출. 모든 owned 리소스 정리.
        public void Dispose()
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
                Object.Destroy(renderTexture);
                renderTexture = null;
            }
            if (roomRoot != null)
            {
                Object.Destroy(roomRoot);
                roomRoot = null;
            }
            previewCamera = null;
            previewLight = null;
            floorRenderer = null;
            wallRenderers = null;
            bookshelfRenderer = null;
            deskRenderer = null;
            dashboardRenderer = null;
            lampRenderer = null;
            mockFurnitureRoot = null;
            prefabSlotRoot = null;
            previewInstancePool.Clear(); // GameObject reference 들은 roomRoot 와 함께 destroy 됨
            defaultFloorMat = null;
            defaultWallMat = null;
            defaultBookshelfMat = null;
            defaultDeskMat = null;
            defaultDashboardMat = null;
            IsReady = false;
        }

        // 온보딩 Step2 "랜덤" 버튼 클릭 시 호출. selection 의 baseTheme 으로 wall/floor/lighting material
        // swap + slot 별 prefab spawn (또는 reuse) → mock furniture 는 hide. 단일 source 에 single Render.
        //
        // Instance pool 정책:
        //   - 같은 slot+assetKey 가 이미 spawn 돼 있으면 reuse (transform 만 재적용).
        //   - assetKey 가 바뀌면 destroy 후 새 instantiate.
        //   - selection 에서 빠진 slot 의 instance 는 hide (다음 random 클릭에서 같은 slot 으로 reuse 가능).
        //
        // Prefab override:
        //   spec.PreviewPrefab 우선 → 없으면 spec.Prefab fallback. 디자이너가 LOD-trimmed preview prefab
        //   을 RoomThemeDecoSpec 에 wire 하면 자동 사용. 모두 null 이면 entry skip + LogWarning.
        //
        // internal — OnboardingRandomThemeSelection 이 internal sealed 라 CS0051 회피. 호출자
        // (OnboardingScreenController) 가 같은 assembly 라 정상 호출.
        internal void ApplyRandomSelection(OnboardingRandomThemeSelection selection)
        {
            if (!IsReady || selection == null || selection.BaseTheme == null)
            {
                Debug.LogWarning(
                    $"[OnboardingPreview] ApplyRandomSelection skip — " +
                    $"IsReady={IsReady} selection={(selection != null ? "ok" : "null")} " +
                    $"baseTheme={(selection?.BaseTheme != null ? selection.BaseTheme.name : "null")}");
                return;
            }

            // (1) base material/lighting — wall/floor 위주. ApplyTheme 가 SetRandomMode(false) 도 호출
            // 하니까 직접 material swap 만. ApplyTheme 호출하면 SetRandomMode(false) 가 prefab pool 을
            // 다시 비활성 시키므로 아래 SetRandomMode(true) 순서가 어긋남 → ApplyTheme 우회하고
            // material swap 만 inline.
            ApplyMaterialsInline(selection.BaseTheme);

            // (2) mode toggle — mock furniture 비활성, prefab pool 활성
            SetRandomMode(true);

            // (3) slot 별 prefab 갱신
            int spawned = 0, reused = 0, skipped = 0;
            var seenSlots = new HashSet<ThemeSlot>();
            foreach (var kv in selection.SelectedSpecs)
            {
                var slot = kv.Key;
                var entry = kv.Value;
                if (entry == null || entry.Spec == null)
                {
                    Debug.LogWarning($"[OnboardingPreview] slot {slot} — entry/spec null. skip.");
                    skipped++;
                    continue;
                }
                if (ApplyPreviewForSlot(slot, entry, out bool wasReused))
                {
                    seenSlots.Add(slot);
                    if (wasReused) reused++; else spawned++;
                }
                else
                {
                    skipped++;
                }
            }

            // (4) selection 에서 빠진 slot 의 instance 는 hide (destroy 안 함 — 다음 random 클릭에서 같은
            // slot 으로 prefab 만 바뀌면 reuse 가능)
            HideUnusedPoolEntries(seenSlots);

            // (5) immediate render — RawImage 가 다음 frame 까지 stale 안 보이게.
            if (previewCamera != null)
            {
                previewCamera.Render();
                App.Core.AppLog.Onboarding("[OnboardingPreview] Camera.Render called (ApplyRandomSelection).");
                ValidateRtHasMeshContent("ApplyRandomSelection");
            }

            App.Core.AppLog.Onboarding(
                $"[OnboardingPreview] ApplyRandomSelection — slots={selection.SelectedSpecs.Count} " +
                $"spawned={spawned} reused={reused} skipped={skipped}");
        }

        // mock furniture (mockFurnitureRoot) ↔ prefab pool (prefabSlotRoot) 활성 토글.
        // 호출 순서 의존성이 있어 ApplyTheme / ApplyRandomSelection 둘 다에서 명시 호출.
        private void SetRandomMode(bool isRandom)
        {
            if (mockFurnitureRoot != null && mockFurnitureRoot.activeSelf != !isRandom)
            {
                mockFurnitureRoot.SetActive(!isRandom);
            }
            if (prefabSlotRoot != null && prefabSlotRoot.activeSelf != isRandom)
            {
                prefabSlotRoot.SetActive(isRandom);
            }
        }

        // ApplyTheme 본문의 material/light swap 부분을 inline. ApplyRandomSelection 이 SetRandomMode(false)
        // 호출을 우회하기 위해 추출 — ApplyTheme 는 단일 테마 모드 진입 hook 으로 SetRandomMode(false) 호출.
        private void ApplyMaterialsInline(RoomThemeAsset theme)
        {
            if (floorRenderer != null)
                floorRenderer.sharedMaterial = theme.FloorMaterial != null ? theme.FloorMaterial : defaultFloorMat;
            if (wallRenderers != null)
            {
                var wallMat = theme.WallMaterial != null ? theme.WallMaterial : defaultWallMat;
                for (int i = 0; i < wallRenderers.Length; i++)
                {
                    if (wallRenderers[i] != null) wallRenderers[i].sharedMaterial = wallMat;
                }
            }
            // mock furniture 들도 hide 돼 있어도 sharedMaterial 갱신 — 다음 single-theme 진입 시 자연스러움.
            if (bookshelfRenderer != null)
                bookshelfRenderer.sharedMaterial = theme.BookshelfMaterial != null ? theme.BookshelfMaterial : defaultBookshelfMat;
            if (deskRenderer != null)
            {
                var deskMat = theme.DeskTopMaterial != null
                    ? theme.DeskTopMaterial
                    : (theme.FurnitureMaterial != null ? theme.FurnitureMaterial : defaultDeskMat);
                deskRenderer.sharedMaterial = deskMat;
            }
            if (dashboardRenderer != null)
                dashboardRenderer.sharedMaterial = theme.DashboardMaterial != null ? theme.DashboardMaterial : defaultDashboardMat;
            if (lampRenderer != null && lampRenderer.material != null)
            {
                var accent = theme.AccentPrimary;
                if (accent.a < 0.01f) accent = Color.white;
                lampRenderer.material.color = accent;
            }
            if (previewLight != null)
            {
                var lc = theme.InteriorLightColor;
                if (lc.a < 0.01f) lc = new Color(1f, 0.96f, 0.9f);
                previewLight.color = lc;
                previewLight.intensity = Mathf.Clamp(theme.AmbientIntensity > 0f ? theme.AmbientIntensity * 1.1f : 1.1f, 0.6f, 2.2f);
            }
        }

        private bool ApplyPreviewForSlot(ThemeSlot slot, OnboardingRandomThemeSelection.Entry entry, out bool wasReused)
        {
            wasReused = false;
            if (entry?.Spec == null || prefabSlotRoot == null) return false;

            if (!OnboardingPreviewSlotAnchor.TryGetAnchor(slot, out var anchor))
            {
                Debug.LogWarning(
                    $"[OnboardingPreview] slot {slot} — anchor 정의 없음. skip. " +
                    "OnboardingPreviewSlotAnchor.TryGetAnchor 에 케이스 추가 필요.");
                return false;
            }

            // prefab 우선순위: PreviewPrefab > Prefab. 둘 다 null 이면 skip.
            var prefab = entry.Spec.PreviewPrefab != null ? entry.Spec.PreviewPrefab : entry.Spec.Prefab;
            if (prefab == null)
            {
                Debug.LogWarning(
                    $"[OnboardingPreview] slot {slot} — spec.PreviewPrefab 와 spec.Prefab 둘 다 null. " +
                    $"assetKey={entry.AssetKey} themeAsset={entry.ThemeAssetName}. skip.");
                return false;
            }
            bool usedFallback = entry.Spec.PreviewPrefab == null;
            if (usedFallback)
            {
                App.Core.AppLog.Onboarding(
                    $"[OnboardingPreview] slot {slot} — PreviewPrefab missing. Fallback to original prefab '{prefab.name}'. " +
                    "RoomThemeDecoSpec.previewPrefab 에 lightweight 버전 wiring 권장.");
            }

            // pool reuse — 같은 assetKey 면 instance 재활용.
            if (previewInstancePool.TryGetValue(slot, out var existing) && existing.Instance != null)
            {
                if (string.Equals(existing.AssetKey, entry.AssetKey, System.StringComparison.Ordinal))
                {
                    existing.Instance.SetActive(true);
                    NormalizeInstanceTransform(existing.Instance, prefabSlotRoot.transform, in anchor, entry.Spec);
                    App.Core.AppLog.Onboarding($"[OnboardingPreview] Reuse preview instance slot={slot} assetKey={entry.AssetKey}");
                    wasReused = true;
                    return true;
                }
                // asset 바뀜 → destroy 후 새로 instantiate
                Object.Destroy(existing.Instance);
                previewInstancePool.Remove(slot);
            }

            GameObject instance;
            try
            {
                instance = Object.Instantiate(prefab, prefabSlotRoot.transform, false);
                instance.name = $"Preview_{slot}_{entry.AssetKey}";
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(
                    $"[OnboardingPreview] Instantiate 실패 slot={slot} prefab='{prefab.name}'. " +
                    $"{ex.GetType().Name}: {ex.Message}");
                return false;
            }

            SetLayerRecursive(instance, PreviewLayer);
            DisablePreviewOnlyComponents(instance);
            NormalizeInstanceTransform(instance, prefabSlotRoot.transform, in anchor, entry.Spec);

            previewInstancePool[slot] = new PreviewSlotInstance
            {
                Instance = instance,
                AssetKey = entry.AssetKey,
            };
            App.Core.AppLog.Onboarding($"[OnboardingPreview] Spawn preview prefab slot={slot} prefab={prefab.name} assetKey={entry.AssetKey} fallback={usedFallback}");
            return true;
        }

        private void HideUnusedPoolEntries(HashSet<ThemeSlot> seenSlots)
        {
            foreach (var kv in previewInstancePool)
            {
                if (seenSlots.Contains(kv.Key)) continue;
                if (kv.Value.Instance != null && kv.Value.Instance.activeSelf)
                {
                    kv.Value.Instance.SetActive(false);
                }
            }
        }

        // Preview 는 "보여주는 것" 만 — 상호작용 / 물리 / 오디오 / 파티클 / 애니메이션 모두 disable.
        // 원본 prefab 의 hierarchy 는 보존 (instantiate 결과라 원본 영향 없음).
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
            var animations = root.GetComponentsInChildren<Animation>(true);
            for (int i = 0; i < animations.Length; i++) animations[i].enabled = false;
        }

        private static void NormalizeInstanceTransform(GameObject inst, Transform parent, in OnboardingPreviewSlotAnchor.Anchor anchor, RoomThemeDecoSpec spec)
        {
            if (inst == null) return;
            var t = inst.transform;
            t.SetParent(parent, false);
            // anchor.LocalPosition 이 base — spec.LocalPosition 은 placedItemsRoot frame 이라 preview 와 부합 X.
            t.localPosition = anchor.LocalPosition;
            // anchor.LocalEulerAngles + spec.LocalRotation — anchor 가 base orientation (예: 벽 방향),
            // spec 이 fine-tune.
            t.localEulerAngles = anchor.LocalEulerAngles + spec.LocalRotation;
            // localScale — prefab transform.localScale 그대로 유지 (1, 1, 1 reset 안 함). spec.LocalScale 은
            // placedItemsRoot frame 의 raw 값이라 preview room 에서 의미가 어긋날 수 있어 무시.
            t.localScale = inst.transform.localScale; // no-op — 명시적 의도 표현

            // bounds 측정 후 anchor.MaxBoundsSize 보다 크면 비례 축소 (한 축이라도 over 시 가장 tight 한 축 기준).
            var bounds = ComputeBoundsSafe(inst);
            if (bounds.HasValue && anchor.MaxBoundsSize.sqrMagnitude > 1e-6f)
            {
                var size = bounds.Value.size;
                if (size.x > 1e-4f && size.y > 1e-4f && size.z > 1e-4f)
                {
                    float fx = anchor.MaxBoundsSize.x / size.x;
                    float fy = anchor.MaxBoundsSize.y / size.y;
                    float fz = anchor.MaxBoundsSize.z / size.z;
                    float factor = Mathf.Min(fx, fy, fz);
                    if (factor < 1f)
                    {
                        t.localScale = t.localScale * factor;
                        bounds = ComputeBoundsSafe(inst);
                    }
                }
            }

            // floor lift — bounds.min.y 가 anchor.LocalPosition.y 아래로 내려가면 mesh 가 바닥 밑으로 잘림.
            // Rug 같이 의도적으로 바닥에 깔리는 slot (MaxBoundsSize.y < 0.1) 은 skip.
            if (bounds.HasValue && anchor.MaxBoundsSize.y > 0.1f)
            {
                float worldFloorY = parent.position.y + anchor.LocalPosition.y;
                float worldMinY = bounds.Value.min.y;
                float diff = worldFloorY - worldMinY;
                if (diff > 0.005f)
                {
                    t.position += Vector3.up * diff;
                }
            }
        }

        private static Bounds? ComputeBoundsSafe(GameObject go)
        {
            if (go == null) return null;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return null;
            Bounds? acc = null;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (!r.gameObject.activeInHierarchy) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var b = r.bounds;
                if (b.size.sqrMagnitude < 1e-8f) continue;
                if (acc == null) acc = b;
                else { var aa = acc.Value; aa.Encapsulate(b); acc = aa; }
            }
            return acc;
        }

        private void BuildMockRoom(Transform parent)
        {
            // Floor — Plane 의 default size 는 10×10 unit. localScale 0.4 → 4×4m 바닥.
            var floor = CreatePrimitive(parent, PrimitiveType.Plane, "PreviewFloor",
                new Vector3(0f, 0f, 0f),
                Vector3.zero,
                new Vector3(0.4f, 1f, 0.4f));
            floorRenderer = floor.GetComponent<Renderer>();
            if (floorRenderer != null) defaultFloorMat = floorRenderer.sharedMaterial;

            // Walls — Quad (1×1) 를 4×3m 로 stretch. inward facing.
            wallRenderers = new Renderer[3];
            var back = CreatePrimitive(parent, PrimitiveType.Quad, "PreviewWallBack",
                new Vector3(0f, 1.5f, 2f),
                new Vector3(0f, 180f, 0f),
                new Vector3(4f, 3f, 1f));
            wallRenderers[0] = back.GetComponent<Renderer>();

            var left = CreatePrimitive(parent, PrimitiveType.Quad, "PreviewWallLeft",
                new Vector3(-2f, 1.5f, 0f),
                new Vector3(0f, 90f, 0f),
                new Vector3(4f, 3f, 1f));
            wallRenderers[1] = left.GetComponent<Renderer>();

            var right = CreatePrimitive(parent, PrimitiveType.Quad, "PreviewWallRight",
                new Vector3(2f, 1.5f, 0f),
                new Vector3(0f, -90f, 0f),
                new Vector3(4f, 3f, 1f));
            wallRenderers[2] = right.GetComponent<Renderer>();

            // URP/Lit 명시 fallback material 생성. PrimitiveType 의 default material 은 white 라
            // cream 배경과 거의 같은 톤이 됨 → 형상 인지 어려움. theme.Material 이 null 이거나
            // background 와 비슷한 cream 톤일 때도 mesh 형상이 보이도록 명시 색 부여.
            // (사용자 진단: "베이지 한 덩어리만 보임" — 배경 + 디폴트 white mesh + cream theme material 조합)
            defaultFloorMat = CreateFallbackUrpLit("OnboardingPreview_DefaultFloor", new Color(0.55f, 0.40f, 0.30f), 0.20f); // warm brown
            defaultWallMat = CreateFallbackUrpLit("OnboardingPreview_DefaultWall", new Color(0.82f, 0.74f, 0.62f), 0.15f); // warmer beige (darker than bg)
            if (floorRenderer != null) floorRenderer.sharedMaterial = defaultFloorMat;
            if (wallRenderers != null)
            {
                for (int i = 0; i < wallRenderers.Length; i++)
                {
                    if (wallRenderers[i] != null) wallRenderers[i].sharedMaterial = defaultWallMat;
                }
            }

            // Furniture 그룹 (책장/책상/대시보드/lamp) — 단일 테마 모드에선 활성 (mock primitive +
            // material swap), 랜덤 모드에선 SetRandomMode(false) 가 toggle. floor/walls 는 그룹 밖
            // 이라 두 모드 모두 활성 유지 + baseTheme material swap.
            mockFurnitureRoot = new GameObject("PreviewMockFurniture");
            mockFurnitureRoot.transform.SetParent(parent, false);
            mockFurnitureRoot.transform.localPosition = Vector3.zero;
            mockFurnitureRoot.layer = PreviewLayer;
            var mockFurnitureParent = mockFurnitureRoot.transform;

            // Furniture default material — URP/Lit 로 명시 색. cream 배경과 대비 보장.
            defaultBookshelfMat = CreateFallbackUrpLit("OnboardingPreview_DefaultBookshelf", new Color(0.45f, 0.32f, 0.22f), 0.25f); // muted wood
            defaultDeskMat = CreateFallbackUrpLit("OnboardingPreview_DefaultDesk", new Color(0.62f, 0.46f, 0.34f), 0.30f); // lighter wood
            defaultDashboardMat = CreateFallbackUrpLit("OnboardingPreview_DefaultDashboard", new Color(0.20f, 0.25f, 0.35f), 0.45f); // dark navy

            // Bookshelf — Cube, against back-left wall.
            var shelf = CreatePrimitive(mockFurnitureParent, PrimitiveType.Cube, "PreviewBookshelf",
                new Vector3(-1.1f, 0.9f, 1.55f),
                Vector3.zero,
                new Vector3(1.4f, 1.8f, 0.35f));
            bookshelfRenderer = shelf.GetComponent<Renderer>();
            if (bookshelfRenderer != null) bookshelfRenderer.sharedMaterial = defaultBookshelfMat;

            // Desk — Cube, front-center-right.
            var desk = CreatePrimitive(mockFurnitureParent, PrimitiveType.Cube, "PreviewDesk",
                new Vector3(0.6f, 0.4f, 0.5f),
                Vector3.zero,
                new Vector3(1.4f, 0.8f, 0.7f));
            deskRenderer = desk.GetComponent<Renderer>();
            if (deskRenderer != null) deskRenderer.sharedMaterial = defaultDeskMat;

            // Dashboard — thin Cube, on back wall above desk.
            var dash = CreatePrimitive(mockFurnitureParent, PrimitiveType.Cube, "PreviewDashboard",
                new Vector3(0.6f, 1.55f, 1.93f),
                Vector3.zero,
                new Vector3(1.2f, 0.7f, 0.05f));
            dashboardRenderer = dash.GetComponent<Renderer>();
            if (dashboardRenderer != null) dashboardRenderer.sharedMaterial = defaultDashboardMat;

            // Lamp — Sphere on desk, accent tint applied per theme.
            var lamp = CreatePrimitive(mockFurnitureParent, PrimitiveType.Sphere, "PreviewLamp",
                new Vector3(1.05f, 0.95f, 0.5f),
                Vector3.zero,
                new Vector3(0.28f, 0.28f, 0.28f));
            lampRenderer = lamp.GetComponent<Renderer>();

            // Prefab slot root — 랜덤 selection 의 slot 별 instance 부모. 초기엔 비활성.
            prefabSlotRoot = new GameObject("PreviewPrefabSlots");
            prefabSlotRoot.transform.SetParent(parent, false);
            prefabSlotRoot.transform.localPosition = Vector3.zero;
            prefabSlotRoot.layer = PreviewLayer;
            prefabSlotRoot.SetActive(false);
        }

        private GameObject CreatePrimitive(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 localEuler, Vector3 localScale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            // preview 는 시각 전용 — collider 제거 (raycast 비용 / 의도치 않은 hit 회피).
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = localEuler;
            go.transform.localScale = localScale;
            go.layer = PreviewLayer;
            return go;
        }

        private void BuildPreviewCamera(Transform parent)
        {
            var camGo = new GameObject("OnboardingPreviewCamera");
            camGo.transform.SetParent(parent, false);
            // Front-center 약간 오른쪽 + 사람 시점 높이 framing. 이전 (2.2, 1.7, -2.0) 은 right wall
            // (x=2) 바로 옆이라 wall 일부만 화면 대부분 차지하는 "벽지 확대" 회귀 (사용자 보고).
            // x 를 center 쪽으로 (2.2 → 1.5), z 를 살짝 안쪽 (-2.0 → -1.6) 으로 이동. front wall 이
            // 없어 -1.6 까지 들어가도 안전 (front 가 카메라 뒤).
            camGo.transform.localPosition = new Vector3(1.5f, 1.6f, -1.6f);
            camGo.layer = PreviewLayer;

            previewCamera = camGo.AddComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            // backgroundColor — cream 톤 (#F2EBDB) 일 때 mock mesh 의 cream/베이지 톤 theme material
            // (Natural 등) 과 한 덩어리로 보여 사용자가 "베이지 한 덩어리만 보임" 호소. cream contrast
            // 명백한 dark slate 로 변경 → mesh 형상이 어떤 theme material 톤이든 background 위에 시각적
            // 분리됨. 동시에 RT 가 *비어 있는지 / mesh 가 안 그려지는지* 진단 색 — 사용자가 우측이
            // dark slate 면 RT/Camera 정상 + mesh 만 안 그려진 것, 여전히 cream 이면 RawImage 자체가
            // 안 보이거나 RT pipeline 자체 문제.
            previewCamera.backgroundColor = new Color(0.18f, 0.19f, 0.23f, 1f); // dark slate
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.orthographic = false;
            // FOV — MainScene CameraController.mainViewFieldOfView (52f) 와 align.
            // OnboardingPreviewStage (active path) 도 52f. 본 mock renderer 는 dead-code 지만 FOV 는 동일 유지.
            previewCamera.fieldOfView = 52f;
            previewCamera.nearClipPlane = 0.1f;
            previewCamera.farClipPlane = 50f;
            previewCamera.targetTexture = renderTexture;
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = true;
            // 다른 main camera 와 paint 순서 충돌 회피 — RT 출력이라 depth/order 가 main display 에 영향 없음.

            // URP (Universal Render Pipeline) 에서 manual Camera.Render() 가 enabled=false 인
            // 카메라에 대해 *안정적으로 동작하지 않는다*. RT 가 첫 clearFlags 색만 남고 mock room
            // mesh 가 그려지지 않는 회귀 발생 (사용자 콘솔 진단: Camera.Render called 출력되는데
            // RT 가 cream 색만 보임 — clearFlags 만 통과).
            //
            // URP RenderPipelineManager 는 enabled=true 인 camera 만 schedule. enabled=true + targetTexture
            // 조합이 URP / Built-in 둘 다 안정. 매 frame 1024×1024 mock room render (mesh ~8개) 는
            // 비용 미미하고 preview lifecycle 이 Step2 짧은 시간 (Step3 진입 시 Dispose) 이라 OK.
            previewCamera.enabled = true;

            // Look at room 의 back-left mid (-0.4, 0.7, 0.6) — back wall 중앙 약간 왼쪽 + 책장
            // 영역. front-right corner 에서 back-left 향하는 자연스러운 cinematic angle.
            camGo.transform.LookAt(parent.position + new Vector3(-0.4f, 0.7f, 0.6f), Vector3.up);
        }

        private void BuildPreviewLight(Transform parent)
        {
            var lightGo = new GameObject("OnboardingPreviewLight");
            lightGo.transform.SetParent(parent, false);
            // directional — angle 만 의미. position 은 시각 디버깅 편의용.
            lightGo.transform.localPosition = new Vector3(1.5f, 3f, -1f);
            lightGo.transform.localEulerAngles = new Vector3(50f, -30f, 0f);
            lightGo.layer = PreviewLayer;

            previewLight = lightGo.AddComponent<Light>();
            previewLight.type = LightType.Directional;
            previewLight.color = new Color(1f, 0.96f, 0.9f); // warm cream
            // intensity 1.8 — dark slate 배경 위에 mesh 가 충분히 밝게 보이도록. theme material 이
            // cream/wood 톤이라도 directional 의 highlight/shadow 로 형상 또렷.
            previewLight.intensity = 1.8f;
            // URP 13+ 의 Light cullingMask 가 Built-in pipeline 처럼 동작 안 하는 케이스 다수 발견 —
            // PreviewLight 가 mock room (TransparentFX layer) 에 닿지 않아 mesh 가 ambient (어두움)
            // 만으로 렌더 → cream 배경과 한 톤으로 보이는 회귀. cullingMask 제한 풀어 모든 layer mesh
            // 가 light 받게 함. onboarding scene 에 3D mesh 거의 없어 다른 영역 영향 미미. directional
            // light 라 angle 만 의미, position 은 무관.
            previewLight.cullingMask = ~0;
            previewLight.shadows = LightShadows.Soft;
            previewLight.shadowStrength = 0.55f;
        }

        // 신규 instantiate 한 prefab 의 모든 자식 GameObject layer 를 PreviewLayer 로 set.
        // PreviewCamera.cullingMask = 1<<PreviewLayer 라 layer 가 다른 자식은 안 보임 — 자식이 별도 layer
        // 를 가진 prefab (예: UI/IgnoreRaycast) 도 preview 에 합류시키기 위한 일괄 처리.
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

        // URP/Lit 명시 fallback material — PrimitiveType default material 의 white 가 cream 배경과 한 톤
        // 으로 보이는 회귀 회피용. theme material 이 null 일 때 mesh 가 cream 한 덩어리로 안 보이게 함.
        // shader 못 찾으면 Standard, 그것도 없으면 Diffuse fallback — 어떤 pipeline 이든 안전.
        private static Material CreateFallbackUrpLit(string name, Color color, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Diffuse");
            var mat = new Material(shader) { name = name };
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            return mat;
        }

        // RT 의 9개 픽셀 sample → backgroundColor 와의 차이가 거의 없으면 mesh 가 RT 에 안 그려진 것.
        // 호출자 (OnboardingScreenController) 가 LastRenderHasMeshContent 를 보고 RawImage 대신
        // sprite fallback 으로 전환. ReadPixels 는 동기 GPU stall 1회 — Initialize / SelectTheme 클릭
        // 시점만 호출이라 비용 미미 (매 frame X).
        private void ValidateRtHasMeshContent(string source)
        {
            if (renderTexture == null || previewCamera == null)
            {
                LastRenderHasMeshContent = false;
                Debug.LogWarning($"[OnboardingPreviewDebug] RT sample skip — rt/camera null. source={source}");
                return;
            }
            try
            {
                var bg = previewCamera.backgroundColor;
                var prevActive = RenderTexture.active;
                RenderTexture.active = renderTexture;
                var tex = new Texture2D(3, 3, TextureFormat.RGBA32, false);
                // 9 점 sample — RT 중앙 + 8 방향. 정밀하지 않아도 mesh 가 어딘가 그려졌는지 판단 충분.
                int w = renderTexture.width;
                int h = renderTexture.height;
                tex.ReadPixels(new Rect(w / 2 - 1, h / 2 - 1, 3, 3), 0, 0);
                tex.Apply();
                int changedPixels = 0;
                const float colorDiffThreshold = 0.05f; // 3채널 평균 차이 임계.
                for (int x = 0; x < 3; x++)
                {
                    for (int y = 0; y < 3; y++)
                    {
                        var p = tex.GetPixel(x, y);
                        float diff = (Mathf.Abs(p.r - bg.r) + Mathf.Abs(p.g - bg.g) + Mathf.Abs(p.b - bg.b)) / 3f;
                        if (diff > colorDiffThreshold) changedPixels++;
                    }
                }
                RenderTexture.active = prevActive;
                Object.Destroy(tex);

                LastRenderHasMeshContent = changedPixels > 0;
                if (LastRenderHasMeshContent)
                {
                    App.Core.AppLog.Onboarding($"[OnboardingPreviewDebug] RT sample changedPixels={changedPixels}/9 (mesh content visible). source={source}");
                }
                else
                {
                    Debug.LogWarning(
                        $"[OnboardingPreviewDebug] RT sample changedPixels=0 → mesh not rendered (RT is background-only). " +
                        $"source={source} bg={bg}. UI 계층에서 sprite/procedural fallback 권장.");
                }
            }
            catch (System.Exception ex)
            {
                LastRenderHasMeshContent = false;
                Debug.LogWarning($"[OnboardingPreviewDebug] RT sample failed — {ex.GetType().Name}: {ex.Message}. source={source}");
            }
        }

        // PreviewCamera 의 frustum 안에 mock room mesh 들이 들어오는지 + layer / 색상 점검.
        // 사용자가 "베이지 한 덩어리" 회귀 보고 시 어디서 막혔는지 진단 위해 Initialize 끝에서 1회 호출.
        // 비용은 enabled=true camera 의 매 frame culling 과 별개 — Initialize 한 번만이라 무시 가능.
        [System.Diagnostics.Conditional("DOTSHELF_LOG_ONBOARDING")]
        private void LogFrustumDiagnostics()
        {
            if (previewCamera == null || roomRoot == null) return;
            try
            {
                var planes = GeometryUtility.CalculateFrustumPlanes(previewCamera);
                var renderers = roomRoot.GetComponentsInChildren<Renderer>(true);
                App.Core.AppLog.Onboarding(
                    $"[OnboardingPreviewDebug] root.pos={roomRoot.transform.position} " +
                    $"cam.pos={previewCamera.transform.position} cam.forward={previewCamera.transform.forward} " +
                    $"cam.near={previewCamera.nearClipPlane} cam.far={previewCamera.farClipPlane} " +
                    $"cam.cullingMask=0x{previewCamera.cullingMask:X8} cam.target={(previewCamera.targetTexture != null ? "RT" : "null")} " +
                    $"light.cullingMask=0x{(previewLight != null ? previewLight.cullingMask : 0):X8} " +
                    $"light.intensity={(previewLight != null ? previewLight.intensity : 0f):F2}");

                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    bool inFrustum = GeometryUtility.TestPlanesAABB(planes, r.bounds);
                    bool layerMatch = (previewCamera.cullingMask & (1 << r.gameObject.layer)) != 0;
                    string matName = r.sharedMaterial != null ? r.sharedMaterial.name : "null";
                    App.Core.AppLog.Onboarding(
                        $"[OnboardingPreviewDebug] {r.name} visibleInFrustum={inFrustum} layerMatch={layerMatch} " +
                        $"layer={r.gameObject.layer} enabled={r.enabled} active={r.gameObject.activeInHierarchy} " +
                        $"bounds.center={r.bounds.center} bounds.size={r.bounds.size} mat={matName}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[OnboardingPreviewDebug] frustum diag failed — {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
