import {
  createClient,
  type ClientResult,
  type HealthReadyResponse,
} from "@blindmesh/api-client";

export type Availability = HealthReadyResponse;

export type AvailabilityState =
  | { kind: "loading" }
  | { kind: "ready"; availability: Availability }
  | { kind: "unavailable"; availability?: Availability; message: string }
  | { kind: "error"; message: string }
  | { kind: "forbidden"; message: string }
  | { kind: "empty"; message: string };

const errorMessage = "The runtime could not be reached. The API did not answer.";

export function toAvailabilityState(
  result: ClientResult<HealthReadyResponse>,
): AvailabilityState {
  if (result.status === 0) {
    return { kind: "error", message: errorMessage };
  }

  if (result.status === 403) {
    return {
      kind: "forbidden",
      message: "You are not allowed to read the runtime status.",
    };
  }

  if (result.status === 503) {
    return {
      kind: "unavailable",
      availability: result.data,
      message:
        "The runtime is not ready. A required dependency is not reachable, so no request can be served yet.",
    };
  }

  if (!result.ok || !result.data) {
    return {
      kind: "error",
      message: `The runtime answered with status ${result.status}.`,
    };
  }

  if (result.data.dependencies.length === 0) {
    return {
      kind: "empty",
      message: "The runtime reported no dependencies. Retry to read the current state.",
    };
  }

  return { kind: "ready", availability: result.data };
}

/** Reads the availability in the browser through the same origin path. */
export async function loadAvailabilityBrowser(): Promise<AvailabilityState> {
  const client = createClient({ baseUrl: "/api/v1", cache: "no-store" });

  return toAvailabilityState(await client.healthReady());
}

export function announcementFor(state: AvailabilityState): string {
  switch (state.kind) {
    case "ready":
      return "The runtime is available.";
    case "unavailable":
      return "The runtime is still unavailable. The retry did not change the state.";
    case "error":
      return "The runtime could not be reached.";
    case "forbidden":
      return "The runtime status is not available to you.";
    case "empty":
      return "The runtime reported no dependencies.";
    default:
      return "";
  }
}
