import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import ErrorBanner from '../../components/ui/ErrorBanner';
import { describeApiError } from '../../api/apiClient';
import { createTask, getTeam } from '../../api/endpoints';
import { TaskKind, TaskPriority } from '../../api/enums';
import { useApiData } from '../../hooks/useApiData';

const controlClass = 'min-h-11 w-full rounded border border-cfi-rule bg-white px-3 text-sm';

const today = () => new Date().toISOString().slice(0, 10);

export default function CreateTaskForm({ onCreated, onCancel }) {
  const { t } = useTranslation();

  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [kind, setKind] = useState(0);
  const [priority, setPriority] = useState('');
  const [scheduledDate, setScheduledDate] = useState(today);
  const [assignedUserIds, setAssignedUserIds] = useState([]);
  const [error, setError] = useState(null);
  const [submitting, setSubmitting] = useState(false);

  // Only the manager's own people can be assigned, which is exactly what /team returns.
  const { data: teamPage } = useApiData(() => getTeam({ pageSize: 100 }));
  const team = teamPage?.items ?? [];

  const toggleAssignee = (id) =>
    setAssignedUserIds((current) =>
      current.includes(id) ? current.filter((x) => x !== id) : [...current, id]);

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      await createTask({
        title,
        description: description || null,
        kind: Number(kind),
        scheduledDate,
        priority: priority === '' ? null : Number(priority),
        assignedUserIds,
      });
      onCreated();
    } catch (submitError) {
      setError(describeApiError(submitError));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Card className="mt-4">
      <form onSubmit={handleSubmit} className="flex flex-col gap-3">
        <label className="flex flex-col gap-1 text-sm">
          {t('task.title')}
          <input required value={title} onChange={(e) => setTitle(e.target.value)} className={controlClass} />
        </label>

        <label className="flex flex-col gap-1 text-sm">
          {t('task.description')} <span className="text-cfi-muted">({t('common.optional')})</span>
          <textarea
            rows={3}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            className="w-full rounded border border-cfi-rule bg-white p-3 text-sm"
          />
        </label>

        <div className="flex flex-wrap gap-3">
          <label className="flex flex-1 flex-col gap-1 text-sm">
            {t('task.kind')}
            <select value={kind} onChange={(e) => setKind(e.target.value)} className={controlClass}>
              {TaskKind.map((name, value) => (
                <option key={name} value={value}>{t(`task.kind_${name}`)}</option>
              ))}
            </select>
          </label>

          <label className="flex flex-1 flex-col gap-1 text-sm">
            {t('task.priority')} <span className="text-cfi-muted">({t('common.optional')})</span>
            <select value={priority} onChange={(e) => setPriority(e.target.value)} className={controlClass}>
              <option value="">—</option>
              {TaskPriority.map((name, value) => (
                <option key={name} value={value}>{t(`task.priority_${name}`)}</option>
              ))}
            </select>
          </label>

          <label className="flex flex-1 flex-col gap-1 text-sm">
            {t('task.scheduledDate')}
            <input
              required
              type="date"
              value={scheduledDate}
              onChange={(e) => setScheduledDate(e.target.value)}
              className={controlClass}
            />
          </label>
        </div>

        <div>
          <p className="mb-1 text-sm">{t('task.assignTo')}</p>
          <div className="flex flex-wrap gap-2">
            {team.map((member) => (
              <label
                key={member.id}
                className={`flex items-center gap-1 rounded border px-2 py-1 text-sm ${
                  assignedUserIds.includes(member.id) ? 'border-cfi-yellow-dark bg-cfi-yellow/20' : 'border-cfi-rule'
                }`}
              >
                <input
                  type="checkbox"
                  checked={assignedUserIds.includes(member.id)}
                  onChange={() => toggleAssignee(member.id)}
                />
                {member.fullName}
              </label>
            ))}
          </div>
        </div>

        <ErrorBanner error={error} />

        <div className="flex gap-2">
          <Button type="submit" disabled={submitting || !title || assignedUserIds.length === 0}>
            {t('task.create')}
          </Button>
          <Button type="button" variant="secondary" onClick={onCancel}>
            {t('common.cancel')}
          </Button>
        </div>
      </form>
    </Card>
  );
}
