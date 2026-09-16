import { useCallback, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

import AsyncSection from '../../components/ui/AsyncSection';
import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import ErrorBanner from '../../components/ui/ErrorBanner';
import { describeApiError } from '../../api/apiClient';
import {
  createShiftAssignment, deleteShiftAssignment, getShiftBoard, getShiftTypes, getTeam,
} from '../../api/endpoints';
import { useApiData } from '../../hooks/useApiData';
import { addDays, weekStart } from '../attendance/hours';

export default function ShiftPlannerPage() {
  const { t } = useTranslation();

  const [from, setFrom] = useState(() => weekStart(new Date()));
  const [dragging, setDragging] = useState(null);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);

  const days = useMemo(() => Array.from({ length: 7 }, (_, i) => addDays(from, i)), [from]);
  const to = days[6];

  const team = useApiData(() => getTeam({ pageSize: 100 }));
  const shiftTypes = useApiData(() => getShiftTypes({ pageSize: 50 }));
  const board = useApiData(() => getShiftBoard({ from, to }), [from, to]);

  const members = team.data?.items ?? [];
  const types = (shiftTypes.data?.items ?? []).filter((x) => x.isActive);
  const assignments = board.data ?? [];

  const runAction = useCallback(async (action) => {
    setError(null);
    setBusy(true);
    try {
      await action();
      board.refetch();
    } catch (actionError) {
      setError(describeApiError(actionError));
    } finally {
      setBusy(false);
    }
  }, [board]);

  const cellAssignments = (day, shiftTypeId) =>
    assignments.filter((x) => x.date === day && x.shiftTypeId === shiftTypeId);

  const drop = (day, shiftTypeId) => {
    if (!dragging) return;

    // A move is a delete plus a create: the API has no "move" verb, and doing it in that
    // order means a rejected create leaves the person where they already were.
    runAction(async () => {
      if (dragging.assignmentId) await deleteShiftAssignment(dragging.assignmentId);
      await createShiftAssignment({ userId: dragging.userId, date: day, shiftTypeId });
    });

    setDragging(null);
  };

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-bold text-cfi-brown-dark">{t('nav.shiftPlanner')}</h1>

        <div className="flex items-center gap-2">
          <Button variant="secondary" onClick={() => setFrom(addDays(from, -7))}>←</Button>
          <span className="text-sm text-cfi-muted">{from} → {to}</span>
          <Button variant="secondary" onClick={() => setFrom(addDays(from, 7))}>→</Button>
          <Button variant="secondary" onClick={() => setFrom(weekStart(new Date()))}>
            {t('shift.thisWeek')}
          </Button>
        </div>
      </div>

      <ErrorBanner error={error} />

      <div className="flex flex-col gap-4 lg:flex-row">
        <Card className="lg:w-56 lg:shrink-0">
          <h2 className="mb-2 font-semibold text-cfi-brown-dark">{t('shift.people')}</h2>
          <AsyncSection {...team} isEmpty={members.length === 0} emptyKey="shift.noPeople">
            <div className="flex flex-wrap gap-2 lg:flex-col">
              {members.map((member) => (
                <div
                  key={member.id}
                  draggable
                  onDragStart={() => setDragging({ userId: member.id, assignmentId: null })}
                  onDragEnd={() => setDragging(null)}
                  className="cursor-grab rounded border border-cfi-rule bg-white px-2 py-1 text-sm active:cursor-grabbing"
                >
                  {member.fullName}
                </div>
              ))}
            </div>
          </AsyncSection>
        </Card>

        <div className="min-w-0 flex-1 overflow-x-auto">
          <AsyncSection {...board} isEmpty={false}>
            <table className="w-full min-w-[46rem] border-collapse text-sm">
              <thead>
                <tr>
                  <th className="border border-cfi-rule bg-cfi-sunk p-2 text-left">{t('shift.shift')}</th>
                  {days.map((day) => (
                    <th key={day} className="border border-cfi-rule bg-cfi-sunk p-2 text-left">
                      {new Date(`${day}T00:00:00`).toLocaleDateString(undefined, {
                        weekday: 'short', day: 'numeric', month: 'short',
                      })}
                    </th>
                  ))}
                </tr>
              </thead>

              <tbody>
                {types.map((type) => (
                  <tr key={type.id}>
                    <th className="border border-cfi-rule bg-cfi-sunk p-2 text-left align-top">
                      <div className="font-semibold text-cfi-brown-dark">{type.name}</div>
                      <div className="text-xs font-normal text-cfi-muted">
                        {type.startTime.slice(0, 5)}–{type.endTime.slice(0, 5)}
                      </div>
                    </th>

                    {days.map((day) => (
                      <td
                        key={day}
                        onDragOver={(event) => event.preventDefault()}
                        onDrop={() => drop(day, type.id)}
                        className="min-w-28 border border-cfi-rule p-1 align-top"
                      >
                        <div className="flex min-h-12 flex-col gap-1">
                          {cellAssignments(day, type.id).map((assignment) => (
                            <div
                              key={assignment.id}
                              draggable
                              onDragStart={() =>
                                setDragging({ userId: assignment.userId, assignmentId: assignment.id })}
                              onDragEnd={() => setDragging(null)}
                              className="group flex cursor-grab items-center justify-between gap-1 rounded bg-cfi-yellow/25 px-2 py-1 text-xs active:cursor-grabbing"
                            >
                              <span className="truncate">{assignment.userFullName}</span>
                              <button
                                type="button"
                                disabled={busy}
                                onClick={() => runAction(() => deleteShiftAssignment(assignment.id))}
                                className="font-bold text-cfi-red"
                                aria-label={t('common.delete')}
                              >
                                ×
                              </button>
                            </div>
                          ))}
                        </div>
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </AsyncSection>

          <p className="mt-2 text-xs text-cfi-muted">{t('shift.dragHint')}</p>
        </div>
      </div>
    </div>
  );
}
