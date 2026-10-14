import type { components } from "./api";

export type HomeResponse = components["schemas"]["HomeResponse"];

// Total a member still owes across every stokvel, in cents. Integer maths, so no rounding errors.
export function totalOwedCents(home: HomeResponse): number {
  return home.stokvels.reduce((sum, s) => sum + (s.owedCents ?? 0), 0);
}