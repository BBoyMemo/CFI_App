import { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router-dom';

import AsyncSection from '../../components/ui/AsyncSection';
import AuthenticatedImage from '../../components/ui/AuthenticatedImage';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import ErrorBanner from '../../components/ui/ErrorBanner';
import PhotoLightbox from '../../components/ui/PhotoLightbox';
import { describeApiError } from '../../api/apiClient';
import { completeTask, getTask, logTaskProgress } from '../../api/endpoints';
import { taskKindKey, taskKindTone, taskPriorityKey } from '../../api/enums';
import { Permissions } from '../../api/permissions';
import { useAuth } from '../../auth/useAuth';
import { useApiData } from '../../hooks/useApiData';
import PhotoUploader from '../workorders/PhotoUploader';

/** A completion or a progress note, shown the same way except for its heading colour. */
function LogEntry({ entry, dateKey, dateValue, onOpenPhoto }) {
  return (
    <div className="border-b border-cfi-rule/60 pb-2 text-sm last:border-b-0">
      <p className="font-medium text-cfi-ink">
        {entry.fullName}
        <span className="ml-2 font-normal text-cfi-muted">{new Date(dateValue).toLocaleString()}</span>
      </p>
      <p className="text-cfi-muted">{entry.note}</p>
      {entry.photoAssetId != null && (
        <button type="button" onClick={() => onOpenPhoto(entry.photoAssetId)} className="mt-1 block">
          <AuthenticatedImage
            mediaId={entry.photoAssetId}
            alt={dateKey}
            className="h-16 w-16 rounded object-cover"
          />
        </button>
      )}
    </div>
  );
}

export default function TaskDetailPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const { me, hasPermission } = useAuth();

  const task = useApiData(() => getTask(id), [id]);
  const [note, setNote] = useState('');
  const [photoAssetIds, setPhotoAssetIds] = useState([]);
  const [actionError, setActionError] = useState(null);
  const [busy, setBusy] = useState(false);
  const [openPhotoId, setOpenPhotoId] = useState(null);

  const runAction = useCallback(async (action, after) => {
    setActionError(null);
    setBusy(true);
    try {
      await action();
      if (after) after();
    } catch (error) {
      setActionError(describeApiError(error));
    } finally {
      setBusy(false);
    }
  }, []);

  const resetForm = () => {
    setNote('');
    setPhotoAssetIds([]);
    task.refetch();
  };

  const detail = task.data;

  return (
    <div className="mx-auto max-w-3xl">
      <AsyncSection {...task} isEmpty={false}>
        {detail && (
          <>
            <div className="flex flex-wrap items-start justify-between gap-2">
              <h1 className="text-xl font-bold text-cfi-brown-dark">{detail.title}</h1>
              <div className="flex gap-2">
                <Badge tone={taskKindTone(detail.kind)}>{t(taskKindKey(detail.kind))}</Badge>
                {detail.priority !== null && detail.priority !== undefined && (
                  <Badge>{t(taskPriorityKey(detail.priority))}</Badge>
                )}
              </div>
            </div>

            <Card className="mt-4">
              {detail.description && (
                <p className="mb-3 whitespace-pre-wrap text-cfi-ink">{detail.description}</p>
              )}
              <p className="text-sm text-cfi-muted">
                {t('task.scheduledDate')}: {detail.scheduledDate}
              </p>
              <p className="text-sm text-cfi-muted">
                {t('task.assignedTo')}: {detail.assignees.map((x) => x.fullName).join(', ') || '—'}
              </p>
            </Card>

            <ErrorBanner error={actionError} />

            {/* Everyone assigned completes it in their own name: a shared task is done by
                each person who did their part, not once for the whole group. */}
            {detail.assignees.some((x) => x.userId === me?.id) &&
              !detail.completions.some((x) => x.userId === me?.id) &&
              hasPermission(Permissions.TaskComplete) && (
              <Card className="mt-4">
                <h2 className="mb-3 font-semibold text-cfi-brown-dark">{t('task.completeTitle')}</h2>

                <label className="flex flex-col gap-1 text-sm">
                  {t('task.note')}
                  <textarea
                    rows={3}
                    value={note}
                    onChange={(event) => setNote(event.target.value)}
                    placeholder={t('task.notePlaceholder')}
                    className="rounded border border-cfi-rule bg-white p-3"
                  />
                </label>

                <div className="mt-3">
                  <span className="text-sm">
                    {t('common.photos')} <span className="text-cfi-muted">({t('common.optional')})</span>
                  </span>
                  <PhotoUploader assetIds={photoAssetIds} onChange={setPhotoAssetIds} />
                </div>

                <div className="mt-3 flex flex-wrap gap-2">
                  <Button
                    disabled={busy || !note.trim()}
                    onClick={() =>
                      runAction(
                        () => completeTask(detail.id, { note, photoAssetId: photoAssetIds[0] ?? null }),
                        resetForm,
                      )}
                  >
                    {t('task.markDone')}
                  </Button>

                  {/* Not every task fits in one shift. This leaves the same note and photo
                      on the record without marking it done, so the task is still there,
                      still assigned, for whoever picks it up next - which is often the
                      same person, on the next shift. */}
                  <Button
                    variant="secondary"
                    disabled={busy || !note.trim()}
                    onClick={() =>
                      runAction(
                        () => logTaskProgress(detail.id, { note, photoAssetId: photoAssetIds[0] ?? null }),
                        resetForm,
                      )}
                  >
                    {t('task.continueTomorrow')}
                  </Button>
                </div>
              </Card>
            )}

            {detail.progressNotes.length > 0 && (
              <Card className="mt-4">
                <h2 className="mb-3 font-semibold text-cfi-brown-dark">{t('task.progressNotes')}</h2>
                <div className="flex flex-col gap-3">
                  {detail.progressNotes.map((entry, index) => (
                    <LogEntry
                      // Nothing on a progress note is unique on its own - the same person
                      // can leave several - so the position in an otherwise-stable,
                      // server-ordered list is the key.
                      key={`${entry.userId}-${entry.loggedAt}-${index}`}
                      entry={entry}
                      dateKey={t('task.progressNotes')}
                      dateValue={entry.loggedAt}
                      onOpenPhoto={setOpenPhotoId}
                    />
                  ))}
                </div>
              </Card>
            )}

            {detail.completions.length > 0 && (
              <Card className="mt-4">
                <h2 className="mb-3 font-semibold text-cfi-brown-dark">{t('task.completions')}</h2>
                <div className="flex flex-col gap-3">
                  {detail.completions.map((entry) => (
                    <LogEntry
                      key={`${entry.userId}-${entry.completedAt}`}
                      entry={entry}
                      dateKey={t('task.completions')}
                      dateValue={entry.completedAt}
                      onOpenPhoto={setOpenPhotoId}
                    />
                  ))}
                </div>
              </Card>
            )}
          </>
        )}
      </AsyncSection>

      {openPhotoId != null && (
        <PhotoLightbox mediaId={openPhotoId} onClose={() => setOpenPhotoId(null)} />
      )}
    </div>
  );
}
