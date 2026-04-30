using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using App.Infrastructure.Api.Dtos;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace App.Auth
{
    // Coroutine-based BE client for /v1/auth/* endpoints. Lives on its own
    // because LoginScene does not host APIManager (APIManager spawns in
    // MainScene and persists via DontDestroyOnLoad). Once login succeeds we
    // hand the access token to AuthSession; APIManager picks it up later in
    // MainScene.
    public sealed class AuthApiClient : MonoBehaviour
    {
        public const string DefaultBaseUrl = "https://api.dotshelf.digital";
        public const int DefaultTimeoutSeconds = 10;

        private string baseUrl = DefaultBaseUrl;
        private int timeoutSeconds = DefaultTimeoutSeconds;

        public static AuthApiClient EnsureInstance(string overrideBaseUrl = null, int overrideTimeoutSeconds = 0)
        {
            var existing = FindFirstObjectByType<AuthApiClient>();
            if (existing == null)
            {
                var holder = new GameObject(nameof(AuthApiClient));
                DontDestroyOnLoad(holder);
                existing = holder.AddComponent<AuthApiClient>();
            }

            if (!string.IsNullOrWhiteSpace(overrideBaseUrl))
            {
                existing.baseUrl = overrideBaseUrl.Trim().TrimEnd('/');
            }

            if (overrideTimeoutSeconds > 0)
            {
                existing.timeoutSeconds = overrideTimeoutSeconds;
            }

            return existing;
        }

        public Coroutine PostSocialLogin(
            SocialLoginRequestDto request,
            Action<AuthTokenResponseDto> onSuccess,
            Action<ApiErrorResponseDto> onError)
        {
            return StartCoroutine(SendJson("/v1/auth/social-login", request, onSuccess, onError));
        }

        public Coroutine PostGuestSession(
            Action<GuestSessionResponseDto> onSuccess,
            Action<ApiErrorResponseDto> onError)
        {
            return StartCoroutine(SendJson<object, GuestSessionResponseDto>("/v1/auth/guest-session", null, onSuccess, onError));
        }

        public Coroutine PostTokenRefresh(
            string refreshToken,
            Action<AuthTokenResponseDto> onSuccess,
            Action<ApiErrorResponseDto> onError)
        {
            var body = new TokenRefreshRequestDto { RefreshToken = refreshToken };
            return StartCoroutine(SendJson("/v1/auth/token/refresh", body, onSuccess, onError));
        }

        public Coroutine PostLogout(string refreshToken, Action onComplete)
        {
            var body = new LogoutRequestDto { RefreshToken = refreshToken };
            return StartCoroutine(SendJson<LogoutRequestDto, object>(
                "/v1/auth/logout",
                body,
                _ => onComplete?.Invoke(),
                _ => onComplete?.Invoke()));
        }

        private IEnumerator SendJson<TRequest, TResponse>(
            string path,
            TRequest body,
            Action<TResponse> onSuccess,
            Action<ApiErrorResponseDto> onError)
        {
            var url = BuildUrl(path);
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = Mathf.Max(1, timeoutSeconds)
            };
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Content-Type", "application/json");

            string requestBodyJson = null;
            if (body != null)
            {
                requestBodyJson = JsonConvert.SerializeObject(body);
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestBodyJson));
            }

            Debug.Log($"[Auth] -> POST {url} body={Redact(requestBodyJson) ?? "(empty)"}");

            using (request)
            {
                yield return request.SendWebRequest();

                var responseText = request.downloadHandler != null ? request.downloadHandler.text : null;

                Debug.Log($"[Auth] <- {request.responseCode} {url} body={Redact(responseText)}");

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Auth] transport failure: result={request.result} error={request.error}");
                    onError?.Invoke(ParseError(request.responseCode, responseText, request.error));
                    yield break;
                }

                ApiEnvelopeDto<TResponse> envelope = null;
                try
                {
                    envelope = string.IsNullOrWhiteSpace(responseText)
                        ? null
                        : JsonConvert.DeserializeObject<ApiEnvelopeDto<TResponse>>(responseText);
                }
                catch (Exception parseException)
                {
                    onError?.Invoke(new ApiErrorResponseDto
                    {
                        StatusCode = request.responseCode,
                        Error = parseException.Message,
                        Body = responseText
                    });
                    yield break;
                }

                if (envelope == null || !envelope.Succeeded)
                {
                    onError?.Invoke(new ApiErrorResponseDto
                    {
                        StatusCode = request.responseCode,
                        Code = envelope != null ? envelope.Code : null,
                        Message = envelope != null ? envelope.Message : null,
                        Body = responseText
                    });
                    yield break;
                }

                onSuccess?.Invoke(envelope.Result);
            }
        }

        private static ApiErrorResponseDto ParseError(long statusCode, string body, string fallback)
        {
            var error = new ApiErrorResponseDto
            {
                StatusCode = statusCode,
                Error = fallback,
                Body = body
            };

            if (string.IsNullOrWhiteSpace(body))
            {
                return error;
            }

            try
            {
                var envelope = JsonConvert.DeserializeObject<ApiEnvelopeDto<object>>(body);
                if (envelope != null && (!string.IsNullOrWhiteSpace(envelope.Code) || !string.IsNullOrWhiteSpace(envelope.Message)))
                {
                    error.Code = envelope.Code;
                    error.Message = envelope.Message;
                    return error;
                }

                var parsed = JsonConvert.DeserializeObject<ApiErrorResponseDto>(body);
                if (parsed != null)
                {
                    error.Code = parsed.Code;
                    error.Message = parsed.Message;
                    error.HttpStatus = parsed.HttpStatus;
                }
            }
            catch (Exception)
            {
                // Server returned a non-envelope body; keep the raw body for diagnostics.
            }

            return error;
        }

        private string BuildUrl(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return baseUrl;
            }

            return Uri.TryCreate(path, UriKind.Absolute, out _)
                ? path
                : $"{baseUrl}/{path.TrimStart('/')}";
        }

        // Redact provider/access/refresh tokens and OIDC nonce from JSON bodies before
        // they reach Debug.Log. Unity logs leak through editor consoles, device logcat,
        // CI batchmode logs, and crash reports, so the raw token must never be written.
        private static readonly Regex SensitiveFieldRegex = new Regex(
            "\"(token|accessToken|refreshToken|idToken|access_token|refresh_token|id_token|nonce|guestSessionId|guest_session_id)\"\\s*:\\s*\"[^\"]*\"",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static string Redact(string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return body;
            }

            return SensitiveFieldRegex.Replace(body, "\"$1\":\"***\"");
        }
    }
}
