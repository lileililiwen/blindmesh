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

export type CandidateListResponse =
  paths["/v1/tasks/{id}/candidates"]["get"]["responses"][200]["content"]["application/json"];

export type CandidateMatchResponse = CandidateListResponse["candidates"][number];

export type CandidateConditionResponse = CandidateMatchResponse["conditions"][number];

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

export type ReportReceiptResponse =
  paths["/v1/reports"]["post"]["responses"][201]["content"]["application/json"];

export type ReportViewResponse =
  paths["/v1/admin/reports"]["get"]["responses"][200]["content"]["application/json"][number];

export type DisputeViewResponse =
  paths["/v1/disputes/{id}"]["get"]["responses"][200]["content"]["application/json"];

export type DisputeSummaryResponse =
  paths["/v1/admin/disputes"]["get"]["responses"][200]["content"]["application/json"][number];

export type BanViewResponse =
  paths["/v1/admin/users/{id}/ban"]["post"]["responses"][200]["content"]["application/json"];

export type ReputationSummaryResponse =
  paths["/v1/people/{username}/reputation"]["get"]["responses"][200]["content"]["application/json"];

export type ReputationHistoryResponse =
  paths["/v1/people/{username}/reputation/history"]["get"]["responses"][200]["content"]["application/json"];

export type ReputationOwnHistoryResponse =
  paths["/v1/reputation/history"]["get"]["responses"][200]["content"]["application/json"];

export type ReputationEventResponse = ReputationHistoryResponse["events"][number];

export type NotificationListResponse =
  paths["/v1/notifications"]["get"]["responses"][200]["content"]["application/json"];

export type NotificationResponse =
  paths["/v1/notifications/{id}/read"]["post"]["responses"][200]["content"]["application/json"];

export type NotificationPreferencesResponse =
  paths["/v1/notification-preferences"]["get"]["responses"][200]["content"]["application/json"];

/** The body of a change of the notification preferences. It holds no credential. */
export type UpdateNotificationPreferencesBody = Partial<
  paths["/v1/notification-preferences"]["patch"]["requestBody"]["content"]["application/json"]
>;

export type OperationBackupListResponse =
  paths["/v1/admin/operations/backups"]["get"]["responses"][200]["content"]["application/json"];

export type OperationBackupResponse =
  paths["/v1/admin/operations/backups"]["post"]["responses"][201]["content"]["application/json"];

export type RestoreRehearsalListResponse =
  paths["/v1/admin/operations/restores"]["get"]["responses"][200]["content"]["application/json"];

export type RestoreRehearsalResponse =
  paths["/v1/admin/operations/restores"]["post"]["responses"][200]["content"]["application/json"];

export type DeletionRequestListResponse =
  paths["/v1/admin/operations/deletions"]["get"]["responses"][200]["content"]["application/json"];

export type DeletionRequestResponse =
  paths["/v1/admin/operations/deletions"]["post"]["responses"][201]["content"]["application/json"];

export type PilotGateResponse =
  paths["/v1/admin/operations/gate"]["get"]["responses"][200]["content"]["application/json"];

export type PublicStatusResponse =
  paths["/v1/status"]["get"]["responses"][200]["content"]["application/json"];

/** The body of the record of a backup. It holds no credential. */
export type RecordBackupBody = Partial<
  paths["/v1/admin/operations/backups"]["post"]["requestBody"]["content"]["application/json"]
>;

/** The body of the command that runs a restore rehearsal. */
export type RunRestoreRehearsalBody = Partial<
  paths["/v1/admin/operations/restores"]["post"]["requestBody"]["content"]["application/json"]
>;

/** The body of the command that handles a deletion request. */
export type CreateDeletionRequestBody = Partial<
  paths["/v1/admin/operations/deletions"]["post"]["requestBody"]["content"]["application/json"]
>;

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

/** The body of a report of abusive content. It holds no credential. */
export type CreateReportBody = Partial<
  paths["/v1/reports"]["post"]["requestBody"]["content"]["application/json"]
>;

/** The body of the opening of a case of arbitration. */
export type OpenDisputeBody = Partial<
  paths["/v1/submissions/{id}/disputes"]["post"]["requestBody"]["content"]["application/json"]
>;

/** The body of the appeal of a decision of a case. */
export type AppealDisputeBody = Partial<
  paths["/v1/disputes/{id}/appeal"]["post"]["requestBody"]["content"]["application/json"]
>;

/** The body of the request of more evidence of a case. */
export type RequestEvidenceBody = Partial<
  paths["/v1/admin/disputes/{id}/request-evidence"]["post"]["requestBody"]["content"]["application/json"]
>;

/** The body of the decision of a case. */
export type ResolveDisputeBody = Partial<
  paths["/v1/admin/disputes/{id}/resolve"]["post"]["requestBody"]["content"]["application/json"]
>;

/** The body of the closing of a case. */
export type CloseDisputeBody = Partial<
  paths["/v1/admin/disputes/{id}/close"]["post"]["requestBody"]["content"]["application/json"]
>;

/** The body of the ban of an account. */
export type BanUserBody = Partial<
  paths["/v1/admin/users/{id}/ban"]["post"]["requestBody"]["content"]["application/json"]
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
  /** Reads the accounts that fit a task of the project of the caller. */
  listTaskCandidates(taskId: string): Promise<ClientResult<CandidateListResponse>>;
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
  /** Writes a report of abusive content and reads the receipt of it back. */
  createReport(
    body: CreateReportBody,
    csrfToken: string,
    idempotencyKey: string,
  ): Promise<ClientResult<ReportReceiptResponse>>;
  /** Reads the receipt of a report of the reporter. */
  getReport(id: string): Promise<ClientResult<ReportReceiptResponse>>;
  /** Lists the reports of the moderation staff. */
  listReports(): Promise<ClientResult<ReportViewResponse[]>>;
  /** Opens the case of arbitration of a seat and freezes its settlement. */
  openDispute(
    submissionId: string,
    body: OpenDisputeBody,
    csrfToken: string,
    idempotencyKey: string,
  ): Promise<ClientResult<DisputeViewResponse>>;
  /** Reads the case of an arbitration with its trail. */
  getDispute(id: string): Promise<ClientResult<DisputeViewResponse>>;
  /** Lists the cases of the moderation staff. */
  listDisputes(): Promise<ClientResult<DisputeSummaryResponse[]>>;
  /** Asks the parties of a case for more evidence. */
  requestDisputeEvidence(
    id: string,
    body: RequestEvidenceBody,
    csrfToken: string,
    idempotencyKey: string,
  ): Promise<ClientResult<DisputeViewResponse>>;
  /** Decides a case and moves the escrow of the seat. */
  resolveDispute(
    id: string,
    body: ResolveDisputeBody,
    csrfToken: string,
    idempotencyKey: string,
  ): Promise<ClientResult<DisputeViewResponse>>;
  /** Closes a case whose decision stands. */
  closeDispute(
    id: string,
    body: CloseDisputeBody,
    csrfToken: string,
    idempotencyKey: string,
  ): Promise<ClientResult<DisputeViewResponse>>;
  /** Appeals the decision of a case once. */
  appealDispute(
    id: string,
    body: AppealDisputeBody,
    csrfToken: string,
    idempotencyKey: string,
  ): Promise<ClientResult<DisputeViewResponse>>;
  /** Bans an account and refuses its requests. */
  banUser(
    id: string,
    body: BanUserBody,
    csrfToken: string,
    idempotencyKey: string,
  ): Promise<ClientResult<BanViewResponse>>;
  /** Reads the public reputation summary of a contributor. */
  getPublicReputation(username: string): Promise<ClientResult<ReputationSummaryResponse>>;
  /** Reads the public history of the reputation of a contributor. */
  getPublicReputationHistory(username: string): Promise<ClientResult<ReputationHistoryResponse>>;
  /** Reads the own history of the reputation of the signed in contributor. */
  getOwnReputationHistory(): Promise<ClientResult<ReputationOwnHistoryResponse>>;
  /** Reads the notifications of the signed in contributor, the newest first. */
  listNotifications(limit?: number, cursor?: string): Promise<ClientResult<NotificationListResponse>>;
  /** Marks one notification of the signed in contributor as read. */
  markNotificationRead(
    id: string,
    csrfToken: string,
  ): Promise<ClientResult<NotificationResponse>>;
  /** Reads the notification preferences of the signed in contributor. */
  getNotificationPreferences(): Promise<ClientResult<NotificationPreferencesResponse>>;
  /** Saves the notification preferences of the signed in contributor. */
  updateNotificationPreferences(
    body: UpdateNotificationPreferencesBody,
    csrfToken: string,
  ): Promise<ClientResult<NotificationPreferencesResponse>>;
  /** Lists the backups of the product, the newest first. */
  listOperationBackups(): Promise<ClientResult<OperationBackupListResponse>>;
  /** Records a backup the operator took. */
  recordOperationBackup(
    body: RecordBackupBody,
    csrfToken: string,
  ): Promise<ClientResult<OperationBackupResponse>>;
  /** Lists the restore rehearsals, the newest first. */
  listRestoreRehearsals(): Promise<ClientResult<RestoreRehearsalListResponse>>;
  /** Runs a restore rehearsal against an isolated database. */
  runRestoreRehearsal(
    body: RunRestoreRehearsalBody,
    csrfToken: string,
  ): Promise<ClientResult<RestoreRehearsalResponse>>;
  /** Lists the deletion requests, the newest first. */
  listDeletionRequests(): Promise<ClientResult<DeletionRequestListResponse>>;
  /** Removes the identity of an account and keeps its pseudonym. */
  createDeletionRequest(
    body: CreateDeletionRequestBody,
    csrfToken: string,
  ): Promise<ClientResult<DeletionRequestResponse>>;
  /** Reads the measured gate of the pilot. */
  getPilotGate(): Promise<ClientResult<PilotGateResponse>>;
  /** Reads the public status of the product. */
  getPublicStatus(): Promise<ClientResult<PublicStatusResponse>>;
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
    listTaskCandidates: (taskId) =>
      request<CandidateListResponse>(`/tasks/${encodeURIComponent(taskId)}/candidates`, {
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
    createReport: (body, csrfToken, idempotencyKey) =>
      request<ReportReceiptResponse>("/reports", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...withCsrf(csrfToken),
          "Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    getReport: (id) => request<ReportReceiptResponse>(`/reports/${encodeURIComponent(id)}`, { cache: "no-store" }),
    listReports: () => request<ReportViewResponse[]>("/admin/reports", { cache: "no-store" }),
    openDispute: (submissionId, body, csrfToken, idempotencyKey) =>
      request<DisputeViewResponse>(`/submissions/${encodeURIComponent(submissionId)}/disputes`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...withCsrf(csrfToken),
          "Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    getDispute: (id) => request<DisputeViewResponse>(`/disputes/${encodeURIComponent(id)}`, { cache: "no-store" }),
    listDisputes: () => request<DisputeSummaryResponse[]>("/admin/disputes", { cache: "no-store" }),
    requestDisputeEvidence: (id, body, csrfToken, idempotencyKey) =>
      request<DisputeViewResponse>(`/admin/disputes/${encodeURIComponent(id)}/request-evidence`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...withCsrf(csrfToken),
          "Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    resolveDispute: (id, body, csrfToken, idempotencyKey) =>
      request<DisputeViewResponse>(`/admin/disputes/${encodeURIComponent(id)}/resolve`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...withCsrf(csrfToken),
          "Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    closeDispute: (id, body, csrfToken, idempotencyKey) =>
      request<DisputeViewResponse>(`/admin/disputes/${encodeURIComponent(id)}/close`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...withCsrf(csrfToken),
          "Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    appealDispute: (id, body, csrfToken, idempotencyKey) =>
      request<DisputeViewResponse>(`/disputes/${encodeURIComponent(id)}/appeal`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...withCsrf(csrfToken),
          "Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    banUser: (id, body, csrfToken, idempotencyKey) =>
      request<BanViewResponse>(`/admin/users/${encodeURIComponent(id)}/ban`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...withCsrf(csrfToken),
          "Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    getPublicReputation: (username) =>
      request<ReputationSummaryResponse>(`/people/${encodeURIComponent(username)}/reputation`, {
        cache: "no-store",
      }),
    getPublicReputationHistory: (username) =>
      request<ReputationHistoryResponse>(`/people/${encodeURIComponent(username)}/reputation/history`, {
        cache: "no-store",
      }),
    getOwnReputationHistory: () => request<ReputationOwnHistoryResponse>("/reputation/history", { cache: "no-store" }),
    listNotifications: (limit, cursor) => {
      const query = new URLSearchParams();

      if (limit !== undefined) {
        query.set("limit", String(limit));
      }

      if (cursor) {
        query.set("cursor", cursor);
      }

      const suffix = query.toString();

      return request<NotificationListResponse>(`/notifications${suffix ? `?${suffix}` : ""}`, {
        cache: "no-store",
      });
    },
    markNotificationRead: (id, csrfToken) =>
      request<NotificationResponse>(`/notifications/${encodeURIComponent(id)}/read`, {
        method: "POST",
        headers: { ...withCsrf(csrfToken) },
        cache: "no-store",
      }),
    getNotificationPreferences: () =>
      request<NotificationPreferencesResponse>("/notification-preferences", { cache: "no-store" }),
    updateNotificationPreferences: (body, csrfToken) =>
      request<NotificationPreferencesResponse>("/notification-preferences", {
        method: "PATCH",
        headers: { "Content-Type": "application/json", ...withCsrf(csrfToken) },
        body: JSON.stringify(body),
        cache: "no-store",
      }),
    listOperationBackups: () => request<OperationBackupListResponse>("/admin/operations/backups", { cache: "no-store" }),
    recordOperationBackup: (body, csrfToken) =>
      request<OperationBackupResponse>("/admin/operations/backups", {
        method: "POST",
        headers: { "Content-Type": "application/json", ...withCsrf(csrfToken) },
        body: JSON.stringify(body ?? {}),
        cache: "no-store",
      }),
    listRestoreRehearsals: () => request<RestoreRehearsalListResponse>("/admin/operations/restores", { cache: "no-store" }),
    runRestoreRehearsal: (body, csrfToken) =>
      request<RestoreRehearsalResponse>("/admin/operations/restores", {
        method: "POST",
        headers: { "Content-Type": "application/json", ...withCsrf(csrfToken) },
        body: JSON.stringify(body ?? {}),
        cache: "no-store",
      }),
    listDeletionRequests: () => request<DeletionRequestListResponse>("/admin/operations/deletions", { cache: "no-store" }),
    createDeletionRequest: (body, csrfToken) =>
      request<DeletionRequestResponse>("/admin/operations/deletions", {
        method: "POST",
        headers: { "Content-Type": "application/json", ...withCsrf(csrfToken) },
        body: JSON.stringify(body ?? {}),
        cache: "no-store",
      }),
    getPilotGate: () => request<PilotGateResponse>("/admin/operations/gate", { cache: "no-store" }),
    getPublicStatus: () => request<PublicStatusResponse>("/status", { cache: "no-store" }),
  };
}
