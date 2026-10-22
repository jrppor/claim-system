import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import {
  ACCESS_COOKIE,
  apiBase,
  clearSessionCookies,
  REFRESH_COOKIE,
  refreshWithApi,
  setSessionCookies,
  type AuthResult,
} from "@/lib/session";

// Authenticated pass-through to the Claim API: the browser calls /api/proxy/<api path>, this handler
// adds the bearer token from the httpOnly cookie, and refreshes (rotating) once on expiry.

type Ctx = { params: Promise<{ path: string[] }> };

async function forward(request: Request, ctx: Ctx) {
  const { path } = await ctx.params;
  const search = new URL(request.url).search;
  const target = `${apiBase}/${path.map(encodeURIComponent).join("/")}${search}`;
  // Raw bytes, not text: a multipart upload (Claim Photo) is binary and must reach the API byte for byte, with its
  // original Content-Type (which carries the multipart boundary). Route handlers have no body-size limit of their own;
  // the API enforces the 10 MB-per-photo rule and a request-size cap (413). Known limit: the whole body is buffered in
  // memory here (up to ~200 MB for a full upload) before it is forwarded; streaming it through is not done.
  const body = ["GET", "HEAD"].includes(request.method) ? undefined : await request.arrayBuffer();

  const jar = await cookies();
  let accessToken = jar.get(ACCESS_COOKIE)?.value;
  const refreshToken = jar.get(REFRESH_COOKIE)?.value;
  let refreshed: AuthResult | null = null;

  const send = (token: string | undefined) =>
    fetch(target, {
      method: request.method,
      headers: {
        ...(body !== undefined ? { "Content-Type": request.headers.get("content-type") ?? "application/json" } : {}),
        ...(request.headers.get("if-match") ? { "If-Match": request.headers.get("if-match")! } : {}),
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body,
      cache: "no-store",
    });

  if (!accessToken && refreshToken) {
    refreshed = await refreshWithApi(refreshToken);
    accessToken = refreshed?.accessToken;
  }

  let upstream = await send(accessToken);
  if (upstream.status === 401 && !refreshed && refreshToken) {
    refreshed = await refreshWithApi(refreshToken);
    if (refreshed) upstream = await send(refreshed.accessToken);
  }

  const res = new NextResponse(upstream.status === 204 ? null : await upstream.arrayBuffer(), {
    status: upstream.status,
    headers: {
      "Content-Type": upstream.headers.get("content-type") ?? "application/json",
      ...(upstream.headers.get("x-content-type-options") ? { "X-Content-Type-Options": upstream.headers.get("x-content-type-options")! } : {}),
    },
  });
  if (refreshed) setSessionCookies(res, refreshed);
  else if (upstream.status === 401) clearSessionCookies(res);
  return res;
}

export { forward as GET, forward as POST, forward as PUT, forward as PATCH, forward as DELETE };
