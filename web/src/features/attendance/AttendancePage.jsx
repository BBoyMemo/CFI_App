import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

import AsyncSection from '../../components/ui/AsyncSection';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import { getMyClockEvents } from '../../api/endpoints';
import { clockTypeKey } from '../../api/enums';
import { useApiData } from '../../hooks/useApiData';
import { formatHours, workedMinutes } from './hours';

/**
 * A month of hours, because a month is what gets paid.
 *
 * The clock button is deliberately not on this screen - it lives in the side panel beside
 * the person's name, which is where the prototype settled it after several rounds. This
 * screen answers the other question: how much have I worked, and on which days.
 */
function monthKey(date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`;
}

function startOfMonth(key) {
  const [year, month] = key.split('-').map(Number);
  return new Date(year, month - 1, 1);
}

function shiftMonth(key, months) {
  const date = startOfMonth(key);
  date.setMonth(date.getMonth() + months);
  return monthKey(date);
}

export default function AttendancePage() {
  const { t } = useTranslation();
  const [month, setMonth] = useState(() => monthKey(new Date()));

  const from = useMemo(() => startOfMonth(month), [month]);
  const to = useMemo(() => startOfMonth(shiftMonth(month, 1)), [month]);

  const events = useApiData(
    () => getMyClockEvents({ from: from.toISOString(), to: to.toISOString(), pageSize: 100 }),
    [month],
  );

  const items = useMemo(() => events.data?.items ?? [], [events.data]);

  // One row per day worked, newest first, so a month reads as a timesheet rather than as
  // a stream of punches.
  const days = useMemo(() => {
    const byDay = new Map();

    for (const event of items) {
      const key = new Date(event.occurredAtUtc).toDateString();
      if (!byDay.has(key)) byDay.set(key, []);
      byDay.get(key).push(event);
    }

    return [...byDay.entries()]
      .map(([key, dayEvents]) => ({
        key,
        date: new Date(key),
        events: dayEvents,
        minutes: workedMinutes(dayEvents),
        suspect: dayEvents.some((x) => x.isSuspect),
      }))
      .sort((a, b) => b.date - a.date);
  }, [items]);

  const monthTotal = days.reduce((sum, day) => sum + day.minutes, 0);
  const isThisMonth = month === monthKey(new Date());

  return (
    <div className="mx-auto max-w-3xl">
      <h1 className="mb-4 text-xl font-bold text-cfi-brown-dark">{t('nav.attendance')}</h1>

      <Card>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <p className="text-sm text-cfi-muted">
              {startOfMonth(month).toLocaleDateString(undefined, { month: 'long', year: 'numeric' })}
            </p>
            <p className="text-2xl font-bold text-cfi-brown-dark">{formatHours(monthTotal)}</p>
            <p className="text-xs text-cfi-muted">{t('attendance.daysWorked', { count: days.length })}</p>
          </div>

          <div className="flex items-center gap-2">
            <Button variant="secondary" onClick={() => setMonth(shiftMonth(month, -1))}>←</Button>
            <Button variant="secondary" disabled={isThisMonth} onClick={() => setMonth(shiftMonth(month, 1))}>
              →
            </Button>
          </div>
        </div>

        <p className="mt-3 text-xs text-cfi-muted">{t('attendance.manualOnlyNote')}</p>
      </Card>

      <div className="mt-4">
        <AsyncSection {...events} isEmpty={days.length === 0} emptyKey="attendance.emptyMonth">
          <div className="flex flex-col gap-2">
            {days.map((day) => (
              <Card key={day.key} className="py-3">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className="font-medium text-cfi-ink">
                    {day.date.toLocaleDateString(undefined, {
                      weekday: 'short', day: 'numeric', month: 'short',
                    })}
                  </span>

                  <div className="flex items-center gap-2">
                    {day.suspect && (
                      <Badge tone="bg-cfi-yellow/25 text-cfi-yellow-dark">{t('attendance.suspect')}</Badge>
                    )}
                    <span className="font-semibold text-cfi-brown-dark">{formatHours(day.minutes)}</span>
                  </div>
                </div>

                <div className="mt-1 flex flex-wrap gap-x-4 text-xs text-cfi-muted">
                  {[...day.events]
                    .sort((a, b) => new Date(a.occurredAtUtc) - new Date(b.occurredAtUtc))
                    .map((event) => (
                      <span key={event.id}>
                        {t(clockTypeKey(event.type))}{' '}
                        {new Date(event.occurredAtUtc).toLocaleTimeString([], {
                          hour: '2-digit', minute: '2-digit',
                        })}
                      </span>
                    ))}
                </div>
              </Card>
            ))}
          </div>
        </AsyncSection>
      </div>
    </div>
  );
}
