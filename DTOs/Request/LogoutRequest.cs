namespace BIDashboardBackend.DTOs.Request
{
    /// <summary>
    /// 用戶端送出登出請求所需的資料模型
    /// </summary>
    public class LogoutRequest
    {
        /// <summary>
        /// 要撤銷的 JWT 刷新權杖
        /// </summary>
        public string RefreshToken { get; set; } = string.Empty;
    }
}
