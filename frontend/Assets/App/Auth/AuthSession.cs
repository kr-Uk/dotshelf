using System;
using App.Domain.Auth;
using App.Infrastructure.Api;
using App.Infrastructure.Api.Dtos;
using UnityEngine;

namespace App.Auth
{
    // Persisted authentication artefacts returned by /v1/auth/social-login and
    // /v1/auth/token/refresh. Stored in PlayerPrefs so the next launch can hand
    // a Bearer token to APIManager without re-running the social login flow.
    public static class AuthSession
    {
        public static string AccessToken => PlayerPrefs.GetString(AuthStorageKeys.AccessToken, string.Empty);
        public static string RefreshToken => PlayerPrefs.GetString(AuthStorageKeys.RefreshToken, string.Empty);
        public static string GuestSessionId => PlayerPrefs.GetString(AuthStorageKeys.GuestSessionId, string.Empty);
        public static bool HasAccessToken => !string.IsNullOrWhiteSpace(AccessToken);

        // True when /v1/auth/guest-session has been issued but no social-login
        // exchange has happened yet — i.e., no Bearer token is available.
        // Authenticated endpoints (anything that returns 401 without a token)
        // must be skipped while this is true; the main-scene bookshelf falls
        // back to the local catalog instead of /v1/spaces/me.
        public static bool IsGuestSession =>
            !HasAccessToken && !string.IsNullOrWhiteSpace(GuestSessionId);
        public static bool NewUser => PlayerPrefs.GetInt(AuthStorageKeys.NewUser, 0) == 1;
        public static bool Onboarded => PlayerPrefs.GetInt(AuthStorageKeys.Onboarded, 0) == 1;

        public static long UserId
        {
            get
            {
                var raw = PlayerPrefs.GetString(AuthStorageKeys.UserId, string.Empty);
                return long.TryParse(raw, out var parsed) ? parsed : 0L;
            }
        }

        public static long ExpiresInSeconds
        {
            get
            {
                var raw = PlayerPrefs.GetString(AuthStorageKeys.ExpiresInSeconds, string.Empty);
                return long.TryParse(raw, out var parsed) ? parsed : 0L;
            }
        }

        public static bool IsAccessTokenExpired
        {
            get
            {
                var raw = PlayerPrefs.GetString(AuthStorageKeys.AccessTokenExpiresAt, string.Empty);
                if (!long.TryParse(raw, out var expiresAtUnix))
                {
                    return true;
                }

                return DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expiresAtUnix;
            }
        }

        public static void SaveTokens(
            long userId,
            string accessToken,
            string refreshToken,
            long expiresInSeconds,
            bool? newUser = null,
            bool? onboarded = null)
        {
            PlayerPrefs.SetString(AuthStorageKeys.UserId, userId.ToString());
            PlayerPrefs.SetString(AuthStorageKeys.AccessToken, accessToken ?? string.Empty);
            PlayerPrefs.SetString(AuthStorageKeys.RefreshToken, refreshToken ?? string.Empty);
            PlayerPrefs.SetString(AuthStorageKeys.ExpiresInSeconds, expiresInSeconds.ToString());
            if (newUser.HasValue)
            {
                PlayerPrefs.SetInt(AuthStorageKeys.NewUser, newUser.Value ? 1 : 0);
            }

            if (onboarded.HasValue)
            {
                PlayerPrefs.SetInt(AuthStorageKeys.Onboarded, onboarded.Value ? 1 : 0);
            }

            var safety = Math.Min(60L, Math.Max(0L, expiresInSeconds / 10));
            var expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Math.Max(0L, expiresInSeconds - safety);
            PlayerPrefs.SetString(AuthStorageKeys.AccessTokenExpiresAt, expiresAt.ToString());
            PlayerPrefs.Save();
        }

        public static void SaveGuestSessionId(string guestSessionId)
        {
            PlayerPrefs.SetString(AuthStorageKeys.GuestSessionId, guestSessionId ?? string.Empty);
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(AuthStorageKeys.AccessToken);
            PlayerPrefs.DeleteKey(AuthStorageKeys.RefreshToken);
            PlayerPrefs.DeleteKey(AuthStorageKeys.UserId);
            PlayerPrefs.DeleteKey(AuthStorageKeys.ExpiresInSeconds);
            PlayerPrefs.DeleteKey(AuthStorageKeys.AccessTokenExpiresAt);
            PlayerPrefs.DeleteKey(AuthStorageKeys.NewUser);
            PlayerPrefs.DeleteKey(AuthStorageKeys.Onboarded);
            PlayerPrefs.DeleteKey(AuthStorageKeys.GuestSessionId);
            PlayerPrefs.Save();
        }
    }

    // Static helper for "401 -> refresh -> retry once" flow.
    //
    // Why not bake this into APIManager / NotebookApiClient.SendRequest?
    //   - Touching every request path is high blast-radius. Auto-retry
    //     can mask BE bugs (idempotency violations on POSTs that already
    //     mutated state) and double-fire side effects.
    //   - Callers know whether their request is safe to retry (GETs and
    //     idempotent PATCHes yes; non-idempotent POSTs case-by-case).
    //
    // Guest tokens have no refreshToken. TryRefreshAndRetry detects this
    // and routes to onAbort directly so the caller can't accidentally
    // retry forever.
    public static class SessionRefresh
    {
        // Single-flight guard so a burst of 401s from concurrent requests
        // does not fan out into multiple POST /v1/auth/token/refresh calls.
        private static bool isRefreshInFlight;

        public static bool IsAuthFailure(ApiErrorResponseDto error)
        {
            var status = error?.StatusCode ?? 0L;
            return status == 401L || status == 403L;
        }

        public static void TryRefreshAndRetry(Action onRetry, Action onAbort)
        {
            if (isRefreshInFlight)
            {
                // Another caller is already refreshing. Abort this caller;
                // the next user action can retry after the in-flight refresh.
                onAbort?.Invoke();
                return;
            }

            var refreshToken = AuthSession.RefreshToken;
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                // Guest sessions and incomplete social login sessions cannot
                // recover here because they do not have a refresh token.
                onAbort?.Invoke();
                return;
            }

            if (APIManager.Instance == null)
            {
                Debug.LogWarning("[SessionRefresh] APIManager.Instance is null; cannot refresh.");
                onAbort?.Invoke();
                return;
            }

            isRefreshInFlight = true;
            var client = new NotebookApiClient(APIManager.Instance);
            var request = new TokenRefreshRequestDto { RefreshToken = refreshToken };

            client.RefreshToken(
                request,
                onSuccess: tokens =>
                {
                    isRefreshInFlight = false;
                    if (tokens == null || string.IsNullOrWhiteSpace(tokens.AccessToken))
                    {
                        Debug.LogWarning("[SessionRefresh] refresh response missing accessToken.");
                        onAbort?.Invoke();
                        return;
                    }

                    AuthSession.SaveTokens(
                        userId: tokens.UserId,
                        accessToken: tokens.AccessToken,
                        refreshToken: string.IsNullOrWhiteSpace(tokens.RefreshToken)
                            ? refreshToken
                            : tokens.RefreshToken,
                        expiresInSeconds: tokens.ExpiresIn);
                    APIManager.Instance.SetAccessToken(tokens.AccessToken);

                    onRetry?.Invoke();
                },
                onError: error =>
                {
                    isRefreshInFlight = false;
                    Debug.LogWarning(
                        $"[SessionRefresh] refresh failed: code={error?.Code} message={error?.Message}");
                    onAbort?.Invoke();
                });
        }

        // Convenience: full session teardown + jump to login. Intended use is
        // inside an onAbort handler when the caller wants the standard
        // "session ended, go log in again" UX.
        public static void ClearAndReturnToLogin()
        {
            AuthSession.Clear();
            SessionState.Clear();
            if (APIManager.Instance != null)
            {
                APIManager.Instance.SetAccessToken(string.Empty);
            }

            App.Bootstrap.SceneFlowManager.GoToLogin();
        }
    }
}
