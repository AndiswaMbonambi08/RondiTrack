// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import type { components } from '@/lib/schema'
import { ContributionForm } from './ContributionForm'
import { ToastProvider } from './ToastProvider'
import { TreasurerActions } from './TreasurerActions'

// Edges only: the router and the Server Actions.
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }) }))
vi.mock('@/app/actions/join', () => ({ createInvite: vi.fn() }))
vi.mock('@/app/actions/close', () => ({ closeCycle: vi.fn() }))
vi.mock('@/app/actions/contributions', () => ({ recordContribution: vi.fn() }))

// Typed from the generated API types: rename a field in the API and this stops compiling.
type StokvelDetail = components['schemas']['StokvelDetailResponse']
const cycle: NonNullable<StokvelDetail['currentCycle']> = {
  id: 'c1',
  label: '2026-10',
  status: 'Open',
  dueDate: null,
  contributorCount: 3,
  collectedCents: 150000,
}
// Sipho: treasurer of the family stokvel, ordinary member of the work one.
const family: StokvelDetail = { id: 's1', name: 'Family', contributionCents: 50000, role: 'Treasurer', memberCount: 8, currentCycle: cycle }
const work: StokvelDetail = { id: 's2', name: 'Work', contributionCents: 50000, role: 'Member', memberCount: 12, currentCycle: cycle }

function view(d: StokvelDetail) {
  return render(
    <ToastProvider>
      <QueryClientProvider client={new QueryClient()}>
        <TreasurerActions role={d.role} stokvelId={d.id} cycle={d.currentCycle} />
        <ContributionForm
          stokvelId={d.id}
          cycleId={cycle.id}
          cycleLabel={cycle.label}
          selfId="sipho"
          selfName="Sipho"
          owedText={null}
          // The page only passes members when THIS stokvel's role allows it.
          members={d.role === 'Treasurer' ? [{ userId: 'thandi', fullName: 'Thandi' }, { userId: 'sipho', fullName: 'Sipho' }] : null}
          initialKey="k"
          initialAmount="500"
        />
      </QueryClientProvider>
    </ToastProvider>,
  )
}

describe('Sipho in the family stokvel (treasurer)', () => {
  it('has the treasurer actions', () => {
    view(family)
    expect(screen.getByRole('heading', { name: /treasurer actions/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /close 2026-10/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /create invite link/i })).toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: /member/i })).toBeInTheDocument()
  })
})

describe('Sipho in the work stokvel (ordinary member)', () => {
  it('has none of them, and nothing hints that they exist', () => {
    view(work)
    expect(screen.queryByRole('heading', { name: /treasurer/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /close 2026-10/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /invite/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('combobox', { name: /member/i })).not.toBeInTheDocument()
    expect(screen.queryByText(/treasurer/i)).not.toBeInTheDocument()
  })

  it('can still record their own contribution', () => {
    view(work)
    expect(screen.getByRole('button', { name: /record contribution/i })).toBeInTheDocument()
  })
})