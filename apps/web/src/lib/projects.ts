import {
  createClient,
  type AddProjectMemberBody,
  type AddProjectVersionBody,
  type ClientResult,
  type CreateProjectBody,
  type ProjectDetailResponse,
  type ProjectListResponse,
  type ProjectSummaryResponse,
  type TransitionProjectBody,
  type UpdateProjectBody,
} from "@blindmesh/api-client";

/**
 * The browser reaches the API through the same origin path /api/v1, so the
 * session cookie and the CSRF token stay with the request. Every read of a
 * project uses no-store, so nothing private stays in the client cache.
 */
const client = createClient({
  baseUrl: "/api/v1",
  cache: "no-store",
  credentials: "same-origin",
});

export type ProjectListState =
  | { kind: "loading" }
  | { kind: "ready"; projects: ProjectSummaryResponse[]; nextCursor: string | null }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type ProjectDetailState =
  | { kind: "loading" }
  | { kind: "ready"; project: ProjectDetailResponse }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "forbidden"; message: string }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type ProjectCommandState =
  | { kind: "idle" }
  | { kind: "saving" }
  | { kind: "saved"; project: ProjectDetailResponse }
  | { kind: "invalid"; fieldErrors: Record<string, string[]> }
  | { kind: "conflict"; message: string }
  | { kind: "refused"; message: string }
  | { kind: "forbidden"; message: string }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

const apiUnreachable = "The API did not answer. Nothing was changed.";

/** Lists the projects the caller may see. */
export async function loadProjects(limit = 20, cursor?: string): Promise<ProjectListState> {
  const result = await client.listProjects(limit, cursor);

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The projects are not readable right now." };
  }

  if (result.status === 401) {
    return { kind: "error", message: "The session of this visitor is not valid any more." };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The projects are not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  const page: ProjectListResponse = result.data;

  return {
    kind: "ready",
    projects: page.items ?? [],
    nextCursor: page.next_cursor ?? null,
  };
}

/** Reads the committed state of a project. */
export async function loadProject(id: string): Promise<ProjectDetailState> {
  const result = await client.getProject(id);

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The project is not readable right now." };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "forbidden", message: "You are not allowed to read this project." };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The project is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "ready", project: result.data };
}

/** Creates a project in the draft state. */
export async function createProject(
  body: CreateProjectBody,
  csrfToken: string,
  idempotencyKey?: string,
): Promise<ProjectCommandState> {
  const result = await client.createProject(body, csrfToken, idempotencyKey);

  return toCommandState(result);
}

/** Saves the content or the visibility of a project. */
export async function updateProject(
  id: string,
  body: UpdateProjectBody,
  csrfToken: string,
): Promise<ProjectCommandState> {
  const result = await client.updateProject(id, body, csrfToken);

  return toCommandState(result);
}

/** Applies a transition of the lifecycle of a project. */
export async function transitionProject(
  id: string,
  body: TransitionProjectBody,
  csrfToken: string,
): Promise<ProjectCommandState> {
  const result = await client.transitionProject(id, body, csrfToken);

  return toCommandState(result);
}

/** Adds a member to a project or changes the role of a member. */
export async function addProjectMember(
  id: string,
  userReference: string,
  body: AddProjectMemberBody,
  csrfToken: string,
): Promise<ProjectCommandState> {
  const result = await client.addProjectMember(id, userReference, body, csrfToken);

  return toCommandState(result);
}

/** Removes a member of a project. */
export async function removeProjectMember(
  id: string,
  userReference: string,
  csrfToken: string,
): Promise<ProjectCommandState> {
  const result = await client.removeProjectMember(id, userReference, csrfToken);

  if (result.status === 0) {
    return { kind: "error", message: apiUnreachable };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "forbidden", message: "You are not allowed to manage the members of this project." };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 409) {
    return { kind: "conflict", message: messageOf(result) };
  }

  if (result.status === 422) {
    return { kind: "invalid", fieldErrors: fieldErrorsOf(result) };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The project is not available right now." };
  }

  if (!result.ok) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "idle" };
}

/** Records a released version of a project. */
export async function addProjectVersion(
  id: string,
  body: AddProjectVersionBody,
  csrfToken: string,
): Promise<ProjectCommandState> {
  const result = await client.addProjectVersion(id, body, csrfToken);

  return toCommandState(result);
}

function toCommandState(result: ClientResult<ProjectDetailResponse>): ProjectCommandState {
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
    return { kind: "invalid", fieldErrors: fieldErrorsOf(result) };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The project is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "saved", project: result.data };
}

function messageOf(result: ClientResult<ProjectDetailResponse>): string {
  return (
    result.error?.error?.message ??
    "The project was changed somewhere else. Load the current project before saving again."
  );
}

function fieldErrorsOf(result: ClientResult<ProjectDetailResponse>): Record<string, string[]> {
  return (result.error?.error?.field_errors ?? {}) as Record<string, string[]>;
}
