import React, {useState} from 'react';
import {ScrollView, StyleSheet, Text, View} from 'react-native';
import {useTranslation} from 'react-i18next';
import {api} from '../api';
import {useAuth} from '../auth';
import {errorMessage, fieldError} from '../format';
import {colors} from '../theme';
import {Button, Field, Glyph, Input, styles as ui} from '../ui';

export default function AccountScreen() {
  const {t} = useTranslation();
  const {user, isManager} = useAuth();
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
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
      <View style={[ui.card, styles.profile]}>
        <View style={[styles.avatar, isManager && styles.avatarManager]}>
          <Glyph name={isManager ? 'shield' : 'wrench'} size={22} />
        </View>
        <View>
          <Text style={styles.name}>{user.name}</Text>
          <Text style={styles.role}>{t(`role.${user.role}`)}</Text>
        </View>
      </View>

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
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  content: {padding: 14, gap: 12},
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
});
