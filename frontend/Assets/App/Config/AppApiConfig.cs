using UnityEngine;

namespace App.Config
{
    [CreateAssetMenu(fileName = "AppApiConfig", menuName = "App/Config/API Config")]
    public sealed class AppApiConfig : ScriptableObject
    {
        [Header("Environment")]
        [SerializeField] private bool useMockApi = true;
        [SerializeField] private string mockBaseUrl = "http://localhost:3001";
        [SerializeField] private string devBaseUrl = "http://localhost:8080";
        [SerializeField] private string prodBaseUrl = "https://api.example.com";

        [Header("Request")]
        [SerializeField] private int requestTimeoutSeconds = 10;
        [SerializeField] private bool enableVerboseLog = true;

        public bool UseMockApi => useMockApi;
        public string MockBaseUrl => mockBaseUrl;
        public string DevBaseUrl => devBaseUrl;
        public string ProdBaseUrl => prodBaseUrl;
        public int RequestTimeoutSeconds => requestTimeoutSeconds;
        public bool EnableVerboseLog => enableVerboseLog;

        public string ResolveBaseUrl()
        {
            if (useMockApi)
            {
                return mockBaseUrl;
            }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            return devBaseUrl;
#else
            return prodBaseUrl;
#endif
        }
    }
}

