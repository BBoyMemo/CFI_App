import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';
import Icon from '../../components/Icon';
import { ConfirmModal } from '../../components/Modal';
import { addDays, errorMessage, formatDay, siteToday } from '../../lib/format';
import { taskRules } from '../../lib/taskRules';
import TaskCard from './TaskCard';
import TaskFormModal from './TaskFormModal';
import UpdateTaskModal from './UpdateTaskModal';

const SHIFTS = [
  { value: 'Morning', icon: 'sun', label: 'tasks.morning' },
  { value: 'Afternoon', icon: 'sunset', label: 'tasks.afternoon' },
];

export default function TasksPage() {
  const { t } = useTranslation();
  const { user, isManager } = useAuth();
  const [date, setDate] = useState(siteToday);
  const [tasks, setTasks] = useState(null);
  // Everyone a task can be given to: engineers and managers alike.
  const [people, setPeople] = useState([]);
  const [error, setError] = useState(null);
  // { type: 'create' | 'edit' | 'complete' | 'delete', task? }
  const [dialog, setDialog] = useState(null);
  const [deleteState, setDeleteState] = useState({ busy: false, error: null });

  const load = useCallback(async () => {
    try {
      setError(null);
      setTasks(await api.tasks(date));
    } catch (err) {
      setError(err);
    }
  }, [date]);

  useEffect(() => {
    setTasks(null);
    load();
  }, [load]);

  // Pick up changes made by others when the tab or app comes back into view.
  useEffect(() => {
    const onVisible = () => document.visibilityState === 'visible' && load();
    document.addEventListener('visibilitychange', onVisible);
    return () => document.removeEventListener('visibilitychange', onVisible);
  }, [load]);

  useEffect(() => {
    if (!isManager) return;
    api
      .users()
      .then(setPeople)
      .catch(() => setPeople([]));
  }, [isManager]);

  const today = siteToday();
  const close = () => {
    setDialog(null);
    setDeleteState({ busy: false, error: null });
  };

  // A saved task may have moved to another day; reloading keeps the list honest.
  const afterSave = () => {
    close();
    load();
  };

  async function confirmDelete() {
    setDeleteState({ busy: true, error: null });
    try {
      await api.deleteTask(dialog.task.id);
      setTasks((list) => list.filter((x) => x.id !== dialog.task.id));
      close();
    } catch (err) {
      setDeleteState({ busy: false, error: errorMessage(t, err) });
    }
  }

  const done = tasks?.filter((x) => x.completed).length ?? 0;

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-2">
        <button type="button" className="icon-btn" onClick={() => setDate(addDays(date, -1))} aria-label={t('common.previousDay')}>
          <Icon name="left" />
        </button>
        {/* Today gets a soft box so it stands out while moving between days. */}
        <label
          className={`relative flex-1 cursor-pointer rounded-2xl border-[1.5px] py-1 text-center ${
            date === today ? 'border-yellow bg-yellow/20' : 'border-transparent'
          }`}
        >
          <span className="block text-lg font-bold text-brown capitalize">
            {formatDay(date, { weekday: 'long', day: 'numeric', month: 'long' })}
          </span>
          {(date === today || tasks?.length > 0) && (
            <span className="block text-xs text-muted">
              {date === today && <span className="font-bold text-brown">{t('common.today')}</span>}
              {date === today && tasks?.length > 0 && ' · '}
              {tasks?.length > 0 && t('tasks.progress', { done, total: tasks.length })}
            </span>
          )}
          <input
            type="date"
            value={date}
            onChange={(e) => e.target.value && setDate(e.target.value)}
            className="absolute inset-0 cursor-pointer opacity-0"
            aria-label={t('tasks.date')}
          />
        </label>
        <button type="button" className="icon-btn" onClick={() => setDate(addDays(date, 1))} aria-label={t('common.nextDay')}>
          <Icon name="right" />
        </button>
        {date !== today && (
          <button type="button" className="btn-ghost px-3 py-1.5" onClick={() => setDate(today)}>
            {t('common.today')}
          </button>
        )}
      </div>

      {error && (
        <div className="card flex items-center gap-3 p-4 text-danger">
          <Icon name="alert" />
          <span className="flex-1 text-sm">{errorMessage(t, error)}</span>
          <button type="button" className="btn-ghost" onClick={load}>
            {t('common.retry')}
          </button>
        </div>
      )}

      {!tasks && !error && <p className="py-10 text-center text-muted">{t('common.loading')}</p>}

      {tasks &&
        SHIFTS.map((shift) => {
          const list = tasks.filter((x) => x.shift === shift.value);
          return (
            <section key={shift.value}>
              <h2 className="mb-2 flex items-center gap-2 text-sm font-bold tracking-wide text-brown uppercase">
                <Icon name={shift.icon} size={18} className="text-yellow-dark" />
                {t(shift.label)}
                <span className="font-normal text-muted">· {list.length}</span>
              </h2>
              {list.length === 0 ? (
                <p className="rounded-2xl border border-dashed border-cream-dark py-5 text-center text-sm text-muted">
                  {t('tasks.empty')}
                </p>
              ) : (
                <div className="space-y-2.5">
                  {list.map((task) => (
                    <TaskCard
                      key={task.id}
                      task={task}
                      rules={taskRules(task, user, isManager)}
                      onUpdate={(outcome) => setDialog({ type: 'update', task, outcome })}
                      onEdit={() => setDialog({ type: 'edit', task })}
                      onDelete={() => setDialog({ type: 'delete', task })}
                    />
                  ))}
                </div>
              )}
            </section>
          );
        })}

      {isManager && (
        <button
          type="button"
          onClick={() => setDialog({ type: 'create' })}
          className="fixed right-5 bottom-24 z-20 flex h-14 w-14 items-center justify-center rounded-full bg-yellow text-ink shadow-lg hover:bg-yellow-dark sm:bottom-8"
          aria-label={t('tasks.new')}
          title={t('tasks.new')}
        >
          <Icon name="plus" size={28} strokeWidth={2.5} />
        </button>
      )}

      {(dialog?.type === 'create' || dialog?.type === 'edit') && (
        <TaskFormModal
          task={dialog.task}
          defaultDate={date < today ? today : date}
          engineers={people}
          onSaved={afterSave}
          onClose={close}
        />
      )}
      {dialog?.type === 'update' && (
        <UpdateTaskModal
          task={dialog.task}
          initialOutcome={dialog.outcome}
          onDone={(updated) => {
            setTasks((list) => list.map((x) => (x.id === updated.id ? updated : x)));
            close();
          }}
          onClose={close}
        />
      )}
      {dialog?.type === 'delete' && (
        <ConfirmModal
          message={t('tasks.deleteConfirm')}
          onConfirm={confirmDelete}
          onClose={close}
          busy={deleteState.busy}
          error={deleteState.error}
        />
      )}
    </div>
  );
}
