import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import Button from '../../components/ui/Button';
import ErrorBanner from '../../components/ui/ErrorBanner';
import { apiClient, describeApiError } from '../../api/apiClient';
import { getWorkOrderHistory } from '../../api/endpoints';
import { useApiData } from '../../hooks/useApiData';
import WorkOrderList from './WorkOrderList';

const controlClass = 'min-h-11 rounded border border-cfi-rule bg-white px-3';

export default function HistoryPage() {
  const { t } = useTranslation();

  const [search, setSearch] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [filters, setFilters] = useState({});
  const [exportError, setExportError] = useState(null);
  const [exporting, setExporting] = useState(false);

  const { status, data, error, refetch } = useApiData(
    () => getWorkOrderHistory({ ...filters, pageSize: 50 }),
    [filters],
  );

  const apply = (event) => {
    event.preventDefault();
    setFilters({
      search: search.trim() || undefined,
      // Whole days: "to 3 March" has to include everything that happened on the 3rd.
      from: from ? `${from}T00:00:00Z` : undefined,
      to: to ? `${to}T23:59:59Z` : undefined,
    });
  };

  /**
   * The CSV comes back through apiClient because the endpoint needs the bearer token,
   * which a plain link cannot carry - the same reason photos go through AuthenticatedImage.
   */
  const exportCsv = async () => {
    setExportError(null);
    setExporting(true);

    try {
      const response = await apiClient.get('/api/v1/workorders/history/export', {
        params: filters,
        responseType: 'blob',
      });

      const url = URL.createObjectURL(response.data);
      const link = document.createElement('a');
      link.href = url;
      link.download = `work-order-history-${new Date().toISOString().slice(0, 10)}.csv`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (downloadError) {
      setExportError(describeApiError(downloadError));
    } finally {
      setExporting(false);
    }
  };

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-bold text-cfi-brown-dark">{t('nav.history')}</h1>
        <Button variant="secondary" disabled={exporting} onClick={exportCsv}>
          {t('history.exportCsv')}
        </Button>
      </div>

      <form onSubmit={apply} className="mb-4 flex flex-wrap items-end gap-2">
        <label className="flex min-w-48 flex-1 flex-col gap-1 text-sm">
          {t('common.search')}
          <input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder={t('history.searchPlaceholder')}
            className={controlClass}
          />
        </label>

        <label className="flex flex-col gap-1 text-sm">
          {t('holiday.from')}
          <input type="date" value={from} onChange={(event) => setFrom(event.target.value)} className={controlClass} />
        </label>

        <label className="flex flex-col gap-1 text-sm">
          {t('holiday.to')}
          <input type="date" value={to} onChange={(event) => setTo(event.target.value)} className={controlClass} />
        </label>

        <Button type="submit">{t('common.search')}</Button>
      </form>

      <ErrorBanner error={exportError} />

      <WorkOrderList status={status} data={data} error={error} refetch={refetch} emptyKey="common.noResults" />
    </div>
  );
}
