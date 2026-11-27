namespace CRMMini.Infrastructure.Services;

using System.Text.Json;
using CRMMini.Application.Interfaces;
using CRMMini.Domain.Entities;
using CRMMini.Infrastructure.Persistence;

public sealed class AuditService(
    CrmDbContext db,
    ICurrentUserService currentUserService,
    IDateTimeProvider clock) : IAuditService
{
    public async Task LogAsync(
        Guid? userId,
        string action,
        string entityName,
        string? entityId = null,
        object? oldValues = null,
        object? newValues = null,
        bool isSuccess = true,
        string? detail = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValuesJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValuesJson = newValues is null ? null : JsonSerializer.Serialize(newValues),
            IpAddress = currentUserService.IpAddress,
            UserAgent = currentUserService.UserAgent is { Length: > 500 } ua ? ua[..500] : currentUserService.UserAgent,
            IsSuccess = isSuccess,
            Detail = detail,
            CreatedAt = clock.UtcNow
        };

        db.AuditLogs.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
