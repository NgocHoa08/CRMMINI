namespace CRMMini.Application.DTOs.Leads;

using CRMMini.Application.Common;
using CRMMini.Domain.Enums;

public sealed record LeadDto(
    Guid Id,
    string FullName,
    string? Email,
    string? Phone,
    string? CompanyName,
    LeadSource Source,
    LeadStatus Status,
    decimal? EstimatedValue,
    string? Notes,
    Guid OwnerId,
    string OwnerName,
    Guid? TeamId,
    Guid? ConvertedCustomerId,
    Guid? ConvertedCompanyId,
    DateTimeOffset? ConvertedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateLeadRequest(
    string FullName,
    string? Email,
    string? Phone,
    string? CompanyName,
    LeadSource Source,
    decimal? EstimatedValue,
    string? Notes,
    Guid? OwnerId);

public sealed record UpdateLeadRequest(
    string FullName,
    string? Email,
    string? Phone,
    string? CompanyName,
    LeadSource Source,
    LeadStatus Status,
    decimal? EstimatedValue,
    string? Notes);

public sealed record LeadQuery : PaginationQuery
{
    public LeadStatus? Status { get; init; }
    public LeadSource? Source { get; init; }
    public Guid? OwnerId { get; init; }
    public Guid? TeamId { get; init; }
}

public sealed record AssignOwnerRequest(Guid OwnerId);

public sealed record ConvertLeadRequest(
    bool CreateCompanyIfMissing = true,
    Guid? UseExistingCustomerId = null,
    bool CreateOpportunity = false,
    string? OpportunityTitle = null,
    decimal? OpportunityValue = null,
    DateTimeOffset? ExpectedCloseDate = null);

public sealed record ConvertLeadResponse(
    Guid LeadId,
    Guid CustomerId,
    Guid? CompanyId,
    Guid? OpportunityId,
    DateTimeOffset ConvertedAt);
