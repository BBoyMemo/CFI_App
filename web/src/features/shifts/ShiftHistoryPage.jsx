import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';

import AsyncSection from '../../components/ui/AsyncSection';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import Icon from '../../components/ui/Icon';
import { getRoster } from '../../api/endpoints';
import { useApiData } from '../../hooks/useApiData';
import WeekdayStrip from './WeekdayStrip';
import { addDays, todayIso } from '../attendance/hours';
import { shiftIcon, sourceName, timeRange } from './shiftModel';

const longDate = (isoDay) =>
  new Date(`${isoDay}T00:00:00`).toLocaleDateString(undefined, {
    weekday: 'long', day: 'numeric', month: 'long', year: 'numeric',
  });

/**
 * Pick a day and see what the factory was running: which shifts, and who was on each.
 *
 * It is the same board endpoint the planner uses, asked about a different date. That is the
 * whole trick - because a change closes the old row rather than overwriting it, asking about
 * last March answers with last March rather than with today.
 */
export default function ShiftHistoryPage() {
  const { t } = useTranslation();
  const today = todayIso();

  const [date, setDate] = useState(today);
  const [search, setSearch] = useState('');

  const board = useApiData(() => getRoster({ on: date }), [date]);

  const term = search.trim().toLowerCase();
  const shifts = (board.data?.shifts ?? [])
    .map((shift) => (term
      ? { ...shift, people: shift.people.filter((p) => p.fullName.toLowerCase().includes(term)) }
      : shift))
    // A search for a name hides the shifts that name is not on; a search that matches the
    // shift itself keeps it whole, so "night" answers "who was on nights".
    .filter((shift) => !term || shift.name.toLowerCase().includes(term) || shift.people.length > 0);

  const nobodyMatches = term.length > 0 && shifts.length === 0;

  return (
    <div className="mx-auto max-w-3xl">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-bold text-cfi-brown-dark">{t('shift.browseByDate')}</h1>

        <Link to="/shifts">
          <Button variant="secondary">{t('common.back')}</Button>
        </Link>
      </div>

      <Card className="mb-4">
        <div className="flex flex-wrap items-end gap-3">
          <label className="text-xs font-medium text-cfi-muted">
            {t('shift.date')}
            <div className="mt-1 flex items-center gap-1">
              <Button variant="secondary" aria-label={t('shift.previousDay')} onClick={() => setDate(addDays(date, -1))}>
                ‹
              </Button>
              <input
                type="date"
                value={date}
                onChange={(event) => event.target.value && setDate(event.target.value)}
                className="min-h-11 rounded border border-cfi-rule bg-white px-2 text-sm"
              />
              <Button variant="secondary" aria-label={t('shift.nextDay')} onClick={() => setDate(addDays(date, 1))}>
                ›
              </Button>
            </div>
          </label>

          <label className="min-w-48 flex-1 text-xs font-medium text-cfi-muted">
            {t('common.search')}
            <input
              type="search"
              value={search}
              placeholder={t('shift.searchPlaceholder')}
              onChange={(event) => setSearch(event.target.value)}
              className="mt-1 min-h-11 w-full rounded border border-cfi-rule bg-white px-2 text-sm"
            />
          </label>

          {date !== today && (
            <Button variant="secondary" onClick={() => setDate(today)}>{t('shift.today')}</Button>
          )}
        </div>

        <p className="mt-2 text-sm font-semibold text-cfi-brown-dark">{longDate(date)}</p>
      </Card>

      <AsyncSection
        {...board}
        isEmpty={shifts.length === 0}
        emptyKey={nobodyMatches ? 'common.noResults' : 'shift.nothingRanThatDay'}
      >
        <div className="flex flex-col gap-3">
          {shifts.map((shift) => (
            <Card key={shift.activeShiftId}>
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div>
                  <div className="flex items-center gap-2">
                    <span className="text-cfi-brown-dark">
                      <Icon name={shiftIcon(shift.startTime)} size={20} />
                    </span>
                    <h2 className="font-semibold text-cfi-brown-dark">{shift.name}</h2>
                    <span className="font-mono text-sm text-cfi-muted">
                      {timeRange(shift.startTime, shift.endTime)}
                    </span>
                  </div>

                  <div className="mt-1.5">
                    <WeekdayStrip days={shift.weekdays} />
                  </div>
                </div>

                <Badge tone={shift.people.length === 0 ? 'bg-cfi-sunk text-cfi-muted' : 'bg-cfi-green/15 text-cfi-green-dark'}>
                  {shift.people.length}
                </Badge>
              </div>

              {shift.people.length === 0 ? (
                <p className="mt-3 text-sm text-cfi-muted">{t('shift.nobodyOnIt')}</p>
              ) : (
                <ul className="mt-3 grid gap-2 sm:grid-cols-2">
                  {shift.people.map((person) => (
                    <li
                      key={person.userId}
                      className="flex items-center justify-between gap-2 rounded border border-cfi-rule bg-white px-2 py-1.5"
                    >
                      <span className="truncate text-sm text-cfi-ink">{person.fullName}</span>

                      {sourceName(person.source) === 'Cover' && (
                        <Badge tone="bg-cfi-yellow/25 text-cfi-yellow-dark">{t('shift.cover')}</Badge>
                      )}
                    </li>
                  ))}
                </ul>
              )}
            </Card>
          ))}
        </div>
      </AsyncSection>
    </div>
  );
}
