namespace CRMMini.Application.DTOs.Customers;

using CRMMini.Application.Common;
using CRMMini.Application.DTOs.Activities;
using CRMMini.Application.DTOs.Opportunities;
using CRMMini.Domain.Enums;

public sealed record CompanyDto(
    Guid Id,
    string Name,
    string? TaxCode,
    string? Industry,
    string? Website,
    string? Phone,
    string? Email,
    string? Address,
    Guid OwnerId,
    string OwnerName,
    Guid? TeamId,
    int CustomerCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateCompanyRequest(
    string Name,
    string? TaxCode,
    string? Industry,
    string? Website,
    string? Phone,
    string? Email,
    string? Address,
    Guid? OwnerId);

public sealed record UpdateCompanyRequest(
    string Name,
    string? TaxCode,
    string? Industry,
    string? Website,
    string? Phone,
    string? Email,
    string? Address);

public sealed record CompanyQuery : PaginationQuery
{
    public string? Industry { get; init; }
    public Guid? OwnerId { get; init; }
}

public sealed record CustomerDto(
    Guid Id,
    string FullName,
    string? Email,
    string? Phone,
    string? JobTitle,
    string? Address,
    CustomerStatus Status,
    string? Notes,
    Guid? CompanyId,
    string? CompanyName,
    Guid OwnerId,
    string OwnerName,
    Guid? TeamId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateCustomerRequest(
    string FullName,
    string? Email,
    string? Phone,
    string? JobTitle,
    string? Address,
    CustomerStatus Status,
    string? Notes,
    Guid? CompanyId,
    Guid? OwnerId);

public sealed record UpdateCustomerRequest(
    string FullName,
    string? Email,
    string? Phone,
    string? JobTitle,
    string? Address,
    CustomerStatus Status,
    string? Notes,
    Guid? CompanyId);

public sealed record CustomerQuery : PaginationQuery
{
    public CustomerStatus? Status { get; init; }
    public Guid? CompanyId { get; init; }
    public Guid? OwnerId { get; init; }
    public Guid? TeamId { get; init; }
}

public sealed record AuditLogItemDto(
    Guid Id,
    string Action,
    string EntityName,
    string? EntityId,
    string? OldValuesJson,
    string? NewValuesJson,
    Guid? UserId,
    string? UserName,
    string? Detail,
    bool IsSuccess,
    DateTimeOffset CreatedAt);

public sealed record CustomerHistoryDto(
    CustomerDto Customer,
    IReadOnlyList<OpportunityDto> Opportunities,
    IReadOnlyList<ActivityDto> Activities,
    IReadOnlyList<AuditLogItemDto> AuditLogs);
