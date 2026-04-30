using System;
using System.IO;
using App.Bootstrap;
using App.Config;
using App.Infrastructure.Api;
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
        private const double RuntimeSmokeTimeoutSeconds = 45d;
        private const string RuntimeSmokeActiveKey = "Dotshelf.MainSceneSplit.RuntimeSmoke.Active";
        private const string RuntimeSmokeStartTimeKey = "Dotshelf.MainSceneSplit.RuntimeSmoke.StartedAt";
        private const string RuntimeSmokeFrameCountKey = "Dotshelf.MainSceneSplit.RuntimeSmoke.FrameCount";
        private const string RuntimeSmokeExitCodeKey = "Dotshelf.MainSceneSplit.RuntimeSmoke.ExitCode";
        private const string RuntimeSmokeNotebookFocusRequestedKey = "Dotshelf.MainSceneSplit.RuntimeSmoke.NotebookFocusRequested";

        private static double runtimeSmokeStartedAt;
        private static int runtimeSmokeFrameCount;
        private static int runtimeSmokeExitCode = -1;
        private static bool runtimeSmokeNotebookFocusRequested;

        [InitializeOnLoadMethod]
        private static void ResumeRuntimeSmokeValidationIfNeeded()
        {
            if (!SessionState.GetBool(RuntimeSmokeActiveKey, false))
            {
                return;
            }

            runtimeSmokeStartedAt = SessionState.GetFloat(RuntimeSmokeStartTimeKey, (float)EditorApplication.timeSinceStartup);
            runtimeSmokeFrameCount = SessionState.GetInt(RuntimeSmokeFrameCountKey, 0);
            runtimeSmokeExitCode = SessionState.GetInt(RuntimeSmokeExitCodeKey, -1);
            runtimeSmokeNotebookFocusRequested = SessionState.GetBool(RuntimeSmokeNotebookFocusRequestedKey, false);
            EditorApplication.update -= TickRuntimeSmokeValidation;
            EditorApplication.update += TickRuntimeSmokeValidation;
        }

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
            AssignSerializedBool(sceneEntry, "loadNotebookUiScene", false);
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

        [MenuItem("Dotshelf/Refactor/Run Bootstrap Runtime Smoke Validation")]
        public static void RunBootstrapRuntimeSmokeValidation()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Cannot start runtime smoke validation while play mode is already active.");
            }

            RequireFile(BootstrapScenePath);
            RequireFile(MainRoomScenePath);
            RequireFile(NotebookUiScenePath);

            EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            runtimeSmokeStartedAt = EditorApplication.timeSinceStartup;
            runtimeSmokeFrameCount = 0;
            runtimeSmokeExitCode = -1;
            SessionState.SetBool(RuntimeSmokeActiveKey, true);
            SessionState.SetFloat(RuntimeSmokeStartTimeKey, (float)runtimeSmokeStartedAt);
            SessionState.SetInt(RuntimeSmokeFrameCountKey, runtimeSmokeFrameCount);
            SessionState.SetInt(RuntimeSmokeExitCodeKey, runtimeSmokeExitCode);
            SessionState.SetBool(RuntimeSmokeNotebookFocusRequestedKey, false);
            runtimeSmokeNotebookFocusRequested = false;
            EditorApplication.update -= TickRuntimeSmokeValidation;
            EditorApplication.update += TickRuntimeSmokeValidation;
            EditorApplication.EnterPlaymode();
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
            if (scenes.Length == 0 || scenes[0].path != StartScenePath || !scenes[0].enabled)
            {
                throw new InvalidOperationException("StartScene must be the first enabled build scene so the login/start flow is preserved.");
            }

            RequireBuildScene(BootstrapScenePath);
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
            RequireSerializedBool(serialized, "loadNotebookUiScene", false);
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
            RequireNotebookControllerCount(mainRoomScene, 0, "MainRoomScene must not own NotebookScreenController after scene split.");
            RequireNotebookControllerCount(notebookUiScene, 1, "NotebookUIScene must own exactly one NotebookScreenController after additive load.");
            if (FindNotebookControllerInScene(notebookUiScene) == null)
            {
                throw new InvalidOperationException("NotebookScreenController is not available after additive load.");
            }

            Debug.Log($"Additive MainRoomScene + NotebookUIScene smoke validation passed with {installerCount} installer(s).");
        }


        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(StartScenePath, true),
                new EditorBuildSettingsScene(LoginScenePath, true),
                new EditorBuildSettingsScene(BootstrapScenePath, true),
                new EditorBuildSettingsScene(MainRoomScenePath, true),
                new EditorBuildSettingsScene(NotebookUiScenePath, true),
            };
        }

        private static void TickRuntimeSmokeValidation()
        {
            if (runtimeSmokeExitCode >= 0)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.update -= TickRuntimeSmokeValidation;
                    SessionState.SetBool(RuntimeSmokeActiveKey, false);
                    if (Application.isBatchMode)
                    {
                        EditorApplication.Exit(runtimeSmokeExitCode);
                    }
                }

                return;
            }

            if (EditorApplication.timeSinceStartup - runtimeSmokeStartedAt > RuntimeSmokeTimeoutSeconds)
            {
                FinishRuntimeSmokeValidation(1, $"Runtime smoke validation timed out after {RuntimeSmokeTimeoutSeconds:0}s.");
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                return;
            }

            runtimeSmokeFrameCount++;
            SessionState.SetInt(RuntimeSmokeFrameCountKey, runtimeSmokeFrameCount);
            if (runtimeSmokeFrameCount < 10)
            {
                return;
            }

            var mainRoomScene = SceneManager.GetSceneByName(SceneKeys.MainRoom);
            var notebookUiScene = SceneManager.GetSceneByName(SceneKeys.NotebookUi);
            if (!mainRoomScene.IsValid() || !mainRoomScene.isLoaded)
            {
                return;
            }

            try
            {
                if (!runtimeSmokeNotebookFocusRequested)
                {
                    if (notebookUiScene.IsValid() && notebookUiScene.isLoaded)
                    {
                        throw new InvalidOperationException("NotebookUIScene was preloaded before notebook focus. Lazy-load expectation failed.");
                    }

                    RequireNotebookControllerCount(mainRoomScene, 0, "MainRoomScene owns a NotebookScreenController before notebook focus.");

                    var experienceManager = UnityEngine.Object.FindFirstObjectByType<BookshelfExperienceManager>(FindObjectsInactive.Include);
                    if (experienceManager == null)
                    {
                        throw new InvalidOperationException("Runtime smoke validation found no BookshelfExperienceManager.");
                    }

                    var cameraController = UnityEngine.Object.FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
                    if (cameraController != null && cameraController.IsTransitioning)
                    {
                        return;
                    }

                    runtimeSmokeNotebookFocusRequested = true;
                    SessionState.SetBool(RuntimeSmokeNotebookFocusRequestedKey, true);
                    experienceManager.FocusNotebook();
                    return;
                }

                notebookUiScene = SceneManager.GetSceneByName(SceneKeys.NotebookUi);
                if (!notebookUiScene.IsValid() || !notebookUiScene.isLoaded)
                {
                    return;
                }

                var loader = UnityEngine.Object.FindFirstObjectByType<NotebookUiSceneLoader>(FindObjectsInactive.Include);
                if (loader != null && loader.IsLoading)
                {
                    return;
                }

                if (SceneManager.GetActiveScene().name != SceneKeys.MainRoom)
                {
                    throw new InvalidOperationException($"Active scene mismatch. Expected {SceneKeys.MainRoom}, got {SceneManager.GetActiveScene().name}.");
                }

                RequireNotebookUiScene(notebookUiScene);
                RequireNoEmbeddedNotebookStation(mainRoomScene);
                RequireNotebookControllerCount(mainRoomScene, 0, "MainRoomScene owns a NotebookScreenController after lazy-loading NotebookUIScene.");
                RequireNotebookControllerCount(notebookUiScene, 1, "NotebookUIScene must own exactly one NotebookScreenController after lazy-load.");

                var installer = UnityEngine.Object.FindFirstObjectByType<BookshelfSceneInstaller>(FindObjectsInactive.Include);
                if (installer == null)
                {
                    throw new InvalidOperationException("Runtime smoke validation found no BookshelfSceneInstaller.");
                }

                var generator = UnityEngine.Object.FindFirstObjectByType<BookshelfGenerator>(FindObjectsInactive.Include);
                if (generator == null)
                {
                    throw new InvalidOperationException("Runtime smoke validation found no BookshelfGenerator.");
                }

                var catalogCount = generator.BookCatalogItems.Count;
                if (catalogCount == 0)
                {
                    throw new InvalidOperationException("Runtime smoke validation loaded zero book catalog items.");
                }

                if (FindNotebookControllerInScene(notebookUiScene) == null)
                {
                    throw new InvalidOperationException("Runtime smoke validation found no NotebookScreenController.");
                }

                if (UnityEngine.Object.FindFirstObjectByType<APIManager>(FindObjectsInactive.Include) == null)
                {
                    throw new InvalidOperationException("Runtime smoke validation found no APIManager.");
                }

                Debug.Log($"Bootstrap runtime smoke validation passed. scenes={SceneKeys.MainRoom}+lazy {SceneKeys.NotebookUi}, catalog={catalogCount}, frames={runtimeSmokeFrameCount}.");
                FinishRuntimeSmokeValidation(0, null);
            }
            catch (Exception exception)
            {
                FinishRuntimeSmokeValidation(1, exception.ToString());
            }
        }

        private static void FinishRuntimeSmokeValidation(int exitCode, string failureMessage)
        {
            runtimeSmokeExitCode = exitCode;
            SessionState.SetInt(RuntimeSmokeExitCodeKey, exitCode);
            SessionState.SetBool(RuntimeSmokeNotebookFocusRequestedKey, false);
            if (!string.IsNullOrWhiteSpace(failureMessage))
            {
                Debug.LogError(failureMessage);
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.ExitPlaymode();
            }
            else if (Application.isBatchMode)
            {
                EditorApplication.update -= TickRuntimeSmokeValidation;
                SessionState.SetBool(RuntimeSmokeActiveKey, false);
                EditorApplication.Exit(exitCode);
            }
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

        private static void RequireNotebookControllerCount(Scene scene, int expectedCount, string message)
        {
            var actualCount = CountNotebookControllersInScene(scene);
            if (actualCount != expectedCount)
            {
                throw new InvalidOperationException($"{message} Expected: {expectedCount}, Actual: {actualCount}");
            }
        }

        private static int CountNotebookControllersInScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return 0;
            }

            var count = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                count += root.GetComponentsInChildren<NotebookScreenController>(true).Length;
            }

            return count;
        }

        private static NotebookScreenController FindNotebookControllerInScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                var controller = root.GetComponentInChildren<NotebookScreenController>(true);
                if (controller != null)
                {
                    return controller;
                }
            }

            return null;
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
