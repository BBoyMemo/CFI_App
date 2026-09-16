import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import AsyncSection from '../../components/ui/AsyncSection';
import Button from '../../components/ui/Button';
import { getAllTasks, getCompletedTasks, getMyTasks } from '../../api/endpoints';
import { Permissions } from '../../api/permissions';
import { useAuth } from '../../auth/useAuth';
import { useApiData } from '../../hooks/useApiData';
import CreateTaskForm from './CreateTaskForm';
import TaskListItem from './TaskListItem';

const tabClass = (active) =>
  `min-h-11 rounded px-3 text-sm font-semibold transition-colors ${
    active ? 'bg-cfi-yellow text-cfi-brown-dark' : 'bg-cfi-sunk text-cfi-brown-dark hover:bg-cfi-rule'
  }`;

export default function TasksPage() {
  const { t } = useTranslation();
  const { hasPermission } = useAuth();

  const canManage = hasPermission(Permissions.TaskManage);

  // The manager lands on the whole board; everyone else on the work that is theirs.
  const [tab, setTab] = useState(canManage ? 'all' : 'mine');
  const [creating, setCreating] = useState(false);

  const fetcher = {
    all: () => getAllTasks({ pageSize: 50 }),
    mine: () => getMyTasks({ pageSize: 50 }),
    completed: () => getCompletedTasks({ pageSize: 50 }),
  }[tab];

  const tasks = useApiData(fetcher, [tab]);
  const items = tasks.data?.items ?? [];

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-bold text-cfi-brown-dark">{t('nav.tasks')}</h1>

        {canManage && !creating && (
          <Button onClick={() => setCreating(true)}>{t('task.create')}</Button>
        )}
      </div>

      {creating && (
        <CreateTaskForm
          onCancel={() => setCreating(false)}
          onCreated={() => {
            setCreating(false);
            tasks.refetch();
          }}
        />
      )}

      <div className="mb-4 mt-4 flex flex-wrap gap-2">
        {canManage && (
          <button type="button" onClick={() => setTab('all')} className={tabClass(tab === 'all')}>
            {t('task.tabAll')}
          </button>
        )}
        <button type="button" onClick={() => setTab('mine')} className={tabClass(tab === 'mine')}>
          {t('task.tabMine')}
        </button>
        <button type="button" onClick={() => setTab('completed')} className={tabClass(tab === 'completed')}>
          {t('task.tabCompleted')}
        </button>
      </div>

      <AsyncSection {...tasks} isEmpty={items.length === 0} emptyKey="task.empty">
        <div className="flex flex-col gap-3">
          {items.map((task) => (
            <TaskListItem key={task.id} task={task} />
          ))}
        </div>
      </AsyncSection>
    </div>
  );
}
