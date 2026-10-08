import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../auth/AuthContext';
import Icon from '../components/Icon';
import LanguageSelect from '../components/LanguageSelect';
import { errorMessage } from '../lib/format';

export default function LoginPage() {
  const { t } = useTranslation();
  const { login } = useAuth();
  const [name, setName] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  async function submit(e) {
    e.preventDefault();
    if (!name.trim() || !password) return;
    setBusy(true);
    setError(null);
    try {
      await login(name.trim(), password);
    } catch (err) {
      setError(err);
      setBusy(false);
    }
  }

  return (
    <div className="flex min-h-screen flex-col items-center justify-center bg-brown px-4">
      <div className="absolute top-3 right-3">
        <LanguageSelect dark />
      </div>
      <div className="mb-6 flex flex-col items-center gap-3 text-cream">
        <span className="flex h-16 w-16 items-center justify-center rounded-2xl bg-yellow text-brown shadow-lg">
          <Icon name="check" size={36} strokeWidth={3} />
        </span>
        <h1 className="text-2xl font-bold">{t('app.name')}</h1>
      </div>

      <form onSubmit={submit} className="w-full max-w-sm space-y-4 rounded-3xl bg-cream p-6 shadow-xl">
        <div>
          <label className="label" htmlFor="name">
            {t('login.name')}
          </label>
          <input
            id="name"
            className="input"
            autoComplete="username"
            autoCapitalize="words"
            value={name}
            onChange={(e) => setName(e.target.value)}
            autoFocus
          />
        </div>
        <div>
          <label className="label" htmlFor="password">
            {t('login.password')}
          </label>
          <div className="relative">
            <input
              id="password"
              type={showPassword ? 'text' : 'password'}
              className="input pr-12"
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
            <button
              type="button"
              onClick={() => setShowPassword((v) => !v)}
              className={`absolute inset-y-0 right-1 flex w-10 items-center justify-center rounded-lg ${
                showPassword ? 'text-brown' : 'text-muted'
              } hover:text-brown`}
              aria-label={showPassword ? t('login.hidePassword') : t('login.showPassword')}
              title={showPassword ? t('login.hidePassword') : t('login.showPassword')}
            >
              <Icon name={showPassword ? 'eyeOff' : 'eye'} size={20} />
            </button>
          </div>
        </div>
        {error && (
          <p className="flex items-center gap-2 rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger" role="alert">
            <Icon name="alert" size={16} />
            {errorMessage(t, error)}
          </p>
        )}
        <button type="submit" className="btn-primary w-full py-3 text-base" disabled={busy || !name.trim() || !password}>
          {t('login.submit')}
        </button>
      </form>
    </div>
  );
}
