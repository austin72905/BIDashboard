namespace BIDashboardBackend.DTOs.Response
{
    public sealed class UserDto
    {
        public long Id { get; init; } = default!;
        public string? Email { get; init; }
        public string? DisplayName { get; init; }
    }
}
