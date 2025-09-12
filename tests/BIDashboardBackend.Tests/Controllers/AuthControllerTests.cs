using BIDashboardBackend.Controllers;
using BIDashboardBackend.DTOs;
using BIDashboardBackend.DTOs.Request;
using BIDashboardBackend.Enums;
using BIDashboardBackend.Interfaces;
using BIDashboardBackend.Tests.TestHelpers;
using Microsoft.AspNetCore.Mvc;

namespace BIDashboardBackend.Tests.Controllers
{
    /// <summary>
    /// 認證控制器測試
    /// </summary>
    [TestFixture]
    public class AuthControllerTests
    {
        private AuthController _authController;
        private Mock<IAuthService> _mockAuthService;

        [SetUp]
        public void Setup()
        {
            _mockAuthService = new Mock<IAuthService>();
            _authController = new AuthController(_mockAuthService.Object);
        }

        #region OauthLogin 測試

        [Test]
        public async Task OauthLogin_WithValidRequest_ShouldReturnOkResult()
        {
            // Arrange
            var request = AuthTestHelper.CreateValidOauthLoginRequest();
            var expectedResult = AuthTestHelper.CreateValidAuthResult();
            
            _mockAuthService.Setup(x => x.OauthLogin(request.IdToken))
                          .ReturnsAsync(expectedResult);

            // Act
            var result = await _authController.OauthLogin(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().BeEquivalentTo(expectedResult);
            
            _mockAuthService.Verify(x => x.OauthLogin(request.IdToken), Times.Once);
        }

        [Test]
        public async Task OauthLogin_WithInvalidRequest_ShouldReturnUnauthorizedResult()
        {
            // Arrange
            var request = AuthTestHelper.CreateInvalidOauthLoginRequest();
            var failedResult = AuthTestHelper.CreateFailedAuthResult();
            
            _mockAuthService.Setup(x => x.OauthLogin(request.IdToken))
                          .ReturnsAsync(failedResult);

            // Act
            var result = await _authController.OauthLogin(request);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
            var unauthorizedResult = result as UnauthorizedObjectResult;
            unauthorizedResult!.Value.Should().BeEquivalentTo(failedResult);
            
            _mockAuthService.Verify(x => x.OauthLogin(request.IdToken), Times.Once);
        }

        [Test]
        public async Task OauthLogin_WithEmptyRequest_ShouldReturnUnauthorizedResult()
        {
            // Arrange
            var request = AuthTestHelper.CreateEmptyOauthLoginRequest();
            var failedResult = AuthTestHelper.CreateFailedAuthResult();
            
            _mockAuthService.Setup(x => x.OauthLogin(request.IdToken))
                          .ReturnsAsync(failedResult);

            // Act
            var result = await _authController.OauthLogin(request);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Test]
        public async Task OauthLogin_WithNullRequest_ShouldThrowNullReferenceException()
        {
            // Arrange
            OauthLoginRequest? nullRequest = null;

            // Act & Assert
            var action = async () => await _authController.OauthLogin(nullRequest!);
            await action.Should().ThrowAsync<NullReferenceException>();
        }

        [Test]
        public async Task OauthLogin_WhenServiceThrowsException_ShouldThrowException()
        {
            // Arrange
            var request = AuthTestHelper.CreateValidOauthLoginRequest();
            
            _mockAuthService.Setup(x => x.OauthLogin(request.IdToken))
                          .ThrowsAsync(new Exception("Service error"));

            // Act & Assert
            var action = async () => await _authController.OauthLogin(request);
            await action.Should().ThrowAsync<Exception>().WithMessage("Service error");
        }

        #endregion

        #region RefreshToken 測試

        [Test]
        public async Task RefreshToken_WithValidRequest_ShouldReturnOkResult()
        {
            // Arrange
            var request = AuthTestHelper.CreateValidRefreshTokenRequest();
            var expectedResult = AuthTestHelper.CreateValidAuthResult();
            
            _mockAuthService.Setup(x => x.RefreshTokenAsync(request.RefreshToken))
                          .ReturnsAsync(expectedResult);

            // Act
            var result = await _authController.RefreshToken(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().BeEquivalentTo(expectedResult);
            
            _mockAuthService.Verify(x => x.RefreshTokenAsync(request.RefreshToken), Times.Once);
        }

        [Test]
        public async Task RefreshToken_WithInvalidRequest_ShouldReturnUnauthorizedResult()
        {
            // Arrange
            var request = AuthTestHelper.CreateInvalidRefreshTokenRequest();
            var failedResult = AuthTestHelper.CreateFailedAuthResult();
            
            _mockAuthService.Setup(x => x.RefreshTokenAsync(request.RefreshToken))
                          .ReturnsAsync(failedResult);

            // Act
            var result = await _authController.RefreshToken(request);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
            var unauthorizedResult = result as UnauthorizedObjectResult;
            unauthorizedResult!.Value.Should().BeEquivalentTo(failedResult);
        }

        [Test]
        public async Task RefreshToken_WithEmptyRequest_ShouldReturnUnauthorizedResult()
        {
            // Arrange
            var request = new RefreshTokenRequest { RefreshToken = string.Empty };
            var failedResult = AuthTestHelper.CreateFailedAuthResult();
            
            _mockAuthService.Setup(x => x.RefreshTokenAsync(request.RefreshToken))
                          .ReturnsAsync(failedResult);

            // Act
            var result = await _authController.RefreshToken(request);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Test]
        public async Task RefreshToken_WithNullRequest_ShouldThrowNullReferenceException()
        {
            // Arrange
            RefreshTokenRequest? nullRequest = null;

            // Act & Assert
            var action = async () => await _authController.RefreshToken(nullRequest!);
            await action.Should().ThrowAsync<NullReferenceException>();
        }

        [Test]
        public async Task RefreshToken_WhenServiceThrowsException_ShouldThrowException()
        {
            // Arrange
            var request = AuthTestHelper.CreateValidRefreshTokenRequest();
            
            _mockAuthService.Setup(x => x.RefreshTokenAsync(request.RefreshToken))
                          .ThrowsAsync(new Exception("Service error"));

            // Act & Assert
            var action = async () => await _authController.RefreshToken(request);
            await action.Should().ThrowAsync<Exception>().WithMessage("Service error");
        }

        #endregion

        #region Logout 測試

        [Test]
        public async Task Logout_WithValidRequest_ShouldReturnOkResult()
        {
            // Arrange
            var request = AuthTestHelper.CreateValidLogoutRequest();
            var expectedResult = new AuthResult
            {
                Status = AuthStatus.SuccessExistingUser,
                Message = "登出成功，refresh token 已撤銷"
            };
            
            _mockAuthService.Setup(x => x.LogoutAsync(request.RefreshToken))
                          .ReturnsAsync(expectedResult);

            // Act
            var result = await _authController.Logout(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().BeEquivalentTo(expectedResult);
            
            _mockAuthService.Verify(x => x.LogoutAsync(request.RefreshToken), Times.Once);
        }

        [Test]
        public async Task Logout_WithInvalidRequest_ShouldReturnOkResult()
        {
            // Arrange
            var request = new LogoutRequest { RefreshToken = "invalid_token" };
            var expectedResult = new AuthResult
            {
                Status = AuthStatus.SuccessExistingUser,
                Message = "已登出（refresh token 不存在或已過期）"
            };
            
            _mockAuthService.Setup(x => x.LogoutAsync(request.RefreshToken))
                          .ReturnsAsync(expectedResult);

            // Act
            var result = await _authController.Logout(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().BeEquivalentTo(expectedResult);
        }

        [Test]
        public async Task Logout_WithEmptyRequest_ShouldReturnOkResult()
        {
            // Arrange
            var request = new LogoutRequest { RefreshToken = string.Empty };
            var expectedResult = new AuthResult
            {
                Status = AuthStatus.SuccessExistingUser,
                Message = "已登出（refresh token 不存在或已過期）"
            };
            
            _mockAuthService.Setup(x => x.LogoutAsync(request.RefreshToken))
                          .ReturnsAsync(expectedResult);

            // Act
            var result = await _authController.Logout(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Test]
        public async Task Logout_WithNullRequest_ShouldThrowNullReferenceException()
        {
            // Arrange
            LogoutRequest? nullRequest = null;

            // Act & Assert
            var action = async () => await _authController.Logout(nullRequest!);
            await action.Should().ThrowAsync<NullReferenceException>();
        }

        [Test]
        public async Task Logout_WhenServiceReturnsErrorResult_ShouldReturnOkWithErrorMessage()
        {
            // Arrange
            var request = AuthTestHelper.CreateValidLogoutRequest();
            var errorResult = new AuthResult
            {
                Status = AuthStatus.SuccessExistingUser,
                Message = "登出完成，但清理過程發生錯誤: Service error"
            };
            
            _mockAuthService.Setup(x => x.LogoutAsync(request.RefreshToken))
                          .ReturnsAsync(errorResult);

            // Act
            var result = await _authController.Logout(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().BeEquivalentTo(errorResult);
        }

        #endregion

        #region 邊界條件測試

        [Test]
        public async Task OauthLogin_WithVeryLongToken_ShouldHandleGracefully()
        {
            // Arrange
            var longToken = new string('a', 10000);
            var request = new OauthLoginRequest { IdToken = longToken };
            var failedResult = AuthTestHelper.CreateFailedAuthResult();
            
            _mockAuthService.Setup(x => x.OauthLogin(request.IdToken))
                          .ReturnsAsync(failedResult);

            // Act
            var result = await _authController.OauthLogin(request);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Test]
        public async Task OauthLogin_WithSpecialCharactersInToken_ShouldHandleGracefully()
        {
            // Arrange
            var specialToken = "token_with_special_chars_!@#$%^&*()_+{}|:<>?[]\\;'\",./";
            var request = new OauthLoginRequest { IdToken = specialToken };
            var failedResult = AuthTestHelper.CreateFailedAuthResult();
            
            _mockAuthService.Setup(x => x.OauthLogin(request.IdToken))
                          .ReturnsAsync(failedResult);

            // Act
            var result = await _authController.OauthLogin(request);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        #endregion

        #region 性能測試

        [Test]
        public async Task OauthLogin_WithMultipleConcurrentRequests_ShouldHandleCorrectly()
        {
            // Arrange
            var requests = Enumerable.Range(1, 10)
                                   .Select(i => new OauthLoginRequest { IdToken = $"token_{i}" })
                                   .ToList();
            
            var successResult = AuthTestHelper.CreateValidAuthResult();
            _mockAuthService.Setup(x => x.OauthLogin(It.IsAny<string>()))
                          .ReturnsAsync(successResult);

            // Act
            var tasks = requests.Select(request => _authController.OauthLogin(request));
            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().HaveCount(10);
            results.Should().AllSatisfy(result => result.Should().BeOfType<OkObjectResult>());
        }

        #endregion
    }
}
