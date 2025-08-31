using BIDashboardBackend.DTOs;
using BIDashboardBackend.DTOs.Response;
using BIDashboardBackend.Enums;
using BIDashboardBackend.Extensions;
using BIDashboardBackend.Interfaces;
using BIDashboardBackend.Interfaces.Repositories;
using BIDashboardBackend.Models;
using BIDashboardBackend.Utils;
using BIDashboardBackend.Configs;
using BIDashboardBackend.Caching;
using Microsoft.Extensions.Options;
using System.Text;

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
        private readonly ICacheService _cache;        // Redis 快取服務，用於儲存刷新權杖
        private readonly string _refreshPrefix;       // 統一的刷新權杖鍵值前綴

        public AuthService(
            IUnitOfWork uow,
            IUserRepository users,
            IJwtTokenService jwt,
            IOptions<JwtOptions> jwtOpt,
            ICacheService cache,
            IOptions<RedisOptions> redisOpt)
        {
            _httpClient = new HttpClient();
            _uow = uow;
            _userRepo = users;
            _jwt = jwt;
            _jwtOpt = jwtOpt.Value;
            _cache = cache;
            _refreshPrefix = $"{redisOpt.Value.KeyPrefix}:refresh:";
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

                // 將刷新權杖與使用者資訊保存於 Redis，並設定過期時間
                var info = new RefreshTokenCache(user!, DateTime.UtcNow.AddDays(_jwtOpt.RefreshTokenExpirationDays));
                var key = _refreshPrefix + refresh;
                await _cache.SetStringAsync(key, Json.Serialize(info), TimeSpan.FromDays(_jwtOpt.RefreshTokenExpirationDays));

                result.Jwt = jwt;
                result.RefreshToken = refresh;
            }

            return result;
        }

        /// <summary>
        /// 以刷新權杖換取新的存取權杖
        /// </summary>
        public async Task<AuthResult> RefreshTokenAsync(string refreshToken)
        {
            /*
             * 發新的 access token 給 用戶
                refresh token 也會跟著刷新，並刷新期限
                如果refresh token 洩漏，只要將 refresh token 從 redis 移除就可
             
             */
            var key = _refreshPrefix + refreshToken;
            // 從 Redis 取得刷新權杖資訊
            var cached = await _cache.GetStringAsync(key);
            if (cached is null)
            {
                return new AuthResult { Status = AuthStatus.InvalidToken, Message = "Refresh token 無效" };
            }

            var info = Json.Deserialize<RefreshTokenCache>(cached);
            if (info.Expiration <= DateTime.UtcNow)
            {
                await _cache.RemoveByPrefixAsync(key);
                return new AuthResult { Status = AuthStatus.InvalidToken, Message = "Refresh token 已過期" };
            }

            var newJwt = _jwt.Generate(info.User);
            var newRefresh = _jwt.GenerateRefreshToken();
            await _cache.RemoveByPrefixAsync(key);

            var newInfo = new RefreshTokenCache(info.User, DateTime.UtcNow.AddDays(_jwtOpt.RefreshTokenExpirationDays));
            var newKey = _refreshPrefix + newRefresh;
            await _cache.SetStringAsync(newKey, Json.Serialize(newInfo), TimeSpan.FromDays(_jwtOpt.RefreshTokenExpirationDays));

            return new AuthResult
            {
                Status = AuthStatus.SuccessExistingUser,
                Jwt = newJwt,
                RefreshToken = newRefresh,
                User = info.User.ToDto()
            };
        }

        /// <summary>
        /// 用於儲存於 Redis 的刷新權杖資料模型
        /// </summary>
        private record RefreshTokenCache(User User, DateTime Expiration);

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

