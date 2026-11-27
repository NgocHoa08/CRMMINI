namespace CRMMini.API.Services;

using System.Security.Claims;
using CRMMini.Application.Common;
using CRMMini.Application.Interfaces;
using CRMMini.Domain.Exceptions;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public CurrentUserInfo? CurrentUser
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
                return null;

            var idValue = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? principal.FindFirstValue("sub");

            if (!Guid.TryParse(idValue, out var userId))
                return null;

            var userName = principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
            var email = principal.FindFirstValue(ClaimTypes.Email)
                        ?? principal.FindFirstValue("email")
                        ?? string.Empty;
            var fullName = principal.FindFirstValue("full_name") ?? userName;
            var teamIdRaw = principal.FindFirstValue("team_id");
            Guid? teamId = Guid.TryParse(teamIdRaw, out var parsedTeamId) ? parsedTeamId : null;
            var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToArray();

            return new CurrentUserInfo(userId, userName, email, fullName, teamId, roles);
        }
    }

    public string? IpAddress
        => httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent
        => httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();

    public CurrentUserInfo GetRequiredUser()
        => CurrentUser ?? throw new UnauthorizedException("Vui lòng đăng nhập để thực hiện thao tác này.");
}
