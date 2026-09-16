namespace CfiApp.Application.Abstractions;

/// <summary>
/// What one manager is allowed to see: their own department, plus anything their
/// ManagerScope rows add - another department, or a unit whose workers report to them
/// whichever department they sit in.
///
/// Nobody sees everything, the Maintenance Manager included. They run maintenance, so
/// they see engineers and QA; operators belong to production and show up in the
/// Production Manager's list instead. Being the site's admin is about approving accounts
/// and editing the site layout, not about reading everyone's hours.
/// </summary>
public sealed record ManagerVisibility(
    IReadOnlyCollection<int> DepartmentIds,
    IReadOnlyCollection<int> UnitIds);

public interface IManagerScopeReader
{
    Task<ManagerVisibility> GetVisibilityAsync(int managerUserId, CancellationToken cancellationToken);
}
