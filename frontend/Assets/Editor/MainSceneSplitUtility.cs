using System;
using System.IO;
using App.Bootstrap;
using App.Config;
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
        private const string StartScenePath = "Assets/Scenes/StartScene.unity";

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
            AssignSerializedBool(sceneEntry, "loadMainSceneOnStart", true);
            AssignSerializedBool(sceneEntry, "loadAdditively", true);
            AssignSerializedBool(sceneEntry, "setLoadedSceneActive", true);
            AssignSerializedBool(sceneEntry, "runInstallersAfterLoad", true);

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
            if (File.Exists("Assets/Scenes/MainScene.unity"))
            {
                throw new InvalidOperationException("Legacy Assets/Scenes/MainScene.unity still exists.");
            }

            ValidateBuildSettings();
            ValidateBootstrapScene();
            ValidateAdditiveMainRoomLoad();
            MainScenePrefabExtractionUtility.ValidateMainSceneComposition();

            Debug.Log("Scene split validation passed.");
        }

        private static void ValidateBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0 || scenes[0].path != BootstrapScenePath || !scenes[0].enabled)
            {
                throw new InvalidOperationException("BootstrapScene must be the first enabled build scene.");
            }

            RequireBuildScene(MainRoomScenePath);
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
            RequireSerializedBool(serialized, "loadMainSceneOnStart", true);
            RequireSerializedBool(serialized, "loadAdditively", true);
            RequireSerializedBool(serialized, "setLoadedSceneActive", true);
            RequireSerializedBool(serialized, "runInstallersAfterLoad", true);

            if (UnityEngine.Object.FindFirstObjectByType<AppRoot>(FindObjectsInactive.Include) == null)
            {
                throw new InvalidOperationException("BootstrapScene has no AppRoot.");
            }

            if (UnityEngine.Object.FindFirstObjectByType<ConfigLoader>(FindObjectsInactive.Include) == null)
            {
                throw new InvalidOperationException("BootstrapScene has no ConfigLoader.");
            }
        }

        private static void ValidateAdditiveMainRoomLoad()
        {
            EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            var mainRoomScene = EditorSceneManager.OpenScene(MainRoomScenePath, OpenSceneMode.Additive);
            if (!SceneManager.SetActiveScene(mainRoomScene))
            {
                throw new InvalidOperationException("Failed to set MainRoomScene active during additive smoke validation.");
            }

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

            Debug.Log($"Additive MainRoomScene smoke validation passed with {installerCount} installer(s).");
        }


        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true),
                new EditorBuildSettingsScene(LoginScenePath, true),
                new EditorBuildSettingsScene(MainRoomScenePath, true),
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
