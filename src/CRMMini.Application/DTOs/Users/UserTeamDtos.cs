namespace CRMMini.Application.DTOs.Users;

using CRMMini.Application.Common;

public sealed record UpdateProfileRequest(string FullName, string? Phone);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record CreateUserRequest(
    string UserName,
    string Email,
    string Password,
    string FullName,
    string? Phone,
    Guid? TeamId,
    IReadOnlyCollection<string> Roles);

public sealed record UpdateUserRequest(
    string FullName,
    string? Phone,
    Guid? TeamId,
    bool IsActive,
    bool IsLocked,
    IReadOnlyCollection<string>? Roles);

public sealed record UserQuery : PaginationQuery
{
    public string? Role { get; init; }
    public Guid? TeamId { get; init; }
    public bool? IsActive { get; init; }
    public bool? IsLocked { get; init; }
}

public sealed record TeamDto(
    Guid Id,
    string Name,
    string Code,
    Guid? ManagerUserId,
    string? ManagerName,
    bool IsActive,
    int MemberCount,
    DateTimeOffset CreatedAt);

public sealed record CreateTeamRequest(string Name, string Code, Guid? ManagerUserId);

public sealed record UpdateTeamRequest(string Name, Guid? ManagerUserId, bool IsActive);
