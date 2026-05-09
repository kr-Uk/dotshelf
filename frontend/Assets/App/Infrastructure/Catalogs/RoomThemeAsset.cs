using System;
using System.Collections.Generic;
using UnityEngine;

namespace App.Infrastructure.Catalogs
{
    /// <summary>
    /// 룸의 4벽 식별자. WallDecoSpec에서 어느 벽에 붙는 deco 인지 명시.
    /// RoomGameObjectPatterns.Wall ('LeftWall'/'RightWall'/'BackWall'/'FrontWall')과 1:1.
    /// None은 wallSide 무관 (BookshelfDecoSpec/FloorDecoSpec 용).
    /// </summary>
    public enum WallSide
    {
        None,
        Left,
        Right,
        Back,
        Front,
    }

    /// <summary>
    /// 테마 적용 시 자동으로 set 할 카테고리별 default entry assetId.
    /// 예: 산뜻 테마에 (Desk, 201) 등록하면 테마 적용 시 DeskCatalog.assetId=201 entry
    /// (GoodVibes_Desk) 가 자동으로 swap. 사용자가 추후 다른 entry 선택하면 그것이 우선.
    /// 이로써 테마 적용 = 카테고리별 default 자산 일괄 적용 시스템 (mesh swap 통합).
    /// </summary>
    [Serializable]
    public sealed class CategoryDefault
    {
        [SerializeField] private CustomizationCategory category;
        [SerializeField] private long assetId;
        public CustomizationCategory Category => category;
        public long AssetId => assetId;
    }

    /// <summary>
    /// 테마 적용 시 RoomThemeApplier가 자동 spawn할 deco 1개.
    /// prefab을 themeDecoRoot(또는 wallSide 매칭 wall transform) 자식으로 instantiate하고
    /// local 트랜스폼 적용.
    ///
    /// AssetId — catalog/BE 참조용 메타. 0이면 카탈로그 무관(prefab만 사용).
    /// WallSide — WallDecoSpec 전용. 어느 벽에 붙는지 명시. 그 외 spec list는 None 유지.
    /// AnchorTag — BookshelfDeco 책장 섹션별 anchoring용 (옵션, follow-up).
    /// </summary>
    [Serializable]
    public sealed class RoomThemeDecoSpec
    {
        [SerializeField] private long assetId;
        [SerializeField] private GameObject prefab;
        [SerializeField] private WallSide wallSide = WallSide.None;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localRotation;
        [SerializeField] private Vector3 localScale = Vector3.one;
        [SerializeField] private string anchorTag;

        public long AssetId => assetId;
        public GameObject Prefab => prefab;
        public WallSide WallSide => wallSide;
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

        // local preview 전용 — 클릭 시 RoomThemeApplier 만 호출하고 백엔드 POST /spaces/me/theme/.../apply 는 skip.
        // 가구/벽지/바닥 미제작 단계의 placeholder 테마(Natural/Classic/...) 가 사용자 inventory 에 없어 403 으로 거부되는 것을 회피.
        [SerializeField] private bool isLocalPreview;

        [Header("Accent palette (UI/decoration tint)")]
        [SerializeField] private Color accentPrimary = Color.white;
        [SerializeField] private Color accentSecondary = Color.white;
        [SerializeField] private Color accentTertiary = Color.white;

        [Header("Theme default entries (자동 카테고리 swap)")]
        [Tooltip("테마 적용 시 자동으로 set 할 카테고리별 default assetId. 예: 산뜻 테마 = " +
                 "Desk:201 (GoodVibes_Desk). 사용자 선택이 우선이라 placement 후 override 가능.")]
        [SerializeField] private CategoryDefault[] defaultEntries;

        [Header("Theme deco placement (RoomThemeApplier 자동 spawn)")]
        [Tooltip("책장 섹션 내부 배치 deco. 빈 list면 spawn 안 함.")]
        [SerializeField] private RoomThemeDecoSpec[] bookshelfDecoSpecs;
        [Tooltip("바닥 배치 deco. 빈 list면 spawn 안 함.")]
        [SerializeField] private RoomThemeDecoSpec[] floorDecoSpecs;
        [Tooltip("벽 배치 deco. 빈 list면 spawn 안 함. 창문도 이 list 의 일반 entry — " +
                 "windowSpec 별도 필드는 폐기됨 (사용자 placement workflow 로 통합).")]
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
        public bool IsLocalPreview => isLocalPreview;

        public Color AccentPrimary => accentPrimary;
        public Color AccentSecondary => accentSecondary;
        public Color AccentTertiary => accentTertiary;

        public IReadOnlyList<CategoryDefault> DefaultEntries => defaultEntries;
        public IReadOnlyList<RoomThemeDecoSpec> BookshelfDecoSpecs => bookshelfDecoSpecs;
        public IReadOnlyList<RoomThemeDecoSpec> FloorDecoSpecs => floorDecoSpecs;
        public IReadOnlyList<RoomThemeDecoSpec> WallDecoSpecs => wallDecoSpecs;

        // legacy alias — DirectionalLight* 이름으로 외부조명을 가리키던 기존 RoomThemeApplier 호환.
        // 새 코드는 ExteriorLight*를 사용할 것.
        public Color DirectionalLightColor => exteriorLightColor;
        public float DirectionalLightKelvin => exteriorLightKelvin;
    }
}
