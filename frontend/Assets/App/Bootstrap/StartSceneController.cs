using UnityEngine;

namespace App.Bootstrap
{
    public sealed class StartSceneController : MonoBehaviour
    {
        private void Start()
        {
            SceneFlowManager.GoToLogin();
        }
    }
}
