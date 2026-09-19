import { useTranslation } from 'react-i18next';

import Icon from '../../components/ui/Icon';
import { sourceName } from './shiftModel';

/**
 * One name, wherever it appears - in the team column or on a shift in the pool.
 *
 * Tapping is the real gesture here and dragging is the shortcut on top of it. A factory
 * tablet has no drag, and the HTML5 drag events simply never fire on touch, so a screen
 * that only dragged would be a screen half the site could not use.
 */
export default function PersonChip({ person, selected = false, onSelect, onDragStart, onDragEnd, children }) {
  const { t } = useTranslation();

  const source = sourceName(person.source);
  const isCover = source === 'Cover';

  const tone = isCover
    ? 'border-dashed border-cfi-yellow-dark bg-cfi-yellow/10'
    : 'border-cfi-rule bg-white';

  return (
    <div
      draggable
      onDragStart={onDragStart}
      onDragEnd={onDragEnd}
      aria-pressed={selected}
      onClick={onSelect}
      onKeyDown={(event) => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          onSelect?.();
        }
      }}
      role="button"
      tabIndex={0}
      className={`cursor-grab rounded border px-2 py-1.5 text-left transition-colors active:cursor-grabbing ${tone} ${
        selected ? 'ring-2 ring-cfi-yellow-dark' : 'hover:border-cfi-yellow-dark'
      }`}
    >
      <div className="flex items-center justify-between gap-2">
        <span className="truncate text-sm font-medium text-cfi-ink">{person.fullName}</span>
        {children}
      </div>

      {isCover && person.coverFrom && (
        <div className="mt-1 flex items-center gap-1 text-xs text-cfi-yellow-dark">
          <Icon name="calendar" size={13} />
          <span>
            {person.coverFrom === person.coverTo
              ? person.coverFrom.slice(5)
              : `${person.coverFrom.slice(5)} → ${person.coverTo.slice(5)}`}
          </span>
          <span className="font-semibold">· {t('shift.cover')}</span>
        </div>
      )}
    </div>
  );
}
