import i18n from './i18n';

const SITE_TIME_ZONE = 'Europe/London';

function pad(n) {
  return String(n).padStart(2, '0');
}

// The factory's "today", matching the API's carry-over boundary. Falls back to the phone's
// own date if the JS engine cannot format in another time zone.
export function siteToday() {
  try {
    const parts = new Intl.DateTimeFormat('en-GB', {
      timeZone: SITE_TIME_ZONE,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    }).formatToParts(new Date());
    const get = type => parts.find(p => p.type === type).value;
    return `${get('year')}-${get('month')}-${get('day')}`;
  } catch {
    const d = new Date();
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  }
}

// The factory-local calendar day an instant falls on, as yyyy-mm-dd.
export function siteDateOf(iso) {
  try {
    const parts = new Intl.DateTimeFormat('en-GB', {
      timeZone: SITE_TIME_ZONE,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    }).formatToParts(new Date(iso));
    const get = type => parts.find(p => p.type === type).value;
    return `${get('year')}-${get('month')}-${get('day')}`;
  } catch {
    return iso.slice(0, 10);
  }
}

export function addDays(isoDate, days) {
  const d = new Date(`${isoDate}T12:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
}

function safeFormat(date, options, fallback) {
  try {
    return new Intl.DateTimeFormat(i18n.language, options).format(date);
  } catch {
    return fallback;
  }
}

export function formatDay(isoDate, long = true) {
  const options = long
    ? {weekday: 'long', day: 'numeric', month: 'long', timeZone: 'UTC'}
    : {day: 'numeric', month: 'short', timeZone: 'UTC'};
  return safeFormat(new Date(`${isoDate}T12:00:00Z`), options, isoDate);
}

export function formatDateTime(iso) {
  const d = new Date(iso);
  return safeFormat(
    d,
    {day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit', timeZone: SITE_TIME_ZONE},
    d.toLocaleString(),
  );
}

export function errorMessage(t, error) {
  if (!error) {
    return '';
  }
  if (error.code) {
    return t(`errors.${error.code}`, {defaultValue: t('errors.generic')});
  }
  if (error.status === 403) {
    return t('errors.forbidden');
  }
  if (error.status === 404) {
    return t('errors.notFound');
  }
  return t('errors.generic');
}

export function fieldError(t, error, field) {
  const code = error?.fieldErrors?.[field]?.[0];
  return code ? t(`errors.${code}`, {defaultValue: t('errors.generic')}) : null;
}
