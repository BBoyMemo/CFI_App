import React, {useCallback, useEffect, useState} from 'react';
import {FlatList, Pressable, RefreshControl, StyleSheet, Text, View} from 'react-native';
import {useTranslation} from 'react-i18next';
import {api} from '../api';
import {useAuth} from '../auth';
import {errorMessage, fieldError} from '../format';
import {colors} from '../theme';
import {
  Button,
  ConfirmSheet,
  ErrorBox,
  Fab,
  Field,
  Glyph,
  Input,
  Segmented,
  Sheet,
  styles as ui,
} from '../ui';

// Active people first (by name), deactivated ones after them.
const byStatusThenName = (a, b) => Number(!a.active) - Number(!b.active) || a.name.localeCompare(b.name);

export default function TeamScreen() {
  const {t} = useTranslation();
  const {user: me} = useAuth();
  const [users, setUsers] = useState(null);
  const [error, setError] = useState(null);
  // {type: 'person' | 'add' | 'edit' | 'deactivate', user?}
  const [dialog, setDialog] = useState(null);
  const [action, setAction] = useState({busy: false, error: null});
  const [refreshing, setRefreshing] = useState(false);

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

  const replace = user => setUsers(list => [...(list ?? []).filter(u => u.id !== user.id), user].sort(byStatusThenName));

  const close = () => {
    setDialog(null);
    setAction({busy: false, error: null});
  };

  // Deactivate (after the confirm) or activate again; either way the person's card closes.
  async function run(call) {
    setAction({busy: true, error: null});
    try {
      replace(await call(dialog.user.id));
      close();
    } catch (err) {
      setAction({busy: false, error: errorMessage(t, err)});
    }
  }

  return (
    <View style={ui.flex}>
      {error ? (
        <View style={styles.pad}>
          <ErrorBox message={errorMessage(t, error)} onRetry={load} />
        </View>
      ) : null}
      {!users && !error ? <Text style={styles.loading}>{t('common.loading')}</Text> : null}
      {users ? (
        <FlatList
          data={users}
          keyExtractor={u => u.id}
          contentContainerStyle={styles.list}
          refreshControl={
            <RefreshControl
              refreshing={refreshing}
              onRefresh={async () => {
                setRefreshing(true);
                await load();
                setRefreshing(false);
              }}
              colors={[colors.brown]}
            />
          }
          ListHeaderComponent={
            <Text style={styles.header}>
              {t('users.title')} · {users.filter(u => u.active).length}
            </Text>
          }
          ListEmptyComponent={<Text style={ui.empty}>{t('users.empty')}</Text>}
          renderItem={({item: u}) => {
            const isMe = u.id === me.id;
            // Tap a person for their card with the actions; your own details live on the account screen.
            return (
              <Pressable
                disabled={isMe}
                onPress={() => setDialog({type: 'person', user: u})}
                style={({pressed}) => [ui.card, styles.row, pressed && styles.pressed]}>
                <View style={[styles.avatar, u.role === 'Manager' && styles.avatarManager]}>
                  <Glyph name={u.role === 'Manager' ? 'shield' : 'wrench'} size={16} />
                </View>
                <View style={ui.flex}>
                  <Text style={styles.name}>
                    {u.name}
                    {isMe ? <Text style={styles.you}> ({t('users.you')})</Text> : null}
                  </Text>
                  <View style={styles.meta}>
                    <Text style={styles.role}>{t(`role.${u.role}`)}</Text>
                    {!u.active ? <Text style={styles.deactivated}>{t('users.deactivated')}</Text> : null}
                    {u.active && u.resetRequested ? (
                      <Text style={styles.reset}>🔑 {t('users.resetRequested')}</Text>
                    ) : null}
                  </View>
                </View>
                {!isMe ? <Glyph name="right" size={22} color={colors.muted} /> : null}
              </Pressable>
            );
          }}
        />
      ) : null}
      <Fab onPress={() => setDialog({type: 'add'})} label={t('users.add')} />
      {dialog?.type === 'person' ? (
        <PersonSheet
          user={dialog.user}
          onEdit={() => setDialog({type: 'edit', user: dialog.user})}
          onDeactivate={() => setDialog({type: 'deactivate', user: dialog.user})}
          onActivate={() => run(api.activateUser)}
          busy={action.busy}
          error={action.error}
          onClose={close}
        />
      ) : null}
      {dialog?.type === 'add' || dialog?.type === 'edit' ? (
        <UserSheet
          user={dialog.user}
          onClose={close}
          onSaved={user => {
            replace(user);
            close();
          }}
        />
      ) : null}
      {dialog?.type === 'deactivate' ? (
        <ConfirmSheet
          message={t('users.deactivateConfirm', {name: dialog.user.name})}
          confirmLabel={t('users.deactivate')}
          onConfirm={() => run(api.deactivateUser)}
          onClose={close}
          busy={action.busy}
          error={action.error}
        />
      ) : null}
    </View>
  );
}

// One person's card: who they are and what a manager can do with them.
function PersonSheet({user, onEdit, onDeactivate, onActivate, busy, error, onClose}) {
  const {t} = useTranslation();
  return (
    <Sheet
      title={user.name}
      onClose={onClose}
      footer={
        user.active ? (
          <>
            <Button title={t('users.deactivate')} icon="trash" variant="ghost" onPress={onDeactivate} style={ui.flex} />
            <Button
              title={user.resetRequested ? t('users.resetPassword') : t('common.edit')}
              icon={user.resetRequested ? 'key' : 'edit'}
              onPress={onEdit}
              style={ui.flex}
            />
          </>
        ) : (
          <Button title={t('users.activate')} icon="check" variant="green" onPress={onActivate} busy={busy} style={ui.flex} />
        )
      }>
      <View style={styles.meta}>
        <Text style={styles.roleChip}>
          {user.role === 'Manager' ? '🛡' : '🔧'} {t(`role.${user.role}`)}
        </Text>
        {!user.active ? <Text style={styles.deactivated}>{t('users.deactivated')}</Text> : null}
        {user.active && user.resetRequested ? <Text style={styles.reset}>🔑 {t('users.resetRequested')}</Text> : null}
      </View>
      {error ? <Text style={styles.error}>{error}</Text> : null}
    </Sheet>
  );
}

// Adds a person, or edits one (`user`). A manager sets a password when adding someone, and later
// only for a person who asked for a new one ("Forgot password?"); that one is temporary and the
// person must change it after logging in.
function UserSheet({user, onSaved, onClose}) {
  const {t} = useTranslation();
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

  async function submit() {
    setBusy(true);
    setError(null);
    try {
      const body = {name: name.trim(), password, role};
      onSaved(editing ? await api.updateUser(user.id, body) : await api.createUser(body));
    } catch (err) {
      setError(err);
      setBusy(false);
    }
  }

  const nameError = fieldError(t, error, 'name') ?? (error?.code === 'user.nameTaken' ? errorMessage(t, error) : null);

  return (
    <Sheet
      title={editing ? t('users.edit') : t('users.add')}
      onClose={onClose}
      footer={
        <>
          <Button title={t('common.cancel')} variant="ghost" onPress={onClose} style={ui.flex} />
          <Button
            title={t('common.save')}
            onPress={submit}
            disabled={name.trim().length < 2 || !passwordOk}
            busy={busy}
            style={ui.flex}
          />
        </>
      }>
      <Field label={t('users.name')} error={nameError}>
        <Input value={name} onChangeText={setName} maxLength={100} autoCapitalize="words" />
      </Field>
      {canSetPassword ? (
        <Field
          label={editing ? t('users.newPassword') : t('users.password')}
          optional={editing}
          error={fieldError(t, error, 'password')}
          hint={editing ? t('users.newPasswordHint') : t('users.passwordHint')}>
          <Input
            value={password}
            onChangeText={setPassword}
            maxLength={128}
            autoCapitalize="none"
            autoCorrect={false}
            autoFocus={Boolean(user?.resetRequested)}
          />
        </Field>
      ) : null}
      {canSetPassword && (!editing || password) ? (
        <Field label={t('users.confirmPassword')} error={mismatch ? t('account.mismatch') : null}>
          <Input value={repeat} onChangeText={setRepeat} maxLength={128} autoCapitalize="none" autoCorrect={false} />
        </Field>
      ) : null}
      <Field label={t('users.role')}>
        <Segmented
          value={role}
          onChange={setRole}
          options={[
            {value: 'Engineer', icon: 'wrench', label: t('role.Engineer')},
            {value: 'Manager', icon: 'shield', label: t('role.Manager')},
          ]}
        />
      </Field>
      {error && !error.fieldErrors && error.code !== 'user.nameTaken' ? (
        <Text style={styles.error}>{errorMessage(t, error)}</Text>
      ) : null}
    </Sheet>
  );
}

const styles = StyleSheet.create({
  pad: {paddingHorizontal: 14, paddingTop: 12},
  list: {padding: 14, paddingBottom: 100},
  loading: {textAlign: 'center', color: colors.muted, paddingVertical: 24},
  header: {fontSize: 17, fontWeight: '800', color: colors.brown, marginBottom: 10},
  row: {flexDirection: 'row', alignItems: 'center', gap: 10, padding: 12, marginBottom: 8},
  avatar: {
    width: 36,
    height: 36,
    borderRadius: 18,
    backgroundColor: colors.cream,
    alignItems: 'center',
    justifyContent: 'center',
  },
  avatarManager: {backgroundColor: colors.yellowSoft},
  name: {fontSize: 15, fontWeight: '600', color: colors.ink},
  you: {fontWeight: '400', color: colors.muted},
  meta: {flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: 6, marginTop: 2},
  role: {color: colors.muted},
  deactivated: {
    fontSize: 12,
    fontWeight: '700',
    color: colors.brown,
    backgroundColor: colors.creamDark,
    borderRadius: 999,
    paddingHorizontal: 8,
    paddingVertical: 1,
    overflow: 'hidden',
  },
  reset: {
    fontSize: 12,
    fontWeight: '700',
    color: colors.brown,
    backgroundColor: colors.yellowSoft,
    borderRadius: 999,
    paddingHorizontal: 8,
    paddingVertical: 1,
    overflow: 'hidden',
  },
  pressed: {opacity: 0.85},
  roleChip: {
    fontSize: 14,
    fontWeight: '700',
    color: colors.brown,
    backgroundColor: colors.cream,
    borderRadius: 999,
    paddingHorizontal: 10,
    paddingVertical: 3,
    overflow: 'hidden',
  },
  error: {color: colors.danger, marginTop: 8},
});
