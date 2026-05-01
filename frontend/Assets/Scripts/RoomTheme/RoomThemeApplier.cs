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
            if (roomRoot != null)
            {
                var renderers = roomRoot.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    var n = r.gameObject.name;
                    if (theme.WallMaterial != null && n.Contains("Wall"))
                    {
                        r.sharedMaterial = theme.WallMaterial;
                    }
                    else if (theme.FloorMaterial != null && n == "Floor")
                    {
                        r.sharedMaterial = theme.FloorMaterial;
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
