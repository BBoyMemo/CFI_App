/**
 * Minutes as the floor says them: 75 becomes "1h 15m".
 *
 * Times are stored in minutes because that is the unit every calculation wants, but nobody
 * on site describes a two hour stoppage as 120. Reading a number back in the same shape it
 * was typed in is also how a mistyped one gets noticed.
 */
export function formatDuration(totalMinutes) {
  if (totalMinutes == null) return '';

  const minutes = Math.max(0, Math.round(totalMinutes));
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;

  if (hours === 0) return `${rest}m`;
  if (rest === 0) return `${hours}h`;

  return `${hours}h ${String(rest).padStart(2, '0')}m`;
}
