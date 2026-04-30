using System;
using App.Config;
using BookshelfPorting.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dotshelf.EditorTools
{
    public static class MainScenePrefabExtractionUtility
    {
        private const string MainScenePath = "Assets/Scenes/MainScene.unity";
        private const string PrefabFolder = "Assets/App/Composition/Prefabs";
        private const string ConfigFolder = "Assets/App/Composition/Configs";
        private const string ConfigPath = ConfigFolder + "/MainSceneCompositionConfig.asset";

        [MenuItem("Dotshelf/Refactor/Extract Main Scene Prefabs")]
        public static void ExtractMainScenePrefabs()
        {
            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            DisableEditModeSceneSaving();
            EnsureFolder(PrefabFolder);
            EnsureFolder(ConfigFolder);

            var notebookPrefab = Extract("NotebookStation", PrefabFolder + "/NotebookStation.prefab");
            var roomPrefab = Extract("RoomRoot", PrefabFolder + "/LibraryRoomBackground.prefab");
            var bookshelfPrefab = Extract("BookshelfTurntableRoot", PrefabFolder + "/BookshelfRig.prefab");
            var cameraPrefab = Extract("Cameras", PrefabFolder + "/CameraAnchors.prefab");
            var runtimePrefab = Extract("BookshelfSystems", PrefabFolder + "/RuntimeSystems.prefab");

            SaveCompositionConfig(runtimePrefab, roomPrefab, bookshelfPrefab, notebookPrefab, cameraPrefab);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("MainScene prefab extraction completed.");
        }

        [MenuItem("Dotshelf/Refactor/Validate Main Scene Composition")]
        public static void ValidateMainSceneComposition()
        {
            EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

            var config = AssetDatabase.LoadAssetAtPath<MainSceneCompositionConfig>(ConfigPath);
            if (config == null)
            {
                throw new InvalidOperationException($"Composition config not found: {ConfigPath}");
            }

            RequirePrefab(config.RuntimeSystemsPrefab, PrefabFolder + "/RuntimeSystems.prefab");
            RequirePrefab(config.RoomBackgroundPrefab, PrefabFolder + "/LibraryRoomBackground.prefab");
            RequirePrefab(config.BookshelfRigPrefab, PrefabFolder + "/BookshelfRig.prefab");
            RequirePrefab(config.NotebookStationPrefab, PrefabFolder + "/NotebookStation.prefab");
            RequirePrefab(config.CameraAnchorsPrefab, PrefabFolder + "/CameraAnchors.prefab");

            RequireScenePrefabInstance("BookshelfSystems", PrefabFolder + "/RuntimeSystems.prefab");
            RequireScenePrefabInstance("Cameras", PrefabFolder + "/CameraAnchors.prefab");
            RequireSceneObject("BookshelfSceneInstaller");
            RequireSceneObject("Main Camera");
            RequireSceneObject("Directional Light");
            RequireSceneObject("Global Volume");

            foreach (var installer in UnityEngine.Object.FindObjectsByType<BookshelfSceneInstaller>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var serialized = new SerializedObject(installer);
                if (serialized.FindProperty("installInEditMode").boolValue)
                {
                    throw new InvalidOperationException("BookshelfSceneInstaller.installInEditMode must be disabled after prefab extraction.");
                }

                if (serialized.FindProperty("saveGeneratedContentInEditMode").boolValue)
                {
                    throw new InvalidOperationException("BookshelfSceneInstaller.saveGeneratedContentInEditMode must be disabled after prefab extraction.");
                }
            }

            Debug.Log("MainScene composition validation passed.");
        }

        private static GameObject Extract(string objectName, string prefabPath)
        {
            var sceneObject = FindSceneObject(objectName);
            if (sceneObject == null)
            {
                throw new InvalidOperationException($"MainScene object not found: {objectName}");
            }

            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
                sceneObject,
                prefabPath,
                InteractionMode.AutomatedAction,
                out var success);
            if (!success || prefab == null)
            {
                throw new InvalidOperationException($"Failed to extract prefab: {objectName} -> {prefabPath}");
            }

            Debug.Log($"Extracted prefab: {objectName} -> {prefabPath}");
            return prefab;
        }

        private static void SaveCompositionConfig(
            GameObject runtimePrefab,
            GameObject roomPrefab,
            GameObject bookshelfPrefab,
            GameObject notebookPrefab,
            GameObject cameraPrefab)
        {
            var config = AssetDatabase.LoadAssetAtPath<MainSceneCompositionConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<MainSceneCompositionConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            config.Configure(runtimePrefab, roomPrefab, bookshelfPrefab, notebookPrefab, cameraPrefab);
            EditorUtility.SetDirty(config);
        }

        private static void RequirePrefab(GameObject prefab, string expectedPath)
        {
            if (prefab == null)
            {
                throw new InvalidOperationException($"Composition config has a missing prefab reference: {expectedPath}");
            }

            var actualPath = AssetDatabase.GetAssetPath(prefab);
            if (actualPath != expectedPath)
            {
                throw new InvalidOperationException($"Composition config prefab mismatch. Expected {expectedPath}, got {actualPath}");
            }
        }

        private static void RequireScenePrefabInstance(string objectName, string expectedPrefabPath)
        {
            var sceneObject = RequireSceneObject(objectName);
            var actualPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(sceneObject);
            if (actualPath != expectedPrefabPath)
            {
                throw new InvalidOperationException($"Scene object {objectName} is not connected to {expectedPrefabPath}. Actual: {actualPath}");
            }
        }

        private static GameObject RequireSceneObject(string objectName)
        {
            var sceneObject = FindSceneObject(objectName);
            if (sceneObject == null)
            {
                throw new InvalidOperationException($"MainScene object not found: {objectName}");
            }

            return sceneObject;
        }

        private static void DisableEditModeSceneSaving()
        {
            foreach (var installer in UnityEngine.Object.FindObjectsByType<BookshelfSceneInstaller>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var serialized = new SerializedObject(installer);
                serialized.FindProperty("installInEditMode").boolValue = false;
                serialized.FindProperty("saveGeneratedContentInEditMode").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(installer);
            }
        }

        private static GameObject FindSceneObject(string name)
        {
            var activeScene = SceneManager.GetActiveScene();
            foreach (var candidate in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.scene == activeScene && candidate.name == name)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void EnsureFolder(string folder)
        {
            var normalized = folder.Replace('\\', '/').Trim('/');
            var parts = normalized.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                throw new ArgumentException($"Folder must be under Assets: {folder}", nameof(folder));
            }

            var current = "Assets";
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
