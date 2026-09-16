import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { apiClient, describeApiError } from '../../api/apiClient';
import { API_BASE_URL } from '../../config/env';

const londonTime = (isoValue) =>
  new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'medium',
    timeZone: 'Europe/London',
  }).format(new Date(isoValue));

function StatusPill({ state, labels }) {
  const styles = {
    healthy: 'bg-cfi-green/15 text-cfi-green-dark',
    unhealthy: 'bg-cfi-red/15 text-cfi-red',
    checking: 'bg-cfi-sunk text-cfi-muted',
  };

  return (
    <span className={`inline-flex items-center rounded px-2.5 py-1 text-sm font-semibold ${styles[state]}`}>
      {labels[state]}
    </span>
  );
}

function DetailRow({ label, children }) {
  return (
    <div className="flex flex-wrap items-baseline justify-between gap-2 border-b border-cfi-rule/60 py-3 last:border-b-0">
      <dt className="text-sm text-cfi-muted">{label}</dt>
      <dd className="font-mono text-sm text-cfi-ink">{children}</dd>
    </div>
  );
}

/**
 * Phase 0 deliverable: proves the whole chain - browser, API, database - is wired up,
 * and doubles as the page operations can open when something looks wrong.
 */
export default function SystemStatusPage() {
  const { t } = useTranslation();
  const [state, setState] = useState({ phase: 'loading', info: null, database: null, error: null });

  const labels = {
    healthy: t('status.healthy'),
    unhealthy: t('status.unhealthy'),
    checking: t('status.checking'),
  };

  // Bumping this re-runs the check. The request lives inside the effect so a result
  // arriving after the component is gone is discarded instead of updating dead state.
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let cancelled = false;

    const run = async () => {
      try {
        const [infoResponse, healthResponse] = await Promise.all([
          apiClient.get('/api/v1/system/info'),
          // A degraded database answers 503; that is information, not a transport failure.
          apiClient.get('/health/ready', {
            validateStatus: (status) => status === 200 || status === 503,
          }),
        ]);

        if (cancelled) return;

        const postgres = healthResponse.data?.checks?.find((entry) => entry.name === 'postgres');

        setState({
          phase: 'ready',
          info: infoResponse.data,
          database: postgres?.status === 'Healthy' ? 'healthy' : 'unhealthy',
          error: null,
        });
      } catch (error) {
        if (cancelled) return;
        setState({ phase: 'error', info: null, database: null, error: describeApiError(error) });
      }
    };

    run();

    return () => {
      cancelled = true;
    };
  }, [attempt]);

  // Re-checking is a user action, so the loading state is set from the event handler.
  const recheck = () => {
    setState((current) => ({ ...current, phase: 'loading' }));
    setAttempt((current) => current + 1);
  };

  return (
    <section className="rounded-lg border border-cfi-rule bg-cfi-surface p-5 sm:p-6">
      <header className="mb-4">
        <h2 className="text-xl font-semibold text-cfi-brown-dark">{t('status.title')}</h2>
        <p className="mt-1 text-sm text-cfi-ink-soft">{t('status.subtitle')}</p>
      </header>

      {state.phase === 'error' ? (
        <div className="rounded border border-cfi-red/40 bg-cfi-red/10 p-4">
          <p className="font-semibold text-cfi-red">
            {state.error?.kind === 'network' ? t('status.errorTitle') : state.error?.title}
          </p>
          <p className="mt-1 text-sm text-cfi-ink-soft">
            {t('status.errorHint', { url: API_BASE_URL })}
          </p>
          <button
            type="button"
            onClick={recheck}
            className="mt-4 min-h-11 rounded bg-cfi-yellow px-4 font-semibold text-cfi-brown-dark"
          >
            {t('status.recheck')}
          </button>
        </div>
      ) : (
        <>
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="flex items-center justify-between rounded border border-cfi-rule bg-cfi-cream/60 px-4 py-3">
              <span className="font-medium">{t('status.api')}</span>
              <StatusPill state={state.phase === 'loading' ? 'checking' : 'healthy'} labels={labels} />
            </div>
            <div className="flex items-center justify-between rounded border border-cfi-rule bg-cfi-cream/60 px-4 py-3">
              <span className="font-medium">{t('status.database')}</span>
              <StatusPill
                state={state.phase === 'loading' ? 'checking' : state.database}
                labels={labels}
              />
            </div>
          </div>

          <dl className="mt-5">
            <DetailRow label={t('status.version')}>{state.info?.version ?? '—'}</DetailRow>
            <DetailRow label={t('status.environment')}>{state.info?.environment ?? '—'}</DetailRow>
            <DetailRow label={t('status.serverTime')}>
              {state.info ? `${state.info.serverTimeUtc} · ${londonTime(state.info.serverTimeUtc)}` : '—'}
            </DetailRow>
          </dl>

          <button
            type="button"
            onClick={recheck}
            disabled={state.phase === 'loading'}
            className="mt-5 min-h-11 rounded border border-cfi-rule bg-cfi-sunk px-4 font-semibold text-cfi-brown-dark disabled:opacity-60"
          >
            {state.phase === 'loading' ? t('status.checking') : t('status.recheck')}
          </button>
        </>
      )}
    </section>
  );
}
