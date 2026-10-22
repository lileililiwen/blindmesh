import {
  createClient,
  type ClientResult,
  type DeletionRequestListResponse,
  type DeletionRequestResponse,
  type OperationBackupListResponse,
  type OperationBackupResponse,
  type PilotGateResponse,
  type PublicStatusResponse,
  type RecordBackupBody,
  type RestoreRehearsalListResponse,
  type RunRestoreRehearsalBody,
} from "@blindmesh/api-client";

/**
 * The browser reaches the API through the same origin path /api/v1, so the
 * session cookie and the CSRF token stay with the request. Every read of the
 * operations uses no-store, so nothing stays in the client cache.
 */
const client = createClient({
  baseUrl: "/api/v1",
  cache: "no-store",
  credentials: "same-origin",
});

export type OperationsAccess = "loading" | "ready" | "unauthorized" | "forbidden" | "error";

export type OperationBackup = OperationBackupListResponse["items"][number];

export type RestoreRehearsal = RestoreRehearsalListResponse["items"][number];

export type DeletionRequest = DeletionRequestListResponse["items"][number];

export type PilotGate = PilotGateResponse;

export type PilotGateItem = PilotGateResponse["items"][number];

export type PublicStatus = PublicStatusResponse;

export type BackupListState =
  | { kind: "loading" }
  | { kind: "ready"; items: OperationBackup[] }
  | { kind: "unauthorized" }
  | { kind: "forbidden" }
  | { kind: "error"; message: string };

export type RehearsalListState =
  | { kind: "loading" }
  | { kind: "ready"; items: RestoreRehearsal[] }
  | { kind: "unauthorized" }
  | { kind: "forbidden" }
  | { kind: "error"; message: string };

export type DeletionListState =
  | { kind: "loading" }
  | { kind: "ready"; items: DeletionRequest[] }
  | { kind: "unauthorized" }
  | { kind: "forbidden" }
  | { kind: "error"; message: string };

export type GateState =
  | { kind: "loading" }
  | { kind: "ready"; gate: PilotGate }
  | { kind: "unauthorized" }
  | { kind: "forbidden" }
  | { kind: "error"; message: string };

export type RehearsalRunState =
  | { kind: "running" }
  | { kind: "rehearsed"; rehearsal: RestoreRehearsal }
  | { kind: "not-found" }
  | { kind: "error"; message: string };

export type BackupRecordState =
  | { kind: "recorded"; backup: OperationBackupResponse }
  | { kind: "error"; message: string; fieldErrors?: Record<string, string[]> };

export type DeletionRequestState =
  | { kind: "requested"; deletion: DeletionRequestResponse }
  | { kind: "not-found" }
  | { kind: "error"; message: string; fieldErrors?: Record<string, string[]> };

export type PublicStatusState =
  | { kind: "loading" }
  | { kind: "ready"; status: PublicStatus }
  | { kind: "error"; message: string };

/** Reads the backups of the product, the newest first. */
export async function loadOperationBackups(): Promise<BackupListState> {
  const result = await client.listOperationBackups();

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The backups are not readable right now." };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "forbidden" };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: errorMessage(result, "The backups are not readable right now.") };
  }

  return { kind: "ready", items: result.data.items ?? [] };
}

/** Reads the restore rehearsals, the newest first. */
export async function loadRestoreRehearsals(): Promise<RehearsalListState> {
  const result = await client.listRestoreRehearsals();

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The rehearsals are not readable right now." };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "forbidden" };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: errorMessage(result, "The rehearsals are not readable right now.") };
  }

  return { kind: "ready", items: result.data.items ?? [] };
}

/** Reads the deletion requests, the newest first. */
export async function loadDeletionRequests(): Promise<DeletionListState> {
  const result = await client.listDeletionRequests();

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The requests are not readable right now." };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "forbidden" };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: errorMessage(result, "The requests are not readable right now.") };
  }

  return { kind: "ready", items: result.data.items ?? [] };
}

/** Reads the measured gate of the pilot. */
export async function loadPilotGate(): Promise<GateState> {
  const result = await client.getPilotGate();

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The gate is not readable right now." };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "forbidden" };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: errorMessage(result, "The gate is not readable right now.") };
  }

  return { kind: "ready", gate: result.data };
}

/**
 * Runs a restore rehearsal. The answer holds the four verdicts of the
 * rehearsal, so the interface shows the result of the real run.
 */
export async function runRestoreRehearsal(
  body: RunRestoreRehearsalBody,
  csrfToken: string,
): Promise<RehearsalRunState> {
  const result = await client.runRestoreRehearsal(body, csrfToken);

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The rehearsal did not run." };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "rehearsed", rehearsal: result.data };
}

/** Records a backup the operator took. */
export async function recordOperationBackup(
  body: RecordBackupBody,
  csrfToken: string,
): Promise<BackupRecordState> {
  const result = await client.recordOperationBackup(body, csrfToken);

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The backup is not recorded." };
  }

  if (!result.ok || !result.data) {
    return {
      kind: "error",
      message: errorMessage(result, "The backup is not recorded."),
      fieldErrors: fieldErrors(result),
    };
  }

  return { kind: "recorded", backup: result.data };
}

/**
 * Handles a deletion request. The answer holds the pseudonymous reference of
 * the subject and it never holds the removed identity.
 */
export async function createDeletionRequest(
  body: { subject_user_id: string; scope: string },
  csrfToken: string,
): Promise<DeletionRequestState> {
  const result = await client.createDeletionRequest(body, csrfToken);

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The request was not handled." };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (!result.ok || !result.data) {
    return {
      kind: "error",
      message: errorMessage(result, "The request was not handled."),
      fieldErrors: fieldErrors(result),
    };
  }

  return { kind: "requested", deletion: result.data };
}

/** Reads the public status of the product. */
export async function loadPublicStatus(): Promise<PublicStatusState> {
  const result = await client.getPublicStatus();

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The status is not readable right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: errorMessage(result, "The status is not readable right now.") };
  }

  return { kind: "ready", status: result.data };
}

/** The message of the error envelope of the API. */
function errorMessage(result: ClientResult<unknown>, fallback: string): string {
  const envelope = result.error as { error?: { message?: string } } | undefined;
  const message = envelope?.error?.message;

  return typeof message === "string" && message.length > 0 ? message : fallback;
}

/** The field errors of the error envelope of the API. */
function fieldErrors(result: ClientResult<unknown>): Record<string, string[]> | undefined {
  const envelope = result.error as { error?: { field_errors?: Record<string, string[]> } } | undefined;
  const errors = envelope?.error?.field_errors;

  return errors && Object.keys(errors).length > 0 ? errors : undefined;
}
