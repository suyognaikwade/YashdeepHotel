using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Yashdeep.Application.Context;
using Yashdeep.Application.Interfaces;
using Yashdeep.Infrastructure.Security;
using Yashdeep.Persistence.Cloud;
using Yashdeep.Server.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<ITenantContext, TenantContext>();

builder.Services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();

builder.Services.AddDbContext<CloudDbContext>((sp, options) =>
{
    options.UseInMemoryDatabase("YashdeepCloudDb");
});

var jwtSecret = builder.Configuration["Jwt:SecretKey"] ?? "SUPER_SECRET_DEV_KEY_DO_NOT_USE_IN_PRODUCTION_32BYTES_MIN!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "YashdeepSaaS";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "YashdeepClientTerminals";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
