import { useTranslation } from 'react-i18next';

import Icon from '../../components/ui/Icon';
import { shortDate, sourceName } from './shiftModel';

/**
 * One name, wherever it appears - in the team column or on a shift's crew.
 *
 * Tapping is the real gesture here and dragging is the shortcut on top of it. A factory
 * tablet has no drag, and the HTML5 drag events simply never fire on touch, so a screen
 * that only dragged would be a screen half the site could not use.
 *
 * Both handlers stop the event: the chip sits inside a shift card that is itself draggable
 * and tappable, and without that a drag of a name would also start dragging the whole shift.
 */
export default function PersonChip({ person, selected = false, detail, onSelect, onDragStart, onDragEnd, children }) {
  const { t } = useTranslation();

  const isCover = sourceName(person.source) === 'Cover';

  const tone = isCover
    ? 'border-dashed border-cfi-yellow-dark bg-cfi-yellow/10'
    : 'border-cfi-rule bg-white';

  let when = null;
  if (isCover && person.fromDate) {
    when = person.fromDate === person.toDate
      ? shortDate(person.fromDate)
      : `${shortDate(person.fromDate)} → ${shortDate(person.toDate)}`;
  } else if (person.fromDate) {
    when = t('shift.from', { date: shortDate(person.fromDate) });
  } else if (person.toDate) {
    when = t('shift.until', { date: shortDate(person.toDate) });
  }

  return (
    <div
      draggable
      onDragStart={(event) => {
        event.stopPropagation();
        onDragStart?.(event);
      }}
      onDragEnd={onDragEnd}
      aria-pressed={selected}
      onClick={(event) => {
        event.stopPropagation();
        onSelect?.();
      }}
      onKeyDown={(event) => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          event.stopPropagation();
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

      {detail && <p className="mt-0.5 truncate text-xs text-cfi-muted">{detail}</p>}

      {when && (
        <div className={`mt-0.5 flex items-center gap-1 text-xs ${isCover ? 'text-cfi-yellow-dark' : 'text-cfi-muted'}`}>
          <Icon name="calendar" size={12} />
          <span>{when}</span>
          {isCover && <span className="font-semibold">· {t('shift.cover')}</span>}
        </div>
      )}
    </div>
  );
}
