using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Core.Domain.Tasks.Validators;
using BlazorFluent.Shared.DataListTypes;
using FluentAssertions;
using Xunit;

namespace BlazorFluent.Core.Tests.Tasks;

public class WorkflowEntityValidatorTests
{
    private readonly WorkflowEntityValidator _validator = new();

    private static WorkflowEntity CreateBaseWorkflow() => new()
    {
        Id = Guid.CreateVersion7(),
        TenantId = "tenant_test",
        Level = TaskLevelEnum.Project,
        ProjectId = Guid.CreateVersion7(),
        Title = "Quarterly Release Pipeline",
        Description = "Orchestrate release readiness across dev, QA, and security",
        Priority = TaskPriorityEnum.High,
        Status = TaskStatusEnum.InProgress,
        StartDate = DateTime.UtcNow,
        DueDate = DateTime.UtcNow.AddDays(14),
        ReporterEmail = "admin@company.local",
        WorkflowOwnerEmail = "lead@company.local"
    };

    [Fact]
    public void Validate_ValidWorkflowOwner_Passes()
    {
        var workflow = CreateBaseWorkflow();

        var result = _validator.Validate(workflow);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_EmptyWorkflowOwner_Fails(string? emptyOwner)
    {
        var workflow = CreateBaseWorkflow();
        workflow.AssigneeEmails = emptyOwner;

        var result = _validator.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(WorkflowEntity.AssigneeEmails) &&
                                            e.ErrorMessage.Contains("Workflow Owner email is required"));
    }

    [Theory]
    [InlineData("lead@company.local;dev@company.local")]
    [InlineData("lead@company.local;")]
    public void Validate_MultipleAssignees_WithSemicolon_Fails(string multipleEmails)
    {
        var workflow = CreateBaseWorkflow();
        workflow.AssigneeEmails = multipleEmails;

        var result = _validator.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(WorkflowEntity.AssigneeEmails) &&
                                            e.ErrorMessage.Contains("Workflow Owner must be a single email address"));
    }

    [Theory]
    [InlineData("lead@company.local,dev@company.local")]
    [InlineData("lead@company.local,")]
    public void Validate_MultipleAssignees_WithComma_Fails(string multipleEmails)
    {
        var workflow = CreateBaseWorkflow();
        workflow.AssigneeEmails = multipleEmails;

        var result = _validator.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(WorkflowEntity.AssigneeEmails) &&
                                            e.ErrorMessage.Contains("Workflow Owner must be a single email address"));
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("notanemail.com")]
    [InlineData("@missingusername.com")]
    public void Validate_InvalidEmailFormat_Fails(string invalidEmail)
    {
        var workflow = CreateBaseWorkflow();
        workflow.AssigneeEmails = invalidEmail;

        var result = _validator.Validate(workflow);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(WorkflowEntity.AssigneeEmails) &&
                                            e.ErrorMessage.Contains("Workflow Owner must be a valid email address"));
    }

    [Fact]
    public void Validate_SettingViaWorkflowOwnerEmailAlias_Passes()
    {
        var workflow = CreateBaseWorkflow();
        workflow.WorkflowOwnerEmail = "owner.alias@company.local";

        workflow.AssigneeEmails.Should().Be("owner.alias@company.local");

        var result = _validator.Validate(workflow);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
