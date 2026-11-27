namespace CRMMini.Application.Services;

using CRMMini.Application.Common;
using CRMMini.Application.DTOs.Customers;
using CRMMini.Application.DTOs.Leads;
using CRMMini.Application.DTOs.Opportunities;
using CRMMini.Application.Helpers;
using CRMMini.Application.Interfaces;
using CRMMini.Application.Mappings;
using CRMMini.Domain.Entities;
using CRMMini.Domain.Enums;
using CRMMini.Domain.Exceptions;
using CRMMini.Domain.Rules;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

public sealed class LeadService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IAuditService auditService,
    IDateTimeProvider clock,
    IValidator<CreateLeadRequest> createValidator,
    IValidator<UpdateLeadRequest> updateValidator,
    IValidator<ConvertLeadRequest> convertValidator) : ILeadService
{
    public async Task<PagedResult<LeadDto>> GetLeadsAsync(LeadQuery query, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        var q = db.Leads
            .AsNoTracking()
            .Include(l => l.Owner)
            .ApplyDataScope(actor);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            q = q.Where(l =>
                l.FullName.ToLower().Contains(term) ||
                (l.Email != null && l.Email.ToLower().Contains(term)) ||
                (l.Phone != null && l.Phone.Contains(term)) ||
                (l.CompanyName != null && l.CompanyName.ToLower().Contains(term)));
        }

        if (query.Status.HasValue)
            q = q.Where(l => l.Status == query.Status.Value);

        if (query.Source.HasValue)
            q = q.Where(l => l.Source == query.Source.Value);

        if (query.OwnerId.HasValue)
            q = q.Where(l => l.OwnerId == query.OwnerId.Value);

        if (query.TeamId.HasValue)
            q = q.Where(l => l.TeamId == query.TeamId.Value);

        q = (query.SortBy?.ToLowerInvariant()) switch
        {
            "fullname" => query.IsDescending ? q.OrderByDescending(l => l.FullName) : q.OrderBy(l => l.FullName),
            "status" => query.IsDescending ? q.OrderByDescending(l => l.Status) : q.OrderBy(l => l.Status),
            "estimatedvalue" => query.IsDescending ? q.OrderByDescending(l => l.EstimatedValue) : q.OrderBy(l => l.EstimatedValue),
            _ => query.IsDescending ? q.OrderByDescending(l => l.CreatedAt) : q.OrderBy(l => l.CreatedAt)
        };

        return await q.ToPagedResultAsync(query, items => items.Select(l => l.ToDto()), ct);
    }

    public async Task<LeadDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        var lead = await db.Leads
            .AsNoTracking()
            .Include(l => l.Owner)
            .FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy Lead '{id}'.");

        lead.EnsureCanRead(actor);
        return lead.ToDto();
    }

    public async Task<LeadDto> CreateAsync(CreateLeadRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAppAsync(request, ct);
        var actor = currentUser.GetRequiredUser();

        var email = ValidationExtensions.NormalizeEmail(request.Email);
        var phone = ValidationExtensions.NormalizeNullable(request.Phone);
        await EnsureNoDuplicateActiveLeadAsync(null, email, phone, ct);

        var (ownerId, teamId) = await DataScopeExtensions.ResolveOwnerAndTeamAsync(db, actor, request.OwnerId, ct);
        var lead = new Lead
        {
            FullName = request.FullName.Trim(),
            Email = email,
            Phone = phone,
            CompanyName = ValidationExtensions.NormalizeNullable(request.CompanyName),
            Source = request.Source,
            Status = LeadStatus.New,
            EstimatedValue = request.EstimatedValue,
            Notes = ValidationExtensions.NormalizeNullable(request.Notes),
            OwnerId = ownerId,
            TeamId = teamId,
            CreatedAt = clock.UtcNow,
            CreatedBy = actor.Id
        };

        db.Leads.Add(lead);
        await db.SaveChangesAsync(ct);

        await auditService.LogAsync(actor.Id, "Lead.Create", "Lead", lead.Id.ToString(),
            newValues: new { lead.FullName, lead.Email, lead.Phone, lead.Source, lead.OwnerId }, cancellationToken: ct);

        return await GetByIdAsync(lead.Id, ct);
    }

    public async Task<LeadDto> UpdateAsync(Guid id, UpdateLeadRequest request, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAppAsync(request, ct);
        var actor = currentUser.GetRequiredUser();

        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy Lead '{id}'.");

        lead.EnsureCanWrite(actor);

        if (!LeadStatusRules.CanTransition(lead.Status, request.Status))
            throw new BusinessRuleException($"Không thể chuyển trạng thái Lead từ '{lead.Status}' sang '{request.Status}'.");

        var email = ValidationExtensions.NormalizeEmail(request.Email);
        var phone = ValidationExtensions.NormalizeNullable(request.Phone);
        if (lead.Status != LeadStatus.Converted && request.Status != LeadStatus.Disqualified)
            await EnsureNoDuplicateActiveLeadAsync(id, email, phone, ct);

        var oldStatus = lead.Status;
        lead.FullName = request.FullName.Trim();
        lead.Email = email;
        lead.Phone = phone;
        lead.CompanyName = ValidationExtensions.NormalizeNullable(request.CompanyName);
        lead.Source = request.Source;
        lead.Status = request.Status;
        lead.EstimatedValue = request.EstimatedValue;
        lead.Notes = ValidationExtensions.NormalizeNullable(request.Notes);
        lead.UpdatedAt = clock.UtcNow;
        lead.UpdatedBy = actor.Id;

        await db.SaveChangesAsync(ct);

        if (oldStatus != lead.Status)
        {
            await auditService.LogAsync(actor.Id, "Lead.StatusChanged", "Lead", lead.Id.ToString(),
                oldValues: new { Status = oldStatus.ToString() },
                newValues: new { Status = lead.Status.ToString() },
                cancellationToken: ct);
        }

        return await GetByIdAsync(lead.Id, ct);
    }

    public async Task<LeadDto> AssignAsync(Guid id, AssignOwnerRequest request, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        if (!actor.IsAdmin && !actor.IsManager)
            throw new ForbiddenException("Chỉ Admin hoặc Manager mới có quyền phân công Lead.");

        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy Lead '{id}'.");

        lead.EnsureCanWrite(actor);
        if (lead.Status == LeadStatus.Converted)
            throw new BusinessRuleException("Không thể phân công lại Lead đã chuyển đổi.");

        var oldOwnerId = lead.OwnerId;
        var (ownerId, teamId) = await DataScopeExtensions.ResolveOwnerAndTeamAsync(db, actor, request.OwnerId, ct);

        lead.OwnerId = ownerId;
        lead.TeamId = teamId;
        lead.UpdatedAt = clock.UtcNow;
        lead.UpdatedBy = actor.Id;

        await db.SaveChangesAsync(ct);
        await auditService.LogAsync(actor.Id, "Lead.Assign", "Lead", lead.Id.ToString(),
            oldValues: new { OwnerId = oldOwnerId },
            newValues: new { OwnerId = ownerId, TeamId = teamId },
            cancellationToken: ct);

        return await GetByIdAsync(lead.Id, ct);
    }

    public async Task<ConvertLeadResponse> ConvertAsync(Guid id, ConvertLeadRequest request, CancellationToken ct = default)
    {
        await convertValidator.ValidateAndThrowAppAsync(request, ct);
        var actor = currentUser.GetRequiredUser();

        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy Lead '{id}'.");

        lead.EnsureCanWrite(actor);

        if (lead.Status == LeadStatus.Converted)
            throw new ConflictException("Lead này đã được chuyển đổi trước đó.");

        if (lead.Status != LeadStatus.Qualified)
            throw new BusinessRuleException("Chỉ được chuyển đổi Lead đang ở trạng thái 'Qualified'.");

        var now = clock.UtcNow;
        await using var tx = await db.BeginTransactionAsync(ct);

        Guid? companyId = null;
        if (request.CreateCompanyIfMissing && !string.IsNullOrWhiteSpace(lead.CompanyName))
        {
            var companyName = lead.CompanyName.Trim();
            var existingCompany = await db.Companies
                .FirstOrDefaultAsync(c => c.Name.ToLower() == companyName.ToLower(), ct);

            if (existingCompany is not null)
            {
                existingCompany.EnsureCanRead(actor, allowSharedTeamCompany: true);
                companyId = existingCompany.Id;
            }
            else
            {
                var newCompany = new Company
                {
                    Name = companyName,
                    OwnerId = lead.OwnerId,
                    TeamId = lead.TeamId,
                    CreatedAt = now,
                    CreatedBy = actor.Id
                };
                db.Companies.Add(newCompany);
                companyId = newCompany.Id;
            }
        }

        Customer customer;
        if (request.UseExistingCustomerId.HasValue)
        {
            customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.UseExistingCustomerId.Value, ct)
                ?? throw new NotFoundException($"Không tìm thấy Customer '{request.UseExistingCustomerId.Value}'.");
            customer.EnsureCanWrite(actor);
            if (!customer.CompanyId.HasValue && companyId.HasValue)
            {
                customer.CompanyId = companyId;
                customer.UpdatedAt = now;
                customer.UpdatedBy = actor.Id;
            }
            companyId ??= customer.CompanyId;
        }
        else
        {
            var dupEmail = lead.Email is not null && await db.Customers.AnyAsync(c => c.Email != null && c.Email.ToLower() == lead.Email, ct);
            var dupPhone = lead.Phone is not null && await db.Customers.AnyAsync(c => c.Phone == lead.Phone, ct);
            if (dupEmail || dupPhone)
                throw new ConflictException("Đã tồn tại Customer có Email hoặc Số điện thoại trùng với Lead. Hãy truyền UseExistingCustomerId để gắn vào Customer hiện có.");

            customer = new Customer
            {
                FullName = lead.FullName,
                Email = lead.Email,
                Phone = lead.Phone,
                Status = CustomerStatus.Active,
                Notes = lead.Notes,
                CompanyId = companyId,
                OwnerId = lead.OwnerId,
                TeamId = lead.TeamId,
                CreatedAt = now,
                CreatedBy = actor.Id
            };
            db.Customers.Add(customer);
        }

        Guid? opportunityId = null;
        if (request.CreateOpportunity)
        {
            var opp = new Opportunity
            {
                Title = string.IsNullOrWhiteSpace(request.OpportunityTitle)
                    ? $"{lead.FullName} - Cơ hội từ Lead"
                    : request.OpportunityTitle.Trim(),
                CustomerId = customer.Id,
                CompanyId = companyId,
                SourceLeadId = lead.Id,
                OwnerId = lead.OwnerId,
                TeamId = lead.TeamId,
                Value = request.OpportunityValue ?? lead.EstimatedValue ?? 0m,
                Stage = OpportunityStage.Qualification,
                Probability = OpportunityStageRules.GetDefaultProbability(OpportunityStage.Qualification),
                ExpectedCloseDate = (request.ExpectedCloseDate ?? now.AddDays(30)).ToUniversalTime(),
                CreatedAt = now,
                CreatedBy = actor.Id
            };
            db.Opportunities.Add(opp);
            opportunityId = opp.Id;
        }

        lead.Status = LeadStatus.Converted;
        lead.ConvertedCustomerId = customer.Id;
        lead.ConvertedCompanyId = companyId;
        lead.ConvertedAt = now;
        lead.ConvertedBy = actor.Id;
        lead.UpdatedAt = now;
        lead.UpdatedBy = actor.Id;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await auditService.LogAsync(actor.Id, "Lead.Convert", "Lead", lead.Id.ToString(),
            newValues: new { CustomerId = customer.Id, CompanyId = companyId, OpportunityId = opportunityId },
            cancellationToken: ct);

        return new ConvertLeadResponse(lead.Id, customer.Id, companyId, opportunityId, now);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy Lead '{id}'.");

        lead.EnsureCanDelete(actor);

        lead.IsDeleted = true;
        lead.DeletedAt = clock.UtcNow;
        lead.DeletedBy = actor.Id;

        await db.SaveChangesAsync(ct);
        await auditService.LogAsync(actor.Id, "Lead.SoftDelete", "Lead", lead.Id.ToString(),
            oldValues: new { lead.FullName, lead.Email, lead.Phone }, cancellationToken: ct);
    }

    private async Task EnsureNoDuplicateActiveLeadAsync(Guid? excludeId, string? email, string? phone, CancellationToken ct)
    {
        if (email is not null && await db.Leads.AnyAsync(
                l => l.Id != excludeId &&
                     l.Status != LeadStatus.Converted &&
                     l.Status != LeadStatus.Disqualified &&
                     l.Email != null && l.Email.ToLower() == email, ct))
        {
            throw new ConflictException($"Đã tồn tại Lead đang xử lý sử dụng email '{email}'.");
        }

        if (phone is not null && await db.Leads.AnyAsync(
                l => l.Id != excludeId &&
                     l.Status != LeadStatus.Converted &&
                     l.Status != LeadStatus.Disqualified &&
                     l.Phone == phone, ct))
        {
            throw new ConflictException($"Đã tồn tại Lead đang xử lý sử dụng số điện thoại '{phone}'.");
        }
    }
}

public sealed class OpportunityService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IAuditService auditService,
    IDateTimeProvider clock,
    IValidator<CreateOpportunityRequest> createValidator,
    IValidator<UpdateOpportunityRequest> updateValidator,
    IValidator<ChangeOpportunityStageRequest> changeStageValidator) : IOpportunityService
{
    public async Task<PagedResult<OpportunityDto>> GetOpportunitiesAsync(OpportunityQuery query, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        var q = db.Opportunities
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Company)
            .Include(o => o.Owner)
            .ApplyDataScope(actor);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            q = q.Where(o =>
                o.Title.ToLower().Contains(term) ||
                o.Customer.FullName.ToLower().Contains(term) ||
                (o.Company != null && o.Company.Name.ToLower().Contains(term)));
        }

        if (query.Stage.HasValue)
            q = q.Where(o => o.Stage == query.Stage.Value);

        if (query.CustomerId.HasValue)
            q = q.Where(o => o.CustomerId == query.CustomerId.Value);

        if (query.OwnerId.HasValue)
            q = q.Where(o => o.OwnerId == query.OwnerId.Value);

        if (query.TeamId.HasValue)
            q = q.Where(o => o.TeamId == query.TeamId.Value);

        if (query.ExpectedCloseFrom.HasValue)
        {
            var from = query.ExpectedCloseFrom.Value.ToUniversalTime();
            q = q.Where(o => o.ExpectedCloseDate >= from);
        }

        if (query.ExpectedCloseTo.HasValue)
        {
            var to = query.ExpectedCloseTo.Value.ToUniversalTime();
            q = q.Where(o => o.ExpectedCloseDate <= to);
        }

        q = (query.SortBy?.ToLowerInvariant()) switch
        {
            "title" => query.IsDescending ? q.OrderByDescending(o => o.Title) : q.OrderBy(o => o.Title),
            "value" => query.IsDescending ? q.OrderByDescending(o => o.Value) : q.OrderBy(o => o.Value),
            "expectedclosedate" => query.IsDescending ? q.OrderByDescending(o => o.ExpectedCloseDate) : q.OrderBy(o => o.ExpectedCloseDate),
            "stage" => query.IsDescending ? q.OrderByDescending(o => o.Stage) : q.OrderBy(o => o.Stage),
            _ => query.IsDescending ? q.OrderByDescending(o => o.CreatedAt) : q.OrderBy(o => o.CreatedAt)
        };

        return await q.ToPagedResultAsync(query, items => items.Select(o => o.ToDto()), ct);
    }

    public async Task<OpportunityDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        var opp = await db.Opportunities
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Company)
            .Include(o => o.Owner)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy Opportunity '{id}'.");

        opp.EnsureCanRead(actor);
        return opp.ToDto();
    }

    public async Task<OpportunityHistoryDto> GetHistoryAsync(Guid id, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        var oppDto = await GetByIdAsync(id, ct);

        var activities = await db.Activities
            .AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .Include(a => a.Opportunity)
            .Include(a => a.Owner)
            .Where(a => a.OpportunityId == id)
            .ApplyDataScope(actor)
            .OrderByDescending(a => a.ScheduledAt)
            .Select(a => a.ToDto())
            .ToListAsync(ct);

        var idText = id.ToString();
        var audits = await db.AuditLogs
            .AsNoTracking()
            .Include(a => a.User)
            .Where(a => a.EntityName == "Opportunity" && a.EntityId == idText)
            .OrderByDescending(a => a.CreatedAt)
            .Take(50)
            .Select(a => new AuditLogItemDto(
                a.Id, a.Action, a.EntityName, a.EntityId,
                a.OldValuesJson, a.NewValuesJson, a.UserId,
                a.User != null ? a.User.FullName : null,
                a.Detail, a.IsSuccess, a.CreatedAt))
            .ToListAsync(ct);

        return new OpportunityHistoryDto(oppDto, activities, audits);
    }

    public async Task<OpportunityDto> CreateAsync(CreateOpportunityRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAppAsync(request, ct);
        var actor = currentUser.GetRequiredUser();

        if (request.Stage == OpportunityStage.ClosedLost)
            throw new BusinessRuleException("Không thể tạo mới Opportunity trực tiếp ở trạng thái ClosedLost; hãy tạo ở giai đoạn mở rồi chuyển trạng thái kèm lý do.");

        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct)
            ?? throw new NotFoundException($"Không tìm thấy khách hàng '{request.CustomerId}'.");
        customer.EnsureCanRead(actor);

        var companyId = request.CompanyId ?? customer.CompanyId;
        if (companyId.HasValue && !await db.Companies.AnyAsync(c => c.Id == companyId.Value, ct))
            throw new NotFoundException($"Không tìm thấy doanh nghiệp '{companyId.Value}'.");

        var (ownerId, teamId) = await DataScopeExtensions.ResolveOwnerAndTeamAsync(db, actor, request.OwnerId, ct);
        var now = clock.UtcNow;

        var opp = new Opportunity
        {
            Title = request.Title.Trim(),
            CustomerId = customer.Id,
            CompanyId = companyId,
            OwnerId = ownerId,
            TeamId = teamId,
            Value = request.Value,
            Stage = request.Stage,
            Probability = request.Probability ?? OpportunityStageRules.GetDefaultProbability(request.Stage),
            ExpectedCloseDate = request.ExpectedCloseDate.ToUniversalTime(),
            ActualCloseDate = request.Stage == OpportunityStage.ClosedWon ? now : null,
            Notes = ValidationExtensions.NormalizeNullable(request.Notes),
            CreatedAt = now,
            CreatedBy = actor.Id
        };

        db.Opportunities.Add(opp);
        await db.SaveChangesAsync(ct);

        await auditService.LogAsync(actor.Id, "Opportunity.Create", "Opportunity", opp.Id.ToString(),
            newValues: new { opp.Title, opp.Value, Stage = opp.Stage.ToString(), opp.Probability, opp.OwnerId },
            cancellationToken: ct);

        return await GetByIdAsync(opp.Id, ct);
    }

    public async Task<OpportunityDto> UpdateAsync(Guid id, UpdateOpportunityRequest request, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAppAsync(request, ct);
        var actor = currentUser.GetRequiredUser();

        var opp = await db.Opportunities.FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy Opportunity '{id}'.");

        opp.EnsureCanWrite(actor);

        if (OpportunityStageRules.IsClosed(opp.Stage))
            throw new BusinessRuleException("Không thể chỉnh sửa Opportunity đã đóng; hãy mở lại giai đoạn nếu cần cập nhật.");

        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct)
            ?? throw new NotFoundException($"Không tìm thấy khách hàng '{request.CustomerId}'.");
        customer.EnsureCanRead(actor);

        var companyId = request.CompanyId ?? customer.CompanyId;
        if (companyId.HasValue && !await db.Companies.AnyAsync(c => c.Id == companyId.Value, ct))
            throw new NotFoundException($"Không tìm thấy doanh nghiệp '{companyId.Value}'.");

        var oldValues = new { opp.Title, opp.Value, opp.Probability, opp.ExpectedCloseDate };

        opp.Title = request.Title.Trim();
        opp.CustomerId = customer.Id;
        opp.CompanyId = companyId;
        opp.Value = request.Value;
        if (request.Probability.HasValue)
            opp.Probability = request.Probability.Value;
        opp.ExpectedCloseDate = request.ExpectedCloseDate.ToUniversalTime();
        opp.Notes = ValidationExtensions.NormalizeNullable(request.Notes);
        opp.UpdatedAt = clock.UtcNow;
        opp.UpdatedBy = actor.Id;

        await db.SaveChangesAsync(ct);
        await auditService.LogAsync(actor.Id, "Opportunity.Update", "Opportunity", opp.Id.ToString(),
            oldValues: oldValues,
            newValues: new { opp.Title, opp.Value, opp.Probability, opp.ExpectedCloseDate },
            cancellationToken: ct);

        return await GetByIdAsync(opp.Id, ct);
    }

    public async Task<OpportunityDto> ChangeStageAsync(Guid id, ChangeOpportunityStageRequest request, CancellationToken ct = default)
    {
        await changeStageValidator.ValidateAndThrowAppAsync(request, ct);
        var actor = currentUser.GetRequiredUser();

        var opp = await db.Opportunities.FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy Opportunity '{id}'.");

        opp.EnsureCanWrite(actor);

        var canReopen = actor.IsAdmin || actor.IsManager;
        if (!OpportunityStageRules.CanTransition(opp.Stage, request.TargetStage, canReopen))
        {
            if (OpportunityStageRules.IsClosed(opp.Stage) && !canReopen)
                throw new ForbiddenException("Chỉ Manager hoặc Admin mới có quyền mở lại Opportunity đã đóng.");

            throw new BusinessRuleException($"Không thể chuyển Opportunity từ giai đoạn '{opp.Stage}' sang '{request.TargetStage}'.");
        }

        var now = clock.UtcNow;
        var oldValues = new { Stage = opp.Stage.ToString(), opp.Probability, opp.ActualCloseDate, opp.LossReason };

        opp.Stage = request.TargetStage;
        opp.Probability = request.Probability ?? OpportunityStageRules.GetDefaultProbability(request.TargetStage);

        if (OpportunityStageRules.IsClosed(request.TargetStage))
        {
            opp.ActualCloseDate = now;
            opp.LossReason = request.TargetStage == OpportunityStage.ClosedLost
                ? ValidationExtensions.NormalizeNullable(request.LossReason)
                : null;
        }
        else
        {
            opp.ActualCloseDate = null;
            opp.LossReason = null;
        }

        if (!string.IsNullOrWhiteSpace(request.Notes))
            opp.Notes = request.Notes.Trim();

        opp.UpdatedAt = now;
        opp.UpdatedBy = actor.Id;

        await db.SaveChangesAsync(ct);
        await auditService.LogAsync(actor.Id, "Opportunity.StageChanged", "Opportunity", opp.Id.ToString(),
            oldValues: oldValues,
            newValues: new { Stage = opp.Stage.ToString(), opp.Probability, opp.ActualCloseDate, opp.LossReason },
            cancellationToken: ct);

        return await GetByIdAsync(opp.Id, ct);
    }

    public async Task<OpportunityDto> AssignAsync(Guid id, AssignOwnerRequest request, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        if (!actor.IsAdmin && !actor.IsManager)
            throw new ForbiddenException("Chỉ Admin hoặc Manager mới có quyền phân công Opportunity.");

        var opp = await db.Opportunities.FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy Opportunity '{id}'.");

        opp.EnsureCanWrite(actor);

        var oldOwnerId = opp.OwnerId;
        var (ownerId, teamId) = await DataScopeExtensions.ResolveOwnerAndTeamAsync(db, actor, request.OwnerId, ct);

        opp.OwnerId = ownerId;
        opp.TeamId = teamId;
        opp.UpdatedAt = clock.UtcNow;
        opp.UpdatedBy = actor.Id;

        await db.SaveChangesAsync(ct);
        await auditService.LogAsync(actor.Id, "Opportunity.Assign", "Opportunity", opp.Id.ToString(),
            oldValues: new { OwnerId = oldOwnerId },
            newValues: new { OwnerId = ownerId, TeamId = teamId },
            cancellationToken: ct);

        return await GetByIdAsync(opp.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        var opp = await db.Opportunities.FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy Opportunity '{id}'.");

        opp.EnsureCanDelete(actor);

        opp.IsDeleted = true;
        opp.DeletedAt = clock.UtcNow;
        opp.DeletedBy = actor.Id;

        await db.SaveChangesAsync(ct);
        await auditService.LogAsync(actor.Id, "Opportunity.SoftDelete", "Opportunity", opp.Id.ToString(),
            oldValues: new { opp.Title, opp.Value, Stage = opp.Stage.ToString() }, cancellationToken: ct);
    }
}
