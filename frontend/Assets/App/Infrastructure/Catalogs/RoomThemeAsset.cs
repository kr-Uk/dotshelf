using System;
using System.Collections.Generic;
using UnityEngine;

namespace App.Infrastructure.Catalogs
{
    /// <summary>
    /// 테마 적용 시 RoomThemeApplier가 자동 spawn할 deco 1개.
    /// prefab을 themeDecoRoot 자식으로 instantiate하고 local 트랜스폼 적용.
    /// AnchorTag는 BookshelfDeco 책장 섹션별 anchoring용 (옵션, follow-up에서 사용).
    /// </summary>
    [Serializable]
    public sealed class RoomThemeDecoSpec
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localRotation;
        [SerializeField] private Vector3 localScale = Vector3.one;
        [SerializeField] private string anchorTag;

        public GameObject Prefab => prefab;
        public Vector3 LocalPosition => localPosition;
        public Vector3 LocalRotation => localRotation;
        public Vector3 LocalScale => localScale;
        public string AnchorTag => anchorTag;
    }

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
        [SerializeField] private Material bookshelfMaterial;       // 책장 스킨 기본값(테마 RESET 시 모든 책장에 적용)
        [SerializeField] private Material furnitureMaterial;       // 책상 본체(legacy 호환)
        [SerializeField] private Material deskTopMaterial;         // 책상 상판(우드 등)
        [SerializeField] private Material chairMaterial;           // 의자
        [SerializeField] private Material dashboardMaterial;       // 대시보드 보드면
        [SerializeField] private Material accentMaterial;

        [Header("Interior lighting (실내 — 천장등/스탠드)")]
        [SerializeField] private Color interiorLightColor = Color.white;
        [SerializeField, Range(2700f, 8000f)] private float interiorLightKelvin = 6500f;
        [SerializeField] private Color ambientTint = Color.white;
        [SerializeField, Range(0f, 2f)] private float ambientIntensity = 1f;

        [Header("Exterior lighting (외부 — 자연광/창문)")]
        [SerializeField] private Color exteriorLightColor = Color.white;
        [SerializeField, Range(2700f, 8000f)] private float exteriorLightKelvin = 6500f;
        [SerializeField, Range(0f, 4f)] private float exteriorLightIntensity = 1f;

        [Header("Customization 권한 — 전체 테마 자산일 때만 의미")]
        [SerializeField] private bool allowsCustomization = true;

        [Header("Accent palette (UI/decoration tint)")]
        [SerializeField] private Color accentPrimary = Color.white;
        [SerializeField] private Color accentSecondary = Color.white;
        [SerializeField] private Color accentTertiary = Color.white;

        [Header("Theme deco placement (RoomThemeApplier 자동 spawn)")]
        [Tooltip("책장 섹션 내부 배치 deco. 빈 list면 spawn 안 함.")]
        [SerializeField] private RoomThemeDecoSpec[] bookshelfDecoSpecs;
        [Tooltip("바닥 배치 deco. 빈 list면 spawn 안 함.")]
        [SerializeField] private RoomThemeDecoSpec[] floorDecoSpecs;
        [Tooltip("벽 배치 deco (창문 포함). 빈 list면 spawn 안 함.")]
        [SerializeField] private RoomThemeDecoSpec[] wallDecoSpecs;

        public long AssetId => assetId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite PreviewSprite => previewSprite;

        public int BookshelfStyleIndex => bookshelfStyleIndex;

        public Material WallMaterial => wallMaterial;
        public Material FloorMaterial => floorMaterial;
        public Material BookshelfMaterial => bookshelfMaterial;
        public Material FurnitureMaterial => furnitureMaterial;
        public Material DeskTopMaterial => deskTopMaterial;
        public Material ChairMaterial => chairMaterial;
        public Material DashboardMaterial => dashboardMaterial;
        public Material AccentMaterial => accentMaterial;

        public Color InteriorLightColor => interiorLightColor;
        public float InteriorLightKelvin => interiorLightKelvin;
        public Color AmbientTint => ambientTint;
        public float AmbientIntensity => ambientIntensity;

        public Color ExteriorLightColor => exteriorLightColor;
        public float ExteriorLightKelvin => exteriorLightKelvin;
        public float ExteriorLightIntensity => exteriorLightIntensity;

        public bool AllowsCustomization => allowsCustomization;

        public Color AccentPrimary => accentPrimary;
        public Color AccentSecondary => accentSecondary;
        public Color AccentTertiary => accentTertiary;

        public IReadOnlyList<RoomThemeDecoSpec> BookshelfDecoSpecs => bookshelfDecoSpecs;
        public IReadOnlyList<RoomThemeDecoSpec> FloorDecoSpecs => floorDecoSpecs;
        public IReadOnlyList<RoomThemeDecoSpec> WallDecoSpecs => wallDecoSpecs;

        // legacy alias — DirectionalLight* 이름으로 외부조명을 가리키던 기존 RoomThemeApplier 호환.
        // 새 코드는 ExteriorLight*를 사용할 것.
        public Color DirectionalLightColor => exteriorLightColor;
        public float DirectionalLightKelvin => exteriorLightKelvin;
    }
}
