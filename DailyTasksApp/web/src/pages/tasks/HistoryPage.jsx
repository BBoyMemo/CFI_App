import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../../api/client';
import Icon from '../../components/Icon';
import { errorMessage, formatDay, siteDateOf, siteToday } from '../../lib/format';
import { useAuth } from '../../auth/AuthContext';
import { taskRules } from '../../lib/taskRules';
import TaskCard from './TaskCard';
import UpdateTaskModal from './UpdateTaskModal';

const PAGE_SIZE = 20;

// Completed tasks, newest first, grouped by the day they were finished. Read-only.
// Searchable by text in the title/description and by completion day.
export default function HistoryPage() {
  const { t } = useTranslation();
  const [text, setText] = useState('');
  const [q, setQ] = useState('');
  const [date, setDate] = useState('');
  const [items, setItems] = useState(null);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState(null);
  const { user, isManager } = useAuth();
  const [updating, setUpdating] = useState(null);
  // Only the newest request may update the list; slower earlier ones are dropped.
  const requestId = useRef(0);

  // Search as you type, without a request per keystroke.
  useEffect(() => {
    const timer = setTimeout(() => setQ(text.trim()), 300);
    return () => clearTimeout(timer);
  }, [text]);

  const load = useCallback(async () => {
    const id = ++requestId.current;
    try {
      setError(null);
      const result = await api.taskHistory(1, { q, date }, PAGE_SIZE);
      if (id !== requestId.current) return;
      setItems(result.items);
      setTotal(result.total);
      setPage(1);
    } catch (err) {
      if (id === requestId.current) setError(err);
    }
  }, [q, date]);

  useEffect(() => {
    load();
  }, [load]);

  async function loadMore() {
    setLoadingMore(true);
    const id = requestId.current;
    try {
      const result = await api.taskHistory(page + 1, { q, date }, PAGE_SIZE);
      if (id !== requestId.current) return;
      setItems((list) => [...list, ...result.items.filter((x) => !list.some((y) => y.id === x.id))]);
      setTotal(result.total);
      setPage(page + 1);
    } catch (err) {
      setError(err);
    } finally {
      setLoadingMore(false);
    }
  }

  const filtered = Boolean(q || date);
  const groups = [];
  for (const task of items ?? []) {
    const day = siteDateOf(task.completedAt);
    if (groups.at(-1)?.day !== day) groups.push({ day, tasks: [] });
    groups.at(-1).tasks.push(task);
  }

  return (
    <div className="space-y-4">
      <h1 className="flex items-center gap-2 text-lg font-bold text-brown">
        <Icon name="history" />
        {t('nav.history')}
        {items && <span className="font-normal text-muted">· {total}</span>}
      </h1>

      <div className="flex flex-wrap gap-2">
        <label className="relative min-w-0 flex-1 basis-56">
          <Icon name="search" size={18} className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-muted" />
          <input
            type="search"
            className="input pl-10"
            placeholder={t('history.search')}
            value={text}
            maxLength={200}
            onChange={(e) => setText(e.target.value)}
            aria-label={t('history.search')}
          />
        </label>
        <label className="relative flex items-center">
          <Icon name="calendar" size={18} className="pointer-events-none absolute left-3 text-muted" />
          <input
            type="date"
            className="input w-44 pl-10"
            value={date}
            max={siteToday()}
            onChange={(e) => setDate(e.target.value)}
            aria-label={t('history.anyDate')}
          />
        </label>
        {filtered && (
          <button
            type="button"
            className="btn-ghost"
            onClick={() => {
              setText('');
              setQ('');
              setDate('');
            }}
          >
            <Icon name="close" size={16} />
            {t('common.clear')}
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
      {!items && !error && <p className="py-10 text-center text-muted">{t('common.loading')}</p>}
      {items && items.length === 0 && (
        <p className="rounded-2xl border border-dashed border-cream-dark py-10 text-center text-sm text-muted">
          {filtered ? t('history.noResults') : t('history.empty')}
        </p>
      )}

      {groups.map((g) => (
        <section key={g.day}>
          <h2 className="mb-2 text-sm font-bold tracking-wide text-brown uppercase">
            {formatDay(g.day, { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })}
          </h2>
          <div className="space-y-2.5">
            {g.tasks.map((task) => (
              <TaskCard
                key={task.id}
                task={task}
                // History is for reading and adding cards; editing / deleting is done from Tasks.
                rules={{ ...taskRules(task, user, isManager), edit: false, delete: false }}
                onUpdate={(outcome) => setUpdating({ task, outcome })}
              />
            ))}
          </div>
        </section>
      ))}

      {items && items.length < total && (
        <button type="button" className="btn-ghost w-full" onClick={loadMore} disabled={loadingMore}>
          {t('orders.loadMore')}
        </button>
      )}
      {updating && (
        <UpdateTaskModal
          task={updating.task}
          initialOutcome={updating.outcome}
          onDone={() => {
            setUpdating(null);
            load();
          }}
          onClose={() => setUpdating(null)}
        />
      )}
    </div>
  );
}
