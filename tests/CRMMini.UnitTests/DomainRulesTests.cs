namespace CRMMini.UnitTests;

using CRMMini.Domain.Enums;
using CRMMini.Domain.Rules;

public sealed class DomainRulesTests
{
    [Theory]
    [InlineData(OpportunityStage.Qualification, 10)]
    [InlineData(OpportunityStage.NeedsAnalysis, 25)]
    [InlineData(OpportunityStage.Proposal, 50)]
    [InlineData(OpportunityStage.Negotiation, 75)]
    [InlineData(OpportunityStage.ClosedWon, 100)]
    [InlineData(OpportunityStage.ClosedLost, 0)]
    public void OpportunityStageRules_GetDefaultProbability_ReturnsExpected(OpportunityStage stage, int expected)
    {
        Assert.Equal(expected, OpportunityStageRules.GetDefaultProbability(stage));
    }

    [Theory]
    [InlineData(OpportunityStage.Qualification, OpportunityStage.Proposal, false, true)]
    [InlineData(OpportunityStage.Negotiation, OpportunityStage.NeedsAnalysis, false, true)]
    [InlineData(OpportunityStage.Proposal, OpportunityStage.ClosedWon, false, true)]
    [InlineData(OpportunityStage.ClosedWon, OpportunityStage.Negotiation, false, false)]
    [InlineData(OpportunityStage.ClosedLost, OpportunityStage.Proposal, true, false)]
    [InlineData(OpportunityStage.ClosedWon, OpportunityStage.Negotiation, true, true)]
    [InlineData(OpportunityStage.ClosedLost, OpportunityStage.Negotiation, true, true)]
    public void OpportunityStageRules_CanTransition_RespectsPipelineAndRole(
        OpportunityStage current,
        OpportunityStage target,
        bool canReopen,
        bool expected)
    {
        var actual = OpportunityStageRules.CanTransition(current, target, canReopen);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(LeadStatus.New, LeadStatus.Contacted, true)]
    [InlineData(LeadStatus.Contacted, LeadStatus.Qualified, true)]
    [InlineData(LeadStatus.Qualified, LeadStatus.Disqualified, true)]
    [InlineData(LeadStatus.Disqualified, LeadStatus.New, true)]
    [InlineData(LeadStatus.Qualified, LeadStatus.Converted, false)]
    [InlineData(LeadStatus.Converted, LeadStatus.Qualified, false)]
    [InlineData(LeadStatus.Converted, LeadStatus.Disqualified, false)]
    public void LeadStatusRules_CanTransition_RespectsStateMachine(
        LeadStatus current,
        LeadStatus target,
        bool expected)
    {
        var actual = LeadStatusRules.CanTransition(current, target);
        Assert.Equal(expected, actual);
    }
}
