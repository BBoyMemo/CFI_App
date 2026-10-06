import { useTranslation } from 'react-i18next';

import Badge from '../../components/ui/Badge';
import Card from '../../components/ui/Card';
import ConfirmButton from '../../components/ui/ConfirmButton';
import Icon from '../../components/ui/Icon';
import PersonChip from './PersonChip';
import PlacementPanel from './PlacementPanel';
import WeekdayStrip from './WeekdayStrip';
import { fullDate, shiftIcon, timeRange } from './shiftModel';

/**
 * A shift in the shift column, with its crew. This is where the work is done: names are
 * dropped onto it, taken off it, moved between shifts. Drag the card itself into the pool to
 * make it run.
 *
 * While it is in the pool the crew shown is its dated rota - a move that starts later says
 * "from", one that is ending says "until", cover is dashed. While it is not, the crew is a
 * draft and nothing about it is on record.
 */
export default function ShiftCard({
  shift,
  selected,
  placement,
  busy,
  today,
  onPickShift,
  onDragEnd,
  onAddToPool,
  onDelete,
  onPickPerson,
  onDropPerson,
  onCancelPlacement,
  onConfirmPlacement,
  onRemovePerson,
}) {
  const { t } = useTranslation();

  const running = shift.runningShiftId != null;
  const isTarget = placement?.shiftTypeId === shift.shiftTypeId;

  return (
    <Card
      // Not while the date panel is open: inside a draggable element, pressing on a date box
      // can start dragging the card instead of opening the picker.
      draggable={!isTarget}
      onDragStart={() => onPickShift(shift)}
      onDragEnd={onDragEnd}
      // Unconditional: a card is always willing to take somebody, and testing state here would
      // mean the very first drag of a page load is refused before it starts.
      onDragOver={(event) => event.preventDefault()}
      onDrop={(event) => {
        event.stopPropagation();
        onDropPerson(shift);
      }}
      onClick={() => {
        if (selected) onDropPerson(shift);
      }}
      className={`cursor-grab transition-colors active:cursor-grabbing ${
        isTarget ? 'border-cfi-yellow-dark' : selected ? 'border-dashed border-cfi-yellow-dark/60' : ''
      }`}
    >
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-cfi-brown-dark"><Icon name={shiftIcon(shift.startTime)} size={20} /></span>
            <h3 className="font-semibold text-cfi-brown-dark">{shift.name}</h3>
            <span className="font-mono text-sm text-cfi-muted">{timeRange(shift.startTime, shift.endTime)}</span>
          </div>

          <div className="mt-1.5 flex flex-wrap items-center gap-3">
            <WeekdayStrip days={shift.weekdays} />
            <span className="flex items-center gap-1 text-xs text-cfi-muted">
              <Icon name="calendar" size={12} />
              {t('shift.startsFrom', { date: fullDate(shift.startsOn) })}
            </span>
          </div>
        </div>

        <div className="flex items-center gap-2">
          {running ? (
            <Badge tone="bg-cfi-green/15 text-cfi-green-dark">{t('shift.inPool')}</Badge>
          ) : (
            <button
              type="button"
              disabled={busy}
              onClick={(event) => {
                event.stopPropagation();
                onAddToPool(shift);
              }}
              title={t('shift.addToPool')}
              className="flex min-h-8 items-center gap-1 rounded border border-cfi-rule bg-cfi-sunk px-2 text-xs font-semibold text-cfi-brown-dark hover:border-cfi-yellow-dark"
            >
              <Icon name="box" size={14} />
              →
            </button>
          )}

          <span onClick={(event) => event.stopPropagation()} role="presentation">
            <ConfirmButton
              onConfirm={() => onDelete(shift)}
              disabled={busy}
              question={t('shift.deleteShiftQuestion')}
              confirmLabel={t('common.delete')}
              variant="ghost"
              className="min-h-8 px-1"
            >
              ×
            </ConfirmButton>
          </span>
        </div>
      </div>

      {shift.people.length === 0 && !isTarget ? (
        <p className="mt-3 rounded border border-dashed border-cfi-rule px-3 py-3 text-center text-sm text-cfi-muted">
          {t('shift.dropHere')}
        </p>
      ) : (
        <div className="mt-3 grid gap-2 sm:grid-cols-2 2xl:grid-cols-3">
          {shift.people.map((person) => (
            <PersonChip
              key={`${person.source}-${person.userId}-${person.fromDate ?? ''}`}
              person={person}
              selected={selected?.userId === person.userId && selected.fromShiftTypeId === shift.shiftTypeId}
              onSelect={() => onPickPerson(person, shift)}
              onDragStart={() => onPickPerson(person, shift)}
            >
              {/* Somebody already on their way off this shift has nothing left to remove. */}
              {!person.toDate || person.coverId ? (
                <button
                  type="button"
                  disabled={busy}
                  aria-label={t('shift.takeOff')}
                  onClick={(event) => {
                    event.stopPropagation();
                    onRemovePerson(person, shift);
                  }}
                  className="shrink-0 px-1 font-bold text-cfi-muted hover:text-cfi-red"
                >
                  ×
                </button>
              ) : null}
            </PersonChip>
          ))}
        </div>
      )}

      {isTarget && selected && (
        <PlacementPanel
          personName={selected.fullName}
          shiftName={shift.name}
          today={today}
          clashes={placement.clashes}
          needsDates={placement.needsDates}
          allowEnd={running}
          busy={busy}
          onCancel={onCancelPlacement}
          onSubmit={(answer) => onConfirmPlacement(shift, answer)}
        />
      )}
    </Card>
  );
}
