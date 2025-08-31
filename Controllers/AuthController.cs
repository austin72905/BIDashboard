using BIDashboardBackend.DTOs.Request;
using BIDashboardBackend.Enums;
using BIDashboardBackend.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BIDashboardBackend.Controllers
{
    /// <summary>
    /// 處理登入與刷新權杖等認證相關 API
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;

        public AuthController(IAuthService auth) => _auth = auth;

        /// <summary>
        /// 以 Firebase ID Token 進行登入
        /// </summary>
        [HttpPost("oauth-login")]
        [AllowAnonymous]
        public async Task<IActionResult> OauthLogin([FromBody] OauthLoginRequest req)
        {
            var result = await _auth.OauthLogin(req.IdToken);
            if (result.Status == AuthStatus.InvalidToken)
            {
                return Unauthorized(result);
            }
            return Ok(result);
        }

        /// <summary>
        /// 使用刷新權杖換取新的存取權杖
        /// </summary>
        [HttpPost("refresh-token")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest req)
        {
            var result = await _auth.RefreshTokenAsync(req.RefreshToken);
            if (result.Status == AuthStatus.InvalidToken)
            {
                return Unauthorized(result);
            }
            return Ok(result);
        }
    }
}

