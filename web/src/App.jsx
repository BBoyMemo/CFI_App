import { Route, Routes } from 'react-router-dom';

import LoginPage from './auth/LoginPage';
import RegisterPage from './auth/RegisterPage';
import RequireAuth from './auth/RequireAuth';
import RequirePermission from './auth/RequirePermission';
import AppLayout from './layouts/AppLayout';
import DashboardPage from './features/dashboard/DashboardPage';
import CreateReportPage from './features/workorders/CreateReportPage';
import PoolPage from './features/workorders/PoolPage';
import MyJobsPage from './features/workorders/MyJobsPage';
import HistoryPage from './features/workorders/HistoryPage';
import WorkOrderDetailPage from './features/workorders/WorkOrderDetailPage';
import TasksPage from './features/tasks/TasksPage';
import TaskDetailPage from './features/tasks/TaskDetailPage';
import OrdersPage from './features/orders/OrdersPage';
import AttendancePage from './features/attendance/AttendancePage';
import TeamHoursPage from './features/attendance/TeamHoursPage';
import WhoIsInPage from './features/attendance/WhoIsInPage';
import OvertimePage from './features/attendance/OvertimePage';
import HolidayPage from './features/attendance/HolidayPage';
import ShiftPlannerPage from './features/shifts/ShiftPlannerPage';
import MyShiftsPage from './features/shifts/MyShiftsPage';
import NotificationsPage from './features/notifications/NotificationsPage';
import MessagesPage from './features/messages/MessagesPage';
import MessageDetailPage from './features/messages/MessageDetailPage';
import TeamPage from './features/team/TeamPage';
import PendingApprovalsPage from './features/team/PendingApprovalsPage';
import AdminPage from './features/admin/AdminPage';
import ProfilePage from './features/profile/ProfilePage';
import SystemStatusPage from './features/status/SystemStatusPage';
import { Permissions } from './api/permissions';

/** One row per screen, so who-can-see-what is readable top to bottom. */
const PROTECTED = [
  { path: '/workorders/new', permission: Permissions.WorkOrderCreate, element: <CreateReportPage /> },
  { path: '/workorders/pool', permission: Permissions.WorkOrderClaim, element: <PoolPage /> },
  { path: '/workorders/mine', permission: Permissions.WorkOrderClaim, element: <MyJobsPage /> },
  { path: '/workorders/history', permission: Permissions.WorkOrderHistory, element: <HistoryPage /> },
  { path: '/tasks', permission: Permissions.TaskViewAssigned, element: <TasksPage /> },
  { path: '/tasks/:id', permission: Permissions.TaskViewAssigned, element: <TaskDetailPage /> },
  { path: '/orders', permission: Permissions.OrderCreate, element: <OrdersPage /> },
  { path: '/attendance', permission: Permissions.AttendanceViewOwn, element: <AttendancePage /> },
  { path: '/attendance/team', permission: Permissions.AttendanceViewTeam, element: <TeamHoursPage /> },
  { path: '/attendance/who-is-in', permission: Permissions.AttendanceViewTeam, element: <WhoIsInPage /> },
  { path: '/overtime', permission: Permissions.OvertimeDeclare, element: <OvertimePage /> },
  { path: '/shifts', permission: Permissions.ShiftPlan, element: <ShiftPlannerPage /> },
  { path: '/shifts/mine', permission: Permissions.ShiftViewOwn, element: <MyShiftsPage /> },
  // The inbox-and-compose screen is a sender's tool, which is why only senders have it in
  // the menu. A single message stays open to anyone who may read one, so the link on a
  // notification works for the person it was sent to.
  { path: '/messages', permission: Permissions.MessageSend, element: <MessagesPage /> },
  { path: '/messages/:id', permission: Permissions.MessageRead, element: <MessageDetailPage /> },
  { path: '/team', permission: Permissions.AttendanceViewTeam, element: <TeamPage /> },
  { path: '/approvals', permission: Permissions.UserApprove, element: <PendingApprovalsPage /> },
  { path: '/admin', permission: Permissions.AdminManage, element: <AdminPage /> },
  // Diagnostic tool, not a feature - reachable but deliberately off the main nav.
  { path: '/status', permission: Permissions.AdminManage, element: <SystemStatusPage /> },
];

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />

      <Route
        element={
          <RequireAuth>
            <AppLayout />
          </RequireAuth>
        }
      >
        <Route path="/" element={<DashboardPage />} />
        <Route path="/profile" element={<ProfilePage />} />

        {/* Everyone gets told things, so everyone can read what they were told. */}
        <Route path="/notifications" element={<NotificationsPage />} />

        {/* The one screen two different permissions open: a worker books leave here, a
            manager approves it here, and each sees only their half. */}
        <Route
          path="/holiday"
          element={(
            <RequirePermission anyOf={[Permissions.HolidayRequest, Permissions.HolidayApprove]}>
              <HolidayPage />
            </RequirePermission>
          )}
        />

        {/* Any signed-in person can open a job they are allowed to see; the endpoint
            itself decides whether this particular one is theirs to read. */}
        <Route path="/workorders/:id" element={<WorkOrderDetailPage />} />

        {PROTECTED.map(({ path, permission, element }) => (
          <Route
            key={path}
            path={path}
            element={<RequirePermission permission={permission}>{element}</RequirePermission>}
          />
        ))}
      </Route>
    </Routes>
  );
}
