using System;
using UnityEngine;

namespace App.Auth
{
    // Persisted authentication artefacts returned by /v1/auth/social-login and
    // /v1/auth/token/refresh. Stored in PlayerPrefs so the next launch can hand
    // a Bearer token to APIManager without re-running the social login flow.
    public static class AuthSession
    {
        private const string AccessTokenKey = "Dotshelf.Auth.AccessToken";
        private const string RefreshTokenKey = "Dotshelf.Auth.RefreshToken";
        private const string UserIdKey = "Dotshelf.Auth.UserId";
        private const string ExpiresInSecondsKey = "Dotshelf.Auth.ExpiresInSeconds";
        private const string AccessTokenExpiresAtKey = "Dotshelf.Auth.AccessTokenExpiresAt";
        private const string NewUserKey = "Dotshelf.Auth.NewUser";
        private const string OnboardedKey = "Dotshelf.Auth.Onboarded";
        private const string GuestSessionIdKey = "Dotshelf.Auth.GuestSessionId";

        public static string AccessToken => PlayerPrefs.GetString(AccessTokenKey, string.Empty);
        public static string RefreshToken => PlayerPrefs.GetString(RefreshTokenKey, string.Empty);
        public static string GuestSessionId => PlayerPrefs.GetString(GuestSessionIdKey, string.Empty);
        public static bool HasAccessToken => !string.IsNullOrWhiteSpace(AccessToken);
        public static bool NewUser => PlayerPrefs.GetInt(NewUserKey, 0) == 1;
        public static bool Onboarded => PlayerPrefs.GetInt(OnboardedKey, 0) == 1;

        public static long UserId
        {
            get
            {
                var raw = PlayerPrefs.GetString(UserIdKey, string.Empty);
                return long.TryParse(raw, out var parsed) ? parsed : 0L;
            }
        }

        public static long ExpiresInSeconds
        {
            get
            {
                var raw = PlayerPrefs.GetString(ExpiresInSecondsKey, string.Empty);
                return long.TryParse(raw, out var parsed) ? parsed : 0L;
            }
        }

        public static bool IsAccessTokenExpired
        {
            get
            {
                var raw = PlayerPrefs.GetString(AccessTokenExpiresAtKey, string.Empty);
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
            PlayerPrefs.SetString(UserIdKey, userId.ToString());
            PlayerPrefs.SetString(AccessTokenKey, accessToken ?? string.Empty);
            PlayerPrefs.SetString(RefreshTokenKey, refreshToken ?? string.Empty);
            PlayerPrefs.SetString(ExpiresInSecondsKey, expiresInSeconds.ToString());
            if (newUser.HasValue)
            {
                PlayerPrefs.SetInt(NewUserKey, newUser.Value ? 1 : 0);
            }

            if (onboarded.HasValue)
            {
                PlayerPrefs.SetInt(OnboardedKey, onboarded.Value ? 1 : 0);
            }

            var safety = Math.Min(60L, Math.Max(0L, expiresInSeconds / 10));
            var expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Math.Max(0L, expiresInSeconds - safety);
            PlayerPrefs.SetString(AccessTokenExpiresAtKey, expiresAt.ToString());
            PlayerPrefs.Save();
        }

        public static void SaveGuestSessionId(string guestSessionId)
        {
            PlayerPrefs.SetString(GuestSessionIdKey, guestSessionId ?? string.Empty);
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(AccessTokenKey);
            PlayerPrefs.DeleteKey(RefreshTokenKey);
            PlayerPrefs.DeleteKey(UserIdKey);
            PlayerPrefs.DeleteKey(ExpiresInSecondsKey);
            PlayerPrefs.DeleteKey(AccessTokenExpiresAtKey);
            PlayerPrefs.DeleteKey(NewUserKey);
            PlayerPrefs.DeleteKey(OnboardedKey);
            PlayerPrefs.DeleteKey(GuestSessionIdKey);
            PlayerPrefs.Save();
        }
    }
}
