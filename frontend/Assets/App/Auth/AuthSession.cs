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
        public static bool HasAccessToken => !string.IsNullOrWhiteSpace(AccessToken);

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
            // 새 토큰 저장 전 옛 token / userId / 만료 값을 명시적으로 지움. SetString 으로 덮어쓰기 만으로
            // 충분해 보이지만, 호출자가 부분 응답 (refreshToken null 등) 만 set 하면 stale 값 잔류 가능.
            // 모든 키를 한 번 비운 뒤 재저장 → 새 세션 = 옛 세션 흔적 0 보장.
            PlayerPrefs.DeleteKey(AuthStorageKeys.AccessToken);
            PlayerPrefs.DeleteKey(AuthStorageKeys.RefreshToken);
            PlayerPrefs.DeleteKey(AuthStorageKeys.UserId);
            PlayerPrefs.DeleteKey(AuthStorageKeys.ExpiresInSeconds);
            PlayerPrefs.DeleteKey(AuthStorageKeys.AccessTokenExpiresAt);

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

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(AuthStorageKeys.AccessToken);
            PlayerPrefs.DeleteKey(AuthStorageKeys.RefreshToken);
            PlayerPrefs.DeleteKey(AuthStorageKeys.UserId);
            PlayerPrefs.DeleteKey(AuthStorageKeys.ExpiresInSeconds);
            PlayerPrefs.DeleteKey(AuthStorageKeys.AccessTokenExpiresAt);
            PlayerPrefs.DeleteKey(AuthStorageKeys.NewUser);
            PlayerPrefs.DeleteKey(AuthStorageKeys.Onboarded);
            PlayerPrefs.Save();
        }
    }

}
