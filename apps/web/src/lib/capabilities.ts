import {
  createClient,
  type CapabilityEligibilityResponse,
  type CapabilityListResponse,
  type CapabilityViewResponse,
  type ClientResult,
  type CreateCapabilityBody,
  type EvaluateEligibilityBody,
  type UpdateCapabilityBody,
  type WithdrawCapabilityBody,
} from "@blindmesh/api-client";

/**
 * The browser reaches the API through the same origin path /api/v1, so the
 * session cookie and the CSRF token stay with the request. Every read of a
 * capability uses no-store, so nothing private stays in the client cache.
 */
const client = createClient({
  baseUrl: "/api/v1",
  cache: "no-store",
  credentials: "same-origin",
});

export type CapabilityListState =
  | { kind: "loading" }
  | { kind: "ready"; capabilities: CapabilityViewResponse[]; nextCursor: string | null }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type CapabilityCommandState =
  | { kind: "idle" }
  | { kind: "saving" }
  | { kind: "saved"; capability: CapabilityViewResponse }
  | { kind: "invalid"; fieldErrors: Record<string, string[]> }
  | { kind: "conflict"; message: string }
  | { kind: "refused"; message: string }
  | { kind: "forbidden"; message: string }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type EligibilityState =
  | { kind: "idle" }
  | { kind: "evaluating" }
  | { kind: "evaluated"; evaluation: CapabilityEligibilityResponse }
  | { kind: "invalid"; fieldErrors: Record<string, string[]> }
  | { kind: "not-found" }
  | { kind: "unauthorized" }
  | { kind: "error"; message: string };

const apiUnreachable = "The API did not answer. Nothing was changed.";

/** Lists the capabilities the caller may see in the directory. */
export async function loadCapabilities(limit = 20, cursor?: string): Promise<CapabilityListState> {
  const result = await client.listCapabilities(limit, cursor);

  return toListState(result);
}

/** Lists the own declarations of the contributor, including the withdrawn ones. */
export async function loadOwnCapabilities(limit = 20, cursor?: string): Promise<CapabilityListState> {
  const result = await client.listCapabilities(limit, cursor, "own");

  return toListState(result);
}

/** Declares a capability. */
export async function createCapability(
  body: CreateCapabilityBody,
  csrfToken: string,
  idempotencyKey?: string,
): Promise<CapabilityCommandState> {
  const result = await client.createCapability(body, csrfToken, idempotencyKey);

  return toCommandState(result);
}

/** Saves the content or the visibility of a capability. */
export async function updateCapability(
  id: string,
  body: UpdateCapabilityBody,
  csrfToken: string,
): Promise<CapabilityCommandState> {
  const result = await client.updateCapability(id, body, csrfToken);

  return toCommandState(result);
}

/** Withdraws a capability. */
export async function withdrawCapability(
  id: string,
  body: WithdrawCapabilityBody,
  csrfToken: string,
): Promise<CapabilityCommandState> {
  const result = await client.withdrawCapability(id, body, csrfToken);

  return toCommandState(result);
}

/** Evaluates a set of hard conditions against the declarations of the caller. */
export async function evaluateEligibility(
  body: EvaluateEligibilityBody,
  csrfToken: string,
): Promise<EligibilityState> {
  const result = await client.evaluateEligibility(body, csrfToken);

  return toEligibilityState(result);
}

/** Evaluates the hard conditions of a published task against the declarations of the caller. */
export async function evaluateTaskEligibility(
  taskId: string,
  csrfToken: string,
): Promise<EligibilityState> {
  const result = await client.evaluateTaskEligibility(taskId, csrfToken);

  return toEligibilityState(result);
}

function toListState(result: ClientResult<CapabilityListResponse>): CapabilityListState {
  if (result.status === 0) {
    return {
      kind: "error",
      message: "The API did not answer. The capabilities are not readable right now.",
    };
  }

  if (result.status === 401) {
    return { kind: "error", message: "Sign in again to read the capabilities." };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The capabilities are not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return {
    kind: "ready",
    capabilities: result.data.items ?? [],
    nextCursor: result.data.next_cursor ?? null,
  };
}

function toCommandState(result: ClientResult<CapabilityViewResponse>): CapabilityCommandState {
  if (result.status === 0) {
    return { kind: "error", message: apiUnreachable };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return {
      kind: "forbidden",
      message: result.error?.error?.message ?? "This change is not allowed.",
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
        "The capability was changed somewhere else. Load the current capability before saving again.",
    };
  }

  if (result.status === 422) {
    return {
      kind: "invalid",
      fieldErrors: (result.error?.error?.field_errors ?? {}) as Record<string, string[]>,
    };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The capability is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "saved", capability: result.data };
}

function toEligibilityState(result: ClientResult<CapabilityEligibilityResponse>): EligibilityState {
  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The eligibility is not known right now." };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 422) {
    return {
      kind: "invalid",
      fieldErrors: (result.error?.error?.field_errors ?? {}) as Record<string, string[]>,
    };
  }

  if (result.status === 503) {
    return { kind: "error", message: "The eligibility is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "evaluated", evaluation: result.data };
}
