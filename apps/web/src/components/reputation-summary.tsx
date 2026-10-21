import type { ReputationSummaryResponse } from "@blindmesh/api-client";

import { reputationNumber } from "@/lib/reputation";

/**
 * The score of a contributor and the counts behind it. The numbers are safe
 * aggregates: they never name a source of a contribution, so the summary may
 * be shown to every visitor.
 */
export function ReputationSummary({ summary }: { summary: ReputationSummaryResponse }) {
  const score = reputationNumber(summary.current_score);

  return (
    <section
      aria-labelledby="reputation-summary-heading"
      className="rounded-lg border border-border bg-surface p-4 sm:p-6"
    >
      <h2 id="reputation-summary-heading" className="text-large font-semibold">
        Reputation
      </h2>

      <p className="mt-2 text-small text-muted-foreground">
        The score only comes from finally accepted contributions. It can never be bought.
      </p>

      <p className="mt-4 text-title font-semibold">
        <span className="text-small font-normal text-muted-foreground">Score </span>
        {score}
      </p>

      <dl className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-3">
        <Count label="Accepted contributions" value={reputationNumber(summary.accepted_contributions)} />
        <Count label="Confirmed findings" value={reputationNumber(summary.confirmed_findings)} />
        <Count label="Reversed contributions" value={reputationNumber(summary.reversed_contributions)} />
      </dl>
    </section>
  );
}

/** One count of the summary. */
function Count({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-md border border-border bg-background p-3">
      <dt className="text-small text-muted-foreground">{label}</dt>
      <dd className="mt-1 text-large font-semibold">{value}</dd>
    </div>
  );
}
