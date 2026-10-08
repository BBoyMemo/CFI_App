import React, {useState} from 'react';
import {Alert, Pressable, StyleSheet, Text, View} from 'react-native';
import {useTranslation} from 'react-i18next';
import {api, photoPart} from '../api';
import CalendarSheet from '../Calendar';
import {
  addDays,
  errorMessage,
  fieldError,
  formatDay,
  siteToday,
} from '../format';
import {MultiPhotoInput} from '../Photo';
import {colors} from '../theme';
import {Button, Field, IconButton, Input, Segmented, Sheet} from '../ui';

const PRIORITIES = ['Low', 'Medium', 'High'];
const MAX_PHOTOS = 5;

// Create and edit in one form. Editing also moves a task to another date.
export function TaskFormSheet({
  task,
  defaultDate,
  engineers,
  onSaved,
  onClose,
}) {
  const {t} = useTranslation();
  const [form, setForm] = useState(() => ({
    title: task?.title ?? '',
    description: task?.description ?? '',
    date: task?.date ?? defaultDate,
    priority: task?.priority ?? 'Medium',
    shift: task?.shift ?? 'Morning',
    assigneeIds: task?.assignees.map(a => a.id) ?? [],
  }));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [calendarOpen, setCalendarOpen] = useState(false);
  // Photos: the stored ones (minus any the manager removes) and newly picked ones.
  const [storedIds, setStoredIds] = useState(() => task?.photoIds ?? []);
  const [added, setAdded] = useState([]);
  const today = siteToday();

  const set = (key, value) => setForm(f => ({...f, [key]: value}));
  const toggle = id =>
    set(
      'assigneeIds',
      form.assigneeIds.includes(id)
        ? form.assigneeIds.filter(x => x !== id)
        : [...form.assigneeIds, id],
    );

  async function submit() {
    setBusy(true);
    setError(null);
    try {
      const payload = {
        ...form,
        title: form.title.trim(),
        description: form.description.trim() || null,
      };
      const saved = task
        ? await api.updateTask(task.id, payload)
        : await api.createTask(payload);
      // The task itself is saved; photos follow one by one. A failed photo does not undo the task.
      const failed = await syncPhotos(saved.id);
      if (failed) {
        Alert.alert(t('tasks.photos'), errorMessage(t, failed));
      }
      onSaved(saved);
    } catch (err) {
      setError(err);
      setBusy(false);
    }
  }

  async function syncPhotos(taskId) {
    let failed = null;
    for (const photoId of (task?.photoIds ?? []).filter(
      id => !storedIds.includes(id),
    )) {
      await api
        .deleteTaskPhoto(taskId, photoId)
        .catch(err => (failed = failed ?? err));
    }
    for (const asset of added) {
      const photoForm = new FormData();
      photoForm.append('photo', photoPart(asset));
      await api
        .addTaskPhoto(taskId, photoForm)
        .catch(err => (failed = failed ?? err));
    }
    return failed;
  }

  const valid = form.title.trim() && form.assigneeIds.length > 0;

  return (
    <Sheet
      title={task ? t('tasks.edit') : t('tasks.new')}
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
            onPress={submit}
            disabled={!valid}
            busy={busy}
            style={styles.flex}
          />
        </>
      }>
      <Field label={t('tasks.title')} error={fieldError(t, error, 'title')}>
        <Input
          value={form.title}
          onChangeText={v => set('title', v)}
          maxLength={200}
        />
      </Field>
      <Field
        label={t('tasks.description')}
        optional
        error={fieldError(t, error, 'description')}>
        <Input
          value={form.description}
          onChangeText={v => set('description', v)}
          maxLength={2000}
          multiline
        />
      </Field>
      <Field label={t('tasks.photos')} optional>
        <MultiPhotoInput
          stored={
            task
              ? storedIds.map(id => ({
                  id,
                  path: `/tasks/${task.id}/photos/${id}`,
                }))
              : []
          }
          added={added}
          onRemoveStored={id => setStoredIds(ids => ids.filter(x => x !== id))}
          onAdd={assets =>
            setAdded(list =>
              [...list, ...assets].slice(0, MAX_PHOTOS - storedIds.length),
            )
          }
          onRemoveAdded={index =>
            setAdded(list => list.filter((_, i) => i !== index))
          }
          max={MAX_PHOTOS}
        />
      </Field>
      <Field label={t('tasks.date')} error={fieldError(t, error, 'date')}>
        <View style={styles.dateRow}>
          <IconButton
            icon="left"
            size={26}
            color={form.date <= today ? colors.creamDark : colors.brown}
            onPress={() =>
              form.date > today && set('date', addDays(form.date, -1))
            }
            label={t('common.previousDay')}
          />
          <Pressable
            style={styles.dateTextWrap}
            onPress={() => setCalendarOpen(true)}>
            <Text style={styles.dateText}>📅 {formatDay(form.date)}</Text>
          </Pressable>
          <IconButton
            icon="right"
            size={26}
            color={colors.brown}
            onPress={() => set('date', addDays(form.date, 1))}
            label={t('common.nextDay')}
          />
        </View>
      </Field>
      <Field label={t('tasks.shift')} error={fieldError(t, error, 'shift')}>
        <Segmented
          value={form.shift}
          onChange={v => set('shift', v)}
          options={[
            {value: 'Morning', icon: 'sun', label: t('shift.Morning')},
            {value: 'Afternoon', icon: 'sunset', label: t('shift.Afternoon')},
          ]}
        />
      </Field>
      <Field
        label={t('tasks.priority')}
        error={fieldError(t, error, 'priority')}>
        <Segmented
          value={form.priority}
          onChange={v => set('priority', v)}
          options={PRIORITIES.map(p => ({value: p, label: t(`priority.${p}`)}))}
        />
      </Field>
      <Field
        label={t('tasks.assignees')}
        error={fieldError(t, error, 'assigneeIds')}>
        {engineers.length === 0 ? (
          <Text style={styles.muted}>{t('tasks.noEngineers')}</Text>
        ) : (
          <View style={styles.chips}>
            {engineers.map(u => {
              const on = form.assigneeIds.includes(u.id);
              return (
                <Pressable
                  key={u.id}
                  onPress={() => toggle(u.id)}
                  style={[styles.person, on && styles.personOn]}>
                  <Text style={[styles.personText, on && styles.personTextOn]}>
                    {on ? '✓ ' : ''}
                    {u.name}
                  </Text>
                </Pressable>
              );
            })}
          </View>
        )}
      </Field>
      {error && !error.fieldErrors ? (
        <Text style={styles.error}>{errorMessage(t, error)}</Text>
      ) : null}
      {calendarOpen ? (
        <CalendarSheet
          value={form.date}
          min={today}
          onSelect={d => set('date', d)}
          onClose={() => setCalendarOpen(false)}
        />
      ) : null}
    </Sheet>
  );
}

// Comment and photo are optional; one tap on the green button is enough.
export function CompleteSheet({task, onDone, onClose}) {
  const {t} = useTranslation();
  const [comment, setComment] = useState('');
  const [photos, setPhotos] = useState([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  async function submit() {
    setBusy(true);
    setError(null);
    try {
      const form = new FormData();
      form.append('comment', comment.trim());
      // Each picked photo goes as its own "photo" field; the server takes up to five.
      photos.forEach(p => form.append('photo', photoPart(p)));
      onDone(await api.completeTask(task.id, form));
    } catch (err) {
      setError(err);
      setBusy(false);
    }
  }

  return (
    <Sheet
      title={t('tasks.completeTitle')}
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
            title={t('tasks.complete')}
            icon="check"
            variant="green"
            onPress={submit}
            busy={busy}
            style={styles.flex}
          />
        </>
      }>
      <Text style={styles.taskTitle}>{task.title}</Text>
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
  muted: {color: colors.muted},
  error: {color: colors.danger, marginTop: 8},
  taskTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: colors.ink,
    marginBottom: 14,
  },
  dateRow: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.white,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: colors.creamDark,
  },
  dateTextWrap: {flex: 1, paddingVertical: 8},
  dateText: {
    textAlign: 'center',
    fontSize: 15,
    fontWeight: '600',
    color: colors.brown,
  },
  chips: {flexDirection: 'row', flexWrap: 'wrap', gap: 8},
  person: {
    borderRadius: 999,
    borderWidth: 1,
    borderColor: colors.creamDark,
    backgroundColor: colors.white,
    paddingHorizontal: 14,
    paddingVertical: 8,
  },
  personOn: {backgroundColor: colors.brown, borderColor: colors.brown},
  personText: {color: colors.brown, fontWeight: '600'},
  personTextOn: {color: colors.cream},
});
