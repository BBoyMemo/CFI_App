import { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation, useParams } from 'react-router-dom';

import AuthenticatedImage from '../../components/ui/AuthenticatedImage';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import ErrorBanner from '../../components/ui/ErrorBanner';
import { describeApiError } from '../../api/apiClient';
import { priorityKey, priorityTone, statusTone, workOrderStatusKey } from '../../api/enums';
import {
  assignWorkOrder, rejectWorkOrder, claimWorkOrder, closeWorkOrder, getTeam, getWorkOrder,
  markWaitingParts, notifyReporter, resumeWork, signOffProduction, signOffQa,
  signOffReporter, startWork, submitQaResult,
} from '../../api/endpoints';
import { Permissions } from '../../api/permissions';
import { useAuth } from '../../auth/useAuth';
import { useApiData } from '../../hooks/useApiData';
import CloseJobForm from './CloseJobForm';
import QaResultForm from './QaResultForm';
import { formatDuration } from './duration';

const ACTIVE_STATUSES = new Set(['Accepted', 'InProgress', 'WaitingParts']);
const STATUS_NAMES = ['New', 'Accepted', 'InProgress', 'WaitingParts', 'AwaitingQa', 'QaFailed', 'Completed', 'Rejected'];

function InfoRow({ label, children }) {
  if (children === null || children === undefined || children === '') return null;
  return (
    <div className="flex justify-between gap-4 border-b border-cfi-rule/60 py-2 text-sm last:border-b-0">
      <dt className="text-cfi-muted">{label}</dt>
      <dd className="text-right text-cfi-ink">{children}</dd>
    </div>
  );
}

export default function WorkOrderDetailPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const location = useLocation();
  const { me, hasPermission } = useAuth();

  const { status, data: detail, error, refetch } = useApiData(() => getWorkOrder(id), [id]);
  const [actionError, setActionError] = useState(null);
  const [busy, setBusy] = useState(false);
  const [rejectReason, setRejectReason] = useState('');
  const [rejecting, setRejecting] = useState(false);

  // Only fetched for someone who can actually hand a job to somebody else.
  const canAssign = hasPermission(Permissions.WorkOrderAssign);
  const { data: teamPage } = useApiData(
    () => (canAssign ? getTeam({ pageSize: 100 }) : Promise.resolve({ data: { items: [] } })),
    [canAssign],
  );

  const runAction = useCallback(
    async (action) => {
      setActionError(null);
      setBusy(true);
      try {
        await action();
        refetch();
      } catch (error_) {
        setActionError(describeApiError(error_));
      } finally {
        setBusy(false);
      }
    },
    [refetch],
  );

  if (status === 'loading') return <p className="text-cfi-muted">{t('common.loading')}</p>;
  if (status === 'error') return <ErrorBanner error={error} />;

  const statusName = STATUS_NAMES[detail.status];
  const isAssignedEngineer = detail.assignedEngineerId != null && detail.assignedEngineerId === me?.id;
  const canClaim = statusName === 'New' && hasPermission(Permissions.WorkOrderClaim);
  const canWorkOnIt = isAssignedEngineer && hasPermission(Permissions.WorkOrderClaim);
  const canClose =
    isAssignedEngineer &&
    hasPermission(Permissions.WorkOrderClose) &&
    ['Accepted', 'InProgress', 'WaitingParts', 'QaFailed'].includes(statusName);
  const canSubmitQa = statusName === 'AwaitingQa' && hasPermission(Permissions.QaCheck);
  const hasProductionSignOff = detail.signOffs.some((s) => s.kind === 'Production' || s.kind === 0);
  const hasQaSignOff = detail.signOffs.some((s) => s.kind === 'Qa' || s.kind === 1);
  const canSignOffProduction =
    statusName === 'Completed' && !hasProductionSignOff && hasPermission(Permissions.ProductionSignOff);
  const canSignOffQa = statusName === 'Completed' && !hasQaSignOff && hasPermission(Permissions.QaSignOff);
  const hasReporterSignOff = detail.signOffs.some((s) => s.kind === 'Reporter' || s.kind === 2);
  const canAcceptRepair =
    statusName === 'Completed' && !hasReporterSignOff && detail.reportedByUserId === me?.id;
  const canReject = hasPermission(Permissions.WorkOrderReject) &&
    !['Completed', 'Rejected', 'AwaitingQa', 'QaFailed'].includes(statusName);
  const engineers = (teamPage?.items ?? []).filter((x) => x.id !== detail.assignedEngineerId);

  return (
    <div className="mx-auto max-w-3xl">
      {location.state?.flash && (
        <div className="mb-4 rounded border border-cfi-green/40 bg-cfi-green/10 p-3 text-sm text-cfi-green-dark">
          {location.state.flash}
        </div>
      )}

      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-bold text-cfi-brown-dark">{t('workorder.detailTitle', { number: detail.number })}</h1>
        <div className="flex gap-2">
          <Badge tone={priorityTone(detail.priority)}>{t(priorityKey(detail.priority))}</Badge>
          <Badge tone={statusTone(detail.status)}>{t(workOrderStatusKey(detail.status))}</Badge>
        </div>
      </div>

      <Card className="mt-4">
        <p className="mb-3 whitespace-pre-wrap text-cfi-ink">{detail.description}</p>
        <dl>
          <InfoRow label={t('workorder.unit')}>{[detail.unitName, detail.areaName, detail.lineName].filter(Boolean).join(' / ')}</InfoRow>
          <InfoRow label={t('workorder.equipment')}>
            {detail.equipmentName ?? detail.equipmentFreeText}
            {/* A part on its own says nothing: "FIBC1" only means something next to
                the machine it belongs to. */}
            {detail.equipmentParentName && (
              <span className="ml-2 text-xs text-cfi-muted">
                {t('workorder.partOf', { machine: detail.equipmentParentName })}
              </span>
            )}
          </InfoRow>
          <InfoRow label={t('workorder.reportedBy')}>
            {detail.reportedByName}
            {detail.reportedByPosition && (
              <span className="text-cfi-muted"> · {detail.reportedByPosition}</span>
            )}
          </InfoRow>
          <InfoRow label={t('workorder.reportedTo')}>
            {detail.reportedToName && (
              <>
                {detail.reportedToName}
                {detail.reportedToPosition && (
                  <span className="text-cfi-muted"> · {detail.reportedToPosition}</span>
                )}
              </>
            )}
          </InfoRow>
          <InfoRow label={t('workorder.reportedAt')}>{new Date(detail.reportedAt).toLocaleString()}</InfoRow>
          {detail.assignedEngineerName && (
            <InfoRow label={t('workorder.assignedEngineer')}>{detail.assignedEngineerName}</InfoRow>
          )}
          {detail.claimedAt && <InfoRow label={t('workorder.claimedAt')}>{new Date(detail.claimedAt).toLocaleString()}</InfoRow>}
          {detail.closedAt && <InfoRow label={t('workorder.closedAt')}>{new Date(detail.closedAt).toLocaleString()}</InfoRow>}
          {detail.labourMinutes != null && (
            <InfoRow label={t('workorder.labourMinutes')}>
              {t('workorder.labourMinutesValue', { value: formatDuration(detail.labourMinutes) })}
            </InfoRow>
          )}
        </dl>
      </Card>

      {detail.photos.length > 0 && (
        <Card className="mt-4">
          <h2 className="mb-3 font-semibold text-cfi-brown-dark">{t('common.photos')}</h2>
          <div className="flex flex-wrap gap-2">
            {detail.photos.map((photo) => (
              <AuthenticatedImage
                key={photo.id}
                mediaId={photo.mediaAssetId}
                alt={t('common.photos')}
                className="h-24 w-24 rounded object-cover"
              />
            ))}
          </div>
        </Card>
      )}

      <ErrorBanner error={actionError} />

      <div className="mt-4 flex flex-wrap gap-2">
        {canClaim && (
          <Button disabled={busy} onClick={() => runAction(() => claimWorkOrder(detail.id))}>
            {t('workorder.claim')}
          </Button>
        )}

        {canWorkOnIt && statusName === 'Accepted' && (
          <Button disabled={busy} onClick={() => runAction(() => startWork(detail.id))}>
            {t('workorder.start')}
          </Button>
        )}

        {canWorkOnIt && ACTIVE_STATUSES.has(statusName) && statusName !== 'WaitingParts' && (
          <Button variant="secondary" disabled={busy} onClick={() => runAction(() => markWaitingParts(detail.id))}>
            {t('workorder.waitingParts')}
          </Button>
        )}

        {canWorkOnIt && statusName === 'WaitingParts' && (
          <Button disabled={busy} onClick={() => runAction(() => resumeWork(detail.id))}>
            {t('workorder.resume')}
          </Button>
        )}

        {canWorkOnIt && ACTIVE_STATUSES.has(statusName) && (
          <>
            <Button variant="secondary" disabled={busy} onClick={() => runAction(() => notifyReporter(detail.id, 0))}>
              {t('workorder.notifyBusy')}
            </Button>
            <Button variant="secondary" disabled={busy} onClick={() => runAction(() => notifyReporter(detail.id, 1))}>
              {t('workorder.notifyOnMyWay')}
            </Button>
          </>
        )}
      </div>

      {/* A manager can hand the job straight to a named engineer, whether it is still in
          the pool or already claimed by somebody else. */}
      {canAssign && !['Completed', 'Rejected'].includes(statusName) && engineers.length > 0 && (
        <Card className="mt-4">
          <h2 className="mb-3 font-semibold text-cfi-brown-dark">{t('workorder.assignTo')}</h2>
          <div className="flex flex-wrap gap-2">
            {engineers.map((engineer) => (
              <Button
                key={engineer.id}
                variant="secondary"
                disabled={busy}
                onClick={() => runAction(() => assignWorkOrder(detail.id, engineer.id))}
              >
                {engineer.fullName}
              </Button>
            ))}
          </div>
        </Card>
      )}

      {canReject && (
        <Card className="mt-4">
          {rejecting ? (
            <form
              className="flex flex-wrap items-end gap-2"
              onSubmit={(event) => {
                event.preventDefault();
                runAction(() => rejectWorkOrder(detail.id, rejectReason));
              }}
            >
              <label className="flex flex-1 flex-col gap-1 text-sm">
                {t('workorder.rejectReason')}
                <input
                  required
                  value={rejectReason}
                  onChange={(event) => setRejectReason(event.target.value)}
                  placeholder={t('workorder.rejectReasonPlaceholder')}
                  className="min-h-11 rounded border border-cfi-rule bg-white px-3"
                />
              </label>
              <Button type="submit" variant="danger" disabled={busy || !rejectReason.trim()}>
                {t('workorder.reject')}
              </Button>
              <Button type="button" variant="secondary" onClick={() => setRejecting(false)}>
                {t('common.cancel')}
              </Button>
            </form>
          ) : (
            <Button variant="secondary" onClick={() => setRejecting(true)}>
              {t('workorder.reject')}
            </Button>
          )}
        </Card>
      )}

      {canClose && (
        <div className="mt-4">
          <CloseJobForm
            submitting={busy}
            error={actionError}
            onSubmit={(payload) => runAction(() => closeWorkOrder(detail.id, payload))}
          />
        </div>
      )}

      {canSubmitQa && (
        <div className="mt-4">
          <QaResultForm
            submitting={busy}
            error={actionError}
            onSubmit={(payload) => runAction(() => submitQaResult(detail.id, payload))}
          />
        </div>
      )}

      {/* The last box on the paper form, filled in by the one person who was standing at
          the machine: whoever reported it, saying it actually works again. */}
      {canAcceptRepair && (
        <Card className="mt-4">
          <h2 className="mb-1 font-semibold text-cfi-brown-dark">{t('workorder.acceptRepairTitle')}</h2>
          <p className="mb-3 text-sm text-cfi-muted">{t('workorder.acceptRepairHint')}</p>
          <Button
            disabled={busy}
            onClick={() =>
              runAction(() => signOffReporter(detail.id, {
                areaCleanAndTidy: true,
                releasedBackIntoService: true,
              }))}
          >
            {t('workorder.acceptRepair')}
          </Button>
        </Card>
      )}

      {(canSignOffProduction || canSignOffQa) && (
        <Card className="mt-4">
          <h2 className="mb-3 font-semibold text-cfi-brown-dark">{t('workorder.signOffs')}</h2>
          <div className="flex gap-2">
            {canSignOffProduction && (
              <Button
                disabled={busy}
                onClick={() =>
                  runAction(() => signOffProduction(detail.id, { areaCleanAndTidy: true, releasedBackIntoService: true }))
                }
              >
                {t('workorder.closeForm.title')} — Production
              </Button>
            )}
            {canSignOffQa && (
              <Button
                disabled={busy}
                onClick={() => runAction(() => signOffQa(detail.id, { areaCleanAndTidy: true, releasedBackIntoService: true }))}
              >
                QA
              </Button>
            )}
          </div>
        </Card>
      )}

      {detail.closures.length > 0 && (
        <Card className="mt-4">
          <h2 className="mb-3 font-semibold text-cfi-brown-dark">{t('workorder.closureHistory')}</h2>
          <div className="flex flex-col gap-3">
            {detail.closures.map((closure) => (
              <div key={closure.version} className="rounded border border-cfi-rule p-3 text-sm">
                <p className="mb-1 font-mono text-xs text-cfi-muted">v{closure.version} — {closure.submittedByName}</p>
                <p><span className="text-cfi-muted">{t('workorder.closeForm.rootCause')}:</span> {closure.rootCause}</p>
                <p><span className="text-cfi-muted">{t('workorder.closeForm.correctiveAction')}:</span> {closure.correctiveAction}</p>
                <p>
                  <span className="text-cfi-muted">{t('workorder.closeForm.downtime')}:</span>{' '}
                  {formatDuration(closure.downtimeMinutes)}
                </p>
              </div>
            ))}
          </div>
        </Card>
      )}

      {detail.qaChecks.length > 0 && (
        <Card className="mt-4">
          <h2 className="mb-3 font-semibold text-cfi-brown-dark">{t('workorder.qaChecks')}</h2>
          <div className="flex flex-col gap-2 text-sm">
            {detail.qaChecks.map((check) => (
              <div key={check.attempt} className="flex items-center justify-between border-b border-cfi-rule/60 py-1 last:border-b-0">
                <span>#{check.attempt} — {check.resultByName ?? '—'}</span>
                <Badge tone={check.result === 'Pass' || check.result === 1 ? 'bg-cfi-green/15 text-cfi-green-dark' : 'bg-cfi-red/15 text-cfi-red'}>
                  {check.result}
                </Badge>
              </div>
            ))}
          </div>
        </Card>
      )}

      <Card className="mt-4">
        <h2 className="mb-3 font-semibold text-cfi-brown-dark">{t('workorder.eventHistory')}</h2>
        <ul className="flex flex-col gap-2 text-sm">
          {detail.events.map((event) => (
            <li key={event.id} className="flex justify-between gap-4 border-b border-cfi-rule/60 pb-2 last:border-b-0">
              <span className="text-cfi-ink">{event.summary}</span>
              <span className="whitespace-nowrap text-cfi-muted">{new Date(event.occurredAt).toLocaleString()}</span>
            </li>
          ))}
        </ul>
      </Card>
    </div>
  );
}
