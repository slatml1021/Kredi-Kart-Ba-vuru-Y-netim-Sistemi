using CreditCardApplication.Application.Applications;
using CreditCardApplication.Application.Auditing;
using CreditCardApplication.Application.Auth;
using CreditCardApplication.Application.Cards;
using CreditCardApplication.Application.Dashboard;
using CreditCardApplication.Infrastructure.Auth;
using CreditCardApplication.Infrastructure.Cards;
using CreditCardApplication.Infrastructure.Dashboard;
using CreditCardApplication.Application.Customers;
using CreditCardApplication.Infrastructure.Applications;
using CreditCardApplication.Infrastructure.Auditing;
using CreditCardApplication.Infrastructure.Customers;
using CreditCardApplication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CreditCardApplication.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection bağlantı bilgisi bulunamadı.");

        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICardApplicationRepository, CardApplicationRepository>();
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<ICreditCardRepository, CreditCardRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        return services;
    }
}
