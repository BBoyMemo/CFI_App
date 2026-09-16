import { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';

import AsyncSection from '../../components/ui/AsyncSection';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import ErrorBanner from '../../components/ui/ErrorBanner';
import { describeApiError } from '../../api/apiClient';
import { getDepartments, getInbox, getTeam, sendMessage } from '../../api/endpoints';
import { MessagePriority } from '../../api/enums';
import { Permissions } from '../../api/permissions';
import { useAuth } from '../../auth/useAuth';
import { useApiData } from '../../hooks/useApiData';

const empty = Promise.resolve({ data: { items: [] } });

const chipClass = (selected) =>
  `flex items-center gap-1 rounded border px-2 py-1 text-sm ${
    selected ? 'border-cfi-yellow-dark bg-cfi-yellow/20' : 'border-cfi-rule'
  }`;

function Composer({ onSent }) {
  const { t } = useTranslation();

  const [body, setBody] = useState('');
  const [priority, setPriority] = useState(0);
  const [userIds, setUserIds] = useState([]);
  const [departmentIds, setDepartmentIds] = useState([]);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);

  const team = useApiData(() => getTeam({ pageSize: 100 }));
  const departments = useApiData(() => getDepartments({ pageSize: 50 }));

  const toggle = (setter) => (id) =>
    setter((current) => (current.includes(id) ? current.filter((x) => x !== id) : [...current, id]));

  const submit = async (event) => {
    event.preventDefault();
    setError(null);
    setBusy(true);

    try {
      await sendMessage({
        body,
        priority: Number(priority),
        expiresAt: null,
        recipientUserIds: userIds,
        recipientDepartmentIds: departmentIds,
      });
      setBody('');
      setUserIds([]);
      setDepartmentIds([]);
      onSent();
    } catch (sendError) {
      setError(describeApiError(sendError));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card>
      <form onSubmit={submit} className="flex flex-col gap-3">
        <label className="flex flex-col gap-1 text-sm">
          {t('message.body')}
          <textarea
            required
            rows={3}
            value={body}
            onChange={(event) => setBody(event.target.value)}
            className="rounded border border-cfi-rule bg-white p-3"
          />
        </label>

        <div>
          <p className="mb-1 text-sm">{t('message.toDepartments')}</p>
          <div className="flex flex-wrap gap-2">
            {(departments.data?.items ?? []).map((department) => (
              <label key={department.id} className={chipClass(departmentIds.includes(department.id))}>
                <input
                  type="checkbox"
                  checked={departmentIds.includes(department.id)}
                  onChange={() => toggle(setDepartmentIds)(department.id)}
                />
                {department.name}
              </label>
            ))}
          </div>
        </div>

        <div>
          <p className="mb-1 text-sm">{t('message.toPeople')}</p>
          <div className="flex flex-wrap gap-2">
            {(team.data?.items ?? []).map((member) => (
              <label key={member.id} className={chipClass(userIds.includes(member.id))}>
                <input
                  type="checkbox"
                  checked={userIds.includes(member.id)}
                  onChange={() => toggle(setUserIds)(member.id)}
                />
                {member.fullName}
              </label>
            ))}
          </div>
        </div>

        <label className="flex w-40 flex-col gap-1 text-sm">
          {t('message.priority')}
          <select
            value={priority}
            onChange={(event) => setPriority(event.target.value)}
            className="min-h-11 rounded border border-cfi-rule bg-white px-3 text-sm"
          >
            {MessagePriority.map((name, value) => (
              <option key={name} value={value}>{t(`message.priority_${name}`)}</option>
            ))}
          </select>
        </label>

        <ErrorBanner error={error} />

        <Button
          type="submit"
          className="w-fit"
          disabled={busy || !body.trim() || (userIds.length === 0 && departmentIds.length === 0)}
        >
          {t('message.send')}
        </Button>
      </form>
    </Card>
  );
}

export default function MessagesPage() {
  const { t } = useTranslation();
  const { hasPermission } = useAuth();

  const canSend = hasPermission(Permissions.MessageSend);
  const [composing, setComposing] = useState(false);

  const inbox = useApiData(() => (hasPermission(Permissions.MessageRead) ? getInbox({ pageSize: 50 }) : empty));
  const items = inbox.data?.items ?? [];

  const onSent = useCallback(() => {
    setComposing(false);
    inbox.refetch();
  }, [inbox]);

  return (
    <div className="mx-auto max-w-3xl">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-bold text-cfi-brown-dark">{t('nav.messages')}</h1>

        {canSend && (
          <Button variant={composing ? 'secondary' : 'primary'} onClick={() => setComposing(!composing)}>
            {composing ? t('common.cancel') : t('message.new')}
          </Button>
        )}
      </div>

      {composing && <Composer onSent={onSent} />}

      <div className="mt-4">
        <AsyncSection {...inbox} isEmpty={items.length === 0} emptyKey="message.empty">
          <div className="flex flex-col gap-2">
            {items.map((message) => (
              <Link key={message.id} to={`/messages/${message.id}`}>
                <Card className={`transition-colors hover:border-cfi-yellow-dark ${
                  message.isRead ? '' : 'border-cfi-yellow-dark bg-cfi-yellow/5'
                }`}
                >
                  <div className="flex flex-wrap items-start justify-between gap-2">
                    <p className={`flex-1 ${message.isRead ? 'text-cfi-muted' : 'font-medium text-cfi-ink'}`}>
                      {message.body}
                    </p>

                    {MessagePriority[message.priority] === 'High' && (
                      <Badge tone="bg-cfi-red/15 text-cfi-red">{t('message.priority_High')}</Badge>
                    )}
                  </div>

                  <p className="mt-2 text-xs text-cfi-muted">
                    {message.senderName} · {new Date(message.createdAt).toLocaleString()}
                  </p>
                </Card>
              </Link>
            ))}
          </div>
        </AsyncSection>
      </div>
    </div>
  );
}
