import {
  createClient,
  type ClientResult,
  type TaskBody,
  type TaskDetailResponse,
  type TaskListResponse,
  type TaskSummaryResponse,
} from "@blindmesh/api-client";

/**
 * The browser reaches the API through the same origin path /api/v1, so the
 * session cookie and the CSRF token stay with the request. Every read of a
 * task uses no-store, so nothing private stays in the client cache.
 */
const client = createClient({
  baseUrl: "/api/v1",
  cache: "no-store",
  credentials: "same-origin",
});

export type TaskListState =
  | { kind: "loading" }
  | { kind: "ready"; tasks: TaskSummaryResponse[]; nextCursor: string | null }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type TaskDetailState =
  | { kind: "loading" }
  | { kind: "ready"; task: TaskDetailResponse }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "forbidden"; message: string }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type TaskCommandState =
  | { kind: "idle" }
  | { kind: "saving" }
  | { kind: "saved"; task: TaskDetailResponse }
  | { kind: "invalid"; fieldErrors: Record<string, string[]> }
  | { kind: "conflict"; message: string }
  | { kind: "refused"; message: string }
  | { kind: "forbidden"; message: string }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

const apiUnreachable = "The API did not answer. Nothing was changed.";

/** The filters of the list of the marketplace. */
export type TaskFilters = {
  mode?: string;
  requirement?: string;
  projectId?: string;
};

/** Lists the tasks the caller may see. */
export async function loadTasks(
  filters: TaskFilters = {},
  limit = 20,
  cursor?: string,
): Promise<TaskListState> {
  const result = await client.listTasks({ ...filters, limit, cursor });

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The tasks are not readable right now." };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The tasks are not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  const page: TaskListResponse = result.data;

  return {
    kind: "ready",
    tasks: page.items ?? [],
    nextCursor: page.next_cursor ?? null,
  };
}

/** Reads the committed state of a task. */
export async function loadTask(id: string): Promise<TaskDetailState> {
  const result = await client.getTask(id);

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The task is not readable right now." };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "forbidden", message: "You are not allowed to read this task." };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The task is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "ready", task: result.data };
}

/** Writes a task in the draft state. */
export async function createTask(
  body: TaskBody,
  csrfToken: string,
  idempotencyKey?: string,
): Promise<TaskCommandState> {
  return toCommandState(await client.createTask(body, csrfToken, idempotencyKey));
}

/** Changes the editable content of a task. */
export async function updateTask(
  id: string,
  body: TaskBody,
  csrfToken: string,
): Promise<TaskCommandState> {
  return toCommandState(await client.updateTask(id, body, csrfToken));
}

/** Publishes a task and reserves its maximum budget. */
export async function publishTask(
  id: string,
  csrfToken: string,
  idempotencyKey: string,
): Promise<TaskCommandState> {
  return toCommandState(await client.publishTask(id, csrfToken, idempotencyKey));
}

/** Closes the admission of a task and refunds its unused slots. */
export async function cancelTask(
  id: string,
  csrfToken: string,
  idempotencyKey: string,
): Promise<TaskCommandState> {
  return toCommandState(await client.cancelTask(id, csrfToken, idempotencyKey));
}

function toCommandState(result: ClientResult<TaskDetailResponse>): TaskCommandState {
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
    return { kind: "unavailable", message: "The task is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "saved", task: result.data };
}

function messageOf(result: ClientResult<TaskDetailResponse>): string {
  return (
    result.error?.error?.message ??
    "The task was changed somewhere else. Load the current task before saving again."
  );
}

function fieldErrorsOf(result: ClientResult<TaskDetailResponse>): Record<string, string[]> {
  return (result.error?.error?.field_errors ?? {}) as Record<string, string[]>;
}
