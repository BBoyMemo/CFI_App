import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';

import { getAllTasks, getPool, getWorkOrders } from '../../api/endpoints';
import { WorkOrderStatus } from '../../api/enums';
import { useApiData } from '../../hooks/useApiData';

const AWAITING_QA = WorkOrderStatus.indexOf('AwaitingQa');
const QA_FAILED = WorkOrderStatus.indexOf('QaFailed');
const CLOSED = WorkOrderStatus.indexOf('Completed');

function Tile({ to, labelKey, count, tone }) {
  const { t } = useTranslation();

  return (
    <Link
      to={to}
      className={`flex min-h-24 flex-col justify-between rounded-lg border p-3 transition-colors hover:border-cfi-yellow-dark ${tone}`}
    >
      <span className="text-3xl font-bold leading-none">{count ?? '–'}</span>
      <span className="text-sm font-medium">{t(labelKey)}</span>
    </Link>
  );
}

/**
 * The manager's five numbers, each one a way in rather than a decoration - the approved
 * prototype made every box clickable, because a count you cannot open is a count you
 * cannot act on.
 *
 * Only the totals are read here, never the rows: five list requests to render five
 * numbers would be five times the work for a screen that is glanced at.
 */
export default function FactoryOverview() {
  const open = useApiData(() => getWorkOrders({ open: true, pageSize: 1 }));
  // The pool list shows every live job; this tile counts only the ones still waiting for
  // somebody, which is the number a manager acts on.
  const pool = useApiData(() => getPool({ unclaimed: true, pageSize: 1 }));
  const awaitingQa = useApiData(() => getWorkOrders({ status: AWAITING_QA, pageSize: 1 }));
  const qaFailed = useApiData(() => getWorkOrders({ status: QA_FAILED, pageSize: 1 }));
  const closed = useApiData(() => getWorkOrders({ status: CLOSED, pageSize: 1 }));
  const tasks = useApiData(() => getAllTasks({ pageSize: 1 }));

  const total = (result) => result.data?.totalCount;

  const qaTotal = total(awaitingQa) === undefined || total(qaFailed) === undefined
    ? undefined
    : total(awaitingQa) + total(qaFailed);

  return (
    <section className="mt-2">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
        <Tile
          to="/workorders/pool"
          labelKey="dashboard.openJobs"
          count={total(open)}
          tone="border-cfi-rule bg-cfi-surface text-cfi-brown-dark"
        />
        <Tile
          to="/workorders/pool"
          labelKey="dashboard.inPool"
          count={total(pool)}
          tone="border-cfi-rule bg-cfi-surface text-cfi-brown-dark"
        />
        <Tile
          to="/workorders/history"
          labelKey="dashboard.qaAttention"
          count={qaTotal}
          tone="border-cfi-yellow-dark/40 bg-cfi-yellow/10 text-cfi-yellow-dark"
        />
        <Tile
          to="/workorders/history"
          labelKey="dashboard.closedJobs"
          count={total(closed)}
          tone="border-cfi-green/30 bg-cfi-green/10 text-cfi-green-dark"
        />
        <Tile
          to="/tasks"
          labelKey="dashboard.openTasks"
          count={total(tasks)}
          tone="border-cfi-rule bg-cfi-surface text-cfi-brown-dark"
        />
      </div>
    </section>
  );
}
