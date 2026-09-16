import { useCallback, useMemo } from 'react';

import { clock, getMyClockEvents } from '../api/endpoints';
import { ClockType } from '../api/enums';
import { newUuid } from '../api/uuid';
import { useApiData } from '../hooks/useApiData';
import { workedMinutes } from '../features/attendance/hours';

/**
 * Whether the signed-in person is currently clocked in, and the one action that changes it.
 *
 * Shared because the answer appears twice at once, exactly as the approved prototypes lay
 * it out: as read-only text in the header, and as the button inside the side panel. Two
 * copies of this would let the header say "Clocked In" while the button still offered to
 * clock in.
 */

/**
 * Web punches are always Manual. Automatic clocking by location belongs to the phone in
 * somebody's pocket, not to a browser tab that might be open on an office desktop -
 * claiming a desktop is at the gate would put a wrong hour on a payslip.
 */
const MANUAL = 1;

export function useClockState() {
  const events = useApiData(() => getMyClockEvents({ pageSize: 100 }));

  const items = useMemo(() => events.data?.items ?? [], [events.data]);

  // The list comes back newest first, so the top row is the current state.
  const isClockedIn = items.length > 0 && ClockType[items[0].type] === 'In';

  const todayMinutes = useMemo(() => {
    const today = new Date().toDateString();
    return workedMinutes(items.filter((x) => new Date(x.occurredAtUtc).toDateString() === today));
  }, [items]);

  const punch = useCallback(async () => {
    await clock({
      type: isClockedIn ? 1 : 0,
      source: MANUAL,
      occurredAtUtc: new Date().toISOString(),
      latitude: null,
      longitude: null,
      accuracyMeters: null,
      isMockLocation: false,
      deviceId: 'web',
      // A fresh id per press: the server uses it to collapse a double submit into one
      // row, which matters far more once the phone app queues these offline.
      clientId: newUuid(),
    });

    events.refetch();
  }, [isClockedIn, events]);

  return { ...events, items, isClockedIn, todayMinutes, punch };
}
