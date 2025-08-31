namespace BIDashboardBackend.DTOs.Request
{
    /// <summary>
    /// 用戶端送出刷新存取權杖所需的資料模型
    /// </summary>
    public class RefreshTokenRequest
    {
        /// <summary>
        /// 既有的 JWT 刷新權杖
        /// </summary>
        public string RefreshToken { get; set; } = string.Empty;
    }
}

