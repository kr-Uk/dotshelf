using App.Application.Books;
using App.Bootstrap;
using App.Config;
using App.Infrastructure.Api;
using App.Infrastructure.Mock;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace BookshelfPorting.Runtime
{
    [ExecuteAlways]
    public sealed class BookshelfSceneInstaller : MonoBehaviour
    {
        [SerializeField] private bool bootstrapOnAwake = true;
        [SerializeField] private bool installInEditMode = true;
        [SerializeField] private bool saveGeneratedContentInEditMode = true;
        [SerializeField] private AppApiConfig apiConfig;
        [SerializeField] private bool useApiRepository = true;
        [SerializeField] private string defaultApiBaseUrl = "http://localhost:3001";
        [SerializeField] private int defaultRequestTimeoutSeconds = 10;

        private void Awake()
        {
            if (!Application.isPlaying || !bootstrapOnAwake)
            {
                return;
            }

            if (SceneManager.GetActiveScene().name != SceneKeys.Main)
            {
                return;
            }

            InstallScene(forceGenerate: true);
        }

#if UNITY_EDITOR
        private void OnEnable()
        {
            if (Application.isPlaying || !installInEditMode)
            {
                return;
            }

            if (SceneManager.GetActiveScene().name != SceneKeys.Main)
            {
                return;
            }

            QueueEditorInstall();
        }

        private void OnValidate()
        {
            if (Application.isPlaying || !installInEditMode)
            {
                return;
            }

            if (SceneManager.GetActiveScene().name != SceneKeys.Main)
            {
                return;
            }

            QueueEditorInstall();
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= InstallSceneForEditor;
        }

        [ContextMenu("Install Scene Content")]
        private void InstallSceneFromContextMenu()
        {
            InstallSceneForEditor();
        }

        private void QueueEditorInstall()
        {
            EditorApplication.delayCall -= InstallSceneForEditor;
            EditorApplication.delayCall += InstallSceneForEditor;
        }

        private void InstallSceneForEditor()
        {
            EditorApplication.delayCall -= InstallSceneForEditor;

            if (this == null || Application.isPlaying || !installInEditMode)
            {
                return;
            }

            if (SceneManager.GetActiveScene().name != SceneKeys.Main)
            {
                return;
            }

            InstallScene(forceGenerate: false);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            if (saveGeneratedContentInEditMode && !string.IsNullOrEmpty(gameObject.scene.path))
            {
                EditorSceneManager.SaveScene(gameObject.scene);
            }
        }
#endif

        public void InstallScene(bool forceGenerate)
        {
            InstallIntoActiveScene(forceGenerate, apiConfig, useApiRepository, defaultApiBaseUrl, defaultRequestTimeoutSeconds);
        }

        public static void InstallIntoActiveScene(bool forceGenerate)
        {
            InstallIntoActiveScene(forceGenerate, null, true, "http://localhost:3001", 10);
        }

        private static void InstallIntoActiveScene(
            bool forceGenerate,
            AppApiConfig apiConfig,
            bool useApiRepository,
            string defaultApiBaseUrl,
            int defaultRequestTimeoutSeconds)
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            }

            var systems = FindOrCreateRoot("BookshelfSystems");
            var state = GetOrAddComponent<BookshelfState>(systems);
            var materialFactory = GetOrAddComponent<MaterialFactory>(systems);
            var generator = GetOrAddComponent<BookshelfGenerator>(systems);
            var cameraController = GetOrAddComponent<CameraController>(systems);
            var interactionController = GetOrAddComponent<BookInteractionController>(systems);
            var runtimeDataStore = GetOrAddComponent<BookshelfRuntimeDataStore>(systems);
            var experienceManager = GetOrAddComponent<BookshelfExperienceManager>(systems);
            var environment = GetOrAddComponent<EnvironmentSetup>(systems);
            var loadBooksUseCase = new LoadMyBookshelfBooksUseCase(CreateBookCatalogRepository(apiConfig, useApiRepository, defaultApiBaseUrl, defaultRequestTimeoutSeconds));

            var camerasRoot = FindOrCreateRoot("Cameras").transform;
            var overviewPosition = new Vector3(1.55f, 1.30f, -1.95f);
            var overviewTarget = new Vector3(-0.55f, 0.72f, 0.62f);
            var overviewAnchor = FindOrCreateAnchor(
                camerasRoot,
                "OverviewAnchor",
                overviewPosition,
                Quaternion.LookRotation((overviewTarget - overviewPosition).normalized, Vector3.up));
            var frontalAnchor = FindOrCreateAnchor(camerasRoot, "FrontalAnchor", new Vector3(0f, 0.95f, -0.95f), Quaternion.LookRotation(new Vector3(0f, -0.1f, 2.15f).normalized, Vector3.up));
            var whiteboardAnchor = FindOrCreateAnchor(
                camerasRoot,
                "WhiteboardAnchor",
                new Vector3(-0.92f, 1.18f, 0.12f),
                Quaternion.LookRotation(Vector3.left, Vector3.up));
            var notebookPosition = new Vector3(-0.12f, 0.72f, -1.28f);
            var notebookTarget = new Vector3(-0.80f, 0.56f, -0.94f);
            var notebookAnchor = FindOrCreateAnchor(
                camerasRoot,
                "NotebookAnchor",
                notebookPosition,
                Quaternion.LookRotation((notebookTarget - notebookPosition).normalized, Vector3.up));
            var bookViewAnchor = FindOrCreateAnchor(camerasRoot, "BookViewAnchor", new Vector3(0f, 0.88f, -0.15f), Quaternion.Euler(0f, 0f, 0f));

            var environmentRoot = FindOrCreateRoot("Environment");
            var probeObject = FindOrCreateChild(environmentRoot.transform, "ReflectionProbe");
            probeObject.transform.position = new Vector3(0f, 1.2f, 0.5f);
            var probe = GetOrAddComponent<ReflectionProbe>(probeObject);

            cameraController.Configure(mainCamera, overviewAnchor, frontalAnchor, whiteboardAnchor, notebookAnchor, bookViewAnchor);
            cameraController.ConfigureViewSettings(58f, 55f, 56f, 24f, 0.65f);
            generator.Configure(state, materialFactory, loadBooksUseCase);
            interactionController.Configure(state, generator, cameraController, materialFactory, mainCamera, experienceManager);
            runtimeDataStore.Configure(state, materialFactory);
            experienceManager.Configure(state, cameraController, interactionController, runtimeDataStore, generator);
            environment.Configure(probe, null);

            mainCamera.transform.position = overviewAnchor.position;
            mainCamera.transform.rotation = overviewAnchor.rotation;
            mainCamera.fieldOfView = 58f;

            environment.Apply();

            if (forceGenerate || !HasGeneratedContent(generator))
            {
                generator.Generate();
            }
            else
            {
                generator.ApplyBookCatalogDataToExistingBooks();
            }

            generator.NormalizeNotebookStationPlacement();
            generator.EnsureRoomDecorations();
            AlignNotebookAnchor(notebookAnchor);

            if (Application.isPlaying)
            {
                cameraController.MoveToMode(CameraMode.Overview);
            }
        }

        private static IBookCatalogRepository CreateBookCatalogRepository(
            AppApiConfig apiConfig,
            bool useApiRepository,
            string defaultApiBaseUrl,
            int defaultRequestTimeoutSeconds)
        {
            var mockRepository = new MockBookCatalogRepository();
            if (!useApiRepository)
            {
                return mockRepository;
            }

            var baseUrl = apiConfig != null ? apiConfig.ResolveBaseUrl() : defaultApiBaseUrl;
            var timeoutSeconds = apiConfig != null
                ? apiConfig.RequestTimeoutSeconds
                : defaultRequestTimeoutSeconds;
            return new ApiBookCatalogRepository(baseUrl, timeoutSeconds, mockRepository);
        }

        private static bool HasGeneratedContent(BookshelfGenerator generator)
        {
            if (generator == null)
            {
                return false;
            }

            var root = generator.transform;
            if (root.Find("RoomRoot") == null ||
                root.Find("BookshelfTurntableRoot/BookshelfRoot") == null)
            {
                return false;
            }

            var guestbook = root.Find("RoomRoot/GuestbookBoard");
            if (guestbook == null)
            {
                return false;
            }

            if (guestbook.Find("dashboard_natural") != null)
            {
                return false;
            }

            if (guestbook.Find("Frame") == null || guestbook.Find("Surface") == null)
            {
                return false;
            }

            var desk = guestbook.Find("NotebookStation/Desk");
            if (desk == null)
            {
                return false;
            }

            if (desk.Find("Desktop") != null)
            {
                return false;
            }

            var table = desk.Find("table_natural");
            if (table == null)
            {
                return false;
            }

            var s = table.localScale;
            if (Mathf.Approximately(s.x, 1f) && Mathf.Approximately(s.y, 1f) && Mathf.Approximately(s.z, 1f))
            {
                return false;
            }

            return true;
        }

        private static GameObject FindOrCreateRoot(string name)
        {
            var existing = GameObject.Find(name);
            return existing != null ? existing : new GameObject(name);
        }

        private static GameObject FindOrCreateChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
            {
                return child.gameObject;
            }

            var childObject = new GameObject(name);
            childObject.transform.SetParent(parent, false);
            return childObject;
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static Transform FindOrCreateAnchor(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var anchor = parent.Find(name);
            if (anchor == null)
            {
                anchor = new GameObject(name).transform;
                anchor.SetParent(parent, false);
            }

            anchor.position = position;
            anchor.rotation = rotation;
            return anchor;
        }

        private static void AlignNotebookAnchor(Transform notebookAnchor)
        {
            if (notebookAnchor == null)
            {
                return;
            }

            var notebookScreenController = Object.FindFirstObjectByType<NotebookScreenController>();
            var screen = notebookScreenController != null
                ? notebookScreenController.transform.Find("Screen")
                : null;
            if (screen == null)
            {
                return;
            }

            var screenCenterTransform = screen.Find("ScreenCenter");
            var cameraFocusPoint = screen.Find("CameraFocusPoint");
            var screenCenter = screenCenterTransform != null
                ? screenCenterTransform.position
                : screen.TransformPoint(new Vector3(0f, 0.068f, 0.014f));
            var anchorPosition = cameraFocusPoint != null
                ? cameraFocusPoint.position
                : screen.TransformPoint(new Vector3(0f, -0.58f, 0.024f));

            notebookAnchor.position = anchorPosition;
            notebookAnchor.rotation = Quaternion.LookRotation((screenCenter - anchorPosition).normalized, Vector3.up);
        }
    }
}
