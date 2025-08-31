using BIDashboardBackend.Configs;
using BIDashboardBackend.Database;
using BIDashboardBackend.Interfaces;
using BIDashboardBackend.Services;
using BIDashboardBackend.Caching; // 引入 Redis 快取服務
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using StackExchange.Redis;           // Redis 連線套件
using Microsoft.Extensions.Options;  // 讀取設定選項

var builder = WebApplication.CreateBuilder(args);

// ===== 註冊服務 =====

// 建立資料庫連線的工作階段
builder.Services.AddScoped<IDbSession>(sp =>
{
    var cs = sp.GetRequiredService<IConfiguration>().GetConnectionString("Pg")!;
    return new DbSession(cs);
});

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ISqlRunner, SqlRunner>();

// 載入 JWT 設定
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("JwtSettings"));

// JWT 產生與驗證服務
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// 載入 Redis 設定並註冊快取服務
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection("Redis"));
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var opt = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
    return ConnectionMultiplexer.Connect(opt.ConnectionString);
});
builder.Services.AddSingleton<ICacheService, RedisCacheService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ===== JWT 驗證設定 =====
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];
var issuer = jwtSettings["Issuer"];
var audience = jwtSettings["Audience"];

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// ===== 中介軟體管線 =====
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

