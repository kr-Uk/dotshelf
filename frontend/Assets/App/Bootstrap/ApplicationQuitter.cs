using UnityEngine;

namespace App.Bootstrap
{
    public sealed class ApplicationQuitter : MonoBehaviour
    {
        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            // Player build 에서만 활성화. App.Bootstrap 의 형제 네임스페이스에 App.Application
            // 이 있어 unqualified Application 이 그쪽으로 해석 → CS0234. UnityEngine.Application
            // 으로 fully-qualified 사용 (Editor 에서는 #if 가 가려서 안전).
            UnityEngine.Application.Quit();
#endif
        }
    }
}
