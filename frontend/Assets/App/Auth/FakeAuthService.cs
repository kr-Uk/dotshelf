using System.Threading.Tasks;

namespace App.Auth
{
    public sealed class FakeAuthService : IAuthService
    {
        public Task<LoginResult> LoginAsync(string userId, string password)
        {
            var effectiveId = string.IsNullOrWhiteSpace(userId) ? "guest" : userId;
            return Task.FromResult(LoginResult.Success(effectiveId));
        }
    }
}
