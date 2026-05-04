namespace App.Domain
{
    public static class ApiEndpointDefaults
    {
        public const string DevBaseUrl = "https://dev-api.dotshelf.digital";
        public const string ProdBaseUrl = "https://api.dotshelf.digital";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public const string DefaultBaseUrl = DevBaseUrl;
#else
        public const string DefaultBaseUrl = ProdBaseUrl;
#endif

        public const string HealthCheckPath = "/health";
    }
}
