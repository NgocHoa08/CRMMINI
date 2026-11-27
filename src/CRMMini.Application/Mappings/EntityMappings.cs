namespace CRMMini.Application.Mappings;

using CRMMini.Application.DTOs.Activities;
using CRMMini.Application.DTOs.Auth;
using CRMMini.Application.DTOs.Customers;
using CRMMini.Application.DTOs.Leads;
using CRMMini.Application.DTOs.Opportunities;
using CRMMini.Domain.Entities;

public static class EntityMappings
{
    public static UserProfileDto ToProfileDto(this User user) =>
        new(
            user.Id,
            user.UserName,
            user.Email,
            user.FullName,
            user.Phone,
            user.TeamId,
            user.Team?.Name,
            user.UserRoles.Select(ur => ur.Role.Name).OrderBy(r => r).ToArray(),
            user.IsActive,
            user.IsLocked,
            user.LockoutEndAt,
            user.LastLoginAt,
            user.CreatedAt);

    public static CompanyDto ToDto(this Company company, int? customerCountOverride = null) =>
        new(
            company.Id,
            company.Name,
            company.TaxCode,
            company.Industry,
            company.Website,
            company.Phone,
            company.Email,
            company.Address,
            company.OwnerId,
            company.Owner?.FullName ?? string.Empty,
            company.TeamId,
            customerCountOverride ?? company.Customers.Count(c => !c.IsDeleted),
            company.CreatedAt,
            company.UpdatedAt);

    public static CustomerDto ToDto(this Customer customer) =>
        new(
            customer.Id,
            customer.FullName,
            customer.Email,
            customer.Phone,
            customer.JobTitle,
            customer.Address,
            customer.Status,
            customer.Notes,
            customer.CompanyId,
            customer.Company?.Name,
            customer.OwnerId,
            customer.Owner?.FullName ?? string.Empty,
            customer.TeamId,
            customer.CreatedAt,
            customer.UpdatedAt);

    public static LeadDto ToDto(this Lead lead) =>
        new(
            lead.Id,
            lead.FullName,
            lead.Email,
            lead.Phone,
            lead.CompanyName,
            lead.Source,
            lead.Status,
            lead.EstimatedValue,
            lead.Notes,
            lead.OwnerId,
            lead.Owner?.FullName ?? string.Empty,
            lead.TeamId,
            lead.ConvertedCustomerId,
            lead.ConvertedCompanyId,
            lead.ConvertedAt,
            lead.CreatedAt,
            lead.UpdatedAt);

    public static OpportunityDto ToDto(this Opportunity opportunity) =>
        new(
            opportunity.Id,
            opportunity.Title,
            opportunity.CustomerId,
            opportunity.Customer?.FullName ?? string.Empty,
            opportunity.CompanyId,
            opportunity.Company?.Name,
            opportunity.SourceLeadId,
            opportunity.OwnerId,
            opportunity.Owner?.FullName ?? string.Empty,
            opportunity.TeamId,
            opportunity.Value,
            opportunity.Stage,
            opportunity.Probability,
            opportunity.ExpectedCloseDate,
            opportunity.ActualCloseDate,
            opportunity.LossReason,
            opportunity.Notes,
            opportunity.CreatedAt,
            opportunity.UpdatedAt);

    public static ActivityDto ToDto(this Activity activity) =>
        new(
            activity.Id,
            activity.Type,
            activity.Subject,
            activity.Description,
            activity.Status,
            activity.ScheduledAt,
            activity.CompletedAt,
            activity.ReminderAt,
            activity.CustomerId,
            activity.Customer?.FullName,
            activity.LeadId,
            activity.Lead?.FullName,
            activity.OpportunityId,
            activity.Opportunity?.Title,
            activity.OwnerId,
            activity.Owner?.FullName ?? string.Empty,
            activity.TeamId,
            activity.CreatedAt,
            activity.UpdatedAt);
}
