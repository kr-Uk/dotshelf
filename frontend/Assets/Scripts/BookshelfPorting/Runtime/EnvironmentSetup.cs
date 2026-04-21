using UnityEngine;

namespace BookshelfPorting.Runtime
{
    public class EnvironmentSetup : MonoBehaviour
    {
        [SerializeField] private ReflectionProbe reflectionProbe = null;
        [SerializeField] private Material skyboxMaterial = null;
        [SerializeField] private bool applyOnStart = true;

        public void Configure(ReflectionProbe probe, Material skybox)
        {
            reflectionProbe = probe;
            skyboxMaterial = skybox;
        }

        private void Start()
        {
            if (applyOnStart)
            {
                Apply();
            }
        }

        [ContextMenu("Apply Lighting")]
        public void Apply()
        {
            if (skyboxMaterial != null)
            {
                RenderSettings.skybox = skyboxMaterial;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.ambientIntensity = 1.1f;

            if (reflectionProbe != null)
            {
                reflectionProbe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
                reflectionProbe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
                reflectionProbe.intensity = 1f;
                reflectionProbe.size = new Vector3(4.2f, 2.8f, 4.2f);
                reflectionProbe.RenderProbe();
            }

            CreateOrUpdateLight("WarmDirectional", new Vector3(40f, -25f, 0f), new Color(1f, 0.88f, 0.74f), LightType.Directional, 1.1f, 6500f);
            CreateOrUpdateLight("AccentDirectional", new Vector3(18f, 145f, 0f), new Color(0.86f, 0.9f, 1f), LightType.Directional, 0.45f, 0f);
            CreateOrUpdatePoint("WarmPointLeft", new Vector3(-1.25f, 1.95f, -0.55f), new Color(1f, 0.76f, 0.58f), 18f, 4.5f);
            CreateOrUpdatePoint("WarmPointRight", new Vector3(1.15f, 1.75f, -0.3f), new Color(1f, 0.81f, 0.66f), 14f, 3.8f);
        }

        private void CreateOrUpdatePoint(string name, Vector3 localPosition, Color color, float intensity, float range)
        {
            var light = CreateOrUpdateLight(name, Vector3.zero, color, LightType.Point, intensity, range);
            light.transform.position = localPosition;
        }

        private Light CreateOrUpdateLight(string name, Vector3 eulerAngles, Color color, LightType type, float intensity, float range)
        {
            var existing = transform.Find(name);
            var lightObject = existing != null ? existing.gameObject : new GameObject(name);
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(eulerAngles);

            var light = lightObject.GetComponent<Light>();
            if (light == null)
            {
                light = lightObject.AddComponent<Light>();
            }

            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.Soft;
            return light;
        }
    }
}
