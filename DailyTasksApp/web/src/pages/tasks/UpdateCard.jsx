import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import Icon from '../../components/Icon';
import { StoredPhoto } from '../../components/Photo';
import TranslationToggle from '../../components/TranslationToggle';
import { formatDateTime } from '../../lib/format';
import { hasTranslation, localized } from '../../lib/translate';

// One card under a task: outcome, who, when, comment and photos.
export default function UpdateCard({ task, update }) {
  const { t, i18n } = useTranslation();
  const [showOriginal, setShowOriginal] = useState(false);
  const completed = update.outcome === 'Completed';
  const comment = localized(update, 'comment', i18n.language, showOriginal);

  return (
    <div
      className={`space-y-1.5 rounded-xl border-l-4 p-2.5 text-sm ${
        completed ? 'border-green bg-green-soft' : 'border-yellow bg-yellow/15'
      }`}
    >
      <p className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
        <span className={`inline-flex items-center gap-1 font-bold ${completed ? 'text-green' : 'text-brown'}`}>
          <Icon name={completed ? 'check' : 'wrench'} size={14} strokeWidth={completed ? 3 : 2} />
          {t(completed ? 'status.Completed' : 'status.InProgress')}
        </span>
        <span className="text-muted">
          {update.author.name} · {formatDateTime(update.createdAt)}
        </span>
      </p>
      {comment && <p className="whitespace-pre-line text-ink">{comment}</p>}
      {hasTranslation(update, ['comment'], i18n.language) && (
        <TranslationToggle showOriginal={showOriginal} onToggle={() => setShowOriginal((v) => !v)} />
      )}
      {update.photoIds.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {update.photoIds.map((photoId) => (
            <StoredPhoto key={photoId} path={`/tasks/${task.id}/photos/${photoId}`} />
          ))}
        </div>
      )}
    </div>
  );
}
