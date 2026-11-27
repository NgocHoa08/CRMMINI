namespace CRMMini.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using CRMMini.Application.Common;
using CRMMini.Application.DTOs.Activities;
using CRMMini.Application.DTOs.Customers;
using CRMMini.Application.DTOs.Opportunities;
using CRMMini.Domain.Enums;

public sealed class CustomerAndCompanyIntegrationTests(CrmWebAppFactory factory) : IClassFixture<CrmWebAppFactory>
{
    [Fact]
    public async Task CompanyAndCustomer_Crud_Duplicate_DataScope_SoftDelete_And_History_Work()
    {
        using var staffAClient = await factory.CreateAuthenticatedClientAsync("staffa@test.local");
        using var staffBClient = await factory.CreateAuthenticatedClientAsync("staffb@test.local");
        using var managerAClient = await factory.CreateAuthenticatedClientAsync("mgra@test.local");

        var suffix = Guid.NewGuid().ToString("N")[..8];

        // 1. Create Company by Staff A
        var compRes = await staffAClient.PostAsJsonAsync("/api/v1/companies", new CreateCompanyRequest(
            $"Acme Corp {suffix}",
            $"TAX-{suffix}",
            "Software",
            "https://acme.example.com",
            "0241234567",
            $"acme_{suffix}@example.com",
            "Ha Noi",
            null));
        Assert.Equal(HttpStatusCode.Created, compRes.StatusCode);
        var company = (await compRes.Content.ReadFromJsonAsync<CompanyDto>(CrmWebAppFactory.JsonOptions))!;

        // Duplicate TaxCode -> 409 Conflict
        var dupCompRes = await staffAClient.PostAsJsonAsync("/api/v1/companies", new CreateCompanyRequest(
            "Duplicate Tax Corp", $"TAX-{suffix}", "Software", null, null, null, null, null));
        Assert.Equal(HttpStatusCode.Conflict, dupCompRes.StatusCode);

        // 2. Create Customer by Staff A
        var custRes = await staffAClient.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest(
            $"Customer Alpha {suffix}",
            $"cust_{suffix}@example.com",
            $"091{Random.Shared.Next(1000000, 9999999)}",
            "Director",
            "Ha Noi",
            CustomerStatus.Active,
            "VIP customer",
            company.Id,
            null));
        Assert.Equal(HttpStatusCode.Created, custRes.StatusCode);
        var customer = (await custRes.Content.ReadFromJsonAsync<CustomerDto>(CrmWebAppFactory.JsonOptions))!;

        // Duplicate customer email -> 409 Conflict
        var dupCustRes = await staffAClient.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest(
            "Another Name",
            $"CUST_{suffix}@example.com",
            null,
            null,
            null,
            CustomerStatus.Active,
            null,
            null,
            null));
        Assert.Equal(HttpStatusCode.Conflict, dupCustRes.StatusCode);

        // 3. Data-scope: Staff B (Team B) cannot read or modify Staff A's customer
        var staffBGet = await staffBClient.GetAsync($"/api/v1/customers/{customer.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, staffBGet.StatusCode);

        var staffBList = await staffBClient.GetFromJsonAsync<PagedResult<CustomerDto>>(
            $"/api/v1/customers?search={suffix}", CrmWebAppFactory.JsonOptions);
        Assert.DoesNotContain(staffBList!.Items, c => c.Id == customer.Id);

        // Manager A (Team A) can read and filter Staff A's customer
        var mgrList = await managerAClient.GetFromJsonAsync<PagedResult<CustomerDto>>(
            $"/api/v1/customers?search={suffix}&page=1&pageSize=10", CrmWebAppFactory.JsonOptions);
        Assert.Contains(mgrList!.Items, c => c.Id == customer.Id);

        // 4. Create Opportunity & Activity -> check Customer History
        var oppRes = await staffAClient.PostAsJsonAsync("/api/v1/opportunities", new CreateOpportunityRequest(
            $"Deal {suffix}",
            customer.Id,
            company.Id,
            120_000_000m,
            OpportunityStage.Qualification,
            null,
            DateTimeOffset.UtcNow.AddDays(15),
            "Initial opp",
            null));
        Assert.Equal(HttpStatusCode.Created, oppRes.StatusCode);

        var actRes = await staffAClient.PostAsJsonAsync("/api/v1/activities", new CreateActivityRequest(
            ActivityType.Call,
            $"Intro call {suffix}",
            "Discussed scope",
            DateTimeOffset.UtcNow.AddDays(1),
            DateTimeOffset.UtcNow.AddHours(20),
            customer.Id,
            null,
            null,
            null));
        Assert.Equal(HttpStatusCode.Created, actRes.StatusCode);

        var history = await staffAClient.GetFromJsonAsync<CustomerHistoryDto>(
            $"/api/v1/customers/{customer.Id}/history", CrmWebAppFactory.JsonOptions);
        Assert.NotNull(history);
        Assert.Single(history!.Opportunities);
        Assert.Single(history.Activities);
        Assert.NotEmpty(history.AuditLogs);

        // 5. Soft delete: Staff A forbidden, Manager A allowed
        var staffDelete = await staffAClient.DeleteAsync($"/api/v1/customers/{customer.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, staffDelete.StatusCode);

        var mgrDelete = await managerAClient.DeleteAsync($"/api/v1/customers/{customer.Id}");
        Assert.Equal(HttpStatusCode.NoContent, mgrDelete.StatusCode);

        var afterDelete = await managerAClient.GetAsync($"/api/v1/customers/{customer.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }
}
