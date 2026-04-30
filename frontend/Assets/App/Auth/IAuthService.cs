using System.Threading.Tasks;

namespace App.Auth
{
    public enum SocialProvider
    {
        Google,
        Kakao,
        Apple
    }

    public enum SocialTokenType
    {
        Default,
        AccessToken,
        IdToken,
        AuthorizationCode
    }

    public interface IAuthService
    {
        // BE: POST /v1/auth/social-login. The provider-specific token is
        // obtained from the platform SDK upstream and handed in here.
        Task<LoginResult> SocialLoginAsync(
            SocialProvider provider,
            string providerToken,
            SocialTokenType tokenType = SocialTokenType.Default,
            string nonce = null);

        // BE: POST /v1/auth/guest-session. Returns a session id that later
        // ships with /v1/auth/guest-migration once the user logs in.
        Task<GuestSessionResult> StartGuestSessionAsync();
    }
}
