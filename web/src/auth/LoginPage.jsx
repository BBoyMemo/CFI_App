import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useLocation, useNavigate } from 'react-router-dom';

import Button from '../components/ui/Button';
import Card from '../components/ui/Card';
import ErrorBanner from '../components/ui/ErrorBanner';
import LanguageSelector from '../components/LanguageSelector';
import { describeApiError } from '../api/apiClient';
import { useAuth } from './useAuth';

export default function LoginPage() {
  const { t } = useTranslation();
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState(null);
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      await login(email, password);
      navigate(location.state?.from?.pathname ?? '/', { replace: true });
    } catch (submitError) {
      setError(describeApiError(submitError));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-cfi-cream px-4">
      <div className="w-full max-w-sm">
        <div className="mb-6 flex items-center justify-between">
          <h1 className="text-xl font-bold text-cfi-brown-dark">{t('app.name')}</h1>
          <LanguageSelector />
        </div>

        <Card>
          <h2 className="mb-4 text-lg font-semibold text-cfi-brown-dark">{t('auth.loginTitle')}</h2>

          <form onSubmit={handleSubmit} className="flex flex-col gap-3">
            <label className="flex flex-col gap-1 text-sm">
              {t('auth.email')}
              <input
                type="email"
                required
                autoComplete="username"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                className="min-h-11 rounded border border-cfi-rule bg-white px-3"
              />
            </label>

            <label className="flex flex-col gap-1 text-sm">
              {t('auth.password')}
              <input
                type="password"
                required
                autoComplete="current-password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                className="min-h-11 rounded border border-cfi-rule bg-white px-3"
              />
            </label>

            <ErrorBanner error={error} />

            <Button type="submit" disabled={submitting} className="mt-1 w-full">
              {t('auth.loginButton')}
            </Button>
          </form>
        </Card>

        <p className="mt-4 text-center text-sm">
          <Link to="/register" className="font-semibold text-cfi-brown-dark underline">
            {t('auth.switchToRegister')}
          </Link>
        </p>
      </div>
    </div>
  );
}
