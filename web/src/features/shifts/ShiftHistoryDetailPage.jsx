import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router-dom';

import AsyncSection from '../../components/ui/AsyncSection';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import Icon from '../../components/ui/Icon';
import { getShiftHistoryDetail } from '../../api/endpoints';
import { useApiData } from '../../hooks/useApiData';
import { addDays } from '../attendance/hours';
import WeekdayStrip from './WeekdayStrip';
import { dayOfWeek, fullDate, shiftIcon, shortDate, sourceName, timeRange } from './shiftModel';

/** What each cell of the week says, and how it looks. */
const CELL = {
  Roster: { mark: '●', tone: 'bg-cfi-green/15 text-cfi-green-dark' },
  Cover: { mark: 'C', tone: 'border border-dashed border-cfi-yellow-dark bg-cfi-yellow/15 text-cfi-yellow-dark' },
  Off: { mark: '–', tone: 'bg-cfi-red/10 text-cfi-red' },
  Holiday: { mark: 'L', tone: 'bg-cfi-sunk text-cfi-brown-dark' },
  PublicHoliday: { mark: 'BH', tone: 'bg-cfi-sunk text-cfi-brown-dark' },
};

const KIND = ['Joined', 'Left', 'Cover', 'Off'];

/**
 * One shift's history: a week at a time, who worked which day, and every crew change made
 * while it was in the pool.
 *
 * A week, not a day, because that is how the site reads a rota - and a day off or a cover
 * stands out against the rest of the week in a way it never does on its own.
 */
export default function ShiftHistoryDetailPage() {
  const { t } = useTranslation();
  const { id } = useParams();

  // Null until the first answer: the server picks this week, or the shift's last week if it
  // has ended, so an old shift does not open on an empty grid.
  const [week, setWeek] = useState(null);

  const detail = useApiData(() => getShiftHistoryDetail(id, week ? { week } : {}), [id, week]);

  const data = detail.data;
  const shift = data?.shift;
  const weekStart = data?.weekStart;

  const step = (days) => setWeek(addDays(weekStart, days));

  return (
    <div className="mx-auto max-w-4xl">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-bold text-cfi-brown-dark">{t('shift.historyTitle')}</h1>

        <Link to="/shifts/history">
          <Button variant="secondary">{t('common.back')}</Button>
        </Link>
      </div>

      <AsyncSection {...detail} isEmpty={false}>
        {shift && (
          <>
            <Card className="mb-4">
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div>
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="text-cfi-brown-dark"><Icon name={shiftIcon(shift.startTime)} size={22} /></span>
                    <h2 className="text-lg font-semibold text-cfi-brown-dark">{shift.name}</h2>
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
            </Card>

            {/* ---- the week ---- */}
            <Card className="mb-4">
              <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
                <div className="flex items-center gap-1">
                  <Button variant="secondary" aria-label={t('shift.previousWeek')} onClick={() => step(-7)}>‹</Button>
                  <span className="px-2 text-sm font-semibold text-cfi-brown-dark">
                    {shortDate(weekStart)} – {fullDate(addDays(weekStart, 6))}
                  </span>
                  <Button variant="secondary" aria-label={t('shift.nextWeek')} onClick={() => step(7)}>›</Button>
                </div>

                <label className="flex items-center gap-2 text-xs font-medium text-cfi-muted">
                  {t('shift.goToDate')}
                  <input
                    type="date"
                    value={weekStart}
                    onChange={(event) => event.target.value && setWeek(event.target.value)}
                    className="min-h-10 rounded border border-cfi-rule bg-white px-2 text-sm"
                  />
                </label>
              </div>

              {data.people.length === 0 ? (
                <p className="text-sm text-cfi-muted">{t('shift.nobodyThatWeek')}</p>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full min-w-[34rem] border-collapse text-sm">
                    <thead>
                      <tr>
                        <th className="p-2 text-left font-semibold text-cfi-muted">{t('shift.whoWorked')}</th>
                        {data.days.map((day) => {
                          const runs = shift.weekdays.includes(dayOfWeek(day));
                          return (
                            <th
                              key={day}
                              className={`p-2 text-center text-xs font-semibold ${runs ? 'text-cfi-brown-dark' : 'text-cfi-muted/60'}`}
                            >
                              {new Date(`${day}T00:00:00`).toLocaleDateString(undefined, { weekday: 'short' })}
                              <div className="font-normal">{shortDate(day)}</div>
                            </th>
                          );
                        })}
                      </tr>
                    </thead>

                    <tbody>
                      {data.people.map((person) => (
                        <tr key={person.userId} className="border-t border-cfi-rule">
                          <td className="p-2 font-medium text-cfi-ink">{person.fullName}</td>
                          {person.days.map((day) => {
                            const runs = shift.weekdays.includes(dayOfWeek(day.date));
                            const cell = CELL[sourceName(day.source)];

                            return (
                              <td
                                key={day.date}
                                title={day.note ?? undefined}
                                className={`p-1 text-center ${runs ? '' : 'bg-cfi-sunk/50'}`}
                              >
                                {cell && (
                                  <span
                                    className={`inline-flex h-7 min-w-7 items-center justify-center rounded px-1 text-xs font-bold ${cell.tone}`}
                                  >
                                    {cell.mark}
                                  </span>
                                )}
                              </td>
                            );
                          })}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}

              <div className="mt-3 flex flex-wrap gap-3 text-xs text-cfi-muted">
                {Object.entries(CELL).map(([name, cell]) => (
                  <span key={name} className="flex items-center gap-1">
                    <span className={`inline-flex h-5 min-w-5 items-center justify-center rounded px-1 text-[10px] font-bold ${cell.tone}`}>
                      {cell.mark}
                    </span>
                    {t(`shift.legend_${name}`)}
                  </span>
                ))}
              </div>
            </Card>

            {/* ---- every change ---- */}
            <Card>
              <div className="mb-3 flex items-center gap-2 text-cfi-brown-dark">
                <Icon name="history" size={18} />
                <h2 className="font-semibold">{t('shift.changes')}</h2>
              </div>

              {data.changes.length === 0 ? (
                <p className="text-sm text-cfi-muted">{t('shift.noChanges')}</p>
              ) : (
                <ol className="flex flex-col gap-3">
                  {data.changes.map((change, index) => {
                    const kind = KIND[change.kind];

                    return (
                      <li key={`${change.userId}-${kind}-${change.fromDate}-${index}`} className="border-l-2 border-cfi-rule pl-3">
                        <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                          <span className="text-sm font-semibold text-cfi-brown-dark">{fullDate(change.fromDate)}</span>
                          <span className="text-sm text-cfi-ink">{change.fullName}</span>
                          <Badge
                            tone={kind === 'Joined'
                              ? 'bg-cfi-green/15 text-cfi-green-dark'
                              : kind === 'Left'
                                ? 'bg-cfi-sunk text-cfi-brown-dark'
                                : 'bg-cfi-yellow/25 text-cfi-yellow-dark'}
                          >
                            {t(`shift.change_${kind}`)}
                          </Badge>
                          {change.toDate && change.toDate !== change.fromDate && (
                            <span className="text-xs text-cfi-muted">→ {fullDate(change.toDate)}</span>
                          )}
                        </div>

                        <p className="mt-0.5 text-xs text-cfi-muted">
                          {change.changedByName && t('shift.by', { name: change.changedByName })}
                          {change.note && ` · ${change.note}`}
                        </p>
                      </li>
                    );
                  })}
                </ol>
              )}
            </Card>
          </>
        )}
      </AsyncSection>
    </div>
  );
}
