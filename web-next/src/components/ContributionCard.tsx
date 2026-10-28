import { Card, CardContent } from '@/components/ui/card';
import { StatusBadge } from '@/components/StatusBadge';
import type { ContributionItem } from '@/lib/types';
import { formatDateTime } from '@/lib/dates';
import { formatCents } from '@/lib/money';

export function ContributionCard({ contribution }: { contribution: ContributionItem }) {
  return (
    <Card className="min-w-0 py-0">
      <CardContent className="flex flex-wrap items-center justify-between gap-2 px-3 py-3">
        <div className="min-w-0">
          <p className="whitespace-nowrap text-base font-medium">{formatCents(contribution.amountCents)}</p>
          <p className="text-sm text-muted-foreground">{formatDateTime(contribution.recordedAt)}</p>
        </div>
        <StatusBadge kind="contribution" status={contribution.status} />
      </CardContent>
    </Card>
  );
}
