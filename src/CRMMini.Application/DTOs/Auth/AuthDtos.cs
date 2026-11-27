namespace CRMMini.Application.DTOs.Auth;

public sealed record RegisterRequest(
    string UserName,
    string Email,
    string Password,
    string FullName,
    string? Phone);

public sealed record LoginRequest(
    string UserNameOrEmail,
    string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record UserProfileDto(
    Guid Id,
    string UserName,
    string Email,
    string FullName,
    string? Phone,
    Guid? TeamId,
    string? TeamName,
    IReadOnlyCollection<string> Roles,
    bool IsActive,
    bool IsLocked,
    DateTimeOffset? LockoutEndAt,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserProfileDto User);
