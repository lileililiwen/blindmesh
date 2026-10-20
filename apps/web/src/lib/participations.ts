import {
  createClient,
  type ApplyForSlotBody,
  type ClientResult,
  type ParticipationDetailResponse,
  type ParticipationListResponse,
} from "@blindmesh/api-client";

/**
 * The browser reaches the API through the same origin path /api/v1, so the
 * session cookie and the CSRF token stay with the request. Every read of a
 * seat uses no-store, so nothing private stays in the client cache.
 */
const client = createClient({
  baseUrl: "/api/v1",
  cache: "no-store",
  credentials: "same-origin",
});

export type WorkListState =
  | { kind: "loading" }
  | { kind: "ready"; items: ParticipationListResponse["items"]; nextCursor: string | null }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type ApplicationsState =
  | { kind: "loading" }
  | { kind: "ready"; items: ParticipationListResponse["items"]; nextCursor: string | null }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "forbidden"; message: string }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type ParticipationDetailState =
  | { kind: "loading" }
  | { kind: "ready"; participation: ParticipationDetailResponse }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type ParticipationCommandState =
  | { kind: "idle" }
  | { kind: "saving" }
  | { kind: "saved"; participation: ParticipationDetailResponse }
  | { kind: "invalid"; fieldErrors: Record<string, string[]> }
  | { kind: "conflict"; message: string }
  | { kind: "refused"; message: string }
  | { kind: "forbidden"; message: string }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

const apiUnreachable = "The API did not answer. Nothing was changed.";

/** Lists the work of the signed in contributor. */
export async function loadWork(limit = 20, cursor?: string): Promise<WorkListState> {
  const result = await client.listWork(limit, cursor);

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The work is not readable right now." };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The work is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  const page: ParticipationListResponse = result.data;

  return {
    kind: "ready",
    items: page.items ?? [],
    nextCursor: page.next_cursor ?? null,
  };
}

/** Lists the applications of a task of the owner. */
export async function loadTaskApplications(
  taskId: string,
  limit = 20,
  cursor?: string,
): Promise<ApplicationsState> {
  const result = await client.listTaskApplications(taskId, limit, cursor);

  if (result.status === 0) {
    return {
      kind: "error",
      message: "The API did not answer. The applications are not readable right now.",
    };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "forbidden", message: "The applications of this task are not readable." };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The applications are not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  const page: ParticipationListResponse = result.data;

  return {
    kind: "ready",
    items: page.items ?? [],
    nextCursor: page.next_cursor ?? null,
  };
}

/** Reads the committed state of one seat. */
export async function loadParticipation(id: string): Promise<ParticipationDetailState> {
  const result = await client.getParticipation(id);

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The seat is not readable right now." };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The seat is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "ready", participation: result.data };
}

/** Applies for one slot of a published task. */
export async function applyForSlot(
  taskId: string,
  body: ApplyForSlotBody,
  csrfToken: string,
  idempotencyKey: string,
): Promise<ParticipationCommandState> {
  return toCommandState(await client.applyForSlot(taskId, body, csrfToken, idempotencyKey));
}

/** Accepts an application and leases one slot of the task. */
export async function acceptParticipation(
  id: string,
  csrfToken: string,
  idempotencyKey: string,
): Promise<ParticipationCommandState> {
  return toCommandState(await client.acceptParticipation(id, csrfToken, idempotencyKey));
}

/** Declines an application of a task. */
export async function declineParticipation(
  id: string,
  csrfToken: string,
  idempotencyKey: string,
): Promise<ParticipationCommandState> {
  return toCommandState(await client.declineParticipation(id, csrfToken, idempotencyKey));
}

/** Starts the work of an accepted seat. */
export async function startParticipation(
  id: string,
  csrfToken: string,
  idempotencyKey: string,
): Promise<ParticipationCommandState> {
  return toCommandState(await client.startParticipation(id, csrfToken, idempotencyKey));
}

/** Withdraws a seat before the work starts. */
export async function withdrawParticipation(
  id: string,
  csrfToken: string,
  idempotencyKey: string,
): Promise<ParticipationCommandState> {
  return toCommandState(await client.withdrawParticipation(id, csrfToken, idempotencyKey));
}

/** A key for a money command. It holds no secret, it only names the command. */
export function newSeatKey(): string {
  return crypto.randomUUID();
}

function toCommandState(
  result: ClientResult<ParticipationDetailResponse>,
): ParticipationCommandState {
  if (result.status === 0) {
    return { kind: "error", message: apiUnreachable };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "forbidden", message: messageOf(result) };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 409) {
    const code = result.error?.error?.code;

    if (code === "version_conflict") {
      return { kind: "conflict", message: messageOf(result) };
    }

    return { kind: "refused", message: messageOf(result) };
  }

  if (result.status === 422) {
    const code = result.error?.error?.code;

    if (code === "insufficient_funds") {
      return { kind: "refused", message: messageOf(result) };
    }

    return { kind: "invalid", fieldErrors: fieldErrorsOf(result) };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The seat is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "saved", participation: result.data };
}

function messageOf(result: ClientResult<ParticipationDetailResponse>): string {
  return (
    result.error?.error?.message ??
    "The seat was changed somewhere else. Load the current seat before trying again."
  );
}

function fieldErrorsOf(
  result: ClientResult<ParticipationDetailResponse>,
): Record<string, string[]> {
  return (result.error?.error?.field_errors ?? {}) as Record<string, string[]>;
}
