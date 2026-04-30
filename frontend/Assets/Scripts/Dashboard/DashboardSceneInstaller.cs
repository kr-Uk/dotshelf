using App.Application.Dashboard;
using App.Infrastructure.Api;
using App.Infrastructure.Api.Dashboard;
using App.Infrastructure.Mock.Dashboard;
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

        [Tooltip("Api 모드일 때 사용할 APIManager. 비워두면 APIManager.Instance(scene 안 다른 곳에 attach된 것)를 사용한다.")]
        [SerializeField] private APIManager apiManager;

        [Tooltip("Awake 시점에 선택된 Mode를 콘솔에 출력한다.")]
        [SerializeField] private bool logChosenMode = true;

        private void Awake()
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

            if (logChosenMode)
            {
                Debug.Log($"[DashboardSceneInstaller] Mode = {mode}");
            }

            IDashboardRepository dashboardRepo;
            IGuestbookRepository guestbookRepo;
            ISpaceSettingsRepository spaceSettingsRepo;
            IFollowRepository followRepo;
            ILibraryViewRepository libraryRepo;

            if (mode == RepositoryMode.Mock)
            {
                dashboardRepo = new MockDashboardRepository();
                guestbookRepo = new MockGuestbookRepository();
                spaceSettingsRepo = new MockSpaceSettingsRepository();
                followRepo = new MockFollowRepository();
                libraryRepo = new MockLibraryViewRepository();
            }
            else
            {
                APIManager resolvedApiManager = apiManager != null ? apiManager : APIManager.Instance;
                if (resolvedApiManager == null)
                {
                    Debug.LogError("[DashboardSceneInstaller] Api 모드인데 APIManager Instance를 찾을 수 없음. " +
                            "scene에 APIManager GameObject가 있는지 확인하거나 SerializeField로 명시 주입.");
                    return;
                }
                NotebookApiClient client = new NotebookApiClient(resolvedApiManager);
                dashboardRepo = new ApiDashboardRepository(client);
                guestbookRepo = new ApiGuestbookRepository(client);
                spaceSettingsRepo = new ApiSpaceSettingsRepository(client);
                followRepo = new ApiFollowRepository(client);
                libraryRepo = new ApiLibraryViewRepository(client);
            }

            controller.Configure(dashboardRepo, guestbookRepo, spaceSettingsRepo, followRepo, libraryRepo);
        }
    }
}
