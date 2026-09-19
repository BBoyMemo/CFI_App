/**
 * The vocabulary the rota screens share. Kept next to them rather than in the generic api
 * folder because none of it means anything outside a shift.
 */

/** Matches the server's ShiftSource enum, by position. */
export const ShiftSource = ['None', 'Roster', 'Cover', 'Off', 'Holiday', 'PublicHoliday'];

export const sourceName = (value) => ShiftSource[value] ?? 'None';

/**
 * Monday first. JavaScript's getDay() and the server's DayOfWeek both start the week on
 * Sunday; the factory does not, and a rota that starts on Sunday is a rota nobody reads
 * correctly.
 */
export const WEEK = [1, 2, 3, 4, 5, 6, 0];

/** The one letter shown in a weekday strip. */
export const weekdayKey = (day) =>
  `shift.weekday_${['sun', 'mon', 'tue', 'wed', 'thu', 'fri', 'sat'][day]}`;

/** Morning gets a sun, nights get a moon, everything else gets a clock. */
export function shiftIcon(startTime) {
  const hour = Number(startTime.slice(0, 2));
  if (hour >= 20 || hour < 5) return 'moon';
  if (hour < 12) return 'sun';
  return 'clock';
}

export const timeRange = (start, end) => `${start.slice(0, 5)}–${end.slice(0, 5)}`;
