using BIDashboardBackend.DTOs;

namespace BIDashboardBackend.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResult> OauthLogin(string firebaseIdToken);

    }
}
