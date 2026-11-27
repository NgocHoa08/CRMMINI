namespace CRMMini.Application.Helpers;

using CRMMini.Application.Common;
using CRMMini.Application.Interfaces;
using CRMMini.Domain.Common;
using CRMMini.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

public static class ValidationExtensions
{
    public static async Task ValidateAndThrowAppAsync<T>(
        this IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken = default)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);
        if (result.IsValid)
            return;

        var errors = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => string.IsNullOrEmpty(g.Key) ? "Request" : g.Key,
                g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

        throw new AppValidationException(errors);
    }

    public static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string? NormalizeEmail(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}

public static class DataScopeExtensions
{
    public static IQueryable<T> ApplyDataScope<T>(this IQueryable<T> query, CurrentUserInfo user)
        where T : class, IOwnedResource
    {
        if (user.IsAdmin)
            return query;

        if (user.IsManager)
        {
            return user.TeamId.HasValue
                ? query.Where(x => x.TeamId == user.TeamId.Value || x.OwnerId == user.Id)
                : query.Where(x => x.OwnerId == user.Id);
        }

        return query.Where(x => x.OwnerId == user.Id);
    }

    public static void EnsureCanRead(this IOwnedResource resource, CurrentUserInfo user, bool allowSharedTeamCompany = false)
    {
        if (user.IsAdmin || resource.OwnerId == user.Id)
            return;

        if ((user.IsManager || allowSharedTeamCompany) && user.TeamId.HasValue && resource.TeamId == user.TeamId.Value)
            return;

        throw new ForbiddenException("Bạn không có quyền truy cập dữ liệu ngoài phạm vi phụ trách.");
    }

    public static void EnsureCanWrite(this IOwnedResource resource, CurrentUserInfo user)
    {
        if (user.IsAdmin || resource.OwnerId == user.Id)
            return;

        if (user.IsManager && user.TeamId.HasValue && resource.TeamId == user.TeamId.Value)
            return;

        throw new ForbiddenException("Bạn không có quyền chỉnh sửa dữ liệu ngoài phạm vi phụ trách.");
    }

    public static void EnsureCanDelete(this IOwnedResource resource, CurrentUserInfo user)
    {
        if (user.IsAdmin)
            return;

        if (user.IsManager && (resource.OwnerId == user.Id || (user.TeamId.HasValue && resource.TeamId == user.TeamId.Value)))
            return;

        throw new ForbiddenException("Chỉ Admin hoặc Manager trong nhóm mới có quyền xóa bản ghi này.");
    }

    public static async Task<(Guid OwnerId, Guid? TeamId)> ResolveOwnerAndTeamAsync(
        IApplicationDbContext db,
        CurrentUserInfo actor,
        Guid? requestedOwnerId,
        CancellationToken cancellationToken)
    {
        if (!requestedOwnerId.HasValue || requestedOwnerId.Value == actor.Id)
            return (actor.Id, actor.TeamId);

        if (!actor.IsAdmin && !actor.IsManager)
            throw new ForbiddenException("Nhân viên không có quyền giao dữ liệu cho người khác.");

        var targetUser = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == requestedOwnerId.Value, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy người phụ trách '{requestedOwnerId.Value}'.");

        if (!targetUser.IsActive)
            throw new BusinessRuleException("Không thể phân công cho tài khoản đang ngừng hoạt động.");

        if (!actor.IsAdmin && (!actor.TeamId.HasValue || targetUser.TeamId != actor.TeamId.Value))
            throw new ForbiddenException("Manager chỉ có thể phân công cho thành viên thuộc nhóm của mình.");

        return (targetUser.Id, targetUser.TeamId);
    }
}
