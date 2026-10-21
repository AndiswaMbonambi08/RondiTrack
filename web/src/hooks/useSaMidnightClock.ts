import { useEffect, useState } from 'react';
import { msUntilNextSaMidnight } from '@/lib/dueDate';

/** Returns "now", refreshed once just after each Johannesburg midnight. */
export function useSaMidnightClock(): Date {
  const [now, setNow] = useState(() => new Date());

  useEffect(() => {
    const id = setTimeout(() => {
      setNow(new Date()); // new value -> effect re-runs -> next midnight is scheduled
    }, msUntilNextSaMidnight(now));
    return () => clearTimeout(id);
  }, [now]);

  return now;
}
