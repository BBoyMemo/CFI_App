import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';

import AsyncSection from '../../components/ui/AsyncSection';
import Card from '../../components/ui/Card';
import Icon from '../../components/ui/Icon';
import { getNotifications } from '../../api/endpoints';
import { useApiData } from '../../hooks/useApiData';

/**
 * What somebody needs telling about: messages from the managers, and changes to their own
 * week.
 *
 * This screen used to repeat every step of every job as well - "an engineer is on their
 * way", "waiting for a part". The job card already shows where a repair has got to, in
 * more detail and where somebody is actually looking, so all that did here was bury the
 * things that have nowhere else to appear. A shift change is one of those: there is no card
 * to go and look at, and the alternative to being told is finding out at six in the morning.
 */

/** System notifications carry a type, not a body - the wording lives in the locale files. */
const SYSTEM_NOTICES = {
  'shift.rosterChanged': { to: '/shifts/mine', icon: 'calendar' },
  'shift.coverAdded': { to: '/shifts/mine', icon: 'calendar' },
  'shift.coverRemoved': { to: '/shifts/mine', icon: 'calendar' },
};

export default function NotificationsPage() {
  const { t } = useTranslation();

  const notifications = useApiData(() => getNotifications({ pageSize: 50 }));
  const items = notifications.data?.items ?? [];

  return (
    <div className="mx-auto max-w-2xl">
      <h1 className="mb-4 text-xl font-bold text-cfi-brown-dark">{t('nav.notifications')}</h1>

      <AsyncSection {...notifications} isEmpty={items.length === 0} emptyKey="notification.empty">
        <div className="flex flex-col gap-2">
          {items.map((item) => {
            const notice = SYSTEM_NOTICES[item.type];

            return (
              <Link key={item.id} to={notice ? notice.to : `/messages/${item.messageId}`}>
                <Card className="flex items-start gap-3 py-3">
                  <span className="text-cfi-brown-dark">
                    <Icon name={notice ? notice.icon : 'message'} />
                  </span>

                  <div className="min-w-0 flex-1">
                    {notice ? (
                      <p className="text-sm font-semibold text-cfi-brown-dark">
                        {t(`notification.${item.type}`)}
                      </p>
                    ) : (
                      <>
                        {item.senderName && (
                          <p className="text-sm font-semibold text-cfi-brown-dark">{item.senderName}</p>
                        )}
                        {item.messageBody && (
                          <p className="mt-0.5 truncate text-sm text-cfi-ink">{item.messageBody}</p>
                        )}
                      </>
                    )}
                  </div>
                </Card>
              </Link>
            );
          })}
        </div>
      </AsyncSection>
    </div>
  );
}
