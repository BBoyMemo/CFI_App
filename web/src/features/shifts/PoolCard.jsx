import { useTranslation } from 'react-i18next';

import Badge from '../../components/ui/Badge';
import ConfirmButton from '../../components/ui/ConfirmButton';
import Icon from '../../components/ui/Icon';
import WeekdayStrip from './WeekdayStrip';
import { fullDate, shiftIcon, timeRange } from './shiftModel';

/**
 * A running shift, at a glance. No names - who is on it is managed on the shift itself, in
 * the shift column; this says what is running, from when, and how many.
 */
export default function PoolCard({ shift, today, busy, onRemove }) {
  const { t } = useTranslation();

  const notStarted = shift.startsOn > today;

  return (
    <div className="rounded-lg border border-cfi-rule bg-cfi-surface p-3">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <div className="flex items-center gap-1.5">
            <span className="text-cfi-brown-dark"><Icon name={shiftIcon(shift.startTime)} size={16} /></span>
            <span className="truncate text-sm font-semibold text-cfi-brown-dark">{shift.name}</span>
          </div>
          <p className="mt-0.5 font-mono text-xs text-cfi-muted">{timeRange(shift.startTime, shift.endTime)}</p>
        </div>

        <div className="flex items-center gap-1">
          <Badge tone={shift.peopleCount === 0 ? 'bg-cfi-sunk text-cfi-muted' : 'bg-cfi-green/15 text-cfi-green-dark'}>
            <span className="flex items-center gap-1"><Icon name="users" size={12} />{shift.peopleCount}</span>
          </Badge>

          <ConfirmButton
            onConfirm={() => onRemove(shift)}
            disabled={busy}
            question={t('shift.removeFromPoolQuestion')}
            confirmLabel={t('shift.removeFromPool')}
            variant="ghost"
            className="min-h-8 px-1"
          >
            ×
          </ConfirmButton>
        </div>
      </div>

      <div className="mt-1.5 flex flex-wrap items-center justify-between gap-2">
        <WeekdayStrip days={shift.weekdays} />
        <span className={`text-xs ${notStarted ? 'font-semibold text-cfi-yellow-dark' : 'text-cfi-muted'}`}>
          {t('shift.startsFrom', { date: fullDate(shift.startsOn) })}
        </span>
      </div>
    </div>
  );
}
