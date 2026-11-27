namespace CRMMini.Domain.Rules;

using CRMMini.Domain.Enums;

public static class LeadStatusRules
{
    public static bool CanTransition(LeadStatus current, LeadStatus target)
    {
        if (current == target)
            return true;

        if (current == LeadStatus.Converted)
            return false;

        if (target == LeadStatus.Converted)
            return false; // Conversion must go through the dedicated Convert workflow

        return current switch
        {
            LeadStatus.New => target is LeadStatus.Contacted or LeadStatus.Qualified or LeadStatus.Disqualified,
            LeadStatus.Contacted => target is LeadStatus.New or LeadStatus.Qualified or LeadStatus.Disqualified,
            LeadStatus.Qualified => target is LeadStatus.Contacted or LeadStatus.Disqualified,
            LeadStatus.Disqualified => target is LeadStatus.New or LeadStatus.Contacted,
            _ => false
        };
    }
}
