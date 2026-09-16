import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';

import Button from '../components/ui/Button';
import Card from '../components/ui/Card';
import ErrorBanner from '../components/ui/ErrorBanner';
import LanguageSelector from '../components/LanguageSelector';
import { describeApiError } from '../api/apiClient';
import { register } from '../api/endpoints';
import { SUPPORTED_LANGUAGES } from '../i18n';

export default function RegisterPage() {
  const { t, i18n } = useTranslation();

  const [form, setForm] = useState({ fullName: '', email: '', phoneNumber: '', password: '' });
  const [error, setError] = useState(null);
  const [submitting, setSubmitting] = useState(false);
  const [done, setDone] = useState(false);

  const update = (field) => (event) => setForm((current) => ({ ...current, [field]: event.target.value }));

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      await register({
        fullName: form.fullName,
        email: form.email,
        phoneNumber: form.phoneNumber || null,
        password: form.password,
        preferredLanguage: i18n.resolvedLanguage ?? SUPPORTED_LANGUAGES[0].code,
      });
      setDone(true);
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

        {done ? (
          <Card>
            <h2 className="mb-2 text-lg font-semibold text-cfi-green-dark">
              {t('auth.registerSuccessTitle')}
            </h2>
            <p className="text-sm text-cfi-ink-soft">{t('auth.registerSuccessBody')}</p>
            <Link to="/login">
              <Button variant="secondary" className="mt-4 w-full">
                {t('auth.backToLogin')}
              </Button>
            </Link>
          </Card>
        ) : (
          <>
            <Card>
              <h2 className="mb-4 text-lg font-semibold text-cfi-brown-dark">{t('auth.registerTitle')}</h2>

              <form onSubmit={handleSubmit} className="flex flex-col gap-3">
                <label className="flex flex-col gap-1 text-sm">
                  {t('auth.fullName')}
                  <input
                    required
                    value={form.fullName}
                    onChange={update('fullName')}
                    className="min-h-11 rounded border border-cfi-rule bg-white px-3"
                  />
                </label>

                <label className="flex flex-col gap-1 text-sm">
                  {t('auth.email')}
                  <input
                    type="email"
                    required
                    autoComplete="username"
                    value={form.email}
                    onChange={update('email')}
                    className="min-h-11 rounded border border-cfi-rule bg-white px-3"
                  />
                </label>

                <label className="flex flex-col gap-1 text-sm">
                  {t('auth.phoneNumber')} <span className="text-cfi-muted">({t('common.optional')})</span>
                  <input
                    type="tel"
                    value={form.phoneNumber}
                    onChange={update('phoneNumber')}
                    className="min-h-11 rounded border border-cfi-rule bg-white px-3"
                  />
                </label>

                <label className="flex flex-col gap-1 text-sm">
                  {t('auth.password')}
                  <input
                    type="password"
                    required
                    autoComplete="new-password"
                    minLength={10}
                    value={form.password}
                    onChange={update('password')}
                    className="min-h-11 rounded border border-cfi-rule bg-white px-3"
                  />
                </label>

                <ErrorBanner error={error} />

                <Button type="submit" disabled={submitting} className="mt-1 w-full">
                  {t('auth.registerButton')}
                </Button>
              </form>
            </Card>

            <p className="mt-4 text-center text-sm">
              <Link to="/login" className="font-semibold text-cfi-brown-dark underline">
                {t('auth.switchToLogin')}
              </Link>
            </p>
          </>
        )}
      </div>
    </div>
  );
}
