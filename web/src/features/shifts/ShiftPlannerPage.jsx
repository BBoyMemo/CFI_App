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
  addShiftToPool, createCover, deleteCover, deleteShiftType, endRoster, getRoster,
  getShiftTypes, removeShiftFromPool, setRoster, upsertShiftType,
} from '../../api/endpoints';
import { useApiData } from '../../hooks/useApiData';
import { todayIso } from '../attendance/hours';
import NewShiftForm from './NewShiftForm';
import PersonChip from './PersonChip';
import ShiftCard from './ShiftCard';
import ShiftTemplateCard from './ShiftTemplateCard';
import { sourceName } from './shiftModel';

/**
 * The rota, in the three columns it is actually thought about: who there is, what shifts have
 * been drawn up, and which of those are running.
 *
 * The two levels are the point. A shift is drawn up once in the middle column and copied into
 * the pool on the right; the template stays where it is, because it is a sketch you keep. So
 * deleting a template later takes nothing away from the people working its copy.
 */
export default function ShiftPlannerPage() {
  const { t } = useTranslation();
  const today = todayIso();

  const [selected, setSelected] = useState(null);
  const [targetShiftId, setTargetShiftId] = useState(null);
  const [addingShift, setAddingShift] = useState(false);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);

  // What is being dragged, kept in a ref as well as in state. A drag starts and ends inside a
  // single burst of native events, before React has re-rendered with the new state, so the
  // drop handlers have to read something that is already true - otherwise the first pull does
  // nothing and you have to click once to "arm" it before dragging works.
  const draggingRef = useRef(null);

  const board = useApiData(() => getRoster({ on: today }), [today]);
  const templates = useApiData(() => getShiftTypes({ pageSize: 100 }));

  const shifts = board.data?.shifts ?? [];
  const unassigned = board.data?.unassigned ?? [];
  const library = (templates.data?.items ?? []).filter((x) => x.isActive);

  const clearSelection = useCallback(() => {
    draggingRef.current = null;
    setSelected(null);
    setTargetShiftId(null);
  }, []);

  // Escape is the way out of a half-finished move on a keyboard, and the only way out that
  // does not involve hunting for the cancel button.
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
    setBusy(true);
    try {
      await action();
      board.refetch();
      templates.refetch();
      clearSelection();
    } catch (actionError) {
      setError(describeApiError(actionError));
    } finally {
      setBusy(false);
    }
  }, [board, templates, clearSelection]);

  /** Lifting somebody: the same thing whether they were tapped or dragged. */
  const pickPerson = (person, fromShiftId) => {
    draggingRef.current = { kind: 'person', ...person, fromShiftId };
    setSelected(draggingRef.current);
    setTargetShiftId(null);
  };

  /** A template heading for the pool. It is copied, so nothing leaves the middle column. */
  const pickTemplate = (template) => {
    draggingRef.current = { kind: 'template', ...template };
    setSelected(null);
    setTargetShiftId(null);
  };

  const dropOnShift = (shift) => {
    const lifted = draggingRef.current;
    if (!lifted || lifted.kind !== 'person') return;

    setSelected(lifted);
    setTargetShiftId(shift.activeShiftId);
  };

  const dropOnPool = () => {
    const lifted = draggingRef.current;
    if (!lifted || lifted.kind !== 'template' || lifted.inPool) return;

    runAction(() => addShiftToPool(lifted.id));
  };

  const confirmPlacement = (shift, placement) => runAction(() =>
    placement.mode === 'cover'
      ? createCover({
        userId: selected.userId,
        activeShiftId: shift.activeShiftId,
        fromDate: placement.fromDate,
        toDate: placement.toDate,
        note: placement.note,
      })
      : setRoster({
        userId: selected.userId,
        activeShiftId: shift.activeShiftId,
        effectiveFrom: placement.effectiveFrom,
      }));

  /**
   * Taking somebody off a shift removes whatever put them there. For cover that is the cover
   * itself - ending their standing rota instead would take them off a shift they are not even
   * on today, and leave the cover running.
   */
  const takeOffPerson = (person) => runAction(() =>
    person.coverId
      ? deleteCover(person.coverId)
      : endRoster(person.userId, { effectiveFrom: today }));

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-bold text-cfi-brown-dark">{t('nav.shiftPlanner')}</h1>

        <Link to="/shifts/history">
          <Button variant="secondary" className="gap-2">
            <Icon name="history" size={16} />
            {t('shift.browseByDate')}
          </Button>
        </Link>
      </div>

      <ErrorBanner error={error} />

      <div className="grid gap-4 xl:grid-cols-[15rem_15rem_minmax(0,1fr)]">
        {/* ---- who there is ---- */}
        <Card className="self-start">
          <div className="mb-3 flex items-center gap-2 text-cfi-brown-dark">
            <Icon name="users" size={18} />
            <h2 className="font-semibold">{t('shift.team')}</h2>
          </div>

          <AsyncSection {...board} isEmpty={unassigned.length === 0} emptyKey="shift.everyoneOnAShift">
            <div className="flex flex-col gap-2">
              {unassigned.map((person) => {
                const source = sourceName(person.source);
                const away = source === 'Holiday' || source === 'Off' || source === 'PublicHoliday';

                return (
                  <PersonChip
                    key={person.userId}
                    person={person}
                    selected={selected?.userId === person.userId && selected.fromShiftId == null}
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
          </AsyncSection>
        </Card>

        {/* ---- what has been drawn up ---- */}
        <Card className="self-start">
          <div className="mb-3 flex items-center justify-between gap-2 text-cfi-brown-dark">
            <div className="flex items-center gap-2">
              <Icon name="clock" size={18} />
              <h2 className="font-semibold">{t('shift.shifts')}</h2>
            </div>

            {!addingShift && (
              <button
                type="button"
                onClick={() => setAddingShift(true)}
                aria-label={t('shift.newShift')}
                className="min-h-8 rounded border border-cfi-rule bg-cfi-sunk px-2 font-bold text-cfi-brown-dark hover:border-cfi-yellow-dark"
              >
                +
              </button>
            )}
          </div>

          {addingShift && (
            <div className="mb-3">
              <NewShiftForm
                today={today}
                busy={busy}
                onCancel={() => setAddingShift(false)}
                onSubmit={(payload) => {
                  setAddingShift(false);
                  runAction(() => upsertShiftType(payload));
                }}
              />
            </div>
          )}

          <AsyncSection {...templates} isEmpty={library.length === 0} emptyKey="shift.noShiftsDrawnUp">
            <div className="flex flex-col gap-2">
              {library.map((template) => (
                <ShiftTemplateCard
                  key={template.id}
                  template={template}
                  busy={busy}
                  onDragStart={() => pickTemplate(template)}
                  onDragEnd={() => { draggingRef.current = null; }}
                  onAdd={() => runAction(() => addShiftToPool(template.id))}
                  onDelete={() => runAction(() => deleteShiftType(template.id))}
                />
              ))}
            </div>
          </AsyncSection>

          <p className="mt-3 text-xs text-cfi-muted">{t('shift.poolHint')}</p>
        </Card>

        {/* ---- what is running ---- */}
        <div
          onDragOver={(event) => event.preventDefault()}
          onDrop={dropOnPool}
          className="flex min-w-0 flex-col gap-4 rounded-lg border border-dashed border-cfi-rule p-3"
        >
          <div className="flex items-center gap-2 text-cfi-brown-dark">
            <Icon name="box" size={18} />
            <h2 className="font-semibold">{t('shift.pool')}</h2>
          </div>

          <AsyncSection {...board} isEmpty={shifts.length === 0} emptyKey="shift.poolEmpty">
            {shifts.map((shift) => (
              <ShiftCard
                key={shift.activeShiftId}
                shift={shift}
                selected={selected}
                isTarget={targetShiftId === shift.activeShiftId}
                busy={busy}
                today={today}
                onPick={pickPerson}
                onDropHere={dropOnShift}
                onCancelPlacement={clearSelection}
                onConfirmPlacement={confirmPlacement}
                onRemovePerson={takeOffPerson}
                onRemoveShift={(shiftToRemove) =>
                  runAction(() => removeShiftFromPool(shiftToRemove.activeShiftId))}
              />
            ))}
          </AsyncSection>

          <p className="text-xs text-cfi-muted">{t('shift.dragHint')}</p>
        </div>
      </div>
    </div>
  );
}
