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

/**
 * A calendar day as YYYY-MM-DD, read off the local clock.
 *
 * Deliberately not toISOString(): that converts to UTC first, so through British Summer
 * Time every one of these helpers used to answer with the previous day for any time before
 * 01:00 - a rota is written in local days, and a shift landing on the wrong date is not a
 * rounding error to anybody expected to turn up for it.
 */
export function toIsoDay(date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/** Today, as an ISO day string. */
export function todayIso() {
  return toIsoDay(new Date());
}

/** Monday of the week containing the given date, as an ISO day string. */
export function weekStart(date) {
  const copy = new Date(date);
  const weekday = (copy.getDay() + 6) % 7; // Monday = 0
  copy.setDate(copy.getDate() - weekday);
  return toIsoDay(copy);
}

export function addDays(isoDay, days) {
  const date = new Date(`${isoDay}T00:00:00`);
  date.setDate(date.getDate() + days);
  return toIsoDay(date);
}
