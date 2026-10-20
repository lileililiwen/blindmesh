import { NextRequest, NextResponse } from "next/server";

/**
 * The browser talks to the API through the same origin path /api/v1. The proxy
 * strips the /api prefix and forwards the request to the API, so the browser
 * never needs the address of the service. The API address is read at request
 * time, so the same image runs in every environment.
 */
const apiBaseUrl = () => process.env.BLINDMESH_API_URL ?? "http://127.0.0.1:8080";

const forwardedRequestHeaders = [
  "accept",
  "content-type",
  "cookie",
  "idempotency-key",
  "origin",
  "x-csrf-token",
  "x-request-id",
];

type RouteContext = { params: Promise<{ path: string[] }> };

async function proxy(request: NextRequest, context: RouteContext): Promise<Response> {
  const { path } = await context.params;
  const target = new URL(`${apiBaseUrl()}/${path.join("/")}`);

  request.nextUrl.searchParams.forEach((value, key) => target.searchParams.append(key, value));

  const headers = new Headers();

  for (const name of forwardedRequestHeaders) {
    const value = request.headers.get(name);

    if (value !== null) {
      headers.set(name, value);
    }
  }

  const hasBody = request.method !== "GET" && request.method !== "HEAD";

  const response = await fetch(target, {
    method: request.method,
    headers,
    body: hasBody ? await request.text() : undefined,
    cache: "no-store",
    redirect: "manual",
  });

  const payload = await response.text();

  const responseHeaders = new Headers({
    "content-type": response.headers.get("content-type") ?? "application/json",
    "x-request-id": response.headers.get("x-request-id") ?? "",
  });

  // The sign in flow is a chain of redirects. The location of the redirect
  // belongs to the browser, so it is passed through.
  const location = response.headers.get("location");

  if (location !== null) {
    responseHeaders.set("location", location);
  }

  // The session cookie of the API belongs to the browser of the visitor, so
  // it is passed through in both directions.
  for (const cookie of response.headers.getSetCookie()) {
    responseHeaders.append("set-cookie", cookie);
  }

  return new NextResponse(payload, {
    status: response.status,
    headers: responseHeaders,
  });
}

export async function GET(request: NextRequest, context: RouteContext) {
  return proxy(request, context);
}

export async function HEAD(request: NextRequest, context: RouteContext) {
  return proxy(request, context);
}

export async function POST(request: NextRequest, context: RouteContext) {
  return proxy(request, context);
}

export async function PUT(request: NextRequest, context: RouteContext) {
  return proxy(request, context);
}

export async function PATCH(request: NextRequest, context: RouteContext) {
  return proxy(request, context);
}

export async function DELETE(request: NextRequest, context: RouteContext) {
  return proxy(request, context);
}
