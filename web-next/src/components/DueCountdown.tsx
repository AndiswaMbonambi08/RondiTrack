'use client'

import { AlarmClock } from 'lucide-react';
import { useSaMidnightClock } from '@/hooks/useSaMidnightClock';
import { describeDue } from '@/lib/dueDate';
import { cn } from '@/lib/utils';

type Props = { dueDate: string; cycleStatus: string };

export function DueCountdown({ dueDate, cycleStatus }: Props) {
  const now = useSaMidnightClock();
  const due = describeDue(dueDate, cycleStatus, now);
  if (!due) return null;
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 text-sm font-medium',
        due.kind === 'overdue' ? 'text-danger-foreground' : 'text-foreground',
      )}
    >
      <AlarmClock aria-hidden="true" className="size-4 shrink-0" />
      {due.text}
    </span>
  );
}
