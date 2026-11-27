namespace CRMMini.API.Controllers.V1;

using Asp.Versioning;
using CRMMini.Application.Common;
using CRMMini.Application.DTOs.Customers;
using CRMMini.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/companies")]
[Authorize(Policy = "StaffOrAbove")]
[EnableRateLimiting("api")]
public sealed class CompaniesController(ICompanyService companyService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CompanyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CompanyDto>>> GetAll([FromQuery] CompanyQuery query, CancellationToken ct)
        => Ok(await companyService.GetCompaniesAsync(query, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CompanyDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CompanyDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await companyService.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(typeof(CompanyDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CompanyDto>> Create([FromBody] CreateCompanyRequest request, CancellationToken ct)
    {
        var created = await companyService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { version = "1", id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CompanyDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CompanyDto>> Update(Guid id, [FromBody] UpdateCompanyRequest request, CancellationToken ct)
        => Ok(await companyService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "ManagerOrAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await companyService.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/customers")]
[Authorize(Policy = "StaffOrAbove")]
[EnableRateLimiting("api")]
public sealed class CustomersController(ICustomerService customerService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CustomerDto>>> GetAll([FromQuery] CustomerQuery query, CancellationToken ct)
        => Ok(await customerService.GetCustomersAsync(query, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await customerService.GetByIdAsync(id, ct));

    [HttpGet("{id:guid}/history")]
    [ProducesResponseType(typeof(CustomerHistoryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerHistoryDto>> GetHistory(Guid id, CancellationToken ct)
        => Ok(await customerService.GetHistoryAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CustomerDto>> Create([FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        var created = await customerService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { version = "1", id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerDto>> Update(Guid id, [FromBody] UpdateCustomerRequest request, CancellationToken ct)
        => Ok(await customerService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "ManagerOrAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await customerService.DeleteAsync(id, ct);
        return NoContent();
    }
}
