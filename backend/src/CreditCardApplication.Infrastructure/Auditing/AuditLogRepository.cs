using CreditCardApplication.Application.Auditing;
using CreditCardApplication.Domain.Entities;
using CreditCardApplication.Infrastructure.Persistence;

namespace CreditCardApplication.Infrastructure.Auditing;

public sealed class AuditLogRepository(ApplicationDbContext dbContext) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(auditLog);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
