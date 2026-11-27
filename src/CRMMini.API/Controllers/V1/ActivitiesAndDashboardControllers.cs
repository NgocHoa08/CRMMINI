namespace CRMMini.API.Controllers.V1;

using Asp.Versioning;
using CRMMini.Application.Common;
using CRMMini.Application.DTOs.Activities;
using CRMMini.Application.DTOs.Dashboard;
using CRMMini.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/activities")]
[Authorize(Policy = "StaffOrAbove")]
[EnableRateLimiting("api")]
public sealed class ActivitiesController(IActivityService activityService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ActivityDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ActivityDto>>> GetAll([FromQuery] ActivityQuery query, CancellationToken ct)
        => Ok(await activityService.GetActivitiesAsync(query, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ActivityDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ActivityDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await activityService.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(typeof(ActivityDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ActivityDto>> Create([FromBody] CreateActivityRequest request, CancellationToken ct)
    {
        var created = await activityService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { version = "1", id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ActivityDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ActivityDto>> Update(Guid id, [FromBody] UpdateActivityRequest request, CancellationToken ct)
        => Ok(await activityService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "ManagerOrAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await activityService.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/dashboard")]
[Authorize(Policy = "StaffOrAbove")]
[EnableRateLimiting("api")]
public sealed class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary([FromQuery] DashboardQuery query, CancellationToken ct)
        => Ok(await dashboardService.GetSummaryAsync(query, ct));

    [HttpGet("pipeline")]
    [ProducesResponseType(typeof(DashboardPipelineDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardPipelineDto>> GetPipeline([FromQuery] DashboardQuery query, CancellationToken ct)
        => Ok(await dashboardService.GetPipelineAsync(query, ct));

    [HttpGet("activities")]
    [ProducesResponseType(typeof(DashboardActivitiesDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardActivitiesDto>> GetActivities([FromQuery] DashboardQuery query, CancellationToken ct)
        => Ok(await dashboardService.GetActivitiesAsync(query, ct));
}
