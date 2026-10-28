import { useState } from 'react';
import { ChevronDown } from 'lucide-react';
import type { ContributionItem, CycleSummary } from '@/lib/types';
import { ContributionCard } from '@/components/ContributionCard';
import { DueCountdown } from '@/components/DueCountdown';
import { StatusBadge } from '@/components/StatusBadge';
import { Button } from '@/components/ui/button';
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible';
import { formatCalendarDate } from '@/lib/dates';
import { groupContributionsByCycle } from '@/lib/groupContributionsByCycle';
import { formatCents } from '@/lib/money';

const UNKNOWN = 'unknown-cycle';

type Props = { cycles: CycleSummary[]; contributions: ContributionItem[] };

export function ContributionList({ cycles, contributions }: Props) {
  // Derived during render: no useState, no useEffect for the grouping.
  const groups = groupContributionsByCycle(cycles, contributions);
  type Group = (typeof groups)[number];

  // The state lives HERE because a group's own toggle and "Expand all" both need it.
  // Keyed by cycle id (never by position). It stores only what the user changed;
  // the default (Open cycle expanded, the rest collapsed) is computed, not stored.
  const [overrides, setOverrides] = useState<Record<string, boolean>>({});

  const idOf = (g: Group) => g.cycle?.id ?? UNKNOWN;
  const isOpen = (g: Group) => overrides[idOf(g)] ?? g.cycle?.status === 'Open';
  const allOpen = groups.every(isOpen); // the button label is derived, not stored

  const setAll = (open: boolean) =>
    setOverrides(Object.fromEntries(groups.map((g) => [idOf(g), open])));

  return (
    <section aria-labelledby="contributions-heading" className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 id="contributions-heading" className="text-lg font-semibold">
          Contributions
        </h2>
        {groups.length > 0 && (
          <Button type="button" variant="outline" className="min-h-11" onClick={() => setAll(!allOpen)}>
            {allOpen ? 'Collapse all' : 'Expand all'}
          </Button>
        )}
      </div>

      <ul className="space-y-3">
        {groups.map((g) => (
          <li key={idOf(g)}>
            <Collapsible
              open={isOpen(g)}
              onOpenChange={(open) => setOverrides((prev) => ({ ...prev, [idOf(g)]: open }))}
              className="rounded-lg border bg-card text-card-foreground"
            >
              <CollapsibleTrigger className="flex min-h-11 w-full items-start gap-2 p-3 text-left [&[data-state=open]>svg]:rotate-180">
                <span className="flex min-w-0 flex-1 flex-col gap-1">
                  <span className="break-words text-base font-semibold">
                    {g.cycle ? `Cycle ${g.cycle.label}` : 'Unknown cycle'}
                  </span>
                  {g.cycle && (
                    <>
                      <span className="flex flex-wrap items-center gap-2">
                        <StatusBadge kind="cycle" status={g.cycle.status} />
                        {g.cycle.dueDate && (
                          <span className="text-sm text-muted-foreground">
                            Due {formatCalendarDate(g.cycle.dueDate)}
                          </span>
                        )}
                      </span>
                      {g.cycle.dueDate && g.cycle.status === 'Open' && (
                        <DueCountdown dueDate={g.cycle.dueDate} cycleStatus={g.cycle.status} />
                      )}
                      <span className="text-sm">
                        Collected{' '}
                        <span className="whitespace-nowrap">{formatCents(g.cycle.collectedCents)}</span>
                        {' of '}
                        <span className="whitespace-nowrap">{formatCents(g.cycle.amountDueCents)}</span>
                      </span>
                    </>
                  )}
                </span>
                <ChevronDown aria-hidden="true" className="mt-1 size-5 shrink-0 transition-transform" />
              </CollapsibleTrigger>

              <CollapsibleContent>
                <ul className="space-y-2 px-3 pb-3">
                  {g.contributions.length === 0 ? (
                    <li className="text-sm text-muted-foreground">No contributions yet</li>
                  ) : (
                    g.contributions.map((c) => (
                      <li key={c.id}>
                        <ContributionCard contribution={c} />
                      </li>
                    ))
                  )}
                </ul>
              </CollapsibleContent>
            </Collapsible>
          </li>
        ))}
      </ul>
    </section>
  );
}
