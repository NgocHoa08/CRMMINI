namespace CRMMini.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using CRMMini.Application.Common;
using CRMMini.Application.DTOs.Activities;
using CRMMini.Application.DTOs.Dashboard;
using CRMMini.Application.DTOs.Leads;
using CRMMini.Application.DTOs.Opportunities;
using CRMMini.Domain.Enums;

public sealed class PipelineAndDashboardIntegrationTests(CrmWebAppFactory factory) : IClassFixture<CrmWebAppFactory>
{
    [Fact]
    public async Task LeadConvert_OpportunityPipeline_Activity_And_ScopedDashboard_WorkEndToEnd()
    {
        using var adminClient = await factory.CreateAuthenticatedClientAsync("admin@test.local");
        using var managerAClient = await factory.CreateAuthenticatedClientAsync("mgra@test.local");
        using var staffAClient = await factory.CreateAuthenticatedClientAsync("staffa@test.local");
        using var staffBClient = await factory.CreateAuthenticatedClientAsync("staffb@test.local");

        var suffix = Guid.NewGuid().ToString("N")[..8];

        // 1. Staff A creates a Lead
        var leadRes = await staffAClient.PostAsJsonAsync("/api/v1/leads", new CreateLeadRequest(
            $"Lead Prospect {suffix}",
            $"lead_{suffix}@example.com",
            $"092{Random.Shared.Next(1000000, 9999999)}",
            $"Prospect Co {suffix}",
            LeadSource.Website,
            200_000_000m,
            "Inbound inquiry",
            null));
        Assert.Equal(HttpStatusCode.Created, leadRes.StatusCode);
        var lead = (await leadRes.Content.ReadFromJsonAsync<LeadDto>(CrmWebAppFactory.JsonOptions))!;

        // Cannot convert while Status == New
        var prematureConvert = await staffAClient.PostAsJsonAsync(
            $"/api/v1/leads/{lead.Id}/convert",
            new ConvertLeadRequest());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, prematureConvert.StatusCode);

        // Manager A cannot assign Lead to Staff B (different team) -> 403
        var crossTeamAssign = await managerAClient.PostAsJsonAsync(
            $"/api/v1/leads/{lead.Id}/assign",
            new AssignOwnerRequest(factory.StaffBId));
        Assert.Equal(HttpStatusCode.Forbidden, crossTeamAssign.StatusCode);

        // Move Lead: New -> Contacted -> Qualified
        var toContacted = await staffAClient.PutAsJsonAsync($"/api/v1/leads/{lead.Id}", new UpdateLeadRequest(
            lead.FullName, lead.Email, lead.Phone, lead.CompanyName, lead.Source, LeadStatus.Contacted, lead.EstimatedValue, lead.Notes));
        Assert.Equal(HttpStatusCode.OK, toContacted.StatusCode);

        var toQualified = await staffAClient.PutAsJsonAsync($"/api/v1/leads/{lead.Id}", new UpdateLeadRequest(
            lead.FullName, lead.Email, lead.Phone, lead.CompanyName, lead.Source, LeadStatus.Qualified, lead.EstimatedValue, lead.Notes));
        Assert.Equal(HttpStatusCode.OK, toQualified.StatusCode);

        // 2. Convert Lead into Customer + Company + Opportunity
        var convertRes = await staffAClient.PostAsJsonAsync(
            $"/api/v1/leads/{lead.Id}/convert",
            new ConvertLeadRequest(
                CreateCompanyIfMissing: true,
                CreateOpportunity: true,
                OpportunityTitle: $"Converted Opp {suffix}",
                OpportunityValue: 220_000_000m,
                ExpectedCloseDate: DateTimeOffset.UtcNow.AddDays(30)));
        Assert.Equal(HttpStatusCode.OK, convertRes.StatusCode);
        var converted = (await convertRes.Content.ReadFromJsonAsync<ConvertLeadResponse>(CrmWebAppFactory.JsonOptions))!;
        Assert.NotNull(converted.CompanyId);
        Assert.NotNull(converted.OpportunityId);

        // Double convert returns 409 Conflict
        var secondConvert = await staffAClient.PostAsJsonAsync(
            $"/api/v1/leads/{lead.Id}/convert",
            new ConvertLeadRequest());
        Assert.Equal(HttpStatusCode.Conflict, secondConvert.StatusCode);

        // 3. Opportunity pipeline transitions
        var oppId = converted.OpportunityId!.Value;

        // ClosedLost without LossReason -> 400
        var invalidLoss = await staffAClient.PostAsJsonAsync(
            $"/api/v1/opportunities/{oppId}/stage",
            new ChangeOpportunityStageRequest(OpportunityStage.ClosedLost, null, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, invalidLoss.StatusCode);

        // Advance to ClosedWon
        var winRes = await staffAClient.PostAsJsonAsync(
            $"/api/v1/opportunities/{oppId}/stage",
            new ChangeOpportunityStageRequest(OpportunityStage.ClosedWon, null, null, "Signed contract"));
        Assert.Equal(HttpStatusCode.OK, winRes.StatusCode);
        var wonOpp = (await winRes.Content.ReadFromJsonAsync<OpportunityDto>(CrmWebAppFactory.JsonOptions))!;
        Assert.Equal(100, wonOpp.Probability);
        Assert.NotNull(wonOpp.ActualCloseDate);

        // Staff cannot reopen ClosedWon -> 403 Forbidden
        var staffReopen = await staffAClient.PostAsJsonAsync(
            $"/api/v1/opportunities/{oppId}/stage",
            new ChangeOpportunityStageRequest(OpportunityStage.Negotiation, null, null, null));
        Assert.Equal(HttpStatusCode.Forbidden, staffReopen.StatusCode);

        // Manager A cannot reopen directly to Proposal -> 422 UnprocessableEntity
        var invalidMgrReopen = await managerAClient.PostAsJsonAsync(
            $"/api/v1/opportunities/{oppId}/stage",
            new ChangeOpportunityStageRequest(OpportunityStage.Proposal, null, null, null));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidMgrReopen.StatusCode);

        // Manager A can reopen to Negotiation -> 200
        var mgrReopen = await managerAClient.PostAsJsonAsync(
            $"/api/v1/opportunities/{oppId}/stage",
            new ChangeOpportunityStageRequest(OpportunityStage.Negotiation, null, null, "Customer requested addendum"));
        Assert.Equal(HttpStatusCode.OK, mgrReopen.StatusCode);

        // 4. Staff B creates their own Lead so Admin vs Team A vs Staff B dashboard scoping is verifiable
        var leadBRes = await staffBClient.PostAsJsonAsync("/api/v1/leads", new CreateLeadRequest(
            $"StaffB Lead {suffix}",
            $"leadb_{suffix}@example.com",
            null,
            null,
            LeadSource.Referral,
            50_000_000m,
            null,
            null));
        Assert.Equal(HttpStatusCode.Created, leadBRes.StatusCode);

        // 5. Overdue activity created by Staff A
        var overdueActRes = await staffAClient.PostAsJsonAsync("/api/v1/activities", new CreateActivityRequest(
            ActivityType.Task,
            $"Overdue contract review {suffix}",
            null,
            DateTimeOffset.UtcNow.AddHours(-5),
            null,
            converted.CustomerId,
            null,
            oppId,
            null));
        Assert.Equal(HttpStatusCode.Created, overdueActRes.StatusCode);

        var overdueList = await staffAClient.GetFromJsonAsync<PagedResult<ActivityDto>>(
            "/api/v1/activities?overdueOnly=true", CrmWebAppFactory.JsonOptions);
        Assert.NotEmpty(overdueList!.Items);

        // 6. Dashboard scoping verification
        var adminSummary = await adminClient.GetFromJsonAsync<DashboardSummaryDto>("/api/v1/dashboard/summary", CrmWebAppFactory.JsonOptions);
        var staffASummary = await staffAClient.GetFromJsonAsync<DashboardSummaryDto>("/api/v1/dashboard/summary", CrmWebAppFactory.JsonOptions);
        var staffBSummary = await staffBClient.GetFromJsonAsync<DashboardSummaryDto>("/api/v1/dashboard/summary", CrmWebAppFactory.JsonOptions);
        var pipeline = await managerAClient.GetFromJsonAsync<DashboardPipelineDto>("/api/v1/dashboard/pipeline", CrmWebAppFactory.JsonOptions);
        var actSummary = await staffAClient.GetFromJsonAsync<DashboardActivitiesDto>("/api/v1/dashboard/activities", CrmWebAppFactory.JsonOptions);

        Assert.True(adminSummary!.TotalLeads >= staffASummary!.TotalLeads + staffBSummary!.TotalLeads);
        Assert.True(staffASummary.OpenOpportunitiesCount >= 1);
        Assert.Equal(0, staffBSummary.OpenOpportunitiesCount);
        Assert.NotEmpty(pipeline!.Stages);
        Assert.True(actSummary!.OverdueCount >= 1);
    }
}
