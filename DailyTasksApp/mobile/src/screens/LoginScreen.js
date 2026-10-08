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
import {normalizeServer} from '../api';
import {useAuth} from '../auth';
import {errorMessage} from '../format';
import LanguagePicker from '../LanguagePicker';
import {colors} from '../theme';
import {Button, Field, Input} from '../ui';

export default function LoginScreen() {
  const {t} = useTranslation();
  const auth = useAuth();
  const [server, setServer] = useState(auth.server);
  const [name, setName] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  const canSubmit = server.trim() && name.trim() && password;

  async function submit() {
    if (!canSubmit) {
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await auth.login(normalizeServer(server), name.trim(), password);
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

        <View style={styles.card}>
          <Field label={t('login.server')} hint={t('login.serverHint')}>
            <Input
              value={server}
              onChangeText={setServer}
              autoCapitalize="none"
              autoCorrect={false}
              keyboardType="url"
              placeholder="http://"
            />
          </Field>
          <Field label={t('login.name')}>
            <Input
              value={name}
              onChangeText={setName}
              autoCapitalize="words"
              autoComplete="username"
            />
          </Field>
          <Field label={t('login.password')}>
            <View style={styles.passwordRow}>
              <Input
                value={password}
                onChangeText={setPassword}
                secureTextEntry={!showPassword}
                autoCapitalize="none"
                autoCorrect={false}
                autoComplete="password"
                onSubmitEditing={submit}
                style={styles.passwordInput}
              />
              <Pressable
                onPress={() => setShowPassword(v => !v)}
                hitSlop={8}
                style={styles.eye}
                accessibilityLabel={
                  showPassword
                    ? t('login.hidePassword')
                    : t('login.showPassword')
                }>
                <Text style={[styles.eyeText, !showPassword && styles.eyeOff]}>
                  👁
                </Text>
              </Pressable>
            </View>
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
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
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
  passwordRow: {justifyContent: 'center'},
  // Room on the right so typed text never runs under the eye.
  passwordInput: {paddingRight: 52},
  eye: {
    position: 'absolute',
    right: 6,
    width: 44,
    height: 44,
    alignItems: 'center',
    justifyContent: 'center',
  },
  eyeText: {fontSize: 22},
  eyeOff: {opacity: 0.35},
  error: {
    color: colors.danger,
    backgroundColor: colors.dangerSoft,
    borderRadius: 10,
    padding: 10,
    marginBottom: 12,
  },
});
