import { useTranslation } from 'react-i18next';

import { WEEK, weekdayKey } from './shiftModel';

/**
 * The seven boxes under a shift's name. Read-only by default; give it onToggle and it
 * becomes the day picker on the new-shift form, so both read the identically same way.
 */
export default function WeekdayStrip({ days = [], onToggle, size = 'sm' }) {
  const { t } = useTranslation();

  const box = size === 'lg' ? 'h-9 w-9 text-xs' : 'w-4 text-[10px] leading-4';

  return (
    <div className="flex gap-0.5" aria-label={t('shift.weekdays')}>
      {WEEK.map((day) => {
        const on = days.includes(day);
        const tone = on
          ? 'bg-cfi-yellow/40 font-semibold text-cfi-brown-dark'
          : 'bg-cfi-sunk text-cfi-muted/60';

        return onToggle ? (
          <button
            key={day}
            type="button"
            aria-pressed={on}
            onClick={() => onToggle(day)}
            className={`rounded border text-center transition-colors ${box} ${
              on ? 'border-cfi-yellow-dark bg-cfi-yellow/40 font-semibold text-cfi-brown-dark'
                : 'border-cfi-rule bg-white text-cfi-muted'
            }`}
          >
            {t(weekdayKey(day))}
          </button>
        ) : (
          <span key={day} className={`rounded text-center ${box} ${tone}`}>
            {t(weekdayKey(day))}
          </span>
        );
      })}
    </div>
  );
}
