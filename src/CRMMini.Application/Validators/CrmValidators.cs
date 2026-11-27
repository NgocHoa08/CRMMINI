namespace CRMMini.Application.Validators;

using CRMMini.Application.DTOs.Activities;
using CRMMini.Application.DTOs.Customers;
using CRMMini.Application.DTOs.Leads;
using CRMMini.Application.DTOs.Opportunities;
using CRMMini.Domain.Enums;
using FluentValidation;

public sealed class CreateCompanyRequestValidator : AbstractValidator<CreateCompanyRequest>
{
    public CreateCompanyRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TaxCode).MaximumLength(30);
        RuleFor(x => x.Industry).MaximumLength(100);
        RuleFor(x => x.Website).MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(20)
            .Matches(@"^[0-9+\-\s().]{8,20}$").When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Address).MaximumLength(300);
    }
}

public sealed class UpdateCompanyRequestValidator : AbstractValidator<UpdateCompanyRequest>
{
    public UpdateCompanyRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TaxCode).MaximumLength(30);
        RuleFor(x => x.Industry).MaximumLength(100);
        RuleFor(x => x.Website).MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(20)
            .Matches(@"^[0-9+\-\s().]{8,20}$").When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Address).MaximumLength(300);
    }
}

public sealed class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(20)
            .Matches(@"^[0-9+\-\s().]{8,20}$").When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Khách hàng phải có ít nhất Email hoặc Số điện thoại.");
        RuleFor(x => x.JobTitle).MaximumLength(100);
        RuleFor(x => x.Address).MaximumLength(300);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class UpdateCustomerRequestValidator : AbstractValidator<UpdateCustomerRequest>
{
    public UpdateCustomerRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(20)
            .Matches(@"^[0-9+\-\s().]{8,20}$").When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Khách hàng phải có ít nhất Email hoặc Số điện thoại.");
        RuleFor(x => x.JobTitle).MaximumLength(100);
        RuleFor(x => x.Address).MaximumLength(300);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class CreateLeadRequestValidator : AbstractValidator<CreateLeadRequest>
{
    public CreateLeadRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(20)
            .Matches(@"^[0-9+\-\s().]{8,20}$").When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Lead phải có ít nhất Email hoặc Số điện thoại.");
        RuleFor(x => x.CompanyName).MaximumLength(200);
        RuleFor(x => x.Source).IsInEnum();
        RuleFor(x => x.EstimatedValue).GreaterThanOrEqualTo(0).When(x => x.EstimatedValue.HasValue);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class UpdateLeadRequestValidator : AbstractValidator<UpdateLeadRequest>
{
    public UpdateLeadRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(20)
            .Matches(@"^[0-9+\-\s().]{8,20}$").When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Lead phải có ít nhất Email hoặc Số điện thoại.");
        RuleFor(x => x.CompanyName).MaximumLength(200);
        RuleFor(x => x.Source).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.EstimatedValue).GreaterThanOrEqualTo(0).When(x => x.EstimatedValue.HasValue);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class ConvertLeadRequestValidator : AbstractValidator<ConvertLeadRequest>
{
    public ConvertLeadRequestValidator()
    {
        RuleFor(x => x.OpportunityTitle)
            .NotEmpty().WithMessage("Vui lòng nhập tiêu đề Opportunity khi chọn tạo cơ hội kinh doanh.")
            .MaximumLength(200)
            .When(x => x.CreateOpportunity);
        RuleFor(x => x.OpportunityValue).GreaterThanOrEqualTo(0)
            .When(x => x.CreateOpportunity && x.OpportunityValue.HasValue);
    }
}

public sealed class CreateOpportunityRequestValidator : AbstractValidator<CreateOpportunityRequest>
{
    public CreateOpportunityRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Value).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Stage).IsInEnum();
        RuleFor(x => x.Probability).InclusiveBetween(0, 100).When(x => x.Probability.HasValue);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class UpdateOpportunityRequestValidator : AbstractValidator<UpdateOpportunityRequest>
{
    public UpdateOpportunityRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Value).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Probability).InclusiveBetween(0, 100).When(x => x.Probability.HasValue);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class ChangeOpportunityStageRequestValidator : AbstractValidator<ChangeOpportunityStageRequest>
{
    public ChangeOpportunityStageRequestValidator()
    {
        RuleFor(x => x.TargetStage).IsInEnum();
        RuleFor(x => x.Probability).InclusiveBetween(0, 100).When(x => x.Probability.HasValue);
        RuleFor(x => x.LossReason).NotEmpty().MaximumLength(500)
            .When(x => x.TargetStage == OpportunityStage.ClosedLost)
            .WithMessage("Bắt buộc nhập lý do thua (LossReason) khi chuyển sang ClosedLost.");
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class CreateActivityRequestValidator : AbstractValidator<CreateActivityRequest>
{
    public CreateActivityRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x)
            .Must(x => x.CustomerId.HasValue || x.LeadId.HasValue || x.OpportunityId.HasValue)
            .WithMessage("Activity phải liên kết với ít nhất một Customer, Lead hoặc Opportunity.");
        RuleFor(x => x.ReminderAt)
            .LessThanOrEqualTo(x => x.ScheduledAt)
            .When(x => x.ReminderAt.HasValue)
            .WithMessage("Thời gian nhắc lịch (ReminderAt) không được sau thời gian thực hiện (ScheduledAt).");
    }
}

public sealed class UpdateActivityRequestValidator : AbstractValidator<UpdateActivityRequest>
{
    public UpdateActivityRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.ReminderAt)
            .LessThanOrEqualTo(x => x.ScheduledAt)
            .When(x => x.ReminderAt.HasValue)
            .WithMessage("Thời gian nhắc lịch (ReminderAt) không được sau thời gian thực hiện (ScheduledAt).");
    }
}
