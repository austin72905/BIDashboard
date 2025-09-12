using BIDashboardBackend.DTOs.Response;
using BIDashboardBackend.Enums;

namespace BIDashboardBackend.DTOs
{
    /// <summary>
    /// 認證流程的回傳結果，用於攜帶 JWT 與使用者資訊
    /// </summary>
    public sealed class AuthResult
    {
        /// <summary>
        /// 代表此次動作的執行結果
        /// </summary>
        public AuthStatus Status { get; init; }

        /// <summary>
        /// 錯誤或提示訊息（若有）
        /// </summary>
        public string? Message { get; init; }

        /// <summary>
        /// 成功時產生的 JWT 存取權杖
        /// </summary>
        public string? Jwt { get; set; }

        /// <summary>
        /// 成功時產生的 JWT 刷新權杖
        /// </summary>
        public string? RefreshToken { get; set; }

        /// <summary>
        /// 對應的使用者資訊
        /// </summary>
        public UserDto? User { get; init; }
    }
}

