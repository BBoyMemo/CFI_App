import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';

import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import ErrorBanner from '../../components/ui/ErrorBanner';
import LanguageSelector from '../../components/LanguageSelector';
import { describeApiError } from '../../api/apiClient';
import { changePassword } from '../../api/endpoints';
import { Permissions } from '../../api/permissions';
import { useAuth } from '../../auth/useAuth';

const controlClass = 'min-h-11 rounded border border-cfi-rule bg-white px-3 text-sm';

export default function ProfilePage() {
  const { t } = useTranslation();
  const { me, hasPermission } = useAuth();

  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState(null);
  const [done, setDone] = useState(false);
  const [busy, setBusy] = useState(false);

  const mismatch = confirmPassword.length > 0 && newPassword !== confirmPassword;

  const submit = async (event) => {
    event.preventDefault();
    setError(null);
    setDone(false);
    setBusy(true);

    try {
      await changePassword({ currentPassword, newPassword });
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
      setDone(true);
    } catch (submitError) {
      setError(describeApiError(submitError));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="mx-auto max-w-xl">
      <h1 className="mb-4 text-xl font-bold text-cfi-brown-dark">{t('nav.profile')}</h1>

      <Card>
        <dl className="flex flex-col gap-2 text-sm">
          <div className="flex justify-between gap-4">
            <dt className="text-cfi-muted">{t('auth.fullName')}</dt>
            <dd className="text-cfi-ink">{me?.fullName}</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-cfi-muted">{t('auth.email')}</dt>
            <dd className="text-cfi-ink">{me?.email}</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-cfi-muted">{t('profile.role')}</dt>
            <dd className="text-cfi-ink">{me?.role}</dd>
          </div>
          <div className="flex items-center justify-between gap-4">
            <dt className="text-cfi-muted">{t('profile.language')}</dt>
            <dd><LanguageSelector /></dd>
          </div>
        </dl>
      </Card>

      {/* Site setup lives behind the account rather than on the main bar: one person
          edits units, areas and machines occasionally, and a permanent nav slot for it
          would put admin work in front of everyone who never does any. */}
      {hasPermission(Permissions.AdminManage) && (
        <Card className="mt-4">
          <h2 className="mb-1 font-semibold text-cfi-brown-dark">{t('nav.admin')}</h2>
          <p className="mb-3 text-sm text-cfi-muted">{t('profile.adminHint')}</p>
          <Link to="/admin">
            <Button>{t('profile.openAdmin')}</Button>
          </Link>
        </Card>
      )}

      <Card className="mt-4">
        <h2 className="mb-3 font-semibold text-cfi-brown-dark">{t('profile.changePassword')}</h2>

        <form onSubmit={submit} className="flex flex-col gap-3">
          <label className="flex flex-col gap-1 text-sm">
            {t('profile.currentPassword')}
            <input
              required
              type="password"
              autoComplete="current-password"
              value={currentPassword}
              onChange={(event) => setCurrentPassword(event.target.value)}
              className={controlClass}
            />
          </label>

          <label className="flex flex-col gap-1 text-sm">
            {t('profile.newPassword')}
            <input
              required
              type="password"
              autoComplete="new-password"
              value={newPassword}
              onChange={(event) => setNewPassword(event.target.value)}
              className={controlClass}
            />
          </label>

          <label className="flex flex-col gap-1 text-sm">
            {t('profile.confirmPassword')}
            <input
              required
              type="password"
              autoComplete="new-password"
              value={confirmPassword}
              onChange={(event) => setConfirmPassword(event.target.value)}
              className={controlClass}
            />
          </label>

          {/* Caught here rather than at the server: a typo in the confirmation is not a
              failed request, it is a question the form can answer itself. */}
          {mismatch && <p className="text-sm text-cfi-red">{t('profile.passwordsDoNotMatch')}</p>}

          <ErrorBanner error={error} />

          {done && (
            <p className="rounded border border-cfi-green/40 bg-cfi-green/10 p-3 text-sm text-cfi-green-dark">
              {t('profile.passwordChanged')}
            </p>
          )}

          <Button type="submit" className="w-fit" disabled={busy || mismatch || !newPassword}>
            {t('profile.changePassword')}
          </Button>
        </form>
      </Card>
    </div>
  );
}
