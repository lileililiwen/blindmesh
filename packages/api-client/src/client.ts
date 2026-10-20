import type { paths } from "./generated/schema";

export type HealthLiveResponse = paths["/v1/health/live"]["get"]["responses"][200]["content"]["application/json"];

export type HealthReadyResponse = paths["/v1/health/ready"]["get"]["responses"][200]["content"]["application/json"];

export type RuntimeHeartbeatResponse =
  paths["/v1/runtime/heartbeat"]["post"]["responses"][201]["content"]["application/json"];

export type ApiErrorResponse = paths["/v1/runtime/heartbeat"]["post"]["responses"][400]["content"]["application/json"];

export type SessionResponse = paths["/v1/session"]["get"]["responses"][200]["content"]["application/json"];

export type SessionUserResponse = paths["/v1/session"]["get"]["responses"][200]["content"]["application/json"]["user"];

export type SignInStartResponse =
  paths["/v1/auth/github/start"]["post"]["responses"][200]["content"]["application/json"];

export type ProfileResponse = paths["/v1/profile"]["get"]["responses"][200]["content"]["application/json"];

export type PublicProfileResponse =
  paths["/v1/people/{username}"]["get"]["responses"][200]["content"]["application/json"];

/** The outcome of one API call. The status code is always the real one. */
export type ClientResult<T> = {
  status: number;
  ok: boolean;
  data?: T;
  error?: ApiErrorResponse;
};

/** The profile fields a contributor may change. */
export type UpdateProfileBody = {
  expected_version: number;
  username?: string;
  display_name?: string;
  bio?: string;
  timezone?: string;
  skills?: string[];
  languages?: string[];
  is_public?: boolean;
};

export type BlindmeshClient = {
  healthLive(): Promise<ClientResult<HealthLiveResponse>>;
  healthReady(): Promise<ClientResult<HealthReadyResponse>>;
  runtimeHeartbeat(idempotencyKey: string, body?: { note?: string }): Promise<ClientResult<RuntimeHeartbeatResponse>>;
  /** Reads the session of the caller and the CSRF token bound to it. */
  getSession(): Promise<ClientResult<SessionResponse>>;
  /** Starts a GitHub sign in. The actor is only used by the local test fixture. */
  startSignIn(
    body?: { return_path?: string },
    actor?: string,
    csrfToken?: string,
  ): Promise<ClientResult<SignInStartResponse>>;
  /** Removes the server session of the caller. */
  logout(csrfToken: string): Promise<ClientResult<never>>;
  /** Reads the private profile of the signed in contributor. */
  getProfile(): Promise<ClientResult<ProfileResponse>>;
  /** Saves the profile of the signed in contributor. */
  updateProfile(body: UpdateProfileBody, csrfToken: string): Promise<ClientResult<ProfileResponse>>;
  /** Reads the public profile of a contributor. */
  getPublicProfile(username: string): Promise<ClientResult<PublicProfileResponse>>;
};

export type BlindmeshClientOptions = {
  /** Absolute API base, for example http://127.0.0.1:8080/v1 or /api/v1. */
  baseUrl: string;
  fetch?: typeof globalThis.fetch;
  headers?: Record<string, string>;
  /** Cache mode of every request. Private reads use no-store. */
  cache?: RequestCache;
  /** Credential mode of every request. The same origin keeps the session cookie. */
  credentials?: RequestCredentials;
};

type ErrorEnvelope = { error?: { code?: string; message?: string } };

function isErrorEnvelope(value: unknown): value is ErrorEnvelope {
  return typeof value === "object" && value !== null && "error" in value;
}

/**
 * Creates a typed client for the API. The client never throws for a documented
 * API outcome: it returns the real status code and the parsed body so the
 * interface can show the state that actually happened.
 */
export function createClient(options: BlindmeshClientOptions): BlindmeshClient {
  const baseUrl = options.baseUrl.replace(/\/+$/, "");
  const doFetch = options.fetch ?? globalThis.fetch;

  async function request<T>(path: string, init?: RequestInit): Promise<ClientResult<T>> {
    let response: Response;

    try {
      response = await doFetch(`${baseUrl}${path}`, {
        ...init,
        cache: options.cache,
        credentials: options.credentials,
        headers: { Accept: "application/json", ...options.headers, ...init?.headers },
      });
    } catch {
      return { status: 0, ok: false };
    }

    const raw = await response.text();
    let parsed: unknown;

    if (raw.length > 0) {
      try {
        parsed = JSON.parse(raw);
      } catch {
        parsed = undefined;
      }
    }

    if (isErrorEnvelope(parsed) && parsed.error) {
      return {
        status: response.status,
        ok: response.ok,
        error: parsed as ApiErrorResponse,
      };
    }

    return {
      status: response.status,
      ok: response.ok,
      data: parsed as T | undefined,
    };
  }

  function withCsrf(csrfToken: string): Record<string, string> {
    return { "X-CSRF-Token": csrfToken };
  }

  return {
    healthLive: () => request<HealthLiveResponse>("/health/live"),
    healthReady: () => request<HealthReadyResponse>("/health/ready"),
    runtimeHeartbeat: (idempotencyKey, body) =>
      request<RuntimeHeartbeatResponse>("/runtime/heartbeat", {
        method: "POST",
        headers: { "Content-Type": "application/json", "Idempotency-Key": idempotencyKey },
        body: JSON.stringify(body ?? {}),
      }),
    getSession: () => request<SessionResponse>("/session", { cache: "no-store" }),
    startSignIn: (body, actor, csrfToken) =>
      request<SignInStartResponse>(`/auth/github/start${actor ? `?actor=${encodeURIComponent(actor)}` : ""}`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...(csrfToken ? withCsrf(csrfToken) : {}),
        },
        body: JSON.stringify(body ?? {}),
        cache: "no-store",
      }),
    logout: (csrfToken) =>
      request<never>("/logout", {
        method: "POST",
        headers: withCsrf(csrfToken),
        cache: "no-store",
      }),
    getProfile: () => request<ProfileResponse>("/profile", { cache: "no-store" }),
    updateProfile: (body, csrfToken) =>
      request<ProfileResponse>("/profile", {
        method: "PATCH",
        headers: { "Content-Type": "application/json", ...withCsrf(csrfToken) },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    getPublicProfile: (username) =>
      request<PublicProfileResponse>(`/people/${encodeURIComponent(username)}`, { cache: "no-store" }),
  };
}
