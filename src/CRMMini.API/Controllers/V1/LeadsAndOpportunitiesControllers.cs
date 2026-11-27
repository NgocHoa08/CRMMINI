namespace CRMMini.API.Controllers.V1;

using Asp.Versioning;
using CRMMini.Application.Common;
using CRMMini.Application.DTOs.Leads;
using CRMMini.Application.DTOs.Opportunities;
using CRMMini.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/leads")]
[Authorize(Policy = "StaffOrAbove")]
[EnableRateLimiting("api")]
public sealed class LeadsController(ILeadService leadService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<LeadDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<LeadDto>>> GetAll([FromQuery] LeadQuery query, CancellationToken ct)
        => Ok(await leadService.GetLeadsAsync(query, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LeadDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await leadService.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<LeadDto>> Create([FromBody] CreateLeadRequest request, CancellationToken ct)
    {
        var created = await leadService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { version = "1", id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LeadDto>> Update(Guid id, [FromBody] UpdateLeadRequest request, CancellationToken ct)
        => Ok(await leadService.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/assign")]
    [Authorize(Policy = "ManagerOrAdmin")]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LeadDto>> Assign(Guid id, [FromBody] AssignOwnerRequest request, CancellationToken ct)
        => Ok(await leadService.AssignAsync(id, request, ct));

    [HttpPost("{id:guid}/convert")]
    [ProducesResponseType(typeof(ConvertLeadResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConvertLeadResponse>> Convert(Guid id, [FromBody] ConvertLeadRequest request, CancellationToken ct)
        => Ok(await leadService.ConvertAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "ManagerOrAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await leadService.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/opportunities")]
[Authorize(Policy = "StaffOrAbove")]
[EnableRateLimiting("api")]
public sealed class OpportunitiesController(IOpportunityService opportunityService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OpportunityDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OpportunityDto>>> GetAll([FromQuery] OpportunityQuery query, CancellationToken ct)
        => Ok(await opportunityService.GetOpportunitiesAsync(query, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OpportunityDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await opportunityService.GetByIdAsync(id, ct));

    [HttpGet("{id:guid}/history")]
    [ProducesResponseType(typeof(OpportunityHistoryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OpportunityHistoryDto>> GetHistory(Guid id, CancellationToken ct)
        => Ok(await opportunityService.GetHistoryAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<OpportunityDto>> Create([FromBody] CreateOpportunityRequest request, CancellationToken ct)
    {
        var created = await opportunityService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { version = "1", id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OpportunityDto>> Update(Guid id, [FromBody] UpdateOpportunityRequest request, CancellationToken ct)
        => Ok(await opportunityService.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/stage")]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OpportunityDto>> ChangeStage(Guid id, [FromBody] ChangeOpportunityStageRequest request, CancellationToken ct)
        => Ok(await opportunityService.ChangeStageAsync(id, request, ct));

    [HttpPost("{id:guid}/assign")]
    [Authorize(Policy = "ManagerOrAdmin")]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OpportunityDto>> Assign(Guid id, [FromBody] AssignOwnerRequest request, CancellationToken ct)
        => Ok(await opportunityService.AssignAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "ManagerOrAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await opportunityService.DeleteAsync(id, ct);
        return NoContent();
    }
}
