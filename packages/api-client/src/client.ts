import type { paths } from "./generated/schema";

export type HealthLiveResponse = paths["/v1/health/live"]["get"]["responses"][200]["content"]["application/json"];

export type HealthReadyResponse = paths["/v1/health/ready"]["get"]["responses"][200]["content"]["application/json"];

export type RuntimeHeartbeatResponse =
  paths["/v1/runtime/heartbeat"]["post"]["responses"][201]["content"]["application/json"];

export type ApiErrorResponse = paths["/v1/runtime/heartbeat"]["post"]["responses"][400]["content"]["application/json"];

/** The outcome of one API call. The status code is always the real one. */
export type ClientResult<T> = {
  status: number;
  ok: boolean;
  data?: T;
  error?: ApiErrorResponse;
};

export type BlindmeshClient = {
  healthLive(): Promise<ClientResult<HealthLiveResponse>>;
  healthReady(): Promise<ClientResult<HealthReadyResponse>>;
  runtimeHeartbeat(idempotencyKey: string, body?: { note?: string }): Promise<ClientResult<RuntimeHeartbeatResponse>>;
};

export type BlindmeshClientOptions = {
  /** Absolute API base, for example http://127.0.0.1:8080/v1 or /api/v1. */
  baseUrl: string;
  fetch?: typeof globalThis.fetch;
  headers?: Record<string, string>;
  /** Cache mode of every request. Private reads use no-store. */
  cache?: RequestCache;
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

  return {
    healthLive: () => request<HealthLiveResponse>("/health/live"),
    healthReady: () => request<HealthReadyResponse>("/health/ready"),
    runtimeHeartbeat: (idempotencyKey, body) =>
      request<RuntimeHeartbeatResponse>("/runtime/heartbeat", {
        method: "POST",
        headers: { "Content-Type": "application/json", "Idempotency-Key": idempotencyKey },
        body: JSON.stringify(body ?? {}),
      }),
  };
}
