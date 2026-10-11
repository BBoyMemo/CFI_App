import React, {useState} from 'react';
import {
  KeyboardAvoidingView,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import {useTranslation} from 'react-i18next';
import {api} from '../api';
import {useAuth} from '../auth';
import {errorMessage} from '../format';
import LanguagePicker from '../LanguagePicker';
import {colors} from '../theme';
import {Button, Field, Input, PasswordInput} from '../ui';

export default function LoginScreen() {
  const {t} = useTranslation();
  const auth = useAuth();
  const [name, setName] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [forgot, setForgot] = useState(false);

  const canSubmit = name.trim() && password;

  async function submit() {
    if (!canSubmit) {
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await auth.login(name.trim(), password);
    } catch (err) {
      setError(err);
      setBusy(false);
    }
  }

  return (
    <KeyboardAvoidingView behavior="height" style={styles.screen}>
      <ScrollView
        contentContainerStyle={styles.content}
        keyboardShouldPersistTaps="handled">
        <View style={styles.lang}>
          <LanguagePicker dark />
        </View>
        <View style={styles.logo}>
          <Text style={styles.logoMark}>✓</Text>
        </View>
        <Text style={styles.title}>{t('app.name')}</Text>

        {forgot ? (
          <ForgotPassword initialName={name} onBack={() => setForgot(false)} />
        ) : (
          <View style={styles.card}>
            <Field label={t('login.name')}>
              <Input
                value={name}
                onChangeText={setName}
                autoCapitalize="words"
                autoComplete="username"
              />
            </Field>
            <Field label={t('login.password')}>
              <PasswordInput
                value={password}
                onChangeText={setPassword}
                autoComplete="password"
                onSubmitEditing={submit}
              />
            </Field>
            {error ? (
              <Text style={styles.error}>⚠ {errorMessage(t, error)}</Text>
            ) : null}
            <Button
              title={t('login.submit')}
              onPress={submit}
              disabled={!canSubmit}
              busy={busy}
            />
            <Pressable
              onPress={() => {
                setError(null);
                setForgot(true);
              }}
              hitSlop={6}
              style={styles.link}>
              <Text style={styles.linkText}>{t('login.forgot')}</Text>
            </Pressable>
          </View>
        )}
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

// Asks the managers for a new password. Nothing changes for the account until a manager sets one.
function ForgotPassword({initialName, onBack}) {
  const {t} = useTranslation();
  const [name, setName] = useState(initialName);
  const [busy, setBusy] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState(null);

  async function submit() {
    setBusy(true);
    setError(null);
    try {
      await api.forgotPassword(name.trim());
      setSent(true);
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <View style={styles.card}>
      <Text style={styles.cardTitle}>🔑 {t('login.forgot')}</Text>
      {sent ? (
        <Text style={styles.sent}>✓ {t('login.forgotSent')}</Text>
      ) : (
        <>
          <Text style={styles.hint}>{t('login.forgotHint')}</Text>
          <Field label={t('login.name')}>
            <Input value={name} onChangeText={setName} autoCapitalize="words" maxLength={100} />
          </Field>
          {error ? <Text style={styles.error}>⚠ {errorMessage(t, error)}</Text> : null}
          <Button title={t('login.forgotSend')} onPress={submit} disabled={!name.trim()} busy={busy} />
        </>
      )}
      <Pressable onPress={onBack} hitSlop={6} style={styles.link}>
        <Text style={styles.linkText}>{t('login.backToLogin')}</Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: {flex: 1, backgroundColor: colors.brown},
  content: {flexGrow: 1, justifyContent: 'center', padding: 20},
  lang: {position: 'absolute', top: 12, right: 12},
  logo: {
    alignSelf: 'center',
    width: 68,
    height: 68,
    borderRadius: 18,
    backgroundColor: colors.yellow,
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 40,
  },
  logoMark: {fontSize: 38, fontWeight: '900', color: colors.brown},
  title: {
    textAlign: 'center',
    color: colors.cream,
    fontSize: 24,
    fontWeight: '800',
    marginVertical: 18,
  },
  card: {backgroundColor: colors.cream, borderRadius: 24, padding: 20},
  cardTitle: {fontSize: 16, fontWeight: '800', color: colors.brown, marginBottom: 10},
  hint: {color: colors.muted, marginBottom: 12},
  sent: {
    color: colors.green,
    backgroundColor: colors.greenSoft,
    borderRadius: 10,
    padding: 10,
    fontWeight: '700',
  },
  link: {alignSelf: 'center', marginTop: 14, padding: 4},
  linkText: {color: colors.muted, fontWeight: '600'},
  error: {
    color: colors.danger,
    backgroundColor: colors.dangerSoft,
    borderRadius: 10,
    padding: 10,
    marginBottom: 12,
  },
});
