import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import Button from '../../components/ui/Button';

/**
 * What opens when somebody is put on a shift. There is no silent drop: the manager chooses
 * the date the change takes effect, because a rota that quietly rewrote itself from today
 * would be rewriting a day people have already worked.
 *
 * Which days of the week they will be in is not asked here - that belongs to the shift, and
 * asking twice is asking for two answers that disagree.
 */
export default function PlacementPanel({ personName, shiftName, today, busy, onCancel, onSubmit }) {
  const { t } = useTranslation();

  const [mode, setMode] = useState('roster');
  const [effectiveFrom, setEffectiveFrom] = useState(today);
  const [fromDate, setFromDate] = useState(today);
  const [toDate, setToDate] = useState(today);
  const [note, setNote] = useState('');

  const canSubmit = mode === 'cover' ? fromDate && toDate && toDate >= fromDate : Boolean(effectiveFrom);

  const submit = () =>
    onSubmit(mode === 'cover'
      ? { mode, fromDate, toDate, note: note.trim() || null }
      : { mode, effectiveFrom });

  return (
    <div className="mt-3 rounded border border-cfi-yellow-dark bg-cfi-yellow/5 p-3">
      <p className="text-sm font-semibold text-cfi-brown-dark">
        {t('shift.placing', { person: personName, shift: shiftName })}
      </p>

      <div className="mt-2 flex gap-2">
        {['roster', 'cover'].map((option) => (
          <button
            key={option}
            type="button"
            onClick={() => setMode(option)}
            className={`min-h-9 flex-1 rounded border px-2 text-sm font-semibold transition-colors ${
              mode === option
                ? 'border-cfi-yellow-dark bg-cfi-yellow/30 text-cfi-brown-dark'
                : 'border-cfi-rule bg-white text-cfi-muted hover:border-cfi-yellow-dark'
            }`}
          >
            {t(option === 'roster' ? 'shift.modeStanding' : 'shift.modeCover')}
          </button>
        ))}
      </div>

      {mode === 'roster' ? (
        <label className="mt-3 block text-xs font-medium text-cfi-muted">
          {t('shift.effectiveFrom')}
          <input
            type="date"
            min={today}
            value={effectiveFrom}
            onChange={(event) => setEffectiveFrom(event.target.value)}
            className="mt-1 min-h-10 w-full rounded border border-cfi-rule bg-white px-2 text-sm"
          />
        </label>
      ) : (
        <>
          <div className="mt-3 flex gap-2">
            <label className="flex-1 text-xs font-medium text-cfi-muted">
              {t('shift.coverFrom')}
              <input
                type="date"
                value={fromDate}
                onChange={(event) => {
                  setFromDate(event.target.value);
                  if (toDate < event.target.value) setToDate(event.target.value);
                }}
                className="mt-1 min-h-10 w-full rounded border border-cfi-rule bg-white px-2 text-sm"
              />
            </label>

            <label className="flex-1 text-xs font-medium text-cfi-muted">
              {t('shift.coverTo')}
              <input
                type="date"
                min={fromDate}
                value={toDate}
                onChange={(event) => setToDate(event.target.value)}
                className="mt-1 min-h-10 w-full rounded border border-cfi-rule bg-white px-2 text-sm"
              />
            </label>
          </div>

          <input
            type="text"
            maxLength={200}
            value={note}
            placeholder={t('shift.coverNotePlaceholder')}
            onChange={(event) => setNote(event.target.value)}
            className="mt-2 min-h-10 w-full rounded border border-cfi-rule bg-white px-2 text-sm"
          />
        </>
      )}

      <div className="mt-3 flex gap-2">
        <Button disabled={busy || !canSubmit} onClick={submit} className="flex-1">
          {t('common.confirm')}
        </Button>
        <Button variant="secondary" disabled={busy} onClick={onCancel}>{t('common.cancel')}</Button>
      </div>
    </div>
  );
}
