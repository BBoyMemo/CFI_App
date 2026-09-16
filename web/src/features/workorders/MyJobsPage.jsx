import { useTranslation } from 'react-i18next';

import { useApiData } from '../../hooks/useApiData';
import { getMyJobs } from '../../api/endpoints';
import WorkOrderList from './WorkOrderList';

export default function MyJobsPage() {
  const { t } = useTranslation();
  const { status, data, error, refetch } = useApiData(() => getMyJobs({ pageSize: 50 }));

  return (
    <div>
      <h1 className="mb-4 text-xl font-bold text-cfi-brown-dark">{t('nav.myJobs')}</h1>
      <WorkOrderList status={status} data={data} error={error} refetch={refetch} emptyKey="workorder.myJobsEmpty" />
    </div>
  );
}
