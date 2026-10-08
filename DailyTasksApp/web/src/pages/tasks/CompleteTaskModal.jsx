import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../../api/client';
import Icon from '../../components/Icon';
import Modal from '../../components/Modal';
import { MultiPhotoPicker } from '../../components/Photo';
import { errorMessage, fieldError } from '../../lib/format';
import { shrinkPhoto } from '../../lib/image';

// Comment and photo are both optional; one tap on the green button is enough.
export default function CompleteTaskModal({ task, onDone, onClose }) {
  const { t } = useTranslation();
  const [comment, setComment] = useState('');
  const [photos, setPhotos] = useState([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  async function submit() {
    setBusy(true);
    setError(null);
    try {
      const form = new FormData();
      form.append('comment', comment.trim());
      // Each photo goes as its own "photo" field; the server takes up to five.
      for (const file of photos) form.append('photo', await shrinkPhoto(file));
      onDone(await api.completeTask(task.id, form));
    } catch (err) {
      setError(err);
      setBusy(false);
    }
  }

  return (
    <Modal
      title={t('tasks.completeTitle')}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn-ghost flex-1" onClick={onClose}>
            {t('common.cancel')}
          </button>
          <button type="button" className="btn-green flex-1" onClick={submit} disabled={busy}>
            <Icon name="check" size={18} strokeWidth={3} />
            {t('tasks.complete')}
          </button>
        </>
      }
    >
      <p className="mb-4 font-semibold">{task.title}</p>
      <label className="label" htmlFor="comment">
        {t('tasks.comment')} <span className="font-normal text-muted">({t('common.optional')})</span>
      </label>
      <textarea
        id="comment"
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
          onAdd={(files) => setPhotos((list) => [...list, ...files].slice(0, 5))}
          onRemoveAdded={(index) => setPhotos((list) => list.filter((_, i) => i !== index))}
          max={5}
        />
        {fieldError(t, error, 'photo') && <p className="mt-1 text-sm text-danger">{fieldError(t, error, 'photo')}</p>}
      </div>
      {error && !error.fieldErrors && <p className="mt-3 text-sm text-danger">{errorMessage(t, error)}</p>}
    </Modal>
  );
}
