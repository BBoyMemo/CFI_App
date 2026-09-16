import { useTranslation } from 'react-i18next';

import ErrorBanner from '../../components/ui/ErrorBanner';
import WorkOrderListItem from './WorkOrderListItem';

export default function WorkOrderList({ status, data, error, refetch, emptyKey }) {
  const { t } = useTranslation();

  if (status === 'loading') {
    return <p className="text-cfi-muted">{t('common.loading')}</p>;
  }

  if (status === 'error') {
    return (
      <div className="flex flex-col items-start gap-2">
        <ErrorBanner error={error} />
        <button type="button" onClick={refetch} className="text-sm font-semibold text-cfi-brown-dark underline">
          {t('common.retry')}
        </button>
      </div>
    );
  }

  const items = data?.items ?? [];

  if (items.length === 0) {
    return <p className="text-cfi-muted">{t(emptyKey)}</p>;
  }

  return (
    <div className="flex flex-col gap-3">
      {items.map((workOrder) => (
        <WorkOrderListItem key={workOrder.id} workOrder={workOrder} />
      ))}
    </div>
  );
}
