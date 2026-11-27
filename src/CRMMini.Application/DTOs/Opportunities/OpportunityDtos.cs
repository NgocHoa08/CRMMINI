namespace CRMMini.Application.DTOs.Opportunities;

using CRMMini.Application.Common;
using CRMMini.Application.DTOs.Activities;
using CRMMini.Application.DTOs.Customers;
using CRMMini.Domain.Enums;

public sealed record OpportunityDto(
    Guid Id,
    string Title,
    Guid CustomerId,
    string CustomerName,
    Guid? CompanyId,
    string? CompanyName,
    Guid? SourceLeadId,
    Guid OwnerId,
    string OwnerName,
    Guid? TeamId,
    decimal Value,
    OpportunityStage Stage,
    int Probability,
    DateTimeOffset ExpectedCloseDate,
    DateTimeOffset? ActualCloseDate,
    string? LossReason,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateOpportunityRequest(
    string Title,
    Guid CustomerId,
    Guid? CompanyId,
    decimal Value,
    OpportunityStage Stage,
    int? Probability,
    DateTimeOffset ExpectedCloseDate,
    string? Notes,
    Guid? OwnerId);

public sealed record UpdateOpportunityRequest(
    string Title,
    Guid CustomerId,
    Guid? CompanyId,
    decimal Value,
    int? Probability,
    DateTimeOffset ExpectedCloseDate,
    string? Notes);

public sealed record ChangeOpportunityStageRequest(
    OpportunityStage TargetStage,
    int? Probability,
    string? LossReason,
    string? Notes);

public sealed record OpportunityQuery : PaginationQuery
{
    public OpportunityStage? Stage { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? OwnerId { get; init; }
    public Guid? TeamId { get; init; }
    public DateTimeOffset? ExpectedCloseFrom { get; init; }
    public DateTimeOffset? ExpectedCloseTo { get; init; }
}

public sealed record OpportunityHistoryDto(
    OpportunityDto Opportunity,
    IReadOnlyList<ActivityDto> Activities,
    IReadOnlyList<AuditLogItemDto> AuditLogs);
