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
    return new DbSession(cs); // �C�� HTTP �ШD�@�� Scoped DbSession�]�Χ��۰��k�ٳs�u��s�u���^
});


builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ISqlRunner, SqlRunner>();

// �j�w JwtOptions
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("JwtSettings"));

// ���U JwtTokenService
//builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();



// Ū���]�w�ɤ��� JWT �]�w
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
        // 1) ���ҳW�h�]�A�۵o�� JWT�^
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero, // �����w�ġA�L���N�L��
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };


    });

// �w�]�Ҧ����I���n���ҡF�ݭn�}�񪺥[ [AllowAnonymous]
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
                  .AllowCredentials(); // �Y�e��ݥ� Cookie/�a�{�ҡA
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
