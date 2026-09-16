import { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';

import Card from '../../components/ui/Card';
import ConfirmButton from '../../components/ui/ConfirmButton';
import ErrorBanner from '../../components/ui/ErrorBanner';
import { describeApiError } from '../../api/apiClient';
import { disableUser, getTeam } from '../../api/endpoints';
import { Permissions } from '../../api/permissions';
import { useAuth } from '../../auth/useAuth';
import { useApiData } from '../../hooks/useApiData';

export default function TeamPage() {
  const { t } = useTranslation();
  const { me, hasPermission } = useAuth();

  const team = useApiData(() => getTeam({ pageSize: 100 }));
  const { status, data, error } = team;

  const [actionError, setActionError] = useState(null);
  const [busy, setBusy] = useState(false);

  /**
   * Removing somebody disables the account rather than deleting the row: their clock
   * events feed a payslip and their name is on signed-off repairs, so the record has to
   * survive them leaving. Every session ends immediately, so a phone that walked out of
   * the gate with them stops working.
   */
  const remove = useCallback(async (userId) => {
    setActionError(null);
    setBusy(true);
    try {
      await disableUser(userId);
      team.refetch();
    } catch (removeError) {
      setActionError(describeApiError(removeError));
    } finally {
      setBusy(false);
    }
  }, [team]);

  const canRemove = hasPermission(Permissions.UserManage);

  return (
    <div>
      <h1 className="mb-4 text-xl font-bold text-cfi-brown-dark">{t('nav.team')}</h1>

      <ErrorBanner error={actionError} />

      {status === 'loading' && <p className="text-cfi-muted">{t('common.loading')}</p>}
      {status === 'error' && <ErrorBanner error={error} />}

      {status === 'ready' && data.items.length === 0 && (
        <p className="text-cfi-muted">{t('common.noResults')}</p>
      )}

      {status === 'ready' && data.items.length > 0 && (
        <div className="flex flex-col gap-2">
          {data.items.map((member) => (
            <Card key={member.id} className="flex flex-wrap items-center justify-between gap-3">
              <div className="min-w-0">
                <p className="font-medium text-cfi-ink">{member.fullName}</p>
                <p className="truncate text-sm text-cfi-muted">{member.email}</p>
              </div>

              <div className="flex items-center gap-3">
                <div className="text-right text-sm">
                  <p className="text-cfi-ink">{member.role}</p>
                  <p className="text-cfi-muted">{member.department ?? member.occupation ?? '—'}</p>
                </div>

                {/* Not offered on your own row: the API refuses it anyway, and a button
                    that always fails is worse than no button. */}
                {canRemove && member.id !== me?.id && (
                  <ConfirmButton
                    disabled={busy}
                    onConfirm={() => remove(member.id)}
                    question={t('team.confirmRemove', { name: member.fullName })}
                    confirmLabel={t('team.remove')}
                  >
                    {t('team.remove')}
                  </ConfirmButton>
                )}
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
