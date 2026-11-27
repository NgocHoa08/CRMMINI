namespace CRMMini.Domain.Enums;

public enum CustomerStatus
{
    Active = 1,
    Inactive = 2
}

public enum LeadSource
{
    Website = 1,
    Referral = 2,
    SocialMedia = 3,
    ColdCall = 4,
    Event = 5,
    Partner = 6,
    Other = 99
}

public enum LeadStatus
{
    New = 1,
    Contacted = 2,
    Qualified = 3,
    Converted = 4,
    Disqualified = 5
}

public enum OpportunityStage
{
    Qualification = 1,
    NeedsAnalysis = 2,
    Proposal = 3,
    Negotiation = 4,
    ClosedWon = 5,
    ClosedLost = 6
}

public enum ActivityType
{
    Call = 1,
    Email = 2,
    Meeting = 3,
    Task = 4,
    Reminder = 5
}

public enum ActivityStatus
{
    Pending = 1,
    Completed = 2,
    Cancelled = 3
}
