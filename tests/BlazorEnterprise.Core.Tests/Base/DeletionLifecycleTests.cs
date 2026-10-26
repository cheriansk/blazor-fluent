using System.Reflection;
using BlazorEnterprise.Core.Domain.Auditing;
using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Domain.Calendar;
using BlazorEnterprise.Core.Domain.Events;
using BlazorEnterprise.Core.Domain.Imports;
using BlazorEnterprise.Core.Domain.Jobs;
using BlazorEnterprise.Core.Domain.Knowledge;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Domain.Tenancy;
using FluentAssertions;
using Xunit;

namespace BlazorEnterprise.Core.Tests.Base;

public class DeletionLifecycleTests
{
    private static IEnumerable<Type> GetDomainEntityTypes()
    {
        return typeof(BaseEntity).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(BaseEntity).IsAssignableFrom(t));
    }

    [Fact]
    public void All_Domain_Entities_Must_Implement_At_Least_One_Deletion_Marker()
    {
        var entityTypes = GetDomainEntityTypes().ToList();
        entityTypes.Should().NotBeEmpty();

        foreach (var entityType in entityTypes)
        {
            var isSoft = typeof(ISoftDeletableEntity).IsAssignableFrom(entityType);
            var isHard = typeof(IHardDeletableEntity).IsAssignableFrom(entityType);
            var isImmutable = typeof(IImmutableEntity).IsAssignableFrom(entityType);

            var totalMarkers = (isSoft ? 1 : 0) + (isHard ? 1 : 0) + (isImmutable ? 1 : 0);

            totalMarkers.Should().BeGreaterThanOrEqualTo(
                1,
                $"Entity '{entityType.Name}' must implement at least one deletion marker (ISoftDeletableEntity, IHardDeletableEntity, or IImmutableEntity).");
        }
    }

    [Fact]
    public void All_Domain_Entities_Must_Implement_At_Most_One_Deletion_Marker()
    {
        var entityTypes = GetDomainEntityTypes().ToList();
        entityTypes.Should().NotBeEmpty();

        foreach (var entityType in entityTypes)
        {
            var isSoft = typeof(ISoftDeletableEntity).IsAssignableFrom(entityType);
            var isHard = typeof(IHardDeletableEntity).IsAssignableFrom(entityType);
            var isImmutable = typeof(IImmutableEntity).IsAssignableFrom(entityType);

            var totalMarkers = (isSoft ? 1 : 0) + (isHard ? 1 : 0) + (isImmutable ? 1 : 0);

            totalMarkers.Should().Be(
                1,
                $"Entity '{entityType.Name}' must implement at most ONE deletion marker (cannot mix Soft, Hard, and Immutable).");
        }
    }

    [Theory]
    [InlineData(typeof(AuditRecordEntity))]
    [InlineData(typeof(EventPublishTrackerEntity))]
    [InlineData(typeof(EventConsumptionTrackerEntity))]
    [InlineData(typeof(JobExecutionEntity))]
    public void Immutable_Ledgers_Must_Only_Implement_IImmutableEntity(Type entityType)
    {
        typeof(IImmutableEntity).IsAssignableFrom(entityType).Should().BeTrue();
        typeof(ISoftDeletableEntity).IsAssignableFrom(entityType).Should().BeFalse();
        typeof(IHardDeletableEntity).IsAssignableFrom(entityType).Should().BeFalse();
    }

    [Theory]
    [InlineData(typeof(TenantEntity))]
    [InlineData(typeof(ProjectCalendarEntity))]
    [InlineData(typeof(WorkDependencyEntity))]
    [InlineData(typeof(ProjectEntity))]
    [InlineData(typeof(ProgramEntity))]
    [InlineData(typeof(TaskEntity))]
    public void Business_Assets_Must_Only_Implement_ISoftDeletableEntity(Type entityType)
    {
        typeof(ISoftDeletableEntity).IsAssignableFrom(entityType).Should().BeTrue();
        typeof(IHardDeletableEntity).IsAssignableFrom(entityType).Should().BeFalse();
        typeof(IImmutableEntity).IsAssignableFrom(entityType).Should().BeFalse();
    }

    [Theory]
    [InlineData(typeof(KnowledgeArticleReviewerEntity))]
    [InlineData(typeof(StagedImportEntity))]
    public void Graph_Links_And_Staging_Records_Must_Only_Implement_IHardDeletableEntity(Type entityType)
    {
        typeof(IHardDeletableEntity).IsAssignableFrom(entityType).Should().BeTrue();
        typeof(ISoftDeletableEntity).IsAssignableFrom(entityType).Should().BeFalse();
        typeof(IImmutableEntity).IsAssignableFrom(entityType).Should().BeFalse();
    }
}
