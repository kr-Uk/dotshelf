namespace App.Domain.Auth
{
    public static class AuthStorageKeys
    {
        public const string AccessToken = "Dotshelf.Auth.AccessToken";
        public const string RefreshToken = "Dotshelf.Auth.RefreshToken";
        public const string UserId = "Dotshelf.Auth.UserId";
        public const string ExpiresInSeconds = "Dotshelf.Auth.ExpiresInSeconds";
        public const string AccessTokenExpiresAt = "Dotshelf.Auth.AccessTokenExpiresAt";
        public const string NewUser = "Dotshelf.Auth.NewUser";
        public const string Onboarded = "Dotshelf.Auth.Onboarded";
        public const string GuestSessionId = "Dotshelf.Auth.GuestSessionId";
    }
}

namespace App.Domain
{
    public static class ApiEndpointDefaults
    {
        public const string DevBaseUrl = "https://dev-api.dotshelf.digital";
        public const string ProdBaseUrl = "https://dev-api.dotshelf.digital";
        public const string DefaultBaseUrl = DevBaseUrl;
        public const string HealthCheckPath = "/health";
    }
}
