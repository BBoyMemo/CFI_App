import { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';

import AsyncSection from '../../components/ui/AsyncSection';
import AuthenticatedImage from '../../components/ui/AuthenticatedImage';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import ConfirmButton from '../../components/ui/ConfirmButton';
import ErrorBanner from '../../components/ui/ErrorBanner';
import PhotoLightbox from '../../components/ui/PhotoLightbox';
import { describeApiError } from '../../api/apiClient';
import { createOrder, deleteOrder, getAllOrders, getMyOrders, markOrderOrdered } from '../../api/endpoints';
import { decisionTone } from '../../api/enums';
import { Permissions } from '../../api/permissions';
import { useAuth } from '../../auth/useAuth';
import { useApiData } from '../../hooks/useApiData';
import PhotoUploader from '../workorders/PhotoUploader';

const controlClass = 'min-h-11 rounded border border-cfi-rule bg-white px-3 text-sm';

export default function OrdersPage() {
  const { t } = useTranslation();
  const { hasPermission } = useAuth();

  const canManageAll = hasPermission(Permissions.OrderManageAll);

  const [partName, setPartName] = useState('');
  const [quantity, setQuantity] = useState(1);
  const [isUrgent, setIsUrgent] = useState(false);
  const [photoAssetIds, setPhotoAssetIds] = useState([]);
  // The uploader keeps its own list of what it has shown; a new key after each request gives
  // the next one an empty uploader instead of last time's file names.
  const [uploaderKey, setUploaderKey] = useState(0);
  const [openPhotoId, setOpenPhotoId] = useState(null);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);

  // A manager watches the whole queue; an engineer watches their own requests.
  const orders = useApiData(
    () => (canManageAll ? getAllOrders({ pageSize: 50 }) : getMyOrders({ pageSize: 50 })),
    [canManageAll],
  );

  const items = orders.data?.items ?? [];

  const runAction = useCallback(async (action) => {
    setError(null);
    setBusy(true);
    try {
      await action();
      orders.refetch();
    } catch (actionError) {
      setError(describeApiError(actionError));
    } finally {
      setBusy(false);
    }
  }, [orders]);

  const handleSubmit = (event) => {
    event.preventDefault();
    runAction(async () => {
      await createOrder({ partName, quantity: Number(quantity), isUrgent, photoAssetIds });
      setPartName('');
      setQuantity(1);
      setIsUrgent(false);
      setPhotoAssetIds([]);
      setUploaderKey((key) => key + 1);
    });
  };

  return (
    <div className="mx-auto max-w-3xl">
      <h1 className="mb-4 text-xl font-bold text-cfi-brown-dark">{t('nav.orders')}</h1>

      {hasPermission(Permissions.OrderCreate) && (
        <Card>
          <form onSubmit={handleSubmit} className="flex flex-wrap items-end gap-3">
            <label className="flex flex-1 flex-col gap-1 text-sm">
              {t('order.partName')}
              <input
                required
                value={partName}
                onChange={(event) => setPartName(event.target.value)}
                placeholder={t('order.partNamePlaceholder')}
                className={controlClass}
              />
            </label>

            <label className="flex w-24 flex-col gap-1 text-sm">
              {t('order.quantity')}
              <input
                required
                type="number"
                min="1"
                value={quantity}
                onChange={(event) => setQuantity(event.target.value)}
                className={controlClass}
              />
            </label>

            <label className="flex min-h-11 items-center gap-2 text-sm">
              <input type="checkbox" checked={isUrgent} onChange={(event) => setIsUrgent(event.target.checked)} />
              {t('order.urgent')}
            </label>

            <Button type="submit" disabled={busy || !partName.trim()}>
              {t('order.request')}
            </Button>

            {/* The same photo button as reporting a breakdown, so it works the same everywhere. */}
            <div className="basis-full">
              <PhotoUploader key={uploaderKey} assetIds={photoAssetIds} onChange={setPhotoAssetIds} />
            </div>
          </form>
        </Card>
      )}

      <ErrorBanner error={error} />

      <div className="mt-4">
        <AsyncSection {...orders} isEmpty={items.length === 0} emptyKey="order.empty">
          <div className="flex flex-col gap-3">
            {items.map((order) => (
              <Card key={order.id}>
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <div>
                    <p className="font-medium text-cfi-ink">
                      {order.partName} <span className="text-cfi-muted">× {order.quantity}</span>
                    </p>
                    <p className="text-sm text-cfi-muted">
                      {order.requestedByName} · {new Date(order.requestedAt).toLocaleDateString()}
                    </p>
                  </div>

                  <div className="flex flex-wrap items-center gap-2">
                    {order.isUrgent && <Badge tone="bg-cfi-red/15 text-cfi-red">{t('order.urgent')}</Badge>}
                    <Badge tone={decisionTone(order.status)}>{order.status}</Badge>
                  </div>
                </div>

                {order.photoAssetIds.length > 0 && (
                  <div className="mt-3 flex flex-wrap gap-2">
                    {order.photoAssetIds.map((mediaId) => (
                      <button
                        key={mediaId}
                        type="button"
                        onClick={() => setOpenPhotoId(mediaId)}
                        className="rounded focus-visible:outline focus-visible:outline-2 focus-visible:outline-cfi-yellow-dark"
                      >
                        <AuthenticatedImage
                          mediaId={mediaId}
                          alt={t('common.photos')}
                          className="h-20 w-20 rounded object-cover"
                        />
                      </button>
                    ))}
                  </div>
                )}

                <div className="mt-3 flex flex-wrap gap-2">
                  {canManageAll && order.status !== 'Ordered' && (
                    <Button disabled={busy} onClick={() => runAction(() => markOrderOrdered(order.id))}>
                      {t('order.markOrdered')}
                    </Button>
                  )}
                  <ConfirmButton
                    disabled={busy}
                    onConfirm={() => runAction(() => deleteOrder(order.id))}
                  >
                    {t('common.delete')}
                  </ConfirmButton>
                </div>
              </Card>
            ))}
          </div>
        </AsyncSection>
      </div>

      {openPhotoId != null && (
        <PhotoLightbox mediaId={openPhotoId} onClose={() => setOpenPhotoId(null)} />
      )}
    </div>
  );
}
