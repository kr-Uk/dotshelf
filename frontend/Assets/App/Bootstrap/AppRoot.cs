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
            // BootstrapScene owns global config/session services while MainRoomScene is loaded additively.
        }
    }
}

