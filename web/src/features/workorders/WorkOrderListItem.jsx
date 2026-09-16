import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';

import Badge from '../../components/ui/Badge';
import Card from '../../components/ui/Card';
import { WorkOrderStatus, priorityKey, priorityTone, statusTone, workOrderStatusKey } from '../../api/enums';

export default function WorkOrderListItem({ workOrder }) {
  const { t } = useTranslation();

  const statusName = WorkOrderStatus[workOrder.status];
  const sentBackByQa = statusName === 'QaFailed';

  const location = [workOrder.unitName, workOrder.areaName, workOrder.lineName]
    .filter(Boolean)
    .join(' / ');

  const equipment = workOrder.equipmentParentName
    ? `${workOrder.equipmentParentName} · ${workOrder.equipmentName}`
    : (workOrder.equipmentName ?? workOrder.equipmentFreeText);

  return (
    <Link to={`/workorders/${workOrder.id}`}>
      <Card className={`transition-colors hover:border-cfi-yellow-dark ${sentBackByQa ? 'border-cfi-red/50' : ''}`}>
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div>
            <div className="flex items-center gap-2">
              <span className="font-mono text-sm text-cfi-muted">{workOrder.number}</span>
              <Badge tone={priorityTone(workOrder.priority)}>{t(priorityKey(workOrder.priority))}</Badge>
            </div>
            <p className="mt-1 font-medium text-cfi-ink">{equipment}</p>
            <p className="text-sm text-cfi-muted">{location}</p>
          </div>

          <Badge tone={statusTone(workOrder.status)}>{t(workOrderStatusKey(workOrder.status))}</Badge>
        </div>

        {sentBackByQa && (
          <p className="mt-2 rounded bg-cfi-red/10 px-2 py-1 text-sm font-medium text-cfi-red">
            {t('workorder.sentBackByQaNotice')}
          </p>
        )}

        {statusName === 'AwaitingQa' && (
          <p className="mt-2 text-sm text-cfi-yellow-dark">{t('workorder.awaitingQaNotice')}</p>
        )}

        <div className="mt-3 flex flex-wrap gap-x-4 gap-y-1 text-xs text-cfi-muted">
          <span>{t('workorder.reportedBy')}: {workOrder.reportedByName}</span>
          {workOrder.assignedEngineerName && (
            <span>{t('workorder.assignedEngineer')}: {workOrder.assignedEngineerName}</span>
          )}
          {workOrder.photoCount > 0 && <span>📷 {workOrder.photoCount}</span>}
        </div>
      </Card>
    </Link>
  );
}
