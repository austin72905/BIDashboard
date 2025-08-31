using BIDashboardBackend.Configs;
using BIDashboardBackend.Interfaces;
using BIDashboardBackend.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BIDashboardBackend.Services
{
    public class JwtTokenService : IJwtTokenService
    {
        private readonly JwtOptions _opt;
        public JwtTokenService(IOptions<JwtOptions> opt)
        {
            _opt = opt.Value;
            if (string.IsNullOrWhiteSpace(_opt.SecretKey) || _opt.SecretKey.Length < 32)
            {
                throw new InvalidOperationException("JwtOptions.SigningKey 必須設定且長度需 >= 32 字元。");
            }
        }

        public string Generate(User user, TimeSpan? lifetime = null, IDictionary<string, string>? extraClaims = null)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.SecretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(_opt.AccessTokenExpirationMinutes));

            // 基本 claims
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim("fid", user.Uid),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("n")),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            // 額外 claims
            if (extraClaims != null)
            {
                foreach (var kv in extraClaims)
                    claims.Add(new Claim(kv.Key, kv.Value));
            }

            var token = new JwtSecurityToken(
                issuer: _opt.Issuer,
                audience: _opt.Audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: expires,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
