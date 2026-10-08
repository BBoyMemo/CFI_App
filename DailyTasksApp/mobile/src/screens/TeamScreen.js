import React, {useCallback, useEffect, useState} from 'react';
import {FlatList, RefreshControl, StyleSheet, Text, View} from 'react-native';
import {useTranslation} from 'react-i18next';
import {api} from '../api';
import {errorMessage, fieldError} from '../format';
import {colors} from '../theme';
import {Button, ErrorBox, Fab, Field, Glyph, Input, Segmented, Sheet, styles as ui} from '../ui';

export default function TeamScreen() {
  const {t} = useTranslation();
  const [users, setUsers] = useState(null);
  const [error, setError] = useState(null);
  const [adding, setAdding] = useState(false);
  const [refreshing, setRefreshing] = useState(false);

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
              {t('users.title')} · {users.length}
            </Text>
          }
          ListEmptyComponent={<Text style={ui.empty}>{t('users.empty')}</Text>}
          renderItem={({item: u}) => (
            <View style={[ui.card, styles.row]}>
              <View style={[styles.avatar, u.role === 'Manager' && styles.avatarManager]}>
                <Glyph name={u.role === 'Manager' ? 'shield' : 'wrench'} size={16} />
              </View>
              <Text style={styles.name}>{u.name}</Text>
              <Text style={styles.role}>{t(`role.${u.role}`)}</Text>
            </View>
          )}
        />
      ) : null}
      <Fab onPress={() => setAdding(true)} label={t('users.add')} />
      {adding ? (
        <AddUserSheet
          onClose={() => setAdding(false)}
          onCreated={user => {
            setUsers(list => [...(list ?? []), user].sort((a, b) => a.name.localeCompare(b.name)));
            setAdding(false);
          }}
        />
      ) : null}
    </View>
  );
}

function AddUserSheet({onCreated, onClose}) {
  const {t} = useTranslation();
  const [name, setName] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState('Engineer');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  async function submit() {
    setBusy(true);
    setError(null);
    try {
      onCreated(await api.createUser({name: name.trim(), password, role}));
    } catch (err) {
      setError(err);
      setBusy(false);
    }
  }

  const nameError = fieldError(t, error, 'name') ?? (error?.code === 'user.nameTaken' ? errorMessage(t, error) : null);

  return (
    <Sheet
      title={t('users.add')}
      onClose={onClose}
      footer={
        <>
          <Button title={t('common.cancel')} variant="ghost" onPress={onClose} style={ui.flex} />
          <Button
            title={t('common.save')}
            onPress={submit}
            disabled={name.trim().length < 2 || password.length < 8}
            busy={busy}
            style={ui.flex}
          />
        </>
      }>
      <Field label={t('users.name')} error={nameError}>
        <Input value={name} onChangeText={setName} maxLength={100} autoCapitalize="words" />
      </Field>
      <Field label={t('users.password')} error={fieldError(t, error, 'password')} hint={t('users.passwordHint')}>
        <Input value={password} onChangeText={setPassword} maxLength={128} autoCapitalize="none" autoCorrect={false} />
      </Field>
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
  row: {flexDirection: 'row', alignItems: 'center', gap: 12, padding: 12, marginBottom: 8},
  avatar: {
    width: 36,
    height: 36,
    borderRadius: 18,
    backgroundColor: colors.cream,
    alignItems: 'center',
    justifyContent: 'center',
  },
  avatarManager: {backgroundColor: colors.yellowSoft},
  name: {flex: 1, fontSize: 15, fontWeight: '600', color: colors.ink},
  role: {color: colors.muted},
  error: {color: colors.danger, marginTop: 8},
});
