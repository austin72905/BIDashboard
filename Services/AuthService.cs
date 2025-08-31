using BIDashboardBackend.DTOs;
using BIDashboardBackend.DTOs.Response;
using BIDashboardBackend.Enums;
using BIDashboardBackend.Extensions;
using BIDashboardBackend.Interfaces;
using BIDashboardBackend.Interfaces.Repositories;
using BIDashboardBackend.Models;
using BIDashboardBackend.Utils;
using System.Text;

namespace BIDashboardBackend.Services
{
    public class AuthService : IAuthService
    {
        private HttpClient _httpClient;
        private readonly IUnitOfWork _uow;
        private readonly IUserRepository _userRepo;
        private readonly IJwtTokenService _jwt;

        public AuthService(IUnitOfWork uow, IUserRepository users, IJwtTokenService jwt)
        {
            _httpClient = new HttpClient();
            _uow = uow;
            _userRepo = users;
            _jwt = jwt;
        }

        public async Task<AuthResult> OauthLogin(string firebaseIdToken)
        {
            var resp = await VerifyFirebaseIdTokenAsync(firebaseIdToken);
            if (resp is null || resp.Users is null || resp.Users.Count == 0)
            {
                return new AuthResult { Status = AuthStatus.InvalidToken, Message = "Invalid Firebase ID token." };
            }

            var fu = resp.Users[0];
            var firebaseUid = fu.Uid;
            var email = fu.Email ?? fu.ProviderUserInfos?.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Email))?.Email;
            var displayName = fu.DisplayName;

            var result = await GetOrCreateUserAsync(firebaseUid, email, displayName);

            if (result.Status == AuthStatus.SuccessExistingUser || result.Status == AuthStatus.SuccessNewUser)
            {
                var user = await _userRepo.GetByFirebaseUidAsync(firebaseUid);
                var jwt = _jwt.Generate(user!);
                result.Jwt = jwt;
            }

            return result;
        }

        private async Task<FirebaseAuthResponse?> VerifyFirebaseIdTokenAsync(string idToken)
        {
            string key = "";
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:lookup?key={key}";
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new { idToken }), Encoding.UTF8, "application/json")
            };
            using var response = await _httpClient.SendAsync(req);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return Json.Deserialize<FirebaseAuthResponse>(json);
        }

        private async Task<AuthResult> GetOrCreateUserAsync(string firebaseUid, string? email, string? displayName)
        {
            var byUid = await _userRepo.GetByFirebaseUidAsync(firebaseUid);
            if (byUid != null)
            {
                byUid.Email = email ?? byUid.Email;
                byUid.DisplayName = displayName ?? byUid.DisplayName;
                byUid.LastLoginAt = DateTime.UtcNow;
                await _userRepo.UpdateAsync(byUid);

                return new AuthResult
                {
                    Status = AuthStatus.SuccessExistingUser,
                    User = byUid.ToDto()
                };
            }

            var created = await _userRepo.CreateAsync(new User
            {
                Uid = firebaseUid,
                Email = email,
                DisplayName = displayName,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            });

            return new AuthResult
            {
                Status = AuthStatus.SuccessNewUser,
                User = created.ToDto()
            };
        }

       

    }
}
