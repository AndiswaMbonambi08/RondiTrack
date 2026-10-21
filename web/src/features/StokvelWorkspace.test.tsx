import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { __resetSessionForTests, login } from '../auth/session'
import { StokvelWorkspace } from './StokvelWorkspace'
import { formatCents } from '../lib/money'

const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms))
const json = (b: unknown) => new Response(JSON.stringify(b), { status: 200, headers: { 'Content-Type': 'application/json' } })

// en-ZA currency uses non-breaking spaces; Testing Library normalizes the DOM text but not our string.
const money = (cents: number) => formatCents(cents).replace(/\s/g, ' ')

const MABENA = { id: 'mabena', name: 'Mabena Family', cents: 111100 }
const SOWETO = { id: 'soweto', name: 'Soweto Teachers', cents: 777700 }

function installFakeApi() {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const req = input instanceof Request ? input : new Request(input, init)
    const { pathname } = new URL(req.url)
    if (pathname.endsWith('/auth/login')) return json({ accessToken: 't' })
    if (pathname.endsWith('/api/v1/stokvels')) return json([MABENA, SOWETO].map(({ id, name }) => ({ id, name })))
    const m = pathname.match(/\/stokvels\/([^/]+)(\/contributions)?$/)
    if (!m) return new Response(null, { status: 404 })
    const s = m[1] === 'mabena' ? MABENA : SOWETO
    if (s === SOWETO) await sleep(300) // the second stokvel answers slowly, or the bug cannot appear
    if (!m[2]) return json({ id: s.id, name: s.name })
    return json({
      items: [{ id: `${s.id}-1`, memberName: 'Member', amountInCents: s.cents, status: 'Paid', recordedAt: '2026-10-01T08:00:00Z', cycleId: 'c1' }],
      nextPageToken: '',
    })
  }))
}

beforeEach(() => { __resetSessionForTests(); installFakeApi() })
afterEach(() => vi.unstubAllGlobals())

describe('switching stokvels', () => {
  it('never shows the previous stokvel contributions after a switch', async () => {
    await login('a@b.co', 'pw')
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(<QueryClientProvider client={client}><StokvelWorkspace /></QueryClientProvider>)

    expect((await screen.findAllByText(money(MABENA.cents))).length).toBeGreaterThan(0)
    expect(screen.getByText('Mabena Family', { selector: 'h2' })).toBeInTheDocument()

    await userEvent.selectOptions(screen.getByLabelText('Stokvel'), 'soweto')

    // immediately after the switch: no Mabena name, no Mabena amount, the skeleton instead
    expect(screen.queryAllByText(money(MABENA.cents))).toHaveLength(0)
    expect(screen.queryByText('Mabena Family', { selector: 'h2' })).not.toBeInTheDocument()
    expect(screen.getByTestId('stokvel-skeleton')).toBeInTheDocument()

    expect((await screen.findAllByText(money(SOWETO.cents))).length).toBeGreaterThan(0)
    expect(screen.getByText('Soweto Teachers', { selector: 'h2' })).toBeInTheDocument()
  })
})