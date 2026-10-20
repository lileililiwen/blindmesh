"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";

import { useSession } from "@/components/session-provider";
import { Button } from "@/components/ui/button";
import { credits, loadWalletHistory, type HistoryState } from "@/lib/credits";
import type { CreditTransactionResponse } from "@blindmesh/api-client";

/**
 * The history of the wallet of the signed in contributor. Every transaction
 * shows what it was about and how it changed the balances of the wallet. The
 * entries of another wallet are never part of the answer.
 */
export function WalletHistory() {
  const { state: session } = useSession();
  const [state, setState] = useState<HistoryState>({ kind: "loading" });
  const [transactions, setTransactions] = useState<CreditTransactionResponse[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [loadingMore, setLoadingMore] = useState(false);

  const load = useCallback(async () => {
    setState({ kind: "loading" });

    const next = await loadWalletHistory();

    if (next.kind === "ready") {
      setTransactions(next.page.items ?? []);
      setCursor(next.page.next_cursor ?? null);
    }

    setState(next);
  }, []);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      if (session.kind === "loading") {
        return;
      }

      const next = await loadWalletHistory();

      if (cancelled) {
        return;
      }

      if (next.kind === "ready") {
        setTransactions(next.page.items ?? []);
        setCursor(next.page.next_cursor ?? null);
      }

      setState(next);
    })();

    return () => {
      cancelled = true;
    };
  }, [session.kind]);

  async function loadMore() {
    if (!cursor || loadingMore) {
      return;
    }

    setLoadingMore(true);

    const next = await loadWalletHistory(20, cursor);

    if (next.kind === "ready") {
      setTransactions((current) => [...current, ...(next.page.items ?? [])]);
      setCursor(next.page.next_cursor ?? null);
    } else {
      setState(next);
    }

    setLoadingMore(false);
  }

  if (session.kind === "loading" || state.kind === "loading") {
    return (
      <section aria-labelledby="history-heading" aria-busy="true" className="reading-width">
        <h1 id="history-heading" className="text-title font-semibold">
          Wallet history
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading the history of your wallet.</p>
        <div className="mt-4 flex flex-col gap-3" aria-hidden="true">
          <div className="h-16 w-full rounded bg-muted" />
          <div className="h-16 w-full rounded bg-muted" />
        </div>
      </section>
    );
  }

  if (state.kind === "unauthorized") {
    return (
      <section aria-labelledby="history-heading" className="reading-width">
        <h1 id="history-heading" className="text-title font-semibold">
          Wallet history
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          The history of a wallet is private. Sign in to read it.
        </p>
        <p className="mt-4">
          <Button asChild variant="primary">
            <Link href="/sign-in?return=%2Fwallet%2Fhistory">Sign in</Link>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "not-found") {
    return (
      <section aria-labelledby="history-heading" className="reading-width">
        <h1 id="history-heading" className="text-title font-semibold">
          Wallet history
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          This wallet does not exist. It belongs to another contributor.
        </p>
      </section>
    );
  }

  if (state.kind === "error" || state.kind === "unavailable") {
    return (
      <section aria-labelledby="history-heading" className="reading-width">
        <h1 id="history-heading" className="text-title font-semibold">
          Wallet history
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          {state.message}
        </p>
        <p className="mt-4">
          <Button type="button" variant="secondary" onClick={() => void load()}>
            Try again
          </Button>
        </p>
      </section>
    );
  }

  return (
    <section aria-labelledby="history-heading" className="reading-width">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 id="history-heading" className="text-title font-semibold">
            Wallet history
          </h1>
          <p className="mt-2 text-muted-foreground">
            Every movement of your credits, newest first. All dates are UTC.
          </p>
        </div>

        <Button asChild variant="secondary">
          <Link href="/wallet">Back to the wallet</Link>
        </Button>
      </div>

      {transactions.length === 0 ? (
        <div className="mt-6 rounded-lg border border-border bg-surface p-6">
          <h2 className="text-large font-semibold">No transaction yet</h2>
          <p className="mt-2 text-muted-foreground">
            Your wallet has no movement yet. The grant of new contributors adds the first transaction.
          </p>
          <p className="mt-4">
            <Button asChild variant="primary">
              <Link href="/wallet">Open the wallet</Link>
            </Button>
          </p>
        </div>
      ) : (
        <>
          <ul className="mt-6 flex flex-col gap-3">
            {transactions.map((transaction) => (
              <li key={transaction.id}>
                <article className="rounded-lg border border-border bg-surface p-4">
                  <div className="flex flex-wrap items-center gap-2">
                    <h2 className="text-body font-semibold">{describeKind(transaction.kind)}</h2>
                    <span className="text-small text-muted-foreground">
                      {formatDate(transaction.created_at)}
                    </span>
                  </div>

                  <p className="mt-1 text-small text-muted-foreground">
                    Reference: {transaction.reference}
                  </p>

                  <ul className="mt-2 flex flex-col gap-1 text-small">
                    {transaction.changes.map((change) => (
                      <li key={change.bucket}>
                        {describeBucket(change.bucket)}: {formatAmount(change.amount)}
                      </li>
                    ))}
                  </ul>
                </article>
              </li>
            ))}
          </ul>

          {cursor ? (
            <p className="mt-5">
              <Button type="button" variant="secondary" disabled={loadingMore} onClick={() => void loadMore()}>
                {loadingMore ? "Loading more transactions" : "Load more transactions"}
              </Button>
            </p>
          ) : null}
        </>
      )}
    </section>
  );
}

/** The name of a kind of transaction. */
function describeKind(kind: string): string {
  switch (kind) {
    case "grant":
      return "Grant";
    case "reserve":
      return "Reserved for a task";
    case "begin_pending":
      return "Work started";
    case "settle":
      return "Payment";
    case "refund":
      return "Refund";
    case "expiry":
      return "Credits expired";
    default:
      return "Movement";
  }
}

/** The name of a bucket of the ledger. */
function describeBucket(bucket: string): string {
  switch (bucket) {
    case "available":
      return "Available";
    case "reserved":
      return "Reserved";
    case "pending":
      return "Pending";
    case "issuance":
      return "Issued by the platform";
    case "expiry":
      return "Expired";
    default:
      return bucket;
  }
}

/** A signed amount of credits. */
function formatAmount(amount: number | string): string {
  const value = credits(amount);
  return value > 0 ? `+${value}` : `${value}`;
}

/** The date of a moment, written as a plain calendar day of UTC. */
function formatDate(value: string): string {
  return new Date(value).toISOString().slice(0, 10);
}
