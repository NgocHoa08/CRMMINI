namespace CRMMini.Application.Services;

using CRMMini.Application.Common;
using CRMMini.Application.DTOs.Customers;
using CRMMini.Application.Helpers;
using CRMMini.Application.Interfaces;
using CRMMini.Application.Mappings;
using CRMMini.Domain.Entities;
using CRMMini.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

public sealed class CompanyService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IAuditService auditService,
    IDateTimeProvider clock,
    IValidator<CreateCompanyRequest> createValidator,
    IValidator<UpdateCompanyRequest> updateValidator) : ICompanyService
{
    public async Task<PagedResult<CompanyDto>> GetCompaniesAsync(CompanyQuery query, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        var q = db.Companies
            .AsNoTracking()
            .Include(c => c.Owner)
            .Include(c => c.Customers)
            .AsQueryable();

        if (!actor.IsAdmin)
        {
            q = actor.TeamId.HasValue
                ? q.Where(c => c.TeamId == actor.TeamId.Value || c.OwnerId == actor.Id)
                : q.Where(c => c.OwnerId == actor.Id);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            q = q.Where(c =>
                c.Name.ToLower().Contains(term) ||
                (c.TaxCode != null && c.TaxCode.ToLower().Contains(term)) ||
                (c.Email != null && c.Email.ToLower().Contains(term)) ||
                (c.Phone != null && c.Phone.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(query.Industry))
        {
            var ind = query.Industry.Trim().ToLower();
            q = q.Where(c => c.Industry != null && c.Industry.ToLower() == ind);
        }

        if (query.OwnerId.HasValue)
            q = q.Where(c => c.OwnerId == query.OwnerId.Value);

        q = (query.SortBy?.ToLowerInvariant()) switch
        {
            "name" => query.IsDescending ? q.OrderByDescending(c => c.Name) : q.OrderBy(c => c.Name),
            "industry" => query.IsDescending ? q.OrderByDescending(c => c.Industry) : q.OrderBy(c => c.Industry),
            _ => query.IsDescending ? q.OrderByDescending(c => c.CreatedAt) : q.OrderBy(c => c.CreatedAt)
        };

        return await q.ToPagedResultAsync(query, items => items.Select(c => c.ToDto(c.Customers.Count)), ct);
    }

    public async Task<CompanyDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        var company = await db.Companies
            .AsNoTracking()
            .Include(c => c.Owner)
            .Include(c => c.Customers)
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy doanh nghiệp '{id}'.");

        company.EnsureCanRead(actor, allowSharedTeamCompany: true);
        return company.ToDto(company.Customers.Count);
    }

    public async Task<CompanyDto> CreateAsync(CreateCompanyRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAppAsync(request, ct);
        var actor = currentUser.GetRequiredUser();

        var taxCode = ValidationExtensions.NormalizeNullable(request.TaxCode);
        if (taxCode is not null && await db.Companies.AnyAsync(c => c.TaxCode != null && c.TaxCode.ToLower() == taxCode.ToLower(), ct))
            throw new ConflictException($"Mã số thuế '{taxCode}' đã tồn tại ở một doanh nghiệp khác.");

        var (ownerId, teamId) = await DataScopeExtensions.ResolveOwnerAndTeamAsync(db, actor, request.OwnerId, ct);
        var now = clock.UtcNow;

        var company = new Company
        {
            Name = request.Name.Trim(),
            TaxCode = taxCode,
            Industry = ValidationExtensions.NormalizeNullable(request.Industry),
            Website = ValidationExtensions.NormalizeNullable(request.Website),
            Phone = ValidationExtensions.NormalizeNullable(request.Phone),
            Email = ValidationExtensions.NormalizeEmail(request.Email),
            Address = ValidationExtensions.NormalizeNullable(request.Address),
            OwnerId = ownerId,
            TeamId = teamId,
            CreatedAt = now,
            CreatedBy = actor.Id
        };

        db.Companies.Add(company);
        await db.SaveChangesAsync(ct);

        await auditService.LogAsync(actor.Id, "Company.Create", "Company", company.Id.ToString(),
            newValues: new { company.Name, company.TaxCode, company.OwnerId }, cancellationToken: ct);

        return await GetByIdAsync(company.Id, ct);
    }

    public async Task<CompanyDto> UpdateAsync(Guid id, UpdateCompanyRequest request, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAppAsync(request, ct);
        var actor = currentUser.GetRequiredUser();

        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy doanh nghiệp '{id}'.");

        company.EnsureCanWrite(actor);

        var taxCode = ValidationExtensions.NormalizeNullable(request.TaxCode);
        if (taxCode is not null && await db.Companies.AnyAsync(c => c.Id != id && c.TaxCode != null && c.TaxCode.ToLower() == taxCode.ToLower(), ct))
            throw new ConflictException($"Mã số thuế '{taxCode}' đã tồn tại ở một doanh nghiệp khác.");

        company.Name = request.Name.Trim();
        company.TaxCode = taxCode;
        company.Industry = ValidationExtensions.NormalizeNullable(request.Industry);
        company.Website = ValidationExtensions.NormalizeNullable(request.Website);
        company.Phone = ValidationExtensions.NormalizeNullable(request.Phone);
        company.Email = ValidationExtensions.NormalizeEmail(request.Email);
        company.Address = ValidationExtensions.NormalizeNullable(request.Address);
        company.UpdatedAt = clock.UtcNow;
        company.UpdatedBy = actor.Id;

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(company.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy doanh nghiệp '{id}'.");

        company.EnsureCanDelete(actor);

        if (await db.Customers.AnyAsync(c => c.CompanyId == id, ct))
            throw new BusinessRuleException("Không thể xóa doanh nghiệp đang có khách hàng liên kết.");

        company.IsDeleted = true;
        company.DeletedAt = clock.UtcNow;
        company.DeletedBy = actor.Id;

        await db.SaveChangesAsync(ct);
        await auditService.LogAsync(actor.Id, "Company.SoftDelete", "Company", company.Id.ToString(),
            oldValues: new { company.Name, company.TaxCode }, cancellationToken: ct);
    }
}

public sealed class CustomerService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IAuditService auditService,
    IDateTimeProvider clock,
    IValidator<CreateCustomerRequest> createValidator,
    IValidator<UpdateCustomerRequest> updateValidator) : ICustomerService
{
    public async Task<PagedResult<CustomerDto>> GetCustomersAsync(CustomerQuery query, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        var q = db.Customers
            .AsNoTracking()
            .Include(c => c.Company)
            .Include(c => c.Owner)
            .ApplyDataScope(actor);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            q = q.Where(c =>
                c.FullName.ToLower().Contains(term) ||
                (c.Email != null && c.Email.ToLower().Contains(term)) ||
                (c.Phone != null && c.Phone.Contains(term)) ||
                (c.Company != null && c.Company.Name.ToLower().Contains(term)));
        }

        if (query.Status.HasValue)
            q = q.Where(c => c.Status == query.Status.Value);

        if (query.CompanyId.HasValue)
            q = q.Where(c => c.CompanyId == query.CompanyId.Value);

        if (query.OwnerId.HasValue)
            q = q.Where(c => c.OwnerId == query.OwnerId.Value);

        if (query.TeamId.HasValue)
            q = q.Where(c => c.TeamId == query.TeamId.Value);

        q = (query.SortBy?.ToLowerInvariant()) switch
        {
            "fullname" => query.IsDescending ? q.OrderByDescending(c => c.FullName) : q.OrderBy(c => c.FullName),
            "email" => query.IsDescending ? q.OrderByDescending(c => c.Email) : q.OrderBy(c => c.Email),
            "status" => query.IsDescending ? q.OrderByDescending(c => c.Status) : q.OrderBy(c => c.Status),
            _ => query.IsDescending ? q.OrderByDescending(c => c.CreatedAt) : q.OrderBy(c => c.CreatedAt)
        };

        return await q.ToPagedResultAsync(query, items => items.Select(c => c.ToDto()), ct);
    }

    public async Task<CustomerDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        var customer = await db.Customers
            .AsNoTracking()
            .Include(c => c.Company)
            .Include(c => c.Owner)
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy khách hàng '{id}'.");

        customer.EnsureCanRead(actor);
        return customer.ToDto();
    }

    public async Task<CustomerHistoryDto> GetHistoryAsync(Guid id, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        var customer = await db.Customers
            .AsNoTracking()
            .Include(c => c.Company)
            .Include(c => c.Owner)
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy khách hàng '{id}'.");

        customer.EnsureCanRead(actor);

        var opportunities = await db.Opportunities
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Company)
            .Include(o => o.Owner)
            .Where(o => o.CustomerId == id)
            .ApplyDataScope(actor)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => o.ToDto())
            .ToListAsync(ct);

        var activities = await db.Activities
            .AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .Include(a => a.Opportunity)
            .Include(a => a.Owner)
            .Where(a => a.CustomerId == id)
            .ApplyDataScope(actor)
            .OrderByDescending(a => a.ScheduledAt)
            .Select(a => a.ToDto())
            .ToListAsync(ct);

        var idText = id.ToString();
        var audits = await db.AuditLogs
            .AsNoTracking()
            .Include(a => a.User)
            .Where(a => a.EntityName == "Customer" && a.EntityId == idText)
            .OrderByDescending(a => a.CreatedAt)
            .Take(50)
            .Select(a => new AuditLogItemDto(
                a.Id, a.Action, a.EntityName, a.EntityId,
                a.OldValuesJson, a.NewValuesJson, a.UserId,
                a.User != null ? a.User.FullName : null,
                a.Detail, a.IsSuccess, a.CreatedAt))
            .ToListAsync(ct);

        return new CustomerHistoryDto(customer.ToDto(), opportunities, activities, audits);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAppAsync(request, ct);
        var actor = currentUser.GetRequiredUser();

        var email = ValidationExtensions.NormalizeEmail(request.Email);
        var phone = ValidationExtensions.NormalizeNullable(request.Phone);
        await EnsureNoDuplicateCustomerAsync(null, email, phone, ct);
        await EnsureCompanyAccessibleAsync(request.CompanyId, actor, ct);

        var (ownerId, teamId) = await DataScopeExtensions.ResolveOwnerAndTeamAsync(db, actor, request.OwnerId, ct);
        var customer = new Customer
        {
            FullName = request.FullName.Trim(),
            Email = email,
            Phone = phone,
            JobTitle = ValidationExtensions.NormalizeNullable(request.JobTitle),
            Address = ValidationExtensions.NormalizeNullable(request.Address),
            Status = request.Status,
            Notes = ValidationExtensions.NormalizeNullable(request.Notes),
            CompanyId = request.CompanyId,
            OwnerId = ownerId,
            TeamId = teamId,
            CreatedAt = clock.UtcNow,
            CreatedBy = actor.Id
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        await auditService.LogAsync(actor.Id, "Customer.Create", "Customer", customer.Id.ToString(),
            newValues: new { customer.FullName, customer.Email, customer.Phone, customer.OwnerId }, cancellationToken: ct);

        return await GetByIdAsync(customer.Id, ct);
    }

    public async Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAppAsync(request, ct);
        var actor = currentUser.GetRequiredUser();

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy khách hàng '{id}'.");

        customer.EnsureCanWrite(actor);

        var email = ValidationExtensions.NormalizeEmail(request.Email);
        var phone = ValidationExtensions.NormalizeNullable(request.Phone);
        await EnsureNoDuplicateCustomerAsync(id, email, phone, ct);
        await EnsureCompanyAccessibleAsync(request.CompanyId, actor, ct);

        var oldValues = new { customer.FullName, customer.Email, customer.Phone, customer.Status, customer.CompanyId };

        customer.FullName = request.FullName.Trim();
        customer.Email = email;
        customer.Phone = phone;
        customer.JobTitle = ValidationExtensions.NormalizeNullable(request.JobTitle);
        customer.Address = ValidationExtensions.NormalizeNullable(request.Address);
        customer.Status = request.Status;
        customer.Notes = ValidationExtensions.NormalizeNullable(request.Notes);
        customer.CompanyId = request.CompanyId;
        customer.UpdatedAt = clock.UtcNow;
        customer.UpdatedBy = actor.Id;

        await db.SaveChangesAsync(ct);

        await auditService.LogAsync(actor.Id, "Customer.Update", "Customer", customer.Id.ToString(),
            oldValues: oldValues,
            newValues: new { customer.FullName, customer.Email, customer.Phone, customer.Status, customer.CompanyId },
            cancellationToken: ct);

        return await GetByIdAsync(customer.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy khách hàng '{id}'.");

        customer.EnsureCanDelete(actor);

        customer.IsDeleted = true;
        customer.DeletedAt = clock.UtcNow;
        customer.DeletedBy = actor.Id;

        await db.SaveChangesAsync(ct);

        await auditService.LogAsync(actor.Id, "Customer.SoftDelete", "Customer", customer.Id.ToString(),
            oldValues: new { customer.FullName, customer.Email, customer.Phone }, cancellationToken: ct);
    }

    private async Task EnsureNoDuplicateCustomerAsync(Guid? excludeId, string? email, string? phone, CancellationToken ct)
    {
        if (email is not null && await db.Customers.AnyAsync(c => c.Id != excludeId && c.Email != null && c.Email.ToLower() == email, ct))
            throw new ConflictException($"Đã tồn tại khách hàng sử dụng email '{email}'.");

        if (phone is not null && await db.Customers.AnyAsync(c => c.Id != excludeId && c.Phone == phone, ct))
            throw new ConflictException($"Đã tồn tại khách hàng sử dụng số điện thoại '{phone}'.");
    }

    private async Task EnsureCompanyAccessibleAsync(Guid? companyId, CurrentUserInfo actor, CancellationToken ct)
    {
        if (!companyId.HasValue)
            return;

        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == companyId.Value, ct)
            ?? throw new NotFoundException($"Không tìm thấy doanh nghiệp '{companyId.Value}'.");

        company.EnsureCanRead(actor, allowSharedTeamCompany: true);
    }
}
