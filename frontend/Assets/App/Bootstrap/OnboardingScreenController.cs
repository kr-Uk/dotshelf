using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using App.Infrastructure.Api;
using App.Infrastructure.Api.Dtos;
using App.Infrastructure.Catalogs;
using BookshelfPorting.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace App.Bootstrap
{
    [DefaultExecutionOrder(-880)]
    [DisallowMultipleComponent]
    public sealed class OnboardingScreenController : MonoBehaviour
    {
        private const string CanvasName = "OnboardingCanvas";
        private const string PreferredKoreanFontAssetName = "NotoSansKR-Regular";
        private const string FallbackKoreanFontAssetName = "NOTOSANSKR SDF";
        private const float CardWidth = 920f;
        private const float CardHeight = 980f;
        private const float PrimaryControlHeight = 56f;
        private const float ProfilePreviewSize = 176f;
        private const float ThemeButtonWidth = 152f;
        private const int UiLayer = 5;
        private const int CanvasSortingOrder = 5100;
        private const float MainSceneLoadTimeoutSeconds = 8f;
        private const float MainViewReadyTimeoutSeconds = 12f;
        private const float BookshelfFocusTimeoutSeconds = 8f;
        private const float BookshelfFocusRetryIntervalSeconds = 0.75f;
        private const float BookshelfVisualSettleSeconds = 3f;
        private const float MainScenePreviewDelaySeconds = 0.25f;
        private const float MainScenePreviewCoverAlpha = 0.62f;
        private const string OnboardingBookshelfVisibility = "PUBLIC";
        private static readonly bool EnableDebugLogs = false;

        // Theme list / default / sprite mapping all live in
        // OnboardingThemePreviewProvider so adding a theme is a one-file edit.
        private static readonly string[] ThemeNames = OnboardingThemePreviewProvider.OrderedThemeIds;

        private static readonly Color BackgroundColor = Hex("#F4EFE6");
        private static readonly Color CardColor = Hex("#FFFDF9");
        private static readonly Color AccentTint = Hex("#D9C7A7");
        private static readonly Color PrimaryColor = Hex("#2B5C8A");
        private static readonly Color PrimaryPressedColor = Hex("#21496D");
        private static readonly Color PrimaryHighlightColor = Hex("#3A749F");
        private static readonly Color SecondaryColor = Hex("#F8F1E4");
        private static readonly Color BorderColor = Hex("#D8CCBC");
        private static readonly Color TextColor = Hex("#2D2418");
        private static readonly Color MutedTextColor = Hex("#7C6A56");
        private static readonly Color ErrorColor = Hex("#C14E4E");
        private static readonly Color SuccessColor = Hex("#2F8B57");
        private static readonly Color InputColor = Hex("#FDF9F3");
        private static readonly Color PreviewAccentColor = Hex("#EFE6D6");

        [SerializeField] private int currentStep = 1;

        private Canvas onboardingCanvas;
        private TMP_FontAsset fontAsset;
        private TMP_FontAsset fallbackFontAsset;
        private Sprite whiteSprite;
        private Sprite roundedRectSprite;

        private GameObject backgroundRoot;
        private GameObject cardRoot;
        private GameObject step1Panel;
        private GameObject step2Panel;
        private GameObject step3Panel;
        private GameObject completionModalRoot;

        private Image profilePreviewImage;
        private TMP_InputField nicknameInput;
        private TMP_InputField introInput;
        private TMP_Text step1MessageLabel;
        private Button step1NextButton;

        private TMP_Text step2MessageLabel;
        private Image themePreviewImage;
        private GameObject themePreviewPlaceholderShelf;
        private OnboardingThemePreviewProvider themePreviewProvider;
        // Step2 의 panel root Image (BackgroundColor 전체 화면 덮음) 와 우측 RightPreview 영역 GameObject.
        // MainScene preview 모드일 때 (Step2 활성 + MainScene 로드 후) 둘 다 hide 해 MainScene 노출.
        // Step1/3 진입 시 복원.
        private Image step2PanelBackgroundImage;
        private GameObject step2RightPreviewRoot;
        // OnboardingScreenController + canvas + EventSystem 을 scene 전환 시 살아남게 하기 위한 1회 토글.
        private bool persistedAcrossSceneLoad;
        // 사용자 의도 (MainScene 통째 X, prefab composition 만) 기반 preview stage. Step2 진입 시
        // EnsureOnboardingPreviewStage 가 LoginSceneController.OnboardingPreviewConfig 받아 Initialize.
        // 성공 시 RawImage.texture = stage.RT, 실패 시 sprite/procedural fallback.
        private OnboardingPreviewStage onboardingPreviewStage;
        // Step2 onboarding 카메라 ease coroutine. 매 호출마다 stop + restart — 새 카드 클릭 시 카메라
        // 추가 이동이 필요해지면 동일 멤버 재사용.
        private Coroutine cameraTransitionRoutine;
        // Step2 preview camera lock — LateUpdate 가 매 frame 이 target pose 를 Camera.main 에 강제 set.
        // 다른 component (BookshelfExperienceManager / RoomEditMode 등) 가 카메라 덮어써도 우리 가 다음
        // frame 다시 set 해 사용자 시각엔 단일 frame flicker 만 발생. 더 깔끔한 *진짜 lock* 은
        // CameraController 에 OnboardingPreview mode 추가가 필요하지만 그건 follow-up.
        private bool isStep2CameraLockActive;
        // PreviewCamera override 의 직전 적용 값 cache — Update tick 이 매 frame Inspector 값과 비교해
        // 변경 시에만 OverrideCameraTransform 호출 (변경 없는 frame 의 RT 재렌더 비용 회피).
        private bool lastPreviewOverrideEnabled;
        private Vector3 lastPreviewOverridePos;
        private Vector3 lastPreviewOverrideEuler;
        private float lastPreviewOverrideFov;
        private bool hasAppliedPreviewOverrideOnce;
        private Vector3 step2CameraTargetPos;
        private Quaternion step2CameraTargetRot;
        private float step2CameraTargetFov;

        // Mock primitive (Cube/Sphere) room preview 사용 여부.
        //
        // 현재 false — 사용자 보고: 실제 MainScene 과 너무 다른 mock 시각 + 카메라 시점 어색 + wall 확대.
        // mock primitive 는 *실제 인테리어* 가 아니라 시각 fidelity 부적합 결정. sprite preview
        // (OnboardingThemePreviewProvider 의 Resources/Onboarding/Themes/*.png) 를 primary 로 사용.
        // 디자이너가 5개 테마의 실제 메인화면 캡쳐 PNG 를 위 경로에 저장하면 우측 preview 에 그 이미지 표시.
        //
        // 향후 true 로 전환 옵션 — Mock primitive 코드는 retain (debug / fallback 후보 용도). 단 현재
        // build 흐름에선 사용 안 함. MainScene Additive Load 기반 진짜 RT preview 는 별도 PR.
        private const bool EnableMockRoomPreview = false;

        // Live preview (RenderTexture 기반 mini-room) 관련. Initialize 실패 시 themePreviewRenderer
        // 는 null 유지 + sprite preview fallback (themePreviewImage). MR 18X "live preview" 작업.
        private OnboardingThemePreviewRenderer themePreviewRenderer;
        private GameObject themePreviewRawImageRoot;
        private RawImage themePreviewRawImage;
        private CanvasGroup themePreviewRawImageGroup;
        private Coroutine themePreviewFadeRoutine;
        private TMP_Text themePreviewTitleLabel;
        private TMP_Text themePreviewCaptionLabel;
        private Button randomThemeButton;
        private Button step2PreviousButton;
        private Button step2NextButton;
        private TMP_Text step2NextButtonLabel;

        private TMP_Text step3OverlayStatusLabel;
        private GameObject step3LoadingCover;
        private Image step3LoadingCoverImage;
        private GameObject step3LoadingContentRoot;
        private GameObject step3StatusRoot;
        private TMP_Text step3LoadingMessageLabel;
        private Button step3CompleteButton;

        private readonly Dictionary<string, Button> themeButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly Dictionary<string, Image> themeButtonImages = new Dictionary<string, Image>(StringComparer.Ordinal);
        private readonly Dictionary<string, Outline> themeButtonOutlines = new Dictionary<string, Outline>(StringComparer.Ordinal);
        private readonly Dictionary<string, TMP_Text> themeButtonLabels = new Dictionary<string, TMP_Text>(StringComparer.Ordinal);

        private string selectedTheme = string.Empty;
        // 랜덤 버튼 클릭 시 OnboardingRandomThemeBuilder 가 생성한 slot mix. 일반 카드 선택 (단일 테마)
        // 과 분리해 보존 — selectedRandomSelection 가 non-null 이면 ApplySelectedThemeIfAvailable 가
        // 랜덤 경로로 분기. 일반 카드 클릭 시점에 SelectTheme 가 이 필드를 null 로 clear.
        private OnboardingRandomThemeSelection selectedRandomSelection;
        // Step2 진입 시 themeApplier.CurrentTheme 캡쳐 — "이전 단계" / 취소 시 rollback 용. preview API
        // (ApplyExternalThemePreview / ApplyExternalRandomThemePreview) 가 PlayerPrefs 저장 안 하므로
        // 사용자가 여러 카드를 둘러본 뒤 취소해도 in-session visual 만 복원하면 충분.
        private RoomThemeAsset step2PreviewSnapshotTheme;
        private bool step2PreviewSnapshotCaptured;
        // Step2 에서 이미 MainScene 을 로드한 상태이면 Step3 진입 시 재로드 skip — EnterMainAndBookshelfFocusFlow
        // 가 이미 active 면 LoadSceneAsync 건너뛰는 기존 동작 + 추가 가드.
        private bool step2MainSceneAlreadyLoaded;
        private bool nicknameChecked;
        // True between user clicking 중복확인 and the BE response arriving. Used
        // to gate re-entrancy and to reject stale callbacks if the user edits
        // the field mid-flight (the post-edit OnNicknameChanged already resets
        // nicknameChecked, but a late success callback could otherwise overwrite
        // it back to true with the wrong nickname).
        private bool isCheckingNickname;
        // True between user clicking 다음 and the PATCH /v1/users/me response
        // arriving. Drops repeat clicks while in-flight so only one profile
        // save round-trip is ever active. Independent of isCheckingNickname:
        // 다음-button validation runs after 중복확인 has already settled, so
        // they cannot overlap, but each flow needs its own re-entrancy guard.
        private bool isSubmittingProfile;
        private NotebookApiClient nicknameApiClient;
        private NotebookApiClient onboardingApiClient;
        private bool isPreparingOnboardingBookshelf;
        private Texture2D profileTexture;
        private Sprite profileSprite;
        private bool isInitialized;
        private bool hasEnabledCrossScenePersistence;
        private bool isMainViewReady;
        private bool isBookshelfFocusRequested;
        private bool isBookshelfFocused;
        private bool isStep3CompletionReady;
        private float nextStep3CompletionGateRefreshAt;
        private Coroutine step3TransitionRoutine;
        private BookshelfExperienceManager cachedBookshelfExperienceManager;
        private CameraController cachedCameraController;
        private BookshelfGenerator cachedBookshelfGenerator;
        private BookshelfApiSyncController cachedBookshelfApiSyncController;

        private sealed class OnboardingBookshelfProvisioningContext
        {
            public SpaceResponseDto Space;
        }

        public void InitializeIfNeeded()
        {
            if (isInitialized)
            {
                return;
            }

            isInitialized = true;
            whiteSprite = CreateWhiteSprite();
            roundedRectSprite = CreateRoundedRectSprite(64, 14);
            // Provider must exist before BuildOnboardingCanvas → BuildStep2Panel
            // wires the preview area; UpdateThemePreview reads it.
            themePreviewProvider = new OnboardingThemePreviewProvider(roundedRectSprite);
            fontAsset = ResolveFontAsset();
            WarmupOnboardingGlyphs(fontAsset);

            BuildOnboardingCanvas();
            ApplyFontToOnboardingCanvas();
            EnsureEventSystem();
            SetCanvasVisible(false);
            ResetFlowState();
            ShowStep1();
            Canvas.ForceUpdateCanvases();
        }

        private void Awake()
        {
            InitializeIfNeeded();
        }

        // Step2 의 live preview 가 보유한 RT/Camera/mock room 정리. SceneManager 가 controller
        // GameObject 를 destroy 할 때 호출 — Unity 가 자동으로 자식 GameObject 도 정리하지만
        // RenderTexture 는 명시 Release + Destroy 가 필요해 leak 회피.
        private void OnDestroy()
        {
            DisposeThemePreviewRenderer();
            DisposeOnboardingPreviewStage();
        }

        private void Update()
        {
            if (currentStep == 3 && step3Panel != null && step3Panel.activeInHierarchy)
            {
                RefreshStep3CompletionGate(false);
            }

            // Step2 의 preview camera override live tick — Inspector 값 변경 즉시 반영.
            // 조건: Step2 활성 + stage ready + override 체크 ON. 매 frame 호출되지만 변경 감지로 RT 재렌더는 변경 시에만.
            TickPreviewCameraLiveOverride();
        }

        // Inspector 값 변경 감지 + 변경 시 OverrideCameraTransform 호출. Step2 외에는 no-op.
        // 변경 임계: position/euler 의 sqrMagnitude > 1e-6, fov > 0.01 — float 정밀도 안전.
        private void TickPreviewCameraLiveOverride()
        {
            if (currentStep != 2) return;
            if (onboardingPreviewStage == null || !onboardingPreviewStage.IsReady) return;
            var loginCtrl = GetComponent<LoginSceneController>();
            if (loginCtrl == null) return;

            bool nowEnabled = loginCtrl.OverridePreviewCamera;
            if (!nowEnabled)
            {
                lastPreviewOverrideEnabled = false;
                return;
            }

            // toggle ON 직후 — Inspector 의 default 값으로 카메라 덮어쓰면 빈 공간 보일 위험.
            // **현재 auto-fit 결과** 를 Inspector 필드에 sync 해서 화면을 그대로 유지하고, 그 값에서
            // 미세 조정 시작하게 한다. cache 도 같은 값으로 채워서 즉시 재렌더 skip.
            bool justEnabled = !lastPreviewOverrideEnabled;
            if (justEnabled)
            {
                var currentPos = onboardingPreviewStage.CurrentCameraPosition;
                var currentEuler = onboardingPreviewStage.CurrentCameraEulerAngles;
                float currentFov = onboardingPreviewStage.CurrentCameraFov;
                loginCtrl.SyncPreviewCameraOverrideFromCurrent(currentPos, currentEuler, currentFov);
                lastPreviewOverridePos = currentPos;
                lastPreviewOverrideEuler = currentEuler;
                lastPreviewOverrideFov = currentFov;
                lastPreviewOverrideEnabled = true;
                hasAppliedPreviewOverrideOnce = true;
                Debug.Log(
                    $"[OnboardingPreviewStage] Override toggle ON — Inspector synced to current auto-fit " +
                    $"position={currentPos} euler={currentEuler} fov={currentFov}");
                return; // 이 frame 은 재렌더 skip (화면 그대로)
            }

            var nowPos = loginCtrl.PreviewCameraPosition;
            var nowEuler = loginCtrl.PreviewCameraEulerAngles;
            float nowFov = loginCtrl.PreviewCameraFov;

            bool posChanged = (nowPos - lastPreviewOverridePos).sqrMagnitude > 1e-6f;
            bool eulerChanged = (nowEuler - lastPreviewOverrideEuler).sqrMagnitude > 1e-6f;
            bool fovChanged = Mathf.Abs(nowFov - lastPreviewOverrideFov) > 0.01f;

            if (!posChanged && !eulerChanged && !fovChanged)
            {
                return; // 변경 없음 — RT 재렌더 skip
            }

            onboardingPreviewStage.OverrideCameraTransform(nowPos, nowEuler, nowFov);
            lastPreviewOverridePos = nowPos;
            lastPreviewOverrideEuler = nowEuler;
            lastPreviewOverrideFov = nowFov;
        }

        // Step2 camera lock — Step2 활성 동안 매 frame Camera.main 을 step2CameraTarget* 로 강제 set.
        // 다른 component (BookshelfExperienceManager / CameraController.LateUpdate / RoomEditMode 등)
        // 가 카메라를 덮어써도 우리가 다음 frame 다시 set. LateUpdate 는 모든 Update 이후 + 카메라
        // 렌더링 전 호출 → 사용자 시각엔 단일 frame flicker 도 없음.
        //
        // lerp coroutine 이 진행 중이면 (cameraTransitionRoutine != null) skip — lerp 가 target 으로 lerp 중.
        private void LateUpdate()
        {
            if (!isStep2CameraLockActive) return;
            if (currentStep != 2) return;
            if (cameraTransitionRoutine != null) return;
            var cam = Camera.main;
            if (cam == null) return;
            // float 비교는 정밀도 손실 회피 위해 sqrMagnitude. 0.001m 임계.
            if ((cam.transform.position - step2CameraTargetPos).sqrMagnitude > 1e-6f ||
                Quaternion.Angle(cam.transform.rotation, step2CameraTargetRot) > 0.01f ||
                Mathf.Abs(cam.fieldOfView - step2CameraTargetFov) > 0.01f)
            {
                cam.transform.position = step2CameraTargetPos;
                cam.transform.rotation = step2CameraTargetRot;
                cam.fieldOfView = step2CameraTargetFov;
            }
        }

        public void StartOnboarding()
        {
            InitializeIfNeeded();
            ResetFlowState();
            GetComponent<LoginScreenController>()?.SetCanvasVisible(false);
            SetCanvasVisible(true);
            // Step2 navigation was conditional on the guest flow before MR #88;
            // every onboarding session now shows "이전 단계" + "다음 단계". Re-applied
            // on each StartOnboarding call so a re-entered flow resets stale state.
            if (step2PreviousButton != null)
            {
                step2PreviousButton.gameObject.SetActive(true);
            }
            RefreshStep2ThemeProvisioningUi();
            ShowStep1();
        }

        public void ShowStep(int step)
        {
            InitializeIfNeeded();

            currentStep = Mathf.Clamp(step, 1, 3);
            var isStep3Overlay = currentStep == 3;

            SetStepPresentationMode(isStep3Overlay);

            // Step2 는 좌우 split 화면 전체 layout 이라 cardRoot 도 hide. Step1/3 은 그대로 card 안.
            // SetStepPresentationMode 가 step1/2 일 때 cardRoot.SetActive(true) 로 set 한 직후 이 분기에서
            // step2 만 false 로 덮어쓴다.
            if (currentStep == 2 && cardRoot != null)
            {
                cardRoot.SetActive(false);
            }

            if (step1Panel != null)
            {
                step1Panel.SetActive(currentStep == 1);
            }

            if (step2Panel != null)
            {
                step2Panel.SetActive(currentStep == 2);
            }

            if (step3Panel != null)
            {
                step3Panel.SetActive(isStep3Overlay);
            }

            if (currentStep == 1)
            {
                RefreshStep1ValidationUi();
            }

            if (currentStep == 2)
            {
                // Step3 에서 Dispose 됐을 수도 있으니 진입 시 재초기화 (이미 ready 면 no-op).
                EnsureThemePreviewRenderer();
                // Step2 가 처음 표시되는 frame 에 layout 미확정 → preview 영역 RectTransform size 가
                // 0 일 가능성 → RawImage 안 보임 회귀 가드. LayoutRebuilder + ForceUpdateCanvases 로
                // 동기 rebuild 후 preview 갱신.
                if (step2Panel != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(step2Panel.GetComponent<RectTransform>());
                }
                Canvas.ForceUpdateCanvases();
                // Overlay 모드는 **MainScene 이 active 일 때만** — 그 때만 우측에 실제 룸 visible 이라
                // 의미가 있음. MainScene 비활성 상태 (사용자 의도: GoToMain 호출 X) 면 onboarding
                // 의 cream background + sprite preview 흐름 그대로 유지 (검정 화면 회귀 회피).
                bool mainSceneActive = SceneManager.GetActiveScene().name == SceneKeys.Main;
                ApplyStep2OverlayMode(mainSceneActive);
                HideMainSceneUiForStep2(mainSceneActive);
                if (mainSceneActive)
                {
                    // scene 전환 살아남기 — MainScene 가 active 면 다른 scene 이동 case 대비.
                    PersistOnboardingAcrossSceneLoad();
                }
                // Step2 진입 시 themeApplier.CurrentTheme snapshot — "이전 단계" / 취소 시 rollback 용.
                // 첫 진입 1회만 캡쳐 (이미 캡쳐 됐으면 유지). MainScene 이 아직 로드 안 됐을 수 있어 null 일 수 있음.
                CaptureStep2PreviewSnapshotIfNeeded();
                // MainScene 미리 로드 — Step2 우측에 실제 메인화면이 보이도록.
                // (이미 로드돼 있으면 EnterMainAndBookshelfFocusFlow 의 scene 로드 분기에서 자동 skip.)
                BeginStep2MainScenePreloadIfNeeded();
                // **사용자 의도 (최종)**: MainScene 통째 X, prefab composition 만. PreviewStage 초기화 시도.
                // config 가 LoginSceneController inspector 에 wired 됐으면 RawImage 에 stage RT 표시.
                // wired 안 됐으면 sprite/procedural fallback (이전 UpdateThemePreview 흐름).
                EnsureOnboardingPreviewStage();
                // selectedTheme 비어 있으면 UpdateThemePreview 가 DefaultThemeId 로 자동 fallback.
                UpdateThemePreview(selectedTheme);
            }
            else
            {
                // Step1/3 진입 시 overlay 모드 해제 — onboarding background 복원.
                // PreviewStage 도 dispose — Step3 책장 focus / Step1 일반 흐름에 영향 0.
                DisposeOnboardingPreviewStage();
                ApplyStep2OverlayMode(false);
                // Step3 또는 Step1 진입 시 MainScene UI 복원. Step3 의 책장 focus 흐름은 자체적으로
                // canvas state 를 관리하므로 일단 복원 — Step3 가 필요시 다시 hide.
                HideMainSceneUiForStep2(false);
                // Step2 camera lock 해제 — Step3 의 책장 focus 또는 Step1 의 일반 main camera 흐름에
                // 우리 LateUpdate 가 간섭하지 않도록.
                if (isStep2CameraLockActive)
                {
                    isStep2CameraLockActive = false;
                    Debug.Log("[OnboardingStep2Camera] Camera lock released (Step1/3 transition).");
                }
                if (cameraTransitionRoutine != null)
                {
                    StopCoroutine(cameraTransitionRoutine);
                    cameraTransitionRoutine = null;
                }
            }

            if (isStep3Overlay)
            {
                // MainScene 로드 전에 preview 자원 정리 — PreviewLight 가 main scene 의 mesh 들에
                // 영향을 주지 않게 (light cullingMask 가 URP 에서 부분만 동작하는 케이스 가드) +
                // RT/Camera/mock room 모두 destroy 해 memory 회수.
                DisposeThemePreviewRenderer();
                SetExperienceManagerStep3Active(true);
                EnterBookshelfFocusForOnboarding();
                RefreshStep3CompletionGate(true);
            }
            else
            {
                SetExperienceManagerStep3Active(false);
                CancelStep3Transition();
            }

            Canvas.ForceUpdateCanvases();
        }

        public void ShowStep1()
        {
            // Step2 → Step1 복귀 시 in-session preview 가 진행됐다면 themeApplier.CurrentTheme 을
            // snapshot 으로 rollback. PlayerPrefs 는 preview API 가 저장 안 했으므로 다음 세션엔
            // 영향 없음. snapshot 미캡쳐 (MainScene 미로드 / Step2 첫 진입 전) 면 no-op.
            RollbackStep2PreviewSnapshot("ShowStep1");
            ShowStep(1);
        }

        public void ShowStep2()
        {
            ShowStep(2);
        }

        public void ShowStep3()
        {
            ShowStep(3);
        }

        public void CompleteOnboarding()
        {
            LogDebug("Onboarding completed");

            SetExperienceManagerStep3Active(false);
            CancelStep3Transition();
            SetCanvasVisible(false);

            if (onboardingCanvas != null)
            {
                Destroy(onboardingCanvas.gameObject);
                onboardingCanvas = null;
            }

            var loginScreenController = GetComponent<LoginScreenController>();
            if (loginScreenController != null)
            {
                Destroy(loginScreenController);
            }

            Destroy(gameObject);
        }

        private static void LogDebug(string message)
        {
            if (!EnableDebugLogs)
            {
                return;
            }

            Debug.Log(message);
        }

        private void SetCanvasVisible(bool isVisible)
        {
            if (onboardingCanvas == null)
            {
                return;
            }

            onboardingCanvas.gameObject.SetActive(isVisible);
        }

        private void ResetFlowState()
        {
            nicknameChecked = false;
            // Default selection so step 2 entry already shows a real preview
            // instead of the procedural placeholder; user can switch or proceed.
            selectedTheme = OnboardingThemePreviewProvider.DefaultThemeId;
            currentStep = 1;
            isPreparingOnboardingBookshelf = false;
            ResetStep3FlowState();
            isStep3CompletionReady = false;
            nextStep3CompletionGateRefreshAt = 0f;
            RefreshStep3CompletionGate(true);

            if (nicknameInput != null)
            {
                nicknameInput.text = string.Empty;
            }

            if (introInput != null)
            {
                introInput.text = string.Empty;
            }

            SetMessage(step2MessageLabel, string.Empty, ErrorColor);
            SetStep1Feedback(string.Empty, MutedTextColor, false);
            ResetProfilePreview();
            UpdateThemeSelectionVisuals();
            RefreshStep2ThemeProvisioningUi();
            // Reflect the default selection set above — keep in sync with selectedTheme.
            UpdateThemePreview(selectedTheme);
            SetStep3OverlayStatus("메인 화면과 책장 포커스 화면으로 이동할 준비가 되었습니다.");
            SetStepPresentationMode(false);
        }

        private void BuildOnboardingCanvas()
        {
            var existingCanvasObject = FindExistingCanvasObject();
            if (existingCanvasObject != null)
            {
                Destroy(existingCanvasObject);
            }

            var canvasObject = new GameObject(CanvasName, typeof(RectTransform));
            canvasObject.layer = UiLayer;
            SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene);

            onboardingCanvas = canvasObject.AddComponent<Canvas>();
            onboardingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            onboardingCanvas.overrideSorting = true;
            onboardingCanvas.sortingOrder = CanvasSortingOrder;
            onboardingCanvas.pixelPerfect = false;
            onboardingCanvas.targetDisplay = 0;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
            canvasRect.anchoredPosition = Vector2.zero;
            canvasRect.sizeDelta = Vector2.zero;

            var background = CreateImage(canvasObject.transform, "Background", BackgroundColor);
            StretchToParent(background.rectTransform);
            backgroundRoot = background.gameObject;

            CreateAccentBlob(background.transform, "TopAccent", new Vector2(0.84f, 0.88f), new Vector2(280f, 280f));
            CreateAccentBlob(background.transform, "BottomAccent", new Vector2(0.18f, 0.18f), new Vector2(240f, 240f));

            var card = CreateImage(background.transform, "OnboardingCard", CardColor);
            card.sprite = roundedRectSprite;
            card.type = Image.Type.Sliced;
            cardRoot = card.gameObject;

            var cardRect = card.rectTransform;
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(CardWidth, CardHeight);
            cardRect.anchoredPosition = Vector2.zero;

            var cardShadow = card.gameObject.AddComponent<Shadow>();
            cardShadow.effectColor = WithAlpha(TextColor, 0.15f);
            cardShadow.effectDistance = new Vector2(0f, -14f);

            var contentRoot = CreateRectObject("ContentRoot", card.transform);
            StretchWithPadding((RectTransform)contentRoot.transform, new Vector2(56f, 52f), new Vector2(-56f, -52f));

            step1Panel = BuildStep1Panel(contentRoot.transform);
            // Step2 는 좌우 split (좌측 사이드 패널 + 우측 대형 방 미리보기) 로 화면 전체를 사용해
            // OnboardingCard 안 (CardWidth 920) 의 폭 제약을 받지 않는다. canvas direct child 로 두고
            // ShowStep 가 currentStep==2 인 동안에만 cardRoot 를 hide 한다 — Step1/3 은 card 안에 그대로.
            step2Panel = BuildStep2Panel(canvasObject.transform);
            step3Panel = BuildStep3Panel(canvasObject.transform);
            completionModalRoot = BuildCompletionModal(canvasObject.transform);

            // Build 직후 canvas 상태 강제 enforcement — renderMode/parent/transform 회귀 가드.
            EnforceOnboardingCanvasOverlayState("BuildOnboardingCanvas.end");
        }

        // Onboarding Canvas 의 Screen Space Overlay 상태 강제. 외부 (다른 스크립트 / Editor 실수 /
        // DontDestroyOnLoad 부수효과 등) 가 renderMode 를 World Space 로 바꾸거나, parent 를 3D
        // GameObject 아래로 옮기거나, RectTransform scale/position 을 변형해도 여기서 원복.
        //
        // 호출 site:
        //   - BuildOnboardingCanvas 직후 (초기 상태)
        //   - PersistOnboardingAcrossSceneLoad (DontDestroyOnLoad 직후 — DontDestroyOnLoad scene 으로
        //     이동하는 과정에서 transform 변형 가능성)
        //   - ShowStep(2) 진입 직후 (Step2 의 preview stage 가 잘못 attach 되는 회귀 가드)
        private void EnforceOnboardingCanvasOverlayState(string source)
        {
            if (onboardingCanvas == null)
            {
                Debug.LogWarning($"[OnboardingUI] Enforce overlay skip — onboardingCanvas null (source={source}).");
                return;
            }

            // 1. renderMode: 반드시 Screen Space Overlay.
            if (onboardingCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                Debug.LogWarning(
                    $"[OnboardingUI] renderMode={onboardingCanvas.renderMode} 감지 — Screen Space Overlay 로 원복 (source={source}).");
                onboardingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            onboardingCanvas.overrideSorting = true;
            onboardingCanvas.sortingOrder = CanvasSortingOrder;
            // worldCamera 가 누군가 set 했다면 Overlay 에선 무의미 — null 로 비워서 혼선 회피.
            onboardingCanvas.worldCamera = null;

            // 2. parent: null (root). PreviewStage / PreviewRoomRoot / Camera / 3D 오브젝트 아래에
            //    들어가면 안 됨. SetParent(null, true) 로 worldPositionStays=true 유지.
            var canvasGo = onboardingCanvas.gameObject;
            if (canvasGo.transform.parent != null)
            {
                Debug.LogWarning(
                    $"[OnboardingUI] Canvas parent={canvasGo.transform.parent.name} 감지 — root 로 분리 (source={source}).");
                canvasGo.transform.SetParent(null, true);
            }

            // 3. RectTransform 정상화: full screen stretch + scale=1 + rotation=identity + position=0.
            //    Screen Space Overlay 는 RectTransform 의 값이 무관하지만 Scene View 에서 시각적으로
            //    이상하게 보이거나 누군가 World Space 로 잠시 바꾼 흔적이 남으면 회귀 — 명시 normalization.
            var rt = (RectTransform)canvasGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition3D = Vector3.zero;
            rt.sizeDelta = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.localPosition = Vector3.zero;

            Debug.Log($"[OnboardingUI] Canvas renderMode={onboardingCanvas.renderMode} (source={source})");
            Debug.Log(
                $"[OnboardingUI] Canvas parent={(canvasGo.transform.parent != null ? canvasGo.transform.parent.name : "null")} " +
                $"scene={canvasGo.scene.name} sortingOrder={onboardingCanvas.sortingOrder}");
            Debug.Log(
                $"[OnboardingUI] Canvas position={rt.localPosition} rotation={rt.localRotation.eulerAngles} " +
                $"scale={rt.localScale}");
        }

        private GameObject BuildCompletionModal(Transform parent)
        {
            var modalRoot = CreateRectObject("CompletionModalRoot", parent);
            StretchToParent((RectTransform)modalRoot.transform);

            var dim = CreateImage(modalRoot.transform, "Dim", new Color(0f, 0f, 0f, 0.55f));
            StretchToParent(dim.rectTransform);
            // Block clicks behind the modal so only the 확인 button is interactable.
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;

            var card = CreateImage(modalRoot.transform, "Card", CardColor);
            card.sprite = roundedRectSprite;
            card.type = Image.Type.Sliced;
            var cardRect = card.rectTransform;
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(640f, 360f);
            cardRect.anchoredPosition = Vector2.zero;

            var cardShadow = card.gameObject.AddComponent<Shadow>();
            cardShadow.effectColor = WithAlpha(TextColor, 0.18f);
            cardShadow.effectDistance = new Vector2(0f, -10f);

            var title = CreateText(
                card.transform,
                "Title",
                "온보딩이 완료되었습니다",
                26f,
                FontStyles.Bold,
                TextColor,
                TextAlignmentOptions.Center,
                new Vector2(0f, -56f),
                new Vector2(540f, 40f));

            var body = CreateText(
                card.transform,
                "Body",
                "이제 나만의 책장을 자유롭게 꾸며보세요.",
                18f,
                FontStyles.Normal,
                MutedTextColor,
                TextAlignmentOptions.Center,
                new Vector2(0f, -148f),
                new Vector2(540f, 60f));

            var confirmButton = CreateButton(
                card.transform,
                "ConfirmButton",
                "확인",
                PrimaryColor,
                Color.white,
                new Vector2(0f, -284f),
                new Vector2(280f, PrimaryControlHeight),
                false,
                BorderColor);
            confirmButton.onClick.AddListener(OnCompletionConfirmClicked);

            modalRoot.SetActive(false);
            return modalRoot;
        }

        private void ShowCompletionModal()
        {
            if (completionModalRoot == null)
            {
                return;
            }

            if (!HasOnboardingBookshelf())
            {
                RefreshStep3CompletionGate(true);
                return;
            }

            // Push to the front so it visually overlays the Step 3 navigation card.
            completionModalRoot.transform.SetAsLastSibling();
            completionModalRoot.SetActive(true);
        }

        private void HideCompletionModal()
        {
            if (completionModalRoot != null)
            {
                completionModalRoot.SetActive(false);
            }
        }

        private void OnCompletionConfirmClicked()
        {
            HideCompletionModal();

            // Return the main-scene camera to the new MainView default. This must run
            // before CompleteOnboarding's tear-down because the onboarding GameObject —
            // and this controller — get destroyed there. Also clear the Step 3 flag so
            // the main-scene chrome and focus-mode chrome resume their normal visibility.
            if (TryGetBookshelfExperienceManager(out var experienceManager) && experienceManager != null)
            {
                experienceManager.IsOnboardingStep3Active = false;
                experienceManager.FocusMainView();
            }

            CompleteOnboarding();
        }

        private GameObject BuildStep1Panel(Transform parent)
        {
            var panel = CreateRectObject("Step1_ProfilePanel", parent);
            StretchToParent((RectTransform)panel.transform);

            var column = CreateColumn(panel.transform, "Layout", 18f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
            StretchToParent(column);

            CreateSectionText(column, "StepCaption", "STEP 1", 14f, FontStyles.Bold, MutedTextColor, TextAlignmentOptions.Center, 28f, 2f);
            CreateSectionText(column, "Title", "프로필을 설정해주세요", 32f, FontStyles.Bold, TextColor, TextAlignmentOptions.Center, 44f);
            CreateSectionText(column, "Subtitle", "프로필 이미지, 닉네임, 한줄소개를 먼저 입력합니다.", 16f, FontStyles.Normal, MutedTextColor, TextAlignmentOptions.Center, 28f);

            CreateSectionText(column, "ProfileImageLabel", "프로필 이미지", 15f, FontStyles.Bold, MutedTextColor, TextAlignmentOptions.Center, 24f);

            CreateSpacer(column, 12f);

            var profileFrame = CreateImage(column, "ProfileFrame", InputColor);
            profileFrame.sprite = roundedRectSprite;
            profileFrame.type = Image.Type.Sliced;
            profileFrame.raycastTarget = false;

            var profileLayout = profileFrame.gameObject.AddComponent<LayoutElement>();
            profileLayout.preferredWidth = ProfilePreviewSize;
            profileLayout.preferredHeight = ProfilePreviewSize;
            profileLayout.minWidth = ProfilePreviewSize;
            profileLayout.minHeight = ProfilePreviewSize;

            var profileOutline = profileFrame.gameObject.AddComponent<Outline>();
            profileOutline.effectColor = WithAlpha(BorderColor, 0.95f);
            profileOutline.effectDistance = new Vector2(1f, -1f);

            profilePreviewImage = profileFrame;
            profilePreviewImage.preserveAspect = true;

            var photoButtonRow = CreateRow(column, "PhotoButtonRow", 0f, TextAnchor.MiddleCenter);
            var photoButton = CreateFixedButton(photoButtonRow, "AddPhotoButton", "사진 추가", SecondaryColor, TextColor, true, BorderColor, new Vector2(220f, PrimaryControlHeight));
            photoButton.onClick.AddListener(OnAddPhotoButtonClicked);

            CreateSpacer(column, 12f);

            CreateSectionText(column, "NicknameLabel", "닉네임", 15f, FontStyles.Bold, MutedTextColor, TextAlignmentOptions.Left, 24f);

            var nicknameRow = CreateRow(column, "NicknameRow", 14f, TextAnchor.MiddleCenter);
            nicknameInput = CreateFlexibleInput(nicknameRow, "NicknameInputField", "닉네임을 입력하세요", false);
            nicknameInput.onValueChanged.AddListener(OnNicknameChanged);

            var duplicateButton = CreateFixedButton(nicknameRow, "DuplicateCheckButton", "중복확인", PrimaryColor, Color.white, false, BorderColor, new Vector2(180f, PrimaryControlHeight));
            duplicateButton.onClick.AddListener(OnDuplicateCheckButtonClicked);

            step1MessageLabel = CreateSectionText(column, "Step1Message", string.Empty, 15f, FontStyles.Normal, MutedTextColor, TextAlignmentOptions.Left, 28f);

            CreateSectionText(column, "IntroLabel", "한줄소개", 15f, FontStyles.Bold, MutedTextColor, TextAlignmentOptions.Left, 24f);
            introInput = CreateFlexibleInput(column, "IntroInputField", "한줄소개를 입력하세요", false);

            CreateFlexibleSpacer(column);

            var navigationRow = CreateRow(column, "Step1NavigationRow", 14f, TextAnchor.MiddleRight);
            CreateFlexibleSpacer(navigationRow);
            step1NextButton = CreateFixedButton(navigationRow, "NextStepButton", "다음 단계", PrimaryColor, Color.white, false, BorderColor, new Vector2(220f, PrimaryControlHeight));
            step1NextButton.onClick.AddListener(OnStep1NextButtonClicked);
            step1NextButton.gameObject.SetActive(false);

            return panel;
        }

        // Step2 layout — 좌우 split:
        //   좌측 (28%): STEP2 caption / Title / Subtitle / Random 버튼 / 구분선 /
        //              현재 선택 테마 이름·설명 / 5개 세로 테마 카드 / 하단 nav 버튼.
        //   우측 (72%): 큰 방 미리보기 (themePreviewImage). placeholder shelf 는 sprite 없을 때 fallback.
        //
        // CanvasScaler 의 referenceResolution 이 1080×1920 (portrait) 이지만 anchor 비율 기반이라
        // 16:9 횡축에서도 비율 유지. ShowStep 가 currentStep==2 동안 cardRoot 를 hide 해 화면 전체 사용.
        //
        // 기존 변수 재할당 (적용/저장 로직과 호환):
        //   - themePreviewImage / themePreviewPlaceholderShelf — 우측 영역
        //   - themePreviewTitleLabel / themePreviewCaptionLabel — 좌측 "현재 선택 테마" 라벨로 재사용
        //   - themeButtons / themeButtonImages / themeButtonOutlines / themeButtonLabels — 카드 리스트
        //   - randomThemeButton / step2PreviousButton / step2NextButton / step2NextButtonLabel / step2MessageLabel
        private GameObject BuildStep2Panel(Transform parent)
        {
            var panel = CreateRectObject("Step2_ThemePanel", parent);
            StretchToParent((RectTransform)panel.transform);

            // 화면 전체 배경 — cardRoot 가 hide 된 상태라 명시 배경이 필요. BackgroundColor 와 동일 톤.
            // Step2 가 MainScene preview 모드로 진입하면 이 background 도 alpha=0 으로 처리해
            // MainScene 노출. step1/3 진입 시 복원.
            var bg = panel.AddComponent<Image>();
            bg.sprite = whiteSprite;
            bg.type = Image.Type.Simple;
            bg.color = BackgroundColor;
            bg.raycastTarget = true;
            step2PanelBackgroundImage = bg;

            // 호출 순서: 우측 preview 를 먼저 만들고 좌측 panel 을 나중에 만들어 sibling order 상
            // 좌측이 위에 그려지게 한다 (좌측 카드 outline 이 우측 preview 가장자리에 가려지지 않음).
            BuildStep2RightPreview(panel.transform);
            BuildStep2LeftPanel(panel.transform);

            // Live preview renderer — RenderTexture 기반. Initialize 실패 시 themePreviewRenderer 는
            // null 로 유지되고 UpdateThemePreview 가 sprite fallback 으로 자동 회피.
            EnsureThemePreviewRenderer();

            UpdateThemeSelectionVisuals();
            UpdateThemePreview(string.Empty);

            return panel;
        }

        // 좌측 사이드 패널 — 화면 폭의 28% 차지. anchor 기반이라 1080×1920 / 1920×1080 둘 다 비율 유지.
        //
        // 내부 구조 (3 분할):
        //   1) HeaderArea (top anchor, fixed height) — STEP2 caption / Title / Subtitle / Random 버튼 /
        //      구분선 / 선택 테마 라벨. 패널 상단 고정.
        //   2) ThemeListScrollArea (stretch, top=HeaderHeight, bottom=FooterHeight) — 5개 카드 ScrollRect.
        //      카드 합계 height 가 영역을 넘으면 세로 스크롤. 작은 해상도에서도 카드가 footer 를 침범 안 함.
        //   3) BottomActionArea (bottom anchor, fixed height, anchorMin=(0,0) / anchorMax=(1,0)) —
        //      Step2Message + 이전/다음 버튼. 패널 하단에 고정 → 어떤 해상도에서도 화면 안에 보임.
        //
        // 회귀 fix: 이전엔 단일 VerticalLayoutGroup 으로 묶여 있었음. 콘텐츠 합계 height 가 LeftPanel
        // 높이를 초과하면 (1920×1080 / 2560×1440 / Free Aspect 등) 하단 nav 가 화면 밖으로 밀려 안 보임
        // → "이전/다음 단계" 버튼 클릭 불가. anchor 3 분할은 헤더/푸터가 패널 height 와 독립적이라 어느
        // 해상도에서도 footer visible. 가변 영역은 ThemeListScrollArea 한 곳뿐 → ScrollRect 가 흡수.
        private const float Step2LeftHeaderHeight = 460f;
        private const float Step2LeftFooterHeight = 140f;

        private void BuildStep2LeftPanel(Transform parent)
        {
            var leftGo = CreateRectObject("Step2_LeftPanel", parent);
            var leftRt = (RectTransform)leftGo.transform;
            leftRt.anchorMin = new Vector2(0f, 0f);
            leftRt.anchorMax = new Vector2(0.28f, 1f);
            leftRt.pivot = new Vector2(0.5f, 0.5f);
            leftRt.offsetMin = Vector2.zero;
            leftRt.offsetMax = Vector2.zero;
            leftRt.anchoredPosition = Vector2.zero;

            // 좌측 패널 자체는 크림 화이트 — 우측 베이지 배경과 자연스럽게 분리.
            var leftBg = leftGo.AddComponent<Image>();
            leftBg.sprite = whiteSprite;
            leftBg.type = Image.Type.Simple;
            leftBg.color = CardColor;
            leftBg.raycastTarget = true;

            BuildStep2LeftHeader(leftGo.transform);
            BuildStep2LeftThemeList(leftGo.transform);
            BuildStep2LeftFooter(leftGo.transform);
        }

        // HeaderArea — 좌측 패널 상단 고정. anchorMin=(0,1) / anchorMax=(1,1) + pivot=(0.5,1) +
        // sizeDelta.y=Step2LeftHeaderHeight 로 패널 height 변화와 무관하게 상단에 붙어 있음.
        private void BuildStep2LeftHeader(Transform parent)
        {
            var headerGo = CreateRectObject("Step2_LeftHeader", parent);
            var headerRt = (RectTransform)headerGo.transform;
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = Vector2.zero;
            headerRt.sizeDelta = new Vector2(0f, Step2LeftHeaderHeight);

            var headerColumn = CreateColumn(headerGo.transform, "LeftHeaderLayout", 12f, new RectOffset(36, 36, 56, 8), TextAnchor.UpperLeft);
            StretchToParent(headerColumn);
            var headerVlg = headerColumn.GetComponent<VerticalLayoutGroup>();
            if (headerVlg != null)
            {
                headerVlg.childForceExpandWidth = true;
                headerVlg.childControlWidth = true;
                headerVlg.childForceExpandHeight = false;
                headerVlg.childControlHeight = true;
            }

            CreateSectionText(headerColumn, "StepCaption", "STEP 2", 14f, FontStyles.Bold, MutedTextColor, TextAlignmentOptions.Left, 22f, 2f);
            CreateSectionText(headerColumn, "Title", "테마를 선택해주세요", 28f, FontStyles.Bold, TextColor, TextAlignmentOptions.Left, 42f);
            CreateSectionText(headerColumn, "Subtitle", "방과 책장의 분위기를 먼저 정해볼 수 있습니다.", 14f, FontStyles.Normal, MutedTextColor, TextAlignmentOptions.Left, 40f);

            CreateSpacer(headerColumn, 6f);

            randomThemeButton = BuildLeftPanelPrimaryButton(headerColumn, "RandomThemeButton", "🎲   랜덤", SecondaryColor, TextColor, true);
            randomThemeButton.onClick.AddListener(OnRandomThemeButtonClicked);

            CreateSpacer(headerColumn, 14f);

            var divider = CreateImage(headerColumn, "Divider", WithAlpha(BorderColor, 0.55f));
            divider.sprite = whiteSprite;
            divider.type = Image.Type.Simple;
            divider.raycastTarget = false;
            var dividerLayout = divider.gameObject.AddComponent<LayoutElement>();
            dividerLayout.preferredHeight = 1f;
            dividerLayout.minHeight = 1f;
            dividerLayout.flexibleWidth = 1f;

            CreateSpacer(headerColumn, 10f);

            // 현재 선택 테마 이름 / 설명 — UpdateThemePreview 가 두 라벨의 text 를 갱신한다 (기존 필드 그대로 재할당).
            themePreviewTitleLabel = CreateSectionText(headerColumn, "SelectedThemeTitle", "테마 미리보기", 20f, FontStyles.Bold, TextColor, TextAlignmentOptions.Left, 30f);
            themePreviewCaptionLabel = CreateSectionText(headerColumn, "SelectedThemeCaption", "테마를 선택하면 이 영역이 변경됩니다.", 13f, FontStyles.Normal, MutedTextColor, TextAlignmentOptions.Left, 56f);
        }

        // ThemeListScrollArea — Header / Footer 사이의 가변 영역. ScrollRect + RectMask2D Viewport +
        // ContentSizeFitter Content. 카드 5개 합계가 영역 height 보다 작으면 그대로 보이고, 크면 세로 스크롤.
        // anchor stretch (top offset = -HeaderHeight, bottom offset = +FooterHeight) — 패널 height 변화에 자동 적응.
        private void BuildStep2LeftThemeList(Transform parent)
        {
            var scrollGo = CreateRectObject("Step2_ThemeListScroll", parent);
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.pivot = new Vector2(0.5f, 0.5f);
            scrollRt.offsetMin = new Vector2(0f, Step2LeftFooterHeight);
            scrollRt.offsetMax = new Vector2(0f, -Step2LeftHeaderHeight);

            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;
            scrollRect.inertia = false;

            // Viewport — RectMask2D 로 content clip. raycast 받기 위해 거의 투명한 Image 1장.
            var viewportGo = CreateRectObject("Viewport", scrollGo.transform);
            var viewportRt = (RectTransform)viewportGo.transform;
            StretchToParent(viewportRt);
            var viewportImage = viewportGo.AddComponent<Image>();
            viewportImage.sprite = whiteSprite;
            viewportImage.color = new Color(1f, 1f, 1f, 0.0001f);
            viewportImage.raycastTarget = true;
            viewportGo.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportRt;

            // Content — ContentSizeFitter.verticalFit=PreferredSize 로 카드 합계 height 자동 계산.
            var contentGo = CreateRectObject("Content", viewportGo.transform);
            var contentRt = (RectTransform)contentGo.transform;
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;
            scrollRect.content = contentRt;

            var contentVlg = contentGo.AddComponent<VerticalLayoutGroup>();
            contentVlg.spacing = 10f;
            contentVlg.padding = new RectOffset(36, 36, 6, 12);
            contentVlg.childAlignment = TextAnchor.UpperLeft;
            contentVlg.childControlWidth = true;
            contentVlg.childControlHeight = true;
            contentVlg.childForceExpandWidth = true;
            contentVlg.childForceExpandHeight = false;

            var contentFitter = contentGo.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            themeButtons.Clear();
            themeButtonImages.Clear();
            themeButtonOutlines.Clear();
            themeButtonLabels.Clear();

            foreach (var themeName in ThemeNames)
            {
                var button = BuildThemeCard(contentGo.transform, themeName);
                themeButtons[themeName] = button;
            }
        }

        // BottomActionArea — 좌측 패널 하단 고정. anchorMin=(0,0) / anchorMax=(1,0) + pivot=(0.5,0) +
        // sizeDelta.y=Step2LeftFooterHeight. 패널 height 변화와 무관하게 화면 하단에 붙어 있음 → 어떤
        // 해상도에서도 "이전/다음 단계" 버튼이 잘리지 않음.
        private void BuildStep2LeftFooter(Transform parent)
        {
            var footerGo = CreateRectObject("Step2_LeftFooter", parent);
            var footerRt = (RectTransform)footerGo.transform;
            footerRt.anchorMin = new Vector2(0f, 0f);
            footerRt.anchorMax = new Vector2(1f, 0f);
            footerRt.pivot = new Vector2(0.5f, 0f);
            footerRt.anchoredPosition = Vector2.zero;
            footerRt.sizeDelta = new Vector2(0f, Step2LeftFooterHeight);

            // Footer 자체 배경 — ScrollArea 의 카드가 footer 뒤로 스크롤될 때 카드 텍스트가 비치는 회귀 방지.
            // Card 영역 위에 살짝 떠 있는 느낌 부여 (상단 그림자).
            var footerBg = footerGo.AddComponent<Image>();
            footerBg.sprite = whiteSprite;
            footerBg.type = Image.Type.Simple;
            footerBg.color = CardColor;
            footerBg.raycastTarget = true;

            var footerShadow = footerGo.AddComponent<Shadow>();
            footerShadow.effectColor = WithAlpha(TextColor, 0.08f);
            footerShadow.effectDistance = new Vector2(0f, 6f);

            var footerColumn = CreateColumn(footerGo.transform, "LeftFooterLayout", 6f, new RectOffset(36, 36, 14, 28), TextAnchor.UpperLeft);
            StretchToParent(footerColumn);
            var footerVlg = footerColumn.GetComponent<VerticalLayoutGroup>();
            if (footerVlg != null)
            {
                footerVlg.childForceExpandWidth = true;
                footerVlg.childControlWidth = true;
                footerVlg.childForceExpandHeight = false;
                footerVlg.childControlHeight = true;
            }

            step2MessageLabel = CreateSectionText(footerColumn, "Step2Message", string.Empty, 13f, FontStyles.Normal, ErrorColor, TextAlignmentOptions.Left, 22f);

            // 하단 nav — 이전(베이지) / 다음(네이비). 같은 row 에 두고 flex spacer 로 양 끝.
            var navigationRow = CreateRow(footerColumn, "Step2NavigationRow", 10f, TextAnchor.MiddleCenter);
            var navHlg = navigationRow.GetComponent<HorizontalLayoutGroup>();
            if (navHlg != null)
            {
                navHlg.childForceExpandWidth = false;
                navHlg.childControlWidth = false;
                navHlg.childForceExpandHeight = false;
                navHlg.childControlHeight = false;
            }
            step2PreviousButton = CreateFixedButton(navigationRow, "PreviousStepButton", "이전 단계", SecondaryColor, TextColor, true, BorderColor, new Vector2(140f, PrimaryControlHeight));
            step2PreviousButton.onClick.AddListener(ShowStep1);
            CreateFlexibleSpacer(navigationRow);
            step2NextButton = CreateFixedButton(navigationRow, "NextStepButton", "다음 단계", PrimaryColor, Color.white, false, BorderColor, new Vector2(170f, PrimaryControlHeight));
            step2NextButton.onClick.AddListener(OnStep2NextButtonClicked);
            step2NextButtonLabel = step2NextButton.GetComponentInChildren<TMP_Text>(true);
        }

        // 우측 대형 방 미리보기 영역 — 화면 폭의 72% 차지. themePreviewImage 가 영역 전체에 stretch
        // 되고 placeholder shelf 가 그 자식. UpdateThemePreview 가 sprite 가 있을 때 placeholder hide.
        // 향후 실제 MainScene live preview (RenderTexture) 로 교체 시 themePreviewImage 의 sprite
        // 위치만 RawImage 로 swap 하면 되는 구조 — Provider 추상화는 이미 OnboardingThemePreviewProvider 가 보유.
        private void BuildStep2RightPreview(Transform parent)
        {
            var rightGo = CreateRectObject("Step2_RightPreview", parent);
            step2RightPreviewRoot = rightGo;
            var rightRt = (RectTransform)rightGo.transform;
            rightRt.anchorMin = new Vector2(0.28f, 0f);
            rightRt.anchorMax = new Vector2(1f, 1f);
            rightRt.pivot = new Vector2(0.5f, 0.5f);
            // 좌측 패널과의 사이에 약간 inset — preview 가 패널에 딱 붙지 않게.
            rightRt.offsetMin = new Vector2(36f, 48f);
            rightRt.offsetMax = new Vector2(-48f, -48f);
            rightRt.anchoredPosition = Vector2.zero;

            themePreviewImage = rightGo.AddComponent<Image>();
            themePreviewImage.sprite = roundedRectSprite;
            themePreviewImage.type = Image.Type.Sliced;
            themePreviewImage.color = PreviewAccentColor;
            // sprite swap 시 type=Simple 로 바뀌면서 preserveAspect 가 적용 — 인테리어 이미지 찌그러짐 회피.
            themePreviewImage.preserveAspect = true;
            themePreviewImage.raycastTarget = false;

            var previewOutline = rightGo.AddComponent<Outline>();
            previewOutline.effectColor = WithAlpha(BorderColor, 0.55f);
            previewOutline.effectDistance = new Vector2(1f, -1f);

            // 카드 그림자 — 우측 preview 가 살짝 떠 있는 느낌. 너무 강하지 않게.
            var previewShadow = rightGo.AddComponent<Shadow>();
            previewShadow.effectColor = WithAlpha(TextColor, 0.10f);
            previewShadow.effectDistance = new Vector2(0f, -10f);

            // Live preview RawImage — RenderTexture 기반. EnsureThemePreviewRenderer 가 Initialize
            // 성공하면 활성화 (UpdateThemePreview 가 selectedTheme 받았을 때). 실패 시 비활성 + 아래
            // sprite/procedural placeholder 가 fallback. Image (sprite) 와 RawImage (RT) 는 다른
            // GameObject — 같이 두면 RaycastTarget 충돌 가능성, 별도 자식이 안전.
            var rawGo = CreateRectObject("Step2_RightPreview_Raw", rightGo.transform);
            StretchToParent((RectTransform)rawGo.transform);
            themePreviewRawImageRoot = rawGo;
            themePreviewRawImage = rawGo.AddComponent<RawImage>();
            themePreviewRawImage.raycastTarget = false;
            themePreviewRawImage.color = Color.white;
            themePreviewRawImageGroup = rawGo.AddComponent<CanvasGroup>();
            themePreviewRawImageGroup.alpha = 1f;
            // 초기엔 비활성 — EnsureThemePreviewRenderer 성공 + theme 선택될 때만 활성.
            rawGo.SetActive(false);

            // placeholder content — sprite 없을 때 procedural shelf + 책 표시.
            var previewContent = CreateRectObject("PreviewContent", rightGo.transform);
            StretchWithPadding((RectTransform)previewContent.transform, new Vector2(48f, 48f), new Vector2(-48f, -48f));

            var previewColumn = CreateColumn(previewContent.transform, "Layout", 0f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
            StretchToParent(previewColumn);

            var previewShelf = CreateImage(previewColumn, "PreviewShelf", WithAlpha(CardColor, 0.78f));
            previewShelf.sprite = roundedRectSprite;
            previewShelf.type = Image.Type.Sliced;
            previewShelf.raycastTarget = false;
            var previewShelfLayout = previewShelf.gameObject.AddComponent<LayoutElement>();
            previewShelfLayout.preferredHeight = 280f;
            previewShelfLayout.minHeight = 220f;
            previewShelfLayout.flexibleWidth = 1f;
            themePreviewPlaceholderShelf = previewShelf.gameObject;

            CreatePreviewAccentBar(previewShelf.transform, "ShelfTop", new Vector2(0.5f, 0.32f), new Vector2(460f, 12f));
            CreatePreviewAccentBar(previewShelf.transform, "ShelfBottom", new Vector2(0.5f, 0.68f), new Vector2(460f, 12f));
            CreatePreviewBook(previewShelf.transform, "PreviewBookA", new Vector2(0.28f, 0.25f), Hex("#CFA47C"));
            CreatePreviewBook(previewShelf.transform, "PreviewBookB", new Vector2(0.42f, 0.25f), Hex("#7C93B7"));
            CreatePreviewBook(previewShelf.transform, "PreviewBookC", new Vector2(0.56f, 0.25f), Hex("#A87F7F"));
            CreatePreviewBook(previewShelf.transform, "PreviewBookD", new Vector2(0.38f, 0.62f), Hex("#91A27A"));
            CreatePreviewBook(previewShelf.transform, "PreviewBookE", new Vector2(0.52f, 0.62f), Hex("#D0B373"));

            // Sibling order 명시 — themePreviewImage(자체) 위에 RawImage(자식), 그 위에 PreviewContent(자식).
            // Unity UI 는 sibling index 가 큰 자식이 더 위에 그려짐. 자식들 추가 순서는:
            //   1. rawGo(Step2_RightPreview_Raw)  ← sibling 0 (RawImage, 비활성 default)
            //   2. previewContent(PreviewContent) ← sibling 1 (procedural placeholder)
            // 의도된 순서대로 들어가지만 명시 보장 — 디자이너가 인스펙터에서 자식 순서 바꿔도 안전.
            if (themePreviewRawImageRoot != null) themePreviewRawImageRoot.transform.SetSiblingIndex(0);
            previewContent.transform.SetAsLastSibling();

            Debug.Log(
                $"[OnboardingPreviewUI] BuildStep2RightPreview created. " +
                $"image={themePreviewImage != null} rawImage={themePreviewRawImage != null} " +
                $"rawGroup={themePreviewRawImageGroup != null} placeholderShelf={themePreviewPlaceholderShelf != null} " +
                $"rightSize={(themePreviewImage != null ? themePreviewImage.rectTransform.sizeDelta.ToString() : "n/a")}");
        }

        // 가로 카드 — 좌측에 thumbnail (preview sprite 의 작은 버전), 우측에 한글 테마 이름.
        // 클릭 시 기존 SelectTheme(themeName) 호출. 선택 강조는 UpdateThemeSelectionVisuals 가
        // themeButtonImages/Outlines/Labels dict 를 통해 일괄 갱신.
        private Button BuildThemeCard(Transform parent, string themeName)
        {
            var cardGo = CreateRectObject($"{themeName}Card", parent);

            var cardImage = cardGo.AddComponent<Image>();
            cardImage.sprite = roundedRectSprite;
            cardImage.type = Image.Type.Sliced;
            cardImage.color = SecondaryColor;
            cardImage.raycastTarget = true;

            var cardOutline = cardGo.AddComponent<Outline>();
            cardOutline.effectColor = WithAlpha(BorderColor, 0.85f);
            cardOutline.effectDistance = new Vector2(1f, -1f);

            var cardLayout = cardGo.AddComponent<LayoutElement>();
            cardLayout.preferredHeight = 84f;
            cardLayout.minHeight = 84f;
            cardLayout.flexibleWidth = 1f;

            var button = cardGo.AddComponent<Button>();
            button.targetGraphic = cardImage;
            // 선택 색상은 UpdateThemeSelectionVisuals 가 명시 set — Unity Selectable 의 자동 transition 끔.
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => SelectTheme(themeName));

            // HorizontalLayoutGroup — thumbnail 좌측 고정, label 우측 flex.
            var row = cardGo.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(16, 22, 14, 14);
            row.spacing = 16f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childForceExpandWidth = false;
            row.childControlWidth = true;
            row.childForceExpandHeight = false;
            row.childControlHeight = true;

            // Thumbnail — Provider 의 preview sprite 가 있으면 그대로 사용 (작게 표시), 없으면 PreviewAccentColor.
            var thumbGo = CreateRectObject("Thumbnail", cardGo.transform);
            var thumbImage = thumbGo.AddComponent<Image>();
            thumbImage.sprite = roundedRectSprite;
            thumbImage.type = Image.Type.Sliced;
            thumbImage.color = themePreviewProvider != null ? GetThemePreviewColor(themeName) : PreviewAccentColor;
            thumbImage.preserveAspect = true;
            thumbImage.raycastTarget = false;
            if (themePreviewProvider != null && themePreviewProvider.TryGetPreviewSprite(themeName, out var thumbSprite))
            {
                thumbImage.sprite = thumbSprite;
                thumbImage.type = Image.Type.Simple;
                thumbImage.color = Color.white;
            }
            var thumbLayout = thumbGo.AddComponent<LayoutElement>();
            thumbLayout.preferredWidth = 56f;
            thumbLayout.preferredHeight = 56f;
            thumbLayout.minWidth = 56f;
            thumbLayout.minHeight = 56f;
            thumbLayout.flexibleWidth = 0f;
            thumbLayout.flexibleHeight = 0f;

            // Label
            var labelGo = CreateRectObject("Label", cardGo.transform);
            var labelText = labelGo.AddComponent<TextMeshProUGUI>();
            labelText.text = themeName;
            labelText.fontSize = 18f;
            labelText.fontStyle = FontStyles.Bold;
            labelText.color = TextColor;
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            if (fontAsset != null) labelText.font = fontAsset;
            labelText.raycastTarget = false;
            var labelLayout = labelGo.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f;
            labelLayout.preferredHeight = 56f;
            labelLayout.minHeight = 56f;

            // UpdateThemeSelectionVisuals 가 dict 기반으로 갱신 — 기존 키 그대로 등록.
            themeButtonImages[themeName] = cardImage;
            themeButtonOutlines[themeName] = cardOutline;
            themeButtonLabels[themeName] = labelText;

            return button;
        }

        // 좌측 패널의 강조 버튼 (Random 등) — column 내부의 큰 사각 버튼.
        // CreateFixedButton 의 고정 사이즈 (Vector2 size) 가 아니라 LayoutElement 로 column 폭에 맞춰 자동 stretch.
        // 첫 인자는 Transform — CreateColumn 의 반환 타입 (RectTransform) 도 implicit 으로 받음.
        private Button BuildLeftPanelPrimaryButton(Transform parent, string name, string label, Color background, Color textColor, bool withOutline)
        {
            var go = CreateRectObject(name, parent);
            var image = go.AddComponent<Image>();
            image.sprite = roundedRectSprite;
            image.type = Image.Type.Sliced;
            image.color = background;
            image.raycastTarget = true;

            if (withOutline)
            {
                var outline = go.AddComponent<Outline>();
                outline.effectColor = WithAlpha(BorderColor, 0.85f);
                outline.effectDistance = new Vector2(1f, -1f);
            }

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 64f;
            layout.minHeight = 64f;
            layout.flexibleWidth = 1f;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var labelGo = CreateRectObject("Label", go.transform);
            var labelRt = (RectTransform)labelGo.transform;
            StretchToParent(labelRt);
            var labelText = labelGo.AddComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 18f;
            labelText.fontStyle = FontStyles.Bold;
            labelText.color = textColor;
            labelText.alignment = TextAlignmentOptions.Center;
            if (fontAsset != null) labelText.font = fontAsset;
            labelText.raycastTarget = false;

            return button;
        }

        private GameObject BuildStep3Panel(Transform parent)
        {
            var panel = CreateRectObject("Step3_BookshelfFocusPanel", parent);
            StretchToParent((RectTransform)panel.transform);

            var loadingCover = CreateImage(panel.transform, "LoadingCover", BackgroundColor);
            loadingCover.sprite = whiteSprite;
            loadingCover.type = Image.Type.Simple;
            StretchToParent(loadingCover.rectTransform);
            step3LoadingCover = loadingCover.gameObject;
            step3LoadingCoverImage = loadingCover;

            var loadingContent = CreateRectObject("LoadingContent", loadingCover.transform);
            StretchToParent((RectTransform)loadingContent.transform);
            step3LoadingContentRoot = loadingContent;

            var loadingCard = CreateImage(loadingContent.transform, "LoadingCard", WithAlpha(CardColor, 0.96f));
            loadingCard.sprite = roundedRectSprite;
            loadingCard.type = Image.Type.Sliced;
            loadingCard.raycastTarget = false;

            var loadingCardRect = loadingCard.rectTransform;
            loadingCardRect.anchorMin = new Vector2(0.5f, 0.5f);
            loadingCardRect.anchorMax = new Vector2(0.5f, 0.5f);
            loadingCardRect.pivot = new Vector2(0.5f, 0.5f);
            loadingCardRect.sizeDelta = new Vector2(560f, 220f);
            loadingCardRect.anchoredPosition = new Vector2(0f, -12f);

            var loadingCardShadow = loadingCard.gameObject.AddComponent<Shadow>();
            loadingCardShadow.effectColor = WithAlpha(TextColor, 0.12f);
            loadingCardShadow.effectDistance = new Vector2(0f, -10f);

            var loadingColumn = CreateColumn(loadingCard.transform, "Layout", 14f, new RectOffset(28, 28, 28, 28), TextAnchor.MiddleCenter);
            StretchToParent(loadingColumn);

            CreateSectionText(loadingColumn, "LoadingTitle", "메인 화면으로 이동 중입니다.", 28f, FontStyles.Bold, TextColor, TextAlignmentOptions.Center, 40f);
            step3LoadingMessageLabel = CreateSectionText(loadingColumn, "LoadingMessage", "책장 포커스 화면을 준비하고 있습니다.", 17f, FontStyles.Normal, MutedTextColor, TextAlignmentOptions.Center, 56f);

            var loadingTitleLabel = loadingCard.transform.Find("Layout/LoadingTitle")?.GetComponent<TMP_Text>();
            if (loadingTitleLabel != null)
            {
                loadingTitleLabel.text = "책장 준비 중입니다.";
            }

            if (step3LoadingMessageLabel != null)
            {
                step3LoadingMessageLabel.text = "나만의 책장 화면으로 이동하고 있어요.";
            }

            var statusChip = CreateImage(panel.transform, "StatusChip", WithAlpha(CardColor, 0.95f));
            statusChip.sprite = roundedRectSprite;
            statusChip.type = Image.Type.Sliced;
            step3StatusRoot = statusChip.gameObject;

            var statusChipRect = statusChip.rectTransform;
            statusChipRect.anchorMin = new Vector2(0.5f, 1f);
            statusChipRect.anchorMax = new Vector2(0.5f, 1f);
            statusChipRect.pivot = new Vector2(0.5f, 1f);
            statusChipRect.sizeDelta = new Vector2(600f, 82f);
            statusChipRect.anchoredPosition = new Vector2(0f, -36f);

            var statusShadow = statusChip.gameObject.AddComponent<Shadow>();
            statusShadow.effectColor = WithAlpha(TextColor, 0.12f);
            statusShadow.effectDistance = new Vector2(0f, -8f);

            step3OverlayStatusLabel = CreateText(
                statusChip.transform,
                "StatusLabel",
                "메인 화면과 책장 포커스 화면으로 이동할 준비가 되었습니다.",
                20f,
                FontStyles.Bold,
                TextColor,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.zero);
            StretchToParent(step3OverlayStatusLabel.rectTransform);

            // "이전 단계" 버튼 제거: Step 3 는 단방향 흐름 (Step2 → Step3 → 완료). 책장 BE 프로비저닝 +
            // 테마 적용이 이미 끝난 상태라 Step2 로 되돌릴 의미가 없음. Step1/2 의 이전 단계 버튼은 유지.
            //
            // Step 3 전용 CompleteButton — 사용자가 Unity Editor Inspector 에서 시각적으로 확정한 RectTransform
            // 기준값을 코드 default 로 fix. 다른 곳에서 anchor / anchoredPosition / sizeDelta / pivot 을
            // 덮어쓰지 않도록 정리됨 (grep step3CompleteButton 으로 확인 — RefreshStep3CompletionGate 의
            // SetActive 만 호출, 위치 manipulate 없음). Step 3 재진입 시 중복 생성 회피는 BuildStep3Panel
            // 자체가 BuildOnboardingCanvas 1회 호출 흐름이라 보장.
            //
            // 책 추가 모달 (BookshelfExperienceManager) 위에 그려짐 — onboarding canvas sortingOrder=5100
            // > 모달 canvas 기본값. 평소 × 버튼이 있던 자리 — × 는 EnforceOnboardingStep3UiHidden 가
            // onboarding 동안만 hide.
            //
            // === Step 3 CompleteButton Inspector 기준값 ===
            //   AnchorMin = (0.5, 1) / AnchorMax = (0.5, 1) / Pivot = (1, 1)
            //   PosX = 781 / PosY = -134 / Width = 120 / Height = 40
            const float Step3CompleteButtonPosX = 781f;
            const float Step3CompleteButtonPosY = -134f;
            const float Step3CompleteButtonWidth = 120f;
            const float Step3CompleteButtonHeight = 40f;
            step3CompleteButton = CreateFixedButton(panel.transform, "CompleteButton", "완료", PrimaryColor, Color.white, false, BorderColor, new Vector2(Step3CompleteButtonWidth, Step3CompleteButtonHeight));
            var completeRect = (RectTransform)step3CompleteButton.transform;
            completeRect.anchorMin = new Vector2(0.5f, 1f);
            completeRect.anchorMax = new Vector2(0.5f, 1f);
            completeRect.pivot = new Vector2(1f, 1f);
            completeRect.anchoredPosition = new Vector2(Step3CompleteButtonPosX, Step3CompleteButtonPosY);
            step3CompleteButton.onClick.AddListener(ShowCompletionModal);

            SetStep3LoadingPanelVisible(true);
            SetStep3StatusVisible(false);
            SetStep3LoadingCoverVisible(false);
            RefreshStep3CompletionGate(true);
            panel.SetActive(false);
            return panel;
        }

        private void CreatePreviewAccentBar(Transform parent, string objectName, Vector2 anchor, Vector2 size)
        {
            var image = CreateImage(parent, objectName, WithAlpha(BorderColor, 0.95f));
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }

        private void CreatePreviewBook(Transform parent, string objectName, Vector2 anchor, Color color)
        {
            var image = CreateImage(parent, objectName, color);
            image.sprite = roundedRectSprite;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(34f, 84f);
            rect.anchoredPosition = Vector2.zero;
        }

        private void OnAddPhotoButtonClicked()
        {
            if (!TryOpenImageFilePanel(out var selectedPath))
            {
                return;
            }

            if (!TryLoadProfilePreview(selectedPath))
            {
                Debug.LogWarning("[OnboardingDemo] Failed to load selected profile image.");
            }
        }

        private void OnDuplicateCheckButtonClicked()
        {
            // Re-entrancy guard: a previous click is still waiting on BE.
            // Drop the second click silently — the in-flight response will
            // settle the UI shortly.
            if (isCheckingNickname)
            {
                return;
            }

            var nickname = nicknameInput != null ? nicknameInput.text.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(nickname))
            {
                nicknameChecked = false;
                SetStep1Feedback("닉네임을 입력해주세요.", ErrorColor, false);
                return;
            }

#if UNITY_EDITOR
            // Dev mode: BE may not be reachable (no token, BE not deployed,
            // or APIManager.Instance not initialized). Skip the round-trip
            // and force a success — the empty/blank guard above is still
            // honored, so the existing "닉네임을 입력해주세요." feedback path
            // for empty input still works.
            if (DevMainSceneMode.IsActive)
            {
                nicknameChecked = true;
                SetStep1Feedback("사용 가능한 닉네임입니다.", SuccessColor, true);
                return;
            }
#endif

            // Capture the nickname value for the request so a late callback
            // arriving after the user has edited the field can be discarded.
            var requestedNickname = nickname;
            var client = EnsureNicknameApiClient();
            if (client == null)
            {
                // APIManager not ready — leave nicknameChecked=false so the
                // user can retry. EnsureNicknameApiClient already logged.
                SetStep1Feedback("닉네임 확인에 실패했습니다. 다시 시도해주세요.", ErrorColor, false);
                return;
            }

            isCheckingNickname = true;
            nicknameChecked = false;
            SetStep1Feedback("확인 중...", MutedTextColor, false);

            string fullUrl =
                App.Domain.ApiEndpointDefaults.DefaultBaseUrl.TrimEnd('/') +
                "/v1/users/nickname/check?nickname=" + Uri.EscapeDataString(requestedNickname);
            Debug.Log(
                $"[OnboardingDemo] nickname/check 요청 → GET {fullUrl} (nickname='{requestedNickname}')");

            client.CheckNickname(
                requestedNickname,
                onSuccess: response => HandleNicknameCheckSuccess(requestedNickname, response),
                onError: error => HandleNicknameCheckError(requestedNickname, error));
        }

        private void HandleNicknameCheckSuccess(string requestedNickname, NicknameAvailabilityResponseDto response)
        {
            isCheckingNickname = false;
            // Stale-response guard: ignore if the user has since changed the
            // input. OnNicknameChanged has already reset nicknameChecked and
            // displayed the "중복확인을 해주세요" prompt, so leaving it alone is
            // the right behavior.
            if (!IsCurrentNickname(requestedNickname))
            {
                return;
            }

            Debug.Log(
                $"[OnboardingDemo] nickname/check 응답 → STATUS=2xx (onSuccess) " +
                $"BODY={{available={(response != null ? response.Available.ToString() : "null")}}} " +
                $"nickname='{requestedNickname}'");

            if (response != null && response.Available)
            {
                nicknameChecked = true;
                SetStep1Feedback("사용 가능한 닉네임입니다.", SuccessColor, true);
            }
            else
            {
                nicknameChecked = false;
                SetStep1Feedback("이미 사용 중인 닉네임입니다.", ErrorColor, false);
            }
        }

        private void HandleNicknameCheckError(string requestedNickname, ApiErrorResponseDto error)
        {
            isCheckingNickname = false;
            if (!IsCurrentNickname(requestedNickname))
            {
                return;
            }

            nicknameChecked = false;
            SetStep1Feedback("닉네임 확인에 실패했습니다. 다시 시도해주세요.", ErrorColor, false);
            Debug.LogWarning(
                $"[OnboardingDemo] nickname/check 실패 → STATUS={(error != null ? error.StatusCode.ToString() : "?")} " +
                $"code={error?.Code} message={error?.Message} body={ApiManagerProvider.TrimForLog(error?.Body)} " +
                $"nickname='{requestedNickname}'");
        }

        private bool IsCurrentNickname(string requestedNickname)
        {
            var current = nicknameInput != null ? nicknameInput.text.Trim() : string.Empty;
            return string.Equals(current, requestedNickname, StringComparison.Ordinal);
        }

        // APIManager.Instance 가 null 인 진입 케이스(온보딩 단독 씬, 씬 전환 race, 부트스트랩
        // 미경유) 에서도 닉네임 중복 체크가 진행될 수 있도록 ApiManagerProvider 의 fallback
        // 체인을 사용한다. 패턴은 BookshelfSceneInstaller.cs:172-174 와 동일.
        private NotebookApiClient EnsureNicknameApiClient()
        {
            APIManager api = ApiManagerProvider.EnsureInstance();
            if (api == null)
            {
                Debug.LogWarning("[OnboardingDemo] APIManager 확보 실패 — fallback 생성도 실패.");
                return null;
            }
            if (nicknameApiClient == null)
            {
                nicknameApiClient = new NotebookApiClient(api);
            }
            return nicknameApiClient;
        }

        private NotebookApiClient EnsureOnboardingApiClient()
        {
            APIManager api = ApiManagerProvider.EnsureInstance();
            if (api == null)
            {
                Debug.LogWarning("[Onboarding] APIManager 확보 실패 — 온보딩 기본 책장 준비를 시작할 수 없습니다.");
                return null;
            }

            if (onboardingApiClient == null)
            {
                onboardingApiClient = new NotebookApiClient(api);
            }

            return onboardingApiClient;
        }

        private void OnNicknameChanged(string _)
        {
            nicknameChecked = false;
            SetStep1Feedback("닉네임 중복확인을 해주세요.", MutedTextColor, false);
        }

        private void OnStep1NextButtonClicked()
        {
            // Re-entrancy guard: a previous click is still waiting on PATCH.
            // Drop the second click silently — the in-flight response will
            // either advance to Step2 or surface an error message.
            if (isSubmittingProfile)
            {
                return;
            }

            var nickname = nicknameInput != null ? nicknameInput.text.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(nickname))
            {
                SetStep1Feedback("닉네임을 입력해주세요.", ErrorColor, false);
                return;
            }

            if (!nicknameChecked)
            {
                SetStep1Feedback("닉네임 중복확인을 해주세요.", ErrorColor, false);
                return;
            }

#if UNITY_EDITOR
            // Dev mode: skip PATCH /v1/users/me. The empty-nickname and
            // !nicknameChecked guards above still apply, so dev still has
            // to clear the same client-side validation as a real user.
            if (DevMainSceneMode.IsActive)
            {
                SetStep1Feedback(string.Empty, MutedTextColor, true);
                ShowStep2();
                return;
            }
#endif

            // BE @Size(max=255) on introduction; null/blank is allowed. Trim
            // here to drop trailing whitespace from the input field.
            var introduction = introInput != null ? introInput.text.Trim() : string.Empty;

            var request = new UpdateProfileRequestDto
            {
                Nickname = nickname,
                ProfileImageUrl = null,
                Introduction = introduction
            };

            var client = EnsureNicknameApiClient();
            if (client == null)
            {
                SetStep1Feedback("프로필 저장에 실패했습니다. 다시 시도해주세요.", ErrorColor, true);
                return;
            }

            isSubmittingProfile = true;
            SetStep1Feedback("프로필을 저장하는 중입니다...", MutedTextColor, true);

            client.UpdateMe(
                request,
                onSuccess: _ => HandleProfileUpdateSuccess(),
                onError: error => HandleProfileUpdateError(error));
        }

        private void HandleProfileUpdateSuccess()
        {
            isSubmittingProfile = false;
            // Clear the inline status before navigating so a stale "저장하는 중"
            // message cannot bleed through into Step2.
            SetStep1Feedback(string.Empty, MutedTextColor, true);
            ShowStep2();
        }

        private void HandleProfileUpdateError(ApiErrorResponseDto error)
        {
            isSubmittingProfile = false;
            SetStep1Feedback("프로필 저장에 실패했습니다. 다시 시도해주세요.", ErrorColor, true);
            // Don't echo nickname/introduction in logs — keep diagnostic to
            // BE-supplied error metadata only.
            Debug.LogWarning(
                $"[OnboardingDemo] Profile update failed: code={error?.Code} message={error?.Message}");
        }

        private void OnRandomThemeButtonClicked()
        {
            if (isPreparingOnboardingBookshelf) return;

            // ThemeSlot 별 mix 생성. Builder 가 5개 RoomThemeAsset 의 spec 풀에서 슬롯별 1개씩 random pick.
            // null 반환 (전 후보 None / RandomEligible=false) 시 기존 5-pick 단일 테마로 fallback.
            var selection = OnboardingRandomThemeBuilder.BuildRandomSelection();
            if (selection == null || selection.SelectedSpecs.Count == 0)
            {
                Debug.LogWarning("[OnboardingDemo] random slot mix build 실패 — 5-pick 단일 테마 fallback.");
                if (ThemeNames.Length == 0) return;
                var themeName = ThemeNames[UnityEngine.Random.Range(0, ThemeNames.Length)];
                SelectTheme(themeName);
                LogDebug($"[OnboardingDemo] Random fallback (single theme): '{GetThemeDebugName(themeName)}'.");
                return;
            }

            selectedRandomSelection = selection;
            selectedTheme = string.Empty; // 일반 단일 테마 선택 해제 — UI 카드 highlight 도 자동 해제.

            SetMessage(step2MessageLabel, string.Empty, ErrorColor);
            UpdateThemeSelectionVisuals();
            UpdateThemePreviewForRandom(selection);

            // PreviewStage 가 ready 면 stage 에 random preview apply.
            ApplyPreviewToStageIfReady("OnRandomThemeButtonClicked");
            // legacy 호환 — MainScene active 시 random preview API 호출 (보통 onboarding 단계엔 no-op).
            ApplyPreviewInMainSceneIfActive("OnRandomThemeButtonClicked");

            LogDebug($"[OnboardingDemo] Random slot mix selected — {selection.BuildSummary()}");
        }

        // 랜덤 조합 selection 을 우측 preview 에 반영. EnableMockRoomPreview=false (현재) 면
        // mock room 사용 안 하므로 baseTheme 의 sprite preview 로 fallback. baseTheme 의 sprite 가
        // OnboardingThemePreviewProvider 에 등록되어 있으면 그것 표시, 없으면 procedural placeholder.
        //
        // (mock room 활성 시점엔 prefab pool spawn 으로 실제 GLB 표시 — 사용자 보고로 비활성. 디자이너가
        // 사용자 보게 될 결과의 캡쳐 PNG 를 추가하면 sprite preview 가 더 정확. main scene additive
        // load 기반 진짜 RT preview 는 follow-up PR.)
        private void UpdateThemePreviewForRandom(OnboardingRandomThemeSelection selection)
        {
            if (themePreviewImage == null || selection == null) return;

            bool liveOk = false;
            if (themePreviewRenderer != null && themePreviewRenderer.IsReady && themePreviewRawImage != null && selection.BaseTheme != null)
            {
                try
                {
                    // 새 API — prefab spawn + material + light 모두 single source 처리.
                    themePreviewRenderer.ApplyRandomSelection(selection);
                    themePreviewRawImage.texture = themePreviewRenderer.RenderTexture;
                    if (themePreviewRawImageRoot != null) themePreviewRawImageRoot.SetActive(true);
                    // **회귀 fix**: themePreviewImage.gameObject == rightGo 라 SetActive(false) 호출 시 자식
                    // RawImage 까지 hide. component enabled 만 toggle.
                    if (themePreviewImage != null) themePreviewImage.enabled = false;
                    // placeholder shelf 도 hide — RawImage 위에 덮이는 회귀 회피.
                    if (themePreviewPlaceholderShelf != null) themePreviewPlaceholderShelf.SetActive(false);
                    BeginPreviewFade();
                    liveOk = true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning(
                        $"[OnboardingPreview] random ApplyRandomSelection 실패 — placeholder fallback. " +
                        $"{ex.GetType().Name}: {ex.Message}");
                }
            }

            // mock room 사용 안 함 (EnableMockRoomPreview=false) → baseTheme 의 sprite preview 시도.
            // OnboardingThemePreviewProvider 가 baseTheme.name 대신 themeId 매핑이라 base assetName 으로
            // 직접 sprite 못 얻음 — baseTheme 의 displayName / 카테고리 이름에서 themeId 역추적은 복잡.
            // 일단 procedural placeholder 로 통일. 디자이너가 "random combo" 캡쳐 sprite 도 추가 가능.
            if (!liveOk)
            {
                if (themePreviewRawImageRoot != null) themePreviewRawImageRoot.SetActive(false);
                if (themePreviewImage != null) themePreviewImage.enabled = true;
                ApplyPlaceholderPreview(PreviewAccentColor);
                Debug.Log("[OnboardingMainPreview] Apply random preview — sprite/procedural fallback (mock disabled).");
            }

            SetMessage(themePreviewTitleLabel, "🎲 랜덤 조합", TextColor);
            SetMessage(themePreviewCaptionLabel,
                string.IsNullOrEmpty(selection.Description)
                    ? "여러 테마의 가구가 섞인 조합입니다."
                    : selection.Description,
                MutedTextColor);
        }

        private void OnStep2NextButtonClicked()
        {
            if (isPreparingOnboardingBookshelf)
            {
                return;
            }

            // 단일 테마 또는 랜덤 조합 둘 중 하나는 있어야 다음 단계 진입 — Step2 default 가
            // selectedTheme="내추럴" 이라 일반 경로에선 항상 통과지만, 랜덤 클릭 후엔 selectedTheme=""
            // 이고 selectedRandomSelection 만 채워져 있으므로 둘 다 검사.
            bool hasRandom = selectedRandomSelection != null && selectedRandomSelection.SelectedSpecs.Count > 0;
            if (string.IsNullOrWhiteSpace(selectedTheme) && !hasRandom)
            {
                SetMessage(step2MessageLabel, "테마를 선택해주세요.", ErrorColor);
                return;
            }

#if UNITY_EDITOR
            // Dev mode: skip BE 책장 프로비저닝 (GET /v1/spaces/me + POST /v1/bookshelves).
            // OnDevOnboardingTestButtonClicked 진입은 AuthSession.Clear() 후라 access token 이
            // 없어 BE 호출이 401 로 떨어진다 → "내 공간 조회 실패" 토스트. Step1 의 nickname check /
            // 프로필 PATCH 와 동일한 short-circuit 패턴을 Step2 의 책장 프로비저닝에도 적용.
            //
            // 영향: Step3 진입 + 테마 적용 흐름은 그대로 — ApplySelectedThemeIfAvailable 가
            // 호출하는 ApplyExternalThemeSelection / ApplyExternalRandomThemeSelection 은 5개
            // 온보딩 테마 모두 isLocalPreview=true 라 PATCH /spaces/me/theme/.../apply 자동 skip
            // → BE 의존 없이 visual swap + PlayerPrefs persist.
            if (DevMainSceneMode.IsActive)
            {
                SetMessage(step2MessageLabel, string.Empty, ErrorColor);
                ShowStep3();
                return;
            }
#endif

            var client = EnsureOnboardingApiClient();
            if (client == null)
            {
                SetMessage(step2MessageLabel, "첫 책장 준비에 실패했습니다. 다시 시도해주세요.", ErrorColor);
                return;
            }

            var context = new OnboardingBookshelfProvisioningContext();

            isPreparingOnboardingBookshelf = true;
            RefreshStep2ThemeProvisioningUi();
            SetMessage(step2MessageLabel, "기본 책장을 준비 중입니다...", MutedTextColor);

            LoadOnboardingSpace(client, context);
        }

        private void LoadOnboardingSpace(NotebookApiClient client, OnboardingBookshelfProvisioningContext context)
        {
            client.GetMySpace(
                space =>
                {
                    if (space == null || space.Id <= 0L)
                    {
                        HandleOnboardingProvisioningError("내 공간 조회", "내 공간 정보를 찾을 수 없습니다.");
                        return;
                    }

                    context.Space = space;
                    client.GetBookshelves(
                        space.Id,
                        shelves => CreateOrUpdateOnboardingBookshelf(client, context, shelves),
                        error => HandleOnboardingProvisioningError("책장 목록 조회", error));
                },
                error => HandleOnboardingProvisioningError("내 공간 조회", error));
        }

        private void CreateOrUpdateOnboardingBookshelf(
            NotebookApiClient client,
            OnboardingBookshelfProvisioningContext context,
            BookshelfListResponseDto shelves)
        {
            var existingShelf = FindFirstActiveBookshelf(shelves);
            if (existingShelf != null)
            {
                CompleteOnboardingBookshelfProvisioning();
                return;
            }

            var request = new CreateBookshelfRequestDto
            {
                SpaceId = context.Space.Id,
                BodyInventoryId = null,
                Name = BuildOnboardingBookshelfName(),
                Description = "온보딩에서 자동으로 만든 기본 책장입니다.",
                VisibilityScope = OnboardingBookshelfVisibility
            };

            client.CreateBookshelf(
                request,
                created =>
                {
                    if (created == null || created.Id <= 0L)
                    {
                        HandleOnboardingProvisioningError("첫 책장 생성", "서버가 생성된 책장 정보를 반환하지 않았습니다.");
                        return;
                    }

                    CompleteOnboardingBookshelfProvisioning();
                },
                error => HandleOnboardingProvisioningError("첫 책장 생성", error));
        }

        private void CompleteOnboardingBookshelfProvisioning()
        {
            isPreparingOnboardingBookshelf = false;
            RefreshStep2ThemeProvisioningUi();
            SetMessage(step2MessageLabel, string.Empty, ErrorColor);
            ShowStep3();
        }

        private void HandleOnboardingProvisioningError(string operationName, ApiErrorResponseDto error)
        {
            HandleOnboardingProvisioningError(operationName, FormatApiErrorMessage(error, "서버 요청을 완료하지 못했습니다."));
        }

        private void HandleOnboardingProvisioningError(string operationName, string message)
        {
            isPreparingOnboardingBookshelf = false;
            RefreshStep2ThemeProvisioningUi();
            SetMessage(step2MessageLabel, $"{operationName}에 실패했습니다. 다시 시도해주세요.", ErrorColor);
            Debug.LogWarning($"[Onboarding] {operationName} failed: {message}");
        }

        private void RefreshStep2ThemeProvisioningUi()
        {
            if (step2NextButtonLabel != null)
            {
                step2NextButtonLabel.text = isPreparingOnboardingBookshelf ? "준비 중..." : "다음 단계";
            }

            if (step2NextButton != null)
            {
                step2NextButton.interactable = !isPreparingOnboardingBookshelf;
            }

            if (step2PreviousButton != null)
            {
                step2PreviousButton.interactable = !isPreparingOnboardingBookshelf;
            }

            if (randomThemeButton != null)
            {
                randomThemeButton.interactable = !isPreparingOnboardingBookshelf;
            }

            foreach (var entry in themeButtons)
            {
                if (entry.Value != null)
                {
                    entry.Value.interactable = !isPreparingOnboardingBookshelf;
                }
            }
        }

        private void SelectTheme(string themeName)
        {
            if (isPreparingOnboardingBookshelf)
            {
                return;
            }

            selectedTheme = themeName ?? string.Empty;
            // 일반 카드 클릭 = 단일 테마 의도 → 이전 random selection 폐기. ApplySelectedThemeIfAvailable
            // 이 selectedTheme 우선으로 단일 테마 적용 흐름 진입.
            selectedRandomSelection = null;
            SetMessage(step2MessageLabel, string.Empty, ErrorColor);
            UpdateThemeSelectionVisuals();
            UpdateThemePreview(selectedTheme);

            // PreviewStage 가 ready 면 stage 에 material swap (사용자 의도: prefab composition preview).
            ApplyPreviewToStageIfReady($"SelectTheme:{themeName}");
            // (legacy 호환) MainScene 이 이미 active 면 거기에도 preview apply — 두 경로 공존 가능.
            // 보통 onboarding 단계엔 MainScene 비활성이라 no-op.
            ApplyPreviewInMainSceneIfActive($"SelectTheme:{themeName}");

            if (!string.IsNullOrWhiteSpace(selectedTheme))
            {
                LogDebug($"Selected theme: {GetThemeDebugName(selectedTheme)}");
            }
        }

        // 온보딩 Step2 에서 선택한 themeId (한글) 를 실제 RoomThemeAsset 으로 변환한 뒤 MainScene 의
        // RoomEditModeController 에 적용 요청. EnterMainAndBookshelfFocusFlow 코루틴이 MainView
        // 의존성 (BookshelfExperienceManager / CameraController / BookshelfGenerator) ready 직후
        // + 책장 포커스 진입 직전에 호출.
        //
        // 적용 자체는 RoomEditModeController.ApplyExternalThemeSelection 가 single source —
        // themeApplier.Apply + RefreshWallVisibility + RebuildInventorySlots +
        // PersistLastAppliedLocalPreviewTheme + ApplyThemeDefaultEntries 를 한 번에 처리한다.
        // 5개 온보딩 테마 (Natural/Classic/Modern/Vintage/Pastel) 는 모두 isLocalPreview=true 라
        // PATCH `/spaces/me/theme/.../apply` 가 자동 skip → 백엔드 의존 없이 visual swap. 동시에
        // user-scoped PlayerPrefs 에 자산 이름 저장 → 다음 세션 SceneInstall 시점에
        // RestoreLastLocalPreviewThemeOnSceneInstall 가 자동 복원.
        //
        // 실패 모드 (각각 LogWarning + 무해한 skip):
        //   1) selectedTheme 비어있음 — Step2 default(내추럴) 보장이 무너졌을 때만 발생.
        //   2) Resolver 매핑 실패 — OnboardingThemeAssetResolver 가 구체적 LogWarning 출력.
        //   3) scene 에 RoomEditModeController 없음 — MainRoomScene 컴포지션 누락.
        //   4) ApplyExternalThemeSelection 가 false — internal precondition 실패.
        private void ApplySelectedThemeIfAvailable(string source)
        {
            // 우선순위 1 — 랜덤 조합 선택이 있으면 random slot mix 적용. ApplyExternalRandomThemeSelection
            // 가 baseTheme material/lighting + slot 별 spec spawn 을 single source 로 처리.
            if (selectedRandomSelection != null && selectedRandomSelection.SelectedSpecs.Count > 0)
            {
                var controller = UnityEngine.Object.FindFirstObjectByType<RoomEditModeController>(FindObjectsInactive.Include);
                if (controller == null)
                {
                    Debug.LogWarning(
                        $"[Onboarding] RoomEditModeController 가 scene 에 없음 — random selection 적용 skip. " +
                        $"source={source} selection=[{selectedRandomSelection.BuildSummary()}].");
                    return;
                }
                bool randomOk = controller.ApplyExternalRandomThemeSelection(selectedRandomSelection, $"onboarding:{source}");
                if (!randomOk)
                {
                    Debug.LogWarning(
                        $"[Onboarding] ApplyExternalRandomThemeSelection 가 false — random 적용 실패. " +
                        $"source={source} selection=[{selectedRandomSelection.BuildSummary()}].");
                    return;
                }
                Debug.Log(
                    $"[Onboarding] random selection apply OK — slots={selectedRandomSelection.SelectedSpecs.Count} " +
                    $"summary=[{selectedRandomSelection.BuildSummary()}]. source={source}");
                // confirm 후 snapshot reset — 이후 rollback 시도해도 더 이상 적용 안 함.
                step2PreviewSnapshotCaptured = false;
                step2PreviewSnapshotTheme = null;
                Debug.Log("[OnboardingStep2MainPreview] Confirm selection on next (random).");
                return;
            }

            // 우선순위 2 — 일반 단일 테마 흐름 (기존).
            if (string.IsNullOrWhiteSpace(selectedTheme))
            {
                Debug.Log($"[Onboarding] ApplySelectedThemeIfAvailable — selectedTheme/selection 모두 비어있음. source={source}. theme 적용 skip.");
                return;
            }

            var resolved = OnboardingThemeAssetResolver.Resolve(selectedTheme);
            if (resolved == null)
            {
                // Resolver 가 이미 구체적인 LogWarning 출력 — 본 메서드는 추가 noise 없이 skip.
                return;
            }

            var ctrl = UnityEngine.Object.FindFirstObjectByType<RoomEditModeController>(FindObjectsInactive.Include);
            if (ctrl == null)
            {
                Debug.LogWarning(
                    $"[Onboarding] RoomEditModeController 가 scene 에 없음 — onboarding theme 적용 skip. " +
                    $"source={source} themeId='{selectedTheme}' resolved='{resolved.name}'. MainRoomScene 컴포지션 확인 필요.");
                return;
            }

            bool ok = ctrl.ApplyExternalThemeSelection(resolved, $"onboarding:{source}");
            if (!ok)
            {
                Debug.LogWarning(
                    $"[Onboarding] ApplyExternalThemeSelection 가 false — theme 적용 실패. " +
                    $"source={source} themeId='{selectedTheme}' resolved='{resolved.name}'.");
                return;
            }

            Debug.Log(
                $"[Onboarding] theme apply OK — themeId='{selectedTheme}' → asset='{resolved.name}' " +
                $"(displayName='{resolved.DisplayName}'). source={source}");
            // confirm 후 snapshot reset.
            step2PreviewSnapshotCaptured = false;
            step2PreviewSnapshotTheme = null;
            Debug.Log("[OnboardingStep2MainPreview] Confirm selection on next (single theme).");
        }

        private void RefreshStep1ValidationUi()
        {
            if (step1NextButton == null)
            {
                return;
            }

            step1NextButton.gameObject.SetActive(nicknameChecked);
        }

        private void SetStep1Feedback(string message, Color color, bool showNextButton)
        {
            SetMessage(step1MessageLabel, message, color);
            if (step1NextButton != null)
            {
                step1NextButton.gameObject.SetActive(showNextButton);
            }
        }

        private void UpdateThemeSelectionVisuals()
        {
            foreach (var entry in themeButtonImages)
            {
                var themeName = entry.Key;
                var buttonImage = entry.Value;
                if (buttonImage == null)
                {
                    continue;
                }

                var isSelected = string.Equals(selectedTheme, themeName, StringComparison.Ordinal);
                // 새 카드 스타일: 선택은 진한 outline + 옅은 primary tint 로 강조. cardImage 를 통째
                // PrimaryColor 로 set 하면 좌측 thumbnail 이미지가 묻혀 시인성 ↓ — 8% tint + outline 조합.
                buttonImage.color = isSelected ? WithAlpha(PrimaryColor, 0.08f) : SecondaryColor;

                if (themeButtonOutlines.TryGetValue(themeName, out var outline) && outline != null)
                {
                    outline.effectColor = isSelected ? WithAlpha(PrimaryColor, 1f) : WithAlpha(BorderColor, 0.85f);
                    outline.effectDistance = isSelected ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
                }

                if (themeButtonLabels.TryGetValue(themeName, out var label) && label != null)
                {
                    label.color = isSelected ? PrimaryColor : TextColor;
                }
            }
        }

        private Button CreateThemeButton(Transform parent, string themeName)
        {
            var button = CreateFixedButton(parent, $"{themeName}Button", themeName, SecondaryColor, TextColor, true, BorderColor, new Vector2(ThemeButtonWidth, 52f));
            button.onClick.AddListener(() => SelectTheme(themeName));

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                themeButtonImages[themeName] = image;
            }

            if (button.TryGetComponent<Outline>(out var outline))
            {
                themeButtonOutlines[themeName] = outline;
            }

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                themeButtonLabels[themeName] = label;
            }

            return button;
        }

        private void UpdateThemePreview(string themeName)
        {
            if (themePreviewImage == null)
            {
                Debug.LogWarning("[OnboardingPreviewUI] UpdateThemePreview skip — themePreviewImage null (build 시점 race 또는 UI 아직 미초기화).");
                return;
            }

            // **회귀 fix**: PreviewStage 가 ready 면 RawImage 가 stage RT 를 표시 중 — sprite/placeholder
            // 흐름이 RawImage 를 hide 시키면 사용자가 stage prefab 대신 sprite 만 봄. stage 활성 시
            // 라벨만 갱신하고 sprite/placeholder 분기는 skip.
            if (onboardingPreviewStage != null && onboardingPreviewStage.IsReady)
            {
                // 변수명 stageEmptyTheme — 아래 outer scope 의 isEmptyTheme 와 CS0136 (nested scope
                // 동일 이름 충돌) 회피.
                bool stageEmptyTheme = string.IsNullOrWhiteSpace(themeName);
                string stageEffectiveThemeName = stageEmptyTheme ? OnboardingThemePreviewProvider.DefaultThemeId : themeName;
                SetMessage(themePreviewTitleLabel, $"{stageEffectiveThemeName} 테마", TextColor);
                SetMessage(themePreviewCaptionLabel, GetThemePreviewCaption(stageEffectiveThemeName), MutedTextColor);
                Debug.Log($"[OnboardingPreviewUI] UpdateThemePreview theme='{stageEffectiveThemeName}' — PreviewStage active, sprite/placeholder skip (RawImage 가 stage RT 표시 중).");
                return;
            }

            // 빈 themeName 시 procedural placeholder 가 visible 보장 + DefaultThemeId 로 live preview 시도.
            // 사용자 요청: "selectedTheme 이 비어있으면 DefaultThemeId 를 사용해서 preview 를 표시."
            // 우측이 베이지 배경만 보이는 회귀 회피 — 어떤 경우에도 placeholder 또는 live 가 보임.
            bool isEmptyTheme = string.IsNullOrWhiteSpace(themeName);
            if (isEmptyTheme)
            {
                Debug.Log("[OnboardingPreviewUI] UpdateThemePreview empty → fallback DefaultThemeId for live preview attempt; placeholder visible if all paths fail.");
                themeName = OnboardingThemePreviewProvider.DefaultThemeId;
            }

            Debug.Log(
                $"[OnboardingPreviewUI] UpdateThemePreview theme='{themeName}' " +
                $"renderer={(themePreviewRenderer != null ? (themePreviewRenderer.IsReady ? "ready" : "notReady") : "null")} " +
                $"rawImage.active={(themePreviewRawImageRoot != null ? themePreviewRawImageRoot.activeSelf.ToString() : "n/a")} " +
                $"image.active={themePreviewImage.gameObject.activeSelf}");

            // 우선순위 1 — Live preview (RenderTexture 기반 mini-room): themePreviewRenderer 가 ready
            // 이고 OnboardingThemeAssetResolver 로 RoomThemeAsset 해석 성공일 때만. material 만 swap
            // + Camera.Render 강제 호출이라 새 GameObject instantiate 없음 (성능 안전).
            //
            // EnableMockRoomPreview=false (현재 default) 면 themePreviewRenderer 가 null 유지 → 이
            // 분기 자동 skip → sprite preview 가 primary. mock 시각 fidelity 낮음 결정 (사용자 보고).
            bool liveOk = false;
            if (themePreviewRenderer != null && themePreviewRenderer.IsReady && themePreviewRawImage != null)
            {
                var theme = OnboardingThemeAssetResolver.Resolve(themeName);
                if (theme != null)
                {
                    try
                    {
                        themePreviewRenderer.ApplyTheme(theme);
                        // RT validate — mesh 가 실제로 RT 에 그려졌는지 검증. background-only RT 면
                        // RawImage 표시해봤자 단색이라 sprite/procedural fallback 으로 자동 전환.
                        if (!themePreviewRenderer.LastRenderHasMeshContent)
                        {
                            Debug.LogWarning(
                                $"[OnboardingPreviewUI] live preview skip — RT background-only (mesh not rendered). " +
                                $"themeName='{themeName}'. sprite/procedural fallback 으로 전환.");
                            // liveOk = false 유지 → 아래 sprite fallback 분기로.
                        }
                        else
                        {
                            themePreviewRawImage.texture = themePreviewRenderer.RenderTexture;
                            if (themePreviewRawImage.color.a < 0.99f)
                            {
                                var c = themePreviewRawImage.color; c.a = 1f;
                                themePreviewRawImage.color = c;
                            }
                            if (themePreviewRawImageGroup != null && themePreviewRawImageGroup.alpha < 0.05f)
                            {
                                // 직전 fade routine 이 stop 안 된 채 alpha=0 남아 있는 회귀 가드.
                                themePreviewRawImageGroup.alpha = 1f;
                            }
                            if (themePreviewRawImageRoot != null) themePreviewRawImageRoot.SetActive(true);
                            // **회귀 fix**: themePreviewImage.gameObject = rightGo (Step2_RightPreview 자체) 이므로
                            // SetActive(false) 하면 rightGo 전체가 hide → RawImage 자식까지 같이 hide 되어
                            // 사용자가 panel 의 BackgroundColor 만 보는 회귀 발생 (콘솔 진단: RT mesh=9/9 OK,
                            // RawImage activeSelf=true 인데 activeInHierarchy=false 였음).
                            // → RawImage 가 sibling order 상 themePreviewImage 위에 그려지므로 RawImage 가
                            //   active 일 때 자동으로 themePreviewImage 를 덮음. themePreviewImage GameObject
                            //   를 hide 할 필요 없음. Image 컴포넌트 자체 enabled 만 false (raycast / draw 비용 약간 절약).
                            if (themePreviewImage != null) themePreviewImage.enabled = false;
                            // placeholder shelf 도 hide — sibling order 상 RawImage 위에 그려져 cream tint
                            // (CardColor 0.78 alpha) 가 RawImage 를 덮는 회귀 회피.
                            if (themePreviewPlaceholderShelf != null) themePreviewPlaceholderShelf.SetActive(false);
                            BeginPreviewFade();
                            liveOk = true;

                            string previewRtSize = themePreviewRenderer.RenderTexture != null
                                ? themePreviewRenderer.RenderTexture.width + "x" + themePreviewRenderer.RenderTexture.height
                                : "n/a";
                            Debug.Log(
                                $"[OnboardingPreviewUI] RawImage active=true, texture={(themePreviewRawImage.texture != null ? "ok" : "null")}, " +
                                $"alpha={(themePreviewRawImageGroup != null ? themePreviewRawImageGroup.alpha : 1f):F2}, " +
                                $"rtSize={previewRtSize} " +
                                $"rawImage.activeInHierarchy={themePreviewRawImage.isActiveAndEnabled} " +
                                $"image.enabled={themePreviewImage.enabled}");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning(
                            $"[OnboardingPreview] live ApplyTheme 실패 — sprite fallback. themeName='{themeName}' " +
                            $"{ex.GetType().Name}: {ex.Message}");
                    }
                }
            }

            // 우선순위 2 — Sprite fallback: live preview 가 실패했거나 init 안 된 상태. 기존 흐름 그대로.
            if (!liveOk)
            {
                if (themePreviewRawImageRoot != null) themePreviewRawImageRoot.SetActive(false);
                // themePreviewImage.gameObject == rightGo (Step2_RightPreview 자체) — gameObject.SetActive
                // 는 자식 RawImage / PreviewContent 까지 함께 hide 시키므로 사용 금지. component enabled 만 toggle.
                if (themePreviewImage != null) themePreviewImage.enabled = true;
                if (themePreviewProvider != null && themePreviewProvider.TryGetPreviewSprite(themeName, out var sprite))
                {
                    themePreviewImage.sprite = sprite;
                    themePreviewImage.type = Image.Type.Simple;
                    // sprite (실제 메인화면 캡쳐 PNG) 가 portrait/landscape 어떤 비율이든 우측 영역에
                    // letterbox 로 fit — 비율 왜곡 회피. type=Simple 일 때만 preserveAspect 가 동작.
                    themePreviewImage.preserveAspect = true;
                    themePreviewImage.color = Color.white;
                    if (themePreviewPlaceholderShelf != null)
                    {
                        themePreviewPlaceholderShelf.SetActive(false);
                    }
                    Debug.Log($"[OnboardingMainPreview] Apply theme preview theme='{themeName}' mode=Sprite sprite='{sprite.name}'");
                }
                else
                {
                    // 우선순위 3 — Procedural placeholder (per-theme tint). sprite 도 없을 때.
                    // 디자이너가 Resources/Onboarding/Themes/theme_natural.png 등 PNG 를 추가하면
                    // 위 분기로 자동 진입 → 실제 메인화면 미리보기.
                    ApplyPlaceholderPreview(GetThemePreviewColor(themeName));
                    Debug.LogWarning(
                        $"[OnboardingMainPreview] Apply theme preview theme='{themeName}' mode=ProceduralPlaceholder " +
                        "— 디자이너가 Resources/Onboarding/Themes/<themeId>.png 미캡쳐. PNG 추가 시 자동 메인화면 sprite preview 로 전환.");
                }
            }

            SetMessage(themePreviewTitleLabel, $"{themeName} 테마", TextColor);
            SetMessage(themePreviewCaptionLabel, GetThemePreviewCaption(themeName), MutedTextColor);
        }

        // BuildStep2Panel 끝점 + ShowStep(2) 진입 시 호출. 이미 ready 면 no-op. Initialize 실패 시
        // themePreviewRenderer 는 null 유지 + UpdateThemePreview 가 sprite fallback 으로 자동 회피.
        //
        // EnableMockRoomPreview=false (현재 default) — mock primitive 폐기 결정. themePreviewRenderer
        // 를 만들지 않고 RawImage 도 hide. UpdateThemePreview 가 sprite/procedural 분기로 자연 진입.
        private void EnsureThemePreviewRenderer()
        {
            if (!EnableMockRoomPreview)
            {
                // mock primitive 사용 안 함 — sprite preview 가 primary. RawImage 도 hide.
                if (themePreviewRawImageRoot != null) themePreviewRawImageRoot.SetActive(false);
                if (themePreviewImage != null) themePreviewImage.enabled = true;
                Debug.Log("[OnboardingMainPreview] Initialize start — mode=SpriteOnly (mock room disabled).");
                return;
            }

            if (themePreviewRenderer != null && themePreviewRenderer.IsReady)
            {
                Debug.Log("[OnboardingPreviewUI] Ensure renderer result: ready=true (already initialized)");
                return;
            }
            if (themePreviewRawImage == null)
            {
                Debug.LogWarning("[OnboardingPreviewUI] Ensure renderer skip — themePreviewRawImage null. BuildStep2RightPreview 호출 전 시점.");
                return;
            }

            themePreviewRenderer = new OnboardingThemePreviewRenderer();
            bool ok = themePreviewRenderer.Initialize();
            if (!ok)
            {
                // OnboardingThemePreviewRenderer.Initialize 내부에서 Dispose 까지 처리. 호출자는 null 로 둠.
                themePreviewRenderer = null;
                Debug.LogWarning("[OnboardingPreviewUI] Ensure renderer result: ready=false (Initialize failed) — sprite fallback only.");
                return;
            }
            // RawImage texture 는 ApplyTheme 가 매 호출 reassign 하지만 첫 활성 frame 에도 valid 하게.
            themePreviewRawImage.texture = themePreviewRenderer.RenderTexture;
            string rtSizeStr = themePreviewRenderer.RenderTexture != null
                ? themePreviewRenderer.RenderTexture.width + "x" + themePreviewRenderer.RenderTexture.height
                : "n/a";
            Debug.Log(
                $"[OnboardingPreviewUI] Ensure renderer result: ready=true rt={(themePreviewRenderer.RenderTexture != null ? "ok" : "null")} " +
                $"rtSize={rtSizeStr}");
        }

        // Step2 진입 시 stage 초기화 시도. LoginSceneController.OnboardingPreviewConfig 가 Inspector 에
        // wired 되어 있어야 동작. null 이면 sprite/procedural fallback (이전 흐름 그대로).
        //
        // 호출 site: ShowStep(2) 의 step==2 분기 안에서 1회.
        //
        // 사용자가 "테마 선택 시 sprite 만 보임" 보고 = 이 메서드 어딘가에서 early return → sprite
        // fallback 으로 빠짐. 콘솔의 [OnboardingPreviewStage] prefix 로그가 정확한 원인 알려줌.
        private void EnsureOnboardingPreviewStage()
        {
            Debug.Log("[OnboardingPreviewStage] EnsureOnboardingPreviewStage entry");
            // Preview stage 생성 직전에 canvas overlay 상태 한 번 더 enforcement — preview stage 의
            // 3D root 가 canvas 와 sibling 관계를 유지하도록 가드 (canvas 가 어쩌다 preview stage 아래에
            // 들어가는 회귀 방지).
            EnforceOnboardingCanvasOverlayState("EnsureOnboardingPreviewStage.entry");
            if (onboardingPreviewStage != null && onboardingPreviewStage.IsReady)
            {
                // 이미 ready — RawImage 에 texture 재할당 (혹시 dispose/reinit 사이에 stale 됐을 때 가드).
                if (themePreviewRawImage != null)
                {
                    themePreviewRawImage.texture = onboardingPreviewStage.RenderTexture;
                    if (themePreviewRawImageRoot != null) themePreviewRawImageRoot.SetActive(true);
                    if (themePreviewImage != null) themePreviewImage.enabled = false;
                    if (themePreviewPlaceholderShelf != null) themePreviewPlaceholderShelf.SetActive(false);
                }
                return;
            }

            var loginCtrl = GetComponent<LoginSceneController>();
            if (loginCtrl == null)
            {
                Debug.LogWarning(
                    "[OnboardingPreviewStage] LoginSceneController not found on same GameObject — " +
                    "OnboardingScreenController 가 LoginSceneController 와 같은 GameObject 에 있어야 함. " +
                    "지금은 sprite fallback.");
                return;
            }
            var config = loginCtrl.OnboardingPreviewConfig;
            if (config == null)
            {
                Debug.LogWarning(
                    "[OnboardingPreviewStage] config not wired — Editor 작업 필요:\n" +
                    "  1. Unity Editor 에서 LoginScene 열기\n" +
                    "  2. Hierarchy 에서 LoginSceneController 가 부착된 GameObject 선택\n" +
                    "  3. Inspector 의 'Onboarding Preview Config' 슬롯에 Assets/App/Composition/Configs/MainSceneCompositionConfig.asset 드래그&드롭\n" +
                    "  4. Scene 저장\n" +
                    "지금은 sprite/procedural fallback 으로 표시됩니다 (이게 사용자가 보고 있는 화면).");
                return;
            }

            // config 의 5 prefab null 검사 — Editor 의 SaveCompositionConfig 가 일부만 wire 한 경우 진단.
            int wiredCount = 0;
            var wiredSummary = new System.Text.StringBuilder("[OnboardingPreviewStage] config wired prefabs:");
            wiredSummary.Append(" RoomBackground=").Append(config.RoomBackgroundPrefab != null ? "OK" : "null");
            if (config.RoomBackgroundPrefab != null) wiredCount++;
            wiredSummary.Append(" BookshelfRig=").Append(config.BookshelfRigPrefab != null ? "OK" : "null");
            if (config.BookshelfRigPrefab != null) wiredCount++;
            wiredSummary.Append(" NotebookStation=").Append(config.NotebookStationPrefab != null ? "OK" : "null");
            if (config.NotebookStationPrefab != null) wiredCount++;
            wiredSummary.Append(" CameraAnchors=").Append(config.CameraAnchorsPrefab != null ? "OK" : "null");
            if (config.CameraAnchorsPrefab != null) wiredCount++;
            wiredSummary.Append(" DashboardScreenPanel=").Append(config.DashboardScreenPanelPrefab != null ? "OK" : "null");
            if (config.DashboardScreenPanelPrefab != null) wiredCount++;
            Debug.Log(wiredSummary.ToString());
            if (wiredCount == 0)
            {
                Debug.LogWarning(
                    "[OnboardingPreviewStage] config 는 wire 됐지만 5 prefab 모두 null. " +
                    "MainSceneCompositionConfig.asset 의 inspector 에서 각 prefab 슬롯에 자산 wire 필요. " +
                    "Editor → MainScene 열기 → 메뉴 'Dotshelf > Extract MainScene Prefabs' (또는 비슷) 실행 필요할 수 있음. sprite fallback.");
                return;
            }

            onboardingPreviewStage = new OnboardingPreviewStage();
            bool ok = onboardingPreviewStage.Initialize(config);
            if (!ok)
            {
                onboardingPreviewStage = null;
                return;
            }

            // Inspector 의 수동 override (LoginSceneController.OverridePreviewCamera) 가 true 면 auto-fit
            // 결과 무시하고 Inspector 값 그대로 적용.
            if (loginCtrl.OverridePreviewCamera)
            {
                onboardingPreviewStage.OverrideCameraTransform(
                    loginCtrl.PreviewCameraPosition,
                    loginCtrl.PreviewCameraEulerAngles,
                    loginCtrl.PreviewCameraFov);
            }

            // Stage RT 를 우측 RawImage 에 연결. sprite/placeholder 흐름 비활성.
            if (themePreviewRawImage != null && themePreviewRawImageRoot != null)
            {
                themePreviewRawImage.texture = onboardingPreviewStage.RenderTexture;
                themePreviewRawImage.color = Color.white;
                if (themePreviewRawImageGroup != null) themePreviewRawImageGroup.alpha = 1f;
                themePreviewRawImageRoot.SetActive(true);
            }
            if (themePreviewImage != null) themePreviewImage.enabled = false;
            if (themePreviewPlaceholderShelf != null) themePreviewPlaceholderShelf.SetActive(false);

            // 현재 selectedTheme / random 으로 즉시 적용.
            ApplyPreviewToStageIfReady("EnsureOnboardingPreviewStage.end");
        }

        // SelectTheme / Random 시 호출 — preview stage 가 ready 면 그것에 ApplyTheme/ApplyRandomSelection.
        // PlayerPrefs 저장은 안 함 (preview 단계).
        //
        // Inspector camera override 가 enabled 면 매번 재적용 — Play 중 Inspector 값 변경 후 theme 카드
        // 한 번 클릭하면 새 값이 즉시 카메라에 반영되는 live tweak workflow.
        private void ApplyPreviewToStageIfReady(string source)
        {
            if (onboardingPreviewStage == null || !onboardingPreviewStage.IsReady) return;
            if (selectedRandomSelection != null && selectedRandomSelection.SelectedSpecs.Count > 0)
            {
                onboardingPreviewStage.ApplyRandomSelection(selectedRandomSelection);
            }
            else if (!string.IsNullOrWhiteSpace(selectedTheme))
            {
                var theme = OnboardingThemeAssetResolver.Resolve(selectedTheme);
                if (theme != null) onboardingPreviewStage.ApplyTheme(theme);
            }

            // Inspector override 재적용 (live tweak).
            var loginCtrl = GetComponent<LoginSceneController>();
            if (loginCtrl != null && loginCtrl.OverridePreviewCamera)
            {
                onboardingPreviewStage.OverrideCameraTransform(
                    loginCtrl.PreviewCameraPosition,
                    loginCtrl.PreviewCameraEulerAngles,
                    loginCtrl.PreviewCameraFov);
            }
        }

        // Step3 진입 / Step1 복귀 시 호출 — preview stage 자원 정리. RawImage 도 hide.
        private void DisposeOnboardingPreviewStage()
        {
            if (onboardingPreviewStage == null) return;
            onboardingPreviewStage.Dispose();
            onboardingPreviewStage = null;
            if (themePreviewRawImage != null) themePreviewRawImage.texture = null;
            if (themePreviewRawImageRoot != null) themePreviewRawImageRoot.SetActive(false);
            if (themePreviewImage != null) themePreviewImage.enabled = true;
            // override cache 리셋 — 다음 Step2 진입 때 1회 강제 적용 보장.
            lastPreviewOverrideEnabled = false;
            hasAppliedPreviewOverrideOnce = false;
        }

        // Step2 overlay 모드 활성/비활성. 활성 시:
        //   - backgroundRoot.SetActive(false): cream 전체 화면 BG 제거 → MainScene 노출
        //   - step2PanelBackgroundImage: enabled=true 유지 + alpha=0 (transparent) + raycastTarget=true
        //     → **시각적으로 투명** 하면서도 **MainScene click 흡수** (좌측 panel 외 영역 click 차단).
        //     이전 turn 의 enabled=false 처리는 raycast 도 비활성 → 사용자가 mainscene 책장/노트북 클릭
        //     가능했던 회귀 — 사용자 보고 "MainScene 기능 실행 안 시켜" 요구로 alpha=0+raycast=true 로 변경.
        //     좌측 panel 은 panel root 의 *자식* 이라 sibling order 가 더 큼 → 좌측 click 은 panel 이 우선 처리.
        //   - step2RightPreviewRoot.SetActive(false): RawImage/Image preview hide.
        // 비활성 시 (Step1/3 진입) 원래 alpha=1 + backgroundRoot 활성 복원.
        private void ApplyStep2OverlayMode(bool active)
        {
            if (backgroundRoot != null)
            {
                // Step2 활성 시 hide — MainScene 직접 노출. Step1/3 활성 시 active=true (cream 배경 보임).
                // 단 isStep3Overlay 분기에서 SetStepPresentationMode 가 별도 처리 — 그건 그대로 유지.
                if (active)
                {
                    backgroundRoot.SetActive(false);
                }
                else if (currentStep != 3)
                {
                    backgroundRoot.SetActive(true);
                }
            }
            if (step2PanelBackgroundImage != null)
            {
                // enabled=true + raycastTarget=true 유지 — MainScene click 차단의 핵심.
                step2PanelBackgroundImage.enabled = true;
                step2PanelBackgroundImage.raycastTarget = true;
                var c = step2PanelBackgroundImage.color;
                c.a = active ? 0f : 1f; // active 시 transparent, 비활성 시 원래 BackgroundColor 보임
                step2PanelBackgroundImage.color = c;
            }
            if (step2RightPreviewRoot != null)
            {
                step2RightPreviewRoot.SetActive(!active);
            }
            Debug.Log(
                $"[OnboardingOverlay] Step2 overlay mode={(active ? "ON" : "OFF")} " +
                $"backgroundRoot.active={(backgroundRoot != null ? backgroundRoot.activeSelf : false)} " +
                $"step2Bg.alpha={(step2PanelBackgroundImage != null ? step2PanelBackgroundImage.color.a : 1f):F2} " +
                $"step2Bg.raycast={(step2PanelBackgroundImage != null ? step2PanelBackgroundImage.raycastTarget : false)} " +
                $"rightPreview.active={(step2RightPreviewRoot != null ? step2RightPreviewRoot.activeSelf : false)}");
            if (active)
            {
                Debug.Log("[OnboardingOverlay] Left panel active=true");
                Debug.Log("[OnboardingOverlay] Right preview disabled");
                Debug.Log("[OnboardingOverlay] Root background alpha=0 (visual) + raycastTarget=true (MainScene click 차단)");
            }
        }

        // Step2 전용 quarter view preset — 기본 ApplyQuarterView 의 overviewAnchor pose 를 base 로,
        // 좌측 UI overlay (28%) 가 가리는 영역 보정 + room 전체 visibility 확보 위해:
        //   - 카메라를 base 보다 약간 뒤로 (room 전체가 frame 안에 들어오게)
        //   - 카메라를 살짝 오른쪽으로 (좌측 UI 가린 만큼 focal point 가 우측으로 shift 되도록)
        //   - fov 약간 wide (cinematic 한 cozy interior showcase 느낌)
        //   - 0.3s ease lerp (abrupt jump 회피)
        //
        // 호출 site: Step2MainScenePreloadRoutine 끝점 — MainScene ready + dependency wired 직후.
        private void ApplyOnboardingPreviewCameraView(BookshelfPorting.Runtime.CameraController controller)
        {
            if (controller == null) return;
            var cam = Camera.main;
            if (cam == null)
            {
                // CameraController.targetCamera 가 Camera.main 으로 fallback 처리 되어 있어 main camera
                // 없으면 ApplyQuarterView 도 no-op. 안전 skip.
                Debug.LogWarning("[OnboardingStep2Camera] Camera.main null — apply skip.");
                return;
            }

            // 진입 직전 transform 캡쳐 (시작 transform). 사용자가 보는 현재 frame.
            var startPos = cam.transform.position;
            var startRot = cam.transform.rotation;
            var startFov = cam.fieldOfView;

            // CameraController.ApplyQuarterView() 가 overviewAnchor pose 로 즉시 set — base.
            controller.ApplyQuarterView();
            var basePos = cam.transform.position;
            var baseRot = cam.transform.rotation;
            var baseFov = cam.fieldOfView;

            // 추가 조정 — Step2 전용 framing (사용자 보고: 기존 값 부족, "책장만 크게 보임" 회귀).
            //   distance: base.forward 반대 방향으로 2.5m (room 전체 + depth 가시)
            //   right shift: base.right 방향으로 0.8m (좌측 UI 28% 영역 보정 강화)
            //   fov: base + 10° (cinematic + wide framing)
            const float distanceOffset = 2.5f;
            const float rightShift = 0.8f;
            const float fovOffset = 10f;
            var targetPos = basePos
                            - baseRot * Vector3.forward * distanceOffset
                            + baseRot * Vector3.right * rightShift;
            var targetRot = baseRot;
            var targetFov = baseFov + fovOffset;

            // target pose 멤버 저장 — LateUpdate 가 매 frame 이 값으로 camera 강제 set (lock).
            step2CameraTargetPos = targetPos;
            step2CameraTargetRot = targetRot;
            step2CameraTargetFov = targetFov;
            isStep2CameraLockActive = true;

            // base 가 즉시 set 되었으니 시작 transform 으로 reset 후 coroutine 으로 ease.
            cam.transform.position = startPos;
            cam.transform.rotation = startRot;
            cam.fieldOfView = startFov;

            if (cameraTransitionRoutine != null)
            {
                StopCoroutine(cameraTransitionRoutine);
                cameraTransitionRoutine = null;
            }
            if (isActiveAndEnabled)
            {
                cameraTransitionRoutine = StartCoroutine(LerpCameraToOnboardingView(cam, targetPos, targetRot, targetFov, 0.3f));
            }
            else
            {
                cam.transform.position = targetPos;
                cam.transform.rotation = targetRot;
                cam.fieldOfView = targetFov;
            }

            Debug.Log("[OnboardingStep2Camera] Apply onboarding quarter view");
            Debug.Log($"[OnboardingStep2Camera] applied pos={targetPos} fov={targetFov:F1}");
            Debug.Log($"[OnboardingStep2Camera] rotation={targetRot.eulerAngles}");
            Debug.Log("[OnboardingStep2Camera] overlaySafeArea=28% lockActive=true");

            // 진단 coroutine — 1 frame / 10 frame 후 camera transform 출력. 우리 target 과 다르면
            // 어떤 component 가 덮어쓴 것 — 사용자가 콘솔 보고 원인 식별 가능.
            if (isActiveAndEnabled)
            {
                StartCoroutine(LogCameraDriftAcrossFrames(cam, targetPos, targetFov));
            }
        }

        private System.Collections.IEnumerator LogCameraDriftAcrossFrames(Camera cam, Vector3 expectedPos, float expectedFov)
        {
            if (cam == null) yield break;
            yield return null;
            Debug.Log(
                $"[OnboardingStep2Camera] after 1 frame pos={cam.transform.position} fov={cam.fieldOfView:F1} " +
                $"posDrift={(cam.transform.position - expectedPos).magnitude:F3} fovDrift={Mathf.Abs(cam.fieldOfView - expectedFov):F2}");
            for (int i = 0; i < 9; i++) yield return null;
            Debug.Log(
                $"[OnboardingStep2Camera] after 10 frames pos={cam.transform.position} fov={cam.fieldOfView:F1} " +
                $"posDrift={(cam.transform.position - expectedPos).magnitude:F3} fovDrift={Mathf.Abs(cam.fieldOfView - expectedFov):F2}");
        }

        private System.Collections.IEnumerator LerpCameraToOnboardingView(Camera cam, Vector3 targetPos, Quaternion targetRot, float targetFov, float duration)
        {
            if (cam == null) yield break;
            var startPos = cam.transform.position;
            var startRot = cam.transform.rotation;
            var startFov = cam.fieldOfView;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                cam.transform.position = Vector3.Lerp(startPos, targetPos, k);
                cam.transform.rotation = Quaternion.Slerp(startRot, targetRot, k);
                cam.fieldOfView = Mathf.Lerp(startFov, targetFov, k);
                yield return null;
            }
            cam.transform.position = targetPos;
            cam.transform.rotation = targetRot;
            cam.fieldOfView = targetFov;
            cameraTransitionRoutine = null;
        }

        // MainScene 의 ScreenSpace UI canvas 들을 toggle. World Space canvas (dashboard / notebook
        // 화면 mesh) 는 룸 visual 의 일부라 *건드리지 않음*. Onboarding canvas (DontDestroyOnLoad)
        // 도 제외.
        //
        // 추가: main camera 의 PhysicsRaycaster 도 toggle — 3D world click (책장/노트북/책 등) 차단.
        // 사용자 명시 요구 "MainScene 기능 실행 안 시켜" — UI click + 3D click 둘 다 차단.
        private void HideMainSceneUiForStep2(bool hide)
        {
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int affected = 0;
            for (int i = 0; i < canvases.Length; i++)
            {
                var c = canvases[i];
                if (c == null) continue;
                if (c == onboardingCanvas) continue;
                if (c.renderMode == RenderMode.WorldSpace) continue; // dashboard / notebook 화면 등 룸 visual 보존
                if (c.gameObject.activeSelf == !hide) continue; // 이미 의도된 상태면 skip
                c.gameObject.SetActive(!hide);
                affected++;
            }

            // main camera 의 PhysicsRaycaster — 3D world click 처리. hide=true 시 비활성 → mainscene
            // 책장/노트북/책 click 차단. 일부 환경엔 PhysicsRaycaster 없을 수도 (no-op).
            var mainCam = Camera.main;
            int physRaycasterAffected = 0;
            if (mainCam != null)
            {
                var raycaster = mainCam.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
                if (raycaster != null && raycaster.enabled == hide)
                {
                    raycaster.enabled = !hide;
                    physRaycasterAffected = 1;
                }
            }

            Debug.Log($"[OnboardingStep2Camera] hideMainSceneUI={hide} affectedCanvases={affected} physRaycaster={physRaycasterAffected}");
        }

        // SceneFlowManager.GoToMain() 이 onboarding scene 을 unload → OnboardingScreenController +
        // canvas + EventSystem GameObject 모두 destroy 회귀. Step2 진입 시 1회 DontDestroyOnLoad
        // 호출로 세 GameObject 보존. Canvas sortingOrder 도 main scene UI 위에 그려지도록 명시 set.
        private void PersistOnboardingAcrossSceneLoad()
        {
            if (persistedAcrossSceneLoad) return;

            // OnboardingScreenController 의 GameObject — LoginSceneController 의 host. DontDestroyOnLoad
            // 는 root GameObject 에만 동작. transform.parent != null 이면 SetParent(null) 필요.
            if (transform.parent != null)
            {
                transform.SetParent(null, true);
                Debug.Log("[OnboardingOverlay] Detached from parent for DontDestroyOnLoad.");
            }
            DontDestroyOnLoad(gameObject);

            // Canvas — BuildOnboardingCanvas 가 SceneManager.MoveGameObjectToScene 으로 onboarding
            // scene 에 부착했음. scene unload 시 destroy 회피 위해 별도 DontDestroyOnLoad.
            if (onboardingCanvas != null)
            {
                var canvasGo = onboardingCanvas.gameObject;
                if (canvasGo.transform.parent != null)
                {
                    canvasGo.transform.SetParent(null, true);
                }
                DontDestroyOnLoad(canvasGo);
                // sortingOrder 명시 — main scene UI 위에 그려지도록 충분히 높은 값.
                onboardingCanvas.overrideSorting = true;
                onboardingCanvas.sortingOrder = CanvasSortingOrder;
                Debug.Log($"[OnboardingOverlay] Canvas sortingOrder={onboardingCanvas.sortingOrder} renderMode={onboardingCanvas.renderMode}");

                // DontDestroyOnLoad scene 이동 + SetParent(null) 직후 renderMode/RectTransform 한 번 더 enforcement.
                EnforceOnboardingCanvasOverlayState("PersistOnboardingAcrossSceneLoad");
            }

            // EventSystem — onboarding UI 클릭 유지 필수. 자체 EventSystem 이 onboarding scene 에
            // 부착돼 있으면 scene unload 시 destroy 회귀. DontDestroyOnLoad 로 보존.
            // main scene 의 EventSystem 과 중복되면 Unity 가 자동으로 LogWarning 만 출력 — 동작에는
            // 영향 적음. 중복 검출 시 main scene 의 EventSystem 비활성을 follow-up 으로 처리.
            var eventSystem = UnityEngine.EventSystems.EventSystem.current
                ?? UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem != null && eventSystem.transform.parent == null)
            {
                DontDestroyOnLoad(eventSystem.gameObject);
                Debug.Log("[OnboardingOverlay] EventSystem persisted across scene load.");
            }

            persistedAcrossSceneLoad = true;
            Debug.Log("[OnboardingOverlay] Persist canvas across MainScene load.");
        }

        // Step2 진입 시 themeApplier.CurrentTheme 을 1회 캡쳐 — rollback 용. MainScene 이 아직
        // 로드 안 됐을 수 있어 themeApplier 가 null 이면 캡쳐 skip + 다음 호출에 재시도 (BeginStep2MainScenePreloadIfNeeded
        // coroutine 끝에서 다시 호출).
        private void CaptureStep2PreviewSnapshotIfNeeded()
        {
            if (step2PreviewSnapshotCaptured) return;
            var applier = UnityEngine.Object.FindFirstObjectByType<Dotshelf.Scripts.RoomTheme.RoomThemeApplier>(FindObjectsInactive.Include);
            if (applier == null)
            {
                Debug.Log("[OnboardingStep2MainPreview] Snapshot deferred — themeApplier not yet in scene (MainScene 미로드).");
                return;
            }
            step2PreviewSnapshotTheme = applier.CurrentTheme;
            step2PreviewSnapshotCaptured = true;
            Debug.Log(
                $"[OnboardingStep2MainPreview] Snapshot captured — theme='{(step2PreviewSnapshotTheme != null ? step2PreviewSnapshotTheme.name : "null")}'.");
        }

        // Step2 진입 시 MainScene 처리.
        //
        // **사용자 의도 (최종 확정)**: MainScene 을 통째로 로드하지 말고 prefab composition (room/책장/
        // 책상/대시보드) 만 가져와 preview. 즉 SceneFlowManager.GoToMain() 호출은 *금지*.
        //
        // 본 메서드의 현재 동작:
        //   - MainScene 이미 active 면 그대로 사용 (이미 메인 진입 후 재진입 case 만)
        //   - 아니면 **no-op** — SceneFlowManager.GoToMain() 호출 안 함. preview 는 sprite/placeholder
        //     fallback 으로 표시 (EnableMockRoomPreview=false default).
        //
        // (Phase 2 follow-up): MainSceneCompositionConfig.asset 의 6개 prefab (RuntimeSystems/
        // RoomBackground/BookshelfRig/NotebookStation/CameraAnchors/DashboardScreenPanel) 을 onboarding
        // scene 에 instantiate + PreviewCamera + RT 로 렌더. config 가 Resources 외 경로라 Runtime
        // Resources.Load 불가 → config 를 Resources 로 이동 또는 LoginSceneController inspector wire
        // 필요. BookshelfSceneInstaller 의 active-scene 가드 우회 + BookshelfGenerator wire / RoomThemeApplier
        // root 분리 등 큰 변경 — 별도 PR 로 분리.
        private void BeginStep2MainScenePreloadIfNeeded()
        {
            if (step2MainSceneAlreadyLoaded) return;
            if (SceneManager.GetActiveScene().name == SceneKeys.Main)
            {
                step2MainSceneAlreadyLoaded = true;
                Debug.Log("[OnboardingStep2MainPreview] MainScene already active — Step2 preview uses live main scene.");
                CaptureStep2PreviewSnapshotIfNeeded();
                return;
            }
            // **사용자 명시 요구**: MainScene 통째 로드 금지. preview 는 sprite/placeholder fallback.
            Debug.Log(
                "[OnboardingStep2MainPreview] MainScene 통째 로드 skip (사용자 의도: prefab composition 만). " +
                "preview = sprite/placeholder fallback. 실제 MainScene prefab composition 기반 RT preview 는 별도 PR.");
        }

        // MainScene load + ready 대기 + quarter view 적용 + snapshot 캡쳐 재시도. 책장 focus 는 skip —
        // Step3 진입 시점의 EnterBookshelfFocusForOnboarding 가 처리.
        private System.Collections.IEnumerator Step2MainScenePreloadRoutine()
        {
            if (SceneManager.GetActiveScene().name != SceneKeys.Main)
            {
                Debug.Log("[OnboardingStep2MainPreview] SceneFlowManager.GoToMain dispatch.");
                SceneFlowManager.GoToMain();
                var loadDeadline = Time.unscaledTime + MainSceneLoadTimeoutSeconds;
                while (SceneManager.GetActiveScene().name != SceneKeys.Main && Time.unscaledTime < loadDeadline)
                {
                    yield return null;
                }
                if (SceneManager.GetActiveScene().name != SceneKeys.Main)
                {
                    Debug.LogWarning("[OnboardingStep2MainPreview] MainScene 로드 timeout — preview 시각 안 보일 수 있음. sprite/placeholder fallback 유지.");
                    step3TransitionRoutine = null;
                    yield break;
                }
            }

            yield return null;
            yield return new WaitForEndOfFrame();
            yield return null;

            // BookshelfExperienceManager / CameraController ready 대기 — 일부 timeout 후에도 안 되면 진행.
            var readyDeadline = Time.unscaledTime + MainViewReadyTimeoutSeconds;
            BookshelfExperienceManager mgr = null;
            BookshelfPorting.Runtime.CameraController cam = null;
            BookshelfGenerator gen = null;
            while (Time.unscaledTime < readyDeadline)
            {
                if (TryGetBookshelfSceneDependencies(out mgr, out cam, out gen) &&
                    IsMainViewReadyForBookshelfFocus(mgr, cam, gen))
                {
                    break;
                }
                yield return null;
            }
            if (cam != null)
            {
                ApplyOnboardingPreviewCameraView(cam);
            }

            step2MainSceneAlreadyLoaded = true;
            CaptureStep2PreviewSnapshotIfNeeded();
            Debug.Log("[OnboardingStep2MainPreview] MainScene ready (Step2 preview).");

            // scene 로드 후 onboarding canvas 가 살아남았는지 + overlay 모드가 그대로 유지되는지 재확인.
            // PersistOnboardingAcrossSceneLoad 가 DontDestroyOnLoad 적용해 destroy 0 보장이지만,
            // background hide 등 visual 상태는 한 번 더 reapply (safe guard).
            ApplyStep2OverlayMode(true);
            Debug.Log("[OnboardingOverlay] Step2 overlay restored after scene load.");

            // preview 가 이미 의도된 테마로 가도록 — 현재 selectedTheme / selectedRandomSelection 기준 즉시 적용.
            ApplyPreviewInMainSceneIfActive("Step2MainScenePreloadRoutine.end");

            step3TransitionRoutine = null;
        }

        // MainScene 이 이미 로드된 상태면 RoomEditModeController.ApplyExternalThemePreview /
        // ApplyExternalRandomThemePreview 호출. 아니면 no-op (sprite/placeholder preview 로 fallback).
        // PlayerPrefs 저장은 안 함 — "다음 단계" 클릭 시점에 ApplySelectedThemeIfAvailable 가 confirm.
        private void ApplyPreviewInMainSceneIfActive(string source)
        {
            if (!step2MainSceneAlreadyLoaded) return;
            var controller = UnityEngine.Object.FindFirstObjectByType<RoomEditModeController>(FindObjectsInactive.Include);
            if (controller == null) return;

            if (selectedRandomSelection != null && selectedRandomSelection.SelectedSpecs.Count > 0)
            {
                controller.ApplyExternalRandomThemePreview(selectedRandomSelection, $"onboarding-preview:{source}");
                Debug.Log($"[OnboardingStep2MainPreview] Preview apply random slots={selectedRandomSelection.SelectedSpecs.Count} source={source}");
                return;
            }
            if (!string.IsNullOrWhiteSpace(selectedTheme))
            {
                var theme = OnboardingThemeAssetResolver.Resolve(selectedTheme);
                if (theme != null)
                {
                    controller.ApplyExternalThemePreview(theme, $"onboarding-preview:{source}");
                    Debug.Log($"[OnboardingStep2MainPreview] Preview apply theme='{selectedTheme}' source={source}");
                }
            }
        }

        // Step2 → Step1 / 취소 시 호출. snapshot 의 테마로 themeApplier.Apply 직접 호출 (PlayerPrefs 무관).
        // snapshot 이 null (Step2 진입 전 default 상태) 이면 themeCatalog.DefaultTheme 으로 fallback.
        private void RollbackStep2PreviewSnapshot(string source)
        {
            if (!step2PreviewSnapshotCaptured)
            {
                Debug.Log($"[OnboardingStep2MainPreview] Rollback skip — snapshot 미캡쳐. source={source}");
                return;
            }
            var applier = UnityEngine.Object.FindFirstObjectByType<Dotshelf.Scripts.RoomTheme.RoomThemeApplier>(FindObjectsInactive.Include);
            if (applier == null)
            {
                Debug.LogWarning($"[OnboardingStep2MainPreview] Rollback failed — themeApplier null. source={source}");
                return;
            }
            if (step2PreviewSnapshotTheme != null)
            {
                applier.Apply(step2PreviewSnapshotTheme);
                Debug.Log($"[OnboardingStep2MainPreview] Rollback preview on previous/cancel — theme='{step2PreviewSnapshotTheme.name}'. source={source}");
            }
            else
            {
                Debug.Log($"[OnboardingStep2MainPreview] Rollback preview — snapshot 이 null (default 상태). themeApplier 그대로 유지. source={source}");
            }
            // 한 번 rollback 후 snapshot reset — 다음 Step2 재진입 시 새 snapshot 캡쳐.
            step2PreviewSnapshotCaptured = false;
            step2PreviewSnapshotTheme = null;
        }

        // Step3 진입 시 + OnDestroy 시 호출. MainScene 로드 전에 PreviewLight / PreviewCamera 가
        // 정리되어야 MainScene 의 mesh 들이 PreviewLight 의 directional 영향을 받지 않는다.
        private void DisposeThemePreviewRenderer()
        {
            if (themePreviewFadeRoutine != null)
            {
                StopCoroutine(themePreviewFadeRoutine);
                themePreviewFadeRoutine = null;
            }
            if (themePreviewRenderer != null)
            {
                themePreviewRenderer.Dispose();
                themePreviewRenderer = null;
            }
            if (themePreviewRawImage != null) themePreviewRawImage.texture = null;
            if (themePreviewRawImageRoot != null) themePreviewRawImageRoot.SetActive(false);
            // gameObject SetActive 대신 component enabled — rightGo (Step2_RightPreview) 자체는 항상 활성.
            if (themePreviewImage != null) themePreviewImage.enabled = true;
        }

        // 테마 변경 시 RawImage 의 CanvasGroup alpha 를 0.55 → 1.0 으로 0.2초간 lerp. abrupt swap
        // 회피용. unscaled time 사용 — 다른 시스템이 timeScale 을 건드려도 일관 동작.
        private void BeginPreviewFade()
        {
            if (themePreviewRawImageGroup == null) return;
            if (themePreviewFadeRoutine != null)
            {
                StopCoroutine(themePreviewFadeRoutine);
                themePreviewFadeRoutine = null;
            }
            if (isActiveAndEnabled)
            {
                themePreviewFadeRoutine = StartCoroutine(FadeRawImageInRoutine());
            }
        }

        private System.Collections.IEnumerator FadeRawImageInRoutine()
        {
            if (themePreviewRawImageGroup == null) yield break;
            // 사용자 진단 로그에서 fade 진행 중 alpha=0.56~0.65 인 frame 동안 RT 가 cream 색만 남는
            // URP 회귀와 겹쳐 사용자 인지에 "안 보임" 으로 잡혔다. 시작 alpha 를 0.85 로 올리고
            // duration 도 단축 — fade 가 끝나기 전 frame 도 충분히 visible.
            const float duration = 0.12f;
            const float startAlpha = 0.85f;
            themePreviewRawImageGroup.alpha = startAlpha;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                themePreviewRawImageGroup.alpha = Mathf.Lerp(startAlpha, 1f, t / duration);
                yield return null;
            }
            themePreviewRawImageGroup.alpha = 1f;
            themePreviewFadeRoutine = null;
        }

        private void ApplyPlaceholderPreview(Color? tintOverride = null)
        {
            themePreviewImage.sprite = roundedRectSprite;
            themePreviewImage.type = Image.Type.Sliced;
            // tint 의 alpha 가 0 또는 1 미만이면 강제로 1 — Hex parse / Color initializer 경유 시 alpha=0 으로
            // 떨어지는 회귀 가드 (우측이 투명한 채 베이지 배경만 노출되는 경우 회피).
            var tint = tintOverride ?? PreviewAccentColor;
            if (tint.a < 0.99f) tint.a = 1f;
            themePreviewImage.color = tint;
            // themePreviewImage.gameObject == rightGo (Step2_RightPreview 자체) — SetActive 는 자식까지
            // 한꺼번에 hide 시키는 회귀 원인. component enabled 만 toggle.
            if (themePreviewImage != null) themePreviewImage.enabled = true;
            if (themePreviewPlaceholderShelf != null)
            {
                themePreviewPlaceholderShelf.SetActive(true);
            }
        }

        private static string GetThemeDebugName(string themeName)
        {
            switch (themeName)
            {
                case "내추럴":
                    return "Natural";
                case "클래식":
                    return "Classic";
                case "모던":
                    return "Modern";
                case "빈티지":
                    return "Vintage";
                case "파스텔":
                    return "Pastel";
                default:
                    return "Unknown";
            }
        }

        private static string GetThemePreviewCaption(string themeName)
        {
            switch (themeName)
            {
                case "내추럴":
                    return "따뜻한 우드 톤과 차분한 분위기를 가진 공간입니다.";
                case "클래식":
                    return "짙은 컬러와 균형감 있는 전통적인 분위기입니다.";
                case "모던":
                    return "정리된 선과 선명한 대비가 중심인 현대적인 분위기입니다.";
                case "빈티지":
                    return "세월감 있는 소재와 부드러운 채도가 어우러진 분위기입니다.";
                case "파스텔":
                    return "밝고 부드러운 컬러감이 강조된 가벼운 분위기입니다.";
                default:
                    return "테마를 선택하면 이 영역이 변경됩니다.";
            }
        }

        private static Color GetThemePreviewColor(string themeName)
        {
            switch (themeName)
            {
                case "내추럴":
                    return Hex("#E5D5BE");
                case "클래식":
                    return Hex("#D2C5B2");
                case "모던":
                    return Hex("#D6D9DF");
                case "빈티지":
                    return Hex("#E0D0C4");
                case "파스텔":
                    return Hex("#F1E4EC");
                default:
                    return PreviewAccentColor;
            }
        }

        private void SetStepPresentationMode(bool isStep3Overlay)
        {
            if (backgroundRoot != null)
            {
                backgroundRoot.SetActive(!isStep3Overlay);
            }

            if (cardRoot != null)
            {
                cardRoot.SetActive(!isStep3Overlay);
            }

            if (step3Panel != null)
            {
                step3Panel.SetActive(isStep3Overlay);
            }

            SetStep3LoadingPanelVisible(true);
            SetStep3StatusVisible(false);

            if (!isStep3Overlay)
            {
                SetStep3LoadingPanelVisible(false);
                SetStep3LoadingCoverVisible(false);
            }

            RefreshStep3CompletionGate(true);
        }

        private void SetStep3OverlayStatus(string message)
        {
            SetMessage(step3OverlayStatusLabel, message, TextColor);
        }

        private void SetStep3LoadingMessage(string message)
        {
            SetMessage(step3LoadingMessageLabel, message, MutedTextColor);
        }

        private void SetStep3LoadingCoverVisible(bool isVisible)
        {
            if (step3LoadingCover == null)
            {
                return;
            }

            step3LoadingCover.SetActive(isVisible);
        }

        private void SetStep3LoadingCoverAlpha(float alpha)
        {
            if (step3LoadingCoverImage == null)
            {
                return;
            }

            step3LoadingCoverImage.color = WithAlpha(BackgroundColor, Mathf.Clamp01(alpha));
        }

        private void SetStep3LoadingPanelVisible(bool isVisible)
        {
            if (step3LoadingContentRoot == null)
            {
                return;
            }

            step3LoadingContentRoot.SetActive(isVisible);
        }

        private void SetStep3StatusVisible(bool isVisible)
        {
            if (step3StatusRoot == null)
            {
                return;
            }

            step3StatusRoot.SetActive(isVisible);
        }

        private void EnterBookshelfFocusForOnboarding()
        {
            CancelStep3Transition();
            ResetStep3FlowState();
            SetStep3LoadingPanelVisible(true);
            SetStep3StatusVisible(false);
            SetStep3LoadingCoverVisible(true);
            SetStep3LoadingCoverAlpha(1f);
            SetStep3OverlayStatus("메인 화면을 준비 중입니다.");
            step3TransitionRoutine = StartCoroutine(EnterMainAndBookshelfFocusFlow());
        }

#if false
        private IEnumerator EnterBookshelfFocusRoutine()
        {
            EnsureCrossScenePersistence();

#if false
            if (SceneManager.GetActiveScene().name != SceneKeys.Main)
            {
                SetStep3OverlayStatus("메인 화면으로 이동 중입니다.");
                SceneFlowManager.GoToMain();

                var loadDeadline = Time.unscaledTime + MainSceneLoadTimeoutSeconds;
                while (SceneManager.GetActiveScene().name != SceneKeys.Main && Time.unscaledTime < loadDeadline)
                {
                    yield return null;
                }

                if (SceneManager.GetActiveScene().name != SceneKeys.Main)
                {
                SetStep3OverlayStatus("메인 화면으로 이동 중입니다.");
                SceneFlowManager.GoToMain();

                var loadDeadline = Time.unscaledTime + MainSceneLoadTimeoutSeconds;
                while (SceneManager.GetActiveScene().name != SceneKeys.Main && Time.unscaledTime < loadDeadline)
                {
                    yield return null;
                }

                if (SceneManager.GetActiveScene().name != SceneKeys.Main)
                {
                SetStep3OverlayStatus("메인 화면으로 이동 중입니다.");
                SceneFlowManager.GoToMain();

                while (SceneManager.GetActiveScene().name != SceneKeys.Main)
                {
                    yield return null;
                }

                yield return null;
                yield return new WaitForEndOfFrame();
                yield return null;
            }

            yield return new WaitForSecondsRealtime(MainScenePreviewDelaySeconds);

            SetStep3LoadingCoverVisible(false);
            yield return new WaitForSecondsRealtime(MainScenePreviewDelaySeconds);

            SetStep3LoadingCoverAlpha(MainScenePreviewCoverAlpha);

            BookshelfExperienceManager experienceManager = null;
            for (var attempt = 0; attempt < 180; attempt++)
            {
                if (TryGetBookshelfExperienceManager(out experienceManager))
                {
                    break;
                }

                yield return null;
            }

            if (experienceManager == null)
            {
                SetStep3OverlayStatus("책장 포커스 연결을 확인해주세요.");
                Debug.LogWarning("[OnboardingDemo] BookshelfExperienceManager was not ready after loading MainScene.");
                step3TransitionRoutine = null;
                yield break;
            }

#endif
            if (SceneManager.GetActiveScene().name != SceneKeys.Main)
            {
                SetStep3OverlayStatus("메인 화면으로 이동 중입니다.");
                SceneFlowManager.GoToMain();

                var loadDeadline = Time.unscaledTime + MainSceneLoadTimeoutSeconds;
                while (SceneManager.GetActiveScene().name != SceneKeys.Main && Time.unscaledTime < loadDeadline)
                {
                    yield return null;
                }

                if (SceneManager.GetActiveScene().name != SceneKeys.Main)
                {
                    SetStep3OverlayStatus("메인 화면 연결을 확인해주세요.");
                    Debug.LogWarning("[OnboardingDemo] MainScene did not become active before the Step 3 timeout.");
                    step3TransitionRoutine = null;
                    yield break;
                }
            }

            yield return null;
            yield return new WaitForEndOfFrame();
            yield return null;

            SetStep3OverlayStatus("책장 포커스 화면으로 이동 중입니다.");
            experienceManager.FocusBookshelf();

            var cameraController = UnityEngine.Object.FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
            var isFocused = false;
            for (var attempt = 0; attempt < 240; attempt++)
            {
                if (cameraController == null)
                {
                    cameraController = UnityEngine.Object.FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
                }

                if (experienceManager.IsBookshelfFocused && (cameraController == null || !cameraController.IsTransitioning))
                {
                    isFocused = true;
                    break;
                }

                yield return null;
            }

            if (isFocused)
            {
                SetStep3LoadingCoverVisible(false);
                SetStep3OverlayStatus("책장 포커스 화면이 표시되었습니다.");
            }
            else
            {
                SetStep3OverlayStatus("책장 포커스 연결을 확인해주세요.");
                Debug.LogWarning("[OnboardingDemo] Failed to complete bookshelf focus transition using existing scene flow.");
            }

            step3TransitionRoutine = null;
        }
#endif

        private bool TryGetBookshelfExperienceManager(out BookshelfExperienceManager experienceManager)
        {
            experienceManager = null;

            if (SceneManager.GetActiveScene().name != SceneKeys.Main)
            {
                ResetCachedMainSceneDependencies();
                return false;
            }

            if (cachedBookshelfExperienceManager == null)
            {
                cachedBookshelfExperienceManager = UnityEngine.Object.FindFirstObjectByType<BookshelfExperienceManager>(FindObjectsInactive.Include);
                if (cachedBookshelfExperienceManager == null)
                {
                    BookshelfAutoBootstrap.BootstrapScene();
                    cachedBookshelfExperienceManager = UnityEngine.Object.FindFirstObjectByType<BookshelfExperienceManager>(FindObjectsInactive.Include);
                }
            }

            experienceManager = cachedBookshelfExperienceManager;
            return experienceManager != null;
        }

        // Best-effort: if the bookshelf manager isn't reachable yet (e.g. main scene
        // hasn't loaded), the flag will be set on the next opportunity through
        // OnCompletionConfirmClicked / CompleteOnboarding, so silently no-op here.
        private void SetExperienceManagerStep3Active(bool active)
        {
            if (TryGetBookshelfExperienceManager(out var experienceManager) && experienceManager != null)
            {
                experienceManager.IsOnboardingStep3Active = active;
            }
        }

        private IEnumerator EnterMainAndBookshelfFocusFlow()
        {
            EnsureCrossScenePersistence();
            SetStep3LoadingPanelVisible(true);
            SetStep3StatusVisible(false);
            SetStep3LoadingCoverVisible(true);
            SetStep3LoadingCoverAlpha(1f);
            SetStep3OverlayStatus("메인 화면을 준비 중입니다.");

            if (SceneManager.GetActiveScene().name != SceneKeys.Main)
            {
                SetStep3OverlayStatus("메인 화면으로 이동 중입니다.");
                SceneFlowManager.GoToMain();

                var loadDeadline = Time.unscaledTime + MainSceneLoadTimeoutSeconds;
                while (SceneManager.GetActiveScene().name != SceneKeys.Main && Time.unscaledTime < loadDeadline)
                {
                    yield return null;
                }

                if (SceneManager.GetActiveScene().name != SceneKeys.Main)
                {
                    SetStep3OverlayStatus("메인 화면 연결을 확인해주세요.");
                    Debug.LogWarning("[OnboardingDemo] MainScene did not become active before the Step 3 timeout.");
                    step3TransitionRoutine = null;
                    yield break;
                }
            }

            yield return null;
            yield return new WaitForEndOfFrame();
            yield return null;

            SetStep3OverlayStatus("메인 화면을 준비 중입니다.");

            BookshelfExperienceManager experienceManager = null;
            CameraController cameraController = null;
            BookshelfGenerator bookshelfGenerator = null;

            // Step 3 special case: the player must see QuarterView (not the new MainView
            // default) before the bookshelf-focus transition kicks in. Snap before the
            // loading cover hides so there is no MainView flash.
            if (TryGetBookshelfSceneDependencies(out var initialExperienceManager, out var initialCameraController, out _))
            {
                if (initialExperienceManager != null)
                {
                    // Manager is now reachable — propagate the Step 3 flag in case the
                    // earlier ShowStep(3) call ran before the main scene finished loading.
                    initialExperienceManager.IsOnboardingStep3Active = true;
                }
                if (initialCameraController != null)
                {
                    initialCameraController.ApplyQuarterView();
                }
            }

            var readyDeadline = Time.unscaledTime + MainViewReadyTimeoutSeconds;
            var visualReadySince = -1f;
            while (Time.unscaledTime < readyDeadline)
            {
                if (TryGetBookshelfSceneDependencies(out experienceManager, out cameraController, out bookshelfGenerator) &&
                    IsMainViewReadyForBookshelfFocus(experienceManager, cameraController, bookshelfGenerator))
                {
                    if (visualReadySince < 0f)
                    {
                        visualReadySince = Time.unscaledTime;
                        SetStep3OverlayStatus("책장을 정리하는 중입니다.");
                        SetStep3LoadingMessage("책장과 책을 화면에 안정적으로 배치하고 있어요.");
                    }

                    if ((Time.unscaledTime - visualReadySince) >= BookshelfVisualSettleSeconds)
                    {
                        isMainViewReady = true;
                        break;
                    }
                }
                else
                {
                    visualReadySince = -1f;
                    SetStep3LoadingMessage("책장 정보를 불러오고 있어요.");
                }

                yield return null;
            }

            if (!isMainViewReady)
            {
                Debug.LogWarning("[OnboardingDemo] Main view was not fully ready before requesting bookshelf focus. A fallback focus attempt will still run.");
                if (!TryGetBookshelfSceneDependencies(out experienceManager, out cameraController, out bookshelfGenerator))
                {
                    SetStep3OverlayStatus("책장 포커스 연결을 확인해주세요.");
                    Debug.LogWarning("[OnboardingDemo] BookshelfExperienceManager was not ready after loading MainScene.");
                    step3TransitionRoutine = null;
                    yield break;
                }
            }
            else
            {
                SetStep3LoadingCoverVisible(false);
                yield return new WaitForSecondsRealtime(MainScenePreviewDelaySeconds);
            }

            // Step2 에서 선택한 테마 (또는 랜덤 버튼이 선택한 테마) 를 MainScene 에 적용 + 영속화.
            // MainView 의존성이 ready 된 직후 + 책장 포커스 진입 전 시점이 자연스러움 — 사용자가
            // 책장 focus 로 카메라가 이동하기 시작할 때 이미 새 테마 visual 이 반영돼 있음.
            // RoomEditModeController.ApplyExternalThemeSelection 가 themeApplier.Apply + Persist
            // LastAppliedLocalPreviewTheme + ApplyThemeDefaultEntries 를 single source 로 처리.
            ApplySelectedThemeIfAvailable("EnterMainAndBookshelfFocusFlow");

            SetStep3OverlayStatus("책장 포커스 화면으로 이동 중입니다.");
            var focusDeadline = Time.unscaledTime + BookshelfFocusTimeoutSeconds;
            var nextRetryAt = Time.unscaledTime;
            while (Time.unscaledTime < focusDeadline)
            {
                if (!TryGetBookshelfSceneDependencies(out experienceManager, out cameraController, out bookshelfGenerator))
                {
                    yield return null;
                    continue;
                }

                if (experienceManager.IsBookshelfFocused && !IsCameraTransitioning(cameraController))
                {
                    isBookshelfFocused = true;
                    break;
                }

                if (IsMainViewReadyForBookshelfFocus(experienceManager, cameraController, bookshelfGenerator) &&
                    Time.unscaledTime >= nextRetryAt)
                {
                    experienceManager.FocusBookshelf();
                    isBookshelfFocusRequested = true;
                    nextRetryAt = Time.unscaledTime + BookshelfFocusRetryIntervalSeconds;
                }

                yield return null;
            }

            if (!isBookshelfFocused && isMainViewReady && !isBookshelfFocusRequested && experienceManager != null)
            {
                Debug.LogWarning("[OnboardingDemo] Step 3 focus request was delayed until the timeout window. Triggering one last focus request.");
                experienceManager.FocusBookshelf();
                yield return null;
                yield return new WaitForEndOfFrame();
                isBookshelfFocused = experienceManager.IsBookshelfFocused;
            }

            if (isBookshelfFocused)
            {
                SetStep3LoadingCoverVisible(false);
                SetStep3OverlayStatus("책장 포커스 화면이 표시되었습니다.");
                RefreshStep3CompletionGate(true);
            }
            else
            {
                SetStep3LoadingCoverVisible(false);
                SetStep3OverlayStatus("책장 포커스 연결을 확인해주세요.");
                Debug.LogWarning("[OnboardingDemo] Failed to complete bookshelf focus transition using existing scene flow before the Step 3 timeout.");
            }

            step3TransitionRoutine = null;
        }

        private bool EnsureMainSceneIsReady()
        {
            if (SceneManager.GetActiveScene().name == SceneKeys.Main)
            {
                return true;
            }

            SetStep3OverlayStatus("메인 화면으로 이동 중입니다.");
            SceneFlowManager.GoToMain();

            var loadDeadline = Time.unscaledTime + MainSceneLoadTimeoutSeconds;
            while (SceneManager.GetActiveScene().name != SceneKeys.Main && Time.unscaledTime < loadDeadline)
            {
                // Waited by caller coroutine through repeated invocation.
                return false;
            }

            return SceneManager.GetActiveScene().name == SceneKeys.Main;
        }

        private bool TryGetBookshelfSceneDependencies(
            out BookshelfExperienceManager experienceManager,
            out CameraController cameraController,
            out BookshelfGenerator bookshelfGenerator)
        {
            experienceManager = null;
            cameraController = null;
            bookshelfGenerator = null;

            if (SceneManager.GetActiveScene().name != SceneKeys.Main)
            {
                ResetCachedMainSceneDependencies();
                return false;
            }

            if (cachedBookshelfExperienceManager == null)
            {
                cachedBookshelfExperienceManager = UnityEngine.Object.FindFirstObjectByType<BookshelfExperienceManager>(FindObjectsInactive.Include);
                if (cachedBookshelfExperienceManager == null)
                {
                    BookshelfAutoBootstrap.BootstrapScene();
                    cachedBookshelfExperienceManager = UnityEngine.Object.FindFirstObjectByType<BookshelfExperienceManager>(FindObjectsInactive.Include);
                }
            }

            if (cachedCameraController == null)
            {
                cachedCameraController = UnityEngine.Object.FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
            }

            if (cachedBookshelfGenerator == null)
            {
                cachedBookshelfGenerator = UnityEngine.Object.FindFirstObjectByType<BookshelfGenerator>(FindObjectsInactive.Include);
            }

            experienceManager = cachedBookshelfExperienceManager;
            cameraController = cachedCameraController;
            bookshelfGenerator = cachedBookshelfGenerator;
            return experienceManager != null && cameraController != null;
        }

        private bool IsMainViewReadyForBookshelfFocus(
            BookshelfExperienceManager experienceManager,
            CameraController cameraController,
            BookshelfGenerator bookshelfGenerator)
        {
            return experienceManager != null &&
                   experienceManager.isActiveAndEnabled &&
                   cameraController != null &&
                   cameraController.isActiveAndEnabled &&
                   !cameraController.IsTransitioning &&
                   IsInitialBookshelfRefreshComplete() &&
                   IsBookshelfVisualReadyForOnboarding(bookshelfGenerator);
        }

        private bool IsInitialBookshelfRefreshComplete()
        {
            if (cachedBookshelfApiSyncController == null)
            {
                cachedBookshelfApiSyncController = UnityEngine.Object.FindFirstObjectByType<BookshelfApiSyncController>(FindObjectsInactive.Include);
            }

            var syncController = cachedBookshelfApiSyncController;
            return syncController == null ||
                   !syncController.IsServerSyncEnabled ||
                   syncController.HasCompletedInitialRefresh;
        }

        private bool IsBookshelfVisualReadyForOnboarding(BookshelfGenerator bookshelfGenerator)
        {
            if (bookshelfGenerator == null || bookshelfGenerator.IsBookshelfSwitching)
            {
                return false;
            }

            if (cachedBookshelfApiSyncController == null)
            {
                cachedBookshelfApiSyncController = UnityEngine.Object.FindFirstObjectByType<BookshelfApiSyncController>(FindObjectsInactive.Include);
            }

            var syncController = cachedBookshelfApiSyncController;
            if (syncController == null || !syncController.IsServerSyncEnabled || !syncController.HasAccessToken)
            {
                return true;
            }

            if (!syncController.HasCompletedInitialRefresh)
            {
                return false;
            }

            if (!syncController.LastInitialRefreshSucceeded && !bookshelfGenerator.HasReceivedServerSnapshot)
            {
                return true;
            }

            return bookshelfGenerator.HasReceivedServerSnapshot;
        }

        private void ResetCachedMainSceneDependencies()
        {
            cachedBookshelfExperienceManager = null;
            cachedCameraController = null;
            cachedBookshelfGenerator = null;
            cachedBookshelfApiSyncController = null;
        }

        private void RefreshStep3CompletionGate(bool force)
        {
            if (step3CompleteButton == null)
            {
                return;
            }

            if (!force && Time.unscaledTime < nextStep3CompletionGateRefreshAt)
            {
                return;
            }

            nextStep3CompletionGateRefreshAt = Time.unscaledTime + 0.25f;
            // 표시 조건: Step 3 진입 + 책 추가 모달이 *실제 화면에 visible* 일 때만. 모달이 close 되면 즉시 hide,
            // reopen 시 즉시 show. 250ms throttle 안의 transient state 는 무시되지만 사용자 시각엔 lag 거의 없음.
            // 모달 visible == 책장 BE 프로비저닝 완료 가정 (AutoOpenBookAddPanelWhenReadyAsync 의 CanOpenBookAddPanel
            // 통과 후 OpenBookAddPanel 호출됐으니) — HasOnboardingBookshelf 추가 체크 불필요.
            var canComplete = currentStep == 3 && IsBookAddPanelVisibleInScene();
            if (!force && canComplete == isStep3CompletionReady)
            {
                return;
            }

            isStep3CompletionReady = canComplete;
            if (step3CompleteButton != null)
            {
                step3CompleteButton.gameObject.SetActive(canComplete);
            }
        }

        private bool IsBookAddPanelVisibleInScene()
        {
            if (!TryGetBookshelfExperienceManager(out var manager) || manager == null)
            {
                return false;
            }
            return manager.IsBookAddPanelVisible;
        }

        private bool HasOnboardingBookshelf()
        {
            if (SceneManager.GetActiveScene().name != SceneKeys.Main)
            {
                ResetCachedMainSceneDependencies();
                return false;
            }

            if (cachedBookshelfGenerator == null)
            {
                cachedBookshelfGenerator = UnityEngine.Object.FindFirstObjectByType<BookshelfGenerator>(FindObjectsInactive.Include);
            }

            return cachedBookshelfGenerator != null && cachedBookshelfGenerator.BookshelfCount > 0;
        }

        private static bool IsCameraTransitioning(CameraController cameraController)
        {
            return cameraController != null && cameraController.IsTransitioning;
        }

        private void ResetStep3FlowState()
        {
            isMainViewReady = false;
            isBookshelfFocusRequested = false;
            isBookshelfFocused = false;
        }

        private void EnsureCrossScenePersistence()
        {
            if (hasEnabledCrossScenePersistence)
            {
                return;
            }

            hasEnabledCrossScenePersistence = true;
            UnityEngine.Object.DontDestroyOnLoad(gameObject);

            if (onboardingCanvas != null)
            {
                UnityEngine.Object.DontDestroyOnLoad(onboardingCanvas.gameObject);
            }
        }

        private void CancelStep3Transition()
        {
            if (step3TransitionRoutine != null)
            {
                StopCoroutine(step3TransitionRoutine);
                step3TransitionRoutine = null;
            }

            SetStep3LoadingPanelVisible(false);
            SetStep3StatusVisible(false);
            SetStep3LoadingCoverVisible(false);
            ResetStep3FlowState();
        }

        private bool TryOpenImageFilePanel(out string selectedPath)
        {
            selectedPath = string.Empty;

            if (TryOpenWithStandaloneFileBrowser(out selectedPath, out var handledByPlugin))
            {
                return true;
            }

            if (handledByPlugin)
            {
                return false;
            }

#if UNITY_EDITOR
            selectedPath = UnityEditor.EditorUtility.OpenFilePanelWithFilters(
                "프로필 이미지 선택",
                string.Empty,
                new[] { "Image Files", "png,jpg,jpeg" });

            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                LogDebug("[OnboardingDemo] Profile image selection canceled.");
                return false;
            }

            return true;
#else
            Debug.LogWarning("파일 선택 기능은 현재 데모 환경에서만 지원됩니다.");
            return false;
#endif
        }

        private bool TryOpenWithStandaloneFileBrowser(out string selectedPath, out bool handled)
        {
            selectedPath = string.Empty;
            handled = false;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type fileBrowserType;
                try
                {
                    fileBrowserType = assembly.GetType("SFB.StandaloneFileBrowser");
                }
                catch
                {
                    fileBrowserType = null;
                }

                if (fileBrowserType == null)
                {
                    continue;
                }

                var extensionFilterType = assembly.GetType("SFB.ExtensionFilter");
                if (extensionFilterType == null)
                {
                    continue;
                }

                handled = true;

                try
                {
                    var filter = Activator.CreateInstance(extensionFilterType, "Image Files", new[] { "png", "jpg", "jpeg" });
                    var openFilePanel = fileBrowserType.GetMethod(
                        "OpenFilePanel",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(string), typeof(string), extensionFilterType.MakeArrayType(), typeof(bool) },
                        null);

                    if (openFilePanel == null)
                    {
                        continue;
                    }

                    var filters = Array.CreateInstance(extensionFilterType, 1);
                    filters.SetValue(filter, 0);
                    var result = openFilePanel.Invoke(null, new object[] { "프로필 이미지 선택", string.Empty, filters, false });
                    if (result is string[] paths && paths.Length > 0 && !string.IsNullOrWhiteSpace(paths[0]))
                    {
                        selectedPath = paths[0];
                        return true;
                    }

                    LogDebug("[OnboardingDemo] Profile image selection canceled.");
                    return false;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private bool TryLoadProfilePreview(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                LogDebug("[OnboardingDemo] Profile image selection canceled.");
                return false;
            }

            var extension = Path.GetExtension(imagePath);
            if (!string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("[OnboardingDemo] Unsupported image file selected.");
                return false;
            }

            try
            {
                var imageBytes = File.ReadAllBytes(imagePath);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.name = "OnboardingProfilePreview";
                texture.hideFlags = HideFlags.HideAndDontSave;

                if (!texture.LoadImage(imageBytes))
                {
                    Destroy(texture);
                    return false;
                }

                ReleaseProfilePreview();
                profileTexture = texture;
                profileSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                profileSprite.name = "OnboardingProfilePreviewSprite";
                profileSprite.hideFlags = HideFlags.HideAndDontSave;

                if (profilePreviewImage != null)
                {
                    profilePreviewImage.sprite = profileSprite;
                    profilePreviewImage.color = Color.white;
                    profilePreviewImage.type = Image.Type.Simple;
                    profilePreviewImage.preserveAspect = true;
                }

                var placeholder = profilePreviewImage != null ? profilePreviewImage.GetComponentInChildren<TMP_Text>(true) : null;
                if (placeholder != null)
                {
                    placeholder.gameObject.SetActive(false);
                }

                LogDebug($"[OnboardingDemo] Loaded profile preview from '{imagePath}'.");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[OnboardingDemo] Failed to read selected image: {exception.Message}");
                return false;
            }
        }

        private void ResetProfilePreview()
        {
            ReleaseProfilePreview();

            if (profilePreviewImage != null)
            {
                profilePreviewImage.sprite = roundedRectSprite;
                profilePreviewImage.type = Image.Type.Sliced;
                profilePreviewImage.color = InputColor;
                profilePreviewImage.preserveAspect = false;
            }

            var placeholder = profilePreviewImage != null ? profilePreviewImage.GetComponentInChildren<TMP_Text>(true) : null;
            if (placeholder != null)
            {
                placeholder.gameObject.SetActive(true);
            }
        }

        private void ReleaseProfilePreview()
        {
            if (profileSprite != null)
            {
                Destroy(profileSprite);
                profileSprite = null;
            }

            if (profileTexture != null)
            {
                Destroy(profileTexture);
                profileTexture = null;
            }
        }

        private void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
            eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
        }

        private TMP_InputField CreateFlexibleInput(Transform parent, string objectName, string placeholderText, bool isPassword)
        {
            var fieldBackground = CreateImage(parent, objectName, InputColor);
            fieldBackground.sprite = roundedRectSprite;
            fieldBackground.type = Image.Type.Sliced;

            var fieldLayout = fieldBackground.gameObject.AddComponent<LayoutElement>();
            fieldLayout.preferredHeight = PrimaryControlHeight;
            fieldLayout.minHeight = PrimaryControlHeight;
            fieldLayout.flexibleWidth = 1f;

            var fieldOutline = fieldBackground.gameObject.AddComponent<Outline>();
            fieldOutline.effectColor = WithAlpha(BorderColor, 0.95f);
            fieldOutline.effectDistance = new Vector2(1f, -1f);

            var input = fieldBackground.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = fieldBackground;
            input.textViewport = CreateViewport(fieldBackground.transform);
            input.contentType = isPassword ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.caretColor = TextColor;
            input.selectionColor = WithAlpha(PrimaryColor, 0.25f);
            input.customCaretColor = true;

            var textComponent = CreateText(input.textViewport, "Text", string.Empty, 17f, FontStyles.Normal, TextColor, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero, 0f);
            StretchWithPadding(textComponent.rectTransform, new Vector2(16f, 10f), new Vector2(-16f, -10f));
            textComponent.textWrappingMode = TextWrappingModes.NoWrap;
            textComponent.overflowMode = TextOverflowModes.Ellipsis;

            var placeholder = CreateText(input.textViewport, "Placeholder", placeholderText, 17f, FontStyles.Normal, WithAlpha(MutedTextColor, 0.7f), TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero, 0f);
            StretchWithPadding(placeholder.rectTransform, new Vector2(16f, 10f), new Vector2(-16f, -10f));
            placeholder.textWrappingMode = TextWrappingModes.NoWrap;
            placeholder.overflowMode = TextOverflowModes.Ellipsis;

            input.textComponent = textComponent;
            input.placeholder = placeholder;
            return input;
        }

        private Button CreateFixedButton(
            Transform parent,
            string objectName,
            string labelText,
            Color backgroundColor,
            Color labelColor,
            bool outlined,
            Color outlineColor,
            Vector2 size)
        {
            var button = CreateButton(parent, objectName, labelText, backgroundColor, labelColor, Vector2.zero, size, outlined, outlineColor);
            var rectTransform = (RectTransform)button.transform;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;

            var layoutElement = button.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = size.x;
            layoutElement.minWidth = size.x;
            layoutElement.preferredHeight = size.y;
            layoutElement.minHeight = size.y;
            layoutElement.flexibleWidth = 0f;

            return button;
        }

        private Button CreateButton(
            Transform parent,
            string objectName,
            string labelText,
            Color backgroundColor,
            Color labelColor,
            Vector2 anchoredPosition,
            Vector2 size,
            bool outlined,
            Color outlineColor)
        {
            var buttonImage = CreateImage(parent, objectName, backgroundColor);
            buttonImage.sprite = roundedRectSprite;
            buttonImage.type = Image.Type.Sliced;

            var buttonRect = buttonImage.rectTransform;
            buttonRect.anchorMin = new Vector2(0.5f, 1f);
            buttonRect.anchorMax = new Vector2(0.5f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            buttonRect.sizeDelta = size;
            buttonRect.anchoredPosition = anchoredPosition;

            if (outlined)
            {
                var outline = buttonImage.gameObject.AddComponent<Outline>();
                outline.effectColor = WithAlpha(outlineColor, 0.95f);
                outline.effectDistance = new Vector2(1f, -1f);
            }

            var button = buttonImage.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.transition = Selectable.Transition.ColorTint;

            var colors = button.colors;
            colors.normalColor = backgroundColor;
            colors.highlightedColor = Blend(backgroundColor, Color.white, 0.08f);
            colors.pressedColor = outlined ? Blend(backgroundColor, TextColor, 0.06f) : PrimaryPressedColor;
            colors.selectedColor = outlined ? Blend(backgroundColor, Color.white, 0.08f) : PrimaryHighlightColor;
            colors.disabledColor = WithAlpha(backgroundColor, 0.5f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var label = CreateText(buttonImage.transform, "Label", labelText, 16f, FontStyles.Bold, labelColor, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero);
            StretchToParent(label.rectTransform);

            return button;
        }

        private RectTransform CreateViewport(Transform parent)
        {
            var viewportObject = CreateRectObject("TextArea", parent);
            var viewport = (RectTransform)viewportObject.transform;
            StretchToParent(viewport);
            return viewport;
        }

        private TMP_Text CreateText(
            Transform parent,
            string objectName,
            string value,
            float fontSize,
            FontStyles fontStyle,
            Color color,
            TextAlignmentOptions alignment,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            float characterSpacing = 0f,
            Vector2? pivotOverride = null)
        {
            var textObject = CreateRectObject(objectName, parent);
            var rectTransform = (RectTransform)textObject.transform;
            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rectTransform.pivot = pivotOverride ?? new Vector2(0.5f, 1f);
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.anchoredPosition = anchoredPosition;

            var text = textObject.AddComponent<TextMeshProUGUI>();
            ApplyFontToText(text);
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = alignment;
            text.characterSpacing = characterSpacing;
            text.raycastTarget = false;

            return text;
        }

        private TMP_Text CreateSectionText(
            Transform parent,
            string objectName,
            string value,
            float fontSize,
            FontStyles fontStyle,
            Color color,
            TextAlignmentOptions alignment,
            float preferredHeight,
            float characterSpacing = 0f)
        {
            var text = CreateText(parent, objectName, value, fontSize, fontStyle, color, alignment, Vector2.zero, Vector2.zero, characterSpacing);
            var rectTransform = text.rectTransform;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;

            var layoutElement = text.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.minHeight = preferredHeight;
            layoutElement.flexibleWidth = 1f;
            return text;
        }

        private Image CreateImage(Transform parent, string objectName, Color color)
        {
            var imageObject = CreateRectObject(objectName, parent);
            var image = imageObject.AddComponent<Image>();
            image.sprite = whiteSprite;
            image.color = color;
            image.type = Image.Type.Simple;
            image.raycastTarget = true;
            return image;
        }

        private void CreateAccentBlob(Transform parent, string objectName, Vector2 anchor, Vector2 size)
        {
            var image = CreateImage(parent, objectName, WithAlpha(AccentTint, 0.22f));
            image.sprite = roundedRectSprite;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;

            var rect = image.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }

        private TMP_FontAsset ResolveFontAsset()
        {
            var preferredFontAsset = FindFontAssetByName(PreferredKoreanFontAssetName);
            if (preferredFontAsset != null)
            {
                fallbackFontAsset = preferredFontAsset;
                return preferredFontAsset;
            }

            preferredFontAsset = FindFontAssetByName(FallbackKoreanFontAssetName);
            if (preferredFontAsset != null)
            {
                fallbackFontAsset = preferredFontAsset;
                return preferredFontAsset;
            }

            fallbackFontAsset = TMP_Settings.defaultFontAsset;

            var existingText = UnityEngine.Object.FindFirstObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include);
            var candidateFontAsset = existingText != null ? existingText.font : TMP_Settings.defaultFontAsset;

            if (TryCreateDynamicFontAsset(candidateFontAsset, out var dynamicFontAsset))
            {
                fallbackFontAsset = candidateFontAsset;
                return dynamicFontAsset;
            }

            if (TryCreateDynamicFontAsset(fallbackFontAsset, out dynamicFontAsset))
            {
                return dynamicFontAsset;
            }

            if (candidateFontAsset != null)
            {
                return candidateFontAsset;
            }

            return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        private TMP_FontAsset FindFontAssetByName(string fontAssetName)
        {
            if (string.IsNullOrWhiteSpace(fontAssetName))
            {
                return null;
            }

            var fontAssets = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (var candidate in fontAssets)
            {
                if (candidate != null && string.Equals(candidate.name, fontAssetName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        private void ApplyFontToOnboardingCanvas()
        {
            if (onboardingCanvas == null || fontAsset == null)
            {
                return;
            }

            var tmpTexts = onboardingCanvas.GetComponentsInChildren<TMP_Text>(true);
            foreach (var tmpText in tmpTexts)
            {
                ApplyFontToText(tmpText);
            }

            var inputFields = onboardingCanvas.GetComponentsInChildren<TMP_InputField>(true);
            foreach (var inputField in inputFields)
            {
                if (inputField == null)
                {
                    continue;
                }

                ApplyFontToText(inputField.textComponent);

                if (inputField.placeholder is TMP_Text placeholderText)
                {
                    ApplyFontToText(placeholderText);
                }
            }
        }

        private void ApplyFontToText(TMP_Text text)
        {
            if (text == null || fontAsset == null)
            {
                return;
            }

            text.font = fontAsset;
            if (fontAsset.material != null)
            {
                text.fontSharedMaterial = fontAsset.material;
            }

            if (fallbackFontAsset != null && text.font != null && text.font != fallbackFontAsset)
            {
                var fallbackTable = text.font.fallbackFontAssetTable;
                if (fallbackTable == null)
                {
                    fallbackTable = new List<TMP_FontAsset>();
                    text.font.fallbackFontAssetTable = fallbackTable;
                }

                if (!fallbackTable.Contains(fallbackFontAsset))
                {
                    fallbackTable.Add(fallbackFontAsset);
                }
            }
        }

        private void WarmupOnboardingGlyphs(TMP_FontAsset targetFontAsset)
        {
            if (targetFontAsset == null || targetFontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            {
                return;
            }

            const string commonText =
                "STEP 1STEP 2STEP 3" +
                "프로필을 설정해주세요" +
                "프로필 이미지, 닉네임, 한줄소개를 먼저 입력합니다." +
                "프로필 이미지" +
                "사진 추가" +
                "닉네임" +
                "닉네임을 입력하세요" +
                "중복확인" +
                "한줄소개" +
                "한줄소개를 입력하세요" +
                "사용 가능한 닉네임입니다." +
                "닉네임을 입력해주세요." +
                "닉네임 중복확인을 해주세요." +
                "다음 단계" +
                "테마를 선택해주세요" +
                "방과 책장의 분위기를 먼저 정해둘 수 있습니다." +
                "랜덤" +
                "내추럴클래식모던빈티지파스텔" +
                "테마 미리보기" +
                "테마를 선택하면 이 영역이 변경됩니다." +
                "메인 화면으로 이동 중입니다." +
                "책장 포커스 화면으로 이동 중입니다." +
                "책장 포커스 화면이 표시되었습니다." +
                "책장 포커스 연결을 확인해주세요." +
                "메인 화면과 책장 포커스 화면으로 이동할 준비가 되었습니다." +
                "첫 책장을 만든 뒤 완료할 수 있습니다." +
                "이전 단계완료";

            targetFontAsset.TryAddCharacters(commonText, out _);
        }

        private bool TryCreateDynamicFontAsset(TMP_FontAsset sourceFontAsset, out TMP_FontAsset dynamicFontAsset)
        {
            dynamicFontAsset = null;
            if (sourceFontAsset == null || sourceFontAsset.sourceFontFile == null)
            {
                return false;
            }

            try
            {
                dynamicFontAsset = TMP_FontAsset.CreateFontAsset(
                    sourceFontAsset.sourceFontFile,
                    90,
                    9,
                    GlyphRenderMode.SDFAA,
                    1024,
                    1024,
                    AtlasPopulationMode.Dynamic,
                    true);

                if (dynamicFontAsset == null)
                {
                    return false;
                }

                dynamicFontAsset.name = $"{sourceFontAsset.name}_OnboardingDynamic";
                dynamicFontAsset.hideFlags = HideFlags.HideAndDontSave;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private Sprite CreateWhiteSprite()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "OnboardingUiWhite";
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            sprite.name = "OnboardingUiWhiteSprite";
            return sprite;
        }

        private Sprite CreateRoundedRectSprite(int size, int radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "OnboardingUiRounded";
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            var pixels = new Color32[size * size];
            var solid = new Color32(255, 255, 255, 255);
            var transparent = new Color32(255, 255, 255, 0);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    pixels[(y * size) + x] = IsInsideRoundedRect(x, y, size, radius) ? solid : transparent;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var border = new Vector4(radius, radius, radius, radius);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect,
                border);

            sprite.name = "OnboardingUiRoundedSprite";
            return sprite;
        }

        private static bool IsInsideRoundedRect(int x, int y, int size, int radius)
        {
            if (x >= radius && x < size - radius)
            {
                return true;
            }

            if (y >= radius && y < size - radius)
            {
                return true;
            }

            var cornerCenterX = x < radius ? radius - 1 : size - radius;
            var cornerCenterY = y < radius ? radius - 1 : size - radius;
            var dx = x - cornerCenterX;
            var dy = y - cornerCenterY;
            return (dx * dx) + (dy * dy) <= radius * radius;
        }

        private static GameObject CreateRectObject(string objectName, Transform parent)
        {
            var result = new GameObject(objectName, typeof(RectTransform));
            result.layer = UiLayer;
            result.transform.SetParent(parent, false);
            return result;
        }

        private static RectTransform CreateColumn(Transform parent, string objectName, float spacing, RectOffset padding, TextAnchor alignment)
        {
            var columnObject = CreateRectObject(objectName, parent);
            var rectTransform = (RectTransform)columnObject.transform;

            var layoutGroup = columnObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = spacing;
            layoutGroup.padding = padding;
            layoutGroup.childAlignment = alignment;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            return rectTransform;
        }

        private static RectTransform CreateRow(Transform parent, string objectName, float spacing, TextAnchor alignment)
        {
            var rowObject = CreateRectObject(objectName, parent);
            var rectTransform = (RectTransform)rowObject.transform;

            var layoutGroup = rowObject.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.spacing = spacing;
            layoutGroup.childAlignment = alignment;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            var layoutElement = rowObject.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;
            layoutElement.minHeight = PrimaryControlHeight;

            return rectTransform;
        }

        private static void CreateSpacer(Transform parent, float preferredHeight)
        {
            var spacer = CreateRectObject("Spacer", parent);
            var layoutElement = spacer.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.minHeight = preferredHeight;
            layoutElement.flexibleWidth = 1f;
        }

        private static void CreateFlexibleSpacer(Transform parent)
        {
            var spacer = CreateRectObject("FlexibleSpacer", parent);
            var layoutElement = spacer.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;
            layoutElement.flexibleHeight = 1f;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
        }

        private static void StretchWithPadding(RectTransform rectTransform, Vector2 minOffset, Vector2 maxOffset)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = minOffset;
            rectTransform.offsetMax = maxOffset;
            rectTransform.anchoredPosition = Vector2.zero;
        }

        private GameObject FindExistingCanvasObject()
        {
            var scene = gameObject.scene;
            if (!scene.IsValid())
            {
                return null;
            }

            foreach (var rootObject in scene.GetRootGameObjects())
            {
                if (rootObject != null && rootObject.name == CanvasName)
                {
                    return rootObject;
                }
            }

            return null;
        }

        private static BookshelfResponseDto FindFirstActiveBookshelf(BookshelfListResponseDto shelves)
        {
            var items = shelves?.Bookshelves;
            if (items == null)
            {
                return null;
            }

            BookshelfResponseDto best = null;
            for (var i = 0; i < items.Count; i++)
            {
                var shelf = items[i];
                if (shelf == null || shelf.Id <= 0L)
                {
                    continue;
                }

                if (best == null || shelf.DisplayOrder < best.DisplayOrder)
                {
                    best = shelf;
                }
            }

            return best;
        }

        private static string BuildOnboardingBookshelfName()
        {
            return "기본 책장";
        }

        private static string FormatApiErrorMessage(ApiErrorResponseDto error, string fallback)
        {
            return FirstNonEmpty(error?.Message, error?.Error, fallback);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (var i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    return values[i];
                }
            }

            return string.Empty;
        }

        private static void SetMessage(TMP_Text label, string message, Color color)
        {
            if (label == null)
            {
                return;
            }

            label.text = message;
            label.color = color;
        }

        private static Color Blend(Color from, Color to, float t)
        {
            return Color.Lerp(from, to, Mathf.Clamp01(t));
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static Color Hex(string html)
        {
            return ColorUtility.TryParseHtmlString(html, out var color) ? color : Color.white;
        }
    }
}
