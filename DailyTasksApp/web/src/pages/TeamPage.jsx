import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import Icon from '../components/Icon';
import Modal, { ConfirmModal } from '../components/Modal';
import PasswordInput from '../components/PasswordInput';
import { errorMessage, fieldError } from '../lib/format';

const ROLES = [
  { value: 'Engineer', icon: 'wrench' },
  { value: 'Manager', icon: 'shield' },
];

// Active people first (by name), deactivated ones after them.
const byStatusThenName = (a, b) => Number(!a.active) - Number(!b.active) || a.name.localeCompare(b.name);

export default function TeamPage() {
  const { t } = useTranslation();
  const { user: me } = useAuth();
  const [users, setUsers] = useState(null);
  const [error, setError] = useState(null);
  // { type: 'person' | 'add' | 'edit' | 'deactivate', user? }
  const [dialog, setDialog] = useState(null);
  const [action, setAction] = useState({ busy: false, error: null });

  const load = useCallback(async () => {
    try {
      setError(null);
      setUsers((await api.users()).sort(byStatusThenName));
    } catch (err) {
      setError(err);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const replace = (user) =>
    setUsers((list) => [...(list ?? []).filter((u) => u.id !== user.id), user].sort(byStatusThenName));

  const close = () => {
    setDialog(null);
    setAction({ busy: false, error: null });
  };

  // Deactivate (after the confirm) or activate again; either way the person's card closes.
  async function run(call) {
    setAction({ busy: true, error: null });
    try {
      replace(await call(dialog.user.id));
      close();
    } catch (err) {
      setAction({ busy: false, error: errorMessage(t, err) });
    }
  }

  const activeCount = users?.filter((u) => u.active).length ?? 0;

  return (
    <div className="space-y-4">
      <h1 className="flex items-center gap-2 text-lg font-bold text-brown">
        <Icon name="team" />
        {t('users.title')}
        {users && <span className="font-normal text-muted">· {activeCount}</span>}
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
          {users.map((u) => {
            const isMe = u.id === me.id;
            return (
              <li key={u.id}>
                {/* Tap a person for their card with the actions; your own details live on the account page. */}
                <button
                  type="button"
                  disabled={isMe}
                  onClick={() => setDialog({ type: 'person', user: u })}
                  className="flex w-full items-center gap-3 px-4 py-3 text-left enabled:hover:bg-cream/60"
                >
                  <span
                    className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-full ${
                      u.role === 'Manager' ? 'bg-brown text-cream' : 'bg-cream text-brown'
                    }`}
                  >
                    <Icon name={u.role === 'Manager' ? 'shield' : 'wrench'} size={18} />
                  </span>
                  <span className="block min-w-0 flex-1">
                    <span className="block font-medium break-words">
                      {u.name}
                      {isMe && <span className="ml-1.5 text-sm font-normal text-muted">({t('users.you')})</span>}
                    </span>
                    <span className="flex flex-wrap items-center gap-1.5 text-sm text-muted">
                      {t(`role.${u.role}`)}
                      {!u.active && (
                        <span className="rounded-full bg-cream-dark px-2 py-0.5 text-xs font-semibold text-brown">
                          {t('users.deactivated')}
                        </span>
                      )}
                      {u.active && u.resetRequested && (
                        <span className="inline-flex items-center gap-1 rounded-full bg-yellow/30 px-2 py-0.5 text-xs font-semibold text-brown">
                          <Icon name="key" size={12} />
                          {t('users.resetRequested')}
                        </span>
                      )}
                    </span>
                  </span>
                  {!isMe && <Icon name="right" size={18} className="shrink-0 text-muted" />}
                </button>
              </li>
            );
          })}
        </ul>
      )}

      <button
        type="button"
        onClick={() => setDialog({ type: 'add' })}
        className="fixed right-5 bottom-24 z-20 flex h-14 w-14 items-center justify-center rounded-full bg-yellow text-ink shadow-lg hover:bg-yellow-dark sm:bottom-8"
        aria-label={t('users.add')}
        title={t('users.add')}
      >
        <Icon name="plus" size={28} strokeWidth={2.5} />
      </button>

      {dialog?.type === 'person' && (
        <PersonModal
          user={dialog.user}
          onEdit={() => setDialog({ type: 'edit', user: dialog.user })}
          onDeactivate={() => setDialog({ type: 'deactivate', user: dialog.user })}
          onActivate={() => run(api.activateUser)}
          busy={action.busy}
          error={action.error}
          onClose={close}
        />
      )}
      {(dialog?.type === 'add' || dialog?.type === 'edit') && (
        <UserModal
          user={dialog.user}
          onClose={close}
          onSaved={(user) => {
            replace(user);
            close();
          }}
        />
      )}
      {dialog?.type === 'deactivate' && (
        <ConfirmModal
          message={t('users.deactivateConfirm', { name: dialog.user.name })}
          confirmLabel={t('users.deactivate')}
          onConfirm={() => run(api.deactivateUser)}
          onClose={close}
          busy={action.busy}
          error={action.error}
        />
      )}
    </div>
  );
}

// One person's card: who they are and what a manager can do with them.
function PersonModal({ user, onEdit, onDeactivate, onActivate, busy, error, onClose }) {
  const { t } = useTranslation();
  return (
    <Modal
      title={user.name}
      onClose={onClose}
      footer={
        user.active ? (
          <>
            <button type="button" className="btn-ghost flex-1 text-danger" onClick={onDeactivate}>
              <Icon name="trash" size={18} />
              {t('users.deactivate')}
            </button>
            <button type="button" className="btn-primary flex-1" onClick={onEdit}>
              <Icon name={user.resetRequested ? 'key' : 'edit'} size={18} />
              {user.resetRequested ? t('users.resetPassword') : t('common.edit')}
            </button>
          </>
        ) : (
          <button type="button" className="btn-green flex-1" onClick={onActivate} disabled={busy}>
            <Icon name="check" size={18} strokeWidth={3} />
            {t('users.activate')}
          </button>
        )
      }
    >
      <div className="flex flex-wrap items-center gap-2">
        <span className="inline-flex items-center gap-1.5 rounded-full bg-cream px-3 py-1 text-sm font-semibold text-brown">
          <Icon name={user.role === 'Manager' ? 'shield' : 'wrench'} size={14} />
          {t(`role.${user.role}`)}
        </span>
        {!user.active && (
          <span className="rounded-full bg-cream-dark px-3 py-1 text-sm font-semibold text-brown">{t('users.deactivated')}</span>
        )}
        {user.active && user.resetRequested && (
          <span className="inline-flex items-center gap-1 rounded-full bg-yellow/30 px-3 py-1 text-sm font-semibold text-brown">
            <Icon name="key" size={14} />
            {t('users.resetRequested')}
          </span>
        )}
      </div>
      {error && <p className="mt-3 text-sm text-danger">{error}</p>}
    </Modal>
  );
}

// Adds a person, or edits one (`user`). A manager sets a password when adding someone, and later
// only for a person who asked for a new one ("Forgot password?"); that one is temporary and the
// person must change it after logging in.
function UserModal({ user, onSaved, onClose }) {
  const { t } = useTranslation();
  const editing = Boolean(user);
  const canSetPassword = !editing || user.resetRequested;
  const [name, setName] = useState(user?.name ?? '');
  const [password, setPassword] = useState('');
  const [repeat, setRepeat] = useState('');
  const [role, setRole] = useState(user?.role ?? 'Engineer');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  const mismatch = repeat.length > 0 && password !== repeat;
  const passwordOk = editing && !password ? true : password.length >= 8 && password === repeat;

  async function submit(e) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const body = { name: name.trim(), password, role };
      onSaved(editing ? await api.updateUser(user.id, body) : await api.createUser(body));
    } catch (err) {
      setError(err);
      setBusy(false);
    }
  }

  // 409 (name taken) comes back as a code, not a field error; show it under the name.
  const nameError = fieldError(t, error, 'name') ?? (error?.code === 'user.nameTaken' ? errorMessage(t, error) : null);

  return (
    <Modal
      title={editing ? t('users.edit') : t('users.add')}
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
            disabled={busy || name.trim().length < 2 || !passwordOk}
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
            autoFocus={!user?.resetRequested}
          />
          {nameError && <p className="mt-1 text-sm text-danger">{nameError}</p>}
        </div>
        {canSetPassword && (
          <div>
            <label className="label" htmlFor="user-password">
              {editing ? t('users.newPassword') : t('users.password')}
              {editing && <span className="font-normal text-muted"> ({t('common.optional')})</span>}
            </label>
            <PasswordInput
              id="user-password"
              value={password}
              onChange={setPassword}
              autoComplete="new-password"
              autoFocus={Boolean(user?.resetRequested)}
            />
            <p className={`mt-1 text-xs ${fieldError(t, error, 'password') ? 'text-danger' : 'text-muted'}`}>
              {fieldError(t, error, 'password') ?? (editing ? t('users.newPasswordHint') : t('users.passwordHint'))}
            </p>
          </div>
        )}
        {canSetPassword && (!editing || password) && (
          <div>
            <label className="label" htmlFor="user-password-repeat">
              {t('users.confirmPassword')}
            </label>
            <PasswordInput id="user-password-repeat" value={repeat} onChange={setRepeat} autoComplete="new-password" />
            {mismatch && <p className="mt-1 text-sm text-danger">{t('account.mismatch')}</p>}
          </div>
        )}
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
