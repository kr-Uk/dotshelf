using UnityEngine;
using UnityEngine.SceneManagement;

namespace App.Bootstrap
{
    public static class SceneFlowManager
    {
        public static void GoToLogin()
        {
            LoadIfNotActive(SceneKeys.Login);
        }

        public static void GoToMain()
        {
            var sceneEntry = Object.FindFirstObjectByType<SceneEntry>(FindObjectsInactive.Include);
            if (sceneEntry != null)
            {
                sceneEntry.LoadMainScene();
                return;
            }

            LoadIfNotActive(SceneKeys.Bootstrap);
        }

        public static void GoToStart()
        {
            LoadIfNotActive(SceneKeys.Start);
        }

        private static void LoadIfNotActive(string sceneName)
        {
            if (SceneManager.GetActiveScene().name == sceneName)
            {
                return;
            }

            SceneManager.LoadSceneAsync(sceneName);
        }
    }
}
