// A person is Treasurer PER STOKVEL, never as a user. The role comes from the API's answer for
// each stokvel (never from the session or the token), and this keeps only the Treasurer ones.
// Admins are not Treasurers: the dashboard lists stokvels where role === 'Treasurer'.
export function stokvelsWhereTreasurer<T extends { role: string }>(stokvels: readonly T[]): T[] {
  return stokvels.filter((s) => s.role === 'Treasurer')
}
