using System;
using App.Domain;
using UnityEngine;
using UnityEngine.Serialization;

namespace App.Config
{
    public enum AppApiMode
    {
        RealApi = 0,
        MockApi = 1
    }

    [CreateAssetMenu(fileName = "AppApiConfig", menuName = "App/Config/API Config")]
    public sealed class AppApiConfig : ScriptableObject
    {
        [Header("Environment")]
        [FormerlySerializedAs("useMockApi")]
        [Tooltip("API mode. MockApi is honored only in Editor/Development builds; production builds force RealApi.")]
        [SerializeField] private AppApiMode apiMode = AppApiMode.RealApi;
        [SerializeField] private string devBaseUrl = ApiEndpointDefaults.DevBaseUrl;
        [SerializeField] private string prodBaseUrl = ApiEndpointDefaults.ProdBaseUrl;
        [SerializeField] private string healthCheckPath = ApiEndpointDefaults.HealthCheckPath;
        [Tooltip("Optional editor/dev access token. Prefer DOTSHELF_ACCESS_TOKEN for local runs.")]
        [SerializeField] private string accessToken = string.Empty;

        [Header("Request")]
        [SerializeField] private int requestTimeoutSeconds = 10;
        [Tooltip("Runtime APIManager success/health logs only. Domain trace logs require DOTSHELF_LOG_* or DOTSHELF_LOG_ALL scripting defines.")]
        [SerializeField] private bool enableVerboseLog;

        public AppApiMode ConfiguredApiMode => NormalizeApiMode(apiMode);
        public AppApiMode ApiMode => ResolveRuntimeApiMode(ConfiguredApiMode);
        public bool UsesMockApi => ApiMode == AppApiMode.MockApi;
        public bool UsesHttpApi => true;
        public string DevBaseUrl => devBaseUrl;
        public string ProdBaseUrl => prodBaseUrl;
        public string HealthCheckPath => string.IsNullOrWhiteSpace(healthCheckPath) ? "/health" : healthCheckPath;
        public int RequestTimeoutSeconds => requestTimeoutSeconds;
        public bool EnableVerboseLog => enableVerboseLog;

        public string ResolveBaseUrl()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            return devBaseUrl;
#else
            return prodBaseUrl;
#endif
        }

        public string ResolveAccessToken()
        {
            var environmentToken = Environment.GetEnvironmentVariable("DOTSHELF_ACCESS_TOKEN");
            return string.IsNullOrWhiteSpace(environmentToken) ? accessToken : environmentToken;
        }

        private void OnEnable()
        {
            apiMode = NormalizeApiMode(apiMode);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            apiMode = NormalizeApiMode(apiMode);
        }
#endif

        private static AppApiMode NormalizeApiMode(AppApiMode value)
        {
            return Enum.IsDefined(typeof(AppApiMode), value) ? value : AppApiMode.RealApi;
        }

        private static AppApiMode ResolveRuntimeApiMode(AppApiMode value)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return value;
#else
            return value == AppApiMode.MockApi ? AppApiMode.RealApi : value;
#endif
        }
    }
}

