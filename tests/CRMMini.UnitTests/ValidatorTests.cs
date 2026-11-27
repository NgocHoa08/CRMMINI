namespace CRMMini.UnitTests;

using CRMMini.Application.DTOs.Activities;
using CRMMini.Application.DTOs.Auth;
using CRMMini.Application.DTOs.Customers;
using CRMMini.Application.DTOs.Leads;
using CRMMini.Application.DTOs.Opportunities;
using CRMMini.Application.Validators;
using CRMMini.Domain.Enums;

public sealed class ValidatorTests
{
    [Fact]
    public void RegisterValidator_WeakPassword_Fails()
    {
        var validator = new RegisterRequestValidator();
        var req = new RegisterRequest("nguyenvana", "a@example.com", "weak", "Nguyen Van A", null);

        var result = validator.Validate(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterRequest.Password));
    }

    [Fact]
    public void RegisterValidator_StrongInput_Passes()
    {
        var validator = new RegisterRequestValidator();
        var req = new RegisterRequest("nguyenvana", "a@example.com", "StrongPass#123", "Nguyen Van A", "0901234567");

        var result = validator.Validate(req);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CustomerValidator_MissingEmailAndPhone_Fails()
    {
        var validator = new CreateCustomerRequestValidator();
        var req = new CreateCustomerRequest("Tran Van B", null, null, null, null, CustomerStatus.Active, null, null, null);

        var result = validator.Validate(req);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void LeadValidator_NegativeEstimatedValue_Fails()
    {
        var validator = new CreateLeadRequestValidator();
        var req = new CreateLeadRequest("Lead X", "lead@example.com", null, null, LeadSource.Website, -100m, null, null);

        var result = validator.Validate(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateLeadRequest.EstimatedValue));
    }

    [Fact]
    public void ChangeOpportunityStageValidator_ClosedLostWithoutReason_Fails()
    {
        var validator = new ChangeOpportunityStageRequestValidator();
        var req = new ChangeOpportunityStageRequest(OpportunityStage.ClosedLost, null, null, null);

        var result = validator.Validate(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ChangeOpportunityStageRequest.LossReason));
    }

    [Fact]
    public void ActivityValidator_NoLinkedEntity_Fails()
    {
        var validator = new CreateActivityRequestValidator();
        var now = DateTimeOffset.UtcNow;
        var req = new CreateActivityRequest(ActivityType.Call, "Follow up", null, now.AddHours(2), null, null, null, null, null);

        var result = validator.Validate(req);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ActivityValidator_ReminderAfterScheduledAt_Fails()
    {
        var validator = new CreateActivityRequestValidator();
        var now = DateTimeOffset.UtcNow;
        var req = new CreateActivityRequest(
            ActivityType.Meeting,
            "Demo meeting",
            null,
            now.AddHours(1),
            now.AddHours(2),
            Guid.NewGuid(),
            null,
            null,
            null);

        var result = validator.Validate(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateActivityRequest.ReminderAt));
    }

    [Fact]
    public void ConvertLeadValidator_CreateOpportunityWithoutTitle_Fails()
    {
        var validator = new ConvertLeadRequestValidator();
        var req = new ConvertLeadRequest(CreateOpportunity: true, OpportunityTitle: null);

        var result = validator.Validate(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ConvertLeadRequest.OpportunityTitle));
    }
}
