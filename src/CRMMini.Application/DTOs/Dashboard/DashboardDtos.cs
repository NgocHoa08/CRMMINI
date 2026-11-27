namespace CRMMini.Application.DTOs.Dashboard;

using CRMMini.Application.DTOs.Activities;
using CRMMini.Domain.Enums;

public sealed record DashboardQuery(
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    int TimeZoneOffsetMinutes = 0);

public sealed record LeadStatusCountDto(LeadStatus Status, int Count);

public sealed record DashboardSummaryDto(
    int TotalCustomers,
    int NewCustomersInPeriod,
    int TotalLeads,
    int ConvertedLeads,
    decimal LeadConversionRatePercent,
    IReadOnlyList<LeadStatusCountDto> LeadsByStatus,
    int OpenOpportunitiesCount,
    decimal OpenPipelineValue,
    decimal WeightedPipelineValue,
    int WonOpportunitiesInPeriod,
    decimal WonValueInPeriod,
    int LostOpportunitiesInPeriod,
    int OverdueActivitiesCount,
    int DueTodayActivitiesCount);

public sealed record PipelineStageStatDto(
    OpportunityStage Stage,
    int Count,
    decimal TotalValue,
    decimal WeightedValue);

public sealed record OwnerPipelineStatDto(
    Guid OwnerId,
    string OwnerName,
    int OpenCount,
    decimal OpenValue,
    int WonCount,
    decimal WonValue);

public sealed record DashboardPipelineDto(
    IReadOnlyList<PipelineStageStatDto> Stages,
    IReadOnlyList<OwnerPipelineStatDto> ByOwner);

public sealed record ActivityTypeCountDto(ActivityType Type, int Count);

public sealed record DashboardActivitiesDto(
    int OverdueCount,
    int DueTodayCount,
    int UpcomingCount,
    int CompletedInPeriodCount,
    IReadOnlyList<ActivityTypeCountDto> ByType,
    IReadOnlyList<ActivityDto> OverdueItems,
    IReadOnlyList<ActivityDto> UpcomingItems);
