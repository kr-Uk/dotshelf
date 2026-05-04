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
    /// 로그인 후 저장된 accessToken 으로 7개 엔드포인트가 정상 응답/파싱되는지 확인하는 진단 러너.
    /// 빈 GameObject 에 부착 후 인스펙터의 ContextMenu("Run Smoke Test") 또는 runOnStart 토글로 실행.
    /// 실제 서비스 흐름과 동일한 APIManager 경로를 통과시키므로 Authorization 헤더 부착/envelope
    /// 파싱이 production code path 그대로 검증된다.
    ///
    /// **UNITY_EDITOR 전용** — 릴리즈/플레이어 빌드에는 포함되지 않는다.
    /// </summary>
    public sealed class ApiSmokeTestRunner : MonoBehaviour
    {
        [Header("실행 옵션")]
        [SerializeField] private bool runOnStart = false;

        // 기본값 false: 로그인 후 저장된 accessToken 을 그대로 써서 인증된 호출이 통과하는지 검증.
        // true 면 메모리 토큰만 비움 (PlayerPrefs/AuthSession 은 건드리지 않음).
        [SerializeField] private bool clearTokenBeforeRun = false;

        [SerializeField] private int bodyLogMaxChars = 1500;
        [SerializeField] private float perRequestTimeoutSec = 12f;

        private int total;
        private int ok2xx;
        private int authBlocked;
        private int otherFail;

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

        private IEnumerator RunAll()
        {
            APIManager api = ApiManagerProvider.EnsureInstance();
            if (api == null)
            {
                Debug.LogError("[ApiSmokeTest] APIManager 초기화 실패 — fallback GameObject 생성도 실패.");
                yield break;
            }

            // AddComponent 직후엔 Awake 가 동기 실행되긴 하지만 한 프레임 양보해 hydrate 정착시킴.
            yield return null;

            total = ok2xx = authBlocked = otherFail = 0;

            if (clearTokenBeforeRun)
            {
                api.SetAccessToken(null);
                Debug.Log("[ApiSmokeTest] clearTokenBeforeRun=true → in-memory accessToken cleared (PlayerPrefs 그대로).");
            }

            LogTokenStatus(api);

            yield return ProbeGet<UserProfileResponseDto>(
                "1 GET /v1/users/me", "/v1/users/me");

            yield return ProbeGet<SpaceResponseDto>(
                "2 GET /v1/spaces/me", "/v1/spaces/me");

            yield return ProbeGet<InventoryResponseDto>(
                "3 GET /v1/assets/me/inventory", "/v1/assets/me/inventory");

            // MR #98 review #2: 다른 사용자/세션과 충돌 안 나게 timestamp 부여. 50자 nickname
            // DB 제약 안 넘게 prefix 짧게.
            string smokeNickname = "smoketest_" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            yield return ProbePatch<UpdateProfileRequestDto, UserProfileResponseDto>(
                "4 PATCH /v1/users/me",
                "/v1/users/me",
                new UpdateProfileRequestDto
                {
                    Nickname = smokeNickname,
                    Introduction = "smoke-test ping",
                    ProfileImageUrl = string.Empty
                });

            yield return ProbePatch<VisibilityRequestDto, SpaceResponseDto>(
                "5 PATCH /v1/spaces/me/visibility",
                "/v1/spaces/me/visibility",
                new VisibilityRequestDto { VisibilityScope = "PUBLIC" });

            yield return ProbeGet<NotificationSettingResponseDto>(
                "6 GET /v1/users/me/notification-settings",
                "/v1/users/me/notification-settings");

            yield return ProbePatch<NotificationSettingRequestDto, NotificationSettingResponseDto>(
                "7 PATCH /v1/users/me/notification-settings",
                "/v1/users/me/notification-settings",
                new NotificationSettingRequestDto { NotifyNewGuestbook = true });

            PrintSummary();
        }

        private IEnumerator ProbeGet<T>(string label, string path)
        {
            APIManager api = APIManager.Instance;
            bool authAttached = api.HasAccessToken;

            bool done = false;
            bool success = false;
            long status = 0;
            string body = null;
            ApiEnvelopeDto<T> envelope = null;
            string envParseStatus = "(not attempted)";

            api.Get<ApiEnvelopeDto<T>>(
                path,
                e =>
                {
                    done = true;
                    success = true;
                    envelope = e;
                    body = SafeSerialize(e);
                    envParseStatus = ClassifyEnvelope(e);
                },
                err =>
                {
                    done = true;
                    status = err != null ? err.StatusCode : 0;
                    body = err != null ? err.Body : null;
                    envParseStatus = TryParseEnvelopeFromBody<T>(body);
                });

            yield return WaitForDone(() => done, label);
            LogResult(label, "GET", path, authAttached, success, status, body, envParseStatus);
        }

        private IEnumerator ProbePatch<TRequest, TResult>(string label, string path, TRequest request)
        {
            APIManager api = APIManager.Instance;
            bool authAttached = api.HasAccessToken;

            bool done = false;
            bool success = false;
            long status = 0;
            string body = null;
            ApiEnvelopeDto<TResult> envelope = null;
            string envParseStatus = "(not attempted)";

            api.Patch<TRequest, ApiEnvelopeDto<TResult>>(
                path,
                request,
                e =>
                {
                    done = true;
                    success = true;
                    envelope = e;
                    body = SafeSerialize(e);
                    envParseStatus = ClassifyEnvelope(e);
                },
                err =>
                {
                    done = true;
                    status = err != null ? err.StatusCode : 0;
                    body = err != null ? err.Body : null;
                    envParseStatus = TryParseEnvelopeFromBody<TResult>(body);
                });

            yield return WaitForDone(() => done, label);
            LogResult(label, "PATCH", path, authAttached, success, status, body, envParseStatus);
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

        private void LogResult(
            string label,
            string method,
            string path,
            bool authAttached,
            bool success,
            long status,
            string body,
            string envParseStatus)
        {
            total++;
            string assumedUrl = ApiEndpointDefaults.DefaultBaseUrl.TrimEnd('/') + path;

            string statusStr;
            if (success)
            {
                statusStr = "2xx (onSuccess)";
                ok2xx++;
            }
            else if (status == 401 || status == 403)
            {
                statusStr = status.ToString();
                authBlocked++;
            }
            else
            {
                statusStr = status > 0 ? status.ToString() : "(no response)";
                otherFail++;
            }

            string authLine = authAttached
                ? "attached (Bearer " + MaskToken(ReadStoredToken()) + ")"
                : "NOT attached (no token)";

            string trimmedBody = TrimBody(body);

            string line =
                $"[ApiSmokeTest] {label}\n" +
                $"  METHOD: {method}\n" +
                $"  URL: {assumedUrl}\n" +
                $"  AUTH: {authLine}\n" +
                $"  STATUS: {statusStr}\n" +
                $"  BODY: {trimmedBody}\n" +
                $"  PARSE: {envParseStatus}";
            Debug.Log(line);

            if (status == 401 || status == 403)
            {
                Debug.LogWarning(
                    $"[ApiSmokeTest][AUTH-FAIL] {label}: status={status} — accessToken 누락/만료/무효 또는 BE 권한 정책 불일치.");
            }
        }

        private void PrintSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[ApiSmokeTest] ===== Summary =====");
            sb.AppendLine($"  total={total}  ok={ok2xx}  auth-blocked={authBlocked}  other-fail={otherFail}");
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
            if (string.IsNullOrEmpty(token))
            {
                return "(none)";
            }
            if (token.Length <= 12)
            {
                return $"***(len={token.Length})";
            }
            return $"{token.Substring(0, 8)}...{token.Substring(token.Length - 4)} (len={token.Length})";
        }

        private static string SafeSerialize(object obj)
        {
            if (obj == null)
            {
                return "(null)";
            }
            try
            {
                return JsonConvert.SerializeObject(obj);
            }
            catch (Exception ex)
            {
                return $"(serialize error: {ex.Message})";
            }
        }

        private static string ClassifyEnvelope<T>(ApiEnvelopeDto<T> envelope)
        {
            if (envelope == null)
            {
                return "envelope-null";
            }
            if (!envelope.Succeeded)
            {
                return $"envelope-fail (code={envelope.Code})";
            }
            if (envelope.Result == null)
            {
                return "envelope-ok-result-null";
            }
            return "envelope-ok-result-parsed";
        }

        private static string TryParseEnvelopeFromBody<T>(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return "(empty body)";
            }
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

        private string TrimBody(string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return "(empty)";
            }
            if (bodyLogMaxChars <= 0 || body.Length <= bodyLogMaxChars)
            {
                return body;
            }
            return body.Substring(0, bodyLogMaxChars) + $"... (truncated, total {body.Length} chars)";
        }
    }
}
#endif
