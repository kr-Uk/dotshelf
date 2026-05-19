using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using App.Domain;
using App.Domain.Auth;
using App.Infrastructure.Api.Dtos;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace App.Infrastructure.Api
{
    public sealed class ApiHealthCheckResult
    {
        public string Url { get; set; }
        public bool IsReachable { get; set; }
        public bool IsHealthy { get; set; }
        public bool RequiresAuthentication { get; set; }
        public long StatusCode { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
        public string Body { get; set; }
    }

    public sealed class APIManager : MonoBehaviour
    {
        private const int ErrorResponseBodyLogLimit = 512;
        private const long UnauthorizedStatusCode = 401L;

        [SerializeField] private string baseUrl = ApiEndpointDefaults.DefaultBaseUrl;
        [SerializeField] private string accessToken;
        [SerializeField] private int timeoutSeconds = 10;
        [SerializeField] private bool enableVerboseLog;
        private long currentUserId;
        private bool tokenRefreshInFlight;
        private readonly List<Action<bool>> tokenRefreshWaiters = new List<Action<bool>>();

        public static APIManager Instance { get; private set; }
        public bool HasAccessToken => !string.IsNullOrWhiteSpace(accessToken);
        public bool EnableVerboseLog => enableVerboseLog;
        public long CurrentUserId => currentUserId;

        private void Awake()
        {
            // 회귀 방지망 — APIManager 가 다른 시스템 prefab (예: RuntimeSystems → 'BookshelfSystems')
            // 의 root 에 attach 되면 DontDestroyOnLoad(gameObject) 가 root 전체를 DDOL 로 이동시켜
            // scene reload 시 BookshelfExperienceManager / state / canvas 등 옛 인스턴스가 통째로
            // 살아남는다 (재로그인 회귀: 옛 BEM 의 stale SerializeField → MissingReferenceException +
            // 책장 오버레이 button 비표시 + dashboard 빈 액자). 정상 prefab 구성은 APIManager 를
            // 단독 host GameObject 에 두는 것 (RuntimeSystems prefab 에서 child 로 분리). 본 가드는
            // 그 구성이 미래에 회귀해도 자동으로 DDOL/Destroy 영향을 root 로 확대하지 않도록 한다 —
            // 컴포넌트 수가 Transform + APIManager (== 2) 면 단독 host, 그 외는 co-located.
            var componentCount = GetComponents<Component>().Length;
            var isCoLocatedWithOtherSystems = componentCount > 2;

            // duplicate 처리 — 이미 살아있는 Instance 가 있으면 자기 자신을 제거.
            // 단독 host duplicate 라면 GameObject 통째로 destroy 안전. co-located 라면 component 만
            // destroy 해서 root 의 다른 시스템 (BEM/state/canvas 등) 을 보호 (review Finding 2 반영).
            if (Instance != null && Instance != this)
            {
                if (isCoLocatedWithOtherSystems)
                {
                    Debug.LogWarning(
                        $"[APIManager] duplicate on co-located host '{gameObject.name}' " +
                        $"(componentCount={componentCount}) — component-only destroy 로 root 보호. " +
                        "Prefab 분리 회귀 의심 — 정상 구성은 APIManager 단독 host GameObject 입니다.");
                    Destroy(this);
                }
                else
                {
                    Destroy(gameObject);
                }
                return;
            }

            Instance = this;

            // co-located 면 DDOL skip — scene 라이프사이클 그대로. 다음 scene 진입 시 새 APIManager
            // 가 prefab 의 일부로 다시 instantiated 되고 HydrateAccessTokenFromAuthSession 가
            // PlayerPrefs 에서 token 복구.
            // 단독 host 면 DDOL — 단 DDOL 은 root GameObject 만 받으므로 child 로 attach 되어 있으면
            // 먼저 root 로 분리 (review Finding 1 반영). prefab 에서 child host 로 두어도 본 가드가
            // 자동으로 root 분리 + DDOL 보장.
            if (!isCoLocatedWithOtherSystems)
            {
                if (transform.parent != null)
                {
                    transform.SetParent(null, worldPositionStays: true);
                }
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Debug.LogWarning(
                    $"[APIManager] DDOL skip — co-located with other systems on '{gameObject.name}' " +
                    $"(componentCount={componentCount}). Prefab 분리 회귀 의심 — 정상 구성은 APIManager " +
                    "단독 host GameObject 입니다.");
            }

            // dev/prod 혼재만 교정 — prefab 의 SerializeField 가 빌드 모드와 정반대인 known endpoint 일 때만 덮어씀.
            // 의도: editor/Dev build 인데 prefab 에 ProdBaseUrl 이 박혀 있으면 (이번 버그 케이스) → DevBaseUrl 로 교정.
            //       Release build 인데 prefab 에 DevBaseUrl 이 박혀 있으면 → ProdBaseUrl 로 교정.
            // 보존: localhost / staging / 개인 터널 / 임시 QA 서버 등 known endpoint 외 값은 그대로 둬서 수동 테스트 경로 유지.
            // (MR 111 review — skh1205 #2: 모든 mismatch 강제 덮어쓰면 로컬/스테이징 테스트 깨짐)
            string oppositeBaseUrl = string.Equals(ApiEndpointDefaults.DefaultBaseUrl, ApiEndpointDefaults.DevBaseUrl, StringComparison.Ordinal)
                ? ApiEndpointDefaults.ProdBaseUrl
                : ApiEndpointDefaults.DevBaseUrl;
            if (string.Equals(baseUrl, oppositeBaseUrl, StringComparison.Ordinal))
            {
                Debug.LogWarning(
                    $"[APIManager] Awake: prefab baseUrl='{baseUrl}' (build mode 와 정반대 endpoint). " +
                    $"build default='{ApiEndpointDefaults.DefaultBaseUrl}' 로 강제 정렬 (dev/prod 혼재 가드).");
                baseUrl = ApiEndpointDefaults.DefaultBaseUrl;
            }

            HydrateAccessTokenFromAuthSession();
        }

        private void OnDestroy()
        {
            // DDOL 안 한 경로 (co-located with other systems) 에서 scene reload 시 static Instance
            // 가 destroyed Unity object 를 가리키는 fake-null 로 남는다. `APIManager.Instance != null`
            // 비교는 Unity == operator 가 fake-null 을 인식하지만 C# null-conditional (`?.`) 은
            // 인식 못 해 destroyed reference 에 메서드를 호출 → MissingReferenceException. 명시 클리어.
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void HydrateAccessTokenFromAuthSession()
        {
            currentUserId = ReadStoredUserId();
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                return;
            }

            var stored = PlayerPrefs.GetString(AuthStorageKeys.AccessToken, string.Empty);
            if (!string.IsNullOrWhiteSpace(stored))
            {
                accessToken = stored;
            }
        }

        /// <summary>
        /// 현재 사용 중인 API base URL (예: "https://dev-api.dotshelf.digital"). 결제 페이지 같이 BE 가
        /// relative path ("/v1/payments/inicis/request/{orderId}") 만 응답하는 경우 외부 브라우저에
        /// 절대 URL 로 전달하기 위해 PaymentApiClient 등에서 사용.
        /// </summary>
        public string BaseUrl => baseUrl;

        public void Configure(string apiBaseUrl, string token = null, int timeout = 10, bool verboseLog = false)
        {
            baseUrl = NormalizeBaseUrl(apiBaseUrl, baseUrl);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                HydrateAccessTokenFromAuthSession();
            }

            // Only override the access token when a non-empty value is supplied.
            // AppApiConfig.ResolveAccessToken() returns string.Empty when no env var
            // and no serialized token are set, which would otherwise erase a token
            // hydrated from PlayerPrefs after a successful social/guest login.
            if (!string.IsNullOrWhiteSpace(token))
            {
                accessToken = token;
                currentUserId = ReadStoredUserId();
            }

            timeoutSeconds = Mathf.Max(1, timeout);
            enableVerboseLog = verboseLog;
        }

        public void SetAccessToken(string token)
        {
            accessToken = token;
            currentUserId = string.IsNullOrWhiteSpace(token) ? 0L : ReadStoredUserId();
        }

        private static long ReadStoredUserId()
        {
            var raw = PlayerPrefs.GetString(AuthStorageKeys.UserId, string.Empty);
            return long.TryParse(raw, out var userId) ? userId : 0L;
        }

        private static string ReadStoredRefreshToken()
        {
            return PlayerPrefs.GetString(AuthStorageKeys.RefreshToken, string.Empty);
        }

        private static void SaveRefreshedTokens(
            long userId,
            string newAccessToken,
            string newRefreshToken,
            long expiresInSeconds)
        {
            PlayerPrefs.DeleteKey(AuthStorageKeys.AccessToken);
            PlayerPrefs.DeleteKey(AuthStorageKeys.RefreshToken);
            PlayerPrefs.DeleteKey(AuthStorageKeys.UserId);
            PlayerPrefs.DeleteKey(AuthStorageKeys.ExpiresInSeconds);
            PlayerPrefs.DeleteKey(AuthStorageKeys.AccessTokenExpiresAt);

            PlayerPrefs.SetString(AuthStorageKeys.UserId, userId.ToString());
            PlayerPrefs.SetString(AuthStorageKeys.AccessToken, newAccessToken ?? string.Empty);
            PlayerPrefs.SetString(AuthStorageKeys.RefreshToken, newRefreshToken ?? string.Empty);
            PlayerPrefs.SetString(AuthStorageKeys.ExpiresInSeconds, expiresInSeconds.ToString());

            var safety = Math.Min(60L, Math.Max(0L, expiresInSeconds / 10L));
            var expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Math.Max(0L, expiresInSeconds - safety);
            PlayerPrefs.SetString(AuthStorageKeys.AccessTokenExpiresAt, expiresAt.ToString());
            PlayerPrefs.Save();
        }

        public Coroutine CheckHealth(
            Action<ApiHealthCheckResult> onComplete = null,
            string path = "/health",
            Dictionary<string, string> headers = null)
        {
            var request = UnityWebRequest.Get(BuildUrl(string.IsNullOrWhiteSpace(path) ? "/health" : path));
            ApplyHeaders(request, headers);
            return StartCoroutine(SendHealth(request, onComplete));
        }

        public Coroutine Get<TResponse>(
            string path,
            Action<TResponse> onSuccess,
            Action<ApiErrorResponseDto> onError = null,
            Dictionary<string, string> headers = null)
        {
            return StartCoroutine(Send(
                () =>
                {
                    var request = UnityWebRequest.Get(BuildUrl(path));
                    ApplyHeaders(request, headers);
                    return request;
                },
                onSuccess,
                onError));
        }

        public Coroutine Post<TRequest, TResponse>(
            string path,
            TRequest body,
            Action<TResponse> onSuccess,
            Action<ApiErrorResponseDto> onError = null,
            Dictionary<string, string> headers = null)
        {
            var json = JsonConvert.SerializeObject(body);
            return StartCoroutine(Send(
                () =>
                {
                    var request = CreateJsonRequest(BuildUrl(path), UnityWebRequest.kHttpVerbPOST, json);
                    ApplyHeaders(request, headers);
                    return request;
                },
                onSuccess,
                onError));
        }

        public Coroutine Post<TResponse>(
            string path,
            Action<TResponse> onSuccess,
            Action<ApiErrorResponseDto> onError = null,
            Dictionary<string, string> headers = null)
        {
            return StartCoroutine(Send(
                () =>
                {
                    var request = CreateJsonRequest(BuildUrl(path), UnityWebRequest.kHttpVerbPOST, null);
                    ApplyHeaders(request, headers);
                    return request;
                },
                onSuccess,
                onError));
        }

        public Coroutine Put<TRequest, TResponse>(
            string path,
            TRequest body,
            Action<TResponse> onSuccess,
            Action<ApiErrorResponseDto> onError = null,
            Dictionary<string, string> headers = null)
        {
            var json = JsonConvert.SerializeObject(body);
            return StartCoroutine(Send(
                () =>
                {
                    var request = CreateJsonRequest(BuildUrl(path), UnityWebRequest.kHttpVerbPUT, json);
                    ApplyHeaders(request, headers);
                    return request;
                },
                onSuccess,
                onError));
        }

        public Coroutine PutRaw(
            string absoluteUrl,
            byte[] body,
            string contentType,
            Action onSuccess,
            Action<ApiErrorResponseDto> onError = null,
            Dictionary<string, string> headers = null)
        {
            var request = new UnityWebRequest(absoluteUrl, UnityWebRequest.kHttpVerbPUT)
            {
                uploadHandler = new UploadHandlerRaw(body ?? Array.Empty<byte>()),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Content-Type", string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

            if (headers != null)
            {
                foreach (var pair in headers)
                {
                    if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value != null)
                    {
                        request.SetRequestHeader(pair.Key, pair.Value);
                    }
                }
            }

            return StartCoroutine(SendRaw(request, onSuccess, onError));
        }

        public Coroutine Patch<TRequest, TResponse>(
            string path,
            TRequest body,
            Action<TResponse> onSuccess,
            Action<ApiErrorResponseDto> onError = null,
            Dictionary<string, string> headers = null)
        {
            var json = JsonConvert.SerializeObject(body);
            return StartCoroutine(Send(
                () =>
                {
                    var request = CreateJsonRequest(BuildUrl(path), "PATCH", json);
                    ApplyHeaders(request, headers);
                    return request;
                },
                onSuccess,
                onError));
        }

        public Coroutine Patch<TResponse>(
            string path,
            Action<TResponse> onSuccess,
            Action<ApiErrorResponseDto> onError = null,
            Dictionary<string, string> headers = null)
        {
            return StartCoroutine(Send(
                () =>
                {
                    var request = CreateJsonRequest(BuildUrl(path), "PATCH", null);
                    ApplyHeaders(request, headers);
                    return request;
                },
                onSuccess,
                onError));
        }

        public Coroutine Delete<TResponse>(
            string path,
            Action<TResponse> onSuccess,
            Action<ApiErrorResponseDto> onError = null,
            Dictionary<string, string> headers = null)
        {
            return StartCoroutine(Send(
                () =>
                {
                    var request = new UnityWebRequest(BuildUrl(path), UnityWebRequest.kHttpVerbDELETE)
                    {
                        downloadHandler = new DownloadHandlerBuffer()
                    };
                    ApplyHeaders(request, headers);
                    return request;
                },
                onSuccess,
                onError));
        }

        private IEnumerator Send<TResponse>(
            Func<UnityWebRequest> requestFactory,
            Action<TResponse> onSuccess,
            Action<ApiErrorResponseDto> onError)
        {
            var first = new RequestOutcome<TResponse>();
            yield return SendOnce(requestFactory(), first);
            if (first.Succeeded)
            {
                onSuccess?.Invoke(first.Response);
                yield break;
            }

            if (ShouldRefreshAccessToken(first))
            {
                bool refreshSucceeded = false;
                yield return RefreshAccessToken(result => refreshSucceeded = result);
                if (refreshSucceeded)
                {
                    var retry = new RequestOutcome<TResponse>();
                    yield return SendOnce(requestFactory(), retry);
                    if (retry.Succeeded)
                    {
                        onSuccess?.Invoke(retry.Response);
                        yield break;
                    }

                    LogApiFailure(retry);
                    onError?.Invoke(retry.Error);
                    yield break;
                }
            }

            LogApiFailure(first);
            onError?.Invoke(first.Error);
        }

        private IEnumerator SendOnce<TResponse>(
            UnityWebRequest request,
            RequestOutcome<TResponse> outcome)
        {
            request.timeout = Mathf.Max(1, timeoutSeconds);

            using (request)
            {
                outcome.Method = request.method;
                outcome.Url = request.url;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    outcome.Error = CreateError(request, request.error);
                    yield break;
                }

                var text = request.downloadHandler?.text;
                try
                {
                    if (enableVerboseLog)
                    {
                        Debug.Log($"API {request.method} {request.url} OK: {request.responseCode}");
                    }

                    outcome.Response = string.IsNullOrWhiteSpace(text)
                        ? default
                        : JsonConvert.DeserializeObject<TResponse>(text);
                    outcome.Succeeded = true;
                }
                catch (Exception exception)
                {
                    outcome.ParseFailed = true;
                    outcome.Error = CreateError(request, exception.Message);
                }
            }
        }

        private IEnumerator Send<TResponse>(
            UnityWebRequest request,
            Action<TResponse> onSuccess,
            Action<ApiErrorResponseDto> onError)
        {
            request.timeout = Mathf.Max(1, timeoutSeconds);

            using (request)
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    var error = CreateError(request, request.error);
                    var message = string.IsNullOrWhiteSpace(error.Message) ? error.Error : error.Message;
                    var body = FormatResponseBodyForLog(error.Body);
                    var bodySuffix = string.IsNullOrWhiteSpace(body) ? string.Empty : $" body='{body}'";
                    Debug.LogError($"API {request.method} {request.url} failed: {error.StatusCode} {message}{bodySuffix}");
                    // 401 진단 — baseUrl mismatch 디버깅용. 토큰 자체 (prefix/length 포함) 는 절대 노출 안 함:
                    // Unity 로그는 device logcat / CI batchmode / crash report 등으로 퍼져 외부에 누출 가능
                    // (AuthApiClient.Redact 와 동일 정책). tokenPresent / baseUrl / errorCode 만으로 충분.
                    // (MR 111 review — skh1205 #1)
                    if (request.responseCode == 401)
                    {
                        bool hasToken = !string.IsNullOrWhiteSpace(accessToken);
                        Debug.LogWarning(
                            $"[APIManager] 401 진단: baseUrl='{baseUrl}' method='{request.method}' " +
                            $"url='{request.url}' tokenPresent={hasToken} errorCode={error.Code}.");
                    }
                    onError?.Invoke(error);
                    yield break;
                }

                var text = request.downloadHandler?.text;
                try
                {
                    if (enableVerboseLog)
                    {
                        Debug.Log($"API {request.method} {request.url} OK: {request.responseCode}");
                    }

                    var response = string.IsNullOrWhiteSpace(text)
                        ? default
                        : JsonConvert.DeserializeObject<TResponse>(text);
                    onSuccess?.Invoke(response);
                }
                catch (Exception exception)
                {
                    var error = CreateError(request, exception.Message);
                    Debug.LogError($"API {request.method} {request.url} parse failed: {exception.Message} (response body omitted)");
                    onError?.Invoke(error);
                }
            }
        }

        private bool ShouldRefreshAccessToken<TResponse>(RequestOutcome<TResponse> outcome)
        {
            if (outcome == null || outcome.Succeeded || outcome.Error == null)
            {
                return false;
            }

            if (outcome.Error.StatusCode != UnauthorizedStatusCode)
            {
                return false;
            }

            if (IsAuthEndpoint(outcome.Url))
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(ReadStoredRefreshToken());
        }

        private IEnumerator RefreshAccessToken(Action<bool> onComplete)
        {
            if (tokenRefreshInFlight)
            {
                bool completed = false;
                bool result = false;
                tokenRefreshWaiters.Add(success =>
                {
                    result = success;
                    completed = true;
                });
                yield return new WaitUntil(() => completed);
                onComplete?.Invoke(result);
                yield break;
            }

            var refreshToken = ReadStoredRefreshToken();
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                onComplete?.Invoke(false);
                yield break;
            }

            tokenRefreshInFlight = true;
            bool succeeded = false;
            var requestBody = JsonConvert.SerializeObject(new TokenRefreshRequestDto { RefreshToken = refreshToken });
            var request = CreateJsonRequest(BuildUrl("/v1/auth/token/refresh"), UnityWebRequest.kHttpVerbPOST, requestBody);
            request.timeout = Mathf.Max(1, timeoutSeconds);
            request.SetRequestHeader("Accept", "application/json");

            using (request)
            {
                yield return request.SendWebRequest();

                var responseText = request.downloadHandler?.text;
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var envelope = string.IsNullOrWhiteSpace(responseText)
                            ? null
                            : JsonConvert.DeserializeObject<ApiEnvelopeDto<AuthTokenResponseDto>>(responseText);
                        var tokens = envelope != null && envelope.Succeeded ? envelope.Result : null;
                        if (tokens != null && !string.IsNullOrWhiteSpace(tokens.AccessToken))
                        {
                            var userId = tokens.UserId > 0L ? tokens.UserId : ReadStoredUserId();
                            SaveRefreshedTokens(
                                userId,
                                tokens.AccessToken,
                                string.IsNullOrWhiteSpace(tokens.RefreshToken) ? refreshToken : tokens.RefreshToken,
                                tokens.ExpiresIn);
                            SetAccessToken(tokens.AccessToken);
                            succeeded = true;
                            if (enableVerboseLog)
                            {
                                Debug.Log("[APIManager] accessToken refreshed after 401.");
                            }
                        }
                        else
                        {
                            Debug.LogWarning("[APIManager] token refresh response missing accessToken.");
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"[APIManager] token refresh parse failed: {exception.Message}");
                    }
                }
                else
                {
                    var error = ParseErrorBody(request.responseCode, responseText, request.error);
                    Debug.LogWarning(
                        $"[APIManager] token refresh failed: {error.StatusCode} " +
                        $"{FirstNonEmpty(error.Code, error.Message, error.Error)}");
                }
            }

            CompleteTokenRefresh(succeeded);
            onComplete?.Invoke(succeeded);
        }

        private void CompleteTokenRefresh(bool succeeded)
        {
            tokenRefreshInFlight = false;
            if (tokenRefreshWaiters.Count == 0)
            {
                return;
            }

            var waiters = tokenRefreshWaiters.ToArray();
            tokenRefreshWaiters.Clear();
            foreach (var waiter in waiters)
            {
                try
                {
                    waiter?.Invoke(succeeded);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[APIManager] token refresh waiter failed: {exception.Message}");
                }
            }
        }

        private static bool IsAuthEndpoint(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            return url.IndexOf("/v1/auth/token/refresh", StringComparison.OrdinalIgnoreCase) >= 0
                || url.IndexOf("/v1/auth/social-login", StringComparison.OrdinalIgnoreCase) >= 0
                || url.IndexOf("/v1/auth/logout", StringComparison.OrdinalIgnoreCase) >= 0
                || url.IndexOf("/v1/auth/guest-session", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void LogApiFailure<TResponse>(RequestOutcome<TResponse> outcome)
        {
            if (outcome == null || outcome.Error == null)
            {
                return;
            }

            var error = outcome.Error;
            if (outcome.ParseFailed)
            {
                Debug.LogError($"API {outcome.Method} {outcome.Url} parse failed: {error.Error} (response body omitted)");
                return;
            }

            var message = string.IsNullOrWhiteSpace(error.Message) ? error.Error : error.Message;
            var body = FormatResponseBodyForLog(error.Body);
            var bodySuffix = string.IsNullOrWhiteSpace(body) ? string.Empty : $" body='{body}'";
            Debug.LogError($"API {outcome.Method} {outcome.Url} failed: {error.StatusCode} {message}{bodySuffix}");

            if (error.StatusCode == UnauthorizedStatusCode)
            {
                bool hasToken = !string.IsNullOrWhiteSpace(accessToken);
                Debug.LogWarning(
                    $"[APIManager] 401 diagnostic: baseUrl='{baseUrl}' method='{outcome.Method}' " +
                    $"url='{outcome.Url}' tokenPresent={hasToken} errorCode={error.Code}.");
            }
        }

        private IEnumerator SendRaw(
            UnityWebRequest request,
            Action onSuccess,
            Action<ApiErrorResponseDto> onError)
        {
            request.timeout = Mathf.Max(1, timeoutSeconds);

            using (request)
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    var error = CreateError(request, request.error);
                    var message = string.IsNullOrWhiteSpace(error.Message) ? error.Error : error.Message;
                    var body = FormatResponseBodyForLog(error.Body);
                    var bodySuffix = string.IsNullOrWhiteSpace(body) ? string.Empty : $" body='{body}'";
                    Debug.LogError($"RAW {request.method} {request.url} failed: {error.StatusCode} {message}{bodySuffix}");
                    onError?.Invoke(error);
                    yield break;
                }

                if (enableVerboseLog)
                {
                    Debug.Log($"RAW {request.method} {request.url} OK: {request.responseCode}");
                }

                onSuccess?.Invoke();
            }
        }

        private IEnumerator SendHealth(UnityWebRequest request, Action<ApiHealthCheckResult> onComplete)
        {
            request.timeout = Mathf.Max(1, timeoutSeconds);

            using (request)
            {
                yield return request.SendWebRequest();

                var body = request.downloadHandler?.text;
                var error = ParseErrorBody(request.responseCode, body, request.error);
                var reachedServer = request.responseCode > 0;
                var statusCode = request.responseCode;
                var result = new ApiHealthCheckResult
                {
                    Url = request.url,
                    IsReachable = reachedServer,
                    IsHealthy = request.result == UnityWebRequest.Result.Success && statusCode >= 200 && statusCode < 300,
                    RequiresAuthentication = statusCode == 401 || string.Equals(error.Code, "A001", StringComparison.OrdinalIgnoreCase),
                    StatusCode = statusCode,
                    Code = error.Code,
                    Message = FirstNonEmpty(error.Message, error.Error, request.error),
                    Body = body
                };

                LogHealthResult(result);
                onComplete?.Invoke(result);
            }
        }

        private static UnityWebRequest CreateJsonRequest(string url, string method, string json)
        {
            var request = new UnityWebRequest(url, method)
            {
                downloadHandler = new DownloadHandlerBuffer()
            };

            request.SetRequestHeader("Content-Type", "application/json");

            if (json != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            }

            return request;
        }

        private static ApiErrorResponseDto CreateError(UnityWebRequest request, string message)
        {
            var error = ParseErrorBody(request.responseCode, request.downloadHandler?.text, message);
            if (string.IsNullOrWhiteSpace(error.Error))
            {
                error.Error = message;
            }

            return error;
        }

        private static ApiErrorResponseDto ParseErrorBody(long statusCode, string body, string fallbackMessage)
        {
            var error = new ApiErrorResponseDto
            {
                StatusCode = statusCode,
                Error = fallbackMessage,
                Body = body
            };

            if (string.IsNullOrWhiteSpace(body))
            {
                return error;
            }

            try
            {
                var parsed = JsonConvert.DeserializeObject<ApiErrorResponseDto>(body);
                if (parsed == null)
                {
                    return error;
                }

                error.HttpStatus = parsed.HttpStatus;
                error.Code = parsed.Code;
                error.Message = parsed.Message;
                error.Success = parsed.Success;
                error.Error = FirstNonEmpty(parsed.Error, parsed.Message, fallbackMessage);
            }
            catch (Exception)
            {
                // Keep the original body for diagnostics when the server does not return the expected envelope.
            }

            return error;
        }

        private static void LogHealthResult(ApiHealthCheckResult result)
        {
            if (result == null)
            {
                return;
            }

            if (result.IsHealthy)
            {
                App.Core.AppLog.Api($"Dotshelf API health OK: {result.Url} ({result.StatusCode})");
                return;
            }

            if (result.RequiresAuthentication)
            {
                Debug.LogWarning($"Dotshelf API reached but requires authentication: {result.Url} ({result.StatusCode}, {result.Code}) {result.Message}");
                return;
            }

            if (result.IsReachable)
            {
                Debug.LogWarning($"Dotshelf API reached with non-healthy response: {result.Url} ({result.StatusCode}) {result.Message}");
                return;
            }

            // Demo runs are expected to launch without a backend (mock catalog repository
            // takes over in BookshelfSceneInstaller), so this is a normal startup path —
            // surface as a warning instead of an error to keep the console clean.
            Debug.LogWarning($"Dotshelf API health check failed before reaching server: {result.Url} {result.Message}");
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static string FormatResponseBodyForLog(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return string.Empty;
            }

            var normalized = body
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
            return normalized.Length <= ErrorResponseBodyLogLimit
                ? normalized
                : normalized.Substring(0, ErrorResponseBodyLogLimit) + "...";
        }

        private void ApplyHeaders(UnityWebRequest request, Dictionary<string, string> headers)
        {
            request.SetRequestHeader("Accept", "application/json");

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            }

            if (headers == null)
            {
                return;
            }

            foreach (var pair in headers)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value != null)
                {
                    request.SetRequestHeader(pair.Key, pair.Value);
                }
            }
        }

        private string BuildUrl(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return baseUrl;
            }

            // path가 진짜 절대 URL인지 검증.
            // (이전 버그) 단순 Uri.TryCreate(path, UriKind.Absolute) 만 쓰면 한글 인코딩 들어간 path도
            // absolute로 잘못 인식해서 baseUrl 없이 path만 반환 → UnityWebRequest가 default 'localhost'
            // host로 채워 호출 실패. http/https/ws/wss 같은 HTTP 계열 known scheme이어야 진짜 절대 URL로 인정.
            //
            // (260503 Mac localhost bug fix) "file" 제거. Mac/Linux Mono는 POSIX 절대 경로 "/v1/..."를
            // Uri.TryCreate에서 file:///v1/... 로 인식해 IsKnownAbsoluteScheme("file") 통과 → early return →
            // baseUrl 없이 "/v1/..." 반환 → UnityWebRequest가 localhost로 fallback. Windows .NET은 "/" 시작
            // 경로를 absolute로 안 보므로 fall-through 정상 동작 → Mac/Windows 차이의 정확한 root cause.
            // API 호출 path에 file:// scheme 쓸 일 없음.
            if (Uri.TryCreate(path, UriKind.Absolute, out var parsed) && IsKnownAbsoluteScheme(parsed.Scheme))
            {
                return path;
            }

            return $"{baseUrl}/{path.TrimStart('/')}";
        }

        private static readonly System.Collections.Generic.HashSet<string> KnownAbsoluteSchemes =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "http", "https", "ws", "wss"
            };

        private static bool IsKnownAbsoluteScheme(string scheme)
        {
            return !string.IsNullOrEmpty(scheme) && KnownAbsoluteSchemes.Contains(scheme);
        }

        private sealed class RequestOutcome<TResponse>
        {
            public bool Succeeded;
            public bool ParseFailed;
            public TResponse Response;
            public ApiErrorResponseDto Error;
            public string Method;
            public string Url;
        }

        private static string NormalizeBaseUrl(string candidate, string fallback)
        {
            return string.IsNullOrWhiteSpace(candidate)
                ? fallback
                : candidate.Trim().TrimEnd('/');
        }
    }
}
