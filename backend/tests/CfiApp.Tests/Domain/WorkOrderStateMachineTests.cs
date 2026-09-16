using CfiApp.Domain.Maintenance;
using Shouldly;

namespace CfiApp.Tests.Domain;

/// <summary>
/// The lifecycle the site agreed, asserted directly. If a future change breaks one of
/// these, it breaks a rule someone on the factory floor depends on.
/// </summary>
public sealed class WorkOrderStateMachineTests
{
    [Theory]
    [InlineData(WorkOrderStatus.New, WorkOrderStatus.Accepted)]
    [InlineData(WorkOrderStatus.Accepted, WorkOrderStatus.InProgress)]
    [InlineData(WorkOrderStatus.InProgress, WorkOrderStatus.WaitingParts)]
    [InlineData(WorkOrderStatus.InProgress, WorkOrderStatus.AwaitingQa)]
    [InlineData(WorkOrderStatus.InProgress, WorkOrderStatus.Completed)]
    [InlineData(WorkOrderStatus.WaitingParts, WorkOrderStatus.InProgress)]
    [InlineData(WorkOrderStatus.AwaitingQa, WorkOrderStatus.Completed)]
    [InlineData(WorkOrderStatus.AwaitingQa, WorkOrderStatus.QaFailed)]
    [InlineData(WorkOrderStatus.QaFailed, WorkOrderStatus.AwaitingQa)]
    [InlineData(WorkOrderStatus.Accepted, WorkOrderStatus.New)]
    public void Allows_the_transitions_the_workflow_needs(WorkOrderStatus from, WorkOrderStatus to) =>
        WorkOrderStateMachine.CanTransition(from, to).ShouldBeTrue();

    [Theory]
    // Work cannot skip being claimed: the pool is how engineers pick up jobs.
    [InlineData(WorkOrderStatus.New, WorkOrderStatus.Completed)]
    [InlineData(WorkOrderStatus.New, WorkOrderStatus.InProgress)]
    [InlineData(WorkOrderStatus.New, WorkOrderStatus.AwaitingQa)]
    // A closed job is never reopened; a new report is raised instead.
    [InlineData(WorkOrderStatus.Completed, WorkOrderStatus.InProgress)]
    [InlineData(WorkOrderStatus.Completed, WorkOrderStatus.QaFailed)]
    [InlineData(WorkOrderStatus.Rejected, WorkOrderStatus.New)]
    // QA is the only route out of AwaitingQa.
    [InlineData(WorkOrderStatus.AwaitingQa, WorkOrderStatus.InProgress)]
    [InlineData(WorkOrderStatus.AwaitingQa, WorkOrderStatus.WaitingParts)]
    // A failed swab cannot be closed without going back through QA.
    [InlineData(WorkOrderStatus.QaFailed, WorkOrderStatus.Completed)]
    public void Rejects_transitions_that_would_break_the_workflow(
        WorkOrderStatus from,
        WorkOrderStatus to) =>
        WorkOrderStateMachine.CanTransition(from, to).ShouldBeFalse();

    [Theory]
    [InlineData(WorkOrderStatus.New)]
    [InlineData(WorkOrderStatus.Completed)]
    public void A_status_never_transitions_to_itself(WorkOrderStatus status) =>
        WorkOrderStateMachine.CanTransition(status, status).ShouldBeFalse();

    [Fact]
    public void Completed_and_rejected_are_the_only_terminal_states()
    {
        foreach (var status in Enum.GetValues<WorkOrderStatus>())
        {
            var expected = status is WorkOrderStatus.Completed or WorkOrderStatus.Rejected;
            WorkOrderStateMachine.IsTerminal(status).ShouldBe(expected, status.ToString());
            WorkOrderStateMachine.NextStatuses(status).Any().ShouldBe(!expected, status.ToString());
        }
    }

    [Fact]
    public void Every_status_has_a_rule_so_a_new_one_cannot_be_forgotten()
    {
        foreach (var status in Enum.GetValues<WorkOrderStatus>())
        {
            // NextStatuses returning an empty set is only valid for terminal states; any
            // other status with no rule means the enum grew and the map did not.
            var hasRule = WorkOrderStateMachine.NextStatuses(status).Count > 0
                          || WorkOrderStateMachine.IsTerminal(status);

            hasRule.ShouldBeTrue($"{status} has no transition rule");
        }
    }

    [Fact]
    public void An_invalid_transition_throws_with_both_states_named()
    {
        var exception = Should.Throw<InvalidWorkOrderTransitionException>(() =>
            WorkOrderStateMachine.EnsureCanTransition(WorkOrderStatus.New, WorkOrderStatus.Completed));

        exception.From.ShouldBe(WorkOrderStatus.New);
        exception.To.ShouldBe(WorkOrderStatus.Completed);
    }
}
