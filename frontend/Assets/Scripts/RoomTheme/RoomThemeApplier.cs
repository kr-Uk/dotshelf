using System.Collections.Generic;
using App.Bootstrap.Exterior;
using App.Infrastructure.Catalogs;
using BookshelfPorting.Runtime;
using UnityEngine;

namespace Dotshelf.Scripts.RoomTheme
{
    [DisallowMultipleComponent]
    public sealed class RoomThemeApplier : MonoBehaviour
    {
        [Header("Targets")]
        [Tooltip("벽/천장/바닥 Renderer를 traverse할 룸 루트. BookshelfGenerator가 만든 'Floor'/'Ceiling'/'*Wall' GameObject들의 부모.")]
        [SerializeField] private Transform roomRoot;
        [Tooltip("책장/가구 색상 분기. 새 머티리얼이 우선 적용되고 이건 fallback tint.")]
        [SerializeField] private MaterialFactory materialFactory;
        [Tooltip("테마별 색온도/색을 적용할 메인 라이트. exteriorApplier 가 wired 되어 있고 " +
                 "theme.DefaultExterior 가 있으면 ExteriorApplier 가 이 light 을 갱신 (RoomThemeApplier 직접 갱신 skip).")]
        [SerializeField] private Light directionalLight;

        [Tooltip("Tier 1.5+: theme.DefaultExterior 가 있으면 RoomThemeApplier 가 sun/ambient 직접 처리하지 않고 " +
                 "이 ExteriorApplier 에 위임 (skybox/ambient/fog/reflection/sun/scenePrefab 모두 흡수). " +
                 "null 이거나 theme.DefaultExterior 가 null 이면 fallback 으로 RoomThemeAsset 의 " +
                 "ExteriorLight*/ambient* 직접 적용. 노션 페이지: 'Exterior 통합 시스템 (260510)'.")]
        [SerializeField] private ExteriorApplier exteriorApplier;

        [Header("Theme deco")]
        [Tooltip("테마 적용 시 spawn된 deco의 부모. 비워두면 Apply 첫 호출 시 자식 'ThemeDecoRoot' 자동 생성. " +
                 "user-placed deco(RoomEditModeController.placedItemsRoot)와 분리되어 테마 교체 시 안전하게 정리.")]
        [SerializeField] private Transform themeDecoRoot;

        [Header("Wallpaper tiling")]
        [Tooltip("벽지 텍스처 한 cycle 이 차지할 world-space 크기 (m). " +
                 "x = 가로 1 cycle 길이, y = 세로 1 cycle 길이. " +
                 "WallTilingNormalizer 가 각 wall renderer 의 face 크기에 비례한 _BaseMap_ST 를 " +
                 "MaterialPropertyBlock 으로 설정해 모든 벽이 동일 texel density 를 갖게 한다. " +
                 "Default (1.0, 2.8): 가로 1m, 세로 RoomHeight 당 한 번 반복. " +
                 "BackWall 사이드 strip (~1m × 2.8m) 은 거의 (1,1) 로 현재와 유사하게 보이고, " +
                 "LeftWall/RightWall (5m × 2.8m) 은 가로 5번 반복해 BackWall 과 패턴 밀도 일치. " +
                 "패턴이 너무 작게 보이면 값 키우고(예: 1.5, 2.8), 너무 크면 줄인다(예: 0.7, 2.0).")]
        [SerializeField] private Vector2 wallPatternWorldSize = new Vector2(1.0f, 2.8f);

        public Vector2 WallPatternWorldSize => wallPatternWorldSize;

        [Header("State")]
        [SerializeField] private RoomThemeAsset currentTheme;

        public RoomThemeAsset CurrentTheme => currentTheme;

        public void Apply(RoomThemeAsset theme)
        {
            if (theme == null)
            {
                Debug.LogWarning("[RoomThemeApplier] Apply called with null theme — skipping.");
                return;
            }

            currentTheme = theme;
            ApplyMaterials(theme);
            ApplyLighting(theme);
            // ApplyDecoSpecs 자동 호출 폐기 — 테마 deco specs 는 RoomEditModeController.
            // ApplyThemeDefaultEntries 가 사용자 placement 시스템 (placedItemsRoot 자식 +
            // DecorationItemMarker + drag/select 가능) 으로 통합 처리. 사용자가 자유롭게
            // 옮기거나 삭제 가능. ApplyDecoSpecs 메서드 자체는 retain (legacy / 외부 호출 가능).

            Debug.Log($"[RoomThemeApplier] Applied theme '{theme.DisplayName}' (assetId={theme.AssetId}).");
        }

        // 매칭 패턴은 RoomGameObjectPatterns에 통합 — RoomEditModeController도 같은 패턴 사용.
        // BookshelfGenerator의 GameObject 이름이 바뀌면 그 한 파일만 갱신.

        // 외부에서 룸 root + Directional Light를 조회할 수 있도록 노출 (RoomEditModeController가
        // 카테고리별 visual swap 시 GameObject.Find/FindObjectsByType 매번 호출 안 하도록).
        public Transform RoomRoot => roomRoot;
        public Light DirectionalLight => directionalLight;

        private void ApplyMaterials(RoomThemeAsset theme)
        {
            // 테마 머티리얼이 null이면 MaterialFactory의 기본 머티리얼로 복원
            // — '기본' 테마(머티리얼 모두 null)로 돌아갈 때 산뜻 머티리얼이 그대로
            // 남는 것을 막는다.
            Material wallMat = theme.WallMaterial != null
                ? theme.WallMaterial
                : (materialFactory != null ? materialFactory.GetWallMaterial() : null);
            Material floorMat = theme.FloorMaterial != null
                ? theme.FloorMaterial
                : (materialFactory != null ? materialFactory.GetFloorMaterial() : null);
            Material bookshelfMat = theme.BookshelfMaterial; // null이면 swap skip
            Material deskMat = theme.FurnitureMaterial;
            Material dashboardMat = theme.DashboardMaterial;

            int wallSwapped = 0, ceilingSwapped = 0, floorSwapped = 0;
            int bookshelfSwapped = 0, deskSwapped = 0, dashboardSwapped = 0;

            if (roomRoot != null)
            {
                var renderers = roomRoot.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    var n = r.gameObject.name;
                    if (wallMat != null && RoomGameObjectPatterns.IsWallpaperSurfaceName(n))
                    {
                        r.sharedMaterial = wallMat;
                        if (RoomGameObjectPatterns.IsCeilingName(n))
                        {
                            ceilingSwapped++;
                            WallTilingNormalizer.ApplyHorizontalSurface(r, wallPatternWorldSize);
                        }
                        else
                        {
                            wallSwapped++;
                            // wall 의 face 크기가 LR(5×2.8) vs BackWall strip(~1×2.8) 으로 크게 달라
                            // 동일 material 의 tiling=(1,1) 만으로는 좌/우가 5× stretch 되어 보임.
                            // MPB 로 face-비례 _BaseMap_ST 를 set → 동일 world-space texel density.
                            WallTilingNormalizer.Apply(r, wallPatternWorldSize);
                        }
                    }
                    else if (floorMat != null && n == RoomGameObjectPatterns.Floor)
                    {
                        r.sharedMaterial = floorMat; floorSwapped++;
                    }
                    else if (bookshelfMat != null && RoomGameObjectPatterns.MatchesPrefix(n, RoomGameObjectPatterns.Bookshelf))
                    {
                        r.sharedMaterial = bookshelfMat; bookshelfSwapped++;
                    }
                    else if (deskMat != null && RoomGameObjectPatterns.MatchesPrefix(n, RoomGameObjectPatterns.Desk))
                    {
                        r.sharedMaterial = deskMat; deskSwapped++;
                    }
                    else if (dashboardMat != null && RoomGameObjectPatterns.MatchesPrefix(n, RoomGameObjectPatterns.Dashboard))
                    {
                        r.sharedMaterial = dashboardMat; dashboardSwapped++;
                    }
                }
            }

            // 매칭 0개일 때 warning — silent fail 방지. 머티리얼이 null인 카테고리는 검사 skip.
            if (roomRoot == null) return;
            if (wallMat != null && wallSwapped == 0)
                Debug.LogWarning($"[RoomThemeApplier] Wall 매칭 0개 — 기대 패턴: {string.Join(",", RoomGameObjectPatterns.Wall)}");
            if (wallMat != null && ceilingSwapped == 0)
                Debug.LogWarning($"[RoomThemeApplier] Ceiling 매칭 0개 — 기대 이름: '{RoomGameObjectPatterns.Ceiling}'");
            if (floorMat != null && floorSwapped == 0)
                Debug.LogWarning($"[RoomThemeApplier] Floor 매칭 0개 — 기대 이름: '{RoomGameObjectPatterns.Floor}'");
            if (bookshelfMat != null && bookshelfSwapped == 0)
                Debug.LogWarning($"[RoomThemeApplier] Bookshelf 매칭 0개 — 기대 패턴: {string.Join(",", RoomGameObjectPatterns.Bookshelf)}");
            if (deskMat != null && deskSwapped == 0)
                Debug.LogWarning($"[RoomThemeApplier] Desk 매칭 0개 — 기대 패턴: {string.Join(",", RoomGameObjectPatterns.Desk)}");
            if (dashboardMat != null && dashboardSwapped == 0)
                Debug.LogWarning($"[RoomThemeApplier] Dashboard 매칭 0개 — 기대 패턴: {string.Join(",", RoomGameObjectPatterns.Dashboard)}");

            if (materialFactory != null)
            {
                var style = (BookshelfThemeStyle)theme.BookshelfStyleIndex;
                if (System.Enum.IsDefined(typeof(BookshelfThemeStyle), style))
                {
                    materialFactory.ApplyTheme(style);
                    PropagateBookshelfDesignStyleToGenerator(style);
                }
                else
                {
                    Debug.LogWarning($"[RoomThemeApplier] bookshelfStyleIndex={theme.BookshelfStyleIndex}는 BookshelfThemeStyle enum 범위 밖 — 책장 tint 적용 skip.");
                }
            }
        }

        // RoomTheme.bookshelfStyleIndex 가 Default 이외 값을 가리키면 절차생성 shell 대신
        // BookshelfThemeLayoutCatalog 의 매칭 GLB 를 functional shell 로 spawn — BookshelfGenerator
        // 의 bookshelfDesignStyles dict 까지 propagate 해야 활성화. materialFactory.ApplyTheme 만으론
        // tint 만 바뀜.
        private BookshelfGenerator cachedBookshelfGenerator;

        private void PropagateBookshelfDesignStyleToGenerator(BookshelfThemeStyle style)
        {
            if (cachedBookshelfGenerator == null)
            {
                cachedBookshelfGenerator = FindFirstObjectByType<BookshelfGenerator>(FindObjectsInactive.Include);
            }
            if (cachedBookshelfGenerator == null) return;

            bool applied = cachedBookshelfGenerator.SetActiveBookshelfDesignStyle(style);
            if (!applied)
            {
                Debug.Log($"[RoomThemeApplier] BookshelfGenerator.SetActiveBookshelfDesignStyle({style}) skipped — bookshelf not ready 또는 동일 style. 다음 user 액션 시점에 자연 재동기화.");
            }
        }

        // applier 가 spawn한 deco만 명시적으로 추적. childCount 기반 정리는 Destroy 지연 race
        // 발생 가능 — 같은 프레임에 Apply 가 두 번 들어오면 첫 호출의 destroy 예약된 자식이
        // 두번째 호출 시점에 여전히 root.childCount 에 남아 있어, 두번째 ClearThemeDecoChildren
        // 가 새로 spawn된 deco 까지 destroy 예약하는 상황이 생김. 이 리스트는 spawn/clear 모두
        // 명시 관리해 race 회피.
        private readonly List<GameObject> spawnedThemeDecos = new List<GameObject>();

        // 테마 자산의 deco spec(bookshelf/floor/wall) 따라 prefab 자동 spawn.
        // 모든 spec 합산 0개면 spawn은 skip되지만 이전 테마가 남긴 deco는 정리됨.
        private void ApplyDecoSpecs(RoomThemeAsset theme)
        {
            int specCount = (theme.BookshelfDecoSpecs?.Count ?? 0)
                          + (theme.FloorDecoSpecs?.Count ?? 0)
                          + (theme.WallDecoSpecs?.Count ?? 0);

            // 이전 테마가 spawn한 deco 정리. spawnedThemeDecos만 destroy — 다른 자식(외부에서
            // 추가했거나 inspector-wired root에 미리 있던 GameObject)은 건드리지 않음.
            ClearSpawnedThemeDecos();

            if (specCount == 0)
            {
                return;
            }

            var root = EnsureThemeDecoRoot();
            int spawned = 0;
            spawned += SpawnDecoSpecs(theme.BookshelfDecoSpecs, root);
            spawned += SpawnDecoSpecs(theme.FloorDecoSpecs, root);
            spawned += SpawnWallDecoSpecs(theme.WallDecoSpecs, root);

            Debug.Log($"[RoomThemeApplier] Theme deco spawn — total {spawned}/{specCount} (bookshelf={theme.BookshelfDecoSpecs?.Count ?? 0}, floor={theme.FloorDecoSpecs?.Count ?? 0}, wall={theme.WallDecoSpecs?.Count ?? 0}).");
        }

        // themeDecoRoot 가 미설정이면 자동 생성. 부모는 roomRoot 우선 (theme deco spec 의 local
        // 좌표계가 룸 좌표 기준이라는 작성자 기대를 만족). roomRoot 도 없으면 applier transform fallback.
        private Transform EnsureThemeDecoRoot()
        {
            if (themeDecoRoot != null) return themeDecoRoot;
            var parent = roomRoot != null ? roomRoot : transform;
            var go = new GameObject("ThemeDecoRoot");
            go.transform.SetParent(parent, false);
            themeDecoRoot = go.transform;
            return themeDecoRoot;
        }

        private void ClearSpawnedThemeDecos()
        {
            for (int i = spawnedThemeDecos.Count - 1; i >= 0; i--)
            {
                var child = spawnedThemeDecos[i];
                if (child == null) continue;
                // Destroy 지연 race 회피 — destroy 예약 즉시 비활성화해서 한 프레임 동안
                // 이전 deco 가 보이지 않게 + 다음 Apply 가 동일 프레임에 들어와도 영향 없음.
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
            spawnedThemeDecos.Clear();
        }

        private int SpawnDecoSpecs(IReadOnlyList<RoomThemeDecoSpec> specs, Transform parent)
        {
            if (specs == null) return 0;
            int spawned = 0;
            for (int i = 0; i < specs.Count; i++)
            {
                spawned += SpawnSingleSpec(specs[i], parent);
            }
            return spawned;
        }

        // wall deco — wall transform 을 직접 parent 로 안 씀 (BookshelfGenerator.CreateBox 가
        // wall 의 actual size 를 transform.localScale 에 넣음 — 그 자식이 되면 spec 의
        // localPosition/Scale 이 wall 의 non-uniform scale 에 곱해져 deco 가 찌그러짐).
        // 대신 themeDecoRoot (scale 1) 자식 + wall surface anchor 의 world coord 변환.
        //
        // Authoring 좌표계: spec.LocalPosition 은 wall surface 기준 (X/Y 는 wall 평면, Z 는
        // 벽에서 룸 안쪽 방향). TryGetWallSurfaceAnchor 가 wall 의 inner face center 와
        // 정면 향한 회전을 반환 → spec local 을 그 frame 에서 world 로 변환.
        private int SpawnWallDecoSpecs(IReadOnlyList<RoomThemeDecoSpec> specs, Transform fallbackParent)
        {
            if (specs == null) return 0;
            int spawned = 0;
            for (int i = 0; i < specs.Count; i++)
            {
                var spec = specs[i];
                if (spec == null || spec.Prefab == null) continue;
                spawned += SpawnWallSpecAtAnchor(spec, fallbackParent);
            }
            return spawned;
        }

        // wall anchor world 좌표계로 spec 적용. anchor 못 찾으면 fallbackParent local 로 fallback
        // + warning. parent 는 항상 fallbackParent (scale 1) — wall mesh scale 영향 차단.
        private int SpawnWallSpecAtAnchor(RoomThemeDecoSpec spec, Transform fallbackParent)
        {
            if (spec == null || spec.Prefab == null) return 0;

            var go = Instantiate(spec.Prefab, fallbackParent);
            spawnedThemeDecos.Add(go);

            if (TryGetWallSurfaceAnchor(spec.WallSide, out var anchorPos, out var anchorRot))
            {
                // wall surface frame 의 local → world 변환
                var worldPos = anchorPos + anchorRot * spec.LocalPosition;
                var worldRot = anchorRot * Quaternion.Euler(spec.LocalRotation);
                go.transform.SetPositionAndRotation(worldPos, worldRot);
                go.transform.localScale = spec.LocalScale;
            }
            else
            {
                if (spec.WallSide != WallSide.None)
                {
                    Debug.LogWarning($"[RoomThemeApplier] WallSide.{spec.WallSide} 의 wall transform 못 찾음 — themeDecoRoot 기준 fallback. Wall 이름이 'LeftWall'/'RightWall'/'BackWall' (또는 *_Strip suffix) 인지 확인.");
                }
                go.transform.localPosition = spec.LocalPosition;
                go.transform.localEulerAngles = spec.LocalRotation;
                go.transform.localScale = spec.LocalScale;
            }
            return 1;
        }

        private int SpawnSingleSpec(RoomThemeDecoSpec spec, Transform parent)
        {
            if (spec == null || spec.Prefab == null) return 0;
            var go = Instantiate(spec.Prefab, parent);
            go.transform.localPosition = spec.LocalPosition;
            go.transform.localEulerAngles = spec.LocalRotation;
            go.transform.localScale = spec.LocalScale;
            spawnedThemeDecos.Add(go);
            return 1;
        }

        // wall 의 inner face center world position + rotation 산출. wall mesh transform 의
        // scale 영향 안 받도록 mesh 의 worldBounds 사용 + strips 다중 매칭 (BackWall 처럼
        // 분할 wall 도 통합). rotation 은 wall normal (룸 안쪽 방향) 을 +Z 로 하는 LookRotation.
        //
        // RoomGameObjectPatterns.IsWallName 의 prefix 매칭 사용 → BackWall_LeftStrip 등 strips
        // 모두 같은 wall 로 인식 + bounds 통합. wallSide=Back 시 strips 의 union bounds 의
        // inner face center 가 anchor.
        public bool TryGetWallSurfaceAnchor(WallSide wall, out Vector3 worldPos, out Quaternion worldRot)
        {
            worldPos = Vector3.zero;
            worldRot = Quaternion.identity;
            if (wall == WallSide.None || roomRoot == null) return false;

            Bounds? unionBounds = null;
            var renderers = roomRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var name = renderers[i].gameObject.name;
                if (!RoomGameObjectPatterns.IsWallName(name)) continue;
                if (WallSideForName(name) != wall) continue;
                if (unionBounds == null) unionBounds = renderers[i].bounds;
                else { var b = unionBounds.Value; b.Encapsulate(renderers[i].bounds); unionBounds = b; }
            }
            if (unionBounds == null) return false;

            // wall normal (룸 안쪽 방향) + inner face center
            Vector3 normal;
            Vector3 surfaceCenter = unionBounds.Value.center;
            switch (wall)
            {
                case WallSide.Left:
                    normal = Vector3.right;
                    surfaceCenter.x = unionBounds.Value.max.x; // inner face = max.x (룸 안쪽 방향)
                    break;
                case WallSide.Right:
                    normal = Vector3.left;
                    surfaceCenter.x = unionBounds.Value.min.x;
                    break;
                case WallSide.Back:
                    normal = Vector3.back;
                    surfaceCenter.z = unionBounds.Value.min.z;
                    break;
                case WallSide.Front:
                    normal = Vector3.forward;
                    surfaceCenter.z = unionBounds.Value.max.z;
                    break;
                default: return false;
            }
            worldPos = surfaceCenter;
            // anchor 의 +Z 가 wall 의 inner face normal (룸 안쪽). spec 작성자가
            // localPosition.z 양수로 두면 wall 에서 룸 안쪽으로 나옴.
            worldRot = Quaternion.LookRotation(normal);
            return true;
        }

        // RoomGameObjectPatterns 의 prefix 매칭과 일치하는 GameObject name → WallSide 변환.
        // RoomEditModeController 의 같은 이름 함수와 동일 로직.
        private static WallSide WallSideForName(string name)
        {
            if (name == "LeftWall" || name.StartsWith("LeftWall_")) return WallSide.Left;
            if (name == "RightWall" || name.StartsWith("RightWall_")) return WallSide.Right;
            if (name == "BackWall" || name.StartsWith("BackWall_")) return WallSide.Back;
            if (name == "FrontWall" || name.StartsWith("FrontWall_")) return WallSide.Front;
            return WallSide.None;
        }

        // legacy — ResolveWallTransform 은 wall mesh transform 을 직접 parent 로 사용했었음.
        // wall 의 non-uniform scale 이 spec 좌표계에 곱해지는 [P1] 버그로 폐기. anchor 기반 변환
        // (TryGetWallSurfaceAnchor) 으로 대체. 호출부 없음.
        [System.Obsolete("Use TryGetWallSurfaceAnchor — wall mesh scale 영향 회피")]
        private Transform ResolveWallTransform(WallSide side)
        {
            if (side == WallSide.None || roomRoot == null) return null;
            string wallName;
            switch (side)
            {
                case WallSide.Left:  wallName = "LeftWall";  break;
                case WallSide.Right: wallName = "RightWall"; break;
                case WallSide.Back:  wallName = "BackWall";  break;
                case WallSide.Front: wallName = "FrontWall"; break;
                default: return null;
            }
            var transforms = roomRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].gameObject.name == wallName) return transforms[i];
            }
            return null;
        }

        private void ApplyLighting(RoomThemeAsset theme)
        {
            // Tier 1.5+: theme.DefaultExterior (ExteriorAsset) 가 있고 exteriorApplier 가 wired 면
            // ExteriorAsset 시스템에 위임. ExteriorApplier.Apply 가 sun/skybox/ambient/fog/reflection/
            // scenePrefab 을 통합 처리한다. 이 경로에서는 RoomThemeApplier 가 sun/ambient 를 직접 만지지 않음.
            if (theme.DefaultExterior != null && exteriorApplier != null)
            {
                exteriorApplier.Apply(theme.DefaultExterior);
                return;
            }

            // Fallback (Tier 1.5 이전 동작 호환, Tier 2 에서 제거 예정):
            // theme.DefaultExterior 가 null 인 RoomTheme (default/Natural/Classic 등 미마이그레이션 자산)
            // 또는 exteriorApplier 미wiring 시 RoomTheme.ExteriorLight* + ambient* 를 직접 적용.
            //
            // 외부조명 = 씬의 Directional Light. 항시 동작 — 창문 유무와 무관.
            // 창문 prefab 은 cutout shader 로 이 빛을 mesh 로 시각화할 뿐이고, 빛 자체는 별도 컴포넌트.
            if (directionalLight != null)
            {
                directionalLight.color = theme.ExteriorLightColor;
                directionalLight.useColorTemperature = true;
                directionalLight.colorTemperature = theme.ExteriorLightKelvin;
                directionalLight.intensity = theme.ExteriorLightIntensity;
            }

            // 내부조명 = ambient(천장등/스탠드 톤). InteriorLight color/kelvin은
            // ambient에 직접 영향이 없어 기본은 ambientTint/intensity. 향후 별도 InteriorLight
            // GameObject 슬롯이 추가되면 여기에 적용 (TODO).
            RenderSettings.ambientLight = theme.AmbientTint;
            RenderSettings.ambientIntensity = theme.AmbientIntensity;
        }
    }
}
