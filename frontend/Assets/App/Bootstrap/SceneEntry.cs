using System.Collections;
using BookshelfPorting.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace App.Bootstrap
{
    public sealed class SceneEntry : MonoBehaviour
    {
        [SerializeField] private string mainSceneName = SceneKeys.Main;
        [SerializeField] private string notebookUiSceneName = SceneKeys.NotebookUi;
        [SerializeField] private bool loadMainSceneOnStart = true;
        [SerializeField] private bool loadAdditively = true;
        [SerializeField] private bool loadNotebookUiScene = true;
        [SerializeField] private bool setLoadedSceneActive = true;
        [SerializeField] private bool runInstallersAfterLoad = true;
        [SerializeField] private bool forceGenerateOnBootstrapLoad = false;

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

            var mainScene = SceneManager.GetSceneByName(mainSceneName);
            if (!mainScene.IsValid() || !mainScene.isLoaded)
            {
                var mode = loadAdditively ? LoadSceneMode.Additive : LoadSceneMode.Single;
                if (!StartSceneLoad(mainSceneName, mode, out var operation))
                {
                    isLoading = false;
                    yield break;
                }

                while (!operation.isDone)
                {
                    yield return null;
                }

                mainScene = SceneManager.GetSceneByName(mainSceneName);
            }

            if (mainScene.IsValid() && setLoadedSceneActive && mainScene.isLoaded)
            {
                if (!SceneManager.SetActiveScene(mainScene))
                {
                    Debug.LogWarning($"Failed to set active scene: {mainSceneName}");
                }
            }

            if (loadAdditively && loadNotebookUiScene)
            {
                var notebookScene = SceneManager.GetSceneByName(notebookUiSceneName);
                if (!notebookScene.IsValid() || !notebookScene.isLoaded)
                {
                    if (!StartSceneLoad(notebookUiSceneName, LoadSceneMode.Additive, out var operation))
                    {
                        isLoading = false;
                        yield break;
                    }

                    while (!operation.isDone)
                    {
                        yield return null;
                    }
                }
            }

            if (mainScene.IsValid() && runInstallersAfterLoad)
            {
                RunSceneInstallers(mainScene, forceGenerateOnBootstrapLoad);
            }

            Debug.Log($"Dotshelf bootstrap loaded {mainSceneName} (additive={loadAdditively}, notebookUi={loadNotebookUiScene}).");
            isLoading = false;
        }

        private static bool StartSceneLoad(string sceneName, LoadSceneMode mode, out AsyncOperation operation)
        {
            operation = SceneManager.LoadSceneAsync(sceneName, mode);
            if (operation != null)
            {
                return true;
            }

            Debug.LogError($"Failed to load scene: {sceneName}");
            return false;
        }

        private static void RunSceneInstallers(Scene scene, bool forceGenerate)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var installer in root.GetComponentsInChildren<BookshelfSceneInstaller>(true))
                {
                    installer.InstallScene(forceGenerate);
                }
            }
        }
    }
}

