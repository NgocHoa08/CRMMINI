namespace CRMMini.Application.Services;

using CRMMini.Application.Common;
using CRMMini.Application.DTOs.Activities;
using CRMMini.Application.DTOs.Dashboard;
using CRMMini.Application.Helpers;
using CRMMini.Application.Interfaces;
using CRMMini.Application.Mappings;
using CRMMini.Domain.Entities;
using CRMMini.Domain.Enums;
using CRMMini.Domain.Exceptions;
using CRMMini.Domain.Rules;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

public sealed class ActivityService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IAuditService auditService,
    IDateTimeProvider clock,
    IValidator<CreateActivityRequest> createValidator,
    IValidator<UpdateActivityRequest> updateValidator) : IActivityService
{
    public async Task<PagedResult<ActivityDto>> GetActivitiesAsync(ActivityQuery query, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        var now = clock.UtcNow;

        var q = db.Activities
            .AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .Include(a => a.Opportunity)
            .Include(a => a.Owner)
            .ApplyDataScope(actor);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            q = q.Where(a =>
                a.Subject.ToLower().Contains(term) ||
                (a.Description != null && a.Description.ToLower().Contains(term)));
        }

        if (query.Type.HasValue)
            q = q.Where(a => a.Type == query.Type.Value);

        if (query.Status.HasValue)
            q = q.Where(a => a.Status == query.Status.Value);

        if (query.OwnerId.HasValue)
            q = q.Where(a => a.OwnerId == query.OwnerId.Value);

        if (query.CustomerId.HasValue)
            q = q.Where(a => a.CustomerId == query.CustomerId.Value);

        if (query.LeadId.HasValue)
            q = q.Where(a => a.LeadId == query.LeadId.Value);

        if (query.OpportunityId.HasValue)
            q = q.Where(a => a.OpportunityId == query.OpportunityId.Value);

        if (query.FromDate.HasValue)
        {
            var from = query.FromDate.Value.ToUniversalTime();
            q = q.Where(a => a.ScheduledAt >= from);
        }

        if (query.ToDate.HasValue)
        {
            var to = query.ToDate.Value.ToUniversalTime();
            q = q.Where(a => a.ScheduledAt <= to);
        }

        if (query.OverdueOnly)
            q = q.Where(a => a.Status == ActivityStatus.Pending && a.ScheduledAt < now);

        q = (query.SortBy?.ToLowerInvariant()) switch
        {
            "scheduledat" => query.IsDescending ? q.OrderByDescending(a => a.ScheduledAt) : q.OrderBy(a => a.ScheduledAt),
            "type" => query.IsDescending ? q.OrderByDescending(a => a.Type) : q.OrderBy(a => a.Type),
            "status" => query.IsDescending ? q.OrderByDescending(a => a.Status) : q.OrderBy(a => a.Status),
            _ => query.IsDescending ? q.OrderByDescending(a => a.ScheduledAt) : q.OrderBy(a => a.ScheduledAt)
        };

        return await q.ToPagedResultAsync(query, items => items.Select(a => a.ToDto()), ct);
    }

    public async Task<ActivityDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        var activity = await db.Activities
            .AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .Include(a => a.Opportunity)
            .Include(a => a.Owner)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy Activity '{id}'.");

        activity.EnsureCanRead(actor);
        return activity.ToDto();
    }

    public async Task<ActivityDto> CreateAsync(CreateActivityRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAppAsync(request, ct);
        var actor = currentUser.GetRequiredUser();

        await ValidateTargetsAccessibleAsync(request.CustomerId, request.LeadId, request.OpportunityId, actor, ct);
        var (ownerId, teamId) = await DataScopeExtensions.ResolveOwnerAndTeamAsync(db, actor, request.OwnerId, ct);

        var activity = new Activity
        {
            Type = request.Type,
            Subject = request.Subject.Trim(),
            Description = ValidationExtensions.NormalizeNullable(request.Description),
            Status = ActivityStatus.Pending,
            ScheduledAt = request.ScheduledAt.ToUniversalTime(),
            ReminderAt = request.ReminderAt?.ToUniversalTime(),
            CustomerId = request.CustomerId,
            LeadId = request.LeadId,
            OpportunityId = request.OpportunityId,
            OwnerId = ownerId,
            TeamId = teamId,
            CreatedAt = clock.UtcNow,
            CreatedBy = actor.Id
        };

        db.Activities.Add(activity);
        await db.SaveChangesAsync(ct);

        await auditService.LogAsync(actor.Id, "Activity.Create", "Activity", activity.Id.ToString(),
            newValues: new { activity.Type, activity.Subject, activity.CustomerId, activity.LeadId, activity.OpportunityId },
            cancellationToken: ct);

        return await GetByIdAsync(activity.Id, ct);
    }

    public async Task<ActivityDto> UpdateAsync(Guid id, UpdateActivityRequest request, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAppAsync(request, ct);
        var actor = currentUser.GetRequiredUser();

        var activity = await db.Activities.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy Activity '{id}'.");

        activity.EnsureCanWrite(actor);

        var now = clock.UtcNow;
        activity.Type = request.Type;
        activity.Subject = request.Subject.Trim();
        activity.Description = ValidationExtensions.NormalizeNullable(request.Description);
        activity.Status = request.Status;
        activity.ScheduledAt = request.ScheduledAt.ToUniversalTime();
        activity.ReminderAt = request.ReminderAt?.ToUniversalTime();
        activity.CompletedAt = request.Status == ActivityStatus.Completed
            ? (request.CompletedAt?.ToUniversalTime() ?? activity.CompletedAt ?? now)
            : null;
        activity.UpdatedAt = now;
        activity.UpdatedBy = actor.Id;

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(activity.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        var activity = await db.Activities.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy Activity '{id}'.");

        if (actor.IsStaff && activity.OwnerId != actor.Id)
            throw new ForbiddenException("Bạn chỉ có thể xóa Activity do mình phụ trách.");

        if (!actor.IsStaff)
            activity.EnsureCanDelete(actor);

        activity.IsDeleted = true;
        activity.DeletedAt = clock.UtcNow;
        activity.DeletedBy = actor.Id;

        await db.SaveChangesAsync(ct);
        await auditService.LogAsync(actor.Id, "Activity.SoftDelete", "Activity", activity.Id.ToString(),
            oldValues: new { activity.Subject, activity.Type }, cancellationToken: ct);
    }

    private async Task ValidateTargetsAccessibleAsync(
        Guid? customerId,
        Guid? leadId,
        Guid? opportunityId,
        CurrentUserInfo actor,
        CancellationToken ct)
    {
        if (customerId.HasValue)
        {
            var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId.Value, ct)
                ?? throw new NotFoundException($"Không tìm thấy Customer '{customerId.Value}'.");
            customer.EnsureCanRead(actor);
        }

        if (leadId.HasValue)
        {
            var lead = await db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == leadId.Value, ct)
                ?? throw new NotFoundException($"Không tìm thấy Lead '{leadId.Value}'.");
            lead.EnsureCanRead(actor);
        }

        if (opportunityId.HasValue)
        {
            var opp = await db.Opportunities.AsNoTracking().FirstOrDefaultAsync(o => o.Id == opportunityId.Value, ct)
                ?? throw new NotFoundException($"Không tìm thấy Opportunity '{opportunityId.Value}'.");
            opp.EnsureCanRead(actor);
        }
    }
}

public sealed class DashboardService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IDateTimeProvider clock) : IDashboardService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(DashboardQuery query, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        var now = clock.UtcNow;
        var (periodStart, periodEnd) = ResolvePeriod(query, now);
        var (todayStartUtc, todayEndUtc) = ResolveTodayUtcWindow(now, query.TimeZoneOffsetMinutes);

        var customersQuery = db.Customers.AsNoTracking().ApplyDataScope(actor);
        var totalCustomers = await customersQuery.CountAsync(ct);
        var newCustomers = await customersQuery.CountAsync(c => c.CreatedAt >= periodStart && c.CreatedAt <= periodEnd, ct);

        var leadsByStatusRaw = await db.Leads
            .AsNoTracking()
            .ApplyDataScope(actor)
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var leadsByStatus = Enum.GetValues<LeadStatus>()
            .Select(s => new LeadStatusCountDto(s, leadsByStatusRaw.FirstOrDefault(x => x.Status == s)?.Count ?? 0))
            .ToList();
        var totalLeads = leadsByStatus.Sum(x => x.Count);
        var convertedLeads = leadsByStatus.First(x => x.Status == LeadStatus.Converted).Count;
        var conversionRate = totalLeads == 0 ? 0m : Math.Round((decimal)convertedLeads * 100m / totalLeads, 2);

        var oppProjections = await db.Opportunities
            .AsNoTracking()
            .ApplyDataScope(actor)
            .Select(o => new { o.Stage, o.Value, o.Probability, o.ActualCloseDate })
            .ToListAsync(ct);

        var openOpps = oppProjections.Where(o => !OpportunityStageRules.IsClosed(o.Stage)).ToList();
        var wonInPeriod = oppProjections
            .Where(o => o.Stage == OpportunityStage.ClosedWon &&
                        (!o.ActualCloseDate.HasValue || (o.ActualCloseDate >= periodStart && o.ActualCloseDate <= periodEnd)))
            .ToList();
        var lostInPeriodCount = oppProjections
            .Count(o => o.Stage == OpportunityStage.ClosedLost &&
                        (!o.ActualCloseDate.HasValue || (o.ActualCloseDate >= periodStart && o.ActualCloseDate <= periodEnd)));

        var openPipelineValue = openOpps.Sum(o => o.Value);
        var weightedPipelineValue = Math.Round(openOpps.Sum(o => o.Value * o.Probability / 100m), 2);
        var wonValueInPeriod = wonInPeriod.Sum(o => o.Value);

        var activitiesQuery = db.Activities.AsNoTracking().ApplyDataScope(actor);
        var overdueCount = await activitiesQuery.CountAsync(a => a.Status == ActivityStatus.Pending && a.ScheduledAt < now, ct);
        var dueTodayCount = await activitiesQuery.CountAsync(
            a => a.Status == ActivityStatus.Pending && a.ScheduledAt >= todayStartUtc && a.ScheduledAt < todayEndUtc, ct);

        return new DashboardSummaryDto(
            totalCustomers,
            newCustomers,
            totalLeads,
            convertedLeads,
            conversionRate,
            leadsByStatus,
            openOpps.Count,
            openPipelineValue,
            weightedPipelineValue,
            wonInPeriod.Count,
            wonValueInPeriod,
            lostInPeriodCount,
            overdueCount,
            dueTodayCount);
    }

    public async Task<DashboardPipelineDto> GetPipelineAsync(DashboardQuery query, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        var oppQuery = db.Opportunities
            .AsNoTracking()
            .ApplyDataScope(actor);

        if (query.FromDate.HasValue)
        {
            var from = query.FromDate.Value.ToUniversalTime();
            oppQuery = oppQuery.Where(o => o.CreatedAt >= from);
        }

        if (query.ToDate.HasValue)
        {
            var to = query.ToDate.Value.ToUniversalTime();
            oppQuery = oppQuery.Where(o => o.CreatedAt <= to);
        }

        var rows = await oppQuery
            .Select(o => new
            {
                o.Stage,
                o.Value,
                o.Probability,
                o.OwnerId,
                OwnerName = o.Owner.FullName
            })
            .ToListAsync(ct);

        var stages = Enum.GetValues<OpportunityStage>()
            .Select(stage =>
            {
                var matching = rows.Where(r => r.Stage == stage).ToList();
                return new PipelineStageStatDto(
                    stage,
                    matching.Count,
                    matching.Sum(x => x.Value),
                    Math.Round(matching.Sum(x => x.Value * x.Probability / 100m), 2));
            })
            .ToList();

        var byOwner = rows
            .GroupBy(r => new { r.OwnerId, r.OwnerName })
            .Select(g =>
            {
                var open = g.Where(x => !OpportunityStageRules.IsClosed(x.Stage)).ToList();
                var won = g.Where(x => x.Stage == OpportunityStage.ClosedWon).ToList();
                return new OwnerPipelineStatDto(
                    g.Key.OwnerId,
                    g.Key.OwnerName,
                    open.Count,
                    open.Sum(x => x.Value),
                    won.Count,
                    won.Sum(x => x.Value));
            })
            .OrderByDescending(x => x.WonValue)
            .ThenByDescending(x => x.OpenValue)
            .ToList();

        return new DashboardPipelineDto(stages, byOwner);
    }

    public async Task<DashboardActivitiesDto> GetActivitiesAsync(DashboardQuery query, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        var now = clock.UtcNow;
        var (periodStart, periodEnd) = ResolvePeriod(query, now);
        var (todayStartUtc, todayEndUtc) = ResolveTodayUtcWindow(now, query.TimeZoneOffsetMinutes);

        var baseQuery = db.Activities
            .AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .Include(a => a.Opportunity)
            .Include(a => a.Owner)
            .ApplyDataScope(actor);

        var overdueCount = await baseQuery.CountAsync(a => a.Status == ActivityStatus.Pending && a.ScheduledAt < now, ct);
        var dueTodayCount = await baseQuery.CountAsync(a => a.Status == ActivityStatus.Pending && a.ScheduledAt >= todayStartUtc && a.ScheduledAt < todayEndUtc, ct);
        var upcomingCount = await baseQuery.CountAsync(a => a.Status == ActivityStatus.Pending && a.ScheduledAt >= now, ct);
        var completedCount = await baseQuery.CountAsync(a => a.Status == ActivityStatus.Completed && a.CompletedAt >= periodStart && a.CompletedAt <= periodEnd, ct);

        var byTypeRaw = await baseQuery
            .GroupBy(a => a.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byType = Enum.GetValues<ActivityType>()
            .Select(t => new ActivityTypeCountDto(t, byTypeRaw.FirstOrDefault(x => x.Type == t)?.Count ?? 0))
            .ToList();

        var overdueItems = await baseQuery
            .Where(a => a.Status == ActivityStatus.Pending && a.ScheduledAt < now)
            .OrderBy(a => a.ScheduledAt)
            .Take(10)
            .Select(a => a.ToDto())
            .ToListAsync(ct);

        var upcomingItems = await baseQuery
            .Where(a => a.Status == ActivityStatus.Pending && a.ScheduledAt >= now)
            .OrderBy(a => a.ScheduledAt)
            .Take(10)
            .Select(a => a.ToDto())
            .ToListAsync(ct);

        return new DashboardActivitiesDto(
            overdueCount,
            dueTodayCount,
            upcomingCount,
            completedCount,
            byType,
            overdueItems,
            upcomingItems);
    }

    private static (DateTimeOffset Start, DateTimeOffset End) ResolvePeriod(DashboardQuery query, DateTimeOffset now)
    {
        var start = query.FromDate?.ToUniversalTime()
            ?? new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = query.ToDate?.ToUniversalTime() ?? now;
        return (start, end);
    }

    private static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) ResolveTodayUtcWindow(DateTimeOffset nowUtc, int offsetMinutes)
    {
        var offset = TimeSpan.FromMinutes(Math.Clamp(offsetMinutes, -720, 840));
        var localNow = nowUtc.ToOffset(offset);
        var localStart = new DateTimeOffset(localNow.Year, localNow.Month, localNow.Day, 0, 0, 0, offset);
        var localEnd = localStart.AddDays(1);
        return (localStart.ToUniversalTime(), localEnd.ToUniversalTime());
    }
}
