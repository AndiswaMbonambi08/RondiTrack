import { describe, expect, it } from 'vitest';
import { groupContributionsByCycle, UNKNOWN_CYCLE_KEY } from './groupContributionsByCycle';

const cycles = [
  { id: 'c1', dueDate: '2026-08-31' },
  { id: 'c3', dueDate: '2026-10-31' },
  { id: 'c2', dueDate: '2026-09-30' },
];
const contributions = [
  { id: 'k1', cycleId: 'c3' },
  { id: 'k2', cycleId: 'c1' },
  { id: 'k3', cycleId: 'c3' },
];

describe('groupContributionsByCycle', () => {
  it('orders cycles newest first', () => {
    expect(groupContributionsByCycle(cycles, contributions).map((g) => g.key)).toEqual(['c3', 'c2', 'c1']);
  });
  it('keeps a cycle with no contributions', () => {
    const empty = groupContributionsByCycle(cycles, contributions).find((g) => g.key === 'c2');
    expect(empty?.contributions).toEqual([]);
  });
  it('puts unknown cycle ids in a final "Unknown cycle" group and never drops them', () => {
    const extra = [...contributions, { id: 'k4', cycleId: 'ghost' }];
    const groups = groupContributionsByCycle(cycles, extra);
    expect(groups.at(-1)?.key).toBe(UNKNOWN_CYCLE_KEY);
    expect(groups.at(-1)?.contributions).toEqual([{ id: 'k4', cycleId: 'ghost' }]);
  });
  it('outputs exactly as many contributions as it was given', () => {
    const extra = [...contributions, { id: 'k4', cycleId: 'ghost' }];
    const total = groupContributionsByCycle(cycles, extra).reduce((n, g) => n + g.contributions.length, 0);
    expect(total).toBe(extra.length);
  });
});