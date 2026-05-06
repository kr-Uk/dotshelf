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

        // 테마 자산의 deco spec(bookshelf/floor/wall) 따라 prefab 자동 spawn.
        // 모든 spec 합산 0개면 silent skip — 자산이 spec 안 채운 일반 테마는 이 단계 no-op.
        // spec 있으면 themeDecoRoot 자식 모두 destroy 후 새로 spawn (테마 교체 시 누적 방지).
        private void ApplyDecoSpecs(RoomThemeAsset theme)
        {
            int specCount = (theme.BookshelfDecoSpecs?.Count ?? 0)
                          + (theme.FloorDecoSpecs?.Count ?? 0)
                          + (theme.WallDecoSpecs?.Count ?? 0);

            // 이전 테마가 deco 가졌을 가능성 — root가 이미 있고 자식이 있으면 무조건 정리.
            if (themeDecoRoot != null && themeDecoRoot.childCount > 0)
            {
                ClearThemeDecoChildren();
            }

            if (specCount == 0)
            {
                return;
            }

            var root = EnsureThemeDecoRoot();
            int spawned = 0;
            spawned += SpawnDecoSpecs(theme.BookshelfDecoSpecs, root);
            spawned += SpawnDecoSpecs(theme.FloorDecoSpecs, root);
            spawned += SpawnDecoSpecs(theme.WallDecoSpecs, root);

            Debug.Log($"[RoomThemeApplier] Theme deco spawn — total {spawned}/{specCount} (bookshelf={theme.BookshelfDecoSpecs?.Count ?? 0}, floor={theme.FloorDecoSpecs?.Count ?? 0}, wall={theme.WallDecoSpecs?.Count ?? 0}).");
        }

        // themeDecoRoot가 미설정이면 자식 GameObject "ThemeDecoRoot" 자동 생성. inspector wiring 없이도 동작.
        private Transform EnsureThemeDecoRoot()
        {
            if (themeDecoRoot != null) return themeDecoRoot;
            var go = new GameObject("ThemeDecoRoot");
            go.transform.SetParent(transform, false);
            themeDecoRoot = go.transform;
            return themeDecoRoot;
        }

        private void ClearThemeDecoChildren()
        {
            for (int i = themeDecoRoot.childCount - 1; i >= 0; i--)
            {
                var child = themeDecoRoot.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

        private int SpawnDecoSpecs(System.Collections.Generic.IReadOnlyList<RoomThemeDecoSpec> specs, Transform parent)
        {
            if (specs == null) return 0;
            int spawned = 0;
            for (int i = 0; i < specs.Count; i++)
            {
                var spec = specs[i];
                if (spec == null || spec.Prefab == null) continue;
                var go = Instantiate(spec.Prefab, parent);
                go.transform.localPosition = spec.LocalPosition;
                go.transform.localEulerAngles = spec.LocalRotation;
                go.transform.localScale = spec.LocalScale;
                spawned++;
            }
            return spawned;
        }

        private void ApplyLighting(RoomThemeAsset theme)
        {
            // 외부조명 = 씬의 Directional Light(자연광/창문 통과). RoomThemeAsset의
            // ExteriorLight* 값이 적용된다. (Legacy DirectionalLight* 프로퍼티는 ExteriorLight*에 alias.)
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
