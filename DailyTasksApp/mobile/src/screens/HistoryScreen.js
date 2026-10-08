import React, {useCallback, useEffect, useMemo, useRef, useState} from 'react';
import {Pressable, RefreshControl, SectionList, StyleSheet, Text, View} from 'react-native';
import {useTranslation} from 'react-i18next';
import {api} from '../api';
import CalendarSheet from '../Calendar';
import {addDays, errorMessage, formatDay, siteDateOf, siteToday} from '../format';
import {colors} from '../theme';
import {ErrorBox, IconButton, Input, styles as ui} from '../ui';
import TaskDetail from './TaskDetail';
import {TaskCard} from './TasksScreen';

const PAGE_SIZE = 20;

// Completed tasks, newest first, grouped by the day they were finished. Read-only.
// Searchable by text in the title/description and by completion day.
export default function HistoryScreen() {
  const {t} = useTranslation();
  const [text, setText] = useState('');
  const [q, setQ] = useState('');
  const [date, setDate] = useState(null);
  const [items, setItems] = useState(null);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loadingMore, setLoadingMore] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState(null);
  const [opened, setOpened] = useState(null);
  const [calendarOpen, setCalendarOpen] = useState(false);
  // Only the newest request may update the list; slower earlier ones are dropped.
  const requestId = useRef(0);

  useEffect(() => {
    const timer = setTimeout(() => setQ(text.trim()), 350);
    return () => clearTimeout(timer);
  }, [text]);

  const load = useCallback(async () => {
    const id = ++requestId.current;
    try {
      setError(null);
      const result = await api.taskHistory(1, {q, date}, PAGE_SIZE);
      if (id !== requestId.current) {
        return;
      }
      setItems(result.items);
      setTotal(result.total);
      setPage(1);
    } catch (err) {
      if (id === requestId.current) {
        setError(err);
      }
    }
  }, [q, date]);

  useEffect(() => {
    load();
  }, [load]);

  async function loadMore() {
    if (loadingMore || !items || items.length >= total) {
      return;
    }
    setLoadingMore(true);
    const id = requestId.current;
    try {
      const result = await api.taskHistory(page + 1, {q, date}, PAGE_SIZE);
      if (id === requestId.current) {
        setItems(list => [...list, ...result.items.filter(x => !list.some(y => y.id === x.id))]);
        setTotal(result.total);
        setPage(page + 1);
      }
    } catch (err) {
      setError(err);
    } finally {
      setLoadingMore(false);
    }
  }

  const sections = useMemo(() => {
    const groups = [];
    for (const task of items ?? []) {
      const day = siteDateOf(task.completedAt);
      if (groups.length === 0 || groups[groups.length - 1].day !== day) {
        groups.push({day, data: []});
      }
      groups[groups.length - 1].data.push(task);
    }
    return groups;
  }, [items]);

  const today = siteToday();
  const filtered = Boolean(q || date);

  const searchBar = (
    <View style={styles.searchBar}>
      <Input
        value={text}
        onChangeText={setText}
        placeholder={`🔍  ${t('history.search')}`}
        maxLength={200}
        returnKeyType="search"
      />
      <View style={styles.filterRow}>
        {date ? (
          <View style={styles.dateFilter}>
            <IconButton icon="left" color={colors.brown} onPress={() => setDate(addDays(date, -1))} label={t('common.previousDay')} />
            <Pressable onPress={() => setCalendarOpen(true)}>
              <Text style={styles.dateText}>📅 {formatDay(date, false)}</Text>
            </Pressable>
            <IconButton
              icon="right"
              color={date >= today ? colors.creamDark : colors.brown}
              onPress={() => date < today && setDate(addDays(date, 1))}
              label={t('common.nextDay')}
            />
          </View>
        ) : (
          <Pressable style={styles.anyDate} onPress={() => setCalendarOpen(true)}>
            <Text style={styles.anyDateText}>📅 {t('history.anyDate')}</Text>
          </Pressable>
        )}
        {filtered ? (
          <Pressable
            style={styles.clear}
            onPress={() => {
              setText('');
              setQ('');
              setDate(null);
            }}>
            <Text style={styles.clearText}>✕ {t('common.clear')}</Text>
          </Pressable>
        ) : null}
      </View>
    </View>
  );

  return (
    <View style={ui.flex}>
      {error ? (
        <View style={styles.pad}>
          <ErrorBox message={errorMessage(t, error)} onRetry={load} />
        </View>
      ) : null}
      <SectionList
        sections={sections}
        keyExtractor={task => task.id}
        contentContainerStyle={styles.list}
        stickySectionHeadersEnabled={false}
        keyboardShouldPersistTaps="handled"
        onEndReached={loadMore}
        onEndReachedThreshold={0.4}
        refreshControl={
          <RefreshControl
            refreshing={refreshing}
            onRefresh={async () => {
              setRefreshing(true);
              await load();
              setRefreshing(false);
            }}
            colors={[colors.brown]}
          />
        }
        ListHeaderComponent={
          <>
            <Text style={styles.header}>
              🕘 {t('nav.history')}
              {items ? ` · ${total}` : ''}
            </Text>
            {searchBar}
          </>
        }
        renderSectionHeader={({section}) => <Text style={styles.day}>{formatDay(section.day)}</Text>}
        renderItem={({item}) => (
          <TaskCard task={item} isManager={false} canComplete={false} onOpen={() => setOpened(item)} />
        )}
        ListEmptyComponent={
          items ? (
            <Text style={ui.empty}>{filtered ? t('history.noResults') : t('history.empty')}</Text>
          ) : !error ? (
            <Text style={styles.loading}>{t('common.loading')}</Text>
          ) : null
        }
        ListFooterComponent={loadingMore ? <Text style={styles.loading}>{t('common.loading')}</Text> : null}
      />
      {opened ? <TaskDetail task={opened} isManager={false} canComplete={false} onClose={() => setOpened(null)} /> : null}
      {calendarOpen ? (
        <CalendarSheet value={date} max={today} onSelect={setDate} onClose={() => setCalendarOpen(false)} />
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  pad: {paddingHorizontal: 14, paddingTop: 12},
  list: {padding: 14, paddingBottom: 40},
  loading: {textAlign: 'center', color: colors.muted, paddingVertical: 24},
  header: {fontSize: 17, fontWeight: '800', color: colors.brown, marginBottom: 10},
  searchBar: {gap: 8, marginBottom: 6},
  filterRow: {flexDirection: 'row', alignItems: 'center', gap: 8},
  anyDate: {
    backgroundColor: colors.white,
    borderWidth: 1,
    borderColor: colors.creamDark,
    borderRadius: 999,
    paddingHorizontal: 14,
    paddingVertical: 8,
  },
  anyDateText: {color: colors.brown, fontWeight: '600'},
  dateFilter: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.yellowSoft,
    borderRadius: 999,
  },
  dateText: {color: colors.brown, fontWeight: '700', paddingHorizontal: 2},
  clear: {paddingHorizontal: 10, paddingVertical: 8},
  clearText: {color: colors.danger, fontWeight: '600'},
  day: {
    fontSize: 13,
    fontWeight: '800',
    color: colors.brown,
    textTransform: 'uppercase',
    marginTop: 10,
    marginBottom: 8,
  },
});
