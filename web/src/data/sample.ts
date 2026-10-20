import type { MeHomeEntry } from '../api/types';

// PLACEHOLDER values in the real shape. Replace this array with the JSON
// "stokvels" array captured from GET /api/v1/me/home in Scalar for your
// Treasurer-and-Member user (made-up names only, no real emails).
export const home: MeHomeEntry[] = [
  {
    stokvelId: '11111111-1111-1111-1111-111111111111',
    name: 'Family Stokvel',
    role: 'Treasurer',
    currentCycle: { id: '22222222-2222-2222-2222-222222222221', label: '2026-10' },
    paidCents: 50000,
    owedCents: 0,
    nextPayoutUtc: '2026-10-31T21:59:59Z',
  },
  {
    stokvelId: '33333333-3333-3333-3333-333333333333',
    name: 'Work Savers',
    role: 'Member',
    currentCycle: { id: '22222222-2222-2222-2222-222222222222', label: '2026-10' },
    paidCents: 10,
    owedCents: 49990,
    nextPayoutUtc: null,
  },
];