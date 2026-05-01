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

            Debug.Log($"[RoomThemeApplier] Applied theme '{theme.DisplayName}' (assetId={theme.AssetId}).");
        }

        // BookshelfGenerator(.cs:585+)에서 CreateBox로 만드는 GameObject 이름 매칭 규칙.
        // 이 이름들이 바뀌면 silent fail이 아니라 warning 로그로 알려준다(ApplyMaterials 끝).
        const string FloorObjectName = "Floor";
        static readonly string[] WallObjectNamePatterns = { "LeftWall", "RightWall", "BackWall", "FrontWall" };

        // v1.0 customization — 카테고리별 GameObject 이름 prefix 매칭. 정확한 prefab 구조와
        // 다를 수 있으므로 매칭 0개일 때 warning 로그(silent fail 방지). 후속 마디에서
        // BookshelfGenerator에 RoomEnvironmentSurfaces 컴포넌트 노출하는 게 결.
        static readonly string[] BookshelfPatterns = { "BookshelfRoot", "Bookshelf_" };
        static readonly string[] DeskPatterns = { "DeskRoot", "NotebookStation", "Desk_" };
        static readonly string[] DashboardPatterns = { "DashboardScreenPanel", "GuestbookBoard", "Dashboard_" };

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
                    if (wallMat != null && IsWallName(n))
                    {
                        r.sharedMaterial = wallMat; wallSwapped++;
                    }
                    else if (floorMat != null && n == FloorObjectName)
                    {
                        r.sharedMaterial = floorMat; floorSwapped++;
                    }
                    else if (bookshelfMat != null && MatchesAny(n, BookshelfPatterns))
                    {
                        r.sharedMaterial = bookshelfMat; bookshelfSwapped++;
                    }
                    else if (deskMat != null && MatchesAny(n, DeskPatterns))
                    {
                        r.sharedMaterial = deskMat; deskSwapped++;
                    }
                    else if (dashboardMat != null && MatchesAny(n, DashboardPatterns))
                    {
                        r.sharedMaterial = dashboardMat; dashboardSwapped++;
                    }
                }
            }

            // 매칭 0개일 때 warning — silent fail 방지. 머티리얼이 null인 카테고리는 검사 skip.
            if (roomRoot == null) return;
            if (wallMat != null && wallSwapped == 0)
                Debug.LogWarning($"[RoomThemeApplier] Wall 매칭 0개 — 기대 패턴: {string.Join(",", WallObjectNamePatterns)}");
            if (floorMat != null && floorSwapped == 0)
                Debug.LogWarning($"[RoomThemeApplier] Floor 매칭 0개 — 기대 이름: '{FloorObjectName}'");
            if (bookshelfMat != null && bookshelfSwapped == 0)
                Debug.LogWarning($"[RoomThemeApplier] Bookshelf 매칭 0개 — 기대 패턴: {string.Join(",", BookshelfPatterns)}");
            if (deskMat != null && deskSwapped == 0)
                Debug.LogWarning($"[RoomThemeApplier] Desk 매칭 0개 — 기대 패턴: {string.Join(",", DeskPatterns)}");
            if (dashboardMat != null && dashboardSwapped == 0)
                Debug.LogWarning($"[RoomThemeApplier] Dashboard 매칭 0개 — 기대 패턴: {string.Join(",", DashboardPatterns)}");

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

        static bool MatchesAny(string name, string[] patterns)
        {
            for (int i = 0; i < patterns.Length; i++)
            {
                if (name.StartsWith(patterns[i])) return true;
            }
            return false;
        }

        static bool IsWallName(string name)
        {
            for (int i = 0; i < WallObjectNamePatterns.Length; i++)
            {
                if (name == WallObjectNamePatterns[i]) return true;
            }
            return false;
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
