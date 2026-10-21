import type { ContributionItem, CycleSummary, MeHomeEntry } from '@/api/types';

// PLACEHOLDER values in the real shape. Replace with JSON captured from Scalar
// (GET /api/v1/me/home, GET /api/v1/stokvels/{id}/cycles, GET /api/v1/stokvels/{id}/contributions)
// for your Treasurer-and-Member user (made-up names only, no real emails).
export const home: MeHomeEntry[] = [
  {
    stokvelId: '11111111-1111-1111-1111-111111111111',
    name: 'Mokoena Family Burial and Savings Society (Soweto)',
    role: 'Treasurer',
    currentCycle: {
      id: '22222222-2222-2222-2222-222222222221',
      label: '2026-10',
      status: 'Open',
      dueDate: '2026-10-31',
    },
    paidCents: 50000,
    owedCents: 0,
    arrearsCents: 0,
    nextPayoutUtc: '2026-10-31T21:59:59Z',
  },
  {
    stokvelId: '33333333-3333-3333-3333-333333333333',
    name: 'Work Savers',
    role: 'Member',
    currentCycle: {
      id: '22222222-2222-2222-2222-222222222222',
      label: '2026-10',
      status: 'Open',
      dueDate: '2026-10-31',
    },
    paidCents: 10,
    owedCents: 49990,
    arrearsCents: 15000,
    nextPayoutUtc: null,
  },
  {
    stokvelId: '44444444-4444-4444-4444-444444444444',
    name: 'Quiet Circle',
    role: 'Member',
    currentCycle: null,
    paidCents: 0,
    owedCents: 0,
    arrearsCents: 0,
    nextPayoutUtc: null,
  },
];

// Cycles of "Work Savers", newest first, as the API sends them.
export const cycles: CycleSummary[] = [
  { id: '22222222-2222-2222-2222-222222222222', label: '2026-10', status: 'Open', dueDate: '2026-10-31', collectedCents: 1234567, amountDueCents: 5000000 },
  { id: '22222222-2222-2222-2222-222222222223', label: '2026-09', status: 'Closed', dueDate: '2026-09-30', collectedCents: 4500000, amountDueCents: 5000000 },
  { id: '22222222-2222-2222-2222-222222222224', label: '2026-08', status: 'PaidOut', dueDate: '2026-08-31', collectedCents: 5000000, amountDueCents: 5000000 },
  { id: '22222222-2222-2222-2222-222222222225', label: '2026-07', status: 'Closed', dueDate: '2026-07-31', collectedCents: 0, amountDueCents: 5000000 }, // empty cycle
];

export const contributions: ContributionItem[] = [
  { id: 'aaaaaaaa-0000-0000-0000-000000000001', cycleId: '22222222-2222-2222-2222-222222222222', cycleLabel: '2026-10', amountCents: 10, recordedAt: '2026-10-31T22:30:00Z', status: 'Paid' },
  { id: 'aaaaaaaa-0000-0000-0000-000000000002', cycleId: '22222222-2222-2222-2222-222222222223', cycleLabel: '2026-09', amountCents: 1234567, recordedAt: '2026-09-29T14:05:00Z', status: 'Pending' },
  { id: 'aaaaaaaa-0000-0000-0000-000000000003', cycleId: '22222222-2222-2222-2222-222222222224', cycleLabel: '2026-08', amountCents: 50000, recordedAt: '2026-08-28T09:15:00Z', status: 'Confirmed' },
  // Unknown status: the API type only lists three values, so cast on purpose to prove StatusBadge shows the raw value.
  { id: 'aaaaaaaa-0000-0000-0000-000000000004', cycleId: '22222222-2222-2222-2222-222222222224', cycleLabel: '2026-08', amountCents: 25000, recordedAt: '2026-08-20T08:00:00Z', status: 'Frozen' as unknown as ContributionItem['status'] },
  // Cycle not in the cycles list: must land in "Unknown cycle".
  { id: 'aaaaaaaa-0000-0000-0000-000000000005', cycleId: '99999999-9999-9999-9999-999999999999', cycleLabel: '2026-06', amountCents: 30000, recordedAt: '2026-06-01T10:00:00Z', status: 'Confirmed' },
];
