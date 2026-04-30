using System;
using System.Threading.Tasks;
using App.Infrastructure.Api.Dtos;

namespace App.Auth
{
    // Real implementation that talks to the BE auth endpoints. The file is
    // still named FakeAuthService.cs to keep its meta GUID stable; the
    // contained type is BackendAuthService.
    public sealed class BackendAuthService : IAuthService
    {
        private readonly AuthApiClient client;

        public BackendAuthService(AuthApiClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public Task<LoginResult> SocialLoginAsync(
            SocialProvider provider,
            string providerToken,
            SocialTokenType tokenType = SocialTokenType.Default,
            string nonce = null)
        {
            if (string.IsNullOrWhiteSpace(providerToken))
            {
                return Task.FromResult(LoginResult.Failure(
                    "INVALID_INPUT",
                    "\uC18C\uC15C \uB85C\uADF8\uC778 \uD1A0\uD070\uC774 \uBE44\uC5B4 \uC788\uC2B5\uB2C8\uB2E4."));
            }

            if (provider == SocialProvider.Apple && string.IsNullOrWhiteSpace(nonce))
            {
                return Task.FromResult(LoginResult.Failure(
                    "INVALID_INPUT",
                    "Apple \uB85C\uADF8\uC778\uC5D0\uB294 nonce\uAC00 \uD544\uC694\uD569\uB2C8\uB2E4."));
            }

            var request = new SocialLoginRequestDto
            {
                Provider = provider.ToBackendString(),
                Token = providerToken,
                TokenType = tokenType.ToBackendString(provider),
                Nonce = nonce
            };

            var tcs = new TaskCompletionSource<LoginResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.PostSocialLogin(
                request,
                response =>
                {
                    if (response == null || string.IsNullOrWhiteSpace(response.AccessToken))
                    {
                        tcs.TrySetResult(LoginResult.Failure(
                            "EMPTY_RESPONSE",
                            "\uC11C\uBC84 \uC751\uB2F5\uC774 \uC62C\uBC14\uB974\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4. \uC7A0\uC2DC \uD6C4 \uB2E4\uC2DC \uC2DC\uB3C4\uD574\uC8FC\uC138\uC694."));
                        return;
                    }

                    tcs.TrySetResult(LoginResult.Success(
                        response.UserId,
                        response.AccessToken,
                        response.RefreshToken,
                        response.ExpiresIn,
                        response.NewUser,
                        response.Onboarded));

                    AuthSession.SaveTokens(
                        response.UserId,
                        response.AccessToken,
                        response.RefreshToken,
                        response.ExpiresIn,
                        response.NewUser,
                        response.Onboarded);

                    if (!SessionState.IsGuest)
                    {
                        AuthSession.SaveGuestSessionId(string.Empty);
                        SessionState.SetPendingIntent(GuestPostLoginIntent.None);
                    }
                },
                error => tcs.TrySetResult(LoginResult.Failure(
                    error?.Code,
                    AuthErrorMessages.ResolveLoginMessage(error))));

            return tcs.Task;
        }

        public Task<GuestSessionResult> StartGuestSessionAsync()
        {
            var tcs = new TaskCompletionSource<GuestSessionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.PostGuestSession(
                response =>
                {
                    if (response == null || string.IsNullOrWhiteSpace(response.GuestSessionId))
                    {
                        tcs.TrySetResult(GuestSessionResult.Failure(
                            "EMPTY_RESPONSE",
                            "\uAC8C\uC2A4\uD2B8 \uC138\uC158\uC744 \uC2DC\uC791\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. \uC7A0\uC2DC \uD6C4 \uB2E4\uC2DC \uC2DC\uB3C4\uD574\uC8FC\uC138\uC694."));
                        return;
                    }

                    tcs.TrySetResult(GuestSessionResult.Success(
                        response.GuestSessionId,
                        response.GuestToken,
                        response.ExpiresInSeconds));
                },
                error => tcs.TrySetResult(GuestSessionResult.Failure(
                    error?.Code,
                    AuthErrorMessages.ResolveGuestMessage(error))));

            return tcs.Task;
        }
    }

    internal static class SocialEnumExtensions
    {
        public static string ToBackendString(this SocialProvider provider)
        {
            return provider switch
            {
                SocialProvider.Google => "GOOGLE",
                SocialProvider.Kakao => "KAKAO",
                SocialProvider.Apple => "APPLE",
                _ => provider.ToString().ToUpperInvariant()
            };
        }

        public static string ToBackendString(this SocialTokenType tokenType, SocialProvider provider)
        {
            return tokenType switch
            {
                SocialTokenType.AccessToken => "ACCESS_TOKEN",
                SocialTokenType.IdToken => "ID_TOKEN",
                SocialTokenType.AuthorizationCode => "AUTHORIZATION_CODE",
                _ => provider switch
                {
                    SocialProvider.Kakao => "ACCESS_TOKEN",
                    SocialProvider.Google => "ID_TOKEN",
                    SocialProvider.Apple => "ID_TOKEN",
                    _ => "ID_TOKEN"
                }
            };
        }
    }
}
