'use client'

import { useState, type ChangeEvent, type FormEvent } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { removeProof, uploadProof } from '@/app/actions/proof'
import { useToast } from '@/components/ToastProvider'
import { checkProofFile, PROOF_TYPES } from '@/lib/proofRules'

type Props = { stokvelId: string; contributionId: string; hasProof: boolean }

export function ProofUpload({ stokvelId, contributionId, hasProof }: Props) {
  const toast = useToast()
  const queryClient = useQueryClient()
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const inputId = `proof-${contributionId}`
  const errorId = `${inputId}-error`

  function onChange(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    setError(file ? checkProofFile(file) : null)
  }

  // The later pages cached for the list do not know about this change.
  const forgetCachedPages = () => queryClient.removeQueries({ queryKey: ['history'] })

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const form = e.currentTarget
    const file = new FormData(form).get('file')
    if (!(file instanceof File) || file.size === 0) {
      setError('Choose a file first.')
      return
    }
    const problem = checkProofFile(file)
    if (problem) {
      setError(problem)
      return
    }

    setError(null)
    setPending(true)
    const body = new FormData()
    body.set('stokvelId', stokvelId)
    body.set('contributionId', contributionId)
    body.set('file', file)
    try {
      const result = await uploadProof(body)
      if (result.status === 'ok') {
        toast.success('Proof of payment uploaded.')
        forgetCachedPages()
        form.reset()
      } else if (result.status === 'rejected') {
        setError(result.message) // a wrong file is never a toast
      } else {
        toast.failure(result.message, result.reference)
      }
    } catch {
      toast.failure('The upload did not go through. Check your connection and try again.')
    } finally {
      setPending(false)
    }
  }

  async function onRemove() {
    setPending(true)
    setError(null)
    try {
      const result = await removeProof(stokvelId, contributionId)
      if (result.status === 'ok') {
        toast.success('Proof of payment removed.')
        forgetCachedPages()
      } else if (result.status === 'rejected') {
        setError(result.message)
      } else {
        toast.failure(result.message, result.reference)
      }
    } catch {
      toast.failure('We could not remove the proof. Try again.')
    } finally {
      setPending(false)
    }
  }

  return (
    <form onSubmit={(e) => void onSubmit(e)} noValidate className="mt-2 w-full space-y-2 border-t pt-2">
      <label htmlFor={inputId} className="block text-sm font-medium">
        {hasProof ? 'Replace proof of payment' : 'Proof of payment'}
        <span className="block text-xs font-normal text-muted-foreground">JPEG, PNG or PDF, up to 5 MB</span>
      </label>
      <input
        id={inputId}
        name="file"
        type="file"
        accept={PROOF_TYPES.join(',')}
        onChange={onChange}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? errorId : undefined}
        className="block w-full text-sm"
      />
      {error && (
        <p id={errorId} role="alert" className="text-sm text-red-700 dark:text-red-300">
          {error}
        </p>
      )}
      <div className="flex flex-wrap gap-2">
        <button
          type="submit"
          disabled={pending}
          className="min-h-11 rounded-full bg-secondary px-5 py-2 text-sm font-semibold text-secondary-foreground hover:bg-accent disabled:opacity-60"
        >
          {pending ? 'Working...' : hasProof ? 'Replace' : 'Upload'}
        </button>
        {hasProof && (
          <button
            type="button"
            onClick={() => void onRemove()}
            disabled={pending}
            className="min-h-11 rounded-full border px-5 py-2 text-sm font-semibold disabled:opacity-60"
          >
            Remove
          </button>
        )}
      </div>
    </form>
  )
}