namespace CRMMini.Infrastructure.Persistence;

using CRMMini.Application.Interfaces;
using CRMMini.Domain.Constants;
using CRMMini.Domain.Entities;
using CRMMini.Domain.Enums;
using Microsoft.EntityFrameworkCore;

public static class DbInitializer
{
    public static Task SeedRolesAsync(CrmDbContext db, CancellationToken cancellationToken = default)
        => EnsureRolesSeededAsync(db, cancellationToken);

    public static async Task EnsureRolesSeededAsync(CrmDbContext db, CancellationToken cancellationToken = default)
    {
        var roleDefinitions = new (string Name, string Description)[]
        {
            (SystemRoles.Admin, "Quản trị toàn hệ thống, tài khoản, nhóm và cấu hình."),
            (SystemRoles.Manager, "Quản lý nhóm kinh doanh, phân công và theo dõi pipeline."),
            (SystemRoles.Staff, "Nhân viên phụ trách khách hàng, lead, cơ hội và hoạt động được giao.")
        };

        var existingNames = await db.Roles
            .Select(r => r.Name)
            .ToListAsync(cancellationToken);

        foreach (var (name, description) in roleDefinitions)
        {
            if (!existingNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                db.Roles.Add(new Role { Name = name, Description = description });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public static async Task SeedDemoDataAsync(
        CrmDbContext db,
        IPasswordHasherService passwordHasher,
        string demoPassword,
        CancellationToken cancellationToken = default)
    {
        await EnsureRolesSeededAsync(db, cancellationToken);

        if (await db.Users.AnyAsync(cancellationToken))
            return;

        if (string.IsNullOrWhiteSpace(demoPassword))
            throw new InvalidOperationException("SeedData:Enabled được bật nhưng chưa cấu hình SeedData:DefaultDemoPassword.");

        var now = DateTimeOffset.UtcNow;
        var roles = await db.Roles.ToDictionaryAsync(r => r.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var teamSalesNorth = new Team
        {
            Name = "Sales Miền Bắc",
            Code = "SALES-HN",
            IsActive = true,
            CreatedAt = now
        };
        db.Teams.Add(teamSalesNorth);
        await db.SaveChangesAsync(cancellationToken);

        var admin = CreateUser("admin", "admin@crmmini.local", "Quản trị viên Hệ thống", "0901000001", null, now, demoPassword, passwordHasher, roles[SystemRoles.Admin]);
        var manager = CreateUser("manager", "manager@crmmini.local", "Trần Quản Lý", "0901000002", teamSalesNorth.Id, now, demoPassword, passwordHasher, roles[SystemRoles.Manager]);
        var staff = CreateUser("staff", "staff@crmmini.local", "Nguyễn Nhân Viên", "0901000003", teamSalesNorth.Id, now, demoPassword, passwordHasher, roles[SystemRoles.Staff]);

        db.Users.AddRange(admin, manager, staff);
        await db.SaveChangesAsync(cancellationToken);

        teamSalesNorth.ManagerUserId = manager.Id;

        var company = new Company
        {
            Name = "Công ty Cổ phần Công nghệ VICO Demo",
            TaxCode = "0109999999",
            Industry = "Phần mềm & Giải pháp CNTT",
            Website = "https://example.crmmini.local",
            Phone = "02439998888",
            Email = "contact@vico-demo.local",
            Address = "Hà Nội, Việt Nam",
            OwnerId = staff.Id,
            TeamId = teamSalesNorth.Id,
            CreatedAt = now,
            CreatedBy = admin.Id
        };
        db.Companies.Add(company);

        var customer = new Customer
        {
            FullName = "Lê Minh Khách",
            Email = "khachhang.demo@example.local",
            Phone = "0912345678",
            JobTitle = "Giám đốc CNTT",
            Address = "Cầu Giấy, Hà Nội",
            Status = CustomerStatus.Active,
            CompanyId = company.Id,
            OwnerId = staff.Id,
            TeamId = teamSalesNorth.Id,
            CreatedAt = now,
            CreatedBy = staff.Id
        };
        db.Customers.Add(customer);

        var lead = new Lead
        {
            FullName = "Phạm Tiềm Năng",
            Email = "lead.demo@example.local",
            Phone = "0987654321",
            CompanyName = "Công ty TNHH Giải pháp Số",
            Source = LeadSource.Website,
            Status = LeadStatus.Qualified,
            EstimatedValue = 150_000_000m,
            Notes = "Đã trao đổi nhu cầu triển khai CRM cho 30 nhân sự.",
            OwnerId = staff.Id,
            TeamId = teamSalesNorth.Id,
            CreatedAt = now,
            CreatedBy = staff.Id
        };
        db.Leads.Add(lead);

        var opp = new Opportunity
        {
            Title = "Triển khai CRM Mini gói Doanh nghiệp",
            CustomerId = customer.Id,
            CompanyId = company.Id,
            OwnerId = staff.Id,
            TeamId = teamSalesNorth.Id,
            Value = 250_000_000m,
            Stage = OpportunityStage.Proposal,
            Probability = 50,
            ExpectedCloseDate = now.AddDays(21),
            Notes = "Đã gửi báo giá sơ bộ.",
            CreatedAt = now,
            CreatedBy = staff.Id
        };
        db.Opportunities.Add(opp);

        var activity = new Activity
        {
            Type = ActivityType.Call,
            Subject = "Gọi xác nhận lịch demo giải pháp",
            Description = "Trao đổi phạm vi tích hợp API và phân quyền.",
            Status = ActivityStatus.Pending,
            ScheduledAt = now.AddDays(2),
            ReminderAt = now.AddDays(2).AddHours(-1),
            CustomerId = customer.Id,
            OpportunityId = opp.Id,
            OwnerId = staff.Id,
            TeamId = teamSalesNorth.Id,
            CreatedAt = now,
            CreatedBy = staff.Id
        };
        db.Activities.Add(activity);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static User CreateUser(
        string userName,
        string email,
        string fullName,
        string phone,
        Guid? teamId,
        DateTimeOffset now,
        string password,
        IPasswordHasherService passwordHasher,
        Role role)
    {
        var user = new User
        {
            UserName = userName,
            Email = email,
            FullName = fullName,
            Phone = phone,
            TeamId = teamId,
            IsActive = true,
            IsLocked = false,
            CreatedAt = now
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, AssignedAt = now });
        return user;
    }
}
