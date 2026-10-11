import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import Icon from '../components/Icon';
import LanguageSelect from '../components/LanguageSelect';
import PasswordInput from '../components/PasswordInput';
import { errorMessage, fieldError } from '../lib/format';

// The signed-in user's own name and password. `forced`: shown on its own right after logging in
// with a password a manager set, until the user picks their own.
export default function AccountPage({ forced = false }) {
  const { t } = useTranslation();
  const { user, isManager, logout } = useAuth();

  const profile = (
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
  );

  if (forced) {
    return (
      <div className="min-h-screen bg-cream px-4 py-6">
        <div className="mx-auto max-w-md space-y-4">
          <div className="flex items-center justify-end gap-2">
            <LanguageSelect />
            <button type="button" className="icon-btn" onClick={logout} aria-label={t('nav.logout')} title={t('nav.logout')}>
              <Icon name="logout" />
            </button>
          </div>
          {profile}
          <p className="flex items-start gap-2 rounded-xl bg-yellow/25 px-3 py-2.5 text-sm font-medium text-brown" role="status">
            <Icon name="key" size={18} className="mt-0.5 shrink-0" />
            {t('account.mustChange')}
          </p>
          <PasswordForm />
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-md space-y-4">
      {profile}
      <NameForm />
      <PasswordForm />
    </div>
  );
}

function NameForm() {
  const { t } = useTranslation();
  const { user, startSession } = useAuth();
  const [name, setName] = useState('');
  const [current, setCurrent] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [saved, setSaved] = useState(false);

  const trimmed = name.trim();
  const canSave = trimmed.length >= 2 && trimmed !== user.name && current;

  async function submit(e) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    setSaved(false);
    try {
      startSession(await api.changeName(current, trimmed));
      setName('');
      setCurrent('');
      setSaved(true);
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  // 409 (name taken) comes back as a code, not a field error; show it under the name.
  const nameError = fieldError(t, error, 'newName') ?? (error?.code === 'user.nameTaken' ? errorMessage(t, error) : null);

  return (
    <form onSubmit={submit} className="card space-y-4 p-4" autoComplete="off">
      <h2 className="flex items-center gap-2 font-bold text-brown">
        <Icon name="user" size={18} />
        {t('account.changeName')}
      </h2>
      <div>
        <label className="label" htmlFor="new-name">
          {t('account.newName')}
        </label>
        <input
          id="new-name"
          className="input"
          value={name}
          maxLength={100}
          autoCapitalize="words"
          placeholder={user.name}
          onChange={(e) => setName(e.target.value)}
        />
        {nameError ? (
          <p className="mt-1 text-sm text-danger">{nameError}</p>
        ) : (
          <p className="mt-1 text-xs text-muted">{t('account.nameHint')}</p>
        )}
      </div>
      <PasswordField
        id="name-current-password"
        label={t('account.currentPassword')}
        value={current}
        onChange={setCurrent}
        autoComplete="current-password"
        error={fieldError(t, error, 'currentPassword')}
      />
      {error && !error.fieldErrors && error.code !== 'user.nameTaken' && (
        <p className="text-sm text-danger">{errorMessage(t, error)}</p>
      )}
      {saved && <Saved text={t('account.nameSaved')} />}
      <button type="submit" className="btn-primary w-full" disabled={busy || !canSave}>
        {t('common.save')}
      </button>
    </form>
  );
}

function PasswordForm() {
  const { t } = useTranslation();
  const { updateUser } = useAuth();
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
      updateUser({ mustChangePassword: false, resetRequested: false });
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
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
      {saved && <Saved text={t('account.saved')} />}
      <button type="submit" className="btn-primary w-full" disabled={busy || !canSave}>
        {t('common.save')}
      </button>
    </form>
  );
}

function Saved({ text }) {
  return (
    <p className="flex items-center gap-2 rounded-lg bg-green-soft px-3 py-2 text-sm font-medium text-green" role="status">
      <Icon name="check" size={16} strokeWidth={3} />
      {text}
    </p>
  );
}

function PasswordField({ id, label, value, onChange, autoComplete, error, hint }) {
  return (
    <div>
      <label className="label" htmlFor={id}>
        {label}
      </label>
      <PasswordInput id={id} value={value} onChange={onChange} autoComplete={autoComplete} />
      {error ? (
        <p className="mt-1 text-sm text-danger">{error}</p>
      ) : hint ? (
        <p className="mt-1 text-xs text-muted">{hint}</p>
      ) : null}
    </div>
  );
}
