import { useTranslation } from 'react-i18next';

import ErrorBanner from './ErrorBanner';

/**
 * The loading / empty / error triple every list screen owes the user, written once.
 *
 * A production screen never shows a blank panel while it thinks, and never swallows a
 * failure - so each page hands this its useApiData result and gets all three states,
 * including a retry that does not cost a page reload.
 */
export default function AsyncSection({ status, error, refetch, isEmpty, emptyKey, children }) {
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

  if (isEmpty) {
    return <p className="text-cfi-muted">{t(emptyKey ?? 'common.noResults')}</p>;
  }

  return children;
}
