using UnityEngine;
using UnityEngine.Rendering;
using App.Infrastructure.Catalogs;

namespace App.Bootstrap.Exterior
{
    /// <summary>
    /// 외부 환경(창밖 풍경 + 자연광 + 분위기) 1세트를 씬에 적용하는 MonoBehaviour.
    ///
    /// 책임:
    /// 1) Far layer — RenderSettings.skybox / ambient / fog / reflection
    /// 2) Sun override — 씬의 DirectionalLight 색/회전/intensity/shadow 갱신
    /// 3) Scene prefab spawn — exteriorRoot 의 자식으로 instantiate, 이전 spawned 정리
    ///
    /// 위치: App/Bootstrap/Exterior/ — Assembly-CSharp 에 들어가 Infrastructure SO
    /// (ExteriorAsset) 자유 reference. RoomThemeApplier 와 동일 패턴이지만 dotshelf
    /// 의 Bootstrap MonoBehaviour 컨벤션 (RoomEditModeController 옆) 따름.
    ///
    /// CLAUDE.md "새 MonoBehaviour 는 Features/ 또는 UI/에만" 규칙과 약간 다름 —
    /// Features asmdef 가 Infrastructure 를 reference 안 하므로 SO 직접 적용 코드는
    /// Bootstrap 에 둘 수밖에 없음. Tier 2 에서 layer 정리 시 재배치 검토.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExteriorApplier : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField, Tooltip("외부 prefab 이 instantiate 될 부모 transform. " +
                                 "씬 컨벤션: 방 정중앙 (또는 World Origin), rotation = identity.")]
        private Transform exteriorRoot;

        [SerializeField, Tooltip("씬의 sun directional light. ExteriorAsset.overrideSun=true 일 때 " +
                                 "색/회전/intensity/shadow 를 이 light 에 적용.")]
        private Light sunLight;

        [Header("Optional — Awake 자동 적용 (dev / Bootstrap wiring 미사용 시)")]
        [SerializeField, Tooltip("null 이 아니면 Awake 에서 자동 Apply. Bootstrap 에서 명시 wiring " +
                                 "하면 비워둘 것 (중복 적용 방지).")]
        private ExteriorAsset autoApplyOnAwake;

        // 다음 Apply 에서 destroy 할 spawned scene root.
        private GameObject _spawnedScene;

        // 외부에서 현재 적용된 SO 조회용. wiring/디버깅 용도.
        public ExteriorAsset CurrentExterior { get; private set; }

        private void Awake()
        {
            if (autoApplyOnAwake != null)
            {
                Apply(autoApplyOnAwake);
            }
        }

        private void OnDestroy()
        {
            ClearSpawnedScene();
        }

        public void Apply(ExteriorAsset asset)
        {
            if (asset == null) return;

            ApplyFarLayer(asset);
            ApplySunOverride(asset);
            SpawnScenePrefab(asset);

            CurrentExterior = asset;
        }

        private static void ApplyFarLayer(ExteriorAsset asset)
        {
            // Skybox — null 이면 변경 안 함 (이전 값 유지).
            if (asset.SkyboxMaterial != null)
            {
                RenderSettings.skybox = asset.SkyboxMaterial;
            }

            // Ambient — mode 와 4가지 색 모두 set (모드별로 Unity 가 알아서 사용).
            RenderSettings.ambientMode = asset.AmbientMode;
            RenderSettings.ambientLight = asset.AmbientFlatColor;
            RenderSettings.ambientSkyColor = asset.AmbientSkyColor;
            RenderSettings.ambientEquatorColor = asset.AmbientEquatorColor;
            RenderSettings.ambientGroundColor = asset.AmbientGroundColor;
            RenderSettings.ambientIntensity = asset.AmbientIntensity;

            // Reflection — custom cubemap 있으면 Custom 모드, 없으면 Skybox 자동.
            if (asset.CustomReflectionCubemap != null)
            {
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = asset.CustomReflectionCubemap;
            }
            else
            {
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
                RenderSettings.customReflectionTexture = null;
            }
            RenderSettings.reflectionIntensity = asset.ReflectionIntensity;

            // Fog
            RenderSettings.fog = asset.UseFog;
            if (asset.UseFog)
            {
                RenderSettings.fogMode = asset.FogMode;
                RenderSettings.fogColor = asset.FogColor;
                RenderSettings.fogDensity = asset.FogDensity;
                RenderSettings.fogStartDistance = asset.FogLinearStart;
                RenderSettings.fogEndDistance = asset.FogLinearEnd;
            }

            // skybox/ambient/reflection 변경 → ambient probe + reflection probe 재계산.
            DynamicGI.UpdateEnvironment();
        }

        private void ApplySunOverride(ExteriorAsset asset)
        {
            if (!asset.OverrideSun || sunLight == null) return;

            sunLight.useColorTemperature = asset.UseColorTemperature;
            sunLight.color = asset.SunColor;
            if (asset.UseColorTemperature)
            {
                sunLight.colorTemperature = asset.SunKelvin;
            }
            sunLight.intensity = asset.SunIntensity;
            sunLight.transform.rotation = Quaternion.Euler(asset.SunRotationEuler);
            sunLight.shadows = asset.SunShadows;
            sunLight.shadowStrength = asset.SunShadowStrength;
        }

        private void SpawnScenePrefab(ExteriorAsset asset)
        {
            ClearSpawnedScene();

            if (asset.ScenePrefab == null || exteriorRoot == null) return;

            _spawnedScene = Instantiate(asset.ScenePrefab, exteriorRoot);
            // (Clone) suffix 제거 — 디버깅 시 hierarchy 깔끔.
            _spawnedScene.name = asset.ScenePrefab.name;
        }

        private void ClearSpawnedScene()
        {
            if (_spawnedScene == null) return;

            if (UnityEngine.Application.isPlaying)
            {
                Destroy(_spawnedScene);
            }
            else
            {
                DestroyImmediate(_spawnedScene);
            }
            _spawnedScene = null;
        }
    }
}
