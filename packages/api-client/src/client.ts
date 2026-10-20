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

export type CreditWalletResponse = paths["/v1/wallet"]["get"]["responses"][200]["content"]["application/json"];

export type CreditLotResponse = CreditWalletResponse["lots"][number];

export type CreditGrantResponse = CreditWalletResponse["grant"];

export type CreditTransactionPageResponse =
  paths["/v1/wallet/transactions"]["get"]["responses"][200]["content"]["application/json"];

export type CreditTransactionResponse = CreditTransactionPageResponse["items"][number];

export type TaskListResponse = paths["/v1/tasks"]["get"]["responses"][200]["content"]["application/json"];

export type TaskSummaryResponse = TaskListResponse["items"][number];

export type TaskDetailResponse = paths["/v1/tasks/{id}"]["get"]["responses"][200]["content"]["application/json"];

export type TaskRequirementResponse = TaskSummaryResponse["requirements"][number];

export type TaskStepResponse = TaskDetailResponse["steps"][number];

export type TaskSlotResponse = TaskDetailResponse["slots"][number];

export type TaskStatusChangeResponse = TaskDetailResponse["status_history"][number];

export type ParticipationListResponse = paths["/v1/work"]["get"]["responses"][200]["content"]["application/json"];

export type ParticipationSummaryResponse = ParticipationListResponse["items"][number];

export type ParticipationDetailResponse =
  paths["/v1/participations/{id}"]["get"]["responses"][200]["content"]["application/json"];

export type ParticipationStatusChangeResponse = ParticipationDetailResponse["history"][number];

export type SubmissionDetailResponse =
  paths["/v1/submissions/{id}"]["get"]["responses"][200]["content"]["application/json"];

export type SubmissionRevisionResponse = SubmissionDetailResponse["revisions"][number];

export type SubmissionFindingResponse = SubmissionDetailResponse["findings"][number];

export type SubmissionHistoryResponse = SubmissionDetailResponse["history"][number];

export type SubmissionSummaryResponse =
  paths["/v1/projects/{id}/reviews"]["get"]["responses"][200]["content"]["application/json"][number];

export type FindingDetailResponse =
  paths["/v1/findings/{id}"]["get"]["responses"][200]["content"]["application/json"];

export type AttachmentViewResponse =
  paths["/v1/attachments/intents"]["post"]["responses"][201]["content"]["application/json"];

export type AttachmentDownloadResponse =
  paths["/v1/attachments/{id}/download"]["get"]["responses"][200]["content"]["application/json"];

/** The body of the evidence of a seat. It holds no credential. */
export type SubmitEvidenceBody = Partial<
  paths["/v1/participations/{id}/submissions"]["post"]["requestBody"]["content"]["application/json"]
>;

/** The body of the acceptance of the work of a seat. */
export type AcceptSubmissionBody = Partial<
  paths["/v1/submissions/{id}/accept"]["post"]["requestBody"]["content"]["application/json"]
>;

/** The body of the request for the one correction of a seat. */
export type RequestChangesBody = Partial<
  paths["/v1/submissions/{id}/request-changes"]["post"]["requestBody"]["content"]["application/json"]
>;

/** The body of the dispute about the completion of a seat. */
export type DisputeSubmissionBody = Partial<
  paths["/v1/submissions/{id}/dispute"]["post"]["requestBody"]["content"]["application/json"]
>;

/** The body of the classification of a finding. */
export type PatchFindingBody = Partial<
  paths["/v1/findings/{id}"]["patch"]["requestBody"]["content"]["application/json"]
>;

/** The body of the declaration of a private file of the evidence. */
export type AttachmentIntentBody = Partial<
  paths["/v1/attachments/intents"]["post"]["requestBody"]["content"]["application/json"]
>;

/** The body of an application for a slot of a task. It holds no credential. */
export type ApplyForSlotBody = Partial<
  paths["/v1/tasks/{id}/applications"]["post"]["requestBody"]["content"]["application/json"]
>;

/** The body of a write command of a task. It holds no credential. */
export type TaskBody = Partial<
  paths["/v1/tasks"]["post"]["requestBody"]["content"]["application/json"]
>;

/** The body of the grant command. It holds no credential. */
export type ClaimGrantBody = Partial<
  paths["/v1/wallet/grant"]["post"]["requestBody"]["content"]["application/json"]
>;

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
  /** Reads the wallet of the signed in contributor. */
  getWallet(): Promise<ClientResult<CreditWalletResponse>>;
  /** Lists the transactions of the wallet of the signed in contributor. */
  listWalletTransactions(limit?: number, cursor?: string): Promise<ClientResult<CreditTransactionPageResponse>>;
  /** Claims the limited grant of the signed in contributor. */
  claimGrant(
    body: ClaimGrantBody,
    csrfToken: string,
    idempotencyKey: string,
  ): Promise<ClientResult<CreditWalletResponse>>;
  /** Lists the tasks of the marketplace that the caller may see. */
  listTasks(options?: {
    limit?: number;
    cursor?: string;
    mode?: string;
    requirement?: string;
    projectId?: string;
  }): Promise<ClientResult<TaskListResponse>>;
  /** Reads the committed state of a task. */
  getTask(id: string): Promise<ClientResult<TaskDetailResponse>>;
  /** Writes a task in the draft state. */
  createTask(
    body: TaskBody,
    csrfToken: string,
    idempotencyKey?: string,
  ): Promise<ClientResult<TaskDetailResponse>>;
  /** Changes the editable content of a task. */
  updateTask(
    id: string,
    body: TaskBody,
    csrfToken: string,
  ): Promise<ClientResult<TaskDetailResponse>>;
  /** Publishes a task and reserves its maximum budget. */
  publishTask(
    id: string,
    csrfToken: string,
    idempotencyKey: string,
  ): Promise<ClientResult<TaskDetailResponse>>;
  /** Closes the admission of a task and refunds its unused slots. */
  cancelTask(
    id: string,
    csrfToken: string,
    idempotencyKey: string,
  ): Promise<ClientResult<TaskDetailResponse>>;
  /** Lists the work of the signed in contributor. */
  listWork(limit?: number, cursor?: string): Promise<ClientResult<ParticipationListResponse>>;
  /** Lists the applications of a task of the owner. */
  listTaskApplications(
    taskId: string,
    limit?: number,
    cursor?: string,
  ): Promise<ClientResult<ParticipationListResponse>>;
  /** Reads the committed state of one seat of a task. */
  getParticipation(id: string): Promise<ClientResult<ParticipationDetailResponse>>;
  /** Applies for one slot of a published task. */
  applyForSlot(
    taskId: string,
    body: ApplyForSlotBody,
    csrfToken: string,
    idempotencyKey: string,
  ): Promise<ClientResult<ParticipationDetailResponse>>;
  /** Accepts an application and leases one slot of the task. */
  acceptParticipation(
    id: string,
    csrfToken: string,
    idempotencyKey: string,
  ): Promise<ClientResult<ParticipationDetailResponse>>;
  /** Declines an application of a task. */
  declineParticipation(
    id: string,
    csrfToken: string,
    idempotencyKey: string,
  ): Promise<ClientResult<ParticipationDetailResponse>>;
  /** Starts the work of an accepted seat. */
  startParticipation(
    id: string,
    csrfToken: string,
    idempotencyKey: string,
  ): Promise<ClientResult<ParticipationDetailResponse>>;
  /** Withdraws a seat before the work starts. */
  withdrawParticipation(
    id: string,
    csrfToken: string,
    idempotencyKey: string,
  ): Promise<ClientResult<ParticipationDetailResponse>>;
  /** Sends the protected evidence of a seat that the caller runs. */
  submitEvidence(
    participationId: string,
    body: SubmitEvidenceBody,
    csrfToken: string,
    idempotencyKey: string,
  ): Promise<ClientResult<SubmissionDetailResponse>>;
  /** Reads the committed state of the evidence of a seat. */
  getSubmission(id: string): Promise<ClientResult<SubmissionDetailResponse>>;
  /** Lists the evidence of a project that waits for the owner. */
  listProjectReviews(projectId: string): Promise<ClientResult<SubmissionSummaryResponse[]>>;
  /** Accepts the work of a seat and settles the credits of the contributor. */
  acceptSubmission(
    id: string,
    body: AcceptSubmissionBody,
    csrfToken: string,
    idempotencyKey: string,
  ): Promise<ClientResult<SubmissionDetailResponse>>;
  /** Asks the contributor for the one correction of the evidence. */
  requestSubmissionChanges(
    id: string,
    body: RequestChangesBody,
    csrfToken: string,
    idempotencyKey: string,
  ): Promise<ClientResult<SubmissionDetailResponse>>;
  /** Opens a dispute about the completion of a seat. */
  disputeSubmission(
    id: string,
    body: DisputeSubmissionBody,
    csrfToken: string,
    idempotencyKey: string,
  ): Promise<ClientResult<SubmissionDetailResponse>>;
  /** Reads one finding of the evidence with its history. */
  getFinding(id: string): Promise<ClientResult<FindingDetailResponse>>;
  /** Classifies one finding of the evidence. */
  patchFinding(
    id: string,
    body: PatchFindingBody,
    csrfToken: string,
    idempotencyKey: string,
  ): Promise<ClientResult<FindingDetailResponse>>;
  /** Declares a private file of the evidence. */
  createAttachmentIntent(
    body: AttachmentIntentBody,
    csrfToken: string,
    idempotencyKey: string,
  ): Promise<ClientResult<AttachmentViewResponse>>;
  /** Uploads the bytes of a quarantined file and releases it. */
  completeAttachment(
    attachmentId: string,
    file: File,
    csrfToken: string,
    idempotencyKey: string,
  ): Promise<ClientResult<AttachmentViewResponse>>;
  /** Signs the short address of a private file of the evidence. */
  downloadAttachment(id: string): Promise<ClientResult<AttachmentDownloadResponse>>;
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
    getWallet: () => request<CreditWalletResponse>("/wallet", { cache: "no-store" }),
    listWalletTransactions: (limit, cursor) => {
      const query = new URLSearchParams();

      if (limit !== undefined) {
        query.set("limit", String(limit));
      }

      if (cursor) {
        query.set("cursor", cursor);
      }

      const suffix = query.toString();

      return request<CreditTransactionPageResponse>(`/wallet/transactions${suffix ? `?${suffix}` : ""}`, {
        cache: "no-store",
      });
    },
    claimGrant: (body, csrfToken, idempotencyKey) =>
      request<CreditWalletResponse>("/wallet/grant", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...withCsrf(csrfToken),
          "Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    listTasks: (options) => {
      const query = new URLSearchParams();

      if (options?.limit !== undefined) {
        query.set("limit", String(options.limit));
      }

      if (options?.cursor) {
        query.set("cursor", options.cursor);
      }

      if (options?.mode) {
        query.set("mode", options.mode);
      }

      if (options?.requirement) {
        query.set("requirement", options.requirement);
      }

      if (options?.projectId) {
        query.set("project_id", options.projectId);
      }

      const suffix = query.toString();

      return request<TaskListResponse>(`/tasks${suffix ? `?${suffix}` : ""}`, { cache: "no-store" });
    },
    getTask: (id) => request<TaskDetailResponse>(`/tasks/${encodeURIComponent(id)}`, { cache: "no-store" }),
    createTask: (body, csrfToken, idempotencyKey) =>
      request<TaskDetailResponse>("/tasks", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...withCsrf(csrfToken),
          ...(idempotencyKey ? { "Idempotency-Key": idempotencyKey } : {}),
        },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    updateTask: (id, body, csrfToken) =>
      request<TaskDetailResponse>(`/tasks/${encodeURIComponent(id)}`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json", ...withCsrf(csrfToken) },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    publishTask: (id, csrfToken, idempotencyKey) =>
      request<TaskDetailResponse>(`/tasks/${encodeURIComponent(id)}/publish`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...withCsrf(csrfToken),
          "Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify({}),
        cache: "no-store",
      }),
    cancelTask: (id, csrfToken, idempotencyKey) =>
      request<TaskDetailResponse>(`/tasks/${encodeURIComponent(id)}/cancel`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...withCsrf(csrfToken),
          "Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify({}),
        cache: "no-store",
      }),
    listWork: (limit, cursor) => {
      const query = new URLSearchParams();

      if (limit !== undefined) {
        query.set("limit", String(limit));
      }

      if (cursor) {
        query.set("cursor", cursor);
      }

      const suffix = query.toString();

      return request<ParticipationListResponse>(`/work${suffix ? `?${suffix}` : ""}`, { cache: "no-store" });
    },
    listTaskApplications: (taskId, limit, cursor) => {
      const query = new URLSearchParams();

      if (limit !== undefined) {
        query.set("limit", String(limit));
      }

      if (cursor) {
        query.set("cursor", cursor);
      }

      const suffix = query.toString();

      return request<ParticipationListResponse>(
        `/tasks/${encodeURIComponent(taskId)}/applications${suffix ? `?${suffix}` : ""}`,
        { cache: "no-store" },
      );
    },
    getParticipation: (id) =>
      request<ParticipationDetailResponse>(`/participations/${encodeURIComponent(id)}`, { cache: "no-store" }),
    applyForSlot: (taskId, body, csrfToken, idempotencyKey) =>
      request<ParticipationDetailResponse>(`/tasks/${encodeURIComponent(taskId)}/applications`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...withCsrf(csrfToken),
          "Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    acceptParticipation: (id, csrfToken, idempotencyKey) =>
      request<ParticipationDetailResponse>(`/participations/${encodeURIComponent(id)}/accept`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...withCsrf(csrfToken),
          "Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify({}),
        cache: "no-store",
      }),
    declineParticipation: (id, csrfToken, idempotencyKey) =>
      request<ParticipationDetailResponse>(`/participations/${encodeURIComponent(id)}/decline`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...withCsrf(csrfToken),
          "Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify({}),
        cache: "no-store",
      }),
    startParticipation: (id, csrfToken, idempotencyKey) =>
      request<ParticipationDetailResponse>(`/participations/${encodeURIComponent(id)}/start`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...withCsrf(csrfToken),
          "Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify({}),
        cache: "no-store",
      }),
    withdrawParticipation: (id, csrfToken, idempotencyKey) =>
      request<ParticipationDetailResponse>(`/participations/${encodeURIComponent(id)}/withdraw`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...withCsrf(csrfToken),
          "Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify({}),
        cache: "no-store",
      }),
    submitEvidence: (participationId, body, csrfToken, idempotencyKey) =>
      request<SubmissionDetailResponse>(`/participations/${encodeURIComponent(participationId)}/submissions`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...withCsrf(csrfToken),
          "Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    getSubmission: (id) => request<SubmissionDetailResponse>(`/submissions/${encodeURIComponent(id)}`, { cache: "no-store" }),
    listProjectReviews: (projectId) =>
      request<SubmissionSummaryResponse[]>(`/projects/${encodeURIComponent(projectId)}/reviews`, { cache: "no-store" }),
    acceptSubmission: (id, body, csrfToken, idempotencyKey) =>
      request<SubmissionDetailResponse>(`/submissions/${encodeURIComponent(id)}/accept`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...withCsrf(csrfToken),
          "Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    requestSubmissionChanges: (id, body, csrfToken, idempotencyKey) =>
      request<SubmissionDetailResponse>(`/submissions/${encodeURIComponent(id)}/request-changes`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...withCsrf(csrfToken),
          "Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    disputeSubmission: (id, body, csrfToken, idempotencyKey) =>
      request<SubmissionDetailResponse>(`/submissions/${encodeURIComponent(id)}/dispute`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...withCsrf(csrfToken),
          "Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    getFinding: (id) => request<FindingDetailResponse>(`/findings/${encodeURIComponent(id)}`, { cache: "no-store" }),
    patchFinding: (id, body, csrfToken, idempotencyKey) =>
      request<FindingDetailResponse>(`/findings/${encodeURIComponent(id)}`, {
        method: "PATCH",
        headers: {
          "Content-Type": "application/json",
          ...withCsrf(csrfToken),
          "Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    createAttachmentIntent: (body, csrfToken, idempotencyKey) =>
      request<AttachmentViewResponse>("/attachments/intents", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...withCsrf(csrfToken),
          "Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    completeAttachment: (attachmentId, file, csrfToken, idempotencyKey) => {
      const form = new FormData();
      form.append("attachment_id", attachmentId);
      form.append("file", file);

      return request<AttachmentViewResponse>("/attachments/complete", {
        method: "POST",
        headers: {
          ...withCsrf(csrfToken),
          "Idempotency-Key": idempotencyKey,
        },
        body: form,
        cache: "no-store",
      });
    },
    downloadAttachment: (id) =>
      request<AttachmentDownloadResponse>(`/attachments/${encodeURIComponent(id)}/download`, { cache: "no-store" }),
  };
}
