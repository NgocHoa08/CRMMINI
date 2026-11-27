namespace CRMMini.Application.Services;

using CRMMini.Application.Common;
using CRMMini.Application.DTOs.Auth;
using CRMMini.Application.Helpers;
using CRMMini.Application.Interfaces;
using CRMMini.Application.Mappings;
using CRMMini.Domain.Constants;
using CRMMini.Domain.Entities;
using CRMMini.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public sealed class AuthService(
    IApplicationDbContext db,
    IPasswordHasherService passwordHasher,
    ITokenService tokenService,
    ICurrentUserService currentUserService,
    IAuditService auditService,
    IDateTimeProvider clock,
    IOptions<JwtSettings> jwtOptions,
    IOptions<SecuritySettings> securityOptions,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IValidator<RefreshTokenRequest> refreshValidator,
    IValidator<LogoutRequest> logoutValidator) : IAuthService
{
    private readonly JwtSettings _jwtSettings = jwtOptions.Value;
    private readonly SecuritySettings _securitySettings = securityOptions.Value;

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        await registerValidator.ValidateAndThrowAppAsync(request, ct);

        var normalizedUserName = request.UserName.Trim();
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var exists = await db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail || u.UserName.ToLower() == normalizedUserName.ToLower(), ct);

        if (exists)
            throw new ConflictException("Email hoặc tên đăng nhập đã được sử dụng.");

        var staffRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == SystemRoles.Staff, ct)
            ?? throw new BusinessRuleException("Hệ thống chưa khởi tạo vai trò mặc định.");

        var now = clock.UtcNow;
        var user = new User
        {
            UserName = normalizedUserName,
            Email = normalizedEmail,
            FullName = request.FullName.Trim(),
            Phone = ValidationExtensions.NormalizeNullable(request.Phone),
            IsActive = true,
            IsLocked = false,
            CreatedAt = now
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = staffRole.Id, AssignedAt = now });

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        await auditService.LogAsync(
            user.Id,
            "Auth.Register",
            nameof(User),
            user.Id.ToString(),
            newValues: new { user.UserName, user.Email, Role = SystemRoles.Staff },
            cancellationToken: ct);

        return await IssueAuthTokensAsync(user, [SystemRoles.Staff], ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        await loginValidator.ValidateAndThrowAppAsync(request, ct);

        var identifier = request.UserNameOrEmail.Trim().ToLowerInvariant();
        var now = clock.UtcNow;

        var user = await db.Users
            .Include(u => u.Team)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(
                u => u.Email.ToLower() == identifier || u.UserName.ToLower() == identifier,
                ct);

        if (user is null)
        {
            await auditService.LogAsync(
                null,
                "Auth.LoginFailed",
                nameof(User),
                identifier,
                isSuccess: false,
                detail: "Tài khoản không tồn tại.",
                cancellationToken: ct);
            throw new UnauthorizedException("Email/tên đăng nhập hoặc mật khẩu không đúng.");
        }

        if (!user.IsActive)
        {
            await auditService.LogAsync(user.Id, "Auth.LoginBlocked", nameof(User), user.Id.ToString(),
                isSuccess: false, detail: "Tài khoản đã bị vô hiệu hóa.", cancellationToken: ct);
            throw new ForbiddenException("Tài khoản đã bị vô hiệu hóa.");
        }

        if (user.IsLocked)
        {
            if (user.LockoutEndAt.HasValue && user.LockoutEndAt.Value <= now)
            {
                user.IsLocked = false;
                user.LockoutEndAt = null;
                user.FailedLoginCount = 0;
            }
            else
            {
                await auditService.LogAsync(user.Id, "Auth.LoginLocked", nameof(User), user.Id.ToString(),
                    isSuccess: false, detail: "Đăng nhập vào tài khoản đang bị khóa.", cancellationToken: ct);
                throw new ForbiddenException("Tài khoản hiện đang bị khóa. Vui lòng liên hệ quản trị viên hoặc thử lại sau.");
            }
        }

        if (!passwordHasher.VerifyPassword(user, user.PasswordHash, request.Password))
        {
            user.FailedLoginCount += 1;
            var detail = $"Sai mật khẩu (lần {user.FailedLoginCount}).";

            if (user.FailedLoginCount >= _securitySettings.MaxFailedLoginAttempts)
            {
                user.IsLocked = true;
                user.LockoutEndAt = now.AddMinutes(_securitySettings.LockoutMinutes);
                detail = $"Sai mật khẩu quá {_securitySettings.MaxFailedLoginAttempts} lần; khóa đến {user.LockoutEndAt:O}.";
            }

            user.UpdatedAt = now;
            await db.SaveChangesAsync(ct);

            await auditService.LogAsync(user.Id, "Auth.LoginFailed", nameof(User), user.Id.ToString(),
                isSuccess: false, detail: detail, cancellationToken: ct);

            if (user.IsLocked)
                throw new ForbiddenException("Tài khoản đã bị khóa do nhập sai mật khẩu quá số lần cho phép.");

            throw new UnauthorizedException("Email/tên đăng nhập hoặc mật khẩu không đúng.");
        }

        user.FailedLoginCount = 0;
        user.LockoutEndAt = null;
        user.LastLoginAt = now;
        user.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        var roles = user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToArray();
        await auditService.LogAsync(user.Id, "Auth.LoginSuccess", nameof(User), user.Id.ToString(),
            detail: "Đăng nhập thành công.", cancellationToken: ct);

        return await IssueAuthTokensAsync(user, roles, ct);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        await refreshValidator.ValidateAndThrowAppAsync(request, ct);

        var now = clock.UtcNow;
        var tokenHash = tokenService.HashToken(request.RefreshToken);

        var existingToken = await db.RefreshTokens
            .Include(rt => rt.User)
                .ThenInclude(u => u.Team)
            .Include(rt => rt.User)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);

        if (existingToken is null)
            throw new UnauthorizedException("Refresh token không hợp lệ.");

        if (existingToken.IsRevoked)
        {
            var activeTokens = await db.RefreshTokens
                .Where(rt => rt.UserId == existingToken.UserId && rt.RevokedAt == null)
                .ToListAsync(ct);

            foreach (var active in activeTokens)
            {
                active.RevokedAt = now;
                active.RevokedReason = "Phát hiện sử dụng lại refresh token đã thu hồi.";
            }
            await db.SaveChangesAsync(ct);

            await auditService.LogAsync(existingToken.UserId, "Auth.RefreshTokenReuse", nameof(RefreshToken),
                existingToken.Id.ToString(), isSuccess: false, detail: "Phát hiện reuse refresh token.", cancellationToken: ct);

            throw new UnauthorizedException("Refresh token đã bị thu hồi.");
        }

        if (existingToken.IsExpired(now))
            throw new UnauthorizedException("Refresh token đã hết hạn.");

        var user = existingToken.User;
        if (!user.IsActive || (user.IsLocked && (!user.LockoutEndAt.HasValue || user.LockoutEndAt.Value > now)))
            throw new ForbiddenException("Tài khoản không khả dụng hoặc đang bị khóa.");

        var rawNewRefresh = tokenService.GenerateRefreshToken();
        var newHash = tokenService.HashToken(rawNewRefresh);
        var refreshExpiry = now.AddDays(_jwtSettings.RefreshTokenExpirationDays);

        existingToken.RevokedAt = now;
        existingToken.RevokedReason = "Rotated";
        existingToken.ReplacedByTokenHash = newHash;

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = newHash,
            ExpiresAt = refreshExpiry,
            CreatedAt = now,
            CreatedByIp = currentUserService.IpAddress
        });

        await db.SaveChangesAsync(ct);

        var roles = user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToArray();
        var (accessToken, accessExpiresAt) = tokenService.GenerateAccessToken(user, roles);

        return new AuthResponse(accessToken, accessExpiresAt, rawNewRefresh, refreshExpiry, user.ToProfileDto());
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken ct = default)
    {
        await logoutValidator.ValidateAndThrowAppAsync(request, ct);

        var tokenHash = tokenService.HashToken(request.RefreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);
        var currentUser = currentUserService.CurrentUser;

        if (token is not null && !token.IsRevoked)
        {
            if (currentUser is not null && token.UserId != currentUser.Id && !currentUser.IsAdmin)
                throw new ForbiddenException();

            token.RevokedAt = clock.UtcNow;
            token.RevokedReason = "User logout";
            await db.SaveChangesAsync(ct);

            await auditService.LogAsync(token.UserId, "Auth.Logout", nameof(RefreshToken), token.Id.ToString(),
                detail: "Thu hồi refresh token khi đăng xuất.", cancellationToken: ct);
        }
    }

    private async Task<AuthResponse> IssueAuthTokensAsync(User user, IReadOnlyCollection<string> roles, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var (accessToken, accessExpiresAt) = tokenService.GenerateAccessToken(user, roles);
        var rawRefreshToken = tokenService.GenerateRefreshToken();
        var refreshExpiry = now.AddDays(_jwtSettings.RefreshTokenExpirationDays);

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenService.HashToken(rawRefreshToken),
            ExpiresAt = refreshExpiry,
            CreatedAt = now,
            CreatedByIp = currentUserService.IpAddress
        });

        await db.SaveChangesAsync(ct);

        var loadedUser = await db.Users
            .AsNoTracking()
            .Include(u => u.Team)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstAsync(u => u.Id == user.Id, ct);

        return new AuthResponse(accessToken, accessExpiresAt, rawRefreshToken, refreshExpiry, loadedUser.ToProfileDto());
    }
}
