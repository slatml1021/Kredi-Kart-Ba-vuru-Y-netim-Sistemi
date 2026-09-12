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
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

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
var jwtKey = jwt["Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException(
        "JWT anahtarı yapılandırılmamış veya 32 bayttan kısa. Geliştirme ortamı için appsettings.Development.json, " +
        "diğer ortamlar için Jwt__Key ortam değişkenini kullanın.");
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
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDevelopment", policy =>
        policy.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();
using var databaseInstanceLock = AcquireDatabaseInstanceLock(app.Environment.ContentRootPath);

app.UseMiddleware<ApiExceptionMiddleware>();
app.UseCors("AngularDevelopment");
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<SessionValidationMiddleware>();
app.UseAuthorization();
app.MapControllers();

await app.Services.InitializeDatabaseAsync();

app.Run();

static FileStream AcquireDatabaseInstanceLock(string contentRootPath)
{
    var dataDirectory = Path.Combine(contentRootPath, "App_Data");
    Directory.CreateDirectory(dataDirectory);
    var lockPath = Path.Combine(dataDirectory, "credit-card-application.instance.lock");
    try
    {
        return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    catch (IOException exception)
    {
        throw new InvalidOperationException(
            "Aynı proje veritabanını kullanan başka bir backend süreci zaten çalışıyor. " +
            "Önce diğer 'dotnet run' sürecini durdurun; SQLite dosyasını iki API süreciyle açmayın.", exception);
    }
}
