namespace CfiApp.Application.Maintenance;

public sealed class WorkOrderNotFoundException(int workOrderId)
    : Exception($"Work order {workOrderId} was not found.")
{
    public int WorkOrderId { get; } = workOrderId;
}

/// <summary>
/// The caller is authenticated and holds the permission the endpoint requires, but this
/// particular record says no - "you are not the engineer assigned to this job".
/// Maps to 403, not 401: the distinction the whole app is built around.
/// </summary>
public sealed class WorkOrderForbiddenException(string reason) : Exception(reason);

public sealed class UnknownReferenceException(string reason) : Exception(reason);
