namespace CRMMini.API.Controllers.V1;

using Asp.Versioning;
using CRMMini.Application.Common;
using CRMMini.Application.DTOs.Auth;
using CRMMini.Application.DTOs.Users;
using CRMMini.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/users")]
[Authorize]
[EnableRateLimiting("api")]
public sealed class UsersController(IUserService userService) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserProfileDto>> GetMe(CancellationToken ct)
        => Ok(await userService.GetMyProfileAsync(ct));

    [HttpPut("me")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserProfileDto>> UpdateMe([FromBody] UpdateProfileRequest request, CancellationToken ct)
        => Ok(await userService.UpdateMyProfileAsync(request, ct));

    [HttpPut("me/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangeMyPassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        await userService.ChangeMyPasswordAsync(request, ct);
        return NoContent();
    }

    [HttpGet]
    [Authorize(Policy = "ManagerOrAdmin")]
    [ProducesResponseType(typeof(PagedResult<UserProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<UserProfileDto>>> GetUsers([FromQuery] UserQuery query, CancellationToken ct)
        => Ok(await userService.GetUsersAsync(query, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "ManagerOrAdmin")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserProfileDto>> GetUserById(Guid id, CancellationToken ct)
        => Ok(await userService.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<UserProfileDto>> CreateUser([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var created = await userService.CreateUserAsync(request, ct);
        return CreatedAtAction(nameof(GetUserById), new { version = "1", id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserProfileDto>> UpdateUser(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
        => Ok(await userService.UpdateUserAsync(id, request, ct));
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/teams")]
[Authorize(Policy = "ManagerOrAdmin")]
[EnableRateLimiting("api")]
public sealed class TeamsController(ITeamService teamService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TeamDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TeamDto>>> GetTeams(CancellationToken ct)
        => Ok(await teamService.GetTeamsAsync(ct));

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TeamDto>> CreateTeam([FromBody] CreateTeamRequest request, CancellationToken ct)
    {
        var created = await teamService.CreateTeamAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeamDto>> UpdateTeam(Guid id, [FromBody] UpdateTeamRequest request, CancellationToken ct)
        => Ok(await teamService.UpdateTeamAsync(id, request, ct));
}
