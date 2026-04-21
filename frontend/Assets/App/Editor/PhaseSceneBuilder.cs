using System.IO;
using App.Bootstrap;
using App.Config;
using App.Features.LibraryRoom;
using BookshelfPorting.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DebugHudComponent = App.Features.Debug.DebugHud;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace App.Editor
{
    public static class PhaseSceneBuilder
    {
        private const string ApiConfigPath = "Assets/App/Config/AppApiConfig.asset";
        private const string BootstrapScenePath = "Assets/Scenes/BootstrapScene.unity";
        private const string MainScenePath = "Assets/Scenes/MainScene.unity";

        [MenuItem("Tools/Project Setup/Run Phase 0-1 Setup")]
        public static void RunPhaseZeroAndOneSetup()
        {
            FolderStructureCreator.CreateFolderStructure();
            EnsureApiConfigAsset();
            CreateBootstrapScene();
            CreateMainScene();
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Phase 0-1 setup completed. MainScene is the first build scene.");
        }

        [MenuItem("Tools/Project Setup/Build MainScene Content")]
        public static void BuildMainSceneContent()
        {
            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            var installer = Object.FindFirstObjectByType<BookshelfSceneInstaller>();
            if (installer == null)
            {
                installer = new GameObject("BookshelfSceneInstaller").AddComponent<BookshelfSceneInstaller>();
            }

            installer.InstallScene(forceGenerate: true);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MainScene content generated and saved.");
        }

        public static void CreateBootstrapScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var appRootObject = new GameObject("AppRoot");
            var appRoot = appRootObject.AddComponent<AppRoot>();
            var configLoader = appRootObject.AddComponent<ConfigLoader>();
            appRootObject.AddComponent<EnvironmentResolver>();
            appRootObject.AddComponent<SessionInitializer>();
            appRootObject.AddComponent<SceneEntry>();

            var apiConfig = EnsureApiConfigAsset();
            AssignSerializedReference(appRoot, "apiConfig", apiConfig);
            AssignSerializedReference(configLoader, "apiConfig", apiConfig);

            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
        }

        public static void CreateMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var world = new GameObject("World").transform;
            var roomRoot = CreateChild(world, "RoomRoot");
            var placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            placeholder.name = "RoomPlaceholder";
            placeholder.transform.SetParent(roomRoot, false);
            placeholder.transform.localPosition = new Vector3(0f, -0.055f, 0f);
            placeholder.transform.localScale = new Vector3(4.2f, 0.1f, 4.2f);

            var cameraRoot = CreateChild(world, "CameraRoot");
            var mainCamera = new GameObject("MainCamera");
            mainCamera.tag = "MainCamera";
            mainCamera.transform.SetParent(cameraRoot, false);
            mainCamera.transform.position = new Vector3(1.45f, 1.38f, -1.83f);
            mainCamera.transform.rotation = Quaternion.LookRotation((new Vector3(-1.05f, 0.98f, 1.18f) - mainCamera.transform.position).normalized, Vector3.up);
            mainCamera.AddComponent<Camera>();
            mainCamera.AddComponent<AudioListener>();
            mainCamera.AddComponent<UniversalAdditionalCameraData>();
            CreateChild(cameraRoot, "VCam_QuarterView");
            CreateChild(cameraRoot, "VCam_ShelfFront");
            CreateChild(cameraRoot, "VCam_BookFocus");
            CreateChild(cameraRoot, "VCam_DashboardFocus");

            var shelfRoot = CreateChild(world, "ShelfRoot");
            CreateChild(shelfRoot, "ShelfTurntablePivot");
            var bookRoot = CreateChild(world, "BookRoot");
            CreateChild(bookRoot, "DragAnchor");
            CreateChild(world, "DashboardRoot");

            var interactionPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            interactionPlane.name = "InteractionPlane";
            interactionPlane.transform.SetParent(world, false);
            interactionPlane.transform.localScale = new Vector3(0.4f, 1f, 0.4f);

            var ui = new GameObject("UI").transform;
            CreateCanvas(ui, "HUDCanvas", 0);
            CreateCanvas(ui, "OverlayCanvas", 10);
            CreateCanvas(ui, "PopupCanvas", 20);
            var debugCanvas = CreateCanvas(ui, "DebugCanvas", 100);

            var systems = new GameObject("Systems");
            var roomState = systems.AddComponent<RoomStateController>();
            var cameraState = systems.AddComponent<CameraStateController>();
            var installer = systems.AddComponent<BookshelfSceneInstaller>();
            var debugHud = systems.AddComponent<DebugHudComponent>();

            var statusText = CreateDebugStatusText(debugCanvas.transform);
            AssignSerializedReference(debugHud, "roomStateController", roomState);
            AssignSerializedReference(debugHud, "cameraStateController", cameraState);
            AssignSerializedReference(debugHud, "statusText", statusText);

            CreateEventSystem();
            installer.InstallScene(forceGenerate: true);
            EditorSceneManager.SaveScene(scene, MainScenePath);
        }

        private static AppApiConfig EnsureApiConfigAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AppApiConfig>(ApiConfigPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ApiConfigPath));
            var config = ScriptableObject.CreateInstance<AppApiConfig>();
            AssetDatabase.CreateAsset(config, ApiConfigPath);
            return config;
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Canvas CreateCanvas(Transform parent, string name, int sortOrder)
        {
            var canvasObject = new GameObject(name);
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static Text CreateDebugStatusText(Transform parent)
        {
            var textObject = new GameObject("StateStatusText");
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;

            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(16f, -16f);
            rect.sizeDelta = new Vector2(320f, 120f);
            return text;
        }

        private static void CreateEventSystem()
        {
            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
            eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainScenePath, true),
                new EditorBuildSettingsScene(BootstrapScenePath, true)
            };
        }

        private static void AssignSerializedReference(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
