// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { recordContribution, type ContributionFormState } from '@/app/actions/contributions'
import { ContributionForm } from './ContributionForm'

// The Server Action is the edge of this component, so it is the one thing replaced.
vi.mock('@/app/actions/contributions', () => ({ recordContribution: vi.fn() }))
const action = vi.mocked(recordContribution)

const state = (over: Partial<ContributionFormState> = {}): ContributionFormState => ({
  status: 'error',
  values: { memberId: 'me', amount: '150' },
  key: 'key-1',
  keyIntent: '',
  topErrors: [],
  fieldErrors: {},
  ...over,
})

function renderForm() {
  return render(
    <QueryClientProvider client={new QueryClient()}>
      <ContributionForm
        stokvelId="s1"
        cycleId="c1"
        cycleLabel="2026-10"
        selfId="me"
        selfName="Sipho"
        owedText={null}
        members={null}
        initialKey="key-1"
        initialAmount="150"
      />
    </QueryClientProvider>,
  )
}

const amountField = () => screen.getByLabelText(/amount \(rands\)/i)
const submit = (name = /record contribution/i) => screen.getByRole('button', { name })

beforeEach(() => {
  action.mockReset()
})

describe('the contribution form shows every answer', () => {
  it('puts a 400 under the amount field, tied to it, and keeps what was typed', async () => {
    action.mockImplementationOnce(async (_prev, data) =>
      state({
        message: 'Check the form.',
        fieldErrors: { amount: 'Enter rands like 150 or 150.50, with at most two decimals.' },
        values: { memberId: 'me', amount: String(data.get('amount')) },
      }),
    )
    const user = userEvent.setup()
    renderForm()

    await user.clear(amountField())
    await user.type(amountField(), 'abc')
    await user.click(submit())

    const message = 'Enter rands like 150 or 150.50, with at most two decimals.'
    expect(await screen.findByText(message)).toBeInTheDocument()
    expect(amountField()).toHaveAccessibleDescription(message)
    expect(amountField()).toBeInvalid()
    await waitFor(() => expect(amountField()).toHaveValue('abc'))
  })

  it('shows a 409 at the top of the form with its reference', async () => {
    action.mockResolvedValueOnce(
      state({ message: "This contribution can't be recorded right now.", reference: 'corr-9' }),
    )
    const user = userEvent.setup()
    renderForm()

    await user.click(submit())

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent("This contribution can't be recorded right now.")
    expect(alert).toHaveTextContent('Reference: corr-9')
  })

  it('shows the success message', async () => {
    action.mockResolvedValueOnce(
      state({ status: 'success', recordedCents: 15050, values: { memberId: 'me', amount: '' }, key: 'key-2' }),
    )
    const user = userEvent.setup()
    renderForm()

    await user.click(submit())

    const status = await screen.findByRole('status')
    expect(status).toHaveTextContent(/Recorded/)
    expect(status).toHaveTextContent(/150,50/)
  })
})

describe('one click, one request', () => {
  it('sends nothing on a second click while the first is pending', async () => {
    let release!: (s: ContributionFormState) => void
    action.mockImplementationOnce(() => new Promise<ContributionFormState>((r) => { release = r }))
    const user = userEvent.setup()
    renderForm()

    await user.click(submit())
    const pending = await screen.findByRole('button', { name: /recording/i })
    expect(pending).toBeDisabled()
    await user.click(pending)
    expect(action).toHaveBeenCalledTimes(1)

    await act(async () => {
      release(state({ status: 'success', recordedCents: 15000, values: { memberId: 'me', amount: '' }, key: 'key-2' }))
    })
    expect(await screen.findByRole('status')).toHaveTextContent(/Recorded/)
  })
})

describe('the key that makes a retry safe', () => {
  it('sends the same key on the retry after a dropped connection, and a new one for the next contribution', async () => {
    const keys: string[] = []
    action.mockImplementation(async (_prev, data) => {
      keys.push(String(data.get('idempotencyKey')))
      if (keys.length === 1) {
        // The connection dropped: the server keeps the key and says it may have landed.
        return state({
          message: 'We could not reach RondiTrack, so we cannot tell if this payment was recorded.',
          mayHaveLanded: true,
          key: 'key-1',
          keyIntent: 's1|c1|me|15000',
        })
      }
      // Success: the server hands out a brand new key for the next contribution.
      return state({ status: 'success', recordedCents: 15000, key: 'key-2', keyIntent: '', values: { memberId: 'me', amount: '' } })
    })
    const user = userEvent.setup()
    renderForm()

    await user.click(submit())
    await screen.findByRole('alert')
    await user.click(await screen.findByRole('button', { name: /try again/i }))
    await screen.findByRole('status')

    await user.type(amountField(), '150')
    await user.click(submit())
    await waitFor(() => expect(keys).toHaveLength(3))

    expect(keys[0]).toBe('key-1')
    expect(keys[1]).toBe(keys[0]) // the retry reuses the key
    expect(keys[2]).toBe('key-2') // the next contribution has a different one
    expect(keys[2]).not.toBe(keys[1])
  })
})