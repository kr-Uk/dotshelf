using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace App.Infrastructure.Catalogs
{
    /// <summary>
    /// 외부 환경(창밖 풍경 + 자연광 + 분위기) 1세트 customization asset.
    /// 사용자가 갤러리에서 구매/소유한 외부 옵션 1개 = ExteriorAsset SO 1개.
    ///
    /// 설계 원칙:
    /// — 얇은 SO + 풍부한 prefab. 동적 콘텐츠는 모두 scenePrefab 안에.
    /// — Sun(DirectionalLight) 1개만 SO 명시 override (씬 sun 충돌 방지).
    /// — Point/Spot Light, mesh, material, animator, particle, MonoBehaviour 동작은
    ///   prefab 내부 자유. Addressables dependency graph 가 자동으로 묶음.
    /// — RenderSettings 로 직접 set 해야 하는 필드(skybox/ambient/fog/reflection)만
    ///   SO 슬롯으로 노출.
    ///
    /// 좌표계 컨벤션 (scenePrefab 디자이너 contract):
    /// — ExteriorRoot transform = 방 정중앙(World Origin 권장), rotation = identity.
    /// — prefab 은 360° 둘러싸도록 디자인 — 어느 벽 창문에서도 보여야 함.
    /// — 거리(XZ 반경): 근 5~15m / 중 20~80m / 원거리는 skybox 위임 (mesh 두지 말 것).
    /// — 높이: ground plane Y = 방 바닥 또는 살짝 아래.
    /// — DirectionalLight 는 prefab 에 넣지 말 것 (Sun override 사용).
    /// 자세히는 frontend/docs/exterior-conventions.md 참조.
    /// </summary>
    [CreateAssetMenu(menuName = "Dotshelf/Exterior Asset", fileName = "Exterior_")]
    public sealed class ExteriorAsset : ScriptableObject
    {
        // ───────────────────────────────────────────────────────────────
        // Identity — BE catalog (assets table) 매핑
        // ───────────────────────────────────────────────────────────────
        [Header("Identity")]
        [Tooltip("BE assets.id. 0 = local-only (default 등 base APK 내장 자산).")]
        [SerializeField] private long assetId;

        [Tooltip("BE assets.code. 영문 대문자+언더스코어. 예: EXT_DEFAULT, EXT_MEADOW_V1.")]
        [SerializeField] private string code;

        [Tooltip("UI 표시명 (한글). 갤러리 카드 / 적용 toast.")]
        [SerializeField] private string displayName;

        [TextArea(2, 4)]
        [Tooltip("UI 상세 설명. 갤러리 detail panel 마케팅 문구.")]
        [SerializeField] private string description;

        [Tooltip("갤러리 썸네일. 256×256 권장, ASTC 압축.")]
        [SerializeField] private Sprite previewSprite;

        [Tooltip("RoomTheme filter 태그. 빈 문자열 = 모든 테마에서 노출. " +
                 "비빈 태그는 같은 themeTag RoomTheme 의 default 후보로만 노출.")]
        [SerializeField] private string themeTag;

        [Tooltip("BE inventory POST 를 skip 하는 local-only entry 플래그. " +
                 "사용자 inventory 에 없는 placeholder asset 적용 시 403 회피용. " +
                 "default 자산은 항상 true.")]
        [SerializeField] private bool isLocalPreview;

        [Tooltip("사용자가 외부만 따로 override 가능한지. " +
                 "false = RoomTheme 강제, true = 사용자 갤러리에서 swap 허용.")]
        [SerializeField] private bool allowsCustomization = true;

        // ───────────────────────────────────────────────────────────────
        // Far layer — Skybox / Ambient / Fog / Reflection
        // RenderSettings 로 직접 set 해야 하므로 SO 슬롯이 자연스러움.
        // ───────────────────────────────────────────────────────────────
        [Header("Far — Skybox")]
        [Tooltip("RenderSettings.skybox 에 set. Procedural / Cubemap / 6-Sided 다 가능. " +
                 "null = skybox 변경 안 함 (이전 값 유지).")]
        [SerializeField] private Material skyboxMaterial;

        [Header("Far — Ambient (간접광)")]
        [Tooltip("RenderSettings.ambientMode. Skybox 권장 (skyboxMaterial 자동 ambient). " +
                 "Trilight/Flat 은 색 명시.")]
        [SerializeField] private AmbientMode ambientMode = AmbientMode.Skybox;

        [Tooltip("ambientMode = Flat 일 때 RenderSettings.ambientLight. Skybox 모드 시 무시.")]
        [SerializeField] private Color ambientFlatColor = new(0.5f, 0.5f, 0.55f, 1f);

        [Tooltip("ambientMode = Trilight 일 때 sky color.")]
        [SerializeField] private Color ambientSkyColor = new(0.7f, 0.8f, 1f, 1f);

        [Tooltip("ambientMode = Trilight 일 때 equator color.")]
        [SerializeField] private Color ambientEquatorColor = new(0.5f, 0.5f, 0.5f, 1f);

        [Tooltip("ambientMode = Trilight 일 때 ground color.")]
        [SerializeField] private Color ambientGroundColor = new(0.3f, 0.3f, 0.25f, 1f);

        [Tooltip("RenderSettings.ambientIntensity. 1.0 = 기본, 0 = 완전 암흑.")]
        [SerializeField, Range(0f, 2f)] private float ambientIntensity = 1f;

        [Header("Far — Reflection (환경 반사)")]
        [Tooltip("Custom reflection cubemap. null = skyboxMaterial 자동 사용.")]
        [SerializeField] private Cubemap customReflectionCubemap;

        [Tooltip("RenderSettings.reflectionIntensity. 0 = 반사 없음, 1 = 풀.")]
        [SerializeField, Range(0f, 1f)] private float reflectionIntensity = 1f;

        [Header("Far — Fog (선택)")]
        [Tooltip("RenderSettings.fog 활성화. 안개 낀 새벽 / 흐린 날 표현.")]
        [SerializeField] private bool useFog;

        [Tooltip("RenderSettings.fogMode. ExponentialSquared 가 모바일 권장 (single param).")]
        [SerializeField] private FogMode fogMode = FogMode.ExponentialSquared;

        [SerializeField] private Color fogColor = new(0.5f, 0.5f, 0.55f, 1f);

        [Tooltip("Exponential / ExponentialSquared 의 density. 0.001~0.02 권장.")]
        [SerializeField, Range(0f, 0.1f)] private float fogDensity = 0.01f;

        [Tooltip("Linear 모드 시작 거리. fogMode = Linear 일 때만.")]
        [SerializeField, Min(0f)] private float fogLinearStart = 10f;

        [Tooltip("Linear 모드 끝 거리. fogMode = Linear 일 때만.")]
        [SerializeField, Min(0f)] private float fogLinearEnd = 100f;

        // ───────────────────────────────────────────────────────────────
        // Scene layer — mid + near 통합 prefab (디자이너 자유 영역)
        // ───────────────────────────────────────────────────────────────
        [Header("Scene — 옵션 디자이너 자유 영역 (mid + near 통합)")]
        [Tooltip("ExteriorRoot 자식으로 instantiate 될 prefab.\n" +
                 "좌표계: ExteriorRoot 기준 (= 방 정중앙).\n" +
                 "디자인: 360° 둘러쌈 — 어느 벽 창문에서도 보여야 함.\n" +
                 "거리: 근 5~15m / 중 20~80m / 원거리는 skybox.\n" +
                 "내부 자유: mesh, material, animator, AnimationClip, " +
                 "Point/Spot Light, ParticleSystem, MonoBehaviour, Shader.\n" +
                 "금지: DirectionalLight (Sun override 슬롯 사용), Realtime Reflection Probe " +
                 "(customReflectionCubemap 사용), 다른 RoomTheme 자산 reference.")]
        [SerializeField] private GameObject scenePrefab;

        // ───────────────────────────────────────────────────────────────
        // Sun override — 씬의 DirectionalLight 1개를 어떻게 바꿀지
        // 씬에 sun light 가 이미 있고 (RoomThemeApplier 관리), prefab 에
        // 또 DirectionalLight 넣으면 충돌 → SO 에서 명시 override.
        // ───────────────────────────────────────────────────────────────
        [Header("Sun — 씬 DirectionalLight override")]
        [Tooltip("false = sun 값 변경 안 함 (이전 RoomTheme/sun 상태 유지). " +
                 "true = 아래 sun* 값을 씬 DirectionalLight 에 적용.")]
        [SerializeField] private bool overrideSun = true;

        [Tooltip("Light.useColorTemperature. true = Kelvin 적용 + sunColor 는 filter, " +
                 "false = sunColor 만 사용 (Kelvin 무시).")]
        [SerializeField] private bool useColorTemperature = true;

        [Tooltip("Light.color (filter). useColorTemperature = true 면 Kelvin 색에 곱해짐.")]
        [SerializeField] private Color sunColor = Color.white;

        [Tooltip("색온도 (Kelvin). 2700=백열, 5500=한낮, 6500=흐린 낮, 8000=새벽 푸름.")]
        [SerializeField, Range(1500f, 20000f)] private float sunKelvin = 5500f;

        [Tooltip("Light.intensity. 한낮=1.0, 노을=0.6, 흐린 날=0.4, 야간=0.05.")]
        [SerializeField, Range(0f, 4f)] private float sunIntensity = 1f;

        [Tooltip("Light.transform.eulerAngles. X=고도(50°≈한낮, 5°≈일출/일몰), Y=방위(0~360°).")]
        [SerializeField] private Vector3 sunRotationEuler = new(50f, -30f, 0f);

        [Tooltip("Light.shadows. None=그림자 없음, Hard=가장자리 sharp, Soft=PCF blur.")]
        [SerializeField] private LightShadows sunShadows = LightShadows.Soft;

        [Tooltip("Light.shadowStrength. 0=그림자 없음, 1=완전 검정.")]
        [SerializeField, Range(0f, 1f)] private float sunShadowStrength = 0.7f;

        // ───────────────────────────────────────────────────────────────
        // Time variants — 시간대 변형 (선택, Tier 2 이후 활용)
        // 비워두면 단일 시간대 옵션. 채우면 Applier 가 variantId / 시간 기준
        // element 1개 골라 root 값을 부분 override.
        // ───────────────────────────────────────────────────────────────
        [Header("Time variants (선택)")]
        [Tooltip("morning/noon/sunset/night 같은 시간대 변형. " +
                 "비우면 root SO 값만 사용. 각 variant 의 override* 플래그가 " +
                 "true 인 필드만 적용 (false 면 root 유지).")]
        [SerializeField] private ExteriorTimeVariant[] timeVariants;

        // ───────────────────────────────────────────────────────────────
        // Budget guardrails — Editor validator 가 import 시 체크 (Tier 1.5+)
        // ───────────────────────────────────────────────────────────────
        [Header("Budget guardrails (Editor validator 검증)")]
        [Tooltip("scenePrefab 안 realtime additional Light(Point/Spot) 최대 수. " +
                 "URP 모바일 권장 한도 4. 초과 시 Editor 경고.")]
        [SerializeField, Range(0, 8)] private int maxAdditionalRealtimeLights = 4;

        [Tooltip("scenePrefab 안 동시 active ParticleSystem 최대 수. " +
                 "fillrate overdraw 위험 — 모바일 권장 한도 2. 초과 시 경고.")]
        [SerializeField, Range(0, 6)] private int maxActiveParticleSystems = 2;

        [Tooltip("이 옵션 1세트 Addressables bundle 목표 크기 (MB). " +
                 "초과 시 Editor 경고. 모바일 안전 한도 ~15MB.")]
        [SerializeField, Range(5, 30)] private int targetBundleSizeMb = 15;

        // ───────────────────────────────────────────────────────────────
        // Public getters
        // ───────────────────────────────────────────────────────────────
        public long AssetId => assetId;
        public string Code => code;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite PreviewSprite => previewSprite;
        public string ThemeTag => themeTag;
        public bool IsLocalPreview => isLocalPreview;
        public bool AllowsCustomization => allowsCustomization;

        public Material SkyboxMaterial => skyboxMaterial;
        public AmbientMode AmbientMode => ambientMode;
        public Color AmbientFlatColor => ambientFlatColor;
        public Color AmbientSkyColor => ambientSkyColor;
        public Color AmbientEquatorColor => ambientEquatorColor;
        public Color AmbientGroundColor => ambientGroundColor;
        public float AmbientIntensity => ambientIntensity;
        public Cubemap CustomReflectionCubemap => customReflectionCubemap;
        public float ReflectionIntensity => reflectionIntensity;

        public bool UseFog => useFog;
        public FogMode FogMode => fogMode;
        public Color FogColor => fogColor;
        public float FogDensity => fogDensity;
        public float FogLinearStart => fogLinearStart;
        public float FogLinearEnd => fogLinearEnd;

        public GameObject ScenePrefab => scenePrefab;

        public bool OverrideSun => overrideSun;
        public bool UseColorTemperature => useColorTemperature;
        public Color SunColor => sunColor;
        public float SunKelvin => sunKelvin;
        public float SunIntensity => sunIntensity;
        public Vector3 SunRotationEuler => sunRotationEuler;
        public LightShadows SunShadows => sunShadows;
        public float SunShadowStrength => sunShadowStrength;

        public IReadOnlyList<ExteriorTimeVariant> TimeVariants => timeVariants;

        public int MaxAdditionalRealtimeLights => maxAdditionalRealtimeLights;
        public int MaxActiveParticleSystems => maxActiveParticleSystems;
        public int TargetBundleSizeMb => targetBundleSizeMb;
    }

    /// <summary>
    /// 외부 옵션 안의 시간대 변형 1개. 활성화 시 root ExteriorAsset 의
    /// sun/skybox/ambient/fog 값을 이 variant 의 값으로 부분 override.
    /// 각 override* 플래그가 true 인 필드만 적용 (false 면 root 유지).
    /// </summary>
    [Serializable]
    public sealed class ExteriorTimeVariant
    {
        [Header("Identity")]
        [Tooltip("variant 식별자 — Morning/Noon/Sunset/Night 등. UI 토글 / API 키.")]
        [SerializeField] private string variantId;

        [Tooltip("UI 표시명 (한글). 예: '아침', '한낮', '노을', '밤'.")]
        [SerializeField] private string displayName;

        [Tooltip("자동 시간 매칭 시 시작 시각 (24h, hour). 예: Morning=6, Noon=11, Sunset=17. " +
                 "-1 = 자동 매칭 안 함 (사용자 수동 선택만).")]
        [SerializeField, Range(-1, 23)] private int startHour = -1;

        [Header("Sun override")]
        [SerializeField] private bool overrideSun;
        [SerializeField] private Color sunColor = Color.white;
        [SerializeField, Range(1500f, 20000f)] private float sunKelvin = 5500f;
        [SerializeField, Range(0f, 4f)] private float sunIntensity = 1f;
        [SerializeField] private Vector3 sunRotationEuler = new(50f, -30f, 0f);

        [Header("Skybox override")]
        [SerializeField] private bool overrideSkybox;
        [SerializeField] private Material skyboxMaterial;

        [Header("Ambient override (Flat 모드 한정)")]
        [SerializeField] private bool overrideAmbient;
        [SerializeField] private Color ambientFlatColor = new(0.5f, 0.5f, 0.55f, 1f);
        [SerializeField, Range(0f, 2f)] private float ambientIntensity = 1f;

        [Header("Fog override")]
        [SerializeField] private bool overrideFog;
        [SerializeField] private Color fogColor = new(0.5f, 0.5f, 0.55f, 1f);
        [SerializeField, Range(0f, 0.1f)] private float fogDensity = 0.01f;

        public string VariantId => variantId;
        public string DisplayName => displayName;
        public int StartHour => startHour;

        public bool OverrideSun => overrideSun;
        public Color SunColor => sunColor;
        public float SunKelvin => sunKelvin;
        public float SunIntensity => sunIntensity;
        public Vector3 SunRotationEuler => sunRotationEuler;

        public bool OverrideSkybox => overrideSkybox;
        public Material SkyboxMaterial => skyboxMaterial;

        public bool OverrideAmbient => overrideAmbient;
        public Color AmbientFlatColor => ambientFlatColor;
        public float AmbientIntensity => ambientIntensity;

        public bool OverrideFog => overrideFog;
        public Color FogColor => fogColor;
        public float FogDensity => fogDensity;
    }
}
