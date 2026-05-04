using System;
using System.Collections.Generic;
using UnityEngine;

namespace App.Infrastructure.Catalogs
{
    /// <summary>
    /// v1.0 customization 카테고리 분류. 항시 7종 + 설치 3종.
    /// RoomEditModeController toolbar 키와 일치 (WALLPAPER, FLOOR, DESK, ...).
    /// </summary>
    public enum CustomizationCategory
    {
        // 항시 7종 — 외형만 변경, 위치/크기 고정
        Wallpaper,
        Floor,
        BookshelfSkin,    // 책장 스킨 기본값(테마 RESET 시 적용). 인스턴스별 외형은 Bookshelf.skinInventoryId
        DashboardSkin,
        Desk,             // 노트북 & 책상 세트
        InteriorLight,
        ExteriorLight,    // 창문 유무와 무관, 항시 동작 (창문 있으면 컷아웃 통과)

        // 설치/삭제 3종 — placement 가능
        BookshelfDeco,    // 책장 섹션 내부에 배치
        FloorDeco,
        WallDeco          // 창문(Window) 포함
    }

    /// <summary>
    /// 한 카탈로그 항목. 카테고리별로 의미 있는 필드만 채우고 나머지는 비움.
    /// — 단순 머티리얼(Wallpaper/Floor/Bookshelf/Dashboard): materials[0]만 사용
    /// — Desk: materials[0]=책상, [1]=상판, [2]=의자 (옵션)
    /// — InteriorLight/ExteriorLight: lightColor + intensity + colorTemperature + (옵션) prefab
    /// — Deco: prefab + placement(anchor/rotation/scale)
    /// </summary>
    [Serializable]
    public sealed class CustomizationEntry
    {
        [Header("Identity")]
        [SerializeField] private long assetId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite previewSprite;

        [Header("Materials (Wallpaper/Floor/Bookshelf/Dashboard/Desk)")]
        [SerializeField] private Material[] materials;

        [Header("Light (InteriorLight/ExteriorLight)")]
        [SerializeField] private Color lightColor = Color.white;
        [SerializeField, Range(0f, 4f)] private float lightIntensity = 1f;
        [SerializeField, Range(2700f, 8000f)] private float colorTemperatureKelvin = 6500f;

        [Header("Prefab (Light fixture / Deco mesh)")]
        [SerializeField] private GameObject prefab;

        [Header("Placement (Deco)")]
        [SerializeField] private Vector3 placementAnchor;
        [SerializeField] private Vector3 placementRotation;
        [SerializeField] private Vector3 placementScale = Vector3.one;

        public long AssetId => assetId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite PreviewSprite => previewSprite;
        public IReadOnlyList<Material> Materials => materials;
        public Material PrimaryMaterial => (materials != null && materials.Length > 0) ? materials[0] : null;
        public Color LightColor => lightColor;
        public float LightIntensity => lightIntensity;
        public float ColorTemperatureKelvin => colorTemperatureKelvin;
        public GameObject Prefab => prefab;
        public Vector3 PlacementAnchor => placementAnchor;
        public Vector3 PlacementRotation => placementRotation;
        public Vector3 PlacementScale => placementScale;
    }

    /// <summary>
    /// 카테고리 하나에 해당하는 항목 리스트.
    /// 자산 1종당 한 SO 파일 — Resources/Catalogs/{Category}Catalog.asset
    /// (DesignPreviewPackCreatorUtility가 placeholder entries 자동 생성).
    /// </summary>
    [CreateAssetMenu(menuName = "Dotshelf/Customization Catalog", fileName = "CustomizationCatalog")]
    public sealed class CustomizationCatalogAsset : ScriptableObject
    {
        [SerializeField] private CustomizationCategory category;
        [SerializeField] private List<CustomizationEntry> entries = new List<CustomizationEntry>();

        public CustomizationCategory Category => category;
        public IReadOnlyList<CustomizationEntry> Entries => entries;

        public CustomizationEntry FindByAssetId(long assetId)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].AssetId == assetId)
                {
                    return entries[i];
                }
            }
            return null;
        }
    }
}
