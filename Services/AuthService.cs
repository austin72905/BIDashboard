using BIDashboardBackend.DTOs;
using BIDashboardBackend.DTOs.Response;
using BIDashboardBackend.Enums;
using BIDashboardBackend.Extensions;
using BIDashboardBackend.Interfaces;
using BIDashboardBackend.Interfaces.Repositories;
using BIDashboardBackend.Models;
using BIDashboardBackend.Utils;
using BIDashboardBackend.Configs;
using Microsoft.Extensions.Options;

namespace BIDashboardBackend.Services
{
    /// <summary>
    /// 提供登入與刷新權杖等認證相關功能的服務實作
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly IUnitOfWork _uow;
        private readonly IUserRepository _userRepo;
        private readonly IJwtTokenService _jwt;
        private readonly JwtOptions _jwtOpt;

        // 簡單的記憶體型刷新權杖儲存，實務上應存放於資料庫或快取
        private static readonly Dictionary<string, (User User, DateTime Expiration)> _refreshTokens = new();

        public AuthService(IUnitOfWork uow, IUserRepository users, IJwtTokenService jwt, IOptions<JwtOptions> jwtOpt)
        {
            _httpClient = new HttpClient();
            _uow = uow;
            _userRepo = users;
            _jwt = jwt;
            _jwtOpt = jwtOpt.Value;
        }

        /// <summary>
        /// 透過 Firebase ID Token 進行登入
        /// </summary>
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
                var refresh = _jwt.GenerateRefreshToken();
                _refreshTokens[refresh] = (user!, DateTime.UtcNow.AddDays(_jwtOpt.RefreshTokenExpirationDays));
                result.Jwt = jwt;
                result.RefreshToken = refresh;
            }

            return result;
        }

        /// <summary>
        /// 以刷新權杖換取新的存取權杖
        /// </summary>
        public Task<AuthResult> RefreshTokenAsync(string refreshToken)
        {
            if (!_refreshTokens.TryGetValue(refreshToken, out var info))
            {
                return Task.FromResult(new AuthResult { Status = AuthStatus.InvalidToken, Message = "Refresh token 無效" });
            }

            if (info.Expiration <= DateTime.UtcNow)
            {
                _refreshTokens.Remove(refreshToken);
                return Task.FromResult(new AuthResult { Status = AuthStatus.InvalidToken, Message = "Refresh token 已過期" });
            }

            var newJwt = _jwt.Generate(info.User);
            var newRefresh = _jwt.GenerateRefreshToken();
            _refreshTokens.Remove(refreshToken);
            _refreshTokens[newRefresh] = (info.User, DateTime.UtcNow.AddDays(_jwtOpt.RefreshTokenExpirationDays));

            return Task.FromResult(new AuthResult
            {
                Status = AuthStatus.SuccessExistingUser,
                Jwt = newJwt,
                RefreshToken = newRefresh,
                User = info.User.ToDto()
            });
        }

        /// <summary>
        /// 呼叫 Google API 驗證 Firebase ID Token
        /// </summary>
        private async Task<FirebaseAuthResponse?> VerifyFirebaseIdTokenAsync(string idToken)
        {
            string key = ""; // 這裡應填入實際的 Firebase API Key
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

        /// <summary>
        /// 取得既有使用者或建立新使用者
        /// </summary>
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

