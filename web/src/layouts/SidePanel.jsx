import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { NavLink } from 'react-router-dom';

import Icon from '../components/ui/Icon';
import { formatHours } from '../features/attendance/hours';

/**
 * The hamburger panel from the approved prototypes.
 *
 * Its header carries the real Clock In/Out button - that placement was settled after
 * several rounds in the prototype (notes 04, §3): the main header shows the status as
 * text only, and the button that changes it lives here, beside the person's name.
 *
 * Everything else is one icon and one word per row, because this is read at arm's length
 * on a factory floor.
 */
export default function SidePanel({ open, onClose, items, me, clockState, onSignOut }) {
  const { t } = useTranslation();
  const [busy, setBusy] = useState(false);

  const punch = async () => {
    setBusy(true);
    try {
      await clockState.punch();
    } catch {
      /* the attendance screen is where a failure gets explained; the button just stays put */
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <div
        onClick={onClose}
        className={`fixed inset-0 z-30 bg-black/40 transition-opacity ${
          open ? 'opacity-100' : 'pointer-events-none opacity-0'
        }`}
        aria-hidden="true"
      />

      <aside
        className={`fixed inset-y-0 left-0 z-40 flex w-72 max-w-[85vw] flex-col bg-cfi-surface shadow-xl transition-transform ${
          open ? 'translate-x-0' : '-translate-x-full'
        }`}
      >
        <div className="border-b border-cfi-rule p-4">
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <div className="truncate font-bold text-cfi-brown-dark">{me?.fullName}</div>
              <div className="text-xs text-cfi-muted">{me?.role}</div>
            </div>

            <button
              type="button"
              disabled={busy}
              onClick={punch}
              className={`shrink-0 rounded-full px-3 py-2 text-xs font-bold transition-colors disabled:opacity-50 ${
                clockState.isClockedIn
                  ? 'bg-cfi-yellow text-cfi-brown-dark'
                  : 'bg-cfi-green text-white'
              }`}
            >
              {clockState.isClockedIn ? t('attendance.clock_Out') : t('attendance.clock_In')}
            </button>
          </div>

          {clockState.isClockedIn && (
            <div className="mt-3 flex items-center gap-2">
              <span className="h-2 w-2 animate-pulse rounded-full bg-cfi-green" />
              <span className="font-mono text-[11px] uppercase tracking-wide text-cfi-green-dark">
                {t('attendance.onSiteSince', {
                  time: new Date(clockState.items[0].occurredAtUtc).toLocaleTimeString([], {
                    hour: '2-digit', minute: '2-digit',
                  }),
                })}
              </span>
            </div>
          )}

          <div className="mt-2 text-xs text-cfi-muted">
            {t('attendance.todayTotal')}: <span className="font-semibold text-cfi-ink">{formatHours(clockState.todayMinutes)}</span>
          </div>
        </div>

        <nav className="flex-1 overflow-y-auto p-2">
          {items.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              onClick={onClose}
              className={({ isActive }) =>
                `flex items-center gap-3 rounded px-3 py-3 text-sm font-semibold transition-colors ${
                  isActive ? 'bg-cfi-yellow text-cfi-brown-dark' : 'text-cfi-ink hover:bg-cfi-sunk'
                }`}
            >
              <Icon name={item.icon} />
              <span className="flex-1">{t(item.labelKey)}</span>
              {item.badge > 0 && (
                <span className="rounded-full bg-cfi-red px-1.5 text-[11px] font-bold text-white">
                  {item.badge}
                </span>
              )}
              <span className="text-cfi-muted">›</span>
            </NavLink>
          ))}
        </nav>

        <div className="border-t border-cfi-rule p-3">
          <button
            type="button"
            onClick={onSignOut}
            className="min-h-11 w-full rounded border border-cfi-rule text-sm font-semibold text-cfi-brown-dark hover:bg-cfi-sunk"
          >
            {t('auth.signOut')}
          </button>
        </div>
      </aside>
    </>
  );
}
