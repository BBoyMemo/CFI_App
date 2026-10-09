import React, {useState} from 'react';
import {StyleSheet, Text, View} from 'react-native';
import {useTranslation} from 'react-i18next';
import {api, photoPart} from '../api';
import {errorMessage, fieldError, formatDateTime} from '../format';
import {MultiPhotoInput, StoredPhoto} from '../Photo';
import {colors} from '../theme';
import {hasTranslation, localized} from '../translate';
import TranslationToggle from '../TranslationToggle';
import {Button, Field, Input, Segmented, Sheet} from '../ui';

const MAX_PHOTOS = 5;

// Who may do what with a task, the same rules the server enforces.
//  - work:     add a progress / completion card while the task is open or in progress
//              (people it is assigned to, and managers);
//  - followUp: add a card once it is completed (anyone);
//  - edit / delete: managers; delete only while nobody has reported on it.
export function taskRules(task, user, isManager) {
  const assigned = task.assignees.some(a => a.id === user.id);
  const completed = task.status === 'Completed';
  return {
    work: !completed && (isManager || assigned),
    followUp: completed,
    edit: isManager && !completed,
    delete: isManager && task.status === 'Open' && task.updates.length === 0,
  };
}

// Small status chip for open-but-started tasks.
export function InProgressChip() {
  const {t} = useTranslation();
  return (
    <View style={styles.progressChip}>
      <Text style={styles.progressChipText}>🔧 {t('status.InProgress')}</Text>
    </View>
  );
}

// One card under a task: outcome, who, when, comment and photos.
export function UpdateCard({task, update, large = false, compact = false}) {
  const {t, i18n} = useTranslation();
  const [showOriginal, setShowOriginal] = useState(false);
  const completed = update.outcome === 'Completed';
  const comment = localized(update, 'comment', i18n.language, showOriginal);

  return (
    <View
      style={[styles.card, completed ? styles.cardDone : styles.cardProgress]}>
      <View style={styles.header}>
        <Text
          style={[
            styles.outcome,
            completed ? styles.outcomeDone : styles.outcomeProgress,
          ]}>
          {completed
            ? `✓ ${t('status.Completed')}`
            : `🔧 ${t('status.InProgress')}`}
        </Text>
        <Text style={styles.meta} numberOfLines={1}>
          {update.author.name} · {formatDateTime(update.createdAt)}
        </Text>
      </View>
      {comment ? (
        <Text style={styles.comment} numberOfLines={compact ? 2 : undefined}>
          {comment}
        </Text>
      ) : null}
      {!compact && hasTranslation(update, ['comment'], i18n.language) ? (
        <TranslationToggle
          showOriginal={showOriginal}
          onToggle={() => setShowOriginal(v => !v)}
        />
      ) : null}
      {update.photoIds.length ? (
        <View style={large ? styles.photosLarge : styles.photos}>
          {(compact ? update.photoIds.slice(0, 3) : update.photoIds).map(
            photoId => (
              <StoredPhoto
                key={photoId}
                path={`/tasks/${task.id}/photos/${photoId}`}
                large={large}
              />
            ),
          )}
        </View>
      ) : null}
    </View>
  );
}

// Adds a card: the result (still in progress / completed), an optional comment and photos.
export function UpdateSheet({
  task,
  initialOutcome = 'Completed',
  onDone,
  onClose,
}) {
  const {t, i18n} = useTranslation();
  const [outcome, setOutcome] = useState(initialOutcome);
  const [comment, setComment] = useState('');
  const [photos, setPhotos] = useState([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const takingBack = task.status === 'Completed' && outcome === 'InProgress';

  async function submit() {
    setBusy(true);
    setError(null);
    try {
      const form = new FormData();
      form.append('outcome', outcome);
      form.append('comment', comment.trim());
      // Each photo goes as its own "photo" field; the server takes up to five.
      photos.forEach(p => form.append('photo', photoPart(p)));
      onDone(await api.addTaskUpdate(task.id, form));
    } catch (err) {
      setError(err);
      setBusy(false);
    }
  }

  return (
    <Sheet
      title={t('tasks.updateTitle')}
      onClose={onClose}
      footer={
        <>
          <Button
            title={t('common.cancel')}
            variant="ghost"
            onPress={onClose}
            style={styles.flex}
          />
          <Button
            title={t('common.save')}
            icon={outcome === 'Completed' ? 'check' : 'wrench'}
            variant={outcome === 'Completed' ? 'green' : 'primary'}
            onPress={submit}
            busy={busy}
            style={styles.flex}
          />
        </>
      }>
      <Text style={styles.taskTitle}>{localized(task, 'title', i18n.language)}</Text>
      <Field label={t('tasks.outcome')} error={fieldError(t, error, 'outcome')}>
        <Segmented
          value={outcome}
          onChange={setOutcome}
          options={[
            {
              value: 'InProgress',
              icon: 'wrench',
              label: t('status.InProgress'),
            },
            {value: 'Completed', icon: 'check', label: t('status.Completed')},
          ]}
        />
      </Field>
      {takingBack ? (
        <Text style={styles.hint}>{t('tasks.reopenHint')}</Text>
      ) : null}
      <Field
        label={t('tasks.comment')}
        optional
        error={fieldError(t, error, 'comment')}>
        <Input
          value={comment}
          onChangeText={setComment}
          maxLength={2000}
          multiline
        />
      </Field>
      <MultiPhotoInput
        stored={[]}
        added={photos}
        onRemoveStored={() => {}}
        onAdd={assets =>
          setPhotos(list => [...list, ...assets].slice(0, MAX_PHOTOS))
        }
        onRemoveAdded={index =>
          setPhotos(list => list.filter((_, i) => i !== index))
        }
        max={MAX_PHOTOS}
        error={fieldError(t, error, 'photo')}
      />
      {error && !error.fieldErrors ? (
        <Text style={styles.error}>{errorMessage(t, error)}</Text>
      ) : null}
    </Sheet>
  );
}

const styles = StyleSheet.create({
  flex: {flex: 1},
  card: {borderRadius: 14, padding: 12, gap: 6, borderLeftWidth: 4},
  cardDone: {backgroundColor: colors.greenSoft, borderLeftColor: colors.green},
  cardProgress: {
    backgroundColor: colors.yellowSoft,
    borderLeftColor: colors.yellow,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    flexWrap: 'wrap',
  },
  outcome: {fontWeight: '800', fontSize: 13},
  outcomeDone: {color: colors.green},
  outcomeProgress: {color: colors.brown},
  meta: {flexShrink: 1, color: colors.muted, fontSize: 13},
  comment: {color: colors.ink, fontSize: 15, lineHeight: 21},
  photos: {flexDirection: 'row', flexWrap: 'wrap', gap: 8},
  photosLarge: {gap: 10},
  progressChip: {
    backgroundColor: colors.yellowSoft,
    borderRadius: 999,
    paddingHorizontal: 9,
    paddingVertical: 3,
  },
  progressChipText: {fontSize: 12, fontWeight: '700', color: colors.brown},
  taskTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: colors.ink,
    marginBottom: 14,
  },
  hint: {
    color: colors.brown,
    backgroundColor: colors.yellowSoft,
    borderRadius: 10,
    padding: 10,
    marginBottom: 14,
  },
  error: {color: colors.danger, marginTop: 8},
});
