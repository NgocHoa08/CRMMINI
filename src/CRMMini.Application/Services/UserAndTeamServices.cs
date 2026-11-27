namespace CRMMini.Application.Services;

using CRMMini.Application.Common;
using CRMMini.Application.DTOs.Auth;
using CRMMini.Application.DTOs.Users;
using CRMMini.Application.Helpers;
using CRMMini.Application.Interfaces;
using CRMMini.Application.Mappings;
using CRMMini.Domain.Entities;
using CRMMini.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

public sealed class UserService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IPasswordHasherService passwordHasher,
    IAuditService auditService,
    IDateTimeProvider clock,
    IValidator<UpdateProfileRequest> updateProfileValidator,
    IValidator<ChangePasswordRequest> changePasswordValidator,
    IValidator<CreateUserRequest> createUserValidator,
    IValidator<UpdateUserRequest> updateUserValidator) : IUserService
{
    public async Task<UserProfileDto> GetMyProfileAsync(CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        var user = await LoadUserAsync(actor.Id, ct);
        return user.ToProfileDto();
    }

    public async Task<UserProfileDto> UpdateMyProfileAsync(UpdateProfileRequest request, CancellationToken ct = default)
    {
        await updateProfileValidator.ValidateAndThrowAppAsync(request, ct);
        var actor = currentUser.GetRequiredUser();
        var user = await db.Users
            .Include(u => u.Team)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == actor.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy người dùng '{actor.Id}'.");

        user.FullName = request.FullName.Trim();
        user.Phone = ValidationExtensions.NormalizeNullable(request.Phone);
        user.UpdatedAt = clock.UtcNow;

        await db.SaveChangesAsync(ct);
        return user.ToProfileDto();
    }

    public async Task ChangeMyPasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        await changePasswordValidator.ValidateAndThrowAppAsync(request, ct);
        var actor = currentUser.GetRequiredUser();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == actor.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy người dùng '{actor.Id}'.");

        if (!passwordHasher.VerifyPassword(user, user.PasswordHash, request.CurrentPassword))
            throw new BusinessRuleException("Mật khẩu hiện tại không chính xác.");

        var now = clock.UtcNow;
        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        user.UpdatedAt = now;

        // Revoke active refresh tokens on password change
        var activeTokens = await db.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
            token.RevokedReason = "PasswordChanged";
        }

        await db.SaveChangesAsync(ct);
        await auditService.LogAsync(user.Id, "User.ChangePassword", "User", user.Id.ToString(),
            detail: "Người dùng tự đổi mật khẩu.", cancellationToken: ct);
    }

    public async Task<PagedResult<UserProfileDto>> GetUsersAsync(UserQuery query, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        if (!actor.IsAdmin && !actor.IsManager)
            throw new ForbiddenException("Chỉ Admin hoặc Manager mới được xem danh sách người dùng.");

        var q = db.Users
            .AsNoTracking()
            .Include(u => u.Team)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .AsQueryable();

        if (!actor.IsAdmin)
        {
            if (actor.TeamId.HasValue)
                q = q.Where(u => u.TeamId == actor.TeamId.Value);
            else
                q = q.Where(u => u.Id == actor.Id);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            q = q.Where(u =>
                u.UserName.ToLower().Contains(term) ||
                u.Email.ToLower().Contains(term) ||
                u.FullName.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var roleName = query.Role.Trim().ToLower();
            q = q.Where(u => u.UserRoles.Any(ur => ur.Role.Name.ToLower() == roleName));
        }

        if (query.TeamId.HasValue && actor.IsAdmin)
            q = q.Where(u => u.TeamId == query.TeamId.Value);

        if (query.IsActive.HasValue)
            q = q.Where(u => u.IsActive == query.IsActive.Value);

        if (query.IsLocked.HasValue)
            q = q.Where(u => u.IsLocked == query.IsLocked.Value);

        q = (query.SortBy?.ToLowerInvariant()) switch
        {
            "username" => query.IsDescending ? q.OrderByDescending(u => u.UserName) : q.OrderBy(u => u.UserName),
            "email" => query.IsDescending ? q.OrderByDescending(u => u.Email) : q.OrderBy(u => u.Email),
            "fullname" => query.IsDescending ? q.OrderByDescending(u => u.FullName) : q.OrderBy(u => u.FullName),
            _ => query.IsDescending ? q.OrderByDescending(u => u.CreatedAt) : q.OrderBy(u => u.CreatedAt)
        };

        return await q.ToPagedResultAsync(query, items => items.Select(u => u.ToProfileDto()), ct);
    }

    public async Task<UserProfileDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        if (!actor.IsAdmin && actor.Id != id)
        {
            if (!actor.IsManager)
                throw new ForbiddenException();

            var target = await LoadUserAsync(id, ct);
            if (!actor.TeamId.HasValue || target.TeamId != actor.TeamId.Value)
                throw new ForbiddenException();
            return target.ToProfileDto();
        }

        var user = await LoadUserAsync(id, ct);
        return user.ToProfileDto();
    }

    public async Task<UserProfileDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        if (!actor.IsAdmin)
            throw new ForbiddenException("Chỉ Admin mới có quyền tạo tài khoản.");

        await createUserValidator.ValidateAndThrowAppAsync(request, ct);

        var normalizedUserName = request.UserName.Trim();
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.UserName.ToLower() == normalizedUserName.ToLower() || u.Email.ToLower() == normalizedEmail, ct))
            throw new ConflictException("Tên đăng nhập hoặc email đã tồn tại.");

        if (request.TeamId.HasValue && !await db.Teams.AnyAsync(t => t.Id == request.TeamId.Value, ct))
            throw new NotFoundException($"Không tìm thấy Team '{request.TeamId.Value}'.");

        var requestedRoleNames = request.Roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var roles = await db.Roles
            .Where(r => requestedRoleNames.Contains(r.Name))
            .ToListAsync(ct);

        if (roles.Count != requestedRoleNames.Count)
            throw new BusinessRuleException("Một hoặc nhiều vai trò không tồn tại trong hệ thống.");

        var now = clock.UtcNow;
        var user = new User
        {
            UserName = normalizedUserName,
            Email = normalizedEmail,
            FullName = request.FullName.Trim(),
            Phone = ValidationExtensions.NormalizeNullable(request.Phone),
            TeamId = request.TeamId,
            IsActive = true,
            CreatedAt = now
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        foreach (var role in roles)
        {
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, AssignedAt = now });
        }

        await db.SaveChangesAsync(ct);
        await auditService.LogAsync(actor.Id, "User.Create", "User", user.Id.ToString(),
            newValues: new { user.UserName, user.Email, user.TeamId, Roles = roles.Select(r => r.Name) }, cancellationToken: ct);

        return (await LoadUserAsync(user.Id, ct)).ToProfileDto();
    }

    public async Task<UserProfileDto> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        if (!actor.IsAdmin)
            throw new ForbiddenException("Chỉ Admin mới có quyền cập nhật thông tin quản trị người dùng.");

        await updateUserValidator.ValidateAndThrowAppAsync(request, ct);

        var user = await db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy người dùng '{id}'.");

        if (request.TeamId.HasValue && !await db.Teams.AnyAsync(t => t.Id == request.TeamId.Value, ct))
            throw new NotFoundException($"Không tìm thấy Team '{request.TeamId.Value}'.");

        var oldSnapshot = new
        {
            user.FullName,
            user.TeamId,
            user.IsActive,
            user.IsLocked,
            Roles = user.UserRoles.Select(ur => ur.Role.Name).ToArray()
        };

        var now = clock.UtcNow;
        user.FullName = request.FullName.Trim();
        user.Phone = ValidationExtensions.NormalizeNullable(request.Phone);
        user.TeamId = request.TeamId;
        user.IsActive = request.IsActive;
        user.IsLocked = request.IsLocked;
        if (!request.IsLocked)
        {
            user.LockoutEndAt = null;
            user.FailedLoginCount = 0;
        }
        user.UpdatedAt = now;

        if (request.Roles is not null)
        {
            var requestedRoleNames = request.Roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var targetRoles = await db.Roles.Where(r => requestedRoleNames.Contains(r.Name)).ToListAsync(ct);
            if (targetRoles.Count != requestedRoleNames.Count)
                throw new BusinessRuleException("Một hoặc nhiều vai trò không hợp lệ.");

            db.UserRoles.RemoveRange(user.UserRoles);
            foreach (var role in targetRoles)
            {
                db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, AssignedAt = now });
            }
        }

        if (!user.IsActive || user.IsLocked)
        {
            var activeTokens = await db.RefreshTokens
                .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
                .ToListAsync(ct);
            foreach (var rt in activeTokens)
            {
                rt.RevokedAt = now;
                rt.RevokedReason = user.IsLocked ? "UserLocked" : "UserDeactivated";
            }
        }

        await db.SaveChangesAsync(ct);

        var reloaded = await LoadUserAsync(user.Id, ct);
        await auditService.LogAsync(actor.Id, "User.Update", "User", user.Id.ToString(),
            oldValues: oldSnapshot,
            newValues: new
            {
                reloaded.FullName,
                reloaded.TeamId,
                reloaded.IsActive,
                reloaded.IsLocked,
                Roles = reloaded.UserRoles.Select(ur => ur.Role.Name).ToArray()
            },
            cancellationToken: ct);

        return reloaded.ToProfileDto();
    }

    private async Task<User> LoadUserAsync(Guid id, CancellationToken ct)
        => await db.Users
            .AsNoTracking()
            .Include(u => u.Team)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy người dùng '{id}'.");
}

public sealed class TeamService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IDateTimeProvider clock,
    IValidator<CreateTeamRequest> createValidator,
    IValidator<UpdateTeamRequest> updateValidator) : ITeamService
{
    public async Task<IReadOnlyList<TeamDto>> GetTeamsAsync(CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        if (!actor.IsAdmin && !actor.IsManager)
            throw new ForbiddenException("Bạn không có quyền xem danh sách nhóm.");

        var q = db.Teams
            .AsNoTracking()
            .Include(t => t.Manager)
            .Include(t => t.Members)
            .AsQueryable();

        if (!actor.IsAdmin)
            q = actor.TeamId.HasValue ? q.Where(t => t.Id == actor.TeamId.Value) : q.Where(_ => false);

        return await q.OrderBy(t => t.Name)
            .Select(t => new TeamDto(
                t.Id,
                t.Name,
                t.Code,
                t.ManagerUserId,
                t.Manager != null ? t.Manager.FullName : null,
                t.IsActive,
                t.Members.Count,
                t.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<TeamDto> CreateTeamAsync(CreateTeamRequest request, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        if (!actor.IsAdmin)
            throw new ForbiddenException("Chỉ Admin mới được tạo nhóm.");

        await createValidator.ValidateAndThrowAppAsync(request, ct);

        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Teams.AnyAsync(t => t.Code.ToUpper() == code, ct))
            throw new ConflictException($"Mã nhóm '{code}' đã tồn tại.");

        if (request.ManagerUserId.HasValue && !await db.Users.AnyAsync(u => u.Id == request.ManagerUserId.Value, ct))
            throw new NotFoundException($"Không tìm thấy Manager '{request.ManagerUserId.Value}'.");

        var team = new Team
        {
            Name = request.Name.Trim(),
            Code = code,
            ManagerUserId = request.ManagerUserId,
            IsActive = true,
            CreatedAt = clock.UtcNow
        };

        db.Teams.Add(team);
        await db.SaveChangesAsync(ct);

        var managerName = team.ManagerUserId.HasValue
            ? await db.Users.Where(u => u.Id == team.ManagerUserId.Value).Select(u => u.FullName).FirstOrDefaultAsync(ct)
            : null;

        return new TeamDto(team.Id, team.Name, team.Code, team.ManagerUserId, managerName, team.IsActive, 0, team.CreatedAt);
    }

    public async Task<TeamDto> UpdateTeamAsync(Guid id, UpdateTeamRequest request, CancellationToken ct = default)
    {
        var actor = currentUser.GetRequiredUser();
        if (!actor.IsAdmin)
            throw new ForbiddenException("Chỉ Admin mới được cập nhật nhóm.");

        await updateValidator.ValidateAndThrowAppAsync(request, ct);

        var team = await db.Teams
            .Include(t => t.Members)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy nhóm '{id}'.");

        if (request.ManagerUserId.HasValue && !await db.Users.AnyAsync(u => u.Id == request.ManagerUserId.Value, ct))
            throw new NotFoundException($"Không tìm thấy Manager '{request.ManagerUserId.Value}'.");

        team.Name = request.Name.Trim();
        team.ManagerUserId = request.ManagerUserId;
        team.IsActive = request.IsActive;
        team.UpdatedAt = clock.UtcNow;

        await db.SaveChangesAsync(ct);

        var managerName = team.ManagerUserId.HasValue
            ? await db.Users.Where(u => u.Id == team.ManagerUserId.Value).Select(u => u.FullName).FirstOrDefaultAsync(ct)
            : null;

        return new TeamDto(team.Id, team.Name, team.Code, team.ManagerUserId, managerName, team.IsActive, team.Members.Count, team.CreatedAt);
    }
}
