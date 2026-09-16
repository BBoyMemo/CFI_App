namespace CfiApp.Domain.Maintenance;

/// <summary>
/// How urgent a breakdown is. Three levels, which is what the site actually uses on the
/// paper form - a fourth only ever produced arguments about where the line sat.
/// Values are explicit because they are persisted.
/// </summary>
public enum Priority
{
    Low = 0,
    Medium = 1,
    High = 2
}
