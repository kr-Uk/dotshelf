using UnityEngine;

namespace App.Infrastructure.Api
{
    /// <summary>
    /// APIManager 인스턴스 확보용 정적 헬퍼. 정상 흐름(BootstrapScene → MainScene 의 RuntimeSystems
    /// prefab) 이외의 진입점(진단 러너, 단독 실행 씬, 씬 전환 race) 에서 APIManager.Instance 가 null
    /// 일 때 안전하게 인스턴스를 확보한다.
    ///
    /// 동일 fallback 패턴이 BookshelfSceneInstaller.cs:172-174 에서 이미 사용 중. 호출 사이트마다
    /// 같은 코드를 복붙하지 않도록 한 곳으로 모았다.
    /// </summary>
    public static class ApiManagerProvider
    {
        /// <summary>
        /// 3 단계 fallback 으로 APIManager 를 확보한다.
        ///   1) 이미 살아있는 <see cref="APIManager.Instance"/>.
        ///   2) 씬에 부착되었지만 Awake 가 아직 실행되지 않은 인스턴스(<see cref="UnityEngine.Object.FindFirstObjectByType{T}()"/>).
        ///   3) 새 GameObject 에 AddComponent — APIManager.Awake 가 동기 실행되며 Instance 세팅 +
        ///      DontDestroyOnLoad + PlayerPrefs 기반 토큰 hydrate 까지 자동.
        /// </summary>
        public static APIManager EnsureInstance()
        {
            if (APIManager.Instance != null)
            {
                return APIManager.Instance;
            }

            APIManager existing = UnityEngine.Object.FindFirstObjectByType<APIManager>();
            if (existing != null)
            {
                Debug.Log("[ApiManagerProvider] 씬에 APIManager 발견 — Awake 가 곧 Instance 세팅.");
                return existing;
            }

            Debug.LogWarning(
                "[ApiManagerProvider] APIManager 미존재 — fallback GameObject 에 새로 생성. " +
                "Awake 가 Instance/DontDestroyOnLoad/토큰 hydrate 자동 처리.");
            GameObject host = new GameObject("AutoApiManager (fallback)");
            return host.AddComponent<APIManager>();
        }
    }
}
