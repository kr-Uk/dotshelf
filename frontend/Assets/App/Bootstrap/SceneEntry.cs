using UnityEngine;
using UnityEngine.SceneManagement;

namespace App.Bootstrap
{
    public sealed class SceneEntry : MonoBehaviour
    {
        [SerializeField] private string mainSceneName = "MainScene";

        public void LoadMainScene()
        {
            if (SceneManager.GetActiveScene().name == mainSceneName)
            {
                return;
            }

            SceneManager.LoadSceneAsync(mainSceneName);
        }
    }
}

