import {
  createClient,
  type ProfileResponse,
  type PublicProfileResponse,
  type SessionResponse,
  type UpdateProfileBody,
} from "@blindmesh/api-client";

/**
 * The browser reaches the API through the same origin path /api/v1, so the
 * session cookie and the CSRF token stay with the request. Every read of a
 * private value uses no-store, so nothing private stays in the client cache.
 */
const client = createClient({
  baseUrl: "/api/v1",
  cache: "no-store",
  credentials: "same-origin",
});

export type SessionUser = NonNullable<SessionResponse["user"]>;

export type SessionState =
  | { kind: "loading" }
  | { kind: "anonymous"; csrfToken: string }
  | { kind: "authenticated"; csrfToken: string; user: SessionUser }
  | { kind: "forbidden"; message: string }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type ProfileState =
  | { kind: "loading" }
  | { kind: "ready"; profile: ProfileResponse }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "forbidden"; message: string }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type PublicProfileState =
  | { kind: "loading" }
  | { kind: "ready"; profile: PublicProfileResponse }
  | { kind: "not-found" }
  | { kind: "unavailable"; message: string }
  | { kind: "error"; message: string };

export type SaveProfileState =
  | { kind: "idle" }
  | { kind: "saving" }
  | { kind: "saved"; version: number }
  | { kind: "invalid"; fieldErrors: Record<string, string[]> }
  | { kind: "conflict"; message: string }
  | { kind: "unauthorized" }
  | { kind: "error"; message: string };

export type SignInState =
  | { kind: "idle" }
  | { kind: "starting" }
  | { kind: "ready"; authorizeUrl: string }
  | { kind: "unavailable"; message: string }
  | { kind: "forbidden"; message: string }
  | { kind: "error"; message: string };

const apiUnreachable = "The API did not answer. The sign in is not possible right now.";

/** Reads the session of the visitor and the CSRF token bound to it. */
export async function loadSession(): Promise<SessionState> {
  const result = await client.getSession();

  if (result.status === 0) {
    return { kind: "error", message: apiUnreachable };
  }

  if (result.status === 401) {
    return {
      kind: "forbidden",
      message: "This account is not allowed to use Blindmesh. The session was closed.",
    };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The sign in is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  const session = result.data;

  if (session.user) {
    return { kind: "authenticated", csrfToken: session.csrf_token, user: session.user };
  }

  return { kind: "anonymous", csrfToken: session.csrf_token };
}

/** Removes the server session. */
export async function signOut(csrfToken: string): Promise<boolean> {
  const result = await client.logout(csrfToken);

  return result.status === 204;
}

/** Starts the GitHub sign in and returns the address of the provider. */
export async function startSignIn(
  csrfToken: string,
  returnPath?: string,
  actor?: string,
): Promise<SignInState> {
  const result = await client.startSignIn(
    returnPath ? { return_path: returnPath } : undefined,
    actor,
    csrfToken,
  );

  if (result.status === 0) {
    return { kind: "error", message: apiUnreachable };
  }

  if (result.status === 401) {
    return { kind: "forbidden", message: "The session of this visitor is not valid any more." };
  }

  if (result.status === 403) {
    return { kind: "forbidden", message: "This request is not allowed." };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The sign in provider is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "ready", authorizeUrl: result.data.authorize_url };
}

/** Reads the private profile of the signed in contributor. */
export async function loadProfile(): Promise<ProfileState> {
  const result = await client.getProfile();

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The profile is not readable right now." };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "forbidden", message: "You are not allowed to read this profile." };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The profile is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "ready", profile: result.data };
}

/** Reads the public profile of a contributor. */
export async function loadPublicProfile(username: string): Promise<PublicProfileState> {
  const result = await client.getPublicProfile(username);

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. The profile is not readable right now." };
  }

  if (result.status === 404) {
    return { kind: "not-found" };
  }

  if (result.status === 503) {
    return { kind: "unavailable", message: "The profile is not available right now." };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "ready", profile: result.data };
}

/** Saves the profile of the signed in contributor. */
export async function saveProfile(
  body: UpdateProfileBody,
  csrfToken: string,
): Promise<SaveProfileState> {
  const result = await client.updateProfile(body, csrfToken);

  if (result.status === 0) {
    return { kind: "error", message: "The API did not answer. Nothing was saved." };
  }

  if (result.status === 401) {
    return { kind: "unauthorized" };
  }

  if (result.status === 403) {
    return { kind: "error", message: "You are not allowed to change this profile." };
  }

  if (result.status === 409) {
    return {
      kind: "conflict",
      message:
        result.error?.error?.message ??
        "The profile was changed somewhere else. Load the current profile before saving again.",
    };
  }

  if (result.status === 422) {
    return {
      kind: "invalid",
      fieldErrors: (result.error?.error?.field_errors ?? {}) as Record<string, string[]>,
    };
  }

  if (!result.ok || !result.data) {
    return { kind: "error", message: `The API answered with status ${result.status}.` };
  }

  return { kind: "saved", version: Number(result.data.version) };
}
