import { useTranslation } from 'react-i18next';

import Card from '../../components/ui/Card';
import ErrorBanner from '../../components/ui/ErrorBanner';
import { getTeam } from '../../api/endpoints';
import { useApiData } from '../../hooks/useApiData';

export default function TeamPage() {
  const { t } = useTranslation();
  const { status, data, error } = useApiData(() => getTeam({ pageSize: 100 }));

  return (
    <div>
      <h1 className="mb-4 text-xl font-bold text-cfi-brown-dark">{t('nav.team')}</h1>

      {status === 'loading' && <p className="text-cfi-muted">{t('common.loading')}</p>}
      {status === 'error' && <ErrorBanner error={error} />}

      {status === 'ready' && data.items.length === 0 && (
        <p className="text-cfi-muted">{t('common.noResults')}</p>
      )}

      {status === 'ready' && data.items.length > 0 && (
        <div className="flex flex-col gap-2">
          {data.items.map((member) => (
            <Card key={member.id} className="flex items-center justify-between">
              <div>
                <p className="font-medium text-cfi-ink">{member.fullName}</p>
                <p className="text-sm text-cfi-muted">{member.email}</p>
              </div>
              <div className="text-right text-sm">
                <p className="text-cfi-ink">{member.role}</p>
                <p className="text-cfi-muted">{member.department ?? member.occupation ?? '—'}</p>
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
