using System.Text;
using BlazorFluent.RulesEngine.Analytics;
using BlazorFluent.RulesEngine.Calendar;
using BlazorFluent.RulesEngine.Parsers;
using BlazorFluent.RulesEngine.Workflows;
using BlazorFluent.Shared.Contracts.Engine.Analytics;
using BlazorFluent.Shared.Contracts.Engine.Calendar;
using BlazorFluent.Shared.Contracts.Engine.Workflows;
using BlazorFluent.Shared.DataListTypes;
using FluentAssertions;
using Xunit;

namespace BlazorFluent.Core.Tests.RulesEngine;

public class RulesEngineUnitTests
{
    [Fact]
    public void DependencyDagEngine_DetectsCycleInCircularGraph()
    {
        var nodeA = Guid.NewGuid();
        var nodeB = Guid.NewGuid();
        var nodeC = Guid.NewGuid();

        // A -> B -> C -> A (cycle)
        var edges = new List<DependencyEdge>
        {
            new(nodeA, nodeB, "DependsOn"),
            new(nodeB, nodeC, "DependsOn"),
            new(nodeC, nodeA, "DependsOn")
        };

        var req = new DependencyDagReqDto(Guid.NewGuid(), [nodeA, nodeB, nodeC], edges);
        var result = DependencyDagEngine.Analyze(req);

        result.HasCycle.Should().BeTrue();
        result.Cycles.Should().NotBeEmpty();
    }

    [Fact]
    public void DependencyDagEngine_ComputesTopologicalOrderInAcyclicGraph()
    {
        var nodeA = Guid.NewGuid();
        var nodeB = Guid.NewGuid();
        var nodeC = Guid.NewGuid();

        // A -> B -> C (linear, no cycle)
        var edges = new List<DependencyEdge>
        {
            new(nodeA, nodeB, "DependsOn"),
            new(nodeB, nodeC, "DependsOn")
        };

        var req = new DependencyDagReqDto(Guid.NewGuid(), [nodeA, nodeB, nodeC], edges);
        var result = DependencyDagEngine.Analyze(req);

        result.HasCycle.Should().BeFalse();
        result.Cycles.Should().BeEmpty();
        result.CriticalPath.Should().ContainInOrder(nodeA, nodeB, nodeC);
    }

    [Fact]
    public void ProgramRollupEngine_CalculatesWeightedProgressAndCriticalHealth()
    {
        var proj1 = new ProjectRollupInput(Guid.NewGuid(), 10, 8, 0, 0, 0m, ProjectHealthRatingEnum.OnTrack);
        var proj2 = new ProjectRollupInput(Guid.NewGuid(), 10, 2, 5, 6, -1000m, ProjectHealthRatingEnum.Critical);

        var req = new ProgramRollupReqDto(Guid.NewGuid(), [proj1, proj2]);
        var result = ProgramRollupEngine.ComputeRollup(req);

        // 10/20 tasks completed = 50.0%
        result.AggregateProgressPercentage.Should().Be(50.0);
        result.TotalActiveBlockers.Should().Be(6);
        result.ComputedHealth.Should().Be(ProgramHealthRatingEnum.Critical);
        result.RiskAlerts.Should().NotBeEmpty();
    }

    [Fact]
    public void CalendarWorkingDayEngine_CalculatesScheduleExcludingWeekendsAndHolidays()
    {
        // Monday 2026-10-12
        var startDate = new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);
        // Friday 2026-10-16 is a dynamic holiday
        var holiday = new DateTime(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc);

        // 5 working days: Mon(1), Tue(2), Wed(3), Thu(4), [Fri Holiday], [Sat Weekend], [Sun Weekend], Mon 2026-10-19(5)
        var req = new CalendarScheduleReqDto(startDate, 5, [holiday], ExcludeWeekends: true);
        var result = CalendarWorkingDayEngine.Calculate(req);

        result.WorkingDaysCount.Should().Be(5);
        result.NonWorkingDaysCount.Should().Be(3); // Fri holiday, Sat, Sun
        result.AdjustedEndDateUtc.Date.Should().Be(new DateTime(2026, 10, 19));
    }

    [Fact]
    public void CsvTabularParser_ParsesValidCsvRows()
    {
        var csvContent = "Title,Status\nImplement Phase 2,Completed\nDeploy to Cloud,Draft\n";
        var bytes = Encoding.UTF8.GetBytes(csvContent);

        var result = CsvTabularParser.Parse(bytes, ["Title", "Status"]);

        result.IsValid.Should().BeTrue();
        result.ValidRows.Should().HaveCount(2);
        result.ValidRows[0]["Title"].Should().Be("Implement Phase 2");
        result.ValidRows[0]["Status"].Should().Be("Completed");
    }

    [Fact]
    public void WorkflowTransitionEngine_AllowsPermittedTransitionsAndBlocksInvalid()
    {
        var entityId = Guid.NewGuid();

        // Draft -> PendingReview is permitted
        var validReq = new WorkflowTransitionReqDto(entityId, "Task", "Draft", "PendingReview", "user1", new Dictionary<string, string>());
        var validResp = WorkflowTransitionEngine.Evaluate(validReq);
        validResp.IsAllowed.Should().BeTrue();
        validResp.NewState.Should().Be("PendingReview");

        // Draft -> Completed directly is not permitted
        var invalidReq = new WorkflowTransitionReqDto(entityId, "Task", "Draft", "Completed", "user1", new Dictionary<string, string>());
        var invalidResp = WorkflowTransitionEngine.Evaluate(invalidReq);
        invalidResp.IsAllowed.Should().BeFalse();
        invalidResp.NewState.Should().Be("Draft");
        invalidResp.GuardViolations.Should().NotBeEmpty();
    }
}
