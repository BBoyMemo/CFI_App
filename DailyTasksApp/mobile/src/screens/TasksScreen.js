import React, {useCallback, useEffect, useMemo, useState} from 'react';
import {
  AppState,
  Pressable,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import {useTranslation} from 'react-i18next';
import {Gesture, GestureDetector} from 'react-native-gesture-handler';
import {api} from '../api';
import CalendarSheet from '../Calendar';
import {useAuth} from '../auth';
import {
  addDays,
  errorMessage,
  formatDateTime,
  formatDay,
  siteToday,
} from '../format';
import {StoredPhoto} from '../Photo';
import {colors, priorityColor} from '../theme';
import {hasTranslation, localized} from '../translate';
import TranslationToggle from '../TranslationToggle';
import {
  Button,
  ConfirmSheet,
  ErrorBox,
  Fab,
  Glyph,
  IconButton,
  styles as ui,
} from '../ui';
import TaskDetail from './TaskDetail';
import {CompleteSheet, TaskFormSheet} from './TaskSheets';

const SHIFTS = [
  {value: 'Morning', icon: 'sun', label: 'tasks.morning'},
  {value: 'Afternoon', icon: 'sunset', label: 'tasks.afternoon'},
];

export default function TasksScreen() {
  const {t} = useTranslation();
  const {user, isManager} = useAuth();
  const [date, setDate] = useState(siteToday);
  const [tasks, setTasks] = useState(null);
  const [engineers, setEngineers] = useState([]);
  const [error, setError] = useState(null);
  const [refreshing, setRefreshing] = useState(false);
  const [dialog, setDialog] = useState(null);
  const [deleteState, setDeleteState] = useState({busy: false, error: null});

  const load = useCallback(async () => {
    try {
      setError(null);
      setTasks(await api.tasks(date));
    } catch (err) {
      setError(err);
    }
  }, [date]);

  useEffect(() => {
    setTasks(null);
    load();
  }, [load]);

  // Coming back to the app picks up what others changed meanwhile.
  useEffect(() => {
    const sub = AppState.addEventListener(
      'change',
      s => s === 'active' && load(),
    );
    return () => sub.remove();
  }, [load]);

  useEffect(() => {
    if (isManager) {
      api
        .users()
        .then(users => setEngineers(users.filter(u => u.role === 'Engineer')))
        .catch(() => setEngineers([]));
    }
  }, [isManager]);

  // A sideways swipe on the list turns the day: left = next, right = previous.
  // Native gesture handler rules: the swipe only starts after the finger has clearly gone
  // sideways (15 pt) and gives up as soon as it has clearly gone up or down (20 pt), so taps
  // with a little wobble stay taps and vertical scrolling stays scrolling.
  const swipe = useMemo(
    () =>
      Gesture.Pan()
        .runOnJS(true)
        .activeOffsetX([-15, 15])
        .failOffsetY([-20, 20])
        .onEnd(e => {
          if (
            e.translationX < -50 ||
            (e.translationX < -20 && e.velocityX < -500)
          ) {
            setDate(d => addDays(d, 1));
          } else if (
            e.translationX > 50 ||
            (e.translationX > 20 && e.velocityX > 500)
          ) {
            setDate(d => addDays(d, -1));
          }
        }),
    [],
  );

  const today = siteToday();
  const close = () => {
    setDialog(null);
    setDeleteState({busy: false, error: null});
  };

  async function refresh() {
    setRefreshing(true);
    await load();
    setRefreshing(false);
  }

  async function confirmDelete() {
    setDeleteState({busy: true, error: null});
    try {
      await api.deleteTask(dialog.task.id);
      setTasks(list => list.filter(x => x.id !== dialog.task.id));
      close();
    } catch (err) {
      setDeleteState({busy: false, error: errorMessage(t, err)});
    }
  }

  const done = tasks?.filter(x => x.completed).length ?? 0;
  const canComplete = task =>
    !isManager && !task.completed && task.assignees.some(a => a.id === user.id);

  return (
    <View style={ui.flex}>
      <View style={styles.dateBar}>
        <IconButton
          icon="left"
          size={28}
          color={colors.brown}
          onPress={() => setDate(addDays(date, -1))}
          label={t('common.previousDay')}
        />
        <Pressable
          style={styles.dateCenter}
          onPress={() => setDialog({type: 'calendar'})}
          accessibilityLabel={t('tasks.date')}>
          <Text style={styles.dateText}>📅 {formatDay(date)}</Text>
          {tasks && tasks.length > 0 ? (
            <Text style={styles.progress}>
              {t('tasks.progress', {done, total: tasks.length})}
            </Text>
          ) : null}
        </Pressable>
        <IconButton
          icon="right"
          size={28}
          color={colors.brown}
          onPress={() => setDate(addDays(date, 1))}
          label={t('common.nextDay')}
        />
      </View>
      {date !== today ? (
        <Button
          title={t('common.today')}
          variant="ghost"
          onPress={() => setDate(today)}
          style={styles.todayButton}
        />
      ) : null}

      <GestureDetector gesture={swipe}>
        <ScrollView
          contentContainerStyle={styles.list}
          refreshControl={
            <RefreshControl
              refreshing={refreshing}
              onRefresh={refresh}
              colors={[colors.brown]}
            />
          }>
          <View>
            {error ? (
              <ErrorBox message={errorMessage(t, error)} onRetry={load} />
            ) : null}
            {!tasks && !error ? (
              <Text style={styles.loading}>{t('common.loading')}</Text>
            ) : null}
            {tasks
              ? SHIFTS.map(shift => {
                  const list = tasks.filter(x => x.shift === shift.value);
                  return (
                    <View key={shift.value} style={styles.section}>
                      <View style={styles.sectionHeader}>
                        <Glyph
                          name={shift.icon}
                          size={16}
                          color={colors.yellow}
                        />
                        <Text style={styles.sectionTitle}>
                          {t(shift.label).toUpperCase()}
                        </Text>
                        <Text style={styles.count}>· {list.length}</Text>
                      </View>
                      {list.length === 0 ? (
                        <Text style={ui.empty}>{t('tasks.empty')}</Text>
                      ) : (
                        list.map(task => (
                          <TaskCard
                            key={task.id}
                            task={task}
                            isManager={isManager}
                            canComplete={canComplete(task)}
                            onOpen={() => setDialog({type: 'detail', task})}
                            onComplete={() =>
                              setDialog({type: 'complete', task})
                            }
                            onEdit={() => setDialog({type: 'edit', task})}
                            onDelete={() => setDialog({type: 'delete', task})}
                          />
                        ))
                      )}
                    </View>
                  );
                })
              : null}
          </View>
        </ScrollView>
      </GestureDetector>

      {isManager ? (
        <Fab
          onPress={() => setDialog({type: 'create'})}
          label={t('tasks.new')}
        />
      ) : null}

      {dialog?.type === 'calendar' ? (
        <CalendarSheet value={date} onSelect={setDate} onClose={close} />
      ) : null}
      {dialog?.type === 'detail' ? (
        <TaskDetail
          task={dialog.task}
          isManager={isManager}
          canComplete={canComplete(dialog.task)}
          onComplete={() => setDialog({type: 'complete', task: dialog.task})}
          onEdit={() => setDialog({type: 'edit', task: dialog.task})}
          onDelete={() => setDialog({type: 'delete', task: dialog.task})}
          onClose={close}
        />
      ) : null}
      {dialog?.type === 'create' || dialog?.type === 'edit' ? (
        <TaskFormSheet
          task={dialog.task}
          defaultDate={date < today ? today : date}
          engineers={engineers}
          onSaved={() => {
            close();
            load();
          }}
          onClose={close}
        />
      ) : null}
      {dialog?.type === 'complete' ? (
        <CompleteSheet
          task={dialog.task}
          onDone={updated => {
            setTasks(list =>
              list.map(x => (x.id === updated.id ? updated : x)),
            );
            close();
          }}
          onClose={close}
        />
      ) : null}
      {dialog?.type === 'delete' ? (
        <ConfirmSheet
          message={t('tasks.deleteConfirm')}
          onConfirm={confirmDelete}
          onClose={close}
          busy={deleteState.busy}
          error={deleteState.error}
        />
      ) : null}
    </View>
  );
}

export function TaskCard({
  task,
  isManager,
  canComplete,
  onOpen,
  onComplete,
  onEdit,
  onDelete,
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
  return (
    <Pressable
      onPress={onOpen}
      disabled={!onOpen}
      style={({pressed}) => [
        ui.card,
        styles.card,
        pressed && styles.cardPressed,
      ]}>
      <View
        style={[
          styles.bar,
          {
            backgroundColor: task.completed
              ? colors.green
              : priorityColor[task.priority],
          },
        ]}
      />
      <View style={styles.cardBody}>
        <View style={styles.titleRow}>
          {task.completed ? (
            <View style={styles.doneDot}>
              <Text style={styles.doneDotText}>✓</Text>
            </View>
          ) : null}
          <Text style={styles.title}>{text('title')}</Text>
          <View
            style={[
              styles.priority,
              {backgroundColor: priorityColor[task.priority]},
            ]}>
            <Text
              style={[
                styles.priorityText,
                task.priority === 'Low'
                  ? {color: colors.muted}
                  : task.priority === 'High' && {color: colors.white},
              ]}>
              {t(`priority.${task.priority}`)}
            </Text>
          </View>
        </View>
        {task.description ? (
          <Text style={styles.description}>{text('description')}</Text>
        ) : null}

        {translated ? (
          <TranslationToggle
            showOriginal={showOriginal}
            onToggle={() => setShowOriginal(v => !v)}
            style={styles.translation}
          />
        ) : null}

        <View style={styles.chipRow}>
          {task.assignees.map(a => (
            <View key={a.id} style={ui.chip}>
              <Text style={ui.chipText}>👤 {a.name}</Text>
            </View>
          ))}
          {task.photoIds?.length ? (
            <View style={ui.chip}>
              <Text style={ui.chipText}>📷 {task.photoIds.length}</Text>
            </View>
          ) : null}
          {carried ? (
            <View style={[ui.chip, {backgroundColor: colors.yellowSoft}]}>
              <Text style={ui.chipText}>
                ↪{' '}
                {t('tasks.carriedOver', {
                  date: formatDay(task.originalDate, false),
                })}
              </Text>
            </View>
          ) : null}
          {isManager && !task.completed ? (
            <View style={styles.actions}>
              <IconButton
                icon="edit"
                onPress={onEdit}
                label={t('common.edit')}
              />
              <IconButton
                icon="trash"
                onPress={onDelete}
                label={t('common.delete')}
              />
            </View>
          ) : null}
        </View>

        {task.completed ? (
          <View style={styles.completion}>
            <Text style={styles.completedBy}>
              {t('tasks.completedBy', {
                name: task.completedBy?.name ?? '—',
                time: formatDateTime(task.completedAt),
              })}
            </Text>
            {task.completionComment ? (
              <Text style={styles.comment}>💬 {text('comment')}</Text>
            ) : null}
            {task.hasPhoto || task.completionPhotoIds?.length ? (
              <View style={styles.thumbs}>
                {task.hasPhoto ? (
                  <StoredPhoto path={`/tasks/${task.id}/photo`} />
                ) : null}
                {(task.completionPhotoIds ?? []).map(photoId => (
                  <StoredPhoto
                    key={photoId}
                    path={`/tasks/${task.id}/photos/${photoId}`}
                  />
                ))}
              </View>
            ) : null}
          </View>
        ) : null}

        {canComplete ? (
          <View style={styles.completeRow}>
            <Button
              title={t('tasks.complete')}
              icon="check"
              variant="green"
              onPress={onComplete}
            />
          </View>
        ) : null}
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  dateBar: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 8,
    paddingTop: 10,
  },
  dateCenter: {flex: 1, alignItems: 'center'},
  dateText: {
    fontSize: 18,
    fontWeight: '800',
    color: colors.brown,
    textTransform: 'capitalize',
  },
  progress: {fontSize: 12, color: colors.muted, marginTop: 2},
  todayButton: {alignSelf: 'center', minHeight: 34, paddingVertical: 2},
  list: {flexGrow: 1, padding: 14, paddingBottom: 100},
  loading: {textAlign: 'center', color: colors.muted, paddingVertical: 40},
  section: {marginBottom: 16},
  sectionHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    marginBottom: 8,
  },
  sectionTitle: {
    fontSize: 13,
    fontWeight: '800',
    color: colors.brown,
    letterSpacing: 0.5,
  },
  count: {fontSize: 13, color: colors.muted},
  card: {flexDirection: 'row', marginBottom: 10},
  cardPressed: {opacity: 0.85},
  bar: {width: 6},
  cardBody: {flex: 1, padding: 13},
  titleRow: {flexDirection: 'row', alignItems: 'flex-start', gap: 8},
  doneDot: {
    width: 22,
    height: 22,
    borderRadius: 11,
    backgroundColor: colors.green,
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 1,
  },
  doneDotText: {color: colors.white, fontWeight: '900', fontSize: 13},
  title: {flex: 1, fontSize: 16, fontWeight: '700', color: colors.ink},
  priority: {borderRadius: 999, paddingHorizontal: 9, paddingVertical: 2},
  priorityText: {fontSize: 12, fontWeight: '700', color: colors.ink},
  description: {color: colors.muted, marginTop: 4},
  translation: {marginTop: 6},
  chipRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    alignItems: 'center',
    gap: 6,
    marginTop: 8,
  },
  actions: {flexDirection: 'row', marginLeft: 'auto'},
  completion: {
    backgroundColor: colors.greenSoft,
    borderRadius: 12,
    padding: 10,
    marginTop: 10,
    gap: 6,
  },
  completedBy: {color: colors.green, fontWeight: '700'},
  comment: {color: colors.ink},
  thumbs: {flexDirection: 'row', flexWrap: 'wrap', gap: 8},
  completeRow: {
    flexDirection: 'row',
    justifyContent: 'flex-end',
    marginTop: 10,
  },
});
