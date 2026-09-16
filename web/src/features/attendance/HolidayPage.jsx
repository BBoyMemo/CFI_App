import { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';

import AsyncSection from '../../components/ui/AsyncSection';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import ErrorBanner from '../../components/ui/ErrorBanner';
import { describeApiError } from '../../api/apiClient';
import { decideHoliday, getMyHoliday, getTeamHoliday, requestHoliday } from '../../api/endpoints';
import { decisionTone } from '../../api/enums';
import { Permissions } from '../../api/permissions';
import { useAuth } from '../../auth/useAuth';
import { useApiData } from '../../hooks/useApiData';

const controlClass = 'min-h-11 rounded border border-cfi-rule bg-white px-3 text-sm';

const empty = Promise.resolve({ data: { items: [] } });

const today = () => new Date().toISOString().slice(0, 10);

export default function HolidayPage() {
  const { t } = useTranslation();
  const { hasPermission } = useAuth();

  const canRequest = hasPermission(Permissions.HolidayRequest);
  const canApprove = hasPermission(Permissions.HolidayApprove);

  const [startDate, setStartDate] = useState(today);
  const [endDate, setEndDate] = useState(today);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);

  const mine = useApiData(() => (canRequest ? getMyHoliday({ pageSize: 50 }) : empty), [canRequest]);
  const team = useApiData(() => (canApprove ? getTeamHoliday({ pageSize: 50 }) : empty), [canApprove]);

  const myItems = mine.data?.items ?? [];
  const teamItems = team.data?.items ?? [];

  const runAction = useCallback(async (action, refresh) => {
    setError(null);
    setBusy(true);
    try {
      await action();
      refresh();
    } catch (actionError) {
      setError(describeApiError(actionError));
    } finally {
      setBusy(false);
    }
  }, []);

  const submit = (event) => {
    event.preventDefault();
    runAction(() => requestHoliday({ startDate, endDate }), mine.refetch);
  };

  const renderRow = (item, withDecision) => (
    <Card key={item.id}>
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <p className="font-medium text-cfi-ink">{item.startDate} → {item.endDate}</p>
          {/* Working days, not calendar days: weekends and UK bank holidays are already
              taken out by the server, which is the number that reaches a payslip. */}
          <p className="text-sm text-cfi-muted">
            {t('holiday.workingDays', { count: item.workingDays })} · {item.requestedByName}
          </p>
          {item.decisionNote && (
            <p className="mt-1 text-sm text-cfi-muted">{t('common.note')}: {item.decisionNote}</p>
          )}
        </div>

        <Badge tone={decisionTone(item.status)}>{item.status}</Badge>
      </div>

      {withDecision && item.status === 'Pending' && (
        <div className="mt-3 flex gap-2">
          <Button
            disabled={busy}
            onClick={() => runAction(() => decideHoliday(item.id, { approve: true, note: null }), team.refetch)}
          >
            {t('common.approve')}
          </Button>
          <Button
            variant="danger"
            disabled={busy}
            onClick={() => runAction(() => decideHoliday(item.id, { approve: false, note: null }), team.refetch)}
          >
            {t('common.reject')}
          </Button>
        </div>
      )}
    </Card>
  );

  return (
    <div className="mx-auto max-w-3xl">
      <h1 className="mb-4 text-xl font-bold text-cfi-brown-dark">{t('nav.holiday')}</h1>

      {canRequest && (
        <Card>
          <form onSubmit={submit} className="flex flex-wrap items-end gap-3">
            <label className="flex flex-col gap-1 text-sm">
              {t('holiday.from')}
              <input
                required
                type="date"
                value={startDate}
                onChange={(event) => {
                  setStartDate(event.target.value);
                  if (event.target.value > endDate) setEndDate(event.target.value);
                }}
                className={controlClass}
              />
            </label>

            <label className="flex flex-col gap-1 text-sm">
              {t('holiday.to')}
              <input
                required
                type="date"
                min={startDate}
                value={endDate}
                onChange={(event) => setEndDate(event.target.value)}
                className={controlClass}
              />
            </label>

            <Button type="submit" disabled={busy}>{t('holiday.request')}</Button>
          </form>
        </Card>
      )}

      <ErrorBanner error={error} />

      {canApprove && (
        <section className="mt-6">
          <h2 className="mb-3 text-lg font-semibold text-cfi-brown-dark">{t('holiday.teamTitle')}</h2>
          <AsyncSection {...team} isEmpty={teamItems.length === 0} emptyKey="holiday.teamEmpty">
            <div className="flex flex-col gap-3">{teamItems.map((item) => renderRow(item, true))}</div>
          </AsyncSection>
        </section>
      )}

      {canRequest && (
        <section className="mt-6">
          <h2 className="mb-3 text-lg font-semibold text-cfi-brown-dark">{t('holiday.mineTitle')}</h2>
          <AsyncSection {...mine} isEmpty={myItems.length === 0} emptyKey="holiday.mineEmpty">
            <div className="flex flex-col gap-3">{myItems.map((item) => renderRow(item, false))}</div>
          </AsyncSection>
        </section>
      )}
    </div>
  );
}
