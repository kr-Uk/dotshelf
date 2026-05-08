using App.Application.Books;
using App.Application.Notebook;
using App.Auth;
using App.Bootstrap;
using App.Config;
using App.Domain;
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
        private const string DefaultApiBaseUrl = ApiEndpointDefaults.DefaultBaseUrl;
        private const int DefaultRequestTimeoutSeconds = 10;

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

            if (this == null || Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || !installInEditMode)
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
            InstallIntoActiveScene(forceGenerate, apiConfig);
        }

        public static void InstallIntoActiveScene(bool forceGenerate)
        {
            InstallIntoActiveScene(forceGenerate, null);
        }

        private static void InstallIntoActiveScene(
            bool forceGenerate,
            AppApiConfig apiConfig)
        {
            ApplyRuntimePerformanceSettings();
            RemoveLegacyNotebookDashboardCanvases();

            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            }

            var systems = FindOrCreateRoot("BookshelfSystems");
            var apiMode = ResolveApiMode(apiConfig);
            var usesHttpApi = apiMode != AppApiMode.LocalMock;
            var apiBaseUrl = ResolveApiBaseUrl(apiConfig);
            var apiTimeoutSeconds = ResolveApiTimeoutSeconds(apiConfig);
            var apiAccessToken = ResolveApiAccessToken(apiConfig);
            var apiHealthCheckPath = apiConfig != null ? apiConfig.HealthCheckPath : "/health";
            if (Application.isPlaying)
            {
                Debug.Log($"Dotshelf API mode: {apiMode}, baseUrl={apiBaseUrl}, usesHttpApi={usesHttpApi}");
                if (apiMode == AppApiMode.LocalMock)
                {
                    Debug.LogWarning("Dotshelf LocalMock mode is active; no HTTP API requests will be sent.");
                }
            }

            var state = GetOrAddComponent<BookshelfState>(systems);
            var materialFactory = GetOrAddComponent<MaterialFactory>(systems);
            var generator = GetOrAddComponent<BookshelfGenerator>(systems);
            var cameraController = GetOrAddComponent<CameraController>(systems);
            var interactionController = GetOrAddComponent<BookInteractionController>(systems);
            var runtimeDataStore = GetOrAddComponent<BookshelfRuntimeDataStore>(systems);
            var experienceManager = GetOrAddComponent<BookshelfExperienceManager>(systems);
            var bookshelfApiSync = GetOrAddComponent<BookshelfApiSyncController>(systems);
            var environment = GetOrAddComponent<EnvironmentSetup>(systems);
            // MR #76 후속 (260503): APIManager 이중 인스턴스화 race fix.
            // 이전: GetOrAddComponent로 무조건 BookshelfSystems에 새 APIManager 추가 → RuntimeSystems
            // prefab의 APIManager와 동시 존재 → Awake duplicate destroy 로직이 race되며 Configure가
            // destroy 예정 인스턴스에 적용. Mac Editor에서만 Awake 순서가 어긋나 살아남은 Instance가
            // 빈/stale baseUrl 보유 → UnityWebRequest가 default 'localhost' host로 fallback (cf.
            // APIManager.BuildUrl 주석). Windows에서는 우연히 순서가 맞아 발생 안 함.
            // fix: scene에 이미 있는 인스턴스(보통 RuntimeSystems prefab) 우선 사용, 없으면 fallback.
            var apiManager = APIManager.Instance
                ?? UnityEngine.Object.FindFirstObjectByType<APIManager>()
                ?? systems.AddComponent<APIManager>();
            var notebookUiSceneLoader = GetOrAddComponent<NotebookUiSceneLoader>(systems);

            apiManager.Configure(
                apiBaseUrl,
                apiAccessToken,
                apiTimeoutSeconds,
                apiConfig != null && apiConfig.EnableVerboseLog);

            // Server bookshelf path needs a Bearer token. Without one, /v1/spaces/me
            // 401s and the shelf would render empty — fall back to the local catalog
            // path until the user logs in. Computed after Configure() so the token
            // hydrated by Configure (env var / inspector / PlayerPrefs) is reflected.
            // (MR #88 replaced the IsGuestSession gate.)
            var useServerBookshelf = usesHttpApi && apiManager.HasAccessToken;

            var loadBooksUseCase = new LoadMyBookshelfBooksUseCase(CreateBookCatalogRepository(
                apiMode,
                apiBaseUrl,
                apiTimeoutSeconds));
            var notebookBookRepository = CreateNotebookBookRepository(apiMode, apiManager);
            var searchNotebookBooksUseCase = new SearchNotebookBooksUseCase(notebookBookRepository);
            var loadNotebookMarketBooksUseCase = new LoadNotebookMarketBooksUseCase(notebookBookRepository);
            var addNotebookBookToShelfUseCase = new AddNotebookBookToShelfUseCase(notebookBookRepository);
            var notebookRuntimeRepository = CreateNotebookRuntimeRepository(apiMode, apiManager);
            var notebookRuntimeUseCase = new NotebookRuntimeUseCase(notebookRuntimeRepository);
            if (Application.isPlaying && usesHttpApi && !apiManager.HasAccessToken)
            {
                Debug.LogWarning("Dotshelf RealApi mode has no access token. Public notebook APIs stay enabled, but authenticated actions are blocked until APIManager.SetAccessToken() is called.");
            }
            if (Application.isPlaying && usesHttpApi)
            {
                apiManager.CheckHealth(null, apiHealthCheckPath);
            }

            // 메인씬 진입 시 /v1/users/me 호출 — 토큰 보유 상태에서만 실행. 결과는 별도 저장소가
            // 없어서 로그만 남김 (개별 화면이 자기 cachedProfile 들고 있음). status/body 가
            // Console 에 가시화되어 진단/디버그에 도움.
            //
            // MR #98 review #3: production 빌드에서 매 메인씬 진입마다 BE 호출하면 운영 traffic
            // 부담 (사용자 1만명 × 5회/일 = 5만 req/일). UNITY_EDITOR 와 DEVELOPMENT_BUILD 에서만
            // 자동 발동. release player 빌드에서는 컴파일 자체에서 제외되어 호출 안 됨.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Application.isPlaying && usesHttpApi && apiManager.HasAccessToken)
            {
                SyncCurrentUserProfileOnSceneEntry(apiManager);
            }
#endif

            var camerasRoot = FindOrCreateRoot("Cameras").transform;
            var overviewPosition = new Vector3(1.55f, 1.30f, -1.95f);
            var overviewTarget = new Vector3(-0.55f, 0.72f, 0.62f);
            var overviewAnchor = FindOrCreateAnchor(
                camerasRoot,
                "OverviewAnchor",
                overviewPosition,
                Quaternion.LookRotation((overviewTarget - overviewPosition).normalized, Vector3.up));
            var frontalPosition = new Vector3(0f, 0.95f, -2.05f);
            var frontalTarget = new Vector3(0f, 0.85f, 1.20f);
            var frontalAnchor = FindOrCreateAnchor(
                camerasRoot,
                "FrontalAnchor",
                frontalPosition,
                Quaternion.LookRotation((frontalTarget - frontalPosition).normalized, Vector3.up));
            var whiteboardAnchor = FindOrCreateAnchor(
                camerasRoot,
                "WhiteboardAnchor",
                new Vector3(-0.72f, 1.18f, 0.12f),
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
            cameraController.ConfigureViewSettings(55f, 45f, 31f, 24f, 0.7f);
            cameraController.ConfigureFrontalZoomSettings(3.25f, 0.8f, 4.5f, 0.45f, 0.15f);
            cameraController.ConfigureFrontalZoomInputPredicate(experienceManager.CanHandleFrontalZoomInput);
            generator.Configure(state, materialFactory, loadBooksUseCase);
            generator.ConfigureServerBookshelfSnapshots(useServerBookshelf);
            interactionController.Configure(state, generator, cameraController, materialFactory, mainCamera, experienceManager);
            runtimeDataStore.Configure(state, materialFactory);
            experienceManager.Configure(state, cameraController, interactionController, runtimeDataStore, generator);
            notebookUiSceneLoader.Configure(SceneKeys.NotebookUi, unloadSceneOnFocusExit: false, enablePerformanceLog: Application.isPlaying);
            experienceManager.ConfigureNotebookUiSceneLoader(notebookUiSceneLoader);
            experienceManager.ConfigureNotebookBookUseCases(searchNotebookBooksUseCase, loadNotebookMarketBooksUseCase, addNotebookBookToShelfUseCase, notebookRuntimeUseCase);
            if (useServerBookshelf)
            {
                bookshelfApiSync.Configure(new NotebookApiClient(apiManager), generator, experienceManager, cameraController, notebookRuntimeUseCase);
                experienceManager.ConfigureBookshelfApiSync(bookshelfApiSync);
            }
            environment.Configure(probe, null);

            // Main scene opens in the new MainView pose (derived from the bookshelf focus
            // anchor). QuarterView remains accessible via the toggle button and onboarding.
            cameraController.ApplyMainView();

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
                // Main scene opens centered on the bookshelf via the MainView pose.
                // (Previously this transitioned to overviewAnchor / QuarterView, which
                // sits at an angle and made the bookshelf appear left-shifted.)
                cameraController.MoveToMainView();

                // MR #128: 룸/책장/scene-baked decoration 생성이 모두 끝난 시점에 저장된 사용자
                // layout 복원을 트리거. controller 자체의 Start() 에서 호출하면 SceneEntry 가 active
                // scene 을 Main 으로 set 하기 전에 발사돼 InstallScene 보다 먼저 실행되는 순서 버그가
                // 발생 (BookshelfSceneInstaller.Awake 의 active scene early-return 경로 + sceneLoaded
                // → AddComponent → controller.Start 가 RunSceneInstallers 보다 빠른 케이스). 따라서
                // installer 가 install 끝점에서 명시적으로 호출하는 single-source-of-truth 방식
                // (skh1205 리뷰 #1 반영). controller 는 dev 빌드/플레이 모드에서 RuntimeInitializeOnLoad
                // hook 으로 auto-bootstrap 되어 이 시점엔 이미 존재.
                var editController = Object.FindFirstObjectByType<RoomEditModeController>(FindObjectsInactive.Include);
                if (editController != null)
                {
                    editController.LoadSavedDecorationsAfterSceneInstall();
                }
            }
        }

        private static void RemoveLegacyNotebookDashboardCanvases()
        {
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (canvas == null || canvas.name != "DashboardCanvas")
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Object.Destroy(canvas.gameObject);
                }
                else
                {
                    Object.DestroyImmediate(canvas.gameObject);
                }
            }
        }

        private static void ApplyRuntimePerformanceSettings()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Application.isMobilePlatform ? 30 : 45;
            QualitySettings.pixelLightCount = Mathf.Min(QualitySettings.pixelLightCount, 2);
            QualitySettings.shadowDistance = Mathf.Min(QualitySettings.shadowDistance, Application.isMobilePlatform ? 18f : 24f);
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }

        private static IBookCatalogRepository CreateBookCatalogRepository(
            AppApiMode apiMode,
            string apiBaseUrl,
            int requestTimeoutSeconds)
        {
            if (apiMode == AppApiMode.LocalMock)
            {
                return new MockBookCatalogRepository();
            }

            return new ApiBookCatalogRepository(
                apiBaseUrl,
                requestTimeoutSeconds);
        }

        private static INotebookBookRepository CreateNotebookBookRepository(AppApiMode apiMode, APIManager apiManager)
        {
            return apiMode != AppApiMode.LocalMock && apiManager != null
                ? new ApiNotebookBookRepository(new NotebookApiClient(apiManager))
                : new NoOpNotebookBookRepository();
        }

        private static INotebookRuntimeRepository CreateNotebookRuntimeRepository(AppApiMode apiMode, APIManager apiManager)
        {
            return apiMode != AppApiMode.LocalMock && apiManager != null
                ? new ApiNotebookRuntimeRepository(new NotebookApiClient(apiManager), () => apiManager.HasAccessToken)
                : new NoOpNotebookRuntimeRepository();
        }

        private static AppApiMode ResolveApiMode(AppApiConfig apiConfig)
        {
            return apiConfig != null ? apiConfig.ApiMode : AppApiMode.RealApi;
        }

        private static string ResolveApiBaseUrl(AppApiConfig apiConfig)
        {
            var baseUrl = apiConfig != null ? apiConfig.ResolveBaseUrl() : DefaultApiBaseUrl;
            return string.IsNullOrWhiteSpace(baseUrl)
                ? DefaultApiBaseUrl
                : baseUrl.Trim().TrimEnd('/');
        }

        private static int ResolveApiTimeoutSeconds(AppApiConfig apiConfig)
        {
            var timeoutSeconds = apiConfig != null ? apiConfig.RequestTimeoutSeconds : DefaultRequestTimeoutSeconds;
            return Mathf.Max(1, timeoutSeconds);
        }

        private static string ResolveApiAccessToken(AppApiConfig apiConfig)
        {
            return apiConfig != null ? apiConfig.ResolveAccessToken() : null;
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
                return HasExternalNotebookUi();
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

        private static bool HasExternalNotebookUi()
        {
            var controllers = Object.FindObjectsByType<NotebookScreenController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return controllers != null && controllers.Length > 0;
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

        public static void AlignNotebookAnchorToLoadedNotebook()
        {
            var camerasRoot = GameObject.Find("Cameras");
            var notebookAnchor = camerasRoot != null ? camerasRoot.transform.Find("NotebookAnchor") : null;
            AlignNotebookAnchor(notebookAnchor);
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

        // 메인씬 진입 시 BE 의 사용자 프로필을 한 번 동기화한다. 결과는 Console 에 status/body 로
        // 남기고, 후속 동기 처리(개별 화면의 cachedProfile)는 각 컨트롤러가 자체적으로 GetMe 를
        // 다시 호출해 채운다 (Settings.LoadCurrentProfile 참조). 호출 사이트와 동일하게
        // dev/development 빌드에서만 컴파일 (MR #98 review #3).
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void SyncCurrentUserProfileOnSceneEntry(APIManager apiManager)
        {
            string url = ApiEndpointDefaults.DefaultBaseUrl.TrimEnd('/') + "/v1/users/me";
            Debug.Log($"[MainScene] GET /v1/users/me 요청 → {url}");

            NotebookApiClient client = new NotebookApiClient(apiManager);
            client.GetMe(
                onSuccess: profile =>
                {
                    Debug.Log(
                        $"[MainScene] GET /v1/users/me 응답 → STATUS=2xx (onSuccess) " +
                        $"BODY={{id={profile?.Id},nickname='{profile?.Nickname}'," +
                        $"onboarded={profile?.Onboarded},hasIntro={!string.IsNullOrEmpty(profile?.Introduction)}}}");
                },
                onError: error =>
                {
                    Debug.LogWarning(
                        $"[MainScene] GET /v1/users/me 실패 → STATUS={(error != null ? error.StatusCode.ToString() : "?")} " +
                        $"code={error?.Code} message={error?.Message} body={ApiManagerProvider.TrimForLog(error?.Body)}");
                });
        }
#endif
    }
}
