import React, {useState} from 'react';
import {Modal, ScrollView, StyleSheet, Text, View} from 'react-native';
import {useTranslation} from 'react-i18next';
import {formatDateTime} from '../format';
import {StoredPhoto} from '../Photo';
import {colors} from '../theme';
import {hasTranslation, localized} from '../translate';
import TranslationToggle from '../TranslationToggle';
import {Button, IconButton} from '../ui';

// Full-screen view of one order with its photo at full width. Actions (manager, New only)
// are handed back to the list screen.
export default function OrderDetail({
  order,
  isManager,
  busy,
  error,
  onMarkOrdered,
  onDelete,
  onClose,
}) {
  const {t, i18n} = useTranslation();
  const [showOriginal, setShowOriginal] = useState(false);
  const description = localized(
    order,
    'description',
    i18n.language,
    showOriginal,
  );
  const translated = hasTranslation(order, ['description'], i18n.language);
  const isNew = order.status === 'New';
  const canChange = isManager && isNew;

  return (
    <Modal visible animationType="slide" onRequestClose={onClose}>
      <View style={styles.screen}>
        <View style={styles.header}>
          <IconButton
            icon="left"
            size={28}
            color={colors.cream}
            onPress={onClose}
            label={t('common.close')}
          />
          <Text style={styles.headerTitle} numberOfLines={1}>
            {description}
          </Text>
        </View>

        <ScrollView contentContainerStyle={styles.content}>
          <View
            style={[
              styles.status,
              isNew ? styles.statusNew : styles.statusOrdered,
            ]}>
            <Text
              style={[styles.statusText, !isNew && styles.statusTextOrdered]}>
              {isNew
                ? `🛒 ${t('orders.statusNew')}`
                : `🚚 ${t('orders.statusOrdered')}`}
            </Text>
          </View>

          <Text style={styles.description}>{description}</Text>
          {translated ? (
            <TranslationToggle
              showOriginal={showOriginal}
              onToggle={() => setShowOriginal(v => !v)}
              style={styles.translation}
            />
          ) : null}

          <View style={styles.people}>
            <Text style={styles.person}>
              👤{' '}
              {t('orders.requestedBy', {
                name: order.createdBy.name,
                date: formatDateTime(order.createdAt),
              })}
            </Text>
            {order.orderedAt ? (
              <Text style={[styles.person, styles.orderedBy]}>
                🚚{' '}
                {t('orders.orderedBy', {
                  name: order.orderedBy?.name ?? '—',
                  date: formatDateTime(order.orderedAt),
                })}
              </Text>
            ) : null}
          </View>

          {order.hasPhoto ? (
            <View style={styles.photo}>
              <StoredPhoto path={`/orders/${order.id}/photo`} large />
            </View>
          ) : null}

          {error ? <Text style={styles.error}>{error}</Text> : null}
        </ScrollView>

        {canChange ? (
          <View style={styles.footer}>
            <Button
              title={t('common.delete')}
              icon="trash"
              variant="ghost"
              onPress={onDelete}
              style={styles.flex}
            />
            <Button
              title={t('orders.markOrdered')}
              icon="truck"
              variant="green"
              onPress={onMarkOrdered}
              busy={busy}
              style={styles.wide}
            />
          </View>
        ) : null}
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  flex: {flex: 1},
  wide: {flex: 2},
  screen: {flex: 1, backgroundColor: colors.cream},
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    backgroundColor: colors.brown,
    paddingHorizontal: 8,
    paddingVertical: 8,
  },
  headerTitle: {flex: 1, color: colors.cream, fontSize: 17, fontWeight: '700'},
  content: {padding: 18, paddingBottom: 32},
  status: {
    alignSelf: 'flex-start',
    borderRadius: 999,
    paddingHorizontal: 12,
    paddingVertical: 5,
  },
  statusNew: {backgroundColor: colors.yellowSoft},
  statusOrdered: {backgroundColor: colors.greenSoft},
  statusText: {fontWeight: '800', color: colors.brown},
  statusTextOrdered: {color: colors.green},
  description: {
    fontSize: 21,
    lineHeight: 29,
    fontWeight: '700',
    color: colors.ink,
    marginTop: 14,
  },
  translation: {marginTop: 10},
  people: {gap: 8, marginTop: 16},
  person: {fontSize: 15, color: colors.muted},
  orderedBy: {color: colors.green, fontWeight: '700'},
  photo: {marginTop: 20},
  error: {color: colors.danger, marginTop: 14},
  footer: {
    flexDirection: 'row',
    gap: 8,
    padding: 12,
    borderTopWidth: 1,
    borderTopColor: colors.creamDark,
    backgroundColor: colors.cream,
  },
});
