namespace CRMMini.Domain.Rules;

using CRMMini.Domain.Enums;

public static class OpportunityStageRules
{
    public static int GetDefaultProbability(OpportunityStage stage) => stage switch
    {
        OpportunityStage.Qualification => 10,
        OpportunityStage.NeedsAnalysis => 25,
        OpportunityStage.Proposal => 50,
        OpportunityStage.Negotiation => 75,
        OpportunityStage.ClosedWon => 100,
        OpportunityStage.ClosedLost => 0,
        _ => 0
    };

    public static bool IsClosed(OpportunityStage stage)
        => stage is OpportunityStage.ClosedWon or OpportunityStage.ClosedLost;

    public static bool CanTransition(OpportunityStage current, OpportunityStage target, bool canReopenClosed)
    {
        if (current == target)
            return true;

        if (IsClosed(current))
            return canReopenClosed && target == OpportunityStage.Negotiation;

        return true;
    }
}
