using App.Config;
using UnityEngine;

namespace App.Bootstrap
{
    public sealed class AppRoot : MonoBehaviour
    {
        [SerializeField] private AppApiConfig apiConfig;

        public AppApiConfig ApiConfig => apiConfig;

        private void Awake()
        {
            // Phase 1+: load config, initialize session, register services, then enter MainScene.
        }
    }
}

