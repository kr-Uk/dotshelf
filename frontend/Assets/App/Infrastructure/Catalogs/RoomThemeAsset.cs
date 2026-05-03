using UnityEngine;

namespace App.Infrastructure.Catalogs
{
    [CreateAssetMenu(menuName = "Dotshelf/Room Theme", fileName = "RoomTheme")]
    public sealed class RoomThemeAsset : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private long assetId;
        [SerializeField] private string displayName;
        [SerializeField] private string description;
        [SerializeField] private Sprite previewSprite;

        [Header("Bookshelf legacy mapping")]
        [Tooltip("BookshelfPorting.Runtime.BookshelfThemeStyle enum의 정수값 — Default=0, Warm=1, Dark=2. " +
                "RoomThemeApplier가 MaterialFactory.ApplyTheme()로 매핑한다.")]
        [SerializeField] private int bookshelfStyleIndex = 0;

        [Header("Surface materials")]
        [SerializeField] private Material wallMaterial;
        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material bookshelfMaterial;
        [SerializeField] private Material furnitureMaterial;
        [SerializeField] private Material accentMaterial;

        [Header("Lighting")]
        [SerializeField] private Color directionalLightColor = Color.white;
        [SerializeField, Range(2700f, 8000f)] private float directionalLightKelvin = 6500f;
        [SerializeField] private Color ambientTint = Color.white;
        [SerializeField, Range(0f, 2f)] private float ambientIntensity = 1f;

        [Header("Accent palette (UI/decoration tint)")]
        [SerializeField] private Color accentPrimary = Color.white;
        [SerializeField] private Color accentSecondary = Color.white;
        [SerializeField] private Color accentTertiary = Color.white;

        public long AssetId => assetId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite PreviewSprite => previewSprite;

        public int BookshelfStyleIndex => bookshelfStyleIndex;

        public Material WallMaterial => wallMaterial;
        public Material FloorMaterial => floorMaterial;
        public Material BookshelfMaterial => bookshelfMaterial;
        public Material FurnitureMaterial => furnitureMaterial;
        public Material AccentMaterial => accentMaterial;

        public Color DirectionalLightColor => directionalLightColor;
        public float DirectionalLightKelvin => directionalLightKelvin;
        public Color AmbientTint => ambientTint;
        public float AmbientIntensity => ambientIntensity;

        public Color AccentPrimary => accentPrimary;
        public Color AccentSecondary => accentSecondary;
        public Color AccentTertiary => accentTertiary;
    }
}
