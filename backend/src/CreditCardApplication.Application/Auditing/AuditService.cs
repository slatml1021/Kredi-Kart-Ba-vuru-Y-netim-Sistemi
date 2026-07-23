using CreditCardApplication.Domain.Entities;

namespace CreditCardApplication.Application.Auditing;

public sealed class AuditService(IAuditLogRepository auditLogRepository)
{
    public Task WriteAsync(int? userId, string action, string entityName, string? entityId,
        string? detail, string? ipAddress, CancellationToken cancellationToken) =>
        auditLogRepository.AddAsync(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Detail = detail,
            IpAddress = ipAddress
        }, cancellationToken);
}
