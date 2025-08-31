using BIDashboardBackend.Configs;
using BIDashboardBackend.Database;
using BIDashboardBackend.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddScoped<IDbSession>(sp =>
{
    var cs = sp.GetRequiredService<IConfiguration>().GetConnectionString("Pg")!;
    return new DbSession(cs); // 每個 HTTP 請求一個 Scoped DbSession（用完自動歸還連線到連線池）
});


builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ISqlRunner, SqlRunner>();

// 綁定 JwtOptions
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("JwtSettings"));

// 註冊 JwtTokenService
//builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();



// 讀取設定檔中的 JWT 設定
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];
var issuer = jwtSettings["Issuer"];
var audience = jwtSettings["Audience"];
var accessTokenExpiration = int.Parse(jwtSettings["AccessTokenExpirationMinutes"] ?? "120");
var refreshTokenExpiration = int.Parse(jwtSettings["RefreshTokenExpirationDays"] ?? "7");


builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // 1) 驗證規則（你自發的 JWT）
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero, // 不給緩衝，過期就過期
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };


    });

// 預設所有端點都要驗證；需要開放的加 [AllowAnonymous]
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials(); // 若前後端用 Cookie/帶認證，
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");

app.UseAuthorization();

app.MapControllers();

app.Run();
