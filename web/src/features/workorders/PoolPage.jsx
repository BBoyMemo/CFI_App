import { useTranslation } from 'react-i18next';

import { useApiData } from '../../hooks/useApiData';
import { getPool } from '../../api/endpoints';
import WorkOrderList from './WorkOrderList';

export default function PoolPage() {
  const { t } = useTranslation();
  const { status, data, error, refetch } = useApiData(() => getPool({ pageSize: 50 }));

  return (
    <div>
      <h1 className="mb-4 text-xl font-bold text-cfi-brown-dark">{t('nav.pool')}</h1>
      <WorkOrderList status={status} data={data} error={error} refetch={refetch} emptyKey="workorder.poolEmpty" />
    </div>
  );
}
