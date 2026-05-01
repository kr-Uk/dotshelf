using System.Linq;
using App.Config;
using BookshelfPorting.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dotshelf.EditorTools
{
    public static class DashboardPanelRewireUtility
    {
        const string MainRoomScenePath = "Assets/Scenes/MainRoomScene.unity";
        const string CompositionConfigPath = "Assets/App/Composition/Configs/MainSceneCompositionConfig.asset";
        const string DashboardPrefabFallbackPath = "Assets/App/Features/Dashboard/Presentation/DashboardScreenPanel.prefab";
        const string PanelObjectName = "DashboardScreenPanel";
        const string ExternalPanelField = "externalDashboardPanel";

        [MenuItem("Dotshelf/Refactor/Rewire Dashboard Panel In MainRoomScene")]
        public static void Rewire()
        {
            var scene = EnsureMainRoomSceneOpen();
            if (!scene.IsValid())
            {
                Debug.LogError($"[DashboardRewire] Failed to open {MainRoomScenePath}");
                return;
            }

            var prefab = ResolveDashboardPrefab();
            if (prefab == null)
            {
                Debug.LogError($"[DashboardRewire] Dashboard prefab not found via config ({CompositionConfigPath}) or fallback ({DashboardPrefabFallbackPath})");
                return;
            }

            var existing = FindInScene(scene, PanelObjectName);
            GameObject panelInstance;
            if (existing != null)
            {
                Debug.Log($"[DashboardRewire] Existing '{PanelObjectName}' found in scene — reusing.");
                panelInstance = existing;
            }
            else
            {
                panelInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                panelInstance.name = PanelObjectName;
                Debug.Log($"[DashboardRewire] Instantiated '{PanelObjectName}' from prefab.");
            }

            var manager = FindBookshelfExperienceManager(scene);
            if (manager == null)
            {
                Debug.LogError("[DashboardRewire] BookshelfExperienceManager not found in MainRoomScene.");
                return;
            }

            var so = new SerializedObject(manager);
            var prop = so.FindProperty(ExternalPanelField);
            if (prop == null)
            {
                Debug.LogError($"[DashboardRewire] Field '{ExternalPanelField}' not found on BookshelfExperienceManager (renamed?).");
                return;
            }
            if (prop.objectReferenceValue != panelInstance)
            {
                prop.objectReferenceValue = panelInstance;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(manager);
                Debug.Log($"[DashboardRewire] Wired '{ExternalPanelField}' on '{manager.name}' → '{panelInstance.name}'.");
            }
            else
            {
                Debug.Log($"[DashboardRewire] '{ExternalPanelField}' already references '{panelInstance.name}' — no change.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[DashboardRewire] Saved {scene.path}. Toggle 'useExternalDashboardPanel' on '{manager.name}' when input routing fix is in.");
        }

        static Scene EnsureMainRoomSceneOpen()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.path == MainRoomScenePath && s.isLoaded) return s;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return default;
            return EditorSceneManager.OpenScene(MainRoomScenePath, OpenSceneMode.Single);
        }

        static GameObject FindInScene(Scene scene, string name)
        {
            return scene.GetRootGameObjects()
                .SelectMany(GetAllDescendants)
                .FirstOrDefault(go => go.name == name);
        }

        static System.Collections.Generic.IEnumerable<GameObject> GetAllDescendants(GameObject root)
        {
            yield return root;
            foreach (Transform child in root.transform)
                foreach (var d in GetAllDescendants(child.gameObject))
                    yield return d;
        }

        static BookshelfExperienceManager FindBookshelfExperienceManager(Scene scene)
        {
            return scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<BookshelfExperienceManager>(true))
                .FirstOrDefault();
        }

        static GameObject ResolveDashboardPrefab()
        {
            var config = AssetDatabase.LoadAssetAtPath<MainSceneCompositionConfig>(CompositionConfigPath);
            if (config != null && config.DashboardScreenPanelPrefab != null)
            {
                Debug.Log($"[DashboardRewire] Using prefab from {CompositionConfigPath}.");
                return config.DashboardScreenPanelPrefab;
            }
            Debug.LogWarning($"[DashboardRewire] Composition config slot empty — falling back to {DashboardPrefabFallbackPath}.");
            return AssetDatabase.LoadAssetAtPath<GameObject>(DashboardPrefabFallbackPath);
        }
    }
}
