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
    /// 테마 적용 시 "이 자리에 들어갈 종류" 식별자. 정책: 같은 슬롯에 이미 다른 오브젝트가
    /// 있으면 교체, 슬롯과 무관한 사용자 placement 는 보존.
    ///
    /// CustomizationCategory(어디에 놓는가) / DecorationObjectKind(무엇인가) 와 직교 —
    /// 같은 Kind=Desk 가구라도 테마가 채워야 하는 자리가 다르면 별개 슬롯으로 잡힌다
    /// (예: vintage_table=Table 슬롯 vs vintage_table_mini=TableMini 슬롯).
    ///
    /// None 은 "슬롯 의미 없음" — 보존 정책에서 매칭 대상 아님. 기존 테마 asset 가
    /// 마이그레이션되기 전까지는 None 으로 직렬화되어 backward compatible.
    /// </summary>
    public enum ThemeSlot
    {
        None = 0,
        Shelf,
        Table,
        TableMini,
        Lamp,
        Frame,
        Rug,
        Bed,
        Window,
        Plant,
        // Dashboard (GuestbookBoard) 자체는 항시 존재 — placedItemsRoot 가 아닌 functional
        // dashboard root (GuestbookBoard/DashboardScreenPanel) 자식 "visual skin" 으로 attach.
        // DashboardVisualSkinApplier 가 처리, ThemeSlot.Shelf 와 동일 정책.
        Dashboard,
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

        // 슬롯 식별자 — 테마 교체 정책의 1차 매칭 키. 같은 슬롯에 이미 placement 가
        // 있으면 destroy 후 새 prefab 으로 교체. None 이면 슬롯 기반 매칭 skip
        // (백워드 호환 — 기존 spec 들은 None 으로 직렬화).
        [SerializeField] private ThemeSlot themeSlot = ThemeSlot.None;

        // 자산 식별자 — InventoryKey/저장 키로 사용. 로컬 GLB SourceAssetPath 와 1:1
        // 매핑되어 저장 후 reload 시에도 같은 자산으로 복원 가능. 슬롯 매칭의 2차
        // 키로도 사용 (slot 미설정 spec 도 assetKey 동일 시 교체 매칭).
        //
        // 미래 S3 도입 시 동일 assetKey 로 remote URL 매핑 → 로직 변화 없이 확장 가능
        // (LocalThemeAssetLoader vs RemoteThemeAssetLoader 만 swap).
        [SerializeField] private string assetKey;

        // 온보딩 랜덤 조합 (slot 별 mix) 의 후보 풀에 포함될지 디자이너가 옵트아웃 가능.
        // default true — 기존 asset 들은 Unity 직렬화 시 C# initializer 값으로 채워져 backward
        // compatible (별도 마이그레이션 불필요). false 로 두면 OnboardingRandomThemeBuilder 의
        // 후보 수집에서 제외 — 미공개 / 디버그 / WIP 자산을 랜덤에서 빼고 싶을 때 사용.
        [SerializeField] private bool randomEligible = true;

        // 온보딩 우측 RenderTexture preview 전용 lightweight prefab override. 옵션.
        // null 이면 prefab (실제 MainScene 적용에 사용하는 GLB) 을 그대로 사용.
        //
        // 디자이너 활용:
        //   - 원본 GLB 가 너무 무겁거나 (LOD0 5만 vert+) collider/audio/animation 같은 preview 에
        //     불필요한 컴포넌트가 많으면 LOD2/3 trimmed 버전을 별도 prefab 으로 만들어 여기 wire.
        //   - URP/Lit 외 커스텀 shader (예: stencil mask) 가 들어 있는 prefab 은 preview 에서
        //     렌더 깨질 수 있어 URP/Lit 만 쓰는 simplified 버전을 wire 권장.
        //
        // OnboardingThemePreviewRenderer.ApplyRandomSelection 이 이 필드를 우선 lookup,
        // 없으면 spec.Prefab fallback. PreviewOnly 컴포넌트 (Collider/Rigidbody/AudioSource/
        // ParticleSystem 등) 는 instantiate 직후 일괄 disable 한다.
        [SerializeField] private GameObject previewPrefab;

        public long AssetId => assetId;
        public GameObject Prefab => prefab;
        public WallSide WallSide => wallSide;
        public Vector3 LocalPosition => localPosition;
        public Vector3 LocalRotation => localRotation;
        public Vector3 LocalScale => localScale;
        public string AnchorTag => anchorTag;
        public ThemeSlot ThemeSlot => themeSlot;
        public string AssetKey => assetKey;
        public bool RandomEligible => randomEligible;
        public GameObject PreviewPrefab => previewPrefab;
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

        [Header("Exterior lighting (외부 — 자연광/창문) — DEPRECATED (Tier 1.5+)")]
        [Tooltip("v1.0 외부조명 단독 카테고리 분리 안의 잔존 필드 — Tier 1.5+ 부터 defaultExterior(ExteriorAsset) 가 sun/ambient 까지 흡수. " +
                 "defaultExterior == null 일 때만 RoomThemeApplier 가 fallback 으로 사용. Tier 2 에서 제거 예정. " +
                 "참고 노션 페이지: 'Exterior 통합 시스템 (260510)'.")]
        [SerializeField] private Color exteriorLightColor = Color.white;
        [SerializeField, Range(2700f, 8000f)] private float exteriorLightKelvin = 6500f;
        [SerializeField, Range(0f, 4f)] private float exteriorLightIntensity = 1f;

        [Header("Exterior — 통합 외부 환경 (Tier 1.5+)")]
        [Tooltip("이 테마의 default 외부 환경 자산. " +
                 "skybox + 자연광 + 풍경 prefab + ambient/fog/reflection 모두 흡수한 ExteriorAsset SO. " +
                 "RoomThemeApplier 가 테마 적용 시 ExteriorApplier 에 forwarding. " +
                 "null 이면 위 ExteriorLight* 필드 + ambientTint/Intensity 를 fallback 으로 직접 적용 " +
                 "(Tier 2 에서 fallback 제거).")]
        [SerializeField] private ExteriorAsset defaultExterior;

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

        public ExteriorAsset DefaultExterior => defaultExterior;

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
