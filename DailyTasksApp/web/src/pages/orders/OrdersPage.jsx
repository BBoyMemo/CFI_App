import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';
import Icon from '../../components/Icon';
import Modal, { ConfirmModal } from '../../components/Modal';
import { PhotoPicker, StoredPhoto } from '../../components/Photo';
import TranslationToggle from '../../components/TranslationToggle';
import { errorMessage, fieldError, formatDateTime, siteToday } from '../../lib/format';
import { shrinkPhoto } from '../../lib/image';
import { hasTranslation, localized } from '../../lib/translate';

const TABS = [
  { status: 'New', label: 'orders.statusNew', icon: 'orders' },
  { status: 'Ordered', label: 'orders.statusOrdered', icon: 'truck' },
];
const PAGE_SIZE = 20;

export default function OrdersPage() {
  const { t } = useTranslation();
  const { isManager } = useAuth();
  const [status, setStatus] = useState('New');
  const [text, setText] = useState('');
  const [q, setQ] = useState('');
  const [date, setDate] = useState('');
  const [items, setItems] = useState(null);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState(null);
  const [dialog, setDialog] = useState(null);
  const [action, setAction] = useState({ busyId: null, error: null });
  // Only the newest request may update the list; slower earlier ones are dropped.
  const requestId = useRef(0);

  // Searching looks through every order, New and Ordered, so "was this part ordered?" has one answer.
  const searching = Boolean(q || date);
  const filters = searching ? { q, date } : { status };

  useEffect(() => {
    const timer = setTimeout(() => setQ(text.trim()), 300);
    return () => clearTimeout(timer);
  }, [text]);

  const load = useCallback(async () => {
    const id = ++requestId.current;
    try {
      setError(null);
      const result = await api.orders(searching ? { q, date } : { status }, 1, PAGE_SIZE);
      if (id !== requestId.current) return;
      setItems(result.items);
      setTotal(result.total);
      setPage(1);
    } catch (err) {
      if (id === requestId.current) setError(err);
    }
  }, [searching, q, date, status]);

  useEffect(() => {
    setItems(null);
    load();
  }, [load]);

  useEffect(() => {
    const onVisible = () => document.visibilityState === 'visible' && load();
    document.addEventListener('visibilitychange', onVisible);
    return () => document.removeEventListener('visibilitychange', onVisible);
  }, [load]);

  async function loadMore() {
    setLoadingMore(true);
    try {
      const result = await api.orders(filters, page + 1, PAGE_SIZE);
      // New orders may have arrived meanwhile and shifted the pages; skip what is already shown.
      setItems((list) => [...list, ...result.items.filter((o) => !list.some((x) => x.id === o.id))]);
      setTotal(result.total);
      setPage(page + 1);
    } catch (err) {
      setError(err);
    } finally {
      setLoadingMore(false);
    }
  }

  async function markOrdered(order) {
    setAction({ busyId: order.id, error: null });
    try {
      const updated = await api.markOrdered(order.id);
      // In the New tab it leaves the list; in search results it stays, now marked Ordered.
      if (searching) {
        setItems((list) => list.map((x) => (x.id === updated.id ? updated : x)));
      } else {
        setItems((list) => list.filter((x) => x.id !== order.id));
        setTotal((n) => n - 1);
      }
      setAction({ busyId: null, error: null });
    } catch (err) {
      setAction({ busyId: null, error: { id: order.id, message: errorMessage(t, err) } });
    }
  }

  async function confirmDelete() {
    const order = dialog.order;
    setAction({ busyId: order.id, error: null });
    try {
      await api.deleteOrder(order.id);
      setItems((list) => list.filter((x) => x.id !== order.id));
      setTotal((n) => n - 1);
      setDialog(null);
      setAction({ busyId: null, error: null });
    } catch (err) {
      setAction({ busyId: null, error: { id: order.id, message: errorMessage(t, err), inDialog: true } });
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap gap-2">
        <label className="relative min-w-0 flex-1 basis-56">
          <Icon name="search" size={18} className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-muted" />
          <input
            type="search"
            className="input pl-10"
            placeholder={t('orders.search')}
            value={text}
            maxLength={200}
            onChange={(e) => setText(e.target.value)}
            aria-label={t('orders.search')}
          />
        </label>
        <label className="relative flex items-center">
          <Icon name="calendar" size={18} className="pointer-events-none absolute left-3 text-muted" />
          <input
            type="date"
            className="input w-44 pl-10"
            value={date}
            max={siteToday()}
            onChange={(e) => setDate(e.target.value)}
            aria-label={t('history.anyDate')}
          />
        </label>
        {(searching || text) && (
          <button
            type="button"
            className="btn-ghost"
            onClick={() => {
              setText('');
              setQ('');
              setDate('');
            }}
          >
            <Icon name="close" size={16} />
            {t('common.clear')}
          </button>
        )}
      </div>

      {searching ? (
        <p className="flex items-center gap-2 text-sm font-bold text-brown">
          <Icon name="search" size={16} />
          {items ? total : '…'}
        </p>
      ) : (
        <div className="grid grid-cols-2 gap-1 rounded-2xl bg-cream-dark p-1">
          {TABS.map((tab) => (
            <button
              key={tab.status}
              type="button"
              onClick={() => setStatus(tab.status)}
              aria-pressed={status === tab.status}
              className={`flex items-center justify-center gap-2 rounded-xl py-2.5 text-sm font-semibold ${
                status === tab.status ? 'bg-white text-brown shadow-sm' : 'text-muted'
              }`}
            >
              <Icon name={tab.icon} size={18} />
              {t(tab.label)}
              {status === tab.status && items && <span className="font-normal text-muted">· {total}</span>}
            </button>
          ))}
        </div>
      )}

      {error && (
        <div className="card flex items-center gap-3 p-4 text-danger">
          <Icon name="alert" />
          <span className="flex-1 text-sm">{errorMessage(t, error)}</span>
          <button type="button" className="btn-ghost" onClick={load}>
            {t('common.retry')}
          </button>
        </div>
      )}

      {!items && !error && <p className="py-10 text-center text-muted">{t('common.loading')}</p>}

      {items && items.length === 0 && (
        <p className="rounded-2xl border border-dashed border-cream-dark py-10 text-center text-sm text-muted">
          {searching ? t('history.noResults') : t('orders.empty')}
        </p>
      )}

      {items && items.length > 0 && (
        <div className="space-y-2.5">
          {items.map((order) => (
            <article key={order.id} className="card flex gap-3 p-3.5">
              <div className="min-w-0 flex-1">
                {searching && (
                  <span
                    className={`mb-1.5 inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-bold ${
                      order.status === 'New' ? 'bg-yellow/25 text-brown' : 'bg-green-soft text-green'
                    }`}
                  >
                    <Icon name={order.status === 'New' ? 'orders' : 'truck'} size={12} />
                    {order.status === 'New' ? t('orders.statusNew') : t('orders.statusOrdered')}
                  </span>
                )}
                <OrderDescription order={order} />
                <p className="mt-1.5 flex items-center gap-1 text-xs text-muted">
                  <Icon name="user" size={12} />
                  {order.createdBy.name} · {formatDateTime(order.createdAt)}
                </p>
                {order.status === 'Ordered' && order.orderedAt && (
                  <p className="mt-1 flex items-center gap-1 text-xs font-medium text-green">
                    <Icon name="truck" size={12} />
                    {t('orders.orderedBy', { name: order.orderedBy?.name ?? '—', date: formatDateTime(order.orderedAt) })}
                  </p>
                )}
                {action.error?.id === order.id && !action.error.inDialog && (
                  <p className="mt-1 text-sm text-danger">{action.error.message}</p>
                )}
                {/* Once ordered it is a purchase record: no actions, no delete. */}
                {isManager && order.status === 'New' && (
                  <div className="mt-2.5 flex items-center gap-1">
                    <button
                      type="button"
                      className="btn-green px-3 py-1.5"
                      onClick={() => markOrdered(order)}
                      disabled={action.busyId === order.id}
                    >
                      <Icon name="truck" size={16} />
                      {t('orders.markOrdered')}
                    </button>
                    <button
                      type="button"
                      className="icon-btn ml-auto hover:text-danger"
                      onClick={() => setDialog({ type: 'delete', order })}
                      aria-label={t('common.delete')}
                      title={t('common.delete')}
                    >
                      <Icon name="trash" size={18} />
                    </button>
                  </div>
                )}
              </div>
              {order.hasPhoto && (
                <div className="shrink-0">
                  <StoredPhoto path={`/orders/${order.id}/photo`} />
                </div>
              )}
            </article>
          ))}
          {items.length < total && (
            <button type="button" className="btn-ghost w-full" onClick={loadMore} disabled={loadingMore}>
              {t('orders.loadMore')}
            </button>
          )}
        </div>
      )}

      <button
        type="button"
        onClick={() => setDialog({ type: 'create' })}
        className="fixed right-5 bottom-24 z-20 flex h-14 w-14 items-center justify-center rounded-full bg-yellow text-ink shadow-lg hover:bg-yellow-dark sm:bottom-8"
        aria-label={t('orders.new')}
        title={t('orders.new')}
      >
        <Icon name="plus" size={28} strokeWidth={2.5} />
      </button>

      {dialog?.type === 'create' && (
        <NewOrderModal
          onClose={() => setDialog(null)}
          onCreated={() => {
            setDialog(null);
            if (!searching && status === 'New') load();
            else {
              setText('');
              setQ('');
              setDate('');
              setStatus('New');
            }
          }}
        />
      )}
      {dialog?.type === 'delete' && (
        <ConfirmModal
          message={t('orders.deleteConfirm')}
          onConfirm={confirmDelete}
          onClose={() => {
            setDialog(null);
            setAction({ busyId: null, error: null });
          }}
          busy={action.busyId === dialog.order.id}
          error={action.error?.inDialog ? action.error.message : null}
        />
      )}
    </div>
  );
}

function OrderDescription({ order }) {
  const { i18n } = useTranslation();
  const [showOriginal, setShowOriginal] = useState(false);
  return (
    <>
      <p className="break-words whitespace-pre-line">
        {localized(order, 'description', i18n.language, showOriginal)}
      </p>
      {hasTranslation(order, ['description'], i18n.language) && (
        <TranslationToggle showOriginal={showOriginal} onToggle={() => setShowOriginal((v) => !v)} className="mt-1" />
      )}
    </>
  );
}

function NewOrderModal({ onCreated, onClose }) {
  const { t } = useTranslation();
  const [description, setDescription] = useState('');
  const [photo, setPhoto] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  async function submit(e) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const form = new FormData();
      form.append('description', description.trim());
      if (photo) form.append('photo', await shrinkPhoto(photo));
      await api.createOrder(form);
      onCreated();
    } catch (err) {
      setError(err);
      setBusy(false);
    }
  }

  return (
    <Modal
      title={t('orders.new')}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn-ghost flex-1" onClick={onClose}>
            {t('common.cancel')}
          </button>
          <button type="submit" form="order-form" className="btn-primary flex-1" disabled={busy || !description.trim()}>
            {t('common.save')}
          </button>
        </>
      }
    >
      <form id="order-form" onSubmit={submit} className="space-y-4">
        <div>
          <label className="label" htmlFor="order-description">
            {t('orders.description')}
          </label>
          <textarea
            id="order-description"
            className="input min-h-28"
            value={description}
            maxLength={2000}
            onChange={(e) => setDescription(e.target.value)}
            autoFocus
          />
          {fieldError(t, error, 'description') && (
            <p className="mt-1 text-sm text-danger">{fieldError(t, error, 'description')}</p>
          )}
        </div>
        <PhotoPicker file={photo} onChange={setPhoto} error={fieldError(t, error, 'photo')} />
        {error && !error.fieldErrors && <p className="text-sm text-danger">{errorMessage(t, error)}</p>}
      </form>
    </Modal>
  );
}
