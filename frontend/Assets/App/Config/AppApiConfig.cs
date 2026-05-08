using System;
using App.Domain;
using UnityEngine;
using UnityEngine.Serialization;

namespace App.Config
{
    public enum AppApiMode
    {
        RealApi = 0
    }

    [CreateAssetMenu(fileName = "AppApiConfig", menuName = "App/Config/API Config")]
    public sealed class AppApiConfig : ScriptableObject
    {
        [Header("Environment")]
        [FormerlySerializedAs("useMockApi")]
        [Tooltip("HTTP API mode. For mock servers, keep the same API paths and change only the base URL.")]
        [SerializeField] private AppApiMode apiMode = AppApiMode.RealApi;
        [SerializeField] private string devBaseUrl = ApiEndpointDefaults.DevBaseUrl;
        [SerializeField] private string prodBaseUrl = ApiEndpointDefaults.ProdBaseUrl;
        [SerializeField] private string healthCheckPath = ApiEndpointDefaults.HealthCheckPath;
        [Tooltip("Optional editor/dev access token. Prefer DOTSHELF_ACCESS_TOKEN for local runs.")]
        [SerializeField] private string accessToken = string.Empty;

        [Header("Request")]
        [SerializeField] private int requestTimeoutSeconds = 10;
        [SerializeField] private bool enableVerboseLog = true;

        public AppApiMode ApiMode => apiMode;
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
    }
}

