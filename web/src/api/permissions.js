/**
 * Mirrors backend/src/CfiApp.Domain/Identity/Permissions.cs. The frontend never decides
 * who is allowed to do what - the access token already carries the permission list from
 * the server - this file only names the same strings so a screen can ask "do I have
 * workorder.claim" instead of hard-coding a role name.
 */
export const Permissions = {
  UserApprove: 'user.approve',
  UserManage: 'user.manage',

  WorkOrderCreate: 'workorder.create',
  WorkOrderViewOwn: 'workorder.viewOwn',
  WorkOrderViewAll: 'workorder.viewAll',
  WorkOrderHistory: 'workorder.history',
  WorkOrderClaim: 'workorder.claim',
  WorkOrderAssign: 'workorder.assign',
  WorkOrderClose: 'workorder.close',
  WorkOrderReject: 'workorder.reject',

  QaCheck: 'qa.check',
  QaSignOff: 'qa.signOff',
  ProductionSignOff: 'production.signOff',

  TaskViewAssigned: 'task.viewAssigned',
  TaskComplete: 'task.complete',
  TaskManage: 'task.manage',

  OrderCreate: 'order.create',
  OrderManageAll: 'order.manageAll',

  AttendanceClock: 'attendance.clock',
  AttendanceViewOwn: 'attendance.viewOwn',
  AttendanceViewTeam: 'attendance.viewTeam',
  AttendanceCorrect: 'attendance.correct',

  OvertimeDeclare: 'overtime.declare',
  OvertimeApprove: 'overtime.approve',

  HolidayRequest: 'holiday.request',
  HolidayApprove: 'holiday.approve',

  ShiftViewOwn: 'shift.viewOwn',
  ShiftPlan: 'shift.plan',

  MessageRead: 'message.read',
  MessageSend: 'message.send',

  AdminManage: 'admin.manage',
};
