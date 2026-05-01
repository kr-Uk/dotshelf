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

            if (roomRoot != null)
            {
                var renderers = roomRoot.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    var n = r.gameObject.name;
                    if (wallMat != null && n.Contains("Wall"))
                    {
                        r.sharedMaterial = wallMat;
                    }
                    else if (floorMat != null && n == "Floor")
                    {
                        r.sharedMaterial = floorMat;
                    }
                }
            }

            if (materialFactory != null)
            {
                int idx = Mathf.Clamp(theme.BookshelfStyleIndex, 0, 2);
                materialFactory.ApplyTheme((BookshelfThemeStyle)idx);
            }
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
