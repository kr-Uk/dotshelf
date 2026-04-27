using App.Config;
using UnityEngine;

namespace App.Bootstrap
{
    public sealed class ConfigLoader : MonoBehaviour
    {
        [SerializeField] private AppApiConfig apiConfig;

        public AppApiConfig ApiConfig => apiConfig;
    }
}

