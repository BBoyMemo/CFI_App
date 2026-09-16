import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';

import Badge from '../../components/ui/Badge';
import Card from '../../components/ui/Card';
import { taskKindKey, taskKindTone, taskPriorityKey } from '../../api/enums';

export default function TaskListItem({ task }) {
  const { t } = useTranslation();

  return (
    <Link to={`/tasks/${task.id}`}>
      <Card className="transition-colors hover:border-cfi-yellow-dark">
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div>
            <p className="font-medium text-cfi-ink">{task.title}</p>
            <p className="text-sm text-cfi-muted">{task.scheduledDate}</p>
          </div>

          <div className="flex flex-wrap gap-2">
            <Badge tone={taskKindTone(task.kind)}>{t(taskKindKey(task.kind))}</Badge>
            {task.priority !== null && task.priority !== undefined && (
              <Badge>{t(taskPriorityKey(task.priority))}</Badge>
            )}
            {task.completionCount > 0 && (
              <Badge tone="bg-cfi-green/15 text-cfi-green-dark">
                {t('task.doneCount', { count: task.completionCount })}
              </Badge>
            )}
          </div>
        </div>

        {task.assignees.length > 0 && (
          <p className="mt-2 text-xs text-cfi-muted">
            {t('task.assignedTo')}: {task.assignees.map((x) => x.fullName).join(', ')}
          </p>
        )}
      </Card>
    </Link>
  );
}
