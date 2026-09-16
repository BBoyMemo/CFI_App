import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import Card from '../../components/ui/Card';
import ErrorBanner from '../../components/ui/ErrorBanner';
import { getUnits, getPendingUsers, getRoles } from '../../api/endpoints';
import { useApiData } from '../../hooks/useApiData';
import ApproveUserForm from './ApproveUserForm';

export default function PendingApprovalsPage() {
  const { t } = useTranslation();
  const [openId, setOpenId] = useState(null);

  const pending = useApiData(() => getPendingUsers({ pageSize: 100 }));
  const roles = useApiData(() => getRoles());
  const units = useApiData(() => getUnits({ pageSize: 100 }));

  const loading = [pending, roles, units].some((x) => x.status === 'loading');
  const anyError = [pending, roles, units].find((x) => x.status === 'error');

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
                <button
                  type="button"
                  onClick={() => setOpenId(openId === user.id ? null : user.id)}
                  className="min-h-11 rounded bg-cfi-sunk px-4 text-sm font-semibold text-cfi-brown-dark"
                >
                  {openId === user.id ? t('common.cancel') : t('common.confirm')}
                </button>
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
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
