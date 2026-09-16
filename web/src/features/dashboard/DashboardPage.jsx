import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';

import { getMyJobs, getPool, getReportedByMe, getWorkOrders } from '../../api/endpoints';
import { WorkOrderStatus } from '../../api/enums';
import { Permissions } from '../../api/permissions';
import { useAuth } from '../../auth/useAuth';
import { useApiData } from '../../hooks/useApiData';
import WorkOrderList from '../workorders/WorkOrderList';
import FactoryOverview from './FactoryOverview';
import ReportHero from './ReportHero';

function Section({ titleKey, to, children }) {
  const { t } = useTranslation();
  return (
    <section className="mt-6">
      <div className="mb-3 flex items-center justify-between">
        <h2 className="text-lg font-semibold text-cfi-brown-dark">{t(titleKey)}</h2>
        {to && (
          <Link to={to} className="text-sm font-semibold text-cfi-brown-dark underline">
            →
          </Link>
        )}
      </div>
      {children}
    </section>
  );
}

/**
 * The operator opens the app to report something, so that card comes first and nothing
 * stands between them and it.
 *
 * Underneath: what they themselves reported and is still open. They deliberately see only
 * this much - whether an engineer has taken it and where it has got to - not the pool,
 * not other people's jobs, not who is free. There is no separate screen for it because
 * this is the only place it belongs.
 */
function OperatorHome() {
  const mine = useApiData(() => getReportedByMe({ pageSize: 10 }));

  const open = (mine.data?.items ?? []).filter(
    (x) => !['Completed', 'Rejected'].includes(WorkOrderStatus[x.status]),
  );

  return (
    <div>
      <ReportHero />

      <Section titleKey="dashboard.myReports">
        <WorkOrderList {...mine} data={{ items: open }} emptyKey="dashboard.myReportsEmpty" />
      </Section>
    </div>
  );
}

/**
 * Engineer and Maintenance Manager home, in the prototype's order: their own jobs first,
 * then the report card, then the pool. Jobs waiting on a swab test or sent back by QA are
 * part of "my jobs" rather than a list to remember to check.
 */
function EngineerHome({ showOverview }) {
  const myJobs = useApiData(() => getMyJobs({ pageSize: 5 }));
  const pool = useApiData(() => getPool({ pageSize: 5 }));
  const waitingParts = useApiData(() => getWorkOrders({ waitingParts: true, pageSize: 10 }));

  return (
    <div>
      {/* The manager gets the factory's five numbers above their own work - they repair
          machines too, so this is one screen rather than a separate manager panel. */}
      {showOverview && <FactoryOverview />}

      <Section titleKey="dashboard.myActiveJobs" to="/workorders/mine">
        <WorkOrderList {...myJobs} emptyKey="workorder.myJobsEmpty" />
      </Section>

      <div className="mt-6">
        <ReportHero />
      </div>

      <Section titleKey="dashboard.poolPreview" to="/workorders/pool">
        <WorkOrderList {...pool} emptyKey="workorder.poolEmpty" />
      </Section>

      {showOverview && (
        <Section titleKey="dashboard.waitingParts">
          <WorkOrderList {...waitingParts} emptyKey="dashboard.waitingPartsEmpty" />
        </Section>
      )}
    </div>
  );
}

/**
 * The Production Manager does not claim jobs and does not search the archive - what they
 * need is what is broken on the floor right now, which is why it is the dashboard itself
 * rather than a screen to navigate to.
 */
function ProductionHome() {
  const open = useApiData(() => getWorkOrders({ open: true, pageSize: 25 }));

  // Told once, watched continuously: the reporter gets a single notice that a part is on
  // order, while a manager needs the standing list of everything held up (notes 03, §3).
  const waiting = useApiData(() => getWorkOrders({ waitingParts: true, pageSize: 25 }));

  return (
    <div>
      <ReportHero />

      <Section titleKey="dashboard.openBreakdowns">
        <WorkOrderList {...open} emptyKey="dashboard.openBreakdownsEmpty" />
      </Section>

      <Section titleKey="dashboard.waitingParts">
        <WorkOrderList {...waiting} emptyKey="dashboard.waitingPartsEmpty" />
      </Section>
    </div>
  );
}

/** QA's queue: the intrusive jobs waiting on a swab test result. */
function QaHome() {
  const awaiting = useApiData(() =>
    getWorkOrders({ status: WorkOrderStatus.indexOf('AwaitingQa'), pageSize: 25 }),
  );

  return (
    <div>
      <Section titleKey="dashboard.awaitingQa">
        <WorkOrderList {...awaiting} emptyKey="dashboard.awaitingQaEmpty" />
      </Section>

      <div className="mt-6">
        <ReportHero />
      </div>
    </div>
  );
}

export default function DashboardPage() {
  const { hasPermission } = useAuth();

  // Read as a chain of "what is this person here to do", most specific first: repair work
  // beats QA checks beats watching the floor, and anything left is an operator.
  if (hasPermission(Permissions.WorkOrderClaim)) {
    return <EngineerHome showOverview={hasPermission(Permissions.TaskManage)} />;
  }

  if (hasPermission(Permissions.QaCheck)) return <QaHome />;
  if (hasPermission(Permissions.WorkOrderViewAll)) return <ProductionHome />;

  return <OperatorHome />;
}
