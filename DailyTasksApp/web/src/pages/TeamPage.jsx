import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../api/client';
import Icon from '../components/Icon';
import Modal from '../components/Modal';
import { errorMessage, fieldError } from '../lib/format';

const ROLES = [
  { value: 'Engineer', icon: 'wrench' },
  { value: 'Manager', icon: 'shield' },
];

export default function TeamPage() {
  const { t } = useTranslation();
  const [users, setUsers] = useState(null);
  const [error, setError] = useState(null);
  const [adding, setAdding] = useState(false);

  const load = useCallback(async () => {
    try {
      setError(null);
      setUsers(await api.users());
    } catch (err) {
      setError(err);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div className="space-y-4">
      <h1 className="flex items-center gap-2 text-lg font-bold text-brown">
        <Icon name="team" />
        {t('users.title')}
        {users && <span className="font-normal text-muted">· {users.length}</span>}
      </h1>

      {error && (
        <div className="card flex items-center gap-3 p-4 text-danger">
          <Icon name="alert" />
          <span className="flex-1 text-sm">{errorMessage(t, error)}</span>
          <button type="button" className="btn-ghost" onClick={load}>
            {t('common.retry')}
          </button>
        </div>
      )}
      {!users && !error && <p className="py-10 text-center text-muted">{t('common.loading')}</p>}
      {users && users.length === 0 && <p className="py-10 text-center text-sm text-muted">{t('users.empty')}</p>}

      {users && users.length > 0 && (
        <ul className="card divide-y divide-cream-dark">
          {users.map((u) => (
            <li key={u.id} className="flex items-center gap-3 px-4 py-3">
              <span
                className={`flex h-9 w-9 items-center justify-center rounded-full ${
                  u.role === 'Manager' ? 'bg-brown text-cream' : 'bg-cream text-brown'
                }`}
              >
                <Icon name={u.role === 'Manager' ? 'shield' : 'wrench'} size={18} />
              </span>
              <span className="flex-1 font-medium">{u.name}</span>
              <span className="text-sm text-muted">{t(`role.${u.role}`)}</span>
            </li>
          ))}
        </ul>
      )}

      <button
        type="button"
        onClick={() => setAdding(true)}
        className="fixed right-5 bottom-24 z-20 flex h-14 w-14 items-center justify-center rounded-full bg-yellow text-ink shadow-lg hover:bg-yellow-dark sm:bottom-8"
        aria-label={t('users.add')}
        title={t('users.add')}
      >
        <Icon name="plus" size={28} strokeWidth={2.5} />
      </button>

      {adding && (
        <AddUserModal
          onClose={() => setAdding(false)}
          onCreated={(user) => {
            setUsers((list) => [...(list ?? []), user].sort((a, b) => a.name.localeCompare(b.name)));
            setAdding(false);
          }}
        />
      )}
    </div>
  );
}

function AddUserModal({ onCreated, onClose }) {
  const { t } = useTranslation();
  const [name, setName] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState('Engineer');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  async function submit(e) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      onCreated(await api.createUser({ name: name.trim(), password, role }));
    } catch (err) {
      setError(err);
      setBusy(false);
    }
  }

  // 409 (name taken) comes back as a code, not a field error; show it under the name.
  const nameError = fieldError(t, error, 'name') ?? (error?.code === 'user.nameTaken' ? errorMessage(t, error) : null);

  return (
    <Modal
      title={t('users.add')}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn-ghost flex-1" onClick={onClose}>
            {t('common.cancel')}
          </button>
          <button
            type="submit"
            form="user-form"
            className="btn-primary flex-1"
            disabled={busy || name.trim().length < 2 || password.length < 8}
          >
            {t('common.save')}
          </button>
        </>
      }
    >
      <form id="user-form" onSubmit={submit} className="space-y-4" autoComplete="off">
        <div>
          <label className="label" htmlFor="user-name">
            {t('users.name')}
          </label>
          <input
            id="user-name"
            className="input"
            value={name}
            maxLength={100}
            autoCapitalize="words"
            onChange={(e) => setName(e.target.value)}
            autoFocus
          />
          {nameError && <p className="mt-1 text-sm text-danger">{nameError}</p>}
        </div>
        <div>
          <label className="label" htmlFor="user-password">
            {t('users.password')}
          </label>
          <input
            id="user-password"
            type="text"
            className="input font-mono"
            value={password}
            maxLength={128}
            autoComplete="new-password"
            onChange={(e) => setPassword(e.target.value)}
          />
          <p className={`mt-1 text-xs ${fieldError(t, error, 'password') ? 'text-danger' : 'text-muted'}`}>
            {fieldError(t, error, 'password') ?? t('users.passwordHint')}
          </p>
        </div>
        <div>
          <span className="label">{t('users.role')}</span>
          <div className="grid grid-cols-2 gap-1 rounded-xl bg-cream-dark p-1">
            {ROLES.map((r) => (
              <button
                key={r.value}
                type="button"
                onClick={() => setRole(r.value)}
                aria-pressed={role === r.value}
                className={`flex items-center justify-center gap-2 rounded-lg py-2 text-sm font-semibold ${
                  role === r.value ? 'bg-white text-brown shadow-sm' : 'text-muted'
                }`}
              >
                <Icon name={r.icon} size={16} />
                {t(`role.${r.value}`)}
              </button>
            ))}
          </div>
        </div>
        {error && !error.fieldErrors && error.code !== 'user.nameTaken' && (
          <p className="text-sm text-danger">{errorMessage(t, error)}</p>
        )}
      </form>
    </Modal>
  );
}
