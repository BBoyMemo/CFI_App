import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import Button from '../../components/ui/Button';
import Icon from '../../components/ui/Icon';

/**
 * What opens when somebody is put on a shift and it matters: the shift is in the pool, or
 * they are already on another shift that works the same days.
 *
 * One form, not two modes. A start date, and an end date only if they are not staying: leave
 * it empty and the move holds until the manager changes it; fill it in and it is temporary,
 * and afterwards they are back on their own shift without anybody putting them there.
 *
 * A clash is spelled out before anything happens - who, and which shift - because the manager
 * is the one who knows whether moving them is right.
 */
export default function PlacementPanel({
  personName,
  shiftName,
  today,
  clashes,
  needsDates,
  allowEnd,
  busy,
  onCancel,
  onSubmit,
}) {
  const { t } = useTranslation();

  const [startDate, setStartDate] = useState(today);
  const [endDate, setEndDate] = useState('');
  const [note, setNote] = useState('');

  const clashNames = clashes.map((x) => x.name).join(', ');
  const temporary = Boolean(endDate);
  const canSubmit = !needsDates || (startDate && (!endDate || endDate >= startDate));

  return (
    <div
      className="mt-3 rounded border border-cfi-yellow-dark bg-cfi-yellow/5 p-3"
      // The panel sits inside a tappable shift card; clicks in here are for the panel.
      onClick={(event) => event.stopPropagation()}
      role="presentation"
    >
      <p className="text-sm font-semibold text-cfi-brown-dark">
        {t('shift.placing', { person: personName, shift: shiftName })}
      </p>

      {clashes.length > 0 && (
        <div className="mt-2 flex gap-2 rounded bg-cfi-yellow/20 px-2 py-2 text-sm text-cfi-brown-dark">
          <span className="mt-0.5 shrink-0 text-cfi-yellow-dark"><Icon name="bell" size={16} /></span>
          <div>
            <p className="font-semibold">{t('shift.clashWarning', { person: personName, shifts: clashNames })}</p>
            <p className="mt-0.5">
              {temporary
                ? t('shift.clashBack', { shifts: clashNames })
                : t('shift.clashMove', { shifts: clashNames })}
            </p>
          </div>
        </div>
      )}

      {needsDates && (
        <div className="mt-3 flex flex-wrap gap-2">
          <label className="min-w-36 flex-1 text-xs font-medium text-cfi-muted">
            {t('shift.startDate')}
            <input
              type="date"
              min={today}
              value={startDate}
              onChange={(event) => {
                setStartDate(event.target.value);
                if (endDate && endDate < event.target.value) setEndDate(event.target.value);
              }}
              className="mt-1 min-h-10 w-full rounded border border-cfi-rule bg-white px-2 text-sm"
            />
          </label>

          {allowEnd && (
            <label className="min-w-36 flex-1 text-xs font-medium text-cfi-muted">
              {t('shift.endDate')}
              <input
                type="date"
                min={startDate}
                value={endDate}
                onChange={(event) => setEndDate(event.target.value)}
                className="mt-1 min-h-10 w-full rounded border border-cfi-rule bg-white px-2 text-sm"
              />
            </label>
          )}
        </div>
      )}

      {needsDates && allowEnd && !temporary && (
        <p className="mt-1 text-xs text-cfi-muted">{t('shift.endHint')}</p>
      )}

      {temporary && (
        <input
          type="text"
          maxLength={200}
          value={note}
          placeholder={t('shift.coverNotePlaceholder')}
          onChange={(event) => setNote(event.target.value)}
          className="mt-2 min-h-10 w-full rounded border border-cfi-rule bg-white px-2 text-sm"
        />
      )}

      <div className="mt-3 flex gap-2">
        <Button
          disabled={busy || !canSubmit}
          className="flex-1"
          onClick={() => onSubmit({
            startDate: needsDates ? startDate : null,
            endDate: temporary ? endDate : null,
            note: note.trim() || null,
          })}
        >
          {clashes.length > 0 && !temporary ? t('shift.move') : t('common.confirm')}
        </Button>
        <Button variant="secondary" disabled={busy} onClick={onCancel}>{t('common.cancel')}</Button>
      </div>
    </div>
  );
}
