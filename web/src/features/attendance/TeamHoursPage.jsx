import { useCallback, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

import AsyncSection from '../../components/ui/AsyncSection';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import ErrorBanner from '../../components/ui/ErrorBanner';
import { describeApiError } from '../../api/apiClient';
import { correctClockEvent, getTeamHours } from '../../api/endpoints';
import { clockTypeKey } from '../../api/enums';
import { Permissions } from '../../api/permissions';
import { useAuth } from '../../auth/useAuth';
import { useApiData } from '../../hooks/useApiData';
import { addDays, formatHours, weekStart, workedMinutes } from './hours';

const controlClass = 'min-h-11 rounded border border-cfi-rule bg-white px-3 text-sm';

/** A correction never edits the original row - it records what the time should have been. */
function CorrectionForm({ clockEventId, onDone }) {
  const { t } = useTranslation();
  const [reason, setReason] = useState('');
  const [newTime, setNewTime] = useState('');
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);

  const submit = async (event) => {
    event.preventDefault();
    setError(null);
    setBusy(true);

    try {
      await correctClockEvent(clockEventId, {
        reason,
        newOccurredAtUtc: new Date(newTime).toISOString(),
      });
      onDone();
    } catch (submitError) {
      setError(describeApiError(submitError));
    } finally {
      setBusy(false);
    }
  };

  return (
    <form onSubmit={submit} className="mt-2 flex flex-wrap items-end gap-2 border-t border-cfi-rule pt-2">
      <label className="flex flex-col gap-1 text-xs">
        {t('attendance.correctedTime')}
        <input
          required
          type="datetime-local"
          value={newTime}
          onChange={(e) => setNewTime(e.target.value)}
          className={controlClass}
        />
      </label>

      <label className="flex flex-1 flex-col gap-1 text-xs">
        {t('attendance.correctionReason')}
        <input required value={reason} onChange={(e) => setReason(e.target.value)} className={controlClass} />
      </label>

      <Button type="submit" disabled={busy || !reason.trim() || !newTime}>
        {t('common.confirm')}
      </Button>

      <ErrorBanner error={error} />
    </form>
  );
}

export default function TeamHoursPage() {
  const { t } = useTranslation();
  const { hasPermission } = useAuth();

  const [from, setFrom] = useState(() => weekStart(new Date()));
  const [correcting, setCorrecting] = useState(null);

  const to = useMemo(() => addDays(from, 7), [from]);

  const hours = useApiData(
    () => getTeamHours({ from: `${from}T00:00:00Z`, to: `${to}T00:00:00Z` }),
    [from, to],
  );

  const members = hours.data ?? [];

  const shiftWeek = useCallback((days) => setFrom((current) => addDays(current, days)), []);

  return (
    <div className="mx-auto max-w-4xl">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-bold text-cfi-brown-dark">{t('nav.teamHours')}</h1>

        <div className="flex items-center gap-2">
          <Button variant="secondary" onClick={() => shiftWeek(-7)}>←</Button>
          <span className="text-sm text-cfi-muted">{from} → {addDays(to, -1)}</span>
          <Button variant="secondary" onClick={() => shiftWeek(7)}>→</Button>
        </div>
      </div>

      <AsyncSection {...hours} isEmpty={members.length === 0} emptyKey="attendance.noTeamHours">
        <div className="flex flex-col gap-3">
          {members.map((member) => (
            <Card key={member.userId}>
              <div className="flex flex-wrap items-center justify-between gap-2">
                <p className="font-medium text-cfi-ink">{member.fullName}</p>
                <Badge tone="bg-cfi-green/15 text-cfi-green-dark">
                  {formatHours(workedMinutes(member.events))}
                </Badge>
              </div>

              <div className="mt-2 flex flex-col gap-1">
                {member.events.map((event) => (
                  <div key={event.id} className="text-sm">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="text-cfi-ink">{t(clockTypeKey(event.type))}</span>
                      <span className="text-cfi-muted">
                        {new Date(event.occurredAtUtc).toLocaleString()}
                      </span>
                      {event.isSuspect && (
                        <Badge tone="bg-cfi-yellow/25 text-cfi-yellow-dark">
                          {event.suspectReason ?? t('attendance.suspect')}
                        </Badge>
                      )}
                      {hasPermission(Permissions.AttendanceCorrect) && (
                        <button
                          type="button"
                          onClick={() => setCorrecting(correcting === event.id ? null : event.id)}
                          className="text-xs font-semibold text-cfi-brown-dark underline"
                        >
                          {t('attendance.correct')}
                        </button>
                      )}
                    </div>

                    {correcting === event.id && (
                      <CorrectionForm
                        clockEventId={event.id}
                        onDone={() => {
                          setCorrecting(null);
                          hours.refetch();
                        }}
                      />
                    )}
                  </div>
                ))}
              </div>
            </Card>
          ))}
        </div>
      </AsyncSection>
    </div>
  );
}
