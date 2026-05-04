using System;
using App.Domain.Auth;
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

}
