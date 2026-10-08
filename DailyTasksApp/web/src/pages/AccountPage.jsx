import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import Icon from '../components/Icon';
import { errorMessage, fieldError } from '../lib/format';

export default function AccountPage() {
  const { t } = useTranslation();
  const { user, isManager } = useAuth();
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [repeat, setRepeat] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [saved, setSaved] = useState(false);

  const mismatch = repeat.length > 0 && next !== repeat;
  const canSave = current && next.length >= 8 && next === repeat;

  async function submit(e) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    setSaved(false);
    try {
      await api.changePassword(current, next);
      setCurrent('');
      setNext('');
      setRepeat('');
      setSaved(true);
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mx-auto max-w-md space-y-4">
      <div className="card flex items-center gap-3 p-4">
        <span
          className={`flex h-12 w-12 items-center justify-center rounded-full ${
            isManager ? 'bg-brown text-cream' : 'bg-cream text-brown'
          }`}
        >
          <Icon name={isManager ? 'shield' : 'wrench'} size={22} />
        </span>
        <div>
          <p className="text-lg font-bold">{user.name}</p>
          <p className="text-sm text-muted">{t(`role.${user.role}`)}</p>
        </div>
      </div>

      <form onSubmit={submit} className="card space-y-4 p-4" autoComplete="off">
        <h2 className="flex items-center gap-2 font-bold text-brown">
          <Icon name="key" size={18} />
          {t('account.changePassword')}
        </h2>
        <PasswordField
          id="current-password"
          label={t('account.currentPassword')}
          value={current}
          onChange={setCurrent}
          autoComplete="current-password"
          error={fieldError(t, error, 'currentPassword')}
        />
        <PasswordField
          id="new-password"
          label={t('account.newPassword')}
          value={next}
          onChange={setNext}
          autoComplete="new-password"
          error={fieldError(t, error, 'newPassword')}
          hint={t('users.passwordHint')}
        />
        <PasswordField
          id="repeat-password"
          label={t('account.confirmPassword')}
          value={repeat}
          onChange={setRepeat}
          autoComplete="new-password"
          error={mismatch ? t('account.mismatch') : null}
        />
        {error && !error.fieldErrors && <p className="text-sm text-danger">{errorMessage(t, error)}</p>}
        {saved && (
          <p className="flex items-center gap-2 rounded-lg bg-green-soft px-3 py-2 text-sm font-medium text-green" role="status">
            <Icon name="check" size={16} strokeWidth={3} />
            {t('account.saved')}
          </p>
        )}
        <button type="submit" className="btn-primary w-full" disabled={busy || !canSave}>
          {t('common.save')}
        </button>
      </form>
    </div>
  );
}

function PasswordField({ id, label, value, onChange, autoComplete, error, hint }) {
  return (
    <div>
      <label className="label" htmlFor={id}>
        {label}
      </label>
      <input
        id={id}
        type="password"
        className="input"
        value={value}
        maxLength={128}
        autoComplete={autoComplete}
        onChange={(e) => onChange(e.target.value)}
      />
      {error ? (
        <p className="mt-1 text-sm text-danger">{error}</p>
      ) : hint ? (
        <p className="mt-1 text-xs text-muted">{hint}</p>
      ) : null}
    </div>
  );
}
