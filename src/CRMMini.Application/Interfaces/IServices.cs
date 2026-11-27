namespace CRMMini.Application.Interfaces;

using CRMMini.Application.Common;
using CRMMini.Application.DTOs.Activities;
using CRMMini.Application.DTOs.Auth;
using CRMMini.Application.DTOs.Customers;
using CRMMini.Application.DTOs.Dashboard;
using CRMMini.Application.DTOs.Leads;
using CRMMini.Application.DTOs.Opportunities;
using CRMMini.Application.DTOs.Users;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default);
    Task LogoutAsync(LogoutRequest request, CancellationToken ct = default);
}

public interface IUserService
{
    Task<UserProfileDto> GetMyProfileAsync(CancellationToken ct = default);
    Task<UserProfileDto> UpdateMyProfileAsync(UpdateProfileRequest request, CancellationToken ct = default);
    Task ChangeMyPasswordAsync(ChangePasswordRequest request, CancellationToken ct = default);
    Task<PagedResult<UserProfileDto>> GetUsersAsync(UserQuery query, CancellationToken ct = default);
    Task<UserProfileDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserProfileDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserProfileDto> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
}

public interface ITeamService
{
    Task<IReadOnlyList<TeamDto>> GetTeamsAsync(CancellationToken ct = default);
    Task<TeamDto> CreateTeamAsync(CreateTeamRequest request, CancellationToken ct = default);
    Task<TeamDto> UpdateTeamAsync(Guid id, UpdateTeamRequest request, CancellationToken ct = default);
}

public interface ICompanyService
{
    Task<PagedResult<CompanyDto>> GetCompaniesAsync(CompanyQuery query, CancellationToken ct = default);
    Task<CompanyDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CompanyDto> CreateAsync(CreateCompanyRequest request, CancellationToken ct = default);
    Task<CompanyDto> UpdateAsync(Guid id, UpdateCompanyRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ICustomerService
{
    Task<PagedResult<CustomerDto>> GetCustomersAsync(CustomerQuery query, CancellationToken ct = default);
    Task<CustomerDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CustomerHistoryDto> GetHistoryAsync(Guid id, CancellationToken ct = default);
    Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default);
    Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ILeadService
{
    Task<PagedResult<LeadDto>> GetLeadsAsync(LeadQuery query, CancellationToken ct = default);
    Task<LeadDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<LeadDto> CreateAsync(CreateLeadRequest request, CancellationToken ct = default);
    Task<LeadDto> UpdateAsync(Guid id, UpdateLeadRequest request, CancellationToken ct = default);
    Task<LeadDto> AssignAsync(Guid id, AssignOwnerRequest request, CancellationToken ct = default);
    Task<ConvertLeadResponse> ConvertAsync(Guid id, ConvertLeadRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IOpportunityService
{
    Task<PagedResult<OpportunityDto>> GetOpportunitiesAsync(OpportunityQuery query, CancellationToken ct = default);
    Task<OpportunityDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<OpportunityHistoryDto> GetHistoryAsync(Guid id, CancellationToken ct = default);
    Task<OpportunityDto> CreateAsync(CreateOpportunityRequest request, CancellationToken ct = default);
    Task<OpportunityDto> UpdateAsync(Guid id, UpdateOpportunityRequest request, CancellationToken ct = default);
    Task<OpportunityDto> ChangeStageAsync(Guid id, ChangeOpportunityStageRequest request, CancellationToken ct = default);
    Task<OpportunityDto> AssignAsync(Guid id, AssignOwnerRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IActivityService
{
    Task<PagedResult<ActivityDto>> GetActivitiesAsync(ActivityQuery query, CancellationToken ct = default);
    Task<ActivityDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ActivityDto> CreateAsync(CreateActivityRequest request, CancellationToken ct = default);
    Task<ActivityDto> UpdateAsync(Guid id, UpdateActivityRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(DashboardQuery query, CancellationToken ct = default);
    Task<DashboardPipelineDto> GetPipelineAsync(DashboardQuery query, CancellationToken ct = default);
    Task<DashboardActivitiesDto> GetActivitiesAsync(DashboardQuery query, CancellationToken ct = default);
}
