import React, {useState} from 'react';
import {Pressable, ScrollView, StyleSheet, Text, View} from 'react-native';
import {useTranslation} from 'react-i18next';
import {version} from '../../app.json';
import {api} from '../api';
import {useAuth} from '../auth';
import {errorMessage, fieldError} from '../format';
import LanguagePicker from '../LanguagePicker';
import {colors} from '../theme';
import {Button, Field, Glyph, Input, styles as ui} from '../ui';

// The signed-in user's own name and password. `forced`: shown on its own right after logging in
// with a password a manager set, until the user picks their own.
export default function AccountScreen({forced = false}) {
  const {t} = useTranslation();
  const {user, isManager, logout} = useAuth();

  return (
    <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
      {forced ? (
        <View style={styles.forcedBar}>
          <LanguagePicker />
          <Pressable onPress={logout} hitSlop={8} accessibilityLabel={t('nav.logout')}>
            <Glyph name="logout" size={22} />
          </Pressable>
        </View>
      ) : null}
      <View style={[ui.card, styles.profile]}>
        <View style={[styles.avatar, isManager && styles.avatarManager]}>
          <Glyph name={isManager ? 'shield' : 'wrench'} size={22} />
        </View>
        <View style={ui.flex}>
          <Text style={styles.name}>{user.name}</Text>
          <Text style={styles.role}>{t(`role.${user.role}`)}</Text>
        </View>
      </View>
      {forced ? <Text style={styles.notice}>🔑 {t('account.mustChange')}</Text> : <NameForm />}
      <PasswordForm />
      {!forced ? <Text style={styles.version}>{t('account.version', {version})}</Text> : null}
    </ScrollView>
  );
}

function NameForm() {
  const {t} = useTranslation();
  const {user, startSession} = useAuth();
  const [name, setName] = useState('');
  const [current, setCurrent] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [saved, setSaved] = useState(false);

  const trimmed = name.trim();
  const canSave = trimmed.length >= 2 && trimmed !== user.name && current;

  async function submit() {
    setBusy(true);
    setError(null);
    setSaved(false);
    try {
      await startSession(await api.changeName(current, trimmed));
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
  const nameError =
    fieldError(t, error, 'newName') ?? (error?.code === 'user.nameTaken' ? errorMessage(t, error) : null);

  return (
    <View style={[ui.card, styles.form]}>
      <Text style={styles.title}>👤 {t('account.changeName')}</Text>
      <Field label={t('account.newName')} error={nameError} hint={t('account.nameHint')}>
        <Input value={name} onChangeText={setName} maxLength={100} autoCapitalize="words" placeholder={user.name} />
      </Field>
      <Field label={t('account.currentPassword')} error={fieldError(t, error, 'currentPassword')}>
        <Input value={current} onChangeText={setCurrent} secureTextEntry maxLength={128} autoCapitalize="none" />
      </Field>
      {error && !error.fieldErrors && error.code !== 'user.nameTaken' ? (
        <Text style={styles.error}>{errorMessage(t, error)}</Text>
      ) : null}
      {saved ? <Text style={styles.saved}>✓ {t('account.nameSaved')}</Text> : null}
      <Button title={t('common.save')} onPress={submit} disabled={!canSave} busy={busy} />
    </View>
  );
}

function PasswordForm() {
  const {t} = useTranslation();
  const {updateUser} = useAuth();
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [repeat, setRepeat] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [saved, setSaved] = useState(false);

  const mismatch = repeat.length > 0 && next !== repeat;
  const canSave = current && next.length >= 8 && next === repeat;

  async function submit() {
    setBusy(true);
    setError(null);
    setSaved(false);
    try {
      await api.changePassword(current, next);
      setCurrent('');
      setNext('');
      setRepeat('');
      setSaved(true);
      updateUser({mustChangePassword: false, resetRequested: false});
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <View style={[ui.card, styles.form]}>
      <Text style={styles.title}>🔑 {t('account.changePassword')}</Text>
      <Field label={t('account.currentPassword')} error={fieldError(t, error, 'currentPassword')}>
        <Input value={current} onChangeText={setCurrent} secureTextEntry maxLength={128} autoCapitalize="none" />
      </Field>
      <Field
        label={t('account.newPassword')}
        error={fieldError(t, error, 'newPassword')}
        hint={t('users.passwordHint')}>
        <Input value={next} onChangeText={setNext} secureTextEntry maxLength={128} autoCapitalize="none" />
      </Field>
      <Field label={t('account.confirmPassword')} error={mismatch ? t('account.mismatch') : null}>
        <Input value={repeat} onChangeText={setRepeat} secureTextEntry maxLength={128} autoCapitalize="none" />
      </Field>
      {error && !error.fieldErrors ? <Text style={styles.error}>{errorMessage(t, error)}</Text> : null}
      {saved ? <Text style={styles.saved}>✓ {t('account.saved')}</Text> : null}
      <Button title={t('common.save')} onPress={submit} disabled={!canSave} busy={busy} />
    </View>
  );
}

const styles = StyleSheet.create({
  content: {padding: 14, gap: 12},
  forcedBar: {flexDirection: 'row', justifyContent: 'flex-end', alignItems: 'center', gap: 16},
  profile: {flexDirection: 'row', alignItems: 'center', gap: 12, padding: 14},
  avatar: {
    width: 48,
    height: 48,
    borderRadius: 24,
    backgroundColor: colors.cream,
    alignItems: 'center',
    justifyContent: 'center',
  },
  avatarManager: {backgroundColor: colors.yellowSoft},
  name: {fontSize: 18, fontWeight: '800', color: colors.ink},
  role: {color: colors.muted},
  notice: {
    color: colors.brown,
    backgroundColor: colors.yellowSoft,
    borderRadius: 12,
    padding: 12,
    fontWeight: '600',
  },
  form: {padding: 14},
  title: {fontSize: 16, fontWeight: '800', color: colors.brown, marginBottom: 12},
  error: {color: colors.danger, marginBottom: 10},
  saved: {
    color: colors.green,
    backgroundColor: colors.greenSoft,
    borderRadius: 10,
    padding: 10,
    marginBottom: 10,
    fontWeight: '700',
  },
  version: {textAlign: 'center', color: colors.muted, fontSize: 12, marginTop: 4, marginBottom: 8},
});
