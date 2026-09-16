import { useTranslation } from 'react-i18next';

import AsyncSection from '../../components/ui/AsyncSection';
import Card from '../../components/ui/Card';
import { getMyShifts } from '../../api/endpoints';
import { useApiData } from '../../hooks/useApiData';

export default function MyShiftsPage() {
  const { t } = useTranslation();

  // No range passed: the server's own default is today plus a fortnight, which is what
  // "what am I on next" means to somebody checking on their way out of the building.
  const shifts = useApiData(() => getMyShifts());
  const items = shifts.data ?? [];

  const todayKey = new Date().toISOString().slice(0, 10);

  return (
    <div className="mx-auto max-w-2xl">
      <h1 className="mb-4 text-xl font-bold text-cfi-brown-dark">{t('nav.myShifts')}</h1>

      <AsyncSection {...shifts} isEmpty={items.length === 0} emptyKey="shift.mineEmpty">
        <div className="flex flex-col gap-2">
          {items.map((shift) => (
            <Card
              key={shift.id}
              className={`flex flex-wrap items-center justify-between gap-2 py-3 ${
                shift.date === todayKey ? 'border-cfi-yellow-dark bg-cfi-yellow/10' : ''
              }`}
            >
              <div>
                <p className="font-medium text-cfi-ink">
                  {new Date(`${shift.date}T00:00:00`).toLocaleDateString(undefined, {
                    weekday: 'long', day: 'numeric', month: 'long',
                  })}
                </p>
                <p className="text-sm text-cfi-muted">{shift.shiftTypeName}</p>
              </div>

              <span className="font-mono text-sm text-cfi-brown-dark">
                {shift.startTime.slice(0, 5)}–{shift.endTime.slice(0, 5)}
              </span>
            </Card>
          ))}
        </div>
      </AsyncSection>
    </div>
  );
}
