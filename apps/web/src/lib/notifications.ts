import {
  createClient,
  type ClientResult,
  type NotificationListResponse,
  type NotificationPreferencesResponse,
  type NotificationResponse,
  type UpdateNotificationPreferencesBody,
} from "@blindmesh/api-client";

/**
 * The browser reaches the API through the same origin path /api/v1, so the
 * session cookie and the CSRF token stay with the request. Every read of a
 * notification uses no-store, so nothing stays in the client cache.
 */
const client = createClient({
  baseUrl: "/api/v1",
  cache: "no-store",
  credentials: "same-origin",
});

export type NotificationListState =
  | { kind: "loading" }
  | { kind: "ready"; items: NotificationListResponse["items"]; nextCursor: string | null }
  | { kind: "unauthorized" }
  | { kind: "error"; message: string };

export type NotificationPreferencesState =
  | { kind: "loading" }
  | { kind: "ready"; preferences: NotificationPreferencesResponse }
  | { kind: "unauthorized" }
  | { kind: "error"; message: string };

export type NotificationReadState =
  | { kind: "read"; notification: NotificationResponse }
  | { kind: "not-found" }
  | { kind: "error"; message: string };

export type NotificationPreferenceSaveState =
  | { kind: "saved"; preferences: NotificationPreferencesResponse }
  | { kind: "error"; message: string };

/** Reads the notifications of the signed in contributor, the newest first. */
export async function loadNotifications(
  cursor?: string,
): Promise<NotificationListState> {
  const result = await client.listNotifications(undefined, cursor);

  return toListState(result);
}

/** Marks one notification of the signed in contributor as read. */
export async function markNotificationRead(
  id: string,
  csrfToken: string,
): Promise<NotificationReadState> {
  const result = await client.markNotificationRead(id, csrfToken);

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The notification is not readable right now." };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "read", notification: result.data };
}

/** Reads the notification preferences of the signed in contributor. */
export async function loadNotificationPreferences(): Promise<NotificationPreferencesState> {
  const result = await client.getNotificationPreferences();

  return toPreferencesState(result);
}

/**
 * Saves the notification preferences of the signed in contributor. The answer
 * holds the committed state, so the interface announces the saved state only
 * after the server confirmed it.
 */
export async function saveNotificationPreferences(
  body: UpdateNotificationPreferencesBody,
  csrfToken: string,
): Promise<NotificationPreferenceSaveState> {
  const result = await client.updateNotificationPreferences(body, csrfToken);

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The preferences are not saved right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "saved", preferences: result.data };
}

function toListState(result: ClientResult<NotificationListResponse>): NotificationListState {
  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The notifications are not readable right now." };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return {
    kind: "ready",
    items: result.data.items ?? [],
    nextCursor: result.data.next_cursor ?? null,
  };
}

function toPreferencesState(
  result: ClientResult<NotificationPreferencesResponse>,
): NotificationPreferencesState {
  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The preferences are not readable right now." };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "ready", preferences: result.data };
}
