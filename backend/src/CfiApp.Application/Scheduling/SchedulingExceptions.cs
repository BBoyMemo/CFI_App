namespace CfiApp.Application.Scheduling;

/// <summary>
/// The request is understood but breaks a rota rule - a roster change dated in the past,
/// a shift type that is switched off. Maps to 400.
/// </summary>
public sealed class ShiftRuleException(string reason) : Exception(reason);

/// <summary>
/// The rota already says something that contradicts this - the person is on booked leave
/// those days, or another cover already covers them. Maps to 409, because the manager has
/// to decide which of the two is right rather than the server picking one.
/// </summary>
public sealed class ShiftClashException(string reason) : Exception(reason);
