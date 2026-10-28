import {
  Banknote, CircleDot, CircleHelp, Clock, Lock, ShieldCheck, TriangleAlert, UserCheck,
  type LucideIcon,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { formatCents } from '@/lib/money';
import { cn } from '@/lib/utils';

const TONES = {
  neutral: 'bg-secondary text-secondary-foreground border-border',
  info: 'bg-info text-info-foreground border-info-foreground/50',
  success: 'bg-success text-success-foreground border-success-foreground/50',
  warning: 'bg-warning text-warning-foreground border-warning-foreground/50',
  violet: 'bg-violet text-violet-foreground border-violet-foreground/50',
  danger: 'bg-danger text-danger-foreground border-danger-foreground/50',
} as const;

type Look = { label: string; Icon: LucideIcon; tone: keyof typeof TONES; extra?: string };

const LOOKS: Record<'cycle' | 'contribution', Record<string, Look>> = {
  cycle: {
    Open: { label: 'Open', Icon: CircleDot, tone: 'info' },
    Closed: { label: 'Closed', Icon: Lock, tone: 'neutral' },
    PaidOut: { label: 'Paid out', Icon: Banknote, tone: 'violet', extra: 'border-solid font-semibold' },
  },
  contribution: {
    Pending: { label: 'Pending', Icon: Clock, tone: 'warning' },
    // Member's claim: dashed border + "unconfirmed" wording so it never looks like a treasurer's check
    Paid: { label: 'Paid (unconfirmed)', Icon: UserCheck, tone: 'info', extra: 'border-dashed' },
    Confirmed: { label: 'Confirmed', Icon: ShieldCheck, tone: 'success', extra: 'font-semibold' },
  },
};

type Props = { kind: 'cycle' | 'contribution'; status: string };

export function StatusBadge({ kind, status }: Props) {
  const look: Look = LOOKS[kind][status] ?? { label: status, Icon: CircleHelp, tone: 'neutral' };
  const { label, Icon, tone, extra } = look;
  return (
    <Badge variant="outline" className={cn('h-auto gap-1 whitespace-normal text-xs', TONES[tone], extra)}>
      <Icon aria-hidden="true" className="size-3.5 shrink-0" />
      {label}
    </Badge>
  );
}

/** No arrears means no badge, not a badge saying "R 0,00". */
export function ArrearsBadge({ cents }: { cents: number | null | undefined }) {
  if (!cents || cents <= 0) return null;
  return (
    <Badge variant="outline" className={cn('h-auto gap-1 whitespace-normal text-xs', TONES.danger)}>
      <TriangleAlert aria-hidden="true" className="size-3.5 shrink-0" />
      <span>In arrears: <span className="whitespace-nowrap">{formatCents(cents)}</span></span>
    </Badge>
  );
}
