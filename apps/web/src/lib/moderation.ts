import {
  createClient,
  type AppealDisputeBody,
  type BanUserBody,
  type BanViewResponse,
  type ClientResult,
  type CloseDisputeBody,
  type CreateReportBody,
  type DisputeSummaryResponse,
  type DisputeViewResponse,
  type OpenDisputeBody,
  type ReportReceiptResponse,
  type ReportViewResponse,
  type RequestEvidenceBody,
  type ResolveDisputeBody,
} from "@blindmesh/api-client";

/**
 * The browser reaches the API through the same origin path /api/v1, so the
 * session cookie and the CSRF token stay with the request. Every read of a
 * report or a case uses no-store, so nothing private stays in the client cache.
 */
const client = createClient({
  baseUrl: "/api/v1",
  cache: "no-store",
  credentials: "same-origin",
});

export type ReportQueueState =
  | { kind: "loading" }
  | { kind: "ready"; reports: ReportViewResponse[] }
  | { kind: "unauthorized" }
  | { kind: "forbidden"; message: string }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type ReportState =
  | { kind: "loading" }
  | { kind: "ready"; report: ReportReceiptResponse }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "forbidden"; message: string }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type ReportCommandState =
  | { kind: "idle" }
  | { kind: "saving" }
  | { kind: "saved"; report: ReportReceiptResponse }
  | { kind: "invalid"; fieldErrors: Record<string, string[]> }
  | { kind: "refused"; message: string }
  | { kind: "forbidden"; message: string }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type DisputeQueueState =
  | { kind: "loading" }
  | { kind: "ready"; disputes: DisputeSummaryResponse[] }
  | { kind: "unauthorized" }
  | { kind: "forbidden"; message: string }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type DisputeState =
  | { kind: "loading" }
  | { kind: "ready"; dispute: DisputeViewResponse }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "forbidden"; message: string }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type DisputeCommandState =
  | { kind: "idle" }
  | { kind: "saving" }
  | { kind: "saved"; dispute: DisputeViewResponse }
  | { kind: "invalid"; fieldErrors: Record<string, string[]> }
  | { kind: "conflict"; message: string }
  | { kind: "refused"; message: string }
  | { kind: "forbidden"; message: string }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type BanCommandState =
  | { kind: "idle" }
  | { kind: "saving" }
  | { kind: "saved"; ban: BanViewResponse }
  | { kind: "invalid"; fieldErrors: Record<string, string[]> }
  | { kind: "refused"; message: string }
  | { kind: "forbidden"; message: string }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type StandingState =
  | { kind: "allowed" }
  | { kind: "anonymous" }
  | { kind: "banned"; message: string }
  | { kind: "unknown"; message: string };

const apiUnreachable = "The API did not answer. Nothing was changed.";

/**
 * The bonus of a decision in whole credits. An empty field carries no bonus.
 * A value that is not a number carries no bonus either, and the server refuses
 * a negative bonus.
 */
export function bonusOf(value: string): number {
  if (value.trim() === "") {
    return 0;
  }

  const parsed = Number(value);

  return Number.isFinite(parsed) ? parsed : 0;
}

/** Reads the reports of the moderation staff. */
export async function loadReports(): Promise<ReportQueueState> {
  const result = await client.listReports();

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The reports are not readable right now." };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "forbidden", message: messageOf(result, "This area belongs to the moderation staff.") };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The reports are not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "ready", reports: result.data };
}

/** Reads the receipt of a report of the reporter. */
export async function loadReport(id: string): Promise<ReportState> {
  const result = await client.getReport(id);

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The receipt is not readable right now." };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "forbidden", message: "The receipt is not readable." };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The receipt is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "ready", report: result.data };
}

/** Writes a report of abusive content and reads the receipt of it back. */
export async function createReport(
  body: CreateReportBody,
  csrfToken: string,
  idempotencyKey: string,
): Promise<ReportCommandState> {
  return toReportState(await client.createReport(body, csrfToken, idempotencyKey));
}

/** Reads the cases of the moderation staff. */
export async function loadDisputes(): Promise<DisputeQueueState> {
  const result = await client.listDisputes();

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The cases are not readable right now." };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "forbidden", message: messageOf(result, "This area belongs to the moderation staff.") };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The cases are not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "ready", disputes: result.data };
}

/** Reads one case of an arbitration with its trail. */
export async function loadDispute(id: string): Promise<DisputeState> {
  const result = await client.getDispute(id);

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The case is not readable right now." };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "forbidden", message: "The case is not readable." };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The case is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "ready", dispute: result.data };
}

/** Opens the case of arbitration of a seat. */
export async function openDispute(
  submissionId: string,
  body: OpenDisputeBody,
  csrfToken: string,
  idempotencyKey: string,
): Promise<DisputeCommandState> {
  return toDisputeState(await client.openDispute(submissionId, body, csrfToken, idempotencyKey));
}

/** Asks the parties of a case for more evidence. */
export async function requestDisputeEvidence(
  id: string,
  body: RequestEvidenceBody,
  csrfToken: string,
  idempotencyKey: string,
): Promise<DisputeCommandState> {
  return toDisputeState(await client.requestDisputeEvidence(id, body, csrfToken, idempotencyKey));
}

/** Decides a case and moves the escrow of the seat. */
export async function resolveDispute(
  id: string,
  body: ResolveDisputeBody,
  csrfToken: string,
  idempotencyKey: string,
): Promise<DisputeCommandState> {
  return toDisputeState(await client.resolveDispute(id, body, csrfToken, idempotencyKey));
}

/** Closes a case whose decision stands. */
export async function closeDispute(
  id: string,
  body: CloseDisputeBody,
  csrfToken: string,
  idempotencyKey: string,
): Promise<DisputeCommandState> {
  return toDisputeState(await client.closeDispute(id, body, csrfToken, idempotencyKey));
}

/** Appeals the decision of a case once. */
export async function appealDispute(
  id: string,
  body: AppealDisputeBody,
  csrfToken: string,
  idempotencyKey: string,
): Promise<DisputeCommandState> {
  return toDisputeState(await client.appealDispute(id, body, csrfToken, idempotencyKey));
}

/** Bans an account and refuses its requests. */
export async function banUser(
  id: string,
  body: BanUserBody,
  csrfToken: string,
  idempotencyKey: string,
): Promise<BanCommandState> {
  const result = await client.banUser(id, body, csrfToken, idempotencyKey);

  if (result.status === 0) {
    return { kind: "error", message: apiUnreachable };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "forbidden", message: messageOf(result, "Only an administrator may ban an account.") };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 409) {
    return { kind: "refused", message: messageOf(result, "The ban conflicts with the state of the account.") };
  }

  if (result.status === 422) {
    return { kind: "invalid", fieldErrors: fieldErrorsOf(result) };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The ban is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "saved", ban: result.data };
}

/** A key for a command of the moderation module. It holds no secret. */
export function newModerationKey(): string {
  return crypto.randomUUID();
}

/**
 * Reads the standing of the account of the caller. A banned account is refused
 * by the API before it changes anything, and the API names the reason of the
 * refusal. The answer of the session alone does not carry that reason.
 */
export async function loadAccountStanding(): Promise<StandingState> {
  const result = await client.getSession();

  if (result.status === 401) {
    return { kind: "anonymous" };
  }

  if (result.status === 403) {
    return { kind: "banned", message: messageOf(result, "This account may not use Blindmesh.") };
  }

  if (!result.ok) {
    return { kind: "unknown", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "allowed" };
}

function toReportState(result: ClientResult<ReportReceiptResponse>): ReportCommandState {
  if (result.status === 0) {
    return { kind: "error", message: apiUnreachable };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "forbidden", message: messageOf(result, "The report is not allowed for this account.") };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 409) {
    return { kind: "refused", message: messageOf(result, "The report conflicts with an earlier one.") };
  }

  if (result.status === 422) {
    return { kind: "invalid", fieldErrors: fieldErrorsOf(result) };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The report is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "saved", report: result.data };
}

function toDisputeState(result: ClientResult<DisputeViewResponse>): DisputeCommandState {
  if (result.status === 0) {
    return { kind: "error", message: apiUnreachable };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "forbidden", message: messageOf(result, "The command is not allowed for this account.") };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 409) {
    const code = result.error?.error?.code;

    if (code === "version_conflict") {
      return { kind: "conflict", message: messageOf(result, "The case changed somewhere else.") };
    }

    return { kind: "refused", message: messageOf(result, "The state of the case does not accept this command.") };
  }

  if (result.status === 422) {
    return { kind: "invalid", fieldErrors: fieldErrorsOf(result) };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The case is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "saved", dispute: result.data };
}

function messageOf(result: ClientResult<unknown>, fallback: string): string {
  return result.error?.error?.message ?? fallback;
}

function fieldErrorsOf(result: ClientResult<unknown>): Record<string, string[]> {
  return (result.error?.error?.field_errors ?? {}) as Record<string, string[]>;
}
