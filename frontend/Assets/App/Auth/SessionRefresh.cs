using System;
using App.Infrastructure.Api;
using App.Infrastructure.Api.Dtos;
using UnityEngine;

namespace App.Auth
{
    // Legacy helper for call sites that still handle auth retries explicitly.
    // APIManager performs the common 401 -> refresh -> retry-once flow for
    // ordinary API requests; this helper remains for custom flows that need
    // to decide their own retry/abort UI.
    public static class SessionRefresh
    {
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
                onAbort?.Invoke();
                return;
            }

            var refreshToken = AuthSession.RefreshToken;
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
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

        public static void ClearAndReturnToLogin()
        {
            AuthSession.Clear();
            if (APIManager.Instance != null)
            {
                APIManager.Instance.SetAccessToken(string.Empty);
            }

            App.Bootstrap.SceneFlowManager.GoToLogin();
        }
    }
}
