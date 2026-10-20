import {
  createClient,
  type AcceptSubmissionBody,
  type AttachmentDownloadResponse,
  type AttachmentViewResponse,
  type ClientResult,
  type DisputeSubmissionBody,
  type FindingDetailResponse,
  type PatchFindingBody,
  type RequestChangesBody,
  type SubmissionDetailResponse,
  type SubmissionSummaryResponse,
  type SubmitEvidenceBody,
} from "@blindmesh/api-client";

/**
 * The browser reaches the API through the same origin path /api/v1, so the
 * session cookie and the CSRF token stay with the request. Every read of the
 * evidence uses no-store, so nothing private stays in the client cache.
 */
const client = createClient({
  baseUrl: "/api/v1",
  cache: "no-store",
  credentials: "same-origin",
});

export type SubmissionDetailState =
  | { kind: "loading" }
  | { kind: "ready"; submission: SubmissionDetailResponse }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "forbidden"; message: string }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type ReviewsState =
  | { kind: "loading" }
  | { kind: "ready"; reviews: SubmissionSummaryResponse[] }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "forbidden"; message: string }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type SubmissionCommandState =
  | { kind: "idle" }
  | { kind: "saving" }
  | { kind: "saved"; submission: SubmissionDetailResponse }
  | { kind: "invalid"; fieldErrors: Record<string, string[]> }
  | { kind: "conflict"; message: string }
  | { kind: "refused"; message: string }
  | { kind: "forbidden"; message: string }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type FindingState =
  | { kind: "loading" }
  | { kind: "ready"; finding: FindingDetailResponse }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "forbidden"; message: string }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type FindingCommandState =
  | { kind: "idle" }
  | { kind: "saving" }
  | { kind: "saved"; finding: FindingDetailResponse }
  | { kind: "invalid"; fieldErrors: Record<string, string[]> }
  | { kind: "conflict"; message: string }
  | { kind: "refused"; message: string }
  | { kind: "forbidden"; message: string }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type AttachmentCommandState =
  | { kind: "idle" }
  | { kind: "saving" }
  | { kind: "saved"; attachment: AttachmentViewResponse }
  | { kind: "invalid"; fieldErrors: Record<string, string[]> }
  | { kind: "refused"; message: string }
  | { kind: "forbidden"; message: string }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type DownloadState =
  | { kind: "idle" }
  | { kind: "loading" }
  | { kind: "ready"; download: AttachmentDownloadResponse }
  | { kind: "refused"; message: string }
  | { kind: "forbidden"; message: string }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

const apiUnreachable = "The API did not answer. Nothing was changed.";

/** Reads the committed state of the evidence of a seat. */
export async function loadSubmission(id: string): Promise<SubmissionDetailState> {
  const result = await client.getSubmission(id);

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The evidence is not readable right now." };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "forbidden", message: "The evidence is not readable." };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The evidence is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "ready", submission: result.data };
}

/** Lists the evidence of a project that waits for the owner. */
export async function loadProjectReviews(projectId: string): Promise<ReviewsState> {
  const result = await client.listProjectReviews(projectId);

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The reviews are not readable right now." };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "forbidden", message: "The reviews of this project are not readable." };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The reviews are not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "ready", reviews: result.data };
}

/** Sends the protected evidence of a seat. */
export async function submitEvidence(
  participationId: string,
  body: SubmitEvidenceBody,
  csrfToken: string,
  idempotencyKey: string,
): Promise<SubmissionCommandState> {
  return toSubmissionState(await client.submitEvidence(participationId, body, csrfToken, idempotencyKey));
}

/** Accepts the work of a seat. */
export async function acceptSubmission(
  id: string,
  body: AcceptSubmissionBody,
  csrfToken: string,
  idempotencyKey: string,
): Promise<SubmissionCommandState> {
  return toSubmissionState(await client.acceptSubmission(id, body, csrfToken, idempotencyKey));
}

/** Asks the contributor for the one correction of the evidence. */
export async function requestSubmissionChanges(
  id: string,
  body: RequestChangesBody,
  csrfToken: string,
  idempotencyKey: string,
): Promise<SubmissionCommandState> {
  return toSubmissionState(await client.requestSubmissionChanges(id, body, csrfToken, idempotencyKey));
}

/** Opens a dispute about the completion of a seat. */
export async function disputeSubmission(
  id: string,
  body: DisputeSubmissionBody,
  csrfToken: string,
  idempotencyKey: string,
): Promise<SubmissionCommandState> {
  return toSubmissionState(await client.disputeSubmission(id, body, csrfToken, idempotencyKey));
}

/** Reads one finding of the evidence with its history. */
export async function loadFinding(id: string): Promise<FindingState> {
  const result = await client.getFinding(id);

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The finding is not readable right now." };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "forbidden", message: "The finding is not readable." };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The finding is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "ready", finding: result.data };
}

/** Classifies one finding of the evidence. */
export async function patchFinding(
  id: string,
  body: PatchFindingBody,
  csrfToken: string,
  idempotencyKey: string,
): Promise<FindingCommandState> {
  return toFindingState(await client.patchFinding(id, body, csrfToken, idempotencyKey));
}

/** Declares a private file of the evidence. */
export async function createAttachmentIntent(
  body: { submission_id: string; file_name: string; content_type: string; size_bytes: number },
  csrfToken: string,
  idempotencyKey: string,
): Promise<AttachmentCommandState> {
  return toAttachmentState(await client.createAttachmentIntent(body, csrfToken, idempotencyKey));
}

/** Uploads the bytes of a quarantined file and releases it. */
export async function completeAttachment(
  attachmentId: string,
  file: File,
  csrfToken: string,
  idempotencyKey: string,
): Promise<AttachmentCommandState> {
  return toAttachmentState(await client.completeAttachment(attachmentId, file, csrfToken, idempotencyKey));
}

/** Signs the short address of a private file of the evidence. */
export async function downloadAttachment(id: string): Promise<DownloadState> {
  const result = await client.downloadAttachment(id);

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The file is not readable right now." };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "forbidden", message: "The file is not readable." };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 409) {
    return { kind: "refused", message: messageOf(result, "The file is not released yet.") };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The private file storage is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "ready", download: result.data };
}

/** A key for a command of the evidence. It holds no secret, it only names the command. */
export function newEvidenceKey(): string {
  return crypto.randomUUID();
}

function toSubmissionState(result: ClientResult<SubmissionDetailResponse>): SubmissionCommandState {
  if (result.status === 0) {
    return { kind: "error", message: apiUnreachable };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "forbidden", message: messageOf(result, "The command is not allowed for this contributor.") };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 409) {
    const code = result.error?.error?.code;

    if (code === "version_conflict") {
      return { kind: "conflict", message: messageOf(result, "The evidence was changed somewhere else.") };
    }

    return { kind: "refused", message: messageOf(result, "The state of the evidence does not accept this command.") };
  }

  if (result.status === 413) {
    return { kind: "refused", message: messageOf(result, "The file is larger than the allowed size.") };
  }

  if (result.status === 422) {
    return { kind: "invalid", fieldErrors: fieldErrorsOf(result) };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The evidence is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "saved", submission: result.data };
}

function toFindingState(result: ClientResult<FindingDetailResponse>): FindingCommandState {
  if (result.status === 0) {
    return { kind: "error", message: apiUnreachable };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "forbidden", message: messageOf(result, "The command is not allowed for this contributor.") };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 409) {
    const code = result.error?.error?.code;

    if (code === "version_conflict") {
      return { kind: "conflict", message: messageOf(result, "The finding was changed somewhere else.") };
    }

    return { kind: "refused", message: messageOf(result, "The finding does not accept this state.") };
  }

  if (result.status === 422) {
    return { kind: "invalid", fieldErrors: fieldErrorsOf(result) };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The finding is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "saved", finding: result.data };
}

function toAttachmentState(result: ClientResult<AttachmentViewResponse>): AttachmentCommandState {
  if (result.status === 0) {
    return { kind: "error", message: apiUnreachable };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "forbidden", message: messageOf(result, "The file is not yours.") };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 409) {
    return { kind: "refused", message: messageOf(result, "The file was already released or refused.") };
  }

  if (result.status === 413) {
    return { kind: "refused", message: messageOf(result, "The file is larger than the allowed size.") };
  }

  if (result.status === 422) {
    return { kind: "invalid", fieldErrors: fieldErrorsOf(result) };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The private file storage is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "saved", attachment: result.data };
}

function messageOf(result: ClientResult<unknown>, fallback: string): string {
  return result.error?.error?.message ?? fallback;
}

function fieldErrorsOf(result: ClientResult<unknown>): Record<string, string[]> {
  return (result.error?.error?.field_errors ?? {}) as Record<string, string[]>;
}
