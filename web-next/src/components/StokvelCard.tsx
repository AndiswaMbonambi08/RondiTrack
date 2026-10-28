import Link from 'next/link';
import type { MeHomeEntry } from '@/lib/types';
import { DueCountdown } from '@/components/DueCountdown';
import { ArrearsBadge, StatusBadge } from '@/components/StatusBadge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { formatDate } from '@/lib/dates';
import { formatCents } from '@/lib/money';
import { isTreasurer, roleText } from '@/lib/roles';

type Props = { entry: MeHomeEntry; href?: string };

export function StokvelCard({ entry, href }: Props) {
  const { name, role, currentCycle, paidCents, owedCents, arrearsCents, nextPayoutUtc } = entry;

  return (
    <Card className="h-full min-w-0 rounded-3xl shadow-sm ring-0 [--card-spacing:--spacing(5)]">
      <CardHeader>
        {/* The stokvel name and its cycle share one line, and wrap on a narrow screen. */}
        <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1">
          <CardTitle className="min-w-0">
            <h3 className="break-words text-lg font-semibold">{name}</h3>
          </CardTitle>
          {currentCycle !== null && (
            <p className="flex items-center gap-2 text-sm">
              <span className="whitespace-nowrap">Cycle: {currentCycle.label}</span>
              <StatusBadge kind="cycle" status={currentCycle.status} />
            </p>
          )}
        </div>
        {/* Role stays plain text with context: "You're the Treasurer", not a bare coloured badge. */}
        <p className="text-sm text-muted-foreground">{roleText(role)}</p>
      </CardHeader>

      <CardContent className="flex flex-1 flex-col gap-2 text-sm">
        {currentCycle === null ? (
          <p>No open cycle</p>
        ) : (
          <>
            {currentCycle.dueDate && (
              <DueCountdown dueDate={currentCycle.dueDate} cycleStatus={currentCycle.status} />
            )}
            <p>
              Paid this cycle:{' '}
              <span className="whitespace-nowrap font-semibold tabular-nums">{formatCents(paidCents)}</span>
            </p>
            {owedCents === 0 ? (
              <p>All paid up for this cycle</p>
            ) : (
              <p>
                Still owed:{' '}
                <span className="whitespace-nowrap font-semibold tabular-nums">{formatCents(owedCents)}</span>
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
              <Button type="button" disabled className="min-h-11 rounded-full px-5">
                Close cycle
              </Button>
              <Button type="button" disabled className="min-h-11 rounded-full px-5">
                Record payout
              </Button>
            </div>
          </section>
        ) : null}

        {href ? (
          <div className="mt-auto pt-3">
            <Link
              href={href}
              className="inline-flex min-h-11 items-center rounded-full bg-primary px-5 text-sm font-semibold text-primary-foreground hover:bg-primary/80"
            >
              View stokvel
            </Link>
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}