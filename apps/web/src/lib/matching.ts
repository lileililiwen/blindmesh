import { createClient, type CandidateListResponse, type ClientResult } from "@blindmesh/api-client";

/**
 * The browser reaches the API through the same origin path /api/v1, so the
 * session cookie and the CSRF token stay with the request. The list of the
 * candidates is a private read of the owner of a project, so it uses no-store
 * and nothing stays in the client cache.
 */
const client = createClient({
  baseUrl: "/api/v1",
  cache: "no-store",
  credentials: "same-origin",
});

export type CandidateListState =
  | { kind: "loading" }
  | { kind: "ready"; list: CandidateListResponse }
  | { kind: "unauthorized" }
  | { kind: "forbidden"; message: string }
  | { kind: "not-found" }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

/** Reads the accounts that fit a task of the project of the caller. */
export async function loadCandidates(taskId: string): Promise<CandidateListState> {
  const result = await client.listTaskCandidates(taskId);

  return toCandidateState(result);
}

function toCandidateState(result: ClientResult<CandidateListResponse>): CandidateListState {
  if (result.status === 0) {
    return {
      kind: "error",
      message: "The API did not answer. The candidates are not readable right now.",
    };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return {
      kind: "forbidden",
      message:
        result.error?.error?.message ??
        "Only the members of the project of this task may read its candidates.",
    };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 503) {
    return {
      kind: "unavailable",
      message:
        result.error?.error?.message ?? "The candidates are not available right now.",
    };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "ready", list: result.data };
}
