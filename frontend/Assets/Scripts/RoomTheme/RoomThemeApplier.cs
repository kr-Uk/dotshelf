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

            int wallSwapped = 0;
            int floorSwapped = 0;

            if (roomRoot != null)
            {
                var renderers = roomRoot.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    var n = r.gameObject.name;
                    if (wallMat != null && IsWallName(n))
                    {
                        r.sharedMaterial = wallMat;
                        wallSwapped++;
                    }
                    else if (floorMat != null && n == FloorObjectName)
                    {
                        r.sharedMaterial = floorMat;
                        floorSwapped++;
                    }
                }
            }

            // BookshelfGenerator의 GameObject 이름이 바뀌면 silent fail이 아니라
            // warning으로 알리도록. roomRoot 자체가 null인 경우는 별개라 위에서 분리.
            if (roomRoot != null && wallMat != null && wallSwapped == 0)
            {
                Debug.LogWarning($"[RoomThemeApplier] Wall renderer 매칭 0개. BookshelfGenerator의 GameObject 이름이 바뀌었거나 roomRoot가 잘못 잡혀있는지 확인하세요. 기대 패턴: {string.Join(",", WallObjectNamePatterns)}");
            }
            if (roomRoot != null && floorMat != null && floorSwapped == 0)
            {
                Debug.LogWarning($"[RoomThemeApplier] Floor renderer 매칭 0개. 기대 이름: '{FloorObjectName}'.");
            }

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
            if (directionalLight != null)
            {
                directionalLight.color = theme.DirectionalLightColor;
                directionalLight.useColorTemperature = true;
                directionalLight.colorTemperature = theme.DirectionalLightKelvin;
            }

            RenderSettings.ambientLight = theme.AmbientTint;
            RenderSettings.ambientIntensity = theme.AmbientIntensity;
        }
    }
}
