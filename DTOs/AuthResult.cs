using BIDashboardBackend.Enums;

namespace BIDashboardBackend.DTOs
{
    public sealed class AuthResult
    {
        public AuthStatus Status { get; init; }
        public string? Message { get; init; }
        public string? Jwt { get; set; } // 成功才有
        public UserDto? User { get; init; }
    }
}
