using App.Bootstrap;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BookshelfPorting.Runtime
{
    public static class BookshelfAutoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (SceneManager.GetActiveScene().name != SceneKeys.Main)
            {
                return;
            }

            if (Object.FindFirstObjectByType<BookshelfSceneInstaller>() != null)
            {
                return;
            }

            BootstrapScene();
        }

        public static void BootstrapScene()
        {
            BookshelfSceneInstaller.InstallIntoActiveScene(forceGenerate: true);
        }
    }
}
