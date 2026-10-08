import React, {useCallback, useEffect, useRef, useState} from 'react';
import {
  AppState,
  FlatList,
  Pressable,
  RefreshControl,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import {useTranslation} from 'react-i18next';
import {api, photoPart} from '../api';
import {useAuth} from '../auth';
import CalendarSheet from '../Calendar';
import {
  addDays,
  errorMessage,
  fieldError,
  formatDateTime,
  formatDay,
  siteToday,
} from '../format';
import {PhotoInput, StoredPhoto} from '../Photo';
import {colors} from '../theme';
import {hasTranslation, localized} from '../translate';
import {
  Button,
  ConfirmSheet,
  ErrorBox,
  Fab,
  Field,
  IconButton,
  Input,
  Segmented,
  Sheet,
  styles as ui,
} from '../ui';
import OrderDetail from './OrderDetail';

const PAGE_SIZE = 20;

export default function OrdersScreen() {
  const {t, i18n} = useTranslation();
  const {isManager} = useAuth();
  const [status, setStatus] = useState('New');
  const [text, setText] = useState('');
  const [q, setQ] = useState('');
  const [date, setDate] = useState(null);
  const [items, setItems] = useState(null);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loadingMore, setLoadingMore] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState(null);
  // {type: 'create' | 'detail' | 'delete' | 'calendar', order?}
  const [dialog, setDialog] = useState(null);
  const [action, setAction] = useState({busyId: null, error: null});
  // Only the newest request may update the list; slower earlier ones are dropped.
  const requestId = useRef(0);

  // Searching looks through every order, New and Ordered, so "was this part ordered?" has one answer.
  const searching = Boolean(q || date);
  const filters = searching ? {q, date} : {status};

  useEffect(() => {
    const timer = setTimeout(() => setQ(text.trim()), 350);
    return () => clearTimeout(timer);
  }, [text]);

  const load = useCallback(async () => {
    const id = ++requestId.current;
    try {
      setError(null);
      const result = await api.orders(
        searching ? {q, date} : {status},
        1,
        PAGE_SIZE,
      );
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
  }, [searching, q, date, status]);

  useEffect(() => {
    setItems(null);
    load();
  }, [load]);

  useEffect(() => {
    const sub = AppState.addEventListener(
      'change',
      s => s === 'active' && load(),
    );
    return () => sub.remove();
  }, [load]);

  async function refresh() {
    setRefreshing(true);
    await load();
    setRefreshing(false);
  }

  async function loadMore() {
    if (loadingMore || !items || items.length >= total) {
      return;
    }
    setLoadingMore(true);
    const id = requestId.current;
    try {
      const result = await api.orders(filters, page + 1, PAGE_SIZE);
      if (id === requestId.current) {
        setItems(list => [
          ...list,
          ...result.items.filter(o => !list.some(x => x.id === o.id)),
        ]);
        setTotal(result.total);
        setPage(page + 1);
      }
    } catch (err) {
      setError(err);
    } finally {
      setLoadingMore(false);
    }
  }

  const replace = updated =>
    setItems(list => list.map(x => (x.id === updated.id ? updated : x)));
  const remove = order => {
    setItems(list => list.filter(x => x.id !== order.id));
    setTotal(n => n - 1);
  };

  async function markOrdered(order) {
    setAction({busyId: order.id, error: null});
    try {
      const updated = await api.markOrdered(order.id);
      // In the New tab it leaves the list; in search results it stays, now marked Ordered.
      if (searching) {
        replace(updated);
      } else {
        remove(order);
      }
      if (dialog?.type === 'detail') {
        setDialog({type: 'detail', order: updated});
      }
      setAction({busyId: null, error: null});
    } catch (err) {
      setAction({
        busyId: null,
        error: {id: order.id, message: errorMessage(t, err)},
      });
    }
  }

  async function confirmDelete() {
    const order = dialog.order;
    setAction({busyId: order.id, error: null});
    try {
      await api.deleteOrder(order.id);
      remove(order);
      setDialog(null);
      setAction({busyId: null, error: null});
    } catch (err) {
      setAction({
        busyId: null,
        error: {id: order.id, message: errorMessage(t, err), inDialog: true},
      });
    }
  }

  const closeDialog = () => {
    setDialog(null);
    setAction({busyId: null, error: null});
  };

  const renderOrder = ({item: order}) => {
    const isNew = order.status === 'New';
    return (
      <Pressable
        onPress={() => setDialog({type: 'detail', order})}
        style={({pressed}) => [
          ui.card,
          styles.card,
          pressed && styles.pressed,
        ]}>
        <View style={ui.flex}>
          {searching ? (
            <View
              style={[
                styles.badge,
                isNew ? styles.badgeNew : styles.badgeOrdered,
              ]}>
              <Text
                style={[styles.badgeText, !isNew && styles.badgeTextOrdered]}>
                {isNew
                  ? `🛒 ${t('orders.statusNew')}`
                  : `🚚 ${t('orders.statusOrdered')}`}
              </Text>
            </View>
          ) : null}
          <Text style={styles.description} numberOfLines={3}>
            {hasTranslation(order, ['description'], i18n.language) ? '🌐 ' : ''}
            {localized(order, 'description', i18n.language)}
          </Text>
          <Text style={styles.meta}>
            👤 {order.createdBy.name} · {formatDateTime(order.createdAt)}
          </Text>
          {order.orderedAt ? (
            <Text style={styles.ordered}>
              🚚{' '}
              {t('orders.orderedBy', {
                name: order.orderedBy?.name ?? '—',
                date: formatDateTime(order.orderedAt),
              })}
            </Text>
          ) : null}
          {action.error?.id === order.id && !action.error.inDialog ? (
            <Text style={styles.error}>{action.error.message}</Text>
          ) : null}
          {isManager && isNew ? (
            <View style={styles.actions}>
              <Button
                title={t('orders.markOrdered')}
                icon="truck"
                variant="green"
                onPress={() => markOrdered(order)}
                busy={action.busyId === order.id}
                style={styles.markButton}
              />
              <View style={styles.spacer} />
              <IconButton
                icon="trash"
                onPress={() => setDialog({type: 'delete', order})}
                label={t('common.delete')}
              />
            </View>
          ) : null}
        </View>
        {order.hasPhoto ? (
          <StoredPhoto path={`/orders/${order.id}/photo`} />
        ) : null}
      </Pressable>
    );
  };

  const today = siteToday();

  return (
    <View style={ui.flex}>
      <View style={styles.top}>
        <Input
          value={text}
          onChangeText={setText}
          placeholder={`🔍  ${t('orders.search')}`}
          maxLength={200}
          returnKeyType="search"
        />
        <View style={styles.filterRow}>
          {date ? (
            <View style={styles.dateFilter}>
              <IconButton
                icon="left"
                color={colors.brown}
                onPress={() => setDate(addDays(date, -1))}
                label={t('common.previousDay')}
              />
              <Pressable onPress={() => setDialog({type: 'calendar'})}>
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
            <Pressable
              style={styles.anyDate}
              onPress={() => setDialog({type: 'calendar'})}>
              <Text style={styles.anyDateText}>📅 {t('history.anyDate')}</Text>
            </Pressable>
          )}
          {searching || text ? (
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
        {searching ? (
          <Text style={styles.found}>🔍 {items ? total : '…'}</Text>
        ) : (
          <Segmented
            value={status}
            onChange={setStatus}
            options={[
              {
                value: 'New',
                icon: 'orders',
                label: `${t('orders.statusNew')}${
                  status === 'New' && items ? ` · ${total}` : ''
                }`,
              },
              {
                value: 'Ordered',
                icon: 'truck',
                label: `${t('orders.statusOrdered')}${
                  status === 'Ordered' && items ? ` · ${total}` : ''
                }`,
              },
            ]}
          />
        )}
      </View>

      {error ? (
        <View style={styles.pad}>
          <ErrorBox message={errorMessage(t, error)} onRetry={load} />
        </View>
      ) : null}
      {!items && !error ? (
        <Text style={styles.loading}>{t('common.loading')}</Text>
      ) : null}
      {items ? (
        <FlatList
          data={items}
          keyExtractor={o => o.id}
          renderItem={renderOrder}
          contentContainerStyle={styles.list}
          keyboardShouldPersistTaps="handled"
          onEndReached={loadMore}
          onEndReachedThreshold={0.4}
          refreshControl={
            <RefreshControl
              refreshing={refreshing}
              onRefresh={refresh}
              colors={[colors.brown]}
            />
          }
          ListEmptyComponent={
            <Text style={ui.empty}>
              {searching ? t('history.noResults') : t('orders.empty')}
            </Text>
          }
          ListFooterComponent={
            loadingMore ? (
              <Text style={styles.loading}>{t('common.loading')}</Text>
            ) : null
          }
        />
      ) : null}

      <Fab
        onPress={() => setDialog({type: 'create'})}
        label={t('orders.new')}
      />

      {dialog?.type === 'create' ? (
        <NewOrderSheet
          onClose={closeDialog}
          onCreated={() => {
            closeDialog();
            if (!searching && status === 'New') {
              load();
            } else {
              setText('');
              setQ('');
              setDate(null);
              setStatus('New');
            }
          }}
        />
      ) : null}
      {dialog?.type === 'detail' ? (
        <OrderDetail
          order={dialog.order}
          isManager={isManager}
          busy={action.busyId === dialog.order.id}
          error={
            action.error?.id === dialog.order.id ? action.error.message : null
          }
          onMarkOrdered={() => markOrdered(dialog.order)}
          onDelete={() => setDialog({type: 'delete', order: dialog.order})}
          onClose={closeDialog}
        />
      ) : null}
      {dialog?.type === 'delete' ? (
        <ConfirmSheet
          message={t('orders.deleteConfirm')}
          onConfirm={confirmDelete}
          onClose={closeDialog}
          busy={action.busyId === dialog.order.id}
          error={action.error?.inDialog ? action.error.message : null}
        />
      ) : null}
      {dialog?.type === 'calendar' ? (
        <CalendarSheet
          value={date}
          max={today}
          onSelect={setDate}
          onClose={closeDialog}
        />
      ) : null}
    </View>
  );
}

function NewOrderSheet({onCreated, onClose}) {
  const {t} = useTranslation();
  const [description, setDescription] = useState('');
  const [photo, setPhoto] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  async function submit() {
    setBusy(true);
    setError(null);
    try {
      const form = new FormData();
      form.append('description', description.trim());
      if (photo) {
        form.append('photo', photoPart(photo));
      }
      await api.createOrder(form);
      onCreated();
    } catch (err) {
      setError(err);
      setBusy(false);
    }
  }

  return (
    <Sheet
      title={t('orders.new')}
      onClose={onClose}
      footer={
        <>
          <Button
            title={t('common.cancel')}
            variant="ghost"
            onPress={onClose}
            style={ui.flex}
          />
          <Button
            title={t('common.save')}
            onPress={submit}
            disabled={!description.trim()}
            busy={busy}
            style={ui.flex}
          />
        </>
      }>
      <Field
        label={t('orders.description')}
        error={fieldError(t, error, 'description')}>
        <Input
          value={description}
          onChangeText={setDescription}
          maxLength={2000}
          multiline
          autoFocus
        />
      </Field>
      <PhotoInput
        photo={photo}
        onChange={setPhoto}
        error={fieldError(t, error, 'photo')}
      />
      {error && !error.fieldErrors ? (
        <Text style={styles.error}>{errorMessage(t, error)}</Text>
      ) : null}
    </Sheet>
  );
}

const styles = StyleSheet.create({
  top: {paddingHorizontal: 14, paddingTop: 12, gap: 8},
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
  found: {color: colors.brown, fontWeight: '800', paddingVertical: 4},
  pad: {paddingHorizontal: 14, paddingTop: 12},
  list: {padding: 14, paddingBottom: 100},
  loading: {textAlign: 'center', color: colors.muted, paddingVertical: 24},
  card: {flexDirection: 'row', gap: 10, padding: 13, marginBottom: 10},
  pressed: {opacity: 0.85},
  badge: {
    alignSelf: 'flex-start',
    borderRadius: 999,
    paddingHorizontal: 9,
    paddingVertical: 2,
    marginBottom: 6,
  },
  badgeNew: {backgroundColor: colors.yellowSoft},
  badgeOrdered: {backgroundColor: colors.greenSoft},
  badgeText: {fontSize: 12, fontWeight: '800', color: colors.brown},
  badgeTextOrdered: {color: colors.green},
  description: {fontSize: 15, color: colors.ink},
  meta: {fontSize: 12, color: colors.muted, marginTop: 6},
  ordered: {fontSize: 12, color: colors.green, fontWeight: '600', marginTop: 4},
  error: {color: colors.danger, marginTop: 6},
  actions: {flexDirection: 'row', alignItems: 'center', marginTop: 10},
  markButton: {minHeight: 38, paddingHorizontal: 12},
  spacer: {flex: 1},
});
