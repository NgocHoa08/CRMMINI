namespace CRMMini.Application.Common;

using CRMMini.Domain.Constants;

public sealed record CurrentUserInfo(
    Guid Id,
    string UserName,
    string Email,
    string FullName,
    Guid? TeamId,
    IReadOnlyCollection<string> Roles)
{
    public bool IsAdmin => Roles.Contains(SystemRoles.Admin, StringComparer.OrdinalIgnoreCase);
    public bool IsManager => Roles.Contains(SystemRoles.Manager, StringComparer.OrdinalIgnoreCase);
    public bool IsStaff => Roles.Contains(SystemRoles.Staff, StringComparer.OrdinalIgnoreCase);
}

public sealed class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "CRMMini";
    public string Audience { get; set; } = "CRMMini.Client";
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}

public sealed class SecuritySettings
{
    public const string SectionName = "SecuritySettings";

    public int MaxFailedLoginAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
}
