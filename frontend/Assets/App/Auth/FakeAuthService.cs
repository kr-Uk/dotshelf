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
                    "소셜 로그인 토큰이 비어 있습니다."));
            }

            var request = new SocialLoginRequestDto
            {
                Provider = provider.ToBackendString(),
                Token = providerToken,
                TokenType = tokenType == SocialTokenType.Default ? null : tokenType.ToBackendString(),
                Nonce = string.IsNullOrWhiteSpace(nonce) ? null : nonce
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
                            "서버 응답이 올바르지 않습니다. 잠시 후 다시 시도해주세요."));
                        return;
                    }

                    tcs.TrySetResult(LoginResult.Success(
                        response.UserId,
                        response.AccessToken,
                        response.RefreshToken,
                        response.ExpiresIn,
                        response.NewUser,
                        response.Onboarded));
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
                            "게스트 세션을 시작할 수 없습니다. 잠시 후 다시 시도해주세요."));
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

        public static string ToBackendString(this SocialTokenType tokenType)
        {
            return tokenType switch
            {
                SocialTokenType.AccessToken => "ACCESS_TOKEN",
                SocialTokenType.IdToken => "ID_TOKEN",
                SocialTokenType.AuthorizationCode => "AUTHORIZATION_CODE",
                _ => null
            };
        }
    }
}
