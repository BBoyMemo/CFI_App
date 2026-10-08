import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../../api/client';
import Icon from '../../components/Icon';
import Modal from '../../components/Modal';
import { MultiPhotoPicker } from '../../components/Photo';
import { errorMessage, fieldError, siteToday } from '../../lib/format';
import { shrinkPhoto } from '../../lib/image';

const MAX_PHOTOS = 5;

const PRIORITIES = ['Low', 'Medium', 'High'];
const SHIFTS = [
  { value: 'Morning', icon: 'sun' },
  { value: 'Afternoon', icon: 'sunset' },
];

// Create and edit in one form. Editing also moves a task to another date.
export default function TaskFormModal({ task, defaultDate, engineers, onSaved, onClose }) {
  const { t } = useTranslation();
  const [form, setForm] = useState(() => ({
    title: task?.title ?? '',
    description: task?.description ?? '',
    date: task?.date ?? defaultDate,
    priority: task?.priority ?? 'Medium',
    shift: task?.shift ?? 'Morning',
    assigneeIds: task?.assignees.map((a) => a.id) ?? [],
  }));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  // Photos: the stored ones (minus any removed here) and newly picked files.
  const [storedIds, setStoredIds] = useState(() => task?.photoIds ?? []);
  const [added, setAdded] = useState([]);

  const set = (key, value) => setForm((f) => ({ ...f, [key]: value }));
  const toggleAssignee = (id) =>
    set('assigneeIds', form.assigneeIds.includes(id) ? form.assigneeIds.filter((x) => x !== id) : [...form.assigneeIds, id]);

  async function submit(e) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const payload = { ...form, title: form.title.trim(), description: form.description.trim() || null };
      const saved = task ? await api.updateTask(task.id, payload) : await api.createTask(payload);
      // The task itself is saved; photos follow one by one. A failed photo does not undo the task.
      let failed = null;
      for (const photoId of (task?.photoIds ?? []).filter((id) => !storedIds.includes(id))) {
        await api.deleteTaskPhoto(saved.id, photoId).catch((err) => (failed ??= err));
      }
      for (const file of added) {
        const photoForm = new FormData();
        photoForm.append('photo', await shrinkPhoto(file));
        await api.addTaskPhoto(saved.id, photoForm).catch((err) => (failed ??= err));
      }
      if (failed) window.alert(`${t('tasks.photos')}: ${errorMessage(t, failed)}`);
      onSaved(saved);
    } catch (err) {
      setError(err);
      setBusy(false);
    }
  }

  const valid = form.title.trim() && form.date && form.assigneeIds.length > 0;
  const generalError = error && !error.fieldErrors ? errorMessage(t, error) : null;

  return (
    <Modal
      title={task ? t('tasks.edit') : t('tasks.new')}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn-ghost flex-1" onClick={onClose}>
            {t('common.cancel')}
          </button>
          <button type="submit" form="task-form" className="btn-primary flex-1" disabled={busy || !valid}>
            {t('common.save')}
          </button>
        </>
      }
    >
      <form id="task-form" onSubmit={submit} className="space-y-4">
        <Field label={t('tasks.title')} error={fieldError(t, error, 'title')}>
          <input className="input" value={form.title} maxLength={200} onChange={(e) => set('title', e.target.value)} autoFocus />
        </Field>

        <Field label={t('tasks.description')} optional error={fieldError(t, error, 'description')}>
          <textarea
            className="input min-h-20"
            value={form.description}
            maxLength={2000}
            onChange={(e) => set('description', e.target.value)}
          />
        </Field>

        <Field label={t('tasks.photos')} optional>
          <MultiPhotoPicker
            stored={task ? storedIds.map((id) => ({ id, path: `/tasks/${task.id}/photos/${id}` })) : []}
            added={added}
            onRemoveStored={(id) => setStoredIds((ids) => ids.filter((x) => x !== id))}
            onAdd={(files) => setAdded((list) => [...list, ...files].slice(0, MAX_PHOTOS - storedIds.length))}
            onRemoveAdded={(index) => setAdded((list) => list.filter((_, i) => i !== index))}
            max={MAX_PHOTOS}
          />
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label={t('tasks.date')} error={fieldError(t, error, 'date')}>
            <input
              type="date"
              className="input"
              value={form.date}
              min={siteToday()}
              onChange={(e) => set('date', e.target.value)}
            />
          </Field>
          <Field label={t('tasks.shift')} error={fieldError(t, error, 'shift')}>
            <div className="grid grid-cols-2 gap-1 rounded-xl bg-cream-dark p-1">
              {SHIFTS.map((s) => (
                <button
                  key={s.value}
                  type="button"
                  onClick={() => set('shift', s.value)}
                  title={t(`shift.${s.value}`)}
                  aria-pressed={form.shift === s.value}
                  className={`flex items-center justify-center rounded-lg py-2 ${
                    form.shift === s.value ? 'bg-white text-brown shadow-sm' : 'text-muted'
                  }`}
                >
                  <Icon name={s.icon} size={20} />
                </button>
              ))}
            </div>
          </Field>
        </div>

        <Field label={t('tasks.priority')} error={fieldError(t, error, 'priority')}>
          <div className="grid grid-cols-3 gap-1 rounded-xl bg-cream-dark p-1">
            {PRIORITIES.map((p) => (
              <button
                key={p}
                type="button"
                onClick={() => set('priority', p)}
                aria-pressed={form.priority === p}
                className={`rounded-lg py-2 text-sm font-semibold ${
                  form.priority === p ? 'bg-white text-brown shadow-sm' : 'text-muted'
                }`}
              >
                {t(`priority.${p}`)}
              </button>
            ))}
          </div>
        </Field>

        <Field label={t('tasks.assignees')} error={fieldError(t, error, 'assigneeIds')}>
          {engineers.length === 0 ? (
            <p className="text-sm text-muted">{t('tasks.noEngineers')}</p>
          ) : (
            <div className="flex flex-wrap gap-2">
              {engineers.map((u) => {
                const on = form.assigneeIds.includes(u.id);
                return (
                  <button
                    key={u.id}
                    type="button"
                    onClick={() => toggleAssignee(u.id)}
                    aria-pressed={on}
                    className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1.5 text-sm font-medium ring-1 ${
                      on ? 'bg-brown text-cream ring-brown' : 'bg-white text-brown ring-cream-dark'
                    }`}
                  >
                    <Icon name={on ? 'check' : 'user'} size={14} strokeWidth={on ? 3 : 2} />
                    {u.name}
                  </button>
                );
              })}
            </div>
          )}
        </Field>

        {generalError && <p className="text-sm text-danger">{generalError}</p>}
      </form>
    </Modal>
  );
}

function Field({ label, optional, error, children }) {
  const { t } = useTranslation();
  return (
    <div>
      <span className="label">
        {label}
        {optional && <span className="ml-1 font-normal text-muted">({t('common.optional')})</span>}
      </span>
      {children}
      {error && <p className="mt-1 text-sm text-danger">{error}</p>}
    </div>
  );
}
