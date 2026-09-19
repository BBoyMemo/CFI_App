import { apiClient } from './apiClient';

const v1 = (path) => `/api/v1${path}`;

// ---------------------------------------------------------------- auth

export const register = (payload) => apiClient.post(v1('/auth/register'), payload);
export const login = (payload) => apiClient.post(v1('/auth/login'), payload);
export const logout = (refreshToken) => apiClient.post(v1('/auth/logout'), { refreshToken });
export const getMe = () => apiClient.get(v1('/auth/me'));
export const setLanguage = (language) => apiClient.put(v1('/auth/me/language'), JSON.stringify(language), {
  headers: { 'Content-Type': 'application/json' },
});

// ---------------------------------------------------------------- reference data

export const getUnits = (params) => apiClient.get(v1('/admin/units'), { params });
export const getAreas = (params) => apiClient.get(v1('/admin/areas'), { params });
export const getLines = (params) => apiClient.get(v1('/admin/lines'), { params });
export const getEquipment = (params) => apiClient.get(v1('/admin/equipment'), { params });
export const getDepartments = (params) => apiClient.get(v1('/admin/departments'), { params });
export const getOccupations = (params) => apiClient.get(v1('/admin/occupations'), { params });

// ---------------------------------------------------------------- work orders

export const createWorkOrder = (payload) => apiClient.post(v1('/workorders'), payload);
export const getPool = (params) => apiClient.get(v1('/workorders/pool'), { params });
export const getMyJobs = (params) => apiClient.get(v1('/workorders/mine'), { params });
export const getWorkOrders = (params) => apiClient.get(v1('/workorders'), { params });
export const getWorkOrderHistory = (params) => apiClient.get(v1('/workorders/history'), { params });
export const getWorkOrder = (id) => apiClient.get(v1(`/workorders/${id}`));
export const claimWorkOrder = (id) => apiClient.post(v1(`/workorders/${id}/claim`));
export const assignWorkOrder = (id, engineerUserId) =>
  apiClient.post(v1(`/workorders/${id}/assign`), { engineerUserId });
export const notifyReporter = (id, kind) => apiClient.post(v1(`/workorders/${id}/notify`), { kind });
export const startWork = (id) => apiClient.post(v1(`/workorders/${id}/start`));
export const markWaitingParts = (id) => apiClient.post(v1(`/workorders/${id}/waiting-parts`));
export const resumeWork = (id) => apiClient.post(v1(`/workorders/${id}/resume`));
export const closeWorkOrder = (id, payload) => apiClient.post(v1(`/workorders/${id}/close`), payload);
export const submitQaResult = (id, payload) => apiClient.post(v1(`/workorders/${id}/qa-result`), payload);
export const signOffProduction = (id, payload) =>
  apiClient.post(v1(`/workorders/${id}/sign-off/production`), payload);
export const signOffQa = (id, payload) => apiClient.post(v1(`/workorders/${id}/sign-off/qa`), payload);

// ---------------------------------------------------------------- media

export const uploadPhoto = (file) => {
  const form = new FormData();
  form.append('file', file);
  return apiClient.post(v1('/media'), form, { headers: { 'Content-Type': 'multipart/form-data' } });
};

// ---------------------------------------------------------------- team / users

export const getTeam = (params) => apiClient.get(v1('/team'), { params });
export const getPendingUsers = (params) => apiClient.get(v1('/users/pending'), { params });
export const approveUser = (id, payload) => apiClient.post(v1(`/users/${id}/approve`), payload);
export const rejectPendingUser = (id, reason) => apiClient.post(v1(`/users/${id}/reject`), { reason });
export const disableUser = (id) => apiClient.post(v1(`/users/${id}/disable`));
export const getRoles = () => apiClient.get(v1('/admin/roles'));

// ---------------------------------------------------------------- tasks

export const createTask = (payload) => apiClient.post(v1('/tasks'), payload);
export const getMyTasks = (params) => apiClient.get(v1('/tasks/mine'), { params });
export const getCompletedTasks = (params) => apiClient.get(v1('/tasks/completed'), { params });
export const getAllTasks = (params) => apiClient.get(v1('/tasks'), { params });
export const getTask = (id) => apiClient.get(v1(`/tasks/${id}`));
export const reassignTask = (id, assignedUserIds) =>
  apiClient.put(v1(`/tasks/${id}/assignees`), { assignedUserIds });
export const completeTask = (id, payload) => apiClient.post(v1(`/tasks/${id}/complete`), payload);
export const logTaskProgress = (id, payload) => apiClient.post(v1(`/tasks/${id}/progress`), payload);

// ---------------------------------------------------------------- part orders

export const createOrder = (payload) => apiClient.post(v1('/orders'), payload);
export const getMyOrders = (params) => apiClient.get(v1('/orders/mine'), { params });
export const getAllOrders = (params) => apiClient.get(v1('/orders'), { params });
export const markOrderOrdered = (id) => apiClient.post(v1(`/orders/${id}/mark-ordered`));
export const deleteOrder = (id) => apiClient.delete(v1(`/orders/${id}`));

// ---------------------------------------------------------------- attendance

export const clock = (payload) => apiClient.post(v1('/attendance/clock'), payload);
export const getMyClockEvents = (params) => apiClient.get(v1('/attendance/mine'), { params });
export const getTeamHours = (params) => apiClient.get(v1('/attendance/team'), { params });
export const correctClockEvent = (clockEventId, payload) =>
  apiClient.post(v1(`/attendance/${clockEventId}/correction`), payload);
export const getGeofence = () => apiClient.get(v1('/attendance/geofence'));

// ---------------------------------------------------------------- overtime

export const declareOvertime = (payload) => apiClient.post(v1('/overtime'), payload);
export const getMyOvertime = (params) => apiClient.get(v1('/overtime/mine'), { params });
export const getTeamOvertime = (params) => apiClient.get(v1('/overtime'), { params });
export const decideOvertime = (id, payload) => apiClient.post(v1(`/overtime/${id}/decide`), payload);

// ---------------------------------------------------------------- holiday

export const requestHoliday = (payload) => apiClient.post(v1('/holiday'), payload);
export const getMyHoliday = (params) => apiClient.get(v1('/holiday/mine'), { params });
export const getTeamHoliday = (params) => apiClient.get(v1('/holiday'), { params });
export const decideHoliday = (id, payload) => apiClient.post(v1(`/holiday/${id}/decide`), payload);

// ---------------------------------------------------------------- shifts

export const getShiftTypes = (params) => apiClient.get(v1('/admin/shift-types'), { params });
export const deleteShiftType = (id) => apiClient.delete(v1(`/admin/shift-types/${id}`));
export const addShiftToPool = (shiftTypeId) => apiClient.post(v1('/shifts/pool'), { shiftTypeId });
export const removeShiftFromPool = (id) => apiClient.delete(v1(`/shifts/pool/${id}`));
export const getRoster = (params) => apiClient.get(v1('/shifts/roster'), { params });
export const setRoster = (payload) => apiClient.post(v1('/shifts/roster'), payload);
export const endRoster = (userId, payload) => apiClient.post(v1(`/shifts/roster/${userId}/end`), payload);
export const createCover = (payload) => apiClient.post(v1('/shifts/cover'), payload);
export const deleteCover = (id) => apiClient.delete(v1(`/shifts/cover/${id}`));
export const getShiftChanges = (params) => apiClient.get(v1('/shifts/changes'), { params });
export const getMyShifts = (params) => apiClient.get(v1('/shifts/mine'), { params });

// ---------------------------------------------------------------- messages

export const sendMessage = (payload) => apiClient.post(v1('/messages'), payload);
export const getInbox = (params) => apiClient.get(v1('/messages/inbox'), { params });
export const getMessage = (id) => apiClient.get(v1(`/messages/${id}`));
export const getReadReceipts = (id) => apiClient.get(v1(`/messages/${id}/read-receipts`));

// ---------------------------------------------------------------- admin site layout

export const upsertUnit = (payload) => apiClient.post(v1('/admin/units'), payload);
export const updateUnit = (id, payload) => apiClient.put(v1(`/admin/units/${id}`), payload);
export const deactivateUnit = (id) => apiClient.post(v1(`/admin/units/${id}/deactivate`));
export const activateUnit = (id) => apiClient.post(v1(`/admin/units/${id}/activate`));

export const upsertArea = (payload) => apiClient.post(v1('/admin/areas'), payload);
export const updateArea = (id, payload) => apiClient.put(v1(`/admin/areas/${id}`), payload);
export const deactivateArea = (id) => apiClient.post(v1(`/admin/areas/${id}/deactivate`));
export const activateArea = (id) => apiClient.post(v1(`/admin/areas/${id}/activate`));

export const upsertLine = (payload) => apiClient.post(v1('/admin/lines'), payload);
export const updateLine = (id, payload) => apiClient.put(v1(`/admin/lines/${id}`), payload);
export const deactivateLine = (id) => apiClient.post(v1(`/admin/lines/${id}/deactivate`));
export const activateLine = (id) => apiClient.post(v1(`/admin/lines/${id}/activate`));

export const upsertEquipment = (payload) => apiClient.post(v1('/admin/equipment'), payload);
export const updateEquipment = (id, payload) => apiClient.put(v1(`/admin/equipment/${id}`), payload);
export const deactivateEquipment = (id) => apiClient.post(v1(`/admin/equipment/${id}/deactivate`));
export const activateEquipment = (id) => apiClient.post(v1(`/admin/equipment/${id}/activate`));

export const upsertShiftType = (payload) => apiClient.post(v1('/admin/shift-types'), payload);
export const updateShiftType = (id, payload) => apiClient.put(v1(`/admin/shift-types/${id}`), payload);
export const deactivateShiftType = (id) => apiClient.post(v1(`/admin/shift-types/${id}/deactivate`));
export const activateShiftType = (id) => apiClient.post(v1(`/admin/shift-types/${id}/activate`));

// ---------------------------------------------------------------- account

export const changePassword = (payload) => apiClient.post(v1('/auth/change-password'), payload);
export const rejectWorkOrder = (id, reason) => apiClient.post(v1(`/workorders/${id}/reject`), { reason });

// ---------------------------------------------------------------- admin: attendance rules

export const getGeofences = () => apiClient.get(v1('/admin/geofences'));
export const createGeofence = (payload) => apiClient.post(v1('/admin/geofences'), payload);
export const updateGeofence = (id, payload) => apiClient.put(v1(`/admin/geofences/${id}`), payload);
export const activateGeofence = (id) => apiClient.post(v1(`/admin/geofences/${id}/activate`));
export const deactivateGeofence = (id) => apiClient.post(v1(`/admin/geofences/${id}/deactivate`));

export const getPublicHolidays = (params) => apiClient.get(v1('/admin/public-holidays'), { params });
export const createPublicHoliday = (payload) => apiClient.post(v1('/admin/public-holidays'), payload);
export const deletePublicHoliday = (id) => apiClient.delete(v1(`/admin/public-holidays/${id}`));

// ---------------------------------------------------------------- notifications

export const getNotifications = (params) => apiClient.get(v1('/notifications'), { params });
export const getUnseenNotifications = (since) =>
  apiClient.get(v1('/notifications/unseen-count'), { params: since ? { since } : undefined });
export const getReportedByMe = (params) => apiClient.get(v1('/workorders/reported-by-me'), { params });

// ---------------------------------------------------------------- admin: people setup

export const upsertDepartment = (payload) => apiClient.post(v1('/admin/departments'), payload);
export const deactivateDepartment = (id) => apiClient.post(v1(`/admin/departments/${id}/deactivate`));
export const activateDepartment = (id) => apiClient.post(v1(`/admin/departments/${id}/activate`));

export const upsertOccupation = (payload) => apiClient.post(v1('/admin/occupations'), payload);
export const deactivateOccupation = (id) => apiClient.post(v1(`/admin/occupations/${id}/deactivate`));
export const activateOccupation = (id) => apiClient.post(v1(`/admin/occupations/${id}/activate`));
export const signOffReporter = (id, payload) =>
  apiClient.post(v1(`/workorders/${id}/sign-off/reporter`), payload);
