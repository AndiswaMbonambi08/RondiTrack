import type { MeHomeEntry } from '@/api/types';
import { DueCountdown } from '@/components/DueCountdown';
import { ArrearsBadge, StatusBadge } from '@/components/StatusBadge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { formatDate } from '@/lib/dates';
import { formatCents } from '@/lib/money';
import { isTreasurer, roleText } from '@/lib/roles';

type Props = { entry: MeHomeEntry };

export function StokvelCard({ entry }: Props) {
  const { name, role, currentCycle, paidCents, owedCents, arrearsCents, nextPayoutUtc } = entry;

  return (
    <Card className="min-w-0">
      <CardHeader>
        <CardTitle>
          <h3 className="break-words text-base font-semibold">{name}</h3>
        </CardTitle>
        {/* Role stays plain text with context: "You're the Treasurer", not a bare coloured badge. */}
        <p className="text-sm text-muted-foreground">{roleText(role)}</p>
      </CardHeader>

      <CardContent className="space-y-2 text-sm">
        {currentCycle === null ? (
          <p>No open cycle</p>
        ) : (
          <>
            <p className="flex flex-wrap items-center gap-2">
              <span className="break-words">Cycle: {currentCycle.label}</span>
              <StatusBadge kind="cycle" status={currentCycle.status} />
            </p>
            {currentCycle.dueDate && (
              <DueCountdown dueDate={currentCycle.dueDate} cycleStatus={currentCycle.status} />
            )}
            <p>
              Paid this cycle: <span className="whitespace-nowrap">{formatCents(paidCents)}</span>
            </p>
            {owedCents === 0 ? (
              <p>All paid up for this cycle</p>
            ) : (
              <p>
                Still owed: <span className="whitespace-nowrap">{formatCents(owedCents)}</span>
              </p>
            )}
          </>
        )}

        {/* The caller's own arrears only. No arrears means no badge. */}
        <ArrearsBadge cents={arrearsCents} />

        <p>Next payout: {nextPayoutUtc === null ? 'No payout scheduled' : formatDate(nextPayoutUtc)}</p>

        {isTreasurer(role) ? (
          <section className="space-y-2 border-t pt-3">
            <h4 className="font-semibold">Treasurer jobs</h4>
            <div className="flex flex-wrap gap-2">
              <Button type="button" disabled className="min-h-11">
                Close cycle
              </Button>
              <Button type="button" disabled className="min-h-11">
                Record payout
              </Button>
            </div>
          </section>
        ) : null}
      </CardContent>
    </Card>
  );
}
