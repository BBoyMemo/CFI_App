import React from 'react';
import {
  ActivityIndicator,
  Modal,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import {useTranslation} from 'react-i18next';
import {colors} from './theme';

// Unicode/emoji glyphs instead of an icon font: no extra native dependency in the APK.
const GLYPHS = {
  check: '✓',
  plus: '+',
  left: '‹',
  right: '›',
  edit: '✎',
  trash: '🗑',
  camera: '📷',
  gallery: '🖼',
  sun: '☀',
  sunset: '🌇',
  truck: '🚚',
  user: '👤',
  orders: '🛒',
  tasks: '☰',
  history: '🕘',
  team: '👥',
  logout: '🚪',
  comment: '💬',
  carry: '↪',
  shield: '🛡',
  wrench: '🔧',
  close: '✕',
  alert: '⚠',
};

export function Glyph({name, size = 18, color = colors.brown, style}) {
  return <Text style={[{fontSize: size, color, lineHeight: size * 1.25}, style]}>{GLYPHS[name]}</Text>;
}

const VARIANTS = {
  primary: {bg: colors.yellow, fg: colors.ink},
  green: {bg: colors.green, fg: colors.white},
  ghost: {bg: 'transparent', fg: colors.brown},
  danger: {bg: colors.danger, fg: colors.white},
};

export function Button({title, icon, onPress, variant = 'primary', disabled, busy, style}) {
  const v = VARIANTS[variant];
  return (
    <Pressable
      onPress={onPress}
      disabled={disabled || busy}
      style={({pressed}) => [
        styles.button,
        {backgroundColor: v.bg, opacity: disabled ? 0.5 : pressed ? 0.85 : 1},
        style,
      ]}>
      {busy ? (
        <ActivityIndicator color={v.fg} />
      ) : (
        <>
          {icon && <Glyph name={icon} color={v.fg} size={17} />}
          {title ? <Text style={[styles.buttonText, {color: v.fg}]}>{title}</Text> : null}
        </>
      )}
    </Pressable>
  );
}

export function IconButton({icon, onPress, color = colors.muted, size = 20, label}) {
  return (
    <Pressable
      onPress={onPress}
      accessibilityLabel={label}
      hitSlop={6}
      style={({pressed}) => [styles.iconButton, pressed && {backgroundColor: colors.creamDark}]}>
      <Glyph name={icon} size={size} color={color} />
    </Pressable>
  );
}

export function Field({label, optional, error, children, hint}) {
  const {t} = useTranslation();
  return (
    <View style={styles.field}>
      {label ? (
        <Text style={styles.label}>
          {label}
          {optional ? <Text style={styles.optional}> ({t('common.optional')})</Text> : null}
        </Text>
      ) : null}
      {children}
      {error ? <Text style={styles.error}>{error}</Text> : hint ? <Text style={styles.hint}>{hint}</Text> : null}
    </View>
  );
}

export function Input(props) {
  return (
    <TextInput
      placeholderTextColor={colors.muted}
      {...props}
      style={[styles.input, props.multiline && styles.multiline, props.style]}
    />
  );
}

export function Segmented({options, value, onChange}) {
  return (
    <View style={styles.segmented}>
      {options.map(o => {
        const on = o.value === value;
        return (
          <Pressable
            key={o.value}
            onPress={() => onChange(o.value)}
            style={[styles.segment, on && styles.segmentOn]}
            accessibilityState={{selected: on}}>
            {o.icon && <Glyph name={o.icon} size={16} color={on ? colors.brown : colors.muted} />}
            {o.label ? <Text style={[styles.segmentText, on && styles.segmentTextOn]}>{o.label}</Text> : null}
          </Pressable>
        );
      })}
    </View>
  );
}

// Bottom sheet used for every form.
export function Sheet({title, onClose, children, footer}) {
  const {t} = useTranslation();
  return (
    // No KeyboardAvoidingView: Android resizes the modal window for the keyboard itself, and
    // a second adjustment left the sheet short after the keyboard closed.
    <Modal visible transparent animationType="slide" onRequestClose={onClose}>
      <View style={styles.backdrop}>
        <Pressable style={StyleSheet.absoluteFill} onPress={onClose} />
        <View style={styles.sheet}>
          <View style={styles.sheetHeader}>
            <Text style={styles.sheetTitle}>{title}</Text>
            <IconButton icon="close" onPress={onClose} label={t('common.close')} />
          </View>
          <ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={styles.sheetBody}>
            {children}
          </ScrollView>
          {footer ? <View style={styles.sheetFooter}>{footer}</View> : null}
        </View>
      </View>
    </Modal>
  );
}

export function ConfirmSheet({message, onConfirm, onClose, busy, error}) {
  const {t} = useTranslation();
  return (
    <Sheet
      title={message}
      onClose={onClose}
      footer={
        <>
          <Button title={t('common.cancel')} variant="ghost" onPress={onClose} style={styles.flex} />
          <Button title={t('common.delete')} icon="trash" variant="danger" onPress={onConfirm} busy={busy} style={styles.flex} />
        </>
      }>
      {error ? <Text style={styles.error}>{error}</Text> : null}
    </Sheet>
  );
}

export function ErrorBox({message, onRetry}) {
  const {t} = useTranslation();
  return (
    <View style={styles.errorBox}>
      <Glyph name="alert" color={colors.danger} />
      <Text style={[styles.flex, {color: colors.danger}]}>{message}</Text>
      {onRetry ? <Button title={t('common.retry')} variant="ghost" onPress={onRetry} /> : null}
    </View>
  );
}

export function Fab({onPress, label}) {
  return (
    <Pressable
      onPress={onPress}
      accessibilityLabel={label}
      style={({pressed}) => [styles.fab, pressed && {backgroundColor: '#C98D22'}]}>
      <Text style={styles.fabText}>+</Text>
    </Pressable>
  );
}

export const styles = StyleSheet.create({
  flex: {flex: 1},
  button: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    borderRadius: 12,
    paddingHorizontal: 16,
    minHeight: 46,
  },
  buttonText: {fontSize: 15, fontWeight: '700'},
  iconButton: {width: 40, height: 40, borderRadius: 10, alignItems: 'center', justifyContent: 'center'},
  field: {marginBottom: 16},
  label: {fontSize: 14, fontWeight: '600', color: colors.brown, marginBottom: 6},
  optional: {fontWeight: '400', color: colors.muted},
  error: {color: colors.danger, fontSize: 13, marginTop: 4},
  hint: {color: colors.muted, fontSize: 12, marginTop: 4},
  input: {
    backgroundColor: colors.white,
    borderWidth: 1,
    borderColor: colors.creamDark,
    borderRadius: 12,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 16,
    color: colors.ink,
  },
  multiline: {minHeight: 90, textAlignVertical: 'top'},
  segmented: {flexDirection: 'row', backgroundColor: colors.creamDark, borderRadius: 12, padding: 4, gap: 4},
  segment: {
    flex: 1,
    flexDirection: 'row',
    gap: 6,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 9,
    paddingVertical: 9,
  },
  segmentOn: {backgroundColor: colors.white, elevation: 1},
  segmentText: {fontSize: 14, fontWeight: '600', color: colors.muted},
  segmentTextOn: {color: colors.brown},
  backdrop: {flex: 1, justifyContent: 'flex-end', backgroundColor: 'rgba(43,33,24,0.45)'},
  sheet: {backgroundColor: colors.cream, borderTopLeftRadius: 24, borderTopRightRadius: 24, maxHeight: '92%'},
  sheetHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 20,
    paddingTop: 14,
    paddingBottom: 6,
  },
  sheetTitle: {fontSize: 18, fontWeight: '700', color: colors.brown, flex: 1},
  sheetBody: {paddingHorizontal: 20, paddingBottom: 12},
  sheetFooter: {
    flexDirection: 'row',
    gap: 8,
    paddingHorizontal: 20,
    paddingVertical: 12,
    borderTopWidth: 1,
    borderTopColor: colors.creamDark,
  },
  errorBox: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    backgroundColor: colors.dangerSoft,
    borderRadius: 14,
    padding: 12,
    marginBottom: 12,
  },
  fab: {
    position: 'absolute',
    right: 20,
    bottom: 20,
    width: 60,
    height: 60,
    borderRadius: 30,
    backgroundColor: colors.yellow,
    alignItems: 'center',
    justifyContent: 'center',
    elevation: 6,
  },
  fabText: {fontSize: 34, lineHeight: 38, color: colors.ink, fontWeight: '500'},
  card: {
    backgroundColor: colors.white,
    borderRadius: 18,
    borderWidth: 1,
    borderColor: colors.creamDark,
    overflow: 'hidden',
  },
  empty: {
    borderWidth: 1,
    borderStyle: 'dashed',
    borderColor: colors.creamDark,
    borderRadius: 16,
    paddingVertical: 18,
    textAlign: 'center',
    color: colors.muted,
  },
  chip: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: colors.cream,
    borderRadius: 999,
    paddingHorizontal: 9,
    paddingVertical: 3,
  },
  chipText: {fontSize: 12, color: colors.brown},
});
