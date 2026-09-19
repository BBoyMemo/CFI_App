import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import AsyncSection from '../../components/ui/AsyncSection';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import Icon from '../../components/ui/Icon';
import { getMyShifts } from '../../api/endpoints';
import { useApiData } from '../../hooks/useApiData';
import { addDays, todayIso, weekStart } from '../attendance/hours';
import { shiftIcon, sourceName, timeRange } from './shiftModel';

/**
 * One week of your own shifts, and nothing else.
 *
 * The shift a person is on is the same most weeks, so what actually matters here is the day
 * that is not: cover, leave, a bank holiday. Each of those says so on the row rather than
 * just showing a different time and leaving them to work out whether the rota has changed
 * for good.
 */
export default function MyShiftsPage() {
  const { t } = useTranslation();
  const today = todayIso();

  const [from, setFrom] = useState(() => weekStart(new Date()));
  const to = addDays(from, 6);

  const week = useApiData(() => getMyShifts({ from, to }), [from, to]);
  const days = week.data ?? [];

  return (
    <div className="mx-auto max-w-2xl">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-bold text-cfi-brown-dark">{t('nav.myShifts')}</h1>

        <div className="flex items-center gap-2">
          <Button variant="secondary" aria-label={t('shift.previousWeek')} onClick={() => setFrom(addDays(from, -7))}>
            ‹
          </Button>
          <Button variant="secondary" onClick={() => setFrom(weekStart(new Date()))}>
            {t('shift.thisWeek')}
          </Button>
          <Button variant="secondary" aria-label={t('shift.nextWeek')} onClick={() => setFrom(addDays(from, 7))}>
            ›
          </Button>
        </div>
      </div>

      <AsyncSection {...week} isEmpty={days.length === 0} emptyKey="shift.mineEmpty">
        <div className="flex flex-col gap-2">
          {days.map((day) => {
            const source = sourceName(day.source);
            const working = Boolean(day.activeShiftId);

            return (
              <Card
                key={day.date}
                className={`flex flex-wrap items-center justify-between gap-2 py-3 ${
                  day.date === today ? 'border-cfi-yellow-dark bg-cfi-yellow/10' : ''
                } ${working ? '' : 'opacity-70'}`}
              >
                <div className="flex items-center gap-3">
                  <span className={working ? 'text-cfi-brown-dark' : 'text-cfi-muted'}>
                    <Icon name={working ? shiftIcon(day.startTime) : 'calendar'} size={20} />
                  </span>

                  <div>
                    <p className="font-medium text-cfi-ink">
                      {new Date(`${day.date}T00:00:00`).toLocaleDateString(undefined, {
                        weekday: 'long', day: 'numeric', month: 'long',
                      })}
                    </p>
                    <p className="text-sm text-cfi-muted">
                      {working ? day.shiftName : t(`shift.day_${source}`)}
                    </p>
                  </div>
                </div>

                <div className="flex items-center gap-2">
                  {source === 'Cover' && (
                    <Badge tone="bg-cfi-yellow/25 text-cfi-yellow-dark">{t('shift.cover')}</Badge>
                  )}

                  {working && (
                    <span className="font-mono text-sm text-cfi-brown-dark">
                      {timeRange(day.startTime, day.endTime)}
                    </span>
                  )}
                </div>
              </Card>
            );
          })}
        </div>
      </AsyncSection>
    </div>
  );
}
