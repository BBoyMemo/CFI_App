import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import Icon from '../../components/Icon';
import { StoredPhoto } from '../../components/Photo';
import TranslationToggle from '../../components/TranslationToggle';
import { formatDay } from '../../lib/format';
import { hasTranslation, localized } from '../../lib/translate';
import UpdateCard from './UpdateCard';

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

// The task as given (main card) with the cards people added under it. `rules` comes from
// taskRules(); onUpdate(outcome) opens the add-card form.
export default function TaskCard({ task, rules, onUpdate, onEdit, onDelete }) {
  const { t, i18n } = useTranslation();
  const [showOriginal, setShowOriginal] = useState(false);
  const [showAll, setShowAll] = useState(false);
  const text = (field) => localized(task, field, i18n.language, showOriginal);
  const translated = hasTranslation(task, ['title', 'description'], i18n.language);
  const carried = task.originalDate !== task.date;
  const updates = showAll ? task.updates : task.updates.slice(-1);

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
          <h3 className="min-w-0 flex-1 font-semibold break-words">{text('title')}</h3>
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
          {(rules.edit || rules.delete) && (
            <div className="order-last ml-auto flex items-center">
              {rules.edit && (
                <button type="button" className="icon-btn" onClick={onEdit} aria-label={t('common.edit')} title={t('common.edit')}>
                  <Icon name="edit" size={18} />
                </button>
              )}
              {rules.delete && (
                <button
                  type="button"
                  className="icon-btn hover:text-danger"
                  onClick={onDelete}
                  aria-label={t('common.delete')}
                  title={t('common.delete')}
                >
                  <Icon name="trash" size={18} />
                </button>
              )}
            </div>
          )}
          {task.status === 'InProgress' && (
            <span className="inline-flex items-center gap-1 rounded-full bg-yellow/25 px-2 py-0.5 text-xs font-bold text-brown">
              <Icon name="wrench" size={12} />
              {t('status.InProgress')}
            </span>
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

        {task.updates.length > 0 && (
          <div className="mt-3 space-y-2">
            {updates.map((update) => (
              <UpdateCard key={update.id} task={task} update={update} />
            ))}
            {task.updates.length > 1 && (
              <button
                type="button"
                className="text-xs font-semibold text-muted hover:text-brown"
                onClick={() => setShowAll((v) => !v)}
              >
                {showAll ? t('common.close') : t('tasks.showAll', { count: task.updates.length })}
              </button>
            )}
          </div>
        )}

        {onUpdate && rules.work && (
          <div className="mt-3 flex justify-end gap-2">
            <button type="button" className="btn-primary" onClick={() => onUpdate('InProgress')}>
              <Icon name="wrench" size={18} />
              {t('status.InProgress')}
            </button>
            <button type="button" className="btn-green" onClick={() => onUpdate('Completed')}>
              <Icon name="check" size={18} strokeWidth={3} />
              {t('tasks.complete')}
            </button>
          </div>
        )}
        {onUpdate && rules.followUp && (
          <div className="mt-3 flex justify-end">
            <button type="button" className="btn-ghost" onClick={() => onUpdate('Completed')}>
              <Icon name="plus" size={18} />
              {t('tasks.addUpdate')}
            </button>
          </div>
        )}
      </div>
    </article>
  );
}
