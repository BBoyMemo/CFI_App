import React from 'react';
import {Pressable, StyleSheet, Text, View} from 'react-native';
import {useTranslation} from 'react-i18next';
import {LANGUAGES, setLanguage} from './i18n';
import {colors} from './theme';

// Four short codes in a row: one tap, no menu.
export default function LanguagePicker({dark = false}) {
  const {i18n} = useTranslation();
  return (
    <View style={styles.row}>
      {LANGUAGES.map(l => {
        const on = i18n.language === l.code;
        return (
          <Pressable key={l.code} onPress={() => setLanguage(l.code)} hitSlop={4} style={[styles.item, on && styles.on]}>
            <Text style={[styles.text, {color: dark ? colors.cream : colors.muted}, on && styles.textOn]}>
              {l.label}
            </Text>
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  row: {flexDirection: 'row', gap: 2},
  item: {paddingHorizontal: 7, paddingVertical: 4, borderRadius: 8},
  on: {backgroundColor: colors.yellow},
  text: {fontSize: 12, fontWeight: '700', opacity: 0.8},
  textOn: {color: colors.ink, opacity: 1},
});
