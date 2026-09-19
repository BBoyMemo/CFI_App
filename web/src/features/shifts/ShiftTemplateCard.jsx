import { useTranslation } from 'react-i18next';

import Badge from '../../components/ui/Badge';
import ConfirmButton from '../../components/ui/ConfirmButton';
import Icon from '../../components/ui/Icon';
import WeekdayStrip from './WeekdayStrip';
import { shiftIcon, timeRange } from './shiftModel';

/**
 * A shift as it was drawn up, waiting to be used.
 *
 * Dragging one into the pool copies it and leaves this card exactly where it is - unlike a
 * person, who moves. That is what makes it safe to delete this afterwards: the pool is
 * working to its own copy.
 */
export default function ShiftTemplateCard({ template, busy, onDragStart, onDragEnd, onAdd, onDelete }) {
  const { t } = useTranslation();

  return (
    <div
      draggable
      onDragStart={onDragStart}
      onDragEnd={onDragEnd}
      className="cursor-grab rounded border border-cfi-rule bg-white p-2 active:cursor-grabbing"
    >
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <div className="flex items-center gap-1.5">
            <span className="text-cfi-brown-dark"><Icon name={shiftIcon(template.startTime)} size={15} /></span>
            <span className="truncate text-sm font-medium text-cfi-ink">{template.name}</span>
          </div>
          <p className="mt-0.5 font-mono text-xs text-cfi-muted">
            {timeRange(template.startTime, template.endTime)}
          </p>
        </div>

        {template.inPool ? (
          <Badge tone="bg-cfi-green/15 text-cfi-green-dark">{t('shift.inPool')}</Badge>
        ) : (
          <button
            type="button"
            disabled={busy}
            onClick={onAdd}
            title={t('shift.addToPool')}
            className="min-h-8 shrink-0 rounded border border-cfi-rule bg-cfi-sunk px-2 text-sm font-semibold text-cfi-brown-dark hover:border-cfi-yellow-dark"
          >
            →
          </button>
        )}
      </div>

      <div className="mt-1.5 flex items-center justify-between gap-2">
        <WeekdayStrip days={template.weekdays} />

        <ConfirmButton
          onConfirm={onDelete}
          disabled={busy}
          question={t('shift.deleteShiftQuestion')}
          confirmLabel={t('common.delete')}
          variant="ghost"
          className="min-h-8 px-1 text-xs"
        >
          ×
        </ConfirmButton>
      </div>
    </div>
  );
}
