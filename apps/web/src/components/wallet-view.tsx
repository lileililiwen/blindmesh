"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";

import { useSession } from "@/components/session-provider";
import { Button } from "@/components/ui/button";
import {
  claimGrant,
  credits,
  loadWallet,
  newIdempotencyKey,
  type GrantState,
  type WalletState,
} from "@/lib/credits";
import type { CreditWalletResponse } from "@blindmesh/api-client";

/**
 * The wallet of the signed in contributor. It shows the available, the
 * reserved and the pending balance, the expiry of every lot and the action
 * that is left. The state of the server is the only truth of the page.
 */
export function WalletView() {
  const { state: session } = useSession();
  const [state, setState] = useState<WalletState>({ kind: "loading" });
  const [grant, setGrant] = useState<GrantState>({ kind: "idle" });
  const [accepted, setAccepted] = useState(false);

  const load = useCallback(async () => {
    setState({ kind: "loading" });

    const next = await loadWallet();

    setState(next);
  }, []);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      if (session.kind === "loading") {
        return;
      }

      const next = await loadWallet();

      if (cancelled) {
        return;
      }

      setState(next);
    })();

    return () => {
      cancelled = true;
    };
  }, [session.kind]);

  async function claim() {
    if (grant.kind === "claiming" || session.kind !== "authenticated") {
      return;
    }

    setGrant({ kind: "claiming" });

    const next = await claimGrant(
      { rules_accepted: accepted },
      session.csrfToken,
      newIdempotencyKey(),
    );

    if (next.kind === "claimed") {
      setState({ kind: "ready", wallet: next.wallet });
      setAccepted(false);
    }

    setGrant(next);
  }

  if (session.kind === "loading" || state.kind === "loading") {
    return (
      <section aria-labelledby="wallet-heading" aria-busy="true" className="reading-width">
        <h1 id="wallet-heading" className="text-title font-semibold">
          Wallet
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading the wallet.</p>
        <div className="mt-4 flex flex-col gap-3" aria-hidden="true">
          <div className="h-20 w-full rounded bg-muted" />
          <div className="h-20 w-full rounded bg-muted" />
        </div>
      </section>
    );
  }

  if (state.kind === "unauthorized") {
    return (
      <section aria-labelledby="wallet-heading" className="reading-width">
        <h1 id="wallet-heading" className="text-title font-semibold">
          Wallet
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          A wallet is private. Sign in to read the credits of your account.
        </p>
        <p className="mt-4">
          <Button asChild variant="primary">
            <Link href="/sign-in?return=%2Fwallet">Sign in</Link>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "not-found") {
    return (
      <section aria-labelledby="wallet-heading" className="reading-width">
        <h1 id="wallet-heading" className="text-title font-semibold">
          Wallet
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          This wallet does not exist. It belongs to another contributor.
        </p>
      </section>
    );
  }

  if (state.kind === "error" || state.kind === "unavailable") {
    return (
      <section aria-labelledby="wallet-heading" className="reading-width">
        <h1 id="wallet-heading" className="text-title font-semibold">
          Wallet
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

  const wallet = state.wallet;

  return (
    <section aria-labelledby="wallet-heading" className="reading-width">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 id="wallet-heading" className="text-title font-semibold">
            Wallet
          </h1>
          <p className="mt-2 text-muted-foreground">
            The credits of your account. Every amount is rebuilt from the journal of the marketplace.
          </p>
        </div>

        <Button asChild variant="secondary">
          <Link href="/wallet/history">Wallet history</Link>
        </Button>
      </div>

      <section aria-labelledby="wallet-balances-heading" className="mt-6">
        <h2 id="wallet-balances-heading" className="text-large font-semibold">
          Balances
        </h2>

        <dl className="mt-3 grid gap-3 sm:grid-cols-3">
          <Balance label="Available" value={credits(wallet.available)} />
          <Balance label="Reserved" value={credits(wallet.reserved)} />
          <Balance label="Pending" value={credits(wallet.pending)} />
        </dl>
      </section>

      <GrantPanel
        wallet={wallet}
        grant={grant}
        accepted={accepted}
        onAcceptedChange={setAccepted}
        onClaim={() => void claim()}
      />

      <section aria-labelledby="wallet-lots-heading" className="mt-8">
        <h2 id="wallet-lots-heading" className="text-large font-semibold">
          Credits and their expiry
        </h2>

        {wallet.lots.length === 0 ? (
          <p className="mt-2 text-muted-foreground">
            You hold no credits yet. The grant below adds 60 credits to your account.
          </p>
        ) : (
          <ul className="mt-3 flex flex-col gap-3">
            {wallet.lots.map((lot) => (
              <li key={lot.id}>
                <article className="rounded-lg border border-border bg-surface p-4">
                  <div className="flex flex-wrap items-center gap-2">
                    <h3 className="text-body font-semibold">
                      {lot.kind === "grant" ? "Grant" : "Earned credits"}
                    </h3>
                    <span className="text-small text-muted-foreground">from {lot.source}</span>
                    {lot.expired ? (
                      <span className="text-small text-muted-foreground">expired</span>
                    ) : null}
                  </div>

                  <dl className="mt-2 grid gap-1 text-small sm:grid-cols-2">
                    <div className="flex gap-2">
                      <dt className="text-muted-foreground">Spendable</dt>
                      <dd>{credits(lot.available_amount)}</dd>
                    </div>
                    <div className="flex gap-2">
                      <dt className="text-muted-foreground">Reserved</dt>
                      <dd>{credits(lot.reserved_amount)}</dd>
                    </div>
                    <div className="flex gap-2">
                      <dt className="text-muted-foreground">Pending</dt>
                      <dd>{credits(lot.pending_amount)}</dd>
                    </div>
                    <div className="flex gap-2">
                      <dt className="text-muted-foreground">
                        {lot.expires_at ? "Expires" : "Expiry"}
                      </dt>
                      <dd>{lot.expires_at ? formatDate(lot.expires_at) : "never"}</dd>
                    </div>
                  </dl>
                </article>
              </li>
            ))}
          </ul>
        )}

        <p className="mt-3 text-small text-muted-foreground">All dates are UTC.</p>
      </section>
    </section>
  );
}

function Balance({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-lg border border-border bg-surface p-4">
      <dt className="text-small text-muted-foreground">{label}</dt>
      <dd className="mt-1 text-large font-semibold">{value}</dd>
    </div>
  );
}

function GrantPanel({
  wallet,
  grant,
  accepted,
  onAcceptedChange,
  onClaim,
}: {
  wallet: CreditWalletResponse;
  grant: GrantState;
  accepted: boolean;
  onAcceptedChange: (value: boolean) => void;
  onClaim: () => void;
}) {
  const eligibility = wallet.grant;
  const claiming = grant.kind === "claiming";

  return (
    <section aria-labelledby="wallet-grant-heading" className="mt-8">
      <h2 id="wallet-grant-heading" className="text-large font-semibold">
        The grant of new contributors
      </h2>

      <p className="mt-2 text-muted-foreground">
        {eligibility.eligible
          ? `You can claim ${credits(eligibility.amount)} credits. They stay valid for ${credits(
              eligibility.expires_in_days,
            )} days.`
          : "The grant is not available for your account."}
      </p>

      {eligibility.reasons.length > 0 ? (
        <ul className="mt-3 flex flex-col gap-1 text-small">
          {eligibility.reasons.map((reason) => (
            <li key={reason}>{reason}</li>
          ))}
        </ul>
      ) : null}

      {eligibility.claimed_at ? (
        <p className="mt-2 text-small text-muted-foreground">
          You claimed the grant on {formatDate(eligibility.claimed_at)}.
        </p>
      ) : null}

      {eligibility.eligible ? (
        <div className="mt-4 flex flex-col gap-3">
          <label className="flex items-start gap-2 text-small">
            <input
              type="checkbox"
              className="mt-1 h-4 w-4"
              checked={accepted}
              onChange={(event) => onAcceptedChange(event.target.checked)}
            />
            <span>I accept the rules of the marketplace and the expiry of the granted credits.</span>
          </label>

          <p>
            <Button type="button" variant="primary" disabled={!accepted || claiming} onClick={onClaim}>
              {claiming
                ? "Claiming the grant"
                : `Claim ${credits(eligibility.amount)} credits`}
            </Button>
          </p>
        </div>
      ) : null}

      <div role="status" aria-live="polite" className="mt-3 text-small">
        <GrantMessage grant={grant} amount={credits(eligibility.amount)} />
      </div>
    </section>
  );
}

function GrantMessage({ grant, amount }: { grant: GrantState; amount: number }) {
  switch (grant.kind) {
    case "idle":
    case "claiming":
      return <span className="text-muted-foreground"> </span>;
    case "claimed":
      return <span>{amount} credits are yours. They are on your wallet now.</span>;
    case "invalid":
      return <span>{grant.message}</span>;
    case "conflict":
      return <span>{grant.message}</span>;
    case "ineligible":
      return <span>{grant.message}</span>;
    case "limited":
      return <span>{grant.message}</span>;
    case "not-found":
      return <span>This account does not exist.</span>;
    case "unauthorized":
      return <span>Sign in again to claim the grant.</span>;
    case "unavailable":
      return <span>{grant.message}</span>;
    case "error":
      return <span>{grant.message}</span>;
    default:
      return null;
  }
}

/** The date of a moment, written as a plain calendar day of UTC. */
function formatDate(value: string): string {
  return new Date(value).toISOString().slice(0, 10);
}
