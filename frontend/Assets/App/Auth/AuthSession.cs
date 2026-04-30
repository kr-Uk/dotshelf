using System;
using UnityEngine;

namespace App.Auth
{
    // Persisted authentication artefacts returned by /v1/auth/social-login and
    // /v1/auth/token/refresh. Stored in PlayerPrefs so the next launch can hand
    // a Bearer token to APIManager without re-running the social login flow.
    //
    // PlayerPrefs is a stop-gap; the access pattern is funneled through this
    // single class so the storage layer can later be swapped (e.g. encrypted
    // keystore, server-issued cookie) without touching call sites.
    public static class AuthSession
    {
        private const string AccessTokenKey = "Dotshelf.Auth.AccessToken";
        private const string RefreshTokenKey = "Dotshelf.Auth.RefreshToken";
        private const string UserIdKey = "Dotshelf.Auth.UserId";
        private const string AccessTokenExpiresAtKey = "Dotshelf.Auth.AccessTokenExpiresAt";
        private const string GuestSessionIdKey = "Dotshelf.Auth.GuestSessionId";

        public static string AccessToken => PlayerPrefs.GetString(AccessTokenKey, string.Empty);
        public static string RefreshToken => PlayerPrefs.GetString(RefreshTokenKey, string.Empty);
        public static long UserId
        {
            get
            {
                var raw = PlayerPrefs.GetString(UserIdKey, string.Empty);
                return long.TryParse(raw, out var parsed) ? parsed : 0L;
            }
        }
        public static string GuestSessionId => PlayerPrefs.GetString(GuestSessionIdKey, string.Empty);

        public static bool HasAccessToken => !string.IsNullOrWhiteSpace(AccessToken);
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

        public static void SaveTokens(long userId, string accessToken, string refreshToken, long expiresInSeconds)
        {
            PlayerPrefs.SetString(UserIdKey, userId.ToString());
            PlayerPrefs.SetString(AccessTokenKey, accessToken ?? string.Empty);
            PlayerPrefs.SetString(RefreshTokenKey, refreshToken ?? string.Empty);
            // Subtract a small safety margin so refresh fires before the token
            // is actually rejected by the server.
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
            PlayerPrefs.DeleteKey(AccessTokenExpiresAtKey);
            PlayerPrefs.DeleteKey(GuestSessionIdKey);
            PlayerPrefs.Save();
        }
    }
}
