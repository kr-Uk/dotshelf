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
    // player never sees a half-loaded "default scene" between Login → Main.
    //
    // Onboarding step 3 has its own dedicated loading cover (driven by
    // OnboardingScreenController) so this cover is only used by the regular
    // login / guest / social-login paths that go straight to main.
    public sealed class MainSceneTransitionCover : MonoBehaviour
    {
        private const int CanvasSortingOrder = 7000;
        private const float MinCoverDurationSeconds = 0.25f;
        private const float MaxCoverDurationSeconds = 12f;
        private const float SlowLoadingStatusSeconds = 4f;
        private const float FailureNoticeDurationSeconds = 1.25f;

        private static readonly Color BackgroundColor = new Color(0.96f, 0.94f, 0.90f, 1f);
        private static readonly Color TextColor = new Color(0.18f, 0.14f, 0.09f, 1f);
        private bool hasRequestedInitialBookshelfRefresh;
        private bool initialBookshelfRefreshCompleted;
        private bool initialBookshelfRefreshFailed;
        private string initialBookshelfRefreshMessage;
        private bool slowLoadingStatusShown;
        private TextMeshProUGUI messageLabel;
        private BookshelfExperienceManager cachedExperienceManager;
        private BookshelfApiSyncController cachedSyncController;

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

            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(canvasObject.transform, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(900f, 200f);
            labelRect.anchoredPosition = Vector2.zero;

            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = message;
            label.fontSize = 32f;
            label.color = TextColor;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.textWrappingMode = TextWrappingModes.Normal;

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

            messageLabel = label;
        }

        // Stays visible until: MainScene is the active scene, the bookshelf
        // experience manager has been instantiated, and the first bookshelf API
        // refresh has completed or intentionally skipped. A short
        // minimum-duration prevents flicker on very fast loads; a generous
        // max-duration is just a safety net so a stuck load eventually clears
        // the cover instead of trapping the player.
        private IEnumerator WaitForMainSceneAndDestroy()
        {
            var startTime = Time.unscaledTime;
            var deadline = startTime + MaxCoverDurationSeconds;
            var readyForReveal = false;

            while (Time.unscaledTime < deadline)
            {
                var mainSceneReady = IsMainSceneReadyForReveal();
                var minDurationElapsed = (Time.unscaledTime - startTime) >= MinCoverDurationSeconds;

                if (mainSceneReady && minDurationElapsed)
                {
                    readyForReveal = true;
                    if (initialBookshelfRefreshFailed)
                    {
                        SetMessage(BuildInitialRefreshFailureMessage());
                        yield return new WaitForSecondsRealtime(FailureNoticeDurationSeconds);
                    }

                    break;
                }

                if (!slowLoadingStatusShown && (Time.unscaledTime - startTime) >= SlowLoadingStatusSeconds)
                {
                    slowLoadingStatusShown = true;
                    SetMessage("책장 정보를 불러오는 중입니다.\n잠시만 기다려 주세요.");
                }

                yield return null;
            }

            if (!readyForReveal && Time.unscaledTime >= deadline)
            {
                SetMessage("책장 로딩이 지연되어 메인 화면으로 이동합니다.\n잠시 후 다시 동기화됩니다.");
                yield return new WaitForSecondsRealtime(FailureNoticeDurationSeconds);
            }

            // One more end-of-frame so the camera has actually rendered the
            // scene before we tear down the cover — otherwise the player sees
            // one frame of unlit / un-cleared backbuffer.
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
                return true;
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

        private string BuildInitialRefreshFailureMessage()
        {
            if (string.IsNullOrWhiteSpace(initialBookshelfRefreshMessage))
            {
                return "책장 정보를 불러오지 못했습니다.\n메인 화면으로 이동합니다.";
            }

            return $"책장 정보를 불러오지 못했습니다.\n{initialBookshelfRefreshMessage}\n메인 화면으로 이동합니다.";
        }
    }
}
