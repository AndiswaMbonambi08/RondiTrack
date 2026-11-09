// The test for signed-in specs. After every test it saves the session again, because the refresh token
// rotates: a saved session that was not saved back would be refused on the next run.
import { test as base } from '@playwright/test'
import { SIPHO_STATE } from './paths'

export const test = base.extend<{ saveSession: void }>({
  saveSession: [
    async ({ context }, provide) => {
      await provide()
      await context.storageState({ path: SIPHO_STATE })
    },
    { auto: true },
  ],
})

export { expect } from '@playwright/test'