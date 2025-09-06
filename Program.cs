using BIDashboardBackend.Caching; // 引入 Redis 快取服務
using BIDashboardBackend.Configs;
using BIDashboardBackend.Database;
using BIDashboardBackend.Features.Ingest;
using BIDashboardBackend.Features.Jobs;
using BIDashboardBackend.Interfaces;
using BIDashboardBackend.Interfaces.Repositories;  // 讀取設定選項
using BIDashboardBackend.Repositories;
using BIDashboardBackend.Services;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;           // Redis 連線套件
using System.Text;


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

// Hangfire
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(c =>
        c.UseNpgsqlConnection(builder.Configuration.GetConnectionString("Pg"))));

builder.Services.AddHangfireServer();



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

// 工具類
builder.Services.AddSingleton<CsvSniffer>();        // 無狀態工具
builder.Services.AddSingleton<CacheKeyBuilder>();   // 產 Key 的工具 

// scope service
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IIngestService, IngestService>();
builder.Services.AddScoped<IMetricService, MetricService>();
builder.Services.AddScoped<IEtlJob,EtlJob>();

// repo
builder.Services.AddScoped<IDatasetRepository, DatasetRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IMetricRepository, MetricRepository>();


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

// 在 /hangfire 看到 UI
app.UseHangfireDashboard("/hangfire");

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

