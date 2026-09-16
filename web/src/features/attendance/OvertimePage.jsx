import { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';

import AsyncSection from '../../components/ui/AsyncSection';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import ErrorBanner from '../../components/ui/ErrorBanner';
import { describeApiError } from '../../api/apiClient';
import { decideOvertime, declareOvertime, getMyOvertime, getTeamOvertime } from '../../api/endpoints';
import { decisionTone } from '../../api/enums';
import { Permissions } from '../../api/permissions';
import { useAuth } from '../../auth/useAuth';
import { useApiData } from '../../hooks/useApiData';

const controlClass = 'min-h-11 rounded border border-cfi-rule bg-white px-3 text-sm';

const today = () => new Date().toISOString().slice(0, 10);

export default function OvertimePage() {
  const { t } = useTranslation();
  const { hasPermission } = useAuth();

  const canDeclare = hasPermission(Permissions.OvertimeDeclare);
  const canApprove = hasPermission(Permissions.OvertimeApprove);

  const [date, setDate] = useState(today);
  const [hours, setHours] = useState('1');
  const [note, setNote] = useState('');
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);

  const mine = useApiData(() => (canDeclare ? getMyOvertime({ pageSize: 50 }) : Promise.resolve({ data: { items: [] } })), [canDeclare]);
  const team = useApiData(() => (canApprove ? getTeamOvertime({ pageSize: 50 }) : Promise.resolve({ data: { items: [] } })), [canApprove]);

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

  const declare = (event) => {
    event.preventDefault();
    runAction(
      async () => {
        // Hours on the form, minutes on the wire: nobody declares overtime in minutes,
        // but the record keeps them so a 90 minute callout is not rounded away.
        await declareOvertime({ date, minutes: Math.round(Number(hours) * 60), note: note || null });
        setNote('');
      },
      mine.refetch,
    );
  };

  const renderRow = (item, withDecision) => (
    <Card key={item.id}>
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <p className="font-medium text-cfi-ink">
            {item.date} · {Math.floor(item.minutes / 60)}h {item.minutes % 60}m
          </p>
          <p className="text-sm text-cfi-muted">{item.requestedByName}</p>
          {item.note && <p className="mt-1 text-sm text-cfi-muted">{item.note}</p>}
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
            onClick={() => runAction(() => decideOvertime(item.id, { approve: true, note: null }), team.refetch)}
          >
            {t('common.approve')}
          </Button>
          <Button
            variant="danger"
            disabled={busy}
            onClick={() => runAction(() => decideOvertime(item.id, { approve: false, note: null }), team.refetch)}
          >
            {t('common.reject')}
          </Button>
        </div>
      )}
    </Card>
  );

  return (
    <div className="mx-auto max-w-3xl">
      <h1 className="mb-4 text-xl font-bold text-cfi-brown-dark">{t('nav.overtime')}</h1>

      {canDeclare && (
        <Card>
          <form onSubmit={declare} className="flex flex-wrap items-end gap-3">
            <label className="flex flex-col gap-1 text-sm">
              {t('overtime.date')}
              <input required type="date" value={date} onChange={(e) => setDate(e.target.value)} className={controlClass} />
            </label>

            <label className="flex w-24 flex-col gap-1 text-sm">
              {t('overtime.hours')}
              <input
                required
                type="number"
                min="0.25"
                step="0.25"
                value={hours}
                onChange={(e) => setHours(e.target.value)}
                className={controlClass}
              />
            </label>

            <label className="flex flex-1 flex-col gap-1 text-sm">
              {t('common.note')} <span className="text-cfi-muted">({t('common.optional')})</span>
              <input value={note} onChange={(e) => setNote(e.target.value)} className={controlClass} />
            </label>

            <Button type="submit" disabled={busy}>{t('overtime.declare')}</Button>
          </form>
        </Card>
      )}

      <ErrorBanner error={error} />

      {canApprove && (
        <section className="mt-6">
          <h2 className="mb-3 text-lg font-semibold text-cfi-brown-dark">{t('overtime.teamTitle')}</h2>
          <AsyncSection {...team} isEmpty={teamItems.length === 0} emptyKey="overtime.teamEmpty">
            <div className="flex flex-col gap-3">{teamItems.map((item) => renderRow(item, true))}</div>
          </AsyncSection>
        </section>
      )}

      {canDeclare && (
        <section className="mt-6">
          <h2 className="mb-3 text-lg font-semibold text-cfi-brown-dark">{t('overtime.mineTitle')}</h2>
          <AsyncSection {...mine} isEmpty={myItems.length === 0} emptyKey="overtime.mineEmpty">
            <div className="flex flex-col gap-3">{myItems.map((item) => renderRow(item, false))}</div>
          </AsyncSection>
        </section>
      )}
    </div>
  );
}
