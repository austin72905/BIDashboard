namespace BIDashboardBackend.DTOs.Request
{
    /// <summary>
    /// 前端以 Firebase 登入時所需的請求模型
    /// </summary>
    public class OauthLoginRequest
    {
        /// <summary>
        /// 從 Firebase 取得的 ID Token
        /// </summary>
        public string IdToken { get; set; } = string.Empty;
    }
}

