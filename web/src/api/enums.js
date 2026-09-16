/**
 * The API serialises C# enums as their numeric ordinal, not their name (System.Text.Json's
 * default, and switching it would mean re-verifying every already-tested endpoint - not a
 * change to make lightly). These arrays are ordinal-indexed to match the backend enums
 * exactly, so a numeric value from the API becomes the right i18n key.
 *
 * Keep in sync with:
 *   backend/src/CfiApp.Domain/Maintenance/WorkOrder.cs      (WorkOrderStatus, PhotoCategory)
 *   backend/src/CfiApp.Domain/Maintenance/Priority.cs       (Priority)
 *   backend/src/CfiApp.Domain/Maintenance/WorkOrderClosure.cs (QaResult, SignOffKind)
 *   backend/src/CfiApp.Domain/Work/MaintenanceTask.cs           (TaskKind, TaskPriority)
 *   backend/src/CfiApp.Domain/Attendance/ClockEvent.cs          (ClockType, ClockSource)
 *   backend/src/CfiApp.Domain/Messaging/Message.cs              (MessagePriority)
 */
export const WorkOrderStatus = [
  'New', 'Accepted', 'InProgress', 'WaitingParts', 'AwaitingQa', 'QaFailed', 'Completed', 'Rejected',
];

export const Priority = ['Low', 'Medium', 'High'];

export const JobType = ['Reactive', 'Task', 'Ppm', 'Project'];

export const QaResult = ['Pending', 'Pass', 'Fail'];

export const TaskKind = ['Daily', 'Weekend', 'ManagerAssigned'];

export const TaskPriority = ['Reactive', 'Corrective', 'Preventive'];

export const ClockType = ['In', 'Out'];

export const ClockSource = ['AutoGeofence', 'Manual'];

export const MessagePriority = ['Normal', 'High'];

export const workOrderStatusKey = (value) => `workorder.status_${WorkOrderStatus[value] ?? 'New'}`;
export const priorityKey = (value) => `workorder.priority_${Priority[value] ?? 'Low'}`;
export const jobTypeKey = (value) => `workorder.jobType_${JobType[value] ?? 'Reactive'}`;

/** Tailwind classes per status, grouped by what the colour is meant to say at a glance. */
export const statusTone = (value) => {
  const name = WorkOrderStatus[value];

  if (name === 'Completed') return 'bg-cfi-green/15 text-cfi-green-dark';
  if (name === 'QaFailed' || name === 'Rejected') return 'bg-cfi-red/15 text-cfi-red';
  if (name === 'AwaitingQa' || name === 'WaitingParts') return 'bg-cfi-yellow/25 text-cfi-yellow-dark';
  return 'bg-cfi-sunk text-cfi-brown-dark'; // New, Accepted, InProgress
};

export const priorityTone = (value) => {
  const name = Priority[value];

  if (name === 'High') return 'bg-cfi-red/15 text-cfi-red';
  if (name === 'Medium') return 'bg-cfi-yellow/25 text-cfi-yellow-dark';
  return 'bg-cfi-sunk text-cfi-muted'; // Low
};

export const taskKindKey = (value) => `task.kind_${TaskKind[value] ?? 'Daily'}`;
export const taskPriorityKey = (value) => `task.priority_${TaskPriority[value] ?? 'Reactive'}`;
export const clockTypeKey = (value) => `attendance.clock_${ClockType[value] ?? 'In'}`;

/** Task kinds are groupings rather than severities, so they get one calm tone each. */
export const taskKindTone = (value) => {
  const name = TaskKind[value];

  if (name === 'ManagerAssigned') return 'bg-cfi-yellow/25 text-cfi-yellow-dark';
  if (name === 'Weekend') return 'bg-cfi-brown/10 text-cfi-brown-dark';
  return 'bg-cfi-sunk text-cfi-muted'; // Daily
};

/** Approval states arrive as strings from the API, not as ordinals. */
export const decisionTone = (status) => {
  if (status === 'Approved' || status === 'Ordered') return 'bg-cfi-green/15 text-cfi-green-dark';
  if (status === 'Rejected') return 'bg-cfi-red/15 text-cfi-red';
  return 'bg-cfi-yellow/25 text-cfi-yellow-dark'; // Pending
};
