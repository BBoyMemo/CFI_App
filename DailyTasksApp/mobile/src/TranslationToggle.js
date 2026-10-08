import React from 'react';
import {Pressable, StyleSheet, Text} from 'react-native';
import {useTranslation} from 'react-i18next';
import {colors} from './theme';

// Small 🌐 chip shown next to translated text; tapping switches between translation and original.
export default function TranslationToggle({showOriginal, onToggle, style}) {
  const {t} = useTranslation();
  return (
    <Pressable onPress={onToggle} hitSlop={6} style={[styles.chip, style]}>
      <Text style={styles.text}>
        🌐{' '}
        {showOriginal
          ? t('translation.showTranslation')
          : `${t('translation.translated')} · ${t('translation.showOriginal')}`}
      </Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  chip: {
    alignSelf: 'flex-start',
    backgroundColor: colors.cream,
    borderRadius: 999,
    paddingHorizontal: 9,
    paddingVertical: 3,
  },
  text: {fontSize: 12, color: colors.muted, fontWeight: '600'},
});
