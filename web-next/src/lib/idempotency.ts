// The rule from 7.4, as pure functions so it can be tested without a server.
export const intentOf = (stokvelId: string, cycleId: string, memberId: string, cents: number) =>
  [stokvelId, cycleId, memberId, cents].join('|')

/**
 * The same payment keeps its key (a retry after a dropped connection must be recognised).
 * A changed amount or member is a different payment, so it gets a new key.
 * keyIntentIn is empty when the key has not been used yet.
 */
export function chooseKey(
  keyIn: string,
  keyIntentIn: string,
  intent: string,
  makeKey: () => string = () => crypto.randomUUID(),
): string {
  return keyIntentIn !== '' && keyIntentIn !== intent ? makeKey() : keyIn
}