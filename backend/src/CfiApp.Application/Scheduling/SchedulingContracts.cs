namespace CfiApp.Application.Scheduling;

public sealed record CreateShiftAssignmentRequest(int UserId, DateOnly Date, int ShiftTypeId);

public sealed record ShiftAssignmentDto(
    int Id, int UserId, string UserFullName, DateOnly Date, int ShiftTypeId, string ShiftTypeName,
    TimeOnly StartTime, TimeOnly EndTime);
