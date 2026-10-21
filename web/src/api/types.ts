import type { components } from './schema';

type Schemas = components['schemas'];

export type MeHome = Schemas['HomeResponse'];
export type MeHomeEntry = Schemas['HomeStokvelResponse'];
export type HomeCycle = Schemas['HomeCycleResponse'];
export type CycleSummary = Schemas['CycleSummaryResponse'];
export type ContributionItem = Schemas['ContributionItemResponse'];
