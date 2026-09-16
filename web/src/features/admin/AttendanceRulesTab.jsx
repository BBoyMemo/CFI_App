import { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';

import AsyncSection from '../../components/ui/AsyncSection';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import ErrorBanner from '../../components/ui/ErrorBanner';
import { describeApiError } from '../../api/apiClient';
import {
  activateGeofence, createGeofence, createPublicHoliday, deactivateGeofence,
  deletePublicHoliday, getGeofences, getPublicHolidays,
} from '../../api/endpoints';
import { useApiData } from '../../hooks/useApiData';

const controlClass = 'min-h-11 rounded border border-cfi-rule bg-white px-3 text-sm';

/**
 * The two settings that quietly decide whether hours and leave come out right:
 * the site boundary automatic clocking measures against, and the bank holidays that
 * must not be counted as leave. Both are empty until somebody fills them in, and the
 * rest of the app is written to say so rather than guess.
 */
export default function AttendanceRulesTab() {
  const { t } = useTranslation();

  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  const [fence, setFence] = useState({});
  const [holiday, setHoliday] = useState({});
  const [year, setYear] = useState(new Date().getFullYear());

  const geofences = useApiData(() => getGeofences());
  const holidays = useApiData(() => getPublicHolidays({ year }), [year]);

  const runAction = useCallback(async (action, source, reset) => {
    setError(null);
    setBusy(true);
    try {
      await action();
      source.refetch();
      if (reset) reset();
    } catch (actionError) {
      setError(describeApiError(actionError));
    } finally {
      setBusy(false);
    }
  }, []);

  const fenceField = (key) => (event) => setFence((c) => ({ ...c, [key]: event.target.value }));
  const holidayField = (key) => (event) => setHoliday((c) => ({ ...c, [key]: event.target.value }));

  return (
    <div className="flex flex-col gap-8">
      <ErrorBanner error={error} />

      <section>
        <h2 className="mb-1 text-lg font-semibold text-cfi-brown-dark">{t('admin.geofenceTitle')}</h2>
        <p className="mb-3 text-sm text-cfi-muted">{t('admin.geofenceHint')}</p>

        <Card>
          <form
            className="flex flex-wrap items-end gap-3"
            onSubmit={(event) => {
              event.preventDefault();
              runAction(() => createGeofence({
                name: fence.name,
                latitude: Number(fence.latitude),
                longitude: Number(fence.longitude),
                radiusMeters: Number(fence.radiusMeters || 300),
                reentryToleranceMinutes: Number(fence.reentryToleranceMinutes || 10),
                requiredAccuracyMeters: Number(fence.requiredAccuracyMeters || 100),
                maxClockDriftMinutes: Number(fence.maxClockDriftMinutes || 15),
              }), geofences, () => setFence({}));
            }}
          >
            <label className="flex flex-1 flex-col gap-1 text-sm">
              {t('admin.name')}
              <input required value={fence.name ?? ''} onChange={fenceField('name')} className={controlClass} />
            </label>
            <label className="flex w-36 flex-col gap-1 text-sm">
              {t('admin.latitude')}
              <input required type="number" step="any" value={fence.latitude ?? ''} onChange={fenceField('latitude')} className={controlClass} />
            </label>
            <label className="flex w-36 flex-col gap-1 text-sm">
              {t('admin.longitude')}
              <input required type="number" step="any" value={fence.longitude ?? ''} onChange={fenceField('longitude')} className={controlClass} />
            </label>
            <label className="flex w-28 flex-col gap-1 text-sm">
              {t('admin.radius')}
              <input type="number" min="1" placeholder="300" value={fence.radiusMeters ?? ''} onChange={fenceField('radiusMeters')} className={controlClass} />
            </label>
            <label className="flex w-28 flex-col gap-1 text-sm">
              {t('admin.accuracy')}
              <input type="number" min="1" placeholder="100" value={fence.requiredAccuracyMeters ?? ''} onChange={fenceField('requiredAccuracyMeters')} className={controlClass} />
            </label>
            <Button type="submit" disabled={busy}>{t('admin.add')}</Button>
          </form>
        </Card>

        <div className="mt-3">
          <AsyncSection {...geofences} isEmpty={(geofences.data ?? []).length === 0} emptyKey="admin.noGeofence">
            <div className="flex flex-col gap-2">
              {(geofences.data ?? []).map((row) => (
                <Card key={row.id} className="flex flex-wrap items-center justify-between gap-2 py-3">
                  <div className="text-sm">
                    <span className="font-medium text-cfi-ink">{row.name}</span>
                    <span className="ml-2 font-mono text-xs text-cfi-muted">
                      {row.latitude.toFixed(5)}, {row.longitude.toFixed(5)} · {row.radiusMeters}m
                    </span>
                  </div>

                  <div className="flex items-center gap-2">
                    <Badge tone={row.isActive ? 'bg-cfi-green/15 text-cfi-green-dark' : 'bg-cfi-sunk text-cfi-muted'}>
                      {row.isActive ? t('admin.active') : t('admin.inactive')}
                    </Badge>
                    <Button
                      variant="secondary"
                      disabled={busy}
                      onClick={() => runAction(
                        () => (row.isActive ? deactivateGeofence(row.id) : activateGeofence(row.id)),
                        geofences,
                      )}
                    >
                      {row.isActive ? t('admin.deactivate') : t('admin.activate')}
                    </Button>
                  </div>
                </Card>
              ))}
            </div>
          </AsyncSection>
        </div>
      </section>

      <section>
        <h2 className="mb-1 text-lg font-semibold text-cfi-brown-dark">{t('admin.holidaysTitle')}</h2>
        <p className="mb-3 text-sm text-cfi-muted">{t('admin.holidaysHint')}</p>

        <Card>
          <form
            className="flex flex-wrap items-end gap-3"
            onSubmit={(event) => {
              event.preventDefault();
              runAction(() => createPublicHoliday({
                date: holiday.date,
                name: holiday.name,
                region: holiday.region || null,
              }), holidays, () => setHoliday({}));
            }}
          >
            <label className="flex w-44 flex-col gap-1 text-sm">
              {t('overtime.date')}
              <input required type="date" value={holiday.date ?? ''} onChange={holidayField('date')} className={controlClass} />
            </label>
            <label className="flex flex-1 flex-col gap-1 text-sm">
              {t('admin.name')}
              <input required value={holiday.name ?? ''} onChange={holidayField('name')} className={controlClass} />
            </label>
            <Button type="submit" disabled={busy}>{t('admin.add')}</Button>
          </form>
        </Card>

        <div className="mt-3 flex items-center gap-2">
          <Button variant="secondary" onClick={() => setYear(year - 1)}>←</Button>
          <span className="text-sm font-semibold text-cfi-brown-dark">{year}</span>
          <Button variant="secondary" onClick={() => setYear(year + 1)}>→</Button>
        </div>

        <div className="mt-3">
          <AsyncSection {...holidays} isEmpty={(holidays.data ?? []).length === 0} emptyKey="admin.noHolidays">
            <div className="flex flex-col gap-2">
              {(holidays.data ?? []).map((row) => (
                <Card key={row.id} className="flex flex-wrap items-center justify-between gap-2 py-3">
                  <span className="text-sm">
                    <span className="font-mono text-cfi-muted">{row.date}</span>
                    <span className="ml-3 font-medium text-cfi-ink">{row.name}</span>
                  </span>
                  <Button
                    variant="secondary"
                    disabled={busy}
                    onClick={() => runAction(() => deletePublicHoliday(row.id), holidays)}
                  >
                    {t('common.delete')}
                  </Button>
                </Card>
              ))}
            </div>
          </AsyncSection>
        </div>
      </section>
    </div>
  );
}
