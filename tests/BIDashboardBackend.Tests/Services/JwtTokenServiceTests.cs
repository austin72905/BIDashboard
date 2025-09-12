using BIDashboardBackend.Configs;
using BIDashboardBackend.Models;
using BIDashboardBackend.Services;
using BIDashboardBackend.Tests.TestHelpers;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace BIDashboardBackend.Tests.Services
{
    /// <summary>
    /// JWT Token 服務測試
    /// </summary>
    [TestFixture]
    public class JwtTokenServiceTests
    {
        private JwtTokenService _jwtTokenService;
        private JwtOptions _jwtOptions;

        [SetUp]
        public void Setup()
        {
            _jwtOptions = AuthTestHelper.CreateTestJwtOptions();
            var options = Options.Create(_jwtOptions);
            _jwtTokenService = new JwtTokenService(options);
        }

        #region Generate 測試

        [Test]
        public void Generate_WithValidUser_ShouldReturnValidToken()
        {
            // Arrange
            var user = AuthTestHelper.CreateTestUser();

            // Act
            var token = _jwtTokenService.Generate(user);

            // Assert
            token.Should().NotBeNullOrEmpty();
            
            // 驗證 Token 格式
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            
            jsonToken.Should().NotBeNull();
            jsonToken.Claims.Should().Contain(c => c.Type == "sub" && c.Value == user.Id.ToString());
            jsonToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Iss);
            jsonToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Aud);
            jsonToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Exp);
        }

        [Test]
        public void Generate_WithValidUserAndCustomLifetime_ShouldReturnValidToken()
        {
            // Arrange
            var user = AuthTestHelper.CreateTestUser();
            var customLifetime = TimeSpan.FromHours(2);

            // Act
            var token = _jwtTokenService.Generate(user, customLifetime);

            // Assert
            token.Should().NotBeNullOrEmpty();
            
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            
            jsonToken.Should().NotBeNull();
            jsonToken.Claims.Should().Contain(c => c.Type == "sub" && c.Value == user.Id.ToString());
        }

        [Test]
        public void Generate_WithValidUserAndExtraClaims_ShouldReturnValidToken()
        {
            // Arrange
            var user = AuthTestHelper.CreateTestUser();
            var extraClaims = new Dictionary<string, string>
            {
                { "role", "admin" },
                { "department", "IT" }
            };

            // Act
            var token = _jwtTokenService.Generate(user, null, extraClaims);

            // Assert
            token.Should().NotBeNullOrEmpty();
            
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            
            jsonToken.Should().NotBeNull();
            jsonToken.Claims.Should().Contain(c => c.Type == "sub" && c.Value == user.Id.ToString());
            jsonToken.Claims.Should().Contain(c => c.Type == "role" && c.Value == "admin");
            jsonToken.Claims.Should().Contain(c => c.Type == "department" && c.Value == "IT");
        }

        [Test]
        public void Generate_WithNullUser_ShouldThrowNullReferenceException()
        {
            // Arrange
            User? nullUser = null;

            // Act & Assert
            var action = () => _jwtTokenService.Generate(nullUser!);
            action.Should().Throw<NullReferenceException>();
        }

        [Test]
        public void Generate_WithUserWithZeroId_ShouldReturnValidToken()
        {
            // Arrange
            var user = new User { Id = 0, Uid = "test_uid", Email = "test@example.com" };

            // Act
            var token = _jwtTokenService.Generate(user);

            // Assert
            token.Should().NotBeNullOrEmpty();
            
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            jsonToken.Claims.Should().Contain(c => c.Type == "sub" && c.Value == "0");
        }

        [Test]
        public void Generate_WithUserWithNegativeId_ShouldReturnValidToken()
        {
            // Arrange
            var user = new User { Id = -1, Uid = "test_uid", Email = "test@example.com" };

            // Act
            var token = _jwtTokenService.Generate(user);

            // Assert
            token.Should().NotBeNullOrEmpty();
            
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            jsonToken.Claims.Should().Contain(c => c.Type == "sub" && c.Value == "-1");
        }

        #endregion

        #region GenerateRefreshToken 測試

        [Test]
        public void GenerateRefreshToken_ShouldReturnValidToken()
        {
            // Act
            var refreshToken = _jwtTokenService.GenerateRefreshToken();

            // Assert
            refreshToken.Should().NotBeNullOrEmpty();
            refreshToken.Should().HaveLength(44); // 32 位元組轉 Base64 後是 44 字元
        }

        [Test]
        public void GenerateRefreshToken_MultipleCalls_ShouldReturnDifferentTokens()
        {
            // Act
            var token1 = _jwtTokenService.GenerateRefreshToken();
            var token2 = _jwtTokenService.GenerateRefreshToken();

            // Assert
            token1.Should().NotBeNullOrEmpty();
            token2.Should().NotBeNullOrEmpty();
            token1.Should().NotBe(token2);
        }

        [Test]
        public void GenerateRefreshToken_ShouldReturnBase64CompatibleString()
        {
            // Act
            var refreshToken = _jwtTokenService.GenerateRefreshToken();

            // Assert
            refreshToken.Should().NotBeNullOrEmpty();
            
            // 驗證是有效的 Base64 字串
            var action = () => Convert.FromBase64String(refreshToken);
            action.Should().NotThrow();
        }

        #endregion

        #region 邊界條件測試

        [Test]
        public void Generate_WithVeryLargeUserId_ShouldReturnValidToken()
        {
            // Arrange
            var user = new User { Id = long.MaxValue, Uid = "test_uid", Email = "test@example.com" };

            // Act
            var token = _jwtTokenService.Generate(user);

            // Assert
            token.Should().NotBeNullOrEmpty();
            
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            jsonToken.Claims.Should().Contain(c => c.Type == "sub" && c.Value == long.MaxValue.ToString());
        }

        [Test]
        public void Generate_WithVerySmallUserId_ShouldReturnValidToken()
        {
            // Arrange
            var user = new User { Id = long.MinValue, Uid = "test_uid", Email = "test@example.com" };

            // Act
            var token = _jwtTokenService.Generate(user);

            // Assert
            token.Should().NotBeNullOrEmpty();
            
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            jsonToken.Claims.Should().Contain(c => c.Type == "sub" && c.Value == long.MinValue.ToString());
        }

        [Test]
        public void Generate_WithVeryShortLifetime_ShouldReturnValidToken()
        {
            // Arrange
            var user = AuthTestHelper.CreateTestUser();
            var veryShortLifetime = TimeSpan.FromMilliseconds(1);

            // Act
            var token = _jwtTokenService.Generate(user, veryShortLifetime);

            // Assert
            token.Should().NotBeNullOrEmpty();
        }

        [Test]
        public void Generate_WithVeryLongLifetime_ShouldReturnValidToken()
        {
            // Arrange
            var user = AuthTestHelper.CreateTestUser();
            var veryLongLifetime = TimeSpan.FromDays(365);

            // Act
            var token = _jwtTokenService.Generate(user, veryLongLifetime);

            // Assert
            token.Should().NotBeNullOrEmpty();
        }

        [Test]
        public void Generate_WithManyExtraClaims_ShouldReturnValidToken()
        {
            // Arrange
            var user = AuthTestHelper.CreateTestUser();
            var manyClaims = new Dictionary<string, string>();
            for (int i = 0; i < 100; i++)
            {
                manyClaims[$"claim_{i}"] = $"value_{i}";
            }

            // Act
            var token = _jwtTokenService.Generate(user, null, manyClaims);

            // Assert
            token.Should().NotBeNullOrEmpty();
            
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            jsonToken.Claims.Should().HaveCount(100 + 9); // 100 個額外 claims + 9 個標準 claims (實際數量)
        }

        #endregion

        #region 配置測試

        [Test]
        public void JwtTokenService_WithNullOptions_ShouldThrowNullReferenceException()
        {
            // Arrange & Act & Assert
            var action = () => new JwtTokenService(null!);
            action.Should().Throw<NullReferenceException>();
        }

        [Test]
        public void JwtTokenService_WithEmptySecretKey_ShouldThrowException()
        {
            // Arrange
            var invalidOptions = new JwtOptions
            {
                SecretKey = string.Empty,
                Issuer = "test_issuer",
                Audience = "test_audience",
                AccessTokenExpirationMinutes = 60,
                RefreshTokenExpirationDays = 7
            };

            // Act & Assert
            var action = () => new JwtTokenService(Options.Create(invalidOptions));
            action.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void JwtTokenService_WithShortSecretKey_ShouldThrowException()
        {
            // Arrange
            var invalidOptions = new JwtOptions
            {
                SecretKey = "short", // 太短的密鑰
                Issuer = "test_issuer",
                Audience = "test_audience",
                AccessTokenExpirationMinutes = 60,
                RefreshTokenExpirationDays = 7
            };

            // Act & Assert
            var action = () => new JwtTokenService(Options.Create(invalidOptions));
            action.Should().Throw<InvalidOperationException>();
        }

        #endregion

        #region 性能測試

        [Test]
        public void Generate_MultipleTokens_ShouldPerformWell()
        {
            // Arrange
            var user = AuthTestHelper.CreateTestUser();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            for (int i = 0; i < 1000; i++)
            {
                var token = _jwtTokenService.Generate(user);
                token.Should().NotBeNullOrEmpty();
            }

            stopwatch.Stop();

            // Assert
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // 應該在 5 秒內完成
        }

        [Test]
        public void GenerateRefreshToken_MultipleTokens_ShouldPerformWell()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            for (int i = 0; i < 1000; i++)
            {
                var token = _jwtTokenService.GenerateRefreshToken();
                token.Should().NotBeNullOrEmpty();
            }

            stopwatch.Stop();

            // Assert
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // 應該在 5 秒內完成
        }

        #endregion
    }
}
