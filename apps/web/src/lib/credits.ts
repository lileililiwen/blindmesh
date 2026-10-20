import {
  createClient,
  type ClaimGrantBody,
  type ClientResult,
  type CreditTransactionPageResponse,
  type CreditWalletResponse,
} from "@blindmesh/api-client";

/**
 * The browser reaches the API through the same origin path /api/v1, so the
 * session cookie and the CSRF token stay with the request. Every read of a
 * wallet uses no-store, so nothing private stays in the client cache.
 */
const client = createClient({
  baseUrl: "/api/v1",
  cache: "no-store",
  credentials: "same-origin",
});

export type WalletState =
  | { kind: "loading" }
  | { kind: "ready"; wallet: CreditWalletResponse }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type HistoryState =
  | { kind: "loading" }
  | { kind: "ready"; page: CreditTransactionPageResponse }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type GrantState =
  | { kind: "idle" }
  | { kind: "claiming" }
  | { kind: "claimed"; wallet: CreditWalletResponse }
  | { kind: "invalid"; message: string }
  | { kind: "conflict"; message: string }
  | { kind: "ineligible"; message: string }
  | { kind: "limited"; message: string }
  | { kind: "not-found" }
  | { kind: "unauthorized" }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

const apiUnreachable = "The API did not answer. Nothing was changed.";

/**
 * The credits of the ledger are 64 bit numbers. The wire format may send them
 * as a number or as a string, so every amount is read through this helper.
 */
export function credits(value: number | string | null | undefined): number {
  if (typeof value === "number") {
    return Number.isFinite(value) ? value : 0;
  }

  if (typeof value === "string") {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  }

  return 0;
}

/** Reads the wallet of the signed in contributor. */
export async function loadWallet(): Promise<WalletState> {
  const result = await client.getWallet();

  return toWalletState(result);
}

/** Reads the history of the wallet of the signed in contributor. */
export async function loadWalletHistory(limit = 20, cursor?: string): Promise<HistoryState> {
  const result = await client.listWalletTransactions(limit, cursor);

  return toHistoryState(result);
}

/** Claims the limited grant of the signed in contributor. */
export async function claimGrant(
  body: ClaimGrantBody,
  csrfToken: string,
  idempotencyKey: string,
): Promise<GrantState> {
  const result = await client.claimGrant(body, csrfToken, idempotencyKey);

  return toGrantState(result);
}

/** A key for a money command. It holds no secret, it only names the command. */
export function newIdempotencyKey(): string {
  return crypto.randomUUID();
}

function toWalletState(result: ClientResult<CreditWalletResponse>): WalletState {
  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The wallet is not readable right now." };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The wallet is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "ready", wallet: result.data };
}

function toHistoryState(result: ClientResult<CreditTransactionPageResponse>): HistoryState {
  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The history is not readable right now." };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The history is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "ready", page: result.data };
}

function toGrantState(result: ClientResult<CreditWalletResponse>): GrantState {
  if (result.status === 0) {
    return { kind: "error", message: apiUnreachable };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return {
      kind: "ineligible",
      message: result.error?.error?.message ?? "The grant is not available for this account.",
    };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 409) {
    return {
      kind: "conflict",
      message:
        result.error?.error?.message ??
        "The grant was already claimed. The wallet shows the credits that are yours.",
    };
  }

  if (result.status === 422) {
    return {
      kind: "invalid",
      message: result.error?.error?.message ?? "Accept the rules of the marketplace before you claim the grant.",
    };
  }

  if (result.status === 429) {
    return {
      kind: "limited",
      message: result.error?.error?.message ?? "The grant of the day is used up. Try again tomorrow.",
    };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The grant is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "claimed", wallet: result.data };
}
