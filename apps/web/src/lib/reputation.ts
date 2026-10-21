import {
  createClient,
  type ClientResult,
  type ReputationHistoryResponse,
  type ReputationSummaryResponse,
} from "@blindmesh/api-client";

/**
 * The browser reaches the API through the same origin path /api/v1, so the
 * session cookie and the CSRF token stay with the request. Every read of a
 * reputation uses no-store, so nothing stays in the client cache.
 */
const client = createClient({
  baseUrl: "/api/v1",
  cache: "no-store",
  credentials: "same-origin",
});

export type ReputationSummaryState =
  | { kind: "loading" }
  | { kind: "ready"; summary: ReputationSummaryResponse }
  | { kind: "not-found" }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type ReputationHistoryState =
  | { kind: "loading" }
  | { kind: "ready"; history: ReputationHistoryResponse }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

/** Reads the public reputation summary of a contributor. */
export async function loadPublicSummary(username: string): Promise<ReputationSummaryState> {
  const result = await client.getPublicReputation(username);

  return toSummaryState(result);
}

/** Reads the public history of the reputation of a contributor. */
export async function loadPublicHistory(username: string): Promise<ReputationHistoryState> {
  const result = await client.getPublicReputationHistory(username);

  return toHistoryState(result);
}

/** Reads the own history of the reputation of the signed in contributor. */
export async function loadOwnHistory(): Promise<ReputationHistoryState> {
  const result = await client.getOwnReputationHistory();

  return toHistoryState(result);
}

/**
 * The counts of the wire format may be a number or a string, so every number
 * is read through this helper.
 */
export function reputationNumber(value: number | string | null | undefined): number {
  if (typeof value === "number") {
    return Number.isFinite(value) ? value : 0;
  }

  if (typeof value === "string") {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  }

  return 0;
}

function toSummaryState(result: ClientResult<ReputationSummaryResponse>): ReputationSummaryState {
  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The reputation is not readable right now." };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The reputation is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "ready", summary: result.data };
}

function toHistoryState(result: ClientResult<ReputationHistoryResponse>): ReputationHistoryState {
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

  return { kind: "ready", history: result.data };
}
