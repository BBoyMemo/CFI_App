namespace CfiApp.Application.Admin;

// All reference data here is CRUD-managed rather than hardcoded, and none of it is ever
// hard deleted through the API: a row is switched off (IsActive = false) instead, because
// old work orders, users and history rows keep pointing at it.

public sealed record DepartmentDto(int Id, string Name, bool IsActive);
public sealed record UpsertDepartmentRequest(string Name);

public sealed record OccupationDto(int Id, string Name, bool IsActive);
public sealed record UpsertOccupationRequest(string Name);

public sealed record ShiftTypeDto(
    int Id, string Name, TimeOnly StartTime, TimeOnly EndTime, int DisplayOrder, bool IsActive);

public sealed record UpsertShiftTypeRequest(string Name, TimeOnly StartTime, TimeOnly EndTime, int DisplayOrder);

public sealed record UnitDto(int Id, string Name, string Code, int DisplayOrder, bool IsActive);
public sealed record UpsertUnitRequest(string Name, string Code, int DisplayOrder);

public sealed record AreaDto(
    int Id, int UnitId, string UnitName, string Name, string? Code, int DisplayOrder,
    bool IsActive, bool IsWorkArea);

public sealed record UpsertAreaRequest(int UnitId, string Name, string? Code, int DisplayOrder, bool IsWorkArea);

public sealed record LineDto(
    int Id, int UnitId, int? AreaId, string Name, int DisplayOrder, bool IsActive);

public sealed record UpsertLineRequest(int UnitId, int? AreaId, string Name, int DisplayOrder);

public sealed record EquipmentDto(
    int Id,
    int UnitId,
    int? AreaId,
    int? LineId,
    /// <summary>Set when this is a part of a bigger machine - FIBC1 belongs to Blender 2.</summary>
    int? ParentEquipmentId,
    string? ParentEquipmentName,
    string Name,
    string? IconKey,
    int DisplayOrder,
    bool IsActive);

public sealed record UpsertEquipmentRequest(
    int UnitId,
    int? AreaId,
    int? LineId,
    int? ParentEquipmentId,
    string Name,
    string? IconKey,
    int DisplayOrder);

/// <summary>
/// Exactly one of DepartmentId or UnitId is set - the database enforces the same rule
/// with a check constraint, this is the request-time mirror of it.
/// </summary>
public sealed record AssignManagerScopeRequest(int UserId, int? DepartmentId, int? UnitId);

public sealed record ManagerScopeDto(
    int Id, int UserId, string UserFullName, int? DepartmentId, string? DepartmentName, int? UnitId, string? UnitName);

public sealed record TeamMemberDto(
    int Id, string FullName, string Email, string? Role, string? Occupation, string? Department);
