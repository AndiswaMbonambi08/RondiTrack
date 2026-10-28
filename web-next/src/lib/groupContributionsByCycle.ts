import { dueDateToDeadline } from './dueDate';

type CycleLike = { id: string | number; label?: string; dueDate: string | null };
type ContributionLike = { cycleId: string | number };

export type CycleGroup<C, K> = { key: string; cycle: C | null; contributions: K[] };

export const UNKNOWN_CYCLE_KEY = '__unknown_cycle__';

// Undated cycles have no deadline, so they sort after every dated one, newest label first.
function deadlineMs(c: CycleLike): number {
  return c.dueDate ? dueDateToDeadline(c.dueDate).getTime() : Number.NEGATIVE_INFINITY;
}

export function groupContributionsByCycle<C extends CycleLike, K extends ContributionLike>(
  cycles: readonly C[],
  contributions: readonly K[],
): CycleGroup<C, K>[] {
  const newestFirst = [...cycles].sort((a, b) => {
    const da = deadlineMs(a);
    const db = deadlineMs(b);
    if (da !== db) return db > da ? 1 : -1;
    return String(b.label ?? '').localeCompare(String(a.label ?? ''));
  });
  const known = new Set(newestFirst.map((c) => String(c.id)));

  const groups: CycleGroup<C, K>[] = newestFirst.map((cycle) => ({
    key: String(cycle.id),
    cycle,
    contributions: contributions.filter((k) => String(k.cycleId) === String(cycle.id)),
  }));

  const orphans = contributions.filter((k) => !known.has(String(k.cycleId)));
  if (orphans.length > 0) {
    groups.push({ key: UNKNOWN_CYCLE_KEY, cycle: null, contributions: orphans });
  }
  return groups;
}
