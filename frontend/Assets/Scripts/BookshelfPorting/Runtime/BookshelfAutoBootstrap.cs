using UnityEngine;

namespace BookshelfPorting.Runtime
{
    public static class BookshelfAutoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
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
