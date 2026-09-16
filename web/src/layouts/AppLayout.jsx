import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { NavLink, Outlet } from 'react-router-dom';

import LanguageSelector from '../components/LanguageSelector';
import { getUnseenNotifications } from '../api/endpoints';
import { Permissions } from '../api/permissions';
import { useAuth } from '../auth/useAuth';
import { useApiData } from '../hooks/useApiData';
import SidePanel from './SidePanel';
import { useClockState } from './useClockState';

/**
 * Where each role goes, in the order the approved prototypes put it.
 *
 * `null` means everyone signed in. Keeping the whole map here is deliberate: adding a
 * screen later is one row, not a hunt through JSX for who is currently allowed where.
 */
const MENU = [
  // Home and the three work-order destinations come first. They are also on the home
  // screen itself, but a menu that cannot reach the pool leaves somebody stranded the
  // moment they navigate away from it.
  { to: '/', labelKey: 'nav.dashboard', icon: 'home', permission: null, end: true },
  { to: '/workorders/new', labelKey: 'nav.createReport', icon: 'wrench', permission: Permissions.WorkOrderCreate },
  { to: '/workorders/mine', labelKey: 'nav.myJobs', icon: 'clipboard', permission: Permissions.WorkOrderClaim },
  { to: '/workorders/pool', labelKey: 'nav.pool', icon: 'box', permission: Permissions.WorkOrderClaim },

  { to: '/attendance', labelKey: 'nav.attendance', icon: 'calendar', permission: Permissions.AttendanceViewOwn },
  { to: '/shifts/mine', labelKey: 'nav.myShifts', icon: 'clock', permission: Permissions.ShiftViewOwn },
  { to: '/notifications', labelKey: 'nav.notifications', icon: 'bell', permission: null },
  { to: '/tasks', labelKey: 'nav.tasks', icon: 'clipboard', permission: Permissions.TaskViewAssigned },
  { to: '/orders', labelKey: 'nav.orders', icon: 'box', permission: Permissions.OrderCreate },
  { to: '/workorders/history', labelKey: 'nav.history', icon: 'history', permission: Permissions.WorkOrderHistory },
  { to: '/holiday', labelKey: 'nav.holiday', icon: 'sun', permission: null },
  { to: '/shifts', labelKey: 'nav.shiftPlanner', icon: 'clock', permission: Permissions.ShiftPlan },
  { to: '/attendance/who-is-in', labelKey: 'nav.whoIsIn', icon: 'users', permission: Permissions.AttendanceViewTeam },
  { to: '/attendance/team', labelKey: 'nav.teamHours', icon: 'history', permission: Permissions.AttendanceViewTeam },
  { to: '/messages', labelKey: 'nav.messages', icon: 'message', permission: Permissions.MessageSend },
  { to: '/team', labelKey: 'nav.team', icon: 'users', permission: Permissions.AttendanceViewTeam },
  { to: '/approvals', labelKey: 'nav.pendingApprovals', icon: 'person', permission: Permissions.UserApprove },
  { to: '/profile', labelKey: 'nav.profile', icon: 'settings', permission: null },
];

export default function AppLayout() {
  const { t } = useTranslation();
  const { me, logout, hasPermission } = useAuth();

  const [menuOpen, setMenuOpen] = useState(false);
  const [now, setNow] = useState(() => new Date());

  const clockState = useClockState();

  // The wall clock in the status strip, as the prototypes show it.
  useEffect(() => {
    const timer = setInterval(() => setNow(new Date()), 30_000);
    return () => clearInterval(timer);
  }, []);

  const unseen = useApiData(() => getUnseenNotifications());
  const unseenCount = unseen.data?.count ?? 0;

  const items = MENU
    .filter((item) => item.permission === null || hasPermission(item.permission))
    .map((item) => (item.to === '/notifications' ? { ...item, badge: unseenCount } : item));

  const firstName = me?.fullName?.split(' ')[0] ?? '';

  return (
    <div className="min-h-screen">
      {/* Status strip: is this device actually talking to the factory, and what time is
          it. Both are things a person on the floor checks without being asked to. */}
      <div className="flex items-center gap-2 bg-cfi-brown-dark px-4 py-1.5 text-[11px] text-white/85 sm:px-6">
        <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-cfi-green" />
        <span className="font-mono uppercase tracking-wide opacity-80">{t('app.networkStatus')}</span>
        <span className="ml-auto font-mono">
          {now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
        </span>
      </div>

      <header className="border-b border-cfi-rule bg-cfi-surface">
        <div className="mx-auto flex max-w-6xl items-center gap-3 px-4 py-3 sm:px-6">
          <button
            type="button"
            onClick={() => setMenuOpen(true)}
            aria-label={t('nav.menu')}
            className="flex h-11 w-11 shrink-0 flex-col items-center justify-center gap-1 rounded hover:bg-cfi-sunk"
          >
            <span className="block h-0.5 w-5 rounded bg-cfi-brown-dark" />
            <span className="block h-0.5 w-5 rounded bg-cfi-brown-dark" />
            <span className="block h-0.5 w-5 rounded bg-cfi-brown-dark" />
          </button>

          <NavLink to="/" className="min-w-0 flex-1">
            <div className="truncate text-lg font-bold text-cfi-brown-dark">
              {t('app.greeting', { name: firstName })}
            </div>
            <div className="truncate text-xs text-cfi-muted">{me?.role}</div>
          </NavLink>

          {/* Status only, never a button - the button lives in the side panel. That was
              settled in the prototype after several rounds, so it stays settled here. */}
          <span
            className={`hidden shrink-0 rounded-full px-3 py-1 text-xs font-semibold sm:inline ${
              clockState.isClockedIn
                ? 'bg-cfi-green/15 text-cfi-green-dark'
                : 'bg-cfi-sunk text-cfi-muted'
            }`}
          >
            {clockState.isClockedIn ? t('attendance.clockedIn') : t('attendance.clockedOut')}
          </span>

          <LanguageSelector />
        </div>
      </header>

      <SidePanel
        open={menuOpen}
        onClose={() => setMenuOpen(false)}
        items={items}
        me={me}
        clockState={clockState}
        onSignOut={logout}
      />

      <main className="mx-auto max-w-6xl px-4 py-6 sm:px-6">
        <Outlet />
      </main>
    </div>
  );
}
