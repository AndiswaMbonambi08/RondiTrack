import type { components } from './schema';

type Schemas = components['schemas'];

export type MeHome = Schemas['HomeResponse'];
export type MeHomeEntry = Schemas['HomeStokvelResponse'];
export type HomeCycle = Schemas['HomeCycleResponse'];

// ContributionItem is added once the API publishes a typed contributions list.