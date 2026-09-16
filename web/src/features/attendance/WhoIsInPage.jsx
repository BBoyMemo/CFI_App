import { useTranslation } from 'react-i18next';

import AsyncSection from '../../components/ui/AsyncSection';
import Card from '../../components/ui/Card';
import { getWhoIsIn } from '../../api/endpoints';
import { useApiData } from '../../hooks/useApiData';
import { formatHours } from './hours';

/**
 * Who is on site right now - the question a manager otherwise answers by walking the
 * floor. Somebody who forgot to clock out yesterday still appears, with the hours to
 * show it: that is the record needing a correction, and hiding it would not fix it.
 */
export default function WhoIsInPage() {
  const { t } = useTranslation();

  const onSite = useApiData(() => getWhoIsIn());
  const people = onSite.data ?? [];

  return (
    <div className="mx-auto max-w-2xl">
      <div className="mb-4 flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-bold text-cfi-brown-dark">{t('nav.whoIsIn')}</h1>
        <span className="text-sm text-cfi-muted">{t('attendance.onSiteCount', { count: people.length })}</span>
      </div>

      <AsyncSection {...onSite} isEmpty={people.length === 0} emptyKey="attendance.nobodyOnSite">
        <div className="flex flex-col gap-2">
          {people.map((person) => {
            // Measured by the server, not by this device: the app does not trust a
            // device clock for attendance anywhere else either.
            const overLong = person.minutesOnSite > 16 * 60;

            return (
              <Card
                key={person.userId}
                className={`flex flex-wrap items-center justify-between gap-2 py-3 ${
                  overLong ? 'border-cfi-yellow-dark' : ''
                }`}
              >
                <div className="flex items-center gap-2">
                  <span className="h-2 w-2 rounded-full bg-cfi-green" />
                  <span className="font-medium text-cfi-ink">{person.fullName}</span>
                </div>

                <div className="text-right text-sm">
                  <div className="text-cfi-ink">{formatHours(person.minutesOnSite)}</div>
                  <div className="text-xs text-cfi-muted">
                    {t('attendance.since')} {new Date(person.sinceUtc).toLocaleString()}
                  </div>
                </div>

                {/* Nobody works sixteen hours: this is a missing clock-out, not a shift. */}
                {overLong && (
                  <p className="w-full text-xs font-medium text-cfi-yellow-dark">
                    {t('attendance.probablyForgotToClockOut')}
                  </p>
                )}
              </Card>
            );
          })}
        </div>
      </AsyncSection>
    </div>
  );
}
