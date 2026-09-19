import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import ErrorBanner from '../../components/ui/ErrorBanner';
import { describeApiError } from '../../api/apiClient';
import { getUnits, getPendingUsers, getRoles, rejectPendingUser } from '../../api/endpoints';
import { useApiData } from '../../hooks/useApiData';
import ApproveUserForm from './ApproveUserForm';

export default function PendingApprovalsPage() {
  const { t } = useTranslation();
  const [openId, setOpenId] = useState(null);
  const [rejectingId, setRejectingId] = useState(null);
  const [rejectReason, setRejectReason] = useState('');
  const [rejectError, setRejectError] = useState(null);
  const [busy, setBusy] = useState(false);

  const pending = useApiData(() => getPendingUsers({ pageSize: 100 }));
  const roles = useApiData(() => getRoles());
  const units = useApiData(() => getUnits({ pageSize: 100 }));

  const loading = [pending, roles, units].some((x) => x.status === 'loading');
  const anyError = [pending, roles, units].find((x) => x.status === 'error');

  const startReject = (userId) => {
    setOpenId(null);
    setRejectingId(userId);
    setRejectReason('');
    setRejectError(null);
  };

  const submitReject = async (userId) => {
    setRejectError(null);
    setBusy(true);
    try {
      await rejectPendingUser(userId, rejectReason);
      setRejectingId(null);
      pending.refetch();
    } catch (error) {
      setRejectError(describeApiError(error));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <h1 className="mb-4 text-xl font-bold text-cfi-brown-dark">{t('nav.pendingApprovals')}</h1>

      {loading && <p className="text-cfi-muted">{t('common.loading')}</p>}
      {anyError && <ErrorBanner error={anyError.error} />}

      {!loading && !anyError && pending.data.items.length === 0 && (
        <p className="text-cfi-muted">{t('common.noResults')}</p>
      )}

      {!loading && !anyError && (
        <div className="flex flex-col gap-3">
          {pending.data.items.map((user) => (
            <Card key={user.id}>
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div>
                  <p className="font-medium text-cfi-ink">{user.fullName}</p>
                  <p className="text-sm text-cfi-muted">{user.email}</p>
                </div>
                <div className="flex gap-2">
                  <button
                    type="button"
                    onClick={() => {
                      setRejectingId(null);
                      setOpenId(openId === user.id ? null : user.id);
                    }}
                    className="min-h-11 rounded bg-cfi-sunk px-4 text-sm font-semibold text-cfi-brown-dark"
                  >
                    {openId === user.id ? t('common.cancel') : t('common.confirm')}
                  </button>
                  <button
                    type="button"
                    onClick={() => (rejectingId === user.id ? setRejectingId(null) : startReject(user.id))}
                    className="min-h-11 rounded bg-cfi-red/10 px-4 text-sm font-semibold text-cfi-red"
                  >
                    {rejectingId === user.id ? t('common.cancel') : t('common.reject')}
                  </button>
                </div>
              </div>

              {openId === user.id && (
                <ApproveUserForm
                  user={user}
                  roles={roles.data}
                  units={units.data.items}
                  onApproved={() => {
                    setOpenId(null);
                    pending.refetch();
                  }}
                />
              )}

              {rejectingId === user.id && (
                <form
                  onSubmit={(event) => {
                    event.preventDefault();
                    submitReject(user.id);
                  }}
                  className="mt-3 flex flex-col gap-3 border-t border-cfi-rule pt-3"
                >
                  <label className="flex flex-col gap-1 text-sm">
                    {t('team.rejectReason')}
                    <input
                      required
                      value={rejectReason}
                      onChange={(event) => setRejectReason(event.target.value)}
                      placeholder={t('team.rejectReasonPlaceholder')}
                      className="min-h-11 rounded border border-cfi-rule bg-white px-3 text-sm"
                    />
                  </label>

                  <ErrorBanner error={rejectError} />

                  <Button
                    type="submit"
                    variant="danger"
                    disabled={busy || !rejectReason.trim()}
                    className="w-fit"
                  >
                    {t('common.reject')}
                  </Button>
                </form>
              )}
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
