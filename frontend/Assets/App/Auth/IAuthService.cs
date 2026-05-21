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
            string nonce = null,
            string redirectUri = null);

        // BE: POST /v1/auth/social-login (tokenType=AUTHORIZATION_CODE). Desktop browser-OAuth
        // flow with PKCE (RFC 7636 + 8252). Unity opens the provider's authorize page in an
        // external browser, captures the redirect to a loopback URL, and ships the resulting
        // authorization code + the original PKCE code_verifier here. BE forwards both to the
        // OAuth provider's token endpoint — provider verifies SHA256(code_verifier) matches
        // the code_challenge sent at authorize time, no client_secret required (Desktop client는
        // public client). codeVerifier null이면 BE는 legacy server pair(client_secret) fallback.
        Task<LoginResult> SocialLoginByCodeAsync(
            SocialProvider provider,
            string code,
            string redirectUri,
            string state,
            string nonce = null,
            string codeVerifier = null);
    }
}
