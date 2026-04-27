using System.Threading.Tasks;

namespace App.Auth
{
    public interface IAuthService
    {
        Task<LoginResult> LoginAsync(string userId, string password);
    }
}
