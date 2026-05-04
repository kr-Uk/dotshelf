// MR #98 review #2: production 빌드 포함 + smoke test 사용자 데이터 변경 위험 차단.
// 클래스 전체를 UNITY_EDITOR 가드 — 릴리즈 빌드에서는 컴파일 자체에서 제외되어 reflection
// 으로도 도달 불가. dev 환경에서만 진단 도구로 사용.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Text;
using App.Domain;
using App.Domain.Auth;
using App.Infrastructure.Api;
using App.Infrastructure.Api.Dtos;
using Newtonsoft.Json;
using UnityEngine;

namespace App.Bootstrap
{
    /// <summary>
    /// 로그인 후 저장된 accessToken 으로 미확인 엔드포인트가 정상 응답/파싱되는지 확인하는 진단 러너.
    /// 빈 GameObject 에 부착 후 인스펙터의 ContextMenu 로 실행.
    ///
    /// 실제 서비스 흐름과 동일한 APIManager 경로(Authorization, baseUrl, envelope 파싱) 를 통과시킨다.
    ///
    /// **UNITY_EDITOR 전용** — 릴리즈/플레이어 빌드에는 포함되지 않는다.
    /// **DELETE /v1/users/me 는 절대 포함하지 않는다** (회원 탈퇴, irreversible).
    ///
    /// ContextMenu 분리:
    ///  - "Run Smoke Test"           : 일반 9개 테스트 (토큰 영향 없음)
    ///  - "Run Token Refresh"        : POST /v1/auth/token/refresh — 토큰 갱신 (현재 토큰 교체됨)
    ///  - "Run Logout"               : POST /v1/auth/logout       — 토큰 무효화 (재로그인 필요)
    /// </summary>
    public sealed class ApiSmokeTestRunner : MonoBehaviour
    {
        [Header("실행 옵션")]
        [SerializeField] private bool runOnStart = false;

        // 기본값 false: 로그인 후 저장된 accessToken 을 그대로 써서 인증된 호출이 통과하는지 검증.
        [SerializeField] private bool clearTokenBeforeRun = false;

        [SerializeField] private int bodyLogMaxChars = 1500;
        [SerializeField] private float perRequestTimeoutSec = 12f;

        [Header("테스트 파라미터 — 비워두면 해당 테스트 skip")]
        [Tooltip("타인 또는 본인 spaceId. GET /v1/spaces/{spaceId}, visits, like/unlike 에 사용.")]
        [SerializeField] private long testSpaceId;

        [Tooltip("타인 userId. POST/DELETE /v1/users/{userId}/follow 에 사용.")]
        [SerializeField] private long testUserId;

        [Tooltip("내 inventoryId. POST /v1/spaces/me/theme/{inventoryId}/apply 에 사용.")]
        [SerializeField] private long testInventoryId;

        [Tooltip("theme apply mode. 일반적으로 RESET. KEEP_BLOCKERS_ONLY 는 BE 가 501.")]
        [SerializeField] private string testThemeMode = "RESET";

        private int total;
        private int ok2xx;
        private int authBlocked;
        private int otherFail;
        private int skipped;

        private void Start()
        {
            if (runOnStart)
            {
                StartCoroutine(RunAll());
            }
        }

        [ContextMenu("Run Smoke Test")]
        public void RunFromInspector()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                Debug.LogWarning("[ApiSmokeTest] Editor Play 모드에서만 실행됩니다.");
                return;
            }
            StartCoroutine(RunAll());
        }

        [ContextMenu("Run Token Refresh (분리)")]
        public void RunTokenRefreshFromInspector()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                Debug.LogWarning("[ApiSmokeTest] Editor Play 모드에서만 실행됩니다.");
                return;
            }
            StartCoroutine(RunTokenRefresh());
        }

        [ContextMenu("Run Logout (분리)")]
        public void RunLogoutFromInspector()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                Debug.LogWarning("[ApiSmokeTest] Editor Play 모드에서만 실행됩니다.");
                return;
            }
            StartCoroutine(RunLogout());
        }

        // ─────────────────────────────────────────────────────────────────────
        // 일반 테스트 (토큰 비파괴)
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator RunAll()
        {
            APIManager api = ApiManagerProvider.EnsureInstance();
            if (api == null)
            {
                Debug.LogError("[ApiSmokeTest] APIManager 초기화 실패.");
                yield break;
            }
            yield return null;

            total = ok2xx = authBlocked = otherFail = skipped = 0;

            if (clearTokenBeforeRun)
            {
                api.SetAccessToken(null);
                Debug.Log("[ApiSmokeTest] clearTokenBeforeRun=true → in-memory accessToken cleared.");
            }

            LogTokenStatus(api);

            // 1. GET /v1/assets/me/inventory
            yield return ProbeGet<InventoryResponseDto>(
                "1 GET /v1/assets/me/inventory",
                "/v1/assets/me/inventory");

            // 2. POST /v1/spaces/me/theme/{inventoryId}/apply (BE 의 실제 POST 경로 — 사용자 요청 표기는
            //    "POST /v1/spaces/me/theme" 였지만 BE 는 inventoryId path 를 받음. testInventoryId 필요)
            if (testInventoryId <= 0)
            {
                LogSkip("2 POST /v1/spaces/me/theme/{inventoryId}/apply", "testInventoryId 미입력");
            }
            else
            {
                string themePath =
                    $"/v1/spaces/me/theme/{testInventoryId}/apply" +
                    $"?mode={Uri.EscapeDataString(string.IsNullOrWhiteSpace(testThemeMode) ? "RESET" : testThemeMode)}";
                yield return ProbePost<SpaceMutationResponseDto>(
                    $"2 POST {themePath}",
                    themePath);
            }

            // 3. PATCH /v1/users/me/notification-settings
            yield return ProbePatch<NotificationSettingRequestDto, NotificationSettingResponseDto>(
                "3 PATCH /v1/users/me/notification-settings",
                "/v1/users/me/notification-settings",
                new NotificationSettingRequestDto { NotifyNewGuestbook = true });

            // 4. GET /v1/spaces/{spaceId}
            if (testSpaceId <= 0)
            {
                LogSkip("4 GET /v1/spaces/{spaceId}", "testSpaceId 미입력");
            }
            else
            {
                yield return ProbeGet<SpaceResponseDto>(
                    $"4 GET /v1/spaces/{testSpaceId}",
                    $"/v1/spaces/{testSpaceId}");
            }

            // 5. POST /v1/spaces/{spaceId}/visits
            if (testSpaceId <= 0)
            {
                LogSkip("5 POST /v1/spaces/{spaceId}/visits", "testSpaceId 미입력");
            }
            else
            {
                yield return ProbePost<ToggleResponseDto>(
                    $"5 POST /v1/spaces/{testSpaceId}/visits",
                    $"/v1/spaces/{testSpaceId}/visits");
            }

            // 6. POST /v1/spaces/{spaceId}/like
            if (testSpaceId <= 0)
            {
                LogSkip("6 POST /v1/spaces/{spaceId}/like", "testSpaceId 미입력");
            }
            else
            {
                yield return ProbePost<ToggleResponseDto>(
                    $"6 POST /v1/spaces/{testSpaceId}/like",
                    $"/v1/spaces/{testSpaceId}/like");
            }

            // 7. DELETE /v1/spaces/{spaceId}/like
            if (testSpaceId <= 0)
            {
                LogSkip("7 DELETE /v1/spaces/{spaceId}/like", "testSpaceId 미입력");
            }
            else
            {
                yield return ProbeDelete<ToggleResponseDto>(
                    $"7 DELETE /v1/spaces/{testSpaceId}/like",
                    $"/v1/spaces/{testSpaceId}/like");
            }

            // 8. POST /v1/users/{userId}/follow
            if (testUserId <= 0)
            {
                LogSkip("8 POST /v1/users/{userId}/follow", "testUserId 미입력");
            }
            else
            {
                yield return ProbePost<ToggleResponseDto>(
                    $"8 POST /v1/users/{testUserId}/follow",
                    $"/v1/users/{testUserId}/follow");
            }

            // 9. DELETE /v1/users/{userId}/follow
            if (testUserId <= 0)
            {
                LogSkip("9 DELETE /v1/users/{userId}/follow", "testUserId 미입력");
            }
            else
            {
                yield return ProbeDelete<ToggleResponseDto>(
                    $"9 DELETE /v1/users/{testUserId}/follow",
                    $"/v1/users/{testUserId}/follow");
            }

            PrintSummary();
        }

        // ─────────────────────────────────────────────────────────────────────
        // 토큰 영향 작업 — 별도 ContextMenu 로 격리
        // ─────────────────────────────────────────────────────────────────────

        // POST /v1/auth/token/refresh — refreshToken 으로 새 accessToken 발급. 응답 success 시
        // APIManager 가 자체적으로 토큰 교체하지는 않음(이 러너에선 단지 응답 검증만). 실서비스
        // 흐름은 SessionRefresh 가 담당.
        private IEnumerator RunTokenRefresh()
        {
            APIManager api = ApiManagerProvider.EnsureInstance();
            if (api == null)
            {
                Debug.LogError("[ApiSmokeTest][refresh] APIManager 초기화 실패.");
                yield break;
            }
            yield return null;

            string refreshToken = PlayerPrefs.GetString(AuthStorageKeys.RefreshToken, string.Empty);
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                Debug.LogWarning("[ApiSmokeTest][refresh] PlayerPrefs 에 refreshToken 없음 — 로그인 먼저.");
                yield break;
            }

            Debug.Log(
                $"[ApiSmokeTest][refresh] 시작 — masked refreshToken={MaskToken(refreshToken)}. " +
                "이 호출은 PlayerPrefs/APIManager 토큰 자체는 건드리지 않음 (응답만 로그).");

            yield return ProbePost<TokenRefreshRequestDto, AuthTokenResponseDto>(
                "TOKEN-REFRESH POST /v1/auth/token/refresh",
                "/v1/auth/token/refresh",
                new TokenRefreshRequestDto { RefreshToken = refreshToken });
        }

        // POST /v1/auth/logout — refreshToken 무효화. 응답 후 다시 로그인 필요.
        // 이 러너 자체는 PlayerPrefs/APIManager 토큰을 정리하지 않음 — 호출 효과만 검증.
        // 실서비스 로그아웃 흐름은 SettingsScreenController.Logout 이 담당 (AuthSession.Clear 등).
        private IEnumerator RunLogout()
        {
            APIManager api = ApiManagerProvider.EnsureInstance();
            if (api == null)
            {
                Debug.LogError("[ApiSmokeTest][logout] APIManager 초기화 실패.");
                yield break;
            }
            yield return null;

            string refreshToken = PlayerPrefs.GetString(AuthStorageKeys.RefreshToken, string.Empty);
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                Debug.LogWarning("[ApiSmokeTest][logout] PlayerPrefs 에 refreshToken 없음 — 로그인 먼저.");
                yield break;
            }

            Debug.LogWarning(
                "[ApiSmokeTest][logout] 시작 — 이 호출 후 BE 의 refreshToken 이 무효화됨. " +
                "다시 정상 흐름을 쓰려면 재로그인 필요.");

            yield return ProbePost<LogoutRequestDto, VoidResponseDto>(
                "LOGOUT POST /v1/auth/logout",
                "/v1/auth/logout",
                new LogoutRequestDto { RefreshToken = refreshToken });
        }

        // ─────────────────────────────────────────────────────────────────────
        // Probe helpers
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator ProbeGet<T>(string label, string path)
        {
            APIManager api = APIManager.Instance;
            bool authAttached = api != null && api.HasAccessToken;
            bool done = false;
            bool success = false;
            long status = 0;
            string body = null;
            string envParse = "(not attempted)";

            api.Get<ApiEnvelopeDto<T>>(
                path,
                e =>
                {
                    done = true; success = true;
                    body = SafeSerialize(e);
                    envParse = ClassifyEnvelope(e);
                },
                err =>
                {
                    done = true;
                    status = err != null ? err.StatusCode : 0;
                    body = err != null ? err.Body : null;
                    envParse = TryParseEnvelopeFromBody<T>(body);
                });

            yield return WaitForDone(() => done, label);
            LogResult(label, "GET", path, authAttached, success, status, body, envParse);
        }

        private IEnumerator ProbePost<T>(string label, string path)
        {
            APIManager api = APIManager.Instance;
            bool authAttached = api != null && api.HasAccessToken;
            bool done = false;
            bool success = false;
            long status = 0;
            string body = null;
            string envParse = "(not attempted)";

            api.Post<ApiEnvelopeDto<T>>(
                path,
                e =>
                {
                    done = true; success = true;
                    body = SafeSerialize(e);
                    envParse = ClassifyEnvelope(e);
                },
                err =>
                {
                    done = true;
                    status = err != null ? err.StatusCode : 0;
                    body = err != null ? err.Body : null;
                    envParse = TryParseEnvelopeFromBody<T>(body);
                });

            yield return WaitForDone(() => done, label);
            LogResult(label, "POST", path, authAttached, success, status, body, envParse);
        }

        private IEnumerator ProbePost<TRequest, TResponse>(string label, string path, TRequest request)
        {
            APIManager api = APIManager.Instance;
            bool authAttached = api != null && api.HasAccessToken;
            bool done = false;
            bool success = false;
            long status = 0;
            string body = null;
            string envParse = "(not attempted)";

            api.Post<TRequest, ApiEnvelopeDto<TResponse>>(
                path,
                request,
                e =>
                {
                    done = true; success = true;
                    body = SafeSerialize(e);
                    envParse = ClassifyEnvelope(e);
                },
                err =>
                {
                    done = true;
                    status = err != null ? err.StatusCode : 0;
                    body = err != null ? err.Body : null;
                    envParse = TryParseEnvelopeFromBody<TResponse>(body);
                });

            yield return WaitForDone(() => done, label);
            LogResult(label, "POST", path, authAttached, success, status, body, envParse);
        }

        private IEnumerator ProbePatch<TRequest, TResult>(string label, string path, TRequest request)
        {
            APIManager api = APIManager.Instance;
            bool authAttached = api != null && api.HasAccessToken;
            bool done = false;
            bool success = false;
            long status = 0;
            string body = null;
            string envParse = "(not attempted)";

            api.Patch<TRequest, ApiEnvelopeDto<TResult>>(
                path,
                request,
                e =>
                {
                    done = true; success = true;
                    body = SafeSerialize(e);
                    envParse = ClassifyEnvelope(e);
                },
                err =>
                {
                    done = true;
                    status = err != null ? err.StatusCode : 0;
                    body = err != null ? err.Body : null;
                    envParse = TryParseEnvelopeFromBody<TResult>(body);
                });

            yield return WaitForDone(() => done, label);
            LogResult(label, "PATCH", path, authAttached, success, status, body, envParse);
        }

        private IEnumerator ProbeDelete<T>(string label, string path)
        {
            APIManager api = APIManager.Instance;
            bool authAttached = api != null && api.HasAccessToken;
            bool done = false;
            bool success = false;
            long status = 0;
            string body = null;
            string envParse = "(not attempted)";

            api.Delete<ApiEnvelopeDto<T>>(
                path,
                e =>
                {
                    done = true; success = true;
                    body = SafeSerialize(e);
                    envParse = ClassifyEnvelope(e);
                },
                err =>
                {
                    done = true;
                    status = err != null ? err.StatusCode : 0;
                    body = err != null ? err.Body : null;
                    envParse = TryParseEnvelopeFromBody<T>(body);
                });

            yield return WaitForDone(() => done, label);
            LogResult(label, "DELETE", path, authAttached, success, status, body, envParse);
        }

        private IEnumerator WaitForDone(Func<bool> isDone, string label)
        {
            float t = 0f;
            while (!isDone() && t < perRequestTimeoutSec)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!isDone())
            {
                Debug.LogWarning($"[ApiSmokeTest] {label}: timeout {perRequestTimeoutSec:0.0}s — 응답 없음.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 로깅
        // ─────────────────────────────────────────────────────────────────────

        private void LogSkip(string label, string reason)
        {
            total++;
            skipped++;
            Debug.LogWarning($"[ApiSmokeTest] {label} → SKIP ({reason}).");
        }

        private void LogResult(
            string label,
            string method,
            string path,
            bool authAttached,
            bool success,
            long status,
            string body,
            string envParse)
        {
            total++;
            string assumedUrl = ApiEndpointDefaults.DefaultBaseUrl.TrimEnd('/') + path;

            string statusStr;
            string verdict;
            if (success)
            {
                statusStr = "2xx (onSuccess)";
                verdict = "OK";
                ok2xx++;
            }
            else if (status == 401 || status == 403)
            {
                statusStr = status.ToString();
                verdict = "AUTH-FAIL";
                authBlocked++;
            }
            else if (status >= 400 && status < 500)
            {
                statusStr = status.ToString();
                verdict = $"4xx 클라이언트 에러 — 요청 파라미터/대상 리소스 점검 필요";
                otherFail++;
            }
            else if (status >= 500)
            {
                statusStr = status.ToString();
                verdict = "5xx 서버 에러 — BE 로그 확인 필요";
                otherFail++;
            }
            else
            {
                statusStr = status > 0 ? status.ToString() : "(no response)";
                verdict = "FAIL (network/timeout 등)";
                otherFail++;
            }

            string authLine = authAttached
                ? "attached (Bearer " + MaskToken(ReadStoredToken()) + ")"
                : "NOT attached";

            string trimmedBody = ApiManagerProvider.TrimForLog(body, bodyLogMaxChars);

            string line =
                $"[ApiSmokeTest] {label}\n" +
                $"  METHOD: {method}\n" +
                $"  URL: {assumedUrl}\n" +
                $"  AUTH: {authLine}\n" +
                $"  STATUS: {statusStr}  → {verdict}\n" +
                $"  BODY: {trimmedBody}\n" +
                $"  PARSE: {envParse}";

            if (success)
            {
                Debug.Log(line);
            }
            else
            {
                Debug.LogWarning(line);
            }
        }

        private void PrintSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[ApiSmokeTest] ===== Summary =====");
            sb.AppendLine($"  total={total}  ok={ok2xx}  auth-blocked={authBlocked}  other-fail={otherFail}  skipped={skipped}");
            sb.AppendLine($"  testSpaceId={testSpaceId}  testUserId={testUserId}  testInventoryId={testInventoryId}");
            Debug.Log(sb.ToString());
        }

        private void LogTokenStatus(APIManager api)
        {
            string raw = ReadStoredToken();
            bool hasPrefs = !string.IsNullOrWhiteSpace(raw);
            Debug.Log(
                $"[ApiSmokeTest] token state: prefs={(hasPrefs ? "present" : "absent")} " +
                $"masked={MaskToken(raw)} | api.HasAccessToken={api.HasAccessToken}");
        }

        private static string ReadStoredToken()
        {
            return PlayerPrefs.GetString(AuthStorageKeys.AccessToken, string.Empty);
        }

        private static string MaskToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return "(none)";
            if (token.Length <= 12) return $"***(len={token.Length})";
            return $"{token.Substring(0, 8)}...{token.Substring(token.Length - 4)} (len={token.Length})";
        }

        private static string SafeSerialize(object obj)
        {
            if (obj == null) return "(null)";
            try { return JsonConvert.SerializeObject(obj); }
            catch (Exception ex) { return $"(serialize error: {ex.Message})"; }
        }

        private static string ClassifyEnvelope<T>(ApiEnvelopeDto<T> envelope)
        {
            if (envelope == null) return "envelope-null";
            if (!envelope.Succeeded) return $"envelope-fail (code={envelope.Code})";
            if (envelope.Result == null) return "envelope-ok-result-null";
            return "envelope-ok-result-parsed";
        }

        private static string TryParseEnvelopeFromBody<T>(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return "(empty body)";
            try
            {
                ApiEnvelopeDto<T> env = JsonConvert.DeserializeObject<ApiEnvelopeDto<T>>(body);
                return ClassifyEnvelope(env);
            }
            catch (Exception ex)
            {
                return $"PARSE-ERROR: {ex.Message}";
            }
        }
    }
}
#endif
