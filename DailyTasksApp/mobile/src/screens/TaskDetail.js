import React, {useState} from 'react';
import {Modal, ScrollView, StyleSheet, Text, View} from 'react-native';
import {useTranslation} from 'react-i18next';
import {formatDateTime, formatDay} from '../format';
import {StoredPhoto} from '../Photo';
import {colors, priorityColor} from '../theme';
import {hasTranslation, localized} from '../translate';
import TranslationToggle from '../TranslationToggle';
import {Button, Glyph, IconButton, styles as ui} from '../ui';

// Full-screen view of one task: everything the card shortens, and the photo at full width.
// Actions are handed back to the list screen, which owns the forms.
export default function TaskDetail({
  task,
  isManager,
  canComplete,
  onComplete,
  onEdit,
  onDelete,
  onClose,
}) {
  const {t, i18n} = useTranslation();
  const [showOriginal, setShowOriginal] = useState(false);
  const text = field => localized(task, field, i18n.language, showOriginal);
  const translated = hasTranslation(
    task,
    ['title', 'description', 'comment'],
    i18n.language,
  );
  const carried = task.originalDate !== task.date;
  // Completed tasks are a record: no edit, no delete.
  const managerCanChange = isManager && !task.completed && (onEdit || onDelete);
  const showActions = canComplete || managerCanChange;

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
            {text('title')}
          </Text>
        </View>

        <ScrollView contentContainerStyle={styles.content}>
          <View style={styles.titleRow}>
            {task.completed ? (
              <View style={styles.doneDot}>
                <Text style={styles.doneDotText}>✓</Text>
              </View>
            ) : null}
            <Text style={styles.title}>{text('title')}</Text>
          </View>
          {translated ? (
            <TranslationToggle
              showOriginal={showOriginal}
              onToggle={() => setShowOriginal(v => !v)}
              style={styles.translation}
            />
          ) : null}

          <View style={styles.metaRow}>
            <View
              style={[
                styles.badge,
                {backgroundColor: priorityColor[task.priority]},
              ]}>
              <Text
                style={[
                  styles.badgeText,
                  task.priority === 'High' && {color: colors.white},
                  task.priority === 'Low' && {color: colors.muted},
                ]}>
                {t(`priority.${task.priority}`)}
              </Text>
            </View>
            <View style={styles.meta}>
              <Glyph
                name={task.shift === 'Morning' ? 'sun' : 'sunset'}
                size={15}
                color={colors.yellow}
              />
              <Text style={styles.metaText}>{t(`shift.${task.shift}`)}</Text>
            </View>
            <View style={styles.meta}>
              <Text style={styles.metaText}>📅 {formatDay(task.date)}</Text>
            </View>
          </View>
          {carried ? (
            <View style={[ui.chip, styles.carried]}>
              <Text style={ui.chipText}>
                ↪{' '}
                {t('tasks.carriedOver', {
                  date: formatDay(task.originalDate, false),
                })}
              </Text>
            </View>
          ) : null}

          {task.description ? (
            <Section label={t('tasks.description')}>
              <Text style={styles.body}>{text('description')}</Text>
            </Section>
          ) : null}

          <Section label={t('tasks.assignees')}>
            <View style={styles.chips}>
              {task.assignees.map(a => (
                <View key={a.id} style={[ui.chip, styles.personChip]}>
                  <Text style={[ui.chipText, styles.bigChip]}>👤 {a.name}</Text>
                </View>
              ))}
            </View>
          </Section>

          {task.photoIds?.length ? (
            <Section label={`${t('tasks.photos')} · ${task.photoIds.length}`}>
              <View style={styles.photos}>
                {task.photoIds.map(photoId => (
                  <StoredPhoto
                    key={photoId}
                    path={`/tasks/${task.id}/photos/${photoId}`}
                    large
                  />
                ))}
              </View>
            </Section>
          ) : null}

          {task.completed ? (
            <View style={styles.completion}>
              <Text style={styles.completedBy}>
                ✓{' '}
                {t('tasks.completedBy', {
                  name: task.completedBy?.name ?? '—',
                  time: formatDateTime(task.completedAt),
                })}
              </Text>
              {task.completionComment ? (
                <View>
                  <Text style={styles.label}>{t('tasks.comment')}</Text>
                  <Text style={styles.body}>{text('comment')}</Text>
                </View>
              ) : null}
              {task.hasPhoto ? (
                <StoredPhoto path={`/tasks/${task.id}/photo`} large />
              ) : null}
              {(task.completionPhotoIds ?? []).map(photoId => (
                <StoredPhoto
                  key={photoId}
                  path={`/tasks/${task.id}/photos/${photoId}`}
                  large
                />
              ))}
            </View>
          ) : null}
        </ScrollView>

        {showActions ? (
          <View style={styles.footer}>
            {managerCanChange && onDelete ? (
              <Button
                title={t('common.delete')}
                icon="trash"
                variant="ghost"
                onPress={onDelete}
                style={styles.flex}
              />
            ) : null}
            {managerCanChange && onEdit ? (
              <Button
                title={t('common.edit')}
                icon="edit"
                onPress={onEdit}
                style={styles.flex}
              />
            ) : null}
            {canComplete ? (
              <Button
                title={t('tasks.complete')}
                icon="check"
                variant="green"
                onPress={onComplete}
                style={styles.flex}
              />
            ) : null}
          </View>
        ) : null}
      </View>
    </Modal>
  );
}

function Section({label, children}) {
  return (
    <View style={styles.section}>
      <Text style={styles.label}>{label}</Text>
      {children}
    </View>
  );
}

const styles = StyleSheet.create({
  flex: {flex: 1},
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
  titleRow: {flexDirection: 'row', alignItems: 'flex-start', gap: 10},
  doneDot: {
    width: 28,
    height: 28,
    borderRadius: 14,
    backgroundColor: colors.green,
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 2,
  },
  doneDotText: {color: colors.white, fontWeight: '900', fontSize: 16},
  title: {flex: 1, fontSize: 22, fontWeight: '800', color: colors.ink},
  translation: {marginTop: 10},
  metaRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    alignItems: 'center',
    gap: 8,
    marginTop: 14,
  },
  badge: {borderRadius: 999, paddingHorizontal: 12, paddingVertical: 4},
  badgeText: {fontSize: 13, fontWeight: '700', color: colors.ink},
  meta: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: colors.white,
    borderRadius: 999,
    paddingHorizontal: 10,
    paddingVertical: 4,
  },
  metaText: {color: colors.brown, fontWeight: '600'},
  carried: {
    alignSelf: 'flex-start',
    backgroundColor: colors.yellowSoft,
    marginTop: 10,
  },
  section: {marginTop: 20},
  label: {
    fontSize: 13,
    fontWeight: '800',
    color: colors.brown,
    textTransform: 'uppercase',
    marginBottom: 6,
  },
  body: {fontSize: 16, lineHeight: 23, color: colors.ink},
  chips: {flexDirection: 'row', flexWrap: 'wrap', gap: 8},
  photos: {gap: 12},
  personChip: {backgroundColor: colors.white},
  bigChip: {fontSize: 14, paddingVertical: 2},
  completion: {
    backgroundColor: colors.greenSoft,
    borderRadius: 16,
    padding: 14,
    marginTop: 22,
    gap: 12,
  },
  completedBy: {color: colors.green, fontWeight: '800', fontSize: 15},
  footer: {
    flexDirection: 'row',
    gap: 8,
    padding: 12,
    borderTopWidth: 1,
    borderTopColor: colors.creamDark,
    backgroundColor: colors.cream,
  },
});
