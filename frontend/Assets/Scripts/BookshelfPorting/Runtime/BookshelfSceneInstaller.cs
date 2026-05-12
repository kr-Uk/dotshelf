using App.Application.Books;
using App.Application.Notebook;
using App.Auth;
using App.Bootstrap;
using App.Config;
using App.Domain;
using App.Infrastructure.Api;
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
            // LocalMock was removed, but keep this installer seam so the
            // manager's local fallback paths can be retired deliberately later.
            var usesHttpApi = true;
            var apiBaseUrl = ResolveApiBaseUrl(apiConfig);
            var apiTimeoutSeconds = ResolveApiTimeoutSeconds(apiConfig);
            var apiAccessToken = ResolveApiAccessToken(apiConfig);
            var apiHealthCheckPath = apiConfig != null ? apiConfig.HealthCheckPath : "/health";
            if (Application.isPlaying)
            {
                Debug.Log($"Dotshelf API mode: {apiMode}, baseUrl={apiBaseUrl}, usesHttpApi={usesHttpApi}");
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

            // [Fix] dev/quick-entry 또는 token 미보유 상태에서 책장이 빈 채로 렌더링되는
            // 회귀 복구. dev 머지 (NO-ISSUE 책장/도서 API 연동) 가 useServerBookshelf 를
            // 항상 true 로 강제했지만, 그러면 BookshelfGenerator 가 useServerBookshelfSnapshots=true
            // 로 동작해 server snapshot 도착 전엔 bookshelfCount=0 → 룸이 책장 없이 표시됨.
            // [DEV] 빠른 입장은 server token 이 없어 RefreshFromServer 가 무한 skip 되므로
            // server snapshot 이 영영 안 옴 → 빈 책장.
            //
            // token 보유 여부로 다시 가드 — token 있으면 server snapshot 기반으로,
            // 없으면 local catalog (InitialBookshelfCount=10) 으로 fallback. 운영에서는
            // 항상 token 보유라 정상 server 흐름 그대로, dev/quick-entry 만 local 로 안전 fallback.
            var useServerBookshelf = usesHttpApi && apiManager.HasAccessToken;

            var loadBooksUseCase = new LoadMyBookshelfBooksUseCase(CreateBookCatalogRepository(
                apiBaseUrl,
                apiTimeoutSeconds));
            var notebookBookRepository = CreateNotebookBookRepository(apiManager);
            var searchNotebookBooksUseCase = new SearchNotebookBooksUseCase(notebookBookRepository);
            var loadNotebookMarketBooksUseCase = new LoadNotebookMarketBooksUseCase(notebookBookRepository);
            var addNotebookBookToShelfUseCase = new AddNotebookBookToShelfUseCase(notebookBookRepository);
            var notebookRuntimeRepository = CreateNotebookRuntimeRepository(apiManager);
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
            experienceManager.ConfigureApiMode(usesHttpApi);
            notebookUiSceneLoader.Configure(SceneKeys.NotebookUi, unloadSceneOnFocusExit: false, enablePerformanceLog: Application.isPlaying);
            experienceManager.ConfigureNotebookUiSceneLoader(notebookUiSceneLoader);
            experienceManager.ConfigureNotebookBookUseCases(searchNotebookBooksUseCase, loadNotebookMarketBooksUseCase, addNotebookBookToShelfUseCase, notebookRuntimeUseCase);
            if (usesHttpApi)
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
            string apiBaseUrl,
            int requestTimeoutSeconds)
        {
            return new ApiBookCatalogRepository(
                apiBaseUrl,
                requestTimeoutSeconds);
        }

        private static INotebookBookRepository CreateNotebookBookRepository(APIManager apiManager)
        {
            return new ApiNotebookBookRepository(new NotebookApiClient(apiManager));
        }

        private static INotebookRuntimeRepository CreateNotebookRuntimeRepository(APIManager apiManager)
        {
            return new ApiNotebookRuntimeRepository(new NotebookApiClient(apiManager), () => apiManager.HasAccessToken);
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

        // BookshelfSceneInstaller.InstallIntoActiveScene 한 사이클에 FindOrCreateRoot 가 system 별 N 회
        // 호출되는데, fallback path (active scene roots + children traversal) 가 발사되면 매 호출마다
        // O(scene_size) 비용이 누적 → mid-large scene 에서 startup latency 영향. 한 사이클 내 같은
        // 이름은 같은 GameObject 를 가리키므로 cache 로 traversal 1 회로 amortize. MR !159 review #2 응답.
        // - Unity null 비교 (`== null`) 가 destroyed object 도 처리하므로 stale reference 안전.
        // - scene reload 시 cache 의 GameObject 가 destroy 되면 다음 lookup 에서 fallback 재발사 → 재충전.
        private static readonly System.Collections.Generic.Dictionary<string, GameObject> rootCache
            = new System.Collections.Generic.Dictionary<string, GameObject>();

        private static GameObject FindOrCreateRoot(string name)
        {
            if (rootCache.TryGetValue(name, out var cached) && cached != null)
            {
                return cached;
            }

            var existing = GameObject.Find(name);
            if (existing != null)
            {
                rootCache[name] = existing;
                return existing;
            }

            // GameObject.Find 는 inactive root / inactive parent 의 자식을 못 찾는다.
            // 신규 가입 path 에서 BookshelfSystems prefab 인스턴스가 onboarding flow 를 거치며
            // inactive 또는 hidden hierarchy 상태로 들어가면, 이 메서드는 못 찾고 동명 빈 GameObject
            // 를 신생시켜 후속 GetOrAddComponent 들이 새 컴포넌트 (SerializeField default 값) 를
            // attach 한다 → BookshelfExperienceManager.externalDashboardPanel / useExternalDashboardPanel
            // 이 prefab override (panel ref / true) 를 잃고 default (null / false) 로 들어가
            // 대시보드 패널이 영영 mount 되지 않는 회귀가 발생했다 (#114 후속).
            //
            // active scene 의 모든 root + 자식을 inactive 포함해 순회하여 기존 prefab 인스턴스를
            // 우선 회복하고, 그래도 없을 때만 신생.
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid())
            {
                var roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    var root = roots[i];
                    if (root == null) continue;
                    if (root.name == name)
                    {
                        rootCache[name] = root;
                        return root;
                    }

                    var transforms = root.GetComponentsInChildren<Transform>(true);
                    for (int j = 0; j < transforms.Length; j++)
                    {
                        if (transforms[j] != null && transforms[j].gameObject.name == name)
                        {
                            var go = transforms[j].gameObject;
                            rootCache[name] = go;
                            return go;
                        }
                    }
                }
            }

            var created = new GameObject(name);
            rootCache[name] = created;
            return created;
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
