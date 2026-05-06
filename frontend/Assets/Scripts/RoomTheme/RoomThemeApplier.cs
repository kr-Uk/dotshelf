using System.Collections.Generic;
using App.Infrastructure.Catalogs;
using BookshelfPorting.Runtime;
using UnityEngine;

namespace Dotshelf.Scripts.RoomTheme
{
    [DisallowMultipleComponent]
    public sealed class RoomThemeApplier : MonoBehaviour
    {
        [Header("Targets")]
        [Tooltip("벽/바닥 Renderer를 traverse할 룸 루트. BookshelfGenerator가 만든 'Floor'/'*Wall' GameObject들의 부모.")]
        [SerializeField] private Transform roomRoot;
        [Tooltip("책장/가구 색상 분기. 새 머티리얼이 우선 적용되고 이건 fallback tint.")]
        [SerializeField] private MaterialFactory materialFactory;
        [Tooltip("테마별 색온도/색을 적용할 메인 라이트.")]
        [SerializeField] private Light directionalLight;

        [Header("Theme deco")]
        [Tooltip("테마 적용 시 spawn된 deco의 부모. 비워두면 Apply 첫 호출 시 자식 'ThemeDecoRoot' 자동 생성. " +
                 "user-placed deco(RoomEditModeController.placedItemsRoot)와 분리되어 테마 교체 시 안전하게 정리.")]
        [SerializeField] private Transform themeDecoRoot;

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
            ApplyDecoSpecs(theme);

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

            int wallSwapped = 0, floorSwapped = 0;
            int bookshelfSwapped = 0, deskSwapped = 0, dashboardSwapped = 0;

            if (roomRoot != null)
            {
                var renderers = roomRoot.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    var n = r.gameObject.name;
                    if (wallMat != null && RoomGameObjectPatterns.IsWallName(n))
                    {
                        r.sharedMaterial = wallMat; wallSwapped++;
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
                }
                else
                {
                    Debug.LogWarning($"[RoomThemeApplier] bookshelfStyleIndex={theme.BookshelfStyleIndex}는 BookshelfThemeStyle enum 범위 밖 — 책장 tint 적용 skip.");
                }
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
                          + (theme.WallDecoSpecs?.Count ?? 0)
                          + (theme.WindowSpec != null && theme.WindowSpec.Prefab != null ? 1 : 0);

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
            spawned += SpawnWindowSpec(theme.WindowSpec, root);

            Debug.Log($"[RoomThemeApplier] Theme deco spawn — total {spawned}/{specCount} (bookshelf={theme.BookshelfDecoSpecs?.Count ?? 0}, floor={theme.FloorDecoSpecs?.Count ?? 0}, wall={theme.WallDecoSpecs?.Count ?? 0}, window={(theme.WindowSpec?.Prefab != null ? 1 : 0)}).");
        }

        // window prefab 은 외부조명(directionalLight)을 cutout shader 로 통과시키는 mesh.
        // 빛 자체는 ApplyLighting 의 directionalLight 가 항시 처리 — 창문 없어도 외부조명은 동작.
        // wallSide 매칭 transform 자식으로 spawn (없으면 fallbackParent — themeDecoRoot).
        private int SpawnWindowSpec(RoomThemeDecoSpec spec, Transform fallbackParent)
        {
            if (spec == null || spec.Prefab == null) return 0;
            var wallParent = ResolveWallTransform(spec.WallSide) ?? fallbackParent;
            return SpawnSingleSpec(spec, wallParent);
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

        // wall deco 는 spec.WallSide 가 명시한 wall transform 자식으로 spawn — local 좌표계가
        // 그 벽 기준이 되어 spec authoring 시 '왼쪽 벽 가운데 (0, 1.5, 0)' 같은 위치 표기 가능.
        // wallSide==None 또는 매칭 wall 못 찾으면 fallback parent (themeDecoRoot) 자식으로 spawn.
        private int SpawnWallDecoSpecs(IReadOnlyList<RoomThemeDecoSpec> specs, Transform fallbackParent)
        {
            if (specs == null) return 0;
            int spawned = 0;
            for (int i = 0; i < specs.Count; i++)
            {
                var spec = specs[i];
                if (spec == null || spec.Prefab == null) continue;
                var wallParent = ResolveWallTransform(spec.WallSide) ?? fallbackParent;
                spawned += SpawnSingleSpec(spec, wallParent);
            }
            return spawned;
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

        // WallSide → 룸의 wall GameObject transform. RoomGameObjectPatterns.Wall과 1:1 매핑.
        // roomRoot 미설정이거나 매칭 wall GameObject 없으면 null. 호출부는 fallback 처리.
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
            // 외부조명 = 씬의 Directional Light. 항시 동작 — 창문(theme.WindowSpec) 유무와 무관.
            // 창문 prefab 은 cutout shader 로 이 빛을 mesh 로 시각화할 뿐이고, 빛 자체는 별도 컴포넌트.
            // 즉 창문 없는 테마도 자연광은 들어오고, 창문 있는 테마는 cutout 으로 빛이 통과되어 보임.
            // RoomThemeAsset의 ExteriorLight* 값이 적용된다. (Legacy DirectionalLight* 프로퍼티는
            // ExteriorLight*에 alias.)
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
