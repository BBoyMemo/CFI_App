import { ClockType } from '../../api/enums';

/**
 * Worked minutes from a list of clock events.
 *
 * Lives on its own because two screens show it - the worker's own day and the manager's
 * week - and this number ends up on a payslip. Two copies of it would mean a correction
 * to one and a stale answer in the other.
 *
 * Events are paired In→Out in time order. An In with no Out yet contributes nothing
 * rather than counting to now: somebody still on shift has not worked those minutes.
 */
export function workedMinutes(events) {
  const chronological = [...events].sort(
    (a, b) => new Date(a.occurredAtUtc) - new Date(b.occurredAtUtc));

  let minutes = 0;
  let openedAt = null;

  for (const event of chronological) {
    if (ClockType[event.type] === 'In') {
      openedAt = new Date(event.occurredAtUtc);
    } else if (openedAt) {
      minutes += Math.round((new Date(event.occurredAtUtc) - openedAt) / 60000);
      openedAt = null;
    }
  }

  return minutes;
}

/** "7h 30m" - the way the site says it, not 7.5 or 450. */
export function formatHours(minutes) {
  return `${Math.floor(minutes / 60)}h ${minutes % 60}m`;
}

/** Monday of the week containing the given date, as an ISO day string. */
export function weekStart(date) {
  const copy = new Date(date);
  const weekday = (copy.getDay() + 6) % 7; // Monday = 0
  copy.setDate(copy.getDate() - weekday);
  return copy.toISOString().slice(0, 10);
}

export function addDays(isoDay, days) {
  const date = new Date(`${isoDay}T00:00:00`);
  date.setDate(date.getDate() + days);
  return date.toISOString().slice(0, 10);
}
