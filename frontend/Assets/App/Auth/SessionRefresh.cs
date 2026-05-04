using System;
using App.Infrastructure.Api;
using App.Infrastructure.Api.Dtos;
using UnityEngine;

namespace App.Auth
{
    // Static helper for "401 → refresh → retry once" flow.
    //
    // Why not bake this into APIManager / NotebookApiClient.SendRequest?
    //   - Touching every request path is high blast-radius. Auto-retry
    //     can mask BE bugs (idempotency violations on POSTs that already
    //     mutated state) and double-fire side effects.
    //   - Callers know whether their request is safe to retry (GETs and
    //     idempotent PATCHes yes; non-idempotent POSTs case-by-case).
    //
    // Pattern at the call site:
    //   client.GetMe(
    //       onSuccess: profile => ...,
    //       onError: error =>
    //       {
    //           if (SessionRefresh.IsAuthFailure(error))
    //           {
    //               SessionRefresh.TryRefreshAndRetry(
    //                   onRetry: () => client.GetMe(onSuccess, onErrorFinal),
    //                   onAbort: () => SessionRefresh.ClearAndReturnToLogin());
    //               return;
    //           }
    //           // non-401 path
    //       });
    //
    // Guest tokens have no refreshToken — TryRefreshAndRetry detects this
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

        // onRetry: invoked once after successful refresh. Caller re-issues
        //   the original request inside this delegate.
        // onAbort: invoked when refresh is impossible (guest, missing token)
        //   or fails. Caller should surface a session-ended message AND/OR
        //   call ClearAndReturnToLogin.
        public static void TryRefreshAndRetry(Action onRetry, Action onAbort)
        {
            if (isRefreshInFlight)
            {
                // Another caller is already refreshing. The simplest correct
                // behavior is to abort this caller; once the in-flight
                // refresh lands, the next user action will retry naturally.
                onAbort?.Invoke();
                return;
            }

            var refreshToken = AuthSession.RefreshToken;
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                // Guest sessions hold no refresh token; same for users that
                // never completed login. Cannot recover the session here.
                onAbort?.Invoke();
                return;
            }

            if (APIManager.Instance == null)
            {
                Debug.LogWarning("[SessionRefresh] APIManager.Instance is null — cannot refresh.");
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
        // "session ended, go log in again" UX. SettingsScreenController has
        // its own version (FinishAccountClearAndReturnToLogin) that also
        // closes the settings panel; that one wins for in-settings cases.
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
