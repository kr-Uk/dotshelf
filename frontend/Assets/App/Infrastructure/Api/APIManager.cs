using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
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
        [SerializeField] private string baseUrl = "https://api.dotshelf.digital";
        [SerializeField] private string accessToken;
        [SerializeField] private int timeoutSeconds = 10;

        public static APIManager Instance { get; private set; }
        public bool HasAccessToken => !string.IsNullOrWhiteSpace(accessToken);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            HydrateAccessTokenFromAuthSession();
        }

        // AuthSession (in Assembly-CSharp under App.Auth) cannot be referenced
        // from this assembly directly, but its persisted access token must be
        // available to APIManager once MainScene loads. We read the same
        // PlayerPrefs key here. Keep this key in sync with AuthSession.cs.
        private void HydrateAccessTokenFromAuthSession()
        {
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                return;
            }

            var stored = PlayerPrefs.GetString("Dotshelf.Auth.AccessToken", string.Empty);
            if (!string.IsNullOrWhiteSpace(stored))
            {
                accessToken = stored;
            }
        }

        public void Configure(string apiBaseUrl, string token = null, int timeout = 10)
        {
            baseUrl = NormalizeBaseUrl(apiBaseUrl, baseUrl);
            // Only override the access token when a non-empty value is supplied.
            // AppApiConfig.ResolveAccessToken() returns string.Empty when no env var
            // and no serialized token are set, which would otherwise erase a token
            // hydrated from PlayerPrefs after a successful social/guest login.
            if (!string.IsNullOrWhiteSpace(token))
            {
                accessToken = token;
            }

            timeoutSeconds = Mathf.Max(1, timeout);
        }

        public void SetAccessToken(string token)
        {
            accessToken = token;
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
            var request = UnityWebRequest.Get(BuildUrl(path));
            ApplyHeaders(request, headers);
            return StartCoroutine(Send<TResponse>(request, onSuccess, onError));
        }

        public Coroutine Post<TRequest, TResponse>(
            string path,
            TRequest body,
            Action<TResponse> onSuccess,
            Action<ApiErrorResponseDto> onError = null,
            Dictionary<string, string> headers = null)
        {
            var json = JsonConvert.SerializeObject(body);
            var request = CreateJsonRequest(BuildUrl(path), UnityWebRequest.kHttpVerbPOST, json);
            ApplyHeaders(request, headers);
            return StartCoroutine(Send<TResponse>(request, onSuccess, onError));
        }

        public Coroutine Post<TResponse>(
            string path,
            Action<TResponse> onSuccess,
            Action<ApiErrorResponseDto> onError = null,
            Dictionary<string, string> headers = null)
        {
            var request = CreateJsonRequest(BuildUrl(path), UnityWebRequest.kHttpVerbPOST, null);
            ApplyHeaders(request, headers);
            return StartCoroutine(Send<TResponse>(request, onSuccess, onError));
        }

        public Coroutine Put<TRequest, TResponse>(
            string path,
            TRequest body,
            Action<TResponse> onSuccess,
            Action<ApiErrorResponseDto> onError = null,
            Dictionary<string, string> headers = null)
        {
            var json = JsonConvert.SerializeObject(body);
            var request = CreateJsonRequest(BuildUrl(path), UnityWebRequest.kHttpVerbPUT, json);
            ApplyHeaders(request, headers);
            return StartCoroutine(Send<TResponse>(request, onSuccess, onError));
        }

        public Coroutine Patch<TRequest, TResponse>(
            string path,
            TRequest body,
            Action<TResponse> onSuccess,
            Action<ApiErrorResponseDto> onError = null,
            Dictionary<string, string> headers = null)
        {
            var json = JsonConvert.SerializeObject(body);
            var request = CreateJsonRequest(BuildUrl(path), "PATCH", json);
            ApplyHeaders(request, headers);
            return StartCoroutine(Send<TResponse>(request, onSuccess, onError));
        }

        public Coroutine Patch<TResponse>(
            string path,
            Action<TResponse> onSuccess,
            Action<ApiErrorResponseDto> onError = null,
            Dictionary<string, string> headers = null)
        {
            var request = CreateJsonRequest(BuildUrl(path), "PATCH", null);
            ApplyHeaders(request, headers);
            return StartCoroutine(Send<TResponse>(request, onSuccess, onError));
        }

        public Coroutine Delete<TResponse>(
            string path,
            Action<TResponse> onSuccess,
            Action<ApiErrorResponseDto> onError = null,
            Dictionary<string, string> headers = null)
        {
            var request = new UnityWebRequest(BuildUrl(path), UnityWebRequest.kHttpVerbDELETE)
            {
                downloadHandler = new DownloadHandlerBuffer()
            };
            ApplyHeaders(request, headers);
            return StartCoroutine(Send<TResponse>(request, onSuccess, onError));
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
                    Debug.LogError($"API {request.method} {request.url} failed: {error.StatusCode} {message}");
                    onError?.Invoke(error);
                    yield break;
                }

                var text = request.downloadHandler?.text;
                try
                {
                    var response = string.IsNullOrWhiteSpace(text)
                        ? default
                        : JsonConvert.DeserializeObject<TResponse>(text);
                    onSuccess?.Invoke(response);
                }
                catch (Exception exception)
                {
                    var error = CreateError(request, exception.Message);
                    Debug.LogError($"API {request.method} {request.url} parse failed: {exception.Message}");
                    onError?.Invoke(error);
                }
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
                Debug.Log($"Dotshelf API health OK: {result.Url} ({result.StatusCode})");
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

        private void ApplyHeaders(UnityWebRequest request, Dictionary<string, string> headers)
        {
            request.SetRequestHeader("Accept", "application/json");

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.SetRequestHeader("access", accessToken);
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
            // host로 채워 호출 실패. http/https/ws/wss/file 같은 known scheme이어야 진짜 절대 URL로 인정.
            if (Uri.TryCreate(path, UriKind.Absolute, out var parsed) && IsKnownAbsoluteScheme(parsed.Scheme))
            {
                return path;
            }

            return $"{baseUrl}/{path.TrimStart('/')}";
        }

        private static readonly System.Collections.Generic.HashSet<string> KnownAbsoluteSchemes =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "http", "https", "ws", "wss", "file"
            };

        private static bool IsKnownAbsoluteScheme(string scheme)
        {
            return !string.IsNullOrEmpty(scheme) && KnownAbsoluteSchemes.Contains(scheme);
        }

        private static string NormalizeBaseUrl(string candidate, string fallback)
        {
            return string.IsNullOrWhiteSpace(candidate)
                ? fallback
                : candidate.Trim().TrimEnd('/');
        }
    }
}
