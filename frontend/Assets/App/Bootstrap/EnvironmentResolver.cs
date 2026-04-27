using UnityEngine;

namespace App.Bootstrap
{
    public sealed class EnvironmentResolver : MonoBehaviour
    {
        public string CurrentEnvironment
        {
            get
            {
#if UNITY_EDITOR
                return "Editor";
#elif DEVELOPMENT_BUILD
                return "Development";
#else
                return "Release";
#endif
            }
        }
    }
}

