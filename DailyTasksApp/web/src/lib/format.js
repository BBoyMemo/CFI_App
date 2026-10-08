import i18n from '../i18n';

const SITE_TIME_ZONE = 'Europe/London';

// "Today" is the factory's day, matching the API's carry-over boundary.
export function siteToday() {
  return new Intl.DateTimeFormat('en-CA', { timeZone: SITE_TIME_ZONE }).format(new Date());
}

// The factory-local calendar day an instant falls on, as yyyy-mm-dd.
export function siteDateOf(iso) {
  return new Intl.DateTimeFormat('en-CA', { timeZone: SITE_TIME_ZONE }).format(new Date(iso));
}

export function addDays(isoDate, days) {
  const d = new Date(`${isoDate}T12:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
}

// Date-only values (yyyy-mm-dd) carry no time zone, so they are formatted as UTC noon.
export function formatDay(isoDate, options = { weekday: 'short', day: 'numeric', month: 'short' }) {
  return new Intl.DateTimeFormat(i18n.language, { ...options, timeZone: 'UTC' }).format(
    new Date(`${isoDate}T12:00:00Z`),
  );
}

export function formatDateTime(iso) {
  return new Intl.DateTimeFormat(i18n.language, {
    day: 'numeric',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
    timeZone: SITE_TIME_ZONE,
  }).format(new Date(iso));
}

// Turns an ApiError into translated text. Field errors are handled by the forms themselves.
export function errorMessage(t, error) {
  if (!error) return '';
  if (error.code) return t(`errors.${error.code}`, { defaultValue: t('errors.generic') });
  if (error.status === 403) return t('errors.forbidden');
  if (error.status === 404) return t('errors.notFound');
  return t('errors.generic');
}

export function fieldError(t, error, field) {
  const code = error?.fieldErrors?.[field]?.[0];
  return code ? t(`errors.${code}`, { defaultValue: t('errors.generic') }) : null;
}
