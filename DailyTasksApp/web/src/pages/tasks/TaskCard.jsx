import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import Icon from '../../components/Icon';
import { StoredPhoto } from '../../components/Photo';
import TranslationToggle from '../../components/TranslationToggle';
import { formatDateTime, formatDay } from '../../lib/format';
import { hasTranslation, localized } from '../../lib/translate';

const PRIORITY_STYLE = {
  High: 'bg-danger text-white',
  Medium: 'bg-yellow text-ink',
  Low: 'bg-cream-dark text-muted',
};

const PRIORITY_BAR = {
  High: 'bg-danger',
  Medium: 'bg-yellow',
  Low: 'bg-cream-dark',
};

export default function TaskCard({ task, isManager, canComplete, onComplete, onEdit, onDelete }) {
  const { t, i18n } = useTranslation();
  const [showOriginal, setShowOriginal] = useState(false);
  const text = (field) => localized(task, field, i18n.language, showOriginal);
  const translated = hasTranslation(task, ['title', 'description', 'comment'], i18n.language);
  const carried = task.originalDate !== task.date;

  return (
    <article className="card relative flex overflow-hidden">
      <div className={`w-1.5 shrink-0 ${task.completed ? 'bg-green' : PRIORITY_BAR[task.priority]}`} />
      <div className="min-w-0 flex-1 p-3.5">
        <div className="flex items-start gap-2">
          {task.completed && (
            <span className="mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-green text-white">
              <Icon name="check" size={15} strokeWidth={3} />
            </span>
          )}
          <h3 className="min-w-0 flex-1 font-semibold break-words">
            {text('title')}
          </h3>
          <span className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-semibold ${PRIORITY_STYLE[task.priority]}`}>
            {t(`priority.${task.priority}`)}
          </span>
        </div>

        {task.description && <p className="mt-1 text-sm whitespace-pre-line text-muted">{text('description')}</p>}
        {task.photoIds?.length > 0 && (
          <div className="mt-2 flex flex-wrap gap-1.5">
            {task.photoIds.map((photoId) => (
              <StoredPhoto key={photoId} path={`/tasks/${task.id}/photos/${photoId}`} />
            ))}
          </div>
        )}
        {translated && (
          <TranslationToggle showOriginal={showOriginal} onToggle={() => setShowOriginal((v) => !v)} className="mt-1.5" />
        )}

        <div className="mt-2 flex flex-wrap items-center gap-1.5">
          {isManager && !task.completed && (
            <div className="order-last ml-auto flex items-center">
              <button type="button" className="icon-btn" onClick={onEdit} aria-label={t('common.edit')} title={t('common.edit')}>
                <Icon name="edit" size={18} />
              </button>
              <button
                type="button"
                className="icon-btn hover:text-danger"
                onClick={onDelete}
                aria-label={t('common.delete')}
                title={t('common.delete')}
              >
                <Icon name="trash" size={18} />
              </button>
            </div>
          )}
          {task.assignees.map((a) => (
            <span key={a.id} className="inline-flex items-center gap-1 rounded-full bg-cream px-2 py-0.5 text-xs text-brown">
              <Icon name="user" size={12} />
              {a.name}
            </span>
          ))}
          {carried && (
            <span className="inline-flex items-center gap-1 rounded-full bg-yellow/25 px-2 py-0.5 text-xs font-medium text-brown">
              <Icon name="carry" size={12} />
              {t('tasks.carriedOver', { date: formatDay(task.originalDate, { day: 'numeric', month: 'short' }) })}
            </span>
          )}
        </div>

        {task.completed && (
          <div className="mt-3 space-y-2 rounded-xl bg-green-soft p-2.5 text-sm">
            <p className="font-medium text-green">
              {t('tasks.completedBy', { name: task.completedBy?.name ?? '—', time: formatDateTime(task.completedAt) })}
            </p>
            {task.completionComment && (
              <p className="flex gap-1.5 whitespace-pre-line text-ink">
                <Icon name="comment" size={16} className="mt-0.5 shrink-0 text-muted" />
                {text('comment')}
              </p>
            )}
            {(task.hasPhoto || task.completionPhotoIds?.length > 0) && (
              <div className="flex flex-wrap gap-1.5">
                {task.hasPhoto && <StoredPhoto path={`/tasks/${task.id}/photo`} />}
                {(task.completionPhotoIds ?? []).map((photoId) => (
                  <StoredPhoto key={photoId} path={`/tasks/${task.id}/photos/${photoId}`} />
                ))}
              </div>
            )}
          </div>
        )}

        {canComplete && (
          <div className="mt-3 flex justify-end">
            <button type="button" className="btn-green" onClick={onComplete}>
              <Icon name="check" size={18} strokeWidth={3} />
              {t('tasks.complete')}
            </button>
          </div>
        )}
      </div>
    </article>
  );
}
