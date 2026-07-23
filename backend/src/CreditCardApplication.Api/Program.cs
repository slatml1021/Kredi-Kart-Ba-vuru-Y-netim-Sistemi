using System.Text;
using CreditCardApplication.Api.Auth;
using CreditCardApplication.Application.Auth;
using CreditCardApplication.Application.Auditing;
using CreditCardApplication.Application.Cards;
using CreditCardApplication.Application.Dashboard;
using CreditCardApplication.Application.Services;
using CreditCardApplication.Application.Customers;
using CreditCardApplication.Application.Applications;
using CreditCardApplication.Api.Middleware;
using CreditCardApplication.Infrastructure;
using CreditCardApplication.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddScoped<LimitCalculator>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<CardApplicationService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<CreditCardService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddInfrastructure(builder.Configuration);
var jwt = builder.Configuration.GetSection("Jwt");
var jwtKey = jwt["Key"] ?? throw new InvalidOperationException("JWT anahtarı yapılandırılmamış.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwt["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwt["Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    });
builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDevelopment", policy =>
        policy.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

app.UseMiddleware<ApiExceptionMiddleware>();
app.UseCors("AngularDevelopment");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.Services.InitializeDatabaseAsync();

app.Run();
