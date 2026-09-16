import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router-dom';

import AsyncSection from '../../components/ui/AsyncSection';
import Badge from '../../components/ui/Badge';
import Card from '../../components/ui/Card';
import { getMessage, getReadReceipts } from '../../api/endpoints';
import { MessagePriority } from '../../api/enums';
import { Permissions } from '../../api/permissions';
import { useAuth } from '../../auth/useAuth';
import { useApiData } from '../../hooks/useApiData';

export default function MessageDetailPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const { hasPermission } = useAuth();

  // Opening the message is what marks it read - there is no separate "mark as read"
  // button to forget to press.
  const message = useApiData(() => getMessage(id), [id]);

  // Only the sender's side needs receipts, and only senders are allowed to read them.
  const canSend = hasPermission(Permissions.MessageSend);
  const receipts = useApiData(
    () => (canSend ? getReadReceipts(id) : Promise.resolve({ data: [] })),
    [id, canSend],
  );

  const detail = message.data;

  return (
    <div className="mx-auto max-w-2xl">
      <AsyncSection {...message} isEmpty={false}>
        {detail && (
          <>
            <Card>
              <div className="flex flex-wrap items-start justify-between gap-2">
                <p className="text-sm text-cfi-muted">
                  {detail.senderName} · {new Date(detail.createdAt).toLocaleString()}
                </p>

                {MessagePriority[detail.priority] === 'High' && (
                  <Badge tone="bg-cfi-red/15 text-cfi-red">{t('message.priority_High')}</Badge>
                )}
              </div>

              <p className="mt-3 whitespace-pre-wrap text-cfi-ink">{detail.body}</p>

              {detail.recipientLabels.length > 0 && (
                <p className="mt-3 text-xs text-cfi-muted">
                  {t('message.sentTo')}: {detail.recipientLabels.join(', ')}
                </p>
              )}
            </Card>

            {canSend && (receipts.data ?? []).length > 0 && (
              <Card className="mt-4">
                <h2 className="mb-3 font-semibold text-cfi-brown-dark">{t('message.readReceipts')}</h2>
                <div className="flex flex-col gap-1 text-sm">
                  {receipts.data.map((receipt) => (
                    <div key={receipt.userId} className="flex justify-between gap-4">
                      <span className="text-cfi-ink">{receipt.fullName}</span>
                      <span className="text-cfi-muted">
                        {receipt.readAt ? new Date(receipt.readAt).toLocaleString() : t('message.unread')}
                      </span>
                    </div>
                  ))}
                </div>
              </Card>
            )}
          </>
        )}
      </AsyncSection>
    </div>
  );
}
