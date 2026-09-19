import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import Button from '../../components/ui/Button';
import WeekdayStrip from './WeekdayStrip';

const WORKING_WEEK = [1, 2, 3, 4, 5];

/**
 * Drawing up a shift: what it is called, when it runs, which days, and the day it starts.
 *
 * The start date is not decoration - a night shift that begins on a Sunday evening runs on a
 * different footing to one that begins on a Monday, and the site plans around that.
 */
export default function NewShiftForm({ today, busy, onCancel, onSubmit }) {
  const { t } = useTranslation();

  const [name, setName] = useState('');
  const [startTime, setStartTime] = useState('06:00');
  const [endTime, setEndTime] = useState('14:00');
  const [weekdays, setWeekdays] = useState(WORKING_WEEK);
  const [startsOn, setStartsOn] = useState(today);

  const toggleDay = (day) =>
    setWeekdays((current) =>
      current.includes(day) ? current.filter((x) => x !== day) : [...current, day]);

  const canSubmit = name.trim() && weekdays.length > 0 && startsOn && startTime !== endTime;

  return (
    <div className="rounded border border-cfi-yellow-dark bg-cfi-yellow/5 p-3">
      <input
        type="text"
        maxLength={60}
        autoFocus
        value={name}
        placeholder={t('shift.namePlaceholder')}
        onChange={(event) => setName(event.target.value)}
        className="min-h-10 w-full rounded border border-cfi-rule bg-white px-2 text-sm"
      />

      <div className="mt-2 flex gap-2">
        <label className="flex-1 text-xs font-medium text-cfi-muted">
          {t('shift.startsAt')}
          <input
            type="time"
            value={startTime}
            onChange={(event) => setStartTime(event.target.value)}
            className="mt-1 min-h-10 w-full rounded border border-cfi-rule bg-white px-2 text-sm"
          />
        </label>

        <label className="flex-1 text-xs font-medium text-cfi-muted">
          {t('shift.endsAt')}
          <input
            type="time"
            value={endTime}
            onChange={(event) => setEndTime(event.target.value)}
            className="mt-1 min-h-10 w-full rounded border border-cfi-rule bg-white px-2 text-sm"
          />
        </label>
      </div>

      <p className="mt-2 text-xs font-medium text-cfi-muted">{t('shift.weekdays')}</p>
      <div className="mt-1">
        <WeekdayStrip days={weekdays} onToggle={toggleDay} size="lg" />
      </div>

      <label className="mt-2 block text-xs font-medium text-cfi-muted">
        {t('shift.startsOn')}
        <input
          type="date"
          value={startsOn}
          onChange={(event) => setStartsOn(event.target.value)}
          className="mt-1 min-h-10 w-full rounded border border-cfi-rule bg-white px-2 text-sm"
        />
      </label>

      <div className="mt-3 flex gap-2">
        <Button
          disabled={busy || !canSubmit}
          className="flex-1"
          onClick={() => onSubmit({
            name: name.trim(),
            startTime: `${startTime}:00`,
            endTime: `${endTime}:00`,
            weekdays,
            startsOn,
            displayOrder: 5,
          })}
        >
          {t('common.save')}
        </Button>
        <Button variant="secondary" disabled={busy} onClick={onCancel}>{t('common.cancel')}</Button>
      </div>
    </div>
  );
}
