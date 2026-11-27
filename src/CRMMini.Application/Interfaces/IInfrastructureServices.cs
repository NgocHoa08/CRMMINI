namespace CRMMini.Application.Interfaces;

using CRMMini.Application.Common;
using CRMMini.Domain.Entities;

public interface ICurrentUserService
{
    CurrentUserInfo? CurrentUser { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
    CurrentUserInfo GetRequiredUser();
}

public interface IPasswordHasherService
{
    string HashPassword(User user, string password);
    bool VerifyPassword(User user, string hashedPassword, string providedPassword);
}

public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) GenerateAccessToken(User user, IReadOnlyCollection<string> roles);
    string GenerateRefreshToken();
    string HashToken(string rawToken);
}

public interface IAuditService
{
    Task LogAsync(
        Guid? userId,
        string action,
        string entityName,
        string? entityId,
        object? oldValues = null,
        object? newValues = null,
        bool isSuccess = true,
        string? detail = null,
        CancellationToken cancellationToken = default);
}

public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
