import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';

import AsyncSection from '../../components/ui/AsyncSection';
import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import ErrorBanner from '../../components/ui/ErrorBanner';
import Icon from '../../components/ui/Icon';
import { describeApiError } from '../../api/apiClient';
import {
  addShiftToPool, createCover, deleteCover, deleteShiftType, getPlanner, placeOnCrew,
  removeFromCrew, removeShiftFromPool, upsertShiftType,
} from '../../api/endpoints';
import { useApiData } from '../../hooks/useApiData';
import NewShiftForm from './NewShiftForm';
import PersonChip from './PersonChip';
import PoolCard from './PoolCard';
import ShiftCard from './ShiftCard';
import { sourceName } from './shiftModel';

/**
 * The shifts in the shift column this person is already on that work any of the same days
 * as the target. Days, not names: weekday mornings and a weekend shift never clash, two
 * Monday-to-Friday shifts always do. Cover and people already on their way off do not count.
 */
function clashesFor(shifts, person, target) {
  return shifts.filter((shift) =>
    shift.shiftTypeId !== target.shiftTypeId
    && shift.people.some((p) => p.userId === person.userId && !p.coverId && !p.toDate)
    && shift.weekdays.some((day) => target.weekdays.includes(day)));
}

/**
 * The rota, in three columns: who there is, the shifts with their crews, and the pool.
 *
 * The team column always lists everybody. Dragging a name onto a shift assigns it - the name
 * stays where it is, so the same person can be put on a weekday shift and a weekend shift.
 * What is not allowed is two shifts on the same day: that asks first, and then moves them.
 *
 * Only what is in the pool goes into history. A shift outside the pool is a draft, and so is
 * its crew; changes to a shift in the pool are dated.
 */
export default function ShiftPlannerPage() {
  const { t } = useTranslation();

  const [selected, setSelected] = useState(null);
  const [placement, setPlacement] = useState(null);
  const [addingShift, setAddingShift] = useState(false);
  const [notice, setNotice] = useState(null);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);

  // What is being dragged, kept in a ref as well as in state. A drag starts and ends inside a
  // single burst of native events, before React has re-rendered with the new state, so the
  // drop handlers have to read something that is already true - otherwise the first pull does
  // nothing and you have to click once to "arm" it before dragging works.
  const draggingRef = useRef(null);

  const planner = useApiData(() => getPlanner());

  const today = planner.data?.today;
  const shifts = planner.data?.shifts ?? [];
  const pool = planner.data?.pool ?? [];
  const team = planner.data?.team ?? [];

  const clearSelection = useCallback(() => {
    draggingRef.current = null;
    setSelected(null);
    setPlacement(null);
  }, []);

  // Escape is the way out of a half-finished move on a keyboard.
  useEffect(() => {
    if (!selected) return undefined;

    const onKeyDown = (event) => {
      if (event.key === 'Escape') clearSelection();
    };

    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [selected, clearSelection]);

  const runAction = useCallback(async (action) => {
    setError(null);
    setNotice(null);
    setBusy(true);
    try {
      await action();
      planner.refetch();
      clearSelection();
    } catch (actionError) {
      setError(describeApiError(actionError));
    } finally {
      setBusy(false);
    }
  }, [planner, clearSelection]);

  /** Lifting somebody: the same thing whether they were tapped or dragged. */
  const pickPerson = (person, fromShift) => {
    draggingRef.current = { kind: 'person', ...person, fromShiftTypeId: fromShift?.shiftTypeId ?? null };
    setSelected(draggingRef.current);
    setPlacement(null);
    setNotice(null);
  };

  /** A shift card heading for the pool. It stays in the shift column - only a copy runs. */
  const pickShift = (shift) => {
    draggingRef.current = { kind: 'shift', ...shift };
  };

  /**
   * Somebody dropped onto a shift. Already on it: nothing happens, and the screen says why.
   * On a clashing shift, or onto a shift in the pool: the panel opens - the first needs a
   * yes, the second a date, because it goes on record. Otherwise it is a draft and just
   * happens.
   */
  const dropPerson = (shift) => {
    const lifted = draggingRef.current;
    if (!lifted || lifted.kind !== 'person') return;

    // A cover chip belongs to its dates on its shift; dragging it elsewhere is not a move.
    if (lifted.coverId || lifted.fromShiftTypeId === shift.shiftTypeId) {
      clearSelection();
      return;
    }

    if (shift.people.some((p) => p.userId === lifted.userId && !p.coverId)) {
      clearSelection();
      setNotice(t('shift.alreadyOnShift', { person: lifted.fullName, shift: shift.name }));
      return;
    }

    const clashes = clashesFor(shifts, lifted, shift);
    const needsDates = shift.runningShiftId != null || clashes.some((x) => x.runningShiftId != null);

    if (needsDates || clashes.length > 0) {
      setSelected(lifted);
      setPlacement({ shiftTypeId: shift.shiftTypeId, clashes, needsDates });
      return;
    }

    runAction(() => placeOnCrew({ userId: lifted.userId, shiftTypeId: shift.shiftTypeId, effectiveFrom: null }));
  };

  const dropOnPool = () => {
    const lifted = draggingRef.current;
    if (!lifted || lifted.kind !== 'shift' || lifted.runningShiftId != null) return;

    runAction(() => addShiftToPool(lifted.shiftTypeId));
  };

  /**
   * The panel's answer. With an end date it is temporary - cover on that shift for those days,
   * and their own shift is left alone to pick them up again afterwards. Without one it is a
   * place on the crew until somebody changes it, moving them off anything that clashes.
   */
  const confirmPlacement = (shift, answer) => runAction(() =>
    answer.endDate
      ? createCover({
        userId: selected.userId,
        activeShiftId: shift.runningShiftId,
        fromDate: answer.startDate,
        toDate: answer.endDate,
        note: answer.note,
      })
      : placeOnCrew({
        userId: selected.userId,
        shiftTypeId: shift.shiftTypeId,
        effectiveFrom: answer.startDate,
        moveFromClashing: placement.clashes.length > 0,
      }));

  /**
   * Taking somebody off a shift removes whatever put them there, on that shift only. Cover is
   * just the cover. A place that has not started yet is cancelled from its own date, so the
   * days before it stay as they were; anything else ends today.
   */
  const takeOff = (person, shift) => runAction(() =>
    person.coverId
      ? deleteCover(person.coverId)
      : removeFromCrew(person.userId, {
        shiftTypeId: shift.shiftTypeId,
        effectiveFrom: person.fromDate ?? today,
      }));

  /** A name dragged from a shift back onto the team column comes off that shift. */
  const dropOnTeam = () => {
    const lifted = draggingRef.current;
    if (!lifted || lifted.kind !== 'person' || lifted.fromShiftTypeId == null) return;
    takeOff(lifted, { shiftTypeId: lifted.fromShiftTypeId });
  };

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-bold text-cfi-brown-dark">{t('nav.shiftPlanner')}</h1>

        <Link to="/shifts/history">
          <Button variant="secondary" className="gap-2">
            <Icon name="history" size={16} />
            {t('shift.historyTitle')}
          </Button>
        </Link>
      </div>

      <ErrorBanner error={error} />

      {notice && (
        <p className="mb-3 flex items-center gap-2 rounded bg-cfi-yellow/20 px-3 py-2 text-sm text-cfi-brown-dark">
          <Icon name="bell" size={16} />
          {notice}
        </p>
      )}

      <AsyncSection {...planner} isEmpty={false}>
        <div className="grid gap-4 xl:grid-cols-[14rem_minmax(0,1fr)_17rem]">
          {/* ---- who there is: everybody, always ---- */}
          <Card
            className="self-start"
            onDragOver={(event) => event.preventDefault()}
            onDrop={dropOnTeam}
          >
            <div className="mb-3 flex items-center gap-2 text-cfi-brown-dark">
              <Icon name="users" size={18} />
              <h2 className="font-semibold">{t('shift.team')}</h2>
            </div>

            {team.length === 0 ? (
              <p className="text-sm text-cfi-muted">{t('shift.teamEmpty')}</p>
            ) : (
              <div className="flex flex-col gap-2">
                {team.map((person) => {
                  const source = sourceName(person.source);
                  const away = source === 'Holiday' || source === 'Off' || source === 'PublicHoliday';

                  return (
                    <PersonChip
                      key={person.userId}
                      person={person}
                      detail={person.shiftNames.join(' · ')}
                      selected={selected?.userId === person.userId && selected.fromShiftTypeId == null}
                      onSelect={() => pickPerson(person, null)}
                      onDragStart={() => pickPerson(person, null)}
                    >
                      {away && (
                        <span className="flex shrink-0 items-center gap-1 text-xs text-cfi-muted">
                          <Icon name="calendar" size={13} />
                          {t(`shift.away_${source}`)}
                        </span>
                      )}
                    </PersonChip>
                  );
                })}
              </div>
            )}
          </Card>

          {/* ---- the shifts, with their crews ---- */}
          <div className="flex min-w-0 flex-col gap-4">
            <div className="flex items-center justify-between gap-2 text-cfi-brown-dark">
              <div className="flex items-center gap-2">
                <Icon name="clock" size={18} />
                <h2 className="font-semibold">{t('shift.shifts')}</h2>
              </div>

              {!addingShift && (
                <Button variant="secondary" onClick={() => setAddingShift(true)} className="gap-1">
                  + {t('shift.newShift')}
                </Button>
              )}
            </div>

            {addingShift && (
              <NewShiftForm
                today={today}
                busy={busy}
                onCancel={() => setAddingShift(false)}
                onSubmit={(payload) => {
                  setAddingShift(false);
                  runAction(() => upsertShiftType(payload));
                }}
              />
            )}

            {shifts.length === 0 && !addingShift && (
              <p className="text-sm text-cfi-muted">{t('shift.noShiftsDrawnUp')}</p>
            )}

            {shifts.map((shift) => (
              <ShiftCard
                key={shift.shiftTypeId}
                shift={shift}
                selected={selected}
                placement={placement}
                busy={busy}
                today={today}
                onPickShift={pickShift}
                onDragEnd={() => { if (draggingRef.current?.kind === 'shift') draggingRef.current = null; }}
                onAddToPool={(s) => runAction(() => addShiftToPool(s.shiftTypeId))}
                onDelete={(s) => runAction(() => deleteShiftType(s.shiftTypeId))}
                onPickPerson={pickPerson}
                onDropPerson={dropPerson}
                onCancelPlacement={clearSelection}
                onConfirmPlacement={confirmPlacement}
                onRemovePerson={takeOff}
              />
            ))}

            <p className="text-xs text-cfi-muted">{t('shift.dragHint')}</p>
          </div>

          {/* ---- what is running ---- */}
          <div
            onDragOver={(event) => event.preventDefault()}
            onDrop={dropOnPool}
            className="flex flex-col gap-3 self-start rounded-lg border border-dashed border-cfi-rule p-3"
          >
            <div className="flex items-center gap-2 text-cfi-brown-dark">
              <Icon name="box" size={18} />
              <h2 className="font-semibold">{t('shift.pool')}</h2>
            </div>

            {pool.length === 0 ? (
              <p className="text-sm text-cfi-muted">{t('shift.poolEmpty')}</p>
            ) : (
              pool.map((shift) => (
                <PoolCard
                  key={shift.activeShiftId}
                  shift={shift}
                  today={today}
                  busy={busy}
                  onRemove={(s) => runAction(() => removeShiftFromPool(s.activeShiftId))}
                />
              ))
            )}

            <p className="text-xs text-cfi-muted">{t('shift.poolHint')}</p>
          </div>
        </div>
      </AsyncSection>
    </div>
  );
}
