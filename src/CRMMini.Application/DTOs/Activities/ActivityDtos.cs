namespace CRMMini.Application.DTOs.Activities;

using CRMMini.Application.Common;
using CRMMini.Domain.Enums;

public sealed record ActivityDto(
    Guid Id,
    ActivityType Type,
    string Subject,
    string? Description,
    ActivityStatus Status,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? ReminderAt,
    Guid? CustomerId,
    string? CustomerName,
    Guid? LeadId,
    string? LeadName,
    Guid? OpportunityId,
    string? OpportunityTitle,
    Guid OwnerId,
    string OwnerName,
    Guid? TeamId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateActivityRequest(
    ActivityType Type,
    string Subject,
    string? Description,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? ReminderAt,
    Guid? CustomerId,
    Guid? LeadId,
    Guid? OpportunityId,
    Guid? OwnerId);

public sealed record UpdateActivityRequest(
    ActivityType Type,
    string Subject,
    string? Description,
    ActivityStatus Status,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? ReminderAt,
    DateTimeOffset? CompletedAt);

public sealed record ActivityQuery : PaginationQuery
{
    public ActivityType? Type { get; init; }
    public ActivityStatus? Status { get; init; }
    public Guid? OwnerId { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? LeadId { get; init; }
    public Guid? OpportunityId { get; init; }
    public DateTimeOffset? FromDate { get; init; }
    public DateTimeOffset? ToDate { get; init; }
    public bool OverdueOnly { get; init; }
}
