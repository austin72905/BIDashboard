using BIDashboardBackend.DTOs;

namespace BIDashboardBackend.Interfaces
{
    /// <summary>
    /// 認證相關服務的介面定義
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// 透過 Firebase ID Token 進行 OAuth 登入
        /// </summary>
        /// <param name="firebaseIdToken">從前端取得的 Firebase ID Token</param>
        Task<AuthResult> OauthLogin(string firebaseIdToken);

        /// <summary>
        /// 使用刷新權杖換取新的存取權杖
        /// </summary>
        /// <param name="refreshToken">既有的刷新權杖</param>
        Task<AuthResult> RefreshTokenAsync(string refreshToken);
    }
}

