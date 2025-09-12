using BIDashboardBackend.Configs;
using BIDashboardBackend.DTOs;
using BIDashboardBackend.DTOs.Request;
using BIDashboardBackend.DTOs.Response;
using BIDashboardBackend.Enums;
using BIDashboardBackend.Models;
using System.Security.Claims;

namespace BIDashboardBackend.Tests.TestHelpers
{
    /// <summary>
    /// 認證測試輔助工具類
    /// </summary>
    public static class AuthTestHelper
    {
        /// <summary>
        /// 創建有效的 OAuth 登入請求
        /// </summary>
        public static OauthLoginRequest CreateValidOauthLoginRequest()
        {
            return new OauthLoginRequest
            {
                IdToken = "valid_firebase_token_12345"
            };
        }

        /// <summary>
        /// 創建無效的 OAuth 登入請求
        /// </summary>
        public static OauthLoginRequest CreateInvalidOauthLoginRequest()
        {
            return new OauthLoginRequest
            {
                IdToken = "invalid_token"
            };
        }

        /// <summary>
        /// 創建空的 OAuth 登入請求
        /// </summary>
        public static OauthLoginRequest CreateEmptyOauthLoginRequest()
        {
            return new OauthLoginRequest
            {
                IdToken = string.Empty
            };
        }

        /// <summary>
        /// 創建有效的刷新權杖請求
        /// </summary>
        public static RefreshTokenRequest CreateValidRefreshTokenRequest()
        {
            return new RefreshTokenRequest
            {
                RefreshToken = "valid_refresh_token_12345"
            };
        }

        /// <summary>
        /// 創建無效的刷新權杖請求
        /// </summary>
        public static RefreshTokenRequest CreateInvalidRefreshTokenRequest()
        {
            return new RefreshTokenRequest
            {
                RefreshToken = "invalid_refresh_token"
            };
        }

        /// <summary>
        /// 創建有效的登出請求
        /// </summary>
        public static LogoutRequest CreateValidLogoutRequest()
        {
            return new LogoutRequest
            {
                RefreshToken = "valid_refresh_token_12345"
            };
        }

        /// <summary>
        /// 創建有效的 Firebase 認證回應
        /// </summary>
        public static FirebaseAuthResponse CreateValidFirebaseAuthResponse()
        {
            return new FirebaseAuthResponse
            {
                Users = new List<FirebaseUser>
                {
                    new FirebaseUser
                    {
                        Uid = "firebase_uid_12345",
                        Email = "test@example.com",
                        DisplayName = "Test User",
                        ProviderUserInfos = new List<ProviderUserInfo>
                        {
                            new ProviderUserInfo
                            {
                                ProviderId = "google.com",
                                Email = "test@example.com",
                                DisplayName = "Test User"
                            }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// 創建無效的 Firebase 認證回應
        /// </summary>
        public static FirebaseAuthResponse CreateInvalidFirebaseAuthResponse()
        {
            return new FirebaseAuthResponse
            {
                Users = new List<FirebaseUser>()
            };
        }

        /// <summary>
        /// 創建有效的認證結果
        /// </summary>
        public static AuthResult CreateValidAuthResult()
        {
            return new AuthResult
            {
                Status = AuthStatus.SuccessExistingUser,
                Jwt = "valid_jwt_token_12345",
                RefreshToken = "valid_refresh_token_12345",
                User = new UserDto
                {
                    Id = 12345,
                    Email = "test@example.com",
                    DisplayName = "Test User"
                }
            };
        }

        /// <summary>
        /// 創建失敗的認證結果
        /// </summary>
        public static AuthResult CreateFailedAuthResult()
        {
            return new AuthResult
            {
                Status = AuthStatus.InvalidToken,
                Message = "認證失敗"
            };
        }

        /// <summary>
        /// 創建有效的 Claims Principal
        /// </summary>
        public static ClaimsPrincipal CreateValidClaimsPrincipal()
        {
            var claims = new List<Claim>
            {
                new Claim("sub", "12345"),
                new Claim(ClaimTypes.NameIdentifier, "12345"),
                new Claim(ClaimTypes.Email, "test@example.com"),
                new Claim(ClaimTypes.Name, "Test User")
            };

            var identity = new ClaimsIdentity(claims, "Test");
            return new ClaimsPrincipal(identity);
        }

        /// <summary>
        /// 創建無效的 Claims Principal
        /// </summary>
        public static ClaimsPrincipal CreateInvalidClaimsPrincipal()
        {
            var claims = new List<Claim>();
            var identity = new ClaimsIdentity(claims, "Test");
            return new ClaimsPrincipal(identity);
        }

        /// <summary>
        /// 創建測試用的 JWT 選項
        /// </summary>
        public static JwtOptions CreateTestJwtOptions()
        {
            return new JwtOptions
            {
                SecretKey = "test_secret_key_that_is_long_enough_for_hmac_sha256",
                Issuer = "test_issuer",
                Audience = "test_audience",
                AccessTokenExpirationMinutes = 60,
                RefreshTokenExpirationDays = 7
            };
        }

        /// <summary>
        /// 創建測試用的用戶資料
        /// </summary>
        public static User CreateTestUser()
        {
            return new User
            {
                Id = 12345,
                Uid = "firebase_uid_12345",
                Email = "test@example.com",
                DisplayName = "Test User",
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                LastLoginAt = DateTime.UtcNow.AddHours(-1)
            };
        }
    }
}
