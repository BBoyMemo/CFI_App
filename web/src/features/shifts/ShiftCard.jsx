import { useTranslation } from 'react-i18next';

import Badge from '../../components/ui/Badge';
import Card from '../../components/ui/Card';
import ConfirmButton from '../../components/ui/ConfirmButton';
import Icon from '../../components/ui/Icon';
import PersonChip from './PersonChip';
import PlacementPanel from './PlacementPanel';
import WeekdayStrip from './WeekdayStrip';
import { shiftIcon, timeRange } from './shiftModel';

/**
 * A shift in the pool - one that is actually being worked, with everybody on it.
 *
 * This is the unit the rota is planned in: the same people, the same shift, week after week,
 * so it is a card you fill rather than a column of seven boxes you fill again every week.
 */
export default function ShiftCard({
  shift,
  selected,
  isTarget,
  busy,
  today,
  onPick,
  onDropHere,
  onCancelPlacement,
  onConfirmPlacement,
  onRemovePerson,
  onRemoveShift,
}) {
  const { t } = useTranslation();

  return (
    <Card
      // Unconditional: a card is always willing to take somebody, and testing state here
      // would mean the very first drag of a page load is refused before it starts.
      onDragOver={(event) => event.preventDefault()}
      onDrop={() => onDropHere(shift)}
      className={`transition-colors ${isTarget ? 'border-cfi-yellow-dark' : ''}`}
    >
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <div className="flex items-center gap-2">
            <span className="text-cfi-brown-dark"><Icon name={shiftIcon(shift.startTime)} size={20} /></span>
            <h3 className="font-semibold text-cfi-brown-dark">{shift.name}</h3>
            <span className="font-mono text-sm text-cfi-muted">
              {timeRange(shift.startTime, shift.endTime)}
            </span>
          </div>

          <div className="mt-1.5">
            <WeekdayStrip days={shift.weekdays} />
          </div>
        </div>

        <div className="flex items-center gap-2">
          <Badge tone={shift.people.length === 0 ? 'bg-cfi-sunk text-cfi-muted' : 'bg-cfi-green/15 text-cfi-green-dark'}>
            {shift.people.length}
          </Badge>

          <ConfirmButton
            onConfirm={() => onRemoveShift(shift)}
            disabled={busy}
            question={t('shift.removeFromPoolQuestion')}
            confirmLabel={t('shift.removeFromPool')}
            variant="ghost"
            className="min-h-8 px-1"
          >
            ×
          </ConfirmButton>
        </div>
      </div>

      {shift.people.length === 0 && !isTarget && (
        <p className="mt-3 text-sm text-cfi-muted">{t('shift.nobodyOnIt')}</p>
      )}

      <div className="mt-3 grid gap-2 sm:grid-cols-2">
        {shift.people.map((person) => (
          <PersonChip
            key={person.userId}
            person={person}
            selected={selected?.userId === person.userId}
            onSelect={() => onPick(person, shift.activeShiftId)}
            onDragStart={() => onPick(person, shift.activeShiftId)}
          >
            <button
              type="button"
              disabled={busy}
              aria-label={t('shift.takeOff')}
              onClick={(event) => {
                event.stopPropagation();
                onRemovePerson(person);
              }}
              className="shrink-0 px-1 font-bold text-cfi-muted hover:text-cfi-red"
            >
              ×
            </button>
          </PersonChip>
        ))}
      </div>

      {isTarget && (
        <PlacementPanel
          personName={selected.fullName}
          shiftName={shift.name}
          today={today}
          busy={busy}
          onCancel={onCancelPlacement}
          onSubmit={(placement) => onConfirmPlacement(shift, placement)}
        />
      )}
    </Card>
  );
}
