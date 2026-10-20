import type { MeHomeEntry } from '../api/types';
import { formatDate } from '../lib/dates';
import { formatCents } from '../lib/money';
import { isTreasurer, roleText } from '../lib/roles';

type Props = {
  entry: MeHomeEntry;
};

export function StokvelCard({ entry }: Props) {
  const { name, role, currentCycle, paidCents, owedCents, nextPayoutUtc } = entry;

  return (
    <article className="card">
      <h2>{name}</h2>
      <p className="role">{roleText(role)}</p>

      {currentCycle === null ? (
        <p>No open cycle</p>
      ) : (
        <div>
          <p>Cycle: {currentCycle.label}</p>
          <p>Paid this cycle: {formatCents(paidCents)}</p>
          {owedCents === 0 ? (
            <p>All paid up for this cycle</p>
          ) : (
            <p>Still owed: {formatCents(owedCents)}</p>
          )}
        </div>
      )}

      <p>
        Next payout:{' '}
        {nextPayoutUtc === null ? 'No payout scheduled' : formatDate(nextPayoutUtc)}
      </p>

      {isTreasurer(role) ? (
        <section className="treasurer">
          <h3>Treasurer jobs</h3>
          <button type="button" disabled>
            Close cycle
          </button>
          <button type="button" disabled>
            Record payout
          </button>
        </section>
      ) : null}
    </article>
  );
}