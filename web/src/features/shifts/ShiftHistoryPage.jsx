import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';

import AsyncSection from '../../components/ui/AsyncSection';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import Icon from '../../components/ui/Icon';
import { getShiftHistory } from '../../api/endpoints';
import { useApiData } from '../../hooks/useApiData';
import WeekdayStrip from './WeekdayStrip';
import { fullDate, shiftIcon, timeRange } from './shiftModel';

/**
 * Every shift that has been in the pool. Only the pool is history: a shift that was drawn up
 * but never put in has nothing to show, so it is not here.
 *
 * One search box for a shift's name or the name of anybody who was on it, and a date to see
 * what was running that day. Open a shift for the week-by-week detail.
 */
export default function ShiftHistoryPage() {
  const { t } = useTranslation();

  const [search, setSearch] = useState('');
  const [on, setOn] = useState('');

  const term = search.trim();

  const history = useApiData(
    () => getShiftHistory({ search: term || undefined, on: on || undefined }),
    [term, on],
  );

  const shifts = history.data ?? [];
  const filtered = term.length > 0 || on.length > 0;

  return (
    <div className="mx-auto max-w-3xl">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-bold text-cfi-brown-dark">{t('shift.historyTitle')}</h1>

        <Link to="/shifts">
          <Button variant="secondary">{t('common.back')}</Button>
        </Link>
      </div>

      <Card className="mb-4">
        <div className="flex flex-wrap items-end gap-3">
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

          <label className="text-xs font-medium text-cfi-muted">
            {t('shift.date')}
            <input
              type="date"
              value={on}
              onChange={(event) => setOn(event.target.value)}
              className="mt-1 block min-h-11 rounded border border-cfi-rule bg-white px-2 text-sm"
            />
          </label>

          {filtered && (
            <Button variant="secondary" onClick={() => { setSearch(''); setOn(''); }}>
              {t('shift.clearSearch')}
            </Button>
          )}
        </div>
      </Card>

      <AsyncSection
        {...history}
        isEmpty={shifts.length === 0}
        emptyKey={filtered ? 'common.noResults' : 'shift.historyEmpty'}
      >
        <div className="flex flex-col gap-3">
          {shifts.map((shift) => (
            <Link key={shift.activeShiftId} to={`/shifts/history/${shift.activeShiftId}`}>
              <Card className="transition-colors hover:border-cfi-yellow-dark">
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="text-cfi-brown-dark"><Icon name={shiftIcon(shift.startTime)} size={20} /></span>
                      <h2 className="font-semibold text-cfi-brown-dark">{shift.name}</h2>
                      <span className="font-mono text-sm text-cfi-muted">{timeRange(shift.startTime, shift.endTime)}</span>
                    </div>

                    <div className="mt-1.5 flex flex-wrap items-center gap-3">
                      <WeekdayStrip days={shift.weekdays} />
                      <span className="flex items-center gap-1 text-xs text-cfi-muted">
                        <Icon name="calendar" size={12} />
                        {fullDate(shift.startsOn)} → {shift.endsOn ? fullDate(shift.endsOn) : t('shift.now')}
                      </span>
                    </div>
                  </div>

                  {shift.endsOn ? (
                    <Badge>{t('shift.ended')}</Badge>
                  ) : (
                    <Badge tone="bg-cfi-green/15 text-cfi-green-dark">{t('shift.inPool')}</Badge>
                  )}
                </div>

                {shift.people.length > 0 && (
                  <p className="mt-2 truncate text-sm text-cfi-ink-soft">
                    <Icon name="users" size={13} className="mr-1 inline" />
                    {shift.people.join(', ')}
                  </p>
                )}
              </Card>
            </Link>
          ))}
        </div>
      </AsyncSection>
    </div>
  );
}
