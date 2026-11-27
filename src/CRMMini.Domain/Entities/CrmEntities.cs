namespace CRMMini.Domain.Entities;

using CRMMini.Domain.Common;
using CRMMini.Domain.Enums;

public class Company : SoftDeletableEntity, IOwnedResource
{
    public string Name { get; set; } = string.Empty;
    public string? TaxCode { get; set; }
    public string? Industry { get; set; }
    public string? Website { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? TeamId { get; set; }

    public User Owner { get; set; } = null!;
    public Team? Team { get; set; }
    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
    public ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
}

public class Customer : SoftDeletableEntity, IOwnedResource
{
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public string? Address { get; set; }
    public CustomerStatus Status { get; set; } = CustomerStatus.Active;
    public string? Notes { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? TeamId { get; set; }

    public Company? Company { get; set; }
    public User Owner { get; set; } = null!;
    public Team? Team { get; set; }
    public ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
}

public class Lead : SoftDeletableEntity, IOwnedResource
{
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? CompanyName { get; set; }
    public LeadSource Source { get; set; } = LeadSource.Other;
    public LeadStatus Status { get; set; } = LeadStatus.New;
    public decimal? EstimatedValue { get; set; }
    public string? Notes { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? TeamId { get; set; }

    public Guid? ConvertedCustomerId { get; set; }
    public Guid? ConvertedCompanyId { get; set; }
    public DateTimeOffset? ConvertedAt { get; set; }
    public Guid? ConvertedBy { get; set; }

    public User Owner { get; set; } = null!;
    public Team? Team { get; set; }
    public Customer? ConvertedCustomer { get; set; }
    public Company? ConvertedCompany { get; set; }
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
}

public class Opportunity : SoftDeletableEntity, IOwnedResource
{
    public string Title { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? SourceLeadId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? TeamId { get; set; }
    public decimal Value { get; set; }
    public OpportunityStage Stage { get; set; } = OpportunityStage.Qualification;
    public int Probability { get; set; } = 10;
    public DateTimeOffset ExpectedCloseDate { get; set; }
    public DateTimeOffset? ActualCloseDate { get; set; }
    public string? LossReason { get; set; }
    public string? Notes { get; set; }

    public Customer Customer { get; set; } = null!;
    public Company? Company { get; set; }
    public Lead? SourceLead { get; set; }
    public User Owner { get; set; } = null!;
    public Team? Team { get; set; }
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
}

public class Activity : SoftDeletableEntity, IOwnedResource
{
    public ActivityType Type { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ActivityStatus Status { get; set; } = ActivityStatus.Pending;
    public DateTimeOffset ScheduledAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? ReminderAt { get; set; }

    public Guid? CustomerId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? OpportunityId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? TeamId { get; set; }

    public Customer? Customer { get; set; }
    public Lead? Lead { get; set; }
    public Opportunity? Opportunity { get; set; }
    public User Owner { get; set; } = null!;
    public Team? Team { get; set; }
}
