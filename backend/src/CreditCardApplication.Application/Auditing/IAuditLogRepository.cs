using CreditCardApplication.Domain.Entities;

namespace CreditCardApplication.Application.Auditing;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken);
}
