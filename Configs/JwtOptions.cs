namespace BIDashboardBackend.Configs
{
    public sealed class JwtOptions
    {
        public string Issuer { get; set; } = default!;
        public string Audience { get; set; } = default!;
        public string SecretKey { get; set; } = default!;
        public int AccessTokenExpirationMinutes { get; set; } = 120;
        public int RefreshTokenExpirationDays { get; set; } = 7;
    }
}
