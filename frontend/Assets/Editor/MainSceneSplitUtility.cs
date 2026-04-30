using System;
using System.IO;
using App.Bootstrap;
using App.Config;
using BookshelfPorting.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dotshelf.EditorTools
{
    public static class MainSceneSplitUtility
    {
        private const string ApiConfigPath = "Assets/App/Config/AppApiConfig.asset";
        private const string BootstrapScenePath = "Assets/Scenes/BootstrapScene.unity";
        private const string LoginScenePath = "Assets/Scenes/LoginScene.unity";
        private const string MainRoomScenePath = "Assets/Scenes/MainRoomScene.unity";
        private const string NotebookUiScenePath = "Assets/Scenes/NotebookUIScene.unity";
        private const string StartScenePath = "Assets/Scenes/StartScene.unity";
        private const string LibraryRoomBackgroundPrefabPath = "Assets/App/Composition/Prefabs/LibraryRoomBackground.prefab";
        private const string NotebookStationPrefabPath = "Assets/App/Composition/Prefabs/NotebookStation.prefab";

        [MenuItem("Dotshelf/Refactor/Rebuild Bootstrap Scene")]
        public static void RebuildBootstrapScene()
        {
            if (!File.Exists(MainRoomScenePath))
            {
                throw new FileNotFoundException("Main room scene is missing.", MainRoomScenePath);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var appRootObject = new GameObject("AppRoot");
            var appRoot = appRootObject.AddComponent<AppRoot>();
            var configLoader = appRootObject.AddComponent<ConfigLoader>();
            appRootObject.AddComponent<EnvironmentResolver>();
            appRootObject.AddComponent<SessionInitializer>();
            var sceneEntry = appRootObject.AddComponent<SceneEntry>();

            var apiConfig = EnsureApiConfigAsset();
            AssignSerializedReference(appRoot, "apiConfig", apiConfig);
            AssignSerializedReference(configLoader, "apiConfig", apiConfig);
            AssignSerializedString(sceneEntry, "mainSceneName", SceneKeys.Main);
            AssignSerializedString(sceneEntry, "notebookUiSceneName", SceneKeys.NotebookUi);
            AssignSerializedBool(sceneEntry, "loadMainSceneOnStart", true);
            AssignSerializedBool(sceneEntry, "loadAdditively", true);
            AssignSerializedBool(sceneEntry, "loadNotebookUiScene", true);
            AssignSerializedBool(sceneEntry, "setLoadedSceneActive", true);
            AssignSerializedBool(sceneEntry, "runInstallersAfterLoad", true);
            AssignSerializedBool(sceneEntry, "forceGenerateOnBootstrapLoad", false);

            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("BootstrapScene rebuilt for additive MainRoomScene loading.");
        }

        [MenuItem("Dotshelf/Refactor/Validate Scene Split")]
        public static void ValidateSceneSplit()
        {
            RequireFile(BootstrapScenePath);
            RequireFile(MainRoomScenePath);
            RequireFile(NotebookUiScenePath);
            if (File.Exists("Assets/Scenes/MainScene.unity"))
            {
                throw new InvalidOperationException("Legacy Assets/Scenes/MainScene.unity still exists.");
            }

            ValidateBuildSettings();
            ValidateBootstrapScene();
            ValidateAdditiveMainRoomAndNotebookUiLoad();
            MainScenePrefabExtractionUtility.ValidateMainSceneComposition();

            Debug.Log("Scene split validation passed.");
        }

        [MenuItem("Dotshelf/Refactor/Extract Notebook UI Scene")]
        public static void ExtractNotebookUiScene()
        {
            RequireFile(LibraryRoomBackgroundPrefabPath);
            RequireFile(NotebookStationPrefabPath);

            var roomPrefabRoot = PrefabUtility.LoadPrefabContents(LibraryRoomBackgroundPrefabPath);
            Vector3 notebookPosition;
            Quaternion notebookRotation;
            Vector3 notebookScale;
            try
            {
                var nestedNotebook = FindChildRecursive(roomPrefabRoot.transform, "NotebookStation");
                if (nestedNotebook == null)
                {
                    notebookPosition = new Vector3(-1.275f, 0f, 0.12f);
                    notebookRotation = Quaternion.identity;
                    notebookScale = Vector3.one;
                }
                else
                {
                    notebookPosition = nestedNotebook.position;
                    notebookRotation = nestedNotebook.rotation;
                    notebookScale = nestedNotebook.lossyScale;
                    UnityEngine.Object.DestroyImmediate(nestedNotebook.gameObject);
                    PrefabUtility.SaveAsPrefabAsset(roomPrefabRoot, LibraryRoomBackgroundPrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(roomPrefabRoot);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var notebookPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NotebookStationPrefabPath);
            if (notebookPrefab == null)
            {
                throw new InvalidOperationException($"NotebookStation prefab not found: {NotebookStationPrefabPath}");
            }

            var notebookInstance = (GameObject)PrefabUtility.InstantiatePrefab(notebookPrefab, scene);
            if (notebookInstance == null)
            {
                throw new InvalidOperationException("Failed to instantiate NotebookStation prefab into NotebookUIScene.");
            }

            notebookInstance.transform.position = notebookPosition;
            notebookInstance.transform.rotation = notebookRotation;
            notebookInstance.transform.localScale = notebookScale;

            EditorSceneManager.SaveScene(scene, NotebookUiScenePath);
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"NotebookUIScene extracted at {NotebookUiScenePath}.");
        }

        private static void ValidateBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0 || scenes[0].path != BootstrapScenePath || !scenes[0].enabled)
            {
                throw new InvalidOperationException("BootstrapScene must be the first enabled build scene.");
            }

            RequireBuildScene(MainRoomScenePath);
            RequireBuildScene(NotebookUiScenePath);
            RequireBuildScene(LoginScenePath);
        }

        private static void ValidateBootstrapScene()
        {
            EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            var entry = UnityEngine.Object.FindFirstObjectByType<SceneEntry>(FindObjectsInactive.Include);
            if (entry == null)
            {
                throw new InvalidOperationException("BootstrapScene has no SceneEntry.");
            }

            var serialized = new SerializedObject(entry);
            RequireSerializedString(serialized, "mainSceneName", SceneKeys.Main);
            RequireSerializedString(serialized, "notebookUiSceneName", SceneKeys.NotebookUi);
            RequireSerializedBool(serialized, "loadMainSceneOnStart", true);
            RequireSerializedBool(serialized, "loadAdditively", true);
            RequireSerializedBool(serialized, "loadNotebookUiScene", true);
            RequireSerializedBool(serialized, "setLoadedSceneActive", true);
            RequireSerializedBool(serialized, "runInstallersAfterLoad", true);
            RequireSerializedBool(serialized, "forceGenerateOnBootstrapLoad", false);

            if (UnityEngine.Object.FindFirstObjectByType<AppRoot>(FindObjectsInactive.Include) == null)
            {
                throw new InvalidOperationException("BootstrapScene has no AppRoot.");
            }

            if (UnityEngine.Object.FindFirstObjectByType<ConfigLoader>(FindObjectsInactive.Include) == null)
            {
                throw new InvalidOperationException("BootstrapScene has no ConfigLoader.");
            }
        }

        private static void ValidateAdditiveMainRoomAndNotebookUiLoad()
        {
            EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            var mainRoomScene = EditorSceneManager.OpenScene(MainRoomScenePath, OpenSceneMode.Additive);
            var notebookUiScene = EditorSceneManager.OpenScene(NotebookUiScenePath, OpenSceneMode.Additive);
            if (!SceneManager.SetActiveScene(mainRoomScene))
            {
                throw new InvalidOperationException("Failed to set MainRoomScene active during additive smoke validation.");
            }

            RequireNoNestedNotebookStationInRoomPrefab();
            RequireNotebookUiScene(notebookUiScene);

            var installerCount = 0;
            foreach (var root in mainRoomScene.GetRootGameObjects())
            {
                foreach (var installer in root.GetComponentsInChildren<BookshelfPorting.Runtime.BookshelfSceneInstaller>(true))
                {
                    installer.InstallScene(forceGenerate: false);
                    installerCount++;
                }
            }

            if (installerCount == 0)
            {
                throw new InvalidOperationException("MainRoomScene has no BookshelfSceneInstaller during additive smoke validation.");
            }

            RequireNoEmbeddedNotebookStation(mainRoomScene);
            if (UnityEngine.Object.FindFirstObjectByType<NotebookScreenController>(FindObjectsInactive.Include) == null)
            {
                throw new InvalidOperationException("NotebookScreenController is not available after additive load.");
            }

            Debug.Log($"Additive MainRoomScene + NotebookUIScene smoke validation passed with {installerCount} installer(s).");
        }


        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true),
                new EditorBuildSettingsScene(LoginScenePath, true),
                new EditorBuildSettingsScene(MainRoomScenePath, true),
                new EditorBuildSettingsScene(NotebookUiScenePath, true),
                new EditorBuildSettingsScene(StartScenePath, true),
            };
        }

        private static void RequireBuildScene(string path)
        {
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.path == path && scene.enabled)
                {
                    return;
                }
            }

            throw new InvalidOperationException($"Build settings missing enabled scene: {path}");
        }

        private static void RequireNoNestedNotebookStationInRoomPrefab()
        {
            var roomPrefabRoot = PrefabUtility.LoadPrefabContents(LibraryRoomBackgroundPrefabPath);
            try
            {
                if (FindChildRecursive(roomPrefabRoot.transform, "NotebookStation") != null)
                {
                    throw new InvalidOperationException("LibraryRoomBackground prefab still contains NotebookStation.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(roomPrefabRoot);
            }
        }

        private static void RequireNotebookUiScene(Scene notebookUiScene)
        {
            var stationCount = 0;
            var controllerCount = 0;
            foreach (var root in notebookUiScene.GetRootGameObjects())
            {
                var transforms = root.GetComponentsInChildren<Transform>(true);
                for (var i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i].name == "NotebookStation")
                    {
                        stationCount++;
                    }
                }

                controllerCount += root.GetComponentsInChildren<NotebookScreenController>(true).Length;
            }

            if (stationCount != 1)
            {
                throw new InvalidOperationException($"NotebookUIScene must contain exactly one NotebookStation. Actual: {stationCount}");
            }

            if (controllerCount == 0)
            {
                throw new InvalidOperationException("NotebookUIScene has no NotebookScreenController.");
            }
        }

        private static void RequireNoEmbeddedNotebookStation(Scene mainRoomScene)
        {
            foreach (var root in mainRoomScene.GetRootGameObjects())
            {
                var roomRoot = root.transform.Find("RoomRoot");
                var embedded = roomRoot != null ? roomRoot.Find("GuestbookBoard/NotebookStation") : null;
                if (embedded != null)
                {
                    throw new InvalidOperationException("MainRoomScene still contains embedded RoomRoot/GuestbookBoard/NotebookStation.");
                }
            }
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            if (parent.name == name)
            {
                return parent;
            }

            for (var i = 0; i < parent.childCount; i++)
            {
                var match = FindChildRecursive(parent.GetChild(i), name);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static AppApiConfig EnsureApiConfigAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AppApiConfig>(ApiConfigPath);
            if (existing != null)
            {
                return existing;
            }

            var directory = Path.GetDirectoryName(ApiConfigPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var config = ScriptableObject.CreateInstance<AppApiConfig>();
            AssetDatabase.CreateAsset(config, ApiConfigPath);
            return config;
        }

        private static void RequireFile(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Required scene file is missing.", path);
            }
        }

        private static void AssignSerializedReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Serialized property not found: {propertyName}");
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignSerializedString(UnityEngine.Object target, string propertyName, string value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Serialized property not found: {propertyName}");
            }

            property.stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignSerializedBool(UnityEngine.Object target, string propertyName, bool value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Serialized property not found: {propertyName}");
            }

            property.boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RequireSerializedString(SerializedObject serialized, string propertyName, string expectedValue)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null || property.stringValue != expectedValue)
            {
                throw new InvalidOperationException($"Unexpected SceneEntry.{propertyName}: {property?.stringValue}");
            }
        }

        private static void RequireSerializedBool(SerializedObject serialized, string propertyName, bool expectedValue)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null || property.boolValue != expectedValue)
            {
                throw new InvalidOperationException($"Unexpected SceneEntry.{propertyName}: {property?.boolValue}");
            }
        }
    }
}
