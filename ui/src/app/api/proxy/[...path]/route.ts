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
  const body = ["GET", "HEAD"].includes(request.method) ? undefined : await request.text();

  const jar = await cookies();
  let accessToken = jar.get(ACCESS_COOKIE)?.value;
  const refreshToken = jar.get(REFRESH_COOKIE)?.value;
  let refreshed: AuthResult | null = null;

  const send = (token: string | undefined) =>
    fetch(target, {
      method: request.method,
      headers: {
        ...(body !== undefined ? { "Content-Type": request.headers.get("content-type") ?? "application/json" } : {}),
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
    headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" },
  });
  if (refreshed) setSessionCookies(res, refreshed);
  else if (upstream.status === 401) clearSessionCookies(res);
  return res;
}

export { forward as GET, forward as POST, forward as PUT, forward as PATCH, forward as DELETE };
