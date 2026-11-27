namespace CRMMini.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using CRMMini.Application.Common;
using CRMMini.Application.DTOs.Activities;
using CRMMini.Application.DTOs.Auth;
using CRMMini.Application.DTOs.Customers;
using CRMMini.Application.DTOs.Opportunities;
using CRMMini.Application.DTOs.Users;
using CRMMini.Domain.Enums;

public sealed class UsersTeamsAndSwaggerIntegrationTests(CrmWebAppFactory factory) : IClassFixture<CrmWebAppFactory>
{
    [Fact]
    public async Task SwaggerJson_ContainsV1EndpointsAndBearerSecurityScheme()
    {
        using var client = factory.CreateClient();
        var json = await client.GetStringAsync("/swagger/v1/swagger.json");

        Assert.Contains("/api/v1/auth/login", json);
        Assert.Contains("/api/v1/customers", json);
        Assert.Contains("/api/v1/leads/{id}/convert", json);
        Assert.Contains("Bearer", json);
    }

    [Fact]
    public async Task TeamsAndUsers_Rbac_ProfileUpdate_And_TeamScoping_Work()
    {
        using var adminClient = await factory.CreateAuthenticatedClientAsync("admin@test.local");
        using var managerAClient = await factory.CreateAuthenticatedClientAsync("mgra@test.local");
        using var staffAClient = await factory.CreateAuthenticatedClientAsync("staffa@test.local");

        // Staff cannot list teams or users
        Assert.Equal(HttpStatusCode.Forbidden, (await staffAClient.GetAsync("/api/v1/teams")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staffAClient.GetAsync("/api/v1/users")).StatusCode);

        // Manager A sees only Team A and users in Team A
        var mgrTeams = await managerAClient.GetFromJsonAsync<IReadOnlyList<TeamDto>>("/api/v1/teams", CrmWebAppFactory.JsonOptions);
        Assert.Single(mgrTeams!);
        Assert.Equal(factory.TeamAId, mgrTeams![0].Id);

        var mgrUsers = await managerAClient.GetFromJsonAsync<PagedResult<UserProfileDto>>("/api/v1/users", CrmWebAppFactory.JsonOptions);
        Assert.All(mgrUsers!.Items, u => Assert.Equal(factory.TeamAId, u.TeamId));

        // Admin creates and updates a Team
        var code = $"T_{Guid.NewGuid():N}"[..10];
        var createTeamRes = await adminClient.PostAsJsonAsync("/api/v1/teams", new CreateTeamRequest($"Team {code}", code, null));
        Assert.Equal(HttpStatusCode.Created, createTeamRes.StatusCode);
        var createdTeam = (await createTeamRes.Content.ReadFromJsonAsync<TeamDto>(CrmWebAppFactory.JsonOptions))!;

        var updateTeamRes = await adminClient.PutAsJsonAsync($"/api/v1/teams/{createdTeam.Id}",
            new UpdateTeamRequest($"Team Updated {code}", factory.ManagerAId, true));
        Assert.Equal(HttpStatusCode.OK, updateTeamRes.StatusCode);

        // Staff updates own profile and verifies DB persistence
        var updateMeRes = await staffAClient.PutAsJsonAsync("/api/v1/users/me",
            new UpdateProfileRequest("Staff Alpha Updated", "0905556666"));
        Assert.Equal(HttpStatusCode.OK, updateMeRes.StatusCode);
        var updatedMe = await updateMeRes.Content.ReadFromJsonAsync<UserProfileDto>(CrmWebAppFactory.JsonOptions);
        Assert.Equal("Staff Alpha Updated", updatedMe!.FullName);

        var reloadedMe = await staffAClient.GetFromJsonAsync<UserProfileDto>("/api/v1/users/me", CrmWebAppFactory.JsonOptions);
        Assert.Equal("Staff Alpha Updated", reloadedMe!.FullName);
        Assert.Equal("0905556666", reloadedMe.Phone);
    }

    [Fact]
    public async Task Pagination_Sorting_OpportunityHistory_And_ActivityCompletion_Work()
    {
        using var staffAClient = await factory.CreateAuthenticatedClientAsync("staffa@test.local");
        using var staffBClient = await factory.CreateAuthenticatedClientAsync("staffb@test.local");
        using var managerAClient = await factory.CreateAuthenticatedClientAsync("mgra@test.local");

        var suffix = Guid.NewGuid().ToString("N")[..8];

        var compRes = await staffAClient.PostAsJsonAsync("/api/v1/companies", new CreateCompanyRequest(
            $"Solo Corp {suffix}", $"STAX-{suffix}", "Finance", null, null, null, null, null));
        Assert.Equal(HttpStatusCode.Created, compRes.StatusCode);
        var company = (await compRes.Content.ReadFromJsonAsync<CompanyDto>(CrmWebAppFactory.JsonOptions))!;

        var cust1 = await (await staffAClient.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest(
            $"AAA Customer {suffix}", $"aaa_{suffix}@example.com", null, null, null, CustomerStatus.Active, null, null, null)))
            .Content.ReadFromJsonAsync<CustomerDto>(CrmWebAppFactory.JsonOptions);
        var cust2 = await (await staffAClient.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest(
            $"ZZZ Customer {suffix}", $"zzz_{suffix}@example.com", null, null, null, CustomerStatus.Active, null, null, null)))
            .Content.ReadFromJsonAsync<CustomerDto>(CrmWebAppFactory.JsonOptions);

        // Pagination + desc sort by fullName
        var page1 = await staffAClient.GetFromJsonAsync<PagedResult<CustomerDto>>(
            $"/api/v1/customers?search={suffix}&sortBy=fullName&sortDir=desc&page=1&pageSize=1",
            CrmWebAppFactory.JsonOptions);
        Assert.Equal(2, page1!.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Single(page1.Items);
        Assert.Equal(cust2!.Id, page1.Items[0].Id);
        Assert.True(page1.HasNextPage);

        // Opportunity + Activity completion + Opportunity History
        var opp = await (await staffAClient.PostAsJsonAsync("/api/v1/opportunities", new CreateOpportunityRequest(
            $"History Opp {suffix}", cust1!.Id, null, 75_000_000m, OpportunityStage.NeedsAnalysis, null, DateTimeOffset.UtcNow.AddDays(10), null, null)))
            .Content.ReadFromJsonAsync<OpportunityDto>(CrmWebAppFactory.JsonOptions);

        var act = await (await staffAClient.PostAsJsonAsync("/api/v1/activities", new CreateActivityRequest(
            ActivityType.Email, $"Proposal Email {suffix}", null, DateTimeOffset.UtcNow.AddHours(3), null, cust1.Id, null, opp!.Id, null)))
            .Content.ReadFromJsonAsync<ActivityDto>(CrmWebAppFactory.JsonOptions);

        // Staff B cannot delete Staff A's activity -> 403
        Assert.Equal(HttpStatusCode.Forbidden, (await staffBClient.DeleteAsync($"/api/v1/activities/{act!.Id}")).StatusCode);

        // Staff A marks Activity Completed -> CompletedAt auto-populated
        var completeRes = await staffAClient.PutAsJsonAsync($"/api/v1/activities/{act.Id}", new UpdateActivityRequest(
            act.Type, act.Subject, "Sent email", ActivityStatus.Completed, act.ScheduledAt, null, null));
        Assert.Equal(HttpStatusCode.OK, completeRes.StatusCode);
        var completedAct = await completeRes.Content.ReadFromJsonAsync<ActivityDto>(CrmWebAppFactory.JsonOptions);
        Assert.NotNull(completedAct!.CompletedAt);

        var oppHistory = await staffAClient.GetFromJsonAsync<OpportunityHistoryDto>(
            $"/api/v1/opportunities/{opp.Id}/history", CrmWebAppFactory.JsonOptions);
        Assert.Single(oppHistory!.Activities);
        Assert.NotEmpty(oppHistory.AuditLogs);

        // Manager A can soft delete empty company -> 204
        Assert.Equal(HttpStatusCode.NoContent, (await managerAClient.DeleteAsync($"/api/v1/companies/{company.Id}")).StatusCode);
    }
}
