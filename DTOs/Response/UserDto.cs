namespace BIDashboardBackend.DTOs.Response
{
    public sealed class UserDto
    {
        public int Id { get; init; } = default!;
        public string? Email { get; init; }
        public string? DisplayName { get; init; }
    }
}
