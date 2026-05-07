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
        // 테마 태그 — 빈 문자열이면 무테마(공통) entry. 상단 메뉴 테마 필터(Editor/RoomEditThemeFilterMenu)
        // 적용 시 빈 태그는 어떤 테마에서도 노출, 비빈 태그는 일치 시에만 노출.
        [SerializeField] private string themeTag;
        // local preview 전용 entry 표시 — 클릭 시 서버 PATCH 를 발사하지 않고 로컬 씬 visual swap 만 수행.
        // placeholder 카탈로그가 생성한 entry 는 사용자 inventory 에 없어 PATCH 가 403 (S006) 으로 거부되므로,
        // 이 플래그가 true 면 RoomEditModeController.OnCustomizationEntrySelected 가 PATCH 단계를 skip.
        [SerializeField] private bool isLocalPreview;

        [Header("Materials (Wallpaper/Floor/Bookshelf/Dashboard/Desk)")]
        [SerializeField] private Material[] materials;

        [Header("Light (InteriorLight/ExteriorLight)")]
        [SerializeField] private Color lightColor = Color.white;
        [SerializeField, Range(0f, 4f)] private float lightIntensity = 1f;
        [SerializeField, Range(2700f, 8000f)] private float colorTemperatureKelvin = 6500f;

        [Header("Prefab (Light fixture / Deco mesh)")]
        [SerializeField] private GameObject prefab;

        [Header("Placement (Deco / Desk mesh swap)")]
        [Tooltip("mesh swap 시 native anchor (예: DeskRoot) 의 world position 에 더할 offset.")]
        [SerializeField] private Vector3 placementAnchor;
        [Tooltip("mesh swap 시 native anchor rotation 위에 추가로 적용할 회전 (Euler).")]
        [SerializeField] private Vector3 placementRotation;
        [Tooltip("spawn 된 prefab 의 transform.localScale. Vector3.one 기본.")]
        [SerializeField] private Vector3 placementScale = Vector3.one;

        [Header("Desk 전용 — NotebookStation override (모니터 진입 위치 fit)")]
        [Tooltip("true 면 Desk swap 시 NotebookStation 의 transform 을 아래 값들로 override. " +
                 "false 면 native 그대로. 다른 책상 모델 적용 시 모니터 화면/클릭 인터랙션 위치 fit.")]
        [SerializeField] private bool overrideNotebookStation;
        [Tooltip("NotebookStation 의 world position. Inspector 에서 직접 입력.")]
        [SerializeField] private Vector3 notebookStationPosition;
        [Tooltip("NotebookStation 의 world rotation (Euler).")]
        [SerializeField] private Vector3 notebookStationRotation;
        [Tooltip("NotebookStation 의 localScale.")]
        [SerializeField] private Vector3 notebookStationScale = Vector3.one;

        public long AssetId => assetId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite PreviewSprite => previewSprite;
        public string ThemeTag => themeTag;
        public bool IsLocalPreview => isLocalPreview;
        public IReadOnlyList<Material> Materials => materials;
        public Material PrimaryMaterial => (materials != null && materials.Length > 0) ? materials[0] : null;
        public Color LightColor => lightColor;
        public float LightIntensity => lightIntensity;
        public float ColorTemperatureKelvin => colorTemperatureKelvin;
        public GameObject Prefab => prefab;
        public Vector3 PlacementAnchor => placementAnchor;
        public Vector3 PlacementRotation => placementRotation;
        public Vector3 PlacementScale => placementScale;
        public bool OverrideNotebookStation => overrideNotebookStation;
        public Vector3 NotebookStationPosition => notebookStationPosition;
        public Vector3 NotebookStationRotation => notebookStationRotation;
        public Vector3 NotebookStationScale => notebookStationScale;
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
