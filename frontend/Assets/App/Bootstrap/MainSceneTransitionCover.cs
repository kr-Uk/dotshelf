using System.Collections;
using App.Auth;
using App.Infrastructure.Api;
using BookshelfPorting.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace App.Bootstrap
{
    // Fullscreen cover that masks the brief frames between when LoginScene goes
    // away and MainScene's BookshelfSceneInstaller has finished spinning up the
    // bookshelf experience. DontDestroyOnLoad survives the scene swap so the
    // player never sees a half-loaded scene between Login and Main.
    //
    // Onboarding step 3 has its own dedicated loading cover, so this cover is
    // used by the regular login / guest / social-login paths that go straight
    // to main.
    public sealed class MainSceneTransitionCover : MonoBehaviour
    {
        private const int CanvasSortingOrder = 7000;
        private const float MinCoverDurationSeconds = 0.25f;
        private const float MaxCoverDurationSeconds = 24f;
        private const float ReadyRevealDelaySeconds = 3f;
        private const float FailureNoticeDurationSeconds = 1.25f;
        private const float ProgressBarWidth = 560f;
        private const float ProgressBarHeight = 18f;
        private const float ProgressMoveSpeed = 0.85f;

        private static readonly Color BackgroundColor = new Color(0.96f, 0.94f, 0.90f, 1f);
        private static readonly Color TextColor = new Color(0.18f, 0.14f, 0.09f, 1f);
        private static readonly Color DetailTextColor = new Color(0.42f, 0.35f, 0.27f, 1f);
        private static readonly Color ProgressTrackColor = new Color(0.83f, 0.78f, 0.69f, 1f);
        private static readonly Color ProgressFillColor = new Color(0.48f, 0.25f, 0.12f, 1f);

        private bool hasRequestedInitialBookshelfRefresh;
        private bool initialBookshelfRefreshCompleted;
        private bool initialBookshelfRefreshFailed;
        private string initialBookshelfRefreshMessage;
        private TextMeshProUGUI messageLabel;
        private TextMeshProUGUI detailLabel;
        private TextMeshProUGUI percentLabel;
        private RectTransform progressFillRect;
        private float targetProgress;
        private float displayedProgress;
        private BookshelfExperienceManager cachedExperienceManager;
        private BookshelfApiSyncController cachedSyncController;
        private BookshelfGenerator cachedBookshelfGenerator;

        public static void Show(string message = "메인 화면으로 이동 중입니다.")
        {
            if (FindFirstObjectByType<MainSceneTransitionCover>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            var host = new GameObject(nameof(MainSceneTransitionCover));
            DontDestroyOnLoad(host);
            var cover = host.AddComponent<MainSceneTransitionCover>();
            cover.BuildCanvas(message);
            cover.StartCoroutine(cover.WaitForMainSceneAndDestroy());
        }

        private void BuildCanvas(string message)
        {
            var canvasObject = new GameObject("Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = CanvasSortingOrder;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(canvasObject.transform, false);
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fill.GetComponent<Image>();
            fillImage.color = BackgroundColor;
            fillImage.raycastTarget = true;

            var contentObject = new GameObject("LoadingContent", typeof(RectTransform));
            contentObject.transform.SetParent(canvasObject.transform, false);
            var contentRect = (RectTransform)contentObject.transform;
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(820f, 300f);
            contentRect.anchoredPosition = Vector2.zero;

            messageLabel = CreateLabel(contentObject.transform, "Title", message, 36f, TextColor, FontStyles.Bold);
            var titleRect = (RectTransform)messageLabel.transform;
            titleRect.sizeDelta = new Vector2(820f, 80f);
            titleRect.anchoredPosition = new Vector2(0f, 88f);

            detailLabel = CreateLabel(contentObject.transform, "Detail", "방을 꾸미는 중입니다.", 24f, DetailTextColor, FontStyles.Normal);
            var detailRect = (RectTransform)detailLabel.transform;
            detailRect.sizeDelta = new Vector2(820f, 70f);
            detailRect.anchoredPosition = new Vector2(0f, 22f);

            var progressTrack = new GameObject("ProgressTrack", typeof(RectTransform), typeof(Image));
            progressTrack.transform.SetParent(contentObject.transform, false);
            var trackRect = (RectTransform)progressTrack.transform;
            trackRect.anchorMin = new Vector2(0.5f, 0.5f);
            trackRect.anchorMax = new Vector2(0.5f, 0.5f);
            trackRect.pivot = new Vector2(0.5f, 0.5f);
            trackRect.sizeDelta = new Vector2(ProgressBarWidth, ProgressBarHeight);
            trackRect.anchoredPosition = new Vector2(0f, -48f);
            progressTrack.GetComponent<Image>().color = ProgressTrackColor;

            var progressFill = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
            progressFill.transform.SetParent(progressTrack.transform, false);
            progressFillRect = (RectTransform)progressFill.transform;
            progressFillRect.anchorMin = new Vector2(0f, 0f);
            progressFillRect.anchorMax = new Vector2(0f, 1f);
            progressFillRect.pivot = new Vector2(0f, 0.5f);
            progressFillRect.sizeDelta = new Vector2(0f, 0f);
            progressFillRect.anchoredPosition = Vector2.zero;
            progressFill.GetComponent<Image>().color = ProgressFillColor;

            percentLabel = CreateLabel(contentObject.transform, "Percent", "0%", 22f, DetailTextColor, FontStyles.Bold);
            var percentRect = (RectTransform)percentLabel.transform;
            percentRect.sizeDelta = new Vector2(240f, 44f);
            percentRect.anchoredPosition = new Vector2(0f, -94f);

            SetProgress(0.04f, true);
        }

        private IEnumerator WaitForMainSceneAndDestroy()
        {
            var startTime = Time.unscaledTime;
            var deadline = startTime + MaxCoverDurationSeconds;
            var readyForReveal = false;
            var readyStartedAt = -1f;

            while (Time.unscaledTime < deadline)
            {
                var mainSceneReady = IsMainSceneReadyForReveal();
                var minDurationElapsed = (Time.unscaledTime - startTime) >= MinCoverDurationSeconds;

                if (mainSceneReady)
                {
                    if (readyStartedAt < 0f)
                    {
                        readyStartedAt = Time.unscaledTime;
                    }

                    var settleProgress = Mathf.Clamp01((Time.unscaledTime - readyStartedAt) / ReadyRevealDelaySeconds);
                    SetLoadingStage(
                        "책을 꽂는 중입니다.",
                        "책장과 책이 화면에 안정적으로 자리 잡는 중입니다.",
                        Mathf.Lerp(0.9f, 1f, settleProgress));

                    if (settleProgress >= 1f && minDurationElapsed)
                    {
                        readyForReveal = true;
                        SetProgress(1f);
                        TickProgress(true);
                        if (initialBookshelfRefreshFailed)
                        {
                            SetMessage(BuildInitialRefreshFailureMessage());
                            SetDetailMessage("잠시 후 메인 화면으로 이동합니다.");
                            yield return new WaitForSecondsRealtime(FailureNoticeDurationSeconds);
                        }

                        break;
                    }
                }
                else
                {
                    readyStartedAt = -1f;
                    UpdateLoadingStage(startTime);
                }

                TickProgress();
                yield return null;
            }

            if (!readyForReveal && Time.unscaledTime >= deadline)
            {
                SetMessage("책장 로딩이 지연되어 메인 화면으로 이동합니다.");
                SetDetailMessage("잠시 후 다시 동기화됩니다.");
                yield return new WaitForSecondsRealtime(FailureNoticeDurationSeconds);
            }

            yield return new WaitForEndOfFrame();
            yield return null;

            Destroy(gameObject);
        }

        private bool IsMainSceneReadyForReveal()
        {
            if (SceneManager.GetActiveScene().name != SceneKeys.Main)
            {
                return false;
            }

            if (cachedExperienceManager == null)
            {
                cachedExperienceManager = FindFirstObjectByType<BookshelfExperienceManager>(FindObjectsInactive.Include);
            }

            if (cachedExperienceManager == null)
            {
                return false;
            }

            if (cachedSyncController == null)
            {
                cachedSyncController = FindFirstObjectByType<BookshelfApiSyncController>(FindObjectsInactive.Include);
            }

            var shouldWaitForServerBookshelf = HasAuthenticatedSessionForMainScene();
            var sync = cachedSyncController;
            if (sync == null || !sync.IsServerSyncEnabled)
            {
                return !shouldWaitForServerBookshelf;
            }

            if (!sync.HasAccessToken)
            {
                return !shouldWaitForServerBookshelf;
            }

            if (sync.HasCompletedInitialRefresh || initialBookshelfRefreshCompleted)
            {
                initialBookshelfRefreshFailed = !sync.LastInitialRefreshSucceeded;
                initialBookshelfRefreshMessage = sync.LastInitialRefreshMessage;
                return IsBookshelfSceneVisualReady(sync);
            }

            if (!hasRequestedInitialBookshelfRefresh)
            {
                hasRequestedInitialBookshelfRefresh = true;
                sync.WaitForInitialRefresh((succeeded, message) =>
                {
                    if (this == null)
                    {
                        return;
                    }

                    initialBookshelfRefreshCompleted = true;
                    initialBookshelfRefreshFailed = !succeeded;
                    initialBookshelfRefreshMessage = message;
                });
            }

            return false;
        }

        private bool IsBookshelfSceneVisualReady(BookshelfApiSyncController sync)
        {
            var generator = ResolveBookshelfGenerator();
            if (generator == null)
            {
                return false;
            }

            if (generator.IsBookshelfSwitching)
            {
                return false;
            }

            if (sync == null || !sync.IsServerSyncEnabled || !sync.HasAccessToken)
            {
                return true;
            }

            if (!sync.HasCompletedInitialRefresh && !initialBookshelfRefreshCompleted)
            {
                return false;
            }

            if (!sync.LastInitialRefreshSucceeded && !generator.HasReceivedServerSnapshot)
            {
                return true;
            }

            return generator.HasReceivedServerSnapshot;
        }

        private BookshelfGenerator ResolveBookshelfGenerator()
        {
            if (cachedBookshelfGenerator == null)
            {
                cachedBookshelfGenerator = FindFirstObjectByType<BookshelfGenerator>(FindObjectsInactive.Include);
            }

            return cachedBookshelfGenerator;
        }

        private static bool HasAuthenticatedSessionForMainScene()
        {
            return AuthSession.HasAccessToken ||
                   (APIManager.Instance != null && APIManager.Instance.HasAccessToken);
        }

        private void SetMessage(string message)
        {
            if (messageLabel != null && !string.IsNullOrWhiteSpace(message))
            {
                messageLabel.text = message;
            }
        }

        private void SetDetailMessage(string message)
        {
            if (detailLabel != null && !string.IsNullOrWhiteSpace(message))
            {
                detailLabel.text = message;
            }
        }

        private void SetLoadingStage(string title, string detail, float progress)
        {
            SetMessage(title);
            SetDetailMessage(detail);
            SetProgress(progress);
        }

        private void UpdateLoadingStage(float startTime)
        {
            var elapsed = Time.unscaledTime - startTime;
            var pulse = Mathf.PingPong(elapsed * 0.08f, 0.04f);

            if (SceneManager.GetActiveScene().name != SceneKeys.Main)
            {
                SetLoadingStage("방으로 들어가는 중입니다.", "조명을 켜고 가구를 정리하는 중입니다.", 0.14f + pulse);
                return;
            }

            if (cachedExperienceManager == null)
            {
                cachedExperienceManager = FindFirstObjectByType<BookshelfExperienceManager>(FindObjectsInactive.Include);
            }

            if (cachedExperienceManager == null)
            {
                SetLoadingStage("방을 꾸미는 중입니다.", "책장을 놓을 공간을 준비하는 중입니다.", 0.28f + pulse);
                return;
            }

            var generator = ResolveBookshelfGenerator();
            if (generator == null)
            {
                SetLoadingStage("책장을 놓는 중입니다.", "책장 시스템을 준비하는 중입니다.", 0.42f + pulse);
                return;
            }

            if (cachedSyncController == null)
            {
                cachedSyncController = FindFirstObjectByType<BookshelfApiSyncController>(FindObjectsInactive.Include);
            }

            if (!HasAuthenticatedSessionForMainScene())
            {
                SetLoadingStage("마지막으로 정리하는 중입니다.", "곧 방으로 이동합니다.", 0.82f + pulse);
                return;
            }

            var sync = cachedSyncController;
            if (sync == null || !sync.IsServerSyncEnabled || !sync.HasAccessToken)
            {
                SetLoadingStage("책장을 확인하는 중입니다.", "로그인 정보를 확인하는 중입니다.", 0.54f + pulse);
                return;
            }

            if (!sync.HasCompletedInitialRefresh && !initialBookshelfRefreshCompleted)
            {
                SetLoadingStage("책장을 불러오는 중입니다.", "내 책장 정보를 서버에서 가져오는 중입니다.", 0.64f + pulse);
                return;
            }

            if (!generator.HasReceivedServerSnapshot || generator.IsBookshelfSwitching)
            {
                SetLoadingStage("책을 꽂는 중입니다.", "책장과 책 배치를 화면에 반영하는 중입니다.", 0.84f + pulse);
                return;
            }

            SetLoadingStage("마지막으로 정리하는 중입니다.", "방에 들어가기 전 화면을 안정화하는 중입니다.", 0.9f);
        }

        private TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float fontSize, Color color, FontStyles style)
        {
            var labelObject = new GameObject(name, typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);

            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = style;
            label.textWrappingMode = TextWrappingModes.Normal;
            ApplyPreferredFont(label);
            return label;
        }

        private static void ApplyPreferredFont(TextMeshProUGUI label)
        {
            var preferred = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            for (var i = 0; i < preferred.Length; i++)
            {
                var candidate = preferred[i];
                if (candidate == null) continue;
                if (string.Equals(candidate.name, "NotoSansKR-Regular", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate.name, "NOTOSANSKR SDF", System.StringComparison.OrdinalIgnoreCase))
                {
                    label.font = candidate;
                    break;
                }
            }
        }

        private void SetProgress(float progress, bool immediate = false)
        {
            targetProgress = Mathf.Max(targetProgress, Mathf.Clamp01(progress));
            if (immediate)
            {
                displayedProgress = targetProgress;
                ApplyProgress(displayedProgress);
            }
        }

        private void TickProgress(bool immediate = false)
        {
            if (immediate)
            {
                displayedProgress = targetProgress;
            }
            else
            {
                displayedProgress = Mathf.MoveTowards(
                    displayedProgress,
                    targetProgress,
                    ProgressMoveSpeed * Time.unscaledDeltaTime);
            }

            ApplyProgress(displayedProgress);
        }

        private void ApplyProgress(float progress)
        {
            if (progressFillRect != null)
            {
                progressFillRect.sizeDelta = new Vector2(ProgressBarWidth * Mathf.Clamp01(progress), 0f);
            }

            if (percentLabel != null)
            {
                percentLabel.text = $"{Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f)}%";
            }
        }

        private string BuildInitialRefreshFailureMessage()
        {
            if (string.IsNullOrWhiteSpace(initialBookshelfRefreshMessage))
            {
                return "책장 정보를 불러오지 못했습니다.";
            }

            return $"책장 정보를 불러오지 못했습니다.\n{initialBookshelfRefreshMessage}";
        }
    }
}
