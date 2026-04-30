using System.Collections;
using BookshelfPorting.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace App.Bootstrap
{
    public sealed class SceneEntry : MonoBehaviour
    {
        [SerializeField] private string mainSceneName = SceneKeys.Main;
        [SerializeField] private bool loadMainSceneOnStart = true;
        [SerializeField] private bool loadAdditively = true;
        [SerializeField] private bool setLoadedSceneActive = true;
        [SerializeField] private bool runInstallersAfterLoad = true;

        private bool isLoading;

        private void Start()
        {
            if (loadMainSceneOnStart)
            {
                LoadMainScene();
            }
        }

        public void LoadMainScene()
        {
            if (isLoading)
            {
                return;
            }

            StartCoroutine(LoadMainSceneRoutine());
        }

        private IEnumerator LoadMainSceneRoutine()
        {
            isLoading = true;

            var scene = SceneManager.GetSceneByName(mainSceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                var mode = loadAdditively ? LoadSceneMode.Additive : LoadSceneMode.Single;
                var operation = SceneManager.LoadSceneAsync(mainSceneName, mode);
                if (operation == null)
                {
                    Debug.LogError($"Failed to load scene: {mainSceneName}");
                    isLoading = false;
                    yield break;
                }

                while (!operation.isDone)
                {
                    yield return null;
                }

                scene = SceneManager.GetSceneByName(mainSceneName);
            }

            if (scene.IsValid() && setLoadedSceneActive && scene.isLoaded)
            {
                if (!SceneManager.SetActiveScene(scene))
                {
                    Debug.LogWarning($"Failed to set active scene: {mainSceneName}");
                }
            }

            if (scene.IsValid() && runInstallersAfterLoad)
            {
                RunSceneInstallers(scene);
            }

            Debug.Log($"Dotshelf bootstrap loaded {mainSceneName} (additive={loadAdditively}).");
            isLoading = false;
        }

        private static void RunSceneInstallers(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var installer in root.GetComponentsInChildren<BookshelfSceneInstaller>(true))
                {
                    installer.InstallScene(forceGenerate: true);
                }
            }
        }
    }
}

