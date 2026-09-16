import { Navigate, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

import { useAuth } from './useAuth';

export default function RequireAuth({ children }) {
  const { me, loading } = useAuth();
  const { t } = useTranslation();
  const location = useLocation();

  if (loading) {
    return <div className="p-6 text-cfi-muted">{t('common.loading')}</div>;
  }

  if (!me) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return children;
}
