using BIDashboardBackend.Models;

namespace BIDashboardBackend.Interfaces
{
    /// <summary>
    /// JWT 產生與相關操作的服務介面
    /// </summary>
    public interface IJwtTokenService
    {
        /// <summary>
        /// 產生存取權杖 (Access Token)
        /// </summary>
        /// <param name="user">系統內的使用者實體</param>
        /// <param name="lifetime">權杖有效期間，若為 null 則使用預設值</param>
        /// <param name="extraClaims">額外要加入權杖的 claims</param>
        string Generate(User user, TimeSpan? lifetime = null, IDictionary<string, string>? extraClaims = null);

        /// <summary>
        /// 產生刷新權杖 (Refresh Token)
        /// </summary>
        string GenerateRefreshToken();
    }
}

