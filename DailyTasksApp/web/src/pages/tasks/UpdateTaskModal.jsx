import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../../api/client';
import Icon from '../../components/Icon';
import Modal from '../../components/Modal';
import { MultiPhotoPicker } from '../../components/Photo';
import { errorMessage, fieldError } from '../../lib/format';
import { shrinkPhoto } from '../../lib/image';
import { localized } from '../../lib/translate';

const MAX_PHOTOS = 5;
const OUTCOMES = [
  { value: 'InProgress', icon: 'wrench' },
  { value: 'Completed', icon: 'check' },
];

// Adds a card to a task: the result (still in progress / completed), an optional comment and photos.
export default function UpdateTaskModal({ task, initialOutcome = 'Completed', onDone, onClose }) {
  const { t, i18n } = useTranslation();
  const [outcome, setOutcome] = useState(initialOutcome);
  const [comment, setComment] = useState('');
  const [photos, setPhotos] = useState([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const takingBack = task.status === 'Completed' && outcome === 'InProgress';

  async function submit() {
    setBusy(true);
    setError(null);
    try {
      const form = new FormData();
      form.append('outcome', outcome);
      form.append('comment', comment.trim());
      // Each photo goes as its own "photo" field; the server takes up to five.
      for (const file of photos) form.append('photo', await shrinkPhoto(file));
      onDone(await api.addTaskUpdate(task.id, form));
    } catch (err) {
      setError(err);
      setBusy(false);
    }
  }

  return (
    <Modal
      title={t('tasks.updateTitle')}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn-ghost flex-1" onClick={onClose}>
            {t('common.cancel')}
          </button>
          <button
            type="button"
            className={`${outcome === 'Completed' ? 'btn-green' : 'btn-primary'} flex-1`}
            onClick={submit}
            disabled={busy}
          >
            <Icon name={outcome === 'Completed' ? 'check' : 'wrench'} size={18} strokeWidth={3} />
            {t('common.save')}
          </button>
        </>
      }
    >
      <p className="mb-4 font-semibold">{localized(task, 'title', i18n.language)}</p>

      <span className="label">{t('tasks.outcome')}</span>
      <div className="mb-3 grid grid-cols-2 gap-1 rounded-xl bg-cream-dark p-1">
        {OUTCOMES.map((o) => (
          <button
            key={o.value}
            type="button"
            onClick={() => setOutcome(o.value)}
            aria-pressed={outcome === o.value}
            className={`flex items-center justify-center gap-2 rounded-lg py-2 text-sm font-semibold ${
              outcome === o.value ? 'bg-white text-brown shadow-sm' : 'text-muted'
            }`}
          >
            <Icon name={o.icon} size={16} />
            {t(`status.${o.value}`)}
          </button>
        ))}
      </div>
      {takingBack && <p className="mb-3 rounded-lg bg-yellow/20 px-3 py-2 text-sm text-brown">{t('tasks.reopenHint')}</p>}

      <label className="label" htmlFor="update-comment">
        {t('tasks.comment')} <span className="font-normal text-muted">({t('common.optional')})</span>
      </label>
      <textarea
        id="update-comment"
        className="input mb-1 min-h-20"
        value={comment}
        maxLength={2000}
        onChange={(e) => setComment(e.target.value)}
      />
      {fieldError(t, error, 'comment') && <p className="text-sm text-danger">{fieldError(t, error, 'comment')}</p>}
      <div className="mt-3">
        <MultiPhotoPicker
          stored={[]}
          added={photos}
          onRemoveStored={() => {}}
          onAdd={(files) => setPhotos((list) => [...list, ...files].slice(0, MAX_PHOTOS))}
          onRemoveAdded={(index) => setPhotos((list) => list.filter((_, i) => i !== index))}
          max={MAX_PHOTOS}
        />
        {fieldError(t, error, 'photo') && <p className="mt-1 text-sm text-danger">{fieldError(t, error, 'photo')}</p>}
      </div>
      {error && !error.fieldErrors && <p className="mt-3 text-sm text-danger">{errorMessage(t, error)}</p>}
    </Modal>
  );
}
