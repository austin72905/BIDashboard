using BIDashboardBackend.Caching;
using BIDashboardBackend.Configs;
using BIDashboardBackend.DTOs;
using BIDashboardBackend.DTOs.Request;
using BIDashboardBackend.Enums;
using BIDashboardBackend.Interfaces;
using BIDashboardBackend.Interfaces.Repositories;
using BIDashboardBackend.Models;
using BIDashboardBackend.Services;
using BIDashboardBackend.Tests.TestHelpers;
using BIDashboardBackend.Utils;
using Microsoft.Extensions.Options;

namespace BIDashboardBackend.Tests.Services
{
    /// <summary>
    /// 認證服務測試
    /// </summary>
    [TestFixture]
    public class AuthServiceTests
    {
        private AuthService _authService;
        private Mock<IUnitOfWork> _mockUnitOfWork;
        private Mock<IUserRepository> _mockUserRepository;
        private Mock<IJwtTokenService> _mockJwtTokenService;
        private Mock<ICacheService> _mockCacheService;
        private Mock<IOptions<JwtOptions>> _mockJwtOptions;
        private Mock<IOptions<RedisOptions>> _mockRedisOptions;

        [SetUp]
        public void Setup()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockJwtTokenService = new Mock<IJwtTokenService>();
            _mockCacheService = new Mock<ICacheService>();
            
            // 設定 JWT 選項
            var jwtOptions = new JwtOptions
            {
                SecretKey = "test_secret_key_that_is_long_enough_for_hmac_sha256",
                Issuer = "test_issuer",
                Audience = "test_audience",
                AccessTokenExpirationMinutes = 60,
                RefreshTokenExpirationDays = 7
            };
            _mockJwtOptions = new Mock<IOptions<JwtOptions>>();
            _mockJwtOptions.Setup(x => x.Value).Returns(jwtOptions);

            // 設定 Redis 選項
            var redisOptions = new RedisOptions
            {
                KeyPrefix = "test_prefix"
            };
            _mockRedisOptions = new Mock<IOptions<RedisOptions>>();
            _mockRedisOptions.Setup(x => x.Value).Returns(redisOptions);

            _authService = new AuthService(
                _mockUnitOfWork.Object,
                _mockUserRepository.Object,
                _mockJwtTokenService.Object,
                _mockJwtOptions.Object,
                _mockCacheService.Object,
                _mockRedisOptions.Object);
        }

        #region OauthLogin 測試

        [Test]
        public async Task OauthLogin_WithValidTokenButFirebaseApiFails_ShouldReturnInvalidTokenResult()
        {
            // Arrange
            var firebaseIdToken = "valid_firebase_token_but_api_fails";

            // Act
            var result = await _authService.OauthLogin(firebaseIdToken);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(AuthStatus.InvalidToken);
            result.Message.Should().Be("Invalid Firebase ID token.");
            result.Jwt.Should().BeNull();
            result.RefreshToken.Should().BeNull();
        }

        [Test]
        public async Task OauthLogin_WithInvalidToken_ShouldReturnInvalidTokenResult()
        {
            // Arrange
            var invalidToken = "invalid_firebase_token";

            // Act
            var result = await _authService.OauthLogin(invalidToken);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(AuthStatus.InvalidToken);
            result.Message.Should().Be("Invalid Firebase ID token.");
            result.Jwt.Should().BeNull();
            result.RefreshToken.Should().BeNull();
        }

        [Test]
        public async Task OauthLogin_WithEmptyToken_ShouldReturnInvalidTokenResult()
        {
            // Arrange
            var emptyToken = string.Empty;

            // Act
            var result = await _authService.OauthLogin(emptyToken);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(AuthStatus.InvalidToken);
            result.Message.Should().Be("Invalid Firebase ID token.");
        }

        [Test]
        public async Task OauthLogin_WithNullToken_ShouldReturnInvalidTokenResult()
        {
            // Arrange
            string? nullToken = null;

            // Act
            var result = await _authService.OauthLogin(nullToken!);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(AuthStatus.InvalidToken);
            result.Message.Should().Be("Invalid Firebase ID token.");
        }

        #endregion

        #region RefreshTokenAsync 測試

        [Test]
        public async Task RefreshTokenAsync_WithValidToken_ShouldReturnNewTokens()
        {
            // Arrange
            var refreshToken = "valid_refresh_token_12345";
            var newJwtToken = "new_jwt_token_12345";
            var newRefreshToken = "new_refresh_token_12345";

            var user = new User
            {
                Id = 12345,
                Uid = "firebase_uid_12345",
                Email = "test@example.com",
                DisplayName = "Test User",
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                UpdatedAt = DateTime.UtcNow
            };

            // 創建一個簡單的 JSON 字串來模擬快取數據
            var cachedJson = $@"{{
                ""User"": {{
                    ""Id"": {user.Id},
                    ""Email"": ""{user.Email}"",
                    ""DisplayName"": ""{user.DisplayName}"",
                    ""Uid"": ""{user.Uid}"",
                    ""LastLoginAt"": null,
                    ""CreatedAt"": ""{user.CreatedAt:yyyy-MM-dd HH:mm:ss}"",
                    ""UpdatedAt"": ""{user.UpdatedAt:yyyy-MM-dd HH:mm:ss}""
                }},
                ""Expiration"": ""{DateTime.UtcNow.AddDays(7):yyyy-MM-dd HH:mm:ss}""
            }}";

            _mockCacheService.Setup(x => x.GetStringAsync($"test_prefix:refresh:{refreshToken}"))
                           .ReturnsAsync(cachedJson);
            _mockJwtTokenService.Setup(x => x.Generate(It.IsAny<User>(), It.IsAny<TimeSpan?>(), It.IsAny<IDictionary<string, string>?>()))
                              .Returns(newJwtToken);
            _mockJwtTokenService.Setup(x => x.GenerateRefreshToken())
                              .Returns(newRefreshToken);

            // Act
            var result = await _authService.RefreshTokenAsync(refreshToken);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(AuthStatus.SuccessExistingUser);
            result.Jwt.Should().Be(newJwtToken);
            result.RefreshToken.Should().Be(newRefreshToken);
            result.User.Should().NotBeNull();
        }

        [Test]
        public async Task RefreshTokenAsync_WithInvalidToken_ShouldReturnInvalidTokenResult()
        {
            // Arrange
            var invalidToken = "invalid_refresh_token";

            _mockCacheService.Setup(x => x.GetStringAsync($"test_prefix:refresh:{invalidToken}"))
                           .ReturnsAsync((string?)null);

            // Act
            var result = await _authService.RefreshTokenAsync(invalidToken);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(AuthStatus.InvalidToken);
            result.Message.Should().Be("Refresh token 無效");
        }

        [Test]
        public async Task RefreshTokenAsync_WithExpiredToken_ShouldReturnInvalidTokenResult()
        {
            // Arrange
            var expiredToken = "expired_refresh_token";

            var user = new User
            {
                Id = 12345,
                Uid = "firebase_uid_12345",
                Email = "test@example.com",
                DisplayName = "Test User"
            };

            var expiredRefreshTokenCache = new RefreshTokenCache(user, DateTime.UtcNow.AddDays(-1)); // 已過期
            var cachedJson = Json.Serialize(expiredRefreshTokenCache);

            _mockCacheService.Setup(x => x.GetStringAsync($"test_prefix:refresh:{expiredToken}"))
                           .ReturnsAsync(cachedJson);

            // Act
            var result = await _authService.RefreshTokenAsync(expiredToken);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(AuthStatus.InvalidToken);
            result.Message.Should().Be("Refresh token 已過期");
        }

        #endregion

        #region LogoutAsync 測試

        [Test]
        public async Task LogoutAsync_WithValidToken_ShouldReturnSuccessResult()
        {
            // Arrange
            var refreshToken = "valid_refresh_token_12345";

            var user = new User
            {
                Id = 12345,
                Uid = "firebase_uid_12345",
                Email = "test@example.com",
                DisplayName = "Test User"
            };

            var refreshTokenCache = new RefreshTokenCache(user, DateTime.UtcNow.AddDays(7));
            var cachedJson = Json.Serialize(refreshTokenCache);

            _mockCacheService.Setup(x => x.GetStringAsync($"test_prefix:refresh:{refreshToken}"))
                           .ReturnsAsync(cachedJson);

            // Act
            var result = await _authService.LogoutAsync(refreshToken);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(AuthStatus.SuccessExistingUser);
            result.Message.Should().Be("登出成功，refresh token 已撤銷");
        }

        [Test]
        public async Task LogoutAsync_WithInvalidToken_ShouldReturnSuccessResult()
        {
            // Arrange
            var invalidToken = "invalid_refresh_token";

            _mockCacheService.Setup(x => x.GetStringAsync($"test_prefix:refresh:{invalidToken}"))
                           .ReturnsAsync((string?)null);

            // Act
            var result = await _authService.LogoutAsync(invalidToken);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(AuthStatus.SuccessExistingUser);
            result.Message.Should().Be("已登出（refresh token 不存在或已過期）");
        }

        [Test]
        public async Task LogoutAsync_WhenCacheThrowsException_ShouldReturnSuccessResult()
        {
            // Arrange
            var refreshToken = "token_that_causes_exception";

            _mockCacheService.Setup(x => x.GetStringAsync($"test_prefix:refresh:{refreshToken}"))
                           .ThrowsAsync(new Exception("Cache error"));

            // Act
            var result = await _authService.LogoutAsync(refreshToken);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(AuthStatus.SuccessExistingUser);
            result.Message.Should().Contain("登出完成，但清理過程發生錯誤");
        }

        #endregion

        #region 邊界條件測試

        [Test]
        public async Task OauthLogin_WithVeryLongToken_ShouldHandleGracefully()
        {
            // Arrange
            var longToken = new string('a', 10000); // 10KB token

            // Act
            var result = await _authService.OauthLogin(longToken);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(AuthStatus.InvalidToken);
            result.Message.Should().Be("Invalid Firebase ID token.");
        }

        [Test]
        public async Task OauthLogin_WithSpecialCharactersInToken_ShouldHandleGracefully()
        {
            // Arrange
            var specialToken = "token_with_special_chars_!@#$%^&*()_+{}|:<>?[]\\;'\",./";

            // Act
            var result = await _authService.OauthLogin(specialToken);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(AuthStatus.InvalidToken);
            result.Message.Should().Be("Invalid Firebase ID token.");
        }

        #endregion

        #region 性能測試

        [Test]
        public async Task OauthLogin_WithMultipleConcurrentRequests_ShouldHandleCorrectly()
        {
            // Arrange
            var tokens = Enumerable.Range(1, 10)
                                 .Select(i => $"token_{i}")
                                 .ToList();

            // Act
            var tasks = tokens.Select(token => _authService.OauthLogin(token));
            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().HaveCount(10);
            results.Should().AllSatisfy(result => result.Should().NotBeNull());
        }

        #endregion

        #region 內部類別定義

        /// <summary>
        /// 用於測試的刷新權杖快取
        /// </summary>
        private record RefreshTokenCache(User User, DateTime Expiration);

        #endregion
    }
}
