using App.Application.Dashboard;
using App.Bootstrap;
using App.Config;
using App.Infrastructure.Api;
using App.Infrastructure.Api.Dashboard;
using UnityEngine;

namespace Dotshelf.Scripts.Dashboard
{
    /// <summary>
    /// 대시보드 화면의 컴포지션 루트.
    /// Inspector에서 Mode를 골라 Mock 또는 Api Repository를 주입한다.
    /// DashboardScreenController.Awake보다 먼저 실행되어야 하므로 ExecutionOrder를 -100으로.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class DashboardSceneInstaller : MonoBehaviour
    {
        public enum RepositoryMode
        {
            Mock,
            Api
        }

        [SerializeField] private DashboardScreenController controller;
        [SerializeField] private RepositoryMode mode = RepositoryMode.Api;
        [SerializeField] private AppApiConfig apiConfig;

        [Tooltip("Api 모드일 때 사용할 APIManager. 비워두면 APIManager.Instance(scene 안 다른 곳에 attach된 것)를 사용한다.")]
        [SerializeField] private APIManager apiManager;

        [Tooltip("Awake 시점에 선택된 Mode를 콘솔에 출력한다.")]
        [SerializeField] private bool logChosenMode = true;

        private void Awake()
        {
            ConfigureController();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ConfigureController();
        }

        private void ConfigureController()
        {
            if (controller == null)
            {
                controller = GetComponent<DashboardScreenController>();
            }
            if (controller == null)
            {
                Debug.LogWarning("[DashboardSceneInstaller] controller가 비어있고 같은 GameObject에서도 찾지 못함. 설치 스킵.");
                return;
            }

            var resolvedApiConfig = ResolveApiConfig();
            var effectiveMode = RepositoryMode.Api;
            if (mode == RepositoryMode.Mock || (resolvedApiConfig != null && resolvedApiConfig.UsesMockApi))
            {
                Debug.LogWarning("[DashboardSceneInstaller] Mock repositories are disabled for this scene. Mock/real runs must use the same API repositories and differ only by base URL.");
            }

            if (logChosenMode)
            {
                Debug.Log($"[DashboardSceneInstaller] Mode = {effectiveMode} (configured={mode}, apiMode={resolvedApiConfig?.ApiMode.ToString() ?? "unset"})");
            }

            IDashboardRepository dashboardRepo;
            IGuestbookRepository guestbookRepo;
            ISpaceSettingsRepository spaceSettingsRepo;
            IFollowRepository followRepo;
            ILibraryViewRepository libraryRepo;

            {
                APIManager resolvedApiManager = apiManager != null ? apiManager : ApiManagerProvider.EnsureInstance();
                if (resolvedApiManager == null)
                {
                    Debug.LogError("[DashboardSceneInstaller] Api mode has no APIManager. Dashboard repositories were not configured.");
                    return;
                }

                if (resolvedApiConfig != null)
                {
                    resolvedApiManager.Configure(
                        resolvedApiConfig.ResolveBaseUrl(),
                        resolvedApiConfig.ResolveAccessToken(),
                        resolvedApiConfig.RequestTimeoutSeconds,
                        resolvedApiConfig.EnableVerboseLog);
                }

                NotebookApiClient client = new NotebookApiClient(resolvedApiManager);
                dashboardRepo = new ApiDashboardRepository(client);
                guestbookRepo = new ApiGuestbookRepository(client);
                spaceSettingsRepo = new ApiSpaceSettingsRepository(client);
                followRepo = new ApiFollowRepository(client);
                libraryRepo = new ApiLibraryViewRepository(client);
            }

            // isMockMode 를 controller 의 useMockRepositories 필드와 동기화한다. prefab 의 직렬화 default(true) 가
            // real-API 주입과 충돌해 OnVisitUser 의 mode 가드가 잘못된 분기로 빠지는 회귀 (#114 후속) 차단.
            controller.Configure(
                dashboardRepo,
                guestbookRepo,
                spaceSettingsRepo,
                followRepo,
                libraryRepo,
                isMockMode: effectiveMode == RepositoryMode.Mock);
        }

        private AppApiConfig ResolveApiConfig()
        {
            if (apiConfig != null)
            {
                return apiConfig;
            }

            var appRoot = FindFirstObjectByType<AppRoot>();
            if (appRoot != null && appRoot.ApiConfig != null)
            {
                return appRoot.ApiConfig;
            }

            var configLoader = FindFirstObjectByType<ConfigLoader>();
            return configLoader != null ? configLoader.ApiConfig : null;
        }
    }
}
