import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { NavLink, Outlet } from 'react-router-dom';

import Icon from '../components/ui/Icon';
import LanguageSelector from '../components/LanguageSelector';
import { getUnseenNotifications } from '../api/endpoints';
import { Permissions } from '../api/permissions';
import { useAuth } from '../auth/useAuth';
import { useApiData } from '../hooks/useApiData';
import SidePanel from './SidePanel';
import { useClockState } from './useClockState';

/**
 * The screens that live on the bar under the header, always one tap away.
 *
 * These are the places somebody comes back to all shift. They are deliberately *not* also
 * in the side panel: two routes to the same screen makes the panel long enough that the
 * things only reachable there get lost in it.
 *
 * `null` means everyone signed in. Nobody sees the whole list - an operator has no pool and
 * no history - so the bar is as short as the person's job is.
 */
const PRIMARY = [
  { to: '/', labelKey: 'nav.dashboard', icon: 'home', permission: null, end: true },
  { to: '/workorders/new', labelKey: 'nav.createReport', icon: 'wrench', permission: Permissions.WorkOrderCreate },
  { to: '/workorders/pool', labelKey: 'nav.pool', icon: 'box', permission: Permissions.WorkOrderClaim },
  { to: '/workorders/mine', labelKey: 'nav.myJobs', icon: 'clipboard', permission: Permissions.WorkOrderClaim },
  { to: '/tasks', labelKey: 'nav.tasks', icon: 'clipboard', permission: Permissions.TaskViewAssigned },
  { to: '/workorders/history', labelKey: 'nav.history', icon: 'history', permission: Permissions.WorkOrderHistory },
  { to: '/messages', labelKey: 'nav.messages', icon: 'message', permission: Permissions.MessageSend },
  { to: '/notifications', labelKey: 'nav.notifications', icon: 'bell', permission: null },
];

/**
 * Everything else, behind the hamburger. Reached a few times a shift rather than
 * constantly: booking leave, planning a rota, letting a new starter in.
 *
 * Keeping the whole map here is deliberate: adding a screen later is one row, not a hunt
 * through JSX for who is currently allowed where.
 */
const MENU = [
  { to: '/attendance', labelKey: 'nav.attendance', icon: 'calendar', permission: Permissions.AttendanceViewOwn },
  { to: '/shifts/mine', labelKey: 'nav.myShifts', icon: 'clock', permission: Permissions.ShiftViewOwn },
  { to: '/orders', labelKey: 'nav.orders', icon: 'box', permission: Permissions.OrderCreate },
  { to: '/holiday', labelKey: 'nav.holiday', icon: 'sun', permission: null },
  { to: '/shifts', labelKey: 'nav.shiftPlanner', icon: 'clock', permission: Permissions.ShiftPlan },
  { to: '/attendance/team', labelKey: 'nav.teamHours', icon: 'history', permission: Permissions.AttendanceViewTeam },
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

  const allowed = (menu) => menu
    .filter((item) => item.permission === null || hasPermission(item.permission))
    .map((item) => (item.to === '/notifications' ? { ...item, badge: unseenCount } : item));

  const primary = allowed(PRIMARY);
  const items = allowed(MENU);

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

      {/* The bar the shift actually uses. It scrolls sideways rather than wrapping: on a
          phone a second row would push the page content below the fold, and a factory
          floor thumb swipes more reliably than it hits a 3mm target. */}
      {primary.length > 1 && (
        <nav className="border-b border-cfi-rule bg-cfi-surface">
          <div className="mx-auto flex max-w-6xl gap-1 overflow-x-auto px-2 sm:px-4">
            {primary.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                className={({ isActive }) =>
                  `relative flex min-h-11 shrink-0 items-center gap-2 border-b-2 px-3 text-sm font-semibold transition-colors ${
                    isActive
                      ? 'border-cfi-yellow-dark text-cfi-brown-dark'
                      : 'border-transparent text-cfi-muted hover:text-cfi-brown-dark'
                  }`
                }
              >
                <Icon name={item.icon} />
                <span>{t(item.labelKey)}</span>

                {item.badge > 0 && (
                  <span className="rounded-full bg-cfi-red px-1.5 text-[11px] font-bold text-white">
                    {item.badge}
                  </span>
                )}
              </NavLink>
            ))}
          </div>
        </nav>
      )}

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
