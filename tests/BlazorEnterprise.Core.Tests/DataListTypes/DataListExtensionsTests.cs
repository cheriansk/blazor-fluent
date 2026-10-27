using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Shared.Extensions;
using FluentAssertions;
using Xunit;

namespace BlazorEnterprise.Core.Tests.DataListTypes;

public class DataListExtensionsTests
{
    #region Requirement 1: Display Name & Description Tests

    [Fact]
    public void GetEnumDisplayName_FromInt_ReturnsConfiguredDisplayName()
    {
        // Arrange & Act
        var queued = 1.GetEnumDisplayName<JobStatusEnum>();
        var running = 2.GetEnumDisplayName<JobStatusEnum>();
        var unknown = 999.GetEnumDisplayName<JobStatusEnum>();

        // Assert
        queued.Should().Be("Queued");
        running.Should().Be("Running");
        unknown.Should().Be("999");
    }

    [Fact]
    public void GetEnumDisplayName_FromNullableInt_ReturnsDisplayNameOrFallback()
    {
        // Arrange
        int? valid = 1;
        int? nullValue = null;
        int? undefined = 999;

        // Act & Assert
        valid.GetEnumDisplayName<JobStatusEnum>().Should().Be("Queued");
        nullValue.GetEnumDisplayName<JobStatusEnum>("DefaultFallback").Should().Be("DefaultFallback");
        undefined.GetEnumDisplayName<JobStatusEnum>("DefaultFallback").Should().Be("DefaultFallback");
    }

    [Fact]
    public void GetEnumDescription_FromInt_ReturnsConfiguredDescription()
    {
        // Arrange & Act
        var queuedDesc = 1.GetEnumDescription<JobStatusEnum>();
        var unknownDesc = 999.GetEnumDescription<JobStatusEnum>();

        // Assert
        queuedDesc.Should().Be("Job is queued in channel awaiting worker pickup");
        unknownDesc.Should().BeNull();
    }

    [Fact]
    public void GetEnumDescription_FromNullableInt_ReturnsDescriptionOrNull()
    {
        // Arrange
        int? valid = 2;
        int? nullValue = null;

        // Act & Assert
        valid.GetEnumDescription<JobStatusEnum>().Should().Be("Job is actively executing");
        nullValue.GetEnumDescription<JobStatusEnum>().Should().BeNull();
    }

    [Fact]
    public void GetEnumDisplayName_FromStringCode_IsCaseInsensitive()
    {
        // Arrange & Act & Assert
        "Queued".GetEnumDisplayName<JobStatusEnum>().Should().Be("Queued");
        "queued".GetEnumDisplayName<JobStatusEnum>().Should().Be("Queued");
        "RUNNING".GetEnumDisplayName<JobStatusEnum>().Should().Be("Running");
        "NonExistentCode".GetEnumDisplayName<JobStatusEnum>().Should().Be("NonExistentCode");
        ((string?)null).GetEnumDisplayName<JobStatusEnum>().Should().BeEmpty();
    }

    [Fact]
    public void GetEnumDescription_FromStringCode_ReturnsDescription()
    {
        // Arrange & Act & Assert
        "Running".GetEnumDescription<JobStatusEnum>().Should().Be("Job is actively executing");
        "running".GetEnumDescription<JobStatusEnum>().Should().Be("Job is actively executing");
        "Unknown".GetEnumDescription<JobStatusEnum>().Should().BeNull();
        ((string?)null).GetEnumDescription<JobStatusEnum>().Should().BeNull();
    }

    [Fact]
    public void GetDisplayName_And_GetDescription_OnEnumInstance_WorkDirectly()
    {
        // Arrange
        var val = JobStatusEnum.Succeeded;

        // Act & Assert
        val.GetDisplayName().Should().Be("Succeeded");
        val.GetDescription().Should().Be("Job completed successfully without errors");
    }

    #endregion

    #region Requirement 2: Enum Code from Integer & Value Conversions

    [Fact]
    public void GetEnumCode_FromInt_ReturnsCodeOrNull()
    {
        // Arrange & Act & Assert
        1.GetEnumCode<JobStatusEnum>().Should().Be("Queued");
        2.GetEnumCode<JobStatusEnum>().Should().Be("Running");
        999.GetEnumCode<JobStatusEnum>().Should().BeNull();
    }

    [Fact]
    public void GetEnumCode_FromNullableInt_ReturnsCodeOrNull()
    {
        // Arrange
        int? valid = 3;
        int? nullValue = null;
        int? undefined = 999;

        // Act & Assert
        valid.GetEnumCode<JobStatusEnum>().Should().Be("Succeeded");
        nullValue.GetEnumCode<JobStatusEnum>().Should().BeNull();
        undefined.GetEnumCode<JobStatusEnum>().Should().BeNull();
    }

    [Fact]
    public void ToEnum_FromInt_ValidCasts_InvalidThrows()
    {
        // Arrange & Act
        var result = 1.ToEnum<JobStatusEnum>();

        // Assert
        result.Should().Be(JobStatusEnum.Queued);

        var act = () => 999.ToEnum<JobStatusEnum>();
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ToEnum_FromIntWithDefault_ReturnsDefaultOnUndefined()
    {
        // Arrange & Act & Assert
        1.ToEnum(JobStatusEnum.Failed).Should().Be(JobStatusEnum.Queued);
        999.ToEnum(JobStatusEnum.Failed).Should().Be(JobStatusEnum.Failed);
    }

    [Fact]
    public void ToEnumOrNull_FromNullableInt_ReturnsEnumOrNull()
    {
        // Arrange & Act & Assert
        ((int?)1).ToEnumOrNull<JobStatusEnum>().Should().Be(JobStatusEnum.Queued);
        ((int?)null).ToEnumOrNull<JobStatusEnum>().Should().BeNull();
        ((int?)999).ToEnumOrNull<JobStatusEnum>().Should().BeNull();
    }

    [Fact]
    public void TryToEnum_FromInt_ReturnsTrueOrFalse()
    {
        // Arrange & Act
        var success = 2.TryToEnum<JobStatusEnum>(out var validResult);
        var fail = 999.TryToEnum<JobStatusEnum>(out var invalidResult);

        // Assert
        success.Should().BeTrue();
        validResult.Should().Be(JobStatusEnum.Running);

        fail.Should().BeFalse();
        invalidResult.Should().Be(default);
    }

    [Fact]
    public void ToEnum_FromString_CaseInsensitive_ValidParses_InvalidThrows()
    {
        // Arrange & Act & Assert
        "Queued".ToEnum<JobStatusEnum>().Should().Be(JobStatusEnum.Queued);
        "queued".ToEnum<JobStatusEnum>().Should().Be(JobStatusEnum.Queued);
        "RUNNING".ToEnum<JobStatusEnum>().Should().Be(JobStatusEnum.Running);

        var act = () => "Invalid".ToEnum<JobStatusEnum>();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToEnum_FromStringWithDefault_ReturnsDefaultOnInvalid()
    {
        // Arrange & Act & Assert
        "running".ToEnum(JobStatusEnum.Failed).Should().Be(JobStatusEnum.Running);
        "invalid".ToEnum(JobStatusEnum.Failed).Should().Be(JobStatusEnum.Failed);
        ((string?)null).ToEnum(JobStatusEnum.Failed).Should().Be(JobStatusEnum.Failed);
    }

    [Fact]
    public void ToEnumOrNull_FromString_ReturnsEnumOrNull()
    {
        // Arrange & Act & Assert
        "Succeeded".ToEnumOrNull<JobStatusEnum>().Should().Be(JobStatusEnum.Succeeded);
        "failed".ToEnumOrNull<JobStatusEnum>().Should().Be(JobStatusEnum.Failed);
        "invalid".ToEnumOrNull<JobStatusEnum>().Should().BeNull();
        ((string?)null).ToEnumOrNull<JobStatusEnum>().Should().BeNull();
    }

    [Fact]
    public void TryToEnum_FromString_ReturnsTrueOrFalse()
    {
        // Arrange & Act
        var success = "Retrying".TryToEnum<JobStatusEnum>(out var valid);
        var fail = "invalid".TryToEnum<JobStatusEnum>(out var invalid);

        // Assert
        success.Should().BeTrue();
        valid.Should().Be(JobStatusEnum.Retrying);

        fail.Should().BeFalse();
        invalid.Should().Be(default);
    }

    [Fact]
    public void GetCode_And_ToInt_OnEnumInstance_ReturnAccuratePrimitives()
    {
        // Arrange
        var val = JobStatusEnum.Retrying;

        // Act & Assert
        val.GetCode().Should().Be("Retrying");
        val.ToInt().Should().Be(5);
    }

    #endregion

    #region Requirement 3: Category-based Lookups & Metadata Tests

    [Fact]
    public void GetEnumCodesByCategory_ReturnsExpectedCodes_CaseInsensitive()
    {
        // Arrange & Act
        var activeCodes = DataListExtensions.GetEnumCodesByCategory<JobStatusEnum>("Active");
        var activeLowerCodes = DataListExtensions.GetEnumCodesByCategory<JobStatusEnum>("active");
        var completedCodes = DataListExtensions.GetEnumCodesByCategory<JobStatusEnum>("Completed");
        var unknownCodes = DataListExtensions.GetEnumCodesByCategory<JobStatusEnum>("UnknownCategory");

        // Assert
        activeCodes.Should().BeEquivalentTo(new[] { "Running", "Retrying" });
        activeLowerCodes.Should().BeEquivalentTo(new[] { "Running", "Retrying" });
        completedCodes.Should().BeEquivalentTo(new[] { "Succeeded", "Failed" });
        unknownCodes.Should().BeEmpty();
    }

    [Fact]
    public void GetEnumValuesByCategory_ReturnsTypedEnumInstances()
    {
        // Arrange & Act
        var activeValues = DataListExtensions.GetEnumValuesByCategory<JobStatusEnum>("Active");

        // Assert
        activeValues.Should().BeEquivalentTo(new[] { JobStatusEnum.Running, JobStatusEnum.Retrying });
    }

    [Fact]
    public void GetDataListItemsByCategory_ProjectsEnrichedDataListItems()
    {
        // Arrange & Act
        var items = DataListExtensions.GetDataListItemsByCategory<JobStatusEnum>("Pending");

        // Assert
        items.Should().HaveCount(1);
        var item = items[0];
        item.Value.Should().Be(JobStatusEnum.Queued);
        item.Code.Should().Be("Queued");
        item.DisplayName.Should().Be("Queued");
        item.Description.Should().Be("Job is queued in channel awaiting worker pickup");
        item.Category.Should().NotBeNull();
        item.Category!.Code.Should().Be("Pending");
        item.Category.DisplayName.Should().Be("Pending Execution");
    }

    [Fact]
    public void GetCategories_And_GetCategoryCodes_ReturnAllDefinedCategories()
    {
        // Arrange & Act
        var categories = DataListExtensions.GetCategories<JobStatusEnum>();
        var codes = DataListExtensions.GetCategoryCodes<JobStatusEnum>();

        // Assert
        codes.Should().BeEquivalentTo(new[] { "Pending", "Active", "Completed" });
        categories.Should().HaveCount(3);
        categories.Select(c => c.DisplayName).Should().Contain(new[] { "Pending Execution", "In Progress", "Terminal States" });
    }

    [Fact]
    public void HasCategory_And_GetCategory_OnInstance_WorkAccurately()
    {
        // Arrange
        var running = JobStatusEnum.Running;

        // Act & Assert
        running.HasCategory("Active").Should().BeTrue();
        running.HasCategory("active").Should().BeTrue();
        running.HasCategory("Completed").Should().BeFalse();

        var cat = running.GetCategory();
        cat.Should().NotBeNull();
        cat!.Code.Should().Be("Active");
        cat.DisplayName.Should().Be("In Progress");
    }

    #endregion
}
