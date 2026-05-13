using UnityEngine;

namespace App.Bootstrap
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class LoginSceneController : MonoBehaviour
    {
        // Onboarding Step2 의 preview stage 가 사용할 MainScene composition config. **Editor 에서 wire 필요** —
        // LoginScene 의 이 GameObject 의 Inspector 에 Assets/App/Composition/Configs/MainSceneCompositionConfig.asset
        // 드래그&드롭. null 이면 OnboardingPreviewStage 가 초기화 실패 + sprite/procedural fallback.
        // 사용자 선택 (RuntimeSystems 제외 visual 자산만 instantiate) 에 따라 RoomBackground / BookshelfRig /
        // NotebookStation / CameraAnchors / DashboardScreenPanel 5개 prefab 만 사용.
        [SerializeField] private App.Config.MainSceneCompositionConfig onboardingPreviewConfig;
        public App.Config.MainSceneCompositionConfig OnboardingPreviewConfig => onboardingPreviewConfig;

        // ===== Onboarding Step2 PreviewCamera 수동 override =====
        // OverridePreviewCamera=true 면 auto-fit (FitCameraToCombinedBounds) 결과를 무시하고 아래 값을
        // 그대로 카메라 transform/fov 에 set.
        //
        // 사용법:
        //   1. Inspector 에서 OverridePreviewCamera 체크
        //   2. PreviewCameraPosition / PreviewCameraEulerAngles / PreviewCameraFov 의 숫자 직접 입력
        //   3. Play 진입 후 Step2 → 우측 preview 에 즉시 반영
        //   4. Play 중에도 Inspector 값 바꾸고 좌측 테마 카드 다시 클릭하면 새 값 즉시 반영 (live tweak)
        //
        // 기준 좌표계: world space. Stage root 가 world origin 에 있고 room 도 정규화되어 floor=0 이라
        // Inspector 의 값이 곧 카메라의 world transform.
        //
        // 디폴트는 frontal quarter view 의 일반적 auto-fit 결과 근사값 — 미세 조정 출발점.
        // **LOCKED**: 사용자 합의 값 (2026-05-13). auto-fit / bounds 기반 reposition 금지. Inspector
        // override 는 향후 미세 조정 path 로 유지. default 값이 LOCKED 와 동일해 override OFF 든 ON 든
        // 카메라 위치 동일.
        // **default OFF** — OverviewAnchor 시점이 우선되도록. 사용자가 미세 조정 필요 시 toggle ON.
        // ON 토글 시 justEnabled sync 로 현재 카메라 (anchor) 값이 Inspector 에 자동 갱신됨.
        [Header("Onboarding PreviewCamera Manual Override")]
        [SerializeField] private bool overridePreviewCamera = false;
        [SerializeField] private Vector3 previewCameraPosition = new Vector3(0f, 1.35f, -4.8f);
        [SerializeField] private Vector3 previewCameraEulerAngles = new Vector3(6f, 0f, 0f);
        [Range(20f, 80f)]
        [SerializeField] private float previewCameraFov = 42f;

        public bool OverridePreviewCamera => overridePreviewCamera;
        public Vector3 PreviewCameraPosition => previewCameraPosition;
        public Vector3 PreviewCameraEulerAngles => previewCameraEulerAngles;
        public float PreviewCameraFov => previewCameraFov;

        // Override toggle ON 직후 호출 — 현재 auto-fit 카메라 transform 을 Inspector 필드에 sync.
        // 사용자가 toggle 한 순간 화면이 갑자기 바뀌는 회귀 (default override 값이 실제 room 위치와
        // 안 맞아 빈 공간만 보임) 를 방지. SerializeField 를 runtime 에 set — Editor Inspector 에 즉시 반영.
        public void SyncPreviewCameraOverrideFromCurrent(Vector3 pos, Vector3 euler, float fov)
        {
            previewCameraPosition = pos;
            previewCameraEulerAngles = euler;
            previewCameraFov = Mathf.Clamp(fov, 20f, 80f);
        }

        private void Awake()
        {
            var onboardingScreenController = GetComponent<OnboardingScreenController>();
            if (onboardingScreenController == null)
            {
                onboardingScreenController = gameObject.AddComponent<OnboardingScreenController>();
            }

            onboardingScreenController.InitializeIfNeeded();

            var loginScreenController = GetComponent<LoginScreenController>();
            if (loginScreenController == null)
            {
                loginScreenController = gameObject.AddComponent<LoginScreenController>();
            }

            loginScreenController.InitializeIfNeeded();

            if (TryGetComponent<LoginUiStyler>(out var legacyStyler))
            {
                legacyStyler.enabled = false;
            }
        }
    }
}
