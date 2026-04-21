using BookshelfPorting.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace App.Editor
{
    [InitializeOnLoad]
    internal static class MainSceneContentAutoBuilder
    {
        private const string MainScenePath = "Assets/Scenes/MainScene.unity";

        static MainSceneContentAutoBuilder()
        {
            EditorApplication.delayCall += TryBuildOpenMainScene;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += TryBuildOpenMainScene;
            }
        }

        private static void TryBuildOpenMainScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.path != MainScenePath || HasSavedBookshelfContent())
            {
                return;
            }

            var installer = Object.FindFirstObjectByType<BookshelfSceneInstaller>();
            if (installer == null)
            {
                installer = new GameObject("BookshelfSceneInstaller").AddComponent<BookshelfSceneInstaller>();
            }

            installer.InstallScene(forceGenerate: true);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("MainScene bookshelf content generated and saved.");
        }

        private static bool HasSavedBookshelfContent()
        {
            var systems = GameObject.Find("BookshelfSystems");
            if (systems == null)
            {
                return false;
            }

            return systems.transform.Find("BookshelfRoot") != null &&
                   systems.transform.Find("RoomRoot") != null;
        }
    }
}
