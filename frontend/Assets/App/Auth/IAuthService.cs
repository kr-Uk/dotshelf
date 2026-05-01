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

        // BE: POST /v1/auth/social-login/code. Desktop browser-OAuth flow:
        // Unity opens the provider's authorize page in an external browser,
        // captures the redirect to a loopback URL, and ships the resulting
        // authorization code here. BE swaps the code for tokens server-side
        // using its own client_secret and the same redirectUri.
        Task<LoginResult> SocialLoginByCodeAsync(
            SocialProvider provider,
            string code,
            string redirectUri,
            string state,
            string nonce = null);

        // BE: POST /v1/auth/guest-session. Returns a session id that later
        // ships with /v1/auth/guest-migration once the user logs in.
        Task<GuestSessionResult> StartGuestSessionAsync();
    }
}
