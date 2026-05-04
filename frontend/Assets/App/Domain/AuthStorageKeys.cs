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
