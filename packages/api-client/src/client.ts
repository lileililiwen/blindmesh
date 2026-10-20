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

export type ProjectListResponse = paths["/v1/projects"]["get"]["responses"][200]["content"]["application/json"];

export type ProjectSummaryResponse = ProjectListResponse["items"][number];

export type ProjectDetailResponse = paths["/v1/projects/{id}"]["get"]["responses"][200]["content"]["application/json"];

export type ProjectMemberResponse = ProjectDetailResponse["members"][number];

export type ProjectVersionResponse = ProjectDetailResponse["versions"][number];

export type ProjectStatusChangeResponse = ProjectDetailResponse["status_history"][number];

export type CapabilityListResponse = paths["/v1/capabilities"]["get"]["responses"][200]["content"]["application/json"];

export type CapabilityViewResponse = CapabilityListResponse["items"][number];

export type CapabilityStatusChangeResponse = CapabilityViewResponse["status_history"][number];

export type CapabilityEligibilityResponse =
  paths["/v1/capabilities/eligibility"]["post"]["responses"][200]["content"]["application/json"];

export type CapabilityConditionOutcomeResponse = CapabilityEligibilityResponse["conditions"][number];

/** The body of a create command of a capability. */
export type CreateCapabilityBody = Partial<
  paths["/v1/capabilities"]["post"]["requestBody"]["content"]["application/json"]
>;

/** The body of a change of a capability. A field that is left out keeps its value. */
export type UpdateCapabilityBody = Partial<
  paths["/v1/capabilities/{id}"]["patch"]["requestBody"]["content"]["application/json"]
>;

/** The body of a withdraw command of a capability. */
export type WithdrawCapabilityBody = Partial<
  paths["/v1/capabilities/{id}/withdraw"]["post"]["requestBody"]["content"]["application/json"]
>;

/** The body of an eligibility evaluation with a set of hard conditions. */
export type EvaluateEligibilityBody = Partial<
  paths["/v1/capabilities/eligibility"]["post"]["requestBody"]["content"]["application/json"]
>;

/** The body of a create command of a project. */
export type CreateProjectBody = Partial<
  paths["/v1/projects"]["post"]["requestBody"]["content"]["application/json"]
>;

/** The body of a change of a project. A field that is left out keeps its value. */
export type UpdateProjectBody = Partial<
  paths["/v1/projects/{id}"]["patch"]["requestBody"]["content"]["application/json"]
>;

/** The body of a transition of the lifecycle of a project. */
export type TransitionProjectBody = Partial<
  paths["/v1/projects/{id}/transitions"]["post"]["requestBody"]["content"]["application/json"]
>;

/** The body of an add member command of a project. */
export type AddProjectMemberBody = Partial<
  paths["/v1/projects/{id}/members/{user_id}"]["put"]["requestBody"]["content"]["application/json"]
>;

/** The body of an add version command of a project. */
export type AddProjectVersionBody = Partial<
  paths["/v1/projects/{id}/versions"]["post"]["requestBody"]["content"]["application/json"]
>;

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
  /** Lists the projects the caller may see. */
  listProjects(limit?: number, cursor?: string): Promise<ClientResult<ProjectListResponse>>;
  /** Reads the committed state of a project. */
  getProject(id: string): Promise<ClientResult<ProjectDetailResponse>>;
  /** Creates a project in the draft state. */
  createProject(
    body: CreateProjectBody,
    csrfToken: string,
    idempotencyKey?: string,
  ): Promise<ClientResult<ProjectDetailResponse>>;
  /** Saves the content or the visibility of a project. */
  updateProject(
    id: string,
    body: UpdateProjectBody,
    csrfToken: string,
  ): Promise<ClientResult<ProjectDetailResponse>>;
  /** Applies a transition of the lifecycle of a project. */
  transitionProject(
    id: string,
    body: TransitionProjectBody,
    csrfToken: string,
  ): Promise<ClientResult<ProjectDetailResponse>>;
  /** Adds a member to a project or changes the role of a member. */
  addProjectMember(
    id: string,
    userReference: string,
    body: AddProjectMemberBody,
    csrfToken: string,
  ): Promise<ClientResult<ProjectDetailResponse>>;
  /** Removes a member of a project. */
  removeProjectMember(
    id: string,
    userReference: string,
    csrfToken: string,
  ): Promise<ClientResult<never>>;
  /** Records a released version of a project. */
  addProjectVersion(
    id: string,
    body: AddProjectVersionBody,
    csrfToken: string,
  ): Promise<ClientResult<ProjectDetailResponse>>;
  /** Lists the capabilities the caller may see. */
  listCapabilities(limit?: number, cursor?: string, scope?: string): Promise<ClientResult<CapabilityListResponse>>;
  /** Reads the committed state of a capability. */
  getCapability(id: string): Promise<ClientResult<CapabilityViewResponse>>;
  /** Declares a capability of the signed in contributor. */
  createCapability(
    body: CreateCapabilityBody,
    csrfToken: string,
    idempotencyKey?: string,
  ): Promise<ClientResult<CapabilityViewResponse>>;
  /** Saves the content or the visibility of a capability. */
  updateCapability(
    id: string,
    body: UpdateCapabilityBody,
    csrfToken: string,
  ): Promise<ClientResult<CapabilityViewResponse>>;
  /** Withdraws a capability of the signed in contributor. */
  withdrawCapability(
    id: string,
    body: WithdrawCapabilityBody,
    csrfToken: string,
  ): Promise<ClientResult<CapabilityViewResponse>>;
  /** Evaluates a set of hard conditions against the declarations of the caller. */
  evaluateEligibility(
    body: EvaluateEligibilityBody,
    csrfToken: string,
  ): Promise<ClientResult<CapabilityEligibilityResponse>>;
  /** Evaluates the hard conditions of a published task against the declarations of the caller. */
  evaluateTaskEligibility(taskId: string, csrfToken: string): Promise<ClientResult<CapabilityEligibilityResponse>>;
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
    listProjects: (limit, cursor) => {
      const query = new URLSearchParams();

      if (limit !== undefined) {
        query.set("limit", String(limit));
      }

      if (cursor) {
        query.set("cursor", cursor);
      }

      const suffix = query.toString();

      return request<ProjectListResponse>(`/projects${suffix ? `?${suffix}` : ""}`, { cache: "no-store" });
    },
    getProject: (id) =>
      request<ProjectDetailResponse>(`/projects/${encodeURIComponent(id)}`, { cache: "no-store" }),
    createProject: (body, csrfToken, idempotencyKey) =>
      request<ProjectDetailResponse>("/projects", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...withCsrf(csrfToken),
          ...(idempotencyKey ? { "Idempotency-Key": idempotencyKey } : {}),
        },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    updateProject: (id, body, csrfToken) =>
      request<ProjectDetailResponse>(`/projects/${encodeURIComponent(id)}`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json", ...withCsrf(csrfToken) },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    transitionProject: (id, body, csrfToken) =>
      request<ProjectDetailResponse>(`/projects/${encodeURIComponent(id)}/transitions`, {
        method: "POST",
        headers: { "Content-Type": "application/json", ...withCsrf(csrfToken) },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    addProjectMember: (id, userReference, body, csrfToken) =>
      request<ProjectDetailResponse>(
        `/projects/${encodeURIComponent(id)}/members/${encodeURIComponent(userReference)}`,
        {
          method: "PUT",
          headers: { "Content-Type": "application/json", ...withCsrf(csrfToken) },
          body: JSON.stringify(body),
          cache: "no-store",
        },
      ),
    removeProjectMember: (id, userReference, csrfToken) =>
      request<never>(
        `/projects/${encodeURIComponent(id)}/members/${encodeURIComponent(userReference)}`,
        {
          method: "DELETE",
          headers: withCsrf(csrfToken),
          cache: "no-store",
        },
      ),
    addProjectVersion: (id, body, csrfToken) =>
      request<ProjectDetailResponse>(`/projects/${encodeURIComponent(id)}/versions`, {
        method: "POST",
        headers: { "Content-Type": "application/json", ...withCsrf(csrfToken) },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    listCapabilities: (limit, cursor, scope) => {
      const query = new URLSearchParams();

      if (limit !== undefined) {
        query.set("limit", String(limit));
      }

      if (cursor) {
        query.set("cursor", cursor);
      }

      if (scope) {
        query.set("scope", scope);
      }

      const suffix = query.toString();

      return request<CapabilityListResponse>(`/capabilities${suffix ? `?${suffix}` : ""}`, { cache: "no-store" });
    },
    getCapability: (id) => request<CapabilityViewResponse>(`/capabilities/${encodeURIComponent(id)}`, { cache: "no-store" }),
    createCapability: (body, csrfToken, idempotencyKey) =>
      request<CapabilityViewResponse>("/capabilities", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...withCsrf(csrfToken),
          ...(idempotencyKey ? { "Idempotency-Key": idempotencyKey } : {}),
        },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    updateCapability: (id, body, csrfToken) =>
      request<CapabilityViewResponse>(`/capabilities/${encodeURIComponent(id)}`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json", ...withCsrf(csrfToken) },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    withdrawCapability: (id, body, csrfToken) =>
      request<CapabilityViewResponse>(`/capabilities/${encodeURIComponent(id)}/withdraw`, {
        method: "POST",
        headers: { "Content-Type": "application/json", ...withCsrf(csrfToken) },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    evaluateEligibility: (body, csrfToken) =>
      request<CapabilityEligibilityResponse>("/capabilities/eligibility", {
        method: "POST",
        headers: { "Content-Type": "application/json", ...withCsrf(csrfToken) },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    evaluateTaskEligibility: (taskId, csrfToken) =>
      request<CapabilityEligibilityResponse>(`/capabilities/tasks/${encodeURIComponent(taskId)}/eligibility`, {
        method: "GET",
        headers: { ...withCsrf(csrfToken) },
        cache: "no-store",
      }),
  };
}
